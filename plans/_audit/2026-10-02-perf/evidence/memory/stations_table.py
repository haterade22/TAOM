"""Markdown tables of [MemStation] screen transitions across taom_debug logs (scratch, read-only).

Usage: stations_table.py <log-or-glob> [...]
Prints, per screen of interest, one row per enter with the private/heap MB at enter and at the matching
exit, plus the elapsed seconds. Screens: GauntletInitialScreen, GameLoadingScreen, GauntletInventoryScreen,
GauntletPartyScreen, GauntletClanScreen, GauntletCharacterDeveloperScreen.
"""
import glob
import os
import re
import sys
from datetime import datetime

ts_re = re.compile(r"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\]")
st_re = re.compile(r"\[MemStation\] (enter|exit) screen='([A-Za-z]+)' privMB=(\d+) wsMB=(\d+) heapMB=(\d+)")
SCREENS = ["GauntletInitialScreen", "GameLoadingScreen", "GauntletInventoryScreen", "GauntletPartyScreen",
           "GauntletClanScreen", "GauntletCharacterDeveloperScreen"]

paths = []
for a in sys.argv[1:]:
    paths.extend(glob.glob(a))
rows = {s: [] for s in SCREENS}
for p in sorted(set(paths), key=os.path.getmtime):
    open_at = {}
    for raw in open(p, encoding="utf-8", errors="replace"):
        if "[CrashReport]" in raw:
            continue
        m = ts_re.match(raw)
        s = st_re.search(raw)
        if not m or not s:
            continue
        ts = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
        kind, screen, priv, ws, heap = s.group(1), s.group(2), int(s.group(3)), int(s.group(4)), int(s.group(5))
        if screen not in rows:
            continue
        if kind == "enter":
            open_at[screen] = (ts, priv, heap)
            rows[screen].append([os.path.basename(p), ts, priv, heap, None, None, None])
        elif screen in open_at:
            t0, _, _ = open_at.pop(screen)
            for r in reversed(rows[screen]):
                if r[1] == t0:
                    r[4], r[5], r[6] = priv, heap, (ts - t0).total_seconds()
                    break

for screen in SCREENS:
    if not rows[screen]:
        continue
    print(f"\n### {screen}\n")
    print("| Log | Enter | privMB enter | heapMB enter | privMB exit | heapMB exit | Open s |")
    print("|---|---|---|---|---|---|---|")
    for log, ts, p0, h0, p1, h1, secs in rows[screen]:
        print(f"| `{log}` | {ts:%m-%d %H:%M:%S} | {p0:,} | {h0:,} | "
              f"{'' if p1 is None else f'{p1:,}'} | {'' if h1 is None else f'{h1:,}'} | {'' if secs is None else int(secs)} |")
