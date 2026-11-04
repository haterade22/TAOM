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
