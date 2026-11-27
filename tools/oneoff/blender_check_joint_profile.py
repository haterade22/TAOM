"""Seam test for two drop-in kit pieces: does the highest-X end of A meet the lowest-X end of B
with no gap, no step and no open hole? Headless Blender:

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_check_joint_profile.py --
        --a <a.fbx> --b <b.fbx> --out <dir> [--render]

Writes <out>/_report/DONE.txt (JSON: every number and a verdict), <out>/joint_diff.png and, with
--render, joint_outside.png, joint_inside.png and joint_top.png. The Store launcher prints nothing:
wait for the report (tools/blender/blender_job.py). The report "status" is "ok" when the test ran;
the verdict (PASS or FAIL, with reasons) is its own field.

Frame (the kit's): the piece runs along X, the outer face is +Y, Z is up. Each file is read as the
export would write it (visual LOD0 meshes only: no .lodN objects, no bo_ bodies, by
mesh_preflight.classify_name), in world space.

Checks, in the order of the report:
  flatness   every vertex within 0.02 m of the end plane lies on it within 0.001 m;
  section    each piece is cut 0.01 m inside its end plane, the cut outline is filled (even-odd per
             connected outline, then the union) and both sections are rasterised in (y, z): area of
             each, area only A has, area only B has, mismatch = their sum. The mismatch is measured
             on a 0.005 m grid, so that a 1 cm slit holds two cell centres wherever it falls; the
             picture and the ray test use a 0.02 m grid;
  open nodes the cut outline of every object must close: a node used an odd number of times is a
             hole in a shell at the plane (part of the verdict);
  closed     a ray along X from every cell of the section, through the end plane: a cell with no hit
             within 0.05 m is open; the share of open cells is reported;
  uv phase   on the outer face, the inner face and the deck, within 0.05 m of the joint, U and V of
             both pieces at the same sample points, each extrapolated to the plane; the largest
             difference in the fractional part, so a texture that jumps at the joint shows (skipped
             when a piece has no UVs);
  colour     the vertex colour (attribute "Col") of both pieces at the same sample points, each
             extrapolated to the plane, in sRGB levels (0 to 255).
Verdict PASS: mismatch at most MISMATCH_LIMIT (0.004 m2; a 1 cm slit 2 m long measures 0.02), no
vertex off the plane, no open node, no open cell, UV phase at most 0.01, colour within 2 levels.
The report also lists "cannot_see": what this tool does not test (a seam inside a piece, anything
deeper than 0.01 m, the faces it does not sample, and the counts of sample points it skipped).

The pure functions (grid, fill, compare, flatness, verdict, picture) import without Blender and are
tested by tools/tests/test_blender_check_joint_profile.py.
"""
import argparse
import math
import os
import struct
import sys
import time
import zlib

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
from blender_job import flag_value, import_fbx, job, report_path, script_args  # noqa: E402
from mesh_preflight import classify_name  # noqa: E402  (bpy is imported inside its functions)

CELL = 0.02  # grid cell for the picture and the ray test, metres
FINE_CELL = 0.005  # grid cell for the mismatch: a 1 cm slit holds two cell centres wherever it falls
CUT_DEPTH = 0.01  # the section is cut this far inside the end plane
FLAT_BAND, FLAT_TOL = 0.02, 0.001
MISMATCH_LIMIT = 0.004  # square metres; a 1 cm slit 2 m long measures 0.02
UV_LIMIT = 0.01
COLOUR_LIMIT_LEVELS = 2.0  # sRGB levels of 255
SAMPLE_STEP = 0.05  # spacing of the UV and colour sample points, metres
FACES = ("outer", "inner", "deck")
RAY_REACH = 0.05  # an end face must lie within this of the cut
UV_BAND = 0.05
GREY, RED, BLUE, WHITE, GRID = (150, 150, 150), (220, 40, 40), (40, 80, 230), (255, 255, 255), (228, 228, 228)
BLACK = (0, 0, 0)


# ---- pure: grid, fill, compare ------------------------------------------------------------------
def make_grid(y_min, y_max, z_min, z_max, cell=CELL, pad=0.1):
    """A grid snapped to multiples of the cell so both pieces and every run share it."""
    y0 = math.floor((y_min - pad) / cell + 1e-9) * cell
    z0 = math.floor((z_min - pad) / cell + 1e-9) * cell
    ny = int(math.ceil((y_max + pad - y0) / cell - 1e-9))
    nz = int(math.ceil((z_max + pad - z0) / cell - 1e-9))
    return {"y0": y0, "z0": z0, "cell": cell, "ny": ny, "nz": nz}


def _cells_in(lo, hi, origin, cell):
    """Indices of the cells whose centre lies in [lo, hi)."""
    return (int(math.ceil((lo - origin) / cell - 0.5)), int(math.ceil((hi - origin) / cell - 0.5)))


def fill_outline(segments, grid):
    """Even-odd fill of the closed outline in `segments` ((y1, z1, y2, z2) each): one bit per cell,
    one Python int per row (bit c = column c). A row is tested at its centre."""
    y0, z0, cell, ny, nz = grid["y0"], grid["z0"], grid["cell"], grid["ny"], grid["nz"]
    cross = {}
    for ya, za, yb, zb in segments:
        if za == zb:
            continue
        if za > zb:
            ya, za, yb, zb = yb, zb, ya, za
        r0, r1 = _cells_in(za, zb, z0, cell)
        for r in range(max(r0, 0), min(r1, nz)):
            zc = z0 + (r + 0.5) * cell
            cross.setdefault(r, []).append(ya + (yb - ya) * (zc - za) / (zb - za))
    rows = [0] * nz
    for r, ys in cross.items():
        ys.sort()
        bits = 0
        for i in range(0, len(ys) - 1, 2):
            c0, c1 = _cells_in(ys[i], ys[i + 1], y0, cell)
            c0, c1 = max(c0, 0), min(c1, ny)
            if c1 > c0:
                bits |= ((1 << (c1 - c0)) - 1) << c0
        rows[r] = bits
    return rows


def fill_components(components, grid):
    """Union of the fills of separate outlines (so overlapping solids add, never cancel)."""
    rows = [0] * grid["nz"]
    for comp in components:
        for r, bits in enumerate(fill_outline(comp, grid)):
            rows[r] |= bits
    return rows


def _count(rows):
    return sum(bin(r).count("1") for r in rows)


def mask_area(rows, cell=CELL):
    return _count(rows) * cell * cell


def compare_masks(a, b, cell=CELL):
    only_a = [x & ~y for x, y in zip(a, b)]
    only_b = [y & ~x for x, y in zip(a, b)]
    both = [x & y for x, y in zip(a, b)]
    sq = cell * cell
    res = {"area_a": _count(a) * sq, "area_b": _count(b) * sq, "only_a": _count(only_a) * sq,
           "only_b": _count(only_b) * sq, "both": _count(both) * sq}
    res["mismatch"] = res["only_a"] + res["only_b"]
    return res


def connected_components(segments, digits=5):
    """Group segments that share an endpoint (rounded to `digits`) into outlines."""
    parent = list(range(len(segments)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    owner = {}
    for i, (ya, za, yb, zb) in enumerate(segments):
        for key in ((round(ya, digits), round(za, digits)), (round(yb, digits), round(zb, digits))):
            j = owner.setdefault(key, i)
            if j != i:
                parent[find(i)] = find(j)
    groups = {}
    for i, seg in enumerate(segments):
        groups.setdefault(find(i), []).append(seg)
    return list(groups.values())


def open_nodes(segments, digits=5):
    """Endpoints used an odd number of times: a closed outline has none."""
    deg = {}
    for ya, za, yb, zb in segments:
        for key in ((round(ya, digits), round(za, digits)), (round(yb, digits), round(zb, digits))):
            deg[key] = deg.get(key, 0) + 1
    return sum(1 for d in deg.values() if d % 2)


# ---- pure: flatness, UV phase, verdict -----------------------------------------------------------
def flatness(xs, plane, band=FLAT_BAND, tol=FLAT_TOL):
    """Vertices within `band` of the plane, how many of them are off it by more than `tol`, and the
    worst deviation among them."""
    near = off = 0
    worst = 0.0
    for x in xs:
        d = abs(x - plane)
        if d <= band:
            near += 1
            worst = max(worst, d)
            off += d > tol + 1e-9
    return {"near": near, "off": off, "worst": worst}


def phase_diff(a, b):
    """Distance between two texture coordinates modulo one tile (0 to 0.5)."""
    d = abs((a - math.floor(a)) - (b - math.floor(b)))
    return min(d, 1.0 - d)


def extrapolate_to_plane(v1, v2, d1, d2):
    """Value at the plane from samples at depths d1 < d2 inside it."""
    return v1 + (v1 - v2) * d1 / (d2 - d1)


def srgb_level(linear):
    """The sRGB level (0 to 255) of a linear value."""
    c = min(max(linear, 0.0), 1.0)
    return 255.0 * (c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1.0 / 2.4) - 0.055)


def colour_levels_diff(a, b):
    """Largest channel difference of two colours given in levels."""
    return max(abs(x - y) for x, y in zip(a, b))


def sample_positions(lo, hi, step=SAMPLE_STEP):
    """Centres of the cells of `step` between lo and hi."""
    out, k = [], 0
    while lo + (k + 1) * step <= hi + 1e-9:
        out.append(lo + (k + 0.5) * step)
        k += 1
    return out


def decide(report):
    reasons = []
    if report["mismatch_m2"] > MISMATCH_LIMIT + 1e-9:
        reasons.append(f"section mismatch {report['mismatch_m2']:.4f} m2 (limit {MISMATCH_LIMIT})")
    for side in ("a", "b"):
        off = report["flatness"][side]["off"]
        if off:
            reasons.append(f"{off} vertices of {side.upper()} near the end plane are off it")
    for side in ("a", "b"):
        nodes = report.get("section_open_nodes", {}).get(side, 0)
        if nodes:
            reasons.append(f"{nodes} open nodes in the section outline of {side.upper()} (a hole in a shell at the plane)")
    for side in ("a", "b"):
        share = report["open_share"][side]
        if share > 0:
            reasons.append(f"end of {side.upper()} is open over {share * 100:.2f}% of its section")
    uv = report["uv_phase"]
    faces = uv.get("faces")
    if faces:
        for name in FACES:
            f = faces.get(name, {})
            if f.get("checked") and (f["max_u"] > UV_LIMIT or f["max_v"] > UV_LIMIT):
                reasons.append(f"UV phase jumps at the joint on the {name} face "
                               f"(U {f['max_u']:.3f}, V {f['max_v']:.3f}; limit {UV_LIMIT})")
    elif uv.get("checked") and (uv["max_u"] > UV_LIMIT or uv["max_v"] > UV_LIMIT):
        reasons.append(f"UV phase jumps at the joint (U {uv['max_u']:.3f}, V {uv['max_v']:.3f}; limit {UV_LIMIT})")
    col = report.get("vertex_colour", {})
    if col.get("checked") and col["max_levels"] > COLOUR_LIMIT_LEVELS + 1e-9:
        reasons.append(f"vertex colour differs by {col['max_levels']:.1f} levels across the joint "
                       f"(limit {COLOUR_LIMIT_LEVELS:.0f})")
    return ("FAIL" if reasons else "PASS"), reasons


STATIC_BLIND_SPOTS = (
    "A seam inside a piece (a drum to its stub, a part to a part) is never tested: only the two end planes.",
    "Only the section 0.01 m inside each end plane is compared: a feature that changes within the first "
    "0.01 m of a piece, or a face that stops short of the plane by less than the flatness band, is not seen.",
    "The mismatch grid is 0.005 m: a slit narrower than 0.005 m has no cell centre in it and is not seen.",
    "UV phase and vertex colour are sampled on the first surface a ray meets on three faces: outer, inner "
    "and deck. The niche jambs, the sides of members, the undersides and the end faces are not compared.",
    "Objects that overlap by design (the parapet parts sink into the wall) are compared as one union, "
    "not object by object.",
    "Normals, material slots, texture content and the sub-mesh split are not compared.",
    "Collision bodies and LODs are not read (no bo_ and no .lodN objects), so a joint in them is untested.",
)


def blind_spots(report):
    """What this tool did not see in this run: the static list, and what the run itself skipped."""
    out = list(STATIC_BLIND_SPOTS)
    uv = report.get("uv_phase", {})
    if not uv.get("checked", True) and "faces" not in uv:
        out.append("UV phase was not checked: " + str(uv.get("note", "no reason given")))
    for name in FACES:
        f = uv.get("faces", {}).get(name)
        if f is None:
            continue
        if not f.get("checked"):
            out.append(f"UV phase and colour on the {name} face: no comparable point ({f.get('note', 'none found')}).")
        elif f.get("skipped"):
            out.append(f"{f['skipped']} sample points on the {name} face had no surface at the same place in both pieces.")
    col = report.get("vertex_colour", {})
    if not col.get("checked", True):
        out.append("The vertex colour was not compared: " + str(col.get("note", "no reason given")))
    if col.get("without_colour"):
        out.append(f"{len(col['without_colour'])} objects have no vertex colour attribute, so a sample that hits one "
                   "has no colour to compare: " + ", ".join(col["without_colour"]))
    return out


# ---- pure: the difference picture ----------------------------------------------------------------
_FONT = {
    "0": "111101101101111", "1": "010110010010111", "2": "111001111100111", "3": "111001111001111",
    "4": "101101111001001", "5": "111100111001111", "6": "111100111101111", "7": "111001010010010",
    "8": "111101111101111", "9": "111101111001111", "-": "000000111000000", ".": "000000000000010",
    "A": "010101111101101", "B": "110101110101110", "Y": "101101010010010", "Z": "111001010100111",
    "M": "101111111101101", " ": "0" * 15,
}


def _text(canvas, x, y, text, scale=2, colour=BLACK):
    h, w = len(canvas), len(canvas[0])
    for ch in text:
        glyph = _FONT.get(ch, _FONT[" "])
        for i, bit in enumerate(glyph):
            if bit == "1":
                px0, py0 = x + (i % 3) * scale, y + (i // 3) * scale
                for dy in range(scale):
                    for dx in range(scale):
                        if 0 <= py0 + dy < h and 0 <= px0 + dx < w:
                            canvas[py0 + dy][px0 + dx] = colour
        x += 4 * scale


def diff_picture(a, b, grid, px=2):
    """(width, height, pixels): grey where both pieces have material, red where only A, blue where
    only B, with a 1 m grid on the empty cells, tick labels in metres and a legend. Y runs right
    (the outer face is on the right), Z runs up."""
    y0, z0, cell, ny, nz = grid["y0"], grid["z0"], grid["cell"], grid["ny"], grid["nz"]
    ml, mb, mt, mr = 34, 22, 20, 10
    w, h = max(ml + ny * px + mr, 200), mt + nz * px + mb
    canvas = [[WHITE] * w for _ in range(h)]
    for r in range(nz):
        ra, rb = a[r], b[r]
        top = mt + (nz - 1 - r) * px
        zc = z0 + (r + 0.5) * cell
        on_z_line = abs(zc - round(zc)) < cell / 2
        for c in range(ny):
            ba, bb = (ra >> c) & 1, (rb >> c) & 1
            if ba and bb:
                col = GREY
            elif ba:
                col = RED
            elif bb:
                col = BLUE
            else:
                yc = y0 + (c + 0.5) * cell
                col = GRID if on_z_line or abs(yc - round(yc)) < cell / 2 else WHITE
            if col is WHITE:
                continue
            left = ml + c * px
            for dy in range(px):
                row = canvas[top + dy]
                for dx in range(px):
                    row[left + dx] = col
    for z in range(int(math.ceil(z0)), int(math.floor(z0 + nz * cell)) + 1):
        y_img = mt + int((nz - (z - z0) / cell) * px)
        for t in range(5):
            if 0 <= y_img < h and ml - 5 + t < w:
                canvas[y_img][ml - 5 + t] = BLACK
        if z % 2 == 0:
            _text(canvas, max(0, ml - 8 - 8 * len(str(z))), y_img - 5, str(z))
    for yv in range(int(math.ceil(y0)), int(math.floor(y0 + ny * cell)) + 1):
        x_img = ml + int((yv - y0) / cell * px)
        base = mt + nz * px
        for t in range(5):
            if base + t < h and 0 <= x_img < w:
                canvas[base + t][x_img] = BLACK
        _text(canvas, x_img - 4 * len(str(yv)), base + 7, str(yv))
    for i, (col, label) in enumerate(((GREY, "AB"), (RED, "A"), (BLUE, "B"))):
        x = ml + i * 60
        for dy in range(10):
            for dx in range(14):
                canvas[4 + dy][x + dx] = col
        _text(canvas, x + 18, 4, label)
    _text(canvas, w - 40, 4, "Y M")
    _text(canvas, 2, 4, "Z")
    return w, h, [p for row in canvas for p in row]


def write_png(path, width, height, pixels):
    raw = bytearray()
    for r in range(height):
        raw.append(0)
        for p in pixels[r * width:(r + 1) * width]:
            raw += bytes(p)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(bytes(raw), 6)) + chunk(b"IEND", b""))


# ---- Blender side --------------------------------------------------------------------------------
def visual_lod0(objs):
    return [o for o in objs if classify_name(o.name)["kind"] == "base"]


def read_piece(objs):
    """World-space triangles of `objs`: V (n,3), T (m,3) into V, UV (m,3,2) or NaN, COL (m,3,3) in sRGB
    levels or NaN (the first colour attribute), OBJ (m,) index."""
    import bpy
    import numpy as np
    dg = bpy.context.evaluated_depsgraph_get()
    vs, ts, uvs, cols, owners, has_uv, has_col, base = [], [], [], [], [], [], [], 0
    for k, o in enumerate(objs):
        eo = o.evaluated_get(dg)
        me = eo.to_mesh()
        n = len(me.vertices)
        co = np.empty(n * 3, dtype=np.float64)
        me.vertices.foreach_get("co", co)
        m = np.array(eo.matrix_world, dtype=np.float64)
        vs.append(co.reshape(-1, 3) @ m[:3, :3].T + m[:3, 3])
        tris = me.loop_triangles
        t = len(tris)
        tv = np.empty(t * 3, dtype=np.int64)
        tl = np.empty(t * 3, dtype=np.int64)
        tris.foreach_get("vertices", tv)
        tris.foreach_get("loops", tl)
        ts.append(tv.reshape(-1, 3) + base)
        layer = me.uv_layers.active
        if layer is not None and t:
            u = np.empty(len(me.loops) * 2, dtype=np.float64)
            layer.data.foreach_get("uv", u)
            uvs.append(u.reshape(-1, 2)[tl].reshape(t, 3, 2))
        else:
            uvs.append(np.full((t, 3, 2), np.nan))
        has_uv.append(layer is not None)
        attr = me.color_attributes[0] if len(me.color_attributes) else None
        if attr is not None and t and attr.domain == "CORNER":
            c = np.empty(len(me.loops) * 4, dtype=np.float64)
            attr.data.foreach_get("color", c)
            lin = c.reshape(-1, 4)[:, :3]
            levels = np.vectorize(srgb_level)(lin)
            cols.append(levels[tl].reshape(t, 3, 3))
        else:
            cols.append(np.full((t, 3, 3), np.nan))
        has_col.append(attr is not None and attr.domain == "CORNER")
        owners.append(np.full(t, k, dtype=np.int64))
        base += n
        eo.to_mesh_clear()
    return {"V": np.concatenate(vs), "T": np.concatenate(ts), "UV": np.concatenate(uvs),
            "COL": np.concatenate(cols), "OBJ": np.concatenate(owners), "names": [o.name for o in objs],
            "has_uv": has_uv, "has_col": has_col}


def cut_segments(piece, xp):
    """(y1, z1, y2, z2) per triangle crossed by the plane x = xp, with the object index of each."""
    import numpy as np
    V, T = piece["V"], piece["T"]
    d = V[T, 0] - xp
    pos = d >= 0
    mixed = np.nonzero(pos.any(1) & ~pos.all(1))[0]
    if not len(mixed):
        return np.zeros((0, 4)), np.zeros(0, dtype=np.int64)
    pts = np.zeros((len(mixed), 3, 2))
    cross = np.zeros((len(mixed), 3), dtype=bool)
    for e, (i, j) in enumerate(((0, 1), (1, 2), (2, 0))):
        ia, ib = T[mixed, i], T[mixed, j]
        swap = ia > ib  # the same edge gives the same point whichever triangle asks
        lo, hi = np.where(swap, ib, ia), np.where(swap, ia, ib)
        pa, pb = V[lo], V[hi]
        cr = (pa[:, 0] >= xp) != (pb[:, 0] >= xp)
        den = np.where(cr, pb[:, 0] - pa[:, 0], 1.0)
        t = (xp - pa[:, 0]) / den
        pts[:, e, 0] = pa[:, 1] + (pb[:, 1] - pa[:, 1]) * t
        pts[:, e, 1] = pa[:, 2] + (pb[:, 2] - pa[:, 2]) * t
        cross[:, e] = cr
    pick = np.argsort(~cross, axis=1, kind="stable")[:, :2]
    rows = np.arange(len(mixed))
    p1, p2 = pts[rows, pick[:, 0]], pts[rows, pick[:, 1]]
    return np.hstack([p1, p2]), piece["OBJ"][mixed]


def section_components(piece, xp):
    segs, owner = cut_segments(piece, xp)
    comps, dangling = [], 0
    for k in sorted(set(owner.tolist())):
        mine = [tuple(s) for s in segs[owner == k].tolist()]
        dangling += open_nodes(mine)
        comps.extend(connected_components(mine))
    return comps, dangling


def build_bvh(piece):
    from mathutils.bvhtree import BVHTree
    return BVHTree.FromPolygons(piece["V"].tolist(), piece["T"].tolist())


def open_cells(piece, bvh, xp, inward, mask, grid):
    """Cells of `mask` whose ray, from inside the cut outward along X, finds no end face."""
    from mathutils import Vector
    y0, z0, cell = grid["y0"], grid["z0"], grid["cell"]
    direction = Vector((-inward, 0.0, 0.0))
    x_start = xp + inward * CUT_DEPTH
    opened, total = [], 0
    for r, bits in enumerate(mask):
        c = 0
        while bits >> c:
            if (bits >> c) & 1:
                total += 1
                y, z = y0 + (c + 0.5) * cell, z0 + (r + 0.5) * cell
                hit = bvh.ray_cast(Vector((x_start, y, z)), direction, RAY_REACH)[0]
                if hit is None:
                    hit = bvh.ray_cast(Vector((x_start, y + 0.0007, z + 0.0005)), direction, RAY_REACH)[0]
                if hit is None:
                    opened.append((y, z))
            c += 1
    return opened, total


def surface_sample(piece, bvh, xp, inward, origin, direction, axis, travel):
    """U, V and colour at the end plane of the first surface that faces the ray, or None. The ray starts
    at `origin` (given for X = xp, moved `depth` inside), runs along `direction` (a unit axis vector)
    and must hit a face whose normal is mostly along `axis`. Two depths, 0.01 and 0.03 m inside the end,
    are extrapolated to the plane. Returns (u, v, (r, g, b) or None, hit coordinate along `axis`)."""
    from mathutils import Vector
    import numpy as np
    got, first = [], None
    for depth in (0.01, 0.03):
        o = Vector((xp + inward * depth, origin[1], origin[2]))
        loc, _n, idx, _d = bvh.ray_cast(o, Vector(direction), travel)
        if loc is None:
            return None
        tri = piece["V"][piece["T"][idx]]
        normal = np.cross(tri[1] - tri[0], tri[2] - tri[0])
        length = np.linalg.norm(normal)
        if length == 0 or abs(normal[axis]) / length < 0.5:
            return None
        if first is None:
            first = idx
        elif idx != first:
            return None
        uv = piece["UV"][idx]
        if np.isnan(uv).any():
            return None
        a, b, c = tri
        p = np.array([loc.x, loc.y, loc.z])
        m = np.array([b - a, c - a]).T
        sol = np.linalg.lstsq(m, p - a, rcond=None)[0]
        u = uv[0] + (uv[1] - uv[0]) * sol[0] + (uv[2] - uv[0]) * sol[1]
        col = piece["COL"][idx]
        rgb = None if np.isnan(col).any() else col[0] + (col[1] - col[0]) * sol[0] + (col[2] - col[0]) * sol[1]
        got.append((float(u[0]), float(u[1]), rgb, float(p[axis])))
    (u1, v1, c1, h1), (u2, v2, c2, _h2) = got
    rgb = None if c1 is None or c2 is None else tuple(extrapolate_to_plane(float(x), float(y), 0.01, 0.03) for x, y in zip(c1, c2))
    return (extrapolate_to_plane(u1, u2, 0.01, 0.03), extrapolate_to_plane(v1, v2, 0.01, 0.03), rgb, h1)


def face_rays(name, pa, pb):
    """The sample rays of one face group: [(origin y, origin z, direction, axis, travel)]."""
    ys = (float(min(pa["V"][:, 1].min(), pb["V"][:, 1].min())), float(max(pa["V"][:, 1].max(), pb["V"][:, 1].max())))
    zs = (float(max(pa["V"][:, 2].min(), pb["V"][:, 2].min())), float(min(pa["V"][:, 2].max(), pb["V"][:, 2].max())))
    if name == "outer":
        return [(ys[1] + 1.0, z, (0.0, -1.0, 0.0), 1, ys[1] - ys[0] + 2.0) for z in sample_positions(zs[0], zs[1])]
    if name == "inner":
        return [(ys[0] - 1.0, z, (0.0, 1.0, 0.0), 1, ys[1] - ys[0] + 2.0) for z in sample_positions(zs[0], zs[1])]
    zt = float(max(pa["V"][:, 2].max(), pb["V"][:, 2].max())) + 1.0
    return [(y, zt, (0.0, 0.0, -1.0), 2, zt - zs[0] + 2.0) for y in sample_positions(ys[0], ys[1])]


def surface_phase(pa, pb, bva, bvb, xa, xb):
    """UV phase and vertex colour on the outer face, the inner face and the deck. Returns
    (uv_phase report, vertex_colour report)."""
    uv_faces, col_levels, col_points, col_worst = {}, 0.0, 0, None
    uv_ok = all(pa["has_uv"]) and all(pb["has_uv"])
    col_ok = any(pa["has_col"]) and any(pb["has_col"])
    without = [n for p in (pa, pb) for n, h in zip(p["names"], p["has_col"]) if not h]
    if not uv_ok:
        uv = {"checked": False, "note": "a piece has an object without UVs"}
    else:
        for name in FACES:
            max_u = max_v = 0.0
            worst, compared, skipped = None, 0, 0
            for oy, oz, direction, axis, travel in face_rays(name, pa, pb):
                sa = surface_sample(pa, bva, xa, -1.0, (0.0, oy, oz), direction, axis, travel)
                sb = surface_sample(pb, bvb, xb, 1.0, (0.0, oy, oz), direction, axis, travel)
                if sa is None or sb is None or abs(sa[3] - sb[3]) > 0.02:
                    skipped += 1
                    continue
                compared += 1
                du, dv = phase_diff(sa[0], sb[0]), phase_diff(sa[1], sb[1])
                if max(du, dv) >= max(max_u, max_v):
                    worst = {"sample": [oy, oz], "hit": sa[3], "u_a": sa[0], "u_b": sb[0], "v_a": sa[1], "v_b": sb[1]}
                max_u, max_v = max(max_u, du), max(max_v, dv)
                if col_ok and sa[2] is not None and sb[2] is not None:
                    d = colour_levels_diff(sa[2], sb[2])
                    col_points += 1
                    if d >= col_levels:
                        col_worst = {"face": name, "sample": [oy, oz], "a": list(sa[2]), "b": list(sb[2])}
                    col_levels = max(col_levels, d)
            if compared:
                uv_faces[name] = {"checked": True, "max_u": max_u, "max_v": max_v, "points": compared,
                                  "skipped": skipped, "worst": worst}
            else:
                uv_faces[name] = {"checked": False, "note": "no points at the same place in both pieces",
                                  "skipped": skipped}
        done = [f for f in uv_faces.values() if f["checked"]]
        uv = {"checked": bool(done), "faces": uv_faces,
              "max_u": max((f["max_u"] for f in done), default=0.0),
              "max_v": max((f["max_v"] for f in done), default=0.0)}
    if not col_ok:
        colour = {"checked": False, "note": "a piece has no object with a vertex colour attribute"}
    elif not col_points:
        colour = {"checked": False, "note": "no colour sample points at the same place in both pieces"}
    else:
        colour = {"checked": True, "max_levels": col_levels, "points": col_points, "worst": col_worst}
    colour["without_colour"] = without
    return uv, colour


PALETTE = [(0.62, 0.60, 0.55), (0.45, 0.35, 0.25), (0.35, 0.50, 0.65), (0.75, 0.62, 0.30), (0.45, 0.60, 0.40),
           (0.65, 0.40, 0.50)]


def render_views(a_objs, b_objs, out, joint_x, y_range):
    """Close views of the joint (B already moved beside A): outside, inside, from above."""
    import bpy
    from mathutils import Vector
    sc = bpy.context.scene
    keep = set(a_objs) | set(b_objs)
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = o not in keep
    names = sorted({s.material.name for o in keep for s in o.material_slots if s.material})
    for o in keep:
        for s in o.material_slots:
            if s.material:
                s.material.diffuse_color = (*PALETTE[names.index(s.material.name) % len(PALETTE)], 1.0)
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = sc.render.resolution_y = 1200
    sc.render.film_transparent = False
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_backface_culling = True
    sc.display.shading.background_type = "VIEWPORT"
    sc.display.shading.background_color = (0.85, 0.9, 0.95)
    cam_data = bpy.data.cameras.new("jointcam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 16.0
    cam_data.clip_end = 500.0
    cam = bpy.data.objects.new("jointcam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    views = (("joint_outside", Vector((joint_x, y_range[1] + 40.0, 6.5)), Vector((0, -1, 0))),
             ("joint_inside", Vector((joint_x, y_range[0] - 40.0, 6.5)), Vector((0, 1, 0))),
             ("joint_top", Vector((joint_x, 0.0, 80.0)), Vector((0, 0, -1))))
    written = []
    for name, loc, look in views:
        cam.location = loc
        if look.z:
            cam.rotation_euler = (0.0, 0.0, 0.0)
        else:
            cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, name + ".png")
        bpy.ops.render.render(write_still=True)
        written.append(sc.render.filepath)
    return written


def load_piece(path, tag):
    objs = import_fbx(path)
    kept = visual_lod0(objs)
    if not kept:
        raise RuntimeError(f"{tag}: no visual LOD0 mesh in {path}")
    return objs, kept


def main():
    args = script_args()
    out = flag_value(args, "--out") or "."
    with job(report_path(out)) as report:
        ap = argparse.ArgumentParser()
        ap.add_argument("--a", required=True)
        ap.add_argument("--b", required=True)
        ap.add_argument("--out", required=True)
        ap.add_argument("--render", action="store_true")
        ns = ap.parse_args(args)
        os.makedirs(ns.out, exist_ok=True)
        t0 = time.time()
        report.update({"a": ns.a, "b": ns.b, "verdict": "ERROR"})
        all_a, kept_a = load_piece(ns.a, "A")
        all_b, kept_b = load_piece(ns.b, "B")
        pa, pb = read_piece(kept_a), read_piece(kept_b)
        xa, xb = float(pa["V"][:, 0].max()), float(pb["V"][:, 0].min())
        report["kept_objects"] = {"a": pa["names"], "b": pb["names"]}
        report["end_plane"] = {"a_max_x": xa, "b_min_x": xb}
        report["flatness"] = {"a": flatness(pa["V"][:, 0].tolist(), xa), "b": flatness(pb["V"][:, 0].tolist(), xb)}

        comps_a, dang_a = section_components(pa, xa - CUT_DEPTH)
        comps_b, dang_b = section_components(pb, xb + CUT_DEPTH)
        report["section_open_nodes"] = {"a": dang_a, "b": dang_b}
        ys = [s[i] for comps in (comps_a, comps_b) for c in comps for s in c for i in (0, 2)]
        zs = [s[i] for comps in (comps_a, comps_b) for c in comps for s in c for i in (1, 3)]
        if not ys:
            raise RuntimeError("neither piece has a section at the end plane")
        grid = make_grid(min(ys), max(ys), min(zs), max(zs))
        ma, mb = fill_components(comps_a, grid), fill_components(comps_b, grid)
        cmp_coarse = compare_masks(ma, mb)
        fine = make_grid(min(ys), max(ys), min(zs), max(zs), cell=FINE_CELL)
        cmp_ = compare_masks(fill_components(comps_a, fine), fill_components(comps_b, fine), FINE_CELL)
        report["grid"] = {k: grid[k] for k in ("y0", "z0", "cell", "ny", "nz")}
        report["fine_cell"] = FINE_CELL
        report["section_m2"] = {"a": cmp_["area_a"], "b": cmp_["area_b"], "both": cmp_["both"],
                                "only_a": cmp_["only_a"], "only_b": cmp_["only_b"]}
        report["mismatch_m2"] = cmp_["mismatch"]
        report["mismatch_m2_coarse_grid"] = cmp_coarse["mismatch"]
        w, h, px = diff_picture(ma, mb, grid, px=2 if grid["nz"] <= 700 else 1)
        report["diff_picture"] = os.path.join(ns.out, "joint_diff.png")
        write_png(report["diff_picture"], w, h, px)

        bva, bvb = build_bvh(pa), build_bvh(pb)
        open_a, tot_a = open_cells(pa, bva, xa, -1.0, ma, grid)
        open_b, tot_b = open_cells(pb, bvb, xb, 1.0, mb, grid)
        report["open_share"] = {"a": len(open_a) / tot_a if tot_a else 0.0, "b": len(open_b) / tot_b if tot_b else 0.0}
        report["open_area_m2"] = {"a": len(open_a) * CELL * CELL, "b": len(open_b) * CELL * CELL}
        for tag, cells in (("a", open_a), ("b", open_b)):
            if cells:
                report.setdefault("open_extent_yz", {})[tag] = {
                    "y": [min(c[0] for c in cells), max(c[0] for c in cells)],
                    "z": [min(c[1] for c in cells), max(c[1] for c in cells)]}
        report["uv_phase"], report["vertex_colour"] = surface_phase(pa, pb, bva, bvb, xa, xb)

        report["verdict"], report["reasons"] = decide(report)
        report["cannot_see"] = blind_spots(report)
        if ns.render:
            import bpy
            from mathutils import Matrix
            for o in all_b:
                o.matrix_world = Matrix.Translation((xa - xb, 0.0, 0.0)) @ o.matrix_world
            bpy.context.view_layer.update()
            y_all = pa["V"][:, 1].tolist() + pb["V"][:, 1].tolist()
            report["renders"] = render_views(kept_a, kept_b, ns.out, xa, (min(y_all), max(y_all)))
        report["seconds"] = round(time.time() - t0, 1)


if __name__ == "__main__":
    main()
