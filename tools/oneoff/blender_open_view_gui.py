"""Blender with its window: show a saved VIEW file of blender_view_textured.py so that the pieces can be orbited.

Run (no -b):  blender-launcher <VIEW .blend> -P tools/oneoff/blender_open_view_gui.py
A glTF model:  blender-launcher -P tools/oneoff/blender_open_view_gui.py -- --glb <model.glb>

It changes nothing on disk. It sets every 3D viewport to Material Preview and frames the pieces.
The ground plane and other very large helpers are left out of the framing.
"""
import sys

import bpy
from mathutils import Vector

HELPER_SIZE = 400.0  # an object larger than this in any direction is a ground plane, not a piece
_ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "--glb" in _ARGS:  # a model file in place of a saved scene: an empty scene, then the import
    for _ob in list(bpy.data.objects):
        bpy.data.objects.remove(_ob)
    bpy.ops.import_scene.gltf(filepath=_ARGS[_ARGS.index("--glb") + 1])


def piece_box(scene):
    """The world bounding box (low corner, high corner) of the mesh objects that are pieces."""
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    found = False
    for ob in scene.objects:
        if ob.type != "MESH" or max(ob.dimensions) > HELPER_SIZE:
            continue
        for corner in ob.bound_box:
            p = ob.matrix_world @ Vector(corner)
            lo = Vector((min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z)))
            hi = Vector((max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z)))
            found = True
    return (lo, hi) if found else (Vector((0, 0, 0)), Vector((10, 10, 10)))


def show():
    """Runs once, a moment after the window exists."""
    try:
        bpy.context.preferences.view.show_splash = False
    except Exception:
        pass
    scene = bpy.context.scene
    if scene.render.engine == "BLENDER_WORKBENCH":  # Material Preview needs another engine
        names = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
        scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in names else "BLENDER_EEVEE"
    lo, hi = piece_box(scene)
    centre = (lo + hi) * 0.5
    size = max((hi - lo).length, 10.0)
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            for space in area.spaces:
                if space.type != "VIEW_3D":
                    continue
                space.shading.type = "MATERIAL"
                space.clip_end = max(space.clip_end, size * 6.0)
                view = space.region_3d
                view.view_perspective = "PERSP"
                view.view_location = centre
                view.view_distance = size * 0.75
            area.tag_redraw()
    return None


bpy.app.timers.register(show, first_interval=1.5)
