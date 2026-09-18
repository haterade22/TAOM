"""Retarget UE4-Mannequin-rigged clips (a Fab creature pack) onto Bannerlord's human_skeleton.

Headless:
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/retarget_mannequin_to_human.py -- ^
        --human "E:\\LOTRAOMAssets\\human_skeleton_with_male_body.fbx" ^
        --clips "E:\\LOTRAOMAssets\\_export\\cave_troll_lightweight\\anims\\CaveTrollLight\\Animations" ^
        --out   "E:\\LOTRAOMAssets\\troll_clips_to_import\\fab_cave_troll" ^
        --preview "E:\\LOTRAOMAssets\\_export\\cave_troll_lightweight\\retarget_preview" ^
        [--name-map tools/blender/fab_cave_troll_clip_names.json] [--keep-root-yaw]
        [--only walk_0 attack_0] [--limit 3] [--strip-prefix cave_troll_]

Clip names: --name-map (source stem -> anim_troll_<what>, the Kit names the resource after the FBX
take, which is the action name at export, so a rename is a re-export); unmapped stems fall back to
troll_<stem minus --strip-prefix>.

The launcher DETACHES (no stdout): completion is <out>/retarget_report.json plus its .DONE twin.

Method (plain bpy, no Auto-Rig Pro): world-space rotation-delta transfer with rest alignment.
  For each mapped pair (source Mannequin bone s, target human bone t):
    D_s(f)  = R_s(f) * inverse(R_s_rest)          world-space rotation of s away from its rest
    S_align = swing rotating t's rest limb direction onto s's rest limb direction (world space),
              so the human skeleton stands in the troll's stance (hunch, bent knees) at rest;
              end bones (head, hands, toes) get identity
    R_t(f)  = D_s(f) * S_align * R_t_rest
  Frame 0 of every export is the REST pose (root turned like the clip), poses start at frame 1: the Kit
  zeroes the root position track at frame 0, so a clip opening on a pose loses its pelvis offset (feet
  skating, Artem 2026-09-18); vanilla masters open on a rest frame and their clips start at Source1 = 1.
  Bone heads follow the HUMAN hierarchy rigidly (human proportions kept), the pelvis translation
  is the source pelvis delta scaled by the pelvis rest-height ratio, and the source armature
  OBJECT's animation (UE root motion lives on the "root" node the importer turns into the object)
  is ignored, which makes every clip in-place. Unmapped human bones (the *_twist1 helpers and
  *_finger0) stay at rest under their parent. Quaternion sign is kept continuous between frames.
  A world-space delta transfer does not depend on either rig's bone-axis convention (Mannequin
  tails point along UE X, Bannerlord's along Y), which is why no bone re-orientation is needed.

Export per clip: armature-only FBX, primary_bone_axis Y / secondary X, forward -Y, up Z, no leaf
bones, baked (step 1, simplify 0), take name = action name, armature named
human_skeleton_notused (lesson L13 + the _notused convention, both verified in game). Output goes
to a NON-Armory staging folder; Kit import is a hand step.

Preview: a Workbench render of the human body at the clip's middle frame, and one of the source
troll mesh at the same frame, side by side in <preview>/<clip>.png (visual check is mandatory;
an fcurve count once hid a fully collapsed rig).
"""
import argparse
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Matrix, Quaternion, Vector

# ---------------------------------------------------------------- bone map
# source (UE4 Mannequin) -> target (Bannerlord human_skeleton, 28 bones)
MANNEQUIN_TO_HUMAN = {
    "pelvis": "pelvis",
    "spine_01": "spine", "spine_02": "spine1", "spine_03": "spine2",
    "neck_01": "neck", "head": "head",
    "clavicle_l": "l_clavicle", "upperarm_l": "l_upperarm_twist", "lowerarm_l": "l_foretwist", "hand_l": "l_hand",
    "clavicle_r": "r_clavicle", "upperarm_r": "r_upperarm_twist", "lowerarm_r": "r_foretwist", "hand_r": "r_hand",
    "thigh_l": "l_thigh", "calf_l": "l_calf", "foot_l": "l_foot", "ball_l": "l_toe0",
    "thigh_r": "r_thigh", "calf_r": "r_calf", "foot_r": "r_foot", "ball_r": "r_toe0",
}
# anatomical "chain child" used for rest alignment (head -> child head direction); end bones absent
TARGET_CHAIN_CHILD = {
    "pelvis": "spine", "spine": "spine1", "spine1": "spine2", "spine2": "neck", "neck": "head",
    "l_clavicle": "l_upperarm_twist", "l_upperarm_twist": "l_foretwist", "l_foretwist": "l_hand",
    "r_clavicle": "r_upperarm_twist", "r_upperarm_twist": "r_foretwist", "r_foretwist": "r_hand",
    "l_thigh": "l_calf", "l_calf": "l_foot", "l_foot": "l_toe0",
    "r_thigh": "r_calf", "r_calf": "r_foot", "r_foot": "r_toe0",
}
SOURCE_CHAIN_CHILD = {
    "pelvis": "spine_01", "spine_01": "spine_02", "spine_02": "spine_03", "spine_03": "neck_01", "neck_01": "head",
    "clavicle_l": "upperarm_l", "upperarm_l": "lowerarm_l", "lowerarm_l": "hand_l",
    "clavicle_r": "upperarm_r", "upperarm_r": "lowerarm_r", "lowerarm_r": "hand_r",
    "thigh_l": "calf_l", "calf_l": "foot_l", "foot_l": "ball_l",
    "thigh_r": "calf_r", "calf_r": "foot_r", "foot_r": "ball_r",
}
PELVIS_T, PELVIS_S = "pelvis", "pelvis"
# bones whose rest DIRECTION is skeleton layout rather than stance: no swing alignment, delta only
NO_ALIGN = {"pelvis", "l_clavicle", "r_clavicle"}
# source bones whose reference pose comes from --ref-clip instead of the bind pose: the trunk and
# head, where the bind pose says nothing about where the character looks. Limbs stay on the bind
# pose: an idle frame has arms doing something (frame 1 of free_idle_0 holds the right forearm
# raised 133 deg), and that offset would ride into every clip as a bent elbow.
CLIP_REF_BONES = {"spine_01", "spine_02", "spine_03", "neck_01", "head"}


# ------------------------------------------------------------------ helpers
def _rot(m):
    """Pure rotation (3x3) of a 4x4 that may carry uniform scale."""
    return m.to_3x3().normalized()


def _hierarchy_order(arm):
    order = []

    def walk(b):
        order.append(b.name)
        for c in b.children:
            walk(c)
    for b in arm.data.bones:
        if b.parent is None:
            walk(b)
    return order


def _import_fbx(path):
    pre = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in pre]
    arms = [o for o in new if o.type == "ARMATURE"]
    return new, (arms[0] if arms else None)


def _assign_slot(obj):
    ad = obj.animation_data
    if ad and ad.action and getattr(ad, "action_slot", None) is None:
        for s in ad.action.slots:
            ad.action_slot = s
            break


def _source_frame_range(arm):
    act = arm.animation_data.action
    fr = act.frame_range
    return int(round(fr[0])), int(round(fr[1]))


def _delete_objects(objs):
    for o in objs:
        data = o.data
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except Exception:
            pass
        try:
            if data is not None and data.users == 0:
                if isinstance(data, bpy.types.Mesh):
                    bpy.data.meshes.remove(data)
                elif isinstance(data, bpy.types.Armature):
                    bpy.data.armatures.remove(data)
        except Exception:
            pass


def _purge_actions(keep):
    for a in list(bpy.data.actions):
        if a.name not in keep and a.users == 0:
            bpy.data.actions.remove(a)


# --------------------------------------------------------------- retarget
class HumanTarget:
    def __init__(self, arm):
        self.arm = arm
        self.Hw = arm.matrix_world.copy()
        self.order = _hierarchy_order(arm)
        self.rest_w = {b.name: self.Hw @ b.matrix_local for b in arm.data.bones}   # world rest 4x4
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
        for pb in arm.pose.bones:
            pb.rotation_mode = "QUATERNION"

    def clear_pose(self):
        for pb in self.arm.pose.bones:
            pb.location = (0.0, 0.0, 0.0)
            pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
            pb.scale = (1.0, 1.0, 1.0)


def _rest_dir(rest_w, chain, bone):
    child = chain.get(bone)
    if child is None or child not in rest_w:
        return None
    d = rest_w[child].translation - rest_w[bone].translation
    return d.normalized() if d.length > 1e-6 else None


SOURCE_FLIP = Matrix.Identity(4)   # set to a 180 deg Z turn in --engine-skeleton mode (Fab faces -Y, the engine +Y)


def _unit_scale_matrix(src_arm):
    """Object animation (root motion) is dropped: keep only the importer's unit scale (and the facing flip)."""
    sc = src_arm.matrix_world.to_scale()
    return SOURCE_FLIP @ Matrix.Diagonal((sc.x, sc.y, sc.z, 1.0))


def _engine_rest_world(bones):
    """bone name -> world rest 4x4 (column-vector) from the engine dump: RestFrame rows are basis vectors
    and M41..M43 the offset, so transpose the 3x3 and move the offset; accumulate down the hierarchy."""
    world = {}
    for b in bones:
        m = b["rest"]
        local = Matrix(((m[0], m[4], m[8], m[12]),
                        (m[1], m[5], m[9], m[13]),
                        (m[2], m[6], m[10], m[14]),
                        (0.0, 0.0, 0.0, 1.0)))
        world[b["name"]] = (world[b["parent"]] @ local) if b["parent"] else local
    return world


def build_engine_rig(json_path, name="human_skeleton_notused"):
    """Build an armature whose every bone has EXACTLY the engine's rest frame (matrix_local == engine world
    rest), object transform identity. The Kit stores an FBX's bone-local transforms verbatim as engine
    locals (measured 2026-09-17: 27 of 28 bones at 0.0 deg, the root off by the armature object's own
    rotation), so authoring on this rig makes the exported clip engine-exact by construction. Bones look
    sideways in Blender (the engine's bone axis is X, Blender draws Y): cosmetic. Returns the object."""
    with open(json_path) as fh:
        spec = json.load(fh)
    bones = spec["bones"]
    world = _engine_rest_world(bones)
    children = {}
    for b in bones:
        if b["parent"]:
            children.setdefault(b["parent"], []).append(b["name"])
    data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for b in bones:
        W = world[b["name"]]
        head = W.translation.copy()
        # length: distance to the first child along the engine bone axis (X), else 0.1 m
        length = 0.1
        for c in children.get(b["name"], []):
            d = (world[c].translation - head).length
            if d > 1e-4:
                length = d
                break
        eb = data.edit_bones.new(b["name"])
        eb.head = head
        eb.tail = head + W.col[1].to_3d().normalized() * length    # Blender bone Y axis := engine frame Y column
        eb.align_roll(W.col[2].to_3d().normalized())                # Blender bone Z axis := engine frame Z column
        eb.use_connect = False
    for b in bones:
        if b["parent"]:
            data.edit_bones[b["name"]].parent = data.edit_bones[b["parent"]]
    bpy.ops.object.mode_set(mode="OBJECT")
    # prove the frames: matrix_local must equal the engine world rest to numerical precision
    worst = 0.0
    for b in bones:
        diff = data.bones[b["name"]].matrix_local - world[b["name"]]
        worst = max(worst, max(abs(v) for row in diff for v in row))
    if worst > 1e-4:
        raise RuntimeError("engine rig frames deviate from the dump by %.2e" % worst)
    return arm


def source_reference(src_arm, frame=None):
    """bone -> world 4x4 of the source at its reference pose: the bind pose (frame None) or the
    evaluated pose at `frame` (e.g. frame 1 of the pack's calm idle). A clip-derived reference
    fixes the end bones: the bind pose says nothing about where a head LOOKS, but in a calm idle
    both characters look ahead, so aligning there needs no hand-tuned offsets."""
    M_fix = _unit_scale_matrix(src_arm)
    if frame is None:
        return {b.name: M_fix @ b.matrix_local for b in src_arm.data.bones}
    _assign_slot(src_arm)
    bpy.context.scene.frame_set(frame)
    return {pb.name: M_fix @ pb.matrix for pb in src_arm.pose.bones}


def _yaw_matrix(src_arm, yaw0):
    """World Z rotation of the source OBJECT relative to its first frame: UE root-motion turns."""
    yaw = src_arm.matrix_world.to_euler().z - yaw0
    return Matrix.Rotation(yaw, 4, "Z")


def retarget_clip(tgt, src_arm, action_name, report, src_ref=None, keep_root_yaw=False, root_world_yaw=None):
    """Bake src_arm's current action onto tgt.arm as a new action `action_name`. Returns (f0, f1).
    keep_root_yaw folds the source object's Z rotation (root-motion turns) into every bone and the
    pelvis position, so a turn clip turns in place; translation is always dropped."""
    scn = bpy.context.scene
    _assign_slot(src_arm)
    f0, f1 = _source_frame_range(src_arm)
    scn.frame_set(f0)
    M_fix = _unit_scale_matrix(src_arm)
    yaw0 = src_arm.matrix_world.to_euler().z
    src_rest_w = src_ref if src_ref is not None else source_reference(src_arm)

    # rest alignment per mapped target bone
    inv_map = {t: s for s, t in MANNEQUIN_TO_HUMAN.items()}
    align = {}
    for t, s in inv_map.items():
        dt = _rest_dir(tgt.rest_w, TARGET_CHAIN_CHILD, t)
        ds = _rest_dir(src_rest_w, SOURCE_CHAIN_CHILD, s)
        if t in NO_ALIGN or dt is None or ds is None:
            align[t] = Quaternion()
        else:
            align[t] = dt.rotation_difference(ds)
    R_s_rest = {s: _rot(src_rest_w[s]) for s in MANNEQUIN_TO_HUMAN if s in src_rest_w}
    missing_src = [s for s in MANNEQUIN_TO_HUMAN if s not in src_rest_w]
    if missing_src:
        report["missing_source_bones"] = missing_src

    k = tgt.rest_w[PELVIS_T].translation.z / src_rest_w[PELVIS_S].translation.z
    p_s_rest = src_rest_w[PELVIS_S].translation.copy()
    p_t_rest = tgt.rest_w[PELVIS_T].translation.copy()

    # fresh target action
    act = bpy.data.actions.new(action_name)
    ad = tgt.arm.animation_data or tgt.arm.animation_data_create()
    ad.action = act
    try:
        slot = act.slots.new(id_type="OBJECT", name=tgt.arm.name)
        ad.action_slot = slot
    except Exception:
        pass
    tgt.clear_pose()

    Hw_inv = tgt.Hw.inverted()
    prev_q = {}
    obj_yaw0 = src_arm.matrix_world.to_euler().z
    pelvis_travel = []
    # Frame 0 is the REST frame, as in every vanilla master (run: 0.6 deg from rest, root position (0,0,0)):
    # the Kit stores the root position track relative to frame 0, so a clip that opens on a posed frame has its
    # pelvis height zeroed at the pose (the troll's hunch sat 9 cm too high, feet off the ground and skating;
    # Artem, 2026-09-18). Children identity; the root gets the same world-Z turn as the posed frames so the
    # Kit's own yaw lands it on the engine rest.
    for t in tgt.order:
        pb = tgt.arm.pose.bones[t]
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        if tgt.parent[t] is None and root_world_yaw is not None:
            M0 = root_world_yaw @ tgt.rest_w[t]
            basis0 = tgt.arm.data.bones[t].matrix_local.inverted() @ (Hw_inv @ M0)
            pb.rotation_quaternion = basis0.to_quaternion()
            pb.location = basis0.translation
            prev_q[t] = basis0.to_quaternion()
        pb.keyframe_insert("rotation_quaternion", frame=0)
        if tgt.parent[t] is None:
            pb.keyframe_insert("location", frame=0)
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        M_f = (_yaw_matrix(src_arm, yaw0) @ M_fix) if keep_root_yaw else M_fix
        Mw = {}      # target world pose 4x4 this frame
        Marm = {}    # armature-space pose 4x4 this frame
        for t in tgt.order:
            par = tgt.parent[t]
            s = inv_map.get(t)
            # rotation
            if s is not None and s in R_s_rest:
                R_s_f = _rot(M_f @ src_arm.pose.bones[s].matrix)
                D = R_s_f @ R_s_rest[s].inverted()
                R_t = D @ align[t].to_matrix() @ _rot(tgt.rest_w[t])
            elif par is not None:
                R_t = _rot(Mw[par]) @ (_rot(tgt.rest_w[par]).inverted() @ _rot(tgt.rest_w[t]))
            else:
                R_t = _rot(tgt.rest_w[t])
            # translation
            if par is None:
                if t == PELVIS_T and PELVIS_S in src_arm.pose.bones:
                    p_s = M_f @ src_arm.pose.bones[PELVIS_S].head
                    head = p_t_rest + (p_s - p_s_rest) * k
                else:
                    head = tgt.rest_w[t].translation.copy()
            else:
                off = tgt.rest_w[par].inverted() @ tgt.rest_w[t].translation
                head = Mw[par] @ off
            if root_world_yaw is not None and (s is not None and s in R_s_rest or par is None):
                # turn the whole character about world Z: every SOURCE-driven bone's world orientation and the
                # root's position. Unmapped helpers (twist1, finger0) are derived from their already-turned
                # parent above, so they must not be turned again; children positions follow through Mw[par].
                R_t = root_world_yaw.to_3x3() @ R_t
                if par is None:
                    head = root_world_yaw @ head
            M = R_t.to_4x4()
            M.translation = head
            Mw[t] = M
            Marm[t] = Hw_inv @ M
            bone = tgt.arm.data.bones[t]
            if par is None:
                basis = bone.matrix_local.inverted() @ Marm[t]
            else:
                pbone = tgt.arm.data.bones[par]
                basis = (Marm[par] @ pbone.matrix_local.inverted() @ bone.matrix_local).inverted() @ Marm[t]
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
        pelvis_travel.append([round(v, 4) for v in Mw[PELVIS_T].translation])
    report["frames"] = [f0, f1]
    report["n_frames"] = f1 - f0 + 1
    report["obj_yaw_delta_deg"] = round(math.degrees(src_arm.matrix_world.to_euler().z - obj_yaw0), 1)
    report["pelvis_world_first_mid_last"] = [pelvis_travel[0], pelvis_travel[len(pelvis_travel) // 2], pelvis_travel[-1]]
    report["pelvis_z_min_max"] = [min(p[2] for p in pelvis_travel), max(p[2] for p in pelvis_travel)]
    report["align_deg"] = {t: round(math.degrees(q.angle), 1) for t, q in align.items()}
    return act, f0, f1


def action_motion_stats(act):
    """Per-bone max quaternion deviation (deg) across the clip, from the slotted action's fcurves."""
    per_bone = {}
    fcs = []
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                fcs.extend(cb.fcurves)
    by_bone = {}
    for fc in fcs:
        if "rotation_quaternion" in fc.data_path:
            bn = fc.data_path.split('"')[1]
            by_bone.setdefault(bn, {})[fc.array_index] = fc
    for bn, comps in by_bone.items():
        if len(comps) < 4:
            continue
        n = len(comps[0].keyframe_points)
        q0 = Quaternion([comps[i].keyframe_points[0].co[1] for i in range(4)])
        mx = 0.0
        for j in range(n):
            q = Quaternion([comps[i].keyframe_points[j].co[1] for i in range(4)])
            mx = max(mx, math.degrees(q0.rotation_difference(q).angle))
        per_bone[bn] = round(mx, 1)
    moving = sum(1 for v in per_bone.values() if v > 1.0)
    return {"fcurves": len(fcs), "bones_moving_over_1deg": moving, "max_dev_deg": per_bone}


# ----------------------------------------------------------------- export
def check_export_against_engine(out_path, engine_json):
    """Re-import the exported FBX and report, at frame 1, each bone's local rotation angle away from the
    engine rest local. The Kit stores FBX locals verbatim, so this is what the engine will play. An idle
    should sit within a few tens of degrees; the FixBoneForBlender rig produced 90 to 180 on most bones."""
    with open(engine_json) as fh:
        bones = json.load(fh)["bones"]
    world = _engine_rest_world(bones)
    parent = {b["name"]: b["parent"] for b in bones}
    pre = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=out_path, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in pre]
    arm = [o for o in new if o.type == "ARMATURE"][0]
    _assign_slot(arm)
    fr = arm.animation_data.action.frame_range
    bpy.context.scene.frame_set(int(fr[0]) + 1)      # the frame after the rest frame (first posed frame)
    rest_dev = {}
    bpy.context.scene.frame_set(int(fr[0]))
    for b in bones:
        n = b["name"]
        if n in arm.pose.bones and parent[n]:
            pose = arm.pose.bones[n].matrix
            ppose = arm.pose.bones[parent[n]].matrix
            local = (ppose.inverted() @ pose).to_3x3().normalized()
            rest_local = (world[parent[n]].inverted() @ world[n]).to_3x3().normalized()
            rest_dev[n] = round(math.degrees((rest_local.inverted() @ local).to_quaternion().angle), 1)
    bpy.context.scene.frame_set(int(fr[0]) + 1)
    devs = {}
    for b in bones:
        n = b["name"]
        if n not in arm.pose.bones:
            continue
        pose = arm.pose.bones[n].matrix            # armature space; children are stored as-is by the Kit
        ppose = arm.pose.bones[parent[n]].matrix if parent[n] else Matrix.Identity(4)
        if parent[n]:
            local = (ppose.inverted() @ pose).to_3x3().normalized()
        else:
            local = (KIT_ROOT_YAW @ arm.matrix_world @ pose).to_3x3().normalized()   # the Kit's root rule
        rest_local = ((world[parent[n]].inverted() if parent[n] else Matrix.Identity(4)) @ world[n]).to_3x3().normalized()
        devs[n] = round(math.degrees((rest_local.inverted() @ local).to_quaternion().angle), 1)
    src_action = arm.animation_data.action if arm.animation_data else None
    _delete_objects(new)
    if src_action is not None:
        try:
            bpy.data.actions.remove(src_action)
        except Exception:
            pass
    vals = list(devs.values())
    return {"max_deg": max(vals), "mean_deg": round(sum(vals) / len(vals), 1), "per_bone": devs,
            "frame0_rest_max_deg": max(rest_dev.values()) if rest_dev else None}   # the rest frame must read ~0


KIT_ROOT_YAW = Matrix.Rotation(math.pi, 4, "Z")
ROOT_YAW_MODE = "pose"   # measured 2026-09-17: kit_root = RotZ(180) @ (FBX world pose of the root)


def export_clip(tgt, act, out_path, root_counter_yaw=False):
    """Armature-only FBX export. With root_counter_yaw the armature OBJECT is turned 180 deg about Z for the
    export only: the Kit stores child bones' FBX locals verbatim but the root as RotZ(180) @ world pose
    (measured on two imports: the FixBoneForBlender rig's -180 Z object rotation cancelled it by accident,
    the identity-object engine rig came back with the pelvis 180 deg off). Turning the object makes the
    Kit's yaw land the pelvis on the engine frame while every bone local stays engine-exact."""
    scn = bpy.context.scene
    N = int(round(act.frame_range[1]))
    scn.frame_start, scn.frame_end = 0, N            # frame 0 is the rest frame the Kit zeroes root motion against
    old_scene_name = scn.name
    scn.name = act.name                       # take name in the FBX
    old_mw = tgt.arm.matrix_world.copy()
    if root_counter_yaw:
        tgt.arm.matrix_world = KIT_ROOT_YAW @ old_mw
    for o in bpy.data.objects:
        try:
            o.select_set(False)
        except Exception:
            pass
    tgt.arm.select_set(True)
    bpy.context.view_layer.objects.active = tgt.arm
    err = None
    try:
        bpy.ops.export_scene.fbx(
            filepath=out_path, use_selection=True, object_types={"ARMATURE"},
            add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
            axis_forward="-Y", axis_up="Z", bake_anim=True, bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    except Exception as e:
        err = str(e)
    tgt.arm.matrix_world = old_mw
    scn.name = old_scene_name
    return {"ok": err is None and os.path.exists(out_path), "err": err,
            "bytes": os.path.getsize(out_path) if os.path.exists(out_path) else 0}


# ---------------------------------------------------------------- preview
def _ensure_camera_light():
    cam = bpy.data.objects.get("_preview_cam")
    if cam is None:
        cd = bpy.data.cameras.new("_preview_cam")
        cam = bpy.data.objects.new("_preview_cam", cd)
        bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    return cam


def _aim(cam, pos, target):
    cam.location = pos
    cam.rotation_euler = (target - pos).to_track_quat("-Z", "Y").to_euler()


def render_pair(tgt, src_arm, src_meshes, frame, out_png, hide_target_meshes_for_source=True):
    """Two Workbench renders (human body / source troll) at `frame`, stitched side by side."""
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.display.shading.light = "STUDIO"
    scn.display.shading.color_type = "MATERIAL"
    scn.render.resolution_x, scn.render.resolution_y = 420, 700
    scn.render.resolution_percentage = 100
    scn.render.film_transparent = False
    cam = _ensure_camera_light()
    scn.frame_set(frame)
    tmp = []
    human_meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.parent == tgt.arm]
    for label, arm, meshes, others in (("human", tgt.arm, human_meshes, src_meshes),
                                       ("troll", src_arm, src_meshes, human_meshes)):
        for o in others:
            o.hide_render = True
        for o in meshes:
            o.hide_render = False
        pel = arm.matrix_world @ arm.pose.bones["pelvis"].head if "pelvis" in arm.pose.bones else arm.matrix_world.translation
        height = 1.9 if label == "human" else 3.2
        target = Vector((pel.x, pel.y, height * 0.5))
        # the human faces engine +Y on the engine rig, else Blender -Y; the Fab source always faces -Y in the
        # scene (the flip is applied in the retarget math, not to the object)
        fwd = (1.0 if (SOURCE_FLIP != Matrix.Identity(4) and ROOT_YAW_MODE == "object") else -1.0) if label == "human" else -1.0
        _aim(cam, target + Vector((height * 1.3, fwd * height * 1.9, height * 0.15)), target)
        cam.data.lens = 40
        p = out_png[:-4] + "_%s.png" % label
        scn.render.filepath = p
        bpy.ops.render.render(write_still=True)
        tmp.append(p)
        for o in others:
            o.hide_render = False
    # stitch
    try:
        a = bpy.data.images.load(tmp[0])
        b = bpy.data.images.load(tmp[1])
        w, h = a.size
        out = bpy.data.images.new("_stitch", w * 2, h)
        pa = list(a.pixels)
        pbx = list(b.pixels)
        rows = []
        for y in range(h):
            rows.extend(pa[y * w * 4:(y + 1) * w * 4])
            rows.extend(pbx[y * w * 4:(y + 1) * w * 4])
        out.pixels = rows
        out.filepath_raw = out_png
        out.file_format = "PNG"
        out.save()
        for im in (a, b, out):
            bpy.data.images.remove(im)
        for p in tmp:
            os.remove(p)
    except Exception:
        pass


# ------------------------------------------------------------------- main
def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--human", required=True)
    ap.add_argument("--clips", required=True, nargs="+", help="FBX files or directories")
    ap.add_argument("--out", required=True)
    ap.add_argument("--preview", default=None)
    ap.add_argument("--only", nargs="*", default=None, help="substrings; a clip is kept if any matches")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--strip-prefix", default="cave_troll_")
    ap.add_argument("--save-blend", default=None)
    ap.add_argument("--ref-clip", default=None, help="FBX whose --ref-frame pose is the source reference (default: bind pose)")
    ap.add_argument("--ref-frame", type=int, default=1)
    ap.add_argument("--name-map", default=None, help="JSON {source stem (lowercase): clip name}; unmapped stems fall back to troll_<stem minus prefix>")
    ap.add_argument("--engine-skeleton", default=None,
                    help="engine skeleton dump JSON (tools/blender/human_skeleton_engine.json): build the target rig from the "
                         "engine's own rest frames instead of the FBX rig; the FBX is then used for its body meshes only")
    ap.add_argument("--root-yaw-mode", choices=("pose", "object"), default="pose",
                    help="how to counter the Kit's 180 deg root yaw in --engine-skeleton mode: 'pose' turns the whole character "
                         "180 deg about world Z in the keyed data (armature node stays identity; default), 'object' rotates the "
                         "armature object for the export only (the node transform then rides into the Kit's Geometry item)")
    ap.add_argument("--keep-root-yaw", action="store_true", help="fold UE root-motion yaw into the clip (turn clips turn in place); actions get a _rootyaw suffix")
    args = ap.parse_args(argv)

    name_map = {}
    if args.name_map:
        with open(args.name_map) as fh:
            name_map = {k.lower(): v for k, v in json.load(fh).items() if not k.startswith("_")}

    clips = []
    for c in args.clips:
        if os.path.isdir(c):
            clips.extend(sorted(os.path.join(c, f) for f in os.listdir(c) if f.lower().endswith(".fbx")))
        else:
            clips.append(c)
    if args.only:
        clips = [c for c in clips if any(k.lower() in os.path.basename(c).lower() for k in args.only)]
    if args.limit:
        clips = clips[:args.limit]
    os.makedirs(args.out, exist_ok=True)
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    new_objs, human_arm = _import_fbx(args.human)
    arms = [o for o in new_objs if o.type == "ARMATURE"]
    # human_skeleton_with_male_body.fbx carries TWO armatures: male_body (the meshes are bound to
    # it) and human_skeleton_notused (the export name the Kit convention wants). Retarget + export
    # the _notused one and rebind the body meshes to it so the preview shows the pose.
    pick = [a for a in arms if a.name.endswith("_notused")]
    human_arm = pick[0] if pick else human_arm
    if human_arm is None:
        raise SystemExit("no armature in human FBX")
    body_meshes = [o for o in new_objs if o.type == "MESH"]   # taken before any object is deleted
    for o in new_objs:
        if o.type == "MESH":
            for md in o.modifiers:
                if md.type == "ARMATURE" and md.object is not human_arm:
                    md.object = human_arm
            if o.parent is not human_arm:
                mw = o.matrix_world.copy()
                o.parent = human_arm
                o.matrix_world = mw
    _delete_objects([a for a in arms if a is not human_arm])
    if human_arm.animation_data:
        human_arm.animation_data_clear()
    if args.engine_skeleton:
        # Target = the engine's rest frames. The FBX rig faces Blender -Y (TpacTool turned the object 180 deg);
        # the engine faces +Y, so turn the body meshes and the Fab source to match.
        global SOURCE_FLIP, ROOT_YAW_MODE
        SOURCE_FLIP = Matrix.Rotation(math.pi, 4, "Z")
        ROOT_YAW_MODE = args.root_yaw_mode
        fbx_arm = human_arm
        human_arm = build_engine_rig(args.engine_skeleton, name=fbx_arm.name)
        turn = Matrix.Rotation(math.pi, 4, "Z")
        for o in body_meshes:
            mw = o.matrix_world.copy()
            o.parent = None
            for md in o.modifiers:
                if md.type == "ARMATURE":
                    md.object = human_arm
            o.parent = human_arm
            o.matrix_parent_inverse = Matrix.Identity(4)
            o.matrix_world = turn @ mw
        _delete_objects([fbx_arm])
    tgt = HumanTarget(human_arm)

    src_ref = None
    if args.ref_clip:
        ref_objs, ref_arm = _import_fbx(args.ref_clip)
        if ref_arm is None:
            raise SystemExit("no armature in --ref-clip")
        clip_ref = source_reference(ref_arm, args.ref_frame)
        bind_ref = source_reference(ref_arm)
        src_ref = dict(bind_ref)
        for b in CLIP_REF_BONES:
            if b in clip_ref:
                src_ref[b] = clip_ref[b]
        ref_action = ref_arm.animation_data.action if ref_arm.animation_data else None
        _delete_objects(ref_objs)
        if ref_action is not None:
            try:
                bpy.data.actions.remove(ref_action)
            except Exception:
                pass

    report = {"human": args.human, "human_armature": human_arm.name, "keep_root_yaw": args.keep_root_yaw,
              "source_reference": (args.ref_clip, args.ref_frame) if args.ref_clip else "bind pose", "clips": {}}
    for path in clips:
        stem = os.path.splitext(os.path.basename(path))[0].lower()
        name = stem[len(args.strip_prefix):] if stem.startswith(args.strip_prefix) else stem
        action_name = name_map.get(stem, "troll_" + name) + ("_rootyaw" if args.keep_root_yaw else "")
        entry = {"source": path, "action": action_name}
        try:
            new_objs, src_arm = _import_fbx(path)
            if src_arm is None or not (src_arm.animation_data and src_arm.animation_data.action):
                raise RuntimeError("source has no armature/action")
            src_meshes = [o for o in new_objs if o.type == "MESH"]
            act, f0, f1 = retarget_clip(tgt, src_arm, action_name, entry, src_ref=src_ref,
                                        keep_root_yaw=args.keep_root_yaw,
                                        root_world_yaw=(KIT_ROOT_YAW if args.engine_skeleton and args.root_yaw_mode == "pose" else None))
            entry["motion"] = action_motion_stats(act)
            if args.preview:
                mid = (f0 + f1) // 2
                # the target action is keyed 1..N; the source at its own frame numbering
                bpy.context.scene.frame_set(mid)
                render_pair(tgt, src_arm, src_meshes, mid, os.path.join(args.preview, action_name + ".png"))
            entry["export"] = export_clip(tgt, act, os.path.join(args.out, action_name + ".fbx"),
                                          root_counter_yaw=bool(args.engine_skeleton) and args.root_yaw_mode == "object")
            if args.engine_skeleton and entry["export"]["ok"]:
                entry["engine_check"] = check_export_against_engine(os.path.join(args.out, action_name + ".fbx"), args.engine_skeleton)
            src_action = src_arm.animation_data.action
            _delete_objects(new_objs)
            try:
                bpy.data.actions.remove(src_action)
            except Exception:
                pass
        except Exception:
            entry["error"] = traceback.format_exc()
        report["clips"][action_name] = entry
        if args.save_blend and action_name in bpy.data.actions:
            bpy.data.actions[action_name].use_fake_user = True   # survive the purge; the WORK blend holds every clip
        _purge_actions(keep={e.get("action") for e in report["clips"].values()})
        with open(os.path.join(args.out, "retarget_report.json"), "w") as fh:
            json.dump(report, fh, indent=1)

    if args.save_blend:
        bpy.ops.wm.save_as_mainfile(filepath=args.save_blend)
    ok = sum(1 for e in report["clips"].values() if e.get("export", {}).get("ok"))
    report["summary"] = {"clips": len(report["clips"]), "exported_ok": ok,
                         "errors": [k for k, e in report["clips"].items() if "error" in e]}
    with open(os.path.join(args.out, "retarget_report.json"), "w") as fh:
        json.dump(report, fh, indent=1)
    open(os.path.join(args.out, "retarget_report.json.DONE"), "w").write("done\n")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        # the launcher detaches, so an uncaught error is otherwise invisible
        _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        _out = _argv[_argv.index("--out") + 1] if "--out" in _argv else os.getcwd()
        os.makedirs(_out, exist_ok=True)
        with open(os.path.join(_out, "retarget_error.log"), "w") as fh:
            fh.write(traceback.format_exc())
        raise
