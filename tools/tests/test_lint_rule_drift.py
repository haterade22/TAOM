#!/usr/bin/env python3
"""Tests for the rule-text drift checks in tools/lint_docs.py.

  - check_dead_memory_refs   -> `feedback_` / `feedback-` citations in rules, agents, skills and
    the ai-includes (machine-local memory files no other client can read).
  - check_rule_symbols       -> backticked names in .claude/rules that exist in no .cs or tools .py file.
  - check_unflagged_dotnet   -> `dotnet test|build` of a TAOM target without `-p:ModuleId=`.

Each test builds a synthetic repo tree and repoints lint_docs's REPO_ROOT at it.
"""
import io
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import lint_docs as ld  # noqa: E402


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class _Repo(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self._orig_root = ld.REPO_ROOT
        self._orig_docs = ld.DOCS_DIR
        self._orig_adrs = ld.ADRS_DIR
        ld.REPO_ROOT = self.root
        ld.DOCS_DIR = self.root / "docs"
        ld.ADRS_DIR = self.root / "docs" / "adrs"

    def tearDown(self):
        ld.REPO_ROOT = self._orig_root
        ld.DOCS_DIR = self._orig_docs
        ld.ADRS_DIR = self._orig_adrs
        self._tmp.cleanup()

    def kinds(self, findings):
        return [f[2] for f in findings]


class DeadMemoryRefTests(_Repo):
    def test_flags_feedback_underscore_in_a_rule(self):
        _write(self.root / ".claude/rules/a.md", "Why: see `feedback_foo_bar`.\n")
        f = ld.check_dead_memory_refs()
        self.assertEqual(self.kinds(f), ["dead-memory-ref"])
        self.assertEqual(f[0][1], 1)

    def test_flags_dash_and_wiki_forms_in_skills_agents_and_includes(self):
        _write(self.root / ".claude/skills/x/SKILL.md", "line\n[[feedback-foo]]\n")
        _write(self.root / ".claude/agents/y.md", "a feedback-memory entry\n")
        _write(self.root / "docs/ai-includes/z.md", "feedback_thing\n")
        self.assertEqual(len(ld.check_dead_memory_refs()), 3)

    def test_the_engine_study_is_scanned_and_the_rest_of_reference_is_not(self):
        _write(self.root / "docs/reference/engine/a.md", "see `feedback_foo`\n")
        _write(self.root / "docs/reference/other.md", "came from feedback_old\n")
        f = ld.check_dead_memory_refs()
        self.assertEqual(self.kinds(f), ["dead-memory-ref"])
        self.assertIn("engine", str(f[0][0]))

    def test_a_bare_memory_path_is_not_a_hit(self):
        _write(self.root / ".claude/rules/a.md", "files in `~/.claude/projects/x/memory/` load\n")
        self.assertEqual(ld.check_dead_memory_refs(), [])

    def test_lessons_rcas_and_adrs_stay_exempt(self):
        _write(self.root / "docs/reviews/lessons/a.md", "feedback_old\n")
        _write(self.root / "docs/reviews/rca-x.md", "feedback_old\n")
        _write(self.root / "docs/adrs/001.md", "feedback_old\n")
        self.assertEqual(ld.check_dead_memory_refs(), [])

    def test_ordinary_words_do_not_match(self):
        _write(self.root / ".claude/rules/a.md", "Give feedback to the user; a feedback loop.\n")
        self.assertEqual(ld.check_dead_memory_refs(), [])


class RuleSymbolTests(_Repo):
    def setUp(self):
        super().setUp()
        _write(self.root / "Main/Foo.cs", "public class RealThingService { }\n")
        _write(self.root / "tools/tests/test_x.py", "class KingdomNameTests:\n    pass\n")

    def test_a_missing_name_in_a_rule_is_a_finding(self):
        _write(self.root / ".claude/rules/a.md", "Use `GhostAdapter` here.\n")
        f = ld.check_rule_symbols()
        self.assertEqual(self.kinds(f), ["rule-symbol"])
        self.assertIn("GhostAdapter", f[0][3])

    def test_names_in_cs_or_tools_py_resolve(self):
        _write(self.root / ".claude/rules/a.md",
               "`RealThingService` and `KingdomNameTests` are real.\n")
        self.assertEqual(ld.check_rule_symbols(), [])

    def test_names_inside_code_fences_are_exempt(self):
        _write(self.root / ".claude/rules/a.md", "```\n`GhostAdapter`\n```\n")
        self.assertEqual(ld.check_rule_symbols(), [])

    def test_the_illustrative_marker_exempts_a_line(self):
        _write(self.root / ".claude/rules/a.md",
               "Name it like `FooService`. <!-- illustrative-name -->\n")
        self.assertEqual(ld.check_rule_symbols(), [])

    def test_other_suffixes_and_unbackticked_names_are_ignored(self):
        _write(self.root / ".claude/rules/a.md",
               "`GhostManager` and GhostService unticked and `lowerService`.\n")
        self.assertEqual(ld.check_rule_symbols(), [])

    def test_all_six_suffixes_are_checked(self):
        names = ["AService", "BAdapter", "CFactory", "DProvider", "ETests", "FHook"]
        _write(self.root / ".claude/rules/a.md", " ".join(f"`{n}`" for n in names) + "\n")
        self.assertEqual(len(ld.check_rule_symbols()), 6)

    def test_adrs_report_but_are_not_in_the_gating_list(self):
        _write(self.root / "docs/adrs/001.md", "We once had `GhostAdapter`.\n")
        self.assertEqual(ld.check_rule_symbols(), [])
        adr = ld.check_rule_symbols(adrs=True)
        self.assertEqual(self.kinds(adr), ["adr-symbol"])


class UnflaggedDotnetTests(_Repo):
    def test_a_taom_target_without_moduleid_is_a_finding(self):
        _write(self.root / "docs/reference/a.md", "Run `dotnet test TAOM.Tests --no-build`.\n")
        self.assertEqual(self.kinds(ld.check_unflagged_dotnet()), ["unflagged-dotnet"])

    def test_dotnet_build_of_main_and_a_sln_is_a_finding(self):
        _write(self.root / "docs/modding/a.md", "```\ndotnet build Main --no-restore\ndotnet build TAOM.sln\n```\n")
        self.assertEqual(len(ld.check_unflagged_dotnet()), 2)

    def test_a_line_with_moduleid_passes(self):
        _write(self.root / "docs/ai-includes/a.md",
               "dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=\n")
        self.assertEqual(ld.check_unflagged_dotnet(), [])

    def test_a_named_module_or_a_comment_is_not_the_empty_override(self):
        _write(self.root / "docs/reference/a.md",
               "dotnet test TAOM.Tests -p:ModuleId=TAOM\ndotnet test TAOM.Tests # clear ModuleId first\n")
        self.assertEqual(len(ld.check_unflagged_dotnet()), 2)

    def test_the_override_inside_a_code_span_passes(self):
        _write(self.root / "docs/reference/a.md", "Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.\n")
        self.assertEqual(ld.check_unflagged_dotnet(), [])

    def test_other_dirs_and_non_taom_targets_are_ignored(self):
        _write(self.root / "docs/features/a.md", "dotnet test TAOM.Tests\n")
        _write(self.root / "docs/reference/b.md", "dotnet build SomethingElse.sln\n")
        self.assertEqual(ld.check_unflagged_dotnet(), [])

    def test_skills_are_scanned(self):
        _write(self.root / ".claude/skills/s/SKILL.md", "dotnet test TAOM.Tests\n")
        self.assertEqual(len(ld.check_unflagged_dotnet()), 1)

    def test_rules_and_agents_are_scanned(self):
        _write(self.root / ".claude/rules/a.md", "dotnet test TAOM.Tests\n")
        _write(self.root / ".claude/agents/b.md", "dotnet build Main/TAOM.csproj\n")
        self.assertEqual(len(ld.check_unflagged_dotnet()), 2)

    def test_flags_before_the_target_are_read_past(self):
        _write(self.root / "docs/reference/a.md", "dotnet test --no-build TAOM.Tests\n")
        _write(self.root / "docs/reference/b.md", "dotnet build -c Debug Main/TAOM.csproj\n")
        self.assertEqual(len(ld.check_unflagged_dotnet()), 2)

    def test_a_command_wrapped_across_lines_is_judged_whole(self):
        _write(self.root / "docs/reference/a.md",
               "dotnet test TAOM.Tests \\\n  -p:DisableModuleCopy=true -p:ModuleId=\n"
               "dotnet test TAOM.Tests `\n  -p:DisableModuleCopy=true -p:ModuleId=\n")
        self.assertEqual(ld.check_unflagged_dotnet(), [])
        _write(self.root / "docs/reference/b.md", "dotnet test TAOM.Tests \\\n  --no-build\n")
        self.assertEqual([f[1] for f in ld.check_unflagged_dotnet()], [1])


class RuleDriftGateTests(_Repo):
    def test_the_findings_fail_the_drift_gate(self):
        import io
        from unittest import mock
        _write(self.root / ".claude/rules/a.md", "see feedback_x\n")
        _write(self.root / "CLAUDE.md", "@AGENTS.md\n@docs/ai-includes/orientation.md\n")
        _write(self.root / "AGENTS.md", "# a\n")
        _write(self.root / "docs/ai-includes/orientation.md", "# o\n\n## Trap index\n")
        _write(self.root / ".claude/pinned-game-version.txt", "v1.5.3\n")
        with mock.patch("sys.stdout", new_callable=io.StringIO) as out, \
                mock.patch.object(ld, "DOC_ROOTS", [self.root / "docs"]):
            rc = ld.main(["--drift-only"])
        self.assertEqual(rc, 1)
        self.assertIn("dead-memory-ref", out.getvalue())


class CommitScopeTests(_Repo):
    """Under --staged the rule drift checks judge what the commit records, and only that: another
    session's unstaged edit must not refuse this commit, and a staged bad line must not pass."""

    RULE = ".claude/rules/a.md"
    OTHER = ".claude/rules/other.md"
    BAD = "see feedback_x\n"

    def setUp(self):
        super().setUp()
        self._git("init", "-q")
        _write(self.root / self.RULE, "clean\n")
        _write(self.root / self.OTHER, "clean\n")
        self._git("add", "-A")
        self._git("commit", "-q", "-m", "base")

    def _git(self, *args):
        subprocess.run(["git", "-C", str(self.root), "-c", "user.email=t@t", "-c", "user.name=t", *args],
                       capture_output=True, text=True, check=True)

    def drift(self, staged=False, only_paths=None, base="HEAD"):
        return self.kinds(ld._rule_drift(ld.commit_view(staged, only_paths, base)))

    def test_a_staged_bad_line_with_a_clean_working_copy_is_a_finding(self):
        _write(self.root / self.RULE, self.BAD)
        self._git("add", self.RULE)
        _write(self.root / self.RULE, "clean\n")
        self.assertEqual(self.drift(staged=True), ["dead-memory-ref"])

    def test_an_unstaged_bad_line_with_a_clean_index_passes(self):
        _write(self.root / self.RULE, self.BAD)
        self.assertEqual(self.drift(staged=True), [])

    def test_another_files_unstaged_bad_line_does_not_refuse_this_commit(self):
        _write(self.root / self.OTHER, self.BAD)
        _write(self.root / self.RULE, "edited\n")
        self._git("add", self.RULE)
        self.assertEqual(self.drift(staged=True), [])

    def test_a_committed_bad_line_in_a_file_this_commit_leaves_alone_does_not_refuse_it(self):
        _write(self.root / self.OTHER, self.BAD)
        self._git("add", self.OTHER)
        self._git("commit", "-q", "-m", "bad")
        _write(self.root / self.RULE, "edited\n")
        self._git("add", self.RULE)
        self.assertEqual(self.drift(staged=True), [])
        self.assertEqual(self.drift(), ["dead-memory-ref"])

    def test_a_path_commit_reads_the_named_working_copy(self):
        _write(self.root / self.RULE, self.BAD)
        self.assertEqual(self.drift(staged=True, only_paths=[self.RULE]), ["dead-memory-ref"])

    def test_a_path_commit_ignores_what_is_staged_elsewhere(self):
        _write(self.root / self.OTHER, self.BAD)
        self._git("add", self.OTHER)
        _write(self.root / self.RULE, "edited\n")
        self.assertEqual(self.drift(staged=True, only_paths=[self.RULE]), [])

    def test_a_deleted_rule_is_no_finding(self):
        _write(self.root / self.OTHER, self.BAD)
        self._git("add", self.OTHER)
        self._git("commit", "-q", "-m", "bad")
        self._git("rm", "-q", self.OTHER)
        self.assertEqual(self.drift(staged=True), [])

    def test_an_amend_also_judges_what_head_added(self):
        _write(self.root / self.RULE, self.BAD)
        self._git("add", self.RULE)
        self._git("commit", "-q", "-m", "bad")
        self.assertEqual(self.drift(staged=True), [])
        self.assertEqual(self.drift(staged=True, base="HEAD~1"), ["dead-memory-ref"])

    def test_a_staged_symbol_and_a_staged_dotnet_line_are_read_from_the_index(self):
        _write(self.root / self.RULE, "Use `GhostAdapter`.\n")
        _write(self.root / "docs/reference/a.md", "dotnet test TAOM.Tests\n")
        self._git("add", self.RULE, "docs/reference/a.md")
        _write(self.root / self.RULE, "clean\n")
        _write(self.root / "docs/reference/a.md", "clean\n")
        self.assertEqual(sorted(self.drift(staged=True)), ["rule-symbol", "unflagged-dotnet"])

    def test_a_name_in_a_file_staged_in_the_same_commit_resolves(self):
        _write(self.root / self.RULE, "Use `NewGateTests`.\n")
        _write(self.root / "TAOM.Tests/NewGateTests.cs", "class NewGateTests { }\n")
        self._git("add", self.RULE, "TAOM.Tests/NewGateTests.cs")
        self.assertEqual(self.drift(staged=True), [])

    def test_a_name_resolves_against_the_index_with_a_commit_in_view(self):
        """Another session's unstaged removal of a class must not refuse a commit that edits a rule."""
        _write(self.root / "TAOM.Tests/GateTests.cs", "class GateTests { }\n")
        self._git("add", "TAOM.Tests/GateTests.cs")
        self._git("commit", "-q", "-m", "gate")
        _write(self.root / self.RULE, "Use `GateTests`.\n")
        self._git("add", self.RULE)
        _write(self.root / "TAOM.Tests/GateTests.cs", "class Renamed { }\n")
        self.assertEqual(self.drift(staged=True), [])
        self.assertEqual(self.drift(), ["rule-symbol"])

    def test_a_path_commit_resolves_a_name_in_a_working_copy_git_tracks(self):
        """A path commit records its named working copies, so the index is not what it holds."""
        _write(self.root / "TAOM.Tests/GateTests.cs", "class Other { }\n")
        self._git("add", "TAOM.Tests/GateTests.cs")
        self._git("commit", "-q", "-m", "gate")
        _write(self.root / "TAOM.Tests/GateTests.cs", "class GateTests { }\n")
        _write(self.root / self.RULE, "Use `GateTests`.\n")
        self.assertEqual(self.drift(staged=True, only_paths=[self.RULE, "TAOM.Tests/GateTests.cs"]), [])

    def test_a_grep_that_cannot_run_reports_no_symbol(self):
        """A tool fault is no finding: it must not refuse a commit."""
        _write(self.root / self.RULE, "Use `GateTests`.\n")
        self._git("add", self.RULE)
        broken = subprocess.CompletedProcess([], 128, "", "fatal")
        real = ld._git_run

        def run(*args):
            return broken if args[0] == "grep" else real(*args)
        with mock.patch.object(ld, "_git_run", side_effect=run):
            self.assertEqual(self.drift(staged=True), [])

    FEATURE = "docs/features/new.md"
    NO_SECTION = "# new\n\nNo verify heading.\n"
    WITH_SECTION = "# new\n\n## How to verify in game\n\nNot applicable: tooling.\n"

    def _feature(self, **kw):
        return self.kinds(ld.check_feature_doc_verify_section(**kw))

    def test_a_path_commit_ignores_another_sessions_staged_new_doc(self):
        _write(self.root / self.FEATURE, self.NO_SECTION)
        self._git("add", self.FEATURE)
        _write(self.root / self.RULE, "edited\n")
        self.assertEqual(self._feature(staged=True), ["no-verify-section"])
        self.assertEqual(self._feature(staged=True, only_paths=[self.RULE]), [])

    def test_a_path_commit_reads_a_named_new_doc_from_the_working_copy(self):
        _write(self.root / self.FEATURE, self.WITH_SECTION)
        self._git("add", self.FEATURE)
        _write(self.root / self.FEATURE, self.NO_SECTION)
        self.assertEqual(self._feature(staged=True), [])
        self.assertEqual(self._feature(staged=True, only_paths=[self.FEATURE]), ["no-verify-section"])

    def test_a_path_commit_of_an_existing_doc_is_not_new(self):
        _write(self.root / self.FEATURE, self.WITH_SECTION)
        self._git("add", self.FEATURE)
        self._git("commit", "-q", "-m", "doc")
        _write(self.root / self.FEATURE, self.NO_SECTION)
        self.assertEqual(self._feature(staged=True, only_paths=[self.FEATURE]), [])

    def test_a_path_commit_of_a_directory_spawns_a_small_constant_of_gits(self):
        for n in range(40):
            _write(self.root / f"docs/other/{n}.md", "x\n")
        self._git("add", "docs")
        self._git("commit", "-q", "-m", "many")
        calls = []
        real = ld._git_run

        def run(*args):
            calls.append(args)
            return real(*args)
        with mock.patch.object(ld, "_git_run", side_effect=run):
            self.assertEqual(self._feature(staged=True, only_paths=["docs"]), [])
        self.assertLess(len(calls), 8, calls)

    def test_a_new_doc_under_a_named_directory_is_judged(self):
        _write(self.root / self.FEATURE, self.NO_SECTION)
        self._git("add", self.FEATURE)
        self.assertEqual(self._feature(staged=True, only_paths=["docs"]), ["no-verify-section"])

    def test_a_path_commit_does_not_count_an_untracked_doc(self):
        _write(self.root / self.FEATURE, self.NO_SECTION)
        _write(self.root / self.RULE, "edited\n")
        self.assertEqual(self._feature(staged=True, only_paths=[self.RULE]), [])

    def _gate(self, *argv):
        with mock.patch("sys.stdout", new_callable=io.StringIO) as out, \
                mock.patch.object(ld, "DOC_ROOTS", [self.root / "docs"]), \
                mock.patch.object(ld, "check_context_budget", return_value=[]), \
                mock.patch.object(ld, "check_version_consistency", return_value=[]):
            return ld.main(list(argv)), out.getvalue()

    def test_the_gate_reads_the_index_under_staged(self):
        _write(self.root / self.RULE, self.BAD)
        self.assertEqual(self._gate("--drift-only", "--staged")[0], 0)
        self.assertEqual(self._gate("--drift-only")[0], 1)
        self._git("add", self.RULE)
        self.assertEqual(self._gate("--drift-only", "--staged")[0], 1)

    def test_the_gate_passes_only_paths_and_the_base_through(self):
        _write(self.root / self.RULE, self.BAD)
        self.assertEqual(self._gate("--drift-only", "--staged", "--only-paths", self.RULE)[0], 1)
        self.assertEqual(self._gate("--drift-only", "--staged", "--only-paths", self.OTHER)[0], 0)
        self._git("add", self.RULE)
        self._git("commit", "-q", "-m", "bad")
        self.assertEqual(self._gate("--drift-only", "--staged")[0], 0)
        self.assertEqual(self._gate("--drift-only", "--staged", "--dash-base", "HEAD~1")[0], 1)


class UnfiledFollowupTests(_Repo):
    def test_lists_reason_only_and_bare_lines_but_not_filed_ones(self):
        nl = chr(10)
        _write(self.root / "docs/reviews/rca-a.md", nl.join([
            "# RCA",
            "FOLLOW-UP: fix the widget, filed as #123.",
            "FOLLOW-UP: no issue: Mike files issues.",
            "FOLLOW-UP: tidy the cache.",
            "plain line", ""]))
        rows = ld.list_unfiled_followups()
        self.assertEqual([r[1] for r in rows], [3, 4])
        self.assertEqual(rows[0][0].name, "rca-a.md")

    def test_cli_prints_the_count_last_and_exits_zero(self):
        import io
        from unittest import mock
        _write(self.root / "docs/reviews/rca-a.md", "FOLLOW-UP: tidy the cache." + chr(10))
        with mock.patch("sys.stdout", new_callable=io.StringIO) as out:
            rc = ld.main(["--followups"])
        self.assertEqual(rc, 0)
        self.assertEqual(out.getvalue().strip().splitlines()[-1], "unfiled FOLLOW-UP lines: 1")


if __name__ == "__main__":
    unittest.main()
