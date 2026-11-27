"""Read-only UV type and texel density measurement of FBX files, per visual mesh and material slot.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_measure_uv_density.py -- \\
        --out <report.json> [--extra-scale 0.01] [--uv-layer <index or name>] <a.fbx> <b.fbx> ...

Method (after texel_density_spread in blender_prep_tripo_prop.py): per triangle, density =
sqrt(uv_area / world_area) in UV units per metre; p5, median and p95 are area weighted.
World area uses matrix_world, then --extra-scale (a multiplier on lengths, applied on top of what
the importer did; the report records the importer's bounding box so the caller can check it).
LOD1+ (.lodN, _lodN), bo_ and UCX_ objects are skipped. The active UV layer is measured, or the one --uv-layer names (an index or a name; a mesh without
that layer reports "layer missing").

Labels (rule text is written into the report):
  tiling             >= 20 percent of face area has a UV outside 0..1 (1e-4 tolerance)
  overlapping atlas  otherwise, and sum(UV area) / covered area at 512 grid > 1.25
  unique             otherwise
Overlap uses pixel-centre sampling of triangles whose UVs are all inside 0..1; a triangle
smaller than a 512 grid cell can cover no centre, so the ratio is slightly high for dense meshes.
"""
import math
import os
import re
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender"))
from blender_job import import_fbx, job, script_args  # noqa: E402

GRID = 512
OUTSIDE_TILING_SHARE = 0.20
OVERLAP_RATIO = 1.25
EPS = 1e-4
LOD_RE = re.compile(r"(\.|_)lod[1-9]", re.I)
RULE = (f"tiling: >= {OUTSIDE_TILING_SHARE:.0%} of face area has a UV outside 0..1; "
        f"overlapping atlas: else, sum(UV area)/covered area at {GRID} grid > {OVERLAP_RATIO}; "
        "unique: else")


def is_visual(name):
    low = name.lower()
    return not (low.startswith("bo_") or low.startswith("ucx_") or LOD_RE.search(name))


def weighted_pct(vals, weights, q):
    order = np.argsort(vals)
    cum = np.cumsum(weights[order]) / weights.sum()
    return float(vals[order][min(np.searchsorted(cum, q), len(vals) - 1)])


def raster_covered(tris_uv):
    """Count of 512 grid cells whose centre lies in any triangle (N,3,2 array)."""
    grid = np.zeros((GRID, GRID), dtype=bool)
    for t in tris_uv:
        x0 = max(int(math.floor(t[:, 0].min() * GRID - 0.5)), 0)
        x1 = min(int(math.ceil(t[:, 0].max() * GRID - 0.5)), GRID - 1)
        y0 = max(int(math.floor(t[:, 1].min() * GRID - 0.5)), 0)
        y1 = min(int(math.ceil(t[:, 1].max() * GRID - 0.5)), GRID - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid((np.arange(x0, x1 + 1) + 0.5) / GRID, (np.arange(y0, y1 + 1) + 0.5) / GRID)
        a, b, c = t
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-18:
            continue
        w1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
        w2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
        w3 = 1.0 - w1 - w2
        inside = (w1 >= 0) & (w2 >= 0) & (w3 >= 0)
        grid[y0:y1 + 1, x0:x1 + 1] |= inside
    return int(grid.sum())


def measure_slot(world_area, uv_tri, uv_area):
    ok = (world_area > 1e-12) & (uv_area > 1e-14)
    wa, ua, tri = world_area[ok], uv_area[ok], uv_tri[ok]
    total_world = float(world_area.sum())
    if len(wa) == 0:
        return {"world_area_m2": total_world, "uv_area": float(uv_area.sum()), "note": "no measurable triangles"}
    dens = np.sqrt(ua / wa)
    lo, hi = tri.min(axis=(0, 1)), tri.max(axis=(0, 1))
    outside = ((tri < -EPS) | (tri > 1 + EPS)).any(axis=(1, 2))
    outside_share = float(wa[outside].sum() / wa.sum())
    inside_tris = tri[~outside]
    covered = raster_covered(inside_tris) if len(inside_tris) else 0
    in_uv_area = float(ua[~outside].sum())
    ratio = in_uv_area / (covered / (GRID * GRID)) if covered else None
    if outside_share >= OUTSIDE_TILING_SHARE:
        label = "tiling"
    elif ratio is not None and ratio > OVERLAP_RATIO:
        label = "overlapping atlas"
    else:
        label = "unique"
    med = weighted_pct(dens, wa, 0.5)
    return {
        "triangles": int(len(wa)),
        "world_area_m2": total_world,
        "uv_area_units2": float(uv_area.sum()),
        "uvpm_p5": weighted_pct(dens, wa, 0.05),
        "uvpm_median": med,
        "uvpm_p95": weighted_pct(dens, wa, 0.95),
        "uv_bounds": {"u_min": float(lo[0]), "u_max": float(hi[0]), "v_min": float(lo[1]), "v_max": float(hi[1])},
        "outside_0_1_area_share": outside_share,
        "overlap_ratio_inside_0_1": ratio,
        "covered_fraction_inside_0_1": covered / (GRID * GRID),
        "uv_type": label,
        "texels_per_m": {str(s): s * med for s in (1024, 2048, 4096)},
    }


def pick_layer(me, spec):
    """The UV layer to measure: the active one when spec is None, else by index or name (None if absent)."""
    if spec is None:
        return me.uv_layers.active
    if spec.isdigit():
        i = int(spec)
        return me.uv_layers[i] if i < len(me.uv_layers) else None
    return me.uv_layers.get(spec)


def measure_file(path, extra_scale, uv_spec=None):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import_fbx(path)
    bpy.context.view_layer.update()
    res = {"file": path, "extra_scale_applied": extra_scale, "objects": [], "skipped": []}
    allmin = np.array([1e30] * 3)
    allmax = np.array([-1e30] * 3)
    for o in sorted(bpy.context.scene.objects, key=lambda o: o.name):
        if o.type != "MESH":
            continue
        if not is_visual(o.name):
            res["skipped"].append(o.name)
            continue
        me = o.data
        me.calc_loop_triangles()
        mw = np.array(o.matrix_world)
        co = np.empty(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        wco = (co @ mw[:3, :3].T + mw[:3, 3]) * extra_scale
        if len(wco):
            allmin, allmax = np.minimum(allmin, wco.min(axis=0)), np.maximum(allmax, wco.max(axis=0))
        entry = {"name": o.name, "uv_layers": len(me.uv_layers),
                 "uv_layer_names": [u.name for u in me.uv_layers],
                 "uv_layer_measured": None,
                 "importer_scale": [float(v) for v in o.matrix_world.to_scale()],
                 "slots": {}}
        ntri = len(me.loop_triangles)
        layer = pick_layer(me, uv_spec)
        entry["uv_layer_measured"] = layer.name if layer else None
        if uv_spec is not None and layer is None and len(me.uv_layers) and ntri:
            entry["note"] = "layer missing"
            res["objects"].append(entry)
            continue
        if not len(me.uv_layers) or ntri == 0:
            entry["note"] = "no UV layer or no triangles"
            res["objects"].append(entry)
            continue
        tv = np.empty(ntri * 3, dtype=np.int64)
        tl = np.empty(ntri * 3, dtype=np.int64)
        me.loop_triangles.foreach_get("vertices", tv)
        me.loop_triangles.foreach_get("loops", tl)
        tpoly = np.empty(ntri, dtype=np.int64)
        me.loop_triangles.foreach_get("polygon_index", tpoly)
        pmat = np.empty(len(me.polygons), dtype=np.int64)
        me.polygons.foreach_get("material_index", pmat)
        tmat = pmat[tpoly]
        uvs = np.empty(len(me.loops) * 2)
        layer.data.foreach_get("uv", uvs)
        uvs = uvs.reshape(-1, 2)
        p = wco[tv.reshape(-1, 3)]
        world_area = 0.5 * np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1)
        u = uvs[tl.reshape(-1, 3)]
        uv_area = 0.5 * np.abs((u[:, 1, 0] - u[:, 0, 0]) * (u[:, 2, 1] - u[:, 0, 1])
                               - (u[:, 2, 0] - u[:, 0, 0]) * (u[:, 1, 1] - u[:, 0, 1]))
        for mi in sorted(set(tmat.tolist())):
            sel = tmat == mi
            mat = me.materials[mi].name if mi < len(me.materials) and me.materials[mi] else f"slot{mi}"
            entry["slots"][f"{mi}:{mat}"] = measure_slot(world_area[sel], u[sel], uv_area[sel])
        res["objects"].append(entry)
    res["bbox_m_after_scale"] = {"min": [float(v) for v in allmin], "max": [float(v) for v in allmax],
                                 "size": [float(v) for v in (allmax - allmin)]}
    return res


def main():
    args = script_args()
    out = args[args.index("--out") + 1]
    extra = float(args[args.index("--extra-scale") + 1]) if "--extra-scale" in args else 1.0
    uv_spec = args[args.index("--uv-layer") + 1] if "--uv-layer" in args else None
    files = [a for i, a in enumerate(args)
             if a.lower().endswith(".fbx") and (i == 0 or args[i - 1] not in ("--out", "--extra-scale", "--uv-layer"))]
    with job(out) as result:
        result["frame"] = "Blender import frame (no 180 degree Z turn)"
        result["rule"] = RULE
        result["grid"] = GRID
        result["uv_layer_requested"] = uv_spec
        result["files"] = []
        for f in files:
            result["files"].append(measure_file(f, extra, uv_spec))


if __name__ == "__main__":
    main()
