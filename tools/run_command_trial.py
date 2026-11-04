#!/usr/bin/env python
"""Run the field trial of the Seat of Seeing (build-spec section 12) in the real game with nobody at the keyboard.

Run:  python tools/run_command_trial.py [--scene battle_terrain_a] [--live-limit S] [--steps enter,fly,...]
                                        [--require-idle-minutes N] [--keep-on-crash S] [--dry-run]

Deploy the tree under test with ./build.ps1 first: a build without the trial idles at the main menu, and the run ends as a stall after
10 minutes. The game plays one custom battle by itself (see docs/features/seat-of-seeing.md, "Field trial"), presses the Seat's own
controls in order, and writes one line per step:

    [SeatTrial] step=<name> result=<PASS|FAIL> <facts>

Exit 0 means every step in --steps has a PASS line and no FAIL line, the done file says reason=final, the log has no CrashReport block,
no failed Seat check line and a clean module unload line, the game left by itself, and the log's [BuildStamp] is this checkout: the
stamp's sha is HEAD, its .dirty mark agrees with the tree (dirty with uncommitted files, clean without), and no uncommitted file under
the build's own pathspec (WATCHED_PATHS) is newer than the stamp's build time. A deploy made before the last edit fails the run when
the edited file is still uncommitted (docs/features/seat-of-seeing.md names the cases only a content hash in the stamp could close),
and the PASS text names the build that was judged. Exit 1 is any failure, including a preflight refusal or an unreadable git; exit
2 is a bad argument; a --dry-run that exits 0 only means the preflight passed. A person still looks at the hud_*.png pictures the run
saves.

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
)
STEPS = SEAT_STEPS

STEP_LINE = re.compile(r"\[SeatTrial\] step=(\w+) result=(PASS|FAIL)\b")
SEAT_CHECK_FAIL = re.compile(r"\bid=(taom\.seat-of-seeing\.[a-z0-9.-]+) result=fail\b")
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


def judge(done, report_json, log_text=None, steps=STEPS, *, head, changed):
    """(exit code, why). A trial run has no JSON report: its evidence is the tagged log lines. The whole log counts for the CrashReport
    check: it opens at this launch, so a boot-time swallow is this run's too. head and changed are git_state(): the log must come from this
    checkout's build."""
    if not done:
        return 1, "no done file"
    reason = done.get("reason", "")
    if reason != "final":
        return 1, f"reason={reason or '?'}"
    if not log_text:
        return 1, "reason=final but no TAOM log was copied, and that log is the trial's evidence"
    stale = stale_build(log_text, head, changed)
    if stale is not None:
        return 1, "the game ran a build that is not this checkout: " + stale

    results = {}
    for line in log_text.splitlines():
        match = STEP_LINE.search(line)
        if match:
            results.setdefault(match.group(1), []).append(match.group(2))
    failed = sorted({step for step, seen in results.items() if "FAIL" in seen})
    other_fails = [line.strip() for line in log_text.splitlines() if "result=FAIL" in line and not STEP_LINE.search(line)]
    if failed or other_fails:
        named = ", ".join(failed + [line[:120] for line in other_fails])
        return 1, f"FAIL lines: {named}"
    missing = [step for step in steps if step not in results]
    if missing:
        return 1, "reason=final but the log has no [SeatTrial] line for: " + ", ".join(missing)
    block = runner.crash_report_line(log_text, None)
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block: " + block[:200]
    faults = sorted({match.group(1) for match in map(SEAT_CHECK_FAIL.search, log_text.splitlines()) if match})
    if faults:
        return 1, "a Seat check line failed: " + ", ".join(faults)
    if UNLOAD_LINE not in log_text:
        return 1, "reason=final but the TAOM log has no clean module unload line"
    stamp = BUILD_STAMP.search(log_text)
    build = f"{stamp.group(2)[:9]}{stamp.group(3) or ''} built {stamp.group(1)} UTC"
    return 0, f"reason=final, {len(steps)} steps with a PASS line, no FAIL line, no CrashReport block, no failed Seat check, clean unload, build {build}"


def normalised_steps(text):
    """The steps in the table's order, names lower-cased and trimmed, duplicates dropped. None means every step. A name not in the table raises
    ValueError. Prerequisites are the game's to check: it refuses to start on a list that skips one, and the done file says start_refused."""
    if text is None:
        return STEPS
    names = {name.strip().lower() for name in text.split(",") if name.strip()}
    if not names:
        raise ValueError("--steps names no step")
    unknown = sorted(names - set(STEPS))
    if unknown:
        raise ValueError("unknown step: " + ", ".join(unknown) + "; known: " + ", ".join(STEPS))
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
    parser.add_argument("--live-limit", type=runner.positive_seconds, default=DEFAULT_LIVE_LIMIT, help="mission seconds before the game ends the run (default %(default)s)")
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

    try:
        head, changed = git_state()
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
    runner.judge = functools.partial(judge, steps=steps, head=head, changed=changed)
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
