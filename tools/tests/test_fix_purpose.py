#!/usr/bin/env python3
"""tools/rebalance_troops.py --fix-purpose (#343): reorder a troop's own skills to its kit, put a real
weapon in Item0, then clamp the upgrade tree. Synthetic data only, no game install.

Run:  python -m unittest tools.tests.test_fix_purpose
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

SKILLS = rb.SKILL_NAMES


def rec(tid, kit, upgrades=(), ladder=None, **vals):
    old = {s: vals.get(s, 0) for s in SKILLS}
    return {"id": tid, "file": "troops_x.xml", "name": tid, "level": 21, "group": "Infantry",
            "culture": "x", "old": old, "upgrades": list(upgrades), "kit": kit,
            "ladder": ladder or {}, "status": "UNCHANGED", "new": dict(old)}


SWORD = tp.kit_of([{"Item0": "OneHanded", "Item1": "Shield"}])
TWO_HANDER = tp.kit_of([{"Item0": "TwoHanded", "Item1": "OneHanded"}])
ARCHER = tp.kit_of([{"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}])


class SettlePurposeTests(unittest.TestCase):
    def test_a_respecialising_edge_raises_the_main_class_to_the_clamped_value(self):
        # A swordsman upgrading to a two-hander hands the child OneHanded 250 through the clamp,
        # above the child's own TwoHanded 220: the main class is raised to tie, never lowered.
        parent = rec("sword", SWORD, upgrades=["great"], OneHanded=250, Polearm=150, TwoHanded=120)
        child = rec("great", TWO_HANDER, OneHanded=150, TwoHanded=220, Polearm=140)
        log, raised = rb.settle_purpose([parent, child])
        final = child["final"]
        self.assertEqual(final["OneHanded"], 250)
        self.assertEqual(final["TwoHanded"], 250)
        self.assertEqual(tp.kit_mismatches(final, TWO_HANDER), [])
        self.assertIn(("sword", "great", "TwoHanded", 220, 250), log)
        # The clamp raised OneHanded and Polearm; TwoHanded is the purpose raise, counted apart.
        self.assertEqual((raised, child["clamp_lifted"], parent["clamp_lifted"]),
                         (2, ["OneHanded", "Polearm"], []))

    def test_a_restat_child_on_a_respecialising_edge_keeps_its_main_class_on_top(self):
        # --fix-monotonicity --restat: the parent stays on disk (its formula row is ignored), the
        # child takes its formula row, and the clamp must not leave its two-hander under the sword.
        parent = rec("sword", SWORD, upgrades=["great"], OneHanded=250, Polearm=150, TwoHanded=120)
        parent["new"] = dict(parent["old"], OneHanded=400)
        child = rec("great", TWO_HANDER, OneHanded=10, TwoHanded=10)
        child["new"] = dict(child["old"], OneHanded=150, TwoHanded=220, Polearm=140)
        rb.settle_purpose([parent, child], base_on_curve=False, restat_ids={"great"})
        self.assertEqual(parent["final"]["OneHanded"], 250)
        self.assertEqual((child["final"]["OneHanded"], child["final"]["TwoHanded"]), (250, 250))
        self.assertEqual(tp.kit_mismatches(child["final"], TWO_HANDER), [])

    def test_the_clamp_only_mode_raises_no_troop_the_clamp_left_alone(self):
        # A troop out of order on disk with no parent is the report's to show, not a repair.
        lone = rec("lone", SWORD, OneHanded=100, Polearm=150)
        lone["new"] = dict(lone["old"], OneHanded=300)
        self.assertEqual(rb.settle_purpose([lone], base_on_curve=False), ([], 0))
        self.assertEqual(lone["final"], lone["old"])

    def test_settling_twice_changes_nothing(self):
        parent = rec("sword", SWORD, upgrades=["great"], OneHanded=250, Polearm=150, TwoHanded=120)
        child = rec("great", TWO_HANDER, OneHanded=150, TwoHanded=220, Polearm=140)
        rb.settle_purpose([parent, child])
        again = [dict(r, old=r["final"], new=dict(r["final"])) for r in (parent, child)]
        self.assertEqual(rb.settle_purpose(again), ([], 0))
        self.assertEqual([r["final"] for r in again], [parent["final"], child["final"]])

    def test_a_ladder_cell_is_never_raised_by_the_purpose_step(self):
        # An infantry parent hands its archer child melee above the Bow cell. The ladder owns Bow, so
        # the cross-kind case stays for the report rather than taking an invented number.
        parent = rec("spear", tp.kit_of([{"Item0": "Polearm"}]), upgrades=["bow"], Polearm=200, OneHanded=150)
        child = rec("bow", ARCHER, ladder={"Bow": 130}, Bow=130, OneHanded=120)
        rb.settle_purpose([parent, child])
        self.assertEqual(child["final"]["Bow"], 130)
        self.assertEqual(child["final"]["Polearm"], 200)
        self.assertEqual(child["final"]["OneHanded"], 200, "the carried sword is not left under the "
                                                          "inherited spear skill")

    def test_a_skipped_troop_keeps_its_values_and_still_lifts_its_child(self):
        parent = rec("troll", SWORD, upgrades=["kid"], OneHanded=10, Polearm=300)
        parent["new"] = None
        child = rec("kid", SWORD, OneHanded=100, Polearm=50)
        rb.settle_purpose([parent, child])
        self.assertEqual(child["final"]["Polearm"], 300)
        self.assertEqual(child["final"]["OneHanded"], 300)


MARCHWARDEN = tp.kit_of([{"Item0": "Bow", "Item1": "Arrows", "Item2": "TwoHanded"},
                         {"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}])
THROWER = tp.kit_of([{"Item0": "Throwing", "Item1": "Throwing", "Item2": "OneHanded", "Item3": "Shield"}])


class PlanFixPurposeTests(unittest.TestCase):
    def test_a_side_tie_raises_the_lower_class_and_logs_it(self):
        c = rec("mw", MARCHWARDEN, OneHanded=195, TwoHanded=130, Polearm=125, Bow=255)
        log, left = rb.plan_fix_purpose([c])
        self.assertEqual((c["final"]["OneHanded"], c["final"]["TwoHanded"]), (195, 195))
        self.assertEqual(c["tie_raises"], [("TwoHanded", 130, 195)])
        again = rec("mw", MARCHWARDEN, **c["final"])
        rb.plan_fix_purpose([again])
        self.assertEqual((again["tie_raises"], again["final"]), ([], c["final"]))

    def test_the_tie_log_starts_from_the_value_after_the_reorder(self):
        # Two sets open with a two-hander and a spear: both are mains and tie. The reorder deals
        # the top value to TwoHanded and the second to Polearm, which the tie then lifts.
        kit = tp.kit_of([{"Item0": "TwoHanded"}, {"Item0": "Polearm"}])
        c = rec("noble", kit, OneHanded=110, TwoHanded=90, Polearm=80)
        rb.plan_fix_purpose([c])
        self.assertEqual(c["tie_raises"], [("Polearm", 90, 110)])
        self.assertEqual((c["final"]["TwoHanded"], c["final"]["Polearm"], c["final"]["OneHanded"]),
                         (110, 110, 80))

    def test_a_three_to_one_split_raises_nothing(self):
        kit = tp.kit_of([{"Item0": "Bow", "Item1": "Arrows", "Item2": "TwoHanded"}] * 3
                        + [{"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}])
        c = rec("mw", kit, OneHanded=195, TwoHanded=130, Bow=255)
        rb.plan_fix_purpose([c])
        self.assertEqual(c["tie_raises"], [])
        self.assertLessEqual(sum(c["final"].values()), sum(c["old"].values()))

    def test_a_thrower_keeps_its_throwing_value_and_is_left_for_a_decision(self):
        c = rec("jav", THROWER, OneHanded=230, Polearm=250, Throwing=55)
        c["formula"] = None
        c["new"] = dict(c["old"], Throwing=300)
        log, left = rb.plan_fix_purpose([c])
        self.assertEqual(c["final"]["Throwing"], 55, "no number is borrowed from the formula")
        self.assertEqual((c["final"]["OneHanded"], c["final"]["Polearm"]), (250, 230))
        self.assertEqual([(f.cls, f.over, value) for _, f, value in left], [("Throwing", "OneHanded", 300)])


TROOP_XML = (
    '﻿<?xml version="1.0" encoding="utf-8"?>\r\n<NPCCharacters>\r\n'
    '  <NPCCharacter id="t" level="21" default_group="Infantry" name="T">\r\n'
    '    <skills>\r\n      <skill id="OneHanded" value="1" />\r\n    </skills>\r\n'
    '    <Equipments>\r\n'
    '      <EquipmentRoster civilian="true">\r\n'
    '        <equipment slot="Item0" id="Item.jav" />\r\n'
    '      </EquipmentRoster>\r\n'
    '      <EquipmentRoster>\r\n'
    '        <equipment slot="Item0" id="Item.shield" />\r\n'
    '        <equipment slot="Item1" id="Item.jav" />\r\n'
    '        <equipment\r\n          slot="Item2"\r\n          id="Item.axe" />\r\n'
    '      </EquipmentRoster>\r\n'
    '      <EquipmentRoster>\r\n'
    '        <equipment slot="Item0" id="Item.axe" />\r\n'
    '        <equipment slot="Item1" id="Item.jav" />\r\n'
    '      </EquipmentRoster>\r\n'
    '    </Equipments>\r\n  </NPCCharacter>\r\n</NPCCharacters>\r\n')
CLASSES = {"jav": "Throwing", "shield": "Shield", "axe": "OneHanded"}


class Item0WriterTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.path = Path(self._tmp.name) / "troops_x.xml"
        self.path.write_bytes(TROOP_XML.encode("utf-8"))

    def tearDown(self):
        self._tmp.cleanup()

    def test_the_kit_reads_the_fixed_slots_and_plans_one_exchange(self):
        npc = ET.parse(self.path).getroot().find("NPCCharacter")
        kit, swaps = rb.troop_kit(npc, CLASSES, fix_item0=True)
        self.assertEqual(swaps, [(1, "Item2")], "the civilian roster is ordinal 0 and never moves")
        self.assertEqual(tp.main_class(kit), "OneHanded")
        unfixed, none = rb.troop_kit(npc, CLASSES)
        self.assertEqual((tp.main_class(unfixed, {"OneHanded": 1}), none), ("OneHanded", []))

    def test_the_writer_exchanges_the_two_items_and_nothing_else(self):
        rb.apply_item0_swaps(str(self.path), {"t": [(1, "Item2")]})
        raw = self.path.read_bytes()
        expected = TROOP_XML.replace(
            '<equipment slot="Item0" id="Item.shield" />\r\n        <equipment slot="Item1" id="Item.jav" />\r\n'
            '        <equipment\r\n          slot="Item2"\r\n          id="Item.axe" />',
            '<equipment slot="Item0" id="Item.axe" />\r\n        <equipment slot="Item1" id="Item.jav" />\r\n'
            '        <equipment\r\n          slot="Item2"\r\n          id="Item.shield" />', 1)
        self.assertNotEqual(expected, TROOP_XML)
        self.assertEqual(raw, expected.encode("utf-8"), "BOM, CRLF and every other byte survive")

    def test_a_second_pass_plans_nothing(self):
        rb.apply_item0_swaps(str(self.path), {"t": [(1, "Item2")]})
        npc = ET.parse(self.path).getroot().find("NPCCharacter")
        self.assertEqual(rb.troop_kit(npc, CLASSES, fix_item0=True)[1], [])


class AnalyzerPurposeTests(unittest.TestCase):
    """analyze_troop_balance.py shows each troop's main class and the two findings, from the same
    function the writer and the validator call."""

    def test_the_overview_reports_the_main_class_and_both_findings(self):
        import analyze_troop_balance as atb
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "troops_x.xml"
            path.write_bytes(TROOP_XML.encode("utf-8"))
            _, troops = atb.parse_troop_file(str(path), CLASSES)
        t = troops[0]
        self.assertEqual(t["main"], "OneHanded")
        self.assertEqual(t["item0_sets"], 1)
        self.assertEqual(t["kit_findings"], [])
        report = atb.render_purpose_section([dict(t, kit_findings=["main class OneHanded 1 is below Polearm 9"])])
        self.assertIn("t", report)
        self.assertIn("OneHanded", report)

    def test_an_archer_opening_with_a_sword_is_a_finding_and_no_item0_set(self):
        import analyze_troop_balance as atb
        xml = (TROOP_XML.replace('default_group="Infantry"', 'default_group="Ranged"')
               .replace('Item.shield', 'Item.sword').replace('Item.axe', 'Item.bow')
               .replace('<EquipmentRoster>\r\n        <equipment slot="Item0" id="Item.bow" />\r\n'
                        '        <equipment slot="Item1" id="Item.jav" />',
                        '<EquipmentRoster>\r\n        <equipment slot="Item0" id="Item.sword" />\r\n'
                        '        <equipment slot="Item1" id="Item.bow" />'))
        classes = {"jav": "Throwing", "sword": "OneHanded", "bow": "Bow"}
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "troops_x.xml"
            path.write_bytes(xml.encode("utf-8"))
            _, troops = atb.parse_troop_file(str(path), classes)
        t = troops[0]
        self.assertEqual(t["item0_sets"], 0)
        self.assertIn("launcher troop that does not open with its launcher: 2 of 2 battle sets",
                      "; ".join(t["kit_findings"]))


if __name__ == "__main__":
    unittest.main()
