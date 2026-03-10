#!/usr/bin/env python3
"""
Troop Skill Rebalancing Script for TAOM

Applies uniform baseline + cultural modifier formula to all troops.
Ensures cultures are within 5-10 skill points of each other per level/group,
with elven factions 25-50 points above baseline.

Usage:
    python rebalance_troops.py --dry-run    # Preview changes
    python rebalance_troops.py --apply      # Write changes to XML files
"""

import xml.etree.ElementTree as ET
import os
import sys
import glob
import re
import copy
from collections import defaultdict

TROOPS_DIR = os.path.join(os.path.dirname(__file__), '..', 'Main', '_Module', 'ModuleData', 'troops')
SKILL_NAMES = ['Athletics', 'Riding', 'OneHanded', 'TwoHanded', 'Polearm', 'Bow', 'Crossbow', 'Throwing']

# Skip the old rhun file (replaced by rhun_new)
SKIP_FILES = {'troops_rhun.xml'}

# =============================================================================
# Baseline Skill Tables (center values per level per group)
# =============================================================================

INFANTRY_BASELINES = {
    1:  {'Athletics': 25, 'Riding': 5,  'OneHanded': 25,  'TwoHanded': 15,  'Polearm': 20,  'Bow': 5,  'Crossbow': 0,  'Throwing': 10},
    6:  {'Athletics': 33, 'Riding': 5,  'OneHanded': 28,  'TwoHanded': 18,  'Polearm': 28,  'Bow': 5,  'Crossbow': 0,  'Throwing': 10},
    11: {'Athletics': 48, 'Riding': 10, 'OneHanded': 60,  'TwoHanded': 55,  'Polearm': 70,  'Bow': 10, 'Crossbow': 5,  'Throwing': 20},
    16: {'Athletics': 80, 'Riding': 15, 'OneHanded': 90,  'TwoHanded': 85,  'Polearm': 95,  'Bow': 15, 'Crossbow': 10, 'Throwing': 30},
    21: {'Athletics': 95, 'Riding': 15, 'OneHanded': 125, 'TwoHanded': 110, 'Polearm': 130, 'Bow': 15, 'Crossbow': 10, 'Throwing': 50},
    26: {'Athletics': 100,'Riding': 20, 'OneHanded': 175, 'TwoHanded': 155, 'Polearm': 170, 'Bow': 20, 'Crossbow': 15, 'Throwing': 70},
    31: {'Athletics': 120,'Riding': 25, 'OneHanded': 230, 'TwoHanded': 210, 'Polearm': 235, 'Bow': 25, 'Crossbow': 20, 'Throwing': 80},
    36: {'Athletics': 140,'Riding': 30, 'OneHanded': 270, 'TwoHanded': 250, 'Polearm': 275, 'Bow': 30, 'Crossbow': 25, 'Throwing': 90},
    41: {'Athletics': 160,'Riding': 35, 'OneHanded': 310, 'TwoHanded': 290, 'Polearm': 310, 'Bow': 35, 'Crossbow': 25, 'Throwing': 100},
    46: {'Athletics': 175,'Riding': 40, 'OneHanded': 330, 'TwoHanded': 310, 'Polearm': 330, 'Bow': 40, 'Crossbow': 30, 'Throwing': 100},
    51: {'Athletics': 190,'Riding': 40, 'OneHanded': 350, 'TwoHanded': 330, 'Polearm': 350, 'Bow': 40, 'Crossbow': 30, 'Throwing': 100},
}

RANGED_BASELINES = {
    6:  {'Athletics': 40, 'Riding': 5,  'OneHanded': 20,  'TwoHanded': 10,  'Polearm': 20,  'Bow': 30,  'Crossbow': 5,  'Throwing': 10},
    11: {'Athletics': 50, 'Riding': 10, 'OneHanded': 30,  'TwoHanded': 25,  'Polearm': 30,  'Bow': 55,  'Crossbow': 15, 'Throwing': 20},
    16: {'Athletics': 85, 'Riding': 15, 'OneHanded': 70,  'TwoHanded': 45,  'Polearm': 45,  'Bow': 85,  'Crossbow': 25, 'Throwing': 30},
    21: {'Athletics': 95, 'Riding': 15, 'OneHanded': 100, 'TwoHanded': 65,  'Polearm': 70,  'Bow': 130, 'Crossbow': 30, 'Throwing': 40},
    26: {'Athletics': 105,'Riding': 20, 'OneHanded': 140, 'TwoHanded': 85,  'Polearm': 90,  'Bow': 170, 'Crossbow': 35, 'Throwing': 50},
    31: {'Athletics': 115,'Riding': 25, 'OneHanded': 165, 'TwoHanded': 100, 'Polearm': 100, 'Bow': 205, 'Crossbow': 40, 'Throwing': 60},
    36: {'Athletics': 130,'Riding': 30, 'OneHanded': 195, 'TwoHanded': 120, 'Polearm': 120, 'Bow': 245, 'Crossbow': 50, 'Throwing': 70},
    41: {'Athletics': 150,'Riding': 35, 'OneHanded': 220, 'TwoHanded': 140, 'Polearm': 140, 'Bow': 280, 'Crossbow': 55, 'Throwing': 80},
    46: {'Athletics': 165,'Riding': 40, 'OneHanded': 240, 'TwoHanded': 160, 'Polearm': 160, 'Bow': 300, 'Crossbow': 60, 'Throwing': 80},
    51: {'Athletics': 180,'Riding': 40, 'OneHanded': 260, 'TwoHanded': 170, 'Polearm': 170, 'Bow': 320, 'Crossbow': 65, 'Throwing': 80},
}

CAVALRY_BASELINES = {
    6:  {'Athletics': 50, 'Riding': 50,  'OneHanded': 30,  'TwoHanded': 10,  'Polearm': 30,  'Bow': 5,  'Crossbow': 0,  'Throwing': 10},
    11: {'Athletics': 60, 'Riding': 65,  'OneHanded': 55,  'TwoHanded': 35,  'Polearm': 60,  'Bow': 10, 'Crossbow': 5,  'Throwing': 15},
    16: {'Athletics': 85, 'Riding': 95,  'OneHanded': 80,  'TwoHanded': 50,  'Polearm': 100, 'Bow': 15, 'Crossbow': 10, 'Throwing': 25},
    21: {'Athletics': 100,'Riding': 120, 'OneHanded': 115, 'TwoHanded': 60,  'Polearm': 145, 'Bow': 25, 'Crossbow': 15, 'Throwing': 30},
    26: {'Athletics': 115,'Riding': 160, 'OneHanded': 170, 'TwoHanded': 80,  'Polearm': 195, 'Bow': 30, 'Crossbow': 20, 'Throwing': 40},
    31: {'Athletics': 130,'Riding': 210, 'OneHanded': 220, 'TwoHanded': 100, 'Polearm': 250, 'Bow': 35, 'Crossbow': 25, 'Throwing': 50},
    36: {'Athletics': 150,'Riding': 270, 'OneHanded': 270, 'TwoHanded': 130, 'Polearm': 300, 'Bow': 45, 'Crossbow': 30, 'Throwing': 60},
    41: {'Athletics': 170,'Riding': 320, 'OneHanded': 310, 'TwoHanded': 160, 'Polearm': 340, 'Bow': 50, 'Crossbow': 35, 'Throwing': 70},
    46: {'Athletics': 185,'Riding': 360, 'OneHanded': 340, 'TwoHanded': 180, 'Polearm': 370, 'Bow': 55, 'Crossbow': 40, 'Throwing': 70},
    51: {'Athletics': 200,'Riding': 400, 'OneHanded': 370, 'TwoHanded': 200, 'Polearm': 400, 'Bow': 60, 'Crossbow': 40, 'Throwing': 80},
}

HORSEARCHER_BASELINES = {
    6:  {'Athletics': 50,  'Riding': 50,  'OneHanded': 25,  'TwoHanded': 10,  'Polearm': 20,  'Bow': 40,  'Crossbow': 0,  'Throwing': 10},
    11: {'Athletics': 65,  'Riding': 75,  'OneHanded': 40,  'TwoHanded': 25,  'Polearm': 40,  'Bow': 65,  'Crossbow': 10, 'Throwing': 20},
    16: {'Athletics': 90,  'Riding': 110, 'OneHanded': 70,  'TwoHanded': 40,  'Polearm': 65,  'Bow': 100, 'Crossbow': 15, 'Throwing': 30},
    21: {'Athletics': 110, 'Riding': 150, 'OneHanded': 100, 'TwoHanded': 55,  'Polearm': 80,  'Bow': 145, 'Crossbow': 20, 'Throwing': 45},
    26: {'Athletics': 125, 'Riding': 200, 'OneHanded': 140, 'TwoHanded': 75,  'Polearm': 110, 'Bow': 195, 'Crossbow': 25, 'Throwing': 55},
    31: {'Athletics': 145, 'Riding': 255, 'OneHanded': 180, 'TwoHanded': 95,  'Polearm': 140, 'Bow': 245, 'Crossbow': 30, 'Throwing': 65},
    36: {'Athletics': 160, 'Riding': 310, 'OneHanded': 215, 'TwoHanded': 115, 'Polearm': 165, 'Bow': 290, 'Crossbow': 35, 'Throwing': 75},
    41: {'Athletics': 175, 'Riding': 350, 'OneHanded': 250, 'TwoHanded': 140, 'Polearm': 200, 'Bow': 320, 'Crossbow': 40, 'Throwing': 85},
    46: {'Athletics': 185, 'Riding': 380, 'OneHanded': 275, 'TwoHanded': 155, 'Polearm': 220, 'Bow': 345, 'Crossbow': 45, 'Throwing': 90},
}

GROUP_BASELINES = {
    'Infantry': INFANTRY_BASELINES,
    'Ranged': RANGED_BASELINES,
    'Cavalry': CAVALRY_BASELINES,
    'HorseArcher': HORSEARCHER_BASELINES,
}

# =============================================================================
# Cultural Modifiers
# =============================================================================

CULTURAL_MODS = {
    'erebor': {
        'OneHanded': 10, 'TwoHanded': 10, 'Throwing': 10,
        'Riding': -5, 'Bow': -5,
    },
    'iron_hills': {
        'OneHanded': 10, 'Polearm': 10, 'Crossbow': 10,
        'Riding': -5, 'Bow': -5,
    },
    'gondor': {
        'Polearm': 5, 'OneHanded': 5,
        'Throwing': -5,
    },
    'rohan': {
        'Riding': 10, 'Polearm': 10,
        'Crossbow': -5,
    },
    'isengard': {
        'Athletics': 5, 'OneHanded': 10, 'TwoHanded': 10,
        'Bow': -5, 'Riding': -5,
    },
    'mordor': {
        'OneHanded': 5, 'Throwing': 10,
        'Athletics': -5, 'Riding': -5, 'Bow': -5,
    },
    'harad': {
        'Riding': 10, 'Bow': 10,
        'Athletics': -5,
    },
    'rhun_new': {
        'Polearm': 10, 'TwoHanded': 5,
        'Bow': -5, 'Throwing': -5,
    },
    'dunland': {
        'Throwing': 10, 'TwoHanded': 5,
        'Riding': -5,
    },
    'dolguldur': {
        'Riding': 5,
        'Athletics': -5, 'Polearm': -5,
    },
    'gundabad': {
        'Athletics': 10, 'TwoHanded': 10,
        'Riding': -5, 'Bow': -5,
    },
    'rivendell': {
        'Athletics': 45, 'Riding': 45, 'OneHanded': 50, 'TwoHanded': 50,
        'Polearm': 50, 'Bow': 50, 'Crossbow': 0, 'Throwing': 45,
    },
    'mirkwood': {
        'Bow': 40, 'Athletics': 30, 'OneHanded': 25, 'TwoHanded': 25,
        'Polearm': 25, 'Riding': 20, 'Throwing': 20,
    },
    'lothlorien': {
        'Bow': 35, 'OneHanded': 35, 'TwoHanded': 35, 'Polearm': 35,
        'Athletics': 30, 'Riding': 25, 'Throwing': 25,
    },
    'umbar': {
        'OneHanded': 5, 'Polearm': 5,
        'Riding': -5, 'Bow': -5,
    },
}


def detect_culture(troop_id, filename_culture):
    """Detect the actual culture from troop ID, handling Iron Hills in erebor file."""
    if 'iron_hills' in troop_id or troop_id.startswith('iron_hills'):
        return 'iron_hills'
    return filename_culture


def detect_weapon_specialization(troop_id, troop_name):
    """
    Detect weapon specialization from troop name/id.
    Returns a dict of skill swaps to apply on top of the base formula.
    """
    name_lower = (troop_name + ' ' + troop_id).lower()
    swaps = {}

    # Crossbow specialists: swap Bow and Crossbow values
    if any(kw in name_lower for kw in ['crossbow', 'arbalest', 'naffatun']):
        swaps['_swap_bow_crossbow'] = True

    # Pike/Halberd/Spear specialists: boost Polearm, reduce OneHanded
    if any(kw in name_lower for kw in ['pike', 'halberd', 'spear', 'lance', 'glaive', 'mattock']):
        swaps['_boost_polearm'] = True

    # Sword specialists: boost OneHanded, reduce Polearm
    if any(kw in name_lower for kw in ['sword', 'blade']):
        swaps['_boost_onehanded'] = True

    # Axe specialists: boost TwoHanded
    if any(kw in name_lower for kw in ['axe', 'hammer', 'reaver']):
        swaps['_boost_twohanded'] = True

    # Shield specialists: boost OneHanded (shield implies 1H weapon)
    if 'shield' in name_lower and '_boost_polearm' not in swaps:
        swaps['_boost_onehanded'] = True

    return swaps


def apply_specialization(skills, specialization):
    """Apply weapon specialization swaps to calculated skills."""
    s = dict(skills)
    swap_amount = 15  # How much to shift between primary/secondary

    if specialization.get('_swap_bow_crossbow'):
        # Swap Bow and Crossbow values
        s['Bow'], s['Crossbow'] = s['Crossbow'], s['Bow']

    if specialization.get('_boost_polearm'):
        shift = min(swap_amount, s['OneHanded'])
        s['Polearm'] += shift
        s['OneHanded'] -= shift

    if specialization.get('_boost_onehanded'):
        shift = min(swap_amount, s['Polearm'])
        s['OneHanded'] += shift
        s['Polearm'] -= shift

    if specialization.get('_boost_twohanded'):
        shift = min(swap_amount, s['OneHanded'])
        s['TwoHanded'] += shift
        s['OneHanded'] -= shift

    # Ensure no negative values
    for k in SKILL_NAMES:
        s[k] = max(0, s[k])

    return s


def is_militia(troop_id, troop_name):
    """Check if a troop is a militia entry."""
    name_lower = (troop_name + ' ' + troop_id).lower()
    return 'militia' in name_lower and any(kw in name_lower for kw in ['spearman', 'archer', 'veteran'])


def calculate_skills(culture, level, group, troop_id, troop_name):
    """Calculate balanced skills for a troop."""
    baselines = GROUP_BASELINES.get(group)
    if not baselines:
        return None

    baseline = baselines.get(level)
    if not baseline:
        return None

    # For militia: use the level 21 baseline of Infantry or Ranged
    if is_militia(troop_id, troop_name):
        if 'archer' in (troop_name + ' ' + troop_id).lower():
            militia_baseline = RANGED_BASELINES.get(21)
        else:
            militia_baseline = INFANTRY_BASELINES.get(21)
        if militia_baseline:
            baseline = militia_baseline

    # Get cultural modifiers
    mods = CULTURAL_MODS.get(culture, {})

    # Calculate final skills
    skills = {}
    for skill in SKILL_NAMES:
        base_val = baseline[skill]
        mod_val = mods.get(skill, 0)
        skills[skill] = max(0, base_val + mod_val)

    # Apply weapon specialization
    specialization = detect_weapon_specialization(troop_id, troop_name)
    if specialization:
        skills = apply_specialization(skills, specialization)

    return skills


def get_display_name(name_attr):
    """Extract display name from localization tag like {=tag}Display Name."""
    if '}' in name_attr:
        return name_attr.split('}', 1)[1]
    return name_attr


def process_file(filepath, dry_run=True):
    """Process a single troop XML file. Returns list of change records."""
    filename = os.path.basename(filepath)
    if filename in SKIP_FILES:
        return []

    filename_culture = filename.replace('troops_', '').replace('.xml', '')

    # Parse preserving formatting
    tree = ET.parse(filepath)
    root = tree.getroot()

    changes = []

    for npc in root.findall('.//NPCCharacter'):
        troop_id = npc.get('id', '')
        troop_name = get_display_name(npc.get('name', ''))
        level = int(npc.get('level', '0'))
        group = npc.get('default_group', 'Infantry')

        culture = detect_culture(troop_id, filename_culture)

        # Get current skills
        old_skills = {}
        skills_elem = npc.find('skills')
        if skills_elem is not None:
            for s in skills_elem.findall('skill'):
                old_skills[s.get('id')] = int(s.get('value', '0'))

        # Calculate new skills
        new_skills = calculate_skills(culture, level, group, troop_id, troop_name)
        if new_skills is None:
            changes.append({
                'file': filename,
                'id': troop_id,
                'name': troop_name,
                'level': level,
                'group': group,
                'culture': culture,
                'status': 'SKIPPED (no baseline for level/group)',
                'old': old_skills,
                'new': None,
            })
            continue

        # Record changes
        has_change = any(old_skills.get(s, 0) != new_skills[s] for s in SKILL_NAMES)
        total_old = sum(old_skills.get(s, 0) for s in SKILL_NAMES)
        total_new = sum(new_skills[s] for s in SKILL_NAMES)

        changes.append({
            'file': filename,
            'id': troop_id,
            'name': troop_name,
            'level': level,
            'group': group,
            'culture': culture,
            'status': 'CHANGED' if has_change else 'UNCHANGED',
            'old': old_skills,
            'new': new_skills,
            'total_old': total_old,
            'total_new': total_new,
            'delta': total_new - total_old,
        })

        # Apply changes to XML if not dry run
        if not dry_run and has_change and skills_elem is not None:
            for s in skills_elem.findall('skill'):
                skill_id = s.get('id')
                if skill_id in new_skills:
                    s.set('value', str(new_skills[skill_id]))

    # Write back if not dry run
    if not dry_run:
        tree.write(filepath, encoding='utf-8', xml_declaration=True)

    return changes


def print_report(all_changes):
    """Print a formatted report of all changes."""
    by_level = defaultdict(list)
    for c in all_changes:
        by_level[c['level']].append(c)

    skipped = [c for c in all_changes if c['status'].startswith('SKIPPED')]
    changed = [c for c in all_changes if c['status'] == 'CHANGED']
    unchanged = [c for c in all_changes if c['status'] == 'UNCHANGED']

    print(f"\n{'='*120}")
    print(f"TROOP REBALANCING REPORT")
    print(f"{'='*120}")
    print(f"Total troops: {len(all_changes)}")
    print(f"Changed: {len(changed)}, Unchanged: {len(unchanged)}, Skipped: {len(skipped)}")
    print()

    if skipped:
        print(f"--- SKIPPED TROOPS ---")
        for c in skipped:
            print(f"  {c['name']:<45} Level {c['level']:>2} {c['group']:<14} {c['status']}")
        print()

    for level in sorted(by_level.keys()):
        level_changes = [c for c in by_level[level] if c['new'] is not None]
        if not level_changes:
            continue

        print(f"\n{'='*120}")
        print(f"LEVEL {level} ({len(level_changes)} troops)")
        print(f"{'='*120}")

        for group in ['Infantry', 'Ranged', 'Cavalry', 'HorseArcher']:
            group_changes = [c for c in level_changes if c['group'] == group]
            if not group_changes:
                continue

            print(f"\n  --- {group} ---")
            print(f"  {'Name':<45} {'Cult':<12} {'Ath':>4} {'Rid':>4} {'1H':>5} {'2H':>5} {'Pol':>5} {'Bow':>4} {'Xbw':>4} {'Thr':>4} {'Tot':>5} {'Chg':>6}")
            print(f"  {'-'*108}")

            for c in sorted(group_changes, key=lambda x: (x['culture'], x['name'])):
                n = c['new']
                delta_str = f"{c['delta']:+d}" if c['delta'] != 0 else "0"
                marker = " ***" if abs(c.get('delta', 0)) > 50 else ""
                print(f"  {c['name']:<45} {c['culture']:<12} "
                      f"{n['Athletics']:>4} {n['Riding']:>4} {n['OneHanded']:>5} {n['TwoHanded']:>5} "
                      f"{n['Polearm']:>5} {n['Bow']:>4} {n['Crossbow']:>4} {n['Throwing']:>4} "
                      f"{c['total_new']:>5} {delta_str:>6}{marker}")

    # Print big delta warnings
    big_deltas = [c for c in all_changes if c.get('delta') and abs(c['delta']) > 100]
    if big_deltas:
        print(f"\n{'='*120}")
        print(f"WARNING: {len(big_deltas)} troops with total skill change > 100 points")
        print(f"{'='*120}")
        for c in sorted(big_deltas, key=lambda x: abs(x['delta']), reverse=True):
            print(f"  {c['name']:<45} L{c['level']:>2} {c['culture']:<12} old={c['total_old']:>5} new={c['total_new']:>5} Δ={c['delta']:+d}")


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('--dry-run', '--apply'):
        print("Usage: python rebalance_troops.py --dry-run|--apply")
        sys.exit(1)

    dry_run = sys.argv[1] == '--dry-run'
    mode = "DRY RUN" if dry_run else "APPLYING CHANGES"
    print(f"\n*** {mode} ***\n")

    troop_files = sorted(glob.glob(os.path.join(TROOPS_DIR, 'troops_*.xml')))
    print(f"Found {len(troop_files)} troop files")

    all_changes = []
    for filepath in troop_files:
        filename = os.path.basename(filepath)
        if filename in SKIP_FILES:
            print(f"  SKIPPING {filename}")
            continue
        print(f"  Processing {filename}...")
        changes = process_file(filepath, dry_run=dry_run)
        all_changes.extend(changes)

    print_report(all_changes)

    if not dry_run:
        print(f"\n*** Changes written to {len(troop_files) - len(SKIP_FILES)} files ***")


if __name__ == '__main__':
    main()
