#!/usr/bin/env python
"""Unit tests for the git commit-msg checker (tools/check_commit_msg.py).

Run:  python tools/tests/test_check_commit_msg.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib. check() is tested on message text; the end-to-end test commits in a throwaway git
repository whose core.hooksPath points at the repo's .githooks folder.
"""
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_commit_msg as ccm  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent
V = "v2.0.36"


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

    def test_empty_message_is_left_to_git(self):
        self.assertEqual(ccm.check("# only a comment\n\n", V), [])

    def test_unknown_version_checks_only_the_other_rules(self):
        self.assertEqual(ccm.check("Add things\n", None), [])
        self.assertIn("72", ccm.check("Add " + "x" * 80 + "\n", None)[0])


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
