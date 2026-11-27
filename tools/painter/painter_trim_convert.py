"""Host script: a Painter export of the textured trim to the engine form.

    python tools/painter/painter_trim_convert.py <export folder> --out <set folder> [--name t_rivendell_wall_trim_a]

Writes <name>_d.png (the baseColor, shifted per channel so that the median of the flat face rows equals the
median of the wall diffuse; the stack carries its own occlusion, so none is multiplied in), <name>_n.png (the
Normal_DirectX export, green down, as it is) and <name>_s.png (R 0, G gloss from the roughness at mean 100 and
spread 14 as for the wall, B the exported occlusion). Prints the offset used.
"""
import argparse
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oneoff"))
import convert_painter_stone_sets as cv  # noqa: E402
import rivendell_trim_strips as rts  # noqa: E402
import trim_bake_check as tb  # noqa: E402

WALL = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/textures/v06/alt_calm_p3/t_rivendell_wall_stone_a_d.png"


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--out", required=True)
    ap.add_argument("--name", default="t_rivendell_wall_trim_a")
    ap.add_argument("--wall", default=WALL)
    ap.add_argument("--gloss-mean", type=float, default=100.0)
    ap.add_argument("--gloss-std", type=float, default=14.0)
    opts = ap.parse_args(argv)
    os.makedirs(opts.out, exist_ok=True)
    f = lambda m: os.path.join(opts.folder, "rivendell_tile_%s.png" % m)
    base = cv.load(f("baseColor"), "RGB").astype(float)
    rows = np.concatenate([tb.margin_rows(t, b, 2, 4) for t, b in rts.strip_rows().values()])
    off = tb.face_offset(base, rows, cv.load(opts.wall, "RGB"))
    diff = np.clip(base + off, 0, 255)
    cv.save(diff, os.path.join(opts.out, opts.name + "_d.png"))
    cv.save(cv.engine_normal(cv.load(f("normal"), "RGB"), "down"), os.path.join(opts.out, opts.name + "_n.png"))
    spec = cv.spec_from_maps(cv.load(f("roughness"), "L"), cv.load(f("ambientOcclusion"), "L"),
                             opts.gloss_mean, opts.gloss_std)
    cv.save(spec, os.path.join(opts.out, opts.name + "_s.png"))
    rep = {"offset_levels": [round(float(v), 2) for v in off], "diffuse_mean": [round(float(v), 2) for v in diff.reshape(-1, 3).mean(0)],
           "spec_g_mean": round(float(spec[..., 1].mean()), 2), "spec_b_mean": round(float(spec[..., 2].mean()), 2)}
    print(json.dumps(rep))


if __name__ == "__main__":
    main()
