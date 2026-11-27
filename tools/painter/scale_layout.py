"""Layout maps for a roof of fish-scale slates (round 5, the roof slate set).

    python tools/painter/scale_layout.py --out <folder> [--size 2048] [--ppm 922] [--seed 1] [--edge-mm 7.5]

One tile is `rows` rows of `cols` scales; every second row moves by half a scale. A scale shows a shield: straight
sides and a round lower end. Each scale lies on the two scales of the row below, so its lower edge stands
`edge_mm` proud of them and its surface slopes back to nothing at its upper end, under the row above. The slope is
the same everywhere (edge_mm per pitch), so the step at every lower edge is exactly edge_mm.

Rows count from the eave (row 0). The maps are in image order: the eave is at the bottom, the rounded end of a scale
points down. Everything wraps, so the tile repeats with no seam (rows must be even for the stagger to wrap).

Files in <folder> (the recipes read them as layout_<key>.png): id (calm tone per scale, 0.5 is neutral), outdark and outlight (masks of the outlier scales), height (16 bit,
one unit is `unit_mm`), gap (a dark line round each scale, 2 to 3 mm), ao (1 is open; a contact shade on the lower
scale under each edge), edgelight (a thin rim on the lower end of each scale), cover and cover2 (masks for two shifted copies of a 1 by 1 tile
material that hide its joint: see joint_cover_maps) and owner (row * cols + col, for the
measures).
"""
import argparse
import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage

KEYS = ("id", "height", "gap", "ao", "edgelight", "cover", "cover2", "outdark", "outlight")


def shield_depth(width, aspect=1.0):
    """Length of the round lower end of a scale `width` wide. A half circle has half the width; an aspect above 1
    makes it a longer, slightly pointed end."""
    return 0.5 * width * aspect


def shield_half_width(v, half_width, depth):
    """Half the width of the shield at `v` above its lower tip: 0 at the tip, `half_width` from `depth` up, an
    ellipse between. `v` may be an array."""
    t = np.clip((depth - np.asarray(v, dtype=float)) / depth, 0.0, 1.0)
    return half_width * np.sqrt(np.clip(1.0 - t * t, 0.0, 1.0))


def _smooth(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def _columns(size, width, k, cols):
    x = np.arange(size) + 0.5
    off = 0.5 * (k % 2)
    t = (x - (0.5 + off) * width) / width
    c = np.rint(t)
    u = (t - c) * width
    return (c.astype(np.int64) % cols), u


def border_cover(size, band_px=48.0, feather_px=24.0):
    """A mask that is 1 within `band_px` of the tile border (over the wrap) and falls to 0 over `feather_px` more.
    A tiling material of one tile has its joint on the tile border; a second copy of it moved by half a tile,
    laid through this mask, hides that joint."""
    i = np.arange(size) + 0.5
    d = np.minimum(i, size - i)  # px to the nearest border of the tile
    m = 1.0 - _smooth((d - band_px) / max(feather_px, 1e-6))
    return np.maximum(m[:, None], m[None, :])


def joint_cover_maps(size, band_px=72.0, feather_px=36.0, hole_px=150.0, hole_feather_px=60.0):
    """Masks (B, C) that hide the joints of a 1 by 1 tile material with two shifted copies. The copy B moved by half
    a tile has its own joints along the middle lines, so it may cover the border band everywhere except round the
    two points (0, size / 2) and (size / 2, 0) where those lines meet the band. There the copy C, moved by a
    quarter tile (its joints are a quarter tile away), takes over. B is the border band times (1 - holes); C is the
    holes. Lay A (the base), then B through B, then C through C."""
    band = border_cover(size, band_px, feather_px)
    i = np.arange(size) + 0.5
    d_edge = np.minimum(i, size - i)          # px to the border
    d_mid = np.abs(i - size / 2.0)            # px to the middle line
    holes = np.zeros((size, size))
    for dy, dx in ((d_mid[:, None], d_edge[None, :]), (d_edge[:, None], d_mid[None, :])):
        dist = np.sqrt(dy * dy + dx * dx)
        holes = np.maximum(holes, 1.0 - _smooth((dist - hole_px) / max(hole_feather_px, 1e-6)))
    return band * (1.0 - holes), holes


def tone_per_scale(rows, cols, seed, spread=0.35, outlier_every=12):
    """A tone t in -1..1 for each of rows * cols scales, all within +-spread (the calm tone), and the outliers: one
    scale in `outlier_every` (an exact count, round(rows * cols / outlier_every)), each dark or light at random.
    Returns (tones, outlier indices, dark flags of the outliers). The outliers keep their calm tone as well; their
    extra push is a separate layer in the recipe (a Multiply for the dark ones, a light fill for the others), because
    Overlay lightens and darkens by very different amounts."""
    rng = np.random.default_rng(seed)
    n = rows * cols
    t = rng.uniform(-spread, spread, n)
    count = int(round(n / outlier_every))
    out = rng.choice(n, size=count, replace=False)
    dark = rng.random(count) < 0.5
    order = np.argsort(out)
    return t, [int(i) for i in out[order]], [bool(d) for d in dark[order]]


def build_maps(size=2048, ppm=922.0, rows=12, cols=10, seed=1, edge_mm=7.5, gap_mm=2.5, gap_depth_mm=3.0,
               aspect=1.0, shade_mm=15.0, shade_strength=0.3, rim_mm=3.5, reach_m=0.34, unit_mm=22.0,
               tone_spread=0.35, outlier_every=12, cover_px=48.0):
    """The layout maps as arrays and an "info" dict. See the module text. `reach_m` is how far above its tip a
    scale extends (it must be longer than the visible part: pitch + half a width); `unit_mm` is the height one
    unit of the 16 bit map stands for in Painter (22 mm for one relief layer at opacity 1)."""
    if rows % 2:
        raise ValueError("rows must be even so that the stagger wraps")
    width = size / cols
    pitch = size / rows
    half = 0.5 * width
    depth = shield_depth(width, aspect)
    reach = reach_m * ppm
    slope = edge_mm / (pitch / ppm)  # mm per metre of height lost going up a scale
    total = 3 * size
    owner = np.full((total, size), -1, dtype=np.int32)
    v_all = np.zeros((total, size), dtype=np.float32)
    u_all = np.zeros((total, size), dtype=np.float32)
    k_all = np.full((total, size), -1, dtype=np.int16)
    spans = {}
    for k in range(3 * rows):
        j0 = max(int(np.floor(k * pitch)), 0)
        j1 = min(int(np.ceil(k * pitch + reach)) + 1, total)
        if j0 >= j1:
            continue
        js = np.arange(j0, j1)
        v = (js + 0.5) - k * pitch
        c, u = _columns(size, width, k, cols)
        ok_v = (v >= 0) & (v <= reach)
        hw_v = shield_half_width(v, half, depth)
        inside = ok_v[:, None] & (np.abs(u)[None, :] <= hw_v[:, None])
        ids = ((k % rows) * cols + c)[None, :] * np.ones((len(js), 1), dtype=np.int64)
        sl = (slice(j0, j1), slice(None))
        owner[sl] = np.where(inside, ids, owner[sl])
        v_all[sl] = np.where(inside, v[:, None], v_all[sl])
        u_all[sl] = np.where(inside, u[None, :], u_all[sl])
        k_all[sl] = np.where(inside, k, k_all[sl])
        spans[k] = (j0, j1)
    mid = slice(size, 2 * size)
    if (owner[mid] < 0).any():
        raise RuntimeError("scales leave part of the tile bare: reach_m is too short")
    v_t, u_t, k_t = v_all[mid], u_all[mid], k_all[mid]

    # height: the sawtooth, with a small groove along the sides of each scale
    v_m = v_t / ppm
    h_mm = slope * (reach_m - v_m)
    ratio = np.sqrt((u_t / half) ** 2 + np.clip((depth - v_t) / depth, 0.0, 1.0) ** 2)
    dist_in = np.where(v_t >= depth, half - np.abs(u_t), half * (1.0 - ratio))
    gap_px = gap_mm * ppm / 1000.0
    gap = np.clip(gap_px / 2.0 - dist_in + 0.5, 0.0, 1.0)
    side = np.clip((v_t - depth) / 2.0 + 0.5, 0.0, 1.0)
    h_mm = h_mm - gap_depth_mm * gap * side

    # rim light on the lower end
    rim_px = max(rim_mm * ppm / 1000.0, 0.7)
    taper = np.clip((depth - v_t) / (0.15 * depth) + 0.0, 0.0, 1.0)
    edgelight = np.exp(-((np.maximum(dist_in, 0.0) / rim_px) ** 2)) * taper

    # the contact shade: on a pixel of a lower row, near the shield of the row above it
    shade = np.zeros((total, size), dtype=np.float32)
    w_px = max(shade_mm * ppm / 1000.0, 1.0)
    margin = int(np.ceil(w_px)) + 4
    for k in range(rows, 2 * rows + 2):
        j0, j1 = spans[k]
        b0, b1 = max(j0 - margin, 0), min(j1 + margin, total)
        js = np.arange(b0, b1)
        v = (js + 0.5) - k * pitch
        c, u = _columns(size, width, k, cols)
        hw_v = shield_half_width(v, half, depth)
        inside = ((v >= 0) & (v <= reach))[:, None] & (np.abs(u)[None, :] <= hw_v[:, None])
        padded = np.pad(~inside, ((0, 0), (margin, margin)), mode="wrap")
        dist = ndimage.distance_transform_edt(padded)[:, margin:-margin]
        fall = np.clip(1.0 - dist / w_px, 0.0, 1.0) ** 2
        lower = k_all[b0:b1] < k
        part = np.where(lower & ~inside, fall, 0.0).astype(np.float32)
        shade[b0:b1] = np.maximum(shade[b0:b1], part)
    shade_t = shade[mid]

    cover_b, cover_c = joint_cover_maps(size, cover_px, 1.5 * cover_px, hole_px=2.6 * cover_px,
                                      hole_feather_px=0.9 * cover_px)
    tones, outliers, dark_flags = tone_per_scale(rows, cols, seed, tone_spread, outlier_every)
    dark_ids = {o for o, d in zip(outliers, dark_flags) if d}
    light_ids = set(outliers) - dark_ids
    own = owner[mid]
    id_map = 0.5 + 0.5 * tones[owner[mid]]
    unit = float(unit_mm)
    height = (h_mm - 0.0) / unit
    if height.max() > 1.0 or height.min() < -1e-6:
        raise ValueError("the height does not fit one unit of %.1f mm: raise unit_mm" % unit)

    def image(a):
        return np.ascontiguousarray(a[::-1])

    maps = {"id": image(id_map), "height": image(height), "gap": image(gap), "edgelight": image(edgelight),
            "ao": image(1.0 - shade_strength * shade_t), "cover": cover_b, "cover2": cover_c,
            "outdark": image(np.isin(own, list(dark_ids)).astype(float)),
            "outlight": image(np.isin(own, list(light_ids)).astype(float)), "owner": image(owner[mid])}
    maps["info"] = {"seed": seed, "size": size, "ppm": ppm, "rows": rows, "cols": cols, "scale_count": rows * cols,
                    "outlier_count": len(outliers), "outlier_ids": outliers, "outlier_dark": dark_flags, "edge_mm": edge_mm, "gap_mm": gap_mm,
                    "gap_depth_mm": gap_depth_mm, "shade_mm": shade_mm, "shade_strength": shade_strength,
                    "unit_mm": unit, "pitch_m": round(pitch / ppm, 4), "width_m": round(width / ppm, 4),
                    "height_mm_max": round(float(h_mm.max()), 2), "aspect": aspect, "reach_m": reach_m,
                    "tone_per_scale": [round(float(0.5 + 0.5 * t), 4) for t in tones]}
    return maps


def measure_scales(lum, owner, outlier_ids, ao, margin_px=8):
    """Numbers of a made slate set, from its luminance `lum`, the owner map and the ao map (any size; the maps are
    taken at the size of `lum` by nearest neighbour). `calm_spread`: largest minus smallest mean luminance of the
    scales that are not outliers, each taken inside `margin_px` of its border. `outlier_min`/`outlier_max`: the
    smallest and largest offset of an outlier's mean from the median of the calm ones. `shade_levels`: the median
    mean of a scale's interior minus the mean luminance of its darkest shade pixels (ao within 0.05 of the
    scale's lowest), the contact shade in levels."""
    lum = np.asarray(lum, dtype=float)
    n = lum.shape[0]

    def fit(a):
        a = np.asarray(a)
        if a.shape[0] == n:
            return a
        idx = (np.arange(n) * a.shape[0] // n)
        return a[np.ix_(idx, idx)]

    owner, ao = fit(owner), fit(ao)
    k = 2 * int(margin_px) + 1
    inner = ndimage.minimum_filter(owner, size=k) == ndimage.maximum_filter(owner, size=k)
    means, shade = {}, []
    for o in np.unique(owner):
        sel = inner & (owner == o)
        if sel.sum() >= 4:
            means[int(o)] = float(lum[sel].mean())
    for o in means:
        zone = (owner == o) & (ao < 0.995)
        if zone.sum() >= 4:
            lo = ao[zone].min()
            dark = zone & (ao <= lo + 0.05)
            shade.append(means[o] - float(lum[dark].mean()))
    outs = set(int(i) for i in outlier_ids)
    calm = [v for o, v in means.items() if o not in outs]
    med = float(np.median(calm))
    off = [abs(v - med) for o, v in means.items() if o in outs]
    return {"calm_spread": round(max(calm) - min(calm), 1), "outliers": len(off),
            "outlier_min": round(min(off), 1) if off else None, "outlier_max": round(max(off), 1) if off else None,
            "shade_levels": round(float(np.median(shade)), 1) if shade else None, "scales_measured": len(means)}


def write_maps(maps, folder):
    """The maps as layout_<key>.png in `folder` (height and owner 16 bit, the rest 8 bit) and layout.json. Returns
    the paths."""
    os.makedirs(folder, exist_ok=True)
    files = {}
    for key in KEYS + ("owner",):
        a = np.asarray(maps[key])
        path = os.path.join(folder, "layout_%s.png" % key)
        if key == "height":
            Image.fromarray(np.rint(np.clip(a, 0.0, 1.0) * 65535).astype(np.uint16)).save(path)
        elif key == "owner":
            Image.fromarray(a.astype(np.uint16)).save(path)
        else:
            Image.fromarray(np.rint(np.clip(a, 0.0, 1.0) * 255).astype(np.uint8)).save(path)
        files[key] = path
    json.dump({"files": files, "info": maps["info"]}, open(os.path.join(folder, "layout.json"), "w"), indent=1)
    return files


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--size", type=int, default=2048)
    ap.add_argument("--ppm", type=float, default=922.0)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--rows", type=int, default=12)
    ap.add_argument("--cols", type=int, default=10)
    ap.add_argument("--edge-mm", type=float, default=7.5)
    ap.add_argument("--gap-mm", type=float, default=2.5)
    ap.add_argument("--aspect", type=float, default=1.0)
    ap.add_argument("--shade-mm", type=float, default=15.0)
    ap.add_argument("--shade-strength", type=float, default=0.3)
    a = ap.parse_args(argv)
    m = build_maps(a.size, a.ppm, a.rows, a.cols, a.seed, a.edge_mm, a.gap_mm, aspect=a.aspect, shade_mm=a.shade_mm,
                   shade_strength=a.shade_strength)
    write_maps(m, a.out)
    print(json.dumps({k: v for k, v in m["info"].items() if k != "tone_per_scale"}))


if __name__ == "__main__":
    main()
