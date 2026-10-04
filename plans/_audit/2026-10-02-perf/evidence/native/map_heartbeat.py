"""Campaign-map frame rate and campaign tick cost from the [MapLoad] heartbeat (scratch, read-only).

Keeps heartbeat windows of 4 to 6 s with the map on top and no loading window, groups them by time
control (Stop, Play, fast forward), and prints per group: windows, fps median / p10, tickMs median / p90 /
max, and the party count range. Also prints the same per session for the running-time groups.
"""
import glob
import os
import re
import statistics

LOGS = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs"
hb = re.compile(r"\[MapLoad\] t=\+(\d+)s frames=(\d+) fps=([\d.]+) tickMs=([\d.]+) parties=(\d+).*?"
                r"loadingWindow=(\w+) timeControl=(\w+) topScreen=(\w+)")
groups = {}
per_session = {}
for path in sorted(glob.glob(os.path.join(LOGS, "taom_debug_2026-*.log")), key=os.path.getmtime):
    prev_t = None
    for line in open(path, encoding="utf-8", errors="replace"):
        m = hb.search(line)
        if not m:
            continue
        t, frames, fps, tick, parties, loading, tc, top = m.groups()
        t = int(t)
        dt = None if prev_t is None else t - prev_t
        prev_t = t
        if dt is None or not (4 <= dt <= 6) or loading != "False" or top != "MapScreen":
            continue
        key = "Stop" if tc == "Stop" else ("Play" if tc == "Play" else "FastForward")
        row = (float(fps), float(tick), int(parties))
        groups.setdefault(key, []).append(row)
        per_session.setdefault((os.path.basename(path)[11:30], key), []).append(row)


def pct(xs, p):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(round(p * (len(xs) - 1))))]


def describe(rows):
    fps = [r[0] for r in rows]
    tick = [r[1] for r in rows]
    parties = [r[2] for r in rows]
    return (f"windows={len(rows):4d} fps med {statistics.median(fps):6.1f} p10 {pct(fps, 0.10):6.1f} | "
            f"tickMs med {statistics.median(tick):5.2f} p90 {pct(tick, 0.90):5.2f} max {max(tick):6.2f} | "
            f"parties {min(parties)}-{max(parties)}")


for key in ("Stop", "Play", "FastForward"):
    if key in groups:
        print(f"{key:12} {describe(groups[key])}")
print()
for (session, key), rows in sorted(per_session.items()):
    if key != "Stop" and len(rows) >= 6:
        print(f"{session} {key:12} {describe(rows)}")
