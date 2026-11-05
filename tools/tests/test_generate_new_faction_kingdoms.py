#!/usr/bin/env python3
"""upsert() in generate_new_faction_kingdoms.py: a re-run keeps its marker block where it stands.

Run:  python -m unittest tools.tests.test_generate_new_faction_kingdoms

#947 (ported from the 1.5.x line): upsert() deleted its marker block and re-appended it before the
closing tag, so a block or rows written after it moved on every re-run. On 1.4.x, heroes.xml holds 36
hand-authored heroes after the lindon block. The texts here are synthetic.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_new_faction_kingdoms as gen  # noqa: E402

MARKER = "TAOM-NEWFACTIONS:lindon"


def doc(lindon_rows, after=""):
    return ("<Factions>\n"
            "  <Faction id=\"vanilla\" />\n"
            f"  <!-- {MARKER}:BEGIN -->\n{lindon_rows}  <!-- {MARKER}:END -->\n"
            f"{after}"
            "</Factions>\n")


LATER = ("  <!-- TAOM-LATER-BLOCK:BEGIN -->\n"
        "  <Faction id=\"clan_later_1\" />\n"
        "  <!-- TAOM-LATER-BLOCK:END -->\n")


class UpsertTests(unittest.TestCase):
    def test_same_payload_with_a_block_after_it_changes_nothing(self):
        rows = "  <Faction id=\"clan_lindon_1\" />\n"
        text = doc(rows, after=LATER)
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), text)

    def test_new_payload_replaces_the_block_in_place(self):
        old = "  <Faction id=\"clan_lindon_1\" />\n"
        new = "  <Faction id=\"clan_lindon_1\" tier=\"5\" />\n"
        self.assertEqual(gen.upsert(doc(old, after=LATER), "</Factions>", new, MARKER), doc(new, after=LATER))

    def test_absent_block_is_appended_before_the_closing_tag(self):
        rows = "  <Faction id=\"clan_lindon_1\" />\n"
        text = "<Factions>\n  <Faction id=\"vanilla\" />\n</Factions>\n"
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), doc(rows))

    def test_a_second_region_is_refused(self):
        # The old strip-all upsert made --check report a stale second copy; replacing only the first would hide it,
        # and the engine loads the later row of a repeated id.
        rows = "  <Faction id=\"clan_lindon_1\" />\n"
        stale = f"  <!-- {MARKER}:BEGIN -->\n  <Faction id=\"clan_lindon_1\" tier=\"6\" />\n  <!-- {MARKER}:END -->\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows, after=LATER + stale), "</Factions>", rows, MARKER)

    def test_a_begin_without_its_end_is_refused(self):
        text = f"<Factions>\n  <!-- {MARKER}:BEGIN -->\n  <Faction id=\"clan_lindon_1\" />\n</Factions>\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(text, "</Factions>", "  <Faction id=\"clan_lindon_1\" />\n", MARKER)

    def test_an_end_without_its_begin_is_refused(self):
        rows = "  <Faction id=\"clan_lindon_1\" />\n"
        orphan = f"  <Faction id=\"clan_lindon_1\" tier=\"6\" />\n  <!-- {MARKER}:END -->\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows, after=orphan), "</Factions>", rows, MARKER)

    def test_a_missing_closing_tag_is_refused(self):
        with self.assertRaises(RuntimeError):
            gen.upsert("<Factions>\n", "</Factions>", "  <Faction id=\"x\" />\n", MARKER)

    def test_a_missing_closing_tag_is_refused_when_the_region_exists(self):
        rows = "  <Faction id=\"clan_lindon_1\" />\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows).replace("</Factions>\n", ""), "</Factions>", rows, MARKER)

    def test_an_end_marker_straight_before_the_closing_tag_is_a_region(self):
        rows = "  <Faction id=\"clan_lindon_1\" />\n"
        text = doc(rows).replace(f":END -->\n</Factions>", ":END --></Factions>")
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), doc(rows))

    def test_payload_backslashes_are_written_verbatim(self):
        rows = "  <Faction id=\"x\" name=\"a\\1b\" />\n"
        self.assertIn("a\\1b", gen.upsert(doc("", after=LATER), "</Factions>", rows, MARKER))


if __name__ == "__main__":
    unittest.main()
