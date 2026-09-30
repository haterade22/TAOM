#!/usr/bin/env python3
"""
The Blender half of tools/creature_fit.py (2026-09-30, hill troll first): put a creature, its held weapon and
its clips in one scene, in the ENGINE's frame, so a measurement or a look shows what the game shows.

WHY
A clip is only wrong against the body and the weapon it plays with. The engine hangs a held weapon on the grip
bone (r_finger0), pieces up its +Z (measured on vanilla two-handed clips, tools/creature_fit_math.py), and every
bone length comes from the rig (the troll's masters key rotations only). This script builds exactly that: the
engine rig from the engine skeleton dump, the race's LOD0 meshes moved onto it, the crafted weapon on the grip
bone, and each clip keyed over the frames it plays.

MODES (the launcher detaches: poll `<out>.report.json` until "stage" is "done")
  export  the meshes (engine space, 4 weights each, the hand's morph channels) and the weapon pieces to an .npz
          for tools/creature_fit.py measure:
            blender-launcher.exe -b --factory-startup -P tools/blender/creature_fit_blender.py -- \\
                --mode export --spec tools/fit_specs/hill_troll.json --out <scratch>/fit_mesh.npz
  scene   one creature per clip side by side, each looping the frames its clip plays, a label over each, saved as
          a .blend to open in the Blender window:
            ... -- --mode scene --spec tools/fit_specs/hill_troll.json --index <troll_index.json> \\
                --json-dir <troll_json> --clips anim_hill_troll_combat_idle1,anim_hill_troll_stand_2h \\
                --save <scratch>/view.blend
Read-only on every input: nothing is written but --out / --save and the report.
"""
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(TOOLS)
for _p in (HERE, TOOLS):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import numpy as np  # noqa: E402

import creature_fit_math as cfm  # noqa: E402
import retarget_mannequin_to_human as rt  # noqa: E402
from _gamedir import game_dir  # noqa: E402

GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
ARMORY = os.path.join(GAME, "Modules", "LOTRLOME_Armory")
SPACING = 5.0          # metres between side-by-side creatures in a scene
COLOURS = {"body": (0.46, 0.50, 0.47, 1.0), "weapon_handle": (0.36, 0.24, 0.14, 1.0),
           "weapon_head": (0.55, 0.55, 0.60, 1.0), "label": (0.95, 0.85, 0.35, 1.0)}


# ------------------------------------------------------------------------------------------------ inputs
def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"mode": None, "spec": None, "out": None, "index": None, "json_dir": None, "clips": [], "save": None,
           "json_dir2": None}
    i = 0
    while i < len(argv):
        k = argv[i].lstrip("-").replace("-", "_")
        if k not in out or i + 1 >= len(argv):
            raise SystemExit("unknown or incomplete argument %r" % argv[i])
        out[k] = argv[i + 1].split(",") if k == "clips" else argv[i + 1]
        i += 2
    return out


def load_spec(path):
    with open(path, encoding="utf-8") as fh:
        spec = json.load(fh)
    spec["_paths"] = {k: os.path.join(ARMORY, v) for k, v in spec["armory"].items()}
    spec["_paths"]["engine_skeleton"] = os.path.join(REPO, spec["engine_skeleton"])
    return spec


def _lod0(name):
    return bpy.data.objects.get(name) or bpy.data.objects.get(name + ".lod0")


# ------------------------------------------------------------------------------------------------ creature
def load_creature(spec, report):
    """Import the race FBX, fit its frame onto the engine's from the bone positions, and return
    (engine skeleton, {mesh name: object moved into engine space}); the FBX armature is deleted."""
    with open(spec["_paths"]["engine_skeleton"], encoding="utf-8-sig") as fh:
        sk = cfm.Skeleton.from_spec(json.load(fh))
    _, pw = sk.rest_world()
    new, fbx_arm = rt._import_fbx(spec["_paths"]["creature_fbx"])
    if fbx_arm is None:
        raise RuntimeError("no armature in %s" % spec["_paths"]["creature_fbx"])
    names = [n for n in sk.names if n in fbx_arm.data.bones]
    src = np.array([list(fbx_arm.matrix_world @ fbx_arm.data.bones[n].head_local) for n in names])
    dst = np.array([pw[sk.index(n)] for n in names])
    fit = cfm.similarity_fit(src, dst)
    report["frame_fit"] = {"bones": len(names), "scale": fit["scale"], "residual_max_m": fit["residual_max"],
                           "R": fit["R"].tolist()}
    if fit["residual_max"] > 0.005:
        raise RuntimeError("the FBX rig does not match the engine skeleton: residual %.4f m" % fit["residual_max"])
    T = Matrix.Identity(4)
    for r in range(3):
        for c in range(3):
            T[r][c] = fit["scale"] * fit["R"][r, c]
        T[r][3] = fit["t"][r]
    meshes = {}
    for name in spec["meshes"]:
        o = _lod0(name)
        if o is None:
            report.setdefault("missing_meshes", []).append(name)
            continue
        mw = T @ o.matrix_world
        o.parent = None
        o.matrix_world = mw
        for md in list(o.modifiers):
            if md.type == "ARMATURE":
                o.modifiers.remove(md)
        meshes[name] = o
    for o in new:
        if o.name not in {m.name for m in meshes.values()}:
            rt._delete_objects([o])
    return sk, meshes


def close_hands(spec, meshes, hand_poses):
    """Show the grip the game shows: the engine picks hand-morph channel 5L + R + 1 of the 26 on the hand mesh
    from a clip's hand-pose pair (creature_fit.hand_pose_channel); key block 0 is the basis, so channel c is
    key block c + 1. Without this the scene shows the open, neutral hand."""
    import creature_fit
    o = meshes.get(spec.get("hand_mesh"))
    if o is None or not o.data.shape_keys:
        return None
    kb = o.data.shape_keys.key_blocks
    channel = creature_fit.hand_pose_channel(*hand_poses)
    for i, k in enumerate(kb):
        if i:
            k.value = 1.0 if i == channel + 1 else 0.0
    return kb[channel + 1].name


def bind(meshes, rig, offset=Vector()):
    """Give each mesh an armature modifier on `rig`; with an offset, bind linked copies instead."""
    out = []
    for name, o in meshes.items():
        m = o if offset.length == 0 else o.copy()
        if m is not o:
            bpy.context.scene.collection.objects.link(m)
            m.matrix_world = Matrix.Translation(offset) @ o.matrix_world
        md = m.modifiers.new("Armature", "ARMATURE")
        md.object = rig
        m.parent = rig
        m.matrix_parent_inverse = rig.matrix_world.inverted()
        m.color = COLOURS["body"]
        out.append(m)
    return out


# ------------------------------------------------------------------------------------------------ weapon
def load_weapon_pieces(spec, report):
    """Import the weapon FBX; return {mesh: object} with each piece's vertices in its own frame (centred, length
    along Z, as the crafting system assumes), checked rather than assumed."""
    new, _ = rt._import_fbx(spec["_paths"]["weapon_fbx"])
    pieces = {}
    for pc in spec["weapon"]["pieces"]:
        o = _lod0(pc["mesh"])
        if o is None:
            raise RuntimeError("weapon piece %s not in %s" % (pc["mesh"], spec["_paths"]["weapon_fbx"]))
        o.data = o.data.copy()
        o.data.transform(o.matrix_world)
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
        co = np.array([list(v.co) for v in o.data.vertices])
        lo, hi = co.min(axis=0), co.max(axis=0)
        ext = hi - lo
        report.setdefault("weapon", {})[pc["mesh"]] = {"extent_m": ext.round(4).tolist(),
                                                       "centre_m": ((lo + hi) / 2).round(4).tolist()}
        if abs(ext[2] - pc["length"]) > 0.02 * pc["length"] and pc["role"] == "handle":
            raise RuntimeError("%s is %.3f m along Z, the spec says %.3f: the piece frame is not the crafting one"
                               % (pc["mesh"], ext[2], pc["length"]))
        pieces[pc["mesh"]] = o
    keep = set(pieces.values())
    rt._delete_objects([o for o in new if o not in keep])
    return pieces


def mount_weapon(spec, pieces, rig, tag):
    """An empty that copies the grip bone's frame, with each piece up its Z at the crafted position."""
    grip = bpy.data.objects.new("grip_" + tag, None)
    grip.empty_display_type = "ARROWS"
    grip.empty_display_size = 0.3
    bpy.context.scene.collection.objects.link(grip)
    con = grip.constraints.new("COPY_TRANSFORMS")
    con.target = rig
    con.subtarget = spec["weapon"]["grip_bone"]
    for lay in cfm.crafted_layout(spec["weapon"]["pieces"]):
        src = pieces[lay["mesh"]]
        o = src.copy()
        o.name = "%s_%s" % (lay["role"], tag)
        bpy.context.scene.collection.objects.link(o)
        o.parent = grip
        o.matrix_parent_inverse = Matrix.Identity(4)
        o.location = (0.0, 0.0, lay["z_centre"])
        o.color = COLOURS["weapon_" + lay["role"]] if ("weapon_" + lay["role"]) in COLOURS else COLOURS["weapon_head"]
    return grip


# ------------------------------------------------------------------------------------------------ clips
def played_locals(sk, clip_json, meta):
    """Engine locals (R, p) over the frames the clip plays, in playing order."""
    import creature_fit
    frames = creature_fit.played_frames(meta)
    return cfm.sample_clip(sk, clip_json, [float(t) for t in frames]), frames


def _fcurve(act, rig, pb, data_path, prop, index, group):
    """The F-curve for one channel in the rig's slot: Action.fcurve_ensure_for_datablock (Blender 4.4+), else one
    keyframe_insert to make it and a lookup through the slotted layout (layers/strips/channelbags)."""
    try:
        return act.fcurve_ensure_for_datablock(rig, data_path, index=index, group_name=group)
    except AttributeError:
        pb.keyframe_insert(prop, index=index, frame=1)
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    fc = bag.fcurves.find(data_path, index=index)
                    if fc is not None:
                        fc.keyframe_points.clear()
                        return fc
        raise


def key_rig(rig, sk, R, p, name, cyclic=True):
    """Key `rig` (built by build_engine_rig, so each bone's matrix_local is the engine rest world) with engine
    locals: a bone's basis is rest_local^-1 @ pose_local, frames 1..F. Written straight into F-curves."""
    act = bpy.data.actions.new(name)
    ad = rig.animation_data or rig.animation_data_create()
    ad.action = act
    try:
        slot = act.slots.new(id_type="OBJECT", name=rig.name)
        ad.action_slot = slot
    except Exception:
        pass
    F = R.shape[0]
    frames = np.arange(1, F + 1, dtype=float)
    for bi, bn in enumerate(sk.names):
        pb = rig.pose.bones.get(bn)
        if pb is None:
            continue
        pb.rotation_mode = "QUATERNION"
        R0, p0 = sk.rest_R[bi], sk.rest_p[bi]
        quats = np.array([cfm.mat_to_quat(R0.T @ R[f, bi]) for f in range(F)])
        for f in range(1, F):                       # keep neighbours on one hemisphere so the curves do not flip
            if quats[f] @ quats[f - 1] < 0:
                quats[f] = -quats[f]
        locs = np.einsum("ij,fj->fi", R0.T, p[:, bi] - p0)
        for path, vals in (("rotation_quaternion", quats), ("location", locs)):
            for k in range(vals.shape[1]):
                fc = _fcurve(act, rig, pb, 'pose.bones["%s"].%s' % (bn, path), path, k, bn)
                fc.keyframe_points.add(F)
                co = np.empty(2 * F)
                co[0::2], co[1::2] = frames, vals[:, k]
                fc.keyframe_points.foreach_set("co", co.tolist())
                for kp in fc.keyframe_points:
                    kp.interpolation = "LINEAR"
                if cyclic:
                    fc.modifiers.new("CYCLES")
                fc.update()
    return act


def add_label(text, loc):
    cu = bpy.data.curves.new("label_" + text, "FONT")
    cu.body = text
    cu.size = 0.28
    cu.align_x = "CENTER"
    o = bpy.data.objects.new("label_" + text, cu)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (math.radians(90), 0.0, math.radians(180))   # upright, readable from the camera at +Y
    o.color = COLOURS["label"]
    return o


def setup_view(n, height=3.6):
    sc = bpy.context.scene
    sc.render.fps = 30
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "OBJECT"
    sc.display.shading.show_shadows = True
    sc.display.shading.show_cavity = True
    w = bpy.data.worlds.get("fit_world") or bpy.data.worlds.new("fit_world")
    w.color = (0.08, 0.09, 0.11)
    sc.world = w
    width = (n - 1) * SPACING
    me = bpy.data.meshes.new("ground")
    s = max(width, 6.0)
    me.from_pydata([(-s, -8, 0), (width + s, -8, 0), (width + s, 8, 0), (-s, 8, 0)], [], [(0, 1, 2, 3)])
    g = bpy.data.objects.new("ground", me)
    g.color = (0.25, 0.23, 0.2, 1.0)
    sc.collection.objects.link(g)
    cd = bpy.data.cameras.new("fit_cam")
    cd.lens = 35
    cam = bpy.data.objects.new("fit_cam", cd)
    sc.collection.objects.link(cam)
    dist = max(9.0, width * 0.75 + 7.0)
    cam.location = (width / 2 + dist * 0.35, dist, height * 0.9)
    d = Vector((width / 2, 0.0, height * 0.45)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam


# ------------------------------------------------------------------------------------------------ modes
def mode_export(args, spec, report):
    sk, meshes = load_creature(spec, report)
    pieces = load_weapon_pieces(spec, report)
    data = {"bone_names": np.array(sk.names), "mesh_names": np.array(list(meshes))}
    for mi, (name, o) in enumerate(meshes.items()):
        me = o.data
        mw = o.matrix_world
        nm = mw.to_3x3().inverted().transposed()
        v = np.array([list(mw @ vv.co) for vv in me.vertices], dtype=np.float32)
        n = np.array([list((nm @ vv.normal).normalized()) for vv in me.vertices], dtype=np.float32)
        groups = {g.index: g.name for g in o.vertex_groups}
        idx = np.zeros((len(me.vertices), 4), dtype=np.int16)
        wts = np.zeros((len(me.vertices), 4), dtype=np.float32)
        unweighted = 0
        for vi, vv in enumerate(me.vertices):
            gw = sorted(((g.weight, groups.get(g.group)) for g in vv.groups
                         if groups.get(g.group) in sk._index and g.weight > 0), reverse=True)[:4]
            tot = sum(w for w, _ in gw)
            if tot <= 0:
                unweighted += 1
                continue
            for k, (w, bn) in enumerate(gw):
                idx[vi, k] = sk.index(bn)
                wts[vi, k] = w / tot
        me.calc_loop_triangles()
        tri = np.array([list(t.vertices) for t in me.loop_triangles], dtype=np.int32)
        data["m%d_v" % mi], data["m%d_n" % mi], data["m%d_tri" % mi] = v, n, tri
        data["m%d_idx" % mi], data["m%d_w" % mi] = idx, wts
        entry = {"vertices": len(v), "triangles": len(tri), "unweighted": unweighted}
        if name == spec.get("hand_mesh") and me.shape_keys:
            kb = me.shape_keys.key_blocks
            basis = np.array([list(p.co) for p in kb[0].data])
            m3 = np.array(mw.to_3x3())
            keys = np.array([(np.array([list(p.co) for p in k.data]) - basis) @ m3.T for k in kb[1:]], dtype=np.float32)
            data["m%d_keys" % mi] = keys
            data["m%d_keynames" % mi] = np.array([k.name for k in kb[1:]])
            entry["shape_keys"] = len(keys)
        report.setdefault("meshes", {})[name] = entry
    for pi, (name, o) in enumerate(pieces.items()):
        o.data.calc_loop_triangles()
        data["w%d_name" % pi] = np.array(name)
        data["w%d_v" % pi] = np.array([list(vv.co) for vv in o.data.vertices], dtype=np.float32)
        data["w%d_tri" % pi] = np.array([list(t.vertices) for t in o.data.loop_triangles], dtype=np.int32)
    np.savez_compressed(args["out"], **data)
    report["out"] = args["out"]


def mode_scene(args, spec, report):
    import creature_fit
    with open(args["index"], encoding="utf-8") as fh:
        index = json.load(fh)["clips"]
    sk, meshes = load_creature(spec, report)
    pieces = load_weapon_pieces(spec, report)
    clips = [c for c in args["clips"] if c and index.get(c, {}).get("master")]
    if clips:                  # linked copies share the mesh data, so one grip shows on every creature
        report["hand_morph"] = close_hands(spec, meshes, tuple(index[clips[0]].get("hand_poses", [3, 3])))
    report["skipped"] = [c for c in args["clips"] if c and c not in clips]
    # with --json-dir2, each clip plays twice side by side: as --json-dir has it, then as --json-dir2 has it
    runs = [(c, args["json_dir"], "") for c in clips]
    if args["json_dir2"]:
        runs = [r for c in clips for r in ((c, args["json_dir"], " before"), (c, args["json_dir2"], " after"))]
    longest = 1
    rigs = []
    for k, (clip, json_dir, tag) in enumerate(runs):
        meta = index[clip]
        with open(os.path.join(json_dir, meta["master"] + ".json"), encoding="utf-8-sig") as fh:
            clip_json = json.load(fh)
        (R, p), frames = played_locals(sk, clip_json, meta)
        rig = rt.build_engine_rig(spec["_paths"]["engine_skeleton"], name="rig_%02d" % k)
        offset = Vector((k * SPACING, 0.0, 0.0))
        rig.location = offset
        bpy.context.view_layer.update()            # matrix_world lags a location change until the next update
        if k:
            bind(meshes, rig, offset=offset)       # linked copies; the originals go on the first rig below
        key_rig(rig, sk, R, p, clip + tag.replace(" ", "_"))
        mount_weapon(spec, pieces, rig, "%02d" % k)
        add_label(clip.replace(spec["clip_prefix"], "") + tag, offset + Vector((0.0, 0.0, 4.3)))
        longest = max(longest, len(frames))
        report.setdefault("clips", {})[clip + tag] = {"master": meta["master"], "json_dir": json_dir,
                                                      "frames": [frames[0], frames[-1]],
                                                      "hand_poses": meta["hand_poses"], "flags": meta["flags"]}
        rigs.append(rig)
    if rigs:
        bind(meshes, rigs[0])
    for o in pieces.values():
        o.hide_set(True)
        o.hide_render = True
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = 1, longest
    setup_view(len(runs))
    if args["save"]:
        bpy.ops.wm.save_as_mainfile(filepath=args["save"])
        report["saved"] = args["save"]


def main():
    args = parse_args(sys.argv)
    out = args["out"] or args["save"]
    if not args["mode"] or not args["spec"] or not out:
        raise SystemExit("--mode, --spec and --out (export) or --save (scene) are required")
    report_path = out + ".report.json"
    report = {"mode": args["mode"], "stage": "start"}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1, default=str)
    save("start")
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        spec = load_spec(args["spec"])
        {"export": mode_export, "scene": mode_scene}[args["mode"]](args, spec, report)
        report["ok"] = True
    except Exception as exc:
        report["ok"] = False
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["traceback"] = traceback.format_exc()
    save("done")


if __name__ == "__main__":
    main()
