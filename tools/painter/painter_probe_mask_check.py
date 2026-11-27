"""Painter-side probe (run through painter_run.py): do the masks of a built candidate act?

Builds the candidate with painter_tile_build.py, then for each layer that has a mask: hides every other layer,
sets that layer's colour to pure red at opacity 1 (Normal blend), and exports the base colour. Where the mask
is white the export is red, where it is black it shows nothing. The host compares the red with the map.

ARGS: lib (painter_tile_lib.py), build (painter_tile_build.py), spec_file (spec JSON), out (folder).
RESULT: the layers (name, has_mask, visible) and, per masked layer, the export status. The real stack is
        restored (visibility, colour, opacity) at the end by building the candidate again.
"""
import json

A = ARGS  # noqa: F821  (injected by painter_run.py)
build_code = open(A["build"]).read()
spec0 = json.load(open(A["spec_file"]))


def build():
    global ARGS
    ARGS = {"lib": A["lib"], "spec": dict(spec0, size=1024, out=A["out"] + "/build")}
    exec(build_code, globals())
    return RESULT


built = build()
layers = [n for n in ls.get_root_layer_nodes(stack())]
info = [{"name": n.get_name(), "has_mask": n.has_mask() if hasattr(n, "has_mask") else None} for n in layers]
checks = {}
for target in layers:
    if not (hasattr(target, "has_mask") and target.has_mask()):
        continue
    for n in layers:
        n.set_visible(n is target)
    target.active_channels = {CT.BaseColor}
    target.set_source(CT.BaseColor, rgb([1.0, 0.0, 0.0]))
    target.set_blending_mode(BM.Normal, CT.BaseColor)
    target.set_opacity(1.0, CT.BaseColor)
    settle()
    r = export(A["out"] + "/mask_" + target.get_name().replace(" ", "_"), 1024, "mk", only="cn")
    checks[target.get_name()] = {"status": r["status"], "files": r.get("files")}
built_again = build()
RESULT = {"built": {"status": built["status"], "layers": built["layers"]}, "layers": info, "checks": checks,
          "rebuilt_status": built_again["status"]}
