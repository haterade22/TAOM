#!/usr/bin/env python3
"""
Give a race head the facegen morph channels the engine needs, in an Armory FBX (2026-09-25, the hill troll).

WHY
A skin's head is a facegen head: its sub-meshes are tagged `face_base_mesh`, `face_eye_mesh` and
`face_mouth_mesh`, and every time an agent is built the engine runs a static face morph over them
(TaleWorlds.Native.dll function 0x570550 on v1.5.3; its only string is "No morph data found for face mesh.
Can not do static morph."). The routine skips a mesh only when it has no morph object at all; otherwise it
reads each vertex's morph row from the mesh's morph buffer, with the channel count of the face request, not
the mesh's. KEYForce's hill troll head, eye and mouth carried NO shape keys, the Kit built an empty morph
record for them, and the first Custom Battle with a hill troll read address 0x168C (a null buffer, row 962,
the head's vertex count): an access violation at +0x57070C, a CTD a second after deployment.

Every working race head carries the same layout (read with tools/audit_fbx_lods.py): the LOD0 head, `.eye`
or `.eyes`, and `.mouth` each hold 101 channels in the same order; their LODs hold none. The names are the
author's (`shape_01` to `shape_101` on the Dol Guldur and pale uruks, `SM_..._shape_frame_150` to `_250` on
the dwarf, `Basis_0` to `Yell_100` on the goblin), so the engine goes by order.

WHAT IT DOES
Adds, on every LOD0 facegen object of `--head` (the object itself and each `<head>.<part>` that is not a
`.lodN`), a reference key if it has none and then `--count` channels named `<prefix>01` .. `<prefix>NN`, each
a copy of the reference: zero offsets. The engine gets a real buffer; face sliders move nothing on this head,
which has no face rig to move. An object that already has exactly `--count` channels is left alone (the female
dwarf `sk_dwarf_bm_f1_head`: head and mouth had 101, only `.eye` had none); any other existing count is refused.

SAFETY (the tools/blender/add_mesh_lods.py shape): a staged `<stem>.facemorphs.fbx` is written and
re-imported; every object must come back with its vertex, polygon, UV, material and parent counts, the
targets with exactly the new channels and every other object's keys untouched. `--apply` writes a write-once
`<fbx>.bak-facemorphs` first. Check without Blender: `python tools/audit_fbx_lods.py --diff <old> <new>`.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/add_face_morph_channels.py -- --fbx <hill_troll_a.fbx> --head hill_troll_a_head [--apply]
The launcher detaches: poll `<fbx>.facemorphs-report.json` until "stage" is "done". Then a Kit re-import.
"""
import json
import os
import re
import shutil
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import add_mesh_lods as aml  # noqa: E402

LOD = re.compile(r"[._]lod\d+$", re.I)


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "fbx": None, "head": None, "count": 101, "prefix": "shape_"}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif a in ("--fbx", "--head", "--count", "--prefix") and i + 1 < len(argv):
            out[a[2:]] = int(argv[i + 1]) if a == "--count" else argv[i + 1]
            i += 2
        else:
            raise SystemExit("bad argument %r" % a)
    if not out["fbx"] or not out["head"]:
        raise SystemExit("--fbx and --head are required")
    if not 1 <= out["count"] <= 200:
        raise SystemExit("--count must be 1 to 200")
    return out


def targets(head):
    """The head's LOD0 facegen objects: the head and its named parts, never a LOD."""
    names = [o.name for o in bpy.data.objects if o.type == "MESH"]
    found = [n for n in names if (n == head or n.startswith(head + ".")) and not LOD.search(n)]
    if head not in found:
        raise SystemExit("no mesh %r in the FBX" % head)
    return sorted(found)


def channel_names(count, prefix):
    # the uruks' shape_01 .. shape_101: two digits minimum, never padded to the count
    return ["%s%02d" % (prefix, i) for i in range(1, count + 1)]


def add_channels(obj, names):
    """Add the channels, or leave an object that already has exactly that many (the female dwarf's head and
    mouth had 101 and only its eye had none). Any other existing count is refused, never topped up."""
    if obj.data.shape_keys and len(obj.data.shape_keys.key_blocks) > 1:
        have = len(obj.data.shape_keys.key_blocks) - 1
        if have == len(names):
            return None
        raise SystemExit("%s already has %d channels, not %d; refusing to change it" % (obj.name, have, len(names)))
    if not obj.data.shape_keys:
        obj.shape_key_add(name="Basis", from_mix=False)
    for n in names:
        obj.shape_key_add(name=n, from_mix=False)      # a copy of the reference: zero offsets
    return [k.name for k in obj.data.shape_keys.key_blocks]


def main():
    fbx = aml.fbx_from_argv(list(sys.argv))
    report_path = fbx + ".facemorphs-report.json"
    report = {"fbx": fbx, "stage": "start", "ok": False, "applied": False, "diffs": [], "objects": {}}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        args = parse_args(list(sys.argv))
        staged = os.path.splitext(fbx)[0] + ".facemorphs.fbx"
        report.update(staged=staged, head=args["head"], count=args["count"])
        save("loading")
        aml.load(fbx)
        before = aml.fingerprint()
        names = channel_names(args["count"], args["prefix"])
        expected = {}
        for n in targets(args["head"]):
            keys = add_channels(bpy.data.objects[n], names)
            report["objects"][n] = {"vertices": len(bpy.data.objects[n].data.vertices),
                                    "added": keys is not None}
            if keys is not None:
                expected[n] = keys
        if not expected:
            raise SystemExit("every facegen object of %s already has %d channels; nothing to do"
                             % (args["head"], args["count"]))
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        after = aml.fingerprint()
        for name, b in before.items():
            a = after.get(name)
            if a is None:
                report["diffs"].append("lost %s" % name)
                continue
            if b["type"] != "MESH":
                continue
            for k in ("verts", "polys", "uv_layers", "materials", "parent"):
                if b[k] != a[k]:
                    report["diffs"].append("%s: %s %r -> %r" % (name, k, b[k], a[k]))
            want = expected.get(name, b["shape_keys"])
            # the FBX importer names the reference key itself; compare the channels after it
            if list(a["shape_keys"][1:]) != list(want[1:]):
                report["diffs"].append("%s: channels %d -> %d (first %s)" % (
                    name, max(len(want) - 1, 0), max(len(a["shape_keys"]) - 1, 0), a["shape_keys"][1:3]))
        report["ok"] = not report["diffs"]
        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-facemorphs"
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
