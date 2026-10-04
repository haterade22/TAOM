"""Which engine (rgl) log messages fall inside battle hitch windows (scratch, read-only).

Usage: hitch_rgl_correlate.py <taom_debug log> <rgl log> [hitch ms, default 300]
A [MissionPerf] line written at T covers the 5 s before it. A window is a hitch window when maxMs >= the
threshold, calm when maxMs < 60. For every rgl line in a window, its message is normalized (numbers and
hex replaced, truncated) and counted per window class; message types over-represented in hitch windows
are printed, with every rgl line of each hitch window listed in full (first 40 per window).
"""
import collections
import re
import sys
from datetime import datetime

taom, rgl = sys.argv[1], sys.argv[2]
thr = float(sys.argv[3]) if len(sys.argv) > 3 else 300.0
day = re.search(r"(\d{4}-\d\d-\d\d)", taom).group(1)

perf_rx = re.compile(r"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\] \[INFO\] \[MissionPerf\] t=\+(\d+)s .*?maxMs=([\d.]+)")
windows = []
for ln in open(taom, encoding="utf-8", errors="replace"):
    m = perf_rx.match(ln)
    if m:
        end = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S").timestamp()
        windows.append((end - 5.0, end + 1.0, float(m.group(3)), m.group(1), int(m.group(2))))
print(f"windows: {len(windows)}; hitch (>= {thr} ms): {sum(1 for w in windows if w[2] >= thr)}; "
      f"calm (< 60 ms): {sum(1 for w in windows if w[2] < 60)}")

rgl_rx = re.compile(r"^\[(\d\d:\d\d:\d\d)\.(\d+)\] (.*)$")
lines = []
for ln in open(rgl, encoding="utf-8", errors="replace"):
    m = rgl_rx.match(ln.rstrip("\n"))
    if m:
        t = datetime.strptime(f"{day} {m.group(1)}", "%Y-%m-%d %H:%M:%S").timestamp() + int(m.group(2)[:3]) / 1000
        lines.append((t, m.group(3)))


def norm(msg):
    msg = re.sub(r"0x[0-9a-fA-F]+", "<hex>", msg)
    msg = re.sub(r"\d+(\.\d+)?", "<n>", msg)
    return msg[:90]


hitch_c, calm_c = collections.Counter(), collections.Counter()
n_h = n_c = 0
detail = []
for start, end, mx, stamp, t in windows:
    inside = [(lt, msg) for lt, msg in lines if start <= lt <= end]
    if mx >= thr:
        n_h += 1
        hitch_c.update({norm(m) for _, m in inside})
        detail.append((stamp, t, mx, inside))
    elif mx < 60:
        n_c += 1
        calm_c.update({norm(m) for _, m in inside})
print(f"hitch windows with rgl lines: {sum(1 for d in detail if d[3])} of {n_h}")
rows = []
for k, v in hitch_c.items():
    ph = v / max(n_h, 1)
    pc = calm_c.get(k, 0) / max(n_c, 1)
    rows.append((ph - pc, ph, pc, k))
print("message types by over-representation (share of hitch windows minus share of calm windows):")
for diff, ph, pc, k in sorted(rows, reverse=True)[:25]:
    print(f"  {diff:+.2f}  hitch {ph:.2f}  calm {pc:.2f}  {k}")
for stamp, t, mx, inside in detail:
    print(f"\n-- hitch window ending {stamp} (t=+{t}s, maxMs={mx}): {len(inside)} rgl lines")
    for lt, msg in inside[:40]:
        print(f"   {datetime.fromtimestamp(lt).strftime('%H:%M:%S.%f')[:12]} {msg[:150]}")
