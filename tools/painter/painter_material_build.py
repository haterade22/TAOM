"""Painter-side script (run through painter_run.py): one or more materials blended into one tiling set, exported.

ARGS: lib (path of painter_tile_lib.py), recipe (path of a JSON file), out (export folder),
      size (export size, 1024), work (texture set size, 2048).
Recipe:
  {"base": {"material": "...", "params": {...}, "colors": {param: [r, g, b] sRGB 0-1},
            "uv": {"rotation": 90, "scale": [1, 1], "offset": [0, 0]}},
   "layers": [
     {"name": "sharpen", "filter": "sharpen/sharpen", "channels": ["BaseColor"], "params": {"intensity": 0.5}},
     {"name": "second", "material": {same keys as base}, "opacity": 1.0,
      "mask": {"proc": "noise_perlin_noise", "params": {}, "scale": 2, "window": [0.4, 0.6], "invert": false}},
     {"name": "tone", "channels": ["BaseColor"], "blend": "Multiply", "opacity": 0.5, "source": {"color": [r, g, b]}}]}
A material layer may carry "content": [{"filter", "channels", "params"}] (filters inside the layer). A layer may take a "map" source or a string "mask" (layout map names: layout_<key>.png in the recipe's "layout"
folder, Raw). A "material" layer is a fill with the material on every channel and the Normal blend; its mask is a procedural
with an optional levels window. The uv keys of a material turn it (a quarter turn keeps the tile seamless), scale
it (a whole number keeps it seamless) or move it. Every procedural mask starts from a Black background.
RESULT: {"status", "layers", "files", "message", "mask_params"} (mask_params: the parameter names of each mask).
"""
# ruff: noqa: F821  (a Painter-side script: painter_run.py gives ARGS and RESULT, and the library that exec runs gives the other names)
exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)
import json
import substance_painter.resource as res

NORMAL_MAP = "Normal_DirectX"
recipe = json.load(open(ARGS["recipe"]))  # noqa: F821
CHAN = {"BaseColor": CT.BaseColor, "Height": CT.Height, "Roughness": RC, "AO": CT.AO, "Normal": CT.Normal,
        "Metallic": CT.Metallic}
ALL = ["BaseColor", "Height", "Roughness", "Normal", "AO", "Metallic"]
tset = ts.all_texture_sets()[0]
work = int(ARGS.get("work", 2048))  # noqa: F821
if tset.get_resolution().width != work:
    tset.set_resolution(ts.Resolution(work, work))
ensure_ao()
clear_all()
top = lambda: ls.InsertPosition.from_textureset_stack(stack())
mask_params = {}
_imported = {}
tag = recipe.get("tag", "mb")


def bitmap(key):
    """A layout map (recipe "layout" folder, layout_<key>.png) as a project texture; the name carries the tag, so
    a changed map under a used name cannot stay dead."""
    if key not in _imported:
        path = recipe["layout"] + "/layout_%s.png" % key
        _imported[key] = res.import_project_resource(path, res.Usage.TEXTURE, name="%s_%s" % (tag, key)).identifier()
    return _imported[key]


def to_raw(source):
    for cs in source.list_available_color_spaces():
        if str(cs) == "GenericColorSpace.Raw":
            source.set_color_space(cs)
            return


def set_material(fill, spec):
    fill.set_material_source(find(spec["material"]))
    src = fill.get_material_source()
    pr = dict(spec.get("params", {}))
    for k, c in spec.get("colors", {}).items():
        cur = src.get_parameters()[k]
        pr[k] = (rgbp(c), 1.0) if isinstance(cur, tuple) else rgbp(c)
    if pr:
        setp(src, pr)
    uv = spec.get("uv")
    if uv:
        fill.set_projection_mode(ls.ProjectionMode.UV)
        p = fill.get_projection_parameters()
        if "rotation" in uv:
            p.uv_transformation.rotation = float(uv["rotation"])
        if "scale" in uv:
            p.uv_transformation.scale = [float(x) for x in uv["scale"]]
        if "offset" in uv:
            p.uv_transformation.offset = [float(x) for x in uv["offset"]]
        fill.set_projection_parameters(p)


def levels(node, window, invert=False):
    lv = ls.insert_levels_effect(ls.InsertPosition.above_node(node))
    p = lv.get_parameters()
    p.input_min, p.input_max = float(window[0]), float(window[1])
    p.output_min, p.output_max = (1.0, 0.0) if invert else (0.0, 1.0)
    lv.set_parameters(p)


def add_proc_mask(layer, spec, name):
    layer.add_mask(ls.MaskBackground.Black)
    m = ls.insert_fill(ls.InsertPosition.inside_node(layer, ls.NodeStack.Mask))
    m.set_source(None, find(spec["proc"]))
    mask_params[name] = sorted(m.get_source().get_parameters())
    if spec.get("params"):
        setp(m.get_source(), spec["params"])
    if spec.get("scale", 1) != 1 or spec.get("uv"):
        m.set_projection_mode(ls.ProjectionMode.UV)
        p = m.get_projection_parameters()
        s = float(spec.get("scale", 1))
        p.uv_transformation.scale = [s, s]
        if spec.get("uv", {}).get("offset"):
            p.uv_transformation.offset = [float(x) for x in spec["uv"]["offset"]]
        m.set_projection_parameters(p)
    if spec.get("window"):
        levels(m, spec["window"], spec.get("invert", False))


base = recipe["base"]
f = ls.insert_fill(top())
f.set_name("base " + base["material"])
set_material(f, base)
f.active_channels = {CHAN[c] for c in ALL}
names = [f.get_name()]

_filters = {r.identifier().name: r.identifier() for r in res.search("u:filter")}
for spec in recipe.get("layers", []):
    chans = [CHAN[c] for c in spec.get("channels", ALL)]
    if spec.get("filter"):
        e = ls.insert_filter_effect(ls.InsertPosition.inside_node(f, ls.NodeStack.Content), _filters[spec["filter"]])
        e.set_name(spec["name"])
        e.active_channels = set(chans)
        if spec.get("params"):
            setp(e.get_source(), spec["params"])
        names.append(spec["name"])
        continue
    n = ls.insert_fill(top())
    n.set_name(spec["name"])
    n.active_channels = set(chans)  # first: a source on an inactive channel is lost
    if spec.get("material"):
        set_material(n, spec["material"])
        n.active_channels = set(chans)  # a material source resets the channels (Height and AO drop out): set them again
        for cs in spec.get("content", []):  # filters inside the layer, on the named channels
            e = ls.insert_filter_effect(ls.InsertPosition.inside_node(n, ls.NodeStack.Content), _filters[cs["filter"]])
            e.active_channels = {CHAN[c] for c in cs["channels"]}
            if cs.get("params"):
                setp(e.get_source(), cs["params"])
    else:
        s = spec["source"]
        for ch in chans:
            if "map" in s:
                n.set_source(ch, bitmap(s["map"]))
                to_raw(n.get_source(ch))
            else:
                n.set_source(ch, gray(float(s["gray"])) if "gray" in s else rgb(s["color"]))
    for ch in chans:
        n.set_blending_mode(getattr(BM, spec.get("blend", "Normal")), ch)
        n.set_opacity(float(spec.get("opacity", 1.0)), ch)
    if isinstance(spec.get("mask"), str):
        n.add_mask(ls.MaskBackground.Black)
        m = ls.insert_fill(ls.InsertPosition.inside_node(n, ls.NodeStack.Mask))
        m.set_source(None, bitmap(spec["mask"]))
        to_raw(m.get_source())
    elif spec.get("mask"):
        add_proc_mask(n, spec["mask"], spec["name"])
    names.append(spec["name"])

settle(300)
r = export(ARGS["out"], int(ARGS.get("size", 1024)))  # noqa: F821
RESULT = {"status": r["status"], "layers": names, "files": r.get("files"), "message": r.get("message"),
          "mask_params": mask_params}
