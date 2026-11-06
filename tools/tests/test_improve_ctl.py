#!/usr/bin/env python
"""Unit tests for the /improve run control tool (tools/improve_ctl.py).

Run:  python tools/tests/test_improve_ctl.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib, no network. The git-backed subcommands run against throwaway repositories built in a
temp directory, with the machine's git config isolated (GIT_CONFIG_GLOBAL is an empty file,
GIT_CONFIG_NOSYSTEM=1). file-issue runs with an injected runner in place of gh, so no issue is ever
filed. watch runs on a fixture transcript folder with fake journal rows and set file times.
"""
import contextlib
import datetime
import io
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import improve_ctl as ctl  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "improve_ctl.py"
REPO_ROOT = TOOL.parent.parent
REDIRECTING_GIT_VARS = ("GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR",
                        "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES")
RULES_BYTES = "# Dispatch rules\r\n\r\n1. HOOK-ASK. Caf\u00e9 text.\r\n".encode("utf-8")
# The layout of .claude/skills/review-codex/references/prompt-fixed.md: a preface, then one fenced
# block under each `## ` heading; the block's first line repeats the label.
PROMPT_FIXED = (
    "# Codex prompt: fixed blocks\n"
    "\n"
    "The TAOM ID CHEATSHEET and the lessons below are pasted as written; this line is preface.\n"
    "Prior review lessons: this preface line names a label too.\n"
    "\n"
    "## TAOM ID CHEATSHEET\n"
    "\n"
    "```text\n"
    "TAOM ID CHEATSHEET:\n"
    "Kingdom IDs: vlandia=Rohan\n"
    "NOTE: \"rohan\" is NOT a valid ID.\n"
    "```\n"
    "\n"
    "## Prior review lessons\n"
    "\n"
    "```text\n"
    "Prior review lessons:\n"
    "SUCCESSES: config ID cross-reference.\n"
    "FAILURES: assuming empire=Rohan.\n"
    "```\n"
)
CHEATSHEET = "TAOM ID CHEATSHEET:\nKingdom IDs: vlandia=Rohan\nNOTE: \"rohan\" is NOT a valid ID."
LESSONS = "Prior review lessons:\nSUCCESSES: config ID cross-reference.\nFAILURES: assuming empire=Rohan."
PLAN = (
    "# Plan 012: Stop the trace walking the stack\n"
    "\n"
    "## Why this matters\n"
    "Every frame pays for a stack walk.\n"
    "\n"
    "## STOP conditions\n"
    "\n"
    "Stop and report back if:\n"
    "\n"
    "- The trace is read by a crash handler\n"
    "  that runs after the log closes.\n"
    "- `LoadingWindow` changed shape.\n"
    "\n"
    "## Steps\n"
    "- not a stop condition\n"
)


def same_path(a, b):
    return os.path.normcase(os.path.realpath(a)) == os.path.normcase(os.path.realpath(b))


class FixtureRepo(unittest.TestCase):
    """A throwaway main checkout at <tmp>/main with an isolated git config."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.tmp = Path(self.temp.name)
        empty = self.tmp / "gitconfig-empty"
        empty.write_text("", encoding="utf-8")
        self.env = {k: v for k, v in os.environ.items() if k not in REDIRECTING_GIT_VARS}
        self.env.pop("TAOM_IMPROVE_ROOT", None)
        self.env.update(GIT_CONFIG_GLOBAL=str(empty), GIT_CONFIG_NOSYSTEM="1",
                        PYTHONIOENCODING="utf-8")
        self.main = self.tmp / "main"
        self.main.mkdir()
        self.git(self.main, "init", "-q", "-b", "main")
        self.git(self.main, "config", "user.email", "test@example.invalid")
        self.git(self.main, "config", "user.name", "improve_ctl test")
        self.git(self.main, "config", "core.autocrlf", "false")

    def git(self, cwd, *args):
        return subprocess.run(["git", *args], cwd=cwd, env=self.env, capture_output=True,
                              check=True).stdout.decode("utf-8").strip()

    def write(self, root, rel, data):
        path = Path(root) / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data if isinstance(data, bytes) else data.encode("utf-8"))
        return path

    def commit(self, cwd, message, files):
        for rel, data in files.items():
            self.write(cwd, rel, data)
        self.git(cwd, "add", "--", *files)
        self.git(cwd, "commit", "-q", "-m", message)
        return self.git(cwd, "rev-parse", "HEAD")

    def run_tool(self, *args, cwd=None, env=None):
        return subprocess.run([sys.executable, "-B", str(TOOL), *map(str, args)],
                              cwd=cwd or self.main, env=env or self.env, capture_output=True)


class LensRoutingTests(unittest.TestCase):
    """deep-review SKILL.md Step 2 "Which lenses run"."""

    def test_csharp_only_runs_the_code_lenses(self):
        self.assertEqual(ctl.lenses_for(["Main/Features/X/Service.cs"]),
                         ["1", "2", "3", "4", "5", "6"])

    def test_a_hook_only_change_gets_standards_as_well_as_tooling(self):
        self.assertEqual(ctl.lenses_for([".claude/hooks/validate-push.sh"]),
                         ["1", "3", "4", "5", "6", "tooling"])

    def test_a_tools_script_runs_efficiency_and_tooling(self):
        self.assertEqual(ctl.lenses_for(["tools/lint_docs.py"]),
                         ["3", "4", "5", "6", "tooling"])

    def test_xml_only_adds_the_xml_lens(self):
        self.assertEqual(ctl.lenses_for(["Main/_Module/ModuleData/spnpccharacters.xml"]),
                         ["4", "5", "6", "7"])

    def test_harness_files_add_standards(self):
        for path in (".claude/skills/improve/SKILL.md", "CLAUDE.md", "AGENTS.md", ".ai/policy.md"):
            self.assertEqual(ctl.lenses_for([path]), ["1", "4", "5", "6"], path)

    def test_docs_only_runs_the_three_lenses_that_always_run(self):
        self.assertEqual(ctl.lenses_for(["docs/features/x.md", "tools/README.md"]),
                         ["4", "5", "6"])

    def test_a_mixed_change_runs_every_lens(self):
        self.assertEqual(ctl.lenses_for(["Main/X.cs", "a.xslt", "tools/t.py"]),
                         ["1", "2", "3", "4", "5", "6", "7", "tooling"])

    def test_files_are_grouped_by_kind(self):
        self.assertEqual(ctl.group_files(["Main/X.cs", "Native/y.cpp", "a.xml", "tools/t.py",
                                          ".claude/hooks/h.sh", ".claude/rules/r.md",
                                          "CLAUDE.md", "docs/d.md", "tools/README.md"]),
                         {"cs": ["Main/X.cs", "Native/y.cpp"], "xml": ["a.xml"],
                          "scripts": ["tools/t.py", ".claude/hooks/h.sh"],
                          "harness": [".claude/rules/r.md", "CLAUDE.md"],
                          "docs": ["docs/d.md", "tools/README.md"]})


class ArgsTests(FixtureRepo):
    def setUp(self):
        super().setUp()
        self.base = self.commit(self.main, "base", {
            "Main/_Module/SubModule.xml": '<Module>\r\n  <Version value="v9.8.7" />\r\n</Module>\r\n',
            ctl.RULES_PATH: RULES_BYTES,
            "README.md": "x\n"})
        self.wt = self.tmp / "wt-042"
        self.git(self.main, "worktree", "add", "-q", "-b", "improve/042-x", str(self.wt))
        self.hook_only = self.commit(self.wt, "hook", {".claude/hooks/guard.sh": "echo\n"})
        self.head = self.commit(self.wt, "rest", {"Main/Foo.cs": "class Foo {}\n",
                                                  "Main/_Module/ModuleData/x.xml": "<x/>\n",
                                                  "docs/features/foo.md": "doc\n",
                                                  "tools/foo.py": "print()\n"})
        self.root = self.tmp / "improve-root"
        self.run_root = self.tmp / "plans" / "_audit" / "run"
        self.items = self.write(self.tmp, "items.json", json.dumps(
            [{"num": "042", "wt": str(self.wt), "base": self.base, "head": self.hook_only},
             {"num": "043", "wt": str(self.wt), "base": self.base}]))

    def run_args(self, *extra, cwd=None, root=True, run_root=True):
        """`args` with TAOM_IMPROVE_ROOT set (root) and --run-root passed (run_root), as the skill does."""
        env = dict(self.env, TAOM_IMPROVE_ROOT=str(self.root)) if root else self.env
        folder = ("--run-root", self.run_root) if run_root else ()
        return self.run_tool("args", *extra, *folder, cwd=cwd, env=env)

    def args_json(self, *extra, cwd=None, root=True, run_root=True):
        out = self.tmp / "args.json"
        proc = self.run_args(*extra, "--out", out, cwd=cwd, root=root, run_root=run_root)
        self.assertEqual(proc.returncode, 0, proc.stderr.decode("utf-8", "replace"))
        return json.loads(out.read_text(encoding="utf-8"))

    def test_review_items_gain_head_files_and_lenses_from_their_worktree(self):
        data = self.args_json("review", "--items", self.items)
        first, second = data["items"]
        self.assertEqual(first["head"], self.hook_only)
        self.assertEqual(first["files"]["scripts"], [".claude/hooks/guard.sh"])
        self.assertEqual(first["lenses"], ["1", "3", "4", "5", "6", "tooling"])
        self.assertEqual(second["head"], self.head)
        self.assertEqual(second["files"], {
            "cs": ["Main/Foo.cs"], "xml": ["Main/_Module/ModuleData/x.xml"],
            "scripts": [".claude/hooks/guard.sh", "tools/foo.py"], "harness": [],
            "docs": ["docs/features/foo.md"]})
        self.assertEqual(second["lenses"], ["1", "2", "3", "4", "5", "6", "7", "tooling"])

    def test_the_rules_file_is_embedded_verbatim(self):
        data = self.args_json("review", "--items", self.items)
        self.assertEqual(data["rules"], RULES_BYTES.decode("utf-8"))

    def test_defaults_come_from_the_checkout_git_and_the_environment(self):
        before = datetime.date.today().isoformat()
        data = self.args_json("execute", "--items", self.items, cwd=self.wt)
        self.assertIn(data["date"], {before, datetime.date.today().isoformat()})
        self.assertEqual(data["version"], "v9.8.7")
        self.assertTrue(same_path(data["repo"], self.main), data["repo"])
        self.assertTrue(same_path(data["runRoot"], self.run_root))
        self.assertTrue(same_path(data["scratch"], self.root / "scratch"))
        self.assertTrue(same_path(data["tmp"], self.root / "scratch" / "tmp"))
        self.assertEqual((data["pool"], data["model"]), (4, {}))
        self.assertNotIn("maxRounds", data)

    def test_other_workflows_pass_items_through_unchanged(self):
        data = self.args_json("fanout", "--items", self.items)
        self.assertEqual(data["items"], json.loads(self.items.read_text(encoding="utf-8")))

    def test_flags_override_the_defaults(self):
        data = self.args_json("review", "--items", self.items, "--date", "2030-01-02",
                              "--version", "v1.2.3", "--repo", self.tmp, "--run-root", self.tmp / "r",
                              "--scratch", self.tmp / "s", "--tmp", self.tmp / "t", "--pool", "2",
                              "--max-rounds", "3", "--model", "reviewer=m-a", "lead=m-b",
                              root=False, run_root=False)
        self.assertEqual((data["date"], data["version"], data["pool"], data["maxRounds"]),
                         ("2030-01-02", "v1.2.3", 2, 3))
        self.assertEqual(data["model"], {"reviewer": "m-a", "lead": "m-b"})
        self.assertTrue(same_path(data["runRoot"], self.tmp / "r"))
        self.assertTrue(same_path(data["scratch"], self.tmp / "s"))
        self.assertTrue(same_path(data["tmp"], self.tmp / "t"))

    def test_an_items_object_passes_its_other_fields_through(self):
        items = self.write(self.tmp, "plans.json", json.dumps(
            {"reviewRounds": 2, "planDir": "plans", "items": [{"num": "050"}]}))
        data = self.args_json("plans", "--items", items)
        self.assertEqual((data["reviewRounds"], data["planDir"], data["items"]),
                         (2, "plans", [{"num": "050"}]))

    def test_an_items_object_keeps_its_fields_unless_a_flag_names_them(self):
        items = self.write(self.tmp, "obj.json", json.dumps(
            {"pool": 2, "model": {"lead": "m-obj"}, "maxRounds": 1, "date": "2031-01-01",
             "runRoot": "R", "items": [{"num": "050"}]}))
        data = self.args_json("plans", "--items", items, run_root=False)
        self.assertEqual((data["pool"], data["model"], data["maxRounds"], data["date"], data["runRoot"]),
                         (2, {"lead": "m-obj"}, 1, "2031-01-01", "R"))
        data = self.args_json("plans", "--items", items, "--pool", "3", "--model", "lead=m-flag",
                              "--max-rounds", "2", "--date", "2032-02-02")
        self.assertEqual((data["pool"], data["model"], data["maxRounds"], data["date"]),
                         (3, {"lead": "m-flag"}, 2, "2032-02-02"))
        self.assertTrue(same_path(data["runRoot"], self.run_root))

    def test_a_review_item_keeps_its_own_lenses_beside_the_computed_ones(self):
        items = self.write(self.tmp, "lens.json", json.dumps(
            [{"num": "042", "wt": str(self.wt), "base": self.base, "head": self.hook_only,
              "lenses": ["2", 7]}]))
        data = self.args_json("review", "--items", items)
        self.assertEqual(data["items"][0]["lenses"], ["1", "2", "3", "4", "5", "6", "7", "tooling"])

    def test_a_review_item_whose_range_changes_no_file_is_refused(self):
        items = self.write(self.tmp, "empty.json", json.dumps(
            [{"num": "042", "wt": str(self.wt), "base": self.hook_only, "head": self.hook_only}]))
        proc = self.run_args("review", "--items", items)
        self.assertEqual(proc.returncode, 2, proc.stderr)
        self.assertIn(b"changes no file", proc.stderr)

    def test_a_revision_that_looks_like_an_option_is_refused(self):
        # git diff would take --output=<file> as an option and write the file.
        trap = self.tmp / "written-by-git"
        for item in ({"base": f"--output={trap}", "head": self.head},
                     {"base": self.base, "head": f"--output={trap}"}):
            items = self.write(self.tmp, "dash.json", json.dumps([dict(item, num="042", wt=str(self.wt))]))
            proc = self.run_args("review", "--items", items)
            self.assertEqual(proc.returncode, 2, proc.stderr)
            self.assertIn(b"is not a commit", proc.stderr)
            self.assertEqual(list(self.tmp.glob("written-by-git*")), [])

    def test_an_items_file_saved_with_a_bom_is_read(self):
        items = self.write(self.tmp, "bom.json", b"\xef\xbb\xbf" + b'[{"num": "7"}]')
        self.assertEqual(self.args_json("fanout", "--items", items)["items"], [{"num": "7"}])

    def test_scratch_and_tmp_are_required_without_the_environment_root(self):
        proc = self.run_args("fanout", "--items", self.items, root=False)
        self.assertEqual(proc.returncode, 2)
        self.assertIn(b"--scratch", proc.stderr)
        self.assertIn(b"TAOM_IMPROVE_ROOT", proc.stderr)

    def test_the_run_folder_never_defaults_to_the_worktree_root(self):
        # TAOM_IMPROVE_ROOT is the worktree root outside the repo, not the run folder.
        proc = self.run_args("fanout", "--items", self.items, run_root=False)
        self.assertEqual(proc.returncode, 2)
        self.assertIn(b"--run-root", proc.stderr)

    def test_a_path_flag_that_is_not_absolute_is_refused(self):
        # Git Bash turns an unquoted E:\root\scratch into E:rootscratch, a drive-relative path that
        # abspath would quietly resolve against the current folder.
        for flag in ("--run-root", "--scratch", "--tmp"):
            for value in ("E:rootscratch", "relative/scratch"):
                proc = self.run_args("fanout", "--items", self.items, flag, value,
                                     run_root=flag != "--run-root")
                self.assertEqual(proc.returncode, 2, (flag, value, proc.stderr))
                self.assertIn(f"{flag} '{value}' is not an absolute path".encode(), proc.stderr)
        proc = self.run_tool("args", "fanout", "--items", self.items, "--run-root", self.run_root,
                             env=dict(self.env, TAOM_IMPROVE_ROOT="E:improveroot"))
        self.assertEqual(proc.returncode, 2, proc.stderr)
        self.assertIn(b"TAOM_IMPROVE_ROOT 'E:improveroot' is not an absolute path", proc.stderr)

    def test_an_unknown_model_role_is_refused(self):
        proc = self.run_args("review", "--items", self.items, "--model", "reveiwer=x")
        self.assertEqual(proc.returncode, 2)
        self.assertIn(b"reveiwer", proc.stderr)

    def test_a_review_item_without_a_worktree_is_refused(self):
        items = self.write(self.tmp, "bad.json", json.dumps([{"num": "1", "base": self.base}]))
        proc = self.run_args("review", "--items", items)
        self.assertEqual(proc.returncode, 2)
        self.assertIn(b"wt", proc.stderr)

    def test_stdout_is_used_without_out(self):
        proc = self.run_args("fanout", "--items", self.items)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertEqual(json.loads(proc.stdout)["version"], "v9.8.7")


class StatusTests(unittest.TestCase):
    INDEX = (
        "# Plans\n"
        "\n"
        "| Plan | Related |\n"
        "|---|---|\n"
        "| [012](012-x.md) | none |\n"
        "\n"
        "| Plan | Title | Status | Owner |\n"
        "|------|-------|--------|-------|\n"
        "| [011](011-y.md) | Other | TODO | me |\n"
        "| [012](012-x.md) | Trace | TODO | me |\n"
        "tail line\n"
    )

    def test_only_the_status_cell_of_the_row_changes(self):
        out = ctl.set_status(self.INDEX.encode("utf-8"), "012", "MERGED (`abc1234`)")
        self.assertEqual(out, self.INDEX.replace("| Trace | TODO |", "| Trace | MERGED (`abc1234`) |")
                         .encode("utf-8"))

    def test_crlf_is_kept_byte_for_byte(self):
        crlf = self.INDEX.replace("\n", "\r\n")
        out = ctl.set_status(crlf.encode("utf-8"), "012", "DONE")
        self.assertEqual(out, crlf.replace("| Trace | TODO |", "| Trace | DONE |").encode("utf-8"))

    def test_a_missing_row_is_an_error(self):
        with self.assertRaises(ctl.Fail) as caught:
            ctl.set_status(self.INDEX.encode("utf-8"), "099", "DONE")
        self.assertEqual(caught.exception.code, 1)

    def test_the_status_column_is_found_wherever_it_sits(self):
        index = "| Plan | Status | Title |\n|---|---|---|\n| [012](012-x.md) | TODO | Trace |\n"
        self.assertEqual(ctl.set_status(index.encode("utf-8"), "012", "DONE"),
                         index.replace("| TODO |", "| DONE |").encode("utf-8"))

    def test_a_row_listed_twice_is_an_error(self):
        index = ("| Plan | Title | Status |\n|---|---|---|\n"
                 "| [012](012-x.md) | A | TODO |\n| [012](012-y.md) | B | TODO |\n")
        with self.assertRaises(ctl.Fail) as caught:
            ctl.set_status(index.encode("utf-8"), "012", "DONE")
        self.assertEqual(caught.exception.code, 1)
        self.assertIn("2 times", str(caught.exception))

    def test_a_number_never_matches_a_longer_one(self):
        index = ("| Plan | Title | Status |\n|---|---|---|\n"
                 "| [120](120-x.md) | A | TODO |\n| [12](12-y.md) | B | TODO |\n| 7 | C | TODO |\n")
        self.assertEqual(ctl.set_status(index.encode("utf-8"), "12", "DONE"),
                         index.replace("| B | TODO |", "| B | DONE |").encode("utf-8"))
        self.assertEqual(ctl.set_status(index.encode("utf-8"), "7", "DONE"),
                         index.replace("| C | TODO |", "| C | DONE |").encode("utf-8"))

    def test_a_cell_breaking_status_is_refused(self):
        for text in ("a | b", "two\nlines"):
            with self.assertRaises(ctl.Fail) as caught:
                ctl.set_status(self.INDEX.encode("utf-8"), "012", text)
            self.assertEqual(caught.exception.code, 2)

    def test_the_cli_rewrites_the_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "README.md"
            path.write_bytes(self.INDEX.encode("utf-8"))
            proc = subprocess.run([sys.executable, "-B", str(TOOL), "status", str(path), "011",
                                   "BLOCKED (plan 009)"], capture_output=True)
            self.assertEqual(proc.returncode, 0, proc.stderr)
            self.assertIn(b"| [011](011-y.md) | Other | BLOCKED (plan 009) | me |",
                          path.read_bytes())
            missing = subprocess.run([sys.executable, "-B", str(TOOL), "status", str(path), "077",
                                      "DONE"], capture_output=True)
            self.assertEqual(missing.returncode, 1)


class CodexPromptTests(FixtureRepo):
    def setUp(self):
        super().setUp()
        self.commit(self.main, "base", {ctl.PROMPT_FIXED_PATH: PROMPT_FIXED, "README.md": "x\n"})
        self.git(self.main, "switch", "-q", "-c", "improve/012-trace-per-frame")
        self.commit(self.main, "work", {"plans/012-loading-trace.md": PLAN,
                                        "Main/Trace.cs": "class T {}\n",
                                        "tools/trace.py": "print()\n"})
        self.git(self.main, "switch", "-q", "main")
        self.out = self.tmp / "out" / "prompt.md"

    def prompt(self, *extra, branch="improve/012-trace-per-frame"):
        proc = self.run_tool("codex-prompt", "--branch", branch, "--base", "main", *extra,
                             "--out", self.out)
        self.assertEqual(proc.returncode, 0, proc.stderr.decode("utf-8", "replace"))
        self.assertTrue(same_path(proc.stdout.decode("utf-8").strip(), self.out))
        return self.out.read_text(encoding="utf-8")

    def test_the_prompt_carries_the_plan_the_diff_and_the_fixed_blocks(self):
        text = self.prompt()
        self.assertIn("git diff main..improve/012-trace-per-frame", text)
        self.assertIn("Stop the trace walking the stack", text)
        self.assertIn("Every frame pays for a stack walk.", text)
        self.assertIn("git show improve/012-trace-per-frame:plans/012-loading-trace.md", text)
        self.assertIn("risk? The trace is read by a crash handler that runs after the log closes.\n",
                      text)
        self.assertIn("risk? `LoadingWindow` changed shape.\n", text)
        self.assertNotIn("Stop and report back if", text)
        self.assertNotIn("not a stop condition", text)
        self.assertIn("- Main/Trace.cs", text)
        self.assertIn("- tools/trace.py", text)
        self.assertIn(CHEATSHEET + "\n", text)
        self.assertIn(LESSONS + "\n", text)
        self.assertNotIn("preface", text)
        self.assertNotIn("```", text)
        self.assertNotIn("## TAOM ID CHEATSHEET", text)
        self.assertNotIn("## Prior review lessons", text)
        # The sentinel stays inside the OUTPUT sentence, never alone on a line, so a prompt echoed
        # into the output file cannot satisfy a line-exact completion check.
        self.assertTrue(text.splitlines()[-1].endswith("must be exactly: END OF CODEX REVIEW"))
        self.assertNotIn("END OF CODEX REVIEW", text.splitlines())
        self.assertNotIn("SECOND REVIEW", text)
        self.assertFalse(any(ch in text for ch in "\u2014\u2013"))

    def test_a_tag_marks_a_second_review(self):
        self.assertIn("SECOND REVIEW (decisions)", self.prompt("--tag", "decisions"))

    def test_every_stop_condition_becomes_a_suspect(self):
        stops = [f"- Condition number {n} fires." for n in range(1, 9)]
        self.git(self.main, "switch", "-q", "-c", "improve/013-many-stops")
        self.commit(self.main, "plan", {"plans/013-many.md": "# Plan 013: Many\n\n## STOP conditions\n\n"
                                        + "\n".join(stops) + "\n\n## Steps\n- no\n"})
        self.git(self.main, "switch", "-q", "main")
        text = self.prompt(branch="improve/013-many-stops")
        for n in range(1, 9):
            self.assertIn(f"risk? Condition number {n} fires.\n", text)

    def test_a_revision_that_looks_like_an_option_is_refused(self):
        # git diff would take --output=<file> as an option and write the file.
        trap = self.tmp / "written-by-git"
        for flags in (("--branch", "improve/012-trace-per-frame", f"--base=--output={trap}"),
                      (f"--branch=--output={trap}", "--base", "main")):
            proc = self.run_tool("codex-prompt", *flags, "--out", self.out)
            self.assertEqual(proc.returncode, 2, proc.stderr)
            self.assertIn(b"is not a commit", proc.stderr)
            self.assertEqual(list(self.tmp.glob("written-by-git*")), [])
            self.assertFalse(self.out.exists())

    def test_a_branch_without_a_plan_still_gets_a_prompt(self):
        self.git(self.main, "branch", "hotfix", "improve/012-trace-per-frame~0")
        text = self.prompt(branch="hotfix")
        self.assertNotIn("The plan the change implements", text)
        self.assertIn("KNOWN SUSPECTS", text)

    def test_the_fixed_blocks_are_the_fences_under_their_headings(self):
        self.assertEqual(ctl.split_fixed(PROMPT_FIXED), (CHEATSHEET, LESSONS))

    def test_a_crlf_checkout_of_the_fixed_blocks_gives_lf_blocks(self):
        self.assertEqual(ctl.split_fixed(PROMPT_FIXED.replace("\n", "\r\n")), (CHEATSHEET, LESSONS))

    def test_a_heading_without_its_fenced_block_is_an_error(self):
        unfenced = PROMPT_FIXED.replace("```text\nPrior review lessons:", "Prior review lessons:")
        with self.assertRaises(ctl.Fail) as caught:
            ctl.split_fixed(unfenced, "fixed.md")
        self.assertIn("## Prior review lessons", str(caught.exception))

    def test_missing_fixed_blocks_are_an_error(self):
        (self.main / ctl.PROMPT_FIXED_PATH).write_text("# nothing here\n", encoding="utf-8")
        proc = self.run_tool("codex-prompt", "--branch", "improve/012-trace-per-frame", "--base",
                             "main", "--out", self.out)
        self.assertEqual(proc.returncode, 2)
        self.assertIn(b"prompt-fixed.md", proc.stderr)
        self.assertFalse(self.out.exists())


class FileIssueTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.dir = Path(self.temp.name)
        self.calls = []

    def fake_gh(self, cmd, **kwargs):
        self.calls.append(cmd)
        return subprocess.CompletedProcess(cmd, 0, "Creating issue\nhttps://github.com/o/r/issues/9\n", "")

    def draft(self, text):
        path = self.dir / "042.md"
        path.write_text(text, encoding="utf-8")
        return path

    def file(self, path):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            try:
                rc = ctl.file_issue(path, run=self.fake_gh)
            except ctl.Fail as exc:
                rc = exc.code
                err.write(str(exc))
        return rc, out.getvalue(), err.getvalue()

    def test_a_clean_draft_is_filed_with_a_body_file(self):
        path = self.draft("TITLE: Make the gate loud\nLABEL: enhancement\n\n## Problem\nIt is quiet.\n")
        rc, out, _ = self.file(path)
        self.assertEqual((rc, out), (0, "https://github.com/o/r/issues/9\n"))
        (cmd,) = self.calls
        body_file = cmd[cmd.index("--body-file") + 1]
        self.assertEqual(cmd[:3], ["gh", "issue", "create"])
        self.assertEqual(cmd[cmd.index("--title") + 1], "Make the gate loud")
        self.assertEqual(cmd[cmd.index("--label") + 1], "enhancement")
        self.assertNotIn("--repo", cmd)
        self.assertEqual(Path(body_file).read_text(encoding="utf-8"), "## Problem\nIt is quiet.\n")

    def test_a_draft_failing_the_public_text_check_is_not_filed(self):
        path = self.draft("TITLE: x\nLABEL: bug\n\nLogs are in E:\\repos\\TAOM\\logs.\n")
        rc, _, err = self.file(path)
        self.assertEqual(rc, 1)
        self.assertEqual(self.calls, [])
        self.assertIn("4: local-path", err)

    def test_a_draft_without_the_header_is_refused(self):
        rc, _, err = self.file(self.draft("# Make the gate loud\n\nbody\n"))
        self.assertEqual(rc, 2)
        self.assertEqual(self.calls, [])
        self.assertIn("TITLE:", err)

    def test_a_header_without_its_blank_line_or_its_title_is_refused(self):
        for text in ("TITLE: x\nLABEL: bug\nthe body starts at once\n",
                     "TITLE:   \nLABEL: bug\n\nbody\n",
                     "TITLE: x\n\nbody\n"):
            rc, _, err = self.file(self.draft(text))
            self.assertEqual(rc, 2, text)
            self.assertEqual(self.calls, [], text)

    def test_a_dash_or_a_local_path_in_the_title_is_refused(self):
        for title, rule in (("Make the gate loud \u2014 now", "1: em-dash"),
                            ("Logs land in E:/repos/x", "1: local-path")):
            rc, _, err = self.file(self.draft(f"TITLE: {title}\nLABEL: bug\n\nbody\n"))
            self.assertEqual(rc, 1, title)
            self.assertEqual(self.calls, [], title)
            self.assertIn(rule, err)

    def test_a_gh_failure_is_reported(self):
        def failing(cmd, **kwargs):
            return subprocess.CompletedProcess(cmd, 1, "", "label not found")
        path = self.draft("TITLE: x\nLABEL: nope\n\nbody\n")
        err = io.StringIO()
        with contextlib.redirect_stderr(err), contextlib.redirect_stdout(io.StringIO()):
            rc = ctl.file_issue(path, run=failing)
        self.assertEqual(rc, 1)
        self.assertIn("label not found", err.getvalue())


def ask_transcript(tool_id, asking_hook, reason, hooks_after=(), answered=False):
    """A transcript in the recorded shape: a Bash tool_use, its PreToolUse hook_success
    attachments (one of them answering "ask" in its stdout JSON), and a tool_result only when
    the ask was answered."""
    def hook(command, stdout):
        return {"type": "attachment", "attachment": {
            "type": "hook_success", "hookName": "PreToolUse:Bash", "toolUseID": tool_id,
            "hookEvent": "PreToolUse", "content": "", "stdout": stdout, "stderr": "", "exitCode": 0,
            "command": command}}
    ask = json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "ask",
                                             "permissionDecisionReason": reason}})
    records = [{"type": "assistant", "message": {"role": "assistant", "content": [
                   {"type": "tool_use", "id": tool_id, "name": "Bash", "input": {"command": "git ..."}}]}},
               hook(".claude/hooks/check-commit-subject-version.sh", "{}\n"),
               hook(asking_hook, ask)]
    records += [hook(command, "{}\n") for command in hooks_after]
    if answered:
        records.append({"type": "user", "message": {"role": "user", "content": [
            {"tool_use_id": tool_id, "type": "tool_result", "content": "done", "is_error": False}]}})
    return "".join(json.dumps(r) + "\n" for r in records)


# The three hook-ask stalls of the 2026-09-24 to 26 run (9.4 h and 4.5 h among them), in the shape
# their transcripts recorded: the asking hook, then any hooks that ran after it for the same call.
RECORDED_STALLS = (
    ("toolu_013deWJsUVAvykyAHa9QVrzD", ".claude/hooks/block-broad-git-add.sh",
     "CONFIRM broad staging: git commit -a stages every tracked file", ()),
    ("toolu_01Bfjm7GwPDrtLRJNqLRPppb", ".claude/hooks/block-dangerous-git.sh",
     "CONFIRM destructive git op: git checkout discards working-tree changes",
     (".claude/hooks/block-broad-git-add.sh", ".claude/hooks/suggest-compact.sh")),
    ("toolu_01NGhmqS9E9VcbjjyaURqWe4", ".claude/hooks/block-dangerous-git.sh",
     "CONFIRM destructive git op: git checkout discards working-tree changes",
     (".claude/hooks/block-broad-git-add.sh", ".claude/hooks/suggest-compact.sh")),
)


class WatchTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.dir = Path(self.temp.name)
        self.wf = self.dir / "workflows" / "wf_1"
        self.wf.mkdir(parents=True)
        rows = [{"type": "launched"},
                {"type": "started", "key": "k1", "agentId": "a1", "label": "review-011"},
                {"type": "started", "key": "k2", "agentId": "a2", "label": "lens-5"},
                {"type": "result", "key": "k2", "agentId": "a2", "result": {}},
                {"type": "started", "key": "k3", "agentId": "a3", "label": "exec-018"},
                {"type": "failed", "key": "k3", "agentId": "a3"},
                {"type": "started", "key": "k3", "agentId": "a4", "label": "exec-018"},
                # A retry with no failed row: a resumed run starts the key again.
                {"type": "started", "key": "k5", "agentId": "a5", "label": "lead-012"},
                {"type": "started", "key": "k5", "agentId": "a6", "label": "lead-012"},
                # A failure that was never retried.
                {"type": "started", "key": "k7", "agentId": "a7", "label": "draft-060"},
                {"type": "failed", "key": "k7", "agentId": "a7"}]
        (self.wf / "journal.jsonl").write_text(
            "".join(json.dumps(r) + "\n" for r in rows) + '{"type": "sta', encoding="utf-8")
        self.now = time.time()
        for agent, minutes in (("a1", 45), ("a2", 300), ("a3", 300), ("a4", 1), ("a5", 300), ("a6", 2),
                               ("a7", 300)):
            self.transcript(self.wf / f"agent-{agent}.jsonl", minutes)
        self.transcript(self.dir / "agent-plain.jsonl", 600)

    def transcript(self, path, minutes, text="{}\n"):
        path.write_text(text, encoding="utf-8")
        mtime = self.now - minutes * 60
        os.utime(path, (mtime, mtime))

    def watch(self, *extra, directory=None):
        """watch on the workflow's own folder unless a directory is given."""
        return subprocess.run([sys.executable, "-B", str(TOOL), "watch", "--dir",
                               str(directory or self.wf), *extra], capture_output=True)

    def flagged(self, proc, flag):
        return [line for line in proc.stdout.decode("utf-8").splitlines() if line.startswith(flag + " ")]

    def test_a_running_agent_silent_past_the_limit_fails_the_watch(self):
        proc = self.watch()
        self.assertEqual(proc.returncode, 1, proc.stderr)
        lines = proc.stdout.decode("utf-8").splitlines()
        stale = self.flagged(proc, "STALE")
        self.assertEqual(len(stale), 1, lines)
        self.assertIn("agent-a1.jsonl", stale[0])
        self.assertIn("review-011", stale[0])
        self.assertEqual(len([line for line in lines if ".jsonl" in line]), 7)

    def test_finished_failed_superseded_and_fresh_agents_are_not_stale(self):
        (self.wf / "agent-a1.jsonl").unlink()
        proc = self.watch()
        self.assertEqual(proc.returncode, 0, proc.stdout)
        self.assertNotIn(b"STALE", proc.stdout)
        (a5,) = [line for line in proc.stdout.decode("utf-8").splitlines() if "agent-a5.jsonl" in line]
        self.assertIn("superseded", a5)

    def test_the_default_limit_is_thirty_minutes(self):
        self.transcript(self.wf / "agent-a1.jsonl", 25)
        self.assertEqual(self.watch().returncode, 0)
        self.assertEqual(self.watch("--stale-min", "20").returncode, 1)

    def test_the_limit_is_configurable(self):
        self.assertEqual(self.watch("--stale-min", "60").returncode, 0)
        self.assertEqual(self.watch("--stale-min", "30").returncode, 1)

    def test_a_missing_folder_is_an_error(self):
        self.assertEqual(self.watch(directory=self.dir / "absent").returncode, 2)

    def test_a_session_folder_is_watched_without_its_workflows_folder(self):
        # self.dir is a session's subagents folder: a direct spawn beside workflows/wf_1, whose
        # journal leaves a1 running and silent. A killed workflow's journal says the same forever,
        # so watch never recurses into a workflows folder; a workflow is watched by its own folder.
        proc = self.watch(directory=self.dir)
        self.assertEqual(proc.returncode, 0, proc.stdout)
        listed = [line for line in proc.stdout.decode("utf-8").splitlines() if ".jsonl" in line]
        self.assertEqual(len(listed), 1, listed)
        self.assertIn("agent-plain.jsonl", listed[0])
        self.assertEqual(self.watch(directory=self.wf).returncode, 1)

    def test_the_recorded_hook_ask_stalls_are_flagged_in_a_direct_spawn_folder(self):
        spawns = self.dir / "spawns"
        spawns.mkdir()
        for n, (tool_id, hook, reason, after) in enumerate(RECORDED_STALLS):
            self.transcript(spawns / f"agent-s{n}.jsonl", 1, ask_transcript(tool_id, hook, reason, after))
        proc = self.watch(directory=spawns)
        self.assertEqual(proc.returncode, 1, proc.stdout)
        asking = self.flagged(proc, "STALLED-ASK")
        self.assertEqual(len(asking), 3, proc.stdout)
        self.assertIn("CONFIRM broad staging", asking[0])
        self.assertIn("CONFIRM destructive git op", asking[1])

    def test_a_running_workflow_agent_waiting_on_an_ask_is_flagged_while_fresh(self):
        (self.wf / "agent-a1.jsonl").unlink()
        tool_id, hook, reason, after = RECORDED_STALLS[1]
        self.transcript(self.wf / "agent-a4.jsonl", 1, ask_transcript(tool_id, hook, reason, after))
        proc = self.watch()
        self.assertEqual(proc.returncode, 1, proc.stdout)
        (line,) = self.flagged(proc, "STALLED-ASK")
        self.assertIn("agent-a4.jsonl", line)
        self.assertIn("exec-018", line)

    def test_an_answered_ask_and_an_ask_in_a_finished_agent_are_not_stalls(self):
        (self.wf / "agent-a1.jsonl").unlink()
        tool_id, hook, reason, after = RECORDED_STALLS[0]
        self.transcript(self.wf / "agent-a4.jsonl", 1, ask_transcript(tool_id, hook, reason, after, answered=True))
        self.transcript(self.wf / "agent-a2.jsonl", 300, ask_transcript(tool_id, hook, reason, after))
        proc = self.watch()
        self.assertEqual(proc.returncode, 0, proc.stdout)
        self.assertNotIn(b"STALLED-ASK", proc.stdout)

    def test_a_tool_result_for_another_call_leaves_the_ask_pending(self):
        # Two tool calls in one turn: the one the hook did not stop comes back, the asked one waits.
        spawns = self.dir / "spawns"
        spawns.mkdir()
        other = {"type": "user", "message": {"role": "user", "content": [
            {"tool_use_id": "toolu_OTHER", "type": "tool_result", "content": "done", "is_error": False}]}}
        asked = ask_transcript("toolu_A", ".claude/hooks/block-dangerous-git.sh", "still waiting", ())
        self.transcript(spawns / "agent-s.jsonl", 1, asked + json.dumps(other) + "\n")
        proc = self.watch(directory=spawns)
        self.assertEqual(proc.returncode, 1, proc.stdout)
        (line,) = self.flagged(proc, "STALLED-ASK")
        self.assertIn("still waiting", line)

    def test_only_the_latest_ask_counts(self):
        spawns = self.dir / "spawns"
        spawns.mkdir()
        first = ask_transcript("toolu_A", ".claude/hooks/block-dangerous-git.sh", "first", ())
        answered = ask_transcript("toolu_B", ".claude/hooks/block-dangerous-git.sh", "second", (), answered=True)
        self.transcript(spawns / "agent-s.jsonl", 1, first + answered)
        self.assertEqual(self.watch(directory=spawns).returncode, 0)
        self.transcript(spawns / "agent-s.jsonl", 1, answered + first)
        self.assertEqual(self.watch(directory=spawns).returncode, 1)


class CliTests(unittest.TestCase):
    def test_an_unexpected_error_exits_three_without_a_traceback(self):
        err = io.StringIO()
        with mock.patch.object(ctl, "cmd_status", side_effect=RuntimeError("boom")), \
                contextlib.redirect_stderr(err):
            rc = ctl.main(["status", "README.md", "1", "DONE"])
        self.assertEqual(rc, 3)
        self.assertIn("unexpected error", err.getvalue())
        self.assertIn("boom", err.getvalue())


class RealFileTests(unittest.TestCase):
    """The files other builders own, checked only once they exist in this checkout."""

    @unittest.skipUnless((REPO_ROOT / ctl.PROMPT_FIXED_PATH).exists(), "prompt-fixed.md not here yet")
    def test_the_real_fixed_blocks_split_into_the_cheatsheet_and_the_lessons(self):
        text = (REPO_ROOT / ctl.PROMPT_FIXED_PATH).read_text(encoding="utf-8")
        cheatsheet, lessons = ctl.split_fixed(text)
        self.assertIn("Kingdom IDs", cheatsheet)
        self.assertIn("SUCCESSES", lessons)

    @unittest.skipUnless((REPO_ROOT / ctl.RULES_PATH).exists(), "dispatch-rules.md not here yet")
    def test_the_real_dispatch_rules_carry_the_pinned_headings(self):
        text = (REPO_ROOT / ctl.RULES_PATH).read_text(encoding="utf-8")
        for needle in ("## Standing rules (every agent)", "HOOK-ASK", "TIMEOUT"):
            self.assertIn(needle, text)


if __name__ == "__main__":
    unittest.main()
