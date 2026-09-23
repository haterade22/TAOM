"""Retarget Animalia (Fab) quadruped clips onto the engine horse_skeleton, for the reskinned elk and moose (#646).

Headless (the Store launcher DETACHES: completion is <out>/retarget_report.json plus its .DONE twin):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/retarget_animalia_to_horse.py -- ^
        --template "C:\\Users\\mikew\\Downloads\\horse.fbx" --map tools/blender/animalia_to_horse_map.json ^
        --engine-skeleton tools/blender/horse_skeleton_engine.json ^
        --clips "E:\\LOTRAOMAssets\\_export\\animalia_elk\\anims\\Animations" --prefix animalia_elk_ ^
        --out "E:\\LOTRAOMAssets\\_retarget_out\\animalia_elk" ^
        [--profile moose] [--only Loco_Walk Stand_01] [--exclude REGEX] ^
        [--preview-mesh "E:\\LOTRAOMAssets\\_reskin_out\\animalia_elk\\animalia_elk_08.fbx" --preview <dir>]

The meshes were BENT into the horse rest pose by reskin_animalia_to_horse.py (their own weights kept), so the
retarget must leave a clip's rest frame at the horse rest and carry each bone's motion through the same fit.
The troll's retarget (retarget_mannequin_to_human.py) does the opposite on purpose: it poses the target into
the source's stance at rest (S_align), right for a human body that keeps human proportions, wrong here.

Method, per clip (world space, so neither rig's bone-axis convention matters):
  D_s(f)  = R_s(f) . R_s_rest^-1              pack bone s away from its bind pose (root motion dropped: the
                                             UE root is the armature OBJECT, only its scale is kept; turn
                                             clips fold the object's yaw in, so they turn in place)
  C_s     = Z . Rfit_s                       Rfit_s: the reskin's pure fit rotation for s's region (same map,
                                             same --profile); Z: template (horse.fbx, facing -Y) to engine
  R_t(f)  = C_s . D_s(f) . C_s^-1 . R_t_rest horse bone t driven by s (map 'joints'); the hooves and any
                                             undriven bone keep their rest pose relative to their parent
  pelvis  = rest + Z . Rfit . (pack pelvis offset from bind) . fit scale
Frame 0 is the REST frame (the Kit zeroes root motion against it, Artem 2026-09-18), poses from frame 1, the
Kit's 180 deg root yaw baked into the pose (bannerlord-skeleton-authoring.md). The export comes from a rig
whose depth-first node order equals horse_skeleton's FILE order (horseneck1 under horsetail3, the ram's fix,
transfer_clip_to_engine_rig.py), take name = file stem = the Kit master name, and the re-import is checked for
bone order and frame 0 at rest.

Clip names: <prefix><stem lower-cased> (Loco_Walk -> animalia_elk_loco_walk). Default exclusions: additive
Add_* layers and the sitting / sleeping / swimming set, which no horse action can play (#646 scope).
"""
import argparse
import json
import math
import os
import re
import sys
import traceback

import bpy
from mathutils import Matrix, Vector

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import retarget_mannequin_to_human as rm  # noqa: E402  engine rig, export, helpers
import transfer_clip_to_engine_rig as tc  # noqa: E402  file-order export rig, rekey, re-import check
import reskin_animalia_to_horse as rk  # noqa: E402  the fit the meshes were built with

DEFAULT_EXCLUDE = r"^(Add_|Sitting|Sleeping|Swimming|Loco_Swim|Trans_Sitting|Trans_Sleeping|Trans_Stand_to_Sitting)"
TURN_CLIPS = r"Turn"


def _scale_only(arm):
    sc = arm.matrix_world.to_scale()
    return Matrix.Diagonal((sc.x, sc.y, sc.z, 1.0))


def _rot(m):
    return m.to_3x3().normalized()


def template_to_engine(template_heads, engine_world):
    """Identity or 180 deg about Z, whichever lays the template's bone heads on the engine rest heads."""
    best = None
    for name, Z in (("identity", Matrix.Identity(4)), ("z180", Matrix.Rotation(math.pi, 4, "Z"))):
        d = [((Z @ template_heads[n]) - engine_world[n].translation).length for n in engine_world if n in template_heads]
        mean = sum(d) / len(d)
        if best is None or mean < best[2]["mean_m"]:
            best = (name, Z, {"turn": name, "mean_m": round(mean, 4), "max_m": round(max(d), 4)})
    return best[1], best[2]


def retarget(tgt, src_arm, action_name, spec, template_heads, Z, report, fold_yaw):
    """Bake src_arm's action onto the engine rig as `action_name`. Returns (action, n_posed_frames)."""
    scn = bpy.context.scene
    rm._assign_slot(src_arm)
    f0, f1 = rm._source_frame_range(src_arm)
    scn.frame_set(f0)
    S = _scale_only(src_arm)
    yaw0 = src_arm.matrix_world.to_euler().z
    heads = {b.name: (S @ b.matrix_local).translation.copy() for b in src_arm.data.bones}
    parents = rk._parents(src_arm)
    G, fit = rk.global_fit(heads, template_heads, spec["joints"])
    _, _, Rfit = rk.bone_transforms(heads, parents, template_heads, spec, G)
    Z3 = Z.to_3x3()
    inv = {t: s for s, t in spec["joints"].items() if s in heads}
    C = {s: Z3 @ Rfit[s] for s in inv.values()}
    Cinv = {s: c.inverted() for s, c in C.items()}
    R_s_rest = {s: _rot(S @ src_arm.data.bones[s].matrix_local) for s in inv.values()}
    pelvis_s = inv.get("horsepelvis")
    root = [n for n in tgt.order if tgt.parent[n] is None][0]
    p_s_rest = (S @ src_arm.data.bones[pelvis_s].matrix_local).translation.copy() if pelvis_s else None
    p_t_rest = tgt.rest_w[root].translation.copy()
    kit = rm.KIT_ROOT_YAW
    kit3 = kit.to_3x3()

    act = bpy.data.actions.new(action_name)
    ad = tgt.arm.animation_data or tgt.arm.animation_data_create()
    ad.action = act
    try:
        ad.action_slot = act.slots.new(id_type="OBJECT", name=tgt.arm.name)
    except Exception:
        pass
    tgt.clear_pose()
    Hw_inv = tgt.Hw.inverted()
    prev_q = {}
    # frame 0: REST, the root turned by the Kit yaw like every posed frame
    for t in tgt.order:
        pb = tgt.arm.pose.bones[t]
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        if tgt.parent[t] is None:
            basis0 = tgt.arm.data.bones[t].matrix_local.inverted() @ (Hw_inv @ (kit @ tgt.rest_w[t]))
            pb.rotation_quaternion = basis0.to_quaternion()
            pb.location = basis0.translation
            prev_q[t] = basis0.to_quaternion()
            pb.keyframe_insert("location", frame=0)
        pb.keyframe_insert("rotation_quaternion", frame=0)
    pelvis_z = []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        Y = Matrix.Rotation(src_arm.matrix_world.to_euler().z - yaw0, 4, "Z") if fold_yaw else Matrix.Identity(4)
        M_f = Y @ S
        Mw, Marm = {}, {}
        for t in tgt.order:
            par = tgt.parent[t]
            s = inv.get(t)
            if s is not None:
                D = _rot(M_f @ src_arm.pose.bones[s].matrix) @ R_s_rest[s].inverted()
                R_t = kit3 @ (C[s] @ D @ Cinv[s]) @ _rot(tgt.rest_w[t])
            elif par is not None:
                R_t = _rot(Mw[par]) @ (_rot(tgt.rest_w[par]).inverted() @ _rot(tgt.rest_w[t]))
            else:
                R_t = kit3 @ _rot(tgt.rest_w[t])
            if par is None:
                head = p_t_rest.copy()
                if p_s_rest is not None:
                    d = (M_f @ src_arm.pose.bones[pelvis_s].head) - (Y @ p_s_rest)
                    head += Z3 @ (Rfit[pelvis_s] @ d) * fit["scale"]
                head = kit @ head
            else:
                head = Mw[par] @ (tgt.rest_w[par].inverted() @ tgt.rest_w[t].translation)
            M = R_t.to_4x4()
            M.translation = head
            Mw[t] = M
            Marm[t] = Hw_inv @ M
            bone = tgt.arm.data.bones[t]
            if par is None:
                basis = bone.matrix_local.inverted() @ Marm[t]
            else:
                basis = (Marm[par] @ tgt.arm.data.bones[par].matrix_local.inverted() @ bone.matrix_local).inverted() @ Marm[t]
            q = basis.to_quaternion()
            if t in prev_q and prev_q[t].dot(q) < 0.0:
                q.negate()
            prev_q[t] = q.copy()
            pb = tgt.arm.pose.bones[t]
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f - f0 + 1)
            if par is None:
                pb.location = basis.translation
                pb.keyframe_insert("location", frame=f - f0 + 1)
        pelvis_z.append(Mw[root].translation.z)
    report.update({"frames": [f0, f1], "n_frames": f1 - f0 + 1, "fit": fit, "fold_yaw": fold_yaw,
                   "root_yaw_deg": round(math.degrees(src_arm.matrix_world.to_euler().z - yaw0), 1),
                   "pelvis_z_min_max": [round(min(pelvis_z), 3), round(max(pelvis_z), 3)],
                   "driven": len(inv)})
    return act, f1 - f0 + 1


def export(tgt, spec, act, action_name, n, out_path, entry):
    """File-order export rig, rekey, export under the take name, re-import check (the ram's path)."""
    fparent, changed = tc.order_parents(spec)
    entry["order_reparents"] = changed
    exp_arm = tc.build_export_rig(spec, fparent, spec["skeleton"] + "_notused_export")
    exp_act = tc.rekey_onto_export_rig(tgt, spec, fparent, exp_arm, action_name + "_export", n)
    act.name = action_name + "_engine_rig"          # free the name first: Blender appends .001 on a collision
    exp_act.name = action_name
    if exp_act.name != action_name:
        raise RuntimeError("export action could not take the name %r (got %r)" % (action_name, exp_act.name))

    class _Exp:
        pass

    exp = _Exp()
    exp.arm = exp_arm
    tgt.arm.name = spec["skeleton"] + "_notused_engine"
    exp_arm.name = spec["skeleton"] + "_notused"
    entry["export"] = rm.export_clip(exp, exp_act, out_path)
    if entry["export"]["ok"]:
        entry["engine_check"] = tc.check_export_order_and_locals(out_path, spec, fparent)
    rm._delete_objects([exp_arm])
    for a in (exp_act,):
        try:
            bpy.data.actions.remove(a)
        except Exception:
            pass
    tgt.arm.name = spec["skeleton"] + "_notused"


def load_preview_mesh(path, tgt, Z):
    """The reskinned LOD0 mesh, moved into engine space and bound to the engine rig (its groups are horse bones)."""
    _, arms, meshes = rk._import_fbx(path)
    lod0 = [m for m in meshes if not re.search(r"_lod\d+$", m.name)][0]
    for m in meshes:
        if m is not lod0:
            bpy.data.objects.remove(m, do_unlink=True)
    world = lod0.matrix_world.copy()
    lod0.parent = None
    for md in list(lod0.modifiers):
        lod0.modifiers.remove(md)
    for a in arms:
        bpy.data.objects.remove(a, do_unlink=True)
    lod0.matrix_world = Z @ world
    lod0.parent = tgt.arm
    lod0.matrix_parent_inverse = tgt.arm.matrix_world.inverted()
    md = lod0.modifiers.new("Armature", "ARMATURE")
    md.object = tgt.arm
    return lod0


def render_frames(mesh, frames, out_png_stem):
    scn = bpy.context.scene
    if scn.camera is None:
        scn.render.engine = "BLENDER_WORKBENCH"
        scn.display.shading.light = "STUDIO"
        scn.display.shading.color_type = "SINGLE"
        scn.render.resolution_x, scn.render.resolution_y = 1000, 520
        cam = bpy.data.objects.new("_cam", bpy.data.cameras.new("_cam"))
        scn.collection.objects.link(cam)
        scn.camera = cam
        cam.data.lens = 30
        cam.location = (10.0, 1.75, 1.4)
        cam.rotation_euler = (Vector((0.0, 1.75, 1.1)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    out = []
    for i, f in enumerate(frames):
        scn.frame_set(f)
        p = "%s_%d.png" % (out_png_stem, i)
        scn.render.filepath = p
        bpy.ops.render.render(write_still=True)
        out.append(p)
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--template", required=True)
    ap.add_argument("--map", required=True)
    ap.add_argument("--engine-skeleton", required=True)
    ap.add_argument("--clips", required=True, help="folder of pack clip FBX (each carries the rig)")
    ap.add_argument("--prefix", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--profile")
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--exclude", default=DEFAULT_EXCLUDE)
    ap.add_argument("--preview-mesh")
    ap.add_argument("--preview")
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    out_json = os.path.join(args.out, "retarget_report.json")
    report = {"args": vars(args), "clips": {}}
    try:
        with open(args.map) as fh:
            spec = json.load(fh)
        if args.profile:
            spec["chains"] = spec["profiles"][args.profile]["chains"]
        with open(args.engine_skeleton) as fh:
            eng = json.load(fh)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.render.fps = 30
        _, tarms, tmeshes = rk._import_fbx(args.template)
        template_heads = rk._heads(tarms[0])
        for o in tarms + tmeshes:
            bpy.data.objects.remove(o, do_unlink=True)
        tgt_arm = rm.build_engine_rig(args.engine_skeleton, name=eng["skeleton"] + "_notused")
        tgt = rm.HumanTarget(tgt_arm)
        Z, zinfo = template_to_engine(template_heads, rm._engine_rest_world(eng["bones"]))
        report["template_to_engine"] = zinfo
        preview_mesh = None
        if args.preview_mesh and args.preview:
            os.makedirs(args.preview, exist_ok=True)
            preview_mesh = load_preview_mesh(args.preview_mesh, tgt, Z)
        stems = sorted(os.path.splitext(f)[0] for f in os.listdir(args.clips) if f.lower().endswith(".fbx"))
        if args.only:
            stems = [s for s in stems if s in args.only]
        else:
            stems = [s for s in stems if not re.search(args.exclude, s)]
        report["selected"] = stems
        for stem in stems:
            entry = {}
            name = args.prefix + stem.lower()
            try:
                new_objs, src_arm = rm._import_fbx(os.path.join(args.clips, stem + ".fbx"))
                if src_arm is None or not (src_arm.animation_data and src_arm.animation_data.action):
                    raise RuntimeError("no armature/action")
                src_action = src_arm.animation_data.action
                act, n = retarget(tgt, src_arm, name, spec, template_heads, Z, entry,
                                  fold_yaw=bool(re.search(TURN_CLIPS, stem)))
                tc.per_bone_motion(tgt, n, entry)
                if preview_mesh is not None:
                    # the clip FBX carries the pack's own mesh: park it 3.5 m to the side as the reference, so each
                    # render shows the original clip beside the retarget at the same frame (target frame i = source
                    # frame f0 + i - 1, the posed frames start at 1)
                    holder = bpy.data.objects.new("_src_offset", None)
                    bpy.context.scene.collection.objects.link(holder)
                    holder.location = (0.0, 3.5, 0.0)
                    src_arm.parent = holder
                    new_objs.append(holder)
                    frames = sorted({1, max(1, n // 3), max(1, (2 * n) // 3), n})
                    entry["png"] = render_frames(preview_mesh, frames, os.path.join(args.preview, name))
                export(tgt, eng, act, name, n, os.path.join(args.out, name + ".fbx"), entry)
                rm._delete_objects(new_objs)
                for a in (src_action, act):
                    try:
                        bpy.data.actions.remove(a)
                    except Exception:
                        pass
            except Exception:
                entry["error"] = traceback.format_exc()
            report["clips"][name] = entry
            with open(out_json, "w") as fh:
                json.dump(report, fh, indent=1, default=str)
        ok = [k for k, e in report["clips"].items()
              if e.get("export", {}).get("ok") and e.get("engine_check", {}).get("order_ok")
              and e.get("engine_check", {}).get("frame0_rest_max_deg", 99) < 0.5]
        report["summary"] = {"selected": len(stems), "exported_and_checked": len(ok),
                             "failed": [k for k in report["clips"] if k not in ok]}
    except Exception:
        report["error"] = traceback.format_exc()
    with open(out_json, "w") as fh:
        json.dump(report, fh, indent=1, default=str)
    with open(out_json + ".DONE", "w") as fh:
        fh.write("error" if "error" in report else "ok")


if __name__ == "__main__":
    main()
