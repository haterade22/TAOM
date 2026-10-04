"""Scan every taom_debug log for inventory-screen opens: stall length and heap jump (scratch, read-only).

For each 'STATE push InventoryState' line, report the gap to the next line written by the main thread
(the first TableauDiag/TooltipProbe/MemStation/MapLoad line after it; MemSample is written by a timer
thread, so it is skipped as a stall marker), and the heapMB on the MemStation enter line and on the first
MemSample at least 15 s later.
"""
import glob
import os
import re
from datetime import datetime

LOGS = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs"
ts_re = re.compile(r"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\]")
heap_re = re.compile(r"heapMB=(\d+)")
priv_re = re.compile(r"privMB=(\d+)")

rows = []
for path in sorted(glob.glob(os.path.join(LOGS, "taom_debug_*.log")), key=os.path.getmtime):
    try:
        lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    except OSError:
        continue
    for i, line in enumerate(lines):
        if "STATE push InventoryState" not in line:
            continue
        m = ts_re.match(line)
        if not m:
            continue
        t0 = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
        enter_heap = enter_priv = None
        for j in range(max(0, i - 4), i):
            if "[MemStation] enter screen='GauntletInventoryScreen'" in lines[j]:
                h = heap_re.search(lines[j]); p = priv_re.search(lines[j])
                enter_heap = int(h.group(1)) if h else None
                enter_priv = int(p.group(1)) if p else None
        gap = None
        nxt = ""
        for j in range(i + 1, min(len(lines), i + 400)):
            mj = ts_re.match(lines[j])
            if not mj or "[MemSample]" in lines[j] or "[AutoResolve]" in lines[j]:
                continue
            gap = (datetime.strptime(mj.group(1), "%Y-%m-%d %H:%M:%S") - t0).total_seconds()
            nxt = lines[j][22:110]
            break
        later_heap = later_priv = None
        for j in range(i + 1, min(len(lines), i + 2000)):
            if "[MemSample]" not in lines[j]:
                continue
            mj = ts_re.match(lines[j])
            if not mj:
                continue
            if (datetime.strptime(mj.group(1), "%Y-%m-%d %H:%M:%S") - t0).total_seconds() >= 15:
                h = heap_re.search(lines[j]); p = priv_re.search(lines[j])
                later_heap = int(h.group(1)) if h else None
                later_priv = int(p.group(1)) if p else None
                break
        rows.append((os.path.basename(path), t0, gap, nxt, enter_heap, later_heap, enter_priv, later_priv))

print(f"{len(rows)} inventory opens")
for name, t0, gap, nxt, eh, lh, ep, lp in rows:
    print(f"{t0:%Y-%m-%d %H:%M:%S} gap={gap if gap is not None else '?':>5}s heap {eh}->{lh} priv {ep}->{lp} next: {nxt}")
