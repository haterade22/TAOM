"""Painter-side script (run through painter_run.py): dump every parameter of shelf materials.

ARGS: shelf (default "your_assets"), out (json path), only (optional list of material names).
Per material: category, tags, presets, outputs, and per parameter the name, label, widget type,
default, current value, min, max, step, group when given, enum values. A colour is written as
sRGB numbers (the Color converted to sRGB). A parameter named $randomseed is a random seed.
"""
import json
import os

import substance_painter.colormanagement as cm
import substance_painter.layerstack as ls
import substance_painter.resource as res
import substance_painter.textureset as ts


def color_srgb(c):
    return [round(x, 5) for x in c.sRGB[:3]]


def cv(x):
    return x() if callable(x) else x


def plain(v):
    if isinstance(v, cm.Color):
        return {"srgb": color_srgb(v), "repr": repr(v)[:80]}
    if isinstance(v, (tuple, list)):
        return [plain(x) for x in v]
    if isinstance(v, (bool, int, float, str)) or v is None:
        return v
    return repr(v)[:80]


shelf = ARGS.get("shelf", "your_assets")  # noqa: F821
only = ARGS.get("only")  # noqa: F821
if isinstance(only, str):
    only = [only]
st = ts.Stack.from_name(ts.all_texture_sets()[0].name())
out = {}
for r in res.search("u:material"):
    ident = r.identifier()
    short = ident.name.split("/")[0]
    if ident.context != shelf or (only and short not in only):
        continue
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(st))
    try:
        f.set_material_source(ident)
        s = f.get_material_source()
        ip = r.internal_properties()
        params = {}
        for k, p in s.get_properties().items():
            pr = p.properties
            pr = pr() if callable(pr) else pr
            params[k] = {"label": cv(p.label), "widget": str(cv(p.widget_type)),
                         "value": plain(cv(p.value)), "default": plain(pr.get("defaultValue")),
                         "min": plain(pr.get("min")), "max": plain(pr.get("max")), "step": pr.get("step"),
                         "visible": pr.get("visible"), "group": pr.get("group"),
                         "enum": {str(a): str(b) for a, b in (cv(p.enum_values) or {}).items()} or None,
                         "pyvalue_type": type(s.get_parameters().get(k)).__name__}
        out[short] = {"id": str(ident), "gui_name": cv(r.gui_name), "category": cv(r.category), "tags": list(cv(r.tags)),
                      "physicalSize": ip.get("physicalSize"), "presets": cv(s.get_preset_list),
                      "outputs": list(cv(s.image_outputs)), "params": params,
                      "has_seed": "$randomseed" in params}
    finally:
        ls.delete_node(f)
if ARGS.get("out"):  # noqa: F821
    os.makedirs(os.path.dirname(ARGS["out"]), exist_ok=True)  # noqa: F821
    with open(ARGS["out"], "w", encoding="utf-8") as fh:  # noqa: F821
        json.dump(out, fh, indent=1)
RESULT = {"count": len(out), "names": sorted(out)}
