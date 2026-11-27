"""Layout maps for a heap of broken stone with no courses and no direction (round 4, the heap set).

    python tools/painter/painter_rubble_layout.py --out <folder> [--size 2048] [--ppm 922] [--seed 1]
        [--piece-m 0.09,0.34] [--gap-mm 10,40]

The pieces are the cells of a periodic Voronoi diagram: convex polygons, so every break is straight and every
corner angular. The seeds come from dart throwing with a different spacing for each seed, which gives pieces
of very different size side by side. Each piece is shrunk by its own gap (bedding of dust between the
pieces; a deep gap is a piece that sits low), has its own tone, its own flat tilt and a small rounded rim.
Everything wraps, so the tile repeats with no seam.

Files in <folder> (the stone recipes read them as layout_<key>.png): id (tone per piece), height (16 bit: pieces
above, dust low), dust (the dust between pieces), ao (1 is open), edgelight (a thin light rim on each piece).
"""
import argparse
import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.spatial import cKDTree

KEYS = ("id", "height", "dust", "ao", "edgelight")


def seed_points(size, ppm, spacing_m, rng, tries=20000):
    """Seeds in a size x size px tile by dart throwing. Each seed has a spacing (log-uniform over `spacing_m`,
    in px: spacing_m * ppm); two seeds stay at least the mean of their spacings apart, measured over the wrap.
    Returns (points as an (n, 2) array of x, y; spacings in px)."""
    lo, hi = spacing_m[0] * ppm, spacing_m[1] * ppm
    pts, rad = np.empty((0, 2)), np.empty(0)
    cand = rng.random((tries, 2)) * size
    spac = np.exp(rng.uniform(np.log(lo), np.log(hi), tries))
    order = np.argsort(-spac)  # the large seeds first, the small ones fill the gaps
    for i in order:
        if len(pts):
            d = np.abs(pts - cand[i])
            d = np.minimum(d, size - d)
            if (np.sqrt((d ** 2).sum(1)) < 0.5 * (rad + spac[i])).any():
                continue
        pts = np.vstack([pts, cand[i]])
        rad = np.append(rad, spac[i])
    return pts, rad


def voronoi_labels(points, size, weights=None, k=16, chunk=250000):
    """The seed that owns each pixel of a size x size tile, over the wrap. With no weights it is the nearest
    seed. With weights it is the seed of least power distance (distance squared minus weight squared), which is a
    power diagram: the borders stay straight lines and a heavy seed owns a larger piece. The k nearest seeds are
    compared (16 is enough for weights within a factor of 4 of each other)."""
    pts = np.asarray(points, dtype=float) % size
    tree = cKDTree(pts, boxsize=size)
    ys, xs = np.mgrid[0:size, 0:size]
    grid = np.stack([xs.ravel() + 0.5, ys.ravel() + 0.5], axis=1)
    out = np.empty(len(grid), dtype=np.int64)
    k = min(k, len(pts))
    w2 = None if weights is None else np.asarray(weights, dtype=float) ** 2
    for i in range(0, len(grid), chunk):
        dist, idx = tree.query(grid[i:i + chunk], k=k)
        if w2 is None or k == 1:
            out[i:i + chunk] = idx if k == 1 else idx[:, 0]
        else:
            power = dist ** 2 - w2[idx]
            out[i:i + chunk] = idx[np.arange(len(idx)), np.argmin(power, axis=1)]
    return out.reshape(size, size)


def merge_roots(labels, n, probability, rng):
    """For each of the n cell ids, the id of the piece it belongs to after a random merge of touching pairs
    (over the wrap). A cell joins at most one other, so a piece is one or two cells: the union of two convex
    cells is an angular, often L-shaped slab. Probability 0 leaves every cell alone."""
    roots = np.arange(n)
    if probability <= 0:
        return roots
    lab = np.asarray(labels)
    pairs = set()
    for ax in (0, 1):
        other = np.roll(lab, -1, axis=ax)
        m = lab != other
        for a, b in zip(lab[m].tolist(), other[m].tolist()):
            pairs.add((min(a, b), max(a, b)))
    pairs = sorted(pairs)
    taken = set()
    for i in rng.permutation(len(pairs)):
        a, b = pairs[i]
        if a in taken or b in taken or rng.random() >= probability:
            continue
        roots[b] = a
        taken.update((a, b))
    return roots


def boundary_distance(labels, pad=256):
    """Pixels from each pixel to the nearest boundary pixel (a pixel whose label differs from a 4-neighbour,
    over the wrap). Zero on a boundary. Exact up to `pad` px, which is more than any rim or gap needs."""
    lab = np.asarray(labels)
    edge = np.zeros(lab.shape, bool)
    for ax in (0, 1):
        for sh in (1, -1):
            edge |= lab != np.roll(lab, sh, axis=ax)
    p = min(pad, lab.shape[0], lab.shape[1])
    wrapped = np.pad(~edge, p, mode="wrap")
    d = ndimage.distance_transform_edt(wrapped)
    return d[p:-p, p:-p]


def _smooth(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def build_maps(size, ppm, seed=1, piece_m=(0.09, 0.34), gap_mm=(10.0, 40.0), rim_mm=8.0, tries=20000, merge=0.35,
               sunk=0.25):
    """The five layout maps as float arrays in 0..1, and an "info" dict. `piece_m` is the range of the seed
    spacing in metres (a piece is about 1.35 times its spacing across before the gap is cut); `merge` the chance that a cell joins a
    touching one; `sunk` the share of pieces that sit low in a wider bed of dust; `gap_mm` the range of
    the dust bedding of a piece (each piece takes its own value); `rim_mm` the width of the rounded rim."""
    rng = np.random.default_rng(seed)
    pts, rad = seed_points(size, ppm, piece_m, rng, tries)
    n = len(pts)
    labels = voronoi_labels(pts, size, 0.45 * rad)
    labels = merge_roots(labels, n, merge, rng)[labels]
    dist = boundary_distance(labels)
    gap = rng.uniform(gap_mm[0], gap_mm[1], n) / 1000.0 * ppm  # px, the full width of a piece's bedding
    tone = rng.random(n)
    sink = rng.random(n) < sunk
    gap = np.where(sink, gap * 1.8, gap)
    base = rng.uniform(0.50, 0.92, n) * np.where(sink, 0.55, 1.0)
    ang = rng.uniform(0, 2 * np.pi, n)
    tilt = rng.uniform(0.06, 0.30, n)  # height change from the centre to the rim of a piece
    e = dist - 0.5 * gap[labels]  # px inside the piece, negative in the dust
    ys, xs = np.mgrid[0:size, 0:size]
    dx = (xs + 0.5 - pts[labels, 0] + size / 2.0) % size - size / 2.0
    dy = (ys + 0.5 - pts[labels, 1] + size / 2.0) % size - size / 2.0
    r = np.maximum(rad[labels] * 0.7, 1.0)
    plane = base[labels] + tilt[labels] * (np.cos(ang[labels]) * dx + np.sin(ang[labels]) * dy) / r
    rim = max(rim_mm / 1000.0 * ppm, 1.0)
    low = 0.10
    ramp = _smooth(e / rim)
    height = np.clip(low + (np.clip(plane, 0.2, 1.0) - low) * ramp, 0.0, 1.0)
    dust = np.clip(1.0 - e / 1.5, 0.0, 1.0)
    occ_w = max(14.0 / 1000.0 * ppm, 1.0)
    ao = np.where(e < 0, 0.55, 0.55 + 0.45 * _smooth(e / occ_w))
    ring = np.exp(-(((e - 0.35 * rim) / max(0.4 * rim, 0.8)) ** 2)) * (e >= 0)
    area = np.bincount(labels[e > 0], minlength=n).astype(float)
    diam = np.sqrt(4.0 * area[area > 0] / np.pi) / ppm
    maps = {"id": tone[labels], "height": height, "dust": dust, "ao": np.clip(ao, 0, 1), "edgelight": np.clip(ring, 0, 1)}
    maps["info"] = {"seed": seed, "size": size, "ppm": ppm, "piece_count": int((area > 0).sum()),
                    "median_diameter_m": round(float(np.median(diam)), 3),
                    "min_diameter_m": round(float(diam.min()), 3), "max_diameter_m": round(float(diam.max()), 3),
                    "dust_share": round(float((dust > 0.5).mean()), 3), "piece_m": list(piece_m), "gap_mm": list(gap_mm)}
    return maps


def write_maps(maps, folder):
    """The maps as layout_<key>.png in `folder` (height 16 bit, the rest 8 bit) and layout.json. Returns the paths."""
    os.makedirs(folder, exist_ok=True)
    files = {}
    for key in KEYS:
        a = np.clip(maps[key], 0.0, 1.0)
        path = os.path.join(folder, "layout_%s.png" % key)
        if key == "height":
            Image.fromarray(np.rint(a * 65535).astype(np.uint16)).save(path)
        else:
            Image.fromarray(np.rint(a * 255).astype(np.uint8)).save(path)
        files[key] = path
    json.dump({"files": files, "info": maps["info"]}, open(os.path.join(folder, "layout.json"), "w"), indent=1)
    return files


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--size", type=int, default=2048)
    ap.add_argument("--ppm", type=float, default=922.0, help="pixels per metre of the layout (922 at 2048 px)")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--piece-m", default="0.09,0.34")
    ap.add_argument("--gap-mm", default="10,40")
    a = ap.parse_args(argv)
    pm = tuple(float(v) for v in a.piece_m.split(","))
    gm = tuple(float(v) for v in a.gap_mm.split(","))
    m = build_maps(a.size, a.ppm, a.seed, pm, gm)
    write_maps(m, a.out)
    print(json.dumps(m["info"]))


if __name__ == "__main__":
    main()
