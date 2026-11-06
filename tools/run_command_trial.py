#!/usr/bin/env python
"""Run the field trial of the Seat of Seeing and Marshal's Word (build-spec section 12) in the real game with nobody at the keyboard.

Run:  python tools/run_command_trial.py [--scene battle_terrain_a] [--live-limit S] [--steps enter,fly,...]
                                        [--require-idle-minutes N] [--keep-on-crash S] [--dry-run]

Deploy the tree under test with ./build.ps1 first: a build without the trial idles at the main menu, and the run ends as a stall after
10 minutes. The game plays one custom battle by itself (see docs/features/seat-of-seeing.md, "Field trial"), presses the Seat's own
controls in order, and writes one line per step:

    [SeatTrial] step=<name> result=<PASS|FAIL> <facts>

Exit 0 means every step in --steps has a PASS line and no FAIL line, the done file says reason=final, the log has no CrashReport block,
no failed Seat or Marshal's Word check line, no [ERROR] line of the Seat or its trial (a Seat patch that failed to apply and the thread guard's
line for a trial or Seat site count), no [ERROR] line of Marshal's Word (a hotkey registration that failed, a session that could not start, a patch
class of its category that failed or was skipped, the shared Patch_MissionTime_SetMovementOrder category that failed to apply (the two thrower patches), the thrower stand that failed, the order panel's registration failure, the spacing repair's degraded method, and the thread guard's line for one of its adapters' sites), and a clean module unload line, the game left by itself, and the log's [BuildStamp] is this checkout, read at the moment the run
is judged: the stamp's sha is HEAD, no tier of the stamp says MISMATCH, its .dirty mark agrees with the tree (dirty with uncommitted files,
clean without), and no uncommitted file under the build's own pathspec (WATCHED_PATHS) is newer than the stamp's build time. A deploy made before the last edit fails the run when
the edited file is still uncommitted (docs/features/seat-of-seeing.md names the cases only a content hash in the stamp could close),
and the PASS text names the build that was judged and the [Session] swallowed counts. Exit 1 is any failure, including a preflight refusal or an unreadable git; exit
2 is a bad argument; a --dry-run that exits 0 only means the preflight passed. A person still looks at the hud_*.png pictures the run
saves.

A run with no --steps arms every step, the Seat's and Marshal's Word's together, in ONE battle. Prove each stage by its subset; the Seat's rows ran in one battle once.
Marshal's Word's do not all fit it, because the battle runs out of time and soldiers before the volley and mark steps (docs/features/marshals-word.md, "How to verify in game",
lists the subsets). Run each Marshal's Word subset with its own --steps list, one battle each.

This reuses the Fords runner (tools/run_fords_autorun.py): its preflight, idle gate, launch recipe, dialog handling, crash scan and
log collection. It only changes the arm variable, the done and status files, the arm text and the judge. The standing permission
and its gate are in docs/reference/unattended-game-runs.md. A builder never launches this; the orchestrator does.
"""
import argparse
import functools
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import run_fords_autorun as runner  # noqa: E402

REPO = Path(__file__).resolve().parent.parent

ENV_VAR = "TAOM_COMMAND_TRIAL"
DONE_FILE = "command_trial_done.txt"
STATUS_FILE = "command_trial_status.txt"
RUNS_ROOT = r"E:\Temp\command-trial-runs"

DEFAULT_SCENE = "battle_terrain_a"
DEFAULT_LIVE_LIMIT = 420

# The step names of Main/Features/SeatOfSeeing/Domain/SeatTrialRows.cs, in its order. A later Seat stage adds its rows to both; another
# feature adds a tuple of its own beside this one and joins it into STEPS, each tied to its row list by a test.
SEAT_STEPS = (
    "refuse_in_deployment",
    "enter",
    "fly",
    "hero_stands",
    "leave_glide",
    "reenter_keeps_height",
    "cursor_with_panel",
    "focus_follows_cursor",
    "keep_panel_reopens",
    "hide_bars_restored",
    "keep_to_soldier",
    "keep_to_company",
    "let_go_on_death",
    "fight_on",
    "morale_held",
    "back_to_stand",
    "pause_flies",
    "slow",
    "fast",
    "adopt_flag",
    "speed_cleared",
    "rise_with_panel",
    "sink_on_close",
    "hero_falls",
    "take_soldier",
)
# The step names of Main/Features/MarshalsWord/Domain/MarshalTrialRows.cs, in its order.
MARSHAL_STEPS = (
    "queue_two_moves",
    "direct_order_clears",
    "array_keeps_offsets",
    "arrive_together",
    "volley_holds",
    "volley_looses",
    "mark_keeps_order",
    "outline_clears",
    "volley_massed",
    "square_side_kept",
    "spacing_one_rider",
    "hold_line_proof",
    "hold_line_slot",
    "hold_line_strike",
    "hold_line_flag",
    "advance_throwers",
    "advance_throwers_vanilla",
)
STEPS = SEAT_STEPS + MARSHAL_STEPS
# The two thrower steps each run in a battle of their own: the roster swaps javelineers in for the spearmen for the whole battle (SeatTrialRoster.PlayerFor).
THROWER_STEPS = ("advance_throwers", "advance_throwers_vanilla")

STEP_LINE = re.compile(r"\[SeatTrial\] step=(\w+) result=(PASS|FAIL)\b")
SEAT_CHECK_FAIL = re.compile(r"\bid=(taom\.seat-of-seeing\.[a-z0-9.-]+) result=fail\b")
MARSHAL_CHECK_FAIL = re.compile(r"\bid=(taom\.marshals-word\.[a-z0-9.-]+) result=fail\b")
# An [ERROR] line of the Seat or its trial (stage S2 onward): a fault, a failed start or a script failure; a Seat patch that failed to apply
# or was skipped (Harmony stops a category at its first failing class, so one line can leave every later patch of it off: Main/PatchCategoryApplier.cs); the
# thread guard's line for a trial or Seat site (MissionThreadGuard.NoteCall, written at LogError by CommandTrialHost, SeatHeroAdapter and SeatScreenAdapter). The
# hot key failure is logged at LogError for the same reason. Another feature's patch or guard line does not fail this run.
SEAT_ERROR = re.compile(
    r"\] \[ERROR\] (?:\[Seat(?:OfSeeing|Trial)\]|\[PatchApply\] \S*Patch140_|\[TAOM\] (?:CommandTrialHost|SeatHeroAdapter|SeatScreenAdapter)\.\w+ ran off the main mission thread)")
# An [ERROR] line of Marshal's Word: its own tag (the hotkey registration that failed, which leaves every key unbound, a session that could not
# start, the order panel's registration that failed, the spacing repair's transpiler that left a method as it was, and the thrower stand that failed); the category Patch141 or the
# shared Patch_MissionTime_SetMovementOrder (the two thrower patches) that failed to apply (the applier writes the category name, one line is the whole category off) or a class of its
# namespace that was skipped (the applier writes the class's full name, which holds no "Patch141_"); and the thread guard's line for one of its
# adapters' native writes. Its fault and its array fault also write a failed check line, which MARSHAL_CHECK_FAIL reads.
MARSHAL_ERROR = re.compile(
    r"\] \[ERROR\] (?:\[MarshalsWord\]"
    r"|\[PatchApply\] (?:\S*Patch141_\S*|Patch_MissionTime_SetMovementOrder|TAOM\.Features\.MarshalsWord\.\S+) (?:FAILED|SKIPPED)"
    r"|\[TAOM\] Marshal(?:Order|Fire|Outline)Adapter\.\w+ ran off the main mission thread)")
SESSION_SUMMARY = re.compile(r"\[Session\] summary reason=\S+ .*?\bswallowed=(\d+) shield_swallowed=(\d+)(?:.*?\berrors=(\d+))?")
SESSION_SITE = re.compile(r"\[Session\] swallowed feature=(\S+) site=(\S+) n=(\d+)")
BUILD_STAMP = re.compile(r"\[BuildStamp\] TAOM=\S+ build\.(\d{8}-\d{6})Z\+([0-9a-f]{7,40})(\.dirty)?(?:\s|$)")
UNLOAD_LINE = "[Shutdown] clean module unload"
# The pathspec the build marks a stamp .dirty from (Directory.Build.props, target TaomStampWorkingTreeState); a test reads it out of there.
WATCHED_PATHS = ("Main", "Client", "Dependencies", "Stubs", "Directory.Build.props", "GameReferences.targets")


def stale_build(log_text, head, changed):
    """Why the log's build is not this checkout, else None. changed: {path: UTC mtime} of the uncommitted files under WATCHED_PATHS, with
    datetime.min for a path that no longer exists."""
    match = BUILD_STAMP.search(log_text)
    if match is None:
        return "the log has no [BuildStamp] line with a git sha"
    if "MISMATCH" in log_text[match.start():].split("\n", 1)[0]:
        return "the stamp says MISMATCH: the TAOM modules in the deploy were not built together"
    if not head.startswith(match.group(2)):
        return f"the deploy is {match.group(2)[:9]}, this checkout is {head[:9]}"
    if bool(match.group(3)) != bool(changed):
        if match.group(3):
            return "the deploy has uncommitted edits and this tree has none"
        return f"the deploy is a clean build and this tree has uncommitted edits ({sorted(changed)[0]})"
    built = datetime.strptime(match.group(1), "%Y%m%d-%H%M%S")
    newer = sorted(path for path, when in changed.items() if when > built)
    return f"{newer[0]} changed after the build" if newer else None


def git_state(repo=REPO):
    """(HEAD sha, {path: UTC mtime}) for the uncommitted files under WATCHED_PATHS. A path that no longer exists, a deletion or the old
    name of a rename, has datetime.min: the tree differs from HEAD there, but no time can be newer than the build. Raises OSError when
    git cannot answer: not knowing the checkout is not a go."""
    def git(*args):
        result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        if result.returncode != 0:
            raise OSError(f"git {' '.join(args)} exited {result.returncode}: {result.stderr.strip()[:200]}")
        return result.stdout

    head = git("rev-parse", "HEAD").strip()
    changed = {}
    for entry in git("--no-optional-locks", "status", "--porcelain", "-z", "--no-renames", "-uall", "--", *WATCHED_PATHS).split("\0"):
        if len(entry) < 4:
            continue
        name = entry[3:]
        try:
            modified = (Path(repo) / name).stat().st_mtime
        except OSError:
            changed[name] = datetime.min
            continue
        changed[name] = datetime.fromtimestamp(modified, timezone.utc).replace(tzinfo=None)
    return head, changed


def session_swallowed(log_text):
    """The [Session] swallowed counts of the log, for the PASS text: the last summary line's two counters and errors, and every site that was
    written. A PASS with a swallowed exception is still a PASS, and the reader sees the number beside it. errors above zero means a check
    line may be missing (ingame_verdict.py warns of it), so the text carries it too."""
    summaries = SESSION_SUMMARY.findall(log_text)
    if not summaries:
        return "no [Session] summary line in the log"
    swallowed, shielded, errors = summaries[-1]
    text = f"[Session] swallowed={swallowed} shield_swallowed={shielded}"
    if errors:
        text += f" errors={errors}"
    sites = {(feature, site): count for feature, site, count in SESSION_SITE.findall(log_text)}
    if sites:
        text += " (" + ", ".join(f"{feature}.{site}={count}" for (feature, site), count in sites.items()) + ")"
    return text


def judge(done, report_json, log_text=None, steps=STEPS):
    """(exit code, why). A trial run has no JSON report: its evidence is the tagged log lines. The whole log counts for the CrashReport
    check: it opens at this launch, so a boot-time swallow is this run's too. The log must come from this checkout's build, and the
    checkout is read here, when the run is judged after the game exits, and not when it was launched: an edit, a merge or an earlier run of a
    batch made in between is seen."""
    if not done:
        return 1, "no done file"
    reason = done.get("reason", "")
    if reason != "final":
        return 1, f"reason={reason or '?'}"
    if not log_text:
        return 1, "reason=final but no TAOM log was copied, and that log is the trial's evidence"
    try:
        head, changed = git_state()
    except (OSError, subprocess.SubprocessError) as error:
        return 1, f"cannot read the checkout from git ({error}); a verdict needs to know which build it judges"
    stale = stale_build(log_text, head, changed)
    if stale is not None:
        return 1, "the game ran a build that is not this checkout: " + stale

    lines = log_text.splitlines()
    results = {}
    for line in lines:
        match = STEP_LINE.search(line)
        if match:
            results.setdefault(match.group(1), []).append(match.group(2))
    failed = sorted({step for step, seen in results.items() if "FAIL" in seen})
    other_fails = [line.strip() for line in lines if "result=FAIL" in line and not STEP_LINE.search(line)]
    faults = sorted({match.group(1) for pattern in (SEAT_CHECK_FAIL, MARSHAL_CHECK_FAIL) for match in map(pattern.search, lines) if match})
    if failed or other_fails:
        named = ", ".join(failed + [line[:120] for line in other_fails])
        return 1, f"FAIL lines: {named}" + (f"; failed checks: {', '.join(faults)}" if faults else "")
    missing = [step for step in steps if step not in results]
    if missing:
        return 1, "reason=final but the log has no [SeatTrial] line for: " + ", ".join(missing)
    block = runner.crash_report_line(log_text, None)
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block: " + block[:200]
    if faults:
        return 1, "a Seat or Marshal's Word check line failed: " + ", ".join(faults)
    errors = [line.strip() for line in lines if SEAT_ERROR.search(line) or MARSHAL_ERROR.search(line)]
    if errors:
        return 1, f"{len(errors)} [ERROR] line(s) of the Seat, Marshal's Word or the trial in the log, the first: " + errors[0][:200]
    if UNLOAD_LINE not in log_text:
        return 1, "reason=final but the TAOM log has no clean module unload line"
    stamp = BUILD_STAMP.search(log_text)
    build = f"{stamp.group(2)[:9]}{stamp.group(3) or ''} built {stamp.group(1)} UTC"
    return 0, f"reason=final, {len(steps)} steps with a PASS line, no FAIL line, no CrashReport block, no failed Seat or Marshal's Word check, no Seat or Marshal's Word [ERROR] line, clean unload, {session_swallowed(log_text)}, build {build}"


def normalised_steps(text):
    """The steps in the table's order, names lower-cased and trimmed, duplicates dropped. None means every step. A name not in the table raises
    ValueError, and so does a list that names a thrower step beside any other step. Prerequisites are the game's to check: it refuses to start on a list that skips one, and the done file says start_refused."""
    if text is None:
        return STEPS
    names = {name.strip().lower() for name in text.split(",") if name.strip()}
    if not names:
        raise ValueError("--steps names no step")
    unknown = sorted(names - set(STEPS))
    if unknown:
        raise ValueError("unknown step: " + ", ".join(unknown) + "; known: " + ", ".join(STEPS))
    if len(names) > 1 and names & set(THROWER_STEPS):
        raise ValueError("advance_throwers and advance_throwers_vanilla each run alone, in their own battle")
    return tuple(step for step in STEPS if step in names)


EPILOG = (
    "Flags of the Fords runner that apply here: --require-idle-minutes, --runs, --keep-on-crash, --runs-root and --dry-run. "
    "--wall-limit moves only the runner's own deadline (the arm file never carries it) and does not end the match. "
    "The Fords match flags (--west, --east, --ai, --west-ai, --speed, --live-seconds, --screenshots) parse and are ignored. "
    "Keep --live-limit well under the wall limit plus 15 minutes, which is the runner's deadline (3300 s by default); at 100000 or more the game keeps its default of 420."
)


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0], epilog=EPILOG)
    parser.add_argument("--scene", default=DEFAULT_SCENE)
    parser.add_argument("--live-limit", type=runner.positive_seconds, default=DEFAULT_LIVE_LIMIT, help="real seconds with a mission open before the game ends the run (default %(default)s)")
    parser.add_argument("--steps", help="a comma list of steps (default: all of " + ", ".join(STEPS) + ")")
    return parser


def arm_pairs(known, out):
    pairs = [("scene", known.scene), ("live_limit_seconds", f"{known.live_limit:g}")]
    if known.steps:
        pairs.append(("steps", known.steps))
    return pairs + [("out", out), ("quit", 1)]


def main(argv=None):
    parser = build_parser()
    known, rest = parser.parse_known_args(argv)
    try:
        steps = normalised_steps(known.steps)
    except ValueError as error:
        parser.error(str(error))
    known.steps = ",".join(steps) if known.steps else None

    # Refuse a broken git before a launch. The answer is not kept: the judge reads the checkout again when the run is judged.
    try:
        git_state()
    except (OSError, subprocess.SubprocessError) as error:
        print(f"cannot read the checkout from git ({error}); a verdict needs to know which build it judges")
        return 1

    def arm_text_for(out, args):
        wall_limit = args.wall_limit if args.wall_limit else runner.DEFAULT_WALL_LIMIT_SECONDS
        return "".join(f"{key}={value}\n" for key, value in arm_pairs(known, out)), wall_limit

    runner.ENV_VAR = ENV_VAR
    runner.DONE_FILE = DONE_FILE
    runner.STATUS_FILE = STATUS_FILE
    runner.RUNS_ROOT = RUNS_ROOT
    runner.arm_text_for = arm_text_for
    runner.judge = functools.partial(judge, steps=steps)
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
