"""Standard QA views of a mesh, rendered the way the engine draws it: back faces culled.

Blender's viewport and Cycles draw both sides of a face; a Bannerlord material culls back faces
unless it is two-sided. So a face wound the wrong way looks right in every ordinary render and is
a hole in game. These views cull back faces and put a magenta world behind the mesh: a hole shows
as magenta, or as the inside of the mesh seen through a missing wall.

Headless only (the Store launcher detaches: wait for <out>/_report/DONE.txt):

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/qa_views.py --
        (--fbx <in.fbx> | --blend <in.blend>) --out <dir> [--res 960] [--only <metamesh> ...] [--bodies]

It changes the scene's render settings, camera and world, and saves nothing. Never run its
functions in a file someone has open: save a WORK copy and pass that with --blend.

Writes <out>/<view>.png for six square orthographic views, named by where the camera stands in Blender's
axes: 3q_front_left (-X -Y, above), 3q_back_right (+X +Y, above), front (-Y), side (-X), top (+Z)
and 3q_below (-X -Y, below: undersides hide holes). "front" is Blender's -Y and nothing more: an
FBX's facing in game follows its kit's frame rule, so judge a direction from the axes, not the
picture. Shown: the visible visual LOD0 meshes only (no LODs, no bo_ bodies); --only keeps the
named metameshes; --bodies shows the collision bodies instead.

Read every PNG before giving a verdict. The numbers come from tools/blender/mesh_preflight.py;
a textured preview from tools/blender/render_painter_preview.py; wireframes from the live viewport.
"""
import argparse
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
from blender_job import flag_value, import_fbx, job, report_path, script_args  # noqa: E402
from mesh_preflight import classify_name, visible_meshes  # noqa: E402  (bpy is imported lazily there)

DISTANCE = 3.0  # camera distance from the centre, in longest box sides
MARGIN = 1.1  # orthographic frame over the box diagonal
HOLE_COLOR = (1.0, 0.0, 0.8)  # the world behind the mesh: what a hole shows
MESH_COLOR = (0.62, 0.6, 0.56)


def _unit(x, y, z):
    n = math.sqrt(x * x + y * y + z * z)
    return (x / n, y / n, z / n)


# (name, direction from the centre of the mesh to the camera)
VIEWS = [("3q_front_left", _unit(-1, -1, 0.6)), ("3q_back_right", _unit(1, 1, 0.6)),
         ("front", (0.0, -1.0, 0.0)), ("side", (-1.0, 0.0, 0.0)),
         ("top", _unit(0, -0.001, 1)),  # a hair off vertical, so the camera's up axis is defined
         ("3q_below", _unit(-1, -1, -0.7))]


def camera_frame(lo, hi, direction):
    """(camera location, orthographic scale, clip end) for a box seen from `direction`."""
    size = [hi[k] - lo[k] for k in range(3)]
    span = max(size) or 1.0
    diagonal = math.sqrt(sum(s * s for s in size)) or 1.0
    reach = span * DISTANCE
    loc = tuple((lo[k] + hi[k]) / 2 + direction[k] * reach for k in range(3))
    return loc, diagonal * MARGIN, reach + diagonal * 2


def render_size(res):
    """Square. An orthographic scale spans the longer side of the picture, so on a wide picture a
    tall mesh loses its top and bottom; on a square one the box diagonal fits both ways."""
    return res, res


def shown_names(names, only=None, bodies=False):
    """The object names a view shows, in the given order."""
    want = "bo" if bodies else "base"
    keep = {n.lower() for n in only} if only else None
    out = []
    for name in names:
        c = classify_name(name)
        if c["kind"] == want and (keep is None or c["metamesh"].lower() in keep):
            out.append(name)
    return out


def parse_args(argv):
    p = argparse.ArgumentParser(allow_abbrev=False)
    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument("--fbx")
    src.add_argument("--blend")
    p.add_argument("--out", required=True)
    p.add_argument("--res", type=int, default=960, help="side of the square picture, in pixels")
    p.add_argument("--only", action="append", default=[], help="a metamesh to show (repeatable)")
    p.add_argument("--bodies", action="store_true", help="show the bo_ collision bodies instead")
    return p.parse_args(argv)


# --------------------------------------------------------------- Blender side

def _setup(scene, res):
    import bpy

    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "SINGLE"
    shading.single_color = MESH_COLOR
    shading.show_backface_culling = True
    shading.show_cavity = True
    shading.show_shadows = False
    scene.render.resolution_x, scene.render.resolution_y = render_size(res)
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("qa_world")
    world.color = HOLE_COLOR
    scene.world = world
    camera = bpy.data.objects.new("qa_cam", bpy.data.cameras.new("qa_cam"))
    camera.data.type = "ORTHO"
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def render_views(scene, objs, out_dir, res):
    """Render VIEWS of `objs` (every other mesh hidden) and return the report rows."""
    import bpy
    from mathutils import Vector

    shown = {o.name for o in objs}
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o.name not in shown
    bpy.context.view_layer.update()
    corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = [min(c[k] for c in corners) for k in range(3)]
    hi = [max(c[k] for c in corners) for k in range(3)]
    centre = Vector([(lo[k] + hi[k]) / 2 for k in range(3)])
    camera = _setup(scene, res)
    rows = []
    for name, direction in VIEWS:
        loc, scale, clip_end = camera_frame(lo, hi, direction)
        camera.location = Vector(loc)
        camera.rotation_euler = (centre - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        camera.data.ortho_scale = scale
        camera.data.clip_start, camera.data.clip_end = clip_end * 1e-4, clip_end
        path = os.path.join(out_dir, name + ".png")
        if os.path.exists(path):
            os.remove(path)  # a picture from an earlier run must not pass for this one
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        if not os.path.exists(path):
            raise RuntimeError(f"the render wrote no file: {path}")
        rows.append({"view": name, "path": path})
    return {"views": rows, "bbox": [[round(v, 4) for v in lo], [round(v, 4) for v in hi]]}


def main():
    args = script_args()
    out = flag_value(args, "--out") or "."  # before argparse, so a bad argument still gets a fresh report
    with job(report_path(out)) as report:
        a = parse_args(args)
        import bpy

        if a.blend:
            bpy.ops.wm.open_mainfile(filepath=a.blend)
        else:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            import_fbx(a.fbx)
        scene = bpy.context.scene
        meshes, skipped = visible_meshes()
        names = shown_names([o.name for o in meshes], a.only, a.bodies)
        report.update({"src": a.blend or a.fbx, "shown": names, "skipped_hidden": skipped,
                       "hidden": sorted(o.name for o in meshes if o.name not in names)})
        if not names:
            raise RuntimeError("nothing to show: no mesh matches (see 'hidden')")
        os.makedirs(a.out, exist_ok=True)
        report.update(render_views(scene, [o for o in meshes if o.name in names], a.out, a.res))


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
