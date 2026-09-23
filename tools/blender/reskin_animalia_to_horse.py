"""Re-skin an Animalia (Fab) quadruped mesh onto Bannerlord's horse_skeleton, keeping the pack's own weights (#646).

Headless (the Store launcher DETACHES: completion is <out>/reskin_report.json plus its .DONE twin):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/reskin_animalia_to_horse.py -- ^
        --template "C:\\Users\\mikew\\Downloads\\horse.fbx" ^
        --map tools/blender/animalia_to_horse_map.json ^
        --src "E:\\LOTRAOMAssets\\_export\\animalia_elk\\meshes\\Meshes" ^
        --variant animalia_elk_08=Elk_M_Body,Elk_Antlers_08 [--variant ...] ^
        --material Elk_M_Material=animalia_elk_body --material Elk_Antlers_Material=animalia_elk_antlers ^
        --out "E:\\LOTRAOMAssets\\_reskin_out\\animalia_elk" [--lods 4] [--hoof-split 0.8,1.05] [--profile moose]

Runs of record (2026-09-23): elk variants animalia_elk_08/_06/_04/_spike (default chains); moose variants
animalia_moose_big/_small with --profile moose (keeps its short neck, see the map's 'profiles' note).

Why not a weight transfer (the troll's route, reskin_to_human_skeleton.py): the Animalia rig is a different
skeleton whose joints do not sit on the horse's. Measured 2026-09-23 against the engine rest, after one
uniform scale the legs and spine are within 2 to 11 cm and the head, neck and tail 0.2 to 0.7 m off, which
is posture (the horse holds its head higher and its tail lower). So the mesh is BENT into the horse rest
pose by its own skinning, and the pack's hand-made weights are kept, only renamed onto horse bones.

Method, per source FBX (LOD0 and every LODn of each part; every part shares the pack skeleton):
  1. global fit G: the uniform scale, turn (identity or 180 deg about Z) and offset that lay the pack's
     mapped joints (map 'joints') on the template armature's bone heads with the least squares residual;
  2. per bone M_b (template space): a bone in a map 'chain' that has a next bone gets
         M_b = T(horse joint) . R . S . T(-G source joint) . G
     R swings the source segment (joint to next joint) onto the horse segment, S stretches along it to the
     horse length, so the joint and the segment end both land exactly on horse joints. Every other bone
     (a chain's last bone, jaw, ears, chest, the pelvis, ...) moves rigidly with its nearest fitted ancestor.
     A chain given as {"bones", "stretch": false, "swing": false, "anchor_first_only": true} keeps its own
     proportions (the elk's tail, the moose's neck): only its first bone is moved onto the horse joint;
  3. vertices are moved by linear blend skinning with the pack's normalised weights (custom split normals,
     if any, follow the blended inverse transpose);
  4. weights: pack groups merge onto horse bones (map 'weights'; an unmapped group climbs to its nearest
     mapped ancestor), the ankle weight past the hoof joint moves to the hoof bone (map 'hoof_split',
     smoothstep over --hoof-split t0,t1 of the ankle-to-hoof segment), then 4 strongest influences,
     under 0.01 dropped, normalised;
  5. parts of one variant are joined per LOD (LOD0 = <variant>, then <variant>_lodN as elk_001 names them),
     materials renamed (--material), parented to the TEMPLATE armature: TaleWorlds' own horse.fbx mesh
     export (horse_skeleton_notused, 39 bones with 7 nubs, horseneck1 under horsetail3, facing -Y), the
     same armature elk_001 ships on, so the Kit gets a file shape it already accepts;
  6. export ARMATURE+MESH (the reskin_to_human_skeleton settings the Kit took for the troll), re-import it
     and check the bones and the influence count.

QA in the report: per joint residual after the fit (0 by construction for fitted joints), weight stats,
edge stretch deformed/rest under six bends applied to the template armature, measured on the reskin AND on
TaleWorlds' own horse mesh (the quality bar), hoof-split calibration (where the template's own hoof and
ankle weights sit along the segment), and Workbench renders (rest side and front, a combined bend) when
--preview is given.
"""
import argparse
import json
import math
import os
import re
import sys
import traceback
from collections import defaultdict

import bpy
from mathutils import Matrix, Vector

TOOLS = os.path.dirname(os.path.abspath(__file__))


# ---------------------------------------------------------------- scene helpers
def _import_fbx(path):
    pre = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in pre]
    return new, [o for o in new if o.type == "ARMATURE"], [o for o in new if o.type == "MESH"]


def _select_only(objs, active):
    for o in bpy.data.objects:
        try:
            o.select_set(False)
        except Exception:
            pass
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active


def _heads(arm):
    return {b.name: (arm.matrix_world @ b.matrix_local).translation.copy() for b in arm.data.bones}


def _parents(arm):
    return {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}


# ---------------------------------------------------------------- the fit
def global_fit(src_heads, tgt_heads, joints):
    """Uniform scale + turn + offset laying source joints on target joints (least squares). Returns (G, info)."""
    best = None
    for turn_name, F in (("identity", Matrix.Identity(4)), ("z180", Matrix.Rotation(math.pi, 4, "Z"))):
        pairs = [(F @ src_heads[s], tgt_heads[t]) for s, t in joints.items() if s in src_heads and t in tgt_heads]
        n = len(pairs)
        cs = sum((p for p, _ in pairs), Vector()) / n
        ct = sum((q for _, q in pairs), Vector()) / n
        num = sum((p - cs).dot(q - ct) for p, q in pairs)
        den = sum((p - cs).length_squared for p, _ in pairs)
        s = num / den
        tr = ct - cs * s
        res = [((p * s + tr) - q).length for p, q in pairs]
        mean = sum(res) / n
        if best is None or mean < best[1]["mean_m"]:
            G = Matrix.Translation(tr) @ Matrix.Diagonal((s, s, s, 1.0)) @ F
            best = (G, {"turn": turn_name, "scale": round(s, 4), "mean_m": round(mean, 4), "max_m": round(max(res), 4),
                        "pairs": n})
    return best


def _outer3(d):
    return Matrix(((d.x * d.x, d.x * d.y, d.x * d.z), (d.y * d.x, d.y * d.y, d.y * d.z), (d.z * d.x, d.z * d.y, d.z * d.z)))


def bone_transforms(src_heads, parents, tgt_heads, spec, G):
    """(bone -> 4x4 taking a source rest point (armature world) to template space, per-segment info,
    bone -> 3x3 pure ROTATION of that fit: the swing times the global turn, no scale or stretch). The
    rotations are what the clip retarget conjugates each bone's motion by, so a clip bends the fitted mesh
    the way it bent the pack's own mesh."""
    joints = spec["joints"]
    nxt = {}
    for chain in spec["chains"]:
        # a chain is a bone list, or {"bones": [...], "stretch": false, "anchor_first_only": true} for a part
        # whose proportions must NOT follow the horse (the elk's short tail inside the horse's long one)
        bones = chain["bones"] if isinstance(chain, dict) else chain
        opts = chain if isinstance(chain, dict) else {}
        stretch, swing = opts.get("stretch", True), opts.get("swing", True)
        first_only = opts.get("anchor_first_only", False)
        for i, b in enumerate(bones):
            if first_only and i > 0:
                continue
            nxt[b] = (bones[i + 1] if i + 1 < len(bones) else None, stretch, swing)
    fitted = {}
    fitted_rot = {}
    turn = G.to_3x3().normalized()
    info = {}
    for b, (n, stretch, swing) in nxt.items():
        if n is None or b not in src_heads or n not in src_heads or b not in joints or n not in joints:
            continue
        a, e = G @ src_heads[b], G @ src_heads[n]
        ta, te = tgt_heads[joints[b]], tgt_heads[joints[n]]
        ds, dt = e - a, te - ta
        ratio = dt.length / ds.length if stretch else 1.0
        d = ds.normalized()
        S = Matrix.Identity(3) + _outer3(d) * (ratio - 1.0)
        R = ds.rotation_difference(dt).to_matrix() if swing else Matrix.Identity(3)
        fitted[b] = Matrix.Translation(ta) @ (R @ S).to_4x4() @ Matrix.Translation(-a) @ G
        fitted_rot[b] = R @ turn
        info[b] = {"stretch": round(ratio, 3), "swing_deg": round(math.degrees(ds.angle(dt)), 1) if swing else 0.0}
    memo, rmemo = {}, {}

    def M(b):
        if b in memo:
            return memo[b]
        if b in fitted:
            memo[b], rmemo[b] = fitted[b], fitted_rot[b]
        elif parents.get(b):
            M(parents[b])
            memo[b], rmemo[b] = memo[parents[b]], rmemo[parents[b]]
        else:
            memo[b], rmemo[b] = G, turn
        return memo[b]

    for b in src_heads:
        M(b)
    return memo, info, rmemo


def joint_residuals(src_heads, tgt_heads, joints, Ms):
    """Where each mapped joint lands against its horse joint. 0 for fitted joints and stretched chain ends;
    the pelvis (global fit only) and the unstretched tail report their real offsets."""
    return {s: round(((Ms[s] @ src_heads[s]) - tgt_heads[t]).length, 4)
            for s, t in joints.items() if s in src_heads and s in Ms and t in tgt_heads}


def deform_mesh(mesh, Ms, G):
    """Move vertices by LBS with the pack's weights into template space; unparent; world = identity."""
    me = mesh.data
    mw = mesh.matrix_world.copy()
    names = {vg.index: vg.name for vg in mesh.vertex_groups}
    had_custom = bool(getattr(me, "has_custom_normals", False))
    loop_normals = None
    if had_custom:
        n3 = mw.to_3x3().inverted().transposed()
        loop_normals = [(n3 @ Vector(cn.vector)).normalized() for cn in me.corner_normals]
    new_pos, blend3, zero = [], [], 0
    for v in me.vertices:
        p = mw @ v.co
        ws = [(names[g.group], g.weight) for g in v.groups if g.weight > 0.0 and names.get(g.group) in Ms]
        tot = sum(w for _, w in ws)
        if tot < 1e-6:
            zero += 1
            new_pos.append(G @ p)
            blend3.append(G.to_3x3())
            continue
        q = Vector()
        A = Matrix(((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
        for n, w in ws:
            f = w / tot
            q += (Ms[n] @ p) * f
            A += Ms[n].to_3x3() * f
        new_pos.append(q)
        blend3.append(A)
    for md in list(mesh.modifiers):
        if md.type == "ARMATURE":
            mesh.modifiers.remove(md)
    mesh.parent = None
    mesh.matrix_world = Matrix.Identity(4)
    for v, q in zip(me.vertices, new_pos):
        v.co = q
    me.update()
    if had_custom:
        out = []
        for loop, n in zip(me.loops, loop_normals):
            out.append(tuple((blend3[loop.vertex_index].inverted_safe().transposed() @ n).normalized()))
        me.normals_split_custom_set(out)
    return {"verts": len(me.vertices), "unweighted_moved_rigidly": zero, "custom_normals": had_custom}


def _smoothstep(t0, t1, x):
    if x <= t0:
        return 0.0
    if x >= t1:
        return 1.0
    u = (x - t0) / (t1 - t0)
    return u * u * (3.0 - 2.0 * u)


def remap_weights(mesh, parents, spec, tgt_heads, hoof_t):
    """Pack groups -> horse bones (merge), hoof split, then 4 strongest, floor 0.01, normalised."""
    wmap = spec["weights"]

    def horse_of(name):
        b = name
        while b is not None:
            if b in wmap:
                return wmap[b]
            b = parents.get(b)
        return None

    names = {vg.index: vg.name for vg in mesh.vertex_groups}
    unmapped = defaultdict(int)
    per_vertex = []
    for v in mesh.data.vertices:
        hw = defaultdict(float)
        for g in v.groups:
            if g.weight <= 0.0:
                continue
            nm = names[g.group]
            hb = horse_of(nm)
            if nm not in wmap:
                unmapped[nm] += 1
            if hb:
                hw[hb] += g.weight
        for ankle, hoof in spec["hoof_split"].items():
            w = hw.get(ankle, 0.0)
            if w <= 0.0:
                continue
            A, B = tgt_heads[ankle], tgt_heads[hoof]
            seg = B - A
            t = (v.co - A).dot(seg) / seg.length_squared
            f = _smoothstep(hoof_t[0], hoof_t[1], t)
            if f > 0.0:
                hw[hoof] += w * f
                hw[ankle] = w * (1.0 - f)
        keep = sorted(((b, w) for b, w in hw.items() if w > 0.01), key=lambda t: -t[1])[:4]
        tot = sum(w for _, w in keep)
        per_vertex.append([(b, w / tot) for b, w in keep] if tot > 1e-8 else [])
    for vg in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(vg)
    groups = {}
    for i, ws in enumerate(per_vertex):
        for b, w in ws:
            if b not in groups:
                groups[b] = mesh.vertex_groups.new(name=b)
            groups[b].add([i], w, "REPLACE")
    return {"unmapped_groups_climbed": dict(unmapped), "horse_groups": sorted(groups)}


def weight_stats(obj):
    zero = over4 = unnorm = 0
    for v in obj.data.vertices:
        gs = [g for g in v.groups if g.weight > 1e-4]
        s = sum(g.weight for g in gs)
        if s < 1e-4:
            zero += 1
        elif abs(s - 1.0) > 0.02:
            unnorm += 1
        if len(gs) > 4:
            over4 += 1
    return {"verts": len(obj.data.vertices), "zero": zero, "over4": over4, "unnorm": unnorm,
            "groups": len(obj.vertex_groups)}


def hoof_calibration(template_mesh, tgt_heads, spec):
    """Where along ankle->hoof the TEMPLATE's own dominant-ankle and dominant-hoof vertices sit."""
    names = {vg.index: vg.name for vg in template_mesh.vertex_groups}
    mw = template_mesh.matrix_world
    out = {}
    for ankle, hoof in spec["hoof_split"].items():
        A, B = tgt_heads[ankle], tgt_heads[hoof]
        seg = B - A
        ts = {"ankle": [], "hoof": []}
        for v in template_mesh.data.vertices:
            gs = sorted(((names[g.group], g.weight) for g in v.groups), key=lambda t: -t[1])
            if not gs:
                continue
            top = gs[0][0]
            if top in (ankle, hoof):
                ts["ankle" if top == ankle else "hoof"].append((mw @ v.co - A).dot(seg) / seg.length_squared)
        out[ankle] = {k: ([round(min(x), 2), round(sorted(x)[len(x) // 2], 2), round(max(x), 2)] if x else None)
                      for k, x in ts.items()}
    return out


# ---------------------------------------------------------------- QA
def _world_axis_pose(arm, spec):
    """spec: [(bone, world axis 'X'|'Z', degrees)]; rotates each bone about a WORLD axis through its head."""
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()
    for bone, axis, deg in spec:
        pb = arm.pose.bones.get(bone)
        if pb is None:
            continue
        rest = pb.bone.matrix_local.to_3x3()
        W = Matrix.Rotation(math.radians(deg), 3, axis)
        pb.rotation_quaternion = (rest.inverted() @ W @ rest).to_quaternion()
    bpy.context.view_layer.update()


QA_POSES = {
    "foreleg_lift": [("horselleg2", "X", -50), ("horselleg4", "X", 80)],
    "hind_flex": [("horselfemur", "X", 30), ("horseltibia", "X", -45)],
    "neck_down": [("horseneck1", "X", -35), ("horseneck2", "X", -20)],
    "neck_turn": [("horseneck1", "Z", 30), ("horseneck2", "Z", 20)],
    "spine_flex": [("horsespine1", "X", 8), ("horsespine2", "X", 8), ("horsespine3", "X", 8)],
    "tail_lift": [("horsetail1", "X", 40)],
}


def _edge_stretch(mesh_obj, depsgraph):
    ev = mesh_obj.evaluated_get(depsgraph)
    me = ev.to_mesh()
    rest = mesh_obj.data
    ratios = []
    for e in rest.edges:
        a, b = e.vertices
        lr = (rest.vertices[a].co - rest.vertices[b].co).length
        if lr > 1e-6:
            ratios.append((me.vertices[a].co - me.vertices[b].co).length / lr)
    ev.to_mesh_clear()
    ratios.sort()
    return {"max": round(ratios[-1], 3), "p99": round(ratios[int(len(ratios) * 0.99) - 1], 3)} if ratios else {}


def qa(arm, meshes, template_mesh, preview_dir, label):
    res = {}
    for pose, spec in QA_POSES.items():
        _world_axis_pose(arm, spec)
        dg = bpy.context.evaluated_depsgraph_get()
        res[pose] = {m.name: _edge_stretch(m, dg) for m in meshes + ([template_mesh] if template_mesh else [])}
    _world_axis_pose(arm, [])
    if not preview_dir:
        return res
    os.makedirs(preview_dir, exist_ok=True)
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.display.shading.light = "STUDIO"
    scn.display.shading.color_type = "SINGLE"
    scn.render.resolution_x, scn.render.resolution_y = 900, 640
    cam = bpy.data.objects.new("_qa_cam", bpy.data.cameras.new("_qa_cam"))
    scn.collection.objects.link(cam)
    scn.camera = cam
    cam.data.lens = 35
    everything = [o for o in bpy.data.objects if o.type == "MESH"]
    shots = {"side": Vector((6.5, -0.3, 1.2)), "front": Vector((0.0, -7.0, 1.2))}
    combo = QA_POSES["foreleg_lift"] + QA_POSES["hind_flex"] + QA_POSES["neck_down"]
    pngs = {}
    for who in [m for m in meshes if not m.name.endswith(tuple("_lod%d" % i for i in range(1, 9)))] + ([template_mesh] if template_mesh else []):
        for o in everything:
            o.hide_render = (o is not who)
        for pose_name, spec in (("rest", []), ("bend", combo)):
            _world_axis_pose(arm, spec)
            for shot, loc in shots.items():
                if pose_name == "bend" and shot == "front":
                    continue
                target = Vector((0.0, -0.3, 1.1))
                cam.location = loc
                cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
                p = os.path.join(preview_dir, "%s_%s_%s_%s.png" % (label, who.name, pose_name, shot))
                scn.render.filepath = p
                bpy.ops.render.render(write_still=True)
                pngs["%s_%s_%s" % (who.name, pose_name, shot)] = p
    for o in everything:
        o.hide_render = False
    _world_axis_pose(arm, [])
    res["png"] = pngs
    return res


# ---------------------------------------------------------------- main
def process_part(path, spec, tgt_heads, hoof_t, report_part):
    _, arms, meshes = _import_fbx(path)
    if len(arms) != 1 or len(meshes) != 1:
        raise RuntimeError("%s: expected 1 armature + 1 mesh, got %d + %d" % (path, len(arms), len(meshes)))
    src_arm, mesh = arms[0], meshes[0]
    heads, parents = _heads(src_arm), _parents(src_arm)
    G, fit = global_fit(heads, tgt_heads, spec["joints"])
    Ms, seg, _ = bone_transforms(heads, parents, tgt_heads, spec, G)
    report_part["fit"] = fit
    report_part["segments"] = seg
    report_part["joint_residual_after_m"] = {k: v for k, v in joint_residuals(heads, tgt_heads, spec["joints"], Ms).items()
                                             if v > 0.001}
    report_part["deform"] = deform_mesh(mesh, Ms, G)
    report_part["weights"] = remap_weights(mesh, parents, spec, tgt_heads, hoof_t)
    bpy.data.objects.remove(src_arm, do_unlink=True)
    return mesh


def join(objs, name):
    _select_only(objs, objs[0])
    if len(objs) > 1:
        bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active
    j.name = name
    j.data.name = name
    return j


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--template", required=True)
    ap.add_argument("--map", required=True)
    ap.add_argument("--src", required=True, help="folder holding <part>.fbx and LODs/<part>_LOD<n>.fbx")
    ap.add_argument("--variant", action="append", required=True, help="name=PartA,PartB")
    ap.add_argument("--material", action="append", default=[], help="SourceMaterial=kit_material_name")
    ap.add_argument("--out", required=True)
    ap.add_argument("--preview")
    ap.add_argument("--lods", type=int, default=4)
    ap.add_argument("--hoof-split", default="0.8,1.05")
    ap.add_argument("--profile", help="a key of the map's 'profiles' whose chains replace the default ones")
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    report = {"args": vars(args), "variants": {}}
    out_json = os.path.join(args.out, "reskin_report.json")
    try:
        with open(args.map) as fh:
            spec = json.load(fh)
        if args.profile:
            spec["chains"] = spec["profiles"][args.profile]["chains"]
        hoof_t = tuple(float(x) for x in args.hoof_split.split(","))
        mat_map = dict(m.split("=", 1) for m in args.material)
        for vspec in args.variant:
            vname, parts = vspec.split("=", 1)
            parts = parts.split(",")
            bpy.ops.wm.read_factory_settings(use_empty=True)
            _, tarms, tmeshes = _import_fbx(args.template)
            tarm = tarms[0]
            tgt_heads = _heads(tarm)
            template_mesh = tmeshes[0] if tmeshes else None
            entry = {"template_armature": tarm.name, "template_bones": len(tarm.data.bones), "lods": {}}
            if template_mesh is not None:
                entry["hoof_calibration_template"] = hoof_calibration(template_mesh, tgt_heads, spec)
            finals = []
            for lod in range(0, args.lods + 1):
                pieces = []
                for part in parts:
                    path = os.path.join(args.src, part + ".fbx") if lod == 0 else \
                        os.path.join(args.src, "LODs", "%s_LOD%d.fbx" % (part, lod))
                    if not os.path.isfile(path):
                        entry["lods"].setdefault(str(lod), {})[part] = {"missing": path}
                        continue
                    rp = {}
                    pieces.append(process_part(path, spec, tgt_heads, hoof_t, rp))
                    entry["lods"].setdefault(str(lod), {})[part] = rp
                if not pieces:
                    continue
                name = vname if lod == 0 else "%s_lod%d" % (vname, lod)
                obj = join(pieces, name)
                for slot in obj.material_slots:
                    if slot.material is None:
                        continue
                    # every import brings its own copy (Elk_M_Material.001, ...): all resolve to one Kit material
                    kit_name = mat_map.get(re.sub(r"\.\d{3}$", "", slot.material.name))
                    if kit_name is None:
                        continue
                    if kit_name in bpy.data.materials:
                        slot.material = bpy.data.materials[kit_name]
                    else:
                        slot.material.name = kit_name
                obj.parent = tarm
                obj.matrix_parent_inverse = tarm.matrix_world.inverted()
                md = obj.modifiers.new("Armature", "ARMATURE")
                md.object = tarm
                md.use_vertex_groups = True
                entry["lods"][str(lod)]["joined"] = {"name": name, "materials": [s.material.name if s.material else None
                                                                                 for s in obj.material_slots],
                                                     "stats": weight_stats(obj)}
                finals.append(obj)
            entry["qa"] = qa(tarm, finals, template_mesh, args.preview, vname)
            if template_mesh is not None:
                bpy.data.objects.remove(template_mesh, do_unlink=True)
            _select_only([tarm] + finals, tarm)
            out_path = os.path.join(args.out, vname + ".fbx")
            bpy.ops.export_scene.fbx(
                filepath=out_path, use_selection=True, object_types={"ARMATURE", "MESH"},
                add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
                axis_forward="-Y", axis_up="Z", bake_anim=False,
                apply_scale_options="FBX_SCALE_NONE", global_scale=1.0,
                use_mesh_modifiers=False, path_mode="AUTO")
            # re-import check: same bones at the same heads, influences within the engine's 4
            bpy.ops.wm.read_factory_settings(use_empty=True)
            _, rarms, rmeshes = _import_fbx(out_path)
            rheads = _heads(rarms[0])
            entry["reimport"] = {
                "bytes": os.path.getsize(out_path),
                "bones": len(rheads),
                "bone_head_drift_max_m": round(max((rheads[n] - tgt_heads[n]).length for n in tgt_heads if n in rheads), 5),
                "meshes": {m.name: weight_stats(m) for m in rmeshes}}
            report["variants"][vname] = entry
    except Exception:
        report["error"] = traceback.format_exc()
    with open(out_json, "w") as fh:
        json.dump(report, fh, indent=1, default=str)
    with open(out_json + ".DONE", "w") as fh:
        fh.write("error" if "error" in report else "ok")


if __name__ == "__main__":
    main()
