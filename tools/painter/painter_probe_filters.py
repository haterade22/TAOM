"""Painter-side probe (run through painter_run.py): list shelf filters and the parameters of some.

ARGS: match (list of lower-case substrings; a filter whose name contains one is listed), params (list of
      exact filter identifier names whose parameters are read through a throw-away fill layer).
RESULT: names, and for each asked filter its parameter dict (key: value) and property labels.
"""
import substance_painter.layerstack as ls
import substance_painter.resource as res
import substance_painter.textureset as ts

A = ARGS  # noqa: F821  (injected by painter_run.py)
needles = [n.lower() for n in A.get("match", ["blur", "bevel", "round", "edge"])]
found = {}
for r in res.search("u:filter"):
    n = r.identifier().name
    if any(k in n.lower() for k in needles):
        found[n] = r.gui_name()


def plain(v):
    if isinstance(v, (tuple, list)):
        return [plain(x) for x in v]
    if isinstance(v, (int, float, str, bool)) or v is None:
        return v
    return str(v)


detail = {}
stack = ts.Stack.from_name(ts.all_texture_sets()[0].name())
ids = {r.identifier().name: r.identifier() for r in res.search("u:filter")}
if A.get("params"):
    with ls.ScopedModification("probe filters"):
        layer = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
        try:
            layer.active_channels = {ts.ChannelType.Height}
            for name in A["params"]:
                eff = ls.insert_filter_effect(ls.InsertPosition.inside_node(layer, ls.NodeStack.Content), ids[name])
                src = eff.get_source()
                detail[name] = {"params": {k: plain(v) for k, v in src.get_parameters().items()},
                                "labels": {k: p.label() for k, p in src.get_properties().items()},
                                "active_channels": [str(c) for c in eff.active_channels]}
                ls.delete_node(eff)
        finally:
            ls.delete_node(layer)
RESULT = {"filters": found, "detail": detail}
