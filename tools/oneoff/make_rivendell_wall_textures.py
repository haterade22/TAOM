#!/usr/bin/env python3
"""Author the three 1K texture sets of the Rivendell wall (kingdom kits job, issue #1600).

Sets (pinned in docs/kitbash/rivendell-siege-set.md and the job brief):
  t_rivendell_wall_stone_a   tiling ashlar masonry, tiles in U and V
  t_rivendell_wall_rubble_a  tiling coursed rubble, tiles in U and V
  t_rivendell_wall_trim_a    trim sheet, five strips in V, tiles in U

Each set writes <set>_d.png, <set>_n.png (tangent space, Y up, the OpenGL sense Blender bakes),
<set>_n_flipgreen.png (green inverted) and <set>_s.png (channel statistics inside the range of three
Gondor control specular maps, which are read only and used for statistics, never copied).

Everything is procedural numpy and PIL: the stone grain is FFT noise, the joints are drawn here. The
bought kit's tileable stone files were looked at and rejected (flat grey concrete), so no kit pixel
is used.

Run:  python tools/oneoff/make_rivendell_wall_textures.py [--out DIR] [--rounds-note TEXT]
"""
import argparse
import json
import os

import numpy as np
from PIL import Image

WORK = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell"
DEFAULT_OUT = WORK + "/textures/v01"
GONDOR = ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/AssetSources/"
          "Scenes/Gondor/textures/")
SIZE = 1024
LUM_W = np.array([0.299, 0.587, 0.114])

STONE_TARGET = (199, 184, 158)   # #c7b89e
RUBBLE_TARGET = (135, 122, 104)  # #877a68
TRIM_TARGET = (222, 214, 203)    # #ded6cb

# ranges measured from three Gondor control _s maps (spec_control_stats.json), channel -> (mean, std)
SPEC_STONE = {"g": (67.0, 14.0), "b": (216.0, 26.0)}
SPEC_RUBBLE = {"g": (67.0, 15.0), "b": (214.0, 28.0)}
SPEC_TRIM = {"g": (72.0, 25.0), "b": (214.0, 38.0)}

# strip limits in V with V = 0 at the bottom of the image (the pinned layout)
STRIP_V = {"S1": (0.0, 0.28), "S2": (0.28, 0.49), "S3": (0.49, 0.70), "S4": (0.70, 0.805), "S5": (0.805, 1.0)}
SAFE = 4


# ---------------------------------------------------------------- pure functions

def tileable_noise(size, beta=2.0, seed=0, aniso=(1.0, 1.0)):
    """Periodic power-law noise (amplitude ~ k^(-beta/2)), zero mean, unit std. aniso scales kx, ky."""
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((size, size)))
    k = np.fft.fftfreq(size) * size
    kx, ky = np.meshgrid(k, k)
    r = np.sqrt((kx * aniso[0]) ** 2 + (ky * aniso[1]) ** 2)
    r[0, 0] = 1.0
    amp = r ** (-beta / 2.0)
    amp[0, 0] = 0.0
    out = np.real(np.fft.ifft2(f * amp))
    out -= out.mean()
    return out / out.std()


def seam_mad(img, axis):
    """Mean absolute difference between the first and last column (axis 1) or row (axis 0)."""
    a = np.asarray(img, dtype=float)
    if axis == 1:
        return float(np.abs(a[:, 0] - a[:, -1]).mean())
    return float(np.abs(a[0] - a[-1]).mean())


def interior_mad(img, axis):
    """Mean absolute difference between every pair of neighbouring columns (axis 1) or rows (axis 0)."""
    a = np.asarray(img, dtype=float)
    return float(np.abs(np.diff(a, axis=axis)).mean())


def strip_rows():
    """Image rows (top, bottom) of each strip; row 0 is the top of the image (V = 1)."""
    out = {}
    for name, (v0, v1) in STRIP_V.items():
        out[name] = (int(round((1.0 - v1) * SIZE)), int(round((1.0 - v0) * SIZE)))
    return out


def flatten_margin(a, margin):
    """Copy the first interior row over the top margin rows and the last interior row over the bottom."""
    out = np.array(a, copy=True)
    for r in range(margin):
        out[r] = out[margin]
        out[-1 - r] = out[-margin - 1]
    return out


def height_to_normal(h, strength, wrap_u=True, wrap_v=True):
    """Tangent-space normal map, Y up (OpenGL), from a height field. Returns uint8 HxWx3."""
    h = np.asarray(h, dtype=float)
    pu = np.pad(h, ((0, 0), (1, 1)), mode="wrap" if wrap_u else "edge")
    gx = (pu[:, 2:] - pu[:, :-2]) * 0.5
    pv = np.pad(h, ((1, 1), (0, 0)), mode="wrap" if wrap_v else "edge")
    g_row = (pv[2:, :] - pv[:-2, :]) * 0.5
    nx = -gx * strength
    ny = g_row * strength  # rows run downward, so d(height)/d(up) = -g_row and ny = -d/d(up)
    nz = np.ones_like(nx)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / ln, ny / ln, nz / ln], axis=-1)
    return np.clip(np.round((n * 0.5 + 0.5) * 255.0), 0, 255).astype(np.uint8)


def flip_green(n):
    out = np.array(n, copy=True)
    out[..., 1] = 255 - out[..., 1]
    return out


def lum_of(rgb):
    return np.asarray(rgb, dtype=float) @ LUM_W


def match_color(rgb, target_mean, spread_range, rounds=8):
    """Shift the mean to target_mean and scale deviations so the luminance p95-p5 lands mid-range."""
    a = np.asarray(rgb, dtype=float).copy()
    t = np.array(target_mean, dtype=float)
    for _ in range(rounds):
        a += t - a.reshape(-1, 3).mean(0)
        if spread_range is not None:
            l = lum_of(a)
            s = float(np.percentile(l, 95) - np.percentile(l, 5))
            aim = 0.5 * (spread_range[0] + spread_range[1])
            if spread_range[0] <= s <= spread_range[1] and abs(s - aim) < 1.0:
                a = np.clip(a, 0, 255)
                continue
            m = a.reshape(-1, 3).mean(0)
            a = m + (a - m) * (aim / max(s, 1e-6))
        a = np.clip(a, 0, 255)
    a += t - a.reshape(-1, 3).mean(0)
    return np.clip(a, 0, 255)


def match_stats(x, mean, std):
    x = np.asarray(x, dtype=float)
    return (x - x.mean()) / max(x.std(), 1e-9) * std + mean


def smoothstep(e0, e1, x):
    t = np.clip((np.asarray(x, dtype=float) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur(a, sigma, wrap_u=True, wrap_v=True):
    r = int(3 * sigma) + 1
    xs = np.arange(-r, r + 1)
    w = np.exp(-(xs ** 2) / (2.0 * sigma ** 2))
    w /= w.sum()
    out = np.asarray(a, dtype=float)
    for axis, wrap in ((1, wrap_u), (0, wrap_v)):
        pad = [(0, 0)] * out.ndim
        pad[axis] = (r, r)
        p = np.pad(out, pad, mode="wrap" if wrap else "edge")
        n = out.shape[axis]
        acc = np.zeros_like(out)
        for i, wi in enumerate(w):
            sl = [slice(None)] * out.ndim
            sl[axis] = slice(i, i + n)
            acc += wi * p[tuple(sl)]
        out = acc
    return out


def profile(n, segs):
    """Height along n rows, bottom to top, from segments (length, kind, h0, h1).
    Kinds: flat, lin, conv (rounded, steep first), conc (cove, steep last)."""
    total = float(sum(s[0] for s in segs))
    t = (np.arange(n) + 0.5) / n * total
    out = np.zeros(n)
    start = 0.0
    for length, kind, h0, h1 in segs:
        end = start + length
        m = (t >= start) & (t < end) if end < total - 1e-9 else (t >= start)
        s = np.clip((t[m] - start) / length, 0, 1)
        if kind == "flat":
            v = np.full(s.shape, h0)
        elif kind == "lin":
            v = h0 + (h1 - h0) * s
        elif kind == "conv":
            v = h0 + (h1 - h0) * np.sqrt(np.clip(1 - (1 - s) ** 2, 0, 1))
        else:
            v = h0 + (h1 - h0) * (1 - np.sqrt(np.clip(1 - s ** 2, 0, 1)))
        out[m] = v
        start = end
    return out


# ---------------------------------------------------------------- stone

def _wrapdiff(v, size):
    return (v + size / 2.0) % size - size / 2.0


def build_stone(size=SIZE, seed=11):
    S = size
    cs = S // 4
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    xs = xx + tileable_noise(S, 2.8, seed + 1) * 1.6
    ys = yy + tileable_noise(S, 2.8, seed + 2) * 1.2
    fl = np.floor(ys / cs)
    course = fl.astype(int) % 4
    ry = ys - cs * fl
    dh = np.minimum(ry, cs - ry)
    # block joints per course: two blocks each (about 0.71 m), staggered, no joint within 110 px of
    # a joint in the next course (also across the wrap)
    joints = {0: (40, 560), 1: (300, 800), 2: (170, 690), 3: (430, 940)}
    block = np.zeros((S, S), dtype=int)
    dv = np.zeros((S, S))
    for c, (a, b) in joints.items():
        xr = (xs - a) % S
        l1 = (b - a) % S
        first = xr < l1
        d1 = np.minimum(xr, l1 - xr)
        d2 = np.minimum(xr - l1, S - xr)
        msk = course == c
        dv[msk] = np.where(first, d1, d2)[msk]
        block[msk] = np.where(first, 0, 1)[msk]
    bid = course * 2 + block
    d = np.minimum(dh, dv)

    jw = 7.0
    edge = smoothstep(jw / 2, jw / 2 + 4, d)
    chamfer = 0.88 + 0.12 * smoothstep(jw / 2 + 3, 26, d)
    rng = np.random.default_rng(seed)
    val = rng.normal(0, 7.5, 8)
    tint = rng.normal(0, 1, (8, 1)) * np.array([[4.0, 1.5, -4.0]]) + rng.normal(0, 1.5, (8, 3))
    hoff = rng.uniform(-0.05, 0.05, 8)
    grain = tileable_noise(S, 1.3, seed + 3)
    tool_h = tileable_noise(S, 1.0, seed + 4, aniso=(0.08, 1.0))
    tool_v = tileable_noise(S, 1.0, seed + 5, aniso=(1.0, 0.08))
    tool = np.where(bid % 2 == 0, tool_h, tool_v)
    mott = tileable_noise(S, 3.0, seed + 6)
    mid = tileable_noise(S, 2.0, seed + 7)
    pit_n = tileable_noise(S, 0.3, seed + 8)
    pits = smoothstep(2.1, 3.2, pit_n)  # small pits, a few per 10 cm
    vein_n = tileable_noise(S, 3.4, seed + 9, aniso=(0.35, 1.0))
    veins = 1.0 - smoothstep(0.0, 0.16, np.abs(vein_n))  # thin pale or dark veins, 0..1
    chip_n = tileable_noise(S, 1.6, seed + 10)
    chips = smoothstep(1.1, 1.9, chip_n) * (1 - smoothstep(6, 30, d))  # chipped arrises
    h = edge * (chamfer * 0.75 + 0.05 * grain + 0.06 * tool + 0.05 * mid + hoff[bid])
    h = np.clip(h - 0.35 * pits * edge - 0.30 * chips * edge - 0.06 * veins * edge, 0, 1)

    base = np.array(STONE_TARGET, dtype=float)
    col = base + tint[bid] + val[bid][..., None]
    col = col + (3.5 * grain + 3.0 * tool + 6.0 * mott)[..., None]
    mott2 = tileable_noise(S, 1.9, seed + 11)
    col = col + (7.0 * mid + 6.0 * mott2)[..., None] * np.array([1.0, 0.95, 0.85])
    col = col * (1.0 - 0.16 * np.exp(-d / 12.0))[..., None]
    col = col * (1.0 - 0.14 * smoothstep(0.3, 1.6, mott))[..., None]
    col = col * (1.0 - 0.28 * pits - 0.07 * veins * (vein_n > 0) + 0.05 * veins * (vein_n <= 0) - 0.12 * chips)[..., None]
    col = col * (1.0 + 0.05 * smoothstep(4, 9, d) * (1 - smoothstep(9, 18, d)))[..., None]
    col = col + np.array([-4.0, -2.0, 6.0]) * smoothstep(0.6, 1.8, mott2)[..., None]
    mortar = base * 0.62 + (2.0 * grain)[..., None]
    col = mortar * (1 - edge)[..., None] + col * edge[..., None]
    col = match_color(col, STONE_TARGET, (38.0, 50.0))
    cav = 1.0 - np.exp(-d / 14.0)
    return {"d": col, "h": h, "cav": cav, "grain": grain, "strength": 4.0}


# ---------------------------------------------------------------- rubble

def rubble_rects(size, seed):
    rng = np.random.default_rng(seed)
    heights = [150, 120, 170, 140, 160, 130, 154]
    assert sum(heights) == size
    rects = []
    y = 0
    for ch in heights:
        widths = []
        rem = size
        while rem > 0:
            if rem <= 300:
                widths.append(rem)
                rem = 0
            elif rem <= 330:
                a = rem // 2
                widths += [a, rem - a]
                rem = 0
            else:
                w = int(rng.integers(110, min(300, rem - 110) + 1))
                widths.append(w)
                rem -= w
        x = int(rng.integers(0, size))
        for w in widths:
            if w >= 150 and ch >= 160 and rng.random() < 0.3:
                a = int(round(ch * rng.uniform(0.45, 0.55)))
                rects += [(x, y, w, a), (x, y + a, w, ch - a)]
            else:
                rects.append((x, y, w, ch))
            x += w
        y += ch
    return rects


def build_rubble(size=SIZE, seed=23):
    S = size
    rects = rubble_rects(S, seed)
    idr = np.full((S, S), -1, dtype=int)
    cx = np.zeros(len(rects))
    cy = np.zeros(len(rects))
    hx = np.zeros(len(rects))
    hy = np.zeros(len(rects))
    for i, (x0, y0, w, h_) in enumerate(rects):
        idr[np.ix_(np.arange(y0, y0 + h_) % S, np.arange(x0, x0 + w) % S)] = i
        cx[i] = x0 + w / 2.0
        cy[i] = y0 + h_ / 2.0
        hx[i] = w / 2.0
        hy[i] = h_ / 2.0
    assert (idr >= 0).all()
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    xs = xx + tileable_noise(S, 3.6, seed + 1) * 8.0
    ys = yy + tileable_noise(S, 3.6, seed + 2) * 8.0
    sid = idr[np.floor(ys).astype(int) % S, np.floor(xs).astype(int) % S]
    relx = _wrapdiff(xs - cx[sid], S)
    rely = _wrapdiff(ys - cy[sid], S)
    r = np.minimum(hx[sid], hy[sid]) * 0.4
    qx = np.abs(relx) - hx[sid] + r
    qy = np.abs(rely) - hy[sid] + r
    sdf = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    d = np.clip(-sdf, 0, None)

    rng = np.random.default_rng(seed + 9)
    n = len(rects)
    val = rng.normal(0, 14.0, n)
    tint = rng.normal(0, 1, (n, 1)) * np.array([[5.0, 1.5, -5.0]]) + rng.normal(0, 2.0, (n, 3))
    srand = rng.uniform(-1, 1, n)
    jw = 10.0
    edge = smoothstep(jw / 2, jw / 2 + 8, d)
    dome = smoothstep(jw / 2, 55, d) ** 0.6
    grain = tileable_noise(S, 1.4, seed + 3)
    bump = tileable_noise(S, 2.2, seed + 4)
    mott = tileable_noise(S, 3.0, seed + 5)
    mid = tileable_noise(S, 2.0, seed + 6)
    pits = smoothstep(2.1, 3.2, tileable_noise(S, 0.3, seed + 7))
    vein_n = tileable_noise(S, 3.4, seed + 8, aniso=(0.4, 1.0))
    veins = 1.0 - smoothstep(0.0, 0.14, np.abs(vein_n))
    h = edge * (0.45 + 0.40 * dome + 0.08 * srand[sid]) + 0.03 * grain * edge + 0.08 * bump * edge + 0.05 * mid * edge
    h = np.clip(h - 0.3 * pits * edge - 0.05 * veins * edge, 0, 1)

    base = np.array(RUBBLE_TARGET, dtype=float)
    col = base + tint[sid] + val[sid][..., None] + (5.0 * grain + 7.0 * mott + 8.0 * mid)[..., None]
    col = col * (1.0 - 0.30 * np.exp(-d / 14.0) - 0.25 * pits - 0.05 * veins * (vein_n > 0) + 0.04 * veins * (vein_n <= 0))[..., None]
    col = col * (0.92 + 0.12 * dome)[..., None]
    mortar = np.array([70.0, 62.0, 50.0]) + (3.0 * grain)[..., None]
    col = mortar * (1 - edge)[..., None] + col * edge[..., None]
    col = match_color(col, RUBBLE_TARGET, (48.0, 70.0))
    cav = 1.0 - np.exp(-d / 18.0)
    return {"d": col, "h": h, "cav": cav, "grain": grain, "strength": 2.6}


# ---------------------------------------------------------------- trim sheet

def _strip_heights(name, n, seed):
    """Height of one strip, n rows x SIZE, row 0 at the top of the strip."""
    u = np.arange(SIZE)[None, :].astype(float)
    r = np.arange(n)[:, None].astype(float)
    grain = tileable_noise(SIZE, 1.2, seed)[:n]
    tool = tileable_noise(SIZE, 1.0, seed + 1, aniso=(0.08, 1.0))[:n]
    if name == "S1":
        h = 0.5 + 0.025 * grain + 0.03 * tool
    elif name == "S2":
        segs = [(0.08, "flat", 0.30, 0.30), (0.22, "conc", 0.30, 0.62), (0.10, "flat", 0.62, 0.62),
                (0.22, "conv", 0.62, 0.92), (0.10, "flat", 0.92, 0.92), (0.10, "lin", 0.92, 0.70),
                (0.08, "flat", 0.70, 0.70), (0.10, "conc", 0.70, 0.80)]
        p = profile(n, segs)[::-1][:, None]
        pos = (np.arange(n)[::-1] + 0.5) / n  # 0 at the bottom of the strip
        total = sum(s[0] for s in segs)
        b0 = (segs[0][0] + segs[1][0]) / total
        b1 = (segs[0][0] + segs[1][0] + segs[2][0]) / total
        band = ((pos >= b0) & (pos < b1)).astype(float)[:, None]
        pu = u % 64.0  # dentils: 32 px blocks every 64 px, 16 per tile
        dent = smoothstep(0, 3, pu) * (1 - smoothstep(29, 32, pu))
        h = p + 0.14 * band * dent + 0.012 * grain
    elif name == "S3":
        yc = n / 2.0
        bg = 0.25
        a = 2 * np.pi * u / 512.0
        top = np.zeros((n, SIZE))
        for i in range(3):
            ph = i * 2 * np.pi / 3
            ycen = yc + 38.0 * np.sin(a + ph)
            dist = np.abs(r - ycen)
            tube = np.sqrt(np.clip(1 - (dist / 24.0) ** 2, 0, 1))
            z = 0.5 + 0.5 * np.cos(a + ph)
            hi = tube * (0.30 + 0.20 * z)
            top = np.maximum(top, hi)
        bead = np.zeros((n, 1))
        for c in (17.0, n - 18.0):
            bead = np.maximum(bead, 0.30 * np.sqrt(np.clip(1 - (np.abs(r - c) / 9.0) ** 2, 0, 1)))
        h = bg + np.maximum(top, bead) + 0.012 * grain
    elif name == "S4":
        # roll moulding with a fillet each side and a cavetto beyond; x runs bottom to top in px
        x = (n - 1 - np.arange(n)).astype(float)

        def cavetto(s):
            return 0.20 + 0.25 * (1 - np.sqrt(np.clip(1 - s ** 2, 0, 1)))

        hh = np.full(n, 0.20)
        hh = np.where((x >= 10) & (x < 22), cavetto((x - 10) / 12.0), hh)
        hh = np.where((x >= 22) & (x < 32), 0.45, hh)
        hh = np.where((x >= 32) & (x <= 76), 0.45 + 0.50 * np.sqrt(np.clip(1 - ((x - 54.0) / 22.0) ** 2, 0, 1)), hh)
        hh = np.where((x > 76) & (x < 86), 0.45, hh)
        hh = np.where((x >= 86) & (x < 98), cavetto((98 - x) / 12.0), hh)
        h = hh[:, None] + 0.012 * grain
    else:  # S5
        row_h = np.interp(np.arange(n), [0, 5, 9, 14, 29, 35, n - 36, n - 30, n - 15, n - 10, n - 1],
                          [0.45, 0.45, 0.62, 0.62, 0.62, 0.30, 0.30, 0.62, 0.62, 0.45, 0.45])
        inside = smoothstep(33, 37, np.arange(n)) * (1 - smoothstep(n - 38, n - 34, np.arange(n)))
        du = np.minimum(u % 512.0, 512.0 - u % 512.0)
        stile = 1 - smoothstep(10, 17, du)
        stile_h = 0.30 + 0.32 * stile
        h = np.maximum(row_h[:, None], stile_h * inside[:, None]) + 0.014 * grain
    return h


STRIP_STRENGTH = {"S1": 2.5, "S2": 6.0, "S3": 6.0, "S4": 7.0, "S5": 6.0}


def build_trim(seed=37):
    rows = strip_rows()
    H = np.zeros((SIZE, SIZE))
    N = np.zeros((SIZE, SIZE, 3), dtype=np.uint8)
    D = np.zeros((SIZE, SIZE, 3))
    cav = np.zeros((SIZE, SIZE))
    base = np.array(TRIM_TARGET, dtype=float)
    grain = tileable_noise(SIZE, 1.3, seed + 20)
    for k, (name, (t, b)) in enumerate(rows.items()):
        n = b - t
        h = flatten_margin(_strip_heights(name, n, seed + k * 3), SAFE)
        H[t:b] = h
        N[t:b] = height_to_normal(h, STRIP_STRENGTH[name], wrap_u=True, wrap_v=False)
        hn = (h - h.min()) / max(h.max() - h.min(), 1e-6)
        ao = np.clip((blur(h, 5.0, True, False) - h) * 6.0, 0, 1)
        shade = 1.0 - 0.22 * (1 - hn) - 0.40 * ao
        c = base[None, None, :] * shade[..., None] * (1 + 0.025 * grain[t:b])[..., None]
        c = flatten_margin(c, SAFE)
        D[t:b] = c
        cav[t:b] = flatten_margin(np.clip(1 - 0.5 * hn + ao, 0, 1), SAFE)
    D = match_color(D, TRIM_TARGET, None)
    # margins stay replicated after the global shift because the shift is uniform
    return {"d": D, "h": H, "n": N, "cav": cav, "grain": grain}


# ---------------------------------------------------------------- spec and writing

def make_spec(h, cav, grain, params, seed):
    z1 = tileable_noise(SIZE, 1.5, seed)
    z2 = tileable_noise(SIZE, 1.5, seed + 1)
    hz = (h - h.mean()) / max(h.std(), 1e-9)
    cz = (cav - cav.mean()) / max(cav.std(), 1e-9)
    g = match_stats(0.6 * hz + 0.5 * z1 + 0.2 * grain, *params["g"])
    b = match_stats(-0.6 * cz + 0.6 * z2, *params["b"])
    out = np.zeros((SIZE, SIZE, 3))
    out[..., 1] = np.clip(g, 0, 255)
    out[..., 2] = np.clip(b, 0, 255)
    return np.round(out).astype(np.uint8)


def save_rgb(path, arr):
    Image.fromarray(np.asarray(arr).astype(np.uint8), "RGB").save(path)


def to_u8(a):
    return np.clip(np.round(a), 0, 255).astype(np.uint8)


def lit_preview(d, n):
    nn = n.astype(float) / 255.0 * 2 - 1
    L = np.array([-0.55, 0.55, 0.65])
    L /= np.linalg.norm(L)
    shade = np.clip((nn * L).sum(-1), 0, 1)
    return to_u8(d.astype(float) * (0.30 + 0.85 * shade)[..., None])


def channel_stats(path):
    a = np.asarray(Image.open(path).convert("RGB")).astype(float)
    return {c: {"mean": round(float(a[..., i].mean()), 2), "std": round(float(a[..., i].std()), 2),
                "p5": float(np.percentile(a[..., i], 5)), "p95": float(np.percentile(a[..., i], 95))}
            for i, c in enumerate("rgb")}


def write_set(out, name, built, spec_params, seed, wrap_v=True):
    d8 = to_u8(built["d"])
    if "n" in built:
        n8 = built["n"]
    else:
        n8 = height_to_normal(built["h"], built["strength"], True, wrap_v)
    s8 = make_spec(built["h"], built["cav"], built["grain"], spec_params, seed)
    save_rgb(out + "/%s_d.png" % name, d8)
    save_rgb(out + "/%s_n.png" % name, n8)
    save_rgb(out + "/%s_n_flipgreen.png" % name, flip_green(n8))
    save_rgb(out + "/%s_s.png" % name, s8)
    return d8, n8, s8


def seam_report(maps, axes):
    rep = {}
    for key, arr in maps.items():
        for ax in axes:
            s, i = seam_mad(arr, ax), interior_mad(arr, ax)
            rep["%s axis%d" % (key, ax)] = {"edge_mad": round(s, 3), "interior_mad": round(i, 3),
                                            "limit_1_5x": round(1.5 * i, 3), "pass": bool(s <= 1.5 * i)}
    return rep


def save_big(path, arr, width):
    im = Image.fromarray(arr, "RGB")
    if im.width > width:
        im = im.resize((width, int(im.height * width / im.width)), Image.LANCZOS)
    im.save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=DEFAULT_OUT)
    args = ap.parse_args()
    out = args.out
    prev = out + "/preview"
    os.makedirs(prev, exist_ok=True)

    sets = {}
    st = build_stone()
    sets["t_rivendell_wall_stone_a"] = (write_set(out, "t_rivendell_wall_stone_a", st, SPEC_STONE, 101), (0, 1))
    ru = build_rubble()
    sets["t_rivendell_wall_rubble_a"] = (write_set(out, "t_rivendell_wall_rubble_a", ru, SPEC_RUBBLE, 202), (0, 1))
    tr = build_trim()
    sets["t_rivendell_wall_trim_a"] = (write_set(out, "t_rivendell_wall_trim_a", tr, SPEC_TRIM, 303), (1,))

    seams = {}
    for name, ((d8, n8, s8), axes) in sets.items():
        seams.update(seam_report({name + "_d": d8, name + "_n": n8, name + "_s": s8}, axes))
        if len(axes) == 2:
            save_big(prev + "/%s_tiled3x3.png" % name, np.tile(d8, (3, 3, 1)), 1536)
            save_big(prev + "/%s_lit3x3.png" % name, np.tile(lit_preview(d8, n8), (3, 3, 1)), 1536)
        else:
            save_big(prev + "/%s_tiled3x1.png" % name, np.tile(d8, (1, 3, 1)), 1536)
            save_big(prev + "/%s_lit3x1.png" % name, np.tile(lit_preview(d8, n8), (1, 3, 1)), 1536)
        save_big(prev + "/%s_lit1x1.png" % name, lit_preview(d8, n8), 1024)
    with open(out + "/seam_report.json", "w") as f:
        json.dump(seams, f, indent=1)

    ctl = {}
    for n in ("t_gondor_bricks_large_a_dirty_s", "t_gondor_stone_trim_a1_s", "t_gondor_rubble_stone_a_s"):
        ctl[n] = channel_stats(GONDOR + n + ".png")
    out_spec = {k: channel_stats(out + "/%s_s.png" % k) for k in sets}
    with open(out + "/spec_stats.json", "w") as f:
        json.dump({"control": ctl, "ours": out_spec}, f, indent=1)

    # stone beside the Gondor control stone at the same pixel scale (native 1024 crop of the 2048 control)
    g = np.asarray(Image.open(GONDOR + "t_gondor_bricks_large_a_dirty_d2.png").convert("RGB"))
    c0 = (g.shape[0] - SIZE) // 2
    ctl_crop = g[c0:c0 + SIZE, c0:c0 + SIZE]
    d8 = sets["t_rivendell_wall_stone_a"][0][0]
    sheet = np.concatenate([d8, np.full((SIZE, 16, 3), 255, np.uint8), ctl_crop], axis=1)
    save_big(prev + "/stone_vs_gondor.png", sheet, 2048)
    print("done")


if __name__ == "__main__":
    main()
