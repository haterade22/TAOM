"""Host side: read the exports of painter_probe_height_depth.py.

    python tools/painter/painter_probe_height_depth_report.py <height_depth folder> <tag> [<tag> ...]
For each tag and bit depth: the distinct height levels across the 16 px ramp (columns 300 to 316) and across
the wide ramp (600 to 856), the normal R channel (dh/dx) in the same places, the run lengths of equal normal
values on the wide ramp (a stair has long runs), and how many distinct normal R values the wide ramp has.
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import painter_probe_metrics as m  # noqa: E402


def load(folder, tag):
    for f in os.listdir(folder):
        if f.endswith("_" + tag + ".png"):
            return np.asarray(Image.open(os.path.join(folder, f)))
    raise FileNotFoundError(tag)


def main(root, tags):
    for tag in tags:
        for bits in (8, 16):
            folder = os.path.join(root, tag, "bits%d" % bits)
            h = load(folder, "height")
            n = load(folder, "normal")
            r = n[..., 0]
            print("== %s, %d bit  height dtype %s  normal dtype %s" % (tag, bits, h.dtype, n.dtype))
            print("  height row 512, cols 300..316: %s" % h[512, 300:317].tolist())
            print("  distinct height levels, 16 px ramp: %d; wide ramp 600..856: %d"
                  % (m.distinct_levels(h, 512, 300, 317), m.distinct_levels(h, 512, 600, 857)))
            print("  normal R row 512, cols 298..320: %s" % r[512, 298:321].tolist())
            print("  distinct normal R values, 16 px ramp: %d; wide ramp: %d"
                  % (m.distinct_levels(r, 512, 296, 320), m.distinct_levels(r, 512, 600, 857)))
            wide = r[512, 620:840]
            runs = m.run_lengths(wide)
            print("  wide ramp normal R run lengths (cols 620..840): max %d, count %d, values %s"
                  % (max(runs), len(runs), sorted(set(wide.tolist()))[:12]))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
