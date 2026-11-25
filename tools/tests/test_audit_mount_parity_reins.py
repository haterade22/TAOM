#!/usr/bin/env python3
"""audit_mount_parity.py section H: a rideable mount must declare rein attributes (#479).

Run:  python -m unittest tools.tests.test_audit_mount_parity_reins

Vanilla pairs "rideable" with "declares rein_* attributes" (or inherits them through base_monster).
A mount with neither has no rein bones for the rider, and the native rein path runs again on
mounted-agent death. The tool's --strict exit code carries the finding, because data_gates.py reads
only the exit code.
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


def monster(mid="m", mountable=True, base=None, **attrs):
    el = ET.Element("Monster", {"id": mid, **({"base_monster": base} if base else {}), **attrs})
    if mountable:
        ET.SubElement(el, "Flags", Mountable="true")
    return el


class ReinGapTests(unittest.TestCase):
    def test_a_mount_with_rein_attributes_has_no_gap(self):
        self.assertIsNone(amp.rein_gap(monster(rein_handle_bone="Spine3_M")))

    def test_a_rideable_mount_without_reins_is_a_gap(self):
        self.assertIn("rein", amp.rein_gap(monster()))

    def test_a_mount_that_inherits_through_base_monster_has_no_gap(self):
        self.assertIsNone(amp.rein_gap(monster(base="horse")))

    def test_a_creature_that_is_not_rideable_has_no_gap(self):
        self.assertIsNone(amp.rein_gap(monster(mountable=False)))

    def test_no_monster_is_a_gap(self):
        self.assertIsNotNone(amp.rein_gap(None))


class ReinFindingsTests(unittest.TestCase):
    def test_an_unknown_mount_without_reins_is_new(self):
        new, known, stale = amp.rein_findings({"a": monster(), "b": monster(rein_handle_bone="x")}, {})
        self.assertEqual((len(new), known, stale), (1, [], []))
        self.assertIn("a", new[0])

    def test_a_listed_mount_is_known_and_not_new(self):
        new, known, stale = amp.rein_findings({"a": monster()}, {"a": "#479"})
        self.assertEqual((new, len(known), stale), ([], 1, []))

    def test_a_listed_mount_with_no_monster_is_new_not_known(self):
        new, known, stale = amp.rein_findings({"a": None}, {"a": "#479"})
        self.assertEqual((len(new), known, stale), (1, [], []))
        self.assertIn("Monster not found", new[0])

    def test_a_listed_mount_that_gained_reins_is_stale(self):
        new, known, stale = amp.rein_findings({"a": monster(rein_handle_bone="x")}, {"a": "#479"})
        self.assertEqual((new, known, len(stale)), ([], [], 1))

    def test_the_listed_mounts_are_the_ones_the_issue_names(self):
        self.assertEqual(set(amp.KNOWN_REIN_GAPS), {"elephant", "mumakil"})


class ReinGateTests(unittest.TestCase):
    """A new finding, or a stale known entry, must fail --strict; known entries alone must not."""

    SECTIONS = ("section_a", "section_bc", "section_d", "section_e", "section_f", "section_g")

    def run_main(self, args, h_gaps):
        with tempfile.TemporaryDirectory() as td, contextlib.ExitStack() as stack:
            stack.enter_context(mock.patch.object(amp, "LOTR", td))
            stack.enter_context(mock.patch.object(amp, "load_action_types", return_value={}))
            stack.enter_context(mock.patch.object(amp, "section_strict", return_value=0))
            for name in self.SECTIONS:
                stack.enter_context(mock.patch.object(amp, name, return_value=[]))
            stack.enter_context(mock.patch.object(amp, "section_h", return_value=h_gaps))
            stack.enter_context(mock.patch.object(sys, "argv", ["audit_mount_parity.py", *args]))
            stack.enter_context(redirect_stdout(io.StringIO()))
            return amp.main()

    def test_main_Strict_AReinGap_Exits1(self):
        self.assertEqual(self.run_main(["--strict"], ["spider has no rein attributes"]), 1)

    def test_main_NotStrict_AReinGap_StaysReportOnly(self):
        self.assertEqual(self.run_main([], ["spider has no rein attributes"]), 0)

    def test_main_Strict_NoReinGap_Exits0(self):
        self.assertEqual(self.run_main(["--strict"], []), 0)

    def test_section_h_OnTheShippedData_ReportsOnlyTheKnownMounts(self):
        if not os.path.isfile(amp.FILES["monster"]["elephant"]):
            self.skipTest("no LOTRLOME_Armory install")
        out = io.StringIO()
        with redirect_stdout(out):
            gaps = amp.section_h()
        self.assertEqual(gaps, [], out.getvalue())
        self.assertIn("KNOWN", out.getvalue())


if __name__ == "__main__":
    unittest.main()
