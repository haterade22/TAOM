#!/usr/bin/env python3
"""
Troop Balance Overview Analyzer for TAOM (READ-ONLY).

Reads every Main/_Module/ModuleData/troops/troops_*.xml plus TroopWeights/troop_weights.xml
and emits a per-culture balance overview (color-coded HTML + markdown + JSON sidecar) comparing
each troop's actual combat skills against the reference baseline + cultural-modifier formula owned
by tools/rebalance_troops.py.

This script NEVER writes to any troop XML. The only files it writes are the three report
artifacts under tools/reports/troop-balance/ (REPORT.html is the viewable one). It is the read-only companion to the
(writing) rebalance_troops.py — it reuses that tool's formula tables verbatim so the
"ideal" curve has a single source of truth.

Usage:
    python analyze_troop_balance.py                     # write the report
    python analyze_troop_balance.py --stdout            # also print the executive summary
    python analyze_troop_balance.py --outlier-threshold 60   # default 50
"""

import argparse
import datetime
import json
import os
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

# --- Reuse the reference formula from the existing rebalance tool (do NOT re-derive) ----
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_troops as rb  # noqa: E402  GROUP_BASELINES / CULTURAL_MODS / calculate_skills / ...

SKILL_NAMES = rb.SKILL_NAMES
GROUPS = ['Infantry', 'Ranged', 'Cavalry', 'HorseArcher']
BASELINE_LEVELS = [1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51]

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
TROOPS_DIR = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'troops')
WEIGHTS_FILE = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'TroopWeights', 'troop_weights.xml')
RESOURCE_COST_FILE = os.path.join(
    REPO_ROOT, 'Main', '_Module', 'ModuleData', 'special_resources', 'troop_resource_costs.xml')
REPORT_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'troop-balance')
REPORT_MD = os.path.join(REPORT_DIR, 'REPORT.md')
REPORT_HTML = os.path.join(REPORT_DIR, 'REPORT.html')
REPORT_JSON = os.path.join(REPORT_DIR, 'troop-balance.json')

# Filename-culture -> formula-culture remap. Now EMPTY: rebalance_troops.detect_culture maps
# rhun_new -> rhun directly (fixed during the 2026-06-24 rebaseline), so t['culture'] already
# carries the resolved key and no analyzer-side remap is needed. Kept as the seam if a future
# filename/key mismatch appears.
CULTURE_FORMULA_REMAP = {}

# Genuine NON-HUMANOID creature/monster troops whose skills are bespoke and must NOT be judged
# against the humanoid baseline formula. Kept deliberately narrow: 'mount' over-matches
# "mistyMOUNTainorcs" / "Mountain Guard"; warg/elephant/spider all denote humanoid *riders*
# (they DO follow the formula) so they live in MOUNT_RIDER_MARKERS, not here.
CREATURE_MARKERS = ('troll', 'creature')
CREATURE_IDS = {'cave_troll'}

# Humanoid troops that ride a non-humanoid mount. Judged on the formula like any other troop;
# tagged purely so the reader has context if their Riding/combat skills look hand-tuned.
MOUNT_RIDER_MARKERS = ('warg', 'elephant', 'spider', 'wolf rider', 'wolf-rider', 'beast master', 'beastmaster')


def get_display_name(name_attr):
    return rb.get_display_name(name_attr or '')


def formula_culture(filename_culture):
    return CULTURE_FORMULA_REMAP.get(filename_culture, filename_culture)


def is_creature_troop(troop_id, troop_name):
    """True for genuine non-humanoid monster troops (formula-exempt)."""
    if troop_id in CREATURE_IDS:
        return True
    blob = (troop_id + ' ' + troop_name).lower()
    return any(m in blob for m in CREATURE_MARKERS)


def is_mount_rider(troop_id, troop_name):
    """True for humanoid riders of a non-humanoid mount (judged on formula; informational tag)."""
    blob = (troop_id + ' ' + troop_name).lower()
    return any(m in blob for m in MOUNT_RIDER_MARKERS)


# =============================================================================
# Parsing
# =============================================================================

def parse_weights():
    """Return {troop_id: weight} from troop_weights.xml (commented-out entries excluded by ET)."""
    weights = {}
    if not os.path.exists(WEIGHTS_FILE):
        return weights
    root = ET.parse(WEIGHTS_FILE).getroot()
    for tw in root.findall('.//TroopWeight'):
        tid = tw.get('id')
        if tid:
            try:
                weights[tid] = float(tw.get('weight', '1.0'))
            except ValueError:
                weights[tid] = 1.0
    return weights


def parse_resource_costs():
    """Return set of troop ids that are special-resource gated (informational only)."""
    gated = set()
    if not os.path.exists(RESOURCE_COST_FILE):
        return gated
    try:
        root = ET.parse(RESOURCE_COST_FILE).getroot()
    except ET.ParseError:
        return gated
    for el in root.iter():
        tid = el.get('troop') or el.get('troopId') or el.get('id')
        if tid:
            gated.add(tid.replace('NPCCharacter.', ''))
    return gated


def parse_troop_file(filepath, item_classes=None):
    """Parse one troops_*.xml. Returns (filename_culture, [troop_record, ...]).

    item_classes: item id -> skill class map (taom_schema.build_item_class_registry)
    for equipment-driven specialization; None degrades to name-only detection."""
    filename = os.path.basename(filepath)
    filename_culture = filename.replace('troops_', '').replace('.xml', '')
    root = ET.parse(filepath).getroot()

    troops = []
    for npc in root.findall('.//NPCCharacter'):
        troop_id = npc.get('id', '')
        if not troop_id:
            continue
        name = get_display_name(npc.get('name', ''))
        try:
            level = int(npc.get('level', '0'))
        except ValueError:
            level = 0
        group = npc.get('default_group', 'Infantry')
        # detect_culture routes iron_hills_* (which live in the erebor file) to 'iron_hills'.
        culture = rb.detect_culture(troop_id, filename_culture)

        actual = {}
        skills_elem = npc.find('skills')
        if skills_elem is not None:
            for s in skills_elem.findall('skill'):
                sid = s.get('id')
                if sid in SKILL_NAMES:
                    try:
                        actual[sid] = int(s.get('value', '0'))
                    except ValueError:
                        actual[sid] = 0

        upgrades = []
        ut = npc.find('upgrade_targets')
        if ut is not None:
            for u in ut.findall('upgrade_target'):
                uid = u.get('id', '')
                if uid:
                    upgrades.append(uid.replace('NPCCharacter.', ''))

        equip = npc.find('Equipments')
        has_equipment = equip is not None and len(list(equip)) > 0
        weapon_classes = rb.troop_weapon_classes(npc, item_classes) if item_classes else None

        troops.append({
            'weapon_classes': weapon_classes,
            'file': filename,
            'file_culture': filename_culture,
            'id': troop_id,
            'name': name,
            'level': level,
            'group': group,
            'culture': culture,
            'is_basic': npc.get('is_basic_troop', 'false') == 'true',
            'militia': rb.is_militia(troop_id, name),
            'actual': actual,
            'upgrades': upgrades,
            'has_equipment': has_equipment,
        })
    return filename_culture, troops


# =============================================================================
# Analysis
# =============================================================================

def analyze(troops, weights, threshold):
    """Decorate each troop record with reference skills + deltas. Returns the same list."""
    for t in troops:
        special = is_creature_troop(t['id'], t['name'])
        t['special'] = special
        t['mount_rider'] = (not special) and is_mount_rider(t['id'], t['name'])

        fc = formula_culture(t['culture'])
        ref = rb.calculate_skills(fc, t['level'], t['group'], t['id'], t['name'],
                                  t.get('weapon_classes'))
        t['ref'] = ref
        t['off_grid'] = ref is None

        total_actual = sum(t['actual'].get(s, 0) for s in SKILL_NAMES)
        t['total_actual'] = total_actual

        if ref is None:
            t['total_ref'] = None
            t['delta'] = None
            t['skill_deltas'] = {}
            t['outlier'] = False
        else:
            total_ref = sum(ref[s] for s in SKILL_NAMES)
            t['total_ref'] = total_ref
            t['delta'] = total_actual - total_ref
            t['skill_deltas'] = {s: t['actual'].get(s, 0) - ref[s] for s in SKILL_NAMES}
            # Special/creature + off-grid troops are not judged against the humanoid formula.
            t['outlier'] = (not special) and abs(t['delta']) > threshold

        t['weight'] = weights.get(t['id'])
    return troops


def used_cultures(all_troops):
    return {t['culture'] for t in all_troops}


def data_quality(all_troops, file_cultures):
    """Return the structured stale-tooling / data-quality findings."""
    used = used_cultures(all_troops)

    # Cultures with troops on disk but no CULTURAL_MODS identity. Keyed on the RESOLVED culture
    # (what detect_culture actually returns), so rhun_new->rhun is correctly counted as 'rhun'
    # and not falsely flagged once the modifier exists.
    no_identity = sorted(c for c in used if c not in rb.CULTURAL_MODS)

    # Key-mismatch cases: a file culture that only balances correctly via the remap.
    remap_findings = []
    for fc_key, target in CULTURE_FORMULA_REMAP.items():
        if fc_key in file_cultures:
            remap_findings.append({
                'file_culture': fc_key,
                'intended_modifier': target,
                'in_mods': target in rb.CULTURAL_MODS,
            })

    # Dead CULTURAL_MODS entries: defined but never produced by detect_culture across all troops.
    dead_mods = sorted(k for k in rb.CULTURAL_MODS if k not in used)

    specials = sorted(
        [{'id': t['id'], 'name': t['name'], 'file': t['file'], 'level': t['level']}
         for t in all_troops if t['special']],
        key=lambda x: (x['file'], x['id']))

    riders = sorted(
        [{'id': t['id'], 'name': t['name'], 'file': t['file'], 'level': t['level'],
          'outlier': t['outlier'], 'delta': t['delta']}
         for t in all_troops if t['mount_rider']],
        key=lambda x: (x['file'], x['level']))

    return {
        'cultures_without_identity': no_identity,
        'remap_findings': remap_findings,
        'dead_modifiers': dead_mods,
        'special_troops': specials,
        'mount_riders': riders,
    }


# =============================================================================
# Monotonicity (no lower level stronger than a higher level)
# =============================================================================

def check_monotonicity(all_troops, tolerance=25):
    """Find genuine level-inversions among PROFESSIONAL troops.

    MILITIA ARE EXCLUDED by design: TAOM militia are intentionally over-statted for siege /
    village defense (they take the L21 baseline regardless of level), so a low-level militia
    out-statting a mid-level regular is deliberate, not a bug (user direction 2026-06-24).

    Comparison is total-skill based: weapon-spec swaps preserve a troop's total, so a melee
    troop upgrading into an archer (lower melee-primary, same/higher total) is NOT flagged.
    """
    by_id = {t['id']: t for t in all_troops}

    def total(t):
        return sum(t['actual'].get(s, 0) for s in SKILL_NAMES)

    def comparable(t):
        return (not t['militia'] and not t.get('special')
                and len(t['actual']) >= 6 and t['level'] in BASELINE_LEVELS)

    upgrade_viol = []
    for t in all_troops:
        if t['militia'] or t.get('special'):
            continue
        for uid in t['upgrades']:
            u = by_id.get(uid)
            if not u or u['militia']:
                continue
            if u['level'] < t['level']:
                upgrade_viol.append((t, u, 'target is lower level', t['level'] - u['level']))
            elif comparable(t) and comparable(u) and total(u) < total(t) - tolerance:
                upgrade_viol.append((t, u, 'target weaker (total)', total(t) - total(u)))

    by_cg = defaultdict(list)
    for t in all_troops:
        if comparable(t):
            by_cg[(t['culture'], t['group'])].append(t)
    level_viol, seen = [], set()
    for ts in by_cg.values():
        for a in ts:
            for b in ts:
                if a['level'] < b['level'] and total(a) > total(b) + tolerance:
                    if (a['id'], b['id']) not in seen:
                        seen.add((a['id'], b['id']))
                        level_viol.append((a, b, total(a) - total(b)))

    return {
        'upgrade_violations': sorted(upgrade_viol, key=lambda x: -x[3]),
        'level_violations': sorted(level_viol, key=lambda x: -x[2]),
        'militia_excluded': sum(1 for t in all_troops if t['militia']),
        'tolerance': tolerance,
    }


# =============================================================================
# Markdown rendering
# =============================================================================

def fmt_delta(d):
    if d is None:
        return '—'
    return f'{d:+d}' if d != 0 else '0'


def render_parity_matrix(by_culture, cultures):
    """One table per group: rows = culture, cols = level, cell = avg total skill."""
    out = []
    for group in GROUPS:
        # Which levels have any troop in this group?
        levels = sorted({t['level'] for c in cultures for t in by_culture[c]
                         if t['group'] == group and not t['special']})
        levels = [lv for lv in BASELINE_LEVELS if lv in levels]
        if not levels:
            continue
        out.append(f'\n#### {group} — avg total skill (sum of 8) by culture × level\n')
        header = '| Culture | ' + ' | '.join(f'L{lv}' for lv in levels) + ' |'
        sep = '|' + '---|' * (len(levels) + 1)
        out.append(header)
        out.append(sep)

        # Formula reference row (baseline only, no cultural modifier).
        ref_cells = []
        for lv in levels:
            b = rb.GROUP_BASELINES.get(group, {}).get(lv)
            ref_cells.append(str(sum(b.values())) if b else '·')
        out.append('| **FORMULA baseline** | ' + ' | '.join(ref_cells) + ' |')

        for c in cultures:
            row_troops = [t for t in by_culture[c] if t['group'] == group and not t['special']]
            if not row_troops:
                continue
            cells = []
            for lv in levels:
                vals = [t['total_actual'] for t in row_troops if t['level'] == lv]
                cells.append(str(round(sum(vals) / len(vals))) if vals else '·')
            out.append(f'| {c} | ' + ' | '.join(cells) + ' |')
        out.append('')
    return '\n'.join(out)


def render_culture_section(culture, troops, all_ids, dq):
    out = [f'\n### {culture}  ({len(troops)} troops)\n']

    no_identity = culture in dq['cultures_without_identity']
    remap = next((r for r in dq['remap_findings'] if r['file_culture'] == culture), None)
    if no_identity:
        out.append('> ⚠ **No cultural identity defined** — this culture has no `CULTURAL_MODS` entry, '
                   'so troops are measured against the **baseline only** (no faction flavour). '
                   'Deltas below reflect deviation from a generic baseline, not a tuned culture.\n')
    if remap:
        out.append(f'> ⚠ **Modifier key mismatch** — file culture `{culture}` does not match the '
                   f'`CULTURAL_MODS` key `{remap["intended_modifier"]}`; the intended modifier is '
                   f'**silently not applied** by `rebalance_troops.py`. Deltas here use the intended '
                   f'`{remap["intended_modifier"]}` modifier for honesty.\n')

    # Tier histogram (level x group counts)
    levels = sorted({t['level'] for t in troops})
    out.append('**Tier distribution** (count by level × group):\n')
    hdr = '| Level | ' + ' | '.join(GROUPS) + ' | Total |'
    out.append(hdr)
    out.append('|' + '---|' * (len(GROUPS) + 2))
    for lv in levels:
        cells = []
        for g in GROUPS:
            n = sum(1 for t in troops if t['level'] == lv and t['group'] == g)
            cells.append(str(n) if n else '·')
        tot = sum(1 for t in troops if t['level'] == lv)
        out.append(f'| {lv} | ' + ' | '.join(cells) + f' | {tot} |')
    out.append('')

    # Outliers
    outliers = sorted([t for t in troops if t['outlier']], key=lambda x: -abs(x['delta']))
    if outliers:
        out.append(f'**Skill outliers** ({len(outliers)} troops > threshold off the formula):\n')
        out.append('| Troop | L | Group | Actual | Formula | Δ | Biggest skill gaps |')
        out.append('|---|---|---|---|---|---|---|')
        for t in outliers:
            gaps = sorted(t['skill_deltas'].items(), key=lambda kv: -abs(kv[1]))[:3]
            gap_str = ', '.join(f'{k} {fmt_delta(v)}' for k, v in gaps if v != 0)
            out.append(f"| {t['name']} | {t['level']} | {t['group']} | {t['total_actual']} | "
                       f"{t['total_ref']} | {fmt_delta(t['delta'])} | {gap_str} |")
        out.append('')
    else:
        out.append('**Skill outliers:** none beyond threshold. ✅\n')

    # Upgrade-path sanity
    dangling = []
    deadends = []
    max_level = max(levels) if levels else 0
    for t in troops:
        for u in t['upgrades']:
            if u not in all_ids:
                dangling.append((t['id'], u))
        if not t['upgrades'] and t['level'] < max_level and not t['special']:
            deadends.append(t)
    if dangling or deadends:
        out.append('**Upgrade-path notes:**')
        if dangling:
            out.append(f'- Dangling upgrade targets (id not found anywhere): '
                       + ', '.join(f'`{a}` → `{b}`' for a, b in dangling[:20]))
        if deadends:
            names = ', '.join(f"{t['name']} (L{t['level']})" for t in deadends[:20])
            out.append(f'- {len(deadends)} non-terminal troops with **no upgrade path** '
                       f'(below this culture\'s max tier L{max_level}): {names}')
        out.append('')
    else:
        out.append('**Upgrade-path notes:** no dangling refs; no sub-max dead-ends. ✅\n')

    # Weight coverage (focus on elites that probably should carry a weight)
    elite_unweighted = [t for t in troops if t['level'] >= 41 and t['weight'] is None and not t['special']]
    weighted = sum(1 for t in troops if t['weight'] is not None)
    out.append(f'**Troop-weight coverage:** {weighted}/{len(troops)} troops carry an explicit weight.')
    if elite_unweighted:
        names = ', '.join(f"{t['name']} (L{t['level']})" for t in elite_unweighted[:20])
        out.append(f'  ⚠ {len(elite_unweighted)} high-tier (L41+) troops have **no explicit weight** '
                   f'(default 1.0, may under-cost elites): {names}')
    out.append('')

    # Special (creature) + mount-rider troops in this culture
    specials = [t for t in troops if t['special']]
    if specials:
        names = ', '.join(f"{t['name']} (L{t['level']})" for t in specials)
        out.append(f'**Non-humanoid creature troops** (excluded from formula judging): {names}\n')
    riders = [t for t in troops if t['mount_rider']]
    if riders:
        names = ', '.join(
            f"{t['name']} (L{t['level']}, Δ{fmt_delta(t['delta'])})" for t in riders)
        out.append(f'**Mount riders** (humanoid; judged on formula): {names}\n')

    # Naked-troop risk
    naked = [t for t in troops if not t['has_equipment']]
    if naked:
        out.append(f'**Equipment note:** {len(naked)} troop(s) have no `<Equipments>` block: '
                   + ', '.join(f"`{t['id']}`" for t in naked[:20]) + '\n')

    return '\n'.join(out)


def render_report(by_culture, all_troops, all_ids, dq, threshold):
    cultures = sorted(by_culture.keys())
    today = datetime.date.today().isoformat()
    total = len(all_troops)
    outliers = sum(1 for t in all_troops if t['outlier'])
    off_grid = [t for t in all_troops if t['off_grid']]
    dq_count = (len(dq['cultures_without_identity'])
                + len(dq['remap_findings'])
                + len(dq['dead_modifiers']))

    L = []
    L.append('# TAOM Troop Balance Overview\n')
    L.append(f'_Generated {today} by `tools/analyze_troop_balance.py` (READ-ONLY — no troop XML modified)._\n')
    L.append('This report compares every troop\'s actual combat skills against the reference '
             'baseline + cultural-modifier formula in `tools/rebalance_troops.py`. It is a snapshot '
             'to inform a future rebalance; it changes nothing.\n')

    # Executive summary
    L.append('## Executive summary\n')
    L.append(f'- **Cultures:** {len(cultures)} troop files')
    L.append(f'- **Troops analysed:** {total}')
    L.append(f'- **Skill outliers** (>|{threshold}| total skill off formula, excl. creatures/off-grid): '
             f'**{outliers}**')
    L.append(f'- **Off-formula-grid troops** (level not on the baseline grid {BASELINE_LEVELS}): '
             f'{len(off_grid)}'
             + ('' if not off_grid else ' — ' + ', '.join(f"`{t['id']}` (L{t['level']})" for t in off_grid)))
    L.append(f'- **Data-quality / tooling findings:** {dq_count} (see next section)')
    L.append(f'- **Special/creature troops** (formula-exempt): {len(dq["special_troops"])}\n')

    # Data quality
    L.append('## Data-quality & tooling findings\n')
    L.append('These gate any future rebalance run — `rebalance_troops.py` would currently mis-handle them.\n')
    if dq['cultures_without_identity']:
        L.append('**Cultures with NO `CULTURAL_MODS` entry** (get baseline-only skills, no faction identity):')
        for c in dq['cultures_without_identity']:
            L.append(f'- `{c}` — add a modifier block to `rebalance_troops.py` `CULTURAL_MODS` to give it flavour.')
        L.append('')
    if dq['remap_findings']:
        L.append('**Modifier key mismatches** (the intended modifier is silently never applied):')
        for r in dq['remap_findings']:
            status = 'exists' if r['in_mods'] else 'MISSING'
            L.append(f"- file culture `{r['file_culture']}` → intended key `{r['intended_modifier']}` "
                     f"({status} in `CULTURAL_MODS`). The tool derives `{r['file_culture']}` from the "
                     f"filename, so no modifier matches. Fix: rename the file to "
                     f"`troops_{r['intended_modifier']}.xml` **or** add a `{r['file_culture']}` key.")
        L.append('')
    if dq['dead_modifiers']:
        L.append('**Dead `CULTURAL_MODS` entries** (defined but no troops ever resolve to them):')
        for k in dq['dead_modifiers']:
            L.append(f'- `{k}` — no `troops_{k}.xml` on disk and `detect_culture` never returns it. '
                     f'Remove or wire up.')
        L.append('')
    if dq['special_troops']:
        L.append('**Non-humanoid creature troops** (bespoke skills — EXCLUDED from outlier judging '
                 'and from any formula rebalance):')
        for s in dq['special_troops']:
            L.append(f"- `{s['id']}` ({s['name']}, L{s['level']}) in `{s['file']}`")
        L.append('')
    if dq['mount_riders']:
        flagged = sum(1 for r in dq['mount_riders'] if r['outlier'])
        L.append(f'**Mount riders** ({len(dq["mount_riders"])} humanoid riders of warg/elephant/spider) '
                 f'are judged on the formula like any troop — only the non-humanoid mounts above are '
                 f'exempt. {flagged} currently read as skill outliers (may be intentional hand-tuning; '
                 f'review in the per-culture sections).\n')

    # Monotonicity
    m = dq['monotonicity']
    L.append('## Level monotonicity (no lower level stronger than a higher level)\n')
    L.append(f'Professional troops only — **militia excluded** (intentionally over-statted for siege / '
             f'village defense). Total-skill based, ±{m["tolerance"]} tolerance for weapon-spec noise. '
             f'{m["militia_excluded"]} militia not checked.\n')
    if not m['upgrade_violations'] and not m['level_violations']:
        L.append('**No inversions.** ✅ Every professional troop is ≥ all lower-level troops in its '
                 'culture+group, and every upgrade target is higher-level and not weaker.\n')
    else:
        if m['upgrade_violations']:
            L.append(f'**Upgrade-path inversions** ({len(m["upgrade_violations"])}):')
            for t, u, kind, d in m['upgrade_violations']:
                L.append(f"- `{t['id']}` (L{t['level']}) → `{u['id']}` (L{u['level']}) — {kind} by {d}")
            L.append('')
        if m['level_violations']:
            L.append(f'**Within culture+group inversions** ({len(m["level_violations"])}):')
            for a, b, d in m['level_violations']:
                L.append(f"- {a['culture']}/{a['group']}: `{a['id']}` (L{a['level']}) out-totals "
                         f"`{b['id']}` (L{b['level']}) by {d}")
            L.append('')

    # Parity matrix
    L.append('## Cross-culture parity matrix\n')
    L.append('Average total skill (sum of all 8 skills) per culture at each tier, vs the formula '
             'baseline (no cultural modifier). Reads high-to-low = stronger-to-weaker rosters at that tier.\n')
    L.append(render_parity_matrix(by_culture, cultures))

    # Per-culture
    L.append('## Per-culture detail\n')
    for c in cultures:
        L.append(render_culture_section(c, by_culture[c], all_ids, dq))

    # Recommendations
    L.append('\n## Recommendations (NOT applied — proposals only)\n')
    recs = []
    for c in dq['cultures_without_identity']:
        recs.append(f'Add a `CULTURAL_MODS["{c}"]` block so `{c}` troops get a faction identity '
                    f'instead of generic baseline skills.')
    for r in dq['remap_findings']:
        recs.append(f'Resolve the `{r["file_culture"]}` → `{r["intended_modifier"]}` key mismatch '
                    f'(rename file or add key) so the Rhûn-style modifier actually applies.')
    for k in dq['dead_modifiers']:
        recs.append(f'Remove or wire up the dead `CULTURAL_MODS["{k}"]` entry.')
    total_elite_unweighted = sum(
        1 for t in all_troops if t['level'] >= 41 and t['weight'] is None and not t['special'])
    if total_elite_unweighted:
        recs.append(f'Add explicit `troop_weights.xml` entries for the {total_elite_unweighted} L41+ '
                    f'troops currently defaulting to weight 1.0 (see per-culture sections).')
    if outliers:
        recs.append(f'Review the {outliers} skill outliers per-culture; decide intentional vs drift, '
                    f'then run `python tools/rebalance_troops.py --dry-run` to preview a corrective pass.')
    recs.append('Confirm special/creature troops stay excluded before any `--apply` rebalance run.')
    for i, r in enumerate(recs, 1):
        L.append(f'{i}. {r}')
    L.append('')

    return '\n'.join(L)


# =============================================================================
# HTML rendering (self-contained, color-coded — the viewable artifact)
# =============================================================================

import html as _html  # noqa: E402

HTML_CSS = """
:root { color-scheme: dark; }
* { box-sizing: border-box; }
body { font-family: -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif;
  margin: 0; background: #15171c; color: #d8dee9; line-height: 1.5; }
.wrap { max-width: 1500px; margin: 0 auto; padding: 24px 32px 80px; }
h1 { font-size: 28px; margin: 0 0 4px; color: #fff; }
h2 { font-size: 22px; margin: 38px 0 10px; padding-bottom: 6px; border-bottom: 2px solid #2c3340; color: #fff; }
h3 { font-size: 18px; margin: 26px 0 8px; color: #eaeef5; }
h4 { font-size: 15px; margin: 18px 0 6px; color: #b9c2d0; font-weight: 600; }
.sub { color: #8a93a3; font-size: 13px; margin-bottom: 18px; }
code { background: #232733; padding: 1px 5px; border-radius: 4px; font-size: 12px; color: #e0c98a; }
table { border-collapse: collapse; margin: 8px 0 18px; font-size: 12.5px; width: auto; }
th, td { border: 1px solid #2c3340; padding: 4px 8px; text-align: center; white-space: nowrap; }
th { background: #1e222b; color: #aab4c4; font-weight: 600; position: sticky; top: 0; }
td.l, th.l { text-align: left; }
tr:nth-child(even) td { background: #191c23; }
.cell-empty { color: #4b5161; }
.summary { display: flex; flex-wrap: wrap; gap: 12px; margin: 14px 0; }
.card { background: #1e222b; border: 1px solid #2c3340; border-radius: 8px; padding: 12px 16px; min-width: 140px; }
.card .n { font-size: 26px; font-weight: 700; color: #fff; }
.card .lbl { font-size: 11px; color: #8a93a3; text-transform: uppercase; letter-spacing: .04em; }
.callout { background: #20242e; border-left: 4px solid #d9a441; padding: 8px 14px; margin: 8px 0; border-radius: 4px; font-size: 13px; }
.callout.bad { border-left-color: #c0392b; }
.callout.ok { border-left-color: #27ae60; }
.legend { font-size: 12px; color: #8a93a3; margin: 4px 0 10px; }
.legend span { display: inline-block; padding: 1px 8px; border-radius: 3px; margin-right: 4px; color: #fff; }
.tag { display: inline-block; font-size: 11px; padding: 1px 7px; border-radius: 10px; margin-left: 6px; vertical-align: middle; }
.tag.warn { background: #7a4b12; color: #ffd99a; }
.tag.mismatch { background: #5a2d2d; color: #ffb3b3; }
ul { margin: 6px 0 14px; } li { margin: 2px 0; }
details { margin: 6px 0; } summary { cursor: pointer; color: #9db2d6; font-weight: 600; }
.toc { columns: 3; font-size: 13px; margin: 8px 0 10px; }
.toc a { color: #9db2d6; text-decoration: none; } .toc a:hover { text-decoration: underline; }
a.anchor { color: inherit; text-decoration: none; }
"""


def _esc(s):
    return _html.escape(str(s))


def _heat_bg(ratio):
    """Heatmap color for actual/baseline ratio."""
    if ratio is None:
        return ''
    if ratio < 0.55:
        return '#922b21'
    if ratio < 0.70:
        return '#c0392b'
    if ratio < 0.85:
        return '#b9770e'
    if ratio <= 1.12:
        return '#1e7a45'
    if ratio <= 1.45:
        return '#1f5d8c'
    return '#5e3a8c'


def _delta_bg(delta):
    if delta is None:
        return ''
    if delta <= -300:
        return '#922b21'
    if delta <= -150:
        return '#c0392b'
    if delta <= -60:
        return '#b9770e'
    if delta < 60:
        return '#1e7a45'
    if delta < 200:
        return '#1f5d8c'
    return '#5e3a8c'


def _cell(text, bg=''):
    style = f' style="background:{bg};color:#fff"' if bg else ''
    if text in ('·', '—', ''):
        return f'<td class="cell-empty">{_esc(text or "·")}</td>'
    return f'<td{style}>{_esc(text)}</td>'


def html_parity(by_culture, cultures):
    out = ['<p class="legend">Heatmap vs formula baseline: '
           '<span style="background:#922b21">&lt;55%</span>'
           '<span style="background:#c0392b">55–70%</span>'
           '<span style="background:#b9770e">70–85%</span>'
           '<span style="background:#1e7a45">85–112% (on-target)</span>'
           '<span style="background:#1f5d8c">112–145%</span>'
           '<span style="background:#5e3a8c">&gt;145% (elite)</span></p>']
    for group in GROUPS:
        levels = sorted({t['level'] for c in cultures for t in by_culture[c]
                         if t['group'] == group and not t['special']})
        levels = [lv for lv in BASELINE_LEVELS if lv in levels]
        if not levels:
            continue
        out.append(f'<h4>{group} — avg total skill by culture × level</h4>')
        out.append('<table><tr><th class="l">Culture</th>'
                   + ''.join(f'<th>L{lv}</th>' for lv in levels) + '</tr>')
        base = {lv: (sum(rb.GROUP_BASELINES.get(group, {}).get(lv, {}).values()) or None) for lv in levels}
        ref_cells = ''.join(
            f'<td style="background:#11141a;color:#cdd6e5">{base[lv]}</td>' if base[lv] else '<td class="cell-empty">·</td>'
            for lv in levels)
        out.append(f'<tr><td class="l"><b>FORMULA baseline</b></td>{ref_cells}</tr>')
        for c in cultures:
            row_troops = [t for t in by_culture[c] if t['group'] == group and not t['special']]
            if not row_troops:
                continue
            cells = ''
            for lv in levels:
                vals = [t['total_actual'] for t in row_troops if t['level'] == lv]
                if not vals:
                    cells += '<td class="cell-empty">·</td>'
                    continue
                avg = round(sum(vals) / len(vals))
                ratio = (avg / base[lv]) if base[lv] else None
                cells += _cell(avg, _heat_bg(ratio))
            out.append(f'<tr><td class="l">{_esc(c)}</td>{cells}</tr>')
        out.append('</table>')
    return '\n'.join(out)


def html_culture(culture, troops, all_ids, dq):
    anchor = 'c_' + culture
    badges = ''
    if culture in dq['cultures_without_identity']:
        badges += '<span class="tag warn">no cultural identity</span>'
    if any(r['file_culture'] == culture for r in dq['remap_findings']):
        badges += '<span class="tag mismatch">modifier key mismatch</span>'
    out = [f'<h3 id="{anchor}"><a class="anchor" href="#{anchor}">{_esc(culture)}</a> '
           f'<span class="sub" style="display:inline">({len(troops)} troops)</span>{badges}</h3>']

    levels = sorted({t['level'] for t in troops})
    # Tier distribution
    out.append('<table><tr><th>Level</th>' + ''.join(f'<th>{g}</th>' for g in GROUPS) + '<th>Total</th></tr>')
    for lv in levels:
        cells = ''
        for g in GROUPS:
            n = sum(1 for t in troops if t['level'] == lv and t['group'] == g)
            cells += f'<td>{n}</td>' if n else '<td class="cell-empty">·</td>'
        tot = sum(1 for t in troops if t['level'] == lv)
        out.append(f'<tr><td>{lv}</td>{cells}<td><b>{tot}</b></td></tr>')
    out.append('</table>')

    outliers = sorted([t for t in troops if t['outlier']], key=lambda x: -abs(x['delta']))
    if outliers:
        out.append(f'<details open><summary>{len(outliers)} skill outliers (&gt;|threshold| off formula)</summary>')
        out.append('<table><tr><th class="l">Troop</th><th>L</th><th>Group</th><th>Actual</th>'
                   '<th>Formula</th><th>Δ</th><th class="l">Biggest skill gaps</th></tr>')
        for t in outliers:
            gaps = sorted(t['skill_deltas'].items(), key=lambda kv: -abs(kv[1]))[:3]
            gap_str = ', '.join(f'{k} {fmt_delta(v)}' for k, v in gaps if v != 0)
            rider = ' 🐺' if t.get('mount_rider') else ''
            out.append(f'<tr><td class="l">{_esc(t["name"])}{rider}</td><td>{t["level"]}</td>'
                       f'<td>{t["group"]}</td><td>{t["total_actual"]}</td><td>{t["total_ref"]}</td>'
                       f'{_cell(fmt_delta(t["delta"]), _delta_bg(t["delta"]))}'
                       f'<td class="l">{_esc(gap_str)}</td></tr>')
        out.append('</table></details>')
    else:
        out.append('<div class="callout ok">No skill outliers beyond threshold.</div>')

    # Upgrade + weight notes (compact)
    notes = []
    dangling = [(t['id'], u) for t in troops for u in t['upgrades'] if u not in all_ids]
    max_level = max(levels) if levels else 0
    deadends = [t for t in troops if not t['upgrades'] and t['level'] < max_level and not t['special']]
    if dangling:
        notes.append('Dangling upgrade targets: ' + ', '.join(f'<code>{_esc(a)}</code>→<code>{_esc(b)}</code>'
                                                              for a, b in dangling[:12]))
    if deadends:
        notes.append(f'{len(deadends)} non-terminal troops with no upgrade path (below L{max_level}): '
                     + ', '.join(_esc(f"{t['name']} (L{t['level']})") for t in deadends[:12]))
    elite_unweighted = [t for t in troops if t['level'] >= 41 and t['weight'] is None and not t['special']]
    weighted = sum(1 for t in troops if t['weight'] is not None)
    notes.append(f'Troop-weight coverage: {weighted}/{len(troops)} carry an explicit weight.'
                 + (f' <b>{len(elite_unweighted)} L41+ troops have no explicit weight.</b>' if elite_unweighted else ''))
    riders = [t for t in troops if t['mount_rider']]
    if riders:
        notes.append('Mount riders (humanoid, judged on formula): '
                     + ', '.join(_esc(f"{t['name']} (L{t['level']}, Δ{fmt_delta(t['delta'])})") for t in riders))
    specials = [t for t in troops if t['special']]
    if specials:
        notes.append('Non-humanoid creatures (formula-exempt): '
                     + ', '.join(_esc(f"{t['name']} (L{t['level']})") for t in specials))
    if notes:
        out.append('<ul>' + ''.join(f'<li>{n}</li>' for n in notes) + '</ul>')
    return '\n'.join(out)


def render_html(by_culture, all_troops, all_ids, dq, threshold):
    cultures = sorted(by_culture.keys())
    today = datetime.date.today().isoformat()
    total = len(all_troops)
    outliers = sum(1 for t in all_troops if t['outlier'])
    off_grid = [t for t in all_troops if t['off_grid']]
    dq_count = (len(dq['cultures_without_identity']) + len(dq['remap_findings']) + len(dq['dead_modifiers']))

    H = ['<!DOCTYPE html><html lang="en"><head><meta charset="utf-8">',
         '<meta name="viewport" content="width=device-width, initial-scale=1">',
         '<title>TAOM Troop Balance Overview</title>',
         f'<style>{HTML_CSS}</style></head><body><div class="wrap">']
    H.append('<h1>TAOM Troop Balance Overview</h1>')
    H.append(f'<div class="sub">Generated {today} by <code>tools/analyze_troop_balance.py</code> '
             '— READ-ONLY, no troop XML modified. Actual skills vs the '
             '<code>rebalance_troops.py</code> baseline + cultural-modifier formula.</div>')

    H.append('<div class="summary">')
    for n, lbl in [(len(cultures), 'cultures'), (total, 'troops'),
                   (outliers, f'outliers (&gt;|{threshold}|)'), (len(off_grid), 'off-grid'),
                   (dq_count, 'data-quality issues'), (len(dq['special_troops']), 'creatures (exempt)')]:
        H.append(f'<div class="card"><div class="n">{n}</div><div class="lbl">{lbl}</div></div>')
    H.append('</div>')

    # TOC
    H.append('<div class="toc">' + ' '.join(f'<a href="#c_{c}">{_esc(c)}</a>' for c in cultures) + '</div>')

    # Data quality
    H.append('<h2>Data-quality &amp; tooling findings</h2>')
    H.append('<p class="sub">These gate any future rebalance run.</p>')
    if dq['cultures_without_identity']:
        H.append('<div class="callout bad"><b>No <code>CULTURAL_MODS</code> entry</b> (baseline-only skills): '
                 + ', '.join(f'<code>{_esc(c)}</code>' for c in dq['cultures_without_identity']) + '</div>')
    for r in dq['remap_findings']:
        H.append(f'<div class="callout bad"><b>Modifier key mismatch:</b> file culture '
                 f'<code>{_esc(r["file_culture"])}</code> → intended key <code>{_esc(r["intended_modifier"])}</code> '
                 f'is silently never applied (tool derives the key from the filename). Rename file or add the key.</div>')
    if dq['dead_modifiers']:
        H.append('<div class="callout"><b>Dead <code>CULTURAL_MODS</code> entries</b> (never resolved): '
                 + ', '.join(f'<code>{_esc(k)}</code>' for k in dq['dead_modifiers']) + '</div>')
    if dq['special_troops']:
        H.append('<div class="callout">Non-humanoid creature troops (formula-exempt): '
                 + ', '.join(f'<code>{_esc(s["id"])}</code> ({_esc(s["name"])})' for s in dq['special_troops'])
                 + '</div>')
    if dq['mount_riders']:
        flagged = sum(1 for r in dq['mount_riders'] if r['outlier'])
        H.append(f'<div class="callout">{len(dq["mount_riders"])} mount riders (warg/elephant/spider) are '
                 f'humanoid and judged on the formula; {flagged} read as outliers (possible hand-tuning).</div>')
    if off_grid:
        H.append('<div class="callout">Off-formula-grid troops (level not on the baseline grid): '
                 + ', '.join(f'<code>{_esc(t["id"])}</code> (L{t["level"]})' for t in off_grid) + '</div>')

    # Monotonicity
    m = dq['monotonicity']
    H.append('<h2>Level monotonicity</h2>')
    H.append(f'<p class="sub">No lower-level troop stronger than a higher-level one. Professional troops '
             f'only — <b>{m["militia_excluded"]} militia excluded</b> (intentionally over-statted for siege / '
             f'village defense). Total-skill based, ±{m["tolerance"]} tolerance for weapon-spec noise.</p>')
    if not m['upgrade_violations'] and not m['level_violations']:
        H.append('<div class="callout ok">No inversions — every upgrade target is higher-level and not '
                 'weaker, and no lower level out-totals a higher level within a culture+group.</div>')
    else:
        for t, u, kind, d in m['upgrade_violations']:
            H.append(f'<div class="callout bad">Upgrade: <code>{_esc(t["id"])}</code> (L{t["level"]}) → '
                     f'<code>{_esc(u["id"])}</code> (L{u["level"]}) — {_esc(kind)} by {d}</div>')
        for a, b, d in m['level_violations']:
            H.append(f'<div class="callout bad">{_esc(a["culture"])}/{_esc(a["group"])}: '
                     f'<code>{_esc(a["id"])}</code> (L{a["level"]}) out-totals '
                     f'<code>{_esc(b["id"])}</code> (L{b["level"]}) by {d}</div>')

    # Parity
    H.append('<h2>Cross-culture parity matrix</h2>')
    H.append('<p class="sub">Average total skill (sum of 8 skills) per culture at each tier, '
             'colored vs the formula baseline. Red = under-tuned, green = on-target, purple = elite.</p>')
    H.append(html_parity(by_culture, cultures))

    # Per-culture
    H.append('<h2>Per-culture detail</h2>')
    for c in cultures:
        H.append(html_culture(c, by_culture[c], all_ids, dq))

    # Recommendations
    H.append('<h2>Recommendations (proposals — not applied)</h2><ol>')
    recs = []
    for c in dq['cultures_without_identity']:
        recs.append(f'Add a <code>CULTURAL_MODS["{_esc(c)}"]</code> block so <code>{_esc(c)}</code> '
                    'troops get a faction identity instead of generic baseline skills.')
    for r in dq['remap_findings']:
        recs.append(f'Resolve the <code>{_esc(r["file_culture"])}</code> → '
                    f'<code>{_esc(r["intended_modifier"])}</code> key mismatch so the modifier applies.')
    for k in dq['dead_modifiers']:
        recs.append(f'Remove or wire up the dead <code>CULTURAL_MODS["{_esc(k)}"]</code> entry.')
    total_elite_unweighted = sum(1 for t in all_troops
                                 if t['level'] >= 41 and t['weight'] is None and not t['special'])
    if total_elite_unweighted:
        recs.append(f'Add explicit <code>troop_weights.xml</code> entries for the {total_elite_unweighted} '
                    'L41+ troops defaulting to weight 1.0.')
    if outliers:
        recs.append(f'Review the {outliers} skill outliers (most are post-#212 elites authored below the '
                    'curve); decide intentional vs drift, then <code>rebalance_troops.py --dry-run</code> to preview.')
    recs.append('Confirm creature troops stay excluded before any <code>--apply</code> rebalance run.')
    H.append(''.join(f'<li>{r}</li>' for r in recs))
    H.append('</ol>')

    H.append('</div></body></html>')
    return '\n'.join(H)


def build_json(all_troops):
    return [{
        'id': t['id'], 'name': t['name'], 'culture': t['culture'], 'file': t['file'],
        'level': t['level'], 'group': t['group'], 'special': t['special'],
        'mount_rider': t['mount_rider'], 'off_grid': t['off_grid'],
        'total_actual': t['total_actual'], 'total_ref': t['total_ref'], 'delta': t['delta'],
        'outlier': t['outlier'], 'weight': t['weight'], 'skill_deltas': t['skill_deltas'],
        'actual': t['actual'],
    } for t in all_troops]


# =============================================================================
# Main
# =============================================================================

def main():
    ap = argparse.ArgumentParser(description='Read-only TAOM troop balance overview generator.')
    ap.add_argument('--outlier-threshold', type=int, default=50,
                    help='Total-skill delta (abs) above which a troop is flagged an outlier (default 50).')
    ap.add_argument('--stdout', action='store_true', help='Also print the executive summary to stdout.')
    args = ap.parse_args()

    import glob
    files = sorted(glob.glob(os.path.join(TROOPS_DIR, 'troops_*.xml')))
    if not files:
        print(f'ERROR: no troop files found under {TROOPS_DIR}', file=sys.stderr)
        sys.exit(1)

    weights = parse_weights()

    # Equipment-driven specialization (mirrors rebalance_troops.py). READ-ONLY
    # tool, so a missing game install degrades to name-only detection with a
    # loud warning instead of failing (per environment-failures.md).
    import taom_schema as ts
    game_modules = rb.DEFAULT_GAME_MODULES
    item_classes = None
    if os.path.isdir(game_modules):
        item_classes = ts.build_item_class_registry(rb.MODULEDATA_DIR, game_modules)
    else:
        print(f'WARNING: Bannerlord Modules folder not found: {game_modules}\n'
              f'         Weapon specialization falls back to NAME-ONLY detection — the\n'
              f'         "ideal" curve may mis-judge crossbow / two-hander troops.',
              file=sys.stderr)

    by_culture = defaultdict(list)
    all_troops = []
    file_cultures = []
    for fp in files:
        fc, troops = parse_troop_file(fp, item_classes)
        file_cultures.append(fc)
        for t in troops:
            by_culture[t['culture']].append(t)
            all_troops.append(t)

    analyze(all_troops, weights, args.outlier_threshold)
    all_ids = {t['id'] for t in all_troops}
    dq = data_quality(all_troops, file_cultures)
    dq['monotonicity'] = check_monotonicity(all_troops)

    report = render_report(by_culture, all_troops, all_ids, dq, args.outlier_threshold)
    report_html = render_html(by_culture, all_troops, all_ids, dq, args.outlier_threshold)

    os.makedirs(REPORT_DIR, exist_ok=True)
    with open(REPORT_MD, 'w', encoding='utf-8') as f:
        f.write(report)
    with open(REPORT_HTML, 'w', encoding='utf-8') as f:
        f.write(report_html)
    with open(REPORT_JSON, 'w', encoding='utf-8') as f:
        json.dump(build_json(all_troops), f, indent=2)

    print(f'Processed {len(files)} troop files, {len(all_troops)} troops, '
          f'{len(by_culture)} cultures.')
    print(f'Wrote {os.path.relpath(REPORT_MD, REPO_ROOT)}')
    print(f'Wrote {os.path.relpath(REPORT_HTML, REPO_ROOT)}')
    print(f'Wrote {os.path.relpath(REPORT_JSON, REPO_ROOT)}')

    if args.stdout:
        outliers = sum(1 for t in all_troops if t['outlier'])
        print('\n--- Executive summary ---')
        print(f'Cultures: {len(by_culture)}  Troops: {len(all_troops)}  Outliers: {outliers}')
        print(f'No-identity cultures: {dq["cultures_without_identity"]}')
        print(f'Remap findings: {[r["file_culture"] for r in dq["remap_findings"]]}')
        print(f'Dead modifiers: {dq["dead_modifiers"]}')
        print(f'Special/creature troops: {len(dq["special_troops"])}')
        m = dq['monotonicity']
        print(f'Monotonicity (professional troops, militia excluded): '
              f'{len(m["upgrade_violations"])} upgrade + {len(m["level_violations"])} level inversions')


if __name__ == '__main__':
    main()
