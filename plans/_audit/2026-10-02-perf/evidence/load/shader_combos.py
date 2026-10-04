"""Count material-flag combinations and system contexts in an rgl log's compile window (scratch, read-only).

Usage: shader_combos.py <rgl log> <from hh:mm:ss> <to hh:mm:ss>
Field order assumed (inferred from the value patterns): input layout, system flags, material flags low 32,
material flags high 32.
"""
import collections
import re
import sys

path, lo, hi = sys.argv[1], sys.argv[2], sys.argv[3]
rx = re.compile(r"^\[(\d\d:\d\d:\d\d)\.\d+\] compile_shader: \$BASE/Shaders/Sources/([^,]+), ([^,]+), ([^,]+), "
                r"(-?\d+), (-?\d+), (-?\d+), (-?\d+)\.")
rows = []
for ln in open(path, encoding="utf-8", errors="replace"):
    m = rx.match(ln)
    if m and lo <= m.group(1) <= hi:
        rows.append((m.group(2), m.group(3), int(m.group(5)), int(m.group(6)) & 0xFFFFFFFF,
                     int(m.group(7)) & 0xFFFFFFFF, int(m.group(8))))
print("rows", len(rows))
mat = collections.Counter((r[4], r[5]) for r in rows)
print("distinct material combinations (low, high):", len(mat))
for (b, c), n in sorted(mat.items()):
    print(f"  low=0x{b:08x} high={c} compiles={n}")
per = collections.defaultdict(set)
for r in rows:
    per[(r[4], r[5])].add(r[3])
print("distinct system flag values:", len(collections.Counter(r[3] for r in rows)))
print("system contexts per material combination:", sorted(len(v) for v in per.values()))
layouts = collections.Counter(r[2] for r in rows)
print("input layouts:", dict(layouts))
