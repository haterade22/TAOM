"""Measure Animalia (Fab) source clips for their Kit clip metadata (#646): travel per loop, hoof plants, fall time.

Headless (the Store launcher DETACHES: completion is the --out JSON plus its .DONE twin):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/measure_animalia_clips.py -- ^
        --template "C:\\Users\\mikew\\Downloads\\horse.fbx" --map tools/blender/animalia_to_horse_map.json ^
        --clips "E:\\LOTRAOMAssets\\_export\\animalia_elk\\anims\\Animations" ^
        --gaits Loco_Walk Loco_Trot Loco_Run Loco_Sprint Loco_WalkBack --deaths Death_Stand_L Death_Stand_R ^
        --out tools/blender/animalia_elk_clip_measure.json [--profile moose]

Why: a horse gait clip carries a QuadMovementUsage whose LoopDisplacement is the ground the animal covers in one
loop (vanilla horse_walkfast 1.52 m in 0.8 s, anim_horse_trot_2 2.9 m, horse_canterfast 4.0 m), and StepPoints
at its hoof plants (make_walk_sound). The retarget dropped root motion (the UE root is the armature OBJECT), so
the travel comes back here as metadata, the way the troll's did (fab_cave_troll_clip_measure.json).

Frames: the retarget keys source frames f0..f1 as master frames 1..n (n = f1 - f0 + 1, frame 0 = rest) and a
clip plays Source1 = 1 .. Source2 = master Duration - 1, so ONE LOOP = n - 1 frames starting at f0.
  travel      horizontal root (armature object) speed over f0..f1, times the loop length, times the reskin's
              fit scale (the mesh was scaled to horse size by the same global fit), metres per loop
  plants      per hoof (the pack's four ankle joints), the first frame of each ground contact (ankle height
              falls below its minimum + 2 cm) as a fraction of the loop, in [0, 1)
  fall        deaths: the fraction of the clip at which the pelvis first comes within 5 cm of its final height
"""
import argparse
import json
import os
import sys
import traceback

import bpy

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import reskin_animalia_to_horse as rk  # noqa: E402

ANKLES = {"LF": "RigLFLegAnkle", "RF": "RigRFLegAnkle", "LB": "RigLBLegAnkle", "RB": "RigRBLegAnkle"}


def _frame_range(arm):
    ad = arm.animation_data
    if ad and ad.action and getattr(ad, "action_slot", None) is None:
        for s in ad.action.slots:
            ad.action_slot = s
            break
    fr = ad.action.frame_range
    return int(round(fr[0])), int(round(fr[1]))


def _world_head(arm, bone):
    return (arm.matrix_world @ arm.pose.bones[bone].matrix).translation.copy()


def measure(path, template_heads, spec, is_death):
    _, arms, meshes = rk._import_fbx(path)
    arm = arms[0]
    scn = bpy.context.scene
    f0, f1 = _frame_range(arm)
    scn.frame_set(f0)
    S = arm.matrix_world.to_scale()
    heads = {b.name: (rk.Matrix.Diagonal((S.x, S.y, S.z, 1.0)) @ b.matrix_local).translation.copy() for b in arm.data.bones}
    _, fit = rk.global_fit(heads, template_heads, spec["joints"])
    n = f1 - f0 + 1
    loop = n - 1
    root, z = [], {k: [] for k in ANKLES}
    pelvis_z = []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        root.append(arm.matrix_world.translation.copy())
        for k, b in ANKLES.items():
            z[k].append(_world_head(arm, b).z)
        pelvis_z.append(_world_head(arm, "RigPelvis").z)
    d = root[-1] - root[0]
    d.z = 0.0
    speed_per_frame = d.length / max(1, f1 - f0)
    out = {"frames": [f0, f1], "n": n, "loop_frames": loop, "fit_scale": fit["scale"],
           "travel_source_m": round(d.length, 4),
           "loop_displacement_m": round(speed_per_frame * loop * fit["scale"], 4),
           "speed_mps_at_30fps": round(speed_per_frame * 30.0 * fit["scale"], 3)}
    plants = {}
    for k, zs in z.items():
        lo = min(zs[:loop]) + 0.02
        hits = [i for i in range(loop) if zs[i] <= lo and zs[i - 1] > lo]   # i - 1 wraps: a loop
        plants[k] = round(hits[0] / loop, 4) if hits else None
    out["plants"] = plants
    out["plants_sorted"] = sorted(v for v in plants.values() if v is not None)
    if is_death:
        final = pelvis_z[-1]
        hit = next((i for i, v in enumerate(pelvis_z) if v <= final + 0.05), len(pelvis_z) - 1)
        out["fall_fraction"] = round(hit / max(1, loop), 4)
        out["pelvis_z_start_end"] = [round(pelvis_z[0], 3), round(final, 3)]
    for o in arms + meshes:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--template", required=True)
    ap.add_argument("--map", required=True)
    ap.add_argument("--clips", required=True)
    ap.add_argument("--gaits", nargs="*", default=[])
    ap.add_argument("--deaths", nargs="*", default=[])
    ap.add_argument("--profile")
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    report = {"_comment": "tools/blender/measure_animalia_clips.py; keys are the pack's clip stems", "clips": {}}
    try:
        with open(args.map) as fh:
            spec = json.load(fh)
        if args.profile:
            spec["chains"] = spec["profiles"][args.profile]["chains"]
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.render.fps = 30
        _, tarms, tmeshes = rk._import_fbx(args.template)
        template_heads = rk._heads(tarms[0])
        for o in tarms + tmeshes:
            bpy.data.objects.remove(o, do_unlink=True)
        for stem in args.gaits + args.deaths:
            try:
                report["clips"][stem] = measure(os.path.join(args.clips, stem + ".fbx"), template_heads, spec,
                                                stem in args.deaths)
            except Exception:
                report["clips"][stem] = {"error": traceback.format_exc()}
    except Exception:
        report["error"] = traceback.format_exc()
    with open(args.out, "w") as fh:
        json.dump(report, fh, indent=1)
    with open(args.out + ".DONE", "w") as fh:
        fh.write("error" if "error" in report else "ok")


if __name__ == "__main__":
    main()
