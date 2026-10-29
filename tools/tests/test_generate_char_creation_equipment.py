#!/usr/bin/env python3
"""Unit tests for tools/generate_char_creation_equipment.py, for the dwarf cultures it writes.

Run:  python -m unittest tools.tests.test_generate_char_creation_equipment

The character-creation rosters are applied to the player at runtime and no NPCCharacter names
them, so validate_moduledata.py's MOUNTED_DWARF sweep never sees them. What these tests pin for
Ered Luin: no Horse or HorseHarness slot (dwarves do not ride, and Ered Luin fields no war ram),
only dwarf gear, and the parents dressed as Erebor's ship (a dress and a noble tunic since
307df401a, so mother and father do not stand in the same leather chest). A culture without the
parent keys keeps the generator's old shape.
"""
import os
import re
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_char_creation_equipment as cc  # noqa: E402

DWARF_GEAR = ("sk_dwarf_", "sm_dwarf_")
MOUNT_SLOTS = ("Horse", "HorseHarness")
OCCUPATIONS = ["none"] + cc.OCCUPATIONS


def generated(culture: str) -> dict:
    """roster id -> element, for the rosters the generator would append for `culture`."""
    lines, _ = cc.generate_culture(culture)
    root = ET.fromstring("<EquipmentRosters>" + "\n".join(lines) + "</EquipmentRosters>")
    return {r.get("id"): r for r in root.iter("EquipmentRoster")}


def shipped() -> ET.Element:
    return ET.parse(os.path.normpath(cc.OUTPUT_PATH)).getroot()


def bare(item_id: str) -> str:
    """`Item.starter_x` -> `x`: a starter twin keeps its donor's mesh, so the donor is the gear."""
    return re.sub(r"^starter_", "", (item_id or "").replace("Item.", "", 1))


def body_of(roster: ET.Element) -> str:
    return next(bare(e.get("id")) for e in roster.iter("Equipment") if e.get("slot") == "Body")


class TestEredLuinTable(unittest.TestCase):
    def test_ered_luin_rosters_carry_no_mount(self):
        mounts = [(rid, e.get("slot")) for rid, r in generated("eredluin").items()
                  for e in r.iter("Equipment") if e.get("slot") in MOUNT_SLOTS]
        self.assertEqual(mounts, [], "a dwarf must not ride out of character creation on a horse")

    def test_ered_luin_hands_out_only_dwarf_gear(self):
        foreign = sorted({bare(e.get("id")) for r in generated("eredluin").values() for e in r.iter("Equipment")
                          if not bare(e.get("id")).startswith(DWARF_GEAR)})
        self.assertEqual(foreign, [], "human gear on a dwarf")

    def test_ered_luin_writes_the_full_roster_set_under_its_own_culture(self):
        rosters = generated("eredluin")
        self.assertEqual(len(rosters), 55)
        self.assertEqual({r.get("culture") for r in rosters.values()}, {"Culture.eredluin"})

    def test_ered_luin_parents_dress_as_the_shipped_erebor_parents(self):
        ship = {r.get("id"): r for r in shipped().iter("EquipmentRoster")}
        gen = generated("eredluin")
        for parent in ("mother", "father"):
            for occ in OCCUPATIONS:
                with self.subTest(parent=parent, occupation=occ):
                    erebor = ship[f"{parent}_char_creation_{occ}_erebor"]
                    self.assertEqual(body_of(gen[f"{parent}_char_creation_{occ}_eredluin"]), body_of(erebor))


DALE_GEAR = ("dale_", "wm_dale_", "sk_dale_", "default_arrows", "sumpter_horse", "light_harness")


class TestDaleCloneTables(unittest.TestCase):
    """Anduin Vale (mounted) and Angaladh (on foot): tables authored from Dale items, since Dale has
    no table and no player_char_creation_sturgia_* rosters."""

    def test_anduin_vale_is_mounted_with_a_harness_on_every_horse(self):
        for rid, r in generated("anduinvale").items():
            slots = [e.get("slot") for e in r.iter("Equipment")]
            with self.subTest(roster=rid):
                self.assertEqual("Horse" in slots, "HorseHarness" in slots,
                                 "MOUNT_WITHOUT_HARNESS: a Horse needs a HorseHarness")

    def test_anduin_vale_has_a_horse_where_cardolan_has_one(self):
        horses = lambda c: sorted(rid.replace(c, "X") for rid, r in generated(c).items()  # noqa: E731
                                  if any(e.get("slot") == "Horse" for e in r.iter("Equipment")))
        self.assertEqual(horses("anduinvale"), horses("cardolan"))

    def test_angaladh_carries_no_mount(self):
        mounts = [(rid, e.get("slot")) for rid, r in generated("angaladh").items()
                  for e in r.iter("Equipment") if e.get("slot") in MOUNT_SLOTS]
        self.assertEqual(mounts, [], "Angaladh is a realm of foot soldiers")

    def test_both_hand_out_only_dale_gear(self):
        for culture in ("anduinvale", "angaladh"):
            foreign = sorted({bare(e.get("id")) for r in generated(culture).values() for e in r.iter("Equipment")
                              if not bare(e.get("id")).startswith(DALE_GEAR)})
            with self.subTest(culture=culture):
                self.assertEqual(foreign, [])

    def test_each_writes_the_full_roster_set_under_its_own_culture(self):
        for culture in ("anduinvale", "angaladh"):
            rosters = generated(culture)
            with self.subTest(culture=culture):
                self.assertEqual(len(rosters), 55)
                self.assertEqual({r.get("culture") for r in rosters.values()}, {"Culture." + culture})

    def test_shipped_rosters_are_present_and_match_the_mount_rule(self):
        for culture, mounted in (("anduinvale", True), ("angaladh", False)):
            rosters = [r for r in shipped().iter("EquipmentRoster") if r.get("culture") == "Culture." + culture]
            with self.subTest(culture=culture):
                self.assertEqual(len(rosters), 55, f"run generate_char_creation_equipment.py --append {culture} --apply")
                has_horse = any(e.get("slot") in MOUNT_SLOTS for r in rosters for e in r.iter("Equipment"))
                self.assertEqual(has_horse, mounted)


class TestParentBodiesAreOptional(unittest.TestCase):
    def test_a_culture_without_parent_bodies_keeps_the_old_shape(self):
        gondor = cc.CULTURES["gondor"]
        self.assertNotIn("mother_body", gondor)
        self.assertNotIn("father_body", gondor)
        gen = generated("gondor")
        self.assertEqual(body_of(gen["mother_char_creation_retainer_gondor"]), gondor["body_civilian"])
        self.assertEqual(body_of(gen["father_char_creation_retainer_gondor"]), gondor["body_military"])
        self.assertEqual(body_of(gen["father_char_creation_merchant_gondor"]), gondor["body_alt"])
        self.assertEqual(body_of(gen["father_char_creation_none_gondor"]), gondor["body_civilian"])


class TestShippedEredLuinRosters(unittest.TestCase):
    """The rosters in the shipped file, after the starter-kit wiring has repointed them."""

    def setUp(self):
        self.rosters = [r for r in shipped().iter("EquipmentRoster") if r.get("culture") == "Culture.eredluin"]

    def test_all_55_are_present(self):
        self.assertEqual(len(self.rosters), 55, "run generate_char_creation_equipment.py --append eredluin --apply")

    def test_none_carries_a_mount(self):
        mounts = [r.get("id") for r in self.rosters for e in r.iter("Equipment") if e.get("slot") in MOUNT_SLOTS]
        self.assertEqual(mounts, [])

    def test_every_item_is_dwarf_gear(self):
        foreign = sorted({f"{r.get('id')} {e.get('id')}" for r in self.rosters for e in r.iter("Equipment")
                          if not bare(e.get("id")).startswith(DWARF_GEAR)})
        self.assertEqual(foreign, [])


if __name__ == "__main__":
    unittest.main()
