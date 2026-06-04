"""
creature_anim_ops.py - reusable Blender bpy operations for the TAOM creature animation pipeline.

Runs INSIDE Blender (needs bpy). Load via the Blender MCP / Python console:
    exec(open(r'C:\\Users\\mikew\\source\\repos\\TAOM\\tools\\blender\\creature_anim_ops.py').read(), globals())
then call e.g. audit_actions() or strip_root_motion('sp_walk_left_001').

Blender 5.x slotted-action aware (fcurves live under action.layers[].strips[].channelbag(slot)).
NON-DESTRUCTIVE: mutating ops operate on a COPY (action.copy()) and never save the .blend.
Validated live against ErkamSpider (1).blend (Blender 5.1.2) on 2026-06-02.

Op status (only VALIDATED ops are live; the rest raise NotImplementedError until verified
with fresh in-Blender evidence -- per the pipeline's evidence-over-claims rule):
  audit_actions               VALIDATED  (matches the standalone 34-action audit)
  strip_root_motion           VALIDATED  (copy + flatten root bone .location to frame-0 value)
  find_duplicates/pose_distance  VALIDATED  (variance-weighted; see L2/L3)
  close_loop                  VALIDATED  (snap each fcurve's last key = first key)
  export_all_actions_fbx      VALIDATED  (ALL takes; names are <armatureObject>|<action> -- L11)
  export_bannerlord_fbx       VALIDATED  (single clip via NLA isolation; BARE take name)
  export_bannerlord_clips_fbx VALIDATED  (many clips -> one FBX via NLA strips; BARE take names + rename map; L11)
  mirror_action               TODO  (needs _L<->_R channel swap + X-axis negate; verify before use)
  reverse_action              TODO
  retime_action               TODO
  decimate                    TODO  (keyframe reduction within an error tolerance)
"""
import bpy
import math

ROOT_BONE_DEFAULT = "Root_M"


def _get_fcurves(action):
    """All fcurves of a (slotted) action across its layers/strips/slots. Blender 5.x safe."""
    out = []
    for layer in action.layers:
        for strip in layer.strips:
            if getattr(strip, "type", None) == 'KEYFRAME':
                for slot in action.slots:
                    try:
                        cb = strip.channelbag(slot)
                    except Exception:
                        cb = None
                    if cb is not None:
                        out.extend(list(cb.fcurves))
    return out


def _root_translation(action, root_bone=ROOT_BONE_DEFAULT):
    """[dx,dy,dz] of root_bone.location over the clip (first->last frame). ~0 == in-place."""
    f0, f1 = float(action.frame_range[0]), float(action.frame_range[1])
    d = [0.0, 0.0, 0.0]
    dp_root = 'pose.bones["%s"].location' % root_bone
    for fc in _get_fcurves(action):
        if fc.data_path == dp_root and 0 <= fc.array_index < 3:
            d[fc.array_index] = round(fc.evaluate(f1) - fc.evaluate(f0), 4)
    return d


def audit_actions(names=None):
    """Read-only metrics per action: frames, keyframes, root translation, loop seams, checksum.
    Returns {name: {...}}. Use as a self-check and as the duplicate-detection source
    (equal kf + checksum within noise => near-identical motion)."""
    acts = list(bpy.data.actions) if names is None else [bpy.data.actions[n] for n in names]
    res = {}
    for a in acts:
        f0, f1 = float(a.frame_range[0]), float(a.frame_range[1])
        loc_seam = 0.0
        rot_seam = 0.0
        chk = 0.0
        kf = 0
        root = [0.0, 0.0, 0.0]
        dp_root = 'pose.bones["%s"].location' % ROOT_BONE_DEFAULT
        for fc in _get_fcurves(a):
            dp = fc.data_path or ""
            for k in fc.keyframe_points:
                chk += float(k.co[0]) + float(k.co[1])
                kf += 1
            try:
                v0, v1 = fc.evaluate(f0), fc.evaluate(f1)
            except Exception:
                continue
            d = abs(v1 - v0)
            if dp.endswith(".location"):
                loc_seam += d
                if dp == dp_root and 0 <= fc.array_index < 3:
                    root[fc.array_index] = round(v1 - v0, 4)
            elif "rotation" in dp:
                rot_seam += d
        res[a.name] = {
            "frames": "%d-%d" % (round(f0), round(f1)),
            "kf": kf,
            "root_mag": round(math.sqrt(sum(x * x for x in root)), 3),
            "loc_seam": round(loc_seam, 2),
            "rot_seam": round(rot_seam, 2),
            "chk": round(chk, 1),
        }
    return res


def strip_root_motion(name, root_bone=ROOT_BONE_DEFAULT, suffix="_inplace", overwrite=False):
    """Create an in-place COPY of `name` with the root bone's translation flattened to its
    first-frame value (engine-driven creature locomotion wants root translation ~0, else the
    feet slide). Non-destructive: returns a NEW action `name+suffix`; the original is untouched
    and the .blend is NOT saved. Returns {"src","dst","root_before","root_after","keys_set"}."""
    src = bpy.data.actions.get(name)
    if src is None:
        raise ValueError("No action named %r" % name)
    dst_name = name + suffix
    existing = bpy.data.actions.get(dst_name)
    if existing is not None:
        if not overwrite:
            raise ValueError("%r already exists (pass overwrite=True)" % dst_name)
        bpy.data.actions.remove(existing)
    before = _root_translation(src, root_bone)
    cp = src.copy()
    cp.name = dst_name
    f0 = float(cp.frame_range[0])
    dp_root = 'pose.bones["%s"].location' % root_bone
    keys_set = 0
    for fc in _get_fcurves(cp):
        if fc.data_path == dp_root:
            base = fc.evaluate(f0)
            for k in fc.keyframe_points:
                k.co[1] = base
                keys_set += 1
            fc.update()
    after = _root_translation(cp, root_bone)
    return {"src": name, "dst": dst_name, "root_before": before,
            "root_after": after, "keys_set": keys_set}


def _pose_samples(action, phases=8):
    """{(data_path,array_index): [value at each of `phases` phase-normalized frames]}."""
    f0, f1 = float(action.frame_range[0]), float(action.frame_range[1])
    frames = [f0 + (f1 - f0) * i / (phases - 1) for i in range(phases)]
    return {(fc.data_path, fc.array_index): [fc.evaluate(fr) for fr in frames]
            for fc in _get_fcurves(action)}


def _distance_matrix(names, phases=8, min_std=0.02):
    """Returns (vecs, kept_keys, dist_fn). Variance-weighted: only channels whose global std
    (across the given clips) exceeds min_std are compared, each z-normalized by that std, so a
    bone that swings in one clip but is static in another actually counts. See LESSONS L2/L3:
    naive all-channel RMS is non-discriminative and checksums over-group."""
    import statistics
    acts = [bpy.data.actions[n] for n in names]
    vecs = {a.name: _pose_samples(a, phases) for a in acts}
    keys = set()
    for v in vecs.values():
        keys |= set(v)
    stds = {}
    for k in keys:
        allv = []
        for v in vecs.values():
            if k in v:
                allv.extend(v[k])
        stds[k] = statistics.pstdev(allv) if len(allv) > 1 else 0.0
    kept = [k for k in keys if stds[k] > min_std]

    def dist(n1, n2):
        d1, d2 = vecs[n1], vecs[n2]
        s = 0.0
        cnt = 0
        for k in kept:
            if k in d1 and k in d2:
                sd = stds[k]
                for v1, v2 in zip(d1[k], d2[k]):
                    s += ((v1 - v2) / sd) ** 2
                    cnt += 1
        return round(math.sqrt(s / cnt), 3) if cnt else None

    return vecs, kept, dist


def find_duplicates(names=None, threshold=0.17, phases=8, min_std=0.02):
    """Reliable duplicate detection (VALIDATED iter 4). Use this -- NOT raw checksums, which
    over-group (L3). Returns {"kept_channels","duplicates":[{a,b,dist}<threshold],"closest":[...]}.
    On ErkamSpider: confirms attack_front==attack_front_001 (~0), attack_bottom==bottom_attack_001
    (0.145), charge_001==charge_002 (0.161); separates true dups (<0.17) from variants (0.35+)."""
    nm = [a.name for a in bpy.data.actions] if names is None else list(names)
    _, kept, dist = _distance_matrix(nm, phases, min_std)
    pairs = []
    for i in range(len(nm)):
        for j in range(i + 1, len(nm)):
            pairs.append({"a": nm[i], "b": nm[j], "dist": dist(nm[i], nm[j])})
    pairs.sort(key=lambda x: (x["dist"] is None, x["dist"]))
    return {"kept_channels": len(kept),
            "duplicates": [p for p in pairs if p["dist"] is not None and p["dist"] < threshold],
            "closest": pairs[:10]}


def pose_distance(name_a, name_b, reference_names=None, phases=8, min_std=0.02):
    """Variance-weighted distance between two clips; channel std is normalized over
    `reference_names` (default: just the two). Lower == more similar motion."""
    nm = list(reference_names) if reference_names else []
    for n in (name_a, name_b):
        if n not in nm:
            nm.append(n)
    _, _, dist = _distance_matrix(nm, phases, min_std)
    return dist(name_a, name_b)


def close_loop(name, root_bone=ROOT_BONE_DEFAULT, skip_root_location=True,
               suffix="_loop", overwrite=False):
    """Make a cyclic clip loop seamlessly by snapping each fcurve's LAST keyframe value to its
    FIRST (kills the position pop at the wrap). Skips the root bone's location by default (root
    translation legitimately travels in root-motion clips; in in-place clips it is already
    constant). Non-destructive copy. NOTE: closes the *position* seam; velocity continuity at the
    wrap is a separate concern. Returns before/after location+rotation seam sums."""
    src = bpy.data.actions.get(name)
    if src is None:
        raise ValueError("No action named %r" % name)
    dst = name + suffix
    ex = bpy.data.actions.get(dst)
    if ex is not None:
        if not overwrite:
            raise ValueError("%r already exists (pass overwrite=True)" % dst)
        bpy.data.actions.remove(ex)

    def _seams(a):
        f0, f1 = float(a.frame_range[0]), float(a.frame_range[1])
        loc = rot = 0.0
        for fc in _get_fcurves(a):
            dp = fc.data_path or ""
            try:
                dlt = abs(fc.evaluate(f1) - fc.evaluate(f0))
            except Exception:
                continue
            if dp.endswith(".location"):
                loc += dlt
            elif "rotation" in dp:
                rot += dlt
        return round(loc, 3), round(rot, 3)

    lb, rb = _seams(src)
    cp = src.copy()
    cp.name = dst
    dp_root = 'pose.bones["%s"].location' % root_bone
    closed = 0
    for fc in _get_fcurves(cp):
        if skip_root_location and fc.data_path == dp_root:
            continue
        kps = fc.keyframe_points
        if len(kps) >= 2:
            kps[-1].co[1] = kps[0].co[1]
            fc.update()
            closed += 1
    la, ra = _seams(cp)
    return {"src": name, "dst": dst, "loc_seam_before": lb, "loc_seam_after": la,
            "rot_seam_before": rb, "rot_seam_after": ra, "channels_closed": closed}


def _todo(*args, **kwargs):
    raise NotImplementedError(
        "This op is not yet validated. Implement it and verify with fresh in-Blender "
        "evidence (per the pipeline's evidence-over-claims rule) before shipping it live."
    )


def export_all_actions_fbx(armature_name="sp_skeleton", out_dir=r"E:\LOTRAOMAssets\_auto_workspace",
                           filename="spider_all_anims.fbx", simplify=0.0):
    """Export the armature with ALL its actions as FBX takes (AnimStacks): armature-only (no mesh),
    Bannerlord axes (-Y fwd / Z up / Primary bone axis X / Secondary Y), no leaf bones, baked.
    The Modding Kit imports each take as a separate animation clip (one import -> all clips).

    VALIDATED iter 7 by re-import: produces a 59-bone armature (1 Null + 59 LimbNode, 0 meshes)
    carrying all 34 actions as takes with correct frame ranges + real motion.

    LIMITATION (Blender 5.x slotted actions): `bake_anim_use_all_actions=False` does NOT isolate a
    single action -- the exporter dumps every compatible action regardless. True one-clip-per-FBX
    therefore needs an NLA-isolation pass (see the `export_bannerlord_fbx` TODO). Returns
    {"path","exists","size","bones"}."""
    import os
    arm = bpy.data.objects.get(armature_name)
    if arm is None or arm.type != 'ARMATURE':
        raise ValueError("No armature object %r" % armature_name)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, filename)
    prev_active = bpy.context.view_layer.objects.active
    prev_sel = list(bpy.context.selected_objects)
    try:
        for o in list(bpy.context.selected_objects):
            o.select_set(False)
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'ARMATURE'},
            add_leaf_bones=False, primary_bone_axis='X', secondary_bone_axis='Y',
            axis_forward='-Y', axis_up='Z', bake_anim=True,
            bake_anim_use_all_actions=True, bake_anim_simplify_factor=simplify)
    finally:
        for o in list(bpy.context.selected_objects):
            o.select_set(False)
        for o in prev_sel:
            try:
                o.select_set(True)
            except Exception:
                pass
        bpy.context.view_layer.objects.active = prev_active
    return {"path": path, "exists": os.path.exists(path),
            "size": (os.path.getsize(path) if os.path.exists(path) else 0),
            "bones": len(arm.data.bones)}


def export_bannerlord_fbx(action_name, armature_name="sp_skeleton", out_dir=r"E:\LOTRAOMAssets\_auto_workspace",
                          clip_name=None, simplify=0.0):
    """TRUE single-clip-per-FBX export via NLA isolation (VALIDATED iter 13). Pushes ONE action to a
    single non-muted NLA strip and exports with `bake_anim_use_nla_strips=True`, so the FBX contains
    exactly one take (re-import confirmed 1 take; ~370 KB vs the all-takes 19-36 MB). This is the
    workaround for L6 (slotted actions make `bake_anim_use_all_actions=False` export every take).
    Non-destructive: saves+restores animation_data (action + the NLA track it creates) and selection.
    Returns {action,path,exists,size,bones}."""
    import os
    arm = bpy.data.objects.get(armature_name)
    if arm is None or arm.type != 'ARMATURE':
        raise ValueError("No armature object %r" % armature_name)
    act = bpy.data.actions.get(action_name)
    if act is None:
        raise ValueError("No action %r" % action_name)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, (clip_name or action_name) + ".fbx")
    had_ad = arm.animation_data is not None
    prev_action = arm.animation_data.action if had_ad else None
    prev_active = bpy.context.view_layer.objects.active
    prev_sel = list(bpy.context.selected_objects)
    ad = arm.animation_data_create()
    ad.action = None
    new_track = None
    try:
        new_track = ad.nla_tracks.new()
        new_track.strips.new(clip_name or action_name, int(act.frame_range[0]), act)
        for o in list(bpy.context.selected_objects):
            o.select_set(False)
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'ARMATURE'},
            add_leaf_bones=False, primary_bone_axis='X', secondary_bone_axis='Y',
            axis_forward='-Y', axis_up='Z', bake_anim=True,
            bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
            bake_anim_simplify_factor=simplify)
    finally:
        if new_track is not None:
            try:
                ad.nla_tracks.remove(new_track)
            except Exception:
                pass
        if not had_ad:
            arm.animation_data_clear()
        else:
            arm.animation_data.action = prev_action
        for o in list(bpy.context.selected_objects):
            o.select_set(False)
        for o in prev_sel:
            try:
                o.select_set(True)
            except Exception:
                pass
        bpy.context.view_layer.objects.active = prev_active
    return {"action": action_name, "path": path, "exists": os.path.exists(path),
            "size": (os.path.getsize(path) if os.path.exists(path) else 0),
            "bones": len(arm.data.bones)}


def export_bannerlord_clips_fbx(clip_map, armature_name="sp_skeleton",
                                out_dir=r"E:\LOTRAOMAssets\_auto_workspace",
                                filename="spider_clips.fbx", simplify=0.0):
    """Export MANY clips into ONE FBX with BARE take names, via per-action NLA strips
    (VALIDATED 2026-06-03). `clip_map` = {source_action_name: target_clip_name}; each entry
    becomes one AnimStack whose take name is EXACTLY target_clip_name -- no armature/skeleton
    prefix -- because the NLA export path names takes after the strip (export_fbx_bin.py:2444,
    ref_id=strip), unlike the all-actions path which bakes <armatureObject>|<action> (see L11).

    Use this to build a Modding-Kit-ready Skeleton-Animation FBX: import it, assign the Owner
    Skeleton in the editor, and each take resolves to <OwnerSkeleton>|<target_clip_name>. The
    target names should be the BARE clip names the action_set binds (e.g. 'an_dg_spi_walk').

    Non-destructive: does NOT rename or delete any action; lays down temp NLA tracks (muting any
    pre-existing ones), exports, then removes them and restores animation_data + selection. Sets
    each strip's action_slot so slotted-action channelbags resolve (L11). Returns
    {path,exists,size,bones,clips,missing}.

    VALIDATE every run: re-import take count == len(clip_map); take names bare (re-import shows a
    SINGLE '<armature>.NNN|' prefix, not a double one); byte size non-trivial (an empty bake is a
    tiny file -- cf. L12); per-clip frame-range/value signatures distinct."""
    import os
    arm = bpy.data.objects.get(armature_name)
    if arm is None or arm.type != 'ARMATURE':
        raise ValueError("No armature object %r" % armature_name)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, filename)
    missing = [s for s in clip_map if bpy.data.actions.get(s) is None]

    had_ad = arm.animation_data is not None
    prev_action = arm.animation_data.action if had_ad else None
    prev_active = bpy.context.view_layer.objects.active
    prev_sel = list(bpy.context.selected_objects)
    ad = arm.animation_data_create()
    prev_nla_muted = [(t, t.mute) for t in ad.nla_tracks]
    ad.action = None
    made_tracks = []
    clips = []
    try:
        for t in ad.nla_tracks:          # mute pre-existing tracks so only our strips export
            t.mute = True
        for src_name, clip_name in clip_map.items():
            act = bpy.data.actions.get(src_name)
            if act is None:
                continue
            tr = ad.nla_tracks.new()
            tr.name = clip_name
            st = tr.strips.new(clip_name, int(act.frame_range[0]), act)
            st.name = clip_name          # take name = strip name = bare clip name
            try:
                if act.slots:
                    st.action_slot = act.slots[0]
            except Exception:
                pass
            made_tracks.append(tr)
            clips.append(clip_name)
        for o in list(bpy.context.selected_objects):
            o.select_set(False)
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'ARMATURE'},
            add_leaf_bones=False, primary_bone_axis='X', secondary_bone_axis='Y',
            axis_forward='-Y', axis_up='Z', bake_anim=True,
            bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
            bake_anim_simplify_factor=simplify)
    finally:
        for tr in made_tracks:
            try:
                ad.nla_tracks.remove(tr)
            except Exception:
                pass
        for t, m in prev_nla_muted:
            try:
                t.mute = m
            except Exception:
                pass
        if not had_ad:
            arm.animation_data_clear()
        else:
            arm.animation_data.action = prev_action
        for o in list(bpy.context.selected_objects):
            o.select_set(False)
        for o in prev_sel:
            try:
                o.select_set(True)
            except Exception:
                pass
        bpy.context.view_layer.objects.active = prev_active
    return {"path": path, "exists": os.path.exists(path),
            "size": (os.path.getsize(path) if os.path.exists(path) else 0),
            "bones": len(arm.data.bones), "clips": clips, "missing": missing}


# Still honest placeholders -- implemented + verified in a later iteration.
mirror_action = _todo
reverse_action = _todo
retime_action = _todo
decimate = _todo
