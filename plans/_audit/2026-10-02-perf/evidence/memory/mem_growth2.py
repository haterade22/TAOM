"""Split [MemSample] private-memory growth by the screen on top (scratch analysis, read-only).

Usage: mem_growth2.py <taom_debug.log> [...]
Tracks the top screen from [MemStation] enter/exit lines (a stack), then reports, per contiguous
segment on one top screen lasting >= 3 minutes, the start/end private MB, heap MB, duration and
rate. Also lists the notable events inside each MapScreen segment (screens pushed, saves, tableau
and tooltip diagnostics, warnings) so a growth span can be attributed to what the player did.
"""
import re
import sys
from datetime import datetime

ts_re = re.compile(r"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\] \[([A-Z]+)\] \[([A-Za-z]+)\]")
mem_re = re.compile(r"\[MemSample\] privMB=(\d+) wsMB=(\d+) heapMB=(\d+).*?availPhysMB=(\d+) memLoad=(\d+)%")
station_re = re.compile(r"\[MemStation\] (enter|exit) screen='([A-Za-z]+)' privMB=(\d+) wsMB=(\d+) heapMB=(\d+)")
BORING = {"MemSample", "MemStation", "MapLoad"}


def analyse(path):
    stack = []
    segs = []  # [screen, start_ts, samples[], events[]]
    cur = None

    def top():
        return stack[-1] if stack else "none"

    def open_seg(ts):
        nonlocal cur
        cur = [top(), ts, [], []]
        segs.append(cur)

    for raw in open(path, encoding="utf-8", errors="replace"):
        m = ts_re.match(raw)
        if not m:
            continue
        ts = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
        level, tag = m.group(2), m.group(3)
        if cur is None:
            open_seg(ts)
        st = station_re.search(raw)
        if st:
            kind, screen = st.group(1), st.group(2)
            priv, heap = int(st.group(3)), int(st.group(5))
            cur[2].append((ts, priv, heap, None, None))
            if kind == "enter":
                stack.append(screen)
            else:
                if screen in stack:
                    # remove the last occurrence
                    idx = len(stack) - 1 - stack[::-1].index(screen)
                    stack.pop(idx)
            if cur[0] != top():
                open_seg(ts)
                cur[2].append((ts, priv, heap, None, None))
            continue
        mm = mem_re.search(raw)
        if mm:
            cur[2].append((ts, int(mm.group(1)), int(mm.group(3)), int(mm.group(4)), int(mm.group(5))))
            continue
        if tag not in BORING and (level in ("WARNING", "ERROR") or tag in ("SaveLoad", "TableauDiag", "TooltipProbe", "MissionDiag", "BattleLoad", "CultureMarketplace", "RealmBorders")):
            msg = raw.strip()[22:150]
            cur[3].append((ts, msg))

    print(f"== {path.replace(chr(92), '/').split('/')[-1]}")
    for screen, start, samples, events in segs:
        if len(samples) < 2:
            continue
        mins = (samples[-1][0] - samples[0][0]).total_seconds() / 60
        if mins < 3:
            continue
        d = samples[-1][1] - samples[0][1]
        dh = samples[-1][2] - samples[0][2]
        peak = max(s[1] for s in samples)
        lows = [s[3] for s in samples if s[3] is not None]
        loads = [s[4] for s in samples if s[4] is not None]
        print(f"   {screen:<26} {samples[0][0].strftime('%H:%M')}-{samples[-1][0].strftime('%H:%M')} ({mins:4.0f} min): "
              f"priv {samples[0][1]:>6} -> {samples[-1][1]:>6} MB ({d:+6d}, {d / mins:+5.0f} MB/min, peak {peak}); heap {dh:+5d} MB"
              + (f"; availPhys min {min(lows)} MB, memLoad max {max(loads)}%" if lows else ""))
        if screen == "MapScreen":
            # growth shape: per-10-minute deltas
            marks = []
            base = samples[0]
            for s in samples[1:]:
                if (s[0] - base[0]).total_seconds() >= 300:
                    marks.append(f"{base[0].strftime('%H:%M')}->{s[0].strftime('%H:%M')} {s[1] - base[1]:+d}")
                    base = s
            if marks:
                print("      5-min steps: " + ", ".join(marks))
            seen = {}
            for ts, msg in events:
                key = msg[:60]
                seen.setdefault(key, [0, ts, msg])
                seen[key][0] += 1
            for key, (n, ts, msg) in list(seen.items())[:25]:
                print(f"      {ts.strftime('%H:%M:%S')} x{n} {msg}")


for p in sys.argv[1:]:
    analyse(p)
