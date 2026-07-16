"""
Bulk-export the ElvenForestCity UE 5.1 kit to FBX + textures + a
material-binding manifest, for conversion into the Bannerlord Rivendell kit.

Runs INSIDE the Unreal Editor's Python environment (not system Python).
Headless invocation (after installing UE 5.1 via the Epic launcher):

    "C:/Program Files/Epic Games/UE_5.1/Engine/Binaries/Win64/UnrealEditor-Cmd.exe" ^
        "E:/LOTRAOMAssets/Environment 3D Cosmos/ElvenForestCity/ElvenForestCity.uproject" ^
        -ExecutePythonScript="e:/repos/TAOM/tools/oneoff/ue_export_rivendell.py" ^
        -stdout -unattended -nosplash

(Or paste into Window > Output Log's Python input with the project open.
 Enable the "Python Editor Script Plugin" first if the import fails.)

Outputs under EXPORT_ROOT:
    meshes/<category>/<Name>.fbx     one FBX per StaticMesh, UCX collision included
    textures/<subpath>/<Name>.tga    every Texture2D (TGA keeps source pixels; the
                                     converter script reads TGA and PNG alike)
    material_bindings.json           mesh -> slots -> material instance -> texture/
                                     scalar/vector parameters (walked up the parent
                                     chain) — the map used to rebuild m_rivendell_*
                                     materials and to CONFIRM ChannelMap packing
    export_report.json               counts + skips + failures

The assembled levels (L_Assembly_Level_*, L_ElvenForestCity) are exported
manually from the UE UI (File > Export All) — see the Rivendell runbook.
"""

import json
import os
import traceback

import unreal

CONTENT_ROOT = "/Game/ElvenForestCity"
MESH_ROOT = CONTENT_ROOT + "/Mesh"
TEXTURE_ROOT = CONTENT_ROOT + "/Textures"
EXPORT_ROOT = r"E:\LOTRAOMAssets\_export\rivendell"

# Cinematic-only mesh folders that will never port to Bannerlord.
# Boundary-anchored ("/dir/" and "Name.") so partial name overlaps like
# SM_god_ray_plane_frame are NOT silently skipped.
SKIP_MESH_SUBSTRINGS = ["/Mesh/Waterfall_Meshes/", "SM_god_ray_plane."]


def _list_assets(root):
    return list(unreal.EditorAssetLibrary.list_assets(root, recursive=True, include_folder=False))


def _ensure_dir(path):
    if not os.path.isdir(path):
        os.makedirs(path)


def _rel_subpath(asset_path, root):
    # "/Game/ElvenForestCity/Mesh/Props/Foo.Foo" -> ("Props", "Foo")
    package = asset_path.split(".")[0]
    rel = package[len(root):].lstrip("/")
    parts = rel.split("/")
    return "/".join(parts[:-1]), parts[-1]


def export_static_meshes():
    exported, skipped, failed = [], [], []
    for asset_path in _list_assets(MESH_ROOT):
        if any(s in asset_path for s in SKIP_MESH_SUBSTRINGS):
            skipped.append(asset_path)
            continue
        asset = unreal.EditorAssetLibrary.load_asset(asset_path)
        if not isinstance(asset, unreal.StaticMesh):
            continue
        subdir, name = _rel_subpath(asset_path, MESH_ROOT)
        out_dir = os.path.join(EXPORT_ROOT, "meshes", subdir)
        _ensure_dir(out_dir)
        out_file = os.path.join(out_dir, name + ".fbx")

        options = unreal.FbxExportOption()
        options.collision = True          # UCX_ hulls ride along where they exist
        options.level_of_detail = False   # LOD0 only; Bannerlord auto-generates LODs
        options.vertex_color = True
        options.ascii = False

        task = unreal.AssetExportTask()
        task.object = asset
        task.filename = out_file
        task.automated = True
        task.replace_identical = True
        task.prompt = False
        task.options = options
        try:
            if unreal.Exporter.run_asset_export_task(task) and os.path.isfile(out_file):
                exported.append(asset_path)
            else:
                failed.append(asset_path)
        except Exception:
            unreal.log_error("FBX export failed: %s\n%s" % (asset_path, traceback.format_exc()))
            failed.append(asset_path)
    return exported, skipped, failed


def export_textures():
    exported, failed = [], []
    for asset_path in _list_assets(TEXTURE_ROOT):
        asset = unreal.EditorAssetLibrary.load_asset(asset_path)
        if not isinstance(asset, unreal.Texture2D):
            continue
        subdir, name = _rel_subpath(asset_path, TEXTURE_ROOT)
        out_dir = os.path.join(EXPORT_ROOT, "textures", subdir)
        _ensure_dir(out_dir)
        task = unreal.AssetExportTask()
        task.object = asset
        task.filename = os.path.join(out_dir, name + ".tga")
        task.automated = True
        task.replace_identical = True
        task.prompt = False
        try:
            if unreal.Exporter.run_asset_export_task(task):
                exported.append(asset_path)
            else:
                failed.append(asset_path)
        except Exception:
            unreal.log_error("Texture export failed: %s\n%s" % (asset_path, traceback.format_exc()))
            failed.append(asset_path)
    return exported, failed


def _material_params(mat):
    """Walk a MaterialInterface (+ parent chain) collecting parameter values."""
    info = {"path": mat.get_path_name(), "chain": [], "textures": {}, "scalars": {}, "vectors": {}}
    seen = set()
    current = mat
    while current is not None and current.get_path_name() not in seen:
        seen.add(current.get_path_name())
        info["chain"].append(current.get_path_name())
        if isinstance(current, unreal.MaterialInstanceConstant):
            for tp in current.texture_parameter_values:
                pname = str(tp.parameter_info.name)
                if pname not in info["textures"] and tp.parameter_value:
                    info["textures"][pname] = tp.parameter_value.get_path_name()
            for sp in current.scalar_parameter_values:
                pname = str(sp.parameter_info.name)
                if pname not in info["scalars"]:
                    info["scalars"][pname] = sp.parameter_value
            for vp in current.vector_parameter_values:
                pname = str(vp.parameter_info.name)
                if pname not in info["vectors"]:
                    v = vp.parameter_value
                    if v is not None:
                        info["vectors"][pname] = [v.r, v.g, v.b, v.a]
            current = current.parent
        elif isinstance(current, unreal.Material):
            # Base material: record parameter DEFAULTS not already overridden.
            for pname in unreal.MaterialEditingLibrary.get_texture_parameter_names(current):
                key = str(pname)
                if key not in info["textures"]:
                    tex = unreal.MaterialEditingLibrary.get_material_default_texture_parameter_value(current, pname)
                    if tex:
                        info["textures"][key] = tex.get_path_name()
            for pname in unreal.MaterialEditingLibrary.get_scalar_parameter_names(current):
                key = str(pname)
                if key not in info["scalars"]:
                    info["scalars"][key] = unreal.MaterialEditingLibrary.get_material_default_scalar_parameter_value(current, pname)
            current = None
        else:
            current = None
    return info


def dump_material_bindings():
    bindings = {}
    for asset_path in _list_assets(MESH_ROOT):
        asset = unreal.EditorAssetLibrary.load_asset(asset_path)
        if not isinstance(asset, unreal.StaticMesh):
            continue
        slots = []
        for sm in asset.static_materials:
            mat = sm.material_interface
            slots.append({
                "slot": str(sm.material_slot_name),
                "material": _material_params(mat) if mat else None,
            })
        bindings[asset_path] = slots
    out = os.path.join(EXPORT_ROOT, "material_bindings.json")
    with open(out, "w") as f:
        json.dump(bindings, f, indent=1, sort_keys=True)
    return len(bindings)


def main():
    _ensure_dir(EXPORT_ROOT)
    unreal.log("[rivendell-export] exporting static meshes...")
    meshes_ok, meshes_skipped, meshes_failed = export_static_meshes()
    unreal.log("[rivendell-export] exporting textures...")
    tex_ok, tex_failed = export_textures()
    unreal.log("[rivendell-export] dumping material bindings...")
    bound = dump_material_bindings()

    report = {
        "meshes_exported": len(meshes_ok),
        "meshes_skipped_cinematic": meshes_skipped,
        "meshes_failed": meshes_failed,
        "textures_exported": len(tex_ok),
        "textures_failed": tex_failed,
        "meshes_in_binding_json": bound,
    }
    with open(os.path.join(EXPORT_ROOT, "export_report.json"), "w") as f:
        json.dump(report, f, indent=1)
    unreal.log("[rivendell-export] DONE: %d meshes, %d textures, %d bindings; %d/%d failures (see export_report.json)"
               % (len(meshes_ok), len(tex_ok), bound, len(meshes_failed), len(tex_failed)))


main()
