"""Hitch windows against working set and memory pressure (scratch, read-only).

Usage: hitch_ws.py <taom_debug.log> [threshold_ms]
Prints every [MissionPerf] window whose maxMs is at least the threshold (default 300), with the
[MemSample] lines in the 25 s around it (wsMB, privMB, availPhysMB, memLoad), so a working-set jump
(pages faulted back in) or a pressure spike next to a hitch is visible.
"""
import re
import sys
from datetime import datetime, timedelta

path = sys.argv[1]
thr = float(sys.argv[2]) if len(sys.argv) > 2 else 300.0
ts_re = re.compile(r"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\]")
perf_re = re.compile(r"\[MissionPerf\] t=\+(\d+)s frames=(\d+) fps=([\d.]+) avgMs=([\d.]+) p95Ms=([\d.]+) maxMs=([\d.]+) agents=(\d+)")
mem_re = re.compile(r"\[MemSample\] privMB=(\d+) wsMB=(\d+) heapMB=(\d+).*?availPhysMB=(\d+) memLoad=(\d+)%")
perf, mem = [], []
for line in open(path, encoding="utf-8", errors="replace"):
    m = ts_re.match(line)
    if not m:
        continue
    ts = datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
    p = perf_re.search(line)
    if p:
        perf.append((ts, int(p.group(1)), float(p.group(6)), float(p.group(5)), int(p.group(7))))
        continue
    q = mem_re.search(line)
    if q:
        mem.append((ts, int(q.group(1)), int(q.group(2)), int(q.group(3)), int(q.group(4)), int(q.group(5))))

print(f"{path.split(chr(92))[-1]}: {len(perf)} perf windows, {len(mem)} mem samples, threshold {thr} ms")
calm_ws_deltas = []
for i in range(1, len(mem)):
    calm_ws_deltas.append(mem[i][2] - mem[i - 1][2])
for ts, t, mx, p95, agents in perf:
    if mx < thr:
        continue
    print(f"HITCH {ts:%H:%M:%S} t=+{t}s maxMs={mx} p95={p95} agents={agents}")
    for ms in mem:
        if ts - timedelta(seconds=15) <= ms[0] <= ts + timedelta(seconds=10):
            print(f"   mem {ms[0]:%H:%M:%S} priv={ms[1]} ws={ms[2]} heap={ms[3]} availPhys={ms[4]} load={ms[5]}%")
if calm_ws_deltas:
    s = sorted(calm_ws_deltas)
    print(f"all 10 s working-set deltas: min {s[0]}, median {s[len(s)//2]}, max {s[-1]} MB over {len(s)} steps")
