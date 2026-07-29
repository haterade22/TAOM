"""
Build the Lond Cirion hybrid gatehouse: the minas_tirith_gatehouse_a1 body
with its four towers cut out — the two big rear octagons replaced by the
wall kit's corner tower (gondor_castle_wall_tower_l3_b, the tower "we've
been using on the walls"), the two small front roofed towers removed
full-height and the wing wall cloned across the gap (the clone brings its
own crenellated top, so the run stays merloned).

Measured facts driving the cuts (gatehouse_probe.json + wing_probe/bins.txt,
2026-07-29):
    ground z=0 (bbox sinks to -15), wings + bridge deck at z=15 == the wall
    kit deck; big towers occupy x sign*(6.95..22.85), y -8..+8 beside the
    central gate span (|x| < 6.95); the front roofed towers are octagons
    x sign*(14.4..21.6), y -24.0..-14.0 standing ON the wing wall (parapet
    x sign*(18.2..19.5), top z 17.5); the wing-end turret is y -29..-26.5 —
    the only clean donor band is NORTH of the tower, y -13.5..-9.5. New rear
    towers sit at x +-15.1 so their inner faces (half-size 7.1 -> 8.0) meet
    the cut edge (7.9); +5 z raise puts their doors (authored z10) on the
    z15 decks; west tower unrotated (doors east to the bridge + south to
    the wing), east Rz(-90) (doors west + south). INTERIOR_ROT 180 per the
    kit convention.

Output: blockout/lond_cirion_gatehouse_a.fbx (lond_cirion_gatehouse_01 +
.lod3/.lod6/bo_) + renders in E:\\LOTRAOMAssets\\_export\\lond_cirion\\gatehouse\\

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_hybrid_gatehouse.py
"""

import json
import math
import os
import re
import traceback

import bpy
import bmesh
from mathutils import Matrix, Vector

GONDOR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\Gondor"
SRC_GATE = os.path.join(GONDOR, "blockout", "minas_tirith_gatehouse_a1.fbx")
SRC_TWR = os.path.join(GONDOR, "walls", "gondor_castle_wall_tower_l3_b.fbx")
SRC_WALL = os.path.join(GONDOR, "walls", "gondor_castle_wall_20m_l3_a.fbx")
OUT_FBX = os.path.join(GONDOR, "blockout", "lond_cirion_gatehouse_a.fbx")
STAGING = r"E:\LOTRAOMAssets\_export\lond_cirion\gatehouse"
SECTION = "lond_cirion_gatehouse_01"

TWR_B = "gondor_castle_wall_tower_l3_b"
WALL = "gondor_castle_wall_20m_l3_a"

# cut regions: (xmin, xmax, ymin, ymax, zmin) — mirrored for both sides.
# Occupancy-mapped 2026-07-29 (gatehouse_map.txt) + 0.5 m y-bin probe
# (wing_probe/bins.txt): the big octagons stand at the BACK, centred
# (+-16, -1), beside the central span (edge x +-8); the front roofed towers
# are full-height octagons y -24.0..-14.0 straddling the wing wall — cut
# whole, +-0.05 tuck into the clean wall at each end.
BIG_CUT = (7.9, 24.2, -8.2, 8.2, -99.0)
FRONT_CUT = (13.9, 22.5, -24.05, -13.95, -99.0)

TWR_X = 15.1          # rear tower centres: inner face 8.0 meets the cut edge
TWR_Y = -1.0
TWR_Z = 5.0
INT_ROT = 180.0

# the removed front towers leave a 10.1 m gap in the wing run — refill by
# cloning the clean wall band NORTH of the cut (probed: pure parapet + wall,
# x 18.2..19.5, top z 17.5; the band south of the cut belongs to the
# wing-end turret and must NOT be cloned) southward in tile-sized cells,
# bisecting every copy to its exact cell so the wall's big quads can't
# overlap neighbours or the intact run. The clone carries its own crenels.
DONOR = (13.9, 22.5, -13.5, -9.5)    # clean wing band north of the cut

GATE_TIERS = {"base": "minas_tirith_gatehouse_a1",
              "lod3": "minas_tirith_gatehouse_a1.lod3",
              "lod6": "minas_tirith_gatehouse_a1.lod6",
              "bo": "bo_minas_tirith_gatehouse_a1"}
TWR_PARTS = {
    "visual": [TWR_B, f"{TWR_B}_int.floor", f"{TWR_B}_int.stairs"]
              + [f"{TWR_B}_m{i}" for i in range(1, 13)],
    "bo": [f"bo_{TWR_B}", f"bo_{TWR_B}_int"]
          + [f"bo_{TWR_B}_m{i}" for i in range(1, 13)],
}
TIER_SUFFIX = {"base": "", "lod3": ".lod3", "lod6": ".lod6"}

LOG_LINES = []
REPORT_DIR = None


def log(msg):
    LOG_LINES.append(str(msg))
    print(msg)
    if REPORT_DIR:
        with open(os.path.join(REPORT_DIR, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG_LINES))


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def in_region(p, region, side):
    xmin, xmax, ymin, ymax, zmin = region
    x = p.x * side
    return xmin <= x <= xmax and ymin <= p.y <= ymax and p.z >= zmin


def with_boundary(faces):
    """Faces + their verts/edges, the geom set bisect_plane expects."""
    verts = {v for f in faces for v in f.verts}
    edges = {e for f in faces for e in f.edges}
    return list(verts) + list(edges) + list(faces)


def cut_towers(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    # split faces crossing the wing gap's y-planes AND the donor band's
    # edges first: the wall's lower body is big quads, and face-center tests
    # alone would leave slabs intruding into the gap (or over-delete past
    # it) and drop band content whose parent quad is centred outside it
    for yplane in (FRONT_CUT[2], FRONT_CUT[3], DONOR[2], DONOR[3]):
        for side in (1, -1):
            band = [f for f in bm.faces
                    if FRONT_CUT[0] <= f.calc_center_median().x * side <= FRONT_CUT[1]
                    and abs(f.calc_center_median().y - yplane) < 6.0]
            if band:
                bmesh.ops.bisect_plane(
                    bm, geom=with_boundary(band), plane_co=(0.0, yplane, 0.0),
                    plane_no=(0.0, 1.0, 0.0), clear_outer=False,
                    clear_inner=False, dist=1e-4)
    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        for side in (1, -1):
            if in_region(c, BIG_CUT, side) or in_region(c, FRONT_CUT, side):
                doomed.append(f)
                break
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    # refill the wing gap: clone the clean donor band southward cell by cell
    xmin, xmax, ymin, ymax = DONOR
    tile = ymax - ymin
    gap_s, gap_n = FRONT_CUT[2], FRONT_CUT[3]
    ncells = int(math.ceil((gap_n - gap_s) / tile))
    for side in (1, -1):
        donor = [f for f in bm.faces
                 if xmin <= f.calc_center_median().x * side <= xmax
                 and ymin <= f.calc_center_median().y <= ymax]
        for k in range(1, ncells + 1):
            cell_hi = gap_n - (k - 1) * tile
            cell_lo = max(cell_hi - tile, gap_s)
            ret = bmesh.ops.duplicate(bm, geom=donor)
            geom = ret["geom"]
            verts = [g for g in geom if isinstance(g, bmesh.types.BMVert)]
            bmesh.ops.translate(bm, verts=verts, vec=(0.0, cell_hi - ymax, 0.0))
            for co, no in (((0.0, cell_hi, 0.0), (0.0, 1.0, 0.0)),
                           ((0.0, cell_lo, 0.0), (0.0, -1.0, 0.0))):
                r = bmesh.ops.bisect_plane(
                    bm, geom=geom, plane_co=co, plane_no=no,
                    clear_outer=True, clear_inner=False, dist=1e-4)
                geom = r["geom"]
    bm.to_mesh(obj.data)
    bm.free()
    return len(doomed)


def main():
    global REPORT_DIR
    os.makedirs(STAGING, exist_ok=True)
    REPORT_DIR = os.path.join(STAGING, "_report")
    os.makedirs(REPORT_DIR, exist_ok=True)
    done_path = os.path.join(REPORT_DIR, "DONE.txt")
    if os.path.exists(done_path):
        os.remove(done_path)

    summary = {"status": "error"}
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        for src in (SRC_GATE, SRC_TWR, SRC_WALL):
            try:
                bpy.ops.wm.fbx_import(filepath=src)
            except Exception:
                bpy.ops.import_scene.fbx(filepath=src)
        bpy.context.view_layer.update()

        # material .NNN dedup (editor binds by exact name)
        for mat in list(bpy.data.materials):
            m = re.match(r"^(.*)\.\d{3}$", mat.name)
            if not m:
                continue
            base = bpy.data.materials.get(m.group(1))
            if base is not None:
                mat.user_remap(base)
                bpy.data.materials.remove(mat)
            else:
                mat.name = m.group(1)

        by_name = {o.name: o for o in bpy.context.scene.objects if o.type == "MESH"}

        def resolve(part, tier):
            if tier == "bo":
                return by_name.get(part)
            cand = by_name.get(part + TIER_SUFFIX[tier])
            return cand if cand is not None else by_name.get(part)

        # tower placements: west unrotated (doors E+S), east Rz(-90) (W+S);
        # interiors get the kit-standard extra 180
        placements = [
            _m for _m in (
                Matrix.Translation((-TWR_X, TWR_Y, TWR_Z)),
                Matrix.Translation((TWR_X, TWR_Y, TWR_Z))
                @ Matrix.Rotation(math.radians(-90.0), 4, "Z"),
            )
        ]


        outputs = []
        stats = {}
        for tier in ("base", "lod3", "lod6", "bo"):
            dups = []
            # 1) the cut gatehouse body
            gate_src = by_name[GATE_TIERS[tier]]
            body = gate_src.copy()
            body.data = gate_src.data.copy()
            bpy.context.scene.collection.objects.link(body)
            removed = cut_towers(body)
            dups.append(body)
            # 2) replacement rear towers (full kit part set)
            for M in placements:
                for part in TWR_PARTS["bo" if tier == "bo" else "visual"]:
                    src = resolve(part, tier)
                    if src is None:
                        continue
                    dup = src.copy()
                    dup.data = src.data.copy()
                    pm = M
                    if "_int" in part:
                        pm = M @ Matrix.Rotation(math.radians(INT_ROT), 4, "Z")
                    dup.matrix_world = pm
                    bpy.context.scene.collection.objects.link(dup)
                    dups.append(dup)


            mesh = bpy.data.meshes.new("join_target")
            target = bpy.data.objects.new("join_target", mesh)
            bpy.context.scene.collection.objects.link(target)
            bpy.context.view_layer.update()
            select_only(dups + [target])
            bpy.context.view_layer.objects.active = target
            bpy.ops.object.join()
            joined = bpy.context.view_layer.objects.active
            name = f"bo_{SECTION}" if tier == "bo" else SECTION + TIER_SUFFIX[tier]
            joined.name = joined.data.name = name
            if tier == "bo":
                joined.data.materials.clear()
                stone = bpy.data.materials.get("stone") or bpy.data.materials.new("stone")
                joined.data.materials.append(stone)
            bpy.context.view_layer.update()
            stats[name] = {"tris": tri_count(joined), "cut_faces": removed,
                           "dims_m": [round(v, 2) for v in joined.dimensions]}
            log(f"[gatehouse] {name}: {stats[name]}")
            outputs.append(joined)

        select_only(outputs)
        bpy.ops.export_scene.fbx(
            filepath=OUT_FBX, use_selection=True, object_types={"MESH"},
            use_mesh_modifiers=True, bake_space_transform=True,
            add_leaf_bones=False, path_mode="AUTO")
        log(f"[export] {OUT_FBX}")

        # renders: front, rear-connection, top
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 48
        for o in bpy.context.scene.objects:
            if o.type == "MESH":
                o.hide_render = o.name != SECTION
        sun_data = bpy.data.lights.new("sun", type="SUN")
        sun_data.energy = 3.5
        sun = bpy.data.objects.new("sun", sun_data)
        scene.collection.objects.link(sun)
        sun.rotation_euler = (math.radians(50), 0.0, math.radians(-30))
        world = bpy.data.worlds.new("w")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.35, 0.38, 1.0)
        scene.world = world
        scene.render.resolution_x = 1600
        scene.render.resolution_y = 1000
        cam_data = bpy.data.cameras.new("cam")
        cam = bpy.data.objects.new("cam", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        shots = {
            "front": (Vector((0.0, -95.0, 45.0)), Vector((0.0, -10.0, 12.0))),
            "rear": (Vector((0.0, 75.0, 50.0)), Vector((0.0, -5.0, 15.0))),
            "top": (Vector((0.0, -12.0, 130.0)), Vector((0.0, -12.0, 0.0))),
            "side": (Vector((85.0, -35.0, 30.0)), Vector((0.0, -16.0, 12.0))),
        }
        for nm, (loc, look) in shots.items():
            cam.location = loc
            cam.rotation_euler = (look - loc).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(STAGING, f"{nm}.png")
            bpy.ops.render.render(write_still=True)
            log(f"[render] {nm}.png")

        summary = {"status": "ok", "out": OUT_FBX, "tiers": stats}
    except Exception:
        log(traceback.format_exc())
        summary["trace"] = traceback.format_exc(limit=3)

    with open(done_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[done] {summary['status']}")


main()
