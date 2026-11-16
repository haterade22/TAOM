#!/usr/bin/env python3
"""Tests for tools/generate_lord_armour_variants.py (lord armour variants, #1333).

Run:  python -m unittest tools.tests.test_generate_lord_armour_variants

THE CONTRACT
------------
A lord armour variant is a new TAOM item that copies one Armory armour piece (mesh, cover flags,
material, weight, every other attribute and child) under the id `taom_lord_<source>`, with
is_merchandise="false", its own name key and a name that marks it a lord's piece, and armour at the
lord band of the source's kingdom line: the primary is rebalance_armor's lord value, a stat the
source declares with a share above 0 follows that share (kingdom_secondary), and a row secondary
the curve gives the slot (a chest's leg, a cape's arm) is written even where the source has none.
A variant is made only for a piece a troop wears and the curve prices under the lord band
(fix_lord_kits.is_short); a piece already at lord values is worn as it is. Only a generic lord
template or a fallback or ruler roster may name a variant. derive_armor_tiers reads the variants
beside the Armory, in the source's folder, so the class table and the restat map see them.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import derive_armor_tiers as dat  # noqa: E402
import generate_lord_armour_variants as glv  # noqa: E402
import rebalance_armor as ra  # noqa: E402

SOURCE = (
    '    <Item\r\n'
    '        id="mkwd_inf3_chest"\r\n'
    '        name="{=aom_mkwd_inf3_chest_name}[Mirkwood] Infantry Chestplate I"\r\n'
    '        subtype="body_armor"\r\n'
    '        mesh="mkwd_inf3_chest"\r\n'
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
    '                material_type="Chainmail" />\r\n'
    '        </ItemComponent>\r\n'
    '        <Flags\r\n'
    '            UseTeamColor="true" />\r\n'
    '    </Item>')


def _lord(slot, line):
    return ra.cap_value(ra.KINGDOM_CAPS[line], slot, 'lord')


class LordValues(unittest.TestCase):
    def test_the_primary_is_the_lord_band_and_the_row_secondary_is_added(self):
        values = glv.lord_values('body', 'mirkwood', 'mkwd_inf3_chest', {'body_armor': 8, 'arm_armor': 0, 'leg_armor': 0})
        row = ra.calculate_stats('lord', 'body', 'mirkwood', item_id='mkwd_inf3_chest')
        self.assertEqual(values['body_armor'], _lord('body', 'mirkwood'))
        # A chest's row carries leg armour: a light chest at the lord band gets it.
        self.assertEqual(values['leg_armor'], row['leg_armor'])
        self.assertGreater(values['leg_armor'], 0)
        # A declared stat with no share and no row value stays 0.
        self.assertEqual(values['arm_armor'], 0)

    def test_a_declared_share_follows_the_source(self):
        values = glv.lord_values('body', 'harad', 'sk_hr_st_chest_cav_heavy_b', {'body_armor': 27, 'arm_armor': 8})
        primary = _lord('body', 'harad')
        self.assertEqual(values['body_armor'], primary)
        self.assertEqual(values['arm_armor'], ra.kingdom_secondary('body', 'arm_armor', 'lord', primary, 8 / 27))
        self.assertEqual(values['leg_armor'],
                         ra.calculate_stats('lord', 'body', 'harad', item_id='sk_hr_st_chest_cav_heavy_b')['leg_armor'])

    def test_a_cape_with_no_arm_share_takes_the_row_arm(self):
        values = glv.lord_values('shoulder', 'mirkwood', 'mkwd_inf3_pauldrons_bronze', {'body_armor': 5, 'arm_armor': 0})
        row = ra.calculate_stats('lord', 'shoulder', 'mirkwood', item_id='mkwd_inf3_pauldrons_bronze')
        self.assertEqual(values, {'body_armor': row['body_armor'], 'arm_armor': row['arm_armor']})

    def test_a_leg_piece_carries_its_primary_only(self):
        self.assertEqual(glv.lord_values('leg', 'harad', 'sk_hr_st_grvs_light_a', {'leg_armor': 3}),
                         {'leg_armor': _lord('leg', 'harad')})

    def test_a_folder_with_no_kingdom_cap_is_refused(self):
        with self.assertRaises(glv.VariantError):
            glv.lord_values('body', 'nowhere', 'x_chest', {'body_armor': 8})


SILVER = {'arm_armor': 8 / 48, 'leg_armor': 19 / 48}


class ReferenceShares(unittest.TestCase):
    """A variant takes the secondary shares of the piece that sets its culture's reference on the
    slot's part (the round 2 rule of #1333): a lord does not lose a part because his look came back."""
    ITEMS = {'silver': {'stats': {'body_armor': 48, 'arm_armor': 8, 'leg_armor': 19}},
             'gold': {'stats': {'body_armor': 50, 'arm_armor': 15, 'leg_armor': 0}},
             'cape': {'stats': {'body_armor': 13, 'arm_armor': 11}}}

    def test_the_reference_troops_lowest_piece_in_the_slot_sets_the_shares(self):
        troop = {'sets': [{'Body': 'gold'}, {'Body': 'silver', 'Cape': 'cape'}]}
        self.assertEqual(glv.reference_shares(troop, self.ITEMS, 'body'), SILVER)
        self.assertEqual(glv.reference_shares(troop, self.ITEMS, 'shoulder'), {'arm_armor': 11 / 13})

    def test_no_piece_in_the_slot_or_no_primary_gives_no_shares(self):
        self.assertEqual(glv.reference_shares({'sets': [{'Body': 'silver'}]}, self.ITEMS, 'leg'), {})
        self.assertEqual(glv.reference_shares({'sets': [{'Body': 'nothing'}]}, self.ITEMS, 'body'), {})
        self.assertEqual(glv.reference_shares(None, self.ITEMS, 'body'), {})

    def test_the_culture_reference_troop_on_the_slots_part_is_read(self):
        troops = {'t_ref': {'sets': [{'Body': 'silver', 'Cape': 'cape'}]}, 't_other': {'sets': [{'Body': 'gold'}]}}
        ref = {'head': (50, 't_other'), 'body': (61, 't_ref'), 'arm': (43, 't_other'), 'leg': (41, 't_other')}
        with mock.patch.object(glv.lk, 'culture_reference', return_value=ref):
            shares = glv.culture_shares(troops, self.ITEMS, 'mirkwood')
        self.assertEqual(shares['body'], SILVER)
        self.assertEqual(shares['shoulder'], {'arm_armor': 11 / 13})
        with mock.patch.object(glv.lk, 'culture_reference', return_value=None):
            self.assertEqual(glv.culture_shares(troops, self.ITEMS, 'nobody'), {})

    def test_a_body_variant_takes_the_reference_arm_share_where_its_own_has_none(self):
        values = glv.lord_values('body', 'mirkwood', 'mkwd_inf3_chest', {'body_armor': 8, 'arm_armor': 0, 'leg_armor': 0},
                                 shares=SILVER)
        primary = _lord('body', 'mirkwood')
        row = ra.calculate_stats('lord', 'body', 'mirkwood', item_id='mkwd_inf3_chest')
        self.assertEqual(values['arm_armor'], ra.kingdom_secondary('body', 'arm_armor', 'lord', primary, 8 / 48))
        self.assertGreater(values['arm_armor'], 0)
        self.assertEqual(values['leg_armor'],
                         max(row['leg_armor'], ra.kingdom_secondary('body', 'leg_armor', 'lord', primary, 19 / 48)))

    def test_a_larger_own_share_wins_and_a_larger_reference_share_wins(self):
        primary = _lord('body', 'harad')
        own = glv.lord_values('body', 'harad', 'sk_hr_st_chest_cav_heavy_b', {'body_armor': 27, 'arm_armor': 8},
                              shares={'arm_armor': 0.05})
        self.assertEqual(own['arm_armor'], ra.kingdom_secondary('body', 'arm_armor', 'lord', primary, 8 / 27))
        ref = glv.lord_values('body', 'harad', 'sk_hr_st_chest_cav_heavy_b', {'body_armor': 27, 'arm_armor': 1},
                              shares={'arm_armor': 16 / 51})
        self.assertEqual(ref['arm_armor'], ra.kingdom_secondary('body', 'arm_armor', 'lord', primary, 16 / 51))

    def test_a_reference_share_under_the_row_value_keeps_the_row_value(self):
        row = ra.calculate_stats('lord', 'body', 'harad', item_id='sk_hr_st_chest_inf_chest_light_a')
        values = glv.lord_values('body', 'harad', 'sk_hr_st_chest_inf_chest_light_a', {'body_armor': 6, 'arm_armor': 0},
                                 shares={'leg_armor': 0.01})
        self.assertEqual(values['leg_armor'], row['leg_armor'])


class Cap(unittest.TestCase):
    def test_a_cap_under_the_lord_band_lowers_the_primary(self):
        band = _lord('head', 'harad')
        values = glv.lord_values('head', 'harad', 'sk_hr_st_helmet_heavy_a', {'head_armor': 34}, cap=band - 1)
        self.assertEqual(values, {'head_armor': band - 1})

    def test_a_cap_that_does_not_bind_is_an_error(self):
        band = _lord('head', 'harad')
        with self.assertRaisesRegex(glv.VariantError, 'cap'):
            glv.lord_values('head', 'harad', 'sk_hr_st_helmet_heavy_a', {'head_armor': 34}, cap=band)

    def test_the_need_is_judged_against_the_capped_value(self):
        band = _lord('head', 'harad')
        with self.assertRaisesRegex(glv.VariantError, 'as it is'):
            glv.check_needed('sk_hr_st_helmet_heavy_a', 'head', 'harad', {'head_armor': 34}, {'anchorLevel': 26},
                             cap=35 if band > 35 else band - 1)

    def test_the_spec_cap_needs_a_whole_value_and_a_reason(self):
        ok = {'cultures': {'a': [{'source': 'x', 'reason': 'r', 'cap': {'value': 44, 'reason': 'the limit'}}]}}
        glv.validate_spec(ok)
        for bad in ({'value': 44}, {'value': 44, 'reason': ' '}, {'value': 0, 'reason': 'r'},
                    {'value': 44.5, 'reason': 'r'}, {'value': True, 'reason': 'r'}, 44, {'reason': 'r'}):
            with self.assertRaises(glv.VariantError, msg=repr(bad)):
                glv.validate_spec({'cultures': {'a': [{'source': 'x', 'reason': 'r', 'cap': bad}]}})


class NeedsAVariant(unittest.TestCase):
    WORN = {'anchorLevel': 11}

    def test_a_worn_piece_under_the_lord_band_needs_one(self):
        glv.check_needed('mkwd_inf3_chest', 'body', 'mirkwood', {'body_armor': 8}, self.WORN)

    def test_a_piece_at_lord_values_is_worn_as_it_is(self):
        with self.assertRaisesRegex(glv.VariantError, 'as it is'):
            glv.check_needed('mkwd_inf3_chest_silver', 'body', 'mirkwood', {'body_armor': 48}, {'anchorLevel': 41})

    def test_a_lord_line_piece_no_troop_wears_is_refused(self):
        # The restat restates an unworn lord-line piece a lord wears at the lord band
        # (rebalance_armor._roster_first_plan), so no variant is needed.
        self.assertTrue(ra.is_lord_kit('sk_hr_st_chest_lord_a'))
        for record in ({'anchorLevel': None}, None):
            with self.assertRaisesRegex(glv.VariantError, 'no troop'):
                glv.check_needed('sk_hr_st_chest_lord_a', 'body', 'harad', {'body_armor': 8}, record)

    def test_another_piece_no_troop_wears_may_have_one(self):
        # An unworn piece without a lord-line token a lord wears is only clamped (lowered) by the restat,
        # never raised, so a variant is its one route to the lord band (the Harad helmets, round 2).
        self.assertFalse(ra.is_lord_kit('sk_hr_st_helmet_heavy_d'))
        glv.check_needed('sk_hr_st_helmet_heavy_d', 'head', 'harad', {'head_armor': 34}, {'anchorLevel': None})
        glv.check_needed('sk_hr_st_helmet_heavy_d', 'head', 'harad', {'head_armor': 34}, None)


class Naming(unittest.TestCase):
    def test_the_lord_mark_follows_the_culture_tag(self):
        self.assertEqual(glv.lord_name('[Mirkwood] Infantry Chestplate I'), "[Mirkwood] Lord's Infantry Chestplate I")

    def test_a_name_with_no_tag_starts_with_the_mark(self):
        self.assertEqual(glv.lord_name('Palace Boots'), "Lord's Palace Boots")

    def test_the_tier_words_are_dropped_and_the_look_letter_kept(self):
        self.assertEqual(glv.lord_name('[Harwan] Infantry Light Chest A'), "[Harwan] Lord's Infantry Chest A")
        self.assertEqual(glv.lord_name('[Harwan] Infantry Med Pauldron A'), "[Harwan] Lord's Infantry Pauldron A")
        self.assertEqual(glv.lord_name('[Harwan] Heavy Helmet D'), "[Harwan] Lord's Helmet D")
        self.assertEqual(glv.lord_name('Medium elite Greaves'), "Lord's Greaves")
        # A tier word inside another word stays.
        self.assertEqual(glv.lord_name('[Gondor] Heavyset Medallion'), "[Gondor] Lord's Heavyset Medallion")

    def test_two_variants_never_share_a_name(self):
        names = glv.unique_names(["[H] Lord's Chest A", "[H] Lord's Chest A", "[H] Lord's Chest B",
                                  "[H] Lord's Greaves A", "[H] Lord's Greaves A", "[H] Lord's Greaves A"])
        self.assertEqual(names, ["[H] Lord's Chest A I", "[H] Lord's Chest A II", "[H] Lord's Chest B",
                                 "[H] Lord's Greaves A I", "[H] Lord's Greaves A II", "[H] Lord's Greaves A III"])

    def test_a_numbered_name_that_meets_a_plain_one_is_an_error(self):
        with self.assertRaises(glv.VariantError):
            glv.unique_names(["Chest A", "Chest A", "Chest A I"])

    def test_render_writes_the_name_it_is_given(self):
        out = glv.render_item(SOURCE, 'mkwd_inf3_chest', {'body_armor': 51}, 'plate', name="[Mirkwood] Lord's Chest II")
        self.assertIn('name="{=taom_lord_mkwd_inf3_chest_name}[Mirkwood] Lord\'s Chest II"', out)

    def test_the_id_and_the_key(self):
        self.assertEqual(glv.variant_id('mkwd_inf3_chest'), 'taom_lord_mkwd_inf3_chest')
        self.assertEqual(dat.lord_variant_source('taom_lord_mkwd_inf3_chest'), 'mkwd_inf3_chest')
        self.assertIsNone(dat.lord_variant_source('mkwd_inf3_chest'))


class RenderItem(unittest.TestCase):
    def setUp(self):
        self.out = glv.render_item(SOURCE, 'mkwd_inf3_chest', {'body_armor': 51, 'arm_armor': 0, 'leg_armor': 20},
                                   'plate')

    def test_id_name_key_and_merchandise(self):
        self.assertIn('id="taom_lord_mkwd_inf3_chest"', self.out)
        self.assertIn('name="{=taom_lord_mkwd_inf3_chest_name}[Mirkwood] Lord\'s Infantry Chestplate I"', self.out)
        self.assertIn('is_merchandise="false"', self.out)
        self.assertNotIn('is_merchandise="true"', self.out)
        self.assertNotIn('aom_mkwd_inf3_chest_name', self.out)

    def test_armour_written_and_a_missing_stat_inserted_beside_the_others(self):
        self.assertIn('body_armor="51"', self.out)
        self.assertIn('arm_armor="0"\r\n                leg_armor="20"\r\n', self.out)
        self.assertIn('modifier_group="plate"', self.out)

    def test_everything_else_is_the_source(self):
        for keep in ('mesh="mkwd_inf3_chest"', 'weight="5.2"', 'material_type="Chainmail"', 'covers_body="true"',
                     'has_gender_variations="false"', 'UseTeamColor="true"', 'culture="Culture.mirkwood"'):
            self.assertIn(keep, self.out)
        self.assertNotIn('\n', self.out.replace('\r\n', ''))

    def test_a_source_with_no_merchandise_flag_gets_one(self):
        out = glv.render_item(SOURCE.replace('        is_merchandise="true"\r\n', ''), 'mkwd_inf3_chest',
                              {'body_armor': 51, 'arm_armor': 0, 'leg_armor': 20}, 'plate')
        self.assertEqual(out.count('is_merchandise="false"'), 1)

    def test_the_result_parses_in_a_file(self):
        text = glv.render_file([('mkwd_inf3_chest', 'reason', self.out)], '\r\n')
        import xml.etree.ElementTree as ET
        root = ET.fromstring(text.encode('utf-8'))
        self.assertEqual([i.get('id') for i in root.iter('Item')], ['taom_lord_mkwd_inf3_chest'])
        self.assertNotIn('\n', text.replace('\r\n', ''))


class FindSource(unittest.TestCase):
    def test_a_commented_copy_is_skipped_and_the_live_item_found(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / 'mirkwood'
            folder.mkdir()
            body = ('<?xml version="1.0" encoding="utf-8"?>\r\n<Items>\r\n<!--\r\n'
                    + SOURCE.replace('body_armor="8"', 'body_armor="99"') + '\r\n-->\r\n' + SOURCE + '\r\n</Items>\r\n')
            (folder / 'body_armors.xml').write_bytes(body.encode('utf-8'))
            found = glv.find_source(tmp, 'mkwd_inf3_chest')
        self.assertEqual((found.folder, found.slot), ('mirkwood', 'body'))
        self.assertEqual(found.declared, {'body_armor': 8, 'arm_armor': 0})
        self.assertIn('body_armor="8"', found.xml)

    def test_a_missing_source_is_an_error(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(glv.VariantError):
                glv.find_source(tmp, 'nothing_here')

    def test_bytes_that_are_not_utf8_are_an_error_not_a_guess(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / 'mirkwood'
            folder.mkdir()
            (folder / 'body_armors.xml').write_bytes(b'<Items>\xff</Items>')
            with self.assertRaises(glv.VariantError):
                glv.find_source(tmp, 'mkwd_inf3_chest')


class Spec(unittest.TestCase):
    def test_a_source_listed_twice_or_without_a_reason_is_refused(self):
        with self.assertRaises(glv.VariantError):
            glv.validate_spec({'cultures': {'a': [{'source': 'x', 'reason': 'r'}], 'b': [{'source': 'x', 'reason': 'r'}]}})
        with self.assertRaises(glv.VariantError):
            glv.validate_spec({'cultures': {'a': [{'source': 'x', 'reason': ''}]}})

    def test_the_shipped_spec_is_valid(self):
        spec = glv.validate_spec(glv.load_spec())
        self.assertIn('mirkwood', spec['cultures'])
        self.assertIn('aserai', spec['cultures'])

    def test_the_variant_cultures_come_from_the_spec(self):
        spec = {'cultures': {'mirkwood': [{'source': 'mkwd_inf3_chest', 'reason': 'r'}]}}
        self.assertEqual(dat.lord_variant_cultures(spec), {'taom_lord_mkwd_inf3_chest': 'mirkwood'})


class SubModule(unittest.TestCase):
    TEXT = ('<Module>\r\n  <Xmls>\r\n    <XmlNode>\r\n      <XmlName id="Items" path="placeholder_weapons/ph_items"/>\r\n'
            '    </XmlNode>\r\n  </Xmls>\r\n</Module>\r\n')

    def test_the_node_goes_in_once_after_the_anchor_with_the_files_newlines(self):
        out = glv.submodule_text(self.TEXT)
        self.assertIn('path="lord_armour/lord_armour_variants"', out)
        self.assertLess(out.index('ph_items'), out.index('lord_armour_variants'))
        self.assertEqual(glv.submodule_text(out), out)
        self.assertNotIn('\n', out.replace('\r\n', ''))

    def test_a_missing_anchor_is_an_error(self):
        with self.assertRaises(glv.VariantError):
            glv.submodule_text('<Module></Module>')

    def test_majority_newline(self):
        self.assertEqual(glv.majority_newline('a\r\nb\r\nc\n'), '\r\n')
        self.assertEqual(glv.majority_newline('a\nb\nc\r\n'), '\n')


class Rosters(unittest.TestCase):
    def test_only_a_lord_template_or_a_fallback_roster_may_name_a_variant(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp)
            (md / 'equipmentsets').mkdir()
            (md / 'troops').mkdir()
            (md / 'equipmentsets' / 'taom_equipment_sets_x.xml').write_text(
                '<EquipmentRosters>\n'
                '<EquipmentRoster id="x_bat_template_medium_a"><EquipmentSet><Equipment slot="Body" id="Item.taom_lord_a" />'
                '</EquipmentSet></EquipmentRoster>\n'
                '<EquipmentRoster id="x_companion_kit"><EquipmentSet><Equipment slot="Body" id="Item.taom_lord_a" />'
                '</EquipmentSet></EquipmentRoster>\n</EquipmentRosters>\n', encoding='utf-8')
            (md / 'equipmentsets' / 'taom_lord_template_equipment.xml').write_text(
                '<EquipmentRosters><EquipmentRoster id="taom_x_lord_battle_male"><EquipmentSet>'
                '<Equipment slot="Body" id="Item.taom_lord_a" /></EquipmentSet></EquipmentRoster></EquipmentRosters>\n',
                encoding='utf-8')
            (md / 'troops' / 'troops_x.xml').write_text(
                '<NPCCharacters><NPCCharacter id="x_guard"><equipment slot="Body" id="Item.taom_lord_a" />'
                '</NPCCharacter></NPCCharacters>\n', encoding='utf-8')
            found = glv.roster_violations(md, {'taom_lord_a'})
        self.assertEqual(sorted((Path(f).name, r) for f, r, _ in found),
                         [('taom_equipment_sets_x.xml', 'x_companion_kit'), ('troops_x.xml', '')])


class SpecMembership(unittest.TestCase):
    """A variant is an id the spec lists, never any id with the prefix: taom_lord_intro_* are dialogue keys
    (F5 of the #1333 review)."""

    def test_an_unrelated_taom_lord_id_is_no_variant(self):
        import fix_lord_kits as lk
        import generate_armour_classes as gac
        for iid in ('taom_lord_x', 'taom_lord_intro_dale_1'):
            self.assertIsNone(dat.lord_variant_source(iid), iid)
            self.assertNotEqual(gac.classify(iid, 'X', 'heavy', 'heavy'), 'named', iid)
            # Its family is read from the id itself, not from the id without the prefix.
            self.assertNotEqual(lk._split(iid), lk._split(iid[len(dat.LORD_VARIANT_PREFIX):]), iid)
        self.assertEqual(lk._split('taom_lord_mkwd_inf3_chest'), lk._split('mkwd_inf3_chest'))

    def test_a_listed_variant_is_one(self):
        self.assertEqual(dat.lord_variant_source('taom_lord_mkwd_inf3_chest'), 'mkwd_inf3_chest')
        spec = {'cultures': {'c': [{'source': 'x', 'reason': 'r'}]}}
        self.assertEqual(dat.lord_variant_source('taom_lord_x', spec), 'x')
        self.assertIsNone(dat.lord_variant_source('taom_lord_y', spec))


class CapAgainstTheReference(unittest.TestCase):
    """A cap above the culture's reference on the slot's part plus fix_lord_kits.CEILING_MARGIN fails, so
    a changed reference cannot leave a stale cap (F4 of the #1333 review)."""
    REF = {'head': (39, 't'), 'body': (51, 't'), 'arm': (25, 't'), 'leg': (39, 't')}

    def test_a_cap_at_the_limit_passes_and_above_it_fails(self):
        glv.check_cap('x', 'head', 39 + glv.lk.CEILING_MARGIN, self.REF)
        with self.assertRaisesRegex(glv.VariantError, 'limit'):
            glv.check_cap('x', 'head', 40 + glv.lk.CEILING_MARGIN, self.REF)

    def test_a_cape_is_judged_on_body_and_no_reference_fails(self):
        with self.assertRaises(glv.VariantError):
            glv.check_cap('x', 'shoulder', 51 + glv.lk.CEILING_MARGIN + 1, self.REF)
        with self.assertRaisesRegex(glv.VariantError, 'reference'):
            glv.check_cap('x', 'head', 30, None)

    def test_no_cap_is_never_judged(self):
        glv.check_cap('x', 'head', None, None)


class Wearers(unittest.TestCase):
    """--check fails when a variant reaches a troop or a hero who is not a lord through a template by id,
    and when an xslt names one (F3 of the #1333 review)."""

    def _moduledata(self, tmp):
        md = Path(tmp)
        for sub in ('equipmentsets', 'characters', 'named_companions', 'troops'):
            (md / sub).mkdir()
        (md / 'equipmentsets' / 'taom_equipment_sets_x.xml').write_text(
            '<EquipmentRosters><EquipmentRoster id="x_bat_template_medium_a"><EquipmentSet>'
            '<Equipment slot="Body" id="Item.taom_lord_a" /></EquipmentSet></EquipmentRoster></EquipmentRosters>\n',
            encoding='utf-8')
        (md / 'characters' / 'lords.xml').write_text(
            '<NPCCharacters><NPCCharacter id="x_lord" is_hero="true" occupation="Lord"><Equipments>'
            '<EquipmentSet id="x_bat_template_medium_a" /></Equipments></NPCCharacter></NPCCharacters>\n',
            encoding='utf-8')
        return md

    def _violations(self, md):
        with mock.patch.object(dat, 'CHARACTERS_DIR', str(md / 'characters')), \
                mock.patch.object(dat, 'NAMED_COMPANIONS_DIR', str(md / 'named_companions')), \
                mock.patch.object(dat, 'WANDERERS_XML', str(md / 'taom_wanderers.xml')), \
                mock.patch.object(dat, 'LORDS_XSLT', str(md / 'lords.xslt')), \
                mock.patch.object(dat, 'EQUIPMENTSETS_DIR', str(md / 'equipmentsets')):
            return glv.wearer_violations(md, {'taom_lord_a'})

    def test_a_lord_wearing_the_template_is_clean(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(self._violations(self._moduledata(tmp)), [])

    def test_a_cultures_lord_or_rebellion_template_is_a_lord(self):
        # spc_<culture>_lord_<n>: is_template, not is_hero, and every hero made from it is a lord.
        with tempfile.TemporaryDirectory() as tmp:
            md = self._moduledata(tmp)
            (md / 'characters' / 'npcs_x.xml').write_text(
                '<NPCCharacters>'
                '<NPCCharacter id="spc_x_lord_1" is_template="true" is_hero="false"><Equipments>'
                '<EquipmentSet id="x_bat_template_medium_a" /></Equipments></NPCCharacter>'
                '<NPCCharacter id="spc_x_rebel_1" is_template="true" is_hero="false"><Equipments>'
                '<EquipmentSet id="x_bat_template_medium_a" /></Equipments></NPCCharacter>'
                '<NPCCharacter id="spc_x_headman_1" is_template="true" is_hero="false"><Equipments>'
                '<EquipmentSet id="x_bat_template_medium_a" /></Equipments></NPCCharacter></NPCCharacters>\n',
                encoding='utf-8')
            (md / 'taom_spcultures.xml').write_text(
                '<SPCultures><Culture id="x"><notable_templates><template name="NPCCharacter.spc_x_headman_1" />'
                '</notable_templates><lord_templates><template name="NPCCharacter.spc_x_lord_1" /></lord_templates>'
                '<rebellion_hero_templates><template name="NPCCharacter.spc_x_rebel_1" /></rebellion_hero_templates>'
                '</Culture></SPCultures>\n', encoding='utf-8')
            found = self._violations(md)
        self.assertEqual([(who, vid) for _, who, vid in found], [('spc_x_headman_1', 'taom_lord_a')])

    def test_a_companion_wearing_a_lord_template_by_id_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = self._moduledata(tmp)
            (md / 'named_companions' / 'c.xml').write_text(
                '<NPCCharacters><NPCCharacter id="x_companion" is_hero="true" occupation="Wanderer"><Equipments>'
                '<EquipmentSet id="x_bat_template_medium_a" /></Equipments></NPCCharacter></NPCCharacters>\n',
                encoding='utf-8')
            found = self._violations(md)
        self.assertEqual([(who, vid) for _, who, vid in found], [('x_companion', 'taom_lord_a')])

    def test_a_troop_wearing_a_lord_template_by_id_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = self._moduledata(tmp)
            (md / 'troops' / 'troops_x.xml').write_text(
                '<NPCCharacters><NPCCharacter id="x_guard" level="26"><Equipments>'
                '<EquipmentSet id="x_bat_template_medium_a" /></Equipments></NPCCharacter></NPCCharacters>\n',
                encoding='utf-8')
            found = self._violations(md)
        self.assertEqual([(who, vid) for _, who, vid in found], [('x_guard', 'taom_lord_a')])

    def test_an_xslt_that_names_a_variant_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = self._moduledata(tmp)
            (md / 'heroes.xslt').write_text(
                '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">'
                '<xsl:template match="NPCCharacter[@id=\'x_hero\']"><Equipments>'
                '<equipment slot="Body" id="Item.taom_lord_a" /></Equipments></xsl:template></xsl:stylesheet>\n',
                encoding='utf-8')
            found = self._violations(md)
        self.assertEqual([(Path(f).name, vid) for f, _, vid in found], [('heroes.xslt', 'taom_lord_a')])


class DeriveSeesTheVariants(unittest.TestCase):
    def test_a_variant_is_indexed_in_its_sources_folder_with_its_own_values(self):
        with tempfile.TemporaryDirectory() as tmp:
            armory = Path(tmp) / 'items'
            (armory / 'mirkwood').mkdir(parents=True)
            (armory / 'mirkwood' / 'body_armors.xml').write_bytes(
                ('<Items>\r\n' + SOURCE + '\r\n</Items>\r\n').encode('utf-8'))
            variants = Path(tmp) / 'lord_armour_variants.xml'
            variants.write_bytes(glv.render_file(
                [('mkwd_inf3_chest', 'r', glv.render_item(SOURCE, 'mkwd_inf3_chest',
                                                          {'body_armor': 51, 'arm_armor': 0, 'leg_armor': 20},
                                                          'plate'))], '\r\n').encode('utf-8'))
            with mock.patch.object(ra, 'ARMORY_DIR', str(armory)), \
                    mock.patch.object(dat, 'LORD_VARIANTS_XML', str(variants)):
                index, _ = dat.build_armory_index()
        self.assertEqual(index['taom_lord_mkwd_inf3_chest']['folder'], 'mirkwood')
        self.assertEqual(index['taom_lord_mkwd_inf3_chest']['slot'], 'body')
        self.assertEqual(index['taom_lord_mkwd_inf3_chest']['armour'], (0, 51, 0, 20))
        self.assertEqual(index['mkwd_inf3_chest']['armour'], (0, 8, 0, 0))


class ExitCodes(unittest.TestCase):
    def test_no_install_is_two(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(glv.entry(['--check', '--game-modules', tmp]), 2)


if __name__ == '__main__':
    unittest.main()
