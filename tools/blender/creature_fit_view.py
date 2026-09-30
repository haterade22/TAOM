"""Open a creature_fit scene in the Blender window and play it: solid shading in object colours, through the scene
camera. Blender runs this after it loads the .blend:

    blender-launcher.exe <scene.blend> -P tools/blender/creature_fit_view.py

Space stops or starts playback; the mouse orbits as usual.
"""
import bpy


def _play():
    wm = bpy.context.window_manager
    for win in wm.windows:
        for area in win.screen.areas:
            if area.type != "VIEW_3D":
                continue
            for sp in area.spaces:
                if sp.type == "VIEW_3D":
                    sp.shading.type = "SOLID"
                    sp.shading.color_type = "OBJECT"
                    sp.shading.show_shadows = True
                    sp.shading.show_cavity = True
                    sp.overlay.show_relationship_lines = False
                    sp.region_3d.view_perspective = "CAMERA"
            with bpy.context.temp_override(window=win, area=area):
                if not bpy.context.screen.is_animation_playing:
                    bpy.ops.screen.animation_play()
            return None
    return 0.5          # the window is not up yet: try again


bpy.app.timers.register(_play, first_interval=1.0)
