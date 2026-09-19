"""Transfer a clip authored on a MESH rig onto the ENGINE rig of the SAME skeleton (same bone names).

The case that needed it: the war ram head-butt (2026-08-29) was authored on SK_EB_Goat_A.fbx's rig
(`horse_skeleton_notused`: 39 bones with 7 `*_nub_notused`, `horseneck1` under `horsetail3`, object turned
180 deg about Z) and came out twisted in the Kit through nine export variants. The Kit stores an FBX's
bone-local transforms verbatim, so the rig you export from must carry the engine's frames
(docs/reference/bannerlord-skeleton-authoring.md, facts 1 to 3). A mesh rig gets bone POSITIONS right and
bone FRAMES wrong, so the transfer is done in world space, where the frames cancel:

  1. build the engine rig from tools/blender/<skeleton>_engine.json (retarget_mannequin_to_human.build_engine_rig,
     frames asserted to 1e-4);
  2. measure which world turn (identity or 180 deg about Z) lays the source rig's bind-pose bone heads on the
     engine rest heads, and report the residual (must be millimetres: it is the one thing a mesh rig gets right);
  3. transfer every bone's WORLD rotation delta from its bind pose onto the engine rest (identity name map,
     nubs and any bone the engine lacks are ignored), root translation as the source root's offset, the Kit's
     180 deg root yaw baked into the pose, frame 0 keyed as REST, poses from frame 1
     (retarget_mannequin_to_human.retarget_clip with its maps pointed at the same-name skeleton);
  4. BONE ORDER (measured 2026-09-18 on the ram): the Kit stores a master's bone tracks in FBX NODE order and never
     remaps them to the skeleton, while the engine reads slot i as bone i of the skeleton's FILE order (vanilla horse
     masters are in it). Blender writes nodes depth-first; human_skeleton's file order IS depth-first (so the troll
     never saw this), horse_skeleton's is not (neck listed after the tail): slots 16..31 came back scrambled and the
     ram stood on its head. When the file order is not a depth-first walk, the export comes from a second rig whose
     hierarchy makes the depth-first order equal the file order (horse: horseneck1 hangs off horsetail3, as in
     TaleWorlds' own goat FBX) while every node local stays the ENGINE-parent-relative value the slot must carry;
  5. export armature-only, primary Y / secondary X, and re-import to check frame 0 reads 0 deg from rest and
     report the per-bone deviation at frame 1; also compare target and source REST-RELATIVE world rotations at the
     middle frame (absolute world rotations differ by the mesh-rig-vs-engine frame difference, the deltas must not:
     the transfer is exact by construction, so anything above a fraction of a degree is a bug here), and list the
     bones the clip moves.

The launcher detaches, so nothing prints: read <out>/transfer_report.json (and transfer_error.log on a crash).

  blender-launcher.exe -b -P tools/blender/transfer_clip_to_engine_rig.py -- ^
      --engine-skeleton tools/blender/horse_skeleton_engine.json ^
      --clips E:/LOTRAOMAssets/WarRam/clips/act_war_ram_butt.fbx --out E:/LOTRAOMAssets/WarRam/_engine_rig_out ^
      [--action act_war_ram_butt] [--name war_ram_butt] [--save-blend x.blend]
      [--hold-bone horseneck1 --hold-seconds 2.5]   # repeat the frame where that bone is furthest from rest

The master the Kit compiles is named after the take (the action), the Geometry item after the FILE: keep the
action name of the existing master and the file name of the existing source so a Kit reimport keeps the master
GUID and the clip that points at it (measured on the troll, 2026-09-18).
"""
import bpy, json, math, os, sys, traceback, argparse
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import retarget_mannequin_to_human as rm   # noqa: E402  (the proven troll path; its maps are repointed below)


def _bind_heads(arm):
    return {b.name: b.matrix_local.translation.copy() for b in arm.data.bones}


def choose_source_flip(src_arm, engine_world, report):
    """Pick the world turn that lays the source bind heads on the engine rest heads; report both residuals."""
    heads = _bind_heads(src_arm)
    common = [n for n in engine_world if n in heads]
    cands = {"identity": Matrix.Identity(4), "z180": Matrix.Rotation(math.pi, 4, "Z")}
    res = {}
    for k, M in cands.items():
        d = [((M @ heads[n]) - engine_world[n].translation).length for n in common]
        res[k] = {"mean_m": round(sum(d) / len(d), 4), "max_m": round(max(d), 4)}
    best = min(res, key=lambda k: res[k]["mean_m"])
    report["source_flip"] = {"chosen": best, "residuals": res, "common_bones": len(common),
                             "engine_bones_missing_in_source": [n for n in engine_world if n not in heads],
                             "source_bones_not_in_engine": [n for n in heads if n not in engine_world]}
    return cands[best]


def compare_world_deltas(tgt, src_arm, src_ref, f_src, f_tgt, mapped, report):
    """The transfer invariant: each bone's WORLD rotation relative to its own rest must match between source
    (flipped, then Kit-yawed) and target. Absolute world rotations do NOT match, by exactly the frame difference
    between the mesh rig and the engine (that difference is the whole point of the transfer)."""
    scn = bpy.context.scene
    rm._assign_slot(src_arm)
    scn.frame_set(f_src)
    M_fix = rm._unit_scale_matrix(src_arm)
    yaw = rm.KIT_ROOT_YAW.to_3x3()
    src_delta = {n: yaw @ (rm._rot(M_fix @ src_arm.pose.bones[n].matrix) @ rm._rot(src_ref[n]).inverted()) for n in mapped}
    scn.frame_set(f_tgt)
    devs = {}
    for n in mapped:
        tgt_delta = rm._rot(tgt.Hw @ tgt.arm.pose.bones[n].matrix) @ rm._rot(tgt.rest_w[n]).inverted()
        devs[n] = round(math.degrees((src_delta[n].inverted() @ tgt_delta).to_quaternion().angle), 2)
    report["world_delta_check"] = {"source_frame": f_src, "target_frame": f_tgt,
                                   "max_deg": max(devs.values()), "per_bone": devs}


def per_bone_motion(tgt, n_frames, report):
    """Max local rotation away from rest per bone over the keyed frames 1..n: which bones the clip actually moves."""
    scn = bpy.context.scene
    dev = {pb.name: 0.0 for pb in tgt.arm.pose.bones}
    for f in range(1, n_frames + 1):
        scn.frame_set(f)
        for pb in tgt.arm.pose.bones:
            dev[pb.name] = max(dev[pb.name], math.degrees(pb.rotation_quaternion.angle))
    moving = {k: round(v, 1) for k, v in sorted(dev.items(), key=lambda kv: -kv[1]) if v > 0.5}
    report["bones_moving_deg"] = moving


def order_parents(spec):
    """FBX parent per bone such that a depth-first walk (the order Blender writes FBX nodes, and the order the Kit
    stores a master's bone tracks in: measured 2026-09-18, it does not remap to the skeleton) equals the skeleton's
    FILE order (the order the engine reads tracks in: vanilla horse masters are in it). Bones stay under their
    engine parent while that parent is on the current depth-first path; otherwise they hang off the previous bone.
    horse_skeleton: only horseneck1 moves (under horsetail3, exactly what TaleWorlds' own goat FBX has)."""
    names = [b["name"] for b in spec["bones"]]
    eparent = {b["name"]: b["parent"] for b in spec["bones"]}
    if eparent[names[0]] is not None:
        raise ValueError("skeleton list must start with a root bone; %r has parent %r" % (names[0], eparent[names[0]]))
    fparent, changed, stack = {}, {}, []
    for n in names:
        p = eparent[n]
        if p is None:
            fparent[n] = None
            stack = [n]
            continue
        if p in stack:
            while stack[-1] != p:
                stack.pop()
            fparent[n] = p
        else:
            fparent[n] = stack[-1]
            changed[n] = stack[-1]
        stack.append(n)
    return fparent, changed


def build_export_rig(spec, fparent, name):
    """Armature with the ORDER hierarchy (fparent) whose node locals equal the ENGINE locals: rest(n) = rest(fparent) @
    engine_local(n). Blender draws a reparented bone in the wrong place; the exported numbers are what matter."""
    bones = spec["bones"]
    local = {}
    for b in bones:
        m = b["rest"]
        local[b["name"]] = Matrix(((m[0], m[4], m[8], m[12]), (m[1], m[5], m[9], m[13]),
                                   (m[2], m[6], m[10], m[14]), (0.0, 0.0, 0.0, 1.0)))
    W = {}
    for b in bones:
        n = b["name"]
        W[n] = (W[fparent[n]] @ local[n]) if fparent[n] else local[n]
    children = {}
    for b in bones:
        if fparent[b["name"]]:
            children.setdefault(fparent[b["name"]], []).append(b["name"])
    data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for b in bones:
        n = b["name"]
        head = W[n].translation.copy()
        length = 0.1
        for c in children.get(n, []):
            d = (W[c].translation - head).length
            if d > 1e-4:
                length = d
                break
        eb = data.edit_bones.new(n)
        eb.head = head
        eb.tail = head + W[n].col[1].to_3d().normalized() * length
        eb.align_roll(W[n].col[2].to_3d().normalized())
        eb.use_connect = False
    for b in bones:
        if fparent[b["name"]]:
            data.edit_bones[b["name"]].parent = data.edit_bones[fparent[b["name"]]]
    bpy.ops.object.mode_set(mode="OBJECT")
    worst = 0.0
    for b in bones:
        diff = data.bones[b["name"]].matrix_local - W[b["name"]]
        worst = max(worst, max(abs(v) for row in diff for v in row))
    if worst > 1e-4:
        raise RuntimeError("export rig frames deviate by %.2e" % worst)
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


def rekey_onto_export_rig(tgt, spec, fparent, exp_arm, act_name, n_frames, hold_at=None, hold_frames=0):
    """Copy the keyed clip from the engine rig onto the export rig so that every node's FBX-parent-relative local
    equals the engine-parent-relative local at every frame (frame 0 = rest .. n_frames). With hold_frames > 0 the
    pose at frame hold_at is repeated that many frames before the clip continues (a head kept low after a butt).
    Returns the new action."""
    scn = bpy.context.scene
    names = [b["name"] for b in spec["bones"]]
    eparent = {b["name"]: b["parent"] for b in spec["bones"]}
    act = bpy.data.actions.new(act_name)
    ad = exp_arm.animation_data or exp_arm.animation_data_create()
    ad.action = act
    try:
        ad.action_slot = act.slots.new(id_type="OBJECT", name=exp_arm.name)
    except Exception:
        pass
    prev_q = {}
    Hw_inv = exp_arm.matrix_world.inverted()
    for f_out in range(0, n_frames + hold_frames + 1):
        if hold_at is None or f_out <= hold_at:
            f_src = f_out
        elif f_out <= hold_at + hold_frames:
            f_src = hold_at
        else:
            f_src = f_out - hold_frames
        f = f_out
        scn.frame_set(f_src)
        Mt = {n: tgt.Hw @ tgt.arm.pose.bones[n].matrix for n in names}        # engine rig, world
        L = {n: ((Mt[eparent[n]].inverted() @ Mt[n]) if eparent[n] else Mt[n]) for n in names}   # engine locals
        Me = {}
        for n in names:                                                        # file order is parent-first for fparent
            Me[n] = (Me[fparent[n]] @ L[n]) if fparent[n] else L[n]
            bone = exp_arm.data.bones[n]
            Marm = Hw_inv @ Me[n]
            if fparent[n]:
                pbone = exp_arm.data.bones[fparent[n]]
                basis = ((Hw_inv @ Me[fparent[n]]) @ pbone.matrix_local.inverted() @ bone.matrix_local).inverted() @ Marm
            else:
                basis = bone.matrix_local.inverted() @ Marm
            q = basis.to_quaternion()
            if n in prev_q and prev_q[n].dot(q) < 0.0:
                q.negate()
            prev_q[n] = q.copy()
            pb = exp_arm.pose.bones[n]
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if fparent[n] is None:
                pb.location = basis.translation
                pb.keyframe_insert("location", frame=f)
    return act


def check_export_order_and_locals(out_path, spec, fparent):
    """Re-import the export: the bone ORDER must be the skeleton's file order, and every node's local (relative to its
    FBX parent) must equal the engine local (relative to the engine parent): 0 deg at frame 0, reported at frame 1."""
    names = [b["name"] for b in spec["bones"]]
    eparent = {b["name"]: b["parent"] for b in spec["bones"]}
    world = rm._engine_rest_world(spec["bones"])
    pre = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=out_path, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in pre]
    arm = [o for o in new if o.type == "ARMATURE"][0]
    rm._assign_slot(arm)
    got = [b.name for b in arm.data.bones]
    fr = arm.animation_data.action.frame_range
    out = {"order_ok": got == names, "first_order_diff": next((i for i, (a, b) in enumerate(zip(got, names)) if a != b), None),
           "fbx_parents_changed": {n: p for n, p in fparent.items() if p != eparent[n]}}
    for label, f in (("frame0_rest_max_deg", int(fr[0])), ("frame1_max_deg", int(fr[0]) + 1)):
        bpy.context.scene.frame_set(f)
        devs = {}
        for n in names:
            pb = arm.pose.bones[n]
            if fparent[n]:
                node_local = (arm.pose.bones[fparent[n]].matrix.inverted() @ pb.matrix).to_3x3().normalized()
                eng_local = (world[eparent[n]].inverted() @ world[n]).to_3x3().normalized()
            else:
                node_local = (rm.KIT_ROOT_YAW @ arm.matrix_world @ pb.matrix).to_3x3().normalized()
                eng_local = world[n].to_3x3().normalized()
            devs[n] = round(math.degrees((eng_local.inverted() @ node_local).to_quaternion().angle), 2)
        out[label] = max(devs.values())
        if label == "frame1_max_deg":
            out["frame1_per_bone_over_1deg"] = {k: v for k, v in devs.items() if v > 1.0}
    src_action = arm.animation_data.action
    rm._delete_objects(new)
    try:
        bpy.data.actions.remove(src_action)
    except Exception:
        pass
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--engine-skeleton", required=True, help="tools/blender/<skeleton>_engine.json")
    ap.add_argument("--clips", required=True, nargs="+", help="source FBX files (mesh rig, same bone names)")
    ap.add_argument("--out", required=True)
    ap.add_argument("--action", default=None, help="action (take) name for the export; default: the source action's name")
    ap.add_argument("--name", default=None, help="output FBX stem; default: the source file's stem")
    ap.add_argument("--save-blend", default=None)
    ap.add_argument("--hold-bone", default=None, help="bone whose most-deviated frame is held (e.g. horseneck1)")
    ap.add_argument("--hold-seconds", type=float, default=0.0, help="repeat the hold frame this long (30 fps) before the clip continues")
    ap.add_argument("--hold-at", type=int, default=None, help="hold at this keyed frame instead of the auto-detected one")
    args = ap.parse_args(argv)
    if args.hold_seconds > 0 and not (args.hold_bone or args.hold_at is not None):
        # RuntimeError, not SystemExit: the launcher detaches, and only an Exception reaches transfer_error.log
        raise RuntimeError("--hold-seconds needs --hold-bone or --hold-at")
    os.makedirs(args.out, exist_ok=True)

    with open(args.engine_skeleton) as fh:
        spec = json.load(fh)
    engine_names = [b["name"] for b in spec["bones"]]
    root = [b["name"] for b in spec["bones"] if not b["parent"]][0]
    engine_world = rm._engine_rest_world(spec["bones"])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    tgt_arm = rm.build_engine_rig(args.engine_skeleton, name=spec["skeleton"] + "_notused")
    tgt = rm.HumanTarget(tgt_arm)

    # point the troll path at a same-name skeleton: identity map, no swing alignment, the engine root as pelvis
    rm.TARGET_CHAIN_CHILD = {}
    rm.SOURCE_CHAIN_CHILD = {}
    rm.NO_ALIGN = set()
    rm.PELVIS_T = rm.PELVIS_S = root
    rm.ROOT_YAW_MODE = "pose"

    report = {"engine_skeleton": args.engine_skeleton, "skeleton": spec["skeleton"], "root": root, "clips": {}}
    for path in args.clips:
        stem = os.path.splitext(os.path.basename(path))[0]
        entry = {"source": path}
        try:
            new_objs, src_arm = rm._import_fbx(path)
            if src_arm is None or not (src_arm.animation_data and src_arm.animation_data.action):
                raise RuntimeError("source has no armature/action")
            src_action = src_arm.animation_data.action
            action_name = args.action or src_action.name.split("|")[-1]
            out_stem = args.name or stem
            entry.update({"action": action_name, "source_action": src_action.name,
                          "source_bones": len(src_arm.data.bones),
                          "source_object_rot_deg": [round(math.degrees(v), 1) for v in src_arm.matrix_world.to_euler()]})
            rm.SOURCE_FLIP = choose_source_flip(src_arm, engine_world, entry)
            mapped = [n for n in engine_names if n in src_arm.data.bones]
            rm.MANNEQUIN_TO_HUMAN = {n: n for n in mapped}
            src_ref = rm.source_reference(src_arm)          # bind pose, flipped
            act, f0, f1 = rm.retarget_clip(tgt, src_arm, action_name, entry, src_ref=src_ref,
                                           keep_root_yaw=False, root_world_yaw=rm.KIT_ROOT_YAW)
            entry["motion"] = rm.action_motion_stats(act)
            per_bone_motion(tgt, f1 - f0 + 1, entry)
            mid = (f0 + f1) // 2
            compare_world_deltas(tgt, src_arm, src_ref, mid, mid - f0 + 1, mapped, entry)
            out_path = os.path.join(args.out, out_stem + ".fbx")
            fparent, changed = order_parents(spec)
            entry["order_reparents"] = changed
            hold_at, hold_frames = None, 0
            if args.hold_seconds > 0:
                hold_frames = int(round(args.hold_seconds * 30))
                if args.hold_at is not None:
                    hold_at = args.hold_at
                else:
                    # the frame where the hold bone is furthest from rest (a head at the bottom of its butt)
                    best = (0.0, 1)
                    pb = tgt.arm.pose.bones[args.hold_bone]
                    for f in range(1, f1 - f0 + 2):
                        bpy.context.scene.frame_set(f)
                        a = math.degrees(pb.rotation_quaternion.angle)
                        if a > best[0]:
                            best = (a, f)
                    hold_at = best[1]
                entry["hold"] = {"bone": args.hold_bone, "at_frame": hold_at, "frames": hold_frames,
                                 "seconds": args.hold_seconds, "total_frames_incl_rest": f1 - f0 + 1 + hold_frames + 1}
            if changed or hold_frames:
                # the skeleton's file order is not a depth-first walk of its hierarchy: export from a rig whose
                # hierarchy makes Blender's depth-first node order equal the file order, node locals = engine locals
                exp_arm = build_export_rig(spec, fparent, spec["skeleton"] + "_notused_export")
                exp_act = rekey_onto_export_rig(tgt, spec, fparent, exp_arm, action_name + "_export", f1 - f0 + 1,
                                                hold_at=hold_at, hold_frames=hold_frames)
                act.name = action_name + "_engine_rig"     # free the name FIRST: Blender appends .001 on a collision
                exp_act.name = action_name                  # the take name is the master name in the Kit
                if exp_act.name != action_name:
                    raise RuntimeError("export action could not take the name %r (got %r)" % (action_name, exp_act.name))
                class _Exp:
                    pass
                exp = _Exp()
                exp.arm = exp_arm
                tgt.arm.name = spec["skeleton"] + "_notused_engine"      # free the name first, or Blender appends .001
                exp_arm.name = spec["skeleton"] + "_notused"
                entry["export"] = rm.export_clip(exp, exp_act, out_path)
                if entry["export"]["ok"]:
                    entry["engine_check"] = check_export_order_and_locals(out_path, spec, fparent)
                rm._delete_objects([exp_arm])
                try:
                    bpy.data.actions.remove(exp_act)
                except Exception:
                    pass
                # restore names only once the export rig and its action are gone, or these collide and get .001
                tgt.arm.name = spec["skeleton"] + "_notused"
                act.name = action_name
            else:
                if act.name != action_name:      # the take name is the master name in the Kit (the .001 trap)
                    raise RuntimeError("export action could not take the name %r (got %r)" % (action_name, act.name))
                entry["export"] = rm.export_clip(tgt, act, out_path)
                if entry["export"]["ok"]:
                    entry["engine_check"] = rm.check_export_against_engine(out_path, args.engine_skeleton)
                    entry["engine_check"]["order_ok"] = True
            rm._delete_objects(new_objs)
            try:
                bpy.data.actions.remove(src_action)
            except Exception:
                pass
        except Exception:
            entry["error"] = traceback.format_exc()
        report["clips"][stem] = entry
        if args.save_blend and entry.get("action") in bpy.data.actions:
            bpy.data.actions[entry["action"]].use_fake_user = True
        with open(os.path.join(args.out, "transfer_report.json"), "w") as fh:
            json.dump(report, fh, indent=1)

    if args.save_blend:
        bpy.ops.wm.save_as_mainfile(filepath=args.save_blend)
    report["summary"] = {"clips": len(report["clips"]),
                         "exported_ok": sum(1 for e in report["clips"].values() if e.get("export", {}).get("ok")),
                         "errors": [k for k, e in report["clips"].items() if "error" in e]}
    with open(os.path.join(args.out, "transfer_report.json"), "w") as fh:
        json.dump(report, fh, indent=1)
    open(os.path.join(args.out, "transfer_report.json.DONE"), "w").write("done\n")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        _out = _argv[_argv.index("--out") + 1] if "--out" in _argv else os.getcwd()
        os.makedirs(_out, exist_ok=True)
        with open(os.path.join(_out, "transfer_error.log"), "w") as fh:
            fh.write(traceback.format_exc())
        raise
