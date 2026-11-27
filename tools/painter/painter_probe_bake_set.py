"""Painter-side probe (run through painter_run.py): set the baking keys that the first probe could not name.

Sets FilterMethod (Match: By mesh name), LowpolySuffix, HipolySuffix, CageMode (Custom file), CageMesh,
HipolyMesh (a file:/// address) and the Height baker's MaximizeRange, then reads each back.

ARGS: high (path of a mesh file to use as the HipolyMesh value), cage (path of a mesh file for CageMesh).
RESULT: for each key the value read back and whether it equals what was set.
"""
import substance_painter.baking as bk
import substance_painter.textureset as ts
from PySide6.QtCore import QUrl

A = ARGS  # noqa: F821  (injected by painter_run.py)
U = bk.MeshMapUsage
tset = ts.all_texture_sets()[0]
params = bk.BakingParameters.from_texture_set(tset)
common = params.common()
height = params.baker(U.Height)
url = QUrl.fromLocalFile(A["high"]).toString()
cage_url = QUrl.fromLocalFile(A["cage"]).toString()
wanted = {
    "FilterMethod": (common["FilterMethod"], common["FilterMethod"].enum_value("By mesh name")),
    "LowpolySuffix": (common["LowpolySuffix"], "_lo"),
    "HipolySuffix": (common["HipolySuffix"], "_hi"),
    "CageMode": (common["CageMode"], common["CageMode"].enum_value("Custom file")),
    "CageMesh": (common["CageMesh"], cage_url),
    "HipolyMesh": (common["HipolyMesh"], url),
    "Height.MaximizeRange": (height["MaximizeRange"], height["MaximizeRange"].enum_value("Manual")),
}
before = {k: p.value() for k, (p, v) in wanted.items()}
params.set({p: v for p, v in wanted.values()})
after = {}
for k, (p, v) in wanted.items():
    got = p.value()
    after[k] = {"set": str(v), "read_back": str(got), "equal": got == v or str(got) == str(v)}
# put them back
params.set({p: before[k] for k, (p, v) in wanted.items()})
RESULT = {"before": {k: str(v) for k, v in before.items()}, "after": after,
          "restored": {k: str(p.value()) for k, (p, v) in wanted.items()}}
