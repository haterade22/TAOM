#!/usr/bin/env python3
"""Design tests for the Cardolan and Rhudaur troop generators (#978), Ered Luin's, the Dale-scaffolded
Anduin Vale, Angaladh and Dorwinion and the Rohan-scaffolded South Rhovanion, which share tools/realm_troops.py
with Arthedain's (tools/tests/test_generate_arthedain_troops.py covers that one).

Run:  python -m unittest tools.tests.test_realm_troops

Pure stdlib; each generator builds its tree in memory. The same invariants the validator would
otherwise report only after a write: the self-check passes, no upgrade lowers a skill, the militia
follow the TAOM rule, the tavern mercenary is a leaf copy, every bow or crossbow troop carries its
ranged-ladder cell, the party templates name only defined troops and the lord template sits at the men's
target. Ered Luin adds the dwarf options (race, body, a borrowed ladder line, crossbows, no mounted
dwarf) and the rule that a realm never becomes a borrowed armour piece's new lowest wearer below the
band another kingdom wears it at (deep review XML-1). Anduin Vale and Angaladh borrow Dale's armour,
whose ids carry no tier keyword: they never wear a Dale piece below Dale's lowest wearer, and shoot only
at the levels where the Dale bow ladder has a cell. South Rhovanion borrows Rohan's armour the same way,
rides on Rohan's mounts (every horse with a harness) and adds the horse-archer role.
"""
import dataclasses
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import derive_armor_tiers as dat  # noqa: E402
import generate_anduinvale_troops as gavt  # noqa: E402
import generate_angaladh_troops as gaht  # noqa: E402
import generate_cardolan_troops as gct  # noqa: E402
import generate_dorwinion_troops as gdot  # noqa: E402
import generate_eredluin_troops as gelt  # noqa: E402
import generate_rhudaur_troops as grt  # noqa: E402
import generate_southrhovanion_troops as gsrt  # noqa: E402
import ranged_ladder as rl  # noqa: E402
import realm_troops as rt  # noqa: E402
import rebalance_armor as ra  # noqa: E402

GENERATORS = (gct, grt, gelt, gavt, gaht, gdot, gsrt)
DALE_REALMS = (gavt, gaht, gdot)
LAUNCHER = {"ranged": "Bow", "crossbow": "Crossbow", "horsearcher": "Bow"}
BANDS = ("light", "medium", "heavy", "elite")


def built(gen):
    return gen.add_mercenary(gen.clamp_skills(gen.build()))


class RealmDesignTests(unittest.TestCase):
    def test_each_design_passes_its_self_check(self):
        for gen in GENERATORS:
            with self.subTest(culture=gen.CULTURE):
                self.assertEqual(gen.self_check(built(gen)), [])

    def _probe_errors(self, weapons):
        """Self-check errors for the first Cardolan troop re-armed with `weapons` in both rosters."""
        probe = dataclasses.replace(built(gct)[0], weapons=(list(weapons), list(weapons)), upgrades=[])
        return rt.self_check(lambda: [], [probe])

    def test_a_shield_with_a_two_handed_axe_fails_the_self_check(self):
        # Dunland's two-handed axes are named dunland_caerdh_axe_2h_*, with no greatsword or poleaxe in the id.
        errors = self._probe_errors(["dunland_caerdh_axe_2h_b", "sk_shield_round_a"])
        self.assertTrue(any("shield and a two-handed" in e for e in errors), errors)

    def test_a_shield_with_a_greatsword_fails_the_self_check(self):
        errors = self._probe_errors(["gondor_greatsword_a", "sk_shield_round_a"])
        self.assertTrue(any("shield and a two-handed" in e for e in errors), errors)

    def test_a_shield_with_a_great_axe_fails_the_self_check(self):
        # Dale's two-handed axe is dale_great_axe_a (TwoHandedAxe), with no _2h_ in the id.
        errors = self._probe_errors(["dale_great_axe_a", "wm_dale_shield_a06"])
        self.assertTrue(any("shield and a two-handed" in e for e in errors), errors)

    def test_a_shield_with_a_one_handed_weapon_passes_that_rule(self):
        errors = self._probe_errors(["gondor_sword_a", "sk_shield_round_a"])
        self.assertFalse(any("shield and a two-handed" in e for e in errors), errors)

    def test_no_upgrade_lowers_any_skill(self):
        for gen in GENERATORS:
            troops = built(gen)
            by_id = {t.id: t for t in troops}
            for t in troops:
                for u in t.upgrades:
                    for k in rt.SKILLS:
                        self.assertGreaterEqual(by_id[u].sk[k], t.sk[k], f"{t.id} -> {u} {k}")

    def test_the_elite_militia_is_the_basic_plus_the_bonus(self):
        for gen in GENERATORS:
            by_id = {t.id: t for t in built(gen)}
            for basic_id, vet_id in gen.MILITIA.items():
                basic, vet = by_id[basic_id], by_id[vet_id]
                self.assertEqual(vet.level, 16, vet_id)
                for k in rt.SKILLS:
                    if k == "Bow" and basic.role == "ranged":
                        continue  # the ladder owns a laddered archer's Bow
                    self.assertEqual(vet.sk[k], basic.sk[k] + rt.MILITIA_ELITE_BONUS, f"{vet_id} {k}")

    def test_the_mercenary_is_a_leaf_copy_of_the_rarest_pool_entry(self):
        for gen in GENERATORS:
            by_id = {t.id: t for t in built(gen)}
            merc = by_id[gen.MERC_SOURCE + "_merc"]
            self.assertEqual(merc.occupation, "Mercenary")
            self.assertEqual(merc.upgrades, [])
            self.assertEqual(merc.sk, by_id[gen.MERC_SOURCE].sk)

    def test_every_ranged_troop_carries_its_ladder_cell(self):
        spec = rl.load_spec()
        for gen in GENERATORS:
            # The realm's line, not its culture: Ered Luin shoots Erebor's ladder (REALM.line).
            self.assertIn(gen.REALM.line, {line["id"] for line in spec["lines"]}, gen.CULTURE)
            for t in built(gen):
                cls = LAUNCHER.get(t.role)
                if cls is None:
                    continue
                tier = rl.engine_tier(t.level)
                self.assertEqual(t.sk[cls], rl.skill_cell(gen.REALM.line, tier, spec), t.id)
                for roster in t.weapons:
                    self.assertEqual(roster[0], rl.ladder_id(gen.REALM.line, cls, tier), t.id)

    def test_every_party_template_names_only_defined_troops(self):
        for gen in GENERATORS:
            ids = {t.id for t in built(gen)}
            for tid, stacks in gen.party_templates():
                for troop, lo, hi in stacks:
                    self.assertIn(troop, ids, tid)
                    self.assertLessEqual(lo, hi, tid)

    def test_the_lord_template_is_this_generators_alone_at_the_mens_target(self):
        import rebalance_party_template_maxes as rb
        for gen in GENERATORS:
            tid = f"kingdom_hero_party_{gen.CULTURE}_template"
            self.assertEqual(rb.culture_for(tid), (None, None), f"second writer on {gen.REALM.pt_marker}")
            stacks = dict(gen.party_templates())[tid]
            self.assertEqual(sum(hi for _troop, _lo, hi in stacks), rb.CULTURE_TARGETS["gondor"], tid)

    def test_every_troop_wears_its_own_realms_label_and_culture(self):
        for gen in GENERATORS:
            root = ET.fromstring(gen.troops_xml(built(gen), "\r\n").encode("utf-8"))
            for npc in root.findall("NPCCharacter"):
                self.assertTrue(npc.get("id").startswith(gen.CULTURE + "_"), npc.get("id"))
                self.assertIn(gen.REALM.label, npc.get("name"))
                self.assertEqual(npc.get("culture"), f"Culture.{gen.CULTURE}")


class BorrowedArmourAnchorTests(unittest.TestCase):
    """The class table and the restat band an armour piece by its lowest battle wearer. A realm that
    borrows another kingdom's kit must not become that lowest wearer at a lower band, or the other
    kingdom's piece drops a class (Ered Luin's first tree moved 50 Erebor and Iron Hills pieces)."""

    def other_kingdoms_lowest_wearer(self, own_culture):
        """item -> its lowest anchor level among the other troop files, read as the class table reads
        them (derive_armor_tiers.parse_rosters: battle sets, noble anchors, exempt pairs)."""
        levels = {item: [w["level"] for w in ws if w["culture"] != own_culture]
                  for item, ws in dat.parse_rosters().items()}
        return {item: min(found) for item, found in levels.items() if found}

    @staticmethod
    def re_banded(troops, lowest):
        bad = []
        for t in troops:
            band = BANDS.index(ra.level_to_band(t.level))
            for gear in t.gear:
                for slot, item in gear.items():
                    if item in lowest and band < BANDS.index(ra.level_to_band(lowest[item])):
                        bad.append(f"{t.id} L{t.level} {slot} {item} (worn from L{lowest[item]} elsewhere)")
        return bad

    def test_ered_luin_never_re_bands_a_piece_another_kingdom_wears(self):
        lowest = self.other_kingdoms_lowest_wearer(gelt.REALM.culture)
        self.assertTrue(lowest, "read no other troop file: the gate would check nothing")
        troops = built(gelt)
        shared = {item for t in troops for gear in t.gear for item in gear.values()} & set(lowest)
        self.assertTrue(shared, "no Ered Luin piece is worn elsewhere: the ids no longer match, so the gate checks nothing")
        self.assertEqual(self.re_banded(troops, lowest), [])

    @staticmethod
    def dressed(troop_id, slot, item):
        t = next(t for t in built(gelt) if t.id == troop_id)
        return dataclasses.replace(t, gear=[dict(g, **{slot: item}) for g in t.gear])

    def test_a_low_level_troop_in_a_piece_worn_higher_elsewhere_is_caught(self):
        item = "sk_dwarf_erebor_chest_chain_a"
        lowest = self.other_kingdoms_lowest_wearer(gelt.REALM.culture)
        probe = self.dressed("eredluin_noble", "Body", item)
        self.assertEqual(ra.level_to_band(probe.level), "light")
        self.assertEqual(ra.level_to_band(lowest[item]), "medium")
        self.assertTrue(any(item in b for b in self.re_banded([probe], lowest)))

    def test_the_anchor_is_read_as_the_class_table_reads_it(self):
        # gondor_anf_vet_cavalry wears this cape at level 26 under an exempt (troop, item) pair, which
        # the class table skips, so the table bands the cape elite from level 31. A reader that keeps
        # the exempt pair sees level 26 (heavy) and passes a level 21 wearer that re-bands it.
        item = "sk_gd_ano_pauld_cape_inf_elite_a"
        lowest = self.other_kingdoms_lowest_wearer(gelt.REALM.culture)
        self.assertEqual(ra.level_to_band(lowest[item]), "elite")
        probe = self.dressed("eredluin_hall_warden", "Cape", item)
        self.assertTrue(any(item in b for b in self.re_banded([probe], lowest)))


class DaleRealmTests(unittest.TestCase):
    """Anduin Vale, Angaladh and Dorwinion are scaffolded from Dale: its armour (ids with no tier keyword, so
    ARMOUR_MESH_TIER_LADDER cannot check them), its bow ladder and its weapons."""

    FIXED_IDS = {
        gavt: {"anduinvale_levy", "anduinvale_beorning_youth", "anduinvale_beorning_youth_merc",
               "anduinvale_militia_spearman", "anduinvale_militia_archer",
               "anduinvale_militia_veteran_spearman", "anduinvale_militia_veteran_archer",
               "anduinvale_bear_warden"},
        gaht: {"angaladh_levy", "angaladh_ironwood_youth", "angaladh_ironwood_youth_merc",
               "angaladh_militia_spearman", "angaladh_militia_archer",
               "angaladh_militia_veteran_spearman", "angaladh_militia_veteran_archer",
               "angaladh_woodmen_guard"},
        gdot: {"dorwinion_levy", "dorwinion_noble", "dorwinion_noble_merc",
               "dorwinion_militia_spearman", "dorwinion_militia_archer",
               "dorwinion_militia_veteran_spearman", "dorwinion_militia_veteran_archer",
               "dorwinion_vineholm_guard"},
    }

    @staticmethod
    def dale_lowest_wearer():
        """Dale item -> its lowest anchor level among Dale's own troops, as the class table reads it."""
        levels = {item: [w["level"] for w in ws if w["culture"] == "dale"]
                  for item, ws in dat.parse_rosters().items()}
        return {item: min(found) for item, found in levels.items() if found}

    @staticmethod
    def worn_too_low(troops, lowest):
        return [f"{t.id} L{t.level} {slot} {item} (Dale wears it from L{lowest[item]})"
                for t in troops for gear in t.gear for slot, item in gear.items()
                if item in lowest and t.level < lowest[item]]

    def test_the_fixed_ids_other_work_depends_on_exist(self):
        for gen in DALE_REALMS:
            ids = {t.id for t in built(gen)}
            self.assertLessEqual(self.FIXED_IDS[gen], ids, gen.CULTURE)

    def test_the_capstone_is_level_36_and_on_foot(self):
        for gen, capstone in ((gavt, "anduinvale_bear_warden"), (gaht, "angaladh_woodmen_guard"),
                               (gdot, "dorwinion_vineholm_guard")):
            troop = next(t for t in built(gen) if t.id == capstone)
            self.assertEqual(troop.level, 36)
            self.assertIsNone(troop.horse)
            self.assertEqual(troop.upgrades, [])

    def test_no_dale_piece_is_worn_below_its_lowest_dale_wearer(self):
        lowest = self.dale_lowest_wearer()
        self.assertTrue(lowest, "read no Dale troop: the gate would check nothing")
        for gen in DALE_REALMS:
            troops = built(gen)
            worn = {item for t in troops for gear in t.gear for item in gear.values()}
            self.assertTrue(worn & set(lowest), f"{gen.CULTURE} wears no Dale piece the gate can read")
            self.assertEqual(self.worn_too_low(troops, lowest), [], gen.CULTURE)

    def test_the_gate_catches_a_dale_piece_worn_too_low(self):
        lowest = self.dale_lowest_wearer()
        item = "sk_dale_helmet_infrantry_a04"   # Dale's level 31 helmet
        self.assertEqual(lowest[item], 31)
        levy = next(t for t in built(gavt) if t.id == "anduinvale_levy")
        probe = dataclasses.replace(levy, gear=[dict(g, Head=item) for g in levy.gear])
        self.assertEqual(len(self.worn_too_low([probe], lowest)), len(probe.gear))

    def test_no_other_kingdoms_armour_class_moves(self):
        checker = BorrowedArmourAnchorTests()
        for gen in DALE_REALMS:
            lowest = checker.other_kingdoms_lowest_wearer(gen.REALM.culture)
            self.assertEqual(checker.re_banded(built(gen), lowest), [], gen.CULTURE)

    def test_every_archer_stands_on_a_level_the_dale_ladder_has_a_bow_cell_for(self):
        bow_tiers = set(rl.tiers_for("dale", "Bow", rl.load_spec()))
        self.assertTrue(bow_tiers)
        for gen in DALE_REALMS:
            archers = [t for t in built(gen) if t.role == "ranged"]
            self.assertTrue(archers, gen.CULTURE)
            for t in archers:
                self.assertIn(rl.engine_tier(t.level), bow_tiers, t.id)

    def test_the_dale_ladder_claims_every_dale_realms_troop_file(self):
        spec = rl.load_spec()
        dale = next(line for line in spec["lines"] if line["id"] == "dale")
        for gen in DALE_REALMS:
            self.assertIn(gen.CULTURE, dale["files"])

    def test_angaladh_never_rides(self):
        for t in built(gaht):
            self.assertIsNone(t.horse, t.id)
            self.assertNotIn(t.group, ("Cavalry", "HorseArcher"), t.id)

    def test_every_dale_realm_horse_carries_a_harness(self):
        for gen in (gavt, gdot):
            riders = [t for t in built(gen) if t.horse]
            self.assertTrue(riders, gen.CULTURE)
            for t in riders:
                self.assertTrue(t.horse[0] and t.horse[1], t.id)
                self.assertEqual(t.group, "Cavalry", t.id)

    def test_dorwinion_rides_only_the_river_branch(self):
        mounted = {t.id for t in built(gdot) if t.horse}
        self.assertEqual(mounted, {"dorwinion_river_rider", "dorwinion_river_cavalier"})

    def test_every_troop_wears_dales_face_and_no_race_attribute(self):
        for gen in DALE_REALMS:
            root = ET.fromstring(gen.troops_xml(built(gen), "\r\n").encode("utf-8"))
            for npc in root.findall("NPCCharacter"):
                self.assertIsNone(npc.get("race"), npc.get("id"))
                self.assertEqual(npc.find("face/face_key_template").get("value"), "BodyProperty.fighter_sturgia")

    def test_the_party_templates_are_the_nine_cardolan_names(self):
        wanted = {tid.replace("cardolan", "{c}") for tid, _ in gct.party_templates()}
        for gen in DALE_REALMS:
            self.assertEqual({tid.replace(gen.CULTURE, "{c}") for tid, _ in gen.party_templates()}, wanted)


class RohanRealmTests(unittest.TestCase):
    """South Rhovanion is scaffolded from Rohan: its armour (ids with no tier keyword), its mounts, its
    bow ladder and its weapons. The Armory's wm_rohan_* blades top out at 47 DPS, so a troop from level 26
    on carries only the vanilla blades that reach the tier 5 band (tools/melee_ladders.json)."""

    FIXED_IDS = {"southrhovanion_levy", "southrhovanion_northman_youth", "southrhovanion_northman_youth_merc",
                 "southrhovanion_militia_spearman", "southrhovanion_militia_archer",
                 "southrhovanion_militia_veteran_spearman", "southrhovanion_militia_veteran_archer",
                 "southrhovanion_horse_lord"}

    @staticmethod
    def rohan_lowest_wearer():
        """Rohan item -> its lowest anchor level among Rohan's own troops, as the class table reads it."""
        levels = {item: [w["level"] for w in ws if w["culture"] == "rohan"]
                  for item, ws in dat.parse_rosters().items()}
        return {item: min(found) for item, found in levels.items() if found}

    def test_the_fixed_ids_other_work_depends_on_exist(self):
        self.assertLessEqual(self.FIXED_IDS, {t.id for t in built(gsrt)})

    def test_the_capstone_is_level_36_mounted_and_a_leaf(self):
        troop = next(t for t in built(gsrt) if t.id == "southrhovanion_horse_lord")
        self.assertEqual(troop.level, 36)
        self.assertTrue(troop.horse)
        self.assertEqual(troop.upgrades, [])

    def test_no_rohan_piece_is_worn_below_its_lowest_rohan_wearer(self):
        lowest = self.rohan_lowest_wearer()
        self.assertTrue(lowest, "read no Rohan troop: the gate would check nothing")
        troops = built(gsrt)
        worn = {item for t in troops for gear in t.gear for item in gear.values()}
        self.assertGreater(len(worn & set(lowest)), 20, "few Rohan pieces match: the ids no longer line up")
        self.assertEqual(DaleRealmTests.worn_too_low(troops, lowest), [])

    def test_the_gate_catches_a_rohan_piece_worn_too_low(self):
        lowest = self.rohan_lowest_wearer()
        item = "cts_rohan_royal_guard_armor"   # Rohan's level 31 body
        self.assertEqual(lowest[item], 31)
        levy = next(t for t in built(gsrt) if t.id == "southrhovanion_levy")
        probe = dataclasses.replace(levy, gear=[dict(g, Body=item) for g in levy.gear])
        self.assertEqual(len(DaleRealmTests.worn_too_low([probe], lowest)), len(probe.gear))

    def test_no_other_kingdoms_armour_class_moves(self):
        checker = BorrowedArmourAnchorTests()
        lowest = checker.other_kingdoms_lowest_wearer(gsrt.REALM.culture)
        self.assertEqual(checker.re_banded(built(gsrt), lowest), [])

    def test_every_archer_stands_on_a_level_the_rohan_ladder_has_a_bow_cell_for(self):
        bow_tiers = set(rl.tiers_for("rohan", "Bow", rl.load_spec()))
        archers = [t for t in built(gsrt) if t.role in ("ranged", "horsearcher")]
        self.assertTrue(archers)
        for t in archers:
            self.assertIn(rl.engine_tier(t.level), bow_tiers, t.id)

    def test_the_rohan_ladder_claims_the_troop_file(self):
        rohan = next(line for line in rl.load_spec()["lines"] if line["id"] == "rohan")
        self.assertIn(gsrt.CULTURE, rohan["files"])

    def test_every_mounted_troop_has_a_horse_and_a_harness_and_every_horse_a_mounted_troop(self):
        mounted = [t for t in built(gsrt) if t.group in ("Cavalry", "HorseArcher")]
        self.assertTrue(mounted)
        for t in mounted:
            self.assertTrue(t.horse and t.horse[0] and t.horse[1], t.id)
        for t in built(gsrt):
            if t.horse:
                self.assertIn(t.group, ("Cavalry", "HorseArcher"), t.id)

    def test_the_horse_archers_ride_and_shoot_from_the_ladder(self):
        archers = [t for t in built(gsrt) if t.role == "horsearcher"]
        self.assertEqual(len(archers), 3)
        for t in archers:
            self.assertEqual(t.group, "HorseArcher", t.id)
            self.assertGreaterEqual(t.sk["Riding"], rt.RIDING[t.level], t.id)

    def test_from_level_26_no_armory_rohan_blade_sits_under_the_band(self):
        for t in built(gsrt):
            if t.level >= 26:
                for roster in t.weapons:
                    self.assertEqual([w for w in roster if w.startswith(("wm_rohan_ws_", "wm_rohan_spear"))],
                                     [], t.id)

    def test_every_troop_wears_rohans_face_and_no_race_attribute(self):
        root = ET.fromstring(gsrt.troops_xml(built(gsrt), "\r\n").encode("utf-8"))
        for npc in root.findall("NPCCharacter"):
            self.assertIsNone(npc.get("race"), npc.get("id"))
            self.assertEqual(npc.find("face/face_key_template").get("value"), "BodyProperty.fighter_rohan")

    def test_the_party_templates_are_the_nine_cardolan_names(self):
        wanted = {tid.replace("cardolan", "{c}") for tid, _ in gct.party_templates()}
        self.assertEqual({tid.replace(gsrt.CULTURE, "{c}") for tid, _ in gsrt.party_templates()}, wanted)


class DwarfRealmOptionTests(unittest.TestCase):
    """Realm.race, Realm.body, Realm.ladder_line, the crossbow role and the mounted-dwarf check."""

    def test_a_dwarf_realm_writes_race_and_its_face_template_on_every_troop(self):
        root = ET.fromstring(gelt.troops_xml(built(gelt), "\r\n").encode("utf-8"))
        for npc in root.findall("NPCCharacter"):
            self.assertEqual(npc.get("race"), "dwarf", npc.get("id"))
            self.assertEqual(npc.find("face/face_key_template").get("value"), "BodyProperty.fighter_erebor")

    def test_a_realm_of_men_writes_no_race_attribute(self):
        root = ET.fromstring(gct.troops_xml(built(gct), "\r\n").encode("utf-8"))
        for npc in root.findall("NPCCharacter"):
            self.assertIsNone(npc.get("race"), npc.get("id"))
            self.assertEqual(npc.find("face/face_key_template").get("value"), f"BodyProperty.{rt.BODY}")

    def test_the_ladder_line_defaults_to_the_culture(self):
        self.assertEqual(gct.REALM.line, "cardolan")
        self.assertEqual(gelt.REALM.line, "erebor")

    def test_a_crossbow_troop_carries_a_ladder_crossbow_and_the_crossbow_skill(self):
        troops = [t for t in built(gelt) if t.role == "crossbow"]
        self.assertTrue(troops)
        for t in troops:
            self.assertTrue(all(r[0].startswith("ladder_erebor_xbow_t") for r in t.weapons), t.id)
            self.assertGreater(t.sk["Crossbow"], t.sk["Bow"], t.id)

    def _probe(self, gen, **change):
        return dataclasses.replace(built(gen)[0], upgrades=[], **change)

    def test_a_mounted_dwarf_fails_the_self_check(self):
        probe = self._probe(gelt, horse=("t2_empire_horse", "gondor_horse_armor_4"))
        self.assertTrue(any("MOUNTED_DWARF" in e for e in rt.self_check(lambda: [], [probe])))

    def test_a_dwarf_in_a_cavalry_group_fails_the_self_check(self):
        probe = self._probe(gelt, group="Cavalry")
        self.assertTrue(any("MOUNTED_DWARF" in e for e in rt.self_check(lambda: [], [probe])))

    def test_a_mounted_man_passes_that_rule(self):
        probe = self._probe(gct, horse=("t2_empire_horse", "gondor_horse_armor_4"), group="Cavalry")
        self.assertFalse(any("MOUNTED_DWARF" in e for e in rt.self_check(lambda: [], [probe])))


class SharedModuleTests(unittest.TestCase):
    def test_a_full_item_id_takes_no_prefix(self):
        realm = rt.Realm("x", "[X]", "x.py", "X-PT", {}, "x_y", "Y")
        a, b = rt.armour(realm, ("=sk_gd_cape_a", "cape_b"), ("chest_a", "chest_b"), None, None, None)
        self.assertEqual(a["Head"], "sk_gd_cape_a")
        self.assertEqual(b["Head"], "sk_ar_art_cape_b")
        self.assertEqual(a["Body"], "sk_ar_art_chest_a")
        self.assertNotIn("Leg", a)


if __name__ == "__main__":
    unittest.main()
