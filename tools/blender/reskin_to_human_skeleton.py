"""Re-skin meshes that sit on human_skeleton with TaleWorlds' own weights, transferred from the vanilla body.

Headless:
    blender-launcher.exe -b -P tools/blender/reskin_to_human_skeleton.py -- ^
        --donor "E:\\LOTRAOMAssets\\human_skeleton_with_male_body.fbx" ^
        --target "E:\\LOTRAOMAssets\\Creatures\\LOME_troll.fbx" [--target ...] ^
        --out "E:\\LOTRAOMAssets\\_troll_rig_out_20260918" ^
        [--engine-skeleton tools/blender/human_skeleton_engine.json --preview <dir>]

Why: the shipping cave troll skins were hand-weighted with 115 un-normalised vertices and 9 over the
engine's 4-influence limit on the body and 1,836 un-normalised on the head (measured 2026-09-18), so the
Kit's playback differs from Blender's preview and the bends are wrong. The vanilla body meshes in the
canonical human FBX carry TaleWorlds' skinning on exactly this skeleton, and the troll meshes sit on the
same joints (0.000 m offsets), so nearest-surface weight transfer gives professional weights everywhere
the troll surface has a human counterpart. Twist bones come along (the donor uses them).

Method, per target FBX:
  1. import donor + target; both are TpacTool FixBoneForBlender exports (armature object turned -180 Z,
     character facing Blender -Y), so they already overlap: no re-spacing needed for the transfer
  2. one FULL donor = the four human meshes joined (body, hands, feet, head), so any troll part
     (armour spans body and head) picks its nearest human surface
  3. every LOD0 target mesh: drop old vertex groups, DataTransfer modifier VGROUP_WEIGHTS with
     POLYINTERP_NEAREST from the full donor, then a Python pass: keep the 4 strongest influences, drop
     under 0.01, normalise to 1.0, and report vertices that ended with no weight
  4. every .lodN target mesh: same transfer but from its own LOD0 result (NEAREST vertex), so all LODs
     deform alike
  5. export the target FBX with the ORIGINAL armature and orientation, only the weights changed: the
     Kit already accepts this exact file shape (it is the shipping asset), object_types ARMATURE+MESH,
     Y/X bone axes, no leaf bones, no animation
  3b. head meshes: the skull above the neck joint and the jaw in front of it ride `head` 100%, eyes and mouth
     parts too, and every other weight on a head mesh becomes `neck`, so a head mesh carries head and neck only.
     A troll's jaw hangs at or below the neck joint's height, where the nearest human surface is the chest: left
     to the transfer, its chin rode `spine2` and stayed behind when the head moved (the stretched mouth,
     2026-09-24). The report counts head-mesh vertices with any other weight, and `.DONE` says "fail" if any remain.
  6. QA (needs --engine-skeleton): duplicates of the OLD-weight and NEW-weight LOD0 meshes plus the matching
     DONOR part are turned into engine space and bound to a rig built from the engine rest frames; six
     standard single-joint bends (thigh, knee, shoulder, elbow, spine, a neck nod each way) are applied and the worst and
     99th-percentile edge stretch deformed/rest is measured on all three, the donor (TaleWorlds skinning)
     being the quality bar; old vs new are rendered on the combined pose from the front. Candy-wrapper
     twists and stretched armpits show up as max ratios well over 2 where the donor stays near 1.5

Outputs: <out>/<target name>.fbx, <out>/reskin_report.json (+ .DONE), <preview>/<mesh>.png
"""
import argparse
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Matrix, Quaternion, Vector

TOOLS = os.path.dirname(os.path.abspath(__file__))
_QA_DBG = {}
sys.path.insert(0, TOOLS)


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


def join_copies(meshes, name):
    """Joined duplicate of the given meshes (originals untouched)."""
    dups = []
    for m in meshes:
        d = m.copy()
        d.data = m.data.copy()
        d.name = name + "_part"
        bpy.context.scene.collection.objects.link(d)
        dups.append(d)
    _select_only(dups, dups[0])
    bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active
    j.name = name
    return j


def transfer_weights(src, dst, mapping):
    """DataTransfer modifier VGROUP_WEIGHTS src -> dst, applied."""
    for vg in list(dst.vertex_groups):
        dst.vertex_groups.remove(vg)
    for vg in src.vertex_groups:
        dst.vertex_groups.new(name=vg.name)
    _select_only([src, dst], dst)
    md = dst.modifiers.new("xfer", "DATA_TRANSFER")
    md.object = src
    md.use_vert_data = True
    md.data_types_verts = {"VGROUP_WEIGHTS"}
    md.vert_mapping = mapping
    md.layers_vgroup_select_src = "ALL"
    md.layers_vgroup_select_dst = "NAME"
    md.mix_mode = "REPLACE"
    md.mix_factor = 1.0
    bpy.ops.object.modifier_apply(modifier=md.name)


def tidy_weights(obj, limit=4, floor=0.01):
    """Keep the strongest `limit` influences per vertex, drop tiny ones, normalise. Returns stats."""
    zero = 0
    for v in obj.data.vertices:
        ws = [(g.group, g.weight) for g in v.groups if g.weight > floor]
        ws.sort(key=lambda t: -t[1])
        keep = ws[:limit]
        total = sum(w for _, w in keep)
        for g in list(v.groups):
            obj.vertex_groups[g.group].remove([v.index])
        if total <= 1e-8:
            zero += 1
            continue
        for gi, w in keep:
            obj.vertex_groups[gi].add([v.index], w / total, "REPLACE")
    # drop groups no vertex uses, so the Kit's per-mesh bone list stays tight
    used_names = set()
    for v in obj.data.vertices:
        for g in v.groups:
            if g.weight > 1e-6:
                used_names.add(obj.vertex_groups[g.group].name)
    for name in [vg.name for vg in obj.vertex_groups if vg.name not in used_names]:
        obj.vertex_groups.remove(obj.vertex_groups[name])       # by name: removing shifts the indices
    return {"zero_weight_after": zero, "groups_kept": len(obj.vertex_groups)}


def smooth_weights(obj, factor=0.5, iterations=1):
    """One-ring Laplacian smoothing of vertex weights over mesh edges (all groups), then re-tidy.
    Softens the seams a nearest-surface transfer leaves and the isolated outliers heat-mapping leaves."""
    me = obj.data
    nbrs = [[] for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices
        nbrs[a].append(b)
        nbrs[b].append(a)
    ng = len(obj.vertex_groups)
    W = [[0.0] * ng for _ in me.vertices]
    for v in me.vertices:
        for g in v.groups:
            W[v.index][g.group] = g.weight
    for _ in range(iterations):
        W2 = []
        for i, row in enumerate(W):
            if not nbrs[i]:
                W2.append(row)
                continue
            avg = [0.0] * ng
            for j in nbrs[i]:
                for k in range(ng):
                    avg[k] += W[j][k]
            n = float(len(nbrs[i]))
            W2.append([row[k] * (1 - factor) + (avg[k] / n) * factor for k in range(ng)])
        W = W2
    for v in me.vertices:
        for g in list(v.groups):
            obj.vertex_groups[g.group].remove([v.index])
        for k, w in enumerate(W[v.index]):
            if w > 1e-4:
                obj.vertex_groups[k].add([v.index], w, "REPLACE")
    return tidy_weights(obj)


def rigid_skull(obj, arm_world_of_bone_head, margin=0.03):
    """Head meshes: every vertex above the neck joint (plus margin) belongs 100% to `head`: a skull does not
    deform, and it stops eye/teeth sub-parts inheriting neck or spine weight from the nearest donor surface."""
    if "head" not in obj.vertex_groups:
        obj.vertex_groups.new(name="head")
    z_neck = arm_world_of_bone_head("neck").z
    head_g = obj.vertex_groups["head"]
    n = 0
    for v in obj.data.vertices:
        if (obj.matrix_world @ v.co).z > z_neck + margin:
            for g in list(v.groups):
                obj.vertex_groups[g.group].remove([v.index])
            head_g.add([v.index], 1.0, "REPLACE")
            n += 1
    return n


# Characters in these exports face Blender -Y (FixBoneForBlender), so a vertex's distance in front of a joint is
# joint.y - vertex.y. The cave troll's jaw underside lies 16 to 32 cm in front of the neck joint and its throat
# 4 to 12 cm (measured 2026-09-24), so 14 cm separates them; a human chin is above the neck joint, in rigid_skull.
JAW_FORWARD_MIN_M = 0.14


def rigid_jaw(obj, arm_world_of_bone_head, forward_min=JAW_FORWARD_MIN_M):
    """Head meshes: every vertex more than `forward_min` in front of the neck joint belongs 100% to `head`. The
    jaw is part of the skull (the skeleton has no jaw bone), but a creature's can hang below the neck joint's
    height, out of rigid_skull's reach, where a nearest-surface transfer gives it chest weight."""
    if "head" not in obj.vertex_groups:
        obj.vertex_groups.new(name="head")
    y_neck = arm_world_of_bone_head("neck").y
    head_g = obj.vertex_groups["head"]
    n = 0
    for v in obj.data.vertices:
        if y_neck - (obj.matrix_world @ v.co).y >= forward_min:
            for g in list(v.groups):
                obj.vertex_groups[g.group].remove([v.index])
            head_g.add([v.index], 1.0, "REPLACE")
            n += 1
    return n


def non_head_neck_verts(obj):
    """Vertices of a head mesh with any weight on a bone other than head or neck (must be 0)."""
    allowed = {obj.vertex_groups[n].index for n in ("head", "neck") if n in obj.vertex_groups}
    return sum(1 for v in obj.data.vertices if any(g.group not in allowed and g.weight > 1e-4 for g in v.groups))


def reassign_groups(obj, from_names, to_name):
    """Move all weight of the `from_names` groups onto `to_name` (creating it), then re-tidy."""
    if to_name not in obj.vertex_groups:
        obj.vertex_groups.new(name=to_name)
    to_g = obj.vertex_groups[to_name]
    moved = 0
    for fn in from_names:
        if fn not in obj.vertex_groups:
            continue
        fg = obj.vertex_groups[fn]
        for v in obj.data.vertices:
            w = 0.0
            for g in v.groups:
                if g.group == fg.index:
                    w = g.weight
            if w > 0:
                cur = 0.0
                for g in v.groups:
                    if g.group == to_g.index:
                        cur = g.weight
                to_g.add([v.index], cur + w, "REPLACE")
                fg.remove([v.index])
                moved += 1
    tidy_weights(obj)
    return moved


def rigid_subparts(obj, bone_name, keep_material_index=0):
    """Vertices used only by polygons of a material other than `keep_material_index` (eyes, teeth, mouth
    interior) get 100% `bone_name`."""
    me = obj.data
    main = set()
    other = set()
    for poly in me.polygons:
        (main if poly.material_index == keep_material_index else other).update(poly.vertices)
    target = other - main
    if not target:
        return 0
    if bone_name not in obj.vertex_groups:
        obj.vertex_groups.new(name=bone_name)
    g = obj.vertex_groups[bone_name]
    for vi in target:
        v = me.vertices[vi]
        for gg in list(v.groups):
            obj.vertex_groups[gg.group].remove([vi])
        g.add([vi], 1.0, "REPLACE")
    return len(target)


def weight_stats(obj):
    n = len(obj.data.vertices)
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
    return {"verts": n, "zero": zero, "over4": over4, "unnorm": unnorm, "groups": len(obj.vertex_groups)}


def bind(mesh, arm):
    for md in list(mesh.modifiers):
        if md.type == "ARMATURE":
            mesh.modifiers.remove(md)
    md = mesh.modifiers.new("Armature", "ARMATURE")
    md.object = arm
    md.use_vertex_groups = True


# ------------------------------------------------------------------ QA
def _edge_stretch(mesh_obj, depsgraph):
    """max and 99th-percentile ratio of deformed/rest edge length."""
    ev = mesh_obj.evaluated_get(depsgraph)
    me = ev.to_mesh()
    rest = mesh_obj.data
    ratios = []
    for e in rest.edges:
        a, b = e.vertices
        lr = (rest.vertices[a].co - rest.vertices[b].co).length
        ld = (me.vertices[a].co - me.vertices[b].co).length
        if lr > 1e-6:
            ratios.append(ld / lr)
    ev.to_mesh_clear()
    if not ratios:
        return {}
    ratios.sort()
    return {"max": round(ratios[-1], 3), "p99": round(ratios[int(len(ratios) * 0.99) - 1], 3), "min": round(ratios[0], 3)}


QA_POSES = {
    # bone: (axis in the bone's local frame, degrees). Engine frames: X along the bone, so Y/Z bend it.
    "thigh_30": [("l_thigh", "Y", 30), ("r_thigh", "Y", -30)],
    "knee_60": [("l_calf", "Y", 60), ("r_calf", "Y", 60)],
    "shoulder_60": [("l_upperarm_twist", "Z", 60), ("r_upperarm_twist", "Z", -60)],
    "elbow_70": [("l_foretwist", "Y", 70), ("r_foretwist", "Y", 70)],
    "spine_30": [("spine", "Y", 15), ("spine1", "Y", 15)],
    "neck_40": [("neck", "Y", 20), ("head", "Y", 20)],
    # the other way: a chin pinned below the head stretches here (the 2026-09-18 troll passed neck_40 at 2.05x)
    "neck_back_40": [("neck", "Y", -20), ("head", "Y", -20)],
}


def _apply_pose(arm, spec):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    for bone, axis, deg in spec:
        pb = arm.pose.bones.get(bone)
        if pb is not None:
            pb.rotation_quaternion = Quaternion({"X": (1, 0, 0), "Y": (0, 1, 0), "Z": (0, 0, 1)}[axis], math.radians(deg))
    bpy.context.view_layer.update()


def qa_render(triples, arm, out_dir, label):
    """triples: (donor_ref, old_copy, new_copy) meshes in engine space, bound to `arm` (engine rest frames).
    For each standard bend in QA_POSES measure edge stretch on all three (the donor is TaleWorlds' skinning
    of the human body, the quality reference), then render old vs new on the combined pose from the front."""
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.display.shading.light = "STUDIO"
    scn.display.shading.color_type = "MATERIAL"
    scn.render.resolution_x, scn.render.resolution_y = 520, 760
    cam_data = bpy.data.cameras.new("_qa_cam")
    cam = bpy.data.objects.new("_qa_cam", cam_data)
    scn.collection.objects.link(cam)
    scn.camera = cam
    cam.data.lens = 40
    all_meshes = [m for t in triples for m in t if m is not None]
    results = {}
    for donor, old, new in triples:
        key = new.name.replace("_NEW", "")
        results[key] = {}
        for pose_name, spec in QA_POSES.items():
            _apply_pose(arm, spec)
            dg = bpy.context.evaluated_depsgraph_get()
            results[key][pose_name] = {tag: _edge_stretch(ob, dg) for tag, ob in (("donor", donor), ("old", old), ("new", new)) if ob is not None}
        for pose_name in ("shoulder_60", "knee_60", "neck_40", "neck_back_40"):
            _apply_pose(arm, QA_POSES[pose_name])
            for tag, ob in (("old", old), ("new", new)):
                for m in all_meshes:
                    m.hide_render = (m is not ob)
                target = Vector((0.0, 0.0, 0.95))
                cam.location = target + Vector((1.6, 2.6, 0.3))      # the character faces +Y at rest in engine space
                cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
                p = os.path.join(out_dir, "%s_%s_%s_%s.png" % (label, key, pose_name, tag))
                scn.render.filepath = p
                bpy.ops.render.render(write_still=True)
                results[key].setdefault("png", {})["%s_%s" % (pose_name, tag)] = p
    for m in all_meshes:
        m.hide_render = False
    _apply_pose(arm, [])
    return {"poses": list(QA_POSES), "meshes": results}


# ---------------------------------------------------------------- main
def process_target(target_fbx, donor_full_fn, out_dir, args, report):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    donor_objs, donor_arms, donor_meshes = _import_fbx(args.donor)
    donor_full = join_copies([m for m in donor_meshes if ".lod" not in m.name], "human_full_donor")
    tgt_objs, tgt_arms, tgt_meshes = _import_fbx(target_fbx)
    tgt_arm = [a for a in tgt_arms if a.name.endswith("_notused")] or tgt_arms
    tgt_arm = tgt_arm[0]
    entry = {"target": target_fbx, "armature": tgt_arm.name, "obj_rot_deg": [round(v * 57.2958, 1) for v in tgt_arm.rotation_euler], "meshes": {}}
    # sanity: donor and target joints coincide
    d_arm = [a for a in donor_arms if a.name.endswith("_notused")][0]
    worst = 0.0
    for b in tgt_arm.data.bones:
        if b.name in d_arm.data.bones:
            worst = max(worst, ((tgt_arm.matrix_world @ b.head_local) - (d_arm.matrix_world @ d_arm.data.bones[b.name].head_local)).length)
    entry["joint_offset_vs_donor_max_m"] = round(worst, 4)
    if worst > 0.02:
        raise RuntimeError("target joints are %.3f m off the donor's: not a same-skeleton re-skin" % worst)

    lod0 = [m for m in tgt_meshes if ".lod" not in m.name]
    lods = [m for m in tgt_meshes if ".lod" in m.name]
    old_copies = {}
    for m in lod0:
        before = weight_stats(m)
        oc = m.copy()
        oc.data = m.data.copy()
        oc.name = m.name + "_OLD"
        bpy.context.scene.collection.objects.link(oc)
        old_copies[m.name] = oc
        name = m.name.lower()
        bone_head = lambda b: tgt_arm.matrix_world @ tgt_arm.data.bones[b].head_local   # noqa: E731
        tidy = {}
        if "bracer" in name:
            # forearm plates: the shipped weights were already clean (0 unnormalised, sharp per segment) and a
            # nearest-surface transfer from the human forearm only softens them; keep, just enforce the limits
            tidy["kept_original"] = True
            tidy.update(tidy_weights(m))
        else:
            transfer_weights(donor_full, m, "POLYINTERP_NEAREST")
            tidy.update(tidy_weights(m))
            if "helmet" in name:
                tidy["rigid_skull_verts"] = rigid_skull(m, bone_head, margin=-10.0)     # a helmet is rigid: all head
            elif "head" in name:
                # skull rigid above the neck joint and jaw rigid in front of it; eyes and mouth are separate
                # material parts inside the skull, they ride the head 100% (pinned before the smoothing too, so the
                # lips do not average in the transfer's weights from the mouth interior); everything else on a
                # head mesh follows the neck, never the chest; smoothing then blends the skull and jaw edges into it
                tidy["rigid_skull_verts"] = rigid_skull(m, bone_head)
                tidy["rigid_jaw_verts"] = rigid_jaw(m, bone_head)
                rigid_subparts(m, "head", keep_material_index=0)
                others = [vg.name for vg in m.vertex_groups if vg.name not in ("head", "neck")]
                tidy["reassigned_to_neck"] = reassign_groups(m, others, "neck")
                tidy["smoothed"] = smooth_weights(m, factor=0.5, iterations=3)
                tidy["rigid_subparts_verts"] = rigid_subparts(m, "head", keep_material_index=0)
                tidy["non_head_neck_verts"] = non_head_neck_verts(m)
            elif "armor" in name or "armour" in name:
                # torso plate: the gorget must ride the torso, not the head; move head/neck weight onto spine2
                tidy["reassigned_to_spine2"] = reassign_groups(m, ("head", "neck"), "spine2")
                tidy["smoothed"] = smooth_weights(m, factor=0.5, iterations=1)
            else:
                tidy["smoothed"] = smooth_weights(m, factor=0.5, iterations=1)
        after = weight_stats(m)
        entry["meshes"][m.name] = {"before": before, "after": after, "tidy": tidy}
    for m in lods:
        base = m.name.split(".lod")[0]
        src = next((x for x in lod0 if x.name == base), None)
        if src is None:
            entry["meshes"][m.name] = {"error": "no LOD0 named %s" % base}
            continue
        before = weight_stats(m)
        transfer_weights(src, m, "NEAREST")
        tidy = tidy_weights(m)
        entry["meshes"][m.name] = {"before": before, "after": weight_stats(m), "tidy": tidy}

    # QA in engine space on duplicates: donor reference, old weights, new weights
    if args.engine_skeleton:
        os.makedirs(args.preview, exist_ok=True)
        import retarget_mannequin_to_human as rt   # noqa: E402  (build_engine_rig)
        eng = rt.build_engine_rig(args.engine_skeleton, name="qa_engine_rig")
        turn = Matrix.Rotation(math.pi, 4, "Z")
        donor_by_role = {}
        for dm in donor_meshes:
            if ".lod" in dm.name:
                continue
            for role in ("body", "hands", "feet", "head"):
                if role in dm.name:
                    donor_by_role[role] = dm

        def role_of(name):
            n = name.lower()
            for role, mapped in (("hands", "hands"), ("feet", "feet"), ("head", "head"), ("bracer", "hands"), ("helmet", "head")):
                if role in n:
                    return mapped
            return "body"

        triples = []
        qa_objs = []
        for m in lod0:
            nc = m.copy()
            nc.data = m.data.copy()
            nc.name = m.name + "_NEW"
            bpy.context.scene.collection.objects.link(nc)
            ref_src = donor_by_role.get(role_of(m.name))
            ref = None
            if ref_src is not None:
                ref = ref_src.copy()
                ref.data = ref_src.data.copy()
                ref.name = "donor_ref_" + m.name
                bpy.context.scene.collection.objects.link(ref)
            for c in (old_copies[m.name], nc, ref):
                if c is None:
                    continue
                # the FBX importer parents with a compensating parent-inverse, so world = identity and the
                # coordinates are FixBone WORLD space (facing -Y); the engine rig faces +Y: turn 180 deg
                c.parent = None
                c.matrix_world = turn
                bind(c, eng)
                qa_objs.append(c)
            triples.append((ref, old_copies[m.name], nc))
        for o in lod0 + lods + [donor_full] + donor_meshes:
            o.hide_render = True
        entry["qa"] = qa_render(triples, eng, args.preview, os.path.splitext(os.path.basename(target_fbx))[0])
        for c in qa_objs:
            bpy.data.objects.remove(c, do_unlink=True)
        bpy.data.objects.remove(eng, do_unlink=True)
    else:
        for oc in old_copies.values():
            bpy.data.objects.remove(oc, do_unlink=True)

    # export: original armature + all target meshes, nothing else
    for o in donor_objs + [donor_full]:
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except Exception:
            pass
    out_path = os.path.join(out_dir, os.path.basename(target_fbx))
    _select_only([tgt_arm] + tgt_meshes, tgt_arm)
    bpy.ops.export_scene.fbx(
        filepath=out_path, use_selection=True, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        axis_forward="-Y", axis_up="Z", bake_anim=False,
        apply_scale_options="FBX_SCALE_NONE", global_scale=1.0,
        use_mesh_modifiers=False, path_mode="AUTO")
    entry["export"] = {"path": out_path, "bytes": os.path.getsize(out_path) if os.path.exists(out_path) else 0}
    report["targets"][os.path.basename(target_fbx)] = entry


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--donor", required=True)
    ap.add_argument("--target", action="append", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--engine-skeleton", default=None)
    ap.add_argument("--preview", default=None)
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    report = {"donor": args.donor, "targets": {}}
    for t in args.target:
        try:
            process_target(t, None, args.out, args, report)
        except Exception:
            report["targets"][os.path.basename(t)] = {"error": traceback.format_exc()}
        with open(os.path.join(args.out, "reskin_report.json"), "w") as fh:
            json.dump(report, fh, indent=1)
    bad = [name for name, e in report["targets"].items() if "error" in e]
    bad += ["%s/%s" % (name, mesh) for name, e in report["targets"].items() for mesh, me in e.get("meshes", {}).items()
            if me.get("tidy", {}).get("non_head_neck_verts", 0) > 0]
    open(os.path.join(args.out, "reskin_report.json.DONE"), "w").write(("fail: " + ", ".join(bad) if bad else "done") + "\n")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        _out = _argv[_argv.index("--out") + 1] if "--out" in _argv else os.getcwd()
        os.makedirs(_out, exist_ok=True)
        with open(os.path.join(_out, "reskin_error.log"), "w") as fh:
            fh.write(traceback.format_exc())
        raise
