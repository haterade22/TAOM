"""The joint profile and the shared rules of the Rivendell siege set, pure Python (no bpy).
Tests: tools/tests/test_rivendell_joint_profile.py. Version 6 (2026-11-25, Mike's second corrections): the end
section has a plain pier with no niche (section), the pilaster taper starts at the top of the collar
(PILASTER_TAPER_Z0, pilaster_proud), the two rib limbs of a bay are mirror pairs that stand whole on the collar
(RIB_LONG, RIB_LOW, rib_curve with max_turn_deg, rib_section, rib_u), and the blind arch frames have
ARCH_STEPS_FINE steps. Version 5: the band under the blind arches ends at the outer legs (arch_band_spans), and
the tone has a wash and soft run-off streaks that sit on the columns of the cell grid (TONE_COLUMNS, column_cuts).

Every piece of the set (solid wall, breached wall, the two wall stubs of a gatehouse) ends, at its lowest
and at its highest X, in the same flat vertical section, so that two pieces set end to end meet with no
gap, no step and no moulding that stops short. The joint falls in the middle of a pier, so the pier
hides the seam. Nobody types these numbers twice: every builder imports them from here.

Frame: metres, a piece runs along X, the outer face is +Y, Z is up, Z = 0 is the ground line, the wall
walk (deck) is at Z = 12.0 and the foundation ends at Z = -8. A profile point is (y, z).

section() returns the section as closed polygons (counter-clockwise in (y, z), the closing point
implied). The polygons of the section tile it without overlap (version 4; tested): the parapet and the rail
stand on the deck at Z 12.0 and are flush with the wall faces (the outer face of the parapet is the
plane Y 2.5, the inner face of the rail the plane Y -2.5), so that the wall face runs on into the
parapet with no lip (a lip at the deck line drew a dark hairline along the wall) and no two faces lie
in one plane at an end plane. The plinth cap and the carved band are not in
section(): at the joint plane they lie inside the pier, and member() gives their profiles for the run
of wall between two piers. The inner pilaster is 0.9 m wide and is cut in half by every joint, so it is
in section(); the inner plinth course and the inner string course run the whole length.

Texel density: TD = 0.45 UV units per metre, one tile is 2.222 m. world_uv() and trim_uv() are the UV
rules every builder uses. The tone of a vertex (vertex_tone) is a pure function of the member, of the
height and of X measured from the piece's low end modulo the bay, so that it is equal on both sides of
every joint.
"""
import math

TD = 0.45  # one tile is 2.222 m: a 20 m wall holds 9, a 26.667 m gatehouse 12, a 13.333 m tower 6

# ---- the pinned section ---------------------------------------------------------------------------
Y_IN, Y_OUT = -2.5, 2.5          # the wall body
Z_FOOT, Z_GROUND, Z_PLINTH, Z_DECK = -8.0, 0.0, 3.0, 12.0
FOUNDATION_Y_OUT = 3.1           # the foundation face, and the plinth at the ground
BAY = 20.0 / 3.0                 # pier to pier
HALF_BAY = BAY / 2.0

PIER_WIDTH, PIER_HALF = 1.2, 0.6
PIER_PROUD_GROUND, PIER_PROUD_TOP, PIER_TOP_Z = 1.0, 0.5, 8.5
PILASTER_PROUD, STRING_Z0, STRING_Z1, STRING_PROUD = 0.3, 10.5, 10.9, 0.15
# The pilaster above the collar starts to die into the wall face at the top of the collar (COLLAR_Z1) and goes
# in one straight slope to the foot of the string course: it has no flat top and no proud stretch above the
# collar, and where it reaches the string course it is not proud at all (the string course is 0.15 m proud).
# Version 5 started the slope at 9.45; Mike asked for it right after the trim.
PILASTER_TAPER_Z0 = 8.8
PIER_FOOT_Z = -1.0               # the pier is buried 1 m below the ground line
NICHE_W, NICHE_Z0, NICHE_H = 0.6, 0.9, 2.4
NICHE_BACK_Y = 3.1  # the niche back is plumb, in the plane of the foundation face

# the carved collar on the pier where the rib limbs spring: a moulded ring a little wider than the pier
COLLAR_Z0, COLLAR_Z1, COLLAR_PROUD, COLLAR_HALF_W = 8.15, 8.8, 0.64, 0.66

PLINTH_CAP_Z0, PLINTH_CAP_Z1, PLINTH_CAP_PROUD = 3.0, 3.2, 0.1
BAND_Z0, BAND_Z1, BAND_PROUD = 3.4, 4.0, 0.08

# the inner (town side, -Y) face: a pilaster behind each pier, a plinth course and a string course
PILASTER_IN_W, PILASTER_IN_PROUD = 0.9, 0.10
INNER_PLINTH_Z1, INNER_PLINTH_PROUD = 3.22, 0.14
INNER_STRING_PROUD = 0.12

PARAPET_Y0, PARAPET_Y1 = 2.05, Y_OUT     # 0.45 thick, its outer face in the plane of the wall face
PARAPET_FOOT, PARAPET_BODY_TOP, PARAPET_TOP = Z_DECK, 14.0, 14.4
PARAPET_CAP_OVER = 0.08
FRIEZE_Z0, FRIEZE_PROUD = 13.6, 0.05     # a flat band under the cap, mapped on strip S5 (the blind arcade)
OPEN_W, OPEN_SILL, OPEN_H = 0.5, 0.8, 0.7  # a parapet opening: its apex is 1.5 m over the deck
OPEN_PITCH, OPEN_MARGIN = 2.0, 0.2       # one opening per 2 m of part, 0.2 m of stone at each end
RAIL_Y0, RAIL_Y1 = Y_IN, -2.30           # 0.2 thick, its inner face in the plane of the wall face
RAIL_FOOT, RAIL_BODY_TOP, RAIL_TOP = Z_DECK, 12.75, 12.9
RAIL_CAP_OVER = 0.04


def pier_front_y(z):
    """The outer face of the pier at height z (0 to 8.5): 1.0 m proud at the ground, 0.5 m at 8.5."""
    t = min(max(z, 0.0), PIER_TOP_Z) / PIER_TOP_Z
    return Y_OUT + PIER_PROUD_GROUND + (PIER_PROUD_TOP - PIER_PROUD_GROUND) * t


def pier_profile(niche=True):
    """The pier in (y, z): from the foundation face up the battered plinth line, the wall face and
    the pilaster, with its front tapering from 1.0 to 0.5 m proud. The pilaster is 0.3 m proud from the
    collar ledge up to PILASTER_TAPER_Z0 (the top of the collar), then slopes back to the wall face at the
    foot of the string course. `niche` adds the notch of the pointed niche as it cuts the front at its centre
    line; the niche back is plumb. The joint section uses niche=False (version 6): a free end of a piece is
    plain. A whole pier inside a piece keeps its niche."""
    f = pier_front_y
    z0, z1 = NICHE_Z0, NICHE_Z0 + NICHE_H
    pts = [(FOUNDATION_Y_OUT, PIER_FOOT_Z), (Y_OUT + PIER_PROUD_GROUND, PIER_FOOT_Z),
           (Y_OUT + PIER_PROUD_GROUND, Z_GROUND)]
    if niche:
        pts += [(f(z0), z0), (NICHE_BACK_Y, z0), (NICHE_BACK_Y, z1), (f(z1), z1)]
    pts += [(Y_OUT + PIER_PROUD_TOP, PIER_TOP_Z), (Y_OUT + PILASTER_PROUD, PIER_TOP_Z),
            (Y_OUT + PILASTER_PROUD, PILASTER_TAPER_Z0), (Y_OUT, STRING_Z0), (Y_OUT, Z_PLINTH),
            (FOUNDATION_Y_OUT, Z_GROUND)]
    return pts


def pilaster_proud(z):
    """How far the pier stands out of the wall face at height z: the battered front up to the pier top, 0.3 m
    on the pilaster up to the top of the collar, one straight slope to nothing at the foot of the string
    course, and nothing above it."""
    if z < PIER_TOP_Z:
        return pier_front_y(z) - Y_OUT
    if z <= PILASTER_TAPER_Z0:
        return PILASTER_PROUD
    if z >= STRING_Z0:
        return 0.0
    return PILASTER_PROUD * (STRING_Z0 - z) / (STRING_Z0 - PILASTER_TAPER_Z0)


def member(name):
    """Profiles of the members that run along the wall between two piers (they lie inside the pier at
    a joint plane, so section() leaves them out)."""
    if name == "plinth_cap":
        p = PLINTH_CAP_PROUD
        return [(Y_OUT, PLINTH_CAP_Z0), (Y_OUT + p, PLINTH_CAP_Z0), (Y_OUT + p, PLINTH_CAP_Z0 + 0.1),
                (Y_OUT + 0.04, PLINTH_CAP_Z0 + 0.16), (Y_OUT + 0.04, PLINTH_CAP_Z1), (Y_OUT, PLINTH_CAP_Z1)]
    if name == "band":
        return [(Y_OUT, BAND_Z0), (Y_OUT + BAND_PROUD, BAND_Z0), (Y_OUT + BAND_PROUD, BAND_Z1),
                (Y_OUT, BAND_Z1)]
    raise KeyError(name)


def collar_profile():
    """The collar in (y, z): it flares out of the battered pier front, keeps a flat face, and slopes
    back into the pilaster face. It rests on the ledge where the pier front steps back to the pilaster."""
    return [(pier_front_y(COLLAR_Z0), COLLAR_Z0), (Y_OUT + COLLAR_PROUD, COLLAR_Z0 + 0.13),
            (Y_OUT + COLLAR_PROUD, PIER_TOP_Z + 0.05), (Y_OUT + PILASTER_PROUD, COLLAR_Z1),
            (Y_OUT + PILASTER_PROUD, PIER_TOP_Z), (Y_OUT + PIER_PROUD_TOP, PIER_TOP_Z)]


def section():
    """The joint section: name to closed polygon of (y, z), counter-clockwise. The pier is the plain one: no niche
    stands at a joint plane (version 6)."""
    d = FOUNDATION_Y_OUT
    s = STRING_PROUD
    po = PARAPET_CAP_OVER
    ro = RAIL_CAP_OVER
    i = INNER_STRING_PROUD
    pi = INNER_PLINTH_PROUD
    return {
        "foundation": [(Y_IN, Z_FOOT), (d, Z_FOOT), (d, Z_GROUND), (Y_IN, Z_GROUND)],
        "plinth": [(Y_IN, Z_GROUND), (d, Z_GROUND), (Y_OUT, Z_PLINTH), (Y_IN, Z_PLINTH)],
        "body": [(Y_IN, Z_PLINTH), (Y_OUT, Z_PLINTH), (Y_OUT, Z_DECK), (Y_IN, Z_DECK)],
        "pier": pier_profile(niche=False),
        "collar": collar_profile(),
        "string_course": [(Y_OUT, STRING_Z0), (Y_OUT + s, STRING_Z0), (Y_OUT + s, STRING_Z0 + 0.18),
                          (Y_OUT + s - 0.07, STRING_Z0 + 0.26), (Y_OUT + s - 0.07, STRING_Z1 - 0.06),
                          (Y_OUT, STRING_Z1)],
        "inner_plinth_course": [(Y_IN - pi, Z_PLINTH), (Y_IN, Z_PLINTH), (Y_IN, INNER_PLINTH_Z1),
                                (Y_IN - PILASTER_IN_PROUD, INNER_PLINTH_Z1),
                                (Y_IN - pi, INNER_PLINTH_Z1 - 0.08)],
        "inner_pilaster": [(Y_IN - PILASTER_IN_PROUD, INNER_PLINTH_Z1), (Y_IN, INNER_PLINTH_Z1),
                           (Y_IN, STRING_Z0), (Y_IN - PILASTER_IN_PROUD, STRING_Z0)],
        "inner_string_course": [(Y_IN - i, STRING_Z0), (Y_IN, STRING_Z0), (Y_IN, STRING_Z1),
                                (Y_IN - i + 0.05, STRING_Z1 - 0.06), (Y_IN - i + 0.05, STRING_Z0 + 0.26),
                                (Y_IN - i, STRING_Z0 + 0.18)],
        "parapet": [(PARAPET_Y0, PARAPET_FOOT), (PARAPET_Y1, PARAPET_FOOT), (PARAPET_Y1, PARAPET_BODY_TOP),
                    (PARAPET_Y0, PARAPET_BODY_TOP)],
        "frieze": [(PARAPET_Y1, FRIEZE_Z0), (PARAPET_Y1 + FRIEZE_PROUD, FRIEZE_Z0),
                   (PARAPET_Y1 + FRIEZE_PROUD, PARAPET_BODY_TOP), (PARAPET_Y1, PARAPET_BODY_TOP)],
        "parapet_cap": [(PARAPET_Y0 - po, PARAPET_BODY_TOP), (PARAPET_Y1 + po, PARAPET_BODY_TOP),
                        (PARAPET_Y1 + po, PARAPET_BODY_TOP + 0.2), (PARAPET_Y1 + po - 0.05, PARAPET_TOP),
                        (PARAPET_Y0 - po + 0.05, PARAPET_TOP), (PARAPET_Y0 - po, PARAPET_BODY_TOP + 0.2)],
        "rail": [(RAIL_Y0, RAIL_FOOT), (RAIL_Y1, RAIL_FOOT), (RAIL_Y1, RAIL_BODY_TOP), (RAIL_Y0, RAIL_BODY_TOP)],
        "rail_cap": [(RAIL_Y0 - ro, RAIL_BODY_TOP), (RAIL_Y1 + ro, RAIL_BODY_TOP),
                     (RAIL_Y1 + ro, RAIL_BODY_TOP + 0.05), (RAIL_Y1, RAIL_TOP), (RAIL_Y0, RAIL_TOP),
                     (RAIL_Y0 - ro, RAIL_BODY_TOP + 0.05)],
    }


def parapet_openings(length):
    """Centres, from the low end of a parapet part of `length` metres (1.0 or more), of its pointed
    openings: one per OPEN_PITCH metres, evenly spaced. For every length from 1.0 up, an opening of
    OPEN_W has at least OPEN_MARGIN of stone at each end and 0.4 m to its neighbour (tested). A 4 m
    part has two, at 1 m and 3 m."""
    if length < 1.0 - 1e-9:
        raise ValueError(f"a parapet part is at least 1.0 m long, got {length}")
    n = max(1, int(math.floor(length / OPEN_PITCH + 0.5)))
    return [(i + 0.5) * length / n for i in range(n)]


# ---- arches ------------------------------------------------------------------------------------------
# The blind arches of a bay: three, each ARCH_W wide, at the bay centre and ARCH_STEP to each side. The band
# under them ends at the outer faces of the outer legs (arch_band_spans), 2.6 m from the bay centre.
ARCH_Z0, ARCH_W, ARCH_H, ARCH_MOULD, ARCH_PROUD = 3.95, 1.6, 4.55, 0.14, 0.12
ARCH_STEP = 1.8
ARCH_STEPS_FINE = 16  # steps of each arc of a pointed arch: 60 / 16 = 3.75 degrees between neighbour segments


def arch_band_spans(centres, x_lo, x_hi, pier_xs, arch_w=None, arch_step=None):
    """The stretches of the carved band of a run, from the blind arch centres in it: each group of arches
    (centres one ARCH_STEP apart) has the band from the outer face of its first leg to the outer face of its
    last leg. A group at a cut end of the run (x_lo or x_hi, with no pier between the end and the group) has
    the band run on to the cut, so that a stub keeps the band to the drum or the gatehouse. A group next to a
    pier stops at the leg, so the band and the arch group are the same width in every bay."""
    w = ARCH_W if arch_w is None else arch_w
    step = ARCH_STEP if arch_step is None else arch_step
    groups = []
    for c in sorted(centres):
        if groups and c - groups[-1][-1] < step + 1e-6:
            groups[-1].append(c)
        else:
            groups.append([c])
    spans = []
    for g in groups:
        lo, hi = g[0] - w / 2.0, g[-1] + w / 2.0
        if not any(x_lo - 1e-6 <= p <= lo for p in pier_xs):
            lo = x_lo
        if not any(hi <= p <= x_hi + 1e-6 for p in pier_xs):
            hi = x_hi
        spans.append((lo, hi))
    return spans


def pointed_arch(width, height, steps):
    """Outline of an equilateral pointed arch standing on v = 0, from the left foot over the apex to the
    right foot, as (u, v) points."""
    rise = width * math.sqrt(3.0) / 2.0
    spring = max(height - rise, 0.0)
    pts = [(-width / 2, 0.0), (-width / 2, spring)]
    for i in range(1, steps + 1):
        a = math.radians(180.0 - 60.0 * i / steps)
        pts.append((width / 2 + width * math.cos(a), spring + width * math.sin(a)))
    for i in range(1, steps + 1):
        a = math.radians(60.0 - 60.0 * i / steps)
        pts.append((-width / 2 + width * math.cos(a), spring + width * math.sin(a)))
    pts.append((width / 2, 0.0))
    return pts


def half_arch(width, height, steps):
    """The right half of a pointed arch, from the apex (0, height) down to the right foot (width / 2, 0)."""
    spring = height - width * math.sqrt(3.0) / 2.0
    pts = []
    for i in range(steps + 1):
        a = math.radians(60.0 * (1.0 - i / steps))
        pts.append((-width / 2 + width * math.cos(a), spring + width * math.sin(a)))
    pts.append((width / 2, 0.0))
    return pts


def signed_arc_lengths(points, apex):
    """Arc length of each point along the polyline, measured from the point `apex`: negative before it,
    positive after. The two halves of a recess cut by a joint plane meet at the apex with equal values."""
    cum = [0.0]
    for i in range(1, len(points)):
        cum.append(cum[-1] + math.dist(points[i], points[i - 1]))
    return [c - cum[apex] for c in cum]


def arc_length_at(points, lengths, u, v):
    """Signed arc length (from signed_arc_lengths) of the point on the polyline `points` that is nearest
    to (u, v). A vertex made by cutting a face lies on the polyline, so this gives it the length that
    belongs to it."""
    best, at = None, 0.0
    for i in range(len(points) - 1):
        (ax, ay), (bx, by) = points[i], points[i + 1]
        dx, dy = bx - ax, by - ay
        seg = dx * dx + dy * dy
        t = 0.0 if seg < 1e-18 else min(max(((u - ax) * dx + (v - ay) * dy) / seg, 0.0), 1.0)
        d = (u - (ax + t * dx)) ** 2 + (v - (ay + t * dy)) ** 2
        if best is None or d < best:
            best, at = d, lengths[i] + t * (lengths[i + 1] - lengths[i])
    return at


# ---- ribs ---------------------------------------------------------------------------------------------
# A rib spec is (start z, apex z, end angle in degrees, width at the pier, width at the apex, proud at the
# pier, proud at the apex, offset of the start from the pier centre line). The limb springs from inside the
# collar of the pier on a foot of the full width and tapers in a straight line to the apex.
RIB_LONG = (8.45, 10.3, 62.0, 0.36, 0.20, 0.52, 0.20, 0.24)
RIB_LOW = (8.45, 9.8, 62.0, 0.36, 0.18, 0.52, 0.16, 0.24)
# Version 6: both limbs of a pier spring from one seat inside the collar: the same foot (centre 0.24 m from the
# pier centre line, 0.36 m wide, so it spans 0.06 to 0.42 m, and the two limbs of a pier leave 0.12 m between
# them), starting at Z 8.45 a little under the front top edge of the collar (8.55), 0.52 m proud (the collar is
# 0.64). A limb stays inside the collar width (0.66 m) until its lower edge has passed the collar top at Z 8.8; a
# test holds it for every half bay the builders use. The low rib is 0.2 m higher than in version 5 (apex 9.8, end
# angle 62 degrees): a flatter limb swings out of the collar sooner than any collar of this width can carry. There
# is no flare: width and proud both taper at one straight rate from the foot to the apex.
RIB_MAX_TURN_DEG = 3.5     # the target turn between two neighbour segments of a limb
RIB_TURN_LIMIT_DEG = 4.0   # the gate: no two neighbour segments turn more than this
RIB_CHAMFER_SHARE = 0.18   # the chamfer of the section is this share of the width at every station


def rib_width(w0, w1, t):
    """Width of a rib at fraction t (0 at the pier, 1 at the apex): one straight taper."""
    return w0 + (w1 - w0) * t


def rib_proud(p0, p1, t):
    """Proud of a rib at fraction t: one straight taper, like the width."""
    return p0 + (p1 - p0) * t


def rib_section(w, front, back):
    """The moulded section of a limb as six (e, y) points, e across the limb (positive to the extrados, the upper
    side) and y out of the wall: the back edge (extrados, then intrados), the intrados side, the intrados
    chamfer, the extrados chamfer and the extrados side. The chamfer is RIB_CHAMFER_SHARE of the width, so the
    section keeps its shape from the foot to the apex."""
    hw = w / 2.0
    ch = RIB_CHAMFER_SHARE * w
    return [(hw, back), (-hw, back), (-hw, front - ch), (-hw + ch, front), (hw - ch, front), (hw, front - ch)]


def rib_u(arc, total):
    """U of a limb at arc length `arc` from its foot, `total` the length to its apex end: it runs from the apex end
    (U 0) at the kit's density on both limbs of a pair, so the two limbs show the same texels at mirror points."""
    return TD * (total - arc)


def rib_t(spec, z):
    """How far along its taper a rib is at height z (0 at the foot, 1 at the apex), from its quarter
    ellipse. The width and the proud of a rib depend on this, so a clipped rib has the width the whole
    rib has at that point."""
    z0, apex, deg = spec[0], spec[1], spec[2]
    th = math.radians(deg)
    b = (apex - z0) / math.sin(th)
    return min(max(math.asin(min(max((z - z0) / b, 0.0), 1.0)) / th, 0.0), 1.0)


def rib_clip_min(spec):
    """The nearest clip plane (as an offset from the pier centre line) that a rib can have: the far edge of
    its foot, plus 2 cm. A plane nearer than this would cut the foot and leave part of it on the far side."""
    return spec[7] + rib_width(spec[3], spec[4], 0.0) / 2.0 + 0.02


def rib_clip_centre(spec, off, half_bay=None):
    """Where the centre line of a rib must end so that the rib is cut by the plane at offset `off` from the pier
    centre line: the far edge of the last section, which is as wide as the rib and tilted by the curve, then
    lies on the plane, and no earlier section crosses it. None when the plane would cut the foot (nearer than
    rib_clip_min)."""
    if off < rib_clip_min(spec):
        return None
    e = off
    for _ in range(80):
        pts = rib_curve(1, spec, 0.0, 1, half_bay=half_bay, clip_off=e)
        if not pts:
            return None
        _, z, tx, tz = pts[-1]
        hw = rib_width(spec[3], spec[4], rib_t(spec, z)) / 2.0
        nxt = off - hw * abs(tz) / math.hypot(tx, tz)
        if abs(nxt - e) < 1e-12:
            break
        e = nxt
    return e if e > spec[7] else None


def clip_offset(sign, xc, clip_x):
    """The clip plane X = clip_x as an offset from the pier centre line xc, along the rib's own side."""
    return sign * (clip_x - xc)


def _equal_turn_params(a, b, t_end, steps, max_turn_deg):
    """Parameters 0 to t_end of the ellipse x = a (1 - cos t), z = b sin t at which its tangent has turned by equal
    angles: as many as the turn needs for `max_turn_deg` per step, and at least `steps` steps."""
    def psi(t):
        return math.atan2(b * math.cos(t), a * math.sin(t)) if t > 0.0 else math.pi / 2.0

    total = psi(0.0) - psi(t_end)
    n = max(steps, int(math.ceil(total / math.radians(max_turn_deg) - 1e-9)), 1)
    out = [0.0]
    for i in range(1, n):
        want = psi(0.0) - total * i / n
        lo, hi = 0.0, t_end
        for _ in range(60):
            mid = 0.5 * (lo + hi)
            if psi(mid) > want:
                lo = mid
            else:
                hi = mid
        out.append(0.5 * (lo + hi))
    out.append(t_end)
    return out


def rib_curve(sign, spec, over_deg, steps, half_bay=None, clip_off=None, max_turn_deg=None):
    """Centre line of a rib that leaves a pier going straight up and bends toward the bay centre: a quarter
    ellipse that reaches `half_bay` (default: HALF_BAY, read when called) from the pier centre line at the
    apex height with the end angle. `clip_off` cuts the curve where its offset from the pier centre line is
    that far (a value past the end changes nothing, one at or before the foot's centre gives []).
    Returns (x offset from the pier centre, z, tangent x, tangent z) per step, steps + 1 points. With
    `max_turn_deg` the points stand where the tangent has turned by equal angles, at most that much from one
    segment to the next (RIB_MAX_TURN_DEG), and `steps` is the least number of steps; without it the points
    are equal steps of the ellipse parameter."""
    hb = HALF_BAY if half_bay is None else half_bay
    z0, apex, deg, _, _, _, _, d = spec
    th = math.radians(deg)
    a = (hb - d) / (1.0 - math.cos(th))
    b = (apex - z0) / math.sin(th)
    t_end = math.radians(deg + over_deg)
    if clip_off is not None:
        if clip_off <= d + 1e-12:
            return []
        t_clip = math.acos(max(-1.0, 1.0 - (clip_off - d) / a))
        if t_clip < t_end:
            t_end = t_clip
    out = []
    ts = [t_end * i / steps for i in range(steps + 1)] if max_turn_deg is None else _equal_turn_params(
        a, b, t_end, steps, max_turn_deg)
    for t in ts:
        out.append((sign * (d + a * (1.0 - math.cos(t))), z0 + b * math.sin(t), sign * a * math.sin(t), b * math.cos(t)))
    if clip_off is not None and out and t_end < math.radians(deg + over_deg):
        out[-1] = (sign * clip_off,) + out[-1][1:]
    return out


def cut_direction(tan_x, tan_z, sign):
    """Unit direction (x, z) along which the end of a swept rib is carried to the clip plane. It is the
    rib's tangent, turned away from the plane's own direction when the tangent is nearly parallel to the
    plane (a rib near its pier is almost vertical), so that the carry is never longer than 2.5 times the
    distance to the plane. `sign` is the side of the pier."""
    n = math.hypot(tan_x, tan_z)
    ux = tan_x / n if n > 1e-12 else 0.0
    if sign * ux >= 0.4:
        return (ux, tan_z / n)
    return (0.4 * sign, math.copysign(math.sqrt(1.0 - 0.16), tan_z if tan_z else 1.0))


# ---- the preview colours of the five materials -------------------------------------------------------
def darken_hex(h, share):
    """The colour `h` (six hex digits, no #) with `share` (0 to 1) taken off every sRGB channel."""
    return "".join("%02x" % int(round(int(h[i:i + 2], 16) * (1.0 - share))) for i in (0, 2, 4))


STONE_COLOUR, RUBBLE_COLOUR, TRIM_COLOUR = "c7b89e", "877a68", "ded6cb"
ROOF_COLOUR = "34405f"
HEAP_COLOUR = darken_hex(RUBBLE_COLOUR, 0.15)


# ---- triangulation without zero-area triangles --------------------------------------------------------
COLLINEAR_AREA = 1e-7  # twice the area of a triangle in m2 below which three corners count as on a line


def _cross(a, b, c):
    u = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    w = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    return (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])


def has_collinear_run(points, tol=COLLINEAR_AREA):
    """True when three consecutive corners of the polygon `points` (3D) lie on a line. A polygon cut into
    cells keeps such corners where a cut meets an edge; a triangulation from one of them has a triangle of
    no area, which gives the vertices round it a wrong tangent (a dark hairline in the normal map)."""
    n = len(points)
    if n < 4:
        return False
    for i in range(n):
        c = _cross(points[i - 1], points[i], points[(i + 1) % n])
        if math.sqrt(c[0] ** 2 + c[1] ** 2 + c[2] ** 2) <= tol:
            return True
    return False


def fan_apex(points, normal, tol=COLLINEAR_AREA):
    """Index of the corner from which the polygon `points` (3D, any winding, face normal `normal`) fans into
    triangles that all have an area (twice the area above `tol`) and the polygon's own orientation, so the
    fan covers it without a zero-area or flipped triangle; the corner with the best smallest triangle. None
    when no corner will do."""
    n = len(points)
    total = 0.0
    for i in range(n):
        c = _cross((0.0, 0.0, 0.0), points[i], points[(i + 1) % n])
        total += c[0] * normal[0] + c[1] * normal[1] + c[2] * normal[2]
    sign = 1.0 if total >= 0.0 else -1.0
    best, best_min = None, 0.0
    for k in range(n):
        worst = None
        for i in range(1, n - 1):
            c = _cross(points[k], points[(k + i) % n], points[(k + i + 1) % n])
            a = sign * (c[0] * normal[0] + c[1] * normal[1] + c[2] * normal[2])
            worst = a if worst is None else min(worst, a)
        if worst is not None and worst > tol and (best is None or worst > best_min):
            best, best_min = k, worst
    return best


# ---- the cell grid that carries the vertex colour -----------------------------------------------------
CELL_MAX = 3.0               # no big flat face has a row taller than this (version 5: 1.1; the wash is weak, so rows are coarse)
CELL_SKIP = 1.25             # a face this narrow (a pier front is 1.2 m) is not cut across
CELL_TRIM = 3.4              # long carved members are cut along X into cells of at most this (a half bay)


def even_cuts(lo, hi, max_cell):
    """Cut positions strictly inside (lo, hi) that divide it into the fewest equal cells of at most
    `max_cell`. A span that fits one cell has no cut."""
    n = int(math.ceil((hi - lo) / max_cell - 1e-9))
    return [lo + (hi - lo) * k / n for k in range(1, n)]


TONE_COLUMNS = 6             # columns of the cell grid in one bay: the streaks of the tone stand on them


def column_cuts(lo, hi, u0):
    """Cut positions strictly inside (lo, hi) at every BAY / TONE_COLUMNS from `u0` (the low end of the piece,
    where a pier stands). The vertical cuts of a big face go here, so that the vertices of every bay have
    the same X phase and the streaks of vertex_tone land on vertices. A joint plane (a pier centre) is a
    column and is not a cut."""
    step = BAY / TONE_COLUMNS
    out = []
    k = int(math.floor((lo - u0) / step + 1e-9)) + 1
    while True:
        x = u0 + k * step
        if x >= hi - 1e-6:
            return out
        if x > lo + 1e-6:
            out.append(x)
        k += 1


# ---- the tone of a vertex -----------------------------------------------------------------------------
TONE_MIN = 0.80
DAMP_TOP = 1.5               # the damp at the foot fades out by this height
# Phases (fractions of a bay) of the drips. Each is a column of the cell grid (k / TONE_COLUMNS, never 0), so a
# streak is a ridge that is full on its column and gone on the next, whatever the width of the cells.
DRIP_STRING = (1 / 6, 4 / 6)             # under the string course
DRIP_CAP = (2 / 6, 5 / 6)                # under the cap of the parapet
DRIP_ARCH = (1 / 6, 3 / 6, 5 / 6)        # under the head of each blind arch
DRIP_SILL = (2 / 6, 4 / 6)               # under the sill (the band) of the blind arches, down the plinth
# (z of the source, length of the streak below it, strength, phases). The cap streak starts at the foot of the
# frieze (the frieze hides the slab above it).
STREAKS = ((STRING_Z0, 3.0, 0.065, DRIP_STRING),
           (FRIEZE_Z0, 2.0, 0.048, DRIP_CAP),
           (8.3, 3.5, 0.040, DRIP_ARCH),
           (BAND_Z0, 3.0, 0.050, DRIP_SILL))
# Extra heights where a big stone face is cut, so that a vertex stands just under the source of a streak that
# the cell rows would miss: 5 cm under the foot of the frieze (the parapet slab has rows at 12, 13 and 14).
TONE_ROWS = (FRIEZE_Z0 - 0.05,)


def damp(z):
    """1 at the ground and below, 0 from DAMP_TOP up, smooth between."""
    t = min(max((DAMP_TOP - z) / DAMP_TOP, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def _patch(angle):
    return 0.5 + 0.5 * math.cos(angle)


def wash(x, z):
    """The broad wash: soft patches that drift with the height, one and two to a bay, and a little more
    darkness low down. Periodic in the bay, so it is equal on both sides of every joint; it changes slowly
    with z there (a joint may be sampled on different rows by two pieces)."""
    a = 2.0 * math.pi * ((x / BAY) % 1.0)
    w1 = 0.0190 * _patch(a + 3.93 + 0.78 * math.sin(2.0 * math.pi * z / 7.08 + 0.4))
    w2 = 0.0218 * _patch(2.0 * a + 0.41 + 1.21 * math.sin(2.0 * math.pi * z / 10.19 + 2.0))
    return w1 + w2 + 0.020 * max(0.0, 1.0 - z / 14.0)


def streak(x, z):
    """Darkening of the rain streaks at X = x (from the piece's low end) and height z: a soft ridge under the
    string course, under the cap, under each arch head and under the sill. A ridge is full on its drip column
    and gone one column away, with the slow fall of (1 - u) squared down the streak. Nothing at a joint plane."""
    phase = (x / BAY) % 1.0
    half = 1.0 / TONE_COLUMNS
    total = 0.0
    for z_src, length, strength, phases in STREAKS:
        if z > z_src + 1e-9:
            continue
        u = (z_src - z) / length
        if u >= 1.0:
            continue
        v = (1.0 - u) ** 2
        for p in phases:
            d = abs(phase - p)
            d = min(d, 1.0 - d)
            if d < half - 1e-9:
                total += strength * v * math.cos(math.pi * d / (2.0 * half)) ** 2
    return total


def vertex_tone(kind, x, z, recess=0.0):
    """(r, g, b) of a vertex, each in TONE_MIN to 1.0: a grey with a slight green cast where damp and a
    slight warm cast in the rain streaks. `kind` is stone, rubble, trim, roof or heap (a heap is as damp as
    rubble, a roof is like stone); `x` is measured from the piece's low end; `recess` is 0 to 1 for the
    inside of a recess. The wash and the streaks repeat every bay, so the tone is equal on both sides of
    every joint. A carved member (trim) has a lighter wash and no streak."""
    wsh = wash(x, z)
    s = 0.0
    if kind == "trim":
        wsh *= 0.6
    else:
        s = streak(x, z)
    wet = damp(z)
    dark = wsh + (0.14 if kind in ("rubble", "heap") else 0.10) * wet + s + 0.07 * recess
    v = 1.0 - dark
    streak_share = min(s / 0.065, 1.0)
    r = v - 0.020 * wet - 0.012 * streak_share
    g = v
    b = v - 0.045 * wet - 0.030 * streak_share
    return tuple(min(max(c, TONE_MIN), 1.0) for c in (r, g, b))


# ---- texel density and the trim sheet --------------------------------------------------------------
STRIPS = {
    "S1": (0.000, 0.280),  # dressed stone, plain: ribs, plain bands
    "S2": (0.280, 0.490),  # cornice moulding: the string course and every cap rail
    "S3": (0.490, 0.700),  # interlace band: the carved band
    "S4": (0.700, 0.805),  # arch moulding: frames of blind arches and openings, rib edges
    "S5": (0.805, 1.000),  # blind arcade of small pointed arches: the frieze under the parapet cap
}
SLOPE_MIN = 0.02  # a normal component below this counts as zero


def slope_tz(n):
    """The Z part of the unit tangent that runs up a battered face (a normal with a Y part and a Z part
    and no X part): 1 for a vertical face, less for a leaning one."""
    return abs(n[1]) / math.hypot(n[1], n[2])


def is_battered(n):
    nx, ny, nz = (abs(c) for c in n)
    return nx < SLOPE_MIN and ny > SLOPE_MIN and nz > SLOPE_MIN


def box_uv(n, co, x_low):
    """(u, v) by the dominant direction of the normal, at TD per metre. Outer and inner faces (normal
    along Y): U = TD (x - x_low), V = TD z. End faces: U = TD y, V = TD z. Horizontal faces:
    U = TD (x - x_low), V = TD y."""
    nx, ny, nz = (abs(c) for c in n)
    x, y, z = co
    if nx >= ny and nx >= nz:
        return (TD * y, TD * z)
    if ny >= nz:
        return (TD * (x - x_low), TD * z)
    return (TD * (x - x_low), TD * y)


def world_uv(n, co, x_low):
    """(u, v) of a stone face, a world box projection at TD per metre. `x_low` is the low
    end of the piece, so that U starts at 0 there and the courses run through every joint.
    The box rule is box_uv. A battered face (a normal with both a Y and a Z part and no X part) takes V
    along the slope, so the density holds: V = TD z / tz, the distance along the slope from the ground
    line (Z 0), so that its courses stay level with the courses of the vertical faces beside it (a pier
    front leans 0.5 m over 8.5 m and its courses were 0.09 UV high at the corner in version 3)."""
    if is_battered(n):
        return (TD * (co[0] - x_low), TD * co[2] / slope_tz(n))
    return box_uv(n, co, x_low)


# ---- the plinth: a bed joint at V phase 0.3767 on the top edge ----------------------------------------
PLINTH_SLANT = math.hypot(FOUNDATION_Y_OUT - Y_OUT, Z_PLINTH - Z_GROUND)  # 3.0594 m along the batter
PLINTH_V_TOP = TD * PLINTH_SLANT                                          # 1.3767: the top edge, Z 3.0


def plinth_v_vertical(z):
    """V on a vertical plinth face at height z: TD z plus the constant that gives PLINTH_V_TOP at Z 3.0.
    The rule goes on below the ground."""
    return PLINTH_V_TOP + TD * (z - Z_PLINTH)


def plinth_v_slope(z, tz):
    """V on a battered plinth face at height z: TD times the distance along the slope from the ground
    line, with `tz` the Z part of the unit slope tangent (slope_tz). It is PLINTH_V_TOP at Z 3.0 on the
    plinth's own batter."""
    return TD * z / tz


def plinth_uv(n, co, x_low):
    """(u, v) of a face of the plinth material (t_rivendell_wall_rubble_a, now large smooth ashlar of three
    courses per tile). The plinth texture has a bed joint at V phase 0.3767, for the top edge of the
    plinth at Z 3.0, so V is exactly PLINTH_V_TOP there on every plinth face of every piece: on the
    battered outer face V runs along the slope from 0 at the ground line; on a vertical face (the inner
    face, an end face, the foundation face) V = plinth_v_vertical(z). U is the wall's U from the piece's low
    end (end faces: TD y); a horizontal face keeps the box rule."""
    nx, ny, nz = (abs(c) for c in n)
    x, y, z = co
    if is_battered(n):
        return (TD * (x - x_low), plinth_v_slope(z, slope_tz(n)))
    if nx >= ny and nx >= nz:
        return (TD * y, plinth_v_vertical(z))
    if ny >= nz:
        return (TD * (x - x_low), plinth_v_vertical(z))
    return (TD * (x - x_low), TD * y)


def heap_uv(n, co, x_low=0.0):
    """(u, v) of a face of the heap material (t_rivendell_wall_heap_a: broken stone with no courses, for
    the heap and the torn core of the breach): a box projection at TD by the face's own dominant direction,
    so a heap face has no course direction and the texture needs no bed joint."""
    return box_uv(n, co, x_low)


def roof_uv(n, co, x_low=0.0, axis=None, eave=0.0, seam=math.pi):
    """(u, v) of a face of the roof slate (t_rivendell_roof_slate_a): U along the eave (the horizontal
    direction in the face plane) and V up the slope, both at TD per metre, so a facet keeps the pinned
    density. A flat face (a normal with no horizontal part) takes the horizontal box rule.

    A gable face (`axis` None): U = TD (x - x_low) for a roof whose ridge runs along X (the horizontal
    direction is turned so that U grows with x, or with y for a ridge along Y); V is TD times the distance
    up the slope, measured from the world origin.

    A spire or a cone (`axis` = (x, y) of its vertical axis, `eave` = the length of the whole eave ring):
    the facet takes U from the azimuth of its normal, TD (eave / 2 pi phi + t), with t the offset along the
    eave from the middle of the facet. For a regular pyramid whose eave ring has that length, U is
    continuous round the ring at the eave, and a facet is isometric (both scales TD). The ring cannot close
    in U: it jumps by TD eave at azimuth `seam` (radians, default 180 degrees), so put the seam on the back.
    V is TD times the distance up the slope measured in the plane of the facet from the axis, equal on every
    facet at one height. The normal is the face normal (`n`, unit)."""
    nx, ny, nz = n
    x, y, z = co
    h = math.hypot(nx, ny)
    if h < SLOPE_MIN:
        return (TD * (x - x_low), TD * y)
    hx, hy = -ny / h, nx / h                  # along the eave, counter-clockwise seen from above
    sx, sy, sz = -nz * nx / h, -nz * ny / h, h  # up the slope
    if axis is None:
        if hx < -1e-9 or (abs(hx) <= 1e-9 and hy < 0):
            hx, hy = -hx, -hy
        return (TD * (hx * (x - x_low) + hy * y), TD * (sx * x + sy * y + sz * z))
    cx, cy = axis
    phi = math.atan2(ny + 0.0, nx)
    if phi > seam + 1e-9:
        phi -= 2.0 * math.pi
    t = hx * (x - cx) + hy * (y - cy)
    return (TD * (eave / (2.0 * math.pi) * phi + t), TD * (sx * (x - cx) + sy * (y - cy) + sz * z))


def trim_uv(n, co, strip, long_axis, lo, hi, x_low=0.0):
    """(u, v) of a trim face. `long_axis` (0, 1 or 2) is the way the member runs: U = TD times the
    coordinate along it (from `x_low` when that is X), and V spans the strip across the member's
    bounding box (lo, hi). A face whose normal is the long axis takes U from the first free axis."""
    a = [abs(c) for c in n]
    d = a.index(max(a))
    others = [i for i in range(3) if i != long_axis]
    if d != long_axis:
        across = [i for i in others if i != d][0]
        u_axis = long_axis
    else:
        u_axis, across = others
    ext = hi[across] - lo[across]
    t = (co[across] - lo[across]) / ext if ext > 1e-6 else 0.5
    t = min(max(t, 0.0), 1.0)
    v0, v1 = STRIPS[strip]
    u = TD * (co[u_axis] - (x_low if u_axis == 0 else 0.0))
    return (u, v0 + (v1 - v0) * t)


# V of the two corners of each face of a limb's section (see rib_section: the back, the intrados side, the intrados
# chamfer, the front, the extrados chamfer, the extrados side), one table for both limbs of a pair. The faces that
# mirror each other (sides 1 and 5, chamfers 2 and 4) carry the same V at the same place of the section, and the
# back and the front carry V 0.000 on the extrados and 0.280 on the intrados (strip S1), so a limb and its mirror
# show the same texel at every pair of mirror points.
RIB_FACE_V = ((STRIPS["S1"][0], STRIPS["S1"][1]), (STRIPS["S4"][0], STRIPS["S4"][1]), (STRIPS["S4"][0], STRIPS["S4"][1]),
              (STRIPS["S1"][1], STRIPS["S1"][0]), (STRIPS["S4"][1], STRIPS["S4"][0]), (STRIPS["S4"][1], STRIPS["S4"][0]))


def uv_scale_axes(p, uvs):
    """The two scales of a triangle's UV mapping, in UV units per metre (the singular values of
    the map from the face plane to UV). Equal values mean no stretch."""
    e1 = [p[1][i] - p[0][i] for i in range(3)]
    e2 = [p[2][i] - p[0][i] for i in range(3)]
    l1 = math.sqrt(sum(c * c for c in e1))
    if l1 < 1e-12:
        return (0.0, 0.0)
    a = [c / l1 for c in e1]
    proj = sum(e2[i] * a[i] for i in range(3))
    perp = [e2[i] - proj * a[i] for i in range(3)]
    l2 = math.sqrt(sum(c * c for c in perp))
    if l2 < 1e-12:
        return (0.0, 0.0)
    d1 = (uvs[1][0] - uvs[0][0], uvs[1][1] - uvs[0][1])
    d2 = (uvs[2][0] - uvs[0][0], uvs[2][1] - uvs[0][1])
    # world matrix W = [[l1, proj], [0, l2]] (columns are vertices 1 and 2), D = [d1 d2] as columns,
    # J = D * inverse(W)
    inv = ((1.0 / l1, -proj / (l1 * l2)), (0.0, 1.0 / l2))
    j = [[d1[r] * inv[0][c] + d2[r] * inv[1][c] for c in range(2)] for r in range(2)]
    s = j[0][0] ** 2 + j[0][1] ** 2 + j[1][0] ** 2 + j[1][1] ** 2
    det_j = j[0][0] * j[1][1] - j[0][1] * j[1][0]
    root = math.sqrt(max(s * s - 4.0 * det_j * det_j, 0.0))
    big = math.sqrt((s + root) / 2.0)
    small = math.sqrt(max((s - root) / 2.0, 0.0))
    return (small, big)
