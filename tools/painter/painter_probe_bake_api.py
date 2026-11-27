"""Painter-side probe (run through painter_run.py): the baking parameter keys, and one bake of a plane on itself.

ARGS: do (list: "keys", "bake"), out (folder for keys.json), size_log2 (bake size, 10), ao_rays (64),
      maps (list of MeshMapUsage names to bake, default ["AO", "Curvature"]).
keys: every key of BakingParameters.from_texture_set(t).common() and .baker(usage) for these usages:
      Normal, AO, Curvature, Height, Position, Thickness, WorldSpaceNormal, ID, with label, widget type,
      value and enum labels. Written to <out>/keys.json.
bake: low as high, bake_selected_textures_async, wait for BakingProcessEnded, time it, list the mesh maps
      that exist afterwards.
RESULT: counts, the bake status and seconds, the maps found.
"""
import json
import math
import os
import time

import substance_painter.baking as bk
import substance_painter.event as ev
import substance_painter.project as proj
import substance_painter.textureset as ts
from PySide6.QtCore import QCoreApplication

A = ARGS  # noqa: F821  (injected by painter_run.py)
U = bk.MeshMapUsage
USAGES = ["Normal", "AO", "Curvature", "Height", "Position", "Thickness", "WorldSpaceNormal", "ID"]


def pump(seconds, until):
    t0 = time.time()
    while not until() and time.time() - t0 < seconds:
        QCoreApplication.processEvents()
        time.sleep(0.05)
    return until()


def plain(v):
    if isinstance(v, (tuple, list)):
        return [plain(x) for x in v]
    if isinstance(v, (int, float, str, bool)) or v is None:
        return v
    return str(v)


def describe(props):
    rows = {}
    for key, p in props.items():
        row = {"label": p.label(), "widget": p.widget_type(), "value": plain(p.value()), "name": p.name()}
        if p.widget_type() == "Combobox":
            row["enum"] = dict(p.enum_values())
        try:
            row["properties"] = {k: plain(v) for k, v in p.properties().items()}
        except Exception as exc:
            row["properties"] = "ERR " + repr(exc)[:80]
        rows[key] = row
    return rows


tset = ts.all_texture_sets()[0]
params = bk.BakingParameters.from_texture_set(tset)
result = {}
todo = A.get("do", ["keys"])
if "keys" in todo:
    keys = {"common": describe(params.common())}
    for u in USAGES:
        keys[u] = describe(params.baker(getattr(U, u)))
    os.makedirs(A["out"], exist_ok=True)
    with open(os.path.join(A["out"], "keys.json"), "w", encoding="utf-8") as fh:
        json.dump(keys, fh, indent=1)
    result["key_counts"] = {k: len(v) for k, v in keys.items()}
    result["curvature_method"] = str(params.get_curvature_method())
    result["enabled_bakers"] = [str(u) for u in params.get_enabled_bakers()]
if "bake" in todo:
    log2 = int(A.get("size_log2", 10))
    common = params.common()
    values = {common["OutputSize"]: (log2, log2), common["LowAsHigh"]: True, common["SmoothNormals"]: False,
              common["MaxHeight"]: 0.001, common["MaxDepth"]: 0.001}
    params.set(values)
    ao = params.baker(U.AO)
    params.set({ao["NbSecondary"]: int(A.get("ao_rays", 64)), ao["MaxDistance"]: 0.05, ao["GroundPlane"]: False})
    params.set_curvature_method(bk.CurvatureMethod.FromMesh)
    wanted = [getattr(U, n) for n in A.get("maps", ["AO", "Curvature"])]
    params.set_enabled_bakers(wanted)
    params.set_textureset_enabled(True)
    state = {"status": None, "event_type": None}

    def on_end(e):
        s = getattr(e, "status", None)
        state["status"] = s.name if hasattr(s, "name") else str(s)
        state["event_type"] = type(e).__name__

    ev.DISPATCHER.connect_strong(ev.BakingProcessEnded, on_end)
    t0 = time.time()
    try:
        bk.bake_selected_textures_async()
        pump(600, lambda: state["status"] is not None)
    finally:
        ev.DISPATCHER.disconnect(ev.BakingProcessEnded, on_end)
    result["bake_seconds"] = round(time.time() - t0, 2)
    result["bake_status"] = state["status"] or "timeout"
    result["event_type"] = state["event_type"]
    result["bake_size"] = 2 ** log2
    result["maps_found"] = [k for k in ts.MeshMapUsage.__members__
                            if tset.get_mesh_map_resource(getattr(ts.MeshMapUsage, k)) is not None]
RESULT = result
