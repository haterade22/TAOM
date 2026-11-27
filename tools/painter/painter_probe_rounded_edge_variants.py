"""Host side: write the rounded-edge probe sources and a variants file.

    python tools/painter/painter_probe_rounded_edge_variants.py <layout folder> <probe folder> <set name>
Writes <probe>/src/{edge,edge_x0,height,height_x0}.png (x0: rolled so that a vertical joint lies on the
left and right border) and <probe>/variants_<set name>.json.
Sets: "scan" (blur, bevel and bevel_smooth with the wrap toggle off and on, two sizes each) and "anchor".
"""
import json
import os
import sys

import numpy as np
from PIL import Image


def sources(layout, src):
    os.makedirs(src, exist_ok=True)
    out = {}
    for key in ("edge", "height"):
        a = np.asarray(Image.open(os.path.join(layout, "layout_%s.png" % key)))
        for suffix, img in (("", a), ("_x0", np.roll(a, -346, axis=1))):
            p = os.path.join(src, key + suffix + ".png").replace("\\", "/")
            Image.fromarray(img).save(p)
            out[key + suffix] = p
    return out


def scan(src):
    v = []
    for tag in ("", "_x0"):
        v.append({"name": "edge_none" + tag, "source": src["edge" + tag]})
        for k in (0.05, 0.1, 0.2):
            for wrap in (0, 1):
                v.append({"name": "edge_blur%s_w%d%s" % (str(k).replace(".", "p"), wrap, tag), "source": src["edge" + tag],
                          "filters": [{"id": "blur/blur", "params": {"intensity": k, "blur_tilling": wrap}}]})
        v.append({"name": "height_none" + tag, "source": src["height" + tag]})
        for d in (0.01, 0.02):
            for wrap in (0, 1):
                v.append({"name": "height_bevel%s_w%d%s" % (str(d).replace(".", "p"), wrap, tag), "source": src["height" + tag],
                          "filters": [{"id": "bevel/bevel", "params": {"distance": d, "smoothing": 1.0, "uv_tiling": wrap}}]})
                v.append({"name": "height_bsmooth%s_w%d%s" % (str(d).replace(".", "p"), wrap, tag), "source": src["height" + tag],
                          "filters": [{"id": "bevel_smooth/bevel_smooth", "params": {"distance": d, "uv_tiling": wrap}}]})
    return v


def anchor(src):
    return [{"name": "anchor_blur", "source": src["edge"], "anchor": True,
             "filters": [], "reader_filters": [{"id": "blur/blur", "params": {"intensity": 0.1, "blur_tilling": 1}}]},
            {"name": "anchor_plain", "source": src["edge"], "anchor": True, "filters": [], "reader_filters": []}]


if __name__ == "__main__":
    layout, probe, which = sys.argv[1:4]
    src = sources(layout, os.path.join(probe, "src"))
    data = {"scan": scan, "anchor": anchor}[which](src)
    with open(os.path.join(probe, "variants_%s.json" % which), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    print(len(data), "variants")
