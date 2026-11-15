#!/usr/bin/env python3
"""tools/rebalance_troops.py --main-weapon-slots (#343): a lance rider and a troop named for its
polearm open with that polearm, and the skills follow. Synthetic data only, no game install.

Run:  python -m unittest tools.tests.test_main_weapon_slots
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import rebalance_troops as rb  # noqa: E402
import troop_purpose as tp     # noqa: E402

CLASSES = {"sword": "OneHanded", "lance": "Polearm", "pike": "Polearm", "shield": "Shield",
           "greatsword": "TwoHanded", "bow": "Bow", "jav": "Throwing"}
COUCH = {"lance"}


def troop(tid="rider", name="Rider", group="Cavalry", loose="", rosters=None):
    rosters = rosters or [
        '      <EquipmentRoster civilian="true">\r\n'
        '        <equipment slot="Item0" id="Item.sword" />\r\n'
        '      </EquipmentRoster>\r\n',
        '      <EquipmentRoster>\r\n'
        '        <equipment slot="Item0" id="Item.sword" />\r\n'
        '        <equipment slot="Item1" id="Item.shield" />\r\n'
        '        <equipment slot="Item2" id="Item.lance" />\r\n'
        '        <equipment slot="Horse" id="Item.steed" />\r\n'
        '      </EquipmentRoster>\r\n',
        '      <EquipmentRoster>\r\n'
        '        <equipment slot="Item0" id="Item.greatsword" />\r\n'
        '        <equipment slot="Item1" id="Item.lance" />\r\n'
        '        <equipment slot="Horse" id="Item.steed" />\r\n'
        '      </EquipmentRoster>\r\n']
    return (f'  <NPCCharacter id="{tid}" level="21" default_group="{group}" name="{name}">\r\n'
            '    <skills>\r\n      <skill id="OneHanded" value="200" />\r\n'
            '      <skill id="Polearm" value="150" />\r\n    </skills>\r\n'
            f'    <Equipments>\r\n{loose}{"".join(rosters)}    </Equipments>\r\n  </NPCCharacter>\r\n')


def document(*troops):
    return ('﻿<?xml version="1.0" encoding="utf-8"?>\r\n<NPCCharacters>\r\n'
            + "".join(troops) + '</NPCCharacters>\r\n')


def read(path, tid="rider"):
    return next(n for n in ET.parse(path).getroot().findall("NPCCharacter") if n.get("id") == tid)


class SlotPlanFromXmlTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.path = Path(self._tmp.name) / "troops_x.xml"

    def tearDown(self):
        self._tmp.cleanup()

    def write(self, *troops):
        self.path.write_bytes(document(*troops).encode("utf-8"))

    def test_each_battle_roster_plans_one_exchange_and_the_civilian_one_never_moves(self):
        self.write(troop())
        kit, swaps, detail = rb.troop_kit_plan(read(self.path), CLASSES, fix_item0=True, couch_ids=COUCH)
        self.assertEqual(swaps, [(1, "Item2"), (2, "Item1")])
        self.assertEqual(detail["plan"].rule, "A")
        self.assertEqual(tp.main_class(kit), "Polearm")
        self.assertEqual(detail["before"][0], (1, {"Item0": "sword", "Item1": "shield", "Item2": "lance"}))
        self.assertEqual(detail["after"][0], (1, {"Item0": "lance", "Item1": "shield", "Item2": "sword"}))

    def test_a_troop_level_horse_row_mounts_every_set(self):
        rosters = ['      <EquipmentRoster>\r\n'
                   '        <equipment slot="Item0" id="Item.sword" />\r\n'
                   '        <equipment slot="Item1" id="Item.lance" />\r\n'
                   '      </EquipmentRoster>\r\n']
        self.write(troop(loose='      <equipment slot="Horse" id="Item.steed" />\r\n', rosters=rosters))
        _, swaps, detail = rb.troop_kit_plan(read(self.path), CLASSES, fix_item0=True, couch_ids=COUCH)
        self.assertEqual((detail["plan"].rule, swaps), ("A", [(0, "Item1")]))

    def test_a_cavalry_troop_with_no_horse_is_no_lancer(self):
        rosters = ['      <EquipmentRoster>\r\n'
                   '        <equipment slot="Item0" id="Item.sword" />\r\n'
                   '        <equipment slot="Item1" id="Item.lance" />\r\n'
                   '      </EquipmentRoster>\r\n']
        self.write(troop(rosters=rosters))
        _, swaps, detail = rb.troop_kit_plan(read(self.path), CLASSES, fix_item0=True, couch_ids=COUCH)
        self.assertEqual((detail["plan"], swaps), (tp.SlotPlan(), []))

    def test_the_name_or_the_id_makes_a_foot_soldier_a_polearm_troop(self):
        rosters = ['      <EquipmentRoster>\r\n'
                   '        <equipment slot="Item0" id="Item.sword" />\r\n'
                   '        <equipment slot="Item1" id="Item.pike" />\r\n'
                   '      </EquipmentRoster>\r\n']
        self.write(troop("pikeman_x", "Guard", "Infantry", rosters=rosters),
                   troop("plain", "Guard", "Infantry", rosters=rosters))
        by_id = {t: rb.troop_kit_plan(read(self.path, t), CLASSES, fix_item0=True, couch_ids=set())
                 for t in ("pikeman_x", "plain")}
        self.assertEqual(by_id["pikeman_x"][1], [(0, "Item1")])
        self.assertEqual(by_id["plain"][1], [])

    def test_without_couch_ids_nothing_is_planned(self):
        self.write(troop())
        _, swaps, detail = rb.troop_kit_plan(read(self.path), CLASSES, fix_item0=True)
        self.assertEqual((swaps, detail), ([], None))

    def test_the_writer_makes_the_exchange_and_a_second_pass_plans_nothing(self):
        self.write(troop())
        before = self.path.read_bytes()
        _, swaps, _ = rb.troop_kit_plan(read(self.path), CLASSES, fix_item0=True, couch_ids=COUCH)
        rb.apply_item0_swaps(str(self.path), {"rider": swaps})
        after = self.path.read_bytes()
        self.assertEqual(after, before.replace(b'slot="Item0" id="Item.sword" />\r\n        <equipment slot="Item1" id="Item.shield" />\r\n'
                                               b'        <equipment slot="Item2" id="Item.lance" />',
                                               b'slot="Item0" id="Item.lance" />\r\n        <equipment slot="Item1" id="Item.shield" />\r\n'
                                               b'        <equipment slot="Item2" id="Item.sword" />', 1)
                         .replace(b'slot="Item0" id="Item.greatsword" />\r\n        <equipment slot="Item1" id="Item.lance" />',
                                  b'slot="Item0" id="Item.lance" />\r\n        <equipment slot="Item1" id="Item.greatsword" />', 1))
        _, again, detail = rb.troop_kit_plan(read(self.path), CLASSES, fix_item0=True, couch_ids=COUCH)
        self.assertEqual((again, detail["plan"]), ([], tp.SlotPlan()))

    def test_a_planned_exchange_on_a_troop_level_row_is_left_and_reported(self):
        # The writer exchanges ids inside a roster; a weapon row shared by every roster has no roster to edit.
        rosters = ['      <EquipmentRoster>\r\n'
                   '        <equipment slot="Item1" id="Item.lance" />\r\n'
                   '        <equipment slot="Horse" id="Item.steed" />\r\n'
                   '      </EquipmentRoster>\r\n']
        self.write(troop(loose='      <equipment slot="Item0" id="Item.sword" />\r\n', rosters=rosters))
        _, swaps, detail = rb.troop_kit_plan(read(self.path), CLASSES, fix_item0=True, couch_ids=COUCH)
        self.assertEqual((swaps, detail["plan"].rule), ([], ""))
        self.assertIn("troop-level", detail["plan"].left)


class CouchableTests(unittest.TestCase):
    """One source says what can be couched: the priced usages (melee_catalogue.couchable_ids), read by
    taom_schema.couchable_item_ids over the Armory, vanilla and the placeholders, for the writer and the
    validator alike."""

    def test_a_priced_item_is_couchable_when_a_usage_carries_the_couch_feature(self):
        import melee_catalogue as mc
        def priced(*feature_sets):
            return mc.Priced(item=None, usages=tuple(
                mc.Usage("d", i == 0, f, "set", frozenset(), None) for i, f in enumerate(feature_sets)),
                blade_piece="", tier=1)
        got = mc.couchable_ids({
            "lance": priced(("onehanded_polearm",), ("polearm", "couch"), ("polearm", "bracing")),
            "spear": priced(("onehanded_polearm",), ("polearm", "bracing")),
            "sword": priced(("onehanded",)),
            "none": priced()})
        self.assertEqual(got, {"lance"})

    @unittest.skipUnless(os.path.isdir(rb.DEFAULT_GAME_MODULES), "needs the game install")
    def test_the_shared_source_reads_the_armory_vanilla_and_the_placeholders(self):
        import taom_schema as ts
        ids = ts.couchable_item_ids(Path(rb.DEFAULT_GAME_MODULES), Path(rb.MODULEDATA_DIR) / "placeholder_weapons")
        self.assertIn("wm_gundabad_spear_a03", ids)          # Armory
        self.assertIn("khuzait_lance_1_t3", ids)             # vanilla, which the validator does not price otherwise
        self.assertIn("taom_ph_gondor_spear_b_t5", ids)      # placeholder, priced from the repo's own sheet
        self.assertNotIn("wm_mordor_set1_sword_a02", ids)    # a sword
        self.assertEqual(rb.load_couchable_ids(rb.DEFAULT_GAME_MODULES), ids)


class SlotChoiceTests(unittest.TestCase):
    def test_with_two_couchable_polearms_the_first_slot_is_used(self):
        row = {"Item0": "OneHanded", "Item1": "Polearm", "Item2": "Polearm"}
        got = tp.main_weapon_slots([row], [frozenset({"Item1", "Item2"})], "Cavalry", True, False)
        self.assertEqual(got.swaps, ((0, "Item1"),))

    def test_one_set_that_needs_the_shield_exchange_and_one_that_needs_the_slot_rule(self):
        classes = {"sword": "OneHanded", "lance": "Polearm", "shield": "Shield"}
        rows = [{"Item0": "shield", "Item1": "lance", "Item2": "sword", "Horse": "h"},
                {"Item0": "sword", "Item1": "shield", "Item2": "lance", "Horse": "h"}]
        plan, p2, after = tp.troop_main_weapon(rows, classes, {"lance"}, "Cavalry", ("Rider", "rider"))
        self.assertEqual((plan.rule, plan.swaps, p2), ("A", ((1, "Item2"),), ["Item1", None]))
        self.assertEqual(after[0]["Item0"], "lance")

    def test_a_set_where_the_shield_exchange_moves_the_sword_and_the_lance_then_takes_item0(self):
        rosters = ('<EquipmentRoster><equipment slot="Item0" id="Item.shield" />'
                   '<equipment slot="Item1" id="Item.sword" /><equipment slot="Item2" id="Item.lance" />'
                   '<equipment slot="Horse" id="Item.h" /></EquipmentRoster>')
        npc = ET.fromstring('<NPCCharacter id="rider_x" name="Rider" default_group="Cavalry" level="21"><skills />'
                            f'<Equipments>{rosters}</Equipments></NPCCharacter>')
        _, swaps, detail = rb.troop_kit_plan(npc, dict(CLASSES, shield="Shield"), fix_item0=True, couch_ids={"lance"})
        self.assertEqual(swaps, [(0, "Item1"), (0, "Item2")])
        self.assertEqual(detail["after"][0][1]["Item0"], "lance")

    def test_the_display_name_decides_rule_b_when_the_id_does_not(self):
        rosters = ('<EquipmentRoster><equipment slot="Item0" id="Item.sword" />'
                   '<equipment slot="Item1" id="Item.pike" /></EquipmentRoster>')
        npc = ET.fromstring('<NPCCharacter id="x_guard" name="{=k}[Dale] Hill Spearman" default_group="Infantry" '
                            f'level="21"><skills /><Equipments>{rosters}</Equipments></NPCCharacter>')
        _, swaps, detail = rb.troop_kit_plan(npc, CLASSES, fix_item0=True, couch_ids=set())
        self.assertEqual((detail["plan"].rule, swaps), ("B", [(0, "Item1")]))


class SkillsFollowTests(unittest.TestCase):
    def test_the_purpose_pass_puts_polearm_on_top_and_a_second_pass_changes_nothing(self):
        before = tp.kit_of([{"Item0": "OneHanded", "Item1": "Shield", "Item2": "Polearm"}])
        after = tp.kit_of(tp.apply_slot_swaps([{"Item0": "OneHanded", "Item1": "Shield", "Item2": "Polearm"}],
                                              ((0, "Item2"),)))
        old = {s: 0 for s in rb.SKILL_NAMES}
        old.update(OneHanded=200, Polearm=150, TwoHanded=100)
        self.assertEqual(tp.kit_mismatches(old, before), [])
        self.assertNotEqual(tp.kit_mismatches(old, after), [])
        record = {"id": "rider", "file": "troops_x.xml", "name": "Rider", "level": 21, "group": "Cavalry",
                  "culture": "x", "old": old, "upgrades": [], "kit": after, "ladder": {},
                  "status": "UNCHANGED", "new": dict(old)}
        rb.plan_fix_purpose([record])
        self.assertEqual((record["final"]["Polearm"], record["final"]["OneHanded"]), (200, 150))
        again = dict(record, old=record["final"], new=dict(record["final"]))
        rb.plan_fix_purpose([again])
        self.assertEqual(again["final"], record["final"])


class SecondRootTests(unittest.TestCase):
    """The Fords of War roster is a second root (troop_purpose.EXTRA_TROOP_FILES): its troops get the
    slot rule and, when they change, their skills follow. The other troops of that file keep their
    skills, which the purpose pass has never reordered."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.path = Path(self._tmp.name) / "fords_troops.xml"
        self.path.write_bytes(document(
            troop("fow_lancer", "Lancer"),
            troop("fow_footman", "Footman", "Infantry", rosters=[
                '      <EquipmentRoster>\r\n        <equipment slot="Item0" id="Item.greatsword" />\r\n'
                '        <equipment slot="Item1" id="Item.sword" />\r\n      </EquipmentRoster>\r\n'])).encode("utf-8"))

    def tearDown(self):
        self._tmp.cleanup()

    def test_the_fords_file_is_the_documented_second_root(self):
        self.assertEqual(tp.EXTRA_TROOP_FILES, ("fords_of_war/fords_troops.xml",))
        self.assertTrue((Path(rb.MODULEDATA_DIR) / tp.EXTRA_TROOP_FILES[0]).is_file())

    def test_only_a_troop_the_slot_rule_changes_has_its_skills_reordered(self):
        records = rb.process_file(str(self.path), item_classes=CLASSES, purpose=True, couch_ids=COUCH,
                                  slots_only=True)
        rb.plan_fix_purpose(records)
        by_id = {r["id"]: r for r in records}
        lancer, footman = by_id["fow_lancer"], by_id["fow_footman"]
        self.assertEqual((lancer["final"]["Polearm"], lancer["final"]["OneHanded"]), (200, 150))
        self.assertEqual(footman["final"], {s: footman["old"].get(s, 0) for s in rb.SKILL_NAMES},
                         "a troop the rule leaves alone keeps its skills")

    def test_without_the_flag_the_whole_file_is_reordered_as_for_the_troop_files(self):
        records = rb.process_file(str(self.path), item_classes=CLASSES, purpose=True, couch_ids=COUCH)
        rb.plan_fix_purpose(records)
        footman = {r["id"]: r for r in records}["fow_footman"]
        self.assertNotEqual(footman["final"], {s: footman["old"].get(s, 0) for s in rb.SKILL_NAMES})


if __name__ == "__main__":
    unittest.main()
