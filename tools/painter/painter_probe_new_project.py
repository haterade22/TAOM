"""Painter-side probe (run through painter_run.py): create a project on a plane and report its settings.

No project may be open. The OCIO environment variable is set for create() only (repo trap table).

ARGS: mesh (FBX path), save (path of the .spp to write), size (1024), normal ("DirectX" or "OpenGL"),
      tangent (optional "PerVertex" or "PerFragment"; absent leaves the default), ocio (optional config path),
      mesh_unit_scale (optional).
RESULT: the calls that ran, colour management evidence, texture sets, channel formats, bounding box.
"""
import os
import time

import substance_painter.colormanagement as cm
import substance_painter.layerstack as ls
import substance_painter.project as proj
import substance_painter.resource as res
import substance_painter.textureset as ts
from PySide6.QtCore import QCoreApplication

A = ARGS  # noqa: F821  (injected by painter_run.py)
OCIO = A.get("ocio", r"C:\Program Files\Adobe\Adobe Substance 3D Painter\resources\ocio\substance\config.ocio")


def pump(seconds, until):
    t0 = time.time()
    while not until() and time.time() - t0 < seconds:
        QCoreApplication.processEvents()
        time.sleep(0.05)
    return until()


if proj.is_open():
    raise RuntimeError("a project is open: %r" % proj.file_path())
if not os.path.isfile(A["mesh"]):
    raise FileNotFoundError(A["mesh"])
kwargs = dict(default_texture_resolution=int(A.get("size", 1024)),
              normal_map_format=getattr(proj.NormalMapFormat, A.get("normal", "DirectX")))
if A.get("tangent"):
    kwargs["tangent_space_mode"] = getattr(proj.TangentSpace, A["tangent"])
if A.get("mesh_unit_scale"):
    kwargs["mesh_unit_scale"] = float(A["mesh_unit_scale"])
settings = proj.Settings(**kwargs)
previous = os.environ.get("OCIO")
os.environ["OCIO"] = OCIO
try:
    proj.create(mesh_file_path=A["mesh"], settings=settings)
finally:
    if previous is None:
        os.environ.pop("OCIO", None)
    else:
        os.environ["OCIO"] = previous
if not pump(120, lambda: proj.is_open() and len(ts.all_texture_sets()) > 0):
    raise RuntimeError("the mesh did not load in 120 s")
pump(60, proj.is_in_edition_state)
os.makedirs(os.path.dirname(A["save"]), exist_ok=True)
proj.save_as(A["save"])
pump(120, lambda: not proj.is_busy())

colour = {}
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
    st = t.get_stack()
    r = t.get_resolution()
    sets[t.name()] = {"resolution": [r.width, r.height], "mesh_names": t.all_mesh_names(),
                      "channels": {str(k): str(v.format()) for k, v in st.all_channels().items()}}
RESULT = {"name": proj.name(), "path": proj.file_path(), "ocio_env_used": OCIO, "colour": colour,
          "bbox": {"dimensions": list(box.dimensions), "center": list(box.center)}, "texture_sets": sets,
          "last_mesh": proj.last_imported_mesh_path(), "settings_passed": {k: str(v) for k, v in kwargs.items()}}
