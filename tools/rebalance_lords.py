#!/usr/bin/env python3
"""
Lord Skill Rebalancing Script for TAOM

Applies baseline + cultural modifier + age scaling formula to all lords.
Reads from and writes to lords.xslt (after complete_lords_xslt.py has run).

Baselines derived from vanilla sandbox_skill_sets.xml, scaled up ~20% for TAOM
power level, then anchored to existing TAOM lord medians.

Usage:
    python rebalance_lords.py --dry-run          # Preview changes
    python rebalance_lords.py --apply            # Write to lords.xslt
    python rebalance_lords.py --export-csv       # Export full inventory to CSV
    python rebalance_lords.py --skills-only      # Only rebalance skills, skip traits
"""

import re
import os
import sys
import csv
from collections import defaultdict

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(SCRIPT_DIR, '..')
XSLT_PATH = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'lords.xslt')
CSV_PATH = os.path.join(SCRIPT_DIR, 'lords_balance.csv')

ALL_SKILLS = [
    'OneHanded', 'TwoHanded', 'Polearm', 'Bow', 'Crossbow', 'Throwing',
    'Riding', 'Athletics', 'Crafting', 'Scouting', 'Tactics', 'Roguery',
    'Charm', 'Leadership', 'Trade', 'Steward', 'Medicine', 'Engineering',
]

COMBAT_SKILLS = ALL_SKILLS[:8]
NONCOMBAT_SKILLS = ALL_SKILLS[8:]

# =============================================================================
# Archetype Detection from skill_template
# =============================================================================

def detect_archetype(skill_template):
    """Detect lord archetype and seniority from skill_template."""
    st = skill_template.lower().replace('skillset.', '')

    is_rookie = '_rookie' in st
    is_ruler = '_ruler' in st or '_clanleader' in st

    if is_ruler:
        archetype = 'ruler'
    elif any(k in st for k in ['knight', 'cavalry', 'faris']):
        archetype = 'warrior_knight'
    elif any(k in st for k in ['shock_troop', 'phalanx', 'berserker', 'swordsman', 'peltast', 'tribal_warrior']):
        archetype = 'warrior_infantry'
    elif any(k in st for k in ['archer', 'fian', 'huntress', 'mounted_archery', 'horse_archer']):
        archetype = 'warrior_ranged'
    elif any(k in st for k in ['politician', 'diplomat', 'courtier']):
        archetype = 'politician'
    elif any(k in st for k in ['chatelaine', 'quartermaster', 'matriarch']):
        archetype = 'manager'
    elif any(k in st for k in ['spy_master', 'intriguer']):
        archetype = 'spymaster'
    elif any(k in st for k in ['healer_scholar', 'physician']):
        archetype = 'scholar'
    elif any(k in st for k in ['trader', 'artisan']):
        archetype = 'trader'
    elif 'dandy' in st:
        archetype = 'dandy'
    elif any(k in st for k in ['tactician']):
        archetype = 'tactician'
    elif any(k in st for k in ['siege_engineering']):
        archetype = 'siege_engineer'
    else:
        archetype = 'warrior_knight'  # Default fallback

    return archetype, is_rookie, is_ruler


# =============================================================================
# Legendary Detection
# =============================================================================

LEGENDARY_IDS = {
    # Nazgul
    'lord_1_14',   # Mouth of Sauron
    'lord_1_15',   # Witch-King of Angmar
    'lord_1_155',  # Nazgul, The Dark Marshall
    'lord_1_16',   # Nazgul, The Knight of Umbar
    'lord_1_17',   # Sauron - Just To View Armor
    'lord_1_28',   # Nazgul, The Betrayer
    'lord_1_38',   # Nazgul, the Undying
    'lord_1_48_1', # Nazgul, the Tainted
    'lord_1_48_2', # Nazgul, the Shadow of Northmen
    'lord_1_48_3', # Nazgul, the Shadow of Umbar
}

LEGENDARY_NAME_PATTERNS = ['nazg', 'sauron', 'witch-king', 'witch_king']


def is_legendary(lord_id, name):
    """Check if a lord is legendary (Nazgul, Sauron, etc.)."""
    if lord_id in LEGENDARY_IDS:
        return True
    name_lower = name.lower()
    return any(p in name_lower for p in LEGENDARY_NAME_PATTERNS)


# =============================================================================
# Baseline Skill Tables
# =============================================================================
# Anchored to vanilla sandbox_skill_sets.xml medians, scaled ~20% up for TAOM.
# Senior baselines. Junior = ~60% of senior.
# Format: {skill_name: value}

BASELINES = {
    # --- Combat-focused archetypes ---
    'warrior_knight': {
        'OneHanded': 200, 'TwoHanded': 160, 'Polearm': 210, 'Bow': 50, 'Crossbow': 25, 'Throwing': 80,
        'Riding': 250, 'Athletics': 160,
        'Crafting': 50, 'Scouting': 150, 'Tactics': 180, 'Roguery': 40,
        'Charm': 140, 'Leadership': 180, 'Trade': 40, 'Steward': 110, 'Medicine': 80, 'Engineering': 40,
    },
    'warrior_infantry': {
        'OneHanded': 190, 'TwoHanded': 210, 'Polearm': 170, 'Bow': 60, 'Crossbow': 30, 'Throwing': 160,
        'Riding': 100, 'Athletics': 230,
        'Crafting': 50, 'Scouting': 130, 'Tactics': 140, 'Roguery': 100,
        'Charm': 60, 'Leadership': 110, 'Trade': 30, 'Steward': 60, 'Medicine': 120, 'Engineering': 40,
    },
    'warrior_ranged': {
        'OneHanded': 150, 'TwoHanded': 120, 'Polearm': 100, 'Bow': 250, 'Crossbow': 100, 'Throwing': 150,
        'Riding': 120, 'Athletics': 200,
        'Crafting': 100, 'Scouting': 200, 'Tactics': 150, 'Roguery': 60,
        'Charm': 80, 'Leadership': 120, 'Trade': 40, 'Steward': 60, 'Medicine': 80, 'Engineering': 50,
    },
    'tactician': {
        'OneHanded': 210, 'TwoHanded': 140, 'Polearm': 160, 'Bow': 150, 'Crossbow': 20, 'Throwing': 70,
        'Riding': 190, 'Athletics': 130,
        'Crafting': 30, 'Scouting': 200, 'Tactics': 250, 'Roguery': 30,
        'Charm': 160, 'Leadership': 210, 'Trade': 100, 'Steward': 180, 'Medicine': 180, 'Engineering': 170,
    },
    'siege_engineer': {
        'OneHanded': 120, 'TwoHanded': 80, 'Polearm': 170, 'Bow': 50, 'Crossbow': 40, 'Throwing': 120,
        'Riding': 110, 'Athletics': 200,
        'Crafting': 220, 'Scouting': 170, 'Tactics': 240, 'Roguery': 50,
        'Charm': 100, 'Leadership': 190, 'Trade': 130, 'Steward': 230, 'Medicine': 140, 'Engineering': 280,
    },

    # --- Non-combat-focused archetypes ---
    'ruler': {
        'OneHanded': 200, 'TwoHanded': 190, 'Polearm': 200, 'Bow': 160, 'Crossbow': 140, 'Throwing': 160,
        'Riding': 180, 'Athletics': 160,
        'Crafting': 140, 'Scouting': 170, 'Tactics': 210, 'Roguery': 170,
        'Charm': 200, 'Leadership': 210, 'Trade': 180, 'Steward': 210, 'Medicine': 140, 'Engineering': 170,
    },
    'politician': {
        'OneHanded': 130, 'TwoHanded': 70, 'Polearm': 80, 'Bow': 50, 'Crossbow': 30, 'Throwing': 40,
        'Riding': 150, 'Athletics': 100,
        'Crafting': 50, 'Scouting': 140, 'Tactics': 170, 'Roguery': 150,
        'Charm': 250, 'Leadership': 220, 'Trade': 190, 'Steward': 190, 'Medicine': 130, 'Engineering': 80,
    },
    'manager': {
        'OneHanded': 100, 'TwoHanded': 50, 'Polearm': 70, 'Bow': 50, 'Crossbow': 50, 'Throwing': 40,
        'Riding': 120, 'Athletics': 110,
        'Crafting': 190, 'Scouting': 150, 'Tactics': 170, 'Roguery': 80,
        'Charm': 220, 'Leadership': 210, 'Trade': 190, 'Steward': 250, 'Medicine': 200, 'Engineering': 160,
    },
    'spymaster': {
        'OneHanded': 160, 'TwoHanded': 80, 'Polearm': 70, 'Bow': 100, 'Crossbow': 60, 'Throwing': 180,
        'Riding': 160, 'Athletics': 170,
        'Crafting': 40, 'Scouting': 220, 'Tactics': 210, 'Roguery': 250,
        'Charm': 220, 'Leadership': 150, 'Trade': 140, 'Steward': 100, 'Medicine': 70, 'Engineering': 60,
    },
    'scholar': {
        'OneHanded': 120, 'TwoHanded': 60, 'Polearm': 60, 'Bow': 70, 'Crossbow': 30, 'Throwing': 40,
        'Riding': 110, 'Athletics': 130,
        'Crafting': 190, 'Scouting': 160, 'Tactics': 160, 'Roguery': 40,
        'Charm': 190, 'Leadership': 150, 'Trade': 100, 'Steward': 200, 'Medicine': 260, 'Engineering': 230,
    },
    'trader': {
        'OneHanded': 120, 'TwoHanded': 50, 'Polearm': 50, 'Bow': 40, 'Crossbow': 10, 'Throwing': 60,
        'Riding': 150, 'Athletics': 150,
        'Crafting': 100, 'Scouting': 210, 'Tactics': 170, 'Roguery': 170,
        'Charm': 230, 'Leadership': 200, 'Trade': 260, 'Steward': 240, 'Medicine': 120, 'Engineering': 80,
    },
    'dandy': {
        'OneHanded': 160, 'TwoHanded': 90, 'Polearm': 130, 'Bow': 90, 'Crossbow': 50, 'Throwing': 100,
        'Riding': 190, 'Athletics': 80,
        'Crafting': 70, 'Scouting': 140, 'Tactics': 60, 'Roguery': 160,
        'Charm': 250, 'Leadership': 140, 'Trade': 210, 'Steward': 190, 'Medicine': 110, 'Engineering': 40,
    },
}

# Legendary tier: 2.5x of ruler baseline
LEGENDARY_BASELINE = {skill: int(val * 2.5) for skill, val in BASELINES['ruler'].items()}

# Junior multiplier
JUNIOR_MULTIPLIER = 0.60


# =============================================================================
# Cultural Modifiers
# =============================================================================
# Vanilla culture -> TAOM culture mapping
CULTURE_MAP = {
    'Culture.empire': 'dunland',
    'Culture.sturgia': 'dale',
    'Culture.aserai': 'harad',
    'Culture.vlandia': 'rohan',
    'Culture.battania': 'mirkwood',
    'Culture.khuzait': 'rhun',
}

CULTURAL_MODS = {
    'dunland': {
        'Athletics': 15, 'OneHanded': 10, 'TwoHanded': 10, 'Throwing': 15,
        'Riding': -10, 'Bow': -5,
        'Scouting': 10, 'Roguery': 10, 'Leadership': -5,
    },
    'dale': {
        'OneHanded': 10, 'Crossbow': 10, 'Athletics': 5,
        'Trade': 15, 'Steward': 10, 'Engineering': 10,
    },
    'harad': {
        'Riding': 10, 'Bow': 15, 'OneHanded': 10,
        'TwoHanded': -10, 'Polearm': -5,
        'Trade': 15, 'Charm': 10,
    },
    'rohan': {
        'Riding': 25, 'Polearm': 15,
        'Athletics': -5, 'Bow': -10,
        'Tactics': 10, 'Leadership': 10, 'Charm': 5,
    },
    'mirkwood': {
        'Athletics': 25, 'Bow': 35, 'OneHanded': 20,
        'Riding': -10,
        'Scouting': 20, 'Medicine': 15, 'Crafting': 10,
    },
    'rhun': {
        'Riding': 20, 'Polearm': 15, 'Bow': 10,
        'Athletics': -5,
        'Trade': 10, 'Steward': 5,
    },
}


# =============================================================================
# Age Scaling
# =============================================================================

def age_multiplier(age):
    """Lords peak at ~40-50, decline slightly after 55."""
    if age < 25:
        return 0.70 + (age - 18) * 0.043  # 0.70 at 18, ~1.0 at 25
    elif age <= 50:
        return 1.0
    elif age <= 60:
        return 1.0 - (age - 50) * 0.005  # 1.0 to 0.95
    else:
        return 0.95 - (age - 60) * 0.01   # 0.95 declining


# =============================================================================
# Skill Calculation
# =============================================================================

def calculate_skills(archetype, is_rookie, is_ruler, culture, age, legendary):
    """Calculate balanced skills for a lord."""
    if legendary:
        baseline = dict(LEGENDARY_BASELINE)
    else:
        baseline = dict(BASELINES.get(archetype, BASELINES['warrior_knight']))

    # Apply junior multiplier
    if is_rookie and not legendary:
        baseline = {k: int(v * JUNIOR_MULTIPLIER) for k, v in baseline.items()}

    # Apply cultural modifiers
    taom_culture = CULTURE_MAP.get(culture, '')
    mods = CULTURAL_MODS.get(taom_culture, {})

    # Apply age scaling
    age_mult = age_multiplier(age)

    skills = {}
    for skill in ALL_SKILLS:
        base = baseline.get(skill, 0)
        mod = mods.get(skill, 0)
        val = int(base * age_mult + mod)
        skills[skill] = max(5, val)  # Minimum 5

    return skills


# =============================================================================
# XSLT Parsing and Writing
# =============================================================================

def parse_xslt(filepath):
    """Parse lords.xslt to extract all lord data."""
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    template_pattern = re.compile(
        r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]\">(.*?)</xsl:template>",
        re.DOTALL
    )

    lords = {}
    for match in template_pattern.finditer(content):
        lord_id = match.group(1)
        block = match.group(2)

        attrs = {}
        for attr_match in re.finditer(r'<xsl:attribute name="([^"]+)">([^<]*)</xsl:attribute>', block):
            attrs[attr_match.group(1)] = attr_match.group(2)

        # Extract current skills
        skills = {}
        for skill_match in re.finditer(r'<skill id="(\w+)" value="(\d+)"', block):
            skills[skill_match.group(1)] = int(skill_match.group(2))

        name = attrs.get('name', lord_id)
        if '}' in name:
            name = name.split('}', 1)[1]

        lords[lord_id] = {
            'attrs': attrs,
            'name': name,
            'current_skills': skills,
            'has_skills': bool(skills),
        }

    return lords, content


def build_skills_block(skills):
    """Build the <skills> XML block from a skill dict."""
    lines = ['            <skills>']
    for skill_id in ALL_SKILLS:
        val = skills.get(skill_id, 0)
        lines.append(f'                <skill id="{skill_id}" value="{val}"/>')
    lines.append('            </skills>')
    return '\n'.join(lines)


def apply_to_xslt(content, lord_skill_map):
    """Apply skill changes to XSLT content using regex."""
    for lord_id, new_skills in lord_skill_map.items():
        new_block = build_skills_block(new_skills)

        # Pattern 1: <skills>...</skills> (populated)
        pattern = re.compile(
            r"(NPCCharacter\[@id='" + re.escape(lord_id) + r"'\].*?)"
            r"(<skills>.*?</skills>)",
            re.DOTALL
        )
        match = pattern.search(content)
        if match:
            content = content[:match.start(2)] + new_block + content[match.end(2):]
            continue

        # Pattern 2: <skills/> (self-closing)
        pattern2 = re.compile(
            r"(NPCCharacter\[@id='" + re.escape(lord_id) + r"'\].*?)"
            r"(<skills\s*/>)",
            re.DOTALL
        )
        match2 = pattern2.search(content)
        if match2:
            content = content[:match2.start(2)] + new_block + content[match2.end(2):]
            continue

        print(f"  WARNING: Could not find skills block for {lord_id}")

    return content


# =============================================================================
# Reporting
# =============================================================================

def print_report(lords, changes, lord_skill_map):
    """Print dry-run report."""
    print('=' * 120)
    print('LORD REBALANCING REPORT')
    print('=' * 120)

    total = len(lords)
    skipped_dead = sum(1 for lid in lords if lid.startswith('dead_'))
    legendary_count = sum(1 for lid, c in changes.items() if c.get('legendary'))
    changed = len(lord_skill_map)
    skipped = total - changed - skipped_dead

    print(f'Total lords: {total}')
    print(f'Rebalanced:  {changed} (legendary: {legendary_count})')
    print(f'Skipped:     {skipped_dead} dead lords')
    print()

    # Group by archetype
    by_arch = defaultdict(list)
    for lord_id, change in changes.items():
        by_arch[change['archetype']].append((lord_id, change))

    for arch in ['ruler', 'warrior_knight', 'warrior_infantry', 'warrior_ranged', 'tactician',
                 'siege_engineer', 'politician', 'manager', 'spymaster', 'scholar', 'trader', 'dandy']:
        entries = by_arch.get(arch, [])
        if not entries:
            continue

        print(f'\n--- {arch.upper()} ({len(entries)} lords) ---')
        print(f"{'Name':<28} {'Jr':>2} {'Age':>3} {'Cul':<8} "
              f"{'1H':>4} {'2H':>4} {'Pol':>4} {'Bow':>4} {'Xbw':>4} {'Thr':>4} {'Rid':>4} {'Ath':>4} | "
              f"{'Crf':>4} {'Sct':>4} {'Tac':>4} {'Rog':>4} {'Chm':>4} {'Ldr':>4} {'Trd':>4} {'Stw':>4} {'Med':>4} {'Eng':>4} | "
              f"{'CMB':>5} {'NCM':>5} {'TOT':>5}")
        print('-' * 120)

        for lord_id, change in sorted(entries, key=lambda x: -sum(x[1]['skills'].values())):
            s = change['skills']
            jr = 'Jr' if change['is_rookie'] else ''
            name = change['name'][:27]
            culture = CULTURE_MAP.get(change['culture'], '?')[:7]
            combat = sum(s.get(sk, 0) for sk in COMBAT_SKILLS)
            noncom = sum(s.get(sk, 0) for sk in NONCOMBAT_SKILLS)
            leg = '*' if change.get('legendary') else ''
            print(f"{name + leg:<28} {jr:>2} {change['age']:>3} {culture:<8} "
                  f"{s.get('OneHanded',0):>4} {s.get('TwoHanded',0):>4} {s.get('Polearm',0):>4} "
                  f"{s.get('Bow',0):>4} {s.get('Crossbow',0):>4} {s.get('Throwing',0):>4} "
                  f"{s.get('Riding',0):>4} {s.get('Athletics',0):>4} | "
                  f"{s.get('Crafting',0):>4} {s.get('Scouting',0):>4} {s.get('Tactics',0):>4} "
                  f"{s.get('Roguery',0):>4} {s.get('Charm',0):>4} {s.get('Leadership',0):>4} "
                  f"{s.get('Trade',0):>4} {s.get('Steward',0):>4} {s.get('Medicine',0):>4} "
                  f"{s.get('Engineering',0):>4} | "
                  f"{combat:>5} {noncom:>5} {combat+noncom:>5}")


def export_csv(changes, csv_path):
    """Export balance data to CSV."""
    headers = ['id', 'name', 'archetype', 'is_rookie', 'legendary', 'age', 'culture',
               'default_group'] + ALL_SKILLS + ['combat_total', 'noncombat_total', 'grand_total']

    with open(csv_path, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(headers)

        for lord_id in sorted(changes.keys()):
            c = changes[lord_id]
            s = c['skills']
            combat = sum(s.get(sk, 0) for sk in COMBAT_SKILLS)
            noncom = sum(s.get(sk, 0) for sk in NONCOMBAT_SKILLS)
            row = [lord_id, c['name'], c['archetype'], 'Jr' if c['is_rookie'] else '',
                   'Yes' if c.get('legendary') else '', c['age'],
                   CULTURE_MAP.get(c['culture'], ''), c.get('default_group', '')]
            for skill in ALL_SKILLS:
                row.append(s.get(skill, 0))
            row.extend([combat, noncom, combat + noncom])
            writer.writerow(row)

    print(f'Exported to {csv_path}')


# =============================================================================
# Main
# =============================================================================

def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('--dry-run', '--apply', '--export-csv'):
        print(__doc__)
        sys.exit(1)

    mode = sys.argv[1]

    print(f'Parsing lords from {XSLT_PATH}...')
    lords, xslt_content = parse_xslt(XSLT_PATH)
    print(f'  Found {len(lords)} lord templates')

    # Calculate new skills for each lord
    changes = {}
    lord_skill_map = {}

    for lord_id, data in lords.items():
        if lord_id.startswith('dead_'):
            continue

        attrs = data['attrs']
        skill_template = attrs.get('skill_template', '')
        age = int(attrs.get('age', '30'))
        culture = attrs.get('culture', '')
        name = data['name']
        default_group = attrs.get('default_group', '')

        archetype, is_rookie, is_ruler_flag = detect_archetype(skill_template)
        legendary = is_legendary(lord_id, name)

        new_skills = calculate_skills(archetype, is_rookie, is_ruler_flag, culture, age, legendary)

        changes[lord_id] = {
            'name': name,
            'archetype': archetype,
            'is_rookie': is_rookie,
            'legendary': legendary,
            'age': age,
            'culture': culture,
            'default_group': default_group,
            'skills': new_skills,
            'had_skills': data['has_skills'],
        }
        lord_skill_map[lord_id] = new_skills

    print(f'  Calculated skills for {len(lord_skill_map)} lords')
    print()

    if mode == '--dry-run':
        print_report(lords, changes, lord_skill_map)

    elif mode == '--apply':
        new_content = apply_to_xslt(xslt_content, lord_skill_map)
        with open(XSLT_PATH, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f'Written to {XSLT_PATH}')
        print(f'  Lords rebalanced: {len(lord_skill_map)}')

    elif mode == '--export-csv':
        export_csv(changes, CSV_PATH)


if __name__ == '__main__':
    main()
