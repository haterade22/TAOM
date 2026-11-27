"""Drum geometry of the Rivendell wall tower, variant b (concept t2), as bmesh builders.
Used by tools/oneoff/blender_build_rivendell_tower.py. The numbers are in tools/oneoff/rivendell_tower_plan.py.

Plan frame: the drum is a half circle of radius DRUM_R about (0, DRUM_CY). An angle runs from 0 at the +X tangent
point, over 90 at the front centre, to 180 at the -X tangent point.

A Loft sweeps a closed profile along a list of sections (a closed shell). Sections come from a path (a polyline of
plan points with an offset vector each) or from a ring of angles. A Build `B` is a wall.Build or a TBuild: it must
have vert, face, seal, direct, trim, pend and terrain.

UV rules on the drum (plan.drum_uv): stone and plinth faces take U along the arc at the face radius, V = TD z on a
vertical stone face (so the courses stay level into the wall) and the plinth rule on the plinth (the bed joint at V
1.3767 on the top edge, Z 3.0), and the density is TD. U continues the flat side faces (U = TD y there) over the tangent point and
mirrors on the -X half, so it has no jump at the front centre. The ring of the platform slab that stands out past
the parapet (the ledge) takes U along the arc and V across the ring (ledge_uv, the rule is in the plan module).

A parapet part on the arc is built like the wall's (wall.build_parapet): a slab with pointed openings laid out by
jp.parapet_openings, a flat frieze band under the cap on the outer face, and the cap. No raised plates.
"""
import math

import blender_build_rivendell_wall as wall
import rivendell_joint_profile as jp
import rivendell_tower_plan as plan

CY = plan.DRUM_CY
R = plan.DRUM_R
BX = plan.BODY_X
TD = jp.TD
STEPS = plan.DRUM_STEPS
STONE, RUBBLE = wall.STONE, wall.RUBBLE
COLUMN_DEG, FRIEZE_DEG, CAP_DEG = plan.PARAPET_ARC_DEG, plan.PARAPET_ARC_DEG, plan.PARAPET_ARC_DEG
LEDGE_R = plan.ledge_mid_radius()  # the middle of the ring of slab that stands out past the parapet


# ---- UV rules ----------------------------------------------------------------------------------------
def stone_uv(v, fc, x_low, plinth=False):
    """(u, v) of a stone face (or, with `plinth`, a face of the plinth material) on or near the drum. The rule is
    plan.drum_uv: U along the arc at TD, V = TD z on a vertical stone face (so every bed joint runs level into the
    wall), the plinth's bed joint at V jp.PLINTH_V_TOP on its top edge (Z 3.0)."""
    verts = fc.verts
    mid = (sum(p.co.x for p in verts) / len(verts), sum(p.co.y for p in verts) / len(verts))
    return plan.drum_uv(tuple(fc.normal), tuple(v.co), mid, x_low, plinth)


def ledge_uv(v, fc, x_low):
    """(u, v) of a face of the ring of the platform slab that stands out past the parapet. A face on the drum (its
    middle is past the tangent line) runs U along the arc and V across the ring, so the courses follow the curve. A
    face of the straight side takes the wall's rule for a horizontal face."""
    my = sum(p.co.y for p in fc.verts) / len(fc.verts)
    return plan.ledge_uv(v.co.x, v.co.y, my > CY + 1e-6, LEDGE_R, x_low)


def rigid_uv(v, fc, th):
    """The wall's world rule in the frame of a pier that stands at angle th (radians): x along the face, y out."""
    s, c = math.sin(th), math.cos(th)
    n, co = fc.normal, v.co
    nt, nr = -n.x * s + n.y * c, n.x * c + n.y * s
    x, y = -co.x * s + (co.y - CY) * c, co.x * c + (co.y - CY) * s
    return jp.world_uv((nt, nr, n.z), (x, y, co.z), 0.0)


def rigid_trim_uv(v, fc, th, lo, hi):
    """The trim rule (strip S2, running along the pier front) in the frame of a pier at angle th (radians); lo and hi
    are the box of the member in that frame (x along the front, y out from the drum centre, z)."""
    s, c = math.sin(th), math.cos(th)
    n, co = fc.normal, v.co
    nt, nr = -n.x * s + n.y * c, n.x * c + n.y * s
    x, y = -co.x * s + (co.y - CY) * c, co.x * c + (co.y - CY) * s
    return jp.trim_uv((nt, nr, n.z), (x, y, co.z), "S2", 0, lo, hi)


# ---- sections and lofts ------------------------------------------------------------------------------
def flat_profile(sec):
    """A section (3D points of a closed profile) as (u, z) pairs, u along the plan line of the section."""
    p0 = sec[0]
    far = max(sec, key=lambda p: math.hypot(p[0] - p0[0], p[1] - p0[1]))
    d = math.hypot(far[0] - p0[0], far[1] - p0[1])
    ex, ey = ((far[0] - p0[0]) / d, (far[1] - p0[1]) / d) if d > 1e-9 else (1.0, 0.0)
    return [((p[0] - p0[0]) * ex + (p[1] - p0[1]) * ey, p[2]) for p in sec]


class Loft:
    """A closed shell swept through `secs` (lists of 3D points, the same count in each). `closed` joins the last
    section to the first (no caps)."""

    def __init__(self, B, secs, closed=False, caps=True):
        self.B, self.secs, self.closed = B, secs, closed
        self.verts = [[B.vert(p) for p in s] for s in secs]
        n, m = len(secs), len(secs[0])
        self.quads = []
        for i in range(n if closed else n - 1):
            a, b = self.verts[i], self.verts[(i + 1) % n]
            self.quads.append([B.face(a[j], a[(j + 1) % m], b[(j + 1) % m], b[j]) for j in range(m)])
        self.caps = []
        if caps and not closed:
            for ring, sec in ((self.verts[0], secs[0]), (self.verts[-1], secs[-1])):
                for t in wall.tess([flat_profile(sec)]):
                    f = B.face(*[ring[k] for k in t])
                    if f is not None:
                        self.caps.append(f)
        self.faces = B.seal([f for row in self.quads for f in row] + self.caps)

    def stone(self, mat):
        self.B.terrain(self.faces, mat)

    def trim(self, strip, s, prof):
        """Direct UVs: U = TD s[i] along the sweep, V across the strip. `prof` is the (offset, z) profile."""
        v0, v1 = jp.STRIPS[strip]
        d = [p[0] for p in prof]
        z = [p[1] for p in prof]
        dmin, dext = min(d), max(max(d) - min(d), 1e-9)
        zmin, zext = min(z), max(max(z) - min(z), 1e-9)
        m = len(prof)
        tv = []
        for j in range(m):
            (d0, z0), (d1, z1) = prof[j], prof[(j + 1) % m]
            if abs(z1 - z0) >= abs(d1 - d0):
                tv.append(((z0 - zmin) / zext, (z1 - zmin) / zext))
            else:
                tv.append(((d0 - dmin) / dext, (d1 - dmin) / dext))
        n = len(self.secs)
        for i, row in enumerate(self.quads):
            a, b = self.verts[i], self.verts[(i + 1) % n]
            for j, f in enumerate(row):
                if f is None or not f.is_valid:
                    continue
                j2 = (j + 1) % m
                ta, tb = tv[j]
                self.B.direct(f, {a[j]: (TD * s[i], v0 + (v1 - v0) * ta), a[j2]: (TD * s[i], v0 + (v1 - v0) * tb),
                                  b[j2]: (TD * s[i + 1], v0 + (v1 - v0) * tb),
                                  b[j]: (TD * s[i + 1], v0 + (v1 - v0) * ta)})
        self.B.trim(self.caps, strip, 0)


def polar_sections(prof, thetas):
    """Sections of a profile of (radius, z) at the given angles (radians)."""
    return [[(r * math.cos(th), CY + r * math.sin(th), z) for r, z in prof] for th in thetas]


def drum_thetas():
    return [math.pi * i / STEPS for i in range(STEPS + 1)]


def path_sections(path, prof):
    """Sections of a profile of (offset, z) along a path of (x, y, mx, my): the point is the path point plus the
    offset vector times the offset."""
    return [[(x + mx * d, y + my * d, z) for d, z in prof] for x, y, mx, my in path]


def path_lengths(path, closed=False):
    pts = list(path) + ([path[0]] if closed else [])
    s = [0.0]
    for a, b in zip(pts, pts[1:]):
        s.append(s[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    return s


def drum_member_paths(y_back, y_stub_back, y_stub_front):
    """The paths of the members that run round the body (string course, plinth cap, band), in three pieces that stop
    at the two wall stubs, so that no member runs on inside a stub or crosses its courses: on each side the strip of
    the flat side face from `y_back` (inside the back quoin) to `y_stub_back` (the outer face of the stub's inner
    course), and the arc from the height `y_stub_front` (the outer face of the stub's outer course) on one side to
    the same height on the other. A point is (x, y, mx, my): the offset vector (mx, my) is the outward normal."""
    east = [(BX, y_back, 1.0, 0.0), (BX, y_stub_back, 1.0, 0.0)]
    west = [(-BX, y_stub_back, -1.0, 0.0), (-BX, y_back, -1.0, 0.0)]
    th0 = math.asin((y_stub_front - CY) / R)
    ths = [th0] + [th for th in drum_thetas() if th0 + 1e-9 < th < math.pi - th0 - 1e-9] + [math.pi - th0]
    arc = [(R * math.cos(th), CY + R * math.sin(th), math.cos(th), math.sin(th)) for th in ths]
    return [east, arc, west]


def body_path(variant, steps=STEPS):
    """The closed path of the body outline in plan with the mitre vectors, for members that wrap the whole body."""
    pts = plan.outline(variant, steps)
    n = len(pts)
    out = []
    for i in range(n):
        n1 = plan._edge_normal(pts[i - 1], pts[i])
        n2 = plan._edge_normal(pts[i], pts[(i + 1) % n])
        k = 1.0 / (1.0 + n1[0] * n2[0] + n1[1] * n2[1])
        out.append((pts[i][0], pts[i][1], (n1[0] + n2[0]) * k, (n1[1] + n2[1]) * k))
    return out


def wall_ring(B, r_in, r_out, z0, z1, y_join):
    """The drum wall as one closed ring of stone from the plane Y = y_join on the +X side to the same plane on the -X
    side (the flat side walls and the door cut end there), in 5 degree slices."""
    def sec(pi, po):
        return [(pi[0], pi[1], z0), (po[0], po[1], z0), (po[0], po[1], z1), (pi[0], pi[1], z1)]

    def ang(r, th):
        return (r * math.cos(th), CY + r * math.sin(th))

    secs = [sec((plan.drum_x(r_in, y_join), y_join), (plan.drum_x(r_out, y_join), y_join))]
    for k in range(1, STEPS):
        th = math.pi * k / STEPS
        secs.append(sec(ang(r_in, th), ang(r_out, th)))
    secs.append(sec((-plan.drum_x(r_in, y_join), y_join), (-plan.drum_x(r_out, y_join), y_join)))
    lo = Loft(B, secs)
    lo.stone(STONE)
    return lo


def engaged_pier(B, th_deg):
    """A pier of the wall's own profile (1.0 m proud at the ground, 0.5 at Z 8.5, the 0.3 m pilaster up to the top of the
    collar, then one straight slope that dies into the wall at the foot of the string course: plan.drum_pier_parts)
    standing at the angle th_deg: a flat front, 1.2 m wide, set 0.6 m into the wall."""
    th = math.radians(th_deg)
    s, c = math.sin(th), math.cos(th)
    h, back = jp.PIER_HALF, R - 0.6

    def pt(x, rho, z):
        return (c * rho - s * x, CY + s * rho + c * x, z)

    def ring(p, z):
        return tuple(pt(x, rho, z) for x, rho in ((-h, back), (h, back), (h, R + p), (-h, R + p)))

    for p0, z0, p1, z1 in plan.drum_pier_parts():
        for f in wall.hexa(B, ring(p0, z0), ring(p1, z1)):
            f.material_index = STONE
            B.pend.append((f, lambda v, fc, th=th: rigid_uv(v, fc, th)))
    # the carved collar where the rib limbs spring: the wall's collar line, a little wider than the pier
    hc = jp.COLLAR_HALF_W
    collar = [(y - jp.Y_OUT, z) for y, z in jp.collar_profile()[:4]]
    lo, hi = (-hc, back, collar[0][1]), (hc, R + max(p for p, _ in collar), collar[-1][1])
    faces = []
    for (p0, z0), (p1, z1) in zip(collar, collar[1:]):
        faces += wall.hexa(B, tuple(pt(x, rho, z0) for x, rho in ((-hc, back), (hc, back), (hc, R + p0), (-hc, R + p0))),
                           tuple(pt(x, rho, z1) for x, rho in ((-hc, back), (hc, back), (hc, R + p1), (-hc, R + p1))))
    B.mapped(faces, lambda v, fc, th=th, lo=lo, hi=hi: rigid_trim_uv(v, fc, th, lo, hi))


def upper_collar(B, th_deg):
    """The carved collar that carries the upper ribs, on the wall above the string course at the angle th_deg (the pier
    has died at the string course): plan.upper_collar_profile swept as flat-fronted rings, 1.32 m wide like the collar
    of the lower ribs, with the same trim rule."""
    th = math.radians(th_deg)
    s, c = math.sin(th), math.cos(th)
    back = R - 0.6
    prof = plan.upper_collar_profile()

    def pt(x, rho, z):
        return (c * rho - s * x, CY + s * rho + c * x, z)

    hc = jp.COLLAR_HALF_W
    lo, hi = (-hc, back, prof[0][1]), (hc, R + max(p for p, _ in prof), prof[-1][1])
    faces = []
    for (p0, z0), (p1, z1) in zip(prof, prof[1:]):
        faces += wall.hexa(B, tuple(pt(x, rho, z0) for x, rho in ((-hc, back), (hc, back), (hc, R + p0), (-hc, R + p0))),
                           tuple(pt(x, rho, z1) for x, rho in ((-hc, back), (hc, back), (hc, R + p1), (-hc, R + p1))))
    B.mapped(faces, lambda v, fc, th=th, lo=lo, hi=hi: rigid_trim_uv(v, fc, th, lo, hi))


def arc_path(th_lo, th_hi, max_deg):
    """A path of the drum face (x, y, mx, my) from the angle th_lo to th_hi (radians) in equal steps of at most
    max_deg degrees: for a member that follows the arc (the carved band under the arch of a bay)."""
    k = max(1, math.ceil(math.degrees(th_hi - th_lo) / max_deg - 1e-9))
    ths = [th_lo + (th_hi - th_lo) * i / k for i in range(k + 1)]
    return [(R * math.cos(th), CY + R * math.sin(th), math.cos(th), math.sin(th)) for th in ths]


# ---- the parapet along the arc ------------------------------------------------------------------------
def arc_parapet(B, th0, th1, dz, offset):
    """A parapet part on the arc from th0 to th1 degrees, like the wall's parapet part (wall.build_parapet): a slab
    with pointed openings laid out by jp.parapet_openings, a frieze band under the cap on the outer face, and the
    cap. The slab is cut into columns of at most COLUMN_DEG (and at every opening corner), so the faces follow the
    arc. The ends are radial planes."""
    r_in = R + offset + (jp.PARAPET_Y0 - jp.Y_OUT)
    r_out = R + offset + (jp.PARAPET_Y1 - jp.Y_OUT)
    r_mid = 0.5 * (r_in + r_out)
    z0, z1 = jp.PARAPET_FOOT + dz, jp.PARAPET_BODY_TOP + dz
    t0, t1 = math.radians(th0), math.radians(th1)
    outline = wall.pointed_arch(wall.OPEN_W, wall.OPEN_H, 4)
    holes = []
    length = (t1 - t0) * r_mid
    for c in (x / length for x in jp.parapet_openings(length)):
        tc = t0 + (t1 - t0) * c
        pts = [(tc + u / r_mid, jp.Z_DECK + wall.OPEN_SILL + dz + v) for u, v in outline]
        top = {round(t, 9): z for t, z in pts[1:-1]}
        holes.append({"tl": pts[0][0], "tr": pts[-1][0], "sill": pts[0][1], "top": top, "pts": pts})
    cuts = {round(t0, 9), round(t1, 9)}
    for h in holes:
        cuts |= {round(t, 9) for t, _ in h["pts"]}
    if t0 < math.pi / 2 < t1:
        cuts.add(round(math.pi / 2, 9))  # U folds at the front centre, so no face may span it
    cuts = sorted(cuts)
    more = []
    for a, b in zip(cuts, cuts[1:]):
        k = math.ceil((b - a) / math.radians(COLUMN_DEG) - 1e-9)
        more += [a + (b - a) * i / k for i in range(1, k)]
    cuts = sorted(cuts + [round(t, 9) for t in more])
    cache = {}

    def V(layer, th, z):
        key = (layer, round(th, 9), round(z, 6))
        if key not in cache:
            r = r_in if layer == 0 else r_out
            cache[key] = B.vert((r * math.cos(th), CY + r * math.sin(th), z))
        return cache[key]

    def hole_in(th):
        return next((h for h in holes if h["tl"] + 1e-9 < th < h["tr"] - 1e-9), None)

    def edge_zs(th):
        for h in holes:
            if abs(th - h["tl"]) < 1e-9 or abs(th - h["tr"]) < 1e-9:
                return [h["sill"], h["top"][round(th, 9)]]
        return []

    stone, arch, reveal = [], [], []
    for ta, tb in zip(cuts, cuts[1:]):
        h = hole_in(0.5 * (ta + tb))
        for layer in (0, 1):
            if h:
                stone.append(B.face(V(layer, ta, z0), V(layer, tb, z0), V(layer, tb, h["sill"]), V(layer, ta, h["sill"])))
                ka, kb = round(ta, 9), round(tb, 9)
                stone.append(B.face(V(layer, ta, h["top"][ka]), V(layer, tb, h["top"][kb]), V(layer, tb, z1), V(layer, ta, z1)))
            else:
                right = [V(layer, tb, z) for z in sorted(edge_zs(tb))]
                left = [V(layer, ta, z) for z in sorted(edge_zs(ta), reverse=True)]
                stone.append(B.face(*([V(layer, ta, z0), V(layer, tb, z0)] + right + [V(layer, tb, z1), V(layer, ta, z1)] + left)))
        for z in (z0, z1):
            stone.append(B.face(V(0, ta, z), V(0, tb, z), V(1, tb, z), V(1, ta, z)))
        if h:
            sill = B.face(V(0, ta, h["sill"]), V(0, tb, h["sill"]), V(1, tb, h["sill"]), V(1, ta, h["sill"]))
            stone.append(sill)
            reveal.append(sill)
            ka, kb = round(ta, 9), round(tb, 9)
            corners = (V(0, ta, h["top"][ka]), V(0, tb, h["top"][kb]), V(1, tb, h["top"][kb]), V(1, ta, h["top"][ka]))
            arch.append((B.face(*corners), h, ta, tb, corners))
    for h in holes:
        for key in (round(h["tl"], 9), round(h["tr"], 9)):
            sp = h["top"][key]
            side = B.face(V(0, key, h["sill"]), V(0, key, sp), V(1, key, sp), V(1, key, h["sill"]))
            stone.append(side)
            reveal.append(side)
    for t in (t0, t1):
        stone.append(B.face(V(0, t, z0), V(1, t, z0), V(1, t, z1), V(0, t, z1)))
    faces = B.seal([f for f in stone if f is not None] + [a[0] for a in arch if a[0] is not None])
    reveal = [f for f in reveal + [a[0] for a in arch] if f is not None]
    B.terrain([f for f in faces if f not in set(reveal)], STONE)
    B.world(reveal, STONE)  # the box rule: a reveal face leans every way, the arc rule of the drum collapses on the steep ones
    # the reveal of each opening is the wall stone, as in the niche of the wall (version 6): the head, the sill and the
    # two jamb faces of every opening, darker for the weather
    B.recess(reveal)
    _frieze_and_cap(B, t0, t1, dz, offset, r_in, r_out, r_mid)


def _frieze_and_cap(B, t0, t1, dz, offset, r_in, r_out, r_mid):
    """The frieze band on the outer face and the cap, swept along the arc."""
    sec = jp.section()
    for name, strip, deg in (("frieze", "S5", FRIEZE_DEG), ("parapet_cap", "S2", CAP_DEG)):
        prof = [(R + offset + (y - jp.Y_OUT), z + dz) for y, z in sec[name]]
        k = max(1, math.ceil((t1 - t0) / math.radians(deg)))
        thetas = _with_centre([t0 + (t1 - t0) * i / k for i in range(k + 1)])
        lo = Loft(B, polar_sections(prof, thetas))
        lo.trim(strip, [r_mid * (t - t0) for t in thetas], prof)


def _with_centre(thetas):
    """The angles with the front centre (90 degrees) added when it falls inside them."""
    c = math.pi / 2
    if thetas[0] + 1e-9 < c < thetas[-1] - 1e-9 and all(abs(t - c) > 1e-9 for t in thetas):
        return sorted(thetas + [c])
    return thetas
