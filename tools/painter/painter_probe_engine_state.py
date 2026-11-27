"""Painter-side probe (run through painter_run.py): engine and project state that could blank the viewport.

ARGS: none.
RESULT: busy, edition state, engine computation status, display settings that the API reads.
"""
import substance_painter.application as app
import substance_painter.project as proj
import substance_painter.textureset as ts

r = {"busy": proj.is_busy(), "edition": proj.is_in_edition_state(), "needs_saving": proj.needs_saving(),
     "version": app.version()}
try:
    r["engine_computations"] = str(app.engine_computations_status())
except Exception as exc:
    r["engine_computations"] = "ERR " + repr(exc)[:200]
r["sets"] = [(t.name(), t.get_resolution().width) for t in ts.all_texture_sets()]
RESULT = r
