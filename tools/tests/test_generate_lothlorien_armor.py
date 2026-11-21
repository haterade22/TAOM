#!/usr/bin/env python3
"""The Lorien armour generator: leg pieces cover legs, and a worn piece is priced at its wearers' band.

Leg pieces: it wrote covers_legs="false" at first (Mike, 2026-11-06), which left the leg skin drawn under every
greave. The rendered leg file is parsed and compared with the eight pieces by name, so the set can neither shrink
nor lose the attribute unnoticed.

Band: a restat prices every other Armory piece at the band of its lowest wearer, so the generator must too, or a
generator run undoes the restat and leaves the wearers under the worn armour floor (#1271).
Needs no game install.
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_lothlorien_armor as gen  # noqa: E402
import rebalance_armor as ra  # noqa: E402

EXPECTED_LEGS = {
    "sk_ll_rg_boots_light_a", "sk_ll_rg_grvs_med_a",
    "sk_ll_mw_grvs_med_a", "sk_ll_mw_grvs_heavy_a",
    "sk_ll_gl_grvs_med_a", "sk_ll_gl_grvs_heavy_a",
    "sk_ll_ml_grvs_med_a", "sk_ll_ml_grvs_heavy_a",
}


class HeroPiecesAreNamedKitTests(unittest.TestCase):
    def test_every_hero_piece_is_excluded_so_the_class_table_calls_it_named(self):
        for item_id, _slot, name in gen.GALADRIEL:
            with self.subTest(item=item_id):
                self.assertTrue(ra.is_excluded(item_id, f"{gen.TAG} {name}"))


class LegPiecesCoverLegsTests(unittest.TestCase):
    def test_the_rendered_leg_file_covers_legs_on_exactly_the_eight_leg_pieces(self):
        by_file, _ = gen.build()
        root = ET.fromstring(gen.render("leg", by_file["leg"]))
        items = {item.get("id"): item for item in root.iter("Item")}
        self.assertEqual(set(items), EXPECTED_LEGS)
        for item_id, item in items.items():
            self.assertEqual(item.get("Type"), "LegArmor", item_id)
            self.assertEqual(item.find(".//Armor").get("covers_legs"), "true", item_id)

    def test_every_leg_piece_in_the_manifest_is_one_of_the_eight(self):
        self.assertEqual({p[0] for p in gen.PIECES if p[1] == "leg"}, EXPECTED_LEGS)

    def test_only_leg_pieces_carry_covers_legs(self):
        by_file, _ = gen.build()
        for slot in ("head", "body", "arm", "shoulder"):
            root = ET.fromstring(gen.render(slot, by_file[slot]))
            for armor in root.iter("Armor"):
                self.assertIsNone(armor.get("covers_legs"), slot)


def _head_primary(item_id, band):
    return gen.stats_for(item_id, "head", band)["head_armor"]


class PricedBandTests(unittest.TestCase):
    def test_a_worn_medium_word_piece_gets_the_elite_value_at_level_31(self):
        item = "sk_ll_rg_helmet_med_b"
        records = {item: {"anchorLevel": 31}}
        self.assertEqual(gen.priced_band(item, records), "elite")
        self.assertGreater(_head_primary(item, "elite"), _head_primary(item, "medium"))

    def test_an_unworn_piece_keeps_the_band_its_id_word_names(self):
        item = "sk_ll_rg_helmet_med_b"
        self.assertEqual(gen.priced_band(item, {}), "medium")
        self.assertEqual(gen.priced_band(item, {item: {"anchorLevel": None}}), "medium")

    def test_a_worn_light_word_piece_follows_a_higher_wearer(self):
        self.assertEqual(gen.priced_band("sk_ll_rg_chest_light_a", {"sk_ll_rg_chest_light_a": {"anchorLevel": 20}}),
                         "heavy")

    def test_a_worn_mallorn_piece_is_one_band_above_its_wearers(self):
        item = "sk_ll_ml_helmet_med_a"
        for level, band in ((10, "medium"), (16, "heavy"), (25, "elite"), (40, "lord")):
            self.assertEqual(gen.priced_band(item, {item: {"anchorLevel": level}}), band, level)

    def test_an_unworn_mallorn_piece_is_one_band_above_its_id_word(self):
        self.assertEqual(gen.priced_band("sk_ll_ml_helmet_med_a", {}), "heavy")
        self.assertEqual(gen.priced_band("sk_ll_ml_helmet_elite_a", {}), "lord")

    def test_the_appearance_stays_with_the_id_word_when_the_price_moves(self):
        item = "sk_ll_rg_helmet_med_b"
        by_file, rows = gen.build({item: {"anchorLevel": 31}})
        head = ET.fromstring(gen.render("head", by_file["head"]))
        node = [i for i in head.iter("Item") if i.get("id") == item][0]
        self.assertEqual(node.get("appearance"), str(gen.APPEARANCE["medium"]))
        row = [r for r in rows if r[0] == item][0]
        self.assertEqual(row[2], "elite")


class ShippedRosterTests(unittest.TestCase):
    def test_no_worn_piece_is_priced_below_the_band_of_its_lowest_wearer(self):
        records, _ = ra.load_roster_records()
        order = ra.MESH_TIER_ORDER
        worn = 0
        for item_id, _slot, _stem in gen.PIECES:
            level = (records.get(item_id) or {}).get("anchorLevel")
            if level is None:
                continue
            band = ra.level_to_band(int(level))
            worn += 1
            self.assertGreaterEqual(order.index(gen.priced_band(item_id, records)), order.index(band), item_id)
        self.assertGreater(worn, 0)


if __name__ == "__main__":
    unittest.main()
