"""
Re-export the textures that failed TGA export in ue_export_rivendell.py
(reads export_report.json's textures_failed), trying .png then .exr then .bmp.
Same invocation pattern as the main export script (see its docstring).
"""

import json
import os
import traceback

import unreal

EXPORT_ROOT = r"E:\LOTRAOMAssets\_export\rivendell"
TEXTURE_ROOT = "/Game/ElvenForestCity/Textures"


def _rel_subpath(asset_path, root):
    package = asset_path.split(".")[0]
    rel = package[len(root):].lstrip("/")
    parts = rel.split("/")
    return "/".join(parts[:-1]), parts[-1]


def main():
    with open(os.path.join(EXPORT_ROOT, "export_report.json")) as f:
        failed = json.load(f)["textures_failed"]
    unreal.log(f"[fixup] retrying {len(failed)} failed texture exports")

    results = {}
    for asset_path in failed:
        asset = unreal.EditorAssetLibrary.load_asset(asset_path)
        if asset is None:
            results[asset_path] = "LOAD FAILED"
            continue
        subdir, name = _rel_subpath(asset_path, TEXTURE_ROOT)
        out_dir = os.path.join(EXPORT_ROOT, "textures", subdir)
        if not os.path.isdir(out_dir):
            os.makedirs(out_dir)
        results[asset_path] = "ALL FORMATS FAILED"
        for ext in ("png", "exr", "bmp"):
            task = unreal.AssetExportTask()
            task.object = asset
            task.filename = os.path.join(out_dir, f"{name}.{ext}")
            task.automated = True
            task.replace_identical = True
            task.prompt = False
            try:
                if unreal.Exporter.run_asset_export_task(task) and os.path.isfile(task.filename):
                    results[asset_path] = ext
                    break
            except Exception:
                unreal.log_warning(f"[fixup] {asset_path} .{ext}: {traceback.format_exc(limit=1)}")

    with open(os.path.join(EXPORT_ROOT, "fixup_report.json"), "w") as f:
        json.dump(results, f, indent=1, sort_keys=True)
    ok = sum(1 for v in results.values() if v in ("png", "exr", "bmp"))
    unreal.log(f"[fixup] DONE {ok}/{len(failed)} recovered (fixup_report.json)")


main()
