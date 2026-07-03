#!/usr/bin/env python3
"""Repoint evil-faction lords onto the low-Leadership SkillSet variants (one-off, 2026-07-02).

Companion to the north-orc / dunland Leadership nerf in tools/apply_culture_skills_traits.py
(new `north_orc_*` + `dunland_*` archetypes + `archetype_alias`). This script does the narrow
skill_template swap the generator would do via process_file — WITHOUT re-running per-NPC
archetype resolution, which is unsafe here: the live XML carries hand-tuned assignments the
generator can't reproduce (149-lord drift documented in commit 1f7a7a9a), and goblin +
mistymountainorcs have no CULTURES entry at all.

Per NPCCharacter of the five target cultures (lords.xml attrs + lords.xslt xsl:attribute):
  - swap skill_template per SWAP_MAP (orc trio -> north_orc trio; dunland shared sets -> dunland_* variants);
  - set the inline <skill id="Leadership"> to the resolved set's value (documentation parity —
    the engine reads only the SkillSet; this keeps analyze_lord_balance.py's mismatch check clean).
    Also applied to lords on in-place-nerfed sets (dunland_warrior/brenin, canonical lord_G4_1).

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
# culture attr value -> swap map (dunland uses the repurposed vanilla empire culture)
CULTURE_SWAPS = {
    'Culture.goblin': ORC_SWAPS,
    'Culture.mistymountainorcs': ORC_SWAPS,
    'Culture.gundabad': ORC_SWAPS,
    'Culture.dolguldur': ORC_SWAPS,
    'Culture.empire': DUNLAND_SWAPS,
}
# Leadership value per FINAL template (swapped targets + in-place-nerfed sets)
LEADERSHIP_BY_TEMPLATE = {
    'SkillSet.taom_north_orc_chieftain_skills': 175,
    'SkillSet.taom_north_orc_warrior_skills': 75,
    'SkillSet.taom_north_orc_female_skills': 60,
    'SkillSet.taom_dunland_knight_skills': 90,
    'SkillSet.taom_dunland_lady_skills': 80,
    'SkillSet.taom_dunland_young_lord_skills': 55,
    'SkillSet.taom_dunland_young_lady_skills': 55,
    'SkillSet.taom_dunland_marauder_skills': 80,
    'SkillSet.taom_dunland_warrior_skills': 100,
    'SkillSet.taom_dunland_brenin_skills': 130,
    'SkillSet.taom_canonical_lord_G4_1_skills': 185,
}

XML_BLOCK = re.compile(r'<NPCCharacter\s+id="([^"]+)"[^>]*>.*?</NPCCharacter>', re.DOTALL)
XSLT_BLOCK = re.compile(r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]\">.*?</xsl:template>", re.DOTALL)
LEADERSHIP_LINE = re.compile(r'(<skill id="Leadership" value=")(\d+)(")')


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
        culture = block_culture(block, mode)
        swaps = CULTURE_SWAPS.get(culture)
        if swaps:
            template = block_template(block, mode)
            new_template = swaps.get(template, template)
            if new_template != template:
                block = block.replace(template, new_template)
                stats[f'{label}:{template.split(".")[-1]}->{new_template.split(".")[-1]}'] = \
                    stats.get(f'{label}:{template.split(".")[-1]}->{new_template.split(".")[-1]}', 0) + 1
                template = new_template
            led = LEADERSHIP_BY_TEMPLATE.get(template)
            if led is not None:
                block, n = LEADERSHIP_LINE.subn(rf'\g<1>{led}\g<3>', block)
                if n:
                    stats[f'{label}:inline-leadership'] = stats.get(f'{label}:inline-leadership', 0) + n
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
    print(f'TOTAL template swaps: {total_swaps}, inline Leadership updates: '
          f'{sum(v for k, v in stats.items() if "inline" in k)}')

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
