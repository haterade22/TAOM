"""Where a load's time goes, from the engine's rgl log timestamps (scratch, read-only).

Usage: rgl_gaps.py <rgl_log.txt> <from hh:mm:ss> <to hh:mm:ss> [top]
Lists the largest gaps between consecutive timestamped lines inside the window, with the line before
the gap (what was running) and the line after it, plus totals for the "Loading xml file" lines.
"""
import re
import sys
from datetime import datetime

path, t0, t1 = sys.argv[1], sys.argv[2], sys.argv[3]
top = int(sys.argv[4]) if len(sys.argv) > 4 else 25
ts_re = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d{3})\] (.*)")
fmt = "%H:%M:%S.%f"
lo = datetime.strptime(t0 + ".000", fmt)
hi = datetime.strptime(t1 + ".999", fmt)
rows = []
for raw in open(path, encoding="utf-8", errors="replace"):
    m = ts_re.match(raw.rstrip("\n"))
    if not m:
        continue
    ts = datetime.strptime(m.group(1), fmt)
    if lo <= ts <= hi:
        rows.append((ts, m.group(2)))
print(f"{len(rows)} lines between {t0} and {t1}")
gaps = []
for i in range(len(rows) - 1):
    gap = (rows[i + 1][0] - rows[i][0]).total_seconds()
    gaps.append((gap, i))
gaps.sort(reverse=True)
total = sum(g for g, _ in gaps)
print(f"window span {total:.3f} s; top {top} gaps cover {sum(g for g, _ in gaps[:top]):.3f} s")
for gap, i in gaps[:top]:
    print(f"{gap:7.3f} s after [{rows[i][0]:%H:%M:%S.%f}] {rows[i][1][:110]}")
    print(f"           next: {rows[i + 1][1][:110]}")
xml = [r for r in rows if r[1].startswith("Loading xml file")]
print(f"'Loading xml file' lines: {len(xml)}")
