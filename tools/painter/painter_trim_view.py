"""Host script: lit pictures of a trim set in the engine form, beside the old one.

    python tools/painter/painter_trim_view.py --new <folder> --old <folder> --out <folder> [--name t_rivendell_wall_trim_a]

Light from the upper left (rivendell_trim_maps.shade, the same as the bake comparison), applied in linear light to
the diffuse, times the occlusion of the _s blue channel. Writes lit_new.png, lit_old.png, lit_new_3x1.png,
lit_old_3x1.png and lit_old_new.png (old above new). The normal maps are green down.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oneoff"))
import rivendell_trim_maps as rtm  # noqa: E402


def lit(folder, name):
    d = np.asarray(Image.open(os.path.join(folder, name + "_d.png")).convert("RGB"), dtype=float) / 255.0
    n = np.asarray(Image.open(os.path.join(folder, name + "_n.png")).convert("RGB"), dtype=float) / 255.0
    s = np.asarray(Image.open(os.path.join(folder, name + "_s.png")).convert("RGB"), dtype=float) / 255.0
    n_up = n.copy()
    n_up[..., 1] = 1.0 - n_up[..., 1]
    k = rtm.shade(n_up, s[..., 2]) / 0.62
    lin = np.where(d <= 0.04045, d / 12.92, ((d + 0.055) / 1.055) ** 2.4)
    out = np.clip(lin * k[..., None], 0, 1)
    return np.where(out <= 0.0031308, out * 12.92, 1.055 * out ** (1 / 2.4) - 0.055)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--new", required=True)
    ap.add_argument("--old", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--name", default="t_rivendell_wall_trim_a")
    o = ap.parse_args(argv)
    os.makedirs(o.out, exist_ok=True)
    new, old = lit(o.new, o.name), lit(o.old, o.name)
    for tag, a in (("new", new), ("old", old)):
        Image.fromarray(rtm._u8(a)).save(os.path.join(o.out, "lit_%s.png" % tag))
        Image.fromarray(rtm.tile_u(rtm._u8(a), 3)).save(os.path.join(o.out, "lit_%s_3x1.png" % tag))
    Image.fromarray(rtm._u8(np.concatenate([old, new], axis=0))).save(os.path.join(o.out, "lit_old_new.png"))


if __name__ == "__main__":
    main()
