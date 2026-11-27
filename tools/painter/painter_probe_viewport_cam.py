"""Painter-side probe (run through painter_run.py): set the camera by script, then save viewport pictures.

ARGS: out (folder), shots (list of {"name", "wait" (seconds of event pumping before the save), "position": [x,y,z], "rotation": [rx,ry,rz], "fov", "projection",
      "ortho_height", "env": "<environment name>"}), size (1024), flag (false), restore (true: put the
      camera and the environment back at the end).
RESULT: the scene bounding box, the camera before, each shot with its file size, the camera after.
"""
import os
import time

import substance_painter.display as disp
import substance_painter.project as proj
import substance_painter.resource as res
from PySide6.QtCore import QCoreApplication

A = ARGS  # noqa: F821  (injected by painter_run.py)
out_dir = A["out"]
size = int(A.get("size", 1024))
os.makedirs(out_dir, exist_ok=True)


def settle(seconds=30):
    t0 = time.time()
    time.sleep(0.3)
    while time.time() - t0 < seconds and proj.is_busy():
        QCoreApplication.processEvents()
        time.sleep(0.1)


def cam_dict(c):
    return {"position": list(c.position), "rotation": list(c.rotation), "fov": c.field_of_view,
            "projection": str(c.projection_type), "ortho_height": c.orthographic_height}


def env_id(name):
    for r in res.search("u:environment"):
        if r.identifier().name == name:
            return r.identifier()
    raise LookupError(name)


cam = disp.Camera.get_default_camera()
box = proj.get_scene_bounding_box()
before = cam_dict(cam)
env_before = disp.get_environment_resource()
result = {"bbox": {"dimensions": list(box.dimensions), "center": list(box.center)}, "before": before,
          "env_before": str(env_before.url()) if env_before else None, "shots": []}
for shot in A.get("shots", []):
    row = {"name": shot["name"]}
    try:
        if "env" in shot:
            disp.set_environment_resource(env_id(shot["env"]))
        if "projection" in shot:
            cam.projection_type = getattr(disp.CameraProjectionType, shot["projection"])
        if "position" in shot:
            cam.position = [float(v) for v in shot["position"]]
        if "rotation" in shot:
            cam.rotation = [float(v) for v in shot["rotation"]]
        if "fov" in shot:
            cam.field_of_view = float(shot["fov"])
        if "ortho_height" in shot:
            cam.orthographic_height = float(shot["ortho_height"])
        settle()
        end = time.time() + float(shot.get("wait", 0))
        while time.time() < end:
            QCoreApplication.processEvents()
            time.sleep(0.05)
        row["camera_read_back"] = cam_dict(cam)
        path = os.path.join(out_dir, shot["name"] + ".bmp")
        import _substance_painter.display as private
        t0 = time.time()
        private.save_viewport(path, size, size, bool(A.get("flag", False)))
        row["seconds"] = round(time.time() - t0, 2)
        row["bytes"] = os.path.getsize(path) if os.path.isfile(path) else 0
    except Exception as exc:
        row["error"] = repr(exc)[:300]
    result["shots"].append(row)
if A.get("restore", True):
    cam.projection_type = getattr(disp.CameraProjectionType, before["projection"].split(".")[-1])
    cam.position = before["position"]
    cam.rotation = before["rotation"]
    cam.field_of_view = before["fov"]
    cam.orthographic_height = before["ortho_height"]
    if env_before:
        disp.set_environment_resource(env_before)
    result["after"] = cam_dict(cam)
RESULT = result
