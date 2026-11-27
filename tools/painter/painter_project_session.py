"""Painter-side script (run through painter_run.py): report, save and close, or open the project.

Two sessions can share one Painter. The session that holds the work claim key "painter-instance"
owns the open project. Before a pass it asks for the status and opens its project; after a pass it
saves and closes, so that the next session finds no foreign project
(docs/kitbash/tiling-texture-sets.md, "Sharing Painter").

ARGS: action = "status" (default), "save_close" or "open"; path (for "open");
      expect (for "save_close": the project path that must be open, so that a foreign
      project is never closed).
RESULT: {"action", "was_open", "path_before", "is_open", "path", "texture_sets"}
"""
import os
import time

import substance_painter.project as proj
import substance_painter.textureset as ts
from PySide6.QtCore import QCoreApplication


def same_file(a, b):
    return os.path.normcase(os.path.abspath(a)) == os.path.normcase(os.path.abspath(b))


def settle(seconds):
    """Let Painter work until it is idle. A script runs on the main thread, so it must pump events."""
    end = time.time() + seconds
    time.sleep(0.5)
    while time.time() < end and proj.is_busy():
        QCoreApplication.processEvents()
        time.sleep(0.1)


action = ARGS.get("action", "status")  # noqa: F821  (injected by painter_run.py)
was_open = proj.is_open()
path_before = proj.file_path() if was_open else None

if action == "save_close":
    expect = ARGS.get("expect")  # noqa: F821
    if was_open:
        if not expect or not path_before or not same_file(expect, path_before):
            raise RuntimeError("the open project is %r, not the expected %r: nothing closed" % (path_before, expect))
        settle(120)
        if proj.needs_saving():
            proj.save()
            settle(300)
        proj.close()
elif action == "open":
    path = ARGS["path"]  # noqa: F821
    if was_open and not same_file(path, path_before):
        raise RuntimeError("another project is open: %r. Nothing opened" % path_before)
    if not was_open:
        proj.open(path)
        end = time.time() + 300
        while time.time() < end and (proj.is_busy() or not ts.all_texture_sets()):
            QCoreApplication.processEvents()
            time.sleep(0.2)
elif action != "status":
    raise ValueError("unknown action %r" % action)

is_open = proj.is_open()
RESULT = {
    "action": action,
    "was_open": was_open,
    "path_before": path_before,
    "is_open": is_open,
    "path": proj.file_path() if is_open else None,
    "texture_sets": [t.name() for t in ts.all_texture_sets()] if is_open else [],
}
