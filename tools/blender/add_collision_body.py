#!/usr/bin/env python3
"""
Author a missing `bo_` collision twin into the FBX its visible mesh already lives in.

WHY THIS EXISTS
`docs/ai-includes/weapon-creation-workflow.md` states the rule: a blade, head or bow needs a
collision body named `bo_` + the exact mesh id, and a body name no loaded pack ships HANGS the game
rather than looking wrong (`PreloadHelper.WaitForMeshesToBeLoaded` polls every registered body
name and exits only when each resolves). That doc also sanctions a placeholder: borrow a
same-shaped `bo_` from another kit until the artist delivers the real one. On the three Rhun
longbows the placeholder shipped and was never replaced (#633): the bows resolved, as the elven
bow's hull, and stayed tied to art that #599 had already renamed once. `COLLISION_BODY_BORROWED`
in `tools/validate_moduledata.py` now refuses a cross-kit borrow; this tool authors the twin.

The body goes into the SAME FBX as the mesh so one import carries both, and so the mesh's own
package is the one that ships its hull. (Where the cook puts them afterwards is not something
this tool can promise: the release cook groups every body into pack0/pack1 and every mesh
elsewhere, and the engine resolves a body by name process-wide, so co-residency is neither
achieved nor needed.)

WHAT IT DOES
Duplicates the mesh's lowest LOD, renames it `bo_<Mesh>`, gives it the collision material you
name, and re-exports the whole file. That matches how the shipped bodies were authored: in
SK_RH_Loke_Weapons_A.fbx the 18 `bo_SM_RH_Loke_*` objects are low-poly hulls at the mesh's own
dimensions, and several are byte-for-byte the mesh's `.lod4` (Axe_Blade_2H_A 38v/52t,
Mace_Blade_1H_A 114v/224t, Spear_Blade_A 13v/20t).

`--material` is required and should name the item's `physics_material` (`wood_weapon` for a
bow, `metal_weapon` for a blade). The shipped Loke bodies all carry `metal_weapon` and the three
#633 bows were authored with it on 2026-09-21; the item XML's `physics_material` is what
`Mission.cs` hands to the physics engine for hits, so the FBX material is UNVERIFIED to matter,
but the artist-authored elven bow body carries `wood_weapon` and the tool no longer guesses.

HOW TO RUN (Windows, Blender from the Microsoft Store)
    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/add_collision_body.py -- --fbx <file.fbx> --mesh <MeshObject> ^
        --material <physics_material> [--from-lod lod5] [--apply]

The raw blender.exe under WindowsApps is ACL-locked (exit 126); use the launcher. The launcher
DETACHES, so nothing comes back on stdout: this script writes <fbx>.bo-report.json on every path,
a bad argument included, and the caller must poll for that file. A dry run also leaves the staged
`<stem>.bo.fbx` beside the source for inspection in Blender (as `fbx_remap_materials.py` does);
`--apply` consumes it. Move the report, any staged file and the `.bak-bo` out of `AssetSources/`
before the Armory is packaged; only the FBX belongs there, and `sweep_module_backups.ps1` sweeps
the `.bak-bo` but not the other two.

SAFETY
Writes a sibling `.bo.fbx` and re-imports it, then compares every pre-existing object's vertex,
polygon, dimension, location, UV-layer and material state against the original, and the new body
against what was built. Any drift is reported and `--apply` is refused. `--apply` first writes a
write-once `<fbx>.bak-bo` copy of the source, the sibling `fbx_remap_materials.py` convention, so
the round trip is reversible for what the fingerprint does not cover (normals, UV content, colour
attributes, custom properties). A Modding Kit import of the FBX is required afterwards, or the new
name exists in no tpac and the reference is worse than the borrow was.
"""
import json
import os
import shutil
import sys

import bpy

LOD_ORDER = [".lod5", ".lod4", ".lod3", ".lod2", ".lod1", ""]
# Blender truncates an object name past this silently, and the XML would then name a body
# that no tpac ships.
BLENDER_NAME_MAX = 63


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "from_lod": None, "material": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif a in ("--fbx", "--mesh", "--material", "--from-lod"):
            if i + 1 >= len(argv):
                raise SystemExit("%s needs a value" % a)
            out[a[2:].replace("-", "_")] = argv[i + 1]
            i += 2
        else:
            raise SystemExit("unknown argument %r" % a)
    for k in ("fbx", "mesh", "material"):
        if not out.get(k):
            raise SystemExit("--%s is required" % k)
    return out


def fbx_from_argv(argv):
    """Best-effort `--fbx` so a report lands beside the file even when the arguments are bad."""
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    if "--fbx" in argv and argv.index("--fbx") + 1 < len(argv):
        return os.path.abspath(argv[argv.index("--fbx") + 1])
    return os.path.join(os.getcwd(), "add_collision_body")


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)


def fingerprint():
    """Everything we can cheaply prove survived the round trip."""
    fp = {}
    for o in bpy.data.objects:
        if o.type != "MESH":
            fp[o.name] = {"type": o.type}
            continue
        fp[o.name] = {
            "type": "MESH",
            "verts": len(o.data.vertices),
            "polys": len(o.data.polygons),
            "dims": [round(x, 4) for x in o.dimensions],
            "loc": [round(x, 4) for x in o.location],
            "uv_layers": len(o.data.uv_layers),
            "materials": [s.material.name if s.material else None for s in o.material_slots],
        }
    return fp


def pick_source(mesh, from_lod):
    """The lowest LOD present, because that is what the shipped bodies are."""
    if from_lod is not None:
        name = mesh + from_lod if from_lod.startswith(".") else mesh + "." + from_lod
        if name not in bpy.data.objects:
            raise SystemExit("no object %r in this file" % name)
        return bpy.data.objects[name]
    for suffix in LOD_ORDER:
        if mesh + suffix in bpy.data.objects:
            return bpy.data.objects[mesh + suffix]
    raise SystemExit("no object named %r or any %s LOD of it" % (mesh, mesh))


def build_body(mesh, source, material):
    body_name = "bo_" + mesh
    if len(body_name) > BLENDER_NAME_MAX:
        raise SystemExit("%r is %d chars; Blender truncates past %d and the XML ref would "
                         "never resolve" % (body_name, len(body_name), BLENDER_NAME_MAX))
    if body_name in bpy.data.objects:
        raise SystemExit("%r already exists; nothing to author" % body_name)

    body = source.copy()
    body.data = source.data.copy()
    body.name = body_name
    body.data.name = body_name
    bpy.context.scene.collection.objects.link(body)

    # The shipped bodies carry exactly one slot, the collision material, and keep the LOD's UVs.
    mat = bpy.data.materials.get(material) or bpy.data.materials.new(material)
    body.data.materials.clear()
    body.data.materials.append(mat)

    # Unparent first, then place: the other order applies the world matrix and then drops the
    # parent's share of it.
    body.parent = None
    body.matrix_world = source.matrix_world.copy()
    return body


def export(path):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        axis_forward="-Y", axis_up="Z", bake_anim=False,
        apply_scale_options="FBX_SCALE_NONE", global_scale=1.0,
        use_mesh_modifiers=False, path_mode="AUTO")


def compare(before, after, body_name, built):
    """Pre-existing objects must be untouched; the body must have arrived as built."""
    diffs = []
    missing = sorted(set(before) - set(after))
    if missing:
        diffs.append("objects lost in the round trip: %s" % ", ".join(missing))
    added = sorted(set(after) - set(before) - {body_name})
    if added:
        diffs.append("unexpected new objects: %s" % ", ".join(added))
    got = after.get(body_name)
    if got is None:
        diffs.append("%s is not in the exported file" % body_name)
    else:
        for key in ("verts", "polys", "dims", "loc"):
            if got.get(key) != built.get(key):
                diffs.append("%s: %s built %r, re-imported %r" % (body_name, key, built.get(key), got.get(key)))
    for name, b in before.items():
        a = after.get(name)
        if a is None or b.get("type") != "MESH":
            continue
        for key in ("verts", "polys", "dims", "loc", "uv_layers", "materials"):
            if b[key] != a[key]:
                diffs.append("%s: %s %r -> %r" % (name, key, b[key], a[key]))
    return diffs


def main():
    fbx = fbx_from_argv(list(sys.argv))
    report_path = fbx + ".bo-report.json"
    report = {"fbx": fbx, "applied": False, "diffs": [], "ok": False}
    try:
        args = parse_args(list(sys.argv))
        mesh = args["mesh"]
        body_name = "bo_" + mesh
        staged = os.path.splitext(fbx)[0] + ".bo.fbx"
        report.update(mesh=mesh, body=body_name, staged=staged, material=args["material"])

        load(fbx)
        before = fingerprint()
        source = pick_source(mesh, args["from_lod"])
        report["source_object"] = source.name
        body = build_body(mesh, source, args["material"])
        built = {
            "verts": len(body.data.vertices),
            "polys": len(body.data.polygons),
            "dims": [round(x, 4) for x in body.dimensions],
            "loc": [round(x, 4) for x in body.location],
        }
        report["body_built"] = built
        export(staged)

        load(staged)
        after = fingerprint()
        report["diffs"] = compare(before, after, body_name, built)
        report["ok"] = not report["diffs"]

        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-bo"
            if not os.path.exists(backup):
                shutil.copy2(fbx, backup)
            report["backup"] = backup
            os.replace(staged, fbx)
            report["applied"] = True
            report["staged"] = None
    except SystemExit as exc:
        report["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001 - a Blender-side failure must still reach the caller
        report["error"] = "%s: %s" % (type(exc).__name__, exc)

    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=1)


if __name__ == "__main__":
    main()
