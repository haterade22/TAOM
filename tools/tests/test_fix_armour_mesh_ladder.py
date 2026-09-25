#!/usr/bin/env python3
"""Tests for tools/fix_armour_mesh_ladder.py (#609).

Run:  python -m unittest tools.tests.test_fix_armour_mesh_ladder

THE CONTRACT
------------
An over-dressed troop (a level-11 snaga in a `_lord_` chest) is moved to the SAME line at the
substitute tier for its level, same variant suffix when the line has it, nearest otherwise,
the same old item becoming the same new item in every set. A line with nothing at any allowed
tier is reported, not written. Under-dressed rows are reported, never written. The write is
byte-faithful (BOM, CRLF) and a second run changes nothing.
"""
import codecs
import os
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import fix_armour_mesh_ladder as fx  # noqa: E402
import fix_upgrade_armour_regressions as fu  # noqa: E402


def _items(*specs):
    """{id: record} in load_item_armour's shape from (id, file, value) tuples."""
    return {iid: {'value': val, 'file': f, 'folder': 'x', 'stats': {}, 'type': None, 'name': iid}
            for iid, f, val in specs}


ITEMS = _items(
    ('sk_x_chest_light_a', 'body_armors.xml', 20), ('sk_x_chest_light_b', 'body_armors.xml', 21),
    ('sk_x_chest_light_d', 'body_armors.xml', 23),
    ('sk_x_chest_med_a', 'body_armors.xml', 31), ('sk_x_chest_med_c', 'body_armors.xml', 33),
    ('sk_x_chest_heavy_a', 'body_armors.xml', 41),
    ('sk_x_chest_elite_a', 'body_armors.xml', 49), ('sk_x_chest_elite_c', 'body_armors.xml', 49),
    ('sk_x_chest_lord_a', 'body_armors.xml', 49), ('sk_x_chest_lord_c', 'body_armors.xml', 49),
    ('sk_x_chest_lord_f', 'body_armors.xml', 49),
    ('sk_x_helmet_lord_a', 'head_armors.xml', 44),          # a lord helmet with no lower line
    ('sk_x_chest_lord_h', 'head_armors.xml', 44),           # same stem, other slot file: never a chest
    ('sk_x_pauld_heavy_cape_a', 'shoulder_armors.xml', 20),
    ('sk_x_pauld_med_cape_b', 'shoulder_armors.xml', 15), ('sk_x_pauld_med_a', 'shoulder_armors.xml', 14),
)


class SplitId(unittest.TestCase):
    def test_splits_at_the_tier_token(self):
        self.assertEqual(fx.split_id('sk_x_chest_lord_c'), ('sk_x_chest', 'lord', '_c'))
        self.assertEqual(fx.split_id('sk_x_chest_med_d'), ('sk_x_chest', 'medium', '_d'))
        self.assertEqual(fx.split_id('sk_x_helmet_medium_b1'), ('sk_x_helmet', 'medium', '_b1'))
        self.assertEqual(fx.split_id('sk_x_pauld_heavy_cape_a'), ('sk_x_pauld', 'heavy', '_cape_a'))
        self.assertEqual(fx.split_id('rivendell_torso_lord'), ('rivendell_torso', 'lord', ''))

    def test_off_ladder_ids_return_none(self):
        self.assertIsNone(fx.split_id('sk_x_civ_heavy_coat_a'))
        self.assertIsNone(fx.split_id('sk_dale_chest_a03'))
        self.assertIsNone(fx.split_id('sk_x_lordly_a'))

    def test_a_digit_may_follow_the_token(self):
        # rivendell_torso_lord3_silver and thenn_armor_med1 are tiered by mesh_tier_of (a
        # substring scan); the split must read them the same way or the validator flags what
        # the fixer cannot touch (deep review, 2026-09-16: 14 such ids, 2 worn).
        self.assertEqual(fx.split_id('rivendell_torso_lord3_silver'), ('rivendell_torso', 'lord', '3_silver'))
        self.assertEqual(fx.split_id('thenn_armor_med1'), ('thenn_armor', 'medium', '1'))
        self.assertEqual(fx.split_id('thenn_armor_heavy2'), ('thenn_armor', 'heavy', '2'))


class PickSubstitute(unittest.TestCase):
    def setUp(self):
        self.index = fx.build_line_index(ITEMS)

    def test_same_suffix_at_the_substitute_tier(self):
        self.assertEqual(fx.pick_substitute('sk_x_chest_lord_a', 11, ITEMS, self.index),
                         ('sk_x_chest_light_a', 'light'))

    def test_nearest_suffix_when_the_same_one_is_missing(self):
        # lord_c: light has a, b, d; d is one letter away, b is one away too; ties break on id.
        self.assertEqual(fx.pick_substitute('sk_x_chest_lord_c', 11, ITEMS, self.index),
                         ('sk_x_chest_light_b', 'light'))
        self.assertEqual(fx.pick_substitute('sk_x_chest_lord_f', 11, ITEMS, self.index),
                         ('sk_x_chest_light_d', 'light'))

    def test_level_16_prefers_medium_and_level_11_light(self):
        self.assertEqual(fx.pick_substitute('sk_x_chest_lord_c', 16, ITEMS, self.index),
                         ('sk_x_chest_med_c', 'medium'))
        self.assertEqual(fx.pick_substitute('sk_x_chest_lord_c', 11, ITEMS, self.index)[1], 'light')

    def test_walks_down_the_allowed_tiers_when_the_first_is_missing(self):
        items = dict(ITEMS)
        for k in list(items):
            if '_med_' in k and k.startswith('sk_x_chest'):
                del items[k]
        index = fx.build_line_index(items)
        self.assertEqual(fx.pick_substitute('sk_x_chest_lord_c', 16, items, index),
                         ('sk_x_chest_light_b', 'light'))

    def test_none_when_the_line_has_nothing_allowed(self):
        self.assertIsNone(fx.pick_substitute('sk_x_helmet_lord_a', 11, ITEMS, self.index))
        # level 21 allows medium only; the chest line has medium, the lord helmet line does not
        self.assertIsNone(fx.pick_substitute('sk_x_helmet_lord_a', 21, ITEMS, self.index))

    def test_never_crosses_slot_files(self):
        # sk_x_chest_lord_h is defined in head_armors.xml: not a chest candidate, and a chest is
        # not a candidate for it.
        self.assertIsNone(fx.pick_substitute('sk_x_chest_lord_h', 11, ITEMS, self.index))

    def test_structurally_matching_suffix_wins(self):
        self.assertEqual(fx.pick_substitute('sk_x_pauld_heavy_cape_a', 16, ITEMS, self.index),
                         ('sk_x_pauld_med_cape_b', 'medium'))

    def test_variant_anchored_at_the_troops_band_beats_the_same_suffix(self):
        # The kingdom-cap curve prices a variant by its LOWEST wearer, so two `_med_` chests
        # can sit two bands apart: med_a worn by a level-11 recruit is light-band kit, med_c
        # worn from level 21 is heavy-band kit. A level-21 troop demoted onto med_a would drop
        # a band below its own parent (the 2026-09-16 first apply: nine upgrade regressions).
        anchors = {'sk_x_chest_med_a': 11, 'sk_x_chest_med_c': 21}
        self.assertEqual(fx.pick_substitute('sk_x_chest_elite_a', 21, ITEMS, self.index, anchors),
                         ('sk_x_chest_med_c', 'medium'))
        # A level-16 troop (medium band) with no variant at its band: an UNWORN variant is as
        # good as one at the band (it becomes medium-band kit with nobody else affected).
        anchors = {'sk_x_chest_med_a': 11}
        self.assertEqual(fx.pick_substitute('sk_x_chest_elite_a', 16, ITEMS, self.index, anchors),
                         ('sk_x_chest_med_c', 'medium'))
        # Both variants at the band: the suffix decides again.
        anchors = {'sk_x_chest_med_a': 11, 'sk_x_chest_med_c': 11}
        self.assertEqual(fx.pick_substitute('sk_x_chest_elite_a', 11, ITEMS, self.index, anchors),
                         ('sk_x_chest_light_a', 'light'))
        self.assertEqual(fx.pick_substitute('sk_x_chest_lord_c', 16, ITEMS, self.index,
                                            {'sk_x_chest_med_a': 16, 'sk_x_chest_med_c': 16}),
                         ('sk_x_chest_med_c', 'medium'))

    def test_a_variant_below_the_band_beats_one_above_it_at_equal_distance(self):
        # Below: this troop wears less than its band, nobody else moves. Above: this troop
        # becomes the variant's new anchor and every higher wearer loses a band.
        # Level 16 is the medium band; med_a (level 11, light) and med_c (level 21, heavy) are
        # each one band away.
        anchors = {'sk_x_chest_med_a': 11, 'sk_x_chest_med_c': 21}
        self.assertEqual(fx.pick_substitute('sk_x_chest_elite_a', 16, ITEMS, self.index, anchors),
                         ('sk_x_chest_med_a', 'medium'))
        # Two bands below loses to one band above.
        anchors = {'sk_x_chest_med_a': 11, 'sk_x_chest_med_c': 31}
        self.assertEqual(fx.pick_substitute('sk_x_chest_elite_a', 21, ITEMS, self.index, anchors),
                         ('sk_x_chest_med_c', 'medium'))

    def test_line_anchors_are_the_lowest_in_scope_battle_wearer(self):
        troops = {
            'snaga': _troop('snaga', 11, {'Body': 'sk_x_chest_med_a'}),
            'grunt': _troop('grunt', 16, {'Body': 'sk_x_chest_med_a'}, {'Body': 'sk_x_chest_med_c'}),
            'villager': _troop('villager', 6, {'Body': 'sk_x_chest_med_c'},
                               file='characters/npcs_x.xml', external=True),
        }
        self.assertEqual(fx.line_anchors(fx.in_scope(troops)), {'sk_x_chest_med_a': 11, 'sk_x_chest_med_c': 16})


def _troop(tid, level, *sets, file='troops/troops_x.xml', external=False):
    return {'id': tid, 'level': level, 'has_level': True, 'sets': list(sets), 'file': file,
            'external': external, 'upgrades': []}


class Plan(unittest.TestCase):
    def test_over_with_substitute_under_and_unresolved_are_separated(self):
        troops = {
            'snaga': _troop('snaga', 11, {'Body': 'sk_x_chest_lord_a', 'Head': 'sk_x_helmet_lord_a'},
                            {'Body': 'sk_x_chest_lord_a'}),
            'champion': _troop('champion', 41, {'Body': 'sk_x_chest_med_a'}),
        }
        changes, unresolved, under = fx.plan(troops, ITEMS)
        self.assertEqual([(c['troop'], c['slot'], c['old'], c['new'], c['sets']) for c in changes],
                         [('snaga', 'Body', 'sk_x_chest_lord_a', 'sk_x_chest_light_a', 2)])
        self.assertEqual([(h['troop'], h['item']) for h in unresolved], [('snaga', 'sk_x_helmet_lord_a')])
        self.assertEqual([(h['troop'], h['item'], h['direction']) for h in under],
                         [('champion', 'sk_x_chest_med_a', 'under')])
        self.assertEqual(changes[0]['culture'], 'x')

    def test_lower_troops_place_first_and_each_pick_moves_the_anchor(self):
        # Two troops demoted onto the medium tier in one run, both `_a` variants of their old
        # kit, with med_a and med_c unworn. Placed lowest first: the level-16 troop takes med_a
        # (unworn, same suffix) and thereby anchors it to the medium band; the level-21 troop
        # (heavy band) then sees med_a one band below and med_c still unworn, and takes med_c.
        # A single pre-run snapshot would have put both on med_a and priced the level-21 troop
        # a band low (deep review, 2026-09-16).
        items = _items(('sk_x_chest_heavy_a', 'body_armors.xml', 41), ('sk_x_chest_elite_a', 'body_armors.xml', 49),
                       ('sk_x_chest_med_a', 'body_armors.xml', 31), ('sk_x_chest_med_c', 'body_armors.xml', 33))
        troops = {
            'zz_veteran': _troop('zz_veteran', 21, {'Body': 'sk_x_chest_elite_a'}),
            'aa_grunt': _troop('aa_grunt', 16, {'Body': 'sk_x_chest_heavy_a'}),
        }
        changes, _, _ = fx.plan(troops, items)
        self.assertEqual([(c['troop'], c['new']) for c in changes],
                         [('aa_grunt', 'sk_x_chest_med_a'), ('zz_veteran', 'sk_x_chest_med_c')])

    def test_a_swap_onto_a_variant_off_the_troops_band_is_marked(self):
        troops = {'fighter': _troop('fighter', 21, {'Body': 'sk_x_chest_elite_a'}),
                  'militia': _troop('militia', 16, {'Body': 'sk_x_chest_med_a'}, {'Body': 'sk_x_chest_med_c'})}
        changes, _, _ = fx.plan(troops, ITEMS)
        self.assertEqual([(c['troop'], c['new'], c['anchor_note']) for c in changes],
                         [('fighter', 'sk_x_chest_med_a', 'anchor L16, a band below')])

    def test_the_fixer_reads_the_validators_sets(self):
        import taom_schema as ts
        self.assertEqual(fx.EXEMPT_TROOPS, frozenset(ts.Validator._ARMOUR_LADDER_EXEMPT)
                         | frozenset(ts.Validator._BODYLESS_BY_DESIGN))
        self.assertEqual(fx.NOBLE_TROOPS, frozenset(ts.Validator._NOBLE_LINE_TROOPS))
        self.assertEqual(fx.EXEMPT_ITEMS, frozenset(ts.Validator._ARMOUR_LADDER_EXEMPT_ITEMS))

    def test_a_noble_one_tier_up_stays_and_its_anchor_is_raised(self):
        old = (fx.NOBLE_TROOPS, fx.EXEMPT_ITEMS)
        try:
            fx.NOBLE_TROOPS = frozenset({'noble'})
            fx.EXEMPT_ITEMS = frozenset({('grunt', 'sk_x_chest_elite_a')})
            troops = {
                'noble': _troop('noble', 21, {'Body': 'sk_x_chest_heavy_a'}),
                'grunt': _troop('grunt', 16, {'Body': 'sk_x_chest_elite_a'}),
            }
            changes, unresolved, _ = fx.plan(troops, ITEMS)
            self.assertEqual((changes, unresolved), ([], []))
            # The noble anchors heavy_a at 31 (a band up); the exempt pair anchors nothing.
            self.assertEqual(fx.line_anchors(fx.in_scope(troops)), {'sk_x_chest_heavy_a': 31})
        finally:
            fx.NOBLE_TROOPS, fx.EXEMPT_ITEMS = old

    def test_an_over_dressed_noble_is_swapped_within_its_one_tier_up_allowance(self):
        old = fx.NOBLE_TROOPS
        try:
            fx.NOBLE_TROOPS = frozenset({'noble'})
            troops = {'noble': _troop('noble', 16, {'Body': 'sk_x_chest_elite_a'})}
            changes, _, _ = fx.plan(troops, ITEMS)
            self.assertEqual([(c['troop'], c['new']) for c in changes], [('noble', 'sk_x_chest_heavy_a')])
        finally:
            fx.NOBLE_TROOPS = old

    def test_an_exempt_pair_leaves_the_troops_other_items_anchoring(self):
        old = fx.EXEMPT_ITEMS
        try:
            fx.EXEMPT_ITEMS = frozenset({('grunt', 'sk_x_chest_elite_a')})
            troops = {'grunt': _troop('grunt', 16, {'Body': 'sk_x_chest_elite_a', 'Cape': 'sk_x_pauld_med_a'})}
            self.assertEqual(fx.line_anchors(fx.in_scope(troops)), {'sk_x_pauld_med_a': 16})
        finally:
            fx.EXEMPT_ITEMS = old

    def test_a_noble_ranks_a_candidate_at_the_level_it_will_be_placed_at(self):
        # A level-11 noble falling through to heavy is placed at 19 (the heavy floor), so the
        # heavy variant already anchored in the heavy band is the one at its band, not the one
        # anchored at 16 (deep review wave 3, 2026-09-25).
        items = _items(('sk_y_chest_elite_a', 'body_armors.xml', 49),
                       ('sk_y_chest_heavy_a', 'body_armors.xml', 41), ('sk_y_chest_heavy_c', 'body_armors.xml', 41))
        anchors = {'sk_y_chest_heavy_a': 16, 'sk_y_chest_heavy_c': 19}
        self.assertEqual(fx.pick_substitute('sk_y_chest_elite_a', 11, items, fx.build_line_index(items),
                                            anchors, noble=True), ('sk_y_chest_heavy_c', 'heavy'))

    def test_scope_is_troops_files_with_a_level_minus_the_exempt(self):
        troops = {
            'villager_x': _troop('villager_x', 6, {'Body': 'sk_x_chest_lord_a'},
                                 file='characters/npcs_x.xml', external=True),
            'cave_troll': _troop('cave_troll', 6, {'Body': 'sk_x_chest_lord_a'}),
            'other_culture': _troop('other_culture', 6, {'Body': 'sk_x_chest_lord_a'},
                                    file='troops/troops_y.xml'),
            'snaga': _troop('snaga', 6, {'Body': 'sk_x_chest_lord_a'}),
        }
        changes, _, _ = fx.plan(troops, ITEMS, cultures={'x'})
        self.assertEqual([c['troop'] for c in changes], ['snaga'])
        changes, _, _ = fx.plan(troops, ITEMS)
        self.assertEqual([c['troop'] for c in changes], ['other_culture', 'snaga'])


FIXTURE = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n'
    '<NPCCharacters>\r\n'
    '    <NPCCharacter id="snaga" level="11" name="{=k}Snaga" culture="Culture.x">\r\n'
    '        <Equipments>\r\n'
    '            <EquipmentRoster>\r\n'
    '                <equipment slot="Item0" id="Item.axe" />\r\n'
    '                <equipment slot="Body" id="Item.sk_x_chest_lord_a" />\r\n'
    '            </EquipmentRoster>\r\n'
    '            <EquipmentRoster>\r\n'
    '                <equipment slot="Body" id="Item.sk_x_chest_lord_a" />\r\n'
    '                <equipment slot="Head" id="Item.sk_x_helmet_lord_a" />\r\n'
    '            </EquipmentRoster>\r\n'
    '            <!-- <equipment slot="Body" id="Item.sk_x_chest_lord_c" /> a commented set -->\r\n'
    '            <EquipmentRoster civilian="true">\r\n'
    '                <equipment slot="Body" id="Item.sk_x_chest_lord_a" />\r\n'
    '            </EquipmentRoster>\r\n'
    '        </Equipments>\r\n'
    '    </NPCCharacter>\r\n'
    '</NPCCharacters>\r\n'
)


class Write(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        os.makedirs(os.path.join(self.tmp, 'troops'))
        os.makedirs(os.path.join(self.tmp, 'characters'))
        self.path = os.path.join(self.tmp, 'troops', 'troops_x.xml')
        with open(self.path, 'wb') as fh:
            fh.write(codecs.BOM_UTF8 + FIXTURE.encode('utf-8'))

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def _run(self):
        troops = fu.load_troops(self.tmp)
        changes, unresolved, under = fx.plan(troops, ITEMS)
        return changes, unresolved, fu.write_changes(changes)

    def test_write_is_byte_faithful_and_touches_only_battle_sets(self):
        changes, unresolved, written = self._run()
        self.assertEqual(written, 1)
        self.assertEqual([(c['old'], c['new']) for c in changes],
                         [('sk_x_chest_lord_a', 'sk_x_chest_light_a')])
        self.assertEqual([h['item'] for h in unresolved], ['sk_x_helmet_lord_a'])
        raw = open(self.path, 'rb').read()
        self.assertTrue(raw.startswith(codecs.BOM_UTF8))
        text = raw.decode('utf-8-sig')
        self.assertNotIn('\n', text.replace('\r\n', ''))                     # CRLF kept
        self.assertEqual(text.count('Item.sk_x_chest_light_a'), 2)          # both battle sets
        self.assertIn('<EquipmentRoster civilian="true">\r\n                '
                      '<equipment slot="Body" id="Item.sk_x_chest_lord_a" />', text)   # civilian kept
        self.assertIn('<!-- <equipment slot="Body" id="Item.sk_x_chest_lord_c" />', text)  # comment kept
        self.assertIn('Item.sk_x_helmet_lord_a', text)                       # unresolved untouched

    def test_second_run_is_a_no_op(self):
        self._run()
        before = open(self.path, 'rb').read()
        changes, _, written = self._run()
        self.assertEqual(changes, [])
        self.assertEqual(written, 0)
        self.assertEqual(open(self.path, 'rb').read(), before)


if __name__ == '__main__':
    unittest.main()
