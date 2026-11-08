#!/usr/bin/env python3
"""Tests for tools/run_skill_evals.py: the staged plugin, the paid-call guards and the committed cases."""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import run_skill_evals as rse  # noqa: E402

CLAUDE_MD = """# CLAUDE.md

## Skills

| When | Invoke |
|---|---|
| Crash | `/investigate` |

## Subagents

- Brief every spawn.
"""

CASE = """schema_version: "1.1"
name: sample
plugins: ["../.."]
execution:
  append_system_prompt: "@@CLAUDE_SKILLS_TABLE@@"
  prompt: "The game crashed."
graders:
  - name: fired
    type: tool_used
    tool: Skill
    input_match: '"skill"\\s*:\\s*"(?:[\\w-]+:)?investigate"'
"""


def make_repo(root: Path) -> Path:
    (root / ".claude" / "skills" / "investigate" / "references").mkdir(parents=True)
    (root / ".claude" / "skills" / "investigate" / "SKILL.md").write_text("---\nname: investigate\n---\nbody\n", encoding="utf-8")
    (root / ".claude" / "skills" / "investigate" / "references" / "long.md").write_text("reference", encoding="utf-8")
    (root / ".claude" / "skills" / "investigate" / "hook.sh").write_text("exit 0", encoding="utf-8")
    (root / "CLAUDE.md").write_text(CLAUDE_MD, encoding="utf-8")
    (root / "tools" / "evals" / "sample").mkdir(parents=True)
    (root / "tools" / "evals" / "sample" / "case.yaml").write_text(CASE, encoding="utf-8")
    (root / "tools" / "evals" / "sample" / "fixture.sh").write_text("true\n", encoding="utf-8")
    return root


class SkillsSectionTests(unittest.TestCase):
    def test_takes_only_the_skills_section(self):
        section = rse.skills_section(CLAUDE_MD)
        self.assertIn("/investigate", section)
        self.assertNotIn("Brief every spawn", section)

    def test_a_missing_section_is_an_error(self):
        with self.assertRaises(ValueError):
            rse.skills_section("# CLAUDE.md\n\n## Other\n")


class StageTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.repo = make_repo(Path(tmp.name) / "repo")
        self.dest = Path(tmp.name) / "plugin"
        rse.stage_plugin(self.repo, self.dest)

    def test_manifest_is_valid_json_with_the_plugin_name(self):
        manifest = json.loads((self.dest / ".claude-plugin" / "plugin.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["name"], rse.PLUGIN_NAME)

    def test_copies_each_skill_body_and_nothing_else_from_the_skill(self):
        self.assertTrue((self.dest / "skills" / "investigate" / "SKILL.md").is_file())
        self.assertFalse((self.dest / "skills" / "investigate" / "hook.sh").exists())
        self.assertFalse((self.dest / "skills" / "investigate" / "references").exists())

    def test_fills_the_placeholder_with_a_yaml_safe_table(self):
        staged = (self.dest / "evals" / "sample" / "case.yaml").read_text(encoding="utf-8")
        self.assertNotIn(rse.PLACEHOLDER, staged)
        line = next(x for x in staged.splitlines() if "append_system_prompt" in x)
        self.assertIn("/investigate", json.loads(line.split(":", 1)[1].strip()))

    def test_copies_case_support_files(self):
        self.assertTrue((self.dest / "evals" / "sample" / "fixture.sh").is_file())


class CapTests(unittest.TestCase):
    def test_accepts_a_positive_cap(self):
        self.assertEqual(rse.positive_cost("0.50"), 0.5)

    def test_rejects_every_value_that_is_not_a_real_cap(self):
        for bad in ("0", "-1", "nan", "inf", "abc"):
            with self.subTest(bad=bad), self.assertRaises(Exception):
                rse.positive_cost(bad)

    def test_rejects_zero_runs(self):
        with self.assertRaises(Exception):
            rse.positive_runs("0")

    def test_no_cap_never_reaches_the_paid_call(self):
        with mock.patch.object(rse.subprocess, "run", side_effect=AssertionError("paid call")), \
                mock.patch.object(sys, "stderr"), self.assertRaises(SystemExit):
            rse.main(["--out", "E:/nowhere"])

    def test_a_zero_cap_never_reaches_the_paid_call(self):
        with mock.patch.object(rse.subprocess, "run", side_effect=AssertionError("paid call")), \
                mock.patch.object(sys, "stderr"), self.assertRaises(SystemExit):
            rse.main(["--max-cost-usd", "0", "--out", "E:/nowhere"])


class CommandTests(unittest.TestCase):
    def build(self, **kw):
        args = {"max_cost": 1.5, "runs": 1, "model": "sonnet", "case": None}
        args.update(kw)
        return rse.build_command(Path("plugin"), Path("out"), **args)

    def test_never_publishes_and_always_carries_the_cap(self):
        cmd = self.build()
        self.assertIn("--no-publish", cmd)
        self.assertEqual(cmd[cmd.index("--max-cost-usd") + 1], "1.5")

    def test_has_no_baseline_arm(self):
        cmd = self.build()
        self.assertEqual(cmd[cmd.index("--ablation") + 1], "none")

    def test_writes_results_only_under_out(self):
        cmd = self.build()
        self.assertEqual(Path(cmd[cmd.index("--output-dir") + 1]), Path("out"))
        self.assertEqual(Path(cmd[cmd.index("--report") + 1]).parent, Path("out"))

    def test_case_filter_is_optional(self):
        self.assertNotIn("--case", self.build())
        cmd = self.build(case="route-*")
        self.assertEqual(cmd[cmd.index("--case") + 1], "route-*")


class CommittedCaseTests(unittest.TestCase):
    """Free gate: a renamed or deleted skill fails here instead of in a paid run."""
    REPO = Path(__file__).resolve().parents[2]

    def cases(self):
        return sorted((self.REPO / "tools" / "evals").glob("*/case.yaml"))

    def test_there_are_cases(self):
        self.assertTrue(self.cases())

    def test_every_named_skill_exists(self):
        for case in self.cases():
            for name in rse.named_skills(case.read_text(encoding="utf-8")):
                with self.subTest(case=case.parent.name, skill=name):
                    self.assertTrue((self.REPO / ".claude" / "skills" / name / "SKILL.md").is_file())

    def test_every_case_is_named_after_its_folder_and_has_a_grader(self):
        import yaml
        for case in self.cases():
            data = yaml.safe_load(case.read_text(encoding="utf-8"))
            with self.subTest(case=case.parent.name):
                self.assertEqual(data["name"], case.parent.name)
                self.assertTrue(data["graders"])

    def test_every_routing_case_grades_its_skill(self):
        for case in self.cases():
            if case.parent.name.startswith("route-"):
                with self.subTest(case=case.parent.name):
                    self.assertTrue(rse.named_skills(case.read_text(encoding="utf-8")))


if __name__ == "__main__":
    unittest.main()
