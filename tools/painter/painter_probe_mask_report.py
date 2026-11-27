"""Host side: compare the false-colour exports of painter_probe_mask_check.py with the layout maps.

    python tools/painter/painter_probe_mask_report.py <mask_check folder> <layout folder>
For each masked layer (stain with layout_stain.png, joint colour with layout_edge.png): the exported red
channel (sRGB decoded to linear) against the map: correlation, mean absolute difference, the share of texels
the map marks (above 0.5) that are red (above 0.5 linear) and the share of texels the map leaves empty (below 0.02)
that are red. Also the green and blue means (they must be about 0 for a pure red layer).
"""
import os
import sys

import numpy as np
from PIL import Image

PAIRS = {"stain": "stain", "joint_colour": "edge"}


def to_linear(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def main(root, layout):
    for layer, key in PAIRS.items():
        folder = os.path.join(root, "mask_" + layer)
        f = next(os.path.join(folder, x) for x in os.listdir(folder) if x.endswith("baseColor.png"))
        a = np.asarray(Image.open(f).convert("RGB")).astype(np.float64)
        red = to_linear(a[..., 0])
        m = np.asarray(Image.open(os.path.join(layout, "layout_%s.png" % key))).astype(np.float64) / 255.0
        corr = float(np.corrcoef(red.ravel(), m.ravel())[0, 1]) if red.std() > 0 and m.std() > 0 else float("nan")
        on, off = m > 0.5, m < 0.02
        print("%-13s map %-5s  red mean %.4f  map mean %.4f  corr %.4f  mean|diff| %.4f  red where map>0.5: %.4f  red where map<0.02: %.4f  G mean %.2f  B mean %.2f"
              % (layer, key, red.mean(), m.mean(), corr, np.abs(red - m).mean(),
                 (red[on] > 0.5).mean() if on.any() else float("nan"), (red[off] > 0.5).mean(),
                 a[..., 1].mean(), a[..., 2].mean()))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
