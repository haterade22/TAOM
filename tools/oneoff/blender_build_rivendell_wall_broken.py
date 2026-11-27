"""Build of the Rivendell breached wall, version 4 (picture b1), real assets for a Modding Kit import. Headless:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_build_rivendell_wall_broken.py --
        --out <dir>

The same wall as tools/oneoff/blender_build_rivendell_wall.py (its builder functions are imported), with a
breach in the middle bay cut by boolean differences, so everything that still stands is that wall: the
foundation, plinth, piers, ribs, blind arches, string course, parapet and inner rail. The end sections are
the joint profile of tools/oneoff/rivendell_joint_profile.py, with their half piers, because no cut comes
nearer an end than 0.3 m of the parapet. How a real wall breaks, as numbers: tools/oneoff/rivendell_breach_profile.py.

  skins  two cutters (the outer and the inner layer of dressed ashlar, 0.55 m each) with a toothed edge: every step
         is on a bed joint and ends on a head joint that the stone texture draws. The faces that the cut makes
         (the end and the top of a tooth, the back of the skin) take the heap set; the dressed face is the wall's
         own. The parapet and the rail end in teeth of their own, and one side of the outer parapet loses a panel.
  core   one cutter between the skins whose two faces are lumpy grids (bp.core_grid): 0.1 to 0.55 m behind the teeth,
         leaning back toward the standing wall, on the heap set.

Sub-objects, named as the Gondor control splits its pieces:
  rivendell_castle_wall_20m_a_dest.wall    what still stands
  rivendell_castle_wall_20m_a_dest.rubble  the mound that fills the gap and spills out on both sides, on the heap set
  rivendell_castle_wall_20m_a_dest.brick   the stones on the mound: angular broken stones, whole and half ashlar
                                           blocks (a dressed face or two, the other faces broken) and carved pieces

Every face of the heap set is mapped by bp.area_uv, which gives it the exact density TD however it leans. Vertex
colour "Col": what stands keeps the colour of the wall; the torn faces take a darker, dusty tone that fades out over
bp.DUST_REACH on the faces next to them, on the mound and on the stones.

Writes <out>/rivendell_castle_wall_20m_a_dest.blend, .fbx and dest_build_report.json (bounds, triangles, tear, gap,
reach, crest, walk paths, flat planes of the core, zero-area faces). The Store launcher prints nothing: wait for
<out>/_report/DONE.txt (tools/blender/blender_job.py).
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import blender_build_rivendell_wall as WB  # noqa: E402
import rivendell_breach_profile as bp  # noqa: E402
import rivendell_joint_profile as jp  # noqa: E402
from blender_job import export_fbx, flag_value, job, report_path, script_args  # noqa: E402

NAME = "rivendell_castle_wall_20m_a_dest"
STONE, RUBBLE, TRIM, HEAP = WB.STONE, WB.RUBBLE, WB.TRIM, WB.HEAP
SKIRT = 0.3            # the mound is closed at this depth below the ground line
FLOOR_Z = -0.28        # no loose block or carved piece lies lower
ANGULAR_STONES = 150
ASHLAR_BLOCKS = 36
LOOSE_TONE = 0.93      # fallen stone is a little darker than the wall that stands
WALL_FOOT_Y = 3.6      # loose pieces lie past the pier fronts (3.5 m at the ground)
TEAR_SEED = 7
MEMBER_SHIFTS = (0.003, 0.006, 0.009)   # the loose members (ribs, arch frames, piers, mouldings) are cut this far behind the
                                        # prisms, each member by one of three shifts, so two members never share a cut plane
CUT_X_MAX = 9.8        # a shell that lies outside +-CUT_X_MAX is not cut (the teeth of the parapet end at 9.7)


# ---- the cutters ----------------------------------------------------------------------------------
def mesh_object(name, verts, faces, mat):
    """A closed mesh of triangles, one slot (mat), as a linked object with normals out."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in verts]
    for f in faces:
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def skin_cutter(mats, tear, skin, shift=0.0):
    """The prism of one skin: the outline of its two edges (x, z), with a chip at the corner of most teeth
    (bp.chip_edge), extruded over Y from the back of the skin (with an overlap into the core) to past the face. With
    `shift` every edge moves that far from the centre line and up: the loose members that stand in the wall (the backs of the
    ribs, the arch frames and the piers lie inside the body) are cut by a shifted copy, so that their cut faces and the
    cut faces of the body are never in one plane."""
    rng = random.Random(len(skin) * 101 + tear["seed"])
    edges = {side: [(e + shift, z + (shift if bp.FLOOR + 1e-9 < z < bp.TOP - 1e-9 else 0.0))
                    for e, z in bp.chip_edge(tear["skins"][skin][side], rng)] for side in ("left", "right")}
    poly = bp.gap_polygon(edges["left"], edges["right"])
    y0, y1 = (bp.SKIN_IN - bp.SKIN_OVERLAP, 4.2) if skin == "outer" else (-3.2, -(bp.SKIN_IN - bp.SKIN_OVERLAP))
    B = WB.Build()
    WB.prism_loops(B, [poly], lambda u, v, k, y0=y0, y1=y1: Vector((u, y1 if k else y0, v)))
    me = bpy.data.meshes.new("cut_" + skin + ("_member" if shift else ""))
    B.bm.to_mesh(me)
    B.bm.free()
    me.materials.append(mats[HEAP])
    ob = bpy.data.objects.new("cut_" + skin + ("_member" if shift else ""), me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def core_cutter(mats, grid):
    """The core as a closed mesh: the lumpy surface of each side (a grid), the floor and the top as strips across the
    gap, and the two backs of the skins (their Y moves from row to row, so the back is no single plane)."""
    rows, cols = len(grid["zs"]), bp.CORE_COLS
    verts, faces = [], []
    left = [[len(verts) + j * cols + i for i in range(cols)] for j in range(rows)]
    for j in range(rows):
        verts += [(-grid["xl"][j][i], grid["ys"][j][i], grid["zs"][j]) for i in range(cols)]
    right = [[len(verts) + j * cols + i for i in range(cols)] for j in range(rows)]
    for j in range(rows):
        verts += [(grid["xr"][j][i], grid["ys"][j][i], grid["zs"][j]) for i in range(cols)]
    for j in range(rows - 1):
        for i in range(cols - 1):
            for g in (left, right):
                faces.append((g[j][i], g[j][i + 1], g[j + 1][i + 1], g[j + 1][i]))
    for j in (0, rows - 1):
        for i in range(cols - 1):
            faces.append((left[j][i], left[j][i + 1], right[j][i + 1], right[j][i]))
    for i in (0, cols - 1):
        for j in range(rows - 1):
            faces.append((left[j][i], left[j + 1][i], right[j + 1][i], right[j][i]))
    return mesh_object("cut_core", verts, faces, mats[HEAP])


# ---- the standing wall: the solid wall, cut --------------------------------------------------------
def split_shells(ob):
    """The loose parts of a mesh object as separate objects (the first is `ob`)."""
    before = set(bpy.data.objects)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    return [ob] + [o for o in bpy.data.objects if o not in before]


def touches_gap(ob):
    co = [v.co for v in ob.data.vertices]
    return min(p.x for p in co) < CUT_X_MAX and max(p.x for p in co) > -CUT_X_MAX and max(p.z for p in co) > bp.FLOOR


def cut_shell(ob, cutters):
    """Difference of the shell and all the cutters at once (one collection operand: the exact solver cuts with
    the union of them in one pass). The faces that the cut makes take the cutter's material (the heap set)."""
    coll = bpy.data.collections.new("cutters")
    bpy.context.scene.collection.children.link(coll)
    for c in cutters:
        coll.objects.link(c)
    m = ob.modifiers.new("cut", "BOOLEAN")
    m.operation, m.solver, m.operand_type, m.collection = "DIFFERENCE", "EXACT", "COLLECTION", coll
    m.material_mode = "TRANSFER"
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    ob.modifiers.clear()
    old = ob.data
    ob.data = me
    bpy.data.meshes.remove(old)
    for c in cutters:
        coll.objects.unlink(c)
    bpy.data.collections.remove(coll)


def kind_of(name):
    return WB.KINDS[WB.MAT_NAMES.index(name)] if name in WB.MAT_NAMES else "trim"


def reuv_cut(ob):
    """Every stone, plinth and heap face of a cut shell takes the rule of its material: the wall's own rules for the
    stone and the plinth (so the seam test and the courses agree), bp.area_uv for the heap set. The trim keeps the
    UV that the cut gave it by interpolation."""
    me = ob.data
    names = [m.name if m else "" for m in me.materials]
    uv = me.uv_layers[0].data
    counts = {"stone": 0, "plinth": 0, "heap": 0}
    for p in me.polygons:
        name = names[p.material_index]
        co = [tuple(me.vertices[me.loops[li].vertex_index].co) for li in p.loop_indices]
        n = tuple(p.normal)
        if name == WB.MAT_NAMES[STONE]:
            rule, key = jp.world_uv, "stone"
        elif name == WB.MAT_NAMES[RUBBLE]:
            rule, key = jp.plinth_uv, "plinth"
        elif name == WB.MAT_NAMES[HEAP]:
            for li, t in zip(p.loop_indices, bp.area_uv(co, n)):
                uv[li].uv = t
            counts["heap"] += 1
            continue
        else:
            continue
        for li, c in zip(p.loop_indices, co):
            uv[li].uv = rule(n, c, WB.X0)
        counts[key] += 1
    return counts


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def fix_collinear(ob):
    """After the cut: remove triangles of no area, and split every face that has three collinear corners into a fan
    from a corner that leaves no zero-area triangle (jp.fan_apex), or by the beauty method when no corner will do.
    A triangle of no area gives the vertices round it a wrong tangent (a dark hairline in the normal map). The UV
    and the colour of the corners go with the faces."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    before = len(bm.faces)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=list(bm.edges))
    uvl, col = bm.loops.layers.uv.active, bm.loops.layers.color.active
    fanned, odd = 0, []
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
        data = {lp.vert: (tuple(lp[uvl].uv), tuple(lp[col]) if col else None) for lp in f.loops}
        mat = f.material_index
        order = [verts[(k + i) % n] for i in range(n)]
        bm.faces.remove(f)
        for i in range(1, n - 1):
            t = bm.faces.new((order[0], order[i], order[i + 1]))
            t.material_index = mat
            for lp in t.loops:
                lp[uvl].uv = data[lp.vert][0]
                if col:
                    lp[col] = data[lp.vert][1]
        fanned += 1
    if odd:
        bmesh.ops.triangulate(bm, faces=odd, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    bm.to_mesh(ob.data)
    bm.free()
    return {"faces_before": before, "fanned": fanned, "beauty": len(odd)}


def is_body(ob):
    """One of the big prisms of the wall (foundation, plinth, body: each reaches across the whole thickness). The
    ribs, arch frames, piers and mouldings are the loose members; they are cut a few millimetres behind the prisms."""
    ys = [v.co.y for v in ob.data.vertices]
    return max(ys) - min(ys) > 4.0


def build_wall_object(mats, cutters, member_groups):
    B = WB.Build(WB.X0)
    WB.build_wall(B, WB.wall_run())
    parts = [WB.make_object("tmp_wall", B, mats)]
    for i, (x0, x1) in enumerate(WB.PARTS, 1):
        B = WB.Build(WB.X0)
        WB.build_parapet(B, x0, x1)
        parts.append(WB.make_object(f"tmp_m{i}", B, mats))
    B = WB.Build(WB.X0)
    WB.build_rail(B, WB.X0, WB.X1)
    parts.append(WB.make_object("tmp_rail", B, mats))
    shells = []
    for ob in parts:
        shells += split_shells(ob)
    kept, cut_count, members = [], 0, 0
    for ob in shells:
        if touches_gap(ob):
            member = not is_body(ob)
            cut_shell(ob, member_groups[members % len(member_groups)] if member else cutters)
            members += member
            cut_count += 1
            if len(ob.data.vertices) == 0:
                bpy.data.objects.remove(ob)
                continue
        kept.append(ob)
    wall = join(kept, f"{NAME}.wall")
    fixed = fix_collinear(wall)
    counts = reuv_cut(wall)
    return wall, {"shells_before_cut": len(shells), "shells_cut": cut_count, "shells_joined": len(kept),
                  "faces_remapped": counts, "collinear_fix": fixed, "members_cut_behind": members}


# ---- the mound -------------------------------------------------------------------------------------
def boundary_rings(adjacency):
    """The closed loops (lists of vertices) of a chain of boundary edges. A vertex with more than two
    neighbours (two parts of the outline touching) is an error: the cap could not be cut."""
    if any(len(n) != 2 for n in adjacency.values()):
        raise ValueError("the heap outline touches itself")
    rings, seen = [], set()
    for start in adjacency:
        if start in seen:
            continue
        ring, prev, cur = [], None, start
        while cur not in seen:
            seen.add(cur)
            ring.append(cur)
            nxt = [n for n in adjacency[cur] if n is not prev and n not in seen]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
        rings.append(ring)
    return rings


def heap_faces_uv(B, faces):
    """Every face of `faces` takes bp.area_uv (the exact density) and the heap slot."""
    for f in faces:
        f.material_index = HEAP
        uvs = bp.area_uv([tuple(v.co) for v in f.verts], tuple(f.normal))
        B.pend.append((f, lambda v, fc, m=dict(zip(f.verts, uvs)): m[v]))


def jittered(rng, x, y, z):
    """A node of the mound moved in the plane by up to 0.14 m outside the path, so that the faces are no regular
    grid; the nodes at the floor of the field and the nodes of the path stay."""
    env = bp.smooth((abs(x) - bp.NOISE_X0) / 1.0) * (1.0 if z > bp.HEAP_FLOOR + 0.05 else 0.0)
    return (x + rng.uniform(-0.14, 0.14) * env, y + rng.uniform(-0.14, 0.14) * env, z)


def build_rubble(heap, mats):
    """The mound as a closed shell: the height field as triangles from the floor of the field (below the ground
    line) up, a skirt down to -SKIRT round its outline and a cap under it. The cap is cut by bp.ear_clip, so it
    has no zero-area face."""
    B = WB.Build(WB.X0)
    bm = B.bm
    rng_j = random.Random(21)
    xs, ys, z = heap.xs, heap.ys, heap.z
    top, quads = {}, []
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            corners = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
            if max(z[c] for c in corners) <= bp.HEAP_FLOOR + 1e-6:
                continue
            vs = []
            for c in corners:
                if c not in top:
                    top[c] = B.vert(jittered(rng_j, xs[c[0]], ys[c[1]], z[c]))
                vs.append(top[c])
            quads.append(B.face(*vs))
    bmesh.ops.triangulate(bm, faces=[f for f in quads if f is not None])
    bottom, adjacency = {}, {}
    for e in [e for e in bm.edges if len(e.link_faces) == 1]:
        a, b = e.verts
        for v in (a, b):
            if v not in bottom:
                bottom[v] = B.vert((v.co.x, v.co.y, -SKIRT))
        B.face(a, b, bottom[b], bottom[a])
        adjacency.setdefault(bottom[a], []).append(bottom[b])
        adjacency.setdefault(bottom[b], []).append(bottom[a])
    for ring in boundary_rings(adjacency):
        pts = [(v.co.x, v.co.y) for v in ring]
        for t in bp.ear_clip(pts):
            B.face(*[ring[k] for k in t])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    heap_faces_uv(B, list(bm.faces))
    return WB.make_object(f"{NAME}.rubble", B, mats, cells=False)


# ---- the stones ------------------------------------------------------------------------------------
def pose(pos, yaw, pitch, roll):
    return (Matrix.Translation(pos) @ Matrix.Rotation(math.radians(yaw), 4, "Z")
            @ Matrix.Rotation(math.radians(pitch), 4, "X") @ Matrix.Rotation(math.radians(roll), 4, "Y"))


class Pile:
    """The ground of the mound plus the tops of the pieces already placed on it, on a 0.3 m grid, so that the next
    piece rests on a stone that lies under it."""
    CELL = 0.3

    def __init__(self, heap):
        self.heap = heap
        self.top = {}

    def cell(self, x, y):
        return (int(round(x / self.CELL)), int(round(y / self.CELL)))

    STACK = 0.55   # a piece rests on the stones under it, but never higher than this over the mound

    def at(self, x, y):
        ground = self.heap.at(x, y)
        return min(max(ground, self.top.get(self.cell(x, y), -9.0)), ground + self.STACK)

    def add(self, samples):
        """Raise the grid to the points of a placed piece (its vertices and points over its faces)."""
        for p in samples:
            c = self.cell(p[0], p[1])
            if p[2] > self.top.get(c, -9.0):
                self.top[c] = p[2]


def surface_normal(pile, x, y, step=0.5):
    gx = (pile.heap.at(x + step, y) - pile.heap.at(x - step, y)) / (2.0 * step)
    gy = (pile.heap.at(x, y + step) - pile.heap.at(x, y - step)) / (2.0 * step)
    return Vector((-gx, -gy, 1.0)).normalized()


def append_build(M, B, matrix):
    """Copy the finished local shell `B`, moved by `matrix`, into the master build `M`, with its UVs (made in
    the block's own frame, so the density holds however the block lies). Returns points on the moved shell (its
    vertices and a sample over every face) for the pile that the next piece rests on."""
    B.apply_uv()
    bmesh.ops.transform(B.bm, matrix=matrix, verts=B.bm.verts)
    made = {v: M.bm.verts.new(v.co) for v in B.bm.verts}
    samples = [tuple(v.co) for v in B.bm.verts]
    for f in B.bm.faces:
        nf = M.bm.faces.new([made[v] for v in f.verts])
        nf.material_index = f.material_index
        for a, b in zip(f.loops, nf.loops):
            b[M.uvl].uv = a[B.uvl].uv
        vs = [v.co for v in f.verts]
        for k in range(1, len(vs) - 1):
            for i in range(1, 4):
                for j in range(0, 4 - i):
                    w = (1.0 - (i + j) / 4.0, i / 4.0, j / 4.0)
                    samples.append(tuple(w[0] * vs[0][c] + w[1] * vs[k][c] + w[2] * vs[k + 1][c] for c in range(3)))
    B.bm.free()
    return samples


def centre_of(B):
    co = [v.co for v in B.bm.verts]
    return Vector([0.5 * (min(p[i] for p in co) + max(p[i] for p in co)) for i in range(3)])


def angular_stone(rng, size):
    """A broken stone: the convex hull of the points of bp.stone_points (two in three a rock, one in three a box with
    its corners cut), every face on the heap set."""
    B = WB.Build(0.0)
    style = "rock" if rng.random() < 0.67 else "cut"
    vs = [B.vert(p) for p in bp.stone_points(rng, size, style=style)]
    res = bmesh.ops.convex_hull(B.bm, input=vs, use_existing_faces=False)
    loose = {v for v in B.bm.verts if not v.link_faces}
    bmesh.ops.delete(B.bm, geom=list(loose), context="VERTS")
    bmesh.ops.recalc_face_normals(B.bm, faces=list(B.bm.faces))
    B.bm.normal_update()
    heap_faces_uv(B, list(B.bm.faces))
    return B


def stone_stone_size(rng):
    big = rng.random() < 0.25
    return ((0.45, 0.9), (0.4, 0.75), (0.3, 0.6)) if big else ((0.25, 0.55), (0.22, 0.5), (0.2, 0.42))


def ashlar_block(rng, course, k, half):
    """A whole or half block of the wall's own stone: block `k` of `course` as the texture draws it. Its front
    face is the block's rectangle of the texture (the joints at its edges), a second face (the top, or an end) is
    dressed too in half the cases, and every other face is broken stone (the heap set). The back corners are moved
    by a few centimetres, and the broken end of a half block by more."""
    length, height = bp.block_dims(course, k)
    u0, v0 = bp.block_rect(course, k)
    depth = rng.uniform(0.42, 0.48)
    side = rng.choice((-1, 1))
    if half:
        length, u0 = 0.5 * length, (u0 if side < 0 else u0 + 0.5 * bp.block_dims(course, k)[0] * jp.TD)
    depth = min(depth, 0.95 * length)
    a, b, d = 0.5 * length, 0.5 * height, 0.5 * depth
    j = lambda s=0.03: rng.uniform(-s, s)
    broken_end = (a * side * -1.0)
    corner = {}
    for sx in (-1, 1):
        for sz in (-1, 1):
            corner[(sx, 1, sz)] = (sx * a, d, sz * b)
            wob = 0.06 if (half and sx * side * -1 > 0) else 0.03
            corner[(sx, -1, sz)] = (sx * a + j(wob), -d + j(), sz * b + j())
    B = WB.Build(0.0)
    vert = {key: B.vert(p) for key, p in corner.items()}

    def quad(keys, kind):
        return kind, [vert[k_] for k_ in keys]

    spec = [quad(((-1, 1, -1), (1, 1, -1), (1, 1, 1), (-1, 1, 1)), "front"),
            quad(((-1, -1, -1), (-1, -1, 1), (1, -1, 1), (1, -1, -1)), "back"),
            quad(((-1, 1, 1), (1, 1, 1), (1, -1, 1), (-1, -1, 1)), "top"),
            quad(((-1, 1, -1), (-1, -1, -1), (1, -1, -1), (1, 1, -1)), "bottom"),
            quad(((1, 1, -1), (1, -1, -1), (1, -1, 1), (1, 1, 1)), "end_r"),
            quad(((-1, 1, -1), (-1, 1, 1), (-1, -1, 1), (-1, -1, -1)), "end_l")]
    extra = rng.choice(("top", "end_l", "end_r", None, None))
    if half and extra == ("end_l" if side < 0 else "end_r"):
        extra = "top"      # the cut end of a half block is broken stone
    u_mid = u0 + 0.5 * (length - depth) * jp.TD
    faces = []
    for kind, vs in spec:
        f = B.face(*vs)
        faces.append((kind, f))
    B.seal([f for _, f in faces])
    for kind, f in faces:
        if kind == "front":
            f.material_index = STONE
            B.pend.append((f, lambda v, fc, u0=u0, v0=v0, a=a, b=b: (u0 + jp.TD * (v.co.x + a), v0 + jp.TD * (v.co.z + b))))
        elif kind == extra:
            f.material_index = STONE
            if kind == "top":
                B.pend.append((f, lambda v, fc, u0=u0, a=a, d=d, h=height, dep=depth: (
                    u0 + jp.TD * (v.co.x + a), v0 + jp.TD * (0.5 * (h - dep) + v.co.y + d))))
            else:
                B.pend.append((f, lambda v, fc, um=u_mid, b=b, d=d: (um + jp.TD * (v.co.y + d), v0 + jp.TD * (v.co.z + b))))
        else:
            tris = bmesh.ops.triangulate(B.bm, faces=[f], quad_method="BEAUTY")["faces"]
            heap_faces_uv(B, tris)
    return B


def carved_arch(width, height):
    B = WB.Build(-width / 2)
    WB.arch_frame(B, lambda u, v, k: Vector((u, -0.1 + 0.22 * k, v)), width, height, 0.14, 8)
    return B


def carved_parapet(length):
    """A short parapet part, as the wall builds it: the slab with its openings, the frieze and the cap."""
    B = WB.Build(-length / 2)
    WB.build_parapet(B, -length / 2, length / 2)
    return B


def carved_cap(length):
    B = WB.Build(-length / 2)
    cap = [(y - 2.3, z - 14.2) for y, z in jp.section()["parapet_cap"]]
    B.trim(WB.prism_x(B, cap, -length / 2, length / 2), "S2", 0)
    body = [(-0.25, -0.5), (0.25, -0.5), (0.25, -0.2), (-0.25, -0.2)]
    B.world(WB.prism_x(B, body, -length / 2 + 0.02, length / 2 - 0.02), STONE)
    return B


def carved_cornice(length):
    """A block of wall with the string course on its face."""
    B = WB.Build(-length / 2)
    B.world(WB.box(B, -length / 2, length / 2, -0.4, 0.4, -0.4, 0.4), STONE)
    prof = [(y - jp.Y_OUT + 0.37, z - 10.7) for y, z in jp.section()["string_course"]]
    B.trim(WB.prism_x(B, prof, -length / 2, length / 2), "S2", 0)
    return B


def carved_pier_facing():
    B = WB.Build(-0.6)
    prof = bp.clip_polygon_z(jp.pier_profile(niche=False), 0.9, 3.0)
    prof = [(y - 3.0, z - 1.95) for y, z in prof]
    B.world(WB.prism_x(B, prof, -0.6, 0.6), STONE)
    return B


def carved_collar():
    """The carved collar of a pier, a short piece with the stone of the pier under it."""
    B = WB.Build(-0.66)
    B.trim(WB.prism_x(B, jp.collar_profile(), -jp.COLLAR_HALF_W, jp.COLLAR_HALF_W), "S2", 0)
    return B


def carved_pilaster():
    """A 1.2 m piece of the inner pilaster (the whole pilaster is 7.3 m tall), on the strip of dressed stone."""
    prof = bp.clip_polygon_z(jp.section()["inner_pilaster"], jp.INNER_PLINTH_Z1, jp.INNER_PLINTH_Z1 + 1.2)
    B = WB.Build(-0.45)
    B.trim(WB.prism_x(B, prof, -0.45, 0.45), "S1", 0)
    return B


def carved_rib(clip):
    """A rib limb of the wall, whole or cut at an angle by a plane at X = clip."""
    B = WB.Build(-1.0)
    WB.build_rib(B, 0.0, 1, WB.LONG_RIB, 10, clip_x=clip)
    return B


# (kind, how many, builder, lies flat). A flat piece lies on its back, as a fallen face does.
CARVED_KINDS = (
    ("arch_big", 3, lambda rng: carved_arch(1.6, 2.4), True),
    ("arch_small", 2, lambda rng: carved_arch(1.3, 2.0), True),
    ("parapet_a", 2, lambda rng: carved_parapet(1.8), True),
    ("parapet_b", 1, lambda rng: carved_parapet(1.2), True),
    ("cap", 3, lambda rng: carved_cap(rng.uniform(1.3, 1.9)), False),
    ("cornice", 2, lambda rng: carved_cornice(rng.uniform(1.2, 1.8)), False),
    ("pier_facing", 2, lambda rng: carved_pier_facing(), True),
    ("collar", 2, lambda rng: carved_collar(), False),
    ("pilaster", 1, lambda rng: carved_pilaster(), True),
    ("rib_whole", 3, lambda rng: carved_rib(None), True),
    ("rib_cut", 3, lambda rng: carved_rib(rng.uniform(1.0, 2.3)), True),
)


def carved_schedule(rng):
    """The carved pieces in the order they are placed, shuffled so the big ones do not all come first."""
    order = [(k, b, flat) for k, n, b, flat in CARVED_KINDS for _ in range(n)]
    rng.shuffle(order)
    return order


def gap_room(tear, z_top):
    """The |x| that a stone standing on the mound may not pass inside the gap: the nearest tooth of any skin below
    `z_top`."""
    best = 99.0
    z = bp.FLOOR
    while z < min(z_top, 11.0):
        for skin in ("outer", "inner"):
            for side in ("left", "right"):
                best = min(best, bp.edge_at(tear["skins"][skin][side], z))
        z += 0.2
    return best


def place(M, rng, pile, tear, placed, B, flat, tilt, sink_range=(0.03, 0.12), tries=120, overlap=0.0):
    """Put one finished local shell on the mound: a position off the clear path, its own yaw, tilted to the slope of
    the mound, lowered until its lowest point lies a little in the ground (never below FLOOR_Z), resting on the
    stones under it. Inside the gap it must also stand clear of the teeth. Returns the label (x, y, r) or None when
    no free place was found in `tries`."""
    centre = centre_of(B)
    for _ in range(tries):
        side = 1.0 if rng.random() < 0.6 else -1.0
        x = rng.choice((-1, 1)) * min(2.2 + rng.expovariate(1.0 / 1.5), 6.6)   # more stones along the path than far off it
        y = side * rng.uniform(0.3, 8.6)
        if pile.heap.at(x, y) < 0.25:
            continue
        n = surface_normal(pile, x, y)
        align = Vector((0.0, 0.0, 1.0)).rotation_difference(n).to_matrix().to_4x4()
        pitch = (90.0 + rng.uniform(-12, 12)) if flat else rng.uniform(-tilt, tilt)
        roll = rng.uniform(-8, 8) if flat else rng.uniform(-tilt, tilt)
        local = (Matrix.Translation((x, y, 0.0)) @ align @ Matrix.Rotation(math.radians(rng.uniform(0, 360)), 4, "Z")
                 @ Matrix.Rotation(math.radians(pitch), 4, "X") @ Matrix.Rotation(math.radians(roll), 4, "Y")
                 @ Matrix.Translation(-centre))
        pts = [local @ v.co for v in B.bm.verts]
        r = max(math.hypot(p.x - x, p.y - y) for p in pts)   # the reach from (x, y), where the centre of the piece lies
        if not bp.outside_path(x, r):
            continue
        if abs(y) - r * 0.6 < WALL_FOOT_Y and abs(x) + 0.8 * r > gap_room(tear, pile.heap.at(x, y) + 2.0 * r) - 0.2:
            continue   # in the gap or at the foot of a torn end: inside the teeth only
        if any(math.hypot(x - qx, y - qy) < (0.5 - overlap) * (r + qr) for qx, qy, qr in placed):
            continue
        dz = bp.rest_offset([(p.x, p.y, p.z) for p in pts], pile.at, sink=rng.uniform(*sink_range), floor_z=FLOOR_Z)
        pile.add(append_build(M, B, Matrix.Translation((0.0, 0.0, dz)) @ local))
        placed.append((x, y, r))
        return (x, y, r)
    B.bm.free()
    return None


def build_bricks(heap, tear, mats, rng):
    """The carved pieces first (they are the ones that should be seen), then the whole and half blocks, then the
    angular stones that make the mound a heap of stones."""
    M = WB.Build(WB.X0)
    pile = Pile(heap)
    placed, labels, blocks, stones = [], [], 0, 0
    for kind, build, flat in carved_schedule(rng):
        if place(M, rng, pile, tear, placed, build(rng), flat, 12.0, overlap=0.1):
            labels.append(kind)
    tries = 0
    while blocks < ASHLAR_BLOCKS and tries < ASHLAR_BLOCKS * 8:
        tries += 1
        B = ashlar_block(rng, rng.randrange(4), rng.randrange(2), rng.random() < 0.4)
        if place(M, rng, pile, tear, placed, B, False, 20.0, sink_range=(0.05, 0.2), overlap=0.2):
            blocks += 1
    tries = 0
    while stones < ANGULAR_STONES and tries < ANGULAR_STONES * 8:
        tries += 1
        if place(M, rng, pile, tear, placed, angular_stone(rng, stone_stone_size(rng)), False, 30.0,
                 sink_range=(0.08, 0.25), overlap=0.3):
            stones += 1
    return WB.make_object(f"{NAME}.brick", M, mats, cells=False), blocks, stones, labels


# ---- dust on the torn faces ------------------------------------------------------------------------
def torn_distances(ob, trees):
    """Distance of every vertex of `ob` to the nearest surface of any cutter."""
    out = []
    for v in ob.data.vertices:
        best = 1e9
        for t in trees:
            hit = t.find_nearest(v.co)
            if hit[0] is not None and hit[3] < best:
                best = hit[3]
        out.append(best)
    return out


def apply_dust(ob, trees, loose=False, mottle=False, torn_tol=1e-3):
    """Darken the vertex colour "Col" of `ob` near the torn faces. A polygon whose every vertex lies on a cutter
    is a torn face: the cutter gave it no colour, so it takes the tone of its material (jp.vertex_tone) with
    the full dust. Every other loop keeps its colour, darkened by bp.dust_tone at its vertex's distance.
    `loose` (the blocks) also takes LOOSE_TONE, `mottle` (the heap) bp.heap_tone. Returns the counts of torn faces and darkened loops."""
    me = ob.data
    attr = me.color_attributes["Col"]
    n = len(attr.data)
    flat = [0.0] * (4 * n)
    attr.data.foreach_get("color_srgb", flat)
    dist = torn_distances(ob, trees) if trees else [1e9] * len(me.vertices)
    torn = dusted = 0
    for p in me.polygons:
        kind = kind_of(me.materials[p.material_index].name if me.materials[p.material_index] else "")
        is_torn = all(dist[v] < torn_tol for v in p.vertices)
        torn += is_torn
        for li in p.loop_indices:
            v = me.vertices[me.loops[li].vertex_index]
            d = dist[v.index]
            if is_torn:
                rgb = jp.vertex_tone(kind, v.co.x - WB.X0, v.co.z, 0.0)
                d = 0.0
            else:
                rgb = tuple(flat[4 * li:4 * li + 3])
            out = bp.dust_tone(rgb, d)
            if not is_torn:     # the dust of a tooth near an end fades out toward the joint plane, so the seam is clean
                w = bp.smooth((WB.X1 - abs(v.co.x)) / bp.DUST_REACH)
                out = tuple(a + (b - a) * w for a, b in zip(rgb, out))
            if loose:
                out = tuple(c * LOOSE_TONE for c in out)
            if mottle:
                out = tuple(c * bp.heap_tone(v.co.x, v.co.y, v.co.z) for c in out)
            if out != tuple(rgb):
                dusted += 1
            flat[4 * li:4 * li + 4] = [out[0], out[1], out[2], 1.0]
    attr.data.foreach_set("color_srgb", flat)
    return {"torn_faces": torn, "loops_darkened": dusted, "loops": n}


# ---- measurements ----------------------------------------------------------------------------------
def islands(ob):
    me = ob.data
    parent = list(range(len(me.vertices)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for p in me.polygons:
        vs = list(p.vertices)
        for v in vs[1:]:
            parent[find(v)] = find(vs[0])
    return len({find(v.index) for v in me.vertices})


def zero_area_faces(ob, limit=1e-9):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    n = sum(1 for f in bm.faces if f.calc_area() < limit)
    bm.free()
    return n


def top_height(trees, x, y):
    best = 0.0
    for t in trees:
        hit = t.ray_cast(Vector((x, y, 40.0)), Vector((0.0, 0.0, -1.0)))
        if hit[0] is not None and hit[0].z > best:
            best = hit[0].z
    return best


def gap_at_deck(trees, y):
    def edge(sign):
        x = 0.0
        while x < 10.0 and top_height(trees, sign * x, y) < 11.9:
            x += 0.05
        return x

    left, right = edge(-1), edge(1)
    return {"left_x": -round(left, 3), "right_x": round(right, 3), "width": round(left + right, 3)}


def flatness(ob):
    return {"vertices_within_0p01_of_an_end": sum(1 for v in ob.data.vertices if abs(v.co.x) > 9.99),
            "vertices_off_the_plane": sum(1 for v in ob.data.vertices
                                          if 9.99 < abs(v.co.x) and min(abs(v.co.x - 10.0), abs(v.co.x + 10.0)) > 1e-5)}


def heap_flat_planes(wall, heap):
    """The largest group of triangles of the heap set in the wall that lie in one plane (normals within 4 degrees of
    the first of the group, joined by edges). Only triangles above the mound count (the floor of the cut and the
    cut faces under the mound are buried): all of them, and the ones of the core (|y| under 1.9 m)."""
    me = wall.data
    me.calc_loop_triangles()
    slot = [i for i, m in enumerate(me.materials) if m and m.name == WB.MAT_NAMES[HEAP]]
    tris_all, tris_core = [], []
    for t in me.loop_triangles:
        if t.material_index not in slot:
            continue
        pts = tuple(tuple(me.vertices[i].co) for i in t.vertices)
        cx, cy, cz = (sum(p[k] for p in pts) / 3.0 for k in range(3))
        if cz < heap.at(cx, cy) + 0.03:
            continue
        tris_all.append(pts)
        if all(abs(p[1]) < 1.9 for p in pts):
            tris_core.append(pts)
    area_all, at_all, n_all = bp.largest_flat_patch(tris_all)
    return {"triangles_all": len(tris_all), "largest_plane_m2_all": round(area_all, 3),
            "largest_plane_all_at": [round(c, 2) for c in at_all] if at_all else None,
            "largest_plane_all_normal": [round(c, 2) for c in n_all] if n_all else None,
            "triangles_core": len(tris_core), "largest_plane_m2_core": round(bp.flat_patch_area(tris_core), 3)}


def measure(wall, rubble, brick, heap):
    dg = bpy.context.evaluated_depsgraph_get()
    trees_all = [BVHTree.FromObject(o, dg) for o in (wall, rubble, brick)]
    trees_rub = [BVHTree.FromObject(rubble, dg)]
    rv = rubble.data.vertices
    face = jp.Y_OUT
    inside = [v.co.z for v in rv if abs(v.co.y) < face]
    ys_hi = [v.co.y for v in rv if v.co.z >= 0.1]
    gap = {f"y={y}": gap_at_deck(trees_all, y) for y in (-2.4, -1.0, 0.0, 1.0, 2.3)}
    widths = [g["width"] for g in gap.values()]
    height = lambda x, y: top_height(trees_all, x, y)
    return {
        "gap_at_deck": gap,
        "gap_width_min_max": [min(widths), max(widths)],
        "deck_stands_at_each_end_min": round(10.0 - max(max(abs(g["left_x"]), g["right_x"]) for g in gap.values()), 3),
        "rubble_reach_plus_y_outline": round(max(v.co.y for v in rv) - face, 3),
        "rubble_reach_minus_y_outline": round(-min(v.co.y for v in rv) - face, 3),
        "rubble_reach_plus_y_above_0p1m": round(max(ys_hi) - face, 3),
        "rubble_reach_minus_y_above_0p1m": round(-min(ys_hi) - face, 3),
        "rubble_x_extent": [round(min(v.co.x for v in rv), 3), round(max(v.co.x for v in rv), 3)],
        "crest_z_rubble_inside_gap": round(max(inside), 3),
        "crest_z_all_on_path": round(max(top_height(trees_all, x * 0.25, y * 0.25)
                                         for x in range(-6, 7) for y in range(-40, 41)), 3),
        "walk_path_all": bp.path_stats(height),
        "walk_path_rubble_only": bp.path_stats(lambda x, y: top_height(trees_rub, x, y)),
        "walk_path_x_half_2p0_all": bp.path_stats(height, half=2.0),
        "walk_path_x_half_2p0_rubble_only": bp.path_stats(lambda x, y: top_height(trees_rub, x, y), half=2.0),
        "walk_steps_x_half_2p0_all": bp.step_stats(height, half=2.0),
        "wall_end_flatness": flatness(wall),
        "wall_top_z": round(max(v.co.z for v in wall.data.vertices), 3),
        "wall_vertices_below_floor_of_cut": sum(1 for v in wall.data.vertices if v.co.z < -8.001),
        "brick_islands": islands(brick),
        "brick_lowest_z": round(min(v.co.z for v in brick.data.vertices), 4),
        "brick_vertices_below_minus_0p3": sum(1 for v in brick.data.vertices if v.co.z < -0.3),
        "rubble_lowest_z": round(min(v.co.z for v in rv), 4),
        "zero_area_faces": {o.name: zero_area_faces(o) for o in (wall, rubble, brick)},
        "heap_planes_in_the_wall": heap_flat_planes(wall, heap),
    }


def setback_report(tear, grid):
    """The core's set-back behind the teeth, sampled over the core faces: min, median, 90th percentile and max, and
    the share at 0.55 m or less."""
    out = {}
    for side in ("left", "right"):
        vals = []
        for z in [bp.FLOOR + 0.1 * k for k in range(115)]:
            skin = max(bp.edge_at(tear["skins"][s][side], z) for s in ("outer", "inner"))
            for y in (-1.5, 0.0, 1.5):
                vals.append(bp.core_edge(tear, side, y, z) - skin)
        vals.sort()
        n = len(vals)
        out[side] = {"min": round(vals[0], 3), "median": round(vals[n // 2], 3), "p90": round(vals[int(0.9 * (n - 1))], 3),
                     "max": round(vals[-1], 3), "share_at_or_under_0p55": round(sum(1 for v in vals if v <= 0.551) / n, 3)}
    out["flat_plane_of_the_cutter_grid_m2"] = {s: round(bp.flat_patch_area(bp.surface_triangles(grid, s)), 3)
                                              for s in ("left", "right")}
    return out


def tear_report(tear):
    out = {"gone_side": tear["gone"], "seed": tear["seed"]}
    for skin, sides in tear["skins"].items():
        for side, pts in sides.items():
            steps = bp.steps_of(pts)
            out[f"{skin}_{side}"] = {"steps": len(steps), "outward": sum(1 for _, a, b in steps if b > a),
                                     "inward": sum(1 for _, a, b in steps if b < a),
                                     "deck_edge": round(bp.edge_at(pts, 11.9), 3),
                                     "step_lengths": [round(abs(b - a), 2) for _, a, b in steps]}
    return out


def build_all(seed=TEAR_SEED):
    mats = WB.make_materials()
    tear = bp.make_tear(seed)
    grid = bp.core_grid(tear)
    core = core_cutter(mats, grid)
    cutters = [skin_cutter(mats, tear, "outer"), skin_cutter(mats, tear, "inner"), core]
    member_groups = [[skin_cutter(mats, tear, "outer", k), skin_cutter(mats, tear, "inner", k), core] for k in MEMBER_SHIFTS]
    wall, wall_info = build_wall_object(mats, cutters, member_groups)
    heap = bp.Heap()
    rubble = build_rubble(heap, mats)
    brick, blocks, stones, labels = build_bricks(heap, tear, mats, random.Random(5))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    trees = [BVHTree.FromObject(c, dg) for c in cutters]
    wall_info["dust"] = {o.name: apply_dust(o, trees, loose=(o is brick), mottle=(o is rubble)) for o in (wall, rubble, brick)}
    for c in cutters + [c for g in member_groups for c in g[:2]]:
        bpy.data.objects.remove(c)
    return wall, rubble, brick, wall_info, blocks, stones, labels, tear, grid, heap


def main():
    args = script_args()
    out = flag_value(args, "--out")
    seed = int(flag_value(args, "--seed") or TEAR_SEED)
    with job(report_path(out)) as result:
        os.makedirs(out, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        wall, rubble, brick, wall_info, blocks, stones, labels, tear, grid, heap = build_all(seed)
        objs = [wall, rubble, brick]
        bpy.context.view_layer.update()
        result["objects"] = {o.name: WB.mesh_stats(o) for o in objs}
        result["total_tris"] = sum(s["tris"] for s in result["objects"].values())
        result["uv_check"] = WB.uv_check(objs)
        result["wall_build"] = wall_info
        result["tear"] = tear_report(tear)
        result["core"] = setback_report(tear, grid)
        result["tear_seed"] = seed
        result["ashlar_blocks"], result["angular_stones"], result["carved_pieces"] = blocks, stones, labels
        result["blocks_total"] = blocks + stones + len(labels)
        result["measured"] = measure(wall, rubble, brick, heap)
        fbx = os.path.join(out, NAME + ".fbx")
        blend = os.path.join(out, NAME + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend)
        export_fbx(fbx, objs)
        result["fbx"], result["blend"] = fbx, blend
        keys = ("objects", "total_tris", "uv_check", "wall_build", "tear", "core", "tear_seed", "ashlar_blocks",
                "angular_stones", "carved_pieces", "blocks_total", "measured")
        with open(os.path.join(out, "dest_build_report.json"), "w", encoding="utf-8") as f:
            json.dump({k: result[k] for k in keys}, f, indent=1)


if __name__ == "__main__":
    main()
