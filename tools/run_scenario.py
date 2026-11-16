#!/usr/bin/env python
"""tools/run_scenario.py: one unattended run in the real game (#1075 slice 1, used by #1283; the story-mission kind is #1368), judged by its done file and log.

A wrapper on run_fords_autorun.py, in the way run_long_defeat.py is: it keeps that tool's idle gate, launch recipe, dialog handling,
crash scan and log collection, and swaps in this feature's names, arm file, judge and printer. It arms TAOM_AUTORUN with an arm file
under E:\\Temp\\scenario-runs\\<yyyyMMdd-HHmmss>\\. The game starts a new campaign, runs the wanted days at the wanted speed, writes
autorun_done.txt and quits.

The kind story-mission starts a new campaign, opens one story mission through the dev menu's own click path, waits for the mission to end and
the map to come back, and quits. It runs no campaign days of its own, so --days, --speed and --stall-seconds are refused with it.

Own arguments (everything else goes to the Fords runner, for example --dry-run, --runs, --wall-limit, --require-idle-minutes):
  --kind campaign-new|story-mission   --days N (campaign-new only: 1 to 1000, default 3)   --culture ID (default gondor)
  --speed X (campaign-new only: above 0, at most 30, default 15)   --scenario LABEL (default the kind)
  --stall-seconds N (campaign-new only, default 600)
  --mission ID (story-mission only, required: a story mission id of Main/_Module/ModuleData/story_missions/story_missions.json, for
  example helms_deep_defend or helms_deep_attack; an unknown id is refused)

This tool does not build or deploy: deploy this tree with `pwsh ./build.ps1` first. It reads the checkout (HEAD and the uncommitted files
under the build's own pathspec) once, at launch, with tools/autorun_build.py, and refuses to start when git cannot say.

Exit 0 needs reason=ok, days_run at least days_wanted, the [Check] pass line in the TAOM log, no CrashReport error block, the clean
module unload line, a done file that echoes the armed plan, no arm file warning, a [BuildStamp] that is the checkout as read at
launch (autorun_build.stale_build: the sha is HEAD, no MISMATCH, the .dirty mark agrees with the tree, no uncommitted watched file is newer
than the build), and a game that left by itself. The PASS line names that build. Then the war ledger of the run is saved as ledger.txt in
the run folder.

Exit 0 for story-mission needs reason=ok, a Won or Lost verdict echoed by the done file for the armed mission, the story done [Check] pass
line, the hosted.boot and ended pass lines of the story mission (a reason=Abandoned end is not one), exactly one [StoryMissions] launching
line (the armed id, autoplay True) and no other launching line, and the same log, crash, unload, arm warning and build conditions. The ended
line must name the row's own side (side=Defending for a defence row, side=Attacking for an attack row, read from the checkout's
story_missions.json). A build
that does not know the kind stays unarmed and writes no done file, so the runner ends it as a 10 minute stall and never calls the judge; the
printers then quote the log's first "not armed:" line. The default wall limit is the campaign-new one plus the mission limit (1200 s, the
arm file's mission_limit_seconds). The driver reads no wall limit for this kind: --wall-limit only moves the runner's give-up time.

Run:  python tools/run_scenario.py --days 30 --dry-run
      python tools/run_scenario.py --kind story-mission --mission helms_deep_defend --dry-run
      python tools/run_scenario.py --kind story-mission --mission helms_deep_attack --dry-run
Tests: python -m unittest tools.tests.test_run_scenario
"""
import argparse
import json
import math
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import autorun_build  # noqa: E402
import run_fords_autorun as runner  # noqa: E402
# Imported here, never after main swaps runner.handle_dialogs: run_long_defeat captures the Fords handler at import.
import run_long_defeat as long_defeat  # noqa: E402

ENV_VAR = "TAOM_AUTORUN"
DONE_FILE = "autorun_done.txt"
STATUS_FILE = "autorun_status.txt"
RUNS_ROOT = r"E:\Temp\scenario-runs"
KIND_CAMPAIGN = "campaign-new"
KIND_STORY = "story-mission"
KINDS = (KIND_CAMPAIGN, KIND_STORY)
CHECK_ID = "taom.autorun.campaign-new.done"
STORY_CHECK_ID = "taom.autorun.story-mission.done"
PASS_LINE = re.compile(r"\[Check\] id=" + re.escape(CHECK_ID) + r"\s+result=pass\b")
# The same patterns as tools/ingame_checks/1105.toml (a test holds them equal): the boot line, and the end line that names only the three
# ways a run ends by itself, so a reason=Abandoned retreat is not a pass. Matched line by line.
STORY_BOOT_PASS = r"\[Check\] id=taom\.story-missions\.hosted\.boot result=pass deployment_finished=true siege_ai=true main=true friendly=false"
ROOT = Path(__file__).resolve().parent.parent
STORY_MISSIONS_FILE = ROOT / "Main" / "_Module" / "ModuleData" / "story_missions" / "story_missions.json"
STORY_SIDES = {"Defend": "Defending", "Attack": "Attacking"}


def story_ended_pass(side):
    """The ended line's pass pattern for a row's side (Defending or Attacking), as tools/ingame_checks/1105.toml (Defending) and 1409.toml
    (Attacking) hold it."""
    return (r"\[Check\] id=taom\.story-missions\.ended result=pass verdict=(Won|Lost) side=" + side
            + r" reason=(TargetReached|DefendersDead|KeepLost) waves=\d+$")


def story_rows():
    """{lowercase mission id: "Defending" or "Attacking"} read from this checkout's story_missions.json. An unreadable file is an empty table."""
    try:
        data = json.loads(STORY_MISSIONS_FILE.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    rows = data.get("missions", []) if isinstance(data, dict) else []
    return {str(row.get("id", "")).lower(): STORY_SIDES[row["side"]] for row in rows if isinstance(row, dict) and row.get("side") in STORY_SIDES}


def story_side(mission):
    """"Defending" or "Attacking" for a story mission id of the checkout, None for an id it does not hold."""
    return story_rows().get(str(mission).strip().lower())


# Both launchers log "[X] launching ..." once per run; a story run holds exactly one such line.
ANY_LAUNCH = re.compile(r"\[(LongDefeat|StoryMissions)\] launching ")
# ScenarioAutorunStart logs this when the arm file is refused; a build that lacks the story kind refuses it.
NOT_ARMED = "[Autorun][Scenario] not armed: "
DEFAULT_MISSION_LIMIT_SECONDS = 1200
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
RUNNER_OPTIONS = ("Fords runner options this tool passes on: --wall-limit SECONDS (default max(1800, 600 + days * 20), plus 1200 for "
                  "story-mission), --runs N, "
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
    parser = argparse.ArgumentParser(description="Run one unattended scenario in the real game: a new campaign, or a new campaign and one story mission.",
                                     epilog=RUNNER_OPTIONS)
    parser.add_argument("--kind", choices=KINDS, default=KIND_CAMPAIGN, help="the scenario kind (default campaign-new)")
    parser.add_argument("--days", type=days_arg, default=None,
                        help=f"campaign-new only: campaign days to run, 1 to {MAX_DAYS} (default {DEFAULT_DAYS})")
    parser.add_argument("--mission", default=None, help="story-mission only, required: the story mission id, for example helms_deep_defend")
    parser.add_argument("--culture", default=DEFAULT_CULTURE, help=f"the player's culture id (default {DEFAULT_CULTURE})")
    parser.add_argument("--speed", type=speed_arg, default=None,
                        help=f"campaign-new only: campaign speed, above 0 and at most {MAX_SPEED:g} (default {DEFAULT_SPEED:g})")
    parser.add_argument("--scenario", default=None, help="label written to the arm and done files (default the kind)")
    parser.add_argument("--stall-seconds", type=stall_arg, default=None,
                        help=f"campaign-new only: seconds without campaign progress before the game ends the run (default {DEFAULT_STALL_SECONDS})")
    return parser


def parse_own(argv):
    """(own arguments, the rest for the Fords runner). A bad value exits through argparse with a message that names it."""
    parser = build_own_parser()
    known, rest = parser.parse_known_args(argv)
    if known.kind == KIND_STORY:
        if known.days is not None:
            parser.error("--days is only for --kind campaign-new; a story-mission run ends when the map is back")
        if known.mission is None or not known.mission.strip():
            parser.error("--kind story-mission needs --mission <story mission id>")
        for flag, value in (("--speed", known.speed), ("--stall-seconds", known.stall_seconds)):
            if value is not None:
                parser.error(f"{flag} is only for --kind campaign-new; a story-mission run reads neither")
        # The game takes the id in any case and logs the catalog's lowercase id, which the judge and tools/ingame_checks/1368.toml match.
        known.mission = known.mission.strip().lower()
        # The side is read once, here, at launch: the judge uses this value, so an edit of the JSON while the game runs cannot fail a good run.
        rows = story_rows()
        known.side = rows.get(known.mission)
        if known.side is None:
            parser.error(f"--mission '{known.mission}' is not a story mission of this checkout"
                         + (f" (known: {', '.join(sorted(rows))})" if rows else f" ({STORY_MISSIONS_FILE} could not be read or holds no row with side Defend or Attack)"))
    else:
        if known.mission is not None:
            parser.error("--mission is only for --kind story-mission")
        if known.days is None:
            known.days = DEFAULT_DAYS
        if known.speed is None:
            known.speed = DEFAULT_SPEED
        if known.stall_seconds is None:
            known.stall_seconds = DEFAULT_STALL_SECONDS
    if known.scenario is None:
        known.scenario = known.kind
    return known, rest


def default_wall_limit(days, mission_limit=0):
    return max(1800, 600 + days * 20) + mission_limit


def number_text(value):
    return str(int(value)) if float(value).is_integer() else repr(float(value))


def arm_text_for(out, known, args):
    """(arm file text, wall limit). The wall limit is the runner's --wall-limit when given, else computed from the days (campaign-new) or the
    boot allowance plus the mission limit (story-mission). For campaign-new the Fords runner stops the process 15 minutes after this limit, so
    the game's own limit trips first. The story kind writes the key, but its driver reads no wall limit (only the Running phase does, and the
    story kind never enters it): --wall-limit only moves the runner's give-up time (this limit plus 15 minutes), which must stay above the
    boot time plus the story phases' own bounds, 2,880 s after the menu: creation 600 (ScenarioRules.CreationWaitSeconds), map 600
    (MapWaitSeconds), four steps of 120 (StoryStepWaitSeconds: menu, open, mission seen, return) and the 1200 s mission limit
    (ScenarioPlan.DefaultMissionLimitSeconds). The story kind writes no days, speed or stall seconds: its driver reads none of them."""
    if known.kind == KIND_STORY:
        wall_limit = args.wall_limit if args.wall_limit else default_wall_limit(DEFAULT_DAYS, DEFAULT_MISSION_LIMIT_SECONDS)
        pairs = [("kind", known.kind), ("out", out), ("scenario", known.scenario), ("mission", known.mission),
                 ("mission_limit_seconds", DEFAULT_MISSION_LIMIT_SECONDS), ("culture", known.culture),
                 ("wall_limit_seconds", number_text(wall_limit)), ("quit", 1)]
        return "".join(f"{key}={value}\n" for key, value in pairs), wall_limit
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


def story_log_problems(log_text, mission, side):
    """The story kind's log conditions, one reason each. The driver's done line alone could pass a mission the player's own agent walked out
    of (reason=Abandoned reads as a loss), so the story's end line and its boot line are required too, and a run holds one launch only.
    side is the row's side, Defending or Attacking: an end line of the other side is no pass."""
    lines = log_text.splitlines()
    problems = []
    done_pass = (r"\[Check\] id=" + re.escape(STORY_CHECK_ID) + r" result=pass reason=ok mission=" + re.escape(mission)
                 + r" verdict=(Won|Lost) mission_seconds=\S+ culture=\S+$")
    if not any(re.search(done_pass, line) for line in lines):
        problems.append(f"the TAOM log has no [Check] id={STORY_CHECK_ID} result=pass line for mission {mission} with a Won or Lost verdict")
    if not any(re.search(STORY_BOOT_PASS, line) for line in lines):
        problems.append("the TAOM log has no taom.story-missions.hosted.boot pass line")
    ended = story_ended_pass(side)
    if not any(re.search(ended, line) for line in lines):
        problems.append(f"the TAOM log has no taom.story-missions.ended pass line with side={side} (a reason=Abandoned end is not one)")
    launch = r"\[StoryMissions\] launching \S+ for '" + re.escape(mission) + r"' \(story\d+, autoplay True\)"
    wanted = [line for line in lines if re.search(launch, line)]
    others = [line for line in lines if ANY_LAUNCH.search(line)]
    if len(wanted) != 1 or len(others) != 1:
        problems.append(f"the TAOM log holds {len(wanted)} launching lines for '{mission}' with autoplay True and {len(others)} launching "
                        "lines in all (wanted one and one)")
    return problems


def judge(done, report_json, log_text=None, armed=None, checkout=None):
    """(exit code, why). Every failed condition adds one reason; the reasons are joined with '; '. armed is the parsed plan the done file
    must echo; checkout is (HEAD sha, {path: mtime}) as main read it with autorun_build.checkout_state() before the game started, which the
    log's [BuildStamp] must match (autorun_build.stale_build). main passes both; a judge called without a checkout fails."""
    if not done:
        return 1, "no done file"
    story = (armed.kind if armed is not None else done.get("kind")) == KIND_STORY
    mission = armed.mission if armed is not None and story else done.get("mission", "")
    problems = []
    if armed is not None:
        echoed = [("kind", armed.kind), ("scenario", armed.scenario), ("culture", armed.culture)]
        echoed.append(("mission", armed.mission) if story else ("days_wanted", str(armed.days)))
        for key, want in echoed:
            if done.get(key) != want:
                problems.append(f"{key}={done.get(key, '?')} in the done file, but the arm file asked for {want}")
    reason = done.get("reason", "")
    if reason != "ok":
        problems.append(f"reason={reason or '?'} (wanted ok)")
    if story:
        if done.get("verdict") not in ("Won", "Lost"):
            problems.append(f"verdict={done.get('verdict') or '?'} in the done file (wanted Won or Lost)")
        days_run = days_wanted = None
    else:
        days_run, days_wanted = parse_count(done, "days_run"), parse_count(done, "days_wanted")
        if days_run is None or days_wanted is None:
            problems.append(f"days_run={done.get('days_run', '?')} or days_wanted={done.get('days_wanted', '?')} is not a whole number")
        elif days_run < days_wanted:
            problems.append(f"days_run={days_run} is below days_wanted={days_wanted}")
    if log_text is None:
        problems.append("no TAOM log copy, so the result=pass line and the crash scan could not be checked")
    else:
        if story:
            # The side parse_own read at launch; a judge called without it (older callers) reads the checkout.
            side = getattr(armed, "side", None) if armed is not None else None
            if side is None:
                side = story_side(mission)
            if side is None:
                problems.append(f"'{mission}' is not a story mission of this checkout, so its side is unknown")
            else:
                problems.extend(story_log_problems(log_text, mission, side))
        elif not PASS_LINE.search(log_text):
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
    if story:
        return 0, (f"{proved}reason=ok, mission {mission} verdict {done['verdict']}, back on the map, pass line present, "
                   "no CrashReport block, clean unload")
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


def say_if_unarmed(text, say):
    """An unarmed build writes no done file and ends as the runner's stall, so the judge never runs for it: the printers name the cause
    from the first 'not armed:' line of the collected log."""
    line = next((line for line in text.splitlines() if NOT_ARMED in line), None)
    if line is not None:
        say("this build did not arm the run: " + line[line.index(NOT_ARMED):][:300])


def show_results(done_text, log_copy, report_json, say):
    print(f"---- {DONE_FILE} ----\n" + done_text.strip())
    if log_copy is None:
        say("no TAOM log copy, so no [Check] lines and no war ledger")
        return
    text = Path(log_copy).read_text(encoding="utf-8", errors="replace")
    say_if_unarmed(text, say)
    print("---- [Check] lines of the log ----")
    for line in text.splitlines():
        if "[Check] id=taom.autorun." in line:
            print(line)
    stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
    print(stamp[0] if stamp else "no [BuildStamp] line")
    say("clean module unload line: " + ("present" if UNLOAD_LINE in text else "ABSENT"))
    run_ledger(log_copy, say)


def show_story_results(done_text, log_copy, report_json, say):
    """The story kind's printer: the done file, the driver's and the story's [Check] lines, the launch line and the build stamp. It runs no
    war ledger: a story run plays no campaign days."""
    print(f"---- {DONE_FILE} ----\n" + done_text.strip())
    if log_copy is None:
        say("no TAOM log copy, so no [Check] lines")
        return
    text = Path(log_copy).read_text(encoding="utf-8", errors="replace")
    say_if_unarmed(text, say)
    print("---- [Check] and launch lines of the log ----")
    for line in text.splitlines():
        if "[Check] id=taom.autorun." in line or "[Check] id=taom.story-missions." in line or ANY_LAUNCH.search(line):
            print(line)
    stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
    print(stamp[0] if stamp else "no [BuildStamp] line")
    say("clean module unload line: " + ("present" if UNLOAD_LINE in text else "ABSENT"))


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
    if known.kind == KIND_STORY:
        # Helm's Deep can stop its load at the terrain shader dialog, which the Long Defeat runner's handler presses Cancel on.
        runner.handle_dialogs = long_defeat.handle_dialogs
        runner.show_results = show_story_results
    else:
        runner.show_results = show_results
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
