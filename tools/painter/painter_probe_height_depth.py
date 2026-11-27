"""Painter-side probe (run through painter_run.py): does an 8-bit height channel stair-step the normal map?

A fill with only the Height channel reads a 16-bit test image (smooth ramps). The script reports the channel
formats, optionally changes the Height channel format, and exports height and the normal at 8 and 16 bit.

ARGS: lib (painter_tile_lib.py), image (16-bit PNG height test), out (folder), height_format (null to leave,
      or a ChannelFormat name such as "L16F"), tag (names the sub folder), normal ("Normal_DirectX").
RESULT: the channel formats before and after, the export status and files.
"""
import os

exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)

A = ARGS  # noqa: F821
NORMAL_MAP = A.get("normal", "Normal_DirectX")


def formats():
    return {str(k): str(v.format()) for k, v in stack().all_channels().items()}


def export_pair(folder, bits):
    os.makedirs(folder, exist_ok=True)
    maps = [{"fileName": "$textureSet_height", "channels": [
                {"destChannel": "L", "srcChannel": "L", "srcMapType": "documentMap", "srcMapName": "height"}]},
            {"fileName": "$textureSet_normal", "channels": [
                {"destChannel": d, "srcChannel": d, "srcMapType": "virtualMap", "srcMapName": NORMAL_MAP}
                for d in "RGB"]}]
    status = exp.export_project_textures({
        "exportShaderParams": False, "exportPath": folder,
        "exportPresets": [{"name": "hd", "maps": maps}], "defaultExportPreset": "hd",
        "exportList": [{"rootPath": t.name()} for t in ts.all_texture_sets()],
        "exportParameters": [{"parameters": {"fileFormat": "png", "bitDepth": str(bits), "dithering": False,
                                             "paddingAlgorithm": "infinite", "sizeLog2": 10}}]})
    return {"status": status.status.name, "message": status.message}


before = formats()
ensure_ao()
clear_all()
img = res.import_project_resource(A["image"], res.Usage.TEXTURE, name="hd_test_" + A.get("tag", "x"))
f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack()))
f.set_name("height ramp test")
f.active_channels = {CT.Height}
f.set_source(CT.Height, img.identifier())
s = f.get_source(CT.Height)
for cs in s.list_available_color_spaces():
    if str(cs) == "GenericColorSpace.Raw":
        s.set_color_space(cs)
        break
if A.get("height_format"):
    stack().edit_channel(CT.Height, getattr(ts.ChannelFormat, A["height_format"]))
settle()
after = formats()
out = {}
for bits in (8, 16):
    settle()
    out["bits%d" % bits] = export_pair("%s/%s/bits%d" % (A["out"], A.get("tag", "x"), bits), bits)
RESULT = {"formats_before": before, "formats_after": after, "exports": out}
