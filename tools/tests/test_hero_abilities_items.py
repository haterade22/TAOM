#!/usr/bin/env python
"""The hero abilities' own item file (Main/_Module/ModuleData/hero_abilities/hero_abilities_items.xml): Legolas's extra arrow (#1361).

Mission.AddCustomMissile adds no bow damage (Mission.cs:4954-4980, damage bonus 0f), so the extra arrow carries the bow's thrust
plus the arrow's own: this test reads the bow and the arrow Legolas carries (taom_mission_heroes.xml) from the installed Armory, so
a retuned or swapped bow or arrow fails here. validate_moduledata.py errors on a missing body and only warns on a missing visual
mesh; this test fails on either.

Run:  python -m unittest tools.tests.test_hero_abilities_items
"""
import os
import re
import subprocess
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ITEMS_DIR = REPO / "Main" / "_Module" / "ModuleData" / "hero_abilities"
HEROES_XML = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "taom_mission_heroes.xml"
GAME_DIR = Path(os.environ.get("TAOM_GAME_DIR", r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
ARMORY_WEAPONS = GAME_DIR / "Modules" / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_weapons.xml"
LEGOLAS = "taom_hero_legolas"


def _item(text, item_id):
    match = re.search(r'<Item\b[^>]*?\bid="' + re.escape(item_id) + r'".*?</Item>', text, re.S)
    if match is None:
        raise AssertionError(f"item {item_id} not found")
    return match.group(0)


def _thrust(item_xml):
    return int(re.search(r'thrust_damage="(\d+)"', item_xml).group(1))


def _carried(character_id, slot):
    """The item id in one equipment slot of a character's first roster, without the "Item." prefix."""
    root = ET.parse(HEROES_XML).getroot()
    character = next((c for c in root.iter("NPCCharacter") if c.get("id") == character_id), None)
    if character is None:
        raise AssertionError(f"{character_id} not found in {HEROES_XML.name}")
    roster = character.find("Equipments/EquipmentRoster")
    if roster is None:
        raise AssertionError(f"{character_id} has no EquipmentRoster in {HEROES_XML.name}")
    item = next((e.get("id", "") for e in roster.iter("equipment") if e.get("slot") == slot), None)
    if not item:
        raise AssertionError(f"{character_id} has no {slot} in his first EquipmentRoster ({HEROES_XML.name})")
    return item[len("Item."):] if item.startswith("Item.") else item


class LegolasArrowItemTests(unittest.TestCase):
    def setUp(self):
        self.text = (ITEMS_DIR / "hero_abilities_items.xml").read_text(encoding="utf-8")

    def test_the_item_file_holds_the_one_extra_arrow_that_cannot_be_picked_up(self):
        self.assertEqual(re.findall(r'<Item\s+id="([^"]+)"', self.text), ["taom_legolas_arrow"])
        self.assertEqual(self.text.count('<Flags CannotBePickedUp="true" />'), 1, "a stuck extra arrow is not a pickup (SpawnedItemEntity.cs:163-164)")
        arrow = _item(self.text, "taom_legolas_arrow")
        self.assertIn('name="{=gFtu0j4T}Arrows"', arrow, "vanilla's name: the item adds no string")
        self.assertIn('Type="Arrows"', arrow)
        self.assertIn('weapon_class="Arrow"', arrow)
        self.assertIn('is_merchandise="false"', arrow)

    @unittest.skipUnless(ARMORY_WEAPONS.is_file(), "no installed Armory to read the bow and the arrow from (a laptop with a partial install, or no game)")
    def test_thrust_is_the_armory_bow_plus_its_arrow(self):
        bow, arrow = _carried(LEGOLAS, "Item0"), _carried(LEGOLAS, "Item1")
        armory = ARMORY_WEAPONS.read_text(encoding="utf-8", errors="replace")
        want = _thrust(_item(armory, bow)) + _thrust(_item(armory, arrow))
        self.assertEqual(_thrust(_item(self.text, "taom_legolas_arrow")), want, f"his bow {bow} plus his arrow {arrow}")

    @unittest.skipUnless((GAME_DIR / "Modules").is_dir(), "no installed game to read the meshes from (a laptop with a partial install, or no game)")
    def test_the_meshes_the_arrow_names_exist_in_the_installed_modules(self):
        done = subprocess.run(
            [sys.executable, "-I", str(REPO / "tools" / "validate_mesh_refs.py"), "--items", str(ITEMS_DIR), "--game", str(GAME_DIR), "--no-rgl-log", "--tpac-modules", "LOTRLOME_Armory", "Native"],
            capture_output=True, text=True, timeout=600)

        self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
        self.assertIn("Tier B missing visual meshes: 0", done.stdout)
        self.assertIn("Tier C missing collision bodies: 0", done.stdout)
        # The two zero lines are printed whatever ran; this one proves the packs were read and every mesh was found.
        self.assertRegex(done.stdout, r"unique visual meshes present \(Tier B\): 3/3")


if __name__ == "__main__":
    unittest.main()
