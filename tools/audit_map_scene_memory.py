#!/usr/bin/env python3
"""Attribute the campaign map scene's asset payload, asset by asset, from the scene's own files.

WHY THIS EXISTS. The campaign map scene costs about 4.4 GB of committed memory (the engine
releases it on every mission entry and rebuilds it on return), and the engine gives no
per-asset accounting: VMMap shows anonymous regions, the engine's own stats API returns one
number. So the map layer has to be attributed OFFLINE, from what the scene references and
what each referenced asset weighs inside the cooked `AssetPackages/*.tpac` containers.

Read-only on every input. Never writes into the game install (the output root is refused
if it lies under the game directory).

WHAT IT READS
  <scene>/references.txt       the engine-written manifest (`<kind> <name>` per line)
  <scene>/scene.xscene         entities, prefab instances, terrain layers, water
  <scene>/atmosphere.xml       skybox and lens textures
  <scene>/flora.bin            flora instance records (walked, see below)
  <scene>/*.bin, flowmap.dds   sizes as-is (the DDS header is decoded, it is a standard header)
  <module>/Prefabs/*.xml       prefab definitions, TAOM_Map first, then Native, then SandBox
  <module>/AssetPackages/*.tpac and <module>/EmAssetPackages/**/*.tpac
                               only the table of contents; no segment is ever decompressed
  Native/ModuleData/flora_kinds.xml (and any other module's) for flora kind -> mesh

CONTAINER LAYOUT (mirrors tools/tpac_skeleton_dump.py, which walks the same table)
  header: "TPAC", u32 version, 16-byte package guid, u32 item count, 8 bytes
  item:   type guid(16) item guid(16) [u32 item version if container version > 1]
          sized name, i64 metadata size, metadata, 8 bytes, i32 segment count,
          segments (69 bytes each: u64 offset, u64 actual size, u64 stored size,
          owner guid, segment type guid, u64, u32, u8 storage format), i32 dep count, deps*48

DECODED METADATA LAYOUTS (every one verified against the installed packs, 2026-09-12)

  Texture (type guid cbcb74c9..., version 3), offsets relative to the metadata start:
    u32 version(3); 20 bytes; sized source path; 8 bytes; u8; u32; u32 flag count and
    that many sized strings ("for_terrain", "dont_degrade"); u32; u8; u32 width;
    u32 height; u32 (always 1); u8 mip count; u8 face count (1, or 6 for a cubemap); u8;
    sized format string ("DXT1", "DXT5", "BC4", "BC5", "BC7", "BC6H_UF16",
    "R8G8B8A8_UNORM", ...); u32; u32 flag count and strings ("has_alpha"); sized "none".
    Verified: for all 3,583 textures across TAOM_Map, Native and SandBox packs whose format
    is block compressed or a plain linear format, and which carry the pixel segment
    (2c4eee70...), the mip chain computed from (width, height, mips, format, faces) with a
    4x4 block floor equals the pixel segment's decompressed size to the byte in 3,582 cases
    (the one exception is reported as FORMULA_MISMATCH). `16K_Vista_02` decodes as
    16384x16384, 15 mips, DXT5, 357,913,968 bytes, matching its segment exactly.
    A second segment type (0365d554...) is a low resolution stub (174,776 bytes for a
    4096x4096 DXT1 `stone`); 3,256 Native textures carry ONLY that stub inside the packs.

  Material (type guid 9313b01d...): 20 bytes; u32; u32; u32 flag count and strings;
    u32; u32 tag count and strings ("bumpmap", "doubleuv", "skinning"); sized blend mode
    ("no_alpha_blend", "modulate", "factor"); shader guid; u32 texture count and that many
    (u32 slot, texture guid) pairs; u32; u32 flag count and strings; 112 bytes of floats.
    Slot 0 is the diffuse, 2 the normal, 4 the specular, 6 the height map in every material
    inspected. Verified: on every material that parses, the set of texture guids the parse
    returns equals the set a brute 16-byte window scan finds against the texture index. The
    window scan is the fallback for a material whose layout drifts.

  Two pack trees. Native ships `AssetPackages` and `EmAssetPackages` (1,038 packs, 28 GB).
    3,256 textures carry only a stub of about 175 KB inside `AssetPackages` (174,776 bytes
    for the 4096x4096 DXT1 `stone`, 174,800 for a 1024x2048 DXT5) and their full chain
    inside `EmAssetPackages` (`empire_wall_brick_d`: 8192x8192 DXT1, 44,739,256 bytes there,
    equal to the formula). Both trees are indexed per module, `AssetPackages` first;
    a texture whose chain is supplied by the second tree is flagged
    FULL_CHAIN_ONLY_IN_EMASSETPACKAGES. TAOM_Map ships no `EmAssetPackages`: every one of its
    textures carries its full chain in the base packs. Whether the engine keeps a full chain
    resident or streams it from the second tree is not knowable offline.

  Metamesh (type guid 978b8fa0...): a header, then one record per mesh. A metamesh is split
    per material and per LOD, so `barad_dur.0` to `.3` are four materials at one LOD and
    `dao_rock_16.lod3` is a LOD. Records are located by pattern rather than by a full layout:
    `u32 2, 16-byte mesh guid, sized name` where the name starts with the metamesh name.
    After the name: 8 bytes, material guid, and at name end + 92 three u32: unique
    positions, faces, split vertices. Anchored on `editor_plane_low` and `dirty_a` (4, 2, 4:
    a quad) and `volume_box` (8, 12, 24: a cube needs 24 split vertices for flat normals).
    Across 13,402 metameshes the record count equals the count of 5f98413d... segments in
    12,660; the rest are reported with UNKNOWN counts and their segment bytes only.
    Each record owns one segment of each of two types (decompressed and inspected on
    `dirty_a`, `barad_dur.2`, `edoras_t3.1`): 5f98413d... starts with a u32 position count
    followed by float4 positions (editor-format geometry; on `dirty_a` the count is 4 and
    the positions are the quad's corners), 97f81dbb... starts with a u32 index count
    followed by u16 indices and a table of (offset, size) attribute streams (runtime
    streams; on `dirty_a` the count is 6 and the indices are 0,1,2,1,0,3). Which of the two
    the shipping client keeps resident is not determined here, so both are reported and the
    totals carry each as a breakdown. The segment order in the table of contents does not
    always follow the record order (`edoras_t3`), so no per-record sizes are claimed.

  flora.bin: "FLR2", u32 payload size (= file size - 8), u32 record count, then records of
    `i32 len, name, u32 variation, 16 f32 (a 4x4 transform), 4 f32`. The walk lands on the
    last byte of the file exactly and the record count equals the header count (360,099).

Every size here is the decompressed on-disk payload the engine would have to hold to draw
the asset. How much of it stays in system RAM versus VRAM, and what the engine adds on top
(driver copies, mip streaming, GPU buffers), is not knowable offline; this tool attributes,
it does not measure.

Usage:
  python tools/audit_map_scene_memory.py                       # Main_map, default outputs
  python tools/audit_map_scene_memory.py --report r.md --tsv-dir out --top 100
"""
from __future__ import annotations

import argparse
import collections
import csv
import math
import os
import struct
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir  # noqa: E402

DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
DEFAULT_OUT = r"E:\taom-memory-2026-09-02\map-manifest"
DEFAULT_MODULES = ("TAOM_Map", "Native", "SandBox")
DEFAULT_SCENE = "Main_map"

TPAC_MAGIC = 0x43415054
KIND_BY_TYPE_GUID = {
    bytes.fromhex("978b8fa07c19ea4bb95b53846cae834e"): "metamesh",
    bytes.fromhex("cbcb74c91c5ff6499a322b5b6c92c2e8"): "texture",
    bytes.fromhex("0e8e52e8b664614ebae07569c0452aea"): "physics",
    bytes.fromhex("9313b01d0269194f83bab37a39830717"): "material",
}
SEG_TEXTURE_PIXELS = bytes.fromhex("2c4eee70e4792d4b8d54d53ecd2a559c")
SEG_TEXTURE_STUB = bytes.fromhex("0365d55444994041ad1755bbd9ce9e39")
SEG_MESH_A = bytes.fromhex("5f98413dd224c14f82e46a6e0da3f4e2")
SEG_MESH_B = bytes.fromhex("97f81dbb4f587047abf2663fe449f247")
SEG_MESH_TABLE = bytes.fromhex("f6304064428a864cb9359b9daa9391c2")
ZERO_GUID = bytes(16)

# bytes per 4x4 block for the block compressed formats; bytes per pixel for the linear ones
BLOCK_FORMATS = {"DXT1": 8, "BC1": 8, "BC4": 8, "DXT3": 16, "DXT5": 16, "BC3": 16,
                 "BC5": 16, "BC7": 16, "BC6H_UF16": 16, "BC6H_SF16": 16}
LINEAR_FORMATS = {"R8_UNORM": 1, "R16_UNORM": 2, "R16G16F": 4, "R8G8B8A8_UNORM": 4,
                  "R16G16B16A16F": 8}
FORMULA_TEXT = ("block formats: sum over mips of ceil(w/4)*ceil(h/4)*block bytes "
                "(DXT1/BC1/BC4 = 8, DXT3/DXT5/BC3/BC5/BC7/BC6H = 16, so BC1 = w*h/2 and "
                "BC3/BC5/BC7 = w*h at the top mip); linear formats: sum over mips of "
                "w*h*bytes per pixel (RGBA8 = w*h*4); the full chain is about 1.33x the top "
                "mip; cubemaps multiply by 6")

PACK_TREES = ("AssetPackages", "EmAssetPackages")
SCENE_BINARY_FILES = ("flora.bin", "terrain.bin", "navmesh.bin", "flowmap.dds", "scene.xscene")
ATMOSPHERE_TEXTURE_KEYS = ("skybox_background_texture_name", "skybox_sun_texture_name",
                           "lens_flare_dirt_texture_name", "lens_flare_star_texture_name")
TERRAIN_TEXTURE_ATTRS = ("vista_diffuse_name", "vista_diffuse_winter_name", "vista_diffuse_fall_name",
                         "vista_normalmap", "vista_detail_albedo_name", "vista_detail_normal_name",
                         "dynamic_flowmap_texture_name")
OUTER_MESH_TEXTURE_VARS = ("diffuse1_texture_name", "diffuse2_texture_name", "diffuse3_texture_name",
                           "areamap_texture_name", "splatmap_texture_name", "splatmap_normal_name")


# --------------------------------------------------------------------------- #
# tpac table of contents
# --------------------------------------------------------------------------- #
@dataclass
class Segment:
    type_guid: bytes
    actual: int
    stored: int
    storage_format: int
    offset: int


@dataclass
class TpacItem:
    module: str
    pack: str
    kind: str
    name: str
    guid: bytes
    meta: bytes
    segments: list

    def seg_bytes(self, type_guid=None) -> int:
        return sum(s.actual for s in self.segments if type_guid is None or s.type_guid == type_guid)

    def has_segment(self, type_guid) -> bool:
        return any(s.type_guid == type_guid for s in self.segments)


class MetaReader:
    """Sequential reader over a metadata blob (same primitives as tpac_skeleton_dump.Reader)."""

    def __init__(self, data: bytes):
        self.b = data
        self.i = 0

    def u8(self) -> int:
        v = self.b[self.i]
        self.i += 1
        return v

    def u32(self) -> int:
        v = struct.unpack_from("<I", self.b, self.i)[0]
        self.i += 4
        return v

    def guid(self) -> bytes:
        v = self.b[self.i:self.i + 16]
        if len(v) != 16:
            raise ValueError("truncated guid")
        self.i += 16
        return v

    def skip(self, n: int) -> None:
        self.i += n

    def string(self, limit: int = 4096) -> str:
        n = struct.unpack_from("<i", self.b, self.i)[0]
        if n < 0 or n > limit or self.i + 4 + n > len(self.b):
            raise ValueError(f"implausible string length {n} at {self.i}")
        v = self.b[self.i + 4:self.i + 4 + n].decode("utf-8", errors="replace")
        self.i += 4 + n
        return v

    def strings(self, limit_count: int = 64) -> list:
        n = self.u32()
        if n > limit_count:
            raise ValueError(f"implausible list count {n} at {self.i - 4}")
        return [self.string() for _ in range(n)]


def iter_tpac_items(path, module: str):
    """Yield every item of a tpac's table of contents. Metadata is read; segment payloads are not."""
    path = str(path)
    pack = os.path.basename(path)
    with open(path, "rb") as f:
        magic = struct.unpack("<I", f.read(4))[0]
        if magic != TPAC_MAGIC:
            raise ValueError(f"not a tpac (magic 0x{magic:08x}): {path}")
        version = struct.unpack("<I", f.read(4))[0]
        f.read(16)
        num_items = struct.unpack("<I", f.read(4))[0]
        f.read(8)
        for index in range(num_items):
            type_guid = f.read(16)
            item_guid = f.read(16)
            if version > 1:
                f.read(4)
            n = struct.unpack("<i", f.read(4))[0]
            if n < 0 or n > 4096:
                raise ValueError(f"implausible name length {n} at item {index}")
            name = f.read(n).decode("utf-8", errors="replace") if n else ""
            meta_size = struct.unpack("<q", f.read(8))[0]
            if meta_size < 0 or meta_size > 64 * 1024 * 1024:
                raise ValueError(f"implausible metadata size {meta_size} at item {index} ({name!r})")
            meta = f.read(meta_size)
            f.read(8)
            seg_count = struct.unpack("<i", f.read(4))[0]
            if seg_count < 0 or seg_count > 100000:
                raise ValueError(f"implausible segment count {seg_count} at item {index} ({name!r})")
            segments = []
            for _ in range(seg_count):
                seg_offset, actual, stored = struct.unpack("<QQQ", f.read(24))
                f.read(16)
                seg_type = f.read(16)
                f.read(12)
                storage_format = f.read(1)[0]
                segments.append(Segment(seg_type, actual, stored, storage_format, seg_offset))
            dep_count = struct.unpack("<i", f.read(4))[0]
            if dep_count < 0 or dep_count > 100000:
                raise ValueError(f"implausible dependency count {dep_count} at item {index} ({name!r})")
            f.seek(dep_count * 48, 1)
            yield TpacItem(module, pack, KIND_BY_TYPE_GUID.get(type_guid, "other"),
                           name, item_guid, meta, segments)


class AssetIndex:
    """(kind, lowercase name) -> the item that supplies it, modules in priority order.

    A name seen again in a later module is recorded under `also_in` and not used. Inside
    one module a texture that carries the real pixel segment replaces a stub-only twin.
    """

    def __init__(self):
        self.items: dict = {}
        self.by_guid: dict = {}
        self.also_in: dict = collections.defaultdict(list)
        self.packs: list = []
        self.errors: list = []
        self.counts: collections.Counter = collections.Counter()

    def add_module(self, module: str, module_dir) -> None:
        """Index `AssetPackages/*.tpac`, then `EmAssetPackages/**/*.tpac`, of one module.

        Native ships both trees. Its `AssetPackages` hold a stub of about 175 KB for 3,256
        textures whose full mip chains sit only under `EmAssetPackages` (28 GB, 1,038 packs,
        measured 2026-09-12); TAOM_Map ships no `EmAssetPackages` at all. Pack labels are
        relative to the module folder so the two trees stay distinguishable.
        """
        module_dir = Path(module_dir)
        found = False
        for sub in PACK_TREES:
            tree = module_dir / sub
            if not tree.is_dir():
                continue
            found = True
            packs = sorted(p for p in tree.rglob("*") if p.is_file() and p.suffix.lower() == ".tpac")
            for pack in packs:
                label = pack.relative_to(module_dir).as_posix()
                self.packs.append((module, label, pack.stat().st_size))
                try:
                    for item in iter_tpac_items(pack, module):
                        item.pack = label
                        self.add_item(item)
                except (OSError, ValueError, struct.error) as exc:
                    self.errors.append((module, label, f"{type(exc).__name__}: {exc}"))
        if not found:
            self.errors.append((module, str(module_dir), "no AssetPackages or EmAssetPackages directory"))

    def add_item(self, item: TpacItem) -> None:
        if item.kind == "other":
            return
        self.counts[(item.module, item.kind)] += 1
        key = (item.kind, item.name.lower())
        current = self.items.get(key)
        if current is None:
            self.items[key] = item
        elif (item.kind == "texture" and current.module == item.module
              and not current.has_segment(SEG_TEXTURE_PIXELS) and item.has_segment(SEG_TEXTURE_PIXELS)):
            self.also_in[key].append(f"{current.module}/{current.pack} (stub)")
            self.items[key] = item
        else:
            self.also_in[key].append(f"{item.module}/{item.pack}")
        self.by_guid.setdefault(item.guid, item)

    def get(self, kind: str, name: str):
        return self.items.get((kind, name.lower()))


# --------------------------------------------------------------------------- #
# metadata decoders
# --------------------------------------------------------------------------- #
@dataclass
class TextureInfo:
    source: str
    flags: list
    width: int
    height: int
    mips: int
    faces: int
    fmt: str
    flags2: list


def parse_texture_meta(meta: bytes) -> TextureInfo:
    r = MetaReader(meta)
    version = r.u32()
    if version != 3:
        raise ValueError(f"texture metadata version {version}, expected 3")
    r.skip(20)
    source = r.string()
    r.skip(8)
    r.u8()
    r.u32()
    flags = r.strings()
    r.u32()
    r.u8()
    width = r.u32()
    height = r.u32()
    r.u32()
    mips = r.u8()
    faces = r.u8()
    r.u8()
    fmt = r.string(64)
    r.u32()
    flags2 = r.strings()
    if width == 0 or height == 0 or width > 65536 or height > 65536:
        raise ValueError(f"implausible dimensions {width}x{height}")
    return TextureInfo(source, flags, width, height, mips, faces, fmt, flags2)


def texture_chain_bytes(width: int, height: int, mips: int, fmt: str, faces: int = 1):
    """Resident bytes of a full mip chain; None when the format is not in the tables."""
    if fmt in BLOCK_FORMATS:
        block = BLOCK_FORMATS[fmt]

        def per_mip(w, h):
            return ((w + 3) // 4) * ((h + 3) // 4) * block
    elif fmt in LINEAR_FORMATS:
        bpp = LINEAR_FORMATS[fmt]

        def per_mip(w, h):
            return w * h * bpp
    else:
        return None
    total = 0
    for level in range(max(1, mips)):
        total += per_mip(max(1, width >> level), max(1, height >> level))
    return total * max(1, faces)


def full_mip_count(width: int, height: int) -> int:
    return int(math.log2(max(width, height, 1))) + 1


@dataclass
class MaterialInfo:
    flags: list
    tags: list
    blend: str
    shader_guid: bytes
    textures: list          # (slot, guid)
    flags2: list


def parse_material_meta(meta: bytes) -> MaterialInfo:
    r = MetaReader(meta)
    r.skip(20)
    r.u32()
    r.u32()
    flags = r.strings()
    r.u32()
    tags = r.strings()
    blend = r.string(64)
    shader = r.guid()
    count = r.u32()
    if count > 64:
        raise ValueError(f"implausible texture count {count}")
    textures = []
    for _ in range(count):
        slot = r.u32()
        textures.append((slot, r.guid()))
    r.u32()
    flags2 = r.strings()
    return MaterialInfo(flags, tags, blend, shader, textures, flags2)


def scan_guids(meta: bytes, by_guid: dict, kind: str) -> set:
    """Every 16-byte window of `meta` that is the guid of an indexed item of `kind`."""
    found = set()
    for i in range(0, len(meta) - 15):
        item = by_guid.get(meta[i:i + 16])
        if item is not None and item.kind == kind:
            found.add(item.guid)
    return found


@dataclass
class MeshRecord:
    name: str
    material_guid: bytes
    positions: int
    faces: int
    vertices: int


def parse_metamesh_records(meta: bytes, metamesh_name: str) -> list:
    """LOD mesh records located by the `u32 2, guid, sized name` pattern (see module docstring)."""
    prefix = metamesh_name.lower().encode("utf-8")
    records = []
    i = 0
    limit = len(meta) - 24
    while i < limit:
        if meta[i:i + 4] == b"\x02\x00\x00\x00":
            n = struct.unpack_from("<i", meta, i + 20)[0]
            if 1 <= n <= 255 and i + 24 + n <= len(meta):
                raw = meta[i + 24:i + 24 + n]
                if raw.lower().startswith(prefix) and all(32 <= c < 127 for c in raw):
                    end = i + 24 + n
                    material = meta[end + 8:end + 24] if end + 24 <= len(meta) else b""
                    if end + 104 <= len(meta):
                        positions, faces, vertices = struct.unpack_from("<III", meta, end + 92)
                    else:
                        positions = faces = vertices = 0
                    records.append(MeshRecord(raw.decode("ascii"), material, positions, faces, vertices))
                    i = end
                    continue
        i += 1
    return records


def plausible_counts(rec: MeshRecord, by_guid: dict) -> bool:
    material_ok = rec.material_guid == ZERO_GUID or (
        rec.material_guid in by_guid and by_guid[rec.material_guid].kind == "material")
    return material_ok and 0 < rec.faces <= 20_000_000 and 0 < rec.positions <= 20_000_000


# --------------------------------------------------------------------------- #
# scene side
# --------------------------------------------------------------------------- #
def parse_references(path) -> dict:
    kinds = collections.defaultdict(list)
    with open(path, "rb") as f:
        for raw in f.read().decode("utf-8", errors="replace").splitlines():
            line = raw.strip()
            if " " not in line:
                continue
            kind, name = line.split(" ", 1)
            kinds[kind].append(name.strip())
    return dict(kinds)


@dataclass
class FloraBin:
    header_count: int
    walked: int
    walk_ok: bool
    per_kind: collections.Counter
    variations: collections.Counter
    size: int


def parse_flora_bin(path) -> FloraBin:
    data = open(path, "rb").read()
    per_kind: collections.Counter = collections.Counter()
    variations: collections.Counter = collections.Counter()
    if len(data) < 12 or data[:4] != b"FLR2":
        return FloraBin(0, 0, False, per_kind, variations, len(data))
    header_count = struct.unpack_from("<I", data, 8)[0]
    off = 12
    walked = 0
    ok = True
    while off < len(data):
        n = struct.unpack_from("<i", data, off)[0]
        if n <= 0 or n > 256 or off + 4 + n + 84 > len(data):
            ok = False
            break
        name = data[off + 4:off + 4 + n].decode("utf-8", errors="replace")
        off += 4 + n
        variations[struct.unpack_from("<I", data, off)[0]] += 1
        off += 4 + 64 + 16
        per_kind[name] += 1
        walked += 1
    ok = ok and off == len(data) and walked == header_count
    return FloraBin(header_count, walked, ok, per_kind, variations, len(data))


def parse_dds_header(path):
    """(width, height, mips, fourcc or 'rgb') from a standard 128-byte DDS header, else None."""
    with open(path, "rb") as f:
        head = f.read(128)
    if len(head) < 128 or head[:4] != b"DDS " or struct.unpack_from("<I", head, 4)[0] != 124:
        return None
    height, width = struct.unpack_from("<II", head, 12)
    mips = struct.unpack_from("<I", head, 28)[0]
    pf_flags, fourcc = struct.unpack_from("<I4s", head, 80)
    if pf_flags & 0x4:
        code = fourcc.decode("latin-1") if fourcc.isalnum() else f"0x{struct.unpack('<I', fourcc)[0]:x}"
    else:
        code = f"rgb{struct.unpack_from('<I', head, 88)[0]}"
    return width, height, mips, code


@dataclass
class Variation:
    name: str
    body: str
    meshes: list        # (sub mesh name, material name)


@dataclass
class FloraKind:
    name: str
    source: str
    seasons: dict       # season -> [Variation]


def parse_flora_kinds(paths) -> dict:
    """kind name (lowercase) -> FloraKind; the first file defining a kind wins."""
    kinds: dict = {}
    for source, path in paths:
        root = ET.fromstring(open(path, "rb").read())
        for kind in root.findall("flora_kind"):
            name = (kind.get("name") or "").strip()
            if not name or name.lower() in kinds:
                continue
            seasons = {}
            for sk in kind.findall("seasonal_kind"):
                variations = []
                for var in sk.iter("flora_variation"):
                    meshes = [((m.get("name") or "").strip(), (m.get("material") or "").strip())
                              for m in var.findall("mesh")]
                    variations.append(Variation((var.get("name") or "").strip(),
                                                (var.get("body_name") or "").strip(), meshes))
                seasons[sk.get("season") or "?"] = variations
            kinds[name.lower()] = FloraKind(name, source, seasons)
    return kinds


@dataclass
class EntityRefs:
    entities: int = 0
    prefabs: collections.Counter = field(default_factory=collections.Counter)
    metameshes: collections.Counter = field(default_factory=collections.Counter)
    physics: collections.Counter = field(default_factory=collections.Counter)
    materials: collections.Counter = field(default_factory=collections.Counter)
    particles: int = 0

    def add(self, other: "EntityRefs", times: int = 1) -> None:
        self.entities += other.entities * times
        for attr in ("prefabs", "metameshes", "physics", "materials"):
            mine = getattr(self, attr)
            for k, v in getattr(other, attr).items():
                mine[k] += v * times
        self.particles += other.particles * times


def collect_entity_refs(root_elem) -> EntityRefs:
    """References made by every game_entity under `root_elem` (itself included when it is one)."""
    refs = EntityRefs()
    for ge in root_elem.iter("game_entity"):
        refs.entities += 1
        prefab = (ge.get("prefab") or "").strip()
        if prefab:
            refs.prefabs[prefab] += 1
        physics = ge.find("physics")
        if physics is not None and (physics.get("shape") or "").strip():
            refs.physics[physics.get("shape").strip()] += 1
        components = ge.find("components")
        if components is None:
            continue
        for comp in components:
            if comp.tag == "meta_mesh_component":
                name = (comp.get("name") or "").strip()
                if name:
                    refs.metameshes[name] += 1
                for mesh in comp.findall("mesh"):
                    material = (mesh.get("material") or "").strip()
                    if material:
                        refs.materials[material] += 1
            elif comp.tag == "decal_component":
                material = (comp.get("material") or "").strip()
                if material:
                    refs.materials[material] += 1
            elif comp.tag == "particle_system_instanced_component":
                refs.particles += 1
    return refs


@dataclass
class SceneRefs:
    entity_refs: EntityRefs
    terrain_textures: dict         # name -> role
    terrain_meshes: set
    water_material: str
    atmosphere_textures: dict      # name -> role


def parse_scene(scene_xml, atmosphere_xml=None) -> SceneRefs:
    root = ET.fromstring(open(scene_xml, "rb").read())
    entities = root.find("entities")
    entity_refs = collect_entity_refs(entities) if entities is not None else EntityRefs()
    terrain_textures: dict = {}
    terrain_meshes: set = set()
    terrain = root.find("terrain")
    if terrain is not None:
        for attr in TERRAIN_TEXTURE_ATTRS:
            value = (terrain.get(attr) or "").strip()
            if value and value.lower() != "none":
                terrain_textures[value] = f"terrain:{attr}"
        outer = terrain.find("outer_mesh")
        if outer is not None:
            for var in outer.findall("variable"):
                value = (var.get("value") or "").strip()
                if not value or value.lower() == "none":
                    continue
                if var.get("name") == "outer_mesh_name":
                    terrain_meshes.add(value)
                elif var.get("name") in OUTER_MESH_TEXTURE_VARS:
                    terrain_textures[value] = f"terrain:outer_mesh:{var.get('name')}"
        for layer in terrain.iter("layer"):
            lname = layer.get("name") or "?"
            for season in layer:
                for tex in season.iter("texture"):
                    value = (tex.get("name") or "").strip()
                    if value and value.lower() != "none":
                        terrain_textures.setdefault(value, f"terrain layer {lname} {tex.get('type')}")
                for mesh in season.iter("mesh"):
                    value = (mesh.get("name") or "").strip()
                    if value:
                        terrain_meshes.add(value)
    water_material = ""
    for prop in root.iter("property"):
        if prop.get("name") == "water_material" and (prop.get("value") or "").strip():
            water_material = prop.get("value").strip()
    atmosphere_textures: dict = {}
    if atmosphere_xml and os.path.isfile(atmosphere_xml):
        atm = ET.fromstring(open(atmosphere_xml, "rb").read())
        for value in atm.iter("value"):
            if value.get("name") in ATMOSPHERE_TEXTURE_KEYS and (value.get("value") or "").strip():
                atmosphere_textures[value.get("value").strip()] = f"atmosphere:{value.get('name')}"
    return SceneRefs(entity_refs, terrain_textures, terrain_meshes, water_material, atmosphere_textures)


class PrefabIndex:
    """Prefab name -> its definition, modules in priority order; refs are expanded recursively."""

    def __init__(self):
        self.defs: dict = {}            # lowercase name -> (module, file, element)
        self.duplicates: collections.Counter = collections.Counter()
        self.parse_errors: list = []
        self._refs_cache: dict = {}
        self.files: collections.Counter = collections.Counter()

    def add_dir(self, module: str, path) -> None:
        path = Path(path)
        if not path.is_dir():
            return
        for xml in sorted(p for p in path.iterdir() if p.suffix.lower() == ".xml"):
            try:
                root = ET.fromstring(open(xml, "rb").read())
            except ET.ParseError as exc:
                self.parse_errors.append((module, xml.name, str(exc)))
                continue
            self.files[module] += 1
            for ge in root.findall("game_entity"):
                name = (ge.get("name") or "").strip()
                if not name:
                    continue
                key = name.lower()
                if key in self.defs:
                    self.duplicates[key] += 1
                    continue
                self.defs[key] = (module, xml.name, ge)

    def resolve(self, name: str):
        return self.defs.get(name.lower())

    def refs(self, name: str, _stack=()) -> EntityRefs:
        """Every reference one instance of `name` pulls in, nested prefab instances expanded."""
        key = name.lower()
        if key in self._refs_cache:
            return self._refs_cache[key]
        entry = self.defs.get(key)
        total = EntityRefs()
        if entry is not None and key not in _stack:
            own = collect_entity_refs(entry[2])
            total.add(own)
            for nested, count in own.prefabs.items():
                total.add(self.refs(nested, _stack + (key,)), count)
        self._refs_cache[key] = total
        return total


# --------------------------------------------------------------------------- #
# manifest
# --------------------------------------------------------------------------- #
@dataclass
class Manifest:
    scene_dir: str
    modules: list
    index: AssetIndex
    scene: SceneRefs
    references: dict
    flora_bin: FloraBin
    flora_kinds: dict
    prefabs: PrefabIndex
    scene_files: list = field(default_factory=list)      # (name, bytes, note)
    texture_rows: list = field(default_factory=list)
    mesh_rows: list = field(default_factory=list)
    material_rows: list = field(default_factory=list)
    physics_rows: list = field(default_factory=list)
    prefab_rows: list = field(default_factory=list)
    flora_rows: list = field(default_factory=list)
    unresolved: list = field(default_factory=list)       # (kind, name, referenced_by)
    reference_notes: list = field(default_factory=list)
    totals: dict = field(default_factory=dict)           # (module, category) -> (count, bytes)
    warnings: list = field(default_factory=list)


def _texture_row(item: TpacItem, index: AssetIndex, referenced_by: str) -> dict:
    row = {"name": item.name, "module": item.module, "pack": item.pack, "format": "UNKNOWN",
           "width": "UNKNOWN", "height": "UNKNOWN", "mips": "UNKNOWN", "faces": "UNKNOWN",
           "formula_bytes": "UNKNOWN", "pixel_segment_bytes": "", "stub_segment_bytes": "",
           "resident_bytes": 0, "size_source": "", "flags": [], "referenced_by": referenced_by,
           "also_in": ";".join(index.also_in.get(("texture", item.name.lower()), [])), "source_path": ""}
    pixel = item.seg_bytes(SEG_TEXTURE_PIXELS) if item.has_segment(SEG_TEXTURE_PIXELS) else None
    stub = item.seg_bytes(SEG_TEXTURE_STUB) if item.has_segment(SEG_TEXTURE_STUB) else None
    row["pixel_segment_bytes"] = "" if pixel is None else pixel
    row["stub_segment_bytes"] = "" if stub is None else stub
    formula = None
    try:
        info = parse_texture_meta(item.meta)
    except (ValueError, struct.error, IndexError) as exc:
        row["flags"].append(f"HEADER_UNDECODED({type(exc).__name__})")
        info = None
    if info is not None:
        row.update(format=info.fmt, width=info.width, height=info.height, mips=info.mips,
                   faces=info.faces, source_path=info.source)
        formula = texture_chain_bytes(info.width, info.height, info.mips, info.fmt, info.faces)
        row["formula_bytes"] = "UNKNOWN" if formula is None else formula
        if info.fmt in LINEAR_FORMATS:
            row["flags"].append("UNCOMPRESSED")
        elif info.fmt not in BLOCK_FORMATS:
            row["flags"].append(f"UNKNOWN_FORMAT({info.fmt})")
        if info.mips == 1 and max(info.width, info.height) > 512:
            row["flags"].append("NO_MIPS_ABOVE_512")
        if formula is not None and pixel is not None and formula != pixel:
            row["flags"].append("FORMULA_MISMATCH")
    if pixel is not None:
        row["resident_bytes"], row["size_source"] = pixel, "pixel_segment"
        if item.pack.startswith("EmAssetPackages/"):
            row["flags"].append("FULL_CHAIN_ONLY_IN_EMASSETPACKAGES")
    elif formula is not None:
        row["resident_bytes"], row["size_source"] = formula, "formula (pixels not in packs)"
        row["flags"].append("PIXELS_NOT_IN_PACKS")
    elif stub is not None:
        row["resident_bytes"], row["size_source"] = stub, "stub_segment_only"
        row["flags"].append("PIXELS_NOT_IN_PACKS")
    else:
        row["size_source"] = "UNKNOWN"
        row["flags"].append("NO_PIXEL_DATA")
    return row


def build_manifest(game_modules, scene_dir, modules=DEFAULT_MODULES, extra_flora_kinds=()) -> Manifest:
    game_modules = Path(game_modules)
    scene_dir = Path(scene_dir)
    modules = list(modules)

    index = AssetIndex()
    for module in modules:
        index.add_module(module, game_modules / module)

    prefabs = PrefabIndex()
    for module in modules:
        prefabs.add_dir(module, game_modules / module / "Prefabs")

    flora_paths = [(m, game_modules / m / "ModuleData" / "flora_kinds.xml") for m in modules]
    flora_paths = [(m, p) for m, p in flora_paths if p.is_file()]
    flora_paths += [(f"extra:{Path(p).name}", Path(p)) for p in extra_flora_kinds if Path(p).is_file()]
    flora_kinds = parse_flora_kinds(flora_paths)

    references = parse_references(scene_dir / "references.txt") if (scene_dir / "references.txt").is_file() else {}
    scene = parse_scene(scene_dir / "scene.xscene", scene_dir / "atmosphere.xml")
    flora_bin = (parse_flora_bin(scene_dir / "flora.bin") if (scene_dir / "flora.bin").is_file()
                 else FloraBin(0, 0, False, collections.Counter(), collections.Counter(), 0))

    m = Manifest(str(scene_dir), modules, index, scene, references, flora_bin, flora_kinds, prefabs)
    m.warnings.extend(f"{mod}/{pack}: {err}" for mod, pack, err in index.errors)
    m.warnings.extend(f"prefab {mod}/{f}: {err}" for mod, f, err in prefabs.parse_errors)
    if not flora_bin.walk_ok:
        m.warnings.append("flora.bin did not walk cleanly to EOF; per-kind counts are partial")

    for name in SCENE_BINARY_FILES:
        p = scene_dir / name
        if p.is_file():
            note = ""
            if name.endswith(".dds"):
                dds = parse_dds_header(p)
                if dds:
                    note = f"DDS {dds[0]}x{dds[1]}, {dds[2]} mip(s), {dds[3]}"
            elif name == "flora.bin":
                note = (f"FLR2, {flora_bin.header_count} instances in header, walked {flora_bin.walked}, "
                        f"{'consistent' if flora_bin.walk_ok else 'INCONSISTENT'}")
            m.scene_files.append((name, p.stat().st_size, note))

    # --- meshes: direct scene refs, prefab-expanded refs, references.txt, terrain, flora ---
    entity_refs = scene.entity_refs
    via_prefabs = EntityRefs()
    for prefab, count in entity_refs.prefabs.items():
        via_prefabs.add(prefabs.refs(prefab), count)

    flora_mesh_instances: collections.Counter = collections.Counter()
    flora_materials: collections.Counter = collections.Counter()
    flora_bodies: collections.Counter = collections.Counter()
    for kind_name in references.get("flora_entity", []):
        kind = flora_kinds.get(kind_name.lower())
        instances = flora_bin.per_kind.get(kind_name, 0)
        row = {"kind": kind_name, "defined_in": kind.source if kind else "UNRESOLVED",
               "instances": instances, "seasons": "", "variation_meshes": 0, "mesh_bytes": 0,
               "unresolved_meshes": 0, "meshes": ""}
        if kind is None:
            m.unresolved.append(("flora_kind", kind_name, "references.txt"))
            m.flora_rows.append(row)
            continue
        mesh_names: set = set()
        body_names: set = set()
        for season, variations in kind.seasons.items():
            for var in variations:
                if var.name:
                    mesh_names.add(var.name)
                if var.body:
                    body_names.add(var.body)
                for _sub, material in var.meshes:
                    if material:
                        flora_materials[material] += 1
        for mesh_name in mesh_names:
            flora_mesh_instances[mesh_name] += instances
        for body_name in body_names:
            flora_bodies[body_name] += instances
        row["seasons"] = ",".join(sorted(kind.seasons))
        row["variation_meshes"] = len(mesh_names)
        row["meshes"] = ";".join(sorted(mesh_names))
        m.flora_rows.append(row)

    mesh_sources: dict = collections.defaultdict(lambda: collections.Counter())
    for name, count in entity_refs.metameshes.items():
        mesh_sources[name.lower()]["scene_direct"] += count
    for name, count in via_prefabs.metameshes.items():
        mesh_sources[name.lower()]["via_prefabs"] += count
    for name in references.get("mesh", []):
        mesh_sources[name.lower()]["references.txt"] += 1
    for name in scene.terrain_meshes:
        mesh_sources[name.lower()]["terrain"] += 1
    for name, count in flora_mesh_instances.items():
        mesh_sources[name.lower()]["flora_kind_instances"] += count
    display_name: dict = {}
    for counter in (entity_refs.metameshes, via_prefabs.metameshes, flora_mesh_instances):
        for name in counter:
            display_name.setdefault(name.lower(), name)
    for name in references.get("mesh", []) + list(scene.terrain_meshes):
        display_name.setdefault(name.lower(), name)

    material_sources: dict = collections.defaultdict(lambda: collections.Counter())
    for name, count in entity_refs.materials.items():
        material_sources[name.lower()]["scene_override"] += count
    for name, count in via_prefabs.materials.items():
        material_sources[name.lower()]["prefab_override"] += count
    for name, count in flora_materials.items():
        material_sources[name.lower()]["flora_kinds"] += count
    if scene.water_material:
        material_sources[scene.water_material.lower()]["water"] += 1
    material_display: dict = {}
    for counter in (entity_refs.materials, via_prefabs.materials, flora_materials):
        for name in counter:
            material_display.setdefault(name.lower(), name)
    if scene.water_material:
        material_display.setdefault(scene.water_material.lower(), scene.water_material)

    material_guids: dict = collections.defaultdict(lambda: collections.Counter())   # guid -> sources
    record_stats = collections.Counter()
    for key in sorted(mesh_sources):
        sources = mesh_sources[key]
        name = display_name.get(key, key)
        item = index.get("metamesh", name)
        if item is None:
            m.unresolved.append(("metamesh", name, ";".join(f"{k}={v}" for k, v in sources.items())))
            continue
        records = parse_metamesh_records(item.meta, item.name)
        seg_a = [s.actual for s in item.segments if s.type_guid == SEG_MESH_A]
        counts_known = bool(records) and len(records) == len(seg_a) and all(
            plausible_counts(r, index.by_guid) for r in records)
        record_stats["known" if counts_known else "unknown"] += 1
        largest = max(records, key=lambda r: r.faces) if records else None
        materials_found = scan_guids(item.meta, index.by_guid, "material")
        for guid in materials_found:
            material_guids[guid][f"mesh:{item.name}"] += 1
        for rec in records:
            if rec.material_guid not in (ZERO_GUID, b"") and rec.material_guid not in index.by_guid:
                m.unresolved.append(("material_guid", rec.material_guid.hex(), f"mesh:{item.name}/{rec.name}"))
        seg_a_total = item.seg_bytes(SEG_MESH_A)
        seg_b_total = item.seg_bytes(SEG_MESH_B)
        flags = []
        if seg_a_total > 1_000_000 and seg_a_total > 10 * seg_b_total:
            flags.append("EDITOR_STREAM_BLOAT")
        m.mesh_rows.append({
            "name": item.name, "module": item.module, "pack": item.pack,
            "records": len(records) if counts_known else f"UNKNOWN({len(records)} found, {len(seg_a)} editor segments)",
            "largest_positions": largest.positions if counts_known and largest else "UNKNOWN",
            "largest_faces": largest.faces if counts_known and largest else "UNKNOWN",
            "largest_vertices": largest.vertices if counts_known and largest else "UNKNOWN",
            "all_records_faces": sum(r.faces for r in records) if counts_known else "UNKNOWN",
            "seg_5f98413d_bytes": seg_a_total,
            "seg_97f81dbb_bytes": seg_b_total,
            "seg_f6304064_bytes": item.seg_bytes(SEG_MESH_TABLE),
            "total_bytes": item.seg_bytes(),
            "flags": flags,
            "scene_direct": sources.get("scene_direct", 0),
            "via_prefabs": sources.get("via_prefabs", 0),
            "flora_kind_instances": sources.get("flora_kind_instances", 0),
            "in_references_txt": "yes" if sources.get("references.txt") else "",
            "terrain": "yes" if sources.get("terrain") else "",
            "materials": ";".join(sorted(index.by_guid[g].name for g in materials_found)),
            "also_in": ";".join(index.also_in.get(("metamesh", key), [])),
        })
    m.reference_notes.append(f"metamesh record counts decoded for {record_stats['known']} meshes, "
                             f"UNKNOWN for {record_stats['unknown']}")

    # --- materials by name (overrides, decals, flora, water) join the guid-referenced ones ---
    for key in sorted(material_sources):
        name = material_display.get(key, key)
        item = index.get("material", name)
        if item is None:
            m.unresolved.append(("material", name, ";".join(f"{k}={v}" for k, v in material_sources[key].items())))
            continue
        for src, count in material_sources[key].items():
            material_guids[item.guid][src] += count

    texture_sources: dict = collections.defaultdict(lambda: collections.Counter())
    texture_display: dict = {}
    material_parse = collections.Counter()
    for guid in sorted(material_guids, key=lambda g: index.by_guid[g].name.lower()):
        item = index.by_guid[guid]
        sources = material_guids[guid]
        try:
            info = parse_material_meta(item.meta)
            tex_guids = [(slot, g) for slot, g in info.textures if g != ZERO_GUID]
            shader = index.by_guid[info.shader_guid].name if info.shader_guid in index.by_guid else info.shader_guid.hex()
            parsed = "parsed"
            material_parse["parsed"] += 1
        except (ValueError, struct.error, IndexError):
            tex_guids = [(-1, g) for g in sorted(scan_guids(item.meta, index.by_guid, "texture"))]
            shader = "UNKNOWN"
            parsed = "guid_scan"
            material_parse["guid_scan"] += 1
        names = []
        for slot, g in tex_guids:
            tex = index.by_guid.get(g)
            if tex is None or tex.kind != "texture":
                m.unresolved.append(("texture_guid", g.hex(), f"material:{item.name} slot {slot}"))
                continue
            names.append(f"{slot}:{tex.name}")
            texture_sources[tex.name.lower()][f"material:{item.name}"] += 1
            texture_display.setdefault(tex.name.lower(), tex.name)
        m.material_rows.append({
            "name": item.name, "module": item.module, "pack": item.pack, "shader": shader,
            "decode": parsed, "textures": ";".join(names),
            "referenced_by": ";".join(f"{k}={v}" for k, v in sorted(sources.items())),
        })
    m.reference_notes.append(f"materials: {material_parse['parsed']} decoded sequentially, "
                             f"{material_parse['guid_scan']} by guid scan")

    for name, role in list(scene.terrain_textures.items()) + list(scene.atmosphere_textures.items()):
        texture_sources[name.lower()][role] += 1
        texture_display.setdefault(name.lower(), name)
    for name in references.get("texture", []):
        texture_sources[name.lower()]["references.txt"] += 1
        texture_display.setdefault(name.lower(), name)

    for key in sorted(texture_sources):
        name = texture_display[key]
        item = index.get("texture", name)
        refs = ";".join(f"{k}={v}" for k, v in sorted(texture_sources[key].items()))
        if item is None:
            m.unresolved.append(("texture", name, refs))
            continue
        m.texture_rows.append(_texture_row(item, index, refs))

    # --- physics shapes ---
    physics_sources: dict = collections.defaultdict(lambda: collections.Counter())
    physics_display: dict = {}
    for name, count in entity_refs.physics.items():
        physics_sources[name.lower()]["scene_direct"] += count
        physics_display.setdefault(name.lower(), name)
    for name, count in via_prefabs.physics.items():
        physics_sources[name.lower()]["via_prefabs"] += count
        physics_display.setdefault(name.lower(), name)
    for name, count in flora_bodies.items():
        physics_sources[name.lower()]["flora_kind_instances"] += count
        physics_display.setdefault(name.lower(), name)
    for key in sorted(physics_sources):
        name = physics_display[key]
        item = index.get("physics", name)
        refs = physics_sources[key]
        if item is None:
            m.unresolved.append(("physics", name, ";".join(f"{k}={v}" for k, v in refs.items())))
            continue
        m.physics_rows.append({"name": item.name, "module": item.module, "pack": item.pack,
                               "total_bytes": item.seg_bytes(),
                               "scene_direct": refs.get("scene_direct", 0),
                               "via_prefabs": refs.get("via_prefabs", 0),
                               "flora_kind_instances": refs.get("flora_kind_instances", 0)})

    # --- prefabs ---
    mesh_bytes_by_name = {r["name"].lower(): r["total_bytes"] for r in m.mesh_rows}
    for prefab, count in sorted(entity_refs.prefabs.items(), key=lambda kv: (-kv[1], kv[0].lower())):
        entry = prefabs.resolve(prefab)
        refs = prefabs.refs(prefab)
        if entry is None:
            m.unresolved.append(("prefab", prefab, f"scene instances={count}"))
        meshes = sorted(refs.metameshes, key=str.lower)
        m.prefab_rows.append({
            "prefab": prefab, "module": entry[0] if entry else "UNRESOLVED", "file": entry[1] if entry else "",
            "scene_instances": count, "entities_per_instance": refs.entities,
            "distinct_meshes": len(meshes),
            "mesh_bytes": sum(mesh_bytes_by_name.get(n.lower(), 0) for n in meshes),
            "physics_shapes": len(refs.physics), "nested_prefabs": len(refs.prefabs),
            "meshes": ";".join(meshes),
        })
    for name in references.get("prefab", []):
        if prefabs.resolve(name) is None and name not in entity_refs.prefabs:
            m.unresolved.append(("references.txt prefab", name, "not a prefab definition in any module"))

    for row in m.flora_rows:
        names = [n for n in row["meshes"].split(";") if n]
        row["mesh_bytes"] = sum(mesh_bytes_by_name.get(n.lower(), 0) for n in names)
        row["unresolved_meshes"] = sum(1 for n in names if n.lower() not in mesh_bytes_by_name)

    # --- totals ---
    totals: dict = collections.defaultdict(lambda: [0, 0])
    for row in m.texture_rows:
        t = totals[(row["module"], "textures")]
        t[0] += 1
        t[1] += row["resident_bytes"]
        for flag, category in (("FULL_CHAIN_ONLY_IN_EMASSETPACKAGES", "textures: chain only in EmAssetPackages"),
                               ("PIXELS_NOT_IN_PACKS", "textures: pixels in no pack (formula)")):
            if flag in row["flags"]:
                t = totals[(row["module"], category)]
                t[0] += 1
                t[1] += row["resident_bytes"]
    for row in m.mesh_rows:
        t = totals[(row["module"], "meshes")]
        t[0] += 1
        t[1] += row["total_bytes"]
        for column, category in (("seg_5f98413d_bytes", "meshes: segment 5f98413d"),
                                 ("seg_97f81dbb_bytes", "meshes: segment 97f81dbb")):
            t = totals[(row["module"], category)]
            t[0] += 1
            t[1] += row[column]
    for row in m.physics_rows:
        t = totals[(row["module"], "physics")]
        t[0] += 1
        t[1] += row["total_bytes"]
    for name, size, _note in m.scene_files:
        t = totals[("TAOM_Map (scene files)", "scene_files")]
        t[0] += 1
        t[1] += size
    m.totals = {k: tuple(v) for k, v in totals.items()}
    return m


# --------------------------------------------------------------------------- #
# output
# --------------------------------------------------------------------------- #
def fmt_bytes(n) -> str:
    if not isinstance(n, int):
        return str(n)
    if n >= 1 << 30:
        return f"{n / (1 << 30):.2f} GB"
    if n >= 1 << 20:
        return f"{n / (1 << 20):.1f} MB"
    if n >= 1 << 10:
        return f"{n / (1 << 10):.1f} KB"
    return f"{n} B"


def top_items(m: Manifest, n: int) -> list:
    rows = ([("texture", r["name"], r["module"], r["resident_bytes"], " ".join(r["flags"])) for r in m.texture_rows]
            + [("metamesh", r["name"], r["module"], r["total_bytes"], " ".join(r["flags"])) for r in m.mesh_rows]
            + [("physics", r["name"], r["module"], r["total_bytes"], "") for r in m.physics_rows]
            + [("scene file", name, "TAOM_Map", size, note) for name, size, note in m.scene_files])
    return sorted(rows, key=lambda r: -r[3])[:n]


def write_tsv(path, rows: list, columns: list) -> None:
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter="\t", lineterminator="\n")
        w.writerow(columns)
        for row in rows:
            w.writerow(["|".join(row[c]) if isinstance(row.get(c), list) else row.get(c, "") for c in columns])


TEXTURE_COLUMNS = ["name", "module", "pack", "format", "width", "height", "mips", "faces", "formula_bytes",
                   "pixel_segment_bytes", "stub_segment_bytes", "resident_bytes", "size_source", "flags",
                   "referenced_by", "also_in", "source_path"]
MESH_COLUMNS = ["name", "module", "pack", "records", "largest_positions", "largest_faces", "largest_vertices",
                "all_records_faces", "seg_5f98413d_bytes", "seg_97f81dbb_bytes", "seg_f6304064_bytes", "total_bytes",
                "flags", "scene_direct", "via_prefabs", "flora_kind_instances", "in_references_txt", "terrain",
                "materials", "also_in"]
MATERIAL_COLUMNS = ["name", "module", "pack", "shader", "decode", "textures", "referenced_by"]
PHYSICS_COLUMNS = ["name", "module", "pack", "total_bytes", "scene_direct", "via_prefabs", "flora_kind_instances"]
PREFAB_COLUMNS = ["prefab", "module", "file", "scene_instances", "entities_per_instance", "distinct_meshes",
                  "mesh_bytes", "physics_shapes", "nested_prefabs", "meshes"]
FLORA_COLUMNS = ["kind", "defined_in", "instances", "seasons", "variation_meshes", "mesh_bytes",
                 "unresolved_meshes", "meshes"]


def write_tsvs(m: Manifest, tsv_dir) -> list:
    tsv_dir = Path(tsv_dir)
    tsv_dir.mkdir(parents=True, exist_ok=True)
    written = []
    for name, rows, cols in (("textures.tsv", sorted(m.texture_rows, key=lambda r: -r["resident_bytes"]), TEXTURE_COLUMNS),
                             ("meshes.tsv", sorted(m.mesh_rows, key=lambda r: -r["total_bytes"]), MESH_COLUMNS),
                             ("materials.tsv", m.material_rows, MATERIAL_COLUMNS),
                             ("physics.tsv", sorted(m.physics_rows, key=lambda r: -r["total_bytes"]), PHYSICS_COLUMNS),
                             ("prefabs.tsv", m.prefab_rows, PREFAB_COLUMNS),
                             ("flora.tsv", sorted(m.flora_rows, key=lambda r: -r["instances"]), FLORA_COLUMNS)):
        write_tsv(tsv_dir / name, rows, cols)
        written.append(str(tsv_dir / name))
    totals_rows = [{"module": mod, "category": cat, "count": c, "bytes": b}
                   for (mod, cat), (c, b) in sorted(m.totals.items())]
    write_tsv(tsv_dir / "totals.tsv", totals_rows, ["module", "category", "count", "bytes"])
    written.append(str(tsv_dir / "totals.tsv"))
    write_tsv(tsv_dir / "unresolved.tsv", [{"kind": k, "name": n, "referenced_by": r} for k, n, r in m.unresolved],
              ["kind", "name", "referenced_by"])
    written.append(str(tsv_dir / "unresolved.tsv"))
    return written


def summary_lines(m: Manifest, top: int = 20) -> list:
    lines = []
    idx = m.index
    lines.append(f"scene: {m.scene_dir}")
    lines.append(f"modules (priority order): {', '.join(m.modules)}; packs indexed: {len(idx.packs)}; "
                 f"item records seen across both pack trees: "
                 + ", ".join(f"{mod} {kind} {n}" for (mod, kind), n in sorted(idx.counts.items())))
    er = m.scene.entity_refs
    lines.append(f"scene entities: {er.entities}; prefab instances: {sum(er.prefabs.values())} of "
                 f"{len(er.prefabs)} distinct prefabs; direct meta_mesh refs: {sum(er.metameshes.values())} "
                 f"({len(er.metameshes)} distinct); particle systems: {er.particles}")
    lines.append(f"references.txt: " + ", ".join(f"{k} {len(v)}" for k, v in sorted(m.references.items())))
    fb = m.flora_bin
    lines.append(f"flora.bin: {fb.header_count} instances (header), {fb.walked} walked, "
                 f"{'consistent' if fb.walk_ok else 'INCONSISTENT'}, {len(fb.per_kind)} kinds")
    lines.append("")
    lines.append("totals by module and category (sub-rows with ':' are breakdowns of the row above, not additive):")
    grand = 0
    by_cat: collections.Counter = collections.Counter()
    for (mod, cat), (count, size) in sorted(m.totals.items()):
        lines.append(f"  {mod:<26} {cat:<42} {count:>6}  {fmt_bytes(size):>10}  ({size:,} bytes)")
        if ":" not in cat:
            grand += size
        by_cat[cat] += size
    for cat, size in sorted(by_cat.items()):
        lines.append(f"  {'ALL':<26} {cat:<42} {'':>6}  {fmt_bytes(size):>10}")
    lines.append(f"  GRAND TOTAL (asset payload the scene references): {fmt_bytes(grand)} ({grand:,} bytes)")
    lines.append("")
    lines.append(f"resolved: {len(m.texture_rows)} textures, {len(m.mesh_rows)} meshes, {len(m.material_rows)} materials, "
                 f"{len(m.physics_rows)} physics shapes, {len(m.prefab_rows)} prefabs, {len(m.flora_rows)} flora kinds; "
                 f"unresolved references: {len(m.unresolved)}")
    for note in m.reference_notes:
        lines.append(f"note: {note}")
    for w in m.warnings:
        lines.append(f"WARNING: {w}")
    lines.append("")
    lines.append(f"top {top} textures by resident bytes:")
    for r in sorted(m.texture_rows, key=lambda r: -r["resident_bytes"])[:top]:
        lines.append(f"  {fmt_bytes(r['resident_bytes']):>10}  {r['name']:<40} {r['module']:<9} {r['format']:<15} "
                     f"{r['width']}x{r['height']} mips={r['mips']} {' '.join(r['flags'])}")
    return lines


def write_report(m: Manifest, path, top: int = 50) -> None:
    L = []
    L.append("# Campaign map scene: asset payload manifest")
    L.append("")
    L.append(f"Scene: `{m.scene_dir}`. Modules in resolution order: {', '.join(m.modules)}. "
             "Generated by `tools/audit_map_scene_memory.py`; read-only on every input.")
    L.append("")
    L.append("Every byte figure is the decompressed payload the engine would hold to draw the asset "
             "(texture mip chains, mesh vertex and index streams, collision bodies, the scene's own "
             "binary files). It is an attribution of what the scene references, not a measurement "
             "of process memory: driver copies, streaming state and GPU buffers are invisible offline.")
    L.append("")
    L.append("## Summary")
    L.append("")
    L.append("```")
    L.extend(summary_lines(m, top=20))
    L.append("```")
    L.append("")
    L.append("## Scene files")
    L.append("")
    L.append("| file | bytes | note |")
    L.append("|---|---:|---|")
    for name, size, note in m.scene_files:
        L.append(f"| {name} | {size:,} | {note} |")
    L.append("")
    L.append(f"## Top {top} by bytes (all categories)")
    L.append("")
    L.append("| kind | name | module | bytes | flags |")
    L.append("|---|---|---|---:|---|")
    for kind, name, module, size, flags in top_items(m, top):
        L.append(f"| {kind} | {name} | {module} | {size:,} | {flags} |")
    L.append("")
    L.append(f"## Textures (top {top} of {len(m.texture_rows)})")
    L.append("")
    L.append(f"Formula: {FORMULA_TEXT}. `resident_bytes` is the pixel segment's decompressed size when the "
             "pack carries it (it matched the formula to the byte on every block-compressed texture checked), "
             "else the formula. Flags: UNCOMPRESSED (a linear format), NO_MIPS_ABOVE_512 (mip count 1 on a "
             "texture wider than 512), PIXELS_NOT_IN_PACKS (only a low resolution stub ships in the packs), "
             "FORMULA_MISMATCH.")
    L.append("")
    L.append("| name | module | pack | format | size | mips | faces | resident bytes | formula bytes | flags | referenced by |")
    L.append("|---|---|---|---|---|---:|---:|---:|---:|---|---|")
    for r in sorted(m.texture_rows, key=lambda r: -r["resident_bytes"])[:top]:
        L.append(f"| {r['name']} | {r['module']} | {r['pack']} | {r['format']} | {r['width']}x{r['height']} | "
                 f"{r['mips']} | {r['faces']} | {r['resident_bytes']:,} | {r['formula_bytes']} | "
                 f"{' '.join(r['flags'])} | {r['referenced_by'][:120]} |")
    flagged = [r for r in m.texture_rows if r["flags"]]
    L.append("")
    L.append(f"{len(flagged)} of {len(m.texture_rows)} referenced textures carry a flag "
             f"(full list in `textures.tsv`).")
    L.append("")
    L.append(f"## Meshes (top {top} of {len(m.mesh_rows)})")
    L.append("")
    L.append("Each mesh record (one per material and per LOD) owns two segments: 5f98413d holds editor-format "
             "geometry (a position count then float4 positions) and 97f81dbb holds the runtime streams (an index "
             "count, u16 indices, an attribute stream table). Which of the two the shipping client keeps resident "
             "is not determined here, so `total bytes` sums both plus the small table segment and the summary "
             "carries each as a breakdown. `largest` counts are (unique positions, faces, split vertices) of the "
             "record with the most faces. EDITOR_STREAM_BLOAT marks a mesh whose editor segment is more than "
             "10x its runtime segment.")
    L.append("")
    L.append("| name | module | pack | records | largest positions | largest faces | editor seg | runtime seg | total bytes | flags | scene direct | via prefabs | flora kind instances |")
    L.append("|---|---|---|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|")
    for r in sorted(m.mesh_rows, key=lambda r: -r["total_bytes"])[:top]:
        L.append(f"| {r['name']} | {r['module']} | {r['pack']} | {r['records']} | {r['largest_positions']} | "
                 f"{r['largest_faces']} | {r['seg_5f98413d_bytes']:,} | {r['seg_97f81dbb_bytes']:,} | "
                 f"{r['total_bytes']:,} | {' '.join(r['flags'])} | {r['scene_direct']} | {r['via_prefabs']} | "
                 f"{r['flora_kind_instances']} |")
    L.append("")
    L.append(f"## Prefabs ({len(m.prefab_rows)} distinct, by scene instance count)")
    L.append("")
    L.append("| prefab | module | file | instances | entities each | distinct meshes | mesh bytes (once) | physics shapes |")
    L.append("|---|---|---|---:|---:|---:|---:|---:|")
    for r in m.prefab_rows:
        L.append(f"| {r['prefab']} | {r['module']} | {r['file']} | {r['scene_instances']} | {r['entities_per_instance']} | "
                 f"{r['distinct_meshes']} | {r['mesh_bytes']:,} | {r['physics_shapes']} |")
    L.append("")
    L.append(f"## Flora ({len(m.flora_rows)} kinds in references.txt)")
    L.append("")
    fb = m.flora_bin
    L.append(f"`flora.bin` header count {fb.header_count:,}; walked {fb.walked:,} records "
             f"({'consistent' if fb.walk_ok else 'INCONSISTENT'}). Instance counts are per kind from the walk; "
             "`variation meshes` is the union over all four seasons.")
    L.append("")
    L.append("| kind | defined in | instances | seasons | variation meshes | mesh bytes (once) | unresolved meshes |")
    L.append("|---|---|---:|---|---:|---:|---:|")
    for r in sorted(m.flora_rows, key=lambda r: -r["instances"]):
        L.append(f"| {r['kind']} | {r['defined_in']} | {r['instances']:,} | {r['seasons']} | {r['variation_meshes']} | "
                 f"{r['mesh_bytes']:,} | {r['unresolved_meshes']} |")
    L.append("")
    L.append(f"## Unresolved references ({len(m.unresolved)})")
    L.append("")
    if m.unresolved:
        L.append("| kind | name | referenced by |")
        L.append("|---|---|---|")
        for kind, name, refs in m.unresolved:
            L.append(f"| {kind} | {name} | {refs[:160]} |")
    else:
        L.append("none")
    L.append("")
    L.append("## Method")
    L.append("")
    L.append("Decoded container and metadata layouts, with how each was verified, are in the module "
             "docstring of `tools/audit_map_scene_memory.py`. Duplicate names across modules are resolved "
             "in the module order above and listed in the `also_in` column of the TSVs; which copy the "
             "engine actually loads when two modules ship the same name is not determined here.")
    L.append("")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L) + "\n")


def refuse_inside_game(out_path, game_root) -> None:
    try:
        Path(out_path).resolve().relative_to(Path(game_root).resolve())
    except ValueError:
        return
    raise SystemExit(f"refusing to write inside the game install: {out_path}")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Attribute the campaign map scene's asset payload from its own files.")
    ap.add_argument("--game-dir", default=game_dir(DEFAULT_GAME), help="Bannerlord install root")
    ap.add_argument("--scene", default=None, help="scene folder (default <game>/Modules/TAOM_Map/SceneObj/Main_map)")
    ap.add_argument("--modules", default=",".join(DEFAULT_MODULES),
                    help="module names in resolution priority order (default TAOM_Map,Native,SandBox)")
    ap.add_argument("--flora-kinds", action="append", default=[], help="extra flora_kinds.xml to consult")
    ap.add_argument("--out-dir", default=DEFAULT_OUT, help="default folder for the report and TSVs")
    ap.add_argument("--report", default=None, help="Markdown report path (default <out-dir>/map-scene-memory.md)")
    ap.add_argument("--tsv-dir", default=None, help="TSV folder (default <out-dir>)")
    ap.add_argument("--top", type=int, default=50, help="rows in the top-N tables")
    args = ap.parse_args(argv)

    game_root = Path(args.game_dir)
    game_modules = game_root / "Modules"
    scene_dir = Path(args.scene) if args.scene else game_modules / "TAOM_Map" / "SceneObj" / DEFAULT_SCENE
    if not (scene_dir / "scene.xscene").is_file():
        print(f"scene.xscene not found under {scene_dir}; is the game install path right?")
        return 2
    report = Path(args.report) if args.report else Path(args.out_dir) / "map-scene-memory.md"
    tsv_dir = Path(args.tsv_dir) if args.tsv_dir else Path(args.out_dir)
    for target in (report.parent, tsv_dir):
        refuse_inside_game(target, game_root)
        target.mkdir(parents=True, exist_ok=True)

    modules = [x.strip() for x in args.modules.split(",") if x.strip()]
    manifest = build_manifest(game_modules, scene_dir, modules, args.flora_kinds)
    written = write_tsvs(manifest, tsv_dir)
    write_report(manifest, report, top=args.top)
    print("\n".join(summary_lines(manifest, top=20)))
    print()
    print(f"report: {report}")
    for path in written:
        print(f"tsv:    {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
