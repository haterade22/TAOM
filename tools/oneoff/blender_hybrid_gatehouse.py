"""
Build the Lond Cirion hybrid gatehouse: the minas_tirith_gatehouse_a1 body
with its four towers cut out — the two big rear octagons replaced by the
wall kit's corner tower (gondor_castle_wall_tower_l3_b, the tower "we've
been using on the walls"), the two small front roofed towers removed and
their wing ends capped with wall-kit merlon segments + a plug wall.

Measured facts driving the cuts (gatehouse_probe.json, 2026-07-29):
    ground z=0 (bbox sinks to -15), wings + bridge deck at z=15 == the wall
    kit deck; big towers occupy x sign*(6.95..22.85), y -24..+8 (centre
    +-17.5, -16.2); central gate span |x| < 6.95; front roofed towers
    x sign*(10.1..21.1), y -30.1..-18.1. New rear towers sit at x +-14.2 so
    their inner faces (7.1) meet the central span edge (6.95) flush; +5 z
    raise puts their doors (authored z10) on the z15 decks; west tower
    unrotated (doors east to the bridge + south to the wing), east Rz(-90)
    (doors west + south). INTERIOR_ROT 180 per the kit convention.

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
# Occupancy-mapped 2026-07-29 (gatehouse_map.txt): the big octagons stand at
# the BACK, centred (+-16, -1), beside the central span (edge x +-8); the
# wings are ground-level walled yards x sign*(12..20) running to y -30; the
# front roofed towers sit ON the wing walls at (+-18, -20) — so they cut
# only above z 14.5, keeping the walls beneath.
BIG_CUT = (7.9, 24.2, -8.2, 8.2, -99.0)
FRONT_CUT = (13.9, 22.5, -24.5, -15.5, 14.5)

TWR_X = 15.1          # rear tower centres: inner face 8.0 meets the span edge
TWR_Y = -1.0
TWR_Z = 5.0
INT_ROT = 180.0

# merlon caps along the wings' OUTER wall tops where the roofed towers were
# (m1 segments run along Y, outer facing outward; wall tops ~z15 = +2 vs the
# piece's authored base 13)
MERLON_TY = (-22.3, -18.3)
MERLON_X = 20.6
MERLON_Z = 2.0
M1_CENTRE = (8.0, 3.57)          # authored m1 centre (x, y)

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


def cut_towers(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        for side in (1, -1):
            if in_region(c, BIG_CUT, side) or in_region(c, FRONT_CUT, side):
                doomed.append(f)
                break
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
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

        m1_off = Matrix.Translation((-M1_CENTRE[0], -M1_CENTRE[1], 0.0))

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
            # 3) merlon caps along the outer wing-wall tops where the
            # roofed towers were (outer faces outward: west Rz(90), east
            # Rz(-90))
            m1 = resolve(f"{WALL}_m1", tier) if tier != "bo" else by_name.get(f"bo_{WALL}_m1")
            if m1 is not None:
                for side in (1, -1):
                    rz = Matrix.Rotation(math.radians(-90.0 * side), 4, "Z")
                    for ty in MERLON_TY:
                        dup = m1.copy()
                        dup.data = m1.data.copy()
                        dup.matrix_world = (Matrix.Translation(
                            (side * MERLON_X, ty, MERLON_Z)) @ rz @ m1_off)
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
