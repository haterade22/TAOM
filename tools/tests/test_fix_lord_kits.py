#!/usr/bin/env python3
"""Tests for tools/fix_lord_kits.py (the lord kit pass, #1271 follow-up to #609).

Run:  python -m unittest tools.tests.test_fix_lord_kits

THE CONTRACT
------------
A hero wears ONE whole battle set (Hero.SetInitialValuesFromCharacter clones one random set), so a
lord is judged per whole set on head, body, arm and leg against the best elite-band troop of his
culture on each part; the floor and the ceiling (reference plus 5) inform, they are not targets.
Within a culture the generic lord templates wear different lord armour: a template in a soldier's
piece, or sharing its Body with another template while an unused lord Body remains, takes an
unused lord set, its other slots from the Body's family. A template in a lord or hero set of its
own keeps it. The fallback and ruler rosters mirror template _a. Writes are byte-faithful and
re-read through ElementTree before anything is written.
"""
import codecs
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import fix_lord_kits as lk  # noqa: E402
import taom_schema as ts  # noqa: E402

TYPES = {'Head': 'HeadArmor', 'Body': 'BodyArmor', 'Cape': 'Cape', 'Gloves': 'HandArmor', 'Leg': 'LegArmor'}


def _it(slot, folder='xf', h=0, b=0, a=0, l=0, name=''):
    return {'stats': {'head_armor': h, 'body_armor': b, 'arm_armor': a, 'leg_armor': l},
            'folder': folder, 'file': slot.lower() + '.xml', 'type': TYPES[slot], 'name': name}


ITEMS = {
    'x_chest_light_a': _it('Body', b=6),
    'x_helm_heavy_a': _it('Head', h=30),
    'x_chest_elite_a': _it('Body', b=46, a=5, l=10),
    'x_pauld_elite_a': _it('Cape', b=13, a=8),
    'x_bracer_elite_a': _it('Gloves', a=20),
    'x_boots_elite_a': _it('Leg', l=20),
    'x_helm_champion_a': _it('Head', h=40),
    'x_chest_lord_a': _it('Body', b=40),
    'x_chest_lord_b': _it('Body', b=30),
    'x_chest_lord_c': _it('Body', b=20),
    'x_helmet_lord_a': _it('Head', h=35),
    'x_helmet_lord_b': _it('Head', h=36),
    'x_pauld_lord_b': _it('Cape', b=10),
    'x_noble_chest_a': _it('Body', b=44),
    'x_ghost_lord_a': _it('Body', b=50),
    'x_hero_mail': _it('Body', b=48),
    'x_elrond_robe': _it('Body', b=60, name='Elrond Robe'),
    'z_chest_lord_a': _it('Body', folder='zf', b=55),
}


def _troop(level, culture, *sets, race=None, file='troops_x.xml'):
    return {'level': level, 'culture': culture, 'race': race, 'sets': list(sets), 'file': file}


TROOPS = {
    'x_recruit': _troop(6, 'x', {'Body': 'x_chest_light_a'}),
    'x_sergeant': _troop(26, 'x', {'Body': 'x_chest_lord_a'}),
    'x_guard': _troop(31, 'x', {'Head': 'x_helm_heavy_a', 'Body': 'x_chest_elite_a', 'Cape': 'x_pauld_elite_a',
                               'Gloves': 'x_bracer_elite_a', 'Leg': 'x_boots_elite_a'}),
    'x_champion': _troop(36, 'x', {'Head': 'x_helm_champion_a'}),
    'x_kingsguard': _troop(46, 'x', {'Body': 'x_noble_chest_a'}),
    'z_guard': _troop(31, 'z', {'Body': 'z_chest_lord_a'}, file='troops_z.xml'),
}


def _lord(lid, culture, *templates, race=None, overrides=None, inline=()):
    return {'id': lid, 'culture': culture, 'race': race, 'file': 'lords.xml', 'line': 1,
            'templates': list(templates), 'inline_sets': list(inline), 'overrides': dict(overrides or {})}


def _tmpl(culture, *sets, file='sets_x.xml'):
    return {'file': file, 'culture': culture, 'sets': [dict(s) for s in sets]}


class WholeSetMeasure(unittest.TestCase):
    def test_a_lord_is_judged_by_his_worst_whole_set_not_the_least_slot_of_each(self):
        sets = [{'Body': 'x_chest_elite_a', 'Cape': 'x_pauld_elite_a'},
                {'Body': 'x_noble_chest_a', 'Cape': 'x_pauld_lord_b'}]
        worst, best = lk.whole_set_extremes(sets, ITEMS)
        # Per slot the least would be 44 + 10 = 54; the worst WHOLE set is 46 + 13 = 59 against 54.
        self.assertEqual((worst['body'], best['body']), (54, 59))
        self.assertEqual((worst['arm'], best['arm']), (0, 13))

    def test_each_part_takes_its_own_best_elite_troop(self):
        ref = lk.culture_reference(TROOPS, ITEMS, 'x')
        self.assertEqual(ref['head'], (40, 'x_champion'))
        self.assertEqual(ref['body'], (59, 'x_guard'))
        self.assertEqual(ref['arm'], (33, 'x_guard'))
        self.assertEqual(ref['leg'], (30, 'x_guard'))

    def test_a_culture_without_an_elite_troop_has_no_reference(self):
        self.assertIsNone(lk.culture_reference(TROOPS, ITEMS, 'khand'))

    def test_a_race_with_its_own_elite_troop_is_judged_against_it(self):
        troops = dict(TROOPS, x_orc=_troop(31, 'x', {'Body': 'x_chest_lord_c'}, race='orc'))
        self.assertEqual(lk.culture_reference(troops, ITEMS, 'x', 'orc')['body'], (20, 'x_orc'))
        self.assertEqual(lk.culture_reference(troops, ITEMS, 'x', 'elf')['body'], (59, 'x_guard'))


class JudgeLords(unittest.TestCase):
    TEMPLATES = {'x_bat_template_medium_a': [{'Body': 'x_chest_light_a'}],
                 'x_bat_template_medium_b': [{'Head': 'x_helm_champion_a', 'Body': 'x_chest_elite_a',
                                              'Cape': 'x_pauld_elite_a', 'Gloves': 'x_bracer_elite_a',
                                              'Leg': 'x_boots_elite_a'}],
                 'x_bat_template_medium_c': [{'Head': 'x_helm_champion_a', 'Body': 'z_chest_lord_a',
                                              'Cape': 'x_pauld_elite_a', 'Gloves': 'x_bracer_elite_a',
                                              'Leg': 'x_boots_elite_a'}]}

    def test_a_lord_under_his_reference_names_the_part_his_figure_and_the_troop(self):
        rows = lk.judge_lords([_lord('lord_1', 'x', 'x_bat_template_medium_a')], self.TEMPLATES, TROOPS, ITEMS)
        self.assertEqual(rows[0]['under']['body'], (6, 59, 'x_guard'))
        self.assertEqual(sorted(rows[0]['under']), ['arm', 'body', 'head', 'leg'])
        self.assertEqual(rows[0]['over'], {})

    def test_a_lord_at_his_reference_is_clean(self):
        rows = lk.judge_lords([_lord('lord_2', 'x', 'x_bat_template_medium_b')], self.TEMPLATES, TROOPS, ITEMS)
        self.assertEqual((rows[0]['under'], rows[0]['over']), ({}, {}))

    def test_a_lord_past_the_reference_plus_five_is_over_the_limit(self):
        rows = lk.judge_lords([_lord('lord_3', 'x', 'x_bat_template_medium_c')], self.TEMPLATES, TROOPS, ITEMS)
        # 55 + 13 = 68 against 59 + 5.
        self.assertEqual(rows[0]['over'], {'body': (68, 64)})

    def test_a_culture_with_no_elite_troop_and_no_known_template_culture_is_not_judged(self):
        self.assertEqual(lk.judge_lords([_lord('lord_k', 'khand', 'x_bat_template_medium_a')],
                                        self.TEMPLATES, TROOPS, ITEMS), [])

    def test_a_culture_with_no_elite_troop_is_judged_against_the_culture_of_its_templates(self):
        rows = lk.judge_lords([_lord('lord_k', 'khand', 'x_bat_template_medium_a')], self.TEMPLATES, TROOPS, ITEMS,
                              template_cultures={'x_bat_template_medium_a': 'x'})
        self.assertEqual((rows[0]['culture'], rows[0]['judged_as']), ('khand', 'x'))
        self.assertEqual(rows[0]['short']['body'], (6, 59, 'x_guard'))

    def test_a_part_within_ten_percent_of_the_reference_is_under_but_not_short(self):
        # Body 46 + 10 = 56 against 59: 3 under, inside the 5.9 tolerance.
        templates = {'x_bat_template_medium_d': [{'Head': 'x_helm_champion_a', 'Body': 'x_chest_elite_a',
                                                  'Cape': 'x_pauld_lord_b', 'Gloves': 'x_bracer_elite_a',
                                                  'Leg': 'x_boots_elite_a'}]}
        row = lk.judge_lords([_lord('lord_d', 'x', 'x_bat_template_medium_d')], templates, TROOPS, ITEMS)[0]
        self.assertIn('body', row['under'])
        self.assertNotIn('body', row['short'])
        self.assertTrue(lk.is_short(53, 59))
        self.assertFalse(lk.is_short(54, 60))

    def test_a_gap_under_three_points_is_never_short(self):
        # The goblin leg lost variety for 3 points against 6: a gap must also be at least 3.
        self.assertFalse(lk.is_short(5, 7))
        self.assertTrue(lk.is_short(3, 6))
        self.assertTrue(lk.is_short(17, 20))


class Families(unittest.TestCase):
    def test_the_slot_word_and_the_variant_leave_the_family(self):
        self.assertEqual(lk.family_key('sk_dale_chest_chivalry_a04'), lk.family_key('sk_dale_helmet_chivlary_b04'))
        self.assertEqual(lk.variant_of('sk_dale_chest_chivalry_a04'), 'a04')
        self.assertEqual(lk.family_key('rivendell_torso_lord1_silver'), lk.family_key('rivendell_torso_lord3'))
        self.assertNotEqual(lk.family_key('roh_nbl_arm_1'), lk.family_key('cts_rohan_armor_captain'))

    def test_a_lord_armour_variant_is_of_its_sources_family_and_colour(self):
        self.assertEqual(lk.family_key('taom_lord_mkwd_inf3_chest_bronze'), lk.family_key('mkwd_inf3_chest_bronze'))
        self.assertEqual(lk.variant_of('taom_lord_mkwd_inf3_chest_bronze'), 'bronze')
        self.assertTrue(lk.same_look('taom_lord_mkwd_inf3_chest_bronze', 'taom_lord_mkwd_inf3_pauldrons_bronze'))
        self.assertFalse(lk.same_look('taom_lord_mkwd_inf3_chest_bronze', 'mkwd_inf3_pauldrons_silver'))


class LordPool(unittest.TestCase):
    def _pool(self, lord_sets=()):
        return lk.lord_pool('x', ITEMS, TROOPS, list(lord_sets))

    def test_lord_marked_pieces_of_a_family_the_culture_wears_are_in_the_pool(self):
        pool = self._pool()
        self.assertEqual(pool['slots']['Body'], ['x_chest_lord_a', 'x_chest_lord_b', 'x_chest_lord_c', 'x_noble_chest_a'])
        self.assertEqual(pool['slots']['Head'], ['x_helmet_lord_a', 'x_helmet_lord_b'])

    def test_another_cultures_folder_a_family_nobody_wears_and_hero_named_kit_stay_out(self):
        body = self._pool([('human', {'Body': 'x_elrond_robe'})])['slots']['Body']
        self.assertNotIn('z_chest_lord_a', body)
        self.assertNotIn('x_ghost_lord_a', body)
        self.assertNotIn('x_elrond_robe', body)

    def test_a_hero_only_piece_the_cultures_lords_wear_joins_the_pool(self):
        self.assertIn('x_hero_mail', self._pool([('human', {'Body': 'x_hero_mail'})])['slots']['Body'])

    def test_the_lowest_troop_wearer_is_recorded(self):
        self.assertEqual(self._pool()['lowest']['x_chest_lord_a'], (26, 'x_sergeant'))

    def test_a_lord_armour_variant_is_a_pool_piece_of_its_own_culture_only(self):
        # #1333: a repo variant has no Armory folder; the spec names its culture.
        items = dict(ITEMS, taom_lord_x_chest_light_a=dict(_it('Body', b=51), folder=None),
                     taom_lord_z_chest_lord_a=dict(_it('Body', b=51), folder=None))
        variants = {'taom_lord_x_chest_light_a': 'x', 'taom_lord_z_chest_lord_a': 'z'}
        # A variant is an id the spec lists (derive_armor_tiers.lord_variant_source); these two stand in it.
        with mock.patch.object(lk.dat, '_shipped_variant_cultures', return_value=variants):
            pool = lk.lord_pool('x', items, TROOPS, [], variants=variants)
        self.assertIn('taom_lord_x_chest_light_a', pool['slots']['Body'])
        self.assertNotIn('taom_lord_z_chest_lord_a', pool['slots']['Body'])
        # It fits the races its source's family is seen on, and no troop wears it.
        self.assertEqual(pool['fits']['taom_lord_x_chest_light_a'], {'human'})
        self.assertNotIn('taom_lord_x_chest_light_a', pool['lowest'])

    def test_the_pool_table_names_its_value_source_and_each_piece_family(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'pool.md')
            lk._pool_md({'x': self._pool()}, ITEMS, {'x_chest_lord_a': {'x_bat_template_medium_a'}}, path,
                        values='planned')
            text = open(path, encoding='utf-8').read()
        self.assertIn('planned values', text)
        self.assertIn('| Slot | Family | Piece |', text)
        self.assertIn('| Body | %s | `x_chest_lord_a` |' % '_'.join(lk.family_key('x_chest_lord_a')), text)
        self.assertIn('x_sergeant L26', text)
        # The promoter's clone templates wear the source pieces (or their item map's) but are not listed.
        self.assertIn('promoter clones', text)


class PlanAssignments(unittest.TestCase):
    def _plan(self, templates, lords):
        return lk.plan_assignments(templates, lords, TROOPS, ITEMS, tolerance=False)

    def _users(self, *rids):
        return [_lord('lord_%d' % i, 'x', rid) for i, rid in enumerate(rids)]

    def test_templates_sharing_a_body_spread_over_the_unused_lord_variants(self):
        rids = ['x_bat_template_medium_%s' % c for c in 'abc']
        templates = {rid: _tmpl('x', {'Body': 'x_chest_lord_a'}) for rid in rids}
        changes, _ = self._plan(templates, self._users(*rids))
        body = {c['template']: c['new'] for c in changes if c['slot'] == 'Body'}
        self.assertEqual(body, {'x_bat_template_medium_b': 'x_chest_lord_b', 'x_bat_template_medium_c': 'x_chest_lord_c'})

    def test_a_soldiers_piece_takes_an_unused_lord_set_with_its_family_in_the_other_slots(self):
        templates = {'x_bat_template_medium_a': _tmpl('x', {'Body': 'x_noble_chest_a'}),
                     'x_bat_template_medium_b': _tmpl('x', {'Body': 'x_chest_light_a', 'Head': 'x_helm_heavy_a'})}
        changes, _ = self._plan(templates, self._users(*templates))
        got = {(c['slot'], c['new']) for c in changes if c['template'] == 'x_bat_template_medium_b'}
        # x_chest_lord_a is also a level 26 sergeant's chest (priced at his band), so lord_b comes first;
        # the helmet follows the Body's variant letter.
        self.assertIn(('Body', 'x_chest_lord_b'), got)
        self.assertIn(('Head', 'x_helmet_lord_b'), got)
        self.assertIn(('Cape', 'x_pauld_lord_b'), got)
        self.assertFalse([c for c in changes if c['template'] == 'x_bat_template_medium_a'])

    def test_a_soldiers_piece_never_swaps_for_a_piece_a_lower_troop_wears(self):
        # Only x_chest_lord_a is unused, and a level 26 sergeant wears it: priced under the level 31
        # guard's chest the template has, so the swap would be a soldier piece for a lower one.
        templates = {'x_bat_template_medium_a': _tmpl('x', {'Body': 'x_chest_lord_b'}),
                     'x_bat_template_medium_b': _tmpl('x', {'Body': 'x_chest_lord_c'}),
                     'x_bat_template_medium_c': _tmpl('x', {'Body': 'x_noble_chest_a'}),
                     'x_bat_template_medium_d': _tmpl('x', {'Body': 'x_chest_elite_a'})}
        changes, notes = self._plan(templates, self._users(*templates))
        self.assertEqual(changes, [])
        self.assertTrue(any('x_bat_template_medium_d' in n and 'x_chest_elite_a' in n for n in notes), notes)

    def test_a_template_in_a_lord_set_of_its_own_keeps_it(self):
        templates = {'x_bat_template_medium_a': _tmpl('x', {'Body': 'x_chest_lord_c', 'Head': 'x_helm_heavy_a'})}
        self.assertEqual(self._plan(templates, self._users(*templates))[0], [])

    def test_when_the_pool_runs_out_a_shared_body_stays_and_is_reported(self):
        rids = ['x_bat_template_medium_%s' % c for c in 'abcdef']
        templates = {rid: _tmpl('x', {'Body': 'x_chest_lord_a'}) for rid in rids}
        changes, notes = self._plan(templates, self._users(*rids))
        bodies = [c['new'] for c in changes if c['slot'] == 'Body']
        self.assertEqual(sorted(bodies), ['x_chest_lord_b', 'x_chest_lord_c', 'x_noble_chest_a'])
        self.assertTrue(any('reuse' in n for n in notes))

    def test_a_template_of_another_race_takes_only_pieces_its_race_is_seen_in(self):
        templates = {'x_bat_template_medium_a': _tmpl('x', {'Body': 'x_chest_light_a'})}
        lords = [_lord('lord_o', 'x', 'x_bat_template_medium_a', race='orc')]
        changes, notes = self._plan(templates, lords)
        self.assertEqual(changes, [])
        self.assertTrue(any('orc' in n for n in notes))

    def test_the_other_slots_take_an_unused_piece_before_a_second_use_of_the_family_match(self):
        # Two templates take the two noble chests; one noble helmet and two plain lord helmets are in the
        # pool. The second template takes a plain helmet nobody wears, not the noble one again.
        items = dict(ITEMS, x_noble_chest_b=_it('Body', b=42), x_noble_helmet_a=_it('Head', h=34))
        templates = {'x_bat_template_medium_a': _tmpl('x', {'Body': 'x_chest_light_a'}),
                     'x_bat_template_medium_b': _tmpl('x', {'Body': 'x_chest_light_a'}),
                     'x_bat_template_medium_c': _tmpl('x', {'Body': 'x_chest_lord_b'}),
                     'x_bat_template_medium_d': _tmpl('x', {'Body': 'x_chest_lord_c'})}
        changes, _ = lk.plan_assignments(templates, self._users(*templates), TROOPS, items, tolerance=False)
        self.assertEqual(sorted(c['new'] for c in changes if c['slot'] == 'Body'),
                         ['x_noble_chest_a', 'x_noble_chest_b'])
        heads = [c['new'] for c in changes if c['slot'] == 'Head']
        self.assertEqual(len(heads), 2)
        self.assertEqual(len(set(heads)), 2, changes)

    def test_a_named_roster_is_never_planned(self):
        templates = {'faramir_bat_equipment': _tmpl('x', {'Body': 'x_chest_light_a'})}
        self.assertEqual(self._plan(templates, [_lord('lord_f', 'x', 'faramir_bat_equipment')])[0], [])


def _y_world(**extra_items):
    """Culture y: two lord chests and one lord helmet in the pool, elite troop helmets of 30 and 32, and a
    vanilla helmet (no Armory folder) that sets the head reference at 40, as Harad's vanilla chest does."""
    items = {
        'y_chest_lord_a': _it('Body', folder='yf', b=40), 'y_chest_lord_b': _it('Body', folder='yf', b=38),
        'y_helm_lord_a': _it('Head', folder='yf', h=36),
        'y_helm_elite_a': _it('Head', folder='yf', h=30), 'y_helm_elite_b': _it('Head', folder='yf', h=32),
        'y_chest_elite_a': _it('Body', folder='yf', b=40), 'y_chest_light_a': _it('Body', folder='yf', b=6),
        'v_vanilla_helm': _it('Head', folder=None, h=40),
    }
    items.update(extra_items)
    troops = {
        'y_recruit': _troop(6, 'y', {'Body': 'y_chest_light_a'}),
        'y_guard': _troop(31, 'y', {'Head': 'y_helm_elite_a', 'Body': 'y_chest_elite_a'}),
        'y_veteran': _troop(36, 'y', {'Head': 'y_helm_elite_b', 'Body': 'y_chest_elite_a'}),
        'y_captain': _troop(33, 'y', {'Head': 'v_vanilla_helm', 'Body': 'y_chest_elite_a'}),
    }
    templates = {
        'y_bat_template_medium_a': _tmpl('y', {'Body': 'y_chest_lord_a', 'Head': 'y_helm_lord_a'}),
        'y_bat_template_medium_b': _tmpl('y', {'Body': 'y_chest_lord_b'}),
        'y_bat_template_medium_c': _tmpl('y', {'Body': 'y_chest_light_a'}),
    }
    lords = [_lord('lord_y%d' % i, 'y', rid) for i, rid in enumerate(templates)]
    return items, troops, templates, lords


class ToleranceAssignment(unittest.TestCase):
    def _plan(self, **extra):
        items, troops, templates, lords = _y_world(**extra)
        changes, notes = lk.plan_assignments(templates, lords, troops, items)
        return {(c['template'], c['slot']): (c['new'], c['rule']) for c in changes}, notes

    def test_a_part_inside_the_tolerance_is_never_a_reason_to_change_a_piece(self):
        # Template _a: head 36 against 40 is 10 percent under, not more.
        got, _ = self._plan()
        self.assertFalse([k for k in got if k[0] == 'y_bat_template_medium_a'])

    def test_an_exhausted_pool_falls_to_distinct_elite_troop_pieces_closest_to_the_reference(self):
        got, _ = self._plan()
        self.assertEqual(got[('y_bat_template_medium_b', 'Head')][0], 'y_helm_elite_b')
        self.assertTrue(got[('y_bat_template_medium_b', 'Head')][1].startswith('b'))
        # _c takes the other elite helmet (distinct before reuse) and an elite chest for its recruit's.
        self.assertEqual(got[('y_bat_template_medium_c', 'Head')][0], 'y_helm_elite_a')
        self.assertEqual(got[('y_bat_template_medium_c', 'Body')][0], 'y_chest_elite_a')

    def test_a_vanilla_piece_is_never_put_on_a_lord_template(self):
        got, _ = self._plan()
        self.assertNotIn('v_vanilla_helm', [v[0] for v in got.values()])

    def test_an_unused_pool_piece_comes_before_an_elite_troop_piece(self):
        got, _ = self._plan(y_helm_lord_b=_it('Head', folder='yf', h=37))
        self.assertEqual(got[('y_bat_template_medium_b', 'Head')], ('y_helm_lord_b', 'a: unused lord piece'))

    def test_a_swap_that_lowers_another_part_is_refused_and_listed(self):
        # The recruit's chest carries leg 15 that the elite chest lacks, so the Body stays.
        got, notes = self._plan(y_chest_light_a=_it('Body', folder='yf', b=6, l=15))
        self.assertNotIn(('y_bat_template_medium_c', 'Body'), got)
        self.assertTrue(any(n.startswith('(c)') and 'y_bat_template_medium_c' in n and 'Body' in n for n in notes),
                        notes)

    def test_a_swap_never_takes_a_part_over_the_limit(self):
        # The only stronger helmet (50) is over 40 + 5; the champion wears it in one set of two.
        items, troops, templates, lords = _y_world(y_helm_elite_c=_it('Head', folder='yf', h=50))
        troops['y_champion'] = _troop(40, 'y', {'Head': 'y_helm_elite_c'}, {})
        for t in ('y_guard', 'y_veteran'):
            troops[t]['sets'][0].pop('Head')
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        self.assertNotIn('y_helm_elite_c', [c['new'] for c in changes])

    def test_a_swap_never_takes_a_lord_of_another_culture_on_the_template_over_his_own_limit(self):
        # Bluecraig lords wear goblin templates: a 42 helmet is inside y's 40 + 5 but over z's 30 + 5.
        def plan(with_z):
            items, troops, templates, lords = _y_world(y_helm_elite_c=_it('Head', folder='yf', h=42),
                                                       z_helm=_it('Head', folder='zf', h=30))
            troops['y_champion'] = _troop(40, 'y', {'Head': 'y_helm_elite_c'}, {})
            for t in ('y_guard', 'y_veteran'):
                troops[t]['sets'][0].pop('Head')
            troops['z_guard'] = _troop(31, 'z', {'Head': 'z_helm'})
            if with_z:
                lords.append(_lord('lord_z', 'z', 'y_bat_template_medium_b'))
            changes, _ = lk.plan_assignments(templates, lords, troops, items)
            return {(c['template'], c['slot']): c['new'] for c in changes}
        self.assertEqual(plan(False).get(('y_bat_template_medium_b', 'Head')), 'y_helm_elite_c')
        self.assertNotEqual(plan(True).get(('y_bat_template_medium_b', 'Head')), 'y_helm_elite_c')

    def test_an_elite_piece_counts_when_its_wearer_is_off_the_reference_list(self):
        # Harad's own heavy pieces are worn only by its mumak riders, whom the reference leaves out.
        items, troops, templates, lords = _y_world(y_helm_rider=_it('Head', folder='yf', h=38))
        for t in ('y_guard', 'y_veteran'):
            troops[t]['sets'][0].pop('Head')
        wearers = dict(troops, y_rider=_troop(51, 'y', {'Head': 'y_helm_rider'}))
        changes, _ = lk.plan_assignments(templates, lords, troops, items, elite_wearers=wearers)
        got = {(c['template'], c['slot']): (c['new'], c['rule']) for c in changes}
        self.assertEqual(got[('y_bat_template_medium_b', 'Head')][0], 'y_helm_rider')
        self.assertTrue(got[('y_bat_template_medium_b', 'Head')][1].startswith('b'))

    def test_an_elite_piece_no_swap_can_take_does_not_block_a_reuse(self):
        # The champion's 50 helmet (one set of two) is over the limit, so it is no candidate; _d reuses
        # a 30 or 32 helmet.
        items, troops, templates, lords = _y_world(y_helm_elite_c=_it('Head', folder='yf', h=50))
        troops['y_champion'] = _troop(40, 'y', {'Head': 'y_helm_elite_c'}, {})
        templates['y_bat_template_medium_d'] = _tmpl('y', {'Body': 'y_chest_light_a'})
        lords.append(_lord('lord_yd', 'y', 'y_bat_template_medium_d'))
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        got = {(c['template'], c['slot']): (c['new'], c['rule']) for c in changes}
        self.assertIn(got.get(('y_bat_template_medium_d', 'Head'), (None,))[0], ('y_helm_elite_a', 'y_helm_elite_b'))

    def test_a_reuse_goes_ahead_when_the_own_piece_is_more_than_25_percent_under(self):
        # Mirkwood: _d's own scout chest (8) is distinct but 80 percent under 40, so it takes the elite
        # chest _c also takes.
        items, troops, templates, lords = _y_world(y_chest_scout=_it('Body', folder='yf', b=8))
        troops['y_scout'] = _troop(31, 'y', {'Body': 'y_chest_scout'})
        templates['y_bat_template_medium_d'] = _tmpl('y', {'Body': 'y_chest_scout'})
        lords.append(_lord('lord_yd', 'y', 'y_bat_template_medium_d'))
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        got = {(c['template'], c['slot']): c['new'] for c in changes}
        self.assertEqual(got.get(('y_bat_template_medium_d', 'Body')), 'y_chest_elite_a')

    def test_a_khamul_piece_stays_on_its_template(self):
        # The maintainer keeps the Khamul pieces on the Rhun lords, short or not.
        items, troops, templates, lords = _y_world(sk_dg_khml_helmet_inf_lord_a=_it('Head', folder='yf', h=30),
                                                   y_helm_lord_b=_it('Head', folder='yf', h=37))
        templates['y_bat_template_medium_b']['sets'][0]['Head'] = 'sk_dg_khml_helmet_inf_lord_a'
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        self.assertNotIn(('y_bat_template_medium_b', 'Head'), {(c['template'], c['slot']) for c in changes})


    def test_inside_25_percent_the_own_piece_stays_and_is_listed_rather_than_reused(self):
        # _d's own helmet (31 against 40) is 22.5 percent under: short, but a reuse of the 32 helmet _b
        # takes would cost a look for 1 point, so _d keeps it. At 29 (27.5 percent) the reuse goes ahead.
        def plan(head):
            items, troops, templates, lords = _y_world(y_helm_own_a=_it('Head', folder='yf', h=head))
            troops['y_page'] = _troop(6, 'y', {'Head': 'y_helm_own_a'})
            templates['y_bat_template_medium_d'] = _tmpl('y', {'Body': 'y_chest_lord_a', 'Head': 'y_helm_own_a'})
            lords.append(_lord('lord_yd', 'y', 'y_bat_template_medium_d'))
            changes, notes = lk.plan_assignments(templates, lords, troops, items)
            return {(c['template'], c['slot']): c['new'] for c in changes}, notes
        got, notes = plan(31)
        self.assertNotIn(('y_bat_template_medium_d', 'Head'), got)
        self.assertTrue(any(n.startswith('(c)') and 'y_bat_template_medium_d' in n and 'Head' in n for n in notes))
        got, _ = plan(29)
        self.assertIn(got.get(('y_bat_template_medium_d', 'Head')), ('y_helm_elite_a', 'y_helm_elite_b'))

    def test_rule_b_takes_a_stem_sibling_or_an_unworn_piece_of_the_folder_before_any_reuse(self):
        # Harad: the elite helmet's unworn sibling (_c) and an unworn hood of the folder join rule (b),
        # so three short templates wear three different helmets before any reuse.
        items, troops, templates, lords = _y_world(y_helm_elite_c=_it('Head', folder='yf', h=35),
                                                   y_hood_spare_a=_it('Head', folder='yf', h=31))
        templates['y_bat_template_medium_d'] = _tmpl('y', {'Body': 'y_chest_light_a'})
        lords.append(_lord('lord_yd', 'y', 'y_bat_template_medium_d'))
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        heads = {c['template']: c['new'] for c in changes if c['slot'] == 'Head'}
        self.assertEqual(heads['y_bat_template_medium_b'], 'y_helm_elite_c')
        self.assertEqual(len(set(heads.values())), len(heads), heads)

    def test_templates_that_share_a_body_differ_in_their_other_slots(self):
        # _c and _d both take the elite chest (a reuse); both wear the same gauntlets, so one of them
        # takes the other elite gauntlets.
        items, troops, templates, lords = _y_world(y_glove_elite_a=_it('Gloves', folder='yf', a=5),
                                                   y_glove_elite_b=_it('Gloves', folder='yf', a=6))
        troops['y_guard']['sets'][0]['Gloves'] = 'y_glove_elite_a'
        troops['y_veteran']['sets'][0]['Gloves'] = 'y_glove_elite_b'
        templates['y_bat_template_medium_c']['sets'][0]['Gloves'] = 'y_glove_elite_a'
        templates['y_bat_template_medium_d'] = _tmpl('y', {'Body': 'y_chest_light_a', 'Gloves': 'y_glove_elite_a'})
        lords.append(_lord('lord_yd', 'y', 'y_bat_template_medium_d'))
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        sim = lk.simulate({r: t['sets'] for r, t in templates.items()}, changes)
        c, d = sim['y_bat_template_medium_c'][0], sim['y_bat_template_medium_d'][0]
        self.assertEqual(c['Body'], d['Body'])
        self.assertNotEqual(c['Gloves'], d['Gloves'])
        self.assertNotEqual(c.get('Head'), d.get('Head'))

    def test_a_new_body_brings_the_cape_its_wearers_wear_with_it(self):
        # The guard's pauldron is arm only, so the body reference stays at 40.
        items, troops, templates, lords = _y_world(y_pauld_elite_a=_it('Cape', folder='yf', a=2),
                                                   y_cape_plain_a=_it('Cape', folder='yf'))
        troops['y_guard']['sets'][0]['Cape'] = 'y_pauld_elite_a'
        troops['y_recruit']['sets'][0]['Cape'] = 'y_cape_plain_a'
        templates['y_bat_template_medium_c']['sets'][0]['Cape'] = 'y_cape_plain_a'
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        got = {(c['template'], c['slot']): c['new'] for c in changes}
        self.assertEqual(got[('y_bat_template_medium_c', 'Body')], 'y_chest_elite_a')
        self.assertEqual(got[('y_bat_template_medium_c', 'Cape')], 'y_pauld_elite_a')

    def test_a_swap_must_also_hold_on_the_planned_values(self):
        # The restat would set the elite chest to body 4, under the recruit chest's 6: refused.
        items, troops, templates, lords = _y_world()
        planned = {k: dict(v, stats=dict(v['stats'])) for k, v in items.items()}
        planned['y_chest_elite_a']['stats']['body_armor'] = 4
        changes, _ = lk.plan_assignments(templates, lords, troops, items, planned=planned)
        self.assertNotIn(('y_bat_template_medium_c', 'Body'), {(c['template'], c['slot']) for c in changes})

    def test_a_body_and_cape_pair_is_one_family_in_one_colour_or_worn_together(self):
        # A line's chainmail and its pauldron are one look (one family starts the other); a second line
        # of the culture, or a silver chest over plain pauldrons, is not, unless a troop wears them so.
        self.assertTrue(lk.same_look('dunland_caerdh_chainmail_lord_a', 'dunland_caerdh_pauldron_elite_a'))
        self.assertFalse(lk.same_look('dunland_wulf_scalemail_lord_a', 'dunland_caerdh_pauldron_elite_a'))
        self.assertFalse(lk.same_look('mkwd_inf3_chest_silver', 'mkwd_inf3_pauldrons'))
        self.assertTrue(lk.same_look('mkwd_inf3_chest_silver', 'mkwd_inf3_pauldrons_silver'))
        self.assertTrue(lk.same_look('cts_rohan_captain_chest', 'roh_nbl_gorget', {'roh_nbl_gorget': 2}))

    def test_the_player_start_kit_is_never_a_rule_b_piece(self):
        # No troop wears the starter helmet, but it is the player's start kit, not a lord piece.
        items, troops, templates, lords = _y_world(starter_y_helm_a=_it('Head', folder='yf', h=35))
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        self.assertNotIn('starter_y_helm_a', {c['new'] for c in changes})

    def test_a_reuse_must_raise_the_part_by_at_least_three_points(self):
        # Head reference 44: _b, _c and _d take three different helmets (32, 31, 30), each more than 25
        # percent under. Trading _c's 31 or _d's 30 for the 32 _b wears buys 1 or 2 points for a look,
        # under the 3 points a gap must reach to count as short, so each keeps its own.
        items, troops, templates, lords = _y_world(y_helm_spare_a=_it('Head', folder='yf', h=31),
                                                   y_chest_own_d=_it('Body', folder='yf', b=40),
                                                   v_vanilla_helm=_it('Head', folder=None, h=44))
        templates['y_bat_template_medium_d'] = _tmpl('y', {'Body': 'y_chest_own_d'})
        lords.append(_lord('lord_yd', 'y', 'y_bat_template_medium_d'))
        changes, notes = lk.plan_assignments(templates, lords, troops, items)
        heads = {c['template']: c['new'] for c in changes if c['slot'] == 'Head'}
        self.assertEqual(len(set(heads.values())), 3, heads)
        self.assertTrue(any('less than 3 points' in n for n in notes), notes)

    def test_a_fresh_piece_inside_25_percent_never_climbs_into_a_reuse(self):
        # Head reference 40: _b, _c and _d take three different helmets (32, 31, 30). _d's 30 is 25 percent
        # under, not more, so it keeps that piece rather than take the 32 _b wears.
        items, troops, templates, lords = _y_world(y_helm_spare_a=_it('Head', folder='yf', h=31),
                                                   y_chest_own_d=_it('Body', folder='yf', b=40))
        templates['y_bat_template_medium_d'] = _tmpl('y', {'Body': 'y_chest_own_d'})
        lords.append(_lord('lord_yd', 'y', 'y_bat_template_medium_d'))
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        heads = {c['template']: c['new'] for c in changes if c['slot'] == 'Head'}
        self.assertEqual(len(set(heads.values())), len(heads), heads)
        self.assertFalse([c for c in changes if 'reuse' in c['rule'] and c['slot'] == 'Head'], changes)

    def _shared_body_world(self, **extra):
        # _e wears the elite chest from the start; _b takes it as a reuse. Both wear the same pauldron.
        items, troops, templates, lords = _y_world(y_pauld_elite_a=_it('Cape', folder='yf', b=3), **extra)
        del items['y_chest_lord_b']
        troops['y_guard']['sets'][0]['Cape'] = 'y_pauld_elite_a'
        del templates['y_bat_template_medium_c']
        templates['y_bat_template_medium_b'] = _tmpl('y', {'Body': 'y_chest_light_a', 'Cape': 'y_pauld_elite_a'})
        templates['y_bat_template_medium_e'] = _tmpl('y', {'Body': 'y_chest_elite_a', 'Cape': 'y_pauld_elite_a'})
        lords = [_lord('lord_y%d' % i, 'y', rid) for i, rid in enumerate(sorted(templates))]
        changes, _ = lk.plan_assignments(templates, lords, troops, items)
        return {(c['template'], c['slot']): (c['new'], c['rule']) for c in changes}

    def test_on_a_shared_body_the_template_that_reused_it_takes_the_other_piece(self):
        got = self._shared_body_world(y_pauld_elite_b=_it('Cape', folder='yf', b=3))
        self.assertIn('reuse', got[('y_bat_template_medium_b', 'Body')][1])
        self.assertEqual(got.get(('y_bat_template_medium_b', 'Cape'), (None,))[0], 'y_pauld_elite_b', got)
        self.assertNotIn(('y_bat_template_medium_e', 'Cape'), got)

    def test_a_shared_body_never_takes_a_cape_of_another_look_for_variety(self):
        got = self._shared_body_world(y_wolf_cape_a=_it('Cape', folder='yf', b=3))
        self.assertFalse([k for k in got if k[1] == 'Cape'], got)


class FallbackSync(unittest.TestCase):
    def test_a_fallback_roster_mirrors_the_armour_of_its_template_a_and_keeps_its_weapons(self):
        rosters = {'taom_x_lord_battle_male': {'file': 'lt.xml', 'culture': 'x',
                                               'sets': [{'Item0': 'sword', 'Body': 'x_chest_light_a',
                                                         'Cape': 'x_pauld_elite_a'}]}}
        templates = {'x_bat_template_medium_a': [{'Item0': 'axe', 'Body': 'x_chest_lord_a', 'Head': 'x_helmet_lord_a'}]}
        changes = lk.plan_fallback_sync(rosters, templates, {'x': 'x'})
        self.assertEqual(sorted((c['slot'], c['old'], c['new']) for c in changes),
                         [('Body', 'x_chest_light_a', 'x_chest_lord_a'), ('Cape', 'x_pauld_elite_a', None),
                          ('Head', None, 'x_helmet_lord_a')])


class WriteChanges(unittest.TestCase):
    XML = ('<?xml version="1.0" encoding="utf-8"?>\r\n<EquipmentRosters>\r\n'
           '\t<EquipmentRoster id="x_bat_template_medium_a" culture="Culture.x">\r\n'
           '\t\t<EquipmentSet>\r\n'
           '\t\t\t<Equipment slot="Item0" id="Item.sword" />\r\n'
           '\t\t\t<Equipment slot="Body" id="Item.x_chest_light_a" />\r\n'
           '\t\t\t<Equipment slot="Cape" id="Item.x_pauld_elite_a" />\r\n'
           '\t\t</EquipmentSet>\r\n'
           '\t</EquipmentRoster>\r\n'
           '</EquipmentRosters>\r\n')

    def _file(self, tmp, text=None):
        path = Path(tmp) / 'taom_equipment_sets_x.xml'
        path.write_bytes(codecs.BOM_UTF8 + (text or self.XML).encode('utf-8'))
        return path

    def _c(self, path, slot, old, new, roster='x_bat_template_medium_a'):
        return {'roster': roster, 'file': str(path), 'set': 0, 'slot': slot, 'old': old, 'new': new}

    def test_replace_append_and_delete_keep_bom_and_crlf(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = self._file(tmp)
            self.assertEqual(lk.write_changes([self._c(path, 'Body', 'x_chest_light_a', 'x_chest_lord_a'),
                                               self._c(path, 'Head', None, 'x_helmet_lord_a'),
                                               self._c(path, 'Cape', 'x_pauld_elite_a', None)]), 1)
            raw = path.read_bytes()
            self.assertTrue(raw.startswith(codecs.BOM_UTF8))
            text = raw[3:].decode('utf-8')
            self.assertNotIn('\n', text.replace('\r\n', ''))
            self.assertEqual(text, self.XML.replace(
                '\t\t\t<Equipment slot="Body" id="Item.x_chest_light_a" />\r\n'
                '\t\t\t<Equipment slot="Cape" id="Item.x_pauld_elite_a" />\r\n',
                '\t\t\t<Equipment slot="Body" id="Item.x_chest_lord_a" />\r\n'
                '\t\t\t<Equipment slot="Head" id="Item.x_helmet_lord_a" />\r\n'))

    def test_a_slot_that_changed_on_disk_refuses_the_whole_write(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = self._file(tmp)
            before = path.read_bytes()
            with self.assertRaises(RuntimeError):
                lk.write_changes([self._c(path, 'Body', 'x_chest_elite_a', 'x_chest_lord_a')])
            self.assertEqual(path.read_bytes(), before)

    def test_an_append_onto_a_slot_already_filled_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = self._file(tmp)
            before = path.read_bytes()
            with self.assertRaises(RuntimeError):
                lk.write_changes([self._c(path, 'Cape', None, 'x_pauld_lord_b')])
            self.assertEqual(path.read_bytes(), before)

    def test_a_write_the_reader_would_see_elsewhere_is_refused(self):
        # An empty first set the regex writer skips but ElementTree also skips; a self-closed set
        # before it shifts nothing. A set holding only a comment is where they could part.
        text = self.XML.replace('\t\t<EquipmentSet>\r\n\t\t\t<Equipment slot="Item0"',
                                '\t\t<EquipmentSet><!-- <Equipment slot="Body" id="Item.x" /> --></EquipmentSet>\r\n'
                                '\t\t<EquipmentSet>\r\n\t\t\t<Equipment slot="Item0"', 1)
        with tempfile.TemporaryDirectory() as tmp:
            path = self._file(tmp, text)
            before = path.read_bytes()
            with self.assertRaises(RuntimeError):
                lk.write_changes([self._c(path, 'Body', 'x_chest_light_a', 'x_chest_lord_a')])
            self.assertEqual(path.read_bytes(), before)

    def test_a_missing_roster_refuses_the_write(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = self._file(tmp)
            with self.assertRaises(RuntimeError):
                lk.write_changes([self._c(path, 'Body', 'x', 'y', roster='nope')])


class CommandLine(unittest.TestCase):
    def test_main_refuses_a_modules_folder_with_no_armory(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(lk.main(['--game-modules', tmp]), 2)

    def test_a_crash_exits_3_not_1(self):
        with mock.patch.object(lk, 'main', side_effect=ValueError('boom')):
            self.assertEqual(lk.entry([]), 3)

    def test_promoted_clones_follow_rosters_kept(self):
        clones, generated = lk.promoted_clones(['gondor_bat_template_medium_a', 'gondor_bat_template_medium_e'])
        self.assertEqual(sorted(clones['gondor_bat_template_medium_a']),
                         ['arthedain_bat_template_medium_a', 'cardolan_bat_template_medium_a',
                          'rhudaur_bat_template_medium_a'])
        self.assertNotIn('gondor_bat_template_medium_e', clones)
        self.assertIn('taom_equipment_sets_arthedain.xml', generated)

    def test_an_item_mapped_clone_wears_its_own_line(self):
        # Arthedain and Rhudaur map Gondor pieces to Arnor ones; Cardolan wears the Gondor pieces as they are.
        st = [{'Head': 'sk_gd_lam_nob_helmet_lord_a', 'Item0': 'wm_gondor_sword_a01'}]
        self.assertEqual(lk.clone_sets('arthedain_bat_template_medium_a', st)[0]['Head'],
                         'sk_ar_art_helmet_noble_prince_b')
        self.assertEqual(lk.clone_sets('cardolan_bat_template_medium_a', st), st)

    def test_creature_markers_match_the_validator(self):
        self.assertEqual(tuple(lk.CREATURE_MARKERS), tuple(ts.Validator._WORN_ARMOUR_CREATURE_MARKERS))


class ShippedFallbackRosters(unittest.TestCase):
    """The shipped taom_<culture>_(lord|ruler)_battle_<sex> rosters must wear their source template
    _a's armour, so a hand edit to either side shows up here."""

    def drift(self, directory=None):
        import derive_armor_tiers as dat
        templates = dat.load_battle_templates(directory)
        out = []
        for rid in sorted(templates):
            m = lk.FALLBACK_ROSTER_RE.match(rid)
            if not m:
                continue
            fam = {**lk.FALLBACK_SOURCES, **lk.PROMOTED_FALLBACKS}.get(m.group('culture'))
            self.assertIsNotNone(fam, 'no source template family for %s' % rid)
            a = templates['%s_bat_template_medium_a' % fam][0]
            for slot in lk.ARMOUR_SLOTS:
                if templates[rid][0].get(slot) != a.get(slot):
                    out.append((rid, slot, templates[rid][0].get(slot), a.get(slot)))
        return out

    def test_an_item_mapped_clone_lord_wears_no_armour_of_its_source_culture(self):
        # A promoter target with an item_map wears its own culture's kit (the Arnor kit for
        # Arthedain and Rhudaur); a source lord piece the map lacks would reach the clone unmapped.
        import derive_armor_tiers as dat
        import promote_borrowed_cultures as pbc
        templates = dat.load_battle_templates()
        clones, _ = lk.promoted_clones([r for r in templates if lk.GENERIC_TEMPLATE_RE.match(r)])
        unmapped = set()
        for target, cfg in sorted(pbc.TARGETS.items()):
            if not cfg.get('item_map'):
                continue
            prefix = os.path.commonprefix(sorted(cfg['item_map']))   # the source line's id prefix
            self.assertTrue(prefix, target)
            for rid in sorted(r for r, cl in clones.items() if any(c.startswith(target + '_') for c in cl)):
                for st in templates[rid]:
                    for slot in lk.ARMOUR_SLOTS:
                        iid = st.get(slot)
                        if iid and iid.startswith(prefix) and iid not in cfg['item_map']:
                            unmapped.add((target, iid))
        self.assertEqual(sorted(unmapped), [],
                         'add these source pieces to the target item_map in promote_borrowed_cultures.py')

    def test_every_fallback_and_ruler_roster_wears_its_template_a_armour(self):
        drift = self.drift()
        self.assertEqual(drift, [], 'python tools/fix_lord_kits.py --apply, then the promoter for the '
                                    'promoted cultures: ' + '; '.join(map(str, drift[:20])))


class LoadLords(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / 'ModuleData'
        (self.md / 'characters').mkdir(parents=True)

    def tearDown(self):
        self._tmp.cleanup()

    def test_heroes_with_occupation_lord_in_characters_and_the_lords_stylesheet(self):
        (self.md / 'characters' / 'lords.xml').write_text(
            '<NPCCharacters>'
            '<NPCCharacter id="lord_a" is_hero="true" occupation="Lord" culture="Culture.x" race="orc">'
            '<Equipments><EquipmentSet id="x_bat_template_medium_a" />'
            '<EquipmentSet id="x_civ_template_a" equipmentType="Civilian" />'
            '<equipment slot="Head" id="Item.x_helm_elite_a" /></Equipments></NPCCharacter>'
            '<NPCCharacter id="wanderer_a" is_hero="true" occupation="Wanderer" culture="Culture.x">'
            '<Equipments><EquipmentSet id="x_bat_template_medium_a" /></Equipments></NPCCharacter>'
            '</NPCCharacters>', encoding='utf-8')
        (self.md / 'lords.xslt').write_text(
            '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
            '<xsl:template match="NPCCharacter[@id=\'lord_1_1\']"><xsl:copy>\n'
            '<xsl:attribute name="id">lord_1_1</xsl:attribute>\n'
            '<xsl:attribute name="culture">Culture.empire</xsl:attribute>\n'
            '<xsl:attribute name="occupation">Lord</xsl:attribute>\n'
            '<Equipments><EquipmentSet id="dunland_bat_template_medium_a" /></Equipments>\n'
            '</xsl:copy></xsl:template></xsl:stylesheet>', encoding='utf-8')
        lords = {l['id']: l for l in lk.load_lords(self.md)}
        self.assertEqual(sorted(lords), ['lord_1_1', 'lord_a'])
        self.assertEqual((lords['lord_a']['race'], lords['lord_a']['overrides']), ('orc', {'Head': 'x_helm_elite_a'}))
        self.assertEqual((lords['lord_1_1']['culture'], lords['lord_1_1']['line']), ('empire', 3))

    def test_a_file_that_does_not_parse_is_reported_not_skipped_in_silence(self):
        (self.md / 'characters' / 'lords.xml').write_text('<NPCCharacters><broken', encoding='utf-8')
        failures = []
        self.assertEqual(lk.load_lords(self.md, failures), [])
        self.assertEqual([Path(f).name for f, _e in failures], ['lords.xml'])


if __name__ == '__main__':
    unittest.main()
