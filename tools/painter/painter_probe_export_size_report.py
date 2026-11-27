"""Host side: compare the exports of painter_probe_export_size.py.

    python tools/painter/painter_probe_export_size_report.py <export_size folder>
Prints, per map (baseColor, height, normal, roughness), the size of each export, the variance of the
Laplacian of the same UV region (the middle 25 percent, at native size and after a box downsample to
1024 scale), and the mean absolute difference between pairs that should match if only an enlarge happened.
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import painter_probe_metrics as m  # noqa: E402

RUNS = [(1024, 1024), (1024, 2048), (2048, 1024), (2048, 2048)]
MAPS = ["baseColor", "height", "normal", "roughness", "ambientOcclusion"]


def load(folder, tag):
    for f in os.listdir(folder):
        if f.endswith("_" + tag + ".png"):
            im = Image.open(os.path.join(folder, f))
            a = np.asarray(im).astype(np.float64)
            return a
    raise FileNotFoundError(tag + " in " + folder)


def main(root):
    for tag in MAPS:
        print("== " + tag)
        imgs = {}
        for s, e in RUNS:
            a = load(os.path.join(root, "set%d_exp%d" % (s, e)), tag)
            imgs[(s, e)] = a
            g = a if a.ndim == 2 else a[..., 1] if tag == "normal" else a.mean(axis=2)
            c = m.crop_uv(g, 0.25, 0.25, 0.75, 0.75)
            k = e // 1024
            c1024 = m.box_down(c, k)
            print("set %4d exp %4d  size %s  lapvar native %10.3f  at 1024 scale %10.3f  mean %.2f"
                  % (s, e, a.shape[:2], m.laplacian_variance(c), m.laplacian_variance(c1024), g.mean()))

        def diff(p, q):
            a, b = imgs[p], imgs[q]
            k = a.shape[0] // b.shape[0]
            a = m.box_down(a, k) if k > 1 else a
            return np.abs(a - b).mean()
        print("  |set1024 exp2048 (box down) - set1024 exp1024| = %.3f" % diff((1024, 2048), (1024, 1024)))
        print("  |set2048 exp1024 - set1024 exp1024|            = %.3f" % diff((2048, 1024), (1024, 1024)))
        print("  |set2048 exp2048 (box down) - set2048 exp1024| = %.3f" % diff((2048, 2048), (2048, 1024)))
        print("  |set2048 exp2048 (box down) - set1024 exp2048 (box down)| = %.3f"
              % np.abs(m.box_down(imgs[(2048, 2048)], 2) - m.box_down(imgs[(1024, 2048)], 2)).mean())


if __name__ == "__main__":
    main(sys.argv[1])
