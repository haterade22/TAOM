# Rebuild a Bannerlord human animation in Blender from a keyframe JSON (no assimp / no FBX import).
# Companion to tools/read_anim_keyframes_tpac.ps1 (which dumps the JSON). Bypasses TpacTool's
# crashing assimp FBX exporter: read keyframes via TpacTool.Lib -> JSON -> rebuild here -> retarget
# onto the troll ARP rig with arp_retarget.py. Re-exec'd each Blender-MCP call:
#   exec(open(r'C:\Users\mikew\source\repos\TAOM\tools\blender\rebuild_anim_from_json.py').read(), globals())
#
# KEY CALIBRATION (verified 2026-06-14): the JSON per-bone LOCAL rotation quaternion (relative to
# parent, Bannerlord space) equals the Blender armature-space rotation for the root with NO axis
# swap or conjugate -- pelvis JSON t0 (0.7071,0,-0.7071,0) == Blender armature pose. So we walk the
# bone hierarchy multiplying parent_armature_matrix @ local(Translation(pos)@Rotation(quat)) and set
# pose_bone.matrix directly. Bones must be processed parent-before-child (the JSON bone order is).
import bpy, json, mathutils


def _mat16(a):
    return mathutils.Matrix((a[0:4], a[4:8], a[8:12], a[12:16]))


def _nearest(frames, t):
    best = None
    for fr in frames:
        if fr["t"] <= t:
            best = fr
        else:
            break
    return best if best is not None else (frames[0] if frames else None)


def rebuild_from_json(json_path, armature_name, action_suffix="_src"):
    """Rebuild the JSON clip's animation onto an existing human armature (correct rest pose, e.g.
    imported once from human_skeleton_with_male_body.fbx or an extracted walk). Frames 1..duration
    map 1:1 to JSON t 0..duration-1. Returns the new action name."""
    J = json.load(open(json_path))
    arm = bpy.data.objects[armature_name]
    bones = J["bones"]
    rest = {b["name"]: _mat16(b["rest"]) for b in bones}
    parent = {b["name"]: b["parent"] for b in bones}
    order = [b["name"] for b in bones]                 # parent-before-child (verified)
    anim = {a["bone"]: a for a in J["boneAnims"]}
    dur = int(J["duration"])

    for o in bpy.data.objects:
        try: o.select_set(False)
        except Exception: pass
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    arm.animation_data_clear()
    arm.animation_data_create()
    act = bpy.data.actions.new(J["clip"] + action_suffix)
    arm.animation_data.action = act

    miss = [bn for bn in order if bn not in arm.pose.bones]
    for fi in range(dur):
        t = fi
        bpy.context.scene.frame_set(fi + 1)
        armmat = {}
        for bn in order:
            if bn not in arm.pose.bones:
                continue
            a = anim.get(bn)
            if a and a["rot"]:
                rf = _nearest(a["rot"], t)
                q = mathutils.Quaternion((rf["w"], rf["x"], rf["y"], rf["z"]))
            else:
                q = rest[bn].to_quaternion()
            if a and a["pos"]:
                pf = _nearest(a["pos"], t)
                loc = mathutils.Vector((pf["x"], pf["y"], pf["z"]))
            else:
                loc = rest[bn].to_translation()
            local = mathutils.Matrix.Translation(loc) @ q.to_matrix().to_4x4()
            p = parent[bn]
            armmat[bn] = (armmat[p] @ local) if (p and p in armmat) else local
            pb = arm.pose.bones[bn]
            pb.matrix = armmat[bn]
            bpy.context.view_layer.update()           # propagate so children read parent pose
            pb.keyframe_insert("rotation_quaternion")
            pb.keyframe_insert("location")
    bpy.ops.object.mode_set(mode='OBJECT')
    return {"action": act.name, "frames": dur, "missing_bones": miss}
