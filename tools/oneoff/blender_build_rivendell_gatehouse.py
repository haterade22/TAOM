"""Build of the Rivendell gatehouses, version 6, real assets for a Modding Kit import. Headless:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_build_rivendell_gatehouse.py --
        --out <dir> [--variant a|b]

Variant a (the default) is the domed gatehouse (picture g2), described next. Variant b is the spired gatehouse
(picture g1); its description is above build_main_b, and it writes rivendell_castle_gatehouse_b.

Frame: metres, the piece runs along X from -13.333 to +13.333, the outer face is +Y, Z is up, Z = 0 is the ground
and the passage floor, the deck is at Z 12.0. The numbers of the joint profile and the texel density are in
tools/oneoff/rivendell_joint_profile.py, the plan geometry (drums, outlines, paths, the stub's arch, the pavilion
fit, the passage tone) in tools/oneoff/rivendell_gatehouse_geometry.py.

A 20 m body (|X| <= 10) sits between two stubs of real wall, each half a bay (3.333 m) long. A stub is made by the
solid wall's own functions (tools/oneoff/blender_build_rivendell_wall.py): the plinth, band, string course, deck, the
inner members and a half pier at the joint plane (|X| = 13.333, exactly the joint profile). There is no pier at
|X| = 10: the drum is tangent to that plane, and the stub's wall starts there, so the drum ends the stub with a
vertical line (a 0.4 m flat return, then the stub's face). The ribs of a stub make one complete small pointed
arch over its blind arch: a limb from the joint pier and a limb from a carved corbel, ending at one apex.
The drums take the wall's lines: a plain battered rubble cone to Z 3.0 with the plinth cap, the carved band (mapped
like the wall's, the whole strip), the string course at Z 10.5 to 10.9 and the deck at Z 12.0.

Version 6 of variant a (Mike's corrections of 2026-11-25, on the version 6 wall and shared code): the blind arches of a drum
are laid out by gg.drum_arch_centres_deg (two a drum, each seen whole from 10 to 20 m in front, 0.7 m of plain drum from the
facade end and between them) and the carved band ends flush on the outer leg of the nearest one (gg.drum_band_path); the
statue wing has room (gg.WING_X0, gg.STATUE_X, a narrower niche and pedestal); the bar, its corbels and the small arcade over
the door are gone: the string course runs straight across the whole front in one sweep (gg.front_course_path) at the height and
in the section of the stub's, and the door ring, an offset of the opening (gg.ring_outline), ends in its tip on the underside of
that course, 0.10 m proud against the course's 0.15; the ring is mapped from the apex on both sides (a mitre); every curve has
at most 4 degrees between two neighbour segments (gg.ARC_STEPS, gg.DOOR_STEPS, the wall's fine ribs and frames) and the drum,
the tunnel arcs and the swept members are shaded smooth with hard edges only at real corners (smooth_drums, smooth_sides,
sweep(smooth=True)); the stub ribs are the wall's equal-turn limbs cut on the arch's centre plane; the stub bands are flush with
the arch (jp.arch_band_spans); the corbel has the pier strip's taper (gg.corbel_taper_profile); the recesses show the stone.

Version 4 (the fix pass after the independent check of version 3), on the new shared code (the plinth rule with the bed
joint at V 1.3767 on the top edge, the roof and heap materials): every plinth face takes that rule (the cone of a drum
is mapped facet by facet, isometric, gg.facet_uv; the foundation facets and the cut ends of swept members by
gg.foundation_uv); the loose stones on the deck are on the heap material, one planar rule per face; no two surfaces
share a plane in the visible parts (the door rings stand clear of the facade, the crown of the door frame is three
disjoint boxes, the parapet returns and the rail returns fit between their neighbours, the drum members stop on the
front face of the stub's twin); the door ring is a strip of quads matched along its arc on the arch moulding strip
(gg.ring_pairs), with no collapsed UV; the corbel of a stub is a carved head on three moulded steps (gg.corbel_tiers);
the floors of the pavilions are the stone material on the deck's own mapping.

Every object carries the vertex colour "Col" of the wall (jp.vertex_tone), and the hall and the door tunnels add
soot and damp (gg.passage_soot). The large faces are cut into cells by the wall's Build.cut_cells.

Objects (all exported, but the placeholder gates):
  rivendell_castle_gatehouse_a             body, drums, stubs, passage, hall, rock hole, curb, piers
  rivendell_castle_gatehouse_a_m1 .. _m6   parapet parts: left stub, left wing, left door-frame part, right door-frame
                                           part, right wing, right stub
  rivendell_castle_gatehouse_a_m7, _m8     the parapet rings on the left and right drum
  rivendell_castle_gatehouse_a_m9          the inner rail (both stub rails, the two returns and the inner edge)
  ..._pavilion_l/_r  (kit part SM_Small_Gazebo), ..._statue_l/_r (kit part SM_Statue): the shapes are the kit's, the
                   UVs are a box projection at jp.TD on the plain strip S1 of the trim sheet, one UV layer (the faces of
                   a pavilion's floor are the stone material, slot 1, on the deck's mapping)
  PLACEHOLDER_outer_gate, PLACEHOLDER_inner_gate  (not exported)

Writes <out>/rivendell_castle_gatehouse_a.blend and .fbx and gatehouse_build_report.json. The Store launcher prints
nothing: wait for <out>/_report/DONE.txt (tools/blender/blender_job.py).
"""
import json
import math
import os
import sys
from contextlib import contextmanager

import bmesh
import bpy
from mathutils import Matrix, Vector

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
from blender_job import export_fbx, flag_value, import_fbx, job, report_path, script_args  # noqa: E402
import blender_build_rivendell_wall as W  # noqa: E402
import rivendell_gatehouse_geometry as gg  # noqa: E402
import rivendell_joint_profile as jp  # noqa: E402
from blender_build_rivendell_wall import Build, ROOF, RUBBLE, STONE, TRIM  # noqa: E402

BASE = "rivendell_castle_gatehouse_a"
XB, XE, HALF_D, FACADE_Y = gg.XB, gg.XE, gg.HALF_D, gg.FACADE_Y
UV_X0 = -XE
DECK_Z = jp.Z_DECK
R, CX, CY = gg.DRUM_R, gg.DRUM_CX, gg.DRUM_CY
PORTAL = gg.PORTAL_HALF_W

HALL_HALF_W, HALL_Y, HALL_CEIL = gg.HALL_HALF_W, gg.HALL_Y, gg.HALL_CEIL
DOOR_HALF_W, DOOR_SPRING, DOOR_RISE = gg.DOOR_HALF_W, gg.DOOR_SPRING, gg.DOOR_RISE   # opening 4.7 wide, 7.0 clear under the spring
DOOR_STEPS, RING_PROUD, RING_TIP_Z = gg.DOOR_STEPS, gg.RING_PROUD, gg.RING_TIP_Z
SMOOTH_DEG = 12.0   # a swept member is shaded smooth at a ring where its path turns less than this
HOLE_X, HOLE_Y0, HOLE_Y1 = gg.HOLE_X, gg.HOLE_Y0, gg.HOLE_Y1
CURB_H, CURB_T = 0.45, 0.3
RIB_YS = (-3.6, 0.0, 3.6)
RIB_SPRING, RIB_RISE, RIB_DROP, RIB_T = 6.0, 3.7, 0.4, 0.4
OUTER_GATE_Y, INNER_GATE_Y = 4.2, -4.2
PAV_SCALE, PAV_LIFT, PAV_MARGIN = 0.875, 0.02, 0.05
STATUE_SCALE, PEDESTAL_H, PEDESTAL_W, PEDESTAL_D = gg.STATUE_SCALE, gg.PEDESTAL_H, gg.PEDESTAL_W, gg.PEDESTAL_D
STATUE_X, STATUE_Y, STATUE_YAW_DEG = gg.STATUE_X, gg.STATUE_Y, 180.0
NICHE_HALF_W = gg.NICHE_HALF_W
KIT_DIR = "E:/LOTRAOMAssets/_export/rivendell/meshes/Outdoor_Assets/"
DRUM_ARCH_DEG = gg.DRUM_ARCH_DEG
INNER_PIER_X = (-20.0 / 3.0, 20.0 / 3.0)
GATE_MAT = len(W.MAT_NAMES)   # the placeholder gates carry the one material after the kit's five


class GBuild(Build):
    """The wall's Build with two additions. `shifted(dy)` moves every vertex made inside it by dy along Y, so the
    wall's own part builders (build_parapet) can make a part that stands on the facade. loop_colours multiplies
    the wall's tone by gg.passage_soot of each loop, so the hall and the door tunnels carry soot and damp."""

    def __init__(self, u0=W.X0):
        super().__init__(u0)
        self.dy = 0.0

    def vert(self, p):
        return self.bm.verts.new((p[0], p[1] + self.dy, p[2]))

    @contextmanager
    def shifted(self, dy):
        old, self.dy = self.dy, dy
        try:
            yield self
        finally:
            self.dy = old

    def ruled(self, faces, rule, mat):
        """Faces of material `mat` whose UV is rule(vertex, face), so that a vertex made by cutting a face into cells
        gets the right value."""
        for f in faces:
            f.material_index = mat
            self.pend.append((f, rule))

    def loop_colours(self):
        out = super().loop_colours()
        i = 0
        for f in self.bm.faces:
            for loop in f.loops:
                co = loop.vert.co
                s = gg.passage_soot(co.x, co.y, co.z)
                for k in range(3):
                    out[i + k] *= s[k]
                i += 4
        return out


# ---- 2D outlines (the first version's) ---------------------------------------------------------
lancet = gg.lancet
opening_loop = gg.opening_loop


def along_y(y0, y1):
    return lambda u, v, k: Vector((u, y1 if k else y0, v))


def vertical(z0, z1):
    return lambda u, v, k: Vector((u, v, z1 if k else z0))


def prism_y(B, prof_xz, y0, y1):
    """A closed solid from an (x, z) profile extruded along Y."""
    n = len(prof_xz)
    a = [B.vert((x, y0, z)) for x, z in prof_xz]
    c = [B.vert((x, y1, z)) for x, z in prof_xz]
    faces = [B.face(a[i], a[(i + 1) % n], c[(i + 1) % n], c[i]) for i in range(n)]
    for t in W.tess([prof_xz]):
        faces.append(B.face(*[a[k] for k in t]))
        faces.append(B.face(*[c[k] for k in t]))
    return B.seal(faces)


# ---- sweeps: a profile carried along a path ---------------------------------------------------
def cum_len(points):
    s = [0.0]
    for a, b in zip(points, points[1:]):
        s.append(s[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    return s


def facet_map(f, uvs, cum_of, plinth):
    """The UV of a quad of a "world" sweep. A battered face (the plinth cone of a drum) is mapped by itself
    (gg.facet_uv), so that it is isometric: U from the length of each ring shears it, because the ring is longer at
    the foot. A horizontal face (the foot or the top of a member, which nobody sees) takes the wall's box rule, which
    is isometric on it. A vertical face keeps the values of its rings."""
    n = f.normal
    if abs(n.z) < 0.05:
        return uvs
    if abs(n.z) > 0.99:
        return {v: jp.box_uv(tuple(n), tuple(v.co), UV_X0) for v in f.verts}
    c = f.calc_center_median()
    s_c = sum(cum_of[v] for v in f.verts) / len(f.verts)
    return {v: gg.facet_uv(tuple(n), tuple(v.co), tuple(c), s_c, plinth) for v in f.verts}


def sweep(B, path, prof, mode, strip="S2", first_n=None, last_n=None, mat=RUBBLE, smooth=False, u_mode="start", sym_caps=False):
    """A closed shell: the closed (d, z) profile `prof` (d outward from the path) swept along `path`, with flat
    caps. mode "trim": U = TD times the length along the path, V is the wall's box rule (gg.edge_v): the whole strip
    `strip` across each face of the member.
    mode "world": U = TD times the length along each ring (so the density holds on a cone), V by the wall's own
    rules for a face of the material `mat` (gg.sweep_edge_v): the plinth's bed joint at its top edge for RUBBLE,
    V = TD z (TD z / tz on a batter) for stone; the faces take the material `mat`.
    `u_mode`: "start" measures U from the first point of the path, "end" from the last one, "mirror" (mode "trim") from
    the middle of the path (gg.mirrored_u), so that the two members of a mirror pair carry the same U at mirror points.
    With `sym_caps` (mode "trim") the cut ends are mapped on |x|, so that the cut ends of a mirror pair agree.
    With `smooth` the quads are shaded smooth along the path: the edges along the path are sharp (the corners of the
    profile), and so is every ring where the path turns by more than SMOOTH_DEG (gg.sharp_rings) and the two end rings."""
    norms = gg.mitre_normals(path, first_n, last_n)
    m = len(prof)
    rings = [[B.vert((p[0] + n[0] * d, p[1] + n[1] * d, z)) for d, z in prof] for p, n in zip(path, norms)]
    v0, v1 = jp.STRIPS[strip]
    if mode == "trim":
        cum = cum_len(path)
        cum = {"start": cum, "end": [cum[-1] - c for c in cum], "mirror": gg.mirrored_u(cum)}[u_mode]
        us = [cum] * m
    else:
        us = [cum_len([(r[k].co.x, r[k].co.y) for r in rings]) for k in range(m)]
        if u_mode == "end":
            us = [[c[-1] - x for x in c] for c in us]
    quads = {}
    faces = []
    for i in range(len(path) - 1):
        for k in range(m):
            k2 = (k + 1) % m
            f = B.face(rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k])
            if f is None:
                continue
            faces.append(f)
            if mode == "trim":
                va, vb = gg.edge_v(prof, k, v0, v1)   # the wall's box rule: the whole strip across each face
            else:
                va, vb = gg.sweep_edge_v(prof, k, mat == RUBBLE)
            quads[f] = {rings[i][k]: (jp.TD * us[k][i], va), rings[i][k2]: (jp.TD * us[k2][i], vb),
                        rings[i + 1][k2]: (jp.TD * us[k2][i + 1], vb), rings[i + 1][k]: (jp.TD * us[k][i + 1], va)}
    caps = [B.face(*rings[0]), B.face(*rings[-1])]
    kept = B.seal(faces + caps)
    cum_of = {rings[i][k]: us[k][i] for i in range(len(path)) for k in range(m)} if mode != "trim" else {}
    if smooth:
        B.smooth([f for f in kept if f in quads])
        sharp_ring = set(gg.sharp_rings(path, SMOOTH_DEG)) | {0, len(path) - 1}
        edges = [B.edge(rings[i][k], rings[i + 1][k]) for i in range(len(path) - 1) for k in range(m)]
        edges += [B.edge(rings[i][k], rings[i][(k + 1) % m]) for i in sharp_ring for k in range(m)]
        B.sharp(edges)
    cap_faces = []
    for f in kept:
        if f in quads:
            if mode != "trim":
                quads[f] = facet_map(f, quads[f], cum_of, mat == RUBBLE)
            B.direct(f, quads[f])
            if mode != "trim":
                f.material_index = mat
        else:
            cap_faces.append(f)
    if mode == "trim" and sym_caps:   # the two cut ends of a mirror pair take the same U (|x| in place of x)
        lo, hi = W.bounds(cap_faces)
        B.mapped(cap_faces, lambda v, f, s=strip, l=lo, h=hi, u0=B.u0:
                 jp.trim_uv(tuple(f.normal), (abs(v.co.x), v.co.y, v.co.z), s, 0, tuple(l), tuple(h), u0))
    elif mode == "trim":
        B.trim(cap_faces, strip, 0)
    else:   # the cut ends of a member swept round a drum lie in diagonal planes: they take the isometric rule
        B.ruled(cap_faces, foundation_rule if mat == RUBBLE else stone_rule, mat)
    return kept


# ---- the body: one stone shell cut by boolean -------------------------------------------------
def cutter_object(name, build):
    B = Build(UV_X0)
    build(B)
    me = bpy.data.meshes.new(name)
    B.bm.to_mesh(me)
    B.bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def core_cut(mats, outline=None, niches=True):
    """The stone body (Z 0 to 12) minus the hall, the two door tunnels, the rock hole shaft and (variant a) the two
    statue niches. `outline` is the footprint (default the drums' one)."""
    B = Build(UV_X0)
    W.prism_loops(B, [outline if outline is not None else gg.core_outline()], vertical(0.0, DECK_Z))
    me = bpy.data.meshes.new("core_tmp")
    B.bm.to_mesh(me)
    B.bm.free()
    me.materials.append(mats[STONE])
    body = bpy.data.objects.new("core_tmp", me)
    bpy.context.scene.collection.objects.link(body)
    door = opening_loop(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, -0.5, DOOR_STEPS)
    cutters = [
        cutter_object("c_hall", lambda b: W.box(b, -HALL_HALF_W, HALL_HALF_W, -HALL_Y, HALL_Y, -0.5, HALL_CEIL)),
        cutter_object("c_door_out", lambda b: W.prism_loops(b, [door], along_y(HALL_Y - 0.5, HALF_D + 1.0))),
        cutter_object("c_door_in", lambda b: W.prism_loops(b, [door], along_y(-HALF_D - 1.0, -HALL_Y + 0.5))),
        cutter_object("c_shaft", lambda b: W.box(b, -HOLE_X, HOLE_X, HOLE_Y0, HOLE_Y1, HALL_CEIL - 0.6, DECK_Z + 1.0)),
    ]
    niche = opening_loop(NICHE_HALF_W, 3.9, 1.8, 1.25, DOOR_STEPS)   # the narrow niche: 22 steps an arc, so the spring is smooth
    for s in ((-1, 1) if niches else ()):
        cutters.append(cutter_object(f"c_niche{s}", lambda b, s=s: W.prism_loops(
            b, [niche], lambda u, v, k: Vector((s * STATUE_X + u, FACADE_Y - 0.5 if k == 0 else FACADE_Y + 0.5, v)))))
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
    bm.normal_update()
    for f in bm.faces:
        f.material_index = STONE
    smooth_drums(bm)
    return bm


def drum_face(f):
    """True for a vertical face whose corners all lie on a drum's circle (the cylinder of a drum, one facet)."""
    if abs(f.normal.z) >= 0.05:
        return False
    return any(all(abs(math.hypot(l.vert.co.x - s * CX, l.vert.co.y - CY) - R) < 0.03 for l in f.loops) for s in (-1, 1))


def arc_face(f):
    """True for a face of a curve cut along Y (the soffit of a door tunnel, the arch of a niche): its normal lies in the (x, z)
    plane and leans away from both axes."""
    n = f.normal
    return abs(n.y) < 0.02 and abs(n.x) > 0.02 and abs(n.z) > 0.02


def smooth_drums(bm):
    """Shade the curved faces of the stone body as round surfaces: the facets of each drum and the arcs of the door tunnels and
    the niches. A smooth face is shaded smooth along the curve. An edge is sharp where a smooth face meets a face that is
    not (the deck, the foot, the facade, the stub, a jamb) or where two smooth faces turn by more than SMOOTH_DEG."""
    drum = {f for f in bm.faces if drum_face(f)}
    curved = drum | {f for f in bm.faces if arc_face(f)}
    for f in curved:
        f.smooth = True
    for e in bm.edges:
        faces = list(e.link_faces)
        if not any(f in curved for f in faces) or len(faces) != 2:
            continue
        a, b = faces
        if a in drum or b in drum:
            e.smooth = not (a not in drum or b not in drum)
        else:
            e.smooth = a in curved and b in curved and a.normal.angle(b.normal) <= math.radians(SMOOTH_DEG)


def smooth_sides(B, faces, side_axis_y=True):
    """Shade the curved side faces of a prism cut along Y (a rib of the hall, the foundation round a drum) smooth along the
    curve: the faces whose normal is square to Y (or to Z for the foundation). A hard edge stays where a side face meets
    a face that is not smooth or where two turn by more than SMOOTH_DEG."""
    faces = [f for f in faces if f is not None and f.is_valid]
    side = [f for f in faces if (abs(f.normal.y) < 0.05 if side_axis_y else abs(f.normal.z) < 0.05)]
    B.smooth(side)
    inside = set(side)
    edges = []
    for f in side:
        for e in f.edges:
            others = [g for g in e.link_faces if g is not f]
            if len(others) == 1 and (others[0] not in inside or f.normal.angle(others[0].normal) > math.radians(SMOOTH_DEG)):
                edges.append(e)
    B.sharp(edges)


def core_uv(v, f):
    """The UV rule of a face of the stone body, a function of the vertex and the face so that a vertex made by
    cutting the face into cells gets the right value: a drum takes a cylinder map, every other face the pinned
    world rule."""
    co = v.co
    if abs(f.normal.z) < 0.05:
        for s in (-1, 1):
            if all(abs(math.hypot(l.vert.co.x - s * CX, l.vert.co.y - CY) - R) < 0.03 for l in f.loops):
                return gg.cyl_uv(s, co.x, co.y, co.z, R)
    if arc_face(f):   # the arc of a door tunnel or a niche: isometric, so the stone keeps its density round the curve
        return gg.planar_uv(tuple(f.normal), tuple(co))
    return jp.world_uv(tuple(f.normal), tuple(co), UV_X0)


def foundation_rule(v, f):
    """The plinth rule of a face of the foundation: the plinth's bed joint at V 1.3767 on a vertical face, and U along
    the face at TD where the foundation under a drum runs diagonally (gg.foundation_uv)."""
    return gg.foundation_uv(tuple(f.normal), tuple(v.co), UV_X0)


def stone_rule(v, f):
    """The wall's stone rule, with U along the face at TD on a vertical face that runs diagonally."""
    return gg.foundation_uv(tuple(f.normal), tuple(v.co), UV_X0, False)


def planar_rule(v, f):
    """Every face at TD per metre both ways, U along the horizontal of its plane: a loose stone with no courses, a roof."""
    return gg.planar_uv(tuple(f.normal), tuple(v.co))


def merge_bm(B, src, rule=None, whole=False, mirror=False):
    """Copy the faces of `src` into the Build `B`. With a `rule` (a function of vertex and face) the faces are
    mapped by it when B applies its UVs, and cut_cells may cut them. Without one they keep the UVs they have
    in `src`, and with `whole` their shell is never cut (a part that was mapped and moved elsewhere). With `mirror` the copy
    is the mirror image about X = 0: the corners keep their UVs, so the two are an exact mirror pair in geometry and in UV."""
    uv_src = src.loops.layers.uv.active
    rec_src = src.faces.layers.int.get("recess")
    vmap = {v: B.bm.verts.new((-v.co.x, v.co.y, v.co.z) if mirror else v.co) for v in src.verts}
    for f in src.faces:
        nf = B.face(*[vmap[v] for v in (reversed(f.verts) if mirror else f.verts)])
        if nf is None:
            continue
        nf.material_index = f.material_index
        nf.smooth = f.smooth
        if rule is not None:
            B.pend.append((nf, rule))
            continue
        mine = {lp.vert: lp for lp in nf.loops}
        for a in f.loops:
            mine[vmap[a.vert]][B.uvl].uv = a[uv_src].uv
        if rec_src is not None and f[rec_src]:
            nf[B.rec] = 1
        if whole:
            B.keep_whole.add(nf)
    B.sharp([B.edge(vmap[e.verts[0]], vmap[e.verts[1]]) for e in src.edges if not e.smooth])


# ---- the main object -----------------------------------------------------------------------
def drum_trim(B):
    """The wall's lines on both drums: the cone plinth, the plinth cap and the carved band, each on a drum (U measured from
    the facade end on both, so the two drums are mirror images in UV too), and the string course, which is one sweep across
    the whole front (gg.front_course_path): round the left drum, along the facade, round the door frame, along the facade
    and round the right drum, at the height and in the section of the stub's string course. The door ring's tip ends on its
    underside. Every sweep is shaded smooth along its path, with a hard edge only at a real corner."""
    cone = [(-0.1, 0.0), (gg.BATTER, 0.0), (0.0, jp.Z_PLINTH), (-0.1, jp.Z_PLINTH)]
    end = math.radians(gg.drum_band_end_deg())
    radial = (math.cos(end), math.sin(end))
    for side in (-1, 1):
        sweep(B, gg.drum_path(side), cone, "world", mat=RUBBLE, smooth=True)
        for member, strip in ((jp.member("plinth_cap"), "S2"), (jp.member("band"), "S3")):
            prof = [(y - jp.Y_OUT, z) for y, z in member]
            # the stub has the same member: this one stops on its front face, so the two share no volume
            tail_y = jp.Y_OUT + max(d for d, _z in prof)
            if strip == "S3":   # the carved band is as long as the group of blind arches: it ends on the outer leg of the nearest one
                path = gg.drum_band_path(side, tail_y)
                ends = {"first_n": radial} if side > 0 else {"last_n": (-radial[0], radial[1])}
            else:
                path, ends = gg.drum_path(side, tail_y=tail_y), {}
            sweep(B, path, prof, "trim", strip, smooth=True, u_mode="end" if side < 0 else "start", **ends)
    prof = [(y - jp.Y_OUT, z) for y, z in jp.section()["string_course"]]
    sweep(B, gg.front_course_path(jp.Y_OUT + max(d for d, _z in prof)), prof, "trim", "S2", smooth=True, u_mode="mirror")


def drum_arches(B):
    for side in (-1, 1):
        for deg in DRUM_ARCH_DEG:
            th = math.radians(deg)

            def place(u, v, k, side=side, th=th):
                phi = th + u / R
                r = R + W.ARCH_PROUD if k else R - 0.1
                return Vector((side * (CX + r * math.cos(phi)), CY + r * math.sin(phi), W.ARCH_Z0 + v))
            W.arch_frame(B, place, W.ARCH_W, W.ARCH_H, W.ARCH_MOULD, 8, fine=True)


def corbel(B, side):
    """The carved corbel that a stub's inner limbs spring from: a head (the wall's collar) whose flat face takes the feet
    of the limbs, on three moulded steps that taper down to the wall (gg.corbel_tiers). Each tier is a prism on the
    cornice strip, centred on the corbel's line and cut off on the drum's side at XB - 0.05, so it starts inside
    the drum and no end face shows. On the bay side every tier and the strip over the head end at gg.STUB_COLLAR_IN, the inner edge of
    the limbs' feet where they meet the head's top, so the head is flush with the arch that stands on it and no tier or strip
    overhangs it."""
    reach = gg.STUB_COLLAR_IN
    for half_w, prof in gg.corbel_tiers():
        xa, xb = sorted((side * max(gg.STUB_CORBEL_X - half_w, XB - 0.05), side * (gg.STUB_CORBEL_X + min(half_w, reach))))
        B.trim(W.prism_x(B, prof, xa, xb), "S2", 0)
    # the strip above the head tapers into the wall as the pier strip does, so that no flat ledge shows over the head
    xa, xb = sorted((side * max(gg.STUB_CORBEL_X - jp.PIER_HALF, XB - 0.05), side * (gg.STUB_CORBEL_X + min(jp.PIER_HALF, reach))))
    B.world(W.prism_x(B, gg.corbel_taper_profile(), xa, xb), STONE)


def stubs(B, rib_steps=16):
    """The two stubs of real wall, built by the wall's functions. The stub starts on the drum's tangent plane
    (X = XB), has no pier there, and ends in the joint profile (a half pier on the plane X = XE). `rib_steps` is the
    number of sections of each rib limb."""
    for side in (-1, 1):
        if side > 0:
            run = W.Run(gg.STUB_X_IN, XE, (XE,), uv_x0=UV_X0)
        else:
            run = W.Run(-XE, -gg.STUB_X_IN, (-XE,), uv_x0=UV_X0)
        # the carved band runs from the drum to the outer leg of the blind arch (jp.arch_band_spans), as in a bay of the wall
        W.build_body(B, run, band_spans=jp.arch_band_spans(W.arch_centres(run), run.x_lo, run.x_hi, run.piers))
        W.build_pier(B, run, side * XE)
        corbel(B, side)
        for spec in (W.LONG_RIB, W.LOW_RIB):
            # one small complete arch: a limb from the joint pier and one from the corbel, cut on the arch's centre plane
            # (the same apex ring, a true mitre), sections at equal turns of at most jp.RIB_MAX_TURN_DEG
            for xc, sign in ((side * XE, -side), (side * gg.STUB_CORBEL_X, side)):
                W.build_rib(B, xc, sign, spec, rib_steps, clip_x=side * gg.STUB_ARCH_X, half_bay=gg.STUB_RIB_REACH,
                            max_turn_deg=jp.RIB_MAX_TURN_DEG)
        centres = W.arch_centres(run)
        if [round(abs(c), 6) for c in centres] != [round(gg.STUB_ARCH_X, 6)]:
            raise RuntimeError(f"the stub's blind arches are at {centres}, the rib arch is centred on {gg.STUB_ARCH_X}")
        for cx in centres:
            place = (lambda u, v, k, cx=cx: Vector((cx + u, W.RIB_BACK_Y if k == 0 else jp.Y_OUT + W.ARCH_PROUD,
                                                    W.ARCH_Z0 + v)))
            W.arch_frame(B, place, W.ARCH_W, W.ARCH_H, W.ARCH_MOULD, 8, fine=True)


def inner_piers(B):
    """Two of the wall's piers on the inner face (Y -6.2), turned half a circle about Z. Each is built and mapped
    whole in the wall's own frame by the wall's build_pier (niche UVs, collar), then turned and copied in as a
    shell that is never cut."""
    for xc in INNER_PIER_X:
        P = GBuild(UV_X0)
        W.build_pier(P, W.Run(xc - jp.PIER_HALF, xc + jp.PIER_HALF, (xc,), uv_x0=UV_X0), xc)
        P.cut_cells()
        P.apply_uv()
        for v in P.bm.verts:
            v.co.x = 2 * xc - v.co.x
            v.co.y = -v.co.y - (HALF_D - jp.Y_OUT)
        P.bm.normal_update()
        merge_bm(B, P.bm, whole=True)
        P.bm.free()


def inner_face(B, ring_out_x=DOOR_HALF_W + gg.DOOR_RING_M, course_gap=0.0):
    """The wall's inner plinth course and inner string course along the inner face of the body (Y -6.2). The plinth course
    stops at the inner door's ring (`ring_out_x`, the ring's outer edge at the foot); the string course stops
    `course_gap` either side of the centre line, or runs on across the whole face when `course_gap` is 0 (both variants:
    the ring's tip ends on its underside)."""
    sec = jp.section()
    dy = -(HALF_D - jp.Y_OUT)
    gaps = {"inner_plinth_course": ring_out_x, "inner_string_course": course_gap}
    for name, gap in gaps.items():
        prof = [(y + dy, z) for y, z in sec[name]]
        spans = ((-XB, XB),) if gap == 0.0 else ((-XB, -gap), (gap, XB))
        for xa, xb in spans:
            B.trim(W.prism_x(B, prof, xa, xb), "S2", 0)


def hall_work(B, arch_steps=8, rib_steps=24):
    """The three ribs of the hall (each arc in `rib_steps` segments, 4 degrees or less between two) and a blind arch frame
    on each side of each half of the hall (fine frames: 16 steps an arc, shaded smooth)."""
    outer = [(u, RIB_SPRING + v) for u, v in lancet(HALL_HALF_W + 0.15, RIB_RISE, rib_steps)]
    inner = [(u, RIB_SPRING - RIB_DROP + v) for u, v in lancet(HALL_HALF_W + 0.15, RIB_RISE, rib_steps)]
    for y in RIB_YS:
        faces = W.prism_loops(B, [outer + inner[::-1]], along_y(y - RIB_T / 2, y + RIB_T / 2))
        B.trim(faces, "S1", 0)
        smooth_sides(B, faces)
    for s in (-1, 1):
        for yc in (-1.8, 1.8):
            place = (lambda u, v, k, s=s, yc=yc: Vector((s * (HALL_HALF_W - 0.1 if k else HALL_HALF_W + 0.1), yc + u, 0.6 + v)))
            W.arch_frame(B, place, 1.4, 4.2, 0.12, arch_steps, fine=True)


def ring_member(B, outer, y0, y1):
    """The moulded ring round a door opening, a strip of quads along its arc from y0 to y1. Each quad joins a pair of
    points (gg.ring_pairs): one on the opening, one on the outer outline `outer`, matched along the curve, so no quad
    is collapsed. U is the arc length along the ring (gg.ring_u, signed from the apex, equal on both sides), V
    runs across the moulding on strip S4 (the arch moulding): from the opening to the outer edge on the front and
    back faces, from the back to the front on the faces between. One shell, mapped by direct values and never cut."""
    inner = opening_loop(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, 0.0, DOOR_STEPS)
    pairs = gg.ring_pairs(inner, outer, DOOR_SPRING)
    us = [jp.TD * u for u in gg.ring_u(pairs)]
    v0, v1 = jp.STRIPS["S4"]
    ring = {}
    for k, y in ((0, y0), (1, y1)):
        ring[k] = ([B.vert((p[0][0], y, p[0][1])) for p in pairs], [B.vert((p[1][0], y, p[1][1])) for p in pairs])
    quads, curved = [], []
    for i in range(len(pairs) - 1):
        for edge in (0, 1):                                  # the opening, then the outer outline
            a0, a1 = ring[0][edge][i], ring[0][edge][i + 1]
            b0, b1 = ring[1][edge][i], ring[1][edge][i + 1]
            quads.append((B.face(a0, a1, b1, b0), {a0: (us[i], v0), a1: (us[i + 1], v0), b1: (us[i + 1], v1), b0: (us[i], v1)}))
            curved.append(quads[-1][0])
        for k in (0, 1):                                     # the back, then the front
            i0, i1 = ring[k][0][i], ring[k][0][i + 1]
            o0, o1 = ring[k][1][i], ring[k][1][i + 1]
            quads.append((B.face(i0, i1, o1, o0), {i0: (us[i], v0), i1: (us[i + 1], v0), o1: (us[i + 1], v1), o0: (us[i], v1)}))
    for i, edge_u in ((0, 1.0), (len(pairs) - 1, 1.0)):      # the two feet, on the ground: U runs on, away from the apex
        across = jp.TD * abs(pairs[i][1][0] - pairs[i][0][0])
        corners = [(ring[0][0][i], (us[i], v0)), (ring[0][1][i], (us[i] + edge_u * across, v0)),
                   (ring[1][1][i], (us[i] + edge_u * across, v1)), (ring[1][0][i], (us[i], v1))]
        quads.append((B.face(*[c[0] for c in corners]), dict(corners)))
    kept = set(B.seal([q[0] for q in quads]))
    for f, uv in quads:
        if f in kept:
            B.direct(f, uv)
    # the faces along the opening and the outer outline are curved: shade them smooth along the ring, with hard edges at
    # the front and back faces, at the tip (a true mitre) and on the ground
    B.smooth([f for f in curved if f in kept])
    mid = len(pairs) // 2
    edges = []
    for k in (0, 1):
        for edge in (0, 1):
            edges += [B.edge(ring[k][edge][i], ring[k][edge][i + 1]) for i in range(len(pairs) - 1)]
    for i in (0, mid, len(pairs) - 1):
        edges += [B.edge(ring[0][e][i], ring[1][e][i]) for e in (0, 1)]
    B.sharp(edges)


def door_rings(B, outer, proud=RING_PROUD):
    """The moulded rings of the two doors (variant a): each stands `proud` clear of the facade it is set on, less proud than
    the string course that its tip meets, with the tip ending on the underside of that course."""
    ring_member(B, outer, HALF_D, HALF_D + proud)
    ring_member(B, outer, -HALF_D - proud, -HALF_D)


def deck_work(B):
    """The curb on the two long sides of the rock hole and its caps. The piles of loose stones that stood on the deck are gone:
    the scene editor places real ones."""
    for ya, yb in ((HOLE_Y0 - CURB_T, HOLE_Y0), (HOLE_Y1, HOLE_Y1 + CURB_T)):
        B.trim(W.box(B, -HOLE_X, HOLE_X, ya, yb, DECK_Z, DECK_Z + CURB_H), "S1", 0)
    for ya, yb in ((HOLE_Y0 - CURB_T - 0.05, HOLE_Y0), (HOLE_Y1, HOLE_Y1 + CURB_T + 0.05)):
        B.trim(W.box(B, -HOLE_X - 0.05, HOLE_X + 0.05, ya, yb, DECK_Z + CURB_H, DECK_Z + CURB_H + 0.08), "S2", 0)


def pedestals(B):
    h, d, over = PEDESTAL_W / 2, PEDESTAL_D / 2, gg.PEDESTAL_CAP_OVER
    for s in (-1, 1):
        x, y = s * STATUE_X, STATUE_Y
        B.trim(W.box(B, x - h, x + h, y - d, y + d, 0.0, PEDESTAL_H - gg.PEDESTAL_CAP_H), "S1", 0)
        B.trim(W.box(B, x - h - over, x + h + over, y - d - over, y + d + over, PEDESTAL_H - gg.PEDESTAL_CAP_H, PEDESTAL_H), "S2", 0)


def build_main(mats):
    B = GBuild(UV_X0)
    merge_bm(B, core_cut(mats), rule=core_uv)
    foundation = W.prism_loops(B, [gg.foundation_outline()], vertical(jp.Z_FOOT, jp.Z_GROUND))
    B.ruled(foundation, foundation_rule, RUBBLE)
    smooth_sides(B, foundation, side_axis_y=False)
    drum_trim(B)
    drum_arches(B)
    stubs(B)
    inner_piers(B)
    inner_face(B)
    hall_work(B)
    door_rings(B, gg.ring_outline(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, RING_TIP_Z, DOOR_STEPS))
    deck_work(B)
    pedestals(B)
    return W.make_object(BASE, B, mats)


# ---- parapet parts and the rail ---------------------------------------------------------------
def parapet_part(B, x0, x1, dy=0.0):
    """The wall's parapet part (the same section, openings and frieze) over any length from 1.0 m, moved dy along
    Y. The ends are flat and exactly the joint profile."""
    before = set(B.bm.faces)
    with B.shifted(dy):
        W.build_parapet(B, x0, x1)
    stone_faces(B, [f for f in B.bm.faces if f not in before and f[B.rec]])   # a recess shows the wall stone, not the trim


def parapet_return(B, xc, dy_wing, dy_door):
    """The 0.5 m return where the parapet jogs from the door frame back to the facade plane: a slab that fills the 0.5 m
    between two parts, and its cap, flush with the slab in X and as wide in Y as the caps of the two parts
    (gg.parapet_return_profiles), so that no volume is shared with them."""
    slab, cap = gg.parapet_return_profiles(xc)
    B.world(prism_y(B, slab, jp.PARAPET_Y0 + dy_wing, jp.PARAPET_Y1 + dy_door), STONE)
    B.trim(prism_y(B, cap, *gg.parapet_return_y(dy_wing, dy_door)), "S2", 0)


def make_part(name, mats, fn):
    B = GBuild(UV_X0)
    fn(B)
    return W.make_object(name, B, mats)


def ring_runs(circles):
    """Stretches of each drum's rim, as sample indices, that clear the pavilion's floor circle (centre and
    radius in `circles[side]`)."""
    steps = 44
    out = {}
    for side in (-1, 1):
        pcx, pcy, pr = circles[side]
        flags = []
        for i in range(steps + 1):
            th = gg.THETA_FRONT * i / steps
            r = R - 0.2
            px, py = side * (CX + r * math.cos(th)), CY + r * math.sin(th)
            flags.append(math.hypot(px - pcx, py - pcy) > pr + 0.25)
        out[side] = [(a, b) for a, b in gg.runs(flags) if b > a]
    return out, steps


def ring_part(B, side, runs, steps):
    """A parapet ring on the drum rim, built from the wall's section: it starts at the facade plane (flush with the
    wing's end) when its run reaches the facade end and ends against the stub's parapet when it reaches the
    tangent point."""
    sec = jp.section()
    slab = [(y - jp.Y_OUT, z) for y, z in sec["parapet"]]
    cap = [(y - jp.Y_OUT, z) for y, z in sec["parapet_cap"]]
    frieze = [(y - jp.Y_OUT, z) for y, z in sec["frieze"]]
    for a, b in runs:
        arc = [(side * (CX + R * math.cos(gg.THETA_FRONT * i / steps)), CY + R * math.sin(gg.THETA_FRONT * i / steps))
               for i in range(a, b + 1)]
        # travel keeps the outside on the left: the right drum runs from the facade (high theta) to the tangent point
        first_n = last_n = None
        if side > 0:
            arc = arc[::-1]
            if b == steps:
                first_n = (0.0, 1.0)
        elif b == steps:
            last_n = (0.0, 1.0)

        def path(member):
            """The arc, and at the tangent point one more point where this member meets the stub's (gg.ring_tail_y)."""
            if a != 0:
                return list(arc)
            tail = (side * XB, gg.ring_tail_y(member))
            return arc + [tail] if side > 0 else [tail] + arc
        sweep(B, path("slab"), slab, "world", first_n=first_n, last_n=last_n, mat=STONE, smooth=True)
        sweep(B, path("cap"), cap, "trim", "S2", first_n=first_n, last_n=last_n, smooth=True)
        sweep(B, path("frieze"), frieze, "trim", "S5", first_n=first_n, last_n=last_n, smooth=True)


def build_parapets(mats, circles):
    objs = []
    wing_x = gg.arc_right()[0][0]                   # where the drum leaves the facade plane
    dy_wing, dy_door = FACADE_Y - jp.Y_OUT, HALF_D - jp.Y_OUT
    parts = [
        ("m1", lambda B: parapet_part(B, *gg.stub_parapet_span(-1))),
        ("m2", lambda B: parapet_part(B, -wing_x, -PORTAL, dy_wing)),
        ("m3", lambda B: (parapet_part(B, -PORTAL + 0.5, 0.0, dy_door),
                          parapet_return(B, -PORTAL + 0.25, dy_wing, dy_door))),
        ("m4", lambda B: (parapet_part(B, 0.0, PORTAL - 0.5, dy_door),
                          parapet_return(B, PORTAL - 0.25, dy_wing, dy_door))),
        ("m5", lambda B: parapet_part(B, PORTAL, wing_x, dy_wing)),
        ("m6", lambda B: parapet_part(B, *gg.stub_parapet_span(1))),
    ]
    for tag, fn in parts:
        objs.append(make_part(f"{BASE}_{tag}", mats, fn))
    runs, steps = ring_runs(circles)
    for tag, side in (("m7", -1), ("m8", 1)):
        if runs[side]:
            objs.append(make_part(f"{BASE}_{tag}", mats, lambda B, s=side: ring_part(B, s, runs[s], steps)))
    objs.append(make_part(f"{BASE}_m9", mats, build_rail))
    return objs, runs


def build_rail(B):
    """The inner rail as one object: the two stub rails (the wall's own), the rail along the inner edge of the deck
    (Y -6.2) and the two returns at |X| = 10 that join them."""
    sec = jp.section()
    dy = -(HALF_D - jp.Y_OUT)
    W.build_rail(B, XB, XE)
    W.build_rail(B, -XE, -XB)
    B.world(W.prism_x(B, [(y + dy, z) for y, z in sec["rail"]], -XB, XB), STONE)
    B.trim(W.prism_x(B, [(y + dy, z) for y, z in sec["rail_cap"]], -XB, XB), "S2", 0)
    y_a, y_b = gg.rail_return_y()
    for s in (-1, 1):   # the returns: they stand between the inner-edge rail and the stub rail and share no volume with either
        x0, x1 = gg.rail_return_x(s)
        slab = [(x0, jp.RAIL_FOOT), (x1, jp.RAIL_FOOT), (x1, jp.RAIL_BODY_TOP), (x0, jp.RAIL_BODY_TOP)]
        B.world(prism_y(B, slab, y_a, y_b), STONE)
        B.trim(prism_y(B, gg.rail_return_cap(s), y_a + jp.RAIL_CAP_OVER, y_b + jp.RAIL_CAP_OVER), "S2", 0)


# ---- kit parts: pavilions, statues ------------------------------------------------------------
def kit_object(name, folder=KIT_DIR):
    objs = import_fbx(folder + name + ".fbx")
    keep = [o for o in objs if o.name.startswith(name) and not o.name.startswith("UCX")]
    for o in objs:
        if o not in keep:
            bpy.data.objects.remove(o)
    return keep[0]


def kit_measure(ob):
    """The kit part as imported (metres): its floor (the level with the largest upward-facing area, and the centre of
    the box of that level's faces), and `below`, the (x, y) of every vertex under the floor (the platform and its
    stairs) relative to the floor centre."""
    mw = ob.matrix_world
    vs = [mw @ v.co for v in ob.data.vertices]
    zmin = min(v.z for v in vs)
    ups = []
    for p in ob.data.polygons:
        if (mw.to_3x3() @ p.normal).normalized().z > 0.95:
            ups.append((p, (mw @ p.center).z, p.area * mw.to_scale()[0] ** 2))
    area = {}
    for _p, z, a in ups:
        key = round(z / 0.05) * 0.05
        area[key] = area.get(key, 0.0) + a
    key = max(area, key=area.get) if area else zmin
    won = [(z, a) for _p, z, a in ups if abs(round(z / 0.05) * 0.05 - key) < 1e-6]
    floor_z = sum(z * a for z, a in won) / sum(a for _z, a in won) if won else zmin
    # the floor is the faces within 3 cm of that level: the stairs and the platform's rim lie 5 cm lower
    fl = []
    for p, z, _a in ups:
        if abs(z - floor_z) < 0.03:
            fl += [mw @ ob.data.vertices[i].co for i in p.vertices]
    fc = ((min(v.x for v in fl) + max(v.x for v in fl)) / 2, (min(v.y for v in fl) + max(v.y for v in fl)) / 2)
    fr = (max(v.x for v in fl) - min(v.x for v in fl)) / 2
    below = [(v.x - fc[0], v.y - fc[1]) for v in vs if v.z < floor_z - 0.02]
    base = [v for v in vs if v.z < zmin + 0.05]
    bc = ((min(v.x for v in base) + max(v.x for v in base)) / 2, (min(v.y for v in base) + max(v.y for v in base)) / 2)
    return {"zmin": zmin, "floor_z": floor_z, "base_c": bc, "floor_c": fc, "floor_r": fr, "below": below,
            "size": [max(v[k] for v in vs) - min(v[k] for v in vs) for k in range(3)]}


def kit_uv_and_colour(me, strip="S1", floor_z=None):
    """Give a placed kit mesh one UV layer, a box projection at jp.TD on the plain strip (gg.kit_face_uv), and
    the vertex colour "Col" (jp.vertex_tone of the trim kind). The old UV layers are removed; the shapes are not
    touched. U runs from the low end of the piece, like every part of the piece. With `floor_z` the faces of a
    floor (they face up and lie within 3 cm of that height) take the stone material, slot 1, mapped by the deck's
    own rule (jp.world_uv from the piece's low end), so that the courses of the deck run on through the floor.
    Returns the number of floor faces."""
    while len(me.uv_layers):
        me.uv_layers.remove(me.uv_layers[0])
    layer = me.uv_layers.new(name="UVMap")
    colours = [0.0] * (4 * len(me.loops))
    floors = 0
    for p in me.polygons:
        pts = [tuple(me.vertices[i].co) for i in p.vertices]
        floor = floor_z is not None and p.normal.z > 0.95 and all(abs(c[2] - floor_z) < 0.03 for c in pts)
        if floor:
            uvs = [jp.world_uv(tuple(p.normal), c, UV_X0) for c in pts]
            floors += 1
        else:
            uvs = gg.kit_face_uv(tuple(p.normal), pts, strip, UV_X0)
        p.material_index = 1 if floor else 0
        for k, li in enumerate(p.loop_indices):
            layer.data[li].uv = uvs[k]
            x, _y, z = pts[k]
            r, g, b = jp.vertex_tone("stone" if floor else "trim", x - UV_X0, z)
            colours[4 * li:4 * li + 4] = [r, g, b, 1.0]
    attr = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    attr.data.foreach_set("color_srgb", colours)
    me.color_attributes.active_color = attr
    return floors


def place_kit_copy(src, name, mats, matrix, floor_z=None):
    """A copy of a kit part moved by `matrix`, on the trim material (slot 0; with `floor_z` the stone material is slot 1
    and takes the floor faces), with one UV layer and a vertex colour."""
    me = src.data.copy()
    me.transform(matrix)
    me.materials.clear()
    me.materials.append(mats[TRIM])
    if floor_z is not None:
        me.materials.append(mats[STONE])
    for p in me.polygons:
        p.material_index = 0
    me.update()
    floors = kit_uv_and_colour(me, floor_z=floor_z)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob["floor_faces"] = floors
    return ob


def build_pavilions(mats, info):
    """The two kit pavilions. Each stands with its floor centre on the drum's centre and its floor 2 cm over the deck.
    The platform and the stairs of the part lie under the floor, so under the deck, inside the stone body: the
    turn about Z (gg.pavilion_rotation) points the stairs at the deck and keeps every vertex under the floor
    inside the body's outline with a margin. The v02 placing put the stairs outward and sent them through the
    drum."""
    src = kit_object("SM_Small_Gazebo")
    m = kit_measure(src)
    info["pavilion"] = {"source_size_m": [round(x, 3) for x in m["size"]], "floor_radius": round(m["floor_r"], 3),
                        "scale": PAV_SCALE, "uv_layers_source": [u.name for u in src.data.uv_layers],
                        "platform_vertices": len(m["below"]),
                        "floor_z": round(m["floor_z"], 4), "floor_centre": [round(c, 4) for c in m["floor_c"]]}
    out, circles, turns = [], {}, {}
    for side, tag in ((-1, "l"), (1, "r")):
        centre = (side * CX, CY)
        psi = gg.pavilion_rotation(side, m["below"], centre, PAV_SCALE, gg.core_outline(), PAV_MARGIN)
        if psi is None:
            raise RuntimeError(f"no turn of the pavilion keeps its platform inside the body on side {side}")
        turns[str(side)] = round(math.degrees(psi), 2)
        mat = (Matrix.Translation((centre[0], centre[1], DECK_Z + PAV_LIFT)) @ Matrix.Scale(PAV_SCALE, 4) @
               Matrix.Rotation(psi, 4, "Z") @ Matrix.Translation((-m["floor_c"][0], -m["floor_c"][1], -m["floor_z"])) @
               src.matrix_world)
        out.append(place_kit_copy(src, f"{BASE}_pavilion_{tag}", mats, mat, floor_z=DECK_Z + PAV_LIFT))
        circles[side] = (centre[0], centre[1], m["floor_r"] * PAV_SCALE)
    bpy.data.objects.remove(src)
    info["pavilion"]["turn_deg"] = turns
    info["pavilion"]["floor_circles"] = {str(k): [round(x, 3) for x in v] for k, v in circles.items()}
    info["pavilion"]["floor_faces_on_the_deck_stone"] = {o.name: int(o["floor_faces"]) for o in out}
    return out, circles


def build_statues(mats, info):
    src = kit_object("SM_Statue")
    m = kit_measure(src)
    info["statue"] = {"source_size_m": [round(x, 3) for x in m["size"]], "scale": STATUE_SCALE,
                      "uv_layers_source": [u.name for u in src.data.uv_layers]}
    out = []
    for side, tag in ((-1, "l"), (1, "r")):
        mat = (Matrix.Translation((side * STATUE_X, STATUE_Y, PEDESTAL_H)) @ Matrix.Scale(STATUE_SCALE, 4) @
               Matrix.Rotation(math.radians(STATUE_YAW_DEG), 4, "Z") @
               Matrix.Translation((-m["base_c"][0], -m["base_c"][1], -m["zmin"])) @ src.matrix_world)
        out.append(place_kit_copy(src, f"{BASE}_statue_{tag}", mats, mat))
    bpy.data.objects.remove(src)
    return out


def build_gates(mats):
    """Placeholder gates, never exported: a pair of outer leaves and an inner frame with its two leaves."""
    objs = []
    B = Build(UV_X0)
    for s in (-1, 1):
        xa, xb = sorted((0.0, s * 2.35))
        B.world(W.box(B, xa, xb, OUTER_GATE_Y - 0.25, OUTER_GATE_Y + 0.25, 0.0, 6.84), GATE_MAT)
    objs.append(W.make_object("PLACEHOLDER_outer_gate", B, mats))
    B = Build(UV_X0)
    fw, fh, lw, lh = 4.52 / 2, 5.8, 3.47 / 2, 3.8
    frame = [(-fw, 0.0), (-lw, 0.0), (-lw, lh), (lw, lh), (lw, 0.0), (fw, 0.0), (fw, fh), (-fw, fh)]
    B.world(W.prism_loops(B, [frame], along_y(INNER_GATE_Y - 0.25, INNER_GATE_Y + 0.25)), GATE_MAT)
    for s in (-1, 1):
        xa, xb = sorted((0.0, s * lw))
        B.world(W.box(B, xa, xb, INNER_GATE_Y - 0.15, INNER_GATE_Y + 0.15, 0.0, lh), GATE_MAT)
    objs.append(W.make_object("PLACEHOLDER_inner_gate", B, mats))
    return objs


# ---- measurements (the first version's checks, over the new width) -----------------------------
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


def run_len(flags, i0):
    if not flags[i0]:
        return 0
    a = b = i0
    while a > 0 and flags[a - 1]:
        a -= 1
    while b < len(flags) - 1 and flags[b + 1]:
        b += 1
    return b - a + 1


def measure_checks(cs):
    res = {}
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
                              "clear_height_at_jamb_x2.3": min(heights[-2.3], heights[2.3]), "apex_height": heights[0.0]}
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
    ends, _ = cs.hit((-XB + 0.5, -1.8, DECK_Z + 0.1), (1, 0, 0), 2 * XB - 1.0)
    res["hole"]["short_ends_open_ray_clear"] = ends is None
    zs = {}
    for x, y in ((-12.0, 0.0), (-9.0, -4.4), (0.0, -4.4), (9.0, -4.4), (12.0, 0.0), (-8.0, -1.0), (0.0, 2.0), (8.0, -1.0)):
        loc, _ = cs.hit((x, y, 14.0), (0, 0, -1), 30.0)
        zs[f"{x},{y}"] = None if loc is None else round(loc.z, 4)
    res["deck"] = {"z_samples": zs}
    res["through"] = measure_through(cs)
    res["deck_grid"] = measure_walk(cs)
    return res


def measure_through(cs):
    """The door opening seen THROUGH the whole piece: rays along -Y from outside the arch to behind the inner door."""
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
        i = min(range(len(xs)), key=lambda q: abs(xs[q] - x))
        heights[x] = round(run_len(clear[i], 0) * dz, 3)
    return {"width_by_z": widths, "height_by_x": heights, "min_width_z0.5_to_6.95": min(widths.values()),
            "min_height_x_pm2.3": min(heights[-2.3], heights[2.3]),
            "rect_4.7x7.0_fully_clear": all(clear[i][k] for i, x in enumerate(xs) if abs(x) < 2.349
                                            for k, z in enumerate(zs) if z <= 7.0),
            "blockers_in_rect_count_xmin_xmax_zmin_zmax": blockers,
            "cells_blocked_in_rect": sum(1 for i, x in enumerate(xs) if abs(x) < 2.349
                                         for k, z in enumerate(zs) if z <= 7.0 and not clear[i][k])}


def measure_walk(cs):
    """Walkable deck cells (0.1 m): 1.9 m free above and the deck at Z 12.0 below. The walk path runs from the
    left joint end (X -13.333) to the right one: both ends are start cells on the stub's walk (Y -1.0 and the
    breadth of the stub deck) and a 4-connected search says whether the right end is reached. The corridor width
    at each X is the widest reachable run of cells along Y."""
    st = 0.1
    x0, y0 = -XE, -HALF_D - 0.1
    nx, ny = int(round(2 * XE / st)), int(round((2 * HALF_D + 0.2) / st))
    ok = []
    for i in range(nx):
        col = []
        for j in range(ny):
            x, y = x0 + (i + 0.5) * st, y0 + (j + 0.5) * st
            up = cs.hit((x, y, DECK_Z + 0.05), (0, 0, 1), 1.9)[0] is None
            loc, _ = cs.hit((x, y, DECK_Z + 0.5), (0, 0, -1), 1.0)
            col.append(up and loc is not None and abs(loc.z - DECK_Z) < 0.02)
        ok.append(col)
    starts = [(0, j) for j in range(ny) if ok[0][j]]
    seen = set(starts)
    todo = list(starts)
    while todo:
        i, j = todo.pop()
        for a, b in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
            if 0 <= a < nx and 0 <= b < ny and ok[a][b] and (a, b) not in seen:
                seen.add((a, b))
                todo.append((a, b))
    width = []
    for i in range(nx):
        reach = [(i, j) in seen for j in range(ny)]
        best = cur = 0
        for r in reach:
            cur = cur + 1 if r else 0
            best = max(best, cur)
        width.append(best * st)
    end_cells = [j for j in range(ny) if (nx - 1, j) in seen]
    start_y = [round(y0 + (j + 0.5) * st, 2) for j in (starts[0][1], starts[-1][1])] if starts else None
    end_y = [round(y0 + (j + 0.5) * st, 2) for j in (end_cells[0], end_cells[-1])] if end_cells else None
    return {"cell_m": st, "x_range": [round(x0, 3), round(x0 + nx * st, 3)], "left_end_walkable_y": start_y,
            "right_end_reached": bool(end_cells), "right_end_walkable_y": end_y,
            "corridor_width_min": round(min(width), 2), "corridor_width_min_at_x": round(x0 + (width.index(min(width)) + 0.5) * st, 2),
            "corridor_width_samples": {round(x0 + (i + 0.5) * st, 2): round(width[i], 2) for i in range(0, nx, 15)},
            "walkable_cells": len(seen)}


def measure_pavilions(cs, pavs):
    """Where each pavilion stands. Under the deck every vertex must lie inside the stone body's outline; over the deck
    the part must stay inside the parapet ring of its drum. A ray down the drum's centre line, from 1.5 m over the deck, says what the floor is."""
    outline = gg.core_outline()
    res = {}
    for ob in pavs:
        side = -1 if ob.name.endswith("_l") else 1
        co = [ob.matrix_world @ v.co for v in ob.data.vertices]
        below = [v for v in co if v.z < DECK_Z - 0.001]
        above = [v for v in co if v.z >= DECK_Z - 0.001]
        outside = [v for v in below if not gg.point_in_polygon((v.x, v.y), outline)]
        loc, name = cs.hit((side * CX, CY, DECK_Z + 1.5), (0, 0, -1), 5.0)
        res[ob.name] = {"min_z": round(min(v.z for v in co), 4), "max_z": round(max(v.z for v in co), 4),
                        "vertices_under_the_deck": len(below), "under_the_deck_outside_the_body": len(outside),
                        "under_the_deck_min_distance_to_the_outline": round(min(gg.edge_distance((v.x, v.y), outline)
                                                                              for v in below), 4),
                        "over_the_deck_max_radius_from_the_drum_centre": round(max(math.hypot(v.x - side * CX, v.y - CY)
                                                                                  for v in above), 4),
                        "parapet_inner_face_radius": round(R + jp.PARAPET_Y0 - jp.Y_OUT, 4),
                        "floor_z_on_the_drum_axis": None if loc is None else round(loc.z, 4),
                        "floor_object": name}
    return res


def measure_drum_clear(cs):
    """Rays from outside toward each drum's axis at heights from 10.3 to 11.8 over the whole front arc: every first
    hit must be the main body. A pavilion base or anything else that shows through a drum is hit first."""
    bad, total = [], 0
    for side in (-1, 1):
        for i in range(0, 37):
            th = gg.THETA_FRONT * i / 36
            for z in (10.3, 11.0, 11.5, 11.8):
                o = (side * (CX + (R + 3.0) * math.cos(th)), CY + (R + 3.0) * math.sin(th), z)
                loc, name = cs.hit(o, (-side * math.cos(th), -math.sin(th), 0.0), 12.0)
                total += 1
                if name != BASE:
                    bad.append({"side": side, "theta_deg": round(math.degrees(th), 1), "z": z, "hit": name})
    return {"rays": total, "first_hit_is_not_the_body": len(bad), "samples": bad[:12]}


def measure_stub_parapet():
    """The stub parapet's openings and the ring that ends beside it, along X (from the parts' layout)."""
    res = {}
    for side in (-1, 1):
        x0, x1 = sorted(gg.stub_parapet_span(side))
        centres = [x0 + c for c in jp.parapet_openings(x1 - x0)]
        ring_end = side * (XB + 0.10)
        res[str(side)] = {"part_x": [round(x0, 4), round(x1, 4)], "opening_centres": [round(c, 4) for c in centres],
                          "opening_width": jp.OPEN_W, "ring_ends_at_x": round(ring_end, 4),
                          "nearest_opening_edge_to_the_ring": round(min(abs(c - ring_end) for c in centres) - jp.OPEN_W / 2.0, 4)}
    return res


# ---- variant a: the domed gatehouse ----------------------------------------------------------------
def build_a(out, result):
    mats = W.make_materials()
    gate = bpy.data.materials.new("rivendell_gate")
    gate.diffuse_color = W.hex_to_linear("9aa8b0")
    all_mats = mats + [gate]
    info = {}
    pav, circles = build_pavilions(mats, info)
    statues = build_statues(mats, info)
    main_ob = build_main(mats)
    parts, runs = build_parapets(mats, circles)
    info["ring_runs_samples"] = {str(k): v for k, v in runs.items()}
    objs = [main_ob] + parts + pav + statues
    bpy.context.view_layer.update()
    result.update(info)
    result["objects"] = {o.name: W.mesh_stats(o) for o in objs}
    result["uv_check"] = W.uv_check([main_ob] + parts)
    cs = Caster()
    result["checks"] = measure_checks(cs)
    result["checks"]["pavilions"] = measure_pavilions(cs, pav)
    result["checks"]["drum_clear"] = measure_drum_clear(cs)
    result["checks"]["stub_parapet"] = measure_stub_parapet()
    gates = build_gates(all_mats)
    result["objects"].update({o.name: W.mesh_stats(o) for o in gates})
    result["total_tris_asset"] = sum(v["tris"] for k, v in result["objects"].items() if not k.startswith("PLACEHOLDER"))
    fbx, blend = os.path.join(out, BASE + ".fbx"), os.path.join(out, BASE + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    export_fbx(fbx, objs)
    result["fbx"], result["blend"] = fbx, blend
    with open(os.path.join(out, "gatehouse_build_report.json"), "w", encoding="utf-8") as f:
        json.dump({k: result[k] for k in result if k != "status"}, f, indent=1, default=str)


# ---- variant b: octagonal turrets with spires and a gable roof (picture g1) ----------------------------
# The same 20 m body and the same working core as variant a (passage, hall, two door openings 4.7 m wide, the rock hole,
# the deck at Z 12), and the same stubs of real wall. The drums are two octagonal turrets whose +X flat lies in the plane
# |X| = 10, so that the stub's face meets it in one vertical corner (gg.turret_outline). The stone shaft rises to Z 14.6;
# the cornice (a ring sweep on strip S2), the lantern floor, an open lantern of eight columns with an arch between each
# pair, the cap and the spire are a separate object per turret (spire_l, spire_r). Version 4: the shaft stops under the
# cornice and every part of the stack stands on the one below with no shared plane; the spire is on the roof slate
# (Build.roof: U runs round the facets at the eave and jumps once, on the back); the lantern floor and the cap slabs map
# their big faces on the stone, their sides on the cornice strip at TD along their length. A gable roof on four columns
# covers the deck (roof): two slate slabs (the roof UV rule of the shared code), rafters under them, a ridge beam and a
# ridge cap, a fascia on each gable edge, and at each end a stone gable end with a pointed opening and its moulding,
# standing on a tie course. The parapet parts run into the turrets; the members of a turret (plinth cap, band, string
# course) stop on the front face of the stub's twin, so the two share no volume.
# Version 6 (Mike's standard of 2026-11-25: smooth stone, curves that read as curves, exact pairs, nothing proud of the trim it
# meets): ONE string course runs straight across the whole front (gg.front_course_path_b) at the height and in the section of the
# stub's, round the turrets and the door frame, and the door ring is an exact concentric offset of the opening (gg.ring_outline,
# 0.10 proud, its tip on the underside of the course) as on variant a; the crown, the panel and the tympanum frame over the door are
# gone. The carved band is as long as its group of arches and ends flush with them (gg.band_path_b); the arches are those that a
# viewer 10 to 20 m in front of the door sees whole (gg.arch_layout_b): one on the facade with 0.5 m of stone to the door frame, a
# tall and a short one on each turret's front flat. Every arc is at most 4 degrees a segment (fine arch frames, 22 steps in the door
# ring, 16 in the gable's opening) and shaded smooth along the curve, hard at a real corner. The left turret top is the mirror image
# of the right one (mirror_object) and so is the left half of the roof (build_roof): exact pairs in geometry and in UV. The spire's
# slate has V = TD times the slant up the profile (gg.spire_slant: no step where the slope changes), the eight arris ribs taper to
# one point at the tip. MAIN_LIMIT is 12,000 for the fine curves.
# Objects: rivendell_castle_gatehouse_b (body, turret shafts, stubs, passage), _m1 to _m6 (parapet), _m7 (inner rail),
# _spire_l, _spire_r, _roof; PLACEHOLDER_outer_gate and PLACEHOLDER_inner_gate (not exported).
BASE_B ="rivendell_castle_gatehouse_b"
CONE = [(-0.1, 0.0), (gg.BATTER, 0.0), (0.0, jp.Z_PLINTH), (-0.1, jp.Z_PLINTH)]   # the battered plinth, as the drums'
CAP = ((2.3, gg.LANTERN_Z1, gg.LANTERN_Z1 + 0.3), (2.45, gg.LANTERN_Z1 + 0.3, gg.SPIRE_Z0))
COL_BASE_H, COL_CAPITAL_H = 0.15, 0.18     # the base and the capital of a lantern column
MAIN_LIMIT, ASSET_LIMIT = 12000, 34000
CORE_CELL = 1.7


def core_uv_b(v, f):
    """The stone rule of the body of variant b: a turret chamfer takes the diagonal rule, every other face the world rule."""
    return gg.face_uv_b(tuple(f.normal), tuple(v.co), UV_X0)


def face_arch(B, mid, d, n, z0, height, width):
    """A blind pointed arch frame standing 0.12 proud of a vertical face: `mid` the face point under the arch's axis,
    `d` the unit direction along the face, `n` the outward normal. The arcs are fine (3.75 degrees between two segments)
    and shaded smooth; the frame is mapped from its apex on both halves."""
    def place(u, v, k):
        r = W.ARCH_PROUD if k else -0.1
        return Vector((mid[0] + d[0] * u + n[0] * r, mid[1] + d[1] * u + n[1] * r, z0 + v))
    W.arch_frame(B, place, width, height, W.ARCH_MOULD, jp.ARCH_STEPS_FINE, fine=True)


def facade_members(B):
    """The wall's lines round each turret and along the facade between a turret and the door frame, and across the whole front.
    The battered plinth and the plinth cap run from the door frame round the turret to the stub. The carved band is as long as
    the group of arches on it: it ends flush with the outer leg of the facade arch and of the arch on the turret's front flat
    (gg.band_path_b). The string course is ONE sweep across the whole front (gg.front_course_path_b): round the left turret,
    along the facade, round the door frame, along the facade and round the right turret, at the height and in the section of
    the stub's course, with nothing between it and the tip of the door ring. Every sweep is shaded smooth along its path with a
    hard edge at a real corner (a turret's corner, the door frame's). Where a member meets the stub it ends on the front face of
    the stub's own member (tail_y), so the two share no volume. The left sides measure U from the end that the right sides
    start at, so each pair is a mirror pair in UV too."""
    def profile(member):
        prof = [(y - jp.Y_OUT, z) for y, z in member]
        return prof, jp.Y_OUT + max(d for d, _z in prof)
    for side in (-1, 1):
        u_mode = "end" if side < 0 else "start"
        sweep(B, gg.plinth_path_b(side), CONE, "world", mat=RUBBLE, smooth=True, u_mode=u_mode)
        prof, tail_y = profile(jp.member("plinth_cap"))
        sweep(B, gg.plinth_path_b(side, tail_y=tail_y), prof, "trim", "S2", smooth=True, u_mode=u_mode, sym_caps=True)
        prof, _tail_y = profile(jp.member("band"))
        sweep(B, gg.band_path_b(side), prof, "trim", "S3", smooth=True, u_mode=u_mode, sym_caps=True)
    prof, tail_y = profile(jp.section()["string_course"])
    sweep(B, gg.front_course_path_b(tail_y), prof, "trim", "S2", smooth=True, u_mode="mirror", sym_caps=True)


def arches(B):
    """The blind arches that a viewer 10 to 20 m in front of the door sees whole (gg.arch_layout_b): one on the facade
    between the door frame and each turret, and a tall and a short one on the front flat of each turret."""
    for side in (-1, 1):
        for a in gg.arch_layout_b(side):
            face_arch(B, a["mid"], a["d"], a["n"], a["z0"], a["height"], a["width"])


def turret_shafts(B):
    """The stone shaft of each turret over the deck (Z 12 to the foot of the cornice, gg.CORNICE_Z0)."""
    for side in (-1, 1):
        B.ruled(W.prism_loops(B, [gg.turret_outline(side)], vertical(DECK_Z, gg.CORNICE_Z0)), core_uv_b, STONE)


@contextmanager
def cell_size(max_cell):
    """The wall's cell cut with another largest cell (jp.CELL_MAX) for the duration."""
    old = jp.CELL_MAX
    jp.CELL_MAX = max_cell
    try:
        yield
    finally:
        jp.CELL_MAX = old


def stone_body_b(mats):
    """The stone body and the turret shafts as one Build, cut into cells of CORE_CELL (coarser than the wall's 1.1 m: the
    body is not at a joint, so its cell grid need not match the wall's) and mapped. It is merged whole into the main
    mesh."""
    C = GBuild(UV_X0)
    merge_bm(C, core_cut(mats, gg.core_outline_b(), niches=False), rule=core_uv_b)
    turret_shafts(C)
    with cell_size(CORE_CELL):
        C.cut_cells()
    C.apply_uv()
    return C


def build_main_b(mats):
    B = GBuild(UV_X0)
    C = stone_body_b(mats)
    merge_bm(B, C.bm, whole=True)
    C.bm.free()
    B.ruled(W.prism_loops(B, [gg.foundation_outline_b()], vertical(jp.Z_FOOT, jp.Z_GROUND)), foundation_rule, RUBBLE)
    facade_members(B)
    arches(B)
    stubs(B, rib_steps=8)
    inner_piers(B)
    inner_face(B)
    hall_work(B)
    door_rings(B, gg.ring_outline(DOOR_HALF_W, DOOR_SPRING, DOOR_RISE, RING_TIP_Z, DOOR_STEPS))
    deck_work(B)
    return W.make_object(BASE_B, B, mats)


def ring_slab(B, cx, cy, a, z0, z1):
    """A small octagonal slab (the base or the capital of a lantern column), box-mapped on strip S2."""
    B.trim(W.prism_loops(B, [gg.octagon(cx, cy, a)], vertical(z0, z1)), "S2", 0)


def sweep_closed(B, path, prof, strip):
    """A ring: the closed (d, z) profile `prof` swept round the closed path `path` (clockwise, so that d runs outward),
    with no caps. U = TD times the length along the path, V is the strip across each face (gg.edge_v), as a swept
    member of the wall. One shell, mapped by direct values and never cut; U jumps by the length of the ring at the
    first vertex, which is the one seam."""
    norms = gg.mitre_normals_closed(path)
    n, m = len(path), len(prof)
    rings = [[B.vert((p[0] + nn[0] * d, p[1] + nn[1] * d, z)) for d, z in prof] for p, nn in zip(path, norms)]
    us = cum_len(list(path) + [path[0]])
    v0, v1 = jp.STRIPS[strip]
    quads = []
    for i in range(n):
        j = (i + 1) % n
        for k in range(m):
            k2 = (k + 1) % m
            f = B.face(rings[i][k], rings[i][k2], rings[j][k2], rings[j][k])
            if f is None:
                continue
            va, vb = gg.edge_v(prof, k, v0, v1)
            quads.append((f, {rings[i][k]: (jp.TD * us[i], va), rings[i][k2]: (jp.TD * us[i], vb),
                              rings[j][k2]: (jp.TD * us[i + 1], vb), rings[j][k]: (jp.TD * us[i + 1], va)}))
    kept = set(B.seal([q[0] for q in quads]))
    for f, uv in quads:
        if f in kept:
            B.direct(f, uv)


def octagon_slab(B, cx, cy, a, z0, z1, strip="S2", caps=STONE):
    """A closed octagonal slab of apothem `a`. Its eight sides take U = TD times the length round the ring and the
    whole strip `strip` over its height (a member of the wall); its top and bottom, which can be big, are the material
    `caps` on the wall's own rule (the stone: courses at TD, not a strip stretched over metres)."""
    pts = gg.octagon(cx, cy, a)
    lo = [B.vert((x, y, z0)) for x, y in pts]
    hi = [B.vert((x, y, z1)) for x, y in pts]
    us = cum_len(pts + [pts[0]])
    v0, v1 = jp.STRIPS[strip]
    sides = []
    for i in range(8):
        j = (i + 1) % 8
        f = B.face(lo[i], lo[j], hi[j], hi[i])
        sides.append((f, {lo[i]: (jp.TD * us[i], v0), lo[j]: (jp.TD * us[i + 1], v0),
                          hi[j]: (jp.TD * us[i + 1], v1), hi[i]: (jp.TD * us[i], v1)}))
    ends = []
    for ring in (lo, hi):
        ends += [B.face(*[ring[k] for k in t]) for t in W.tess([pts])]
    kept = set(B.seal([f for f, _uv in sides] + ends))
    for f, uv in sides:
        if f in kept:
            B.direct(f, uv)
    B.world([f for f in ends if f in kept], caps)


def spire_solid(B, cx, cy):
    """The octagonal spire as one closed solid from gg.spire_profile(), on the roof slate: U runs round the facets at
    the eave (the eave ring's length is gg.ngon_eave_length) and jumps once, at gg.SPIRE_SEAM on the back; V is the
    distance up the slope. The foot, which nobody sees, is the stone."""
    rings, tip = [], None
    for z, a in gg.spire_profile():
        if a > 0.0:
            rings.append([B.vert((x, y, z)) for x, y in gg.octagon(cx, cy, a)])
        else:
            tip = B.vert((cx, cy, z))
    faces = []
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(8):
            faces.append(B.face(r0[i], r0[(i + 1) % 8], r1[(i + 1) % 8], r1[i]))
    for i in range(8):
        faces.append(B.face(rings[-1][i], rings[-1][(i + 1) % 8], tip))
    foot = B.face(*rings[0][::-1])
    kept = B.seal(faces + [foot])
    B.world([foot], STONE)
    eave = gg.ngon_eave_length(8, gg.spire_profile()[0][1])

    def slate(v, f):
        """U as the roof rule of the shared code gives it (round the facets, one jump at the seam); V is the slant up the
        profile (gg.spire_uv_v), so it has no step where the slope changes."""
        u, _v = jp.roof_uv(tuple(f.normal), tuple(v.co), UV_X0, (cx, cy), eave, gg.SPIRE_SEAM)
        return (u, gg.spire_uv_v(tuple(v.co)))
    B.ruled([f for f in kept if f is not foot], slate, ROOF)
    # the profile is a curve (concave): the facets are shaded smooth along it. The eight arrises, the tip and the eave stay hard.
    B.smooth([f for f in kept if f is not foot])
    hard = [B.edge(rings[j][i], rings[j + 1][i]) for j in range(len(rings) - 1) for i in range(8)]
    hard += [B.edge(rings[-1][i], tip) for i in range(8)]
    hard += [B.edge(rings[0][i], rings[0][(i + 1) % 8]) for i in range(8)]
    B.sharp(hard)


def spire_arrises(B, cx, cy):
    """A thin stone rib along each of the eight arrises of the spire, from the eave to the tip: a triangular section swept along
    the profile, sunk into the spire. It is full size up to a radius of gg.SPIRE_RIB_FULL_R and smaller in proportion to the
    radius above that (gg.spire_rib_section), so that the eight ribs end in one point at the tip and never touch on the way.
    U is the slant up the profile (gg.spire_slant) and V runs across each flank from the sunk edge to the crest on strip S2, so
    that the flanks of two ribs that mirror each other carry the same UV at mirror points."""
    prof = gg.spire_profile()
    v0, v1 = jp.STRIPS["S2"]
    vm = 0.5 * (v0 + v1)
    for k in range(8):
        phi = math.radians(22.5 + 45.0 * k)
        rad, tan = (math.cos(phi), math.sin(phi)), (-math.sin(phi), math.cos(phi))
        secs, us = [], []
        for z, a in prof:
            us.append(jp.TD * gg.spire_slant(z))
            if a <= 0.0:
                secs.append(B.vert((cx, cy, z)))
                continue
            half, sink, out = gg.spire_rib_section(a)
            r = a / math.cos(math.pi / 8)
            x, y = cx + r * rad[0], cy + r * rad[1]
            secs.append([B.vert((x - tan[0] * half - rad[0] * sink, y - tan[1] * half - rad[1] * sink, z)),
                         B.vert((x + tan[0] * half - rad[0] * sink, y + tan[1] * half - rad[1] * sink, z)),
                         B.vert((x + rad[0] * out, y + rad[1] * out, z))])
        # V of the corners (sunk left, sunk right, crest) on a flank; the hidden base across the strip
        flank = {(0, 1): (v0, v1), (1, 2): (v0, v1), (2, 0): (v1, v0)}
        faces = []
        for i in range(len(secs) - 2):
            a, b = secs[i], secs[i + 1]
            for m in range(3):
                m2 = (m + 1) % 3
                f = B.face(a[m], a[m2], b[m2], b[m])
                va, vb = flank[(m, m2)]
                faces.append((f, {a[m]: (us[i], va), a[m2]: (us[i], vb), b[m2]: (us[i + 1], vb), b[m]: (us[i + 1], va)}))
        last, tip = secs[-2], secs[-1]
        for m in range(3):
            m2 = (m + 1) % 3
            f = B.face(last[m], last[m2], tip)
            va, vb = flank[(m, m2)]
            faces.append((f, {last[m]: (us[-2], va), last[m2]: (us[-2], vb), tip: (us[-1], vm)}))
        half0 = gg.spire_rib_section(prof[0][1])[0]
        start = B.face(*secs[0])
        faces.append((start, {secs[0][0]: (us[0] - jp.TD * half0, v0), secs[0][1]: (us[0] + jp.TD * half0, v0), secs[0][2]: (us[0], v1)}))
        kept = set(B.seal([f for f, _uv in faces]))
        for f, uv in faces:
            if f in kept:
                B.direct(f, uv)
        # the rib follows the profile, a curve: shaded smooth along it, hard along its three edges, at the tip and at the eave
        B.smooth(list(kept))
        hard = [B.edge(secs[i][m], secs[i + 1][m]) for i in range(len(secs) - 2) for m in range(3)]
        hard += [B.edge(secs[-2][m], secs[-1]) for m in range(3)]
        hard += [B.edge(secs[0][m], secs[0][(m + 1) % 3]) for m in range(3)]
        B.sharp(hard)


def turret_top(B, side):
    """What stands on a turret's shaft, each part on the one below with no plane in common: the cornice ring (Z 14.6 to
    15.0), the floor of the lantern, eight slender columns each on a base and under a capital with a pointed arch between
    each pair, the cap and the spire."""
    cx, cy = side * gg.TUR_CX, gg.TUR_CY
    sweep_closed(B, gg.ring_path(cx, cy, gg.TUR_A), gg.cornice_profile(), "S2")
    octagon_slab(B, cx, cy, gg.LANTERN_FLOOR_A, gg.SHAFT_TOP_Z, gg.LANTERN_Z0)
    z_base, z_cap = gg.LANTERN_Z0 + COL_BASE_H, gg.LANTERN_Z1 - COL_CAPITAL_H
    for x, y in gg.octagon(cx, cy, gg.LANTERN_A):
        B.trim(W.prism_loops(B, [gg.octagon(x, y, gg.LANTERN_COL_R)], vertical(z_base, z_cap)), "S1", 2)
        ring_slab(B, x, y, gg.LANTERN_COL_R + 0.07, gg.LANTERN_Z0, z_base)     # base
        ring_slab(B, x, y, gg.LANTERN_COL_R + 0.07, z_cap, gg.LANTERN_Z1)      # capital
    for mid, d, n, _ln in gg.poly_edges(gg.octagon(cx, cy, gg.LANTERN_A)):
        def place(u, v, k, mid=mid, d=d, n=n):
            r = 0.07 if k else -0.07
            return Vector((mid[0] + d[0] * u + n[0] * r, mid[1] + d[1] * u + n[1] * r, gg.LANTERN_Z0 - 0.02 + v))
        W.arch_frame(B, place, gg.LANTERN_ARCH_W, gg.LANTERN_ARCH_H, W.ARCH_MOULD, jp.ARCH_STEPS_FINE, fine=True)   # feet 2 cm in the floor
    for a, z0, z1 in CAP:
        octagon_slab(B, cx, cy, a, z0, z1)
    spire_solid(B, cx, cy)
    spire_arrises(B, cx, cy)


def bar(B, p0, p1, w, y0, y1):
    """A straight bar of width w between two points of the (x, z) plane, extruded from y0 to y1."""
    d = Vector((p1[0] - p0[0], p1[1] - p0[1]))
    n = Vector((-d.y, d.x)).normalized() * (w / 2)
    return prism_y(B, [(p0[0] + n.x, p0[1] + n.y), (p1[0] + n.x, p1[1] + n.y), (p1[0] - n.x, p1[1] - n.y),
                       (p0[0] - n.x, p0[1] - n.y)], y0, y1)


def stone_faces(B, faces):
    """Faces of the wall stone, each on the wall's own rule (jp.world_uv) or, where it leans, on the isometric planar
    rule (the box rule would stretch it)."""
    for f in faces:
        if gg.is_sloped(tuple(f.normal)):
            B.ruled([f], planar_rule, STONE)
        else:
            B.world([f], STONE)


def bar_faces(B, faces, p0, p1, width, y0, y1, strip):
    """Faces of a bar in the (x, z) plane on the trim sheet, mapped by gg.bar_uv: U along the bar at TD, the whole strip
    across it."""
    B.mapped(faces, lambda v, f, p0=p0, p1=p1, w=width, a=y0, b=y1, s=strip:
             gg.bar_uv(tuple(f.normal), tuple(v.co), p0, p1, w, a, b, s))


def soffit_rule(v, f):
    """The soffit of a roof slab: one rule for both slopes (gg.planar_uv_sym), so that they are a mirror pair in UV."""
    return gg.planar_uv_sym(tuple(f.normal), tuple(v.co))


def roof_slab(B, side):
    """One slate slab: the top is the roof slate (the roof rule of the shared code: U along the ridge, V up the slope),
    the underside is the stone on the isometric rule (a closed soffit between the rafters), the eave end is a strip
    of the cornice, the two gable ends and the ridge end (hidden by the fascia and the other slab) are the stone."""
    hx, hy = gg.ROOF_HX, gg.ROOF_HY
    faces = prism_y(B, gg.roof_band(side, hx, 0.0, 0.0), -hy, hy)
    top, under, eave, rest = [], [], [], []
    for f in faces:
        n = f.normal
        if n.z > 0.3:
            top.append(f)
        elif n.z < -0.3:
            under.append(f)
        elif abs(n.x) > 0.9 and n.x * side > 0.0:
            eave.append(f)
        else:
            rest.append(f)
    B.world(top, ROOF)
    B.ruled(under, soffit_rule, STONE)
    B.trim(eave, "S2", 1)
    stone_faces(B, rest)


def roof_fascia(B, side, y0, y1):
    """The fascia on one gable edge of one slab: a band that stands 0.12 over the slate and 0.10 under the slab,
    on strip S2 along its length."""
    x_out = gg.ROOF_HX + 0.04
    p0, p1, w = gg.band_axis(side, x_out, 0.12, 0.10)
    bar_faces(B, prism_y(B, gg.roof_band(side, x_out, 0.12, 0.10), y0, y1), p0, p1, w, y0, y1, "S2")


def gable_end(B, e):
    """The stone gable end at the end `e` (+1 outer, -1 inner) of the roof: a triangle that stands on the tie course and
    touches the slab's underside, with a pointed opening cut through it, and a moulding round the opening on each
    face (the cut is 2 cm inside the moulding's inner edge, so the two share no plane)."""
    ya, yb = sorted((e * gg.TYMP_Y_IN, e * gg.TYMP_Y_OUT))
    faces = W.prism_loops(B, [gg.tympanum_outline(), gg.gable_opening(gg.GABLE_STEPS)], along_y(ya, yb))
    stone_faces(B, faces)
    smooth_sides(B, faces)   # the jamb of the pointed opening is shaded smooth along the curve, hard at the point and the sill
    y_a, y_b = sorted((e * gg.TYMP_Y_IN, e * (gg.TYMP_Y_OUT + 0.08)))
    B.trim(W.box(B, -gg.TIE_HALF_X, gg.TIE_HALF_X, y_a, y_b, gg.TIE_Z0, gg.TIE_Z1), "S2", 0)
    z0, height, width = gg.GABLE_ARCH
    for y_face, out in ((e * gg.TYMP_Y_OUT, e), (e * gg.TYMP_Y_IN, -e)):
        def place(u, v, k, y_face=y_face, out=out):
            return Vector((u, y_face + out * (0.12 if k else -0.1), z0 + v))
        W.arch_frame(B, place, width, height, gg.GABLE_ARCH_MOULD, jp.ARCH_STEPS_FINE, fine=True)


def roof_half(B):
    """The right half of the gable roof over the deck (ridge along Y, on four columns; the columns and the roof stand clear of the
    rock hole and its throwers, gg.ROOF_COLUMNS, tested): the two columns on the right, the wall plate under the
    eave, the slate slab with its soffit, the fascia on each gable edge and the rafters. The left half is the mirror image of this
    one (build_roof), so the two slopes are an exact mirror pair in geometry and in UV."""
    hy, ez = gg.ROOF_HY, gg.ROOF_EAVE_Z
    ch = gg.ROOF_COL_HALF
    for x, y in gg.ROOF_COLUMNS:
        if x > 0.0:
            B.trim(W.prism_loops(B, [gg.octagon(x, y, ch)], vertical(DECK_Z + 0.3, ez - 0.65)), "S1", 2)
            B.trim(W.box(B, x - 0.32, x + 0.32, y - 0.32, y + 0.32, DECK_Z, DECK_Z + 0.3), "S2", 0)
            B.trim(W.box(B, x - 0.31, x + 0.31, y - 0.31, y + 0.31, ez - 0.65, ez - 0.35), "S2", 0)
    xa, xb = gg.plate_x_range(1)
    plate = [(xa, ez - 0.35), (xb, ez - 0.35), (xb, gg.roof_under_z(xb) + 0.03), (xa, gg.roof_under_z(xa) + 0.03)]
    B.trim(prism_y(B, plate, -hy + 0.2, hy - 0.2), "S1", 1)
    roof_slab(B, 1)
    for y0, y1 in ((gg.FASCIA_Y0, gg.FASCIA_Y1), (-gg.FASCIA_Y1, -gg.FASCIA_Y0)):
        roof_fascia(B, 1, y0, y1)
    p0, p1 = gg.rafter_axis(1)
    for y in gg.RAFTER_YS:
        y0, y1 = y - gg.RAFTER_T / 2.0, y + gg.RAFTER_T / 2.0
        bar_faces(B, bar(B, p0, p1, gg.RAFTER_W, y0, y1), p0, p1, gg.RAFTER_W, y0, y1, "S1")


def roof_centre(B):
    """What stands on the ridge plane: the ridge beam under the ridge, the ridge cap over it, and a stone gable end with a pointed
    opening at each end. Every part stands on the next without a plane in common (checked by the overlap measure)."""
    rz, t = gg.ROOF_RIDGE_Z, gg.ROOF_T
    half = gg.RIDGE_BEAM_HALF
    B.trim(W.box(B, -half, half, -gg.RIDGE_BEAM_Y, gg.RIDGE_BEAM_Y, rz - t - 0.30, rz - t + 0.02), "S1", 1)
    B.trim(prism_y(B, gg.ridge_cap_profile(), -gg.RIDGE_CAP_Y, gg.RIDGE_CAP_Y), "S2", 1)
    for e in (-1, 1):
        gable_end(B, e)


def object_bm(src, mats):
    """The mesh of a finished object as a bmesh, with the material indices of the whole piece (`mats`)."""
    bm = bmesh.new()
    bm.from_mesh(src.data)
    slot = [mats.index(m) for m in src.data.materials]
    for f in bm.faces:
        f.material_index = slot[f.material_index]
    return bm


def build_roof(mats):
    """The roof object: the right half, its mirror image about X = 0, and the parts on the ridge plane."""
    half = make_part("roof_half_tmp", mats, roof_half)
    bm = object_bm(half, mats)
    B = GBuild(UV_X0)
    merge_bm(B, bm, whole=True)
    merge_bm(B, bm, whole=True, mirror=True)
    bm.free()
    me = half.data
    bpy.data.objects.remove(half)
    bpy.data.meshes.remove(me)
    roof_centre(B)
    return W.make_object(f"{BASE_B}_roof", B, mats)


def build_parapets_b(mats):
    """The parapet in separate parts: the two stub parts (they end inside the turrets), the two wings between the door
    frame and a turret, the two halves of the door-frame part with their returns, and the inner rail."""
    dy_wing, dy_door = FACADE_Y - jp.Y_OUT, HALF_D - jp.Y_OUT
    wing = gg.WING_END_X
    parts = [
        ("m1", lambda B: parapet_part(B, *gg.stub_parapet_span_b(-1))),
        ("m2", lambda B: parapet_part(B, -wing, -PORTAL, dy_wing)),
        ("m3", lambda B: (parapet_part(B, -PORTAL + 0.5, 0.0, dy_door),
                          parapet_return(B, -PORTAL + 0.25, dy_wing, dy_door))),
        ("m4", lambda B: (parapet_part(B, 0.0, PORTAL - 0.5, dy_door),
                          parapet_return(B, PORTAL - 0.25, dy_wing, dy_door))),
        ("m5", lambda B: parapet_part(B, PORTAL, wing, dy_wing)),
        ("m6", lambda B: parapet_part(B, *gg.stub_parapet_span_b(1))),
        ("m7", build_rail),
    ]
    return [make_part(f"{BASE_B}_{tag}", mats, fn) for tag, fn in parts]


def measure_stub_corner(cs):
    """Where a turret meets a stub: rays along X at Y 2.7 (0.2 m in front of the stub's face) from 1 m inside the
    joint toward the turret, at several heights. The first hit should be the turret's flat (X 10) or a member that
    stands proud of it (the corbel, a rib, the band)."""
    res = {}
    for side in (-1, 1):
        rows = {}
        for z in (1.0, 2.0, 5.0, 6.5, 11.0, 11.5, 13.0, 14.0):
            loc, name = cs.hit((side * (XE - 1.0), 2.7, z), (-side, 0.0, 0.0), 6.0)
            rows[str(z)] = None if loc is None else {"x": round(loc.x, 3), "object": name}
        res[str(side)] = rows
    return res


def measure_roof(cs):
    """The roof by ray: from above, the first hit over each sample is the slate (or the ridge cap) at the height of
    the slope; from the deck up, the first hit is a rafter or the slab's underside (the samples stand clear of the
    columns), and the lowest of them over the deck is reported; along the gable's axis a ray through
    both stone gable ends passes through their openings."""
    down, up = {}, {}
    for x in (-3.4, -2.0, 2.0, 3.4):
        for y in (-3.3, 0.65, 2.6):
            loc, name = cs.hit((x, y, 30.0), (0, 0, -1), 40.0)
            down[f"{x},{y}"] = None if loc is None else {"z": round(loc.z, 4), "expected": round(gg.roof_top_z(x), 4),
                                                          "object": name}
            loc, name = cs.hit((x, y, DECK_Z + 0.5), (0, 0, 1), 12.0)
            up[f"{x},{y}"] = None if loc is None else {"z": round(loc.z, 4), "object": name}
    clear = {}
    for z in (16.2, 17.0, 17.8):
        loc, name = cs.hit((0.0, 14.0, z), (0, -1, 0), 40.0)
        clear[str(z)] = {"clear": loc is None, "blocked_by": name}
    roof_hits = [v["z"] for v in up.values() if v and v["object"] == BASE_B + "_roof"]
    return {"from_above": down, "from_the_deck": up, "samples_that_hit_the_roof_from_the_deck": len(roof_hits),
            "lowest_roof_part_over_the_deck_m": round(min(roof_hits) - DECK_Z, 4) if roof_hits else None,
            "through_both_gable_openings": clear}


def measure_stub_parapet_b():
    res = {}
    for side in (-1, 1):
        x0, x1 = sorted(gg.stub_parapet_span_b(side))
        centres = [x0 + c for c in jp.parapet_openings(x1 - x0)]
        res[str(side)] = {"part_x": [round(x0, 4), round(x1, 4)], "opening_centres": [round(c, 4) for c in centres],
                          "opening_width": jp.OPEN_W,
                          "nearest_opening_edge_to_the_turret_flat": round(min(abs(c) for c in centres) - jp.OPEN_W / 2.0 - XB, 4)}
    wing = [gg.PORTAL_HALF_W + c for c in jp.parapet_openings(gg.WING_END_X - gg.PORTAL_HALF_W)]
    res["wing_openings_right"] = [round(c, 4) for c in wing]
    return res


def mirror_object(name, src, mats):
    """The mirror image about X = 0 of the finished object `src`, as an object of its own: the same faces, UVs, smooth faces and
    sharp edges, the materials in the same order, the vertex colour worked out again from the new place."""
    bm = object_bm(src, mats)
    B = GBuild(UV_X0)
    merge_bm(B, bm, whole=True, mirror=True)
    bm.free()
    return W.make_object(name, B, mats, cells=False)


def build_b(out, result):
    mats = W.make_materials()
    gate = bpy.data.materials.new("rivendell_gate")
    gate.diffuse_color = W.hex_to_linear("9aa8b0")
    all_mats = mats + [gate]
    main_ob = build_main_b(mats)
    parts = build_parapets_b(mats)
    right = make_part(f"{BASE_B}_spire_r", mats, lambda B: turret_top(B, 1))
    tops = [mirror_object(f"{BASE_B}_spire_l", right, mats), right]   # the left turret is the exact mirror image of the right
    roof = build_roof(mats)
    objs = [main_ob] + parts + tops + [roof]
    bpy.context.view_layer.update()
    result["objects"] = {o.name: W.mesh_stats(o) for o in objs}
    result["uv_check"] = W.uv_check(objs)
    cs = Caster()
    result["checks"] = measure_checks(cs)
    result["checks"]["stub_corner"] = measure_stub_corner(cs)
    result["checks"]["roof"] = measure_roof(cs)
    result["checks"]["stub_parapet"] = measure_stub_parapet_b()
    gates = build_gates(all_mats)
    result["objects"].update({o.name: W.mesh_stats(o) for o in gates})
    result["total_tris_asset"] = sum(v["tris"] for k, v in result["objects"].items() if not k.startswith("PLACEHOLDER"))
    result["budget"] = {"main_tris": result["objects"][BASE_B]["tris"], "main_limit": MAIN_LIMIT,
                        "asset_tris": result["total_tris_asset"], "asset_limit": ASSET_LIMIT}
    fbx, blend = os.path.join(out, BASE_B + ".fbx"), os.path.join(out, BASE_B + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    export_fbx(fbx, objs)
    result["fbx"], result["blend"] = fbx, blend
    with open(os.path.join(out, "gatehouse_build_report.json"), "w", encoding="utf-8") as f:
        json.dump({k: result[k] for k in result if k != "status"}, f, indent=1, default=str)


# ---- main -----------------------------------------------------------------------------------------
def main():
    args = script_args()
    out = flag_value(args, "--out")
    variant = flag_value(args, "--variant") or "a"
    with job(report_path(out)) as result:
        if variant not in ("a", "b"):
            raise ValueError(f"--variant is a or b, got {variant}")
        os.makedirs(out, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        {"a": build_a, "b": build_b}[variant](out, result)


if __name__ == "__main__":
    main()
