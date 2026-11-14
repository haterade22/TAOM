#!/usr/bin/env python3
"""audit_troop_formation.py: a troop's default_group must match what its battle sets carry.

Run:  python -m unittest tools.tests.test_audit_troop_formation
"""
import os
import sys
import unittest
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


class ShippedDataTests(unittest.TestCase):
    """Every shipped troop's default_group matches its gear (needs the install for item types)."""

    def test_no_shipped_troop_mismatches_its_gear(self):
        modules = atf.game_modules(None)
        if modules is None or not Path(modules).is_dir():
            self.skipTest("BANNERLORD_GAME_DIR not set")
        bad = sorted(f"{t['id']} {t['group']} -> {t['expected'] or 'mixed sets'}"
                     for t in atf.scan(Path(modules)).values() if t["status"] in ("MISMATCH", "MIXED"))
        self.assertEqual([], bad)


if __name__ == "__main__":
    unittest.main()
