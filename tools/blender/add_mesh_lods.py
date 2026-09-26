#!/usr/bin/env python3
"""
Add missing distance LODs to meshes in an Armoury FBX, as decimated copies of the lowest LOD
the mesh already has.

WHY THIS EXISTS
`tools/audit_fbx_lods.py` lists the meshes that ship with only LOD0 or with a chain that stops
early (docs/reference/armory-catalogue/lod-audit.md). The first batch (2026-09-25): the dwarf
hairs stopped at LOD3 (A to E) and LOD2 (F to J), and the Gundabad and Dol Guldur uruk basemeshes
had no LODs at all.

WHAT A LOD IS HERE (measured on the Armoury's race FBX, 2026-09-25)
- Only LOD0 carries the 101 face-morph channels, on every race head and every hair; every LOD1+
  is a plain skinned mesh. So a new LOD is a copy of the lowest existing LOD with its shape keys
  dropped, then collapse-decimated. The armature parent and modifier, vertex groups (weights are
  interpolated by the collapse) and material slots come with the copy.
- The ratio chain the elf and KEYForce dwarf basemeshes use, and hairs A to E, is LOD1 70%,
  LOD2 30%, LOD3 15%, LOD4 7%, LOD5 3% of LOD0's triangles. `--ratios` is always relative to
  LOD0, whichever LOD the copy starts from.
- Names follow the file's own scheme: `<name>_lodN` where LOD0 is `<name>_lod0` (the hairs),
  otherwise `<name>.lodN`. The Kit folds both into one metamesh.

`--lock-boundary` keeps the open-boundary vertices (neck, wrist, ankle, waist rings; eye and
mouth holes) where they are, so a stitched body part still meets its neighbour at every LOD. The
collapse cost of an edge grows with `length * (2 - w1 - w2) * factor`, so boundary vertices sit
outside a temporary weight-1 group and the factor is the maximum. That is a penalty, not a hard
lock, so the report counts every source boundary vertex with no LOD vertex on it and a non-zero
count fails the run. Never use it on hair: a hair card is all boundary.

HOW TO RUN (Windows, Blender from the Microsoft Store)
    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/add_mesh_lods.py -- --fbx <file.fbx> ^
        --mesh <Base> [--mesh <Base>@<maxlevel> ...] --levels 4,5 --ratios 0.07,0.03 ^
        [--lock-boundary] [--apply]

`--mesh` names the LOD0 base: `Dwarf_Hair_A` for `Dwarf_Hair_A_lod0`, `SK_Head.eyes` for a
sub-mesh. `@4` caps that mesh at LOD4 (eyes and mouth stop there on the elf and the dwarf).
A requested level that already exists is refused, so a re-run cannot stack copies, unless
`--replace` says to rebuild it.

ABSOLUTE BUDGETS (KEYforce's warg and saddle specs, 2026-09-25)
    --levels 2,3,4,5 --tris 5000,2000,1000,plane --replace --delete-levels 6
    --levels 0,1,2,3,4,5 --tris 15000,10000,5000,2000,500,100 --replace ^
        --drop-small 2,3,4,5:0.15 --bulky-only 4,5:0.5
- `--tris` replaces `--ratios` with triangle counts. `plane` makes that level one tiny quad at
  the mesh's centre, weighted to its main bone: it draws nothing, so the mesh vanishes at range
  instead of holding its last real LOD there (the fur's last LOD). A plane is the last level.
- `--replace` rebuilds levels that exist. LOD0 itself is rebuilt only with `--tris`, and never
  when it carries morphs. Each level is decimated from the lowest-detail LOD that survives the
  run with more triangles than the target, else from LOD0 as it was before the run.
- `--delete-levels` removes whole LOD objects (the fur's LOD6).
- `--drop-small LEVELS:F` drops, at those levels, every loose part whose longest side is under
  F of the mesh's longest side (the saddle's buckles and rivets). `--bulky-only LEVELS:R` keeps
  only parts whose shortest side is at least R of their own longest (the saddle's seat, not its
  straps).

The launcher DETACHES: this script writes <fbx>.lods-report.json on every path, a bad argument
included, and rewrites it at each step, so the caller polls until `"stage": "done"`. A report
stuck on another stage means Blender died natively there (no traceback, no crash log). Launch
one run at a time: of three launches fired back to back on 2026-09-25, only the first ran. A dry
run leaves the staged `<stem>.lods.fbx` beside the source; `--apply` consumes it. Move the report and the `.bak-lods` out of `AssetSources/` once the
Kit import is done.

SAFETY
Writes the staged sibling and re-imports it, then compares every pre-existing object's vertex,
polygon, dimension, location, UV-layer, material and shape-key state against the original, and
each new LOD against what was built (dimensions and locations to float round-off; empties are
exported too, since they parent LODs and collision bodies). Any drift, a LOD with more unweighted
vertices than its source, or (with `--lock-boundary`) any lost seam vertex refuses `--apply`.
A `--plan` may carry naming fixes (`delete`, `renames`), applied before any level is built. `--apply` first writes a write-once
`<fbx>.bak-lods`. A Modding Kit re-import of the FBX is required afterwards; a Kit re-import
rebinds materials by FBX material name, which is why the material names are part of the check.
For a Blender-independent check of the result: `python tools/audit_fbx_lods.py --diff
<fbx>.bak-lods <fbx>`.
"""
import json
import os
import shutil
import sys

import bpy

LOCK_GROUP = "__lod_lock"
# Empties too: `wm_boromir_shield` is an empty that parents the shield's LODs and collision
# bodies, and an export without it orphaned every child (2026-09-25).
EXPORT_TYPES = {"ARMATURE", "MESH", "EMPTY"}
LOCK_FACTOR = 1000.0          # the Decimate modifier's maximum vertex-group factor
MIN_TRIS = 4
BLENDER_NAME_MAX = 63


# --------------------------------------------------------------------------- #
# pure helpers (unit-tested without Blender)
# --------------------------------------------------------------------------- #
PLANE = "plane"


def _level_map(value, cast):
    """`2,3,4:0.15` -> {2: 0.15, 3: 0.15, 4: 0.15}"""
    levels, _, x = value.partition(":")
    if not x:
        raise SystemExit("%r needs LEVELS:VALUE" % value)
    return {int(lv): cast(x) for lv in levels.split(",")}


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "lock_boundary": False, "replace": False, "meshes": [], "fbx": None,
           "levels": None, "ratios": None, "tris": None, "delete_levels": [],
           "drop_small": {}, "bulky_only": {}, "plan": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--apply", "--lock-boundary", "--replace"):
            out[a[2:].replace("-", "_")] = True
            i += 1
            continue
        if a not in ("--fbx", "--mesh", "--levels", "--ratios", "--tris", "--delete-levels",
                     "--drop-small", "--bulky-only", "--plan"):
            raise SystemExit("unknown argument %r" % a)
        if i + 1 >= len(argv):
            raise SystemExit("%s needs a value" % a)
        v = argv[i + 1]
        if a in ("--fbx", "--plan"):
            out[a[2:]] = v
        elif a == "--mesh":
            name, _, cap = v.partition("@")
            out["meshes"].append((name, int(cap) if cap else None))
        elif a in ("--levels", "--delete-levels"):
            out[a[2:].replace("-", "_")] = [int(x) for x in v.split(",")]
        elif a == "--ratios":
            out["ratios"] = [float(x) for x in v.split(",")]
        elif a == "--tris":
            out["tris"] = [x if x == PLANE else int(x) for x in v.split(",")]
        else:
            out[a[2:].replace("-", "_")].update(_level_map(v, float))
        i += 2
    if out["plan"] is not None:
        if not out["fbx"]:
            raise SystemExit("--fbx is required")
        if out["meshes"] or out["levels"] or out["ratios"] or out["tris"] or out["replace"]:
            raise SystemExit("--plan carries the meshes and levels; give it alone")
        return out
    for k in ("fbx", "mesh", "levels"):
        if not (out["meshes"] if k == "mesh" else out[k]):
            raise SystemExit("--%s is required" % k)
    if (out["ratios"] is None) == (out["tris"] is None):
        raise SystemExit("--ratios is required (or --tris), and never both")
    targets = out["ratios"] if out["ratios"] is not None else out["tris"]
    if len(out["levels"]) != len(targets):
        raise SystemExit("--levels and --ratios/--tris need one entry each")
    if min(out["levels"]) < 0 or (min(out["levels"]) == 0 and not (
            out["replace"] and out["tris"] is not None)):
        raise SystemExit("LOD0 is rebuilt only with --replace and --tris; otherwise it is the source")
    pairs = sorted(zip(out["levels"], targets))
    if out["ratios"] is not None:
        if any(not 0 < r < 1 for _, r in pairs) or any(
                a[1] <= b[1] for a, b in zip(pairs, pairs[1:])):
            raise SystemExit("--ratios must be in (0, 1) and fall as the level rises")
    else:
        sizes = [t for _, t in pairs]
        if PLANE in sizes[:-1]:
            raise SystemExit("a plane is the last level; nothing can follow it")
        counts = [t for t in sizes if t != PLANE]
        if any(t < MIN_TRIS for t in counts) or any(a <= b for a, b in zip(counts, counts[1:])):
            raise SystemExit("--tris must fall as the level rises")
    return out


def load_plan(path):
    """`tools/lod_fill_batch.py`'s plan: [(base, [(level, tris), ...], lock), ...]. A plan only
    adds levels 1 to 9 with absolute counts; it never rebuilds or deletes anything."""
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    out = []
    for m in data["meshes"]:
        levels = sorted((int(k), int(v)) for k, v in m["levels"].items())
        if any(lv < 1 or lv > 9 or n < MIN_TRIS for lv, n in levels):
            raise SystemExit("%s: a plan adds levels 1 to 9 of at least %d tris" % (m["base"], MIN_TRIS))
        out.append((m["base"], levels, bool(m.get("lock", False))))
    return out


def load_plan_fixes(path):
    """The plan's fixes, applied before any level is built: (delete, renames, materials), where
    materials maps an object to the Kit material name for each of its slots, in slot order."""
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    return (list(data.get("delete", [])), dict(data.get("renames", {})),
            {k: list(v) for k, v in data.get("materials", {}).items()})


def same_vector(a, b, rel=1e-3, absolute=2e-4):
    """Dimensions or locations equal but for float round-off: rounded to 4 places they can flip
    the last digit on a round trip (0.1393 -> 0.1392 on a helmet feather)."""
    return len(a) == len(b) and all(abs(x - y) <= max(absolute, rel * abs(x)) for x, y in zip(a, b))


def fbx_from_argv(argv):
    """Best-effort `--fbx` so a report lands beside the file even when the arguments are bad."""
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    if "--fbx" in argv and argv.index("--fbx") + 1 < len(argv):
        return os.path.abspath(argv[argv.index("--fbx") + 1])
    return os.path.join(os.getcwd(), "add_mesh_lods")


def detect_scheme(base, names):
    """`underscore` when LOD0 is `<base>_lod0` (the dwarf hairs), `dot0` when it is
    `<base>.lod0` (the Dale boots), `dot` when it is `<base>` itself."""
    names = set(names)
    if base + "_lod0" in names:
        return "underscore"
    if base + ".lod0" in names:
        return "dot0"
    if base in names:
        return "dot"
    raise SystemExit("no LOD0 object %r, %r or %r in this file" % (base, base + "_lod0", base + ".lod0"))


def lod_name(base, level, scheme):
    if scheme == "underscore":
        return "%s_lod%d" % (base, level)
    if scheme == "dot0":
        return "%s.lod%d" % (base, level)
    return base if level == 0 else "%s.lod%d" % (base, level)


def existing_levels(base, names, scheme):
    names = set(names)
    return [lv for lv in range(0, 10) if lod_name(base, lv, scheme) in names]


def plan_levels(levels, targets, cap):
    return [(lv, t) for lv, t in sorted(zip(levels, targets)) if cap is None or lv <= cap]


def resolve_target(lod0_tris, ratio=None, tris=None):
    """Triangles wanted at a level: a ratio of LOD0, an absolute count, or a plane."""
    if tris is not None:
        return tris
    return target_tris(lod0_tris, ratio)


def pick_source(tris_by_level, replaced, target):
    """The lowest-detail LOD that survives this run and still has more triangles than `target`:
    the artist's own reduction beats a level that is about to be rebuilt or deleted (pass both in
    `replaced`). LOD0 (kept pristine even when it is itself rebuilt) is the fallback."""
    kept = [lv for lv, n in tris_by_level.items() if lv not in replaced and n > target]
    if kept:
        return max(kept)
    if tris_by_level.get(0, 0) > target:
        return 0
    raise SystemExit("no LOD has more than %d triangles to decimate from" % target)


def keep_island(dims, mesh_max, drop_small=None, bulky_only=None):
    """Whether a loose part survives at a level. `drop_small`: drop a part whose longest side is
    under that fraction of the whole mesh's longest side (buckles, rivets). `bulky_only`: drop a
    part whose shortest side is under that fraction of its own longest (straps, belts)."""
    d = sorted(dims, reverse=True)
    if drop_small is not None and d[0] < drop_small * mesh_max:
        return False
    if bulky_only is not None and d[0] > 0 and d[2] / d[0] < bulky_only:
        return False
    return True


def check_new_levels(base, have, plan):
    clash = [lv for lv, _ in plan if lv in have]
    if clash:
        raise SystemExit("%s already has LOD %s; nothing is added twice"
                         % (base, ",".join(map(str, clash))))


def target_tris(lod0_tris, ratio):
    return max(MIN_TRIS, int(round(lod0_tris * ratio)))


def boundary_vertices(polys):
    """Vertices on an edge that only one polygon uses."""
    count = {}
    for p in polys:
        n = len(p)
        for k in range(n):
            e = (p[k], p[(k + 1) % n])
            key = (e[0], e[1]) if e[0] < e[1] else (e[1], e[0])
            count[key] = count.get(key, 0) + 1
    out = set()
    for (a, b), c in count.items():
        if c == 1:
            out.add(a)
            out.add(b)
    return out


# --------------------------------------------------------------------------- #
# Blender side
# --------------------------------------------------------------------------- #
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)


def tris_of(mesh):
    return sum(len(p.vertices) - 2 for p in mesh.polygons)


def fingerprint():
    fp = {}
    for o in bpy.data.objects:
        if o.type != "MESH":
            fp[o.name] = {"type": o.type}
            continue
        keys = o.data.shape_keys.key_blocks if o.data.shape_keys else []
        fp[o.name] = {
            "type": "MESH",
            "verts": len(o.data.vertices),
            "polys": len(o.data.polygons),
            "tris": tris_of(o.data),
            "dims": [round(x, 4) for x in o.dimensions],
            "loc": [round(x, 4) for x in o.location],
            "uv_layers": len(o.data.uv_layers),
            "materials": [s.material.name if s.material else None for s in o.material_slots],
            "shape_keys": [k.name for k in keys],
            "parent": o.parent.name if o.parent else None,
        }
    return fp


def world_positions(obj, indices=None):
    mw = obj.matrix_world
    verts = obj.data.vertices
    idx = range(len(verts)) if indices is None else indices
    return [mw @ verts[i].co for i in idx]


def copy_object(src, name):
    if len(name) > BLENDER_NAME_MAX:
        raise SystemExit("%r is %d chars; Blender truncates past %d" % (name, len(name), BLENDER_NAME_MAX))
    obj = src.copy()
    obj.data = src.data.copy()
    obj.name = name
    obj.data.name = name
    for coll in src.users_collection:
        coll.objects.link(obj)
    if obj.data.shape_keys:
        obj.shape_key_clear()
    return obj


def filter_islands(mesh, drop_small, bulky_only):
    """Delete the loose parts `keep_island` rejects. Returns (kept, dropped) island counts."""
    if drop_small is None and bulky_only is None:
        return None
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh)
    seen, islands = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        seen.add(f.index)
        stack, faces = [f], []
        while stack:
            x = stack.pop()
            faces.append(x)
            for v in x.verts:
                for y in v.link_faces:
                    if y.index not in seen:
                        seen.add(y.index)
                        stack.append(y)
        islands.append(faces)

    def extent(cos):
        return [max(c[k] for c in cos) - min(c[k] for c in cos) for k in range(3)]

    mesh_max = max(extent([v.co for v in bm.verts]))
    drop = [fs for fs in islands
            if not keep_island(extent([v.co for f in fs for v in f.verts]), mesh_max, drop_small, bulky_only)]
    if len(drop) == len(islands):
        raise SystemExit("%s: the island filter would leave nothing" % mesh.name)
    bmesh.ops.delete(bm, geom=[f for fs in drop for f in fs], context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    return {"kept": len(islands) - len(drop), "dropped": len(drop)}


def make_lod(src, name, target, lock, drop_small=None, bulky_only=None):
    """Copy `src` as `name`, drop its shape keys and any loose parts the filters reject, then
    collapse-decimate it to `target` triangles. The armature modifier is muted during the
    evaluation so the rest pose is baked."""
    obj = copy_object(src, name)
    islands = filter_islands(obj.data, drop_small, bulky_only)
    have = tris_of(obj.data)
    border = set()
    if target < have:
        muted = [m for m in obj.modifiers if m.show_viewport]
        for m in muted:
            m.show_viewport = False
        dec = obj.modifiers.new("__lod_decimate", "DECIMATE")
        dec.decimate_type = "COLLAPSE"
        dec.ratio = target / have
        if lock:
            border = boundary_vertices([tuple(p.vertices) for p in obj.data.polygons])
            vg = obj.vertex_groups.new(name=LOCK_GROUP)
            vg.add([v.index for v in obj.data.vertices if v.index not in border], 1.0, "REPLACE")
            dec.vertex_group = LOCK_GROUP
            dec.vertex_group_factor = LOCK_FACTOR

        dg = bpy.context.evaluated_depsgraph_get()
        mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True,
                                               depsgraph=dg)
        old = obj.data
        obj.modifiers.remove(dec)
        for m in muted:
            m.show_viewport = True
        obj.data = mesh
        mesh.name = name
        bpy.data.meshes.remove(old)
        if lock:
            obj.vertex_groups.remove(obj.vertex_groups[LOCK_GROUP])
    return obj, border, clean(obj.data), islands


def make_plane(src, name):
    """A single quad, 1/1000 of the mesh across, at the centre of `src`, weighted wholly to the
    bone that carries most of `src`: a LOD that draws nothing visible. KEYforce's rule for the
    warg fur's last LOD, so the fur stops rendering at range instead of holding its last LOD."""
    cos = [v.co for v in src.data.vertices]
    lo = [min(c[k] for c in cos) for k in range(3)]
    hi = [max(c[k] for c in cos) for k in range(3)]
    c = [(lo[k] + hi[k]) / 2 for k in range(3)]
    s = 0.0005 * max(hi[k] - lo[k] for k in range(3))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(c[0] - s, c[1] - s, c[2]), (c[0] + s, c[1] - s, c[2]),
                      (c[0] + s, c[1] + s, c[2]), (c[0] - s, c[1] + s, c[2])], [], [(0, 1, 2, 3)])
    mesh.uv_layers.new(name=src.data.uv_layers[0].name if src.data.uv_layers else "UVMap")
    if src.data.materials:
        mesh.materials.append(src.data.materials[0])
    totals = {}
    for v in src.data.vertices:
        for g in v.groups:
            totals[g.group] = totals.get(g.group, 0.0) + g.weight
    obj = src.copy()
    obj.name = name
    for coll in src.users_collection:
        coll.objects.link(obj)
    obj.data = mesh
    if totals:
        bone = src.vertex_groups[max(totals, key=totals.get)].name
        obj.vertex_groups.new(name=bone).add([0, 1, 2, 3], 1.0, "REPLACE")
    return obj


def clean(mesh):
    """Drop what the collapse leaves behind: degenerate faces (the FBX importer's own validate
    drops them on the way back in, so the built and re-imported counts would disagree) and the
    loose edges and vertices of card islands collapsed away (on hair A's LOD5, 3,376 vertices for
    548 triangles before this)."""
    import bmesh
    mesh.validate(clean_customdata=False)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    edges = [e for e in bm.edges if not e.link_faces]
    if edges:
        bmesh.ops.delete(bm, geom=edges, context="EDGES_FACES")
    verts = [v for v in bm.verts if not v.link_faces]
    if verts:
        bmesh.ops.delete(bm, geom=verts, context="VERTS")
    bm.to_mesh(mesh)
    bm.free()
    return {"loose_edges": len(edges), "loose_verts": len(verts)}


def lost_seam_vertices(src, border, lod):
    """Source boundary vertices with no LOD vertex within a hair of them."""
    if not border:
        return 0
    from mathutils.kdtree import KDTree
    pts = world_positions(lod)
    tree = KDTree(len(pts))
    for i, p in enumerate(pts):
        tree.insert(p, i)
    tree.balance()
    tol = 1e-5 * max(max(src.dimensions), 1e-6)
    return sum(1 for p in world_positions(src, sorted(border)) if tree.find(p)[2] > tol)


def weightless_vertices(obj):
    names = {g.index for g in obj.vertex_groups if g.name != LOCK_GROUP}
    if not names:
        return 0
    return sum(1 for v in obj.data.vertices
               if not any(g.group in names and g.weight > 0 for g in v.groups))


def export(path):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types=EXPORT_TYPES,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        axis_forward="-Y", axis_up="Z", bake_anim=False,
        apply_scale_options="FBX_SCALE_NONE", global_scale=1.0,
        use_mesh_modifiers=False, path_mode="AUTO")


def compare(before, after, built, deleted=(), renamed=None, remapped=None, rekeyed=None):
    """Every object this run did not rebuild, rename or delete must come back as it was; every
    rebuilt or new one as built; every renamed one as it was under its old name; every deleted
    one not at all (unless a rename took its name). `rekeyed` maps an object that only gained
    shape keys to its expected full key list: its geometry must come back as it was and its
    channels exactly as expected after the reference key, which the FBX importer names itself
    (add_face_morph_channels.py, transfer_hand_morphs.py)."""
    renamed = renamed or {}
    remapped = remapped or {}
    rekeyed = rekeyed or {}
    diffs = []
    lost = sorted(set(before) - set(after) - set(deleted))
    if lost:
        diffs.append("objects lost in the round trip: %s" % ", ".join(lost))
    back = sorted((set(deleted) & set(after)) - set(built) - set(renamed))
    if back:
        diffs.append("deleted objects still exported: %s" % ", ".join(back))
    extra = sorted(set(after) - set(before) - set(built) - set(renamed))
    if extra:
        diffs.append("unexpected new objects: %s" % ", ".join(extra))
    for name, b in list(before.items()) + [(n, s) for n, s in renamed.items()]:
        a = after.get(name)
        if a is None or b["type"] != "MESH" or name in built:
            continue
        if name in renamed and b is not renamed[name]:
            continue     # the object that held this name before was deleted; check the renamed one
        for key in ("verts", "polys", "dims", "loc", "uv_layers", "materials", "shape_keys",
                    "parent"):
            if key in ("dims", "loc"):
                if not same_vector(b[key], a[key]):
                    diffs.append("%s: %s %r -> %r" % (name, key, b[key], a[key]))
            elif key == "shape_keys" and name in rekeyed:
                want = list(rekeyed[name])
                if list(a[key][1:]) != want[1:]:
                    diffs.append("%s: channels expected %d, re-imported %d (first %s)"
                                 % (name, max(len(want) - 1, 0), max(len(a[key]) - 1, 0), a[key][1:3]))
            elif key == "materials" and name in remapped:
                # the export folds two slots naming one material, so compare the names as sets
                if set(a[key]) != set(remapped[name]):
                    diffs.append("%s: materials set to %r, re-imported %r" % (name, remapped[name], a[key]))
            elif b[key] != a[key]:
                diffs.append("%s: %s %r -> %r" % (name, key, b[key], a[key]))
    for name, want in built.items():
        got = after.get(name)
        if got is None:
            diffs.append("%s is not in the exported file" % name)
            continue
        for key in ("verts", "polys", "materials", "parent"):
            if got[key] != want[key]:
                diffs.append("%s: %s built %r, re-imported %r" % (name, key, want[key], got[key]))
        if got["shape_keys"]:
            diffs.append("%s: arrived with shape keys %s" % (name, got["shape_keys"][:3]))
    return diffs


def main():
    fbx = fbx_from_argv(list(sys.argv))
    report_path = fbx + ".lods-report.json"
    report = {"fbx": fbx, "stage": "start", "applied": False, "ok": False, "meshes": {},
              "diffs": [], "gates": []}

    def save(stage):
        # A native crash leaves no traceback and the launcher shows no stdout, so the report is
        # rewritten at each step and the last `stage` says how far it got. Poll for "done".
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        args = parse_args(list(sys.argv))
        staged = os.path.splitext(fbx)[0] + ".lods.fbx"
        report.update(staged=staged, lock_boundary=args["lock_boundary"])

        save("loading")
        load(fbx)
        save("loaded")
        before = fingerprint()
        built, deleted, renamed, remapped = {}, [], {}, {}
        if args["plan"]:
            drop, renames, materials = load_plan_fixes(args["plan"])
            for name in drop:
                if name not in bpy.data.objects:
                    raise SystemExit("no object %r to delete" % name)
                bpy.data.objects.remove(bpy.data.objects[name])
                deleted.append(name)
            for old, new in renames.items():
                if old not in bpy.data.objects or new in bpy.data.objects:
                    raise SystemExit("cannot rename %r to %r" % (old, new))
                obj = bpy.data.objects[old]
                obj.name = new
                if obj.type == "MESH":
                    obj.data.name = new
                deleted.append(old)
                renamed[new] = before[old]
            for name, slots in materials.items():
                obj = bpy.data.objects.get(name)
                if obj is None or obj.type != "MESH" or len(obj.material_slots) != len(slots):
                    raise SystemExit("cannot set %d materials on %r" % (len(slots), name))
                for slot, target in zip(obj.material_slots, slots):
                    # the exact Kit name: an existing datablock is reused, a new one keeps the name
                    slot.material = bpy.data.materials.get(target) or bpy.data.materials.new(target)
                remapped[name] = slots
            report["fixes"] = {"delete": drop, "renames": renames, "materials": len(materials)}
        names = [o.name for o in bpy.data.objects]
        by_ratio = args["ratios"] is not None
        if args["plan"]:
            jobs = load_plan(args["plan"])
        else:
            jobs = [(base, plan_levels(args["levels"], args["ratios"] if by_ratio else args["tris"], cap),
                     args["lock_boundary"]) for base, cap in args["meshes"]]
        for base, plan, lock in jobs:
            scheme = detect_scheme(base, names)
            have = existing_levels(base, names, scheme)
            if not args["replace"]:
                check_new_levels(base, have, plan)
            replaced = {lv for lv, _ in plan if lv in have}
            lod0 = bpy.data.objects[lod_name(base, 0, scheme)]
            if 0 in replaced and lod0.data.shape_keys:
                raise SystemExit("%s LOD0 carries morphs; it is never rebuilt" % lod0.name)
            tris_by_level = {lv: tris_of(bpy.data.objects[lod_name(base, lv, scheme)].data)
                             for lv in have}
            # LOD0 as it was, whatever this run does to it: the fallback source for every level
            pristine = copy_object(lod0, "__pristine_lod0")
            entry = {"scheme": scheme, "had": tris_by_level, "levels": {}}
            for level, t in plan:
                want = resolve_target(tris_by_level[0], ratio=t) if by_ratio else resolve_target(0, tris=t)
                name = lod_name(base, level, scheme)
                save("building " + name)
                border, removed, islands, src = set(), None, None, pristine
                if want == PLANE:
                    obj = make_plane(pristine, name + "__new")
                else:
                    lv = pick_source(tris_by_level, replaced | set(args["delete_levels"]), want)
                    src = pristine if lv == 0 else bpy.data.objects[lod_name(base, lv, scheme)]
                    obj, border, removed, islands = make_lod(
                        src, name + "__new", want, lock,
                        args["drop_small"].get(level), args["bulky_only"].get(level))
                lost = lost_seam_vertices(src, border, obj)
                bare = weightless_vertices(obj)
                # a source that already carries unweighted vertices hands some on (cts_rohan_cape3);
                # only a LOD with more of them than its source is a decimation fault
                src_bare = 0 if want == PLANE else weightless_vertices(src)
                if level == 0:
                    old = lod0.data
                    lod0.data = obj.data
                    bpy.data.objects.remove(obj)
                    bpy.data.meshes.remove(old)
                    obj = lod0
                else:
                    if name in bpy.data.objects:
                        bpy.data.objects.remove(bpy.data.objects[name])
                    obj.name = name
                obj.data.name = name
                entry["levels"][level] = {
                    "object": name, "rebuilt": level in replaced,
                    "source": None if want == PLANE else (lod_name(base, lv, scheme)),
                    "target_tris": want, "tris": tris_of(obj.data), "verts": len(obj.data.vertices),
                    "seam_vertices": len(border), "seam_lost": lost, "weightless_vertices": bare,
                    "cleaned": removed, "islands": islands}
                if lost:
                    report["gates"].append("%s: %d of %d seam vertices lost" % (name, lost, len(border)))
                if bare > src_bare:
                    report["gates"].append("%s: %d vertices with no weight (source %s has %d)"
                                           % (name, bare, src.name, src_bare))
                built[name] = {"verts": len(obj.data.vertices), "polys": len(obj.data.polygons),
                               "materials": [s.material.name if s.material else None
                                             for s in obj.material_slots],
                               "parent": obj.parent.name if obj.parent else None}
            for level in args["delete_levels"]:
                name = lod_name(base, level, scheme)
                if name not in bpy.data.objects:
                    raise SystemExit("%s has no LOD%d to delete" % (base, level))
                bpy.data.objects.remove(bpy.data.objects[name])
                deleted.append(name)
            mesh = pristine.data
            bpy.data.objects.remove(pristine)
            bpy.data.meshes.remove(mesh)
            report["meshes"][base] = entry

        save("exporting")
        export(staged)
        save("re-importing")
        load(staged)
        report["diffs"] = compare(before, fingerprint(), built, deleted, renamed, remapped)
        report["deleted"] = deleted
        report["ok"] = not report["diffs"] and not report["gates"]

        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-lods"
            if not os.path.exists(backup):
                shutil.copy2(fbx, backup)
            report["backup"] = backup
            os.replace(staged, fbx)
            report["applied"] = True
            report["staged"] = None
    except SystemExit as exc:
        report["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001 - a Blender-side failure must still reach the caller
        import traceback
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["traceback"] = traceback.format_exc()

    save("done")


if __name__ == "__main__":
    main()
