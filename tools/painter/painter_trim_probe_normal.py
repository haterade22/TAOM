"""Painter-side probe (run through painter_run.py): the colour space of a baked normal map used as a Normal channel source.

Makes a throwaway fill on the open project, sets the baked Normal mesh map as the source of its Normal channel, reports
the colour space and the spaces on offer, then deletes the fill. RESULT: dict.
"""
import substance_painter.layerstack as ls
import substance_painter.textureset as ts

tset = ts.all_texture_sets()[0]
stack = ts.Stack.from_name(tset.name())
out = {}
with ls.ScopedModification("probe normal source"):
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
    try:
        f.active_channels = {ts.ChannelType.Normal}
        f.set_source(ts.ChannelType.Normal, tset.get_mesh_map_resource(ts.MeshMapUsage.Normal))
        s = f.get_source(ts.ChannelType.Normal)
        out["type"] = type(s).__name__
        out["space"] = str(s.get_color_space())
        out["offered"] = [str(c) for c in s.list_available_color_spaces()]
    except Exception as exc:
        out["error"] = repr(exc)[:300]
    finally:
        ls.delete_node(f)
RESULT = out
