#!/usr/bin/env python3
"""Turn photographic stone sources into tiling 1K texture sets (kingdom kits job, issue #1600, v02).

Per source: remove uneven light, make it tile with a minimum-cost seam path that runs through the
joints (blocks stay whole; no cross-fade of whole blocks), resize to 1024 with a wrap-aware Lanczos,
match the colour by per-channel gain, derive a height field from the mid and high frequencies of the
luminance, write a tangent-space normal map (Y up) plus a green-flipped copy, and write a specular map
with the channel statistics of the Gondor controls (read from spec_stats.json, never copied pixels).

The trim sheet uses the plain dressed stone as the surface of five carved strips. The strip height
functions come from make_rivendell_wall_textures.py.

Run:  python tools/oneoff/make_tileable_texture_set.py [--out DIR] [--overlap PX] [--only NAME]
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_rivendell_wall_textures import (  # noqa: E402
    STRIP_STRENGTH, _strip_heights, flatten_margin, flip_green, interior_mad, lit_preview, lum_of,
    save_rgb, seam_mad, smoothstep, strip_rows, to_u8)

WORK = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell"
SRC = WORK + "/textures/sources/imagine_v01/"
DEFAULT_OUT = WORK + "/textures/v02_imagine"
SPEC_STATS = WORK + "/textures/v01/spec_stats.json"
GONDOR = ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/AssetSources/"
          "Scenes/Gondor/textures/")
SIZE = 1024
SAFE = 4
PX_PER_M = 768.0  # 1024 px over a 1.333 m tile

# set name -> source file, target mean, specular control, normal strength, overlap, contrast
SETS = {
    "t_rivendell_wall_stone_a": dict(src="ashlar_b_four_courses", target="#c7b89e",
                                     ctl="t_gondor_bricks_large_a_dirty_s", strength=5.0, contrast=2.4,
                                     relief=0.35, overlap=64, low_feather=40),
    "t_rivendell_wall_stone_b": dict(src="ashlar_a_three_courses", target="#c7b89e",
                                     ctl="t_gondor_bricks_large_a_dirty_s", strength=5.0, contrast=1.8,
                                     relief=0.35, overlap=224, low_feather=96),
    "t_rivendell_wall_rubble_a": dict(src="rubble_a_coursed", target="#877a68",
                                      ctl="t_gondor_rubble_stone_a_s", strength=5.0, contrast=1.0),
    "t_rivendell_wall_rubble_b": dict(src="rubble_b_dry_stone", target="#877a68",
                                      ctl="t_gondor_rubble_stone_a_s", strength=5.0, contrast=1.0),
}
TRIM = dict(src="dressed_stone_plain", target="#ded6cb", ctl="t_gondor_stone_trim_a1_s", strength=2.0)
OVERLAP = 224
FEATHER = 6
JOINT_BIAS = 0.4
FLAT_FRAC = 0.25
LOW_SIGMA = 6.0


# ---------------------------------------------------------------- pure functions

def hex_rgb(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def gauss_periodic(a, sigma):
    """Gaussian blur that wraps in both axes (FFT). Works on HxW or HxWxC."""
    a = np.asarray(a, dtype=float)
    h, w = a.shape[:2]
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.rfftfreq(w)[None, :]
    k = np.exp(-2.0 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    f = np.fft.rfft2(a, axes=(0, 1))
    if a.ndim == 3:
        k = k[..., None]
    return np.fft.irfft2(f * k, s=(h, w), axes=(0, 1))


def _blur_reflect_small(l, sigma):
    """Blur of a small 2-D array with reflected borders (no wrap), by FFT on a padded copy."""
    pad = int(min(3 * sigma, min(l.shape) - 1))
    p = np.pad(l, pad, mode="reflect")
    return gauss_periodic(p, sigma)[pad:-pad, pad:-pad] if pad else gauss_periodic(l, sigma)


def flatten_light(rgb, sigma):
    """Divide by a strongly blurred copy of the luminance, so no corner is brighter than another."""
    rgb = np.asarray(rgb, dtype=float)
    lum = lum_of(rgb)
    f = max(1, int(sigma / 8))
    small = np.asarray(Image.fromarray(lum.astype(np.float32), "F").resize(
        (max(2, lum.shape[1] // f), max(2, lum.shape[0] // f)), Image.BILINEAR))
    blur = _blur_reflect_small(small, sigma / f)
    blur = np.asarray(Image.fromarray(blur.astype(np.float32), "F").resize(lum.shape[::-1], Image.BILINEAR),
                      dtype=float)
    ratio = np.clip(lum.mean() / np.maximum(blur, 1.0), 0.4, 2.5)
    return rgb * ratio[..., None]


def _dp(cost, start):
    length, w = cost.shape
    inf = 1e18
    acc = cost[0].astype(float).copy()
    if start is not None:
        keep = np.full(w, inf)
        keep[start] = 0.0
        acc = acc + keep
    choice = np.zeros((length, w), dtype=np.int8)
    idx = np.arange(w)
    for y in range(1, length):
        left = np.concatenate(([inf], acc[:-1]))
        right = np.concatenate((acc[1:], [inf]))
        stack = np.stack((left, acc, right))
        k = stack.argmin(axis=0)
        choice[y] = k - 1
        acc = cost[y] + stack[k, idx]
    if start is None:
        end = int(acc.argmin())
    else:
        lo, hi = max(0, start - 1), min(w - 1, start + 1)
        end = lo + int(acc[lo:hi + 1].argmin())
    total = float(acc[end])
    path = np.zeros(length, dtype=int)
    path[-1] = end
    for y in range(length - 1, 0, -1):
        path[y - 1] = path[y] + int(choice[y][path[y]])
    return path, total


def min_cost_path(cost, periodic=False):
    """Column per row of the cheapest top-to-bottom path (each step moves at most one column).
    periodic: the last row's column is within one of the first row's, so the path closes when wrapped."""
    cost = np.asarray(cost, dtype=float)
    if not periodic:
        return _dp(cost, None)[0]
    w = cost.shape[1]
    best, best_total = None, None
    for s in range(0, w, max(1, w // 32)):
        path, total = _dp(cost, s)
        if best is None or total < best_total:
            best, best_total = path, total
    return best


def _blur_channels(a, sigma):
    return np.stack([_blur_reflect_small(a[..., c], sigma) for c in range(a.shape[2])], axis=-1)


def _quilt_cols(img, w, feather, joint_bias, low_feather=0):
    """Make the left and right edges meet: cut w columns off the right, then blend them over the
    first w columns along a minimum-cost vertical path. Output is w columns narrower.
    Detail (joints, pits) switches over `feather` pixels. With low_feather > 0 the low frequencies (tone,
    stains) are cross-faded over that many pixels, so no tone step shows where the path crosses a block."""
    img = np.asarray(img, dtype=float)
    width = img.shape[1]
    t = img[:, : width - w]
    s = img[:, width - w:]
    over = t[:, :w]
    cost = np.abs(s - over).mean(axis=2) / 255.0 + joint_bias * (lum_of(s) + lum_of(over)) / 510.0
    margin = max(int(feather), int(low_feather)) // 2 + 2
    path = min_cost_path(cost[:, margin:w - margin], periodic=True) + margin
    cols = np.arange(w)[None, :]

    def weight(width_px):
        if width_px > 0:
            return np.clip((path[:, None] - cols) / float(width_px) + 0.5, 0.0, 1.0)[..., None]
        return (cols < path[:, None]).astype(float)[..., None]

    wh = weight(feather)
    out = t.copy()
    if low_feather > 0:
        wl = weight(low_feather)
        ls, lo = _blur_channels(s, LOW_SIGMA), _blur_channels(over, LOW_SIGMA)
        out[:, :w] = (wl * ls + (1 - wl) * lo) + (wh * (s - ls) + (1 - wh) * (over - lo))
    else:
        out[:, :w] = wh * s + (1.0 - wh) * over
    return out


def make_tileable(img, overlap, feather=FEATHER, joint_bias=JOINT_BIAS, low_feather=0):
    """Tile in U then V with whole blocks: a seam path through the darkest, most alike pixels, detail
    switched over a few pixels only along that path, tone cross-faded wider if low_feather is set.
    The result is (size - overlap) square."""
    out = _quilt_cols(img, overlap, feather, joint_bias, low_feather)
    out = _quilt_cols(out.transpose(1, 0, 2), overlap, feather, joint_bias, low_feather).transpose(1, 0, 2)
    return out


def resize_wrapped(img_u8, size):
    """Lanczos resize that sees the neighbouring tile at the borders (3 x 3 tiling, centre crop)."""
    im = Image.fromarray(np.asarray(img_u8, dtype=np.uint8), "RGB")
    big = Image.fromarray(np.tile(np.asarray(im), (3, 3, 1)), "RGB").resize((3 * size, 3 * size), Image.LANCZOS)
    return np.asarray(big)[size:2 * size, size:2 * size]


def drop_border_rows(img, rows):
    """Remove `rows` rows at the top and at the bottom and stretch the rest back to the full height.
    A one-block stone tile carries a joint along those two edges, and a trim strip must not show it.
    The columns do not move, so the tile still wraps in U."""
    a = np.asarray(img, dtype=float)
    if rows <= 0:
        return a
    h, w = a.shape[:2]
    core = np.clip(np.round(a[rows:h - rows]), 0, 255).astype(np.uint8)
    return np.asarray(Image.fromarray(core, "RGB").resize((w, h), Image.LANCZOS)).astype(float)


def match_colour(rgb, target, contrast=1.0, rounds=12):
    """Scale each channel so the mean lands on the target; the photo's own variation is kept."""
    a = np.asarray(rgb, dtype=float)
    if contrast != 1.0:
        m = a.reshape(-1, 3).mean(0)
        a = m + (a - m) * contrast
    t = np.array(target, dtype=float)
    gain = np.ones(3)
    for _ in range(rounds):
        cur = np.clip(a * gain, 0, 255).reshape(-1, 3).mean(0)
        gain *= t / np.maximum(cur, 1e-6)
    return np.clip(a * gain, 0, 255)


def height_from_luminance(lum, fine=1.0, coarse=12.0, unit=40.0, pit_gain=1.2, relief=1.0):
    """Height field from the mid and high frequencies of the luminance: joints and dark pits low, no
    relief from stains. The result wraps. Units are about one per 40 luminance steps."""
    lum = np.asarray(lum, dtype=float)
    band = gauss_periodic(lum, fine) - gauss_periodic(lum, coarse)
    band = band / unit * relief
    band = np.where(band < 0, band * 1.4, band)
    h = np.tanh(band / 3.0) * 3.0
    ratio = gauss_periodic(lum, 2.0) / max(float(np.median(lum)), 1.0)
    h = h - pit_gain * smoothstep(0.78, 0.55, ratio)
    return h


def normal_from_height(h, strength, wrap_u=True, wrap_v=True):
    """Tangent-space normal map, Y up (OpenGL), from a height field. uint8 HxWx3."""
    h = np.asarray(h, dtype=float)
    pu = np.pad(h, ((0, 0), (1, 1)), mode="wrap" if wrap_u else "edge")
    gx = (pu[:, 2:] - pu[:, :-2]) * 0.5
    pv = np.pad(h, ((1, 1), (0, 0)), mode="wrap" if wrap_v else "edge")
    g_row = (pv[2:, :] - pv[:-2, :]) * 0.5
    nx, ny = -gx * strength, g_row * strength
    nz = np.ones_like(nx)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / ln, ny / ln, nz / ln], axis=-1)
    return np.clip(np.round((n * 0.5 + 0.5) * 255.0), 0, 255).astype(np.uint8)


def fit_clipped(raw, mean, std, lo=0.0, hi=255.0, rounds=40):
    """Affine map of raw so that, after clipping to lo..hi, mean and std are the targets."""
    z = (raw - raw.mean()) / max(raw.std(), 1e-9)
    m, s = float(mean), float(std)
    for _ in range(rounds):
        out = np.clip(m + s * z, lo, hi)
        m += (mean - out.mean()) * 0.8
        s *= (std / max(out.std(), 1e-6)) ** 0.8
    return np.clip(m + s * z, lo, hi)


def make_spec(h, stain, params):
    """R 0. G (gloss) and B (strength) fall in joints, pits and dark stains, so those read duller."""
    hz = np.clip((h - h.mean()) / max(h.std(), 1e-9), -3.0, 3.0)
    sz = (stain - stain.mean()) / max(stain.std(), 1e-9)
    raw = 0.75 * hz + 0.25 * sz
    out = np.zeros(h.shape + (3,))
    out[..., 1] = fit_clipped(raw, *params["g"])
    out[..., 2] = fit_clipped(raw, *params["b"])
    return np.round(out).astype(np.uint8)


# ---------------------------------------------------------------- building sets

def load_params(ctl):
    with open(SPEC_STATS) as f:
        c = json.load(f)["control"][ctl]
    return {"g": (c["g"]["mean"], c["g"]["std"]), "b": (c["b"]["mean"], c["b"]["std"])}


def prepare_surface(src_path, overlap, tiled_out=None, low_feather=0):
    """Photo -> flattened, tiling, 1024 px float RGB (no colour match yet)."""
    rgb = np.asarray(Image.open(src_path).convert("RGB")).astype(float)
    flat = np.clip(flatten_light(rgb, FLAT_FRAC * rgb.shape[0]), 0, 255)
    tiled = make_tileable(flat, overlap, low_feather=low_feather)
    u8 = np.clip(np.round(tiled), 0, 255).astype(np.uint8)
    if tiled_out:
        Image.fromarray(u8, "RGB").save(tiled_out)
    return resize_wrapped(u8, SIZE).astype(float), rgb


def build_masonry(cfg, overlap, scratch=None):
    overlap = cfg.get("overlap", overlap)
    res, raw = prepare_surface(SRC + cfg["src"] + ".png", overlap, scratch, cfg.get("low_feather", 0))
    lum = lum_of(res)
    d = match_colour(res, hex_rgb(cfg["target"]), cfg["contrast"])
    h = height_from_luminance(lum, relief=cfg.get("relief", 1.0))
    n = normal_from_height(h, cfg["strength"])
    stain = gauss_periodic(lum, 3.0)
    s = make_spec(h, stain, load_params(cfg["ctl"]))
    return {"d": to_u8(d), "n": n, "s": s, "h": h}


def build_trim(overlap, seed=37, surface=None, target=None, border_rows=0):
    """The trim sheet. `surface` is a 1024 px stone image that already wraps in U (a Painter export);
    without it the surface is the photographic source of TRIM. `target` replaces the TRIM mean colour."""
    cfg = TRIM
    if surface:
        rgb = np.asarray(Image.open(surface).convert("RGB"))
        if rgb.shape[:2] != (SIZE, SIZE):
            raise ValueError("the trim surface must be %d px square: %s" % (SIZE, surface))
        res = drop_border_rows(rgb, border_rows)
    else:
        res, _ = prepare_surface(SRC + cfg["src"] + ".png", overlap)
    lum = lum_of(res)
    photo_h = height_from_luminance(lum)
    rows = strip_rows()
    carved = np.zeros((SIZE, SIZE))
    heff = np.zeros((SIZE, SIZE))
    shade = np.ones((SIZE, SIZE))
    photo_strength = {"S1": 5.0, "S2": 2.5, "S3": 2.5, "S4": 2.5, "S5": 2.5}
    for k, (name, (t, b)) in enumerate(rows.items()):
        h = flatten_margin(_strip_heights(name, b - t, seed + k * 3), SAFE)
        hn = np.clip((h - 0.2) / 0.75, 0.0, 1.0)  # fixed range: a flat strip must not stretch its noise
        ao = np.clip(((gauss_periodic_v(h, 5.0) - h) * 6.0 - 0.25) / 0.75, 0, 1)  # dead zone: tool noise is not a cavity
        carved[t:b] = h
        heff[t:b] = flatten_margin(h * STRIP_STRENGTH[name] + photo_h[t:b] * photo_strength[name], SAFE)
        shade[t:b] = flatten_margin(1.0 - 0.22 * (1 - hn) - 0.40 * ao, SAFE)
    d = flatten_margin_rows(res * shade[..., None], rows)
    d = match_colour(d, hex_rgb(target or cfg["target"]))
    n = np.zeros((SIZE, SIZE, 3), dtype=np.uint8)
    for name, (t, b) in rows.items():
        n[t:b] = normal_from_height(heff[t:b], 1.0, wrap_u=True, wrap_v=False)
    stain = gauss_periodic(lum, 3.0)
    s = make_spec(heff, stain, load_params(cfg["ctl"]))
    return {"d": to_u8(d), "n": n, "s": s, "h": heff, "carved": carved}


def gauss_periodic_v(h, sigma):
    """Blur that wraps in U and repeats the edge row in V (a strip does not wrap in V)."""
    r = int(3 * sigma) + 1
    p = np.pad(h, ((r, r), (0, 0)), mode="edge")
    return gauss_periodic(p, sigma)[r:-r]


def flatten_margin_rows(d, rows):
    out = np.array(d, copy=True)
    for t, b in rows.values():
        out[t:b] = flatten_margin(out[t:b], SAFE)
    return out


# ---------------------------------------------------------------- reports and previews

def seam_report(maps, axes):
    rep = {}
    for key, arr in maps.items():
        for ax in axes:
            sm, im = seam_mad(arr, ax), interior_mad(arr, ax)
            rep["%s axis%d" % (key, ax)] = {"edge_mad": round(sm, 3), "interior_mad": round(im, 3),
                                            "limit_1_5x": round(1.5 * im, 3), "pass": bool(sm <= 1.5 * im)}
    return rep


def save_scaled(path, arr, width):
    im = Image.fromarray(arr, "RGB")
    if im.width > width:
        im = im.resize((width, int(im.height * width / im.width)), Image.LANCZOS)
    im.save(path)


def region_3m(d8, px_per_m, tile_m=SIZE / PX_PER_M, extent_m=3.0):
    """extent_m of wall made of tiles of tile_m, drawn at px_per_m (square crop)."""
    tile_px = int(round(tile_m * px_per_m))
    t = np.asarray(Image.fromarray(d8, "RGB").resize((tile_px, tile_px), Image.LANCZOS))
    reps = int(np.ceil(extent_m / tile_m)) + 1
    big = np.tile(t, (reps, reps, 1))
    n = int(round(extent_m * px_per_m))
    return big[:n, :n]


def channel_stats(a):
    a = np.asarray(a).astype(float)
    return {c: {"mean": round(float(a[..., i].mean()), 2), "std": round(float(a[..., i].std()), 2)}
            for i, c in enumerate("rgb")}


def write_set(out, prev, name, built, axes):
    save_rgb(out + "/%s_d.png" % name, built["d"])
    save_rgb(out + "/%s_n.png" % name, built["n"])
    save_rgb(out + "/%s_n_flipgreen.png" % name, flip_green(built["n"]))
    save_rgb(out + "/%s_s.png" % name, built["s"])
    reps = (3, 3, 1) if len(axes) == 2 else (1, 3, 1)
    tag = "3x3" if len(axes) == 2 else "3x1"
    save_scaled(prev + "/%s_tiled%s.png" % (name, tag), np.tile(built["d"], reps), 1536)
    lit = lit_preview(built["d"], built["n"])
    save_scaled(prev + "/%s_lit%s.png" % (name, tag), np.tile(lit, reps), 1536)
    save_rgb(prev + "/%s_lit1x1.png" % name, lit)
    return seam_report({name + "_d": built["d"], name + "_n": built["n"], name + "_s": built["s"]}, axes)


def compare_sheet(prev, name, d8, control_file):
    """Ours beside the Gondor control, 3 m x 3 m of wall each, at 683 px per metre (the control's
    2048 px over 3 m). The sheet is written at full size and a half-size copy."""
    ctl = np.asarray(Image.open(GONDOR + control_file).convert("RGB"))
    ours = region_3m(d8, ctl.shape[0] / 3.0)
    gap = np.full((ours.shape[0], 24, 3), 255, np.uint8)
    sheet = np.concatenate([ours, gap, ctl[:ours.shape[0], :ours.shape[1]]], axis=1)
    Image.fromarray(sheet, "RGB").save(prev + "/%s_vs_gondor_full.png" % name)
    save_scaled(prev + "/%s_vs_gondor.png" % name, sheet, 2000)
    return {"ours": channel_stats(ours), "control": channel_stats(ctl)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--overlap", type=int, default=OVERLAP)
    ap.add_argument("--only", default=None, help="build one set by name")
    ap.add_argument("--trim-surface", default=None, help="a 1024 px stone image that wraps in U, for the trim sheet")
    ap.add_argument("--trim-target", default=None, help="mean colour of the trim sheet, as #rrggbb")
    ap.add_argument("--trim-border-rows", type=int, default=0,
                    help="rows to remove at the top and bottom of --trim-surface (its border joint)")
    args = ap.parse_args()
    out, prev = args.out, args.out + "/preview"
    os.makedirs(prev, exist_ok=True)
    seams, built, cmp_stats, params = {}, {}, {}, {}
    for name, cfg in SETS.items():
        if args.only and args.only != name:
            continue
        b = build_masonry(cfg, args.overlap, prev + "/%s_tiled_source.png" % name)
        built[name] = b
        params[name] = dict(cfg)
        seams.update(write_set(out, prev, name, b, (0, 1)))
        print(name, "built", flush=True)
    if not args.only or args.only == "t_rivendell_wall_trim_a":
        b = build_trim(args.overlap, surface=args.trim_surface, target=args.trim_target,
                       border_rows=args.trim_border_rows)
        built["t_rivendell_wall_trim_a"] = b
        params["t_rivendell_wall_trim_a"] = {"surface": args.trim_surface or TRIM["src"],
                                             "target": args.trim_target or TRIM["target"],
                                             "border_rows": args.trim_border_rows}
        seams.update(write_set(out, prev, "t_rivendell_wall_trim_a", b, (1,)))
        print("trim built", flush=True)
    for name, control in (("t_rivendell_wall_stone_a", "t_gondor_bricks_large_a_dirty_d2.png"),
                          ("t_rivendell_wall_stone_b", "t_gondor_bricks_large_a_dirty_d2.png"),
                          ("t_rivendell_wall_rubble_a", "t_gondor_rubble_stone_a_d2.png"),
                          ("t_rivendell_wall_rubble_b", "t_gondor_rubble_stone_a_d2.png")):
        if name in built:
            cmp_stats[name] = compare_sheet(prev, name, built[name]["d"], control)
    with open(out + "/seam_report.json", "w") as f:
        json.dump(seams, f, indent=1)
    with open(out + "/build_params.json", "w") as f:
        json.dump(params, f, indent=1)
    with open(out + "/compare_stats.json", "w") as f:
        json.dump(cmp_stats, f, indent=1)
    print("done")


if __name__ == "__main__":
    main()
