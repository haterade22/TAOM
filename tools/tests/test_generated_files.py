#!/usr/bin/env python3
"""tools/_generated_files.py: the writer both Armory generators use, and the skill carry-over of the Lorien troop
generator. Temp folders only, no game install.

Run: python -m unittest tools.tests.test_generated_files
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _generated_files as gf  # noqa: E402
import generate_lothlorien_troops as glt  # noqa: E402

A = '<Items>\n    <Item id="a" />\n</Items>\n'
B = '<Items>\n    <Item id="b" />\n</Items>\n'


class SyncTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.out = Path(self._tmp.name) / "items"
        self.out.mkdir()

    def tearDown(self):
        self._tmp.cleanup()

    def _put(self, name, text):
        (self.out / name).write_bytes(text.encode("utf-8"))

    def _backups(self):
        return sorted(p.name for p in self.out.iterdir() if ".bak-gen-" in p.name)

    def test_a_dry_run_writes_nothing(self):
        self._put("x.xml", A)
        before = (self.out / "x.xml").read_bytes()
        self.assertEqual(gf.sync(self.out, {"x.xml": B, "y.xml": A}), 0)
        self.assertEqual((self.out / "x.xml").read_bytes(), before)
        self.assertFalse((self.out / "y.xml").exists())
        self.assertEqual(self._backups(), [])

    def test_check_returns_0_when_every_file_is_identical(self):
        self._put("x.xml", A.replace("\n", "\r\n"))
        self.assertEqual(gf.sync(self.out, {"x.xml": A}, check=True), 0)

    def test_check_returns_1_when_a_file_differs_or_is_missing(self):
        self._put("x.xml", A)
        self.assertEqual(gf.sync(self.out, {"x.xml": B}, check=True), 1)
        self.assertEqual(gf.sync(self.out, {"x.xml": A, "gone.xml": A}, check=True), 1)

    def test_check_writes_nothing(self):
        self._put("x.xml", A)
        before = (self.out / "x.xml").read_bytes()
        gf.sync(self.out, {"x.xml": B}, check=True)
        self.assertEqual((self.out / "x.xml").read_bytes(), before)
        self.assertEqual(self._backups(), [])

    def test_apply_writes_crlf_without_a_bom_and_backs_up_only_what_changed(self):
        self._put("changed.xml", A)
        self._put("same.xml", B.replace("\n", "\r\n"))
        self.assertEqual(gf.sync(self.out, {"changed.xml": B, "same.xml": B, "new.xml": A}, apply=True), 0)
        for name, want in (("changed.xml", B), ("same.xml", B), ("new.xml", A)):
            data = (self.out / name).read_bytes()
            self.assertEqual(data, want.replace("\n", "\r\n").encode("utf-8"), name)
            self.assertFalse(data.startswith(b"\xef\xbb\xbf"), name)
        backups = self._backups()
        self.assertEqual(len(backups), 1)
        self.assertTrue(backups[0].startswith("changed.xml.bak-gen-"))
        self.assertFalse(backups[0].endswith(".xml"), "the folder is globbed for *.xml; a backup must not load")
        self.assertEqual((self.out / backups[0]).read_bytes(), A.encode("utf-8"))

    def test_a_second_apply_changes_and_backs_up_nothing(self):
        self._put("x.xml", A)
        gf.sync(self.out, {"x.xml": B}, apply=True)
        snapshot = {p.name: p.read_bytes() for p in self.out.iterdir()}
        self.assertEqual(gf.sync(self.out, {"x.xml": B}, apply=True), 0)
        self.assertEqual({p.name: p.read_bytes() for p in self.out.iterdir()}, snapshot)

    def test_a_render_that_does_not_parse_returns_1_and_leaves_every_file_untouched(self):
        self._put("good.xml", A)
        before = (self.out / "good.xml").read_bytes()
        result = gf.sync(self.out, {"good.xml": B, "bad.xml": "<Items><Item></Items>"}, apply=True)
        self.assertEqual(result, 1)
        self.assertEqual((self.out / "good.xml").read_bytes(), before)
        self.assertFalse((self.out / "bad.xml").exists())
        self.assertEqual(self._backups(), [])


class ExistingSkillsTests(unittest.TestCase):
    """The Lorien troop generator keeps placed skills through an XML parse, whatever the attribute order or BOM."""

    def test_skills_are_read_per_troop_id_in_any_attribute_order_with_a_bom(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "troops.xml"
            path.write_bytes(b"\xef\xbb\xbf" + (
                '<NPCCharacters><NPCCharacter level="21" id="t1"><skills>'
                '<skill value="55" id="Bow" /><skill id="Athletics" value="120" /></skills></NPCCharacter>'
                '<NPCCharacter id="t2"><skills /></NPCCharacter></NPCCharacters>').encode("utf-8"))
            self.assertEqual(glt.existing_skills(path), {"t1": {"Bow": 55, "Athletics": 120}, "t2": {}})

    def test_a_missing_file_yields_no_skills(self):
        self.assertEqual(glt.existing_skills(Path(tempfile.gettempdir()) / "no_such_troops_file.xml"), {})


if __name__ == "__main__":
    unittest.main()
