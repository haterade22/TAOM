"""Previews of the Painter-route stone sets: 3 x 3 tiled diffuse, a lit view, and a sheet that puts each set
beside a Gondor control at the same pixel scale per metre (ours 1.333 m per 1024 px = 768 px/m; Gondor 3 m
per 2048 px = 683 px/m; ours is resampled to 683). Run: python tools/oneoff/preview_painter_stone_sets.py <v02_painter folder>"""
import os
import sys

import numpy as np
from PIL import Image

GONDOR = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/AssetSources/Scenes/Gondor/textures/"
PAIRS = {"t_rivendell_wall_stone_a": "t_gondor_bricks_large_a_dirty", "t_rivendell_wall_stone_b": "t_gondor_bricks_large_a_dirty",
         "t_rivendell_wall_rubble_a": "t_gondor_rubble_stone_a", "t_rivendell_wall_rubble_b": "t_gondor_rubble_stone_a",
         "t_rivendell_dressed_stone_src": "t_gondor_bricks_large_a_dirty"}
LIGHT = np.array([-0.5, 0.6, 0.62]); LIGHT /= np.linalg.norm(LIGHT)
VIEW = np.array([0, 0, 1.0])


def lit(d, n, s=None):
    """Blinn-Phong from a diffuse (0-255 RGB), an OpenGL normal and an optional R0/G/B spec map."""
    nv = n.astype(float) / 127.5 - 1
    nv[..., 2] = np.sqrt(np.clip(1 - nv[..., 0] ** 2 - nv[..., 1] ** 2, 0, 1))
    nv /= np.linalg.norm(nv, axis=2, keepdims=True)
    alb = (d.astype(float) / 255) ** 2.2
    lam = np.clip(nv @ LIGHT, 0, 1)[..., None]
    h = LIGHT + VIEW; h /= np.linalg.norm(h)
    gloss = 0.4 if s is None else s[..., 2:3].astype(float) / 255
    spec = (np.clip(nv @ h, 0, 1)[..., None] ** 24) * 0.08 * gloss
    out = alb * (0.18 + 0.95 * lam) + spec
    return np.clip(out ** (1 / 2.2) * 255, 0, 255).astype(np.uint8)


def tile(a, k):
    return np.tile(a, (k, k, 1))


def rs(a, size):
    return np.asarray(Image.fromarray(a).resize(size, Image.LANCZOS))


def main(root):
    os.makedirs(os.path.join(root, "preview"), exist_ok=True)
    for name, g in PAIRS.items():
        d = np.asarray(Image.open(f"{root}/{name}_d.png").convert("RGB"))
        n = np.asarray(Image.open(f"{root}/{name}_n.png").convert("RGB"))
        s = np.asarray(Image.open(f"{root}/{name}_s.png").convert("RGB"))
        t3 = tile(d, 3)
        Image.fromarray(rs(t3, (1536, 1536))).save(f"{root}/preview/{name}_tiled3x3.png")
        l3 = lit(tile(d, 3), tile(n, 3), tile(s, 3))
        Image.fromarray(rs(l3, (1536, 1536))).save(f"{root}/preview/{name}_lit3x3.png")
        # same pixel scale per metre: ours 768 px/m -> 683 px/m; a 2.4 m square of each
        side = int(2.4 * 683)
        k = 683 / 768
        ours_d = rs(tile(d, 3), (int(3072 * k), int(3072 * k)))[:side, :side]
        ours_l = rs(l3, (int(3072 * k), int(3072 * k)))[:side, :side]
        gd = np.asarray(Image.open(GONDOR + g + ("_d2.png")).convert("RGB"))
        gn = np.asarray(Image.open(GONDOR + g + "_n.png").convert("RGB"))
        gd2 = tile(gd, 2)[:side, :side]
        gl = lit(gd2, tile(gn, 2)[:side, :side])
        f = 0.36
        sz = (int(side * f), int(side * f))
        row = np.concatenate([rs(x, sz) for x in (ours_d, ours_l, gd2, gl)], axis=1)
        Image.fromarray(row).save(f"{root}/preview/{name}_vs_gondor.png")
        print(name, "gondor", g, "ours mean", d.reshape(-1, 3).mean(0).round(1), "gondor mean", gd.reshape(-1, 3).mean(0).round(1))


if __name__ == "__main__":
    main(sys.argv[1])
