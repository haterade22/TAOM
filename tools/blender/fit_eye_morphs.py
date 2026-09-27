#!/usr/bin/env python3
"""
Make a race head's eyeballs follow their sockets through the face morph channels, in an Armory FBX
(2026-09-26, the female dwarf `sk_dwarf_bm_f1`).

WHY
The engine's static face morph moves every LOD0 facegen sub-mesh by the character's face sliders. The female
dwarf's `.eye` came with no channels (#385, a crash at load), and `add_face_morph_channels.py` gave it 101
zero-offset ones, which stopped the crash but left the eyeballs still while the head's sockets move up to 8.5 mm
(channel 14). In game her eye openings showed the socket's skin and a dark gap; the Kit applies no morph and
looked right. The math, and its validation on the male dwarf's hand-authored eyes (mean error 0.97 mm), is in
eye_follow.py.

WHAT IT DOES
Reads the LOD0 head `--head` and its eye `--eye` (default `<head>.eye`). For every channel, matched BY ORDER (the
engine goes by order, and the eye's keys are named `shape_NN` while the head's carry the artist's names), sets the
eye key to eye_follow.follow() of the head key, in world space. Refuses an eye whose channels already move
anything (hand-authored eyes are never overwritten) and a head and eye with different channel counts. Every key
keeps weight 0.

SAFETY (the add_face_morph_channels.py shape): a staged `<stem>.eyefollow.fbx` is written and re-imported;
`add_mesh_lods.compare` checks every object came back as it was (names, keys, geometry), and the eye's keys are
read back and compared with what was set. `--apply` writes a write-once `<fbx>.bak-eyefollow` first. Then a Kit
re-import of the FBX and a Save, and a check that every mesh kept its material.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup ^
        -P tools/blender/fit_eye_morphs.py -- --fbx <sk_dwarf_bm_f1.fbx> --head sk_dwarf_bm_f1_head [--apply]
The launcher detaches: poll `<fbx>.eyefollow-report.json` until "stage" is "done".
"""
import json
import os
import shutil
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import add_mesh_lods as aml  # noqa: E402
import eye_follow as ef  # noqa: E402

STILL = 1e-7  # an offset below this is "no motion" (float noise in an FBX round trip)


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {"apply": False, "fbx": None, "head": None, "eye": None}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--apply":
            out["apply"] = True
            i += 1
        elif a in ("--fbx", "--head", "--eye") and i + 1 < len(argv):
            out[a[2:]] = argv[i + 1]
            i += 2
        else:
            raise SystemExit("bad argument %r" % a)
    if not out["fbx"] or not out["head"]:
        raise SystemExit("--fbx and --head are required")
    out["eye"] = out["eye"] or out["head"] + ".eye"
    return out


def channels(obj):
    """The object's keys after the reference key, in order."""
    if not obj.data.shape_keys:
        return []
    return list(obj.data.shape_keys.key_blocks)[1:]


def world(obj, coords):
    m = obj.matrix_world
    return [tuple(m @ c) for c in coords]


def still(obj):
    """True when every channel of `obj` equals its reference key."""
    ref = obj.data.shape_keys.key_blocks[0].data
    return all((k.data[i].co - ref[i].co).length < STILL for k in channels(obj) for i in range(len(ref)))


def fit(head, eye):
    """Set every eye channel from the head channel in the same position. Returns the per-channel eye
    positions (object space) that were written, for the read-back."""
    hk, ek = channels(head), channels(eye)
    if not ek or len(hk) != len(ek):
        raise SystemExit("%s has %d channels and %s has %d; they must match" % (head.name, len(hk), eye.name, len(ek)))
    if not still(eye):
        raise SystemExit("%s already has channels that move; refusing to overwrite authored eyes" % eye.name)
    head_ref = [v.co.copy() for v in head.data.shape_keys.key_blocks[0].data]
    eye_ref = [v.co.copy() for v in eye.data.shape_keys.key_blocks[0].data]
    head_basis = world(head, head_ref)
    eye_basis = world(eye, eye_ref)
    rings = ef.rings_for(head_basis, eye_basis)
    inverse = eye.matrix_world.inverted()
    written = []
    for h, e in zip(hk, ek):
        target = ef.follow(head_basis, world(head, [d.co for d in h.data]), eye_basis, rings)
        local = [inverse @ bpy_vector(p) for p in target]
        for i, p in enumerate(local):
            e.data[i].co = p
        e.value = 0.0
        written.append([tuple(p) for p in local])
    return written, [len(r) for r in rings]


def bpy_vector(p):
    from mathutils import Vector
    return Vector(p)


def read_back(eye, written):
    """The largest difference between the eye keys as re-imported and as written."""
    worst = 0.0
    for k, want in zip(channels(eye), written):
        for i, w in enumerate(want):
            worst = max(worst, (k.data[i].co - bpy_vector(w)).length)
    return worst


def main():
    fbx = aml.fbx_from_argv(list(sys.argv))
    report_path = fbx + ".eyefollow-report.json"
    report = {"fbx": fbx, "stage": "start", "ok": False, "applied": False, "diffs": []}

    def save(stage):
        report["stage"] = stage
        with open(report_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1)

    try:
        args = parse_args(list(sys.argv))
        staged = os.path.splitext(fbx)[0] + ".eyefollow.fbx"
        report.update(staged=staged, head=args["head"], eye=args["eye"])
        save("loading")
        aml.load(fbx)
        before = aml.fingerprint()
        for n in (args["head"], args["eye"]):
            if n not in bpy.data.objects:
                raise SystemExit("no mesh %r in the FBX" % n)
        head, eye = bpy.data.objects[args["head"]], bpy.data.objects[args["eye"]]
        written, ring_sizes = fit(head, eye)
        report.update(channels=len(written), ring_vertices=ring_sizes)
        save("exporting")
        aml.export(staged)
        save("re-importing")
        aml.load(staged)
        report["diffs"] = aml.compare(before, aml.fingerprint(), {})
        worst = read_back(bpy.data.objects[args["eye"]], written)
        report["read_back_max"] = worst
        if worst > 1e-4:
            report["diffs"].append("eye keys came back %.6f off what was written" % worst)
        report["ok"] = not report["diffs"]
        if report["ok"] and args["apply"]:
            backup = fbx + ".bak-eyefollow"
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
