#!/usr/bin/env python3
"""audit_mount_parity.py --strict: a BT-driven mount's usage set must be total.

Run:  python -m unittest tools.tests.test_audit_mount_parity_strict

The engine's usage-record lookups dereference an end sentinel on a miss and crash the game
(creature-mount-authoring.md Phase 5). Only a creature steered by a behaviour tree queries every
jump direction, so the warg (front and none only) and the vanilla horse are outside the check.
"""
import contextlib
import io
import os
import sys
import tempfile
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
    """Section G. On the 1.4.x line the war ram is a horse-skeleton reskin that authors no action set:
    its Monster sits on the vanilla as_horse and plays act_horse_kick (war-ram.md). The chained
    as_war_ram sets and act_war_ram_butt exist only on the 1.5.x line, so they are not asked for here."""

    def monster(self, **attrs):
        return ET.Element("Monster", {"id": "taom_war_ram", "base_monster": "horse",
                                      "action_set": "as_horse", **attrs})

    def test_a_ram_on_the_horse_has_no_gaps(self):
        self.assertEqual(amp.ram_gaps(self.monster()), [])

    def test_no_monster_is_a_gap(self):
        self.assertEqual(len(amp.ram_gaps(None)), 1)

    def test_a_base_monster_other_than_the_horse_is_a_gap(self):
        gaps = amp.ram_gaps(self.monster(base_monster="human"))
        self.assertEqual(len(gaps), 1)
        self.assertIn("base_monster", gaps[0])

    def test_an_own_monster_usage_is_a_gap_unless_it_is_the_horse(self):
        gaps = amp.ram_gaps(self.monster(monster_usage="ram"))
        self.assertEqual(len(gaps), 1)
        self.assertIn("monster_usage", gaps[0])
        self.assertEqual(amp.ram_gaps(self.monster(monster_usage="horse")), [])

    def test_an_action_set_other_than_the_horse_is_a_gap(self):
        gaps = amp.ram_gaps(self.monster(action_set="as_dwarf_warrior"))
        self.assertEqual(len(gaps), 1)
        self.assertIn("as_horse", gaps[0])

    def test_the_1_5_x_set_name_is_a_gap_on_this_line(self):
        gaps = amp.ram_gaps(self.monster(action_set="as_war_ram"))
        self.assertEqual(len(gaps), 1)
        self.assertIn("as_war_ram", gaps[0])


class MissingInstallTests(unittest.TestCase):
    def test_a_missing_armory_exits_two_not_one(self):
        """Exit 1 is what --strict returns for a gap; a missing install is bad input."""
        err = io.StringIO()
        with mock.patch.object(amp, "LOTR", r"E:\nonexistent-install\Modules\LOTRLOME_Armory\ModuleData"), \
                mock.patch.object(sys, "argv", ["audit_mount_parity.py", "--strict"]), \
                mock.patch.object(sys, "stderr", err), redirect_stdout(io.StringIO()):
            self.assertEqual(amp.main(), 2)
        self.assertIn("BANNERLORD_GAME_DIR", err.getvalue())


class SectionGGateTests(unittest.TestCase):
    """data_gates.py reads only the exit code, so a gap section G prints must count under --strict."""

    SECTIONS = ("section_a", "section_bc", "section_d", "section_e", "section_f")

    def run_main(self, args, ram_gaps=None, ram_path="missing"):
        with tempfile.TemporaryDirectory() as td, contextlib.ExitStack() as stack:
            ram = {"monster": (os.path.join(td, ram_path + ".xml"), "taom_war_ram")}
            stack.enter_context(mock.patch.object(amp, "LOTR", td))
            stack.enter_context(mock.patch.object(amp, "RAM", ram))
            stack.enter_context(mock.patch.object(amp, "load_action_types", return_value={}))
            stack.enter_context(mock.patch.object(amp, "section_strict", return_value=0))
            for name in self.SECTIONS:
                stack.enter_context(mock.patch.object(amp, name))
            if ram_gaps is not None:
                stack.enter_context(mock.patch.object(amp, "ram_gaps", return_value=ram_gaps))
            stack.enter_context(mock.patch.object(sys, "argv", ["audit_mount_parity.py", *args]))
            out = io.StringIO()
            stack.enter_context(redirect_stdout(out))
            return amp.main(), out.getvalue()

    def test_main_Strict_AGapSectionGReturns_Exits1(self):
        code, out = self.run_main(["--strict"], ram_gaps=["synthetic gap"])
        self.assertEqual(code, 1)
        self.assertIn("synthetic gap", out)

    def test_main_Strict_MissingWarRamFile_ExitsOneInsteadOfRaising(self):
        code, out = self.run_main(["--strict"])
        self.assertEqual(code, 1)
        self.assertIn("Monster taom_war_ram not found", out)

    def test_main_NotStrict_AGap_StaysReportOnly(self):
        code, out = self.run_main([], ram_gaps=["synthetic gap"])
        self.assertEqual(code, 0)
        self.assertIn("synthetic gap", out)

    def test_main_Strict_NoGap_Exits0(self):
        code, _ = self.run_main(["--strict"], ram_gaps=[])
        self.assertEqual(code, 0)

    def test_section_g_ReturnsItsGaps(self):
        with mock.patch.object(amp, "RAM", {"monster": ("missing.xml", "taom_war_ram")}), \
                redirect_stdout(io.StringIO()):
            self.assertEqual(amp.section_g(), ["Monster taom_war_ram not found"])

    def test_section_g_TheShippedMonsterShape_HasNoGaps(self):
        """Read from a file, with no action_sets.xml or action type table behind it: 1.4.x needs neither."""
        with tempfile.TemporaryDirectory() as td:
            path = os.path.join(td, "lotr_monster_war_ram.xml")
            with open(path, "w", encoding="utf-8") as f:
                f.write('<base><Monster id="taom_war_ram" base_monster="horse" action_set="as_horse"/></base>')
            with mock.patch.object(amp, "RAM", {"monster": (path, "taom_war_ram")}), redirect_stdout(io.StringIO()):
                self.assertEqual(amp.section_g(), [])


if __name__ == "__main__":
    unittest.main()
