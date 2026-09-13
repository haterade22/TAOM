#!/usr/bin/env python3
"""Unit tests for tools/analyze_kingdom_armour.py, the cross-kingdom armour overview (#581).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_analyze_kingdom_armour.py

Synthetic data only, no game install needed: a two-culture ModuleData and a tiny Armory tree
live in a temporary directory.

THE CONTRACT
------------
Read-only. A troop's armour is the four engine regions (head, body, arm, leg) summed over the
five armour slots exactly as Equipment.Get*ArmorSum does, averaged over its battle sets with an
unfilled slot counting 0, civilian sets excluded. Troops are placed by engine tier
(clamp(ceil((level - 5) / 5), 0, 10)). The tool reports culture x tier matrices, cross-culture
inversions, per-culture Armory ceilings, the curve's prediction, and the validator gate's own
verdict; it never writes XML.
"""
import hashlib
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import analyze_kingdom_armour as ka  # noqa: E402
import analyze_troop_balance as atb  # noqa: E402
import fix_upgrade_armour_regressions as fx  # noqa: E402
import rebalance_armor as ra  # noqa: E402
import rebalance_troops as rb  # noqa: E402
import taom_schema as ts  # noqa: E402


def _npc(tid, level, upgrades=(), sets=(), name=None, group="Infantry"):
    ups = "".join(f'<upgrade_target id="NPCCharacter.{u}" />' for u in upgrades)
    rosters = ""
    for st in sets:
        civ = ' civilian="true"' if st.get("_civilian") else ""
        eqs = "".join(f'<equipment slot="{s}" id="Item.{i}" />'
                      for s, i in st.items() if s != "_civilian")
        rosters += f"<EquipmentRoster{civ}>{eqs}</EquipmentRoster>"
    name = name or tid
    lvl = "" if level is None else f' level="{level}"'
    return (f'<NPCCharacter id="{tid}"{lvl} name="{{=k}}{name}" default_group="{group}">'
            f"<upgrade_targets>{ups}</upgrade_targets>"
            f"<Equipments>{rosters}</Equipments>"
            f"</NPCCharacter>")


def _item(iid, itype, name=None, **stats):
    attrs = " ".join(f'{k}="{v}"' for k, v in stats.items())
    return (f'<Item id="{iid}" name="{{=k}}{name or iid}" Type="{itype}">'
            f"<ItemComponent><Armor {attrs} material_type=\"Plate\" /></ItemComponent></Item>")


HEAVY_SET = {"Head": "a_helm_heavy", "Body": "a_chest_heavy", "Cape": "a_cape",
             "Gloves": "a_glove", "Leg": "a_boot"}


class KingdomArmourTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.md = root / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        (self.md / "characters").mkdir(parents=True)
        (self.md / "taom_spcultures.xml").write_text(
            '<SPCultures><Culture id="c" militia_troop="NPCCharacter.alpha_militia" '
            'melee_militia_troop="NPCCharacter.alpha_militia" /></SPCultures>', encoding="utf-8")
        (self.md / "spcultures.xslt").write_text(
            '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0" />',
            encoding="utf-8")
        rb._militia_ids_cache.clear()

        self.modules = root / "Modules"
        alpha = self.modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "alpha"
        beta = self.modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "beta"
        vanilla = self.modules / "SandBoxCore" / "ModuleData" / "items"
        for d in (alpha, beta, vanilla):
            d.mkdir(parents=True)
        (alpha / "head_armors.xml").write_text("<Items>" + "".join([
            _item("a_helm_heavy", "HeadArmor", head_armor=32),
            _item("a_helm_elite", "HeadArmor", head_armor=40),
            _item("a_helm_hero", "HeadArmor", name="Faramir's Helm", head_armor=60),
        ]) + "</Items>", encoding="utf-8")
        (alpha / "body_armors.xml").write_text("<Items>" + "".join([
            _item("a_chest_heavy", "BodyArmor", body_armor=42, leg_armor=22),
            _item("a_chest_elite", "BodyArmor", body_armor=50, leg_armor=28),
        ]) + "</Items>", encoding="utf-8")
        (alpha / "arm_armors.xml").write_text(
            "<Items>" + _item("a_glove", "HandArmor", arm_armor=20) + "</Items>", encoding="utf-8")
        (alpha / "leg_armors.xml").write_text(
            "<Items>" + _item("a_boot", "LegArmor", leg_armor=28) + "</Items>", encoding="utf-8")
        (alpha / "shoulder_armors.xml").write_text(
            "<Items>" + _item("a_cape", "Cape", body_armor=13, arm_armor=11) + "</Items>",
            encoding="utf-8")
        (beta / "head_armors.xml").write_text(
            "<Items>" + _item("b_helm_lord", "HeadArmor", head_armor=45) + "</Items>",
            encoding="utf-8")
        (beta / "body_armors.xml").write_text(
            "<Items>" + _item("b_chest", "BodyArmor", body_armor=50, leg_armor=28) + "</Items>",
            encoding="utf-8")
        (vanilla / "vanilla.xml").write_text(
            "<Items>" + _item("v_boot", "LegArmor", leg_armor=5) + "</Items>", encoding="utf-8")

        self.bodyless_id = sorted(fx.BODYLESS_BY_DESIGN)[0]
        # A real exempt id that is neither a creature nor a bespoke rider (the Ithilien ranger),
        # dressed in a light kit at tier 10 as the real one is.
        self.exempt_id = sorted(i for i in ts.Validator._ARMOUR_LADDER_EXEMPT
                                if not atb.is_creature_troop(i, i) and i not in ka.BESPOKE_RIDERS)[0]
        self._write("alpha", [
            _npc("alpha_t5", 26, ["alpha_t6"], [HEAVY_SET]),
            _npc("alpha_t6", 31, ["alpha_t9"], [HEAVY_SET]),
            _npc("alpha_t9", 46, [], [HEAVY_SET, {k: v for k, v in HEAVY_SET.items() if k != "Head"},
                                      {"_civilian": True, "Head": "a_helm_hero"}]),
            _npc("alpha_militia", 21, [], [HEAVY_SET]),
            _npc("alpha_troll", 51, [], [HEAVY_SET], name="Cave Troll"),
            _npc("alpha_ranger", 51, [], []),
            _npc("alpha_unlevelled", None, [], [HEAVY_SET]),
            _npc(self.exempt_id, 51, [], [{"Gloves": "a_glove"}]),
        ])
        self._write("beta", [
            _npc("beta_t5", 26, ["beta_t6"], [{"Head": "b_helm_lord", "Body": "b_chest",
                                               "Gloves": "a_glove", "Leg": "a_boot"}]),
            _npc("beta_t6", 31, ["beta_t9"], [HEAVY_SET]),
            _npc("beta_t9", 46, [], [HEAVY_SET]),
            _npc(self.bodyless_id, 26, [], [{"Head": "b_helm_lord", "Gloves": "a_glove",
                                             "Leg": "a_boot"}]),
        ])
        (self.md / "characters" / "npcs_alpha.xml").write_text(
            "<NPCCharacters>" + _npc("villager_alpha", 1, ["alpha_t5"], [{"Leg": "v_boot"}])
            + "</NPCCharacters>", encoding="utf-8")

        self.items = fx.load_item_armour(str(self.modules), str(self.md))
        self.troops = fx.load_troops(str(self.md))
        self.militia = rb.militia_troop_ids(str(self.md))

    def tearDown(self):
        rb._militia_ids_cache.clear()
        self._tmp.cleanup()

    def _write(self, culture, npcs):
        (self.md / "troops" / f"troops_{culture}.xml").write_text(
            "<NPCCharacters>" + "".join(npcs) + "</NPCCharacters>", encoding="utf-8")

    def _records(self):
        return ka.build_records(self.troops, self.items, self.militia)

    def _by_id(self):
        return {r["id"]: r for r in self._records()}

    # -- model ---------------------------------------------------------------------------- #

    def test_mumakil_rider_is_a_mount_rider(self):
        self.assertTrue(atb.is_mount_rider("harad_mumakil_rider", "Mumakil Rider"))
        self.assertTrue(atb.is_mount_rider("harad_elephant_rider", "Elephant Rider"))

    def test_bespoke_riders_are_the_ones_the_skill_tool_skips(self):
        self.assertTrue(ka.BESPOKE_RIDERS <= set(rb.SKIP_TROOP_IDS))

    def test_engine_tier_matches_the_validator(self):
        self.assertEqual([ka.engine_tier(lv) for lv in (1, 5, 6, 11, 16, 21, 26, 31, 46, 51)],
                         [0, 0, 1, 2, 3, 4, 5, 6, 9, 10])
        self.assertEqual(ka.engine_tier(51), ts.Validator._troop_tier(51))

    def test_region_avg_sums_slots_into_engine_regions_and_averages_unfilled_as_zero(self):
        t = self.troops["alpha_t9"]
        avg = ka.region_avg(t, self.items)
        # Set 1: head 32, body 42+13, arm 20+11, leg 22+28; set 2 has no Head.
        self.assertEqual(avg, {"head_armor": 16.0, "body_armor": 55.0, "arm_armor": 31.0,
                               "leg_armor": 50.0})
        self.assertAlmostEqual(ka.troop_total(t, self.items), fx.total(t, self.items))

    def test_civilian_sets_are_ignored(self):
        self.assertEqual(len(self.troops["alpha_t9"]["sets"]), 2)
        self.assertEqual(ka.region_avg(self.troops["alpha_t9"], self.items)["head_armor"], 16.0)

    def test_classification_excludes_creatures_riders_and_setless_but_only_tags_militia(self):
        recs = self._by_id()
        self.assertNotIn("villager_alpha", recs)
        self.assertTrue(recs["alpha_troll"]["excluded"])
        self.assertIn("creature", recs["alpha_troll"]["tags"])
        self.assertTrue(recs["alpha_ranger"]["excluded"])
        self.assertIn("no_sets", recs["alpha_ranger"]["tags"])
        self.assertFalse(recs["alpha_militia"]["excluded"])
        self.assertIn("militia", recs["alpha_militia"]["tags"])
        self.assertIn("standalone", recs["alpha_militia"]["tags"])
        self.assertNotIn("standalone", recs["alpha_t5"]["tags"])
        self.assertIn("bodyless", recs[self.bodyless_id]["tags"])
        self.assertFalse(recs[self.bodyless_id]["excluded"])
        rider = dict(self.troops["alpha_t5"], id=sorted(ka.BESPOKE_RIDERS)[0])
        tags, excluded = ka.classify(rider, set(), {}, {})
        self.assertTrue(excluded)
        self.assertIn("bespoke_rider", tags)
        # No level= is what the validator's index skips (level None), so the analyzer skips it too
        # rather than filing the troop at tier 0.
        self.assertTrue(recs["alpha_unlevelled"]["excluded"])
        self.assertIn("no_level", recs["alpha_unlevelled"]["tags"])
        self.assertIn("ladder_exempt", recs[self.exempt_id]["tags"])
        self.assertFalse(recs[self.exempt_id]["excluded"])

    def test_ladder_exempt_troops_leave_the_matrices_and_the_pair_list_but_not_the_records(self):
        recs = self._records()
        self.assertNotIn(10, ka.matrices(recs)["total"]["alpha"])
        pairs = ka.cross_culture_inversions(recs, self.items, self.troops, threshold=0, min_gap=2)
        self.assertFalse(any(self.exempt_id in (p["strong"], p["weak"]) for p in pairs))
        self.assertNotIn(("alpha", 10), ka.gate_cells(recs))
        self.assertIn(self.exempt_id, [r["id"] for r in recs])

    def test_shipped_creature_and_rider_exclusions_are_all_in_the_validators_exempt_list(self):
        """The analyzer excludes creatures and bespoke riders by name; the validator only knows
        _ARMOUR_LADDER_EXEMPT. The two gate cell sets agree only while every such troop is listed
        there, so pin it on the shipped data."""
        import re
        troops = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "troops"
        if not troops.is_dir():
            self.skipTest("troop data not present")
        found = {}
        for f in troops.glob("troops_*.xml"):
            text = f.read_text(encoding="utf-8-sig", errors="ignore")
            for m in re.finditer(r'<NPCCharacter\b([^>]*)>', text):
                attrs = m.group(1)
                idm = re.search(r'\sid="([^"]+)"', attrs)
                nm = re.search(r'\sname="([^"]*)"', attrs)
                if idm:
                    found[idm.group(1)] = rb.get_display_name(nm.group(1) if nm else "")
        self.assertGreater(len(found), 100, "the scan is broken, not the allowlist")
        by_marker = sorted(t for t, n in found.items()
                           if atb.is_creature_troop(t, n) or t in ka.BESPOKE_RIDERS)
        self.assertTrue(by_marker, "the markers match nothing; the scan or the markers are broken")
        missing = [t for t in by_marker if t not in ts.Validator._ARMOUR_LADDER_EXEMPT]
        self.assertEqual(missing, [], f"excluded by the analyzer but judged by the validator: {missing}")

    def test_curve_culture_aliases_resolve_to_cultural_mods_keys(self):
        for fc, expected in (("dolguldur", "dol_guldur"), ("rhun_new", "rhun"),
                             ("lindon", "rivendell"), ("goblin", "mordor"), ("gondor", "gondor")):
            self.assertEqual(ka.curve_culture({"id": "x", "file": f"troops_{fc}.xml"}), expected)
            self.assertIn(expected, ra.CULTURAL_MODS)
        self.assertEqual(ka.curve_culture({"id": "iron_hills_axe", "file": "troops_erebor.xml"}),
                         "iron_hills")

    def test_predicted_regions_follow_level_band_and_cultural_mod_and_are_flat_above_31(self):
        gondor = {"id": "g", "file": "troops_gondor.xml", "level": 46}
        pred = ka.predicted_regions(gondor)
        elite = {slot: ra.calculate_stats("elite", slot, "gondor")
                 for slot in ("head", "body", "arm", "leg", "shoulder")}
        self.assertEqual(pred["head_armor"], elite["head"]["head_armor"])
        self.assertEqual(pred["body_armor"], elite["body"]["body_armor"] + elite["shoulder"]["body_armor"])
        self.assertEqual(pred["arm_armor"], elite["arm"]["arm_armor"] + elite["shoulder"].get("arm_armor", 0)
                         + elite["body"].get("arm_armor", 0))
        self.assertEqual(pred["leg_armor"], elite["leg"]["leg_armor"] + elite["body"]["leg_armor"])
        self.assertEqual(ka.predicted_regions(dict(gondor, level=31)), ka.predicted_regions(dict(gondor, level=51)))
        medium = ka.predicted_regions(dict(gondor, level=16))
        self.assertEqual(medium["head_armor"], ra.calculate_stats("medium", "head", "gondor")["head_armor"])
        self.assertLess(medium["head_armor"], pred["head_armor"])

    # -- analyses ------------------------------------------------------------------------- #

    def test_matrix_cells_report_median_min_max_n_per_region(self):
        m = ka.matrices(self._records())
        self.assertEqual(m["total"]["alpha"][9], {"median": 152.0, "min": 152.0, "max": 152.0, "n": 1})
        self.assertEqual(m["head_armor"]["beta"][5]["max"], 45.0)
        # The bodyless troop is in beta tier 5 too, but its total is not comparable: n stays 1.
        self.assertEqual(m["total"]["beta"][5]["n"], 1)
        self.assertNotIn("villager", "".join(m["total"]))

    def test_pairwise_inversions_respect_threshold_and_gap(self):
        recs = self._records()
        # beta_t5: 45 + 50/28 + 20 + 28 = 171; alpha_t9: 152 (one set lacks its helmet).
        pairs = ka.cross_culture_inversions(recs, self.items, self.troops, threshold=0, min_gap=2)
        found = [(p["strong"], p["weak"], p["diff"]) for p in pairs]
        self.assertIn(("beta_t5", "alpha_t9", 19.0), found)
        # At 20 only the bare-chested pair (+29 on Head/Gloves/Leg) survives; at 30 nothing does.
        at_20 = ka.cross_culture_inversions(recs, self.items, self.troops, threshold=20, min_gap=2)
        self.assertEqual([p["strong"] for p in at_20], [self.bodyless_id])
        self.assertEqual(ka.cross_culture_inversions(recs, self.items, self.troops, threshold=30, min_gap=2), [])
        self.assertEqual(ka.cross_culture_inversions(recs, self.items, self.troops, threshold=0, min_gap=5), [])
        # Same-culture pairs are never listed, and militia are ordinary troops here.
        self.assertFalse(any(p["strong_culture"] == p["weak_culture"] for p in pairs))

    def test_bodyless_pairs_are_compared_without_body_and_cape(self):
        recs = self._records()
        pairs = ka.cross_culture_inversions(recs, self.items, self.troops, threshold=0, min_gap=2)
        hit = [p for p in pairs if p["strong"] == self.bodyless_id and p["weak"] == "alpha_t9"]
        self.assertEqual(len(hit), 1)
        # Head 45 + Gloves 20 + Leg 28 = 93 vs alpha_t9 on the same slots: head 16 + 20 + 28 = 64.
        self.assertEqual((hit[0]["strong_total"], hit[0]["weak_total"]), (93.0, 64.0))

    def test_inversions_aggregate_per_culture_pair_with_count_and_worst(self):
        recs = self._records()
        pairs = ka.cross_culture_inversions(recs, self.items, self.troops, threshold=0, min_gap=2)
        agg = ka.aggregate_inversions(pairs)
        self.assertIn(("beta", "alpha"), agg)
        self.assertEqual(agg[("beta", "alpha")]["count"], len(pairs))
        self.assertEqual(agg[("beta", "alpha")]["worst"]["diff"], max(p["diff"] for p in pairs))

    def test_ceilings_report_available_vs_worn_and_list_unworn_elite_items(self):
        recs = self._records()
        worn_by = ka.worn_by_index(self.troops)
        c = ka.ceilings("alpha", recs, self.troops, self.items, worn_by)
        self.assertEqual(c["folders"], {"alpha": 1})  # observed, never hardcoded
        head = c["slots"]["Head"]
        self.assertEqual((head["avail_max"], head["avail_max_incl_hero"], head["worn_max"]), (40, 60, 32))
        self.assertEqual(head["avail_max_item"], "a_helm_elite")
        unworn = [(u["id"], u["primary"]) for u in c["unworn_elite"]]
        self.assertIn(("a_helm_elite", 40), unworn)
        self.assertIn(("a_chest_elite", 50), unworn)
        self.assertNotIn("a_helm_hero", [u["id"] for u in c["unworn_elite"]])

    def test_an_unworn_items_tier_is_judged_on_its_own_folders_curve(self):
        """beta wears alpha's gloves and boots, so alpha's folder is in beta's ceiling. Under a
        beta curve 30 points harder the 40 helmet would read as light and vanish from the reserve;
        the item was statted on alpha's curve, so that is the curve it is judged on."""
        recs = self._records()
        worn_by = ka.worn_by_index(self.troops)
        ra.CULTURAL_MODS["beta"] = {"protection": 30, "weight_mult": 1.0}
        try:
            c = ka.ceilings("beta", recs, self.troops, self.items, worn_by)
        finally:
            del ra.CULTURAL_MODS["beta"]
        self.assertEqual(set(c["folders"]), {"alpha", "beta"})
        self.assertIn(("a_helm_elite", "elite"), [(u["id"], u["tier"]) for u in c["unworn_elite"]])

    def test_gate_preview_uses_the_validator_function_and_constants(self):
        recs = self._records()
        self.assertEqual(ka.gate_preview(recs), [])
        cells = ka.gate_cells(recs)
        self.assertEqual(sorted(cells), [("alpha", 4), ("alpha", 5), ("alpha", 6), ("alpha", 9),
                                         ("beta", 5), ("beta", 6), ("beta", 9)])
        # The bodyless troop shares beta tier 5 but is left out, as the validator leaves it out.
        self.assertEqual(cells[("beta", 5)], [171.0])
        old = (ts.Validator._CROSS_CULTURE_ARMOUR_MIN_CULTURES,
               ts.Validator._CROSS_CULTURE_ARMOUR_TIER_GAP, ts.Validator._CROSS_CULTURE_ARMOUR_MARGIN)
        try:
            # One neighbour is enough, judged three tiers down, and a 10-point margin: alpha
            # tier 9 (152) now sits under beta tier 6 (168) by more than the margin.
            ts.Validator._CROSS_CULTURE_ARMOUR_MIN_CULTURES = 1
            ts.Validator._CROSS_CULTURE_ARMOUR_TIER_GAP = 3
            ts.Validator._CROSS_CULTURE_ARMOUR_MARGIN = 10
            hits = ka.gate_preview(recs)
            self.assertEqual([(h["culture"], h["tier"], h["shortfall"]) for h in hits],
                             [("alpha", 9, 16.0)])
        finally:
            (ts.Validator._CROSS_CULTURE_ARMOUR_MIN_CULTURES, ts.Validator._CROSS_CULTURE_ARMOUR_TIER_GAP,
             ts.Validator._CROSS_CULTURE_ARMOUR_MARGIN) = old

    # -- CLI ------------------------------------------------------------------------------ #

    def _hashes(self):
        out = {}
        for p in sorted(Path(self._tmp.name).rglob("*.xml")):
            out[str(p)] = hashlib.sha1(p.read_bytes()).hexdigest()
        return out

    def test_main_writes_the_three_artifacts_and_touches_no_game_data(self):
        before = self._hashes()
        out = Path(self._tmp.name) / "reports"
        rc = ka.main(["--moduledata", str(self.md), "--game-modules", str(self.modules),
                      "--report-dir", str(out)])
        self.assertEqual(rc, 0)
        self.assertEqual(self._hashes(), before)
        for name in (ka.REPORT_MD, ka.REPORT_HTML, ka.REPORT_JSON):
            self.assertTrue((out / name).is_file(), name)
        data = json.loads((out / ka.REPORT_JSON).read_text(encoding="utf-8"))
        self.assertEqual(len(data["troops"]), len(self._records()))
        md = (out / ka.REPORT_MD).read_text(encoding="utf-8")
        self.assertIn("| Culture |", md)
        self.assertIn("### alpha", md)
        self.assertIn("### beta", md)

    def test_main_culture_filter_and_missing_install(self):
        out = Path(self._tmp.name) / "reports"
        self.assertEqual(ka.main(["--moduledata", str(self.md), "--game-modules", str(self.modules),
                                  "--report-dir", str(out), "--culture", "nope"]), 2)
        self.assertEqual(ka.main(["--moduledata", str(self.md),
                                  "--game-modules", str(Path(self._tmp.name) / "nowhere"),
                                  "--report-dir", str(out)]), 2)
        self.assertEqual(ka.main(["--moduledata", str(self.md), "--game-modules", str(self.modules),
                                  "--report-dir", str(out), "--culture", "beta"]), 0)
        md = (out / ka.REPORT_MD).read_text(encoding="utf-8")
        self.assertIn("### beta", md)
        self.assertNotIn("### alpha", md)


if __name__ == "__main__":
    unittest.main()
