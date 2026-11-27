"""Host script: a lit picture of the Painter bake beside the Blender reference, and seam numbers.

    python tools/painter/painter_trim_lit.py <painter map folder> --out <folder>

Writes lit_compare.png (reference | Painter | 4 x difference), lit_painter_3x1.png and the numbers of
the seam (angle between the two normal maps in the first and last columns) to seam.json.
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
import rivendell_trim_maps as rtm  # noqa: E402

REF = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/painter_lab/trim_bake"


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--out", required=True)
    ap.add_argument("--ref", default=REF)
    opts = ap.parse_args(argv)
    os.makedirs(opts.out, exist_ok=True)
    f = lambda n: os.path.join(opts.folder, "rivendell_tile_%s_Raw.png" % n)
    raw = os.path.join(opts.ref, "maps_raw")
    ref_n, ref_ao = np.load(os.path.join(raw, "normal.npy")), np.load(os.path.join(raw, "ao.npy"))
    n = tb.downsample_normal(tb.read_unit(f("normal_base")), 2)
    n_up = tb.to_green_up(n, True)
    ao = tb.box_down(tb.read_unit(f("ambient_occlusion")), 2)
    lit_ref = rtm._gamma(rtm.shade(ref_n, ref_ao))
    lit_p = rtm._gamma(rtm.shade(n_up, ao))
    diff = np.clip(np.abs(lit_ref - lit_p) * 4.0, 0, 1)
    row = np.concatenate([lit_ref, lit_p, diff], axis=1)
    Image.fromarray(rtm._u8(row)).save(os.path.join(opts.out, "lit_compare.png"))
    Image.fromarray(rtm.tile_u(rtm._u8(lit_p), 3)).save(os.path.join(opts.out, "lit_painter_3x1.png"))
    ang = tb.angle_map(ref_n, n_up)
    seam = {"angle_deg_first3_cols": float(ang[:, :3].mean()), "angle_deg_last3_cols": float(ang[:, -3:].mean()),
            "angle_deg_middle": float(ang[:, 3:-3].mean()),
            "lit_mean_abs_diff": float(np.abs(lit_ref - lit_p).mean()),
            "painter_col0_vs_last_max": float(np.abs(n_up[:, 0] - n_up[:, -1]).max()),
            "ref_col0_vs_last_max": float(np.abs(ref_n[:, 0] - ref_n[:, -1]).max())}
    with open(os.path.join(opts.out, "seam.json"), "w", encoding="utf-8") as fh:
        json.dump(seam, fh, indent=1)
    print(json.dumps(seam, indent=1))


if __name__ == "__main__":
    main()
