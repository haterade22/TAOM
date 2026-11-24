#!/usr/bin/env python3
"""Unit tests for tools/generate_armour_classes.py (docs/features/armour-acquisition.md).

Run:  python -m unittest tools.tests.test_generate_armour_classes
  or:  python tools/tests/test_generate_armour_classes.py

THE CONTRACT
------------
Every character armour piece in the live Armory gets exactly one acquisition class, which decides
where a player can get it: light and medium are sold and looted freely; heavy and elite are sold
only by a town whose armoury (Barracks) level allows them; lord pieces are forged or earned, never
sold; named hero kit is never sold, looted or upgraded. Precedence: named kit first, then the
artist's `_lord` token (a lord variant is a lord variant whoever wears it), then `_civ` kit, then
derive_armor_tiers' tier (the lowest battle wearer's band, else the id token), then the band whose
kingdom-cap target is nearest the piece's own primary stat.

Each upgradable piece also names the piece the armoury turns it into: the nearest class above it
that has a candidate, looked for first in the piece's own tokened line (same stem and slot, the
same suffix preferred) and then among same-kit, same-slot pieces sharing the longest id prefix. A
kit is rebalance_armor.kingdom_key's kingdom, or the Armory folder when the culture carries no
kingdom cap, since a folder can hold more than one kingdom's kit (mordor, rhun).
The rendered table is sorted and carries no timestamp, so an unchanged Armory regenerates
byte-identically and the drift check can compare text.
"""
import os
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_armour_classes as gac  # noqa: E402


def piece(item_id, cls, folder='gondor', slot='body'):
    return gac.Piece(item_id=item_id, cls=cls, folder=folder, slot=slot)


class Classify(unittest.TestCase):
    def test_named_hero_kit_beats_every_token(self):
        # rebalance_armor.is_excluded: a HERO_NAMES display name marks hero kit.
        self.assertEqual(gac.classify('faramir_armor_lord_a', "Faramir's Armour", 'elite', None), 'named')

    def test_arthedains_crown_is_named_kit(self):
        # Mike, 2026-10-08: never sold or looted; earned on Arthedain's lord's ladder.
        self.assertEqual(gac.classify('sk_ar_art_crown_king_a', "[Arnor] King's Crown", None, 'light'), 'named')

    def test_lord_token_beats_the_roster_band(self):
        # derive_armor_tiers bands a lord chest worn from level 41 as elite; the artist named it lord.
        self.assertEqual(gac.classify('sk_gb_uruk_chest_lord_c', 'Uruk Lord Chest III', 'elite', None), 'lord')

    def test_civilian_kit_is_civilian(self):
        self.assertEqual(gac.classify('sk_gd_civ_heavy_coat', 'Heavy Coat', 'civilian', None), 'civilian')

    def test_derived_tier_is_used_when_no_lord_or_civilian_token(self):
        self.assertEqual(gac.classify('sk_rh_helmet_a01', 'Rohan Helmet', 'heavy', 'light'), 'heavy')

    def test_stat_band_classes_an_unworn_untokened_piece(self):
        self.assertEqual(gac.classify('sk_rh_helmet_b07', 'Rohan Helmet', None, 'elite'), 'elite')

    def test_no_signal_at_all_falls_back_to_light(self):
        # Fail open: a piece nothing can class stays freely available rather than vanishing.
        self.assertEqual(gac.classify('mystery_piece', 'Mystery', None, None), 'light')

    def test_a_lord_armour_variant_is_named_kit(self):
        # #1333: a variant is a lord template's piece; never sold, looted, forged, stripped or upgraded.
        self.assertEqual(gac.classify('taom_lord_mkwd_inf3_chest', "[Mirkwood] Lord's Infantry Chestplate I",
                                      'lord', None), 'named')

    def test_no_piece_upgrades_into_a_lord_armour_variant(self):
        records = {
            'mkwd_inf3_chest_silver': {'tier': 'elite', 'slot': 'body', 'culture': 'mirkwood', 'name': 'Silver',
                                       'armour': (0, 48, 8, 19)},
            'taom_lord_mkwd_inf3_chest': {'tier': 'lord', 'slot': 'body', 'culture': 'mirkwood', 'name': 'Lord',
                                          'armour': (0, 51, 0, 20)},
        }
        rows = gac.build_rows(records, targets=lambda *a: {})
        self.assertEqual(rows['taom_lord_mkwd_inf3_chest'], ('named', None))
        self.assertIsNone(rows['mkwd_inf3_chest_silver'][1])

    def test_khamul_line_is_troop_kit_not_named(self):
        # The Dol Guldur "Khamul ..." line is named after its captain, not worn by him.
        self.assertEqual(gac.classify('sk_dg_khml_chest_heavy_a', 'Khamul Guard Chest', 'heavy', None), 'heavy')

    def test_an_unworn_mallorn_piece_is_classed_one_band_above_its_id_word(self):
        # generate_lothlorien_armor prices Mallorn one band up (ML_BAND_UP), so the Mallorn Medium
        # Helmet carries head armour 40, the heavy target; classed medium it sold with no armoury.
        records = {
            'sk_ll_ml_helmet_med_a': {'tier': 'medium', 'tierSource': 'id-keyword', 'slot': 'head',
                                      'culture': 'lothlorien', 'name': 'Mallorn Medium Helmet'},
            'sk_ll_ml_helmet_heavy_b': {'tier': 'heavy', 'tierSource': 'id-keyword', 'slot': 'head',
                                        'culture': 'lothlorien', 'name': 'Mallorn Heavy Helmet'},
        }
        rows = gac.build_rows(records, targets=lambda *a: {})
        self.assertEqual(rows['sk_ll_ml_helmet_med_a'][0], 'heavy')
        self.assertEqual(rows['sk_ll_ml_helmet_heavy_b'][0], 'elite')

    def test_the_mallorn_step_up_never_reaches_lord_or_moves_a_worn_or_other_kit_piece(self):
        records = {
            'sk_ll_ml_helmet_elite_a': {'tier': 'elite', 'tierSource': 'id-keyword', 'slot': 'head',
                                        'culture': 'lothlorien', 'name': 'Mallorn Elite Helmet'},
            'sk_ll_ml_helmet_heavy_a': {'tier': 'elite', 'tierSource': 'roster(L46)', 'slot': 'head',
                                        'culture': 'lothlorien', 'name': 'Mallorn Heavy Helmet'},
            'sk_ll_gl_helmet_med_b': {'tier': 'medium', 'tierSource': 'id-keyword', 'slot': 'head',
                                      'culture': 'lothlorien', 'name': 'Galadhrim Medium Helmet'},
        }
        rows = gac.build_rows(records, targets=lambda *a: {})
        self.assertEqual(rows['sk_ll_ml_helmet_elite_a'][0], 'elite')
        self.assertEqual(rows['sk_ll_ml_helmet_heavy_a'][0], 'elite')
        self.assertEqual(rows['sk_ll_gl_helmet_med_b'][0], 'medium')


class StatBand(unittest.TestCase):
    def test_nearest_target_wins(self):
        targets = {'light': 20, 'medium': 32, 'heavy': 42, 'elite': 50}
        self.assertEqual(gac.stat_band(40, targets), 'heavy')
        self.assertEqual(gac.stat_band(49, targets), 'elite')

    def test_tie_goes_to_the_lower_band(self):
        self.assertEqual(gac.stat_band(26, {'light': 20, 'medium': 32}), 'light')

    def test_missing_primary_or_targets_is_none(self):
        self.assertIsNone(gac.stat_band(None, {'light': 20}))
        self.assertIsNone(gac.stat_band(30, {}))


class PickNext(unittest.TestCase):
    def test_same_line_next_class_with_same_suffix(self):
        pieces = [
            piece('sk_gd_fount_chest_med_a', 'medium'),
            piece('sk_gd_fount_chest_heavy_b', 'heavy'),
            piece('sk_gd_fount_chest_heavy_a', 'heavy'),
        ]
        self.assertEqual(gac.pick_next(pieces[0], pieces), 'sk_gd_fount_chest_heavy_a')

    def test_line_class_comes_from_the_table_not_the_token(self):
        # The `_heavy_a` sibling is worn at level 46 and so classed elite: it is the medium's
        # next only if nothing is classed heavy; here a heavy-classed line piece exists.
        pieces = [
            piece('sk_gd_x_chest_med_a', 'medium'),
            piece('sk_gd_x_chest_heavy_a', 'elite'),
            piece('sk_gd_x_chest_elite_a', 'heavy'),
        ]
        self.assertEqual(gac.pick_next(pieces[0], pieces), 'sk_gd_x_chest_elite_a')

    def test_skips_a_missing_class_to_the_nearest_above(self):
        pieces = [
            piece('sk_gd_y_helmet_light_a', 'light', slot='head'),
            piece('sk_gd_y_helmet_elite_a', 'elite', slot='head'),
        ]
        self.assertEqual(gac.pick_next(pieces[0], pieces), 'sk_gd_y_helmet_elite_a')

    def test_other_slots_are_never_candidates(self):
        pieces = [
            piece('sk_gd_z_chest_light_a', 'light', slot='body'),
            piece('sk_gd_z_chest_medium_a', 'medium', slot='leg'),
        ]
        self.assertIsNone(gac.pick_next(pieces[0], pieces))

    def test_untokened_piece_upgrades_to_the_longest_shared_prefix_in_its_culture_and_slot(self):
        # 'rohan_helmet_rider_b' (dale) ties 'rohan_helmet_rider_c' (rohan) for longest shared
        # prefix (19 chars) and sorts before it ('b' < 'c'): without the folder/kit guard the
        # naive longest-prefix-then-alphabetical pick would return the WRONG (dale) candidate,
        # which the old fixture (with only a 'd'-suffixed dale sibling) could never expose.
        pieces = [
            piece('rohan_helmet_rider_a', 'light', folder='rohan', slot='head'),
            piece('rohan_helmet_guard_b', 'medium', folder='rohan', slot='head'),
            piece('rohan_helmet_rider_b', 'medium', folder='dale', slot='head'),
            piece('rohan_helmet_rider_c', 'medium', folder='rohan', slot='head'),
            piece('rohan_helmet_rider_d', 'medium', folder='dale', slot='head'),
        ]
        self.assertEqual(gac.pick_next(pieces[0], pieces), 'rohan_helmet_rider_c')

    def test_lord_named_and_civilian_have_no_next(self):
        pieces = [
            piece('sk_a_chest_lord_a', 'lord'),
            piece('faramir_armor', 'named'),
            piece('sk_a_civ_coat', 'civilian'),
            piece('sk_a_chest_elite_a', 'elite'),
        ]
        for p in pieces[:3]:
            self.assertIsNone(gac.pick_next(p, pieces), p.item_id)

    def test_fallback_never_crosses_a_kingdom_line_inside_a_shared_folder(self):
        # The rhun Armory folder holds two kits at two different kingdom caps: the Rhun
        # dragon-riders (sk_rh_drag_*, kingdom_key 'rhun') and Dol Guldur's Khamul line
        # (sk_dg_*, kingdom_key 'dol_guldur'). Sharing a folder is not sharing a kit, so the
        # untokened-line fallback must not link across them (Mike, #609 follow-up).
        pieces = [
            piece('sk_dg_khml_grvs_light_a', 'light', folder='rhun', slot='leg'),
            piece('sk_rh_drag_grvs_plate_med_a', 'medium', folder='rhun', slot='leg'),
        ]
        self.assertIsNone(gac.pick_next(pieces[0], pieces))

    def test_named_and_civilian_are_never_targets(self):
        pieces = [
            piece('sk_b_chest_elite_a', 'elite'),
            piece('sk_b_chest_lord_x', 'named'),
            piece('sk_b_chest_civ_a', 'civilian'),
        ]
        self.assertIsNone(gac.pick_next(pieces[0], pieces))

    def test_elite_upgrades_to_lord(self):
        pieces = [
            piece('sk_c_chest_elite_a', 'elite'),
            piece('sk_c_chest_lord_a', 'lord'),
        ]
        self.assertEqual(gac.pick_next(pieces[0], pieces), 'sk_c_chest_lord_a')

    def test_only_an_elite_piece_upgrades_to_lord(self):
        pieces = [
            piece('sk_d_chest_heavy_a', 'heavy'),
            piece('sk_d_chest_lord_a', 'lord'),
        ]
        self.assertIsNone(gac.pick_next(pieces[0], pieces))

    def test_a_successor_no_better_on_any_armour_value_and_worse_on_one_is_never_offered(self):
        # The Iron Hills cape shape the Ered Luin review found: the elite piece of the same line
        # carries body 42 and arm 34 against the heavy one's 42 and 45, so the paid upgrade would
        # only make the wearer easier to hit. The armoury offers no upgrade rather than that.
        # Values are (head, body, arm, leg).
        heavy = gac.Piece('sk_dwarf_iron_pauldron_heavy_cape_c', 'heavy', 'iron_hills', 'cape', armour=(0, 42, 45, 0))
        worse = gac.Piece('sk_dwarf_iron_pauldron_elite_cape_c', 'elite', 'iron_hills', 'cape', armour=(0, 42, 34, 0))
        self.assertIsNone(gac.pick_next(heavy, [heavy, worse]))

    def test_a_dominated_medium_successor_of_a_light_piece_is_never_offered(self):
        # Light to medium is a paid upgrade too (armour_acquisition_config.xml), so the rule covers
        # every class below lord, not only heavy and elite.
        light = gac.Piece('sk_j_chest_light_a', 'light', 'gondor', 'body', armour=(0, 20, 6, 4))
        worse = gac.Piece('sk_j_chest_med_a', 'medium', 'gondor', 'body', armour=(0, 20, 5, 4))
        self.assertIsNone(gac.pick_next(light, [light, worse]))

    def test_a_dominated_in_line_successor_gives_way_to_a_same_kit_piece(self):
        heavy = gac.Piece('sk_e_pauld_heavy_c', 'heavy', 'erebor', 'cape', armour=(0, 42, 34, 0))
        in_line_worse = gac.Piece('sk_e_pauld_elite_a', 'elite', 'erebor', 'cape', armour=(0, 42, 33, 0))
        kit_better = gac.Piece('sk_e_pauldron_cape_elite_b', 'elite', 'erebor', 'cape', armour=(0, 42, 38, 0))
        self.assertEqual(gac.pick_next(heavy, [heavy, in_line_worse, kit_better]), 'sk_e_pauldron_cape_elite_b')

    def test_a_successor_better_on_one_value_and_worse_on_another_is_still_offered(self):
        # A trade (more body, less arm) is a design choice the rule leaves alone: only a successor
        # that gains nothing anywhere is dropped.
        heavy = gac.Piece('sk_f_chest_heavy_a', 'heavy', 'gondor', 'body', armour=(0, 40, 10, 5))
        trade = gac.Piece('sk_f_chest_elite_a', 'elite', 'gondor', 'body', armour=(0, 48, 6, 5))
        self.assertEqual(gac.pick_next(heavy, [heavy, trade]), 'sk_f_chest_elite_a')

    def test_a_successor_worse_on_the_first_value_but_better_on_a_later_one_is_still_offered(self):
        # Pins value-by-value comparison: an ordered tuple compare would reject this trade.
        heavy = gac.Piece('sk_h_chest_heavy_a', 'heavy', 'gondor', 'body', armour=(0, 40, 10, 5))
        trade = gac.Piece('sk_h_chest_elite_a', 'elite', 'gondor', 'body', armour=(0, 39, 20, 5))
        self.assertEqual(gac.pick_next(heavy, [heavy, trade]), 'sk_h_chest_elite_a')

    def test_the_elite_to_lord_forge_path_ignores_the_values(self):
        # Lord kit is forged from an elite piece for its look, and the restat leaves many lord
        # pieces a few arm points under their elite source (Rhun's Loke line, Rivendell, Gondor):
        # the rule guards every upgrade below lord, not that forge path.
        elite = gac.Piece('sk_rh_loke_chest_elite_a', 'elite', 'rhun', 'body', armour=(0, 57, 21, 29))
        lord = gac.Piece('sk_rh_loke_chest_lord_a', 'lord', 'rhun', 'body', armour=(0, 57, 20, 29))
        self.assertEqual(gac.pick_next(elite, [elite, lord]), 'sk_rh_loke_chest_lord_a')

    def test_an_equal_successor_is_still_offered(self):
        heavy = gac.Piece('sk_g_chest_heavy_a', 'heavy', 'gondor', 'body', armour=(0, 40, 10, 5))
        same = gac.Piece('sk_g_chest_elite_a', 'elite', 'gondor', 'body', armour=(0, 40, 10, 5))
        self.assertEqual(gac.pick_next(heavy, [heavy, same]), 'sk_g_chest_elite_a')

    def test_a_piece_with_unknown_armour_keeps_the_class_and_id_rule(self):
        pieces = [
            piece('sk_gd_fount_chest_med_a', 'medium'),
            gac.Piece('sk_gd_fount_chest_heavy_a', 'heavy', 'gondor', 'body', armour=(0, 1, 0, 0)),
        ]
        self.assertEqual(gac.pick_next(pieces[0], pieces), 'sk_gd_fount_chest_heavy_a')


class RenderAndDiff(unittest.TestCase):
    ROWS = {
        'b_piece': ('heavy', 'c_piece'),
        'a_piece': ('light', None),
        'c_piece': ('elite', None),
    }

    def test_render_is_sorted_and_stable(self):
        text = gac.render(self.ROWS)
        self.assertEqual(text, gac.render(dict(reversed(list(self.ROWS.items())))))
        self.assertLess(text.index('a_piece'), text.index('b_piece'))
        self.assertNotIn('generated_at', text)

    def test_render_round_trips_through_parse(self):
        self.assertEqual(gac.parse(gac.render(self.ROWS)), self.ROWS)

    def test_next_attribute_is_omitted_when_there_is_none(self):
        text = gac.render({'solo': ('lord', None)})
        self.assertIn('<Item id="solo" class="lord" />', text)

    def test_diff_counts_added_removed_and_changed(self):
        old = {'a': ('light', None), 'b': ('heavy', 'c'), 'gone': ('medium', None)}
        new = {'a': ('light', None), 'b': ('elite', 'c'), 'fresh': ('lord', None)}
        d = gac.diff(old, new)
        self.assertEqual(d['added'], ['fresh'])
        self.assertEqual(d['removed'], ['gone'])
        self.assertEqual(d['changed'], ['b'])

    def test_diff_of_identical_tables_is_empty(self):
        d = gac.diff(self.ROWS, dict(self.ROWS))
        self.assertEqual((d['added'], d['removed'], d['changed']), ([], [], []))


class ReadCommittedAndDrift(unittest.TestCase):
    """read_committed and drift_summary must survive a merge conflict left in armour_classes.xml:
    describe it as stale text, never crash, so --check reports STALE and --apply can still
    regenerate over it (deep-review MEDIUM finding)."""

    def test_read_committed_resolves_table_path_at_call_time(self):
        # A default argument is bound once at import time and would miss a later monkeypatch of
        # TABLE_PATH; `path = path or TABLE_PATH` reads the CURRENT global on every no-arg call.
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            text = gac.render({'a': ('light', None)})
            with open(path, 'wb') as fh:
                fh.write(text.encode('utf-8'))
            with mock.patch.object(gac, 'TABLE_PATH', path):
                self.assertEqual(gac.read_committed(), text)

    def test_read_committed_missing_file_is_none(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertIsNone(gac.read_committed(os.path.join(tmp, 'nope.xml')))

    def test_read_committed_normalises_crlf(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            with open(path, 'wb') as fh:
                fh.write(b'<a>\r\n<b/>\r\n</a>')
            self.assertNotIn('\r', gac.read_committed(path))

    def test_read_committed_never_raises_on_undecodable_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            with open(path, 'wb') as fh:
                fh.write(b'<a>\xff\xfe not valid utf-8</a>')
            text = gac.read_committed(path)  # must not raise
            self.assertIsInstance(text, str)

    def test_drift_summary_names_a_missing_table(self):
        drift = gac.drift_summary(None, {'a': ('light', None), 'b': ('medium', None)})
        self.assertIn('the table is missing', drift)
        self.assertIn('2 pieces', drift)

    def test_drift_summary_names_a_formatting_only_difference(self):
        rows = {'a': ('light', None)}
        # Same content, different whitespace: parses identically but the text differs.
        reformatted = gac.render(rows).replace('  <Item', '<Item')
        self.assertEqual(gac.drift_summary(reformatted, rows), 'the text differs (formatting only)')

    def test_conflicted_table_reads_as_stale_not_a_crash(self):
        conflicted = '<<<<<<< HEAD\n<ArmourClasses></ArmourClasses>\n=======\n'
        drift = gac.drift_summary(conflicted, {'a': ('light', None)})
        self.assertIsNotNone(drift)
        self.assertIn('does not parse', drift)
        self.assertIn('1 pieces', drift)

    def test_apply_regenerates_over_a_conflicted_table(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            with open(path, 'wb') as fh:
                fh.write(b'<<<<<<< HEAD\n<ArmourClasses></ArmourClasses>\n')
            with mock.patch.object(gac, 'TABLE_PATH', path), \
                 mock.patch.object(gac, 'generate', return_value={'a': ('light', None)}):
                self.assertEqual(gac.main(['--apply']), 0)
            gac.parse(gac.read_committed(path))  # must parse cleanly now

    def test_check_on_a_conflicted_table_is_stale_and_exits_1(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            with open(path, 'wb') as fh:
                fh.write(b'<<<<<<< HEAD\n<ArmourClasses></ArmourClasses>\n')
            with mock.patch.object(gac, 'TABLE_PATH', path), \
                 mock.patch.object(gac, 'generate', return_value={'a': ('light', None)}):
                self.assertEqual(gac.main(['--check']), 1)


class CliContract(unittest.TestCase):
    """main()'s three modes: the dry run never writes, --apply writes the rendered table, and
    --check's exit code is the drift verdict (0 current, 1 stale, 2 no Armory)."""

    def test_dry_run_writes_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            with mock.patch.object(gac, 'TABLE_PATH', path), \
                 mock.patch.object(gac, 'generate', return_value={'a': ('light', None)}):
                self.assertEqual(gac.main([]), 0)
            self.assertFalse(os.path.exists(path))

    def test_apply_writes_the_rendered_table(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            rows = {'a': ('light', None)}
            with mock.patch.object(gac, 'TABLE_PATH', path), \
                 mock.patch.object(gac, 'generate', return_value=rows):
                self.assertEqual(gac.main(['--apply']), 0)
            with open(path, 'rb') as fh:
                self.assertEqual(fh.read().decode('utf-8'), gac.render(rows))

    def test_check_exits_0_when_current(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            rows = {'a': ('light', None)}
            with open(path, 'wb') as fh:
                fh.write(gac.render(rows).encode('utf-8'))
            with mock.patch.object(gac, 'TABLE_PATH', path), \
                 mock.patch.object(gac, 'generate', return_value=rows):
                self.assertEqual(gac.main(['--check']), 0)

    def test_check_exits_1_when_stale(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'armour_classes.xml')
            with open(path, 'wb') as fh:
                fh.write(gac.render({'a': ('light', None)}).encode('utf-8'))
            with mock.patch.object(gac, 'TABLE_PATH', path), \
                 mock.patch.object(gac, 'generate', return_value={'a': ('heavy', None)}):
                self.assertEqual(gac.main(['--check']), 1)

    def test_check_exits_2_without_the_armory(self):
        with mock.patch.object(gac, 'generate', return_value=None):
            self.assertEqual(gac.main(['--check']), 2)


class StatTargets(unittest.TestCase):
    """calculate_stats never raises for an unmapped culture (it falls back to the neutral
    CULTURAL_MODS default); the try/except in stat_targets was dead defence for a failure mode
    that cannot happen, and it would have hidden a real one."""

    def test_every_band_is_covered_for_an_uncapped_culture(self):
        targets = gac.stat_targets('mystery_item', 'body', 'made_up_culture')
        self.assertEqual(set(targets), set(gac.BANDS))

    def test_a_raising_calculate_stats_propagates(self):
        with mock.patch.object(gac.ra, 'calculate_stats', side_effect=RuntimeError('boom')):
            with self.assertRaises(RuntimeError):
                gac.stat_targets('mystery_item', 'body', 'gondor')


class BuildRows(unittest.TestCase):
    def test_build_rows_classes_every_record_and_links_upgrades(self):
        records = {
            'sk_gd_q_chest_light_a': {'culture': 'gondor', 'slot': 'body', 'name': 'Q Chest', 'tier': 'light', 'current': 20},
            'sk_gd_q_chest_med_a': {'culture': 'gondor', 'slot': 'body', 'name': 'Q Chest', 'tier': 'medium', 'current': 32},
            'sk_gd_q_chest_lord_a': {'culture': 'gondor', 'slot': 'body', 'name': 'Q Chest', 'tier': 'elite', 'current': 57},
            'rohan_cap_a': {'culture': 'rohan', 'slot': 'head', 'name': 'Cap', 'tier': None, 'current': 14},
        }
        rows = gac.build_rows(records, targets=lambda item_id, slot, folder: {'light': 14, 'medium': 23})
        self.assertEqual(rows['sk_gd_q_chest_light_a'], ('light', 'sk_gd_q_chest_med_a'))
        # Lord kit is forged from an elite piece only, so a line with no heavy or elite stops here.
        self.assertEqual(rows['sk_gd_q_chest_med_a'], ('medium', None))
        self.assertEqual(rows['sk_gd_q_chest_lord_a'], ('lord', None))
        self.assertEqual(rows['rohan_cap_a'], ('light', None))


class ArmoryIndexType(unittest.TestCase):
    def test_a_piece_that_is_not_character_armour_is_left_out_of_the_index(self):
        # Harad's art drop filed four camel bardings (Type="HorseHarness") in body_armors.xml; the
        # slot file alone put them in the index as body armour and 12 heavy chests "upgraded" into
        # one (2026-10-14). The filter lives in the index so every consumer gets it.
        import derive_armor_tiers as dat
        items = (
            '<Items>'
            '<Item id="sk_hr_st_chest_heavy_a" name="{=a}Chest" Type="BodyArmor" weight="10">'
            '<ItemComponent><Armor body_armor="40" /></ItemComponent></Item>'
            '<Item id="sk_hr_pz_camel_bard_elite_a" name="{=b}Barding" Type="HorseHarness" weight="20">'
            '<ItemComponent><Armor body_armor="50" /></ItemComponent></Item>'
            '</Items>')
        with tempfile.TemporaryDirectory() as tmp:
            os.makedirs(os.path.join(tmp, 'harad'))
            with open(os.path.join(tmp, 'harad', 'body_armors.xml'), 'w', encoding='utf-8') as fh:
                fh.write(items)
            with mock.patch.object(gac.ra, 'ARMORY_DIR', tmp):
                index, _ = dat.build_armory_index()
        self.assertEqual(sorted(index), ['sk_hr_st_chest_heavy_a'])
        self.assertEqual(index['sk_hr_st_chest_heavy_a']['type'], 'BodyArmor')


@unittest.skipUnless(os.path.isdir(gac.ra.ARMORY_DIR), "needs the LOTRLOME_Armory install; skipped, never faked")
class CommittedTableAgainstTheArmory(unittest.TestCase):
    def test_every_row_and_every_next_is_character_armour_of_one_type(self):
        import derive_armor_tiers as dat
        index, _ = dat.build_armory_index()
        rows = gac.parse(gac.read_committed())
        bad = []
        for item_id, (_cls, nxt) in rows.items():
            kind = index.get(item_id, {}).get('type')
            if kind not in gac.ra.CHARACTER_ARMOUR_TYPES:
                bad.append(f'{item_id} is {kind}')
            elif nxt and index.get(nxt, {}).get('type') != kind:
                bad.append(f'{item_id} ({kind}) -> {nxt} ({index.get(nxt, {}).get("type")})')
        self.assertEqual(bad, [], 'run python tools/generate_armour_classes.py --apply')



class MallornBandUpMatchesTheLothlorienGenerator(unittest.TestCase):
    """The class table raises an unworn Mallorn piece one band, as generate_lothlorien_armor.py prices it.
    The two tables are kept apart on purpose (the class table stops at elite, never lord), so this pins
    them together below lord."""

    def test_every_band_below_lord_steps_up_alike(self):
        import generate_lothlorien_armor as lothlorien
        for band, raised in gac.MALLORN_BAND_UP.items():
            self.assertEqual(lothlorien.ML_BAND_UP[band], raised, band)
        self.assertEqual(set(gac.MALLORN_BAND_UP), {'light', 'medium', 'heavy'})

if __name__ == '__main__':
    unittest.main()
