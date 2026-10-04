"""Where in a session the engine compiles shaders (scratch, read-only).

Usage: compile_timeline.py <rgl log> [<rgl log> ...]
For each log: compile_shader lines bucketed per minute, with the nearest preceding phase marker
(scene loads and mission markers) so mid-battle compiles stand out; bursts of 3+ compiles within
50 ms are listed with their time.
"""
import re
import sys

rx_t = re.compile(r"^\[(\d\d):(\d\d):(\d\d)\.(\d+)\]")
markers = ("Loading scene", "loading scene", "Mission", "mission", "scene_loaded", "Scene loaded",
           "SceneObj", "Loading xml file: $BASE/Modules/Native//ModuleData/water_prefabs.xml")


def ts(line):
    m = rx_t.match(line)
    if not m:
        return None
    h, mi, s, ms = m.groups()
    return int(h) * 3600 + int(mi) * 60 + int(s) + int(ms[:3].ljust(3, "0")) / 1000


for path in sys.argv[1:]:
    print(f"== {path.split('/')[-1]}")
    comp = []
    last_marker = ""
    first = None
    for ln in open(path, encoding="utf-8", errors="replace"):
        t = ts(ln)
        if t is None:
            continue
        if first is None:
            first = t
        if "compile_shader:" in ln:
            src = ln.split("Sources/")[-1].split(",")[0] if "Sources/" in ln else "?"
            comp.append((t, src, last_marker))
        elif any(k in ln for k in ("Loading scene", "scene:", "Scene:", "OpenNew", "mission_", "Mission state")):
            last_marker = ln.strip()[15:90]
    print(f"   compiles: {len(comp)}")
    if not comp:
        continue
    buckets = {}
    for t, src, mk in comp:
        key = int((t - first) // 60)
        buckets.setdefault(key, []).append((t, src, mk))
    for k in sorted(buckets):
        b = buckets[k]
        srcs = {}
        for _, s, _ in b:
            srcs[s] = srcs.get(s, 0) + 1
        top = ", ".join(f"{s} {n}" for s, n in sorted(srcs.items(), key=lambda kv: -kv[1])[:4])
        print(f"   minute +{k}: {len(b)} compiles ({top}); last marker before: {b[-1][2][:70]!r}")
    bursts = []
    i = 0
    while i < len(comp):
        j = i
        while j + 1 < len(comp) and comp[j + 1][0] - comp[i][0] <= 0.05:
            j += 1
        if j - i + 1 >= 3:
            bursts.append((comp[i][0], j - i + 1))
        i = j + 1
    print(f"   bursts of 3+ within 50 ms: {len(bursts)}")
