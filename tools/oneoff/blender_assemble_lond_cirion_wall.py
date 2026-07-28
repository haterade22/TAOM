"""
Assemble Lond Cirion city-wall section 01 — an L-shaped wall with towers —
from the existing Gondor castle kit pieces, following the Minas Tirith
blockout template format (one kit FBX; per section: base mesh + .lod3 +
.lod6 + bo_ collision twin, pivot at the plop anchor).

Sources (AssetSources/Scenes/Gondor/meshes):
    gondor_castle_wall_L1.fbx    -> gondor_castle_wall_20m_L1_A (+lods, bo_)
                                    20 x 6.2 x 15.3 m, X -10..+10, outer -Y
    gondor_castle_tower_L1_a.fbx -> gondor_castle_wall_tower_L1_A.wall/_top
                                    (+lods, bo_ + int.001 top-floor bo)
                                    9 x 10 x 25.5 m, X +-4.5, outer -6.5

Layout (symmetric L, corner at origin, city interior = northeast):
    corner tower  rotated -45 deg (outer face bisects both outward
                  directions; wall ends overlap into its footprint)
    arm A along +X / arm B along -Y (arm A rotated -90), each:
        wall @12.5  wall @32.5  tower @47  wall @61.5  end tower @76
    -> 6 walls + 5 towers, ~80.5 m per arm.

Simplifications (v1, noted in report): tower wood interiors (_int) and
decal meshes skipped; bo_ takes a single 'stone' physics slot (the wood
top-platform floor rides under stone physics).

Headless (MS-Store launcher DETACHES; completion = <staging>\\_report\\DONE.txt):
    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_assemble_lond_cirion_wall.py
"""

import json
import math
import os
import re
import traceback

import bpy
from mathutils import Matrix

GONDOR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\Gondor"
SRC_WALL = os.path.join(GONDOR, "meshes", "gondor_castle_wall_L1.fbx")
SRC_TOWER = os.path.join(GONDOR, "meshes", "gondor_castle_tower_L1_a.fbx")
OUT_FBX = os.path.join(GONDOR, "blockout", "lond_cirion_wall_a.fbx")
STAGING = r"E:\LOTRAOMAssets\_export\lond_cirion\wall_01"
SECTION = "lond_cirion_wall_01"

# tier -> {part key -> source object name}
WALL_TIERS = {
    "base": "gondor_castle_wall_20m_L1_A",
    "lod3": "gondor_castle_wall_20m_L1_A.lod3",
    "lod6": "gondor_castle_wall_20m_L1_A.lod6",
    "bo": "bo_gondor_castle_wall_20m_L1_A",
}
TOWER_TIERS = {
    "base": ["gondor_castle_wall_tower_L1_A.wall", "gondor_castle_wall_tower_L1_A_top"],
    "lod3": ["gondor_castle_wall_tower_L1_A.wall.lod3", "gondor_castle_wall_tower_L1_A_top.lod3"],
    "lod6": ["gondor_castle_wall_tower_L1_A.wall.lod6", "gondor_castle_wall_tower_L1_A_top.lod6"],
    "bo": ["bo_gondor_castle_wall_tower_L1_A", "bo_gondor_castle_wall_tower_L1_A_int.001"],
}
TIER_SUFFIX = {"base": "", "lod3": ".lod3", "lod6": ".lod6"}

ARM_WALL_S = [12.5, 32.5, 61.5]   # wall centres along the arm axis
ARM_TOWER_S = [47.0, 76.0]        # mid + end tower centres

# The tower's walkway-level door is authored OFF the wall line: cap-arch
# outline on the +-X faces spans y -4.91..-3.09 (centre -4.0) with its
# threshold at z 9.82..10.06 — exactly the wall walkway floor (z~10.0,
# y -3.0..+3.09, centre +0.05). Shift every tower +4.05 in its local Y so
# the door lands centred on the walkway (measured 2026-07-28, measure3.json).
TOWER_DY = 4.05

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


def placements():
    """(kind, world matrix) for every piece. Arm A as-modeled along +X;
    arm B = same distances under Rz(-90) (axis -Y, outer face -X); the
    shared corner tower at Rz(-45)."""
    rz = lambda deg: Matrix.Rotation(math.radians(deg), 4, "Z")
    out = [("tower", rz(-45.0) @ Matrix.Translation((0, TOWER_DY, 0)))]
    for s in ARM_WALL_S:
        out.append(("wall", Matrix.Translation((s, 0, 0))))
        out.append(("wall", rz(-90.0) @ Matrix.Translation((s, 0, 0))))
    for s in ARM_TOWER_S:
        out.append(("tower", Matrix.Translation((s, TOWER_DY, 0))))
        out.append(("tower", rz(-90.0) @ Matrix.Translation((s, TOWER_DY, 0))))
    return out


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
        for src in (SRC_WALL, SRC_TOWER):
            try:
                bpy.ops.wm.fbx_import(filepath=src)
            except Exception:
                bpy.ops.import_scene.fbx(filepath=src)
        bpy.context.view_layer.update()

        # the two source FBXs share material names — Blender renames the
        # second import's copies to <name>.001, which the editor cannot bind
        # (slots bind by exact name; .001 slots render white). Remap every
        # .NNN duplicate onto its base-named material.
        for mat in list(bpy.data.materials):
            m = re.match(r"^(.*)\.\d{3}$", mat.name)
            if not m:
                continue
            base = bpy.data.materials.get(m.group(1))
            if base is not None:
                mat.user_remap(base)
                bpy.data.materials.remove(mat)
                log(f"[materials] remapped duplicate -> {m.group(1)}")
            else:
                mat.name = m.group(1)
                log(f"[materials] renamed stray duplicate -> {m.group(1)}")

        sources = {}
        needed = set(WALL_TIERS.values())
        for names in TOWER_TIERS.values():
            needed.update(names)
        for o in list(bpy.context.scene.objects):
            if o.type == "MESH" and o.name in needed:
                sources[o.name] = o
            else:
                bpy.data.objects.remove(o, do_unlink=True)
        missing = needed - set(sources)
        if missing:
            raise RuntimeError(f"missing source pieces: {sorted(missing)}")
        # sanity: kit pieces are authored at identity — a non-identity
        # transform here would silently shift every placement
        for name, o in sources.items():
            if any(abs(v) > 1e-4 for v in o.matrix_world.to_translation()):
                raise RuntimeError(f"{name} not at origin: {o.matrix_world.to_translation()}")

        plan = placements()
        outputs = []
        tier_stats = {}
        for tier in ("base", "lod3", "lod6", "bo"):
            dups = []
            for kind, mat in plan:
                names = [WALL_TIERS[tier]] if kind == "wall" else TOWER_TIERS[tier]
                for n in names:
                    src = sources[n]
                    dup = src.copy()
                    dup.data = src.data.copy()
                    dup.matrix_world = mat
                    bpy.context.scene.collection.objects.link(dup)
                    dups.append(dup)
            # join into a fresh identity-pivot target so the section pivot
            # is the corner-tower anchor at the world origin
            mesh = bpy.data.meshes.new("join_target")
            target = bpy.data.objects.new("join_target", mesh)
            bpy.context.scene.collection.objects.link(target)
            bpy.context.view_layer.update()  # join must see the fresh matrices
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
            tier_stats[name] = {
                "tris": tri_count(joined),
                "dims_m": [round(v, 2) for v in joined.dimensions],
            }
            log(f"[join] {name}: {tier_stats[name]}")
            outputs.append(joined)

        select_only(outputs)
        bpy.ops.export_scene.fbx(
            filepath=OUT_FBX,
            use_selection=True,
            object_types={"MESH"},
            use_mesh_modifiers=True,
            bake_space_transform=True,
            add_leaf_bones=False,
            path_mode="AUTO",
        )
        log(f"[export] {OUT_FBX}")

        # geometry-only preview (kit materials live editor-side)
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 32
        for o in outputs:
            o.hide_render = o.name != SECTION
        base = next(o for o in outputs if o.name == SECTION)
        from mathutils import Vector
        cam_data = bpy.data.cameras.new("cam")
        cam = bpy.data.objects.new("cam", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        cam.location = Vector((95.0, 60.0, 70.0))
        cam.rotation_euler = (Vector((30.0, -35.0, 8.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sun_data = bpy.data.lights.new("sun", type="SUN")
        sun_data.energy = 3.0
        sun = bpy.data.objects.new("sun", sun_data)
        scene.collection.objects.link(sun)
        sun.rotation_euler = (math.radians(50), 0.0, math.radians(-30))
        world = bpy.data.worlds.new("w")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.12, 0.13, 1.0)
        scene.world = world
        scene.render.resolution_x = 1400
        scene.render.resolution_y = 900
        scene.render.filepath = os.path.join(STAGING, "preview.png")
        bpy.ops.render.render(write_still=True)
        log(f"[preview] {scene.render.filepath}")

        summary = {"status": "ok", "out": OUT_FBX, "tiers": tier_stats,
                   "pieces": {"walls": 6, "towers": 5}}
    except Exception:
        log(traceback.format_exc())
        summary["trace"] = traceback.format_exc(limit=3)

    with open(done_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[done] {summary['status']}")


main()
