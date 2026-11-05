#!/usr/bin/env python3
"""Unit tests for tools/generate_enlistment_rosters.py.

Run:  python -m unittest tools.tests.test_generate_enlistment_rosters
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_enlistment_rosters as ger  # noqa: E402


class SeedMarkerTests(unittest.TestCase):
    def test_the_seed_marker_is_a_legal_xml_comment(self):
        # XML forbids "--" inside a comment, and the marker once named the flag, so every
        # --seed-missing run refused its own output as malformed (#798).
        ET.fromstring(f'<EquipmentRosters>\n    {ger.SEED_MARKER}\n</EquipmentRosters>')

    def test_the_append_path_writes_the_constant(self):
        with open(ger.__file__, encoding="utf-8") as fh:
            self.assertNotIn("generate_enlistment_rosters.py --seed-missing -->", fh.read())


if __name__ == "__main__":
    unittest.main()
