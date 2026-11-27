"""Painter-side probe (run through painter_run.py): names in the private _substance_painter modules.

ARGS: modules (list, default project, baking, textureset, application), match (optional lower-case
      substring that a name must contain).
RESULT: module -> sorted names (with the first line of the docstring of each when "doc" is true).
"""
import importlib

A = ARGS  # noqa: F821  (injected by painter_run.py)
out = {}
needle = A.get("match", "").lower()
for mod in A.get("modules", ["project", "baking", "textureset", "application"]):
    try:
        m = importlib.import_module("_substance_painter." + mod)
    except Exception as exc:
        out[mod] = "n/a: " + repr(exc)[:100]
        continue
    rows = {}
    for n in dir(m):
        if n.startswith("__") or needle not in n.lower():
            continue
        doc = (getattr(getattr(m, n), "__doc__", "") or "").strip().splitlines()
        rows[n] = doc[0][:160] if (A.get("doc") and doc) else ""
    out[mod] = rows if A.get("doc") else sorted(rows)
RESULT = out
