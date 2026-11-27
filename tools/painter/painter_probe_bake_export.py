"""Painter-side probe (run through painter_run.py): write the baked mesh maps of the open project as PNG.

ARGS: out (folder). Uses the shelf export preset "Mesh Maps" (Adobe engine_tests/test_baking.py) and keeps the
      maps that exist.
RESULT: status, the map names kept, the files.
"""
import os

import substance_painter.export as exp
import substance_painter.textureset as ts

A = ARGS  # noqa: F821  (injected by painter_run.py)
U = ts.MeshMapUsage
USAGE = {"normal_base": U.Normal, "world_space_normals": U.WorldSpaceNormal, "id": U.ID, "position": U.Position,
         "bent_normals": U.BentNormals, "ambient_occlusion": U.AO, "curvature": U.Curvature,
         "thickness": U.Thickness, "height": U.Height, "opacity": U.Opacity}
os.makedirs(A["out"], exist_ok=True)
tset = ts.all_texture_sets()[0]
preset = next(p for p in exp.list_resource_export_presets() if p.resource_id.name == "Mesh Maps")
maps = preset.list_output_maps()
keep = [m for m in maps if tset.get_mesh_map_resource(USAGE[m["channels"][0]["srcMapName"]]) is not None]
status = exp.export_project_textures({
    "exportShaderParams": False, "exportPath": A["out"], "defaultExportPreset": "probe maps",
    "exportPresets": [{"name": "probe maps", "maps": keep}],
    "exportList": [{"rootPath": tset.name(), "filter": {"outputMaps": [m["fileName"] for m in keep]}}],
    "exportParameters": [{"parameters": {"fileFormat": "png", "bitDepth": "8", "dithering": False,
                                         "paddingAlgorithm": "infinite", "sizeLog2": 10}}]})
RESULT = {"status": status.status.name, "message": status.message, "kept": [m["fileName"] for m in keep],
          "files": sorted(str(p) for paths in dict(status.textures).values() for p in paths)}
