# Elephant animation refinement harness (Blender 5.1.2, slotted-action API)
# Re-exec'd each MCP call:  exec(open(r'E:\LOTRAOMAssets\Elephant\_refine_tools\harness.py').read(), globals())
import bpy, os, math
from mathutils import Vector, Euler, Quaternion

ARM_NAME = 'elephant_skeleton'
BODY_NAME = 'elephant_mesh'
RENDER_DIR = r'E:\LOTRAOMAssets\Elephant\_refine_renders'
ARMOR = ['SK_Elephant_Armor_A.base','SK_Elephant_Armor_A.belt','SK_Elephant_Armor_A.cloth',
         'SK_Elephant_Armor_A.feather','SK_Elephant_Armor_A.head','SK_Elephant_Armor_A.legs',
         'SK_Elephant_Armor_A.nose','SK_Elephant_Armor_A.pillow','SK_Elephant_Armor_A.platform',
         'SK_Elephant_Armor_A.tusk']

# Foot / key bones (exact names, leading spaces preserved)
FEET = {'FL': ' L Hand_034', 'FR': ' R Hand_030', 'BL': ' L Foot_037', 'BR': ' R Foot_040'}
ROOT = ' Pelvis_03'

os.makedirs(RENDER_DIR, exist_ok=True)

def arm():
    return bpy.data.objects.get(ARM_NAME)

def set_action(action_name):
    """Make an action active on the rig, binding its first slot (slotted-action requirement)."""
    o = arm()
    if o.animation_data is None:
        o.animation_data_create()
    act = bpy.data.actions.get(action_name)
    o.animation_data.action = act
    if act and len(act.slots):
        try:
            o.animation_data.action_slot = act.slots[0]
        except Exception:
            pass
    return act

def world_bbox(names):
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for n in names:
        ob = bpy.data.objects.get(n)
        if not ob: continue
        for c in ob.bound_box:
            w = ob.matrix_world @ Vector(c)
            for i in range(3):
                mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
    return mn, mx

def show_armor(on):
    for n in ARMOR:
        ob = bpy.data.objects.get(n)
        if ob:
            ob.hide_render = not on
            ob.hide_viewport = not on

def _get(name, coll, new):
    ob = bpy.data.objects.get(name)
    return ob

def setup_scene(res=(960, 540)):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'SINGLE'
    sc.display.shading.single_color = (0.6, 0.58, 0.55)
    sc.display.shading.show_shadows = True
    sc.display.shading.show_cavity = True
    try: sc.view_settings.view_transform = 'Standard'
    except Exception: pass
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    # world
    w = bpy.data.worlds.get('rworld') or bpy.data.worlds.new('rworld')
    w.use_nodes = False
    w.color = (0.05, 0.07, 0.10)
    sc.world = w
    # camera
    cam = bpy.data.objects.get('rcam')
    if cam is None:
        cd = bpy.data.cameras.new('rcam'); cam = bpy.data.objects.new('rcam', cd)
        sc.collection.objects.link(cam)
    sc.camera = cam
    # sun
    sun = bpy.data.objects.get('rsun')
    if sun is None:
        ld = bpy.data.lights.new('rsun', 'SUN'); ld.energy = 4.0; ld.angle = math.radians(8)
        sun = bpy.data.objects.new('rsun', ld); sc.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(55), math.radians(8), math.radians(40)))
    # ground plane at feet level
    mn, mx = world_bbox([BODY_NAME])
    g = bpy.data.objects.get('rground')
    if g is None:
        me = bpy.data.meshes.new('rground')
        s = max(mx.x-mn.x, mx.y-mn.y) * 2.0
        cx, cy = (mn.x+mx.x)/2, (mn.y+mx.y)/2
        z = mn.z - 0.01
        me.from_pydata([(cx-s,cy-s,z),(cx+s,cy-s,z),(cx+s,cy+s,z),(cx-s,cy+s,z)], [], [(0,1,2,3)])
        g = bpy.data.objects.new('rground', me); sc.collection.objects.link(g)
    return {'bbox_min': list(mn), 'bbox_max': list(mx)}

def aim_cam(loc, target, ortho=True, margin=1.25):
    sc = bpy.context.scene; cam = sc.camera
    cam.location = Vector(loc)
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    mn, mx = world_bbox([BODY_NAME])
    span = max(mx.x-mn.x, mx.y-mn.y, mx.z-mn.z)
    cam.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho:
        cam.data.ortho_scale = span * margin

def view_side():
    mn, mx = world_bbox([BODY_NAME])
    c = (mn+mx)/2
    span = max(mx.x-mn.x, mx.y-mn.y, mx.z-mn.z)
    aim_cam((c.x - span*3, c.y, c.z), c)   # look along +X from -X side

def view_3q():
    mn, mx = world_bbox([BODY_NAME])
    c = (mn+mx)/2
    span = max(mx.x-mn.x, mx.y-mn.y, mx.z-mn.z)
    aim_cam((c.x - span*2.4, c.y - span*2.4, c.z + span*0.9), c, margin=1.5)

def view_front():
    mn, mx = world_bbox([BODY_NAME])
    c = (mn+mx)/2
    span = max(mx.x-mn.x, mx.y-mn.y, mx.z-mn.z)
    aim_cam((c.x, c.y - span*3, c.z), c)

def render_frame(path, frame):
    sc = bpy.context.scene
    sc.frame_set(int(frame))
    bpy.context.view_layer.update()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path

def montage(paths, out_path, cols=6, pad=6, bg=(0.11, 0.13, 0.15)):
    """Tile rendered PNGs into one contact sheet via numpy (reliable, in-Blender)."""
    import numpy as np
    tiles = []; cw = ch = 0
    for p in paths:
        im = bpy.data.images.load(p, check_existing=False)
        try: im.colorspace_settings.name = 'Non-Color'
        except Exception: pass
        w, h = im.size
        buf = np.empty(w * h * 4, dtype=np.float32)
        im.pixels.foreach_get(buf)
        tiles.append(buf.reshape(h, w, 4)[::-1].copy())  # flip to top-down
        cw = max(cw, w); ch = max(ch, h)
        bpy.data.images.remove(im)
    n = len(tiles)
    rows = (n + cols - 1) // cols
    W = cols * cw + (cols + 1) * pad
    H = rows * ch + (rows + 1) * pad
    canvas = np.empty((H, W, 4), dtype=np.float32)
    canvas[..., 0], canvas[..., 1], canvas[..., 2], canvas[..., 3] = bg[0], bg[1], bg[2], 1.0
    for i, t in enumerate(tiles):
        r = i // cols; c = i % cols
        y = pad + r * (ch + pad); x = pad + c * (cw + pad)
        canvas[y:y + t.shape[0], x:x + t.shape[1], :] = t
    out = bpy.data.images.new('montage_tmp', width=W, height=H, alpha=True)
    try: out.colorspace_settings.name = 'Non-Color'
    except Exception: pass
    out.pixels.foreach_set(canvas[::-1].ravel())  # back to bottom-up for bpy
    out.filepath_raw = out_path; out.file_format = 'PNG'; out.save()
    bpy.data.images.remove(out)
    return {'out': out_path, 'n': n, 'WH': [int(W), int(H)], 'cell': [int(cw), int(ch)]}

def count_fcurves(act):
    nfc = 0
    for layer in act.layers:
        for strip in layer.strips:
            if strip.type == 'KEYFRAME':
                for cb in strip.channelbags:
                    nfc += len(cb.fcurves)
    return nfc

def extract_clip(src_name, f0, f1, new_name, cyclic=True, key_scale=True):
    """Re-bake frames [f0,f1] of a source action into a new action retimed to 1..N
    via matrix_basis (faithful regardless of source rotation mode)."""
    o = arm()
    set_action(src_name)
    sc = bpy.context.scene
    bones = list(o.pose.bones)
    samp = {pb.name: [] for pb in bones}
    for f in range(f0, f1 + 1):
        sc.frame_set(f); bpy.context.view_layer.update()
        for pb in bones:
            samp[pb.name].append(pb.matrix_basis.copy())
    if new_name in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[new_name])
    new = bpy.data.actions.new(new_name)
    new.use_fake_user = True
    ad = o.animation_data or o.animation_data_create()
    ad.action = new
    for pb in bones:
        pb.rotation_mode = 'QUATERNION'
    N = f1 - f0 + 1
    for idx in range(N):
        nf = idx + 1
        for pb in bones:
            pb.matrix_basis = samp[pb.name][idx]
        for pb in bones:
            pb.keyframe_insert('location', frame=nf)
            pb.keyframe_insert('rotation_quaternion', frame=nf)
            if key_scale:
                pb.keyframe_insert('scale', frame=nf)
    try: new.use_cyclic = cyclic
    except Exception: pass
    return {'action': new_name, 'frames': N, 'range': [1, N], 'fcurves': count_fcurves(new)}

# Leg bone groups (front = Clavicle/UpperArm/Forearm/Hand; back = Thigh/Calf/Foot)
FRONT_LEGS = [' R Clavicle_027', ' R UpperArm_028', ' R Forearm_029', ' R Hand_030',
              ' L Clavicle_031', ' L UpperArm_032', ' L Forearm_033', ' L Hand_034']
BACK_LEGS = [' L Thigh_035', ' L Calf_036', ' L Foot_037', ' R Thigh_038', ' R Calf_039', ' R Foot_040']
TRUNK = [b for b in [] ]  # filled at runtime via name match 'Queue de cheval'
EARS = ['ear_L_1_023', 'ear_L_2_024', 'ear_R_1_025', 'ear_R_2_026']
TAIL = [' Tail_041', ' Tail1_042', ' Tail2_043', ' Tail3_044', ' Tail4_045', ' Tail5_046', ' Tail6_047']

def restore_action(src_name, dst_name):
    """Copy src action over dst (revert a working clip to its faithful baseline)."""
    if dst_name in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[dst_name])
    c = bpy.data.actions[src_name].copy(); c.name = dst_name; c.use_fake_user = True
    return dst_name

def _action_bone_fcurves(action_name, bones):
    a = bpy.data.actions[action_name]
    out = []
    for layer in a.layers:
        for strip in layer.strips:
            if strip.type != 'KEYFRAME': continue
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    if 'pose.bones' in fc.data_path and fc.data_path.split('"')[1] in bones:
                        out.append(fc)
    return out

def phase_shift_bones(action_name, bones, shift, period):
    """Cyclically time-shift the given bones' curves by `shift` frames (wrap period=`period`).
    Keyframes assumed at integer frames 1..N. Preserves per-bone trajectory; changes only timing."""
    fcs = _action_bone_fcurves(action_name, bones)
    for fc in fcs:
        kps = fc.keyframe_points
        frames = [int(round(kp.co[0])) for kp in kps]
        newv = {}
        for f in frames:
            srcf = ((f - 1 - shift) % period) + 1
            newv[f] = fc.evaluate(srcf)
        for kp in kps:
            kp.co[1] = newv[int(round(kp.co[0]))]
        fc.update()
    return len(fcs)

def _bone_quat_fcurves(action_name, bone):
    a = bpy.data.actions[action_name]
    dp = 'pose.bones["%s"].rotation_quaternion' % bone
    fcs = {}
    for layer in a.layers:
        for strip in layer.strips:
            if strip.type != 'KEYFRAME': continue
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    if fc.data_path == dp:
                        fcs[fc.array_index] = fc
    return fcs

def damp_leg_lift(action_name, leg_bones, foot_key, reduce=0.5):
    """Reduce a leg's swing-apex lift WITHOUT floating the planted foot: blend each leg bone
    toward its planted-frame pose, weighted by the foot's normalized height that frame
    (planted frames ~unchanged -> stays grounded; apex frames damped by `reduce`)."""
    import mathutils
    o = arm(); set_action(action_name)
    a = bpy.data.actions[action_name]
    N = int(round(a.frame_range[1]))
    sc = bpy.context.scene
    Z = []
    for f in range(1, N + 1):
        sc.frame_set(f); bpy.context.view_layer.update()
        Z.append((o.matrix_world @ o.pose.bones[FEET[foot_key]].tail).z)
    zmin = min(Z); zmax = max(Z); span = (zmax - zmin) or 1.0
    w = [(z - zmin) / span for z in Z]
    anchor_idx = Z.index(zmin)
    sc.frame_set(anchor_idx + 1); bpy.context.view_layer.update()
    aq = {bn: o.pose.bones[bn].matrix_basis.to_quaternion() for bn in leg_bones}
    at = {bn: o.pose.bones[bn].matrix_basis.translation.copy() for bn in leg_bones}
    for bn in leg_bones:
        qf = _bone_quat_fcurves(action_name, bn)
        lf = {}
        for fc in _action_bone_fcurves(action_name, [bn]):
            if fc.data_path.endswith('location'): lf[fc.array_index] = fc
        if len(qf) < 4: continue
        nkp = len(qf[0].keyframe_points)
        for i in range(nkp):
            f = int(round(qf[0].keyframe_points[i].co[0]))
            blend = w[f - 1] * reduce
            oq = mathutils.Quaternion((qf[0].keyframe_points[i].co[1], qf[1].keyframe_points[i].co[1],
                                       qf[2].keyframe_points[i].co[1], qf[3].keyframe_points[i].co[1]))
            nq = oq.slerp(aq[bn], blend)
            qf[0].keyframe_points[i].co[1] = nq.w; qf[1].keyframe_points[i].co[1] = nq.x
            qf[2].keyframe_points[i].co[1] = nq.y; qf[3].keyframe_points[i].co[1] = nq.z
            for ai in lf:
                ov = lf[ai].keyframe_points[i].co[1]
                lf[ai].keyframe_points[i].co[1] = ov + (at[bn][ai] - ov) * blend
        for fc in list(qf.values()) + list(lf.values()):
            fc.update()
    return {'foot': foot_key, 'anchor_frame': anchor_idx + 1, 'zmin': round(zmin, 3), 'zmax': round(zmax, 3)}

def analyze_gait(action_name, N=None, fps=24):
    """Return foot lift, swing phasing, stance slip, body bob, in-place + loop-seam metrics."""
    import math
    o = arm(); set_action(action_name)
    a = bpy.data.actions[action_name]
    if N is None: N = int(round(a.frame_range[1]))
    sc = bpy.context.scene
    keys = ['FL', 'FR', 'BL', 'BR']
    Z = {k: [] for k in keys}; Y = {k: [] for k in keys}; rootY = []; rootZ = []
    for f in range(1, N + 1):
        sc.frame_set(f); bpy.context.view_layer.update()
        rt = o.matrix_world @ o.pose.bones[ROOT].tail
        rootY.append(rt.y); rootZ.append(rt.z)
        for k in keys:
            w = o.matrix_world @ o.pose.bones[FEET[k]].tail
            Z[k].append(w.z); Y[k].append(w.y)
    res = {'action': action_name, 'N': N, 'rootY_net': round(rootY[-1] - rootY[0], 4),
           'rootZ_bob': round(max(rootZ) - min(rootZ), 4)}
    ft = {}
    for k in keys:
        z = Z[k]; y = Y[k]; zmin = min(z); zmax = max(z)
        thr = zmin + max(0.05, 0.2 * (zmax - zmin))
        planted = [i for i, v in enumerate(z) if v <= thr]
        dys = [y[i + 1] - y[i - 1] for i in planted if 0 < i < len(y) - 1]
        ft[k] = {'lift': round(zmax - zmin, 3), 'midswing_f': z.index(zmax) + 1,
                 'stance_n': len(planted), 'stance_dY': round(sum(dys) / len(dys), 4) if dys else None}
    res['feet'] = ft
    res['midswing_order'] = sorted([(k, ft[k]['midswing_f']) for k in keys], key=lambda t: t[1])
    sc.frame_set(1); bpy.context.view_layer.update()
    p1 = {pb.name: (pb.matrix_basis.translation.copy(), pb.matrix_basis.to_quaternion()) for pb in o.pose.bones}
    sc.frame_set(N); bpy.context.view_layer.update()
    locd = rotd = 0.0
    for pb in o.pose.bones:
        l1, q1 = p1[pb.name]
        locd += (pb.matrix_basis.translation - l1).length
        rotd += q1.rotation_difference(pb.matrix_basis.to_quaternion()).angle
    res['loop_seam_loc'] = round(locd, 4); res['loop_seam_rot_deg'] = round(math.degrees(rotd), 2)
    return res

def timescale_action(src_name, dst_name, new_N):
    """Resample a clip to new_N frames (time-compress/stretch). Preserves gait pattern + loop."""
    src = bpy.data.actions[src_name]
    N = int(round(src.frame_range[1]))
    if dst_name in bpy.data.actions and dst_name != src_name:
        bpy.data.actions.remove(bpy.data.actions[dst_name])
    dst = src.copy(); dst.name = dst_name; dst.use_fake_user = True
    for layer in dst.layers:
        for strip in layer.strips:
            if strip.type != 'KEYFRAME': continue
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    vals = [fc.evaluate(1 + (i / (new_N - 1)) * (N - 1)) for i in range(new_N)]
                    kps = fc.keyframe_points
                    try: kps.clear()
                    except Exception:
                        while len(kps): kps.remove(kps[0])
                    kps.add(new_N)
                    for i in range(new_N):
                        kps[i].co = (i + 1, vals[i]); kps[i].interpolation = 'BEZIER'
                    fc.update()
    return {'src_N': N, 'dst_N': new_N, 'action': dst_name}

def reverse_action(src_name, dst_name):
    """Time-reverse a clip (dst frame f = src frame N+1-f). Used for walk_backwards."""
    src = bpy.data.actions[src_name]; N = int(round(src.frame_range[1]))
    if dst_name in bpy.data.actions and dst_name != src_name:
        bpy.data.actions.remove(bpy.data.actions[dst_name])
    dst = src.copy(); dst.name = dst_name; dst.use_fake_user = True
    for layer in dst.layers:
        for strip in layer.strips:
            if strip.type != 'KEYFRAME': continue
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    newv = {int(round(kp.co[0])): fc.evaluate(N + 1 - int(round(kp.co[0]))) for kp in fc.keyframe_points}
                    for kp in fc.keyframe_points:
                        kp.co[1] = newv[int(round(kp.co[0]))]
                    fc.update()
    return {'action': dst_name, 'N': N}

def boost_loc_amplitude(action_name, bone, axis_index, factor):
    """Scale a bone's location-channel deviation-from-mean by factor (e.g. body bob boost)."""
    a = bpy.data.actions[action_name]
    dp = 'pose.bones["%s"].location' % bone
    for layer in a.layers:
        for strip in layer.strips:
            if strip.type != 'KEYFRAME': continue
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    if fc.data_path == dp and fc.array_index == axis_index:
                        vals = [kp.co[1] for kp in fc.keyframe_points]
                        m = sum(vals) / len(vals)
                        for kp in fc.keyframe_points:
                            kp.co[1] = m + (kp.co[1] - m) * factor
                        fc.update(); return True
    return False

def freeze_toward_rest(action_name, bones, factor):
    """Blend the given bones toward their REST pose (matrix_basis identity) by `factor`.
    factor=0.85 -> bones ~85% frozen at rest with 15% residual motion. Used to turn a
    walk-in-place idle into a settled stance (freeze legs, keep body breathing)."""
    import mathutils
    iq = mathutils.Quaternion((1, 0, 0, 0))
    for bn in bones:
        qf = _bone_quat_fcurves(action_name, bn)
        lf = {fc.array_index: fc for fc in _action_bone_fcurves(action_name, [bn]) if fc.data_path.endswith('location')}
        if len(qf) < 4: continue
        for i in range(len(qf[0].keyframe_points)):
            oq = mathutils.Quaternion((qf[0].keyframe_points[i].co[1], qf[1].keyframe_points[i].co[1],
                                       qf[2].keyframe_points[i].co[1], qf[3].keyframe_points[i].co[1]))
            nq = oq.slerp(iq, factor)
            qf[0].keyframe_points[i].co[1] = nq.w; qf[1].keyframe_points[i].co[1] = nq.x
            qf[2].keyframe_points[i].co[1] = nq.y; qf[3].keyframe_points[i].co[1] = nq.z
            for ai in lf:
                lf[ai].keyframe_points[i].co[1] *= (1.0 - factor)
        for fc in list(qf.values()) + list(lf.values()):
            fc.update()
    return len(bones)

def export_clip_fbx(action_name, out_path, export_arm_name='elephant_skeleton_notused'):
    """Export the active clip as a Kit-ready armature-only FBX (Bannerlord axes, baked, no leaf bones).
    Temporarily renames the armature to <name>_notused so the engine won't register a 2nd skeleton."""
    import os
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    o = arm(); set_action(action_name)
    a = bpy.data.actions[action_name]; N = int(round(a.frame_range[1]))
    sc = bpy.context.scene; sc.frame_start = 1; sc.frame_end = N
    osn = sc.name; sc.name = action_name   # take name in FBX = scene name
    for ob in bpy.data.objects:
        ob.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    on, dn = o.name, o.data.name
    o.name = export_arm_name; o.data.name = export_arm_name
    win = bpy.context.window_manager.windows[0]; scr = win.screen
    v3d = [ar for ar in scr.areas if ar.type == 'VIEW_3D']; area = v3d[0] if v3d else scr.areas[0]
    region = [r for r in area.regions if r.type == 'WINDOW'][0]
    err = None
    try:
        with bpy.context.temp_override(window=win, area=area, region=region,
                                       active_object=o, selected_objects=[o], object=o):
            bpy.ops.export_scene.fbx(filepath=out_path, use_selection=True, object_types={'ARMATURE'},
                add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
                axis_forward='-Y', axis_up='Z', bake_anim=True, bake_anim_use_all_actions=False,
                bake_anim_use_nla_strips=False, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    except Exception as e:
        err = str(e)
    o.name = on; o.data.name = dn; sc.name = osn
    return {'ok': err is None, 'err': err, 'out': out_path, 'frames': N, 'exists': os.path.exists(out_path)}
