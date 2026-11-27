"""Painter-side test (run through painter_run.py): a stack of one fill whose Normal channel is the baked normal map.

ARGS: lib (painter_tile_lib.py), out (export folder), space ("default", "Raw", "NormalXYZRight", "NormalXYZLeft"),
      resolution (set size, 2048), size (export size, 1024), opacity (1.0).
Clears the stack, makes the fill, exports, RESULT: the status and the colour space read back.
"""
# ruff: noqa: F821  (a Painter-side script: painter_run.py gives ARGS and RESULT, and the library that exec runs gives the other names)
exec(open(ARGS["lib"]).read())  # noqa: F821
NORMAL_MAP = "Normal_DirectX"
tset = ts.all_texture_sets()[0]
r = int(ARGS.get("resolution", 2048))  # noqa: F821
if tset.get_resolution().width != r:
    tset.set_resolution(ts.Resolution(r, r))
ensure_ao()
clear_all()
n = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack()))
n.active_channels = {CT.Normal}
n.set_source(CT.Normal, tset.get_mesh_map_resource(ts.MeshMapUsage.Normal))
src = n.get_source(CT.Normal)
want = ARGS.get("space", "default")  # noqa: F821
if want != "default":
    for cs in src.list_available_color_spaces():
        if str(cs).endswith(want):
            src.set_color_space(cs)
n.set_opacity(float(ARGS.get("opacity", 1.0)), CT.Normal)  # noqa: F821
settle(120)
res_ = export(ARGS["out"], int(ARGS.get("size", 1024)))  # noqa: F821
RESULT = {"status": res_["status"], "space": str(n.get_source(CT.Normal).get_color_space())}
