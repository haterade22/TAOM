"""Campaign-map memory drift after the entry plateau, across every taom_debug log (scratch, read-only).

For each contiguous MapScreen-on-top segment of >= 10 minutes (screen stack from [MemStation]
enter/exit lines; a mission, inventory or other screen on top ends the segment), report the private
and heap MB at entry, at entry+5 min (the end of map streaming), and at the end, and the drift rate
from minute 5 to the end. A steady leak shows as a positive drift that does not flatten.
"""
import glob
import os
import re
from datetime import datetime

LOGS = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs"
ts_re = re.compile(r"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\]")
mem_re = re.compile(r"\[MemSample\] privMB=(\d+) wsMB=(\d+) heapMB=(\d+)")
station_re = re.compile(r"\[MemStation\] (enter|exit) screen='([A-Za-z]+)'")


def segments(path):
    stack, segs, cur = [], [], None
    for raw in open(path, encoding="utf-8", errors="replace"):
        m = ts_re.match(raw)
        if not m:
            continue
        ts = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
        st = station_re.search(raw)
        if st:
            kind, screen = st.groups()
            if kind == "enter":
                stack.append(screen)
            elif screen in stack:
                idx = len(stack) - 1 - stack[::-1].index(screen)
                stack.pop(idx)
            top = stack[-1] if stack else "none"
            if cur is None or cur[0] != top:
                cur = [top, []]
                segs.append(cur)
            continue
        mm = mem_re.search(raw)
        if mm and cur is not None:
            cur[1].append((ts, int(mm.group(1)), int(mm.group(3))))
    return segs


total = 0
for path in sorted(glob.glob(os.path.join(LOGS, "taom_debug_2026-*.log")), key=os.path.getmtime):
    try:
        segs = segments(path)
    except OSError:
        continue
    for screen, samples in segs:
        if screen != "MapScreen" or len(samples) < 2:
            continue
        mins = (samples[-1][0] - samples[0][0]).total_seconds() / 60
        if mins < 10:
            continue
        t5 = next((s for s in samples if (s[0] - samples[0][0]).total_seconds() >= 300), None)
        if t5 is None:
            continue
        rest = (samples[-1][0] - t5[0]).total_seconds() / 60
        drift = (samples[-1][1] - t5[1]) / rest if rest > 0 else 0
        hdrift = (samples[-1][2] - t5[2]) / rest if rest > 0 else 0
        peak = max(s[1] for s in samples)
        total += 1
        print(f"{os.path.basename(path)[11:30]} {samples[0][0]:%H:%M}-{samples[-1][0]:%H:%M} ({mins:4.0f} min) "
              f"priv {samples[0][1]:>6} | +5min {t5[1]:>6} | end {samples[-1][1]:>6} (peak {peak}); "
              f"drift after 5 min {drift:+6.1f} MB/min priv, {hdrift:+5.1f} MB/min heap over {rest:.0f} min")
print(f"{total} MapScreen segments of 10+ minutes")
