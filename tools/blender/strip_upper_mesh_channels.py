#!/usr/bin/env python3
"""
Remove every morph channel (shape key) from a race's hair and beard meshes in an Armory FBX, the vanilla way
(2026-09-28, Saruman `saruman_delivery_2026.03.16.fbx`).

WHY
Vanilla hair and beards carry no morph channels: the 98 hair and beard metameshes in
`Native/EmAssetPackages/pack3/pack3.tpac` have 0 morph frames (vanilla eyebrows were not measured). On the CPU path
the engine moves them with the head's channels through its own per-vertex table (0x56EBA0) and never reads their
own; the GPU path (`gpu_morph_mapping`, 0x209360) is unverified. Mike, 2026-09-28: vanilla is the reference.
docs/reference/race-face-and-hand-morphs.md "Hair, beards and eyebrows".

WHAT IT DOES
For every object named by `--mesh` or a LOD of it (`<mesh>.lod1` and so on), removes all shape keys, basis
included, so the mesh keeps its rest shape. Never touches the head, eye or mouth: face parts need their 101
channels, and one without them crashes the static face morph. Never touches hands, arms or bodies either: the LOD0
hand, arm or full-body mesh carries the 26 hand-pose channels. Refuses a name that looks like a face part, a name
that is not a hair, beard, eyebrow or moustache mesh, a name that is not in the FBX, and a run that would strip
nothing (a re-run, or only empty LODs named) before anything is exported.

SAFETY (fit_eye_morphs.py's shape): a staged `<stem>.stripupper.fbx` is written and re-imported;
`add_mesh_lods.compare` checks every object came back as it was apart from the removed keys. `--apply` writes a
write-once `<fbx>.bak-stripupper` first, then replaces the FBX. Then a Kit re-import and Save, and a check that every
mesh kept its material.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/strip_upper_mesh_channels.py -- --fbx <saruman_delivery_2026.03.16.fbx> ^
        --mesh SK_saruman_hair --mesh SK_saruman_beard [--apply]
The launcher detaches: poll `<fbx>.stripupper-report.json` until "stage" is "done".
"""
import json
import os
import re
import shutil
import sys

FACE_PART = re.compile(r"(^|[._])(head|eye|mouth)($|[._])", re.I)
# Only upper meshes lose their channels; hands, arms and bodies carry the hand-pose channels.
UPPER_PART = re.compile(r"hair|beard|brow|mustache|moustache", re.I)


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "fbx": None, "mesh": []}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif a == "--fbx" and i + 1 < len(argv):
            out["fbx"] = argv[i + 1]
            i += 2
        elif a == "--mesh" and i + 1 < len(argv):
            out["mesh"].append(argv[i + 1])
            i += 2
        else:
            raise SystemExit("bad argument %r" % a)
    if not out["fbx"] or not out["mesh"]:
        raise SystemExit("--fbx and at least one --mesh are required")
    return out


def targets(names, meshes):
    """The objects to strip: each named mesh and its `.lodN` copies. Refuses face parts, anything that is not a hair,
    beard, eyebrow or moustache mesh, and unknown names."""
    out = []
    for m in meshes:
        if FACE_PART.search(m):
            raise SystemExit("%r looks like a face part; face parts keep their channels" % m)
        if not UPPER_PART.search(m):
            raise SystemExit("%r is not a hair, beard or eyebrow mesh; hands, arms and bodies keep their channels" % m)
        found = [n for n in names if n == m or re.fullmatch(re.escape(m) + r"\.lod\d+", n)]
        if m not in found:
            raise SystemExit("no mesh %r in the FBX" % m)
        out.extend(found)
    return sorted(set(out))


def main():
    import bpy  # noqa: F401  (Blender only)
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import add_mesh_lods as aml

    fbx = aml.fbx_from_argv(list(sys.argv))
    report_path = fbx + ".stripupper-report.json"
    report = {"fbx": fbx, "stage": "start", "ok": False, "applied": False, "diffs": []}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        args = parse_args(list(sys.argv))
        staged = os.path.splitext(fbx)[0] + ".stripupper.fbx"
        report["staged"] = staged
        save("loading")
        aml.load(fbx)
        before = aml.fingerprint()
        mesh_names = [o.name for o in bpy.data.objects if o.type == "MESH"]
        strip = targets(mesh_names, args["mesh"])
        removed = {}
        for n in strip:
            ob = bpy.data.objects[n]
            keys = ob.data.shape_keys
            removed[n] = len(keys.key_blocks) - 1 if keys else 0
            if keys:
                ob.shape_key_clear()
        report["removed_channels"] = removed
        if not any(removed.values()):
            raise SystemExit("no channels on %s; nothing to strip" % ", ".join(strip))
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        after = aml.fingerprint()
        report["diffs"] = aml.compare(before, after, {}, rekeyed={n: [] for n in strip})
        left = {n: after[n]["shape_keys"] for n in strip if after.get(n, {}).get("shape_keys")}
        if left:
            report["diffs"].append("shape keys came back on %s" % ", ".join(sorted(left)))
        report["ok"] = not report["diffs"]
        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-stripupper"
            if not os.path.exists(backup):
                shutil.copy2(fbx, backup)
            os.replace(staged, fbx)
            report.update(applied=True, backup=backup, staged=None)
    except SystemExit as exc:
        report["error"] = str(exc)
    except Exception as exc:  # noqa: BLE001
        import traceback
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["traceback"] = traceback.format_exc()
    save("done")


if __name__ == "__main__":
    main()
