"""Host script: numbers of a textured trim export against the wall stone and the bake.

    python tools/painter/painter_trim_eval.py <export folder> [--wall <wall _d.png>]

Reads rivendell_tile_baseColor.png and rivendell_tile_normal.png (Normal_DirectX, green down) of an export at
1024 and prints: the face value of the flat margin rows (the plane level, between the strips) and of the S1
faces against the median of the wall diffuse; the darkness of the S1 and S2 joints; the correlation of the
diffuse luminance with the baked occlusion and curvature; the normal against the Blender reference.
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oneoff"))
import trim_bake_check as tb  # noqa: E402
import rivendell_trim_strips as rts  # noqa: E402

REF = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/painter_lab/trim_bake"
WALL = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/textures/v06/alt_calm_p3/t_rivendell_wall_stone_a_d.png"


def lum(a):
    return a[..., 0] * 0.2126 + a[..., 1] * 0.7152 + a[..., 2] * 0.0722


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--wall", default=WALL)
    ap.add_argument("--diffuse", default=None, help="a converted _d.png to measure instead of baseColor")
    opts = ap.parse_args(argv)
    d = np.asarray(Image.open(opts.diffuse or os.path.join(opts.folder, "rivendell_tile_baseColor.png")).convert("RGB"), dtype=float)
    wall = np.asarray(Image.open(opts.wall).convert("RGB"), dtype=float)
    rows = rts.strip_rows()
    raw = os.path.join(REF, "maps_raw")
    h = np.load(os.path.join(raw, "height_mm.npy"))
    ao = np.load(os.path.join(raw, "ao.npy"))
    curv = tb.read_unit(os.path.join(REF, "trim_ref_curvature.png"))
    out = {"wall_median_rgb": [float(np.median(wall[..., i])) for i in range(3)], "wall_median_lum": float(np.median(lum(wall)))}
    m = np.concatenate([tb.margin_rows(t, b, 2, 4) for t, b in rows.values()])
    flat = d[m]
    out["flat_margin_median_rgb"] = [float(np.median(flat[..., i])) for i in range(3)]
    out["flat_margin_median_lum"] = float(np.median(lum(flat)))
    # faces: pixels at 3 to 4.6 mm in S1 (the plain stones), away from joints (height above 3.5 mm)
    t, b = rows["S1"]
    s1 = d[t:b]
    face = (h[t:b] > 3.6) & (h[t:b] < 4.6)
    out["s1_face_median_rgb"] = [float(np.median(s1[..., i][face])) for i in range(3)]
    out["s1_face_median_lum"] = float(np.median(lum(s1)[face]))
    jt = h[t:b] < 2.0
    out["s1_joint_pixels"] = int(jt.sum())
    out["s1_joint_lum_median"] = float(np.median(lum(s1)[jt])) if jt.any() else None
    t2, b2 = rows["S2"]
    jt2 = h[t2:b2] < 2.0
    out["s2_joint_pixels"] = int(jt2.sum())
    out["s2_joint_lum_median"] = float(np.median(lum(d[t2:b2])[jt2])) if jt2.any() else None
    L = lum(d)
    out["corr_lum_ao"] = tb.pearson(L, ao)
    out["corr_lum_curvature"] = tb.pearson(L, curv)
    out["lum_p1_p99"] = [float(np.percentile(L, 1)), float(np.percentile(L, 99))]
    nf = os.path.join(opts.folder, "rivendell_tile_normal.png")
    if os.path.exists(nf):
        n = tb.unit(np.asarray(Image.open(nf).convert("RGB")))
        out["normal_vs_reference"] = tb.normal_report(np.load(os.path.join(raw, "normal.npy")), n, False, True)
    print(json.dumps(out, indent=1))


if __name__ == "__main__":
    main()
