#!/usr/bin/env python3
"""
Export KEYForce's hill troll war hammer from his troll .blend as an Armory weapon FBX (2026-09-26).

WHY
KEYForce's `troll_rig_base_01.blend` carries the hammer as three unrigged objects beside the troll:
`SM_TR_Hammer_Blade_A` (the head, 1.21 m wide on X, 0.70 m on Z), `SM_TR_Hammer_Handle_A` (3.51 m on Z) and
`bo_SM_TR_Hammer_Blade_A` (its collision body). `tools/blender/export_rig_for_kit.py` exports only meshes skinned
to `troll_skeleton_a`, so the hammer never reached the Armory; only its textures and `t_tr_hill_troll_hammer_a`
material did.

WHAT IT DOES
Keeps the three objects, renames them to the Armory weapon convention (the cave troll weapons,
`AssetSources/weapons/Mordor/troll/wm_cave_troll_ws_1.fbx`): the visible pieces `wm_hill_troll_2h_hammer_head.lod0`
and `wm_hill_troll_2h_hammer_handle.lod0`, the body `bo_wm_hill_troll_2h_hammer_head` (a weapon's body is `bo_` +
its own mesh id: docs/ai-includes/weapon-creation-workflow.md Step D). The visible pieces take the material
`t_tr_hill_troll_hammer_a`, which the Kit binds by name to the material already imported with the troll; the body
takes `metal_iron`, the physics material name the cave troll mace body carries. The pieces stay where KEYForce put
them: centred on their origin with their length along Z, which is the convention the crafting system assumes (a
piece's default distance to its neighbours is half its length, `TaleWorlds.Core.CraftingPiece`; the cave troll
mace pieces sit the same way). Written to `AssetSources/weapons/Mordor/troll/wm_hill_troll_ws_1.fbx` through a
staged export and a re-import check; an existing file is refused.

    blender-launcher.exe -b --factory-startup -P tools/oneoff/export_hill_troll_hammer.py -- \\
        --blend <troll_rig_base_01.blend> --out <Armory>/AssetSources/weapons/Mordor/troll/wm_hill_troll_ws_1.fbx
Report `<out>.export-report.json` (the launcher detaches: poll until "stage" is "done"). Then
`tools/blender/add_mesh_lods.py` for LOD1 to LOD5, and a Modding Kit import.
"""
import json
import os
import sys

import bpy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import add_mesh_lods as aml  # noqa: E402

RENAMES = {
    "SM_TR_Hammer_Blade_A": ("wm_hill_troll_2h_hammer_head.lod0", "t_tr_hill_troll_hammer_a"),
    "SM_TR_Hammer_Handle_A": ("wm_hill_troll_2h_hammer_handle.lod0", "t_tr_hill_troll_hammer_a"),
    "bo_SM_TR_Hammer_Blade_A": ("bo_wm_hill_troll_2h_hammer_head", "metal_iron"),
}


def args_of(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {}
    for i in range(0, len(argv) - 1, 2):
        out[argv[i].lstrip("-")] = argv[i + 1]
    if not out.get("blend") or not out.get("out"):
        raise SystemExit("--blend and --out are required")
    return out


def material(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def main():
    args = args_of(list(sys.argv))
    out = args["out"]
    report = {"blend": args["blend"], "out": out, "stage": "start", "ok": False, "diffs": [], "objects": {}}
    report_path = out + ".export-report.json"

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        if os.path.exists(out):
            raise SystemExit("%s exists; refusing to overwrite it" % out)
        save("opening")
        bpy.ops.wm.open_mainfile(filepath=args["blend"])
        missing = [n for n in RENAMES if n not in bpy.data.objects]
        if missing:
            raise SystemExit("not in the .blend: %s" % ", ".join(missing))
        for o in list(bpy.data.objects):
            if o.name not in RENAMES:
                bpy.data.objects.remove(o, do_unlink=True)
        expected = {}
        for old, (new, mat) in RENAMES.items():
            o = bpy.data.objects[old]
            if o.parent is not None or any(abs(x) > 1e-6 for x in o.location) or any(abs(x) > 1e-6 for x in o.rotation_euler):
                raise SystemExit("%s is not at the origin, unparented, unrotated; the piece convention needs it" % old)
            o.name = new
            o.data.name = new
            o.data.materials.clear()
            o.data.materials.append(material(mat))
            report["objects"][new] = {"from": old, "verts": len(o.data.vertices), "polys": len(o.data.polygons),
                                      "uv_layers": len(o.data.uv_layers), "material": mat,
                                      "dims": [round(x, 4) for x in o.dimensions]}
            expected[new] = (len(o.data.vertices), len(o.data.polygons), [mat])
        staged = os.path.splitext(out)[0] + ".staged.fbx"
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        after = aml.fingerprint()
        for name, (verts, polys, mats) in expected.items():
            a = after.get(name)
            if a is None:
                report["diffs"].append("lost %s" % name)
                continue
            if (a["verts"], a["polys"], a["materials"]) != (verts, polys, mats):
                report["diffs"].append("%s: %r -> %r" % (name, (verts, polys, mats), (a["verts"], a["polys"], a["materials"])))
        extra = sorted(set(after) - set(expected))
        if extra:
            report["diffs"].append("unexpected objects: %s" % ", ".join(extra))
        report["ok"] = not report["diffs"]
        if report["ok"]:
            os.replace(staged, out)
            report["written"] = out
    except SystemExit as exc:
        report["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001
        import traceback
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["traceback"] = traceback.format_exc()
    save("done")


if __name__ == "__main__":
    main()
