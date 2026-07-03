#!/usr/bin/env python3
"""Repoint/parity pass for the 2026-07-02 lord army-size rebalance (one-off).

Companion to tools/apply_culture_skills_traits.py changes: the evil-faction Leadership nerf
(new `north_orc_*` + `dunland_*` archetypes + `archetype_alias`, #322) and the elf Steward
boost (+100 on the elf archetypes + elf canonical sets). This script does the narrow
skill_template swap / inline-parity work the generator would do via process_file — WITHOUT
re-running per-NPC archetype resolution, which is unsafe here: the live XML carries hand-tuned
assignments the generator can't reproduce (149-lord drift documented in commit 1f7a7a9a), and
goblin + mistymountainorcs have no CULTURES entry at all.

Per NPCCharacter of the target cultures (lords.xml attrs + lords.xslt xsl:attribute):
  - swap skill_template per the culture's swap map (orc trio -> north_orc trio; dunland shared
    sets -> dunland_* variants; elf cultures have no swaps — their sets were edited in place);
  - set inline <skill> values to the resolved set's values per PARITY_BY_TEMPLATE (documentation
    parity — the engine reads only the SkillSet; keeps analyze_lord_balance.py's mismatch check clean);
  - apply INLINE_OVERRIDES for lords with NO skill_template, where the inline block IS
    engine-authoritative (lord_R3_1: Steward 200 -> 300, absolute so re-runs are idempotent).

NOTE: Culture.battania is Khand (Variags — evil), NOT Mirkwood; the stale battania->mirkwood
entry in rebalance_lords.CULTURE_MAP mislabels those 41 lords in reports. Khand is untouched here.

Usage:
    python tools/repoint_evil_lord_skillsets.py            # dry-run (default)
    python tools/repoint_evil_lord_skillsets.py --apply
"""
import argparse
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LORDS_XML = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"
LORDS_XSLT = REPO / "Main" / "_Module" / "ModuleData" / "lords.xslt"

ORC_SWAPS = {
    'SkillSet.taom_orc_chieftain_skills': 'SkillSet.taom_north_orc_chieftain_skills',
    'SkillSet.taom_orc_warrior_skills': 'SkillSet.taom_north_orc_warrior_skills',
    'SkillSet.taom_orc_female_skills': 'SkillSet.taom_north_orc_female_skills',
}
DUNLAND_SWAPS = {
    'SkillSet.taom_knight_skills': 'SkillSet.taom_dunland_knight_skills',
    'SkillSet.taom_lady_skills': 'SkillSet.taom_dunland_lady_skills',
    'SkillSet.taom_young_lord_skills': 'SkillSet.taom_dunland_young_lord_skills',
    'SkillSet.taom_young_lady_skills': 'SkillSet.taom_dunland_young_lady_skills',
    'SkillSet.taom_dunland_raider_skills': 'SkillSet.taom_dunland_marauder_skills',
}
# culture attr value -> swap map (dunland uses the repurposed vanilla empire culture;
# elf cultures get inline parity only — their sets were +100-Steward'd in place)
CULTURE_SWAPS = {
    'Culture.goblin': ORC_SWAPS,
    'Culture.mistymountainorcs': ORC_SWAPS,
    'Culture.gundabad': ORC_SWAPS,
    'Culture.dolguldur': ORC_SWAPS,
    'Culture.empire': DUNLAND_SWAPS,
    'Culture.rivendell': {},
    'Culture.lothlorien': {},
    'Culture.mirkwood': {},
}
# inline-skill values per FINAL template (swapped targets + in-place-edited sets)
PARITY_BY_TEMPLATE = {
    # evil-faction Leadership nerf (#322)
    'SkillSet.taom_north_orc_chieftain_skills': {'Leadership': 175},
    'SkillSet.taom_north_orc_warrior_skills': {'Leadership': 75},
    'SkillSet.taom_north_orc_female_skills': {'Leadership': 60},
    'SkillSet.taom_dunland_knight_skills': {'Leadership': 90},
    'SkillSet.taom_dunland_lady_skills': {'Leadership': 80},
    'SkillSet.taom_dunland_young_lord_skills': {'Leadership': 55},
    'SkillSet.taom_dunland_young_lady_skills': {'Leadership': 55},
    'SkillSet.taom_dunland_marauder_skills': {'Leadership': 80},
    'SkillSet.taom_dunland_warrior_skills': {'Leadership': 100},
    'SkillSet.taom_dunland_brenin_skills': {'Leadership': 130},
    'SkillSet.taom_canonical_lord_G4_1_skills': {'Leadership': 185},
    # elf Steward boost (+100, party size = 0.25/point of Steward)
    'SkillSet.taom_elf_king_skills': {'Steward': 385},
    'SkillSet.taom_elf_queen_skills': {'Steward': 390},
    'SkillSet.taom_elf_lady_skills': {'Steward': 365},
    'SkillSet.taom_elf_lord_skills': {'Steward': 355},
    'SkillSet.taom_elf_warrior_skills': {'Steward': 300},
    'SkillSet.taom_elf_archer_skills': {'Steward': 300},
    'SkillSet.taom_elf_young_skills': {'Steward': 290},
    'SkillSet.taom_canonical_lord_L1_1_skills': {'Steward': 415},
    'SkillSet.taom_canonical_lord_L1_2_skills': {'Steward': 350},
    'SkillSet.taom_canonical_lord_M1_1_skills': {'Steward': 360},
    'SkillSet.taom_canonical_lord_M1_11_skills': {'Steward': 338},
    'SkillSet.taom_canonical_lord_R1_1_skills': {'Steward': 400},
    'SkillSet.taom_canonical_lord_R1_3_skills': {'Steward': 300},
    'SkillSet.taom_canonical_lord_R1_4_skills': {'Steward': 300},
    'SkillSet.taom_canonical_lord_R1_5_skills': {'Steward': 365},
    'SkillSet.taom_canonical_lord_R2_1_skills': {'Steward': 342},
}
# Lords with NO skill_template: the inline block IS engine-authoritative. Absolute values.
INLINE_OVERRIDES = {
    'lord_R3_1': {'Steward': 300},  # rivendell, template-less; 200 + 100
}

XML_BLOCK = re.compile(r'<NPCCharacter\s+id="([^"]+)"[^>]*>.*?</NPCCharacter>', re.DOTALL)
XSLT_BLOCK = re.compile(r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]\">.*?</xsl:template>", re.DOTALL)


def skill_line(skill: str) -> re.Pattern:
    return re.compile(rf'(<skill id="{skill}" value=")(\d+)(")')


def block_culture(block: str, mode: str) -> str:
    if mode == 'xml':
        m = re.search(r'culture="([^"]+)"', block)
    else:
        m = re.search(r'<xsl:attribute name="culture">([^<]+)</xsl:attribute>', block)
    return m.group(1) if m else ''


def block_template(block: str, mode: str) -> str:
    if mode == 'xml':
        m = re.search(r'skill_template="([^"]+)"', block)
    else:
        m = re.search(r'<xsl:attribute name="skill_template">([^<]+)</xsl:attribute>', block)
    return m.group(1) if m else ''


def process(text: str, mode: str, label: str, stats: dict) -> str:
    pattern = XML_BLOCK if mode == 'xml' else XSLT_BLOCK
    out, last = [], 0
    for m in pattern.finditer(text):
        block = m.group(0)
        npc_id = m.group(1)
        culture = block_culture(block, mode)
        swaps = CULTURE_SWAPS.get(culture)
        if swaps is not None:
            template = block_template(block, mode)
            new_template = swaps.get(template, template)
            if new_template != template:
                block = block.replace(template, new_template)
                stats[f'{label}:{template.split(".")[-1]}->{new_template.split(".")[-1]}'] = \
                    stats.get(f'{label}:{template.split(".")[-1]}->{new_template.split(".")[-1]}', 0) + 1
                template = new_template
            for skill, value in PARITY_BY_TEMPLATE.get(template, {}).items():
                block, n = skill_line(skill).subn(rf'\g<1>{value}\g<3>', block)
                if n:
                    stats[f'{label}:inline-{skill.lower()}'] = stats.get(f'{label}:inline-{skill.lower()}', 0) + n
            if not template:
                for skill, value in INLINE_OVERRIDES.get(npc_id, {}).items():
                    block, n = skill_line(skill).subn(rf'\g<1>{value}\g<3>', block)
                    if n:
                        stats[f'{label}:override-{npc_id}-{skill.lower()}'] = n
        out.append(text[last:m.start()])
        out.append(block)
        last = m.end()
    out.append(text[last:])
    return ''.join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--apply', action='store_true')
    args = ap.parse_args()

    stats = {}
    xml_text = LORDS_XML.read_text(encoding='utf-8')
    xslt_text = LORDS_XSLT.read_text(encoding='utf-8')
    new_xml = process(xml_text, 'xml', 'lords.xml', stats)
    new_xslt = process(xslt_text, 'xslt', 'lords.xslt', stats)

    for key in sorted(stats):
        print(f'  {key}: {stats[key]}')
    total_swaps = sum(v for k, v in stats.items() if '->' in k)
    print(f'TOTAL template swaps: {total_swaps}, inline parity updates: '
          f'{sum(v for k, v in stats.items() if "inline" in k or "override" in k)}')

    # Post-condition: no target-culture lord may still reference a swapped-away set.
    leftovers = 0
    for text, mode, label in ((new_xml, 'xml', 'lords.xml'), (new_xslt, 'xslt', 'lords.xslt')):
        pattern = XML_BLOCK if mode == 'xml' else XSLT_BLOCK
        for m in pattern.finditer(text):
            culture = block_culture(m.group(0), mode)
            swaps = CULTURE_SWAPS.get(culture)
            if swaps and block_template(m.group(0), mode) in swaps:
                print(f'  LEFTOVER: {label} {m.group(1)} ({culture})')
                leftovers += 1
    print('post-condition:', 'PASS' if leftovers == 0 else f'FAIL ({leftovers} leftovers)')

    if args.apply:
        LORDS_XML.write_text(new_xml, encoding='utf-8')
        LORDS_XSLT.write_text(new_xslt, encoding='utf-8')
        print('WROTE lords.xml + lords.xslt')
    else:
        print('(dry-run — pass --apply to write)')
    return 0 if leftovers == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
