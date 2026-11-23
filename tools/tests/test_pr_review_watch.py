#!/usr/bin/env python3
"""Unit tests for tools/pr_review_watch.py.

Run:  python -B -m unittest tools.tests.test_pr_review_watch

The gh runner, the claude runner, the notifier and the clock are injected, so no test touches
the network, GitHub or a model.
"""
import contextlib
import io
import json
import os
import re
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)

import pr_review_watch as w  # noqa: E402

REPO = "haterade22/TAOM"
HEAD_A = "a" * 40
HEAD_B = "b" * 40
NOW = 1_800_000_000.0


def pr(number, head=HEAD_A, base="bannerlord-1.5.x", cross=False):
    return {"number": number, "headRefOid": head, "baseRefName": base, "isCrossRepository": cross,
            "author": {"login": "someone"}, "title": "a title", "body": "a body"}


def entry(name, patch="@@ -1 +1 @@\n-a\n+b", changes=2, status="modified", previous=None):
    item = {"filename": name, "status": status, "changes": changes}
    if patch is not None:
        item["patch"] = patch
    if previous is not None:
        item["previous_filename"] = previous
    return item


class FakeGh:
    """Answers the gh calls the watcher makes from a table of PRs, files and file texts."""

    def __init__(self, prs, files, texts=None, diff="diff --git a/x b/x\n+line\n", diff_error=None):
        self.prs = prs
        self.files = files
        self.texts = texts or {}
        self.diff = diff
        self.diff_error = diff_error
        self.calls = []

    def __call__(self, args):
        self.calls.append(args)
        if args[:2] == ["pr", "list"]:
            return json.dumps([dict(p, changedFiles=p.get("changedFiles", len(self.files.get(p["number"], []))))
                               for p in self.prs])
        if args[:2] == ["pr", "view"]:
            number = int(args[2])
            return json.dumps(next(p for p in self.prs if p["number"] == number))
        if args[:2] == ["pr", "diff"]:
            if self.diff_error:
                raise w.GhError(self.diff_error)
            return self.diff
        if args[0] == "api" and "/files" in args[-1]:
            number = int(args[-1].split("/files")[0].split("/")[-1])
            # gh applies the --jq projection: every named field, null when GitHub sent none, each object on one
            # line, with non-ASCII characters raw (U+0085 included).
            fields = [k.strip() for k in re.search(r"\{([^}]*)\}", args[args.index("--jq") + 1]).group(1).split(",")]
            return "\n".join(json.dumps({k: f.get(k) for k in fields}, ensure_ascii=False) for f in self.files[number])
        if args[0] == "api" and "/contents/" in args[-1]:
            path = args[-1].split("/contents/", 1)[1].split("?ref=", 1)[0]
            return self.texts.get(path, "text of " + path)
        raise AssertionError("unexpected gh call: %r" % (args,))


class FakeClaude:
    def __init__(self, rc=0, out="**Verdict:** LOOKS RIGHT\n\nNo findings."):
        self.rc = rc
        self.out = out
        self.calls = []

    def __call__(self, cmd, cwd, timeout):
        self.calls.append((cmd, cwd, timeout))
        return self.rc, self.out


class HighRiskPathTests(unittest.TestCase):
    def test_build_harness_and_hook_paths_are_high_risk(self):
        paths = ["TAOM.Tests/TAOM.Tests.csproj", "Directory.Build.props", "Main/Build.targets", "TAOM.sln",
                 "Directory.Build.rsp", "binding-gate.runsettings", "NuGet.config", "global.json",
                 ".config/dotnet-tools.json", "Main/Properties/launchSettings.json", "build.ps1", "setup-dev-env.ps1", ".claude/hooks/x.sh", ".ai/policy.md",
                 ".agents/a.md", ".codex/config.toml", ".mcp.json", "AGENTS.md", "CLAUDE.md",
                 ".github/workflows/pr-checks.yml", ".gitattributes", ".gitmodules", ".vscode/tasks.json",
                 ".githooks/pre-commit"]
        self.assertEqual(paths, w.high_risk_paths(paths))

    def test_a_nested_or_differently_cased_path_is_high_risk_too(self):
        # A nested CLAUDE.md, AGENTS.md or nuget.config is read by its tool; Windows writes .Claude/ into .claude/.
        paths = [".Claude/rules/x.md", "claude.md", "NuGet.Config", "Main/NuGet.Config", "Main/.claude/skills/x/SKILL.md",
                 "Main/CLAUDE.md", "docs/AGENTS.md", ".GitHub/workflows/a.yml", "Main/X.CSPROJ"]
        self.assertEqual(paths, w.high_risk_paths(paths))

    def test_the_list_matches_the_review_outside_pr_skill(self):
        skill = Path(TOOLS).parent / ".claude" / "skills" / "review-outside-pr" / "SKILL.md"
        text = skill.read_text(encoding="utf-8")
        step = text[text.index("3. **High-risk paths.**"):text.index("`tools/pr_review_watch.py` keeps")]
        listed = {p.replace("/**", "/*") for p in re.findall(r"`([^`]+)`", step) if p != "tools/"}
        self.assertEqual(listed, set(w.HIGH_RISK))
        self.assertIn("any script under `tools/` that a hook", step, "the watcher finds those scripts itself")

    def test_feature_code_data_and_docs_are_not_high_risk(self):
        paths = ["Main/Features/X/Y.cs", "Main/_Module/ModuleData/a.xml", "docs/features/x.md",
                 "tools/some_tool.py", "docs/CLAUDE-notes.md", "docs/claude/notes.md"]
        self.assertEqual([], w.high_risk_paths(paths))

    def test_a_tools_script_that_a_hook_or_the_build_runs_is_high_risk(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / ".claude" / "hooks").mkdir(parents=True)
            (root / ".claude" / "hooks" / "gate.sh").write_text('python "$CLAUDE_PROJECT_DIR/tools/work_claims.py" check\n',
                                                                encoding="utf-8")
            (root / "Main").mkdir()
            (root / "Main" / "TAOM.csproj").write_text('<Exec Command="python $(Root)tools\\gen_ids.py" />',
                                                       encoding="utf-8")
            (root / "tools").mkdir()
            (root / "tools" / "work_claims.py").write_text("import json\nimport _paths\n", encoding="utf-8")
            (root / "tools" / "_paths.py").write_text("from _env import home\n", encoding="utf-8")
            (root / "tools" / "_env.py").write_text("x = 1\n", encoding="utf-8")
            guarded = w.guarded_scripts(root)

        self.assertEqual(frozenset({"tools/work_claims.py", "tools/gen_ids.py", "tools/_paths.py", "tools/_env.py"}),
                         guarded, "the modules a guarded script imports, at any depth, are guarded too")
        self.assertEqual(["tools/work_claims.py", "tools/Gen_Ids.py"],
                         w.high_risk_paths(["tools/work_claims.py", "tools/Gen_Ids.py", "tools/other.py"], guarded))


class UnreadableFileTests(unittest.TestCase):
    def test_a_file_without_a_text_patch_is_unreadable(self):
        files = [entry("a.cs"), entry("big.xml", patch=None, changes=90000)]
        self.assertEqual(["big.xml"], w.unreadable_files(files))

    def test_a_binary_is_unreadable_though_github_reports_it_with_no_changes(self):
        # GitHub's shape for an added PNG (trunk 415277404): additions 0, deletions 0, changes 0, no patch.
        files = [entry("x.png", patch=None, changes=0, status="added"), entry("y.dll", patch=None, changes=0)]
        self.assertEqual(["x.png", "y.dll"], w.unreadable_files(files))

    def test_every_rename_without_a_patch_is_unreadable_because_a_changed_binary_looks_the_same(self):
        # Trunk 9a409deac renamed a PNG and changed its content: GitHub reports renamed, changes 0, no patch,
        # exactly as for a rename with no content change.
        files = [entry("taom_banners_05.png", patch=None, changes=0, status="renamed", previous="packed_03.png"),
                 entry("new.cs", patch=None, changes=0, status="renamed", previous="old.cs")]
        self.assertEqual(["taom_banners_05.png", "new.cs"], w.unreadable_files(files))


class HiddenUnicodeTests(unittest.TestCase):
    def test_bidi_zero_width_invisible_and_tag_characters_are_found_by_line(self):
        text = "plain\nhas \u202e override\nok\nzero\u200bwidth\nbom \ufeff\n+tag \U000e0041\n+join\u2060\n+\u061c\n"
        self.assertEqual([2, 4, 5, 6, 7, 8], w.hidden_unicode_lines(text))

    def test_blank_letters_valid_in_a_csharp_name_and_variation_selectors_are_found(self):
        text = "+var \u3164 = 1;\n+a\U000e0101b\n+soft\u00adhyphen\n+ok \u2665\ufe0f\n"
        self.assertEqual([1, 2, 3], w.hidden_unicode_lines(text), "U+FE0F stays: emoji use it")

    def test_the_ranges_match_the_review_outside_pr_skills_grep(self):
        skill = Path(TOOLS).parent / ".claude" / "skills" / "review-outside-pr" / "SKILL.md"
        line = next(l for l in skill.read_text(encoding="utf-8").splitlines() if "hidden or bidirectional Unicode" in l)
        listed = [(int(lo, 16), int(hi or lo, 16))
                  for lo, hi in re.findall(r"\\x\{([0-9A-Fa-f]+)\}(?:-\\x\{([0-9A-Fa-f]+)\})?", line)]
        self.assertEqual(sorted(listed), sorted(w.HIDDEN_RANGES))

    def test_a_byte_order_mark_at_the_start_of_a_file_line_is_not_hidden_text(self):
        self.assertEqual([], w.hidden_unicode_lines("+\ufeffusing System;\n-\ufeffusing X;\n \ufeffkept\n"))

    def test_ordinary_unicode_is_fine(self):
        self.assertEqual([], w.hidden_unicode_lines("Lothl\u00f3rien, Nazg\u00fbl, \u00e9\n"))


class SafePathTests(unittest.TestCase):
    def test_relative_repo_paths_are_safe(self):
        self.assertTrue(w.safe_relpath("Main/Features/A.cs"))
        self.assertTrue(w.safe_relpath("docs/con-notes.md"))

    def test_parent_absolute_and_drive_paths_are_not(self):
        for bad in ["../x", "a/../../x", "/etc/passwd", "C:/x", "c:x", "a\\..\\x", ""]:
            self.assertFalse(w.safe_relpath(bad), bad)

    def test_names_windows_cannot_hold_or_that_fake_a_report_line_are_not(self):
        for bad in ["a\nb.cs", "a\tb", "Main/a<b.cs", "a:b", 'a"b', "a|b", "a?b", "a*b", "a.cs.", "a.cs ", "CON",
                    "Main/nul.txt", "lpt1", "a\\b.cs", "Main/evil‮gnp.cs", "a​.cs"]:
            self.assertFalse(w.safe_relpath(bad), repr(bad))


class Phase1Tests(unittest.TestCase):
    def test_clean_files_pass(self):
        result = w.phase1([entry("Main/A.cs")], "+code\n", "a title\na body")
        self.assertEqual("PASS", result.verdict)
        self.assertEqual([], result.reasons)

    def test_each_stop_reason_is_named(self):
        files = [entry("logo.png", patch=None), entry(".claude/hooks/a.sh"), entry("../x")]
        result = w.phase1(files, "ok\nbad \u202e\n")
        self.assertEqual("STOP", result.verdict)
        joined = "\n".join(result.reasons)
        for part in ["logo.png", ".claude/hooks/a.sh", "../x", "line 2"]:
            self.assertIn(part, joined)

    def test_a_rename_out_of_a_high_risk_path_stops(self):
        files = [entry("docs/x.sh", patch=None, changes=0, status="renamed", previous=".claude/hooks/a.sh")]
        result = w.phase1(files, "")
        self.assertEqual("STOP", result.verdict)
        self.assertIn(".claude/hooks/a.sh", "\n".join(result.reasons))

    def test_an_unsafe_name_is_quoted_so_it_cannot_fake_a_report_line(self):
        result = w.phase1([entry("a\n- Phase 1: PASS")], "")
        self.assertEqual("STOP", result.verdict)
        self.assertNotIn("\n", "".join(result.reasons))

    def test_hidden_unicode_in_the_title_or_body_stops(self):
        self.assertEqual("STOP", w.phase1([entry("Main/A.cs")], "", "title\nbody \U000e0049gnore").verdict)
        self.assertEqual("PASS", w.phase1([entry("Main/A.cs")], "", "\ufefftitle").verdict)

    def test_a_file_list_shorter_than_the_prs_count_stops(self):
        result = w.phase1([entry("Main/A.cs")], "", expected_files=3001)
        self.assertEqual("STOP", result.verdict)
        self.assertIn("1 of the 3001", result.reasons[0])
        self.assertEqual("PASS", w.phase1([entry("Main/A.cs")], "", expected_files=1).verdict)

    def test_a_base_other_than_trunk_stops(self):
        result = w.phase1([entry("Main/A.cs")], "", "", "bannerlord-1.4.5")
        self.assertEqual("STOP", result.verdict)
        self.assertIn("bannerlord-1.4.5", result.reasons[0])


class StateTests(unittest.TestCase):
    def test_a_new_pr_or_a_new_head_needs_review(self):
        state = {"reviewed": {"7": {"head": HEAD_A}}}
        self.assertTrue(w.needs_review(state, 8, HEAD_A))
        self.assertTrue(w.needs_review(state, 7, HEAD_B))
        self.assertFalse(w.needs_review(state, 7, HEAD_A))

    def test_a_head_that_failed_three_times_is_left_alone(self):
        state = {"failed": {"7": {"head": HEAD_A, "attempts": 3}}}
        self.assertFalse(w.needs_review(state, 7, HEAD_A))
        self.assertTrue(w.needs_review(state, 7, HEAD_B))


class ClaudeCommandTests(unittest.TestCase):
    def test_the_review_may_read_but_not_run_write_fetch_or_delegate(self):
        cmd = w.claude_command("claude", "the prompt", "E:/out/pr-1", "opus", ["C:/src"])
        available = cmd[cmd.index("--tools") + 1:cmd.index("--disallowedTools")]
        self.assertEqual(["Read", "Grep", "Glob"], available, "an allow-list, so a new CLI tool is not available")
        self.assertNotIn("--allowedTools", cmd, "a bare Read approval would allow a read of any path")
        self.assertEqual("project", cmd[cmd.index("--setting-sources") + 1], "no user settings and their Read rules")
        denied = set(cmd[cmd.index("--disallowedTools") + 1:cmd.index("--strict-mcp-config")])
        for tool in ["Bash", "PowerShell", "Edit", "Write", "WebFetch", "WebSearch", "Agent", "Skill"]:
            self.assertIn(tool, denied)
        self.assertIn("--strict-mcp-config", cmd)
        self.assertIn("--no-session-persistence", cmd)
        self.assertEqual(["-p", "the prompt"], cmd[1:3])
        self.assertEqual("default", cmd[cmd.index("--permission-mode") + 1])
        self.assertIn("E:/out/pr-1", cmd)
        self.assertIn("C:/src", cmd)

    def test_the_prompt_names_the_pr_folder_as_untrusted_and_keeps_pr_text_out(self):
        prompt = w.review_prompt(1540, REPO, HEAD_A, "E:/out/pr-1540-aaaaaaa")
        self.assertIn("#1540", prompt)
        self.assertIn("E:/out/pr-1540-aaaaaaa", prompt)
        self.assertIn("untrusted", prompt)
        self.assertNotIn("a body", prompt)


class RunTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.out = Path(self.tmp.name) / "out"
        self.repo_dir = str(Path(self.tmp.name) / "repo")
        self.notes = []
        self.stderr = ""

    def tearDown(self):
        self.tmp.cleanup()

    def notify(self, title, body):
        self.notes.append((title, body))

    def run_once(self, gh, claude, **kw):
        err = io.StringIO()
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(err):
            rc = w.run(REPO, self.out, gh=gh, claude=claude, notify=self.notify, clock=lambda: NOW,
                       claude_exe="claude", model="opus", repo_dir=self.repo_dir, extra_dirs=[], **kw)
        self.stderr = err.getvalue()
        return rc

    def report(self, number, head=HEAD_A):
        return (self.out / ("pr-%d-%s" % (number, head[:7])) / "report.md").read_text(encoding="utf-8")

    def state(self):
        return json.loads((self.out / "state.json").read_text(encoding="utf-8"))

    def test_a_clean_pr_gets_an_ai_report_and_is_not_reviewed_twice(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs"), entry("docs/x.md")]}, texts={"Main/A.cs": "class A {}"})
        claude = FakeClaude()

        self.assertEqual(0, self.run_once(gh, claude))

        folder = self.out / ("pr-5-" + HEAD_A[:7])
        self.assertEqual("class A {}", (folder / "files" / "Main" / "A.cs").read_text(encoding="utf-8"))
        self.assertTrue((folder / "pr.diff").exists())
        self.assertIn("a body", (folder / "pr-body.md").read_text(encoding="utf-8"))
        report = (folder / "report.md").read_text(encoding="utf-8")
        self.assertIn("LOOKS RIGHT", report)
        self.assertIn("Phase 1: PASS", report)
        self.assertIn("not run", report.lower())
        self.assertEqual(HEAD_A, self.state()["reviewed"]["5"]["head"])
        self.assertEqual(1, len(claude.calls))
        self.assertEqual(self.repo_dir, claude.calls[0][1])
        self.assertEqual(1, len(self.notes))
        self.assertIn("#5", self.notes[0][0])

        self.assertEqual(0, self.run_once(gh, claude))
        self.assertEqual(1, len(claude.calls))
        self.assertEqual(1, len(self.notes))

    def test_a_stop_pr_gets_a_report_and_no_ai_call(self):
        gh = FakeGh([pr(6)], {6: [entry(".github/workflows/x.yml")]})
        claude = FakeClaude()

        self.assertEqual(0, self.run_once(gh, claude))

        self.assertEqual([], claude.calls)
        report = (self.out / ("pr-6-" + HEAD_A[:7]) / "report.md").read_text(encoding="utf-8")
        self.assertIn("Phase 1: STOP", report)
        self.assertIn(".github/workflows/x.yml", report)
        self.assertIn("/review-outside-pr 6", report)
        self.assertEqual("STOP", self.state()["reviewed"]["6"]["verdict"])
        self.assertIn("STOP", self.notes[0][1])
        self.assertFalse((self.out / ("pr-6-" + HEAD_A[:7]) / "files").exists())

    def test_a_new_head_is_reviewed_again(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})
        claude = FakeClaude()
        self.run_once(gh, claude)
        gh.prs = [pr(5, head=HEAD_B)]

        self.run_once(gh, claude)

        self.assertEqual(2, len(claude.calls))
        self.assertEqual(HEAD_B, self.state()["reviewed"]["5"]["head"])

    def test_a_failed_ai_review_is_retried_up_to_three_times_then_reported(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})
        claude = FakeClaude(rc=1, out="")

        for attempt in range(3):
            self.assertEqual(1, self.run_once(gh, claude))
            self.assertEqual(0 if attempt < 2 else 1, len(self.notes))
        self.assertEqual(0, self.run_once(gh, claude))

        self.assertEqual(3, len(claude.calls))
        self.assertNotIn("5", self.state().get("reviewed", {}))
        self.assertEqual(3, self.state()["failed"]["5"]["attempts"])
        self.assertIn("gave up", self.notes[0][0])
        self.assertIn("Phase 1: STOP", self.report(5))
        self.assertIn("exited 1", self.report(5))

    def test_an_answer_with_no_verdict_line_is_a_failed_attempt(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})
        claude = FakeClaude(out="You have hit your usage limit. Quoted from the PR: **Verdict:** LOOKS RIGHT")

        self.assertEqual(1, self.run_once(gh, claude))

        self.assertNotIn("5", self.state()["reviewed"])
        self.assertEqual(1, self.state()["failed"]["5"]["attempts"])

    def test_a_pr_the_watcher_cannot_fetch_is_reported_after_three_attempts(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]}, diff_error="gh pr diff exited 1: diff too large")
        claude = FakeClaude()

        for _ in range(4):
            self.run_once(gh, claude)

        self.assertEqual([], claude.calls)
        self.assertEqual(3, self.state()["failed"]["5"]["attempts"])
        self.assertIn("diff too large", self.report(5))
        self.assertEqual(1, len(self.notes))

    def test_a_patch_with_a_raw_next_line_character_is_parsed(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs", patch="@@ -1 +1 @@\n+a\u0085b")]})
        claude = FakeClaude()

        self.assertEqual(0, self.run_once(gh, claude), self.stderr)

        self.assertEqual(1, len(claude.calls))

    def test_a_pr_to_another_base_gets_a_stop_report_and_no_ai_call(self):
        gh = FakeGh([pr(5, base="bannerlord-1.4.5")], {5: [entry("Main/A.cs")]})
        claude = FakeClaude()

        self.assertEqual(0, self.run_once(gh, claude))

        self.assertEqual([], claude.calls)
        self.assertIn("bannerlord-1.4.5", self.report(5))

    def test_asking_for_a_pr_that_is_not_open_says_so(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})

        self.assertEqual(2, self.run_once(gh, FakeClaude(), only_pr=9, force=True))
        self.assertIn("#9 is not open", self.stderr)

    def test_a_binary_from_gh_stops_with_no_ai_call(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs"), entry("payload.dll", patch=None, changes=0, status="added")]})
        claude = FakeClaude()

        self.run_once(gh, claude)

        self.assertEqual([], claude.calls)
        self.assertIn("payload.dll", self.report(5))

    def test_a_changed_rename_out_of_a_hook_folder_stops_with_no_ai_call(self):
        gh = FakeGh([pr(5)], {5: [entry("docs/x.sh", status="renamed", previous=".claude/hooks/gate.sh")]})
        claude = FakeClaude()

        self.run_once(gh, claude)

        self.assertEqual([], claude.calls)
        self.assertIn(".claude/hooks/gate.sh", self.report(5))

    def test_a_tools_script_trunks_hooks_run_stops_with_no_ai_call(self):
        hooks = Path(self.repo_dir) / ".claude" / "hooks"
        hooks.mkdir(parents=True)
        (hooks / "gate.sh").write_text('python "$CLAUDE_PROJECT_DIR/tools/work_claims.py"\n', encoding="utf-8")
        gh = FakeGh([pr(5)], {5: [entry("tools/work_claims.py")]})
        claude = FakeClaude()

        self.run_once(gh, claude)

        self.assertEqual([], claude.calls)
        self.assertIn("tools/work_claims.py", self.report(5))

    def test_a_success_after_a_failure_clears_the_failure(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})
        self.run_once(gh, FakeClaude(rc=1, out=""))

        self.run_once(gh, FakeClaude())

        self.assertNotIn("5", self.state()["failed"])
        self.assertEqual("LOOKS RIGHT", self.state()["reviewed"]["5"]["verdict"])

    def test_a_new_head_starts_its_attempts_from_one(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})
        for _ in range(2):
            self.run_once(gh, FakeClaude(rc=1, out=""))
        gh.prs = [pr(5, head=HEAD_B)]

        self.run_once(gh, FakeClaude(rc=1, out=""))

        self.assertEqual({"head": HEAD_B, "attempts": 1},
                         {k: self.state()["failed"]["5"][k] for k in ("head", "attempts")})
        self.assertEqual([], self.notes, "a new head does not inherit the old head's attempts")

    def test_a_model_failure_ends_the_run_so_one_outage_costs_one_attempt(self):
        gh = FakeGh([pr(5), pr(6)], {5: [entry("Main/A.cs")], 6: [entry("Main/B.cs")]})
        claude = FakeClaude(rc=1, out="usage limit")

        self.run_once(gh, claude)

        self.assertEqual(1, len(claude.calls))
        self.assertNotIn("6", self.state()["failed"])

    def test_the_lock_is_stamped_again_before_each_pr(self):
        gh = FakeGh([pr(5), pr(6)], {5: [entry("Main/A.cs")], 6: [entry("Main/B.cs")]})
        ticks = iter(range(int(NOW), int(NOW) + 100))
        seen = []

        def claude(cmd, cwd, timeout):
            seen.append(json.loads((self.out / ".lock").read_text(encoding="utf-8"))["at"])
            return 0, "**Verdict:** LOOKS RIGHT"

        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            w.run(REPO, self.out, gh=gh, claude=claude, notify=self.notify, clock=lambda: float(next(ticks)),
                  claude_exe="claude", model="opus", repo_dir=self.repo_dir, extra_dirs=[])

        self.assertEqual(2, len(seen))
        self.assertLess(seen[0], seen[1])

    def test_a_run_leaves_another_runs_lock_alone(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})

        def claude(cmd, cwd, timeout):
            (self.out / ".lock").write_text(json.dumps({"pid": -1, "at": NOW}), encoding="utf-8")
            return 0, "**Verdict:** LOOKS RIGHT"

        self.run_once(gh, claude)

        self.assertTrue((self.out / ".lock").exists(), "a run removes only the lock it wrote")

    def test_a_dry_run_lists_what_it_would_do_and_writes_nothing(self):
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})
        claude = FakeClaude()

        self.assertEqual(0, self.run_once(gh, claude, dry_run=True))

        self.assertEqual([], claude.calls)
        self.assertFalse(self.out.exists())

    def test_only_pr_reviews_that_pr_even_if_reviewed(self):
        gh = FakeGh([pr(5), pr(6)], {5: [entry("Main/A.cs")], 6: [entry("Main/B.cs")]})
        claude = FakeClaude()
        self.run_once(gh, claude)

        self.run_once(gh, claude, only_pr=6, force=True)

        self.assertEqual(3, len(claude.calls))

    def test_a_run_in_progress_blocks_a_second_one(self):
        self.out.mkdir(parents=True, exist_ok=True)
        (self.out / ".lock").write_text(json.dumps({"pid": 1, "at": NOW - 60}), encoding="utf-8")
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})

        self.assertEqual(0, self.run_once(gh, FakeClaude()))

        self.assertEqual([], gh.calls)

    def test_a_stale_lock_is_taken_over(self):
        self.out.mkdir(parents=True, exist_ok=True)
        (self.out / ".lock").write_text(json.dumps({"pid": 1, "at": NOW - 3 * 3600}), encoding="utf-8")
        gh = FakeGh([pr(5)], {5: [entry("Main/A.cs")]})
        claude = FakeClaude()

        self.assertEqual(0, self.run_once(gh, claude))

        self.assertEqual(1, len(claude.calls))
        self.assertFalse((self.out / ".lock").exists())

    def test_an_unsafe_file_name_never_becomes_a_path(self):
        gh = FakeGh([pr(5)], {5: [entry("../escape.cs")]})
        claude = FakeClaude()

        self.run_once(gh, claude)

        self.assertEqual([], claude.calls)
        self.assertFalse((self.out.parent / "escape.cs").exists())


if __name__ == "__main__":
    unittest.main()
