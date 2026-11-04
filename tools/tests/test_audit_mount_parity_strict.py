#!/usr/bin/env python3
"""audit_mount_parity.py --strict: a BT-driven mount's usage set must be total.

Run:  python -m unittest tools.tests.test_audit_mount_parity_strict

The engine's usage-record lookups dereference an end sentinel on a miss and crash the game
(creature-mount-authoring.md Phase 5). Only a creature steered by a behaviour tree queries every
jump direction, so the warg (front and none only) and the vanilla horse are outside the check.
"""
import io
import os
import sys
import unittest
import xml.etree.ElementTree as ET
from contextlib import redirect_stdout
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import audit_mount_parity as amp  # noqa: E402

DIRECTIONS = ("front", "front_left", "front_right", "none", "left", "right", "back", "back_left", "back_right")


def usage_set(verbs=True, directions=DIRECTIONS, paces=range(6)):
    root = ET.Element("monster_usage_set", id="t")
    if verbs:
        for verb in amp.STRICT_VERBS:
            root.set(verb, "act_x")
    jumps = ET.SubElement(root, "monster_usage_jumps")
    for d in directions:
        ET.SubElement(jumps, "monster_usage_jump", jump_state="start", direction=d, action="a")
        for h in ("false", "true"):
            ET.SubElement(jumps, "monster_usage_jump", jump_state="loop", direction=d, is_hard=h, action="a")
            ET.SubElement(jumps, "monster_usage_jump", jump_state="end", direction=d, is_hard=h, action="a")
    moves = ET.SubElement(root, "monster_usage_movements")
    for p in paces:
        ET.SubElement(moves, "monster_usage_movement", pace=str(p), direction="none",
                      turn_direction="none", is_left_foot="false", action="a")
    return root


class StrictGapsTests(unittest.TestCase):
    def test_a_total_set_has_no_gaps(self):
        self.assertEqual(amp.strict_gaps(usage_set()), [])

    def test_a_front_and_none_only_jump_table_is_a_gap(self):
        gaps = amp.strict_gaps(usage_set(directions=("front", "none")))
        self.assertEqual(len(gaps), 1)
        self.assertIn("monster_usage_jumps", gaps[0])
        self.assertIn("35", gaps[0])

    def test_a_missing_pace_reference_row_is_a_gap(self):
        gaps = amp.strict_gaps(usage_set(paces=(0, 1, 2, 3, 4)))
        self.assertEqual(len(gaps), 1)
        self.assertIn("pace 5", gaps[0])

    def test_a_missing_verb_is_a_gap(self):
        gaps = amp.strict_gaps(usage_set(verbs=False))
        self.assertEqual(len(gaps), 1)
        self.assertIn("verb", gaps[0])

    def test_no_set_is_a_gap(self):
        self.assertEqual(len(amp.strict_gaps(None)), 1)

    def test_the_warg_and_the_horse_are_outside_the_named_list(self):
        self.assertTrue(set(amp.STRICT_MOUNTS).isdisjoint({"warg", "horse"}))
        self.assertIn("spider", amp.STRICT_MOUNTS)


class WarRamGapsTests(unittest.TestCase):
    """Section G. The war ram rides the vanilla horse skeleton and inherits the horse's usage set, so
    it is held to the horse rather than to the BT-driven mounts. A missing _map or _town_and_village
    child set is the elephant's native-AV class (war-ram.md)."""

    TYPES = {"act_war_ram_butt": "actt_kick"}

    def monster(self, **attrs):
        return ET.Element("Monster", {"id": "taom_war_ram", "base_monster": "horse",
                                      "action_set": "as_war_ram", **attrs})

    def sets(self, drop=(), **bases):
        want = {"as_war_ram": "as_horse", "as_war_ram_map": "as_horse_map",
                "as_war_ram_town_and_village": "as_horse_town_and_village", **bases}
        return {sid: ET.Element("action_set", {"id": sid, "base_set": base})
                for sid, base in want.items() if sid not in drop}

    def test_a_ram_over_the_horse_sets_has_no_gaps(self):
        self.assertEqual(amp.ram_gaps(self.monster(), self.sets(), self.TYPES), [])

    def test_no_monster_is_a_gap(self):
        self.assertEqual(len(amp.ram_gaps(None, self.sets(), self.TYPES)), 1)

    def test_a_base_monster_other_than_the_horse_is_a_gap(self):
        gaps = amp.ram_gaps(self.monster(base_monster="human"), self.sets(), self.TYPES)
        self.assertEqual(len(gaps), 1)
        self.assertIn("base_monster", gaps[0])

    def test_an_own_monster_usage_is_a_gap_unless_it_is_the_horse(self):
        gaps = amp.ram_gaps(self.monster(monster_usage="ram"), self.sets(), self.TYPES)
        self.assertEqual(len(gaps), 1)
        self.assertIn("monster_usage", gaps[0])
        self.assertEqual(amp.ram_gaps(self.monster(monster_usage="horse"), self.sets(), self.TYPES), [])

    def test_a_missing_map_set_is_a_gap(self):
        gaps = amp.ram_gaps(self.monster(), self.sets(drop=("as_war_ram_map",)), self.TYPES)
        self.assertEqual(len(gaps), 1)
        self.assertIn("as_war_ram_map", gaps[0])

    def test_a_set_over_the_wrong_base_is_a_gap(self):
        gaps = amp.ram_gaps(self.monster(), self.sets(as_war_ram="as_dwarf_warrior"), self.TYPES)
        self.assertEqual(len(gaps), 1)
        self.assertIn("as_horse", gaps[0])

    def test_a_head_butt_not_typed_as_a_kick_is_a_gap(self):
        for types in ({}, {"act_war_ram_butt": "actt_strike"}):
            with self.subTest(types=types):
                gaps = amp.ram_gaps(self.monster(), self.sets(), types)
                self.assertEqual(len(gaps), 1)
                self.assertIn("act_war_ram_butt", gaps[0])


class MissingInstallTests(unittest.TestCase):
    def test_a_missing_armory_exits_two_not_one(self):
        """Exit 1 is what --strict returns for a gap; a missing install is bad input."""
        err = io.StringIO()
        with mock.patch.object(amp, "LOTR", r"E:\nonexistent-install\Modules\LOTRLOME_Armory\ModuleData"), \
                mock.patch.object(sys, "argv", ["audit_mount_parity.py", "--strict"]), \
                mock.patch.object(sys, "stderr", err), redirect_stdout(io.StringIO()):
            self.assertEqual(amp.main(), 2)
        self.assertIn("BANNERLORD_GAME_DIR", err.getvalue())


if __name__ == "__main__":
    unittest.main()
