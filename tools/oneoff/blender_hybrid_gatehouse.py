"""
Build the Lond Cirion hybrid gatehouse: the minas_tirith_gatehouse_a1 body
with the two big rear octagons replaced by the wall kit's corner tower
(gondor_castle_wall_tower_l3_b, the tower "we've been using on the walls")
and the two wings — which the user identified as embedded instances of
gondor_castle_wall_tower_l1_d — cut off whole and replaced by clean kit
instances of that piece. The front roofed towers stood ON the merged wings,
so the wing swap removes them too; l1_d brings its own merlons, stairwell
pit, and machicolated prow.

Measured facts driving the build (gatehouse_probe.json + wing_probe/bins.txt
+ l1d_inv/inventory.json, 2026-07-29):
    ground z=0, decks at z=15 == the wall kit deck; big octagons occupy
    x sign*(6.95..22.85), y -8..+8 beside the central gate span (|x| < 6.95);
    the merged wings run y -8.2..-34.36, parapet x sign*(18.28..19.46).
    l1_d is authored root at +Y (12.1), prow at -Y (-18.558), deck z 15,
    merlon band |x| 3.485..4.67, main body |x| <= 4.1: T(+-14.79, -15.8, 0)
    maps its merlon band onto the measured parapet and its prow tip onto
    the body's -34.36 tip (bo tiers agree to ~1 cm); no rotation — the
    piece is x-symmetric, root embeds into the rear-tower footprint like it
    embedded into the octagons. New rear towers sit at x +-15.1 so their
    inner faces (half-size 7.1 -> 8.0) meet the cut edge (7.9); +5 z raise
    puts their doors (authored z10) on the z15 decks; west tower unrotated
    (doors east to the bridge + south to the wing), east Rz(-90) (doors
    west + south). INTERIOR_ROT 180 per the kit convention.

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
SRC_L1D = os.path.join(GONDOR, "walls", "gondor_castle_wall_tower_l1_d.fbx")
OUT_FBX = os.path.join(GONDOR, "blockout", "lond_cirion_gatehouse_a.fbx")
STAGING = r"E:\LOTRAOMAssets\_export\lond_cirion\gatehouse"
SECTION = "lond_cirion_gatehouse_01"

TWR_B = "gondor_castle_wall_tower_l3_b"
L1D = "gondor_castle_wall_tower_l1_d"

# cut regions: (xmin, xmax, ymin, ymax, zmin) — mirrored for both sides.
# The big octagons stand at the BACK, centred (+-16, -1), beside the
# central span (edge x +-8); everything south of them in the wing band is
# the merged l1_d wing (plus the front roofed tower standing on it) — cut
# whole and replaced by a clean kit instance.
BIG_CUT = (7.9, 24.2, -8.2, 8.2, -99.0)
WING_CUT = (9.0, 23.8, -35.0, -8.2, -99.0)

TWR_X = 15.1          # rear tower centres: inner face 8.0 meets the cut edge
TWR_Y = -1.0
TWR_Z = 5.0
INT_ROT = 180.0

L1D_X = 14.79         # authored merlon edge 4.67 -> wing parapet 19.46
L1D_Y = -15.8         # authored prow tip -18.558 -> body tip -34.36

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
# NB the addon prefix really is "..._l1_d1_m{i}" (d1), not "..._l1_d_m{i}"
L1D_PARTS = {
    "visual": [L1D] + [f"{L1D}1_m{i}" for i in range(1, 14)],
    "bo": [f"bo_{L1D}"] + [f"bo_{L1D}1_m{i}" for i in range(1, 14)],
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
            if in_region(c, BIG_CUT, side) or in_region(c, WING_CUT, side):
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
        for src in (SRC_GATE, SRC_TWR, SRC_L1D):
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

        # rear towers: west unrotated (doors E+S), east Rz(-90) (W+S),
        # interiors get the kit-standard extra 180; wings: l1_d unrotated
        # both sides (x-symmetric piece, prow already faces south)
        placements = [
            (Matrix.Translation((-TWR_X, TWR_Y, TWR_Z)), TWR_PARTS),
            (Matrix.Translation((TWR_X, TWR_Y, TWR_Z))
             @ Matrix.Rotation(math.radians(-90.0), 4, "Z"), TWR_PARTS),
            (Matrix.Translation((-L1D_X, L1D_Y, 0.0)), L1D_PARTS),
            (Matrix.Translation((L1D_X, L1D_Y, 0.0)), L1D_PARTS),
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
            # 2) replacement kit pieces (full part sets)
            for M, parts in placements:
                for part in parts["bo" if tier == "bo" else "visual"]:
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
