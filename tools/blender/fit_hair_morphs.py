#!/usr/bin/env python3
"""
Make a race head's hair and beard follow the head through the face morph channels, in an Armory FBX
(2026-09-28, Saruman `saruman_delivery_2026.03.16.fbx`).

WHY
The engine morphs the hair and beard by the same face channels as the head (the artist's own hair channel 51 and
beard channels 67, 68, 71, 95 and 100 move exactly with the scalp and jaw under them). Saruman's other channels are
zero on the hair and beard while the head moves up to 42 mm (channel 46), so in game the hair floats off the
scalp; the Kit applies no morph and looks right. Same class as the eyes (fit_eye_morphs.py). Math: hair_follow.py.

WHAT IT DOES
For each `--mesh` (default: every LOD0 mesh whose name holds "hair" or "beard"), channel by channel, matched BY
ORDER with `--head`: a channel on which the mesh already moves more than AUTHORED (0.5 mm) anywhere is kept as
authored; every other channel is set to hair_follow.follow() of the head channel, in world space. Refuses a mesh
whose channel count differs from the head's. Every key keeps weight 0. LODs are left alone (the engine morphs LOD0).

SAFETY (fit_eye_morphs.py's shape): a staged `<stem>.hairfollow.fbx` is written and re-imported;
`add_mesh_lods.compare` checks every object came back as it was, and the written keys are read back. `--apply`
writes a write-once `<fbx>.bak-hairfollow` first, then replaces the FBX. Then a Kit re-import and Save, and a
check that every mesh kept its material.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/fit_hair_morphs.py -- --fbx <saruman_delivery_2026.03.16.fbx> ^
        --head sk_saruman_head.base [--mesh SK_saruman_hair --mesh SK_saruman_beard] [--apply]
The launcher detaches: poll `<fbx>.hairfollow-report.json` until "stage" is "done".
"""
import json
import os
import shutil
import sys

import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import add_mesh_lods as aml  # noqa: E402
import hair_follow as hf  # noqa: E402

AUTHORED = 0.0005  # m: a channel moving the mesh more than this anywhere is the artist's; keep it


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "fbx": None, "head": None, "mesh": []}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif a in ("--fbx", "--head") and i + 1 < len(argv):
            out[a[2:]] = argv[i + 1]
            i += 2
        elif a == "--mesh" and i + 1 < len(argv):
            out["mesh"].append(argv[i + 1])
            i += 2
        else:
            raise SystemExit("bad argument %r" % a)
    if not out["fbx"] or not out["head"]:
        raise SystemExit("--fbx and --head are required")
    return out


def channels(obj):
    return list(obj.data.shape_keys.key_blocks)[1:] if obj.data.shape_keys else []


def world(obj, coords):
    m = obj.matrix_world
    return [tuple(m @ c) for c in coords]


def default_meshes():
    return sorted(o.name for o in bpy.data.objects if o.type == "MESH" and ".lod" not in o.name.lower()
                  and ("hair" in o.name.lower() or "beard" in o.name.lower()))


def fit(head, obj):
    hk, ok = channels(head), channels(obj)
    if not ok or len(hk) != len(ok):
        raise SystemExit("%s has %d channels and %s has %d; they must match" % (head.name, len(hk), obj.name, len(ok)))
    head_base = world(head, [v.co for v in head.data.shape_keys.key_blocks[0].data])
    base = world(obj, [v.co for v in obj.data.shape_keys.key_blocks[0].data])
    kd = KDTree(len(head_base))
    for j, p in enumerate(head_base):
        kd.insert(p, j)
    kd.balance()
    near = [kd.find(p)[2] for p in base]
    edges = [tuple(e.vertices) for e in obj.data.edges]
    root = hf.roots(len(base), edges, near)
    anchors = {}
    for r in set(root):
        found = kd.find_n(base[r], hf.K)
        ws = hf.weights([d for _, _, d in found])
        anchors[r] = [(j, w) for (_, j, _), w in zip(found, ws)]
    inverse = obj.matrix_world.inverted()
    written, kept = {}, []
    for c, (h, k) in enumerate(zip(hk, ok)):
        frame = world(obj, [d.co for d in k.data])
        if hf.max_motion(base, frame) > AUTHORED:
            kept.append(c)
            continue
        target = hf.follow(base, root, anchors, head_base, world(head, [d.co for d in h.data]))
        local = [inverse @ Vector(p) for p in target]
        for i, p in enumerate(local):
            k.data[i].co = p
        k.value = 0.0
        written[c] = [tuple(p) for p in local]
    return written, kept, len(set(root))


def read_back(obj, written):
    worst = 0.0
    keys = channels(obj)
    for c, want in written.items():
        for i, w in enumerate(want):
            worst = max(worst, (keys[c].data[i].co - Vector(w)).length)
    return worst


def main():
    fbx = aml.fbx_from_argv(list(sys.argv))
    report_path = fbx + ".hairfollow-report.json"
    report = {"fbx": fbx, "stage": "start", "ok": False, "applied": False, "diffs": [], "meshes": {}}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        args = parse_args(list(sys.argv))
        staged = os.path.splitext(fbx)[0] + ".hairfollow.fbx"
        report.update(staged=staged, head=args["head"])
        save("loading")
        aml.load(fbx)
        before = aml.fingerprint()
        names = args["mesh"] or default_meshes()
        for n in [args["head"]] + names:
            if n not in bpy.data.objects:
                raise SystemExit("no mesh %r in the FBX" % n)
        head = bpy.data.objects[args["head"]]
        all_written = {}
        for n in names:
            written, kept, nroots = fit(head, bpy.data.objects[n])
            all_written[n] = written
            report["meshes"][n] = {"written": len(written), "kept_authored": kept, "roots": nroots}
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        report["diffs"] = aml.compare(before, aml.fingerprint(), {})
        for n, written in all_written.items():
            worst = read_back(bpy.data.objects[n], written)
            report["meshes"][n]["read_back_max"] = worst
            if worst > 1e-4:
                report["diffs"].append("%s keys came back %.6f off what was written" % (n, worst))
        report["ok"] = not report["diffs"]
        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-hairfollow"
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
