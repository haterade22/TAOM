#!/usr/bin/env python3
"""
Roster-derived armor tiering for TAOM (READ-ONLY) — Phase 2 of the armor rebalance.

The authoritative signal for an armor item's intended balance tier is NOT its name keyword
(brittle: a Dale "Archer Helmet A01" carries no tier word) but the LEVEL of the troop that
wears it. This tool joins every troop roster (Main/_Module/ModuleData/troops/troops_*.xml) to
the LIVE LOTRLOME_Armory item stats, keyed by item id, and for each item derives a tier from
its wearers.

Reuse-safe anchoring: armor items are deliberately reused across troops (not enough meshes per
soldier), so an item worn at several levels anchors its tier to its LOWEST wearer — it must never
over-arm the lower troops. Items worn across a wide level span are flagged as shared (the lower
troops get the right armor; the higher ones accept a compromise).

Tier signal precedence per item:
  1. explicit tier keyword in the id (_light_/_med_/_heavy_/_elite_/_lord_) — the author's own label
  2. roster anchor band (lowest wearer level) — for items with no keyword (the Dale case)
  3. unworn — no troop references it; roster cannot tier it (falls back to name/value detection)

This script NEVER writes armor XML. It writes the derived map (tools/data/armor_roster_tiers.json)
and a human report (tools/reports/armor-balance/ROSTER-TIERS.md). It computes the level-band
target as a REFERENCE — it does not decide whether an under-progressed line (e.g. Dale's flat
a-line) should be scaled up or accepted; that is a Phase-3 design call. See
docs/features/armor-balance.md.

Usage:
    python tools/derive_armor_tiers.py                 # write the map + report
    python tools/derive_armor_tiers.py --stdout        # also print the per-culture summary
    python tools/derive_armor_tiers.py --culture dale  # restrict the report to one armory folder
"""

import argparse
import datetime
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_armor as ra  # noqa: E402

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
TROOPS_DIR = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'troops')
DATA_DIR = os.path.join(REPO_ROOT, 'tools', 'data')
REPORT_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'armor-balance')
MAP_JSON = os.path.join(DATA_DIR, 'armor_roster_tiers.json')
REPORT_MD = os.path.join(REPORT_DIR, 'ROSTER-TIERS.md')

# Bannerlord equipment slot -> armor curve slot. Item0-3/Horse/HorseHarness are not armor.
EQUIP_SLOT_MAP = {'Head': 'head', 'Body': 'body', 'Leg': 'leg', 'Gloves': 'arm', 'Cape': 'shoulder'}

SLOT_ORDER = ['head', 'body', 'arm', 'leg', 'shoulder']
TIER_ORDER = ['light', 'medium', 'heavy', 'elite', 'lord']

# Level -> tier band. TAOM troop levels run {1,6,11,16,21,26,31,36,41,46,51}. Calibrated against
# Dale ground truth (L6/L11 wear the light a01 chest; L16 medium; L21/26 heavy; L31 elite). These
# bands are a documented, tunable reference, not a verdict — see the module docstring.
def level_to_tier(level):
    if level <= 13:
        return 'light'
    if level <= 18:
        return 'medium'
    if level <= 28:
        return 'heavy'
    if level <= 38:
        return 'elite'
    return 'lord'


def id_keyword_tier(item_id):
    """Return the tier the item id explicitly encodes, or None."""
    idl = item_id.lower()
    for kw, tier in (('_lord', 'lord'), ('_elite', 'elite'), ('_heavy', 'heavy'),
                     ('_medium', 'medium'), ('_med', 'medium'), ('_light', 'light')):
        if kw in idl:
            return tier
    return None


def line_suffix(item_id):
    """Dale-style light/heavy line suffix: _aNN -> 'a' (light line), _bNN -> 'b' (heavy line)."""
    m = re.search(r'_([ab])\d*$', item_id.lower())
    return m.group(1) if m else None


# =============================================================================
# Parsing
# =============================================================================

def parse_rosters():
    """Return wearers: item_id -> list of {troop, culture, level, slot}."""
    wearers = defaultdict(list)
    if not os.path.isdir(TROOPS_DIR):
        return wearers
    for fn in sorted(os.listdir(TROOPS_DIR)):
        if not (fn.startswith('troops_') and fn.endswith('.xml')):
            continue
        culture = fn[len('troops_'):-len('.xml')]
        try:
            root = ET.parse(os.path.join(TROOPS_DIR, fn)).getroot()
        except ET.ParseError:
            continue
        for npc in root.findall('.//NPCCharacter'):
            tid = npc.get('id', '')
            try:
                level = int(npc.get('level', '0'))
            except ValueError:
                level = 0
            seen = set()  # de-dupe (item, slot) across a troop's multiple rosters
            for eq in npc.findall('.//EquipmentRoster/equipment'):
                slot = EQUIP_SLOT_MAP.get(eq.get('slot', ''))
                if not slot:
                    continue
                raw = eq.get('id', '')
                item_id = raw.split('.', 1)[1] if raw.startswith('Item.') else raw
                if not item_id or (item_id, slot) in seen:
                    continue
                seen.add((item_id, slot))
                wearers[item_id].append({'troop': tid, 'culture': culture, 'level': level, 'slot': slot})
    return wearers


def build_armory_index():
    """Return item_id -> {folder, slot, primary, weight, name}."""
    index = {}
    armory_dir = ra.ARMORY_DIR
    if not os.path.isdir(armory_dir):
        return index, armory_dir
    for folder in sorted(os.listdir(armory_dir)):
        fpath = os.path.join(armory_dir, folder)
        if not os.path.isdir(fpath):
            continue
        for armor_file, slot_type in ra.SLOT_TYPES.items():
            filepath = os.path.join(fpath, armor_file)
            if not os.path.exists(filepath):
                continue
            try:
                root = ET.parse(filepath).getroot()
            except ET.ParseError:
                continue
            for item in root.findall('.//Item'):
                item_id = item.get('id', '')
                armor = item.find('.//Armor')
                if armor is None or not item_id:
                    continue
                values = ra.parse_current_values(armor, slot_type)
                try:
                    weight = float(item.get('weight')) if item.get('weight') is not None else None
                except (TypeError, ValueError):
                    weight = None
                index[item_id] = {
                    'folder': folder,
                    'slot': slot_type,
                    'primary': ra._get_primary_stat(values, slot_type),
                    'weight': weight,
                    'name': ra.get_display_name(item.get('name', '')),
                }
    return index, armory_dir


# =============================================================================
# Derivation
# =============================================================================

def derive():
    wearers = parse_rosters()
    index, armory_dir = build_armory_index()

    records = {}
    for item_id, info in index.items():
        slot = info['slot']
        culture = info['folder']
        ws = wearers.get(item_id, [])
        levels = sorted({w['level'] for w in ws})
        anchor = levels[0] if levels else None
        kw_tier = id_keyword_tier(item_id)

        if kw_tier:
            tier, source = kw_tier, 'id-keyword'
        elif anchor is not None:
            tier, source = level_to_tier(anchor), f'roster(L{anchor})'
        else:
            tier, source = None, 'unworn'

        target = ra._get_primary_stat(ra.calculate_stats(tier, slot, culture), slot) if tier else None
        current = info['primary']
        delta = (current - target) if (current is not None and target is not None) else None
        if delta is None:
            status = 'unworn' if anchor is None else 'no-target'
        elif abs(delta) <= 2:
            status = 'match'
        elif delta < 0:
            status = 'under'
        else:
            status = 'over'

        span = (max(levels) - anchor) if levels else 0
        records[item_id] = {
            'culture': culture,
            'slot': slot,
            'name': info['name'],
            'current': current,
            'weight': info['weight'],
            'anchorLevel': anchor,
            'maxLevel': max(levels) if levels else None,
            'levelSpan': span,
            'wearerCount': len(ws),
            'tier': tier,
            'tierSource': source,
            'target': target,
            'delta': delta,
            'status': status,
            'line': line_suffix(item_id),
            'shared': span >= 15,                # worn across >=3 tier bands -> reuse compromise
            'wearers': [{'troop': w['troop'], 'level': w['level']} for w in sorted(ws, key=lambda x: x['level'])],
        }
    return records, armory_dir


# =============================================================================
# Reporting
# =============================================================================

def render_markdown(records, armory_dir, generated_at):
    by_culture = defaultdict(list)
    for iid, r in records.items():
        by_culture[r['culture']].append((iid, r))

    lines = ["# TAOM Roster-Derived Armor Tiers (read-only, Phase 2)\n"]
    lines.append(f"_Generated {generated_at}. Joins troop rosters to the live armory; an item's tier "
                 "is anchored to its LOWEST wearer (reuse-safe). Target = baseline+cultural-mod for that "
                 "tier (REFERENCE — scale-vs-accept is a Phase-3 call)._\n")

    # Summary
    lines.append("## Per-culture summary\n")
    lines.append("| Culture | Items | Worn | Unworn | Match | Under | Over | Shared (reuse) |")
    lines.append("|---------|------:|-----:|-------:|------:|------:|-----:|---------------:|")
    for culture in sorted(by_culture):
        rs = [r for _, r in by_culture[culture]]
        worn = sum(1 for r in rs if r['anchorLevel'] is not None)
        lines.append(f"| {culture} | {len(rs)} | {worn} | {len(rs) - worn} "
                     f"| {sum(1 for r in rs if r['status'] == 'match')} "
                     f"| {sum(1 for r in rs if r['status'] == 'under')} "
                     f"| {sum(1 for r in rs if r['status'] == 'over')} "
                     f"| {sum(1 for r in rs if r['shared'])} |")
    lines.append("")

    # Per-culture detail: the actionable under/over re-stat list
    for culture in sorted(by_culture):
        rs = sorted(by_culture[culture], key=lambda x: (x[1]['slot'], x[1]['anchorLevel'] or 999, x[0]))
        flagged = [(i, r) for i, r in rs if r['status'] in ('under', 'over')]
        unworn = [(i, r) for i, r in rs if r['status'] == 'unworn']
        lines.append(f"## {culture}\n")
        if not flagged and not unworn:
            lines.append("_All roster-tiered items on target._\n")
            continue
        if flagged:
            lines.append("**Re-stat candidates (current vs roster-tier target):**\n")
            lines.append("| Item | Slot | Anchor L | Tier (src) | Cur→Tgt | Δ | Line | Shared | Wearers |")
            lines.append("|------|------|---------:|------------|:-------:|--:|:----:|:------:|---------|")
            for iid, r in flagged:
                wstr = ', '.join(f"{w['troop']}(L{w['level']})" for w in r['wearers'][:4])
                if len(r['wearers']) > 4:
                    wstr += '…'
                lines.append(f"| `{iid}` | {r['slot']} | {r['anchorLevel']} | {r['tier']} ({r['tierSource']}) "
                             f"| {r['current']}→{r['target']} | {r['delta']:+d} | {r['line'] or ''} "
                             f"| {'yes' if r['shared'] else ''} | {wstr} |")
            lines.append("")
        if unworn:
            lines.append(f"**Unworn by any roster ({len(unworn)}) — roster cannot tier; name/value fallback or hero/display:**\n")
            lines.append("`" + "`, `".join(i for i, _ in unworn[:30]) + ("` …" if len(unworn) > 30 else "`"))
            lines.append("")
    return "\n".join(lines)


# =============================================================================
# Main
# =============================================================================

def main():
    parser = argparse.ArgumentParser(description="Roster-derived armor tiering for TAOM (read-only).")
    parser.add_argument('--stdout', action='store_true', help='Print the per-culture summary')
    parser.add_argument('--culture', default='', help='Restrict the printed summary to one armory folder')
    args = parser.parse_args()

    records, armory_dir = derive()
    if not records:
        print(f"ERROR: no armory items found under {armory_dir} (set $BANNERLORD_GAME_DIR / --armory-path?)")
        sys.exit(1)

    generated_at = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    os.makedirs(DATA_DIR, exist_ok=True)
    os.makedirs(REPORT_DIR, exist_ok=True)
    with open(MAP_JSON, 'w', encoding='utf-8') as f:
        json.dump({'generatedAt': generated_at, 'armoryDir': armory_dir, 'items': records}, f, indent=2)
    with open(REPORT_MD, 'w', encoding='utf-8') as f:
        f.write(render_markdown(records, armory_dir, generated_at))

    worn = sum(1 for r in records.values() if r['anchorLevel'] is not None)
    under = sum(1 for r in records.values() if r['status'] == 'under')
    over = sum(1 for r in records.values() if r['status'] == 'over')
    print(f"Derived tiers for {len(records)} items: {worn} roster-worn, {len(records) - worn} unworn. "
          f"{under} under-target, {over} over-target.")
    print(f"Map:    {MAP_JSON}")
    print(f"Report: {REPORT_MD}")

    if args.stdout:
        by_culture = defaultdict(list)
        for r in records.values():
            if not args.culture or r['culture'] == args.culture:
                by_culture[r['culture']].append(r)
        print(f"\n{'Culture':<14}{'Items':>6}{'Worn':>6}{'Unworn':>7}{'Under':>6}{'Over':>5}{'Shared':>7}")
        print('-' * 51)
        for culture in sorted(by_culture):
            rs = by_culture[culture]
            worn_c = sum(1 for r in rs if r['anchorLevel'] is not None)
            print(f"{culture:<14}{len(rs):>6}{worn_c:>6}{len(rs) - worn_c:>7}"
                  f"{sum(1 for r in rs if r['status'] == 'under'):>6}"
                  f"{sum(1 for r in rs if r['status'] == 'over'):>5}"
                  f"{sum(1 for r in rs if r['shared']):>7}")


if __name__ == '__main__':
    main()
