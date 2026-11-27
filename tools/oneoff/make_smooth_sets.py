"""Joint-free ("smooth stone, no tile") sets of the Rivendell kit, made on the host from maps that exist.

    python tools/oneoff/make_smooth_sets.py [--out <folder>]

Mike, 2026-11-25: "Remove the TILE look from this and make it smooth stone. The trim should be a smooth
stone no tile but a cream color (a bit darker than the stone)". And of the roof slate: "These tiles and
color is perfect for the roof".

It writes three sets of 15 files (five materials, each _d, _n, _s at 1024 px):
    set_smooth      the wall stone with no joints, the plinth as the same stone a little darker, the
                    trim sheet with no joints in a cream, the accepted roof slate, the heap
    set_smooth_b    the same with a darker cream on the trim
    set_hairline    as set_smooth, with hairline joints of one pixel on the wall stone, for comparison
No Painter is needed: the wall stone is the dressed stone export of round 3 (no joint layers), and
the trim is the baked sheet of round 2b, whose one joint per half tile is patched over with
the same strip from 48 px further on.
"""
import argparse
import json
import os
import shutil
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "painter"))

TEX = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/textures"
STONE_SRC = TEX + "/v07/dressed/engine"          # the dressed stone of round 3: no joint layers
TRIM_SRC = TEX + "/v06/trim_baked"               # the baked sheet of round 2b: mouldings, one faint joint per half tile
KEEP_SRC = TEX + "/v08/set_final"                # the accepted roof slate (slate_b) and the heap
LAYOUT = TEX + "/v07/layout_a/layout.json"       # the block rectangles of the wall stone, at 2048 px
STONE, PLINTH, TRIM = "t_rivendell_wall_stone_a", "t_rivendell_wall_rubble_a", "t_rivendell_wall_trim_a"
SLATE, HEAP = "t_rivendell_roof_slate_a", "t_rivendell_wall_heap_a"
PLINTH_LUM = 180.0                               # the wall stone has about 190
CREAM_A = (190.0, 176.0, 148.0)                  # a cream, a bit darker than the stone (luminance 177)
CREAM_B = (182.0, 167.0, 138.0)                  # a darker cream (luminance 168)
HAIRLINE_SHARE = 0.06                            # a hairline joint is this share darker than the face
TRIM_JOINT_COLUMNS = (0, 512)                    # the joints of the round 2b sheet: U 0 and U 0.5
TRIM_JOINT_HALF = 8                              # columns on each side of a joint that the patch replaces
TRIM_JOINT_OFFSET, TRIM_JOINT_FEATHER = 48, 6    # where the patch comes from, and its cross-fade
TRIM_JOINT_ROWS = slice(515, 1024)               # strips S1 and S2 (V 0 to 0.49), image rows from the top
STAIN = TEX + "/v07/layout_a/layout_stain.png"   # a cloud mask that wraps and has no block pattern
# "a tad bit of wear and tear" (Mike, 2026-11-25): stain amount, pit count per tile, pit darkness, pit depth in px
WEAR = {"set_smooth_w1": (0.012, 220, 0.10, 0.35), "set_smooth_w2": (0.022, 500, 0.14, 0.5)}


def luminance(rgb):
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]


def scaled_target(rgb, lum):
    """The colour `rgb` scaled to the luminance `lum`: the hue stays."""
    k = lum / luminance(rgb)
    return tuple(c * k for c in rgb)


def patch_columns(img, x, half, offset, feather, rows=slice(None)):
    """A copy of `img` in which the columns x-half..x+half of `rows` are the columns `offset` further on,
    with a linear cross-fade over `feather` columns at each side. Columns wrap at the tile edge. A member
    that runs along U has the same section at every U, so the copied stone hides a joint with real grain,
    where a blend of two columns would leave a smear."""
    src = np.asarray(img, dtype=float)
    out = src.copy()
    w = src.shape[1]
    for k in range(-half - feather, half + feather + 1):
        weight = 1.0 if abs(k) <= half else 1.0 - (abs(k) - half) / float(feather + 1)
        col = (x + k) % w
        out[rows, col] = src[rows, col] * (1.0 - weight) + src[rows, (x + k + offset) % w] * weight
    return out


def hairline_mask(blocks, src, out):
    """A mask out by out, 1 on a line of one pixel at the upper edge of every course (the whole width)
    and at the low end of every block (inside its course), 0 elsewhere. `blocks` hold x0, x1, y0, y1
    at `src` pixels; a block may reach past the tile edge and wraps."""
    k = out / float(src)
    m = np.zeros((out, out))
    for b in blocks:
        y0 = int(round(b["y0"] * k)) % out
        y1 = int(round(b["y1"] * k))
        m[y0, :] = 1.0
        m[y0:min(y1, out), int(round(b["x0"] * k)) % out] = 1.0
    return m


def darken(img, mask, share):
    """`img` made darker by `share` where `mask` is 1."""
    return np.asarray(img, dtype=float) * (1.0 - share * np.asarray(mask, dtype=float)[..., None])


def stain_gain(mask, amount, limit=2.5):
    """A gain map with mean 1 from a cloud mask: 1 + amount times the mask in standard deviations,
    cut at `limit` deviations. A flat mask gives 1 everywhere."""
    m = np.asarray(mask, dtype=float)
    std = m.std()
    if std < 1e-9:
        return np.ones_like(m)
    return 1.0 + amount * np.clip((m - m.mean()) / std, -limit, limit)


def pit_field(size, count, seed, sigma):
    """A map size by size, 0 to 1, with `count` small round pits at seeded random places (1 at the
    deepest point of the field). It wraps at the tile edge."""
    from tile_compose import gauss_blur  # the tested periodic blur of the stone tools

    rng = np.random.default_rng(seed)
    field = np.zeros((size, size))
    field[rng.integers(0, size, count), rng.integers(0, size, count)] = 1.0
    field = gauss_blur(field, sigma, periodic=True)
    return np.clip(field / field.max(), 0.0, 1.0)


def worn_diffuse(d, gain, pits, pit_dark):
    """The diffuse with the stain gain and with each pit darker by `pit_dark` at its deepest point."""
    g = np.asarray(gain, dtype=float)[..., None] * (1.0 - pit_dark * np.asarray(pits, dtype=float)[..., None])
    return np.asarray(d, dtype=float) * g


def worn_set(stone, stain, amount, pit_count, pit_dark, pit_depth_px, seed=11):
    """The three stone maps with a little wear: soft stains in the colour, and sparse small pits in
    the colour, the normal map (green down) and the gloss."""
    from tile_compose import combine_normal

    size = stone["d"].shape[0]
    pits = pit_field(size, pit_count, seed, 0.9)
    s = stone["s"].copy()
    s[..., 1] = s[..., 1] * (1.0 - 0.3 * pits)   # a pit is rougher
    s[..., 2] = s[..., 2] * (1.0 - 0.3 * pits)   # and a little occluded
    return {"d": worn_diffuse(stone["d"], stain_gain(stain, amount), pits, pit_dark),
            "n": combine_normal(stone["n"], -pit_depth_px * pits).astype(float),
            "s": s}


def _load(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(float)


def _save(arr, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(np.clip(np.rint(arr), 0, 255).astype(np.uint8)).save(path)


def _trim_without_joints(key):
    img = _load("%s/%s_%s.png" % (TRIM_SRC, TRIM, key))
    for x in TRIM_JOINT_COLUMNS:
        img = patch_columns(img, x, TRIM_JOINT_HALF, TRIM_JOINT_OFFSET, TRIM_JOINT_FEATHER, rows=TRIM_JOINT_ROWS)
    return img


def build(out):
    """Writes the three sets under `out` and returns their measured means."""
    from painter_material_set import gain_to_mean  # the tested gain per channel of round 4

    stone = {k: _load("%s/%s_%s.png" % (STONE_SRC, STONE, k)) for k in "dns"}
    stone_mean = tuple(stone["d"].reshape(-1, 3).mean(0))
    plinth_d = gain_to_mean(stone["d"], scaled_target(stone_mean, PLINTH_LUM))
    trim = {k: _trim_without_joints(k) for k in "dns"}
    with open(LAYOUT, encoding="utf-8") as fh:
        blocks = json.load(fh)["blocks"]
    hair_d = darken(stone["d"], hairline_mask(blocks, 2048, 1024), HAIRLINE_SHARE)
    report = {"stone_mean": [round(c, 1) for c in stone_mean]}
    plain = dict(stone, d=stone["d"])
    stain = np.asarray(Image.open(STAIN).convert("L").resize((1024, 1024), Image.LANCZOS)).astype(float)
    worn = {name: worn_set(stone, stain, *WEAR[name]) for name in WEAR}
    sets = {"set_smooth": (plain, CREAM_A), "set_smooth_b": (plain, CREAM_B),
            "set_hairline": (dict(stone, d=hair_d), CREAM_A),
            "set_smooth_w1": (worn["set_smooth_w1"], CREAM_A), "set_smooth_w2": (worn["set_smooth_w2"], CREAM_A)}
    for name, (maps, cream) in sets.items():
        folder = os.path.join(out, name)
        stone_d = maps["d"]
        plinth_d = gain_to_mean(stone_d, scaled_target(stone_mean, PLINTH_LUM))
        _save(stone_d, "%s/%s_d.png" % (folder, STONE))
        _save(plinth_d, "%s/%s_d.png" % (folder, PLINTH))
        trim_d = gain_to_mean(trim["d"], cream)
        _save(trim_d, "%s/%s_d.png" % (folder, TRIM))
        for k in "ns":
            _save(maps[k], "%s/%s_%s.png" % (folder, STONE, k))
            _save(maps[k], "%s/%s_%s.png" % (folder, PLINTH, k))
            _save(trim[k], "%s/%s_%s.png" % (folder, TRIM, k))
        for keep in (SLATE, HEAP):
            for k in "dns":
                shutil.copyfile("%s/%s_%s.png" % (KEEP_SRC, keep, k), "%s/%s_%s.png" % (folder, keep, k))
        report[name] = {"trim_mean": [round(c, 1) for c in trim_d.reshape(-1, 3).mean(0)],
                        "plinth_mean": [round(c, 1) for c in plinth_d.reshape(-1, 3).mean(0)],
                        "stone_d_mean": [round(c, 1) for c in stone_d.reshape(-1, 3).mean(0)],
                        "files": len(os.listdir(folder))}
    with open(os.path.join(out, "smooth_sets_report.json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=1)
    return report


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", default=TEX + "/v10")
    args = ap.parse_args(argv)
    print(json.dumps(build(args.out), indent=1))


if __name__ == "__main__":
    main()
