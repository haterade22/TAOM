#!/usr/bin/env python3
"""Unit tests for the kingdom-cap armour curve in tools/rebalance_armor.py (#583).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_kingdom_caps.py

THE CONTRACT
------------
Each kingdom has a chest cap (the maintainer's table, 2026-09-13). The elite band's body_armor
equals the cap; helmet, bracer, pauldron and greaves are fixed ratios of it; the bands below are
fixed ratios of the elite band; the lord band is 7 percent above it (2026-11-14). Sub-lines sharing a
folder are routed by id prefix. The writer takes an item's band from its lowest troop wearer, keeps
weights and material_type, prices secondary stats from the item's own share of its primary (zero below
the heavy band, capped from it), re-scopes extremity loot tables, lowers hero-only kit to the lord band
and keyword-less unworn kit to the elite band without ever raising either, and writes byte-faithfully
with one dated backup per file.
"""
import contextlib
import io
import math
import os
import re
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import analyze_armor_balance as ab  # noqa: E402
import derive_armor_tiers as dat  # noqa: E402
import rebalance_armor as ra  # noqa: E402

FOLDERS = {"arnor", "dale", "dol_guldur", "dunland", "erebor", "gondor", "gundabad", "harad", "iron_hills",
           "isengard", "mercenary", "mirkwood", "mordor", "rhun", "rivendell", "rohan", "thenn", "troll"}
LINE_KEYS = {"mordor_numenorean", "mordor_uruk", "mordor_orc", "lindon", "goblin", "mistymountainorcs",
             "umbar", "khand", "lothlorien"}


def _item(iid, itype, **attrs):
    stats = " ".join(f'{k}="{v}"' for k, v in attrs.items() if k.endswith("_armor"))
    weight = attrs.get("weight", 3.0)
    mat = attrs.get("material_type", "Plate")
    mod = attrs.get("modifier_group", "plate")
    return (f'  <Item id="{iid}" name="{{=k}}{iid}" Type="{itype}" weight="{weight}">\n'
            f'    <ItemComponent><Armor {stats} material_type="{mat}" modifier_group="{mod}" /></ItemComponent>\n'
            f"  </Item>\n")


def _file(*items, crlf=False, bom=False):
    text = '<?xml version="1.0" encoding="utf-8"?>\n<Items>\n' + "".join(items) + "</Items>\n"
    if crlf:
        text = text.replace("\n", "\r\n")
    return (b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8")


class CapTableTests(unittest.TestCase):
    def test_caps_match_the_maintainers_table(self):
        want = {"erebor": 52, "iron_hills": 52, "rivendell": 51, "lindon": 51, "mirkwood": 48,
                "lothlorien": 47, "gondor": 46, "rhun": 46, "mordor_numenorean": 46, "arnor": 46,
                "dale": 43, "gundabad": 42, "dol_guldur": 41, "khand": 41, "isengard": 40,
                "harad": 40, "umbar": 40, "mercenary": 40, "mordor_uruk": 39, "rohan": 37,
                "dunland": 37, "mordor_orc": 36, "mistymountainorcs": 36, "goblin": 36, "thenn": 35}
        self.assertEqual(ra.KINGDOM_CAPS, want)
        self.assertNotIn("troll", ra.KINGDOM_CAPS)  # the troll stays on the legacy curve

    def test_best_body_sum_stays_in_the_vanilla_range(self):
        """2026-11-14: chest plus shoulder of an elite troop is at most 67, the body armour of
        vanilla's best armoured troop; Erebor, the top cap, sits exactly on it."""
        for kingdom, cap in ra.KINGDOM_CAPS.items():
            total = ra.cap_value(cap, "body", "elite") + ra.cap_value(cap, "shoulder", "elite")
            self.assertLessEqual(total, 67, kingdom)
        cap = ra.KINGDOM_CAPS["erebor"]
        self.assertEqual(ra.cap_value(cap, "body", "elite") + ra.cap_value(cap, "shoulder", "elite"), 67)

    def test_every_cap_key_is_a_folder_or_a_routed_line(self):
        self.assertEqual(set(ra.KINGDOM_CAPS) - FOLDERS - LINE_KEYS, set())

    def test_ratios(self):
        """2026-11-14: the bands fall from 0.40/0.64/0.84 to 0.16/0.35/0.68 so an elite can one-shot a
        recruit again; the head slot rises to 1.04 and takes its own light and heavy bands; the lord band
        sits 7 percent above elite; a secondary stat exists from the heavy band only."""
        self.assertEqual(ra.SLOT_CAP_RATIO, {"body": 1.0, "head": 1.04, "arm": 0.5, "shoulder": 0.28, "leg": 0.45})
        self.assertEqual(ra.BAND_RATIO, {"light": 0.16, "medium": 0.35, "heavy": 0.68, "elite": 1.0, "lord": 1.07})
        self.assertEqual(ra.SLOT_BAND_RATIO, {"head": {"light": 0.24, "heavy": 0.82}})
        self.assertEqual(ra.SECONDARY_FROM_BAND, "heavy")
        self.assertEqual(ra.SECONDARY_SHARE_CAP,
                         {("body", "arm_armor"): 0.30, ("body", "leg_armor"): 0.40, ("shoulder", "arm_armor"): 1.0})

    def test_the_curve_reproduces_the_decided_parameter_table(self):
        """Head, body (chest plus cape), arm and leg of a whole kit at fixed shares, as the decision
        table of 2026-11-14 lists them (Erebor, Gondor, Rohan, orcs, Thenn; the lord column)."""
        def kit(cap, tier):
            c = lambda slot: ra.cap_value(cap, slot, tier)  # noqa: E731
            chest, cape = c("body"), c("shoulder")
            ks = lambda slot, stat, p, ratio: ra.kingdom_secondary(slot, stat, tier, p, ratio)  # noqa: E731
            return (c("head"), chest + cape,
                    c("arm") + ks("body", "arm_armor", chest, 1.0) + ks("shoulder", "arm_armor", cape, 1.0),
                    c("leg") + ks("body", "leg_armor", chest, 1.0))
        caps = ra.KINGDOM_CAPS
        want = {
            ("erebor", "light"): (13, 10, 4, 4), ("erebor", "medium"): (19, 23, 9, 8),
            ("erebor", "heavy"): (44, 45, 39, 30), ("erebor", "elite"): (54, 67, 57, 44),
            ("erebor", "lord"): (58, 72, 61, 47),
            ("gondor", "light"): (11, 9, 4, 3), ("gondor", "medium"): (17, 21, 8, 7),
            ("gondor", "heavy"): (39, 40, 34, 26), ("gondor", "elite"): (48, 59, 50, 39),
            ("gondor", "lord"): (51, 63, 54, 42),
            ("rohan", "light"): (9, 8, 3, 3), ("rohan", "medium"): (13, 17, 6, 6),
            ("rohan", "heavy"): (32, 32, 28, 21), ("rohan", "elite"): (38, 47, 40, 32),
            ("rohan", "lord"): (41, 51, 43, 34),
            ("mordor_orc", "heavy"): (31, 31, 26, 21), ("mordor_orc", "elite"): (37, 46, 39, 30),
            ("mordor_orc", "lord"): (40, 50, 42, 33),
            ("thenn", "light"): (9, 8, 3, 3), ("thenn", "medium"): (13, 15, 6, 6),
            ("thenn", "heavy"): (30, 31, 26, 21), ("thenn", "elite"): (36, 45, 39, 30),
            ("thenn", "lord"): (39, 47, 40, 32),
        }
        for (kingdom, tier), expected in want.items():
            self.assertEqual(kit(caps[kingdom], tier), expected, f"{kingdom} {tier}")

    def test_the_lord_band_is_at_most_five_above_elite_per_region_on_every_kingdom(self):
        """R3b of the 2026-11-14 decision: a lord set sums at most 5 over the same kingdom's elite set."""
        for kingdom, cap in ra.KINGDOM_CAPS.items():
            for slot in ra.SLOT_CAP_RATIO:
                self.assertLessEqual(ra.cap_value(cap, slot, "lord") - ra.cap_value(cap, slot, "elite"),
                                     5, f"{kingdom} {slot}")

    def test_the_best_elite_head_and_body_stay_inside_the_vanilla_best_troop(self):
        """R3a: the best elite set per region is at most vanilla's best troop (head 54, body 67)."""
        for kingdom, cap in ra.KINGDOM_CAPS.items():
            self.assertLessEqual(ra.cap_value(cap, "head", "elite"), 54, kingdom)

    def test_a_secondary_is_zero_below_the_heavy_band_and_capped_from_it(self):
        ks = ra.kingdom_secondary
        for tier in ("civilian", "light", "medium"):
            self.assertEqual(ks("body", "leg_armor", tier, 30, 0.56), 0, tier)
        self.assertEqual(ks("body", "leg_armor", "heavy", 31, 0.56), 12)    # capped at 0.40: 12.4
        self.assertEqual(ks("body", "leg_armor", "heavy", 31, 0.10), 3)     # a small own ratio wins: 3.1
        self.assertEqual(ks("body", "arm_armor", "elite", 46, 0.5), 14)     # capped at 0.30: 13.8
        self.assertEqual(ks("shoulder", "arm_armor", "elite", 13, 0.9), 12)
        self.assertEqual(ks("shoulder", "arm_armor", "elite", 13, 1.4), 13)  # capped at 1.0
        self.assertEqual(ks("body", "leg_armor", "elite", 46, 0), 0)        # no stat on the item
        self.assertEqual(ks("body", "head_armor", "elite", 46, 0.5), 0)     # a stat the table has no share for


class RoutingTests(unittest.TestCase):
    def test_kingdom_key_routes_folders_and_sub_lines(self):
        k = ra.kingdom_key
        self.assertEqual(k("sk_md_num_hood_elite_a", "mordor"), "mordor_numenorean")
        self.assertEqual(k("sm_md_num_grvs_elite_a", "mordor"), "mordor_numenorean")
        self.assertEqual(k("sk_uruk_mordor_helmet_heavy_a", "mordor"), "mordor_uruk")
        for iid in ("sk_md_mor_inf_helmet_elite_a", "sk_md_orc_inf_chest_heavy_e", "sk_gn_orc_x"):
            self.assertEqual(k(iid, "mordor"), "mordor_orc", iid)
        self.assertEqual(k("witch_king_helmet", "mordor"), "mordor_numenorean")   # a lord's kit, A1 below
        self.assertEqual(k("urukscout_helmet", "mordor"), "isengard")
        self.assertEqual(k("ar_ardunian_elite_armour", "mordor"), "umbar")
        self.assertEqual(k("sk_dg_khml_plate_elite_a", "rhun"), "dol_guldur")
        self.assertEqual(k("sk_rh_drag_plate_heavy_b", "rhun"), "rhun")
        self.assertEqual(k("sk_dwarf_x", "iron_hills"), "iron_hills")
        self.assertEqual(k("sk_dwarf_x", "erebor"), "erebor")
        self.assertEqual(k("rivendell_torso_x", "rivendell"), "rivendell")
        self.assertEqual(k("anything", "lindon"), "lindon")
        self.assertIsNone(k("lotr_troll_armor", "troll"))
        self.assertIsNone(k("x", "nowhere"))


    def test_mordor_lords_named_kit_is_priced_on_the_numenorean_line(self):
        """The mordor/ folder defaults to the orc cap (36), which put Sauron's own torso at 39 body
        armour, under a Gondor elite troop (59). Named kit of Mordor's lords takes the numenorean line
        (cap 46, as Gondor), whatever the folder default is (2026-11-14, #1271)."""
        k = ra.kingdom_key
        for iid in ("sauron_torso", "sauron_helmet_player", "nazgul_armor", "nazgul_v1_pauldrons",
                    "khamul_body", "witch_king", "witch_king_boots", "sk_mordor_mouth_of_sauron_helm"):
            self.assertEqual(k(iid, "mordor"), "mordor_numenorean", iid)
        self.assertEqual(k("witchking_gloves", "mordor"), "mordor_numenorean")
        # only the Mordor folder: Dol Guldur's `sk_dg_khml_` troop line and the other folders keep their lines
        self.assertEqual(k("sk_dg_khml_plate_elite_a", "rhun"), "dol_guldur")
        self.assertEqual(k("sauron_ward_helm", "rohan"), "rohan")
        # the orc and uruk kit of the same folder stay on their own caps
        self.assertEqual(k("sk_md_orc_inf_helmet_elite_a", "mordor"), "mordor_orc")
        self.assertEqual(k("sk_uruk_mordor_helmet_heavy_a", "mordor"), "mordor_uruk")
        self.assertEqual(ra.KINGDOM_CAPS[ra.MORDOR_LORD_KIT_LINE], ra.KINGDOM_CAPS["gondor"])


class CurveTests(unittest.TestCase):
    def test_calculate_stats_on_the_cap_model(self):
        g = lambda tier, slot: ra.calculate_stats(tier, slot, "gondor")  # noqa: E731
        self.assertEqual(g("elite", "body")["body_armor"], 46)
        self.assertEqual(g("elite", "head")["head_armor"], 48)     # 47.84
        self.assertEqual(g("elite", "arm")["arm_armor"], 23)
        self.assertEqual(g("elite", "shoulder")["body_armor"], 13)
        self.assertEqual(g("elite", "leg")["leg_armor"], 21)
        self.assertEqual(g("lord", "body")["body_armor"], 49)      # 49.22: the lord band is 7 percent above elite
        self.assertEqual(g("heavy", "body")["body_armor"], 31)     # 31.28
        self.assertEqual(g("medium", "body")["body_armor"], 16)    # 16.1
        self.assertEqual(g("light", "body")["body_armor"], 7)      # 7.36
        self.assertEqual(g("light", "head")["head_armor"], 11)     # the head's own light band, 0.24: 11.48
        self.assertEqual(g("heavy", "head")["head_armor"], 39)     # the head's own heavy band, 0.82: 39.2
        self.assertEqual(ra.calculate_stats("elite", "head", "erebor")["head_armor"], 54)
        self.assertEqual(ra.calculate_stats("elite", "leg", "isengard")["leg_armor"], 18)  # 18.0
        self.assertEqual(ra.calculate_stats("elite", "body", "mordor", item_id="sk_md_num_x")["body_armor"], 46)
        self.assertEqual(ra.calculate_stats("elite", "body", "mordor", item_id="sk_uruk_mordor_x")["body_armor"], 39)
        self.assertEqual(ra.calculate_stats("elite", "body", "mordor", item_id="sk_md_orc_x")["body_armor"], 36)
        self.assertEqual(ra.calculate_stats("elite", "body", "rhun", item_id="sk_dg_khml_x")["body_armor"], 41)

    def test_cap_model_row_secondaries_are_capped_and_start_at_the_heavy_band(self):
        st = ra.calculate_stats("elite", "body", "gondor")
        self.assertEqual(st["body_armor"], 46)
        self.assertEqual(st["leg_armor"], 18)          # the legacy 28/50 would be 26; the share cap is 0.40: 18.4
        self.assertIn("weight", st)
        self.assertEqual(st["material_type"], "Plate")
        cape = ra.calculate_stats("heavy", "shoulder", "gondor")
        self.assertEqual(cape["body_armor"], 9)       # 46 * 0.28 * 0.68 = 8.76
        self.assertEqual(cape["arm_armor"], 8)        # 9 * 11 / 13 = 7.6 -> 8
        light = ra.calculate_stats("light", "shoulder", "thenn")
        self.assertEqual((light["body_armor"], light["arm_armor"]), (2, 0))   # 35 * .28 * .16 = 1.57; no secondary
        self.assertEqual(ra.calculate_stats("medium", "body", "gondor")["leg_armor"], 0)

    def test_civilian_and_uncapped_cultures_stay_on_the_legacy_curve(self):
        self.assertEqual(ra.calculate_stats("civilian", "body", "gondor")["body_armor"],
                         ra.BODY_BASELINES["civilian"]["body_armor"] + 1)
        self.assertEqual(ra.calculate_stats("elite", "body", "troll")["body_armor"], 50 + 8)
        self.assertEqual(ra.calculate_stats("elite", "body", "troll")["leg_armor"], 28 + 5)

    def test_tier_from_value_routes_a_sub_line_by_item_id(self):
        """A Dol Guldur helmet at 37 (its own elite) read as Rhun heavy when the value
        was judged on the folder's cap alone (Codex, 2026-09-13; numbers rescaled 2026-11-14 and again for the new bands)."""
        self.assertEqual(ra.tier_from_value(43, "head", "rhun"), "heavy")
        self.assertEqual(ra.tier_from_value(43, "head", "rhun", item_id="sk_dg_khml_helmet_x"), "elite")
        self.assertEqual(ra.tier_from_value(48, "head", "rhun", item_id="sk_rh_loke_helmet_x"), "elite")

    def test_weight_is_the_legacy_ladder(self):
        self.assertEqual(ra.calculate_stats("elite", "head", "gondor")["weight"],
                         round(ra.HEAD_BASELINES["elite"]["weight"] * ra.CULTURAL_MODS["gondor"]["weight_mult"], 1))

    def test_level_to_band_is_shared_with_the_derivation(self):
        self.assertEqual([ra.level_to_band(l) for l in (1, 13, 14, 18, 19, 30, 31, 51)],
                         ["light", "light", "medium", "medium", "heavy", "heavy", "elite", "elite"])
        self.assertIs(dat.level_to_tier, ra.level_to_band)

    def test_black_numenoreans_are_no_longer_excluded_but_heroes_are(self):
        self.assertFalse(ra.is_excluded("sk_md_num_hood_elite_a", "[Mordor] Black Numenorean Hood"))
        self.assertFalse(ab.is_excluded("sm_md_num_grvs_elite_a", "[Mordor] Black Numenorean Greaves"))
        self.assertTrue(ra.is_excluded("khamul_helmet", "Khamul's Helmet"))
        self.assertTrue(ra.is_excluded("gf_helmet_a", "Glorfindel's Helm"))
        self.assertTrue(ra.is_excluded("thranduil_crown", "Thranduil's Crown"))
        self.assertTrue(ra.is_excluded("sk_dwarf_dain_helmet_elite_a", "[Erebor] Dain Elite Helmet A"))
        # The Dol Guldur troop line is NAMED after Khamul; it is not his kit.
        for fn in (ra.is_excluded, ab.is_excluded):
            self.assertFalse(fn("sk_dg_khml_plate_elite_a", "[Rhun] Khamul Heavy Plate Armor"), fn)
            self.assertTrue(fn("khamul_body", "Khamul's Armour"), fn)

    def test_extremity_loot_tables_are_scoped(self):
        for slot in ("arm", "leg", "shoulder"):
            # Light and medium take the +3 table: a +5 cloth roll let a light cape pass a heavy one and a
            # medium cape pass an elite one. Heavy and up take leather, whose down-rolls (-2, -3) fit a
            # 7 to 28 point stat where chain's (-6, -8) did not.
            self.assertEqual([ra.modifier_group_for(slot, t) for t in ra.TIERS],
                             ["cloth_unarmoured"] * 3 + ["leather"] * 3, slot)
        self.assertEqual(ra.modifier_group_for("body", "elite"), "plate")
        self.assertEqual(ra.modifier_group_for("head", "medium"), "chain")

    def test_kingdom_curve_clears_the_two_tier_invariant(self):
        violations = ab.check_kingdom_curve_invariant()
        self.assertEqual([], violations, "\n".join(
            f"{v['kingdom']} {v['slot']} {v['lo_tier']}->{v['hi_tier']}: hi={v['hi']} <= lo={v['lo']} + {v['lego']}"
            for v in violations))

    def test_kingdom_invariant_reports_a_compressed_cap(self):
        old = dict(ra.KINGDOM_CAPS)
        try:
            ra.KINGDOM_CAPS["thenn"] = 20
            self.assertTrue(any(v["kingdom"] == "thenn" for v in ab.check_kingdom_curve_invariant()))
        finally:
            ra.KINGDOM_CAPS.clear()
            ra.KINGDOM_CAPS.update(old)


class AnchorTests(unittest.TestCase):
    """derive_armor_tiers anchors an item to its lowest BATTLE wearer that is not ladder-exempt."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        troops = Path(self._tmp.name) / "troops"
        troops.mkdir()
        exempt = sorted(dat.LADDER_EXEMPT_TROOPS)[0]
        troops.joinpath("troops_alpha.xml").write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="alpha_t9" level="46"><Equipments>'
            '<EquipmentRoster><equipment slot="Body" id="Item.a_chest" /></EquipmentRoster>'
            '<EquipmentRoster civilian="true"><equipment slot="Body" id="Item.a_dress" /></EquipmentRoster>'
            '<EquipmentSet equipmentType="Civilian"><equipment slot="Head" id="Item.a_hat" /></EquipmentSet>'
            "</Equipments></NPCCharacter>"
            f'<NPCCharacter id="{exempt}" level="51"><Equipments>'
            '<EquipmentRoster><equipment slot="Head" id="Item.a_hood" /></EquipmentRoster>'
            "</Equipments></NPCCharacter>"
            '<NPCCharacter id="alpha_t3" level="16"><Equipments>'
            '<EquipmentRoster><equipment slot="Head" id="Item.a_hood" /></EquipmentRoster>'
            "</Equipments></NPCCharacter>"
            "</NPCCharacters>", encoding="utf-8")
        self._old = dat.TROOPS_DIR
        dat.TROOPS_DIR = str(troops)

    def tearDown(self):
        dat.TROOPS_DIR = self._old
        self._tmp.cleanup()

    def test_civilian_sets_and_exempt_troops_never_anchor(self):
        w = dat.parse_rosters()
        self.assertEqual([x["level"] for x in w["a_chest"]], [46])
        self.assertNotIn("a_dress", w)
        self.assertNotIn("a_hat", w)
        self.assertEqual([x["troop"] for x in w["a_hood"]], ["alpha_t3"])  # the exempt L51 wearer is not there

    def test_exempt_set_is_the_validators(self):
        import taom_schema as ts
        self.assertEqual(dat.LADDER_EXEMPT_TROOPS, frozenset(ts.Validator._ARMOUR_LADDER_EXEMPT))
        self.assertEqual(dat.NOBLE_TROOPS, frozenset(ts.Validator._NOBLE_LINE_TROOPS))
        self.assertEqual(dat.LADDER_EXEMPT_ITEMS, frozenset(ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS))

    def test_a_noble_anchors_one_band_up_and_an_exempt_pair_does_not_anchor(self):
        old = (dat.NOBLE_TROOPS, dat.LADDER_EXEMPT_ITEMS)
        try:
            dat.NOBLE_TROOPS = frozenset({"alpha_t3"})
            dat.LADDER_EXEMPT_ITEMS = frozenset({("alpha_t9", "a_chest")})
            w = dat.parse_rosters()
            # alpha_t3 is level 16 (medium band); as a noble it anchors at 19, the heavy band.
            self.assertEqual([(x["troop"], x["level"]) for x in w["a_hood"]], [("alpha_t3", 19)])
            self.assertNotIn("a_chest", w)
        finally:
            dat.NOBLE_TROOPS, dat.LADDER_EXEMPT_ITEMS = old

    def test_an_exempt_pair_leaves_the_troops_other_items_anchoring_and_a_noble_is_floored(self):
        tmp = tempfile.TemporaryDirectory()
        troops = Path(tmp.name) / "troops"
        troops.mkdir()
        troops.joinpath("troops_beta.xml").write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="beta_grunt" level="21"><Equipments><EquipmentRoster>'
            '<equipment slot="Body" id="Item.b_chest_elite_a" /><equipment slot="Head" id="Item.b_helmet_med_a" />'
            "</EquipmentRoster></Equipments></NPCCharacter>"
            '<NPCCharacter id="beta_noble" level="11"><Equipments><EquipmentRoster>'
            '<equipment slot="Head" id="Item.b_helm_heavy_a" /><equipment slot="Leg" id="Item.b_grvs_med_a" />'
            "</EquipmentRoster></Equipments></NPCCharacter>"
            "</NPCCharacters>", encoding="utf-8")
        old = (dat.TROOPS_DIR, dat.NOBLE_TROOPS, dat.LADDER_EXEMPT_ITEMS)
        try:
            dat.TROOPS_DIR = str(troops)
            dat.NOBLE_TROOPS = frozenset({"beta_noble"})
            dat.LADDER_EXEMPT_ITEMS = frozenset({("beta_grunt", "b_chest_elite_a")})
            w = dat.parse_rosters()
            self.assertNotIn("b_chest_elite_a", w)                                  # the pair
            self.assertEqual([x["level"] for x in w["b_helmet_med_a"]], [21])       # the troop's other slot
            self.assertEqual([x["level"] for x in w["b_helm_heavy_a"]], [19])       # floored at heavy
            self.assertEqual([x["level"] for x in w["b_grvs_med_a"]], [14])         # a band up
        finally:
            dat.TROOPS_DIR, dat.NOBLE_TROOPS, dat.LADDER_EXEMPT_ITEMS = old
            tmp.cleanup()

    def test_keyword_detector_knows_civilian_kit(self):
        self.assertEqual(dat.id_keyword_tier("sk_gd_civ_heavy_coat_a"), "civilian")
        self.assertEqual(dat.id_keyword_tier("sk_dwarf_civilian_dress_a"), "civilian")
        self.assertEqual(dat.id_keyword_tier("sk_gd_x_helmet_heavy_a"), "heavy")
        self.assertIsNone(dat.id_keyword_tier("sk_dale_helmet_archer_a03"))

    def test_the_map_is_anchor_first_like_the_writer(self):
        """The map's tier/target/status columns describe what --tier-source roster-first does: a
        worn item takes its lowest wearer's band even over an id keyword; the keyword decides
        only for unworn kit; civilian keyword kit is civilian whoever wears it."""
        old_index = dat.build_armory_index
        try:
            dat.build_armory_index = lambda: ({
                "a_chest": {"folder": "gondor", "slot": "body", "primary": 42, "weight": 1.0, "name": "x"},
                "a_hood": {"folder": "gondor", "slot": "head", "primary": 15, "weight": 1.0, "name": "x"},
                "sk_gd_x_helmet_heavy_a": {"folder": "gondor", "slot": "head", "primary": 33, "weight": 1.0, "name": "x"},
                "sk_gd_x_helmet_lord_a": {"folder": "gondor", "slot": "head", "primary": 40, "weight": 1.0, "name": "x"},
                "sk_gd_civ_coat_heavy_a": {"folder": "gondor", "slot": "body", "primary": 8, "weight": 1.0, "name": "x"},
            }, "fixture")
            troops = Path(self._tmp.name) / "troops"
            troops.joinpath("troops_alpha.xml").write_text(
                "<NPCCharacters>"
                '<NPCCharacter id="alpha_t9" level="46"><Equipments><EquipmentRoster>'
                '<equipment slot="Head" id="Item.sk_gd_x_helmet_heavy_a" />'
                '<equipment slot="Body" id="Item.sk_gd_civ_coat_heavy_a" />'
                "</EquipmentRoster></Equipments></NPCCharacter>"
                "</NPCCharacters>", encoding="utf-8")
            recs, _ = dat.derive()
        finally:
            dat.build_armory_index = old_index
        self.assertEqual((recs["sk_gd_x_helmet_heavy_a"]["tier"], recs["sk_gd_x_helmet_heavy_a"]["tierSource"]),
                         ("elite", "roster(L46)"))
        self.assertEqual(recs["sk_gd_x_helmet_heavy_a"]["target"], 48)
        self.assertEqual((recs["sk_gd_x_helmet_lord_a"]["tier"], recs["sk_gd_x_helmet_lord_a"]["tierSource"]),
                         ("lord", "id-keyword"))
        self.assertEqual(recs["sk_gd_civ_coat_heavy_a"]["tier"], "civilian")   # worn at 46, still civilian
        self.assertEqual(recs["a_hood"]["tierSource"], "unworn")

    def test_a_generator_owned_item_is_marked_so_in_the_map_not_under_or_over(self):
        """The restat skips sk_ll_ items, so the map must not list them as work for the restat."""
        old_index = dat.build_armory_index
        try:
            dat.build_armory_index = lambda: ({
                "sk_ll_gl_chest_heavy_a": {"folder": "lothlorien", "slot": "body", "primary": 20, "weight": 1.0, "name": "x"},
                "sk_gd_x_chest_heavy_a": {"folder": "gondor", "slot": "body", "primary": 20, "weight": 1.0, "name": "x"},
            }, "fixture")
            troops = Path(self._tmp.name) / "troops"
            troops.joinpath("troops_alpha.xml").write_text(
                "<NPCCharacters>"
                '<NPCCharacter id="alpha_t6" level="31"><Equipments><EquipmentRoster>'
                '<equipment slot="Body" id="Item.sk_ll_gl_chest_heavy_a" />'
                "</EquipmentRoster></Equipments></NPCCharacter>"
                '<NPCCharacter id="alpha_t7" level="36"><Equipments><EquipmentRoster>'
                '<equipment slot="Body" id="Item.sk_gd_x_chest_heavy_a" />'
                "</EquipmentRoster></Equipments></NPCCharacter>"
                "</NPCCharacters>", encoding="utf-8")
            recs, _ = dat.derive()
        finally:
            dat.build_armory_index = old_index
        self.assertEqual(recs["sk_ll_gl_chest_heavy_a"]["status"], "generator-owned")
        self.assertIn(recs["sk_gd_x_chest_heavy_a"]["status"], ("under", "over"))   # an ordinary item is still judged


class WriterTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.armory = Path(self._tmp.name) / "LOTRLOME_items"
        gondor = self.armory / "gondor"
        mordor = self.armory / "mordor"
        gondor.mkdir(parents=True)
        mordor.mkdir(parents=True)
        (gondor / "body_armors.xml").write_bytes(_file(
            _item("sk_gd_x_chest_heavy_a", "BodyArmor", body_armor=42, arm_armor=14, weight=18.5),
            _item("sk_gd_x_chest_unworn_a", "BodyArmor", body_armor=42, arm_armor=14),
            _item("sk_gd_x_chest_lord_a", "BodyArmor", body_armor=50, arm_armor=20),
            _item("sk_gd_x_chest_civ_a", "BodyArmor", body_armor=8, arm_armor=0, material_type="Cloth", modifier_group="cloth"),
            _item("boromir_jerkin", "BodyArmor", body_armor=60, arm_armor=30),
            _item("sk_gd_x_chest_hero_low", "BodyArmor", body_armor=44, arm_armor=12),
            _item("sk_gd_x_chest_unworn_hi", "BodyArmor", body_armor=60, arm_armor=30),
            _item("sk_gd_x_chest_unworn_civ", "BodyArmor", body_armor=60, arm_armor=30),
            _item("lotr_troll_chest", "BodyArmor", body_armor=90, arm_armor=40),
            crlf=True))
        (gondor / "head_armors.xml").write_bytes(_file(
            _item("sk_gd_x_helm_heroonly", "HeadArmor", head_armor=70),
            _item("erkenbrand_helmet", "HeadArmor", head_armor=70)))
        (gondor / "arm_armors.xml").write_bytes(_file(
            _item("sk_gd_x_bracer_med_a", "HandArmor", arm_armor=15, material_type="Chainmail", modifier_group="plate"),
            bom=True))
        (mordor / "shoulder_armors.xml").write_bytes(_file(
            _item("sk_md_num_pauld_elite_a", "Cape", body_armor=24, arm_armor=20),
            _item("sk_md_orc_pauldron_med_b", "Cape", body_armor=8, arm_armor=0)))
        self.roster = {
            "sk_gd_x_chest_heavy_a": {"anchorLevel": 46, "tier": "heavy", "tierSource": "id-keyword"},
            "sk_gd_x_chest_lord_a": {"anchorLevel": None, "tier": "lord", "tierSource": "id-keyword"},
            "sk_gd_x_chest_unworn_a": {"anchorLevel": None, "tier": None, "tierSource": "unworn"},
            "sk_gd_x_chest_civ_a": {"anchorLevel": 6, "tier": "civilian", "tierSource": "id-keyword"},
            # hero-only kit: worn by a hero, no troop wears it (derive_armor_tiers records heroWorn)
            "boromir_jerkin": {"anchorLevel": None, "tier": "lord", "tierSource": "id-keyword", "heroWorn": True},
            "sk_gd_x_chest_hero_low": {"anchorLevel": None, "tier": None, "tierSource": "unworn", "heroWorn": True},
            "sk_gd_x_helm_heroonly": {"anchorLevel": None, "tier": None, "tierSource": "unworn", "heroWorn": True},
            # hero-named kit a troop wears: it goes on the troop curve at its lowest wearer's band
            "erkenbrand_helmet": {"anchorLevel": 36, "tier": "elite", "tierSource": "roster(L36)", "heroWorn": True},
            # unworn merchandise: a keyword-less item is lowered to the elite band at most
            "sk_gd_x_chest_unworn_hi": {"anchorLevel": None, "tier": None, "tierSource": "unworn"},
            "sk_gd_x_chest_unworn_civ": {"anchorLevel": None, "tier": "civilian", "tierSource": "id-keyword"},
            # a troll wears its own plate; the troop curve never reaches it
            "lotr_troll_chest": {"anchorLevel": 51, "tier": "elite", "tierSource": "roster(L51)"},
            "sk_gd_x_bracer_med_a": {"anchorLevel": 16, "tier": "medium", "tierSource": "id-keyword"},
            "sk_md_num_pauld_elite_a": {"anchorLevel": 41, "tier": "elite", "tierSource": "id-keyword"},
            "sk_md_orc_pauldron_med_b": {"anchorLevel": 21, "tier": "medium", "tierSource": "id-keyword"},
        }

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, folder, fname, slot, dry_run=True, **kw):
        return ra.process_file(str(self.armory / folder / fname), slot, dry_run=dry_run,
                               tier_source="roster-first", roster_map=self.roster,
                               keep_weights=True, keep_material_type=True, backup_tag="t", **kw)

    def test_roster_first_takes_the_lowest_wearers_band_and_keywords_only_for_unworn_kit(self):
        by_id = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body")}
        self.assertEqual(by_id["sk_gd_x_chest_heavy_a"]["tier"], "elite")     # worn at 46, keyword says heavy
        self.assertEqual(by_id["sk_gd_x_chest_heavy_a"]["new_values"]["body_armor"], 46)
        # Unworn merchandise never prices above elite, even with a lord keyword (2026-11-14).
        self.assertEqual(by_id["sk_gd_x_chest_lord_a"]["tier"], "elite")
        self.assertEqual(by_id["sk_gd_x_chest_lord_a"]["new_values"]["body_armor"], 46)
        # no band: the elite clamp leaves the primary (42 < 47) and cuts the arm armour to the 0.30 share: 12.6
        self.assertEqual(by_id["sk_gd_x_chest_unworn_a"]["new_values"], {"body_armor": 42, "arm_armor": 13})
        self.assertEqual(by_id["sk_gd_x_chest_civ_a"]["status"], "UNCHANGED")     # civilian: skipped

    def test_the_plan_of_hero_worn_and_unworn_lord_kit(self):
        def plan(rec):
            return ra._roster_first_plan({"x": rec}, "x", "x", "gondor")
        hero_only = {"anchorLevel": None, "tier": None, "tierSource": "unworn", "heroWorn": True}
        self.assertEqual(plan(hero_only), ("lord", "clamp"))
        keyword_lord = {"anchorLevel": None, "tier": "lord", "tierSource": "id-keyword"}
        self.assertEqual(plan(keyword_lord), ("elite", "full"))
        # a troop wearer decides before a hero does, and a hero never moves the troop's band
        both = {"anchorLevel": 16, "tier": "medium", "tierSource": "roster(L16)", "heroWorn": True}
        self.assertEqual(plan(both), ("medium", "full"))
        # the tier-only map of --tier-source roster: no wearers are known, a lord tier counts as elite
        self.assertEqual(ra._roster_first_plan({"x": "lord"}, "x", "x", "gondor"), ("elite", "full"))

    def test_hero_only_lord_kit_is_priced_at_the_lord_band_and_hero_named_kit_stays_clamped(self):
        """Lord kit 2026-11-15: a lord line piece only heroes wear is restated AT the lord band (it can
        rise), so a lettered lord chest a lord template takes is not left at a stale light value."""
        hero_only = {"anchorLevel": None, "tier": "lord", "tierSource": "id-keyword", "heroWorn": True,
                     "lordWorn": True}
        for iid in ("sk_gd_x_chest_lord_b", "sk_gd_x_noble_chest_a", "roh_nbl_arm_b", "x_chainmail_captain_a1"):
            self.assertEqual(ra._roster_first_plan({iid: hero_only}, iid, "Chest", "gondor"), ("lord", "full"), iid)
        # a lord-line piece only a companion or wanderer wears is not lord kit: companions stay light
        companion_only = dict(hero_only, lordWorn=False)
        self.assertEqual(ra._roster_first_plan({"roh_nbl_shldr_s": companion_only}, "roh_nbl_shldr_s", "Shoulder",
                                               "gondor"), ("lord", "clamp"))
        # a hero-named piece keeps the lower-only clamp, and so does hero kit with no lord marker
        self.assertEqual(ra._roster_first_plan({"sk_gd_x_chest_lord_c": hero_only}, "sk_gd_x_chest_lord_c",
                                               "Boromir's Lord Chest", "gondor"), ("lord", "clamp"))
        self.assertEqual(ra._roster_first_plan({"sk_gd_x_mail": hero_only}, "sk_gd_x_mail", "Mail", "gondor"),
                         ("lord", "clamp"))
        # unworn lord kit still prices as elite merchandise
        unworn = {"anchorLevel": None, "tier": "lord", "tierSource": "id-keyword"}
        self.assertEqual(ra._roster_first_plan({"sk_gd_x_chest_lord_b": unworn}, "sk_gd_x_chest_lord_b", "C", "gondor"),
                         ("elite", "full"))

    def test_the_lord_kit_marker_reads_whole_tokens(self):
        for iid in ("x_chest_lord_a", "rivendell_torso_lord3_silver", "roh_nbl_arm_1", "sk_gd_lam_nob_helmet_a",
                    "gondor_noble_coat_a", "cts_rohan_armor_captain", "x_king_helm"):
            self.assertTrue(ra.is_lord_kit(iid), iid)
        for iid in ("x_lordly_robe", "x_chest_elite_a", "cts_rohan_royal_guard_armor", "x_kingsfoil", "nobody_cap"):
            self.assertFalse(ra.is_lord_kit(iid), iid)

    def test_hero_only_kit_is_clamped_to_the_lord_band_and_never_raised(self):
        by_id = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body")}
        c = by_id["boromir_jerkin"]
        self.assertEqual(c["status"], "CHANGED")
        self.assertEqual(c["tier"], "lord")
        # 46 * 1.07 = 49.2; the chest's arm armour is 30/60 of the body, capped at 0.30: 49 * 0.3 = 14.7
        self.assertEqual(c["new_values"], {"body_armor": 49, "arm_armor": 15})
        self.assertEqual((c["new_weight"], c["new_material"], c["new_modifier"]),
                         (c["old_weight"], c["old_material"], c["old_modifier"]))
        low = by_id["sk_gd_x_chest_hero_low"]               # 44 is under the lord cap of 49: nothing to lower
        self.assertEqual(low["new_values"], {"body_armor": 44, "arm_armor": 12})
        self.assertEqual(low["status"], "UNCHANGED")
        head = {c["id"]: c for c in self._run("gondor", "head_armors.xml", "head")}
        self.assertEqual(head["sk_gd_x_helm_heroonly"]["new_values"], {"head_armor": 51})   # 46 * 1.04 * 1.07

    def _clamped(self, slot, folder_file, *items, hero_worn=True):
        """Write `items` into a fresh file as kit nobody but a hero wears (or nobody at all) and return
        the clamp plan keyed by id."""
        for iid in [m for it in items for m in re.findall(r'id="([^"]+)"', it)]:
            self.roster.setdefault(iid, {"anchorLevel": None, "tier": None, "tierSource": "unworn",
                                         "heroWorn": hero_worn})
        path = self.armory / "gondor" / folder_file
        path.write_bytes(_file(*items, crlf=True))
        return {c["id"]: c for c in self._run("gondor", folder_file, slot)}

    def test_a_hero_only_heavy_extremity_leaves_plate_and_chain_for_the_lord_loot_table(self):
        """The group decides the roll of a lord's drop, so the clamp moves the piece to its band's table."""
        for kind, group in (("plate", "plate"), ("chain", "chain")):
            by_id = self._clamped("arm", "arm_armors_clamp.xml",
                                  _item("boromir_bracer_" + kind, "HandArmor", arm_armor=60, modifier_group=group))
            c = by_id["boromir_bracer_" + kind]
            self.assertEqual((c["tier"], c["status"], c["new_modifier"]), ("lord", "CHANGED", "leather"), kind)
            self.assertEqual(c["new_material"], c["old_material"])

    def test_a_clamped_extremity_already_under_its_cap_still_moves_to_the_band_table(self):
        by_id = self._clamped("leg", "leg_armors_clamp.xml",
                              _item("boromir_greave_low", "LegArmor", leg_armor=2, modifier_group="chain"))
        c = by_id["boromir_greave_low"]
        self.assertEqual((c["old_values"], c["new_values"]), ({"leg_armor": 2}, {"leg_armor": 2}))
        self.assertEqual((c["status"], c["new_modifier"]), ("CHANGED", "leather"))

    def test_a_clamped_unworn_extremity_takes_the_elite_table(self):
        by_id = self._clamped("leg", "leg_armors_clamp.xml",
                              _item("sk_gd_x_greave_unworn_a", "LegArmor", leg_armor=60, modifier_group="chain"),
                              hero_worn=False)
        c = by_id["sk_gd_x_greave_unworn_a"]
        self.assertEqual((c["tier"], c["new_modifier"]), ("elite", "leather"))

    def test_the_clamp_never_raises_a_loot_table_and_leaves_head_and_body_alone(self):
        for group in ("cloth", "cloth_unarmoured", "leather"):
            by_id = self._clamped("arm", "arm_armors_clamp.xml",
                                  _item("boromir_bracer_x", "HandArmor", arm_armor=60, modifier_group=group))
            self.assertEqual(by_id["boromir_bracer_x"]["new_modifier"], group, group)
        by_id = self._clamped("body", "body_armors_clamp.xml",
                              _item("boromir_jerkin", "BodyArmor", body_armor=60, arm_armor=30,
                                    modifier_group="chain"))
        self.assertEqual(by_id["boromir_jerkin"]["new_modifier"], "chain")
        by_id = self._clamped("head", "head_armors_clamp.xml",
                              _item("sk_gd_x_helm_heroonly", "HeadArmor", head_armor=70, modifier_group="chain"))
        self.assertEqual(by_id["sk_gd_x_helm_heroonly"]["new_modifier"], "chain")

    def test_the_clamp_writes_the_loot_table_to_the_file(self):
        self._clamped("arm", "arm_armors_clamp.xml",
                      _item("boromir_bracer_w", "HandArmor", arm_armor=60, modifier_group="plate"))
        path = self.armory / "gondor" / "arm_armors_clamp.xml"
        self._run("gondor", "arm_armors_clamp.xml", "arm", dry_run=False)
        out = path.read_bytes().decode("utf-8")
        self.assertIn('modifier_group="leather"', out)
        self.assertNotIn('modifier_group="plate"', out)

    def test_a_clamp_is_skipped_by_no_lower_armor_and_weights_only(self):
        for kw in ({"no_lower_armor": True}, {"weights_only": True}):
            by_id = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body", **kw)}
            self.assertEqual(by_id["boromir_jerkin"]["status"], "UNCHANGED", kw)
            self.assertEqual(by_id["sk_gd_x_chest_unworn_hi"]["status"], "UNCHANGED", kw)

    def test_troop_worn_hero_named_kit_is_restated_but_troll_kit_is_not(self):
        head = {c["id"]: c for c in self._run("gondor", "head_armors.xml", "head")}
        c = head["erkenbrand_helmet"]
        self.assertEqual((c["tier"], c["new_values"]), ("elite", {"head_armor": 48}))   # a hero's name, a troop's curve
        body = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body")}
        self.assertEqual(body["lotr_troll_chest"]["status"], "UNCHANGED")
        self.assertEqual(body["lotr_troll_chest"]["old_values"], {"body_armor": 90, "arm_armor": 40})
        self.assertTrue(ra.is_monster_kit("lotr_troll_chest") and ra.is_monster_kit("sk_cave_troll_x"))
        self.assertFalse(ra.is_monster_kit("gf_helmet_a"))
        self.assertTrue(set(ra.MONSTER_ID_SUBSTRINGS) <= set(ra.EXCLUDE_ID_SUBSTRINGS))

    def test_unworn_merchandise_is_lowered_to_the_elite_band_plus_a_variant_and_never_raised(self):
        by_id = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body")}
        c = by_id["sk_gd_x_chest_unworn_hi"]
        self.assertEqual(c["tier"], "elite")
        # 46 + the variant cap of 1 = 47; the secondary keeps its 30/60 ratio under the 0.30 share cap: 14.1
        self.assertEqual(c["new_values"], {"body_armor": 47, "arm_armor": 14})
        self.assertEqual(by_id["sk_gd_x_chest_unworn_civ"]["status"], "UNCHANGED")   # civilian kit is off the curve

    def test_a_generator_owned_item_is_left_to_its_generator_in_every_restat_mode(self):
        """The Lorien and Snow Orc kits are priced by their own generators (a Mallorn piece one band up; each Snow Orc
        item at the band its id names). No restat mode may touch them. Each mode also restates four control items that
        carry the same values under an id no generator owns, so a mode that would not have changed the owned item
        anyway proves nothing."""
        cases = [
            ("lothlorien", "sk_ll_ml_chest_heavy_a", "sk_xx_ml_chest_heavy_"),
            ("gundabad_snow", "sk_gb_orc_inf_chest_heavy_a", "sk_zz_orc_inf_chest_heavy_"),
        ]

        def block(text, item_id):
            start = text.index(f'id="{item_id}"')
            return text[start:text.index("</Item>", start)]

        for folder_name, owned_id, control_prefix in cases:
            folder = self.armory / folder_name
            folder.mkdir(exist_ok=True)
            controls = [control_prefix + n for n in "abcd"]
            records = {i: {"anchorLevel": 46, "tier": "heavy", "tierSource": "id-keyword"} for i in [owned_id] + controls}
            tiers = {i: "heavy" for i in records}      # the legacy map the roster and materials-only modes read
            modes = {
                "roster-first": dict(tier_source="roster-first", roster_map=records, keep_weights=True,
                                     keep_material_type=True),
                "roster": dict(tier_source="roster", roster_map=tiers),
                "keyword": dict(tier_source="keyword"),
                "weights-only": dict(tier_source="keyword", weights_only=True),
                "materials-only": dict(tier_source="roster", roster_map=tiers, materials_only=True),
            }
            for mode, kwargs in modes.items():
                with self.subTest(kit=folder_name, mode=mode):
                    path = folder / f"body_armors_{mode}.xml"
                    path.write_bytes(_file(
                        _item(owned_id, "BodyArmor", body_armor=10, arm_armor=4, weight=18.5,
                              material_type="Cloth", modifier_group="cloth"),
                        *[_item(i, "BodyArmor", body_armor=10, arm_armor=4, weight=18.5,
                                material_type="Cloth", modifier_group="cloth") for i in controls],
                        crlf=True))
                    before = path.read_text(encoding="utf-8")
                    recs = ra.process_file(str(path), "body", dry_run=False, backup_tag="t", **kwargs)
                    after = path.read_text(encoding="utf-8")
                    self.assertEqual([c["id"] for c in recs if c["id"] == owned_id and c["status"] != "UNCHANGED"], [])
                    self.assertEqual(block(after, owned_id), block(before, owned_id))
                    self.assertTrue(any(block(after, i) != block(before, i) for i in controls),
                                    f"{folder_name} {mode} changed no control item, so this mode proves nothing about the skip")

    def test_the_restat_counts_the_generator_owned_items_it_leaves_alone(self):
        lorien = self.armory / "lothlorien"
        lorien.mkdir()
        path = lorien / "body_armors.xml"
        path.write_bytes(_file(
            _item("sk_ll_ml_chest_heavy_a", "BodyArmor", body_armor=42, arm_armor=14),
            _item("sk_ll_rg_chest_light_a", "BodyArmor", body_armor=24, arm_armor=12),
            _item("sk_gb_orc_arc_chest_med_a", "BodyArmor", body_armor=31, arm_armor=10),
            _item("sk_gd_x_chest_heavy_a", "BodyArmor", body_armor=42, arm_armor=14),
            crlf=True))
        self.assertEqual(ra.count_generator_owned(str(path)), 3)
        self.assertEqual(ra.count_generator_owned(str(self.armory / "gondor" / "body_armors.xml")), 0)

    def test_secondaries_scale_with_the_primary_and_zero_stays_zero(self):
        by_id = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body")}
        # 14 / 42 = 0.33 of the body, over the chest arm share cap of 0.30: 46 * 0.30 = 13.8
        self.assertEqual(by_id["sk_gd_x_chest_heavy_a"]["new_values"], {"body_armor": 46, "arm_armor": 14})
        by_id = {c["id"]: c for c in self._run("mordor", "shoulder_armors.xml", "shoulder")}
        # Black Numenorean cape: 46 * 0.28 = 13; arm 20 / 24 of the body, under the cape cap of 1.0: 10.8
        self.assertEqual(by_id["sk_md_num_pauld_elite_a"]["new_values"], {"body_armor": 13, "arm_armor": 11})
        # orc cape: the id says medium but its lowest wearer is level 21, the heavy band:
        # 36 * 0.28 * 0.68 = 6.85 -> 7; a 0 secondary stays 0
        self.assertEqual(by_id["sk_md_orc_pauldron_med_b"]["new_values"], {"body_armor": 7, "arm_armor": 0})

    def test_keep_weights_and_material_type_but_rescope_extremity_loot_tables(self):
        by_id = {c["id"]: c for c in self._run("gondor", "arm_armors.xml", "arm")}
        c = by_id["sk_gd_x_bracer_med_a"]
        self.assertEqual(c["new_weight"], c["old_weight"])
        self.assertEqual(c["new_material"], "Chainmail")
        self.assertEqual(c["new_modifier"], "cloth_unarmoured")   # medium arm loot table
        self.assertEqual(c["new_values"], {"arm_armor": 8})       # 46 * 0.5 * 0.35 = 8.05
        by_id = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body")}
        self.assertEqual(by_id["sk_gd_x_chest_heavy_a"]["new_modifier"], "plate")  # chest table unchanged
        self.assertEqual(by_id["sk_gd_x_chest_heavy_a"]["new_weight"], 18.5)

    def test_a_commented_out_copy_of_an_item_is_skipped_and_the_live_one_edited(self):
        """The Armory keeps commented-out <Item> blocks for reference. The block regex must skip a
        match inside <!-- --> and edit the live item after it (deep review, 2026-09-13)."""
        path = self.armory / "gondor" / "leg_armors.xml"
        live = _item("sk_gd_x_grvs_heavy_a", "LegArmor", leg_armor=20)
        path.write_bytes(_file("  <!-- old copy\n" + live + "  -->\n", live))
        self.roster["sk_gd_x_grvs_heavy_a"] = {"anchorLevel": 46, "tier": "heavy", "tierSource": "id-keyword"}
        self._run("gondor", "leg_armors.xml", "leg", dry_run=False)
        out = path.read_bytes().decode("utf-8")
        head, _, tail = out.partition("-->")
        self.assertIn('leg_armor="20"', head)      # the commented copy keeps the old value
        self.assertNotIn('leg_armor="21"', head)
        self.assertIn('leg_armor="21"', tail)      # the live item moved to Gondor's elite greaves
        self.assertNotIn('leg_armor="20"', tail)

    def test_the_dark_lords_kit_is_clamped_on_the_numenorean_cap_not_the_orc_cap(self):
        """sauron_torso 50/40/45 sat in the orc-default mordor/ folder and was clamped to 39/12/16, below a
        Gondor elite troop (59). On the numenorean line (46) the lord band is 49 and its secondaries are
        the item's own shares capped at 0.30 and 0.40 of that (2026-11-14, #1271)."""
        path = self.armory / "mordor" / "body_armors.xml"
        path.write_bytes(_file(
            _item("sauron_torso", "BodyArmor", body_armor=50, arm_armor=40, leg_armor=45),
            _item("khamul_body", "BodyArmor", body_armor=48, arm_armor=38),
            _item("sk_md_orc_hero_hood", "BodyArmor", body_armor=50, arm_armor=40, leg_armor=45)))
        for iid in ("sauron_torso", "khamul_body", "sk_md_orc_hero_hood"):
            self.roster[iid] = {"anchorLevel": None, "tier": None, "tierSource": "unworn", "heroWorn": True}
        by_id = {c["id"]: c for c in self._run("mordor", "body_armors.xml", "body")}
        self.assertEqual(by_id["sauron_torso"]["new_values"], {"body_armor": 49, "arm_armor": 15, "leg_armor": 20})
        self.assertEqual(by_id["khamul_body"]["new_values"], {"body_armor": 48, "arm_armor": 14})   # 48 * 0.30 = 14.4
        # the same folder's orc kit keeps the orc cap: 36 * 1.07 = 38.5
        self.assertEqual(by_id["sk_md_orc_hero_hood"]["new_values"]["body_armor"], 39)

    def test_keep_weights_never_rewrites_the_weight_attribute(self):
        """A full restat with --keep-weights wrote weight="3" back as "3.0" (four Rivendell chests)."""
        path = self.armory / "gondor" / "leg_armors.xml"
        path.write_bytes(_file(_item("sk_gd_x_grvs_heavy_a", "LegArmor", leg_armor=20, weight=3)))
        self.roster["sk_gd_x_grvs_heavy_a"] = {"anchorLevel": 46, "tier": "heavy", "tierSource": "id-keyword"}
        self._run("gondor", "leg_armors.xml", "leg", dry_run=False)
        out = path.read_bytes()
        self.assertIn(b'leg_armor="21"', out)                 # the armour did change
        self.assertIn(b'weight="3"', out)
        self.assertNotIn(b'weight="3.0"', out)

    def test_apply_reports_the_files_it_really_wrote(self):
        body = self.armory / "gondor" / "body_armors.xml"
        written = []
        self._run("gondor", "body_armors.xml", "body", dry_run=False, written=written)
        self.assertEqual(written, [str(body)])
        written = []
        self._run("gondor", "body_armors.xml", "body", dry_run=False, written=written)   # nothing left to change
        self.assertEqual(written, [])
        dry = []
        self._run("gondor", "head_armors.xml", "head", dry_run=True, written=dry)
        self.assertEqual(dry, [])

    def _main(self, *argv):
        out = io.StringIO()
        with mock.patch.object(sys, "argv", ["rebalance_armor.py", *argv]), \
                mock.patch.object(ra, "load_roster_records", return_value=(self.roster, "t")), \
                contextlib.redirect_stdout(out):
            try:
                ra.main()
                code = 0
            except SystemExit as e:
                code = e.code
        return code, out.getvalue()

    def test_main_counts_the_files_written_and_refuses_an_apply_without_a_backup_tag(self):
        args = ["--all", "--tier-source", "roster-first", "--keep-weights", "--keep-material-type",
                "--armory-path", str(self.armory)]
        before = {p: p.read_bytes() for p in self.armory.rglob("*.xml")}
        code, out = self._main("--apply", *args)
        self.assertEqual(code, 2)
        self.assertIn("--backup-tag", out)
        self.assertEqual({p: p.read_bytes() for p in self.armory.rglob("*.xml")}, before)   # nothing written
        code, out = self._main("--dry-run", *args)                                           # a dry run needs none
        self.assertEqual(code, 0)
        code, out = self._main("--apply", "--backup-tag", "t", *args)
        backups = list(self.armory.rglob("*.bak-t"))
        self.assertTrue(backups)
        self.assertIn(f"Changes written to {len(backups)} files", out)    # a backup exists per written file
        code, out = self._main("--apply", "--backup-tag", "t", *args)
        self.assertIn("Changes written to 0 files", out)

    def test_check_exits_1_on_a_plan_and_0_on_an_empty_one_and_writes_nothing(self):
        """--check is the dry run of the apply sequence with its flags fixed, so a bare --check is enough."""
        before = {p: p.read_bytes() for p in self.armory.rglob("*") if p.is_file()}
        code, out = self._main("--check", "--armory-path", str(self.armory))
        self.assertEqual(code, 1)
        self.assertRegex(out, r"\d+ item\(s\) differ from the curve")
        self.assertIn("gondor/body_armors.xml", out.replace("\\", "/"))
        self.assertIn("sk_gd_x_chest_heavy_a", out)
        self.assertEqual({p: p.read_bytes() for p in self.armory.rglob("*") if p.is_file()}, before)
        self._main("--apply", "--backup-tag", "t", "--all", "--tier-source", "roster-first", "--keep-weights",
                   "--keep-material-type", "--armory-path", str(self.armory))
        code, out = self._main("--check", "--armory-path", str(self.armory))
        self.assertEqual(code, 0)
        self.assertIn("0 item(s) differ from the curve", out)

    def test_check_lists_the_first_twenty_differences_with_the_live_and_the_curve_value(self):
        items = []
        for n in range(25):
            iid = f"sk_gd_x_grvs_heavy_{n:02d}"
            self.roster[iid] = {"anchorLevel": 46, "tier": "heavy", "tierSource": "id-keyword"}
            items.append(_item(iid, "LegArmor", leg_armor=20, modifier_group="chain"))
        (self.armory / "gondor" / "leg_armors.xml").write_bytes(_file(*items, crlf=True))
        for name in ("arm_armors.xml", "body_armors.xml", "head_armors.xml"):
            (self.armory / "gondor" / name).unlink()
        (self.armory / "mordor" / "shoulder_armors.xml").unlink()
        code, out = self._main("--check", "--armory-path", str(self.armory))
        self.assertEqual(code, 1)
        self.assertIn("25 item(s) differ from the curve", out)
        lines = [l for l in out.splitlines() if "sk_gd_x_grvs_heavy_" in l]
        self.assertEqual(len(lines), 20)
        self.assertIn("leg_armor: live 20 -> curve 21", lines[0])    # Gondor's elite greaves
        self.assertEqual(out.strip().splitlines()[-1], "25 item(s) differ from the curve.")
        self.assertTrue(any("modifier_group" in l and "chain" in l and "leather" in l for l in lines))

    def test_check_exits_2_when_the_armory_is_not_installed(self):
        code, out = self._main("--check", "--armory-path", str(self.armory / "missing"))
        self.assertEqual(code, 2)
        self.assertIn("not installed", out)

    def test_check_refuses_flags_that_would_change_what_it_checks(self):
        for flag in (("--weights-only",), ("--no-lower-armor",), ("--materials-only",), ("--keep-materials",),
                     ("--backup-tag", "t")):
            code, _ = self._main("--check", *flag, "--armory-path", str(self.armory))
            self.assertEqual(code, 2, flag)

    def test_a_check_that_read_nothing_is_not_a_pass(self):
        """Exit 0 would make the gate print PASS for a run that compared no file with the curve."""
        import data_gates as dg
        empty = Path(self._tmp.name) / "empty_items"
        (empty / "gondor").mkdir(parents=True)               # a culture folder with no armour file
        bare = Path(self._tmp.name) / "no_cultures"
        bare.mkdir()
        cases = {
            "no culture folder": ["--armory-path", str(bare)],
            "no armour file": ["--armory-path", str(empty)],
            "unknown culture": ["--cultures", "gondor,nowhere", "--armory-path", str(self.armory)],
            "unknown slot": ["--slots", "bogus", "--armory-path", str(self.armory)],
        }
        for label, argv in cases.items():
            code, out = self._main("--check", *argv)
            self.assertEqual(code, 2, label)
            self.assertIn("ERROR: SKIPPED", out, label)
            self.assertIn("nothing was checked", out, label)
            self.assertEqual(dg.verdict(code, out)[0], "NOT RUN", label)
        code, out = self._main("--check", "--cultures", "gondor", "--slots", "body",
                               "--armory-path", str(self.armory))
        self.assertEqual(code, 1)                             # a real scope still compares files

    def test_apply_is_byte_faithful_and_backs_up_once(self):
        body = self.armory / "gondor" / "body_armors.xml"
        arm = self.armory / "gondor" / "arm_armors.xml"
        cape = self.armory / "mordor" / "shoulder_armors.xml"
        self._run("gondor", "body_armors.xml", "body", dry_run=False)
        self._run("gondor", "arm_armors.xml", "arm", dry_run=False)
        self._run("mordor", "shoulder_armors.xml", "shoulder", dry_run=False)
        b = body.read_bytes()
        self.assertIn(b"\r\n<Items>\r\n", b)                                  # CRLF kept
        self.assertIn(b'body_armor="46" arm_armor="14" material_type="Plate" modifier_group="plate"', b)
        self.assertIn(b'weight="18.5"', b)
        self.assertIn(b'body_armor="49" arm_armor="15"', b)                   # hero-only kit: the lord clamp
        a = arm.read_bytes()
        self.assertTrue(a.startswith(b"\xef\xbb\xbf"))                        # BOM kept
        self.assertIn(b'arm_armor="8" material_type="Chainmail" modifier_group="cloth_unarmoured"', a)
        self.assertNotIn(b"\r\n", a)                                          # LF file stays LF
        self.assertIn(b'body_armor="13" arm_armor="11"', cape.read_bytes())
        self.assertTrue(body.with_name("body_armors.xml.bak-t").exists())
        first = body.with_name("body_armors.xml.bak-t").read_bytes()
        self.assertIn(b'body_armor="42" arm_armor="14"', first)
        # A second apply changes nothing and does not overwrite the backup.
        changes = self._run("gondor", "body_armors.xml", "body", dry_run=False)
        self.assertEqual([c["id"] for c in changes if c["status"] == "CHANGED"], [])
        self.assertEqual(body.with_name("body_armors.xml.bak-t").read_bytes(), first)



class HeroWearerTests(unittest.TestCase):
    """derive_armor_tiers records which items a hero wears (heroWorn) beside the troop anchor (2026-11-14)."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        for d in ("troops", "characters", "named_companions", "equipmentsets"):
            (root / d).mkdir()
        (root / "equipmentsets" / "sets.xml").write_text(
            "<EquipmentRosters>"
            '<EquipmentRoster id="hero_bat_template" culture="Culture.gondor"><EquipmentSet>'
            '<Equipment slot="Head" id="Item.h_helm" /><Equipment slot="Body" id="Item.h_chest" />'
            '<Equipment slot="Item0" id="Item.h_sword" /></EquipmentSet></EquipmentRoster>'
            '<EquipmentRoster id="hero_civ_template" culture="Culture.gondor">'
            '<EquipmentSet equipmentType="Civilian"><Equipment slot="Body" id="Item.h_dress" /></EquipmentSet>'
            "</EquipmentRoster></EquipmentRosters>", encoding="utf-8")
        (root / "characters" / "lords.xml").write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="lord_a" is_hero="true" level="31"><Equipments>'
            '<EquipmentSet id="hero_bat_template" /><EquipmentSet id="hero_civ_template" equipmentType="Civilian" />'
            "</Equipments></NPCCharacter>"
            '<NPCCharacter id="guard_a" is_hero="false" level="16"><Equipments><EquipmentRoster>'
            '<equipment slot="Head" id="Item.g_helm" /></EquipmentRoster></Equipments></NPCCharacter>'
            "</NPCCharacters>", encoding="utf-8")
        (root / "named_companions" / "named_companions.xml").write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="comp_a" is_hero="true" level="26"><Equipments>'
            '<EquipmentRoster><equipment slot="Body" id="Item.n_chest" /><equipment slot="Cape" id="Item.h_chest" /></EquipmentRoster>'
            '<EquipmentRoster civilian="true"><equipment slot="Body" id="Item.n_dress" /></EquipmentRoster>'
            "</Equipments></NPCCharacter></NPCCharacters>", encoding="utf-8")
        (root / "troops" / "troops_alpha.xml").write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="alpha_t3" level="16"><Equipments><EquipmentRoster>'
            '<equipment slot="Body" id="Item.h_chest" /></EquipmentRoster></Equipments></NPCCharacter>'
            "</NPCCharacters>", encoding="utf-8")
        self.root = root
        self._old = (dat.TROOPS_DIR, dat.CHARACTERS_DIR, dat.NAMED_COMPANIONS_DIR, dat.EQUIPMENTSETS_DIR,
                     dat.LORDS_XSLT, dat.WANDERERS_XML)
        dat.WANDERERS_XML = str(root / "no_such_wanderers.xml")
        dat.TROOPS_DIR, dat.CHARACTERS_DIR = str(root / "troops"), str(root / "characters")
        dat.NAMED_COMPANIONS_DIR, dat.EQUIPMENTSETS_DIR = str(root / "named_companions"), str(root / "equipmentsets")
        dat.LORDS_XSLT = str(root / "no_such_lords.xslt")     # the real stylesheet would add its own heroes

    def tearDown(self):
        (dat.TROOPS_DIR, dat.CHARACTERS_DIR, dat.NAMED_COMPANIONS_DIR, dat.EQUIPMENTSETS_DIR,
         dat.LORDS_XSLT, dat.WANDERERS_XML) = self._old
        self._tmp.cleanup()

    def test_lords_xslt_hands_battle_templates_to_retagged_vanilla_lords(self):
        """lords.xslt gives 74 vanilla lords an <EquipmentSet id=...> template; those lords are not in
        characters/, so their kit was priced as troop kit (2026-11-14, #1271). Civilian sets are skipped."""
        (self.root / "equipmentsets" / "more.xml").write_text(
            '<EquipmentRosters><EquipmentRoster id="xslt_bat" culture="Culture.gondor"><EquipmentSet>'
            '<Equipment slot="Body" id="Item.x_chest" /></EquipmentSet></EquipmentRoster></EquipmentRosters>',
            encoding="utf-8")
        Path(dat.LORDS_XSLT).write_text(
            '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">'
            "<xsl:template match=\"NPCCharacter[@id='lord_x']\"><xsl:copy>"
            '<xsl:attribute name="id">lord_x</xsl:attribute>'
            '<Equipments><EquipmentSet id="xslt_bat" /><EquipmentSet id="hero_civ_template" equipmentType="Civilian" />'
            "</Equipments></xsl:copy></xsl:template>"
            "<xsl:template match=\"NPCCharacter[starts-with(@id,'minor_')]/@culture\">"
            '<xsl:attribute name="culture">Culture.gondor</xsl:attribute></xsl:template>'
            "</xsl:stylesheet>", encoding="utf-8")
        worn = dat.parse_hero_wearers()
        self.assertEqual(worn["x_chest"], ["lord_x"])
        self.assertNotIn("h_dress", worn)

    def test_a_direct_equipment_override_replaces_its_slot_in_every_set(self):
        """BasicCharacterObject.Deserialize hands the <equipment> children of <Equipments> to
        MBEquipmentRoster.AddOverriddenEquipments, which sets that slot in every set of the hero. The
        replaced template item is no longer worn by that hero."""
        (self.root / "characters" / "kings.xml").write_text(
            '<NPCCharacters><NPCCharacter id="king_a" is_hero="true"><Equipments>'
            '<EquipmentSet id="hero_bat_template" /><EquipmentSet id="hero_civ_template" equipmentType="Civilian" />'
            '<equipment slot="Head" id="Item.crown" /></Equipments></NPCCharacter></NPCCharacters>',
            encoding="utf-8")
        worn = dat.parse_hero_wearers()
        self.assertEqual(worn["crown"], ["king_a"])
        self.assertEqual(sorted(worn["h_chest"]), ["comp_a", "king_a", "lord_a"])
        self.assertEqual(worn["h_helm"], ["lord_a"])          # king_a's helmet slot holds the crown

    def test_heroes_wear_what_their_inline_rosters_and_battle_templates_list(self):
        worn = dat.parse_hero_wearers()
        self.assertEqual(sorted(worn), ["h_chest", "h_helm", "h_sword", "n_chest"])   # slots Item0 and up included
        self.assertEqual(sorted(worn["h_chest"]), ["comp_a", "lord_a"])
        # civilian sets and non-hero characters (guards) are not hero kit
        self.assertTrue({"h_dress", "n_dress", "g_helm"}.isdisjoint(worn))

    def test_the_map_marks_hero_kit_without_moving_the_troop_anchor(self):
        old_index = dat.build_armory_index
        try:
            dat.build_armory_index = lambda: ({
                iid: {"folder": "gondor", "slot": slot, "primary": 20, "weight": 1.0, "name": "x"}
                for iid, slot in (("h_chest", "body"), ("h_helm", "head"), ("t_only", "body"))}, "fixture")
            recs, _ = dat.derive()
        finally:
            dat.build_armory_index = old_index
        self.assertTrue(recs["h_chest"]["heroWorn"])
        self.assertEqual((recs["h_chest"]["anchorLevel"], recs["h_chest"]["tierSource"]), (16, "roster(L16)"))
        self.assertEqual(recs["h_chest"]["tier"], "medium")          # the troop decides the band
        self.assertTrue(recs["h_helm"]["heroWorn"])
        self.assertEqual((recs["h_helm"]["anchorLevel"], recs["h_helm"]["tierSource"]), (None, "unworn"))
        self.assertFalse(recs["t_only"]["heroWorn"])

    def test_lord_wearers_are_occupation_lord_heroes_and_never_companions_or_wanderers(self):
        """Companions stay light (2026-11-15): only a lord's kit may be priced at the lord band."""
        (self.root / "characters" / "lords.xml").write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="lord_l" is_hero="true" occupation="Lord"><Equipments>'
            '<EquipmentSet id="hero_bat_template" /></Equipments></NPCCharacter>'
            '<NPCCharacter id="hero_x" is_hero="true"><Equipments><EquipmentRoster>'
            '<equipment slot="Cape" id="Item.x_cape" /></EquipmentRoster></Equipments></NPCCharacter>'
            "</NPCCharacters>", encoding="utf-8")
        (self.root / "named_companions" / "named_companions.xml").write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="comp_l" is_hero="true" occupation="Lord"><Equipments><EquipmentRoster>'
            '<equipment slot="Cape" id="Item.c_cape" /></EquipmentRoster></Equipments></NPCCharacter>'
            "</NPCCharacters>", encoding="utf-8")
        Path(dat.WANDERERS_XML).write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="spc_w_0" is_template="true" is_hero="false" occupation="Wanderer"><Equipments>'
            '<EquipmentRoster><equipment slot="Head" id="Item.w_helm" /></EquipmentRoster></Equipments></NPCCharacter>'
            "</NPCCharacters>", encoding="utf-8")
        lords = dat.parse_lord_wearers()
        self.assertEqual(lords["h_helm"], ["lord_l"])
        self.assertTrue({"x_cape", "c_cape", "w_helm"}.isdisjoint(lords))
        heroes = dat.parse_hero_wearers()
        self.assertTrue({"x_cape", "c_cape", "w_helm"} <= set(heroes))   # heroWorn keeps its meaning
        old_index = dat.build_armory_index
        try:
            dat.build_armory_index = lambda: ({
                iid: {"folder": "gondor", "slot": slot, "primary": 20, "weight": 1.0, "name": "x"}
                for iid, slot in (("h_helm", "head"), ("c_cape", "cape"))}, "fixture")
            recs, _ = dat.derive()
        finally:
            dat.build_armory_index = old_index
        self.assertTrue(recs["h_helm"]["lordWorn"])
        self.assertTrue(recs["c_cape"]["heroWorn"])
        self.assertFalse(recs["c_cape"]["lordWorn"])

    def test_wanderer_templates_are_heroes_but_other_templates_are_not(self):
        """CompanionsCampaignBehavior spawns a hero from every template with occupation Wanderer, though the
        file says is_hero="false"; the hero takes a battle set from the template's roster."""
        Path(dat.WANDERERS_XML).write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="spc_w_0" is_template="true" is_hero="false" occupation="Wanderer"><Equipments>'
            '<EquipmentSet id="hero_bat_template" /><EquipmentSet id="hero_civ_template" equipmentType="Civilian" />'
            "</Equipments></NPCCharacter>"
            '<NPCCharacter id="not_a_template" is_hero="false" occupation="Wanderer"><Equipments>'
            '<EquipmentRoster><equipment slot="Head" id="Item.w_helm" /></EquipmentRoster></Equipments></NPCCharacter>'
            "</NPCCharacters>", encoding="utf-8")
        worn = dat.parse_hero_wearers()
        self.assertEqual(sorted(worn["h_helm"]), ["lord_a", "spc_w_0"])
        self.assertNotIn("w_helm", worn)
        self.assertNotIn("h_dress", worn)

    def test_the_map_marks_wanderer_kit_without_anchoring_it(self):
        Path(dat.WANDERERS_XML).write_text(
            "<NPCCharacters>"
            '<NPCCharacter id="spc_w_0" is_template="true" is_hero="false" occupation="Wanderer"><Equipments>'
            '<EquipmentRoster><equipment slot="Head" id="Item.w_helm" /></EquipmentRoster></Equipments></NPCCharacter>'
            "</NPCCharacters>", encoding="utf-8")
        old_index = dat.build_armory_index
        try:
            dat.build_armory_index = lambda: ({"w_helm": {"folder": "gondor", "slot": "head", "primary": 20,
                                                          "weight": 1.0, "name": "x"}}, "fixture")
            recs, _ = dat.derive()
        finally:
            dat.build_armory_index = old_index
        self.assertTrue(recs["w_helm"]["heroWorn"])
        self.assertEqual((recs["w_helm"]["anchorLevel"], recs["w_helm"]["tierSource"]), (None, "unworn"))


class ShippedHeroWearerTests(unittest.TestCase):
    """parse_hero_wearers over the repo's own ModuleData (no install needed)."""

    def test_the_retagged_vanilla_lords_and_the_crown_override_reach_the_map(self):
        worn = dat.parse_hero_wearers()
        # lord_1_53's angbor_bat_equipment, lord_1_45's forlong_bat_equipment and Faramir's hood come only
        # through lords.xslt; before 2026-11-14 their lord pieces took a full troop restat
        for iid in ("sk_gd_lam_inf_chest_lord_a", "sk_gd_los_nob_chest_lord_a", "ithilien_hood", "sauron_torso",
                    "nazgul_armor", "sk_mordor_mouth_of_sauron_helm", "sk_dg_khml_scalemail_lord_a"):
            self.assertIn(iid, worn, iid)
        # the Arnor king's lord_AN1_1 carries the crown as a direct <equipment> override in lords.xml
        self.assertIn("sk_ar_art_crown_king_a", worn)

    def test_a_named_companions_lord_line_cape_is_not_lord_kit(self):
        """roh_nbl_shldr_s is worn only by the named companion Eohart, so the restat must not raise it."""
        self.assertIn("roh_nbl_shldr_s", dat.parse_hero_wearers())
        self.assertNotIn("roh_nbl_shldr_s", dat.parse_lord_wearers())

    def test_a_wanderer_template_helmet_is_hero_kit(self):
        """spc_wanderer_dolguldur_0 draws npc_companion_equipment_template_dolguldur, so the Uruk elite
        helmet is a hero's; without it the restat raised that helmet's head armour."""
        self.assertIn("spc_wanderer_dolguldur_0", dat.parse_hero_wearers()["sk_dg_uruk_helmet_elite_a"])


class WornArmourTests(unittest.TestCase):
    """The pure functions the validator's WORN_ARMOUR_* gates and analyze_kingdom_armour share."""

    ITEMS = {iid: {"stats": stats} for iid, stats in {
        "head_big": {"head_armor": 40}, "head_small": {"head_armor": 6},
        "body_big": {"body_armor": 50, "leg_armor": 10}, "body_small": {"body_armor": 8},
        "cape": {"body_armor": 12, "arm_armor": 9}, "glove": {"arm_armor": 20}, "boot": {"leg_armor": 15},
    }.items()}

    TWO_SETS = {"sets": [{"Head": "head_big", "Body": "body_small", "Cape": "cape"},
                         {"Head": "head_small", "Body": "body_big", "Gloves": "glove", "Leg": "boot"}]}

    def test_extremes_per_slot_mix_the_sets_as_a_seeded_spawn_does(self):
        """Equipment.GetRandomEquipmentElements re-rolls the set index for every slot whenever the seed is
        not -1, and every troop spawn passes CharacterHelper.GetPartyMemberFaceSeed (never -1), so in a
        campaign battle each armour slot comes from its own set. The reach is the per-slot min and max."""
        worst, best = ra.worn_armour_extremes_per_slot(self.TWO_SETS, self.ITEMS)
        # best: head 40; Body 50 + Cape 12; Cape arm 9 + Gloves 20; Body leg 10 + Leg 15
        self.assertEqual(best, {"head": 40, "body": 62, "arm": 29, "leg": 25})
        # worst: head 6; Body 8 + Cape 0; no arm; no leg
        self.assertEqual(worst, {"head": 6, "body": 8, "arm": 0, "leg": 0})

    def test_an_unknown_item_and_an_unfilled_slot_count_zero_and_no_sets_is_all_zero(self):
        troop = {"sets": [{"Head": "no_such_item", "Body": ""}]}
        zero = {"head": 0, "body": 0, "arm": 0, "leg": 0}
        self.assertEqual(ra.worn_armour_extremes_per_slot(troop, self.ITEMS), (zero, zero))
        self.assertEqual(ra.worn_armour_extremes_per_slot({"sets": []}, self.ITEMS), (zero, zero))

    def test_the_limits_are_vanillas_best_troop_plus_ten_and_the_band_floors(self):
        self.assertEqual(ra.WORN_ARMOUR_CEILING, {"head": 64, "body": 77, "arm": 69, "leg": 59})
        self.assertEqual(ra.WORN_ARMOUR_FLOOR, {"medium": {"head": 8, "body": 8}, "heavy": {"head": 20, "body": 20},
                                                "elite": {"head": 30, "body": 30}})

    def test_breaches_name_the_ceiling_and_the_floor_by_the_troops_band(self):
        worst = {"head": 25, "body": 31, "arm": 0, "leg": 0}
        best = {"head": 65, "body": 77, "arm": 70, "leg": 59}
        out = ra.worn_armour_breaches(46, worst, best)           # level 46 is the elite band
        self.assertEqual(out["ceiling"], [("head", 65, 64), ("arm", 70, 69)])
        self.assertEqual(out["floor"], [("head", 25, 30)])        # body 31 meets 30
        self.assertEqual(ra.worn_armour_breaches(6, worst, best)["floor"], [])            # light: no floor
        self.assertEqual(ra.worn_armour_breaches(46, worst, best, bodyless=True)["floor"], [("head", 25, 30)])
        low = {"head": 30, "body": 5, "arm": 0, "leg": 0}
        self.assertEqual(ra.worn_armour_breaches(46, low, low)["floor"], [("body", 5, 30)])
        self.assertEqual(ra.worn_armour_breaches(46, low, low, bodyless=True)["floor"], [])


class RaisedSecondaryTests(unittest.TestCase):
    """A piece the restat raises into the heavy or elite band from below it carries 0 secondaries (none
    exist below the heavy band), so its own share is 0. Such a 0 takes the legacy row's share for the
    tier, or, for a stat the row lacks (a chest's arm armour), the median share of the line's heavy or
    better pieces that carry it, hero and lord pieces left out (2026-11-17)."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.armory = Path(self._tmp.name) / "LOTRLOME_items"
        mirkwood = self.armory / "mirkwood"
        arnor = self.armory / "arnor"
        mirkwood.mkdir(parents=True)
        arnor.mkdir(parents=True)
        (mirkwood / "body_armors.xml").write_bytes(_file(
            _item("mirkwood_body", "BodyArmor", body_armor=8, arm_armor=0),
            _item("mkwd_inf3_chest", "BodyArmor", body_armor=8, arm_armor=0, leg_armor=0),
            _item("mkwd_inf3_chest_silver", "BodyArmor", body_armor=48, arm_armor=8, leg_armor=19),
            _item("legolas_body", "BodyArmor", body_armor=51, arm_armor=15, leg_armor=20),     # hero
            _item("mkwd_lord_chest", "BodyArmor", body_armor=51, arm_armor=20, leg_armor=20),  # lord
            _item("mkwd_x_chest_heavy_a", "BodyArmor", body_armor=40, arm_armor=0, leg_armor=15),
            crlf=True))
        (mirkwood / "shoulder_armors.xml").write_bytes(_file(
            _item("mkwd_inf3_pauldrons_bronze", "Cape", body_armor=5, arm_armor=0)))
        (arnor / "body_armors.xml").write_bytes(_file(
            _item("sk_ar_x_chest", "BodyArmor", body_armor=8, arm_armor=0)))
        elite = {"anchorLevel": 41, "tier": "elite", "tierSource": "roster(L41)"}
        self.roster = {i: dict(elite) for i in ("mirkwood_body", "mkwd_inf3_chest", "mkwd_inf3_chest_silver",
                                                 "mkwd_x_chest_heavy_a", "mkwd_inf3_pauldrons_bronze",
                                                 "sk_ar_x_chest")}

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, folder, fname, slot):
        return {c["id"]: c for c in ra.process_file(
            str(self.armory / folder / fname), slot, dry_run=True, tier_source="roster-first",
            roster_map=self.roster, keep_weights=True, keep_material_type=True)}

    def test_the_line_share_leaves_out_hero_and_lord_pieces_and_low_pieces(self):
        shares = ra.line_secondary_shares(str(self.armory))
        # only mkwd_inf3_chest_silver counts for the arm: 8 / 48; the heavy chest with arm 0 carries none
        self.assertAlmostEqual(shares[("mirkwood", "body", "arm_armor")], 8 / 48)
        self.assertNotIn(("arnor", "body", "arm_armor"), shares)

    def test_the_palace_guard_chest_raised_to_elite_takes_the_lines_arm_share(self):
        by_id = self._run("mirkwood", "body_armors.xml", "body")
        self.assertEqual(by_id["mirkwood_body"]["new_values"]["arm_armor"], 8)
        self.assertEqual(by_id["mirkwood_body"]["new_values"]["body_armor"], 48)

    def test_a_raised_chest_that_declares_no_leg_takes_the_lines_leg_share(self):
        # Review 2026-11-17 wave 2: mirkwood_body declares no leg_armor, so at the elite band the wardens wore 0
        # leg on the chest while their level 36 recruits' mkwd_inf3_chest carried 19. A raised body piece takes
        # the median leg share of the line's heavy+ chests that carry leg (19/48 and 15/40, capped at 0.40).
        shares = ra.line_secondary_shares(str(self.armory))
        median = (19 / 48 + 15 / 40) / 2
        self.assertAlmostEqual(shares[("mirkwood", "body", "leg_armor")], median)
        by_id = self._run("mirkwood", "body_armors.xml", "body")
        want = int(math.floor(48 * min(median, ra.SECONDARY_SHARE_CAP[("body", "leg_armor")]) + 0.5))
        self.assertEqual(by_id["mirkwood_body"]["new_values"], {"body_armor": 48, "arm_armor": 8, "leg_armor": want})
        self.assertEqual(by_id["mirkwood_body"]["status"], "CHANGED")

    def test_an_undeclared_leg_is_never_added_where_the_line_has_none_or_off_the_body(self):
        self.assertNotIn("leg_armor", self._run("arnor", "body_armors.xml", "body")["sk_ar_x_chest"]["new_values"])
        self.assertNotIn("leg_armor",
                         self._run("mirkwood", "shoulder_armors.xml", "shoulder")["mkwd_inf3_pauldrons_bronze"]["new_values"])
        self.roster["mirkwood_body"] = {"anchorLevel": 16, "tier": "medium", "tierSource": "roster(L16)"}
        self.assertNotIn("leg_armor", self._run("mirkwood", "body_armors.xml", "body")["mirkwood_body"]["new_values"])

    def test_the_apply_writes_the_new_leg_attribute_and_a_second_run_plans_nothing(self):
        path = self.armory / "mirkwood" / "body_armors.xml"
        ra.process_file(str(path), "body", dry_run=False, tier_source="roster-first", roster_map=self.roster,
                        keep_weights=True, keep_material_type=True, line_shares=ra.line_secondary_shares(str(self.armory)))
        raw = path.read_bytes()
        self.assertEqual(raw.count(b"\r\n"), raw.count(b"\n"))   # CRLF kept
        block = raw.split(b'id="mirkwood_body"', 1)[1].split(b"</Item>", 1)[0]
        self.assertRegex(block, rb'<Armor body_armor="48" arm_armor="8" leg_armor="\d+" material_type=')
        again = self._run("mirkwood", "body_armors.xml", "body")
        self.assertEqual(again["mirkwood_body"]["status"], "UNCHANGED")

    def test_a_declared_zero_leg_still_takes_the_rows_share(self):
        by_id = self._run("mirkwood", "body_armors.xml", "body")
        # the leg takes the legacy row's share (28 / 50, capped at 0.40): 48 * 0.40 = 19.2
        self.assertEqual(by_id["mkwd_inf3_chest"]["new_values"], {"body_armor": 48, "arm_armor": 8, "leg_armor": 19})

    def test_a_piece_already_at_the_heavy_band_keeps_its_own_share(self):
        by_id = self._run("mirkwood", "body_armors.xml", "body")
        self.assertEqual(by_id["mkwd_x_chest_heavy_a"]["new_values"]["arm_armor"], 0)
        self.assertEqual(by_id["mkwd_inf3_chest_silver"]["new_values"], {"body_armor": 48, "arm_armor": 8, "leg_armor": 19})

    def test_a_raised_cape_takes_the_rows_arm_share(self):
        by_id = self._run("mirkwood", "shoulder_armors.xml", "shoulder")
        # 48 * 0.28 = 13; the elite row's arm share 17 / 19 = 0.89 is under the cape cap of 1.0: 11.6
        self.assertEqual(by_id["mkwd_inf3_pauldrons_bronze"]["new_values"], {"body_armor": 13, "arm_armor": 12})

    def test_a_stat_no_piece_of_the_line_carries_stays_zero_and_is_listed(self):
        change = self._run("arnor", "body_armors.xml", "body")["sk_ar_x_chest"]
        self.assertEqual(change["new_values"]["arm_armor"], 0)
        self.assertEqual(change["no_line_share"], ["arm_armor"])
        self.assertEqual(self._run("mirkwood", "body_armors.xml", "body")["mirkwood_body"]["no_line_share"], [])

    def test_below_the_heavy_band_a_raised_piece_still_has_no_secondary(self):
        self.roster["mirkwood_body"] = {"anchorLevel": 16, "tier": "medium", "tierSource": "roster(L16)"}
        by_id = self._run("mirkwood", "body_armors.xml", "body")
        self.assertEqual(by_id["mirkwood_body"]["new_values"]["arm_armor"], 0)

    def test_restat_armour_takes_the_same_shares(self):
        shares = ra.line_secondary_shares(str(self.armory))
        values, _ = ra.restat_armour("body", "elite", "mirkwood", "mirkwood_body", {"body_armor": 8, "arm_armor": 0},
                                     line_shares=shares)
        self.assertEqual(values, self._run("mirkwood", "body_armors.xml", "body")["mirkwood_body"]["new_values"])


if __name__ == "__main__":
    unittest.main()
