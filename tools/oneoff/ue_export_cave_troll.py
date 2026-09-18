"""
Bulk-export a Fab creature pack (Cave Troll Lightweight, 2026-09-17) out of a UE
project: skeletal meshes, animation clips and textures to FBX / TGA, plus a
material-binding manifest and an inventory, for retargeting onto a Bannerlord
skeleton (docs/ai-includes/troll-race-arp-retargeting-workflow.md).

Sibling of ue_export_rivendell.py (static kit). Runs INSIDE the Unreal Editor's
Python environment (not system Python). Headless invocation, UE 5.4.4 build at
E:\\UE_5.4 (the Python Editor Script Plugin must be enabled in the project, or
pass -EnablePlugins=PythonScriptPlugin as below):

    E:\\UE_5.4\\Engine\\Binaries\\Win64\\UnrealEditor-Cmd.exe ^
        "E:\\LOTRAOMAssets\\Troll_Animation_5_4\\Troll_Animation_5_4.uproject" ^
        -run=pythonscript -script="E:\\repos\\TAOM\\tools\\oneoff\\ue_export_cave_troll.py" ^
        -EnablePlugins=PythonScriptPlugin -stdout -unattended -nosplash -nullrhi

(Or paste into Window > Output Log's Python input with the project open.)

Environment overrides (so the constants below need no edit per pack):
    TAOM_UE_CONTENT_ROOT   asset folder to scan, default /Game (a fresh project
                           holds only the pack; engine and plugin content sit
                           outside /Game and are never touched)
    TAOM_UE_EXPORT_ROOT    output folder, default E:\\LOTRAOMAssets\\_export\\cave_troll_lightweight
    TAOM_UE_INVENTORY_ONLY set to 1 to write inventory.json and stop

Outputs under EXPORT_ROOT:
    inventory.json            every asset under the root with its class; per
                              AnimSequence: skeleton, frames, length, track
                              (bone) names; per SkeletalMesh: skeleton, LODs,
                              material slots. Written BEFORE any export.
    meshes/<sub>/<Name>.fbx   one FBX per SkeletalMesh, LOD0, morph targets on
    static/<sub>/<Name>.fbx   one FBX per StaticMesh (the mace), UCX riding along
    anims/<sub>/<Name>.fbx    one FBX per AnimSequence WITH its skeletal mesh
                              (bExportPreviewMesh), so the file carries the rig
    textures/<sub>/<Name>.tga every Texture2D
    material_bindings.json    mesh -> slots -> material instance -> texture/
                              scalar/vector parameters (parent chain walked)
    export_report.json        counts, failures, the assets left un-exported

The export is read-only on the project: the preview-mesh assignment below
happens in memory and nothing is saved.

Verified against the 5.4.4 sources on 2026-09-17:
  - FbxExportOption fields (Engine/Source/Editor/UnrealEd/Classes/Exporters/
    FbxExportOption.h): ASCII, ForceFrontXAxis, VertexColor, LevelOfDetail,
    Collision, ExportSourceMesh, ExportMorphTargets, ExportPreviewMesh,
    MapSkeletalMotionToRoot, ExportLocalTime. Python drops the b prefix and
    snake_cases.
  - UAnimSequenceExporterFBX::ExportBinary (EditorExporters.cpp:2250) RETURNS
    FALSE with a warning when the skeleton has no preview mesh, hence
    set_preview_skeletal_mesh() before each clip export.
  - AnimationLibrary (UAnimationBlueprintLibrary, ScriptName AnimationLibrary):
    get_num_frames, get_sequence_length, get_animation_track_names.
"""

import json
import os
import traceback

import unreal

CONTENT_ROOT = os.environ.get("TAOM_UE_CONTENT_ROOT", "/Game").rstrip("/") or "/Game"
EXPORT_ROOT = os.environ.get("TAOM_UE_EXPORT_ROOT", r"E:\LOTRAOMAssets\_export\cave_troll_lightweight")
INVENTORY_ONLY = os.environ.get("TAOM_UE_INVENTORY_ONLY", "") == "1"
TAG = "[cave-troll-export]"


def _list_assets(root):
    return list(unreal.EditorAssetLibrary.list_assets(root, recursive=True, include_folder=False))


def _ensure_dir(path):
    if not os.path.isdir(path):
        os.makedirs(path)


def _rel_subpath(asset_path, root):
    # "/Game/CaveTroll/Animations/Attack_01.Attack_01" -> ("CaveTroll/Animations", "Attack_01")
    package = asset_path.split(".")[0]
    rel = package[len(root):].lstrip("/")
    parts = rel.split("/")
    return "/".join(parts[:-1]), parts[-1]


def _path_or_none(obj):
    return obj.get_path_name() if obj is not None else None


def _load_by_class(root, cls):
    out = []
    for asset_path in _list_assets(root):
        asset = unreal.EditorAssetLibrary.load_asset(asset_path)
        if isinstance(asset, cls):
            out.append((asset_path, asset))
    return out


def _run_export(asset, out_file, options):
    task = unreal.AssetExportTask()
    task.object = asset
    task.filename = out_file
    task.automated = True
    task.replace_identical = True
    task.prompt = False
    task.options = options
    return unreal.Exporter.run_asset_export_task(task) and os.path.isfile(out_file)


# ---------------------------------------------------------------- inventory

def write_inventory():
    inventory = {"content_root": CONTENT_ROOT, "assets": {}, "by_class": {}}
    for asset_path in _list_assets(CONTENT_ROOT):
        asset = unreal.EditorAssetLibrary.load_asset(asset_path)
        cls = asset.get_class().get_name() if asset is not None else "UNLOADABLE"
        entry = {"class": cls}
        try:
            if isinstance(asset, unreal.AnimSequence):
                entry["skeleton"] = _path_or_none(asset.get_editor_property("skeleton"))
                entry["frames"] = unreal.AnimationLibrary.get_num_frames(asset)
                entry["length_s"] = unreal.AnimationLibrary.get_sequence_length(asset)
                entry["tracks"] = [str(n) for n in unreal.AnimationLibrary.get_animation_track_names(asset)]
            elif isinstance(asset, unreal.SkeletalMesh):
                entry["skeleton"] = _path_or_none(asset.get_editor_property("skeleton"))
                entry["lods"] = len(asset.get_editor_property("lod_info"))
                entry["material_slots"] = [
                    {"slot": str(m.material_slot_name), "material": _path_or_none(m.material_interface)}
                    for m in asset.materials
                ]
            elif isinstance(asset, unreal.Texture2D):
                entry["size"] = [asset.blueprint_get_size_x(), asset.blueprint_get_size_y()]
        except Exception:
            entry["inventory_error"] = traceback.format_exc()
        inventory["assets"][asset_path] = entry
        inventory["by_class"].setdefault(cls, 0)
        inventory["by_class"][cls] += 1
    with open(os.path.join(EXPORT_ROOT, "inventory.json"), "w") as f:
        json.dump(inventory, f, indent=1, sort_keys=True)
    return inventory


# ------------------------------------------------------------------ exports

def _skeletal_mesh_options():
    options = unreal.FbxExportOption()
    options.ascii = False
    options.level_of_detail = False      # LOD0 only; Bannerlord generates its own
    options.collision = False            # no UCX on a creature; physics asset is inventoried instead
    options.vertex_color = True
    options.export_morph_targets = True
    options.export_source_mesh = False
    return options


def _anim_options():
    options = unreal.FbxExportOption()
    options.ascii = False
    options.level_of_detail = False
    options.export_preview_mesh = True   # the FBX carries the skinned mesh + rig
    options.export_morph_targets = True
    options.map_skeletal_motion_to_root = False
    options.export_local_time = True
    return options


def export_skeletal_meshes():
    exported, failed = [], []
    for asset_path, asset in _load_by_class(CONTENT_ROOT, unreal.SkeletalMesh):
        subdir, name = _rel_subpath(asset_path, CONTENT_ROOT)
        out_dir = os.path.join(EXPORT_ROOT, "meshes", subdir)
        _ensure_dir(out_dir)
        out_file = os.path.join(out_dir, name + ".fbx")
        try:
            (exported if _run_export(asset, out_file, _skeletal_mesh_options()) else failed).append(asset_path)
        except Exception:
            unreal.log_error("%s skeletal mesh export failed: %s\n%s" % (TAG, asset_path, traceback.format_exc()))
            failed.append(asset_path)
    return exported, failed


def export_static_meshes():
    """Props that ride with the creature (the mace is a StaticMesh, not a skinned part)."""
    exported, failed = [], []
    for asset_path, asset in _load_by_class(CONTENT_ROOT, unreal.StaticMesh):
        subdir, name = _rel_subpath(asset_path, CONTENT_ROOT)
        out_dir = os.path.join(EXPORT_ROOT, "static", subdir)
        _ensure_dir(out_dir)
        out_file = os.path.join(out_dir, name + ".fbx")
        options = unreal.FbxExportOption()
        options.ascii = False
        options.collision = True
        options.level_of_detail = False
        options.vertex_color = True
        try:
            (exported if _run_export(asset, out_file, options) else failed).append(asset_path)
        except Exception:
            unreal.log_error("%s static mesh export failed: %s\n%s" % (TAG, asset_path, traceback.format_exc()))
            failed.append(asset_path)
    return exported, failed


def _mesh_for_skeleton(skeleton, meshes):
    """First SkeletalMesh bound to this skeleton, used as the clip's preview mesh."""
    if skeleton is None:
        return None
    want = skeleton.get_path_name()
    for _, mesh in meshes:
        sk = mesh.get_editor_property("skeleton")
        if sk is not None and sk.get_path_name() == want:
            return mesh
    return None


def export_animations():
    exported, failed, no_mesh = [], [], []
    meshes = _load_by_class(CONTENT_ROOT, unreal.SkeletalMesh)
    for asset_path, asset in _load_by_class(CONTENT_ROOT, unreal.AnimSequence):
        subdir, name = _rel_subpath(asset_path, CONTENT_ROOT)
        out_dir = os.path.join(EXPORT_ROOT, "anims", subdir)
        _ensure_dir(out_dir)
        out_file = os.path.join(out_dir, name + ".fbx")
        try:
            mesh = _mesh_for_skeleton(asset.get_editor_property("skeleton"), meshes)
            if mesh is None:
                # The exporter refuses a clip whose skeleton has no preview mesh
                # (EditorExporters.cpp:2255); nothing in the pack can stand in.
                no_mesh.append(asset_path)
                continue
            asset.set_preview_skeletal_mesh(mesh)   # in memory only; never saved
            (exported if _run_export(asset, out_file, _anim_options()) else failed).append(asset_path)
        except Exception:
            unreal.log_error("%s anim export failed: %s\n%s" % (TAG, asset_path, traceback.format_exc()))
            failed.append(asset_path)
    return exported, failed, no_mesh


def export_textures():
    exported, failed = [], []
    for asset_path, asset in _load_by_class(CONTENT_ROOT, unreal.Texture2D):
        subdir, name = _rel_subpath(asset_path, CONTENT_ROOT)
        out_dir = os.path.join(EXPORT_ROOT, "textures", subdir)
        _ensure_dir(out_dir)
        task = unreal.AssetExportTask()
        task.object = asset
        task.filename = os.path.join(out_dir, name + ".tga")
        task.automated = True
        task.replace_identical = True
        task.prompt = False
        try:
            (exported if unreal.Exporter.run_asset_export_task(task) else failed).append(asset_path)
        except Exception:
            unreal.log_error("%s texture export failed: %s\n%s" % (TAG, asset_path, traceback.format_exc()))
            failed.append(asset_path)
    return exported, failed


# --------------------------------------------------------- material bindings

def _material_params(mat):
    """Walk a MaterialInterface (+ parent chain) collecting parameter values.
    Same walker as ue_export_rivendell.py: the texture PARAMETER NAMES are what
    decide the _s channel packing later, never an assumption about the pack."""
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
    for asset_path, asset in _load_by_class(CONTENT_ROOT, unreal.SkeletalMesh):
        slots = []
        for sm in asset.materials:
            mat = sm.material_interface
            slots.append({
                "slot": str(sm.material_slot_name),
                "material": _material_params(mat) if mat else None,
            })
        bindings[asset_path] = slots
    with open(os.path.join(EXPORT_ROOT, "material_bindings.json"), "w") as f:
        json.dump(bindings, f, indent=1, sort_keys=True)
    return len(bindings)


# --------------------------------------------------------------------- main

def main():
    _ensure_dir(EXPORT_ROOT)
    unreal.log("%s inventory of %s -> %s" % (TAG, CONTENT_ROOT, EXPORT_ROOT))
    inventory = write_inventory()
    unreal.log("%s inventory: %s" % (TAG, json.dumps(inventory["by_class"], sort_keys=True)))
    if INVENTORY_ONLY:
        unreal.log("%s TAOM_UE_INVENTORY_ONLY=1, stopping after inventory.json" % TAG)
        return

    unreal.log("%s exporting skeletal meshes..." % TAG)
    meshes_ok, meshes_failed = export_skeletal_meshes()
    unreal.log("%s exporting static meshes..." % TAG)
    static_ok, static_failed = export_static_meshes()
    unreal.log("%s exporting animations..." % TAG)
    anims_ok, anims_failed, anims_no_mesh = export_animations()
    unreal.log("%s exporting textures..." % TAG)
    tex_ok, tex_failed = export_textures()
    unreal.log("%s dumping material bindings..." % TAG)
    bound = dump_material_bindings()

    exported_classes = {"SkeletalMesh", "StaticMesh", "AnimSequence", "Texture2D"}
    report = {
        "content_root": CONTENT_ROOT,
        "skeletal_meshes_exported": len(meshes_ok),
        "skeletal_meshes_failed": meshes_failed,
        "static_meshes_exported": len(static_ok),
        "static_meshes_failed": static_failed,
        "anims_exported": len(anims_ok),
        "anims_failed": anims_failed,
        "anims_skipped_no_skeletal_mesh_for_skeleton": anims_no_mesh,
        "textures_exported": len(tex_ok),
        "textures_failed": tex_failed,
        "meshes_in_binding_json": bound,
        "inventoried_not_exported": {
            path: e["class"] for path, e in inventory["assets"].items() if e["class"] not in exported_classes
        },
    }
    with open(os.path.join(EXPORT_ROOT, "export_report.json"), "w") as f:
        json.dump(report, f, indent=1, sort_keys=True)
    unreal.log("%s DONE: %d skeletal, %d static, %d anims (%d without a mesh), %d textures, %d bindings; failures %d/%d/%d/%d (export_report.json)"
               % (TAG, len(meshes_ok), len(static_ok), len(anims_ok), len(anims_no_mesh), len(tex_ok), bound,
                  len(meshes_failed), len(static_failed), len(anims_failed), len(tex_failed)))


main()
