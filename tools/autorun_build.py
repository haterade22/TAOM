#!/usr/bin/env python
"""tools/autorun_build.py: the one build-check rule of the three unattended game launchers (#1075 F5).

run_scenario.py, run_long_defeat.py and run_command_trial.py each read the checkout ONCE with checkout_state(), in main, before the game
starts (a git that cannot answer ends the launch with exit 1 and nothing started), and hand that state to their judge. The judge asks
stale_build() whether the log's [BuildStamp] is that checkout, and names the build with build_label(). A commit or an edit made while the
game runs is not read, so it cannot fail a good run.

stale_build says the log is not this checkout when: the log has no stamp with a git sha; the stamp line says MISMATCH; the stamp's sha is
not HEAD; the stamp's .dirty mark disagrees with the tree (dirty with no uncommitted watched file, clean with one); or an uncommitted watched
file is newer than the stamp's build time.

Tests: python -m unittest tools.tests.test_autorun_build
"""
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent

BUILD_STAMP = re.compile(r"\[BuildStamp\] TAOM=\S+ build\.(\d{8}-\d{6})Z\+([0-9a-f]{7,40})(\.dirty)?(?:\s|$)")
# The pathspec the build marks a stamp .dirty from (Directory.Build.props, target TaomStampWorkingTreeState); a test reads it out of there.
WATCHED_PATHS = ("Main", "Client", "CoopCompat", "Dependencies", "Stubs", "Directory.Build.props", "GameReferences.targets")


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


def build_label(log_text):
    """The log's build as "<sha9> built <yyyyMMdd-HHmmss> UTC" (with .dirty after the sha when the build had local edits), or None when
    the log has no stamp with a git sha. The time tells two dirty builds of one HEAD apart, for example between the runs of a batch."""
    match = BUILD_STAMP.search(log_text)
    return None if match is None else f"{match.group(2)[:9]}{match.group(3) or ''} built {match.group(1)} UTC"


def checkout_state(repo=REPO):
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


def read_checkout_for_launch():
    """(state, None) from checkout_state(), or (None, the refusal line to print) when git cannot answer. A launcher calls this once in
    main, before the game starts."""
    try:
        return checkout_state(), None
    except (OSError, subprocess.SubprocessError) as error:
        return None, f"cannot read the checkout from git ({error}); a verdict needs to know which build it judges, so nothing was started"
