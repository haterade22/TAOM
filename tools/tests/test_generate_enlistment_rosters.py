#!/usr/bin/env python3
"""Unit tests for tools/generate_enlistment_rosters.py's creature-rider skip, on synthetic data.

Run:  python -m unittest tools.tests.test_generate_enlistment_rosters

A rider of a mount-locked creature (the giant spider, the war elephant, the Mumakil) is never an enlistment donor
(Mike 2026-09-29): the generator emits no mounts, so a spider rider's kit would hand a goblin "cavalry" enlistee a
spider rider's gear on foot. The traps: dropping a horse or war-ram rider or a troop with no mount, reading the mount
from a civilian roster the troop never rides into battle, reading it from the first battle roster only (the engine
draws each slot from any battle set), missing a Horse written directly under <Equipments> (the engine writes it over
that slot in every set), letting the mount into the emitted slots, and matching a Monster by substring (the
spider_statue row). Record: docs/reviews/rca-keyforce-art-wiring-2026-09-29.md, Codex pass C1 and fix review R1.
"""
import collections
import contextlib
import io
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_enlistment_rosters as ger  # noqa: E402

HORSES = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<Items>\n'
    '  <Item id="spider_mount_mountain_a1" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.spider" body_length="100" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="taom_war_elephant" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.taom_war_elephant" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="taom_mumakil" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.taom_mumakil" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="war_horse_a" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.horse" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="taom_war_ram_a" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.taom_war_ram" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="spider_statue_mount" mesh="m" Type="Horse">\n'
    '    <ItemComponent><Horse monster="Monster.spider_statue" /></ItemComponent>\n'
    '  </Item>\n'
    '  <Item id="spider_saddle_like" mesh="m" Type="HorseHarness">\n'
    '    <ItemComponent><Armor family_type="1" /></ItemComponent>\n'
    '  </Item>\n'
    '</Items>\n'
)


class CreatureMountItemIdsTests(unittest.TestCase):
    def test_reads_the_mount_locked_creatures_and_nothing_else(self):
        with tempfile.TemporaryDirectory() as items_dir:
            with open(os.path.join(items_dir, 'LOTRAOM_horses.xml'), 'w', encoding='utf-8') as f:
                f.write(HORSES)
            ids = ger.creature_mount_item_ids(items_dir)
        self.assertEqual({'spider_mount_mountain_a1', 'taom_war_elephant', 'taom_mumakil'}, ids)

    def test_a_missing_items_folder_is_an_error_not_an_empty_set(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(SystemExit):
                ger.creature_mount_item_ids(os.path.join(root, 'absent'))


class DropCreatureRidersTests(unittest.TestCase):
    def test_drops_only_riders_of_a_creature_mount(self):
        by_culture = {
            'goblin': [
                {'id': 'goblin_lurker', 'level': 16, 'group': 'Ranged', 'slots': {}, 'mounts': []},
                {'id': 'goblin_spider_rider', 'level': 21, 'group': 'Cavalry', 'slots': {},
                 'mounts': ['spider_mount_mountain_a1']},
            ],
            'aserai': [
                {'id': 'harad_elephant_rider', 'level': 51, 'group': 'HorseArcher', 'slots': {},
                 'mounts': ['taom_war_elephant']},
                {'id': 'harad_horseman', 'level': 16, 'group': 'Cavalry', 'slots': {}, 'mounts': ['war_horse_a']},
            ],
            'erebor': [
                {'id': 'ironpass_ram_breaker', 'level': 26, 'group': 'Cavalry', 'slots': {},
                 'mounts': ['taom_war_ram_a']},
            ],
        }
        kept, dropped = ger.drop_creature_riders(by_culture, {'spider_mount_mountain_a1', 'taom_war_elephant'})
        self.assertEqual(['goblin_lurker'], [t['id'] for t in kept['goblin']])
        self.assertEqual(['harad_horseman'], [t['id'] for t in kept['aserai']])
        self.assertEqual(['ironpass_ram_breaker'], [t['id'] for t in kept['erebor']])
        self.assertEqual(['goblin_spider_rider', 'harad_elephant_rider'], sorted(dropped))


TROOPS = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<NPCCharacters>\n'
    '  <NPCCharacter id="goblin_spider_rider" level="21" default_group="Cavalry" occupation="Soldier"\n'
    '                culture="Culture.goblin">\n'
    '    <Equipments>\n'
    '      <EquipmentRoster civilian="true">\n'
    '        <equipment slot="Horse" id="Item.war_horse_a" />\n'
    '      </EquipmentRoster>\n'
    '      <EquipmentRoster>\n'
    '        <equipment slot="Item0" id="Item.wm_gundabad_axe_a01" />\n'
    '        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_med_b" />\n'
    '        <equipment slot="Horse" id="Item.spider_mount_mountain_a1" />\n'
    '      </EquipmentRoster>\n'
    '    </Equipments>\n'
    '  </NPCCharacter>\n'
    '</NPCCharacters>\n'
)


def goblin(troop_id, equipments):
    """One goblin Cavalry NPCCharacter whose <Equipments> body is `equipments`."""
    return (f'  <NPCCharacter id="{troop_id}" level="21" default_group="Cavalry" occupation="Soldier"\n'
            f'                culture="Culture.goblin">\n'
            f'    <Equipments>\n{equipments}    </Equipments>\n'
            f'  </NPCCharacter>\n')


AXE = '        <equipment slot="Item0" id="Item.wm_gundabad_axe_a01" />\n'
SPIDER = '        <equipment slot="Horse" id="Item.spider_mount_mountain_a1" />\n'
HORSE = '        <equipment slot="Horse" id="Item.war_horse_a" />\n'


def roster(body, attrs=''):
    return f'      <EquipmentRoster{attrs}>\n{body}      </EquipmentRoster>\n'


def override(item_id):
    """An <equipment slot="Horse"> directly under <Equipments>: the engine writes it over that slot in every set."""
    return f'      <equipment slot="Horse" id="{item_id}" />\n'


# One goblin troop per rule of how the engine assembles a troop's battle sets, each named for its case.
MIXED_ROSTERS = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<NPCCharacters>\n'
    + goblin('spider_in_second_set', roster(AXE)
             + roster(AXE + '        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_med_b" />\n' + SPIDER))
    + goblin('horse_then_spider', roster(HORSE) + roster(SPIDER))
    + goblin('spider_then_horse', roster(SPIDER) + roster(HORSE))
    + goblin('spider_as_override', roster(AXE) + override('Item.spider_mount_mountain_a1'))
    + goblin('horse_overrides_spider', roster(AXE + SPIDER) + override('Item.war_horse_a'))
    + goblin('override_clears_spider', roster(AXE + SPIDER) + override(''))
    + goblin('civilian_typed_spider', roster(SPIDER, ' equipmentType="Civilian"') + roster(AXE))
    + goblin('goblin_horseman', roster(HORSE) + roster(HORSE))
    + '</NPCCharacters>\n'
)


def parse_troops_from(xml):
    """parse_troops() over one troops_goblin.xml holding `xml`."""
    with tempfile.TemporaryDirectory() as troops_dir:
        with open(os.path.join(troops_dir, 'troops_goblin.xml'), 'w', encoding='utf-8') as f:
            f.write(xml)
        with mock.patch.object(ger, 'TROOPS_DIR', troops_dir):
            return ger.parse_troops()


class ParseTroopsPlaceholderTests(unittest.TestCase):
    def test_a_placeholder_reads_as_its_donor(self):
        # A tier clone (tools/placeholder_weapons.json) is a troop's restated weapon: an enlistment kit,
        # like a career kit (generate_career_kits.index_troop_gear), takes the Armory donor instead.
        xml = ('<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
               + goblin('clone_carrier', roster('        <equipment slot="Item0" id="Item.taom_ph_axe_t5" />\n'))
               + '</NPCCharacters>\n')
        with mock.patch.object(ger, 'placeholder_donors', return_value={'taom_ph_axe_t5': 'wm_gundabad_axe_a01'}):
            troop = parse_troops_from(xml)['goblin'][0]
        self.assertEqual('wm_gundabad_axe_a01', troop['slots']['Item0'])


class ParseTroopsMountTests(unittest.TestCase):
    def test_the_mounts_come_from_the_battle_rosters_and_are_never_a_slot(self):
        troop = parse_troops_from(TROOPS)['goblin'][0]
        self.assertEqual(['spider_mount_mountain_a1'], troop['mounts'],
                         'the civilian roster is not what the troop rides')
        self.assertNotIn('Horse', troop['slots'], 'a mount must never reach the emitted slots')

    def test_a_creature_the_engine_mounts_in_any_battle_set_drops_the_troop(self):
        kept, dropped = ger.drop_creature_riders(parse_troops_from(MIXED_ROSTERS), {'spider_mount_mountain_a1'})
        self.assertEqual(['horse_then_spider', 'spider_as_override', 'spider_in_second_set', 'spider_then_horse'],
                         sorted(dropped))
        self.assertEqual(['civilian_typed_spider', 'goblin_horseman', 'horse_overrides_spider', 'override_clears_spider'],
                         sorted(t['id'] for t in kept['goblin']))

    def test_the_kit_comes_from_the_first_battle_set(self):
        troops = {t['id']: t for t in parse_troops_from(MIXED_ROSTERS)['goblin']}
        self.assertEqual({'Item0': 'wm_gundabad_axe_a01'}, troops['spider_in_second_set']['slots'])
        self.assertEqual({'Item0': 'wm_gundabad_axe_a01'}, troops['civilian_typed_spider']['slots'],
                         'an equipmentType="Civilian" roster is not a battle set')


class SeedMarkerTests(unittest.TestCase):
    def test_the_seed_marker_is_a_legal_xml_comment(self):
        # XML forbids "--" inside a comment, and the marker once named the flag, so every
        # --seed-missing run refused its own output as malformed (2026-10-15).
        import xml.etree.ElementTree as ET
        ET.fromstring(f'<EquipmentRosters>\n    {ger.SEED_MARKER}\n</EquipmentRosters>')


CLASSES = {'composite_bow': 'Bow', 'ladder_harad_bow_x': 'Bow', 'arrows_a': 'Arrows', 'sword_a': 'OneHanded',
           'ladder_other_bow': 'Bow'}


class IssueLauncherTests(unittest.TestCase):
    def test_a_listed_cultures_archer_cell_swaps_only_the_bow(self):
        slots = {'Item0': 'ladder_harad_bow_x', 'Item1': 'arrows_a', 'Item2': 'sword_a', 'Body': 'b'}
        out = ger.apply_issue_launcher('aserai', 'archer', 'soldier', slots, CLASSES)
        self.assertEqual({'Item0': 'steppe_heavy_bow', 'Item1': 'arrows_a', 'Item2': 'sword_a', 'Body': 'b'}, out)
        self.assertEqual('ladder_harad_bow_x', slots['Item0'], 'the input is not mutated')

    def test_each_rank_has_its_own_bow_for_each_harad_culture(self):
        for culture in ('aserai', 'abanissa', 'shaghana'):
            got = [ger.apply_issue_launcher(culture, 'archer', r, {'Item0': 'ladder_harad_bow_x'}, CLASSES)['Item0']
                   for r in ger.RANKS]
            self.assertEqual(['composite_bow', 'steppe_heavy_bow', 'composite_steppe_bow', 'steppe_war_bow'], got)

    def test_other_assignments_and_cultures_are_untouched(self):
        slots = {'Item0': 'ladder_other_bow', 'Item1': 'arrows_a'}
        self.assertEqual(slots, ger.apply_issue_launcher('aserai', 'cavalry', 'soldier', slots, CLASSES))
        self.assertEqual(slots, ger.apply_issue_launcher('khuzait', 'archer', 'soldier', slots, CLASSES))

    def test_a_listed_archer_cell_without_a_bow_is_left_alone(self):
        slots = {'Item0': 'sword_a'}
        self.assertEqual(slots, ger.apply_issue_launcher('aserai', 'archer', 'recruit', slots, CLASSES))


def rosters_xml(*ids, comment_before=None):
    parts = ['<?xml version="1.0"?>\r\n<EquipmentRosters>\r\n']
    for rid in ids:
        if comment_before == rid:
            parts.append('    <!-- Hand-tuned bows (2026-10-15): keep -->\r\n')
        parts.append(f'    <EquipmentRoster id="{rid}" culture="Culture.x">\r\n        <EquipmentSet>\r\n'
                     f'            <Equipment slot="Item0" id="Item.a" />\r\n        </EquipmentSet>\r\n'
                     f'    </EquipmentRoster>\r\n')
    parts.append('</EquipmentRosters>')
    return ''.join(parts)


class ReseedTests(unittest.TestCase):
    def test_strip_removes_only_the_exact_culture_and_its_hand_tuned_comment(self):
        text = rosters_xml('enlist_aserai_archer_recruit', 'enlist_aserai2_archer_recruit',
                           'enlist_aserai_archer_soldier', 'enlist_khuzait_archer_recruit',
                           'enlist_default_archer_recruit', comment_before='enlist_aserai_archer_soldier')
        out, removed, _where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual(2, removed)
        self.assertNotIn('"enlist_aserai_archer_recruit"', out)
        self.assertNotIn('"enlist_aserai_archer_soldier"', out)
        self.assertNotIn('Hand-tuned', out)
        for keep in ('enlist_aserai2_archer_recruit', 'enlist_khuzait_archer_recruit', 'enlist_default_archer_recruit'):
            self.assertIn(f'"{keep}"', out)
        ET.fromstring(out)

    def test_a_comment_above_a_kept_roster_survives(self):
        text = rosters_xml('enlist_aserai_archer_recruit', 'enlist_khuzait_archer_recruit',
                           comment_before='enlist_khuzait_archer_recruit')
        out, _, _where = ger.strip_culture_rosters(text, 'aserai')
        self.assertIn('Hand-tuned', out)

    def test_strip_reports_where_the_first_removed_roster_stood(self):
        text = rosters_xml('enlist_khuzait_archer_recruit', 'enlist_aserai_archer_recruit',
                           'enlist_aserai_archer_soldier')
        out, removed, where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual(2, removed)
        self.assertEqual(text.index('    <EquipmentRoster id="enlist_aserai_archer_recruit"'), where)
        self.assertTrue(out[where:].startswith('</EquipmentRosters>'))

    def test_strip_reports_no_position_when_nothing_matched(self):
        text = rosters_xml('enlist_khuzait_archer_recruit')
        out, removed, where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual((text, 0, None), (out, removed, where))

    def test_a_self_closing_roster_does_not_swallow_its_neighbour(self):
        text = ('<EquipmentRosters>\r\n'
                '    <EquipmentRoster id="enlist_aserai_archer_recruit" culture="Culture.x" />\r\n'
                '    <EquipmentRoster id="enlist_khuzait_archer_recruit" culture="Culture.x">\r\n'
                '        <EquipmentSet />\r\n'
                '    </EquipmentRoster>\r\n'
                '</EquipmentRosters>')
        out, removed, _where = ger.strip_culture_rosters(text, 'aserai')
        self.assertEqual(1, removed)
        self.assertIn('"enlist_khuzait_archer_recruit"', out)
        self.assertNotIn('enlist_aserai_archer_recruit', out)
        ET.fromstring(out)

    def run_main(self, argv):
        with mock.patch.object(sys, 'argv', ['gen'] + argv), \
                mock.patch.object(ger, 'build_indexes', side_effect=AssertionError('indexes built')):
            return ger.main()

    def test_reseed_refuses_without_culture_or_apply(self):
        for argv in (['--reseed'], ['--reseed', '--apply'], ['--reseed', '--culture', 'aserai']):
            with self.assertRaises(SystemExit) as cm:
                self.run_main(argv)
            self.assertIn('--reseed', str(cm.exception.code))


class EverythingExists:
    """A stand-in index that contains every id."""

    def __contains__(self, _item):
        return True


PIPE_CLASSES = {'bow_x': 'Bow', 'arrows_a': 'Arrows', 'composite_bow': 'Bow', 'steppe_heavy_bow': 'Bow',
                'composite_steppe_bow': 'Bow', 'steppe_war_bow': 'Bow'}
ARCHER = {'id': 'archer_a', 'level': 10, 'group': 'Ranged', 'mounts': [],
          'slots': {'Body': 'body_a', 'Item0': 'bow_x', 'Item1': 'arrows_a'}}

OLD_FILE = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n<EquipmentRosters>\r\n'
    '\r\n    <!-- KHUZAIT -->\r\n\r\n'
    '    <EquipmentRoster id="enlist_khuzait_archer_recruit" culture="Culture.khuzait">\r\n'
    '        <EquipmentSet>\r\n            <Equipment slot="Item0" id="Item.k" />\r\n        </EquipmentSet>\r\n'
    '    </EquipmentRoster>\r\n'
    '\r\n    <!-- SHAGHANA -->\r\n\r\n'
    '    <EquipmentRoster id="enlist_shaghana_archer_recruit" culture="Culture.shaghana">\r\n'
    '        <EquipmentSet>\r\n            <Equipment slot="Item0" id="Item.old" />\r\n        </EquipmentSet>\r\n'
    '    </EquipmentRoster>\r\n'
    '\r\n    <!-- NEXT -->\r\n\r\n'
    '    <EquipmentRoster id="enlist_next_archer_recruit" culture="Culture.next">\r\n'
    '        <EquipmentSet>\r\n            <Equipment slot="Item0" id="Item.n" />\r\n        </EquipmentSet>\r\n'
    '    </EquipmentRoster>\r\n'
    '\r\n</EquipmentRosters>\r\n'
)


class PipelineTests(unittest.TestCase):
    def run_main(self, argv, troops=None):
        troops = {'shaghana': [dict(ARCHER)]} if troops is None else troops
        buf = io.StringIO()
        with mock.patch.object(sys, 'argv', ['gen'] + argv), \
                mock.patch.object(ger, 'build_indexes',
                                  return_value=(EverythingExists(), EverythingExists(), PIPE_CLASSES, {})), \
                mock.patch.object(ger, 'parse_troops', return_value=troops), \
                mock.patch.object(ger, 'game_modules', return_value='nowhere'), \
                mock.patch.object(ger, 'creature_mount_item_ids', return_value=set()), \
                mock.patch.object(ger, 'build_rank_indexes', return_value=({}, {})), \
                mock.patch.object(ger, 'culture_troop_gear', return_value={}), \
                contextlib.redirect_stdout(buf):
            ger.main()
        return buf.getvalue()

    def test_the_launcher_swap_precedes_duplicate_suppression(self):
        # One donor serves every rank, so the donor kits are identical; only the issued launchers differ.
        out = self.run_main(['--culture', 'shaghana'])
        rows = [ln for ln in out.splitlines() if ln.strip().startswith('shaghana') and ' archer ' in ln]
        self.assertEqual(4, len(rows))
        self.assertFalse(any('SUPPRESSED' in ln for ln in rows), rows)
        soldier = [ln for ln in rows if ' soldier ' in ln][0]
        self.assertIn('Item0=steppe_heavy_bow', soldier)

    def reseed(self, path):
        return self.run_main(['--culture', 'shaghana', '--apply', '--reseed', '--out', path])

    def test_reseed_replaces_in_place_and_is_idempotent(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            with open(path, 'wb') as fh:
                fh.write(OLD_FILE.encode('utf-8'))
            self.reseed(path)
            first = open(path, 'rb').read()
            self.reseed(path)
            second = open(path, 'rb').read()
        self.assertEqual(first, second)
        text = first.decode('utf-8')
        self.assertNotIn('seed-missing', text)
        self.assertNotIn('Item.old', text)
        self.assertNotIn('enlist_default_', text, 'reseed does not run the defaults loop')
        shaghana = text.index('id="enlist_shaghana_archer_recruit"')
        self.assertGreater(shaghana, text.index('<!-- SHAGHANA -->'))
        self.assertLess(shaghana, text.index('<!-- NEXT -->'))
        self.assertIn('id="enlist_shaghana_archer_soldier"', text)
        ET.fromstring(text)

    def test_reseed_of_a_culture_with_no_rosters_falls_back_to_the_marker_append(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            bare = '<?xml version="1.0"?>\r\n<EquipmentRosters>\r\n</EquipmentRosters>\r\n'
            with open(path, 'wb') as fh:
                fh.write(bare.encode('utf-8'))
            self.reseed(path)
            text = open(path, 'rb').read().decode('utf-8')
        self.assertIn(ger.SEED_MARKER, text)
        self.assertIn('id="enlist_shaghana_archer_soldier"', text)
        self.assertNotIn('enlist_default_', text)

    def test_a_reseed_ends_with_the_rank_repair_dry_run_for_its_culture(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            with open(path, 'wb') as fh:
                fh.write(OLD_FILE.encode('utf-8'))
            out = self.reseed(path)
        self.assertIn('Rank repairs (', out)
        self.assertIn('python tools/generate_enlistment_rosters.py --repair-ranks --culture shaghana --apply', out)

    def test_a_reseed_refuses_a_culture_the_promoter_owns(self):
        owned = OLD_FILE.replace('\r\n    <!-- SHAGHANA -->', '\r\n  <!-- TAOM-NEWCULTURE:shaghana:BEGIN -->')
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            with open(path, 'wb') as fh:
                fh.write(owned.encode('utf-8'))
            with self.assertRaises(SystemExit) as cm:
                self.reseed(path)
            self.assertEqual(owned.encode('utf-8'), open(path, 'rb').read())
        self.assertIn('promote_borrowed_cultures.py', str(cm.exception.code))

    def test_a_full_apply_refuses_a_file_that_holds_promoter_regions(self):
        owned = OLD_FILE.replace('\r\n    <!-- NEXT -->', '\r\n  <!-- TAOM-NEWCULTURE:next:BEGIN -->')
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            with open(path, 'wb') as fh:
                fh.write(owned.encode('utf-8'))
            with self.assertRaises(SystemExit) as cm:
                self.run_main(['--apply', '--out', path])
            self.assertEqual(owned.encode('utf-8'), open(path, 'rb').read())
        message = str(cm.exception.code)
        for flag in ('--reseed --culture', '--seed-missing', '--repair-ranks', 'next'):
            self.assertIn(flag, message)


class LauncherIdCheckTests(unittest.TestCase):
    def test_a_launcher_id_missing_from_the_universe_fails_closed(self):
        universe = {i for ranks in ger.ISSUE_LAUNCHER.values() for i in ranks.values()}
        universe.discard('steppe_war_bow')
        with self.assertRaises(SystemExit) as cm:
            ger.check_issue_launchers(universe)
        self.assertIn('steppe_war_bow', str(cm.exception.code))

    def test_every_launcher_present_passes(self):
        ger.check_issue_launchers({i for ranks in ger.ISSUE_LAUNCHER.values() for i in ranks.values()})


class FileHeaderTests(unittest.TestCase):
    def test_header_carries_the_abanissa_and_launcher_sentences(self):
        header = ger.file_header()
        self.assertIn('ISSUE_LAUNCHER', header)
        self.assertIn('abanissa binds', header)
        self.assertIn('builds their rosters from those trees', header)
        ET.fromstring(header + '</EquipmentRosters>')


# The rank rule (#1290): a promotion kit must not total less armour than any kit the player drew before
# it, nor carry a weaker weapon in a category it shares with them.
RANK_ARMOUR = {
    'helm_lo': {'head_armor': 12}, 'helm_hi': {'head_armor': 30},
    'chest_lo': {'body_armor': 20, 'arm_armor': 5}, 'chest_hi': {'body_armor': 40, 'leg_armor': 5},
    'chest_x': {'body_armor': 42, 'leg_armor': 5}, 'chest_y': {'body_armor': 60},
    'boots_lo': {'leg_armor': 4}, 'boots_hi': {'leg_armor': 8}, 'gloves': {'arm_armor': 6},
}
# The generator's KitWeapon fields, so the fixtures load before the rule they test exists.
_W = collections.namedtuple('KitWeapon', 'category output kind foot_only couchable')


def _w(category, output, kind, foot_only=False, couchable=False):
    return _W(category, output, kind, foot_only, couchable)


RANK_WEAPONS = {
    'sword_a': _w('one handed', 40.0, 'OneHandedSword'),
    'sword_b': _w('one handed', 50.0, 'OneHandedSword'),
    'sword_c': _w('one handed', 49.0, 'OneHandedSword'),
    'sword_d': _w('one handed', 48.0, 'OneHandedSword'),
    'sword_e': _w('one handed', 60.0, 'OneHandedSword'),
    'mace_a': _w('one handed', 30.0, 'Mace'), 'mace_b': _w('one handed', 55.0, 'Mace'),
    'spear_a': _w('spear', 30.0, 'OneHandedPolearm'), 'spear_b': _w('spear', 20.0, 'OneHandedPolearm'),
    'bow_a': _w('bow', 50, 'Bow'), 'bow_b': _w('bow', 60, 'Bow'), 'bow_c': _w('bow', 72, 'Bow'),
    'longbow': _w('bow', 70, 'Bow', foot_only=True), 'mystery': None,
    'sm_x_1h_axe_a': _w('one handed', 30.0, 'OneHandedAxe'),
    'sm_x_1h_sword_b': _w('one handed', 58.0, 'OneHandedSword'),
    'sm_y_1h_sword_a': _w('one handed', 51.0, 'OneHandedSword'),
    'lance_lo': _w('spear', 28.0, 'OneHandedPolearm', couchable=True),
    'lance_hi': _w('spear', 40.0, 'OneHandedPolearm', couchable=True),
    'spear_hi': _w('spear', 38.0, 'OneHandedPolearm'),
}
RECRUIT, SOLDIER, VETERAN = ('x', 'infantry', 'recruit'), ('x', 'infantry', 'soldier'), ('x', 'infantry', 'veteran')


class _Usage:
    def __init__(self, *features):
        self.features = features


class _Priced:
    def __init__(self, description, best_dps, usages=()):
        self.description, self.best_dps, self.usages = description, best_dps, usages


class _Launcher:
    def __init__(self, damage, usage='bow'):
        self.damage, self.usage = damage, usage


class WeaponCategoryTests(unittest.TestCase):
    def test_categories_follow_the_maintainers_list(self):
        cases = [('OneHanded', 'OneHandedSword', 'one handed'), ('OneHanded', 'Mace', 'one handed'),
                 ('TwoHanded', 'TwoHandedSword', 'two handed sword'),
                 ('TwoHanded', 'TwoHandedAxe', 'two handed axe or mace'),
                 ('TwoHanded', 'TwoHandedMace', 'two handed axe or mace'),
                 ('Polearm', 'OneHandedPolearm', 'spear'), ('Polearm', 'TwoHandedPolearm', 'swinging polearm'),
                 ('Polearm', 'TwoHandedPolearm_Pike', 'pike'), ('Bow', None, 'bow'), ('Crossbow', None, 'crossbow'),
                 ('OneHanded', 'OneHandedBastardSword', 'one handed')]
        for cls, description, want in cases:
            self.assertEqual(want, ger.weapon_category(cls, description), (cls, description))

    def test_a_bastard_sword_is_one_handed_because_it_is_swung_beside_a_shield(self):
        # wm_gondor_lamedon_1h_sword_a: primary usage OneHandedBastardSword, a TwoHandedSword alternate.
        self.assertEqual('one handed', ger.weapon_category('TwoHanded', 'OneHandedBastardSword'))

    def test_thrown_weapons_shields_and_ammunition_have_no_category(self):
        for cls in ('Throwing', 'Shield', 'Arrows', 'Bolts', None):
            self.assertIsNone(ger.weapon_category(cls, 'OneHandedPolearm'))

    def test_outputs_carry_the_kind_and_mark_a_longbow_a_bow_the_saddle_bars(self):
        classes = {'s': 'OneHanded', 'p': 'Polearm', 'b': 'Bow', 'lb': 'Bow', 'j': 'Throwing', 'sh': 'Shield',
                   'unpriced': 'OneHanded', 'xb': 'Crossbow'}
        priced = {'s': _Priced('OneHandedSword', 41.5), 'p': _Priced('TwoHandedPolearm', 30.0),
                  'j': _Priced('OneHandedPolearm', 36.0)}
        launchers = {'b': _Launcher(57), 'lb': _Launcher(64, 'long_bow')}
        out = ger.kit_weapon_outputs(classes, launchers, priced, mount_barred={'long_bow'})
        self.assertEqual({'s': ('one handed', 41.5, 'OneHandedSword', False, False),
                          'p': ('swinging polearm', 30.0, 'TwoHandedPolearm', False, False),
                          'b': ('bow', 57, 'Bow', False, False), 'lb': ('bow', 64, 'Bow', True, False),
                          'unpriced': None, 'xb': None}, out)

    def test_a_polearm_with_a_couch_usage_is_marked_couchable(self):
        lance = _Priced('OneHandedPolearm', 28.3, (_Usage('onehanded_polearm', 'thrust'), _Usage('polearm', 'couch')))
        spear = _Priced('OneHandedPolearm', 38.0, (_Usage('onehanded_polearm', 'thrust'), _Usage('polearm', 'thrust')))
        out = ger.kit_weapon_outputs({'lance': 'Polearm', 'spear': 'Polearm'}, {}, {'lance': lance, 'spear': spear})
        self.assertTrue(out['lance'].couchable)
        self.assertFalse(out['spear'].couchable)

    def test_a_polearm_with_a_thrown_usage_is_a_thrown_weapon_and_never_compared(self):
        # imperial_throwing_spear_1_t4: a TwoHandedPolearm craft whose alternate is TwoHandedPolearm_Thrown.
        pilum = 'imperial_throwing_spear_1_t4'
        out = ger.kit_weapon_outputs({pilum: 'Polearm'}, {}, {pilum: _Priced('OneHandedPolearm', 36.4)},
                                     thrown={pilum})
        self.assertNotIn(pilum, out)


class RankRuleTests(unittest.TestCase):
    def regressions(self, lower, upper, mounted=False):
        return ger.rank_regressions([(RECRUIT, lower)], upper, RANK_ARMOUR, RANK_WEAPONS, mounted=mounted)

    def drawn(self, earlier, upper):
        return ger.rank_regressions(earlier, upper, RANK_ARMOUR, RANK_WEAPONS)

    def test_a_lower_armour_total_is_a_finding_that_names_both_totals(self):
        found = self.regressions({'Body': 'chest_hi', 'Item0': 'sword_a'}, {'Body': 'chest_lo', 'Item0': 'sword_a'})
        self.assertEqual(['armour'], [kind for kind, _text in found])
        self.assertIn('armour total 45 -> 25', found[0][1])

    def test_the_total_decides_so_a_zone_may_fall_when_another_rises_more(self):
        self.assertEqual([], self.regressions({'Head': 'helm_lo', 'Leg': 'boots_hi'},
                                              {'Head': 'helm_hi', 'Leg': 'boots_lo'}))

    def test_an_equal_kit_is_clean(self):
        kit = {'Body': 'chest_lo', 'Item0': 'sword_a'}
        self.assertEqual([], self.regressions(kit, dict(kit)))

    def test_a_weaker_weapon_of_a_shared_category_is_a_finding(self):
        found = self.regressions({'Item0': 'sword_b'}, {'Item0': 'mace_a'})
        self.assertEqual(['weapon'], [kind for kind, _text in found])
        self.assertIn('sword_b 50.0 -> mace_a 30.0', found[0][1])

    def test_a_weapon_inside_the_shared_tolerance_holds(self):
        self.assertEqual(0.03, ger.WEAPON_TOLERANCE)
        self.assertEqual([], self.regressions({'Item0': 'sword_b'}, {'Item0': 'sword_c'}))
        self.assertEqual(1, len(self.regressions({'Item0': 'sword_b'}, {'Item0': 'sword_d'})))

    def test_every_category_both_kits_hold_is_compared_not_only_the_main_weapon(self):
        found = self.regressions({'Item0': 'bow_b', 'Item1': 'sword_b'}, {'Item0': 'bow_b', 'Item1': 'sword_a'})
        self.assertEqual(['weapon'], [kind for kind, _text in found])
        self.assertIn('one handed', found[0][1])

    def test_a_category_the_upper_kit_drops_is_its_own_reason(self):
        found = self.regressions({'Item0': 'sword_b', 'Item1': 'spear_a'}, {'Item0': 'sword_b'})
        self.assertEqual([('dropped', 'drops the spear (spear_a) of enlist_x_infantry_recruit')], found)
        self.assertEqual(['dropped'], [k for k, _t in self.regressions({'Item0': 'sword_b'}, {'Item0': 'spear_b'})])

    def test_a_longbow_and_a_bow_are_one_category(self):
        found = self.regressions({'Item0': 'longbow'}, {'Item0': 'bow_b'})
        self.assertEqual(['weapon'], [kind for kind, _text in found])
        self.assertIn('bow (damage) longbow 70.0 -> bow_b 60.0', found[0][1])

    def test_every_kit_drawn_before_counts_not_only_the_last(self):
        # The player keeps every kit he was issued, so the best piece of any of them is the floor.
        found = self.drawn([(RECRUIT, {'Item0': 'sword_e'}), (SOLDIER, {'Item0': 'sword_b'})], {'Item0': 'sword_b'})
        self.assertEqual(['weapon'], [kind for kind, _text in found])
        self.assertIn('sword_e 60.0 -> sword_b 50.0 against enlist_x_infantry_recruit', found[0][1])

    def test_armour_is_held_against_the_strongest_total_drawn_before(self):
        found = self.drawn([(RECRUIT, {'Body': 'chest_hi'}), (SOLDIER, {'Body': 'chest_lo'})], {'Body': 'chest_lo'})
        self.assertEqual(['armour'], [kind for kind, _text in found])
        self.assertIn('armour total 45 -> 25 against enlist_x_infantry_recruit', found[0][1])

    def test_a_category_dropped_since_any_earlier_kit_is_reported_once(self):
        found = self.drawn([(RECRUIT, {'Item0': 'spear_a'}), (SOLDIER, {'Item0': 'spear_b', 'Item1': 'sword_a'})],
                           {'Item0': 'sword_a'})
        self.assertEqual([('dropped', 'drops the spear (spear_a) of enlist_x_infantry_recruit')], found)

    def test_a_cavalry_kit_that_trades_every_lance_for_a_spear_drops_the_lance(self):
        found = self.regressions({'Item0': 'lance_lo'}, {'Item0': 'spear_hi'}, mounted=True)
        self.assertEqual([('dropped', 'drops the lance (lance_lo) of enlist_x_infantry_recruit')], found)
        self.assertEqual([], self.regressions({'Item0': 'lance_lo'}, {'Item0': 'spear_hi', 'Item1': 'lance_lo'},
                                              mounted=True))

    def test_a_foot_kit_has_no_use_for_the_couch_so_no_lance_is_reported(self):
        # The engine's polearm_couch usage set carries requires_mount.
        self.assertEqual([], self.regressions({'Item0': 'lance_lo'}, {'Item0': 'spear_hi'}))

    def test_in_a_cavalry_kit_a_lance_is_its_own_category_not_held_against_a_spears_thrust(self):
        # The maintainer's list names "a spear that lances" beside "spear and shield". The Rohan cavalry
        # sergeant draws a lance (28.9) after three kits with a plain spear (38.0): a new weapon, and
        # the player still holds the spear.
        found = self.regressions({'Item0': 'spear_hi'}, {'Item0': 'lance_lo'}, mounted=True)
        self.assertEqual([('dropped', 'drops the spear (spear_hi) of enlist_x_infantry_recruit')], found)

    def test_on_foot_the_same_lance_is_a_spear_and_its_thrust_is_compared(self):
        found = self.regressions({'Item0': 'spear_hi'}, {'Item0': 'lance_lo'})
        self.assertEqual(['weapon'], [kind for kind, _text in found])

    def test_a_cavalry_lance_is_held_against_the_lances_drawn_before(self):
        found = self.regressions({'Item0': 'lance_hi', 'Item1': 'spear_b'}, {'Item0': 'lance_lo', 'Item1': 'spear_hi'},
                                 mounted=True)
        self.assertEqual(['weapon'], [kind for kind, _text in found])
        self.assertIn('lance (damage per second) lance_hi 40.0 -> lance_lo 28.0', found[0][1])

    def test_an_unpriced_weapon_is_never_compared(self):
        self.assertEqual([], self.regressions({'Item0': 'sword_b'}, {'Item0': 'mystery', 'Item1': 'sword_b'}))
        self.assertEqual([], self.regressions({'Item0': 'mystery'}, {}))


def rank_kits(chain, culture='x', assignment='infantry'):
    return {(culture, assignment, rank): kit for rank, kit in chain.items()}


class ResolverMirrorTests(unittest.TestCase):
    KITS = {('x', 'infantry', 'recruit'): {}, ('x', 'infantry', 'veteran'): {}, ('x', 'cavalry', 'soldier'): {},
            ('default', 'archer', 'recruit'): {}, ('default', 'infantry', 'soldier'): {}}

    def resolve(self, culture, assignment, rank):
        return ger.resolve_kit(culture, assignment, rank, self.KITS.__contains__)

    def test_the_requested_assignment_is_probed_down_every_tier_before_infantry(self):
        self.assertEqual(('x', 'cavalry', 'soldier'), self.resolve('x', 'cavalry', 'sergeant'))
        self.assertEqual(('x', 'infantry', 'recruit'), self.resolve('x', 'cavalry', 'recruit'))
        self.assertEqual(('x', 'infantry', 'veteran'), self.resolve('x', 'archer', 'sergeant'))

    def test_the_default_kits_come_only_after_the_whole_culture_chain(self):
        self.assertEqual(('default', 'archer', 'recruit'), self.resolve('y', 'archer', 'veteran'))
        self.assertEqual(('default', 'infantry', 'soldier'), self.resolve('y', 'support', 'soldier'))
        self.assertIsNone(self.resolve('y', 'cavalry', 'recruit'))

    def test_each_kit_is_held_against_every_distinct_kit_the_resolver_issued_before_it(self):
        kits = {k: v for k, v in self.KITS.items() if k[0] == 'x'}
        kits[('x', 'infantry', 'sergeant')] = {}
        chains = ger.rank_chains(kits)
        by_kit = dict(chains)
        self.assertEqual([('x', 'infantry', 'recruit')], by_kit[('x', 'infantry', 'veteran')])
        self.assertEqual([('x', 'infantry', 'recruit'), ('x', 'infantry', 'veteran')], by_kit[('x', 'infantry', 'sergeant')])
        self.assertEqual([('x', 'infantry', 'recruit')], by_kit[('x', 'cavalry', 'soldier')])
        self.assertEqual(len(by_kit), len(chains), 'one entry per kit')
        self.assertNotIn(('x', 'infantry', 'recruit'), by_kit)

    def test_the_mirror_matches_the_csharp_resolver(self):
        # resolve_kit cites these lines; if the resolver moves or changes order, re-read it and update both.
        path = os.path.join(ger.REPO_ROOT, 'Main', 'Features', 'Enlistment', 'Equipment', 'EnlistmentRosterResolver.cs')
        with open(path, encoding='utf-8-sig') as fh:
            lines = fh.read().splitlines()
        self.assertIn('foreach (var assignmentStep in AssignmentChain(assignment))', lines[53])
        self.assertIn('EnlistmentRosterIds.Build(cultureId', '\n'.join(lines[53:63]))
        self.assertIn('foreach (var assignmentStep in AssignmentChain(assignment))', lines[64])
        self.assertIn('EnlistmentRosterIds.BuildDefault(assignmentStep', '\n'.join(lines[64:71]))
        self.assertIn('AssignmentChain(ServiceAssignment assignment)', lines[81])
        chain = '\n'.join(lines[81:92])
        self.assertLess(chain.index('yield return assignment;'), chain.index('yield return ServiceAssignment.Infantry;'))
        self.assertIn('for (var r = (int)tier; r >= (int)EnlistmentKitTier.Recruit; r--)', lines[94])


class RankFindingsTests(unittest.TestCase):
    def findings(self, kits):
        return ger.enlistment_kit_findings(kits, RANK_ARMOUR, RANK_WEAPONS)

    def test_each_kit_is_held_against_the_kit_issued_before_it(self):
        kits = rank_kits({'recruit': {'Body': 'chest_hi'}, 'veteran': {'Body': 'chest_lo'}})
        self.assertEqual([(('x', 'infantry', 'veteran'), [('x', 'infantry', 'recruit')])],
                         [f[:2] for f in self.findings(kits)])

    def test_a_cavalry_kit_is_held_against_the_infantry_kit_issued_before_it(self):
        kits = {**rank_kits({'recruit': {'Body': 'chest_hi'}}), **rank_kits({'soldier': {'Body': 'chest_lo'}}, assignment='cavalry')}
        self.assertEqual([(('x', 'cavalry', 'soldier'), [('x', 'infantry', 'recruit')])],
                         [f[:2] for f in self.findings(kits)])

    def test_a_culture_kit_is_held_against_the_default_kit_issued_before_it(self):
        kits = {**rank_kits({'recruit': {'Body': 'chest_hi'}}, culture='default'), **rank_kits({'soldier': {'Body': 'chest_lo'}})}
        self.assertEqual([(('x', 'infantry', 'soldier'), [('default', 'infantry', 'recruit')])],
                         [f[:2] for f in self.findings(kits)])

    def test_another_cultures_kit_is_never_compared(self):
        kits = {**rank_kits({'recruit': {'Body': 'chest_hi'}}), **rank_kits({'soldier': {'Body': 'chest_lo'}}, culture='y')}
        self.assertEqual([], self.findings(kits))

    def test_kits_read_from_the_roster_file_text(self):
        text = rosters_xml('enlist_x_infantry_recruit', 'enlist_default_archer_veteran', 'not_an_enlist_roster')
        self.assertEqual({('x', 'infantry', 'recruit'): {'Item0': 'a'}, ('default', 'archer', 'veteran'): {'Item0': 'a'}},
                         ger.read_kits(text))

    def test_a_piece_the_rule_cannot_judge_is_named(self):
        kits = rank_kits({'recruit': {'Body': 'nowhere', 'Item0': 'mystery', 'Item1': 'sword_a', 'Item2': 'shield'}})
        self.assertEqual(['mystery', 'nowhere'], ger.unresolved_kit_items(kits, RANK_ARMOUR, RANK_WEAPONS))


class RankRepairTests(unittest.TestCase):
    def plan(self, kits, gear=None, skip=()):
        return ger.plan_rank_repairs(kits, RANK_ARMOUR, RANK_WEAPONS, skip=skip, gear=gear)

    @staticmethod
    def applied(kits, edits):
        out = {cell: dict(kit) for cell, kit in kits.items()}
        for cell, slot, _old, new in edits:
            out[cell][slot] = new
        return out

    SOLDIER = ('x', 'infantry', 'soldier')

    def test_armour_carries_the_fewest_lower_rank_pieces_forward(self):
        kits = rank_kits({'recruit': {'Head': 'helm_hi', 'Body': 'chest_hi', 'Leg': 'boots_hi'},
                          'soldier': {'Head': 'helm_lo', 'Body': 'chest_lo', 'Leg': 'boots_hi', 'Gloves': 'gloves'}})
        # 83 against 51: the chest is the larger deficit (20) and the helmet closes the rest (18).
        edits, left = self.plan(kits)
        self.assertEqual([(self.SOLDIER, 'Body', 'chest_lo', 'chest_hi'), (self.SOLDIER, 'Head', 'helm_lo', 'helm_hi')], edits)
        self.assertEqual([], left)
        self.assertEqual([], ger.enlistment_kit_findings(self.applied(kits, edits), RANK_ARMOUR, RANK_WEAPONS))

    def test_a_slot_the_upper_rank_leaves_empty_takes_the_lower_ranks_piece(self):
        kits = rank_kits({'recruit': {'Body': 'chest_lo', 'Gloves': 'gloves'}, 'soldier': {'Body': 'chest_lo'}})
        self.assertEqual([(self.SOLDIER, 'Gloves', None, 'gloves')], self.plan(kits)[0])

    def test_armour_prefers_the_culture_piece_that_matches_the_lower_one_most_closely(self):
        kits = rank_kits({'recruit': {'Body': 'chest_hi'}, 'soldier': {'Body': 'chest_lo'}})
        self.assertEqual([(self.SOLDIER, 'Body', 'chest_lo', 'chest_x')],
                         self.plan(kits, gear={'x': {'Body': {'chest_x', 'chest_y', 'chest_lo'}}})[0])
        self.assertEqual([(self.SOLDIER, 'Body', 'chest_lo', 'chest_hi')],
                         self.plan(kits, gear={'x': {'Body': {'chest_hi', 'chest_x'}}})[0])

    def test_a_foreign_lower_piece_is_never_carried_into_a_culture_kit(self):
        kits = {**rank_kits({'recruit': {'Body': 'chest_hi', 'Item0': 'sword_b'}}, culture='default'),
                **rank_kits({'soldier': {'Body': 'chest_lo', 'Item0': 'sword_a'}})}
        edits, left = self.plan(kits, gear={'x': {}})
        self.assertEqual([], edits)
        self.assertEqual([['armour', 'weapon']], [[k for k, _t in f[2]] for f in left])

    def test_a_weaker_weapon_takes_the_same_kind_from_the_cultures_gear_first(self):
        kits = rank_kits({'recruit': {'Item0': 'sword_b'}, 'soldier': {'Item1': 'sword_a', 'Item0': 'spear_a'}})
        gear = {'x': {'Item0': {'sword_c', 'sword_e', 'mace_b', 'sword_d'}}}
        self.assertEqual([(self.SOLDIER, 'Item1', 'sword_a', 'sword_c')], self.plan(kits, gear=gear)[0])

    def test_then_another_kind_of_the_category_from_the_cultures_gear(self):
        kits = rank_kits({'recruit': {'Item0': 'sword_b'}, 'soldier': {'Item0': 'sword_a'}})
        self.assertEqual([(self.SOLDIER, 'Item0', 'sword_a', 'mace_b')],
                         self.plan(kits, gear={'x': {'Item2': {'mace_b', 'mace_a'}}})[0])

    def test_within_a_preference_the_replaced_pieces_own_line_comes_first(self):
        # sm_x_1h_axe_a and sm_x_1h_sword_b are one look family; the closer id keeps the kit's look.
        kits = rank_kits({'recruit': {'Item0': 'sword_b'}, 'soldier': {'Item0': 'sm_x_1h_axe_a'}})
        gear = {'x': {'Item0': {'sm_y_1h_sword_a', 'sm_x_1h_sword_b', 'sword_c'}}}
        self.assertEqual([(self.SOLDIER, 'Item0', 'sm_x_1h_axe_a', 'sm_x_1h_sword_b')], self.plan(kits, gear=gear)[0])

    def test_then_the_lower_kits_own_weapon(self):
        kits = rank_kits({'recruit': {'Item0': 'sword_b'}, 'soldier': {'Item0': 'sword_a'}})
        self.assertEqual([(self.SOLDIER, 'Item0', 'sword_a', 'sword_b')], self.plan(kits, gear={'x': {}})[0])

    ARCHERS = rank_kits({'soldier': {'Item0': 'longbow'}, 'veteran': {'Item0': 'bow_b'}}, assignment='archer')
    ARCHER_VETERAN = ('x', 'archer', 'veteran')

    # The maintainer's rule (2026-11-15): a kit with a Horse slot is a horse archer's, one without is a
    # foot archer's. The culture's riders and what they carry no longer decide it.
    HORSE_ARCHERS = rank_kits({'soldier': {'Item0': 'longbow'}, 'veteran': {'Item0': 'bow_b', 'Horse': 'horse_a'}},
                              assignment='archer')

    def test_a_foot_archers_kit_takes_the_earlier_kits_longbow_when_the_troop_gear_has_none_that_holds(self):
        self.assertEqual([(self.ARCHER_VETERAN, 'Item0', 'bow_b', 'longbow')],
                         self.plan(self.ARCHERS, gear={'x': {'Item0': {'bow_a'}}})[0])

    def test_a_foot_archers_kit_takes_a_longbow_from_the_troop_gear_first(self):
        kits = rank_kits({'soldier': {'Item0': 'bow_c'}, 'veteran': {'Item0': 'bow_b'}}, assignment='archer')
        self.assertEqual([(self.ARCHER_VETERAN, 'Item0', 'bow_b', 'longbow')],
                         self.plan(kits, gear={'x': {'Item0': {'longbow'}}})[0])

    def test_a_kit_with_a_horse_slot_never_takes_a_launcher_the_saddle_bars(self):
        edits, left = self.plan(self.HORSE_ARCHERS, gear={'x': {'Item0': {'longbow'}}})
        self.assertEqual([], edits)
        self.assertEqual([['weapon']], [[k for k, _t in f[2]] for f in left])
        self.assertEqual([(self.ARCHER_VETERAN, 'Item0', 'bow_b', 'bow_c')],
                         self.plan(self.HORSE_ARCHERS, gear={'x': {'Item0': {'longbow', 'bow_c'}}})[0])

    def test_the_saddle_bar_follows_the_horse_slot_not_the_assignment(self):
        kits = rank_kits({'soldier': {'Item0': 'bow_c'}, 'veteran': {'Item0': 'bow_b'}}, assignment='cavalry')
        self.assertEqual([(('x', 'cavalry', 'veteran'), 'Item0', 'bow_b', 'longbow')],
                         self.plan(kits, gear={'x': {'Item0': {'longbow'}}})[0])
        mounted = {**kits, ('x', 'cavalry', 'veteran'): {'Item0': 'bow_b', 'Horse': 'horse_a'}}
        self.assertEqual([(('x', 'cavalry', 'veteran'), 'Item0', 'bow_b', 'bow_c')],
                         self.plan(mounted, gear={'x': {'Item0': {'longbow'}}})[0])

    def test_a_dropped_category_is_never_repaired(self):
        kits = rank_kits({'recruit': {'Item0': 'sword_b', 'Item1': 'spear_a'}, 'soldier': {'Item0': 'sword_b'}})
        edits, left = self.plan(kits, gear={'x': {'Item1': {'spear_a'}}})
        self.assertEqual([], edits)
        self.assertEqual([[('dropped', 'drops the spear (spear_a) of enlist_x_infantry_recruit')]], [f[2] for f in left])

    def test_a_weapon_is_held_against_the_best_of_any_earlier_kit_and_may_take_that_kits_piece(self):
        kits = rank_kits({'recruit': {'Item0': 'sword_e'}, 'soldier': {'Item0': 'sword_e', 'Item1': 'spear_a'},
                          'veteran': {'Item0': 'sword_b', 'Item1': 'spear_a'}})
        self.assertEqual([(VETERAN, 'Item0', 'sword_b', 'sword_e')], self.plan(kits, gear={'x': {}})[0])

    def test_armour_takes_the_pieces_of_the_strongest_earlier_kit(self):
        kits = rank_kits({'recruit': {'Body': 'chest_hi'}, 'soldier': {'Body': 'chest_lo', 'Head': 'helm_hi'},
                          'veteran': {'Body': 'chest_lo'}})
        # The recruit totals 45 and the soldier 55; the veteran (25) is held against the soldier's 55.
        self.assertEqual([(VETERAN, 'Head', None, 'helm_hi')], self.plan(kits)[0])

    LANCERS = rank_kits({'soldier': {'Item0': 'lance_hi'}, 'veteran': {'Item0': 'lance_lo'}}, assignment='cavalry')
    LANCER_VETERAN = ('x', 'cavalry', 'veteran')

    def test_a_couchable_lance_in_a_cavalry_kit_is_replaced_only_by_a_couchable_one(self):
        self.assertEqual([(self.LANCER_VETERAN, 'Item0', 'lance_lo', 'lance_hi')],
                         self.plan(self.LANCERS, gear={'x': {'Item0': {'spear_hi', 'lance_hi'}}})[0])
        self.assertEqual([(self.LANCER_VETERAN, 'Item0', 'lance_lo', 'lance_hi')],
                         self.plan(self.LANCERS, gear={'x': {'Item0': {'spear_hi'}}})[0], "the earlier kit's own lance")
        kits = rank_kits({'soldier': {'Item0': 'spear_hi'}, 'veteran': {'Item0': 'lance_lo'}}, assignment='cavalry')
        edits, left = self.plan(kits, gear={'x': {'Item0': {'spear_hi'}}})
        self.assertEqual([], edits)
        # A rider's lance after a plain spear is a new category, not a weaker spear: nothing to repair.
        self.assertEqual([['dropped']], [[k for k, _t in f[2]] for f in left])

    def test_a_plain_spear_in_a_cavalry_kit_is_not_replaced_by_a_lance(self):
        # In a cavalry kit the lance is another category, so the weaker spear takes the earlier kit's
        # spear; in an infantry kit the same couchable item is a spear and the troop gear's piece wins.
        chain = {'soldier': {'Item0': 'spear_a'}, 'veteran': {'Item0': 'spear_b'}}
        gear = {'x': {'Item0': {'lance_hi'}}}
        riders = rank_kits(chain, assignment='cavalry')
        self.assertEqual([(('x', 'cavalry', 'veteran'), 'Item0', 'spear_b', 'spear_a')], self.plan(riders, gear=gear)[0])
        self.assertEqual([(('x', 'infantry', 'veteran'), 'Item0', 'spear_b', 'lance_hi')],
                         self.plan(rank_kits(chain), gear=gear)[0])

    def test_only_armour_and_weapon_reasons_are_regressions(self):
        self.assertEqual(('armour', 'weapon'), ger.REGRESSION_KINDS)
        reasons = [('dropped', 'drops the spear'), ('weapon', 'weaker sword'), ('armour', 'less armour')]
        self.assertEqual([('weapon', 'weaker sword'), ('armour', 'less armour')], ger.regression_reasons(reasons))

    def test_a_repair_never_gives_a_kit_one_weapon_twice(self):
        kits = rank_kits({'recruit': {'Item0': 'sword_b'}, 'soldier': {'Item0': 'sword_a', 'Item1': 'spear_a'}})
        with mock.patch.object(ger, '_pick_weapon', return_value='spear_a'):
            with self.assertRaises(SystemExit) as cm:
                self.plan(kits)
        self.assertIn('twice', str(cm.exception.code))

    def test_a_repaired_rank_is_the_floor_for_the_rank_above_it(self):
        kits = rank_kits({'recruit': {'Body': 'chest_hi'}, 'soldier': {'Body': 'chest_lo'},
                          'veteran': {'Body': 'chest_lo', 'Gloves': 'gloves'}})
        self.assertEqual([(self.SOLDIER, 'Body', 'chest_lo', 'chest_hi'),
                          (('x', 'infantry', 'veteran'), 'Body', 'chest_lo', 'chest_hi')], self.plan(kits)[0])

    def test_the_named_cultures_and_the_default_kits_are_skipped(self):
        kits = rank_kits({'recruit': {'Body': 'chest_hi'}, 'soldier': {'Body': 'chest_lo'}})
        self.assertEqual([], self.plan(kits, skip={'x'})[0])
        self.assertEqual([], self.plan({**rank_kits({'recruit': {'Body': 'chest_hi'}, 'soldier': {'Body': 'chest_lo'}},
                                                    culture='default')})[0])


class CultureTroopGearTests(unittest.TestCase):
    def test_every_battle_set_of_the_line_troops_counts_and_an_alias_takes_its_source_tree(self):
        xml = ('<NPCCharacters>'
               + goblin('t1', roster('<equipment slot="Item0" id="Item.a" />')
                        + roster('<equipment slot="Item0" id="Item.b" />')
                        + roster('<equipment slot="Item0" id="Item.civ" />', ' civilian="true"'))
               + '</NPCCharacters>').replace('Culture.goblin', 'Culture.khuzait')
        with tempfile.TemporaryDirectory() as tmp:
            with open(os.path.join(tmp, 'troops_k.xml'), 'w', encoding='utf-8') as fh:
                fh.write(xml)
            gear = ger.culture_troop_gear(tmp)
        self.assertEqual({'a', 'b'}, gear['khuzait']['Item0'])
        self.assertNotIn('mounted', gear['khuzait'], "the kit's Horse slot decides the saddle, not the riders")
        self.assertEqual(gear['khuzait'], gear['battania'])


class RepairTextTests(unittest.TestCase):
    TEXT = ('<EquipmentRosters>\r\n'
            '    <EquipmentRoster id="enlist_x_infantry_soldier" culture="Culture.x">\r\n'
            '        <EquipmentSet>\r\n'
            '            <Equipment slot="Item0" id="Item.sword_a" />\r\n'
            '            <Equipment slot="Body" id="Item.chest_lo" />\r\n'
            '            <Equipment slot="Cape" id="Item.cape" />\r\n'
            '        </EquipmentSet>\r\n'
            '    </EquipmentRoster>\r\n'
            '    <EquipmentRoster id="enlist_y_infantry_soldier" culture="Culture.y">\r\n'
            '        <EquipmentSet>\r\n'
            '            <Equipment slot="Body" id="Item.chest_lo" />\r\n'
            '        </EquipmentSet>\r\n'
            '    </EquipmentRoster>\r\n'
            '</EquipmentRosters>\r\n')
    CELL = ('x', 'infantry', 'soldier')

    def test_a_swap_changes_only_the_named_rosters_slot(self):
        out = ger.repair_text(self.TEXT, [(self.CELL, 'Body', 'chest_lo', 'chest_hi')])
        self.assertEqual({('x', 'infantry', 'soldier'): {'Item0': 'sword_a', 'Body': 'chest_hi', 'Cape': 'cape'},
                          ('y', 'infantry', 'soldier'): {'Body': 'chest_lo'}}, ger.read_kits(out))
        self.assertEqual(self.TEXT.replace('Item.chest_lo" />\r\n            <Equipment slot="Cape"',
                                           'Item.chest_hi" />\r\n            <Equipment slot="Cape"'), out)

    def test_an_added_slot_goes_in_emission_order_with_the_files_line_ending(self):
        out = ger.repair_text(self.TEXT, [(self.CELL, 'Gloves', None, 'gloves')])
        self.assertIn('Item.chest_lo" />\r\n            <Equipment slot="Gloves" id="Item.gloves" />\r\n'
                      '            <Equipment slot="Cape"', out)
        ET.fromstring(out)

    def test_a_stale_plan_is_refused(self):
        with self.assertRaises(SystemExit) as cm:
            ger.repair_text(self.TEXT, [(self.CELL, 'Body', 'not_there', 'chest_hi')])
        self.assertIn('enlist_x_infantry_soldier', str(cm.exception.code))

    def run_repair(self, argv, text):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, 'rosters.xml')
            with open(path, 'wb') as fh:
                fh.write(text.encode('utf-8'))
            out = io.StringIO()
            with mock.patch.object(sys, 'argv', ['gen', '--repair-ranks', '--out', path] + argv), \
                    mock.patch.object(ger, 'build_rank_indexes', return_value=(RANK_ARMOUR, RANK_WEAPONS)), \
                    mock.patch.object(ger, 'culture_troop_gear', return_value={}), \
                    mock.patch.object(ger, 'build_indexes', side_effect=AssertionError('indexes built')), \
                    contextlib.redirect_stdout(out):
                ger.main()
            with open(path, 'rb') as fh:
                return fh.read().decode('utf-8'), out.getvalue()

    WEAK = TEXT.replace('enlist_y_infantry_soldier', 'enlist_x_infantry_recruit').replace(
        'Culture.y', 'Culture.x').replace('Item.chest_lo" />\r\n        </EquipmentSet>\r\n    </EquipmentRoster>\r\n</',
                                          'Item.chest_hi" />\r\n        </EquipmentSet>\r\n    </EquipmentRoster>\r\n</')

    def test_the_repair_mode_is_a_dry_run_until_apply(self):
        text, out = self.run_repair([], self.WEAK)
        self.assertEqual(self.WEAK, text)
        self.assertIn('enlist_x_infantry_soldier Body chest_lo -> chest_hi', out)
        self.assertIn('DRY-RUN', out)

    def test_the_repair_mode_applies_its_plan(self):
        text, _out = self.run_repair(['--apply'], self.WEAK)
        self.assertEqual(ger.read_kits(self.WEAK.replace('Item.chest_lo', 'Item.chest_hi')), ger.read_kits(text))
        self.assertIn('\r\n', text)

    def test_the_dry_run_lists_the_dropped_categories(self):
        dropped = self.WEAK.replace('<Equipment slot="Body" id="Item.chest_hi" />',
                                    '<Equipment slot="Body" id="Item.chest_hi" />\r\n'
                                    '            <Equipment slot="Item1" id="Item.spear_a" />')
        _text, out = self.run_repair([], dropped)
        self.assertIn('Dropped categories, for information (the player keeps every earlier kit) (1):', out)
        self.assertIn('enlist_x_infantry_soldier: drops the spear (spear_a) of enlist_x_infantry_recruit', out)

    def test_the_repair_mode_refuses_a_culture_the_promoter_owns(self):
        owned = self.WEAK.replace('<EquipmentRosters>\r\n', '<EquipmentRosters>\r\n  <!-- TAOM-NEWCULTURE:x:BEGIN -->\r\n')
        with self.assertRaises(SystemExit) as cm:
            self.run_repair(['--culture', 'x'], owned)
        self.assertIn('promote_borrowed_cultures.py', str(cm.exception.code))

    def test_the_repair_mode_refuses_an_unknown_culture_or_the_defaults_and_names_the_known_ids(self):
        for culture in ('nope', 'default'):
            with self.assertRaises(SystemExit) as cm:
                self.run_repair(['--culture', culture], self.WEAK)
            self.assertIn(culture, str(cm.exception.code))
            self.assertIn('Known: x', str(cm.exception.code))

    def test_the_shipped_default_kits_equal_the_generators_literal(self):
        # The repair leaves the defaults to a hand edit of both; a full apply rewrites the file from the literal.
        with open(ger.OUT_XML, 'rb') as fh:
            kits = ger.read_kits(fh.read().decode('utf-8-sig'))
        for (assignment, rank), items in ger.DEFAULT_ROSTER_ITEMS.items():
            self.assertEqual(dict(items), kits[('default', assignment, rank)], (assignment, rank))

    def test_promoter_regions_name_the_cultures_the_promoter_owns(self):
        text = ('<EquipmentRosters>\r\n  <!-- TAOM-NEWCULTURE:cardolan:BEGIN -->\r\n'
                '  <!-- TAOM-NEWCULTURE:cardolan:END -->\r\n</EquipmentRosters>')
        self.assertEqual({'cardolan'}, ger.promoter_owned_cultures(text))

if __name__ == '__main__':
    unittest.main()
