#!/usr/bin/env python3
"""audit_scene_names.py skips a settlements file whose module registers no Settlements.

Run:  python -m unittest tools.tests.test_audit_scene_names
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import audit_scene_names as asn  # noqa: E402


class RegistersSettlementsTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.module = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def _manifest(self, body):
        (self.module / "SubModule.xml").write_text(f"<Module><Xmls>{body}</Xmls></Module>", encoding="utf-8")

    def test_a_registered_settlements_row_is_true(self):
        self._manifest('<XmlNode><XmlName id="Settlements" path="settlements"/></XmlNode>')
        self.assertTrue(asn.registers_settlements(self.module))

    def test_a_row_that_spans_several_lines_is_true(self):
        self._manifest('<XmlNode><XmlName\n  id="Settlements"\n  path="settlements"\n/></XmlNode>')
        self.assertTrue(asn.registers_settlements(self.module))

    def test_no_settlements_row_is_false(self):
        self._manifest('<XmlNode><XmlName id="Items" path="items"/></XmlNode>')
        self.assertFalse(asn.registers_settlements(self.module))

    def test_a_missing_manifest_is_false(self):
        self.assertFalse(asn.registers_settlements(self.module))

    def test_a_damaged_manifest_is_true_so_it_never_hides_a_suspect(self):
        (self.module / "SubModule.xml").write_text("<Module><Xmls>", encoding="utf-8")
        self.assertTrue(asn.registers_settlements(self.module))


if __name__ == "__main__":
    unittest.main()
