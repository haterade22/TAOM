#!/usr/bin/env python3
"""In-repo gate for the war ram bardings' team colour, an edit that lives only in the unversioned Armory.

Run:  python -m unittest tools.tests.test_live_ram_bardings

The eight `taom_ram_barding_*` HorseHarness items in the LIVE
`LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` carry greyscale cloth that is meant to take the
rider's colours, so each needs `UseTeamColor="true"` (engine: `MountVisualCreator.AddMountMeshToAgentVisual` tints
the harness mesh when `harnessItem.IsUsingTeamColor`). Added 2026-09-18 and confirmed in game. The Armory is not in
git, so a reinstall reverts it with no signal; the orientation.md "Unversioned modules" trap asks for a gate in the repo.
Each item must also keep exactly ONE `<Flags>` element: `Items.xsd` allows one, and a second logs a schema error on
every load (`docs/reference/lotrlome-war-ram-changes.md`). The same element keeps `Civilian="true"`, which every
barding carried before the team-colour edit, so the fold into one element is checked not to have dropped it.

Skips, never fails, where the install is absent (CI, the laptop's partial install). Stdlib only.
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from _gamedir import game_dir  # noqa: E402

HORSES = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"),
                      "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml")
BARDINGS = [f"taom_ram_barding_{tier}_{v}" for tier in ("light", "med", "heavy", "elite") for v in ("a", "b")]


@unittest.skipUnless(os.path.isfile(HORSES), "live LOTRLOME_Armory not installed on this machine")
class LiveRamBardingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with open(HORSES, "rb") as fh:
            root = ET.fromstring(fh.read())
        cls.items = {it.get("id"): it for it in root.iter("Item")}

    def test_all_eight_bardings_exist(self):
        missing = [b for b in BARDINGS if b not in self.items]
        self.assertEqual(missing, [], "a barding id is gone from the live Armory")

    def test_each_barding_takes_team_colour_and_stays_civilian_in_one_flags_element(self):
        for b in BARDINGS:
            item = self.items.get(b)
            if item is None:
                continue  # reported by test_all_eight_bardings_exist
            with self.subTest(item=b):
                flags = [c for c in item if c.tag == "Flags"]
                self.assertEqual(len(flags), 1, "Items.xsd allows one <Flags> element")
                self.assertEqual(flags[0].get("UseTeamColor"), "true")
                self.assertEqual(flags[0].get("Civilian"), "true")


if __name__ == "__main__":
    unittest.main()
