"""generate_troop_roster_page reads items by the catalogue's module rule (#1372).

NavalDLC never loads beside TAOM, and it redefines SandBoxCore items as bare stubs
(`northern_round_shield` with no Type). Read in folder order, the stub won and the page showed
an Umbar shield with no hit points.
"""
from __future__ import annotations

import shutil
import sys
import tempfile
import unittest
from types import SimpleNamespace
from unittest import mock
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import generate_troop_roster_page as page  # noqa: E402
import melee_catalogue as mc  # noqa: E402


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


@unittest.skipIf(mc.LET is None, "needs lxml")
class PlainItemTests(unittest.TestCase):
    def test_an_incompatible_modules_stub_does_not_replace_the_real_item(self):
        tmp = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        modules = tmp / "Modules"
        write(modules / "NavalDLC" / "ModuleData" / "items.xml", '<Items><Item id="round_shield"/></Items>\n')
        write(modules / "SandBoxCore" / "ModuleData" / "shields.xml",
              '<Items><Item id="round_shield" Type="Shield"/></Items>\n')
        own = tmp / "own" / "SubModule.xml"
        write(own, '<Module><Id value="TAOM"/><DependedModules><DependedModule Id="SandBoxCore"/>'
                   '</DependedModules><IncompatibleModules><Module Id="NavalDLC"/>'
                   '</IncompatibleModules></Module>\n')
        old = mc.TAOM_SUBMODULE
        mc.TAOM_SUBMODULE = own
        self.addCleanup(setattr, mc, "TAOM_SUBMODULE", old)
        self.assertEqual(page.load_plain_items(modules)["round_shield"].type, "Shield")


    def test_a_multiplayer_only_item_file_is_left_out(self):
        """#1398: Native's mpitems.xml loads only in multiplayer; its stub must not replace the item."""
        tmp = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        modules = tmp / "Modules"
        write(modules / "Native" / "SubModule.xml",
              '<Module><Id value="Native"/><Xmls><XmlNode><XmlName id="Items" path="mpitems"/>'
              '<IncludedGameTypes><GameType value="MultiplayerGame"/></IncludedGameTypes></XmlNode>'
              '</Xmls></Module>\n')
        write(modules / "Native" / "ModuleData" / "mpitems.xml", '<Items><Item id="round_shield"/></Items>\n')
        write(modules / "SandBoxCore" / "ModuleData" / "shields.xml",
              '<Items><Item id="round_shield" Type="Shield"/></Items>\n')
        own = tmp / "own" / "SubModule.xml"
        write(own, '<Module><Id value="TAOM"/><DependedModules><DependedModule Id="SandBoxCore"/>'
                   '</DependedModules></Module>\n')
        old = mc.TAOM_SUBMODULE
        mc.TAOM_SUBMODULE = own
        self.addCleanup(setattr, mc, "TAOM_SUBMODULE", old)
        self.assertEqual(page.load_plain_items(modules)["round_shield"].type, "Shield")


@unittest.skipIf(mc.LET is None, "needs lxml")
class WeaponIndexTests(unittest.TestCase):
    """#1384: the placeholder weapons are priced from the tree's placeholder_weapons/ folder, as the
    validator prices them (taom_schema.price_placeholders), so a carrier's slot is never "not found"."""

    def setUp(self):
        tmp = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        self.ph_dir = tmp / "placeholder_weapons"
        write(self.ph_dir / "ph_items.xml",
              '<Items><CraftedItem id="taom_ph_spear" name="{=k}Placeholder Spear" crafting_template="Spear">'
              '<Pieces><Piece id="b" Type="Blade"/></Pieces></CraftedItem>'
              '<CraftedItem id="taom_ph_broken" name="{=k}Broken Spear" crafting_template="Spear">'
              '<Pieces><Piece id="b" Type="Blade"/></Pieces></CraftedItem></Items>\n')
        self.cat = SimpleNamespace(items={})

    def priced(self, iid, name):
        return SimpleNamespace(item=SimpleNamespace(name=name, template="Spear"), swing_damage=0,
                               thrust_damage=40, best_dps=12.5)

    def test_placeholders_are_priced_and_unpriced_ones_keep_their_name(self):
        with mock.patch.object(page.mc, "price_all", return_value=({}, [])), \
                mock.patch.object(page.ts, "price_placeholders", return_value=(
                    {"taom_ph_spear": self.priced("taom_ph_spear", "Placeholder Spear")},
                    [("ph_items.xml", "taom_ph_broken", "no description")])) as ph:
            items: dict = {}
            counts = page.index_weapons(items, self.cat, self.ph_dir)
        ph.assert_called_once_with(self.cat, self.ph_dir)
        self.assertTrue(items["taom_ph_spear"].melee)
        self.assertEqual(items["taom_ph_spear"].thrust_damage, 40)
        self.assertEqual(items["taom_ph_broken"].name, "Broken Spear")
        self.assertEqual(counts, (1, 1))

    def test_an_unreadable_placeholder_file_is_counted_not_fatal(self):
        """price_placeholders reports a file that will not parse; the page must not crash on it."""
        write(self.ph_dir / "ph_items.xml", "<Items><Crafted")
        with mock.patch.object(page.mc, "price_all", return_value=({}, [])):
            items: dict = {}
            counts = page.index_weapons(items, self.cat, self.ph_dir)
        self.assertEqual(counts, (0, 1))
        self.assertEqual(items, {})

    def test_a_tree_without_placeholders_indexes_only_the_catalogue(self):
        with mock.patch.object(page.mc, "price_all", return_value=({}, [])):
            items: dict = {}
            counts = page.index_weapons(items, self.cat, self.ph_dir.parent / "none")
        self.assertEqual(counts, (0, 0))
        self.assertEqual(items, {})


@unittest.skipIf(mc.LET is None, "needs lxml")
class TroopSetTests(unittest.TestCase):
    def test_civilian_rosters_are_not_battle_sets(self):
        """The engine reads civilian="true" as Civilian when no equipmentType is given
        (analyze_melee_ladder._is_civilian_roster); the page counted those as battle sets."""
        tmp = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, tmp, ignore_errors=True)
        write(tmp / "troops_test.xml",
              '<NPCCharacters><NPCCharacter id="t" name="T" level="6" culture="Culture.c"><Equipments>'
              '<EquipmentRoster><equipment slot="Item0" id="Item.battle"/></EquipmentRoster>'
              '<EquipmentRoster civilian="true"><equipment slot="Item0" id="Item.town"/></EquipmentRoster>'
              '<EquipmentRoster equipmentType="Civilian"><equipment slot="Item0" id="Item.town2"/></EquipmentRoster>'
              '<EquipmentRoster civilian="true" equipmentType="Battle"><equipment slot="Item0" id="Item.typed"/>'
              '</EquipmentRoster></Equipments></NPCCharacter></NPCCharacters>\n')
        (troop,) = page.load_troops_full(tmp)
        self.assertEqual([s.slots["Item0"] for s in troop.sets], ["battle", "typed"])


if __name__ == "__main__":
    unittest.main()
