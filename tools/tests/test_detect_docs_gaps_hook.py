#!/usr/bin/env python
"""Tests for .claude/hooks/detect-docs-gaps.sh.

Run:  python -m unittest tools.tests.test_detect_docs_gaps_hook
"""
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
HOOK = REPO / ".claude" / "hooks" / "detect-docs-gaps.sh"
BASH = shutil.which("bash") or "C:/Program Files/Git/bin/bash.exe"


def run(features, docs, source="startup"):
    with tempfile.TemporaryDirectory() as root:
        for name in features:
            (Path(root) / "Main" / "Features" / name).mkdir(parents=True)
        (Path(root) / "docs" / "features").mkdir(parents=True)
        for doc in docs:
            (Path(root) / "docs" / "features" / f"{doc}.md").write_text("x", encoding="utf-8")
        env = dict(os.environ, CLAUDE_PROJECT_DIR=root)
        return subprocess.run([BASH, HOOK.as_posix()], input=json.dumps({"source": source}),
                              capture_output=True, text=True, encoding="utf-8", env=env)


class DetectDocsGapsHook(unittest.TestCase):
    def test_a_feature_without_a_doc_is_reported(self):
        result = run(["Autorun"], ["long-defeat"])
        self.assertEqual(result.returncode, 0)
        self.assertIn("Autorun → docs/features/autorun.md", result.stdout)

    def test_an_exact_doc_name_is_silent(self):
        self.assertEqual(run(["LongDefeat"], ["long-defeat"]).stdout.strip(), "")

    def test_a_doc_that_starts_with_the_feature_name_is_silent(self):
        self.assertEqual(run(["RaceAge"], ["race-age-system"]).stdout.strip(), "")

    def test_a_doc_that_ends_with_the_feature_name_is_silent(self):
        self.assertEqual(run(["Autorun"], ["scenario-autorun"]).stdout.strip(), "")

    def test_a_doc_that_only_contains_the_name_mid_word_is_reported(self):
        self.assertIn("Autorun", run(["Autorun"], ["scenarioautorun"]).stdout)

    def test_a_resume_is_silent(self):
        self.assertEqual(run(["Autorun"], [], source="resume").stdout.strip(), "")


if __name__ == "__main__":
    unittest.main()
