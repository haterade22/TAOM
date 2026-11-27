"""Concept blockout of the Rivendell gatehouse (pictures g2 and g1), in real metres. Headless Blender:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_blockout_rivendell_gatehouse.py --
        --variant g2|g1 --out <dir> [--detail 1|2|3] [--pass N]

Frame: metres, origin at the centre of the passage floor, +Y outside (toward the attackers), X across
the passage, Z up, Z = 0 is the ground and the passage floor, the deck is at Z 12.0 (level with the wall
walk of blender_blockout_rivendell_wall.py, whose builder functions this file reuses).

The shared core (body, hall, two door tunnels, rock hole, deck items, placeholder gates, checks) is in
functions; a variant adds its corner structures. g2 has round drums with a domed pavilion each; g1 has
two spired turrets (kit part SM_Spire_Cut), a pointed arch on the outer face (SM_Decorative_Arch) and
an open gable roof on six columns over the rock hole.

Detail 1 is the silhouette and proportions, 2 adds the secondary forms (plinth courses, cornices,
balustrade, hall ribs, inner piers), 3 adds the carved forms (drum trunks, ogee moulding, tympanum,
niches, blind arches, parapet openings).

Writes <out>/gatehouse_<variant>_blockout.blend, <out>/rivendell_gatehouse_<variant>_blockout.fbx (without the
placeholder gates), <out>/blockout_report.json and two Workbench pictures. The Store launcher prints
nothing: wait for <out>/_report/DONE.txt (tools/blender/blender_job.py).
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
import blender_blockout_rivendell_wall as wall  # noqa: E402
from blender_blockout_rivendell_wall import (RUBBLE, STONE, TRIM, box, hex_to_linear,  # noqa: E402
                                             mesh_stats, object_from_bmesh, prism, finish, _new_face)
from blender_job import export_fbx, flag_value, import_fbx, job, report_path, script_args  # noqa: E402

# ---- sizes (metres) -------------------------------------------------------------------------
DECK_Z = 12.0
HALF_W, HALF_D = 10.0, 6.2  # 20.0 wide, 12.4 deep
FOUNDATION_Z0 = -6.0
PLINTH_Z1, PLINTH_BATTER, PLINTH_COURSES = 3.0, 0.6, 6
HALL_HALF_W, HALL_Y, HALL_CEIL = 4.2, 4.6, 9.6  # hall: X -4.2..4.2, Y -4.6..4.6
DOOR_HALF_W, DOOR_SPRING, DOOR_RISE = 2.35, 7.0, 3.0  # opening 4.7 wide, 7.0 clear under the spring
DOOR_Y0 = HALL_Y  # door frames are Y 4.6..6.2 and -6.2..-4.6
OGEE_MOULD, OGEE_RISE = 0.55, 3.9
HOLE_X, HOLE_Y0, HOLE_Y1 = 2.9, -2.6, -1.0
CURB_H, CURB_T = 0.45, 0.3
RIB_YS = (-3.6, 0.0, 3.6)  # hall vault ribs, clear of the hole (Y -2.6..-1.0)
RIB_SPRING, RIB_RISE, RIB_DROP, RIB_T = 6.0, 3.7, 0.4, 0.4

OUTER_GATE_Y, INNER_GATE_Y = 4.2, -4.2

DRUM_R, DRUM_CX, DRUM_CY = 3.3, 6.7, 2.9
FACADE_Y = 5.4  # outer face between the drums
PORTAL_HALF_W = 3.0  # the door frame stands proud to Y 6.2
PAV_SCALE = 0.875  # 7.47 m x 0.875 = 6.54 m: the base disc stays 3 cm inside the drum
PAV_LIFT = 0.02  # floor above the deck, so it does not fight the deck face
STATUE_SCALE, PEDESTAL_H, PEDESTAL_W = 1.4, 1.2, 1.2
STATUE_X, STATUE_Y = 3.75, 5.6
STATUE_YAW_DEG = 180.0  # the kit statue faces -Y at yaw 0 (pass-1 close-up); the guardians face out

COLOURS = {"rivendell_stone": "c7b89e", "rivendell_trim": "ded6cb", "rivendell_rubble": "877a68",
           "rivendell_slate": "5d6670", "rivendell_wood": "6b4e32", "rivendell_gate": "9aa8b0"}
SLATE, WOOD, GATE = 3, 4, 5
KIT_DIR = "E:/LOTRAOMAssets/_export/rivendell/meshes/Outdoor_Assets/"


# ---- 2D outlines ----------------------------------------------------------------------------
def lancet(half_w, rise, steps=10):
    """Two-centre pointed arch standing on v = 0: left foot, apex, right foot, as (u, v)."""
    r = (half_w ** 2 + rise ** 2) / (2 * half_w)
    phi1 = math.acos((half_w - r) / r)
    left = []
    for i in range(steps + 1):
        p = math.pi + (phi1 - math.pi) * i / steps
        left.append((-half_w + r + r * math.cos(p), r * math.sin(p)))
    right = [(-x, y) for x, y in reversed(left[:-1])]
    return left + right


def opening_loop(half_w, spring, rise, z_bottom):
    pts = [(u, spring + v) for u, v in lancet(half_w, rise)]
    return [(-half_w, z_bottom)] + pts + [(half_w, z_bottom)]


def ogee_half(w, zs, h, steps=14):
    """Left half of an ogee outline from the jamb (-w, zs) to the tip (0, zs + h): convex, then concave."""
    p = [(-w, zs), (-w, zs + 0.80 * h), (-0.31 * w, zs + 0.62 * h), (0.0, zs + h)]  # stays 0.4 m outside the door arch
    out = []
    for i in range(steps + 1):
        t = i / steps
        a, b, c, d = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t ** 2, t ** 3
        out.append((a * p[0][0] + b * p[1][0] + c * p[2][0] + d * p[3][0],
                    a * p[0][1] + b * p[1][1] + c * p[2][1] + d * p[3][1]))
    return out


def ogee_ring(a, spring, rise, mould, tip_h):
    w = a + mould
    half = ogee_half(w, spring, tip_h)
    outer = [(-w, 0.0)] + half + [(-x, y) for x, y in reversed(half[:-1])] + [(w, 0.0)]
    inner = opening_loop(a, spring, rise, 0.0)
    return outer + inner[::-1]


def arc_points(cx, cy, r, th0, th1, mirror, n=None):
    n = n or max(4, int(abs(th1 - th0) / math.radians(6)))
    pts = []
    for i in range(n + 1):
        th = th0 + (th1 - th0) * i / n
        pts.append((cx + (-1 if mirror else 1) * r * math.cos(th), cy + r * math.sin(th)))
    return pts


THETA_FRONT = math.pi - math.asin((FACADE_Y - DRUM_CY) / DRUM_R)  # where the drum meets the facade plane


def body_outline():
    """Footprint of the stone body: the 20 x 12.4 rectangle with the drum fronts and the door frame."""
    pts = [(-HALF_W, -HALF_D), (HALF_W, -HALF_D)]
    pts += arc_points(DRUM_CX, DRUM_CY, DRUM_R, 0.0, THETA_FRONT, False, 24)
    pts += [(PORTAL_HALF_W, FACADE_Y), (PORTAL_HALF_W, HALF_D), (-PORTAL_HALF_W, HALF_D),
            (-PORTAL_HALF_W, FACADE_Y)]
    pts += [(-x, y) for x, y in arc_points(DRUM_CX, DRUM_CY, DRUM_R, 0.0, THETA_FRONT, False, 24)][::-1]
    return pts


# ---- bmesh helpers --------------------------------------------------------------------------
def merge_into(bm, tmp, matrix):
    """Copy the faces of bmesh `tmp` into `bm`, moved by `matrix`."""
    vmap = {v: bm.verts.new(matrix @ v.co) for v in tmp.verts}
    for f in tmp.faces:
        nf = _new_face(bm, [vmap[v] for v in f.verts])
        if nf is not None:
            nf.material_index = f.material_index


def sphere(bm, centre, r, mat):
    before = set(bm.faces)
    ret = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
    bmesh.ops.translate(bm, vec=centre, verts=ret["verts"])
    for f in bm.faces:
        if f not in before:
            f.material_index = mat


def along_y(y0, y1):
    return lambda u, v, k: Vector((u, y1 if k else y0, v))


def along_x(x0, x1):
    return lambda u, v, k: Vector((x1 if k else x0, u, v))


def vertical(z0, z1):
    return lambda u, v, k: Vector((u, v, z1 if k else z0))


def cutter_object(name, build):
    bm = bmesh.new()
    build(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def cut_body(bm_body, cutters):
    """Boolean-subtract the cutter objects from the body bmesh; return the result as a bmesh."""
    me = bpy.data.meshes.new("body_tmp")
    bm_body.to_mesh(me)
    for m in make_materials():
        me.materials.append(m)
    body = bpy.data.objects.new("body_tmp", me)
    bpy.context.scene.collection.objects.link(body)
    for c in cutters:
        mod = body.modifiers.new(c.name, "BOOLEAN")
        mod.operation, mod.solver, mod.object, mod.use_self = "DIFFERENCE", "EXACT", c, True
    bpy.context.view_layer.update()
    ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    out = bpy.data.meshes.new_from_object(ev)
    bm = bmesh.new()
    bm.from_mesh(out)
    for c in cutters:
        bpy.data.objects.remove(c)
    bpy.data.objects.remove(body)
    return bm


# ---- core: the body and the cuts ----------------------------------------------------------------
def build_core_body(detail, outline=None, niches=True):
    """Stone body minus hall, two door tunnels, the rock hole and (g2, detail 3) two niches."""
    bm = bmesh.new()
    prism(bm, [outline or body_outline()], vertical(0.0, DECK_Z), STONE)
    cutters = []

    def hall(b):
        box(b, -HALL_HALF_W, HALL_HALF_W, -HALL_Y, HALL_Y, -0.5, HALL_CEIL, STONE)

    def door_out(b):
        prism(b, [opening_loop(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, -0.5)], along_y(HALL_Y - 0.5, HALF_D + 1.0),
              STONE)

    def door_in(b):
        prism(b, [opening_loop(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, -0.5)], along_y(-HALF_D - 1.0, -HALL_Y + 0.5),
              STONE)

    def shaft(b):
        box(b, -HOLE_X, HOLE_X, HOLE_Y0, HOLE_Y1, HALL_CEIL - 0.6, DECK_Z + 1.0, STONE)

    for nm, fn in (("c_hall", hall), ("c_door_out", door_out), ("c_door_in", door_in), ("c_shaft", shaft)):
        cutters.append(cutter_object(nm, fn))
    if detail >= 3 and niches:
        for s in (-1, 1):
            def niche(b, s=s):
                loop = opening_loop(0.75, 3.9, 1.8, 1.25)
                prism(b, [loop], lambda u, v, k: Vector((s * STATUE_X + u, FACADE_Y - 0.5 if k == 0 else FACADE_Y + 0.5,
                                                       v)), STONE)
            cutters.append(cutter_object(f"c_niche{s}", niche))
    return cut_body(bm, cutters)


def plinth_courses(detail):
    """(z0, z1, proud) of each plinth course, wider at the ground."""
    if detail < 2:
        return [(0.0, PLINTH_Z1, PLINTH_BATTER / 2)]
    h = PLINTH_Z1 / PLINTH_COURSES
    step = PLINTH_BATTER / (PLINTH_COURSES - 1)
    return [(h * i, h * (i + 1), PLINTH_BATTER - step * i) for i in range(PLINTH_COURSES - 1)]


def build_foundation_and_plinth(bm, detail):
    prism(bm, [body_outline()], vertical(FOUNDATION_Z0, 0.0), RUBBLE)
    for z0, z1, p in plinth_courses(detail):
        for s in (-1, 1):
            xa, xb = sorted((s * DOOR_HALF_W, s * PORTAL_HALF_W))
            box(bm, xa, xb, HALF_D, HALF_D + p, z0, z1, RUBBLE)  # in front of the door frame
            xa, xb = sorted((s * PORTAL_HALF_W, s * 4.6))
            box(bm, xa, xb, FACADE_Y, FACADE_Y + p, z0, z1, RUBBLE)  # between the frame and the drum
            xa, xb = sorted((s * DOOR_HALF_W, s * HALF_W))
            box(bm, xa, xb, -HALF_D - p, -HALF_D, z0, z1, RUBBLE)  # inner face
        pts = [(DRUM_CX, DRUM_CY)] + arc_points(DRUM_CX, DRUM_CY, DRUM_R + p, 0.0, THETA_FRONT, False, 24)
        prism(bm, [pts], vertical(z0, z1), RUBBLE)
        prism(bm, [[(-x, y) for x, y in pts][::-1]], vertical(z0, z1), RUBBLE)


def hall_ribs(bm):
    outer = [(u, RIB_SPRING + v) for u, v in lancet(HALL_HALF_W + 0.15, RIB_RISE)]
    inner = [(u, RIB_SPRING - RIB_DROP + v) for u, v in lancet(HALL_HALF_W + 0.15, RIB_RISE)]
    for y in RIB_YS:
        prism(bm, [outer + inner[::-1]], along_y(y - RIB_T / 2, y + RIB_T / 2), TRIM)


def hall_blind_arches(bm):
    ring = wall.arch_ring(1.4, 4.2, 0.12)
    for s in (-1, 1):
        for yc in (-1.8, 1.8):
            prism(bm, [ring], lambda u, v, k, s=s, yc=yc: Vector((s * (HALL_HALF_W + 0.1 if k == 0 else HALL_HALF_W - 0.1),
                                                                  yc + u, 0.6 + v)), TRIM)


def inner_piers(bm, detail):
    """Two of the wall's piers on the inner face (Y -6.2), turned about Z: the wall's own rhythm."""
    for xc in (-wall.PIER_XS[2], wall.PIER_XS[2]):
        tmp = bmesh.new()
        wall.build_pier(tmp, xc, detail)
        if detail >= 2:
            wall.build_trunk(tmp, xc)
        m = Matrix.Translation((0, wall.HALF_THICK - HALF_D, 0)) @ Matrix.Rotation(math.pi, 4, "Z")
        merge_into(bm, tmp, m)
        tmp.free()


# ---- the hole, the curb, the piles ------------------------------------------------------------
def deck_items(bm, detail):
    for ya, yb in ((HOLE_Y0 - CURB_T, HOLE_Y0), (HOLE_Y1, HOLE_Y1 + CURB_T)):
        box(bm, -HOLE_X, HOLE_X, ya, yb, DECK_Z, DECK_Z + CURB_H, TRIM)  # long sides only
    if detail >= 3:  # carved caps on the curb
        for ya, yb in ((HOLE_Y0 - CURB_T - 0.05, HOLE_Y0), (HOLE_Y1, HOLE_Y1 + CURB_T + 0.05)):  # no lip over the hole
            box(bm, -HOLE_X - 0.05, HOLE_X + 0.05, ya, yb, DECK_Z + CURB_H, DECK_Z + CURB_H + 0.08, TRIM)
    for cx in (-4.2, 4.2):  # two low piles of round stones, each within 4 m of the hole
        cy = -0.2
        for dx, dy in ((-0.3, -0.2), (0.3, -0.2), (0.0, 0.3), (-0.3, 0.35), (0.3, 0.35)):
            sphere(bm, (cx + dx, cy + dy, DECK_Z + 0.17), 0.17, STONE)
        for dx, dy in ((0.0, 0.05), (-0.15, 0.3), (0.15, 0.3)):
            sphere(bm, (cx + dx, cy + dy, DECK_Z + 0.46), 0.15, STONE)
        sphere(bm, (cx, cy + 0.1, DECK_Z + 0.72), 0.13, STONE)


# ---- g2: drums, trunks, cornices, balustrade, ogee, statues, pavilions ----------------------------
def drum_rib(bm, side, pts, radius=DRUM_R):
    """A swept strip on the drum surface. pts: (theta, z, half width, proud). side -1 mirrors X."""
    secs = []
    for i, (th, z, hw, pr) in enumerate(pts):
        j = min(i + 1, len(pts) - 1)
        k = max(i - 1, 0)
        ds = radius * (pts[j][0] - pts[k][0])
        dz = pts[j][1] - pts[k][1]
        n = math.hypot(ds, dz) or 1.0
        ns, nz = dz / n, -ds / n
        sec = []
        for sd in (-1.0, 1.0):
            for rho in (radius - 0.2, radius + pr):
                t2 = th + sd * hw * ns / radius
                zz = z + sd * hw * nz
                sec.append(bm.verts.new((DRUM_CX * side + side * rho * math.cos(t2), DRUM_CY + rho * math.sin(t2), zz)))
        secs.append(sec)
    faces = []
    for a, b in zip(secs, secs[1:]):
        for p, q in ((0, 1), (1, 3), (3, 2), (2, 0)):
            faces.append(_new_face(bm, [a[p], a[q], b[q], b[p]]))
    for s in (secs[0], secs[-1]):
        faces.append(_new_face(bm, [s[0], s[1], s[3], s[2]]))
    finish(bm, faces, TRIM)


def drum_trunks(bm, side):
    for deg in (20.0, 60.0, 100.0):
        th0 = math.radians(deg)
        trunk = [(th0, 2.9 + (8.0 - 2.9) * i / 6, 0.45 - 0.17 * i / 6, 0.40 - 0.05 * i / 6) for i in range(7)]
        drum_rib(bm, side, trunk)
        for sg in (-1, 1):
            br = []
            for i in range(13):
                phi = math.radians(60.0 * i / 12)
                dth = math.radians(20.0) * (1 - math.cos(phi)) / (1 - math.cos(math.radians(60.0)))
                z = 8.0 + (10.8 - 8.0) / math.sin(math.radians(60.0)) * math.sin(phi)
                br.append((th0 + sg * dth, z, 0.28 - 0.12 * i / 12, 0.35 - 0.2 * i / 12))
            drum_rib(bm, side, br)


def drum_cornice(bm, side):
    outer = arc_points(0.0, DRUM_CY, DRUM_R + 0.2, 0.0, THETA_FRONT, False, 24)
    inner = arc_points(0.0, DRUM_CY, DRUM_R - 0.1, 0.0, THETA_FRONT, False, 24)
    loop = [(side * (DRUM_CX + x), y) for x, y in outer + inner[::-1]]
    prism(bm, [loop], vertical(11.2, 11.9), TRIM)


def drum_balustrade(bm, side, pav):
    """A low ring on the drum top (the full 3.3 m radius), left open on the side toward the deck and
    wherever the pavilion body is. pav = (cx, cy, r) of the pavilion floor in the frame."""
    toward_deck = math.atan2(-1.0, -1.0 * side)  # diagonal toward the hole and the deck
    keep = []
    for i in range(0, 360, 3):
        th = math.radians(i - 180)
        x, y = side * DRUM_CX + DRUM_R * 0.88 * math.cos(th), DRUM_CY + DRUM_R * 0.88 * math.sin(th)
        d = math.degrees(abs((th - toward_deck + math.pi) % (2 * math.pi) - math.pi))
        free = math.hypot(x - pav[0], y - pav[1]) > pav[2] + 0.25
        keep.append(d > 60.0 and free and y > -0.2)
    runs, cur = [], []
    for i, k in enumerate(keep):
        if k:
            cur.append(i)
        elif cur:
            runs.append(cur)
            cur = []
    if cur:
        runs.append(cur)
    for run in runs:
        if len(run) < 2:
            continue
        a, b = math.radians(run[0] * 3 - 180), math.radians(run[-1] * 3 - 180)
        for r0, r1, z0, z1 in ((DRUM_R - 0.55, DRUM_R - 0.1, DECK_Z, DECK_Z + 0.25),
                               (DRUM_R - 0.45, DRUM_R - 0.2, DECK_Z + 0.25, DECK_Z + 0.85),
                               (DRUM_R - 0.55, DRUM_R - 0.1, DECK_Z + 0.85, DECK_Z + 1.05)):
            n = max(2, len(run))
            o = [(side * DRUM_CX + side * r1 * math.cos(a + (b - a) * i / n), DRUM_CY + r1 * math.sin(a + (b - a) * i / n))
                 for i in range(n + 1)]
            ii = [(side * DRUM_CX + side * r0 * math.cos(a + (b - a) * i / n), DRUM_CY + r0 * math.sin(a + (b - a) * i / n))
                  for i in range(n + 1)]
            prism(bm, [o + ii[::-1]], vertical(z0, z1), TRIM)


def facade_trim(bm, detail):
    """Cornice strips on the facade and the door frame, and at detail 3 the ogee moulding and tympanum."""
    for s in (-1, 1):
        xa, xb = sorted((s * PORTAL_HALF_W, s * 4.55))
        box(bm, xa, xb, FACADE_Y, FACADE_Y + 0.2, 11.2, 11.9, TRIM)
        xa, xb = sorted((s * PORTAL_HALF_W, s * (PORTAL_HALF_W + 0.2)))
        box(bm, xa, xb, FACADE_Y, HALF_D + 0.2, 11.2, 11.9, TRIM)
    box(bm, -PORTAL_HALF_W - 0.2, PORTAL_HALF_W + 0.2, HALF_D, HALF_D + 0.2, 11.55, 11.9, TRIM)
    if detail < 3:
        return
    ring = ogee_ring(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, OGEE_MOULD, OGEE_RISE)
    prism(bm, [ring], along_y(HALF_D - 0.15, HALF_D + 0.25), TRIM)
    tip = DOOR_SPRING + OGEE_RISE
    box(bm, -1.6, 1.6, HALF_D - 0.1, HALF_D + 0.08, tip + 0.10, tip + 0.50, TRIM)  # tympanum frame
    box(bm, -1.4, 1.4, HALF_D - 0.1, HALF_D + 0.14, tip + 0.18, tip + 0.42, TRIM)  # carved field


def inner_door_trim(bm):
    ring = ogee_ring(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, OGEE_MOULD, OGEE_RISE)
    prism(bm, [ring], along_y(-HALF_D - 0.2, -HALF_D + 0.1), TRIM)


def kit_object(name, folder=KIT_DIR):
    path = folder + name + ".fbx"
    objs = import_fbx(path)
    keep = [o for o in objs if o.name.startswith(name) and not o.name.startswith("UCX")]
    for o in objs:
        if o not in keep:
            bpy.data.objects.remove(o)
    return keep[0]


def kit_measure(ob):
    mw = ob.matrix_world
    vs = [mw @ v.co for v in ob.data.vertices]
    zmin = min(v.z for v in vs)
    area = {}
    for p in ob.data.polygons:
        c = mw @ p.center
        if (mw.to_3x3() @ p.normal).normalized().z > 0.95:
            key = round(c.z / 0.05) * 0.05
            area[key] = area.get(key, 0.0) + p.area * mw.to_scale()[0] ** 2
    floor_z = max(area, key=area.get) if area else zmin
    base = [v for v in vs if v.z < zmin + 0.05]
    bc = ((min(v.x for v in base) + max(v.x for v in base)) / 2, (min(v.y for v in base) + max(v.y for v in base)) / 2)
    fl = []
    for p in ob.data.polygons:
        c = mw @ p.center
        if abs(c.z - floor_z) < 0.05 and (mw.to_3x3() @ p.normal).normalized().z > 0.95:
            fl += [mw @ ob.data.vertices[i].co for i in p.vertices]
    fc = ((min(v.x for v in fl) + max(v.x for v in fl)) / 2, (min(v.y for v in fl) + max(v.y for v in fl)) / 2)
    fr = (max(v.x for v in fl) - min(v.x for v in fl)) / 2
    return {"zmin": zmin, "floor_z": floor_z, "base_c": bc, "floor_c": fc, "floor_r": fr, "verts": vs,
            "size": [max(v[k] for v in vs) - min(v[k] for v in vs) for k in range(3)]}


def place_kit_copy(src, name, mats, matrix, only_slot):
    me = src.data.copy()
    me.transform(matrix)
    me.materials.clear()
    me.materials.append(mats[only_slot])
    for p in me.polygons:
        p.material_index = 0
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def build_pavilions(mats, info):
    src = kit_object("SM_Small_Gazebo")
    m = kit_measure(src)
    s = PAV_SCALE
    # The pavilion body is off-centre on its base disc (the base is as wide as the drum). Turn the
    # offset toward the deck: the body then leaves a wide ledge on the outer side for the balustrade.
    alpha = math.atan2(m["floor_c"][1] - m["base_c"][1], m["floor_c"][0] - m["base_c"][0])
    info["pavilion"] = {"source_size_m": [round(x, 3) for x in m["size"]], "floor_z_in_source": round(m["floor_z"], 3),
                        "base_centre": [round(x, 3) for x in m["base_c"]], "floor_centre": [round(x, 3) for x in m["floor_c"]],
                        "floor_radius": round(m["floor_r"], 3), "floor_offset_direction_deg": round(math.degrees(alpha), 1),
                        "scale": s}
    out, circles = [], {}
    for side, tag in ((-1, "l"), (1, "r")):
        target = math.atan2(-1.0, -1.0 * side)  # the floor offset points toward the deck
        psi = target - alpha
        mw = src.matrix_world
        mat = (Matrix.Translation((side * DRUM_CX, DRUM_CY, DECK_Z + PAV_LIFT)) @ Matrix.Scale(s, 4) @
               Matrix.Rotation(psi, 4, "Z") @ Matrix.Translation((-m["base_c"][0], -m["base_c"][1], -m["floor_z"])) @ mw)
        ob = place_kit_copy(src, f"rivendell_gatehouse_g2_blockout_pavilion_{tag}", mats, mat, TRIM)
        out.append(ob)
        fc = Vector((m["floor_c"][0] - m["base_c"][0], m["floor_c"][1] - m["base_c"][1], 0))
        fc.rotate(Matrix.Rotation(psi, 3, "Z"))
        circles[side] = (side * DRUM_CX + fc.x * s, DRUM_CY + fc.y * s, m["floor_r"] * s)
    bpy.data.objects.remove(src)
    return out, circles


def build_statues(mats, info):
    src = kit_object("SM_Statue")
    m = kit_measure(src)
    info["statue"] = {"source_size_m": [round(x, 3) for x in m["size"]], "scale": STATUE_SCALE,
                      "scaled_size_m": [round(x * STATUE_SCALE, 3) for x in m["size"]]}
    out = []
    for side, tag in ((-1, "l"), (1, "r")):
        yaw = math.radians(STATUE_YAW_DEG)
        mat = (Matrix.Translation((side * STATUE_X, STATUE_Y, PEDESTAL_H)) @ Matrix.Scale(STATUE_SCALE, 4) @
               Matrix.Rotation(yaw, 4, "Z") @ Matrix.Translation((-m["base_c"][0], -m["base_c"][1], -m["zmin"])) @ src.matrix_world)
        ob = place_kit_copy(src, f"rivendell_gatehouse_g2_blockout_statue_{tag}", mats, mat, TRIM)
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        h = PEDESTAL_W / 2
        box(bm, side * STATUE_X - h, side * STATUE_X + h, STATUE_Y - h, STATUE_Y + h, 0.0, PEDESTAL_H, 0)  # slot 0
        box(bm, side * STATUE_X - h - 0.08, side * STATUE_X + h + 0.08, STATUE_Y - h - 0.08, STATUE_Y + h + 0.08,
            PEDESTAL_H - 0.15, PEDESTAL_H, 0)
        bm.to_mesh(ob.data)
        bm.free()
        out.append(ob)
    bpy.data.objects.remove(src)
    return out


# ---- placeholders ---------------------------------------------------------------------------------
def build_gates(mats):
    objs = []
    bm = bmesh.new()
    for s in (-1, 1):  # pair of leaves, 4.70 wide together, 6.84 high, 0.5 thick, closed at Y +4.2
        xa, xb = sorted((0.0, s * 2.35))
        box(bm, xa, xb, OUTER_GATE_Y - 0.25, OUTER_GATE_Y + 0.25, 0.0, 6.84, GATE)
    objs.append(object_from_bmesh("PLACEHOLDER_outer_gate", bm, mats))
    bm.free()
    bm = bmesh.new()
    fw, fh, lw, lh = 4.52 / 2, 5.8, 3.47 / 2, 3.8
    frame = [(-fw, 0.0), (-lw, 0.0), (-lw, lh), (lw, lh), (lw, 0.0), (fw, 0.0), (fw, fh), (-fw, fh)]
    prism(bm, [frame], along_y(INNER_GATE_Y - 0.25, INNER_GATE_Y + 0.25), GATE)
    for s in (-1, 1):
        xa, xb = sorted((0.0, s * lw))
        box(bm, xa, xb, INNER_GATE_Y - 0.15, INNER_GATE_Y + 0.15, 0.0, lh, GATE)
    objs.append(object_from_bmesh("PLACEHOLDER_inner_gate", bm, mats))
    bm.free()
    return objs


# ---- scene ----------------------------------------------------------------------------------------
def make_materials():
    mats = []
    for name, hx in COLOURS.items():
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = hex_to_linear(hx)
        mats.append(m)
    return mats


def with_edge(fn, edge_y):
    old = wall.HALF_THICK
    wall.HALF_THICK = edge_y
    try:
        fn()
    finally:
        wall.HALF_THICK = old


def build_parapets(mats, detail, make):
    """m1 and m4 are the wings on the facade plane (Y 5.4), m2 and m3 run along the door frame (Y 6.2)."""
    old = (wall.OPEN_CENTRES, wall.PANEL_CX)
    wing = min(detail, 2)
    make("rivendell_gatehouse_g2_blockout_m1", lambda bm: with_edge(
        lambda: (wall.build_parapet(bm, -4.7, -PORTAL_HALF_W, wing), ret_box(bm, -PORTAL_HALF_W, -PORTAL_HALF_W + 0.5)), FACADE_Y))
    wall.OPEN_CENTRES, wall.PANEL_CX = (0.75, 2.25), 1.5
    make("rivendell_gatehouse_g2_blockout_m2", lambda bm: with_edge(
        lambda: wall.build_parapet(bm, -PORTAL_HALF_W, 0.0, detail), HALF_D))
    make("rivendell_gatehouse_g2_blockout_m3", lambda bm: with_edge(
        lambda: wall.build_parapet(bm, 0.0, PORTAL_HALF_W, detail), HALF_D))
    wall.OPEN_CENTRES, wall.PANEL_CX = old
    make("rivendell_gatehouse_g2_blockout_m4", lambda bm: with_edge(
        lambda: (wall.build_parapet(bm, PORTAL_HALF_W, 4.7, wing), ret_box(bm, PORTAL_HALF_W - 0.5, PORTAL_HALF_W)), FACADE_Y))
    make("rivendell_gatehouse_g2_blockout_m6", lambda bm: with_edge(lambda: wall.build_rail(bm, detail), HALF_D))


def ret_box(bm, xa, xb):
    """The return wall at a jog of the parapet, so the edge between Y 5.4 and 6.2 is closed."""
    box(bm, xa, xb, FACADE_Y - 0.5, FACADE_Y + 0.8 - 0.1, wall.PARAPET_Z0, DECK_Z + wall.PARAPET_H, TRIM)


def build_variant_g2(detail, mats, info):
    objs = []

    def make(name, fn):
        bm = bmesh.new()
        fn(bm)
        objs.append(object_from_bmesh(name, bm, mats))
        bm.free()

    pav, circles = build_pavilions(mats, info)
    bm = build_core_body(detail)
    build_foundation_and_plinth(bm, detail)
    deck_items(bm, detail)
    if detail >= 2:
        hall_ribs(bm)
        facade_trim(bm, detail)
        inner_piers(bm, detail)
        inner_door_trim(bm)
        for side in (-1, 1):
            drum_cornice(bm, side)
            drum_balustrade(bm, side, circles[side])
    else:
        facade_trim(bm, 1)
    if detail >= 3:
        hall_blind_arches(bm)
        for side in (-1, 1):
            drum_trunks(bm, side)
    objs.append(object_from_bmesh("rivendell_gatehouse_g2_blockout", bm, mats))
    bm.free()
    build_parapets(mats, detail, make)
    objs += pav
    objs += build_statues(mats, info)
    return objs


# ---- g1: spired turrets, pointed arch, open gable roof on six columns -------------------------------
TURRET_X, TURRET_Y = 7.5, 3.7
TURRET_DIR = "E:/LOTRAOMAssets/_export/rivendell/meshes/Exterior_Construction/GENERATED/"
SPIRE_ROOF_R, SPIRE_ROOF_H = 2.65, 7.0  # slate spire on the part's cap flange (the part ends in a bare stone tip)
ARCH_SX, ARCH_SY, ARCH_SZ, ARCH_Z0 = 3.6, 1.5, 0.95, 3.1  # kit arch: wide and low so its piers clear the door
ARCH_CLIP_X = 4.95  # faces beyond this |X| (the scroll feet) are cut: the turret stands at 5.0
G1_PARAPET = ((-5.9, -5.9 / 3.0), (-5.9 / 3.0, 5.9 / 3.0), (5.9 / 3.0, 5.9))  # to the turret shafts (|X| 5.78)
ROOF_HX, ROOF_HY = 4.6, 5.2
EAVE_Z, RIDGE_Z, ROOF_T = 15.6, 19.6, 0.16
COL_T, COL_X, COL_YS = 0.4, 4.2, (-4.8, 0.0, 4.8)  # six columns, none within 1 m of a thrower or in a throw line
RAFTER_YS = (-3.9, -2.6, -1.3, 0.0, 1.3, 2.6, 3.9)


def g1_outline():
    return [(-HALF_W, -HALF_D), (HALF_W, -HALF_D), (HALF_W, HALF_D), (-HALF_W, HALF_D)]


def build_g1_foundation_and_plinth(bm, detail):
    prism(bm, [g1_outline()], vertical(FOUNDATION_Z0, 0.0), RUBBLE)
    for z0, z1, p in plinth_courses(detail):
        for s in (-1, 1):
            xa, xb = sorted((s * DOOR_HALF_W, s * 5.0))  # outer face, from the door jamb to the turret
            box(bm, xa, xb, HALF_D, HALF_D + p, z0, z1, RUBBLE)
            xa, xb = sorted((s * DOOR_HALF_W, s * HALF_W))
            box(bm, xa, xb, -HALF_D - p, -HALF_D, z0, z1, RUBBLE)  # inner face


def g1_facade_trim(bm):
    """String course across the outer face between the turrets, broken where the arch rises."""
    for s in (-1, 1):
        xa, xb = sorted((s * 0.9, s * 5.0))
        box(bm, xa, xb, HALF_D, HALF_D + 0.2, 11.2, 11.9, TRIM)


def g1_inner_face(bm, detail):
    """Inner face (Y -6.2): the wall's band and string course beside the door, and at detail 3 its blind arches."""
    for s in (-1, 1):
        xa, xb = sorted((s * 3.0, s * HALF_W))
        for z0, z1, pr in ((wall.BAND_Z0, wall.BAND_Z1, wall.BAND_PROUD), (wall.STRING_Z0, wall.STRING_Z1, wall.STRING_PROUD)):
            box(bm, xa, xb, -HALF_D - pr, -HALF_D + 0.1, z0, z1, TRIM)
    if detail < 3:
        return
    for cx in (-8.6, -4.8, 4.8, 8.6):
        tmp = bmesh.new()
        wall.build_blind_arch(tmp, cx)
        merge_into(bm, tmp, Matrix.Translation((0, wall.HALF_THICK - HALF_D, 0)) @ Matrix.Rotation(math.pi, 4, "Z"))
        tmp.free()


def bar(bm, p0, p1, w, y0, y1, mat):
    """A straight bar of width `w` between two points of the (x, z) plane, extruded from y0 to y1."""
    d = Vector((p1[0] - p0[0], p1[1] - p0[1]))
    n = Vector((-d.y, d.x)).normalized() * (w / 2)
    poly = [(p0[0] + n.x, p0[1] + n.y), (p1[0] + n.x, p1[1] + n.y), (p1[0] - n.x, p1[1] - n.y), (p0[0] - n.x, p0[1] - n.y)]
    prism(bm, [poly], along_y(y0, y1), mat)


def build_roof(bm, detail):
    """Open gable roof, ridge along Y, gables at Y +5.2 (outside) and -5.2, six columns."""
    t = ROOF_T
    for s in (-1, 1):
        prism(bm, [[(s * ROOF_HX, EAVE_Z), (0.0, RIDGE_Z), (0.0, RIDGE_Z - t), (s * ROOF_HX, EAVE_Z - t)]],
              along_y(-ROOF_HY, ROOF_HY), SLATE)
        for y in COL_YS:
            box(bm, s * COL_X - COL_T / 2, s * COL_X + COL_T / 2, y - COL_T / 2, y + COL_T / 2, DECK_Z, EAVE_Z - 0.35, TRIM)
        xa, xb = sorted((s * (COL_X - 0.25), s * (COL_X + 0.25)))
        box(bm, xa, xb, -ROOF_HY + 0.2, ROOF_HY - 0.2, EAVE_Z - 0.35, EAVE_Z - 0.05, WOOD)  # wall plate
    box(bm, -0.18, 0.18, -ROOF_HY, ROOF_HY, RIDGE_Z - t, RIDGE_Z + 0.14, SLATE)  # ridge cap
    for y in (-ROOF_HY + 0.2, ROOF_HY - 0.2):  # the two gable frames
        a, b = y - 0.1, y + 0.1
        box(bm, -ROOF_HX + 0.15, ROOF_HX - 0.15, a, b, EAVE_Z - 0.35, EAVE_Z - 0.05, WOOD)  # tie beam
        for s in (-1, 1):
            bar(bm, (s * (ROOF_HX - 0.1), EAVE_Z - t - 0.2), (0.0, RIDGE_Z - t - 0.25), 0.28, a, b, WOOD)
        box(bm, -0.1, 0.1, a, b, EAVE_Z - 0.05, RIDGE_Z - t - 0.3, WOOD)  # king post
    if detail < 2:
        return
    for y in RAFTER_YS:
        for s in (-1, 1):
            bar(bm, (s * (ROOF_HX - 0.1), EAVE_Z - t - 0.14), (0.0, RIDGE_Z - t - 0.17), 0.2, y - 0.06, y + 0.06, WOOD)
    for s in (-1, 1):
        for y in COL_YS:  # capital and base on each column
            for z0, z1, w in ((EAVE_Z - 0.6, EAVE_Z - 0.35, 0.62), (DECK_Z, DECK_Z + 0.3, 0.64)):
                box(bm, s * COL_X - w / 2, s * COL_X + w / 2, y - w / 2, y + w / 2, z0, z1, TRIM)
        for y in (-ROOF_HY + 0.2, ROOF_HY - 0.2):  # braces from the wall plate to the tie beam
            bar(bm, (s * (COL_X - 0.2), EAVE_Z - 0.35), (s * (COL_X - 1.4), EAVE_Z - 1.2), 0.22, y - 0.1, y + 0.1, WOOD)
    if detail >= 3:
        for y in (-ROOF_HY + 0.2, ROOF_HY - 0.2):
            for s in (-1, 1):  # struts from the king post to the rafters
                bar(bm, (0.0, RIDGE_Z - 1.2), (s * 1.9, RIDGE_Z - 2.3), 0.16, y - 0.1, y + 0.1, WOOD)
        for y in (-ROOF_HY + 0.2, ROOF_HY - 0.2):
            box(bm, -0.3, 0.3, y - 0.12, y + 0.12, EAVE_Z + 0.2, EAVE_Z + 0.3, WOOD)  # collar plank


def spire_roof(bm, cx, cy, z0, mat):
    """Octagonal slate pyramid on the turret top, with its underside closed."""
    ring = [bm.verts.new((cx + SPIRE_ROOF_R * math.cos(math.radians(22.5 + 45 * i)),
                          cy + SPIRE_ROOF_R * math.sin(math.radians(22.5 + 45 * i)), z0)) for i in range(8)]
    apex = bm.verts.new((cx, cy, z0 + SPIRE_ROOF_H))
    faces = [_new_face(bm, [ring[i], ring[(i + 1) % 8], apex]) for i in range(8)]
    faces.append(_new_face(bm, ring[::-1]))
    finish(bm, faces, mat)


def build_turrets(mats, info):
    src = kit_object("SM_Spire_Cut", TURRET_DIR)
    mw = src.matrix_world
    vs = [mw @ v.co for v in src.data.vertices]
    lo = [min(v[k] for v in vs) for k in range(3)]
    hi = [max(v[k] for v in vs) for k in range(3)]
    cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    flange = max(v.z for v in vs if max(abs(v.x - cx), abs(v.y - cy)) > 2.2) - lo[2]
    info["turret"] = {"source_size_m": [round(hi[k] - lo[k], 3) for k in range(3)], "scale_on_object": list(src.scale),
                      "cap_flange_top_z": round(flange, 3), "roof_apex_z": round(flange - 0.05 + SPIRE_ROOF_H, 3)}
    out = []
    for side, tag in ((-1, "l"), (1, "r")):
        mat = Matrix.Translation((side * TURRET_X, TURRET_Y, 0.0)) @ Matrix.Translation((-cx, -cy, -lo[2])) @ mw
        ob = place_kit_copy(src, f"rivendell_gatehouse_g1_blockout_turret_{tag}", mats, mat, TRIM)
        ob.data.materials.append(mats[SLATE])
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        spire_roof(bm, side * TURRET_X, TURRET_Y, flange - 0.05, 1)
        bm.to_mesh(ob.data)
        bm.free()
        out.append(ob)
    bpy.data.objects.remove(src)
    return out


def build_arch(mats, info):
    src = kit_object("SM_Decorative_Arch")
    y0 = HALF_D - 0.05 * ARCH_SY - 0.15  # the kit's back face is 0.15 m inside the body; it faces -Y, so turn it out
    mat = (Matrix.Translation((0.0, y0, ARCH_Z0)) @ Matrix.Rotation(math.pi, 4, "Z") @
           Matrix.Diagonal((ARCH_SX, ARCH_SY, ARCH_SZ, 1.0)) @ src.matrix_world)
    ob = place_kit_copy(src, "rivendell_gatehouse_g1_blockout_arch", mats, mat, TRIM)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    cut = [f for f in bm.faces if any(abs(v.co.x) > ARCH_CLIP_X for v in f.verts)]
    bmesh.ops.delete(bm, geom=cut, context="FACES")
    bm.to_mesh(ob.data)
    bm.free()
    info["arch"] = {"scale": [ARCH_SX, ARCH_SY, ARCH_SZ], "z0": ARCH_Z0, "y0": round(y0, 3), "faces_cut_beyond_x": len(cut),
                    "clip_x": ARCH_CLIP_X}
    bpy.data.objects.remove(src)
    return ob


def build_variant_g1(detail, mats, info):
    objs = []

    def make(name, fn):
        bm = bmesh.new()
        fn(bm)
        objs.append(object_from_bmesh(name, bm, mats))
        bm.free()

    bm = build_core_body(detail, g1_outline(), niches=False)
    build_g1_foundation_and_plinth(bm, detail)
    deck_items(bm, detail)
    if detail >= 2:
        hall_ribs(bm)
        g1_facade_trim(bm)
        inner_piers(bm, detail)
        inner_door_trim(bm)
        g1_inner_face(bm, detail)
    if detail >= 3:
        hall_blind_arches(bm)
    objs.append(object_from_bmesh("rivendell_gatehouse_g1_blockout", bm, mats))
    bm.free()
    for i, (x0, x1) in enumerate(G1_PARAPET, 1):
        make(f"rivendell_gatehouse_g1_blockout_m{i}", lambda bm, a=x0, b=x1: with_edge(
            lambda: wall.build_parapet(bm, a, b, detail), HALF_D))
    make("rivendell_gatehouse_g1_blockout_m6", lambda bm: with_edge(lambda: wall.build_rail(bm, detail), HALF_D))
    make("rivendell_gatehouse_g1_blockout_roof", lambda bm: build_roof(bm, detail))
    objs += build_turrets(mats, info)
    objs.append(build_arch(mats, info))
    return objs


VARIANTS = {"g2": build_variant_g2, "g1": build_variant_g1}


# ---- measurements ---------------------------------------------------------------------------------
class Caster:
    def __init__(self):
        bpy.context.view_layer.update()
        self.dg = bpy.context.evaluated_depsgraph_get()
        self.scene = bpy.context.scene

    def hit(self, o, d, dist=100.0):
        ok, loc, _n, _i, ob, _m = self.scene.ray_cast(self.dg, Vector(o), Vector(d).normalized(), distance=dist)
        return (loc, ob.name) if ok else (None, None)

    def dist(self, o, d, dist=100.0):
        loc, _ = self.hit(o, d, dist)
        return (loc - Vector(o)).length if loc is not None else None


def measure_checks(cs):
    res = {}
    # 1. door openings: width at several heights (ray each way), height at several X
    for tag, y in (("outer", 5.4), ("inner", -5.4)):
        widths = {}
        for z in (0.5, 2.0, 4.0, 6.0, 6.9):
            a, b = cs.dist((0, y, z), (1, 0, 0)), cs.dist((0, y, z), (-1, 0, 0))
            widths[z] = None if a is None or b is None else round(a + b, 3)
        heights = {}
        for x in (-2.3, -1.5, 0.0, 1.5, 2.3):
            d = cs.dist((x, y, 0.05), (0, 0, 1))
            heights[x] = None if d is None else round(d + 0.05, 3)
        res[f"door_{tag}"] = {"width_by_z": widths, "height_by_x": heights,
                              "clear_width": min(w for w in widths.values() if w),
                              "clear_height_at_jamb_x2.3": min(heights[-2.3], heights[2.3]),
                              "apex_height": heights[0.0]}
    # 2. hall
    hw = {}
    for y in (-3.0, 1.8, 3.0):
        for z in (1.0, 3.0, 5.0):
            a, b = cs.dist((0, y, z), (1, 0, 0)), cs.dist((0, y, z), (-1, 0, 0))
            hw[f"y{y}_z{z}"] = round(a + b, 3)
    hh = {}
    for y in (-3.6, -1.8, 0.0, 1.8, 3.6):
        d = cs.dist((0, y, 0.2), (0, 0, 1))
        hh[f"x0_y{y}"] = None if d is None else round(d + 0.2, 3)
    res["hall"] = {"width": hw, "height": hh, "clear_width_min": min(hw.values()),
                   "clear_height_between_ribs": hh["x0_y1.8"], "clear_height_under_rib": hh["x0_y0.0"]}
    # 3. hole: size by scanning, then the 12 rays
    step = 0.05
    xs = [x * step for x in range(-80, 81)]
    open_x = [x for x in xs if (cs.hit((x, -1.8, 13.0), (0, 0, -1))[0] or Vector((0, 0, 99)))[2] < 3.0]
    ys = [-3.5 + i * step for i in range(0, 61)]
    open_y = [y for y in ys if (cs.hit((0.0, y, 13.0), (0, 0, -1))[0] or Vector((0, 0, 99)))[2] < 3.0]
    rays, clear = [], 0
    for ex in (-HOLE_X, HOLE_X):
        far = 1 if ex < 0 else -1
        for k in range(6):
            tx = far * (0.5 + 2.7 * k / 5)
            eye, tgt = Vector((ex, -1.8, 13.6)), Vector((tx, -1.8, 1.2))
            loc, name = cs.hit(eye, tgt - eye, (tgt - eye).length - 0.001)
            rays.append({"eye_x": ex, "target_x": round(tx, 2), "clear": loc is None, "blocked_by": name})
            clear += loc is None
    res["hole"] = {"x_extent": [round(min(open_x), 3), round(max(open_x), 3)], "x_size": round(max(open_x) - min(open_x) + step, 3),
                   "y_extent": [round(min(open_y), 3), round(max(open_y), 3)], "y_size": round(max(open_y) - min(open_y) + step, 3),
                   "rays": rays, "rays_clear": clear, "rays_total": len(rays)}
    # short ends open: a horizontal ray along X at Y -1.8, 0.1 above the deck
    ends, _ = cs.hit((-9.5, -1.8, DECK_Z + 0.1), (1, 0, 0), 19.0)
    res["hole"]["short_ends_open_ray_clear"] = ends is None
    # 4. deck height and walk path
    zs = {}
    for x, y in ((-9.0, -4.4), (0.0, -4.4), (9.0, -4.4), (-8.0, -1.0), (0.0, 2.0), (8.0, -1.0)):
        loc, _ = cs.hit((x, y, 14.0), (0, 0, -1), 30.0)
        zs[f"{x},{y}"] = None if loc is None else round(loc.z, 4)
    paths = {}
    for i in range(-99, 100, 3):
        x = i / 10.0
        a, b = cs.dist((x, -4.4, DECK_Z + 0.1), (0, 1, 0)), cs.dist((x, -4.4, DECK_Z + 0.1), (0, -1, 0))
        paths[x] = None if a is None or b is None else round(a + b, 3)
    vals = [v for v in paths.values() if v is not None]
    behind = [v for x, v in paths.items() if v is not None and abs(x) <= HOLE_X]
    res["deck"] = {"z_samples": zs, "walk_width_min_all_x": min(vals), "walk_width_min_behind_hole": min(behind),
                   "walk_width_samples": paths}
    res["through"] = measure_through(cs)
    res["deck_grid"] = measure_walk(cs)
    return res


def run_len(flags, i0):
    """Length (in cells) of the run of True around index i0 (0 when flags[i0] is False)."""
    if not flags[i0]:
        return 0
    a = b = i0
    while a > 0 and flags[a - 1]:
        a -= 1
    while b < len(flags) - 1 and flags[b + 1]:
        b += 1
    return b - a + 1


def measure_through(cs):
    """Door opening seen THROUGH the whole piece: rays along -Y from outside the arch to behind the inner door.
    A cell is clear when the ray meets nothing: arch, tunnel, hall ribs and inner trim all count."""
    dx, dz = 0.05, 0.05
    xs = [round(i * dx, 3) for i in range(-70, 71)]
    zs = [round(0.025 + k * dz, 3) for k in range(0, 160)]
    hits = [[cs.hit((x, 14.0, z), (0, -1, 0), 28.0) for z in zs] for x in xs]
    clear = [[h[0] is None for h in col] for col in hits]
    blockers = {}
    for i, x in enumerate(xs):
        for k, z in enumerate(zs):
            if abs(x) < 2.349 and z <= 7.0 and not clear[i][k]:
                b = blockers.setdefault(hits[i][k][1], [0, 99.0, -99.0, 99.0, -99.0])
                b[0] += 1
                b[1], b[2], b[3], b[4] = min(b[1], x), max(b[2], x), min(b[3], z), max(b[4], z)
    ix0 = xs.index(0.0)
    widths = {}
    for z in (0.5, 2.0, 4.0, 6.0, 6.95):
        k = min(range(len(zs)), key=lambda q: abs(zs[q] - z))
        widths[z] = round(run_len([clear[i][k] for i in range(len(xs))], ix0) * dx, 3)
    heights = {}
    for x in (-2.3, -1.5, 0.0, 1.5, 2.3):
        i = xs.index(round(x, 3)) if round(x, 3) in xs else min(range(len(xs)), key=lambda q: abs(xs[q] - x))
        heights[x] = round(run_len(clear[i], 0) * dz, 3)
    rect = all(clear[i][k] for i, x in enumerate(xs) if abs(x) < 2.349 for k, z in enumerate(zs) if z <= 7.0)
    return {"width_by_z": widths, "height_by_x": heights, "min_width_z0.5_to_6.95": min(widths.values()),
            "min_height_x_pm2.3": min(heights[-2.3], heights[2.3]), "rect_4.7x7.0_fully_clear": rect,
            "blockers_in_rect_count_xmin_xmax_zmin_zmax": blockers,
            "cells_blocked_in_rect": sum(1 for i, x in enumerate(xs) if abs(x) < 2.349
                                         for k, z in enumerate(zs) if z <= 7.0 and not clear[i][k])}


def measure_walk(cs):
    """Walkable deck cells (0.1 m): free 1.9 m above and the deck 12.0 below. The walk is the free run along Y
    through Y -4.0 at each X; both hole ends must be reachable from the X -10 end by 4-connected cells."""
    st = 0.1
    nx, ny = 200, 124
    x_of = lambda i: -10.0 + (i + 0.5) * st
    y_of = lambda j: -6.2 + (j + 0.5) * st
    ok = []
    for i in range(nx):
        col = []
        for j in range(ny):
            x, y = x_of(i), y_of(j)
            up = cs.hit((x, y, DECK_Z + 0.05), (0, 0, 1), 1.9)[0] is None
            loc, _ = cs.hit((x, y, DECK_Z + 0.5), (0, 0, -1), 1.0)
            col.append(up and loc is not None and abs(loc.z - DECK_Z) < 0.02)
        ok.append(col)
    j0 = int((-4.0 + 6.2) / st)
    widths = {round(x_of(i), 2): round(run_len(ok[i], j0) * st, 2) for i in range(nx)}
    behind = [v for x, v in widths.items() if abs(x) <= HOLE_X]
    seen = {(0, j0)}
    todo = [(0, j0)]
    while todo:
        i, j = todo.pop()
        for a, b in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
            if 0 <= a < nx and 0 <= b < ny and ok[a][b] and (a, b) not in seen:
                seen.add((a, b))
                todo.append((a, b))
    jh = int((-1.8 + 6.2) / st)
    ends = {}
    for tag, x in (("x-3.15", -3.15), ("x+3.15", 3.15)):
        ends[tag] = (int((x + 10.0) / st), jh) in seen
    return {"cell_m": st, "walk_width_min_all_x": min(widths.values()), "walk_width_min_behind_hole": min(behind),
            "x_with_width_below_2.5": [x for x, v in widths.items() if v < 2.5],
            "walk_width_at_x": {k: widths[k] for k in sorted(widths) if abs(round(k * 10) % 15) < 1},
            "both_hole_ends_reachable_from_x_minus10": all(ends.values()), "hole_ends": ends,
            "walkable_cells": len(seen)}


# ---- pictures -------------------------------------------------------------------------------------
def add_box(name, dims, loc, mat, mats):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=dims, verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    ob = object_from_bmesh(name, bm, [mat])
    bm.free()
    return ob


def setup_render(path, res=(1376, 768)):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = path
    scene.render.image_settings.file_format = "PNG"
    sh = scene.display.shading
    sh.light, sh.color_type, sh.show_cavity = "STUDIO", "MATERIAL", False
    scene.world = scene.world or bpy.data.worlds.new("w")
    scene.world.color = (0.62, 0.64, 0.66)


PICTURES = {"g2": {"cam": (-24.0, 32.0, 26.0), "look": (0.0, 0.0, 9.0), "ortho": 40.0, "sec_look": (0.0, 0.0, 9.0)},
            "g1": {"cam": (-36.0, 48.0, 36.0), "look": (0.0, 0.0, 12.0), "ortho": 62.0, "sec_look": (0.0, 0.0, 14.0)}}


def render_pictures(out, pass_n, variant="g2"):
    pic = PICTURES[variant]
    scene = bpy.context.scene
    red = bpy.data.materials.new("scale_ref")
    red.diffuse_color = (0.8, 0.1, 0.1, 1.0)
    gr = bpy.data.materials.new("preview_ground")
    gr.diffuse_color = hex_to_linear("c9cacb")
    add_box("preview_ground", (140.0, 140.0, 0.02), (0, 0, -0.011), gr, None)
    fig_out = add_box("scale_1p8m_out", (0.5, 0.3, 1.8), (5.0, 8.5, 0.9), red, None)
    fig_deck = add_box("scale_1p8m_deck", (0.5, 0.3, 1.8), (-1.0, -4.4, DECK_Z + 0.9), red, None)
    fig_hall = add_box("scale_1p8m_hall", (0.5, 0.3, 1.8), (-2.0, 1.0, 0.9), red, None)
    cd = bpy.data.cameras.new("cam")
    cd.lens, cd.clip_end = 30.0, 500.0
    cam = bpy.data.objects.new("cam", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # deck picture: outside and above, from the left, as the concept
    for f in (fig_hall,):
        f.hide_render = True
    cam.location = pic["cam"]
    cam.rotation_euler = (Vector(pic["look"]) - cam.location).to_track_quat("-Z", "Y").to_euler()
    setup_render(os.path.join(out, f"concept_deck_pass{pass_n}.png"))
    bpy.ops.render.render(write_still=True)
    # close-up of the door frame (statue facing, ogee), from outside at eye height
    cam.location = (-7.0, 17.0, 5.0)
    cam.rotation_euler = (Vector((1.5, 6.0, 5.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    setup_render(os.path.join(out, f"concept_door_pass{pass_n}.png"))
    bpy.ops.render.render(write_still=True)
    # section: cut every mesh at X = 0 (the camera clip plane did not cut in Workbench), look along -X
    for ob in list(scene.objects):
        if ob.type == "MESH" and (ob.name.startswith("rivendell_") or ob.name.startswith("PLACEHOLDER_")):
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=(1, 0, 0), clear_outer=True)
            bm.to_mesh(ob.data)
            bm.free()
    fig_out.hide_render = fig_deck.hide_render = True
    fig_hall.hide_render = False
    cd.type, cd.ortho_scale = "ORTHO", pic["ortho"]
    cam.location = (60.0, -12.0, pic["sec_look"][2] + 2.0)
    cam.rotation_euler = (Vector(pic["sec_look"]) - cam.location).to_track_quat("-Z", "Y").to_euler()
    add_box("scale_deck2", (0.3, 0.5, 1.8), (-0.5, -4.4, DECK_Z + 0.9), red, None)
    setup_render(os.path.join(out, f"concept_section_pass{pass_n}.png"))
    bpy.ops.render.render(write_still=True)


def main():
    args = script_args()
    out = flag_value(args, "--out")
    variant = flag_value(args, "--variant") or "g2"
    detail = int(flag_value(args, "--detail") or 3)
    pass_n = flag_value(args, "--pass") or str(detail)
    with job(report_path(out)) as result:
        os.makedirs(out, exist_ok=True)
        if VARIANTS.get(variant) is None:
            raise SystemExit(f"variant {variant!r} is not built in this script yet")
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mats = make_materials()
        info = {}
        objs = VARIANTS[variant](detail, mats, info)
        result.update(info)
        result["detail"] = detail
        result["objects"] = {o.name: mesh_stats(o) for o in objs}
        result["checks"] = measure_checks(Caster())  # before the placeholder gates exist: they are hidden
        gates = build_gates(mats)
        result["objects"].update({o.name: mesh_stats(o) for o in gates})
        result["total_tris_asset"] = sum(v["tris"] for k, v in result["objects"].items() if not k.startswith("PLACEHOLDER"))
        fbx = os.path.join(out, f"rivendell_gatehouse_{variant}_blockout.fbx")
        export_fbx(fbx, objs)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, f"gatehouse_{variant}_blockout.blend"))
        result["fbx"] = fbx
        with open(os.path.join(out, "blockout_report.json"), "w", encoding="utf-8") as f:
            json.dump({k: result[k] for k in result if k != "status"}, f, indent=1, default=str)
        render_pictures(out, pass_n, variant)


if __name__ == "__main__":
    main()
