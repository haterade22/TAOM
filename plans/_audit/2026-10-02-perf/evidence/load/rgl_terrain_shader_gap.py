"""Time from the campaign map's terrain shader setup to the next real step, per rgl log (scratch, read-only).

For each rgl_log_*.txt, finds every "rglTerrain_shader_generator::clear" line, then reports the time to
the first later line that is not a "Missing shader from sack" line, plus how many such lines came
between, and whether the compressed sack read took measurable time.
"""
import glob
import os
import re
from datetime import datetime

LOGS = r"C:\ProgramData\Mount and Blade II Bannerlord\logs"
ts_re = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d{3})\] (.*)")
fmt = "%H:%M:%S.%f"
for path in sorted(glob.glob(os.path.join(LOGS, "rgl_log_[0-9]*.txt")), key=os.path.getmtime):
    rows = []
    for raw in open(path, encoding="utf-8", errors="replace"):
        m = ts_re.match(raw.rstrip("\n"))
        if m:
            rows.append((datetime.strptime(m.group(1), fmt), m.group(2).strip()))
    for i, (ts, text) in enumerate(rows):
        if text != "rglTerrain_shader_generator::clear":
            continue
        sack = next((r[1] for r in rows[i:i + 5] if "read_compressed_shader_cache_package" in r[1]), "")
        j, missing = i + 1, 0
        while j < len(rows) and (rows[j][1].startswith("Missing shader from sack") or "read_compressed_shader_cache_package" in rows[j][1]):
            missing += rows[j][1].startswith("Missing shader from sack")
            j += 1
        if j < len(rows):
            gap = (rows[j][0] - ts).total_seconds()
            print(f"{os.path.basename(path)} {ts:%H:%M:%S} terrain setup to next step {gap:6.3f} s, "
                  f"{missing} missing-from-sack lines; sack read: {sack[-30:]}; next: {rows[j][1][:70]}")
