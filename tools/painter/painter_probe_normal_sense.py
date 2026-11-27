"""Painter-side probe (run through painter_run.py): which green sense does the project's own normal map have?

The API shows no normal map format of a project. This puts a vertical height ramp in a fill and exports the
document map "normal" beside the virtual maps Normal_OpenGL and Normal_DirectX; the host compares them.

ARGS: lib (painter_tile_lib.py), image (16-bit PNG whose height changes down the rows), out (folder).
RESULT: export status and files. The fill is deleted again.
"""
import os

exec(open(ARGS["lib"]).read())  # noqa: F821  (ARGS injected by painter_run.py)

A = ARGS  # noqa: F821
os.makedirs(A["out"], exist_ok=True)
img = res.import_project_resource(A["image"], res.Usage.TEXTURE, name="ns_test")
f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack()))
f.set_name("normal sense test")
f.active_channels = {CT.Height}
f.set_source(CT.Height, img.identifier())
s = f.get_source(CT.Height)
for cs in s.list_available_color_spaces():
    if str(cs) == "GenericColorSpace.Raw":
        s.set_color_space(cs)
        break
settle()


def rgb_map(file_name, kind, name):
    return {"fileName": file_name, "channels": [
        {"destChannel": d, "srcChannel": d, "srcMapType": kind, "srcMapName": name} for d in "RGB"]}


maps = [rgb_map("doc_normal", "documentMap", "normal"), rgb_map("virt_opengl", "virtualMap", "Normal_OpenGL"),
        rgb_map("virt_directx", "virtualMap", "Normal_DirectX"),
        {"fileName": "height", "channels": [
            {"destChannel": "L", "srcChannel": "L", "srcMapType": "documentMap", "srcMapName": "height"}]}]
status = exp.export_project_textures({
    "exportShaderParams": False, "exportPath": A["out"],
    "exportPresets": [{"name": "ns", "maps": maps}], "defaultExportPreset": "ns",
    "exportList": [{"rootPath": t.name()} for t in ts.all_texture_sets()],
    "exportParameters": [{"parameters": {"fileFormat": "png", "bitDepth": "8", "dithering": False,
                                         "paddingAlgorithm": "infinite", "sizeLog2": 10}}]})
ls.delete_node(f)
RESULT = {"status": status.status.name, "message": status.message,
          "files": sorted(str(p) for paths in dict(status.textures).values() for p in paths)}
