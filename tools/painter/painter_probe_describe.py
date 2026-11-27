"""Painter-side probe (run through painter_run.py): report the open project's settings that the API shows.

ARGS: colour (true: also run the throw-away fill colour probe, default true).
RESULT: name, path, colour evidence, bounding box, texture sets with resolution, mesh names, channel formats.
"""
import substance_painter.colormanagement as cm
import substance_painter.layerstack as ls
import substance_painter.project as proj
import substance_painter.resource as res
import substance_painter.textureset as ts

A = ARGS  # noqa: F821  (injected by painter_run.py)
colour = {}
if A.get("colour", True):
    stack = ts.Stack.from_name(ts.all_texture_sets()[0].name())
    with ls.ScopedModification("probe colour"):
        f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(stack))
        try:
            f.active_channels = {ts.ChannelType.BaseColor}
            try:
                f.set_source(ts.ChannelType.BaseColor, cm.Color(0.2, 0.2, 0.2, cm.GenericColorSpace.Working))
                colour["working_colour_accepted"] = True
            except Exception as exc:
                colour["working_colour_accepted"] = False
                colour["refusal"] = str(exc)[:200]
            tex = list(res.search("u:texture"))
            if tex:
                s = f.set_source(ts.ChannelType.BaseColor, tex[0].identifier())
                colour["bitmap_spaces"] = ([str(c) for c in s.list_available_color_spaces()][:12]
                                       if hasattr(s, "list_available_color_spaces") else "source is " + type(s).__name__)
        finally:
            ls.delete_node(f)
box = proj.get_scene_bounding_box()
sets = {}
for t in ts.all_texture_sets():
    r = t.get_resolution()
    sets[t.name()] = {"resolution": [r.width, r.height], "mesh_names": t.all_mesh_names(),
                      "channels": {str(k): str(v.format()) for k, v in t.get_stack().all_channels().items()}}
RESULT = {"name": proj.name(), "path": proj.file_path(), "needs_saving": proj.needs_saving(), "colour": colour,
          "bbox": {"dimensions": list(box.dimensions), "center": list(box.center)}, "texture_sets": sets,
          "last_mesh": proj.last_imported_mesh_path()}
