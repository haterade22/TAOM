"""Painter-side script (run through painter_run.py): export shelf materials at default parameters.

ARGS: lib (path of painter_tile_lib.py), names (list of material short names), out (folder; one
subfolder per material), sub (optional subfolder name below it), params ({name: {param: value}}),
colors ({name: {param: [r, g, b] sRGB 0-1]}}), channels ("all" or "cn": base colour and normal only),
size (default 1024), uvscale ({name: [sx, sy]}: the fill's UV scale), normal_format (set the material's normal_format parameter, 0 DirectX, 1 OpenGL).
Each material goes onto an empty fill on the plane, is exported, and the fill is deleted.
RESULT: {name: status}.
"""
# ruff: noqa: F821  (a Painter-side script: painter_run.py gives ARGS and RESULT, and the library that exec runs gives the other names)
exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)
ensure_ao()
res_out = {}
for n in ARGS["names"]:
    clear_all()
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack()))
    f.set_material_source(find(n))
    src = f.get_material_source()
    pr = dict(ARGS.get("params", {}).get(n, {}))
    for k, c in ARGS.get("colors", {}).get(n, {}).items():
        cur = src.get_parameters()[k]
        pr[k] = (rgbp(c), 1.0) if isinstance(cur, tuple) else rgbp(c)
    if "normal_format" in ARGS and "normal_format" in src.get_parameters():
        pr["normal_format"] = ARGS["normal_format"]
    if pr:
        setp(src, pr)
    sc = ARGS.get("uvscale", {}).get(n)
    if sc:
        uvscale(f, sc[0], sc[1])
    f.active_channels = {CT.BaseColor, CT.Height, RC, CT.Normal, CT.AO, CT.Metallic}
    d = ARGS["out"] + "/" + n + ("/" + ARGS["sub"] if ARGS.get("sub") else "")
    r = export(d, int(ARGS.get("size", 1024)), only=ARGS.get("channels", "all"))
    res_out[n] = r["status"] if r["status"] == "Success" else r
clear_all()
RESULT = res_out
