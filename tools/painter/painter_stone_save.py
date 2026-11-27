"""Painter-side script (run through painter_run.py): save the open project and leave it open.

ARGS: expect (the project path that must be open). RESULT: {"path", "saved", "needs_saving"}.
"""
import os
import time

import substance_painter.project as proj
from PySide6.QtCore import QCoreApplication

expect = ARGS["expect"]  # noqa: F821  (injected by painter_run.py)
if not proj.is_open() or os.path.normcase(os.path.abspath(proj.file_path())) != os.path.normcase(os.path.abspath(expect)):
    raise RuntimeError("the open project is %r, not %r: nothing saved" % (proj.file_path() if proj.is_open() else None, expect))
t0 = time.time()
while time.time() - t0 < 300 and proj.is_busy():
    QCoreApplication.processEvents()
    time.sleep(0.1)
proj.save()
t0 = time.time()
while time.time() - t0 < 300 and proj.is_busy():
    QCoreApplication.processEvents()
    time.sleep(0.1)
RESULT = {"path": proj.file_path(), "needs_saving": proj.needs_saving()}
