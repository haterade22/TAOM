#!/usr/bin/env python3
"""Clone a metamesh inside a Bannerlord `*_geo.tpac` under a new name, bound to a different
material, into a NEW tpac. Dry run by default; `--apply` writes; never overwrites.

WHY THIS EXISTS. The giant spider's three skins are one body with three materials
(`m_mordor_spider_a1/a2/a3`). The engine's own recolour path, `<Materials>` on a Horse item,
reaches only sub-meshes tagged `horse_body` on the BASE mesh (`MountVisualCreator.
SetMaterialProperties`, TaleWorlds.MountAndBlade.View) and never an `AdditionalMeshes` half, so
for a mesh authored without that tag the block is a silent no-op. A Kit re-import is the other
route and has broken this creature before (docs/features/spider.md, 2026-06-13). Cloning the
proven, in-game-tested mesh items from files sidesteps both: the live bundle is never touched and
the clone is byte-for-byte the tested geometry with a different material reference.

FORMAT (verified 2026-09-17 against spider_correct_geo.tpac, the tpac_skeleton_inject.py output the
game loads today):
    0   "TPAC", uint32 version (2)
    8   16-byte package GUID
    24  uint32 item count
    28  uint64 TOC size; the engine derives data_start = 36 + toc_size from it
    36  TOC entries, then every segment's data laid out in TOC order
  Item entry: type GUID(16) item GUID(16) [uint32 when version > 1] int32 name-len + name
              int64 meta-len + metadata, 8-byte checksum, int32 seg count, seg entries (69 each),
              int32 user-data count + 48 bytes each
  Segment entry: uint64 offset, uint64 actual size, uint64 storage size, segment GUID(16),
              segment TYPE guid(16; its first 4 bytes are the tag below), uint64 xxHash64 (seed 0)
              of the DECOMPRESSED payload, 4 zero bytes, 1 byte
  Item checksum: uint64 xxHash64 (seed 0) over the int64 metadata length plus the metadata bytes.
  Both formulas verified on every item and segment of the live spider bundle (2026-09-17).
  A metamesh (type 978b8fa0...) has one geometry segment per LOD (tag 5f98413d) plus one LZ4
  "binding" segment (tag f6304064) listing `[count][len+meshname][len+material]` per LOD by NAME.
  Its metadata carries, per LOD, the segment GUID, the length-prefixed LOD name and the material's
  ITEM GUID. Nothing in a mesh item references the skeleton, so clones can ship in their own tpac.

What a clone changes, and only that: a same-length name (TOC name field, every metadata LOD name,
the binding segment), a same-length material name in the binding segment, the material item GUID in
the metadata, a fresh item GUID, package GUID and segment GUIDs (a GUID collision between two
loaded items crashed the engine in 2026-06-14, see tpac_skeleton_extract.py), and the two hashes
recomputed: the binding segment's xxHash64 and the item checksum. Geometry segments are copied
verbatim with their hashes. THE HASHES ARE LOAD-BEARING: the first cut of this tool kept c's
values and the clones rendered invisible in game (2026-09-17, #616), the engine keying segment data
by content hash so the rewritten bindings never reached the clone.

Usage:
  python tools/tpac_clone_metamesh.py <src_geo.tpac> --out <new_geo.tpac> \
      --clone sk_spider_forest_c=sk_spider_forest_a,m_mordor_spider_a3=m_mordor_spider_a1 \
      --clone sk_spider_forest_c_2=sk_spider_forest_a_2,m_mordor_spider_a3=m_mordor_spider_a1 \
      [--material-dir <folder of *_mtl.tpac>] [--apply]

Material GUIDs are read from the `_mtl.tpac` files' own TOC (the item named exactly as given), never
from filenames. Validate the output with tools/tpac_skeleton_scan.py and, in game, a Custom Battle.
"""
from __future__ import annotations

import argparse
import struct
import sys
import uuid
from dataclasses import dataclass, field
from pathlib import Path

import lz4.block
import xxhash

MAGIC = b"TPAC"
HEADER_SIZE = 36
SEG_ENTRY_SIZE = 69
USERDATA_ENTRY_SIZE = 48
METAMESH_TYPE_GUID = bytes.fromhex("978b8fa07c19ea4bb95b53846cae834e")
BINDING_SEG_TAG = bytes.fromhex("f6304064")


class CloneError(Exception):
    """A refusal: the request does not fit the file, so nothing is written."""


@dataclass
class Segment:
    entry_pos: int          # position of this segment's 69-byte entry inside the item TOC
    offset: int             # data offset inside the file the item was parsed from
    actual: int             # uncompressed size
    storage: int            # bytes on disk (== actual when the segment is stored raw)
    guid: bytes
    tag: bytes

    @property
    def is_binding(self) -> bool:
        return self.tag == BINDING_SEG_TAG

    @property
    def is_compressed(self) -> bool:
        return self.actual != self.storage


@dataclass
class Item:
    toc: bytearray
    name: str
    type_guid: bytes
    item_guid: bytes
    checksum: bytes
    segments: list[Segment]
    blobs: list[bytes] = field(default_factory=list)   # stored bytes, one per segment

    @property
    def is_metamesh(self) -> bool:
        return self.type_guid == METAMESH_TYPE_GUID


@dataclass
class Package:
    package_guid: bytes
    version: int
    items: list[Item]


def _sized(name: str | bytes) -> bytes:
    b = name if isinstance(name, bytes) else name.encode("ascii")
    return struct.pack("<i", len(b)) + b


def _read_item(data: bytes, pos: int, version: int) -> tuple[Item, int]:
    start = pos
    type_guid = data[pos:pos + 16]
    item_guid = data[pos + 16:pos + 32]
    pos += 32
    if version > 1:
        pos += 4
    name_len = struct.unpack_from("<i", data, pos)[0]
    pos += 4
    name = data[pos:pos + name_len].decode("utf-8", "replace")
    pos += name_len
    meta_len = struct.unpack_from("<q", data, pos)[0]
    pos += 8 + meta_len
    checksum = data[pos:pos + 8]
    pos += 8
    seg_count = struct.unpack_from("<i", data, pos)[0]
    pos += 4
    segments = []
    for _ in range(seg_count):
        offset, actual, storage = struct.unpack_from("<QQQ", data, pos)
        segments.append(Segment(pos - start, offset, actual, storage, data[pos + 24:pos + 40], data[pos + 40:pos + 44]))
        pos += SEG_ENTRY_SIZE
    userdata = struct.unpack_from("<i", data, pos)[0]
    pos += 4 + userdata * USERDATA_ENTRY_SIZE
    return Item(bytearray(data[start:pos]), name, type_guid, item_guid, checksum, segments), pos


def parse(data: bytes) -> Package:
    if data[:4] != MAGIC:
        raise CloneError(f"not a tpac (magic is {data[:4]!r})")
    version = struct.unpack_from("<I", data, 4)[0]
    if version != 2:
        raise CloneError(f"tpac version {version}; this tool knows the version-2 item layout only")
    package_guid = data[8:24]
    count = struct.unpack_from("<I", data, 24)[0]
    pos = HEADER_SIZE
    items = []
    for _ in range(count):
        item, pos = _read_item(data, pos, version)
        item.blobs = [data[s.offset:s.offset + s.storage] for s in item.segments]
        items.append(item)
    return Package(package_guid, version, items)


def _reparse(toc: bytes | bytearray, blobs: list[bytes], version: int = 2) -> Item:
    item, end = _read_item(bytes(toc), 0, version)
    if end != len(toc):
        raise CloneError(f"item TOC for {item.name!r} did not re-parse to its own length ({end} != {len(toc)})")
    item.blobs = list(blobs)
    return item


def serialize(package_guid: bytes, items: list[Item], version: int = 2) -> bytes:
    """Header + TOC + data, offsets recomputed, data in TOC order. Round-trips a parsed file byte
    for byte (the writer contract in docs/ai-includes/creature-mount-authoring.md)."""
    tocs = [bytearray(it.toc) for it in items]
    toc_size = sum(len(t) for t in tocs)
    cur = HEADER_SIZE + toc_size
    blobs = []
    for it, toc in zip(items, tocs):
        if len(it.blobs) != len(it.segments):
            raise CloneError(f"{it.name!r}: {len(it.blobs)} blobs for {len(it.segments)} segments")
        for seg, blob in zip(it.segments, it.blobs):
            if len(blob) != seg.storage:
                raise CloneError(f"{it.name!r}: segment blob is {len(blob)} bytes, entry says {seg.storage}")
            struct.pack_into("<Q", toc, seg.entry_pos, cur)
            blobs.append(blob)
            cur += len(blob)
    header = MAGIC + struct.pack("<I", version) + package_guid + struct.pack("<I", len(items)) + struct.pack("<Q", toc_size)
    return header + b"".join(bytes(t) for t in tocs) + b"".join(blobs)


def segment_bytes(item: Item, seg: Segment) -> bytes:
    return item.blobs[item.segments.index(seg)]


def segment_payload(item: Item, seg: Segment) -> bytes:
    blob = segment_bytes(item, seg)
    if not seg.is_compressed:
        return blob
    return lz4.block.decompress(blob, uncompressed_size=seg.actual)


def segment_hash(item: Item, seg: Segment) -> int:
    """The uint64 at entry offset 56: xxHash64 (seed 0) of the decompressed payload."""
    return struct.unpack_from("<Q", item.toc, seg.entry_pos + 56)[0]


def _metadata_span(item: Item) -> tuple[int, int]:
    """(start, end) of the int64-length-prefixed metadata inside a version-2 item TOC:
    type guid(16) item guid(16) uint32 version field, then the sized name, then the metadata."""
    pos = 36
    name_len = struct.unpack_from("<i", item.toc, pos)[0]
    pos += 4 + name_len
    meta_len = struct.unpack_from("<q", item.toc, pos)[0]
    return pos, pos + 8 + meta_len


def expected_checksum(item: Item) -> bytes:
    """xxHash64 (seed 0) over the metadata length prefix plus the metadata, as the Kit writes it."""
    start, end = _metadata_span(item)
    return struct.pack("<Q", xxhash.xxh64(bytes(item.toc[start:end]), seed=0).intdigest())


def _replace_names(buf: bytes, old: str, new: str) -> tuple[bytes, int]:
    """Rewrite every length-prefixed occurrence of `old` or `old.lodN` to the same-length `new`
    form. Occurrences are recognised by the int32 length in front of them, so a name that merely
    shares a prefix with `old` (sk_spider_forest_c vs sk_spider_forest_c_2) is left alone."""
    old_b, new_b = old.encode("ascii"), new.encode("ascii")
    out = bytearray(buf)
    count = 0
    pos = 0
    while True:
        pos = out.find(old_b, pos)
        if pos < 0:
            break
        end = pos + len(old_b)
        length = struct.unpack_from("<i", out, pos - 4)[0] if pos >= 4 else -1
        tail = bytes(out[end:end + 4])
        exact = length == len(old_b)
        lod = length > len(old_b) + 4 and tail == b".lod" and out[end + 4:pos - 4 + 4 + length].isdigit()
        if exact or lod:
            out[pos:end] = new_b
            count += 1
        pos = end
    return bytes(out), count


def clone_metamesh(pkg: Package, src_name: str, new_name: str,
                   old_material: tuple[str, bytes], new_material: tuple[str, bytes]) -> Item:
    src = next((it for it in pkg.items if it.name == src_name), None)
    if src is None:
        raise CloneError(f"no item named {src_name!r} in the package")
    if not src.is_metamesh:
        raise CloneError(f"{src_name!r} is not a metamesh (type {src.type_guid.hex()})")
    if len(new_name) != len(src_name):
        raise CloneError(f"new name {new_name!r} must be as long as {src_name!r} ({len(src_name)} bytes)")
    old_mat_name, old_mat_guid = old_material
    new_mat_name, new_mat_guid = new_material
    if len(new_mat_name) != len(old_mat_name):
        raise CloneError(f"material name {new_mat_name!r} must be as long as {old_mat_name!r}")
    if len(old_mat_guid) != 16 or len(new_mat_guid) != 16:
        raise CloneError("material guids must be 16 bytes")
    if bytes(src.toc).count(old_mat_guid) == 0:
        raise CloneError(f"{src_name!r} does not bind material {old_mat_name!r} (guid {old_mat_guid.hex()} absent)")
    bindings = [s for s in src.segments if s.is_binding]
    if len(bindings) != 1:
        raise CloneError(f"{src_name!r} has {len(bindings)} binding segments, expected 1")

    toc, renamed = _replace_names(bytes(src.toc), src_name, new_name)
    if renamed < 1 or src_name.encode("ascii") in toc:
        raise CloneError(f"{src_name!r}: an occurrence of the name is not a name field or a LOD name; refusing")
    toc = toc.replace(old_mat_guid, new_mat_guid)

    new_item_guid = uuid.uuid4().bytes
    toc = toc.replace(src.item_guid, new_item_guid)
    for seg in src.segments:
        if seg.guid == src.item_guid:
            continue                      # the binding segment is keyed by the item guid, already swapped
        fresh = uuid.uuid4().bytes
        toc = toc.replace(seg.guid, fresh)

    blobs = list(src.blobs)
    toc = bytearray(toc)
    binding = bindings[0]
    raw = segment_payload(src, binding)
    raw, n_mesh = _replace_names(raw, src_name, new_name)
    raw, n_mat = _replace_names(raw, old_mat_name, new_mat_name)
    if n_mesh < 1 or n_mat < 1 or src_name.encode("ascii") in raw or old_mat_name.encode("ascii") in raw:
        raise CloneError(f"{src_name!r}: binding segment did not rename cleanly (mesh {n_mesh}, material {n_mat})")
    if len(raw) != binding.actual:
        raise CloneError("binding segment changed size; the substitutions were not same-length")
    if binding.is_compressed:
        stored = lz4.block.compress(raw, store_size=False)
        if lz4.block.decompress(stored, uncompressed_size=len(raw)) != raw:
            raise CloneError("recompressed binding segment does not round-trip")
    else:
        stored = raw
    idx = src.segments.index(binding)
    blobs[idx] = stored
    struct.pack_into("<Q", toc, binding.entry_pos + 16, len(stored))   # storage size field
    struct.pack_into("<Q", toc, binding.entry_pos + 56, xxhash.xxh64(raw, seed=0).intdigest())

    clone = _reparse(toc, blobs, pkg.version)
    start, end = _metadata_span(clone)
    clone.toc[end:end + 8] = expected_checksum(clone)
    clone = _reparse(clone.toc, blobs, pkg.version)
    if clone.name != new_name or clone.item_guid != new_item_guid:
        raise CloneError("clone did not re-parse to the requested name and guid")
    if clone.checksum != expected_checksum(clone) or segment_hash(clone, clone.segments[idx]) != xxhash.xxh64(raw, seed=0).intdigest():
        raise CloneError("clone hashes did not land")
    return clone


def find_material_guid(folder: Path, material_name: str) -> bytes:
    """The item GUID of the material named `material_name`, read from the TOC of whichever
    `*_mtl.tpac` under `folder` defines it. Filenames are not trusted: one shipped texture carries a
    space in its name and the Kit keys resources by GUID, not by file."""
    hits = []
    for path in sorted(Path(folder).glob("*_mtl.tpac")):
        try:
            pkg = parse(path.read_bytes())
        except (CloneError, struct.error):
            continue
        for it in pkg.items:
            if it.name == material_name:
                hits.append((path, it.item_guid))
    if not hits:
        raise CloneError(f"no *_mtl.tpac under {folder} defines material {material_name!r}")
    if len({g for _, g in hits}) > 1:
        raise CloneError(f"material {material_name!r} is defined with different guids: {[str(p) for p, _ in hits]}")
    return hits[0][1]


def _parse_clone_spec(spec: str) -> tuple[str, str, str, str]:
    try:
        mesh_part, mat_part = spec.split(",")
        src, new = mesh_part.split("=")
        old_mat, new_mat = mat_part.split("=")
    except ValueError:
        raise CloneError(f"--clone expects src=new,oldmat=newmat, got {spec!r}")
    return src.strip(), new.strip(), old_mat.strip(), new_mat.strip()


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("source", help="the *_geo.tpac holding the mesh to clone")
    ap.add_argument("--out", required=True, help="the NEW tpac to write (never overwritten)")
    ap.add_argument("--clone", action="append", required=True, metavar="SRC=NEW,OLDMAT=NEWMAT",
                    help="one clone: same-length mesh name and same-length material name; repeatable")
    ap.add_argument("--material-dir", default=None,
                    help="folder of *_mtl.tpac files (default: <source dir>/../textures)")
    ap.add_argument("--apply", action="store_true", help="write the output (default: dry run)")
    args = ap.parse_args(argv)

    src_path = Path(args.source)
    out_path = Path(args.out)
    mat_dir = Path(args.material_dir) if args.material_dir else src_path.parent.parent / "textures"
    try:
        pkg = parse(src_path.read_bytes())
        clones = []
        for spec in args.clone:
            src, new, old_mat, new_mat = _parse_clone_spec(spec)
            old_guid = find_material_guid(mat_dir, old_mat)
            new_guid = find_material_guid(mat_dir, new_mat)
            clone = clone_metamesh(pkg, src, new, (old_mat, old_guid), (new_mat, new_guid))
            clones.append(clone)
            print(f"  {src} -> {new}  material {old_mat} ({old_guid.hex()[:8]}) -> {new_mat} ({new_guid.hex()[:8]})"
                  f"  item {clone.item_guid.hex()[:8]}  segments {len(clone.segments)}")
        if len({c.name for c in clones}) != len(clones):
            raise CloneError("two clones share a name")
        out = serialize(uuid.uuid4().bytes, clones, pkg.version)
        back = parse(out)
        if [it.name for it in back.items] != [c.name for c in clones] or serialize(back.package_guid, back.items, back.version) != out:
            raise CloneError("output does not re-parse to itself")
    except (CloneError, OSError) as exc:
        print(f"refusing: {exc}")
        return 1

    print(f"  output: {out_path}  {len(out)} bytes, {len(clones)} items, package {out[8:24].hex()[:8]}")
    if not args.apply:
        print("  (dry run; pass --apply to write)")
        return 0
    if out_path.exists():
        print(f"refusing: {out_path} already exists")
        return 1
    out_path.write_bytes(out)
    print("  written")
    return 0


if __name__ == "__main__":
    sys.exit(main())
