"""Painter-side probe (run through painter_run.py): the state of the main window and a Qt grab of it.

ARGS: out (path of the PNG for the grab, optional).
RESULT: window state, size, visibility, the screen it is on, the UI mode, and the names of the docked widgets.
"""
import substance_painter.ui as pui

A = ARGS  # noqa: F821  (injected by painter_run.py)
w = pui.get_main_window()
state = {"title": w.windowTitle(), "visible": w.isVisible(), "minimized": w.isMinimized(),
         "active": w.isActiveWindow(), "size": [w.width(), w.height()], "state_flags": str(w.windowState()),
         "ui_mode": str(pui.get_current_mode())}
try:
    state["screen"] = w.screen().name()
except Exception as exc:
    state["screen"] = repr(exc)
state["children"] = sorted({type(c).__name__ + ":" + c.objectName() for c in w.findChildren(type(w).__mro__[1])
                            if c.objectName() and ("view" in c.objectName().lower() or "dock" in c.objectName().lower())})[:40]
if A.get("out"):
    pm = w.grab()
    state["grab_saved"] = pm.save(A["out"])
    state["grab_size"] = [pm.width(), pm.height()]
RESULT = state
