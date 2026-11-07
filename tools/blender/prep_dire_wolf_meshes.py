"""Prepare the purchased ADOD_Beasts wolf for a Modding Kit import as TAOM's dire wolf (#1081).

Headless (the Store launcher DETACHES: completion is <out>/prep_report.json, status ok or error, from blender_job.job):
    blender-launcher.exe -b --factory-startup -P tools/blender/prep_dire_wolf_meshes.py -- ^
        --src "E:\\LOTRAOMAssets\\ADOD_Beasts\\AssetSources\\wolves\\adod_wolf_model.fbx" ^
        --saddle "<Armory>\\AssetSources\\creature\\warg\\mesh\\Warg_Rig_V5.fbx" ^
        --out "E:\\LOTRAOMAssets\\_reskin_out\\dire_wolf"

Why this exists rather than importing the pack's FBX directly:
- The pack's cooked mesh tpacs are 1.6 GB against a 17 MB FBX. Body, Fur and Mouth carry shape keys, the likely
  cause, and nothing in TAOM drives them, so they are cleared.
- Its four model FBX files hold the same geometry and the same generic material names (Body, Fur, Eye). The Kit
  binds materials by name across the module, so each skin gets its own names here.
- It has no LODs. Warg_Rig_V5 ships LOD0 to LOD5, and fur cards do not survive decimation, so they stop at LOD2.
- A rider needs a seat, and warg_saddle is skinned to skeleton_warg. The warg's saddle geometry is scaled to the
  wolf, placed on its back and weighted to its own spine, inside the FBX that defines the skeleton
  (creature-mount-authoring.md, replace table row 6).
- The pack's wolf stands 0.93 m, and the engine draws portraits and the inventory at mesh size, so it looked like a
  pup beside the 1.70 m warg (Mike, 2026-11-06). The whole rig, saddle included, is scaled by RIG_SCALE last
  (tools/blender/rig_scale.py). The skeleton package and the animation masters take the same factor
  (apply_dire_wolf_skeleton.py --scale, scale_dire_wolf_anims.py).
- The engine seats a rider where its mount's usage's rider clip puts it in model space; rider_sit_bone only decides
  what the rider follows (docs/features/dire-wolf.md "Seat"). The dire wolf uses the warg's rider rows, so its
  saddle must sit where the warg's does. RIG_SCALE brings the seat to the warg's height, and the rig then moves
  RIG_BACK toward the tail (--back), the same move the skeleton and the masters take. The gate measures both seats
  and fails beyond SEAT_TOLERANCE.

The armature keeps its name and bone order: WargConfig.ImpactBoneIds and the pack's animation masters index it.
"""
import argparse
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blender_job import flag_value, job, script_args  # noqa: E402
from rig_scale import scale_rig, seat_point, tail_offset  # noqa: E402

SKINS = ("grey", "black", "brown", "white")
ARMATURE = "adod_wolf_armature_unused"

# Pack material -> Kit material. Fur and Claws use the body texture in the pack, and no map has an alpha channel.
# One mouth material serves every skin: the pack shares its mouth maps, and Mike's Kit holds one (2026-11-04).
MATERIALS = {"Body": "dire_wolf_{skin}_body", "Fur": "dire_wolf_{skin}_body", "Mouth": "dire_wolf_mouth",
             "Wiskers": "dire_wolf_{skin}_whiskers"}
# Each skin's default eye. A Horse item can override it in XML with <Materials> in its <Horse> node, once the eye sub-mesh carries the
# horse_body tag in the Kit: the engine swaps only tagged sub-meshes (MountVisualCreator.SetMaterialProperties).
EYE_PARTS = ("Eye", "Eyes Ball")
EYE_MATERIALS = {"grey": "dire_wolf_eye_yellow", "black": "dire_wolf_eye_red", "brown": "dire_wolf_eye_yellow",
                 "white": "dire_wolf_eye_white"}

# (ratio, parts). The ratios follow warg_low's LOD chain, but LOD1 and LOD2 are heavier than warg_low's, because fur
# cards do not survive decimation. Triangles: warg 24525, 6130, 3321, 1800, 976, 415; wolf 23738, 11870, 5381, 1985, 956, 382.
LODS = (
    (1.0, ("Body", "Claws", "Eyes", "Fur", "Mouth", "Wiskers")),
    (0.5, ("Body", "Claws", "Eyes", "Fur", "Mouth", "Wiskers")),
    (0.3, ("Body", "Claws", "Eyes", "Fur")),
    (0.18, ("Body", "Claws")),
    (0.1, ("Body",)),
    (0.04, ("Body",)),
)
SADDLE_LOD_COUNT = 6
SADDLE_BONE = "Spine1"
# skeleton_warg is 2.37 times the pack's wolf (Root_M to Head_M 2.128 m, Pelvis to Head 0.814 m on the fitted axes).
# The saddle is fitted at the pack wolf's size, then scale_rig resizes it with the rest of the rig.
WARG_TO_WOLF = 1.0 / 2.37
SEAT_ABOVE_BACK = 0.02
# 0.93 m to 1.94 m tall: portraits and the inventory draw this size. 2.08 puts the wolf's seat at the warg's seat
# height (1.26 m at 1.73, the warg's 1.51 m). Battle size is the Monster's taom_body_length on top (115).
RIG_SCALE = 2.08
# Model units toward the tail: the wolf's seat (y 0.39 at 2.08, nose +y) onto the warg's (y -0.13), 2026-11-06.
RIG_BACK = 0.52
SEAT_TOLERANCE = 0.06  # model units, in y and in z
TAIL = (0.0, 1.0, 0.0)  # scale_dire_wolf_anims.TAIL: the animation files move their object along it


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument("--src", required=True)
    p.add_argument("--saddle", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--scale", type=float, default=RIG_SCALE)
    p.add_argument("--back", type=float, default=RIG_BACK)
    return p.parse_args(script_args())


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def single_uv(obj):
    """Keep the rendered UV layer under one name, because a join merges layers by name and blanks the rest."""
    layers = obj.data.uv_layers
    keep = next((u for u in layers if u.active_render), layers.active or layers[0])
    for u in [x for x in layers if x.name != keep.name]:
        layers.remove(u)
    layers[0].name = "UVMap"


def decimate(obj, ratio):
    if ratio >= 1.0:
        return
    activate(obj)
    mod = obj.modifiers.new("lod", "DECIMATE")
    mod.ratio = ratio
    while obj.modifiers[0].name != "lod":
        bpy.ops.object.modifier_move_up(modifier="lod")
    bpy.ops.object.modifier_apply(modifier="lod")


def build_lod(parts, armature, name, ratio, keep):
    copies = []
    for key in keep:
        src = parts[key]
        obj = src.copy()
        obj.data = src.data.copy()
        bpy.context.collection.objects.link(obj)
        decimate(obj, ratio)
        copies.append(obj)
    activate(copies[0])
    for c in copies:
        c.select_set(True)
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    activate(joined)
    bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.001)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    return joined


def import_saddles(path, armature, body):
    """Bring orc_rider_saddle and its LODs across, rigid on the wolf's spine bone."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    imported = set(bpy.data.objects) - before
    warg = next(o for o in imported if o.type == "ARMATURE")
    warg_forward = warg.matrix_world @ warg.data.bones["Head_M"].head_local - warg.matrix_world @ warg.data.bones["Root_M"].head_local
    wolf_bone = armature.matrix_world @ armature.data.bones[SADDLE_BONE].head_local
    wolf_forward = armature.matrix_world @ armature.data.bones["Head"].head_local - armature.matrix_world @ armature.data.bones["Pelvis"].head_local
    turn = 0.0 if warg_forward.y * wolf_forward.y > 0 else math.pi

    saddles = sorted((o for o in imported if o.type == "MESH" and o.name.startswith("orc_rider_saddle")), key=lambda o: o.name)
    if len(saddles) != SADDLE_LOD_COUNT:
        raise RuntimeError(f"expected {SADDLE_LOD_COUNT} saddle LODs, found {[o.name for o in saddles]}")

    near = [body.matrix_world @ v.co for v in body.data.vertices if abs((body.matrix_world @ v.co).y - wolf_bone.y) < 0.12 and abs((body.matrix_world @ v.co).x) < 0.05]
    back_top = max(p.z for p in near)

    # The seat is the mesh's top; the straps hang below it. It keeps the warg's place along the spine.
    base = saddles[0]
    points = [base.matrix_world @ v.co for v in base.data.vertices]
    # Seats compare in the engine's model frame, nose +y: each Kit import turns its creature that way.
    nose = 1.0 if warg_forward.y > 0 else -1.0
    rest_y, rest_z = seat_point([tuple(p) for p in points], -nose)
    warg_seat = (rest_y * nose, rest_z)
    top = max(p.z for p in points)
    seat = [p for p in points if p.z > top - 0.08]
    seat_y = sum(p.y for p in seat) / len(seat)
    warg_root = warg.matrix_world @ warg.data.bones["Root_M"].head_local
    along = (seat_y - warg_root.y) / warg_forward.y
    pelvis = armature.matrix_world @ armature.data.bones["Pelvis"].head_local
    target_y = pelvis.y + along * wolf_forward.y
    place = (Matrix.Translation((0.0, target_y, back_top + SEAT_ABOVE_BACK))
             @ Matrix.Rotation(turn, 4, "Z")
             @ Matrix.Scale(WARG_TO_WOLF, 4)
             @ Matrix.Translation((0.0, -seat_y, -top)))

    result = []
    for i, src in enumerate(saddles):
        name = "dire_wolf_saddle" if i == 0 else f"dire_wolf_saddle.lod{i}"
        world = place @ src.matrix_world
        src.parent = None
        src.modifiers.clear()
        src.vertex_groups.clear()
        src.data = src.data.copy()
        src.data.transform(world)
        src.matrix_world = Matrix.Identity(4)
        src.name = name
        src.data.name = name
        if src.data.shape_keys:
            src.shape_key_clear()
        group = src.vertex_groups.new(name=SADDLE_BONE)
        group.add(range(len(src.data.vertices)), 1.0, "REPLACE")
        src.modifiers.new("Armature", "ARMATURE").object = armature
        result.append(src)

    for obj in imported:
        if obj not in saddles:
            bpy.data.objects.remove(obj, do_unlink=True)
    return result, {"back_top": round(back_top, 3), "turned": turn != 0.0, "along_spine": round(along, 3),
                    "seat_y": round(target_y, 3), "spine1_y": round(wolf_bone.y, 3), "warg_seat": warg_seat,
                    "mats": sorted({s.material.name for s in saddles[0].material_slots if s.material})}


def rename_materials(objs, skin):
    for obj in objs:
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None or mat.name.startswith("dire_wolf_"):
                continue
            base = mat.name.split(".")[0]
            pattern = EYE_MATERIALS[skin] if base in EYE_PARTS else MATERIALS.get(base)
            if pattern is None:
                raise RuntimeError(f"no Kit material for pack material {mat.name!r}")
            wanted = pattern.format(skin=skin)
            slot.material = bpy.data.materials.get(wanted) or bpy.data.materials.new(wanted)


def merge_slots(obj):
    """One slot per Kit material: Body and Fur now share one, so reassign faces and drop the duplicate."""
    names = []
    remap = {}
    for i, slot in enumerate(obj.material_slots):
        name = slot.material.name
        if name not in names:
            names.append(name)
        remap[i] = names.index(name)
    mats = [bpy.data.materials[n] for n in names]
    faces = [remap[poly.material_index] for poly in obj.data.polygons]
    # clear() zeroes every face's index, so the faces go back after it.
    obj.data.materials.clear()
    for m in mats:
        obj.data.materials.append(m)
    for poly, index in zip(obj.data.polygons, faces):
        poly.material_index = index


def faces_per_material(obj):
    counts = {}
    for poly in obj.data.polygons:
        name = obj.material_slots[poly.material_index].material.name
        counts[name] = counts.get(name, 0) + 1
    return counts


def export(path):
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
        primary_bone_axis="Y", secondary_bone_axis="X", axis_forward="-Z", axis_up="Y",
        bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=False,
        apply_scale_options="FBX_SCALE_NONE", bake_space_transform=False)


def world_bones(arm):
    return {b.name: arm.matrix_world @ b.head_local for b in arm.data.bones}


def world_tails(arm):
    return {b.name: arm.matrix_world @ b.tail_local for b in arm.data.bones}


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def heights(armature, body):
    """(pelvis, lowest vertex, highest vertex) in world z: a resize about the ground scales all three alike."""
    zs = [(body.matrix_world @ v.co).z for v in body.data.vertices]
    return (armature.matrix_world @ armature.data.bones["Pelvis"].head_local).z, min(zs), max(zs)


def gate(path, want_heads, want_tails, want_order, want_tris, want_faces, want_saddle):
    """Re-import what was written. Heads alone can look right while tails drift (creature-mount-authoring.md)."""
    reset()
    bpy.ops.import_scene.fbx(filepath=path)
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    meshes = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    problems = []
    if len(arms) != 1 or arms[0].name != ARMATURE:
        return [f"armature is {[a.name for a in arms]}"]
    arm = arms[0]
    if [b.name for b in arm.data.bones] != want_order:
        problems.append("bone order changed")
    heads, tails = world_bones(arm), world_tails(arm)
    drift = max((heads[k] - want_heads[k]).length for k in want_heads)
    tail_drift = max((tails[k] - want_tails[k]).length for k in want_tails)
    if drift > 0.001:
        problems.append(f"head drift {drift:.4f} m")
    if tail_drift > 0.001:
        problems.append(f"tail drift {tail_drift:.4f} m")
    for name, count in want_tris.items():
        got = meshes.get(name)
        if got is None:
            problems.append(f"missing mesh {name}")
            continue
        if tris(got) != count:
            problems.append(f"{name}: {tris(got)} tris, wrote {count}")
        if faces_per_material(got) != want_faces[name]:
            problems.append(f"{name}: faces per material {faces_per_material(got)}, wrote {want_faces[name]}")
        if got.data.shape_keys:
            problems.append(f"{name}: shape keys survived")
        bones = {b.name for b in arm.data.bones}
        stray = [g.name for g in got.vertex_groups if g.name not in bones]
        if stray:
            problems.append(f"{name}: groups outside the skeleton {stray[:5]}")
        indexed = {g.index: g.name for g in got.vertex_groups}
        weightless = 0
        off_sum = 0
        for v in got.data.vertices:
            total = sum(e.weight for e in v.groups if e.group in indexed)
            if total <= 0.0:
                weightless += 1
            elif abs(total - 1.0) > 0.01:
                off_sum += 1
        if weightless:
            problems.append(f"{name}: {weightless} unweighted vertices")
        if off_sum:
            problems.append(f"{name}: {off_sum} vertices with weights not summing to 1")
    if want_saddle and "dire_wolf_saddle" not in meshes:
        problems.append("the saddle is missing")
    return problems


def render(path, preview_stem, location_ratio):
    reset()
    bpy.ops.import_scene.fbx(filepath=path)
    for o in bpy.data.objects:
        if o.type == "MESH" and ".lod" in o.name:
            o.hide_render = True
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "RANDOM"
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    cam = bpy.data.cameras.new("c")
    cam.type = "ORTHO"
    cam.ortho_scale = location_ratio
    obj = bpy.data.objects.new("c", cam)
    bpy.context.collection.objects.link(obj)
    scene.camera = obj
    for view, loc, rot in (("side", (5, -0.1, 0.5), (math.pi / 2, 0, math.pi / 2)), ("top", (0, -0.1, 5), (0, 0, 0))):
        obj.location, obj.rotation_euler = loc, rot
        scene.render.filepath = f"{preview_stem}_{view}.png"
        bpy.ops.render.render(write_still=True)


def main(report):
    args = parse_args()
    os.makedirs(args.out, exist_ok=True)
    report.update({"blender": bpy.app.version_string, "scale": args.scale, "back": args.back, "skins": {}})
    for skin in SKINS:
        reset()
        bpy.ops.import_scene.fbx(filepath=args.src)
        armature = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        if armature.name != ARMATURE or len(armature.data.bones) != 57:
            raise RuntimeError(f"unexpected armature {armature.name} with {len(armature.data.bones)} bones")
        armature.animation_data_clear()
        armature.data.pose_position = "REST"
        for action in list(bpy.data.actions):
            bpy.data.actions.remove(action)
        parts = {}
        for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
            if obj.data.shape_keys:
                obj.shape_key_clear()
            single_uv(obj)
            parts[obj.name.split(".")[-1]] = obj
        if set(parts) != {"Body", "Claws", "Eyes", "Fur", "Mouth", "Wiskers"}:
            raise RuntimeError(f"unexpected parts {sorted(parts)}")

        name = f"dire_wolf_{skin}"
        rename_materials(parts.values(), skin)
        lods = [build_lod(parts, armature, name if i == 0 else f"{name}.lod{i}", ratio, keep)
                for i, (ratio, keep) in enumerate(LODS)]
        for src in parts.values():
            bpy.data.objects.remove(src, do_unlink=True)
        for lod in lods:
            merge_slots(lod)

        # The saddle goes in one file only: the Kit needs one definition of a mesh name per module.
        saddle_info = None
        if skin == SKINS[0]:
            _, saddle_info = import_saddles(args.saddle, armature, lods[0])

        before = heights(armature, lods[0])
        bones = world_bones(armature)
        offset = tail_offset(tuple(bones["Pelvis"]), tuple(bones["Head"]), args.back)
        scale_rig(armature, args.scale, [o for o in bpy.data.objects if o.type == "MESH"], offset=offset)
        after = heights(armature, lods[0])
        size_problems = [f"{label} z {a:.3f}, want {b * args.scale:.3f}" for label, a, b in
                         zip(("pelvis", "lowest vertex", "highest vertex"), after, before)
                         if abs(a - b * args.scale) > 0.005]
        if any(abs(a - b * args.back) > 1e-4 for a, b in zip(offset, TAIL)):
            size_problems.append(f"the tail is {offset}, not scale_dire_wolf_anims.TAIL times --back")
        if saddle_info is not None:
            saddle = bpy.data.objects["dire_wolf_saddle"]
            tail_y = 1.0 if TAIL[1] > 0 else -1.0  # from TAIL, which the check above ties to the rig: --back 0 is legal
            seat_y, seat_z = seat_point([tuple(saddle.matrix_world @ v.co) for v in saddle.data.vertices], tail_y)
            seat = (-seat_y * tail_y, seat_z)
            want = saddle_info["warg_seat"]
            saddle_info.update(offset=[round(v, 3) for v in offset], wolf_seat=[round(v, 3) for v in seat],
                               warg_seat=[round(v, 3) for v in want])
            if abs(seat[0] - want[0]) > SEAT_TOLERANCE or abs(seat[1] - want[1]) > SEAT_TOLERANCE:
                size_problems.append(f"the seat is at y {seat[0]:.3f} z {seat[1]:.3f}, the warg's at y {want[0]:.3f} "
                                     f"z {want[1]:.3f}")
        out = os.path.join(args.out, f"{name}.fbx")
        want_heads, want_tails = world_bones(armature), world_tails(armature)
        order = [b.name for b in armature.data.bones]
        want_tris = {o.name: tris(o) for o in bpy.data.objects if o.type == "MESH"}
        materials = {o.name: faces_per_material(o) for o in bpy.data.objects if o.type == "MESH"}
        export(out)
        problems = size_problems + gate(out, want_heads, want_tails, order, want_tris, materials, skin == SKINS[0])
        report["skins"][skin] = {"fbx": out, "heights": [round(z, 3) for z in after], "tris": want_tris, "materials": materials, "problems": problems, "saddle": saddle_info,
                                 "mb": round(os.path.getsize(out) / 1048576, 1)}
    render(os.path.join(args.out, f"dire_wolf_{SKINS[0]}.fbx"), os.path.join(args.out, "preview"), 2.2 * args.scale)
    failed = sorted(k for k, s in report["skins"].items() if s["problems"])
    if failed:
        raise RuntimeError(f"the re-import gate failed for {failed}")


if __name__ == "__main__":
    out_dir = flag_value(script_args(), "--out") or os.getcwd()
    with job(os.path.join(out_dir, "prep_report.json")) as job_report:
        main(job_report)
