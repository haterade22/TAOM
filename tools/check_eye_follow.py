#!/usr/bin/env python3
"""
Check that a compiled race head's eyeballs follow their sockets through the face morph channels (2026-09-26).

WHY
The engine's static face morph moves the head's LOD0 face base mesh and its eye mesh by the character's face
sliders, channel by channel. When the head's channels move the eye sockets and the eye's channels move nothing,
the sockets open around still eyeballs and the game shows the socket's skin and a dark gap where the eyes should
be. The Kit applies no morph, so the Kit look passes. That shipped on the female dwarf: her eye had no channels
(#385, a crash), `add_face_morph_channels.py` zero-filled them, and the sockets still moved up to 8.5 mm.
Repair: `tools/blender/fit_eye_morphs.py`, then a Kit re-import. Background:
docs/reference/race-face-and-hand-morphs.md.

WHAT IT CHECKS
Exports the package's LOD0 `face_base_mesh` and `face_eye_mesh` (tools/export_face_morphs.ps1, TpacTool.Lib) and,
per eye (tools/blender/eye_follow.py splits and rings them), compares the socket ring's motion with the eyeball's
on every channel. FAIL when some channel moves an eye's socket ring at least --ring-mm (1.0) while that eyeball
never moves more than --still-mm (0.1) on any channel: an eye left behind. A head whose channels move nothing
(the hill troll's zero-filled face) passes, since nothing opens. It does not judge how well an authored eye
follows; the per-channel table shows that.

    python tools/check_eye_follow.py --package "<Armory>/Assets/Race Test/dwarf/sk_dwarf_bm_f1_geo.tpac" ^
        --metamesh sk_dwarf_bm_f1_head
Exit 0 OK, 1 an eye left behind, 2 could not run (no package, no pwsh, no TpacTool, no face pair).
"""
import argparse
import json
import math
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "blender"))
import eye_follow as ef  # noqa: E402


def mean_motion(base, frame, idx):
    """Mean distance moved by the vertices `idx` between `base` and `frame`."""
    return sum(math.dist(base[i], frame[i]) for i in idx) / len(idx)


def max_motion(base, frame, idx):
    return max(math.dist(base[i], frame[i]) for i in idx)


def analyse(head_base, head_frames, eye_base, eye_frames):
    """Per eye: the channel rows (channel, ring mean, eye mean) and the eyeball's largest motion anywhere.
    Positions are 3-tuples; frames are lists of positions in the same vertex order as the base."""
    if len(head_frames) != len(eye_frames):
        raise ValueError("head has %d channels, eye %d" % (len(head_frames), len(eye_frames)))
    out = []
    sides = ef.split_eyes(eye_base)
    for side, ring in zip(sides, ef.rings_for(head_base, eye_base)):
        if not ring:
            raise ValueError("no head vertex near an eye")
        rows, eye_max = [], 0.0
        for c, (hf, ef_) in enumerate(zip(head_frames, eye_frames)):
            rows.append((c, mean_motion(head_base, hf, ring), mean_motion(eye_base, ef_, side)))
            eye_max = max(eye_max, max_motion(eye_base, ef_, side))
        out.append({"vertices": len(side), "ring": len(ring), "rows": rows, "eye_max": eye_max})
    return out


def verdict(sides, ring_min=0.001, still=0.0001):
    """The eyes left behind: a socket ring that moves >= ring_min on some channel around an eyeball that never
    moves more than `still`. Returns a list of messages, empty when every eye is fine."""
    bad = []
    for n, s in enumerate(sides):
        worst = max(s["rows"], key=lambda r: r[1])
        if worst[1] >= ring_min and s["eye_max"] <= still:
            bad.append("eye %d: its socket moves %.2f mm on channel %d and the eyeball never moves (max %.3f mm)"
                       % (n, worst[1] * 1000, worst[0], s["eye_max"] * 1000))
    return bad


def load_export(path):
    d = json.load(open(path, encoding="utf-8-sig"))

    def part(tag):
        m = d[tag]
        base = [tuple(p) for p in m["positions"]]
        frames = [[(f[3 * i], f[3 * i + 1], f[3 * i + 2]) for i in range(len(base))] for f in m["frames"]]
        return base, frames

    return part("face_base_mesh") + part("face_eye_mesh")


def export(package, metamesh, out):
    script = os.path.join(HERE, "export_face_morphs.ps1")
    try:
        r = subprocess.run(["pwsh", "-NoProfile", "-File", script, "-Package", package, "-Metamesh", metamesh,
                            "-Out", out], capture_output=True, text=True, timeout=1800)
    except FileNotFoundError:
        raise SystemExit(2)
    if r.returncode != 0 or not os.path.exists(out):
        sys.stderr.write((r.stderr or r.stdout)[-800:] + "\n")
        raise SystemExit(2)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--package", required=True, help="the compiled _geo.tpac holding the head")
    ap.add_argument("--metamesh", required=True, help="the head metamesh, the skin's face_meta_mesh")
    ap.add_argument("--json", help="read an existing export_face_morphs.ps1 JSON instead of exporting")
    ap.add_argument("--ring-mm", type=float, default=1.0)
    ap.add_argument("--still-mm", type=float, default=0.1)
    args = ap.parse_args(argv)
    if args.json:
        path = args.json
    else:
        if not os.path.isfile(args.package):
            print("SKIPPED: no package %s" % args.package)
            return 2
        path = os.path.join(tempfile.mkdtemp(prefix="eyefollow_"), "head.json")
        export(args.package, args.metamesh, path)
    try:
        sides = analyse(*load_export(path))
    except (KeyError, ValueError) as exc:
        print("SKIPPED: %s" % exc)
        return 2
    for n, s in enumerate(sides):
        print("eye %d: %d vertices, socket ring %d head vertices, eyeball max motion %.2f mm"
              % (n, s["vertices"], s["ring"], s["eye_max"] * 1000))
        for c, ring, eye in sorted(s["rows"], key=lambda r: -r[1])[:8]:
            if ring >= 0.0005 or eye >= 0.0005:
                print("   channel %3d  socket %6.2f mm  eye %6.2f mm" % (c, ring * 1000, eye * 1000))
    bad = verdict(sides, args.ring_mm / 1000.0, args.still_mm / 1000.0)
    for b in bad:
        print("FAIL: %s (repair: tools/blender/fit_eye_morphs.py, then a Kit re-import)" % b)
    if not bad:
        print("OK: every eyeball moves with its socket, or no channel moves a socket")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
