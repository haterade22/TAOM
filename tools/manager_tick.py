"""Start one /manage tick in a headless session when the PC is idle (ADR-015, phase B).

A Windows scheduled task runs this every 30 minutes and after a reboot (docs/features/project-manager.md, "Unattended ticks"):

    python tools/manager_tick.py --worktree E:/repos/taom-manager            report the gate, start nothing
    python tools/manager_tick.py --worktree E:/repos/taom-manager --run      start one tick when the gate passes

The gate refuses while any of these holds: the STOP file exists, a usage-limit pause has not ended, the PC had keyboard or mouse input in the
last --idle-minutes (default 10), Bannerlord runs, another session holds the manager-tick claim, or the worktree is missing or has uncommitted
changes. When the gate passes, the launcher fetches the trunk and detaches the worktree at it, so the tick reads the trunk's skills. An unreadable input
age, process list or claim store refuses: it is never read as a pass. While the maintainer is active, nothing starts.

The tick runs as `claude -p "/manage tick" --permission-mode default --output-format json` in the worktree. A headless session starts in the
project's default mode (plan) unless the mode is given, and in default mode a hook ask becomes a denial, never a wait (probe, 2026-11-18).
State lives in <git common dir>/taom-manager/: STOP (made by the maintainer), PAUSE (an epoch second, written after a usage limit),
launcher.log (one line per decision) and last-tick.json (the session's result).

Exit codes: 0 a tick ran and ended, or a report with no --run; 1 the tick session failed; 2 usage; 3 the gate refused.
"""

import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import run_fords_autorun as autorun  # noqa: E402
import work_claims as wc  # noqa: E402

REFUSED = 3
FAILED = 1
PAUSE_SECONDS = 3600
TICK_TIMEOUT_SECONDS = 6 * 3600
TRUNK = "bannerlord-1.5.x"
GAME_PROCESSES =["Bannerlord.exe", "Bannerlord.Native.exe", "Bannerlord_BE.exe"]


def decide(stop, pause_until, now, idle_seconds, idle_minutes, game_running, tick_held, worktree_ok, worktree_clean):
    """(start, reason). Each check fails closed: None means unknown, and unknown refuses."""
    if stop:
        return False, "the STOP file exists"
    if pause_until > now:
        return False, f"a usage limit paused ticks until {time.strftime('%Y-%m-%d %H:%M', time.localtime(pause_until))}"
    if not worktree_ok:
        return False, "the tick worktree is missing"
    if worktree_clean is not True:
        return False, "the tick worktree has uncommitted changes (or git could not say), so it is not moved to the trunk"
    if not autorun.idle_ok(idle_seconds, idle_minutes):
        return False, "not idle: " + autorun.idle_state_text(idle_seconds, idle_minutes)
    if game_running is not False:
        return False, "Bannerlord runs" if game_running else "Bannerlord state unknown (the process list failed)"
    if tick_held is not False:
        return False, "another session holds the manager-tick claim" if tick_held else "the manager-tick claim is unknown (no claim store)"
    return True, "every check passed"


def usage_limited(result_text):
    """True when the session's JSON result is an error about a usage limit."""
    try:
        result = json.loads(result_text)
    except (ValueError, TypeError):
        result = {"is_error": True, "result": str(result_text)}
    if not isinstance(result, dict) or not result.get("is_error"):
        return False
    return "usage limit" in str(result.get("result", "")).lower()


def tick_command(claude, variant):
    return [claude, "-p", f"/manage {variant}", "--permission-mode", "default", "--output-format", "json"]


def read_pause(state, now=None):
    path = Path(state) / "PAUSE"
    if not path.exists():
        return 0
    try:
        return int(path.read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return (int(time.time()) if now is None else now) + PAUSE_SECONDS


def write_pause(state, until):
    Path(state).mkdir(parents=True, exist_ok=True)
    (Path(state) / "PAUSE").write_text(str(int(until)), encoding="utf-8")


def state_dir(worktree):
    out = subprocess.run(["git", "-C", worktree, "rev-parse", "--path-format=absolute", "--git-common-dir"],
                         capture_output=True, text=True, timeout=10, check=False)
    common = out.stdout.strip() if out.returncode == 0 else ""
    return Path(common) / "taom-manager" if common else None


def gather(worktree, state, idle_minutes):
    now = int(time.time())
    idle_seconds, _ = autorun.current_idle_seconds()
    try:
        game_running = bool(autorun.list_processes(GAME_PROCESSES))
    except (OSError, ValueError, subprocess.SubprocessError):
        game_running = None
    store = wc.default_store()
    if store is None:
        tick_held = None
    else:
        nobody = wc.Me(session_id="", pid=0, name="", worktree=worktree, branch="")
        tick_held = wc.holder("manager-tick", nobody, store, wc.default_registry()) is not None
    worktree_ok = Path(worktree, ".git").exists()
    return dict(stop=(Path(state) / "STOP").exists(), pause_until=read_pause(state, now), now=now, idle_seconds=idle_seconds,
                idle_minutes=idle_minutes, game_running=game_running, tick_held=tick_held, worktree_ok=worktree_ok,
                worktree_clean=is_clean(worktree) if worktree_ok else None)


def _git(worktree, *args):
    return subprocess.run(["git", "-C", worktree, *args], capture_output=True, text=True, timeout=120, check=False)


def is_clean(worktree):
    """True when git status lists nothing, False when it lists something, None when git fails."""
    try:
        out = _git(worktree, "--no-optional-locks", "status", "--porcelain", "--untracked-files=normal")
    except (OSError, subprocess.SubprocessError):
        return None
    return None if out.returncode != 0 else out.stdout.strip() == ""


def refresh(worktree):
    """(ok, why): fetch the trunk and detach the tick worktree at it, so the tick reads the trunk's skills. A failed fetch moves nothing."""
    for args in (["fetch", "origin", TRUNK], ["checkout", "--detach", "FETCH_HEAD"]):
        try:
            out = _git(worktree, *args)
        except (OSError, subprocess.SubprocessError) as error:
            return False, f"git {args[0]}: {error}"
        if out.returncode != 0:
            return False, f"git {args[0]} exited {out.returncode}: {(out.stderr or '').strip()[:200]}"
    return True, ""


def run_tick(cmd, worktree):
    try:
        done = subprocess.run(cmd, cwd=worktree, capture_output=True, text=True, encoding="utf-8", errors="replace",
                              timeout=TICK_TIMEOUT_SECONDS, check=False)
    except subprocess.TimeoutExpired:
        return FAILED, '{"is_error": true, "result": "the tick ran past its time limit"}'
    except OSError as error:
        return FAILED, json.dumps({"is_error": True, "result": f"could not start the tick: {error}"})
    return done.returncode, done.stdout


def log(state, text):
    Path(state).mkdir(parents=True, exist_ok=True)
    with open(Path(state) / "launcher.log", "a", encoding="utf-8") as f:
        f.write(f"{time.strftime('%Y-%m-%d %H:%M:%S')} {text}\n")


def main(argv=None):
    parser = argparse.ArgumentParser(description="Start one /manage tick when the PC is idle (ADR-015 phase B).")
    parser.add_argument("--worktree", required=True, help="the checkout the tick runs in")
    parser.add_argument("--run", action="store_true", help="start the tick when the gate passes; without it, report only")
    parser.add_argument("--dry-tick", action="store_true", help="start /manage tick --dry in place of a full tick")
    parser.add_argument("--idle-minutes", type=autorun.non_negative_minutes, default=10.0)
    parser.add_argument("--claude", default="claude", help="the CLI to start")
    args = parser.parse_args(argv)

    state = state_dir(args.worktree)
    if state is None:
        print(f"manager_tick: {args.worktree} is not a git checkout", file=sys.stderr)
        return 2
    start, reason = decide(**gather(args.worktree, state, args.idle_minutes))
    if not args.run:
        print(("would start: " if start else "would refuse: ") + reason)
        return 0
    if not start:
        log(state, "refused: " + reason)
        return REFUSED
    moved, why = refresh(args.worktree)
    if not moved:
        log(state, "refused: the trunk refresh failed: " + why)
        return REFUSED
    variant = "tick --dry" if args.dry_tick else "tick"
    log(state, f"started: /manage {variant} in {args.worktree}")
    code, out = run_tick(tick_command(args.claude, variant), args.worktree)
    (Path(state) / "last-tick.json").write_text(out or "", encoding="utf-8")
    if usage_limited(out):
        write_pause(state, int(time.time()) + PAUSE_SECONDS)
        log(state, f"ended: usage limit, ticks paused for {PAUSE_SECONDS // 60} minutes")
        return FAILED
    log(state, f"ended: exit {code}")
    return 0 if code == 0 else FAILED


if __name__ == "__main__":
    sys.exit(main())
