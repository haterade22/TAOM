"""
Reverse-engineer a shipped Gondor building into a parts recipe, then rebuild
it FROM that recipe and render both side by side — the proof that the
building-composition grammar is recoverable (buildings-from-kit-parts pilot,
step 2; catalog from blender_catalog_parts.py is the parts database).

Per building sub-object: dump its world transform and mesh signature
(tri count + local dims + material set), match against parts_catalog.json
(same signature ⇒ same source part), emit recipe JSON
[(family, part name, matrix)], then import the matched parts fresh from
their family FBXs, place them per the recipe 60 m beside the original, and
render the pair from the same angle.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_rebuild_building.py

Output: E:\\LOTRAOMAssets\\_export\\lond_cirion\\rebuild\\
    recipe_<building>.json · compare_<building>.png · _report\\DONE.txt
"""

import json
import math
import os
import traceback

import bpy
from mathutils import Matrix, Vector

GONDOR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\Gondor"
BUILDING = os.path.join(GONDOR, "buildings", "gondor_building_small_a_01.fbx")
CATALOG = r"E:\LOTRAOMAssets\_export\lond_cirion\parts_catalog\parts_catalog.json"
OUT = r"E:\LOTRAOMAssets\_export\lond_cirion\rebuild"
OFFSET = 60.0  # rebuilt copy sits this far +X of the original

LOG_LINES = []
REPORT_DIR = None


def log(msg):
    LOG_LINES.append(str(msg))
    print(msg)
    if REPORT_DIR:
        with open(os.path.join(REPORT_DIR, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG_LINES))


def signature(obj):
    obj.data.calc_loop_triangles()
    d = sorted(round(v, 1) for v in obj.dimensions)
    return (len(obj.data.loop_triangles), d[0], d[1], d[2])


def main():
    global REPORT_DIR
    os.makedirs(OUT, exist_ok=True)
    REPORT_DIR = os.path.join(OUT, "_report")
    os.makedirs(REPORT_DIR, exist_ok=True)
    done = os.path.join(REPORT_DIR, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)

    summary = {"status": "error"}
    try:
        catalog = json.load(open(CATALOG, encoding="utf-8"))
        by_sig = {}
        for p in catalog:
            d = sorted(p["dims"])
            key = (p["tris"], round(d[0], 1), round(d[1], 1), round(d[2], 1))
            by_sig.setdefault(key, []).append(p)

        bname = os.path.splitext(os.path.basename(BUILDING))[0]
        bpy.ops.wm.read_factory_settings(use_empty=True)
        try:
            bpy.ops.wm.fbx_import(filepath=BUILDING)
        except Exception:
            bpy.ops.import_scene.fbx(filepath=BUILDING)
        bpy.context.view_layer.update()
        originals = [o for o in bpy.context.scene.objects if o.type == "MESH"]

        recipe = []
        unmatched = []
        for o in originals:
            key = signature(o)
            cands = by_sig.get(key, [])
            if len(cands) == 1 or (cands and all(
                    c["family"] == cands[0]["family"] and c["name"] == cands[0]["name"]
                    for c in cands)):
                p = cands[0]
                recipe.append({"family": p["family"], "part": p["name"],
                               "idx": p["idx"],
                               "matrix": [list(r) for r in o.matrix_world]})
            elif cands:
                # ambiguous signature: same geometry duplicated across the
                # sheet — any candidate is geometrically identical, take the
                # first and note it
                p = cands[0]
                recipe.append({"family": p["family"], "part": p["name"],
                               "idx": p["idx"], "ambiguous": len(cands),
                               "matrix": [list(r) for r in o.matrix_world]})
            else:
                unmatched.append({"name": o.name, "sig": list(key),
                                  "mats": [m.name for m in o.data.materials if m]})
        log(f"[rebuild] {bname}: {len(originals)} sub-objects -> "
            f"{len(recipe)} matched, {len(unmatched)} unmatched")
        for u in unmatched:
            log(f"[rebuild]   unmatched: {u['name']} sig={u['sig']} mats={u['mats'][:2]}")

        with open(os.path.join(OUT, f"recipe_{bname}.json"), "w", encoding="utf-8") as f:
            json.dump({"building": bname, "recipe": recipe,
                       "unmatched": unmatched}, f, indent=1)

        # rebuild: import each needed family once, duplicate matched parts
        needed = {r["family"] for r in recipe}
        family_objs = {}
        for fam in needed:
            before = set(bpy.data.objects)
            path = os.path.join(GONDOR, "meshes", fam + ".fbx")
            try:
                bpy.ops.wm.fbx_import(filepath=path)
            except Exception:
                bpy.ops.import_scene.fbx(filepath=path)
            new = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
            for o in new:
                o.hide_render = True
            family_objs[fam] = {o.name.split(".")[0] if False else o.name: o
                                for o in new}
        bpy.context.view_layer.update()

        rebuilt = []
        misses = 0
        for r in recipe:
            src = family_objs[r["family"]].get(r["part"])
            if src is None:
                misses += 1
                continue
            dup = src.copy()
            dup.data = src.data.copy()
            M = Matrix([r["matrix"][i] for i in range(4)])
            dup.matrix_world = Matrix.Translation((OFFSET, 0, 0)) @ M
            dup.hide_render = False
            bpy.context.scene.collection.objects.link(dup)
            rebuilt.append(dup)
        log(f"[rebuild] placed {len(rebuilt)} parts ({misses} name misses)")

        # side-by-side render
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 48
        sun_data = bpy.data.lights.new("sun", type="SUN")
        sun_data.energy = 3.5
        sun = bpy.data.objects.new("sun", sun_data)
        scene.collection.objects.link(sun)
        sun.rotation_euler = (math.radians(50), 0.0, math.radians(-35))
        world = bpy.data.worlds.new("w")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.35, 0.38, 1.0)
        scene.world = world
        scene.render.resolution_x = 1800
        scene.render.resolution_y = 900
        cam_data = bpy.data.cameras.new("cam")
        cam = bpy.data.objects.new("cam", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        centre = Vector((OFFSET / 2, 0.0, 6.0))
        cam.location = centre + Vector((10.0, -85.0, 40.0))
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"compare_{bname}.png")
        bpy.ops.render.render(write_still=True)
        log(f"[rebuild] rendered compare_{bname}.png (original left, rebuilt right)")

        summary = {"status": "ok", "building": bname,
                   "sub_objects": len(originals), "matched": len(recipe),
                   "unmatched": len(unmatched), "placed": len(rebuilt)}
    except Exception:
        log(traceback.format_exc())
        summary["trace"] = traceback.format_exc(limit=3)

    with open(done, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[done] {summary['status']}")


main()
