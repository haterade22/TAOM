"""Painter-side probe (run through painter_run.py): export size against texture set size.

Builds one candidate (painter_tile_build.py) with the texture set at each size in "sets", and exports it
at each size in "exports". The texture set goes back to 1024 at the end.

ARGS: lib (painter_tile_lib.py), build (painter_tile_build.py), spec_file (a spec JSON), out (folder),
      sets ([1024, 2048]), exports ([1024, 2048]).
Each export lands in <out>/set<S>_exp<E>.
RESULT: per run, the export status, the files and the seconds taken.
"""
import json
import time

A = ARGS  # noqa: F821  (injected by painter_run.py)
build_code = open(A["build"]).read()
spec0 = json.load(open(A["spec_file"]))
rows = []
for set_size in A.get("sets", [1024, 2048]):
    exports = A.get("exports", [1024, 2048])
    first = exports[0]
    ARGS = {"lib": A["lib"], "spec": dict(spec0, size=first, out="%s/set%d_exp%d" % (A["out"], set_size, first))}
    exec(open(A["lib"]).read())
    import substance_painter.textureset as ts  # noqa: E402
    for t in ts.all_texture_sets():
        t.set_resolution(ts.Resolution(set_size, set_size))
    settle()
    t0 = time.time()
    exec(build_code)
    rows.append({"set": set_size, "export": first, "seconds": round(time.time() - t0, 1),
                 "status": RESULT["status"], "files": RESULT["files"]})
    for e in exports[1:]:
        t0 = time.time()
        r = export("%s/set%d_exp%d" % (A["out"], set_size, e), e)
        rows.append({"set": set_size, "export": e, "seconds": round(time.time() - t0, 1),
                     "status": r["status"], "files": r.get("files")})
for t in ts.all_texture_sets():
    t.set_resolution(ts.Resolution(1024, 1024))
settle()
RESULT = {"rows": rows, "final_set": [(t.name(), t.get_resolution().width) for t in ts.all_texture_sets()]}
