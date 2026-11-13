"""Pre-export check of a mesh before it goes to the Modding Kit: one report, one verdict.

Headless, on an FBX or a .blend (the Store launcher detaches: wait for <out>/_report/DONE.txt):

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/mesh_preflight.py --
        (--fbx <in.fbx> | --blend <in.blend>) --out <dir> [--profile mesh|prop|icon]
        [--budget-tris N] [--interior]

In a live Blender session (over the Blender MCP), read-only on the scene:

    import sys, importlib
    sys.path.insert(0, r"E:\\repos\\TAOM\\tools\\blender")
    import mesh_preflight
    importlib.reload(mesh_preflight)
    report = mesh_preflight.preflight_scene(names=["sm_gd_crate_001"], profile="prop")

The report holds "verdict" (FAIL, PASS WITH NOTES or PASS), "counts" and "findings" (worst first),
then one record per mesh. A headless run with a blocker ends as status "error", so a caller that
reads only the status still sees the failure.

A check is a blocker only where TAOM has a recorded failure or gate for it, and a major where a
recorded trap applies; the reason for each is in docs/features/blender-asset-gates.md. Plain
topology counts are notes. Profiles:
  mesh  the checks every mesh gets (the default);
  prop  also: every visual mesh has its bo_ collision twin (one twin serves the sub-meshes and
        LODs of its prefix; clo_ cloth needs none);
  icon  also: no bo_ at all (a campaign map icon is visual only).
--budget-tris is the LOD0 triangle total of the visual meshes; take N from the asset this one
replaces (tools/blender/probe_fbx.py). --interior is for a shell that is seen from inside (a hall,
a cave): its faces point inward by design, so the inverted-face check becomes a note.

Not checked here: LOD chains (python tools/audit_fbx_lods.py --fbx <x>), UV fold-over and texel
density (the probes of tools/oneoff/blender_prep_tripo_prop.py), the names other files refer to
(python tools/validate_mesh_refs.py --check-name <mesh>).

Every visible mesh is read as the export will write it: modifiers applied, in world space, and a
mirrored object's faces reversed. A mesh that is hidden or in an excluded collection is not read;
the report lists it in "skipped_hidden". The Kit's own frame (rotation X 90 and scale 0.01 on an FBX read back in
Blender) is uniform, so it is accepted.

Inverted faces: the rule of tools/blender/unwrap_texture_sets.py ("never seen along its normal,
seen along the reversed one"), with rays over the whole sphere and no ground plane, so it also
fits a mesh that does not stand on z = 0. Each mesh is tested alone: its LODs share its space.
Geometry cannot tell a hole from a face with another face right behind it (the approved
erebor_dwarf_house_01 measures 0.99%: thin trim slabs wound inward), so a share between 0.1% and
5% is a major finding that sends the reader to tools/blender/qa_views.py, and only a larger share
fails the run.
"""
import argparse
import math
import os
import re
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "oneoff"))
from blender_job import flag_value, import_fbx, job, report_path, script_args  # noqa: E402
from kitbash_kits import split_name, strip_dup  # noqa: E402  (pure Python: base | lod | bo)

PROFILES = ("mesh", "prop", "icon")
SEVERITIES = ("blocker", "major", "minor", "note")
INVERTED_LIMIT = 0.001  # share of the surface; the limit of unwrap_texture_sets.py: above it, look
INVERTED_BLOCK = 0.05  # above it the run fails: whole boxes or the whole mesh are wound inward
COLLAPSED_UV_LIMIT = 0.01  # share of the surface whose UVs have no area
MAX_INFLUENCES = 4  # the Kit keeps the 4 strongest weights of a vertex and drops the rest
MAX_BONES = 64  # Skeleton.MaxBoneCount
SCALE_TOLERANCE = 1.001
MAX_RAY_FACES = 200000  # above this the ray test reads an even sample of the faces
EPS_AREA = 1e-12

_DUP_RE = re.compile(r"\.\d{3}$")  # Blender's duplicate suffix
_CLOTH_RE = re.compile(r"(^clo_|_clo$|_clo_)", re.IGNORECASE)  # as tools/audit_fbx_lods.py


# ----------------------------------------------------------------- pure rules

def classify_name(name):
    """kind (base | lod | bo), base (without bo_ or the LOD suffix), metamesh (the base up to its
    first dot: a sub-mesh `<mesh>.<part>` belongs to <mesh>), and the name defects. Blender's .NNN
    suffix is set aside first, so `x.lod1.001` is still a LOD and `bo_x.001` still the body of x;
    LOD0 written out (`x_lod0`, `x.lod0`) is the base, as armory-guide.md "Naming" allows."""
    kind, base, lod = split_name(strip_dup(name))
    if kind == "lod" and lod == "0":
        kind = "base"
    return {"kind": kind, "base": base, "metamesh": base.split(".")[0],
            "cloth": bool(_CLOTH_RE.search(base)), "dup": bool(_DUP_RE.search(name)),
            "upper": any(c.isupper() for c in name)}


def _polygon_area(verts, face):
    """Area of a polygon by Newell's method."""
    nx = ny = nz = 0.0
    for i, a in enumerate(face):
        ax, ay, az = verts[a]
        bx, by, bz = verts[face[(i + 1) % len(face)]]
        nx += (ay - by) * (az + bz)
        ny += (az - bz) * (ax + bx)
        nz += (ax - bx) * (ay + by)
    return math.sqrt(nx * nx + ny * ny + nz * nz) / 2.0


def topology_stats(verts, faces):
    """Counts over plain lists: verts are (x, y, z), faces are lists of vertex indices."""
    edge_faces, used = {}, set()
    tris = ngons = zero = 0
    for face in faces:
        n = len(face)
        tris += max(0, n - 2)
        ngons += n > 4
        zero += _polygon_area(verts, face) < EPS_AREA
        used.update(face)
        for i, a in enumerate(face):
            b = face[(i + 1) % n]
            key = (a, b) if a < b else (b, a)
            edge_faces[key] = edge_faces.get(key, 0) + 1
    boundary = sum(1 for c in edge_faces.values() if c == 1)
    non_manifold = sum(1 for c in edge_faces.values() if c > 2)
    return {"faces": len(faces), "tris": tris, "ngons": ngons, "zero_area_faces": zero,
            "boundary_edges": boundary, "non_manifold_edges": non_manifold,
            "loose_verts": len(verts) - len(used),
            "closed": bool(faces) and boundary == 0 and non_manifold == 0}


def sphere_dirs(n=48):
    """n unit directions spread evenly over the whole sphere, on a golden-angle spiral."""
    golden = math.pi * (3 - math.sqrt(5))
    out = []
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(max(0.0, 1 - z * z))
        out.append((r * math.cos(golden * i), r * math.sin(golden * i), z))
    return out


def sample_step(face_count):
    """Read every `step`-th face in the ray test, so at most MAX_RAY_FACES are read."""
    return max(1, math.ceil(face_count / MAX_RAY_FACES))


def skin_counts(vertex_weights, bone_names):
    """(vertices with no bone weight, vertices with more than MAX_INFLUENCES bone weights).
    vertex_weights: per vertex, its (vertex group name, weight) pairs. Only a group named after a
    bone is a skin weight: the FBX export writes no cluster for a mask or pin group."""
    unweighted = over = 0
    for pairs in vertex_weights:
        n = sum(1 for group, weight in pairs if weight > 1e-6 and group in bone_names)
        unweighted += n == 0
        over += n > MAX_INFLUENCES
    return unweighted, over


def is_inverted(front, back):
    """front and back: the share of a face's sample points from which a ray escapes the mesh
    along the normal and along the reversed normal."""
    return front == 0.0 and back >= 0.5


def collapsed_uv_share(pairs):
    """pairs: (UV area, surface area) per face. The share of the surface whose UVs have no area."""
    total = sum(area for _uv, area in pairs if area > EPS_AREA)
    if total <= 0.0:
        return 0.0
    return sum(area for uv, area in pairs if area > EPS_AREA and abs(uv) < EPS_AREA) / total


def _finding(severity, code, mesh, message):
    return {"severity": severity, "code": code, "mesh": mesh, "message": message}


def check_mesh(m, interior=False):
    """The findings of one mesh record (extract_mesh writes the record)."""
    name, c, out = m["name"], classify_name(m["name"]), []

    def add(severity, code, message):
        out.append(_finding(severity, code, name, message))

    if c["dup"]:
        add("major", "name-duplicate-suffix",
            "the name ends in Blender's .NNN: the Kit reads it as a sub-mesh named NNN")
    if c["upper"]:
        add("note", "name-upper-case", "the Kit lowercases mesh names: every XML reference must be lower case")

    for slot in m["slots"]:
        if slot["faces"] and not slot["name"]:
            add("major", "material-empty-slot", f"{slot['faces']} faces use a slot with no material")
        elif _DUP_RE.search(slot["name"]):
            add("blocker", "material-duplicate-suffix",
                f"material {slot['name']!r}: the Kit binds by exact name and has no material with a .NNN suffix")
        elif not slot["faces"]:
            add("minor", "material-unused-slot",
                f"no face uses material {slot['name']!r}: it reaches the Kit as an empty sub-mesh")

    if m["mirrored"]:
        add("major", "transform-mirrored",
            "the object is mirrored (negative scale): apply the transform and flip the normals before export")
    if m["data_users"] > 1 and not m["identity"]:
        add("major", "transform-multi-user",
            f"{m['data_users']} objects share this mesh data: transform_apply skips it silently")
    scale = [abs(s) for s in m["scale"]]
    if min(scale) > 0 and max(scale) / min(scale) > SCALE_TOLERANCE:
        add("minor", "transform-non-uniform-scale", f"scale {[round(s, 4) for s in m['scale']]} is not uniform")

    skin = m["skin"]
    if skin:
        if skin["unweighted"]:
            add("blocker", "skin-unweighted", f"{skin['unweighted']} vertices carry no bone weight")
        if skin["bones"] > MAX_BONES:
            add("blocker", "skin-too-many-bones", f"{skin['bones']} bones: the engine's limit is {MAX_BONES}")
        if skin["over_limit"]:
            add("major", "skin-over-four-influences",
                f"{skin['over_limit']} vertices have more than {MAX_INFLUENCES} weights: the Kit drops the rest")

    if c["kind"] == "bo":
        if not any(slot["name"] for slot in m["slots"]):
            add("major", "body-no-physics-material",
                "a collision body carries its physics material as a material slot (stone, wood)")
        return out

    if not m["slots"]:
        add("major", "material-empty-slot", "the mesh has no material slot")
    if not m["uv_layers"]:
        add("blocker", "uv-missing", "the mesh has no UV layer")
    elif len(m["uv_layers"]) > 1:
        add("note", "uv-extra-layers", f"UV layers {m['uv_layers']}: check which one the material reads")
    if (m["collapsed_uv_share"] or 0.0) > COLLAPSED_UV_LIMIT:
        add("major", "uv-collapsed", f"{m['collapsed_uv_share']:.1%} of the surface has UVs with no area")

    if m["inverted_faces"]:
        share = m["inverted_share"] or 0.0
        text = f"{m['inverted_faces']} faces ({share:.2%} of the surface) are seen only from behind"
        if interior:
            add("note", "faces-inverted-interior", text + " (expected for a shell seen from inside)")
        elif share > INVERTED_BLOCK:
            add("blocker", "faces-inverted", text + ": the engine culls back faces, so they are holes in game")
        elif share > INVERTED_LIMIT:
            add("major", "faces-inverted-suspect",
                text + ": a hole in game unless another face sits right behind; look at the culling-on views")
        else:
            add("note", "faces-inverted-few", text + ": usually crevices between overlapping shells")

    topo = m["topo"]
    for key, code, what in (("non_manifold_edges", "topo-non-manifold", "edges shared by more than two faces"),
                            ("loose_verts", "topo-loose-verts", "vertices used by no face"),
                            ("zero_area_faces", "topo-zero-area", "faces with no area"),
                            ("ngons", "topo-ngons", "faces with more than four corners (the export triangulates)")):
        if topo.get(key):
            add("note", code, f"{topo[key]} {what}")
    return out


def check_set(meshes, profile="mesh", budget_tris=None, interior=False):
    """Every mesh's findings plus the rules that need the whole file."""
    if not meshes:
        return [_finding("blocker", "no-meshes", "", "the file holds no mesh object")]
    out = []
    for m in meshes:
        out += check_mesh(m, interior)
    named = [(m, classify_name(m["name"])) for m in meshes]
    bodies = {key.lower() for _m, c in named if c["kind"] == "bo" for key in (c["base"], c["metamesh"])}
    visual_lod0 = [(m, c) for m, c in named if c["kind"] == "base"]

    if profile == "prop":
        missing = set()
        for _m, c in visual_lod0:
            if c["cloth"] or c["base"].lower() in bodies or c["metamesh"].lower() in bodies:
                continue
            missing.add(c["metamesh"])
        for metamesh in sorted(missing):
            out.append(_finding("blocker", "body-missing", metamesh,
                                f"no bo_{metamesh.lower()}: a missing collision body hangs the mission load"))
    elif profile == "icon":
        for m, c in named:
            if c["kind"] == "bo":
                out.append(_finding("major", "icon-has-body", m["name"],
                                    "a map icon is visual only: the settlement entity has its own body"))

    if budget_tris is not None:
        total = sum(m["tris"] for m, _c in visual_lod0)
        if total > budget_tris:
            out.append(_finding("major", "over-budget", "",
                                f"{total} LOD0 triangles, over the budget of {budget_tris}"))
    return out


def verdict(findings):
    worst = {f["severity"] for f in findings}
    if "blocker" in worst:
        return "FAIL"
    return "PASS WITH NOTES" if worst & {"major", "minor"} else "PASS"


def build_report(meshes, profile="mesh", budget_tris=None, interior=False):
    findings = check_set(meshes, profile, budget_tris, interior)
    findings.sort(key=lambda f: (SEVERITIES.index(f["severity"]), f["mesh"], f["code"]))
    return {"profile": profile, "verdict": verdict(findings),
            "counts": {s: sum(1 for f in findings if f["severity"] == s) for s in SEVERITIES},
            "findings": findings, "meshes": meshes}


def parse_args(argv):
    p = argparse.ArgumentParser(allow_abbrev=False)
    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument("--fbx")
    src.add_argument("--blend")
    p.add_argument("--out", required=True)
    p.add_argument("--profile", choices=PROFILES, default="mesh")
    p.add_argument("--budget-tris", type=int)
    p.add_argument("--interior", action="store_true", help="a shell seen from inside: inverted faces are a note")
    return p.parse_args(argv)


# --------------------------------------------------------------- Blender side

def _escape_share(bvh, points, normal, dirs, eps, far):
    """Share of `points` from which at least one ray on the `normal` side leaves the mesh."""
    valid = [d for d in dirs if d.dot(normal) > 0.05]
    seen = 0
    for point in points:
        origin = point + normal * eps
        for d in valid:
            if bvh.ray_cast(origin, d, far)[0] is None:
                seen += 1
                break
    return seen / len(points)


def _inverted(bm):
    """(face count, surface share) of the faces that look inverted, and whether a sample was read."""
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree

    if not bm.faces:
        return 0, 0.0, False
    bvh = BVHTree.FromBMesh(bm)
    dirs = [Vector(d) for d in sphere_dirs()]
    used = [v.co for v in bm.verts if v.link_faces]  # a loose vertex far away must not stretch the offsets
    span = max(max(co[k] for co in used) - min(co[k] for co in used) for k in range(3)) or 1.0
    eps, far = span * 1e-4, span * 10.0
    step = sample_step(len(bm.faces))
    count, inv_area, area = 0, 0.0, 0.0
    for i, face in enumerate(bm.faces):  # a bmesh sequence takes no stepped slice
        a = face.calc_area()
        if i % step or a < EPS_AREA:
            continue
        area += a
        centre = face.calc_center_median()
        corners = [v.co for v in face.verts]
        points = [centre] + [centre.lerp(co, 0.6) for co in corners[::max(1, len(corners) // 3)][:3]]
        front = _escape_share(bvh, points, face.normal, dirs, eps, far)
        if front == 0.0 and is_inverted(front, _escape_share(bvh, points, -face.normal, dirs, eps, far)):
            count += 1
            inv_area += a
    return count * step, (inv_area / area if area else 0.0), step > 1


def extract_mesh(obj, depsgraph):
    """The record of one mesh object, read as the export will write it. Changes nothing."""
    import bmesh

    world = obj.matrix_world
    mirrored = world.determinant() < 0
    bm = bmesh.new()
    try:
        bm.from_object(obj, depsgraph)
        bm.transform(world)
        if mirrored:
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.normal_update()
        bm.verts.index_update()
        topo = topology_stats([tuple(v.co) for v in bm.verts], [[v.index for v in f.verts] for f in bm.faces])
        uv = bm.loops.layers.uv.active
        uv_pairs = []
        used = {}
        for face in bm.faces:
            used[face.material_index] = used.get(face.material_index, 0) + 1
            if uv is not None:
                loops, signed = face.loops, 0.0
                for i in range(1, len(loops) - 1):
                    a, b, c = loops[0][uv].uv, loops[i][uv].uv, loops[i + 1][uv].uv
                    signed += ((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) / 2.0
                uv_pairs.append((signed, face.calc_area()))
        kind = classify_name(obj.name)["kind"]
        inv_count, inv_share, sampled = (0, 0.0, False) if kind == "bo" else _inverted(bm)
    finally:
        bm.free()

    slots = [{"name": s.material.name if s.material else "", "faces": used.get(i, 0)}
             for i, s in enumerate(obj.material_slots)]
    skin = None
    armature = obj.find_armature()
    if armature is not None:
        groups = {g.index: g.name for g in obj.vertex_groups}
        unweighted, over = skin_counts(([(groups[g.group], g.weight) for g in v.groups] for v in obj.data.vertices),
                                       {b.name for b in armature.data.bones})
        skin = {"unweighted": unweighted, "over_limit": over, "bones": len(armature.data.bones),
                "armature": armature.name}
    identity = all(abs(world[r][c] - (1.0 if r == c else 0.0)) < 1e-6 for r in range(4) for c in range(4))
    return {"name": obj.name, "data_users": obj.data.users, "identity": identity,
            "scale": [round(s, 5) for s in world.to_scale()], "mirrored": mirrored, "tris": topo["tris"],
            "uv_layers": [layer.name for layer in obj.data.uv_layers],
            "collapsed_uv_share": round(collapsed_uv_share(uv_pairs), 5) if uv is not None else None,
            "slots": slots, "inverted_faces": inv_count, "inverted_share": round(inv_share, 5),
            "inverted_sampled": sampled, "topo": topo, "skin": skin}


def visible_meshes():
    """(the mesh objects visible in the view layer, the names of the scene's other meshes). A mesh
    in an excluded collection or hidden in the viewport has no evaluated mesh to read."""
    import bpy

    shown = [o for o in bpy.context.view_layer.objects if o.type == "MESH" and o.visible_get()]
    names = {o.name for o in shown}
    return shown, sorted(o.name for o in bpy.context.scene.objects if o.type == "MESH" and o.name not in names)


def preflight_objects(objs, profile="mesh", budget_tris=None, interior=False):
    import bpy

    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    return build_report([extract_mesh(o, depsgraph) for o in objs], profile, budget_tris, interior)


def preflight_scene(names=None, profile="mesh", budget_tris=None, interior=False):
    """Live-session entry. names: the mesh objects to check, or None for every visible mesh."""
    import bpy

    if names is None:
        objs = visible_meshes()[0]
    else:
        missing = [n for n in names if n not in bpy.data.objects or bpy.data.objects[n].type != "MESH"]
        if missing:
            raise ValueError(f"not a mesh object in this file: {missing}")
        objs = [bpy.data.objects[n] for n in names]
    return preflight_objects(objs, profile, budget_tris, interior)


def main():
    args = script_args()
    out = flag_value(args, "--out") or "."  # before argparse, so a bad argument still gets a fresh report
    with job(report_path(out)) as report:
        a = parse_args(args)
        import bpy

        if a.blend:
            bpy.ops.wm.open_mainfile(filepath=a.blend)
        else:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            import_fbx(a.fbx)
        report["src"] = a.blend or a.fbx
        objs, report["skipped_hidden"] = visible_meshes()
        report.update(preflight_objects(objs, a.profile, a.budget_tris, a.interior))
        if report["verdict"] == "FAIL":
            blockers = sorted({f["code"] for f in report["findings"] if f["severity"] == "blocker"})
            raise RuntimeError("preflight FAIL: " + ", ".join(blockers))


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
