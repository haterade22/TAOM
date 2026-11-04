#!/usr/bin/env python
"""Run The Long Defeat (#1079) in the real game with nobody at the keyboard, and judge its [Check] lines.

Run:  python tools/run_long_defeat.py [--fortress helms] [--waves N] [--lose] [--garrison-scale X] [--live-limit S] [--screenshots 5,60]
                                      [--require-idle-minutes N] [--runs N] [--dry-run]

Deploy the tree under test with ./build.ps1 first and check the [BuildStamp] line: a build without the autorun idles at the main menu and
the run ends as a stall. The in-game LongDefeatAutorunDriver starts the run from the main menu with the shop bot and an AI-driven main
agent, leaves it when the run ends or a limit trips, writes long_defeat_done.txt and quits.

Exit 0 needs, in order: the done reason (final, or lost_* under --lose), the JSON report (final true and wavesCleared of at least N, not
under --lose), no "spawn level WRONG" line, no [Check] fail line of this feature, a [BuildStamp] that starts with this tree's HEAD, a
pass line for every id in REQUIRED_CHECKS (a shorter list under --lose), both READ_BACK lines, as many wave pass lines as waves cleared
(not under --lose), no CrashReport block, and a game that left by itself. The best run goes under the run folder, never to the player's
own file.

This reuses the Fords runner (tools/run_fords_autorun.py): its preflight, idle gate, launch recipe, dialog handling, crash scan and log
collection. It changes the arm variable, the done and status files, the arm text and the judge, and presses Cancel on the Helm's Deep
terrain shader dialog before the Fords dialog handling. The standing permission and its gate are in docs/reference/unattended-game-runs.md.
"""
import argparse
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import ingame_verdict as iv  # noqa: E402
import run_fords_autorun as runner  # noqa: E402

ENV_VAR = "TAOM_LONGDEFEAT_AUTORUN"
DONE_FILE = "long_defeat_done.txt"
STATUS_FILE = "long_defeat_status.txt"
RUNS_ROOT = r"E:\Temp\long-defeat-runs"

# Five waves took about 460 s each in the spike's 3-wave run (1,380 s), so the live limit is mission seconds with room, and the wall limit
# sits above it. DEFAULT_WALL_LIMIT_SECONDS of the Fords runner is 2400, which a 5-wave run would trip.
DEFAULT_WALL_LIMIT = 3900
DEFAULT_LIVE_LIMIT = 3600
LOSE_GARRISON_SCALE = 0.2

CHECK_PREFIX = "taom.long-defeat."

# The check ids a full run must write, each with a row in the manifest. menu is written when the main
# menu first asks whether the option is hidden (LongDefeatMenuEntry), so it precedes the run and the unattended driver's own start reads it too.
REQUIRED_CHECKS = ("menu", "boot", "general", "muster", "keep", "orders", "hud", "wave", "no-rout", "gold", "shop", "early-start", "end", "best-run")

# A run built to lose clears no wave on purpose, so the checks that need a cleared wave, a bought pack or a horn are not asked of it.
LOSE_CHECKS = ("menu", "boot", "general", "muster", "keep", "orders", "hud", "end", "best-run")

LOSE_REASONS = ("lost_defenders", "lost_keep")

# The director's own read-back of every batch; "WRONG" means soldiers on the wrong floor or off the navmesh.
SPAWN_LEVEL_WRONG = "spawn level WRONG"

# The director reads back every batch, so a run that placed both sides wrote one ok line for each. Attackers have no other floor check
# (LongDefeatDirector.Effects.cs), so a log with no attackers line proves nothing about where they stood.
READ_BACK = ("spawn level ok: defenders", "spawn level ok: attackers")

# Helm's Deep can stop its load at this dialog: the terrain shader's vista variant fails to compile (terrain_pixel_functions.rsh:818, X4008
# raised to an error by X3129). Retry compiles the same source and fails again. Cancel lets the mission load, and the same answer works
# for a person at the keyboard. Only this variant and this error are pressed: another shader, or another error in this one, is a new fact.
SHADER_DIALOG = re.compile(r"Shaders/Sources/pbr_terrain(_gbuffer)?\.rs$")
SHADER_ERROR = "X4008"
FORDS_HANDLE_DIALOGS = runner.handle_dialogs

# Set by main() before the runner starts: the wave target the judge holds the report to, and the commit of this tree, which the log's
# [BuildStamp] must carry (a stale deploy otherwise passes on the old build's lines).
target_waves = None
tree_head = None


def check_pattern(name, result):
    return re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX + name) + r" result=" + result + r"\b")


FAIL_LINE = re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX) + r"\S+ result=fail\b")


def judge(done, report_json, log_text=None):
    """(exit code, why). The whole log counts for the CrashReport check: it opens at this launch, so every line is this run's, and a time
    cut would only hide a block from the load."""
    if not done:
        return 1, "no done file"
    reason = done.get("reason", "")
    lose = done.get("expect") == "lost"
    wrong = next((row for row in (log_text or "").splitlines() if SPAWN_LEVEL_WRONG in row), None)
    note = f"; the log holds {wrong[:400]}" if wrong else ""
    accepted = LOSE_REASONS if lose else ("final",)
    if reason not in accepted:
        return 1, f"reason={reason or '?'} (wanted {' or '.join(accepted)}){note}"
    if report_json is None:
        return 1, f"reason={reason} but the JSON report could not be read"
    if not lose:
        if report_json.get("final") is not True:
            return 1, "the JSON report is not final"
        cleared = report_json.get("wavesCleared")
        if not isinstance(cleared, int) or isinstance(cleared, bool) or (target_waves is not None and cleared < target_waves):
            return 1, f"wavesCleared={cleared!r}, wanted at least {target_waves}"
    if not log_text:
        return 1, f"reason={reason} but no TAOM log was copied, and the [Check] lines are the evidence"
    if wrong:
        return 1, f"reason={reason}" + note
    failed = FAIL_LINE.search(log_text)
    if failed:
        return 1, "the log holds a failed check: " + failed.group(0)
    build, problem = build_of(log_text)
    if problem:
        return 1, f"reason={reason} but {problem}"
    required = LOSE_CHECKS if lose else REQUIRED_CHECKS
    missing = [name for name in required if not check_pattern(name, "pass").search(log_text)]
    if missing:
        return 1, f"reason={reason} but no pass line for: " + ", ".join(CHECK_PREFIX + name for name in missing)
    unread = [line for line in READ_BACK if line not in log_text]
    if unread:
        return 1, f"reason={reason} but the log has no '{unread[0]}' line, so that side's spawn floor was never read back"
    if not lose:
        passes = len(check_pattern("wave", "pass").findall(log_text))
        if passes < cleared:
            return 1, f"the report cleared {cleared} waves but the log holds {passes} wave pass lines"
    block = runner.crash_report_line(log_text, None)
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block: " + block[:200]
    felled = report_json.get("wavesFelled", 0)
    return 0, (f"build {build}, reason={reason}, {report_json.get('wavesCleared', 0)} waves cleared, checks {', '.join(required)} passed, "
               f"no spawn level WRONG, no CrashReport block, {felled} waves ended by felling stragglers")


def build_of(log_text):
    """(build label, problem). The log's [BuildStamp] must name this tree's HEAD; .dirty is allowed and shown, because a build from the
    working tree is what a run under test is."""
    stamp = next((s for s in (iv.parse_build_stamp(row) for row in log_text.splitlines() if "[BuildStamp]" in row) if s), None)
    if stamp is None:
        return None, "the log has no [BuildStamp] line with a git sha"
    sha, dirty = stamp
    label = sha[:9] + (".dirty" if dirty else "")
    if not tree_head:
        return label, "this tree's HEAD is unknown, so the build cannot be matched"
    if not sha.startswith(tree_head):
        return label, f"the log's build {label} is not this tree's HEAD {tree_head[:9]}: deploy this tree and run again"
    return label, None


def tree_head_sha():
    """The commit of the tree this wrapper lives in, or None when git cannot say."""
    try:
        done = subprocess.run(["git", "-C", str(Path(__file__).resolve().parent.parent), "rev-parse", "HEAD"],
                              capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.SubprocessError):
        return None
    head = done.stdout.strip()
    return head if done.returncode == 0 and re.fullmatch(r"[0-9a-f]{40}", head) else None


def handle_dialogs(pid, seen, say):
    """Presses Cancel on the terrain shader's compile dialog, then hands every dialog to the Fords runner, which logs a dialog it has not
    seen and reports a failed check."""
    try:
        dialogs = runner.game_dialogs(pid)
    except (OSError, AttributeError):
        dialogs = []
    for hwnd, title, texts, buttons in dialogs:
        if SHADER_DIALOG.search(title or "") and "Cancel" in buttons and any(SHADER_ERROR in t for t in texts) and runner.click_button(hwnd, buttons["Cancel"]) and hwnd not in seen:
            seen.add(hwnd)
            say(f"pressed Cancel on the shader compile dialog '{title}': {' | '.join(t for t in texts if t)}")
    return FORDS_HANDLE_DIALOGS(pid, seen, say)


def positive_int(text):
    value = int(text)
    if value <= 0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a positive whole number")
    return value


def garrison_scale(text):
    value = float(text)
    if not 0.01 <= value <= 10.0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a number from 0.01 to 10")
    return value


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0],
                                     epilog="The Fords runner's flags also apply: --require-idle-minutes, --wall-limit, --runs, --keep-on-crash, --runs-root, --dry-run.")
    parser.add_argument("--fortress", default="helms", help="a fortress id of fortresses.json (default helms)")
    parser.add_argument("--waves", type=positive_int, default=5, help="waves to clear (default 5)")
    parser.add_argument("--lose", action="store_true",
                        help=f"a run built to lose: garrison scale {LOSE_GARRISON_SCALE}, expect=lost, judged by the shorter check list")
    parser.add_argument("--garrison-scale", type=garrison_scale, help="scale every garrison row's count (default 1; --lose sets 0.2)")
    parser.add_argument("--live-limit", type=runner.positive_seconds, default=DEFAULT_LIVE_LIMIT,
                        help=f"mission seconds before the run is ended (default {DEFAULT_LIVE_LIMIT})")
    parser.add_argument("--screenshots", help="live seconds at which the game saves a picture of the screen into the run folder, comma separated")
    return parser


def arm_pairs(known, out, wall_limit):
    scale = known.garrison_scale if known.garrison_scale is not None else (LOSE_GARRISON_SCALE if known.lose else None)
    return [("fortress", known.fortress), ("waves", known.waves), ("garrison_scale", scale), ("expect", "lost" if known.lose else None),
            ("live_limit_seconds", known.live_limit), ("wall_limit_seconds", wall_limit), ("screenshots", known.screenshots),
            ("out", out), ("quit", 1)]


def main(argv=None):
    global target_waves, tree_head
    parser = build_parser()
    known, rest = parser.parse_known_args(argv)
    target_waves = known.waves
    tree_head = tree_head_sha()
    if tree_head is None:
        print("cannot read this tree's HEAD with git, so a run could not be matched to its build")
        return 1

    def arm_text_for(out, args):
        wall_limit = args.wall_limit if args.wall_limit else DEFAULT_WALL_LIMIT
        text = "".join(f"{key}={value}\n" for key, value in arm_pairs(known, out, wall_limit) if value is not None and str(value) != "")
        return text, wall_limit

    runner.ENV_VAR = ENV_VAR
    runner.DONE_FILE = DONE_FILE
    runner.STATUS_FILE = STATUS_FILE
    runner.RUNS_ROOT = RUNS_ROOT
    runner.arm_text_for = arm_text_for
    runner.judge = judge
    runner.handle_dialogs = handle_dialogs
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
