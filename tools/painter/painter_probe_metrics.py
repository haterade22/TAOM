"""Host-side helpers to compare exported Painter maps (used by the painter probes).

laplacian_variance(a): variance of the 4-neighbour Laplacian of the interior, a sharpness number.
crop_uv(a, u0, v0, u1, v1): the region between two UV fractions, so that two sizes show the same area.
box_down(a, k): mean of k by k blocks, to bring a larger export to the scale of a smaller one.
distinct_levels(a, row, c0, c1): how many different values lie on one row segment (a stair-step test).
run_lengths(line): lengths of the runs of equal values along a line.
"""
import numpy as np


def laplacian_variance(a):
    a = np.asarray(a, dtype=np.float64)
    lap = (a[:-2, 1:-1] + a[2:, 1:-1] + a[1:-1, :-2] + a[1:-1, 2:]) - 4.0 * a[1:-1, 1:-1]
    return float(lap.var())


def crop_uv(a, u0, v0, u1, v1):
    h, w = a.shape[:2]
    return a[int(round(v0 * h)):int(round(v1 * h)), int(round(u0 * w)):int(round(u1 * w))]


def box_down(a, k):
    k = int(k)
    if k == 1:
        return a
    h, w = a.shape[:2]
    if h % k or w % k:
        raise ValueError("size %dx%d is not a multiple of %d" % (w, h, k))
    shape = (h // k, k, w // k, k) + a.shape[2:]
    return a.reshape(shape).mean(axis=(1, 3))


def distinct_levels(a, row, c0, c1):
    return int(len(np.unique(np.asarray(a)[row, c0:c1])))


def run_lengths(line):
    line = np.asarray(line)
    if line.size == 0:
        return []
    cut = np.flatnonzero(line[1:] != line[:-1]) + 1
    edges = np.concatenate(([0], cut, [line.size]))
    return [int(n) for n in np.diff(edges)]


def step_profile(size, base, steps):
    """One row of heights: base plus a smoothstep rise of amp over width columns from col0, per step."""
    c = np.arange(size, dtype=np.float64)
    row = np.full(size, float(base))
    for col0, width, amp in steps:
        t = np.clip((c - col0) / float(width), 0.0, 1.0)
        row += amp * t * t * (3.0 - 2.0 * t)
    return row


def step_image_16(size, base, steps):
    """A size by size 16-bit image whose columns follow step_profile (a test height map)."""
    row = np.rint(step_profile(size, base, steps) * 65535.0).astype(np.uint16)
    return np.tile(row, (size, 1))


def fwhm(line):
    """Full width at half the extreme of a dip or bump against the median of the line, in pixels."""
    v = np.asarray(line, dtype=np.float64)
    base = float(np.median(v))
    d = v - base
    peak = d[np.argmax(np.abs(d))]
    if peak == 0:
        return 0.0
    d = d / peak
    above = d >= 0.5
    if not above.any():
        return 0.0
    idx = np.flatnonzero(above)
    i0, i1 = idx[0], idx[-1]
    left = float(i0)
    if i0 > 0:
        left = i0 - (d[i0] - 0.5) / (d[i0] - d[i0 - 1])
    right = float(i1)
    if i1 < len(d) - 1:
        right = i1 + (d[i1] - 0.5) / (d[i1] - d[i1 + 1])
    return float(right - left)


def mirror_asymmetry(line, reach):
    """Largest difference between mirror pairs about the wrap of a periodic line: (n-1-i) against i."""
    v = np.asarray(line, dtype=np.float64)
    n = len(v)
    return float(max(abs(v[(n - 1 - i) % n] - v[i]) for i in range(reach)))
