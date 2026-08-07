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


if __name__ == "__main__":
    unittest.main()
