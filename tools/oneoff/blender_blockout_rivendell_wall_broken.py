"""Concept blockout of the Rivendell broken wall (picture b1), in real metres. Headless Blender:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_blockout_rivendell_wall_broken.py --
        --out <dir> [--detail 1|2|3] [--pass N]

The breached state of the solid wall (tools/oneoff/blender_blockout_rivendell_wall.py, imported as W).
Same frame: the wall runs along X from -10 to +10, the outer face is +Y, Z = 0 is the ground, the deck
is at Z 12.0. The gap is centred on X 0, about 12 m wide at the deck and narrower in steps below.
Three objects, named as the Gondor control splits its pieces:
  rivendell_wall_broken_blockout.wall   what still stands (both wall ends with their parapet and rail)
  rivendell_wall_broken_blockout.rubble the ramp that fills the gap and spills out on both sides
  rivendell_wall_broken_blockout.brick  the loose blocks, with a few carved pieces

Writes <out>/wall_broken_blockout.blend, <out>/rivendell_wall_broken_blockout.fbx,
<out>/blockout_report.json and <out>/concept_view_pass<N>.png. The Store launcher prints nothing:
wait for <out>/_report/DONE.txt (tools/blender/blender_job.py).
"""
import json
import math
import os
import random
import sys
from contextlib import contextmanager

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import blender_blockout_rivendell_wall as W  # noqa: E402
from blender_job import export_fbx, flag_value, job, report_path, script_args  # noqa: E402

NAME = "rivendell_wall_broken_blockout"

# Torn wall ends: (z from, z to, wall starts at |x|). The wall stands outside |x| of each band. The
# gap is 12 m at the deck (|x| 6.0 each side) and steps down to about 6.8 m at the plinth.
LEFT_EDGES = ((0.0, 3.0, 3.6), (3.0, 8.6, 4.2), (8.6, 9.9, 4.8), (9.9, 12.0, 6.0))
RIGHT_EDGES = ((0.0, 3.0, 3.2), (3.0, 8.6, 4.15), (8.6, 10.4, 4.95), (10.4, 12.0, 6.0))
STUMP_TOP = 2.2  # the middle pier, below the rubble crest on purpose (it would block the walk path)

# Rubble ramp
CREST = 4.3
PLATEAU_Y = 0.9  # flat crest for |y| below this
REACH_Y = 9.5  # the toe; the wall face is at |y| 2.5, so the ramp reaches 7.0 m past it
PLATEAU_X = 2.8  # |x| below this the ramp has no lateral fall-off and no noise
SPILL_X0 = 6.5  # the ramp reaches this |x| inside the gap, so it banks against the torn ends
SPILL_GROW = 0.3  # and widens by this per metre past the wall face
GRID = 0.5
NOISE_AMP = 0.26
NOISE_X0 = 2.2  # noise starts here and is full 1 m further out
SKIRT = 0.3  # the ramp is sunk this far below the ground line, so no gap shows at the toe

PATH_HALF = 1.5  # the walk path is 3 m wide along Y through the middle of the gap
STEP = 0.25
LIMIT_DEG = 32.0


@contextmanager
def patched(**kw):
    """Set module constants of the solid script for one call and put them back."""
    old = {k: getattr(W, k) for k in kw}
    for k, v in kw.items():
        setattr(W, k, v)
    try:
        yield
    finally:
        for k, v in old.items():
            setattr(W, k, v)


def smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def band_edge(edges, z0, z1):
    """The widest torn edge among the bands that overlap z0..z1: everything inside must lie beyond it."""
    return max(e for a, b, e in edges if a < z1 and b > z0)


# ---- what still stands ---------------------------------------------------------------------------
def torn_courses(bm, sign, edges):
    """Broken courses: a few stone blocks stick out from each torn face, at different heights."""
    rng = random.Random(3 if sign < 0 else 4)
    for z0, z1, e in edges[1:]:
        for _ in range(3):
            h = rng.uniform(0.35, 0.6)
            z = rng.uniform(z0 + 0.1, z1 - h - 0.1)
            ext = rng.uniform(0.3, 0.7)
            y = rng.uniform(-W.HALF_THICK, W.HALF_THICK - 1.2)
            xa, xb = sorted((sign * e, sign * (e - ext)))
            W.box(bm, xa, xb, y, y + rng.uniform(0.9, 1.6), z, z + h, W.STONE)


def build_side(bm, sign, edges, detail):
    """One standing end: foundation and plinth, stepped wall body, pier, trims, parapet, rail."""
    x_out = 10.0 * sign
    e0 = edges[0][2]
    low = [(-W.HALF_THICK, W.FOUNDATION_Z0), (W.FOUNDATION_FRONT_Y, W.FOUNDATION_Z0)]
    low += W.plinth_profile(detail) + [(-W.HALF_THICK, W.PLINTH_Z1)]
    xa, xb = sorted((x_out, sign * e0))
    W.prism(bm, [low], lambda u, v, k: Vector((xb if k else xa, u, v)), W.RUBBLE)
    pts = [(x_out, W.PLINTH_Z1)]
    for z0, z1, e in edges[1:]:
        pts += [(sign * e, z0), (sign * e, z1)]
    pts += [(x_out, W.DECK_Z)]
    W.prism(bm, [pts], lambda u, v, k: Vector((u, W.HALF_THICK if k else -W.HALF_THICK, v)), W.STONE)

    xc = sign * 20.0 / 3.0
    W.build_pier(bm, xc, detail)
    if detail >= 2:
        for z0, z1 in ((W.BAND_Z0, W.BAND_Z1), (W.STRING_Z0, W.STRING_Z1)):
            e = band_edge(edges, z0, z1)
            proud = W.BAND_PROUD if z0 == W.BAND_Z0 else W.STRING_PROUD
            xa, xb = sorted((x_out, sign * e))
            W.box(bm, xa, xb, W.RIB_BACK_Y, W.HALF_THICK + proud, z0, z1, W.TRIM)
        W.build_trunk(bm, xc)
    if detail >= 3:
        W.build_rib(bm, xc, sign, W.RIB_END_DEG, clip_x=x_out)  # toward the module end, as the solid wall
        a = W.RIB_HALF_SPAN / (1.0 - math.cos(math.radians(W.RIB_END_DEG)))
        reach = abs(xc) - (band_edge(edges, W.PIER_TOP_Z, W.RIB_APEX_Z) + 0.35)
        deg = math.degrees(math.acos(1.0 - reach / a))
        W.build_rib(bm, xc, -sign, deg)  # toward the tear: the rib stops where the wall does
        for cx in W.arch_centres():
            e = band_edge(edges, W.ARCH_Z0, W.ARCH_Z0 + W.ARCH_H)
            if cx * sign > 0 and abs(cx) - W.ARCH_W / 2 >= e + 0.05:
                W.build_blind_arch(bm, cx)
        torn_courses(bm, sign, edges)
    x0, x1 = sorted((x_out, sign * 6.0))
    W.build_parapet(bm, x0, x1, detail)
    first = x0 + 0.25
    with patched(WALL_X0=x0, WALL_X1=x1, BALUSTER_FIRST=first):
        W.build_rail(bm, detail)


def build_wall(bm, detail):
    """The standing part: both ends, the foundation under the gap and the stump of the middle pier."""
    e_l, e_r = LEFT_EDGES[0][2], RIGHT_EDGES[0][2]
    base = [(-W.HALF_THICK, W.FOUNDATION_Z0), (W.FOUNDATION_FRONT_Y, W.FOUNDATION_Z0),
            (W.FOUNDATION_FRONT_Y, 0.0), (-W.HALF_THICK, 0.0)]
    W.prism(bm, [base], lambda u, v, k: Vector((e_r if k else -e_l, u, v)), W.RUBBLE)
    build_side(bm, -1, LEFT_EDGES, detail)
    build_side(bm, 1, RIGHT_EDGES, detail)
    W.box(bm, -0.6, 0.6, W.PIER_BACK_Y, W.HALF_THICK + W.PIER_PROUD_BASE, 0.0, STUMP_TOP, W.TRIM)


# ---- the rubble ramp -------------------------------------------------------------------------------
def base_height(x, y):
    ay = abs(y)
    hy = min(max(1.0 - (ay - PLATEAU_Y) / (REACH_Y - PLATEAU_Y), 0.0), 1.0)
    xe = SPILL_X0 + (SPILL_GROW * (ay - W.HALF_THICK) if ay > W.HALF_THICK else 0.0)
    hx = 1.0 - smooth((abs(x) - PLATEAU_X) / (xe - PLATEAU_X))
    return CREST * hy * hx


class Heights:
    """The ramp height field on a grid, with the noise that makes it look like a heap."""

    def __init__(self, detail):
        n = int(round(REACH_Y / (GRID * (2.0 if detail < 2 else 1.0))))
        self.step = REACH_Y / n  # the toe falls on a grid line
        lim = n
        self.xs = [i * self.step for i in range(-lim, lim + 1)]
        self.ys = [j * self.step for j in range(-n, n + 1)]
        self.z = {}
        rng = random.Random(11)
        for i, x in enumerate(self.xs):
            for j, y in enumerate(self.ys):
                b = base_height(x, y)
                env = smooth((abs(x) - NOISE_X0) / 1.0) * min(1.0, b / 0.6)
                noise = (rng.random() * 2.0 - 1.0) * NOISE_AMP * env if detail >= 2 else 0.0
                self.z[(i, j)] = max(b + noise, 0.0)

    def at(self, x, y):
        fx = (x - self.xs[0]) / self.step
        fy = (y - self.ys[0]) / self.step
        i = min(max(int(math.floor(fx)), 0), len(self.xs) - 2)
        j = min(max(int(math.floor(fy)), 0), len(self.ys) - 2)
        tx, ty = fx - i, fy - j
        z = self.z
        return ((1 - tx) * (1 - ty) * z[(i, j)] + tx * (1 - ty) * z[(i + 1, j)]
                + (1 - tx) * ty * z[(i, j + 1)] + tx * ty * z[(i + 1, j + 1)])


def build_rubble(bm, heights):
    """A closed slab: the height field on top, a skirt down to -SKIRT and a flat bottom."""
    z = heights.z
    ni, nj = len(heights.xs), len(heights.ys)
    top = {}
    faces = []
    for i in range(ni - 1):
        for j in range(nj - 1):
            corners = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
            if max(z[c] for c in corners) <= 1e-4:
                continue
            vs = []
            for c in corners:
                if c not in top:
                    top[c] = bm.verts.new((heights.xs[c[0]], heights.ys[c[1]], z[c]))
                vs.append(top[c])
            faces.append(W._new_face(bm, vs))
    bmesh.ops.triangulate(bm, faces=[f for f in faces if f is not None])
    edges = [e for e in bm.edges if len(e.link_faces) == 1]
    bottom = {}
    adjacency = {}
    for e in edges:
        a, b = e.verts
        for v in (a, b):
            if v not in bottom:
                bottom[v] = bm.verts.new((v.co.x, v.co.y, -SKIRT))
        W._new_face(bm, [a, b, bottom[b], bottom[a]])
        adjacency.setdefault(bottom[a], []).append(bottom[b])
        adjacency.setdefault(bottom[b], []).append(bottom[a])
    seen = set()
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
        cap = W._new_face(bm, ring)
        bmesh.ops.triangulate(bm, faces=[cap], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    for f in bm.faces:
        f.material_index = W.RUBBLE


# ---- the loose blocks ---------------------------------------------------------------------------------
def place(bm, tmp, matrix):
    """Move the shell built in `tmp` by `matrix`, copy it into `bm` and empty `tmp`."""
    bmesh.ops.transform(tmp, matrix=matrix, verts=tmp.verts)
    made = {v: bm.verts.new(v.co) for v in tmp.verts}
    for f in tmp.faces:
        nf = bm.faces.new([made[v] for v in f.verts])
        nf.material_index = f.material_index
    tmp.clear()


def pose(pos, yaw, pitch, roll):
    return (Matrix.Translation(pos) @ Matrix.Rotation(math.radians(yaw), 4, "Z")
            @ Matrix.Rotation(math.radians(pitch), 4, "X") @ Matrix.Rotation(math.radians(roll), 4, "Y"))


def carved_pieces(bm, tmp, heights):
    """The recognisable pieces, each lying on the ramp where `spot` says: an arch moulding, a parapet
    panel with its pointed opening and a pier fragment (a block with a carved arch on its face)."""
    def ground(x, y, lift):
        return Vector((x, y, heights.at(x, y) + lift))

    def arch_moulding(x, y, yaw, tilt):
        ring = W.arch_ring(1.6, 2.2, 0.16)
        W.prism(tmp, [ring], lambda u, v, k: Vector((u, 0.35 * k, v)), W.TRIM)
        place(bm, tmp, pose(ground(x, y, 0.35), yaw, 90.0 + tilt, 0.0))

    def panel(x, y, yaw, tilt):
        outer = [(-0.9, 0.0), (0.9, 0.0), (0.9, 1.5), (-0.9, 1.5)]
        hole = [(u, 0.25 + v) for u, v in W.pointed_arch(0.5, 1.0)][::-1]
        W.prism(tmp, [outer, hole], lambda u, v, k: Vector((u, 0.5 * k, v)), W.TRIM)
        place(bm, tmp, pose(ground(x, y, 0.35), yaw, 90.0 + tilt, 0.0))

    def pier_piece(x, y, yaw, tilt):
        W.box(tmp, -0.55, 0.55, -0.4, 0.4, 0.0, 2.0, W.TRIM)
        ring = W.arch_ring(0.6, 1.4, 0.1, steps=6)
        W.prism(tmp, [ring], lambda u, v, k: Vector((u, 0.4 + 0.1 * k, 0.3 + v)), W.TRIM)
        place(bm, tmp, pose(ground(x, y, 0.3), yaw, 80.0 + tilt, 0.0))

    arch_moulding(3.9, 5.2, 30.0, 12.0)
    arch_moulding(-4.6, -3.6, -70.0, -8.0)
    panel(-3.6, 5.6, 200.0, 15.0)
    panel(4.2, -5.0, 120.0, -10.0)
    pier_piece(-3.4, 3.4, 150.0, 20.0)
    return 5


def build_bricks(bm, heights, detail):
    """Plain blocks (axis boxes turned and tilted) lying on and around the ramp, never on the walk
    path: |x| is kept at least 1.9 m away from the middle, and never inside the wall ends."""
    tmp = bmesh.new()
    rng = random.Random(5)
    placed = []
    want = {1: 12, 2: 34, 3: 34}[detail]
    tries = 0
    while len(placed) < want and tries < 4000:
        tries += 1
        w, d, h = rng.uniform(0.45, 1.3), rng.uniform(0.4, 0.9), rng.uniform(0.3, 0.7)
        r = math.hypot(w, d) / 2
        side = 1.0 if rng.random() < 0.5 else -1.0
        x = side * rng.uniform(2.3, 7.2)
        y = (1.0 if rng.random() < 0.65 else -1.0) * rng.uniform(1.0, 8.4)
        if abs(x) - r < 1.9 or (abs(y) < 2.9 + r / 2 and abs(x) > 3.0):
            continue
        if heights.at(x, y) < 0.25:
            continue
        if any(math.hypot(x - px, y - py) < 0.55 * (r + pr) + 0.2 for px, py, pr in placed):
            continue
        placed.append((x, y, r))
        mat = W.RUBBLE if rng.random() < 0.25 else W.STONE
        W.box(tmp, -w / 2, w / 2, -d / 2, d / 2, -h / 2, h / 2, mat)
        pos = Vector((x, y, heights.at(x, y) + h * rng.uniform(0.15, 0.35)))
        place(bm, tmp, pose(pos, rng.uniform(0, 360), rng.uniform(-28, 28), rng.uniform(-28, 28)))
    count = len(placed)
    if detail >= 3:
        count += carved_pieces(bm, tmp, heights)
    tmp.free()
    return count


# ---- scene, measurements ---------------------------------------------------------------------------------
def islands(ob):
    """Number of separate shells in a mesh."""
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


def top_height(trees, x, y):
    best = 0.0
    for t in trees:
        hit = t.ray_cast(Vector((x, y, 40.0)), Vector((0.0, 0.0, -1.0)))
        if hit[0] is not None and hit[0].z > best:
            best = hit[0].z
    return best


def path_stats(trees, half=PATH_HALF):
    """Slope along the walk path from neighbouring ray-cast heights on a 0.25 m grid, from the far
    toe on -Y to the far toe on +Y: central differences in X and Y, one-sided at the edge."""
    n_x = int(round(2 * half / STEP)) + 1
    n_y = int(round(2 * REACH_Y / STEP)) + 1
    xs = [-half + i * STEP for i in range(n_x)]
    ys = [-REACH_Y + j * STEP for j in range(n_y)]
    z = [[top_height(trees, x, y) for y in ys] for x in xs]
    slopes = []
    for i in range(n_x):
        for j in range(n_y):
            i0, i1 = max(i - 1, 0), min(i + 1, n_x - 1)
            j0, j1 = max(j - 1, 0), min(j + 1, n_y - 1)
            gx = (z[i1][j] - z[i0][j]) / ((i1 - i0) * STEP)
            gy = (z[i][j1] - z[i][j0]) / ((j1 - j0) * STEP)
            slopes.append((math.degrees(math.atan(math.hypot(gx, gy))), xs[i], ys[j]))
    worst = max(slopes)
    under = sum(1 for s in slopes if s[0] < LIMIT_DEG)
    return {"nodes": len(slopes), "under_limit_share": round(under / len(slopes), 4),
            "steepest_deg": round(worst[0], 2), "steepest_at_xy": [worst[1], worst[2]],
            "limit_deg": LIMIT_DEG, "path_half_width": half,
            "crest_on_path": round(max(max(r) for r in z), 3)}


def gap_at_deck(trees, y):
    """Width of the gap from the top hits along X at a fixed Y: where the deck (Z 11.9 up) ends."""
    def edge(sign):
        x = 0.0
        while x < 10.0 and top_height(trees, sign * x, y) < 11.9:
            x += 0.05
        return x
    left, right = edge(-1), edge(1)
    return {"left_x": -round(left, 3), "right_x": round(right, 3), "width": round(left + right, 3)}


def measure(objs, heights):
    dg = bpy.context.evaluated_depsgraph_get()
    by = {o.name.rsplit(".", 1)[1]: o for o in objs}
    trees_all = [BVHTree.FromObject(o, dg) for o in objs]
    trees_ground = [BVHTree.FromObject(by["rubble"], dg)]
    rub = by["rubble"].data.vertices
    face = W.HALF_THICK
    out = {
        "gap_at_deck": {f"y={y}": gap_at_deck(trees_all, y) for y in (-1.0, 0.0, 1.0)},
        "rubble_reach_plus_y": round(max(v.co.y for v in rub) - face, 3),
        "rubble_reach_minus_y": round(-min(v.co.y for v in rub) - face, 3),
        "rubble_reach_plus_y_above_0p1m": round(max(v.co.y for v in rub if v.co.z >= 0.1) - face, 3),
        "rubble_reach_minus_y_above_0p1m": round(-min(v.co.y for v in rub if v.co.z >= 0.1) - face, 3),
        "crest_z_inside_gap": round(max(v.co.z for v in rub if abs(v.co.y) < face), 3),
        "walk_path_all": path_stats(trees_all),
        "walk_path_rubble_only": path_stats(trees_ground),
        "path_for_x_half_2p0_all": path_stats(trees_all, half=2.0),
    }
    wall = by["wall"].data.vertices
    out["wall_top_z"] = round(max(v.co.z for v in wall), 3)
    out["brick_islands"] = islands(by["brick"])
    return out


def render_concept(path):
    """Three-quarter view from the outside at eye level, Workbench, material colours, with a 1.8 m
    thin box in front of the ramp for scale. Preview objects are added after the .blend is saved."""
    scene = bpy.context.scene
    scale = bpy.data.materials.new("scale_ref")
    scale.diffuse_color = (0.8, 0.1, 0.1, 1.0)
    ground = bpy.data.materials.new("preview_ground")
    ground.diffuse_color = W.hex_to_linear("c9cacb")
    for name, dims, loc, mat in (("scale_1p8m", (0.5, 0.3, 1.8), (2.0, 11.5, 0.9), scale),
                                 ("preview_ground", (120.0, 120.0, 0.02), (0.0, 0.0, -0.011), ground)):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=dims, verts=bm.verts)
        bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
        W.object_from_bmesh(name, bm, [mat])
        bm.free()
    cam_data = bpy.data.cameras.new("concept_cam")
    cam_data.lens = 30.0
    cam_data.clip_end = 500.0
    cam = bpy.data.objects.new("concept_cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (5.0, 31.0, 1.8)
    target = Vector((0.0, 0.0, 6.0))
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


def build_all(detail):
    mats = W.make_materials()
    heights = Heights(detail)
    objs = []
    bm = bmesh.new()
    build_wall(bm, detail)
    objs.append(W.object_from_bmesh(f"{NAME}.wall", bm, mats))
    bm.free()
    bm = bmesh.new()
    build_rubble(bm, heights)
    objs.append(W.object_from_bmesh(f"{NAME}.rubble", bm, mats))
    bm.free()
    bm = bmesh.new()
    count = build_bricks(bm, heights, detail)
    objs.append(W.object_from_bmesh(f"{NAME}.brick", bm, mats))
    bm.free()
    return objs, heights, count


def main():
    args = script_args()
    out = flag_value(args, "--out")
    detail = int(flag_value(args, "--detail") or 3)
    pass_n = flag_value(args, "--pass") or str(detail)
    with job(report_path(out)) as result:
        os.makedirs(out, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs, heights, count = build_all(detail)
        bpy.context.view_layer.update()
        result["detail"] = detail
        result["objects"] = {o.name: W.mesh_stats(o) for o in objs}
        result["total_tris"] = sum(v["tris"] for v in result["objects"].values())
        result["blocks_placed"] = count
        result["measured"] = measure(objs, heights)
        fbx = os.path.join(out, "rivendell_wall_broken_blockout.fbx")
        export_fbx(fbx, objs)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "wall_broken_blockout.blend"))
        result["fbx"] = fbx
        with open(os.path.join(out, "blockout_report.json"), "w", encoding="utf-8") as f:
            json.dump({k: result[k] for k in ("detail", "objects", "total_tris", "blocks_placed", "measured")},
                      f, indent=1)
        render_concept(os.path.join(out, f"concept_view_pass{pass_n}.png"))


if __name__ == "__main__":
    main()
