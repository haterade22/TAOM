"""Does each ModuleData file's load cost grow with the files merged before it? (scratch, read-only)

Usage: rgl_xml_sequence.py <rgl_log.txt> <from hh:mm:ss> <to hh:mm:ss> <Schema>
Prints, in load order, each file of one schema with its "before" seconds (schema line to file line),
its size on disk, and the cumulative size of the same-schema files before it, then the correlation of
"before" with the cumulative size and with the file's own size.
"""
import os
import re
import sys
from datetime import datetime

path, t0, t1, want = sys.argv[1:5]
GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
ts_re = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d{3})\] (.*)")
fmt = "%H:%M:%S.%f"
lo = datetime.strptime(t0 + ".000", fmt)
hi = datetime.strptime(t1 + ".999", fmt)
rows = []
for raw in open(path, encoding="utf-8", errors="replace"):
    m = ts_re.match(raw.rstrip("\n"))
    if m:
        ts = datetime.strptime(m.group(1), fmt)
        if lo <= ts <= hi:
            rows.append((ts, m.group(2).strip()))
schema_re = re.compile(r"^opening \.\./\.\./XmlSchemas/(\w+)\.xsd")
file_re = re.compile(r"^opening \.\.\\\.\.\\(Modules\\.+)$")
seq = []
for i in range(1, len(rows)):
    fm = file_re.match(rows[i][1])
    sm = schema_re.match(rows[i - 1][1])
    if fm and sm and sm.group(1) == want:
        rel = fm.group(1).replace("/", "\\")
        full = os.path.join(GAME, rel)
        size = os.path.getsize(full) if os.path.exists(full) else -1
        seq.append(((rows[i][0] - rows[i - 1][0]).total_seconds(), size, rel))
cum = 0
xs_cum, xs_own, ys = [], [], []
print(f"{'#':>3} {'before s':>8} {'own KB':>8} {'cum KB before':>13}  file")
for n, (pre, size, rel) in enumerate(seq, 1):
    print(f"{n:3d} {pre:8.3f} {size / 1024:8.0f} {cum / 1024:13.0f}  {rel[-70:]}")
    if size >= 0:
        xs_cum.append(cum)
        xs_own.append(size)
        ys.append(pre)
        cum += size


def corr(a, b):
    n = len(a)
    if n < 3:
        return float("nan")
    ma, mb = sum(a) / n, sum(b) / n
    va = sum((x - ma) ** 2 for x in a)
    vb = sum((y - mb) ** 2 for y in b)
    cov = sum((x - ma) * (y - mb) for x, y in zip(a, b))
    return cov / (va * vb) ** 0.5 if va and vb else float("nan")


print(f"\n{len(seq)} files, total before {sum(ys):.3f} s; corr(before, cumulative size) = {corr(xs_cum, ys):.2f}; "
      f"corr(before, own size) = {corr(xs_own, ys):.2f}")
