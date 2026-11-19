"""Sample the evaluated deform skeleton of one source elephant scene per frame, for tools/elephant_gait_retarget.py (#1474).

The source is a film rig (552 bones, 208 of them deform bones, driven by constraints and drivers), so its
action cannot be read from fcurves: each frame is evaluated through the dependency graph and the WORLD head,
tail and axes of every deform bone are written out, root motion included.

Headless (the Store launcher DETACHES: completion is <out>/DONE.json). A downloaded .blend is untrusted:
open it with --disable-autoexec, keep this script outside its folder, and never save it.
    blender-launcher.exe -b --disable-autoexec <scene.blend> -P tools/blender/elephant_pack_sample.py -- ^
        --out <folder> [--render 0,20,40]
Writes <out>/sample.json: {"fps", "f0", "f1", "bones", "parents", "rest": {bone: head, tail, rot},
"frames": {frame: {bone: [head xyz, tail xyz, 3x3 row-major]}}} and, with --render, a side and a rear
picture of each named frame, to prove that the rig evaluates in this Blender version with scripts disabled.
"""
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
out = args[args.index("--out") + 1]
render = [int(v) for v in args[args.index("--render") + 1].split(",")] if "--render" in args else []
os.makedirs(out, exist_ok=True)
done = os.path.join(out, "DONE.json")
if os.path.exists(done):
    os.remove(done)
rep = {"status": "running"}
try:
    sc = bpy.context.scene
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    names = [b.name for b in arm.data.bones if b.use_deform] + ["CTR_Root", "CTR_Gravity", "CTR_Spine.Bk", "CTR_Spine.Fr"]
    rep.update({"arm": arm.name, "action": act.name, "f0": f0, "f1": f1, "fps": sc.render.fps, "bones": names,
                "parents": {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones},
                "rest": {}})
    for n in names:
        b = arm.data.bones[n]
        m = arm.matrix_world @ b.matrix_local
        rep["rest"][n] = {"head": [round(v, 5) for v in (arm.matrix_world @ b.head_local)],
                          "tail": [round(v, 5) for v in (arm.matrix_world @ b.tail_local)],
                          "rot": [round(v, 6) for row in m.to_3x3() for v in row]}
    body = bpy.data.objects.get("African_elephant_Body")
    frames = {}
    lows = {}
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        ae = arm.evaluated_get(dg)
        row = {}
        for n in names:
            pb = ae.pose.bones[n]
            m = ae.matrix_world @ pb.matrix
            h = ae.matrix_world @ pb.head
            t = ae.matrix_world @ pb.tail
            row[n] = [round(v, 5) for v in h] + [round(v, 5) for v in t] + [round(v, 6) for r in m.to_3x3() for v in r]
        frames[f] = row
    rep["frames"] = frames
    # mesh ground truth at a few frames: lowest vertex and bounds of the evaluated body
    if body:
        chk = {}
        for f in sorted(set([f0, (f0 + f1) // 2, f1] + render)):
            sc.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            be = body.evaluated_get(dg)
            me = be.to_mesh()
            xs = [be.matrix_world @ v.co for v in me.vertices]
            chk[f] = {"min": [round(min(p[i] for p in xs), 4) for i in range(3)],
                      "max": [round(max(p[i] for p in xs), 4) for i in range(3)]}
            be.to_mesh_clear()
        rep["body_bounds"] = chk
    if render:
        sc.render.engine = "BLENDER_WORKBENCH"
        sc.render.resolution_x, sc.render.resolution_y = 1100, 700
        sc.render.film_transparent = False
        sc.display.shading.light = "STUDIO"
        sc.display.shading.color_type = "SINGLE"
        for o in bpy.data.objects:
            if o.type == "MESH":
                for m in o.modifiers:
                    if m.type == "PARTICLE_SYSTEM":
                        m.show_render = False
        cam_d = bpy.data.cameras.new("chk_cam")
        cam_d.type = "ORTHO"
        cam_d.ortho_scale = 9.0
        cam = bpy.data.objects.new("chk_cam", cam_d)
        sc.collection.objects.link(cam)
        sc.camera = cam
        for f in render:
            sc.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            ae = arm.evaluated_get(dg)
            c = (ae.matrix_world @ ae.pose.bones["DEF_Spine.004"].head)
            for tag, loc, rot in (("side", (c.x + 30, c.y, 1.8), (math.radians(90), 0, math.radians(90))),
                                  ("rear", (c.x, c.y + 30, 1.8), (math.radians(90), 0, math.radians(180)))):
                cam.location = loc
                cam.rotation_euler = rot
                sc.render.filepath = os.path.join(out, "f%04d_%s.png" % (f, tag))
                bpy.ops.render.render(write_still=True)
    json.dump(rep, open(os.path.join(out, "sample.json"), "w"))
    rep = {"status": "ok", "frames": len(frames), "f0": f0, "f1": f1, "body_bounds": rep.get("body_bounds")}
except BaseException:
    rep = {"status": "error", "trace": traceback.format_exc()}
finally:
    json.dump(rep, open(done, "w"), indent=1, default=str)
