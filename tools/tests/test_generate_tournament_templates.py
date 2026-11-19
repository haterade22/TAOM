#!/usr/bin/env python3
"""Unit tests for tools/generate_tournament_templates.py, on synthetic troops (#422).

Run:  python -m unittest tools.tests.test_generate_tournament_templates

The mission reads roster i of a template for team member i, so a template with fewer rosters than the team
crashes the arena; the traps pinned here: roster counts per team size, weapon slots only (the fighter's own
armour fills slots 5-9), arrows and bolts swapped for the blunt tournament ones with no second quiver, a
launcher without its ammunition dropped, a Horse written under <Equipments> joining every roster, a banned
(creature or dwarf-ram) mount dropping the kit, a class the culture lacks falling back instead of failing,
and the shipped file matching a fresh run when the game install is there.
"""
import collections
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_tournament_templates as gtt  # noqa: E402

CLASSES = {
    "sword": "OneHanded", "axe": "OneHanded", "greatsword": "TwoHanded", "spear": "Polearm",
    "shield": "Shield", "bow": "Bow", "xbow": "Crossbow", "arrow": "Arrows", "arrow2": "Arrows", "bolt": "Bolts",
    "javelin": "Throwing",
}


def npc(culture, *rosters, shared=()):
    """An NPCCharacter element; each roster is a list of (slot, id), shared rows sit directly under Equipments."""
    root = ET.Element("NPCCharacter", id="t", culture="Culture." + culture)
    eq = ET.SubElement(root, "Equipments")
    for rows in rosters:
        ro = ET.SubElement(eq, "EquipmentRoster")
        for slot, item in rows:
            ET.SubElement(ro, "equipment", slot=slot, id="Item." + item)
    for slot, item in shared:
        ET.SubElement(eq, "equipment", slot=slot, id="Item." + item)
    return root


def kit(rows, banned=frozenset()):
    return gtt.classify([(s, "Item." + i) for s, i in rows], CLASSES, banned)


class ClassifyTests(unittest.TestCase):
    def test_sword_and_shield_is_onehand(self):
        slots, cls = kit([("Item0", "sword"), ("Item1", "shield"), ("Body", "x")])
        self.assertEqual(cls, "onehand")
        self.assertEqual(slots, (("Item0", "Item.sword"), ("Item1", "Item.shield")))

    def test_lone_onehander_is_onehand_two_hander_and_polearm_are_twohand(self):
        self.assertEqual(kit([("Item0", "axe")])[1], "onehand")
        self.assertEqual(kit([("Item0", "greatsword")])[1], "twohand")
        self.assertEqual(kit([("Item0", "spear"), ("Item1", "javelin")])[1], "twohand")
        self.assertEqual(kit([("Item0", "spear"), ("Item1", "shield")])[1], "onehand")

    def test_shield_beside_two_hander_is_no_kit(self):
        self.assertIsNone(kit([("Item0", "greatsword"), ("Item1", "shield")]))
        self.assertIsNotNone(kit([("Item0", "spear"), ("Item1", "shield")]))

    def test_bow_gets_the_blunt_arrows_and_loses_the_second_quiver(self):
        slots, cls = kit([("Item0", "bow"), ("Item1", "arrow"), ("Item2", "arrow2"), ("Item3", "sword")])
        self.assertEqual(cls, "archer")
        self.assertEqual(slots, (("Item0", "Item.bow"), ("Item1", "Item.tournament_arrows"), ("Item2", "Item.sword")))

    def test_crossbow_gets_blunt_bolts(self):
        slots, _ = kit([("Item0", "xbow"), ("Item1", "bolt")])
        self.assertEqual(slots[1], ("Item1", "Item.tournament_bolts"))

    def test_launcher_without_its_ammunition_is_no_kit(self):
        self.assertIsNone(kit([("Item0", "bow"), ("Item1", "sword")]))
        self.assertIsNone(kit([("Item0", "bow"), ("Item1", "bolt")]))

    def test_unclassed_weapon_slot_item_is_no_kit(self):
        self.assertIsNone(kit([("Item0", "sword"), ("Item1", "a_banner")]))

    def test_mounted_keeps_horse_and_harness_and_ignores_banned_mounts(self):
        rows = [("Item0", "spear"), ("Horse", "steed"), ("HorseHarness", "barding")]
        slots, cls = kit(rows)
        self.assertEqual(cls, "mounted")
        self.assertEqual(slots[-2:], (("Horse", "Item.steed"), ("HorseHarness", "Item.barding")))
        self.assertIsNone(kit(rows, banned=frozenset({"steed"})))

    def test_weapon_slots_close_up_from_item0(self):
        slots, _ = kit([("Item1", "sword"), ("Item3", "shield")])
        self.assertEqual([s for s, _ in slots], ["Item0", "Item1"])

    def test_only_a_shield_or_only_ammunition_is_no_kit(self):
        self.assertIsNone(kit([("Item0", "shield")]))
        self.assertIsNone(kit([("Item0", "arrow")]))


class RosterTests(unittest.TestCase):
    def test_civilian_rosters_are_skipped_and_shared_horse_joins_every_roster(self):
        troop = npc("x", [("Item0", "sword")], [("Item0", "axe"), ("Horse", "other")], shared=[("Horse", "steed")])
        troop.find("Equipments").insert(0, ET.Element("EquipmentRoster", civilian="true"))
        rosters = gtt.troop_rosters(troop)
        self.assertEqual(len(rosters), 2)
        self.assertIn(("Horse", "Item.steed"), rosters[0])
        self.assertEqual([r for r in rosters[1] if r[0] == "Horse"], [("Horse", "Item.other")])


class RankTests(unittest.TestCase):
    def test_most_carried_first_and_main_weapons_spread(self):
        a = (("Item0", "Item.sword"), ("Item1", "Item.shield"))
        b = (("Item0", "Item.sword"), ("Item1", "Item.shield"), ("Item2", "Item.javelin"))
        c = (("Item0", "Item.axe"),)
        counts = collections.Counter({(a, "onehand"): 5, (b, "onehand"): 4, (c, "onehand"): 1})
        ranked = gtt.rank_variants(counts)["onehand"]
        self.assertEqual(ranked, [a, c, b])


def variants(**by_class):
    return {cls: [(("Item0", "Item." + w),) for w in ws] for cls, ws in by_class.items()}


class TemplateTests(unittest.TestCase):
    def build(self, **by_class):
        return gtt.build_templates("gondor", variants(**by_class))

    def test_three_sets_per_size_with_one_two_four_rosters(self):
        templates = self.build(onehand=["sword", "axe"], twohand=["greatsword"], archer=["bow"], mounted=["spear"])
        self.assertEqual(len(templates), 9)
        sizes = collections.Counter()
        for tid, rosters in templates:
            size = tid.split("_")[3]
            sizes[size] += 1
            self.assertEqual(len(rosters), gtt.TEAM_SIZE[size], tid)
        self.assertEqual(dict(sizes), {"one": 3, "two": 3, "four": 3})
        self.assertEqual([t for t, _ in templates][0], "taom_tournament_gondor_one_participant_set_v1")

    def test_a_culture_without_mounts_or_bows_falls_back_and_still_fills_every_roster(self):
        templates = self.build(onehand=["sword"])
        self.assertEqual(sum(len(r) for _, r in templates), 3 * 1 + 3 * 2 + 3 * 4)
        self.assertTrue(all(slots == (("Item0", "Item.sword"),) for _, rosters in templates for slots in rosters))

    def test_picks_rotate_through_the_variants(self):
        templates = dict(self.build(onehand=["sword", "axe", "mace"], twohand=["greatsword"]))
        firsts = [templates[f"taom_tournament_gondor_one_participant_set_v{i}"][0][0][1] for i in (1, 3)]
        self.assertNotEqual(firsts[0], firsts[1])

    def test_no_usable_kit_is_an_error_not_an_empty_template(self):
        with self.assertRaises(ValueError):
            gtt.build_templates("gondor", {})
        with self.assertRaises(gtt.NoUsableKit) as ctx:
            gtt.generate({"gondor": [npc("gondor", [("Body", "x")])]}, CLASSES, ["gondor"])
        self.assertIn("gondor", str(ctx.exception))
        self.assertEqual(ctx.exception.culture, "gondor")

    def test_kits_come_from_the_cultures_own_troops_or_its_named_sources(self):
        troops = {
            "khuzait": [npc("khuzait", [("Item0", "sword"), ("Item1", "shield")])],
            "battania": [npc("battania", [("Item0", "axe")])],
        }
        own = gtt.culture_kits("khuzait", troops, CLASSES)
        khand = gtt.culture_kits("battania", troops, CLASSES)
        self.assertEqual({c for _, c in own}, {"onehand"})
        self.assertEqual(list(khand), list(own), "Khand (battania) uses the Rhun (khuzait) roster, as spcultures.xslt says")

    def test_abanissa_kits_come_from_the_harad_aserai_troops_not_shaghana(self):
        troops = {
            "aserai": [npc("aserai", [("Item0", "sword"), ("Item1", "shield")])],
            "shaghana": [npc("shaghana", [("Item0", "greatsword")])],
        }
        kits = gtt.culture_kits("abanissa", troops, CLASSES)
        self.assertEqual(list(kits), list(gtt.culture_kits("aserai", troops, CLASSES)))
        self.assertEqual({c for _, c in kits}, {"onehand"})

    def test_orc_cultures_add_the_goblin_troops_to_their_own(self):
        troops = {
            "goblin": [npc("goblin", [("Item0", "sword")])],
            "bluecraig": [npc("bluecraig", [("Item0", "bow"), ("Item1", "arrow")])],
            "mistymountainorcs": [npc("mistymountainorcs", [("Item0", "greatsword")])],
        }
        self.assertEqual(gtt.KIT_SOURCES["bluecraig"], ("goblin", "bluecraig"))
        self.assertEqual(gtt.KIT_SOURCES["mistymountainorcs"], ("goblin", "mistymountainorcs"))
        self.assertEqual({c for _, c in gtt.culture_kits("bluecraig", troops, CLASSES)}, {"onehand", "archer"})
        self.assertEqual({c for _, c in gtt.culture_kits("mistymountainorcs", troops, CLASSES)}, {"onehand", "twohand"})

    def test_town_cultures_lists_cultures_with_templates_then_the_renamed_six(self):
        import tempfile
        from pathlib import Path
        xml = ("<Cultures><Culture id='gondor'><tournament_team_templates_one_participant/></Culture>"
               "<Culture id='harad_bandits'/></Cultures>")
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "sp.xml"
            path.write_text(xml, encoding="utf-8")
            self.assertEqual(gtt.town_cultures(path), ["gondor"] + list(gtt.RENAMED))


class RenderTests(unittest.TestCase):
    def test_rendered_file_parses_and_holds_weapon_slots_only(self):
        templates = {"gondor": gtt.build_templates("gondor", variants(onehand=["sword"], archer=["bow"]))}
        root = ET.fromstring(gtt.render(templates).encode("utf-8"))
        chars = root.findall("NPCCharacter")
        self.assertEqual(len(chars), 9)
        for ch in chars:
            self.assertEqual(ch.get("culture"), "Culture.gondor")
            self.assertEqual(ch.get("level"), "6")
            self.assertEqual(ch.get("occupation"), "NotAssigned")
            for e in ch.iter("equipment"):
                self.assertIn(e.get("slot"), gtt.WEAPON_SLOTS + ("Horse", "HorseHarness"))
            civilian = ch.find("Equipments").find("EquipmentSet")
            self.assertEqual(civilian.get("equipmentType"), "Civilian")
        self.assertEqual(len({c.get("id") for c in chars}), 9)


    def test_human_cultures_get_their_twins_civilian_set_and_dwarves_do_not(self):
        for culture, twin in (("aserai", "aserai"), ("gondor", "empire")):
            root = ET.fromstring(gtt.render({culture: gtt.build_templates(culture, variants(onehand=["sword"]))}).encode("utf-8"))
            for ch in root.findall("NPCCharacter"):
                civilian = ch.find("Equipments").find("EquipmentSet")
                self.assertEqual(civilian.get("id"), f"npc_wanderer_equipment_template_{twin}_civilian", culture)
        dwarves = ET.fromstring(gtt.render({"erebor": gtt.build_templates("erebor", variants(onehand=["sword"]))}).encode("utf-8"))
        self.assertNotIn("EquipmentSet", {e.tag for e in dwarves.iter()})


class DwarfRenderTests(unittest.TestCase):
    def test_dwarf_cultures_are_dwarves_with_an_inline_dwarf_civilian_roster(self):
        templates = {"erebor": gtt.build_templates("erebor", variants(onehand=["sword"]))}
        root = ET.fromstring(gtt.render(templates).encode("utf-8"))
        for ch in root.findall("NPCCharacter"):
            self.assertEqual(ch.get("race"), "dwarf")
            self.assertIsNone(ch.find("Equipments").find("EquipmentSet"))
            civilian = [r for r in ch.find("Equipments").findall("EquipmentRoster") if r.get("civilian") == "true"]
            self.assertEqual(len(civilian), 1)


class ShippedFileTests(unittest.TestCase):
    def test_shipped_file_matches_a_fresh_run(self):
        from _gamedir import game_modules
        modules = game_modules(gtt.DEFAULT_GAME_ROOT)
        if not (modules / "LOTRLOME_Armory").is_dir():
            self.skipTest("game install not present: %s" % modules)
        self.assertEqual(gtt.main(["--check"]), 0, "run: python tools/generate_tournament_templates.py --apply")


if __name__ == "__main__":
    unittest.main()
