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
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

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


class AiDashGitScopeTests(unittest.TestCase):
    """check_ai_dashes reports NEW writing only.

    40,476 em dashes already live in the tree (CHANGELOG 1,604 / docs 37,448 /
    .claude 1,424, counted 2026-08-11). A whole-tree check would drown the report
    and the rule would be ignored, so the scope is added lines plus untracked files.
    """

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


if __name__ == "__main__":
    unittest.main()
