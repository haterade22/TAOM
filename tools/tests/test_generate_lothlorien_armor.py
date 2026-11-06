#!/usr/bin/env python3
"""The Lorien armour generator writes covers_legs="true" on every leg piece (Mike, 2026-11-06).

It wrote "false" at first, which left the leg skin drawn under every greave. The rendered leg file is parsed and
compared with the eight pieces by name, so the set can neither shrink nor lose the attribute unnoticed.
Needs no game install.
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_lothlorien_armor as gen  # noqa: E402

EXPECTED_LEGS = {
    "sk_ll_rg_boots_light_a", "sk_ll_rg_grvs_med_a",
    "sk_ll_mw_grvs_med_a", "sk_ll_mw_grvs_heavy_a",
    "sk_ll_gl_grvs_med_a", "sk_ll_gl_grvs_heavy_a",
    "sk_ll_ml_grvs_med_a", "sk_ll_ml_grvs_heavy_a",
}


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


if __name__ == "__main__":
    unittest.main()
