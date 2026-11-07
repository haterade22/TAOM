#!/usr/bin/env python
"""Unit tests for the git commit-msg checker (tools/check_commit_msg.py).

Run:  python tools/tests/test_check_commit_msg.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib. check() is tested on message text; the end-to-end test commits in a throwaway git
repository whose core.hooksPath points at the repo's .githooks folder.
"""
import io
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_commit_msg as ccm  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent
V = "v2.0.36"


SCISSORS = "# ------------------------ >8 ------------------------\n"
VERBOSE_TAIL = ("# Do not modify or remove the line above.\n# Everything below it will be ignored.\n"
                "diff --git a/x.md b/x.md\n+Closes #1, #2\n+Generated with Claude Code\n")


class ScissorsTests(unittest.TestCase):
    """git commit -v hands commit-msg the staged diff below git's scissors line."""

    def test_a_diff_below_the_scissors_line_is_not_the_message(self):
        self.assertEqual(ccm.check("fix: v2.0.36 - good\n\nBody.\n\n" + SCISSORS + VERBOSE_TAIL, V), [])

    def test_a_crlf_scissors_line_cuts_too(self):
        text = "fix: v2.0.36 - good\n\n" + SCISSORS + VERBOSE_TAIL
        self.assertEqual(ccm.check(text.replace("\n", "\r\n"), V), [])

    def test_another_comment_character_cuts_too(self):
        tail = (SCISSORS + VERBOSE_TAIL).replace("# ", "; ")
        self.assertEqual(ccm.check("fix: v2.0.36 - good\n\n" + tail, V), [])

    def test_the_same_text_above_the_scissors_line_is_refused(self):
        problems = ccm.check("fix: v2.0.36 - good\n\nCloses #1, #2\n\n" + SCISSORS + VERBOSE_TAIL, V)
        self.assertEqual(len(problems), 1)
        self.assertIn("closes only the first issue", problems[0])
        problems = ccm.check("fix: v2.0.36 - good\n\nGenerated with Claude Code\n\n" + SCISSORS, V)
        self.assertEqual(len(problems), 1)
        self.assertIn("AI attribution", problems[0])

    def test_a_subject_below_the_scissors_line_does_not_count(self):
        self.assertEqual(ccm.check(SCISSORS + "# Do not modify\nnot a subject\n", V), [])

    def test_a_lookalike_with_no_comment_line_below_it_cuts_nothing(self):
        for mark in ("x", "#"):
            line = SCISSORS.replace("# ", mark + " ", 1)
            problems = ccm.check("fix: v2.0.36 - good\n\n" + line + "Co-Authored-By: Claude <noreply@anthropic.com>\n", V)
            self.assertEqual(len(problems), 1, mark)
            self.assertIn("AI attribution", problems[0])
            self.assertEqual(len(ccm.check(line + "feat: no label\n", V)), 1, mark)

    def test_a_scissors_line_quoted_in_the_body_needs_its_exact_form(self):
        problems = ccm.check("fix: v2.0.36 - good\n\nSee -- >8 -- in git.\nCloses #1, #2\n", V)
        self.assertEqual(len(problems), 1)


class CheckTests(unittest.TestCase):
    def test_labelled_subject_with_the_version_passes(self):
        self.assertEqual(ccm.check("fix(map): v2.0.36 - Dunland names\n\nBody.\n", V), [])

    def test_subject_without_the_label_fails(self):
        problems = ccm.check("Add unit tests for various tools\n", V)
        self.assertEqual(len(problems), 1)
        self.assertIn("version label", problems[0])

    def test_subject_with_another_version_fails(self):
        problems = ccm.check("fix: v2.0.35 - old label\n", V)
        self.assertIn("v2.0.35", problems[0])
        self.assertIn(V, problems[0])

    def test_subject_over_72_characters_fails(self):
        subject = "docs: v2.0.36 - " + "x" * 57
        self.assertEqual(len(subject), 73)
        problems = ccm.check(subject + "\n", V)
        self.assertEqual(len(problems), 1)
        self.assertIn("73", problems[0])

    def test_subject_of_exactly_72_characters_passes(self):
        subject = "docs: v2.0.36 - " + "x" * 56
        self.assertEqual(ccm.check(subject + "\n", V), [])

    def test_ai_attribution_trailer_fails(self):
        msg = "fix: v2.0.36 - a fix\n\nBody.\n\nCo-Authored-By: Claude <noreply@anthropic.com>\n"
        problems = ccm.check(msg, V)
        self.assertEqual(len(problems), 1)
        self.assertIn("AI attribution", problems[0])

    def test_generated_with_claude_code_line_fails(self):
        msg = "fix: v2.0.36 - a fix\n\n\U0001F916 Generated with [Claude Code](https://claude.com)\n"
        self.assertIn("AI attribution", ccm.check(msg, V)[0])

    def test_human_co_author_passes(self):
        msg = "fix: v2.0.36 - a fix\n\nCo-Authored-By: Garrett <g@example.com>\n"
        self.assertEqual(ccm.check(msg, V), [])

    def test_git_comment_lines_are_ignored(self):
        msg = "# Please enter the commit message\nfeat: v2.0.36 - a feature\n# On branch x\n"
        self.assertEqual(ccm.check(msg, V), [])

    def test_autosquash_prefixes_pass(self):
        for prefix in ("fixup! ", "squash! ", "amend! "):
            self.assertEqual(ccm.check(prefix + "anything at all\n", V), [], prefix)

    def test_a_closing_keyword_with_a_number_list_fails(self):
        for body in ("Closes #1036, #1037", "fixes #1 ,#2", "Resolves #5,  #6, #7",
                     "Close #1, #2", "Closed #1, #2", "Fix #1, #2", "Fixed #1, #2", "Resolve #1, #2",
                     "Resolved #1, #2", "Closes #1 and #2", "Closes #1 & #2", "Closes #1 #2",
                     "Closes: #1, #2", "(closes #182 #187 #188)", "This fixes #1, #2 stays open"):
            problems = ccm.check("fix: v2.0.36 - a fix\n\n" + body + "\n", V)
            self.assertEqual(len(problems), 1, body)
            self.assertIn("keyword before each number", problems[0])

    def test_a_comma_and_then_and_is_still_a_number_list(self):
        for body in ("Closes #1, and #2", "Fixes #1 , and #2", "Resolves #5, #6, and #7"):
            problems = ccm.check("fix: v2.0.36 - a fix\n\n" + body + "\n", V)
            self.assertEqual(len(problems), 1, body)
            self.assertIn("keyword before each number", problems[0])

    def test_a_separator_never_crosses_a_line(self):
        self.assertEqual(ccm.check("fix: v2.0.36 - a fix\n\nCloses #1071\n\n#1072 is left open.\n", V), [])

    def test_a_double_backtick_span_naming_the_form_passes(self):
        body = "The gate refuses ``Closes #1, #2`` because it closes only #1."
        self.assertEqual(ccm.check("fix: v2.0.36 - a fix\n\n" + body + "\n", V), [])

    def test_a_code_span_naming_the_form_passes(self):
        for body in ("The gate refuses `Closes #1, #2` because it closes only #1.",
                     "Fixes #4. The old form `Closes #1 #2` is refused."):
            self.assertEqual(ccm.check("fix: v2.0.36 - a fix\n\n" + body + "\n", V), [], body)

    def test_a_number_after_a_keyword_that_is_not_an_issue_list_passes(self):
        msg = "fix: v2.0.36 - a fix\n\nFixes #4 100 lines of drift, closes #5\n"
        self.assertEqual(ccm.check(msg, V), [])

    def test_a_keyword_before_each_number_passes(self):
        msg = "fix: v2.0.36 - a fix\n\nCloses #1036, closes #1037\nFixes #4\n"
        self.assertEqual(ccm.check(msg, V), [])

    def test_empty_message_is_left_to_git(self):
        self.assertEqual(ccm.check("# only a comment\n\n", V), [])

    def test_unknown_version_checks_only_the_other_rules(self):
        self.assertEqual(ccm.check("Add things\n", None), [])
        self.assertIn("72", ccm.check("Add " + "x" * 80 + "\n", None)[0])


class EntryTests(unittest.TestCase):
    """Python exits 1 for an uncaught exception, the code that means "a finding"."""

    def test_a_crash_exits_three_with_the_traceback_on_stderr(self):
        err = io.StringIO()
        with mock.patch.object(ccm, "main", side_effect=RuntimeError("boom")), mock.patch.object(sys, "stderr", err):
            self.assertEqual(ccm.entry(["check_commit_msg.py", "msg.txt"]), 3)
        self.assertIn("RuntimeError: boom", err.getvalue())
        self.assertIn("exit 3", err.getvalue())

    def test_a_finding_still_exits_one_and_a_pass_zero(self):
        with tempfile.TemporaryDirectory() as tmp:
            msg = Path(tmp) / "msg.txt"
            for text, want in (("Add things\n", 1), ("fix: v2.0.36 - a fix\n", 0)):
                msg.write_text(text, encoding="utf-8")
                with mock.patch.object(ccm, "commit_version", return_value=V), mock.patch.object(sys, "stderr", io.StringIO()):
                    self.assertEqual(ccm.entry(["check_commit_msg.py", str(msg)]), want, text)

    def test_a_usage_error_still_exits_two(self):
        with mock.patch.object(sys, "stderr", io.StringIO()):
            self.assertEqual(ccm.entry(["check_commit_msg.py"]), 2)


@unittest.skipUnless(shutil.which("git"), "needs git")
class CommitVersionTests(unittest.TestCase):
    def test_an_unstaged_version_edit_is_not_the_version_the_commit_carries(self):
        tmp = tempfile.mkdtemp(prefix="taom_commitver_")
        self.addCleanup(shutil.rmtree, tmp, True)
        path = Path(tmp) / ccm.SUBMODULE
        path.parent.mkdir(parents=True)
        path.write_text('<Version value="v2.0.36"/>', encoding="utf-8")
        subprocess.run(["git", "init", "-q", tmp], check=True)
        subprocess.run(["git", "-C", tmp, "add", ccm.SUBMODULE], check=True)
        subprocess.run(["git", "-C", tmp, "-c", "user.name=t", "-c", "user.email=t@example.invalid",
                        "commit", "-q", "-m", "seed"], check=True)
        path.write_text('<Version value="v9.0.0"/>', encoding="utf-8")
        before = os.getcwd()
        self.addCleanup(os.chdir, before)
        os.chdir(tmp)
        self.assertEqual(ccm.commit_version(), "v2.0.36")


@unittest.skipUnless(shutil.which("git") and shutil.which("bash"), "needs git and bash")
class HookEndToEndTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="taom_commitmsg_")
        root = Path(self.tmp)
        (root / "Main" / "_Module").mkdir(parents=True)
        (root / "Main" / "_Module" / "SubModule.xml").write_text(
            '<Module><Version value="v2.0.36" /></Module>\n', encoding="utf-8")
        shutil.copytree(REPO / ".githooks", root / ".githooks")
        (root / "tools").mkdir()
        shutil.copy2(REPO / "tools" / "check_commit_msg.py", root / "tools" / "check_commit_msg.py")
        env = dict(os.environ, GIT_CONFIG_GLOBAL=os.devnull, GIT_CONFIG_NOSYSTEM="1")
        self.env = env
        for args in (["init", "-q"], ["config", "user.email", "t@example.com"], ["config", "user.name", "T"],
                     ["config", "core.hooksPath", ".githooks"], ["add", "Main"]):
            subprocess.run(["git", *args], cwd=root, check=True, env=env, capture_output=True)
        self.root = root

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def commit(self, message):
        return subprocess.run(["git", "commit", "-q", "-m", message], cwd=self.root, env=self.env,
                              capture_output=True, text=True)

    def test_hook_refuses_an_unlabelled_commit(self):
        r = self.commit("Add things")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("version label", r.stderr)

    def test_hook_accepts_a_labelled_commit(self):
        r = self.commit("chore: v2.0.36 - first commit")
        self.assertEqual(r.returncode, 0, r.stderr)


if __name__ == "__main__":
    unittest.main()
