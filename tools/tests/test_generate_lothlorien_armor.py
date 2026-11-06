#!/usr/bin/env python3
"""The Lorien armour generator writes covers_legs="true" on every leg piece (Mike, 2026-11-06).

It wrote "false" at first, which left the body's legs showing through every greave. Needs no game install.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_lothlorien_armor as gen  # noqa: E402


class LegPiecesCoverLegsTests(unittest.TestCase):
    def test_every_leg_piece_covers_legs(self):
        legs = [p for p in gen.PIECES if p[1] == "leg"]
        self.assertGreaterEqual(len(legs), 6, "expected the Lorien boots and greaves")
        for item_id, slot, name in legs:
            st = gen.stats_for(item_id, slot, gen.priced_band(item_id))
            xml = gen.item_xml(item_id, slot, name, st, appearance=3)
            self.assertIn('covers_legs="true"', xml, item_id)
            self.assertNotIn('covers_legs="false"', xml, item_id)

    def test_only_leg_pieces_carry_covers_legs(self):
        for item_id, slot, name in gen.PIECES:
            if slot == "leg":
                continue
            st = gen.stats_for(item_id, slot, gen.priced_band(item_id))
            self.assertNotIn("covers_legs", gen.item_xml(item_id, slot, name, st, appearance=3), item_id)


if __name__ == "__main__":
    unittest.main()
