"""Painter-side helpers for the tiling-plane catalogue scripts.

A script starts with exec(open(ARGS["lib"]).read()). Painter 12.2.0. find() looks a resource up
by name; setp() sets material parameters with the right Python types; export() writes the
catalogue maps (base colour, OpenGL normal, height, roughness, AO, metallic) at one size.
"""
import math
import os

import substance_painter.colormanagement as cm
import substance_painter.export as exp
import substance_painter.layerstack as ls
import substance_painter.project as proj
import substance_painter.resource as res
import substance_painter.textureset as ts

CT = ts.ChannelType
BM = ls.BlendingMode
RC = CT.SpecularRoughness
_cache = {}
NORMAL_MAP = "Normal_OpenGL"  # the virtual map for the exported normal; Normal_DirectX is the engine form


def find(name):
    for usage in ("u:procedural", "u:material", "u:texture", "u:filter", "u:generator", "u:alpha", "u:smartmask"):
        if usage not in _cache:
            _cache[usage] = {r.identifier().name: r.identifier() for r in res.search(usage)}
        for full, ident in _cache[usage].items():
            if full == name or full.split("/")[0] == name:
                return ident
    raise LookupError(name)


def lin(c):
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def hexrgb(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def rgb(c):
    """sRGB triple -> fill colour (a fill takes linear numbers)."""
    return cm.Color(*lin(c), cm.GenericColorSpace.Working)


def rgbp(c):
    return cm.Color(*c, cm.GenericColorSpace.sRGB)


def gray(v):
    return cm.Color(v, v, v, cm.GenericColorSpace.Raw)


def setp(source, params):
    have = source.get_parameters()
    miss = sorted(set(params) - set(have))
    if miss:
        raise KeyError("no params " + str(miss) + " in " + str(sorted(have)))
    fixed = {}
    for k, v in params.items():
        cur = have[k]
        if isinstance(cur, bool):
            v = bool(v)
        elif isinstance(cur, int):
            v = int(v)
        elif isinstance(cur, float):
            v = float(v)
        elif isinstance(cur, (tuple, list)) and cur and isinstance(cur[0], cm.Color):
            pass
        elif isinstance(cur, (tuple, list)):
            v = tuple(type(c)(x) for c, x in zip(cur, v))
        fixed[k] = v
    source.set_parameters(fixed)


def stack():
    return ts.Stack.from_name(ts.all_texture_sets()[0].name())


def ensure_ao():
    st = stack()
    if CT.AO not in st.all_channels():
        st.add_channel(CT.AO, ts.ChannelFormat.L8)


def clear_all():
    st = stack()
    for n in list(ls.get_root_layer_nodes(st)):
        ls.delete_node(n)


def group(name, pos=None):
    g = ls.insert_group(pos or ls.InsertPosition.from_textureset_stack(stack()))
    g.set_name(name)
    return g


def inside(g):
    return ls.InsertPosition.inside_node(g, ls.NodeStack.Substack)


def fill(pos, name, channels, color=None, rough=None, height=None):
    f = ls.insert_fill(pos)
    f.set_name(name)
    f.active_channels = set(channels)
    if color is not None:
        f.set_source(CT.BaseColor, rgb(color))
    if rough is not None:
        f.set_source(RC, gray(rough))
    if height is not None:
        f.set_source(CT.Height, gray(height))
    return f


def uvscale(node, sx, sy=None):
    node.set_projection_mode(ls.ProjectionMode.UV)
    p = node.get_projection_parameters()
    p.uv_transformation.scale = [sx, sy or sx]
    node.set_projection_parameters(p)


def mask_proc(layer, proc, params=None, scale=1, bg=ls.MaskBackground.Black):
    """A mask whose first node is a procedural fill; returns that fill."""
    layer.add_mask(bg)
    m = ls.insert_fill(ls.InsertPosition.inside_node(layer, ls.NodeStack.Mask))
    m.set_source(None, find(proc))
    if params:
        setp(m.get_source(), params)
    if scale != 1:
        uvscale(m, scale)
    return m


def settle(seconds=60):
    import time
    from PySide6.QtCore import QCoreApplication
    t0 = time.time()
    time.sleep(0.5)
    while time.time() - t0 < seconds and proj.is_busy():
        QCoreApplication.processEvents()
        time.sleep(0.1)


def export(out_dir, size=1024, preset_name="rv", only="all"):
    for _ in range(4):
        settle()
        r = _export(out_dir, size, preset_name, only)
        if r["status"] == "Success":
            return r
    return r


def _export(out_dir, size, preset_name, only="all"):
    os.makedirs(out_dir, exist_ok=True)
    chans = {"baseColor": ("baseColor", "RGB"), "normal": ("normal", "RGB"), "height": ("height", "L"),
             "roughness": ("specularRoughness", "L"), "ambientOcclusion": ("ambientOcclusion", "L"),
             "metallic": ("metallic", "L")}
    maps = []
    for fn, (src, kind) in chans.items():
        if only == "cn" and fn not in ("baseColor", "normal"):
            continue
        if fn == "normal":
            ch = [{"destChannel": d, "srcChannel": d, "srcMapType": "virtualMap", "srcMapName": NORMAL_MAP}
                  for d in "RGB"]
        elif kind == "RGB":
            ch = [{"destChannel": d, "srcChannel": d, "srcMapType": "documentMap", "srcMapName": src}
                  for d in "RGB"]
        else:
            ch = [{"destChannel": "L", "srcChannel": "L", "srcMapType": "documentMap", "srcMapName": src}]
        maps.append({"fileName": "$textureSet_" + fn, "channels": ch})
    params = {"fileFormat": "png", "bitDepth": "8", "dithering": False, "paddingAlgorithm": "infinite",
              "sizeLog2": int(math.log2(size))}
    status = exp.export_project_textures({
        "exportShaderParams": False,
        "exportPath": out_dir,
        "exportPresets": [{"name": preset_name, "maps": maps}],
        "defaultExportPreset": preset_name,
        "exportList": [{"rootPath": t.name()} for t in ts.all_texture_sets()],
        "exportParameters": [{"parameters": params}],
    })
    return {"status": status.status.name, "message": status.message,
            "files": sorted(str(p) for paths in dict(status.textures).values() for p in paths)}
