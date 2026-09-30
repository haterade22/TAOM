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
import os
import sys
import tempfile
import unittest
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


if __name__ == '__main__':
    unittest.main()
