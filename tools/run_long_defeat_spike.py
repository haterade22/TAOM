#!/usr/bin/env python3
"""Run the Long Defeat Phase 0 spike (#1079) in the real game with nobody at the keyboard.

Run:  python tools/run_long_defeat_spike.py [--fortress helms|minas] [--waves N] [--wave-size N] [--garrison N] [--intermission S]
                                            [--live-limit S] [--no-defenders-ai] [--require-idle-minutes N] [--dry-run]

Deploy the tree under test with ./build.ps1 first and check the [BuildStamp] line: a build without the spike idles at the main menu,
and the run ends as a stall after 10 minutes. Exit 0 means every wave cleared, the S1, S2, S4, S5 and S7 lines are in the log, the log has
no CrashReport block, and the game left by itself. It judges no value on those lines: a person or a session reads them.

This reuses the Fords runner (tools/run_fords_autorun.py): its preflight, idle gate, launch recipe, dialog handling, crash scan and
log collection. It only changes the arm variable, the done and status files, the arm text and the judge. The standing permission
and its gate are in docs/reference/unattended-game-runs.md. Phase 1 replaces this wrapper with a general option on the runner.
"""
import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import run_fords_autorun as runner  # noqa: E402

ENV_VAR = "TAOM_LONGDEFEAT_SPIKE"
DONE_FILE = "long_defeat_done.txt"
STATUS_FILE = "long_defeat_status.txt"
RUNS_ROOT = r"E:\Temp\long-defeat-runs"

# TacticBreachWalls stops using machines at this many attackers or fewer, so a smaller wave cannot show what the engines do.
SHOCK_ASSAULT_UNITS = 100

# The lines a complete spike run prints. A run that lacks one has not answered that spike.
EVIDENCE = (
    "[LongDefeat][S1] after initial spawn",
    "[LongDefeat][S2] wave 1 CLEARED",
    "[LongDefeat][S4] repaired",
    "[LongDefeat][S5] deployed",
    "[LongDefeat][S7] after the armour write",
)


def judge(done, report_json, log_text=None):
    """(exit code, why). A spike run has no JSON report: its evidence is the tagged log lines. The whole log counts for the CrashReport
    check, because the log opens at this launch and the mission load comes before the done file's start time."""
    if not done:
        return 1, "no done file"
    if done.get("reason") != "final":
        return 1, f"reason={done.get('reason', '?')}"
    if not log_text:
        return 1, "reason=final but no TAOM log was copied, and that log is the spike's evidence"
    missing = [line for line in EVIDENCE if line not in log_text]
    if missing:
        return 1, "reason=final but the log lacks: " + ", ".join(missing)
    block = runner.crash_report_line(log_text, None)
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block: " + block[:200]
    return 0, f"reason=final, {log_text.count(' CLEARED at ')} waves cleared, S1 S2 S4 S5 S7 lines present, no CrashReport block"


def positive_int(text):
    value = int(text)
    if value <= 0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a positive whole number")
    return value


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0], epilog="The Fords runner's flags also apply: --require-idle-minutes, --wall-limit, --runs, --keep-on-crash, --runs-root, --dry-run.")
    parser.add_argument("--fortress", choices=("helms", "minas"), default="helms")
    parser.add_argument("--waves", type=positive_int, default=3)
    parser.add_argument("--wave-size", type=positive_int, default=120)
    parser.add_argument("--garrison", type=positive_int, default=100)
    parser.add_argument("--intermission", type=runner.positive_seconds, default=15)
    parser.add_argument("--live-limit", type=runner.positive_seconds, default=900)
    parser.add_argument("--no-defenders-ai", action="store_true")
    return parser


def arm_pairs(known, out):
    return [("fortress", known.fortress), ("waves", known.waves), ("wave_size", known.wave_size), ("garrison", known.garrison),
            ("intermission_seconds", known.intermission), ("live_limit_seconds", known.live_limit),
            ("defenders_ai", 0 if known.no_defenders_ai else 1), ("out", out), ("quit", 1)]


def main(argv=None):
    parser = build_parser()
    known, rest = parser.parse_known_args(argv)
    if known.waves < 2:
        parser.error("--waves must be at least 2: S4, S5 and S7 run between waves")

    def arm_text_for(out, args):
        wall_limit = args.wall_limit if args.wall_limit else runner.DEFAULT_WALL_LIMIT_SECONDS
        return "".join(f"{key}={value}\n" for key, value in arm_pairs(known, out)), wall_limit

    runner.ENV_VAR = ENV_VAR
    runner.DONE_FILE = DONE_FILE
    runner.STATUS_FILE = STATUS_FILE
    runner.RUNS_ROOT = RUNS_ROOT
    runner.arm_text_for = arm_text_for
    runner.judge = judge
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
