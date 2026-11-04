#!/usr/bin/env python3
"""Run the Long Defeat Phase 0 spike (#1079) in the real game with nobody at the keyboard.

Run:  python tools/run_long_defeat_spike.py [--fortress helms|minas] [--waves N] [--wave-size N] [--garrison N] [--intermission S]
                                            [--live-limit S] [--no-defenders-ai] [--require-idle-minutes N] [--dry-run]

Deploy the tree under test with ./build.ps1 first and check the [BuildStamp] line: a build without the spike idles at the main menu,
and the run ends as a stall after 10 minutes. Exit 0 means every wave cleared, the S1, S2, S4, S5 and S7 lines are in the log, no batch
logged spawn level WRONG, the log has no CrashReport block, and the game left by itself. It judges no other value on those lines: a person
or a session reads them.

This reuses the Fords runner (tools/run_fords_autorun.py): its preflight, idle gate, launch recipe, dialog handling, crash scan and
log collection. It only changes the arm variable, the done and status files, the arm text and the judge, and it presses Cancel on one
shader dialog before the Fords dialog handling. The standing permission
and its gate are in docs/reference/unattended-game-runs.md. Phase 1 replaces this wrapper with a general option on the runner.
"""
import argparse
import re
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
    "[LongDefeat][S1] spawn level ok",
    "[LongDefeat][S1] after initial spawn",
    "[LongDefeat][S2] wave 1 CLEARED",
    "[LongDefeat][S4] repaired",
    "[LongDefeat][S5] deployed",
    "[LongDefeat][S7] after the armour write",
)

# In any batch, a soldier more than 10 m from the height of its frame or off the navmesh fails the run, whatever the other batches did.
SPAWN_LEVEL_WRONG = "[LongDefeat][S1] spawn level WRONG"

# The engine stands every soldier past the last slot on the frame point, and they spread out by themselves. It fails nothing, and the result names it, because Phase 1 needs a muster place with room.
SPAWN_PILE = "[LongDefeat][S1] spawn pile"

# A clear that the rule made and not the fight. It passes, and the result counts it, because a run where every clear came this way proves little.
STRAGGLERS_FELL = "they fall"

# Helm's Deep can stop its load at this dialog: the terrain shader's vista variant fails to compile (terrain_pixel_functions.rsh:818, X4008 raised to an error by X3129). Retry compiles the same source
# and fails again. Cancel lets the mission load, and the same answer works for a person at the keyboard. Only this variant is pressed: a dialog for another shader is a new fact.
SHADER_DIALOG = re.compile(r"Shaders/Sources/pbr_terrain(_gbuffer)?\.rs$")
FORDS_HANDLE_DIALOGS = runner.handle_dialogs


def judge(done, report_json, log_text=None):
    """(exit code, why). A spike run has no JSON report: its evidence is the tagged log lines. The whole log counts for the CrashReport
    check, because the log opens at this launch and the mission load comes before the done file's start time."""
    if not done:
        return 1, "no done file"
    wrong = next((row for row in (log_text or "").splitlines() if SPAWN_LEVEL_WRONG in row), None)
    note = f"; the log holds {wrong[:400]}" if wrong else ""
    if done.get("reason") != "final":
        return 1, f"reason={done.get('reason', '?')}{note}"
    if not log_text:
        return 1, "reason=final but no TAOM log was copied, and that log is the spike's evidence"
    if wrong:
        return 1, "reason=final" + note
    missing = [line for line in EVIDENCE if line not in log_text]
    if missing:
        return 1, "reason=final but the log lacks: " + ", ".join(missing)
    block = runner.crash_report_line(log_text, None)
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block: " + block[:200]
    return 0, f"reason=final, {log_text.count(' CLEARED at ')} waves cleared, S1 S2 S4 S5 S7 lines present, no spawn level WRONG, no CrashReport block, {log_text.count(STRAGGLERS_FELL)} waves ended by felling stragglers" + ("; the log holds a spawn pile" if SPAWN_PILE in log_text else "")


def handle_dialogs(pid, seen, say):
    """Presses Cancel on the terrain shader's compile dialog, then hands every dialog to the Fords runner, which logs a dialog it has not seen and reports a failed check."""
    try:
        dialogs = runner.game_dialogs(pid)
    except (OSError, AttributeError):
        dialogs = []
    for hwnd, title, texts, buttons in dialogs:
        if SHADER_DIALOG.search(title or "") and "Cancel" in buttons and runner.click_button(hwnd, buttons["Cancel"]) and hwnd not in seen:
            seen.add(hwnd)
            say(f"pressed Cancel on the shader compile dialog '{title}': {' | '.join(t for t in texts if t)}")
    return FORDS_HANDLE_DIALOGS(pid, seen, say)


def positive_int(text):
    value = int(text)
    if value <= 0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a positive whole number")
    return value


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0], epilog="The Fords runner's flags also apply: --require-idle-minutes, --wall-limit, --runs, --keep-on-crash, --runs-root, --dry-run.")
    parser.add_argument("--fortress", choices=("helms", "minas"), default="helms")
    parser.add_argument("--waves", type=positive_int, default=3)
    # 100 defenders against 120 attackers held on one run and all died on the other, and S4, S5 and S7 need a cleared wave. These numbers passed; they are no balance measure.
    parser.add_argument("--wave-size", type=positive_int, default=110)
    parser.add_argument("--garrison", type=positive_int, default=170)
    parser.add_argument("--intermission", type=runner.positive_seconds, default=15)
    # Three waves of 110 at Helm's Deep took 1,380 s of mission time.
    parser.add_argument("--live-limit", type=runner.positive_seconds, default=1800)
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
    runner.handle_dialogs = handle_dialogs
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
