"""Headless Blender script: make a one-plane copy of the trim low-poly FBX for the Painter bake.

Run: blender-launcher.exe -b --factory-startup -P painter_trim_plane.py -- --blend <trim_highpoly.blend> --out <dir>
Opens the .blend that wrote the FBX pair (the FBX importer returns the plane 100 times too large and
turned, so a re-export would not match trim_high.fbx), keeps only the object named trim_low, gives it
one material named rivendell_tile (the texture set takes the FBX material name) and writes
<dir>/trim_plane_low.fbx with the Kit export settings, the same ones that wrote trim_high.fbx.
Report: <dir>/_report/DONE.txt.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "blender"))
import blender_job as bj  # noqa: E402


def main():
    import bpy
    args = bj.script_args()
    out = bj.flag_value(args, "--out")
    blend = bj.flag_value(args, "--blend")
    with bj.job(bj.report_path(out)) as rep:
        bpy.ops.wm.open_mainfile(filepath=blend)
        objs = [o for o in bpy.data.objects if o.type == "MESH" and o.name.endswith("_low") or o.name == "trim_low"]
        rep["imported"] = sorted(o.name for o in objs)
        plane = [o for o in objs if o.name == "trim_low"]
        if len(plane) != 1:
            raise RuntimeError("expected one object named trim_low, found %s" % rep["imported"])
        for o in objs:
            if o is not plane[0]:
                bpy.data.objects.remove(o, do_unlink=True)
        obj = plane[0]
        mat = bpy.data.materials.new("rivendell_tile")
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        xs = [v.co.x for v in obj.data.vertices]
        ys = [v.co.y for v in obj.data.vertices]
        zs = [v.co.z for v in obj.data.vertices]
        rep["bounds_blender"] = {"x": [min(xs), max(xs)], "y": [min(ys), max(ys)], "z": [min(zs), max(zs)]}
        rep["polygons"] = len(obj.data.polygons)
        rep["uv_layers"] = [u.name for u in obj.data.uv_layers]
        os.makedirs(out, exist_ok=True)
        bj.export_fbx(os.path.join(out, "trim_plane_low.fbx"), [obj])
        rep["written"] = os.path.join(out, "trim_plane_low.fbx")


main()
