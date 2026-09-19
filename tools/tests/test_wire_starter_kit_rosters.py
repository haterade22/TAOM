#!/usr/bin/env python3
"""Unit tests for tools/wire_starter_kit_rosters.py, on synthetic XML.

Run:  python -m unittest tools.tests.test_wire_starter_kit_rosters

The rewiring is an in-place attribute substitution, so the traps are: touching a roster
that is not the player's (childhood, education, show, the parents), missing the civilian
set, rewiring a mount, re-prefixing an id that already starts with `starter_`, and
rewriting formatting the file did not ask for. The vanilla-six override is generated from
the career rosters, so its traps are: a roster without the replace attribute (the engine
would APPEND a second battle set instead of replacing), a borrowed culture pointing at the
wrong kit, and a civilian set that hands the player a bow. Since #629 the career rosters
name real troop items, so the last trap is rewiring them into starter_ twins that do not exist.
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import wire_starter_kit_rosters as wk  # noqa: E402

CC = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n'
    "<EquipmentRosters>\r\n"
    "\t<EquipmentRoster\r\n"
    '\t\tid="player_char_creation_gondor_retainer_m"\r\n'
    '\t\tculture="Culture.gondor">\r\n'
    "\t\t<EquipmentSet>\r\n"
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Item0"\r\n'
    '\t\t\t\tid="Item.wm_gondor_sword_a01" />\r\n'
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Item1"\r\n'
    '\t\t\t\tid="Item.gondor_steel_bow_starter" />\r\n'
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Body"\r\n'
    '\t\t\t\tid="Item.gondor_noble_coat_a" />\r\n'
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Leg"\r\n'
    '\t\t\t\tid="Item.starter_infantry_gondor_leg_a" />\r\n'
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Horse"\r\n'
    '\t\t\t\tid="Item.saddle_horse" />\r\n'
    "\t\t</EquipmentSet>\r\n"
    '\t\t<EquipmentSet equipmentType="Civilian">\r\n'
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Item0"\r\n'
    '\t\t\t\tid="Item.wm_gondor_sword_a01" />\r\n'
    "\t\t</EquipmentSet>\r\n"
    "\t</EquipmentRoster>\r\n"
    "\t<EquipmentRoster\r\n"
    '\t\tid="player_char_creation_childhood_age_gondor_retainer_m"\r\n'
    '\t\tculture="Culture.gondor">\r\n'
    "\t\t<EquipmentSet>\r\n"
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Item0"\r\n'
    '\t\t\t\tid="Item.childhood_stick" />\r\n'
    "\t\t</EquipmentSet>\r\n"
    "\t</EquipmentRoster>\r\n"
    "\t<EquipmentRoster\r\n"
    '\t\tid="mother_char_creation_gondor_retainer"\r\n'
    '\t\tculture="Culture.gondor">\r\n'
    "\t\t<EquipmentSet>\r\n"
    "\t\t\t<Equipment\r\n"
    '\t\t\t\tslot="Body"\r\n'
    '\t\t\t\tid="Item.mother_dress" />\r\n'
    "\t\t</EquipmentSet>\r\n"
    "\t</EquipmentRoster>\r\n"
    "</EquipmentRosters>\r\n"
)

CAREER = """<?xml version="1.0" encoding="utf-8"?>
<EquipmentRosters>
    <EquipmentRoster id="player_career_khuzait_ranged_m" culture="Culture.khuzait">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.starter_composite_bow" />
            <Equipment slot="Item1" id="Item.starter_bodkin_arrows_a" />
            <Equipment slot="Item2" id="Item.starter_empire_sword_1_t2" />
            <Equipment slot="Body" id="Item.starter_ranged_khuzait_body_a" />
            <Equipment slot="Leg" id="Item.starter_ranged_khuzait_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_career_khuzait_ranged_f" culture="Culture.khuzait">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.starter_composite_bow" />
            <Equipment slot="Item1" id="Item.starter_bodkin_arrows_a" />
            <Equipment slot="Item2" id="Item.starter_empire_sword_1_t2" />
            <Equipment slot="Body" id="Item.starter_ranged_khuzait_body_a" />
            <Equipment slot="Leg" id="Item.starter_ranged_khuzait_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_career_khuzait_cavalry_m" culture="Culture.khuzait">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.starter_easterling_spear" />
            <Equipment slot="Item1" id="Item.starter_battered_kite_shield" />
            <Equipment slot="Item2" id="Item.starter_empire_sword_1_t2" />
            <Equipment slot="Body" id="Item.starter_cavalry_khuzait_body_a" />
            <Equipment slot="Leg" id="Item.starter_cavalry_khuzait_leg_a" />
            <Equipment slot="Horse" id="Item.saddle_horse" />
            <Equipment slot="HorseHarness" id="Item.light_harness" />
        </EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_career_khuzait_cavalry_f" culture="Culture.khuzait">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.starter_easterling_spear" />
            <Equipment slot="Item1" id="Item.starter_battered_kite_shield" />
            <Equipment slot="Item2" id="Item.starter_empire_sword_1_t2" />
            <Equipment slot="Body" id="Item.starter_cavalry_khuzait_body_a" />
            <Equipment slot="Leg" id="Item.starter_cavalry_khuzait_leg_a" />
            <Equipment slot="Horse" id="Item.saddle_horse" />
            <Equipment slot="HorseHarness" id="Item.light_harness" />
        </EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_career_khuzait_infantry_m" culture="Culture.khuzait">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.starter_empire_sword_1_t2" />
            <Equipment slot="Item1" id="Item.starter_battered_kite_shield" />
            <Equipment slot="Item2" id="Item.starter_easterling_spear" />
            <Equipment slot="Body" id="Item.starter_infantry_khuzait_body_a" />
            <Equipment slot="Leg" id="Item.starter_infantry_khuzait_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_career_khuzait_infantry_f" culture="Culture.khuzait">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.starter_empire_sword_1_t2" />
            <Equipment slot="Item1" id="Item.starter_battered_kite_shield" />
            <Equipment slot="Item2" id="Item.starter_easterling_spear" />
            <Equipment slot="Body" id="Item.starter_infantry_khuzait_body_a" />
            <Equipment slot="Leg" id="Item.starter_infantry_khuzait_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>
</EquipmentRosters>
"""

TITLES = {"khuzait": ["bard", "guard", "hunter", "infantry", "retainer", "skirmisher"],
          "battania": ["guard", "hunter"]}


class TestRewire(unittest.TestCase):
    def test_rewrites_only_target_rosters_and_both_sets(self):
        out, changed, rosters = wk.rewire_text(CC)
        self.assertEqual(rosters, ["player_char_creation_gondor_retainer_m"])
        self.assertEqual(out.count('id="Item.starter_wm_gondor_sword_a01"'), 2, "battle AND civilian set")
        self.assertIn('id="Item.starter_gondor_noble_coat_a"', out)
        self.assertIn('id="Item.childhood_stick"', out)
        self.assertIn('id="Item.mother_dress"', out)
        self.assertEqual(changed, 4)

    def test_skips_mounts_and_already_starter_ids(self):
        out, _, _ = wk.rewire_text(CC)
        self.assertIn('id="Item.saddle_horse"', out)
        self.assertNotIn("starter_saddle_horse", out)
        self.assertEqual(out.count("starter_infantry_gondor_leg_a"), 1)
        self.assertNotIn("starter_starter_", out)

    def test_strips_trailing_starter_suffix(self):
        out, _, _ = wk.rewire_text(CC)
        self.assertIn('id="Item.starter_gondor_steel_bow"', out)
        self.assertNotIn("gondor_steel_bow_starter", out)

    def test_preserves_formatting_byte_for_byte_outside_the_ids(self):
        out, _, _ = wk.rewire_text(CC)
        restored = (out.replace("Item.starter_wm_gondor_sword_a01", "Item.wm_gondor_sword_a01")
                       .replace("Item.starter_gondor_steel_bow", "Item.gondor_steel_bow_starter")
                       .replace("Item.starter_gondor_noble_coat_a", "Item.gondor_noble_coat_a"))
        self.assertEqual(restored, CC)
        self.assertEqual(out.count("\r\n"), CC.count("\r\n"))

    def test_idempotent(self):
        once, n1, _ = wk.rewire_text(CC)
        twice, n2, rosters = wk.rewire_text(once)
        self.assertEqual(twice, once)
        self.assertEqual(n2, 0)
        self.assertEqual(rosters, [])
        self.assertGreater(n1, 0)


class TestVanillaOverride(unittest.TestCase):
    def setUp(self):
        self.text = wk.build_vanilla_override(CAREER, TITLES, {"battania": "khuzait"}, eol="\n")
        self.root = ET.fromstring(self.text.encode("utf-8"))

    def test_one_roster_per_culture_title_sex_with_the_replace_attribute(self):
        rosters = list(self.root.iter("EquipmentRoster"))
        self.assertEqual(len(rosters), (6 + 2) * 2)
        ids = {r.get("id") for r in rosters}
        self.assertIn("player_char_creation_khuzait_retainer_f", ids)
        self.assertIn("player_char_creation_battania_hunter_m", ids)
        for roster in rosters:
            self.assertEqual(roster.get("_replaceWhileMerging"), "true", roster.get("id"))
            self.assertEqual(roster.get("culture"), "Culture." + roster.get("id").split("_")[3])

    def test_battle_set_is_the_mapped_career_kit(self):
        r = next(r for r in self.root.iter("EquipmentRoster") if r.get("id") == "player_char_creation_battania_hunter_m")
        battle = [s for s in r.findall("EquipmentSet") if s.get("equipmentType") is None]
        self.assertEqual(len(battle), 1)
        rows = {e.get("slot"): e.get("id") for e in battle[0].findall("Equipment")}
        self.assertEqual(rows["Item0"], "Item.starter_composite_bow")
        self.assertEqual(rows["Body"], "Item.starter_ranged_khuzait_body_a")
        r = next(r for r in self.root.iter("EquipmentRoster") if r.get("id") == "player_char_creation_khuzait_retainer_f")
        rows = {e.get("slot"): e.get("id") for e in r.findall("EquipmentSet")[0].findall("Equipment")}
        self.assertEqual(rows["Horse"], "Item.saddle_horse")
        self.assertEqual(rows["HorseHarness"], "Item.light_harness")

    def test_civilian_set_is_sword_body_leg_only(self):
        for r in self.root.iter("EquipmentRoster"):
            civ = [s for s in r.findall("EquipmentSet") if s.get("equipmentType") == "Civilian"]
            self.assertEqual(len(civ), 1, r.get("id"))
            rows = {e.get("slot"): e.get("id") for e in civ[0].findall("Equipment")}
            self.assertEqual(set(rows), {"Item0", "Body", "Leg"}, r.get("id"))
            self.assertEqual(rows["Item0"], "Item.starter_empire_sword_1_t2", r.get("id"))

    def test_unknown_title_or_missing_career_roster_fails_loudly(self):
        with self.assertRaises(wk.WireError):
            wk.build_vanilla_override(CAREER, {"khuzait": ["prophet"]}, {}, eol="\n")
        with self.assertRaises(wk.WireError):
            wk.build_vanilla_override(CAREER, {"vlandia": ["guard"]}, {}, eol="\n")

    def test_override_copies_career_ids_verbatim(self):
        career_ids = {e.get("id") for e in ET.fromstring(CAREER.encode("utf-8")).iter("Equipment")}
        for e in self.root.iter("Equipment"):
            self.assertIn(e.get("id"), career_ids)


# Since #629 the career rosters name the culture's lowest troop items, not starter_ twins.
CAREER_TROOP = CAREER.replace("starter_composite_bow", "ladder_rhun_new_bow_t2") \
                     .replace("starter_empire_sword_1_t2", "sm_rh_loke_1h_sword_a") \
                     .replace("starter_ranged_khuzait_body_a", "sk_rh_loke_tunic_a")


class TestCareerKitIsNotRewired(unittest.TestCase):
    """#629: career kits are real troop items. The rewire pass must never turn them into
    starter_ ids (no such twins exist), and the override must copy them as they are."""

    def test_rewire_files_exclude_the_career_rosters(self):
        self.assertEqual(wk.REWIRE_FILES, (wk.CC_FILE,))
        self.assertNotIn(wk.CAREER_FILE, wk.REWIRE_FILES)

    def test_civilian_sidearm_slot_holds_a_one_handed_weapon_of_any_kind(self):
        # Dunland's is an axe; the error must not say "sword"
        broken = CAREER.replace('<Equipment slot="Item2" id="Item.starter_empire_sword_1_t2" />', "")
        with self.assertRaises(wk.WireError) as ctx:
            wk.build_vanilla_override(broken, {"khuzait": ["hunter"]}, {}, eol="\n")
        self.assertIn("one-handed weapon", str(ctx.exception))

    def test_override_keeps_troop_items(self):
        text = wk.build_vanilla_override(CAREER_TROOP, TITLES, wk.BORROW, eol="\n")
        root = ET.fromstring(text.encode("utf-8"))
        r = next(r for r in root.iter("EquipmentRoster") if r.get("id") == "player_char_creation_battania_hunter_m")
        battle, civ = r.findall("EquipmentSet")
        rows = {e.get("slot"): e.get("id") for e in battle.findall("Equipment")}
        self.assertEqual(rows["Item0"], "Item.ladder_rhun_new_bow_t2")
        self.assertEqual(rows["Body"], "Item.sk_rh_loke_tunic_a")
        civ_rows = {e.get("slot"): e.get("id") for e in civ.findall("Equipment")}
        self.assertEqual(civ_rows["Item0"], "Item.sm_rh_loke_1h_sword_a")
        self.assertNotIn("Item.starter_ladder_", text)
        self.assertNotIn("Item.starter_sm_rh_loke", text)

    def test_committed_override_is_what_the_tool_builds_from_the_committed_career_file(self):
        # the override is generated from the career kit; editing the career file without
        # re-running the tool leaves the six vanilla-mapped starts on the old kit
        built = wk.build_vanilla_override(wk.gk.read_xml(wk.CAREER_FILE)[0],
                                          wk.titles_from_menus(wk.MENUS_DIR), wk.BORROW)
        committed = wk.gk.read_xml(wk.OVERRIDE_FILE)[0]
        self.assertEqual(committed.replace("\r\n", "\n"), built.replace("\r\n", "\n"),
                         "re-run python tools/wire_starter_kit_rosters.py --apply")


VANILLA_SETS = """<EquipmentRosters>
    <EquipmentRoster id="player_char_creation_khuzait_hunter_m" culture="Culture.khuzait"><EquipmentSet /></EquipmentRoster>
    <EquipmentRoster id="npc_disguised_hero_equipment_template"><EquipmentSet /></EquipmentRoster>
</EquipmentRosters>"""


class TestOverrideIdsExistInVanilla(unittest.TestCase):
    """An override roster whose id vanilla lacks does not append: MBObjectManager.MergeElements
    merges it into the FIRST roster of the accumulated document (1.5.3, lines 846 and 857-859),
    so that roster loses its id and its content. The tool must refuse to write one."""

    def test_every_id_present_is_accepted(self):
        override = '<EquipmentRosters><EquipmentRoster id="player_char_creation_khuzait_hunter_m" /></EquipmentRosters>'
        self.assertEqual(wk.missing_vanilla_ids(override, VANILLA_SETS), [])

    def test_an_id_vanilla_lacks_is_reported(self):
        override = ('<EquipmentRosters><EquipmentRoster id="player_char_creation_khuzait_hunter_m" />'
                    '<EquipmentRoster id="player_char_creation_khuzait_prophet_f" /></EquipmentRosters>')
        self.assertEqual(wk.missing_vanilla_ids(override, VANILLA_SETS), ["player_char_creation_khuzait_prophet_f"])


if __name__ == "__main__":
    unittest.main()
