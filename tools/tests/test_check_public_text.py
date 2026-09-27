#!/usr/bin/env python
"""Unit tests for the public-text gate (tools/check_public_text.py).

Run:  python tools/tests/test_check_public_text.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib. Each rule is shown firing on a fixture line and staying quiet on its allowed form.
"""
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_public_text as cpt  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "check_public_text.py"
EM, EN = "\u2014", "\u2013"


def rules(text):
    return [(line, rule) for line, rule, _ in cpt.check_text(text)]


class DashTests(unittest.TestCase):
    def test_an_em_dash_in_prose_is_flagged(self):
        self.assertEqual(rules(f"one {EM} two\n"), [(1, "em-dash")])

    def test_an_en_dash_in_prose_is_flagged(self):
        self.assertEqual(rules(f"pages 3{EN}4\n"), [(1, "en-dash")])

    def test_a_dash_inside_a_code_span_passes(self):
        self.assertEqual(rules(f"run `a {EM} b` first\n"), [])

    def test_a_dash_inside_a_fenced_block_passes(self):
        self.assertEqual(rules(f"```\na {EM} b\n```\n"), [])

    def test_a_dash_on_a_lint_allow_dash_line_passes(self):
        self.assertEqual(rules(f"a quote {EM} kept <!-- lint-allow-dash -->\n"), [])


class LocalPathTests(unittest.TestCase):
    def test_a_drive_letter_path_is_flagged(self):
        self.assertEqual(rules("see E:\\repos\\TAOM\\tools\n"), [(1, "local-path")])

    def test_a_users_folder_is_flagged(self):
        self.assertEqual(rules("from C:\\Users\\someone\\x.log\n"), [(1, "local-path")])

    def test_a_git_bash_repos_path_is_flagged(self):
        self.assertEqual(rules("cd /e/repos/TAOM\n"), [(1, "local-path")])

    def test_a_local_path_inside_a_code_span_is_still_flagged(self):
        self.assertEqual(rules("run `E:\\repos\\x.py`\n"), [(1, "local-path")])

    def test_a_repo_relative_path_and_a_url_pass(self):
        self.assertEqual(rules("tools/check_public_text.py and https://github.com/o/r/issues/1\n"), [])

    def test_forward_slash_drive_paths_are_flagged(self):
        for line in ("git worktree list printed E:/repos/taom-improve/wt-integrate",
                     "the log sits in C:/Users/someone/AppData/Local/Temp/x.log",
                     "a file URL file:///E:/repos/TAOM/docs/x.md",
                     "in a code span `E:/repos/TAOM/tools/x.py`"):
            self.assertEqual(rules(line + "\n"), [(1, "local-path")], line)

    def test_git_bash_drive_paths_are_flagged(self):
        for line in ("Git Bash home: /c/Users/someone/.claude/projects",
                     "the game: /e/Steam/steamapps/common/Bannerlord",
                     "scratch at /e/Temp/claude/x"):
            self.assertEqual(rules(line + "\n"), [(1, "local-path")], line)

    def test_drive_shaped_text_that_is_no_local_path_passes(self):
        for line in ("PowerShell drives `Env:\\TEMP` and `HKLM:\\SOFTWARE`",
                     'a C# string `"Error:\\n"` in a code span',
                     "https://example.com/a/b and http://x.org/c/d",
                     "and/or, src/a/b, docs/e/f.md, a ratio 3:2"):
            self.assertEqual(rules(line + "\n"), [], line)


class PlaceholderTests(unittest.TestCase):
    def test_a_leftover_placeholder_token_is_flagged(self):
        self.assertEqual(rules("Closed by MERGE_HASH on trunk.\n"), [(1, "placeholder")])

    def test_each_placeholder_suffix_is_flagged(self):
        text = "PLAN_ISSUE\nSUITE_TOTALS\nREVIEW_RESULT\nFEATURE_DOCS\nMERGE_HASH\n"
        self.assertEqual([r for _, r in rules(text)], ["placeholder"] * 5)

    def test_a_short_prefix_is_not_a_placeholder(self):
        self.assertEqual(rules("the ABC_HASH column\n"), [])

    def test_a_bare_todo_line_is_flagged(self):
        self.assertEqual(rules("Intro.\n  TODO\n"), [(2, "placeholder")])

    def test_todo_inside_a_sentence_passes(self):
        self.assertEqual(rules("The TODO list is empty.\n"), [])

    def test_a_placeholder_shaped_token_in_a_code_span_passes(self):
        self.assertEqual(rules("the constants `COMMIT_HASH` and ``TEST_RESULT`` in the diff\n"), [])
        self.assertEqual(rules("`TODO`\n"), [])

    def test_a_placeholder_beside_a_code_span_is_still_flagged(self):
        self.assertEqual(rules("`x` then MERGE_HASH\n"), [(1, "placeholder")])


class ScopeTests(unittest.TestCase):
    def test_the_words_claude_and_codex_pass(self):
        self.assertEqual(rules("Claude Code hooks and the Codex review both ran.\n"), [])

    def test_findings_keep_line_order_and_carry_the_line_text(self):
        text = f"clean\nE:\\x {EM} y\n"
        self.assertEqual(cpt.check_text(text),
                         [(2, "em-dash", f"E:\\x {EM} y"), (2, "local-path", f"E:\\x {EM} y")])

    def test_findings_are_in_line_order_across_rules(self):
        self.assertEqual(rules(f"MERGE_HASH\na {EM} b\n/e/repos/x\n"),
                         [(1, "placeholder"), (2, "em-dash"), (3, "local-path")])


class CliTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.dir = Path(self.temp.name)

    def write(self, name, text):
        path = self.dir / name
        path.write_text(text, encoding="utf-8")
        return path

    def run_tool(self, *paths):
        env = dict(os.environ, PYTHONIOENCODING="utf-8")
        return subprocess.run([sys.executable, "-B", str(TOOL), *map(str, paths)],
                              capture_output=True, env=env)

    def test_a_clean_file_exits_zero_and_prints_nothing(self):
        proc = self.run_tool(self.write("ok.md", "All good.\n"))
        self.assertEqual((proc.returncode, proc.stdout), (0, b""), proc.stderr)

    def test_a_finding_exits_one_as_file_line_rule_text(self):
        bad = self.write("bad.md", "fine\nsee /e/repos/TAOM\n")
        proc = self.run_tool(bad)
        self.assertEqual(proc.returncode, 1, proc.stderr)
        self.assertEqual(proc.stdout.decode("utf-8").splitlines(),
                         [f"{bad}:2: local-path: see /e/repos/TAOM"])

    def test_every_file_is_checked(self):
        proc = self.run_tool(self.write("a.md", "fine\n"), self.write("b.md", "TODO\n"))
        self.assertEqual(proc.returncode, 1)
        self.assertIn(b"b.md:1: placeholder: TODO", proc.stdout)

    def test_a_bom_is_not_part_of_the_first_line(self):
        path = self.dir / "bom.md"
        path.write_bytes(b"\xef\xbb\xbfTODO\n")
        self.assertEqual(self.run_tool(path).returncode, 1)

    def test_a_missing_file_exits_two(self):
        proc = self.run_tool(self.dir / "absent.md")
        self.assertEqual(proc.returncode, 2)
        self.assertIn(b"absent.md", proc.stderr)


if __name__ == "__main__":
    unittest.main()
