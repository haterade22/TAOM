"""Tests for tools/lock_creature_gear.py: the replay of the troll-gear lockout into a LOTRLOME_Armory.

Run:  python -m pytest tools/tests/test_lock_creature_gear.py -q
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import lock_creature_gear as lcg  # noqa: E402

CRLF = "\r\n"


def _items(entries):
    return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + CRLF + "<Items>" + CRLF + entries + "</Items>" + CRLF


def _item(tag, item_id, merch_line):
    lines = [f"    <{tag}", f'        id="{item_id}"', '        culture="Culture.mordor"']
    if merch_line:
        lines.append(f"        {merch_line}")
    lines.append('        weight="1.0">')
    lines.append(f"    </{tag}>")
    return CRLF.join(lines) + CRLF


def _piece(pid):
    return CRLF.join([
        "    <CraftingPiece", f'        id="{pid}"', '        tier="3"', '        piece_type="Blade" />']) + CRLF


class LockCreatureGearTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "LOTRLOME_Armory" / "ModuleData"
        files = {
            "LOTRLOME_items/LOTRAOM_weapons.xml": _items(
                _item("CraftedItem", "wm_cave_troll_1h_mace_a", 'is_merchandise="true"')
                + _item("CraftedItem", "other_mace", 'is_merchandise="true"')),
            "LOTRLOME_items/LOTRAOM_shields.xml": _items(
                _item("Item", "wm_cave_troll_shield_a01", 'is_merchandise="true"')),
            "LOTRLOME_items/troll/arm_armors.xml": _items(_item("Item", "lotr_troll_bracers", None)),
            lcg.PIECES_FILE: ("<?xml version=\"1.0\"?>" + CRLF + "<CraftingPieces>" + CRLF
                              + "".join(_piece(p) for p in lcg.PIECES) + _piece("human_blade")
                              + "</CraftingPieces>" + CRLF),
        }
        for rel, text in files.items():
            path = self.md / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(text.encode("utf-8"))
        self.before = {rel: (self.md / rel).read_bytes() for rel in files}

    def tearDown(self):
        self._tmp.cleanup()

    def _text(self, rel):
        return (self.md / rel).read_bytes().decode("utf-8")

    def test_dry_run_writes_nothing(self):
        self.assertEqual(lcg.lock(self.md, apply=False, stamp="t"), 4)
        for rel, data in self.before.items():
            self.assertEqual((self.md / rel).read_bytes(), data, rel)

    def test_apply_locks_every_item_and_piece_and_nothing_else(self):
        self.assertEqual(lcg.lock(self.md, apply=True, stamp="t"), 4)

        weapons = self._text("LOTRLOME_items/LOTRAOM_weapons.xml")
        self.assertIn('id="wm_cave_troll_1h_mace_a"\r\n        culture="Culture.mordor"\r\n        is_merchandise="false"', weapons)
        self.assertIn('id="other_mace"\r\n        culture="Culture.mordor"\r\n        is_merchandise="true"', weapons)
        self.assertIn('is_merchandise="false"', self._text("LOTRLOME_items/LOTRAOM_shields.xml"))
        self.assertIn('culture="Culture.mordor"\r\n        is_merchandise="false"\r\n        weight',
                      self._text("LOTRLOME_items/troll/arm_armors.xml"))
        pieces = self._text(lcg.PIECES_FILE)
        self.assertEqual(pieces.count('is_hidden="true"'), len(lcg.PIECES))
        self.assertNotIn('id="human_blade"\r\n        tier="3"\r\n        is_hidden', pieces)
        # CRLF kept: no bare LF anywhere.
        for rel in self.before:
            self.assertNotIn("\n", self._text(rel).replace("\r\n", ""), rel)

    def test_apply_backs_up_each_changed_file_without_an_xml_extension(self):
        lcg.lock(self.md, apply=True, stamp="t")
        for rel, data in self.before.items():
            backup = (self.md / rel).with_name((self.md / rel).name + ".bak-t")
            self.assertEqual(backup.read_bytes(), data, rel)
            self.assertFalse(backup.name.endswith(".xml"))

    def test_second_run_changes_nothing(self):
        lcg.lock(self.md, apply=True, stamp="t")
        after = {rel: (self.md / rel).read_bytes() for rel in self.before}
        self.assertEqual(lcg.lock(self.md, apply=True, stamp="t2"), 0)
        for rel, data in after.items():
            self.assertEqual((self.md / rel).read_bytes(), data, rel)

    def test_a_missing_item_writes_nothing_anywhere(self):
        # The shield file is planned after the weapons file: a failure there must not leave the weapons half-locked.
        (self.md / "LOTRLOME_items/LOTRAOM_shields.xml").write_bytes(_items("").encode("utf-8"))
        with self.assertRaises(lcg.LockError):
            lcg.lock(self.md, apply=True, stamp="t")
        self.assertEqual((self.md / "LOTRLOME_items/LOTRAOM_weapons.xml").read_bytes(),
                         self.before["LOTRLOME_items/LOTRAOM_weapons.xml"])

    def test_a_piece_hidden_false_is_left_for_a_person(self):
        path = self.md / lcg.PIECES_FILE
        path.write_bytes(path.read_bytes().replace(
            b'id="wm_cave_troll_2h_mace_head"\r\n        tier="3"',
            b'id="wm_cave_troll_2h_mace_head"\r\n        tier="3"\r\n        is_hidden="false"'))
        with self.assertRaises(lcg.LockError):
            lcg.lock(self.md, apply=False, stamp="t")


if __name__ == "__main__":
    unittest.main()
