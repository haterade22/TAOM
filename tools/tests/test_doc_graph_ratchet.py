#!/usr/bin/env python3
"""Unit tests for tools/check_doc_graph_ratchet.py.

Why this exists at all: `test_graph_query.py` tests the metrics ALGORITHM against
synthetic fixtures and was green every day while the real `docs/` tree went from 64
isolated docs to 153. A green algorithm suite says nothing about the corpus. The
ratchet is the check that would have spoken, so its comparison logic is tested in
BOTH directions here, because a gate only ever seen passing is not known to be able
to fail.
"""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_doc_graph_ratchet as ratchet  # noqa: E402


class CompareTests(unittest.TestCase):
    BASE = {"orphans": 153, "components": 156}

    def test_equal_to_baseline_passes(self):
        self.assertEqual(ratchet.compare(dict(self.BASE), self.BASE), [])

    def test_below_baseline_passes(self):
        """Lowering the numbers is the whole point; it must never be a failure."""
        better = {"orphans": 100, "components": 120}
        self.assertEqual(ratchet.compare(better, self.BASE), [])

    def test_more_orphans_fails(self):
        problems = ratchet.compare({"orphans": 154, "components": 156}, self.BASE)
        self.assertEqual(len(problems), 1)
        self.assertIn("orphans", problems[0])
        self.assertIn("+1", problems[0])

    def test_more_components_fails(self):
        problems = ratchet.compare({"orphans": 153, "components": 157}, self.BASE)
        self.assertEqual(len(problems), 1)
        self.assertIn("components", problems[0])

    def test_both_regressions_are_reported(self):
        problems = ratchet.compare({"orphans": 200, "components": 200}, self.BASE)
        self.assertEqual(len(problems), 2)

    def test_missing_baseline_key_is_reported_not_ignored(self):
        """A truncated baseline must not read as 'nothing to check'."""
        problems = ratchet.compare({"orphans": 1, "components": 1}, {"orphans": 5})
        self.assertTrue(any("components" in p and "missing" in p for p in problems))


class BaselineFileTests(unittest.TestCase):
    def test_shipped_baseline_is_valid_and_complete(self):
        data = json.loads(ratchet.BASELINE.read_text(encoding="utf-8"))
        for key in ratchet.RATCHETED:
            self.assertIn(key, data, f"{key} missing from the shipped baseline")
            self.assertIsInstance(data[key], int)

    def test_update_writes_what_compare_reads(self):
        """Round-trip: --update output must satisfy compare() against itself."""
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "baseline.json"
            current = {"orphans": 7, "components": 9}
            p.write_text(json.dumps(current, indent=2) + "\n", encoding="utf-8")
            self.assertEqual(
                ratchet.compare(current, json.loads(p.read_text(encoding="utf-8"))), [])


if __name__ == "__main__":
    unittest.main()
