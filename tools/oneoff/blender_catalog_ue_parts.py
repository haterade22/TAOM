"""Catalog raw Unreal FBX exports (centimetres, UCX_ collision hulls) as measured kit parts.

One Blender run reads a list file (one FBX path per line) and writes, under <out>:
    kit_parts.json         one entry per FBX, rewritten after every file (resume key: "name")
    thumbs/<name>.png      512 px three-quarter Workbench render, a 1.8 m box beside the part
    _report/log.txt        one line per file, so a crash shows where it stopped
    _report/DONE.txt       blender_job report ("ok" or "error" with the trace)

A restart skips every FBX that already has an entry; failed files are retried.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_catalog_ue_parts.py -- <list.txt> <out_dir>

Sizes are metres in the Blender import frame (the engine reads a Gondor kit FBX turned 180 degrees
about Z from this frame). Metres = Blender units * scene unit scale_length, taken after import.
Read-only on every source file.
"""
import json
import math
import os
import sys
import time
import traceback

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender"))
from blender_job import import_fbx, job, report_path, script_args  # noqa: E402

THUMB_PX = 512
SCALE_BOX_H = 1.8
SCALE_BOX_W = 0.25


def log(out, msg):
    os.makedirs(os.path.join(out, "_report"), exist_ok=True)
    with open(os.path.join(out, "_report", "log.txt"), "a", encoding="utf-8") as f:
        f.write(f"{time.strftime('%H:%M:%S')} {msg}\n")


def world_bounds(objs):
    """Min and max corner over `objs` in world Blender units."""
    lo = Vector((1e30, 1e30, 1e30))
    hi = Vector((-1e30, -1e30, -1e30))
    for o in objs:
        mw = o.matrix_world
        for c in o.bound_box:
            w = mw @ Vector(c)
            for i in range(3):
                lo[i] = min(lo[i], w[i])
                hi[i] = max(hi[i], w[i])
    return lo, hi


def frac(origin, lo, hi):
    return [round((origin[i] - lo[i]) / (hi[i] - lo[i]), 4) if hi[i] - lo[i] > 1e-9 else None
            for i in range(3)]


def uv_outside(mesh):
    """Per UV layer: [name, outside 0..1 flag, min u, min v, max u, max v]."""
    import numpy as np
    rows = []
    n = len(mesh.loops)
    for layer in mesh.uv_layers:
        arr = np.zeros(n * 2, dtype=np.float32)
        try:
            layer.uv.foreach_get("vector", arr)
        except Exception:
            layer.data.foreach_get("uv", arr)
        a = arr.reshape(-1, 2) if n else np.zeros((0, 2), dtype=np.float32)
        if n == 0:
            rows.append([layer.name, False, None, None, None, None])
            continue
        mn, mx = a.min(axis=0), a.max(axis=0)
        out = bool((mn < -1e-4).any() or (mx > 1.0001).any())
        rows.append([layer.name, out, round(float(mn[0]), 4), round(float(mn[1]), 4),
                     round(float(mx[0]), 4), round(float(mx[1]), 4)])
    return rows


def render_thumb(visual, lo, hi, scale_m, png):
    """Workbench flat-grey three-quarter render with a 1.8 m box beside the part."""
    scene = bpy.context.scene
    box_h = SCALE_BOX_H / scale_m
    box_w = SCALE_BOX_W / scale_m
    gap = max(0.4 / scale_m, 0.02 * (hi.x - lo.x))
    x0 = hi.x + gap
    y0 = lo.y
    z0 = lo.z
    verts = [(x0 + dx, y0 + dy, z0 + dz)
             for dx in (0, box_w) for dy in (0, box_w) for dz in (0, box_h)]
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    me = bpy.data.meshes.new("scalebox")
    me.from_pydata(verts, [], faces)
    box = bpy.data.objects.new("scalebox", me)
    box.color = (0.15, 0.15, 0.17, 1.0)
    scene.collection.objects.link(box)
    for o in visual:
        o.color = (0.55, 0.55, 0.55, 1.0)
        for slot in o.material_slots:
            if slot.material:
                slot.material.use_backface_culling = True

    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = scene.render.resolution_y = THUMB_PX
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "Standard"
    sh = scene.display.shading
    sh.light = "FLAT"
    sh.color_type = "OBJECT"
    sh.show_backface_culling = True
    sh.show_cavity = True
    sh.show_object_outline = True
    world = bpy.data.worlds.new("w")
    world.color = (0.82, 0.84, 0.86)
    scene.world = world

    all_lo = Vector((min(lo.x, x0), lo.y, lo.z))
    all_hi = Vector((x0 + box_w, max(hi.y, y0 + box_w), max(hi.z, z0 + box_h)))
    center = (all_lo + all_hi) / 2
    radius = max((all_hi - all_lo).length / 2, 0.5 / scale_m)
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = radius * 2.15
    cam_data.clip_start = 0.001
    cam_data.clip_end = radius * 20
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    direction = Vector((0.75, -1.0, 0.7)).normalized()
    cam.location = center + direction * radius * 6
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = png
    bpy.ops.render.render(write_still=True)


def catalog_one(path, out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    meshes = import_fbx(path)
    bpy.context.view_layer.update()
    scale_m = scene.unit_settings.scale_length
    visual = sorted((o for o in meshes if not o.name.startswith("UCX_")), key=lambda o: o.name)
    hulls = [o for o in meshes if o.name.startswith("UCX_")]
    entry = {
        "name": os.path.splitext(os.path.basename(path))[0],
        "src": path,
        "scene_unit_scale_length": scale_m,
        "scene_unit_system": scene.unit_settings.system,
        "object_scales": sorted({tuple(round(v, 6) for v in o.matrix_world.to_scale()) for o in meshes}),
        "ucx_hulls": len(hulls),
        "visual_count": len(visual),
        "other_object_types": sorted({o.type for o in scene.objects if o.type != "MESH"}),
        "meshes": [],
    }
    entry["object_scales"] = [list(s) for s in entry["object_scales"]]
    if not visual:
        entry["error"] = "no visual mesh"
        return entry
    lo, hi = world_bounds(visual)
    entry["size_m"] = [round((hi[i] - lo[i]) * scale_m, 4) for i in range(3)]
    entry["bounds_min_m"] = [round(lo[i] * scale_m, 4) for i in range(3)]
    entry["bounds_max_m"] = [round(hi[i] * scale_m, 4) for i in range(3)]
    entry["pivot_frac"] = frac((0.0, 0.0, 0.0), lo, hi)  # world origin inside the part bounds
    entry["tris"] = 0
    entry["materials"] = []
    entry["uv_layer_count_max"] = 0
    entry["uv_outside_0_1"] = False
    for o in visual:
        me = o.data
        me.calc_loop_triangles()
        mlo, mhi = world_bounds([o])
        uvs = uv_outside(me)
        mats = [(s.material.name if s.material else "") for s in o.material_slots]
        entry["meshes"].append({
            "object": o.name,
            "size_m": [round((mhi[i] - mlo[i]) * scale_m, 4) for i in range(3)],
            "bounds_min_m": [round(mlo[i] * scale_m, 4) for i in range(3)],
            "bounds_max_m": [round(mhi[i] * scale_m, 4) for i in range(3)],
            "pivot_frac": frac(o.matrix_world.translation, mlo, mhi),
            "tris": len(me.loop_triangles),
            "material_slots": mats,
            "uv_layers": uvs,
            "uv_layer_count": len(me.uv_layers),
            "uv_outside_0_1": any(r[1] for r in uvs),
            "object_scale": [round(v, 6) for v in o.matrix_world.to_scale()],
        })
        entry["tris"] += len(me.loop_triangles)
        entry["uv_layer_count_max"] = max(entry["uv_layer_count_max"], len(me.uv_layers))
        entry["uv_outside_0_1"] = entry["uv_outside_0_1"] or any(r[1] for r in uvs)
        for m in mats:
            if m not in entry["materials"]:
                entry["materials"].append(m)
    for h in hulls:  # hulls are counted, then dropped so they never show in the render
        bpy.data.objects.remove(h, do_unlink=True)
    png = os.path.join(out, "thumbs", entry["name"] + ".png")
    os.makedirs(os.path.dirname(png), exist_ok=True)
    try:
        render_thumb(visual, lo, hi, scale_m, png)
        entry["thumb"] = os.path.relpath(png, out).replace("\\", "/")
    except Exception:
        entry["thumb_error"] = traceback.format_exc()
    return entry


def main():
    args = script_args()
    list_file, out = args[0], args[1]
    with job(report_path(out)) as result:
        with open(list_file, encoding="utf-8") as f:
            paths = [ln.strip() for ln in f if ln.strip()]
        names = [os.path.splitext(os.path.basename(p))[0] for p in paths]
        if len(set(names)) != len(names):
            raise RuntimeError("duplicate FBX stems in the list: thumbnails would collide")
        os.makedirs(out, exist_ok=True)
        jpath = os.path.join(out, "kit_parts.json")
        done = {}
        if os.path.exists(jpath):
            with open(jpath, encoding="utf-8") as f:
                done = {e["name"]: e for e in json.load(f).get("parts", []) if "error" not in e}
        failed = {}
        for p, name in zip(paths, names):
            if name in done:
                log(out, f"skip {name}")
                continue
            log(out, f"start {name}")
            try:
                done[name] = catalog_one(p, out)
                log(out, f"ok {name} tris={done[name].get('tris')} hulls={done[name]['ucx_hulls']}")
            except Exception:
                failed[name] = traceback.format_exc()
                log(out, f"FAIL {name}: {failed[name].splitlines()[-1]}")
            ordered = [done[n] for n in names if n in done]
            with open(jpath, "w", encoding="utf-8") as f:
                json.dump({"frame": "Blender import frame, metres", "parts": ordered}, f, indent=1)
        result["listed"] = len(paths)
        result["cataloged"] = len(done)
        result["failed"] = failed
        result["blender"] = bpy.app.version_string


if __name__ == "__main__":
    main()
