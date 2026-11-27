"""Painter-side script (run through painter_run.py): build a layered stone from a recipe and export it.

ARGS: lib (path of painter_tile_lib.py), recipe (path of a JSON file), out (export folder),
      size (export size, 1024), work (texture set size, 2048).
Recipe:
  {"layout": "<folder of layout_*.png>",
   "base": {"material": "...", "params": {...}, "colors": {param: [r, g, b] sRGB 0-1}},
   "layers": [ {"name", "channels": ["BaseColor"], "blend": "Overlay", "opacity": 0.24,
                "source": {"color": [r, g, b]} (sRGB 0-1) | {"gray": v} | {"map": "id"},
                "mask": "stain" (a layout map used as the mask, optional)} , ... ]}
A layer with "filter": "sharpen/sharpen" and "params" is an effect in the content stack of the base fill.
Channels: BaseColor, Height, Roughness, AO. A "map" source is a layout bitmap imported as a project texture
and read as data (Raw). The stack is built from the bottom, after the base fill. The Painter blends run in
LINEAR light, with a Raw map read as a linear value (the lesson of round 2b), so the opacities of a recipe
are not the ones of the host preview.
RESULT: {"status", "layers", "files"}.
"""
exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)
import json
import substance_painter.resource as res

NORMAL_MAP = "Normal_DirectX"
recipe = json.load(open(ARGS["recipe"]))  # noqa: F821
CHAN = {"BaseColor": CT.BaseColor, "Height": CT.Height, "Roughness": RC, "AO": CT.AO}
tset = ts.all_texture_sets()[0]
work = int(ARGS.get("work", 2048))  # noqa: F821
if tset.get_resolution().width != work:
    tset.set_resolution(ts.Resolution(work, work))
ensure_ao()
clear_all()
top = lambda: ls.InsertPosition.from_textureset_stack(stack())
tag = recipe.get("tag", "st")
_imported = {}


def bitmap(key):
    if key not in _imported:
        path = recipe["layout"] + "/layout_%s.png" % key
        _imported[key] = res.import_project_resource(path, res.Usage.TEXTURE, name="%s_%s" % (tag, key)).identifier()
    return _imported[key]


def to_raw(source):
    for cs in source.list_available_color_spaces():
        if str(cs) == "GenericColorSpace.Raw":
            source.set_color_space(cs)
            return


base = recipe["base"]
f = ls.insert_fill(top())
f.set_name("base " + base["material"])
f.set_material_source(find(base["material"]))
src = f.get_material_source()
pr = dict(base.get("params", {}))
for k, c in base.get("colors", {}).items():
    cur = src.get_parameters()[k]
    pr[k] = (rgbp(c), 1.0) if isinstance(cur, tuple) else rgbp(c)
if pr:
    setp(src, pr)
f.active_channels = {CT.BaseColor, CT.Height, RC, CT.Normal, CT.AO, CT.Metallic}
names = [f.get_name()]

_filters = {r.identifier().name: r.identifier() for r in res.search("u:filter")}
for spec in recipe["layers"]:
    chans = [CHAN[c] for c in spec["channels"]]
    if spec.get("filter"):
        # Painter refuses an effect at the top of the stack: the filter goes into the content stack of the
        # base fill, on the named channels only.
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
    s = spec["source"]
    for ch in chans:
        if "map" in s:
            n.set_source(ch, bitmap(s["map"]))
            to_raw(n.get_source(ch))
        elif "gray" in s:
            n.set_source(ch, gray(float(s["gray"])))
        else:
            n.set_source(ch, rgb(s["color"]))
        n.set_blending_mode(getattr(BM, spec.get("blend", "Normal")), ch)
        n.set_opacity(float(spec.get("opacity", 1.0)), ch)
    if spec.get("mask"):
        n.add_mask(ls.MaskBackground.Black)
        m = ls.insert_fill(ls.InsertPosition.inside_node(n, ls.NodeStack.Mask))
        m.set_source(None, bitmap(spec["mask"]))
        to_raw(m.get_source())
    if spec.get("hidden"):
        n.set_visible(False)
    names.append(spec["name"])

settle(300)
r = export(ARGS["out"], int(ARGS.get("size", 1024)))  # noqa: F821
RESULT = {"status": r["status"], "layers": names, "files": r.get("files"), "message": r.get("message")}
