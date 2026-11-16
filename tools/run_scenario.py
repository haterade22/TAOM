#!/usr/bin/env python
"""tools/run_scenario.py: one unattended new-campaign run in the real game (#1075 slice 1, used by #1283), judged by its done file and log.

A wrapper on run_fords_autorun.py, in the way run_long_defeat.py is: it keeps that tool's idle gate, launch recipe, dialog handling,
crash scan and log collection, and swaps in this feature's names, arm file, judge and printer. It arms TAOM_AUTORUN with an arm file
under E:\\Temp\\scenario-runs\\<yyyyMMdd-HHmmss>\\. The game starts a new campaign, runs the wanted days at the wanted speed, writes
autorun_done.txt and quits.

Own arguments (everything else goes to the Fords runner, for example --dry-run, --runs, --wall-limit, --require-idle-minutes):
  --kind campaign-new   --days N (1 to 1000, default 3)   --culture ID (default gondor)   --speed X (above 0, at most 30, default 15)
  --scenario LABEL      --stall-seconds N (default 600)

This tool does not build or deploy: deploy this tree with `pwsh ./build.ps1` first. It reads the checkout (HEAD and the uncommitted files
under the build's own pathspec) once, at launch, with tools/autorun_build.py, and refuses to start when git cannot say.

Exit 0 needs reason=ok, days_run at least days_wanted, the [Check] pass line in the TAOM log, no CrashReport error block, the clean
module unload line, a done file that echoes the armed plan, no arm file warning, a [BuildStamp] that is the checkout as read at
launch (autorun_build.stale_build: the sha is HEAD, no MISMATCH, the .dirty mark agrees with the tree, no uncommitted watched file is newer
than the build), and a game that left by itself. The PASS line names that build. Then the war ledger of the run is saved as ledger.txt in
the run folder.

Run:  python tools/run_scenario.py --days 30 --dry-run
Tests: python -m unittest tools.tests.test_run_scenario
"""
import argparse
import math
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import autorun_build  # noqa: E402
import run_fords_autorun as runner  # noqa: E402

ENV_VAR = "TAOM_AUTORUN"
DONE_FILE = "autorun_done.txt"
STATUS_FILE = "autorun_status.txt"
RUNS_ROOT = r"E:\Temp\scenario-runs"
KINDS = ("campaign-new",)
CHECK_ID = "taom.autorun.campaign-new.done"
PASS_LINE = re.compile(r"\[Check\] id=" + re.escape(CHECK_ID) + r"\s+result=pass\b")
UNLOAD_LINE = "[Shutdown] clean module unload"
DEFAULT_DAYS = 3
DEFAULT_CULTURE = "gondor"
DEFAULT_SPEED = 15.0
DEFAULT_STALL_SECONDS = 600
MAX_DAYS = 1000
MAX_SPEED = 30.0
LEDGER_FILE = "ledger.txt"
LEDGER_TOOL = Path(__file__).resolve().parent / "analyze_war_ledger.py"
LEDGER_TIMEOUT_SECONDS = 300
# ScenarioAutorunDriver.cs logs every arm file warning with this text (Prefix + "arm file: "); the game then ran a default instead.
ARM_WARNING = "[Autorun][Scenario] arm file: "
RUNNER_OPTIONS = ("Fords runner options this tool passes on: --wall-limit SECONDS (default max(1800, 600 + days * 20)), --runs N, "
                  "--runs-root DIR, --require-idle-minutes N, --keep-on-crash MINUTES, --dry-run. The Fords match options (factions, maps, "
                  "AI profile) are parsed and ignored; --speed is this tool's campaign speed.")


def days_arg(text):
    try:
        value = int(text)
    except ValueError:
        raise argparse.ArgumentTypeError(f"days must be a whole number from 1 to {MAX_DAYS}, got '{text}'") from None
    if not 1 <= value <= MAX_DAYS:
        raise argparse.ArgumentTypeError(f"days must be from 1 to {MAX_DAYS}, got {value}")
    return value


def speed_arg(text):
    try:
        value = float(text)
    except ValueError:
        raise argparse.ArgumentTypeError(f"speed must be a number above 0 and at most {MAX_SPEED:g}, got '{text}'") from None
    if math.isnan(value) or not 0 < value <= MAX_SPEED:
        raise argparse.ArgumentTypeError(f"speed must be above 0 and at most {MAX_SPEED:g}, got '{text}'")
    return value


def stall_arg(text):
    try:
        value = int(text)
    except ValueError:
        raise argparse.ArgumentTypeError(f"stall seconds must be a whole number above 0, got '{text}'") from None
    if value <= 0:
        raise argparse.ArgumentTypeError(f"stall seconds must be above 0, got {value}")
    return value


def build_own_parser():
    parser = argparse.ArgumentParser(description="Run one unattended new-campaign scenario in the real game.", epilog=RUNNER_OPTIONS)
    parser.add_argument("--kind", choices=KINDS, default=KINDS[0], help="the scenario kind (only campaign-new)")
    parser.add_argument("--days", type=days_arg, default=DEFAULT_DAYS, help=f"campaign days to run, 1 to {MAX_DAYS} (default {DEFAULT_DAYS})")
    parser.add_argument("--culture", default=DEFAULT_CULTURE, help=f"the player's culture id (default {DEFAULT_CULTURE})")
    parser.add_argument("--speed", type=speed_arg, default=DEFAULT_SPEED, help=f"campaign speed, above 0 and at most {MAX_SPEED:g} (default {DEFAULT_SPEED:g})")
    parser.add_argument("--scenario", default=KINDS[0], help="label written to the arm and done files")
    parser.add_argument("--stall-seconds", type=stall_arg, default=DEFAULT_STALL_SECONDS,
                        help=f"seconds without campaign progress before the game ends the run (default {DEFAULT_STALL_SECONDS})")
    return parser


def parse_own(argv):
    """(own arguments, the rest for the Fords runner). A bad value exits through argparse with a message that names it."""
    return build_own_parser().parse_known_args(argv)


def default_wall_limit(days):
    return max(1800, 600 + days * 20)


def number_text(value):
    return str(int(value)) if float(value).is_integer() else repr(float(value))


def arm_text_for(out, known, args):
    """(arm file text, wall limit). The wall limit is the runner's --wall-limit when given, else computed from the days. The Fords runner
    stops the process 15 minutes after this limit, so the game's own limit always trips first."""
    wall_limit = args.wall_limit if args.wall_limit else default_wall_limit(known.days)
    pairs = [("kind", known.kind), ("out", out), ("scenario", known.scenario), ("culture", known.culture), ("days", known.days),
             ("speed", number_text(known.speed)), ("stall_seconds", known.stall_seconds),
             ("wall_limit_seconds", number_text(wall_limit)), ("quit", 1)]
    return "".join(f"{key}={value}\n" for key, value in pairs), wall_limit


def parse_count(done, key):
    try:
        return int(done.get(key, ""))
    except ValueError:
        return None


def judge(done, report_json, log_text=None, armed=None, checkout=None):
    """(exit code, why). Every failed condition adds one reason; the reasons are joined with '; '. armed is the parsed plan the done file
    must echo; checkout is (HEAD sha, {path: mtime}) as main read it with autorun_build.checkout_state() before the game started, which the
    log's [BuildStamp] must match (autorun_build.stale_build). main passes both; a judge called without a checkout fails."""
    if not done:
        return 1, "no done file"
    problems = []
    if armed is not None:
        for key, want in (("kind", armed.kind), ("scenario", armed.scenario), ("culture", armed.culture), ("days_wanted", str(armed.days))):
            if done.get(key) != want:
                problems.append(f"{key}={done.get(key, '?')} in the done file, but the arm file asked for {want}")
    reason = done.get("reason", "")
    if reason != "ok":
        problems.append(f"reason={reason or '?'} (wanted ok)")
    days_run, days_wanted = parse_count(done, "days_run"), parse_count(done, "days_wanted")
    if days_run is None or days_wanted is None:
        problems.append(f"days_run={done.get('days_run', '?')} or days_wanted={done.get('days_wanted', '?')} is not a whole number")
    elif days_run < days_wanted:
        problems.append(f"days_run={days_run} is below days_wanted={days_wanted}")
    if log_text is None:
        problems.append("no TAOM log copy, so the result=pass line and the crash scan could not be checked")
    else:
        if not PASS_LINE.search(log_text):
            problems.append(f"the TAOM log has no [Check] id={CHECK_ID} result=pass line")
        # The whole log is this session's (the runner takes the newest log created after launch), and TAOM's load comes before the driver.
        block = runner.crash_report_line(log_text, None)
        if block is not None:
            problems.append("the TAOM log holds a CrashReport block: " + block[:200])
        if UNLOAD_LINE not in log_text:
            problems.append("the log has no clean module unload line")
        warning = next((line for line in log_text.splitlines() if ARM_WARNING in line), None)
        if warning is not None:
            problems.append("the game did not take the arm file as written: " + warning[:200])
    build = None
    if checkout is None:
        problems.append("the checkout was not read at launch, so the build cannot be judged")
    elif log_text is not None:
        problem = autorun_build.stale_build(log_text, *checkout)
        if problem is not None:
            problems.append("the game ran a build that is not this checkout: " + problem)
        else:
            build = autorun_build.build_label(log_text)
    if problems:
        return 1, "; ".join(problems)
    proved = f"build {build}, " if build else ""
    return 0, f"{proved}reason=ok, {days_run} of {days_wanted} days, pass line present, no CrashReport block, clean unload"


def run_ledger(log_copy, say):
    """Runs analyze_war_ledger.py on the copied log and prints it; on exit 0 saves ledger.txt beside the log. A failure is reported,
    never fatal, and leaves no ledger.txt."""
    try:
        done = subprocess.run([sys.executable, "-X", "utf8", str(LEDGER_TOOL), str(log_copy)], capture_output=True, text=True,
                              encoding="utf-8", errors="replace", timeout=LEDGER_TIMEOUT_SECONDS)
    except (OSError, subprocess.SubprocessError) as error:
        say(f"war ledger not produced: {error}")
        return
    text = done.stdout + (("\n" + done.stderr) if done.stderr else "")
    print(f"---- war ledger (exit {done.returncode}) ----\n" + text.strip())
    if done.returncode != 0:
        say(f"war ledger tool exited {done.returncode}; no {LEDGER_FILE}: " + text.strip()[:200])
        return
    try:
        (Path(log_copy).parent / LEDGER_FILE).write_text(text, encoding="utf-8")
    except OSError as error:
        say(f"war ledger not saved: {error}")


def show_results(done_text, log_copy, report_json, say):
    print(f"---- {DONE_FILE} ----\n" + done_text.strip())
    if log_copy is None:
        say("no TAOM log copy, so no [Check] lines and no war ledger")
        return
    text = Path(log_copy).read_text(encoding="utf-8", errors="replace")
    print("---- [Check] lines of the log ----")
    for line in text.splitlines():
        if "[Check] id=taom.autorun." in line:
            print(line)
    stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
    print(stamp[0] if stamp else "no [BuildStamp] line")
    say("clean module unload line: " + ("present" if UNLOAD_LINE in text else "ABSENT"))
    run_ledger(log_copy, say)


def main(argv=None):
    known, rest = parse_own(argv)

    def armed_text(out, args):
        return arm_text_for(str(out), known, args)

    runner.ENV_VAR = ENV_VAR
    runner.DONE_FILE = DONE_FILE
    runner.STATUS_FILE = STATUS_FILE
    runner.RUNS_ROOT = RUNS_ROOT
    runner.arm_text_for = armed_text
    # Read once, before the game starts: a commit or an edit made during the run is not the deployed build.
    checkout, refusal = autorun_build.read_checkout_for_launch()
    if refusal:
        print(refusal)
        return 1
    runner.judge = lambda done, report_json, log_text=None: judge(done, report_json, log_text, known, checkout)
    runner.show_results = show_results
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
