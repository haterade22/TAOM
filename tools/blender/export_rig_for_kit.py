"""Export an authored skeleton and its skinned meshes as ONE FBX for a Modding Kit import (headless Blender).

    blender-launcher.exe -b <source.blend> -P tools/blender/export_rig_for_kit.py -- ^
        --armature troll_skeleton_a ^
        --slot hill_troll_a_body=SK_troll_hill_body,SK_troll_hill_shoulder,SK_troll_hill_cloth1 ^
        --slot hill_troll_a_head=SK_troll_hill_head.base ^
        --slot hill_troll_a_head.eye=SK_troll_hill_head.eyes ^
        --slot hill_troll_a_head.mouth=SK_troll_hill_head.mouth ^
        --out <dir>\\hill_troll_a.fbx

The source .blend is only read: the joins and weight edits happen on copies and nothing is saved. Each --slot joins
the listed mesh objects (copies) into one object named after the slot, because the Kit makes one mesh per FBX
object and a race skin names one mesh per slot (body, head, hands, legs). A bare --slot name=object renames.
A slot named <mesh>.<part> becomes a named sub-mesh of <mesh> in the Kit, the dwarf's head layout
(SM_Dwarf_Basemesh_A1_head, _head.eye, _head.mouth, which the Kit tags face_base_mesh, face_eye_mesh and
face_mouth_mesh); one object carrying two materials comes in as <mesh>.0 and <mesh>.1 instead, with no mouth part.

Pre-Kit gates (docs/community/bannerlordmodding-lt/guides/custom_creature_skeleton.md, creature-mount-authoring.md):
  - a vertex group that is not a bone of --armature is dropped when it carries no weight, and refused when it does
    (its vertices would lose that share in the Kit);
  - at most 4 influences per vertex (the engine's limit: the Kit drops the rest, so the game would bend the mesh
    differently from Blender): the strongest 4 are kept, weights under 0.01 dropped, each vertex normalised to 1;
  - no vertex left without weight (refused).
--bone-frames JSON sets bones to engine-space frames from tools/tpac_skeleton_copy_physics.py before export:
--missing-bones adds the bones the rig lacks (the grip bones l_finger0 / r_finger0 a Monster holds items on, which
troll_skeleton_a was authored without); --reframe also turns every existing bone about its head to the human's
axes, so human clips (joint rotations) bend the rig right. Heads never move, and the mesh and weights are untouched:
at rest a bone's orientation does not move its skin. A bone's tail then points along its new Y axis, which Blender
allows and the engine ignores. The engine-to-Blender axis map is found from the existing bones' heads in the same JSON (the
Kit import of this export gives engine = (-x, -y, z)) and must hold within 1 mm and 0.5 deg on every bone's head
and axes; each set bone is read back against its engine frame to the same tolerance, or the run fails.
--material OLD=NEW renames a material on export (repeatable). The Kit binds each mesh to the material its FBX
names, looks that name up across the whole module, and resets the binding on every re-import, so the FBX must carry
the Kit material's exact name: the hill troll's first import bound KEYForce's M_HillTroll_* names to the OLD
hill troll's March materials (m_hilltroll_*_a in Trolls\\Hill Troll\\textures, old textures) instead of the
t_tr_hill_troll_*_a materials made for it. A missing OLD, or a NEW already naming another material, is refused.
Export: object_types ARMATURE+MESH, primary Y / secondary X, forward -Y, up Z, no leaf bones, no animation, no
scale baking, no modifiers, the armature under its REAL name (the Kit registers it as the skeleton; `_notused` is
for animation exports). Material slots no face uses are dropped (they would reach the Kit as empty sub-meshes).
Then the FBX is re-imported and checked: every bone present with its head within 1 mm and its rest orientation
within 0.5 deg (the engine skins from a bone's origin and rotation; a leaf bone's tail is recomputed on import and
is not compared: docs/features/spider-skeleton-animation-pipeline.md gotcha 4), every slot present, every vertex's
weights summing to 1 and at most 4. Report <out>.report.json and
<out>.DONE ("ok" or "fail: ..."); the launcher detaches, so .DONE is the completion signal.
"""
import argparse
import itertools
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Matrix, Vector

LIMIT = 4
FLOOR = 0.01


def _args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--armature", required=True)
    ap.add_argument("--slot", action="append", required=True, help="name=obj1,obj2,...")
    ap.add_argument("--material", action="append", default=[], help="OLD=NEW: rename a material on export")
    ap.add_argument("--bone-frames", help="JSON from tpac_skeleton_copy_physics.py --missing-bones or --reframe")
    ap.add_argument("--out", required=True)
    return ap.parse_args(argv)


def _select_only(objs, active):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active


def _copy(obj, name):
    c = obj.copy()
    c.data = obj.data.copy()
    c.name = name
    bpy.context.scene.collection.objects.link(c)
    return c


def build_slot(name, sources, arm):
    parts = [_copy(s, name + "_part") for s in sources]
    for p in parts:
        # keep one Armature modifier on the armature being exported, the parenting as authored
        for m in list(p.modifiers):
            if m.type == "ARMATURE":
                m.object = arm
    if len(parts) > 1:
        _select_only(parts, parts[0])
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active if len(parts) > 1 else parts[0]
    obj.name = name
    obj.data.name = name
    used = {p.material_index for p in obj.data.polygons}
    for i in reversed(range(len(obj.material_slots))):
        if i not in used:
            obj.active_material_index = i
            with bpy.context.temp_override(object=obj, active_object=obj):
                bpy.ops.object.material_slot_remove()
    return obj


def clean_weights(obj, bones):
    """Drop empty non-bone groups, refuse weighted ones; keep the strongest 4, drop < FLOOR, normalise."""
    rep = {"dropped_empty_groups": [], "weighted_non_bone_groups": {}, "over4_before": 0, "unnorm_before": 0,
           "zero_weight_after": 0}
    names = {g.index: g.name for g in obj.vertex_groups}
    non_bone = {i for i, n in names.items() if n not in bones}
    for v in obj.data.vertices:
        ws = [(g.group, g.weight) for g in v.groups if g.weight > 1e-6]
        for gi, w in ws:
            if gi in non_bone:
                rep["weighted_non_bone_groups"][names[gi]] = rep["weighted_non_bone_groups"].get(names[gi], 0) + 1
        if len(ws) > LIMIT:
            rep["over4_before"] += 1
        s = sum(w for _, w in ws)
        if ws and abs(s - 1.0) > 0.02:
            rep["unnorm_before"] += 1
    if rep["weighted_non_bone_groups"]:
        return rep
    for i in sorted(non_bone, reverse=True):
        rep["dropped_empty_groups"].append(names[i])
        obj.vertex_groups.remove(obj.vertex_groups[names[i]])
    for v in obj.data.vertices:
        ws = sorted(((g.group, g.weight) for g in v.groups if g.weight > FLOOR), key=lambda t: -t[1])[:LIMIT]
        total = sum(w for _, w in ws)
        for g in list(v.groups):
            obj.vertex_groups[g.group].remove([v.index])
        if total <= 1e-8:
            rep["zero_weight_after"] += 1
            continue
        for gi, w in ws:
            obj.vertex_groups[gi].add([v.index], w / total, "REPLACE")
    return rep


def _engine_basis(w):
    """Row-convention 16 floats (a tpac rest or world frame) -> the bone's axes as matrix columns."""
    return Matrix([[w[c * 4 + r] for c in range(3)] for r in range(3)])


def _angle_deg(a, b):
    r = a.transposed() @ b
    return math.degrees(math.acos(max(-1.0, min(1.0, (r[0][0] + r[1][1] + r[2][2] - 1) / 2))))


def _frame_error(arm, name, P, w):
    mw = arm.matrix_world @ arm.data.bones[name].matrix_local
    move = (P @ mw.to_translation() - Vector(w[12:15])).length
    return move, _angle_deg(P @ mw.to_3x3(), _engine_basis(w))


def set_bone_frames(arm, path):
    """Set the rig's bones to the engine frames a tpac_skeleton_copy_physics.py JSON carries: --missing-bones (the
    bones it lacks, added) or --reframe (every bone re-oriented about its head, plus the missing ones), converted to
    Blender through the axis map the existing bones give. Every bone is disconnected first, so turning a parent
    never drags a child's head. -> (changed names, report, problems)."""
    with open(path, encoding="utf-8") as fh:
        data = json.load(fh)
    target = data["target_world"]
    shared = [n for n in target if n in arm.data.bones]
    if len(shared) < 3:
        return [], {}, ["--bone-frames: only %d bones shared with the JSON's skeleton" % len(shared)]
    best = None
    for perm in itertools.permutations(range(3)):
        for signs in itertools.product((1.0, -1.0), repeat=3):
            P = Matrix(((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
            for i in range(3):
                P[i][perm[i]] = signs[i]
            if round(P.determinant()) != 1:
                continue
            err = max(_frame_error(arm, n, P, target[n])[0] for n in shared)
            if best is None or err < best[0]:
                best = (err, P)
    err, P = best
    turn = max(_frame_error(arm, n, P, target[n])[1] for n in shared)
    report = {"axis_map": [list(r) for r in P], "shared_bones": len(shared), "map_move_m": round(err, 6),
              "map_turn_deg": round(turn, 3)}
    if err > 0.001 or turn > 0.5:
        return [], report, ["--bone-frames: no axis map fits the rig (%.4f m, %.2f deg)" % (err, turn)]
    Pt = P.transposed()
    to_arm = arm.matrix_world.inverted()
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    edit = arm.data.edit_bones
    for b in edit:
        b.use_connect = False
    changed, problems = [], []
    for spec in data["bones"]:
        m = (Pt @ _engine_basis(spec["world"])).to_4x4()
        m.translation = Pt @ Vector(spec["world"][12:15])
        m = to_arm @ m
        if spec["name"] in edit:
            b = edit[spec["name"]]
            if (b.head - m.translation).length > 0.001:
                problems.append("--bone-frames: %s would move %.4f m; only its axes may change"
                                % (spec["name"], (b.head - m.translation).length))
                continue
            b.matrix = m  # keeps the bone's length
        else:
            if spec["parent"] not in edit:
                problems.append("--bone-frames: %s names parent %s, which the rig lacks" % (spec["name"], spec["parent"]))
                continue
            b = edit.new(spec["name"])
            b.head, b.tail = (0.0, 0.0, 0.0), (0.0, 0.1, 0.0)
            b.matrix = m
            b.parent = edit[spec["parent"]]
            b.use_connect = False
            b.use_deform = True
        changed.append(spec["name"])
    bpy.ops.object.mode_set(mode="OBJECT")
    worst = (0.0, 0.0)
    for spec in data["bones"]:
        if spec["name"] in changed:
            move, ang = _frame_error(arm, spec["name"], P, spec["world"])
            worst = (max(worst[0], move), max(worst[1], ang))
            if move > 0.001 or ang > 0.5:
                problems.append("--bone-frames: %s landed %.4f m / %.2f deg off its engine frame" % (spec["name"], move, ang))
    report.update(set_bones=len(changed), worst_move_m=round(worst[0], 6), worst_turn_deg=round(worst[1], 3))
    return changed, report, problems


def rename_materials(specs):
    """OLD=NEW pairs -> {old: new}, applied to this session's materials (the .blend is never saved)."""
    done, problems = {}, []
    for spec in specs:
        old, _, new = spec.partition("=")
        mat = bpy.data.materials.get(old)
        if mat is None or not new:
            problems.append("material %r not in the file (or no new name given)" % old)
            continue
        other = bpy.data.materials.get(new)
        if other is not None and other is not mat:
            problems.append("material name %r already belongs to another material" % new)
            continue
        mat.name = new
        if mat.name != new:
            problems.append("material %r came out as %r" % (new, mat.name))
            continue
        done[old] = new
    return done, problems


def _base_name(name):
    """Blender suffixes a clashing name on import (t_tr_x.001); compare without it."""
    head, dot, tail = name.rpartition(".")
    return head if dot and tail.isdigit() and len(tail) == 3 else name


def check_reimport(path, arm_name, rest, slot_names):
    pre = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in pre]
    arms = [o for o in new if o.type == "ARMATURE"]
    meshes = {_base_name(o.name): o for o in new if o.type == "MESH"}  # keeps <mesh>.<part> names
    problems = []
    if len(arms) != 1:
        problems.append("expected one armature, found %d" % len(arms))
        return problems, {}
    a = arms[0]
    worst = worst_deg = 0.0
    for name, (h, q) in rest.items():
        b = a.data.bones.get(name)
        if b is None:
            problems.append("bone %s missing after re-import" % name)
            continue
        worst = max(worst, ((a.matrix_world @ b.head_local) - h).length)
        worst_deg = max(worst_deg, math.degrees(q.rotation_difference((a.matrix_world @ b.matrix_local).to_quaternion()).angle))
    if len(a.data.bones) != len(rest):
        problems.append("bone count %d, expected %d" % (len(a.data.bones), len(rest)))
    if worst > 0.001:
        problems.append("a bone head moved %.4f m in the round trip" % worst)
    if worst_deg > 0.5:
        problems.append("a bone's rest orientation turned %.2f deg in the round trip" % worst_deg)
    stats = {}
    for s in slot_names:
        m = meshes.get(s)
        if m is None:
            problems.append("slot %s missing after re-import" % s)
            continue
        bad_sum = over = 0
        for v in m.data.vertices:
            ws = [g.weight for g in v.groups if g.weight > 1e-4]
            if len(ws) > LIMIT:
                over += 1
            if abs(sum(ws) - 1.0) > 0.02:
                bad_sum += 1
        stats[s] = {"verts": len(m.data.vertices), "materials": [sl.material.name if sl.material else None for sl in m.material_slots],
                    "over4": over, "unnormalised": bad_sum}
        if over or bad_sum:
            problems.append("slot %s: %d over %d influences, %d not summing to 1" % (s, over, LIMIT, bad_sum))
    return problems, {"armature": a.name, "bones": len(a.data.bones), "bone_order": [b.name for b in a.data.bones],
                      "worst_bone_move_m": round(worst, 6), "worst_bone_turn_deg": round(worst_deg, 3), "slots": stats}


def main():
    args = _args()
    rep = {"source": bpy.data.filepath, "armature": args.armature, "slots": {}, "problems": []}
    out_dir = os.path.dirname(os.path.abspath(args.out))
    os.makedirs(out_dir, exist_ok=True)
    arm = bpy.data.objects[args.armature]
    if args.bone_frames:
        rep["bones_set"], rep["bone_frames"], probs = set_bone_frames(arm, args.bone_frames)
        rep["problems"].extend(probs)
    bones = {b.name for b in arm.data.bones}
    rest = {b.name: (arm.matrix_world @ b.head_local, (arm.matrix_world @ b.matrix_local).to_quaternion()) for b in arm.data.bones}
    rep["bone_count"] = len(bones)
    rep["materials_renamed"], probs = rename_materials(args.material)
    rep["problems"].extend(probs)
    slots = []
    for spec in args.slot:
        name, _, objs = spec.partition("=")
        sources = [bpy.data.objects[n] for n in objs.split(",")]
        bad = [s.name for s in sources if s.type != "MESH"]
        if bad:
            rep["problems"].append("slot %s: not meshes: %s" % (name, bad))
            continue
        obj = build_slot(name, sources, arm)
        cw = clean_weights(obj, bones)
        rep["slots"][name] = {"sources": [s.name for s in sources], "verts": len(obj.data.vertices), **cw}
        if cw["weighted_non_bone_groups"]:
            rep["problems"].append("slot %s: vertex groups that are not bones carry weight: %s" % (name, cw["weighted_non_bone_groups"]))
        if cw["zero_weight_after"]:
            rep["problems"].append("slot %s: %d vertices without weight" % (name, cw["zero_weight_after"]))
        slots.append(obj)
    if not rep["problems"]:
        _select_only([arm] + slots, arm)
        bpy.ops.export_scene.fbx(
            filepath=args.out, use_selection=True, object_types={"ARMATURE", "MESH"},
            add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
            axis_forward="-Y", axis_up="Z", bake_anim=False,
            apply_scale_options="FBX_SCALE_NONE", global_scale=1.0,
            use_mesh_modifiers=False, path_mode="AUTO")
        rep["bytes"] = os.path.getsize(args.out)
        probs, check = check_reimport(args.out, args.armature, rest, [s.name for s in slots])
        rep["reimport"] = check
        rep["problems"].extend(probs)
        stale = sorted({_base_name(m) for s in check.get("slots", {}).values() for m in s["materials"] if m}
                       & set(rep["materials_renamed"]))
        if stale:
            rep["problems"].append("materials still under their old names after the round trip: %s" % stale)
    return rep, args


if __name__ == "__main__":
    report, out = {}, None
    try:
        report, a = main()
        out = a.out
    except Exception:
        report["error"] = traceback.format_exc()
        a = _args()
        out = a.out
    with open(out + ".report.json", "w") as fh:
        json.dump(report, fh, indent=1)
    bad = report.get("problems") or ([report["error"].splitlines()[-1]] if "error" in report else [])
    with open(out + ".DONE", "w") as fh:
        fh.write(("fail: " + "; ".join(bad)) if bad else "ok")
