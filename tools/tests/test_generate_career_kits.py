#!/usr/bin/env python3
"""Unit tests for tools/generate_career_kits.py (#629), on synthetic XML.

Run:  python -m unittest tools.tests.test_generate_career_kits

The career kits are the gear each culture's lowest troops carry. The ways the derivation could be
wrong and still look plausible: a higher-level item beating a lower one, a vanilla item beating a
culture's own, a high-tier culture item sneaking in past the level cap, a bow whose skill
requirement a new character cannot meet, a hero's or a civilian set's item counted as troop gear,
and a rewrite that touches the mounts or the file's line endings.
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_career_kits as ck  # noqa: E402


def cand(item, level, count=1, vanilla=False, difficulty=0):
    return ck.Candidate(item, level, count, vanilla, difficulty)


class TestPick(unittest.TestCase):
    def test_lowest_level_wins(self):
        self.assertEqual(ck.pick([cand("b", 16), cand("a", 11)]), "a")

    def test_own_item_beats_a_lower_level_vanilla_one(self):
        self.assertEqual(ck.pick([cand("van", 6, vanilla=True), cand("own", 11)]), "own")

    def test_an_own_item_above_the_level_cap_loses_to_vanilla(self):
        self.assertEqual(ck.pick([cand("van", 11, vanilla=True), cand("own_t7", 36)]), "van")

    def test_the_cap_is_inclusive(self):
        self.assertEqual(ck.pick([cand("van", 11, vanilla=True), cand("own", ck.LEVEL_CAP)]), "own")

    def test_tie_goes_to_the_most_carried_then_the_id(self):
        self.assertEqual(ck.pick([cand("b", 11, 3), cand("a", 11, 7)]), "a")
        self.assertEqual(ck.pick([cand("b", 11, 2), cand("a", 11, 2)]), "a")

    def test_a_bow_takes_the_lowest_skill_requirement_first(self):
        bows = [cand("own_t3", 16, difficulty=50), cand("van_t2", 11, vanilla=True, difficulty=0)]
        self.assertEqual(ck.pick(bows, is_bow=True), "van_t2")
        self.assertEqual(ck.pick(bows), "own_t3")

    def test_among_equal_requirements_the_usual_rule_applies(self):
        bows = [cand("van_t2", 11, vanilla=True), cand("own_t4", 21)]
        self.assertEqual(ck.pick(bows, is_bow=True), "own_t4")

    def test_no_candidate_is_none(self):
        self.assertIsNone(ck.pick([]))


class TestLayout(unittest.TestCase):
    def test_slot_classes_follow_the_archetype(self):
        self.assertEqual(ck.slot_classes("gondor", "ranged"),
                         {"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHandedSword",
                          "Body": "BodyArmor", "Leg": "LegArmor"})
        self.assertEqual(ck.slot_classes("gondor", "cavalry")["Item0"], "TwoHandedPolearm")
        self.assertEqual(ck.slot_classes("gondor", "infantry")["Item2"], "TwoHandedPolearm")

    def test_culture_exceptions(self):
        self.assertEqual(ck.slot_classes("empire", "infantry")["Item0"], "OneHandedAxe")
        self.assertEqual(ck.slot_classes("erebor", "ranged")["Item2"], "OneHandedAxe")
        self.assertEqual(ck.slot_classes("dolguldur", "infantry")["Item2"], "TwoHandedAxe")
        self.assertEqual(ck.slot_classes("dolguldur", "cavalry")["Item0"], "TwoHandedPolearm")


TROOPS = """<NPCCharacters>
  <NPCCharacter id="recruit" level="6" culture="Culture.gondor">
    <Equipments>
      <EquipmentRoster><equipment slot="Item0" id="Item.low_sword" /><equipment slot="Body" id="Item.rags" /></EquipmentRoster>
      <EquipmentRoster civilian="true"><equipment slot="Item0" id="Item.civ_sword" /></EquipmentRoster>
      <equipment slot="Horse" id="Item.pony" />
    </Equipments>
  </NPCCharacter>
  <NPCCharacter id="veteran" level="16" culture="Culture.gondor">
    <Equipments>
      <EquipmentRoster><equipment slot="Item0" id="Item.low_sword" /><equipment slot="Item0" id="Item.good_sword" /></EquipmentRoster>
    </Equipments>
  </NPCCharacter>
  <NPCCharacter id="lord" level="1" culture="Culture.gondor" is_hero="true">
    <Equipments><EquipmentRoster><equipment slot="Item0" id="Item.hero_sword" /></EquipmentRoster></Equipments>
  </NPCCharacter>
</NPCCharacters>"""


class TestTroopIndex(unittest.TestCase):
    def setUp(self):
        self.index = ck.index_troop_gear([ET.fromstring(TROOPS)])["gondor"]

    def test_min_level_and_count_per_item(self):
        self.assertEqual(self.index["low_sword"], (6, 2))
        self.assertEqual(self.index["good_sword"], (16, 1))

    def test_heroes_and_civilian_sets_are_not_troop_gear(self):
        self.assertNotIn("hero_sword", self.index)
        self.assertNotIn("civ_sword", self.index)

    def test_direct_equipment_overrides_count(self):
        # the engine applies an <Equipments>/<equipment> override to every set of the troop
        self.assertEqual(self.index["pony"], (6, 1))


class TestScan(unittest.TestCase):
    def test_an_unparsable_item_file_is_reported_not_silently_skipped(self):
        # its items would otherwise just vanish from the candidate pool and the rule would
        # quietly pick the next item
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            bad = Path(tmp) / "broken.xml"
            bad.write_text("<Items><Item id='x'></Items>", encoding="utf-8")
            failures = []
            items, _, _ = ck._scan([bad], failures)
        self.assertEqual(items, {})
        self.assertEqual(len(failures), 1)
        self.assertIn("broken.xml", failures[0])


CAREER = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n'
    "<EquipmentRosters>\r\n"
    '    <EquipmentRoster id="player_career_gondor_ranged_m" culture="Culture.gondor">\r\n'
    "        <EquipmentSet>\r\n"
    '            <Equipment slot="Item0" id="Item.old_bow" />\r\n'
    '            <Equipment slot="Item1" id="Item.old_arrows" />\r\n'
    '            <Equipment slot="Item2" id="Item.old_sword" />\r\n'
    '            <Equipment slot="Body" id="Item.old_body" />\r\n'
    '            <Equipment slot="Leg" id="Item.old_leg" />\r\n'
    '            <Equipment slot="Horse" id="Item.saddle_horse" />\r\n'
    "        </EquipmentSet>\r\n"
    "    </EquipmentRoster>\r\n"
    "</EquipmentRosters>\r\n"
)
KIT = {"Item0": "new_bow", "Item1": "new_arrows", "Item2": "new_sword", "Body": "new_body", "Leg": "new_leg"}


class TestRewrite(unittest.TestCase):
    def test_substitutes_the_five_slots_only_and_keeps_crlf(self):
        out, changed = ck.apply_kits(CAREER, {("gondor", "ranged"): KIT})
        self.assertEqual(changed, 5)
        for iid in KIT.values():
            self.assertIn(f'id="Item.{iid}"', out)
        self.assertIn('id="Item.saddle_horse"', out)
        self.assertEqual(out.count("\r\n"), CAREER.count("\r\n"))
        restored = out
        for slot, iid in KIT.items():
            restored = restored.replace(f"Item.{iid}", "Item.old_" + {"Item0": "bow", "Item1": "arrows",
                                                                      "Item2": "sword", "Body": "body",
                                                                      "Leg": "leg"}[slot])
        self.assertEqual(restored, CAREER)

    def test_idempotent(self):
        once, _ = ck.apply_kits(CAREER, {("gondor", "ranged"): KIT})
        twice, changed = ck.apply_kits(once, {("gondor", "ranged"): KIT})
        self.assertEqual(twice, once)
        self.assertEqual(changed, 0)

    def test_a_roster_without_a_kit_fails_loudly(self):
        with self.assertRaises(ck.CareerKitError):
            ck.apply_kits(CAREER, {})

    def test_a_slot_the_culture_has_no_candidate_for_fails_loudly(self):
        with self.assertRaises(ck.CareerKitError):
            ck.apply_kits(CAREER, {("gondor", "ranged"): {**KIT, "Item0": None}})


@unittest.skipUnless(ck.DEFAULT_ARMORY.exists(), "needs the LOTRLOME_Armory install; skipped, never faked")
class TestCommittedCareerFile(unittest.TestCase):
    def test_the_committed_career_file_is_what_the_rule_derives(self):
        drift = ck.verify(ck.CAREER_FILE, ck.derive_kits(ck.load_sources()))
        self.assertEqual(drift, [], "run python tools/generate_career_kits.py --apply, then "
                                    "python tools/wire_starter_kit_rosters.py --apply")


if __name__ == "__main__":
    unittest.main()
