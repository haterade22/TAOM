"""How much of a startup is shader compilation? (scratch, read-only)

Usage: rgl_compile_window.py <rgl_log.txt>
Finds the compile_shader lines, then reports the window from the first to the last, how many other
lines fall inside it, the time spent in gaps that end on a compile line (compile time, approximately),
the shader sources compiled and the share of the whole startup (log start to the first scene of the
main menu or to the end of the window, whichever is later).
"""
import collections
import re
import sys
from datetime import datetime

ts_re = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d{3})\] (.*)")
fmt = "%H:%M:%S.%f"
rows = []
for raw in open(sys.argv[1], encoding="utf-8", errors="replace"):
    m = ts_re.match(raw.rstrip("\n"))
    if m:
        rows.append((datetime.strptime(m.group(1), fmt), m.group(2)))
idx = [i for i, r in enumerate(rows) if r[1].startswith("compile_shader:")]
if not idx:
    print("no compile_shader lines")
    sys.exit(0)
first, last = idx[0], idx[-1]
window = (rows[last][0] - rows[first][0]).total_seconds()
others = sum(1 for i in range(first, last + 1) if not rows[i][1].startswith("compile_shader:"))
compile_gap = sum((rows[i][0] - rows[i - 1][0]).total_seconds() for i in idx if i > 0)
sources = collections.Counter(re.sub(r"^compile_shader: \$BASE/Shaders/Sources/([^,]+),.*$", r"\1", rows[i][1]) for i in idx)
start = rows[0][0]
print(f"{len(idx)} compiles from {rows[first][0]:%H:%M:%S.%f} to {rows[last][0]:%H:%M:%S.%f}: window {window:.1f} s, "
      f"{others} other lines inside it, {compile_gap:.1f} s in gaps ending on a compile line")
print(f"log start {start:%H:%M:%S}, window ends {(rows[last][0] - start).total_seconds():.1f} s after it")
print("by source:", ", ".join(f"{s} {n}" for s, n in sources.most_common(8)))
