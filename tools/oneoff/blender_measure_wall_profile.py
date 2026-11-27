"""Measure the geometry of one castle wall or gatehouse FBX, read only, as JSON (metres, Blender import frame).

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_measure_wall_profile.py -- <in.fbx> <out.json> --kind solid|broken|gatehouse

The Store launcher detaches with no stdout: wait for <out.json> (tools/blender/blender_job.py writes it on
every exit, "status" first). Positions are in Blender's import frame; the engine reads a Gondor kit FBX
turned 180 degrees about Z from it (docs/kitbash/gondor/design-patterns.md).

Visual meshes are the LOD0 objects (no .lodN, no bo_ bodies, no .decal planes), grouped by the prefix before
the first dot. The wall is assumed to run along X and to be thin along Y; "axis_check" reports the bounds
so a caller can see when that is wrong.

What is measured:
  - bounds per prefix and total, triangles per LOD tier, bo_ body triangles and bounds, materials;
  - wall walk: rays down on a GRID m lattice; the deck is the most common first-hit height. Deck depth is the
    run of deck-height cells across Y per X column; the parapet side is the Y side whose highest first hit
    above the deck is greater;
  - thickness: rays along Y at deck level and at z = 0.01, solid length summed from entry and exit faces;
  - gatehouse: rays along Y on an X by Z lattice; a free ray hits nothing. Free cells that do not reach the
    left, right or top lattice border are openings; each is refined with rays along X and Z;
  - broken wall: the longest run of X columns without deck cells is the gap; rubble reach per Y side is
    measured from the vertices in the gap against the intact wall's Y extent; rubble top faces are the faces
    a downward ray hits first inside the gap, split at 35 degrees of slope.
"""
import argparse
import math
import os
import statistics
import sys
from collections import Counter

import bpy

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
from blender_job import import_fbx, job, script_args  # noqa: E402
from kitbash_kits import is_decal, split_name, strip_dup  # noqa: E402

GRID = 0.25
PLATEAU_STEP = 0.05
SLOPE_LIMIT_DEG = 35.0
MAX_HITS = 64


def r(v, n=3):
    return round(float(v), n)


def classify(name):
    kind, base, lod = split_name(strip_dup(name))
    if kind == "lod" and lod == "0":
        kind = "base"
    prefix = base.split(".")[0].lower()
    return kind, base, lod, prefix


def world_mesh(obj):
    """(vertices as tuples, triangles as index tuples, per triangle area) in world space."""
    me = obj.data
    me.calc_loop_triangles()
    mw = obj.matrix_world
    verts = [tuple(mw @ v.co) for v in me.vertices]
    tris = [tuple(t.vertices) for t in me.loop_triangles]
    return verts, tris


def bounds_of(points):
    if not points:
        return None
    lo = [min(p[k] for p in points) for k in range(3)]
    hi = [max(p[k] for p in points) for k in range(3)]
    return {"min": [r(v) for v in lo], "max": [r(v) for v in hi], "size": [r(hi[k] - lo[k]) for k in range(3)]}


def frange(lo, hi, step):
    n = int(math.floor((hi - lo) / step + 1e-9))
    return [lo + step * (i + 0.5) for i in range(max(n, 1))]


def tri_area(a, b, c):
    ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    x, y, z = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    return 0.5 * math.sqrt(x * x + y * y + z * z)


def all_hits(tree, origin, direction, limit):
    """Every hit along a ray as (distance, location, normal, face index)."""
    from mathutils import Vector
    hits = []
    o = Vector(origin)
    d = Vector(direction)
    travelled = 0.0
    while len(hits) < MAX_HITS:
        loc, nrm, idx, dist = tree.ray_cast(o, d, limit - travelled)
        if loc is None:
            break
        travelled += dist
        hits.append((travelled, tuple(loc), tuple(nrm), idx))
        o = loc + d * 1e-4
        travelled += 1e-4
    return hits


def runs(flags):
    """[(start index, length)] of consecutive True values."""
    out, start = [], None
    for i, f in enumerate(flags + [False]):
        if f and start is None:
            start = i
        elif not f and start is not None:
            out.append((start, i - start))
            start = None
    return out


def measure_top(tree, lo, hi):
    """First hit from above on the lattice: {(ix, iy): (z, face, normal)} with the lattice coordinates."""
    xs = frange(lo[0], hi[0], GRID)
    ys = frange(lo[1], hi[1], GRID)
    top = {}
    for ix, x in enumerate(xs):
        for iy, y in enumerate(ys):
            hits = all_hits(tree, (x, y, hi[2] + 1.0), (0, 0, -1), hi[2] - lo[2] + 2.0)
            if hits:
                _d, loc, nrm, idx = hits[0]
                top[(ix, iy)] = (loc[2], idx, nrm)
    return xs, ys, top


def plateaus(top, count=6):
    c = Counter(round(v[0] / PLATEAU_STEP) * PLATEAU_STEP for v in top.values())
    total = sum(c.values()) or 1
    return [{"z": r(z, 2), "cells": n, "share": r(n / total, 4)} for z, n in c.most_common(count)]


def wall_walk(xs, ys, top):
    out = {"grid_m": GRID}
    if not top:
        out["error"] = "no first hits"
        return out
    pl = plateaus(top)
    out["plateaus"] = pl
    deck = pl[0]["z"]
    out["deck_z"] = deck
    band = 0.1
    deck_cells = {k for k, v in top.items() if abs(v[0] - deck) <= band}
    depths, centers, y_lo_edge, y_hi_edge = [], [], [], []
    for ix in range(len(xs)):
        col = [iy for iy in range(len(ys)) if (ix, iy) in deck_cells]
        if not col:
            continue
        flags = [(ix, iy) in deck_cells for iy in range(len(ys))]
        rs = max(runs(flags), key=lambda t: t[1])
        depths.append(rs[1] * GRID)
        a, b = ys[rs[0]] - GRID / 2, ys[rs[0] + rs[1] - 1] + GRID / 2
        y_lo_edge.append(a)
        y_hi_edge.append(b)
        centers.append((a + b) / 2)
    if not depths:
        out["error"] = "no deck cells"
        return out
    out["deck_columns"] = len(depths)
    out["deck_depth_m"] = {"median": r(statistics.median(depths)), "min": r(min(depths)), "max": r(max(depths))}
    yc = statistics.median(centers)
    out["deck_y_range"] = [r(statistics.median(y_lo_edge)), r(statistics.median(y_hi_edge))]
    ylo, yhi = out["deck_y_range"]
    return out


def parapet(xs, ys, top_all, deck, deck_y):
    """Parapet rise above the deck per Y side, from the first hits on every LOD0 part (merlon parts included)."""
    ylo, yhi = deck_y
    ymid = (ylo + yhi) / 2
    out = {"deck_z": deck, "deck_y_mid": r(ymid), "rise_min_m": 0.1}
    for name, test in (("+Y", lambda y: y > ymid), ("-Y", lambda y: y < ymid)):
        colmax, ycells, zall = [], [], []
        for ix in range(len(xs)):
            zs = [(top_all[(ix, iy)][0], ys[iy]) for iy in range(len(ys)) if test(ys[iy]) and (ix, iy) in top_all]
            hi = [(z, y) for z, y in zs if z > deck + 0.1]
            if zs:
                colmax.append(max(z for z, _y in zs))
            ycells += [y for _z, y in hi]
            zall += [z for z, _y in hi]
        if not zall:
            out[name] = {"rise_cells": 0}
            continue
        out[name] = {"rise_cells": len(zall), "highest_z": r(max(zall)), "highest_above_deck": r(max(zall) - deck),
                     "median_above_deck_of_cells_over_deck": r(statistics.median(zall) - deck),
                     "median_of_column_highest_above_deck": r(statistics.median(colmax) - deck),
                     "share_of_columns_with_rise": r(sum(1 for z in colmax if z > deck + 0.1) / len(colmax), 3),
                     "y_range_of_rise_cells": [r(min(ycells) - GRID / 2), r(max(ycells) + GRID / 2)]}
    a, b = out.get("+Y", {}).get("highest_above_deck"), out.get("-Y", {}).get("highest_above_deck")
    if a is not None and b is not None:
        out["higher_parapet_side"] = "+Y" if a > b else ("-Y" if b > a else "equal")
    flags_runs = []
    for ix in range(len(xs)):
        flags = [(ix, iy) in top_all and abs(top_all[(ix, iy)][0] - deck) <= 0.1 for iy in range(len(ys))]
        if any(flags):
            flags_runs.append(max(n for _s, n in runs(flags)) * GRID)
    if flags_runs:
        out["clear_deck_depth_with_parapets_m"] = {"median": r(statistics.median(flags_runs)), "min": r(min(flags_runs)),
                                                    "max": r(max(flags_runs))}
    return out


def thickness_at(tree, lo, hi, z, xs):
    spans, solids, nohit = [], [], 0
    length = hi[1] - lo[1] + 2.0
    for x in xs:
        hits = all_hits(tree, (x, lo[1] - 1.0, z), (0, 1, 0), length)
        if not hits:
            nohit += 1
            continue
        spans.append(hits[-1][1][1] - hits[0][1][1])
        solid, entry = 0.0, None
        for _d, loc, nrm, _i in hits:
            if nrm[1] < 0 and entry is None:  # normal faces the ray origin: entering solid
                entry = loc[1]
            elif nrm[1] > 0 and entry is not None:
                solid += loc[1] - entry
                entry = None
        solids.append(solid)

    def stat(v):
        return {"median": r(statistics.median(v)), "min": r(min(v)), "max": r(max(v))} if v else None

    return {"z": r(z, 3), "columns_with_hits": len(spans), "columns_without_hits": nohit,
            "outer_span_m": stat(spans), "solid_length_m": stat(solids)}


def passage(tree, lo, hi):
    xs = frange(lo[0], hi[0], GRID)
    zs = frange(lo[2], hi[2], GRID)
    length = hi[1] - lo[1] + 2.0
    free = set()
    firsts = {}
    for ix, x in enumerate(xs):
        for iz, z in enumerate(zs):
            hits = all_hits(tree, (x, lo[1] - 1.0, z), (0, 1, 0), length)
            if not hits:
                free.add((ix, iz))
            else:
                firsts[(ix, iz)] = (hits[0][1][1], hits[-1][1][1])
    seen, comps = set(), []
    for cell in sorted(free):
        if cell in seen:
            continue
        stack, comp = [cell], []
        seen.add(cell)
        while stack:
            c = stack.pop()
            comp.append(c)
            for n in ((c[0] + 1, c[1]), (c[0] - 1, c[1]), (c[0], c[1] + 1), (c[0], c[1] - 1)):
                if n in free and n not in seen:
                    seen.add(n)
                    stack.append(n)
        comps.append(comp)
    openings = []
    for comp in comps:
        ixs = [c[0] for c in comp]
        izs = [c[1] for c in comp]
        outside = min(ixs) == 0 or max(ixs) == len(xs) - 1 or max(izs) == len(zs) - 1
        if outside:
            continue
        x0, x1 = xs[min(ixs)] - GRID / 2, xs[max(ixs)] + GRID / 2
        z0, z1 = zs[min(izs)] - GRID / 2, zs[max(izs)] + GRID / 2
        ring = []
        for ix, iz in comp:
            for n in ((ix + 1, iz), (ix - 1, iz), (ix, iz + 1), (ix, iz - 1)):
                if n in firsts:
                    ring.append(firsts[n])
        o = {"cells": len(comp), "grid_x_range": [r(x0), r(x1)], "grid_z_range": [r(z0), r(z1)],
             "grid_width_m": r(x1 - x0), "grid_height_m": r(z1 - z0),
             "touches_lattice_bottom": min(izs) == 0}
        if ring:
            o["depth_from_neighbour_hits"] = {"y_min": r(min(a for a, _b in ring)), "y_max": r(max(b for _a, b in ring)),
                                              "depth_m": r(max(b for _a, b in ring) - min(a for a, _b in ring))}
        xm, zm = (x0 + x1) / 2, (z0 + z1) / 2
        ym = (lo[1] + hi[1]) / 2
        ref = {"probe_x": r(xm), "probe_z": r(zm), "probe_y": r(ym)}
        up = all_hits(tree, (xm, ym, zm), (0, 0, 1), hi[2] - zm + 2.0)
        down = all_hits(tree, (xm, ym, zm), (0, 0, -1), zm - lo[2] + 2.0)
        left = all_hits(tree, (xm, ym, zm), (-1, 0, 0), xm - lo[0] + 2.0)
        right = all_hits(tree, (xm, ym, zm), (1, 0, 0), hi[0] - xm + 2.0)
        ref["ceiling_z"] = r(up[0][1][2]) if up else None
        ref["floor_z"] = r(down[0][1][2]) if down else None
        ref["left_x"] = r(left[0][1][0]) if left else None
        ref["right_x"] = r(right[0][1][0]) if right else None
        if ref["left_x"] is not None and ref["right_x"] is not None:
            ref["width_m"] = r(ref["right_x"] - ref["left_x"])
        if ref["ceiling_z"] is not None and ref["floor_z"] is not None:
            ref["height_m"] = r(ref["ceiling_z"] - ref["floor_z"])
        o["refined_at_mid_y"] = ref
        openings.append(o)
    openings.sort(key=lambda o: -o["cells"])
    return {"grid_m": GRID, "free_cells": len(free), "openings": openings}


def slope_split(faces, face_area, tri_sub, keep):
    """Area and count split at SLOPE_LIMIT_DEG for the first-hit faces whose sub-object passes `keep`."""
    walk = steep = 0.0
    cw = cs = 0
    for idx, nrm in faces.items():
        if not keep(tri_sub[idx]):
            continue
        slope = math.degrees(math.acos(max(-1.0, min(1.0, nrm[2]))))
        if slope < SLOPE_LIMIT_DEG:
            walk += face_area[idx]
            cw += 1
        else:
            steep += face_area[idx]
            cs += 1
    tot = walk + steep
    return {"faces_hit_first": cw + cs, "slope_limit_deg": SLOPE_LIMIT_DEG,
            "area_share_under_limit": r(walk / tot, 4) if tot else None,
            "area_share_over_limit": r(steep / tot, 4) if tot else None,
            "face_count_under_limit": cw, "face_count_over_limit": cs}


def gap_report(face_area, tri_sub, subs_pts, xs, ys, top, deck, deck_y):
    """The gap is the longest run of X columns where fewer than half of the deck-band cells are at deck height.
    Debris is every sub-object that is not `.wall` (brick, rubble); `.wall` is the remaining wall mesh."""
    out = {}
    ylo, yhi = deck_y
    band = 0.1
    intact = []
    for ix in range(len(xs)):
        total = sum(1 for iy in range(len(ys)) if ylo <= ys[iy] <= yhi)
        have = sum(1 for iy in range(len(ys)) if ylo <= ys[iy] <= yhi and (ix, iy) in top
                   and abs(top[(ix, iy)][0] - deck) <= band)
        intact.append(total > 0 and have / total >= 0.5)
    gaps = runs([not f for f in intact])
    out["gap_runs"] = [{"x_start": r(xs[s] - GRID / 2), "x_end": r(xs[s + n - 1] + GRID / 2), "width_m": r(n * GRID)}
                       for s, n in gaps]
    if not gaps:
        out["error"] = "no gap at deck level"
        return out
    s, n = max(gaps, key=lambda t: t[1])
    gx0, gx1 = xs[s] - GRID / 2, xs[s + n - 1] + GRID / 2
    out["gap"] = {"x_start": r(gx0), "x_end": r(gx1), "width_m": r(gx1 - gx0)}
    wall_pts = subs_pts.get("wall")
    foot = [v[1] for v in wall_pts] if wall_pts else None
    wy0, wy1 = (min(foot), max(foot)) if foot else (ylo, yhi)
    out["wall_footprint_y"] = {"source": "the .wall sub-object" if foot else "deck_y_range",
                               "y_min": r(wy0), "y_max": r(wy1)}
    debris = {k: [v for v in pts if gx0 <= v[0] <= gx1] for k, pts in subs_pts.items() if k != "wall"}
    rem = [v for v in subs_pts.get("wall", []) if gx0 <= v[0] <= gx1]
    d_all = [v for pts in debris.values() for v in pts]
    if d_all:
        out["debris_in_gap"] = {
            "y_range": [r(min(v[1] for v in d_all)), r(max(v[1] for v in d_all))],
            "reach_beyond_wall_footprint_m": {"+Y": r(max(0.0, max(v[1] for v in d_all) - wy1)),
                                              "-Y": r(max(0.0, wy0 - min(v[1] for v in d_all)))},
            "highest_vertex_z": r(max(v[2] for v in d_all)),
            "per_sub_object": {k: {"y_range": [r(min(v[1] for v in pts)), r(max(v[1] for v in pts))],
                                   "highest_vertex_z": r(max(v[2] for v in pts))} for k, pts in debris.items() if pts}}
    if rem:
        out["remaining_wall_mesh_in_gap"] = {"highest_vertex_z": r(max(v[2] for v in rem)),
                                              "y_range": [r(min(v[1] for v in rem)), r(max(v[1] for v in rem))]}
    cells = [(k, v) for k, v in top.items() if gx0 <= xs[k[0]] <= gx1]
    if cells:
        out["highest_first_hit_in_gap_z"] = r(max(v[0] for _k, v in cells))
        in_band = [v[0] for k, v in cells if ylo <= ys[k[1]] <= yhi]
        out["highest_first_hit_in_gap_inside_deck_y_z"] = r(max(in_band)) if in_band else None
        faces = {idx: nrm for _k, (_z, idx, nrm) in cells}
        out["top_faces_all"] = slope_split(faces, face_area, tri_sub, lambda sub: True)
        out["top_faces_debris_only"] = slope_split(faces, face_area, tri_sub, lambda sub: sub != "wall")
        deb = [(k, v) for k, v in cells if tri_sub[v[1]] != "wall"]
        dw = sum(1 for _k, (_z, _i, nrm) in deb if math.degrees(math.acos(max(-1.0, min(1.0, nrm[2])))) < SLOPE_LIMIT_DEG)
        out["top_faces_debris_only"]["projected_cell_share_under_limit"] = r(dw / len(deb), 4) if deb else None
        out["top_faces_debris_only"]["projected_cells"] = len(deb)
        out["highest_debris_first_hit_z"] = r(max(v[0] for _k, v in deb)) if deb else None
    return out


def main():
    args = script_args()
    out = args[1] if len(args) > 1 else "profile.json"
    with job(out) as result:
        p = argparse.ArgumentParser(allow_abbrev=False)
        p.add_argument("fbx")
        p.add_argument("out")
        p.add_argument("--kind", choices=["solid", "broken", "gatehouse"], required=True)
        a = p.parse_args(args)
        result["src"] = a.fbx
        result["kind"] = a.kind
        result["frame"] = "Blender import frame, metres; the engine reads the kit turned 180 degrees about Z"
        bpy.ops.wm.read_factory_settings(use_empty=True)
        import_fbx(a.fbx)
        bpy.context.view_layer.update()
        result["scene_unit_scale"] = bpy.context.scene.unit_settings.scale_length

        stem = os.path.splitext(os.path.basename(a.fbx))[0].lower()
        result["main_prefix"] = stem
        tiers, tiers_main, body_tris, body_pts, decals = {}, {}, 0, [], 0
        body_main_tris, body_main_pts = 0, []
        groups, mats = {}, {}
        verts_all, tris_all, face_area = [], [], []
        every_verts, every_tris = [], []
        tri_sub, subs_pts = [], {}
        for o in bpy.context.scene.objects:
            if o.type != "MESH":
                continue
            kind, base, lod, prefix = classify(o.name)
            verts, tris = world_mesh(o)
            is_main = prefix == stem
            if kind == "bo":
                body_tris += len(tris)
                body_pts += verts
                if is_main:
                    body_main_tris += len(tris)
                    body_main_pts += verts
                continue
            if kind == "lod":
                for scope in ((tiers, tiers_main) if is_main else (tiers,)):
                    t = scope.setdefault("lod" + (lod or "?"), {"tris": 0, "objects": 0})
                    t["tris"] += len(tris)
                    t["objects"] += 1
                continue
            if is_decal(o.name):
                decals += 1
                continue
            for scope in ((tiers, tiers_main) if is_main else (tiers,)):
                t0 = scope.setdefault("lod0", {"tris": 0, "objects": 0})
                t0["tris"] += len(tris)
                t0["objects"] += 1
            g = groups.setdefault(prefix, {"objects": [], "tris": 0, "pts": []})
            g["objects"].append(o.name)
            g["tris"] += len(tris)
            g["pts"] += verts
            mats.setdefault(prefix, set()).update(m.name for m in o.data.materials if m)
            off2 = len(every_verts)
            every_verts += verts
            every_tris += [(t[0] + off2, t[1] + off2, t[2] + off2) for t in tris]
            if not is_main:
                continue
            sub = base.split(".", 1)[1].lower() if "." in base else ""
            tri_sub += [sub] * len(tris)
            subs_pts.setdefault(sub, []).extend(verts)
            off = len(verts_all)
            verts_all += verts
            for t in tris:
                tris_all.append((t[0] + off, t[1] + off, t[2] + off))
                face_area.append(tri_area(verts_all[t[0] + off], verts_all[t[1] + off], verts_all[t[2] + off]))
        result["lod_tiers_all_objects"] = dict(sorted(tiers.items()))
        result["lod_tiers_main_prefix"] = dict(sorted(tiers_main.items()))
        result["body_main_prefix"] = {"tris": body_main_tris, "bounds": bounds_of(body_main_pts)}
        result["decal_objects_excluded"] = decals
        result["body"] = {"tris": body_tris, "bounds": bounds_of(body_pts)}
        result["visual_bounds_all_objects"] = bounds_of([v for g in groups.values() for v in g["pts"]])
        result["visual_bounds"] = bounds_of(verts_all)  # main prefix only: the ray target
        result["prefixes"] = {k: {"objects": g["objects"], "tris_lod0": g["tris"], "bounds": bounds_of(g["pts"]),
                                  "materials": sorted(mats.get(k, []))} for k, g in sorted(groups.items())}
        result["materials"] = sorted(set().union(*mats.values())) if mats else []
        b = result["visual_bounds"]
        if not b:
            raise RuntimeError("no visual LOD0 mesh under the main prefix " + stem)
        result["axis_check"] = {"size": b["size"], "assumed_length_axis": "X",
                                "x_is_longest_horizontal": b["size"][0] >= b["size"][1]}

        from mathutils.bvhtree import BVHTree
        tree = BVHTree.FromPolygons(verts_all, tris_all)
        tree_all = BVHTree.FromPolygons(every_verts, every_tris)
        lo, hi = b["min"], b["max"]
        xs, ys, top = measure_top(tree, lo, hi)
        walk = wall_walk(xs, ys, top)
        result["wall_walk"] = walk
        result["wall_walk"]["note"] = "deck, thickness, passage and gap use the main prefix only; parapet uses every LOD0 part"
        ab = result["visual_bounds_all_objects"]
        if "deck_z" in walk:
            _xa, _ya, top_all = measure_top(tree_all, ab["min"], ab["max"])
            result["parapet"] = parapet(_xa, _ya, top_all, walk["deck_z"], walk["deck_y_range"])
            result["parapet"]["lattice_y_origin"] = ab["min"][1]
        if "deck_z" in walk:
            xcols = frange(lo[0], hi[0], GRID)
            result["thickness"] = {
                "at_deck_level": thickness_at(tree, lo, hi, walk["deck_z"] - 0.05, xcols),
                "at_z_0": thickness_at(tree, lo, hi, 0.01, xcols),
                "at_base": thickness_at(tree, lo, hi, lo[2] + 0.05, xcols),
                "note": "deck level is deck_z minus 0.05 m; z = 0 is 0.01 m; base is the lowest visual z plus 0.05 m"}
        if a.kind == "gatehouse":
            result["passage"] = passage(tree, lo, hi)
        if a.kind == "broken" and "deck_z" in walk:
            result["gap"] = gap_report(face_area, tri_sub, subs_pts, xs, ys, top, walk["deck_z"], walk["deck_y_range"])


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
