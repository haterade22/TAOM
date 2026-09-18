"""Per Fab clip: object (root-motion) travel, yaw, and normalized foot-plant times -> JSON."""
import json
import os
import sys

import bpy
from mathutils import Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
out_path, clip_dir = argv[0], argv[1]
K = 0.915 / 1.181   # human / troll pelvis rest height, same scale the retarget used for translation

report = {}
for fn in sorted(os.listdir(clip_dir)):
    if not fn.lower().endswith(".fbx"):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(clip_dir, fn), automatic_bone_orientation=False)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    act = arm.animation_data.action
    f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
    scn = bpy.context.scene
    scn.frame_set(f0)
    t0 = arm.matrix_world.translation.copy()
    yaw0 = arm.matrix_world.to_euler().z
    sc = arm.matrix_world.to_scale()
    M_fix = Matrix.Diagonal((sc.x, sc.y, sc.z, 1.0))
    ball_l, ball_r, pel = [], [], []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        ball_l.append((M_fix @ arm.pose.bones["ball_l"].head).z)
        ball_r.append((M_fix @ arm.pose.bones["ball_r"].head).z)
        pel.append((M_fix @ arm.pose.bones["pelvis"].head).z)
    scn.frame_set(f1)
    t1 = arm.matrix_world.translation.copy()
    yaw1 = arm.matrix_world.to_euler().z
    n = f1 - f0 + 1
    # foot plants: frames where the foot is within 1.5 cm of its minimum, take the first frame of each plateau
    def plants(z):
        zmin = min(z)
        thr = zmin + 0.015 * (1.181 / 0.915)   # 1.5 cm in troll units
        out, inside = [], False
        for i, v in enumerate(z):
            if v <= thr and not inside:
                out.append(round(i / max(1, n - 1), 3))
                inside = True
            elif v > thr:
                inside = False
        return out
    d = t1 - t0
    report[os.path.splitext(fn)[0].lower()] = {
        "frames": n,
        "travel_xyz_troll_m": [round(v, 3) for v in d],
        "travel_forward_troll_m": round(-d.y, 3),                 # forward is -Y in the import
        "travel_forward_human_m": round(-d.y * K, 3),
        "yaw_deg": round((yaw1 - yaw0) * 57.2958, 1),
        "plants_l": plants(ball_l), "plants_r": plants(ball_r),
        "pelvis_z_troll_first_min_last": [round(pel[0], 3), round(min(pel), 3), round(pel[-1], 3)],
    }
with open(out_path, "w") as f:
    json.dump(report, f, indent=1)
open(out_path + ".DONE", "w").write("done\n")
