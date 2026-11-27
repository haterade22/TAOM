"""Joint numbers of a Rivendell trim sheet diffuse, in the terms that tile_compose uses for the wall stone.

    python tools/painter/trim_joint_check.py <trim _d.png> --wall <wall _d.png> [--out report.json]

For each strip, the luminance of a band of rows that does not change along U is averaged per column; the joints
are the dips of that profile under its local median (tile_compose.joint_profile on the transposed band, which
wraps in U). The wall's own number comes from the same function on its rows: python tools/painter/tile_compose.py
stats <wall _d.png> --ppm 461. Tests: tools/tests/test_trim_joint_check.py.
"""
import argparse
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oneoff"))
import rivendell_trim_strips as rts  # noqa: E402
import tile_compose as tc  # noqa: E402


def joint_row_span(name, size=1024):
    """Image rows (top, bottom) of a band of the strip where nothing changes along U except the joints: the whole
    inner span of S1, S2 and S4; the lower bead of S3; the plain pier below the sill of S5."""
    t, b = rts.strip_rows()[name]
    px = rts.TILE_M / float(size)
    if name == "S3":
        s0, s1 = rts.MARGIN + rts.S3_BEAD_CENTRE - 0.007, rts.MARGIN + rts.S3_BEAD_CENTRE + 0.007
    elif name == "S5":
        s0, s1 = rts.MARGIN + 0.030, rts.S5_SILL - 0.004
    else:
        return t + rts.MARGIN_PX + 2, b - rts.MARGIN_PX - 2
    return int(round(b - s1 / px)), int(round(b - s0 / px))


def strip_joint_stats(lum, name, size=1024):
    """count, contrast (levels of luminance), width_px and width_mm of the joints of strip `name`."""
    a, z = joint_row_span(name, size)
    band = np.asarray(lum, dtype=float)[a:z].T  # columns become rows, so that the profile wraps in U
    ppm = size / rts.TILE_M
    jp = tc.joint_profile(band, ppm, periodic=True)
    return {"count": jp["count"], "contrast": round(jp["contrast"], 1), "width_px": round(jp["width_px"], 1),
            "width_mm": round(jp["width_px"] * 1000.0 / ppm, 1)}


def window_joint_stats(lum, name, size=1024, reach=14):
    """Contrast and width of the joints of `name` read at their known places (within `reach` px of each joint
    column), against the median of the band profile. For a strip whose profile is too noisy for the dip search
    (the beads of S3: the band is only a few rows high)."""
    a, z = joint_row_span(name, size)
    prof = np.asarray(lum, dtype=float)[a:z].mean(axis=0)
    base = float(np.median(prof))
    out = []
    for j in rts.joint_positions(name):
        c = int(round(j * size))
        seg = np.array([prof[(c + k) % size] for k in range(-reach, reach + 1)])
        dip = base - seg
        out.append((float(dip.max()), int((dip > dip.max() / 2.0).sum())))
    px = size / rts.TILE_M
    wid = float(np.median([w for _, w in out]))
    return {"count": len(out), "contrast": round(float(np.median([c for c, _ in out])), 1), "width_px": wid,
            "width_mm": round(wid * 1000.0 / px, 1)}


def face_gap(trim_rgb, wall_rgb, size=1024):
    """The flat faces of the trim (the margin rows of every strip) against the median of the wall: the
    difference in luminance and in red minus blue, trim minus wall."""
    trim = np.asarray(trim_rgb, dtype=float)
    rows = np.concatenate([np.r_[t + 2:t + 6, b - 6:b - 2] for t, b in rts.strip_rows().values()])
    face = np.median(trim[rows].reshape(-1, 3), axis=0)
    wall = np.median(np.asarray(wall_rgb, dtype=float).reshape(-1, 3), axis=0)
    lum = lambda c: float(tc.luminance(np.asarray(c, dtype=float).reshape(1, 1, 3))[0, 0])
    return {"trim_face": [round(float(v), 1) for v in face], "wall_face": [round(float(v), 1) for v in wall],
            "lum_diff": round(lum(face) - lum(wall), 2),
            "red_minus_blue_diff": round(float((face[0] - face[2]) - (wall[0] - wall[2])), 2)}


def main(argv=None):
    from PIL import Image
    ap = argparse.ArgumentParser()
    ap.add_argument("trim")
    ap.add_argument("--wall", required=True)
    ap.add_argument("--out")
    opts = ap.parse_args(argv)
    d = np.asarray(Image.open(opts.trim).convert("RGB"))
    wall = np.asarray(Image.open(opts.wall).convert("RGB"))
    lum = tc.luminance(d)
    rep = {"joints": {n: strip_joint_stats(lum, n, d.shape[0]) for n in rts.STRIPS},
           "joints_at_known_places": {n: window_joint_stats(lum, n, d.shape[0]) for n in rts.STRIPS
                                      if rts.joint_positions(n)},
           "face": face_gap(d, wall, d.shape[0]),
           "wall_joint": tc.tile_stats(wall, 461.0)}
    text = json.dumps(rep, indent=1)
    if opts.out:
        with open(opts.out, "w", encoding="utf-8") as f:
            f.write(text)
    print(text)


if __name__ == "__main__":
    main()
