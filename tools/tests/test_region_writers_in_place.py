#!/usr/bin/env python3
"""promote_borrowed_cultures.upsert and insert_new_factions.upsert_before keep their region where it stands (#976).

Run:  python -m unittest tools.tests.test_region_writers_in_place

Both deleted their region and re-appended it before the closing tag, so a block added after it read as drift and
moved on every run (the cause of #947). These tests call the functions only: insert_new_factions.py has no
--check and its older regions have content drift, so it must never be run against the data to test this.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import insert_new_factions as inf  # noqa: E402
import promote_borrowed_cultures as pbc  # noqa: E402

M = "TAOM-NEWFACTIONS:goblin"
OTHER = "  <!-- OTHER:BEGIN -->\n  <Row id=\"other\" />\n  <!-- OTHER:END -->\n"


def region(payload):
    return f"  <!-- {M}:BEGIN -->\n{payload}\n  <!-- {M}:END -->\n"


WRITERS = {
    "promote_borrowed_cultures.upsert": lambda text, payload: pbc.upsert(text, "</Root>", payload, M),
    "insert_new_factions.upsert_before": lambda text, payload: inf.upsert_before(text, "</Root>", payload, M),
}


class InPlaceTests(unittest.TestCase):
    def test_a_block_after_the_region_stays_after_it(self):
        text = "<Root>\n" + region("  <Row id=\"a\" />") + OTHER + "</Root>\n"
        for name, write in WRITERS.items():
            with self.subTest(name):
                self.assertEqual(write(text, "  <Row id=\"a\" />"), text)
                self.assertEqual(write(text, "  <Row id=\"a\" v=\"2\" />"),
                                 "<Root>\n" + region("  <Row id=\"a\" v=\"2\" />") + OTHER + "</Root>\n")

    def test_an_absent_region_goes_before_the_closing_tag(self):
        for name, write in WRITERS.items():
            with self.subTest(name):
                self.assertEqual(write("<Root>\n</Root>\n", "  <Row id=\"a\" />"),
                                 "<Root>\n" + region("  <Row id=\"a\" />") + "</Root>\n")

    def test_a_second_region_is_refused(self):
        text = "<Root>\n" + region("  <Row id=\"a\" />") + region("  <Row id=\"a\" v=\"6\" />") + "</Root>\n"
        for name, write in WRITERS.items():
            with self.subTest(name):
                with self.assertRaises(RuntimeError):
                    write(text, "  <Row id=\"a\" />")

    def test_promote_still_picks_the_strings_or_base_closing_tag(self):
        for root in ("strings", "base"):
            with self.subTest(root):
                text = f"<{root}>\n</{root}>\n"
                self.assertEqual(pbc.upsert(text, None, "  <string id=\"a\" />", M),
                                 f"<{root}>\n" + region("  <string id=\"a\" />") + f"</{root}>\n")


class ParseBeforeWriteTests(unittest.TestCase):
    """#979: both scripts wrote a file, then parsed it, so a broken output landed on disk and the files after it
    were never written. They now parse every output before writing any."""

    GOOD = "<Root>\n  <Row id=\"a\" />\n</Root>\n"
    BAD = "<Root>\n  <Row id=\"a\">\n</Root>\n"

    def test_promote_lists_each_unparseable_output(self):
        pending = [("good.xml", self.GOOD, "\r\n", False), ("bad.xml", self.BAD, "\r\n", True)]
        self.assertEqual([path for path, _ in pbc.unparsable(pending)], ["bad.xml"])
        self.assertEqual(pbc.unparsable(pending[:1]), [])

    def test_insert_refuses_before_writing_anything(self):
        with self.assertRaises(SystemExit) as raised:
            inf.refuse_unparsable([("good.xml", self.GOOD), ("bad.xml", self.BAD)])
        self.assertIn("bad.xml", str(raised.exception))
        inf.refuse_unparsable([("good.xml", self.GOOD)])

    def test_insert_guards_every_output_before_its_first_write(self):
        # main() cannot run in a test (it rewrites eight shared files), so pin the order in its source.
        path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "insert_new_factions.py")
        body = open(path, encoding="utf-8").read().split("def main():", 1)[1]
        guard = body.index("refuse_unparsable(outputs)")
        self.assertLess(body.index("outputs.append((SUBMODULE, sm))"), guard, "SubModule.xml is guarded too")
        self.assertLess(guard, body.index('open(path, "w"'), "the guard runs before the first write")
        self.assertEqual(body.count('"w"'), 1, "every write goes through the guarded loop")


if __name__ == "__main__":
    unittest.main()
