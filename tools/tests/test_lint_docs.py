#!/usr/bin/env python3
"""Unit tests for the doc-linter drift checks (tools/lint_docs.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_lint_docs.py

Covers the two checks added for the v1.4.7-bump deep-review finding:
  - check_config_example_drift  -> a docs/features/*.md ```json example that disagrees
    with the shipped Main/_Module/ModuleData config it mirrors (the banner_color_config
    EnableLayerLimitTranspiler=true-in-doc case).
  - check_version_consistency   -> CLAUDE.md target / snapshot header != the pin.

Each test builds a SYNTHETIC repo tree in a tempdir and points lint_docs's REPO_ROOT /
DOCS_DIR at it, so the checks are exercised independently of the real repo contents.
"""
import io
import json
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


class _TempRepo(unittest.TestCase):
    """Base: spin up a temp repo tree and repoint lint_docs's roots at it."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self._orig_root = ld.REPO_ROOT
        self._orig_docs = ld.DOCS_DIR
        ld.REPO_ROOT = self.root
        ld.DOCS_DIR = self.root / "docs"

    def tearDown(self):
        ld.REPO_ROOT = self._orig_root
        ld.DOCS_DIR = self._orig_docs
        self._tmp.cleanup()

    def _feature_doc(self, name: str, cfg_rel: str, json_body: str) -> Path:
        doc = self.root / "docs" / "features" / name
        _write(doc, f"# {name}\n\n## Configuration\n\n`{cfg_rel}`:\n\n```json\n{json_body}\n```\n")
        return doc

    def _config(self, cfg_rel: str, text: str, bom: bool = False) -> None:
        p = self.root / cfg_rel
        p.parent.mkdir(parents=True, exist_ok=True)
        data = text.encode("utf-8")
        if bom:
            data = b"\xef\xbb\xbf" + data
        p.write_bytes(data)


class ConfigDriftTests(_TempRepo):
    CFG = "Main/_Module/ModuleData/configs/foo.json"

    def test_value_mismatch_flagged(self):
        self._config(self.CFG, '{"EnableX": false, "EnableY": true}')
        doc = self._feature_doc("foo.md", self.CFG, '{\n  "EnableX": true,\n  "EnableY": true\n}')
        findings = ld.check_config_example_drift([doc])
        self.assertEqual(len(findings), 1)
        self.assertIn("EnableX", findings[0][3])

    def test_matching_no_finding(self):
        self._config(self.CFG, '{"EnableX": false, "EnableY": true}')
        doc = self._feature_doc("foo.md", self.CFG, '{\n  "EnableX": false,\n  "EnableY": true\n}')
        self.assertEqual(ld.check_config_example_drift([doc]), [])

    def test_partial_example_ok(self):
        # Doc shows a SUBSET of shipped keys; all present keys match -> no finding.
        self._config(self.CFG, '{"EnableX": false, "EnableY": true, "EnableZ": false}')
        doc = self._feature_doc("foo.md", self.CFG, '{\n  "EnableX": false\n}')
        self.assertEqual(ld.check_config_example_drift([doc]), [])

    def test_extra_doc_key_flagged(self):
        # Doc shows a key the shipped config no longer has (renamed/removed).
        self._config(self.CFG, '{"EnableX": false}')
        doc = self._feature_doc("foo.md", self.CFG, '{\n  "EnableX": false,\n  "EnableRemoved": true\n}')
        findings = ld.check_config_example_drift([doc])
        self.assertEqual(len(findings), 1)
        self.assertIn("EnableRemoved", findings[0][3])

    def test_non_json_block_skipped(self):
        # Annotated example (ellipsis) is not valid JSON -> not comparable, no false positive.
        self._config(self.CFG, '{"EnableX": false}')
        doc = self._feature_doc("foo.md", self.CFG, '{\n  "EnableX": true,\n  ...\n}')
        self.assertEqual(ld.check_config_example_drift([doc]), [])

    def test_bom_shipped_config_read(self):
        # Shipped config with a UTF-8 BOM must still parse (utf-8-sig).
        self._config(self.CFG, '{"EnableX": false}', bom=True)
        doc = self._feature_doc("foo.md", self.CFG, '{\n  "EnableX": true\n}')
        self.assertEqual(len(ld.check_config_example_drift([doc])), 1)

    def test_missing_config_file_skipped(self):
        # Doc references a config that doesn't exist -> skip, no crash/finding.
        doc = self._feature_doc("foo.md", self.CFG, '{\n  "EnableX": true\n}')
        self.assertEqual(ld.check_config_example_drift([doc]), [])

    def test_historical_doc_exempt(self):
        # An rca-* transcript with drift is exempt (point-in-time snapshot).
        self._config(self.CFG, '{"EnableX": false}')
        doc = self.root / "docs" / "reviews" / "rca-foo-2026-01-01.md"
        _write(doc, f"# rca\n\n`{self.CFG}`:\n\n```json\n{{\n  \"EnableX\": true\n}}\n```\n")
        self.assertEqual(ld.check_config_example_drift([doc]), [])


class VersionConsistencyTests(_TempRepo):
    def _pin(self, v: str):
        _write(self.root / ".claude" / "pinned-game-version.txt", v + "\n")

    def _claude(self, target: str):
        _write(self.root / "CLAUDE.md", f"# CLAUDE.md\n\n> **Target: Bannerlord {target}** (installed).\n")

    def _snapshot(self, ver: str):
        _write(self.root / "docs" / "reference" / "taleworlds-api-snapshot" / "gamemodel-bases.md",
               f"# GameModel Base Signatures ({ver} snapshot)\n")

    def test_all_matching_no_finding(self):
        self._pin("v1.4.7")
        self._claude("1.4.7")
        self._snapshot("v1.4.7")
        self.assertEqual(ld.check_version_consistency(), [])

    def test_claude_target_mismatch_flagged(self):
        self._pin("v1.4.7")
        self._claude("1.4.6")  # stale
        findings = ld.check_version_consistency()
        self.assertTrue(any("CLAUDE.md target" in f[3] for f in findings))

    def test_snapshot_header_mismatch_flagged(self):
        self._pin("v1.4.7")
        self._snapshot("v1.4.5")  # stale
        findings = ld.check_version_consistency()
        self.assertTrue(any("snapshot header" in f[3] for f in findings))

    def test_v_prefix_agnostic(self):
        # "v1.4.7" pin vs "1.4.7" CLAUDE target must be treated equal.
        self._pin("v1.4.7")
        self._claude("1.4.7")
        self.assertEqual(ld.check_version_consistency(), [])

    def test_no_pin_is_noop(self):
        self._claude("1.4.6")
        self.assertEqual(ld.check_version_consistency(), [])

    def _agents(self, target: str):
        _write(self.root / "AGENTS.md", f"# TAOM\n\nTarget: Bannerlord {target} (installed).\n")

    def test_agents_target_mismatch_flagged(self):
        # ADR-011 moved the Target line into AGENTS.md; it must be checked like CLAUDE.md's was.
        self._pin("v1.5.3")
        self._agents("v1.5.2")
        findings = ld.check_version_consistency()
        self.assertTrue(any("AGENTS.md target" in f[3] for f in findings))

    def test_agents_target_matching_no_finding(self):
        self._pin("v1.5.3")
        self._agents("v1.5.3")
        self.assertEqual(ld.check_version_consistency(), [])

    def test_no_target_line_anywhere_is_flagged(self):
        # Moving the line must never switch the check off in silence.
        self._pin("v1.5.3")
        _write(self.root / "AGENTS.md", "# TAOM\n\nNo version stated here.\n")
        findings = ld.check_version_consistency()
        self.assertTrue(any("no 'Target: Bannerlord" in f[3] for f in findings))


class ContextBudgetTests(_TempRepo):
    """ADR-011 budgets: everything CLAUDE.md loads at launch, the trap index, and the rules."""

    TRAP_HEAD = "# o\n\n## Trap index\n\n| Trap | Rule | Doc |\n|---|---|---|\n"
    IMPORTS = "@AGENTS.md\n@docs/ai-includes/orientation.md\n\n"

    def _entry(self, claude="# c\n", agents="# a\n", orientation=None):
        _write(self.root / "CLAUDE.md", self.IMPORTS + claude)
        _write(self.root / "AGENTS.md", agents)
        _write(self.root / "docs" / "ai-includes" / "orientation.md",
               self.TRAP_HEAD if orientation is None else orientation)

    def _rule(self, name, body, paths=None):
        fm = "---\n" + (f'paths:\n  - "{paths}"\n' if paths else "") + "description: x\n---\n"
        _write(self.root / ".claude" / "rules" / name, fm + body)

    def _kinds(self):
        return [f[2] for f in ld.check_context_budget()]

    def test_small_entry_docs_and_rules_pass(self):
        self._entry()
        self._rule("a.md", "short\n")
        self.assertEqual(ld.check_context_budget(), [])

    def test_an_import_counts_toward_the_entry_budget(self):
        # orientation.md is @-imported by CLAUDE.md, so its bytes load at launch too.
        self._entry(orientation="x" * 300 + "\n")
        with mock.patch.object(ld, "ENTRY_DOCS_MAX_BYTES", 200), \
                mock.patch.object(ld, "ENTRY_DOCS_WARN_BYTES", 150):
            self.assertIn("size", self._kinds())

    def test_entry_docs_between_warn_and_cap_only_warn(self):
        self._entry(claude="x" * 170 + "\n")
        with mock.patch.object(ld, "ENTRY_DOCS_MAX_BYTES", 400), \
                mock.patch.object(ld, "ENTRY_DOCS_WARN_BYTES", 150):
            self.assertEqual(self._kinds(), ["size-warn"])

    def test_entry_doc_line_cap(self):
        self._entry(agents="line\n" * 250)
        self.assertIn("lines", self._kinds())

    def test_trap_index_row_over_cap(self):
        row = "| T | " + "r" * 200 + " | [d](x.md) |\n"
        self._entry(orientation=self.TRAP_HEAD + row)
        self.assertIn("trap-row", self._kinds())

    def test_trap_index_row_count_over_cap(self):
        rows = "".join(f"| T{i} | r | [d](x.md) |\n" for i in range(50))
        self._entry(orientation=self.TRAP_HEAD + rows)
        self.assertIn("trap-count", self._kinds())

    def test_unscoped_rules_are_counted_together(self):
        self._entry()
        self._rule("a.md", "x" * 120 + "\n")
        self._rule("b.md", "x" * 120 + "\n")
        with mock.patch.object(ld, "UNSCOPED_RULES_MAX_BYTES", 250), \
                mock.patch.object(ld, "UNSCOPED_RULES_WARN_BYTES", 200):
            self.assertIn("rules-size", self._kinds())

    def test_a_scoped_rule_is_not_counted_as_unscoped(self):
        self._entry()
        self._rule("scoped.md", "x" * 500 + "\n", paths="Main/**")
        with mock.patch.object(ld, "UNSCOPED_RULES_MAX_BYTES", 250), \
                mock.patch.object(ld, "UNSCOPED_RULES_WARN_BYTES", 200):
            self.assertNotIn("rules-size", self._kinds())

    def test_an_oversized_scoped_rule_only_warns_outside_a_git_tree(self):
        # No HEAD copy to compare with: fail open, the way the other git-reading checks do.
        self._entry()
        self._rule("scoped.md", "x" * 500 + "\n", paths="Main/**")
        with mock.patch.object(ld, "SCOPED_RULE_MAX_BYTES", 300):
            self.assertEqual(self._kinds(), ["size-warn"])

    def test_a_dead_link_in_an_entry_doc_or_a_rule_is_found(self):
        self._entry(claude="# c\n\n[gone](docs/missing.md)\n")
        self._rule("a.md", "[also gone](../../nope.md)\n")
        dead = ld.check_dead_links(ld.harness_link_files())
        self.assertEqual(sorted(p.name for p, *_ in dead), ["CLAUDE.md", "a.md"])

    # The entry docs are whatever CLAUDE.md imports, read from CLAUDE.md itself: a second
    # hand-kept list drifts from the imports and budgets the wrong files (#647 review).
    def test_entry_docs_follow_the_imports_in_load_order(self):
        _write(self.root / "CLAUDE.md", "@docs/x.md\n")
        _write(self.root / "docs" / "x.md", "@y.md\n")
        _write(self.root / "docs" / "y.md", "# y\n")
        _write(self.root / "AGENTS.md", "# not imported\n")
        docs, missing = ld.entry_docs()
        self.assertEqual([ld.rel(p) for p in docs], ["CLAUDE.md", "docs/x.md", "docs/y.md"])
        self.assertEqual(missing, [])

    def test_imports_stop_after_four_hops(self):
        _write(self.root / "CLAUDE.md", "@docs/h1.md\n")
        for i in range(1, 7):
            _write(self.root / "docs" / f"h{i}.md", f"@h{i + 1}.md\n" if i < 6 else "# end\n")
        docs, _ = ld.entry_docs()
        self.assertEqual([p.name for p in docs], ["CLAUDE.md", "h1.md", "h2.md", "h3.md", "h4.md"])

    def test_a_missing_import_is_a_gating_finding(self):
        self._entry(claude="@docs/gone.md\n")
        self.assertIn("import-missing", self._kinds())

    def test_an_import_inside_code_is_not_followed(self):
        self._entry(claude="Write `@docs/gone.md` to import.\n\n```\n@docs/also-gone.md\n```\n")
        self.assertNotIn("import-missing", self._kinds())

    # A renamed heading or a dropped import must not switch the row caps off in silence.
    def test_a_renamed_trap_index_heading_is_a_gating_finding(self):
        self._entry(orientation="# o\n\n## Traps\n\n| Trap | Rule | Doc |\n|---|---|---|\n")
        self.assertIn("trap-index-missing", self._kinds())

    def test_a_trap_index_no_longer_imported_is_a_gating_finding(self):
        _write(self.root / "CLAUDE.md", "@AGENTS.md\n")
        _write(self.root / "AGENTS.md", "# a\n")
        self.assertIn("trap-index-missing", self._kinds())

    # Codex review 2026-09-23 (#647): the import scan must read Markdown the way Claude Code
    # does, or a loaded file escapes the budget and a code example fails CI.
    def test_a_longer_fence_is_closed_only_by_a_fence_as_long(self):
        self._entry(claude="````markdown\n```\n````\n@docs/extra.md\n")
        _write(self.root / "docs" / "extra.md", "# extra\n")
        docs, missing = ld.entry_docs()
        self.assertIn("docs/extra.md", [ld.rel(p) for p in docs])
        self.assertEqual(missing, [])

    def test_a_code_span_across_lines_hides_an_import(self):
        self._entry(claude="Example `code\n@docs/not-an-import.md\nend` here.\n")
        self.assertNotIn("import-missing", self._kinds())

    def test_a_rule_in_a_subfolder_is_measured(self):
        # Claude Code discovers .claude/rules recursively.
        self._entry()
        _write(self.root / ".claude" / "rules" / "nested" / "deep.md", "x" * 300 + "\n")
        with mock.patch.object(ld, "UNSCOPED_RULES_MAX_BYTES", 250), \
                mock.patch.object(ld, "UNSCOPED_RULES_WARN_BYTES", 200):
            self.assertIn("rules-size", self._kinds())
        self.assertIn(".claude/rules/nested/deep.md",
                      [r["path"] for r in ld.context_budget_snapshot()["rules"]])

    def test_a_rule_whose_frontmatter_does_not_parse_counts_as_unscoped(self):
        # Claude Code loads such a rule in every session; the budget must see it that way.
        self._entry()
        _write(self.root / ".claude" / "rules" / "broken.md",
               '---\npaths: [Main/**\ndescription: x\n---\n' + "x" * 300 + "\n")
        with mock.patch.object(ld, "UNSCOPED_RULES_MAX_BYTES", 250), \
                mock.patch.object(ld, "UNSCOPED_RULES_WARN_BYTES", 200):
            kinds = self._kinds()
        self.assertIn("rule-frontmatter-invalid", kinds)
        self.assertIn("rules-size", kinds)

    def test_an_unquoted_colon_in_a_description_breaks_the_frontmatter(self):
        self._entry()
        _write(self.root / ".claude" / "rules" / "colon.md",
               '---\npaths:\n  - "Main/**"\ndescription: Use when: things break\n---\nbody\n')
        self.assertIn("rule-frontmatter-invalid", self._kinds())

    def test_crlf_and_lf_count_the_same_bytes(self):
        # A Windows checkout may hold CRLF where CI holds LF; the gate must agree on both.
        self._entry()
        p = self.root / "CLAUDE.md"
        body = (self.IMPORTS + "line\n" * 40).encode("utf-8")
        p.write_bytes(body)
        lf = ld.context_budget_snapshot()["entry_docs"]
        p.write_bytes(body.replace(b"\n", b"\r\n"))
        self.assertEqual(ld.context_budget_snapshot()["entry_docs"], lf)
        self.assertEqual(lf[0]["bytes"], len(body))


class DriftGateExitCodeTests(_TempRepo):
    """The exit code CI and the commit hook act on, not only the finding lists."""

    def setUp(self):
        super().setUp()
        roots = mock.patch.object(ld, "DOC_ROOTS", [self.root / "docs"])
        roots.start()
        self.addCleanup(roots.stop)
        _write(self.root / ".claude" / "pinned-game-version.txt", "v1.5.3\n")
        _write(self.root / "AGENTS.md", "# a\n\nTarget: Bannerlord v1.5.3 (installed).\n")
        _write(self.root / "CLAUDE.md", ContextBudgetTests.IMPORTS)
        _write(self.root / "docs" / "ai-includes" / "orientation.md", ContextBudgetTests.TRAP_HEAD)

    def _run(self, *argv):
        with mock.patch("sys.stdout", new_callable=io.StringIO) as out:
            rc = ld.main(list(argv))
        return rc, out.getvalue()

    def test_drift_only_passes_a_clean_tree(self):
        self.assertEqual(self._run("--drift-only")[0], 0)

    def test_drift_only_fails_on_a_version_mismatch(self):
        _write(self.root / "AGENTS.md", "# a\n\nTarget: Bannerlord v1.5.2 (installed).\n")
        self.assertEqual(self._run("--drift-only")[0], 1)

    def test_a_size_warning_does_not_fail_the_gate(self):
        with mock.patch.object(ld, "ENTRY_DOCS_WARN_BYTES", 10):
            rc, out = self._run("--drift-only")
        self.assertEqual(rc, 0)
        self.assertIn("size-warn", out)

    def test_a_line_cap_breach_fails_the_gate(self):
        _write(self.root / "AGENTS.md", "Target: Bannerlord v1.5.3\n" + "line\n" * 250)
        self.assertEqual(self._run("--drift-only")[0], 1)

    def test_the_full_run_gates_the_same_way(self):
        _write(self.root / "AGENTS.md", "Target: Bannerlord v1.5.3\n" + "line\n" * 250)
        self.assertEqual(self._run("--fail-on-drift")[0], 1)

    def test_drift_only_prints_only_the_gating_checks(self):
        _write(self.root / "CLAUDE.md", ContextBudgetTests.IMPORTS + "[gone](docs/missing.md)\n")
        rc, out = self._run("--drift-only")
        self.assertEqual(rc, 0)
        self.assertNotIn("Dead links", out)

    def test_context_budget_json_names_the_loaded_files_and_caps(self):
        rc, out = self._run("--context-budget-json")
        data = json.loads(out)
        self.assertEqual(rc, 0)
        self.assertEqual([d["path"] for d in data["entry_docs"]],
                         ["CLAUDE.md", "AGENTS.md", "docs/ai-includes/orientation.md"])
        self.assertEqual(data["caps"]["ENTRY_DOCS_MAX_BYTES"], ld.ENTRY_DOCS_MAX_BYTES)


class NormVerTests(unittest.TestCase):
    def test_strips_v_and_lowercases(self):
        self.assertEqual(ld._norm_ver("V1.4.7"), "1.4.7")
        self.assertEqual(ld._norm_ver("  v1.4.7  "), "1.4.7")
        self.assertEqual(ld._norm_ver("1.4.7"), "1.4.7")


class _PathConstantRepo(_TempRepo):
    """_TempRepo + the path constants derived from DOCS_DIR at import time.

    lint_docs computes ADRS_DIR / REVIEWS_RAW_DIR / the exempt prefixes once at module load,
    so repointing DOCS_DIR alone leaves them aimed at the real repo. Repoint them too, or the
    synthetic tree is linted against the real repo's exemptions.
    """

    def setUp(self):
        super().setUp()
        self._orig_consts = (ld.ADRS_DIR, ld.REVIEWS_RAW_DIR, ld.STALE_VERSION_EXEMPT_PREFIXES)
        ld.ADRS_DIR = ld.DOCS_DIR / "adrs"
        ld.REVIEWS_RAW_DIR = ld.DOCS_DIR / "reviews" / "raw"
        ld.STALE_VERSION_EXEMPT_PREFIXES = (str(ld.ADRS_DIR).replace("\\", "/"),)

    def tearDown(self):
        (ld.ADRS_DIR, ld.REVIEWS_RAW_DIR, ld.STALE_VERSION_EXEMPT_PREFIXES) = self._orig_consts
        super().tearDown()

    def _doc(self, rel: str, body: str) -> Path:
        p = self.root / "docs" / rel
        _write(p, body)
        return p


class StaleVersionRotTests(_PathConstantRepo):
    """#397: the check reported 29 findings, all historical references, and no real rot.

    Its model was "a version string older than the pin is rot". The model here is "a version
    string PRESENTED AS THE CURRENT TARGET is rot", so these tests pin both directions: real
    rot must still fire (or the fix is indistinguishable from deleting the check), and each
    shape of legitimate historical reference must not.
    """

    def setUp(self):
        super().setUp()
        _write(self.root / ".claude" / "pinned-game-version.txt", "v1.4.7\n")

    def _stale(self, rel: str, body: str):
        return ld.check_stale_versions([self._doc(rel, body)])

    def test_naming_an_old_version_as_the_current_target_is_still_reported(self):
        """The fixture #397 asks for: the check must still fire on genuine rot."""
        found = self._stale("features/rotten.md", "The current target is Bannerlord 1.3.15.\n")
        self.assertEqual(len(found), 1, "genuine rot must still be reported")
        self.assertEqual(found[0][1], 1)

    def test_port_note_recording_when_it_happened_is_not_reported(self):
        # docs/features/companion-tactics.md:11 shape
        self.assertEqual(
            self._stale("features/port.md", "Ported from the 1.3 template for TAOM v1.3.15.\n"), [])

    def test_self_labelled_historical_baseline_is_not_reported(self):
        # docs/features/native-skin-fixes.md:123 shape — 10 of the original 29 were this file
        self.assertEqual(
            self._stale("features/rva.md", "| `historicalRva` | v1.3.15 reference RVA, informational only |\n"), [])

    def test_line_that_also_names_the_pin_is_a_contrast_not_a_claim(self):
        # docs/features/native-skin-fixes.md:57 shape: "current" belongs to the pin on the same line
        self.assertEqual(
            self._stale("features/contrast.md",
                        "That mod ships a v1.3.15-only DLL. TAOM tracks the current engine (v1.4.7).\n"), [])

    def test_present_tense_word_inside_inline_code_is_not_a_claim(self):
        # docs/features/messengers.md:23 shape: `CampaignTime.Now` is an identifier, not "now"
        self.assertEqual(
            self._stale("features/api.md",
                        "- Bannerlord 1.3.15 introduced API breaks: use `CampaignTime.Now` for elapsed math.\n"), [])

    def test_adrs_are_exempt_as_point_in_time_records(self):
        # The exemption comment already claimed ADRs were covered; the tuple never included them,
        # so adrs/010 — the ADR that recorded this very problem — reported itself as rot.
        self.assertEqual(
            self._stale("adrs/010-thing.md", "Stale refs (`Bannerlord 1.3.15` when the current target is 1.4.5).\n"), [])

    # --- #405 gap 1: rot phrased without a marker word --------------------------------------
    # Each of these named a current target and was silent before the marker set was widened.
    # One test per shape, so a later narrowing of the regex says WHICH phrasing it gave up.

    def test_built_for_is_a_current_target_claim(self):
        self.assertEqual(
            len(self._stale("features/a.md", "TAOM is built for Bannerlord 1.3.15.\n")), 1)

    def test_requires_is_a_current_target_claim(self):
        self.assertEqual(
            len(self._stale("features/b.md", "This feature requires Bannerlord 1.3.15.\n")), 1)

    def test_runs_on_is_a_current_target_claim(self):
        self.assertEqual(
            len(self._stale("features/c.md", "TAOM runs on Bannerlord 1.3.15.\n")), 1)

    def test_compatible_with_is_a_current_target_claim(self):
        self.assertEqual(
            len(self._stale("features/d.md", "Compatible with Bannerlord 1.3.15.\n")), 1)

    def test_bare_engine_label_is_a_current_target_claim(self):
        # `Engine\s*:` has to live outside the \b group — a word boundary after a colon needs a
        # following word character, so this shape is silently missed if it is written inside it.
        self.assertEqual(
            len(self._stale("features/e.md", "Engine: 1.3.15\n")), 1)

    # --- #405 gap 2: 1.4.5 / 1.4.6, and the shapes that made them noisy -----------------------
    # agent-operating-manual.md names both stale alongside 1.3.15, but the patterns never looked
    # for them. They are one and two steps back rather than five, so they appear all over ordinary
    # prose — 24 raw hits, of which 22 were noise. Each test below pins one reason a hit was noise.

    def test_a_current_claim_about_145_is_reported(self):
        self.assertEqual(len(self._stale("features/v1.md", "The current target is Bannerlord 1.4.5.\n")), 1)

    def test_a_current_claim_about_146_is_reported(self):
        self.assertEqual(len(self._stale("features/v2.md", "TAOM currently builds against v1.4.6.\n")), 1)

    def test_the_branch_name_is_not_a_version_claim(self):
        # The active branch IS `bannerlord-1.4.5`, quoted 53 times across 21 docs. This sentence is
        # correct, current prose and says "currently" — no wording rule could separate the two.
        self.assertEqual(
            self._stale("localization/g.md",
                        "Open a PR targeting the active branch (currently `bannerlord-1.4.5`).\n"), [])

    def test_a_hyphenated_compound_is_not_a_version_claim(self):
        # spider.md:262 — "(2026-06-12, post-1.4.6 campaign)" says WHEN the campaign ran.
        self.assertEqual(
            self._stale("features/hy.md",
                        "## Current state & known issues (2026-06-12, post-1.4.6 campaign)\n"), [])

    # The claim/silence pairs below exist because a guard that only ever has a negative test can
    # be over-broad without anything noticing. Each silence above has a firing counterpart.

    def test_the_copula_form_of_target_is_still_a_claim(self):
        # Caught by the pre-#405 checker with a bare marker; narrowing to "marker immediately
        # before a version" dropped it, which is a worse defect than the noise it removed.
        for line in ("The target is Bannerlord 1.3.15.\n",
                     "Our target version is 1.3.15.\n",
                     "Our target remains 1.3.15.\n"):
            self.assertEqual(len(self._stale(f"features/c{hash(line) & 0xffff}.md", line)), 1, line)

    def test_the_copula_form_of_active_is_still_a_claim(self):
        for line in ("The active version is 1.3.15.\n",
                     "Bannerlord 1.3.15 is the active engine.\n"):
            self.assertEqual(len(self._stale(f"features/a{hash(line) & 0xffff}.md", line)), 1, line)

    def test_a_marker_in_a_different_sentence_does_not_pair(self):
        # castle-recruitment.md:117 shape — "Current" opens a new sentence, about the data.
        self.assertEqual(
            self._stale("features/two.md",
                        "The engine throws rather than returning null on v1.4.6. "
                        "Current dev data has full coverage.\n"), [])

    def test_a_marker_in_a_different_table_cell_does_not_pair(self):
        self.assertEqual(
            self._stale("features/tbl.md",
                        "| Patch targets exist in v1.4.5 | the current pin is elsewhere |\n"), [])

    def test_a_cross_reference_aside_is_not_a_claim(self):
        # native-skin-fixes.md:322 — `(see "v1.4.6 native port")` names a section.
        self.assertEqual(
            self._stale("features/see.md",
                        'The fastest path is now `tools/x.py` (see "v1.4.6 native port" below).\n'), [])

    def test_an_explicitly_negated_version_is_not_a_claim(self):
        # elephant.md:117 — "built for Bannerlord ~1.2.12, NOT 1.4.5".
        self.assertEqual(
            self._stale("features/neg.md",
                        "The upstream pack is built for Bannerlord ~1.2.12, NOT 1.4.5.\n"), [])

    def test_target_as_a_noun_is_not_a_claim(self):
        # "hook targets", "patch targets" — the thing a patch points at, next to a version in
        # exactly the docs that discuss porting.
        self.assertEqual(
            self._stale("features/noun.md", "The v1.4.6 hook targets remain authored + verified.\n"), [])

    def test_target_as_a_verb_before_a_version_is_a_claim(self):
        self.assertEqual(len(self._stale("features/verb.md", "TAOM targets Bannerlord 1.4.5.\n")), 1)

    def test_target_as_a_label_is_a_claim(self):
        # The CLAUDE.md header shape — the highest-value site this check exists for.
        self.assertEqual(len(self._stale("features/lbl.md", "> **Target: Bannerlord 1.4.6**\n")), 1)

    def test_active_as_an_adjective_is_not_a_claim(self):
        # lotrlome-armory-snapshot/README.md:99 — "423 missing active action types".
        self.assertEqual(
            self._stale("features/adj.md",
                        "By Native 1.4.6 it had drifted to 423 missing active action types.\n"), [])

    def test_engine_label_counts_but_engine_mid_sentence_does_not(self):
        self.assertEqual(len(self._stale("features/lab.md", "Engine: 1.4.6\n")), 1)
        # elephant.md:309 — "verified against the engine:" is punctuation, not a label.
        self.assertEqual(
            self._stale("features/mid.md",
                        "Bearing verified against the engine: `Vec2.LeftVec()` in the v1.4.5 decompile.\n"), [])

    def test_widening_did_not_swallow_the_historical_shapes(self):
        # The four shapes #399 exists to keep quiet must stay quiet after the widening.
        for name, line in [
            ("features/h1.md", "Ported from the 1.3 template for TAOM v1.3.15.\n"),
            ("features/h2.md", "| `historicalRva` | v1.3.15 reference RVA, informational only |\n"),
            ("features/h3.md", "Pinned v1.3.15 ilspycmd outputs live in docs/scene-scripts/sigs/.\n"),
            ("features/h4.md", "- Bannerlord 1.3.15 introduced API breaks: use `CampaignTime.Now`.\n"),
        ]:
            self.assertEqual(self._stale(name, line), [], f"{line.strip()!r} should stay silent")

    def test_a_mention_of_one_version_does_not_silence_a_claim_about_another(self):
        """The guards must `continue`, not `break`, or the pattern list retires on the mention.

        1.3.15 sorts before 1.4.5 in STALE_VERSION_PATTERNS, so a line carrying both a historical
        mention and a live claim gets its mention evaluated first. If failing that guard abandoned
        the whole list, the claim after it would never be tested and real rot would read as clean —
        the one failure mode worse than a false positive, because a check that stops reporting says
        nothing about having stopped.
        """
        found = self._stale(
            "features/mixed.md",
            "Historical: v1.3.15 shipped. The current target is Bannerlord 1.4.5.\n")
        self.assertEqual(len(found), 1, "the 1.4.5 claim must survive the 1.3.15 mention")
        self.assertEqual(found[0][2], "1.4.5")

    def test_a_negated_version_does_not_silence_a_claim_after_it(self):
        """Same defect in the negation guard: `NOT 1.4.5` must not retire the 1.4.6 pattern."""
        found = self._stale(
            "features/negated.md",
            "Built for ~1.2.12, NOT 1.4.5. The current target is 1.4.6.\n")
        self.assertEqual(len(found), 1, "the 1.4.6 claim must survive the negated 1.4.5")
        self.assertEqual(found[0][2], "1.4.6")

    def test_the_pin_guard_still_retires_the_whole_line(self):
        """The contrast guard is per-line, not per-match, so it correctly keeps using `break`.

        Naming the pin is a property of the sentence, not of one version in it — a line that
        contrasts two old versions against the pin is one contrast, and must stay silent for both.
        """
        self.assertEqual(
            self._stale("features/pinned.md",
                        "We shipped on 1.4.5 and then 1.4.6; TAOM now targets v1.4.7.\n"), [])


class DeadLinkNeverCommittedTests(_PathConstantRepo):
    """#397 follow-on: 14 dead links, all pointing into the gitignored docs/reviews/raw/.

    Those transcripts exist only on the machine that ran the review, so the check read clean
    for the author and dirty on every fresh clone. Exempting by TARGET keeps dead-link coverage
    for the linking files' other links.
    """

    def test_link_into_the_gitignored_raw_dir_is_not_reported(self):
        doc = self._doc("reviews/rca-thing.md", "See [review](raw/codex-adversarial-thing.md).\n")
        self.assertEqual(ld.check_dead_links([doc]), [])

    def test_a_genuinely_missing_target_is_still_reported(self):
        doc = self._doc("reviews/rca-thing.md", "See [notes](../features/gone.md).\n")
        self.assertEqual(len(ld.check_dead_links([doc])), 1, "real dead links must still be reported")

    def test_an_existing_target_is_not_reported(self):
        self._doc("features/here.md", "# here\n")
        doc = self._doc("reviews/rca-thing.md", "See [notes](../features/here.md).\n")
        self.assertEqual(ld.check_dead_links([doc]), [])


class UntrackedLinkTargetTests(_PathConstantRepo):
    """#517: a link target that exists on disk but is not tracked by git.

    `check_dead_links` resolves with `Path.exists()`, a filesystem stat, so trackedness is
    invisible to it. On 2026-08-28 docs/INDEX.md and docs/reference/doc-lookup.md were pushed
    carrying links to docs/features/armoury-mesh-cleanup.md while that file was still untracked:
    the linter read 0 dead links locally and the remote carried 2. Same asymmetry as the
    gitignored-raw case, opposite cause. Report-only, and it must never fire on a missing
    target, which is check_dead_links' job.
    """

    def test_untracked_target_is_reported(self):
        self._doc("features/brand-new.md", "# new\n")
        doc = self._doc("INDEX.md", "See [new](features/brand-new.md).\n")
        found = ld.check_untracked_link_targets(
            [doc], untracked={ld.REPO_ROOT / "docs" / "features" / "brand-new.md"})
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0][2], "features/brand-new.md")

    def test_tracked_target_is_not_reported(self):
        self._doc("features/committed.md", "# committed\n")
        doc = self._doc("INDEX.md", "See [doc](features/committed.md).\n")
        self.assertEqual(ld.check_untracked_link_targets([doc], untracked=set()), [])

    def test_a_missing_target_is_left_to_the_dead_link_check(self):
        # Not on disk at all, so it is not "untracked", it is dead. Reporting it here too
        # would double-count every dead link.
        doc = self._doc("INDEX.md", "See [gone](features/gone.md).\n")
        self.assertEqual(
            ld.check_untracked_link_targets(
                [doc], untracked={ld.REPO_ROOT / "docs" / "features" / "gone.md"}), [])

    def test_no_untracked_paths_means_no_findings(self):
        # Fail open: git unavailable yields an empty set, so this can only under-report.
        self._doc("features/thing.md", "# thing\n")
        doc = self._doc("INDEX.md", "See [t](features/thing.md).\n")
        self.assertEqual(ld.check_untracked_link_targets([doc], untracked=set()), [])

    def test_gitignored_raw_targets_stay_exempt(self):
        # --exclude-standard already omits ignored files, but pin it: a raw transcript must
        # not migrate from "exempt dead link" to "untracked link".
        doc = self._doc("reviews/rca-thing.md", "See [review](raw/codex-thing.md).\n")
        self.assertEqual(
            ld.check_untracked_link_targets(
                [doc], untracked={ld.REPO_ROOT / "docs" / "reviews" / "raw" / "codex-thing.md"}), [])


EM = "—"
EN = "–"


class AiDashScannerTests(unittest.TestCase):
    """The pure scanner behind check_ai_dashes.

    Em and en dashes are the loudest AI-writing tell, so produced prose must not
    contain them (.claude/rules/output-style.md Part 2). Hyphens stay legal: CLI
    flags, version numbers and kebab-case filenames are full of them, and matching
    those would make the check unusable.
    """

    P = Path("docs/features/thing.md")

    def _scan(self, text, only_lines=None):
        return ld.scan_text_for_dashes(self.P, text, only_lines)

    def test_a_bare_em_dash_in_prose_is_reported(self):
        found = self._scan(f"The guard fires early {EM} before state init.\n")
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0][1], 1)
        self.assertEqual(found[0][2], "em-dash")

    def test_a_bare_en_dash_in_prose_is_reported(self):
        found = self._scan(f"Pages 10{EN}14 cover the bump.\n")
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0][2], "en-dash")

    def test_a_dash_inside_a_fenced_block_is_not_reported(self):
        self.assertEqual(self._scan(f"Intro.\n\n```\ncode {EM} sample\n```\n"), [])

    def test_a_tilde_fence_also_suppresses(self):
        self.assertEqual(self._scan(f"Intro.\n\n~~~\ncode {EM} sample\n~~~\n"), [])

    def test_a_backtick_fence_inside_a_tilde_fence_does_not_close_it(self):
        self.assertEqual(self._scan(f"~~~\n```\nstill code {EM} here\n```\n~~~\n"), [])

    def test_prose_after_a_closed_fence_is_still_scanned(self):
        found = self._scan(f"```\ncode\n```\n\nProse {EM} here.\n")
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0][1], 5)

    def test_a_dash_inside_an_inline_code_span_is_not_reported(self):
        self.assertEqual(self._scan(f"Run `taom {EM} src` to check.\n"), [])

    def test_a_dash_inside_a_link_target_is_not_reported(self):
        self.assertEqual(self._scan(f"See [notes](docs/a{EM}b.md) for detail.\n"), [])

    def test_a_dash_inside_a_bare_url_is_not_reported(self):
        self.assertEqual(self._scan(f"Source: https://example.com/a{EM}b\n"), [])

    def test_a_dash_outside_the_code_span_on_the_same_line_is_still_reported(self):
        found = self._scan(f"Run `build.ps1` first {EM} it deploys the module.\n")
        self.assertEqual(len(found), 1)

    def test_hyphens_in_flags_versions_and_filenames_are_not_reported(self):
        text = (
            "Run `./build.ps1 -RunTests` on v1.4.8.\n"
            "The hook is check-freeze.sh, a kebab-case name.\n"
            "Pass --fail-on-drift to gate the commit.\n"
            "A well-known cross-platform trade-off.\n"
        )
        self.assertEqual(self._scan(text), [])

    def test_an_explicit_allow_marker_suppresses_the_line(self):
        text = f'Vanilla emits "load {EM} failed". <!-- lint-allow-dash -->\n'
        self.assertEqual(self._scan(text), [])

    def test_one_finding_per_line_even_with_several_dashes(self):
        found = self._scan(f"A {EM} b {EM} c {EN} d.\n")
        self.assertEqual(len(found), 1)

    def test_only_lines_restricts_the_scan(self):
        text = f"Old prose {EM} untouched.\nNew prose {EM} added.\n"
        found = self._scan(text, only_lines={2})
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0][1], 2)


class _GitRepo(unittest.TestCase):
    """A throwaway git repository that stands in for REPO_ROOT, for the checks that read git."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self._orig_root = ld.REPO_ROOT
        ld.REPO_ROOT = self.root
        self._git("init", "-q")

    def tearDown(self):
        ld.REPO_ROOT = self._orig_root
        self._tmp.cleanup()

    def _git(self, *args):
        subprocess.run(
            ["git", "-C", str(self.root), "-c", "user.email=t@t", "-c", "user.name=t", *args],
            capture_output=True, text=True, check=True,
        )

    def _commit(self, msg="c"):
        self._git("add", "-A")
        self._git("commit", "-q", "-m", msg)

    def _gate(self, *argv):
        with mock.patch.object(ld, "DOC_ROOTS", [self.root / "docs"]), \
                mock.patch.object(ld, "check_context_budget", return_value=[]), \
                mock.patch.object(ld, "check_version_consistency", return_value=[]), \
                mock.patch("sys.stdout", new_callable=io.StringIO) as out:
            return ld.main(list(argv)), out.getvalue()


class ScopedRuleRatchetTests(_GitRepo):
    """An over-cap path rule gates only when it is larger than its HEAD copy: the working copy
    normally, the staged copy under --staged."""

    RULE = ".claude/rules/scoped.md"

    def setUp(self):
        super().setUp()
        _write(self.root / "CLAUDE.md", ContextBudgetTests.IMPORTS)
        _write(self.root / "AGENTS.md", "# a\n")
        _write(self.root / "docs" / "ai-includes" / "orientation.md", ContextBudgetTests.TRAP_HEAD)
        self._set_rule(500)
        self._commit()
        cap = mock.patch.object(ld, "SCOPED_RULE_MAX_BYTES", 300)
        cap.start()
        self.addCleanup(cap.stop)

    def _set_rule(self, size):
        _write(self.root / self.RULE, '---\npaths:\n  - "Main/**"\ndescription: x\n---\n' + "x" * size + "\n")

    def _kinds(self, staged=False, only_paths=None, base="HEAD"):
        return [f[2] for f in ld.check_context_budget(ld.commit_view(staged, only_paths, base))]

    def test_staged_growth_past_the_cap_gates_with_the_working_copy_under_it(self):
        self._set_rule(100)
        self._commit()
        self._set_rule(500)
        self._git("add", self.RULE)
        self._set_rule(100)
        self.assertEqual(self._kinds(staged=True), ["scoped-rule-size"])
        self.assertEqual(self._kinds(), [])

    def test_a_working_copy_over_the_cap_with_the_index_under_it_is_not_this_commits(self):
        self._set_rule(100)
        self._commit()
        self._set_rule(500)
        self.assertEqual(self._kinds(staged=True), [])
        self.assertEqual(self._kinds(), ["scoped-rule-size"])

    def test_a_path_commit_judges_the_named_working_copy(self):
        self._set_rule(600)
        self.assertEqual(self._kinds(staged=True, only_paths=[self.RULE]), ["scoped-rule-size"])

    def test_a_path_commit_does_not_judge_a_rule_it_leaves_out(self):
        self._set_rule(600)
        self._git("add", self.RULE)
        self.assertEqual(self._kinds(staged=True, only_paths=["CLAUDE.md"]), ["size-warn"])

    def test_an_amend_compares_with_the_base(self):
        self._set_rule(600)
        self._commit()
        self.assertEqual(self._kinds(staged=True), ["size-warn"])
        self.assertEqual(self._kinds(staged=True, base="HEAD~1"), ["scoped-rule-size"])

    def test_an_over_cap_rule_that_did_not_grow_only_warns(self):
        self.assertEqual(self._kinds(), ["size-warn"])

    def test_an_over_cap_rule_that_shrank_only_warns(self):
        self._set_rule(400)
        self.assertEqual(self._kinds(), ["size-warn"])

    def test_an_over_cap_rule_that_grew_gates(self):
        self._set_rule(600)
        self.assertEqual(self._kinds(), ["scoped-rule-size"])

    def test_a_new_over_cap_rule_gates(self):
        _write(self.root / ".claude/rules/fresh.md", '---\npaths:\n  - "Main/**"\n---\n' + "y" * 500 + "\n")
        self.assertEqual(sorted(self._kinds()), ["scoped-rule-size", "size-warn"])

    def test_growth_under_the_cap_is_silent(self):
        self._set_rule(100)
        self._commit()
        self._set_rule(200)
        self.assertEqual(self._kinds(), [])

    def test_staged_mode_ignores_an_unstaged_growth(self):
        self._set_rule(600)
        self.assertEqual(self._kinds(staged=True), ["size-warn"])

    def test_staged_mode_reads_the_index_not_the_working_copy(self):
        self._set_rule(600)
        self._git("add", self.RULE)
        self._set_rule(500)
        self.assertEqual(self._kinds(staged=True), ["scoped-rule-size"])
        self.assertEqual(self._kinds(), ["size-warn"])


class CommitViewTests(_GitRepo):
    """Every gating check judges the tree the commit records, so another session's unstaged edit in
    the same checkout cannot refuse this commit, and a staged breach cannot pass behind a clean
    working copy."""

    CONFIG = "Main/_Module/ModuleData/thing.json"
    DOC = "docs/features/thing.md"

    def setUp(self):
        super().setUp()
        _write(self.root / "CLAUDE.md", ContextBudgetTests.IMPORTS)
        _write(self.root / "AGENTS.md", "# a\n\nTarget: Bannerlord v1.5.6\n")
        _write(self.root / "docs" / "ai-includes" / "orientation.md", ContextBudgetTests.TRAP_HEAD)
        _write(self.root / ".claude" / "pinned-game-version.txt", "v1.5.6\n")
        _write(self.root / ".claude" / "rules" / "plain.md", "# plain\n")
        _write(self.root / self.CONFIG, '{"Speed": 1}\n')
        _write(self.root / self.DOC, f"`{self.CONFIG}`:\n\n```json\n{{\"Speed\": 1}}\n```\n")
        _write(self.root / "docs" / "reference" / "a.md", "one\n")
        self._commit()
        self.size = ld._loaded_bytes((self.root / "AGENTS.md").read_text(encoding="utf-8"))
        total = sum(ld._loaded_bytes(p.read_text(encoding="utf-8")) for p in ld.entry_docs()[0])
        cap = mock.patch.object(ld, "ENTRY_DOCS_MAX_BYTES", total + 50)
        warn = mock.patch.object(ld, "ENTRY_DOCS_WARN_BYTES", total + 50)
        rules = mock.patch.object(ld, "UNSCOPED_RULES_MAX_BYTES", 100)
        rules_warn = mock.patch.object(ld, "UNSCOPED_RULES_WARN_BYTES", 100)
        docs_dir = mock.patch.object(ld, "DOCS_DIR", self.root / "docs")
        for patcher in (cap, warn, rules, rules_warn, docs_dir):
            patcher.start()
            self.addCleanup(patcher.stop)

    def _budget(self, staged=True, only_paths=None):
        return [f[2] for f in ld.check_context_budget(ld.commit_view(staged, only_paths, "HEAD"))]

    def _grow_agents(self):
        _write(self.root / "AGENTS.md", "# a\n\nTarget: Bannerlord v1.5.6\n" + "x" * 200 + "\n")

    def _trim_agents(self):
        _write(self.root / "AGENTS.md", "# a\n\nTarget: Bannerlord v1.5.6\n")

    def test_a_staged_entry_doc_over_budget_with_a_trimmed_working_copy_gates(self):
        self._grow_agents()
        self._git("add", "AGENTS.md")
        self._trim_agents()
        self.assertEqual(self._budget(), ["size"])

    def test_another_sessions_unstaged_entry_doc_growth_does_not_refuse_this_commit(self):
        self._grow_agents()
        _write(self.root / "docs" / "reference" / "a.md", "two\n")
        self._git("add", "docs/reference/a.md")
        self.assertEqual(self._budget(), [])
        self.assertEqual(self._budget(staged=False), ["size"])

    def test_a_path_commit_reads_the_named_copy_and_head_for_the_rest(self):
        self._grow_agents()
        self.assertEqual(self._budget(only_paths=["AGENTS.md"]), ["size"])
        self.assertEqual(self._budget(only_paths=[ "docs/reference/a.md"]), [])

    def test_a_path_commit_ignores_what_is_staged_outside_it(self):
        self._grow_agents()
        self._git("add", "AGENTS.md")
        self._trim_agents()
        self.assertEqual(self._budget(only_paths=["docs/reference/a.md"]), [])

    def test_unstaged_unscoped_rule_growth_does_not_refuse_this_commit(self):
        _write(self.root / ".claude" / "rules" / "plain.md", "x" * 200 + "\n")
        self.assertEqual(self._budget(), [])
        self._git("add", ".claude/rules/plain.md")
        self.assertEqual(self._budget(), ["rules-size"])

    def test_an_untracked_rule_is_no_part_of_the_commit(self):
        _write(self.root / ".claude" / "rules" / "another.md", "x" * 200 + "\n")
        self.assertEqual(self._budget(), [])

    def test_a_rule_staged_for_deletion_is_not_counted(self):
        _write(self.root / ".claude" / "rules" / "plain.md", "x" * 200 + "\n")
        self._commit()
        self._git("rm", "-q", "--cached", ".claude/rules/plain.md")
        self.assertEqual(self._budget(), [])

    def test_an_import_the_index_lacks_is_missing(self):
        self._git("rm", "-q", "--cached", "docs/ai-includes/orientation.md")
        self.assertIn("import-missing", self._budget())

    def _drift(self, staged=True, only_paths=None):
        files = [self.root / self.DOC]
        return ld.check_config_example_drift(files, ld.commit_view(staged, only_paths, "HEAD"))

    def test_unstaged_config_drift_does_not_refuse_this_commit(self):
        _write(self.root / self.CONFIG, '{"Speed": 2}\n')
        self.assertEqual(self._drift(), [])
        self.assertEqual(len(self._drift(staged=False)), 1)

    def test_a_staged_config_drift_gates_behind_a_clean_working_copy(self):
        _write(self.root / self.CONFIG, '{"Speed": 2}\n')
        self._git("add", self.CONFIG)
        _write(self.root / self.CONFIG, '{"Speed": 1}\n')
        self.assertEqual(len(self._drift()), 1)

    def test_the_version_pin_is_read_from_the_index(self):
        _write(self.root / ".claude" / "pinned-game-version.txt", "v9.9.9\n")
        view = ld.commit_view(True, None, "HEAD")
        self.assertEqual(ld.check_version_consistency(view), [])
        self.assertEqual(len(ld.check_version_consistency(None)), 1)
        self._git("add", ".claude/pinned-game-version.txt")
        self.assertEqual(len(ld.check_version_consistency(ld.commit_view(True, None, "HEAD"))), 1)

    def test_a_crlf_working_copy_and_its_lf_index_copy_measure_the_same(self):
        path = self.root / "AGENTS.md"
        path.write_bytes(path.read_bytes().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n"))
        view = ld.commit_view(True, None, "HEAD")
        self.assertEqual(ld._loaded_bytes(view.read(path)), self.size)

    def test_a_tree_or_a_missing_path_reads_as_no_file(self):
        view = ld.commit_view(True, None, "HEAD")
        self.assertIsNone(view.read(self.root / "docs"))
        self.assertIsNone(view.read(self.root / "nope.md"))
        self.assertEqual(view.read(self.root / "docs" / "reference" / "a.md").strip(), "one")

    def test_a_path_with_a_space_reads(self):
        _write(self.root / "docs" / "two words.md", "spaced\n")
        self._commit()
        self.assertEqual(ld.commit_view(True, None, "HEAD").read(self.root / "docs" / "two words.md").strip(), "spaced")

    def test_outside_a_git_tree_there_is_no_commit_in_view(self):
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(ld, "REPO_ROOT", Path(tmp)):
            self.assertIsNone(ld.commit_view(True, None, "HEAD"))

    def test_no_staged_or_path_argument_means_no_commit_in_view(self):
        self.assertIsNone(ld.commit_view(False, None, "HEAD"))


class CrashExitTests(unittest.TestCase):
    def test_a_crash_exits_3_because_exit_1_is_a_finding(self):
        err = io.StringIO()
        with mock.patch.object(ld, "main", side_effect=RuntimeError("boom")), mock.patch("sys.stderr", err):
            self.assertEqual(ld.entry([]), 3)
        self.assertIn("boom", err.getvalue())

    def test_an_ordinary_exit_passes_through(self):
        with mock.patch.object(ld, "main", return_value=1):
            self.assertEqual(ld.entry([]), 1)


class AiDashGitScopeTests(_GitRepo):
    """check_ai_dashes reports NEW writing only.

    40,476 em dashes already live in the tree (CHANGELOG 1,604 / docs 37,448 /
    .claude 1,424, counted 2026-08-11). A whole-tree check would drown the report
    and the rule would be ignored, so the scope is added lines plus untracked files.
    """

    def test_committed_dashes_are_invisible_and_only_the_new_line_is_reported(self):
        doc = self.root / "docs" / "old.md"
        _write(doc, f"Legacy prose {EM} with a dash.\nAnother {EM} one.\n")
        self._commit()
        self.assertEqual(ld.check_ai_dashes(), [], "a clean tree must report nothing")

        _write(doc, f"Legacy prose {EM} with a dash.\nAnother {EM} one.\nFresh {EM} line.\n")
        found = ld.check_ai_dashes()
        self.assertEqual([(f.name, n) for f, n, _k, _t in found], [("old.md", 3)])

    def test_an_untracked_markdown_file_is_scanned_in_full(self):
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        _write(self.root / "docs" / "new.md", f"Brand new {EM} file.\n")
        found = ld.check_ai_dashes()
        self.assertEqual([(f.name, n) for f, n, _k, _t in found], [("new.md", 1)])

    def test_a_staged_addition_is_reported(self):
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        _write(self.root / "docs" / "seed.md", f"# seed\n\nStaged {EM} prose.\n")
        self._git("add", "-A")
        found = ld.check_ai_dashes()
        self.assertEqual([(f.name, n) for f, n, _k, _t in found], [("seed.md", 3)])

    def test_a_non_markdown_file_is_ignored(self):
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        _write(self.root / "notes.txt", f"Plain text {EM} file.\n")
        self.assertEqual(ld.check_ai_dashes(), [])

    def test_an_added_line_inside_a_fenced_block_is_not_reported(self):
        doc = self.root / "docs" / "old.md"
        _write(doc, "Intro.\n\n```\ncode\n```\n")
        self._commit()
        _write(doc, f"Intro.\n\n```\ncode\nmore {EM} code\n```\n")
        self.assertEqual(ld.check_ai_dashes(), [])

    def test_a_base_ref_widens_the_scan_to_a_whole_branch(self):
        doc = self.root / "docs" / "old.md"
        _write(doc, "# base\n")
        self._commit("base")
        base = subprocess.run(
            ["git", "-C", str(self.root), "rev-parse", "HEAD"],
            capture_output=True, text=True, check=True,
        ).stdout.strip()
        _write(doc, f"# base\n\nCommitted {EM} later.\n")
        self._commit("later")
        self.assertEqual(ld.check_ai_dashes(), [], "HEAD base sees nothing after the commit")
        self.assertEqual(len(ld.check_ai_dashes(base)), 1, "the branch base still sees it")

    def test_a_repo_with_no_commits_fails_open(self):
        _write(self.root / "docs" / "a.md", "# a\n")
        self.assertIsInstance(ld.check_ai_dashes(), list)


class RcaFollowUpScannerTests(unittest.TestCase):
    """scan_text_for_followups: an uppercase FOLLOW-UP needs an issue or a reason.

    One case per shape of a line. The matching is on the text with inline code spans and link
    targets removed, so a passing mention in backticks is exempt and an anchor never counts
    as an issue.
    """

    P = Path("docs/reviews/rca-sample-2026-10-23.md")

    def _flagged(self, text, only_lines=None):
        return [n for _p, n, _k, _t in ld.scan_text_for_followups(self.P, text, only_lines)]

    def _bare(self, line):
        self.assertEqual(self._flagged(line + "\n"), [1], line)

    def _owned(self, line):
        self.assertEqual(self._flagged(line + "\n"), [], line)

    def test_a_cited_issue_passes(self):
        self._owned("MS7-4 FOLLOW-UP #983 tracked.")

    def test_an_issue_url_passes(self):
        self._owned("FOLLOW-UP https://github.com/o/r/issues/983 tracks it.")

    def test_a_markdown_link_to_an_issue_passes(self):
        self._owned("FOLLOW-UP [tracked](https://github.com/o/r/issues/983).")

    def test_a_stated_reason_passes(self):
        self._owned("FOLLOW-UP, no issue: a test-only refactor.")

    def test_the_house_phrasings_pass(self):
        self._owned("FOLLOW-UP (pre-existing code; no issue filed, because filing is public).")
        self._owned("FOLLOW-UP (pre-existing code, no issues filed, since it needs a word).")
        self._owned("FOLLOW-UP no issue filed: the code is not ours.")
        self._owned("FOLLOW-UP pre-existing; none is filed because it is public.")

    def test_a_reason_may_follow_a_few_words_after_the_phrase(self):
        """The hook's deny text says to state why none is filed; these are how that reads."""
        self._owned("FOLLOW-UP no issue filed here because filing is public.")
        self._owned("FOLLOW-UP No issue filed (public action).")
        self._owned("FOLLOW-UP no issue filed; Mike files issues.")
        self._owned("FOLLOW-UP no issue filed from this review lead, since filing is public.")
        self._owned("FOLLOW-UP no issue filed by me, standing rule 11.")

    def test_a_sentence_end_after_no_issue_is_still_bare(self):
        self._bare("FOLLOW-UP, no issue.")
        self._bare("FOLLOW-UP, with no issue.")
        self._bare("FOLLOW-UP, no issue filed.")
        self._bare("FOLLOW-UP, no issue filed (")
        self._bare("FOLLOW-UP no issue there. Later, we revisit it.")

    def test_a_reason_marker_far_from_the_phrase_is_not_its_reason(self):
        self._bare("FOLLOW-UP no issue at all has been raised for this one yet, sadly")

    def test_a_bare_follow_up_is_flagged(self):
        self._bare("FOLLOW-UP later.")

    def test_no_issue_with_nothing_after_it_is_flagged(self):
        self._bare("FOLLOW-UP, no issue:")
        self._bare("FOLLOW-UP, no issue filed")
        self._bare("FOLLOW-UP, no issue")

    def test_an_empty_reason_in_a_table_cell_is_flagged(self):
        self._bare("| 11 | FOLLOW-UP, no issue: | later |")

    def test_lowercase_follow_up_is_ignored(self):
        self._owned("A follow-up would help.")

    def test_the_plural_is_a_follow_up_too(self):
        self._bare("FOLLOW-UPS: the two below.")

    def test_a_backticked_mention_is_exempt(self):
        self._owned("Each deferral is a `FOLLOW-UP` line in the RCA.")
        self._owned("Run ``FOLLOW-UP`` through the linter.")

    def test_an_issue_inside_a_code_span_is_an_example_not_a_citation(self):
        self._bare("FOLLOW-UP write it as `#983` once filed.")

    def test_a_follow_up_outside_the_code_span_is_still_checked(self):
        self._bare("The `rca` template says FOLLOW-UP and nothing else.")

    def test_a_link_anchor_is_not_an_issue(self):
        self._bare("FOLLOW-UP see [the notes](#3-details).")

    def test_a_hex_colour_is_not_an_issue(self):
        self._bare("FOLLOW-UP recolour the bar to #123456.")

    def test_a_numbered_label_is_not_an_issue(self):
        self._bare("FOLLOW-UP finding #2 is deferred.")
        self._bare("FOLLOW-UP the Crash #3 path.")
        self._bare("FOLLOW-UP Step #4, row #5, line #6, item #7, case #8.")
        self._owned("FOLLOW-UP finding #2 is tracked in #983.")

    def test_a_language_name_or_entity_is_not_an_issue(self):
        self._bare("FOLLOW-UP port it to C#7.")
        self._bare("FOLLOW-UP the quote &#39;x&#39;.")

    def test_a_table_row_with_the_issue_in_another_cell_passes(self):
        self._owned("| 11 | HIGH | bug | FOLLOW-UP | #980 |")

    def test_a_table_row_without_any_issue_is_flagged(self):
        self._bare("| 11 | HIGH | bug | FOLLOW-UP | none |")

    def test_a_follow_up_inside_a_fenced_block_is_ignored(self):
        self.assertEqual(self._flagged("```\nFOLLOW-UP bare\n```\n"), [])
        self.assertEqual(self._flagged("~~~\nFOLLOW-UP bare\n~~~\n"), [])

    def test_an_indented_fence_in_a_list_item_is_ignored(self):
        self.assertEqual(self._flagged("- item\n\n  ```\n  FOLLOW-UP bare\n  ```\n"), [])

    def test_prose_after_a_closed_fence_is_checked(self):
        self.assertEqual(self._flagged("```\ncode\n```\nFOLLOW-UP bare\n"), [4])

    def test_only_lines_restricts_the_scan(self):
        text = "FOLLOW-UP old bare.\nFOLLOW-UP new bare.\n"
        self.assertEqual(self._flagged(text, {2}), [2])

    def test_the_finding_names_the_kind_and_the_line(self):
        found = ld.scan_text_for_followups(self.P, "ok\nFOLLOW-UP later.\n")
        self.assertEqual([(self.P, 2, "followup-no-owner", "FOLLOW-UP later.")], found)


class RcaFollowUpGateTests(_GitRepo):
    """check_rca_followups: only the new lines of a docs/reviews/rca-*.md, found through git.

    Working-tree mode reads the file on disk (git diff -U0 plus untracked); staged mode reads
    the index, which is what a commit would record.
    """

    def setUp(self):
        super().setUp()
        self.rca = self.root / "docs" / "reviews" / "rca-sample-2026-10-23.md"

    def _found(self, body, path=None):
        _write(path or self.rca, "# rca\n" + body)
        return [(f.name, n) for f, n, _k, _t in ld.check_rca_followups()]

    def _staged(self):
        return [(f.name, n) for f, n, _k, _t in ld.check_rca_followups(staged=True)]

    def test_a_bare_follow_up_is_flagged_with_file_and_line(self):
        found = self._found("ok line\nFOLLOW-UP later.\n")
        self.assertEqual(found, [(self.rca.name, 3)])

    def test_a_non_rca_doc_is_ignored(self):
        other = self.root / "docs" / "reviews" / "notes.md"
        self.assertEqual(self._found("FOLLOW-UP bare.\n", other), [])

    def test_an_old_unchanged_line_is_ignored_and_only_the_new_one_flagged(self):
        _write(self.rca, "# rca\nFOLLOW-UP old bare.\n")
        self._commit()
        self.assertEqual(ld.check_rca_followups(), [], "committed lines are invisible")
        _write(self.rca, "# rca\nFOLLOW-UP old bare.\nFOLLOW-UP new bare.\n")
        found = ld.check_rca_followups()
        self.assertEqual([(f.name, n) for f, n, _k, _t in found], [(self.rca.name, 3)])

    def test_an_edited_legacy_line_is_new_and_must_be_fixed(self):
        _write(self.rca, "# rca\nFOLLOW-UP old bare.\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP old bare, reworded.\n")
        self.assertEqual([(self.rca.name, 2)], [(f.name, n) for f, n, _k, _t in ld.check_rca_followups()])

    def test_a_staged_bare_line_is_reported_even_when_the_working_copy_is_fixed(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP bare.\nFOLLOW-UP new bare.\n")
        self._git("add", "-A")
        _write(self.rca, "# rca\nFOLLOW-UP bare.\nFOLLOW-UP new, tracked in #983.\n")
        self.assertEqual(ld.check_rca_followups(), [], "the working copy is clean")
        self.assertEqual(self._staged(), [(self.rca.name, 3)], "the index is not")

    def test_the_staged_diff_is_the_index_against_the_base_not_the_working_tree(self):
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\nFOLLOW-UP staged bare.\n")
        self._git("add", "-A")
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\n")  # the working copy is back at HEAD
        self.assertEqual(ld.check_rca_followups(), [])
        self.assertEqual(self._staged(), [(self.rca.name, 3)])

    def test_staged_mode_lists_no_untracked_file(self):
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP untracked bare.\n")
        self.assertIn(self.rca, ld._changed_markdown_lines("HEAD", pathspec="docs/reviews/rca-*.md"))
        self.assertEqual({}, ld._changed_markdown_lines("HEAD", staged=True, pathspec="docs/reviews/rca-*.md"))

    def test_an_unstaged_bare_line_is_not_reported_in_staged_mode(self):
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\nFOLLOW-UP unstaged bare.\n")
        self.assertEqual(self._staged(), [])
        self.assertEqual(len(ld.check_rca_followups()), 1, "working-tree mode still sees it")

    def test_an_untracked_rca_is_not_reported_in_staged_mode(self):
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP untracked bare.\n")
        self.assertEqual(self._staged(), [])
        self.assertEqual(len(ld.check_rca_followups()), 1)

    def test_a_new_staged_rca_is_scanned_in_full(self):
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP staged bare.\n")
        self._git("add", "-A")
        self.assertEqual(self._staged(), [(self.rca.name, 2)])

    def test_a_file_renamed_into_an_rca_is_scanned_in_full(self):
        notes = self.root / "docs" / "reviews" / "notes.md"
        _write(notes, "# notes\nFOLLOW-UP old bare.\n")
        self._commit()
        self._git("mv", "docs/reviews/notes.md", "docs/reviews/rca-sample-2026-10-23.md")
        self.assertEqual(self._staged(), [(self.rca.name, 2)], "flagged in full: nothing is old here")

    def test_a_staged_non_rca_doc_is_ignored(self):
        _write(self.root / "docs" / "reviews" / "notes.md", "# notes\nFOLLOW-UP bare.\n")
        self._git("add", "-A")
        self.assertEqual(self._staged(), [])

    def test_a_staged_deletion_is_not_an_error(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        self._commit()
        self._git("rm", "-q", "docs/reviews/rca-sample-2026-10-23.md")
        self.assertEqual(self._staged(), [])

    def test_a_flagged_line_fails_the_drift_gate_and_is_reported(self):
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\n")
        self.assertEqual(self._gate("--fail-on-drift")[0], 0)
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        rc, out = self._gate("--fail-on-drift")
        self.assertEqual(rc, 1)
        self.assertIn("rca-sample-2026-10-23.md:2", out)
        self.assertIn("RCA FOLLOW-UP lines with no issue or reason (new lines only): **1**", out)

    def test_the_report_section_header_is_pinned(self):
        """check-doc-config-drift.sh quotes every `## ` section of --drift-only; this header is
        the one a blocked commit shows for a bare FOLLOW-UP, so it must not drift."""
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        out = self._gate("--drift-only")[1]
        self.assertIn("\n## RCA FOLLOW-UP lines with no issue or reason\n", out)

    def test_drift_only_gates_on_it_too(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        self.assertEqual(self._gate("--drift-only")[0], 1)

    def test_drift_only_staged_gates_on_the_index_not_the_working_copy(self):
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP ok #1.\nFOLLOW-UP bare.\n")
        self.assertEqual(self._gate("--drift-only", "--staged")[0], 0, "nothing staged")
        self._git("add", "-A")
        rc, out = self._gate("--drift-only", "--staged")
        self.assertEqual(rc, 1)
        self.assertIn("rca-sample-2026-10-23.md:3", out)


class FeatureDocVerifySectionTests(_GitRepo):
    """check_feature_doc_verify_section: a NEW docs/features/*.md needs `## How to verify in game`."""

    HEADING = "## How to verify in game"

    def setUp(self):
        super().setUp()
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        self.feat = self.root / "docs" / "features" / "sample-thing.md"

    def _names(self, staged=False):
        return [f.name for f, _n, _k, _t in ld.check_feature_doc_verify_section(staged=staged)]

    def test_an_added_doc_without_the_heading_is_flagged(self):
        _write(self.feat, "# thing\n\nText.\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [self.feat.name])
        self.assertEqual(self._names(), [self.feat.name], "working-tree mode sees it too")

    def test_the_finding_message_names_the_heading_and_the_template(self):
        _write(self.feat, "# thing\n")
        self._git("add", "-A")
        msg = ld.check_feature_doc_verify_section(staged=True)[0][3]
        self.assertIn("## How to verify in game", msg)
        self.assertIn("TEMPLATE.md", msg)
        self.assertIn("Not applicable:", msg)

    def test_an_untracked_doc_is_flagged_in_working_tree_mode_only(self):
        _write(self.feat, "# thing\n")
        self.assertEqual(self._names(), [self.feat.name])
        self.assertEqual(self._names(staged=True), [])

    def test_an_added_doc_with_the_heading_passes(self):
        _write(self.feat, f"# thing\n\n{self.HEADING}\n\n1. Start a campaign.\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [])

    def test_not_applicable_under_the_heading_passes(self):
        _write(self.feat, f"# thing\n\n{self.HEADING}\n\nNot applicable: pure tooling.\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [])

    def test_staged_mode_reads_the_index_not_the_working_copy(self):
        _write(self.feat, "# thing\n")
        self._git("add", "-A")
        _write(self.feat, f"# thing\n{self.HEADING}\n")
        self.assertEqual(self._names(staged=True), [self.feat.name])

    def test_a_modified_existing_doc_without_the_heading_is_not_checked(self):
        _write(self.feat, "# thing\n")
        self._commit()
        _write(self.feat, "# thing\n\nmore text\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [])
        self.assertEqual(self._names(), [])

    def test_template_and_readme_are_excluded(self):
        _write(self.root / "docs" / "features" / "TEMPLATE.md", "# template\n")
        _write(self.root / "docs" / "features" / "README.md", "# readme\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [])
        self.assertEqual(self._names(), [])

    def test_a_doc_outside_docs_features_is_ignored(self):
        _write(self.root / "docs" / "reference" / "thing.md", "# thing\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [])

    def _also(self, *paths, staged=True):
        return [f.name for f, _n, _k, _t in
                ld.check_feature_doc_verify_section(staged=staged, also_new=list(paths))]

    REL = "docs/features/sample-thing.md"

    def test_also_new_flags_an_untracked_doc_without_the_heading(self):
        _write(self.feat, "# thing\n")
        self.assertEqual(self._also(self.REL), [self.feat.name])

    def test_also_new_passes_an_untracked_doc_with_the_heading(self):
        _write(self.feat, f"# thing\n\n{self.HEADING}\n\nNot applicable: tooling.\n")
        self.assertEqual(self._also(self.REL), [])

    def test_also_new_ignores_a_doc_that_exists_in_head(self):
        _write(self.feat, "# thing\n")
        self._commit()
        _write(self.feat, "# thing\n\nedited\n")
        self.assertEqual(self._also(self.REL), [])

    def test_also_new_ignores_a_missing_file_a_template_and_a_subdirectory(self):
        _write(self.root / "docs" / "features" / "TEMPLATE.md", "# t\n")
        _write(self.root / "docs" / "features" / "sub" / "x.md", "# x\n")
        self.assertEqual(self._also(self.REL, "docs/features/TEMPLATE.md",
                                    "docs/features/sub/x.md", "docs/reference/y.md"), [])

    def test_also_new_is_deduplicated_with_the_staged_set(self):
        _write(self.feat, "# thing\n")
        self._git("add", "-A")
        self.assertEqual(self._also(self.REL, self.REL), [self.feat.name])

    def test_cli_also_new_option_feeds_the_check(self):
        _write(self.feat, "# thing\n")
        rc, out = self._gate("--drift-only", "--staged", "--also-new", self.REL)
        self.assertEqual(rc, 1)
        self.assertIn("sample-thing.md", out)

    def test_a_heading_inside_a_code_fence_does_not_count(self):
        _write(self.feat, f"# thing\n\n```\n{self.HEADING}\nsteps\n```\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [self.feat.name])

    def test_an_empty_section_is_flagged(self):
        _write(self.feat, f"# thing\n\n{self.HEADING}\n\n\n## Next\n\ntext\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [self.feat.name])

    def test_an_empty_section_at_end_of_file_is_flagged(self):
        _write(self.feat, f"# thing\n\n{self.HEADING}\n\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [self.feat.name])

    def test_a_section_with_a_fenced_block_only_passes(self):
        _write(self.feat, f"# thing\n\n{self.HEADING}\n\n```\n## inside\nrun it\n```\n\n## Next\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [])

    def _merge_side_branch_adding(self, side_doc_text):
        """Commit a doc on a side branch, then start a no-commit merge of it into the first branch."""
        self._git("checkout", "-q", "-b", "side")
        _write(self.feat, side_doc_text)
        self._commit("side adds a doc")
        self._git("checkout", "-q", "-")
        _write(self.root / "docs" / "other.md", "# other\n")
        self._commit("main moves on")
        self._git("merge", "-q", "--no-ff", "--no-commit", "side")

    def test_a_doc_the_merged_branch_already_has_is_not_new(self):
        self._merge_side_branch_adding("# thing\n\nNo verify section.\n")
        self.assertEqual(self._names(staged=True), [])
        self.assertEqual(self._names(), [])

    def test_a_doc_added_in_the_merge_itself_is_still_new(self):
        self._merge_side_branch_adding(f"# thing\n\n{self.HEADING}\n\nNot applicable: tooling.\n")
        fresh = self.root / "docs" / "features" / "fresh-thing.md"
        _write(fresh, "# fresh\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [fresh.name])

    def test_a_doc_in_a_features_subdirectory_is_not_checked(self):
        _write(self.root / "docs" / "features" / "elephant" / "x.md", "# x\n")
        self._git("add", "-A")
        self.assertEqual(self._names(staged=True), [])
        self.assertEqual(self._names(), [])

    def test_the_drift_gate_fails_and_reports_a_new_doc_without_the_section(self):
        _write(self.feat, "# thing\n")
        self._git("add", "-A")
        rc, out = self._gate("--drift-only", "--staged")
        self.assertEqual(rc, 1)
        self.assertIn("sample-thing.md", out)
        self.assertIn("## Feature docs with no in-game verification section", out)
        self.assertEqual(self._gate("--fail-on-drift", "--staged")[0], 1)

    def test_the_drift_gate_passes_once_the_section_exists(self):
        _write(self.feat, f"# thing\n{self.HEADING}\nNot applicable: tooling.\n")
        self._git("add", "-A")
        self.assertEqual(self._gate("--drift-only", "--staged")[0], 0)


class SharedFenceWalkerTests(unittest.TestCase):
    """#987 item 5: the dash scanner and the FOLLOW-UP scanner walk fences the same way, and a
    closing fence needs a run at least as long as its opener (nested fences)."""

    P = Path("docs/reviews/rca-sample-2026-10-23.md")

    def _dashes(self, text):
        return [n for _p, n, _k, _t in ld.scan_text_for_dashes(Path("docs/features/a.md"), text)]

    def _followups(self, text):
        return [n for _p, n, _k, _t in ld.scan_text_for_followups(self.P, text)]

    def test_a_dash_inside_a_fence_indented_four_spaces_in_a_list_is_not_reported(self):
        self.assertEqual(self._dashes(f"- item\n\n    ```\n    code {EM} sample\n    ```\n"), [])

    def test_a_four_backtick_block_holding_a_three_backtick_line_stays_open_for_dashes(self):
        self.assertEqual(self._dashes(f"````\n```\ncode {EM} sample\n````\n"), [])

    def test_a_four_backtick_block_holding_a_three_backtick_line_stays_open_for_follow_ups(self):
        self.assertEqual(self._followups("````\n```\nFOLLOW-UP bare\n````\n"), [])

    def test_prose_after_the_long_closer_is_checked_again(self):
        self.assertEqual(self._followups("````\n```\n````\nFOLLOW-UP bare\n"), [4])
        self.assertEqual(self._dashes(f"````\n```\n````\nProse {EM} here\n"), [4])

    def test_a_longer_closer_closes_a_shorter_opener(self):
        self.assertEqual(self._dashes(f"```\ncode\n`````\nProse {EM} here\n"), [4])


class GitDiffFailureTests(_GitRepo):
    """#987 item 6: an unresolvable --dash-base exits 2 before any check runs. A diff that fails on
    a resolvable base (repository damage) is a git-diff-failed finding that gates, never zero
    findings. A git that cannot run at all keeps failing open, with a note."""

    def setUp(self):
        super().setUp()
        _write(self.root / "docs" / "seed.md", "# seed\n")
        self._commit()
        self.rca = self.root / "docs" / "reviews" / "rca-sample-2026-10-23.md"

    def _kinds(self, findings):
        return [k for _p, _n, k, _t in findings]

    def test_the_dash_check_reports_an_unresolvable_base(self):
        failures = []
        self.assertEqual([], ld.check_ai_dashes("no-such-ref", failures=failures))
        self.assertEqual(["git-diff-failed"], self._kinds(failures))

    def test_the_follow_up_check_reports_an_unresolvable_base(self):
        failures = []
        self.assertEqual([], ld.check_rca_followups("no-such-ref", failures=failures))
        self.assertEqual(["git-diff-failed"], self._kinds(failures))
        self.assertIn("no-such-ref", failures[0][3])

    def test_the_staged_check_reports_an_unresolvable_base(self):
        failures = []
        ld.check_rca_followups("no-such-ref", staged=True, failures=failures)
        self.assertEqual(["git-diff-failed"], self._kinds(failures))

    def test_two_checks_sharing_one_list_report_the_failure_once(self):
        failures = []
        ld.check_ai_dashes("no-such-ref", failures=failures)
        ld.check_rca_followups("no-such-ref", failures=failures)
        self.assertEqual(["git-diff-failed"], self._kinds(failures))

    def test_a_resolvable_base_reports_nothing(self):
        failures = []
        self.assertEqual([], ld.check_rca_followups("HEAD", failures=failures))
        self.assertEqual([], ld.check_ai_dashes("HEAD", failures=failures))
        self.assertEqual([], failures)

    def _gate_err(self, *argv):
        with mock.patch("sys.stderr", new_callable=io.StringIO) as err:
            rc, out = self._gate(*argv)
        return rc, out, err.getvalue()

    def test_drift_only_exits_2_on_an_unresolvable_base(self):
        rc, out, err = self._gate_err("--drift-only", "--dash-base", "no-such-ref")
        self.assertEqual(2, rc, out)
        self.assertEqual("", out)
        self.assertIn("lint_docs: --dash-base no-such-ref does not resolve", err)

    def test_fail_on_drift_exits_2_on_an_unresolvable_base(self):
        rc, out, err = self._gate_err("--fail-on-drift", "--dash-base", "no-such-ref")
        self.assertEqual(2, rc, out)
        self.assertIn("does not resolve", err)

    def test_the_full_report_exits_2_on_an_unresolvable_base(self):
        self.assertEqual(2, self._gate_err("--dash-base", "no-such-ref")[0])

    def test_drift_only_staged_exits_2_on_an_unresolvable_base(self):
        self.assertEqual(2, self._gate_err("--drift-only", "--staged", "--dash-base", "no-such-ref")[0])

    def test_a_tree_id_is_a_valid_base(self):
        rc, out, err = self._gate_err("--drift-only", "--staged", "--dash-base", ld._EMPTY_TREE)
        self.assertEqual(0, rc, out + err)

    def test_the_empty_tree_base_counts_every_staged_file_as_added(self):
        _write(self.root / "docs" / "features" / "x.md", "# x\n\nNo verify heading.\n")
        self._commit("x")
        rc, out, err = self._gate_err("--drift-only", "--staged", "--dash-base", "HEAD")
        self.assertEqual(0, rc, out + err)
        rc, out, _err = self._gate_err("--drift-only", "--staged", "--dash-base", ld._EMPTY_TREE)
        self.assertEqual(1, rc, out)
        self.assertIn("no-verify-section", out)

    def test_an_unborn_head_is_not_an_unresolvable_base(self):
        with tempfile.TemporaryDirectory() as fresh:
            ld.REPO_ROOT = Path(fresh)
            subprocess.run(["git", "-C", fresh, "init", "-q"], check=True, capture_output=True)
            with mock.patch.object(ld, "DOC_ROOTS", [Path(fresh) / "docs"]), \
                    mock.patch.object(ld, "check_context_budget", return_value=[]), \
                    mock.patch.object(ld, "check_version_consistency", return_value=[]), \
                    mock.patch("sys.stdout", new_callable=io.StringIO):
                self.assertEqual(0, ld.main(["--drift-only"]))

    def _break_the_index(self):
        (self.root / ".git" / "index").write_bytes(b"DIRCgarbage")

    def test_a_failed_diff_has_its_own_section_not_the_follow_up_one(self):
        self._break_the_index()
        rc, out = self._gate("--drift-only", "--dash-base", "HEAD")
        self.assertEqual(1, rc, out)
        self.assertIn("## Git diff failed", out)
        self.assertNotIn("## RCA FOLLOW-UP lines", out)
        self.assertIn("RCA FOLLOW-UP lines with no issue or reason (new lines only): **0**", out)
        self.assertEqual(1, out.count("[git-diff-failed]"), out)

    def test_a_failed_diff_has_its_own_summary_counter(self):
        self._break_the_index()
        rc, out = self._gate("--drift-only", "--summary", "--dash-base", "HEAD")
        self.assertEqual(1, rc, out)
        self.assertIn("git_diff_failed:   1", out)
        self.assertIn("rca_followups:     0", out)
        self.assertIn("total_findings:    1", out)

    def test_a_good_base_still_exits_0(self):
        self.assertEqual(0, self._gate("--drift-only", "--dash-base", "HEAD")[0])

    def test_a_missing_git_fails_open_with_a_note(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        with mock.patch.object(ld.subprocess, "run", side_effect=FileNotFoundError("git")), \
                mock.patch("sys.stderr", new_callable=io.StringIO) as err:
            failures = []
            self.assertEqual([], ld.check_rca_followups(failures=failures))
            self.assertEqual([], ld.check_ai_dashes(failures=failures))
        self.assertEqual([], failures)
        self.assertIn("git", err.getvalue())
        self.assertIn("read nothing", err.getvalue())

    def test_a_directory_that_is_no_git_work_tree_fails_open_with_a_note(self):
        with tempfile.TemporaryDirectory() as plain:
            ld.REPO_ROOT = Path(plain)
            with mock.patch("sys.stderr", new_callable=io.StringIO) as err:
                self.assertEqual([], ld.check_rca_followups("HEAD"))
        self.assertIn("not a git work tree", err.getvalue())

    def test_a_repo_with_no_commits_is_not_a_failed_diff(self):
        with tempfile.TemporaryDirectory() as fresh:
            ld.REPO_ROOT = Path(fresh)
            subprocess.run(["git", "-C", fresh, "init", "-q"], check=True, capture_output=True)
            _write(Path(fresh) / "docs" / "reviews" / "rca-x-2026-10-23.md", "# rca\nFOLLOW-UP bare.\n")
            subprocess.run(["git", "-C", fresh, "add", "-A"], check=True, capture_output=True)
            found = ld.check_rca_followups("HEAD", staged=True)
        self.assertEqual(["followup-no-owner"], self._kinds(found))


class CommitOnlyPathsTests(_GitRepo):
    """#987 item 7, linter side: `git commit -- <paths>` records the named paths' working copies,
    so --only-paths reads those from the working tree whatever --staged says."""

    def setUp(self):
        super().setUp()
        self.rca = self.root / "docs" / "reviews" / "rca-sample-2026-10-23.md"
        self.other = self.root / "docs" / "reviews" / "rca-other-2026-10-23.md"
        _write(self.rca, "# rca\n")
        _write(self.other, "# rca\n")
        self._commit()

    def test_an_unstaged_edit_of_a_named_path_is_judged(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        self.assertEqual(0, self._gate("--drift-only", "--staged")[0], "the index holds nothing new")
        rc, out = self._gate("--drift-only", "--staged", "--only-paths", "docs/reviews/rca-sample-2026-10-23.md")
        self.assertEqual(1, rc, out)

    def test_a_bad_line_in_an_unnamed_path_is_not_judged(self):
        _write(self.other, "# rca\nFOLLOW-UP bare.\n")
        rc, out = self._gate("--drift-only", "--staged", "--only-paths", "docs/reviews/rca-sample-2026-10-23.md")
        self.assertEqual(0, rc, out)

    def test_a_staged_bad_line_that_the_working_copy_fixed_is_not_judged(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        self._git("add", "-A")
        _write(self.rca, "# rca\nFOLLOW-UP fixed in #987.\n")
        self.assertEqual(1, self._gate("--drift-only", "--staged")[0], "the index still holds the bare line")
        rc, out = self._gate("--drift-only", "--staged", "--only-paths", "docs/reviews/rca-sample-2026-10-23.md")
        self.assertEqual(0, rc, out)

    def test_a_directory_path_covers_its_files(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        self.assertEqual(1, self._gate("--drift-only", "--staged", "--only-paths", "docs/reviews")[0])

    def _git_calls(self, fn):
        calls = []
        real = ld._git_run

        def spy(*args):
            calls.append(args)
            return real(*args)
        with mock.patch.object(ld, "_git_run", spy):
            result = fn()
        return result, calls

    def _diff_specs(self, calls):
        return [list(a[a.index("--") + 1:]) for a in calls if "diff" in a]

    def test_only_paths_reads_only_the_rca_docs_it_names(self):
        seed = self.root / "docs" / "seed.md"
        _write(seed, "# seed\n")
        self._commit()
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        _write(seed, "# seed\nedited\n")
        found, calls = self._git_calls(lambda: ld.check_rca_followups(
            only_paths=["docs/seed.md", "docs/reviews/rca-sample-2026-10-23.md"]))
        self.assertEqual(1, len(found))
        self.assertEqual([["docs/reviews/rca-sample-2026-10-23.md"]], self._diff_specs(calls))

    def test_only_paths_naming_no_rca_doc_runs_no_diff(self):
        seed = self.root / "docs" / "seed.md"
        _write(seed, "# seed\n")
        self._commit()
        found, calls = self._git_calls(lambda: ld.check_rca_followups(only_paths=["docs/seed.md"]))
        self.assertEqual([], found)
        self.assertEqual([], self._diff_specs(calls))

    def test_only_paths_accepts_the_absolute_paths_the_hook_passes(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        found, calls = self._git_calls(lambda: ld.check_rca_followups(only_paths=[self.rca.as_posix()]))
        self.assertEqual([(self.rca.name, 2)], [(f.name, n) for f, n, _k, _t in found])
        self.assertEqual([["docs/reviews/rca-sample-2026-10-23.md"]], self._diff_specs(calls))

    def test_a_resolvable_head_is_not_probed_for_an_unborn_branch(self):
        _write(self.rca, "# rca\nFOLLOW-UP bare.\n")
        _found, calls = self._git_calls(lambda: ld.check_rca_followups())
        self.assertEqual([], [a for a in calls if "rev-parse" in a and "HEAD^{commit}" in a])


if __name__ == "__main__":
    unittest.main()
