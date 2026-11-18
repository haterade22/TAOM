#!/usr/bin/env python3
"""Tests for tools/generate_armour_band_clones.py (armour band clones, 2026-11-17).

Run:  python -m unittest tools.tests.test_generate_armour_band_clones

THE CONTRACT
------------
The restat prices an Armory armour piece at the band of its lowest battle wearer. When a troop of
tier 7 to 10 (level rebalance_armor.TOP_TIER_LEVEL, 36, or higher) and a troop of level 30 or lower
wear the same piece, the low troop drags the piece down for the top-tier troop (the Mirkwood palace
guard chest at body 8 on a level 51 guard). Such a piece is "shared down" and takes one treatment
(treatment()):
  A  an elite troop's own piece: each band of its low wearers gets a clone, a TAOM item with the
     source's look, name key and text, not merchandise, at that band's restat values (never above the
     source at the elite band), id taom_band_<lt|md|hv>_<source>;
  B  a low or mid piece: its level 36+ wearers swap to a stronger piece of the same line and slot
     (pick_upgrade), never lower on a region and never past the worn ceiling; no clone. A B piece
     with no such piece falls back to A.
Battle sets only; civilian sets keep the source. Every other piece is left alone. The whole-file
troop generators apply the same swap (armour_for_troop), and the kit tools read a clone as its source.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_armour_band_clones as gbc  # noqa: E402
import rebalance_armor as ra  # noqa: E402

SOURCE = (
    '    <Item\r\n'
    '        id="mirkwood_body"\r\n'
    '        name="{=aom_mirkwood_body_name}[Mirkwood] Palace Guard Body Armor"\r\n'
    '        subtype="body_armor"\r\n'
    '        mesh="mirkwood_body"\r\n'
    '        culture="Culture.mirkwood"\r\n'
    '        is_merchandise="true"\r\n'
    '        weight="5.2"\r\n'
    '        Type="BodyArmor">\r\n'
    '        <ItemComponent>\r\n'
    '            <Armor\r\n'
    '                body_armor="8"\r\n'
    '                arm_armor="0"\r\n'
    '                has_gender_variations="false"\r\n'
    '                covers_body="true"\r\n'
    '                modifier_group="leather"\r\n'
    '                material_type="Leather" />\r\n'
    '        </ItemComponent>\r\n'
    '        <Flags UseTeamColor="true" />\r\n'
    '    </Item>'
)


def troop(level, *sets, file="troops_x.xml"):
    return {"level": level, "sets": [dict(s) for s in sets], "file": file}


class CloneIdTests(unittest.TestCase):
    def test_an_id_round_trips_and_names_its_band(self):
        cid = ra.band_clone_id("mirkwood_body", "light")
        self.assertEqual(cid, "taom_band_lt_mirkwood_body")
        self.assertEqual(ra.band_clone_parts(cid), ("mirkwood_body", "light"))
        self.assertEqual(ra.band_clone_source(cid), "mirkwood_body")
        self.assertIsNone(ra.band_clone_parts("mirkwood_body"))
        self.assertIsNone(ra.band_clone_parts("taom_band_xx_mirkwood_body"))

    def test_no_clone_band_is_elite(self):
        with self.assertRaises(ValueError):
            ra.band_clone_id("mirkwood_body", "elite")

    def test_a_clone_reads_as_its_source_for_line_and_mesh_tier(self):
        for src, folder in (("sk_md_num_chest_heavy_a", "mordor"), ("sk_md_orc_inf_chest_med_b", "mordor"),
                            ("sk_dg_chest_light_a", "rhun"), ("mirkwood_body", "mirkwood")):
            for band in ("light", "medium", "heavy"):
                cid = ra.band_clone_id(src, band)
                self.assertEqual(ra.kingdom_key(cid, folder), ra.kingdom_key(src, folder), cid)
                self.assertEqual(ra.mesh_tier_of(cid), ra.mesh_tier_of(src), cid)
                self.assertEqual(ra.is_lord_kit(cid), ra.is_lord_kit(src), cid)


class SharedDownTests(unittest.TestCase):
    """rebalance_armor.shared_down_pieces: the scope rule, one function for the generator and the gate."""

    def test_a_piece_a_top_tier_and_a_low_troop_wear_is_in_scope_with_each_low_band(self):
        troops = {"guard": troop(51, {"Body": "p"}), "warden": troop(41, {"Body": "p"}),
                  "militia": troop(11, {"Body": "p"}), "vet": troop(16, {"Body": "p"}),
                  "sentry": troop(31, {"Body": "p"})}
        got = ra.shared_down_pieces(troops)
        self.assertEqual(sorted(got), ["p"])
        self.assertEqual(got["p"]["top"], ["guard", "warden"])
        self.assertEqual(got["p"]["low"], {"light": ["militia"], "medium": ["vet"]})

    def test_a_piece_whose_best_wearer_is_under_level_36_is_out_of_scope(self):
        troops = {"a": troop(31, {"Body": "p"}), "b": troop(11, {"Body": "p"})}
        self.assertEqual(ra.shared_down_pieces(troops), {})

    def test_a_piece_only_elite_band_troops_wear_is_out_of_scope(self):
        troops = {"a": troop(46, {"Head": "h"}), "b": troop(31, {"Head": "h"})}
        self.assertEqual(ra.shared_down_pieces(troops), {})

    def test_a_noble_is_judged_at_its_anchor_level_and_exempt_troops_and_pairs_do_not_count(self):
        troops = {"top": troop(36, {"Body": "p", "Leg": "q"}), "noble": troop(26, {"Body": "p"}),
                  "plain": troop(26, {"Leg": "q"}), "ex": troop(11, {"Body": "p"}),
                  "pair": troop(21, {"Body": "p"})}
        got = ra.shared_down_pieces(troops, exempt={"ex"}, noble={"noble"}, exempt_items={("pair", "p")})
        self.assertNotIn("p", got)   # the noble L26 anchors at 31 (elite band); the rest are exempt
        self.assertEqual(got["q"]["low"], {"heavy": ["plain"]})

    def test_eligible_and_source_of_filter_and_map_ids(self):
        troops = {"top": troop(41, {"Body": "p", "Cape": "vanilla"}),
                  "low": troop(11, {"Body": "taom_band_lt_p", "Cape": "vanilla"})}
        self.assertEqual(ra.shared_down_pieces(troops, eligible=lambda i: i != "vanilla"), {})
        got = ra.shared_down_pieces(troops, eligible=lambda i: i != "vanilla", source_of=ra.band_clone_source)
        self.assertEqual(got["p"]["low"], {"light": ["low"]})

    def test_wearer_band_uses_the_noble_anchor(self):
        self.assertEqual(ra.wearer_band(26, "x_chest_a", noble=True), "elite")
        self.assertEqual(ra.wearer_band(26, "x_chest_a"), "heavy")
        self.assertEqual(ra.wearer_band(11, "x_chest_a"), "light")


class RestatValuesTests(unittest.TestCase):
    def test_restat_armour_is_what_process_file_writes(self):
        body = SOURCE.replace("\r\n", "\n").replace('body_armor="8"', 'body_armor="25"').replace(
            'arm_armor="0"', 'arm_armor="10"')
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "mirkwood" / "body_armors.xml"
            path.parent.mkdir()
            path.write_text("<Items>\n" + body + "\n</Items>\n", encoding="utf-8")
            for band in ("light", "medium", "heavy", "elite"):
                rec = {"mirkwood_body": {"anchorLevel": {"light": 11, "medium": 16, "heavy": 21, "elite": 41}[band],
                                         "tier": band, "tierSource": "roster"}}
                change = ra.process_file(str(path), "body", dry_run=True, tier_source="roster-first",
                                         roster_map=rec, keep_weights=True, keep_material_type=True)[0]
                values, group = ra.restat_armour("body", band, "mirkwood", "mirkwood_body",
                                                 {"body_armor": 25, "arm_armor": 10},
                                                 line_shares=ra.line_secondary_shares(tmp))
                self.assertEqual(values, change["new_values"], band)
                self.assertEqual(group, change["new_modifier"], band)

    def test_clone_values_follow_the_band_and_never_pass_the_elite_source(self):
        declared = {"body_armor": 25, "arm_armor": 10}
        elite, _ = gbc.clone_values("mirkwood_body", "body", "mirkwood", "elite", declared)
        last = None
        for band in ("light", "medium", "heavy"):
            values, group = gbc.clone_values("mirkwood_body", "body", "mirkwood", band, declared)
            self.assertEqual(group, ra.modifier_group_for("body", band))
            self.assertEqual(values["body_armor"], ra.cap_value(ra.KINGDOM_CAPS["mirkwood"], "body", band))
            for stat, v in values.items():
                self.assertLessEqual(v, elite[stat], (band, stat))
            if last is not None:
                self.assertGreater(values["body_armor"], last)
            last = values["body_armor"]
        light, _ = gbc.clone_values("mirkwood_body", "body", "mirkwood", "light", declared)
        self.assertEqual(light["arm_armor"], 0)   # no secondary below the heavy band

    def test_a_generator_owned_source_is_priced_as_its_generator_prices(self):
        declared = {"body_armor": 20, "leg_armor": 0}
        values, group = gbc.clone_values("sk_ll_mw_chest_med_a", "body", "lothlorien", "heavy", declared)
        row = ra.calculate_stats("heavy", "body", "lothlorien", item_id="sk_ll_mw_chest_med_a")
        self.assertEqual(values, {"body_armor": row["body_armor"], "leg_armor": row["leg_armor"]})
        self.assertEqual(group, row["modifier_group"])


class RenderTests(unittest.TestCase):
    def test_the_clone_keeps_name_look_weight_and_material_and_is_not_merchandise(self):
        xml = gbc.render_item(SOURCE.replace("\r\n", "\n"), "mirkwood_body", "light",
                              {"body_armor": 8, "arm_armor": 0}, "cloth_unarmoured")
        self.assertIn('id="taom_band_lt_mirkwood_body"', xml)
        self.assertIn('name="{=aom_mirkwood_body_name}[Mirkwood] Palace Guard Body Armor"', xml)
        self.assertIn('mesh="mirkwood_body"', xml)
        self.assertIn('weight="5.2"', xml)
        self.assertIn('material_type="Leather"', xml)
        self.assertIn('is_merchandise="false"', xml)
        self.assertNotIn('is_merchandise="true"', xml)
        self.assertIn('modifier_group="cloth_unarmoured"', xml)
        self.assertIn('body_armor="8"', xml)

    def test_a_source_without_is_merchandise_gets_it(self):
        xml = gbc.render_item(SOURCE.replace("\r\n", "\n").replace('        is_merchandise="true"\n', ""),
                              "mirkwood_body", "heavy", {"body_armor": 33, "arm_armor": 0}, "plate")
        self.assertIn('is_merchandise="false"', xml)

    def test_the_file_parses_and_uses_the_newline_asked_for(self):
        import xml.etree.ElementTree as ET
        block = gbc.render_item(SOURCE.replace("\r\n", "\n"), "mirkwood_body", "light",
                                {"body_armor": 8, "arm_armor": 0}, "cloth_unarmoured")
        text = gbc.render_file([("taom_band_lt_mirkwood_body", "a comment", block)])
        root = ET.fromstring(text.encode("utf-8"))
        self.assertEqual([i.get("id") for i in root.iter("Item")], ["taom_band_lt_mirkwood_body"])
        self.assertEqual(text.count("\n"), text.count("\r\n"))


class SpecTests(unittest.TestCase):
    def test_a_spec_needs_known_bands_and_no_repeated_source(self):
        good = {"clones": {"p": ["light", "heavy"]}, "upgrades": {"q": {"to": "r", "troops": ["t", "u"]}},
                "fallback": ["p"]}
        self.assertEqual(gbc.clone_table(gbc.check_spec(good)),
                         {("p", "light"): "taom_band_lt_p", ("p", "heavy"): "taom_band_hv_p"})
        self.assertEqual(gbc.upgrade_table(good), {("t", "q"): "r", ("u", "q"): "r"})
        for bad in ({"clones": {"p": ["elite"]}}, {"clones": {"p": []}}, {"clones": {"p": ["light", "light"]}},
                    {"clones": []}, {},
                    {"clones": {"p": ["light"]}, "upgrades": {"p": {"to": "r", "troops": ["t"]}}},
                    {"clones": {}, "upgrades": {"q": {"to": "q", "troops": ["t"]}}},
                    {"clones": {}, "upgrades": {"q": {"to": "r", "troops": []}}},
                    {"clones": {}, "upgrades": {}, "fallback": ["q"]}):
            with self.assertRaises(gbc.CloneError, msg=bad):
                gbc.check_spec(bad)

    def test_the_spec_text_round_trips(self):
        import json
        spec = {"description": ["d"], "clones": {"p": ["light"]}, "upgrades": {"q": {"to": "r", "troops": ["t"]}},
                "fallback": [], "retired": ["taom_band_md_p"]}
        self.assertEqual(json.loads(gbc.spec_text(spec)), spec)


class TreatmentTests(unittest.TestCase):
    """The classifier: A (duplicate for the low wearers) or B (upgrade the level 36+ wearers)."""

    def test_the_maintainers_examples(self):
        self.assertEqual(gbc.treatment("mirkwood_body", "[Mirkwood] Palace Guard Body Armor"), "A")
        self.assertEqual(gbc.treatment("rivendell_boots_greaves3", "[Noldor] Gold Greaves III"), "A")
        self.assertEqual(gbc.treatment("sk_md_orc_inf_chest_med_b", "[Mordor] Mordor Orc Chain+Scale I"), "B")
        self.assertEqual(gbc.treatment("sk_gd_ano_bracer_inf_med_a", "[Gondor] Anorien Bracers"), "B")
        self.assertEqual(gbc.treatment("cts_rohan_boots1", "[Rohan] Boots I"), "B")

    def test_the_id_token_decides_before_the_name(self):
        self.assertEqual(gbc.treatment("x_chest_elite_a", "Levy Coat I"), "A")
        self.assertEqual(gbc.treatment("x_chest_lord_a", "Coat"), "A")
        self.assertEqual(gbc.treatment("x_chest_light_a", "Royal Coat"), "B")
        self.assertEqual(gbc.treatment("x_chest_heavy_a", "Royal Coat"), "B")

    def test_a_name_without_a_token_says_low_or_is_a_named_piece(self):
        for name in ("[Dale] Coat II", "[Dale] Coat II - Red", "[Dale] Militia Coat", "[Dale] Infantry Helmet",
                     "[Dale] Levy Cap", "Coat I"):
            self.assertEqual(gbc.treatment("x_coat", name), "B", name)
        for name in ("[Dale] Coat III", "[Dale] Elite Knight Boots B04", "[Dale] Coat IV", "", "Infantryman's Pride"):
            self.assertEqual(gbc.treatment("x_coat", name), "A", name)

    def test_a_low_word_beats_a_high_numeral_and_a_hyphen_numeral_is_read(self):
        self.assertEqual(gbc.treatment("x_coat", "[Gondor] Infantry Coat III"), "B")
        self.assertEqual(gbc.treatment("x_coat", "[Rohan] Coat T1-I"), "B")
        self.assertEqual(gbc.treatment("x_coat", "[Rohan] Coat T1-III"), "A")


_NAMES = iter(range(10 ** 6))


def source(slot="body", culture="gondor", type_="BodyArmor", declared=None):
    return {"folder": culture, "slot": slot, "culture": culture, "type": type_, "name": f"Coat {next(_NAMES)}", "xml": "",
            "declared": declared or {"body_armor": 30, "leg_armor": 10}}


class UpgradeTests(unittest.TestCase):
    """pick_upgrade: a stronger piece of the same line and slot for the level 36+ wearers of a B piece."""

    SOURCES = {
        "tst_chest_med_a": source(),
        "tst_chest_heavy_a": source(),
        "tst_chest_elite_a": source(),
        "zzz_chest_elite_a": source(),
        "tst_chest_lord_a": source(),                       # lord line: never an upgrade
        "tst_mord_chest_elite_a": source(culture="mordor"),  # another kingdom line
        "tst_worn_chest_elite_a": source(),                  # a level 11 troop wears it
        "tst_helm_elite_a": source(slot="head", type_="HeadArmor", declared={"head_armor": 30}),
    }

    def scope(self):
        return {"top": ["top"], "low": {"medium": ["low"]}}

    def campaign(self, extra=None):
        return {"top": {"level": 41, "sets": [dict({"Body": "tst_chest_med_a"}, **(extra or {}))], "file": "f.xml"},
                "low": {"level": 16, "sets": [{"Body": "tst_chest_med_a"}], "file": "f.xml"}}

    def test_candidates_keep_line_slot_and_a_higher_tier_and_skip_lord_and_low_worn_pieces(self):
        got = gbc.candidates("tst_chest_med_a", self.SOURCES, {"tst_worn_chest_elite_a": 11}, [(41, False)])
        self.assertEqual(got, ["tst_chest_elite_a", "tst_chest_heavy_a", "zzz_chest_elite_a"])

    def test_a_level_36_troop_prefers_a_tier_its_ladder_allows(self):
        got = gbc.candidates("tst_chest_med_a", self.SOURCES, {}, [(36, False)])
        self.assertNotIn("tst_chest_lord_a", got)
        self.assertEqual(got[0], "tst_chest_elite_a")

    def test_a_colour_variant_of_the_source_is_never_an_upgrade(self):
        sources = {"roh_x_arm_g": dict(source(culture="rohan"), name="[Rohan] Scalemail II - Green"),
                   "roh_x_arm_r": dict(source(culture="rohan"), name="[Rohan] Scalemail II - Red"),
                   "roh_x_arm_y": dict(source(culture="rohan"), name="[Rohan] Scalemail V - Yellow")}
        self.assertEqual(gbc.candidates("roh_x_arm_g", sources, {}, [(41, False)]), ["roh_x_arm_y"])

    def test_lord_armour_is_only_an_upgrade_inside_a_lord_line_and_the_smallest_step_wins(self):
        sources = {"tst_chest_med_a": source(), "tst_noble_chest_elite_a": source(), "tst_noble_chest": source(),
                   "tst_chest_lord_a": source(), "tst_nob_chest_med_a": source()}
        # a tiered noble piece is a soldier grade; untiered or lord-tier lord armour is not, for a plain source
        self.assertEqual(gbc.candidates("tst_chest_med_a", sources, {}, [(41, False)]), ["tst_noble_chest_elite_a"])
        # a lord-line source may take its own line's pieces; the elite step comes before the lord step
        got = gbc.candidates("tst_nob_chest_med_a", sources, {}, [(41, False)])
        self.assertLess(got.index("tst_chest_lord_a") if "tst_chest_lord_a" in got else 99, 99)
        self.assertLess(got.index("tst_noble_chest_elite_a"), got.index("tst_chest_lord_a"))

    def test_the_pick_is_stronger_on_every_region_and_priced_at_the_elite_band(self):
        to, theirs, mine = gbc.pick_upgrade("tst_chest_med_a", self.scope(), self.SOURCES, {}, self.campaign(),
                                            {}, {}, frozenset())
        self.assertEqual(to, "tst_chest_elite_a")
        declared = {"body_armor": 30, "leg_armor": 10}
        self.assertEqual(theirs, gbc._stats(ra.restat_armour("body", "elite", "gondor", "tst_chest_elite_a", declared)[0]))
        self.assertEqual(mine, gbc._stats(ra.restat_armour("body", "medium", "gondor", "tst_chest_med_a", declared)[0]))
        for stat in gbc.ARMOUR_STATS:
            self.assertGreaterEqual(theirs[stat], mine[stat], stat)
        self.assertGreater(theirs["body_armor"], mine["body_armor"])

    def test_a_pick_that_would_pass_the_worn_ceiling_is_refused(self):
        live = {"vanilla_cape": {"stats": {"body_armor": 70}}}
        to, why, _ = gbc.pick_upgrade("tst_chest_med_a", self.scope(), self.SOURCES, {},
                                      self.campaign({"Cape": "vanilla_cape"}), live, {}, frozenset())
        self.assertIsNone(to)
        self.assertIn("ceiling", why)

    def test_a_pick_that_would_out_armour_the_troops_upgrade_target_is_refused(self):
        # the source has no secondary, the replacement has one: at the elite band the replacement is the stronger
        # (the source sits at the heavy band already, 35 over Gondor's 31, so the restat keeps its leg at 0)
        sources = {"tst_chest_med_a": source(declared={"body_armor": 35, "leg_armor": 0}),
                   "tst_chest_elite_a": source(declared={"body_armor": 30, "leg_armor": 10})}
        campaign = self.campaign()
        campaign["top"]["upgrades"] = ["child"]
        # the child wears the source piece at the elite band: what the top troop would wear under treatment A
        campaign["child"] = {"level": 46, "sets": [{"Body": "tst_chest_med_a"}], "file": "f.xml", "upgrades": []}
        to, why, _ = gbc.pick_upgrade("tst_chest_med_a", self.scope(), sources, {}, campaign, {}, {}, frozenset())
        self.assertIsNone(to)
        self.assertIn("child", why)
        # an edge between two militia troops is not judged, as in the validator
        to, _, _ = gbc.pick_upgrade("tst_chest_med_a", self.scope(), sources, {}, campaign, {}, {},
                                    frozenset(), militia=frozenset({"top", "child"}))
        self.assertEqual(to, "tst_chest_elite_a")
        # a child that wears the source too takes the same pick, so the edge holds
        tops = {"top": ["top", "child"], "low": {"medium": ["low"]}}
        to, _, _ = gbc.pick_upgrade("tst_chest_med_a", tops, sources, {}, campaign, {}, {}, frozenset())
        self.assertEqual(to, "tst_chest_elite_a")
        # a child that stays stronger takes the upgrade
        campaign["child"]["sets"] = [{"Body": "strong"}]
        to, _, _ = gbc.pick_upgrade("tst_chest_med_a", self.scope(), sources, {}, campaign,
                                    {"strong": {"stats": {"body_armor": 99}}}, {}, frozenset())
        self.assertEqual(to, "tst_chest_elite_a")

    def test_no_candidate_is_a_reason_not_a_crash(self):
        campaign = {"top": {"level": 41, "sets": [{"Head": "tst_helm_elite_a"}]}, "low": {"level": 11, "sets": []}}
        to, why, _ = gbc.pick_upgrade("tst_helm_elite_a", {"top": ["top"], "low": {"light": ["low"]}},
                                      self.SOURCES, {}, campaign, {}, {}, frozenset())
        self.assertIsNone(to)
        self.assertTrue(why)

    def test_a_plan_after_apply_reads_an_upgrade_back_as_its_source(self):
        troops = {"top": {"level": 41, "sets": [{"Body": "r", "Head": "h"}], "file": "f"},
                  "other": {"level": 41, "sets": [{"Body": "r"}], "file": "f"}}
        got = gbc.as_planned_from(troops, {("top", 0, "Body"): ("p", "r")})
        self.assertEqual(got["top"]["sets"], [{"Body": "p", "Head": "h"}])
        self.assertEqual(got["other"]["sets"], [{"Body": "r"}])   # the same item, no record: it was native

    def test_two_sources_upgraded_to_one_piece_each_read_back_at_its_own_position(self):
        troops = {"top": {"level": 41, "sets": [{"Leg": "r", "Head": "h"}, {"Leg": "r"}], "file": "f"}}
        got = gbc.as_planned_from(troops, {("top", 0, "Leg"): ("p", "r"), ("top", 1, "Leg"): ("q", "r")})
        self.assertEqual(got["top"]["sets"], [{"Leg": "p", "Head": "h"}, {"Leg": "q"}])
        self.assertNotIn("extra_sets", got["top"])

    def test_a_record_that_no_longer_matches_the_roster_reads_the_item_as_native(self):
        # the slot was edited by hand after the upgrade wrote it: it is no longer the replacement
        troops = {"top": {"level": 41, "sets": [{"Body": "edited"}], "file": "f"}}
        got = gbc.as_planned_from(troops, {("top", 0, "Body"): ("p", "r")})
        self.assertEqual(got["top"]["sets"], [{"Body": "edited"}])

    def test_the_candidates_keep_the_culture_attribute(self):
        # darkhun_ironbound (Culture.khuzait) took a Dol Guldur bracer through the shared sk_dg_ prefix (F3).
        sources = {"sk_dg_khml_bracer_heavy_a": dict(source(culture="rhun", slot="arm", type_="HandArmor"),
                                                     attr_culture="Culture.khuzait"),
                   "sk_dg_uruk_bracer_elite_a": dict(source(culture="rhun", slot="arm", type_="HandArmor"),
                                                     attr_culture="Culture.dolguldur"),
                   "sk_dg_khml_bracer_elite_a": dict(source(culture="rhun", slot="arm", type_="HandArmor"),
                                                     attr_culture="Culture.khuzait")}
        got = gbc.candidates("sk_dg_khml_bracer_heavy_a", sources, {}, [(41, False)])
        self.assertEqual(got, ["sk_dg_khml_bracer_elite_a"])


class UpgradeColourPickTests(unittest.TestCase):
    """pick_upgrade's colour branch (the maintainer 2026-11-18): a silver chest never goes beside bronze pauldrons."""

    SOURCES = UpgradeTests.SOURCES
    SCOPE = {"top": ["top"], "low": {"medium": ["low"]}}

    def campaign(self, extra):
        return {"top": {"level": 41, "sets": [dict({"Body": "tst_chest_med_a"}, **extra)], "file": "f.xml"},
                "low": {"level": 16, "sets": [{"Body": "tst_chest_med_a"}], "file": "f.xml"}}

    def pick(self, colours, extra, swapped=None):
        return gbc.pick_upgrade("tst_chest_med_a", self.SCOPE, self.SOURCES, {"tst_worn_chest_elite_a": 11},
                                self.campaign(extra), {}, swapped or {}, frozenset(), colours=colours)

    def test_a_clashing_candidate_is_skipped_and_the_next_is_taken(self):
        colours = {"tst_chest_med_a": "bronze", "cape_bronze": "bronze", "tst_chest_elite_a": "silver",
                   "tst_chest_heavy_a": "bronze"}
        to, _theirs, _mine = self.pick(colours, {"Cape": "cape_bronze"})
        self.assertEqual(to, "tst_chest_heavy_a")   # the first candidate, tst_chest_elite_a, is silver
        self.assertEqual(self.pick(None, {"Cape": "cape_bronze"})[0], "tst_chest_elite_a")   # no table: no judging

    def test_every_candidate_clashing_is_no_pick_and_says_which_piece_clashed(self):
        colours = {"tst_chest_med_a": "bronze", "cape_bronze": "bronze", "tst_chest_elite_a": "silver",
                   "tst_chest_heavy_a": "silver", "zzz_chest_elite_a": "silver"}
        to, why, _mine = self.pick(colours, {"Cape": "cape_bronze"})
        self.assertIsNone(to)   # build() then clones the piece instead (treatment A)
        self.assertIn("clashes with top Cape cape_bronze", why)

    def test_a_pick_is_judged_against_the_swap_an_earlier_source_already_took(self):
        colours = {"tst_chest_med_a": "silver", "cape_src": "silver", "cape_gold": "gold", "tst_chest_elite_a": "silver",
                   "tst_chest_heavy_a": "gold", "zzz_chest_elite_a": "gold"}
        extra = {"Cape": "cape_src"}
        self.assertEqual(self.pick(colours, extra)[0], "tst_chest_elite_a")   # silver beside a silver cape
        # the cape is swapped to a gold one by another pick: the silver chest now clashes with it
        self.assertEqual(self.pick(colours, extra, swapped={"top": {"cape_src": "cape_gold"}})[0], "tst_chest_heavy_a")

    def test_a_clone_in_the_set_is_read_as_its_source(self):
        colours = {"tst_chest_med_a": "bronze", "pauld_bronze": "bronze", "tst_chest_elite_a": "silver"}
        sets = [{"Body": "tst_chest_med_a", "Cape": "taom_band_lt_pauld_bronze"}]
        self.assertEqual(gbc.colour_clash("tst_chest_elite_a", "tst_chest_med_a", sets, colours),
                         "Cape taom_band_lt_pauld_bronze bronze")


class ColourTableTests(unittest.TestCase):
    """colour_table: build() never plans on a table that is empty or lacks a piece of the Armory it reads."""

    def table(self, rows):
        import scan_armour_colours as sac
        return mock.patch.object(sac, "load_table", return_value=rows)

    def test_an_empty_table_is_an_error_that_names_the_repair(self):
        with self.table({}), self.assertRaises(gbc.CloneError) as ctx:
            gbc.colour_table({"a": {}})
        self.assertIn("scan_armour_colours.py --apply", str(ctx.exception))

    def test_a_piece_without_a_row_is_an_error_naming_the_first_ids_and_the_repair(self):
        sources = {f"p{i:02d}": {} for i in range(13)}
        with self.table({"p00": "gold", "p01": "silver"}), self.assertRaises(gbc.CloneError) as ctx:
            gbc.colour_table(sources)
        text = str(ctx.exception)
        self.assertIn("11 piece(s)", text)
        self.assertIn("p02, p03", text)
        self.assertNotIn("p12", text)
        self.assertIn("scan_armour_colours.py --apply", text)

    def test_a_complete_table_is_returned(self):
        rows = {"a": "gold", "b": "silver", "extra": "red"}
        with self.table(rows):
            self.assertEqual(gbc.colour_table({"a": {}, "b": {}}), rows)

    def test_build_stops_on_an_empty_table_before_planning(self):
        import derive_armor_tiers as dat
        import scan_armour_colours as sac
        with mock.patch.object(gbc, "load_spec", return_value={"clones": {}, "upgrades": {}, "fallback": []}), \
                mock.patch.object(gbc, "load_troops", return_value=({}, {})), \
                mock.patch.object(dat, "build_armory_index", return_value=({}, None)), \
                mock.patch.object(gbc, "plan_scope", return_value={}), \
                mock.patch.object(gbc, "read_sources", return_value={"a": {}}), \
                mock.patch.object(gbc, "check_shipped_pieces"), \
                mock.patch.object(sac, "load_table", return_value={}):
            with self.assertRaises(gbc.CloneError) as ctx:
                gbc.build(Path("armory"), Path("game"))
        self.assertIn("colour table", str(ctx.exception))


class SwapRuleTests(unittest.TestCase):
    TABLE = {("p", "light"): "taom_band_lt_p", ("p", "heavy"): "taom_band_hv_p"}

    def test_a_listed_top_tier_troop_gets_its_upgrade(self):
        ups = {("top", "q"): "r"}
        with mock.patch.object(gbc, "_rule_sets", return_value=(frozenset({"ex"}), frozenset(), frozenset())):
            self.assertEqual(gbc.armour_for_troop("q", "top", 41, self.TABLE, ups), "r")
            self.assertEqual(gbc.armour_for_troop("q", "fow_top", 41, self.TABLE, ups), "r")
            self.assertEqual(gbc.armour_for_troop("q", "other", 41, self.TABLE, ups), "q")
            self.assertEqual(gbc.armour_for_troop("q", "low", 11, self.TABLE, ups), "q")   # a B piece has no clone
            self.assertEqual(gbc.armour_for_troop("q", "ex", 41, self.TABLE, {("ex", "q"): "r"}), "q")

    def test_a_low_troop_gets_the_clone_of_its_band_and_the_rest_keep_the_source(self):
        with mock.patch.object(gbc, "spec_table", return_value=self.TABLE), \
                mock.patch.object(gbc, "spec_upgrades", return_value={}), \
                mock.patch.object(gbc, "_rule_sets", return_value=(frozenset({"ex"}), frozenset({"nob"}),
                                                                     frozenset({("pair", "p")}))):
            self.assertEqual(gbc.armour_for_troop("p", "t", 11), "taom_band_lt_p")
            self.assertEqual(gbc.armour_for_troop("p", "t", 26), "taom_band_hv_p")
            self.assertEqual(gbc.armour_for_troop("p", "t", 16), "p")      # no medium clone
            self.assertEqual(gbc.armour_for_troop("p", "t", 41), "p")
            self.assertEqual(gbc.armour_for_troop("q", "t", 11), "q")
            self.assertEqual(gbc.armour_for_troop("p", "ex", 11), "p")     # a ladder-exempt troop
            self.assertEqual(gbc.armour_for_troop("p", "pair", 11), "p")   # an exempt (troop, item) pair
            self.assertEqual(gbc.armour_for_troop("p", "nob", 26), "p")    # a noble L26 anchors elite
            # a clone the troop already wears is read as its source first
            self.assertEqual(gbc.armour_for_troop("taom_band_lt_p", "t", 26), "taom_band_hv_p")
            self.assertEqual(gbc.armour_for_troop("taom_band_lt_p", "t", 41), "p")
            # a Fords of War copy follows the rule of its campaign troop
            self.assertEqual(gbc.armour_for_troop("p", "fow_ex", 11), "p")

    def test_swaps_touch_only_battle_slots_that_change(self):
        troops = {"low": {"level": 11, "file": "f.xml", "sets": [{"Body": "p", "Head": "h"}, {"Body": "p"}]},
                  "top": {"level": 41, "file": "f.xml", "sets": [{"Body": "p"}]}}
        with mock.patch.object(gbc, "_rule_sets", return_value=(frozenset(), frozenset(), frozenset())):
            swaps = gbc.plan_swaps(troops, self.TABLE, {})
        self.assertEqual(swaps, [{"file": "f.xml", "troop": "low", "slot": "Body", "old": "p",
                                  "new": "taom_band_lt_p", "set_indexes": [0, 1]}])

    def test_a_generated_troop_file_is_owed_to_its_generator(self):
        swaps = [{"file": "x/troops_arthedain.xml"}, {"file": "x/troops_lothlorien.xml"}, {"file": "x/troops_dale.xml"},
                 {"file": "x/fords_troops.xml"}]
        import fix_melee_ladder as fml
        self.assertNotIn("split_generated", vars(gbc))   # one copy, in fix_melee_ladder
        mine, owed = fml.split_generated(swaps)
        self.assertEqual([s["file"] for s in mine], ["x/troops_dale.xml", "x/fords_troops.xml"])
        self.assertEqual(len(owed), 2)

    def test_a_fords_copy_follows_its_campaign_troops_rule(self):
        self.assertEqual((gbc.rule_id("fow_militia_x"), gbc.rule_id("militia_x")), ("militia_x", "militia_x"))

    def test_a_civilian_set_takes_the_clone_and_never_the_upgrade(self):
        ups = {("top", "q"): "r"}
        with mock.patch.object(gbc, "_rule_sets", return_value=(frozenset(), frozenset(), frozenset())):
            self.assertEqual(gbc.armour_for_troop("p", "low", 11, self.TABLE, ups, civilian=True), "taom_band_lt_p")
            self.assertEqual(gbc.armour_for_troop("q", "top", 41, self.TABLE, ups, civilian=True), "q")
            self.assertEqual(gbc.armour_for_troop("p", "top", 41, self.TABLE, ups, civilian=True), "p")


class AppliedReadBackTests(unittest.TestCase):
    """plan_swaps reads rosters where an applied upgrade already names its replacement. It maps the replacement back
    to its source at the positions the shipped spec says the upgrade wrote (review 2026-11-18), so a changed or
    dropped pick reaches the rosters and a slot that held the replacement natively is never touched."""

    def troops(self, *items):
        return {"top": {"level": 41, "file": "f.xml", "sets": [{"Body": i} for i in items]}}

    def run_swaps(self, troops, upgrades, written):
        with mock.patch.object(gbc, "_rule_sets", return_value=(frozenset(), frozenset(), frozenset())):
            return gbc.plan_swaps(troops, {}, upgrades, written)

    def test_a_changed_pick_reaches_the_rosters(self):
        swaps = self.run_swaps(self.troops("r"), {("top", "p"): "r2"}, {("top", 0, "Body"): ("p", "r")})
        self.assertEqual([(s["old"], s["new"], s["set_indexes"]) for s in swaps], [("r", "r2", [0])])

    def test_a_dropped_pick_puts_the_source_back(self):
        swaps = self.run_swaps(self.troops("r"), {}, {("top", 0, "Body"): ("p", "r")})
        self.assertEqual([(s["old"], s["new"]) for s in swaps], [("r", "p")])

    def test_an_unchanged_pick_is_no_swap(self):
        self.assertEqual(self.run_swaps(self.troops("r"), {("top", "p"): "r"}, {("top", 0, "Body"): ("p", "r")}), [])

    def test_a_wearer_that_natively_wears_the_target_keeps_that_set_when_the_target_changes(self):
        # imladris_marksman: sets 5 and 7 held rivendell_torso_infantry_tier2_silver natively, set 11 held the
        # source until the upgrade wrote the same piece there. A new pick moved all of them (review 2026-11-18).
        troops = self.troops("r", "r", "r", "r")
        written = {("top", 1, "Body"): ("p", "r"), ("top", 3, "Body"): ("p", "r")}
        swaps = self.run_swaps(troops, {("top", "p"): "r2"}, written)
        self.assertEqual([(s["old"], s["new"], s["set_indexes"]) for s in swaps], [("r", "r2", [1, 3])])
        # the same where the native piece is the target itself
        swaps = self.run_swaps(self.troops("r", "r"), {("top", "p"): "r2"}, {("top", 1, "Body"): ("p", "r")})
        self.assertEqual([(s["old"], s["new"], s["set_indexes"]) for s in swaps], [("r", "r2", [1])])
        # a dropped pick restores the written sets only
        swaps = self.run_swaps(self.troops("r", "r"), {}, {("top", 1, "Body"): ("p", "r")})
        self.assertEqual([(s["old"], s["new"], s["set_indexes"]) for s in swaps], [("r", "p", [1])])

    def test_two_sources_on_one_replacement_move_apart_when_the_new_plan_parts(self):
        written = {("top", 0, "Body"): ("p", "r"), ("top", 1, "Body"): ("q", "r")}
        swaps = self.run_swaps(self.troops("r", "r"), {("top", "p"): "r2", ("top", "q"): "r3"}, written)
        self.assertEqual(sorted((s["new"], s["set_indexes"]) for s in swaps), [("r2", [0]), ("r3", [1])])

    def test_a_fords_copy_is_read_back_by_its_own_id_and_its_campaign_troops_rule(self):
        troops = {"fow_top": {"level": 41, "file": "f.xml", "sets": [{"Body": "r"}]}}
        swaps = self.run_swaps(troops, {("top", "p"): "r2"}, {("fow_top", 0, "Body"): ("p", "r")})
        self.assertEqual([(s["troop"], s["new"]) for s in swaps], [("fow_top", "r2")])

    def test_the_written_positions_are_the_slots_that_wear_a_source_of_a_listed_upgrade(self):
        troops = {"top": {"level": 41, "file": "f", "sets": [{"Body": "p", "Head": "h"}, {"Body": "r"}, {"Body": "p"}]},
                  "fow_top": {"level": 41, "file": "f", "sets": [{"Body": "p"}]},
                  "low": {"level": 11, "file": "f", "sets": [{"Body": "p"}]}}
        got = gbc.written_positions(troops, {("top", "p"): "r"})
        self.assertEqual(got, {"p": {"top": {"Body": [0, 2]}, "fow_top": {"Body": [0]}}})

    def test_a_spec_records_where_each_upgrade_wrote(self):
        spec = {"clones": {}, "upgrades": {"p": {"to": "r", "troops": ["top"], "wrote": {"top": {"Body": [0, 2]}}}},
                "fallback": []}
        self.assertEqual(gbc.written_table(gbc.check_spec(spec)), {("top", 0, "Body"): ("p", "r"),
                                                                    ("top", 2, "Body"): ("p", "r")})
        for bad in ({"top": ["Body"]}, {"top": {"Body": "0"}}, {"top": {"Body": [-1]}}, []):
            with self.assertRaises(gbc.CloneError, msg=bad):
                gbc.check_spec(dict(spec, upgrades={"p": {"to": "r", "troops": ["top"], "wrote": bad}}))

    def test_the_spec_text_keeps_the_record(self):
        import json
        spec = {"description": ["d"], "clones": {}, "fallback": [], "retired": [],
                "upgrades": {"p": {"to": "r", "troops": ["top"], "wrote": {"top": {"Body": [0, 2]}}}}}
        self.assertEqual(json.loads(gbc.spec_text(spec)), spec)


class GeneratorAppliesTheSwapTests(unittest.TestCase):
    """A troop in a generated file gets its clone or upgrade through its generator, under the same rule."""

    def realm_troop(self, tid, level):
        import realm_troops as rt
        realm = rt.Realm(culture="arthedain", label="[Arthedain]", script="x.py", pt_marker="X", militia={},
                         merc_source="", merc_name="", civilian_kits=({"Body": "src_chest"},))
        tr = rt.Troop(tid, "X", level, "Infantry", "spear", rt.same("spear_y", "shield_z"),
                      ({"Body": "src_chest"}, {"Body": "src_chest"}), realm=realm)
        tr.sk = dict.fromkeys(rt.SKILLS, 0)
        return tr

    def test_a_realm_generator_renders_the_clone_in_battle_and_civilian_rosters(self):
        # A low troop's civilian set follows its battle sets (review 2026-11-17): Field Commission copies a
        # troop's sets to a companion (HeroCommissionAdapter), so a civilian source would reach a player raised.
        tr = self.realm_troop("arthedain_x", 11)
        with mock.patch.object(gbc, "spec_table", return_value={("src_chest", "light"): "taom_band_lt_src_chest"}), \
                mock.patch.object(gbc, "spec_upgrades", return_value={}):
            xml = tr.xml("\n")
        self.assertEqual(xml.count('slot="Body" id="Item.taom_band_lt_src_chest"'), 3)
        self.assertEqual(xml.count('slot="Body" id="Item.src_chest"'), 0)

    def test_a_realm_generator_renders_an_upgrade_for_the_listed_troop_in_battle_only(self):
        tr = self.realm_troop("arthedain_y", 41)
        with mock.patch.object(gbc, "spec_table", return_value={}), \
                mock.patch.object(gbc, "spec_upgrades", return_value={("arthedain_y", "src_chest"): "better_chest"}):
            xml = tr.xml("\n")
        self.assertEqual(xml.count('slot="Body" id="Item.better_chest"'), 2)
        self.assertEqual(xml.count('slot="Body" id="Item.src_chest"'), 1)   # an upgrade is a battle swap

    def test_the_lothlorien_generator_renders_the_clone(self):
        import generate_lothlorien_troops as lorien
        tr = next(t for t in lorien.TROOPS if t["id"] == "lorien_militia_spearman")
        body = lorien.sets_of(tr)[0]["Body"]
        with mock.patch.object(gbc, "spec_table", return_value={(body, "light"): "taom_band_lt_" + body}), \
                mock.patch.object(gbc, "spec_upgrades", return_value={}):
            xml = lorien.render(lorien.TROOPS, {})
        block = next(b for b in xml.split("<NPCCharacter") if f'id="{tr["id"]}"' in b)
        self.assertIn(f'id="Item.taom_band_lt_{body}"', block)
        self.assertNotIn(f'slot="Body" id="Item.{body}"', block)

    def test_the_snow_orc_generator_renders_the_clone(self):
        import generate_snow_orc_troops as snow
        tr = snow.TROOPS[0]
        body = snow.sets_of(tr)[0]["Body"]
        band = ra.level_to_band(snow.TIER_LEVEL[tr["tier"]])
        with mock.patch.object(gbc, "spec_table", return_value={(body, band): ra.band_clone_id(body, band)}), \
                mock.patch.object(gbc, "spec_upgrades", return_value={}):
            xml = snow.render([tr], {})
        self.assertIn(f'id="Item.{ra.band_clone_id(body, band)}"', xml)


class KitToolsKeepTheCloneTests(unittest.TestCase):
    """A kit never hands out the source of a clone: the source is priced at the elite band (review 2026-11-17,
    the Mirkwood recruit kits went from 23 to 90). A career kit keeps its donor troop's clone; an enlistment kit
    takes the clone of its rank's band."""

    def test_the_donor_maps_do_not_read_a_clone_as_its_source(self):
        import generate_career_kits as ck
        import generate_enlistment_rosters as er
        with mock.patch.object(gbc, "spec_table", return_value={("p", "light"): "taom_band_lt_p"}):
            self.assertNotIn("taom_band_lt_p", ck.placeholder_donors())
            self.assertNotIn("taom_band_lt_p", er.placeholder_donors())

    def test_an_enlistment_kit_takes_the_clone_of_its_rank(self):
        import generate_enlistment_rosters as er
        table = {("p", "light"): "taom_band_lt_p", ("p", "medium"): "taom_band_md_p"}
        kit = {"Body": "taom_band_md_p", "Leg": "p", "Item0": "p", "Head": "h"}
        with mock.patch.object(gbc, "spec_table", return_value=table):
            self.assertEqual(er.kit_at_rank(kit, "recruit"),
                             {"Body": "taom_band_lt_p", "Leg": "taom_band_lt_p", "Item0": "p", "Head": "h"})
            self.assertEqual(er.kit_at_rank(kit, "soldier")["Leg"], "taom_band_md_p")
            self.assertEqual(er.kit_at_rank(kit, "veteran")["Body"], "p")    # no heavy clone: the source
            self.assertEqual(er.kit_at_rank(kit, "sergeant")["Body"], "p")   # elite: the source

    def test_band_text_puts_every_set_outside_a_troop_battle_set_at_its_band(self):
        # promote_borrowed_cultures.py clones Gondor's enlistment kits into Arthedain and Rhudaur whole: it runs
        # band_text on each block, or its next --apply hands a recruit the elite-priced source again.
        table = {("p", "light"): "taom_band_lt_p", ("p", "medium"): "taom_band_md_p"}
        kit = ROSTERS.format(rid="enlist_x_archer_recruit")
        with mock.patch.object(gbc, "spec_table", return_value=table):
            out = gbc.band_text(kit, "equipmentsets/taom_enlistment_equipment.xml")
            self.assertEqual(out, kit.replace('slot="Leg" id="Item.p"', 'slot="Leg" id="Item.taom_band_lt_p"'))
            sergeant = ROSTERS.format(rid="enlist_x_archer_sergeant")
            self.assertEqual(gbc.band_text(sergeant, "equipmentsets/taom_enlistment_equipment.xml"), sergeant)
            lord = ROSTERS.format(rid="x_civ_template_default_d")
            self.assertEqual(gbc.band_text(lord, "equipmentsets/taom_equipment_sets_dale.xml"), lord)
            self.assertEqual(gbc.band_text(out, "equipmentsets/taom_enlistment_equipment.xml"), out)   # idempotent

    def test_a_promoted_block_takes_the_band_rule_after_its_item_map(self):
        import promote_borrowed_cultures as pbc
        kits = "equipmentsets/taom_enlistment_equipment.xml"
        block = ('  <EquipmentRoster id="enlist_y_infantry_recruit" culture="Culture.y">\r\n'
                 '    <EquipmentSet><Equipment slot="Body" id="Item.old_chest" /></EquipmentSet>\r\n'
                 '  </EquipmentRoster>\r\n')
        npcs = NPCS.replace('id="Item.p"', 'id="Item.old_chest"')
        cfg = {"item_map_files": [kits, "characters/npcs_{c}.xml"], "item_map": {"old_chest": "p"}}
        with mock.patch.object(gbc, "spec_table", return_value={("p", "light"): "taom_band_lt_p"}), \
                mock.patch.object(gbc, "spec_upgrades", return_value={}):
            shared = pbc.band_block(block, kits, kits, cfg)
            standalone = pbc.band_block(npcs, "characters/npcs_{c}.xml", "characters/npcs_y.xml", cfg)
            unmapped = pbc.band_block(block, kits, kits, {})
        self.assertEqual(shared, block.replace("Item.old_chest", "Item.taom_band_lt_p"))
        self.assertIn('<EquipmentRoster civilian="true"><equipment slot="Body" id="Item.taom_band_lt_p" />', standalone)
        self.assertIn('level="35" occupation="Soldier">\r\n    <Equipments><EquipmentRoster><equipment slot="Gloves" '
                      'id="Item.p" />', standalone)   # an elite wearer keeps the mapped source
        self.assertEqual(unmapped, block)   # no item map: old_chest is no clone source

    def test_the_rank_bands_are_the_generators(self):
        import generate_enlistment_rosters as er
        self.assertEqual({r: gbc.enlist_rank_band(r) for r in er.RANKS},
                         {r: ra.level_to_band(er.RANK_BANDS[r][0]) for r in er.RANKS})
        self.assertEqual([gbc.enlist_rank_band(r) for r in er.RANKS], ["light", "medium", "heavy", "elite"])


class LordPoolTests(unittest.TestCase):
    def test_a_clone_is_never_a_lord_pool_piece(self):
        import fix_lord_kits as lk
        items = {"x_chest_lord_a": {"folder": "gondor", "type": "BodyArmor", "file": "body_armors.xml", "name": "A"},
                 "taom_band_lt_x_chest_lord_a": {"folder": "gondor", "type": "BodyArmor", "file": "c.xml",
                                                 "name": "A"}}
        troops = {"t": {"culture": "gondor", "race": None, "level": 11,
                        "sets": [{"Body": "taom_band_lt_x_chest_lord_a"}]},
                  "u": {"culture": "gondor", "race": None, "level": 41, "sets": [{"Body": "x_chest_lord_a"}]}}
        pool = lk.lord_pool("gondor", items, troops, [], variants={})
        members = [i for ids in pool["slots"].values() for i in ids]
        self.assertNotIn("taom_band_lt_x_chest_lord_a", members)


class ShippedSpecTests(unittest.TestCase):
    def test_the_shipped_spec_loads_and_every_clone_is_in_the_clone_file(self):
        import xml.etree.ElementTree as ET
        table = gbc.spec_table()
        self.assertGreater(len(table) + len(gbc.spec_upgrades()), 0)
        ids = {i.get("id") for i in ET.parse(gbc.OUT_FILE).getroot().iter("Item")}
        # A retired clone stays in the file (retired_blocks), so the file holds the planned and the retired ids.
        self.assertEqual(ids, set(table.values()) | set(gbc.load_spec().get("retired", [])))

    def test_the_submodule_loads_the_clone_file(self):
        text = gbc.SUBMODULE.read_text(encoding="utf-8-sig")
        self.assertIn(f'path="{gbc.SUBMODULE_PATH}"', text)


class CloneStabilityTests(unittest.TestCase):
    """A clone is priced from its source as the restat leaves it, so --check gives the same clone before and
    after the restat apply (2026-11-17): a rounding difference between the two would be drift."""

    def test_a_clone_is_the_same_before_and_after_the_restat_apply(self):
        shares = {(k, "body", "arm_armor"): 0.17 for k in ra.KINGDOM_CAPS}
        cases = [("body", {"body_armor": b, "arm_armor": a, "leg_armor": l})
                 for b in (2, 8, 17, 29, 35) for a in (0, 3, 7, 9) for l in (0, 5, 11)]
        cases += [("shoulder", {"body_armor": b, "arm_armor": a}) for b in (2, 5, 8, 10) for a in (0, 2, 7, 9)]
        drift = []
        for culture in sorted(ra.KINGDOM_CAPS):
            iid = f"tst_{culture}_piece"
            if ra.kingdom_key(iid, culture) is None:
                continue
            for slot, declared in cases:
                elite, _ = gbc.clone_values(iid, slot, culture, "elite", declared, shares)
                after = dict(declared, **elite)
                for band in ("light", "medium", "heavy"):
                    before = gbc.clone_values(iid, slot, culture, band, declared, shares)
                    again = gbc.clone_values(iid, slot, culture, band, after, shares)
                    if before != again:
                        drift.append((culture, slot, declared, band, before[0], again[0]))
        self.assertEqual(drift[:5], [])

    def test_a_raised_source_gives_its_heavy_clone_a_secondary(self):
        shares = {("mirkwood", "body", "arm_armor"): 8 / 48}
        declared = {"body_armor": 8, "arm_armor": 0, "leg_armor": 0}
        heavy, _ = gbc.clone_values("mkwd_inf3_chest", "body", "mirkwood", "heavy", declared, shares)
        self.assertGreater(heavy["arm_armor"], 0)
        self.assertGreater(heavy["leg_armor"], 0)
        medium, _ = gbc.clone_values("mkwd_inf3_chest", "body", "mirkwood", "medium", declared, shares)
        self.assertEqual((medium["arm_armor"], medium["leg_armor"]), (0, 0))

    def test_a_clone_reads_the_same_from_its_source_before_and_after_the_restat_adds_a_stat(self):
        after = ra._add_armour_attribute(SOURCE, "leg_armor", 19)
        self.assertIn('                arm_armor="0"\r\n                leg_armor="19"\r\n', after)   # its own line
        values = {"body_armor": 8, "arm_armor": 0, "leg_armor": 0}
        self.assertEqual(gbc.render_item(after, "mirkwood_body", "light", values, "leather"),
                         gbc.render_item(SOURCE, "mirkwood_body", "light", values, "leather"))

    def test_a_source_given_a_leg_it_never_declared_gives_its_heavy_clone_the_leg_before_and_after_the_apply(self):
        shares = {("mirkwood", "body", "arm_armor"): 8 / 48, ("mirkwood", "body", "leg_armor"): 0.40}
        declared = {"body_armor": 8, "arm_armor": 0}
        elite, _ = gbc.clone_values("mirkwood_body", "body", "mirkwood", "elite", declared, shares)
        self.assertGreater(elite["leg_armor"], 0)
        heavy, _ = gbc.clone_values("mirkwood_body", "body", "mirkwood", "heavy", declared, shares)
        self.assertGreater(heavy["leg_armor"], 0)
        self.assertLessEqual(heavy["leg_armor"], elite["leg_armor"])
        again = gbc.clone_values("mirkwood_body", "body", "mirkwood", "heavy", dict(declared, **elite), shares)
        self.assertEqual(again[0], heavy)


NPCS = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n<NPCCharacters>\r\n'
    '  <NPCCharacter id="townsfolk_x" occupation="Townsfolk">\r\n'
    '    <Equipments>\r\n'
    '      <EquipmentRoster><equipment slot="Body" id="Item.p" /></EquipmentRoster>\r\n'
    '      <EquipmentRoster civilian="true"><equipment slot="Body" id="Item.p" /></EquipmentRoster>\r\n'
    '    </Equipments>\r\n'
    '  </NPCCharacter>\r\n'
    '  <!-- <NPCCharacter id="commented" level="11"><Equipments><EquipmentRoster>'
    '<equipment slot="Body" id="Item.p" /></EquipmentRoster></Equipments></NPCCharacter> -->\r\n'
    '  <NPCCharacter id="guard_x" level="16" occupation="Soldier">\r\n'
    '    <Equipments><EquipmentRoster><equipment slot="Gloves" id="Item.p" /></EquipmentRoster></Equipments>\r\n'
    '  </NPCCharacter>\r\n'
    '  <NPCCharacter id="master_x" level="26" occupation="CaravanGuard">\r\n'
    '    <Equipments><EquipmentRoster><equipment slot="Gloves" id="Item.p" /></EquipmentRoster></Equipments>\r\n'
    '  </NPCCharacter>\r\n'
    '  <NPCCharacter id="practice_x" level="35" occupation="Soldier">\r\n'
    '    <Equipments><EquipmentRoster><equipment slot="Gloves" id="Item.p" /></EquipmentRoster></Equipments>\r\n'
    '  </NPCCharacter>\r\n'
    '  <NPCCharacter id="lord_x" occupation="Lord" is_hero="true">\r\n'
    '    <Equipments><EquipmentRoster><equipment slot="Body" id="Item.taom_band_lt_p" /></EquipmentRoster>'
    '<EquipmentSet id="x_civ" equipmentType="Civilian" /></Equipments>\r\n'
    '  </NPCCharacter>\r\n'
    '</NPCCharacters>\r\n')

ROSTERS = (
    '<EquipmentRosters>\r\n'
    '  <EquipmentRoster id="{rid}" culture="Culture.x">\r\n'
    '    <EquipmentSet><Equipment slot="Leg" id="Item.p" /><Equipment slot="Item0" id="Item.p" /></EquipmentSet>\r\n'
    '    <EquipmentSet equipmentType="Civilian"><Equipment slot="Leg" id="Item.p" /></EquipmentSet>\r\n'
    '  </EquipmentRoster>\r\n'
    '</EquipmentRosters>\r\n')

TROOPS = (
    '<NPCCharacters>\r\n'
    '  <NPCCharacter id="militia_x" level="11">\r\n'
    '    <Equipments>\r\n'
    '      <EquipmentRoster><equipment slot="Body" id="Item.taom_band_lt_p" /></EquipmentRoster>\r\n'
    '      <EquipmentRoster civilian="true"><equipment slot="Body" id="Item.p" /></EquipmentRoster>\r\n'
    '    </Equipments>\r\n'
    '  </NPCCharacter>\r\n'
    '  <NPCCharacter id="guard_y" level="41">\r\n'
    '    <Equipments>\r\n'
    '      <EquipmentRoster><equipment slot="Body" id="Item.p" /></EquipmentRoster>\r\n'
    '      <EquipmentRoster civilian="true"><equipment slot="Body" id="Item.p" /></EquipmentRoster>\r\n'
    '    </Equipments>\r\n'
    '  </NPCCharacter>\r\n'
    '</NPCCharacters>\r\n')

NO_RULES = (frozenset(), frozenset(), frozenset())


def wearers(text, rel):
    return {(w.container, w.civilian, w.slot): w for w in gbc.scan_text(text, rel)}


class WearerScanTests(unittest.TestCase):
    """Every set that names a clone source outside the troop battle sets (review 2026-11-17, F1): NPCs, enlistment
    kits, wanderer kits, character creation, child templates and the civilian sets of troops. A wearer with a
    level takes the clone of its band, one with none the light clone, and a lord keeps the source."""

    def test_an_npc_wears_the_band_of_its_level_and_an_unlevelled_one_the_light_band(self):
        got = wearers(NPCS, "characters/npcs_x.xml")
        self.assertEqual(got[("townsfolk_x", False, "Body")].band, "light")
        self.assertEqual(got[("townsfolk_x", True, "Body")].band, "light")
        self.assertEqual(got[("guard_x", False, "Gloves")].band, "medium")
        self.assertEqual(got[("master_x", False, "Gloves")].band, "heavy")
        self.assertEqual(got[("practice_x", False, "Gloves")].band, "elite")
        self.assertIsNone(got[("lord_x", False, "Body")].band)
        self.assertNotIn("commented", {w.container for w in got.values()})
        self.assertFalse(any(w.troop for w in got.values()))

    def test_a_roster_file_reads_its_band_from_the_file_and_the_roster_id(self):
        def band(rid, fname):
            return {(w.civilian, w.slot): w.band for w in gbc.scan_text(ROSTERS.format(rid=rid), fname)}
        self.assertEqual(band("enlist_mirkwood_infantry_recruit", "equipmentsets/taom_enlistment_equipment.xml"),
                         {(False, "Leg"): "light", (True, "Leg"): "light"})
        self.assertEqual(band("enlist_x_archer_soldier", "equipmentsets/taom_enlistment_equipment.xml")[(False, "Leg")],
                         "medium")
        self.assertEqual(band("enlist_x_archer_sergeant", "equipmentsets/taom_enlistment_equipment.xml")[(False, "Leg")],
                         "elite")
        self.assertEqual(band("mother_char_creation_none_x", "equipmentsets/taom_char_creation_equipment.xml")[
            (False, "Leg")], "light")
        self.assertEqual(band("child_template_x", "equipmentsets/taom_child_equipment_templates.xml")[(True, "Leg")],
                         "light")
        self.assertIsNone(band("x_civ_template_default_d", "equipmentsets/taom_equipment_sets_dale.xml")[(True, "Leg")])
        self.assertIsNone(band("taom_x_lord_civilian_male", "equipmentsets/taom_lord_template_equipment.xml")[
            (True, "Leg")])

    def test_a_weapon_slot_is_not_read(self):
        got = gbc.scan_text(ROSTERS.format(rid="r"), "equipmentsets/x.xml")
        self.assertEqual({w.slot for w in got}, {"Leg"})

    def test_a_troop_file_follows_the_troop_rule_in_both_kinds_of_set(self):
        table = {("p", "light"): "taom_band_lt_p"}
        with mock.patch.object(gbc, "_rule_sets", return_value=NO_RULES):
            got = wearers(TROOPS, "troops/troops_x.xml")
            self.assertTrue(all(w.troop for w in got.values()))
            self.assertEqual(gbc.planned_for(got[("militia_x", True, "Body")], table, {}), "taom_band_lt_p")
            self.assertEqual(gbc.planned_for(got[("militia_x", False, "Body")], table, {}), "taom_band_lt_p")
            self.assertEqual(gbc.planned_for(got[("guard_y", True, "Body")], table, {}), "p")
            self.assertEqual(got[("militia_x", True, "Body")].band, "light")
            self.assertEqual(got[("guard_y", False, "Body")].band, "elite")
            self.assertEqual([w.line for w in gbc.scan_text(TROOPS, "troops/troops_x.xml")], [4, 5, 10, 11])
            unlevelled = wearers(TROOPS.replace(' level="11"', ''), "troops/troops_x.xml")
            self.assertIsNone(unlevelled[("militia_x", False, "Body")].band)
        fords = wearers(TROOPS.replace('id="militia_x"', 'id="fow_militia_x"'), "fords_of_war/fords_troops.xml")
        self.assertTrue(fords[("fow_militia_x", True, "Body")].troop)

    def test_planned_for_a_non_troop_wearer(self):
        table = {("p", "light"): "taom_band_lt_p", ("p", "heavy"): "taom_band_hv_p"}
        got = wearers(NPCS, "characters/npcs_x.xml")
        self.assertEqual(gbc.planned_for(got[("townsfolk_x", True, "Body")], table, {}), "taom_band_lt_p")
        self.assertEqual(gbc.planned_for(got[("master_x", False, "Gloves")], table, {}), "taom_band_hv_p")
        self.assertEqual(gbc.planned_for(got[("practice_x", False, "Gloves")], table, {}), "p")
        self.assertEqual(gbc.planned_for(got[("lord_x", False, "Body")], table, {}), "p")   # a lord: the source
        self.assertEqual(gbc.armour_for_band("taom_band_lt_p", "heavy", table), "taom_band_hv_p")
        self.assertEqual(gbc.armour_for_band("q", "light", table), "q")

    def test_the_bands_a_clone_must_cover(self):
        got = gbc.scan_text(NPCS, "characters/npcs_x.xml") + gbc.scan_text(
            ROSTERS.format(rid="x_civ_template_default_d"), "equipmentsets/taom_equipment_sets_dale.xml")
        self.assertEqual(gbc.wearer_bands(got), {"p": {"light", "medium", "heavy"}})


class WearerWriteTests(unittest.TestCase):
    def test_a_rewrite_changes_only_the_named_sets_and_keeps_the_bytes(self):
        edits = {("townsfolk_x", True, "Body", "p"): "taom_band_lt_p", ("guard_x", False, "Gloves", "p"): "taom_band_md_p"}
        out = gbc.rewrite_text(NPCS, "characters/npcs_x.xml", edits)
        civ = '<EquipmentRoster civilian="true"><equipment slot="Body" id="Item.%s" />'
        guard = 'level="16" occupation="Soldier">\r\n    <Equipments><EquipmentRoster><equipment slot="Gloves" id="Item.%s" />'
        expected = NPCS.replace(civ % "p", civ % "taom_band_lt_p").replace(guard % "p", guard % "taom_band_md_p")
        self.assertEqual(out, expected)   # the townsfolk battle set, the comment and the lord are untouched
        self.assertNotEqual(out, NPCS)

    def test_write_worn_swaps_keeps_a_bom_and_parses_first(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp)
            (md / "characters").mkdir()
            path = md / "characters" / "npcs_x.xml"
            path.write_bytes(b"\xef\xbb\xbf" + NPCS.encode("utf-8"))
            swaps = [{"file": "characters/npcs_x.xml", "container": "guard_x", "civilian": False, "slot": "Gloves",
                      "old": "p", "new": "taom_band_md_p"}]
            self.assertEqual(gbc.write_worn_swaps(swaps, md), ["characters/npcs_x.xml"])
            raw = path.read_bytes()
            self.assertTrue(raw.startswith(b"\xef\xbb\xbf"))
            self.assertIn(b'<equipment slot="Gloves" id="Item.taom_band_md_p" />', raw)
            self.assertEqual(gbc.write_worn_swaps(swaps, md), [])   # nothing left to change


class SourcesWornLowTests(unittest.TestCase):
    """The validator's BAND_CLONE_SOURCE_WORN_LOW reads gbc.sources_worn_low: a set below the elite band that names
    the source of a clone. The source is priced at the elite band, so such a wearer gets elite armour."""

    def test_only_a_set_below_the_elite_band_is_reported(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp)
            for rel, text in (("characters/npcs_x.xml", NPCS), ("troops/troops_x.xml", TROOPS),
                              ("equipmentsets/taom_lord_template_equipment.xml", ROSTERS.format(rid="taom_x_lord")),
                              ("equipmentsets/taom_char_creation_equipment.xml", ROSTERS.format(rid="mother_x"))):
                (md / rel).parent.mkdir(parents=True, exist_ok=True)
                (md / rel).write_text(text, encoding="utf-8", newline="")
            with mock.patch.object(gbc, "_rule_sets", return_value=NO_RULES):
                got = gbc.sources_worn_low(md, {"p"})
        self.assertEqual(sorted((w.file, w.container, w.civilian) for w in got), [
            ("characters/npcs_x.xml", "guard_x", False), ("characters/npcs_x.xml", "master_x", False),
            ("characters/npcs_x.xml", "townsfolk_x", False), ("characters/npcs_x.xml", "townsfolk_x", True),
            ("equipmentsets/taom_char_creation_equipment.xml", "mother_x", False),
            ("equipmentsets/taom_char_creation_equipment.xml", "mother_x", True),
            ("troops/troops_x.xml", "militia_x", True)])


class RetentionTests(unittest.TestCase):
    """A shipped clone id is never dropped (review 2026-11-17, F4): a save or a mod that names it would find the slot
    empty. A clone the plan no longer needs stays in the file, still not merchandise, listed as retired."""

    OLD = ('<?xml version="1.0" encoding="utf-8"?>\r\n<Items>\r\n'
           '    <!-- taom_band_lt_p: x -->\r\n    <Item id="taom_band_lt_p" is_merchandise="false">\r\n'
           '        <ItemComponent><Armor body_armor="8" /></ItemComponent>\r\n    </Item>\r\n</Items>\r\n')

    def test_a_retired_clone_keeps_its_block(self):
        got = gbc.retired_blocks(self.OLD, {"taom_band_lt_p"}, set())
        self.assertEqual([cid for cid, _c, _x in got], ["taom_band_lt_p"])
        self.assertIn('body_armor="8"', got[0][2])
        self.assertIn("retired", got[0][1])
        self.assertEqual(gbc.retired_blocks(self.OLD, {"taom_band_lt_p"}, {"taom_band_lt_p"}), [])

    def test_a_shipped_source_out_of_scope_keeps_its_clones_while_another_set_wears_one(self):
        # The Mirkwood militia left the bronze pieces (2026-11-18): villager and wanderer kits still wear their
        # light clones, so the source stays cloned at their bands, never back to the elite source.
        shipped = {"p": ["light", "medium"], "q": ["light"], "r": ["medium"], "s": ["light"]}
        scope = {"r": {"top": ["t"], "low": {"medium": ["m"]}}}
        bands = {"p": {"light"}, "r": {"medium"}, "s": {"light"}}
        self.assertEqual(gbc.kept_sources(shipped, scope, bands, {"p", "q", "r"}, {"p": 41}), ["p"])

    def test_a_source_a_low_battle_troop_still_wears_is_not_kept(self):
        # Its top wearers left: the restat prices it at its low troops' band, so its clones retire.
        self.assertEqual(gbc.kept_sources({"p": ["light"]}, {}, {"p": {"light"}}, {"p"}, {"p": 11}), [])
        self.assertEqual(gbc.kept_sources({"p": ["light"]}, {}, {"p": {"light"}}, {"p"}, {}), ["p"])

    def test_a_shipped_upgrade_source_out_of_scope_is_kept_too(self):
        # A B source the plan no longer upgrades goes back to the elite band; a kit that wore it raw keeps a clone.
        self.assertEqual(gbc.kept_sources({"u": {"to": "v"}}, {}, {"u": {"light"}}, {"u"}, {"u": 36}), ["u"])


    def test_a_shipped_clone_missing_from_the_file_is_an_error(self):
        with self.assertRaises(gbc.CloneError):
            gbc.retired_blocks(self.OLD, {"taom_band_md_q"}, set())

    def test_the_shipped_ids_are_the_spec_and_the_file(self):
        spec = {"clones": {"q": ["medium"]}, "upgrades": {}, "fallback": [], "retired": ["taom_band_hv_r"]}
        self.assertEqual(gbc.shipped_clone_ids(spec, self.OLD),
                         {"taom_band_md_q", "taom_band_hv_r", "taom_band_lt_p"})

    def test_a_spec_retired_id_must_be_a_clone_id_not_also_planned(self):
        gbc.check_spec({"clones": {"q": ["medium"]}, "upgrades": {}, "retired": ["taom_band_lt_p"]})
        for bad in (["x"], ["taom_band_md_q"]):
            with self.assertRaises(gbc.CloneError, msg=bad):
                gbc.check_spec({"clones": {"q": ["medium"]}, "upgrades": {}, "retired": bad})


class HeldUpgradeTests(unittest.TestCase):
    """A shipped upgrade whose source no low battle troop wears any more stays while the replacement clashes with
    no top wearer's set (2026-11-18: the Mirkwood recruits and sentinels keep their T3 helmets when the militia
    leave the T1 and T2 helmets)."""

    ROW = {"to": "v", "troops": ["t"], "wrote": {"t": {"Head": [0]}}}

    def held(self, scope=None, low=None, live=("u", "v"), clash=None):
        return gbc.held_upgrades({"u": self.ROW}, scope or {}, low or {"u": 36}, set(live), lambda src, row: clash)

    def test_held_when_no_low_battle_troop_wears_the_source_and_nothing_clashes(self):
        self.assertEqual(self.held(), {"u": {"to": "v", "troops": ["t"]}})
        self.assertEqual(self.held(low={"x": 11}), {"u": {"to": "v", "troops": ["t"]}})

    def test_dropped_on_a_clash_in_scope_on_a_low_battle_wearer_or_a_missing_piece(self):
        self.assertEqual(self.held(clash="Cape p gold"), {})
        self.assertEqual(self.held(scope={"u": {}}), {})
        self.assertEqual(self.held(low={"u": 11}), {})
        self.assertEqual(self.held(live=("u",)), {})


class DeriveSeesTheClonesTests(unittest.TestCase):
    def test_a_clone_is_indexed_in_its_sources_folder_and_slot(self):
        import derive_armor_tiers as dat
        with tempfile.TemporaryDirectory() as tmp:
            armory = Path(tmp) / "items"
            (armory / "mirkwood").mkdir(parents=True)
            (armory / "mirkwood" / "body_armors.xml").write_bytes(
                ("<Items>\r\n" + SOURCE + "\r\n</Items>\r\n").encode("utf-8"))
            clones = Path(tmp) / "armour_band_clones.xml"
            clones.write_bytes(gbc.render_file(
                [("taom_band_lt_mirkwood_body", "c", gbc.render_item(
                    SOURCE, "mirkwood_body", "light", {"body_armor": 8, "arm_armor": 0}, "leather"))]).encode("utf-8"))
            with mock.patch.object(ra, "ARMORY_DIR", str(armory)), \
                    mock.patch.object(dat, "LORD_VARIANTS_XML", str(Path(tmp) / "none.xml")), \
                    mock.patch.object(dat, "BAND_CLONES_XML", str(clones)):
                index, _ = dat.build_armory_index()
        self.assertEqual((index["taom_band_lt_mirkwood_body"]["folder"], index["taom_band_lt_mirkwood_body"]["slot"]),
                         ("mirkwood", "body"))


class OnDiskTests(unittest.TestCase):
    """--check compares text with its newlines made one: core.autocrlf checks a file out CRLF (tooling F1)."""

    def test_either_newline_on_disk_matches_the_render(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "f"
            path.write_bytes(b"a\r\nb\r\n")
            self.assertEqual(gbc._on_disk(path), "a\nb\n")
            self.assertFalse(gbc._stale(path, "a\nb\n"))
            path.write_bytes(b"a\nb\n")
            self.assertFalse(gbc._stale(path, "a\r\nb\r\n"))
            self.assertTrue(gbc._stale(path, "a\nc\n"))
            self.assertTrue(gbc._stale(Path(tmp) / "missing", "a"))
            self.assertIsNone(gbc._on_disk(Path(tmp) / "missing"))


ITEM = ('<Item id="{iid}" name="{{=k}}[Mirkwood] Coat" Type="BodyArmor" culture="Culture.mirkwood">'
        '<ItemComponent><Armor body_armor="8" /></ItemComponent></Item>')


class ReadSourcesTests(unittest.TestCase):
    def armory(self, tmp, text):
        folder = Path(tmp) / "mirkwood"
        folder.mkdir()
        (folder / "body_armors.xml").write_bytes(text)
        return tmp

    def test_a_commented_copy_is_skipped(self):
        with tempfile.TemporaryDirectory() as tmp:
            text = ("<Items><!-- " + ITEM.format(iid="old_p") + " -->" + ITEM.format(iid="p") + "</Items>")
            got = gbc.read_sources(self.armory(tmp, text.encode("utf-8")))
        self.assertEqual(sorted(got), ["p"])
        self.assertEqual(got["p"]["attr_culture"], "Culture.mirkwood")

    def test_bytes_that_are_not_utf8_are_an_error(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(gbc.CloneError):
                gbc.read_sources(self.armory(tmp, b"<Items>\xff</Items>"))

    def test_no_slot_file_is_no_armory(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(gbc.NoArmory):
                gbc.read_sources(tmp)

    def test_a_shipped_piece_off_the_armory_is_refused(self):
        spec = {"clones": {"p": ["light"]}, "upgrades": {"q": {"to": "r", "troops": ["t"]}}, "fallback": []}
        gbc.check_shipped_pieces(spec, {"p": {}, "q": {}, "r": {}})
        with self.assertRaises(gbc.CloneError) as ctx:
            gbc.check_shipped_pieces(spec, {"p": {}, "q": {}})
        self.assertIn("r", str(ctx.exception))


class SubModuleTests(unittest.TestCase):
    TEXT = ("<Module>\r\n  <Xmls>\r\n    <XmlNode>\r\n" + gbc.SUBMODULE_ANCHOR + "\r\n    </XmlNode>\r\n"
            "  </Xmls>\r\n</Module>\r\n")

    def test_the_node_goes_in_once_after_the_anchor_with_the_files_newlines(self):
        out = gbc.submodule_text(self.TEXT)
        self.assertEqual(out.count(f'path="{gbc.SUBMODULE_PATH}"'), 1)
        self.assertLess(out.index("lord_armour_variants"), out.index(gbc.SUBMODULE_PATH))
        self.assertEqual(gbc.submodule_text(out), out)
        self.assertNotIn("\n", out.replace("\r\n", ""))

    def test_a_missing_anchor_is_an_error(self):
        with self.assertRaises(gbc.CloneError):
            gbc.submodule_text("<Module></Module>")


class MainTests(unittest.TestCase):
    """main()'s exit codes and writes, on a plan build() hands it (no Armory needed)."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.spec, self.out, self.sub = root / "spec.json", root / "clones.xml", root / "SubModule.xml"
        self.sub.write_bytes(SubModuleTests.TEXT.encode("utf-8"))
        armory = root / "armory"
        armory.mkdir()
        spec = {"description": ["d"], "clones": {"p": ["light"]}, "upgrades": {}, "fallback": [], "retired": []}
        self.plan = {"spec": spec, "pieces": [], "text": "<Items>\r\n</Items>\r\n", "swaps": [], "worn_swaps": [],
                     "keys": 0}
        self.patches = [mock.patch.object(gbc, "SPEC", self.spec), mock.patch.object(gbc, "OUT_FILE", self.out),
                        mock.patch.object(gbc, "SUBMODULE", self.sub), mock.patch.object(ra, "ARMORY_DIR", str(armory)),
                        mock.patch.object(gbc, "build", side_effect=lambda *a, **k: self.plan),
                        mock.patch.object(gbc, "loc_backlog_keys", return_value=0)]
        for p in self.patches:
            p.start()

    def tearDown(self):
        for p in self.patches:
            p.stop()
        self._tmp.cleanup()

    def run_main(self, *args):
        import contextlib
        import io
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf), contextlib.redirect_stderr(io.StringIO()):
            code = gbc.entry(list(args))
        return code, buf.getvalue()

    def test_a_dry_run_writes_nothing_and_check_reports_drift(self):
        sub = self.sub.read_bytes()
        self.assertEqual(self.run_main()[0], 0)
        self.assertFalse(self.spec.exists() or self.out.exists())
        self.assertEqual(self.sub.read_bytes(), sub)
        self.assertEqual(self.run_main("--check")[0], 1)

    def test_apply_then_check_is_clean_and_a_second_apply_writes_nothing(self):
        code, text = self.run_main("--apply")
        self.assertEqual(code, 0)
        self.assertIn("spec.json", text)
        self.assertEqual(self.run_main("--check")[0], 0)
        code, text = self.run_main("--apply")
        self.assertEqual(code, 0)
        self.assertIn("written: nothing", text)

    def test_a_crlf_checkout_of_the_spec_is_not_drift(self):
        self.run_main("--apply")
        self.spec.write_bytes(self.spec.read_bytes().replace(b"\n", b"\r\n"))
        self.assertEqual(self.run_main("--check")[0], 0)

    def test_no_armory_is_two_and_a_crash_is_three_and_a_bad_spec_is_one(self):
        with mock.patch.object(ra, "ARMORY_DIR", str(Path(self._tmp.name) / "none")):
            self.assertEqual(self.run_main("--check")[0], 2)
        with mock.patch.object(gbc, "build", side_effect=RuntimeError("boom")):
            self.assertEqual(self.run_main("--check")[0], 3)
        with mock.patch.object(gbc, "build", side_effect=gbc.NoArmory("no slot file")):
            self.assertEqual(self.run_main("--check")[0], 2)
        with mock.patch.object(gbc, "build", side_effect=gbc.CloneError("bad")):
            self.assertEqual(self.run_main("--apply")[0], 1)
        self.assertFalse(self.spec.exists())

    def test_owed_swaps_and_the_key_count_go_under_next(self):
        self.plan["swaps"] = [{"file": "x/troops_arthedain.xml", "troop": "t", "slot": "Body", "old": "p", "new": "c"}]
        self.plan["keys"] = 5
        code, text = self.run_main("--apply")
        self.assertEqual(code, 0)
        written = next(line for line in text.splitlines() if line.startswith("written:"))
        self.assertNotIn("troops_arthedain", written)
        self.assertIn("NEXT", text)
        self.assertIn("generate_arthedain_troops.py --apply", text)
        self.assertIn("loc_inline_backlog.json", text)


if __name__ == "__main__":
    unittest.main()
