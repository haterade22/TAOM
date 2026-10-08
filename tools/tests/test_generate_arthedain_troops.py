#!/usr/bin/env python3
"""Unit tests for tools/generate_arthedain_troops.py.

Run:  python -m unittest tools.tests.test_generate_arthedain_troops

Pure stdlib; the generator builds the tree in memory. Pins the design invariants the validator
would otherwise only report after a write:
  - the shipped design passes the generator's own self-check
  - no upgrade lowers any skill (UPGRADE_SKILL_REGRESSION)
  - the militia follow the TAOM-wide rule: the elite slot is the basic one plus the bonus
  - the tavern mercenary is a leaf copy with occupation Mercenary
  - every troop's two battle rosters fill the same slots, and a bow troop has its bow in Item0 of both
  - no troop pairs a shield with a two-handed weapon, and none carries numenorean_bastard_medium
  - the generated file and templates parse
  - every bow and Bow skill is its ranged-ladder cell; the veteran militia sit at level 16
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_arthedain_troops as gat  # noqa: E402
import ranged_ladder as rl  # noqa: E402


def built():
    return gat.add_mercenary(gat.clamp_skills(gat.build()))


class DesignTests(unittest.TestCase):
    def setUp(self):
        self.troops = built()
        self.by_id = {t.id: t for t in self.troops}

    def test_the_shipped_design_passes_its_self_check(self):
        self.assertEqual(gat.self_check(self.troops), [])

    def test_no_upgrade_lowers_any_skill(self):
        for t in self.troops:
            for u in t.upgrades:
                for k in gat.SKILLS:
                    self.assertGreaterEqual(self.by_id[u].sk[k], t.sk[k], f"{t.id} -> {u} {k}")

    def test_the_elite_militia_is_the_basic_plus_the_bonus(self):
        for basic_id, vet_id in gat.MILITIA.items():
            basic, vet = self.by_id[basic_id], self.by_id[vet_id]
            for k in gat.SKILLS:
                if k == "Bow" and basic.role == "ranged":
                    continue  # the ladder owns a laddered archer's Bow
                self.assertEqual(vet.sk[k], basic.sk[k] + gat.MILITIA_ELITE_BONUS, f"{vet_id} {k}")

    def test_the_mercenary_is_a_leaf_copy_of_the_rarest_pool_entry(self):
        merc = self.by_id[gat.MERC_SOURCE + "_merc"]
        self.assertEqual(merc.occupation, "Mercenary")
        self.assertEqual(merc.upgrades, [])
        self.assertEqual(merc.sk, self.by_id[gat.MERC_SOURCE].sk)

    def test_bow_troops_hold_the_bow_in_item0_of_both_rosters(self):
        for t in self.troops:
            if t.role != "ranged":
                continue
            for roster in t.weapons:
                self.assertTrue(roster[0].startswith("ladder_arthedain_bow_t"), t.id)

    def test_nobody_carries_the_over_band_bastard_sword(self):
        for t in self.troops:
            for roster in t.weapons:
                self.assertFalse(any(w.startswith("numenorean_bastard_medium") for w in roster), t.id)

    def test_every_party_template_names_only_defined_troops(self):
        for tid, stacks in gat.party_templates():
            for troop, lo, hi in stacks:
                self.assertIn(troop, self.by_id, tid)
                self.assertLessEqual(lo, hi, tid)


class OutputTests(unittest.TestCase):
    def test_the_troop_file_and_templates_parse(self):
        root = ET.fromstring(gat.troops_xml(built(), "\r\n").encode("utf-8"))
        self.assertEqual(len(root.findall("NPCCharacter")), len(built()))
        ET.fromstring(("<partyTemplates>" + gat.templates_xml("\n") + "</partyTemplates>").encode("utf-8"))

    def test_every_ranged_troop_carries_its_ladder_cell(self):
        # One function writes Bow here and in rebalance_ranged_ladders.py, so --check never drifts
        # against the ladder tool and --apply never reverts it.
        spec = rl.load_spec()
        for t in built():
            if t.role != "ranged":
                continue
            tier = rl.engine_tier(t.level)
            self.assertEqual(t.sk["Bow"], rl.skill_cell("arthedain", tier, spec), t.id)
            for roster in t.weapons:
                self.assertEqual(roster[0], rl.ladder_id("arthedain", "Bow", tier), t.id)

    def test_the_veteran_militia_sit_at_every_cultures_level_16(self):
        # Every other culture's veteran militia is level 16 (tier 3); 21 put Arthedain's a tier above
        # everyone's, against a realm weak by design.
        by_id = {t.id: t for t in built()}
        for vet_id in gat.MILITIA.values():
            self.assertEqual(by_id[vet_id].level, 16, vet_id)


if __name__ == "__main__":
    unittest.main()
