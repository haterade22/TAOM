"""
Normalize UE-exported FBX meshes into Bannerlord Rivendell-kit FBX.

Per mesh: unit/scale fix (UE cm -> metres), lowercase sm_rivendell_* renaming
(Bannerlord lowercases mesh IDs on import — see build_erebor_kitbash.py),
m_rivendell_* material-slot renaming, bo_ collision twin (UCX_ hulls joined
when present, else generated), visual-poly triage, per-FBX mesh-name dumps
for the future kitbash builder, CSV report.

Blender on this machine is the Microsoft Store app — the raw blender.exe is
ACL-blocked, so invoke through the launcher alias. The launcher DETACHES
(no console output): completion is signalled by <dst>\\_normalize_report\\DONE.txt,
progress by log.txt next to it. Verified on Blender 5.2.0 LTS.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P e:\\repos\\TAOM\\tools\\oneoff\\blender_normalize_rivendell.py -- ^
        --src "E:\\LOTRAOMAssets\\_export\\rivendell\\meshes" ^
        --dst "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\AssetSources\\Scenes\\Rivendell" ^
        [--collision auto|decimate|hull|skip] [--collision-tris 4000]
        [--max-tris 150000] [--mode mesh|level] [--only <substring>]

--mode level: one merged UE level export (File > Export All) in, one merged
FBX out (assembled/) — objects renamed/scaled, no collision generation.
"""

import argparse
import csv
import json
import os
import re
import sys
import traceback

import bpy
import bmesh

KIT = "rivendell"

# UE staging subdir -> AssetSources/Scenes/Rivendell subdir. Foliage gets no
# generated collision by default (whole-tree hulls would block movement).
CATEGORY_MAP = {
    "Exterior_Construction": ("buildings", "decimate"),
    "Interior_Construction": ("interior", "decimate"),
    "Outdoor_Assets": ("outdoor", "decimate"),
    "Props": ("props", "decimate"),
    "Foliage": ("foliage", "skip"),
    "UCX": ("props", "decimate"),
}

LOG_LINES = []


def log(msg):
    LOG_LINES.append(msg)
    print(msg)


def flush_log(report_dir):
    with open(os.path.join(report_dir, "log.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(LOG_LINES))


def sanitize(name):
    name = re.sub(r"\.\d{3}$", "", name)          # Blender .001 suffixes
    name = re.sub(r"^(SM_|S_|sm_|s_)", "", name)  # UE static-mesh prefixes
    name = re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_").lower()
    return name


def tri_count(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def wipe_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def bake_world(objs):
    """Deterministic data-level world bake. bpy.ops.transform_apply SILENTLY
    SKIPS multi-user (instanced) data — the root cause of the 0.8m-buildings
    fiasco — so bake matrices into mesh data by hand: exported FBX then
    carries unit-scale, real-metre geometry like the modular kit.

    Two hard-won subtleties (diag_house 2026-07-15):
    - matrix_world is STALE right after import in background mode; a
      depsgraph update must run first or the bake applies garbage.
    - UE level instances carry NEGATIVE (mirrored) scales; baking a
      negative-determinant matrix turns meshes inside-out unless normals
      are flipped back."""
    from mathutils import Matrix
    bpy.context.view_layer.update()
    for o in objs:
        if o.data.users > 1:
            o.data = o.data.copy()
        mw = o.matrix_world.copy()
        o.data.transform(mw)
        if mw.determinant() < 0:
            o.data.flip_normals()
        o.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.update()


def normalize_scale(objs):
    """Bake world into data, then detect a cm-vintage file (median piece
    ~100x too big) and uniform-scale it down. Median+p90 gating avoids
    misfiring on legitimately huge single pieces (74m spiral gazebos)."""
    from mathutils import Matrix
    bake_world(objs)
    dims = sorted(max(o.dimensions) for o in objs)
    scaled = False
    if dims:
        median = dims[len(dims) // 2]
        p90 = dims[int(len(dims) * 0.9)] if len(dims) > 1 else median
        if median > 50.0 and p90 > 100.0:
            log(f"[scale] cm-vintage detected (median {median:.0f} m, p90 {p90:.0f} m) -> x0.01")
            for o in objs:
                o.data.transform(Matrix.Scale(0.01, 4))
            scaled = True
    max_dim = max((max(o.dimensions) for o in objs), default=0.0)
    return max_dim, scaled


def decimate_to(obj, target_tris, weld=False):
    """weld=True first merges duplicate vertices (UE LEVEL exports are
    disconnected triangle soup — collapse-decimate refuses boundary edges and
    only shaves ~2%; welding rebuilds connectivity so the ratio actually
    lands). Per-ASSET exports have real connectivity and skip the weld."""
    tris = tri_count(obj)
    if tris <= target_tris:
        # Nothing to decimate — and welding here is pure downside: paper-thin
        # double-shell geometry (drapes) welds itself to zero triangles.
        return tris
    select_only([obj])
    if weld:
        wmod = obj.modifiers.new("norm_weld", "WELD")
        wmod.merge_threshold = 0.001
        bpy.ops.object.modifier_apply(modifier=wmod.name)
        tris = tri_count(obj)
    if tris <= target_tris:
        return tris
    mod = obj.modifiers.new("norm_decimate", "DECIMATE")
    mod.ratio = target_tris / tris
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return tri_count(obj)


def convex_hull_copy(obj, name):
    hull = obj.copy()
    hull.data = obj.data.copy()
    bpy.context.scene.collection.objects.link(hull)
    bm = bmesh.new()
    bm.from_mesh(hull.data)
    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    interior = [e for e in result["geom_interior"] if isinstance(e, bmesh.types.BMVert)]
    if interior:
        bmesh.ops.delete(bm, geom=interior, context="VERTS")
    bm.to_mesh(hull.data)
    bm.free()
    hull.name = hull.data.name = name
    hull.data.materials.clear()
    return hull


def decimated_copy(obj, name, target_tris, weld=False):
    dup = obj.copy()
    dup.data = obj.data.copy()
    bpy.context.scene.collection.objects.link(dup)
    dup.name = dup.data.name = name
    dup.data.materials.clear()
    decimate_to(dup, target_tris, weld=weld)
    return dup


MATERIAL_MAP = {}


def rename_materials(objs):
    for obj in objs:
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None:
                continue
            base = sanitize(re.sub(r"^(M_|MI_|m_|mi_)", "", mat.name))
            new = f"m_{base}" if base.startswith(f"{KIT}_") else f"m_{KIT}_{base}"
            # Texture-set-aligned final name (material_rename_map.json).
            if new in MATERIAL_MAP:
                new = MATERIAL_MAP[new]
            else:
                # UE FBX names duplicate slots Name_1/Name_2 — retry the map
                # with the numeric suffix stripped (gated on a map hit so real
                # numeric set names like lantern_00 are never mangled).
                stripped = re.sub(r"_\d+$", "", new)
                if stripped in MATERIAL_MAP:
                    new = MATERIAL_MAP[stripped]
                else:
                    # In-mesh materials invisible to the bindings dump
                    # (e.g. Window_Glass): keep the stem, t_ prefix per the
                    # kit-wide material naming decision.
                    new = "t_" + new[2:]
            existing = bpy.data.materials.get(new)
            if existing is not None and existing is not mat:
                slot.material = existing
            else:
                mat.name = new


def assign_physics_material(bo_obj, physmat):
    """Collision bodies carry their physics material as a material slot named
    after the physics id (Erebor precedent: plain 'stone' on bo_ meshes)."""
    bo_obj.data.materials.clear()
    mat = bpy.data.materials.get(physmat) or bpy.data.materials.new(physmat)
    bo_obj.data.materials.append(mat)


def export_fbx(path, objs):
    select_only(objs)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        bake_space_transform=True,
        add_leaf_bones=False,
        path_mode="AUTO",
    )


def process_mesh_fbx(src, dst_dir, meshlist_dir, collision_mode, collision_tris, max_tris, physmat, rows):
    wipe_scene()
    bpy.ops.import_scene.fbx(filepath=src)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        rows.append({"src": src, "error": "no mesh objects after import"})
        return

    # UCX_ = UE collision hulls; bo_ = pre-existing Bannerlord collision
    # (present when re-processing an already-converted kitbash FBX).
    # Files that ARE standalone collision hulls (kit ships e.g. SM_boat_UCX
    # as its own asset) never become visual meshes.
    if os.path.splitext(os.path.basename(src))[0].lower().endswith("_ucx"):
        rows.append({"src": src, "error": "SKIPPED: collision-only source asset"})
        return

    ucx = [o for o in meshes
           if o.name.upper().startswith("UCX") or o.name.lower().startswith("bo_")]
    visuals = [o for o in meshes if o not in ucx]
    if not visuals:
        rows.append({"src": src, "error": "only collision objects in file"})
        return

    if len(visuals) > 1:
        # evaluate matrices before joining multi-part sources (stale-matrix class)
        bpy.context.view_layer.update()
        select_only(visuals)
        bpy.ops.object.join()
        visuals = [bpy.context.view_layer.objects.active]
    visual = visuals[0]

    max_dim, rescaled = normalize_scale([visual] + ucx)

    # Name from the source FILENAME — deterministic, and it's how the UE
    # export names files (one StaticMesh per FBX).
    stem = sanitize(os.path.splitext(os.path.basename(src))[0])
    # avoid sm_tent_tent_* when the source stems already carry the kit word
    sm_name = f"sm_{stem}" if stem.startswith(f"{KIT}_") else f"sm_{KIT}_{stem}"
    visual.name = visual.data.name = sm_name
    rename_materials([visual])
    # UE kits leave the odd slot unmaterialed (telescope): the importer maps
    # those to a "FbxDefaultMaterial" placeholder (or leaves None). Fill from
    # the mesh's own first real material so the editor never sees a nameless
    # slot.
    def is_placeholder(m):
        return m is None or "fbx_default_material" in m.name.lower() or "fbxdefaultmaterial" in m.name.lower()
    bound = [s.material for s in visual.material_slots if not is_placeholder(s.material)]
    if bound:
        for slot in visual.material_slots:
            if is_placeholder(slot.material):
                slot.material = bound[0]

    tris_before = tri_count(visual)
    tris_after = tris_before
    if tris_before > max_tris:
        tris_after = decimate_to(visual, max_tris)

    bo_name = f"bo_{sm_name}"
    bo_obj = None
    bo_source = "none"
    if ucx:
        select_only(ucx)
        if len(ucx) > 1:
            bpy.ops.object.join()
        bo_obj = bpy.context.view_layer.objects.active
        bo_obj.name = bo_obj.data.name = bo_name
        bo_source = "ucx"
    elif collision_mode == "decimate":
        bo_obj = decimated_copy(visual, bo_name, collision_tris)
        bo_source = "decimate"
    elif collision_mode == "hull":
        bo_obj = convex_hull_copy(visual, bo_name)
        bo_source = "hull"
    if bo_obj is not None:
        assign_physics_material(bo_obj, physmat)

    out_objs = [visual] + ([bo_obj] if bo_obj else [])
    out_path = os.path.join(dst_dir, sm_name + ".fbx")
    export_fbx(out_path, out_objs)

    with open(os.path.join(meshlist_dir, sm_name + ".txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(o.name for o in out_objs))

    rows.append({
        "src": src, "out": out_path, "sm": sm_name,
        "tris_before": tris_before, "tris_after": tris_after,
        "bo": bo_name if bo_obj else "", "bo_source": bo_source,
        "max_dim_m": round(max_dim, 2), "rescaled_100x": rescaled, "error": "",
    })


def process_building_fbx(src, dst_dir, meshlist_dir, max_tris, collision_tris, rows, name=None):
    """One whole structure -> ONE joined mesh + bo_ twin (the Gondor
    gondor_building_small_a_01.fbx pattern): single unique mesh ID per FBX."""
    wipe_scene()
    try:
        bpy.ops.wm.fbx_import(filepath=src)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=src)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    drop = [o for o in meshes
            if o.name.upper().startswith("UCX") or o.name.lower().startswith("bo_")
            or re.search(r"_lod[1-9]\d*(\.\d{3})?$", o.name.lower())]
    for o in drop:
        bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        rows.append({"src": src, "error": "no mesh objects after import"})
        return
    max_dim, rescaled = normalize_scale(meshes)
    # Sky domes / backdrop artifacts (the 327.68m power-of-two spheres in the
    # kit's level exports) must not join into a building.
    sky = [o for o in meshes if max(o.dimensions) > 250.0]
    for o in sky:
        log(f"[building] dropping backdrop-scale object {o.name} ({max(o.dimensions):.0f} m)")
        bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    max_dim = max((max(o.dimensions) for o in meshes), default=0.0)
    select_only(meshes)
    if len(meshes) > 1:
        bpy.ops.object.join()
    building = bpy.context.view_layer.objects.active
    base = name or sanitize(os.path.splitext(os.path.basename(src))[0])
    base = re.sub(rf"^{KIT}_", "", base)
    base = re.sub(r"^l_assembly_level_elvish_", "", base)  # browser-friendly IDs
    sm_name = f"sm_{KIT}_bld_{base}"
    building.name = building.data.name = sm_name
    rename_materials([building])

    tris_before = tri_count(building)
    tris_after = decimate_to(building, max_tris, weld=True)

    bo_obj = decimated_copy(building, f"bo_{sm_name}", collision_tris, weld=True)
    assign_physics_material(bo_obj, "stone")

    out_path = os.path.join(dst_dir, sm_name + ".fbx")
    export_fbx(out_path, [building, bo_obj])
    asm_dir = os.path.join(os.path.dirname(meshlist_dir), "_meshlists_assembled")
    os.makedirs(asm_dir, exist_ok=True)
    with open(os.path.join(asm_dir, sm_name + ".txt"), "w", encoding="utf-8") as f:
        f.write(f"{sm_name}\nbo_{sm_name}")
    rows.append({"src": src, "out": out_path, "sm": sm_name,
                 "tris_before": tris_before, "tris_after": tris_after,
                 "bo": f"bo_{sm_name}", "bo_source": "decimate",
                 "max_dim_m": round(max_dim, 2), "rescaled_100x": rescaled, "error": ""})


FOLIAGE_TOKENS = ("tree", "shrub", "grass", "leaf", "leaves", "fern", "flower",
                  "bush", "clover", "daisy", "ivy", "vine", "hornbeam", "pine")


def process_citysplit_fbx(src, dst_dir, meshlist_dir, max_tris, collision_tris, rows):
    src_base = sanitize(os.path.splitext(os.path.basename(src))[0])
    src_base = re.sub(r"^l_assembly_level_elvish_", "", src_base)
    chunk_prefix = "city" if "elvenforestcity" in src_base else src_base
    """Split a whole-city level export into per-structure chunks: cluster by
    AABB adjacency (4m grid hash + union-find), join each cluster into one
    mesh re-pivoted to its base centre, and record original positions in
    city_layout.json for reassembly. Foliage instances are excluded (they
    stay modular) and listed with transforms in the same JSON."""
    import json as _json
    from mathutils import Matrix, Vector

    wipe_scene()
    try:
        bpy.ops.wm.fbx_import(filepath=src)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=src)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshes = [o for o in meshes
              if not (o.name.upper().startswith("UCX") or o.name.lower().startswith("bo_")
                      or re.search(r"_lod[1-9]\d*(\.\d{3})?$", o.name.lower()))]
    if not meshes:
        rows.append({"src": src, "error": "no mesh objects after import"})
        return
    # Foliage transforms must be captured BEFORE bake_world zeroes every
    # matrix (deep-review CRITICAL 2026-07-16: all foliage placements were
    # recorded at world origin). Token-exact matching, not substring —
    # 'tree' ⊂ 'street' was misclassifying structural geometry as foliage.
    bpy.context.view_layer.update()

    def is_foliage(name):
        return any(t in FOLIAGE_TOKENS for t in re.split(r"[^a-z]+", name.lower()))

    foliage_entries = []
    solid = []
    for o in meshes:
        if is_foliage(o.name):
            m = o.matrix_world
            foliage_entries.append({"name": sanitize(o.name),
                                    "pos": [round(v, 3) for v in m.to_translation()],
                                    "rot": [round(v, 4) for v in m.to_euler("XYZ")],
                                    "scale": [round(v, 3) for v in m.to_scale()]})
            bpy.data.objects.remove(o, do_unlink=True)
        else:
            solid.append(o)
    log(f"[citysplit] {len(solid)} structural objects, {len(foliage_entries)} foliage instances")

    max_dim, rescaled = normalize_scale(solid)
    if rescaled:
        for e in foliage_entries:
            e["pos"] = [round(v * 0.01, 3) for v in e["pos"]]
    sky = [o for o in solid if max(o.dimensions) > 250.0]
    for o in sky:
        log(f"[citysplit] dropping backdrop-scale object {o.name} ({max(o.dimensions):.0f} m)")
        bpy.data.objects.remove(o, do_unlink=True)
    solid = [o for o in bpy.context.scene.objects if o.type == "MESH"]

    # --- cluster structural objects on a 4m grid over inflated AABBs
    CELL = 4.0
    parent = list(range(len(solid)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    cell_map = {}
    for idx, o in enumerate(solid):
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        lo = [min(v[k] for v in bb) - 1.0 for k in range(3)]
        hi = [max(v[k] for v in bb) + 1.0 for k in range(3)]
        for cx in range(int(lo[0] // CELL), int(hi[0] // CELL) + 1):
            for cy in range(int(lo[1] // CELL), int(hi[1] // CELL) + 1):
                key = (cx, cy)  # cluster in plan view; verticality joins naturally
                if key in cell_map:
                    union(idx, cell_map[key])
                else:
                    cell_map[key] = idx

    clusters = {}
    for idx in range(len(solid)):
        clusters.setdefault(find(idx), []).append(solid[idx])
    log(f"[citysplit] {len(clusters)} raw clusters")

    def dominant_token(objs):
        counts = {}
        for o in objs:
            for tok in re.split(r"[^a-z]+", sanitize(o.name)):
                if len(tok) >= 4 and not tok.isdigit():
                    counts[tok] = counts.get(tok, 0) + 1
        return max(counts, key=counts.get) if counts else "chunk"

    layout = {"chunks": [], "foliage": foliage_entries}
    layout_name = f"{chunk_prefix}_layout.json"

    def flush_layout():
        with open(os.path.join(dst_dir, layout_name), "w", encoding="utf-8") as f:
            _json.dump(layout, f, indent=1)

    n_out = 0
    for ci, objs in enumerate(sorted(clusters.values(), key=len, reverse=True)):
        token = dominant_token(objs)
        select_only(objs)
        if len(objs) > 1:
            bpy.ops.object.join()
        chunk = bpy.context.view_layer.objects.active
        # stale bound_box right after join: dims read 0.0 and the re-pivot
        # below no-ops without this (deep-review HIGH 2026-07-16)
        bpy.context.view_layer.update()
        tris = tri_count(chunk)
        dims = max(chunk.dimensions)
        if tris < 3000 and dims < 4.0:
            log(f"[citysplit] skipping tiny cluster ({tris} tris, {dims:.1f} m): {chunk.name}")
            bpy.data.objects.remove(chunk, do_unlink=True)
            continue
        if token == chunk_prefix:
            sm_name = f"sm_{KIT}_{chunk_prefix}_{n_out:02d}"
        else:
            sm_name = f"sm_{KIT}_{chunk_prefix}_{token}_{n_out:02d}"
        n_out += 1
        chunk.name = chunk.data.name = sm_name
        rename_materials([chunk])
        # bake world into the mesh, then re-pivot to bbox bottom-centre;
        # the original world position goes into city_layout.json
        select_only([chunk])
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.context.view_layer.update()
        bb = [Vector(c) for c in chunk.bound_box]
        origin = Vector((sum(v[0] for v in bb) / 8.0, sum(v[1] for v in bb) / 8.0,
                         min(v[2] for v in bb)))
        chunk.data.transform(Matrix.Translation(-origin))

        tris_after = decimate_to(chunk, max_tris, weld=True)
        bo_obj = decimated_copy(chunk, f"bo_{sm_name}", collision_tris, weld=True)
        assign_physics_material(bo_obj, "stone")
        out_path = os.path.join(dst_dir, sm_name + ".fbx")
        export_fbx(out_path, [chunk, bo_obj])
        # non-modular (assembled) outputs go to a SEPARATE meshlist dir so the
        # placement matchers' known-set stays modular-only (deep-review HIGH:
        # chunk ids were matchable as 'templates')
        asm_dir = os.path.join(os.path.dirname(meshlist_dir), "_meshlists_assembled")
        os.makedirs(asm_dir, exist_ok=True)
        with open(os.path.join(asm_dir, sm_name + ".txt"), "w", encoding="utf-8") as f:
            f.write(f"{sm_name}\nbo_{sm_name}")
        layout["chunks"].append({"mesh": sm_name, "pos": [round(v, 3) for v in origin]})
        flush_layout()  # incremental: a mid-loop crash keeps earlier chunks' placements
        rows.append({"src": src, "out": out_path, "sm": sm_name, "tris_before": tris,
                     "tris_after": tris_after, "bo": f"bo_{sm_name}", "bo_source": "decimate",
                     "max_dim_m": round(dims, 2), "rescaled_100x": rescaled, "error": ""})
        bpy.data.objects.remove(chunk, do_unlink=True)
        bpy.data.objects.remove(bo_obj, do_unlink=True)

    flush_layout()
    log(f"[citysplit] exported {n_out} chunks + {layout_name} "
        f"({len(layout['foliage'])} foliage placements)")


def process_level_fbx(src, dst_dir, rows):
    wipe_scene()
    # UE level exports contain light actors; Blender 5.2's legacy Python FBX
    # importer crashes on them (CyclesLightSettings.cast_shadow removed).
    # The native importer handles lights fine — and we only keep meshes.
    try:
        bpy.ops.wm.fbx_import(filepath=src)
    except Exception:
        log(f"[normalize] native fbx_import failed on {src}; falling back to legacy importer")
        bpy.ops.import_scene.fbx(filepath=src)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    # Layout references: drop collision hulls and any LOD1+ duplicates that
    # slipped through the UE export options.
    drop = [o for o in meshes
            if o.name.upper().startswith("UCX") or o.name.lower().startswith("bo_")
            or re.search(r"_lod[1-9]\d*(\.\d{3})?$", o.name.lower())]
    for o in drop:
        bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        rows.append({"src": src, "error": "no mesh objects after import"})
        return
    max_dim, rescaled = normalize_scale(meshes)
    for o in meshes:
        o.name = o.data.name = f"sm_{KIT}_lvl_{sanitize(o.name)}"
    rename_materials(meshes)
    base = sanitize(os.path.splitext(os.path.basename(src))[0])
    out_path = os.path.join(dst_dir, f"{KIT}_{base}.fbx")
    export_fbx(out_path, meshes)
    rows.append({"src": src, "out": out_path, "sm": f"{len(meshes)} objects",
                 "max_dim_m": round(max_dim, 2), "rescaled_100x": rescaled, "error": ""})


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--dst", required=True)
    ap.add_argument("--collision", default="auto", choices=["auto", "decimate", "hull", "skip"])
    ap.add_argument("--collision-tris", type=int, default=4000)
    ap.add_argument("--max-tris", type=int, default=150000)
    ap.add_argument("--mode", default="mesh", choices=["mesh", "level", "building", "citysplit"])
    ap.add_argument("--building-max-tris", type=int, default=300000)
    ap.add_argument("--only", default="", help="process only FBX whose path contains this substring")
    ap.add_argument("--category-root", default="", help="base dir for category detection (default --src); pass the meshes root when sharding per-category")
    ap.add_argument("--shard", default="", help="i/n — process only files where index %% n == i (parallel sharding)")
    ap.add_argument("--material-map", default="", help="JSON {current material name: final name} from build_rivendell_material_sheet.py")
    ap.add_argument("--kit", default="rivendell", help="kit prefix for sm_/m_ names")
    ap.add_argument("--physmat", default="", help="physics material override for ALL collision (else stone/wood by category)")
    args = ap.parse_args(argv)
    global KIT
    KIT = args.kit
    args.category_root = args.category_root or args.src
    if args.material_map:
        with open(args.material_map, encoding="utf-8") as f:
            MATERIAL_MAP.update(json.load(f))

    shard_slug = ""
    if args.shard:
        shard_i, shard_n = (int(x) for x in args.shard.split("/"))
        shard_slug = f"_shard{shard_i}of{shard_n}"
    run_slug = re.sub(r"[^A-Za-z0-9]+", "_", os.path.basename(os.path.normpath(args.src))) + shard_slug

    report_dir = os.path.join(args.dst, "_normalize_report", run_slug)
    os.makedirs(report_dir, exist_ok=True)
    done_path = os.path.join(report_dir, "DONE.txt")
    if os.path.exists(done_path):
        os.remove(done_path)

    rows = []
    fbx_files = []
    for root, _dirs, files in os.walk(args.src):
        for f in files:
            if f.lower().endswith(".fbx") and (not args.only or args.only.lower() in os.path.join(root, f).lower()):
                fbx_files.append(os.path.join(root, f))
    fbx_files.sort()
    if args.shard:
        fbx_files = [p for idx, p in enumerate(fbx_files) if idx % shard_n == shard_i]
    log(f"[normalize] {len(fbx_files)} FBX files, mode={args.mode}, run={run_slug}")

    for i, src in enumerate(fbx_files):
        rel_dir = os.path.relpath(os.path.dirname(src), args.category_root)
        category = rel_dir.split(os.sep)[0] if rel_dir != "." else ""
        if args.mode in ("level", "building", "citysplit"):
            dst_dir = os.path.join(args.dst, "assembled")
            os.makedirs(dst_dir, exist_ok=True)
        else:
            sub, default_coll = CATEGORY_MAP.get(category, (category.lower() or "misc", "decimate"))
            dst_dir = os.path.join(args.dst, sub)
            os.makedirs(dst_dir, exist_ok=True)
        meshlist_dir = os.path.join(args.dst, "_meshlists")
        os.makedirs(meshlist_dir, exist_ok=True)

        collision_mode = args.collision
        if collision_mode == "auto":
            collision_mode = default_coll if args.mode == "mesh" else "skip"

        log(f"[normalize] ({i + 1}/{len(fbx_files)}) {src} -> {dst_dir} [collision={collision_mode}]")
        try:
            if args.mode == "building":
                process_building_fbx(src, dst_dir, meshlist_dir,
                                     args.building_max_tris, args.collision_tris, rows)
            elif args.mode == "citysplit":
                process_citysplit_fbx(src, dst_dir, meshlist_dir,
                                      args.building_max_tris, args.collision_tris, rows)
            elif args.mode == "level":
                process_level_fbx(src, dst_dir, rows)
            else:
                # Physics material by destination category: masonry-scale
                # pieces read as stone, furniture/props as wood.
                physmat = args.physmat or ("stone" if sub in ("buildings", "outdoor", "misc") else "wood")
                process_mesh_fbx(src, dst_dir, meshlist_dir, collision_mode,
                                 args.collision_tris, args.max_tris, physmat, rows)
        except Exception:
            log(traceback.format_exc())
            rows.append({"src": src, "error": traceback.format_exc(limit=1)})
        flush_log(report_dir)

    fields = ["src", "out", "sm", "tris_before", "tris_after", "bo", "bo_source",
              "max_dim_m", "rescaled_100x", "error"]
    with open(os.path.join(report_dir, "report.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)

    errors = [r for r in rows if r.get("error")]
    summary = {"processed": len(rows), "errors": len(errors),
               "decimated_visuals": len([r for r in rows if r.get("tris_after") != r.get("tris_before")])}
    with open(done_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[normalize] DONE {summary}")
    flush_log(report_dir)


main()
