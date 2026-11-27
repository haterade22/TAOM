"""Painter-side probe (run through painter_run.py): the default blending mode of a new fill layer per channel.

ARGS: none. RESULT: channel -> blending mode name and opacity of a throw-away fill (deleted afterwards).
"""
import substance_painter.layerstack as ls
import substance_painter.textureset as ts

stack = ts.Stack.from_name(ts.all_texture_sets()[0].name())
out = {}
with ls.ScopedModification("probe blend default"):
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
    try:
        for ch in stack.all_channels():
            f.active_channels = {ch}
            out[str(ch)] = {"blend": f.get_blending_mode(ch).name, "opacity": f.get_opacity(ch)}
    finally:
        ls.delete_node(f)
RESULT = out
