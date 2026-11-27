"""Host side: a picture of the same region from the exports of painter_probe_export_size.py.

    python tools/painter/painter_probe_export_size_sheet.py <export_size folder> <out png>
Rows: baseColor, normal, height (stretched). Columns: set 1024 exp 1024, set 2048 exp 1024, set 1024 exp 2048
(each region is 25 percent of the tile, shown at 4x nearest neighbour for the 1024 images and 2x for the 2048).
"""
import os
import sys

import numpy as np
from PIL import Image

RUNS = [(1024, 1024), (2048, 1024), (1024, 2048)]
MAPS = ["baseColor", "normal", "height"]
REGION = (0.30, 0.30, 0.55, 0.55)


def load(folder, tag):
    for f in os.listdir(folder):
        if f.endswith("_" + tag + ".png"):
            return Image.open(os.path.join(folder, f))
    raise FileNotFoundError(tag)


def main(root, out):
    tiles = []
    for tag in MAPS:
        row = []
        for s, e in RUNS:
            im = load(os.path.join(root, "set%d_exp%d" % (s, e)), tag)
            w = im.size[0]
            box = tuple(int(round(v * w)) for v in REGION)
            c = im.crop(box)
            if tag == "height":
                a = np.asarray(c.convert("L")).astype(float)
                lo, hi = np.percentile(a, 1), np.percentile(a, 99)
                c = Image.fromarray((np.clip((a - lo) / max(hi - lo, 1), 0, 1) * 255).astype(np.uint8))
            c = c.convert("RGB").resize((1024, 1024), Image.NEAREST)
            row.append(c)
        tiles.append(row)
    sheet = Image.new("RGB", (3 * 1024 + 20, 3 * 1024 + 20), (40, 0, 40))
    for r, row in enumerate(tiles):
        for c, im in enumerate(row):
            sheet.paste(im, (c * 1034, r * 1034))
    sheet = sheet.resize((sheet.width // 2, sheet.height // 2), Image.LANCZOS)
    sheet.save(out)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
