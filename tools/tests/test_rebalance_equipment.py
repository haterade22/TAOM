#!/usr/bin/env python3
"""Unit tests for equipment-driven weapon specialization in
tools/rebalance_troops.py + the item-class registry in tools/taom_schema.py.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_rebalance_equipment.py

Synthetic data only (no game install). Encodes the fix contract for the
skill-vs-equipment mismatch bug class: crossbowmen named "Sharpshooter" got
Bow-top skills and two-hander troops named "Knight" got Polearm-top skills,
because detection was name-keyword-only.
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import rebalance_troops as rb  # noqa: E402
import taom_schema as ts       # noqa: E402


class ItemClassRegistryTests(unittest.TestCase):
    """build_item_class_registry must read BOTH vanilla <Item Type="..."> and
    Armory <CraftedItem crafting_template="..."> (no two-hander anywhere in the
    install uses a Type attribute)."""

    def test_parses_both_item_shapes(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp) / "ModuleData"
            md.mkdir(parents=True)
            (md / "items.xml").write_text("""<?xml version="1.0"?>
<Items>
  <Item id="vanilla_xbow" Type="Crossbow" name="x" />
  <Item
      id="vanilla_polearm"
      Type="Polearm" />
  <CraftedItem id="taom_greatsword" crafting_template="TwoHandedSword" culture="Culture.gondor" />
  <CraftedItem id="taom_pike" crafting_template="Pike" />
  <Item id="some_helmet" Type="HeadArmor" />
  <!-- <Item id="commented_out" Type="Bow" /> -->
</Items>
""", encoding="utf-8")
            classes = ts.build_item_class_registry(md, None)
        self.assertEqual(classes.get("vanilla_xbow"), "Crossbow")
        self.assertEqual(classes.get("vanilla_polearm"), "Polearm")
        self.assertEqual(classes.get("taom_greatsword"), "TwoHanded")
        self.assertEqual(classes.get("taom_pike"), "Polearm")
        self.assertNotIn("some_helmet", classes, "non-weapon types must be omitted")
        self.assertNotIn("commented_out", classes, "commented defs must be ignored")


def _npc(equipment_ids):
    """Minimal NPCCharacter element with the given Item-slot equipment ids."""
    eq_lines = "".join(
        f'<equipment slot="Item{i}" id="Item.{iid}" />' for i, iid in enumerate(equipment_ids))
    xml = f"""<NPCCharacter id="t" name="T" level="31" default_group="Ranged">
  <Equipments><EquipmentRoster>{eq_lines}
    <equipment slot="Head" id="Item.helmet_a" />
  </EquipmentRoster></Equipments>
</NPCCharacter>"""
    return ET.fromstring(xml)


ITEM_CLASSES = {
    "xbow_a": "Crossbow", "bolt_a": "Bolts", "bow_a": "Bow", "arrows_a": "Arrows",
    "sword_1h": "OneHanded", "sword_2h": "TwoHanded", "spear_a": "Polearm",
    "javelin_a": "Throwing", "shield_a": "Shield",
}


class WeaponClassExtractionTests(unittest.TestCase):
    def test_collects_weapon_slots_only(self):
        npc = _npc(["xbow_a", "bolt_a", "sword_1h"])
        wc = rb.troop_weapon_classes(npc, ITEM_CLASSES)
        self.assertEqual(wc, {"Crossbow", "Bolts", "OneHanded"})


class CrossbowSwapTests(unittest.TestCase):
    """The Bow<->Crossbow swap must fire on EQUIPMENT, not name."""

    def test_sharpshooter_with_crossbow_swaps(self):
        # The reported bug: "Sharpshooter" (no crossbow keyword) carrying a crossbow.
        skills = rb.calculate_skills(
            "gondor", 31, "Ranged", "tol_sharpshooter", "Tolfalas Sharpshooter",
            weapon_classes={"Crossbow", "Bolts", "OneHanded"})
        self.assertGreater(skills["Crossbow"], skills["Bow"],
                           "crossbow-armed troop must be Crossbow-top")

    def test_crossbowman_named_troop_with_bow_does_not_swap(self):
        # Name says crossbow, equipment says bow -> equipment wins, no swap.
        skills = rb.calculate_skills(
            "gondor", 31, "Ranged", "x_crossbowman", "Crossbowman",
            weapon_classes={"Bow", "Arrows"})
        self.assertGreater(skills["Bow"], skills["Crossbow"])

    def test_name_fallback_without_equipment_data(self):
        # weapon_classes=None (no game install) -> name keywords still work.
        skills = rb.calculate_skills(
            "gondor", 31, "Ranged", "x_crossbowman", "Crossbowman", weapon_classes=None)
        self.assertGreater(skills["Crossbow"], skills["Bow"])

    def test_naffatun_keyword_removed_from_fallback(self):
        # 'naffatun' wrongly swapped javelin throwers; it must not fire by name.
        skills = rb.calculate_skills(
            "rhun", 31, "Ranged", "sagarun_naffatun", "Naffatun",
            weapon_classes={"OneHanded", "Throwing"})
        self.assertGreater(skills["Bow"], skills["Crossbow"])


class MeleeSanitySwapTests(unittest.TestCase):
    """A two-hander-only troop must not have Polearm as top melee skill."""

    def test_knight_with_two_hander_swaps(self):
        # The reported bug: cavalry "Hill-Knight" carrying only a 2H sword.
        base = rb.CAVALRY_BASELINES[41]
        self.assertGreater(base["Polearm"], base["TwoHanded"],
                           "precondition: cavalry baseline is polearm-biased")
        skills = rb.calculate_skills(
            "gondor", 41, "Cavalry", "arn_hill_knight", "Arndir Hill-Knight",
            weapon_classes={"TwoHanded"})
        self.assertGreater(skills["TwoHanded"], skills["Polearm"])

    def test_swap_is_total_preserving(self):
        with_swap = rb.calculate_skills(
            "gondor", 41, "Cavalry", "arn_hill_knight", "Arndir Hill-Knight",
            weapon_classes={"TwoHanded"})
        without = rb.calculate_skills(
            "gondor", 41, "Cavalry", "arn_hill_knight", "Arndir Hill-Knight",
            weapon_classes=None)
        self.assertEqual(sorted(with_swap.values()), sorted(without.values()))

    def test_mixed_carrier_untouched(self):
        # Carries both a 2H and a polearm -> baseline ordering stands.
        skills = rb.calculate_skills(
            "gondor", 41, "Cavalry", "t", "Mixed Guard",
            weapon_classes={"TwoHanded", "Polearm"})
        self.assertGreater(skills["Polearm"], skills["TwoHanded"])

    def test_polearm_carrier_untouched(self):
        skills = rb.calculate_skills(
            "gondor", 41, "Cavalry", "t", "Lancer",
            weapon_classes={"Polearm", "OneHanded", "Shield"})
        self.assertGreater(skills["Polearm"], skills["TwoHanded"])

    def test_idempotent(self):
        # The swap condition (Polearm > TwoHanded) is false after swapping, so
        # recomputing from the same inputs yields the same output.
        a = rb.calculate_skills("gondor", 41, "Cavalry", "t", "Knight",
                                weapon_classes={"TwoHanded"})
        b = rb.calculate_skills("gondor", 41, "Cavalry", "t", "Knight",
                                weapon_classes={"TwoHanded"})
        self.assertEqual(a, b)
        self.assertGreater(a["TwoHanded"], a["Polearm"])


if __name__ == "__main__":
    unittest.main()
