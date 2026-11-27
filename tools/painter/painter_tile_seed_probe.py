"""Painter-side script (run through painter_run.py): export base colour and normal of each shelf
material with $randomseed changed, to see whether the seed does anything.

ARGS: lib (path of painter_tile_lib.py), names (list), out (catalogue folder), seed (default 5),
size (default 1024), sub (folder name below the material, default seed_probe). Each export goes to <out>/<name>/<sub>/. One call runs the whole loop,
so no other script can interleave with it. It refuses to start when the layer stack is not empty
(another script is building there): it raises and changes nothing. It leaves the stack empty.
painter_tile_merge.py compares the exports with the default ones.
"""
# ruff: noqa: F821  (a Painter-side script: painter_run.py gives ARGS and RESULT, and the library that exec runs gives the other names)
exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)
ensure_ao()
if ls.get_root_layer_nodes(stack()):
    raise RuntimeError("the layer stack is not empty: another script is using Painter")
res_out = {}
try:
    for n in ARGS["names"]:
        f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack()))
        f.set_material_source(find(n))
        src = f.get_material_source()
        setp(src, {"$randomseed": int(ARGS.get("seed", 5))})
        # every channel stays on: the exported OpenGL normal is built from the Height and Normal channels
        f.active_channels = {CT.BaseColor, CT.Height, RC, CT.Normal, CT.AO, CT.Metallic}
        r = export(ARGS["out"] + "/" + n + "/" + ARGS.get("sub", "seed_probe"), int(ARGS.get("size", 1024)), only="cn")
        res_out[n] = r["status"] if r["status"] == "Success" else r
        ls.delete_node(f)
finally:
    clear_all()
RESULT = res_out
