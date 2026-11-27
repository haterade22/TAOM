"""The breach of the Rivendell broken wall, as plain numbers (no bpy). Tests:
tools/tests/test_rivendell_breach_profile.py.

Used by tools/oneoff/blender_build_rivendell_wall_broken.py. Frame as tools/oneoff/rivendell_joint_profile.py:
a piece runs along X from -10 to +10, the outer face is +Y, Z is up, Z = 0 is the ground line.

How the wall breaks (version 4). The wall is two skins of dressed ashlar with a rubble core between them.
  skins    the outer layer (Y 1.95 to 2.5, with the piers, ribs and arch frames) and the inner layer. At each side
           of the gap a skin ends in a toothed edge of whole blocks. Every step sits on a joint that the stone
           texture draws: a bed joint in Z, a head joint in X (LAYOUT, bed_joints, head_joints). A step is
           0.25 to 1.4 m long, out (a missing block) or in (a block that hangs over a missing one), so the edge is
           no regular stair. The parapet and the rail end in teeth of their own, set back from the deck edge, and
           one side of the outer parapet loses the whole panel next to the gap.
  core     the rubble between the skins, set back 0.1 to 0.5 m behind the teeth, a lumpy surface (a grid of
           points moved by a smooth noise) that leans back toward the standing wall.
Every edge is a list of (e, z) points, e being |x| where the wall starts at height z.

The heap is a mound as a height field (Heap): a plateau across the gap and a toe 7 m past the wall face. The heap
texture has no courses, so the faces take area_uv: an exact density TD on every face, however it leans.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rivendell_joint_profile as jp  # noqa: E402

TD = jp.TD

# ---- the stone texture: where its joints lie -------------------------------------------------------
# t_rivendell_wall_stone_a is 2048 px in the layout (textures/v07/layout_a): four courses of 576, 448, 576 and 448
# rows (counted from the top of the picture) and two blocks in each. V is 1 at the top of the picture. The mortar
# line of a bed joint lies below the course boundary in the picture, so its centre is MORTAR_V under the
# boundary in V. A head joint lies on the block boundary.
X_LOW = -10.0
LAYOUT_PX = 2048
COURSE_ROWS = ((0, 576), (576, 1024), (1024, 1600), (1600, 2048))
HEAD_PX = ((850, 1970), (130, 1154), (930, 1794), (616, 1448))   # the block boundaries of each course
MORTAR_V = 8.0 / LAYOUT_PX
TILE = 1.0 / TD
# V (0 to 1) of the bed joints, lowest first; course c lies between the joint below and the joint above it
BED_V = tuple(sorted((1.0 - r0 / LAYOUT_PX - MORTAR_V) % 1.0 for r0, _ in COURSE_ROWS))
PLINTH_COURSE = 1.0 / (3.0 * TD)       # the plinth texture has three courses to a tile


def course_height(course):
    r0, r1 = COURSE_ROWS[course]
    return (r1 - r0) / LAYOUT_PX * TILE


def course_at(z):
    """The course (0 to 3) of the stone texture that height z (metres) lies in."""
    v = (TD * z) % 1.0
    for c, (r0, _) in enumerate(COURSE_ROWS):
        top = (1.0 - r0 / LAYOUT_PX - MORTAR_V) % 1.0   # the joint above the course
        bottom = (1.0 - COURSE_ROWS[c][1] / LAYOUT_PX - MORTAR_V) % 1.0
        if bottom <= top:
            if bottom <= v < top:
                return c
        elif v >= bottom or v < top:
            return c
    raise ValueError(z)


def bed_joints(z0, z1):
    """Heights in (z0, z1] of the centres of the bed joints, lowest first."""
    out = []
    for n in range(int(math.floor(TD * z0)) - 1, int(math.ceil(TD * z1)) + 1):
        for v in BED_V:
            z = (n + v) / TD
            if z0 < z <= z1 + 1e-12:
                out.append(z)
    return sorted(out)


def block_edges(course):
    """The block boundaries of a course in one tile, as fractions of the tile (0 to 1), lowest first."""
    return sorted(b / LAYOUT_PX for b in HEAD_PX[course])


def head_joints(course, side, lo, hi):
    """The e (|x|) in [lo, hi] of the head joints of `course` on the side 'left' or 'right' of the wall, lowest
    first. U = TD (x - X_LOW), so a joint at U = n + b is at x = X_LOW + (n + b) / TD."""
    out = []
    for n in range(-1, 30):
        for b in block_edges(course):
            x = X_LOW + (n + b) / TD
            e = x if side == "right" else -x
            if lo - 1e-12 <= e <= hi + 1e-12:
                out.append(e)
    return sorted(out)


def block_dims(course, k):
    """(length, height) in metres of block k (0 or 1) of a course, as the texture draws it."""
    a, b = block_edges(course)
    length = (b - a) if k == 0 else (1.0 - b + a)
    return length * TILE, course_height(course)


def block_rect(course, k):
    """(u0, v0): the UV of the lower left corner of block k of a course, the block's own rectangle, so that a
    face of the block's size mapped as U = u0 + TD a, V = v0 + TD b shows exactly that block with its mortar."""
    a, b = block_edges(course)
    u0 = a if k == 0 else b
    r0, r1 = COURSE_ROWS[course]
    v0 = 1.0 - r1 / LAYOUT_PX - MORTAR_V
    return u0, v0


# ---- the planes of the solid wall that a cutter plane must miss ----------------------------------------
AVOID_Z = tuple(sorted({
    jp.Z_GROUND, jp.NICHE_Z0, jp.NICHE_Z0 + jp.NICHE_H, jp.Z_PLINTH, jp.PLINTH_CAP_Z0 + 0.1, jp.PLINTH_CAP_Z0 + 0.16,
    jp.PLINTH_CAP_Z1, jp.BAND_Z0, jp.BAND_Z1, jp.INNER_PLINTH_Z1, jp.INNER_PLINTH_Z1 - 0.08, jp.COLLAR_Z0,
    jp.COLLAR_Z0 + 0.13, jp.PIER_TOP_Z, jp.PIER_TOP_Z + 0.05, jp.COLLAR_Z1, jp.STRING_Z0, jp.STRING_Z0 + 0.18,
    jp.STRING_Z0 + 0.26, jp.STRING_Z1 - 0.06, jp.STRING_Z1, jp.Z_DECK, jp.FRIEZE_Z0, jp.PARAPET_BODY_TOP,
    jp.PARAPET_BODY_TOP + 0.2, jp.PARAPET_TOP, jp.Z_DECK + jp.OPEN_SILL, jp.RAIL_BODY_TOP, jp.RAIL_BODY_TOP + 0.05,
    jp.RAIL_TOP}))
_PIER_XS = (-jp.BAY, 0.0, jp.BAY)
AVOID_X = tuple(sorted({round(sx * (jp.BAY / 2.0 * k) + d, 6)
                        for k in (1, 3) for sx in (-1, 1)
                        for d in (-jp.PIER_HALF, jp.PIER_HALF, -jp.PILASTER_IN_W / 2, jp.PILASTER_IN_W / 2,
                                  -jp.NICHE_W / 2, jp.NICHE_W / 2, -jp.COLLAR_HALF_W, jp.COLLAR_HALF_W)}
                       | {round(sx * (m + i * 1.8 + d), 6)
                          for m in (0.0, jp.BAY) for i in (-1, 0, 1) for sx in (-1, 1) for d in (-0.8, 0.8, -0.66, 0.66)}))
MORTAR_SHIFT, MORTAR_NEAR = 0.005, 0.003   # a plane this near a wall plane moves this far (inside the mortar line)


def step_z(z):
    """A bed joint height, moved up by MORTAR_SHIFT when it falls within MORTAR_NEAR of a horizontal plane of the
    wall (a coplanar face breaks a boolean); the shift stays inside the mortar line."""
    if any(abs(z - p) <= MORTAR_NEAR for p in AVOID_Z):
        return z + MORTAR_SHIFT
    return z


def step_e(e):
    """A head joint position, moved out by MORTAR_SHIFT when it falls within MORTAR_NEAR of a plane of the wall
    that runs along Y or Z (a pier or an arch frame)."""
    if any(abs(e - abs(p)) <= MORTAR_NEAR for p in AVOID_X):
        return e + MORTAR_SHIFT
    return e


# ---- the tear -----------------------------------------------------------------------------------
FLOOR, TOP, CORE_TOP = 0.45, 16.0, 13.0   # the cutters stand on the floor and run above the parapet
E_MIN, E_MAX = 2.45, 6.5                   # no edge of the stone comes nearer the centre line than E_MIN
TREND_LOW, TREND_DECK = 3.2, 5.75          # the average edge at the floor and at the deck
TREND_BAND = 1.2                           # an edge never leaves its trend by more than this
DECK_LO, DECK_HI = 5.15, 6.4               # the edge of a skin in the course that holds the deck
PARAPET_MIN = 6.05                         # the parapet ends behind the deck edge (past the part joint at 6)
PARAPET_END = 9.7                          # no tooth of the parapet or the rail lies farther out than this
PARAPET_SET = 2.0                          # the parapet ends at most this far behind the deck edge
STEP_MIN, STEP_MAX = 0.25, 1.4             # a step is at least this long, and at most one block
BAND_MIN = 0.28                            # a band shorter than this is joined to the one below
GONE_LO, GONE_HI = 8.1, 9.6                # where the outer parapet ends on the side that lost its first panel
SKIN_IN = 1.95                             # the back of a skin (Y), 0.55 m behind the wall face
SKIN_OVERLAP = 0.1                         # a skin cutter reaches this far into the core
SET_MIN, SET_RANGE, ROUGH = 0.1, 0.3, 0.15   # the core lies 0.1 to 0.55 m behind the teeth: lumps, and a grain
ENV_SLOPE = 1.6                            # the core slopes at 58 degrees from the vertical where a skin steps


def bands():
    """The bands of an edge, lowest first, as (z0, z1, course): a band is the height of one course of the stone
    (or of the plinth, course None) and an edge keeps one e over it. The plinth bands follow the plinth
    joints, the stone bands the bed joints from the plinth top, and the last two are the cap of the parapet
    (14.0 to 14.4) and what lies above it."""
    cuts = [FLOOR]
    cuts += [z for z in (jp.Z_PLINTH - j * PLINTH_COURSE for j in range(3, 0, -1)) if z > FLOOR + 0.25]
    cuts += [jp.Z_PLINTH]
    for z in bed_joints(jp.Z_PLINTH, jp.PARAPET_BODY_TOP):
        if z - cuts[-1] >= BAND_MIN and jp.PARAPET_BODY_TOP - z >= BAND_MIN:
            cuts.append(z)
    cuts += [jp.PARAPET_BODY_TOP, jp.PARAPET_TOP, TOP]
    out = []
    for a, b in zip(cuts, cuts[1:]):
        if b <= jp.Z_PLINTH + 1e-9 or a >= jp.PARAPET_BODY_TOP - 1e-9:
            course = None
        else:
            course = course_at(0.5 * (a + b))
        out.append((a, b, course))
    return out


def deck_band():
    """Index of the band that holds the deck (Z 12.0)."""
    for i, (a, b, _) in enumerate(bands()):
        if a <= jp.Z_DECK < b:
            return i
    raise ValueError("no band holds the deck")


def trend(z):
    t = min(max((z - FLOOR) / (jp.Z_DECK - FLOOR), 0.0), 1.0)
    return TREND_LOW + (TREND_DECK - TREND_LOW) * t


def _plinth_candidates(lo, hi):
    n = int(round((hi - lo) / 0.05))
    return [lo + 0.05 * k for k in range(n + 1)]


def _candidates(band, side, lo, hi):
    course = band[2]
    if course is None:
        return _plinth_candidates(lo, hi)
    return [step_e(e) for e in head_joints(course, side, lo, hi)]


def _walk(rng, order, es, cands):
    """Fill es band by band in `order` (indices), taking the candidates of cands(k) and trying them in a random
    order that favours the trend; a band with no candidate sends the walk back to the band before it. Returns True
    when every band has a value."""
    if not order:
        return True
    k, rest = order[0], order[1:]
    pool = list(cands(k))
    while pool:
        weights = [math.exp(-((c - trend(0.5 * (bands()[k][0] + bands()[k][1]))) / 0.9) ** 2) + 0.05 for c in pool]
        pick = rng.choices(range(len(pool)), weights)[0]
        es[k] = pool.pop(pick)
        if _walk(rng, rest, es, cands):
            return True
    es[k] = None
    return False


def _skin_side(rng, side, parapet_from, gone):
    """The e of every band of one side of a skin, lowest first (a list), or None when no walk fits. The deck band
    takes a head joint in DECK_LO to DECK_HI; the bands below it keep to the trend and step 0.25 to 1.4 m; the
    bands above it (the parapet, or the rail) only move out, from `parapet_from` on, and `gone` sends the first of
    them past the first panel."""
    bs = bands()
    d = deck_band()
    deck = _candidates(bs[d], side, DECK_LO, DECK_HI)
    if not deck:
        return None
    es = [None] * len(bs)
    es[d] = rng.choice(deck)

    def below(k):
        z_mid = 0.5 * (bs[k][0] + bs[k][1])
        lo, hi = max(E_MIN, trend(z_mid) - TREND_BAND), min(E_MAX, trend(z_mid) + TREND_BAND)
        return [e for e in _candidates(bs[k], side, lo, hi) if STEP_MIN <= abs(e - es[k + 1]) <= STEP_MAX]

    def above(k):
        prev = es[k - 1]
        low, high = max(parapet_from, prev), prev + (PARAPET_SET if k == d + 1 else STEP_MAX)
        if gone and k == d + 1:
            return _candidates(bs[k], side, GONE_LO, GONE_HI)
        if bs[k][2] is None:      # the cap, and above it: a block of any length
            return [e for e in (low + 0.3 + 0.1 * i for i in range(7)) if e <= PARAPET_END]
        steps = [e for e in _candidates(bs[k], side, low, min(high, PARAPET_END)) if e - prev >= STEP_MIN]
        return steps + ([prev] if prev >= parapet_from else [])   # or the tooth goes on up as it is (a block cut across)

    if not _walk(rng, list(range(d - 1, -1, -1)), es, below):
        return None
    if not _walk(rng, list(range(d + 1, len(bs))), es, above):
        return None
    return es


def edge_points(es):
    """The stair outline of an edge from the e of each band: (e, z) points from FLOOR to TOP, a step at every band
    boundary where e changes. A bed joint that lies near a plane of the wall moves (step_z)."""
    bs = bands()
    pts = [(es[0], bs[0][0])]
    for k in range(len(bs) - 1):
        z = step_z(bs[k][1])
        if abs(es[k + 1] - es[k]) < 1e-9:
            continue
        pts += [(es[k], z), (es[k + 1], z)]
    pts.append((es[-1], TOP))
    return pts


def steps_of(pts):
    """The (z, e before, e after) of every step of a stair edge: two points on one height."""
    out = []
    for (e0, z0), (e1, z1) in zip(pts, pts[1:]):
        if abs(z1 - z0) < 1e-9 and abs(e1 - e0) > 1e-9:
            out.append((z0, e0, e1))
    return out


def edge_at(pts, z):
    """|x| where the wall starts at height z: a stair or sloping edge of (e, z) points, None outside it."""
    if z < pts[0][1] or z >= pts[-1][1]:
        return None
    for (e0, z0), (e1, z1) in zip(pts, pts[1:]):
        if z1 - z0 > 1e-9 and z0 <= z < z1:
            return e0 + (e1 - e0) * (z - z0) / (z1 - z0)
    return None


def gap_width(left, right, z):
    return edge_at(left, z) + edge_at(right, z)


def gap_polygon(left, right):
    """The gap as one closed (x, z) outline: down the left edge, across the floor, up the right edge. Each
    edge is (e, z) points that never run down, and both start on one floor."""
    for pts in (left, right):
        if any(b[1] < a[1] - 1e-9 for a, b in zip(pts, pts[1:])):
            raise ValueError("an edge runs down")
    if abs(left[0][1] - right[0][1]) > 1e-9:
        raise ValueError("the two sides need one floor")
    out = []
    for p in [(-e, z) for e, z in reversed(left)] + [(e, z) for e, z in right]:
        if not out or abs(out[-1][0] - p[0]) > 1e-9 or abs(out[-1][1] - p[1]) > 1e-9:
            out.append(p)
    return out


def _skin_ok(es):
    """True when an edge steps out at least three times and in at least three times below the deck."""
    d = deck_band()
    below = [b - a for a, b in zip(es[:d], es[1:d + 1])]
    return sum(1 for s in below if s > 0) >= 3 and sum(1 for s in below if s < 0) >= 3


def make_tear(seed=7):
    """The tear: {"skins": {"outer": {"left": pts, "right": pts}, "inner": ...}, "gone": side of the outer
    parapet that lost its first panel, "rail_from": ..., "seed": seed}. The same seed gives the same tear.
    A draw is thrown away unless every edge steps both out and in at least three times below the deck, and the
    deck gap, between the skins and between the cores, is 11.05 to 12.45 m."""
    rng = random.Random(seed)
    for _ in range(4000):
        gone = rng.choice(("left", "right"))
        es = {}
        for skin in ("outer", "inner"):
            for side in ("left", "right"):
                from_ = PARAPET_MIN if skin == "outer" else 5.6
                es[(skin, side)] = _skin_side(rng, side, from_, gone == side and skin == "outer")
        if any(v is None for v in es.values()):
            continue
        if not all(_skin_ok(v) for v in es.values()):
            continue
        d = deck_band()
        for skin in ("outer", "inner"):
            if not 11.05 <= es[(skin, "left")][d] + es[(skin, "right")][d] <= 12.0:
                break
        else:
            skins = {skin: {side: edge_points(es[(skin, side)]) for side in ("left", "right")} for skin in ("outer", "inner")}
            return {"skins": skins, "gone": gone, "seed": seed}
    raise RuntimeError("no tear found")


CHIP_MIN, CHIP_MAX = 0.03, 0.12   # a tooth loses a corner of this size (a chip, not a saw cut)


def chip_edge(pts, rng, share=0.6):
    """The edge `pts` with the corner of a tooth chipped off at a share of the steps. Where the wall's own corner
    sticks out (the top of the tooth at a step out, the underside of the hanging block at a step in) a diagonal of
    CHIP_MIN to CHIP_MAX replaces the corner. A chip only takes wall away, never runs down, and is at most 0.4 of the
    step and of the band, so a step is still a step."""
    repl = {}
    for k in range(1, len(pts) - 2):
        (e0, z0), (e1, z1) = pts[k], pts[k + 1]
        if abs(z1 - z0) > 1e-9 or abs(e1 - e0) < 1e-9 or rng.random() >= share:
            continue
        c_e = min(rng.uniform(CHIP_MIN, CHIP_MAX), 0.4 * abs(e1 - e0))
        if e1 > e0:    # a step out: the top front corner of the tooth
            c_z = min(rng.uniform(CHIP_MIN, CHIP_MAX), 0.4 * (z0 - pts[k - 1][1]))
            repl[k] = [(e0, z0 - c_z), (e0 + c_e, z0)]
        else:          # a step in: the underside front corner of the block that hangs
            c_z = min(rng.uniform(CHIP_MIN, CHIP_MAX), 0.4 * (pts[k + 2][1] - z1))
            repl[k + 1] = [(e1 + c_e, z1), (e1, z1 + c_z)]
    out = []
    for i, p in enumerate(pts):
        out += repl.get(i, [p])
    return out


def deck_edge(tear, skin, side, z=11.9):
    return edge_at(tear["skins"][skin][side], z)


# ---- a smooth noise, and the core -----------------------------------------------------------------
def _hash01(ix, iy, seed):
    n = (ix * 374761393 + iy * 668265263 + seed * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    n ^= n >> 16
    return (n & 0xFFFFFF) / float(0xFFFFFF)


def smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def value_noise(x, y, seed=0):
    """A smooth noise from 0 to 1 on a lattice of spacing 1: the values at the lattice points are hashed, and the
    value between them is the smoothstep blend of the four corners."""
    ix, iy = int(math.floor(x)), int(math.floor(y))
    fx, fy = smooth(x - ix), smooth(y - iy)
    a, b = _hash01(ix, iy, seed), _hash01(ix + 1, iy, seed)
    c, d = _hash01(ix, iy + 1, seed), _hash01(ix + 1, iy + 1, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def lump(y, z, seed=3):
    """0 to 1: the lumps of a rubble face, three octaves (0.75 m, 0.37 m and 0.2 m), over the face (y, z). The
    value is not cut off at either end, so no part of the face is a flat plane at the least or the most set back."""
    v = (0.5 * value_noise(y / 0.75, z / 0.75, seed) + 0.3 * value_noise(y / 0.37 + 7.1, z / 0.37 + 3.3, seed + 1)
         + 0.2 * value_noise(y / 0.2 + 3.7, z / 0.2 + 9.9, seed + 2))
    return min(max(v, 0.0), 1.0)


def skin_envelope(tear, side, z, slope=ENV_SLOPE):
    """The e that the core must reach at height z to stay behind the teeth of both skins on `side`: the largest of
    (e of a stretch of tooth) - slope * (the height between z and that stretch). Where a skin steps, the core
    slopes at `slope` (e per metre of height) instead of running level, so there is no flat roof under a hanging
    block and no flat floor under a missing one. The core has no wall above the deck: z is held at the deck."""
    z = min(max(z, FLOOR), jp.Z_DECK - 0.01)
    best = 0.0
    for skin in ("outer", "inner"):
        pts = tear["skins"][skin][side]
        for (e0, z0), (e1, z1) in zip(pts, pts[1:]):
            if z1 - z0 > 1e-9 and abs(e1 - e0) < 1e-9 and z0 < jp.Z_DECK:
                d = max(z0 - z, z - min(z1, jp.Z_DECK), 0.0)
                best = max(best, e0 - slope * d)
    return best


def core_edge(tear, side, y, z, seed=3):
    """e of the core surface at (y, z): the envelope of the two skins, plus SET_MIN, plus up to SET_RANGE of lump and
    up to ROUGH of grain (a noise of 0.11 m, so that neighbouring points of the grid lean every way)."""
    grain = value_noise(y / 0.11 + 1.3, z / 0.11 + 5.9, seed + 3)
    return skin_envelope(tear, side, z) + SET_MIN + SET_RANGE * lump(y, z, seed) + ROUGH * grain


CORE_ROW_STEP, CORE_COLS = 0.25, 15
CORE_Y = SKIN_IN


def core_grid(tear, seed=3):
    """The core as two lumpy surfaces on a grid. Row j lies at height zs[j] (from FLOOR - 0.03 to CORE_TOP) and
    has CORE_COLS points across the wall from -y to +y, with y = CORE_Y plus a jitter of up to 0.04 m that is
    fixed for the row, so that the back of a skin is no single plane. xl and xr give e (|x|) of the left and the
    right surface at each point."""
    rng = random.Random(seed + 100)
    z0 = FLOOR - 0.03
    n = int(math.ceil((CORE_TOP - z0) / CORE_ROW_STEP))
    zs = [z0 + (CORE_TOP - z0) * j / n for j in range(n + 1)]
    ys, xl, xr = [], [], []
    for z in zs:
        hi = CORE_Y + rng.uniform(-0.04, 0.04)
        lo = CORE_Y + rng.uniform(-0.04, 0.04)
        row = [-lo + (hi + lo) * i / (CORE_COLS - 1) for i in range(CORE_COLS)]
        ys.append(row)
        xl.append([core_edge(tear, "left", y, z, seed) for y in row])
        xr.append([core_edge(tear, "right", y, z, seed) for y in row])
    return {"zs": zs, "ys": ys, "xl": xl, "xr": xr}


def surface_triangles(grid, side):
    """The lumpy surface of one side as triangles of 3D points: each cell of the grid is cut on a diagonal that
    alternates from cell to cell. Left points are at x = -e."""
    sign = -1.0 if side == "left" else 1.0
    xs = grid["xl"] if side == "left" else grid["xr"]
    p = lambda i, j: (sign * xs[j][i], grid["ys"][j][i], grid["zs"][j])
    tris = []
    for j in range(len(grid["zs"]) - 1):
        for i in range(CORE_COLS - 1):
            a, b, c, d = p(i, j), p(i + 1, j), p(i + 1, j + 1), p(i, j + 1)
            tris += [(a, b, c), (a, c, d)] if (i + j) % 2 == 0 else [(a, b, d), (b, c, d)]
    return tris


def _tri_normal_area(t):
    u = [t[1][k] - t[0][k] for k in range(3)]
    w = [t[2][k] - t[0][k] for k in range(3)]
    n = (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])
    m = math.sqrt(sum(c * c for c in n))
    return ((n[0] / m, n[1] / m, n[2] / m) if m > 1e-15 else (0.0, 0.0, 1.0)), 0.5 * m


def largest_flat_patch(tris, tol_deg=4.0):
    """The largest group of triangles that share an edge (a corner counts when it is exact) and whose normals differ
    by less than `tol_deg` from the first of the group: the largest flat plane of a surface. Returns (area in m2,
    centre of the group, normal of its first triangle); (0, None, None) for no triangle."""
    info = [_tri_normal_area(t) for t in tris]
    key = lambda p: tuple(round(c, 6) for c in p)
    owner = {}
    for i, t in enumerate(tris):
        for a in range(3):
            owner.setdefault(frozenset((key(t[a]), key(t[(a + 1) % 3]))), []).append(i)
    seen, best = set(), (0.0, None, None)
    cos_tol = math.cos(math.radians(tol_deg))
    for s in range(len(tris)):
        if s in seen:
            continue
        seen.add(s)
        stack, total, n0, centre = [s], 0.0, info[s][0], [0.0, 0.0, 0.0]
        while stack:
            i = stack.pop()
            total += info[i][1]
            t = tris[i]
            for k in range(3):
                centre[k] += info[i][1] * (t[0][k] + t[1][k] + t[2][k]) / 3.0
            for a in range(3):
                for j in owner[frozenset((key(t[a]), key(t[(a + 1) % 3])))]:
                    if j not in seen and sum(x * y for x, y in zip(info[j][0], n0)) >= cos_tol:
                        seen.add(j)
                        stack.append(j)
        if total > best[0]:
            best = (total, tuple(c / total for c in centre), n0)
    return best


def flat_patch_area(tris, tol_deg=4.0):
    """The area (m2) of the largest flat plane of a surface (largest_flat_patch)."""
    return largest_flat_patch(tris, tol_deg)[0]


def deck_gap(tear, y, z=11.9, seed=3):
    """(left e, right e, width) of the gap at height z at Y = y: in a skin the tooth of that skin, in the core the
    lumpy surface."""
    if y >= SKIN_IN + 0.05:
        skin = "outer"
    elif y <= -SKIN_IN - 0.05:
        skin = "inner"
    else:
        skin = None
    if skin:
        left, right = deck_edge(tear, skin, "left", z), deck_edge(tear, skin, "right", z)
    else:
        left, right = core_edge(tear, "left", y, z, seed), core_edge(tear, "right", y, z, seed)
    return left, right, left + right


def clip_polygon_z(poly, z0, z1):
    """A polygon of (a, z) points cut to z0 <= z <= z1 (Sutherland-Hodgman, two planes)."""
    def cut(pts, keep, limit):
        out = []
        for i, p in enumerate(pts):
            q = pts[(i + 1) % len(pts)]
            pin, qin = keep(p[1]), keep(q[1])
            if pin:
                out.append(p)
            if pin != qin:
                t = (limit - p[1]) / (q[1] - p[1])
                out.append((p[0] + t * (q[0] - p[0]), limit))
        return out

    pts = cut(list(poly), lambda z: z >= z0, z0)
    pts = cut(pts, lambda z: z <= z1, z1) if pts else []
    out = []  # a vertex on a clip plane is kept and also re-created: drop the repeat
    for p in pts:
        if not out or abs(out[-1][0] - p[0]) > 1e-9 or abs(out[-1][1] - p[1]) > 1e-9:
            out.append(p)
    if len(out) > 1 and abs(out[0][0] - out[-1][0]) < 1e-9 and abs(out[0][1] - out[-1][1]) < 1e-9:
        out.pop()
    return out


# ---- the heap mound ------------------------------------------------------------------------------
CREST = 4.3
PLATEAU_Y = 0.9          # flat crest for |y| below this
REACH_Y = 9.4            # the toe on the walk path: 6.9 m past the wall face at |y| 2.5
FACE_Y = 2.5
PLATEAU_X = 2.8          # |x| below this the ramp has no lateral fall-off and no noise
SPILL_AX = 3.8           # the ramp reaches |x| 6.6 along the wall face
GRID = 0.5
HALF_X, HALF_Y = 10.0, 10.5
NODE_NOISE = 0.22
LUMP_NOISE = 0.24        # the smooth lumps (value noise of 1.1 m) that break the ramp up outside the path
NOISE_X0 = 2.2
TOE_WAVE = 0.25          # the outline moves in by up to this share outside the walk path
PATH_HALF, STEP = 1.5, 0.25
HEAP_FLOOR = -0.25       # the field runs on below the ground line to this, so the toe has no stair


def base_height(x, y):
    """A flat crest across the gap (|x| below PLATEAU_X, |y| below PLATEAU_Y), falling off in an ellipse whose
    outline is moved in and out by an angle-dependent wave. On the walk path the wave is zero, so the slope
    there is the plain fall from the crest to the toe. Below the ground line (q over 1) the fall goes on, down
    to HEAP_FLOOR."""
    ax, ay = abs(x), abs(y)
    u, v = max(ax - PLATEAU_X, 0.0), max(ay - PLATEAU_Y, 0.0)
    env = smooth((ax - 2.4) / 1.2)
    th = math.atan2(v, u)
    k = (0 if x < 0 else 1) + (0 if y < 0 else 2)
    wave = (math.sin(3.0 * th + 0.8 * k) + 0.6 * math.sin(7.0 * th + 1.9 * k)) / 1.6
    scale = 1.0 - TOE_WAVE * env * (0.5 + 0.5 * wave)  # at most 1: the outline never passes the toe
    q = math.hypot(u / SPILL_AX, v / (REACH_Y - PLATEAU_Y)) / scale
    return CREST * max(1.0 - q, HEAP_FLOOR / CREST)


def make_bumps(rng, count=54):
    """Stone-sized bumps (cx, cy, radius, height) away from the clear path (4 m wide: no bump reaches |x| 2.1)."""
    bumps = []
    while len(bumps) < count:
        r = rng.uniform(0.4, 0.9)
        cx = rng.choice((-1, 1)) * rng.uniform(2.1 + r, 6.2)
        cy = rng.choice((-1, 1)) * rng.uniform(0.5, 7.8)
        bumps.append((cx, cy, r, rng.uniform(0.12, 0.38)))
    return bumps


class Heap:
    """The mound as a height field on a GRID m grid, with the noise, the lumps and the bumps that make it a heap."""

    def __init__(self, seed=11):
        rng = random.Random(seed)
        nx, ny = int(round(HALF_X / GRID)), int(round(HALF_Y / GRID))
        self.xs = [i * GRID for i in range(-nx, nx + 1)]
        self.ys = [j * GRID for j in range(-ny, ny + 1)]
        bumps = make_bumps(rng)
        self.z = {}
        for i, x in enumerate(self.xs):
            for j, y in enumerate(self.ys):
                b = base_height(x, y)
                above = min(1.0, max(b, 0.0) / 0.6)
                env = smooth((abs(x) - NOISE_X0) / 1.0) * above
                z = b + (rng.random() * 2.0 - 1.0) * NODE_NOISE * env
                z += (value_noise(x / 1.1, y / 1.1, 5) - 0.5) * 2.0 * LUMP_NOISE * env
                if b > 0.05:
                    for cx, cy, r, h in bumps:
                        d = math.hypot(x - cx, y - cy)
                        if d < r:
                            z += h * math.cos(0.5 * math.pi * d / r) ** 2 * above
                self.z[(i, j)] = max(z, HEAP_FLOOR)

    def nodes(self):
        return self.z.items()

    def at(self, x, y):
        fx = (x - self.xs[0]) / GRID
        fy = (y - self.ys[0]) / GRID
        i = min(max(int(math.floor(fx)), 0), len(self.xs) - 2)
        j = min(max(int(math.floor(fy)), 0), len(self.ys) - 2)
        tx, ty = fx - i, fy - j
        z = self.z
        return ((1 - tx) * (1 - ty) * z[(i, j)] + tx * (1 - ty) * z[(i + 1, j)]
                + (1 - tx) * ty * z[(i, j + 1)] + tx * ty * z[(i + 1, j + 1)])


def path_stats(height, half=PATH_HALF, reach=HALF_Y, step=STEP, limit=32.0):
    """Slope along the walk path (X within +-half, Y from -reach to +reach) on a `step` grid: central
    differences in X and Y, one-sided at the edge. `height(x, y)` is the surface height."""
    n_x = int(round(2 * half / step)) + 1
    n_y = int(round(2 * reach / step)) + 1
    xs = [-half + i * step for i in range(n_x)]
    ys = [-reach + j * step for j in range(n_y)]
    z = [[height(x, y) for y in ys] for x in xs]
    slopes = []
    for i in range(n_x):
        for j in range(n_y):
            i0, i1 = max(i - 1, 0), min(i + 1, n_x - 1)
            j0, j1 = max(j - 1, 0), min(j + 1, n_y - 1)
            gx = (z[i1][j] - z[i0][j]) / ((i1 - i0) * step)
            gy = (z[i][j1] - z[i][j0]) / ((j1 - j0) * step)
            slopes.append((math.degrees(math.atan(math.hypot(gx, gy))), xs[i], ys[j]))
    worst = max(slopes)
    under = sum(1 for s in slopes if s[0] < limit)
    return {"nodes": len(slopes), "under_limit_share": round(under / len(slopes), 4),
            "steepest_deg": round(worst[0], 4), "steepest_at_xy": [worst[1], worst[2]],
            "limit_deg": limit, "path_half_width": half,
            "crest_on_path": round(max(max(r) for r in z), 3)}


def step_stats(height, half=2.0, reach=HALF_Y, run=0.1, lines=9):
    """The largest rise over a run of `run` metres along Y on `lines` lines across the path (X within +-half): the
    step a walker meets. `height(x, y)` is the surface height."""
    n_y = int(round(2 * reach / run)) + 1
    worst, at = 0.0, None
    for k in range(lines):
        x = -half + 2.0 * half * k / (lines - 1)
        prev = height(x, -reach)
        for j in range(1, n_y):
            y = -reach + j * run
            cur = height(x, y)
            if abs(cur - prev) > worst:
                worst, at = abs(cur - prev), (x, y)
            prev = cur
    return {"max_step": round(worst, 4), "at_xy": list(at) if at else None, "run": run, "lines": lines}


# ---- a polygon without a zero-area triangle -----------------------------------------------------
def _cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])


def ear_clip(pts, eps=1e-9):
    """Triangles (index triples, counter-clockwise) of a simple polygon of (x, y) points, which may have
    several points on one straight edge. A point on a straight edge is never the tip of an ear, so no
    triangle has zero area. Raises ValueError if the polygon cannot be cut (not simple)."""
    idx = list(range(len(pts)))
    if sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1]
           for i in idx) < 0.0:
        idx.reverse()
    tris = []
    while len(idx) > 3:
        m = len(idx)
        for k in range(m):
            a, b, c = idx[k - 1], idx[k], idx[(k + 1) % m]
            if _cross(pts[a], pts[b], pts[c]) <= eps:
                continue
            if any(o not in (a, b, c) and _inside_or_on(pts[o], pts[a], pts[b], pts[c], eps) for o in idx):
                continue
            tris.append((a, b, c))
            del idx[k]
            break
        else:
            raise ValueError("the polygon cannot be cut into triangles")
    a, b, c = idx
    if _cross(pts[a], pts[b], pts[c]) > eps:
        tris.append((a, b, c))
    return tris


def _inside_or_on(p, a, b, c, eps):
    return (_cross(a, b, p) >= -eps and _cross(b, c, p) >= -eps and _cross(c, a, p) >= -eps)


# ---- the UV of a face with no courses --------------------------------------------------------------
def dominant_axis(n):
    a = [abs(c) for c in n]
    return a.index(max(a))


def plane_coords(axis, co):
    """The two coordinates of a point seen along `axis` (the projection axis): Z gives (x, y), X gives
    (y, z), Y gives (x, z)."""
    x, y, z = co
    return {0: (y, z), 1: (x, z), 2: (x, y)}[axis]


def polygon_normal_area(points):
    """(unit normal, area) of a polygon of 3D points (Newell's method)."""
    nx = ny = nz = 0.0
    for i, p in enumerate(points):
        q = points[(i + 1) % len(points)]
        nx += (p[1] - q[1]) * (p[2] + q[2])
        ny += (p[2] - q[2]) * (p[0] + q[0])
        nz += (p[0] - q[0]) * (p[1] + q[1])
    m = math.sqrt(nx * nx + ny * ny + nz * nz)
    if m < 1e-15:
        return (0.0, 0.0, 1.0), 0.0
    return (nx / m, ny / m, nz / m), 0.5 * m


def area_uv(points, normal=None):
    """The UV of each corner of a face of the heap material at the exact density TD: the area of the face in UV
    (units2) over its area in metres is TD squared, however the face leans. The corners are projected along the
    face's dominant axis (so that neighbouring faces keep the texture's drift), and the projection is scaled
    about its own centre by 1 / sqrt(cos) of the lean, which makes up the area. A neighbour with another lean
    differs by TD (s - 1) times the distance from the centre: a few millimetres of texture."""
    n, area = polygon_normal_area(points)
    if normal is not None:
        n = tuple(normal)
    axis = dominant_axis(n)
    flat = [plane_coords(axis, p) for p in points]
    proj = 0.5 * abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(flat, flat[1:] + flat[:1])))
    if proj < 1e-12 or area < 1e-12:
        return [(0.0, 0.0)] * len(points)
    s = math.sqrt(area / proj)
    cu = sum(p[0] for p in flat) / len(flat)
    cv = sum(p[1] for p in flat) / len(flat)
    return [(TD * (cu + s * (p[0] - cu)), TD * (cv + s * (p[1] - cv))) for p in flat]


# ---- loose stones ------------------------------------------------------------------------------------
ROCK_POINTS = 12


def stone_points(rng, size, extra=3, style="cut"):
    """The points of an angular stone (the corners of its convex hull), local coordinates. size is (w, d, h), each a
    (low, high) range in metres. Style "cut": the eight corners of a box, each moved by up to a fifth of the box,
    and `extra` more points a little outside the box on its faces, so that the hull has cut corners. Style "rock":
    ROCK_POINTS points on an ellipsoid of that size, each pulled in by up to a third, so the hull has no box in it."""
    w, d, h = (rng.uniform(*r) for r in size)
    if style == "rock":
        pts = []
        for _ in range(ROCK_POINTS):
            v = [rng.gauss(0.0, 1.0) for _ in range(3)]
            m = math.sqrt(sum(c * c for c in v)) or 1.0
            k = rng.uniform(0.66, 1.0)
            pts.append((0.5 * w * k * v[0] / m, 0.5 * d * k * v[1] / m, 0.5 * h * k * v[2] / m))
        return pts
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                pts.append((sx * 0.5 * w * rng.uniform(0.8, 1.0), sy * 0.5 * d * rng.uniform(0.8, 1.0),
                            sz * 0.5 * h * rng.uniform(0.8, 1.0)))
    for _ in range(extra):
        axis = rng.randrange(3)
        sign = rng.choice((-1, 1))
        p = [rng.uniform(-0.35, 0.35) * dim for dim in (w, d, h)]
        p[axis] = sign * 0.5 * (w, d, h)[axis] * rng.uniform(1.0, 1.12)
        pts.append(tuple(p))
    return pts


# ---- dust, the vertex colour mottle and where a block may lie -------------------------------------------
def heap_tone(x, y, z):
    """A mottle of the heap's vertex colour, 0.88 to 1.0: two slow waves across the heap, so that the patches of
    the texture are not all one tone."""
    w = 0.5 + 0.25 * math.sin(0.9 * x + 1.1) * math.cos(0.7 * y + 0.3) + 0.25 * math.sin(1.7 * y - 0.8 * x + 0.4 + 0.3 * z)
    return 0.88 + 0.12 * min(max(w, 0.0), 1.0)


DUST_REACH = 0.9    # the dust fades out at this distance from a torn face
DUST_TORN = 0.80    # the colour factor on a torn face
DUST_FLOOR = 0.62


def dust_tone(rgb, d):
    """The vertex colour `rgb` darkened, with a warm grey cast, by the dust of a torn face `d` metres away:
    the factor DUST_TORN on the face, fading out smoothly at DUST_REACH."""
    t = smooth(1.0 - d / DUST_REACH)
    if t <= 0.0:
        return tuple(rgb)
    f = 1.0 - (1.0 - DUST_TORN) * t
    out = (rgb[0] * f, rgb[1] * f * (1.0 - 0.02 * t), rgb[2] * f * (1.0 - 0.06 * t))
    return tuple(min(max(c, DUST_FLOOR), 1.0) for c in out)


PATH_CLEAR_HALF = 2.0   # the clear path over the ramp is 4 m wide
PATH_MARGIN = 0.15


def outside_path(x, r, half=PATH_CLEAR_HALF, margin=PATH_MARGIN):
    """True when a loose piece at X = x with reach r stays out of the clear path (|x| up to `half`)."""
    return abs(x) - r >= half + margin


def rest_offset(points, ground, sink=0.06, floor_z=-0.28):
    """The height to add to `points` ((x, y, z) of a piece in place) so that its lowest point lies `sink` below
    the ground height `ground(x, y)`, and no point lies below `floor_z`."""
    dz = -sink + max(ground(p[0], p[1]) - p[2] for p in points)
    return max(dz, floor_z - min(p[2] for p in points))
