"""Painter-side script (run through painter_run.py): bake the trim high-poly onto the open plane and export the maps.

ARGS (all optional but high and out):
  high    path of the high-poly FBX (written as a file:/// address)
  out     folder for the exported mesh maps (cleared of old *.png of the same names by Painter)
  size    bake size in pixels (2048)
  common  {key: value} for BakingParameters.common(); a string value for a combobox is an enum label
  ao, curvature, height, normal  {key: value} for that baker
  maps    list of baker names to enable (default ["Normal", "AO", "Curvature", "Height", "Position"])
  curvature_method  "FromMesh" (default) or "FromNormalMap"
  export_bits  8 or 16 (16)
RESULT: bake status, seconds, what was set (with the value read back), the maps found, the files.
"""
import json
import math
import os
import time

import substance_painter.baking as bk
import substance_painter.event as ev
import substance_painter.export as exp
import substance_painter.textureset as ts
from PySide6.QtCore import QCoreApplication, QUrl

A = ARGS  # noqa: F821  (injected by painter_run.py)
U = bk.MeshMapUsage
MAP_USAGE = {"normal_base": U.Normal, "world_space_normals": U.WorldSpaceNormal, "id": U.ID,
             "position": U.Position, "bent_normals": U.BentNormals, "ambient_occlusion": U.AO,
             "curvature": U.Curvature, "thickness": U.Thickness, "height": U.Height, "opacity": U.Opacity}


def pump(seconds, until):
    t0 = time.time()
    while not until() and time.time() - t0 < seconds:
        QCoreApplication.processEvents()
        time.sleep(0.05)
    return until()


def value_for(prop, value):
    if isinstance(value, str) and prop.widget_type() == "Combobox":
        return prop.enum_value(value)
    if isinstance(value, list):
        return tuple(value)
    return value


def plain(v):
    if isinstance(v, (tuple, list)):
        return [plain(x) for x in v]
    return v if isinstance(v, (int, float, str, bool)) or v is None else str(v)


tset = ts.all_texture_sets()[0]
params = bk.BakingParameters.from_texture_set(tset)
# The bake parameters persist in the project between runs (a Distribution set by an earlier run stays), and the
# curvature keys depend on the method, so: set the method first, then put every key back to its default.
params.set_curvature_method(getattr(bk.CurvatureMethod, A.get("curvature_method", "FromMesh")))
log2 = int(math.log2(int(A.get("size", 2048))))
groups = {"common": params.common()}
for name in ("Normal", "AO", "Curvature", "Height"):
    groups[name] = params.baker(getattr(U, name))
reset = {}
for group in groups.values():
    for prop in group.values():
        default = prop.properties().get("defaultValue")
        if default is not None:
            if isinstance(prop.value(), float) and not isinstance(default, bool):
                default = float(default)  # a float key's default can come back as an int
            reset[prop] = tuple(default) if isinstance(default, list) else default
params.set(reset)
wanted = {"common": {"OutputSize": [log2, log2], "LowAsHigh": False,
                     "HipolyMesh": QUrl.fromLocalFile(A["high"]).toString()}}
wanted["common"].update(A.get("common", {}))
for arg, name in (("normal", "Normal"), ("ao", "AO"), ("curvature", "Curvature"), ("height", "Height")):
    if A.get(arg):
        wanted[name] = dict(A[arg])
values, readback = {}, {}
for group, kv in wanted.items():
    for key, value in kv.items():
        prop = groups[group][key]
        values[prop] = value_for(prop, value)
params.set(values)
for group, kv in wanted.items():
    for key in kv:
        readback[group + "." + key] = plain(groups[group][key].value())
enabled = [getattr(U, n) for n in A.get("maps", ["Normal", "AO", "Curvature", "Height", "Position"])]
params.set_enabled_bakers(enabled)
params.set_textureset_enabled(True)

state = {"status": None}


def on_end(e):
    s = getattr(e, "status", None)
    state["status"] = s.name if hasattr(s, "name") else str(s)


ev.DISPATCHER.connect_strong(ev.BakingProcessEnded, on_end)
t0 = time.time()
try:
    bk.bake_selected_textures_async()
    pump(1500, lambda: state["status"] is not None)
finally:
    ev.DISPATCHER.disconnect(ev.BakingProcessEnded, on_end)
seconds = round(time.time() - t0, 1)
found = [k for k in ts.MeshMapUsage.__members__ if tset.get_mesh_map_resource(getattr(ts.MeshMapUsage, k)) is not None]

files, message, export_status = [], None, None
if A.get("out") and state["status"] == "Success":
    os.makedirs(A["out"], exist_ok=True)
    preset = next(p for p in exp.list_resource_export_presets() if p.resource_id.name == "Mesh Maps")
    keep = [m for m in preset.list_output_maps()
            if tset.get_mesh_map_resource(MAP_USAGE[m["channels"][0]["srcMapName"]]) is not None]
    st = exp.export_project_textures({
        "exportShaderParams": False, "exportPath": A["out"], "defaultExportPreset": "trim maps",
        "exportPresets": [{"name": "trim maps", "maps": keep}],
        "exportList": [{"rootPath": tset.name(), "filter": {"outputMaps": [m["fileName"] for m in keep]}}],
        "exportParameters": [{"parameters": {"fileFormat": "png", "bitDepth": str(int(A.get("export_bits", 16))),
                                             "dithering": False, "paddingAlgorithm": "infinite",
                                             "sizeLog2": log2}}]})
    export_status, message = st.status.name, st.message
    files = sorted(str(p) for paths in dict(st.textures).values() for p in paths)
RESULT = {"bake_status": state["status"] or "timeout", "bake_seconds": seconds, "set": readback, "maps_found": found,
          "export_status": export_status, "message": message, "files": files}
