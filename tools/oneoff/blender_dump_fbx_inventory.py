"""
Dump the object-level structure of FBX files to JSON — names, world
transforms, dimensions, tri counts, materials. Used to reverse-engineer how
composed kit pieces (e.g. the Minas Tirith blockout walls) are assembled so
new city walls can be built from the same parts programmatically.

Headless (MS-Store Blender launcher, DETACHES — completion protocol:
<out>\\DONE.txt, log.txt beside it):

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_dump_fbx_inventory.py -- ^
        --files "path1.fbx;path2.fbx;..." --out E:\\staging\\inventory
"""

import argparse
import json
import math
import os
import sys
import traceback

import bpy

LOG = []


def log(msg, out_dir=None):
    LOG.append(str(msg))
    print(msg)
    if out_dir:
        with open(os.path.join(out_dir, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG))


def dump_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=path)
    bpy.context.view_layer.update()
    objs = []
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            objs.append({"name": o.name, "type": o.type})
            continue
        o.data.calc_loop_triangles()
        mw = o.matrix_world
        loc = mw.to_translation()
        rot = mw.to_euler("XYZ")
        scl = mw.to_scale()
        from mathutils import Vector
        bb = [mw @ Vector(c) for c in o.bound_box]
        objs.append({
            "name": o.name,
            "mesh": o.data.name,
            "tris": len(o.data.loop_triangles),
            "loc": [round(v, 3) for v in loc],
            "rot_deg": [round(math.degrees(a), 2) for a in rot],
            "scale": [round(v, 4) for v in scl],
            "dims": [round(v, 3) for v in o.dimensions],
            "bbox_min": [round(min(v[k] for v in bb), 3) for k in range(3)],
            "bbox_max": [round(max(v[k] for v in bb), 3) for k in range(3)],
            "materials": [m.name for m in o.data.materials if m],
            "uv_layers": len(o.data.uv_layers),
        })
    return objs


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--files", required=True, help="semicolon-separated FBX paths")
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)

    os.makedirs(args.out, exist_ok=True)
    done = os.path.join(args.out, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)

    result = {}
    status = "ok"
    for path in (p for p in args.files.split(";") if p.strip()):
        key = os.path.splitext(os.path.basename(path))[0]
        try:
            objs = dump_fbx(path)
            result[key] = {"objects": objs, "count": len(objs)}
            log(f"[dump] {key}: {len(objs)} objects", args.out)
        except Exception:
            result[key] = {"error": traceback.format_exc(limit=2)}
            status = "partial"
            log(traceback.format_exc(), args.out)

    with open(os.path.join(args.out, "inventory.json"), "w", encoding="utf-8") as f:
        json.dump(result, f, indent=1)
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status, "files": len(result)}, f)
    log(f"[done] {status}", args.out)


main()
