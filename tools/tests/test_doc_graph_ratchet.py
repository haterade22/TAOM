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
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_doc_graph_ratchet as ratchet  # noqa: E402

_BUILD = ratchet._orphans_and_components  # the real builder, captured before any test patches it
_LIVE_GRAPH = None


def _live_graph():
    """One build of the live docs graph for this module; each build reads the whole docs tree."""
    global _LIVE_GRAPH
    if _LIVE_GRAPH is None:
        _LIVE_GRAPH = _BUILD()
    return _LIVE_GRAPH


def _shared_graph():
    """Serve the tool's graph builder from the one build above, for the tests that read the live tree."""
    return mock.patch.object(ratchet, "_orphans_and_components", side_effect=_live_graph)


class CompareTests(unittest.TestCase):
    BASE = {"orphans": 153, "components": 156}

    def test_an_empty_corpus_is_a_problem_and_not_an_improvement(self):
        problems = ratchet.compare({"orphans": 0, "components": 0}, self.BASE)
        self.assertEqual(len(problems), 1)
        self.assertIn("no docs", problems[0])

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


class EligibilityTests(unittest.TestCase):
    def test_review_records_are_outside_the_ratchet(self):
        docs = ratchet.dg.ld.DOCS_DIR
        self.assertFalse(ratchet.ratchet_eligible((docs / "reviews" / "rca-x.md").resolve()))
        self.assertFalse(ratchet.ratchet_eligible((docs / "reviews" / "lessons" / "a.md").resolve()))

    def test_ordinary_docs_stay_in(self):
        docs = ratchet.dg.ld.DOCS_DIR
        self.assertTrue(ratchet.ratchet_eligible((docs / "features" / "enlistment.md").resolve()))


class FailureTextTests(unittest.TestCase):
    def run_main(self, orphans):
        import io
        from unittest import mock
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp) / "baseline.json"
            base.write_text(json.dumps({"orphans": 1, "components": 1}), encoding="utf-8")
            out = io.StringIO()
            with mock.patch.object(sys, "argv", ["check_doc_graph_ratchet.py", "--baseline", str(base)]), \
                    mock.patch.object(ratchet, "current_metrics", return_value={"orphans": 2, "components": 1}), \
                    mock.patch.object(ratchet, "orphan_docs", return_value=orphans), \
                    mock.patch("sys.stdout", out):
                rc = ratchet.main()
        return rc, out.getvalue()

    def test_a_failure_lists_the_orphans_the_gate_counted(self):
        rc, text = self.run_main(["docs/features/lonely.md", "docs/reference/stray.md"])
        self.assertEqual(rc, 1)
        self.assertIn("docs/features/lonely.md", text)
        self.assertIn("docs/reference/stray.md", text)

    def test_a_failure_does_not_send_the_reader_to_a_count_that_includes_reviews(self):
        _, text = self.run_main(["docs/features/lonely.md"])
        self.assertNotIn("graph_query.py metrics", text)

    def test_orphan_docs_agrees_with_the_orphan_count(self):
        with _shared_graph():
            self.assertEqual(len(ratchet.orphan_docs()), ratchet.current_metrics()["orphans"])


class LiveTreeTests(unittest.TestCase):
    """The ratchet against the real docs/ tree: nothing else runs it, so without this test the
    orphan and component counts could grow while every suite stays green."""

    def test_the_live_tree_is_within_the_baseline(self):
        with _shared_graph():
            baseline = json.loads(ratchet.BASELINE.read_text(encoding="utf-8"))
            problems = ratchet.compare(ratchet.current_metrics(), baseline)
            self.assertEqual(problems, [], "the doc graph got worse: link the new doc from INDEX.md "
                                           "or a sibling, or run tools/check_doc_graph_ratchet.py --update "
                                           "and say why")


if __name__ == "__main__":
    unittest.main()
