"""Blender script (blender-launcher.exe -b -P ... -- --out <fbx>): one flat quad of 1.333 m in the
XZ plane (normal +Y, the wall's outer face), UVs filling 0 to 1, for a Painter texture project.
U = 0.75 * x, V = 0.75 * z, the Rivendell texel density (one tile is 1.333 m).
Report: <fbx folder>/_report/DONE.txt (blender_job).
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender"))
import blender_job as bj  # noqa: E402

S = 4.0 / 3.0


def main():
    import bpy
    out = bj.flag_value(bj.script_args(), "--out")
    with bj.job(bj.report_path(os.path.dirname(out))) as rep:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mesh = bpy.data.meshes.new("tile_plane")
        mesh.from_pydata([(0, 0, 0), (S, 0, 0), (S, 0, S), (0, 0, S)], [], [(0, 3, 2, 1)])
        uv = mesh.uv_layers.new(name="UVMap")
        for loop, (u, v) in zip(mesh.loops, [(0, 0), (0, 1), (1, 1), (1, 0)]):
            uv.data[loop.index].uv = (u, v)
        mesh.update()
        mat = bpy.data.materials.new("rivendell_tile")
        mesh.materials.append(mat)
        obj = bpy.data.objects.new("tile_plane", mesh)
        bpy.context.scene.collection.objects.link(obj)
        bj.export_fbx(out, [obj])
        rep["size_m"] = S
        rep["normal"] = [round(x, 3) for x in mesh.polygons[0].normal]
        rep["uv"] = [tuple(round(c, 3) for c in d.uv) for d in uv.data]
        rep["fbx_bytes"] = os.path.getsize(out)


main()
