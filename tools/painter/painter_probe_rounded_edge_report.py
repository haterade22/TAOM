"""Host side: measure the rounded-edge probe exports.

    python tools/painter/painter_probe_rounded_edge_report.py <probe folder>/out [name ...]
Per variant: the width at half depth (FWHM) of the vertical joint at columns near 345.5 (rows 20..180 mean),
its depth (height levels of 65535 between the joint and the face), the number of distinct 16 bit levels over
the profile, the mirror asymmetry about the wrap for the horizontal joint at the top and bottom border
(columns 100..300 mean), and for the _x0 variants the same for the vertical joint on the left and right border.
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import painter_probe_metrics as m  # noqa: E402


def load(folder, tag):
    return np.asarray(Image.open(os.path.join(folder, tag + ".png"))).astype(np.float64)


def measure(folder, x0):
    h = load(folder + "/h16", "height") / 65535.0
    if not x0:
        prof = h[20:180, 300:392].mean(axis=0)
        wrap = h[:, 100:300].mean(axis=1)
        wrap_line = np.concatenate((wrap[-40:], wrap[:40]))
        line = prof
    else:
        wrap = h[20:180, :].mean(axis=0)
        wrap_line = np.concatenate((wrap[-40:], wrap[:40]))
        line = wrap_line
    base = float(np.median(line))
    depth = float(np.max(np.abs(line - base)))
    # mirror pairs about the wrap in the concatenated line: index 39 and 40 are the pair next to the wrap
    asym = float(max(abs(wrap_line[39 - i] - wrap_line[40 + i]) for i in range(20)))
    return {"fwhm": m.fwhm(line), "depth": depth, "levels": int(len(np.unique(np.round(line * 65535)))),
            "wrap_asym": asym}


def main(root, names):
    names = names or sorted(d for d in os.listdir(root) if os.path.isdir(os.path.join(root, d)))
    for n in names:
        r = measure(os.path.join(root, n), n.endswith("_x0"))
        print("%-28s fwhm %6.2f px  depth %.3f  levels %4d  wrap asym %.4f" % (n, r["fwhm"], r["depth"], r["levels"], r["wrap_asym"]))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
