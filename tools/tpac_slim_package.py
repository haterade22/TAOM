#!/usr/bin/env python3
"""Make a slimmed copy of one tpac package: drop named Metamesh items and whatever only they used,
re-point the texture source paths, optionally give the copy a fresh package GUID. Dry run by
default; `--apply` writes; the source is never overwritten.

WHY. A third-party pack (the upstream chariot pack) ships one package holding the rig, its 75 clips
and 54 skeletal animations next to meshes, materials and textures we do not want. The animation and
skeleton items are the part we need, and a Kit re-import of them has broken rigs before, so this
tool cuts the unwanted items out of the compiled package and keeps every other item byte for byte.

WHAT IT DOES, in order:
  1. Round-trip gate: parse the source and re-serialise it unchanged. Anything but byte-identical
     stops the run and names the first differing offset (tools/README.md, "Mandatory before trusting
     any of the writers"). It uses tpac_clone_metamesh's packed writer.
  2. `--drop-mesh NAME` (repeatable) removes those Metamesh items. A Material that no remaining
     Metamesh references is removed, then a Texture that no remaining Material references. A
     reference is the 16-byte item GUID inside the referrer's metadata. A Skeleton, SkeletalAnimation
     or AnimationClip is never removed. A kept item whose metadata or dependency table still names
     a removed item makes the run refuse.
  3. `--texture-source-root TEXT` rewrites the `$BASE/Modules/...` source image path stored in every
     kept Texture to `TEXT/<file>`. The path is an int32-length-prefixed string inside the metadata;
     the int64 metadata length and the item checksum (xxHash64 seed 0 over length plus metadata) are
     recomputed, nothing else in the item changes.
  4. `--new-guid` gives the output a fresh package GUID. Item and segment GUIDs are unchanged.
  5. The output is re-parsed and checked: every kept item has the same segment sizes, hashes and
     bytes as in the plan, the header TOC size equals the real TOC size, and every Material a kept
     Metamesh references and every Texture a kept Material references is present.
  6. `--forbid TEXT` (repeatable, ASCII) refuses an output that contains TEXT, case-insensitive, as
     ASCII or UTF-16, in the file's bytes or in the unpacked payload of any segment. It covers the
     package only: the RuntimeDataCache entry that the Modding Kit later writes for it is another file.
  7. With `--out` and `--apply` the output is written (never over a file), read back and compared. A
     run that fails at any step leaves no file.

A package this tool writes is invisible to the shipping game until the Modding Kit has saved the
module once (its RuntimeDataCache entry is named after the package GUID); see tpac_clone_metamesh.py.

Usage:
  python tools/tpac_slim_package.py <src.tpac> --out <new.tpac> --drop-mesh NAME [--drop-mesh NAME ...] \\
      --texture-source-root '$BASE/Modules/<Module>/AssetSources/<folders>' --new-guid \\
      --forbid <text> [--apply]
Exit 0 on success, 1 on a refusal or a failed check.
"""
from __future__ import annotations

import argparse
import os
import struct
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tpac_clone_metamesh as tcm  # noqa: E402

MATERIAL_TYPE_GUID = uuid.UUID("1db01393-6902-4f19-83ba-b37a39830717").bytes_le
TEXTURE_TYPE_GUID = uuid.UUID("c974cbcb-5f1c-49f6-9a32-2b5b6c92c2e8").bytes_le
PROTECTED_TYPE_GUIDS = {
    "Skeleton": uuid.UUID("c635a3d5-eabb-45dd-883e-aa57e4196113").bytes_le,
    "SkeletalAnimation": uuid.UUID("bafab007-7e3f-453f-bac6-e7640043112b").bytes_le,
    "AnimationClip": uuid.UUID("506509c8-e563-4ca4-b166-a53b92e913a7").bytes_le,
}
TYPE_NAMES = {
    tcm.METAMESH_TYPE_GUID: "Metamesh",
    MATERIAL_TYPE_GUID: "Material",
    TEXTURE_TYPE_GUID: "Texture",
    **{g: n for n, g in PROTECTED_TYPE_GUIDS.items()},
}
PATH_MARK = b"$BASE/"


class SlimError(Exception):
    """A refusal: nothing is written."""


def type_name(item: tcm.Item) -> str:
    return TYPE_NAMES.get(item.type_guid, "Other " + item.type_guid.hex())


def metadata(item: tcm.Item) -> bytes:
    """The metadata bytes, without their int64 length prefix."""
    start, end = tcm._metadata_span(item)
    return bytes(item.toc[start + 8:end])


def references(item: tcm.Item, targets: dict[bytes, tcm.Item]) -> set[bytes]:
    """The GUIDs in `targets` that appear inside the item's metadata."""
    meta = metadata(item)
    return {g for g in targets if g in meta}


def first_difference(a: bytes, b: bytes) -> int:
    for i in range(min(len(a), len(b))):
        if a[i] != b[i]:
            return i
    return min(len(a), len(b))


def roundtrip_gate(data: bytes) -> tcm.Package:
    try:
        pkg = tcm.parse(data)
        again = tcm.serialize(pkg.package_guid, pkg.items, pkg.version)     # a cut-off payload fails here
    except (tcm.CloneError, struct.error) as exc:
        raise SlimError(f"source does not parse: {exc}")
    if again != data:
        raise SlimError(f"round-trip gate failed: re-serialised {len(again)} bytes against {len(data)}, "
                        f"first differing offset {first_difference(data, again)}")
    return pkg


@dataclass
class Plan:
    kept: list[tcm.Item]
    removed: list[tcm.Item]


def plan_slim(pkg: tcm.Package, drop_names: list[str]) -> Plan:
    metas = [it for it in pkg.items if it.type_guid == tcm.METAMESH_TYPE_GUID]
    for name in drop_names:
        hits = [it for it in metas if it.name == name]
        if len(hits) != 1:
            raise SlimError(f"--drop-mesh {name!r}: {len(hits)} Metamesh items by that name (need exactly 1)")
    dropped = {it.item_guid for it in metas if it.name in set(drop_names)}
    kept_meshes = [it for it in metas if it.item_guid not in dropped]
    materials = {it.item_guid: it for it in pkg.items if it.type_guid == MATERIAL_TYPE_GUID}
    textures = {it.item_guid: it for it in pkg.items if it.type_guid == TEXTURE_TYPE_GUID}

    used_materials: set[bytes] = set()
    for mesh in kept_meshes:
        used_materials |= references(mesh, materials)
    used_textures: set[bytes] = set()
    for guid in used_materials:
        used_textures |= references(materials[guid], textures)

    gone: set[bytes] = set(dropped)
    gone |= {g for g in materials if g not in used_materials}
    gone |= {g for g in textures if g not in used_textures}
    kept = [it for it in pkg.items if it.item_guid not in gone]
    removed = [it for it in pkg.items if it.item_guid in gone]

    for it in kept:
        for g in gone:
            if bytes(it.toc).count(g):
                other = next(r for r in removed if r.item_guid == g)
                raise SlimError(f"kept {type_name(it)} {it.name!r} still names removed {type_name(other)} "
                                f"{other.name!r}; drop it too or keep both")
    for it in removed:
        if it.type_guid in PROTECTED_TYPE_GUIDS.values():
            raise SlimError(f"would remove protected {type_name(it)} {it.name!r}")
    return Plan(kept, removed)


def _rebuild(item: tcm.Item, new_toc: bytes | bytearray) -> tcm.Item:
    return tcm._reparse(new_toc, item.blobs)


def rewrite_texture_path(item: tcm.Item, root: str) -> tcm.Item:
    """Replace the `$BASE/...` source path in a Texture's metadata by `root/<file name>`, fixing the
    string length, the int64 metadata length and the item checksum."""
    base = texture_path(item).replace("\\", "/").rsplit("/", 1)[-1]
    return set_texture_path(item, root.rstrip("/") + "/" + base)


def _path_span(item: tcm.Item) -> tuple[int, int, int]:
    """Where the single `$BASE/...` string of a Texture is in its TOC: (length prefix at, string at, length)."""
    start, end = tcm._metadata_span(item)
    meta_start = start + 8
    toc = bytes(item.toc)
    found = []
    pos = meta_start
    while True:
        pos = toc.find(PATH_MARK, pos, end)
        if pos < 0:
            break
        if pos - 4 >= meta_start:
            n = struct.unpack_from("<i", toc, pos - 4)[0]
            if 0 < n and pos + n <= end:
                found.append((pos - 4, pos, n))
        pos += 1
    if len(found) != 1:
        raise SlimError(f"Texture {item.name!r}: {len(found)} length-prefixed '$BASE/' strings in its metadata (need 1)")
    return found[0]


def set_texture_path(item: tcm.Item, new_path: str) -> tcm.Item:
    """Replace the single `$BASE/...` string in a Texture's metadata by `new_path` (any length)."""
    len_at, str_at, n = _path_span(item)
    start = tcm._metadata_span(item)[0]
    toc = bytes(item.toc)
    new = new_path.encode("utf-8")
    new_toc = bytearray(toc[:len_at] + struct.pack("<i", len(new)) + new + toc[str_at + n:])
    delta = len(new) - n
    old_meta_len = struct.unpack_from("<q", new_toc, start)[0]
    struct.pack_into("<q", new_toc, start, old_meta_len + delta)
    out = _rebuild(item, new_toc)
    end = tcm._metadata_span(out)[1]
    out.toc[end:end + 8] = tcm.expected_checksum(out)
    return _rebuild(out, out.toc)


def texture_path(item: tcm.Item) -> str:
    _, str_at, n = _path_span(item)
    return bytes(item.toc[str_at:str_at + n]).decode("utf-8")


def contains_text(data: bytes, text: str) -> list[str]:
    """The encodings of the ASCII `text` found in `data`, case-insensitive (ASCII, UTF-16 LE and BE). Lowering
    the bytes is exact for ASCII letters in all three encodings; it is wrong for any other letter."""
    if not text or not text.isascii():
        raise SlimError(f"{text!r} is not a text this search can prove absent: it takes ASCII, at least one character")
    low = data.lower()
    return [label for label, enc in (("ascii", "ascii"), ("utf-16le", "utf-16-le"), ("utf-16be", "utf-16-be"))
            if text.lower().encode(enc) in low]


def forbidden_hits(out_bytes: bytes, text: str) -> list[str]:
    """Where `text` is in the output: its bytes, and the unpacked payload of every segment. LZ4 stores a
    repeated run as a back reference, so a text can be in a payload and in no byte run of the file.

    Every segment is tried as LZ4, whatever its two sizes say: the Kit packs a segment even when that saves
    nothing, so "stored size equals unpacked size" does not mean "stored raw" (the storage format is the last
    byte of the segment's entry: 0 raw, 1 LZ4). A segment that is marked packed and does not unpack is refused;
    a raw one that does not unpack was already searched as bytes."""
    try:
        import lz4.block
    except ImportError as exc:
        raise SlimError(f"--forbid needs the lz4 module to read packed segments: {exc}")
    hits = contains_text(out_bytes, text)
    for item in tcm.parse(out_bytes).items:
        for k, seg in enumerate(item.segments):
            marked = seg.actual != seg.storage or item.toc[seg.entry_pos + tcm.SEG_ENTRY_SIZE - 1] != 0
            try:
                payload = lz4.block.decompress(item.blobs[k], uncompressed_size=seg.actual)
            except (lz4.block.LZ4BlockError, ValueError) as exc:
                if marked:
                    raise SlimError(f"{item.name} segment {k} is marked packed and does not unpack: {exc}")
                continue
            hits += [f"{item.name} segment {k}, {h}" for h in contains_text(payload, text)]
    return hits


def verify_output(src: tcm.Package, out_bytes: bytes, plan: Plan) -> list[str]:
    """Problems found re-reading the output; empty means it checks out."""
    bad: list[str] = []
    try:
        out = tcm.parse(out_bytes)
    except (tcm.CloneError, struct.error) as exc:
        return [f"output does not re-parse: {exc}"]
    if [(i.name, i.item_guid) for i in out.items] != [(i.name, i.item_guid) for i in plan.kept]:
        return ["output items differ from the plan"]
    header_toc = struct.unpack_from("<Q", out_bytes, 28)[0]
    real_toc = sum(len(i.toc) for i in out.items)       # parse walked the table item by item
    if header_toc != real_toc:
        bad.append(f"header TOC size {header_toc} != real TOC size {real_toc}")
    if tcm.serialize(out.package_guid, out.items, out.version) != out_bytes:
        bad.append("output does not re-serialise to itself")
    for a, b in zip(plan.kept, out.items):
        if len(a.segments) != len(b.segments):
            bad.append(f"{a.name}: {len(a.segments)} segments became {len(b.segments)}")
            continue
        for k, (sa, sb) in enumerate(zip(a.segments, b.segments)):
            same = (sa.actual, sa.storage, sa.guid, sa.tag) == (sb.actual, sb.storage, sb.guid, sb.tag)
            if not same or tcm.segment_hash(a, sa) != tcm.segment_hash(b, sb) or a.blobs[k] != b.blobs[k]:
                bad.append(f"{a.name}: segment {k} differs")
        # `a` is the planned item, a Texture with its new path included, so this holds for every item
        if b.checksum != a.checksum or bytes(b.toc) != bytes(a.toc) and not _only_offsets_differ(a, b):
            bad.append(f"{a.name}: the written item differs from the planned one")
    materials = {it.item_guid: it for it in src.items if it.type_guid == MATERIAL_TYPE_GUID}
    textures = {it.item_guid: it for it in src.items if it.type_guid == TEXTURE_TYPE_GUID}
    present = {i.item_guid for i in out.items}
    for it in out.items:
        if it.type_guid == tcm.METAMESH_TYPE_GUID:
            for g in references(it, materials):
                if g not in present:
                    bad.append(f"{it.name}: its material {materials[g].name} is missing")
        if it.type_guid == MATERIAL_TYPE_GUID:
            for g in references(it, textures):
                if g not in present:
                    bad.append(f"{it.name}: its texture {textures[g].name} is missing")
    return bad


def _only_offsets_differ(a: tcm.Item, b: tcm.Item) -> bool:
    """Segment data offsets move when items are removed; every other TOC byte must match."""
    ta, tb = bytearray(a.toc), bytearray(b.toc)
    for sa in a.segments:
        struct.pack_into("<Q", ta, sa.entry_pos, 0)
    for sb in b.segments:
        struct.pack_into("<Q", tb, sb.entry_pos, 0)
    return ta == tb


def slim(data: bytes, drop_names: list[str], texture_root: str | None,
         new_guid: bool) -> tuple[bytes, tcm.Package, Plan]:
    pkg = roundtrip_gate(data)
    plan = plan_slim(pkg, drop_names)
    if texture_root is not None:
        plan = Plan([rewrite_texture_path(it, texture_root) if it.type_guid == TEXTURE_TYPE_GUID else it
                     for it in plan.kept], plan.removed)
    guid = uuid.uuid4().bytes if new_guid else pkg.package_guid
    return tcm.serialize(guid, plan.kept, pkg.version), pkg, plan


def summary(label: str, items: list[tcm.Item]) -> None:
    counts: dict[str, int] = {}
    for it in items:
        counts[type_name(it)] = counts.get(type_name(it), 0) + 1
    print(f"  {label}: {len(items)} items  " + ", ".join(f"{n} {t}" for t, n in sorted(counts.items())))
    for t in ("Metamesh", "Material", "Texture"):
        names = [it.name for it in items if type_name(it) == t]
        if names:
            print(f"    {t}: {', '.join(names)}")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("source", help="the tpac to slim (read only)")
    ap.add_argument("--out", help="the new tpac to write (never overwritten)")
    ap.add_argument("--drop-mesh", action="append", default=[], metavar="NAME", help="a Metamesh item to remove; repeatable")
    ap.add_argument("--texture-source-root", metavar="TEXT", help="new folder for every kept Texture's source image path")
    ap.add_argument("--new-guid", action="store_true", help="give the output a fresh package GUID")
    ap.add_argument("--forbid", action="append", default=[], metavar="TEXT", help="fail when the output contains TEXT; repeatable")
    ap.add_argument("--apply", action="store_true", help="write the output (default: dry run)")
    args = ap.parse_args(argv)

    src_path = Path(args.source)
    try:
        if args.apply and not args.out:
            raise SlimError("--apply needs --out")
        if args.out:
            out_path = Path(args.out)
            if out_path.resolve() == src_path.resolve() or (out_path.exists() and src_path.exists() and os.path.samefile(out_path, src_path)):
                raise SlimError("--out is the source; refusing to overwrite it")
            if args.apply and out_path.exists():
                raise SlimError(f"{out_path} already exists; refusing to overwrite it")
        data = src_path.read_bytes()
        out, pkg, plan = slim(data, args.drop_mesh, args.texture_source_root, args.new_guid)
        print(f"  round-trip: byte-identical ({len(data)} bytes)")
        summary("kept", plan.kept)
        summary("removed", plan.removed)
        problems = verify_output(pkg, out, plan)
        if problems:
            raise SlimError("verification failed:\n    " + "\n    ".join(problems))
        # the id as a RuntimeDataCache entry is named (tools/check_rdc_entries.py)
        print(f"  verified: segments, hashes, TOC size, references ({len(out)} bytes, "
              f"package {uuid.UUID(bytes_le=out[8:24])})")
        for text in args.forbid:
            hits = forbidden_hits(out, text)
            if hits:
                raise SlimError(f"output contains forbidden text {text!r} ({'; '.join(hits)})")
        if args.forbid:
            print(f"  forbid: {len(args.forbid)} text(s) absent (ascii, utf-16; file bytes and unpacked segments)")
    except (SlimError, OSError) as exc:
        print(f"refusing: {exc}")
        return 1

    if not args.apply:
        print("  (dry run; pass --out and --apply to write)")
        return 0
    created = False
    try:
        with open(out_path, "xb") as fh:        # "x": never over a file that appeared since the check above
            created = True
            fh.write(out)
        if out_path.read_bytes() != out:
            raise SlimError("the written file differs from the verified bytes")
    except (SlimError, OSError) as exc:
        if created:                             # only a file this run made: a refused run leaves none
            out_path.unlink(missing_ok=True)
        print(f"refusing: {exc}")
        return 1
    print(f"  written: {out_path}  {len(out)} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
