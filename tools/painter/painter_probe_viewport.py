"""Painter-side probe (run through painter_run.py): a picture of the viewport from Painter itself.

ARGS: out (folder for the pictures), do (list: "names", "save", "display").
  names:   the names and docstrings of _substance_painter.display
  save:    save_viewport(path, size, size, flag) for each flag in "flags" (default [false, true])
  display: read the public display settings (environment, tone mapping, camera)
  size (1024), flags ([false, true]), tag ("v").
RESULT: what each step saw, with file sizes. The pictures are read by the caller.
"""
import os
import time

import substance_painter.display as disp
import substance_painter.project as proj
import substance_painter.resource as res
from PySide6.QtCore import QCoreApplication

import _substance_painter.display as private

A = ARGS  # noqa: F821  (injected by painter_run.py)
out_dir = A["out"]
todo = A.get("do", ["names"])
size = int(A.get("size", 1024))
tag = A.get("tag", "v")
os.makedirs(out_dir, exist_ok=True)


def settle(seconds=60):
    t0 = time.time()
    time.sleep(0.5)
    while time.time() - t0 < seconds and proj.is_busy():
        QCoreApplication.processEvents()
        time.sleep(0.1)


def cam_dict(c):
    return {"position": list(c.position), "rotation": list(c.rotation), "fov": c.field_of_view,
            "focal_length": c.focal_length, "focus_distance": c.focus_distance, "aperture": c.aperture,
            "ortho_height": c.orthographic_height, "projection": str(c.projection_type)}


result = {}
if "names" in todo:
    result["names"] = {n: (getattr(private, n).__doc__ or "")[:300] for n in dir(private) if not n.startswith("__")}
if "save" in todo:
    settle()
    rows = []
    for flag in A.get("flags", [False, True]):
        path = os.path.join(out_dir, "%s_%d_%s.bmp" % (tag, size, str(bool(flag)).lower()))
        row = {"flag": bool(flag), "path": path}
        t0 = time.time()
        try:
            private.save_viewport(path, size, size, bool(flag))
            row["seconds"] = round(time.time() - t0, 2)
            row["exists"] = os.path.isfile(path)
            row["bytes"] = os.path.getsize(path) if row["exists"] else 0
        except Exception as exc:
            row["error"] = repr(exc)[:300]
        rows.append(row)
    result["save"] = rows
if "display" in todo:
    d = {}
    for name in ("get_environment_resource", "get_color_lut_resource", "get_tone_mapping"):
        try:
            v = getattr(disp, name)()
            d[name] = str(v.url() if hasattr(v, "url") and v is not None else v)
        except Exception as exc:
            d[name] = "ERR " + repr(exc)[:200]
    try:
        d["camera"] = cam_dict(disp.Camera.get_default_camera())
    except Exception as exc:
        d["camera"] = "ERR " + repr(exc)[:200]
    d["environments"] = [r.identifier().name for r in res.search("u:environment")][:40]
    result["display"] = d
RESULT = result
