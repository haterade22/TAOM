#!/usr/bin/env python3
"""List every Armoury mesh that ships with only LOD0, straight from the FBX sources.

WHY THIS EXISTS
A mesh with no distance LODs is drawn at full detail at every range, and nothing in the repo
could say which ones those were: `catalogue.tsv` (tools/generate_armory_catalogue.py) sees one
row per compiled metamesh and cannot count its LODs, because the Kit keeps a metamesh's LODs as
records inside it. The FBX sources in `LOTRLOME_Armory/AssetSources` still name every LOD as its
own object, so they can.

HOW IT READS AN FBX
A small reader for the binary FBX format (7.x) walks `Objects` and `Connections` and returns, per
mesh model: triangle count, vertex count, blend-shape (morph) channel names in order, and material
names. No Blender, so the whole Armoury (1,148 FBX, 3.8 GB on 2026-09-25) reads in about a
minute. ASCII FBX is refused rather than skipped.

WHAT COUNTS AS A LOD
LOD0 is the bare object name or `<name>_lod0`; LOD N is `<name>.lodN` or `<name>_lodN`. The Kit
folds both schemes into one metamesh (`catalogue.tsv` holds only `dwarf_hair_a` for an FBX of
`Dwarf_Hair_A_lod0` to `_lod3`). `<mesh>.<part>` (`.eyes`, `.mouth`, `.base`) is a named sub-mesh
of the same metamesh with its own LOD chain (docs/features/troll-race.md, the hill troll head).
A name the Kit cannot fold (`.lod` with no number, `.lod2.001`, `.od2`, a Blender `.001`
duplicate) is reported as a naming defect, and so is a family of numbered sub-meshes whose
triangle counts fall step by step (`.base`, `.base1` .. `.base5`): a LOD chain named as parts,
which the Kit draws all at once. `bo_` collision bodies are skipped.

WHAT IT JOINS
Each FBX is matched to the tpac a Kit import writes (`AssetSources/<rel>.fbx` to
`Assets/<rel>_geo.tpac`), and the chain's metamesh is looked up in the LIVE tpacs through
`generate_armory_catalogue.build_rows` (about 2 seconds): shipped from this FBX's tpac or not,
and referenced by an item or not. Not the committed `catalogue.tsv`, which lags every Kit import
until someone regenerates it (on 2026-09-25 it predated the hill troll). An FBX whose own tpac
does not ship a mesh (an animation clip, a skeleton export, an old copy) is listed apart.
`ModuleData/skins.xml` supplies what a race wears (body, head, hair, beard), which items never
name.

Usage:
  python tools/audit_fbx_lods.py                 # write docs/reference/armory-catalogue/lod-audit.md
  python tools/audit_fbx_lods.py --check         # drift gate: exit 1 if the report no longer
                                                 # reproduces (the Armory is unversioned, so a
                                                 # reinstall silently reverts a LOD pass)
  python tools/audit_fbx_lods.py --fbx X.fbx     # one file's chains: levels and triangles
  python tools/audit_fbx_lods.py --diff A.fbx B.fbx
                                                 # per-mesh delta between two FBX (a .bak and its rewrite),
                                                 # plus the rig's bind-pose drift

Exit: 0; 1 on drift (--check); 2 on a bad path.
"""
from __future__ import annotations

import argparse
import re
import struct
import sys
import xml.etree.ElementTree as ET
import zlib
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import generate_armory_catalogue as gac  # noqa: E402
import validate_mesh_refs as vm  # noqa: E402
from _gamedir import ensure_exists, game_dir  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_MODULE = "LOTRLOME_Armory"
OUT_MD = REPO_ROOT / "docs" / "reference" / "armory-catalogue" / "lod-audit.md"

# Every mesh carries LOD0 to LOD5 (Mike, 2026-09-25); cloth needs none.
REQUIRED_LEVELS = frozenset(range(6))

DEFECT_NO_NUMBER = "`.lod` with no number"
DEFECT_DUPLICATE = "Blender duplicate suffix"
DEFECT_TYPO = "`.odN`, a `.lodN` typo"
DEFECT_NO_LOD0 = "LODs but no LOD0"
DEFECT_NUMBERED = "numbered sub-mesh with fewer tris each step, a LOD chain named as parts"
DEFECT_RANGE = "LOD number past 9, a typo"

# --------------------------------------------------------------------------- #
# binary FBX reader
# --------------------------------------------------------------------------- #
FBX_MAGIC = b"Kaydara FBX Binary  \x00"
_SCALARS = {b"Y": "<h", b"C": "<?", b"I": "<i", b"F": "<f", b"D": "<d", b"L": "<q"}
_ARRAYS = {b"f": ("f", 4), b"d": ("d", 8), b"l": ("q", 8), b"i": ("i", 4), b"b": ("?", 1)}
_KEEP_TOP = ("Objects", "Connections")


@dataclass
class FbxMesh:
    name: str
    tris: int
    vertices: int
    channels: list = field(default_factory=list)
    materials: list = field(default_factory=list)
    shapes: list = field(default_factory=list)       # each channel's shape geometry, in order


def _parse_tree(data: bytes) -> dict:
    """Top-level `Objects` and `Connections` as (name, props, children) tuples. Array properties
    are decoded only for PolygonVertexIndex and TransformLink; any other array becomes its
    element count."""
    if not data.startswith(FBX_MAGIC):
        raise ValueError("not a binary FBX (ASCII FBX is not supported)")
    version = struct.unpack_from("<I", data, 23)[0]
    wide = version >= 7500
    null_len = 25 if wide else 13

    def prop(off, decode):
        t = data[off:off + 1]
        off += 1
        if t in _SCALARS:
            fmt = _SCALARS[t]
            return struct.unpack_from(fmt, data, off)[0], off + struct.calcsize(fmt)
        if t in _ARRAYS:
            n, enc, clen = struct.unpack_from("<III", data, off)
            off += 12
            if not decode:
                return n, off + clen
            raw = data[off:off + clen]
            if enc:
                raw = zlib.decompress(raw)
            code, size = _ARRAYS[t]
            return struct.unpack_from("<%d%s" % (n, code), raw[:n * size]), off + clen
        if t in (b"S", b"R"):
            n = struct.unpack_from("<I", data, off)[0]
            off += 4
            return data[off:off + n], off + n
        raise ValueError("unknown FBX property type %r at byte %d" % (t, off - 1))

    def node(off, top):
        if wide:
            end, nprops, _ = struct.unpack_from("<QQQ", data, off)
            off += 24
        else:
            end, nprops, _ = struct.unpack_from("<III", data, off)
            off += 12
        nlen = data[off]
        off += 1
        name = data[off:off + nlen].decode("latin-1")
        off += nlen
        if end == 0:
            return None, off
        if top and name not in _KEEP_TOP:
            return (name, None, None), end
        decode = name in ("PolygonVertexIndex", "TransformLink")
        props = []
        for _ in range(nprops):
            value, off = prop(off, decode)
            props.append(value)
        kids = []
        while off < end - null_len:
            kid, off = node(off, False)
            if kid is None:
                break
            kids.append(kid)
        return (name, props, kids), end

    tree = {}
    off = 27
    while off < len(data) - null_len:
        n, off = node(off, True)
        if n is None:
            break
        if n[1] is not None:
            tree[n[0]] = n
    return tree


def _obj_name(raw: bytes) -> str:
    return raw.split(b"\x00\x01")[0].decode("utf-8", "replace")


def read_fbx_meshes(path) -> list:
    """Every mesh model in a binary FBX, in file order."""
    with open(path, "rb") as f:
        tree = _parse_tree(f.read())
    objects = tree["Objects"][2] if "Objects" in tree else []
    by_id = {o[1][0]: o for o in objects if o[1]}
    kids = defaultdict(list)
    for c in (tree["Connections"][2] if "Connections" in tree else []):
        if c[1] and c[1][0] == b"OO":
            kids[c[1][2]].append(c[1][1])

    def children(oid, kind, subtype=None):
        out = []
        for cid in kids.get(oid, ()):
            o = by_id.get(cid)
            if o and o[0] == kind and (subtype is None or o[1][2] == subtype):
                out.append(o)
        return out

    meshes = []
    for o in objects:
        if o[0] != "Model" or len(o[1]) < 3 or o[1][2] != b"Mesh":
            continue
        tris = verts = 0
        channels, shapes = [], []
        for g in children(o[1][0], "Geometry", b"Mesh"):
            for sub in g[2]:
                if sub[0] == "PolygonVertexIndex" and sub[1]:
                    n = 0
                    for v in sub[1][0]:
                        n += 1
                        if v < 0:
                            tris += n - 2
                            n = 0
                elif sub[0] == "Vertices" and sub[1]:
                    verts += sub[1][0] // 3
            for bs in children(g[1][0], "Deformer", b"BlendShape"):
                for ch in children(bs[1][0], "Deformer", b"BlendShapeChannel"):
                    channels.append(_obj_name(ch[1][1]))
                    shapes += [_obj_name(s[1][1]) for s in children(ch[1][0], "Geometry", b"Shape")]
        materials = [_obj_name(m[1][1]) for m in children(o[1][0], "Material")]
        meshes.append(FbxMesh(_obj_name(o[1][1]), tris, verts, channels, materials, shapes))
    return meshes


# --------------------------------------------------------------------------- #
# naming
# --------------------------------------------------------------------------- #
_LOD_DUP_RE = re.compile(r"\.lod\d+\.\d{3}$", re.IGNORECASE)
_LOD_RE = re.compile(r"[._]lod(\d+)$", re.IGNORECASE)
_NO_NUMBER_RE = re.compile(r"\.lod$", re.IGNORECASE)
_TYPO_RE = re.compile(r"\.od\d+$", re.IGNORECASE)
_DUP_RE = re.compile(r"\.\d{3}$")
_CLOTH_RE = re.compile(r"(^clo_|_clo$|_clo_)", re.IGNORECASE)


def split_lod(name: str):
    """(chain, level, defect). A defective name is its own chain with level None."""
    if _LOD_DUP_RE.search(name):
        return name, None, DEFECT_DUPLICATE
    m = _LOD_RE.search(name)
    if m:
        if int(m.group(1)) > 9:
            return name, None, DEFECT_RANGE
        return name[:m.start()], int(m.group(1)), None
    if _NO_NUMBER_RE.search(name):
        return name, None, DEFECT_NO_NUMBER
    if _TYPO_RE.search(name):
        return name, None, DEFECT_TYPO
    if _DUP_RE.search(name):
        return name, None, DEFECT_DUPLICATE
    return name, 0, None


def metamesh_of(chain: str) -> str:
    return chain.split(".")[0].lower()


def is_cloth(chain: str) -> bool:
    return bool(_CLOTH_RE.search(chain))


def tpac_for(fbx_rel: str) -> str:
    rel = fbx_rel.replace("\\", "/")
    stem = rel[:-4] if rel.lower().endswith(".fbx") else rel
    return stem + "_geo.tpac"


# --------------------------------------------------------------------------- #
# joining and classification
# --------------------------------------------------------------------------- #
@dataclass
class Chain:
    fbx: str
    chain: str
    defect: str | None = None
    tris: dict = field(default_factory=dict)          # level -> triangles
    shipped_by: list = field(default_factory=list)    # tpacs that ship the metamesh
    used_by: str = ""

    @property
    def levels(self) -> set:
        return set(self.tris)

    @property
    def top(self) -> int:
        return max(self.tris) if self.tris else -1

    @property
    def lod0_tris(self) -> int:
        return self.tris.get(0, 0)

    @property
    def missing(self) -> set:
        return set(REQUIRED_LEVELS - self.levels)

    @property
    def metamesh(self) -> str:
        return metamesh_of(self.chain)


def build_chains(fbx_rel: str, meshes) -> list:
    """Group one FBX's meshes into LOD chains, case-insensitively, in first-seen order."""
    chains = {}
    for m in meshes:
        if m.name.lower().startswith("bo_"):
            continue
        name, level, defect = split_lod(m.name)
        key = name.lower()
        c = chains.get(key)
        if c is None:
            c = chains[key] = Chain(fbx_rel.replace("\\", "/"), name, defect)
        if level is not None:
            c.tris[level] = c.tris.get(level, 0) + m.tris
    return list(chains.values())


def catalogue_from_rows(rows) -> dict:
    """metamesh -> {"tpacs": set, "referenced": "Y"/"SLIM"/"N"}, from catalogue rows."""
    out = {}
    for r in rows:
        if r["kind"] != "metamesh":
            continue
        row = out.setdefault(r["mesh"].lower(), {"tpacs": set(), "referenced": "N"})
        row["tpacs"].add(r["tpac"])
        if r["referenced"] in ("Y", "SLIM"):
            row["referenced"] = r["referenced"]
    return out


def live_catalogue(module_root: Path) -> dict:
    """The catalogue as the live tpacs stand now, built the way generate_armory_catalogue.py
    builds it (same `referenced` rule)."""
    refs = vm.extract_refs(module_root / "ModuleData")
    referenced = {re.sub(r"\.lod\d+$", "", r.name, flags=re.IGNORECASE)
                  for r in refs if r.kind in ("visual_mesh", "collision_body")}
    return catalogue_from_rows(gac.build_rows(module_root, referenced))


def skins_mesh_refs(path) -> set:
    """Lowercased mesh names skins.xml puts on a race: every `*mesh*` attribute of a <skin>,
    and the name and cover types of each hair, beard and eyebrow mesh."""
    refs = set()
    for el in ET.parse(path).getroot().iter():
        for key, value in el.attrib.items():
            if not value:
                continue
            if "mesh" in key or re.fullmatch(r"cover_type\d", key) or (
                    key == "name" and el.tag.endswith("_mesh")):
                refs.add(value.lower())
    return refs


_NUMBERED_PART_RE = re.compile(r"^(.*\D)(\d+)$")


def mark_numbered_parts(chains) -> None:
    """Flag `X.base1` .. `X.baseN` beside `X.base` when every step has fewer triangles than the
    one before: that is a LOD chain named as sub-meshes, and the Kit draws each part at once."""
    groups = defaultdict(dict)
    for c in chains:
        if c.defect or "." not in c.chain or 0 not in c.tris:
            continue
        part = c.chain.split(".", 1)[1]
        m = _NUMBERED_PART_RE.match(part)
        stem, n = (m.group(1), int(m.group(2))) if m else (part, 0)
        groups[(c.fbx, c.metamesh, stem.lower())][n] = c
    for members in groups.values():
        if len(members) < 2 or 0 not in members:
            continue
        order = sorted(members)
        tris = [members[n].lod0_tris for n in order]
        if all(a > b for a, b in zip(tris, tris[1:])):
            for n in order[1:]:
                members[n].defect = DEFECT_NUMBERED


def classify(chains, catalogue: dict, skins: set) -> dict:
    """Sort chains into report sections. Only the FBX whose own tpac ships a mesh reports it;
    every other copy lands in `excluded`."""
    sections = {k: [] for k in ("fix", "incomplete", "unreferenced", "defects", "excluded")}
    mark_numbered_parts(chains)
    for c in chains:
        if is_cloth(c.chain):
            continue      # cloth needs no LODs (Mike, 2026-09-25)
        row = catalogue.get(c.metamesh)
        c.shipped_by = sorted(row["tpacs"]) if row else []
        users = []
        if row and row["referenced"] in ("Y", "SLIM"):
            users.append("item")
        if c.metamesh in skins:
            users.append("skins")
        c.used_by = ", ".join(users)
        shipped = tpac_for(c.fbx) in c.shipped_by
        if not c.defect and 0 not in c.levels:
            c.defect = DEFECT_NO_LOD0
        if c.defect:
            sections["defects" if shipped else "excluded"].append(c)
            continue
        if not c.missing:
            continue
        if not shipped:
            sections["excluded"].append(c)
        elif not users:
            sections["unreferenced"].append(c)
        elif c.levels == {0}:
            sections["fix"].append(c)
        else:
            sections["incomplete"].append(c)
    for rows in sections.values():
        rows.sort(key=lambda c: (c.fbx.lower(), c.chain.lower()))
    return sections


# --------------------------------------------------------------------------- #
# report
# --------------------------------------------------------------------------- #
SECTION_TEXT = [
    ("fix", "LOD0 only",
     "Ships from its own FBX, an item or a race skin uses it, and it has no LODs at all."),
    ("incomplete", "Incomplete chains",
     "Ships, is used, and has LODs, but not every one of LOD1 to LOD5: it stops early or skips a "
     "level (the Kit then draws the level before it)."),
    ("unreferenced", "Shipped, unreferenced",
     "Missing LODs, but no item and no skin names it, so nothing draws it."),
    ("defects", "Naming defects",
     "Names the Kit cannot fold into a LOD chain. Rename them in the FBX before adding LODs."),
    ("excluded", "Excluded",
     "Incomplete or misnamed meshes in an FBX whose own tpac does not ship them: animation "
     "clips, skeleton exports and duplicate FBX copies. Listed per file."),
]


def _anchor(title: str) -> str:
    return re.sub(r"[^a-z0-9 -]", "", title.lower()).replace(" ", "-")


def render(sections: dict, stats: dict) -> str:
    out = [
        "# Armoury LOD audit",
        "",
        "GENERATED by `tools/audit_fbx_lods.py`, do not hand-edit. Regenerate after an art drop "
        "or a LOD pass.",
        "",
        f"Source: every FBX under `{stats['module']}/AssetSources` ({stats['fbx']} files, "
        f"{stats['with_meshes']} holding meshes, {stats['chains']} mesh chains), joined to "
        "the live tpacs for what ships and what an item names (the "
        "[catalogue.tsv](catalogue.tsv) scan, run fresh), and to `ModuleData/skins.xml` for "
        "what a race wears. A chain is one mesh or sub-mesh "
        "(`<mesh>.<part>`) with its LODs (`.lodN` or `_lodN`). Every mesh carries LOD0 to LOD5; "
        "`bo_` collision bodies and `clo_` cloth meshes need none and are skipped.",
        "",
        "| Section | Chains | Meaning |",
        "|---|---|---|",
    ]
    for key, title, text in SECTION_TEXT:
        out.append(f"| [{title}](#{_anchor(title)}) | {len(sections[key])} | {text} |")

    for key, title, text in SECTION_TEXT:
        rows = sections[key]
        out += ["", f"## {title}", "", text, ""]
        if not rows:
            out.append("None.")
            continue
        if key == "excluded":
            per_fbx = defaultdict(list)
            for c in rows:
                per_fbx[c.fbx].append(c)
            out += ["| FBX | Chains | Shipped instead by |", "|---|---|---|"]
            for fbx, cs in per_fbx.items():
                others = sorted({t for c in cs for t in c.shipped_by})
                where = ", ".join(f"`{t}`" for t in others[:3]) or "nothing"
                if len(others) > 3:
                    where += f" and {len(others) - 3} more"
                out.append(f"| `{fbx}` | {len(cs)} | {where} |")
            continue
        if key == "defects":
            out += ["| FBX | Object | Defect | Used by |", "|---|---|---|---|"]
            for c in rows:
                out.append(f"| `{c.fbx}` | `{c.chain}` | {c.defect} | {c.used_by or '-'} |")
            continue
        if key in ("incomplete", "unreferenced"):
            out += ["| FBX | Mesh | Has | Missing | LOD0 tris | Used by |",
                    "|---|---|---|---|---|---|"]
            for c in rows:
                has = ",".join(str(x) for x in sorted(c.levels))
                miss = ",".join(str(x) for x in sorted(c.missing))
                out.append(f"| `{c.fbx}` | `{c.chain}` | {has} | {miss} | {c.lod0_tris:,} | "
                           f"{c.used_by or '-'} |")
            continue
        out += ["| FBX | Mesh | LOD0 tris | Used by |", "|---|---|---|---|"]
        for c in rows:
            out.append(f"| `{c.fbx}` | `{c.chain}` | {c.lod0_tris:,} | {c.used_by or '-'} |")
    return "\n".join(out) + "\n"


def diff_meshes(a, b) -> list:
    """Per-mesh delta from `a` to `b`: added (+), removed (-), changed (~)."""
    before = {m.name: m for m in a}
    after = {m.name: m for m in b}
    lines = []
    for name in sorted(set(before) | set(after), key=str.lower):
        x, y = before.get(name), after.get(name)
        if x is None:
            lines.append(f"+ {name}: {y.tris} tris, {y.vertices} verts, "
                         f"{len(y.channels)} channels, materials {y.materials}")
            continue
        if y is None:
            lines.append(f"- {name}")
            continue
        changes = []
        if x.tris != y.tris:
            changes.append(f"tris {x.tris} -> {y.tris}")
        if x.vertices != y.vertices:
            changes.append(f"verts {x.vertices} -> {y.vertices}")
        if x.channels != y.channels:
            if x.shapes and x.shapes == y.shapes:
                changes.append("channel labels renamed, shape geometry and order unchanged")
            else:
                changes.append(f"channels {len(x.channels)} -> {len(y.channels)}"
                               + (" (same names, new order)" if sorted(x.channels) == sorted(y.channels)
                                  else ""))
        elif x.shapes != y.shapes:
            changes.append(f"shape geometry {len(x.shapes)} -> {len(y.shapes)}")
        if x.materials != y.materials:
            changes.append(f"materials {x.materials} -> {y.materials}")
        if changes:
            lines.append(f"~ {name}: " + "; ".join(changes))
    return lines


def _bind_matrices(path) -> dict:
    """bone name -> its cluster's TransformLink (the bone's bind pose, column-major 4x4)."""
    with open(path, "rb") as f:
        tree = _parse_tree(f.read())
    objects = tree["Objects"][2] if "Objects" in tree else []
    by_id = {o[1][0]: o for o in objects if o[1]}
    parents = defaultdict(list)
    for c in (tree["Connections"][2] if "Connections" in tree else []):
        if c[1] and c[1][0] == b"OO":
            parents[c[1][1]].append(c[1][2])
    out = {}
    for o in objects:
        if o[0] != "Model" or len(o[1]) < 3 or o[1][2] != b"LimbNode":
            continue
        for pid in parents.get(o[1][0], ()):
            cl = by_id.get(pid)
            if cl and cl[0] == "Deformer" and cl[1][2] == b"Cluster":
                link = [s for s in cl[2] if s[0] == "TransformLink" and s[1]]
                if link:
                    out.setdefault(_obj_name(o[1][1]), link[0][1][0])
    return out


def _unit_frame(m):
    axes = [m[0:3], m[4:7], m[8:11]]
    s = sum(x * x for x in axes[0]) ** 0.5 or 1.0
    return [[x / s for x in a] for a in axes], [x / s for x in m[12:15]]


def bind_pose_drift(a, b):
    """(bones compared, max axis drift, max offset drift, rig extent) between two FBX rigs, each
    bone taken relative to one reference bone of its own file. A Blender export rewrites the
    file's global axis system (the raw bind matrices of a round-tripped hair FBX differ by a 180
    degree flip), which this cancels; a bone that really moved or turned does not cancel. The
    extent (farthest bone from the reference, same units as the offsets) scales the offset: the
    warg's 0.0013 is round-off on a 315-unit rig."""
    A, B = _bind_matrices(a), _bind_matrices(b)
    common = sorted(set(A) & set(B))
    if not common:
        return 0, 0.0, 0.0, 0.0
    ref = "pelvis" if "pelvis" in common else common[0]

    def rel(m, r):
        (Rb, tb), (Ra, ta) = _unit_frame(m), _unit_frame(r)
        axes = [[sum(u[k] * v[k] for k in range(3)) for v in Ra] for u in Rb]
        d = [tb[k] - ta[k] for k in range(3)]
        return axes, [sum(d[k] * v[k] for k in range(3)) for v in Ra]

    axis = offset = extent = 0.0
    for bone in common:
        (xa, oa), (xb, ob) = rel(A[bone], A[ref]), rel(B[bone], B[ref])
        axis = max(axis, max(abs(p - q) for u, v in zip(xa, xb) for p, q in zip(u, v)))
        offset = max(offset, max(abs(p - q) for p, q in zip(oa, ob)))
        extent = max(extent, sum(x * x for x in oa) ** 0.5)
    return len(common), axis, offset, extent


def drifted(committed, fresh) -> bool:
    """True when the committed report (None if absent) no longer matches a fresh run."""
    if committed is None:
        return True
    return committed.replace("\r\n", "\n") != fresh.replace("\r\n", "\n")


def describe_fbx(path) -> list:
    meshes = read_fbx_meshes(path)
    lines = []
    for c in build_chains(Path(path).name, meshes):
        levels = " ".join(f"L{lv}={c.tris[lv]:,}" for lv in sorted(c.tris))
        lines.append(f"{c.chain}: {levels}" + (f"  [{c.defect}]" if c.defect else ""))
    for m in meshes:
        if m.channels:
            lines.append(f"  {m.name}: {len(m.channels)} channels, first {m.channels[0]}, "
                         f"materials {m.materials}")
    return lines


def main() -> int:
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game", default=str(DEFAULT_GAME))
    ap.add_argument("--module", default=DEFAULT_MODULE)
    ap.add_argument("--out", default=str(OUT_MD))
    ap.add_argument("--check", action="store_true",
                    help="regenerate in memory and exit 1 if the committed report differs; writes nothing")
    ap.add_argument("--fbx", help="print one FBX's LOD chains and morph channels, write nothing")
    ap.add_argument("--diff", nargs=2, metavar=("A", "B"), help="per-mesh delta, write nothing")
    args = ap.parse_args()

    if args.diff:
        a, b = (ensure_exists(p, "the FBX") for p in args.diff)
        lines = diff_meshes(read_fbx_meshes(a), read_fbx_meshes(b))
        print("\n".join(lines) if lines else "no mesh differences")
        bones, axis, offset, extent = bind_pose_drift(a, b)
        if bones:
            print(f"bind pose: {bones} bones in both, max drift relative to the reference bone: "
                  f"axes {axis:.2g}, offsets {offset:.2g} on a rig {extent:.4g} across (file units)")
        return 0
    if args.fbx:
        print("\n".join(describe_fbx(ensure_exists(args.fbx, "the FBX"))))
        return 0

    game = ensure_exists(args.game, "the Bannerlord install")
    module_root = ensure_exists(game / "Modules" / args.module, f"the {args.module} module")
    sources = ensure_exists(module_root / "AssetSources", "the AssetSources folder")
    catalogue = live_catalogue(module_root)
    skins =skins_mesh_refs(ensure_exists(module_root / "ModuleData" / "skins.xml", "skins.xml"))

    chains = []
    files = sorted(p for p in sources.rglob("*") if p.suffix.lower() == ".fbx")
    with_meshes = 0
    for fbx in files:
        meshes = read_fbx_meshes(fbx)
        if meshes:
            with_meshes += 1
        chains += build_chains(fbx.relative_to(sources).as_posix(), meshes)
    sections = classify(chains, catalogue, skins)
    stats = {"module": args.module, "fbx": len(files), "with_meshes": with_meshes,
             "chains": len(chains)}
    text = render(sections, stats)
    if args.check:
        out = Path(args.out)
        if drifted(out.read_text(encoding="utf-8") if out.exists() else None, text):
            print(f"DRIFT: {out} does not reproduce from the live Armory. An FBX changed (a LOD "
                  "pass, an art drop, or a reinstall reverting one); regenerate and read the diff.",
                  file=sys.stderr)
            return 1
        print(f"OK: {out} reproduces")
        return 0
    Path(args.out).write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {args.out}")
    for key, title, _ in SECTION_TEXT:
        print(f"  {title:24} {len(sections[key])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
