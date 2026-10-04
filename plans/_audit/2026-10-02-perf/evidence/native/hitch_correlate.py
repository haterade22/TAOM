"""Correlate [MissionPerf] hitch windows with other timed log lines (scratch analysis, read-only).

Usage: hitch_correlate.py <taom_debug.log> [threshold_ms]
For every [MissionPerf] window whose maxMs exceeds the threshold, the window covers the 5 s of wall clock
ending at its own timestamp. Lists which tags appear inside each hitch window, and for comparison how often
each tag appears inside non-hitch windows.
"""
import re
import sys
from collections import Counter
from datetime import datetime, timedelta

path = sys.argv[1]
thr = float(sys.argv[2]) if len(sys.argv) > 2 else 400.0
line_re = re.compile(r"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\] \[(\w+)\] (\[[^\]]+\])?(.*)$")
perf_re = re.compile(r"maxMs=([\d.]+)")
events = []
windows = []
for raw in open(path, encoding="utf-8", errors="replace"):
    m = line_re.match(raw.rstrip("\n"))
    if not m:
        continue
    ts = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
    tag = (m.group(3) or "").strip()
    tag = re.sub(r"#\d+", "#", tag)
    if tag == "[MissionPerf]":
        pm = perf_re.search(m.group(4))
        if pm:
            windows.append((ts, float(pm.group(1)), m.group(4)[:150]))
    else:
        events.append((ts, tag, m.group(2)))

hit_tags, calm_tags = Counter(), Counter()
n_hit = n_calm = 0
for i, (ts, mx, txt) in enumerate(windows):
    start = ts - timedelta(seconds=5)
    inside = [e for e in events if start < e[0] <= ts]
    tags = Counter(e[1] for e in inside)
    if mx > thr:
        n_hit += 1
        hit_tags.update(set(tags))
        print(f"HITCH {ts} maxMs={mx:.1f}: " + ", ".join(f"{k}x{v}" for k, v in tags.most_common(12)))
    else:
        n_calm += 1
        calm_tags.update(set(tags))
print(f"\n{n_hit} hitch windows, {n_calm} calm windows (threshold {thr} ms)")
print("tag | share of hitch windows | share of calm windows")
for tag in sorted(set(hit_tags) | set(calm_tags), key=lambda t: -(hit_tags[t] / max(n_hit, 1))):
    print(f"{tag or '(untagged)'} | {hit_tags[tag]}/{n_hit} | {calm_tags[tag]}/{n_calm}")
