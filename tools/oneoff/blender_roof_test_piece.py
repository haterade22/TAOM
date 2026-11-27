"""Build a roof test piece: an ashlar wall slab under a gable roof, with the final material names.

Headless:  blender-launcher.exe -b --factory-startup -P tools/oneoff/blender_roof_test_piece.py -- --out <dir>
               [--kind roof|heap]   (heap: a rubble mound with the heap material, writes heap_test_piece.fbx)

The wall (x -4 to 4, y 1.5 to 2.5, z 0 to 6, outer face at y 2.5) has the material t_rivendell_wall_stone_a with a
box UV at 0.45 per metre. The two roof planes (eave at z 6, pitch 40 degrees, eaves at y 3.1 and y 0.9, ridge
at y 2.0) have t_rivendell_roof_slate_a: U along the eave, V up the slope at 0.45 per metre. Writes
<out>/roof_test_piece.fbx and <out>/_report/DONE.txt. The geometry numbers come from rivendell_roof_test_piece.py.
"""
import argparse
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
sys.path.insert(0, _HERE)
from blender_job import export_fbx, flag_value, job, report_path, script_args  # noqa: E402
import rivendell_roof_test_piece as rp  # noqa: E402

WALL = {"x": (-4.0, 4.0), "y": (1.5, 2.5), "z": (0.0, 6.0)}
STONE, SLATE = "t_rivendell_wall_stone_a", "t_rivendell_roof_slate_a"


def wall_faces():
    """Six quads (vertices counter-clockwise seen from outside) with their outward normals."""
    (x0, x1), (y0, y1), (z0, z1) = WALL["x"], WALL["y"], WALL["z"]
    return [
        ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0)),
        ([(x1, y0, z0), (x0, y0, z0), (x0, y0, z1), (x1, y0, z1)], (0, -1, 0)),
        ([(x1, y1, z0), (x1, y0, z0), (x1, y0, z1), (x1, y1, z1)], (1, 0, 0)),
        ([(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], (-1, 0, 0)),
        ([(x0, y0, z1), (x0, y1, z1), (x1, y1, z1), (x1, y0, z1)], (0, 0, 1)),
    ]


def build():
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mesh = bpy.data.meshes.new("roof_test_piece")
    obj = bpy.data.objects.new("roof_test_piece", mesh)
    bpy.context.scene.collection.objects.link(obj)
    for name in (STONE, SLATE):
        mesh.materials.append(bpy.data.materials.new(name))
    verts, faces, uvs, mats = [], [], [], []

    def quad(vs, uv, mat):
        base = len(verts)
        verts.extend(vs)
        faces.append((base, base + 1, base + 2, base + 3))
        uvs.append(uv)
        mats.append(mat)

    for vs, n in wall_faces():
        quad(vs, [rp.box_uv(v, n) for v in vs], 0)
    front = rp.roof_slope(-4.4, 4.4, 3.1, 2.0, 6.0, 40.0)
    back = rp.roof_slope(-4.4, 4.4, 0.9, 2.0, 6.0, 40.0)
    quad(front["verts"], front["uvs"], 1)
    quad(list(reversed(back["verts"])), list(reversed(back["uvs"])), 1)
    mesh.from_pydata(verts, [], faces)
    layer = mesh.uv_layers.new(name="UVMap")
    for poly, uv, mat in zip(mesh.polygons, uvs, mats):
        poly.material_index = mat
        for k, li in enumerate(poly.loop_indices):
            layer.data[li].uv = uv[k]
    mesh.update()
    # Faces whose normal points the wrong way are turned by the importer's own rules; the roof is double sided
    # in the picture tool, so the back slope's winding does not matter for the look.
    return obj


HEAP = "t_rivendell_wall_heap_a"


def build_heap():
    """A rubble mound for the heap set: a flattened, roughened ico-sphere (6 m long, 3 m wide, 1.8 m high) with a
    box UV by the axis of each face normal at 0.45 per metre, as the breach heap of the gatehouse is mapped."""
    import bmesh
    import bpy
    import math
    import random
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mesh = bpy.data.meshes.new("heap_test_piece")
    obj = bpy.data.objects.new("heap_test_piece", mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(bpy.data.materials.new(HEAP))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    rnd = random.Random(7)
    for v in bm.verts:
        k = 1.0 + 0.18 * (rnd.random() - 0.5) + 0.12 * math.sin(7 * v.co.x) * math.sin(5 * v.co.y + 1.0)
        v.co.x *= 3.0 * k
        v.co.y *= 1.5 * k
        v.co.z = max(v.co.z, -0.1) * 1.8 * k
    bm.normal_update()
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        f.material_index = 0
        for loop in f.loops:
            loop[uv].uv = rp.box_uv(loop.vert.co, f.normal)
    bm.to_mesh(mesh)
    bm.free()
    obj.location = (0.0, 2.5, 0.0)
    return obj


def main():
    args = script_args()
    out = flag_value(args, "--out")
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--kind", choices=("roof", "heap"), default="roof")
    a = ap.parse_args(args)
    with job(report_path(out)) as report:
        obj = build() if a.kind == "roof" else build_heap()
        os.makedirs(out, exist_ok=True)
        path = os.path.join(out, "%s_test_piece.fbx" % a.kind)
        export_fbx(path, [obj])
        report["fbx"] = path
        report["materials"] = [m.name for m in obj.data.materials]
        report["polygons"] = len(obj.data.polygons)


if __name__ == "__main__":
    main()
