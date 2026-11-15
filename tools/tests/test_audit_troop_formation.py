#!/usr/bin/env python3
"""audit_troop_formation.py: a troop's default_group must match what its battle sets carry.

Run:  python -m unittest tools.tests.test_audit_troop_formation
"""
import io
import os
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET
from pathlib import Path

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import audit_troop_formation as atf  # noqa: E402

INDEX = {"bow": "launcher", "xbow": "launcher", "jav": "thrown", "sword": "other",
         "shield": "shield", "horse": "horse"}


def npc(body: str) -> ET.Element:
    return ET.fromstring(f'<NPCCharacter id="t">{body}</NPCCharacter>')


def eq(slot, item):
    return f'<equipment slot="{slot}" id="Item.{item}" />'


class BattleSetTests(unittest.TestCase):
    def test_loose_equipment_is_laid_over_every_set(self):
        n = npc("<Equipments>"
                f"<EquipmentRoster>{eq('Item0', 'sword')}</EquipmentRoster>"
                f"<EquipmentRoster>{eq('Item0', 'bow')}</EquipmentRoster>"
                f"{eq('Horse', 'horse')}</Equipments>")
        sets = atf.battle_sets(n, {})
        self.assertEqual(2, len(sets))
        self.assertTrue(all(s.get("Horse") == "horse" for s in sets))

    def test_empty_loose_id_clears_the_slot(self):
        n = npc("<Equipments>"
                f"<EquipmentRoster>{eq('Item0', 'sword')}{eq('Horse', 'horse')}</EquipmentRoster>"
                '<equipment slot="Horse" id="" /></Equipments>')
        self.assertNotIn("Horse", atf.battle_sets(n, {})[0])

    def test_civilian_and_stealth_sets_are_not_battle_sets(self):
        n = npc("<Equipments>"
                f"<EquipmentRoster>{eq('Item0', 'sword')}</EquipmentRoster>"
                f'<EquipmentRoster equipmentType="Civilian">{eq("Horse", "horse")}</EquipmentRoster>'
                f'<EquipmentRoster civilian="true">{eq("Item0", "bow")}</EquipmentRoster>'
                '<EquipmentSet id="EquipmentRoster.r" equipmentType="Stealth" /></Equipments>')
        self.assertEqual([{"Item0": "sword"}], atf.battle_sets(n, {"r": [{"Item0": "bow"}]}))

    def test_referenced_roster_sets_count(self):
        n = npc('<Equipments><EquipmentSet id="EquipmentRoster.r" /></Equipments>')
        self.assertEqual([{"Item0": "bow"}], atf.battle_sets(n, {"r": [{"Item0": "bow"}]}))

    def test_a_standalone_roster_reads_its_capitalised_equipment_children(self):
        # Standalone EquipmentRoster files write <Equipment>; the engine reads every child whatever its
        # name (Equipment.Deserialize), so the loader must not see these sets as empty (#1256 review).
        s = ET.fromstring('<EquipmentSet><Equipment slot="Horse" id="Item.horse" />'
                          '<Equipment slot="Item0" id="Item.bow" /></EquipmentSet>')
        self.assertEqual({"Horse": "horse", "Item0": "bow"}, atf._items_of(s))


class VerdictTests(unittest.TestCase):
    def check(self, group, sets):
        return atf.verdict(group, atf.classify(sets, INDEX))

    def test_mounted_bow_is_horse_archer(self):
        self.assertEqual(("OK", "HorseArcher"),
                         self.check("HorseArcher", [{"Item0": "bow", "Horse": "horse"}]))
        self.assertEqual("MISMATCH", self.check("Cavalry", [{"Item0": "bow", "Horse": "horse"}])[0])

    def test_mounted_melee_and_mounted_javelin_are_cavalry(self):
        self.assertEqual(("OK", "Cavalry"), self.check("Cavalry", [{"Item0": "sword", "Horse": "horse"}]))
        self.assertEqual(("MISMATCH", "Cavalry"),
                         self.check("HorseArcher", [{"Item0": "jav", "Horse": "horse"}]))

    def test_foot_bow_or_crossbow_is_ranged(self):
        self.assertEqual(("OK", "Ranged"), self.check("Ranged", [{"Item0": "xbow"}]))
        self.assertEqual("MISMATCH", self.check("Infantry", [{"Item0": "bow"}])[0])

    def test_foot_javelin_is_infantry(self):
        self.assertEqual(("MISMATCH", "Infantry"), self.check("Ranged", [{"Item0": "jav", "Item1": "sword"}]))
        self.assertEqual("OK", self.check("Infantry", [{"Item0": "jav"}])[0])

    def test_mount_or_bow_in_only_some_sets_is_mixed(self):
        self.assertEqual("MIXED", self.check("Cavalry", [{"Item0": "sword", "Horse": "horse"},
                                                         {"Item0": "sword"}])[0])
        self.assertEqual("MIXED", self.check("Ranged", [{"Item0": "bow"}, {"Item0": "sword"}])[0])

    def test_no_battle_set_is_reported_separately(self):
        self.assertEqual("NOSETS", self.check("Infantry", [])[0])

    def test_a_launcher_in_different_slots_of_different_sets_is_mixed(self):
        # Each slot is drawn from its own set (Equipment.cs:571-590), so a bow in Item0 of set A and Item2
        # of set B can spawn with no bow at all: Ranged cannot be promised.
        sets = [{"Item0": "bow"}, {"Item2": "bow"}]
        self.assertEqual("MIXED", self.check("Ranged", sets)[0])
        # The report still counts the sets that carry one; the guarantee is a flag of its own.
        c = atf.classify(sets, INDEX)
        self.assertEqual((2, False), (c["launcher"], c["launcher_all"]))
        self.assertEqual("MIXED", self.check("Infantry", sets)[0])

    def test_a_launcher_in_the_same_slot_of_every_set_is_ranged(self):
        self.assertEqual(("OK", "Ranged"), self.check("Ranged", [{"Item0": "bow", "Item1": "sword"},
                                                                 {"Item0": "xbow"}]))

    def test_a_sling_item_is_indexed_as_a_launcher(self):
        # ItemObject.ItemTypeEnum.Sling exists in v1.5.7; no shipped item uses it yet.
        with tempfile.TemporaryDirectory() as tmp:
            data = Path(tmp) / "ModuleData"
            data.mkdir()
            (data / "items.xml").write_text('<Items><Item id="s" Type="Sling" /></Items>', encoding="utf-8")
            with mock.patch.object(atf, "MODULE_DATA", data):
                index = atf.build_item_index(Path(tmp) / "Modules")
        self.assertEqual("launcher", index["s"])


class ParseFailureTests(unittest.TestCase):
    """A troop file that does not parse must fail the gate, not vanish from it (#1295)."""

    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name)
        self.data = self.root / "ModuleData"
        self.data.mkdir()
        for name, value in (("MODULE_DATA", self.data), ("REPO", self.root)):
            patcher = mock.patch.object(atf, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)
        (self.data / "troops_good.xml").write_text(
            '<NPCCharacters><NPCCharacter id="g" default_group="Infantry"><Equipments>'
            '<EquipmentRoster><equipment slot="Item0" id="Item.sword" /></EquipmentRoster>'
            "</Equipments></NPCCharacter></NPCCharacters>", encoding="utf-8")
        (self.data / "troops_broken.xml").write_text("<NPCCharacters><NPCCharacter", encoding="utf-8")

    def test_scan_reports_each_file_that_does_not_parse(self):
        errors = []
        troops = atf.scan(self.root / "Modules", errors)
        self.assertIn("g", troops)
        self.assertTrue(errors and all("troops_broken.xml" in e for e in errors), errors)

    def test_main_fails_and_names_the_broken_file(self):
        out = io.StringIO()
        with (mock.patch.object(atf, "game_modules", return_value=str(self.root / "Modules")),
              mock.patch("sys.stdout", out),
              mock.patch("sys.stderr", out)):
            (self.root / "Modules").mkdir()
            code = atf.main([])
        self.assertEqual(1, code)
        self.assertIn("troops_broken.xml", out.getvalue())


class ShippedDataTests(unittest.TestCase):
    """Every shipped troop's default_group matches its gear (needs the install for item types)."""

    def test_no_shipped_troop_mismatches_its_gear(self):
        modules = atf.game_modules(None)
        if modules is None or not Path(modules).is_dir():
            self.skipTest("BANNERLORD_GAME_DIR not set")
        errors = []
        troops = atf.scan(Path(modules), errors)
        self.assertEqual([], errors)
        bad = sorted(f"{t['id']} {t['group']} -> {t['expected'] or 'mixed sets'}"
                     for t in troops.values() if t["status"] in ("MISMATCH", "MIXED"))
        self.assertEqual([], bad)


if __name__ == "__main__":
    unittest.main()
