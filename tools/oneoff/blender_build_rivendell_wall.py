"""Build of the Rivendell solid wall, version 6 (picture w1), real assets for a Modding Kit import. Headless:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_build_rivendell_wall.py --
        --out <dir>

Frame (the Gondor control mesh): metres, the wall runs along X from -10 to +10, the outer face is +Y,
Z is up, Z = 0 is the ground line, the deck is at Z 12.0, the foundation goes down to Z -8. The numbers of
the end section, the texel density, the trim strips, the rib specs and the tone of a vertex are in
tools/oneoff/rivendell_joint_profile.py.

Version 6 (2026-11-25, Mike's second corrections): the half pier at a joint plane has no niche (build_pier), so a free
end of the piece is plain; build_rib builds a limb in the frame of a +X limb and mirrors it, so the two limbs of a bay
are exact mirror images in geometry and UV, shares the apex ring of the pair (one mitre, no cap, no overlap), maps U
from the apex and shades the limb smooth with sharp edges only along the moulded section; with fine=True build_wall
cuts the limbs at 3.5 degrees per segment (jp.RIB_MAX_TURN_DEG) and the blind arch frames at jp.ARCH_STEPS_FINE steps
(smooth shaded), and build_all passes it. Without it a call builds as before (steps as given), so another builder keeps
its triangle budget until it asks for the fine parts.

Version 5 (2026-11-25): the band under the blind arches ends at the outer faces of the outer legs of each bay
(build_body takes `band_spans`, build_wall passes jp.arch_band_spans); the pilaster of a pier dies into the wall in
a taper (jp.pier_profile); the niche in the front of a pier is the wall stone with the wall's own UV rule, not the
trim sheet; the big faces are cut on the columns of the bay (jp.column_cuts), so that the streaks of jp.vertex_tone
land on vertices.

Objects: rivendell_castle_wall_20m_a (foundation, plinth, wall body, piers with collars, ribs, blind
arches, string course, and on the inner face a pilaster behind each pier, a plinth course and a string
course), its parapet parts _m1 to _m5 (4 m each, a flat frieze under the cap) and the inner rail _m6.
LODs and collision bodies are not part of this pass. Every shell is built closed. Both ends of every
object are flat and exactly the joint profile, so two copies set end to end meet with no gap.

Every object carries a vertex colour attribute "Col" (the engine multiplies it into the diffuse): the tone
of jp.vertex_tone at every vertex. The large flat faces are cut into cells (Build.cut_cells) so that the
vertices can carry it.

Five materials (MAT_NAMES): the wall stone, the plinth (rubble: the name stays, its texture is large ashlar
now), the trim sheet, the roof slate and the heap (broken stone with no courses). An object keeps only the
slots that its faces use. Build.world(faces, mat) maps a face by the rule of its material (jp.world_uv for
the stone, jp.plinth_uv for the plinth, jp.heap_uv for the heap, jp.roof_uv for a gable roof) and
Build.roof(faces, axis, eave, seam) maps a spire or a cone. make_object splits a face that has three
collinear corners into a fan from a corner that leaves no zero-area triangle (jp.fan_apex).

The builder functions are importable. The breach, the gatehouse and the towers call them: a `Run` names the
stretch of wall to build (x_lo, x_hi, the pier centres inside it, the low end of the whole piece for the
UVs), a `Build` collects one mesh. A gatehouse builds its two stubs with Run(-13.333, -10, (-13.333,),
uv_x0=-13.333) and Run(10, 13.333, (13.333,), uv_x0=-13.333): half a bay of real wall, ending in the
joint profile.

Writes <out>/rivendell_castle_wall_20m_a.blend, .fbx and wall_build_report.json (bounds and triangles per
object, UV checks, the cells added). The Store launcher prints nothing: wait for <out>/_report/DONE.txt
(tools/blender/blender_job.py).
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector, geometry

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
from blender_job import export_fbx, flag_value, job, report_path, script_args  # noqa: E402
import rivendell_joint_profile as jp  # noqa: E402

BASE = "rivendell_castle_wall_20m_a"
MAT_NAMES = ("t_rivendell_wall_stone_a", "t_rivendell_wall_rubble_a", "t_rivendell_wall_trim_a",
             "t_rivendell_roof_slate_a", "t_rivendell_wall_heap_a")
MAT_COLOURS = (jp.STONE_COLOUR, jp.RUBBLE_COLOUR, jp.TRIM_COLOUR, jp.ROOF_COLOUR, jp.HEAP_COLOUR)
STONE, RUBBLE, TRIM, ROOF, HEAP = 0, 1, 2, 3, 4
KINDS = ("stone", "rubble", "trim", "roof", "heap")
# the UV rule of the materials that Build.world maps (the trim has its own calls); a material that is not here
# takes the stone rule
UV_RULES = {STONE: jp.world_uv, RUBBLE: jp.plinth_uv, ROOF: jp.roof_uv, HEAP: jp.heap_uv}
# the slots whose faces are measured against the pinned density (the trim is mapped across its strip)
DENSITY_SLOTS = tuple(n for i, n in enumerate(MAT_NAMES) if i != TRIM)

X0, X1 = -10.0, 10.0
PARTS = ((-10, -6), (-6, -2), (-2, 2), (2, 6), (6, 10))
PIER_HALF = jp.PIER_HALF
PIER_INNER = jp.PILASTER_IN_W / 2.0  # half width of the pilaster on the inner face
ARCH_Z0, ARCH_W, ARCH_H, ARCH_MOULD, ARCH_PROUD = jp.ARCH_Z0, jp.ARCH_W, jp.ARCH_H, jp.ARCH_MOULD, jp.ARCH_PROUD
ARCH_STEP = jp.ARCH_STEP  # blind arches stand at the bay centre and 1.8 m to each side
RIB_BACK_Y = jp.Y_OUT - 0.1
LONG_RIB, LOW_RIB = jp.RIB_LONG, jp.RIB_LOW
OPEN_W, OPEN_SILL, OPEN_H = jp.OPEN_W, jp.OPEN_SILL, jp.OPEN_H
# The raised plates of the parapet are gone (a flat frieze took their place). The names stay for the tower
# builders that still copy the old part and import them.
PANEL_RELIEF = 0.04
PANEL_Z0, PANEL_Z1 = 13.0, 13.85
TRIM_CUT_MIN = 3.5  # a carved member at least this long (or this tall and narrow) is cut into cells too


class Run:
    """A stretch of wall: x_lo to x_hi, the pier centres inside it (a pier on x_lo or x_hi is a half pier,
    the joint passes through its middle) and the low end of the whole piece, where U starts."""

    def __init__(self, x_lo, x_hi, pier_xs, uv_x0=None):
        self.x_lo, self.x_hi = x_lo, x_hi
        self.piers = tuple(pier_xs)
        self.uv_x0 = x_lo if uv_x0 is None else uv_x0

    def on_pier(self, x):
        return any(abs(x - p) < 1e-6 for p in self.piers)


def wall_run():
    return Run(X0, X1, (X0, X0 + jp.BAY, X0 + 2 * jp.BAY, X1))


# ---- geometry plumbing ---------------------------------------------------------------------------
def tess(loops):
    """Triangles (indices into the loops laid end to end) of a polygon with holes."""
    polys, origin, off = [], [], 0
    for k, loop in enumerate(loops):
        n = len(loop)
        area = sum(loop[i][0] * loop[(i + 1) % n][1] - loop[(i + 1) % n][0] * loop[i][1] for i in range(n))
        rev = (area > 0) != (k == 0)
        order = list(range(n))[::-1] if rev else list(range(n))
        polys.append([Vector((loop[i][0], loop[i][1], 0.0)) for i in order])
        origin += [off + i for i in order]
        off += n
    return [tuple(origin[i] for i in t) for t in geometry.tessellate_polygon(polys)]


def bounds(faces):
    vs = [v.co for f in faces for v in f.verts]
    return (Vector((min(p[i] for p in vs) for i in range(3))),
            Vector((max(p[i] for p in vs) for i in range(3))))


pointed_arch = jp.pointed_arch
half_arch = jp.half_arch


def tri_count(bm):
    return sum(len(f.verts) - 2 for f in bm.faces)


class Build:
    """One mesh under construction: faces, per-face UV rules applied at the end, material slots.
    `u0` is the low end of the whole piece in X (U starts at 0 there). `pend` holds (face, rule) pairs; a
    rule maps (vertex, face) to (u, v). cut_cells() may split faces, so before it cuts it writes the index of
    each face's rule into the face layer `rule`, which every piece of a split face keeps."""

    def __init__(self, u0=X0):
        self.bm = bmesh.new()
        self.uvl = self.bm.loops.layers.uv.new("UVMap")
        self.rule = self.bm.faces.layers.int.new("rule")
        self.rec = self.bm.faces.layers.int.new("recess")
        self.shell = self.bm.faces.layers.int.new("shell")
        self.hid = self.bm.faces.layers.int.new("hidden")
        self.pend = []
        self.u0 = u0
        self.cut_done = False
        self.keep_whole = set()  # faces whose rule is a table of its own vertices: their shell is never cut

    def vert(self, p):
        return self.bm.verts.new(p)

    def face(self, *vs):
        try:
            return self.bm.faces.new(vs)
        except ValueError:  # the same face twice: a degenerate loop
            return None

    def seal(self, faces):
        """Orient one closed shell outward; returns its faces."""
        faces = [f for f in faces if f is not None]
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self.bm.normal_update()
        return faces

    def world(self, faces, mat):
        """Faces of material `mat`, mapped by the rule of that material (UV_RULES) at the pinned density: the
        stone rule for the wall stone, the plinth rule (a bed joint at V 1.3767 on the top edge of the plinth)
        for RUBBLE, the box rule for the HEAP, the gable rule for a ROOF."""
        u0 = self.u0
        rule = UV_RULES.get(mat, jp.world_uv)
        for f in faces:
            f.material_index = mat
            self.pend.append((f, lambda v, fc, u0=u0, r=rule: r(tuple(fc.normal), tuple(v.co), u0)))

    def roof(self, faces, axis=None, eave=0.0, seam=math.pi):
        """Faces of the roof slate. With no `axis` a gable rule (jp.roof_uv); for a spire or a cone, `axis` is
        (x, y) of its vertical axis and `eave` the length of the whole eave ring, so that U runs on round the
        facets (see jp.roof_uv; `seam` is the azimuth where U jumps)."""
        u0 = self.u0
        for f in faces:
            f.material_index = ROOF
            self.pend.append((f, lambda v, fc, u0=u0, a=axis, e=eave, s=seam:
                              jp.roof_uv(tuple(fc.normal), tuple(v.co), u0, a, e, s)))

    def trim(self, faces, strip, axis):
        self.trim_boxed(faces, strip, axis, None)

    def trim_boxed(self, faces, strip, axis, box):
        """Carved faces mapped by jp.trim_uv. `box` (lo, hi) replaces the bounding box of the faces for the
        V across the member: a member cut in half by a joint takes the box of the whole member, so that
        the two halves carry the same V at the joint plane. (trim() is the call with no box; a subclass may
        override it, as the tower's TBuild does, so no new argument goes through it.)"""
        if not faces:
            return
        lo, hi = box if box is not None else bounds(faces)
        u0 = self.u0
        for f in faces:
            f.material_index = TRIM
            self.pend.append((f, lambda v, fc, s=strip, a=axis, l=lo, h=hi, u0=u0:
                              jp.trim_uv(tuple(fc.normal), tuple(v.co), s, a, tuple(l), tuple(h), u0)))

    def direct(self, face, uvs):
        """`uvs` maps each vertex of the face to its (u, v)."""
        face.material_index = TRIM
        self.keep_whole.add(face)
        self.pend.append((face, lambda v, fc, m=uvs: m[v]))

    def mapped(self, faces, fn):
        """Trim faces whose UV is a function fn(vertex, face) of the vertex position, so that a vertex made by
        cutting the face gets the right value."""
        for f in faces:
            f.material_index = TRIM
            self.pend.append((f, fn))

    def smooth(self, faces):
        """Shade these faces smooth: the vertex normals average over their smooth edges."""
        for f in faces:
            if f is not None and f.is_valid:
                f.smooth = True

    def sharp(self, edges):
        """Mark edges sharp: the smooth faces on the two sides of such an edge keep their own normals along it."""
        for e in edges:
            if e is not None and e.is_valid:
                e.smooth = False

    def edge(self, a, b):
        return self.bm.edges.get((a, b))

    def hide(self, faces):
        """Mark faces that cut_cells leaves whole: faces nobody sees (inside the wall, or under another member)
        and faces that a plane would only cut into slivers (the front of a pier round its niche). It also
        leaves every underside whole."""
        for f in faces:
            if f is not None and f.is_valid:
                f[self.hid] = 1

    def recess(self, faces):
        """Mark faces that lie inside a recess: their vertex colour is darker."""
        for f in faces:
            if f is not None and f.is_valid:
                f[self.rec] = 1

    # ---- cells -------------------------------------------------------------------------------------
    def _shells(self):
        """Face to the number of its connected shell (faces joined by shared edges)."""
        seen, n = {}, 0
        for f0 in self.bm.faces:
            if f0 in seen:
                continue
            n += 1
            seen[f0] = n
            stack = [f0]
            while stack:
                f = stack.pop()
                for e in f.edges:
                    for g in e.link_faces:
                        if g not in seen:
                            seen[g] = n
                            stack.append(g)
        return seen

    def cut_cells(self):
        """Cut the large flat faces into cells so that the vertices can carry the colour. Each shell is
        divided along Z into the fewest equal cells of at most jp.CELL_MAX, and along X into the columns of the bay: a
        shell with stone or rubble faces that stands above the ground is cut along X (unless it is narrower than
        jp.CELL_SKIP, as a pier is) and along Z (above the ground only). A carved member at least TRIM_CUT_MIN long is cut along X into
        cells of at most jp.CELL_TRIM; a narrow tall one (a pilaster) along Z. The cuts along X of a stone shell
        stand on the columns of the bay (jp.column_cuts from the piece's low end). A plane cuts the faces of the
        shell that it crosses, except faces marked hidden and undersides, so that no cut adds a vertex that
        shows nothing; a neighbour of a cut edge only gains a vertex on that edge. Returns the triangles
        before and after."""
        bm = self.bm
        for i, (f, _fn) in enumerate(self.pend):
            if f.is_valid:
                f[self.rule] = i + 1
        self.cut_done = True
        before = tri_count(bm)
        bm.normal_update()
        for f in bm.faces:
            if f.normal.z < -0.9:
                f[self.hid] = 1
        ids = self._shells()
        info = {}
        for f, sid in ids.items():
            f[self.shell] = sid
            rec = info.setdefault(sid, {"stone": False, "lo": [1e9] * 3, "hi": [-1e9] * 3})
            if f.material_index in (STONE, RUBBLE):
                rec["stone"] = True
            for v in f.verts:
                for k in range(3):
                    rec["lo"][k] = min(rec["lo"][k], v.co[k])
                    rec["hi"][k] = max(rec["hi"][k], v.co[k])
        whole = {ids[f] for f in self.keep_whole if f.is_valid}
        planes = {}

        def want(axis, cuts, sid):
            for pos in cuts:
                planes.setdefault((axis, round(pos, 6)), set()).add(sid)

        for sid, rec in info.items():
            lo, hi = rec["lo"], rec["hi"]
            if sid in whole:
                continue
            wide, tall = hi[0] - lo[0], hi[2] - lo[2]
            z_lo = max(lo[2], 0.0)
            if rec["stone"]:
                if hi[2] <= 0.0:
                    continue  # underground: nobody sees it
                if wide >= jp.CELL_SKIP:
                    want(0, jp.column_cuts(lo[0], hi[0], self.u0), sid)
                rows = set(jp.even_cuts(z_lo, hi[2], jp.CELL_MAX)) | {r for r in jp.TONE_ROWS if z_lo < r < hi[2]}
                want(2, sorted(rows), sid)
            elif wide >= TRIM_CUT_MIN:
                want(0, jp.even_cuts(lo[0], hi[0], jp.CELL_TRIM), sid)
            elif wide < 1.5 and tall >= TRIM_CUT_MIN:
                want(2, jp.even_cuts(z_lo, hi[2], jp.CELL_MAX), sid)
        eps = 1e-6
        for (axis, pos) in sorted(planes):
            sids = planes[(axis, pos)]
            faces = [f for f in bm.faces if f[self.shell] in sids and not f[self.hid]
                     and min(v.co[axis] for v in f.verts) < pos - eps and max(v.co[axis] for v in f.verts) > pos + eps]
            if not faces:
                continue
            geom = list(faces) + list({e for f in faces for e in f.edges}) + list({v for f in faces for v in f.verts})
            co = (pos, 0.0, 0.0) if axis == 0 else (0.0, 0.0, pos)
            no = (1.0, 0.0, 0.0) if axis == 0 else (0.0, 0.0, 1.0)
            bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=co, plane_no=no)
        bm.normal_update()
        return {"tris_before_cut": before, "tris_after_cut": tri_count(bm), "planes": len(planes)}

    def apply_uv(self):
        self.bm.normal_update()
        if self.cut_done:
            for f in self.bm.faces:
                i = f[self.rule]
                if i:
                    fn = self.pend[i - 1][1]
                    for loop in f.loops:
                        loop[self.uvl].uv = fn(loop.vert, f)
            return
        for f, fn in self.pend:
            if f.is_valid:
                for loop in f.loops:
                    loop[self.uvl].uv = fn(loop.vert, f)

    def split_collinear(self):
        """Split every face of four or more corners that has three collinear corners into triangles fanned from
        a corner that leaves no zero-area triangle (jp.fan_apex), keeping its UVs, material and layers. A cut
        leaves such corners where it meets an edge, and a triangulation from one of them has a triangle of no
        area, which gives the vertices round it a wrong tangent: a dark hairline across the face in the normal
        map. A face that no corner fans cleanly (a concave one) is triangulated by Blender's beauty method
        instead. Call it after apply_uv. Returns (faces split by a fan, faces left to the beauty method)."""
        bm = self.bm
        layers = (self.rule, self.rec, self.shell, self.hid)
        done = 0
        odd = []
        for f in list(bm.faces):
            n = len(f.verts)
            if n < 4:
                continue
            pts = [tuple(v.co) for v in f.verts]
            if not jp.has_collinear_run(pts):
                continue
            k = jp.fan_apex(pts, tuple(f.normal))
            if k is None:
                odd.append(f)
                continue
            verts = list(f.verts)
            uv = {lp.vert: tuple(lp[self.uvl].uv) for lp in f.loops}
            mat, vals, shade = f.material_index, [f[ly] for ly in layers], f.smooth
            order = [verts[(k + i) % n] for i in range(n)]
            made = [self.face(order[0], order[i], order[i + 1]) for i in range(1, n - 1)]
            if any(t is None for t in made):
                raise RuntimeError("fan split: a triangle of the fan exists already")
            for t in made:
                t.material_index = mat
                t.smooth = shade
                for ly, val in zip(layers, vals):
                    t[ly] = val
                for lp in t.loops:
                    lp[self.uvl].uv = uv[lp.vert]
            bm.faces.remove(f)
            done += 1
        if odd:
            bmesh.ops.triangulate(bm, faces=odd, quad_method="BEAUTY", ngon_method="BEAUTY")
        bm.normal_update()
        return done, len(odd)

    def loop_colours(self):
        """The vertex colour of every loop in face order, flat (r, g, b, a): jp.vertex_tone of the face's
        material kind, X from the piece's low end, Z, and the recess mark of the face."""
        out = []
        for f in self.bm.faces:
            kind = KINDS[min(f.material_index, len(KINDS) - 1)]
            rec = 1.0 if f[self.rec] else 0.0
            for loop in f.loops:
                r, g, b = jp.vertex_tone(kind, loop.vert.co.x - self.u0, loop.vert.co.z, rec)
                out += [r, g, b, 1.0]
        return out


def hexa(B, bottom, top):
    """A closed six-sided solid from a bottom ring and a top ring of four (x, y, z) points in the
    box order (-,-) (+,-) (+,+) (-,+). Returns its faces."""
    v = [B.vert(p) for p in list(bottom) + list(top)]
    quads = ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7))
    return B.seal([B.face(*[v[i] for i in q]) for q in quads])


def box(B, x0, x1, y0, y1, z0, z1):
    return hexa(B, ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)),
                ((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)))


def prism_x(B, prof, x0, x1):
    """A closed solid from a (y, z) profile extruded along X."""
    n = len(prof)
    a = [B.vert((x0, y, z)) for y, z in prof]
    c = [B.vert((x1, y, z)) for y, z in prof]
    faces = [B.face(a[i], a[(i + 1) % n], c[(i + 1) % n], c[i]) for i in range(n)]
    for t in tess([prof]):
        faces.append(B.face(*[a[k] for k in t]))
        faces.append(B.face(*[c[k] for k in t]))
    return B.seal(faces)


def prism_loops(B, loops, mapper):
    """A closed solid from a polygon with holes, `loops` as (u, v) points, mapped to 3D by
    mapper(u, v, k), k 0 for the back layer and 1 for the front."""
    layers = [[[B.vert(mapper(u, v, k)) for u, v in loop] for loop in loops] for k in (0, 1)]
    flat = [[v for loop in layer for v in loop] for layer in layers]
    faces = []
    for t in tess(loops):
        for layer in flat:
            faces.append(B.face(*[layer[i] for i in t]))
    for li, loop in enumerate(loops):
        n = len(loop)
        for i in range(n):
            j = (i + 1) % n
            faces.append(B.face(layers[0][li][i], layers[0][li][j], layers[1][li][j], layers[1][li][i]))
    return B.seal(faces)


def arch_frame(B, place, width, height, mould, steps, strip="S4", fine=False):
    """A moulded frame round a pointed arch, standing proud of a plane, a closed shell. place(u, v, k) gives
    the 3D point of a profile point (u across, v up) on the front (k 1) or back (k 0) layer. Built as quad
    strips so U runs along the arch. U is TD times the distance from the apex on both halves (version 6), so the two
    halves of the frame show the same texels at mirror points. With `fine` the arcs have at least jp.ARCH_STEPS_FINE
    steps (3.75 degrees a segment) and the shading is smooth along the frame, with sharp edges along its moulding, at the
    apex and at the feet."""
    if fine:
        steps = max(steps, jp.ARCH_STEPS_FINE)
    outer = pointed_arch(width, height, steps)
    w_in = width - 2 * mould
    spring = height - width * math.sqrt(3.0) / 2.0
    inner = pointed_arch(w_in, spring + w_in * math.sqrt(3.0) / 2.0, steps)
    n = len(outer)
    of = [B.vert(place(u, v, 1)) for u, v in outer]
    ob = [B.vert(place(u, v, 0)) for u, v in outer]
    inf = [B.vert(place(u, v, 1)) for u, v in inner]
    inb = [B.vert(place(u, v, 0)) for u, v in inner]
    s = [0.0]
    for i in range(1, n):
        s.append(s[-1] + math.dist(outer[i], outer[i - 1]))
    apex = steps + 1  # the index of the apex in the outline (see jp.pointed_arch)
    v0, v1 = jp.STRIPS[strip]
    index, faces = {}, []
    for i in range(n - 1):
        j = i + 1
        for vs in ((of[i], of[j], inf[j], inf[i]), (of[i], of[j], ob[j], ob[i]),
                   (inf[i], inf[j], inb[j], inb[i]), (ob[i], ob[j], inb[j], inb[i])):
            f = B.face(*vs)
            faces.append(f)
            index[f] = (i, vs)
    caps = {B.face(of[0], inf[0], inb[0], ob[0]): (of[0], inf[0], inb[0], ob[0], 0),
            B.face(of[-1], inf[-1], inb[-1], ob[-1]): (of[-1], inf[-1], inb[-1], ob[-1], n - 1)}
    faces += list(caps)
    kept = B.seal(faces)
    for f in kept:
        if f in caps:  # the foot of the frame: U runs inward from the outer edge, the same on both feet
            a, b, c, d, k = caps[f]
            u0 = jp.TD * abs(s[k] - s[apex])
            B.direct(f, {a: (u0, v0), b: (u0 + jp.TD * mould, v0), c: (u0 + jp.TD * mould, v1), d: (u0, v1)})
            continue
        if f not in index:
            B.trim([f], strip, 0)
            continue
        i, vs = index[f]
        ua, ub = jp.TD * abs(s[i] - s[apex]), jp.TD * abs(s[i + 1] - s[apex])
        uvs = {vs[0]: (ua, v0), vs[1]: (ub, v0), vs[2]: (ub, v1), vs[3]: (ua, v1)}
        B.direct(f, uvs)
    if fine:
        B.smooth([f for f in kept if f in index])
        edges = []
        for i in range(n - 1):
            edges += [B.edge(of[i], of[i + 1]), B.edge(inf[i], inf[i + 1]), B.edge(ob[i], ob[i + 1]), B.edge(inb[i], inb[i + 1])]
        for k in (0, apex, n - 1):
            edges += [B.edge(of[k], inf[k]), B.edge(of[k], ob[k]), B.edge(inf[k], inb[k]), B.edge(ob[k], inb[k])]
        B.sharp(edges)
    return kept


# ---- the main wall -------------------------------------------------------------------------------
def spans(run, inset=PIER_HALF - 0.05):
    """The stretch where a member that stops at a half pier runs: it starts inside the pier at a joint
    end and runs flat to a cut end that falls mid-bay."""
    a = run.x_lo + (inset if run.on_pier(run.x_lo) else 0.0)
    b = run.x_hi - (inset if run.on_pier(run.x_hi) else 0.0)
    return [(a, b)]


def build_body(B, run, band_spans=None):
    """Foundation and plinth (rubble set), the wall body (stone set), the string course. The inner face has
    real shallow relief: a plinth course and a string course that run the whole length, and a pilaster
    0.9 m wide and 0.10 m proud behind each pier (half of it on each joint). All of them are in jp.section().
    The carved band runs over `band_spans` (a list of (x0, x1); default spans(run): the whole run, ending inside
    the half piers). build_wall passes the width of each bay's group of blind arches."""
    sec = jp.section()
    x0, x1 = run.x_lo, run.x_hi
    B.world(prism_x(B, sec["foundation"], x0, x1), RUBBLE)
    plinth = prism_x(B, sec["plinth"], x0, x1)
    B.world(plinth, RUBBLE)
    B.hide([f for f in plinth if abs(f.normal.z) > 0.9])  # the foot, and the top under the wall body
    B.world(prism_x(B, sec["body"], x0, x1), STONE)  # its underside is skipped by cut_cells
    B.trim(prism_x(B, sec["string_course"], x0, x1), "S2", 0)
    B.trim(prism_x(B, sec["inner_plinth_course"], x0, x1), "S2", 0)
    B.trim(prism_x(B, sec["inner_string_course"], x0, x1), "S2", 0)
    for xc in run.piers:
        a, b = max(xc - PIER_INNER, x0), min(xc + PIER_INNER, x1)
        if b - a > 1e-9:
            whole = ((xc - PIER_INNER, jp.Y_IN - jp.PILASTER_IN_PROUD, jp.INNER_PLINTH_Z1),
                     (xc + PIER_INNER, jp.Y_IN, jp.STRING_Z0))
            B.trim_boxed(prism_x(B, sec["inner_pilaster"], a, b), "S1", 2, whole)
    for a, b in spans(run):
        B.trim(prism_x(B, jp.member("plinth_cap"), a, b), "S2", 0)
    for a, b in (spans(run) if band_spans is None else band_spans):
        B.trim(prism_x(B, jp.member("band"), a, b), "S3", 0)


def niche_uv(B, xc, jambs, floors, backs):
    """UVs of the faces of a niche. All of them are the wall stone with the wall's own rule (jp.world_uv, version 5;
    the trim sheet showed stretched streaks in a niche). The rule is a function of the face and the position only,
    so it is equal on both sides of a joint plane and equal to what the breach builder gives a cut stone face again
    (its reuv_cut). A jamb on the curve of the arch is a box projection, so its density dips below the pinned
    0.45 where the face turns from vertical to horizontal. There is no frame moulding round the opening, so
    nothing of the niche is trim."""
    B.world(list(jambs) + list(floors) + list(backs), STONE)
    B.recess(list(jambs) + list(floors) + list(backs))


def build_pier(B, run, xc):
    """One pier: a 1.2 m wide solid with the battered front, a pointed niche in the front, and the pilaster
    above, and the carved collar where the rib limbs spring. At a joint end the pier is half (0.6 m) and has no
    niche (version 6): the end face is exactly jp.section()['pier'] on the plane X = xc (the collar's too), so a
    free end of a piece is plain and straight. A whole pier inside a piece keeps its niche."""
    xa, xb = max(xc - PIER_HALF, run.x_lo), min(xc + PIER_HALF, run.x_hi)
    cut_a, cut_b = abs(xa - xc) < 1e-9, abs(xb - xc) < 1e-9
    if cut_a and cut_b:
        raise ValueError("a pier needs width")
    plain_p = jp.pier_profile(niche=False)
    A = [B.vert((xa, y, z)) for y, z in plain_p]
    C = [B.vert((xb, y, z)) for y, z in plain_p]
    n = len(plain_p)
    faces = []
    against_wall = []
    for i in range(n):
        if i == 2:  # the front edge: replaced below by the front (a plain quad, or the front with its niche)
            continue
        j = (i + 1) % n
        f = B.face(A[i], A[j], C[j], C[i])
        faces.append(f)
        if i in (0, 6, 7, 8):  # the foot, the back against the wall, the back against the plinth, the foundation face
            against_wall.append(f)
    for ring in (A, C):
        for t in tess([plain_p]):
            faces.append(B.face(*[ring[k] for k in t]))
    top = jp.PIER_TOP_Z
    f_y, back_y = jp.pier_front_y, jp.NICHE_BACK_Y
    z0 = jp.NICHE_Z0
    jambs, floors, backs, fronts = [], [], [], []
    ua, ub = xa - xc, xb - xc
    corners = [A[2], C[2], C[3], A[3]]
    if cut_a or cut_b:
        fronts.append(B.face(*corners))
    else:
        outer2d = [(ua, 0.0), (ub, 0.0), (ub, top), (ua, top)]
        arch = pointed_arch(jp.NICHE_W, jp.NICHE_H, 6)
        F = [B.vert((xc + u, f_y(z0 + v), z0 + v)) for u, v in arch]
        K = [B.vert((xc + u, back_y, z0 + v)) for u, v in arch]
        hole = [(u, z0 + v) for u, v in arch]
        for t in tess([outer2d, hole]):
            fronts.append(B.face(*[(corners + F)[k] for k in t]))
        m = len(arch)
        for i in range(m):
            j = (i + 1) % m
            (floors if i == m - 1 else jambs).append(B.face(F[i], F[j], K[j], K[i]))
        for t in tess([hole]):
            backs.append(B.face(*[K[k] for k in t]))
    niche = [f for f in jambs + floors + backs if f is not None]
    kept = B.seal(faces + fronts + niche)
    inner = set(niche)
    B.world([f for f in kept if f not in inner], STONE)
    B.hide(against_wall)
    B.hide(fronts + niche)  # a plane cuts the fan of the front into slivers, some within 2 cm of a joint plane
    niche_uv(B, xc, [f for f in jambs if f is not None], [f for f in floors if f is not None],
             [f for f in backs if f is not None])
    ca, cb = max(xc - jp.COLLAR_HALF_W, run.x_lo), min(xc + jp.COLLAR_HALF_W, run.x_hi)
    B.trim(prism_x(B, jp.collar_profile(), ca, cb), "S2", 0)


def rib_curve(sign, spec, over_deg, steps, half_bay=None, clip_off=None, max_turn_deg=None):
    """The centre line of a rib (jp.rib_curve): `half_bay` defaults to jp.HALF_BAY, `clip_off` cuts it,
    `max_turn_deg` samples it at equal turns of the tangent."""
    return jp.rib_curve(sign, spec, over_deg, steps, half_bay=half_bay, clip_off=clip_off, max_turn_deg=max_turn_deg)


def outward(faces, inside):
    """Flip each face of `faces` whose normal points toward its point `inside[i]` (a point of the solid that it bounds)."""
    for f, p in zip(faces, inside):
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - p) < 0.0:
            f.normal_flip()


def build_rib(B, xc, sign, spec, steps, clip_x=None, half_bay=None, max_turn_deg=None):
    """One rib limb, a swept chamfered section (jp.rib_section: a flat back, two sides, two chamfers, a front), proud
    of the wall. It grows out of the collar of the pier: a foot that tapers to the apex (jp.rib_width, jp.rib_proud,
    both in one straight line). `clip_x` cuts the rib on the plane X = clip_x, at any X from the far edge of the foot
    out to the apex and beyond (a plane that would cut the foot builds nothing: jp.rib_clip_min); `half_bay` (default
    jp.HALF_BAY) is how far the rib reaches from its pier centre line. The start lies inside the collar and the back
    inside the wall. `steps` is the number of sections; with `max_turn_deg` (jp.RIB_MAX_TURN_DEG) it is the least number
    and the sections stand at equal turns of the tangent.

    A limb is worked out in the frame of a limb on the +X side of its pier and mirrored for sign -1, so the two limbs of
    a bay are exact mirror images, and their sections and UVs correspond one to one (the extrados is the upper side on
    both). U runs from the end of the limb (the apex, jp.rib_u) on both. The end ring of a limb that is cut by a plane is
    shared with the limb that meets it there (the other half of the bay): the two end faces are gone, the pair is one
    closed shell with a true mitre. The shading is smooth along the limb; the edges along the section and the rings at
    the ends are sharp."""
    w0, w1, p0, p1 = spec[3:7]
    off = None if clip_x is None else jp.clip_offset(sign, xc, clip_x)
    centre = None
    if off is not None:
        centre = jp.rib_clip_centre(spec, off, half_bay)  # the far edge of the last section lands on the plane
        if centre is None:
            return  # the plane would cut the foot
    pts = jp.rib_curve(1, spec, 0.0 if clip_x is not None else 2.0, steps, half_bay=half_bay, clip_off=centre,
                       max_turn_deg=max_turn_deg)
    if not pts:
        return
    clipped = centre is not None and abs(pts[-1][0] - centre) < 1e-6
    local, arc, secs = [], [0.0], []
    for i, (u, z, tx, tz) in enumerate(pts):
        n = math.hypot(tx, tz)
        ex, ez = -tz / n, tx / n  # the extrados normal: up, and toward the pier
        t = jp.rib_t(spec, z)
        w = jp.rib_width(w0, w1, t)
        front = jp.Y_OUT + jp.rib_proud(p0, p1, t)
        sec = jp.rib_section(w, front, RIB_BACK_Y)
        ring = [(u + e * ex, y, z + e * ez) for e, y in sec]
        if clipped and i == len(pts) - 1:
            wx, wz = jp.cut_direction(tx / n, tz / n, 1)
            ring = [(off, y, pz + wz * (off - px) / wx) for px, y, pz in ring]
        local.append(ring)
        secs.append(sec)
        if i:
            arc.append(arc[-1] + math.hypot(u - pts[i - 1][0], z - pts[i - 1][1]))
    world = [[(xc + sign * px, y, pz) for px, y, pz in ring] for ring in local]
    ends = B.__dict__.setdefault("rib_ends", [])
    shared = None
    if clipped:
        for ent in ends:
            if all(math.dist(p, q.co) < 1e-5 for p, q in zip(world[-1], ent["verts"])):
                shared = ent
                break
    rings = [[B.vert(p) for p in ring] for ring in world[:-1]]
    if shared is not None:
        rings.append(shared["verts"])
        if shared["cap"] is not None and shared["cap"].is_valid:
            B.bm.faces.remove(shared["cap"])
        shared["cap"] = None
    else:
        rings.append([B.vert(p) for p in world[-1]])
    rows, faces = [], []
    for a, b in zip(rings, rings[1:]):
        row = [B.face(a[m], a[(m + 1) % 6], b[(m + 1) % 6], b[m]) for m in range(6)]
        rows.append(row)
        faces += row
    cen = [Vector((sum(v.co.x for v in r), sum(v.co.y for v in r), sum(v.co.z for v in r))) / 6.0 for r in rings]
    outward([f for row in rows for f in row], [(cen[k] + cen[k + 1]) * 0.5 for k, row in enumerate(rows) for _ in row])
    start = B.face(*rings[0])
    outward([start], [cen[1]])
    end = None
    if shared is None:
        end = B.face(*rings[-1])
        outward([end], [cen[-2]])
        ends.append({"verts": rings[-1], "cap": end})
    B.smooth(faces)
    sharp = []
    for a, b in zip(rings, rings[1:]):
        sharp += [B.edge(a[m], b[m]) for m in range(6)]
    for ring in (rings[0], rings[-1]):
        sharp += [B.edge(ring[m], ring[(m + 1) % 6]) for m in range(6)]
    B.sharp(sharp)
    f0, f1 = jp.STRIPS["S1"]
    total = arc[-1]
    for k, (a, b) in enumerate(zip(rings, rings[1:])):
        u0, u1 = jp.rib_u(arc[k], total), jp.rib_u(arc[k + 1], total)
        for m, f in enumerate(rows[k]):
            m2 = (m + 1) % 6
            va, vb = jp.RIB_FACE_V[m]
            B.direct(f, {a[m]: (u0, va), a[m2]: (u0, vb), b[m2]: (u1, vb), b[m]: (u1, va)})
    for cap, ring, sec in ((start, rings[0], secs[0]), (end, rings[-1], secs[-1])):
        if cap is not None:
            lo, hi = sec[0][1], sec[3][1]
            B.direct(cap, {v: (jp.TD * e, f0 + (f1 - f0) * (y - lo) / (hi - lo)) for v, (e, y) in zip(ring, sec)})


def arch_centres(run):
    """Blind arch centres in the run: three per bay, each whole arch inside x_lo..x_hi."""
    anchor = run.piers[0]
    k0 = math.floor((run.x_lo - anchor) / jp.BAY) - 1
    k1 = math.ceil((run.x_hi - anchor) / jp.BAY) + 1
    out = []
    for k in range(k0, k1 + 1):
        mid = anchor + k * jp.BAY + jp.HALF_BAY
        for i in (-1, 0, 1):
            cx = mid + i * ARCH_STEP
            if cx - ARCH_W / 2 >= run.x_lo - 1e-6 and cx + ARCH_W / 2 <= run.x_hi + 1e-6:
                out.append(cx)
    return sorted(out)


def build_wall(B, run, steps=12, fine=False):
    """The main wall mesh of a run: body, piers, ribs and blind arches. The carved band under the arches is as
    wide as each bay's group of arches (jp.arch_band_spans). With `fine` the rib limbs are cut at jp.RIB_MAX_TURN_DEG
    and the blind arch frames at jp.ARCH_STEPS_FINE steps (smooth shaded); without it `steps` are the sections of a
    limb and a frame has 8 steps an arc, as before."""
    build_body(B, run, jp.arch_band_spans(arch_centres(run), run.x_lo, run.x_hi, run.piers))
    for xc in run.piers:
        build_pier(B, run, xc)
    for xc in run.piers:
        for sign in (-1, 1):
            xend = xc + sign * jp.HALF_BAY
            if xend < run.x_lo - 1e-6 or xend > run.x_hi + 1e-6:
                continue  # that rib belongs to the neighbouring piece
            # Every limb is cut on the plane through the bay centre (version 3 let the two limbs run 2 degrees
            # past it and into each other, which left a spike at the apex): the two limbs end on the same
            # plane with mirror sections, so the apex is clean and pointed.
            for spec in (LONG_RIB, LOW_RIB):
                build_rib(B, xc, sign, spec, steps, clip_x=xend, max_turn_deg=jp.RIB_MAX_TURN_DEG if fine else None)
    for cx in arch_centres(run):
        place = (lambda u, v, k, cx=cx: Vector((cx + u, RIB_BACK_Y if k == 0 else jp.Y_OUT + ARCH_PROUD, ARCH_Z0 + v)))
        arch_frame(B, place, ARCH_W, ARCH_H, ARCH_MOULD, 8, fine=fine)


# ---- parapet parts and the inner rail ------------------------------------------------------------
def build_parapet(B, x0, x1):
    """A parapet part of any length from 1.0 m up: a 0.5 m slab with pointed openings laid out from the
    length (jp.parapet_openings: two in a 4 m part), a continuous frieze band under the cap on the outer
    face (0.05 m proud, strip S5, the blind arcade), and the cap rail. The ends are flat and exactly the
    joint profile."""
    sec = jp.section()
    length = x1 - x0
    centres = [x0 + c for c in jp.parapet_openings(length)]
    outer = [(x0, jp.PARAPET_FOOT), (x1, jp.PARAPET_FOOT), (x1, jp.PARAPET_BODY_TOP), (x0, jp.PARAPET_BODY_TOP)]
    holes = [[(c + u, jp.Z_DECK + OPEN_SILL + v) for u, v in pointed_arch(OPEN_W, OPEN_H, 6)] for c in centres]
    y0, y1 = jp.PARAPET_Y0, jp.PARAPET_Y1
    slab = prism_loops(B, [outer] + holes, lambda u, v, k: Vector((u, y0 if k == 0 else y1, v)))
    flat = [f for f in slab if max(abs(c) for c in f.normal) > 0.99]  # faces and ends of the slab
    B.world(flat, STONE)
    B.hide([f for f in flat if f.normal.z < -0.9])  # the foot sunk into the deck
    jambs = [f for f in slab if f not in set(flat)]  # the curved jambs of the openings
    B.trim(jambs, "S4", 0)
    B.recess(jambs)
    B.trim(prism_x(B, sec["frieze"], x0, x1), "S5", 0)
    B.trim(prism_x(B, sec["parapet_cap"], x0, x1), "S2", 0)


def build_rail(B, x0, x1):
    """The inner rail: a 0.25 m slab and its cap, flat ends."""
    sec = jp.section()
    B.world(prism_x(B, sec["rail"], x0, x1), STONE)
    B.trim(prism_x(B, sec["rail_cap"], x0, x1), "S2", 0)


# ---- scene, objects, measurements ----------------------------------------------------------------
def hex_to_linear(h):
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


def make_materials():
    out = []
    for name, hx in zip(MAT_NAMES, MAT_COLOURS):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = hex_to_linear(hx)
        out.append(m)
    return out


def make_object(name, B, mats, cells=True):
    """The object of a Build: cut the large faces into cells (unless `cells` is False), apply the UV rules,
    write the vertex colour "Col" (jp.vertex_tone) and put the material slots in order."""
    stats = B.cut_cells() if cells else {"tris_before_cut": tri_count(B.bm), "tris_after_cut": tri_count(B.bm), "planes": 0}
    B.apply_uv()
    stats["fan_split"], stats["fan_left"] = B.split_collinear()
    colours = B.loop_colours()
    used = sorted({f.material_index for f in B.bm.faces})
    for f in B.bm.faces:
        f.material_index = used.index(f.material_index)
    me = bpy.data.meshes.new(name)
    B.bm.to_mesh(me)
    for i in used:
        me.materials.append(mats[i])
    B.bm.free()
    if len(colours) != 4 * len(me.loops):
        raise RuntimeError(f"{name}: {len(colours) // 4} colours for {len(me.loops)} loops")
    attr = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    attr.data.foreach_set("color_srgb", colours)
    me.color_attributes.active_color = attr
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for k, v in stats.items():
        ob[k] = v
    return ob


def build_all():
    mats = make_materials()
    objs = []
    B = Build(X0)
    build_wall(B, wall_run(), fine=True)
    objs.append(make_object(BASE, B, mats))
    for i, (x0, x1) in enumerate(PARTS, 1):
        B = Build(X0)
        build_parapet(B, x0, x1)
        objs.append(make_object(f"{BASE}_m{i}", B, mats))
    B = Build(X0)
    build_rail(B, X0, X1)
    objs.append(make_object(f"{BASE}_m6", B, mats))
    return objs


def mesh_stats(ob):
    me = ob.data
    me.calc_loop_triangles()
    co = [v.co for v in me.vertices]
    bm = bmesh.new()
    bm.from_mesh(me)
    open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    bm.free()
    out = {"min": [round(min(p[k] for p in co), 4) for k in range(3)],
           "max": [round(max(p[k] for p in co), 4) for k in range(3)],
           "tris": len(me.loop_triangles), "verts": len(me.vertices), "faces": len(me.polygons),
           "edges_not_two_faces": open_edges, "loose_verts": loose,
           "zero_area_tris": sum(1 for t in me.loop_triangles if t.area < 1e-9),
           "uv_layers": [u.name for u in me.uv_layers], "materials": [m.name for m in me.materials],
           "smooth_faces": sum(1 for p in me.polygons if p.use_smooth),
           "sharp_edges": sum(1 for e in me.edges if e.use_edge_sharp)}
    if "fan_split" in ob:
        out["fan_split_faces"], out["fan_left_faces"] = int(ob["fan_split"]), int(ob["fan_left"])
    if "tris_before_cut" in ob:
        out["tris_before_cut"] = int(ob["tris_before_cut"])
        out["tris_added_by_cells"] = len(me.loop_triangles) - int(ob["tris_before_cut"])
    if len(me.color_attributes):
        attr = me.color_attributes[0]
        n = len(attr.data)
        flat = [0.0] * (4 * n)
        attr.data.foreach_get("color_srgb", flat)
        rgb = [c for i, c in enumerate(flat) if i % 4 != 3]
        out["colour"] = {"name": attr.name, "type": attr.data_type, "domain": attr.domain,
                         "min": round(min(rgb), 4), "max": round(max(rgb), 4)}
    return out


def uv_check(objs):
    """Per object: triangles with a missing or non-finite UV, and the UV scale range of the stone and rubble
    triangles (UV units per metre, the two singular values of every triangle)."""
    res = {}
    for ob in objs:
        me = ob.data
        if not me.uv_layers:
            res[ob.name] = {"uv_layers": 0}
            continue
        uvd = me.uv_layers.active.data
        me.calc_loop_triangles()
        bad = 0
        lo_s, hi_s, off, tri_n = {}, {}, {}, {}
        for tri in me.loop_triangles:
            uvs = [tuple(uvd[k].uv) for k in tri.loops]
            if any(not (math.isfinite(a) and math.isfinite(b)) for a, b in uvs):
                bad += 1
                continue
            slot = me.materials[me.polygons[tri.polygon_index].material_index].name
            if slot not in DENSITY_SLOTS:
                continue
            p = [tuple(me.vertices[i].co) for i in tri.vertices]
            lo, hi = jp.uv_scale_axes(p, uvs)
            if hi == 0.0:
                continue
            lo_s[slot] = min(lo_s.get(slot, 9.0), lo)
            hi_s[slot] = max(hi_s.get(slot, 0.0), hi)
            tri_n[slot] = tri_n.get(slot, 0) + 1
            if lo < jp.TD * 0.95 or hi > jp.TD * 1.05:
                off[slot] = off.get(slot, 0) + 1
        res[ob.name] = {"uv_layers": len(me.uv_layers), "bad_uv_triangles": bad,
                        "scale_min": {k: round(v, 4) for k, v in lo_s.items()},
                        "scale_max": {k: round(v, 4) for k, v in hi_s.items()},
                        "triangles_outside_5_percent": off, "stone_rubble_triangles": tri_n}
    return res


def main():
    args = script_args()
    out = flag_value(args, "--out")
    with job(report_path(out)) as result:
        os.makedirs(out, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs = build_all()
        bpy.context.view_layer.update()
        result["objects"] = {o.name: mesh_stats(o) for o in objs}
        result["total_tris"] = sum(s["tris"] for s in result["objects"].values())
        result["total_tris_before_cells"] = sum(s["tris_before_cut"] for s in result["objects"].values())
        result["total_tris_added_by_cells"] = result["total_tris"] - result["total_tris_before_cells"]
        result["uv_check"] = uv_check(objs)
        fbx = os.path.join(out, BASE + ".fbx")
        blend = os.path.join(out, BASE + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend)
        export_fbx(fbx, objs)
        result["fbx"], result["blend"] = fbx, blend
        with open(os.path.join(out, "wall_build_report.json"), "w", encoding="utf-8") as f:
            json.dump({k: result[k] for k in ("objects", "total_tris", "total_tris_before_cells",
                                              "total_tris_added_by_cells", "uv_check")}, f, indent=1)


if __name__ == "__main__":
    main()
