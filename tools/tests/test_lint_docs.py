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


if __name__ == "__main__":
    unittest.main()
