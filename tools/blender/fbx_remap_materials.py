"""Point FBX material slots at a different material, for a Modding Kit import (headless Blender).

The Kit binds each mesh to the material whose NAME the FBX gives it, and it resets every mesh to that name on a
reimport. A mesh whose FBX material has no Kit counterpart warns "Unable to find material X" and renders unbound;
a hand reassignment in the Kit is lost on the next reimport. Measured 2026-09-18: `LOME_troll.fbx`'s head meshes
(`lotr_troll_head`, lod0..lod5) have named `lotr_troll_head` since before the 2026-09-18 re-skin, the Kit has never
had that material, and the March import had been fixed by hand to `base_body_olog`.

This re-exports the FBX with the slots remapped and NOTHING else changed: the same import and export settings as
`reskin_to_human_skeleton.py` (automatic_bone_orientation off; armature + mesh, primary Y / secondary X, -Y forward,
Z up, no leaf bones, no scale baking, no modifiers), then re-imports both files and compares every mesh's vertex
count, every vertex group's weight sum and every bone's rest matrix. The source is backed up first (write-once
`<file>.bak-matremap`). The launcher detaches: read `<out>.matremap.json` (and `.matremap_error.log` on a crash).

  blender-launcher.exe -b -P tools/blender/fbx_remap_materials.py -- --fbx <file.fbx> --map lotr_troll_head=base_body_olog [--apply]

Without --apply it writes the remapped FBX beside the source as `<stem>.matremap.fbx` and changes nothing else.
"""
import bpy, json, os, shutil, sys, traceback, argparse


def _import(path):
    pre = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    return [o for o in bpy.data.objects if o not in pre]


def _fingerprint(objs):
    fp = {"meshes": {}, "bones": {}, "materials": {}}
    for o in objs:
        if o.type == "MESH":
            sums = {}
            for vg in o.vertex_groups:
                sums[vg.name] = 0.0
            for v in o.data.vertices:
                for g in v.groups:
                    sums[o.vertex_groups[g.group].name] += g.weight
            fp["meshes"][o.name] = {"verts": len(o.data.vertices), "polys": len(o.data.polygons),
                                    "groups": {k: round(v, 4) for k, v in sums.items()}}
            fp["materials"][o.name] = [s.material.name if s.material else None for s in o.material_slots]
        elif o.type == "ARMATURE":
            for b in o.data.bones:
                fp["bones"][b.name] = [round(x, 5) for row in b.matrix_local for x in row]
    return fp


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--map", required=True, nargs="+", help="old=new material names")
    ap.add_argument("--apply", action="store_true", help="replace the source FBX (after a write-once backup)")
    args = ap.parse_args(argv)
    mapping = dict(m.split("=", 1) for m in args.map)
    stem = os.path.splitext(args.fbx)[0]
    report = {"fbx": args.fbx, "map": mapping, "apply": args.apply}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = _import(args.fbx)
    before = _fingerprint(objs)
    missing = [new for new in mapping.values() if new not in bpy.data.materials]
    if missing:
        raise RuntimeError("target material(s) not in the FBX: %s (present: %s)" % (missing, sorted(m.name for m in bpy.data.materials)))
    changed = []
    for o in objs:
        if o.type != "MESH":
            continue
        for i, slot in enumerate(o.material_slots):
            if slot.material and slot.material.name in mapping:
                old = slot.material.name
                slot.material = bpy.data.materials[mapping[old]]
                changed.append("%s[%d]: %s -> %s" % (o.name, i, old, slot.material.name))
    report["slots_changed"] = changed
    if not changed:
        raise RuntimeError("no slot uses any of %s" % sorted(mapping))
    out = stem + ".matremap.fbx"
    for o in bpy.data.objects:
        o.select_set(o in objs and o.type in ("ARMATURE", "MESH"))
    bpy.context.view_layer.objects.active = next(o for o in objs if o.type == "ARMATURE")
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=True, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        axis_forward="-Y", axis_up="Z", bake_anim=False,
        apply_scale_options="FBX_SCALE_NONE", global_scale=1.0,
        use_mesh_modifiers=False, path_mode="AUTO")

    # re-import the result and compare everything except the remapped material names
    bpy.ops.wm.read_factory_settings(use_empty=True)
    after = _fingerprint(_import(out))
    diffs = []
    if sorted(before["meshes"]) != sorted(after["meshes"]):
        diffs.append("mesh set differs")
    for n, m in before["meshes"].items():
        a = after["meshes"].get(n)
        if a is None:
            continue
        if (m["verts"], m["polys"]) != (a["verts"], a["polys"]):
            diffs.append("%s: verts/polys %s -> %s" % (n, (m["verts"], m["polys"]), (a["verts"], a["polys"])))
        for g, w in m["groups"].items():
            if abs(a["groups"].get(g, -1.0) - w) > 1e-3:
                diffs.append("%s: group %s weight sum %s -> %s" % (n, g, w, a["groups"].get(g)))
    worst = 0.0
    for b, mat in before["bones"].items():
        if b not in after["bones"]:
            diffs.append("bone missing: " + b)
            continue
        worst = max(worst, max(abs(x - y) for x, y in zip(mat, after["bones"][b])))
    report["bone_matrix_max_diff"] = worst
    if worst > 1e-4:
        diffs.append("bone rest matrices moved by %.2e" % worst)
    expected = {n: [mapping.get(x, x) for x in mats] for n, mats in before["materials"].items()}
    if expected != after["materials"]:
        diffs.append("materials after export are not the expected remap")
    report["materials_after"] = after["materials"]
    report["diffs"] = diffs
    report["ok"] = not diffs
    if args.apply and not diffs:
        if not os.path.exists(args.fbx + ".bak-matremap"):
            shutil.copy2(args.fbx, args.fbx + ".bak-matremap")
        shutil.move(out, args.fbx)
        report["applied"] = True
    with open(stem + ".matremap.json", "w") as fh:
        json.dump(report, fh, indent=1)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        _fbx = _argv[_argv.index("--fbx") + 1] if "--fbx" in _argv else os.path.join(os.getcwd(), "x.fbx")
        with open(os.path.splitext(_fbx)[0] + ".matremap_error.log", "w") as fh:
            fh.write(traceback.format_exc())
        raise
