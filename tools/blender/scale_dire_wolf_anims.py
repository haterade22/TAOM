"""Scale the ADOD_Beasts wolf's animation FBX files for a Modding Kit import at the dire wolf's size (#1081).

Headless (the Store launcher DETACHES: completion is <out>/scale_anims_report.json, status ok or error):
    blender-launcher.exe -b --factory-startup -P tools/blender/scale_dire_wolf_anims.py -- ^
        --src "E:\\LOTRAOMAssets\\ADOD_Beasts\\AssetSources\\wolves" --out "<folder>" [--scale 2.08] [--back 0.52]

The pack's masters were cooked from these six files, and the clips play them by frame, so each file keeps its name, its
frame range and its one armature take. The mesh objects and their shape-key takes are dropped: an animation import
reads the armature only. Rest bones and location keys scale by --scale (tools/blender/rig_scale.py), and the rest
armature object then moves --back toward the tail, Blender +y, the move prep_dire_wolf_meshes.py gives the meshes so
the saddle sits under the warg's rider clip. The object moves, not the bones, so the move stays constant in model
space while a clip turns the object (rig_scale.move_object). The armature object keeps the pack's 0.01 scale and the export uses prep_dire_wolf_meshes.py's
axes, so the Kit sees the same nodes it read for the meshes.

Each export keeps the pack file's take name (tools/blender/fbx_takes.py). The Kit names a master after its take, a
module holds a name once, and gen_dire_wolf_anim_clips.ps1 finds each master by the pack master's name. The exporter
names its one take after the scene, so six exports at "Scene" made the Kit refuse five imports (2026-11-06).

Gate, per file: the export is imported again and compared with the pack's file at sample frames. Every bone's world
position must be --scale times the pack's plus the move, and its world rotation the pack's, the frame range and the frame rate the
same, the bone order unchanged and the take name the pack's. Any miss fails the run.

The pack's files start at time 0, which Blender's importer shows as frame 1, and its exporter writes frame f at time
f, so the keys move back one frame before the export. Without that every master would start one frame late and each
clip's Source1 and Source2 would cut one frame off (the first gate run, 2026-11-06).
"""
import argparse
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blender_job import flag_value, job, script_args  # noqa: E402
from fbx_takes import file_take_names  # noqa: E402
from rig_scale import fcurves, move_object, scale_rig  # noqa: E402

FILES = ("adod_wolf_armature_unused", "adod_wolf_animations_turn", "adod_wolf_animations_unused", "adod_wolf_gallop",
         "adod_wolf_trot", "adod_wolf_walk")
RIG_SCALE = 2.08  # the same factor as prep_dire_wolf_meshes.RIG_SCALE
RIG_BACK = 0.52  # the same move as prep_dire_wolf_meshes.RIG_BACK, model units toward the tail
TAIL = (0.0, 1.0, 0.0)  # the wolf's tail in Blender world; prep_dire_wolf_meshes.py fails if its rest pose disagrees
SAMPLES = 7
POSITION_TOLERANCE = 0.002  # metres, at the scaled size
ANGLE_TOLERANCE = 0.002  # radians


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument("--src", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--scale", type=float, default=RIG_SCALE)
    p.add_argument("--back", type=float, default=RIG_BACK)
    return p.parse_args(script_args())


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise RuntimeError(f"{path}: {len(arms)} armatures")
    arm = arms[0]
    if arm.animation_data is None or arm.animation_data.action is None:
        raise RuntimeError(f"{path}: the armature has no action")
    return arm


def sample_frames(action):
    start, end = (int(round(f)) for f in action.frame_range)
    step = max(1, (end - start) // (SAMPLES - 1))
    return sorted(set(list(range(start, end + 1, step)) + [end]))


def poses(arm, frames):
    scene = bpy.context.scene
    out = {}
    for f in frames:
        scene.frame_set(f)
        out[f] = {pb.name: (arm.matrix_world @ pb.matrix).copy() for pb in arm.pose.bones}
    return out


def compare(want, got, factor, offset):
    worst_pos, worst_ang, where = 0.0, 0.0, None
    for f, bones in want.items():
        for name, m in bones.items():
            g = got[f][name]
            d = (g.translation - m.translation * factor - offset).length
            a = m.to_quaternion().rotation_difference(g.to_quaternion()).angle
            a = min(a, 2 * math.pi - a)  # q and -q are one rotation
            if d > worst_pos:
                worst_pos, where = d, (f, name)
            worst_ang = max(worst_ang, a)
    return worst_pos, worst_ang, where


def scale_one(src, out, factor, back):
    takes = file_take_names(src)
    if len(takes) != 1:
        raise RuntimeError(f"{src}: {len(takes)} takes {takes}, want one")
    arm = load(src)
    action = arm.animation_data.action
    order = [b.name for b in arm.data.bones]
    frames = sample_frames(action)
    fps = bpy.context.scene.render.fps
    want = poses(arm, frames)
    frame_range = [round(x) for x in action.frame_range]

    for obj in [o for o in bpy.data.objects if o is not arm]:
        bpy.data.objects.remove(obj, do_unlink=True)
    for other in [a for a in bpy.data.actions if a is not action]:
        bpy.data.actions.remove(other)
    offset = tuple(v * back for v in TAIL)
    keys = scale_rig(arm, factor, actions=[action])
    move_object(arm, offset, [action])
    for fc in fcurves(action):
        for kp in fc.keyframe_points:
            kp.co.x -= 1
            kp.handle_left.x -= 1
            kp.handle_right.x -= 1
        fc.update()
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = frame_range[0] - 1, frame_range[1] - 1

    # The Kit names the master after the take, and the exporter names its one take after the scene.
    scene.name = takes[0]
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False,
        primary_bone_axis="Y", secondary_bone_axis="X", axis_forward="-Z", axis_up="Y",
        use_armature_deform_only=False, apply_scale_options="FBX_SCALE_NONE", bake_space_transform=False,
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)

    again = load(out)
    got = poses(again, frames)
    worst_pos, worst_ang, where = compare(want, got, factor, Vector(offset))
    problems = []
    if file_take_names(out) != takes:
        problems.append(f"takes {file_take_names(out)}, pack {takes}")
    if [b.name for b in again.data.bones] != order:
        problems.append("bone order changed")
    if [round(x) for x in again.animation_data.action.frame_range] != frame_range:
        problems.append(f"frame range {list(again.animation_data.action.frame_range)}, pack {frame_range}")
    if bpy.context.scene.render.fps != fps:
        problems.append(f"fps {bpy.context.scene.render.fps}, pack {fps}")
    if worst_pos > POSITION_TOLERANCE:
        problems.append(f"position off by {worst_pos:.4f} m at frame {where[0]}, bone {where[1]}")
    if worst_ang > ANGLE_TOLERANCE:
        problems.append(f"rotation off by {worst_ang:.4f} rad")
    return {"fbx": out, "frames": frame_range, "fps": fps, "location_keys_scaled": keys, "sampled": frames,
            "offset": [round(v, 4) for v in offset],
            "worst_position_m": round(worst_pos, 5), "worst_angle_rad": round(worst_ang, 5),
            "mb": round(os.path.getsize(out) / 1048576, 1), "problems": problems}


def main(report):
    args = parse_args()
    os.makedirs(args.out, exist_ok=True)
    report.update({"blender": bpy.app.version_string, "scale": args.scale, "back": args.back, "files": {}})
    for stem in FILES:
        report["files"][stem] = scale_one(os.path.join(args.src, stem + ".fbx"), os.path.join(args.out, stem + ".fbx"),
                                          args.scale, args.back)
    failed = sorted(k for k, v in report["files"].items() if v["problems"])
    if failed:
        raise RuntimeError(f"the re-import gate failed for {failed}")


if __name__ == "__main__":
    out_dir = flag_value(script_args(), "--out") or os.getcwd()
    with job(os.path.join(out_dir, "scale_anims_report.json")) as job_report:
        main(job_report)
