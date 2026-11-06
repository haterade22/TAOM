#!/usr/bin/env python3
"""tools/_marker_region.py: replace one generator-owned marker region in place, or refuse.

Run:  python -m unittest tools.tests.test_marker_region

#947 and #975: a region re-appended at the closing tag moved below any block added after it, which a generator's
--check reported as drift; and a guard that replaced only the first region let a stale second copy pass (the
engine loads the later row of a repeated id).
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from _marker_region import replace_region  # noqa: E402

M = "TAOM-TEST:x"
CLOSE = "</Root>"


def region(rows, nl="\n", indent="  "):
    return f"{indent}<!-- {M}:BEGIN -->{nl}{rows}{indent}<!-- {M}:END -->{nl}"


OTHER = "  <!-- OTHER:BEGIN -->\n  <Row id=\"other\" />\n  <!-- OTHER:END -->\n"
ROWS = "  <Row id=\"a\" />\n"
NEW = "  <Row id=\"a\" v=\"2\" />\n"


class ReplaceRegionTests(unittest.TestCase):
    def test_absent_region_goes_before_the_last_closing_tag(self):
        text = "<Root>\n  <Row id=\"v\" />\n</Root>\n"
        self.assertEqual(replace_region(text, M, region(ROWS), CLOSE),
                         "<Root>\n  <Row id=\"v\" />\n" + region(ROWS) + "</Root>\n")

    def test_existing_region_is_replaced_where_it_stands(self):
        text = "<Root>\n" + region(ROWS) + OTHER + "</Root>\n"
        self.assertEqual(replace_region(text, M, region(NEW), CLOSE), "<Root>\n" + region(NEW) + OTHER + "</Root>\n")

    def test_the_same_region_changes_nothing(self):
        text = "<Root>\n" + region(ROWS) + OTHER + "</Root>\n"
        self.assertEqual(replace_region(text, M, region(ROWS), CLOSE), text)

    def test_crlf_text_with_a_crlf_block_is_unchanged(self):
        text = "<Root>\r\n" + region("\t<Row id=\"a\" />\r\n", "\r\n", "\t") + "</Root>\r\n"
        self.assertEqual(replace_region(text, M, region("\t<Row id=\"a\" />\r\n", "\r\n", "\t"), CLOSE), text)

    def test_an_end_straight_before_the_closing_tag_is_a_region(self):
        text = "<Root>\n" + region(ROWS).rstrip("\n") + "</Root>\n"
        self.assertEqual(replace_region(text, M, region(ROWS), CLOSE), "<Root>\n" + region(ROWS) + "</Root>\n")

    def test_payload_backslashes_are_written_verbatim(self):
        rows = "  <Row id=\"a\\1b\" />\n"
        self.assertIn("a\\1b", replace_region("<Root>\n" + region(ROWS) + "</Root>\n", M, region(rows), CLOSE))

    def test_a_marker_that_extends_this_one_is_not_this_region(self):
        longer = region(ROWS).replace(M, M + "2")
        text = "<Root>\n" + longer + "</Root>\n"
        self.assertEqual(replace_region(text, M, region(ROWS), CLOSE), "<Root>\n" + longer + region(ROWS) + "</Root>\n")

    def test_refusals(self):
        cases = {
            "two regions": "<Root>\n" + region(ROWS) + region(NEW) + "</Root>\n",
            "lone BEGIN": f"<Root>\n  <!-- {M}:BEGIN -->\n{ROWS}</Root>\n",
            "lone END": "<Root>\n" + region(ROWS) + f"{NEW}  <!-- {M}:END -->\n</Root>\n",
            "no closing tag, no region": "<Root>\n",
            "no closing tag, with region": "<Root>\n" + region(ROWS),
        }
        for name, text in cases.items():
            with self.subTest(name):
                with self.assertRaises(RuntimeError):
                    replace_region(text, M, region(ROWS), CLOSE)


if __name__ == "__main__":
    unittest.main()
