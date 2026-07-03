#!/usr/bin/env python3
"""Author the 2026-07-02 elf lord expansion (one-off): Lothlorien 3->10 adult lords
(+2 new clans -> 9 party slots), Rivendell 17->20 adults (fills Nos Glorfindel's empty
party slots). Companion to the #323 Steward boost — party size only matters if lords
exist to lead parties, and parties-per-clan is tier-capped (t<3:1, t3-4:2, t5+:3).

Writes, style-matched to existing entries:
  - characters/lords.xml   — 10 new NPCCharacter blocks (inline skills = live SkillSet
                             values, traits = generator archetypes, culture equipment
                             templates a-e rotated, donor elf face keys reused per the
                             existing 10-male/4-female key pool convention)
  - characters/heroes.xml  — 10 new Hero entries + move lord_L2_1 into clan_lothlorien_2
                             (fixes the L2-id-in-clan-1 mismatch; he becomes a Warden)
  - characters/clans.xml   — clan_lothlorien_2 "Wardens of the Naith" (t6) +
                             clan_lothlorien_3 "Nos Malgalad" (t5); banner keys donated
                             from clan_rivendell_2 / clan_lindon_2 (valid > invented)

Skill templates: males -> taom_elf_warrior_skills, females -> taom_elf_lady_skills,
clan owners + Erestor -> taom_elf_lord_skills, one archer -> taom_elf_archer_skills.
Non-default archetypes are pinned as canonical entries in apply_culture_skills_traits.py
(separate hand edit) so a future generator run reproduces these assignments.

Usage:
    python tools/author_elf_lords.py            # dry-run (default)
    python tools/author_elf_lords.py --apply
"""
import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LORDS = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"
HEROES = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "heroes.xml"
CLANS = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "clans.xml"
SETS = REPO / "Main" / "_Module" / "ModuleData" / "taom_lord_skill_sets.xml"

sys.path.insert(0, str(REPO / 'tools'))
from apply_culture_skills_traits import BASE_ARCHETYPES, SKILL_ORDER, TRAIT_ORDER  # noqa: E402

# id, name, gender, age, voice, archetype, group, clan, blurb
NEW_LORDS = [
    ('lord_L2_2', 'Thandirion', 'M', 34, 'earnest', 'elf_lord', 'Infantry', 'clan_lothlorien_2',
     'Thandirion, Warden-captain of the Naith, commands the hidden crossings of the Celebrant and answers only to the Lord and Lady of the Galadhrim.'),
    ('lord_L2_3', 'Baranthir', 'M', 27, 'curt', 'elf_warrior', 'Infantry', 'clan_lothlorien_2',
     'Baranthir keeps the northern eaves of the Golden Wood, where his patrols have turned back orc-bands out of Moria beyond counting.'),
    ('lord_L2_4', 'Aeglossen', 'M', 23, 'softspoken', 'elf_archer', 'Ranged', 'clan_lothlorien_2',
     'Aeglossen is counted among the surest bows of the Galadhrim; it is said no shaft of his has ever been found far from a foe.'),
    ('lord_L2_5', 'Nimlothiel', 'F', 26, 'earnest', 'elf_lady', 'Infantry', 'clan_lothlorien_2',
     'Nimlothiel orders the provisioning of the wardens upon the borders, and the talans of the Naith stand ready at her word.'),
    ('lord_L3_1', 'Malthorn', 'M', 41, 'ironic', 'elf_lord', 'Infantry', 'clan_lothlorien_3',
     'Malthorn leads Nos Malgalad, a house that has not forgotten Amdir who fell at Dagorlad, and keeps that memory sharp upon its spears.'),
    ('lord_L3_2', 'Galuvir', 'M', 24, 'curt', 'elf_warrior', 'Infantry', 'clan_lothlorien_3',
     'Galuvir of the Egladil drills the young of the Golden Wood in blade and bow against the gathering dark in Dol Guldur.'),
    ('lord_L3_3', 'Silivren', 'F', 29, 'softspoken', 'elf_lady', 'Infantry', 'clan_lothlorien_3',
     'Silivren tends the stores and songs of Nos Malgalad alike, for in Lorien the two have never been far apart.'),
    ('lord_R2_2', 'Gildor', 'M', 38, 'earnest', 'elf_warrior', 'Infantry', 'clan_rivendell_2',
     'Gildor Inglorion of the House of Finrod has long wandered the westlands; in these darkening days his company keeps to Imladris and the banner of Glorfindel.'),
    ('lord_R2_3', 'Erestor', 'M', 44, 'softspoken', 'elf_lord', 'Infantry', 'clan_rivendell_2',
     'Erestor, chief of the counsellors of the house of Elrond, sets aside the ledger for the sword when the passes of the Misty Mountains grow restless.'),
    ('lord_R2_4', 'Lindir', 'M', 21, 'curt', 'elf_warrior', 'Infantry', 'clan_rivendell_2',
     'Lindir of Imladris, quicker to song than to war, has nonetheless learned the ways of blade and bow as the Wild grows bold.'),
]

NEW_CLANS = [
    # id, name, tier, owner, banner donor clan
    ('clan_lothlorien_2', 'Wardens of the Naith', 6, 'lord_L2_2', 'clan_rivendell_2'),
    ('clan_lothlorien_3', 'Nos Malgalad', 5, 'lord_L3_1', 'clan_lindon_2'),
]

ARCH_TO_SET = {
    'elf_lord': 'taom_elf_lord_skills',
    'elf_warrior': 'taom_elf_warrior_skills',
    'elf_archer': 'taom_elf_archer_skills',
    'elf_lady': 'taom_elf_lady_skills',
}
CULTURE_OF_CLAN = {
    'clan_lothlorien_2': 'lothlorien', 'clan_lothlorien_3': 'lothlorien',
    'clan_rivendell_2': 'rivendell',
}


def load_set_values():
    root = ET.parse(SETS).getroot()
    return {ss.get('id'): {s.get('id'): int(s.get('value')) for s in ss.findall('skill')}
            for ss in root.findall('.//SkillSet')}


def harvest_face_keys(lords_text):
    mkeys, fkeys = [], []
    for m in re.finditer(r'<NPCCharacter\b[^>]*race="elf"[^>]*>.*?</NPCCharacter>', lords_text, re.DOTALL):
        head = re.search(r'<NPCCharacter[^>]*>', m.group(0)).group(0)
        k = re.search(r'key="([0-9A-F]{100,})"', m.group(0))
        if not k:
            continue
        (fkeys if 'is_female="true"' in head else mkeys).append(k.group(1))
    return list(dict.fromkeys(mkeys)), list(dict.fromkeys(fkeys))


def npc_block(lid, name, gender, age, voice, arch, group, clan, set_values, face_key, eq_variant):
    culture = CULTURE_OF_CLAN[clan]
    set_id = ARCH_TO_SET[arch]
    skills = set_values[set_id]
    traits = BASE_ARCHETYPES[arch]['traits']
    female = ' is_female="true"' if gender == 'F' else ' is_female="false"'
    lines = [
        f'    <NPCCharacter id="{lid}" race="elf" name="{{=aom_{lid}_name}}{name}" age="{age}" voice="{voice}"'
        f' is_hero="true"{female} culture="Culture.{culture}" occupation="Lord" default_group="{group}"'
        f' face_mesh_cache="true" skill_template="SkillSet.{set_id}">',
        '        <face>',
        f'            <BodyProperties version="4" age="{age}.0" weight="0.25" build="0.45" key="{face_key}" />',
        '            <hair_tags>',
        '                <hair_tag name="battania" />',
        '            </hair_tags>',
        '            <beard_tags>',
        '                <beard_tag name="battania" />',
        '            </beard_tags>',
        '            <tattoo_tags>',
        '                <tattoo_tag name="Cleanface" />',
        '            </tattoo_tags>',
        '        </face>',
        '        <skills>',
    ]
    for s in SKILL_ORDER:
        lines.append(f'            <skill id="{s}" value="{skills.get(s, 0)}" />')
    lines.append('        </skills>')
    lines.append('        <Traits>')
    for t in TRAIT_ORDER:
        lines.append(f'            <Trait id="{t}" value="{traits.get(t, 0)}" />')
    lines.append('        </Traits>')
    lines.append('        <Equipments>')
    lines.append(f'            <EquipmentSet id="{culture}_bat_template_medium_{eq_variant}" />')
    lines.append(f'            <EquipmentSet id="{culture}_civ_template_default_{eq_variant}" equipmentType="Civilian" />')
    lines.append('        </Equipments>')
    lines.append('    </NPCCharacter>')
    return '\n'.join(lines)


def hero_entry(lid, clan, blurb):
    return (f'<Hero\n\t\tid="{lid}"\n\t\tfaction="Faction.{clan}"\n\t\t'
            f'text="{{=aom_{lid}_desc}}{blurb}" />')


def clan_block(cid, cname, tier, owner, banner_key):
    return ('  <Faction\n'
            f'\t\tid="{cid}"\n'
            '\t\tinitial_home_settlement="Settlement.town_L1"\n'
            f'\t\tname="{{=aom_{cid}_name}}{cname}"\n'
            f'\t\ttier="{tier}"\n'
            f'\t\towner="Hero.{owner}"\n'
            '\t\tculture="Culture.lothlorien"\n'
            '\t\tsuper_faction="Kingdom.lothlorien"\n'
            '\t\tis_noble="true"\n'
            '\t\tcolor="FF184031"\n'
            '\t\tcolor2="FFC0E3B5"\n'
            '\t\tdefault_party_template="PartyTemplate.kingdom_hero_party_lothlorien_lothlorien_1_template"\n'
            f'\t\tbanner_key="{banner_key}" />')


def insert_after(text, anchor_pattern, insertion, what):
    m = re.search(anchor_pattern, text, re.DOTALL)
    assert m, f'anchor not found: {what}'
    return text[:m.end()] + '\n' + insertion + text[m.end():]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--apply', action='store_true')
    args = ap.parse_args()

    lords = LORDS.read_text(encoding='utf-8')
    heroes = HEROES.read_text(encoding='utf-8')
    clans = CLANS.read_text(encoding='utf-8')
    set_values = load_set_values()
    mkeys, fkeys = harvest_face_keys(lords)
    print(f'face key donors: {len(mkeys)} male, {len(fkeys)} female')

    for lid, *_ in NEW_LORDS:
        assert f'id="{lid}"' not in lords, f'{lid} already exists in lords.xml'
        assert f'id="{lid}"' not in heroes, f'{lid} already exists in heroes.xml'
    for cid, *_ in NEW_CLANS:
        assert f'id="{cid}"' not in clans, f'{cid} already exists'

    # --- clans.xml: 2 new lothlorien clans, banner keys donated from existing clans ---
    def donor_banner(donor_id):
        m = re.search(rf'id="{donor_id}".*?banner_key="([^"]+)"', clans, re.DOTALL)
        assert m, f'donor banner {donor_id}'
        return m.group(1)
    clan_xml = '\n'.join(clan_block(cid, cn, t, ow, donor_banner(d)) for cid, cn, t, ow, d in NEW_CLANS)
    clans_new = insert_after(clans, r'id="clan_lothlorien_1".*?/>', clan_xml, 'clan_lothlorien_1')

    # --- lords.xml: blocks inserted after a same-culture sibling ---
    mi = fi = 0
    eq = 'abcde'
    loth_blocks, riv_blocks = [], []
    for i, (lid, name, g, age, voice, arch, group, clan, blurb) in enumerate(NEW_LORDS):
        if g == 'M':
            key = mkeys[mi % len(mkeys)]; mi += 1
        else:
            key = fkeys[fi % len(fkeys)]; fi += 1
        block = npc_block(lid, name, g, age, voice, arch, group, clan, set_values, key, eq[i % 5])
        (loth_blocks if clan.startswith('clan_lothlorien') else riv_blocks).append(block)
    lords_new = insert_after(lords, r'<NPCCharacter\s+id="lord_L2_1".*?</NPCCharacter>',
                             '\n'.join(loth_blocks), 'lord_L2_1 npc')
    lords_new = insert_after(lords_new, r'<NPCCharacter\s+id="lord_R2_11".*?</NPCCharacter>',
                             '\n'.join(riv_blocks), 'lord_R2_11 npc')

    # --- heroes.xml: entries + move lord_L2_1 into clan_lothlorien_2 ---
    hero_xml_loth = '\n'.join(hero_entry(lid, clan, blurb) for lid, _, _, _, _, _, _, clan, blurb in NEW_LORDS
                              if clan.startswith('clan_lothlorien'))
    hero_xml_riv = '\n'.join(hero_entry(lid, clan, blurb) for lid, _, _, _, _, _, _, clan, blurb in NEW_LORDS
                             if clan.startswith('clan_rivendell'))
    heroes_new = insert_after(heroes, r'<Hero\b[^>]*id="lord_L2_1"[^>]*/>', hero_xml_loth, 'lord_L2_1 hero')
    heroes_new = insert_after(heroes_new, r'<Hero\b[^>]*id="lord_R2_11"[^>]*/>', hero_xml_riv, 'lord_R2_11 hero')
    l21 = re.search(r'<Hero\b[^>]*id="lord_L2_1"[^>]*/>', heroes_new).group(0)
    assert 'faction="Faction.clan_lothlorien_1"' in l21, 'lord_L2_1 hero not in clan 1?'
    heroes_new = heroes_new.replace(
        l21, l21.replace('faction="Faction.clan_lothlorien_1"', 'faction="Faction.clan_lothlorien_2"'), 1)
    print('moved lord_L2_1 (Caurminas) -> clan_lothlorien_2')

    # well-formedness gate before writing
    for label, txt in (('lords', lords_new), ('heroes', heroes_new), ('clans', clans_new)):
        ET.fromstring(txt.encode('utf-8'))
        print(f'{label}.xml: well-formed with insertions')

    print(f'new lords: {len(NEW_LORDS)} ({len(loth_blocks)} lothlorien, {len(riv_blocks)} rivendell); new clans: {len(NEW_CLANS)}')
    if args.apply:
        LORDS.write_text(lords_new, encoding='utf-8')
        HEROES.write_text(heroes_new, encoding='utf-8')
        CLANS.write_text(clans_new, encoding='utf-8')
        print('WROTE lords.xml + heroes.xml + clans.xml')
    else:
        print('(dry-run — pass --apply to write)')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
