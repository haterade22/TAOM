"""The numbers of the Rivendell wall tower, pure Python (no bpy). Tests: tools/tests/test_rivendell_tower_plan.py.

Used by tools/oneoff/blender_build_rivendell_tower.py. Frame as tools/oneoff/rivendell_joint_profile.py (jp):
metres, X runs along the wall line, +Y is the outer (attacker) side, Z is up, Z 0 is the ground line. The piece
is two bays long (X -6.667 to +6.667): the tower body (X -5.0 to +5.0) and a wall stub of 1.667 m on each side.
Variant "a" is the rectangular body (concept t1), variant "b" the half-round one (concept t2).

A rectangle is (x0, x1, y0, y1). A floor is a slab whose top face is its level. A flight rises one storey in
risers of RISER and treads of TREAD; its last tread lies at the upper level, so a flight of N risers has N
treads and a run of N * TREAD.

Pinned numbers that were changed (the report of the build says so): flight F3 starts at X +3.5, not +4.2. At +4.2
its first riser would stand against the east wall with no floor to approach it from. +3.5 gives the same
0.7 m landing as flight F2.
Version 4: the cell sizes of the vertex colour, the sill under the back arches of tower a, the UV rule of the drum, the cap
of a rail and the parapet numbers that follow the wall section are at the end of this file.
Variant b: the drum wall starts at the +-0.7 m jamb of the side door (the door reaches 0.2 m past the tangent point
at Y 0.5); the wall stubs start at X +-4.4, inside the drum wall, because the drum falls away from the flat side face;
the platform parapet stands out by the corbel (0.5 m) on every side, not on the drum alone, so that its straight
sides run tangent into the arc.
"""
import math

import rivendell_joint_profile as jp

VARIANTS = ("a", "b")
BASE_NAMES = {"a": "rivendell_castle_wall_tower_a", "b": "rivendell_castle_wall_tower_b"}
TRIANGLE_BUDGET = 14000

PIECE_X0, PIECE_X1 = -jp.BAY, jp.BAY
PIECE_LENGTH = PIECE_X1 - PIECE_X0
BODY_X = 5.0
BODY_Y0, BODY_Y1 = -3.5, 5.5
WALL_T = 0.8
DRUM_R, DRUM_CY = 5.0, 0.5        # variant b: the half drum on the outside
CORBEL_B = 0.5                    # variant b: the platform reaches out this far past the drum
FACE_CY = 0.5 * (BODY_Y0 + BODY_Y1)  # 1.0: the middle of the side faces

FLOORS_Z = (0.0, 6.0, 12.0)
PLATFORM_Z = 17.0
SLAB_T = 0.3
HEADROOM = 2.3                    # the asked 2.2 m and a margin; a flight rises 0.25 m per tread, so the real least is 2.45
RISER, TREAD, LANE_W = 0.25, 0.28, 1.3
END_FACE = 0.5                    # height of the end face of a flight under its top tread
LANE_A = (-2.7, -1.4)             # against the inner wall
LANE_B = (1.0, 2.3)
RAIL_T, RAIL_H = 0.2, 1.0         # the rail round a hole
CAP_OVER = 0.04                   # the rail cap is this much wider than the rail on each side

PANEL_RELIEF_IN = 0.04            # no panel stands on the inner face of a parapet any more; kept as a margin
# The platform parapet is the wall's parapet part (wall.build_parapet), so its thickness and its stand-out come from the
# section: 0.45 thick, its outer face in the plane of the body face.
PARAPET_T, PARAPET_PROUD = jp.PARAPET_Y1 - jp.PARAPET_Y0, jp.PARAPET_Y1 - jp.Y_OUT
PARAPET_OUT = PARAPET_PROUD       # the parapet outer face stands this far outside the body face
DOOR_PATH_HALF = 0.7
DECK_Z = jp.Z_DECK

F3_FOOT = 3.5
PINNED_F3_FOOT = 4.2

STUB_X_IN_B = 4.4                 # variant b: the stubs reach this far into the drum wall (a: flush on the side face)
PIER_ANGLES_B = (36.0, 72.0, 108.0, 144.0)  # variant b: engaged piers, degrees from the +X end of the drum
DRUM_STEPS = 36                   # variant b: 5 degree steps round the drum


# ---- outline and interior --------------------------------------------------------------------------
def _arc(cx, cy, r, steps=24):
    return [(cx + r * math.cos(math.pi * i / steps), cy + r * math.sin(math.pi * i / steps))
            for i in range(steps + 1)]


def outline(variant, steps=24):
    """The outer outline of the body in plan, counter-clockwise. Variant b: the drum has `steps` steps, the first
    point of the arc is the tangent point (BODY_X, DRUM_CY)."""
    if variant == "a":
        return [(-BODY_X, BODY_Y0), (BODY_X, BODY_Y0), (BODY_X, BODY_Y1), (-BODY_X, BODY_Y1)]
    if variant == "b":
        return [(-BODY_X, BODY_Y0), (BODY_X, BODY_Y0)] + _arc(0.0, DRUM_CY, DRUM_R, steps)
    raise KeyError(variant)


def offset_outline(pts, d):
    """A convex outline moved out by d on every edge: each point goes along its mitre."""
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        n1, n2 = _edge_normal(p0, p1), _edge_normal(p1, p2)
        k = d / (1.0 + n1[0] * n2[0] + n1[1] * n2[1])
        out.append((p1[0] + (n1[0] + n2[0]) * k, p1[1] + (n1[1] + n2[1]) * k))
    return out


def _edge_normal(a, b):
    """The outward normal of the edge a to b of a counter-clockwise outline."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    return (dy / length, -dx / length)


def interior_outline(variant):
    """The floor outline inside the walls: the interior rectangle (a), or the back part and a half disk (b)."""
    x0, x1, y0, y1 = interior_bounds(variant)
    if variant == "a":
        return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    return [(x0, y0), (x1, y0)] + _arc(0.0, DRUM_CY, DRUM_R - WALL_T, DRUM_STEPS)


def floor_outline(variant):
    """The outline of the floor slabs: the interior (a), or for b the back part with the flat side up to the door
    jamb and a half disk in 10 degree steps whose edges touch the interior circle, so that it ends inside the wall
    (by at most 2 cm) and never in the doorway."""
    if variant == "a":
        return interior_outline(variant)
    x0, x1, y0, _ = interior_bounds(variant)
    r = (DRUM_R - WALL_T) / math.cos(math.pi / DRUM_STEPS)
    steps = DRUM_STEPS // 2
    arc = [(r * math.cos(math.pi * i / steps), DRUM_CY + r * math.sin(math.pi * i / steps)) for i in range(1, steps)]
    return [(x0, y0), (x1, y0), (x1, drum_join_y())] + arc + [(x0, drum_join_y())]


def drum_x(r, y):
    """X of the circle of radius r about the drum centre at height y of the plan."""
    return math.sqrt(r * r - (y - DRUM_CY) ** 2)


def drum_join_y():
    """Where the flat side wall ends and the drum wall begins: the +Y jamb of the side door. The door reaches 0.2 m
    past the tangent point, where the drum is within 5 mm of the flat wall."""
    return 0.5 * side_door()["width"]


def drum_junction_angle():
    """The angle (degrees from +X) where the outer face of a wall stub meets the drum."""
    return math.degrees(math.asin((jp.Y_OUT - DRUM_CY) / DRUM_R))


def stub_inner_x(variant):
    """The X where the wall stub on the +X side starts (the -X stub mirrors it)."""
    return BODY_X if variant == "a" else STUB_X_IN_B


def platform_offset(variant):
    """How far the platform parapet line stands out from the body on every side."""
    return 0.0 if variant == "a" else CORBEL_B


def interior_bounds(variant):
    """The bounding rectangle of the room inside the walls."""
    return (-BODY_X + WALL_T, BODY_X - WALL_T, BODY_Y0 + WALL_T, (BODY_Y1 if variant == "a" else DRUM_CY + DRUM_R) - WALL_T)


def _in_disk(x, y, cx, cy, r):
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 1e-9


def in_room(variant, x, y, eps=1e-6):
    """Is the plan point (x, y) in the room inside the walls, wall faces included? The faces of the shell whose middle
    is in the room look into it."""
    x0, x1, y0, y1 = interior_bounds(variant)
    if x < x0 - eps or x > x1 + eps or y < y0 - eps or y > y1 + eps:
        return False
    if variant == "b" and y > DRUM_CY:
        return math.hypot(x, y - DRUM_CY) <= DRUM_R - WALL_T + eps
    return True


def rect_in_interior(variant, rect):
    x0, x1, y0, y1 = rect
    b = interior_bounds(variant)
    if x0 < b[0] - 1e-9 or x1 > b[1] + 1e-9 or y0 < b[2] - 1e-9 or y1 > b[3] + 1e-9:
        return False
    if variant == "b" and y1 > DRUM_CY:
        r = DRUM_R - WALL_T
        return all(_in_disk(x, y, 0.0, DRUM_CY, r) for x in (x0, x1) for y in (y0 if y0 > DRUM_CY else DRUM_CY, y1))
    return True


# ---- doors -----------------------------------------------------------------------------------------
def side_door():
    """The door in each side wall, at the wall walk level. The head is an equilateral pointed arch that starts at
    `jamb` (2.2 m); the opening is 1.4 m wide with at least 2.2 m of straight jamb."""
    width, jamb = 1.4, 2.2
    return {"width": width, "y_centre": 0.0, "z_sill": jp.Z_DECK, "jamb": jamb,
            "height": jamb + width * math.sqrt(3.0) / 2.0}


def ground_door():
    """The door in the back (inner) face: clear 1.6 m wide up to 2.2 m (`jamb`), then a pointed head with its apex
    at `height` above the sill. The head rises 1.0 m on a 0.8 m half width, so it is a two-centre arch and not the
    equilateral one of the side doors (an equilateral head on a 2.2 m jamb would reach 3.59 m)."""
    return {"width": 1.6, "jamb": 2.2, "height": 3.2, "x_centre": 0.0, "y_face": BODY_Y0, "z_sill": 0.0}


def door_outline(width, jamb, height, steps=6, mould=0.0):
    """Outline of a door with straight sides to `jamb` and a two-centre pointed head with its apex at `height`, as
    (u, v) points from the left foot (-width / 2, 0) over the apex to the right foot, u across and v up from the
    sill: two feet, two springings and `steps` points on each arc, 2 * steps + 3 points. `mould` moves every edge
    out by that much with the same two centres, so a frame has the same point count and one point per point."""
    b, h = width / 2.0, height - jamb
    if h <= b:
        raise ValueError("a pointed head rises more than its half width")
    xc = (h * h - b * b) / (2.0 * b)  # the centre of each arc is on the spring line, this far from the middle
    r = xc + b + mould
    rise = math.sqrt(r * r - xc * xc)
    half = b + mould
    pts = [(-half, 0.0), (-half, jamb)]
    a_apex = math.atan2(rise, -xc)  # the angle of the apex seen from the centre of the left arc
    for i in range(1, steps + 1):
        a = math.pi + (a_apex - math.pi) * i / steps
        pts.append((xc + r * math.cos(a), jamb + r * math.sin(a)))
    for i in range(1, steps + 1):
        a = (math.pi - a_apex) * (1.0 - i / steps)
        pts.append((-xc + r * math.cos(a), jamb + r * math.sin(a)))
    pts.append((half, 0.0))
    return pts


def door_half_width_at(width, jamb, height, z, mould=0.0):
    """Half the width of the door outline at the height z above the sill, 0.0 over the apex. `mould` moves the
    outline out as door_outline does (same two arc centres)."""
    b, h = width / 2.0, height - jamb
    if h <= b:
        raise ValueError("a pointed head rises more than its half width")
    if z <= jamb:
        return b + mould
    xc = (h * h - b * b) / (2.0 * b)
    r = xc + b + mould
    dz = z - jamb
    return max(math.sqrt(max(r * r - dz * dz, 0.0)) - xc, 0.0)


def back_course_spans(x0, x1, mould):
    """The stretches of the back face (local x0 to x1) that carry the inner plinth course: the course stops on each
    side of the ground door where its frame is (the frame is `mould` wider than the hole), so it never stands in
    front of the opening."""
    d = ground_door()
    w = door_half_width_at(d["width"], d["jamb"], d["height"], jp.Z_PLINTH, mould)
    return [(x0, d["x_centre"] - w), (d["x_centre"] + w, x1)]


def door_cut(width, jamb, height, z_cut, steps=6):
    """The door outline split at the height z_cut: (left foot up to the crossing, the head from crossing to crossing
    over the apex, crossing to the right foot). The two crossings lie on the outline at v = z_cut."""
    if height <= z_cut:
        raise ValueError("the apex must be above the cut")
    pts = door_outline(width, jamb, height, steps)
    above = [i for i, p in enumerate(pts) if p[1] > z_cut]
    i, j = above[0], above[-1]

    def cross(a, b):
        t = (z_cut - a[1]) / (b[1] - a[1])
        return (a[0] + t * (b[0] - a[0]), z_cut)

    left, right = cross(pts[i - 1], pts[i]), cross(pts[j], pts[j + 1])
    return pts[:i] + [left], [left] + pts[i:j + 1] + [right], [right] + pts[j + 1:]


def door_path():
    """The straight walk at Z 12 through the two side doors, as a rectangle."""
    return (-BODY_X, BODY_X, -DOOR_PATH_HALF, DOOR_PATH_HALF)


# ---- flights ---------------------------------------------------------------------------------------
def flights():
    """The three flights. direction +1 rises toward +X. x_foot is the X of the first riser."""
    return [
        {"name": "F1", "lane": LANE_B, "direction": 1, "x_foot": -3.36, "z0": 0.0, "risers": 24, "ceiling": "floor6"},
        {"name": "F2", "lane": LANE_A, "direction": -1, "x_foot": 3.5, "z0": 6.0, "risers": 24, "ceiling": "floor12"},
        {"name": "F3", "lane": LANE_A, "direction": -1, "x_foot": F3_FOOT, "z0": 12.0, "risers": 20,
         "ceiling": "platform"},
    ]


def flight_run(f):
    return f["risers"] * TREAD


def flight_x_top(f):
    return f["x_foot"] + f["direction"] * flight_run(f)


def flight_z_top(f):
    return f["z0"] + f["risers"] * RISER


def flight_footprint(f):
    xs = sorted((f["x_foot"], flight_x_top(f)))
    return (xs[0], xs[1], f["lane"][0], f["lane"][1])


def treads(f):
    """Per tread: index, its X range and the Z of its top face."""
    out = []
    for k in range(f["risers"]):
        a = f["x_foot"] + f["direction"] * TREAD * k
        b = f["x_foot"] + f["direction"] * TREAD * (k + 1)
        out.append({"k": k, "x0": min(a, b), "x1": max(a, b), "z": f["z0"] + RISER * (k + 1)})
    return out


def tread_z(f, x):
    """The Z of the top of the flight at X (the tread that holds X), or None outside the flight."""
    for t in treads(f):
        if t["x0"] - 1e-9 <= x <= t["x1"] + 1e-9:
            return t["z"]
    return None


def underside_z(f, x):
    """The Z of the sloped underside of a flight at X: flat on the lower floor at the foot, then parallel to the
    treads, END_FACE below the top tread at the top end."""
    u = min(max(abs(x - f["x_foot"]), 0.0), flight_run(f))
    return max(f["z0"], flight_z_top(f) - END_FACE - (flight_run(f) - u) * RISER / TREAD)


# ---- floors, holes, rails --------------------------------------------------------------------------
def hole_for(f, slab_bottom):
    """The hole in the slab above a flight: from the foot side of the first tread that has less than HEADROOM
    under the slab to the top end of the flight. Same width as the flight."""
    k_req = next(k for k in range(f["risers"]) if f["z0"] + RISER * (k + 1) > slab_bottom - HEADROOM + 1e-9)
    foot_side = f["x_foot"] + f["direction"] * TREAD * k_req
    top = flight_x_top(f)
    xs = sorted((foot_side, top))
    return (xs[0], xs[1], f["lane"][0], f["lane"][1])


def rails_for(hole, direction, clear_y0, clear_y1):
    """The rail rectangles (outside the hole): along each long side that is not against a wall or parapet, and
    across the foot end. The top end of the flight stays open."""
    x0, x1, y0, y1 = hole
    foot_x = x1 if direction < 0 else x0
    xa, xb = (x0, x1 + RAIL_T) if direction < 0 else (x0 - RAIL_T, x1)
    out = []
    if y1 < clear_y1 - 1e-6:
        out.append((xa, xb, y1, y1 + RAIL_T))
    if y0 > clear_y0 + 1e-6:
        out.append((xa, xb, y0 - RAIL_T, y0))
    out.append((foot_x, foot_x + RAIL_T, y0, y1) if direction < 0 else (foot_x - RAIL_T, foot_x, y0, y1))
    return out


def rail_is_along_x(rail):
    """Does the rail (x0, x1, y0, y1) run along X (a side rail of a hole) and not across it (the foot rail)?"""
    return (rail[1] - rail[0]) >= (rail[3] - rail[2])


def rail_cap_rect(rail, rails):
    """The plan rectangle (x0, x1, y0, y1) of the cap of `rail`, CAP_OVER wider than the rail. A rail across the foot
    of a hole meets a side rail at each end; the cap of the side rail is CAP_OVER wider and would cross the cap of
    the foot rail over a 4 cm by 24 cm square, in one plane. The cap of the foot rail stops at the cap of the side
    rail at such an end, so no two caps overlap."""
    x0, x1, y0, y1 = rail
    c = CAP_OVER
    if rail_is_along_x(rail):
        return (x0, x1, y0 - c, y1 + c)

    def meets(y, edge):
        """Is there a side rail whose edge `edge` (2: its low-Y side, 3: its high-Y side) lies at y, over this rail?"""
        return any(rail_is_along_x(s) and s is not rail and abs(s[edge] - y) < 1e-9
                   and min(x1, s[1]) - max(x0, s[0]) > 1e-9 for s in rails)

    lo = y0 + c if meets(y0, 3) else y0
    hi = y1 - c if meets(y1, 2) else y1
    return (x0 - c, x1 + c, lo, hi)


def flight_rail_sides(variant, f):
    """The (y0, y1) of each rail beside a flight: one on every long side of the lane that is not against the wall
    of the room. A rail stands outside the lane, so the 1.3 m clear width is not touched."""
    _, _, iy0, iy1 = interior_bounds(variant)
    y0, y1 = f["lane"]
    out = []
    if y0 > iy0 + 1e-6:
        out.append((round(y0 - RAIL_T, 9), y0))
    if y1 < iy1 - 1e-6:
        out.append((y1, round(y1 + RAIL_T, 9)))
    return out


def stair_rail_top(f, x):
    """The Z of the top of a rail beside flight f at X: RAIL_H over the line through the nosings (the front edge of
    each tread), so the rail is at least RAIL_H over every point of every tread."""
    u = abs(x - f["x_foot"])
    return f["z0"] + RISER * (u / TREAD + 1.0) + RAIL_H


def stair_rail_profile(f):
    """The (x, z) outline of the stone beside a flight: flat on the lower floor at the foot, then along the sloped
    underside of the flight, up to the underside of the slab above, back along that underside to where the line
    RAIL_H over the nosings reaches it, and down that line to the foot. Under the slab the rail ends flat on the
    slab, so no part of it enters the slab or its hole."""
    run, z_top = flight_run(f), flight_z_top(f)
    slab_bottom = z_top - SLAB_T
    u_flat = run - (z_top - f["z0"] - END_FACE) * TREAD / RISER
    u_cut = (slab_bottom - RAIL_H - f["z0"] - RISER) * TREAD / RISER
    if not 0.0 < u_cut < run:
        raise ValueError(f"the rail of {f['name']} meets the slab outside the flight")
    pts = [(0.0, f["z0"]), (u_flat, f["z0"]), (run, z_top - END_FACE), (run, slab_bottom), (u_cut, slab_bottom),
           (0.0, f["z0"] + RISER + RAIL_H)]
    return [(f["x_foot"] + f["direction"] * u, z) for u, z in pts]


def platform_clear_rect(variant, corbel=None):
    """The rectangle inside the platform parapet that the straight part of the platform covers. Variant b stands the
    parapet out by the corbel on every side, so its straight sides run tangent into the drum parapet."""
    pad = PARAPET_T - PARAPET_OUT  # the parapet inner face is this far inside the body face
    if variant == "a":
        return (-BODY_X + pad, BODY_X - pad, BODY_Y0 + pad, BODY_Y1 - pad)
    o = CORBEL_B if corbel is None else corbel
    return (-BODY_X - o + pad, BODY_X + o - pad, BODY_Y0 - o + pad, DRUM_CY)


def floors(variant):
    """Every slab: name, level (top face), bottom, its rectangle, holes and rails. The platform's rectangle is the
    clear deck inside the parapet; its rails keep clear of the parapet."""
    ix0, ix1, iy0, iy1 = interior_bounds(variant)
    fl = {f["ceiling"]: f for f in flights()}
    out = []
    for name, z in (("floor0", 0.0), ("floor6", 6.0), ("floor12", 12.0), ("platform", PLATFORM_Z)):
        rect = (ix0, ix1, iy0, iy1)
        clear_y0, clear_y1 = iy0, iy1
        if name == "platform":
            rect = platform_clear_rect(variant)
            clear_y0, clear_y1 = rect[2], rect[3]
        holes, rails = [], []
        if name in fl:
            hole = hole_for(fl[name], z - SLAB_T)
            holes.append(hole)
            rails += rails_for(hole, fl[name]["direction"], clear_y0, clear_y1)
        out.append({"name": name, "z_top": z, "z_bottom": z - SLAB_T, "rect": rect, "holes": holes, "rails": rails})
    return out


def _overlap(a, b):
    w = min(a[1], b[1]) - max(a[0], b[0])
    h = min(a[3], b[3]) - max(a[2], b[2])
    return w * h if w > 1e-9 and h > 1e-9 else 0.0


def min_headroom(variant, with_holes=True):
    """The least clear height over any tread: the lowest slab (less its holes) or other flight above it.
    Returns the number with the flight and the tread that has it."""
    slabs = floors(variant)
    fls = flights()
    worst = {"headroom": 99.0, "flight": None, "tread": None}
    for f in fls:
        for t in treads(f):
            rect = (t["x0"], t["x1"], f["lane"][0], f["lane"][1])
            best = 99.0
            for s in slabs:
                if s["z_bottom"] < t["z"] - 1e-9:
                    continue
                solid = _overlap(rect, s["rect"])
                if with_holes:
                    solid -= sum(_overlap(rect, h) for h in s["holes"])
                if solid > 1e-9:
                    best = min(best, s["z_bottom"] - t["z"])
            for g in fls:
                if g is f or _overlap(rect, flight_footprint(g)) <= 1e-9:
                    continue
                lo, hi = max(t["x0"], flight_footprint(g)[0]), min(t["x1"], flight_footprint(g)[1])
                for x in (lo, hi, 0.5 * (lo + hi)):
                    u = underside_z(g, x)
                    if u > t["z"] - 1e-9:
                        best = min(best, u - t["z"])
            if best < worst["headroom"]:
                worst = {"headroom": best, "flight": f["name"], "tread": t["k"]}
    return worst


# ---- the platform ----------------------------------------------------------------------------------
def platform_obstacles(variant):
    """The hatch and its rails, the rails widened by their cap, as rectangles."""
    plat = [f for f in floors(variant) if f["name"] == "platform"][0]
    out = list(plat["holes"])
    out += [(r[0] - CAP_OVER, r[1] + CAP_OVER, r[2] - CAP_OVER, r[3] + CAP_OVER) for r in plat["rails"]]
    return out


def _region_contains(variant, rect, corbel):
    """Is the rectangle on the clear deck? Variant a: the rectangle inside the parapet. Variant b: the straight back
    part, and past the drum centre line a disk of the drum radius plus the corbel, less the parapet."""
    x0, x1, y0, y1 = rect
    base = _inset(platform_clear_rect(variant, corbel), variant)
    if variant == "a":
        return x0 >= base[0] - 1e-9 and x1 <= base[1] + 1e-9 and y0 >= base[2] - 1e-9 and y1 <= base[3] + 1e-9
    if x0 < base[0] - 1e-9 or x1 > base[1] + 1e-9 or y0 < base[2] - 1e-9:
        return False
    if y1 <= DRUM_CY + 1e-9:
        return True
    r = DRUM_R + corbel + PARAPET_OUT - PARAPET_T - PANEL_RELIEF_IN
    return all(_in_disk(x, y, 0.0, DRUM_CY, r) for x in (x0, x1) for y in (max(y0, DRUM_CY), y1))


def _inset(rect, variant):
    """The clear rectangle less the relief of the parapet panels, on the sides that have a parapet."""
    d = PANEL_RELIEF_IN
    return (rect[0] + d, rect[1] - d, rect[2] + d, rect[3] - (d if variant == "a" else 0.0))


def platform_region_rect(variant):
    """The bounding box that fit_rect scans."""
    base = _inset(platform_clear_rect(variant), variant)
    if variant == "a":
        return base
    r = DRUM_R + CORBEL_B + PARAPET_OUT - PARAPET_T - PANEL_RELIEF_IN
    return (base[0], base[1], base[2], DRUM_CY + r)


def fit_rect(variant, w, h, corbel=None, step=0.05):
    """A place for a w (X) by h (Y) rectangle on the clear deck, outside the hatch and its rails: the lower left
    corner (x, y), or None. Variant b uses its corbel (CORBEL_B unless given)."""
    corbel = CORBEL_B if corbel is None else corbel
    obstacles = platform_obstacles(variant)
    bx0, bx1, by0, by1 = platform_region_rect(variant)
    nx = int((bx1 - bx0 - w) / step + 1e-9) + 1
    ny = int((by1 - by0 - h) / step + 1e-9) + 1
    for i in range(max(nx, 0)):
        for j in range(max(ny, 0)):
            x, y = bx0 + i * step, by0 + j * step
            rect = (x, x + w, y, y + h)
            if all(_overlap(rect, o) == 0.0 for o in obstacles) and _region_contains(variant, rect, corbel):
                return (x, y)
    return None


def platform_parapet_parts(variant):
    """The parapet parts of the platform: per part its face and the local X range along that face. The local frame
    of a face is the wall frame: front is the +Y face, back the -Y face, east the +X face, west the -X face; local x
    is world X on front and back, 1.0 - Y on east, Y - 1.0 on west (variant a). Front and back parts run the whole
    width; east and west parts run between them.
    Variant b: back (local x is minus world X), east (local x is DRUM_CY minus Y) and west (Y minus DRUM_CY) run from
    the back parapet to the tangent line, where the five arc parts follow: face "arc", from th0 to th1 degrees
    counter-clockwise from +X, edges on the pier angles."""
    if variant == "b":
        o = platform_offset(variant)
        half = BODY_X + o + PARAPET_OUT
        side = DRUM_CY - (BODY_Y0 - o + PARAPET_T - PARAPET_OUT)
        edges = [-half + 2.0 * half * i / 3 for i in range(4)]
        out = [{"face": "back", "x0": a, "x1": b} for a, b in zip(edges, edges[1:])]
        out.append({"face": "east", "x0": 0.0, "x1": side})
        out.append({"face": "west", "x0": -side, "x1": 0.0})
        angles = [0.0] + list(PIER_ANGLES_B) + [180.0]
        out += [{"face": "arc", "th0": a, "th1": b} for a, b in zip(angles, angles[1:])]
        return out
    out = []
    half_long = BODY_X + PARAPET_OUT
    half_side = 0.5 * (BODY_Y1 - BODY_Y0) + PARAPET_OUT - PARAPET_T
    for face, half, n in (("front", half_long, 3), ("back", half_long, 3), ("east", half_side, 2), ("west", half_side, 2)):
        edges = [-half + 2.0 * half * i / n for i in range(n + 1)]
        for a, b in zip(edges, edges[1:]):
            out.append({"face": face, "x0": a, "x1": b})
    return out


CORNER_CAP_INSET = 0.003  # the block ends this much short of the outer face of the side cap: no two faces coplanar
CORNER_CAP_LENGTH = jp.PARAPET_CAP_OVER - CORNER_CAP_INSET  # the block of cap that closes a corner


def corner_ends(variant, part):
    """(low end, high end) of a parapet part: is that end on a corner of the platform? The first and last part of
    the front and back face (variant a) or of the back face (variant b) end on a corner. The east and west parts
    end on the front and back parts (variant a) or on the tangent line (variant b), the arc parts on the piers."""
    face = part["face"]
    if face not in ("front", "back") or (variant == "b" and face == "front"):
        return (False, False)
    same = [p for p in platform_parapet_parts(variant) if p["face"] == face]
    return (part["x0"] <= min(p["x0"] for p in same) + 1e-9, part["x1"] >= max(p["x1"] for p in same) - 1e-9)


CORNER_FRIEZE_LENGTH = jp.PARAPET_Y1 - jp.PARAPET_Y0  # the frieze block runs over the end face of the neighbour


def frieze_ends(variant, part):
    """(low end, high end) of an east or west parapet part: is that end on a corner of the platform? Variant a: the
    outer end of the first and of the last part of the face. Variant b: the end at the back face (high local x on
    the east side, low on the west side); the other end is the tangent line, where the arc parts follow."""
    if part["face"] not in ("east", "west"):
        return (False, False)
    if variant == "b":
        return (False, True) if part["face"] == "east" else (True, False)
    same = [p for p in platform_parapet_parts(variant) if p["face"] == part["face"]]
    return (part["x0"] <= min(p["x0"] for p in same) + 1e-9, part["x1"] >= max(p["x1"] for p in same) - 1e-9)


# ---- the front of variant a --------------------------------------------------------------------------
FRONT_PIERS = (-BODY_X, 0.0, BODY_X)               # a pier on each corner and one in the middle
FRONT_HALF_BAY = BODY_X / 2.0                      # half the pier spacing: the ribs of neighbours meet in the bay
FRONT_ARCH_X = tuple(0.5 * (a + b) for a, b in zip(FRONT_PIERS, FRONT_PIERS[1:]))  # one arch in each bay
FRONT_ARCH_W = 2.2                                 # both tiers; the clear wall in a bay is 3.8 m


# ---- the ledge of the platform slab, variant b ------------------------------------------------------
def ledge_uv(x, y, polar, r_ref, x_low=PIECE_X0):
    """(u, v) of a horizontal face of the ring of slab that stands out past the parapet. On the drum (`polar`) U runs
    along the arc at radius r_ref, at TD per metre, and V across the ring, so the stone courses follow the curve;
    U continues the flat side (U = TD y there) over the tangent point and mirrors on the -X half. Elsewhere the wall
    rule for a horizontal face."""
    if not polar:
        return jp.world_uv((0.0, 0.0, 1.0), (x, y, 0.0), x_low)
    th = math.atan2(y - DRUM_CY, x)
    r = math.hypot(x, y - DRUM_CY)
    return (jp.TD * (DRUM_CY + r_ref * min(th, math.pi - th)), jp.TD * (r - r_ref))


# ---- the vertex colour cells -------------------------------------------------------------------------
# Cells of the body, the drum and the platform: the colour is a soft wash. Tower b is over its triangle budget with
# cells of 2.2 m (15,540 against 14,000), so its cells are 4.4 m; tower a has room for 2.2 m.
CELL_BODY = {"a": 2.0 * jp.CELL_MAX, "b": 8.0 * jp.CELL_MAX}   # along X and along Z
END_REACH = BODY_X + 1.0           # a shell that reaches past this X is a stub: it ends in a joint plane


PLINTH_CELL = 1.5                  # the plinth of the body: its two rows (Z 0 to 1.5 to 3.0) are already in the mesh


def cell_max_for(variant, x_lo, x_hi, z_hi):
    """The largest cell (along Z) of a shell that spans X x_lo to x_hi and reaches up to z_hi. A shell that reaches a
    stub (past END_REACH) keeps the wall's cell (jp.CELL_MAX), so that the vertex colour of the end plane has the
    vertices of the wall. A shell in the plinth zone (up to Z 3.0) takes PLINTH_CELL: the damp that darkens the foot
    fades out within 1.5 m, and the rows of 1.5 m differ from the wall's rows of 1.0 m by less than 5 levels. Every
    other shell takes CELL_BODY of its variant."""
    if is_end_shell(x_lo, x_hi):
        return jp.CELL_MAX
    if z_hi <= jp.Z_PLINTH + 1e-9:
        return PLINTH_CELL
    return CELL_BODY[variant]


def is_end_shell(x_lo, x_hi):
    """Does a shell that spans X x_lo to x_hi reach a stub, so that it ends in a joint plane?"""
    return max(abs(x_lo), abs(x_hi)) > END_REACH


def wants_x_cuts(x_lo, x_hi):
    """Is a shell cut along X? An end shell is not: it is one stub (at most 2.3 m wide), and the vertex colour at its
    end plane depends on the Z of the vertices on it, which the cuts along Z give (the wall's own cell). The wash
    changes over the stub by less than the seam limit, so one column of cells is enough."""
    return not is_end_shell(x_lo, x_hi)


# ---- the sill under a blind arch on the back face of tower a ----------------------------------------------
SILL_OVER = 0.0     # the sill ends flush with the outer faces of the outer legs (version 6), less FLUSH_INSET
FLUSH_INSET = 0.003  # a band or sill ends this much inside the outer face of a leg: its end face is more than 2 mm from the plane of the leg
SILL_PROUD = 0.06   # and stands this far out past the front of the frame
SILL_DROP = 0.16    # its underside is this far under the foot of the frame
SILL_RISE = 0.02    # its top is this far over the foot of the frame, so the legs stand in it and no face is coplanar


def sill_x_range(cx, width):
    """The X range of the sill under a frame of the given outer width centred on cx."""
    half = 0.5 * width + SILL_OVER - FLUSH_INSET
    return (cx - half, cx + half)


def sill_profile(z_foot, y_back, y_front):
    """The (y, z) profile of a sill, in the local frame of a face (y out): from the back of the frame to a nose
    SILL_PROUD past its front, flat on top over the frame and cut back at the nose. The top is SILL_RISE over the
    foot of the frame (z_foot) and the underside SILL_DROP under it."""
    y_nose = y_front + SILL_PROUD
    z0, z1 = z_foot - SILL_DROP, z_foot + SILL_RISE
    return [(y_back, z0), (y_nose, z0), (y_nose, z1 - SILL_PROUD), (y_front, z1), (y_back, z1)]


# ---- UV of a stone or plinth face on the drum ----------------------------------------------------------------
DRUM_FACE_R_MIN = 4.0  # a face nearer the drum centre than this (a hole in a floor) takes the flat rule


def drum_uv(n, co, mid, x_low, plinth=False):
    """(u, v) of a face on or near the drum of variant b. `n` is the face normal, `co` the point, `mid` the (x, y)
    of the middle of the face in plan, `plinth` selects the plinth material (t_rivendell_wall_rubble_a).
    Faces on the flat part and horizontal faces take the wall's rule (jp.world_uv, or jp.plinth_uv for the plinth).
    U on the drum runs along the arc at the face radius at TD per metre and continues the flat side faces (U = TD y
    there) over the tangent point, mirrored on the -X half. V is TD z on a vertical stone face and TD z / tz on a
    battered one (the slope distance from the ground line), so the bed joints run level round the drum and into
    the wall. The plinth carries its bed joint at V jp.PLINTH_V_TOP on the top edge (Z 3.0): jp.plinth_v_slope on
    a battered face, jp.plinth_v_vertical on a vertical one."""
    flat = jp.plinth_uv if plinth else jp.world_uv
    if abs(n[2]) > 0.98:
        return flat(n, co, x_low)
    mx, my = mid
    r_mid = math.hypot(mx, my - DRUM_CY)
    if my <= DRUM_CY + 1e-6 or r_mid < DRUM_FACE_R_MIN:
        return flat(n, co, x_low)
    z = co[2]
    n_rad = (n[0] * mx + n[1] * (my - DRUM_CY)) / r_mid
    if abs(n[2]) < jp.SLOPE_MIN:
        v = jp.plinth_v_vertical(z) if plinth else jp.TD * z
    else:
        tz = abs(n_rad) / math.hypot(n_rad, n[2])
        v = jp.plinth_v_slope(z, tz) if plinth else jp.TD * z / tz
    if abs(n_rad) < 0.2 and abs(n[2]) < jp.SLOPE_MIN:
        return (jp.TD * math.hypot(co[0], co[1] - DRUM_CY), v)  # a flat face that stands along a radius
    th = math.atan2(co[1] - DRUM_CY, co[0])
    return (jp.TD * (DRUM_CY + r_mid * min(th, math.pi - th)), v)


# ---- the ring of the platform slab, variant b ------------------------------------------------------------------
SLAB_PROUD = 0.35   # the platform slab stands this far out past the corbel line of the parapet
LEDGE_UNDER = 0.1   # the ring of the slab reaches this far in under the outer face of the parapet


def ledge_offsets(variant):
    """(outer, inner): how far the outer edge of the ring of slab and its inner edge (under the parapet) stand out
    from the body outline."""
    o = platform_offset(variant)
    return (o + SLAB_PROUD, o + PARAPET_OUT - LEDGE_UNDER)


def ledge_mid_radius():
    """The radius of the middle of the ring on the drum: the reference of U on the ring (ledge_uv), so that the
    density over the ring stays within 5 percent."""
    outer, inner = ledge_offsets("b")
    return DRUM_R + 0.5 * (outer + inner)



# ---- version 6: piers that die at the string course, the string course round the corners, flush bands -------------
DRUM_PILASTER_END_PROUD = -0.04  # a flat drum pilaster ends this far inside the wall (its edges stand 3.6 mm out of the round)
UPPER_TIER_Z0 = jp.STRING_Z1 - 0.05   # the foot of the frames of the upper tier: 5 cm inside the top of the string course
BAY_TIER1_W = 1.4                     # variant b: the lower blind arch of a bay (outer width)
SIDE_ARCH_X = {"east": -2.7, "west": 2.7}  # variant a: the blind arch of a side face, local x (1.8 m from the front pier)
UP_RIB = (12.6, 15.0, 62.0, 0.46, 0.28, 0.40, 0.20, 0.33)  # the ribs of the drum that spring from the upper collar


def corner_pier_parts(front):
    """The solids of a corner pier as (proud at the bottom, z, proud at the top, z) of each part, for a pier that stands
    square at a corner of the body. Front corners: the wall's pier (1.0 m proud at the ground, 0.5 at Z 8.5), then
    the pilaster 0.3 m proud up to the top of the collar, then one straight slope that dies at the foot of the string
    course (jp.pilaster_proud). Back corners: a plain quoin 0.3 m proud with the same taper. Nothing stands above
    the string course, so no pier meets a cornice from below."""
    taper = (jp.PILASTER_PROUD, jp.PILASTER_TAPER_Z0, 0.0, jp.STRING_Z0)
    if front:
        g, t = jp.PIER_PROUD_GROUND, jp.PIER_PROUD_TOP
        return [(g, jp.PIER_FOOT_Z, g, 0.0), (g, 0.0, t, jp.PIER_TOP_Z),
                (jp.PILASTER_PROUD, jp.PIER_TOP_Z, jp.PILASTER_PROUD, jp.PILASTER_TAPER_Z0), taper]
    q = jp.PILASTER_PROUD
    return [(q, jp.PIER_FOOT_Z, q, jp.PILASTER_TAPER_Z0), taper]


def drum_pier_parts():
    """The solids of an engaged pier of the drum: the corner pier of the front, but the taper ends DRUM_PILASTER_END_PROUD
    inside the wall, because the flat front of the pilaster stands out of the round wall at its edges."""
    parts = corner_pier_parts(True)
    p0, z0, _, z1 = parts[-1]
    return parts[:-1] + [(p0, z0, DRUM_PILASTER_END_PROUD, z1)]


STRING_STUB_Y = (jp.Y_IN - jp.INNER_STRING_PROUD, jp.Y_OUT + jp.STRING_PROUD)  # back, front: the outer faces of the stub courses


def string_course_corner_paths():
    """The two open polylines (plan points) that the string course follows round the corners of the body, each from the
    course of one wall stub to the course of the other, counter clockwise: the front (round the two front corners) and
    the back (round the two back corners)."""
    front = [(BODY_X, STRING_STUB_Y[1]), (BODY_X, BODY_Y1), (-BODY_X, BODY_Y1), (-BODY_X, STRING_STUB_Y[1])]
    back = [(-BODY_X, STRING_STUB_Y[0]), (-BODY_X, BODY_Y0), (BODY_X, BODY_Y0), (BODY_X, STRING_STUB_Y[0])]
    return front, back


def wrap_path(points, max_len):
    """Sections (x, y, mx, my) of a member that follows the open polyline `points` (counter clockwise, the outside on the
    right of the way): the corners of the polyline, and as many equal sections between them as keep every step at most
    `max_len`. (mx, my) is the offset vector: the point of a profile at offset d is (x + mx d, y + my d). A corner takes
    the mitre vector, an end the normal of its edge."""
    normals = [_edge_normal(a, b) for a, b in zip(points, points[1:])]
    out = []
    for i, (x, y) in enumerate(points):
        if i == 0:
            vec = normals[0]
        elif i == len(points) - 1:
            vec = normals[-1]
        else:
            n1, n2 = normals[i - 1], normals[i]
            k = 1.0 / (1.0 + n1[0] * n2[0] + n1[1] * n2[1])
            vec = ((n1[0] + n2[0]) * k, (n1[1] + n2[1]) * k)
        out.append((x, y, vec[0], vec[1]))
    res = [out[0]]
    for a, b in zip(out, out[1:]):
        k = max(1, int(math.ceil(math.hypot(b[0] - a[0], b[1] - a[1]) / max_len - 1e-9)))
        for j in range(1, k):
            t = j / k
            res.append(tuple(a[c] + (b[c] - a[c]) * t for c in range(4)))
        res.append(b)
    return res


def band_spans(face):
    """The stretches of the carved band of a face of tower a (local x along the face), one under each group of blind
    arches, each as wide as the outer faces of the outer legs (version 6; FLUSH_INSET inside them, so that the end face of
    the band is not in the plane of the leg): the front has one arch in each bay, a side face has one arch between the
    corner pier and the stub."""
    if face == "front":
        return [(cx - FRONT_ARCH_W / 2.0 + FLUSH_INSET, cx + FRONT_ARCH_W / 2.0 - FLUSH_INSET) for cx in FRONT_ARCH_X]
    cx = SIDE_ARCH_X[face]
    return [(cx - jp.ARCH_W / 2.0 + FLUSH_INSET, cx + jp.ARCH_W / 2.0 - FLUSH_INSET)]


def drum_bay_centres():
    """The angles (degrees) of the middle of each full bay of the drum, between two piers."""
    a = PIER_ANGLES_B
    return [0.5 * (x + y) for x, y in zip(a, a[1:])]


def drum_band_half_angle():
    """Half the angle (radians) that the carved band of a bay covers: the arc of the lower arch of the bay."""
    return (BAY_TIER1_W / 2.0 - FLUSH_INSET) / DRUM_R


def drum_half_bay():
    """Half a bay of the drum as an arc length at the drum radius: the half bay of the rib functions."""
    return math.radians((PIER_ANGLES_B[1] - PIER_ANGLES_B[0]) / 2.0) * DRUM_R


def upper_collar_profile():
    """The carved collar that carries the upper ribs of the drum, as (proud, z) points from the wall face (variant b: the
    pier has died at the string course, so the collar stands on the wall alone). It grows out of the wall in a slope,
    keeps a flat front COLLAR_PROUD out, and slopes back into the wall: no flat ledge under it or over it. The ends
    lie DRUM_PILASTER_END_PROUD-like inside the wall (8 cm), so the slopes meet the round wall in a clean line."""
    z0 = jp.STRING_Z1 + 1.15
    return [(-0.08, z0), (jp.COLLAR_PROUD, z0 + 0.45), (jp.COLLAR_PROUD, z0 + 0.75), (-0.08, z0 + 1.15)]


FINE_TURN_DEG = 3.75  # the turn between two neighbour segments of a fine curve (the wall's jp.ARCH_STEPS_FINE gives 3.75)


def door_fine_steps(width, jamb, height, max_deg=FINE_TURN_DEG):
    """The steps on each arc of a door head (door_outline) that keep the turn between two neighbour segments at most
    `max_deg`. The arc turns by atan2(rise, xc) from its springing to the apex."""
    b, h = width / 2.0, height - jamb
    xc = (h * h - b * b) / (2.0 * b)
    return int(math.ceil(math.degrees(math.atan2(h, xc)) / max_deg - 1e-9))


def stub_rib_clip_x(variant):
    """The |X| where the ribs of a wall stub are cut, 5 cm inside the solid they run into: the body of tower a, or
    for tower b a place in the drum wall where the whole front of the rib is under the drum face (the stub starts
    at X 4.4, where the round wall is only 0.37 m in front of the stub face, and a rib stands 0.52 m proud)."""
    if variant == "a":
        return BODY_X - 0.05
    return 4.2


# ---- version 6: where the triangle budget (14,000 a tower) makes a curve coarser --------------------------------------
# The fine curves (ribs at jp.RIB_MAX_TURN_DEG, frames at jp.ARCH_STEPS_FINE, FINE_TURN_DEG a segment) are on every part
# that is seen from the outside or from the platform. The groups below are coarser, in the order of how little they are
# seen: the arches and the ground door of the town side (the back of the tower), the narrow window frame of the drum,
# the ribs of its upper tier, and the foundation under the ground.
TOWN_FRAME_STEPS = 8      # the blind arches of the back of tower a (60 / 8 = 7.5 degrees a segment)
GROUND_DOOR_STEPS = 8     # the ground door of either tower, in the back wall
WINDOW_FRAME_STEPS = 6    # the narrow window frame (0.7 m wide) in each bay of the drum (10 degrees a segment)
UP_RIB_TURN = 12.0        # the upper ribs of the drum
FOUNDATION_STEPS_B = 6    # the foundation of the drum, under the ground
SIDE_DOOR_STEPS = 8       # the head of each side door, its frame and its reveal (7.5 degrees a segment)
DRUM_MEMBER_DEG = 10.0    # the sections of the string course and the plinth cap along the drum (the wall behind has 5)
PARAPET_ARC_DEG = 12.0    # the columns, the frieze and the cap of the platform parapet on the arc of the drum
CORNICE_STEPS_B = 18      # the steps of the two courses of the cornice round the drum (10 degrees; the wall below has 5)
DRUM_ARCH_STEPS = 10      # the blind arch of each bay of the drum (60 / 10 = 6 degrees a segment)
