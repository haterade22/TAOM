"""Painter-side probe (run through painter_run.py): rounded block edges made inside Painter.

Each variant is one Height-only fill layer whose source is a bitmap, with filters in its content stack,
and optionally an anchor point that a second layer reads as its Height source. The layer replaces the height
(Normal blend, opacity 1), so the exported height is the filtered bitmap itself. Height and the
Normal_DirectX map are exported per variant.

ARGS: lib (painter_tile_lib.py), variants_file (JSON list), out (folder).
Variant: {"name", "source": "<png path>", "filters": [{"id": "blur/blur", "params": {...}}, ...],
          "anchor": false, "reader_filters": [...] (filters on the reader layer when anchor is true)}
RESULT: per variant the status and, for anchor variants, what the reader source reported.
"""
# ruff: noqa: F821  (a Painter-side script: painter_run.py gives ARGS and RESULT, and the library that exec runs gives the other names)
import json
import os

exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)

A = ARGS  # noqa: F821
NORMAL_MAP = "Normal_DirectX"
variants = json.load(open(A["variants_file"]))
imported = {}
filter_ids = {r.identifier().name: r.identifier() for r in res.search("u:filter")}


def bitmap(path):
    if path not in imported:
        imported[path] = res.import_project_resource(path, res.Usage.TEXTURE, name="re_" + str(len(imported)) + "_" + os.path.basename(path)).identifier()
    return imported[path]


def raw(node, channel):
    s = node.get_source(channel)
    for cs in s.list_available_color_spaces():
        if str(cs) == "GenericColorSpace.Raw":
            s.set_color_space(cs)
            break


def add_filters(layer, filters):
    """Filters go into the content stack of the layer, each above the previous, on Height only."""
    made = []
    for spec in filters:
        eff = ls.insert_filter_effect(ls.InsertPosition.inside_node(layer, ls.NodeStack.Content), filter_ids[spec["id"]])
        eff.active_channels = {CT.Height}
        if spec.get("params"):
            setp(eff.get_source(), spec["params"])
        made.append(eff)
    return made


def export_hn(folder):
    os.makedirs(folder, exist_ok=True)
    maps = [{"fileName": "height", "channels": [
                {"destChannel": "L", "srcChannel": "L", "srcMapType": "documentMap", "srcMapName": "height"}]},
            {"fileName": "normal", "channels": [
                {"destChannel": d, "srcChannel": d, "srcMapType": "virtualMap", "srcMapName": NORMAL_MAP}
                for d in "RGB"]}]
    for bits, sub in (("16", "h16"), ("8", "h8")):
        status = exp.export_project_textures({
            "exportShaderParams": False, "exportPath": folder + "/" + sub,
            "exportPresets": [{"name": "re", "maps": maps}], "defaultExportPreset": "re",
            "exportList": [{"rootPath": t.name()} for t in ts.all_texture_sets()],
            "exportParameters": [{"parameters": {"fileFormat": "png", "bitDepth": bits, "dithering": False,
                                                 "paddingAlgorithm": "infinite", "sizeLog2": 10}}]})
        if status.status.name != "Success":
            return status.status.name + ": " + str(status.message)
    return "Success"


rows = []
for v in variants:
    row = {"name": v["name"]}
    try:
        clear_all()
        layer = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack()))
        layer.set_name("src " + v["name"])
        layer.active_channels = {CT.Height}
        layer.set_source(CT.Height, bitmap(v["source"]))
        raw(layer, CT.Height)
        add_filters(layer, v.get("filters", []))
        if v.get("anchor"):
            anchor = ls.insert_anchor_point_effect(ls.InsertPosition.inside_node(layer, ls.NodeStack.Content), "probe_anchor")
            reader = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack()))
            reader.set_name("reader " + v["name"])
            reader.active_channels = {CT.Height}
            ref = reader.set_source(CT.Height, anchor)
            ref.referenced_channel = CT.Height
            row["reader_source"] = type(ref).__name__
            row["referenced_channel"] = str(ref.referenced_channel)
            row["anchor_name"] = ref.anchor.get_name() if ref.anchor is not None else None
            add_filters(reader, v.get("reader_filters", []))
            layer.set_visible(False) if v.get("hide_source") else None
        settle()
        row["status"] = export_hn(A["out"] + "/" + v["name"])
    except Exception as exc:
        row["error"] = repr(exc)[:400]
    rows.append(row)
RESULT = rows
