#!/usr/bin/env python3
"""
Gondor/Mordor armor parity fix (#342) — one-off, moves to tools/oneoff/ when done.

Users reported Mordor armor beating Gondor's. The curve (rebalance_armor.py) says Gondor +1
protection / Mordor -1, so Gondor should lead every tier by ~2 per slot. The live data drifted:
the Black Uruk set (sk_uruk_mordor_*_heavy_*) sits above even Mordor's ELITE targets, and
Gondor's keyword-tiered items sit at baseline+0 (authored before the cultural mod existed).

Owner decisions (2026-07-13, issue #342):
  * Mordor: CAP-ONLY — any keyword/roster-tiered combat item whose armor stats exceed its Mordor
    tier target is pulled down to target. Items below target keep their hand-authored values.
    The sk_uruk_mordor_*_heavy_* set is roster-ELITE gear (worn L26-36) — it caps at elite
    targets, not its '_heavy_' keyword.
  * Mordor caps apply ONLY to items worn exclusively by Mordor-culture troops (per the roster
    map). The mordor/ folder also hosts shared pools whose wearers are other cultures:
    sk_gn_orc_* + sk_md_orc_* (goblin/mistymountainorcs/isengard), urukscout_* (isengard),
    ar_ardunian_* (umbar) — capping those at Mordor's -1 curve would nerf those cultures as
    collateral. Shared-pool ties vs Gondor are broken by the Gondor +1 top-up instead (Gondor
    leads shared kit by 1, Mordor-exclusive kit by 2).
  * Gondor: TOP-UP-ONLY — a keyword-tiered combat stat sitting exactly at the plain baseline
    (the pre-mod authored value) rises to the Gondor curve target. Off-pattern regional specials
    (sk_gd_ser_*, etc.) are untouched.
  * Heroes / bosses / '_lord' items / weights / material_type / other cultures: never touched.

Usage:
    python tools/fix_gondor_mordor_armor_parity.py             # dry-run (default): print old->new
    python tools/fix_gondor_mordor_armor_parity.py --apply     # back up XMLs, then write
"""

import argparse
import json
import os
import shutil
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import rebalance_armor as ra  # noqa: E402  curve + regex writer (single source of truth)
import analyze_armor_balance as ab  # noqa: E402  hero/boss exclusion
from derive_armor_tiers import id_keyword_tier  # noqa: E402  the shared keyword detector

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..'))
ROSTER_MAP_JSON = os.path.join(REPO_ROOT, 'tools', 'data', 'armor_roster_tiers.json')
TROOPS_MORDOR_XML = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'troops',
                                 'troops_mordor.xml')
BACKUP_SUFFIX = '.bak-parity-20260713'

# Unworn items in the mordor/ folder are still cappable when their prefix is Mordor-exclusive
# (the painted-variant naming convention: MOR = Mordor-only, URUK_MORDOR = Black Uruk kit).
MORDOR_EXCLUSIVE_PREFIXES = ('sk_uruk_mordor_', 'sk_md_mor_')

SLOT_FILES = {
    'head': 'head_armors.xml',
    'body': 'body_armors.xml',
    'arm': 'arm_armors.xml',
    'leg': 'leg_armors.xml',
    'shoulder': 'shoulder_armors.xml',
}

# Armor stats governed by the curve, per slot (weight/material deliberately NOT here).
SLOT_STATS = {
    'head': ['head_armor'],
    'body': ['body_armor', 'leg_armor'],
    'arm': ['arm_armor'],
    'leg': ['leg_armor'],
    'shoulder': ['body_armor', 'arm_armor'],
}


def curve_targets(tier, slot, culture):
    """Curve target for each governed stat (drops weight/material from calculate_stats)."""
    stats = ra.calculate_stats(tier, slot, culture)
    return {s: stats[s] for s in SLOT_STATS[slot]}


def plain_baseline(tier, slot):
    """The uncultured baseline value per stat — Gondor's pre-mod authored value."""
    return {s: ra.SLOT_BASELINES[slot][tier][s] for s in SLOT_STATS[slot]}


def load_mordor_troop_ids():
    root = ET.parse(TROOPS_MORDOR_XML).getroot()
    return {npc.get('id') for npc in root.iter('NPCCharacter')}


def mordor_cap_eligible(item_id, roster_map, mordor_troops):
    """True only for items worn exclusively by Mordor troops (or unworn Mordor-exclusive kit).

    The mordor/ folder hosts shared pools (sk_gn_orc_*, sk_md_orc_*, urukscout_*, ar_ardunian_*)
    worn by goblin/mistymountainorcs/isengard/umbar troops — capping those at Mordor's -1 curve
    would nerf other cultures as collateral, so they are skipped.
    """
    if item_id.startswith('sk_gn_orc_'):
        return False  # GN = design-shared cross-faction pool even when current wearers are all Mordor
    entry = roster_map.get(item_id)
    wearers = entry.get('wearers', []) if entry else []
    if wearers:
        return all(w['troop'] in mordor_troops for w in wearers)
    return item_id.startswith(MORDOR_EXCLUSIVE_PREFIXES)


def effective_mordor_tier(item_id, roster_map):
    """Mordor tier: keyword first (Black Uruk heavy set promotes to elite), else roster map."""
    kw = id_keyword_tier(item_id)
    if item_id.startswith('sk_uruk_mordor') and kw == 'heavy':
        return 'elite'  # roster-derived: the set is worn by L26-36 elites (#342 owner decision)
    if kw:
        return kw
    entry = roster_map.get(item_id)
    if entry and entry.get('tierSource', '').startswith('roster'):
        return entry['tier']
    return None  # unworn + untiered: leave untouched


def process_culture(culture, roster_map, apply=False):
    changes = []
    armory = ra.ARMORY_DIR
    mordor_troops = load_mordor_troop_ids() if culture == 'mordor' else set()
    for slot, fname in SLOT_FILES.items():
        path = os.path.join(armory, culture, fname)
        if not os.path.exists(path):
            continue
        root = ET.parse(path).getroot()
        item_changes = {}
        for item in root.findall('.//Item'):
            item_id = item.get('id', '')
            name = item.get('name', '')
            armor = item.find('.//Armor')
            if armor is None or ab.is_excluded(item_id, name):
                continue
            kw = id_keyword_tier(item_id)
            if kw == 'lord':
                continue

            per_item = {}
            if culture == 'mordor':
                if not mordor_cap_eligible(item_id, roster_map, mordor_troops):
                    continue
                tier = effective_mordor_tier(item_id, roster_map)
                if tier in (None, 'lord'):
                    continue
                targets = curve_targets(tier, slot, 'mordor')
                for stat, target in targets.items():
                    cur = int(armor.get(stat) or 0)
                    if cur > target:  # cap-only
                        per_item[stat] = target
            else:  # gondor
                tier = kw
                if tier is None:
                    continue
                targets = curve_targets(tier, slot, 'gondor')
                base = plain_baseline(tier, slot)
                for stat, target in targets.items():
                    cur = int(armor.get(stat) or 0)
                    if cur == base[stat] and cur < target:  # top-up-only, exact-baseline items
                        per_item[stat] = target

            if per_item:
                item_changes[item_id] = per_item
                for stat, new in per_item.items():
                    changes.append((culture, slot, item_id, tier, stat,
                                    int(armor.get(stat) or 0), new))

        if item_changes and apply:
            backup = path + BACKUP_SUFFIX
            if not os.path.exists(backup):
                shutil.copy2(path, backup)
            ra.apply_changes_via_regex(path, item_changes)
    return changes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', help='write changes (default: dry-run)')
    args = parser.parse_args()

    if not os.path.exists(ROSTER_MAP_JSON):
        sys.exit(f"Missing {ROSTER_MAP_JSON} — run: python tools/derive_armor_tiers.py")
    roster_map = json.load(open(ROSTER_MAP_JSON, encoding='utf-8'))['items']

    all_changes = []
    for culture in ('mordor', 'gondor'):
        all_changes += process_culture(culture, roster_map, apply=args.apply)

    mode = 'APPLIED' if args.apply else 'DRY-RUN'
    by_culture = defaultdict(int)
    print(f"\n=== Gondor/Mordor armor parity fix (#342) — {mode} ===\n")
    for culture, slot, item_id, tier, stat, old, new in all_changes:
        arrow = 'v' if new < old else '^'
        print(f"  {culture:7} {slot:9} {tier:7} {item_id:48} {stat:11} {old:>3} -> {new:<3} {arrow}")
        by_culture[culture] += 1
    print(f"\nStat changes: " + ', '.join(f"{c}: {n}" for c, n in sorted(by_culture.items()))
          + (f"  (total {len(all_changes)})" if all_changes else "  none"))
    if not args.apply and all_changes:
        print("Dry-run only — re-run with --apply to write (backups taken as *" + BACKUP_SUFFIX + ")")


if __name__ == '__main__':
    main()
