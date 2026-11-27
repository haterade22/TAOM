"""Painter-side script (run through painter_run.py): texture the baked trim sheet from its bakes and export it.

The project must hold the bake of painter_trim_bake.py. The stack is built from the bottom after the same base
fill as the wall stone (painter_stone_build.py). The bakes are used as mesh map resources of the texture set.

ARGS: lib (path of painter_tile_lib.py), out (export folder), size (export size, 1024), work (set size, 2048),
      cfg (path of a JSON file, see below), maps (optional: folder of the exported bake PNGs, used when
      "map_source" is "png").
cfg keys (all have defaults in DEFAULTS below):
  base_colour_step   sRGB colour of the Add fill after the base (the wall's [0.49,0.45,0.43])
  cavity             {"method": "levels"|"multiply"|"generator"|"none", "colour", "opacity", "window": [lo, hi]}
  edge               {"method": "levels"|"generator"|"none", "colour", "opacity", "window"}
  joint              {"method": "levels"|"map"|"none", "colour", "window", "map"}. "levels": the low parts of the
                     baked height (it misses the joints that cut a raised moulding). "map": a PNG of the joints drawn
                     from the strip functions (rivendell_trim_maps.py --joint-map), used as the mask of the joint
                     colour and of the joint roughness
  halo               {"method": "none"|"map", "map": path, "opacity": 0.45}. "map": the contact shade beside the joints
                     (rivendell_trim_maps.py --halo-map), a Multiply fill like the wall stone's halo layer
  normal             {"method": "none"|"channel"|"height"}. Default none: Painter applies the baked normal map to
                     the exported normal by itself; a layer with it doubles the tilt (the trap of 2026-11-24)
  gloss              {"shift": 0.085, "joint": 0.137}
  base_height        false: the base fill has no Height channel (no grain normal); base_height_opacity
  ao_channel         opacity of the baked AO multiplied into the AO channel (1.0, 0 for none)
RESULT: {"status", "layers", "files", "message", "notes"}.
"""
# ruff: noqa: F821  (a Painter-side script: painter_run.py gives ARGS and RESULT, and the library that exec runs gives the other names)
import json

exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)
import substance_painter.resource as res

NORMAL_MAP = "Normal_DirectX"
DEFAULTS = {
    "base": {"material": "smoothed_limestone_rock_surface",
             "params": {"normal_intensity": 0.1, "height_range": 0.3, "contrast": -0.6},
             "colors": {"color": [0.82, 0.80, 0.74]}},
    "base_colour_step": [0.49, 0.45, 0.43],
    "sharpen": 0.5,
    "cavity": {"method": "levels", "colour": [0.36, 0.33, 0.29], "opacity": 0.55, "window": [0.7, 1.0]},
    "edge": {"method": "levels", "colour": [1.0, 0.97, 0.9], "opacity": 0.30, "window": [0.56, 0.85]},
    "joint": {"method": "levels", "colour": [0.915, 0.901, 0.878], "window": [0.5208, 0.525]},
    "halo": {"method": "none", "opacity": 0.45},
    "normal": {"method": "none", "blend": "NormalMapCombine", "opacity": 1.0},
    "gloss": {"shift": 0.085, "joint": 0.137},
    "ao_channel": 1.0,
    "height_ray_mm": 119.99,
}
cfg = json.loads(json.dumps(DEFAULTS))
for k, v in json.load(open(ARGS["cfg"])).items():  # noqa: F821
    if isinstance(v, dict) and isinstance(cfg.get(k), dict):
        cfg[k].update(v)
    else:
        cfg[k] = v

CHAN = {"BaseColor": CT.BaseColor, "Height": CT.Height, "Roughness": RC, "AO": CT.AO, "Normal": CT.Normal}
tset = ts.all_texture_sets()[0]
work = int(ARGS.get("work", 2048))  # noqa: F821
if tset.get_resolution().width != work:
    tset.set_resolution(ts.Resolution(work, work))
ensure_ao()
clear_all()
top = lambda: ls.InsertPosition.from_textureset_stack(stack())
USAGE = {"ao": ts.MeshMapUsage.AO, "curvature": ts.MeshMapUsage.Curvature, "height": ts.MeshMapUsage.Height,
         "normal": ts.MeshMapUsage.Normal}
_png = {}
notes = []


def bake(name):
    """The resource id of a baked map: the mesh map of the texture set, or its exported PNG imported."""
    if ARGS.get("map_source", "mesh") == "png":  # noqa: F821
        if name not in _png:
            fn = {"ao": "ambient_occlusion", "curvature": "curvature", "height": "height_base",
                  "normal": "normal_base"}[name]
            path = ARGS["maps"] + "/rivendell_tile_%s_Raw.png" % fn  # noqa: F821
            _png[name] = res.import_project_resource(path, res.Usage.TEXTURE, name="trim_" + name).identifier()
        return _png[name]
    return tset.get_mesh_map_resource(USAGE[name])


def to_raw(source):
    try:
        for cs in source.list_available_color_spaces():
            if str(cs) == "GenericColorSpace.Raw":
                source.set_color_space(cs)
                return True
    except Exception as exc:
        notes.append("no colour space list: " + str(exc)[:80])
    return False


def levels(mask_fill, window, invert=False):
    lv = ls.insert_levels_effect(ls.InsertPosition.above_node(mask_fill))
    p = lv.get_parameters()
    p.input_min, p.input_max = window[0], window[1]
    p.output_min, p.output_max = (1.0, 0.0) if invert else (0.0, 1.0)
    lv.set_parameters(p)


def map_mask(layer, name, window, invert=False):
    layer.add_mask(ls.MaskBackground.Black)
    m = ls.insert_fill(ls.InsertPosition.inside_node(layer, ls.NodeStack.Mask))
    s = m.set_source(None, bake(name))
    notes.append("mask %s raw=%s" % (name, to_raw(m.get_source())))
    levels(m, window, invert)
    return m


def map_file_mask(layer, path, name):
    """A mask from a PNG file (the joint map), set to Raw. No levels step."""
    layer.add_mask(ls.MaskBackground.Black)
    m = ls.insert_fill(ls.InsertPosition.inside_node(layer, ls.NodeStack.Mask))
    rid = res.import_project_resource(path, res.Usage.TEXTURE, name=name).identifier()
    m.set_source(None, rid)
    notes.append("mask %s raw=%s" % (name, to_raw(m.get_source())))
    if cfg["joint"].get("identity_levels"):
        levels(m, [0.0, 1.0])
    return m


def colour_layer(name, colour, blend, opacity, channels=("BaseColor",)):
    n = ls.insert_fill(top())
    n.set_name(name)
    n.active_channels = {CHAN[c] for c in channels}
    for c in channels:
        n.set_source(CHAN[c], rgb(colour))
        n.set_blending_mode(getattr(BM, blend), CHAN[c])
        n.set_opacity(float(opacity), CHAN[c])
    return n


base = cfg["base"]
f = ls.insert_fill(top())
f.set_name("base " + base["material"])
f.set_material_source(find(base["material"]))
src = f.get_material_source()
pr = dict(base.get("params", {}))
for k, c in base.get("colors", {}).items():
    cur = src.get_parameters()[k]
    pr[k] = (rgbp(c), 1.0) if isinstance(cur, tuple) else rgbp(c)
setp(src, pr)
f.active_channels = {CT.BaseColor, CT.Height, RC, CT.Normal, CT.AO, CT.Metallic}
if not cfg.get("base_height", True):
    f.active_channels = {CT.BaseColor, RC, CT.Normal, CT.AO, CT.Metallic}
elif cfg.get("base_height_opacity", 1.0) != 1.0:
    f.set_opacity(float(cfg["base_height_opacity"]), CT.Height)
names = [f.get_name()]
_filters = {r.identifier().name: r.identifier() for r in res.search("u:filter")}
e = ls.insert_filter_effect(ls.InsertPosition.inside_node(f, ls.NodeStack.Content), _filters["sharpen/sharpen"])
e.set_name("grain")
e.active_channels = {CT.BaseColor}
setp(e.get_source(), {"intensity": cfg["sharpen"]})

colour_layer("colour", cfg["base_colour_step"], "LinearDodge", 1.0)
names.append("colour")

cav = cfg["cavity"]
if cav["method"] == "levels":
    n = colour_layer("cavity (AO levels)", cav["colour"], "Normal", cav["opacity"])
    map_mask(n, "ao", cav["window"], invert=True)
    names.append(n.get_name())
elif cav["method"] == "multiply":
    n = ls.insert_fill(top())
    n.set_name("cavity (AO multiply)")
    n.active_channels = {CT.BaseColor}
    n.set_source(CT.BaseColor, bake("ao"))
    to_raw(n.get_source(CT.BaseColor))
    n.set_blending_mode(BM.Multiply, CT.BaseColor)
    n.set_opacity(float(cav["opacity"]), CT.BaseColor)
    names.append(n.get_name())
elif cav["method"] == "generator":
    n = colour_layer("cavity (mg_dirt)", cav["colour"], "Normal", cav["opacity"])
    n.add_mask(ls.MaskBackground.Black)
    g = ls.insert_generator_effect(ls.InsertPosition.inside_node(n, ls.NodeStack.Mask), find("mg_dirt"))
    setp(g.get_source(), cav.get("params", {}))
    names.append(n.get_name())

ed = cfg["edge"]
if ed["method"] == "levels":
    n = colour_layer("edge light (curvature levels)", ed["colour"], "Screen", ed["opacity"])
    map_mask(n, "curvature", ed["window"])
    names.append(n.get_name())
elif ed["method"] == "generator":
    n = colour_layer("edge light (mg_curvature)", ed["colour"], "Screen", ed["opacity"])
    n.add_mask(ls.MaskBackground.Black)
    g = ls.insert_generator_effect(ls.InsertPosition.inside_node(n, ls.NodeStack.Mask), find("mg_curvature"))
    setp(g.get_source(), ed.get("params", {}))
    names.append(n.get_name())

jt = cfg["joint"]
if jt["method"] == "levels":
    n = colour_layer("joint (height levels)", jt["colour"], "Multiply", 1.0)
    map_mask(n, "height", jt["window"], invert=True)
    names.append(n.get_name())
elif jt["method"] == "map":
    n = colour_layer("joint (map)", jt["colour"], "Multiply", float(jt.get("opacity", 1.0)))
    map_file_mask(n, jt["map"], "trim_joint_colour")
    names.append(n.get_name())

hl = cfg["halo"]
if hl["method"] == "map":
    n = ls.insert_fill(top())
    n.set_name("halo (contact shade)")
    n.active_channels = {CT.BaseColor}
    rid = res.import_project_resource(hl["map"], res.Usage.TEXTURE, name="trim_halo").identifier()
    n.set_source(CT.BaseColor, rid)
    notes.append("halo raw=%s" % to_raw(n.get_source(CT.BaseColor)))
    n.set_blending_mode(BM.Multiply, CT.BaseColor)
    n.set_opacity(float(hl["opacity"]), CT.BaseColor)
    names.append(n.get_name())

nm = cfg["normal"]
if nm["method"] == "channel":
    n = ls.insert_fill(top())
    n.set_name("baked normal")
    n.active_channels = {CT.Normal}
    n.set_source(CT.Normal, bake("normal"))
    n.set_blending_mode(getattr(BM, nm["blend"]), CT.Normal)
    n.set_opacity(float(nm["opacity"]), CT.Normal)
    names.append("baked normal")
elif nm["method"] == "height":
    n = ls.insert_fill(top())
    n.set_name("baked height")
    n.active_channels = {CT.Height}
    n.set_source(CT.Height, bake("height"))
    to_raw(n.get_source(CT.Height))
    n.set_blending_mode(BM.LinearDodge, CT.Height)
    n.set_opacity(float(nm["opacity"]), CT.Height)
    names.append("baked height")

g = cfg["gloss"]
n = ls.insert_fill(top())
n.set_name("gloss shift")
n.active_channels = {RC}
n.set_source(RC, gray(g["shift"]))
n.set_blending_mode(BM.Subtract, RC)
names.append("gloss shift")
if jt["method"] in ("levels", "map") and g.get("joint"):
    n = ls.insert_fill(top())
    n.set_name("joint rough")
    n.active_channels = {RC}
    n.set_source(RC, gray(g["joint"]))
    n.set_blending_mode(BM.LinearDodge, RC)
    if jt["method"] == "map":
        map_file_mask(n, jt["map"], "trim_joint_rough")
    else:
        map_mask(n, "height", jt["window"], invert=True)
    names.append("joint rough")

if cfg["ao_channel"]:
    n = ls.insert_fill(top())
    n.set_name("baked occlusion")
    n.active_channels = {CT.AO}
    n.set_source(CT.AO, bake("ao"))
    to_raw(n.get_source(CT.AO))
    n.set_blending_mode(BM.Multiply, CT.AO)
    n.set_opacity(float(cfg["ao_channel"]), CT.AO)
    names.append("baked occlusion")

settle(300)
r = export(ARGS["out"], int(ARGS.get("size", 1024)))  # noqa: F821
RESULT = {"status": r["status"], "layers": names, "files": r.get("files"), "message": r.get("message"),
          "notes": notes}
