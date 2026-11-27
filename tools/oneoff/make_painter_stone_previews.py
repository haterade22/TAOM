"""Previews of the Painter stone sets (WORK/textures/v02_painter): a 3 x 3 tiled diffuse and a lit
preview per set, and a sheet that sets each beside its Gondor control at one pixel scale per metre.

    python tools/oneoff/make_painter_stone_previews.py --work <v02_painter folder>

The lit preview is a Lambert term (light from the upper left) on the diffuse, with the normal map read
as Y up; the set's own occlusion is not applied (the diffuse already carries a little of it).
"""
import argparse
import os

import numpy as np
from PIL import Image, ImageDraw

GONDOR = ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/AssetSources/"
          "Scenes/Gondor/textures/")
TILE_M = 4.0 / 3.0
PX_PER_M = 256
SIZE_M = 4.0
LIGHT = np.array([-0.5, 0.6, 0.62])
LIGHT = LIGHT / np.linalg.norm(LIGHT)

PAIRS = [
    ("t_rivendell_wall_stone_a", "t_rivendell_wall_stone_a_d.png", "t_rivendell_wall_stone_a_n.png",
     "t_gondor_bricks_large_a_dirty_d2.png", "t_gondor_bricks_large_a_dirty_n.png", 3.0),
    ("t_rivendell_wall_rubble_a", "t_rivendell_wall_rubble_a_d.png", "t_rivendell_wall_rubble_a_n.png",
     "t_gondor_rubble_stone_a_d2.png", "t_gondor_rubble_stone_a_n.png", 3.0),
    ("t_rivendell_dressed_stone_src", "t_rivendell_dressed_stone_src_d.png", "t_rivendell_dressed_stone_src_n.png",
     "t_gondor_stone_trim_a1_d.png", "t_gondor_stone_trim_a1_n.png", 3.0),
]


def lit(base, normal, _ao):
    """Lambert (ambient 0.35) on `base` (float RGB) with a normal map (0..255, Y up)."""
    n = normal.astype(np.float64) / 255.0 * 2 - 1
    n = n / np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    d = np.clip((n * LIGHT).sum(2), 0, 1)
    k = 0.35 + 0.8 * d
    return np.clip(base * k[..., None], 0, 255)


def tile_to_metres(tile, tile_m, px_per_m, size_m):
    """Resize `tile` to tile_m * px_per_m pixels, repeat it, crop to size_m metres."""
    px = int(round(tile_m * px_per_m))
    im = Image.fromarray(np.clip(tile, 0, 255).astype(np.uint8)).resize((px, px), Image.LANCZOS)
    a = np.asarray(im)
    reps = int(np.ceil(size_m / tile_m)) + 1
    big = np.tile(a, (reps, reps, 1))
    n = int(round(size_m * px_per_m))
    return big[:n, :n]


def rgb(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float64)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--work", required=True)
    a = ap.parse_args()
    out = os.path.join(a.work, "preview")
    os.makedirs(out, exist_ok=True)
    rows = []
    for name, d, n, gd, gn, g_tile_m in PAIRS:
        diff, nrm = rgb(os.path.join(a.work, d)), rgb(os.path.join(a.work, n))
        gdiff, gnrm = rgb(GONDOR + gd), rgb(GONDOR + gn)
        # 3 x 3 tiled diffuse and lit, 1024 px per tile pair shown at 768 px per tile
        t3 = np.tile(diff, (3, 3, 1))
        Image.fromarray(t3.astype(np.uint8)).resize((1536, 1536), Image.LANCZOS).save(
            os.path.join(out, f"{name}_tiled3x3.png"))
        l3 = lit(np.tile(diff, (3, 3, 1)), np.tile(nrm, (3, 3, 1)), None)
        Image.fromarray(l3.astype(np.uint8)).resize((1536, 1536), Image.LANCZOS).save(
            os.path.join(out, f"{name}_lit3x3.png"))
        # same pixel scale per metre: 4 m square of each
        ours_d = tile_to_metres(diff, TILE_M, PX_PER_M, SIZE_M)
        ours_l = tile_to_metres(lit(diff, nrm, None), TILE_M, PX_PER_M, SIZE_M)
        g_d = tile_to_metres(gdiff, g_tile_m, PX_PER_M, SIZE_M)
        g_l = tile_to_metres(lit(gdiff, gnrm, None), g_tile_m, PX_PER_M, SIZE_M)
        rows.append((name, ours_d, ours_l, g_d, g_l, gd))
    cell = int(SIZE_M * PX_PER_M)
    pad = 16
    head = 34
    sheet = Image.new("RGB", (4 * cell + 5 * pad, 3 * (cell + head) + pad), (30, 30, 30))
    dr = ImageDraw.Draw(sheet)
    for r, (name, od, ol, gd_, gl, gname) in enumerate(rows):
        y = pad // 2 + r * (cell + head)
        for c, (im, label) in enumerate(((od, name + " diffuse"), (ol, name + " lit"),
                                         (gd_, gname[:-4] + " diffuse"), (gl, gname[:-4] + " lit"))):
            x = pad + c * (cell + pad)
            dr.text((x, y + 4), label, fill=(255, 255, 255))
            sheet.paste(Image.fromarray(im.astype(np.uint8)), (x, y + head))
    sheet.save(os.path.join(out, "painter_vs_gondor.png"))
    print("done", out)


if __name__ == "__main__":
    main()
