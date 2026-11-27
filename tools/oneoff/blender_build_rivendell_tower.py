"""Build of the Rivendell wall tower, version 4 (concept t1 rectangular, t2 half-round), a real asset for a Modding
Kit import. Headless:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_build_rivendell_tower.py --
        --variant a --out <dir>

The numbers are in tools/oneoff/rivendell_tower_plan.py, the section, the texel density, the UV rules and the tone of
a vertex in tools/oneoff/rivendell_joint_profile.py, the wall functions (stubs, piers, ribs, arch frames, parapet,
rail, materials, cells, vertex colour) in tools/oneoff/blender_build_rivendell_wall.py. Frame: metres, X along the wall
line, +Y outer, Z up, Z 0 ground.

Objects: <base> (shell: foundation, plinth, outer walls, platform slab, cornice, piers, the two wall stubs),
<base>_int (floors 0, 6, 12 with holes, the rail of every hole, the platform hatch included, and the rail beside every
open side of a flight), <base>_stairs (the three flights, nothing else), <base>_m1 ... (platform parapet parts, then the
two stub parapets, then the two stub rails). No LODs, no bo_ bodies. The stubs end in the joint profile with half a
pier, so a wall or a gatehouse closes on either end with no slit. Every object carries the vertex colour "Col" of the
wall (jp.vertex_tone).

Variant b (the half drum): its shell and its platform parapet are built by shell_b and parapet_builders_b, with the
drum helpers in tools/oneoff/blender_rivendell_tower_drum.py. Its wall stubs start at X +-4.4, inside the drum wall
(the drum falls away from the flat side face, so a stub that ended at X +-5.0 would leave a gap). Interior, stairs,
the stub parapets and rails and the report are shared.

Version 6 against version 5 (2026-11-25, Mike's second corrections, the same set of faults as on the wall):
- Every pier dies at the string course. The corner piers, the back quoins and the drum pilasters start their taper at the
  top of the collar and end in one straight slope at the foot of the string course (plan.corner_pier_parts,
  plan.drum_pier_parts), so nothing of them meets the string course or the cornice from below and stands prouder. The
  string course wraps the corners in one mitred sweep, front and back (string_course_wraps), with no gap at a pier.
- Ribs and arch frames are the wall's fine ones (jp.RIB_MAX_TURN_DEG, jp.ARCH_STEPS_FINE). The two limbs of a bay are cut on
  the plane through its middle and share their end ring (one mitre); on the drum both limbs are built in the frame of
  the bay, so they are exact mirror images about its radial plane (drum_bays). The ribs of the upper tier spring from a
  collar of their own on the wall above the string course (drum.upper_collar, plan.upper_collar_profile).
- The carved band is as wide as the arches that stand on it (plan.band_spans, plan.drum_band_half_angle); the sill under the
  back arches ends flush with the outer legs (plan.SILL_OVER 0).
- The reveal of a door or a parapet opening is the wall stone and a recess (jambs, back_wall, framed_part,
  drum.arc_parapet), never the trim sheet.
- Every face is smooth shaded and every edge over SHARP_DEG is sharp (TBuild.shade_by_angle), so the drum shades as one
  round surface.
- The triangle budget: the curves of the town side (the back arches and the ground door), the narrow window frames, the
  upper ribs, the side doors, the cornice and the string course on the drum, and the foundation are coarser than the
  4 degrees of the rest (the constants at the end of plan.py).

Version 4 against version 3: every face of the plinth material takes the plinth rule (a bed joint at V 1.3767 on the
top edge, Z 3.0: jp.plinth_uv on the flat faces, plan.drum_uv on the drum), so the plinth of the tower and the plinth
of the wall carry the same courses; the masonry of a lifted part (the platform parapet, 5 m up) gets V = TD z, so
every bed joint of the platform runs level with the wall's; the vertex colour cells are coarse on the body (TBuild.
cut_cells, plan.cell_max_for) and fine on the stubs, where the seam test compares them with the wall; tower a has
a sill under the blind arch frames of its back face (plan.sill_profile); the rails of a hatch no longer cross caps
(plan.rail_cap_rect); the platform parapet follows the 0.45 m parapet section of the wall.

A face of the body is dressed in its own local frame (the wall frame: outer face at y = 2.5, X along the face), so
every wall function (arch_frame, build_rib, build_pier, build_parapet, prism_x) works on any face unchanged. The
faces of a framed build are made, cut into cells, mapped and given their colour in that local frame, and the
frame moves them into place last (TBuild.loop_colours), so every UV rule, every cell and every tone is the wall's.
"""
import contextlib
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
from blender_job import export_fbx, flag_value, job, report_path, script_args  # noqa: E402
import blender_build_rivendell_wall as wall  # noqa: E402
import blender_rivendell_tower_drum as drum  # noqa: E402
import rivendell_joint_profile as jp  # noqa: E402
import rivendell_tower_plan as plan  # noqa: E402

STONE, RUBBLE, TRIM = wall.STONE, wall.RUBBLE, wall.TRIM
U0 = plan.PIECE_X0
BX, Y0, Y1 = plan.BODY_X, plan.BODY_Y0, plan.BODY_Y1
IX0, IX1, IY0, IY1 = plan.interior_bounds("a")
OUT = jp.FOUNDATION_Y_OUT - jp.Y_OUT          # 0.6: the foundation and the plinth foot stand this far out
WALL_TOP = plan.PLATFORM_Z - plan.SLAB_T      # 16.7
CORNICE = ((16.0, 16.35, 0.15), (16.35, WALL_TOP, 0.25))
SLAB_PROUD = plan.SLAB_PROUD
PIER_BACK = 0.6                               # a corner pier reaches this far into the body
BACK_TIERS = ((wall.ARCH_Z0, wall.ARCH_W, wall.ARCH_H), (plan.UPPER_TIER_Z0, wall.ARCH_W, 4.4))
FRONT_TIERS = ((wall.ARCH_Z0, plan.FRONT_ARCH_W, wall.ARCH_H), (plan.UPPER_TIER_Z0, plan.FRONT_ARCH_W, 4.4))
ARCH_X = (-2.7, -0.9, 0.9, 2.7)
DOOR_FRAME_PROUD = 0.12
SIDE_LIFT = 0.005                             # a part that crosses another one stands 5 mm higher: no cap top is coplanar
SQ3 = math.sqrt(3.0) / 2.0
STEPS = 12                                    # least steps of a rib limb (the sections stand at equal turns: jp.RIB_MAX_TURN_DEG)
SHARP_DEG = 25.0                              # an edge whose two faces differ by more than this stays sharp, every other is smooth
TURN = jp.RIB_MAX_TURN_DEG  # the turn between two neighbour segments of a rib limb (the wall's value; the gate is 4 degrees)
RECESS_LOG = []  # per mesh: the faces marked as a recess and how many of them carry the trim sheet (must be none)
STEP_TRIS = {}  # name of a shell step -> triangles it added (written into the report)
# which objects are cut into cells for the vertex colour: the weather is on the outside, so the floors, rails and
# stairs inside are not
CELLS = {"shell": True, "int": False, "stairs": False, "parts": True}


# ---- frames and the placed build ------------------------------------------------------------------
class Frame:
    """A turn about Z by rot degrees (0, 90, 180, 270), then a shift. Local frame: the wall frame."""

    def __init__(self, rot, tx, ty, tz=0.0):
        a = math.radians(rot)
        self.rot, self.c, self.s = rot, round(math.cos(a), 12), round(math.sin(a), 12)
        self.t = (tx, ty, tz)

    def point(self, p):
        x, y, z = p[0], p[1], p[2]
        return (self.c * x - self.s * y + self.t[0], self.s * x + self.c * y + self.t[1], z + self.t[2])

    def lifted(self, dz):
        return Frame(self.rot, self.t[0], self.t[1], self.t[2] + dz)


# local x runs along the face; the outer face is at local y = 2.5. East and west faces are centred on Y = 1.0.
# "back_in" dresses the back face as the inner face of the wall (local y = -2.5 is the face, members stand out to -y).
FACES = {"front": Frame(0, 0.0, Y1 - jp.Y_OUT), "back": Frame(180, 0.0, Y0 + jp.Y_OUT),
         "back_in": Frame(0, 0.0, Y0 - jp.Y_IN),
         "east": Frame(270, BX - jp.Y_OUT, plan.FACE_CY), "west": Frame(90, -BX + jp.Y_OUT, plan.FACE_CY)}


class TBuild(wall.Build):
    """A wall Build whose framed faces are made in the local frame of the face they dress. Every face made inside
    `framed(B, frame)` remembers the frame (the face layer `frame`); nothing moves until the build is finished.
    loop_colours (the last step before the mesh is written) returns the wall's colours and then places every
    framed vertex, so the cells, the UV rules and the tones were all worked out in the wall frame."""

    def __init__(self, u0=U0, variant="a"):
        super().__init__(u0)
        self.variant = variant
        self.frame = None
        self.frames = [None]
        self.fr = self.bm.faces.layers.int.new("frame")

    def face(self, *vs):
        f = super().face(*vs)
        if f is not None and self.frame is not None:
            if self.frame not in self.frames:
                self.frames.append(self.frame)
            f[self.fr] = self.frames.index(self.frame)
        return f

    def terrain(self, faces, mat):
        """Stone or plinth faces on or near the drum: U along the arc (blender_rivendell_tower_drum.stone_uv). The
        plinth material takes the plinth rule, so its bed joint is at V jp.PLINTH_V_TOP on the top edge (Z 3.0)."""
        plinth = mat == RUBBLE
        for f in faces:
            f.material_index = mat
            self.pend.append((f, lambda v, fc, u0=self.u0, pl=plinth: drum.stone_uv(v, fc, u0, pl)))

    def ledge(self, faces):
        """Faces of the ring of platform slab that stands out past the parapet (drum.ledge_uv)."""
        for f in faces:
            f.material_index = STONE
            self.pend.append((f, lambda v, fc, u0=self.u0: drum.ledge_uv(v, fc, u0)))

    def slope(self, faces, x_foot, stretch):
        """Stone UVs on a face that slopes in X and Z: U along the slope at TD per metre, V along Y."""
        for f in faces:
            f.material_index = STONE
            self.pend.append((f, lambda v, fc, x0=x_foot, k=stretch: (jp.TD * abs(v.co.x - x0) * k, jp.TD * v.co.y)))

    def cut_cells(self):
        """wall.Build.cut_cells, with the cell of each shell chosen by plan.cell_max_for: a shell that reaches a
        stub keeps the wall's cell (jp.CELL_MAX), so that the vertex colour of the end plane has the vertices of the
        wall; every shell of the body takes plan.CELL_BODY. The rest is the wall's rule: a shell with stone or plinth
        faces above the ground is cut along X (unless it is narrower than jp.CELL_SKIP) and along Z; a long carved
        member is cut along X; a narrow tall one along Z. Faces marked hidden and undersides are left whole."""
        bm = self.bm
        for i, (f, _fn) in enumerate(self.pend):
            if f.is_valid:
                f[self.rule] = i + 1
        self.cut_done = True
        before = wall.tri_count(bm)
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
            cell = plan.cell_max_for(self.variant, lo[0], hi[0], hi[2])
            wide, tall = hi[0] - lo[0], hi[2] - lo[2]
            z_lo = max(lo[2], 0.0)
            if rec["stone"]:
                if hi[2] <= 0.0:
                    continue  # underground: nobody sees it
                if wide >= jp.CELL_SKIP and plan.wants_x_cuts(lo[0], hi[0]):
                    want(0, jp.even_cuts(lo[0], hi[0], cell), sid)
                want(2, jp.even_cuts(z_lo, hi[2], cell), sid)
            elif wide >= wall.TRIM_CUT_MIN:
                want(0, jp.even_cuts(lo[0], hi[0], jp.CELL_TRIM), sid)
            elif wide < 1.5 and tall >= wall.TRIM_CUT_MIN:
                want(2, jp.even_cuts(z_lo, hi[2], cell), sid)
        tris0 = self._shell_tris()
        eps = 1e-6
        for (axis, pos) in sorted(planes):
            sids = planes[(axis, pos)]
            # a plane along X cuts only the faces that stand square to an axis: a face of the drum is already one slice
            # of 5 degrees, which carries the wash along the arc, and a cut through it adds a vertex that shows nothing
            faces = [f for f in bm.faces if f[self.shell] in sids and not f[self.hid]
                     and min(v.co[axis] for v in f.verts) < pos - eps and max(v.co[axis] for v in f.verts) > pos + eps
                     and (axis == 2 or max(abs(c) for c in f.normal) > 0.999)]
            if not faces:
                continue
            geom = list(faces) + list({e for f in faces for e in f.edges}) + list({v for f in faces for v in f.verts})
            co = (pos, 0.0, 0.0) if axis == 0 else (0.0, 0.0, pos)
            no = (1.0, 0.0, 0.0) if axis == 0 else (0.0, 0.0, 1.0)
            bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=co, plane_no=no)
        bm.normal_update()
        tris1 = self._shell_tris()
        added = sorted(((tris1.get(k, 0) - tris0.get(k, 0), k) for k in tris1), reverse=True)[:10]
        worst = [{"added": n, "tris_before": tris0.get(k, 0), "x": [round(info[k]["lo"][0], 2), round(info[k]["hi"][0], 2)],
                  "y": [round(info[k]["lo"][1], 2), round(info[k]["hi"][1], 2)],
                  "z": [round(info[k]["lo"][2], 2), round(info[k]["hi"][2], 2)],
                  "cell": plan.cell_max_for(self.variant, info[k]["lo"][0], info[k]["hi"][0], info[k]["hi"][2])} for n, k in added if n > 0]
        return {"tris_before_cut": before, "tris_after_cut": wall.tri_count(bm), "planes": len(planes),
                "cell_shells": json.dumps(worst)}

    def _shell_tris(self):
        """Triangles per shell id (the face layer `shell`)."""
        out = {}
        for f in self.bm.faces:
            out[f[self.shell]] = out.get(f[self.shell], 0) + len(f.verts) - 2
        return out

    def apply_uv(self):
        """The wall's UVs, then the V of the masonry of a lifted part (before the faces that have three collinear
        corners are split into a fan: the fan triangles are new faces that carry no frame), then the shading."""
        super().apply_uv()
        self.lift_masonry_v()
        self.shade_by_angle()

    def shade_by_angle(self, limit=SHARP_DEG):
        """Shade every face smooth and mark an edge sharp when its two faces differ by more than `limit` degrees. A curved
        face (the drum, an arch, a rib, a door head) is cut into facets of a few degrees, so it shades as one round
        surface; a real edge (a moulding, a cap, an opening, a corner) is far over the limit and stays crisp."""
        bm = self.bm
        bm.normal_update()
        cos_limit = math.cos(math.radians(limit))
        for f in bm.faces:
            f.smooth = True
        for e in bm.edges:
            if len(e.link_faces) == 2:
                a, b = e.link_faces
                e.smooth = a.normal.dot(b.normal) >= cos_limit

    def lift_masonry_v(self):
        """A part built in a lifted frame was mapped at its local height, so its stone V is TD z of the local z. The
        frame lifts it by dz, so V gains TD dz here (the platform parapet is 5 m up, which is 2.25 UV: a quarter of
        a tile off the courses of the wall). Only the masonry of the vertical faces takes the shift: a horizontal
        face has no bed joint in V, and the trim strips are laid out across the member."""
        for f in self.bm.faces:
            k = f[self.fr]
            dz = self.frames[k].t[2] if k else 0.0
            if dz and f.material_index in (STONE, RUBBLE) and abs(f.normal.z) < 0.9:
                for loop in f.loops:
                    u, v = loop[self.uvl].uv
                    loop[self.uvl].uv = (u, v + jp.TD * dz)

    def loop_colours(self):
        marked = [f for f in self.bm.faces if f[self.rec]]
        RECESS_LOG.append({"recess_faces": len(marked), "recess_trim_faces": sum(1 for f in marked if f.material_index == wall.TRIM)})
        out = super().loop_colours()
        done = set()
        for f in self.bm.faces:
            k = f[self.fr]
            if not k:
                continue
            for v in f.verts:
                if v not in done:
                    done.add(v)
                    v.co = self.frames[k].point(tuple(v.co))
        self.bm.normal_update()
        return out


class ArcFrame:
    """A local frame bent round the drum, for ribs and arch frames: local x runs clockwise along the face at the drum
    radius from the angle theta_c, local y is the wall frame y (2.5 is the drum face), z is up."""

    def __init__(self, theta_c_deg):
        self.tc = math.radians(theta_c_deg)
        self.t = (0.0, 0.0, 0.0)  # no lift: the frame does not move a part up

    def point(self, p):
        r = plan.DRUM_R + (p[1] - jp.Y_OUT)
        th = self.tc - p[0] / plan.DRUM_R
        return (r * math.cos(th), plan.DRUM_CY + r * math.sin(th), p[2])


@contextlib.contextmanager
def framed(B, frame):
    B.frame = frame
    try:
        yield
    finally:
        B.frame = None


def rect_loop(x0, x1, y0, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def jambs(B, faces):
    """The faces of a cut in stone (a door): all of them are the wall stone with the wall's own rule, and a recess (darker),
    as the niche of the wall (version 6: the curved part of the reveal was the trim sheet)."""
    faces = [f for f in faces if f is not None]
    B.world(faces, STONE)
    B.recess(faces)


def slab(B, shape, holes, z0, z1, drum_uv=False):
    """A slab: a rectangle (x0, x1, y0, y1) or an outline whose first edge is the low-Y edge, with holes. A hole on
    the low-Y edge becomes a notch of the outline (a hole that touches the outline cannot be tessellated). Variant b
    passes drum_uv so that the faces of its curved edge take the arc rule."""
    outline = rect_loop(*shape) if isinstance(shape[0], (int, float)) else list(shape)
    y0 = outline[0][1]
    inner = []
    for h in holes:
        if abs(h[2] - y0) < 1e-9:
            outline = [outline[0], (h[0], y0), (h[0], h[3]), (h[1], h[3]), (h[1], y0)] + outline[1:]
        else:
            inner.append(rect_loop(*h))
    faces = wall.prism_loops(B, [outline] + inner, lambda u, v, k: Vector((u, v, z0 if k == 0 else z1)))
    (B.terrain if drum_uv else B.world)(faces, STONE)
    if z1 == plan.PLATFORM_Z:  # the deck of the platform is seen from above only: its edge faces keep their cells
        B.hide([f for f in faces if f.normal.z > 0.9])


def slab_ledge(B, outer, inner, holes, z0, z1):
    """The platform slab of variant b. Its top and bottom are a ring (between the loops `outer` and `inner`, the same
    number of points each) and a deck (inside `inner`, with the holes). The ring is the ledge that stands out past
    the parapet; it takes the arc rule (U along the curve) instead of the planar rule that made chevrons over a
    curve. `inner` lies under the parapet, so the seam between the ring and the deck is hidden."""
    n = len(outer)

    def ring(loop, z):
        return [B.vert((x, y, z)) for x, y in loop]

    holes = [rect_loop(*h) for h in holes]
    bo, to, bi, ti = ring(outer, z0), ring(outer, z1), ring(inner, z0), ring(inner, z1)
    hb, ht = [ring(h, z0) for h in holes], [ring(h, z1) for h in holes]
    sides, ledge = [], []
    for i in range(n):
        j = (i + 1) % n
        sides.append(B.face(bo[i], bo[j], to[j], to[i]))
        ledge.append(B.face(to[i], to[j], ti[j], ti[i]))
        ledge.append(B.face(bo[i], bo[j], bi[j], bi[i]))
    loops = [inner] + holes
    top, bottom = [ti] + ht, [bi] + hb
    flat_top, flat_bottom = [v for r in top for v in r], [v for r in bottom for v in r]
    deck = []
    for t in wall.tess(loops):
        deck.append(B.face(*[flat_top[i] for i in t]))
        deck.append(B.face(*[flat_bottom[i] for i in t]))
    for h, a, b in zip(holes, hb, ht):
        for i in range(len(h)):
            j = (i + 1) % len(h)
            sides.append(B.face(a[i], a[j], b[j], b[i]))
    kept = B.seal(sides + ledge + deck)
    ledge_set = {f for f in ledge if f is not None}
    B.ledge([f for f in kept if f in ledge_set])
    B.terrain([f for f in kept if f not in ledge_set], STONE)
    # the ring on top lies under the parapet and the deck is seen from above only: no cells (its edge faces keep theirs)
    deck_set = {f for f in deck if f is not None}
    B.hide([f for f in kept if f in ledge_set or f in deck_set])


def prism_y(B, prof, y0, y1):
    """A closed solid from an (x, z) profile extruded along Y."""
    n = len(prof)
    a = [B.vert((x, y0, z)) for x, z in prof]
    c = [B.vert((x, y1, z)) for x, z in prof]
    faces = [B.face(a[i], a[(i + 1) % n], c[(i + 1) % n], c[i]) for i in range(n)]
    for t in wall.tess([prof]):
        faces.append(B.face(*[a[k] for k in t]))
        faces.append(B.face(*[c[k] for k in t]))
    return B.seal(faces)


def stone_prism(B, prof, y0, y1, x_foot):
    """A stone solid of an (x, z) profile that has sloped faces (a flight, the rail beside it): the sloped faces
    take the slope rule from the foot at x_foot, the others the wall rule."""
    stretch = math.hypot(plan.TREAD, plan.RISER) / plan.TREAD
    faces = prism_y(B, prof, y0, y1)
    slope = [fc for fc in faces if abs(fc.normal.y) < 0.02 and abs(fc.normal.x) > 0.02 and abs(fc.normal.z) > 0.02]
    B.world([fc for fc in faces if fc not in set(slope)], STONE)
    B.slope(slope, x_foot, stretch)


def outline_frame(B, place, outer, inner, strip="S4"):
    """A moulded frame between two outlines of the same point count (wall.arch_frame for an outline that is not the
    equilateral arch). place(u, v, k) gives the 3D point of an outline point on the front (k 1) or back (k 0) layer.
    Built as quad strips so that U runs along the outline."""
    n = len(outer)
    of = [B.vert(place(u, v, 1)) for u, v in outer]
    ob = [B.vert(place(u, v, 0)) for u, v in outer]
    inf = [B.vert(place(u, v, 1)) for u, v in inner]
    inb = [B.vert(place(u, v, 0)) for u, v in inner]
    s = [0.0]
    for i in range(1, n):
        s.append(s[-1] + math.dist(outer[i], outer[i - 1]))
    v0, v1 = jp.STRIPS[strip]
    index, faces = {}, []
    for i in range(n - 1):
        j = i + 1
        for vs in ((of[i], of[j], inf[j], inf[i]), (of[i], of[j], ob[j], ob[i]),
                   (inf[i], inf[j], inb[j], inb[i]), (ob[i], ob[j], inb[j], inb[i])):
            f = B.face(*vs)
            faces.append(f)
            index[f] = (i, vs)
    faces += [B.face(of[0], inf[0], inb[0], ob[0]), B.face(of[-1], inf[-1], inb[-1], ob[-1])]
    for f in B.seal(faces):
        if f not in index:
            B.trim([f], strip, 0)
            continue
        i, vs = index[f]
        B.direct(f, {vs[0]: (jp.TD * s[i], v0), vs[1]: (jp.TD * s[i + 1], v0),
                     vs[2]: (jp.TD * s[i + 1], v1), vs[3]: (jp.TD * s[i], v1)})


# ---- the shell, variant a ---------------------------------------------------------------------------
def build_stubs(B, variant="a"):
    """The two wall stubs, the wall's own builder, plus the ribs of the joint pier on the tower side, cut flat on
    the tower face (the wall builder leaves them to the neighbour). Variant b: the stubs start inside the drum wall."""
    xin = plan.stub_inner_x(variant)
    cut = plan.stub_rib_clip_x(variant)
    runs = ((wall.Run(plan.PIECE_X0, -xin, (plan.PIECE_X0,), uv_x0=U0), plan.PIECE_X0, 1, -cut),
            (wall.Run(xin, plan.PIECE_X1, (plan.PIECE_X1,), uv_x0=U0), plan.PIECE_X1, -1, cut))
    for run, xc, sign, clip in runs:
        wall.build_wall(B, run, STEPS, fine=True)
        for spec in (wall.LONG_RIB, wall.LOW_RIB):
            wall.build_rib(B, xc, sign, spec, STEPS, clip_x=clip, max_turn_deg=TURN)


def build_foundation_and_plinth(B):
    fx = BX + OUT
    B.world(wall.box(B, -fx, fx, Y0, Y1 + OUT, jp.Z_FOOT, -plan.SLAB_T), RUBBLE)
    ring = wall.prism_loops(B, [rect_loop(-fx, fx, Y0, Y1 + OUT), rect_loop(IX0, IX1, IY0, IY1)],
                            lambda u, v, k: Vector((u, v, -plan.SLAB_T if k == 0 else 0.0)))
    B.world(ring, RUBBLE)
    z = jp.Z_PLINTH
    for sx in (-1, 1):  # side plinth, battered outside
        xs = sorted((sx * IX1, sx * (BX + OUT)))
        xt = sorted((sx * IX1, sx * BX))
        B.world(wall.hexa(B, ((xs[0], Y0, 0), (xs[1], Y0, 0), (xs[1], IY1, 0), (xs[0], IY1, 0)),
                          ((xt[0], Y0, z), (xt[1], Y0, z), (xt[1], IY1, z), (xt[0], IY1, z))), RUBBLE)
    B.world(wall.hexa(B, ((-fx, IY1, 0), (fx, IY1, 0), (fx, Y1 + OUT, 0), (-fx, Y1 + OUT, 0)),
                      ((-BX, IY1, z), (BX, IY1, z), (BX, Y1, z), (-BX, Y1, z))), RUBBLE)


def back_wall(B):
    """The back wall of the room (X IX0 to IX1, Y Y0 to IY0) with the ground door. Below the plinth line (Z 3.0) it is
    two rubble blocks, one each side of the door, as the wall's inner face is rubble up to the plinth line; above
    it is one stone block whose underside is the head of the door. The door has straight sides to 2.2 m and its apex
    over the plinth line, so the head is cut in the stone. The reveal of the door (its jambs and its head) is the
    wall stone with the wall's rule, as a niche (version 6); the head has the fine steps of plan.door_fine_steps."""
    d = plan.ground_door()
    z = jp.Z_PLINTH
    steps = plan.GROUND_DOOR_STEPS
    lo_l, head, lo_r = plan.door_cut(d["width"], d["jamb"], d["height"], z, steps)
    xc = d["x_centre"]

    def at(pts):
        return [(xc + u, v) for u, v in pts]

    solids = ((([(IX0, 0.0)] + at(lo_l) + [(IX0, z)]), RUBBLE),
              ((at(lo_r) + [(IX1, 0.0), (IX1, z)]), RUBBLE),
              (([(IX0, z)] + at(head) + [(IX1, z), (IX1, WALL_TOP), (IX0, WALL_TOP)]), STONE))
    for loop, mat in solids:
        faces = wall.prism_loops(B, [loop], lambda u, v, k: Vector((u, Y0 if k == 0 else IY0, v)))
        fronts = [f for f in faces if abs(f.normal.y) > 0.99]
        B.world(fronts, mat)
        rest = [f for f in faces if f not in set(fronts)]
        B.world(rest, STONE)
        B.recess(rest)


def build_walls(B):
    door = plan.side_door()
    for sx in (-1, 1):
        outline = [(Y0, jp.Z_PLINTH), (Y1, jp.Z_PLINTH), (Y1, WALL_TOP), (Y0, WALL_TOP)]
        hole = [(door["y_centre"] + u, door["z_sill"] + v)
                for u, v in wall.pointed_arch(door["width"], door["height"], plan.SIDE_DOOR_STEPS)]
        xa, xb = sx * IX1, sx * BX
        jambs(B, wall.prism_loops(B, [outline, hole], lambda u, v, k: Vector((xa if k == 0 else xb, u, v))))
    B.world(wall.box(B, IX0, IX1, IY1, Y1, jp.Z_PLINTH, WALL_TOP), STONE)
    back_wall(B)


def build_foundation_and_plinth_b(B):
    """Variant b: the foundation is the back part and a half drum (rubble), and the plinth is a ring of the drum
    with the flat side plinths, as in variant a, up to the tangent line."""
    fx = BX + OUT
    outline = [(-fx, Y0), (fx, Y0)] + plan._arc(0.0, plan.DRUM_CY, plan.DRUM_R + OUT, plan.FOUNDATION_STEPS_B)
    floor_hole = plan.floor_outline("b")
    for loops, z0, z1 in (([outline], jp.Z_FOOT, -plan.SLAB_T), ([outline, floor_hole], -plan.SLAB_T, 0.0)):
        faces = wall.prism_loops(B, loops, lambda u, v, k, a=z0, b=z1: Vector((u, v, a if k == 0 else b)))
        B.terrain(faces, RUBBLE)
    z = jp.Z_PLINTH
    for sx in (-1, 1):
        xs = sorted((sx * IX1, sx * (BX + OUT)))
        xt = sorted((sx * IX1, sx * BX))
        B.world(wall.hexa(B, ((xs[0], Y0, 0), (xs[1], Y0, 0), (xs[1], plan.DRUM_CY, 0), (xs[0], plan.DRUM_CY, 0)),
                          ((xt[0], Y0, z), (xt[1], Y0, z), (xt[1], plan.DRUM_CY, z), (xt[0], plan.DRUM_CY, z))), RUBBLE)
    mid = plan.DRUM_R + OUT / 2.0  # the batter in two rows, so that each row takes its own arc scale for U
    ring = drum.Loft(B, drum.polar_sections([(plan.DRUM_R - plan.WALL_T, 0.0), (plan.DRUM_R + OUT, 0.0),
                                             (mid, z / 2.0), (plan.DRUM_R, z), (plan.DRUM_R - plan.WALL_T, z)],
                                            drum.drum_thetas()))
    ring.stone(RUBBLE)


def build_walls_b(B):
    """Variant b: the back wall, and on each side the flat wall up to the +-0.7 m jambs of the side door (a block under
    the door, a lintel over it, a box behind), then the drum wall from the plane of the jamb. The part of the block
    under the stub is 0.1 m lower than the deck, so the stub deck is the sill there and no two faces share a plane."""
    hw = plan.drum_join_y()
    door = plan.side_door()
    top = [(door["y_centre"] + u, door["z_sill"] + v)
           for u, v in wall.pointed_arch(door["width"], door["height"], plan.SIDE_DOOR_STEPS)]
    lintel = top[1:-1] + [(hw, WALL_TOP), (-hw, WALL_TOP)]
    for sx in (-1, 1):
        xa, xb = sorted((sx * IX1, sx * BX))
        xs = sx * plan.stub_inner_x("b")
        B.world(wall.box(B, xa, xb, Y0, -hw, jp.Z_PLINTH, WALL_TOP), STONE)
        under = sorted((sx * IX1, xs))
        B.world(wall.box(B, under[0], under[1], -hw, hw, jp.Z_PLINTH, jp.Z_DECK), STONE)
        under = sorted((xs, sx * BX))
        B.world(wall.box(B, under[0], under[1], -hw, hw, jp.Z_PLINTH, jp.Z_DECK - 0.1), STONE)
        faces = wall.prism_loops(B, [lintel], lambda u, v, k, a=xa, b=xb: Vector((a if k == 0 else b, u, v)))
        jambs(B, faces)
    back_wall(B)
    drum.wall_ring(B, plan.DRUM_R - plan.WALL_T, plan.DRUM_R, jp.Z_PLINTH, WALL_TOP, hw)


CORNICE_B = ((15.95, 16.3, 0.30), (16.3, WALL_TOP, 0.60))


def build_platform_slab_and_cornice_b(B):
    """Variant b: the platform stands out by the corbel on every side: a slab that is the body outline moved out by
    corbel + plan.SLAB_PROUD, over a cornice of two stepped courses, swept round the body. The ring of the slab past the
    parapet is mapped along the arc."""
    plat = [f for f in plan.floors("b") if f["name"] == "platform"][0]
    body = plan.outline("b", plan.CORNICE_STEPS_B)
    outer, inner = plan.ledge_offsets("b")
    slab_ledge(B, plan.offset_outline(body, outer), plan.offset_outline(body, inner),
               plat["holes"], WALL_TOP, plan.PLATFORM_Z)
    path = drum.body_path("b", plan.CORNICE_STEPS_B)
    s = drum.path_lengths(path, closed=True)
    for z0, z1, p in CORNICE_B:
        prof = [(-0.05, z0), (p, z0), (p, z1), (-0.05, z1)]
        drum.Loft(B, drum.path_sections(path, prof), closed=True).trim("S2", s, prof)


def build_platform_slab_and_cornice(B, variant):
    plat = [f for f in plan.floors(variant) if f["name"] == "platform"][0]
    p = SLAB_PROUD
    slab(B, (-BX - p, BX + p, Y0 - p, Y1 + p), plat["holes"], WALL_TOP, plan.PLATFORM_Z)
    for z0, z1, p in CORNICE:
        faces = wall.prism_loops(B, [rect_loop(-BX - p, BX + p, Y0 - p, Y1 + p), rect_loop(-BX, BX, Y0, Y1)],
                                 lambda u, v, k, a=z0, b=z1: Vector((u, v, a if k == 0 else b)))
        B.trim(faces, "S2", 0)


def build_corner_piers(B, fronts=(True, False)):
    """Front corners: the wall's pier (1.0 m proud at the ground, 0.5 at Z 8.5) and its pilaster, as one square pier
    that serves both faces, with the wall's carved collar swept round it (the rib limbs of both faces spring from
    it). Back corners: a plain quoin. Variant b has the back corners only. Both start their taper at the top of the
    collar and die into the wall at the foot of the string course (plan.corner_pier_parts), so nothing of a corner
    meets the string course or the cornice from below and stands prouder than it."""
    wide = jp.COLLAR_HALF_W - jp.PIER_HALF  # the collar is this much wider than the pier on the two inner sides
    for sx in (-1, 1):
        for front in fronts:
            def ring(p, z, w=0.0, sx=sx, front=front):
                xs = sorted((sx * (BX - PIER_BACK - w), sx * (BX + p)))
                ys = sorted((Y1 - PIER_BACK - w, Y1 + p)) if front else sorted((Y0 - p, Y0 + PIER_BACK))
                return tuple((x, y, z) for x, y in ((xs[0], ys[0]), (xs[1], ys[0]), (xs[1], ys[1]), (xs[0], ys[1])))
            for p0, z0, p1, z1 in plan.corner_pier_parts(front):
                B.world(wall.hexa(B, ring(p0, z0), ring(p1, z1)), STONE)
            if front:
                collar = [(y - jp.Y_OUT, z) for y, z in jp.collar_profile()[:4]]  # the outer line of the collar
                faces = []
                for (p0, z0), (p1, z1) in zip(collar, collar[1:]):
                    faces += wall.hexa(B, ring(p0, z0, wide), ring(p1, z1, wide))
                B.trim(faces, "S2", 0)


def spans_around(x0, x1, gaps):
    """The stretches of x0 to x1 that run up to a pier at each x of `gaps` and stop inside it, as the wall's members."""
    pts = [x0]
    for g in gaps:
        pts += [g - (jp.PIER_HALF - 0.05), g + (jp.PIER_HALF - 0.05)]
    pts.append(x1)
    return list(zip(pts[::2], pts[1::2]))


MEMBERS = (("plinth_cap", "S2"), ("band", "S3"))
# Where each member of a side face stops against the wall stub beside it: the outer face of the stub's own course
# on the town side (y of the stub's back strip end) and on the outer side (y where the front stretch starts). The
# stub is 5 m thick and covers the face between; its courses run on from the face, so a member of the face that
# crossed it would overlap them in a small square at each corner, with coplanar tops and bottoms. (The string course
# is a wrap, plan.string_course_corner_paths, and ends at the same places.)
STUB_ENDS = {"plinth_cap": (jp.Y_IN - jp.INNER_PLINTH_PROUD, jp.Y_OUT + jp.PLINTH_CAP_PROUD),
             "band": (jp.Y_IN, jp.Y_OUT + jp.BAND_PROUD)}


def member_profile(name):
    return jp.section()[name] if name == "string_course" else jp.member(name)


def side_spans(face, name):
    """The stretches of an east or west face (local x) that carry the member `name`: the front stretch and the back
    strip, each up to the course of the wall stub beside it (STUB_ENDS)."""
    stub_back, stub_front = STUB_ENDS[name]
    back, front = (Y0 + PIER_BACK - 0.05, stub_back), (stub_front, Y1 - PIER_BACK + 0.05)
    if face == "east":
        return [tuple(sorted(plan.FACE_CY - y for y in ys)) for ys in (front, back)]
    return [tuple(sorted(y - plan.FACE_CY for y in ys)) for ys in (back, front)]


def face_members(B, face, spans_of):
    """The plinth cap and the carved band, carried across a face of the tower at the same Z, over the stretches
    spans_of(member name) (local x ranges of the face). The band is only as wide as the arches that stand on it."""
    with framed(B, FACES[face]):
        for name, strip in MEMBERS:
            for a, b in spans_of(name):
                B.trim(wall.prism_x(B, member_profile(name), a, b), strip, 0)


def sweep(B, path, name, strip):
    """A member swept along a path (x, y, mx, my) with the wall's profile of `name`: U along the path, and the same rule
    on a straight run, a mitred corner and the drum."""
    prof = [(y - jp.Y_OUT, z) for y, z in member_profile(name)]
    drum.Loft(B, drum.path_sections(path, prof)).trim(strip, drum.path_lengths(path), prof)


def string_course_wraps(B, front=True):
    """The string course of the wall, wrapped round the corners of the body in one mitred sweep each: the front (round the
    two front corners, variant a) and the back (round the two back corners). It runs on from stub to stub and nothing
    interrupts it: the piers have died at its foot. Sections stand at most 2.5 m apart for the vertex colour."""
    paths = plan.string_course_corner_paths()
    for k, path in enumerate(paths):
        if k == 0 and not front:
            continue
        sweep(B, plan.wrap_path(path, 2.5), "string_course", "S2")


def back_members(B, x0, x1):
    """The back face is the inner face of the wall carried across the tower: the wall's inner plinth course at the same
    Z (the rubble plinth below it is back_wall). The string course of the back is part of string_course_wraps."""
    sec = jp.section()
    with framed(B, FACES["back_in"]):
        for a, b in plan.back_course_spans(x0, x1, wall.ARCH_MOULD):  # the plinth course stops at the door frame
            B.trim(wall.prism_x(B, sec["inner_plinth_course"], a, b), "S2", 0)


def blind_arches(B, face, xs, tiers, sill_tiers=0, steps=None):
    """The blind arch frames of a face, tier by tier. The lowest `sill_tiers` tiers get one sill under all their
    frames (the front and the sides have the carved band under the first tier; the back has none, so its frames
    would end in the air). `steps` None builds the fine frame of the wall (16 steps an arc); a number builds that many."""
    with framed(B, FACES[face]):
        for t, (z0, w, h) in enumerate(tiers):
            for cx in xs:
                def place(u, v, k, cx=cx, z0=z0):
                    return Vector((cx + u, wall.RIB_BACK_Y if k == 0 else jp.Y_OUT + wall.ARCH_PROUD, z0 + v))
                wall.arch_frame(B, place, w, h, wall.ARCH_MOULD, steps or 8, fine=steps is None)
            if t < sill_tiers:
                x0, x1 = plan.sill_x_range(min(xs), w)[0], plan.sill_x_range(max(xs), w)[1]
                prof = plan.sill_profile(z0, wall.RIB_BACK_Y, jp.Y_OUT + wall.ARCH_PROUD)
                B.trim(wall.prism_x(B, prof, x0, x1), "S2", 0)


def door_frame(B, face, cx, z0, width, jamb):
    """A moulded frame round a door hole in the wall face (it stands on the face, not inside it)."""
    w = width + 2 * wall.ARCH_MOULD
    with framed(B, FACES[face]):
        def place(u, v, k):
            return Vector((cx + u, jp.Y_OUT if k == 0 else jp.Y_OUT + DOOR_FRAME_PROUD, z0 + v))
        wall.arch_frame(B, place, w, jamb + w * SQ3, wall.ARCH_MOULD, plan.SIDE_DOOR_STEPS, fine=False)


def ground_door_frame(B):
    """The moulded frame of the ground door on the back face, the shape of the door (straight sides, a pointed head)."""
    d = plan.ground_door()
    steps = plan.GROUND_DOOR_STEPS
    inner = plan.door_outline(d["width"], d["jamb"], d["height"], steps)
    outer = plan.door_outline(d["width"], d["jamb"], d["height"], steps, mould=wall.ARCH_MOULD)
    with framed(B, FACES["back"]):
        def place(u, v, k):
            return Vector((d["x_centre"] + u, jp.Y_OUT if k == 0 else jp.Y_OUT + DOOR_FRAME_PROUD, d["z_sill"] + v))
        outline_frame(B, place, outer, inner)


def pier_ribs(B, face, xc, sign, half_bay=None, clip_x=None):
    """The two ribs that spring from the pier at xc of a face and run toward `sign`, cut on the plane clip_x. Two limbs of a
    bay that are cut on the same plane share their end ring (one mitre, no cap, no overlap): wall.build_rib."""
    with framed(B, FACES[face]):
        for spec in (wall.LONG_RIB, wall.LOW_RIB):
            wall.build_rib(B, xc, sign, spec, STEPS, clip_x=clip_x, half_bay=half_bay, max_turn_deg=TURN)


def front_pier(B, xc):
    """A whole pier of the wall, with its niche and collar, in the middle of the front."""
    with framed(B, FACES["front"]):
        wall.build_pier(B, wall.Run(-BX, BX, (xc,), uv_x0=U0), xc)


def counted(B, name, fn, *args):
    """Run a shell step and record the triangles it added."""
    before = sum(len(f.verts) - 2 for f in B.bm.faces)
    fn(*args)
    STEP_TRIS[name] = sum(len(f.verts) - 2 for f in B.bm.faces) - before


def shell_a(B):
    long_x = BX - PIER_BACK + 0.05
    sd = plan.side_door()
    counted(B, "stubs", build_stubs, B)
    counted(B, "foundation_plinth", build_foundation_and_plinth, B)
    counted(B, "walls", build_walls, B)
    counted(B, "platform_cornice", build_platform_slab_and_cornice, B, "a")
    counted(B, "corner_piers", build_corner_piers, B)
    counted(B, "front_pier", lambda: [front_pier(B, xc) for xc in plan.FRONT_PIERS[1:-1]])
    counted(B, "string_course", string_course_wraps, B)
    counted(B, "back_members", back_members, B, -long_x, long_x)
    # the plinth cap runs round the face between the piers; the band is only as wide as the arches on it
    counted(B, "front_members", face_members, B, "front", lambda name: spans_around(-long_x, long_x, plan.FRONT_PIERS[1:-1])
            if name == "plinth_cap" else plan.band_spans("front"))
    for face in ("east", "west"):
        counted(B, face + "_members", face_members, B, face, lambda name, face=face: side_spans(face, name)
                if name == "plinth_cap" else plan.band_spans(face))
    counted(B, "arches_front", blind_arches, B, "front", plan.FRONT_ARCH_X, FRONT_TIERS)
    counted(B, "arches_back", blind_arches, B, "back", ARCH_X, BACK_TIERS, 1, plan.TOWN_FRAME_STEPS)
    # one arch pair on each side face, between the stub and the front pier
    counted(B, "arches_sides", lambda: (blind_arches(B, "east", (plan.SIDE_ARCH_X["east"],), BACK_TIERS),
                                        blind_arches(B, "west", (plan.SIDE_ARCH_X["west"],), BACK_TIERS)))

    def ribs():
        # the ribs of the front reach the middle of each bay (the half bay is half the pier spacing, 2.5 m) and are cut on
        # its plane, so the two limbs of a bay are one mitred pair
        for xc, sign in zip(plan.FRONT_PIERS[:-1] + plan.FRONT_PIERS[1:], (1, 1, -1, -1)):
            pier_ribs(B, "front", xc, sign, plan.FRONT_HALF_BAY, clip_x=xc + sign * plan.FRONT_HALF_BAY)
        # a side face has the ribs of the front corner pier only: they run into the stub, so they are cut 5 cm inside it
        stub_x = plan.FACE_CY - jp.Y_OUT + 0.05
        pier_ribs(B, "east", -(Y1 - plan.FACE_CY), 1, clip_x=stub_x)
        pier_ribs(B, "west", Y1 - plan.FACE_CY, -1, clip_x=-stub_x)
    counted(B, "ribs", ribs)
    counted(B, "doors", lambda: (
        door_frame(B, "east", plan.FACE_CY - sd["y_centre"], sd["z_sill"], sd["width"], sd["jamb"]),
        door_frame(B, "west", sd["y_centre"] - plan.FACE_CY, sd["z_sill"], sd["width"], sd["jamb"]),
        ground_door_frame(B)))


# ---- the shell, variant b ---------------------------------------------------------------------------
UP_RIB = plan.UP_RIB  # the ribs that branch near the top: they spring from the upper collar (spec as wall.LONG_RIB)
RIB_STEPS = 8
BAY_TIER1 = (wall.ARCH_Z0, plan.BAY_TIER1_W, 4.2)
BAY_WINDOW = (plan.UPPER_TIER_Z0, 0.7, 3.4)
BAND_ARC_DEG = 6.0  # the sections of the carved band under the arch of a bay


def drum_members(B):
    """The string course, the plinth cap and the carved band of the drum. The string course is one sweep along the arc
    from stub to stub and one wrap round the two back corners (string_course_wraps); the plinth cap runs round the
    drum and the back strip of each side; the band is only as wide as the arch of each bay (plan.drum_band_half_angle),
    ending on the radial planes of the outer faces of its legs. Every piece stops at the courses of the wall stubs
    (STUB_ENDS)."""
    for name, ends in (("string_course", (plan.STRING_STUB_Y[0], plan.STRING_STUB_Y[1])), ("plinth_cap", STUB_ENDS["plinth_cap"])):
        east, arc, west = drum.drum_member_paths(Y0 + PIER_BACK - 0.05, *ends)
        if name == "plinth_cap":
            sweep(B, east, name, "S2")
            sweep(B, west, name, "S2")
        th0 = math.atan2(arc[0][1] - plan.DRUM_CY, arc[0][0])
        sweep(B, drum.arc_path(th0, math.pi - th0, plan.DRUM_MEMBER_DEG), name, "S2")
    half = plan.drum_band_half_angle()
    for tc in plan.drum_bay_centres():
        t = math.radians(tc)
        sweep(B, drum.arc_path(t - half, t + half, BAND_ARC_DEG), "band", "S3")


def drum_bays(B):
    """Blind arches and a narrow window frame in the three full bays, and the ribs of every pier: the wall's own two
    ribs under the string course, and the ribs that branch near the top. A bay is the 36 degrees between two piers; the
    ribs reach half of it (the arc length). Both limbs of a bay are built in the frame of the bay (its middle is local
    x 0, local x runs clockwise), the left one from the pier at x -half_bay and the right one from the pier at +half_bay,
    each cut on the plane x 0: the two are exact mirror images about the radial plane of the bay and share their apex
    ring (one mitre)."""
    half_bay = plan.drum_half_bay()
    for tc in plan.drum_bay_centres():
        B.rib_ends = []  # the frames of two bays have the same local numbers: a ring of one bay is never the ring of another
        with framed(B, ArcFrame(tc)):
            for (z0, w, h), steps in ((BAY_TIER1, plan.DRUM_ARCH_STEPS), (BAY_WINDOW, plan.WINDOW_FRAME_STEPS)):
                def place(u, v, k, z0=z0):
                    return Vector((u, wall.RIB_BACK_Y if k == 0 else jp.Y_OUT + wall.ARCH_PROUD, z0 + v))
                wall.arch_frame(B, place, w, h, wall.ARCH_MOULD, steps or 8, fine=steps is None)
            for spec in (wall.LONG_RIB, wall.LOW_RIB, UP_RIB):
                for xc, sign in ((-half_bay, 1), (half_bay, -1)):
                    wall.build_rib(B, xc, sign, spec, RIB_STEPS, clip_x=0.0, half_bay=half_bay,
                                   max_turn_deg=plan.UP_RIB_TURN if spec is UP_RIB else TURN)


def shell_b(B):
    long_x = BX - PIER_BACK + 0.05
    sd = plan.side_door()
    counted(B, "stubs", build_stubs, B, "b")
    counted(B, "foundation_plinth", build_foundation_and_plinth_b, B)
    counted(B, "walls", build_walls_b, B)
    counted(B, "platform_cornice", build_platform_slab_and_cornice_b, B)
    counted(B, "back_quoins", build_corner_piers, B, (False,))  # the quoin dies at the string course
    counted(B, "back_members", back_members, B, -long_x, long_x)
    counted(B, "string_course_back", string_course_wraps, B, False)
    counted(B, "drum_members", drum_members, B)
    counted(B, "piers", lambda: [drum.engaged_pier(B, th) for th in plan.PIER_ANGLES_B])
    counted(B, "upper_collars", lambda: [drum.upper_collar(B, th) for th in plan.PIER_ANGLES_B])
    counted(B, "bays", drum_bays, B)
    counted(B, "doors", lambda: (
        door_frame(B, "east", plan.FACE_CY - sd["y_centre"], sd["z_sill"], sd["width"], sd["jamb"]),
        door_frame(B, "west", sd["y_centre"] - plan.FACE_CY, sd["z_sill"], sd["width"], sd["jamb"]),
        ground_door_frame(B)))


# ---- interior, stairs, parapets ---------------------------------------------------------------------
def rail_box(B, r, level, rails):
    """A rail: a stone slab 1.0 m above the level with its cap. It stands on the floor (it is not sunk into it), so
    its face on the edge of the hole continues the side of the hole and the two are never in one plane. The cap is
    plan.rail_cap_rect: the cap of the rail across the foot of a hole stops at the caps of the side rails, so no
    two caps cross."""
    x0, x1, y0, y1 = r
    top = level + plan.RAIL_H
    B.world(wall.box(B, x0, x1, y0, y1, level, top - 0.1), STONE)
    cx0, cx1, cy0, cy1 = plan.rail_cap_rect(r, rails)
    B.trim(wall.box(B, cx0, cx1, cy0, cy1, top - 0.1, top), "S2", 0 if plan.rail_is_along_x(r) else 1)


def build_interior(B, variant):
    """The floors with their holes, the rail round every hole, and the rail beside every open side of a flight."""
    for fl in plan.floors(variant):
        if fl["name"] != "platform":
            slab(B, plan.floor_outline(variant), fl["holes"], fl["z_bottom"], fl["z_top"], variant == "b")
        for r in fl["rails"]:
            rail_box(B, r, fl["z_top"], fl["rails"])
    for f in plan.flights():
        for y0, y1 in plan.flight_rail_sides(variant, f):
            stone_prism(B, plan.stair_rail_profile(f), y0, y1, f["x_foot"])


def flight_profile(f):
    """The (x, z) outline of a flight: flat foot, sloped underside, end face, then the treads and risers from the
    top down."""
    n, run = f["risers"], plan.flight_run(f)
    z_top = plan.flight_z_top(f)
    u_flat = run - (z_top - f["z0"] - plan.END_FACE) * plan.TREAD / plan.RISER  # where the underside leaves the floor
    u_pts = [(0.0, f["z0"]), (u_flat, f["z0"]), (run, z_top - plan.END_FACE), (run, z_top)]
    for k in range(n - 1, -1, -1):
        z = f["z0"] + plan.RISER * (k + 1)
        if k != n - 1:
            u_pts.append((plan.TREAD * (k + 1), z))
        u_pts.append((plan.TREAD * k, z))
    return [(f["x_foot"] + f["direction"] * u, z) for u, z in u_pts]


def build_stairs(B):
    for f in plan.flights():
        stone_prism(B, flight_profile(f), f["lane"][0], f["lane"][1], f["x_foot"])


def hide_inner_faces(B, before):
    """Leave the faces made since `before` that look to -Y (the inner face of a parapet part, in its own frame) whole."""
    B.bm.normal_update()
    B.hide([f for f in B.bm.faces if f not in before and f.normal.y < -0.9])


def hide_room_faces(B, variant):
    """Leave the faces of the shell that look into the room whole: the weather on the outer faces is what the vertex
    colour is for, and a wall face seen from inside needs no cells. A face of a framed build is on an outer face."""
    B.bm.normal_update()
    faces = []
    for f in B.bm.faces:
        if f[B.fr] or abs(f.normal.z) > 0.1:
            continue
        c = sum((v.co for v in f.verts), Vector()) / len(f.verts)
        if plan.in_room(variant, c.x, c.y):
            faces.append(f)
    B.hide(faces)


def framed_part(B, frame, x0, x1, part=None, variant="a"):
    """A parapet part (the wall's build_parapet) in `frame`. A part of the front or back face that ends on a corner
    of the platform gets a short block of cap there (the cap of the side part overhangs its own face and would stop in
    a step beside the end of this part). A part of a side face that ends on a corner gets its frieze carried on over
    the end face of the neighbour, to the corner."""
    before = set(B.bm.faces)
    sec = jp.section()
    with framed(B, frame):
        wall.build_parapet(B, x0, x1)
        # the wall's parapet maps the reveal of its openings on the trim sheet; here it is the wall stone (version 6)
        B.world([f for f in B.bm.faces if f not in before and f[B.rec] and f.material_index == wall.TRIM], STONE)
        if part is not None:
            n = plan.CORNER_CAP_LENGTH
            lo, hi = plan.corner_ends(variant, part)
            if lo:
                B.trim(wall.prism_x(B, sec["parapet_cap"], x0 - n, x0), "S2", 0)
            if hi:
                B.trim(wall.prism_x(B, sec["parapet_cap"], x1, x1 + n), "S2", 0)
            m = plan.CORNER_FRIEZE_LENGTH
            lo, hi = plan.frieze_ends(variant, part)
            if lo:
                B.trim(wall.prism_x(B, sec["frieze"], x0 - m, x0), "S5", 0)
            if hi:
                B.trim(wall.prism_x(B, sec["frieze"], x1, x1 + m), "S5", 0)
    hide_inner_faces(B, before)


def parapet_builders_a(variant):
    """One builder per platform parapet part: a function that builds the part into a Build. The parts of the east
    and west faces stand SIDE_LIFT higher, so that where their caps cross the cap of a front or back part no two
    cap tops lie in one plane."""
    lift = plan.PLATFORM_Z - jp.Z_DECK
    out = []
    for p in plan.platform_parapet_parts(variant):
        dz = lift + (SIDE_LIFT if p["face"] in ("east", "west") else 0.0)
        out.append(lambda B, p=p, dz=dz: framed_part(B, FACES[p["face"]].lifted(dz), p["x0"], p["x1"], p, variant))
    return out


def parapet_builders_b(variant):
    """Variant b: straight parts on the back and the two sides, set out by the corbel, and five arc parts."""
    o, lift = plan.platform_offset(variant), plan.PLATFORM_Z - jp.Z_DECK
    frames = {"back": Frame(180, 0.0, Y0 - o + jp.Y_OUT, lift),
              "east": Frame(270, BX + o - jp.Y_OUT, plan.DRUM_CY, lift + SIDE_LIFT),
              "west": Frame(90, -BX - o + jp.Y_OUT, plan.DRUM_CY, lift + SIDE_LIFT)}
    out = []
    for p in plan.platform_parapet_parts(variant):
        if p["face"] == "arc":
            out.append(lambda B, p=p: drum.arc_parapet(B, p["th0"], p["th1"], lift, o))
        else:
            out.append(lambda B, p=p: framed_part(B, frames[p["face"]], p["x0"], p["x1"], p, variant))
    return out


BUILDERS = {"a": {"shell": shell_a, "parapets": parapet_builders_a},
            "b": {"shell": shell_b, "parapets": parapet_builders_b}}


# ---- objects and the report --------------------------------------------------------------------------
def build_all(variant):
    mats = wall.make_materials()
    base = plan.BASE_NAMES[variant]
    objs = []
    B = TBuild(variant=variant)
    BUILDERS[variant]["shell"](B)
    hide_room_faces(B, variant)
    objs.append(wall.make_object(base, B, mats, cells=CELLS["shell"]))
    B = TBuild(variant=variant)
    build_interior(B, variant)
    objs.append(wall.make_object(base + "_int", B, mats, cells=CELLS["int"]))
    B = TBuild(variant=variant)
    build_stairs(B)
    objs.append(wall.make_object(base + "_stairs", B, mats, cells=CELLS["stairs"]))
    n = 0
    for build in BUILDERS[variant]["parapets"](variant):
        n += 1
        B = TBuild(variant=variant)
        build(B)
        objs.append(wall.make_object(f"{base}_m{n}", B, mats, cells=CELLS["parts"]))
    xin = plan.stub_inner_x(variant)
    for x0, x1 in ((plan.PIECE_X0, -xin), (xin, plan.PIECE_X1)):
        n += 1
        B = TBuild(variant=variant)
        framed_part(B, None, x0, x1)
        objs.append(wall.make_object(f"{base}_m{n}", B, mats, cells=CELLS["parts"]))
    for x0, x1 in ((plan.PIECE_X0, -xin), (xin, plan.PIECE_X1)):
        n += 1
        B = TBuild(variant=variant)
        wall.build_rail(B, x0, x1)
        objs.append(wall.make_object(f"{base}_m{n}", B, mats, cells=CELLS["parts"]))
    return objs


def plan_report(variant):
    return {"shell_tris_by_step": dict(STEP_TRIS), "variant": variant, "piece_length": plan.PIECE_LENGTH, "td": jp.TD,
            "flights": [{"name": f["name"], "x_foot": f["x_foot"], "x_top": plan.flight_x_top(f), "z0": f["z0"],
                         "z_top": plan.flight_z_top(f), "risers": f["risers"], "lane": f["lane"],
                         "rails": plan.flight_rail_sides(variant, f)}
                        for f in plan.flights()],
            "floors": [{k: v for k, v in fl.items() if k in ("name", "z_top", "holes", "rails")}
                       for fl in plan.floors(variant)],
            "min_headroom": plan.min_headroom(variant), "headroom_asked": plan.HEADROOM,
            "ground_door": plan.ground_door(),
            "front_piers": list(plan.FRONT_PIERS) if variant == "a" else None,
            "catapult_spot_5_50_x_5_72": plan.fit_rect(variant, 5.50, 5.72),
            "pinned_f3_foot": plan.PINNED_F3_FOOT, "f3_foot_used": plan.F3_FOOT,
            "stub_x_in": plan.stub_inner_x(variant), "platform_offset": plan.platform_offset(variant),
            "pier_angles": list(plan.PIER_ANGLES_B) if variant == "b" else None,
            "drum_steps": plan.DRUM_STEPS if variant == "b" else None}


def main():
    args = script_args()
    out = flag_value(args, "--out")
    variant = flag_value(args, "--variant") or "a"
    with job(report_path(out)) as result:
        os.makedirs(out, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs = build_all(variant)
        bpy.context.view_layer.update()
        result["objects"] = {o.name: wall.mesh_stats(o) for o in objs}
        result["total_tris"] = sum(s["tris"] for s in result["objects"].values())
        result["total_tris_before_cells"] = sum(s["tris_before_cut"] for s in result["objects"].values())
        result["budget"] = plan.TRIANGLE_BUDGET
        result["uv_check"] = wall.uv_check(objs)
        result["recess"] = {"meshes": len(RECESS_LOG), "faces": sum(r["recess_faces"] for r in RECESS_LOG),
                            "faces_on_the_trim_sheet": sum(r["recess_trim_faces"] for r in RECESS_LOG)}
        result["plan"] = plan_report(variant)
        base = plan.BASE_NAMES[variant]
        fbx, blend = os.path.join(out, base + ".fbx"), os.path.join(out, base + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend)
        export_fbx(fbx, objs)
        result["fbx"], result["blend"] = fbx, blend
        with open(os.path.join(out, "tower_build_report.json"), "w", encoding="utf-8") as f:
            json.dump({k: result[k] for k in ("objects", "total_tris", "total_tris_before_cells", "budget", "uv_check",
                                              "recess", "plan")}, f, indent=1)


if __name__ == "__main__":
    main()
