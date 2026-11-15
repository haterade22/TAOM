#!/usr/bin/env python3
"""The Snow Orc armour generator prices a worn piece at its lowest wearer's band (#1271).

A restat prices every other Armory piece at the band of its lowest wearer, so the generator must too, or a generator
run undoes the restat and leaves the wearers under the worn armour floor. The appearance stays with the id word.
Needs no game install.
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_snow_orc_armor as gen  # noqa: E402
import rebalance_armor as ra  # noqa: E402

CAPE = "sk_gb_orc_inf_pauld_cape_med_a"


class BandTests(unittest.TestCase):
    def test_a_worn_medium_word_piece_gets_the_heavy_value_at_level_21(self):
        records = {CAPE: {"anchorLevel": 21}}
        self.assertEqual(gen.band_of(CAPE, records), "heavy")
        worn = gen.stats_for(CAPE, "shoulder", records)["body_armor"]
        unworn = gen.stats_for(CAPE, "shoulder", {})["body_armor"]
        self.assertGreater(worn, unworn)

    def test_an_unworn_piece_keeps_the_band_its_id_word_names(self):
        self.assertEqual(gen.band_of(CAPE, {}), "medium")
        self.assertEqual(gen.band_of(CAPE, {CAPE: {"anchorLevel": None}}), "medium")

    def test_the_tunic_has_no_tier_word_and_prices_as_light_when_unworn(self):
        self.assertEqual(gen.band_of("sk_gb_orc_arc_tunic_a", {}), "light")

    def test_the_appearance_stays_with_the_id_word_when_the_price_moves(self):
        by_slot, rows = gen.build({CAPE: {"anchorLevel": 21}})
        root = ET.fromstring(gen.render(by_slot["shoulder"]))
        node = [i for i in root.iter("Item") if i.get("id") == CAPE][0]
        self.assertEqual(node.get("appearance"), str(gen.APPEARANCE["medium"]))
        self.assertEqual([r for r in rows if r[0] == CAPE][0][2], "heavy")


class ShippedRosterTests(unittest.TestCase):
    def test_no_worn_piece_is_priced_below_the_band_of_its_lowest_wearer(self):
        records, _ = ra.load_roster_records()
        order = ra.MESH_TIER_ORDER
        worn = 0
        for item_id, _slot, _name in gen.PIECES:
            level = (records.get(item_id) or {}).get("anchorLevel")
            if level is None:
                continue
            worn += 1
            floor = ra.level_to_band(int(level))
            self.assertGreaterEqual(order.index(gen.band_of(item_id, records)), order.index(floor), item_id)
        self.assertGreater(worn, 0)


if __name__ == "__main__":
    unittest.main()
