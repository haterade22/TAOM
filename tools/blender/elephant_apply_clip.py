"""Key tools/elephant_gait_retarget.py clips onto the elephant rig, render check sheets, export Kit FBX (#1474).

Headless (the Store launcher DETACHES: completion is <out>/_report/DONE.txt):
    blender-launcher.exe -b <elephant work .blend> -P tools/blender/elephant_apply_clip.py -- ^
        --clips <walk.json> [<trot.json> ...] --out <folder> [--render] [--sequences] [--compare <action>] ^
        [--fbx <folder>] [--save-as <new .blend>] [--armor]
Another rig (#1541, the spider): --arm spider_skeleton --harness spider_cfg.py --rest-from-clip. The spider's
clips come from tools/spider_gait.py, whose frame 0 is the ENGINE skeleton's rest pose; --rest-from-clip carries
each bone's motion from that rest pose onto this .blend's own rest frames, so the two need the same rest shape
and not the same bone axes or unit (the spider work scene is in centimetres under an object scale of 0.01: the
unit factor is read from the root bone's rest position).
Read-only on the opened .blend: nothing is saved unless --save-as names a NEW file.

A clip JSON holds, for frame 0 (the rest pose) and frames 1..N, every bone's armature-space 4x4. The bone's
pose basis comes straight from those matrices (no depsgraph update per bone):
    basis = (M0_parent^-1 . M0)^-1 . M_parent^-1 . M        root: basis = M0^-1 . M
The FBX is the armature alone, frames 0..N, take = clip name, armature named <skeleton>_notused, axes as
tools/blender/harness.py export_clip_fbx (the proven June 2026 recipe). Frame 0 is the rest pose because the Kit
stores the root track relative to the master's first frame; the AnimationClip then starts at Source1 = 1.
"""
import argparse
import json
import os
import sys

import bpy
from mathutils import Matrix

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
from blender_job import flag_value, job, report_path, script_args  # noqa: E402

ARM = "elephant_skeleton"


def _mat(v):
    return Matrix([v[0:4], v[4:8], v[8:12], v[12:16]])


def key_clip(arm, clip, rest_from_clip=False):
    bones = arm.data.bones
    names = clip["bones"]
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    old = bpy.data.actions.get(clip["clip"])
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(clip["clip"])
    arm.animation_data_create()
    arm.animation_data.action = act
    m0 = {n: bones[n].matrix_local.copy() for n in names}
    par = {n: (bones[n].parent.name if bones[n].parent else None) for n in names}
    rel_inv = {n: (m0[par[n]].inverted() @ m0[n]).inverted() if par[n] else m0[n].inverted() for n in names}
    last = {}
    worst = 0.0
    undo = {n: _mat(clip["mats"][0][i]).inverted() for i, n in enumerate(names)} if rest_from_clip else None
    unit = m0[names[0]].translation.length / _mat(clip["mats"][0][0]).translation.length if rest_from_clip else 1.0
    for f, row in enumerate(clip["mats"]):
        M = {n: _mat(row[i]) for i, n in enumerate(names)}
        if undo:    # M . undo is the bone's motion from the clip's rest pose, in armature space
            for n in names:
                move = M[n] @ undo[n]
                move.translation = move.translation * unit
                M[n] = move @ m0[n]
        for n in names:
            basis = rel_inv[n] @ (M[par[n]].inverted() @ M[n]) if par[n] else rel_inv[n] @ M[n]
            loc, rot, _ = basis.decompose()
            if n in last and rot.dot(last[n]) < 0:
                rot.negate()
            last[n] = rot.copy()
            pb = arm.pose.bones[n]
            pb.rotation_quaternion = rot
            if par[n]:
                worst = max(worst, loc.length)   # a child bone must not translate: the solve keeps offsets rigid
                pb.location = (0, 0, 0)
            else:
                pb.location = loc
                pb.keyframe_insert("location", frame=f)
            pb.keyframe_insert("rotation_quaternion", frame=f)
    return act, worst


def export_fbx(arm, clip, path, export_name):
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = 0, clip["frames"]
    scene_name, arm_name, data_name = sc.name, arm.name, arm.data.name
    sc.name = clip["clip"]            # the FBX take is the scene's name; the Kit names the master after it
    arm.name = arm.data.name = export_name
    for ob in bpy.data.objects:
        ob.select_set(False)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False,
            primary_bone_axis="Y", secondary_bone_axis="X", axis_forward="-Y", axis_up="Z", bake_anim=True,
            bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0)
    finally:
        sc.name, arm.name, arm.data.name = scene_name, arm_name, data_name
    return os.path.getsize(path)


def make_belt(stride, width=7.0, length=24.0):
    """Dark stripes on the ground, one object. Moved toward +Y by stride / frames each frame (the rig faces -Y),
    they are the ground the clip's travel per loop claims: a planted foot must ride a stripe. The spacing divides
    the stride, so the stripes loop with the clip."""
    spacing = stride / max(1, round(stride / 0.5))
    verts, faces = [], []
    n = int(length / spacing)
    for i in range(-n, n + 1):
        y = i * spacing
        k = len(verts)
        verts += [(-width / 2, y, 0.004), (width / 2, y, 0.004), (width / 2, y + 0.07, 0.004), (-width / 2, y + 0.07, 0.004)]
        faces.append((k, k + 1, k + 2, k + 3))
    me = bpy.data.meshes.new("belt")
    me.from_pydata(verts, [], faces)
    ob = bpy.data.objects.new("belt", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.color = (0.03, 0.03, 0.03, 1.0)
    return ob, spacing


def render_sequences(H, clip, out):
    """One loop of PNG frames per view, with the moving ground: <out>/seq_<clip>_<view>/0001.png ..."""
    sc = bpy.context.scene
    sc.display.shading.color_type = "OBJECT"
    for ob in bpy.data.objects:
        if ob.type == "MESH" and ob.name != "belt":
            ob.color = (0.62, 0.60, 0.57, 1.0)
    g = bpy.data.objects.get("rground")
    if g:
        g.color = (0.30, 0.33, 0.30, 1.0)
    belt, spacing = make_belt(clip["stride_m"])
    n = clip["frames"]
    dirs = {}
    try:
        for view, fn in (("side", "view_side"), ("threeq", "view_3q")):
            H[fn]()
            d = os.path.join(out, "seq_%s_%s" % (clip["clip"], view))
            os.makedirs(d, exist_ok=True)
            for f in range(1, n + 1):
                belt.location.y = ((f - 1) * clip["stride_m"] / n) % spacing
                H["render_frame"](os.path.join(d, "%04d.png" % f), f)
            dirs[view] = d
    finally:
        bpy.data.objects.remove(belt)
    return dirs


def main():
    args = script_args()
    out = flag_value(args, "--out") or "."
    with job(report_path(out)) as rep:
        ap = argparse.ArgumentParser()
        ap.add_argument("--clips", nargs="+", required=True)
        ap.add_argument("--out", required=True)
        ap.add_argument("--render", action="store_true")
        ap.add_argument("--compare", help="an action already in the .blend, rendered beside each clip")
        ap.add_argument("--fbx")
        ap.add_argument("--save-as")
        ap.add_argument("--armor", action="store_true")
        ap.add_argument("--tiles", type=int, default=12)
        ap.add_argument("--arm", default=ARM, help="the armature object; its FBX export name is <arm>_notused")
        ap.add_argument("--harness", default="harness.py", help="the harness layer beside this file to run")
        ap.add_argument("--rest-from-clip", action="store_true")
        ap.add_argument("--sequences", action="store_true",
                        help="render one loop of frames per view over a ground that moves at the clip's stride")
        a = ap.parse_args(args)
        os.makedirs(a.out, exist_ok=True)
        if a.save_as and os.path.abspath(a.save_as) == os.path.abspath(bpy.data.filepath):
            raise SystemExit("--save-as must name a new file")
        arm = bpy.data.objects[a.arm]
        H = {}
        exec(open(os.path.join(TOOLS, a.harness)).read(), H)
        H["RENDER_DIR"] = a.out
        rep["clips"] = {}
        if a.render or a.sequences:
            H["setup_scene"]((640, 400))
            H["show_armor"](a.armor)
        for path in a.clips:
            clip = json.load(open(path))
            act, worst = key_clip(arm, clip, a.rest_from_clip)
            info = {"frames": clip["frames"], "child_translation_m": round(worst, 6), "stride_m": clip["stride_m"]}
            if worst > 1e-3:
                raise RuntimeError("%s: a child bone would translate %.4f m" % (clip["clip"], worst))
            if a.render:
                n = clip["frames"]
                frames = [1 + round(i * n / a.tiles) for i in range(a.tiles)]
                views = [("side", "view_side"), ("front", "view_front")] + ([("top", "view_top")] if "view_top" in H else [])
                for view, fn in views:
                    H[fn]()
                    tiles = [H["render_frame"](os.path.join(a.out, "_t_%s_%s_%03d.png" % (clip["clip"], view, f)), f)
                             for f in (frames[::2] if view == "front" else frames)]
                    sheet = os.path.join(a.out, "%s_%s.png" % (clip["clip"], view))
                    H["montage"](tiles, sheet, cols=6 if view == "side" else 6)
                    for t in tiles:
                        os.remove(t)
                    info[view] = sheet
            if a.sequences:
                info["sequences"] = render_sequences(H, clip, a.out)
            if a.fbx:
                os.makedirs(a.fbx, exist_ok=True)
                fp = os.path.join(a.fbx, clip["clip"] + ".fbx")
                info["fbx"] = fp
                info["fbx_bytes"] = export_fbx(arm, clip, fp, a.arm + "_notused")
            rep["clips"][clip["clip"]] = info
        if a.render and a.compare:
            act = H["set_action"](a.compare)
            n = int(round(act.frame_range[1]))
            H["view_side"]()
            tiles = [H["render_frame"](os.path.join(a.out, "_c_%03d.png" % f), f)
                     for f in [1 + round(i * n / a.tiles) for i in range(a.tiles)]]
            sheet = os.path.join(a.out, "COMPARE_%s_side.png" % a.compare)
            H["montage"](tiles, sheet, cols=6)
            for t in tiles:
                os.remove(t)
            rep["compare"] = sheet
        if a.save_as:
            bpy.ops.wm.save_as_mainfile(filepath=a.save_as, copy=True)
            rep["saved"] = a.save_as


main()
