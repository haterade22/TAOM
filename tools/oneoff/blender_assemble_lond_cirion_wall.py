"""
Assemble Lond Cirion city-wall section 01 — an L-shaped wall with towers —
from the Gondor castle L3 wall kit (AssetSources/Scenes/Gondor/walls),
following the Minas Tirith blockout template format (one kit FBX; per
section: base mesh + .lod3 + .lod6 + bo_ collision twin, origin pivot).

Pieces (all measured 2026-07-28, l3_family.json in the staging dir):
    gondor_castle_wall_20m_l3_a   20 m wall, X -10..+10, deck z=15,
                                  outer face +Y; merlon add-ons _m1.._m5
                                  (outer) + _m6 (inner railing)
    gondor_castle_wall_tower_l3_a 10.8 m square tower, doors on BOTH +-X
                                  faces centred at y=0, threshold z=15
                                  (flush with the wall deck) — the in-line
                                  arm tower; full interior (floor+stairs)
    gondor_castle_wall_tower_l3_b 14.2 m square tower, doors on the
                                  ADJACENT +X and -Y faces (authored
                                  corner tower), thresholds z=10 -> placed
                                  at z=+5 so doors meet the deck and its
                                  crown (20.16+5) aligns with tower a's
                                  (25.05) — the corner anchor

Layout (corner at origin, arm A along +X, arm B along -Y via Rz(-90),
city interior southwest; 0.1 m butt overlap so seams can't gap):
    corner: tower b @ (0,0,+5), no rotation — +X door faces arm A,
            -Y door faces arm B (player crosses deck through the tower)
    each arm: wall @16.9, wall @36.9, tower a @51.8, wall @66.7,
              tower a @81.6  (~87 m per arm)

Tower interiors ARE included (floors + spiral stairs — the towers are
enterable); decal meshes are not. bo_ tier takes a single 'stone' slot.
Materials from the three source FBXs share names — the .NNN duplicates
Blender creates on import are remapped back before joining (editor slots
bind by exact name; .001 slots render white).

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
SRC_FILES = [
    os.path.join(GONDOR, "walls", "gondor_castle_wall_20m_l3_a.fbx"),
    os.path.join(GONDOR, "walls", "gondor_castle_wall_tower_l3_a.fbx"),
    os.path.join(GONDOR, "walls", "gondor_castle_wall_tower_l3_b.fbx"),
    os.path.join(GONDOR, "walls", "gondor_castle_gatehouse_l1_a.fbx"),
]
OUT_FBX = os.path.join(GONDOR, "blockout", "lond_cirion_wall_a.fbx")
STAGING = r"E:\LOTRAOMAssets\_export\lond_cirion\wall_01"

WALL = "gondor_castle_wall_20m_l3_a"
TWR_A = "gondor_castle_wall_tower_l3_a"
TWR_B = "gondor_castle_wall_tower_l3_b"
GATE = "gondor_castle_gatehouse_l1_a"

# per piece kind: visual part names + bo part names (tier names resolved at
# runtime: <part>.lod3/.lod6 when present, else the base part carries over)
PARTS = {
    "wall": {
        "visual": [WALL] + [f"{WALL}_m{i}" for i in range(1, 7)],
        "bo": [f"bo_{WALL}"] + [f"bo_{WALL}_m{i}" for i in range(1, 7)],
    },
    "tower_a": {
        "visual": [f"{TWR_A}.wall", f"{TWR_A}.ground",
                   f"{TWR_A}_int.floor", f"{TWR_A}_int.stairs"]
                  + [f"{TWR_A}_m{i}" for i in range(1, 13)],
        "bo": [f"bo_{TWR_A}", f"bo_{TWR_A}_int"]
              + [f"bo_{TWR_A}_m{i}" for i in range(1, 13)],
    },
    "tower_b": {
        "visual": [TWR_B, f"{TWR_B}_int.floor", f"{TWR_B}_int.stairs"]
                  + [f"{TWR_B}_m{i}" for i in range(1, 13)],
        "bo": [f"bo_{TWR_B}", f"bo_{TWR_B}_int"]
              + [f"bo_{TWR_B}_m{i}" for i in range(1, 13)],
    },
    # gatehouse: no interior; deck top 15.0 == the L3 wall deck despite the
    # l1 name (measured 2026-07-28, gatehouse_inv); outer merlons +Y like
    # the wall; 16.4 m wide, gate tunnel through +-Y at ground level
    "gate": {
        "visual": [GATE] + [f"{GATE}_m{i}" for i in range(1, 5)],
        "bo": [f"bo_{GATE}"] + [f"bo_{GATE}_m{i}" for i in range(1, 5)],
    },
}
TIER_SUFFIX = {"base": "", "lod3": ".lod3", "lod6": ".lod6"}

ARM_WALL_S = [16.9, 36.9, 66.7]   # wall centres along the arm axis
ARM_TWRA_S = [51.8, 81.6]         # in-line tower centres
TWR_B_Z = 5.0                     # corner tower raise: doors z10 -> deck z15

# Interior rotations (user direction 2026-07-28: "rotate the piece in the
# tower so the stairs are on the window side"): put the stairwell descent
# and flight against the window/plain walls, never the doors. The flight's
# high end edge-clips one door lane (samples sit at the lane edge,
# doorzones.json) — walkable, confirmed in-editor by the user's read.
#   tower_a: rot90  -> descent on +Y window wall, flight edge at +X
#   tower_b: rot180 -> descent on +Y plain wall, flight edge at -Y
INTERIOR_ROT = {"tower_a": 90.0, "tower_b": 180.0}

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


def _rz(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Z")


def _t(x, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def section_plans():
    """Section name -> [(kind, world matrix)]. Chirality law: never rotate a
    handed wall piece with the arm direction — orient outer-consistent first,
    translate in world space after (the arm-B lesson, 2026-07-28). Kinks keep
    both walls' +X eastward (outer +Y stays north-ish) rotated +-half the
    bend angle; wall inner ends tuck 14.2 m from the vertex so their merlon
    corners stay inside the vertex tower's shell. The L is achiral (identical
    arms) — mirrored variants are editor rotations, no extra section."""
    plans = {}

    # 01 — the proven L (arm A east, arm B south, city interior southeast)
    plan = [("tower_b", _t(0, 0, TWR_B_Z))]
    for s in ARM_WALL_S:
        plan += [("wall", _t(s)), ("wall", _t(0, -s) @ _rz(90.0))]
    for s in ARM_TWRA_S:
        plan += [("tower_a", _t(s)), ("tower_a", _t(0, -s) @ _rz(90.0))]
    plans["lond_cirion_wall_01"] = plan

    # 02 — gatehouse straight: gate half 8.0; walls at +-(8.0-0.1+10);
    # end towers at +-(27.9-0.1+5.0); deck runs level across the gate top
    plans["lond_cirion_wall_02"] = [
        ("gate", Matrix.Identity(4)),
        ("wall", _t(17.9)), ("wall", _t(-17.9)),
        ("tower_a", _t(32.8)), ("tower_a", _t(-32.8)),
    ]

    # 03 — straight run with a mid tower (~50 m chaining piece)
    plans["lond_cirion_wall_03"] = [
        ("tower_a", Matrix.Identity(4)),
        ("wall", _t(14.9)), ("wall", _t(-14.9)),
    ]

    # 04/05 — coastal kinks: tower at the vertex (outer window face = the
    # convex bisector), walls headed east rotated +-half-angle, centred
    # 14.2 m out along their own heading
    for name, bend in (("lond_cirion_wall_04", 22.5), ("lond_cirion_wall_05", 45.0)):
        h = bend / 2.0
        plans[name] = [
            ("tower_a", Matrix.Identity(4)),
            ("wall", _rz(h) @ _t(-14.2)),
            ("wall", _rz(-h) @ _t(14.2)),
        ]

    # 06 — gate front v3: RECESSED GATE COURT (user's traced shape,
    # 2026-07-28). Build frame: water/outer = -Y. Wings run along X at y=0
    # (the waterfront line, outer south); at each wing's inner end a corner
    # tower turns a LEG north (the section-01 arm rhythm, ending in a
    # tower); the gate face runs between the two leg end-towers at
    # y = 81.6, set back from the waterfront, outer south over the court.
    # Filler walls join each leg end-tower to the gate piece (their deck
    # dead-ends on the tower's plain face — the established pattern).
    # Corners: left Rz(180) (doors west+north), right Rz(90) (doors
    # east+north); leg outer faces west/east, wings+face Rz(180).
    yF = ARM_TWRA_S[-1]              # 81.6 — gate-face line = leg end-tower centres
    xC = 63.0                        # corner centres; makes the filler span close
    plan = [("gate", _t(0, yF) @ _rz(180.0))]
    for s in (17.9, 47.7):           # face walls + filler walls
        plan += [("wall", _t(s, yF) @ _rz(180.0)),
                 ("wall", _t(-s, yF) @ _rz(180.0))]
    plan += [("tower_a", _t(32.8, yF) @ _rz(180.0)),
             ("tower_a", _t(-32.8, yF) @ _rz(180.0))]
    plan += [("tower_b", _t(-xC, 0, TWR_B_Z) @ _rz(180.0)),
             ("tower_b", _t(xC, 0, TWR_B_Z) @ _rz(90.0))]
    for kind, dists in (("wall", ARM_WALL_S), ("tower_a", ARM_TWRA_S)):
        for d in dists:
            plan += [
                (kind, _t(-xC - d, 0) @ _rz(180.0)),   # west wing (outer S)
                (kind, _t(xC + d, 0) @ _rz(180.0)),    # east wing (outer S)
                (kind, _t(-xC, d) @ _rz(90.0)),        # west leg (outer W)
                (kind, _t(xC, d) @ _rz(-90.0)),        # east leg (outer E)
            ]
    plans["lond_cirion_wall_06"] = plan
    return plans


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
        for src in SRC_FILES:
            try:
                bpy.ops.wm.fbx_import(filepath=src)
            except Exception:
                bpy.ops.import_scene.fbx(filepath=src)
        bpy.context.view_layer.update()

        # the source FBXs share material names — Blender renames later
        # imports' copies to <name>.001, which the editor cannot bind
        # (slots bind by exact name; .001 slots render white)
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
        # sanity: kit pieces are authored at identity
        for name, o in by_name.items():
            if any(abs(v) > 1e-4 for v in o.matrix_world.to_translation()):
                raise RuntimeError(f"{name} not at origin: {o.matrix_world.to_translation()}")

        def resolve(part, tier):
            if tier == "bo":
                return by_name.get(part)
            cand = by_name.get(part + TIER_SUFFIX[tier])
            if cand is None and tier != "base":
                cand = by_name.get(part)  # no LOD authored: base carries over
            return cand

        missing = [p for kind in PARTS.values() for p in kind["visual"] + kind["bo"]
                   if p not in by_name]
        if missing:
            log(f"[warn] source parts not found (skipped): {missing}")

        outputs = []
        tier_stats = {}
        fallbacks = set()
        section_bases = {}
        for section, plan in section_plans().items():
            for tier in ("base", "lod3", "lod6", "bo"):
                dups = []
                for kind, mat in plan:
                    parts = PARTS[kind]["bo" if tier == "bo" else "visual"]
                    for part in parts:
                        src = resolve(part, tier)
                        if src is None:
                            continue
                        if tier in ("lod3", "lod6") and src.name == part and \
                                (part + TIER_SUFFIX[tier]) not in by_name:
                            fallbacks.add(part)
                        dup = src.copy()
                        dup.data = src.data.copy()
                        part_mat = mat
                        if "_int" in part and kind in INTERIOR_ROT:
                            part_mat = mat @ Matrix.Rotation(
                                math.radians(INTERIOR_ROT[kind]), 4, "Z")
                        dup.matrix_world = part_mat
                        bpy.context.scene.collection.objects.link(dup)
                        dups.append(dup)
                mesh = bpy.data.meshes.new("join_target")
                target = bpy.data.objects.new("join_target", mesh)
                bpy.context.scene.collection.objects.link(target)
                bpy.context.view_layer.update()  # join must see the fresh matrices
                select_only(dups + [target])
                bpy.context.view_layer.objects.active = target
                bpy.ops.object.join()
                joined = bpy.context.view_layer.objects.active
                name = f"bo_{section}" if tier == "bo" else section + TIER_SUFFIX[tier]
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
                if tier == "base":
                    section_bases[section] = joined
        if fallbacks:
            log(f"[lods] parts without authored LODs (base carried over): {sorted(fallbacks)}")

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

        # geometry-only previews, one per section (kit materials live editor-side)
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 32
        from mathutils import Vector
        cam_data = bpy.data.cameras.new("cam")
        cam = bpy.data.objects.new("cam", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
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
        for section, base in section_bases.items():
            # hide EVERYTHING except this section's base — the imported
            # source pieces still sit at the origin and z-fight the render
            # otherwise (the export is unaffected: use_selection)
            for o in bpy.context.scene.objects:
                if o.type == "MESH":
                    o.hide_render = o is not base
            bpy.context.view_layer.update()
            d = max(base.dimensions)
            cam.location = Vector((d * 0.85, -d * 0.75, d * 0.65 + 15.0))
            look = Vector((0.0, 0.0, 12.0))
            cam.rotation_euler = (look - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(STAGING, f"preview_{section}.png")
            bpy.ops.render.render(write_still=True)
            log(f"[preview] {scene.render.filepath}")

        summary = {"status": "ok", "out": OUT_FBX, "tiers": tier_stats,
                   "sections": list(section_bases)}
    except Exception:
        log(traceback.format_exc())
        summary["trace"] = traceback.format_exc(limit=3)

    with open(done_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[done] {summary['status']}")


main()
