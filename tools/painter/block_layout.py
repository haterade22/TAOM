"""A block layout for a tiling ashlar wall, drawn by script (no Painter needed).

plan_blocks() lays the courses and blocks (wrapping in both axes, vertical joints staggered).
render() draws the maps that a Painter layer stack loads as textures: block id (one grey per block),
height (block face 1, joint 0, a 45 degree bevel between), edge mask (1 - height), joint occlusion and a
seamless stain map. Pure numpy; tests are in tools/tests/test_block_layout.py.

Version 2 (render_v2, make_layout): a quarter-round fall of the height next to the joint, a joint
width in px, an edge-light band, sparse chips, a cloud map (a different soft field in each block), one
tone per block with no landmark block, course heights as a pattern, and a pixels-per-metre parameter so
that a layout fits a tile of 2.222 m (461 px per metre) or 3.333 m (307). Every map wraps in both axes.

Run:  python tools/painter/block_layout.py --out <dir> --heights 192,160,160,192,160,160
          --lengths 256,384,512 [--size 1024 --seed 1 --stagger 64 --joint 2 --arris 2]
It writes layout_id.png, layout_height.png (16 bit), layout_edge.png, layout_ao.png, layout_stain.png and
layout.json (the blocks). With --v2 (see make_layout) it also writes layout_joint, _edgelight, _chip and _cloud.
"""
import argparse
import json
import os

import numpy as np
from PIL import Image


def _compositions(total, lengths, rng, tries=400):
    """A random list of block lengths from `lengths` that sums to `total`, or None."""
    for _ in range(tries):
        out, left = [], total
        while left > 0:
            fit = [n for n in lengths if n <= left and (left - n == 0 or left - n >= min(lengths))]
            if not fit:
                break
            n = int(rng.choice(fit))
            out.append(n)
            left -= n
        if left == 0:
            return out
    return None


def _cuts(lengths, size, offset=0):
    """Start x of each block, shifted by `offset` px around the wrap (a block may cross the tile edge)."""
    x, cuts = 0, []
    for n in lengths:
        cuts.append((x + offset) % size)
        x += n
    return cuts


def _staggered(a, b, size, gap):
    """True when no cut of `a` is within `gap` px of a cut of `b`, measured around the wrap."""
    for x in a:
        for y in b:
            d = abs(x - y)
            if min(d, size - d) < gap:
                return False
    return True


def plan_blocks(heights, lengths, size, seed, stagger, attempts=300):
    """Blocks as dicts x0, x1, y0, y1, id. `heights`: course heights in px (they sum to `size`).
    `lengths`: the block lengths in px. `stagger`: the least distance between a vertical joint and one
    in the next course (the last course also against the first, because the tile wraps)."""
    if sum(heights) != size:
        raise ValueError("course heights sum to %d, not %d" % (sum(heights), size))
    rng = np.random.default_rng(seed)
    for _ in range(attempts):
        rows = []
        ok = True
        for i in range(len(heights)):
            for _try in range(60):
                comp = _compositions(size, lengths, rng)
                if comp is None:
                    raise RuntimeError("the block lengths %s cannot fill %d px" % (list(lengths), size))
                shift = int(rng.integers(0, size))
                cuts = _cuts(comp, size, shift)
                if i == 0 or _staggered(rows[-1][0], cuts, size, stagger):
                    rows.append((cuts, comp))
                    break
            else:
                ok = False
                break
        if ok and (len(rows) == 1 or _staggered(rows[-1][0], rows[0][0], size, stagger)):
            blocks, y, n = [], 0, 0
            for (cuts, comp), h in zip(rows, heights):
                for x, w in zip(cuts, comp):
                    blocks.append({"x0": x, "x1": x + w, "y0": y, "y1": y + h, "id": n})
                    n += 1
                y += h
            return blocks
    raise RuntimeError("no layout with a %d px stagger in %d attempts" % (stagger, attempts))


def plan_courses(ppm, size, courses_px, lengths_m, stagger_m, seed, length_step=16):
    """Blocks for courses of the given heights in px (top course first, they sum to `size`) and block lengths
    between `lengths_m[0]` and `lengths_m[1]` metres. The head joints of two courses that touch (the last course
    against the first too) are at least `stagger_m` apart."""
    lengths = length_choices(ppm, lengths_m[0], lengths_m[1], length_step)
    return plan_blocks(list(courses_px), lengths, size, seed, int(round(stagger_m * ppm)))


def _gauss_periodic(a, sigma):
    """A blur that wraps in both axes (FFT)."""
    fy = np.fft.fftfreq(a.shape[0])[:, None]
    fx = np.fft.fftfreq(a.shape[1])[None, :]
    k = np.exp(-2.0 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * k))


def _stain(size, seed, power=2.2):
    """A seamless noise map in 0..1 with a 1/f^power spectrum (soft clouds)."""
    rng = np.random.default_rng(seed)
    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    f = np.sqrt(fx ** 2 + fy ** 2)
    f[0, 0] = 1.0
    spec = (rng.normal(size=(size, size)) + 1j * rng.normal(size=(size, size))) / f ** (power / 2.0)
    spec[0, 0] = 0
    a = np.real(np.fft.ifft2(spec))
    a = a - a.min()
    return a / a.max()


def render(blocks, size, joint=2, arris=2, seed=1, ao_sigma=5.0, ao_strength=2.2):
    """The maps of a layout as float arrays in 0..1: id, height, edge, ao, stain."""
    ids = np.zeros((size, size))
    dist = np.zeros((size, size))
    rng = np.random.default_rng(seed + 1000)
    yy, xx = np.mgrid[0:size, 0:size]
    for b in blocks:
        w = b["x1"] - b["x0"]
        left = (xx - b["x0"]) % size
        sel = (yy >= b["y0"]) & (yy < b["y1"]) & (left < w)
        ids[sel] = rng.uniform(0.0, 1.0)
        d = np.minimum.reduce([left, w - 1 - left, yy - b["y0"], b["y1"] - 1 - yy])
        dist[sel] = d[sel]
    half = joint / 2.0
    height = np.where(dist < half, 0.0, np.minimum(1.0, (dist - half + 1.0) / (arris + 1.0)))
    edge = 1.0 - height
    joints_only = (dist < half).astype(float)
    ao = np.clip(1.0 - ao_strength * _gauss_periodic(joints_only, ao_sigma), 0.0, 1.0)
    return {"id": ids, "height": height, "edge": edge, "ao": ao, "stain": _stain(size, seed)}


REF_PPM = 461.0  # pixels per metre of the reference tile (2.222 m of wall in 1024 px)


def scaled_px(px_at_ref, ppm):
    """A size given in pixels at 461 px per metre, kept the same in world size at `ppm`."""
    return max(1, int(round(px_at_ref * ppm / REF_PPM)))


def course_heights(size, ppm, pattern_m):
    """Course heights in px that sum to `size`. `pattern_m` (metres) repeats a whole number of times,
    for example (0.625, 0.486) for alternate courses. The pattern is scaled a little so that it fits."""
    tile_m = size / float(ppm)
    reps = max(1, int(round(tile_m / sum(pattern_m))))
    raw = list(pattern_m) * reps
    scale = size / sum(raw)
    vals = [v * scale for v in raw]
    out = [int(np.floor(v)) for v in vals]
    order = sorted(range(len(vals)), key=lambda i: vals[i] - out[i], reverse=True)
    for i in order[:size - sum(out)]:
        out[i] += 1
    return out


def length_choices(ppm, min_m, max_m, step=16):
    """Block lengths in px (multiples of `step`) that lie between `min_m` and `max_m` metres."""
    lo = int(np.ceil(min_m * ppm / step)) * step
    hi = int(np.floor(max_m * ppm))
    return list(range(lo, hi + 1, step))


def smooth_noise(size, sigma, seed):
    """Seamless noise with zero mean and unit std. The correlation falls to a half at 1.665 sigma."""
    rng = np.random.default_rng(seed)
    n = _gauss_periodic(rng.normal(size=(size, size)), sigma)
    return (n - n.mean()) / n.std()


def landmark_blocks(tones, z=1.8):
    """Ids of blocks whose tone is more than `z` standard deviations from the mean: a lone landmark."""
    keys = sorted(tones)
    vals = np.array([tones[k] for k in keys], dtype=float)
    if len(vals) < 3 or vals.std() == 0:
        return []
    zs = np.abs(vals - vals.mean()) / vals.std()
    return [k for k, v in zip(keys, zs) if v > z]


def _neighbour_pairs(bid):
    """Pairs of block ids that touch each other, also across the tile wrap."""
    pairs = set()
    for ax in (0, 1):
        other = np.roll(bid, -1, axis=ax)
        m = bid != other
        for a, b in zip(bid[m].tolist(), other[m].tolist()):
            pairs.add((min(a, b), max(a, b)))
    return sorted(pairs)


def assign_tones(n, pairs, lo, hi, rng, tries=300):
    """One tone per block, evenly spread over lo..hi (so no outlier) and shuffled so that touching blocks
    differ as much as the tries allow. Returns the tones and the least step between touching blocks."""
    vals = np.linspace(lo, hi, n) if n > 1 else np.array([(lo + hi) / 2.0])
    best, best_step = vals, -1.0
    for _ in range(tries):
        t = vals[rng.permutation(n)]
        step = min((abs(t[a] - t[b]) for a, b in pairs), default=1.0)
        if step > best_step:
            best, best_step = t, step
    return best, float(best_step)


def _course_groups(blocks):
    """The blocks of each course, top course first."""
    rows = {}
    for b in blocks:
        rows.setdefault((b["y0"], b["y1"]), []).append(b)
    return [rows[k] for k in sorted(rows)]


def _band_areas(blocks, size, bands):
    """The area of each block inside each of `bands` equal vertical bands (px squared), as a list of lists."""
    out = []
    for b in blocks:
        cols = np.arange(b["x0"], b["x1"]) % size
        counts = np.bincount(cols * bands // size, minlength=bands)
        out.append([float(c) * (b["y1"] - b["y0"]) for c in counts])
    return out


def balanced_tones(blocks, pairs, size, rng, extreme=0.7, bands=4, tries=1500, floor=0.35):
    """One tone z in -1..1 per block, so that no course is lighter than another.

    In every course the tones add up to zero when weighted by block length (the longest block takes the
    balance). The others are drawn from `floor`..1 in size with a random sign. Of many draws the best one
    is kept: no two touching blocks with |z| of `extreme` or more and the same sign, then the least spread
    between the means of `bands` equal vertical bands. Returns the tones by block id and an info dict
    (extreme_pairs, band_spread, course_means, band_means)."""
    courses = _course_groups(blocks)
    areas = dict(zip((b["id"] for b in blocks), _band_areas(blocks, size, bands)))
    best, best_key = None, None
    for _ in range(tries):
        z, ok = {}, True
        for course in courses:
            order = sorted(course, key=lambda b: -(b["x1"] - b["x0"]))
            main, rest = order[0], order[1:]
            acc = 0.0
            for b in rest:
                z[b["id"]] = float(rng.choice([-1.0, 1.0]) * rng.uniform(floor, 1.0))
                acc += z[b["id"]] * (b["x1"] - b["x0"])
            z[main["id"]] = -acc / (main["x1"] - main["x0"])
            if abs(z[main["id"]]) > 1.0:
                ok = False
                break
        if not ok:
            continue
        bad = sum(1 for a, b in pairs if abs(z[a]) >= extreme and abs(z[b]) >= extreme and z[a] * z[b] > 0)
        means = []
        for k in range(bands):
            tot = sum(areas[i][k] for i in z)
            means.append(sum(z[i] * areas[i][k] for i in z) / tot if tot else 0.0)
        key = (bad, max(means) - min(means))
        if best_key is None or key < best_key:
            best, best_key, best_means = z, key, means
    if best is None:
        raise RuntimeError("no balanced tones in %d tries" % tries)
    cm = [sum(best[b["id"]] * (b["x1"] - b["x0"]) for b in c) / sum(b["x1"] - b["x0"] for b in c) for c in courses]
    return best, {"extreme_pairs": int(best_key[0]), "band_spread": float(best_key[1]), "course_means": cm,
                  "band_means": best_means}


def _smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def _chip_plan(blocks, size, joint, radius, chip_frac, chip_px, rng):
    """Where the chips go: about `chip_frac` of the blocks, each with one chip at an edge or corner."""
    if chip_frac <= 0 or not blocks:
        return []
    n = max(1, int(round(chip_frac * len(blocks))))
    jl = (joint + 1) // 2
    inset = jl + 0.4 * radius
    chips = []
    for bi in rng.choice(len(blocks), size=min(n, len(blocks)), replace=False):
        b = blocks[int(bi)]
        w, h = b["x1"] - b["x0"], b["y1"] - b["y0"]
        r = int(rng.integers(chip_px[0], chip_px[1] + 1))
        u, v = rng.uniform(0.15, 0.85, size=2)
        kind = rng.choice(["left", "right", "top", "bottom", "corner"], p=[0.15, 0.15, 0.2, 0.2, 0.3])
        cx, cy = b["x0"] + u * w, b["y0"] + v * h
        if kind in ("left", "corner"):
            cx = b["x0"] + inset
        if kind == "right":
            cx = b["x1"] - 1 - inset
        if kind in ("top", "corner"):
            cy = b["y0"] + inset
        if kind == "bottom":
            cy = b["y1"] - 1 - inset
        if kind == "corner" and rng.random() < 0.5:
            cx = b["x1"] - 1 - inset
        chips.append({"block": int(b["id"]), "cx": int(round(cx)) % size, "cy": int(round(cy)) % size,
                      "r": r, "kind": str(kind), "phase": [float(p) for p in rng.uniform(0, 6.28, size=2)]})
    return chips


def soft_limit(x, limit):
    """`x` pressed smoothly inside plus or minus `limit` (tanh): small values pass almost unchanged."""
    return limit * np.tanh(np.asarray(x, dtype=float) / limit)


def band_noise(size, ppm, wave_m, seed):
    """A seamless noise field with zero mean and unit std whose wavelengths lie between `wave_m[0]` and
    `wave_m[1]` metres (a Gaussian ring in log frequency, plus or minus two sigma at the band edges)."""
    rng = np.random.default_rng(seed)
    fy = np.fft.fftfreq(size)[:, None] * ppm
    fx = np.fft.fftfreq(size)[None, :] * ppm
    f = np.sqrt(fx ** 2 + fy ** 2)
    f[0, 0] = 1.0
    lo, hi = 1.0 / wave_m[1], 1.0 / wave_m[0]
    s = np.log(hi / lo) / 4.0
    win = np.exp(-(np.log(f / np.sqrt(lo * hi)) ** 2) / (2.0 * s * s))
    win[0, 0] = 0.0
    spec = (rng.normal(size=(size, size)) + 1j * rng.normal(size=(size, size))) * win
    a = np.real(np.fft.ifft2(spec))
    return (a - a.mean()) / a.std()


def face_tilts(blocks, size, ppm, seed=1, depth_mm=22.0, tilt_deg=1.5, tilt_cap_mm=6.0, tilt_min=0.3):
    """A plane per block, in height units (one unit is `depth_mm`), zero at the middle of the block. The slope of
    a block is `tilt_deg` times a draw from `tilt_min`..1 (a random direction), and it is cut so that no corner
    rises or falls more than `tilt_cap_mm`: the height range of the map is limited. Each block draws from its own
    generator (seed and block id) and works in block coordinates, so a block over the tile edge is one
    plane and a shifted layout gives the same planes shifted."""
    yy, xx = np.mgrid[0:size, 0:size]
    mm = 1000.0 / ppm
    out = np.zeros((size, size))
    for b in blocks:
        w, h = b["x1"] - b["x0"], b["y1"] - b["y0"]
        rng = np.random.default_rng([seed, int(b["id"]), 77])
        slope = np.tan(np.radians(tilt_deg * float(rng.uniform(tilt_min, 1.0))))
        phi = float(rng.uniform(0.0, 2.0 * np.pi))
        c, s = np.cos(phi), np.sin(phi)
        reach = (0.5 * w * abs(c) + 0.5 * h * abs(s)) * mm
        if slope * reach > tilt_cap_mm:
            slope = tilt_cap_mm / reach
        left = (xx - b["x0"]) % size
        sel = (yy >= b["y0"]) & (yy < b["y1"]) & (left < w)
        d = ((left - w / 2.0) * c + (yy - b["y0"] - h / 2.0) * s) * mm * slope / depth_mm
        out[sel] = d[sel]
    return out


def render_v2(blocks, size, joint=5, radius=8, seed=1, radius_jitter=0.3, chip_frac=0.06, chip_px=(3, 8),
              cloud_px=60, cloud_std=0.16, tone_range=(0.2, 0.8), light_gain=(0.7, 1.25),
              ao_sigma=5.0, ao_strength=2.2, radius_detail=0.0, px_scale=1.0, profile="round", edge_gain=None,
              line_px=4.0, tone_amp=None, tone_floor=0.35, chip_depth=0.6, face=None, face_level=1.0,
              face_depth_mm=22.0, ppm=None, halo_mm=None):
    """Version 2 maps of a layout as float arrays in 0..1, plus "info".

    id        one tone per block (evenly spread over `tone_range`, no outlier block)
    height    face 1, joint 0, a quarter-round fall over `radius` px next to the joint, chips cut in
    edge      1 - height
    joint     the joint pixels (a faint blur of 0.5 px)
    edgelight a thin band just inside the rounded edge, a gain per block and a wear noise along it
    chip      the chips (1 = chipped), about `chip_frac` of the blocks, `chip_px` radius, at edges
    cloud     0.5 plus soft tone of `cloud_px` px and `cloud_std`, a different field in each block
    ao        joint occlusion, stain: seamless 1/f noise
    `joint` is the total width in px; the left and top share are the larger one when it is odd.
    `radius_detail` adds a finer wobble (3 px sigma) to the edge radius, so that the radius varies along an
    edge at two scales. `px_scale` stretches the noise sigmas of the radius and the wear (2.0 for a layout
    drawn at twice the pixel density).

    Round 3 options (all off by default): `profile` "chamfer" makes the fall of the height linear (a crisp 45
    degree arris) instead of a quarter round; `edge_gain` (top, right, bottom, left) draws the edge light as
    a line of `line_px` px (full width at half height) in the middle of the bevel, with a gain per side and no
    wear or gain per block; `tone_amp` replaces the evenly spread tones by balanced_tones() (id = 0.5 plus
    `tone_amp` times a tone in -1..1); `chip_depth` is how much of the height a chip removes.

    Round 4 (dressed ashlar): `face_level` is the height of the face in the map "height" (1.0 reaches the top
    of the map, the joint floor is 0; Painter's relief layer turns one unit into `face_depth_mm`, 22 mm measured).
    `face` (a dict, `ppm` needed) gives each block its own face: tilt_deg, tilt_cap_mm (a plane per block, see
    face_tilts), pillow_mm and pillow_width_mm (the face drops by up to `pillow_mm` toward the edge, over
    `pillow_width_mm`; this one is part of "height") and wave_mm, wave_m (a soft undulation of that rms size and
    those wavelengths in metres, seamless over the tile). The planes and the undulation go into a second map,
    "face" (`limit`, if given, presses the sum of plane and undulation softly inside plus or minus that, 0.48 at
    most): 0.5 in the joint, 0.5 plus the plane and the undulation on the faces, faded in from half a radius to
    one and a half radii in from the edge, so the arris stays straight and the joint floor stays flat. Painter
    adds it to the height of the profile (a second Height layer; Painter does not clamp the sum above 1), so the
    bevel keeps its full depth and a plane can move a corner by several millimetres. A map that leaves 0..1
    raises ValueError. The map "wave" holds the undulation alone and is not written.
    `halo_mm` (needs `ppm`) adds the map "halo": 1 in the joint and far from it, 0 at the edge of the joint and
    rising back to 1 over about 2.5 times `halo_mm`, a contact shade that darkens the face next to the joint
    and not the joint itself (a Multiply layer; the joint has its own colour).
    """
    yy, xx = np.mgrid[0:size, 0:size]
    bid = np.zeros((size, size), dtype=int)
    sb = np.zeros((size, size))
    side = np.zeros((size, size), dtype=int)
    jl, jr = (joint + 1) // 2, joint // 2
    r_noise = smooth_noise(size, 10.0 * px_scale, seed + 11)
    if radius_detail:
        r_noise = (r_noise + radius_detail * smooth_noise(size, 3.0 * px_scale, seed + 12)) / np.sqrt(
            1.0 + radius_detail ** 2)
    cloud_z = smooth_noise(size, cloud_px / 3.3, seed + 3000)
    rng_c = np.random.default_rng(seed + 5000)
    cloud = np.full((size, size), 0.5)
    for b in blocks:
        w = b["x1"] - b["x0"]
        left = (xx - b["x0"]) % size
        sel = (yy >= b["y0"]) & (yy < b["y1"]) & (left < w)
        parts = [yy - b["y0"] - jl, w - 1 - left - jr, b["y1"] - 1 - yy - jr, left - jl]  # top right bottom left
        d = np.minimum.reduce(parts) + 0.5
        bid[sel] = b["id"]
        sb[sel] = d[sel]
        if edge_gain is not None:
            side[sel] = np.argmin(np.stack(parts), axis=0)[sel]
        dy, dx = (int(v) for v in rng_c.integers(0, size, size=2))
        field = np.roll(cloud_z, (dy, dx), (0, 1))
        cloud[sel] = 0.5 + cloud_std * field[sel]
    ids_sorted = sorted(b["id"] for b in blocks)
    pos = {k: i for i, k in enumerate(ids_sorted)}
    pairs = _neighbour_pairs(bid)
    if tone_amp is None:
        tone_arr, step = assign_tones(len(ids_sorted), [(pos[a], pos[b]) for a, b in pairs],
                                      tone_range[0], tone_range[1], np.random.default_rng(seed + 1000))
        tones = {k: float(tone_arr[pos[k]]) for k in ids_sorted}
    else:
        z, _ = balanced_tones(blocks, pairs, size, np.random.default_rng(seed + 1000), floor=tone_floor)
        tones = {k: 0.5 + tone_amp * z[k] for k in ids_sorted}
        step = min((abs(tones[a] - tones[b]) for a, b in pairs), default=1.0)
    gain_rng = np.random.default_rng(seed + 4000)
    gain = {k: float(gain_rng.uniform(light_gain[0], light_gain[1])) for k in ids_sorted}
    ids = np.zeros((size, size))
    gain_map = np.zeros((size, size))
    for k in ids_sorted:
        ids[bid == k] = tones[k]
        gain_map[bid == k] = gain[k]
    r_eff = np.maximum(1.0, radius * (1.0 + radius_jitter * np.clip(r_noise, -1.5, 1.5)))
    is_joint = sb < 0
    cloud = np.where(is_joint, 0.5, cloud)
    u = np.clip(sb / r_eff, 0.0, 1.0)
    fall = u if profile == "chamfer" else np.sqrt(np.clip(1.0 - (1.0 - u) ** 2, 0.0, 1.0))
    wave_map, face_range, face_map = None, None, None
    if face is None:
        height = np.where(is_joint, 0.0, fall * face_level)
    else:
        if ppm is None:
            raise ValueError("a face relief needs ppm")
        mm = 1000.0 / ppm
        pillow = face.get("pillow_mm", 0.0) / face_depth_mm
        width = max(1.0, face.get("pillow_width_mm", 80.0) / mm)
        level = face_level - pillow * (1.0 - _smoothstep(sb / width))
        fade = _smoothstep((sb - 0.5 * r_eff) / r_eff)
        delta = np.zeros((size, size))
        if face.get("tilt_deg", 0.0):
            delta += face_tilts(blocks, size, ppm, seed, face_depth_mm, face["tilt_deg"],
                                face.get("tilt_cap_mm", 6.0), face.get("tilt_min", 0.3))
        wave_map = np.zeros((size, size))
        if face.get("wave_mm", 0.0):
            wave_map = band_noise(size, ppm, face.get("wave_m", (0.2, 0.6)), seed + 9000) \
                * (face["wave_mm"] / face_depth_mm)
            delta += wave_map
        height = np.where(is_joint, 0.0, fall * level)
        if face.get("limit"):
            delta = soft_limit(delta, face["limit"])
        face_map = np.where(is_joint, 0.5, 0.5 + fade * delta)
        face_range = (float(face_map.min()), float(face_map.max()))
        if face_range[0] < 0.0 or face_range[1] > 1.0:
            raise ValueError("the face relief leaves the range 0..1: %s" % (face_range,))
    if edge_gain is None:
        wear = np.clip(0.8 + 0.25 * smooth_noise(size, 4.0 * px_scale, seed + 7), 0.4, 1.1)
        band = np.exp(-(((sb - 0.6 * r_eff) / (0.25 * r_eff + 1.0)) ** 2))
        edgelight = np.where(is_joint, 0.0, np.clip(band * gain_map * wear, 0.0, 1.0))
    else:
        band = np.exp(-(((sb - 0.5 * r_eff) / (line_px / 1.665)) ** 2))
        edgelight = np.where(is_joint, 0.0, np.clip(band * np.asarray(edge_gain, dtype=float)[side], 0.0, 1.0))
    chip = np.zeros((size, size))
    chips = _chip_plan(blocks, size, joint, radius, chip_frac, chip_px, np.random.default_rng(seed + 2000))
    for c in chips:
        dx = ((xx - c["cx"] + size // 2) % size) - size // 2
        dy = ((yy - c["cy"] + size // 2) % size) - size // 2
        th = np.arctan2(dy, dx)
        reff = c["r"] * (1.0 + 0.3 * np.sin(2 * th + c["phase"][0]) + 0.15 * np.sin(3 * th + c["phase"][1]))
        rho = np.sqrt(dx ** 2 + dy ** 2) / np.maximum(reff, 1.0)
        one = np.clip((1.0 - rho) / 0.35, 0.0, 1.0) * ((bid == c["block"]) & ~is_joint)
        chip = np.maximum(chip, one)
    height = height * (1.0 - chip_depth * chip)
    joint_hard = is_joint.astype(float)
    ao = np.clip(1.0 - ao_strength * _gauss_periodic(joint_hard, ao_sigma), 0.0, 1.0)
    info = {"joint": joint, "radius": radius, "blocks": len(blocks), "chips": [
        {k: v for k, v in c.items() if k != "phase"} for c in chips], "tones": tones,
        "landmarks": landmark_blocks(tones), "min_neighbour_step": step}
    info["face_level"] = face_level
    halo = None
    if halo_mm:
        if ppm is None:
            raise ValueError("a halo needs ppm")
        near = _gauss_periodic(joint_hard, max(0.5, halo_mm * ppm / 1000.0))
        halo = 1.0 - (1.0 - joint_hard) * np.clip(2.0 * near, 0.0, 1.0)
    if face_range is not None:
        info["face_range"] = face_range
    out = {"id": ids, "height": height, "edge": 1.0 - height,
           "joint": np.clip(_gauss_periodic(joint_hard, 0.5), 0.0, 1.0), "edgelight": edgelight,
           "chip": chip, "cloud": np.clip(cloud, 0.0, 1.0), "ao": ao, "stain": _stain(size, seed),
           "info": info}
    if wave_map is not None:
        out["wave"] = wave_map
    if face_map is not None:
        out["face"] = face_map
    if halo is not None:
        out["halo"] = halo
    return out


def scale_blocks(blocks, k):
    """The same layout at `k` times the pixel density: every coordinate times `k`, the ids kept."""
    return [{"x0": b["x0"] * k, "x1": b["x1"] * k, "y0": b["y0"] * k, "y1": b["y1"] * k, "id": b["id"]}
            for b in blocks]


def _noise1d(n, sigma, rng):
    """Smooth 1-D noise of `n` samples with unit std (not periodic: used in block-local coordinates)."""
    z = rng.normal(size=n)
    k = np.exp(-2.0 * (np.pi * sigma) ** 2 * np.fft.fftfreq(n) ** 2)
    a = np.real(np.fft.ifft(np.fft.fft(z) * k))
    return (a - a.mean()) / (a.std() or 1.0)


def weathering(blocks, size, joint, ppm, seed=1, streak_mm=(60.0, 150.0), streak_width_mm=(14.0, 40.0),
               lower_mm=45.0):
    """Directional weathering maps in 0..1, both zero in joints, both seamless in x.

    streak  soft vertical streaks that start under the bed joint at the top of a block and fade out over
            `streak_mm` (a length for each streak), sparse, a different set in each block
    lower   a darker band along the lower edge of each block, `lower_mm` wide, strength varying along the edge
    Each block draws from its own generator (seed and block id) in block-local coordinates, so a layout
    shifted around the wrap gives the same maps shifted.
    """
    yy, xx = np.mgrid[0:size, 0:size]
    jl, jr = (joint + 1) // 2, joint // 2
    px = ppm / 1000.0
    streak = np.zeros((size, size))
    lower = np.zeros((size, size))
    wb = max(1.0, lower_mm * px)
    for b in blocks:
        w, h = b["x1"] - b["x0"], b["y1"] - b["y0"]
        left = (xx - b["x0"]) % size
        u = left - jl
        v = yy - b["y0"] - jl
        face = (yy >= b["y0"]) & (yy < b["y1"]) & (left < w) & (u >= 0) & (left <= w - 1 - jr) & (v >= 0) \
            & (yy <= b["y1"] - 1 - jr)
        rng = np.random.default_rng([seed, int(b["id"])])
        width_px = float(rng.uniform(streak_width_mm[0], streak_width_mm[1])) * px
        amp = np.clip((_noise1d(w, max(1.0, width_px / 2.0), rng) - 0.5) / 0.8, 0.0, 1.0)
        length = (streak_mm[0] + (streak_mm[1] - streak_mm[0])
                  * np.clip(0.5 + 0.35 * _noise1d(w, max(2.0, 4.0 * width_px), rng), 0.0, 1.0)) * px
        gain = float(rng.uniform(0.55, 1.0))
        along = np.clip(0.7 + 0.3 * _noise1d(w, max(2.0, 3.0 * width_px), rng), 0.3, 1.0)
        ui = np.clip(left, 0, w - 1)
        fade = np.clip(1.0 - v / length[ui], 0.0, 1.0) ** 1.5
        streak[face] = (gain * amp[ui] * fade)[face]
        dist = (b["y1"] - 1 - jr) - yy
        lower[face] = ((np.clip(1.0 - dist / wb, 0.0, 1.0) ** 1.5) * along[ui])[face]
    return {"streak": np.clip(streak, 0.0, 1.0), "lower": np.clip(lower, 0.0, 1.0)}


def make_layout(ppm, size=1024, courses_m=(0.44,), lengths_m=(0.8, 1.4), joint_mm=11, radius_mm=17, seed=1,
                stagger_m=0.15, courses_px=None, length_step=16, blocks=None, weather=None, **render_kw):
    """A whole layout for a tile of `size` px at `ppm` px per metre (461: 2.222 m of wall, 307: 3.333 m).
    Courses come from the pattern `courses_m` or the list `courses_px`; block lengths from `lengths_m`;
    joint, round, chips and cloud keep their world size. Returns blocks, maps and an info dict.
    `blocks` (for example scale_blocks of an earlier layout) skips the planning and keeps that layout.
    `weather` (a dict of weathering() options, {} for the defaults) adds the maps streak and lower."""
    if blocks is not None:
        rows = sorted({(b["y0"], b["y1"]) for b in blocks})
        heights = [y1 - y0 for y0, y1 in rows]
    else:
        heights = list(courses_px) if courses_px else course_heights(size, ppm, courses_m)
        lengths = length_choices(ppm, lengths_m[0], lengths_m[1], length_step)
        blocks = plan_blocks(heights, lengths, size, seed, int(round(stagger_m * ppm)))
    joint = max(1, int(round(joint_mm * ppm / 1000.0)))
    radius = max(1, int(round(radius_mm * ppm / 1000.0)))
    kw = dict(chip_px=(scaled_px(3, ppm), scaled_px(8, ppm)), cloud_px=60.0 * ppm / REF_PPM,
              ao_sigma=5.0 * ppm / REF_PPM, ppm=ppm)
    kw.update(render_kw)
    maps = render_v2(blocks, size, joint=joint, radius=radius, seed=seed, **kw)
    if weather is not None:
        maps.update(weathering(blocks, size, joint, ppm, seed, **weather))
    info = {"ppm": ppm, "size": size, "tile_m": size / float(ppm), "courses_px": heights,
            "joint_px": joint, "radius_px": radius, "joint_mm": joint * 1000.0 / ppm,
            "radius_mm": radius * 1000.0 / ppm, "seed": seed, "lengths_m": list(lengths_m),
            "stagger_px": int(round(stagger_m * ppm)), "blocks": len(blocks)}
    maps["info"].update(info)
    return blocks, maps, info


def to_u8(a):
    return np.clip(np.rint(np.asarray(a) * 255.0), 0, 255).astype(np.uint8)


def to_u16(a):
    return np.clip(np.rint(np.asarray(a) * 65535.0), 0, 65535).astype(np.uint16)


def write(out, maps, blocks):
    """Write every map (height as 16 bit, the others as 8 bit) and layout.json (blocks, files, info)."""
    os.makedirs(out, exist_ok=True)
    files = {}
    for key, arr in maps.items():
        if key in ("info", "wave"):
            continue
        files[key] = os.path.join(out, "layout_%s.png" % key)
        if key in ("height", "face"):
            Image.fromarray(to_u16(arr)).save(files[key])
        else:
            Image.fromarray(to_u8(arr), "L").save(files[key])
    json.dump({"blocks": blocks, "files": files, "info": maps.get("info")},
              open(os.path.join(out, "layout.json"), "w"), indent=1)
    return files


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--heights", help="course heights in px, version 1")
    ap.add_argument("--lengths", help="block lengths in px, version 1")
    ap.add_argument("--size", type=int, default=1024)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--stagger", type=int, default=64)
    ap.add_argument("--joint", type=int, default=2)
    ap.add_argument("--arris", type=int, default=2)
    ap.add_argument("--v2", action="store_true", help="version 2: rounded edges, clouds, chips, tones")
    ap.add_argument("--ppm", type=float, default=REF_PPM)
    ap.add_argument("--courses-m", default="0.44", help="course heights in metres, a repeating pattern")
    ap.add_argument("--lengths-m", default="0.8,1.4", help="shortest and longest block in metres")
    ap.add_argument("--joint-mm", type=float, default=11.0)
    ap.add_argument("--radius-mm", type=float, default=17.0)
    ap.add_argument("--stagger-m", type=float, default=0.15)
    a = ap.parse_args(argv)
    if a.v2:
        blocks, maps, info = make_layout(
            a.ppm, a.size, tuple(float(v) for v in a.courses_m.split(",")),
            tuple(float(v) for v in a.lengths_m.split(",")), a.joint_mm, a.radius_mm, a.seed, a.stagger_m)
        files = write(a.out, maps, blocks)
        print(json.dumps({"blocks": len(blocks), "info": info, "files": files}, indent=1))
        return
    blocks = plan_blocks([int(v) for v in a.heights.split(",")], [int(v) for v in a.lengths.split(",")],
                         a.size, a.seed, a.stagger)
    files = write(a.out, render(blocks, a.size, a.joint, a.arris, a.seed), blocks)
    print(json.dumps({"blocks": len(blocks), "files": files}, indent=1))


if __name__ == "__main__":
    main()
