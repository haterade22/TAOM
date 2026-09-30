#!/usr/bin/env python3
"""Unit tests for tools/generate_enlistment_rosters.py's creature-rider skip, on synthetic data.

Run:  python -m unittest tools.tests.test_generate_enlistment_rosters

A rider of a mount-locked creature (the giant spider, the war elephant, the Mumakil) is never an enlistment donor
(Mike 2026-09-29): the generator emits no mounts, so a spider rider's kit would hand a goblin "cavalry" enlistee a
spider rider's gear on foot. The traps: dropping a horse or war-ram rider or a troop with no mount, reading the mount
from a civilian roster the troop never rides into battle, letting the mount into the emitted slots, and matching a
Monster by substring (the spider_statue row).
"""
import os
import sys
import tempfile
import unittest

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
                {'id': 'goblin_lurker', 'level': 16, 'group': 'Ranged', 'slots': {}, 'mount': ''},
                {'id': 'goblin_spider_rider', 'level': 21, 'group': 'Cavalry', 'slots': {},
                 'mount': 'spider_mount_mountain_a1'},
            ],
            'aserai': [
                {'id': 'harad_elephant_rider', 'level': 51, 'group': 'HorseArcher', 'slots': {},
                 'mount': 'taom_war_elephant'},
                {'id': 'harad_horseman', 'level': 16, 'group': 'Cavalry', 'slots': {}, 'mount': 'war_horse_a'},
            ],
            'erebor': [
                {'id': 'ironpass_ram_breaker', 'level': 26, 'group': 'Cavalry', 'slots': {}, 'mount': 'taom_war_ram_a'},
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


class ParseTroopsMountTests(unittest.TestCase):
    def test_the_mount_comes_from_the_first_battle_roster_and_is_never_a_slot(self):
        with tempfile.TemporaryDirectory() as troops_dir:
            with open(os.path.join(troops_dir, 'troops_goblin.xml'), 'w', encoding='utf-8') as f:
                f.write(TROOPS)
            saved = ger.TROOPS_DIR
            ger.TROOPS_DIR = troops_dir
            try:
                troop = ger.parse_troops()['goblin'][0]
            finally:
                ger.TROOPS_DIR = saved
        self.assertEqual('spider_mount_mountain_a1', troop['mount'], 'the civilian roster is not what the troop rides')
        self.assertNotIn('Horse', troop['slots'], 'a mount must never reach the emitted slots')


if __name__ == '__main__':
    unittest.main()
