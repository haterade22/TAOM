#!/usr/bin/env python3
"""Unit tests for the armour mesh-tier ladder in tools/rebalance_armor.py (#609).

Run:  python -m unittest tools.tests.test_mesh_tier_ladder
  or:  python tools/tests/test_mesh_tier_ladder.py

THE CONTRACT
------------
An armour item's id carries the artist's mesh tier (_light_, _med_, _heavy_, _elite_, _lord_).
A troop may wear only the tiers its level allows (the maintainer's table, 2026-09-16): lord kit
is for level 41+ troops or lords, and a recruit in a lord chest anchors the whole lord line to
the light band (the Gundabad uruk lord chest at 20 body armour). Over-dressed is the tier above
the highest allowed; under-dressed is below the lowest. The substitute tier for a swap is the
highest allowed tier not above the troop's stat band, so the fix cannot recreate the anchor
bug one notch down.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import derive_armor_tiers as dat  # noqa: E402
import rebalance_armor as ra  # noqa: E402


class LadderTable(unittest.TestCase):
    def test_allowed_tiers_at_every_troop_level(self):
        expected = {
            1: {'light'}, 6: {'light'},
            11: {'light', 'medium'}, 16: {'light', 'medium'},
            21: {'medium'},
            26: {'heavy'},
            31: {'heavy', 'elite'},
            36: {'elite'},
            41: {'elite', 'lord'}, 46: {'elite', 'lord'}, 51: {'elite', 'lord'},
        }
        for level, tiers in expected.items():
            self.assertEqual(ra.allowed_mesh_tiers(level), frozenset(tiers), level)

    def test_boundaries_are_inclusive_upper_bounds(self):
        # TAOM levels are 6, 11, 16 ...; a level between two rows takes the row above it.
        self.assertEqual(ra.allowed_mesh_tiers(7), frozenset({'light', 'medium'}))
        self.assertEqual(ra.allowed_mesh_tiers(17), frozenset({'medium'}))
        self.assertEqual(ra.allowed_mesh_tiers(37), frozenset({'elite', 'lord'}))

    def test_table_rows_are_ascending_and_use_known_tiers(self):
        bounds = [b for b, _ in ra.MESH_TIER_LADDER]
        self.assertEqual(bounds, sorted(bounds))
        for _, tiers in ra.MESH_TIER_LADDER:
            self.assertTrue(set(tiers) <= set(ra.MESH_TIER_ORDER), tiers)


class MeshTierOfItem(unittest.TestCase):
    def test_reads_the_tier_token(self):
        self.assertEqual(ra.mesh_tier_of('sk_gb_uruk_chest_lord_c'), 'lord')
        self.assertEqual(ra.mesh_tier_of('sk_gb_uruk_chest_elite_a'), 'elite')
        self.assertEqual(ra.mesh_tier_of('dunland_wulf_scalemail_heavy_e'), 'heavy')
        self.assertEqual(ra.mesh_tier_of('sk_gb_uruk_chest_med_d'), 'medium')
        self.assertEqual(ra.mesh_tier_of('sk_x_helmet_medium_b'), 'medium')
        self.assertEqual(ra.mesh_tier_of('sk_gb_uruk_chest_light_a'), 'light')

    def test_civilian_and_untokened_kit_are_off_the_ladder(self):
        self.assertEqual(ra.mesh_tier_of('sk_gd_civ_heavy_coat_a'), 'civilian')
        self.assertEqual(ra.mesh_tier_of('sk_dwarf_civilian_dress_a'), 'civilian')
        self.assertIsNone(ra.mesh_tier_of('sk_dale_helmet_archer_a03'))
        self.assertIsNone(ra.mesh_tier_of(''))
        self.assertIsNone(ra.mesh_tier_of(None))

    def test_derivation_reads_the_same_function(self):
        self.assertIs(dat.id_keyword_tier, ra.mesh_tier_of)


class SubstituteTier(unittest.TestCase):
    def test_allowed_tiers_at_or_below_the_stat_band_first_then_the_rest(self):
        # L11 is light/medium but its stat band is light: a snaga in _med_a would drag the
        # medium line to the light band, so light comes first. Medium stays on the list,
        # last: a line with no light variant (the Mordor orc infantry chest) still has a
        # ladder-legal swap, and a medium mesh dragged to the light band beats a heavy one
        # left there (the 2026-09-16 first pass left the goblin snaga in `_heavy_e`).
        self.assertEqual(ra.substitute_mesh_tiers(11), ('light', 'medium'))
        self.assertEqual(ra.substitute_mesh_tiers(6), ('light',))
        self.assertEqual(ra.substitute_mesh_tiers(16), ('medium', 'light'))
        self.assertEqual(ra.substitute_mesh_tiers(21), ('medium',))
        self.assertEqual(ra.substitute_mesh_tiers(26), ('heavy',))
        self.assertEqual(ra.substitute_mesh_tiers(31), ('elite', 'heavy'))
        self.assertEqual(ra.substitute_mesh_tiers(36), ('elite',))
        self.assertEqual(ra.substitute_mesh_tiers(41), ('lord', 'elite'))


class NobleLines(unittest.TestCase):
    """A noble line wears a band above a regular troop of its level (Mike, 2026-09-25): the
    gate allows one mesh tier above the level's ceiling, and the curve prices the noble's kit
    as if the wearer stood in the next stat band."""

    def test_nobles_may_wear_one_tier_above_the_levels_ceiling(self):
        expected = {
            11: {'light', 'medium', 'heavy'}, 16: {'light', 'medium', 'heavy'},
            21: {'medium', 'heavy'},
            26: {'heavy', 'elite'},
            31: {'heavy', 'elite', 'lord'},
            36: {'elite', 'lord'},
            41: {'elite', 'lord'},   # nothing above lord
        }
        for level, tiers in expected.items():
            self.assertEqual(ra.allowed_mesh_tiers(level, noble=True), frozenset(tiers), level)

    def test_a_noble_anchors_at_the_first_level_of_the_next_band(self):
        # level_to_band: light to 13, medium 14-18, heavy 19-30, elite from 31.
        for level, anchored in {6: 14, 11: 14, 16: 19, 21: 31, 26: 31, 31: 31, 36: 36, 46: 46}.items():
            self.assertEqual(ra.noble_anchor_level(level), anchored, level)
        for level in (11, 21, 26):
            self.assertEqual(ra.MESH_TIER_ORDER.index(ra.level_to_band(ra.noble_anchor_level(level))),
                             ra.MESH_TIER_ORDER.index(ra.level_to_band(level)) + 1, level)

    def test_a_noble_never_anchors_an_item_below_the_tier_in_its_id(self):
        # The level-11 Ringlo militia wears the regular Anorien heavy helmet; a band up is only
        # medium, and anchoring there dragged the helmet from 43 to 33 for every regular troop
        # wearing it. A noble may raise an item's price, never lower it below its own tier.
        self.assertEqual(ra.noble_anchor_level(11, 'sk_x_helmet_heavy_a'), 19)
        self.assertEqual(ra.noble_anchor_level(11, 'sk_x_helmet_med_a'), 14)
        self.assertEqual(ra.noble_anchor_level(26, 'sk_x_chest_lord_a'), 31)
        self.assertEqual(ra.noble_anchor_level(26, 'sk_x_chest_med_a'), 31)   # a band up wins
        self.assertEqual(ra.noble_anchor_level(11, 'sk_dale_chest_a03'), 14)  # no tier token

    def test_a_nobles_substitute_is_priced_off_its_raised_band(self):
        # L21 noble: allowed medium/heavy; its band is elite, so heavy comes first.
        self.assertEqual(ra.substitute_mesh_tiers(21, noble=True), ('heavy', 'medium'))
        self.assertEqual(ra.substitute_mesh_tiers(21), ('medium',))

    def test_the_gate_judges_a_noble_one_tier_up(self):
        troops = {
            'noble': _troop(21, {'Body': 'sk_x_chest_heavy_a', 'Head': 'sk_x_helmet_elite_a',
                                 'Leg': 'sk_x_grvs_light_a'}),
        }
        hits = ra.mesh_ladder_violations(troops, noble={'noble'})
        self.assertEqual([(h['item'], h['direction']) for h in hits],
                         [('sk_x_helmet_elite_a', 'over'), ('sk_x_grvs_light_a', 'under')])
        self.assertEqual(hits[0]['allowed'], ('medium', 'heavy'))
        # The same troop judged as a regular also flags the heavy chest.
        self.assertIn('sk_x_chest_heavy_a', [h['item'] for h in ra.mesh_ladder_violations(troops)])


    def test_a_level_16_nobles_substitutes_start_at_its_raised_heavy_band(self):
        # Level 16 is the medium band; a noble is priced in the heavy band and may wear heavy.
        self.assertEqual(ra.substitute_mesh_tiers(16, noble=True), ('heavy', 'medium', 'light'))
        self.assertEqual(ra.substitute_mesh_tiers(16), ('medium', 'light'))


class PairExemption(unittest.TestCase):
    def test_an_exempt_troop_item_pair_is_skipped_and_nothing_else(self):
        troops = {'snaga': _troop(11, {'Body': 'sk_x_chest_lord_a', 'Head': 'sk_x_helmet_lord_a'})}
        hits = ra.mesh_ladder_violations(troops, exempt_items={('snaga', 'sk_x_chest_lord_a')})
        self.assertEqual([h['item'] for h in hits], ['sk_x_helmet_lord_a'])

    def test_the_pair_is_keyed_on_the_troop_as_well_as_the_item(self):
        troops = {'snaga': _troop(11, {'Body': 'sk_x_chest_lord_a'}),
                  'grunt': _troop(11, {'Body': 'sk_x_chest_lord_a'})}
        hits = ra.mesh_ladder_violations(troops, exempt_items={('snaga', 'sk_x_chest_lord_a')})
        self.assertEqual([h['troop'] for h in hits], ['grunt'])


def _troop(level, *sets, **extra):
    rec = {'level': level, 'sets': list(sets), 'file': 'troops/troops_x.xml', 'line': 1}
    rec.update(extra)
    return rec


class Violations(unittest.TestCase):
    def test_over_and_under_dressed_are_classified(self):
        troops = {
            'snaga': _troop(11, {'Body': 'sk_x_chest_lord_a'}, {'Body': 'sk_x_chest_light_a'}),
            'champion': _troop(41, {'Body': 'sk_x_chest_med_a', 'Head': 'sk_x_helmet_lord_a'}),
        }
        hits = ra.mesh_ladder_violations(troops)
        self.assertEqual([(h['troop'], h['slot'], h['item'], h['tier'], h['direction']) for h in hits],
                         [('champion', 'Body', 'sk_x_chest_med_a', 'medium', 'under'),
                          ('snaga', 'Body', 'sk_x_chest_lord_a', 'lord', 'over')])
        over = hits[1]
        self.assertEqual(over['level'], 11)
        self.assertEqual(over['allowed'], ('light', 'medium'))
        self.assertEqual(over['file'], 'troops/troops_x.xml')

    def test_one_row_per_troop_slot_item_even_when_worn_in_many_sets(self):
        troops = {'snaga': _troop(11, {'Body': 'sk_x_chest_lord_a'}, {'Body': 'sk_x_chest_lord_a'},
                                  {'Body': 'sk_x_chest_lord_a'})}
        hits = ra.mesh_ladder_violations(troops)
        self.assertEqual(len(hits), 1)
        self.assertEqual(hits[0]['sets'], 3)

    def test_kit_off_the_ladder_is_skipped(self):
        troops = {
            'recruit': _troop(6, {'Body': 'sk_x_civ_heavy_coat_a'},      # civilian token
                              {'Body': 'sk_dale_chest_a03'},              # no tier token
                              {'Item0': 'sk_x_sword_lord_a'}),            # not an armour slot
        }
        self.assertEqual(ra.mesh_ladder_violations(troops), [])

    def test_exempt_troops_and_unlevelled_troops_are_skipped(self):
        troops = {
            'ranger': _troop(51, {'Body': 'sk_x_chest_light_a'}),
            'no_level': _troop(None, {'Body': 'sk_x_chest_lord_a'}),
        }
        self.assertEqual(ra.mesh_ladder_violations(troops, exempt={'ranger'}), [])
        self.assertEqual(len(ra.mesh_ladder_violations(troops)), 1)

    def test_a_troop_within_the_ladder_yields_nothing(self):
        troops = {
            'grunt': _troop(16, {'Body': 'sk_x_chest_light_a'}, {'Body': 'sk_x_chest_med_b'}),
            'veteran': _troop(31, {'Body': 'sk_x_chest_heavy_a', 'Head': 'sk_x_helmet_elite_a'}),
            'lord_guard': _troop(46, {'Body': 'sk_x_chest_lord_a'}),
        }
        self.assertEqual(ra.mesh_ladder_violations(troops), [])


if __name__ == '__main__':
    unittest.main()
