"""Pure helpers for the plinth stone of the Rivendell wall (round 2b, the plinth).

The plinth runs from Z 0 to Z 3.0 m on the wall. One tile covers 2.222 m and V 0 is at Z 0, which is the
bottom row of the image. A bed joint has to sit at Z 3.0 m, under the string course, so the finished maps
are rolled by whole rows until a joint lies on that row. A damp band at the foot of the tile is a map that
wraps in both axes, so it is a smooth bump around one row.
"""
import numpy as np


def damp_map(size, centre=0.0, half_width=0.2):
    """A mask 0..1, `size` by `size`, constant along x. A raised cosine around the row `centre * size`
    (a fraction of the tile height), zero further than `half_width` (a fraction) away, wrapping in y."""
    y = np.arange(size) / float(size)
    d = np.abs(y - centre)
    d = np.minimum(d, 1.0 - d)
    col = np.where(d < half_width, 0.5 * (1.0 + np.cos(np.pi * d / half_width)), 0.0)
    return np.tile(col[:, None], (1, size))


def joint_rows(img, min_gap=30, share=0.5):
    """The centre rows of the dark horizontal bands (the bed joints) of a 2D image, wrapping in y.
    A row is dark when the mean of its pixels lies under the median of the rows by `share` of the deepest
    dip. Bands closer than `min_gap` rows merge into the deeper one."""
    prof = np.asarray(img, dtype=float)
    if prof.ndim == 3:
        prof = prof.mean(2)
    prof = prof.mean(axis=1)
    n = len(prof)
    dip = np.median(prof) - prof
    if dip.max() <= 0:
        return []
    dark = dip >= share * dip.max()
    if dark.all():
        return []
    runs = []
    for i in range(n):
        if dark[i] and not dark[i - 1]:
            k = 0
            while dark[(i + k) % n]:
                k += 1
            runs.append(((i + (k - 1) / 2.0) % n, float(dip[[(i + j) % n for j in range(k)]].mean())))
    rows = []
    for r, _ in sorted(runs, key=lambda t: -t[1]):
        if all(min(abs(r - q), n - abs(r - q)) >= min_gap for q in rows):
            rows.append(r)
    return [int(round(r)) % n for r in rows]


def shift_for_joint(rows, size, want_row):
    """The smallest row shift (signed) that moves one of `rows` onto `want_row`, wrapping."""
    best = None
    for r in rows:
        s = (want_row - r) % size
        if s > size // 2:
            s -= size
        if best is None or abs(s) < abs(best):
            best = s
    return best


def roll_rows(a, shift):
    """The array turned by `shift` rows (down for a positive shift), wrapping."""
    return np.roll(np.asarray(a), shift, axis=0)


def row_for_height(z_m, tile_m, size):
    """The image row (from the top) of the wall height `z_m`, when V 0 at Z 0 is the bottom row."""
    frac = (z_m % tile_m) / tile_m
    return int(round((1.0 - frac) * size))


def slant_length(rise, run):
    """The length along a battered face that rises `rise` m while it leans back `run` m. V runs along this
    length when the face is unwrapped flat, so it is the height to give row_for_height."""
    return float(np.hypot(rise, run))


def joint_at_row(blocks, row, size):
    """True when `row` is a bed joint of a block layout: a course starts there (the tile wraps)."""
    return any(b["y0"] % size == row % size for b in blocks)


def course_heights_below(top_row, courses_px, size, tile_m):
    """The course heights in metres, from the top of the plinth down to the ground, for a tile whose courses
    are `courses_px` (top course first) and whose rows start a course at `top_row`. The face runs down from
    `top_row` to the bottom of the image, wraps to row 0 and runs down one more tile, so the course that
    starts at `top_row` shows twice (at the top and at the ground) and every other course once."""
    starts, y = [], 0
    for h in courses_px:
        starts.append(y)
        y += h
    if top_row % size not in starts:
        raise ValueError("no course starts at row %d" % top_row)
    k = starts.index(top_row % size)
    order = [(k + i) % len(courses_px) for i in range(len(courses_px) + 1)]
    return [courses_px[i] * tile_m / size for i in order]
