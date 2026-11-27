"""A close look at a tiling set (round 5): the set's d, n, s files lit by a raking light from the left and from
above, a 512 px crop at full size, beside the diffuse of the same crop (the fourth quarter is black).

    python tools/painter/set_close_view.py <set folder> <set name> --out <png> [--x 0] [--y 0] [--size 512]

The crop wraps over the tile border, so a seam would show in it. The light is the one of tile_compose.lit_view.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tile_compose as tc  # noqa: E402

LIGHTS = {"left": (-0.85, 0.2, 0.35), "top": (0.1, 0.85, 0.35)}


def crop_wrapped(a, x, y, size):
    h, w = a.shape[:2]
    return a[np.ix_([(y + i) % h for i in range(size)], [(x + j) % w for j in range(size)])]


def close_view(folder, name, x=0, y=0, size=512):
    d, n, s = (np.asarray(Image.open("%s/%s_%s.png" % (folder, name, k)).convert("RGB")) for k in "dns")
    parts = [crop_wrapped(d, x, y, size)]
    for light in LIGHTS.values():
        parts.append(crop_wrapped(tc.lit_view(d, n, s, 1, light), x, y, size))
    sheet = np.concatenate([np.concatenate(parts[:2], axis=1), np.concatenate(parts[2:] + [parts[0] * 0], axis=1)], 0)
    return Image.fromarray(sheet)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("name")
    ap.add_argument("--out", required=True)
    ap.add_argument("--x", type=int, default=0)
    ap.add_argument("--y", type=int, default=0)
    ap.add_argument("--size", type=int, default=512)
    a = ap.parse_args(argv)
    close_view(a.folder, a.name, a.x, a.y, a.size).save(a.out)


if __name__ == "__main__":
    main()
