"""Plan geometry of the Rivendell domed gatehouse, pure Python (no bpy).
Tests: tools/tests/test_rivendell_gatehouse_geometry.py. Builder: blender_build_rivendell_gatehouse.py.

Frame (the wall's, tools/oneoff/rivendell_joint_profile.py): metres, X across the passage, the outer face is
+Y, Z up, Z = 0 the ground and the passage floor, the deck at Z 12. The gatehouse is 2 XE = 26.667 m wide:
a 20 m body (|X| <= XB) between two stubs of real wall, each half a bay (3.333 m) long and ending in the
joint profile at |X| = XE. The body is 12.4 m deep with a round drum at each outer corner; a drum's circle
is tangent to the plane |X| = XB, so it meets the stub's outer face square and the whole pier at |X| = XB
(1.2 m wide) covers the meeting.

A "path" is a list of (x, y) points. Travel keeps the outside on the LEFT, so the left normal is the outward
one. mitre_normals() gives each point the normal that, scaled by an offset d, stays d from both edges.
"""
import math

import rivendell_joint_profile as jp

XB = 10.0                       # body half width; the stubs run from here to XE
XE = XB + jp.HALF_BAY           # the joint planes, +-13.333
STUB_X_IN = XB                  # the stub's wall starts on the drum's tangent plane: there is no pier at XB
STUB_PARAPET_X0 = XB            # the stub's parapet starts on the drum's tangent plane, where the drum's ring ends
HALF_D = 6.2                    # body half depth (Y -6.2 .. 6.2)
HALL_HALF_W, HALL_Y, HALL_CEIL = 4.2, 4.6, 9.6   # the hall: 8.4 m wide, 9.2 m deep, vault to 9.6
DOOR_HALF_W, DOOR_TOP_Z = 2.35, 10.0            # the door tunnels: 4.7 m wide, the arch apex at 10.0
SOOT_MIN = 0.55                 # no channel of the passage tone goes below this
FACADE_Y = 5.4                  # outer face between the drum and the door frame
PORTAL_HALF_W = 3.0             # the door frame stands proud to Y 6.2
DRUM_R, DRUM_CX, DRUM_CY = 3.3, 6.7, 2.9
BATTER = jp.FOUNDATION_Y_OUT - jp.Y_OUT   # 0.6: the plinth, as the wall's
THETA_FRONT = math.pi - math.asin((FACADE_Y - DRUM_CY) / DRUM_R)  # where the drum leaves the facade plane
STUB_TAIL_Y = 2.4               # a drum sweep ends here, inside the stub's mass (the stub's face is Y 2.5)
ARC_STEPS = 36                  # version 6: 130.8 / 36 = 3.63 degrees between two neighbour segments of a drum


def path_turns_deg(path):
    """The turn in degrees at every inner point of a polyline: 0 on a straight run, 90 at a right angle."""
    out = []
    for a, b, c in zip(path, path[1:], path[2:]):
        d1 = math.atan2(b[1] - a[1], b[0] - a[0])
        d2 = math.atan2(c[1] - b[1], c[0] - b[0])
        out.append(abs((math.degrees(d2 - d1) + 180.0) % 360.0 - 180.0))
    return out


def sharp_rings(path, smooth_deg):
    """Indices of the inner points of `path` where it turns by more than `smooth_deg`: a swept member keeps a hard edge
    at those rings and is shaded smooth at every other one."""
    return [i + 1 for i, t in enumerate(path_turns_deg(path)) if t > smooth_deg]


def mirrored_u(cum):
    """U (in metres) of each ring of a path from its cumulative lengths: the distance from the middle of the path, so
    that a path that is a mirror image of itself carries the same U at mirror points."""
    mid = cum[-1] / 2.0
    return [abs(c - mid) for c in cum]


def mitre_normals(path, first_n=None, last_n=None):
    """Per point of `path`, the left normal scaled so that an offset d lies d from both neighbouring edges
    (a straight run gives the unit normal; a right angle gives (1, 1) up to sign). `first_n` and `last_n`
    replace the normal at the two ends, so a sweep can start in a given plane."""
    seg = []
    for i in range(len(path) - 1):
        dx, dy = path[i + 1][0] - path[i][0], path[i + 1][1] - path[i][1]
        n = math.hypot(dx, dy)
        seg.append((-dy / n, dx / n))
    out = []
    for i in range(len(path)):
        a = seg[i - 1] if i > 0 else seg[0]
        b = seg[i] if i < len(path) - 1 else seg[-1]
        k = 1.0 + a[0] * b[0] + a[1] * b[1]
        out.append(((a[0] + b[0]) / k, (a[1] + b[1]) / k))
    if first_n is not None:
        out[0] = tuple(first_n)
    if last_n is not None:
        out[-1] = tuple(last_n)
    return out


def offset_path(path, d, first_n=None, last_n=None):
    return [(p[0] + n[0] * d, p[1] + n[1] * d) for p, n in zip(path, mitre_normals(path, first_n, last_n))]


def arc_right(steps=ARC_STEPS, r=DRUM_R):
    """The right drum's front arc, from where it leaves the facade plane (D) to the tangent point (E),
    clockwise seen from +Z, so the outside is on the left."""
    return [(DRUM_CX + r * math.cos(THETA_FRONT * (1.0 - i / steps)),
             DRUM_CY + r * math.sin(THETA_FRONT * (1.0 - i / steps))) for i in range(steps + 1)]


def drum_path(side, steps=ARC_STEPS, tail_y=STUB_TAIL_Y):
    """A drum's front arc in travel order, then one point carried into the stub's mass (`tail_y`; a member that has
    a twin on the stub stops on the front face of that twin instead, so that the two share no volume). side +1 is
    the right drum (D to E to the tail); side -1 is the mirror image, kept in travel order (tail first)."""
    right = arc_right(steps) + [(XB, tail_y)]
    if side > 0:
        return right
    return [(-x, y) for x, y in right][::-1]


def front_path(steps=ARC_STEPS):
    """The whole outer front from the left tangent point to the right one: left drum, facade, door frame,
    facade, right drum."""
    arc = arc_right(steps)
    left = [(-x, y) for x, y in arc][::-1]
    portal = [(-PORTAL_HALF_W, FACADE_Y), (-PORTAL_HALF_W, HALF_D), (PORTAL_HALF_W, HALF_D),
              (PORTAL_HALF_W, FACADE_Y)]
    return left + portal + arc


def core_outline(steps=ARC_STEPS):
    """The stone body's footprint: the 20 x 12.4 box with the drum fronts and the door frame."""
    return [(-XB, -HALF_D), (XB, -HALF_D)] + front_path(steps)[::-1]


def foundation_outline(steps=ARC_STEPS):
    """The footprint of the foundation: the body with its outer front pushed out by the batter (the ground end
    of the plinth). The inner face and the two ends stay flat."""
    fp = front_path(steps)
    off = offset_path(fp, BATTER)
    # the two ends step in to the stub's tail line, never along the line of the outline's own end points: three
    # collinear points make the tessellator emit zero-area triangles
    return off + [(XB, STUB_TAIL_Y), (XB, -HALF_D), (-XB, -HALF_D), (-XB, STUB_TAIL_Y)]


def cyl_theta(side, x, y):
    """Angle of (x, y) round a drum's centre, 0 on the side toward the stub, growing toward the facade."""
    return math.atan2(y - DRUM_CY, side * (x - side * DRUM_CX))


def cyl_uv(side, x, y, z, r):
    """(u, v) of a point on a drum's side: U = TD times the arc length at radius `r`, V = TD z, so the
    courses stay level with the wall's."""
    return (jp.TD * r * cyl_theta(side, x, y), jp.TD * z)


# ---- version 6: the blind arches of a drum, the statue wing and the door ring ---------------------------
# The right drum's circle is read by an angle THETA from the +X axis: 0 at the tangent point toward the stub, THETA_FRONT
# at the facade end. A viewer 10 to 20 m in front of the door sees only the part of the drum that faces him, so an arch
# near the stub is not seen whole from the front: it is dropped, and the arches that stay stand clear of the facade end.
DRUM_ARCH_MARGIN = 0.7          # plain drum between the facade end and the nearest arch (metres of arc)
DRUM_ARCH_GAP = 0.7             # plain drum between two arches (metres of arc)
VIEW_DISTANCES = (10.0, 20.0)   # a viewer on the centre line, this far in front of the door plane


def drum_visible_from_deg(viewer):
    """The least THETA (degrees) of the right drum that a viewer at (x, y) sees: the tangent from the viewer to the circle."""
    dx, dy = viewer[0] - DRUM_CX, viewer[1] - DRUM_CY
    dist = math.hypot(dx, dy)
    return math.degrees(math.atan2(dy, dx) - math.acos(DRUM_R / dist))


def drum_arch_centres_deg():
    """THETA (degrees, ascending) of the blind arches of a drum. The nearest to the facade leaves DRUM_ARCH_MARGIN of plain
    drum; the next ones follow at DRUM_ARCH_GAP; one whose near edge a viewer on the centre line at 10 to 20 m would
    not see is left out."""
    half = math.degrees(jp.ARCH_W / 2.0 / DRUM_R)
    pitch = math.degrees((jp.ARCH_W + DRUM_ARCH_GAP) / DRUM_R)
    last = math.degrees(THETA_FRONT) - math.degrees(DRUM_ARCH_MARGIN / DRUM_R) - half
    limit = max(drum_visible_from_deg((0.0, HALF_D + d)) for d in VIEW_DISTANCES)
    out = [last]
    while out[-1] - pitch - half >= limit:
        out.append(out[-1] - pitch)
    return tuple(sorted(out))


DRUM_ARCH_DEG = drum_arch_centres_deg()

DRUM_BAND_STEP_DEG = 3.9
DRUM_BAND_OVER = 0.02           # the band ends this far (metres of arc) past the arch's outer leg, so that the end face of the
                                # band and the outer face of the leg are two planes 2 cm apart, never one (the overlap check
                                # counts planes within 2 mm, measured from the world origin, as one)


def drum_band_end_deg():
    """THETA (degrees) where the carved band of a drum ends: the outer edge of the nearest blind arch (and DRUM_BAND_OVER), so
    that the band and the group of arches have the same extent, as in a bay of the wall (jp.arch_band_spans)."""
    return max(DRUM_ARCH_DEG) + math.degrees((jp.ARCH_W / 2.0 + DRUM_BAND_OVER) / DRUM_R)


def drum_band_path(side, tail_y=STUB_TAIL_Y):
    """The path of the carved band round a drum, in travel order like drum_path: it starts on the circle at the outer edge of
    the nearest blind arch, runs to the tangent point (segments of at most DRUM_BAND_STEP_DEG) and goes on to one point on
    the stub's face (`tail_y`). Side -1 is the mirror image, in travel order."""
    end = math.radians(drum_band_end_deg())
    n = max(1, math.ceil(end / math.radians(DRUM_BAND_STEP_DEG) - 1e-9))
    right = [(DRUM_CX + DRUM_R * math.cos(end * (1.0 - i / n)), DRUM_CY + DRUM_R * math.sin(end * (1.0 - i / n)))
             for i in range(n + 1)] + [(XB, tail_y)]
    if side > 0:
        return right
    return [(-x, y) for x, y in right][::-1]


# The statue wing is the stretch of facade between the door frame and the drum. The statue stands in the middle of it, on a
# pedestal, in front of a pointed niche cut into the facade. The kit statue is 0.744 by 0.835 m at scale 1.
WING_X0 = PORTAL_HALF_W
WING_X1 = DRUM_CX + DRUM_R * math.cos(THETA_FRONT)
STATUE_SOURCE_W, STATUE_SOURCE_D = 0.744, 0.835


def drum_foot_x():
    """The least X of the right drum's battered plinth at the ground, where the sweep starts at the facade end: the outer corner of
    the start cap. It is where the drum is widest, and it lies inside the wing between the door frame and the drum (the drum at
    the facade is at WING_X1, 0.38 m further out)."""
    path = drum_path(1)
    n = mitre_normals(path)[0]
    return path[0][0] + n[0] * BATTER


def drum_cap_edge_x(z):
    """X of the outer edge of the plinth's start cap at height z (0 to Z_PLINTH): it leans from the foot corner back to the
    facade end of the drum at the top of the plinth."""
    t = min(max(z / jp.Z_PLINTH, 0.0), 1.0)
    return drum_foot_x() + (WING_X1 - drum_foot_x()) * t


# The pedestal keeps one small clear distance to the door frame (a vertical face at X = PORTAL_HALF_W) and to the foot of the drum,
# each measured where that neighbour is widest: its cap (the widest part of the pedestal) against the door frame and its body at the
# ground against the corner of the drum's foot. The statue is as wide as the body allows.
STATUE_CLEAR = 0.21
PEDESTAL_H, PEDESTAL_CAP_H, PEDESTAL_CAP_OVER = 1.2, 0.15, 0.04
_CAP_MIN = PORTAL_HALF_W + STATUE_CLEAR
_BODY_MAX = drum_foot_x() - STATUE_CLEAR
PEDESTAL_W = _BODY_MAX - _CAP_MIN - PEDESTAL_CAP_OVER                  # the body's width
PEDESTAL_CAP_W = PEDESTAL_W + 2.0 * PEDESTAL_CAP_OVER
STATUE_X = _CAP_MIN + PEDESTAL_CAP_W / 2.0
PEDESTAL_D = 0.9
STATUE_Y = HALF_D - PEDESTAL_D / 2.0                                   # the pedestal's front is in the plane of the door frame's
STATUE_SCALE = math.floor((PEDESTAL_W - 0.12) / STATUE_SOURCE_W * 100.0) / 100.0
NICHE_HALF_W = round(min(0.5, STATUE_X - PORTAL_HALF_W - 0.25), 2)


def pedestal_half_w(z):
    """Half the width of the pedestal at height z: the body, and the cap over its top PEDESTAL_CAP_H."""
    return PEDESTAL_CAP_W / 2.0 if z > PEDESTAL_H - PEDESTAL_CAP_H else PEDESTAL_W / 2.0


def pedestal_clearances(z):
    """(to the door frame, to the drum) in metres at height z of the pedestal: the gap between its side and the door frame, and
    between its side and the drum's plinth cap edge (the start cap of the sweep, drum_cap_edge_x)."""
    h = pedestal_half_w(z)
    return (STATUE_X - h - PORTAL_HALF_W, drum_cap_edge_x(z) - (STATUE_X + h))

# The door ring is an offset of the opening (concentric arcs) whose tip stops on the underside of the string course, and it
# stands less proud than the string courses it meets (outer face 0.15, inner face 0.12).
DOOR_STEPS = 22                 # 76 degrees of arc in 22 steps: 3.5 degrees between two neighbour segments
RING_PROUD = 0.10
RING_TIP_Z = jp.STRING_Z0
DOOR_SPRING, DOOR_RISE = 7.0, 3.0   # the opening: 7.0 m of straight jamb, then 3.0 m of arch (apex at 10.0)
FRONT_COURSE_Z0, FRONT_COURSE_Z1 = jp.STRING_Z0, jp.STRING_Z1


def ring_moulding(half_w, spring, rise, tip_z):
    """The width of the ring round an opening of half width `half_w`, springing at `spring` with `rise` over it, that makes
    the ring's outer arcs, concentric with the opening's, end in a tip at height `tip_z`."""
    r = (half_w * half_w + rise * rise) / (2.0 * half_w)
    c = r - half_w
    return math.sqrt(c * c + (tip_z - spring) ** 2) - r


DOOR_RING_M = ring_moulding(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, RING_TIP_Z)   # about 0.49 m


def ring_outline(half_w, spring, rise, tip_z, steps):
    """The outer outline of a door ring, foot to foot over the tip, as (x, z): straight up to the spring line, then arcs
    concentric with the opening's (a mirror pair about X = 0, `steps` segments an arc)."""
    r = (half_w * half_w + rise * rise) / (2.0 * half_w)
    c = r - half_w
    rho = r + ring_moulding(half_w, spring, rise, tip_z)
    end = math.acos(-c / rho)
    left = [(-rho + c, 0.0)]
    for i in range(steps + 1):
        phi = math.pi + (end - math.pi) * i / steps
        left.append((c + rho * math.cos(phi), spring + rho * math.sin(phi)))
    left[-1] = (0.0, spring + math.sqrt(rho * rho - c * c))
    return left + [(-x, y) for x, y in reversed(left[:-1])]


def front_course_path(tail_y):
    """The path of the string course across the whole front: from the left stub (the tail at X -XB, `tail_y`) round the
    left drum, along the facade, round the door frame, along the facade and round the right drum to the right stub. The
    middle of the door frame's front is a point of the path, so that U (mirrored from the middle) rises on both sides of it
    and the long front face of the frame is not mapped to one value."""
    fp = front_path()
    half = len(fp) // 2
    return [(-XB, tail_y)] + fp[:half] + [(0.0, HALF_D)] + fp[half:] + [(XB, tail_y)]


# ---- the stub's rib arch: a smaller arch between the joint pier and a corbel at the drum ---------------
# The wall puts three blind arches in a bay, 1.8 m apart (blender_build_rivendell_wall.ARCH_STEP). Only the
# one at XB + 1.8 fits in a stub. The ribs make one complete pointed arch over it: a limb from the joint
# pier and a limb from a corbel on the wall face, both ending at that arch's centre line.
STUB_ARCH_X = XB + 1.8
STUB_RIB_REACH = XE - STUB_ARCH_X                  # half_bay of both limbs: 1.533 m
STUB_CORBEL_X = STUB_ARCH_X - STUB_RIB_REACH       # the corbel's centre line: 10.267 m


def rib_foot_inner_offset(spec, half_bay):
    """How far from the pier's centre line, on the bay side, the inner edge of a rib limb lies where the limb leaves the collar: the
    lower edge of its front (the corner of the section at the foot of the intrados chamfer) at the height of the collar's sloping
    top at that corner's depth. `half_bay` is the reach of the limb (jp.rib_curve)."""
    z0, _apex, _deg, w0, w1, p0, p1, _d = spec
    z_top, y_top = jp.PIER_TOP_Z + 0.05, jp.Y_OUT + jp.COLLAR_PROUD
    slope = (jp.COLLAR_Z1 - z_top) / (jp.COLLAR_PROUD - jp.PILASTER_PROUD)    # the collar's top rises as it goes back
    pts = jp.rib_curve(1, spec, 0.0, 4000, half_bay=half_bay)
    prev = None
    for u, z, tx, tz in pts:
        n = math.hypot(tx, tz)
        ex, ez = -tz / n, tx / n
        t = jp.rib_t(spec, z)
        w = jp.rib_width(w0, w1, t)
        front = jp.Y_OUT + jp.rib_proud(p0, p1, t)
        e, y = jp.rib_section(w, front, jp.Y_OUT - 0.1)[2]
        x, zz = u + e * ex, z + e * ez
        gap = zz - (z_top + (y_top - y) * slope)
        if gap >= 0.0:
            if prev is None:
                return x
            px, pg = prev
            return px + (x - px) * (-pg) / (gap - pg)
        prev = (x, gap)
    raise ValueError("the limb never leaves the collar")


# The bay-side end of the stub corbel's head lies on the inner edge of the limbs' feet where they meet the collar top: the outer
# of the two limbs (the lower one) leaves the collar last.
STUB_COLLAR_IN = max(rib_foot_inner_offset(spec, STUB_RIB_REACH) for spec in (jp.RIB_LONG, jp.RIB_LOW))


CORBEL_HEAD_Z0 = 8.12           # the underside of the head, on which the steps hang
# the three steps under the head: (half width, proud of the wall face, lowest Z, highest Z, chamfer of the underside)
CORBEL_STEPS = ((0.52, 0.46, 7.86, CORBEL_HEAD_Z0, 0.12),
                (0.38, 0.30, 7.62, 7.86, 0.10),
                (0.24, 0.14, 7.44, 7.62, 0.10))


def corbel_profile():
    """The head of the corbel in (y, z), counter-clockwise and convex: a bracket on the wall face (Y 2.5) with
    the collar's flat face (Y 3.14, Z 8.28 to 8.55) that slopes back under the string course and is chamfered
    under the flat face. The limbs of the stub arch spring from inside its flat face. Its back is buried in
    the wall."""
    return [(jp.Y_OUT - 0.1, CORBEL_HEAD_Z0), (jp.Y_OUT + 0.50, CORBEL_HEAD_Z0),
            (jp.Y_OUT + jp.COLLAR_PROUD, jp.COLLAR_Z0 + 0.13), (jp.Y_OUT + jp.COLLAR_PROUD, jp.PIER_TOP_Z + 0.05),
            (jp.Y_OUT + jp.PILASTER_PROUD, jp.COLLAR_Z1), (jp.Y_OUT - 0.1, jp.COLLAR_Z1)]


def corbel_tiers():
    """The carved corbel of a stub as a list of (half width, (y, z) profile), the head first and then three
    steps that taper down to the wall. Every profile is convex and counter-clockwise with its back buried
    0.1 m in the wall; a step has a chamfered underside; each tier hangs from the one above with no gap, is
    narrower and less proud, and is a prism along X (centred on STUB_CORBEL_X)."""
    tiers = [(jp.COLLAR_HALF_W, corbel_profile())]
    for half_w, proud, z0, z1, ch in CORBEL_STEPS:
        y = jp.Y_OUT + proud
        tiers.append((half_w, [(jp.Y_OUT - 0.1, z0), (y - ch, z0), (y, z0 + ch), (y, z1), (jp.Y_OUT - 0.1, z1)]))
    return tiers


def corbel_taper_profile():
    """The stone strip above a stub corbel, in (y, z), counter-clockwise: it starts at the top of the corbel's head, as
    proud as the pier strip (jp.PILASTER_PROUD), and slopes in one straight line to the wall face at the foot of the
    string course, as the pier strip does (jp.pilaster_proud). It has no flat top, so no ledge shows over the head, and it
    is not proud where it reaches the string course. Its back is buried 0.1 m in the wall."""
    return [(jp.Y_OUT - 0.1, jp.COLLAR_Z1), (jp.Y_OUT + jp.PILASTER_PROUD, jp.COLLAR_Z1),
            (jp.Y_OUT, jp.STRING_Z0), (jp.Y_OUT - 0.1, jp.STRING_Z0)]


# ---- the inner rail's returns and the door frame's parapet returns ------------------------------------
def rail_return_x(side):
    """(x low, x high) of the rail return on a side: a slab as thick as the rail that ends on the plane |X| = XB,
    where the stub's rail starts."""
    t = jp.RAIL_Y1 - jp.RAIL_Y0
    return tuple(sorted((side * (XB - t), side * XB)))


def rail_return_y():
    """(y low, y high) of a rail return: it starts where the inner-edge rail ends (that rail is the rail section
    moved to the inner face of the body) and ends on the stub rail's inner face, so it overlaps neither."""
    return (-HALF_D + (jp.RAIL_Y1 - jp.RAIL_Y0), jp.RAIL_Y1)


def rail_return_cap(side):
    """The cap of a rail return in (x, z): flush with the slab on the stub side, overhanging the slab by the
    rail cap's overhang on the other side (the stub rail's own cap starts on the plane |X| = XB)."""
    x0, x1 = rail_return_x(side)
    near, far = (x1, x0) if side > 0 else (x0, x1)
    out = -jp.RAIL_CAP_OVER if side > 0 else jp.RAIL_CAP_OVER
    return [(far + out, jp.RAIL_BODY_TOP), (near, jp.RAIL_BODY_TOP), (near, jp.RAIL_TOP), (far, jp.RAIL_TOP),
            (far + out, jp.RAIL_BODY_TOP + 0.05)]


RETURN_HALF_W = 0.25


def parapet_return_profiles(xc):
    """(slab, cap) in (x, z) of the 0.5 m return where the parapet jogs from the door frame back to the facade plane.
    The slab fills exactly the 0.5 m between the two parts, and the cap is flush with it in X, so that no cap
    shares a volume with the caps of the parts at its ends."""
    hw = RETURN_HALF_W
    slab = [(xc - hw, jp.PARAPET_FOOT), (xc + hw, jp.PARAPET_FOOT), (xc + hw, jp.PARAPET_BODY_TOP),
            (xc - hw, jp.PARAPET_BODY_TOP)]
    cap = [(xc - hw, jp.PARAPET_BODY_TOP), (xc + hw, jp.PARAPET_BODY_TOP), (xc + hw, jp.PARAPET_BODY_TOP + 0.2),
           (xc + hw - 0.05, jp.PARAPET_TOP), (xc - hw + 0.05, jp.PARAPET_TOP), (xc - hw, jp.PARAPET_BODY_TOP + 0.2)]
    return slab, cap


def parapet_return_y(dy_wing, dy_door):
    """(y low, y high) of a return's cap: from the inner overhang of the wing's cap to the outer overhang of the
    door-frame part's cap."""
    return (jp.PARAPET_Y0 + dy_wing - jp.PARAPET_CAP_OVER, jp.PARAPET_Y1 + dy_door + jp.PARAPET_CAP_OVER)


def stub_parapet_span(side):
    """(x low, x high) of a stub's parapet part: it starts on the drum's tangent plane, where the ring ends, and
    ends on the joint plane."""
    if side > 0:
        return (STUB_PARAPET_X0, XE)
    return (-XE, -STUB_PARAPET_X0)


def ring_tail_y(member):
    """Y where a member of the drum's parapet ring ('slab', 'frieze' or 'cap') stops at the stub: the front face of the
    same member of the stub's parapet, so that the two meet in a face or an edge and share no volume (the cap and
    the frieze stand proud of the slab and stop further out)."""
    sec = jp.section()
    return max(p[0] for p in sec[{"slab": "parapet", "frieze": "frieze", "cap": "parapet_cap"}[member]])


# ---- trim mapping on a swept member and on the kit parts ---------------------------------------------
def edge_v(prof, k, v0, v1):
    """V at the two ends of edge k of the closed profile `prof` (points (d, z), d outward) swept along a
    path, as the wall maps a carved member (jp.trim_uv): a face whose normal is mostly radial takes V from
    Z over the member's whole height, any other face from d over the member's whole depth. The strip
    (v0, v1) is spread over that extent, so a band on a drum shows the whole strip as it does on the wall."""
    ds, zs = [p[0] for p in prof], [p[1] for p in prof]
    (da, za), (db, zb) = prof[k], prof[(k + 1) % len(prof)]

    def frac(x, lo, hi):
        return 0.5 if hi - lo < 1e-9 else (x - lo) / (hi - lo)
    if abs(zb - za) >= abs(db - da):
        fa, fb = frac(za, min(zs), max(zs)), frac(zb, min(zs), max(zs))
    else:
        fa, fb = frac(da, min(ds), max(ds)), frac(db, min(ds), max(ds))
    return (v0 + (v1 - v0) * fa, v0 + (v1 - v0) * fb)


KIT_V_PAD = 0.004   # UV units kept clear of the strip's edges (about four texels at 1024 px)


def kit_face_uv(n, pts, strip, x_low=0.0):
    """(u, v) of each point of a kit face on the plain strip `strip`: a box projection at jp.TD. The dominant
    axis of the normal picks the plane. U = TD times the first free axis (X for a Y or Z normal, else Y), so
    it is continuous between faces. V = TD times the second (Z, or Y for a floor), centred on the strip's
    middle for this face and clamped into the strip, so a face never reads the next strip."""
    v0, v1 = jp.STRIPS[strip]
    a = [abs(c) for c in n]
    d = a.index(max(a))
    u_axis, v_axis = {0: (1, 2), 1: (0, 2), 2: (0, 1)}[d]
    cs = [p[v_axis] for p in pts]
    mid = (min(cs) + max(cs)) / 2.0
    vm = (v0 + v1) / 2.0
    out = []
    for p in pts:
        u = jp.TD * (p[u_axis] - (x_low if u_axis == 0 else 0.0))
        v = min(max(vm + jp.TD * (p[v_axis] - mid), v0 + KIT_V_PAD), v1 - KIT_V_PAD)
        out.append((u, v))
    return out


# ---- polygons and the pavilion's placing -------------------------------------------------------------
def point_in_polygon(p, poly):
    x, y = p
    hit = False
    for i in range(len(poly)):
        (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % len(poly)]
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
            hit = not hit
    return hit


def edge_distance(p, poly):
    """Distance from p to the nearest edge of the polygon."""
    best = None
    for i in range(len(poly)):
        (ax, ay), (bx, by) = poly[i], poly[(i + 1) % len(poly)]
        dx, dy = bx - ax, by - ay
        seg = dx * dx + dy * dy
        t = 0.0 if seg < 1e-18 else min(max(((p[0] - ax) * dx + (p[1] - ay) * dy) / seg, 0.0), 1.0)
        d = math.hypot(p[0] - (ax + t * dx), p[1] - (ay + t * dy))
        best = d if best is None else min(best, d)
    return best


def place_point(pt, centre, scale, psi):
    """A point of the kit part (relative to its floor centre) placed: scaled, turned by psi about Z, moved."""
    c, s = math.cos(psi), math.sin(psi)
    return (centre[0] + scale * (pt[0] * c - pt[1] * s), centre[1] + scale * (pt[0] * s + pt[1] * c))


def pavilion_rotation(side, below_pts, centre, scale, outline, margin, step_deg=1.0):
    """The turn of a pavilion about its floor centre, which stands on `centre` (the drum's centre). `below_pts`
    are the (x, y) of the part's vertices under the floor (the platform and its stairs), relative to the floor
    centre. They lie under the deck, so each must stand inside the stone body `outline` by `margin`: nothing
    of the platform shows through a drum. The mass of the platform (the mean of the points) lies toward its
    stairs: it is turned to point at the deck, diagonally toward the middle of the piece and the inner side.
    The nearest turn that fits wins, and the search alternates in the direction that mirrors the other
    side. None when no turn fits."""
    mean = (sum(p[0] for p in below_pts) / len(below_pts), sum(p[1] for p in below_pts) / len(below_pts))
    psi0 = math.atan2(-1.0, -float(side)) - math.atan2(mean[1], mean[0])
    order = [0]
    for k in range(1, int(180.0 / step_deg) + 1):
        order += [k, -k]
    for k in order:
        psi = psi0 + side * math.radians(k * step_deg)
        if all(point_in_polygon(q, outline) and edge_distance(q, outline) >= margin
               for q in (place_point(p, centre, scale, psi) for p in below_pts)):
            return psi
    return None


# ---- the lancet outlines and the door ring's mapping ---------------------------------------------------
def lancet(half_w, rise, steps=10):
    """The (x, z) points of a pointed arch of half width `half_w` and rise `rise` over its spring line, from the
    left foot over the apex (index `steps`) to the right foot."""
    r = (half_w ** 2 + rise ** 2) / (2 * half_w)
    phi1 = math.acos((half_w - r) / r)
    left = []
    for i in range(steps + 1):
        p = math.pi + (phi1 - math.pi) * i / steps
        left.append((-half_w + r + r * math.cos(p), r * math.sin(p)))
    return left + [(-x, y) for x, y in reversed(left[:-1])]


def opening_loop(half_w, spring, rise, z_bottom, steps=10):
    """A door opening: straight jambs up to `spring`, then the lancet (`steps` segments an arc). Left foot to right foot."""
    return [(-half_w, z_bottom)] + [(u, spring + v) for u, v in lancet(half_w, rise, steps)] + [(half_w, z_bottom)]


def door_opening_outline():
    """The door tunnel's outline in (x, z), left foot over the apex to the right foot (4.7 m wide, apex at 10.0)."""
    return opening_loop(DOOR_HALF_W, 7.0, DOOR_TOP_Z - 7.0, 0.0)


def nearest_on_polyline(points, x, z):
    """(s, d) of the point of the polyline `points` (x, z) that is nearest to (x, z): s is its arc length from
    the first point, d its distance. The door ring takes U from s and V from d."""
    best, at, run = None, 0.0, 0.0
    for i in range(len(points) - 1):
        (ax, az), (bx, bz) = points[i], points[i + 1]
        dx, dz = bx - ax, bz - az
        seg = dx * dx + dz * dz
        length = math.sqrt(seg)
        t = 0.0 if seg < 1e-18 else min(max(((x - ax) * dx + (z - az) * dz) / seg, 0.0), 1.0)
        d = math.hypot(x - (ax + t * dx), z - (az + t * dz))
        if best is None or d < best:
            best, at = d, run + t * length
        run += length
    return at, best


# ---- the door ring as a strip of quads between the opening and the outer outline ---------------------
def _cumulative(points):
    cum = [0.0]
    for a, b in zip(points, points[1:]):
        cum.append(cum[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    return cum


def _point_at(points, cum, s):
    """The point of the polyline at arc length s."""
    for i in range(len(points) - 1):
        if s <= cum[i + 1] + 1e-12:
            seg = cum[i + 1] - cum[i]
            t = 0.0 if seg < 1e-18 else min(max((s - cum[i]) / seg, 0.0), 1.0)
            return (points[i][0] + t * (points[i + 1][0] - points[i][0]),
                    points[i][1] + t * (points[i + 1][1] - points[i][1]))
    return points[-1]


def ring_pairs(inner, outer, spring):
    """Pairs ((x, z) on the opening, (x, z) on the outer outline) that make the quads of a door ring, from the
    left foot over the apex to the right foot. `inner` and `outer` are polylines from foot to foot, both
    straight up to `spring` and then curved to an apex on X = 0. Up the straight jamb the pairs share a
    height. On the curve every vertex of the outer outline is paired with the point of the opening at the same
    fraction of the curve's length, so a quad is never collapsed, however far the outline swings from the
    opening (an ogee against a lancet)."""
    def left(points):
        apex = max(range(len(points)), key=lambda i: points[i][1])
        half = points[:apex + 1]
        k = max(i for i, p in enumerate(half) if p[1] <= spring + 1e-9)
        return half[:k], half[k:]

    vi, ci = left(inner)
    vo, co = left(outer)
    pairs = [((vi[0][0], z), (vo[0][0], z)) for z in sorted({round(p[1], 9) for p in vi + vo})]
    cum_i, cum_o = _cumulative(ci), _cumulative(co)
    for p, s in zip(co, cum_o):
        pairs.append((_point_at(ci, cum_i, s / cum_o[-1] * cum_i[-1]), p))
    mirror = [((-a[0], a[1]), (-b[0], b[1])) for a, b in reversed(pairs[:-1])]
    return pairs + mirror


def ring_u(pairs):
    """U of each pair, in metres along the ring, the distance from the apex pair (0 there, the same on both sides, so
    that the two sides are mirror images and meet in a mitre at the tip): the mean of the arc length along the opening
    and along the outer outline, so that the strip is stretched by no more than the difference of the two edges, and
    by nothing up the straight jamb."""
    cin = _cumulative([p[0] for p in pairs])
    cout = _cumulative([p[1] for p in pairs])
    m = len(pairs) // 2
    return [abs(0.5 * ((cin[i] - cin[m]) + (cout[i] - cout[m]))) for i in range(len(pairs))]


# ---- plinth mapping on a swept member and on the foundation ----------------------------------------
def sweep_edge_v(prof, k, plinth):
    """V at the two ends of edge k of the closed profile `prof` (points (d, z), d outward) swept along a path, with the
    wall's rules, so that a swept stone face has its courses level with the wall's. A vertical edge: V = TD z,
    or the plinth's V (a bed joint at jp.PLINTH_V_TOP on the top edge, Z 3.0) when `plinth`. A battered edge:
    V = TD z / tz, the distance up the slope from the ground line, or jp.plinth_v_slope. A horizontal edge:
    V across the member at TD."""
    (da, za), (db, zb) = prof[k], prof[(k + 1) % len(prof)]
    dd, dz = db - da, zb - za
    if abs(dz) < 1e-9:
        return (jp.TD * da, jp.TD * db)
    if abs(dd) < 1e-9:
        if plinth:
            return (jp.plinth_v_vertical(za), jp.plinth_v_vertical(zb))
        return (jp.TD * za, jp.TD * zb)
    tz = abs(dz) / math.hypot(dd, dz)
    if plinth:
        return (jp.plinth_v_slope(za, tz), jp.plinth_v_slope(zb, tz))
    return (jp.TD * za / tz, jp.TD * zb / tz)


def foundation_uv(n, co, x_low, plinth=True):
    """(u, v) of a face that the box rule would stretch: a vertical face whose normal runs diagonally (the foundation
    under a drum, which is a polygon of facets; the cut end of a member swept round a drum) takes U along the face
    at TD and the vertical V of the material: the plinth's (a bed joint at jp.PLINTH_V_TOP on the top edge) or, with
    `plinth` False, TD z of the wall's stone. Any other face takes jp.plinth_uv or jp.world_uv."""
    nx, ny, nz = n
    if abs(nz) < 0.05 and abs(nx) > jp.SLOPE_MIN and abs(ny) > jp.SLOPE_MIN:
        h = math.hypot(nx, ny)
        v = jp.plinth_v_vertical(co[2]) if plinth else jp.TD * co[2]
        return (jp.TD * ((-ny / h) * co[0] + (nx / h) * co[1]), v)
    return jp.plinth_uv(n, co, x_low) if plinth else jp.world_uv(n, co, x_low)


def facet_uv(n, p, c, s_c, plinth=True):
    """(u, v) of the point `p` of one face of a member swept round a drum, with `n` the face's outward normal, `c`
    its centre and `s_c` the distance travelled along the path to that centre (travel keeps the outside on the
    left). U runs along the face's own horizontal tangent: TD (s_c + t . (p - c)). V is the wall's rule up the
    face: the plinth's bed joint (V 0 at the ground line, jp.PLINTH_V_TOP at Z 3.0 on the battered face) or, with
    `plinth` False, TD z / tz. A face mapped one by one this way is isometric, where U taken from the length of
    each ring shears a battered face (the ring is longer at its foot), and the U of two neighbours agrees along
    their shared edge to a few centimetres."""
    nx, ny, nz = n
    h = math.hypot(nx, ny)
    u = jp.TD * (s_c + (ny / h) * (p[0] - c[0]) + (-nx / h) * (p[1] - c[1]))
    if abs(nz) < 0.05:
        v = jp.plinth_v_vertical(p[2]) if plinth else jp.TD * p[2]
    else:
        tz = h / math.hypot(h, nz)
        v = jp.plinth_v_slope(p[2], tz) if plinth else jp.TD * p[2] / tz
    return (u, v)


# ---- soot and damp in the passage --------------------------------------------------------------------
def _smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def in_passage(x, y, z):
    """True inside the hall or a door tunnel (a little over its surface, so that a vertex on a wall counts)."""
    ay, ax = abs(y), abs(x)
    if ay <= HALL_Y + 1e-6:
        return ax <= HALL_HALF_W + 0.2 and -0.05 <= z <= HALL_CEIL + 0.05
    return ay <= HALF_D + 1e-6 and ax <= DOOR_HALF_W + 0.02 and -0.05 <= z <= DOOR_TOP_Z + 0.02


def passage_soot(x, y, z):
    """(r, g, b) multiplier, 1.0 outside the passage. Soot builds toward the vault and toward the middle of
    the hall, and thins out along a door tunnel to nothing at its rim (the facade plane). Damp at the floor
    darkens with a green cast. Mirror symmetric in X and Y; every channel is in SOOT_MIN to 1.0."""
    if not in_passage(x, y, z):
        return (1.0, 1.0, 1.0)
    ay = abs(y)
    fade = 1.0 if ay <= HALL_Y else _smooth((HALF_D - ay) / (HALF_D - HALL_Y))
    centre = 1.0 - min(ay / HALL_Y, 1.0) ** 2
    soot = fade * (0.16 + 0.30 * _smooth((z - 3.0) / (HALL_CEIL - 3.0))) * (0.55 + 0.45 * centre)
    wet = fade * 0.12 * jp.damp(z)
    return tuple(min(max(1.0 - k_s * soot - k_w * wet, SOOT_MIN), 1.0)
                 for k_s, k_w in ((0.95, 1.00), (1.00, 0.55), (1.05, 1.20)))


# ---- variant b: the spired gatehouse (picture g1) -------------------------------------------------------
# The body is the same 20 x 12.4 m block with the same passage. Each outer corner carries an octagonal turret
# whose +X flat lies in the plane |X| = XB, so the stub's outer face (Y 2.5) meets that flat in one plain
# vertical corner. The flat starts exactly at the stub's face. Nothing round or stepped reaches a joint.
TUR_A = 2.5                                        # apothem of a turret (5 m across the flats)
TUR_CX = XB - TUR_A                                # 7.5
TUR_CY = jp.Y_OUT + TUR_A * math.tan(math.pi / 8)  # the +X flat starts at Y_OUT
SHAFT_TOP_Z = 15.0                                 # the stone shaft ends in a cornice here
FACADE_JOIN_X = (TUR_CX - TUR_A) + (FACADE_Y - (TUR_CY + TUR_A * math.tan(math.pi / 8)))  # facade meets the chamfer
WING_END_X = 5.99                                  # the facade parapet runs into the turret's chamfer (one opening fits)
STUB_PARAPET_X0_B = XB - 0.55                      # the stub parapet runs into the turret
HOLE_X, HOLE_Y0, HOLE_Y1 = 2.9, -2.6, -1.0         # the rock hole in the deck (5.8 x 1.6 m)
THROW_Y = (HOLE_Y0 + HOLE_Y1) / 2.0                # the throwers stand on the long rims, rays run in this plane

LANTERN_A, LANTERN_COL_R = 1.8, 0.13               # the open lantern: apothem, column radius
LANTERN_Z0, LANTERN_Z1 = SHAFT_TOP_Z + 0.5, 19.2
LANTERN_ARCH_W, LANTERN_ARCH_H = 1.1, 2.9
SPIRE_Z0 = 19.8

ROOF_HX, ROOF_HY = 4.6, 5.2                        # the open gable roof over the deck, ridge along Y
ROOF_EAVE_Z, ROOF_RIDGE_Z = 15.6, 19.6
ROOF_COL_HALF = 0.2
ROOF_COLUMNS = tuple((sx * 4.2, sy * 4.3) for sx in (-1, 1) for sy in (-1, 1))


def octagon(cx, cy, a):
    """The eight vertices, counter-clockwise, of the regular octagon of apothem `a` with flats on the axes:
    vertex k lies at 22.5 + 45 k degrees."""
    r = a / math.cos(math.pi / 8)
    return [(cx + r * math.cos(math.radians(22.5 + 45.0 * k)), cy + r * math.sin(math.radians(22.5 + 45.0 * k)))
            for k in range(8)]


def turret_outline(side):
    """The turret's octagon, counter-clockwise. side +1 is the right one (centre X +TUR_CX)."""
    if side > 0:
        return octagon(TUR_CX, TUR_CY, TUR_A)
    return [(-x, y) for x, y in octagon(TUR_CX, TUR_CY, TUR_A)][::-1]


def plinth_path_b(side, tail_y=STUB_TAIL_Y):
    """The outer boundary from the door frame's side to the stub line round one turret, outside on the LEFT
    (side +1: from the facade corner at the frame, along the facade, round the turret and down its +X flat to the
    stub's mass). side -1 is the mirror image in travel order (the stub's mass first, the frame last). The path
    ends at Y `tail_y` on the flat: inside the stub by default, or, for a member that has a twin on the stub, on the
    front face of that twin (Y_OUT plus its proud), so that the two share no volume."""
    o = octagon(TUR_CX, TUR_CY, TUR_A)
    right = [(PORTAL_HALF_W, FACADE_Y), (FACADE_JOIN_X, FACADE_Y), o[2], o[1], o[0], (XB, tail_y)]
    if side > 0:
        return right
    return [(-x, y) for x, y in right][::-1]


def front_path_b(tail=True):
    """The whole outer front from the left stub line to the right one: left turret, facade, door frame, facade,
    right turret. `tail` False drops the two end points carried into the stubs."""
    path = plinth_path_b(-1) + [(-PORTAL_HALF_W, HALF_D), (PORTAL_HALF_W, HALF_D)] + plinth_path_b(1)
    return path if tail else path[1:-1]


def core_outline_b():
    """The stone body's footprint (counter-clockwise): the box to the facade, the door frame and the two turrets."""
    return [(-XB, -HALF_D), (XB, -HALF_D)] + front_path_b(False)[::-1]


def foundation_outline_b():
    """The footprint of the foundation: the front pushed out by the batter, the inner face and the ends flat."""
    off = offset_path(front_path_b(), BATTER)
    # the closing points sit 0.4 m below the path's end points, inside the stub's own foundation: four points on one
    # line across the piece would make the tessellator emit zero-area triangles
    y = STUB_TAIL_Y - 0.4
    return off + [(XB, y), (XB, -HALF_D), (-XB, -HALF_D), (-XB, y)]


def poly_edges(poly):
    """(middle, unit direction, outward unit normal, length) of each edge of a counter-clockwise polygon. Edge i runs
    from vertex i to vertex i + 1."""
    out = []
    for i in range(len(poly)):
        (ax, ay), (bx, by) = poly[i], poly[(i + 1) % len(poly)]
        ln = math.hypot(bx - ax, by - ay)
        dx, dy = (bx - ax) / ln, (by - ay) / ln
        out.append((((ax + bx) / 2.0, (ay + by) / 2.0), (dx, dy), (dy, -dx), ln))
    return out


def stub_parapet_span_b(side):
    """(x low, x high) of a stub's parapet part: it starts inside the turret and ends on the joint plane."""
    if side > 0:
        return (STUB_PARAPET_X0_B, XE)
    return (-XE, -STUB_PARAPET_X0_B)


def diag_uv(n, co):
    """(u, v) of a vertical face whose normal runs diagonally: U = TD along the face, V = TD z."""
    h = math.hypot(n[0], n[1])
    t = (-n[1] / h, n[0] / h)
    return (jp.TD * (t[0] * co[0] + t[1] * co[1]), jp.TD * co[2])


def face_uv_b(n, co, x_low):
    """The stone rule of the body of variant b: a diagonal vertical face (a turret chamfer) takes diag_uv, every other
    face the pinned world rule."""
    if abs(n[2]) < 0.05 and abs(n[0]) > 0.2 and abs(n[1]) > 0.2:
        return diag_uv(n, co)
    return jp.world_uv(n, co, x_low)


def planar_uv(n, co):
    """(u, v) of any face at TD per metre both ways: U along the horizontal in the face's plane (the first free axis
    for a flat face), V along the slope. A roof or a spire takes this."""
    h = math.hypot(n[0], n[1])
    th = (1.0, 0.0, 0.0) if h < 1e-9 else (-n[1] / h, n[0] / h, 0.0)
    ts = (n[1] * th[2] - n[2] * th[1], n[2] * th[0] - n[0] * th[2], n[0] * th[1] - n[1] * th[0])
    return (jp.TD * sum(a * b for a, b in zip(co, th)), jp.TD * sum(a * b for a, b in zip(co, ts)))


def spire_profile():
    """(z, apothem) of the octagonal spire from the eave to the tip: a flared eave and a concave sweep."""
    return [(SPIRE_Z0, 2.45), (SPIRE_Z0 + 0.5, 2.30), (SPIRE_Z0 + 1.8, 1.90), (SPIRE_Z0 + 3.8, 1.30),
            (SPIRE_Z0 + 6.0, 0.72), (SPIRE_Z0 + 7.6, 0.32), (SPIRE_Z0 + 9.0, 0.0)]


def runs(flags):
    """Maximal stretches of True in `flags`, as (first index, last index)."""
    out, start = [], None
    for i, f in enumerate(flags):
        if f and start is None:
            start = i
        elif not f and start is not None:
            out.append((start, i - 1))
            start = None
    if start is not None:
        out.append((start, len(flags) - 1))
    return out


# ---- variant b, version 4: the turret top, the spire's mapping, the gable roof ---------------------------
def ring_path(cx, cy, a):
    """The octagon of apothem `a` run clockwise, so that its left normal points out (a swept member's offset d is
    then d further from the centre)."""
    return octagon(cx, cy, a)[::-1]


def mitre_normals_closed(path):
    """mitre_normals() for a closed path (the last point joins the first): the left normal at each point, scaled so
    that an offset d lies d from both neighbouring edges. A clockwise path has its left side outside."""
    n = len(path)
    seg = []
    for i in range(n):
        a, b = path[i], path[(i + 1) % n]
        ln = math.hypot(b[0] - a[0], b[1] - a[1])
        seg.append((-(b[1] - a[1]) / ln, (b[0] - a[0]) / ln))
    out = []
    for i in range(n):
        p, q = seg[i - 1], seg[i]
        k = 1.0 + p[0] * q[0] + p[1] * q[1]
        out.append(((p[0] + q[0]) / k, (p[1] + q[1]) / k))
    return out


CORNICE_Z0 = SHAFT_TOP_Z - 0.4                      # the stone shaft ends here and the cornice ring starts
CORNICE_D_IN = -0.30                                # the ring's inner edge, in d from the shaft's face
LANTERN_FLOOR_A = TUR_A + CORNICE_D_IN              # the lantern floor's apothem: its edge is the ring's inner edge


def cornice_profile():
    """The cornice of a turret as a closed (d, z) profile for a ring sweep round the shaft (d outward from the shaft's
    face). It spans Z CORNICE_Z0 to SHAFT_TOP_Z, steps out 0.30 m with a chamfer to a narrower top, and its inner edge
    is the edge of the lantern floor, so the floor sits on it with no slot between them."""
    return [(CORNICE_D_IN, CORNICE_Z0), (0.30, CORNICE_Z0), (0.30, CORNICE_Z0 + 0.18), (0.15, CORNICE_Z0 + 0.26),
            (0.15, SHAFT_TOP_Z), (CORNICE_D_IN, SHAFT_TOP_Z)]


def ngon_eave_length(n, a):
    """The length of the eave ring of a regular n-gon of apothem a (the `eave` argument of jp.roof_uv for a spire)."""
    return 2.0 * n * a * math.tan(math.pi / n)


SPIRE_SEAM = -math.radians(112.5)   # the azimuth where U of a spire jumps: between its south-west and south facets, on the back


def is_sloped(n, tol=0.05):
    """True for a face that leans (a normal with a Z part between tol and 1 - tol): the box rule would stretch it."""
    return tol < abs(n[2]) < 1.0 - tol


# the gable roof over the deck: ridge along Y, two slate slabs, rafters under them, a stone gable end with a pointed
# opening at each end, a fascia along each gable edge, a ridge beam and a ridge cap
ROOF_T = 0.18                                       # the slab's thickness, measured straight up
ROOF_SLOPE = (ROOF_RIDGE_Z - ROOF_EAVE_Z) / ROOF_HX


def roof_top_z(x):
    return ROOF_RIDGE_Z - ROOF_SLOPE * abs(x)


def roof_under_z(x):
    return roof_top_z(x) - ROOF_T


def roof_band(side, x_out, up, down):
    """The (x, z) corners of the band between the slab's top line raised by `up` and its underside lowered by `down`,
    from the ridge plane X = 0 to |x| = x_out on `side`. A parallelogram with vertical ends."""
    return [(0.0, roof_under_z(0.0) - down), (side * x_out, roof_under_z(x_out) - down),
            (side * x_out, roof_top_z(x_out) + up), (0.0, roof_top_z(0.0) + up)]


def band_axis(side, x_out, up, down):
    """(start, end, width) of the middle line of roof_band(): start on the ridge plane, end at |x| = x_out, and the
    band's width measured square to the slope."""
    top0, bot0 = roof_top_z(0.0) + up, roof_under_z(0.0) - down
    top1, bot1 = roof_top_z(x_out) + up, roof_under_z(x_out) - down
    cos_t = 1.0 / math.sqrt(1.0 + ROOF_SLOPE ** 2)
    return (0.0, (top0 + bot0) / 2.0), (side * x_out, (top1 + bot1) / 2.0), (top0 - bot0) * cos_t


def roof_axes(side):
    """((tx, tz), (nx, nz)) of a slope on `side` in the (x, z) plane: the unit tangent from the eave toward the ridge,
    and the unit normal that points up and out."""
    rise = ROOF_RIDGE_Z - ROOF_EAVE_Z
    ln = math.hypot(ROOF_HX, rise)
    return (-side * ROOF_HX / ln, rise / ln), (side * rise / ln, ROOF_HX / ln)


RAFTER_W, RAFTER_T, RAFTER_EMBED = 0.22, 0.16, 0.03   # depth square to the slope, thickness along Y, and how far the top is inside the slab
RAFTER_YS = tuple(1.3 * k for k in range(-3, 4))
RAFTER_EAVE_INSET, RAFTER_RIDGE_X = 0.25, 0.10         # where a rafter starts and ends, in |x|, on the underside
RIDGE_BEAM_HALF = 0.15


def rafter_axis(side):
    """(start, end) of the middle line of a rafter in (x, z): from just inside the eave to inside the ridge beam, hung
    under the slab so that its top face lies RAFTER_EMBED inside the slab (no face shares the underside's plane)."""
    _t, (nx, nz) = roof_axes(side)
    off = RAFTER_W / 2.0 - RAFTER_EMBED
    return tuple((side * x - off * nx, roof_under_z(x) - off * nz) for x in (ROOF_HX - RAFTER_EAVE_INSET, RAFTER_RIDGE_X))


def plate_x_range(side):
    """(x low, x high) of the wall plate over the columns on `side`."""
    return tuple(sorted((side * 4.2 - 0.25, side * 4.2 + 0.25)))


TYMP_Y_OUT, TYMP_Y_IN = ROOF_HY - 0.15, ROOF_HY - 0.55   # the stone gable end, 0.4 thick, set back under the slab's end
TIE_Z0, TIE_Z1, TIE_HALF_X = ROOF_EAVE_Z - 0.40, ROOF_EAVE_Z - 0.05, 4.5   # the course the gable end stands on
RIDGE_BEAM_Y = ROOF_HY - 0.30                       # the ridge beam ends inside the gable end
FASCIA_Y0, FASCIA_Y1 = ROOF_HY, ROOF_HY + 0.12      # the fascia stands on the end of the slab
RIDGE_CAP_Y = ROOF_HY + 0.15
GABLE_ARCH = (15.85, 2.6, 1.9)                      # z0, height and outer width of the moulding round the gable's opening
GABLE_ARCH_MOULD, GABLE_ARCH_LEDGE = 0.14, 0.02     # the moulding's width, and how far the cut stands inside its inner edge


def tympanum_outline():
    """The stone gable end as an (x, z) triangle: its base on the tie course, its two sides on the slab's underside,
    its apex on the ridge's underside."""
    xb = (roof_under_z(0.0) - TIE_Z1) / ROOF_SLOPE
    return [(-xb, TIE_Z1), (xb, TIE_Z1), (0.0, roof_under_z(0.0))]


def gable_opening(steps):
    """The (x, z) loop of the pointed opening cut through the gable end: the moulding's inner arch, narrowed by
    GABLE_ARCH_LEDGE on each side (so the cut and the moulding share no plane), from the left foot over the apex."""
    z0, height, width = GABLE_ARCH
    w_cut = width - 2.0 * GABLE_ARCH_MOULD - 2.0 * GABLE_ARCH_LEDGE
    spring = height - width * math.sqrt(3.0) / 2.0
    return [(u, z0 + v) for u, v in jp.pointed_arch(w_cut, spring + w_cut * math.sqrt(3.0) / 2.0, steps)]


def ridge_cap_profile():
    """The ridge cap in (x, z): a roll 0.6 m wide whose flat foot lies inside the slabs."""
    rz = ROOF_RIDGE_Z
    return [(-0.30, rz - 0.32), (0.30, rz - 0.32), (0.26, rz + 0.05), (0.0, rz + 0.17), (-0.26, rz + 0.05)]


def bar_uv(n, co, p0, p1, width, y_lo, y_hi, strip):
    """(u, v) of a point on a straight bar in the (x, z) plane, extruded along Y (a rafter, a fascia): U = TD times the
    distance along the bar from `p0` toward `p1`, so it holds the density along the length; V spans the whole strip
    across the bar, as the wall's members do. A broad face (normal along Y) takes V from the square distance from the
    middle line (`width` wide). A long face (normal square to the bar) takes V from Y over y_lo to y_hi. An end
    face (normal along the bar) takes U from Y and V across the width."""
    v0, v1 = jp.STRIPS[strip]
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]
    ln = math.hypot(dx, dz)
    tx, tz = dx / ln, dz / ln
    rx, rz = co[0] - p0[0], co[2] - p0[1]
    s = rx * tx + rz * tz
    w = rx * -tz + rz * tx
    if tx < -1e-9:   # the bar on the other slope: the same V at the mirror point
        w = -w

    def frac(a, lo, hi):
        return min(max((a - lo) / (hi - lo), 0.0), 1.0)
    if abs(n[1]) >= 0.7:
        return (jp.TD * s, v0 + (v1 - v0) * frac(w, -width / 2.0, width / 2.0))
    if abs(n[0] * tx + n[2] * tz) >= 0.7:
        return (jp.TD * co[1], v0 + (v1 - v0) * frac(w, -width / 2.0, width / 2.0))
    return (jp.TD * s, v0 + (v1 - v0) * frac(co[1], y_lo, y_hi))


# ---- variant b, version 6: the front, the arches that are seen whole, the spire's slate, the roof's UV -----------------
# One string course runs across the whole front (the same path as the plinth, with the door frame between the two sides). The
# carved band, the plinth cap and the blind arches stand on the faces that a viewer 10 to 20 m in front of the door sees head on.
FACADE_ARCH_X = (PORTAL_HALF_W + FACADE_JOIN_X) / 2.0     # the middle of the flat facade between the door frame and the turret
FACADE_ARCH = (jp.ARCH_Z0, 5.8, 1.5)                      # z0, height, outer width: the legs stand on the band as the wall's do
LOW_ARCH = (jp.ARCH_Z0, 5.8, 1.4)                         # the arch on the front flat of a turret
HIGH_ARCH = (12.4, 1.9, 0.9)                              # the short arch over the deck, 0.3 m under the cornice
FACE_MIN_COS = 0.5                                        # a face is seen head on when its normal is within 60 degrees of the line of sight
FRAME_CLEAR = 0.5                                         # plain stone between a blind arch and the door frame
BAND_OVER_B = DRUM_BAND_OVER                              # the band ends this far past the outer leg of its arch
DOOR_FRAME_BOX = (-PORTAL_HALF_W, FACADE_Y, PORTAL_HALF_W, HALF_D)   # x0, y0, x1, y1 in plan
ARCH_PROUD_B = jp.ARCH_PROUD


def front_course_path_b(tail_y):
    """The string course across the whole front of variant b: from the left stub (the tail at X -XB, `tail_y`) round the left
    turret, along the facade, round the door frame (the middle of its front is a point of the path, so that U, mirrored from the
    middle, rises on both sides), along the facade and round the right turret to the right stub."""
    return (plinth_path_b(-1, tail_y=tail_y) + [(-PORTAL_HALF_W, HALF_D), (0.0, HALF_D), (PORTAL_HALF_W, HALF_D)] +
            plinth_path_b(1, tail_y=tail_y))


def band_path_b(side):
    """The carved band of variant b in travel order (like plinth_path_b): from the outer leg of the facade arch (and BAND_OVER_B)
    along the facade, round the chamfer to the front flat of the turret and along it to the outer leg of the turret's arch. The
    band is as long as the group of arches on it, as in a bay of the wall. Side -1 is the mirror image, in travel order."""
    o = octagon(TUR_CX, TUR_CY, TUR_A)
    right = [(FACADE_ARCH_X - FACADE_ARCH[2] / 2.0 - BAND_OVER_B, FACADE_Y), (FACADE_JOIN_X, FACADE_Y), o[2],
             (TUR_CX + LOW_ARCH[2] / 2.0 + BAND_OVER_B, o[1][1])]
    if side > 0:
        return right
    return [(-x, y) for x, y in right][::-1]


def view_points():
    """Where a viewer stands: on the centre line, 10, 15 and 20 m in front of the door plane."""
    near, far = VIEW_DISTANCES
    return [(0.0, HALF_D + d) for d in (near, 0.5 * (near + far), far)]


def view_cos(mid, n, viewer):
    """The cosine of the angle between the outward normal `n` of a face and the line from `mid` to the viewer."""
    dx, dy = viewer[0] - mid[0], viewer[1] - mid[1]
    return (n[0] * dx + n[1] * dy) / math.hypot(dx, dy)


def segment_hits_box(p, q, box):
    """True when the plan segment p-q crosses the inside of the box (x0, y0, x1, y1) (Liang and Barsky)."""
    x0, y0, x1, y1 = box
    t0, t1 = 0.0, 1.0
    dx, dy = q[0] - p[0], q[1] - p[1]
    for pk, qk in ((-dx, p[0] - x0), (dx, x1 - p[0]), (-dy, p[1] - y0), (dy, y1 - p[1])):
        if abs(pk) < 1e-12:
            if qk <= 0.0:
                return False
            continue
        t = qk / pk
        if pk < 0.0:
            t0 = max(t0, t)
        else:
            t1 = min(t1, t)
        if t0 >= t1 - 1e-12:
            return False
    return True


def arch_sight_clear(arch, viewer):
    """True when the door frame hides none of the arch: the lines from the viewer to five points across the arch's width, at the
    face of the arch, cross the plan of the door frame nowhere."""
    mid, d, n, w = arch["mid"], arch["d"], arch["n"], arch["width"]
    for u in (-w / 2.0, -w / 4.0, 0.0, w / 4.0, w / 2.0):
        p = (mid[0] + d[0] * u + n[0] * ARCH_PROUD_B, mid[1] + d[1] * u + n[1] * ARCH_PROUD_B)
        if segment_hits_box(viewer, p, DOOR_FRAME_BOX):
            return False
    return True


def _face_exposed(a, b):
    """The length of the edge a-b of a turret that shows. A face on the door side of the turret's axis is hidden by the facade
    where it lies behind the facade plane."""
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    if abs(0.5 * (a[0] + b[0])) >= TUR_CX:
        return length
    ya, yb = a[1] - FACADE_Y, b[1] - FACADE_Y
    if ya >= 0.0 and yb >= 0.0:
        return length
    if ya <= 0.0 and yb <= 0.0:
        return 0.0
    return length * max(ya, yb) / abs(ya - yb)


def arch_layout_b(side):
    """The blind arches of variant b on one side: the tall one on the facade, and a tall and a short one on the front flat of the
    turret. A face carries arches when a viewer on the centre line 10 to 20 m in front of the door sees it head on (FACE_MIN_COS)
    and enough of it shows for the arch and 0.3 m of stone each side. Each is a dict: kind, mid (xy of the face point under the
    arch's axis), d (unit direction along the face), n (outward normal), z0, height, width."""
    out = [{"kind": "facade", "mid": (side * FACADE_ARCH_X, FACADE_Y), "d": (1.0, 0.0), "n": (0.0, 1.0),
            "z0": FACADE_ARCH[0], "height": FACADE_ARCH[1], "width": FACADE_ARCH[2]}]
    poly = turret_outline(side)
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        ln = math.hypot(b[0] - a[0], b[1] - a[1])
        d = ((b[0] - a[0]) / ln, (b[1] - a[1]) / ln)
        n = (d[1], -d[0])
        mid = ((a[0] + b[0]) / 2.0, (a[1] + b[1]) / 2.0)
        seen = min(view_cos(mid, n, v) for v in view_points()) >= FACE_MIN_COS
        if seen and _face_exposed(a, b) >= LOW_ARCH[2] + 0.6:
            for kind, (z0, h, w) in (("front_low", LOW_ARCH), ("front_high", HIGH_ARCH)):
                out.append({"kind": kind, "mid": mid, "d": d, "n": n, "z0": z0, "height": h, "width": w})
    return out


# the slate of a spire: V is TD times the distance up the profile from the eave, so it has no step where the slope changes
def spire_slant(z):
    """The distance along the spire's profile (an apothem and a height), from the eave to height z, in metres."""
    prof = spire_profile()
    run = 0.0
    for (z0, a0), (z1, a1) in zip(prof, prof[1:]):
        length = math.hypot(a1 - a0, z1 - z0)
        if z <= z1:
            return run + length * (z - z0) / (z1 - z0)
        run += length
    return run


def spire_uv_v(co):
    """V of a point of a spire: TD times the slant to its height."""
    return jp.TD * spire_slant(co[2])


# the arris ribs: a triangular rib on each of the eight arrises. It is full size to a radius of SPIRE_RIB_FULL_R and then grows
# smaller in proportion to the radius, so that the eight ribs end in one point at the tip and never touch on the way.
SPIRE_RIB_HALF_W, SPIRE_RIB_SINK, SPIRE_RIB_OUT = 0.08, 0.06, 0.07
SPIRE_RIB_FULL_R = 0.9


def spire_rib_section(a):
    """(half width, sunk into the spire, out of the spire) of the arris rib where the spire's apothem is `a`."""
    k = min(1.0, max(a, 0.0) / math.cos(math.pi / 8.0) / SPIRE_RIB_FULL_R)
    return (SPIRE_RIB_HALF_W * k, SPIRE_RIB_SINK * k, SPIRE_RIB_OUT * k)


def planar_uv_sym(n, co):
    """(u, v) of a sloped face of the roof whose normal lies in the (x, z) plane: U = TD y, V = TD times the distance up the
    slope (the unit tangent in that plane that points up). The two slopes of a roof take the same value at mirror points."""
    ln = math.hypot(n[0], n[2])
    tx, tz = -n[2] / ln, n[0] / ln
    if tz < 0.0 or (abs(tz) < 1e-12 and tx < 0.0):
        tx, tz = -tx, -tz
    return (jp.TD * co[1], jp.TD * (tx * co[0] + tz * co[2]))


GABLE_STEPS = jp.ARCH_STEPS_FINE   # the pointed opening of the gable: 3.75 degrees between two neighbour segments
