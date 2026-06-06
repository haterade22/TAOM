#!/usr/bin/env python3
"""
spider_render_triage.py - one-command triage of the Giant Spider PreloadForRendering AV.

Replaces the by-hand log archaeology we ran every spawn test: auto-finds the latest
TAOM debug log, the latest native rgl_log (ProgramData), and the latest crash dump,
then summarizes the spider signal and prints a VERDICT.

What it reports:
  - spider spawn intercepts vs fail-opens (= native AVs caught by the HPCSE guard)
  - whether the spider ever rendered + acted (move/bite/HIT diag lines)
  - the debug-log crash tail
  - native rgl_log signal: missing-shader summary, the formation-crash signature,
    shader-compile activity, the final native lines
  - the managed exception type + spider frames from the newest crash dump

Usage:
  python tools/spider_render_triage.py
  python tools/spider_render_triage.py --game "E:/Steam/.../Mount & Blade II Bannerlord"
  python tools/spider_render_triage.py --programdata "C:/ProgramData/Mount and Blade II Bannerlord/logs"

Pure stdlib. Read-only. Fail-soft: a missing path is reported, never fatal.
"""
import argparse
import glob
import os

DEFAULT_GAME = r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord"
DEFAULT_PROGRAMDATA = r"C:/ProgramData/Mount and Blade II Bannerlord/logs"


def newest(pattern, exclude=None):
    files = [f for f in glob.glob(pattern) if not (exclude and exclude in os.path.basename(f))]
    return max(files, key=os.path.getmtime) if files else None


def read_lines(path):
    try:
        with open(path, "r", errors="replace") as f:
            return f.readlines()
    except Exception:
        return []


def count_substr(lines, needle):
    return sum(1 for ln in lines if needle in ln)


def distinct_after(lines, prefix, top=8):
    """Count distinct tokens following `prefix` (e.g. 'Missing shader from sack: ')."""
    counts = {}
    for ln in lines:
        i = ln.find(prefix)
        if i >= 0:
            tok = ln[i + len(prefix):].strip().split()[0] if ln[i + len(prefix):].strip() else "?"
            counts[tok] = counts.get(tok, 0) + 1
    return sorted(counts.items(), key=lambda kv: -kv[1])[:top]


def section(title):
    print("\n=== %s ===" % title)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--game", default=DEFAULT_GAME)
    ap.add_argument("--programdata", default=DEFAULT_PROGRAMDATA)
    args = ap.parse_args()

    logs_dir = os.path.join(args.game, "bin", "Win64_Shipping_Client", "Logs")
    debug_log = newest(os.path.join(logs_dir, "taom_debug_*.log"))
    rgl_log = newest(os.path.join(args.programdata, "rgl_log_*.txt"), exclude="errors")
    crash_dirs = sorted(glob.glob(os.path.join(logs_dir, "taom_crash_*")),
                        key=os.path.getmtime, reverse=True)
    crash_dir = next((d for d in crash_dirs if os.path.isdir(d)), None)

    intercepts = failopens = 0
    advancing = bites = hits = 0
    debug_tail = []
    if debug_log:
        dl = read_lines(debug_log)
        intercepts = count_substr(dl, "intercept char=taom_spider")
        failopens = count_substr(dl, "Spawning humanoid anchor instead")
        advancing = count_substr(dl, "move: ADVANCING")
        bites = count_substr(dl, "bite: CustomAttack")
        hits = count_substr(dl, "HIT: bite connected")
        debug_tail = [ln.rstrip() for ln in dl[-10:]]

    section("TAOM debug log")
    print("file: %s" % (os.path.basename(debug_log) if debug_log else "NONE FOUND"))
    print("spider intercepts: %d   fail-opens (native AV caught): %d" % (intercepts, failopens))
    print("rendered+acted?  move:ADVANCING=%d  bite=%d  HIT=%d" % (advancing, bites, hits))
    if debug_tail:
        print("crash tail:")
        for ln in debug_tail:
            print("   " + ln)

    rgl_tail = []
    missing = []
    formation_invalid = 0
    compiles = 0
    if rgl_log:
        rl = read_lines(rgl_log)
        missing = distinct_after(rl, "Missing shader from sack: ")
        formation_invalid = count_substr(rl, "Formation order position is not valid")
        compiles = sum(1 for ln in rl if ("compil" in ln.lower() or "generating shader" in ln.lower()))
        rgl_tail = [ln.rstrip() for ln in rl[-8:]]

    section("native rgl_log")
    print("file: %s" % (os.path.basename(rgl_log) if rgl_log else "NONE FOUND"))
    if missing:
        print("missing-shader-from-sack (top): " + ", ".join("%s x%d" % (n, c) for n, c in missing))
    print("shader-compile activity: %d lines    'Formation order position is not valid': %d" % (compiles, formation_invalid))
    if rgl_tail:
        print("native tail:")
        for ln in rgl_tail:
            print("   " + ln)

    section("newest crash dump")
    exc = ""
    if crash_dir:
        print("dir: %s" % os.path.basename(crash_dir))
        rpt = newest(os.path.join(crash_dir, "*.txt"))
        if rpt:
            for ln in read_lines(rpt):
                low = ln.lower()
                if ("exception" in low and "::" not in ln) or "preloadforrendering" in low or "accessviolation" in low:
                    exc = exc or ln.strip()
                    print("   " + ln.strip())
    else:
        print("none (straight-to-desktop crashes leave no dump)")

    section("VERDICT")
    if intercepts == 0:
        print("No spider spawn this run (intercepts=0). Did the spider get into the battle?")
    elif failopens >= intercepts and failopens > 0:
        v = "Spider AV'd in PreloadForRendering %d/%d times -> fail-open caught each." % (failopens, intercepts)
        if formation_invalid:
            v += " Game then FORMATION-crashed to desktop (fallback humanoids)."
        v += " RENDER AV UNRESOLVED."
        print(v)
    elif advancing or bites or hits:
        print("Spider RENDERED and acted (move/bite/HIT fired, AVs=%d). Likely SUCCESS." % failopens)
    else:
        print("Spider intercepted %d, fail-opens %d, no act-diag. Inspect tails above." % (intercepts, failopens))


if __name__ == "__main__":
    main()
