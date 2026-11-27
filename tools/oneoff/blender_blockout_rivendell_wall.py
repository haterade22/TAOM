"""Concept blockout of the Rivendell solid wall (picture w1), in real metres. Headless Blender:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_blockout_rivendell_wall.py --
        --out <dir> [--detail 1|2|3] [--pass N]

Frame (the Gondor control mesh): metres, the wall runs along X from -10 to +10, the outer face
(toward the attackers) is +Y, Z is up, Z = 0 is the ground line, the deck is at Z 12.0.

Detail 1 is the silhouette and proportions, 2 adds the secondary forms (plinth courses, tapered
piers, band, string course, parapet caps, rail rails), 3 adds the carved forms (ribs, blind
arches, niches, parapet openings, balusters). The build functions take a BMesh and return nothing,
so the breach piece can import this file and reuse them.

Writes <out>/wall_blockout.blend, <out>/rivendell_wall_blockout.fbx, <out>/blockout_report.json
(bounds and triangles per object, measurements) and <out>/concept_view_pass<N>.png. The Store
launcher prints nothing: wait for <out>/_report/DONE.txt (tools/blender/blender_job.py).
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector, geometry

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
from blender_job import export_fbx, flag_value, job, report_path, script_args  # noqa: E402

# ---- sizes (metres) -------------------------------------------------------------------------
WALL_X0, WALL_X1 = -10.0, 10.0
DECK_Z = 12.0
HALF_THICK = 2.5  # wall body Y -2.5 to +2.5 at the deck
FOUNDATION_Z0 = -8.0
FOUNDATION_FRONT_Y = 3.5  # plain block below ground, as deep as the piers stand out
PLINTH_Z1 = 3.0
PLINTH_BATTER = 0.6  # wider at the ground than at the top, +Y side
PLINTH_COURSES = 6

PIER_XS = (-20.0 / 3.0, 0.0, 20.0 / 3.0)  # one third of the 20 m module, so the rhythm crosses a join
PIER_W_BASE, PIER_W_TOP = 1.2, 1.0
PIER_PROUD_BASE, PIER_PROUD_TOP = 1.0, 0.5
PIER_TOP_Z = 8.5
PIER_BACK_Y = 2.3  # buried in the wall
NICHE_W, NICHE_Z0, NICHE_H, NICHE_DEPTH = 0.6, 0.9, 2.4, 0.2

TRUNK_Z1 = 10.5
TRUNK_W_BASE, TRUNK_W_TOP = 0.6, 0.4
TRUNK_PROUD_BASE, TRUNK_PROUD_TOP = 0.45, 0.3
RIB_W = 0.5  # in the plane of the wall
RIB_PROUD_START, RIB_PROUD_END = 0.5, 0.22
RIB_BACK_Y = 2.45
RIB_APEX_Z = 10.2  # centre line of the rib where it meets the next pier's rib
RIB_HALF_SPAN = 20.0 / 6.0  # pier to bay centre
RIB_END_DEG = 60.0
RIB_OVERLAP_DEG = 62.0  # inside a wall, the two ribs cross a little so the point is solid
RIB_STEPS = 16

BAND_Z0, BAND_Z1, BAND_PROUD = 3.4, 4.0, 0.10
ARCH_Z0, ARCH_W, ARCH_H = 4.0, 1.6, 4.5
ARCH_MOULD_W, ARCH_PROUD, ARCH_BACK_Y = 0.14, 0.12, 2.45
ARCH_PITCH = 1.8
BAY_ARCH_COUNT = 3
HALF_BAY_ARCH_CX = 8.6  # distance from the wall centre
ARCH_ARC_STEPS = 8

STRING_Z0, STRING_Z1, STRING_PROUD = 10.5, 10.9, 0.15

PARAPET_PARTS = ((-10, -6), (-6, -2), (-2, 2), (2, 6), (6, 10))
PARAPET_Z0 = 11.4  # laps over the wall top
PARAPET_H = 2.3  # above the deck
PARAPET_THICK = 0.5
PARAPET_CAP_Z0, PARAPET_CAP_OUT = 14.14, 0.08
OPEN_W, OPEN_SILL, OPEN_H = 0.5, 0.9, 1.2
OPEN_CENTRES = (1.0, 3.0)  # from the part start
PANEL_CX, PANEL_W, PANEL_Z0, PANEL_H, PANEL_MOULD, PANEL_PROUD = 2.0, 1.1, 12.6, 1.4, 0.08, 0.05

RAIL_H = 0.9
RAIL_THICK = 0.3
RAIL_BASE_Z0, RAIL_BASE_Z1 = 11.8, 12.25
RAIL_TOP_Z0 = DECK_Z + RAIL_H - 0.15
BALUSTER_W, BALUSTER_PITCH, BALUSTER_FIRST = 0.12, 0.5, -9.75

COLOURS = {"rivendell_stone": "c7b89e", "rivendell_trim": "ded6cb", "rivendell_rubble": "877a68"}
STONE, TRIM, RUBBLE = 0, 1, 2  # material slot of every object, in COLOURS order


# ---- small helpers --------------------------------------------------------------------------
def hex_to_linear(h):
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


def finish(bm, faces, mat):
    """Give a finished closed shell its material and outward normals."""
    faces = [f for f in faces if f is not None and f.is_valid]
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    for f in faces:
        f.material_index = mat


def _new_face(bm, vs):
    try:
        return bm.faces.new(vs)
    except ValueError:  # the same face twice: only possible for a degenerate loop
        return None


def box(bm, x0, x1, y0, y1, z0, z1, mat):
    v = [bm.verts.new(p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                                   (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    quads = ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7))
    finish(bm, [_new_face(bm, [v[i] for i in q]) for q in quads], mat)


def _tris(loops):
    """Triangles (index triples into the loops laid end to end) of a polygon with holes."""
    polys = [[Vector((u, v, 0.0)) for u, v in loop] for loop in loops]
    return geometry.tessellate_polygon(polys)


def prism(bm, loops, mapper, mat):
    """A closed solid: the polygon `loops` (outer first, then holes) as (u, v) points, mapped to 3D
    by mapper(u, v, k) with k 0 for the back layer and 1 for the front. Holes go right through."""
    layers = [[[bm.verts.new(mapper(u, v, k)) for u, v in loop] for loop in loops] for k in (0, 1)]
    flat = [[v for loop in layer for v in loop] for layer in layers]
    faces = []
    for tri in _tris(loops):
        for layer in flat:
            faces.append(_new_face(bm, [layer[i] for i in tri]))
    for loop_i, loop in enumerate(loops):
        n = len(loop)
        for i in range(n):
            j = (i + 1) % n
            faces.append(_new_face(bm, [layers[0][loop_i][i], layers[0][loop_i][j],
                                        layers[1][loop_i][j], layers[1][loop_i][i]]))
    finish(bm, faces, mat)


def pointed_arch(width, height, steps=ARCH_ARC_STEPS):
    """Outline of an equilateral pointed arch standing on v = 0, from the left foot over the apex
    to the right foot, as (u, v) points. The apex is `height` above the feet."""
    rise = width * math.sqrt(3.0) / 2.0
    spring = max(height - rise, 0.0)
    pts = [(-width / 2, 0.0), (-width / 2, spring)]
    for i in range(1, steps + 1):  # left arc: centre at the right spring point, 180 to 120 degrees
        a = math.radians(180.0 - 60.0 * i / steps)
        pts.append((width / 2 + width * math.cos(a), spring + width * math.sin(a)))
    for i in range(1, steps + 1):  # right arc: centre at the left spring point, 60 to 0 degrees
        a = math.radians(60.0 - 60.0 * i / steps)
        pts.append((-width / 2 + width * math.cos(a), spring + width * math.sin(a)))
    pts.append((width / 2, 0.0))
    return pts


def arch_ring(width, height, mould, steps=ARCH_ARC_STEPS):
    """A U-shaped moulding around a pointed arch, as one loop (outer then inner reversed)."""
    outer = pointed_arch(width, height, steps)
    w_in = width - 2 * mould
    rise_in = w_in * math.sqrt(3.0) / 2.0
    spring = height - width * math.sqrt(3.0) / 2.0
    inner = pointed_arch(w_in, spring + rise_in, steps)
    return outer + inner[::-1]


# ---- the main mesh ----------------------------------------------------------------------------
def plinth_profile(detail):
    """(y, z) points of the foundation and plinth on +Y, from the bottom front corner up to the wall
    face. Detail 1 is one sloped plane; from 2 on it is six coursed steps."""
    ground_y = HALF_THICK + PLINTH_BATTER
    pts = [(FOUNDATION_FRONT_Y, 0.0)]
    if detail < 2:
        pts += [(ground_y, 0.0), (HALF_THICK, PLINTH_Z1)]
        return pts
    h = PLINTH_Z1 / PLINTH_COURSES
    step = PLINTH_BATTER / (PLINTH_COURSES - 1)
    for i in range(PLINTH_COURSES):
        y = ground_y - step * i
        pts += [(y, h * i), (y, h * (i + 1))]
    return pts


def build_body(bm, detail):
    """Foundation, plinth and wall body as one closed prism, rubble below Z 3 and stone above."""
    low = [(-HALF_THICK, FOUNDATION_Z0), (FOUNDATION_FRONT_Y, FOUNDATION_Z0)] + plinth_profile(detail)
    low += [(-HALF_THICK, PLINTH_Z1)]

    def along_x(u, v, k):
        return Vector((WALL_X1 if k else WALL_X0, u, v))

    prism(bm, [low], along_x, RUBBLE)
    box(bm, WALL_X0, WALL_X1, -HALF_THICK, HALF_THICK, PLINTH_Z1, DECK_Z, STONE)
    # the two faces at Z 3 touch inside the solid: drop them and weld, so the body is one shell
    inner = [f for f in bm.faces if all(abs(v.co.z - PLINTH_Z1) < 1e-6 for v in f.verts)
             and abs(f.normal.z) > 0.99 and f.calc_area() > 50.0]
    bmesh.ops.delete(bm, geom=inner, context="FACES_ONLY")
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)


def pier_half_width(z):
    return (PIER_W_BASE + (PIER_W_TOP - PIER_W_BASE) * z / PIER_TOP_Z) / 2.0


def pier_front_y(z):
    return HALF_THICK + PIER_PROUD_BASE + (PIER_PROUD_TOP - PIER_PROUD_BASE) * z / PIER_TOP_Z


def build_pier(bm, xc, detail):
    """A tapering pier from the ground to Z 8.5. With detail 3 its front has a pointed blind niche."""
    if detail < 2:  # silhouette: a plain box at the mean depth
        mid = HALF_THICK + (PIER_PROUD_BASE + PIER_PROUD_TOP) / 2
        box(bm, xc - 0.55, xc + 0.55, PIER_BACK_Y, mid, 0.0, PIER_TOP_Z, TRIM)
        return
    hb, ht = pier_half_width(0.0), pier_half_width(PIER_TOP_Z)
    fb, ft = pier_front_y(0.0), pier_front_y(PIER_TOP_Z)
    v = {k: bm.verts.new(p) for k, p in {
        "bfl": (xc - hb, fb, 0.0), "bfr": (xc + hb, fb, 0.0),
        "bbl": (xc - hb, PIER_BACK_Y, 0.0), "bbr": (xc + hb, PIER_BACK_Y, 0.0),
        "tfl": (xc - ht, ft, PIER_TOP_Z), "tfr": (xc + ht, ft, PIER_TOP_Z),
        "tbl": (xc - ht, PIER_BACK_Y, PIER_TOP_Z), "tbr": (xc + ht, PIER_BACK_Y, PIER_TOP_Z)}.items()}
    faces = [_new_face(bm, [v[a] for a in q]) for q in (
        ("bbl", "bbr", "bfr", "bfl"), ("tfl", "tfr", "tbr", "tbl"), ("bbl", "bfl", "tfl", "tbl"),
        ("bfr", "bbr", "tbr", "tfr"), ("bbr", "bbl", "tbl", "tbr"))]
    corners = [v["bfl"], v["bfr"], v["tfr"], v["tfl"]]
    outer = [(c.co.x - xc, c.co.z) for c in corners]
    if detail < 3:
        faces.append(_new_face(bm, corners))
        finish(bm, faces, TRIM)
        return
    hole = [(u, NICHE_Z0 + z) for u, z in pointed_arch(NICHE_W, NICHE_H)]
    front = [bm.verts.new((xc + u, pier_front_y(z), z)) for u, z in hole]
    back = [bm.verts.new((xc + u, pier_front_y(z) - NICHE_DEPTH, z)) for u, z in hole]
    ring = corners + front
    for tri in _tris([outer, hole]):
        faces.append(_new_face(bm, [ring[i] for i in tri]))
    for i in range(len(hole)):
        j = (i + 1) % len(hole)
        faces.append(_new_face(bm, [front[i], front[j], back[j], back[i]]))
    for tri in _tris([hole]):
        faces.append(_new_face(bm, [back[i] for i in tri]))
    finish(bm, faces, TRIM)


def _rib_curve(sign, end_deg):
    """Centre line of a rib that leaves the pier top going straight up and bends toward the bay
    centre: a quarter ellipse reaching RIB_APEX_Z at 60 degrees. Returns (x offset, z, dx, dz)."""
    a = RIB_HALF_SPAN / (1.0 - math.cos(math.radians(RIB_END_DEG)))
    b = (RIB_APEX_Z - PIER_TOP_Z) / math.sin(math.radians(RIB_END_DEG))
    out = []
    for i in range(RIB_STEPS + 1):
        t = math.radians(end_deg) * i / RIB_STEPS
        out.append((sign * a * (1.0 - math.cos(t)), PIER_TOP_Z + b * math.sin(t),
                    sign * a * math.sin(t), b * math.cos(t)))
    return out


def build_rib(bm, xc, sign, end_deg, clip_x=None):
    """One rib: a swept rectangle (RIB_W in the wall plane) along the curve. `clip_x` cuts the end
    flat on a plane X = clip_x (a module end), so the next module meets it exactly."""
    pts = _rib_curve(sign, end_deg)
    secs = []
    for i, (dx, z, tx, tz) in enumerate(pts):
        tan = Vector((tx, 0.0, tz)).normalized()
        nrm = Vector((tan.z, 0.0, -tan.x))  # in the wall plane, perpendicular to the curve
        proud = RIB_PROUD_START + (RIB_PROUD_END - RIB_PROUD_START) * i / RIB_STEPS
        c = Vector((xc + dx, 0.0, z))
        sec = []
        for s in (-0.5, 0.5):
            for y in (RIB_BACK_Y, HALF_THICK + proud):
                sec.append(c + nrm * (s * RIB_W) + Vector((0.0, y, 0.0)))
        if clip_x is not None and i == RIB_STEPS:
            sec = [p + tan * ((clip_x - p.x) / tan.x) for p in sec]
        secs.append([bm.verts.new(p) for p in sec])
    faces = []
    for a, b in zip(secs, secs[1:]):
        for i, j in ((0, 1), (1, 3), (3, 2), (2, 0)):
            faces.append(_new_face(bm, [a[i], a[j], b[j], b[i]]))
    faces.append(_new_face(bm, [secs[0][0], secs[0][1], secs[0][3], secs[0][2]]))
    faces.append(_new_face(bm, [secs[-1][0], secs[-1][1], secs[-1][3], secs[-1][2]]))
    finish(bm, faces, TRIM)


def build_trunk(bm, xc):
    """The short trunk from the pier top to the string course."""
    z0, z1 = PIER_TOP_Z - 0.1, TRUNK_Z1
    wb, wt = TRUNK_W_BASE / 2, TRUNK_W_TOP / 2
    fb, ft = HALF_THICK + TRUNK_PROUD_BASE, HALF_THICK + TRUNK_PROUD_TOP
    v = [bm.verts.new(p) for p in (
        (xc - wb, RIB_BACK_Y, z0), (xc + wb, RIB_BACK_Y, z0), (xc + wb, fb, z0), (xc - wb, fb, z0),
        (xc - wt, RIB_BACK_Y, z1), (xc + wt, RIB_BACK_Y, z1), (xc + wt, ft, z1), (xc - wt, ft, z1))]
    quads = ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7))
    finish(bm, [_new_face(bm, [v[i] for i in q]) for q in quads], TRIM)


def arch_centres():
    """X of every blind arch: three in each full bay, one in each half bay at the ends."""
    xs = [-HALF_BAY_ARCH_CX, HALF_BAY_ARCH_CX]
    for a, b in zip(PIER_XS, PIER_XS[1:]):
        mid = (a + b) / 2
        xs += [mid + ARCH_PITCH * (i - (BAY_ARCH_COUNT - 1) / 2) for i in range(BAY_ARCH_COUNT)]
    return sorted(xs)


def build_blind_arch(bm, cx):
    ring = arch_ring(ARCH_W, ARCH_H, ARCH_MOULD_W)

    def mapper(u, v, k):
        return Vector((cx + u, ARCH_BACK_Y if k == 0 else HALF_THICK + ARCH_PROUD, ARCH_Z0 + v))

    prism(bm, [ring], mapper, TRIM)


def build_main(bm, detail):
    build_body(bm, detail)
    for xc in PIER_XS:
        build_pier(bm, xc, detail)
    if detail < 2:
        return
    box(bm, WALL_X0, WALL_X1, RIB_BACK_Y, HALF_THICK + BAND_PROUD, BAND_Z0, BAND_Z1, TRIM)
    box(bm, WALL_X0, WALL_X1, RIB_BACK_Y, HALF_THICK + STRING_PROUD, STRING_Z0, STRING_Z1, TRIM)
    for xc in PIER_XS:
        build_trunk(bm, xc)
    if detail < 3:
        return
    for xc in PIER_XS:
        for sign in (-1, 1):
            edge = (sign < 0 and xc == PIER_XS[0]) or (sign > 0 and xc == PIER_XS[-1])
            if edge:
                build_rib(bm, xc, sign, RIB_END_DEG, clip_x=WALL_X0 if sign < 0 else WALL_X1)
            else:
                build_rib(bm, xc, sign, RIB_OVERLAP_DEG)
    for cx in arch_centres():
        build_blind_arch(bm, cx)


# ---- the parapet parts and the inner rail ------------------------------------------------------
def build_parapet(bm, x0, x1, detail):
    """One 4 m parapet part on the +Y edge. Detail 3 pierces it with two pointed openings and raises
    a lancet moulding on the middle panel."""
    y0, y1 = HALF_THICK - PARAPET_THICK, HALF_THICK
    top = DECK_Z + PARAPET_H
    if detail < 2:
        box(bm, x0, x1, y0, y1, PARAPET_Z0, top, TRIM)
        return
    body_top = PARAPET_CAP_Z0 if detail >= 2 else top
    outer = [(x0, PARAPET_Z0), (x1, PARAPET_Z0), (x1, body_top), (x0, body_top)]
    holes = []
    if detail >= 3:
        for c in OPEN_CENTRES:
            arch = pointed_arch(OPEN_W, OPEN_H)
            holes.append([(x0 + c + u, DECK_Z + OPEN_SILL + v) for u, v in arch][::-1])

    def through(u, v, k):
        return Vector((u, y0 if k == 0 else y1, v))

    prism(bm, [outer] + holes, through, TRIM)
    box(bm, x0, x1, y0 - PARAPET_CAP_OUT, y1 + PARAPET_CAP_OUT, PARAPET_CAP_Z0, top, TRIM)
    if detail >= 3:
        ring = arch_ring(PANEL_W, PANEL_H, PANEL_MOULD, steps=6)

        def relief(u, v, k):
            return Vector((x0 + PANEL_CX + u, y1 - 0.05 if k == 0 else y1 + PANEL_PROUD, PANEL_Z0 + v))

        prism(bm, [ring], relief, TRIM)


def build_rail(bm, detail):
    """The inner rail on the -Y edge: 0.9 m above the deck, one part for the whole 20 m."""
    y0, y1 = -HALF_THICK, -HALF_THICK + RAIL_THICK
    top = DECK_Z + RAIL_H
    if detail < 2:
        box(bm, WALL_X0, WALL_X1, y0, y1, RAIL_BASE_Z0, top, TRIM)
        return
    box(bm, WALL_X0, WALL_X1, y0, y1, RAIL_BASE_Z0, RAIL_BASE_Z1, TRIM)
    box(bm, WALL_X0, WALL_X1, y0, y1, RAIL_TOP_Z0, top, TRIM)
    if detail < 3:
        box(bm, WALL_X0, WALL_X1, y0 + 0.08, y1 - 0.08, RAIL_BASE_Z1, RAIL_TOP_Z0, TRIM)
        return
    n = int(round((WALL_X1 - WALL_X0) / BALUSTER_PITCH))
    cy, h = (y0 + y1) / 2, BALUSTER_W / 2
    for i in range(n):
        cx = BALUSTER_FIRST + BALUSTER_PITCH * i
        box(bm, cx - h, cx + h, cy - h, cy + h, RAIL_BASE_Z1 - 0.05, RAIL_TOP_Z0 + 0.05, TRIM)


# ---- scene, export, measurements ----------------------------------------------------------------
def make_materials():
    mats = []
    for name, hx in COLOURS.items():
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = hex_to_linear(hx)
        mats.append(m)
    return mats


def object_from_bmesh(name, bm, mats):
    me = bpy.data.meshes.new(name)
    used = sorted({f.material_index for f in bm.faces})  # a slot nothing uses is an empty sub-mesh
    for f in bm.faces:
        f.material_index = used.index(f.material_index)
    bm.to_mesh(me)
    for i in used:
        me.materials.append(mats[i])
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def build_all(detail):
    mats = make_materials()
    objs = []

    def make(name, fn):
        bm = bmesh.new()
        fn(bm)
        objs.append(object_from_bmesh(name, bm, mats))
        bm.free()

    make("rivendell_wall_blockout", lambda bm: build_main(bm, detail))
    for i, (x0, x1) in enumerate(PARAPET_PARTS, 1):
        make(f"rivendell_wall_blockout_m{i}", lambda bm, a=x0, b=x1: build_parapet(bm, a, b, detail))
    make("rivendell_wall_blockout_m6", lambda bm: build_rail(bm, detail))
    return objs


def mesh_stats(ob):
    me = ob.data
    me.calc_loop_triangles()
    xs = [v.co for v in me.vertices]
    lo = [round(min(p[k] for p in xs), 4) for k in range(3)]
    hi = [round(max(p[k] for p in xs), 4) for k in range(3)]
    bm = bmesh.new()
    bm.from_mesh(me)
    open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    bm.free()
    return {"min": lo, "max": hi, "tris": len(me.loop_triangles), "verts": len(me.vertices),
            "faces": len(me.polygons), "edges_not_two_faces": open_edges,
            "materials": [m.name for m in me.materials]}


def measure(objs):
    """Deck height and clear walk, read from the built meshes."""
    by = {o.name: o for o in objs}
    main = by["rivendell_wall_blockout"].data
    deck = [sum(v.co.z for v in (main.vertices[i] for i in p.vertices)) / len(p.vertices)
            for p in main.polygons if p.normal.z > 0.99 and p.area > 50.0
            and 11.5 < p.center.z < 12.5]
    par_y = [v.co.y for n, o in by.items() if n[-2:] in ("m1", "m2", "m3", "m4", "m5")
             for v in o.data.vertices if DECK_Z + 0.05 < v.co.z < PARAPET_CAP_Z0 - 0.05]
    par_cap_y = [v.co.y for n, o in by.items() if n[-2:] in ("m1", "m2", "m3", "m4", "m5")
                 for v in o.data.vertices if v.co.z > DECK_Z + 0.05]
    par_y = par_y or par_cap_y  # detail 1 has no vertex between the foot and the top
    rail_y = [v.co.y for v in by["rivendell_wall_blockout_m6"].data.vertices if v.co.z > DECK_Z + 0.05]
    return {"deck_z": sorted(set(round(z, 4) for z in deck)),
            "parapet_inner_y_below_cap": round(min(par_y), 4),
            "parapet_inner_y_at_cap": round(min(par_cap_y), 4),
            "rail_inner_y": round(max(rail_y), 4),
            "clear_walk_to_parapet_body": round(min(par_y) - max(rail_y), 4),
            "clear_walk_to_cap": round(min(par_cap_y) - max(rail_y), 4)}


def render_concept(path, objs):
    """Three-quarter view from the outside at eye level, Workbench, material colours, with a 1.8 m
    thin box at the wall foot for scale. The preview objects are added after the .blend is saved."""
    scene = bpy.context.scene
    scale = bpy.data.materials.new("scale_ref")
    scale.diffuse_color = (0.8, 0.1, 0.1, 1.0)
    ground = bpy.data.materials.new("preview_ground")
    ground.diffuse_color = hex_to_linear("c9cacb")
    for name, dims, loc, mat in (("scale_1p8m", (0.5, 0.3, 1.8), (3.33, 4.6, 0.9), scale),
                                 ("preview_ground", (120.0, 120.0, 0.02), (0.0, 0.0, -0.011), ground)):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=dims, verts=bm.verts)
        bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
        ob = object_from_bmesh(name, bm, [mat])
        bm.free()
    cam_data = bpy.data.cameras.new("concept_cam")
    cam_data.lens = 34.0
    cam_data.clip_end = 500.0
    cam = bpy.data.objects.new("concept_cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (21.0, 31.0, 1.8)
    target = Vector((-1.0, 0.0, 7.0))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 1376, 768
    scene.render.filepath = path
    scene.render.image_settings.file_format = "PNG"
    sh = scene.display.shading
    sh.light, sh.color_type, sh.show_cavity = "STUDIO", "MATERIAL", False
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.62, 0.64, 0.66)
    bpy.ops.render.render(write_still=True)


def main():
    args = script_args()
    out = flag_value(args, "--out")
    detail = int(flag_value(args, "--detail") or 3)
    pass_n = flag_value(args, "--pass") or str(detail)
    with job(report_path(out)) as result:
        os.makedirs(out, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs = build_all(detail)
        bpy.context.view_layer.update()
        result["detail"] = detail
        result["objects"] = {o.name: mesh_stats(o) for o in objs}
        result["total_tris"] = sum(v["tris"] for v in result["objects"].values())
        result["measured"] = measure(objs)
        fbx = os.path.join(out, "rivendell_wall_blockout.fbx")
        export_fbx(fbx, objs)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "wall_blockout.blend"))
        result["fbx"] = fbx
        with open(os.path.join(out, "blockout_report.json"), "w", encoding="utf-8") as f:
            json.dump({k: result[k] for k in ("detail", "objects", "total_tris", "measured")}, f, indent=1)
        render_concept(os.path.join(out, f"concept_view_pass{pass_n}.png"), objs)


if __name__ == "__main__":
    main()
