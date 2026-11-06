#!/usr/bin/env python3
"""Unit tests for the Tharbad Bridge Guard in tools/generate_cardolan_troops.py.

Run:  python -m unittest tools.tests.test_generate_cardolan_troops

The invariants every realm design shares (self-check, no skill lost on an upgrade, ladder cells) are in
test_realm_troops.py. This file pins the one troop added from player feedback (2026-11-06) and where it sits.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_cardolan_troops as gct  # noqa: E402


class TharbadBridgeGuardTests(unittest.TestCase):
    def setUp(self):
        self.troops = gct.add_mercenary(gct.clamp_skills(gct.build()))
        self.by_id = {t.id: t for t in self.troops}
        self.guard = self.by_id["cardolan_tharbad_bridge_guard"]

    def test_the_barrow_guard_keeps_one_upgrade(self):
        # The engine's AI upgrader gives each party leader's hash one target of a two-target troop weight 9999
        # (DefaultPartyTroopUpgradeModel.GetUpgradeChanceForTroopUpgrade). A second target at level 21 would send
        # about half of the AI lords' Barrow Guards to a dead end instead of the Warden line.
        self.assertEqual(self.by_id["cardolan_barrow_guard"].upgrades, ["cardolan_veteran_barrow_guard"])

    def test_the_bridge_guard_is_an_end_of_line_beside_the_warden(self):
        self.assertEqual(self.by_id["cardolan_veteran_barrow_guard"].upgrades,
                         ["cardolan_warden", "cardolan_tharbad_bridge_guard"])
        self.assertEqual(self.guard.level, self.by_id["cardolan_warden"].level)
        self.assertEqual(self.guard.upgrades, [])

    def test_the_bridge_guard_carries_a_pike_and_no_shield(self):
        for roster in self.guard.weapons:
            self.assertEqual(roster[0], "fine_pike_t4")
            self.assertFalse([w for w in roster if w.startswith("sm_")], f"shield in {roster}")

    def test_the_bridge_guard_fights_its_opening_pike_at_its_best_melee_skill(self):
        # Item0 is the weapon in hand at spawn, and the engine reads a Pike (TwoHandedPolearm usage) with the
        # Polearm skill (WeaponComponentData.GetRelevantSkillFromWeaponClass).
        for other in ("OneHanded", "TwoHanded"):
            self.assertGreaterEqual(self.guard.sk["Polearm"], self.guard.sk[other], other)


if __name__ == "__main__":
    unittest.main()
