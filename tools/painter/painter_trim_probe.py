"""Painter-side probe (run through painter_run.py): what the texturing of the baked trim sheet can use.

Reads, never changes the stack: the blending modes, the type of a mesh map resource, the parameters of a
Levels effect on a throwaway layer (deleted again), the generator names that contain dirt or curv.
RESULT: dict.
"""
import substance_painter.layerstack as ls
import substance_painter.resource as res
import substance_painter.textureset as ts

tset = ts.all_texture_sets()[0]
out = {"blend_modes": [m for m in dir(ls.BlendingMode) if not m.startswith("_")]}
maps = {}
for name in ("AO", "Curvature", "Height", "Normal", "Position"):
    r = tset.get_mesh_map_resource(getattr(ts.MeshMapUsage, name))
    maps[name] = {"type": type(r).__name__, "has_identifier": hasattr(r, "identifier"),
                  "repr": str(r.identifier() if hasattr(r, "identifier") else r)[:120]}
out["mesh_maps"] = maps
gens = sorted(r.identifier().name for r in res.search("u:generator"))
out["generators"] = [g for g in gens if any(k in g.lower() for k in ("dirt", "curv", "edge", "ao", "occl", "cavity"))]
stack = ts.Stack.from_name(tset.name())
with ls.ScopedModification("probe levels"):
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
    try:
        f.add_mask(ls.MaskBackground.Black)
        m = ls.insert_fill(ls.InsertPosition.inside_node(f, ls.NodeStack.Mask))
        lv = ls.insert_levels_effect(ls.InsertPosition.above_node(m))
        p = lv.get_parameters()
        out["levels_type"] = type(p).__name__
        out["levels_fields"] = [a for a in dir(p) if not a.startswith("_")]
        out["levels_values"] = {a: str(getattr(p, a)) for a in out["levels_fields"] if not callable(getattr(p, a))}
    finally:
        ls.delete_node(f)
RESULT = out
