#!/usr/bin/env python3
"""Unit tests for the kingdom-cap armour curve in tools/rebalance_armor.py (#583).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_kingdom_caps.py

THE CONTRACT
------------
Each kingdom has a chest cap (the maintainer's table, 2026-09-13). The elite band's body_armor
equals the cap; helmet, bracer, pauldron and greaves are fixed ratios of it; the bands below are
fixed ratios of the elite band; the lord band equals the elite band. Sub-lines sharing a folder
are routed by id prefix. The writer takes an item's band from its lowest troop wearer, keeps
weights and material_type, scales secondary stats with the primary, re-scopes extremity loot
tables, and writes byte-faithfully with one dated backup per file.
"""
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

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
        self.assertEqual(ra.KINGDOM_CAPS["erebor"], 70)
        self.assertEqual(ra.KINGDOM_CAPS["iron_hills"], 70)
        self.assertEqual(ra.KINGDOM_CAPS["rivendell"], 68)
        self.assertEqual(ra.KINGDOM_CAPS["lindon"], 68)
        self.assertEqual(ra.KINGDOM_CAPS["mirkwood"], 63)
        self.assertEqual(ra.KINGDOM_CAPS["lothlorien"], 60)
        for k in ("gondor", "rhun", "mordor_numenorean", "arnor"):
            self.assertEqual(ra.KINGDOM_CAPS[k], 57, k)
        self.assertEqual(ra.KINGDOM_CAPS["gundabad"], 49)
        self.assertEqual(ra.KINGDOM_CAPS["dol_guldur"], 46)
        self.assertEqual(ra.KINGDOM_CAPS["khand"], 46)
        self.assertEqual(ra.KINGDOM_CAPS["isengard"], 45)
        for k in ("dale", "harad", "umbar", "mercenary"):
            self.assertEqual(ra.KINGDOM_CAPS[k], 44, k)
        self.assertEqual(ra.KINGDOM_CAPS["mordor_uruk"], 43)
        self.assertEqual(ra.KINGDOM_CAPS["rohan"], 40)
        self.assertEqual(ra.KINGDOM_CAPS["dunland"], 40)
        for k in ("mordor_orc", "mistymountainorcs", "goblin"):
            self.assertEqual(ra.KINGDOM_CAPS[k], 38, k)
        self.assertEqual(ra.KINGDOM_CAPS["thenn"], 35)
        self.assertNotIn("troll", ra.KINGDOM_CAPS)  # the troll stays on the legacy curve

    def test_every_cap_key_is_a_folder_or_a_routed_line(self):
        self.assertEqual(set(ra.KINGDOM_CAPS) - FOLDERS - LINE_KEYS, set())

    def test_ratios(self):
        self.assertEqual(ra.SLOT_CAP_RATIO, {"body": 1.0, "head": 0.9, "arm": 0.6, "shoulder": 0.6, "leg": 0.5})
        self.assertEqual(ra.BAND_RATIO, {"light": 0.40, "medium": 0.64, "heavy": 0.84, "elite": 1.0, "lord": 1.0})


class RoutingTests(unittest.TestCase):
    def test_kingdom_key_routes_folders_and_sub_lines(self):
        k = ra.kingdom_key
        self.assertEqual(k("sk_md_num_hood_elite_a", "mordor"), "mordor_numenorean")
        self.assertEqual(k("sm_md_num_grvs_elite_a", "mordor"), "mordor_numenorean")
        self.assertEqual(k("sk_uruk_mordor_helmet_heavy_a", "mordor"), "mordor_uruk")
        for iid in ("sk_md_mor_inf_helmet_elite_a", "sk_md_orc_inf_chest_heavy_e", "sk_gn_orc_x"):
            self.assertEqual(k(iid, "mordor"), "mordor_orc", iid)
        self.assertEqual(k("witch_king_helmet", "mordor"), "mordor_orc")   # routing only; is_excluded skips it
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


class CurveTests(unittest.TestCase):
    def test_calculate_stats_on_the_cap_model(self):
        g = lambda tier, slot: ra.calculate_stats(tier, slot, "gondor")  # noqa: E731
        self.assertEqual(g("elite", "body")["body_armor"], 57)
        self.assertEqual(g("elite", "head")["head_armor"], 51)
        self.assertEqual(g("elite", "arm")["arm_armor"], 34)
        self.assertEqual(g("elite", "shoulder")["body_armor"], 34)
        self.assertEqual(g("elite", "leg")["leg_armor"], 29)
        self.assertEqual(g("lord", "body")["body_armor"], 57)      # lord = elite: nothing above the cap
        self.assertEqual(g("heavy", "body")["body_armor"], 48)     # 47.88
        self.assertEqual(g("medium", "body")["body_armor"], 36)    # 36.48
        self.assertEqual(g("light", "body")["body_armor"], 23)     # 22.8
        self.assertEqual(ra.calculate_stats("elite", "head", "erebor")["head_armor"], 63)
        self.assertEqual(ra.calculate_stats("elite", "leg", "isengard")["leg_armor"], 23)  # 22.5 rounds up
        self.assertEqual(ra.calculate_stats("elite", "body", "mordor", item_id="sk_md_num_x")["body_armor"], 57)
        self.assertEqual(ra.calculate_stats("elite", "body", "mordor", item_id="sk_uruk_mordor_x")["body_armor"], 43)
        self.assertEqual(ra.calculate_stats("elite", "body", "mordor", item_id="sk_md_orc_x")["body_armor"], 38)
        self.assertEqual(ra.calculate_stats("elite", "body", "rhun", item_id="sk_dg_khml_x")["body_armor"], 46)

    def test_cap_model_row_secondaries_keep_the_legacy_proportion(self):
        st = ra.calculate_stats("elite", "body", "gondor")
        self.assertEqual(st["body_armor"], 57)
        self.assertEqual(st["leg_armor"], 32)          # 57 * 28 / 50 = 31.9
        self.assertIn("weight", st)
        self.assertEqual(st["material_type"], "Plate")
        cape = ra.calculate_stats("heavy", "shoulder", "gondor")
        self.assertEqual(cape["body_armor"], 29)      # 57 * 0.6 * 0.84 = 28.7
        self.assertEqual(cape["arm_armor"], 25)       # 29 * 11 / 13 = 24.5 -> 25
        civ = ra.calculate_stats("light", "shoulder", "thenn")
        self.assertEqual(civ["arm_armor"], 5)         # 35 * .6 * .4 = 8.4 -> 8; 8 * 3 / 5 = 4.8 -> 5

    def test_civilian_and_uncapped_cultures_stay_on_the_legacy_curve(self):
        self.assertEqual(ra.calculate_stats("civilian", "body", "gondor")["body_armor"],
                         ra.BODY_BASELINES["civilian"]["body_armor"] + 1)
        self.assertEqual(ra.calculate_stats("elite", "body", "troll")["body_armor"], 50 + 8)
        self.assertEqual(ra.calculate_stats("elite", "body", "troll")["leg_armor"], 28 + 5)

    def test_tier_from_value_routes_a_sub_line_by_item_id(self):
        """A Dol Guldur helmet at 41 (its own elite) read as Rhun heavy (43) when the value
        was judged on the folder's cap alone (Codex, 2026-09-13)."""
        self.assertEqual(ra.tier_from_value(41, "head", "rhun"), "heavy")
        self.assertEqual(ra.tier_from_value(41, "head", "rhun", item_id="sk_dg_khml_helmet_x"), "elite")
        self.assertEqual(ra.tier_from_value(51, "head", "rhun", item_id="sk_rh_loke_helmet_x"), "elite")

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
            self.assertEqual([ra.modifier_group_for(slot, t) for t in ra.TIERS],
                             ["cloth_unarmoured", "cloth", "cloth", "chain", "chain", "chain"], slot)
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
        self.assertEqual(recs["sk_gd_x_helmet_heavy_a"]["target"], 51)
        self.assertEqual((recs["sk_gd_x_helmet_lord_a"]["tier"], recs["sk_gd_x_helmet_lord_a"]["tierSource"]),
                         ("lord", "id-keyword"))
        self.assertEqual(recs["sk_gd_civ_coat_heavy_a"]["tier"], "civilian")   # worn at 46, still civilian
        self.assertEqual(recs["a_hood"]["tierSource"], "unworn")


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
            crlf=True))
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
            "boromir_jerkin": {"anchorLevel": 51, "tier": "lord", "tierSource": "id-keyword"},
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
        self.assertEqual(by_id["sk_gd_x_chest_heavy_a"]["new_values"]["body_armor"], 57)
        self.assertEqual(by_id["sk_gd_x_chest_lord_a"]["tier"], "lord")       # unworn: keyword
        self.assertEqual(by_id["sk_gd_x_chest_lord_a"]["new_values"]["body_armor"], 57)
        self.assertEqual(by_id["sk_gd_x_chest_unworn_a"]["status"], "UNCHANGED")  # no band at all: skipped
        self.assertEqual(by_id["sk_gd_x_chest_civ_a"]["status"], "UNCHANGED")     # civilian: skipped
        self.assertEqual(by_id["boromir_jerkin"]["status"], "UNCHANGED")          # hero kit: skipped

    def test_secondaries_scale_with_the_primary_and_zero_stays_zero(self):
        by_id = {c["id"]: c for c in self._run("gondor", "body_armors.xml", "body")}
        # 14 * 57 / 42 = 19
        self.assertEqual(by_id["sk_gd_x_chest_heavy_a"]["new_values"], {"body_armor": 57, "arm_armor": 19})
        by_id = {c["id"]: c for c in self._run("mordor", "shoulder_armors.xml", "shoulder")}
        # Black Numenorean cape: 57 * 0.6 = 34; arm 20 * 34 / 24 = 28
        self.assertEqual(by_id["sk_md_num_pauld_elite_a"]["new_values"], {"body_armor": 34, "arm_armor": 28})
        # orc cape: the id says medium but its lowest wearer is level 21, the heavy band:
        # 38 * 0.6 * 0.84 = 19.15 -> 19; a 0 secondary stays 0
        self.assertEqual(by_id["sk_md_orc_pauldron_med_b"]["new_values"], {"body_armor": 19, "arm_armor": 0})

    def test_keep_weights_and_material_type_but_rescope_extremity_loot_tables(self):
        by_id = {c["id"]: c for c in self._run("gondor", "arm_armors.xml", "arm")}
        c = by_id["sk_gd_x_bracer_med_a"]
        self.assertEqual(c["new_weight"], c["old_weight"])
        self.assertEqual(c["new_material"], "Chainmail")
        self.assertEqual(c["new_modifier"], "cloth")        # medium arm loot table
        self.assertEqual(c["new_values"], {"arm_armor": 22})  # 57 * 0.6 * 0.64 = 21.9
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
        self.assertNotIn('leg_armor="29"', head)
        self.assertIn('leg_armor="29"', tail)      # the live item moved to Gondor's elite greaves
        self.assertNotIn('leg_armor="20"', tail)

    def test_apply_is_byte_faithful_and_backs_up_once(self):
        body = self.armory / "gondor" / "body_armors.xml"
        arm = self.armory / "gondor" / "arm_armors.xml"
        cape = self.armory / "mordor" / "shoulder_armors.xml"
        self._run("gondor", "body_armors.xml", "body", dry_run=False)
        self._run("gondor", "arm_armors.xml", "arm", dry_run=False)
        self._run("mordor", "shoulder_armors.xml", "shoulder", dry_run=False)
        b = body.read_bytes()
        self.assertIn(b"\r\n<Items>\r\n", b)                                  # CRLF kept
        self.assertIn(b'body_armor="57" arm_armor="19" material_type="Plate" modifier_group="plate"', b)
        self.assertIn(b'weight="18.5"', b)
        self.assertIn(b'body_armor="60" arm_armor="30"', b)                   # hero kit untouched
        a = arm.read_bytes()
        self.assertTrue(a.startswith(b"\xef\xbb\xbf"))                        # BOM kept
        self.assertIn(b'arm_armor="22" material_type="Chainmail" modifier_group="cloth"', a)
        self.assertNotIn(b"\r\n", a)                                          # LF file stays LF
        self.assertIn(b'body_armor="34" arm_armor="28"', cape.read_bytes())
        self.assertTrue(body.with_name("body_armors.xml.bak-t").exists())
        first = body.with_name("body_armors.xml.bak-t").read_bytes()
        self.assertIn(b'body_armor="42" arm_armor="14"', first)
        # A second apply changes nothing and does not overwrite the backup.
        changes = self._run("gondor", "body_armors.xml", "body", dry_run=False)
        self.assertEqual([c["id"] for c in changes if c["status"] == "CHANGED"], [])
        self.assertEqual(body.with_name("body_armors.xml.bak-t").read_bytes(), first)


if __name__ == "__main__":
    unittest.main()
