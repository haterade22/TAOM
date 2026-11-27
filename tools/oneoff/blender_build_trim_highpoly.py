"""Blender script: the high-poly model of the Rivendell trim sheet, ready for a bake onto a flat plane.

    blender-launcher.exe -b --factory-startup -P blender_build_trim_highpoly.py -- --out <dir>

Writes <dir>/trim_high.fbx (S1_high to S5_high), <dir>/trim_low.fbx (S1_low to S5_low and trim_low),
<dir>/trim_highpoly.blend and <dir>/_report/DONE.txt (blender_job).

Frame: metres, the sheet lies in XY, X = U * TILE_M, Y = V * TILE_M, the faces look up (+Z). The low-poly
plane is at Z = 0. The relief stands on it: every strip rests at Z_BASE and nothing rises above Z_LIMIT.
Each high-poly strip is built over THREE tiles (X from -TILE_M to 2 TILE_M), one tile past each side of
the plane, so that a bake has no fault at the U edge. The heights come from rivendell_trim_strips.py.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "blender"))
import blender_job as bj  # noqa: E402
import rivendell_trim_strips as rts  # noqa: E402


def strip_grid(name):
    """Vertices (x, y, z) and quads of one high-poly strip over TILES tiles, as numpy arrays."""
    import numpy as np
    ul, sl = rts.u_lines(name), rts.s_lines(name)
    nu, nv = len(ul) - 1, len(sl) - 1
    y0, _ = rts.strip_bounds_m(name)
    one = np.array([[rts.height(name, u, s) for s in sl] for u in ul[:-1]])  # one period, nu x (nv + 1)
    h = np.concatenate([np.tile(one, (rts.TILES, 1)), one[:1]])
    xs = np.array([(t - 1 + u) * rts.TILE_M for t in range(rts.TILES) for u in ul[:-1]] + [2.0 * rts.TILE_M])
    ys = y0 + np.array(sl)
    gx, gy = np.meshgrid(xs, ys, indexing="ij")
    verts = np.stack([gx, gy, h], axis=-1).reshape(-1, 3)
    iu, iv = np.meshgrid(np.arange(len(xs) - 1), np.arange(nv), indexing="ij")
    a = (iu * (nv + 1) + iv).ravel()
    quads = np.stack([a, a + (nv + 1), a + (nv + 1) + 1, a + 1], axis=1)
    return verts, quads


def make_object(bpy, name, verts, faces, uvs=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts.tolist(), [], faces.tolist())
    if uvs is not None:
        layer = mesh.uv_layers.new(name="UVMap")
        for poly in mesh.polygons:
            for k, loop in enumerate(poly.loop_indices):
                layer.data[loop].uv = uvs[k]
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    return obj


def flat_quad(x0, y0, x1, y1):
    import numpy as np
    return np.array([[x0, y0, 0.0], [x1, y0, 0.0], [x1, y1, 0.0], [x0, y1, 0.0]]), np.array([[0, 1, 2, 3]])


def main():
    import bpy
    args = bj.script_args()
    out = bj.flag_value(args, "--out")
    with bj.job(bj.report_path(out)) as rep:
        os.makedirs(out, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        scene.unit_settings.system = "METRIC"
        high_col = bpy.data.collections.new("high")
        low_col = bpy.data.collections.new("low")
        scene.collection.children.link(high_col)
        scene.collection.children.link(low_col)
        highs, lows, stats = [], [], {}
        total_tris = 0
        for name in rts.STRIPS:
            verts, quads = strip_grid(name)
            obj = make_object(bpy, name + "_high", verts, quads)
            obj.data.polygons.foreach_set("use_smooth", [True] * len(obj.data.polygons))
            high_col.objects.link(obj)
            highs.append(obj)
            tris = 2 * len(quads)
            total_tris += tris
            stats[name + "_high"] = {"vertices": len(verts), "triangles": tris,
                                     "x": [float(verts[:, 0].min()), float(verts[:, 0].max())],
                                     "y": [float(verts[:, 1].min()), float(verts[:, 1].max())],
                                     "z": [float(verts[:, 2].min()), float(verts[:, 2].max())]}
            v0, v1 = rts.STRIPS[name]
            y0, y1 = rts.strip_bounds_m(name)
            lv, lf = flat_quad(0.0, y0, rts.TILE_M, y1)
            low = make_object(bpy, name + "_low", lv, lf, [(0.0, v0), (1.0, v0), (1.0, v1), (0.0, v1)])
            low_col.objects.link(low)
            lows.append(low)
        lv, lf = flat_quad(0.0, 0.0, rts.TILE_M, rts.TILE_M)
        whole = make_object(bpy, "trim_low", lv, lf, [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)])
        low_col.objects.link(whole)
        lows.append(whole)

        bj.export_fbx(os.path.join(out, "trim_high.fbx"), highs)
        bj.export_fbx(os.path.join(out, "trim_low.fbx"), lows)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "trim_highpoly.blend"), compress=True)
        rep["blender"] = bpy.app.version_string
        rep["objects"] = stats
        rep["triangles_total"] = total_tris
        rep["tile_m"] = rts.TILE_M
        rep["z_base_m"] = rts.Z_BASE
        rep["z_limit_m"] = rts.Z_LIMIT
        rep["files"] = {n: os.path.getsize(os.path.join(out, n))
                        for n in ("trim_high.fbx", "trim_low.fbx", "trim_highpoly.blend")}


main()
