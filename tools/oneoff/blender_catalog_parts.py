"""
Catalog the Gondor building-part family sheets: every mesh object in each
family FBX gets a global index, a grid slot on a numbered CONTACT SHEET
render, and a JSON row (family, mesh name, dims, tris). The sheets let a
human point at parts ("roof 12 on walls 3+3") the way the Lond Cirion wall
recipes worked; the JSON is the machine side for composition scripts.

Output: E:\\LOTRAOMAssets\\_export\\lond_cirion\\parts_catalog\\
    parts_catalog.json
    <family>_p<page>.png     (numbered 10x10 grids, 3/4 view)
    _report\\DONE.txt + log.txt (MS-Store launcher detaches; log(), not print)

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_catalog_parts.py
"""

import json
import math
import os
import traceback

import bpy
from mathutils import Vector

GONDOR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\Gondor"
OUT = r"E:\LOTRAOMAssets\_export\lond_cirion\parts_catalog"

FAMILIES = [
    "gondor_roofs_a.fbx", "gondor_roof_circular_full_6m_b.fbx",
    "gondor_trims_straight_a.fbx", "gondor_trims_curved_a.fbx",
    "gondor_wall_3m_straight_a.fbx", "gondor_wall_3m_curved_a.fbx",
    "gondor_wall_6m_straight_a.fbx", "gondor_wall_6m_curved_a.fbx",
    "gondor_pillars_a.fbx", "gondor_railing_a1.fbx",
    "gondor_stairs_12m_straight_a.fbx", "gondor_stairs_3m_straight_a.fbx",
    "gondor_stairs_3m_curved_a.fbx", "gondor_buttres_a.fbx",
    "gondor_brick.fbx",
]
PAGE = 100  # parts per contact sheet (10x10)

LOG_LINES = []
REPORT_DIR = None


def log(msg):
    LOG_LINES.append(str(msg))
    print(msg)
    if REPORT_DIR:
        with open(os.path.join(REPORT_DIR, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG_LINES))


def label(text, loc, size):
    curve = bpy.data.curves.new(f"lbl_{text}", type="FONT")
    curve.body = text
    curve.size = size
    obj = bpy.data.objects.new(f"lbl_{text}", curve)
    obj.location = loc
    obj.rotation_euler = (math.radians(60), 0.0, 0.0)  # face the 3/4 camera
    mat = bpy.data.materials.get("_lbl") or bpy.data.materials.new("_lbl")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (0.1, 1.0, 0.2, 1.0)
    em.inputs["Strength"].default_value = 4.0
    nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    obj.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def main():
    global REPORT_DIR
    os.makedirs(OUT, exist_ok=True)
    REPORT_DIR = os.path.join(OUT, "_report")
    os.makedirs(REPORT_DIR, exist_ok=True)
    done = os.path.join(REPORT_DIR, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)

    catalog = []
    status = "ok"
    gidx = 0
    for fname in FAMILIES:
        path = os.path.join(GONDOR, "meshes", fname)
        family = os.path.splitext(fname)[0]
        try:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            try:
                bpy.ops.wm.fbx_import(filepath=path)
            except Exception:
                bpy.ops.import_scene.fbx(filepath=path)
            bpy.context.view_layer.update()
            meshes = sorted((o for o in bpy.context.scene.objects
                             if o.type == "MESH"), key=lambda o: o.name)
            # data-level world bake so grid placement is clean
            from mathutils import Matrix
            for o in meshes:
                if o.data.users > 1:
                    o.data = o.data.copy()
                mw = o.matrix_world.copy()
                o.data.transform(mw)
                if mw.determinant() < 0:
                    o.data.flip_normals()
                o.matrix_world = Matrix.Identity(4)
            bpy.context.view_layer.update()
            cell = max((max(o.dimensions) for o in meshes), default=1.0) + 4.0
            cell = min(cell, 30.0)
            pages = [meshes[i:i + PAGE] for i in range(0, len(meshes), PAGE)]
            log(f"[catalog] {family}: {len(meshes)} parts, cell {cell:.1f}, "
                f"{len(pages)} page(s)")

            scene = bpy.context.scene
            scene.render.engine = "CYCLES"
            scene.cycles.samples = 24
            sun_data = bpy.data.lights.new("sun", type="SUN")
            sun_data.energy = 4.0
            sun_data.angle = 0.8
            sun = bpy.data.objects.new("sun", sun_data)
            scene.collection.objects.link(sun)
            sun.rotation_euler = (math.radians(45), 0.0, math.radians(-30))
            world = bpy.data.worlds.new("w")
            world.use_nodes = True
            world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.35, 0.37, 1.0)
            scene.world = world
            scene.render.resolution_x = 2200
            scene.render.resolution_y = 2200
            cam_data = bpy.data.cameras.new("cam")
            cam_data.type = "ORTHO"
            cam = bpy.data.objects.new("cam", cam_data)
            scene.collection.objects.link(cam)
            scene.camera = cam

            for pi, page in enumerate(pages):
                cols = math.ceil(math.sqrt(len(page)))
                labels = []
                for o in meshes:
                    o.hide_render = True
                for i, o in enumerate(page):
                    r, c = divmod(i, cols)
                    # ground each part at its cell
                    bb = [Vector(v) for v in o.bound_box]
                    zmin = min(v.z for v in bb)
                    cx = (min(v.x for v in bb) + max(v.x for v in bb)) / 2
                    cy = (min(v.y for v in bb) + max(v.y for v in bb)) / 2
                    o.location = (c * cell - cx, -r * cell - cy, -zmin)
                    o.hide_render = False
                    labels.append(label(str(gidx + pi * PAGE + i),
                                        (c * cell - cell * 0.42,
                                         -r * cell + cell * 0.32, 0.2),
                                        cell * 0.22))
                bpy.context.view_layer.update()
                span = cols * cell
                cam_data.ortho_scale = span * 1.15
                centre = Vector((span / 2 - cell / 2, -span / 2 + cell / 2, 0))
                cam.location = centre + Vector((0, -span * 0.55, span * 0.9))
                cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
                scene.render.filepath = os.path.join(OUT, f"{family}_p{pi}.png")
                bpy.ops.render.render(write_still=True)
                for l in labels:
                    bpy.data.objects.remove(l, do_unlink=True)
                log(f"[catalog] rendered {family}_p{pi}.png")

            for i, o in enumerate(meshes):
                o.data.calc_loop_triangles()
                catalog.append({
                    "idx": gidx + i, "family": family, "name": o.name,
                    "dims": [round(v, 3) for v in o.dimensions],
                    "tris": len(o.data.loop_triangles),
                    "page": i // PAGE,
                })
            gidx += len(meshes)
        except Exception:
            log(traceback.format_exc())
            status = "partial"

    with open(os.path.join(OUT, "parts_catalog.json"), "w", encoding="utf-8") as f:
        json.dump(catalog, f, indent=1)
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status, "parts": gidx}, f)
    log(f"[done] {status}: {gidx} parts")


main()
