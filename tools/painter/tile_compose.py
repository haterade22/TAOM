"""Compose a tiling stone set on the host, without Painter, from Painter exports and layout maps.

Input: a base export folder (rivendell_tile_baseColor / height / normal / roughness / ambientOcclusion of the
dressed stone with no joints), a layout folder written by block_layout.py (version 2), and a spec.
Output: <set>_d.png, <set>_n.png (green DOWN), <set>_s.png (R 0, G gloss, B occlusion) and <set>_h.png.

Every step is one that a Painter layer stack can do, so the winner is rebuilt in Painter from the same maps
and numbers. PAINTER_LAYERS names the layer for each step. Blends use the formulas of Painter (Overlay,
Screen, Multiply, a Normal blend with a mask) on sRGB values in 0..1; the normal map is the base normal with
the slope of the layout height added (Painter: the Height channel feeds the normal).

Run:  python tools/painter/tile_compose.py build <candidates.json> [name ...] [--steps layout,wall,...]
      python tools/painter/tile_compose.py stats <diffuse.png> --ppm 461
Pure steps are tested in tools/tests/test_tile_compose.py.
"""
import argparse
import copy
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "oneoff"))
import block_layout as bl  # noqa: E402

WORK = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell"
GONDOR = ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/AssetSources/Scenes/Gondor/"
          "textures/t_gondor_bricks_large_a_dirty_d2.png")
GONDOR_PPM = 2048 / 3.0
WALL, PLINTH, TRIM = "t_rivendell_wall_stone_a", "t_rivendell_wall_rubble_a", "t_rivendell_wall_trim_a"

PAINTER_LAYERS = [
    ("base", "The dressed stone: a fill layer with the material, all five channels (the export folder)."),
    ("flatten", "A Filter layer 'High Pass' on the base colour (radius = flatten.sigma_mm in px, intensity = amount)."),
    ("grain", "A Filter layer 'Sharpen' (radius = grain.sigma_mm in px, intensity = gain) on the base colour."),
    ("colour", "A Filter layer 'Color Balance / Levels' per channel on the base colour: the channel means "
               "move to d.target; 'HSL Perceptive' saturation is lowered until the mean saturation is d.sat_max."),
    ("tone", "A fill layer, colour from layout_id.png, blend Overlay, opacity d.tone."),
    ("cloud", "A fill layer, colour from layout_cloud.png, blend Overlay, opacity d.cloud."),
    ("stain", "A fill layer, colour d.stain.color, blend Normal, mask layout_stain.png, opacity d.stain.opacity."),
    ("joint", "A fill layer, colour d.joint.color, blend Multiply, mask layout_joint.png, opacity 1: darkened stone."),
    ("ao", "A fill layer, colour from layout_ao.png, blend Multiply, opacity d.ao."),
    ("edge light", "A fill layer, colour d.edge_light.color, blend Screen, mask layout_edgelight.png, "
                   "opacity d.edge_light.opacity."),
    ("chip", "A fill layer, colour d.chip.color, blend Screen, mask layout_chip.png, opacity d.chip.opacity."),
    ("mean trim", "A last Levels layer: a small constant offset per channel so that the tile mean is d.target."),
    ("normal", "A fill layer with Height from layout_height.png (Height channel, no colour), Normal blend; "
               "the normal is built from the height (height scale = n.relief x radius px); the stone's own "
               "normal stays under it. Export Normal_DirectX."),
    ("gloss", "Roughness: the base roughness, a Levels layer to the gloss mean (G = 255 minus roughness), "
              "a fill layer with mask layout_joint.png and layout_chip.png that adds s.joint_rough / "
              "s.chip_rough to the roughness (Add)."),
    ("occlusion", "Ambient occlusion: the base AO, a fill layer from layout_ao.png, blend Multiply, opacity s.ao."),
    ("height", "Height: the base height scaled by h.base_amount plus layout_height.png (Add), exported 8 bit."),
]

DEFAULTS = {
    "d": {"flatten": None, "grain": {"sigma_mm": 4.5, "gain": 0.0}, "tone": 0.12, "cloud": 0.2,
          "stain": {"color": [0.55, 0.5, 0.44], "opacity": 0.08}, "joint": {"color": [0.8, 0.78, 0.75]},
          "ao": 0.3, "edge_light": {"color": [1.0, 0.97, 0.9], "opacity": 0.2},
          "chip": {"color": [1.0, 0.97, 0.9], "opacity": 0.3}, "target": [195, 187, 178], "sat_max": 0.10},
    "n": {"relief": 0.6},
    "s": {"gloss_mean": 100.0, "gloss_std_max": 14.0, "joint_rough": 35.0, "chip_rough": 20.0, "ao": 0.6},
    "h": {"base_amount": 0.12},
}


def merge_spec(spec):
    """The spec with the defaults filled in (a deep merge; a None value switches a step off)."""
    def merge(a, b):
        out = copy.deepcopy(a)
        for k, v in (b or {}).items():
            out[k] = merge(out[k], v) if isinstance(v, dict) and isinstance(out.get(k), dict) else copy.deepcopy(v)
        return out
    return merge(DEFAULTS, spec or {})


# ----------------------------------------------------------------------------------------- colour steps

def luminance(rgb):
    rgb = np.asarray(rgb, dtype=float)
    return 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]


def saturation(rgb):
    """HSV saturation per pixel, (max - min) / max."""
    rgb = np.asarray(rgb, dtype=float)
    mx, mn = rgb.max(axis=-1), rgb.min(axis=-1)
    return np.where(mx > 0, (mx - mn) / np.where(mx > 0, mx, 1.0), 0.0)


def gauss_blur(a, sigma, periodic=True):
    """A Gaussian blur of a 2-D or 3-D array. `periodic` wraps; otherwise the edge is mirrored."""
    a = np.asarray(a, dtype=float)
    if a.ndim == 3:
        return np.stack([gauss_blur(a[..., i], sigma, periodic) for i in range(a.shape[2])], axis=-1)
    if periodic:
        return bl._gauss_periodic(a, sigma)
    r = int(np.ceil(4 * sigma))
    return bl._gauss_periodic(np.pad(a, r, mode="reflect"), sigma)[r:-r, r:-r]


def gauss_aniso(a, sigma_x, sigma_y):
    """A periodic blur with different widths along x and y (tool marks)."""
    fy = np.fft.fftfreq(a.shape[0])[:, None]
    fx = np.fft.fftfreq(a.shape[1])[None, :]
    k = np.exp(-2.0 * np.pi ** 2 * ((sigma_x * fx) ** 2 + (sigma_y * fy) ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * k))


def _mask3(m):
    return np.asarray(m, dtype=float)[..., None]


def overlay(base, blend, opacity):
    """Painter's Overlay at an opacity. `base` (H, W, 3) and `blend` (H, W) are in 0..1."""
    g = _mask3(blend)
    full = np.where(base < 0.5, 2.0 * base * g, 1.0 - 2.0 * (1.0 - base) * (1.0 - g))
    return base + opacity * (full - base)


def screen_mask(base, colour, mask):
    """Screen of a colour, with the mask as opacity: base + mask * colour * (1 - base)."""
    return base + _mask3(mask) * np.asarray(colour, dtype=float) * (1.0 - base)


def multiply_mask(base, colour, mask):
    """Multiply by a colour, with the mask as opacity."""
    return base * (1.0 + _mask3(mask) * (np.asarray(colour, dtype=float) - 1.0))


def tint_mask(base, colour, mask):
    """A Normal-blend fill of a colour with a mask."""
    return base + _mask3(mask) * (np.asarray(colour, dtype=float) - base)


def unsharp(img, sigma, gain):
    return img + gain * (img - gauss_blur(img, sigma))


def highpass(img, sigma, amount):
    """Remove the tone that is wider than `sigma` px (keeps the mean)."""
    mean = img.reshape(-1, img.shape[-1]).mean(0)
    return img - amount * (gauss_blur(img, sigma) - mean)


def retarget_colour(img, target, sat_max=None, rounds=8):
    """Move the channel means to `target` (0..255). With `sat_max` the chroma spread is squeezed around
    the luminance until the mean saturation is at most that, and the means are put back."""
    out = np.asarray(img, dtype=float).copy()
    t = np.asarray(target, dtype=float)
    for _ in range(rounds):
        if sat_max is not None:
            s = float(saturation(out).mean())
            if s > sat_max:
                lum = luminance(out)[..., None]
                out = lum + (out - lum) * (sat_max / s)
        out = np.clip(out + (t - out.reshape(-1, 3).mean(0)), 0, 255)
    return out


def scale_to_luminance(rgb, lum):
    """The colour scaled so that its luminance is `lum` (the hue family stays)."""
    k = lum / float(luminance(np.asarray(rgb, dtype=float)))
    return tuple(float(v) * k for v in rgb)


def shift_to_mean(a, mean, rounds=8):
    a = np.asarray(a, dtype=float)
    for _ in range(rounds):
        a = np.clip(a + (mean - np.clip(a, 0, 255).mean()), 0, 255)
    return a


# --------------------------------------------------------------------------------------------- normals

def _slopes(h):
    """Periodic central differences of a height map: d/dx and d/drow (rows run downward)."""
    return ((np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) / 2.0,
            (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) / 2.0)


def _encode(sx, sy):
    """Slopes (z = 1) to an 8 bit normal map. Green is DOWN: G = -dh/drow."""
    inv = 1.0 / np.sqrt(sx ** 2 + sy ** 2 + 1.0)
    v = np.stack([sx * inv, sy * inv, inv], axis=-1)
    return np.clip(np.rint((v * 0.5 + 0.5) * 255.0), 0, 255).astype(np.uint8)


def _base_slopes(n):
    v = np.asarray(n, dtype=float)[..., :3] / 127.5 - 1.0
    nz = np.sqrt(np.clip(1.0 - v[..., 0] ** 2 - v[..., 1] ** 2, 0.05, 1.0))
    return v[..., 0] / nz, v[..., 1] / nz


def mean_tilt(n):
    """Mean of sqrt(nx^2 + ny^2) of the decoded normal map: 0 is flat."""
    v = np.asarray(n, dtype=float)[..., :2] / 127.5 - 1.0
    return float(np.sqrt(v[..., 0] ** 2 + v[..., 1] ** 2).mean())


def tilt_map(n):
    """Per pixel sqrt(nx^2 + ny^2) of the decoded normal map: 0 is flat."""
    v = np.asarray(n, dtype=float)[..., :2] / 127.5 - 1.0
    return np.sqrt(v[..., 0] ** 2 + v[..., 1] ** 2)


def _rows(blocks):
    return sorted({(b["y0"], b["y1"]) for b in blocks})


def inner_mask(blocks, layout_size, out_size, margin_px):
    """A bool map at `out_size` px: the pixels of the blocks that are `margin_px` (output px) or more away from
    every block edge. The blocks are in layout px and may cross the tile edge."""
    k = out_size / float(layout_size)
    mask = np.zeros((out_size, out_size), dtype=bool)
    m = int(np.ceil(margin_px))
    for b in blocks:
        y0, y1 = int(round(b["y0"] * k)) + m, int(round(b["y1"] * k)) - m
        x0, x1 = int(round(b["x0"] * k)) + m, int(round(b["x1"] * k)) - m
        if y1 <= y0 or x1 <= x0:
            continue
        cols = np.arange(x0, x1) % out_size
        mask[y0:y1, cols] = True
    return mask


def face_tilt(n, blocks, layout_size, margin_px=12):
    """The mean tilt of the normal map inside the blocks, `margin_px` (output px) away from every edge: the
    roughness of the faces, with no joint or bevel."""
    n = np.asarray(n)
    m = inner_mask(blocks, layout_size, n.shape[0], margin_px)
    return float(tilt_map(n)[m].mean())


def face_tilt_median(n, blocks, layout_size, margin_px=12):
    """The median tilt of the normal map inside the blocks, `margin_px` (output px) away from every edge. The
    median is what the control judge reads: a few steep pixels do not move it."""
    n = np.asarray(n)
    m = inner_mask(blocks, layout_size, n.shape[0], margin_px)
    return float(np.median(tilt_map(n)[m]))


def block_means(lum, blocks, layout_size, margin_px=12):
    """The mean luminance inside each block, `margin_px` (output px) away from its edges, in the order of
    `blocks`. A block may cross the tile edge."""
    lum = np.asarray(lum, dtype=float)
    size = lum.shape[0]
    k = size / float(layout_size)
    m = int(np.ceil(margin_px))
    out = []
    for b in blocks:
        y0, y1 = int(round(b["y0"] * k)) + m, int(round(b["y1"] * k)) - m
        x0, x1 = int(round(b["x0"] * k)) + m, int(round(b["x1"] * k)) - m
        cols = np.arange(x0, x1) % size
        out.append(float(lum[y0:y1][:, cols].mean()))
    return out


def cloud_contrast(lum, blocks, layout_size, sigma=24.0, margin_px=48):
    """The soft tone inside the blocks, in levels: the luminance blurred by `sigma` px, minus the mean of its
    block, as one std over all block interiors (`margin_px` from every edge, so that no joint reaches in).
    Block tone and joints do not count; the clouds and the large undulation of the colour do."""
    soft = gauss_blur(np.asarray(lum, dtype=float), sigma)
    size = soft.shape[0]
    k = size / float(layout_size)
    m = int(np.ceil(margin_px))
    parts = []
    for b in blocks:
        y0, y1 = int(round(b["y0"] * k)) + m, int(round(b["y1"] * k)) - m
        x0, x1 = int(round(b["x0"] * k)) + m, int(round(b["x1"] * k)) - m
        if y1 <= y0 or x1 <= x0:
            continue
        patch = soft[y0:y1][:, np.arange(x0, x1) % size]
        parts.append((patch - patch.mean()).ravel())
    return float(np.concatenate(parts).std()) if parts else 0.0


def course_means(lum, blocks, layout_size):
    """The mean luminance of each course band (every column of the rows of one course), top course first."""
    lum = np.asarray(lum, dtype=float)
    k = lum.shape[0] / float(layout_size)
    return [float(lum[int(round(y0 * k)):int(round(y1 * k))].mean()) for y0, y1 in _rows(blocks)]


def band_means(lum, bands=4):
    """The mean luminance of `bands` equal vertical bands (a band is a range of columns), left first."""
    lum = np.asarray(lum, dtype=float)
    w = lum.shape[1]
    return [float(lum[:, i * w // bands:(i + 1) * w // bands].mean()) for i in range(bands)]


def spread(values):
    return float(max(values) - min(values))


def _crossing(line, level):
    """The first position (sub-pixel) where `line` rises through `level`, or None."""
    above = np.nonzero(line >= level)[0]
    if len(above) == 0:
        return None
    i = int(above[0])
    if i == 0:
        return 0.0
    lo, hi = float(line[i - 1]), float(line[i])
    return i - 1 + (level - lo) / (hi - lo) if hi > lo else float(i)


def edge_straightness(height, blocks, skip=(), tol=1.0, level=0.5, corner=12, reach=40):
    """How straight the arris of each block is on the layout height map. For each of the four edges of a block
    the edge line is where the height first rises through `level` (sub-pixel), walking in from the joint, row by
    row (or column by column); `corner` px at each end are left out. An edge's deviation is its largest distance
    from the median position. Blocks in `skip` (for example the chipped ones) are left out.
    Returns edges, bad_edges (deviation over `tol` px), max_dev_px and the worst edges."""
    h = np.asarray(height, dtype=float)
    size = h.shape[0]
    worst, devs = [], []
    for b in blocks:
        if b["id"] in skip:
            continue
        w, hh = b["x1"] - b["x0"], b["y1"] - b["y0"]
        reach_x, reach_y = min(reach, w // 2), min(reach, hh // 2)
        ys = [y % size for y in range(b["y0"], b["y1"])]
        xs = [x % size for x in range(b["x0"], b["x1"])]
        edges = {"left": [h[y, xs[:reach_x]] for y in ys[corner:hh - corner]],
                 "right": [h[y, xs[::-1][:reach_x]] for y in ys[corner:hh - corner]],
                 "top": [h[ys[:reach_y], x] for x in xs[corner:w - corner]],
                 "bottom": [h[ys[::-1][:reach_y], x] for x in xs[corner:w - corner]]}
        for name, lines in edges.items():
            pos = [_crossing(line, level) for line in lines]
            pos = np.array([p for p in pos if p is not None], dtype=float)
            dev = float(np.abs(pos - np.median(pos)).max()) if len(pos) else 0.0
            devs.append(dev)
            worst.append((dev, b["id"], name))
    worst.sort(reverse=True)
    return {"edges": len(devs), "bad_edges": int(sum(d > tol for d in devs)),
            "max_dev_px": round(max(devs), 3) if devs else 0.0,
            "worst": [{"block": i, "edge": e, "dev_px": round(d, 2)} for d, i, e in worst[:4]]}


def combine_normal(base_n, height_px):
    """The base normal with the slope of `height_px` added (green down: n = (-dh/dx, -dh/drow, 1))."""
    sx, sy = _base_slopes(base_n)
    dx, dy = _slopes(np.asarray(height_px, dtype=float))
    return _encode(sx - dx, sy - dy)


def flip_green(n):
    out = np.array(n, copy=True)
    out[..., 1] = 255 - out[..., 1]
    return out


def _fit_tilt(sx, sy, target):
    """A factor f so that the slopes f*sx, f*sy give a mean tilt `target` (bisection)."""
    def tilt(f):
        inv = 1.0 / np.sqrt((f * sx) ** 2 + (f * sy) ** 2 + 1.0)
        return float((np.sqrt((f * sx) ** 2 + (f * sy) ** 2) * inv).mean())
    lo, hi = 0.0, 1.0
    while tilt(hi) < target and hi < 1e6:
        hi *= 2.0
    for _ in range(40):
        mid = (lo + hi) / 2.0
        lo, hi = (mid, hi) if tilt(mid) < target else (lo, mid)
    return (lo + hi) / 2.0


def scale_normal_to_tilt(n, target):
    """Weaken or strengthen a normal map until its mean tilt is `target`."""
    sx, sy = _base_slopes(n)
    f = _fit_tilt(sx, sy, target)
    return _encode(f * sx, f * sy)


def detail_normal(src_height, size=512, tilt=0.065, tool_marks=0.4, seed=1, grain_sigma=(0.7, 5.0)):
    """A tiling detail normal map (green down): fine stone grain from a periodic height source plus faint
    horizontal tool marks, scaled to the mean tilt `tilt`."""
    src = np.asarray(src_height, dtype=float)
    f = src.shape[0] // size
    if f > 1:
        src = src[:size * f, :size * f].reshape(size, f, size, f).mean(axis=(1, 3))
    src = (src - src.mean()) / (src.std() or 1.0)
    grain = gauss_blur(src, grain_sigma[0]) - gauss_blur(src, grain_sigma[1])
    grain /= grain.std() or 1.0
    noise = np.random.default_rng(seed).normal(size=(size, size))
    marks = gauss_aniso(noise, 14.0, 0.7)
    marks /= marks.std() or 1.0
    h = grain + tool_marks * marks
    dx, dy = _slopes(h)
    k = _fit_tilt(-dx, -dy, tilt)
    return _encode(-k * dx, -k * dy)


# -------------------------------------------------------------------------------------------- the maps

def spec_map(rough, ao_base, ao_layout, joint, chip, s):
    """The engine _s: R 0, G gloss (255 minus roughness, at the chosen mean, joints and chips rougher),
    B the base occlusion times the layout occlusion at strength s.ao."""
    g = 255.0 - np.asarray(rough, dtype=float)
    if g.std() > s["gloss_std_max"]:
        g = g.mean() + (g - g.mean()) * (s["gloss_std_max"] / g.std())
    g = g - joint * s["joint_rough"] - chip * s["chip_rough"]
    g = shift_to_mean(g, s["gloss_mean"])
    b = np.asarray(ao_base, dtype=float) * (1.0 - s["ao"] * (1.0 - ao_layout))
    out = np.zeros(g.shape + (3,))
    out[..., 1] = g
    out[..., 2] = b
    return np.clip(np.rint(out), 0, 255).astype(np.uint8)


def height_map(base_h, layout_h, base_amount):
    """An 8 bit height map: the layout height, with the stone's own relief at `base_amount`."""
    b = np.asarray(base_h, dtype=float) / 255.0
    b = np.clip(0.5 + (b - b.mean()) * 2.0, 0.0, 1.0)
    h = (1.0 - base_amount) * layout_h + base_amount * b
    return np.clip(np.rint(h * 255.0), 0, 255).astype(np.uint8)


def compose_diffuse(base_rgb, maps, d, ppm, with_layout=True):
    """The diffuse as floats in 0..255. `with_layout` False gives the dressed stone only (the trim surface)."""
    x = np.asarray(base_rgb, dtype=float) / 255.0
    if d.get("flatten"):
        x = highpass(x, d["flatten"]["sigma_mm"] * ppm / 1000.0, d["flatten"]["amount"])
    if d["grain"]["gain"]:
        x = unsharp(x, max(0.8, d["grain"]["sigma_mm"] * ppm / 1000.0), d["grain"]["gain"])
    x = np.clip(retarget_colour(np.clip(x, 0, 1) * 255.0, d["target"], d["sat_max"]) / 255.0, 0, 1)
    if with_layout:
        x = overlay(x, maps["id"], d["tone"])
        x = overlay(x, maps["cloud"], d["cloud"])
        if d.get("stain"):
            x = tint_mask(x, d["stain"]["color"], maps["stain"] * d["stain"]["opacity"])
        x = multiply_mask(x, d["joint"]["color"], maps["joint"])
        x = x * _mask3(1.0 - d["ao"] * (1.0 - maps["ao"]))
        x = screen_mask(x, d["edge_light"]["color"], maps["edgelight"] * d["edge_light"]["opacity"])
        x = screen_mask(x, d["chip"]["color"], maps["chip"] * d["chip"]["opacity"])
    return retarget_colour(np.clip(x, 0, 1) * 255.0, d["target"], d["sat_max"], rounds=3)


def compose(base, maps, spec, info):
    """The four maps of a wall set (uint8). `base`: arrays color, height, normal, rough, ao (uint8).
    `maps`: the layout maps in 0..1. `info`: ppm and radius_px of the layout."""
    ppm = float(info["ppm"])
    radius = float(info.get("radius_px", info.get("radius")))
    d = compose_diffuse(base["color"], maps, spec["d"], ppm)
    relief = spec["n"]["relief"] * radius
    n = combine_normal(base["normal"], maps["height"] * relief)
    s = spec_map(base["rough"], base["ao"], maps["ao"], maps["joint"], maps["chip"], spec["s"])
    h = height_map(base["height"], maps["height"], spec["h"]["base_amount"])
    return {"d": np.clip(np.rint(d), 0, 255).astype(np.uint8), "n": n, "s": s, "h": h}


def compose_plinth(base, target, sat_max, normal_form, tilt, gloss_mean, gloss_std_max=14.0, ao_strength=0.35):
    """A plinth set from a Painter export: moved to `target` colour, the normal in green-down form and weakened
    to a mean tilt, gloss at its own mean, B the occlusion."""
    ao = np.asarray(base["ao"], dtype=float) / 255.0
    x = np.asarray(base["color"], dtype=float) * (1.0 - ao_strength * (1.0 - ao))[..., None]
    d = retarget_colour(x, target, sat_max)
    n = base["normal"] if normal_form == "down" else flip_green(base["normal"])
    if tilt:
        n = scale_normal_to_tilt(n, tilt)
    zero = np.zeros(ao.shape)
    s = spec_map(base["rough"], base["ao"], np.ones(ao.shape), zero, zero,
                 {"gloss_mean": gloss_mean, "gloss_std_max": gloss_std_max, "joint_rough": 0, "chip_rough": 0,
                  "ao": 0})
    h = np.asarray(base["height"], dtype=np.uint8)
    return {"d": np.clip(np.rint(d), 0, 255).astype(np.uint8), "n": n, "s": s, "h": h}


# ------------------------------------------------------------------------------------------ the numbers

def fine_grain(lum, ppm):
    """The grain of the stone: a robust std (1.4826 x the median absolute deviation) of the luminance minus
    its blur of 4.5 mm. A few joint lines and chips do not move it much."""
    lum = np.asarray(lum, dtype=float)
    hp = lum - gauss_blur(lum, max(1.0, 0.0045 * ppm), periodic=False)
    return float(1.4826 * np.median(np.abs(hp - np.median(hp))))


def dark_tail(lum):
    """The dark tail of a tile: the median luminance minus the 5th percentile (joints and their shade)."""
    lum = np.asarray(lum, dtype=float)
    p5, p50 = np.percentile(lum, [5, 50])
    return float(p50 - p5)


def luminance709(rgb):
    """The luminance with the weights of the control judge's script (Rec. 709)."""
    rgb = np.asarray(rgb, dtype=float)
    return 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]


def lowfreq_std(lum, sigma=24.0, periodic=False):
    """The low-frequency spread of the luminance: the std of the image blurred by `sigma` px (24 px is 52 mm at
    461 px per metre). The default is the control judge's measure exactly: PIL's GaussianBlur on the 8 bit
    luminance, edge extended (feed it luminance709). `periodic` blurs with a wrap instead, which is the true
    figure for a tile that repeats."""
    a = np.asarray(lum, dtype=float)
    if periodic:
        return float(gauss_blur(a, sigma, True).std())
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    return float(np.asarray(img.filter(ImageFilter.GaussianBlur(sigma)), dtype=np.float64).std())


def joint_profile(lum, ppm, periodic=True):
    """The horizontal joints from the row profile (the mean of each row): the dip under a local baseline
    (the median over 80 mm). Returns count, contrast (levels of luminance) and width (px, full width at
    half depth), each as the median over the joints found."""
    prof = np.asarray(lum, dtype=float).mean(axis=1)
    n = len(prof)
    w = int(0.08 * ppm) | 1
    pad = np.pad(prof, w // 2, mode="wrap" if periodic else "reflect")
    base = np.median(np.lib.stride_tricks.sliding_window_view(pad, w), axis=1)
    dip = base - prof
    thr, gap = 6.0, max(3, int(0.15 * ppm))
    cand = [i for i in range(n) if dip[i] >= thr and (periodic or 3 <= i < n - 3)
            and dip[i] >= dip[(i - 1) % n] and dip[i] > dip[(i + 1) % n]]
    peaks = []
    for i in sorted(cand, key=lambda j: -dip[j]):
        if all(min(abs(i - j), n - abs(i - j) if periodic else abs(i - j)) >= gap for j in peaks):
            peaks.append(i)
    if not peaks:
        return {"count": 0, "contrast": 0.0, "width_px": 0.0}
    widths = []
    for i in peaks:
        half, run = dip[i] / 2.0, 1
        for step in (1, -1):
            k = i + step
            while 0 <= (k if not periodic else k % n) < n and dip[k % n] > half and run < n // 2:
                run += 1
                k += step
        widths.append(run)
    return {"count": len(peaks), "contrast": float(np.median([dip[i] for i in peaks])),
            "width_px": float(np.median(widths))}


def tile_stats(rgb, ppm, periodic=True):
    """The numbers that judge a diffuse: mean colour, mean luminance, mean saturation, p5 to p95 of the
    luminance, fine grain, and the joints (contrast in levels, width in px and mm)."""
    a = np.asarray(rgb, dtype=float)
    lum = luminance(a)
    p5, p95 = np.percentile(lum, [5, 95])
    jp = joint_profile(lum, ppm, periodic)
    return {"mean": [round(float(v), 1) for v in a.reshape(-1, 3).mean(0)], "lum_mean": round(float(lum.mean()), 1),
            "saturation": round(float(saturation(a).mean()), 3), "lum_p5_p95": round(float(p95 - p5), 1),
            "fine_grain": round(fine_grain(lum, ppm), 2), "dark_tail": round(dark_tail(lum), 1),
            "lowfreq_std": round(lowfreq_std(luminance709(a), 24.0), 2),
            "lowfreq_std_wrapped": round(lowfreq_std(luminance709(a), 24.0, True), 2), "joint_count": jp["count"],
            "joint_contrast": round(jp["contrast"], 1), "joint_width_px": round(jp["width_px"], 1),
            "joint_width_mm": round(jp["width_px"] * 1000.0 / ppm, 1), "ppm": round(float(ppm), 1)}


report = tile_stats


def resample_to_ppm(rgb, from_ppm, to_ppm):
    h, w = rgb.shape[:2]
    size = (int(round(w * to_ppm / from_ppm)), int(round(h * to_ppm / from_ppm)))
    return np.asarray(Image.fromarray(np.asarray(rgb, dtype=np.uint8)).resize(size, Image.LANCZOS))


_CONTROL = {}


def control_stats(ppm):
    """The same numbers for the Gondor control stone resampled to `ppm` pixels per metre."""
    key = round(float(ppm), 1)
    if key not in _CONTROL:
        ctl = np.asarray(Image.open(GONDOR).convert("RGB"))
        _CONTROL[key] = tile_stats(resample_to_ppm(ctl, GONDOR_PPM, ppm), ppm, periodic=False)
    return _CONTROL[key]


# ------------------------------------------------------------------------------------------------ files

def load_base(folder):
    def f(m):
        return os.path.join(folder, "rivendell_tile_%s.png" % m)
    return {"color": np.asarray(Image.open(f("baseColor")).convert("RGB")),
            "height": np.asarray(Image.open(f("height")).convert("L")),
            "normal": np.asarray(Image.open(f("normal")).convert("RGB")),
            "rough": np.asarray(Image.open(f("roughness")).convert("L")),
            "ao": np.asarray(Image.open(f("ambientOcclusion")).convert("L"))}


def load_layout(folder):
    """The layout maps as floats in 0..1 exactly as a Painter layer reads the PNG files, and layout.json info."""
    meta = json.load(open(os.path.join(folder, "layout.json")))
    maps = {}
    for key, path in meta["files"].items():
        a = np.asarray(Image.open(os.path.join(folder, os.path.basename(path))))
        maps[key] = a.astype(float) / (65535.0 if a.dtype == np.uint16 else 255.0)
    return maps, meta.get("info") or {}


def write_set(folder, name, out):
    os.makedirs(folder, exist_ok=True)
    files = {}
    for key in ("d", "n", "s", "h"):
        files[key] = os.path.join(folder, "%s_%s.png" % (name, key))
        Image.fromarray(out[key]).save(files[key])
    return files


def flat_sheet(d_png, ppm, out_png, extent_m=3.0):
    """Our tile and the Gondor control side by side, `extent_m` square each, at the control's density."""
    ctl = np.asarray(Image.open(GONDOR).convert("RGB"))
    n = int(round(extent_m * GONDOR_PPM))
    ours = np.asarray(Image.open(d_png).convert("RGB"))
    tile_px = int(round(ours.shape[0] / float(ppm) * GONDOR_PPM))
    t = np.asarray(Image.fromarray(ours).resize((tile_px, tile_px), Image.LANCZOS))
    big = np.tile(t, (int(np.ceil(n / tile_px)) + 1,) * 2 + (1,))[:n, :n]
    sheet = np.concatenate([big, np.full((n, 24, 3), 255, np.uint8), ctl[:n, :n]], axis=1)
    Image.fromarray(sheet).save(out_png)


def lit_view(d, n, s, k=3, light=(-0.55, 0.6, 0.58)):
    """A k x k tiling lit by one raking light (a check of normals and gloss; green-down normals)."""
    nv = n.astype(float) / 127.5 - 1.0
    nv[..., 1] = -nv[..., 1]
    nv[..., 2] = np.sqrt(np.clip(1.0 - nv[..., 0] ** 2 - nv[..., 1] ** 2, 0, 1))
    nv /= np.linalg.norm(nv, axis=2, keepdims=True)
    lv = np.array(light) / np.linalg.norm(light)
    hv = lv + np.array([0, 0, 1.0])
    hv /= np.linalg.norm(hv)
    alb = (d.astype(float) / 255.0) ** 2.2
    lam = np.clip(nv @ lv, 0, 1)[..., None]
    gloss = s[..., 1:2].astype(float) / 255.0
    spec = (np.clip(nv @ hv, 0, 1)[..., None] ** 30) * 0.12 * gloss
    occ = s[..., 2:3].astype(float) / 255.0
    out = (alb * (0.16 + 0.95 * lam) * occ + spec) ** (1 / 2.2)
    return np.tile(np.clip(out * 255, 0, 255).astype(np.uint8), (k, k, 1))


# ------------------------------------------------------------------------------------------------- build

def _hex(rgb):
    return "#%02x%02x%02x" % tuple(int(round(min(255, max(0, v)))) for v in rgb)


def _png(path):
    return np.asarray(Image.open(path).convert("RGB"))


def face_colour(d, maps):
    """The mean colour of the block faces (height 1), the colour that a trim member must match."""
    return d.reshape(-1, 3)[(maps["height"] > 0.9).ravel()].mean(0)


def trim_strip_means(trim_d):
    """Mean luminance of each carved strip of the trim sheet."""
    from make_rivendell_wall_textures import strip_rows
    lum = luminance(trim_d.astype(float))
    return {k: round(float(lum[t:b].mean()), 1) for k, (t, b) in strip_rows().items()}


def equalise_strips(trim_d, face_lum):
    """Add a constant per carved strip so that each strip's mean luminance is the wall face's
    (Painter: one Levels layer per strip, masked to its rows)."""
    from make_rivendell_wall_textures import strip_rows
    out = trim_d.astype(float)
    for t, b in strip_rows().values():
        out[t:b] += face_lum - float(luminance(out[t:b]).mean())
    return np.clip(np.rint(out), 0, 255).astype(np.uint8)


def build_trim(cdir, base, spec, ppm, face, gloss_mean, passes=3):
    """The trim set through make_tileable_texture_set.py with the candidate's dressed stone (recoloured to
    the face colour) as surface, the target moved until the sheet mean is within 1 level of the face."""
    work = cdir + "/_trim_work"
    os.makedirs(work, exist_ok=True)
    d = copy.deepcopy(spec["d"])
    d["target"] = [float(v) for v in face]
    dressed = np.clip(np.rint(compose_diffuse(base["color"], None, d, ppm, with_layout=False)), 0, 255).astype(np.uint8)
    surf = work + "/dressed_d.png"
    Image.fromarray(dressed).save(surf)
    face_lum = float(luminance(np.asarray(face)))
    target = list(face)
    for _ in range(passes):
        subprocess.run([sys.executable, os.path.join(HERE, "..", "oneoff", "make_tileable_texture_set.py"),
                        "--out", work, "--only", TRIM, "--trim-surface", surf, "--trim-target", _hex(target),
                        "--trim-border-rows", "0"], check=True, capture_output=True)
        sheet = _png("%s/%s_d.png" % (work, TRIM))
        delta = face_lum - float(luminance(sheet.astype(float)).mean())
        if abs(delta) <= 1.0:
            break
        target = [v + delta for v in target]
    for suffix, dst in (("_d", "_d"), ("_n_flipgreen", "_n"), ("_s", "_s")):
        os.replace("%s/%s%s.png" % (work, TRIM, suffix), "%s/%s%s.png" % (cdir, TRIM, dst))
    s = np.asarray(Image.open("%s/%s_s.png" % (cdir, TRIM)).convert("RGB")).astype(float)
    s[..., 1] = shift_to_mean(s[..., 1], gloss_mean)
    Image.fromarray(np.clip(np.rint(s), 0, 255).astype(np.uint8)).save("%s/%s_s.png" % (cdir, TRIM))
    import shutil
    shutil.rmtree(work)
    trim_d = _png("%s/%s_d.png" % (cdir, TRIM))
    before = trim_strip_means(trim_d)
    trim_d = equalise_strips(trim_d, face_lum)
    Image.fromarray(trim_d).save("%s/%s_d.png" % (cdir, TRIM))
    return {"face_rgb": [round(float(v), 1) for v in face], "face_lum": round(face_lum, 1),
            "trim_sheet_lum": round(float(luminance(trim_d.astype(float)).mean()), 1),
            "strip_lum_before": before, "strip_lum": trim_strip_means(trim_d), "target_used": _hex(target)}


def build_plinth(folder, kind, cfg, wall_target):
    """A plinth set (t_rivendell_wall_rubble_a) of kind 'rustic' or 'rubble' in `folder`."""
    p = cfg[kind]
    base = load_base(p["export"])
    lum = p.get("lum", 150.0)
    target = scale_to_luminance(wall_target, lum)
    out = compose_plinth(base, target, p.get("sat_max", 0.09), p.get("normal_form", "up"), p.get("tilt"),
                         p.get("gloss_mean", 70.0))
    write_set(folder, PLINTH, out)
    return {"kind": kind, "export": p["export"], "target": [round(v, 1) for v in target],
            "stats": tile_stats(out["d"], p.get("ppm", 461.0)), "normal_tilt": round(mean_tilt(out["n"]), 3),
            "gloss_mean": round(float(out["s"][..., 1].mean()), 1)}


def build_wall(name, cfg, out_root, steps, base_dir, plinth_cfg):
    cdir = "%s/%s" % (out_root, name)
    os.makedirs(cdir, exist_ok=True)
    ppm = float(cfg["layout"].get("ppm", 461.0))
    lay_dir = cdir + "/layout"
    spec = merge_spec(cfg.get("spec"))
    pj = cdir + "/params.json"
    params = json.load(open(pj)) if os.path.exists(pj) else {}
    rep = params.setdefault("report", {})
    if "layout" in steps:
        kw = {k: v for k, v in cfg["layout"].items() if k not in ("ppm", "size")}
        blocks, maps, info = bl.make_layout(ppm, cfg["layout"].get("size", 1024), **kw)
        bl.write(lay_dir, maps, blocks)
        rep["layout"] = {k: v for k, v in maps["info"].items() if k not in ("tones", "chips")}
        rep["layout"]["chips"] = len(maps["info"]["chips"])
    maps, info = load_layout(lay_dir)
    info = dict(info, ppm=ppm)
    base = load_base(base_dir)
    if "wall" in steps:
        out = compose(base, maps, spec, info)
        write_set(cdir, WALL, out)
        rep["wall"] = {"ours": tile_stats(out["d"], ppm), "control_same_ppm": control_stats(ppm),
                       "normal_tilt": round(mean_tilt(out["n"]), 3),
                       "gloss_mean": round(float(out["s"][..., 1].mean()), 1)}
    wall_d = _png("%s/%s_d.png" % (cdir, WALL))
    if "trim" in steps:
        rep["trim"] = build_trim(cdir, base, spec, ppm, face_colour(wall_d, maps), spec["s"]["gloss_mean"])
    if "plinth" in steps:
        rep["plinth"] = build_plinth(cdir, "rustic", plinth_cfg, tuple(spec["d"]["target"]))
    if "sheet" in steps:
        flat_sheet("%s/%s_d.png" % (cdir, WALL), ppm, cdir + "/flat_vs_gondor.png")
        d, n, s = (_png("%s/%s_%s.png" % (cdir, WALL, k)) for k in "dns")
        Image.fromarray(lit_view(d, n, s)).resize((1536, 1536), Image.LANCZOS).save(cdir + "/lit_3x3.png")
        Image.fromarray(np.tile(d, (3, 3, 1))).resize((1536, 1536), Image.LANCZOS).save(cdir + "/tiled_3x3.png")
    params.update({"name": name, "note": cfg.get("note", ""), "ppm": ppm, "layout_config": cfg["layout"],
                   "spec": spec, "painter_layers": PAINTER_LAYERS, "layout_folder": lay_dir,
                   "base_folder": base_dir})
    json.dump(params, open(pj, "w"), indent=1)
    return rep


def build_siblings(name, out_root, plinth_cfg, spec_target):
    """Folders <name>_rustic and <name>_rubble: the candidate's wall and trim sets and one kind of plinth."""
    import shutil
    src = "%s/%s" % (out_root, name)
    res = {}
    for kind in ("rustic", "rubble"):
        dst = "%s/%s_%s" % (out_root, name, kind)
        os.makedirs(dst, exist_ok=True)
        for f in os.listdir(src):
            if f.startswith((WALL, TRIM)) and f.endswith(".png"):
                shutil.copy2("%s/%s" % (src, f), "%s/%s" % (dst, f))
        res[kind] = build_plinth(dst, kind, plinth_cfg, spec_target)
        json.dump({"candidate": name, "plinth": res[kind]}, open(dst + "/params.json", "w"), indent=1)
    return res


def build_detail(out_root, cfg):
    """The detail normal map t_rivendell_stone_detail_n.png (512 px, green down, tiling)."""
    src = np.asarray(Image.open(cfg["source_height"]).convert("L"), dtype=float)
    n = detail_normal(src, cfg.get("size", 512), cfg.get("tilt", 0.065), cfg.get("tool_marks", 0.4), cfg.get("seed", 1))
    path = out_root + "/t_rivendell_stone_detail_n.png"
    Image.fromarray(n).save(path)
    f = n[..., :2].astype(float)
    seam = [round(float(np.abs(np.take(f, 0, axis=a) - np.take(f, -1, axis=a)).mean() / np.abs(np.diff(f, axis=a)).mean()), 2)
            for a in (0, 1)]
    return {"file": path, "size": n.shape[0], "mean_tilt": round(mean_tilt(n), 3), "seam_ratio_rows_cols": seam}


def main(argv=None):
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build")
    b.add_argument("config")
    b.add_argument("names", nargs="*")
    b.add_argument("--steps", default="layout,wall,trim,plinth,sheet")
    b.add_argument("--out", default=WORK + "/textures/v05_preview")
    b.add_argument("--siblings", default="", help="comma list of candidates that get _rustic and _rubble folders")
    b.add_argument("--detail", action="store_true")
    s = sub.add_parser("stats")
    s.add_argument("diffuse")
    s.add_argument("--ppm", type=float, default=461.0)
    a = ap.parse_args(argv)
    if a.cmd == "stats":
        img = np.asarray(Image.open(a.diffuse).convert("RGB"))
        print(json.dumps({"ours": tile_stats(img, a.ppm), "control": control_stats(a.ppm)}, indent=1))
        return
    cfg = json.load(open(a.config, encoding="utf-8"))
    names = a.names or [k for k in cfg if not k.startswith("_")]
    for name in names:
        rep = build_wall(name, cfg[name], a.out, a.steps.split(","), cfg["_base"], cfg["_plinth"])
        print(name, json.dumps(rep.get("wall", {}).get("ours", {})), flush=True)
    for name in [v for v in a.siblings.split(",") if v]:
        t = tuple(merge_spec(cfg[name].get("spec"))["d"]["target"])
        print(name, json.dumps(build_siblings(name, a.out, cfg["_plinth"], t))[:300], flush=True)
    if a.detail:
        print(json.dumps(build_detail(a.out, cfg["_detail"])), flush=True)


if __name__ == "__main__":
    main()
