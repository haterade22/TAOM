"""
Reconstruct an assembled building as ONE clean mesh by stamping the MODULAR
kit meshes (proper connectivity, sane budgets, final t_ materials) at the
instance transforms found in the UE level export — instead of decimating the
level's Nanite triangle soup (which melts walls/floors into 'paper airplanes';
see rivendell-kit memory).

Per level FBX:
  1. import level, match every instance to a modular mesh (same matcher as
     blender_dump_level_placements), record matrix_world per instance
  2. carry UNMATCHED structural objects (floor, vaulted ceilings, merged
     groups) as world-baked residual geometry; drop FX (flames), sky, LODs
  3. stamp modular sm_ meshes at all transforms -> join -> sm_rivendell_bld_<X>
     stamp their bo_ twins at the same transforms -> join -> real piecewise
     collision (physics material 'stone')
  4. export <out>/sm_rivendell_bld_<X>.fbx; decimate only if > --max-tris
     (default 1.2M — clean geometry, engine auto-LODs downstream)

    blender-launcher.exe -b -P tools/oneoff/blender_reconstruct_buildings.py -- ^
        --level "<levels>/L_Assembly_Level_Elvish_House.fbx" ^
        --modular "<AssetSources>/Scenes/Rivendell" ^
        --material-map "E:/LOTRAOMAssets/_export/rivendell/material_rename_map.json" ^
        --out "<AssetSources>/Scenes/Rivendell/assembled"

Detached launcher: completion = <out>/_reconstruct_<name>.json (log inside).
"""

import argparse
import json
import math
import os
import re
import sys

import bpy
from mathutils import Matrix

KIT = "rivendell"
LOG = []
MATERIAL_MAP = {}


def log(msg):
    LOG.append(msg)
    print(msg)


def sanitize(name):
    name = re.sub(r"\.\d{3}$", "", name)
    name = re.sub(r"^(SM_|sm_)", "", name, flags=re.IGNORECASE)
    return re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_").lower()


def load_known(meshlist_dir):
    known, stripped_known = {}, {}
    for f in os.listdir(meshlist_dir):
        if f.endswith(".txt"):
            for line in open(os.path.join(meshlist_dir, f), encoding="utf-8"):
                n = line.strip()
                if n.startswith(f"sm_{KIT}_"):
                    k = n.replace("_", "")
                    known[k] = n
                    stripped_known.setdefault(re.sub(r"\d+$", "", k), n)
    return known, stripped_known


def _try_key(base, known, stripped_known):
    key = f"sm{KIT}{base}".replace("_", "")
    while True:
        if key in known:
            return known[key]
        if key in stripped_known:
            return stripped_known[key]
        s = re.sub(r"\d+$", "", key)
        if s == key or len(s) <= len(f"sm{KIT}"):
            return None
        key = s


def match_mesh(obj_name, known, stripped_known):
    base = sanitize(obj_name)
    base = re.sub(r"_?staticmeshcomponent\d*$", "", base)
    base = re.sub(r"^sm_", "", base)
    hit = _try_key(base, known, stripped_known)
    if hit:
        return hit
    tokens = base.split("_")
    for skip in range(1, min(4, len(tokens))):
        hit = _try_key("_".join(tokens[skip:]), known, stripped_known)
        if hit:
            return hit
    return None


def rename_materials(objs):
    for obj in objs:
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None:
                continue
            base = re.sub(r"^(M_|MI_|m_|mi_)", "", mat.name)
            new = f"m_{KIT}_{sanitize(base)}"
            if new in MATERIAL_MAP:
                new = MATERIAL_MAP[new]
            else:
                stripped = re.sub(r"_\d+$", "", new)
                new = MATERIAL_MAP.get(stripped, "t_" + new[2:])
            existing = bpy.data.materials.get(new)
            if existing is not None and existing is not mat:
                slot.material = existing
            else:
                mat.name = new


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def decimate_to(obj, target):
    tris = tri_count(obj)
    if tris <= target:
        return tris
    select_only([obj])
    wmod = obj.modifiers.new("w", "WELD")
    wmod.merge_threshold = 0.001
    bpy.ops.object.modifier_apply(modifier=wmod.name)
    tris = tri_count(obj)
    if tris > target:
        mod = obj.modifiers.new("d", "DECIMATE")
        mod.ratio = target / tris
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return tri_count(obj)


RESIDUAL_DROP = ("flame", "skysphere", "fog", "god_ray", "godray")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--level", required=True)
    ap.add_argument("--modular", required=True, help="AssetSources/Scenes/Rivendell root")
    ap.add_argument("--material-map", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--max-tris", type=int, default=1_200_000)
    ap.add_argument("--collision-tris", type=int, default=60_000)
    args = ap.parse_args(argv)

    with open(args.material_map, encoding="utf-8") as f:
        MATERIAL_MAP.update(json.load(f))

    base = sanitize(os.path.splitext(os.path.basename(args.level))[0])
    base = re.sub(r"^l_assembly_level_elvish_", "", base)
    sm_name = f"sm_{KIT}_bld_{base}"
    known, stripped_known = load_known(os.path.join(args.modular, "_meshlists"))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=args.level)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=args.level)
    bpy.context.view_layer.update()

    placements = []   # (modular_name, matrix)
    residual = []
    dropped = 0
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        n = o.name.lower()
        if (n.startswith(("ucx", "bo_")) or re.search(r"_lod[1-9]\d*(\.\d{3})?$", n)
                or any(t in n for t in RESIDUAL_DROP)):
            dropped += 1
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        target = match_mesh(o.name, known, stripped_known)
        if target:
            placements.append((target, o.matrix_world.copy()))
            bpy.data.objects.remove(o, do_unlink=True)
        else:
            residual.append(o)
    log(f"[reconstruct] {len(placements)} placements, {len(residual)} residual, {dropped} dropped")

    # bake residuals to world space (single-user, mirror-safe)
    for o in residual:
        if o.data.users > 1:
            o.data = o.data.copy()
        mw = o.matrix_world.copy()
        o.data.transform(mw)
        if mw.determinant() < 0:
            o.data.flip_normals()
        o.matrix_world = Matrix.Identity(4)
    rename_materials(residual)

    # import templates once per unique modular mesh
    fbx_index = {}
    for root, _d, files in os.walk(args.modular):
        if "_normalize_report" in root or "assembled" in root:
            continue
        for f in files:
            if f.endswith(".fbx"):
                fbx_index[f[:-4]] = os.path.join(root, f)

    templates = {}   # modular_name -> (sm_data, bo_data or None)
    missing_tpl = set()
    for name in {p[0] for p in placements}:
        path = fbx_index.get(name)
        if not path:
            missing_tpl.add(name)
            continue
        before = set(bpy.context.scene.objects)
        bpy.ops.import_scene.fbx(filepath=path)
        sm_d = bo_d = None
        imported = [o for o in set(bpy.context.scene.objects) - before if o.type == "MESH"]
        for o in imported:
            if o.name.lower().startswith("bo_"):
                bo_d = o.data
            else:
                sm_d = o.data
        for o in imported:
            bpy.data.objects.remove(o, do_unlink=True)  # keep data, drop objects
        if sm_d:
            templates[name] = (sm_d, bo_d)
        else:
            missing_tpl.add(name)
    if missing_tpl:
        log(f"[reconstruct] WARNING no template FBX for: {sorted(missing_tpl)[:8]}")

    def stamp(data_getter, mirror_flip):
        objs = []
        for name, mw in placements:
            tpl = templates.get(name)
            data = data_getter(tpl) if tpl else None
            if data is None:
                continue
            d = data.copy()
            d.transform(mw)
            if mirror_flip and mw.determinant() < 0:
                d.flip_normals()
            o = bpy.data.objects.new("stamp", d)
            bpy.context.scene.collection.objects.link(o)
            objs.append(o)
        return objs

    visual_objs = stamp(lambda t: t[0], True) + residual
    select_only(visual_objs)
    if len(visual_objs) > 1:
        bpy.ops.object.join()
    building = bpy.context.view_layer.objects.active
    building.name = building.data.name = sm_name
    tris_before = tri_count(building)
    tris_after = decimate_to(building, args.max_tris)

    bo_objs = stamp(lambda t: t[1], False)
    bo_name = f"bo_{sm_name}"
    if bo_objs:
        select_only(bo_objs)
        if len(bo_objs) > 1:
            bpy.ops.object.join()
        bo = bpy.context.view_layer.objects.active
        bo.name = bo.data.name = bo_name
        bo.data.materials.clear()
        pm = bpy.data.materials.get("stone") or bpy.data.materials.new("stone")
        bo.data.materials.append(pm)
        decimate_to(bo, args.collision_tris)
        out_objs = [building, bo]
    else:
        out_objs = [building]

    os.makedirs(args.out, exist_ok=True)
    out_path = os.path.join(args.out, sm_name + ".fbx")
    select_only(out_objs)
    bpy.ops.export_scene.fbx(filepath=out_path, use_selection=True,
                             object_types={"MESH"}, use_mesh_modifiers=True,
                             bake_space_transform=True, add_leaf_bones=False,
                             path_mode="AUTO")
    asm_dir = os.path.join(args.modular, "_meshlists_assembled")
    os.makedirs(asm_dir, exist_ok=True)
    with open(os.path.join(asm_dir, sm_name + ".txt"), "w", encoding="utf-8") as f:
        f.write(f"{sm_name}\n{bo_name if bo_objs else ''}".strip())

    collisionless = sorted(n for n, t in templates.items() if t[1] is None)
    summary = {"sm": sm_name, "placements": len(placements), "residual": len(residual),
               "missing_templates": sorted(missing_tpl),
               "templates_without_collision": collisionless,
               "tris": {"before": tris_before, "after": tris_after},
               "out": out_path, "log": LOG}
    with open(os.path.join(args.out, f"_reconstruct_{base}.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[reconstruct] DONE {sm_name}: {tris_after} tris")


main()
