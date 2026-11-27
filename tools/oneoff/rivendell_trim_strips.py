"""The five strips of the Rivendell trim sheet as height functions, pure Python (no bpy).
Tests: tools/tests/test_rivendell_trim_strips.py.

The sheet is one 1024 px texture of five horizontal strips. A strip tiles in U and is mapped across the
width of a carved member. The V ranges come from rivendell_joint_profile.STRIPS, because the built
meshes use them. The sheet stands for TILE_M metres in U and the same TILE_M metres in V.

height(name, u, s) is the height in metres above the low-poly plane (Z = 0) of strip `name` at U = u
(a fraction of the tile, period 1) and at s metres above the bottom of the strip. Rules:
  - Every function is exactly periodic in U.
  - The first and last MARGIN metres (8 px) of a strip are flat at Z_BASE, the lowest face of the strip.
  - A joint is a V groove that cuts below Z_BASE, but the lowest point stays above the plane.
  - No height is above Z_LIMIT.
The Blender builder (blender_build_trim_highpoly.py) meshes these functions on the grids that
u_lines() and s_lines() give. Nobody types these numbers twice.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rivendell_joint_profile as jp  # noqa: E402

STRIPS = jp.STRIPS
SIZE = 1024
TILE_M = 2.222
MARGIN_PX = 8
MARGIN = MARGIN_PX * TILE_M / SIZE
MM = 0.001
Z_BASE = 0.006  # the face of a plain strip; a joint floor is Z_BASE - JOINT_DEPTH = 0.5 mm over the plane
Z_LIMIT = 0.025
# The wide joint (S1, S2, S3 beads, S5) has the section of the wall stone_d joint (joint.json of 2026-11-25): a flat
# floor 13 mm wide and a round-over of 13 mm on each side. The height above the floor at t of the round (0 at the
# floor edge, 1 at the face) is sqrt(1 - (1 - t)^2) of the depth: vertical at the floor, tangent to the face. The
# wall's depth of 22 mm does not fit the 25 mm relief of the sheet, so the depth stays 5.5 mm (the floor 0.5 mm over
# the plane) and the round is as wide as on the wall.
JOINT_FLOOR = 0.013
JOINT_ROUND = 0.013
JOINT_DEPTH = 0.0055
JOINT_TAPER = 0.006
JOINT_MASK_SOFT = 0.001  # the colour mask of a joint falls from 1 to 0 over this width (about one pixel at 2048)
# The joint of S4: the stones of an arch, six to a tile, 8.4 mm wide at half depth.
S4_JOINTS = 6
S4_JOINT_FLOOR = 0.0075
S4_JOINT_ROUND = 0.0035
S4_JOINT_DEPTH = 0.0045
ROUND_T = (0.015, 0.04, 0.08, 0.14, 0.22, 0.33, 0.47, 0.65, 0.85, 1.0)  # mesh lines across the round, steep first
CUSHION = 0.0015
TILES = 3  # the high-poly spans one tile past each side of the plane

# S3, the interlace
S3_PERIODS = 4
S3_AMPLITUDE = 0.092
S3_HALF_WIDTH = 0.0225
S3_Z_MID = 0.0085
S3_Z_AMP = 0.0035
S3_BEAD_CENTRE = 0.020
S3_BEAD_HALF = 0.014
S3_BEAD_PEAK = 0.009
S3_BEAD_EDGE_SLOPE = 0.9  # rise over run at the foot of the bead (42 degrees); round 2b had 3.3 (73 degrees)

# S5, the blind arcade
S5_ARCHES = 8
S5_FACE = 0.015
S5_ARCH_WIDTH = 0.190
S5_ARCH_K = 1.2
S5_RISE = 0.080
S5_STEP1, S5_LEVEL1 = 0.006, 0.009
S5_INSET, S5_STEP2, S5_LEVEL2 = 0.014, 0.005, 0.004
S5_FACE_START_MM = 62.0
S5_SHAFT_HALF, S5_SHAFT_PEAK = 0.010, 0.0030
S5_SILL = MARGIN + (S5_FACE_START_MM + 22.0) * MM

# the V sections, as (kind, width in mm, height or base in mm, top in mm); widths are scaled to the strip.
# Z_BASE rose by 3 mm for the deeper joint, so the tops of the two mouldings dropped by 0.5 and 1.5 mm.
S2_SEGS = [("flat", 26, 0), ("cove", 72, 0, 8), ("flat", 12, 8), ("torus", 96, 8, 18.5), ("flat", 12, 8),
           ("ovolo", 52, 8, 15), ("flat", 38, 15), ("cove", 50, 15, 0), ("flat", 74, 0)]
S4_SEGS = [("flat", 14, 0), ("cove", 26, 0, 8), ("flat", 10, 8), ("torus", 72, 8, 18.5), ("flat", 10, 8),
           ("cove", 26, 8, 0), ("flat", 14, 0)]

GRID = {  # (du, dv) in metres; S3 also has finer dv inside the band and the beads, see s_lines()
    "S1": (0.02, 0.02), "S2": (0.1, 0.004), "S3": (0.0044, 0.012), "S4": (0.1, 0.003), "S5": (0.0044, 0.0044),
}
S3_DV_BAND = 0.0033
S3_DV_BEAD = 0.0015


# ---- rows and bounds -------------------------------------------------------------------------------
def strip_rows():
    """Image rows (top, bottom) of each strip; row 0 is the top of the image (V = 1)."""
    return {n: (int(round((1.0 - v1) * SIZE)), int(round((1.0 - v0) * SIZE))) for n, (v0, v1) in STRIPS.items()}


def strip_bounds_m(name):
    """(y0, y1) of the strip on the sheet in metres, with Y = V * TILE_M."""
    v0, v1 = STRIPS[name]
    return v0 * TILE_M, v1 * TILE_M


def strip_height_m(name):
    y0, y1 = strip_bounds_m(name)
    return y1 - y0


def joint_positions(name):
    """U of each joint of a strip: every 1.11 m on S1, S2, S5 and in the two beads of S3; six to a tile on S4."""
    if name in ("S1", "S2", "S3", "S5"):
        return (0.0, 0.5)
    if name == "S4":
        return tuple(k / float(S4_JOINTS) for k in range(S4_JOINTS))
    return ()


def joint_spec(name):
    """(floor width, depth, round) in metres of the joints of a strip."""
    if name == "S4":
        return S4_JOINT_FLOOR, S4_JOINT_DEPTH, S4_JOINT_ROUND
    return JOINT_FLOOR, JOINT_DEPTH, JOINT_ROUND


# ---- shapes ----------------------------------------------------------------------------------------
_K = 0.96


def _clamp01(t):
    return 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)


def _smoothstep(a, b, x):
    t = _clamp01((x - a) / (b - a))
    return t * t * (3.0 - 2.0 * t)


def _cove01(t):
    """A hollow: flat at t = 0, rising steeply to 1 at t = 1."""
    t = _clamp01(t)
    return (1.0 - math.sqrt(1.0 - (_K * t) ** 2)) / (1.0 - math.sqrt(1.0 - _K * _K))


def _ovolo01(t):
    """A quarter round: steep at t = 0, flat at t = 1."""
    return 1.0 - _cove01(1.0 - t)


def _torus01(t):
    """A bead: 0 at both ends, 1 in the middle, steep at the ends."""
    r = 2.0 * _clamp01(t) - 1.0
    k = 0.97
    e = math.sqrt(1.0 - k * k)
    return (math.sqrt(1.0 - (k * r) ** 2) - e) / (1.0 - e)


def _wrap(u, c=0.0):
    """Signed distance of u from c on the circle of period 1, in [-0.5, 0.5)."""
    return (u - c + 0.5) % 1.0 - 0.5


def _segment(seg, t):
    kind = seg[0]
    if kind == "flat":
        return seg[2] * MM
    h0, h1 = seg[2], seg[3]
    if kind == "ramp":
        return (h0 + (h1 - h0) * t) * MM
    if kind == "torus":
        return (h0 + (h1 - h0) * _torus01(t)) * MM
    if kind == "cove":
        g = _cove01(t) if h1 >= h0 else _cove01(1.0 - t)
    elif kind == "ovolo":
        g = _ovolo01(t) if h1 >= h0 else _ovolo01(1.0 - t)
    else:
        raise KeyError(kind)
    lo, hi = min(h0, h1), max(h0, h1)
    return (lo + (hi - lo) * g) * MM


def _section(segs, x, inner):
    """Height in metres of a V section at x metres from the start of the inner span (length `inner`)."""
    total = sum(s[1] for s in segs)
    xm = _clamp01(x / inner) * total
    acc = 0.0
    for i, seg in enumerate(segs):
        if xm <= acc + seg[1] or i == len(segs) - 1:
            return _segment(seg, _clamp01((xm - acc) / seg[1]))
        acc += seg[1]
    return 0.0


def _section_breaks(segs, inner):
    total = sum(s[1] for s in segs)
    out, acc = [], 0.0
    for seg in segs:
        acc += seg[1]
        out.append(inner * acc / total)
    return out


def _inner(name):
    return strip_height_m(name) - 2.0 * MARGIN


def _across(d, floor, rnd):
    """Share of the full depth of a joint at distance d from its centre: 1 on the floor, then the round-over of
    the wall stone (1 - sqrt(1 - (1 - t)^2) at t of the round), 0 beyond it."""
    t = (abs(d) - floor / 2.0) / rnd
    if t <= 0.0:
        return 1.0
    if t >= 1.0:
        return 0.0
    return 1.0 - math.sqrt(1.0 - (1.0 - t) ** 2)


def _nearest_joint(name, u):
    """Distance in metres from U = u to the nearest joint of the strip, or None when it has none."""
    pos = joint_positions(name)
    if not pos:
        return None
    return min(abs(_wrap(u, j)) for j in pos) * TILE_M


def _joint_share(name, u):
    """Share of the full depth of the nearest joint of a strip at U = u."""
    d = _nearest_joint(name, u)
    if d is None:
        return 0.0
    floor, _, rnd = joint_spec(name)
    return _across(d, floor, rnd)


def _joint_mask(name, u, widen=0.0):
    """1 on the floor of the nearest joint, 0 off it, soft over JOINT_MASK_SOFT: the shape of the wall's joint
    colour. `widen` (metres) adds to the floor width."""
    d = _nearest_joint(name, u)
    if d is None:
        return 0.0
    half = (joint_spec(name)[0] + widen) / 2.0
    return 1.0 - _smoothstep(half - JOINT_MASK_SOFT / 2.0, half + JOINT_MASK_SOFT / 2.0, d)


def _taper(x, inner):
    """A joint ends in a smooth ramp inside the flat margin."""
    return _smoothstep(0.0, JOINT_TAPER, x) * _smoothstep(0.0, JOINT_TAPER, inner - x)


def _groove(name, u, x, inner):
    """Depth of the joint grooves of strip `name` at U = u and x metres into the inner span."""
    return joint_spec(name)[1] * _taper(x, inner) * _joint_share(name, u)


def joint_weight(name, u, s, widen=0.0):
    """0 to 1: where the floor of a joint is, for the texture (the joint colour and roughness of the trim sheet),
    as on the wall stone: the floor only, not the round-over. The same on every row of the strip; on S3 only in
    the two beads. `widen` is added to the floor width in metres."""
    top = strip_height_m(name)
    if s <= MARGIN or s >= top - MARGIN:
        return 0.0
    inner = top - 2.0 * MARGIN
    x = s - MARGIN
    w = _taper(x, inner) * _joint_mask(name, u, widen)
    if name == "S3":
        w *= max(_bead_window(x, S3_BEAD_CENTRE), _bead_window(x, inner - S3_BEAD_CENTRE))
    return w


# ---- S3, the interlace -----------------------------------------------------------------------------
def _theta(k, u):
    return 2.0 * math.pi * (S3_PERIODS * u + k / 3.0)


def strand_y(k, u):
    """Lateral place of strand k at U = u, from -1 to 1 (times S3_AMPLITUDE across the band)."""
    return math.sin(_theta(k, u))


def strand_top(k, u):
    """Height of strand k above the base at its centre line. Over at 12 mm, under at 5 mm. The sign
    makes every crossing have one strand over the other, and each strand alternate over and under."""
    return S3_Z_MID - S3_Z_AMP * math.sin(2.0 * _theta(k, u))


def _bead01(t):
    """The profile of a bead from its foot (t = 0) to its crest (t = 1): a cubic with the slope S3_BEAD_EDGE_SLOPE
    at the foot and a level top. A torus (round 2b) stood 73 degrees steep at the foot, one pixel of cliff."""
    c = S3_BEAD_EDGE_SLOPE * S3_BEAD_HALF / S3_BEAD_PEAK
    a = c - 2.0
    b = 1.0 - c - a
    return ((a * t + b) * t + c) * t


def _bead_window(x, c):
    """1 inside the bead centred at c (x metres into the inner span), falling to 0 over 2 mm at its foot."""
    return _smoothstep(0.0, 0.002, S3_BEAD_HALF - abs(x - c))


def _bead(x, c, half, peak, u=0.0):
    """Height of a bead at x; the joint of the strip cuts it down to the field, never below."""
    d = x - c
    if abs(d) >= half:
        return 0.0
    h = peak * _bead01((half - abs(d)) / half)
    cut = JOINT_DEPTH * _joint_share("S3", u) * _smoothstep(0.0, 0.004, half - abs(d))
    return max(0.0, h - cut)


def _s3(u, x, inner):
    h = max(_bead(x, S3_BEAD_CENTRE, S3_BEAD_HALF, S3_BEAD_PEAK, u),
            _bead(x, inner - S3_BEAD_CENTRE, S3_BEAD_HALF, S3_BEAD_PEAK, u))
    s = x + MARGIN - strip_height_m("S3") / 2.0  # across the band from its centre line
    for k in range(3):
        th = _theta(k, u)
        slope = S3_AMPLITUDE * 2.0 * math.pi * S3_PERIODS / TILE_M * math.cos(th)
        d = abs(s - S3_AMPLITUDE * math.sin(th)) / math.sqrt(1.0 + slope * slope)
        if d < S3_HALF_WIDTH:
            p = (1.0 - (d / S3_HALF_WIDTH) ** 2) ** 0.65
            h = max(h, strand_top(k, u) * p)
    return h


# ---- S5, the blind arcade --------------------------------------------------------------------------
def arch_rise():
    """Height of the pointed head above the springing line."""
    w = S5_ARCH_WIDTH
    return math.sqrt(S5_ARCH_K * w * w - w * w / 4.0)


def arch_sd(x, y):
    """Signed distance to the niche (negative inside): x from the arch axis, y as s on the strip."""
    w = S5_ARCH_WIDTH
    y_s = S5_SILL + S5_RISE
    if y < y_s:
        return max(abs(x) - w / 2.0, S5_SILL - y)
    r = S5_ARCH_K * w
    c = r - w / 2.0
    return max(math.hypot(x - c, y - y_s) - r, math.hypot(x + c, y - y_s) - r)


def _arris(sd, width, hi, lo):
    if sd >= 0.0:
        return hi
    if sd <= -width:
        return lo
    return lo + (hi - lo) * _ovolo01(1.0 + sd / width)


def _s5_segs(inner):
    inner_mm = inner / MM
    tail = 24 + 16
    return [("flat", 6, 0), ("torus", 26, 0, 8), ("flat", 8, 0), ("ovolo", 22, 0, S5_FACE / MM),
            ("flat", inner_mm - (62 + tail), S5_FACE / MM), ("ovolo", 24, S5_FACE / MM, 0), ("flat", 16, 0)]


def _s5(u, x, inner):
    h = _section(_s5_segs(inner), x, inner)
    p = 1.0 / S5_ARCHES
    xl = ((u % p) / p - 0.5) * p * TILE_M
    sd = arch_sd(xl, x + MARGIN)
    if sd + S5_INSET < 0.0:
        n = _arris(sd + S5_INSET, S5_STEP2, S5_LEVEL1 * 1.0, S5_LEVEL2)
    else:
        n = _arris(sd, S5_STEP1, S5_FACE, S5_LEVEL1)
    h = min(h, n)
    # a slender shaft in the middle of each pier, from above the sill to the shoulder of the arch
    y0, y1 = S5_SILL + 0.010, S5_SILL + S5_RISE + 0.5 * arch_rise()
    y = x + MARGIN
    dy = max(y0 - y, 0.0, y - y1)
    d = math.hypot(p * TILE_M / 2.0 - abs(xl), dy)
    if d < S5_SHAFT_HALF and x > S5_FACE_START_MM * MM:
        h = max(h, S5_FACE + S5_SHAFT_PEAK * _torus01(0.5 + 0.5 * d / S5_SHAFT_HALF))
    return h


# ---- the height function ---------------------------------------------------------------------------
def height(name, u, s):
    """Height in metres above the low-poly plane. See the module text."""
    top = strip_height_m(name)
    if s <= MARGIN + 1e-12 or s >= top - MARGIN - 1e-12:
        return Z_BASE
    inner = top - 2.0 * MARGIN
    x = s - MARGIN
    if name == "S1":
        a = (2.0 * u) % 1.0
        b = x / inner
        c = (1.0 - abs(2.0 * a - 1.0) ** 3) * (1.0 - abs(2.0 * b - 1.0) ** 3)
        return Z_BASE + CUSHION * c - _groove("S1", u, x, inner)
    if name == "S2":
        return Z_BASE + _section(S2_SEGS, x, inner) - _groove("S2", u, x, inner)
    if name == "S3":
        return Z_BASE + _s3(u % 1.0, x, inner)
    if name == "S4":
        return Z_BASE + _section(S4_SEGS, x, inner) - _groove("S4", u, x, inner)
    if name == "S5":
        return Z_BASE + _s5(u % 1.0, x, inner) - _groove("S5", u, x, inner)
    raise KeyError(name)


# ---- the grids of the mesh -------------------------------------------------------------------------
def _merge(values, lo, hi, tol=1e-7):
    out = []
    for v in sorted(set(min(max(v, lo), hi) for v in values)):
        if not out or v - out[-1] > tol:
            out.append(v)
    return out


def joint_lines(name):
    """The mesh lines of the joints of a strip, as offsets in metres from each joint centre: the floor edge, the
    lines of ROUND_T across the round-over (close together where it is steep), the end of the round, and two flat
    lines outside it that keep the curve out of the smoothed normals of the face."""
    floor, _, rnd = joint_spec(name)
    f = floor / 2.0
    side = [f] + [f + t * rnd for t in ROUND_T] + [f + rnd + 0.0015, f + rnd + 0.004]
    return [0.0] + side + [-v for v in side]


def u_lines(name):
    """Mesh lines along U in [0, 1]: a uniform step, the joint lines and the tile ends. The uniform lines that fall
    inside a joint's zone are left out, so that no thin sliver of a quad sits beside a joint line."""
    n = int(math.ceil(TILE_M / GRID[name][0]))
    pos = joint_positions(name)
    offsets = joint_lines(name) if pos else []
    reach = (max(offsets) if offsets else 0.0) / TILE_M
    lines = [k / n for k in range(n + 1) if not any(abs(_wrap(k / n, j)) < reach for j in pos)]
    for j in pos:
        lines += [(j + d / TILE_M) % 1.0 for d in offsets]
    lines += [0.0, 1.0]
    return _merge(lines, 0.0, 1.0)


def s_lines(name):
    """Mesh lines along s (metres from the strip bottom): uniform steps, the section breaks and the margins."""
    top = strip_height_m(name)
    inner = top - 2.0 * MARGIN
    dv = GRID[name][1]
    lines = [0.0, top, MARGIN, top - MARGIN]
    if name == "S3":
        band = S3_AMPLITUDE + S3_HALF_WIDTH + 0.003
        lo, hi = top / 2.0 - band, top / 2.0 + band
        n =int(math.ceil(top / dv))
        lines += [k * top / n for k in range(n + 1) if not lo < k * top / n < hi]
        m = int(math.ceil((hi - lo) / S3_DV_BAND))
        lines += [lo + k * (hi - lo) / m for k in range(m + 1)]
        for c in (S3_BEAD_CENTRE, inner - S3_BEAD_CENTRE):
            lines += [MARGIN + c + d for d in (-S3_BEAD_HALF, 0.0, S3_BEAD_HALF)]
        m = int(math.ceil(2.0 * S3_BEAD_HALF / S3_DV_BEAD))
        for c in (S3_BEAD_CENTRE, inner - S3_BEAD_CENTRE):
            lines += [MARGIN + c - S3_BEAD_HALF + k * 2.0 * S3_BEAD_HALF / m for k in range(m + 1)]
    else:
        n = int(math.ceil(top / dv))
        lines += [k * top / n for k in range(n + 1)]
    if name in ("S2", "S4"):
        segs = S2_SEGS if name == "S2" else S4_SEGS
        lines += [MARGIN + b for b in _section_breaks(segs, inner)]
    if name == "S5":
        lines += [MARGIN + b for b in _section_breaks(_s5_segs(inner), inner)]
        y_s = S5_SILL + S5_RISE
        lines += [S5_SILL, y_s, y_s + arch_rise()]
    if joint_positions(name):
        lines += [MARGIN + JOINT_TAPER, top - MARGIN - JOINT_TAPER]
    return _merge(lines, 0.0, top)


def triangle_count():
    """Triangles of all five high-poly strips over TILES tiles, with quads split in two."""
    total = 0
    for name in STRIPS:
        total += 2 * (TILES * (len(u_lines(name)) - 1)) * (len(s_lines(name)) - 1)
    return total
