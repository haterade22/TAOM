"""Painter-side script (run through painter_run.py): build one wall stone candidate and export it.

ARGS: lib (path of painter_tile_lib.py), spec (a dict, see below). One candidate:
  spec = {
    "name": "limestone_a", "out": "<export folder>", "size": 1024, "normal": "Normal_DirectX",
    "base": {"material": "smoothed_limestone_rock_surface", "params": {...}, "colors": {param: [r,g,b] sRGB 0-1},
             "uvscale": [sx, sy]},
    "layout": "<folder written by block_layout.py>" or null,      # null: the material alone
    "tone":  {"opacity": 0.2, "mode": "Overlay"},                  # a grey per block, from layout_id.png
    "stain": {"color": [r,g,b] sRGB 0-1, "opacity": 0.12},         # layout_stain.png as a mask
    "joint": {"color": [r,g,b] sRGB 0-1, "opacity": 0.6,           # layout_edge.png as a mask
              "depth": 0.3,                                        # height subtracted along the joint
              "rough": 0.75},                                      # roughness in the joint (null: leave)
    "ao": true                                                     # layout_ao.png multiplied into AO
  }
Stack from the bottom: base material, tone, stain, joint colour, joint height, joint AO. Every layout
bitmap is imported as a project texture and read as data (Raw colour space).
RESULT: {"status", "layers", "files"}.
"""
# ruff: noqa: F821  (a Painter-side script: painter_run.py gives ARGS and RESULT, and the library that exec runs gives the other names)
exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)
import substance_painter.resource as res

spec = ARGS["spec"]
NORMAL_MAP = spec.get("normal", "Normal_OpenGL")
ensure_ao()
clear_all()
top = lambda: ls.InsertPosition.from_textureset_stack(stack())
base = spec["base"]
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
if base.get("uvscale"):
    uvscale(f, base["uvscale"][0], base["uvscale"][1])
f.active_channels = {CT.BaseColor, CT.Height, RC, CT.Normal, CT.AO, CT.Metallic}
layers = [f.get_name()]


def bitmap(key):
    r = res.import_project_resource(spec["layout"] + "/layout_%s.png" % key, res.Usage.TEXTURE, name="lay_" + spec["name"] + "_" + key)
    return r.identifier()


def data_source(node, channel, ident):
    """Set a bitmap as the source of a channel and read it as data."""
    node.set_source(channel, ident)
    s = node.get_source(channel)
    for cs in s.list_available_color_spaces():
        if str(cs) == "GenericColorSpace.Raw":
            s.set_color_space(cs)
            break


def mask_bitmap(layer, ident):
    layer.add_mask(ls.MaskBackground.Black)
    m = ls.insert_fill(ls.InsertPosition.inside_node(layer, ls.NodeStack.Mask))
    m.set_source(None, ident)
    s = m.get_source()
    for cs in s.list_available_color_spaces():
        if str(cs) == "GenericColorSpace.Raw":
            s.set_color_space(cs)
            break
    return m


if spec.get("layout"):
    tone, stain, joint = spec.get("tone"), spec.get("stain"), spec.get("joint")
    if tone:
        t = ls.insert_fill(top())
        t.set_name("tone per block")
        t.active_channels = {CT.BaseColor}
        data_source(t, CT.BaseColor, bitmap("id"))
        t.set_blending_mode(getattr(BM, tone.get("mode", "Overlay")), CT.BaseColor)
        t.set_opacity(float(tone["opacity"]), CT.BaseColor)
        layers.append(t.get_name())
    if stain:
        s = ls.insert_fill(top())
        s.set_name("stain")
        s.active_channels = {CT.BaseColor}
        s.set_source(CT.BaseColor, rgb(stain["color"]))
        s.set_opacity(float(stain["opacity"]), CT.BaseColor)
        mask_bitmap(s, bitmap("stain"))
        layers.append(s.get_name())
    if joint:
        j = ls.insert_fill(top())
        j.set_name("joint colour")
        chans = {CT.BaseColor}
        if joint.get("rough") is not None:
            chans.add(RC)
        j.active_channels = chans  # set the channels first: a source on an inactive channel is lost
        j.set_source(CT.BaseColor, rgb(joint["color"]))
        if joint.get("rough") is not None:
            j.set_source(RC, gray(joint["rough"]))
        mask_bitmap(j, bitmap("edge"))
        j.set_opacity(float(joint["opacity"]), CT.BaseColor)
        layers.append(j.get_name())
        h = ls.insert_fill(top())
        h.set_name("joint height")
        h.active_channels = {CT.Height}
        data_source(h, CT.Height, bitmap("edge"))
        h.set_blending_mode(BM.Subtract, CT.Height)
        h.set_opacity(float(joint["depth"]), CT.Height)
        layers.append(h.get_name())
    if spec.get("ao"):
        a = ls.insert_fill(top())
        a.set_name("joint ao")
        a.active_channels = {CT.AO}
        data_source(a, CT.AO, bitmap("ao"))
        a.set_blending_mode(BM.Multiply, CT.AO)
        layers.append(a.get_name())
settle()
r = export(spec["out"], int(spec.get("size", 1024)))
RESULT = {"status": r["status"], "layers": layers, "files": r.get("files"), "message": r.get("message")}
