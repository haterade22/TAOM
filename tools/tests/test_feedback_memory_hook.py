#!/usr/bin/env python
"""Tests for .claude/hooks/check-feedback-memory.sh.

Run:  python -m unittest tools.tests.test_feedback_memory_hook
"""
import json
import shutil
import subprocess
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
HOOK = REPO / ".claude" / "hooks" / "check-feedback-memory.sh"
BASH = shutil.which("bash") or "C:/Program Files/Git/bin/bash.exe"
MEMORY = r"C:\Users\x\.claude\projects\e--repos-TAOM\memory"


def run(tool="Write", path=MEMORY + r"\feedback_new.md", kind="create", content="a rule"):
    payload = {"tool_name": tool, "tool_input": {"file_path": path, "content": content},
               "tool_response": {"type": kind, "filePath": path}}
    return subprocess.run([BASH, HOOK.as_posix()], input=json.dumps(payload), capture_output=True, text=True)


class FeedbackMemoryHook(unittest.TestCase):
    def assert_nudged(self, result):
        self.assertEqual(result.returncode, 0)
        output = json.loads(result.stdout)["hookSpecificOutput"]
        self.assertEqual(output["hookEventName"], "PostToolUse")
        self.assertIn("goes in the repo", output["additionalContext"])

    def assert_silent(self, result):
        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stdout.strip(), "")

    def test_a_new_feedback_memory_gets_the_nudge(self):
        self.assert_nudged(run())

    def test_a_forward_slash_path_gets_the_nudge(self):
        self.assert_nudged(run(path="C:/Users/x/.claude/projects/e--repos-TAOM/memory/feedback_new.md"))

    def test_an_update_of_an_existing_card_is_silent(self):
        self.assert_silent(run(kind="update"))

    def test_an_edit_is_silent(self):
        self.assert_silent(run(tool="Edit"))

    def test_another_memory_file_is_silent(self):
        self.assert_silent(run(path=MEMORY + r"\features\rules-review.md"))

    def test_a_repo_file_named_feedback_is_silent(self):
        self.assert_silent(run(path=r"E:\repos\TAOM\docs\reviews\feedback_notes.md"))

    def test_a_write_that_only_mentions_a_feedback_memory_is_silent(self):
        self.assert_silent(run(path=r"E:\repos\TAOM\docs\a.md", content=r"see memory\feedback_new.md"))


if __name__ == "__main__":
    unittest.main()
