#!/usr/bin/env python
"""tools/autorun_build.py: the build-check rule the three unattended launchers share.

Run:  python -m unittest tools.tests.test_autorun_build
"""
import os
import re
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import autorun_build as ab  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
HEAD = "1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc"
BUILT = datetime(2026, 11, 4, 9, 11, 4)

# The stamp line of the first real run: UTC build time 09:11:04, the log's own clock 03:11 local.
STAMP_LINE = ("[2026-11-04 03:11:29] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261104-091104Z+1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc.dirty "
              "TAOM.Dependencies=v0.1.0.0 build.20261104-091104Z+1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc.dirty (pair OK)")
CLEAN_STAMP_LINE = STAMP_LINE.replace(".dirty", "")
OLDER_CHANGE = {"Main/a.cs": datetime(2026, 11, 4, 9, 0, 0)}


class StaleBuildTests(unittest.TestCase):
    """stale_build, each clause in both directions, with the stamp line of the first real run as the passing sample."""

    def test_a_log_with_no_stamp_line_is_stale(self):
        self.assertIn("BuildStamp", ab.stale_build("[2026-11-04 10:00:00] [INFO] nothing here", HEAD, {}))
        self.assertIn("BuildStamp", ab.stale_build("[2026-11-04 10:00:00] [INFO] [BuildStamp] sample", HEAD, {}), "a stamp with no git sha")

    def test_the_real_stamp_line_is_the_one_the_pattern_reads(self):
        match = ab.BUILD_STAMP.search(STAMP_LINE)
        self.assertIsNotNone(match)
        self.assertEqual(match.group(1), "20261104-091104")
        self.assertEqual(match.group(2), HEAD)
        self.assertEqual(match.group(3), ".dirty")

    def test_another_sha_is_stale_and_names_both(self):
        reason = ab.stale_build(STAMP_LINE.replace(HEAD, "0000000deadbeef"), HEAD, {})
        self.assertIn("0000000de", reason)
        self.assertIn("1735f2add", reason)

    def test_the_same_sha_is_not_stale_and_a_short_sha_matches_by_prefix(self):
        self.assertIsNone(ab.stale_build(CLEAN_STAMP_LINE, HEAD, {}))
        self.assertIsNone(ab.stale_build(CLEAN_STAMP_LINE.replace(HEAD, "1735f2a"), HEAD, {}))

    def test_a_changed_file_newer_than_the_build_is_stale_and_named(self):
        changed = {"Main/a.cs": datetime(2026, 11, 4, 9, 11, 5), "Client/b.cs": datetime(2026, 11, 4, 9, 0, 0)}
        reason = ab.stale_build(STAMP_LINE, HEAD, changed)
        self.assertIn("Main/a.cs", reason)
        self.assertNotIn("Client/b.cs", reason)

    def test_a_changed_file_older_than_the_build_is_not_stale(self):
        self.assertIsNone(ab.stale_build(STAMP_LINE, HEAD, {"Main/a.cs": datetime(2026, 11, 4, 9, 11, 3)}))

    def test_a_file_at_exactly_the_stamps_time_is_not_stale(self):
        self.assertIsNone(ab.stale_build(STAMP_LINE, HEAD, {"Main/a.cs": BUILT}))

    MISMATCH_TAIL = (" TAOM.Dependencies=v0.1.0.0 build.20261104-091104Z+" + HEAD + " MISMATCH \u2014 built 2d 03h 10m apart. These modules were not built "
                     "together; update BOTH from the same release or expect the preview patches to fail (issue #371).")
    REBUILT_TAIL = (" (pair rebuilt 0d 00h 12m apart \u2014 one module was rebuilt after the other, which is normal mid-session and is usually a docs or "
                    "config commit. Rebuild both if the preview patches misbehave. Issue #371.)")

    def stamp(self, tail):
        return CLEAN_STAMP_LINE.split(" TAOM.Dependencies=")[0] + tail

    def test_a_stamp_marked_mismatch_is_stale(self):
        """The tail is on the tier that was built apart: the TAOM= tier the pattern reads can look right while the pair does not."""
        self.assertIn("MISMATCH", ab.stale_build(self.stamp(self.MISMATCH_TAIL), HEAD, {}))

    def test_a_stamp_with_the_pair_rebuilt_tail_is_not_stale(self):
        self.assertIsNone(ab.stale_build(self.stamp(" TAOM.Dependencies=v0.1.0.0 build.20261104-091104Z+" + HEAD + self.REBUILT_TAIL), HEAD, {}))

    def test_a_stamp_line_with_no_sha_before_the_real_one_does_not_hide_the_mismatch(self):
        no_sha = "[2026-11-04 03:11:00] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261104-091104Z (no git sha)"
        reason = ab.stale_build(no_sha + "\n" + self.stamp(self.MISMATCH_TAIL), HEAD, {})
        self.assertIsNotNone(reason)
        self.assertIn("MISMATCH", reason)

    def test_the_word_mismatch_on_another_line_is_not_the_stamps(self):
        other = "[2026-11-04 10:00:00] [WARNING] [Player] MISMATCH: no snapshot at all."
        self.assertIsNone(ab.stale_build(other + "\n" + CLEAN_STAMP_LINE, HEAD, {}))
        self.assertIsNone(ab.stale_build(CLEAN_STAMP_LINE + "\n" + other, HEAD, {}))

    def test_a_nogit_stamp_is_refused(self):
        reason = ab.stale_build(CLEAN_STAMP_LINE.replace(HEAD, HEAD + ".nogit", 1), HEAD, {})
        self.assertIsNotNone(reason)
        self.assertIn("[BuildStamp]", reason)

    def test_a_clean_match_with_no_changed_file_is_not_stale(self):
        self.assertIsNone(ab.stale_build(CLEAN_STAMP_LINE, HEAD, {}))

    def test_a_dirty_build_of_a_tree_that_has_no_change_is_stale(self):
        """A dirty build, then a restore; or a sibling worktree at the same HEAD with edits of its own."""
        reason = ab.stale_build(STAMP_LINE, HEAD, {})
        self.assertIn("uncommitted edits", reason)
        self.assertIn("this tree has none", reason)

    def test_a_clean_build_of_a_tree_with_a_change_older_than_the_build_is_stale(self):
        """A file restored with its old time still differs from HEAD, and a clean build cannot have compiled it."""
        reason = ab.stale_build(CLEAN_STAMP_LINE, HEAD, OLDER_CHANGE)
        self.assertIn("clean build", reason)
        self.assertIn("Main/a.cs", reason)

    def test_a_clean_build_of_a_tree_whose_only_change_is_a_deletion_is_stale(self):
        self.assertIn("clean build", ab.stale_build(CLEAN_STAMP_LINE, HEAD, {"Main/gone.cs": datetime.min}))

    def test_a_dirty_build_with_an_older_change_is_the_real_run_and_is_not_stale(self):
        self.assertIsNone(ab.stale_build(STAMP_LINE, HEAD, OLDER_CHANGE))


class BuildLabelTests(unittest.TestCase):

    def test_a_clean_stamp_is_the_sha_cut_to_nine(self):
        self.assertEqual(ab.build_label(CLEAN_STAMP_LINE), "1735f2add built 20261104-091104 UTC")

    def test_a_dirty_stamp_adds_the_dirty_mark(self):
        self.assertEqual(ab.build_label(STAMP_LINE), "1735f2add.dirty built 20261104-091104 UTC")

    def test_no_stamp_is_none(self):
        self.assertIsNone(ab.build_label("[2026-11-04 10:00:00] [INFO] nothing here"))
        self.assertIsNone(ab.build_label("[2026-11-04 10:00:00] [INFO] [BuildStamp] sample"))


class CheckoutStateTests(unittest.TestCase):

    def test_the_state_of_this_checkout_is_a_head_and_a_dict(self):
        head, changed = ab.checkout_state()
        self.assertRegex(head, r"^[0-9a-f]{40}$")
        self.assertIsInstance(changed, dict)

    def test_the_command_asks_for_no_renames_and_takes_no_optional_lock_over_the_builds_own_paths(self):
        calls = []

        def fake_run(command, **kwargs):
            calls.append(command)
            return SimpleNamespace(returncode=0, stdout=HEAD + "\n" if "rev-parse" in command else "", stderr="")

        with mock.patch.object(ab.subprocess, "run", side_effect=fake_run):
            ab.checkout_state(REPO)

        status = next(command for command in calls if "status" in command)
        self.assertIn("--no-optional-locks", status)
        self.assertIn("--no-renames", status)
        self.assertEqual(tuple(status[status.index("--") + 1:]), ab.WATCHED_PATHS)

    def test_a_real_repository_reports_an_edit_a_staged_rename_an_untracked_file_and_a_deletion(self):
        """Real git in a temp repo. The hooks path, the identity and the config files are given or cleared on the command line, so
        nothing of the machine reaches the repo. A deleted path, and the old path of a rename, are changes with no time."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            options = ["-c", "core.hooksPath=", "-c", "user.name=Trial", "-c", "user.email=trial@example.invalid", "-c", "commit.gpgsign=false",
                       "-c", "core.autocrlf=false"]
            environment = {**os.environ, "GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1"}

            def git(*args):
                result = subprocess.run(["git", "-C", tmp, *options, *args], capture_output=True, text=True, encoding="utf-8", timeout=60, env=environment)
                self.assertEqual(result.returncode, 0, result.stderr)
                return result.stdout

            for name in ("Main/edit.cs", "Main/old.cs", "Client/gone.cs", "Dependencies/quiet.cs"):
                (repo / name).parent.mkdir(parents=True, exist_ok=True)
                (repo / name).write_text(f"// {name}, long enough for git to see a rename\n" * 12, encoding="utf-8")
            git("init", "-q")
            git("add", "-A")
            git("commit", "-q", "-m", "base")

            (repo / "Main/edit.cs").write_text("// edited\n", encoding="utf-8")
            git("mv", "Main/old.cs", "Main/new.cs")
            # A new folder, so only -uall lists the file: a plain status shows the folder and not what is in it.
            (repo / "Client/New").mkdir()
            (repo / "Client/New/fresh.cs").write_text("// untracked\n", encoding="utf-8")
            (repo / "Client/gone.cs").unlink()
            for name in ("Main/edit.cs", "Main/new.cs", "Client/New/fresh.cs"):
                os.utime(repo / name, (1762247465, 1762247465))
            self.assertIn("R  Main/old.cs -> Main/new.cs", git("status", "--porcelain", "-uall"), "the fixture must hold a staged rename")

            head, changed = ab.checkout_state(repo)

        self.assertRegex(head, r"^[0-9a-f]{40}$")
        self.assertEqual(sorted(changed), ["Client/New/fresh.cs", "Client/gone.cs", "Main/edit.cs", "Main/new.cs", "Main/old.cs"])
        for name in ("Main/edit.cs", "Main/new.cs", "Client/New/fresh.cs"):
            self.assertEqual(changed[name], datetime(2025, 11, 4, 9, 11, 5), name)
        for name in ("Client/gone.cs", "Main/old.cs"):
            self.assertEqual(changed[name], datetime.min, name)

    def test_the_watched_paths_are_the_ones_the_build_stamps_its_dirty_mark_from(self):
        props = (REPO / "Directory.Build.props").read_text(encoding="utf-8")
        match = re.search(r"git --no-optional-locks status [^\"]*? -- ([^\"]*?) 2&gt;nul", props)
        self.assertIsNotNone(match, "the stamp target's git status command was not found in Directory.Build.props")
        self.assertEqual(tuple(match.group(1).split()), ab.WATCHED_PATHS)

    def test_a_git_that_fails_raises_instead_of_answering_empty(self):
        with mock.patch.object(ab.subprocess, "run", return_value=SimpleNamespace(returncode=128, stdout="", stderr="fatal: not a git repository")):
            with self.assertRaises(OSError):
                ab.checkout_state(REPO)


class ReadCheckoutForLaunchTests(unittest.TestCase):

    def test_an_answer_is_returned_with_no_refusal(self):
        with mock.patch.object(ab, "checkout_state", return_value=(HEAD, {})):
            self.assertEqual(ab.read_checkout_for_launch(), ((HEAD, {}), None))

    def test_a_git_that_fails_or_times_out_is_a_refusal_naming_the_reason(self):
        for error in (OSError("git exited 128"), subprocess.TimeoutExpired("git", 60)):
            with self.subTest(error=type(error).__name__), mock.patch.object(ab, "checkout_state", side_effect=error):
                state, refusal = ab.read_checkout_for_launch()
            self.assertIsNone(state)
            self.assertIn("cannot read the checkout from git", refusal)
            self.assertIn("nothing was started", refusal)


if __name__ == "__main__":
    unittest.main()
