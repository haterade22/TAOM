"""Dump the elephant work scene's rig for tools/elephant_gait_retarget.py (#1474): every armature's rest bones
(name, parent, head, tail, matrix_local), the objects and the actions. Read-only: never saves the scene.

Headless (the Store launcher DETACHES: completion is the --out file):
    blender-launcher.exe -b <elephant work .blend> -P tools/blender/elephant_rig_dump.py -- --out <rig.json>
The work scene's elephant_skeleton equals the engine skeleton's rest frames turned 180 degrees about Z, bone for
bone and in the same order (measured 2026-11-18 against adod_elephant_geo.tpac: 0.00002 worst difference).
"""
import json
import os
import sys
import traceback

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
out = args[args.index("--out") + 1]
rep = {"status": "running"}
try:
    rep["blender"] = bpy.app.version_string
    rep["objects"] = [{"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
                       "loc": list(o.location), "rot": list(o.rotation_euler), "scale": list(o.scale),
                       "dims": list(o.dimensions), "hide": o.hide_viewport or o.hide_get() if o.name in bpy.context.view_layer.objects else None,
                       "mods": [(m.type, getattr(m, "object", None).name if getattr(m, "object", None) else None) for m in o.modifiers],
                       "verts": len(o.data.vertices) if o.type == "MESH" else None,
                       "action": o.animation_data.action.name if o.animation_data and o.animation_data.action else None}
                      for o in bpy.data.objects]
    arms = {}
    for o in bpy.data.objects:
        if o.type == "ARMATURE":
            arms[o.name] = {"world": [list(r) for r in o.matrix_world],
                            "bones": [{"name": b.name, "parent": b.parent.name if b.parent else None,
                                       "head": list(b.head_local), "tail": list(b.tail_local),
                                       "m": [list(r) for r in b.matrix_local]} for b in o.data.bones]}
    rep["armatures"] = arms
    rep["actions"] = [{"name": a.name, "range": list(a.frame_range), "users": a.users} for a in bpy.data.actions]
    rep["scene"] = {"fps": bpy.context.scene.render.fps, "frame": bpy.context.scene.frame_current}
    rep["status"] = "ok"
except BaseException:
    rep["status"] = "error"
    rep["trace"] = traceback.format_exc()
finally:
    os.makedirs(os.path.dirname(out), exist_ok=True)
    json.dump(rep, open(out, "w"), indent=1, default=str)
