#!/usr/bin/env python3
"""
Armor Balance Overview Analyzer for TAOM (READ-ONLY).

Reads every culture folder of the LIVE LOTRLOME_Armory item tree (the one the game loads)
and emits a per-culture armor-balance overview (color-coded HTML + markdown + JSON sidecar).
It is the read-only companion to the (writing) rebalance_armor.py and reuses that tool's
SLOT_BASELINES + CULTURAL_MODS + tier/stat helpers verbatim, so the "ideal" curve has a single
source of truth and the two tools can never drift.

This script NEVER writes to any armor XML. The only files it writes are the three report
artifacts under tools/reports/armor-balance/ (REPORT.html is the viewable one).

What it flags (the defect classes the 2026-06-30 18-culture audit found by hand):
  * MONOLITHIC slot weight/armor — a slot frozen at one value across all tiers (e.g. harad body
    all 9.5kg, iron_hills arm all 25/3.5). The #1 recurring defect. Reported as ERROR.
  * Data-integrity — items missing material_type / modifier_group, 0-armor combat items,
    helmets missing hair/beard cover, legs missing covers_legs, arms missing covers_hands.
  * Ceiling shortfall — a culture whose top combat tier sits well under its baseline+mod target.
  * Approx over/under-curve — each combat item vs its detect_tier baseline (APPROX; the keyword
    tier-detector is brittle — Phase 2 replaces it with roster-derived tiering, see the doc).

Hero / boss / display items are EXCLUDED from every curve judgment (they are deliberately extreme).

Usage:
    python tools/analyze_armor_balance.py                 # write the report
    python tools/analyze_armor_balance.py --stdout        # also print the executive summary
    python tools/analyze_armor_balance.py --culture dale  # restrict to one culture
    python tools/analyze_armor_balance.py --armory-path "E:\\custom\\path"
"""

import argparse
import datetime
import json
import os
import statistics
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

# --- Reuse the reference formula from the writing tool (do NOT re-derive) -------------------
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_armor as ra  # noqa: E402  ARMORY_DIR / SLOT_BASELINES / CULTURAL_MODS / detect_tier / ...

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
REPORT_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'armor-balance')
REPORT_MD = os.path.join(REPORT_DIR, 'REPORT.md')
REPORT_HTML = os.path.join(REPORT_DIR, 'REPORT.html')
REPORT_JSON = os.path.join(REPORT_DIR, 'armor-balance.json')

SLOT_ORDER = ['head', 'body', 'arm', 'leg', 'shoulder']
# A slot needs at least this many combat items before "all one value" counts as a monolithic defect
# (a 2-item slot sharing a value is just thin, not a frozen ladder).
MONOLITHIC_MIN_ITEMS = 4

# Hero / boss / fixed-display item id substrings excluded from the curve (in addition to the
# HERO_NAMES matched against display names). Derived from the 18-culture audit's exclusion list.
EXCLUDE_ID_SUBSTRINGS = (
    'lotr_troll', 'cave_troll',          # boss creature kit (95 by design)
    'glorfindel', 'gf_',                 # rivendell hero
    'dain_crown',                        # legitimate hero outlier (rest of dain_ is judged for the inversion bug)
)


def is_excluded(item_id, display_name):
    """True for hero / boss / fixed-display items that must NOT be judged against the troop curve."""
    idl = (item_id or '').lower()
    nl = (display_name or '').lower()
    if any(s in idl for s in EXCLUDE_ID_SUBSTRINGS):
        return True
    for hero in ra.HERO_NAMES:
        if hero and hero in nl:
            return True
    return False


# =============================================================================
# Parsing
# =============================================================================

def parse_culture_slot(filepath, slot_type):
    """Parse one armor XML file. Returns (items, parse_error_or_None).

    Each item record: id, name, tier(approx), excluded(bool), primary(int|None), weight(float|None),
    material_type, modifier_group, and the cover attrs relevant to the slot.
    """
    try:
        root = ET.parse(filepath).getroot()
    except ET.ParseError as e:
        return [], f"XML parse error: {e}"

    items = []
    for item in root.findall('.//Item'):
        item_id = item.get('id', '')
        name = ra.get_display_name(item.get('name', ''))
        armor = item.find('.//Armor')
        if armor is None:
            continue
        values = ra.parse_current_values(armor, slot_type)
        primary = ra._get_primary_stat(values, slot_type)
        try:
            weight = float(item.get('weight')) if item.get('weight') is not None else None
        except (TypeError, ValueError):
            weight = None
        tier = ra.detect_tier(item_id, name, values, slot_type)
        items.append({
            'id': item_id,
            'name': name,
            'tier': tier,
            'excluded': is_excluded(item_id, name),
            'primary': primary,
            'weight': weight,
            'material_type': armor.get('material_type'),
            'modifier_group': armor.get('modifier_group'),
            'hair_cover': armor.get('hair_cover_type'),
            'beard_cover': armor.get('beard_cover_type'),
            'covers_legs': armor.get('covers_legs'),
            'covers_hands': armor.get('covers_hands'),
        })
    return items, None


# =============================================================================
# Analysis
# =============================================================================

def _baseline_target(slot_type, tier, culture):
    """Primary-stat target = baseline + cultural protection mod (mirrors rebalance_armor)."""
    stats = ra.calculate_stats(tier, slot_type, culture, variant_num=0)
    return ra._get_primary_stat(stats, slot_type)


def analyze_slot(culture, slot_type, items, parse_error):
    """Return a slot analysis dict + a list of finding dicts {severity, slot, message}."""
    findings = []
    if parse_error:
        findings.append({'severity': 'ERROR', 'slot': slot_type, 'message': parse_error})
        return {'slot': slot_type, 'count': 0, 'error': parse_error}, findings

    combat = [i for i in items if not i['excluded'] and i['tier'] != 'civilian']
    civilian = [i for i in items if not i['excluded'] and i['tier'] == 'civilian']
    excluded = [i for i in items if i['excluded']]

    primaries = [i['primary'] for i in combat if i['primary'] is not None]
    weights = [i['weight'] for i in combat if i['weight'] is not None]

    analysis = {
        'slot': slot_type,
        'count': len(items),
        'combat': len(combat),
        'civilian': len(civilian),
        'excluded': len(excluded),
        'armorMin': min(primaries) if primaries else None,
        'armorMedian': round(statistics.median(primaries), 1) if primaries else None,
        'armorMax': max(primaries) if primaries else None,
        'weightMin': min(weights) if weights else None,
        'weightMax': max(weights) if weights else None,
        'distinctWeights': len(set(weights)),
        'distinctArmor': len(set(primaries)),
    }

    # --- Monolithic detection (the #1 recurring defect) ---
    if len(weights) >= MONOLITHIC_MIN_ITEMS and len(set(weights)) == 1:
        findings.append({'severity': 'ERROR', 'slot': slot_type,
                         'message': f"MONOLITHIC WEIGHT: all {len(weights)} combat items weigh {weights[0]} "
                                    f"- no weight tradeoff between tiers."})
    if len(primaries) >= MONOLITHIC_MIN_ITEMS and len(set(primaries)) == 1:
        findings.append({'severity': 'ERROR', 'slot': slot_type,
                         'message': f"MONOLITHIC ARMOR: all {len(primaries)} combat items share primary "
                                    f"armor {primaries[0]} - fake tiers, no progression."})

    # --- Data integrity ---
    no_material = [i['id'] for i in combat if not i['material_type']]
    if no_material:
        findings.append({'severity': 'ERROR', 'slot': slot_type,
                         'message': f"{len(no_material)} item(s) missing material_type: "
                                    f"{', '.join(no_material[:6])}{'…' if len(no_material) > 6 else ''}"})
    no_modgroup = [i['id'] for i in combat if not i['modifier_group']]
    if no_modgroup:
        findings.append({'severity': 'WARN', 'slot': slot_type,
                         'message': f"{len(no_modgroup)} item(s) missing modifier_group: "
                                    f"{', '.join(no_modgroup[:6])}{'…' if len(no_modgroup) > 6 else ''}"})
    zero_armor = [i['id'] for i in combat if i['primary'] in (0, None)]
    if zero_armor:
        findings.append({'severity': 'WARN', 'slot': slot_type,
                         'message': f"{len(zero_armor)} combat item(s) with 0/None primary armor: "
                                    f"{', '.join(zero_armor[:6])}{'…' if len(zero_armor) > 6 else ''}"})

    # Slot-specific cover attributes
    if slot_type == 'head':
        miss = [i['id'] for i in combat if not i['hair_cover'] or not i['beard_cover']]
        if miss:
            findings.append({'severity': 'WARN', 'slot': slot_type,
                             'message': f"{len(miss)}/{len(combat)} helmet(s) missing hair_cover_type or "
                                        f"beard_cover_type (clipping/identity gap): "
                                        f"{', '.join(miss[:6])}{'…' if len(miss) > 6 else ''}"})
    if slot_type == 'leg':
        miss = [i['id'] for i in combat if i['covers_legs'] is None]
        if miss:
            findings.append({'severity': 'INFO', 'slot': slot_type,
                             'message': f"{len(miss)} leg item(s) without an explicit covers_legs attribute."})
    if slot_type == 'arm':
        miss = [i['id'] for i in combat if i['covers_hands'] is None]
        if miss:
            findings.append({'severity': 'INFO', 'slot': slot_type,
                             'message': f"{len(miss)} arm item(s) without an explicit covers_hands attribute."})

    # --- Ceiling shortfall (top combat item vs lord-tier baseline+mod) ---
    if primaries:
        lord_target = _baseline_target(slot_type, 'lord', culture)
        elite_target = _baseline_target(slot_type, 'elite', culture)
        # Only warn on a meaningful (near-full-tier) gap, not a 1-2 point rounding miss.
        if (elite_target and analysis['armorMax'] is not None
                and analysis['armorMax'] < elite_target - 3):
            findings.append({'severity': 'WARN', 'slot': slot_type,
                             'message': f"CEILING SHORTFALL: top combat {slot_type} armor {analysis['armorMax']} "
                                        f"is well under the elite target {elite_target} (lord {lord_target}) for this culture."})

    return analysis, findings


def analyze_culture(culture, armory_dir):
    """Analyze one culture folder across all slots. Returns a culture record."""
    culture_path = os.path.join(armory_dir, culture)
    slots = {}
    all_findings = []
    total_items = 0
    for armor_file, slot_type in ra.SLOT_TYPES.items():
        filepath = os.path.join(culture_path, armor_file)
        if not os.path.exists(filepath):
            continue
        items, parse_error = parse_culture_slot(filepath, slot_type)
        total_items += len(items)
        analysis, findings = analyze_slot(culture, slot_type, items, parse_error)
        slots[slot_type] = analysis
        all_findings.extend(findings)

    errors = sum(1 for f in all_findings if f['severity'] == 'ERROR')
    warns = sum(1 for f in all_findings if f['severity'] == 'WARN')
    if errors:
        rating = 'Internally-inconsistent'
    elif warns:
        rating = 'Minor issues'
    else:
        rating = 'Clean'

    mods = ra.CULTURAL_MODS.get(culture)
    return {
        'culture': culture,
        'inCulturalMods': mods is not None,
        'culturalMod': mods,
        'totalItems': total_items,
        'rating': rating,
        'errorCount': errors,
        'warnCount': warns,
        'slots': slots,
        'findings': all_findings,
    }


# =============================================================================
# Reporting
# =============================================================================

def _slot_cell(a):
    if not a or a.get('count', 0) == 0:
        return '—'
    mono = ' ⚠' if a.get('distinctWeights') == 1 and (a.get('combat') or 0) >= MONOLITHIC_MIN_ITEMS else ''
    return (f"{a.get('combat', 0)} it · {a.get('armorMin')}-{a.get('armorMax')} "
            f"· w {a.get('weightMin')}-{a.get('weightMax')}{mono}")


def render_markdown(cultures, generated_at):
    lines = []
    lines.append("# TAOM Armor Balance Overview (read-only)\n")
    lines.append(f"_Generated {generated_at} from the LIVE LOTRLOME_Armory tree by "
                 "`tools/analyze_armor_balance.py`. Curve owned by `tools/rebalance_armor.py`._\n")
    lines.append("Hero / boss / fixed-display items are excluded from all curve judgments. "
                 "Tiers are APPROX (keyword-based; Phase 2 replaces with roster-derived tiering).\n")

    # Summary table
    lines.append("## Cross-culture summary\n")
    lines.append("| Culture | Rating | Items | Errors | Warns | In CULTURAL_MODS |")
    lines.append("|---------|--------|------:|-------:|------:|:----------------:|")
    for c in cultures:
        lines.append(f"| {c['culture']} | {c['rating']} | {c['totalItems']} | {c['errorCount']} "
                     f"| {c['warnCount']} | {'yes' if c['inCulturalMods'] else '**NO**'} |")
    lines.append("")

    # Per-culture detail
    for c in cultures:
        lines.append(f"## {c['culture']} — {c['rating']}\n")
        mod = c['culturalMod']
        modstr = (f"protection {mod['protection']:+d}, weight ×{mod['weight_mult']}"
                  if mod else "**missing from CULTURAL_MODS — runs on neutral default (0/×1.0)**")
        lines.append(f"Cultural mod: {modstr}\n")
        lines.append("| Slot | Combat items | Armor min-max | Weight min-max | Distinct weights |")
        lines.append("|------|-------------:|:-------------:|:--------------:|:----------------:|")
        for slot in SLOT_ORDER:
            a = c['slots'].get(slot)
            if not a or a.get('count', 0) == 0:
                continue
            dw = a.get('distinctWeights')
            mono = ' ⚠ MONOLITHIC' if dw == 1 and (a.get('combat') or 0) >= MONOLITHIC_MIN_ITEMS else ''
            lines.append(f"| {slot} | {a.get('combat', 0)} | {a.get('armorMin')}-{a.get('armorMax')} "
                         f"| {a.get('weightMin')}-{a.get('weightMax')} | {dw}{mono} |")
        lines.append("")
        if c['findings']:
            lines.append("**Findings:**\n")
            for f in c['findings']:
                lines.append(f"- `{f['severity']}` ({f['slot']}) {f['message']}")
        else:
            lines.append("_No findings — clean._")
        lines.append("")
    return "\n".join(lines)


def render_html(cultures, generated_at):
    sev_color = {'ERROR': '#c0392b', 'WARN': '#d68910', 'INFO': '#5d6d7e'}
    rate_color = {'Internally-inconsistent': '#f9d6d5', 'Minor issues': '#fcf3cf', 'Clean': '#d5f5e3'}
    h = ['<!DOCTYPE html><html><head><meta charset="utf-8"><title>TAOM Armor Balance</title>',
         '<style>',
         'body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222;}',
         'table{border-collapse:collapse;margin:8px 0 20px;}',
         'th,td{border:1px solid #ccc;padding:4px 8px;font-size:13px;}',
         'th{background:#34495e;color:#fff;}',
         'h2{margin-top:28px;border-bottom:2px solid #34495e;padding-bottom:2px;}',
         '.mono{color:#c0392b;font-weight:bold;}',
         'code{background:#f4f4f4;padding:1px 4px;border-radius:3px;}',
         '</style></head><body>']
    h.append(f"<h1>TAOM Armor Balance Overview</h1>")
    h.append(f"<p><em>Generated {generated_at} from the LIVE LOTRLOME_Armory tree. Read-only. "
             "Curve owned by <code>tools/rebalance_armor.py</code>. Hero/boss items excluded; "
             "tiers approximate (Phase 2 = roster-derived).</em></p>")
    # Summary
    h.append("<h2>Cross-culture summary</h2><table>")
    h.append("<tr><th>Culture</th><th>Rating</th><th>Items</th><th>Errors</th><th>Warns</th><th>In CULTURAL_MODS</th></tr>")
    for c in cultures:
        bg = rate_color.get(c['rating'], '#fff')
        modcell = 'yes' if c['inCulturalMods'] else '<span class="mono">NO</span>'
        h.append(f"<tr style='background:{bg}'><td>{c['culture']}</td><td>{c['rating']}</td>"
                 f"<td align='right'>{c['totalItems']}</td><td align='right'>{c['errorCount']}</td>"
                 f"<td align='right'>{c['warnCount']}</td><td align='center'>{modcell}</td></tr>")
    h.append("</table>")
    # Detail
    for c in cultures:
        bg = rate_color.get(c['rating'], '#fff')
        h.append(f"<h2 style='background:{bg};padding:4px 8px'>{c['culture']} — {c['rating']}</h2>")
        mod = c['culturalMod']
        modstr = (f"protection {mod['protection']:+d}, weight ×{mod['weight_mult']}" if mod
                  else "<span class='mono'>missing from CULTURAL_MODS — neutral default</span>")
        h.append(f"<p>Cultural mod: {modstr} &nbsp;|&nbsp; {c['totalItems']} items</p>")
        h.append("<table><tr><th>Slot</th><th>Combat</th><th>Armor min-max</th>"
                 "<th>Weight min-max</th><th>Distinct weights</th></tr>")
        for slot in SLOT_ORDER:
            a = c['slots'].get(slot)
            if not a or a.get('count', 0) == 0:
                continue
            dw = a.get('distinctWeights')
            mono = " <span class='mono'>MONOLITHIC</span>" if dw == 1 and (a.get('combat') or 0) >= MONOLITHIC_MIN_ITEMS else ''
            h.append(f"<tr><td>{slot}</td><td align='right'>{a.get('combat', 0)}</td>"
                     f"<td align='center'>{a.get('armorMin')}-{a.get('armorMax')}</td>"
                     f"<td align='center'>{a.get('weightMin')}-{a.get('weightMax')}</td>"
                     f"<td align='center'>{dw}{mono}</td></tr>")
        h.append("</table>")
        if c['findings']:
            h.append("<ul>")
            for f in c['findings']:
                col = sev_color.get(f['severity'], '#000')
                h.append(f"<li><strong style='color:{col}'>{f['severity']}</strong> "
                         f"<em>({f['slot']})</em> {f['message']}</li>")
            h.append("</ul>")
        else:
            h.append("<p><em>No findings — clean.</em></p>")
    h.append("</body></html>")
    return "\n".join(h)


# =============================================================================
# Main
# =============================================================================

def main():
    parser = argparse.ArgumentParser(description="Read-only armor balance analyzer for TAOM.")
    parser.add_argument('--stdout', action='store_true', help='Also print the executive summary')
    parser.add_argument('--culture', default='', help='Restrict to one culture folder')
    parser.add_argument('--armory-path', default=ra.ARMORY_DIR,
                        help='Override the armory base dir (default: live LOTRLOME_Armory)')
    args = parser.parse_args()

    armory_dir = os.path.normpath(args.armory_path)
    if not os.path.isdir(armory_dir):
        print(f"ERROR: Armory directory not found: {armory_dir}")
        print("(Set $BANNERLORD_GAME_DIR or pass --armory-path if the game install moved.)")
        sys.exit(1)

    culture_dirs = sorted([
        d for d in os.listdir(armory_dir)
        if os.path.isdir(os.path.join(armory_dir, d)) and (not args.culture or d == args.culture)
    ])
    if not culture_dirs:
        print(f"ERROR: no culture folders found under {armory_dir}"
              + (f" matching --culture {args.culture}" if args.culture else ""))
        sys.exit(1)

    cultures = [analyze_culture(c, armory_dir) for c in culture_dirs]

    generated_at = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    os.makedirs(REPORT_DIR, exist_ok=True)
    with open(REPORT_JSON, 'w', encoding='utf-8') as f:
        json.dump({'generatedAt': generated_at, 'armoryDir': armory_dir, 'cultures': cultures},
                  f, indent=2)
    with open(REPORT_MD, 'w', encoding='utf-8') as f:
        f.write(render_markdown(cultures, generated_at))
    with open(REPORT_HTML, 'w', encoding='utf-8') as f:
        f.write(render_html(cultures, generated_at))

    total_err = sum(c['errorCount'] for c in cultures)
    total_warn = sum(c['warnCount'] for c in cultures)
    print(f"Analyzed {len(cultures)} culture(s), "
          f"{sum(c['totalItems'] for c in cultures)} items. "
          f"{total_err} errors, {total_warn} warnings.")
    print(f"Reports: {REPORT_HTML}")

    if args.stdout:
        print("\n=== EXECUTIVE SUMMARY ===")
        print(f"{'Culture':<14}{'Rating':<24}{'Items':>6}{'Err':>5}{'Warn':>6}{'Mods':>6}")
        print('-' * 61)
        for c in cultures:
            print(f"{c['culture']:<14}{c['rating']:<24}{c['totalItems']:>6}"
                  f"{c['errorCount']:>5}{c['warnCount']:>6}{'y' if c['inCulturalMods'] else 'NO':>6}")
        print("\nMonolithic slots (the #1 recurring defect):")
        for c in cultures:
            for f in c['findings']:
                if 'MONOLITHIC' in f['message']:
                    print(f"  {c['culture']:<12} {f['slot']:<9} {f['message']}")


if __name__ == '__main__':
    main()
