# Auto-Rig Pro retarget driver for TAOM humanoid races (Blender 5.1.2).
# Re-exec'd each Blender-MCP call:  exec(open(r'E:\repos\TAOM\tools\blender\arp_retarget.py').read(), globals())
#
# PURPOSE: retarget Bannerlord human_skeleton animation clips onto an Auto-Rig Pro
# authoring rig (e.g. the troll's "rig"), so a humanoid RACE can be given a full
# animation set sourced from the human library. PROVEN 2026-06-14: human_skeleton
# anim_walk_forward_unarmed -> troll "rig", 72 moving fcurves baked (clean, no errors).
# Source clips are extracted from animations.tpac by tools/extract_human_anims_tpac.ps1.
#
# ARP is a paid + GPL Blender Market addon: install it separately (we ship an installable
# zip at E:\LOTRAOMAssets\Auto-Rig Pro v3.78.10\auto_rig_pro.zip; do NOT commit ARP source
# into the repo). This driver only CALLS bpy.ops.arp.* and is committable.
#
# Confirmed working on Blender 5.1.2 with ARP 3.78.10 (bl_ext.user_default.auto_rig_pro, 167 ops).
import bpy

# ---- human_skeleton (28-bone Bannerlord source) -> troll ARP control bones (FK chain) ----
# ARP's build_bones_list() auto-guesses targets but gets several WRONG; this is the corrected map.
# (auto-guess errors it fixes: spine/spine1->empty, l_foretwist->c_foot_ik.l, fingers->c_spine_01.x,
#  pelvis->c_root_master.x; all corrected below.)
HUMAN_TO_TROLL_FK = {
    'pelvis': 'c_root.x',                                  # ROOT (set_as_root)
    'spine': 'c_spine_01.x', 'spine1': 'c_spine_02.x', 'spine2': 'c_spine_03.x',
    'neck': 'c_neck.x', 'head': 'c_head.x',
    'l_clavicle': 'c_shoulder.l', 'r_clavicle': 'c_shoulder.r',
    'l_upperarm_twist': 'c_arm_fk.l', 'r_upperarm_twist': 'c_arm_fk.r',
    'l_foretwist': 'c_forearm_fk.l', 'r_foretwist': 'c_forearm_fk.r',
    'l_hand': 'c_hand_fk.l', 'r_hand': 'c_hand_fk.r',
    'l_thigh': 'c_thigh_fk.l', 'r_thigh': 'c_thigh_fk.r',
    'l_calf': 'c_leg_fk.l', 'r_calf': 'c_leg_fk.r',
    'l_foot': 'c_foot_fk.l', 'r_foot': 'c_foot_fk.r',
    'l_toe0': 'c_toes_fk.l', 'r_toe0': 'c_toes_fk.r',
    # twist1 + finger bones are NOT retargeted (auto-driven / unused on the troll).
    # IMPORTANT: leave them OUT of the map entirely -- the retarget() loop REMOVES unmapped
    # bones_map entries. Do NOT add them with an empty '' target: ARP then tries to create a
    # tweak bone for an empty target -> 'NoneType has no attribute name' crash (2026-06-14).
}
ROOT_SOURCE = 'pelvis'


def v3d_override(extra=None):
    """VIEW_3D context for ops that only need a real 3D area (e.g. import). Safe to pin an
    active object here. Do NOT use this for bpy.ops.arp.retarget -- use _ovv() instead."""
    win = bpy.context.window_manager.windows[0]; scr = win.screen
    v3d = [ar for ar in scr.areas if ar.type == 'VIEW_3D']
    area = v3d[0] if v3d else scr.areas[0]
    region = [r for r in area.regions if r.type == 'WINDOW'][0]
    ov = dict(window=win, area=area, region=region)
    if extra:
        ov.update(extra)
    return ov


def _ovv():
    """Override with window/area/region ONLY -- NO active_object pin. CRITICAL for ARP retarget:
    ARP switches the active object internally (set_active_object(source) then (target)); pinning
    active_object via temp_override overrides those switches, so ARP enters edit mode on the WRONG
    armature -> get_edit_bone(source bone) is None -> 'Creating Bones' crash, and the final
    mode_set('POSE') throws 'Toggle Pose Mode' which aborts the unbind and leaves a stuck
    arp_retarget_bound flag (every later bake then re-bakes a static bind). (RCA 2026-06-14.)"""
    win = bpy.context.window_manager.windows[0]; scr = win.screen
    v3d = [ar for ar in scr.areas if ar.type == 'VIEW_3D']
    area = v3d[0] if v3d else scr.areas[0]
    region = [r for r in area.regions if r.type == 'WINDOW'][0]
    return dict(window=win, area=area, region=region)


def import_source_fbx(path):
    """Import a human_skeleton source clip (armature + action). Returns the new armature name."""
    pre = {o.name for o in bpy.data.objects if o.type == 'ARMATURE'}
    with bpy.context.temp_override(**v3d_override()):
        bpy.ops.import_scene.fbx(filepath=path)
    new = [o.name for o in bpy.data.objects if o.type == 'ARMATURE' and o.name not in pre]
    return new[0] if new else None


def _assign_source_slot(src_obj):
    """Blender 5.x slotted-action gotcha: an imported action needs its action_slot assigned, or
    the source armature evaluates to its REST pose during ARP's bake -> a flat (zero-motion)
    retarget. The 198-fcurve _remap looks valid but every curve is constant. (RCA 2026-06-14.)"""
    ad = src_obj.animation_data
    if ad and ad.action and ad.action_slot is None:
        for s in ad.action.slots:
            ad.action_slot = s
            break


def retarget(source_rig, target_rig, frame_start, frame_end,
             mapping=HUMAN_TO_TROLL_FK, root_src=ROOT_SOURCE):
    """Retarget source_rig's active action onto the troll ARP target_rig. Returns the baked
    '<source-action>_remap' action name (or None). Encodes four hard-won fixes (RCA 2026-06-14):
      1. DROP bones_map entries not in `mapping` (do NOT blank them -> ARP empty-target crash).
      2. Assign the source action SLOT (else the bake is flat / zero-motion).
      3. Override with window/area/region ONLY (no active_object pin) so ARP switches the active
         object internally (else eb=None 'Creating Bones' crash + 'Toggle Pose Mode' unbind abort).
      4. Clear a stuck target['arp_retarget_bound'] before binding (a prior aborted unbind leaves
         it True -> 'Already bound' -> ARP skips binding and re-bakes a static pose)."""
    scn = bpy.context.scene
    tgt = bpy.data.objects[target_rig]
    src = bpy.data.objects[source_rig]
    if "arp_retarget_bound" in tgt.keys():
        tgt["arp_retarget_bound"] = False
    _assign_source_slot(src)
    src_action = src.animation_data.action.name if (src.animation_data and src.animation_data.action) else None
    scn.source_rig = source_rig
    scn.target_rig = target_rig
    for o in bpy.data.objects:
        try: o.select_set(False)
        except Exception: pass
    tgt.select_set(True)
    bpy.context.view_layer.objects.active = tgt   # REAL active object (not pinned via override)
    MAP = {k: v for k, v in mapping.items() if v}
    with bpy.context.temp_override(**_ovv()):
        bpy.ops.arp.build_bones_list()
    for i in reversed(range(len(scn.bones_map_v2))):
        it = scn.bones_map_v2[i]
        if it.source_bone in MAP:
            it.name = MAP[it.source_bone]
            try: it.set_as_root = (it.source_bone == root_src)
            except Exception: pass
        else:
            scn.bones_map_v2.remove(i)
    with bpy.context.temp_override(**_ovv()):
        bpy.ops.arp.retarget('EXEC_DEFAULT', frame_start=int(frame_start), frame_end=int(frame_end))
    if src_action:
        rem = src_action + "_remap"
        if rem in bpy.data.actions:
            return rem
    rem = [a for a in bpy.data.actions if a.name.endswith("_remap")]
    return rem[-1].name if rem else None


def action_motion_count(action_name, f0=2, f1=16, f2=30):
    """Count fcurves that actually change across 3 frames -- a flat retarget reports 0.
    Slotted-action aware (reads layers/strips/channelbags when act.fcurves is empty)."""
    a = bpy.data.actions.get(action_name)
    if not a:
        return -1
    fcs = []
    try:
        fcs = list(a.fcurves)
    except Exception:
        pass
    if not fcs:
        for lay in a.layers:
            for st in lay.strips:
                for cb in st.channelbags:
                    fcs.extend(list(cb.fcurves))
    return sum(1 for cv in fcs
               if len({round(cv.evaluate(f0), 4), round(cv.evaluate(f1), 4), round(cv.evaluate(f2), 4)}) > 1)


def retarget_clip(fbx_path, out_action_name, frame_start=1, frame_end=0, target_rig="rig"):
    """One full clip cycle: import source FBX -> retarget -> rename baked action -> protect it
    (fake_user) -> delete the imported source armature + its raw action. Returns a dict report.
    frame_end=0 -> auto from the imported action's range. Verifies motion (>0) before keeping."""
    src = import_source_fbx(fbx_path)
    if not src:
        return {"clip": out_action_name, "ok": False, "err": "no armature imported"}
    so = bpy.data.objects[src]
    raw = so.animation_data.action if (so.animation_data and so.animation_data.action) else None
    if frame_end <= 0 and raw:
        frame_end = int(raw.frame_range[1]) + 1
    rem = retarget(src, target_rig, frame_start, frame_end)
    moving = action_motion_count(rem) if rem else -1
    out = {"clip": out_action_name, "src": src, "remap": rem, "moving": moving}
    if rem and moving > 0:
        if out_action_name in bpy.data.actions:
            bpy.data.actions.remove(bpy.data.actions[out_action_name])
        ra = bpy.data.actions[rem]
        ra.name = out_action_name
        ra.use_fake_user = True
        out["ok"] = True
    else:
        out["ok"] = False
    # cleanup imported source so the next clip starts clean
    if bpy.data.objects.get(src):
        bpy.data.objects.remove(bpy.data.objects[src], do_unlink=True)
    if raw and raw.name in bpy.data.actions:
        try: bpy.data.actions.remove(bpy.data.actions[raw.name])
        except Exception: pass
    return out


def save_bmap(filepath):
    """Save the current bones_map_v2 as an ARP .bmap preset (reusable / re-importable).
    Requires the target rig active (poll). Returns True on success."""
    scn = bpy.context.scene
    tgt = bpy.data.objects[scn.target_rig]
    for o in bpy.data.objects:
        try: o.select_set(False)
        except Exception: pass
    tgt.select_set(True); bpy.context.view_layer.objects.active = tgt
    ov = v3d_override(dict(active_object=tgt, selected_objects=[tgt], object=tgt))
    with bpy.context.temp_override(**ov):
        bpy.ops.arp.export_config(filepath=filepath)
    import os
    return os.path.exists(filepath)


# ---- GAME-ENGINE FBX EXPORT (ARP > Export) ------------------------------------------------
# CONFIRMED working HEADLESS 2026-06-14 via bpy.ops.arp.arp_export_fbx_panel(quick_export=True)
# + the _ovv() override (window/area/region only, NO active_object pin -- the same fix that
# unblocked retarget; quick_export=True bypasses the file dialog). Exports the clean DEFORM
# skeleton (root.x, spine_01.x, foot.l ... -- 30 bones, NO IK/control bones) + the selected
# skinned mesh, ready for Modding-Kit import. Verified: a skeleton-only export re-imported as
# 'skeleton_troll' with 30 deform bones and zero c_*/_ik controls.

def ge_export(filepath, rig="rig", meshes=("troll_hill_body_a.base",), rest_pose_only=True,
              rig_export_name="skeleton_troll"):
    """ARP Game-Engine FBX export of the troll deform skeleton (+ selected meshes) for Kit import.
    rest_pose_only=True detaches any action on the rig first so the FBX is a pure rest-pose
    skeleton (otherwise an assigned clip bakes in -- handy for a skeleton+anim test export).
    Bannerlord-compatible settings: UNIVERSAL rig type, engine OTHERS, primary axis Y / secondary X."""
    scn = bpy.context.scene
    scn.arp_export_rig_type = 'UNIVERSAL'
    scn.arp_engine_type = 'OTHERS'
    scn.arp_bone_axis_primary_export = 'Y'
    scn.arp_bone_axis_secondary_export = 'X'
    scn.arp_ge_sel_only = True
    scn.arp_ge_force_rest_pose_export = True
    scn.arp_export_rig_name = rig_export_name
    r = bpy.data.objects[rig]
    detached = None
    if rest_pose_only and r.animation_data and r.animation_data.action:
        detached = r.animation_data.action.name
        r.animation_data.action = None
    for o in bpy.data.objects:
        try: o.select_set(False)
        except Exception: pass
    r.select_set(True)
    for mn in meshes:
        m = bpy.data.objects.get(mn)
        if m:
            m.select_set(True)
    bpy.context.view_layer.objects.active = r
    with bpy.context.temp_override(**_ovv()):
        bpy.ops.arp.arp_export_fbx_panel(filepath=filepath, quick_export=True, check_existing=False)
    import os
    return {"filepath": filepath, "exists": os.path.exists(filepath),
            "size": (os.path.getsize(filepath) if os.path.exists(filepath) else 0),
            "detached_action": detached}
