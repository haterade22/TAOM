#!/usr/bin/env python3
"""Kingdom armour overview: how every culture's troops are armoured, tier by tier, side by side.

READ-ONLY. Writes tools/reports/kingdom-armour/{REPORT.md,REPORT.html,kingdom-armour.json} and
nothing else. There is no --apply.

WHY
---
On 2026-09-12 a Dunland level-31 noble (engine tier 6, 182 armour, a lord-row helmet) out-armoured
Gondor's level-46 Moon Guard (tier 9, 180) and five of Gondor's twelve level-41 capstones, and a
Dunland level-26 helmet (40) beat every Minas Tirith helmet in the tree (33). Nothing along the way
compared kingdoms with each other: rebalance_armor.py and analyze_armor_balance.py judge ITEMS
within one culture, fix_upgrade_armour_regressions.py and the UPGRADE_ARMOUR_REGRESSION gate judge
one upgrade EDGE at a time. The structural cause is that the item curve has ONE row, "elite", for
levels 31 to 51, so a culture whose tree stops at level 31 and one that runs to level 51 target the
same stats (#581).

WHAT COUNTS
-----------
A troop's armour is the four engine regions (head, body, arm, leg), each summed over the five
armour slots (Head, Body, Cape, Gloves, Leg) exactly as Equipment.GetHeadArmorSum /
GetHumanBodyArmorSum / GetArmArmorSum / GetLegArmorSum do (v1.4.8 Equipment.cs:271-325): a chest
contributes arm armour, a cape contributes body armour. Each region is averaged over the troop's
BATTLE equipment sets, an unfilled slot counting 0 because the engine draws each slot from an
independently chosen set (.claude/rules/troops.md); civilian sets are excluded. The total is the
sum of the four regions, which equals fix_upgrade_armour_regressions.total on the same slots.
Troops are placed by engine tier, clamp(ceil((level - 5) / 5), 0, 10), the tier the party screen
shows (taom_schema.Validator._troop_tier). The per-slot draw is the campaign spawn path's
behaviour: Equipment.GetRandomEquipmentElements redraws the set per slot whenever the seed is not
-1, and CharacterHelper.GetPartyMemberFaceSeed never yields -1 (.claude/rules/troops.md).

Creature troops, the two bespoke mount riders, troops with no level= and troops with no battle set
are excluded from every statistic; militia and standalone troops (no upgrade edge in or out) are
tagged, not excluded; the bare-chested-by-design troops are compared without Body and Cape and
left out of the matrices. The validator's _ARMOUR_LADDER_EXEMPT troops (kit off the ladder by
design) leave the matrices, the pair list and the gate exactly as they leave the validator, and
stay in the per-culture troop tables with a ladder_exempt tag, so an exempt capstone cannot top
the worst-pairs list and read as a live regression. Villagers (characters/npcs_*.xml) are not a
culture.

WHAT IT REPORTS
---------------
  1. culture x engine-tier matrices: total armour and each region, median (min-max) n;
  2. cross-culture inversions: a lower-tier troop of another culture out-armouring a higher-tier
     troop by more than --threshold with at least --min-tier-gap tiers between them, aggregated per
     culture pair plus the worst pairs;
  3. the validator's CROSS_CULTURE_ARMOUR_INVERSION verdict, computed by its own function and
     constants (taom_schema.cross_culture_armour_inversions), so tool and gate cannot disagree;
  4. per culture: the Armory ceiling per slot (best troop-eligible item in the folders the culture
     actually wears vs the best it wears) and the unworn elite/lord-row items, i.e. the re-slot
     reserve, each judged on the curve of the FOLDER it was authored in (Umbar wears Harad's
     folder, whose items were statted on Harad's -3, not Umbar's -1); the item curve's prediction
     per tier vs actual, on the wearer's own curve; every troop with its regions.

Usage:
    python tools/analyze_kingdom_armour.py                     # writes the three reports
    python tools/analyze_kingdom_armour.py --stdout            # also prints the summary
    python tools/analyze_kingdom_armour.py --culture gondor    # narrows the detail sections
    python tools/analyze_kingdom_armour.py --threshold 30 --min-tier-gap 3
    python tools/analyze_kingdom_armour.py --game-modules "<.../Modules>"
"""

import argparse
import html as _html
import json
import os
import re
import statistics
import sys
from collections import Counter, defaultdict
from datetime import datetime, timezone

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import analyze_troop_balance as atb  # noqa: E402
import derive_armor_tiers as dat  # noqa: E402
import fix_upgrade_armour_regressions as fx  # noqa: E402
import rebalance_armor as ra  # noqa: E402
import rebalance_troops as rb  # noqa: E402
import taom_schema as ts  # noqa: E402

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
REPORT_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'kingdom-armour')
REPORT_MD = 'REPORT.md'
REPORT_HTML = 'REPORT.html'
REPORT_JSON = 'kingdom-armour.json'

ARMOUR_SLOTS = fx.ARMOUR_SLOTS                 # ('Head', 'Body', 'Cape', 'Gloves', 'Leg')
REGIONS = fx.ARMOUR_STATS                      # ('head_armor', 'body_armor', 'arm_armor', 'leg_armor')
REGION_LABELS = {'head_armor': 'head', 'body_armor': 'body', 'arm_armor': 'arm', 'leg_armor': 'leg'}
SLOT_TO_CURVE = dat.EQUIP_SLOT_MAP             # Head->head, Body->body, Leg->leg, Gloves->arm, Cape->shoulder
ITEM_TYPE_TO_SLOT = {'HeadArmor': 'Head', 'BodyArmor': 'Body', 'HandArmor': 'Gloves',
                     'LegArmor': 'Leg', 'Cape': 'Cape'}
ENGINE_TIERS = tuple(range(0, ts.Validator._MAX_CHARACTER_TIER + 1))

# Troop-file culture -> rebalance_armor.CULTURAL_MODS key, for the four file cultures the curve
# has no entry for. Each alias is the folder the culture's rosters actually wear (measured
# 2026-09-12: goblin wears mordor items, lindon the rivendell set, rhun_new the rhun folder).
CURVE_CULTURE_ALIASES = {'dolguldur': 'dol_guldur', 'rhun_new': 'rhun', 'lindon': 'rivendell',
                         'goblin': 'mordor'}

# Humanoid riders of a bespoke mount at level 51 in light kit: hand-tuned, off the ladder, and
# already skipped by the skill tool. Kept explicit rather than derived from rb.SKIP_TROOP_IDS,
# which also names three ordinary crossbow troops; a test pins the subset relation.
BESPOKE_RIDERS = frozenset({'harad_elephant_rider', 'harad_mumakil_rider'})
# A troop line inside one troop file that is dressed from its own armour line (the same prefixes
# tools/ranged_ladders.json routes); every other troop is held to its file culture's default line.
TROOP_LINE_PREFIXES = (('mordor_num_', 'mordor_numenorean'), ('mordor_uruk_', 'mordor_uruk'))

DEFAULT_THRESHOLD = 20
DEFAULT_MIN_TIER_GAP = 2
WORST_PAIRS_MD = 25
WORST_PAIRS_JSON = 100


# =============================================================================
# Per-troop model
# =============================================================================

def engine_tier(level):
    return ts.Validator._troop_tier(level)


def file_culture(troop):
    name = re.split(r'[\\/]', troop['file'])[-1]
    if name.startswith('troops_') and name.endswith('.xml'):
        return name[len('troops_'):-len('.xml')]
    return name


def curve_culture(troop):
    if troop['id'].startswith('iron_hills'):
        return 'iron_hills'
    fc = file_culture(troop)
    return CURVE_CULTURE_ALIASES.get(fc, fc)


def judged_slots(*troops):
    """Every armour slot, minus Body and Cape when any of the troops is bare-chested by design
    (their skirt sits in the Cape slot as the chest stand-in; fx.compared_slots rule)."""
    if any(t['id'] in fx.BODYLESS_BY_DESIGN for t in troops):
        return tuple(s for s in ARMOUR_SLOTS if s not in ('Body', 'Cape'))
    return ARMOUR_SLOTS


def region_values(troop, items, slots=ARMOUR_SLOTS):
    """{region: [per battle set: the region summed over the slots]}; unfilled or unknown = 0."""
    out = {r: [] for r in REGIONS}
    for st in troop['sets']:
        for r in REGIONS:
            total = 0
            for s in slots:
                iid = st.get(s)
                if iid:
                    total += items.get(iid, {}).get('stats', {}).get(r, 0)
            out[r].append(total)
    return out


def region_avg(troop, items, slots=ARMOUR_SLOTS):
    vals = region_values(troop, items, slots)
    return {r: (sum(v) / len(v) if v else 0.0) for r, v in vals.items()}


def troop_total(troop, items, slots=ARMOUR_SLOTS):
    return sum(region_avg(troop, items, slots).values())


def troop_scaled_total(troop, items, slots=ARMOUR_SLOTS):
    """The troop's total with every item scaled to the reference cap of the line it belongs to
    (taom_schema.item_cap_for on the item's Armory folder), exactly as the validator scales it.
    Kingdoms differ in armour power by design; this is the "dressed below its power" number."""
    n = len(troop['sets'])
    if not n:
        return 0.0
    total = 0.0
    for st in troop['sets']:
        for s in slots:
            iid = st.get(s)
            if not iid:
                continue
            it = items.get(iid, {})
            total += ts.scale_to_reference_cap(it.get('value', 0), ts.item_cap_for(iid, it.get('folder')))
    return total / n


def filled_fraction(troop):
    """{slot: share of battle sets that fill it}; 0 for a troop with no battle sets."""
    n = len(troop['sets'])
    return {s: (sum(1 for st in troop['sets'] if st.get(s)) / n if n else 0.0) for s in ARMOUR_SLOTS}


def classify(troop, militia, indeg, outdeg):
    """(tags, excluded). Excluded troops take part in no statistic; tagged ones do."""
    tags = []
    name = troop.get('name', '')
    if atb.is_creature_troop(troop['id'], name):
        tags.append('creature')
    if troop['id'] in BESPOKE_RIDERS:
        tags.append('bespoke_rider')
    elif atb.is_mount_rider(troop['id'], name):
        tags.append('mount_rider')
    if troop['id'] in fx.BODYLESS_BY_DESIGN:
        tags.append('bodyless')
    if troop['id'] in militia:
        tags.append('militia')
    if not indeg.get(troop['id'], 0) and not outdeg.get(troop['id'], 0):
        tags.append('standalone')
    if not troop['sets']:
        tags.append('no_sets')
    if not troop.get('has_level', True):
        tags.append('no_level')  # the validator skips these too (level None)
    if troop['id'] in ts.Validator._ARMOUR_LADDER_EXEMPT:
        tags.append('ladder_exempt')
    excluded = bool({'creature', 'bespoke_rider', 'no_sets', 'no_level'} & set(tags))
    return tuple(tags), excluded


def predicted_regions(troop, slots=ARMOUR_SLOTS):
    """What the item curve (rebalance_armor) predicts for a fully slotted troop at this level,
    summed into the four regions. Flat for levels 31 to 51 by construction."""
    tier = dat.level_to_tier(troop['level'])
    culture = curve_culture(troop)
    out = {r: 0 for r in REGIONS}
    for s in slots:
        st = ra.calculate_stats(tier, SLOT_TO_CURVE[s], culture)
        for r in REGIONS:
            out[r] += st.get(r, 0)
    return out


def _degrees(troops):
    indeg, outdeg = Counter(), Counter()
    for t in troops.values():
        for u in t['upgrades']:
            if u in troops:
                outdeg[t['id']] += 1
                indeg[u] += 1
    return indeg, outdeg


def build_records(troops, items, militia):
    """One record per troop-file troop (villagers dropped), in file order then id."""
    indeg, outdeg = _degrees(troops)
    records = []
    for tid in sorted(troops, key=lambda i: (troops[i]['file'], i)):
        t = troops[tid]
        if t['external']:
            continue
        tags, excluded = classify(t, militia, indeg, outdeg)
        slots = judged_slots(t)
        regions = region_avg(t, items, slots)
        predicted = predicted_regions(t, slots)
        total = sum(regions.values())
        ptotal = sum(predicted.values())
        records.append({
            'id': tid, 'name': t.get('name', ''), 'culture': file_culture(t),
            'curve_culture': curve_culture(t), 'level': t['level'], 'tier': engine_tier(t['level']),
            'group': t.get('group', ''), 'tags': tags, 'excluded': excluded,
            'n_sets': len(t['sets']), 'filled': filled_fraction(t),
            'regions': regions, 'total': total,
            'scaled_total': troop_scaled_total(t, items, slots),
            'predicted': predicted, 'predicted_total': ptotal, 'delta': total - ptotal,
        })
    return records


# =============================================================================
# Matrices, inversions, ceilings, curve view, gate preview
# =============================================================================

def _ladder_records(records):
    """The troops the ladder is judged on: no excluded and no ladder-exempt troop. Shared by the
    matrices and the pair list, so an exempt capstone cannot top the worst-pairs list."""
    return [r for r in records if not r['excluded'] and 'ladder_exempt' not in r['tags']]


def _matrix_records(records):
    return [r for r in _ladder_records(records) if 'bodyless' not in r['tags']]


def cells(records, key='total'):
    """{(culture, tier): [values]} over the troops the matrices count."""
    out = defaultdict(list)
    for r in _matrix_records(records):
        out[(r['culture'], r['tier'])].append(r['total'] if key == 'total' else r['regions'][key])
    return dict(out)


def cell_stats(values):
    return {'median': float(statistics.median(values)), 'min': float(min(values)),
            'max': float(max(values)), 'n': len(values)}


def matrices(records):
    out = {}
    for key in ('total',) + tuple(REGIONS):
        table = defaultdict(dict)
        for (culture, tier), vals in cells(records, key).items():
            table[culture][tier] = cell_stats(vals)
        out[key] = dict(table)
    return out


def cross_culture_inversions(records, items, troops, threshold=DEFAULT_THRESHOLD,
                             min_gap=DEFAULT_MIN_TIER_GAP):
    """Every (strong, weak) pair of different cultures where the STRONG troop sits at least
    min_gap tiers below the weak one and still totals more than threshold points above it.
    Bare-chested pairs are recomputed on the slots both can be judged on."""
    included = _ladder_records(records)
    pairs = []
    for weak in included:
        for strong in included:
            if strong['culture'] == weak['culture'] or strong['tier'] > weak['tier'] - min_gap:
                continue
            slots = judged_slots(troops[strong['id']], troops[weak['id']])
            # Kingdoms differ in armour power by design (the chest caps); compare the troops on
            # kit scaled to the reference cap of the line each item belongs to.
            s_total = troop_scaled_total(troops[strong['id']], items, slots)
            w_total = troop_scaled_total(troops[weak['id']], items, slots)
            diff = s_total - w_total
            if diff <= threshold:
                continue
            pairs.append({
                'strong': strong['id'], 'strong_culture': strong['culture'],
                'strong_level': strong['level'], 'strong_tier': strong['tier'], 'strong_total': s_total,
                'weak': weak['id'], 'weak_culture': weak['culture'],
                'weak_level': weak['level'], 'weak_tier': weak['tier'], 'weak_total': w_total,
                'gap': weak['tier'] - strong['tier'], 'diff': diff,
                'slots': slots,
            })
    pairs.sort(key=lambda p: (-p['diff'], p['weak'], p['strong']))
    return pairs


def aggregate_inversions(pairs):
    agg = {}
    for p in pairs:
        key = (p['strong_culture'], p['weak_culture'])
        a = agg.setdefault(key, {'count': 0, 'worst': p, 'tiers': set(), 'weak_ids': set()})
        a['count'] += 1
        a['tiers'].add((p['strong_tier'], p['weak_tier']))
        a['weak_ids'].add(p['weak'])
        if p['diff'] > a['worst']['diff']:
            a['worst'] = p
    return agg


def worn_by_index(troops):
    """{item id: set of troop ids wearing it in a battle set}, villagers included (an item a
    villager wears is still worn)."""
    worn = defaultdict(set)
    for t in troops.values():
        for st in t['sets']:
            for iid in st.values():
                if iid:
                    worn[iid].add(t['id'])
    return dict(worn)


def worn_folders(culture, records, troops, items):
    """Share of the culture's battle-set slot fills per Armory folder (None = vanilla / repo)."""
    fills = Counter()
    for r in records:
        if r['culture'] != culture or r['excluded']:
            continue
        for st in troops[r['id']]['sets']:
            for iid in st.values():
                if iid:
                    fills[items.get(iid, {}).get('folder')] += 1
    n = sum(fills.values())
    return {k: v / n for k, v in fills.items()} if n else {}


def ceilings(culture, records, troops, items, worn_by):
    """Per slot: the best troop-eligible item available in the folders this culture wears vs the
    best it actually wears, plus the unworn elite/lord-row items (the re-slot reserve).

    An item's tier is judged on the curve of the FOLDER it lives in, not the wearer's: the
    Armory folders are named after rebalance_armor's culture keys and their items were statted on
    that culture's protection modifier. Umbar wears Harad's folder (-3) while carrying its own -1;
    judged on Umbar's curve two Harad elite pieces read as heavy and vanished from Umbar's reserve
    list (deep review, 2026-09-12)."""
    folders = worn_folders(culture, records, troops, items)
    folder_keys = {f for f in folders if f}
    worn_here = set()
    for r in records:
        if r['culture'] == culture and not r['excluded']:
            for st in troops[r['id']]['sets']:
                worn_here.update(i for i in st.values() if i)
    slots = {}
    unworn = []
    for slot in ARMOUR_SLOTS:
        cslot = SLOT_TO_CURVE[slot]
        primary_stat = ra.GOVERNED_STATS[cslot][0]
        avail = []
        for iid, rec in items.items():
            if rec.get('folder') not in folder_keys or ITEM_TYPE_TO_SLOT.get(rec.get('type')) != slot:
                continue
            primary = rec['stats'].get(primary_stat, 0)
            hero = ra.is_excluded(iid, rec.get('name', '')) and iid not in worn_by
            avail.append((primary, iid, hero, rec))
        eligible = [a for a in avail if not a[2]]
        best = max(eligible, default=None, key=lambda a: (a[0], a[1]))
        best_any = max(avail, default=None, key=lambda a: (a[0], a[1]))
        worn_vals = [(items[i]['stats'].get(primary_stat, 0), i) for i in worn_here
                     if i in items and ITEM_TYPE_TO_SLOT.get(items[i].get('type')) == slot]
        worn_best = max(worn_vals, default=None)
        slots[slot] = {
            'stat': primary_stat,
            'avail_max': best[0] if best else None, 'avail_max_item': best[1] if best else None,
            'avail_max_incl_hero': best_any[0] if best_any else None,
            'worn_max': worn_best[0] if worn_best else None,
            'worn_max_item': worn_best[1] if worn_best else None,
        }
        for primary, iid, hero, rec in eligible:
            if iid in worn_by:
                continue
            tier = ra.tier_from_value(primary, cslot, rec.get('folder') or culture, item_id=iid)
            if tier in ('elite', 'lord'):
                unworn.append({'id': iid, 'name': rec.get('name', ''), 'slot': slot,
                               'primary': primary, 'tier': tier, 'folder': rec.get('folder')})
    unworn.sort(key=lambda u: (ARMOUR_SLOTS.index(u['slot']), -u['primary'], u['id']))
    return {'folders': folders, 'slots': slots, 'unworn_elite': unworn}


def _culture_cap(troop):
    """The chest cap of the line this troop is dressed from by design (its own sub-line for the
    Mordor Black Numenoreans and Black Uruks, else its file culture's default line), or None."""
    for prefix, line in TROOP_LINE_PREFIXES:
        if troop['id'].startswith(prefix):
            return ra.KINGDOM_CAPS.get(line)
    return ra.KINGDOM_CAPS.get(ra.kingdom_key(None, curve_culture(troop)) or '')


def off_line_kit(records, troops, items):
    """Two observation lists for the roster pass (not findings): `imports`, a worn item whose
    line cap differs from the wearer culture's own cap (an Umbar noble in Black Numenorean plate,
    a Dol Guldur archer in Rhun's helmet), and `uncurved`, a worn item with no cap at all (vanilla,
    or a folder off the curve) whose primary stat sits above the culture's elite value for that
    slot, which no restat can move. Battle sets of the ladder's troops only (no creature, exempt or
    bare-chested troop); one row per troop, slot and item."""
    imports, uncurved, seen = [], [], set()
    for r in _ladder_records(records):
        troop = troops.get(r['id'])
        if not troop:
            continue
        culture_cap = _culture_cap(troop)
        if not culture_cap:
            continue
        for st in troop['sets']:
            for slot in ARMOUR_SLOTS:
                iid = st.get(slot)
                if not iid or (r['id'], slot, iid) in seen or iid not in items:
                    continue
                seen.add((r['id'], slot, iid))
                it = items[iid]
                line = ra.kingdom_key(iid, it.get('folder')) if it.get('folder') else None
                line_cap = ra.KINGDOM_CAPS.get(line) if line else None
                base = {'troop': r['id'], 'culture': r['culture'], 'tier': r['tier'], 'slot': slot, 'item': iid}
                if line_cap and line_cap != culture_cap:
                    imports.append(dict(base, culture_cap=culture_cap, line=line, line_cap=line_cap))
                elif not line_cap:
                    cslot = SLOT_TO_CURVE[slot]
                    stat = ra.GOVERNED_STATS[cslot][0]
                    primary = it.get('stats', {}).get(stat, 0)
                    ceiling = ra.cap_value(culture_cap, cslot, 'elite')
                    if primary > ceiling:
                        uncurved.append(dict(base, primary=primary, ceiling=ceiling))

    def key(x):
        return (x['culture'], -x['tier'], x['troop'], ARMOUR_SLOTS.index(x['slot']))
    return {'imports': sorted(imports, key=key), 'uncurved': sorted(uncurved, key=key)}


def curve_view(records):
    """{culture: {tier: n, predicted median, actual median, deltas, mean filled share}}.
    The prediction is a generic benchmark: the culture's DEFAULT line at the troop's band
    (a Black Numenorean in troops_mordor is held against the orc cap), every slot filled,
    secondaries at the legacy proportion. It is not a per-item target."""
    groups = defaultdict(list)
    for r in _matrix_records(records):
        groups[(r['culture'], r['tier'])].append(r)
    out = defaultdict(dict)
    for (culture, tier), recs in groups.items():
        out[culture][tier] = {
            'n': len(recs),
            'predicted_total': float(statistics.median(r['predicted_total'] for r in recs)),
            'actual_median': float(statistics.median(r['total'] for r in recs)),
            'delta_median': float(statistics.median(r['delta'] for r in recs)),
            'region_delta_median': {
                reg: float(statistics.median(r['regions'][reg] - r['predicted'][reg] for r in recs))
                for reg in REGIONS},
            'filled_mean': sum(sum(r['filled'].values()) / len(ARMOUR_SLOTS) for r in recs) / len(recs),
        }
    return dict(out)


def gate_cells(records):
    """The cells the validator judges: no excluded, bodyless or ladder-exempt troops, each
    total scaled item by item to the reference cap exactly as the validator scales it."""
    out = defaultdict(list)
    for r in records:
        if r['excluded'] or 'bodyless' in r['tags'] or 'ladder_exempt' in r['tags']:
            continue
        out[(r['culture'], r['tier'])].append(r['scaled_total'])
    return dict(out)


def gate_preview(records):
    v = ts.Validator
    return ts.cross_culture_armour_inversions(
        gate_cells(records), v._CROSS_CULTURE_ARMOUR_MARGIN, v._CROSS_CULTURE_ARMOUR_TIER_GAP,
        v._CROSS_CULTURE_ARMOUR_MIN_CULTURES)


# =============================================================================
# Context
# =============================================================================

def build_context(troops, items, militia, threshold, min_gap, only_culture, game_modules, moduledata):
    records = build_records(troops, items, militia)
    cultures = sorted({r['culture'] for r in records})
    pairs = cross_culture_inversions(records, items, troops, threshold, min_gap)
    worn_by = worn_by_index(troops)
    detail_cultures = [only_culture] if only_culture else cultures
    return {
        'generated_at': datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC'),
        'game_modules': game_modules, 'moduledata': moduledata,
        'n_items': len(items), 'n_troops': len(troops),
        'threshold': threshold, 'min_gap': min_gap, 'only_culture': only_culture,
        'records': records, 'cultures': cultures, 'detail_cultures': detail_cultures,
        'matrices': matrices(records),
        'pairs': pairs, 'aggregated': aggregate_inversions(pairs),
        'gate': gate_preview(records),
        'ceilings': {c: ceilings(c, records, troops, items, worn_by) for c in detail_cultures},
        'curve': curve_view(records),
        'off_line': off_line_kit(records, troops, items),
    }


# =============================================================================
# Markdown
# =============================================================================

def _fmt(x, nd=0):
    return '·' if x is None else f'{x:.{nd}f}'


def _tiers_present(table):
    return [t for t in ENGINE_TIERS if any(t in row for row in table.values())]


def render_matrix(table, cultures, title, unit):
    tiers = _tiers_present(table)
    out = [f'\n#### {title}\n', f'_Cell = median (min-max) n, {unit}. Engine tier = clamp(ceil((level - 5) / 5), 0, 10)._\n',
           '| Culture | ' + ' | '.join(f'T{t}' for t in tiers) + ' |',
           '|' + '---|' * (len(tiers) + 1)]
    for c in cultures:
        row = table.get(c, {})
        cellstr = []
        for t in tiers:
            s = row.get(t)
            cellstr.append(f'{s["median"]:.0f} ({s["min"]:.0f}-{s["max"]:.0f}) {s["n"]}' if s else '·')
        out.append(f'| {c} | ' + ' | '.join(cellstr) + ' |')
    return '\n'.join(out) + '\n'


def _pair_line(p):
    return (f'`{p["strong"]}` ({p["strong_culture"]} L{p["strong_level"]} T{p["strong_tier"]}, '
            f'{p["strong_total"]:.0f}) > `{p["weak"]}` ({p["weak_culture"]} L{p["weak_level"]} '
            f'T{p["weak_tier"]}, {p["weak_total"]:.0f}), +{p["diff"]:.0f}')


def render_report(ctx):
    records, cultures = ctx['records'], ctx['cultures']
    included = [r for r in records if not r['excluded']]
    tagged = lambda tag: [r for r in records if tag in r['tags']]  # noqa: E731
    out = ['# TAOM Kingdom Armour Overview\n',
           f'_Generated {ctx["generated_at"]} by tools/analyze_kingdom_armour.py (READ-ONLY; nothing was written to game data)._\n',
           f'Inputs: `{ctx["game_modules"]}` ({ctx["n_items"]:,} armour items), `{ctx["moduledata"]}` '
           f'({ctx["n_troops"]:,} troops read, {len(records)} in troop files).\n']

    out.append('## Executive summary\n')
    out.append(f'- Cultures: {len(cultures)}. Troops analysed: {len(included)} '
               f'(excluded: {len(tagged("creature"))} creature, {len(tagged("bespoke_rider"))} bespoke rider, '
               f'{len(tagged("no_sets"))} with no battle set, {len(tagged("no_level"))} with no level). '
               f'Ladder-exempt, kept out of the matrices and pairs: {len(tagged("ladder_exempt"))}. '
               f'Judged without Body/Cape: {len(tagged("bodyless"))}. '
               f'Flagged, not excluded: {len(tagged("militia"))} militia, {len(tagged("standalone"))} standalone.')
    agg = ctx['aggregated']
    out.append(f'- Cross-culture inversions at threshold {ctx["threshold"]} / tier gap {ctx["min_gap"]}: '
               f'{len(ctx["pairs"])} troop pairs over {len(agg)} culture pairs.')
    out.append(f'- Validator gate `CROSS_CULTURE_ARMOUR_INVERSION` would warn on {len(ctx["gate"])} culture-tier cell(s): '
               + (', '.join(f'{h["culture"]}/tier{h["tier"]}' for h in ctx['gate']) or 'none') + '.')
    if ctx['pairs']:
        out.append('- Worst pairs:')
        for p in ctx['pairs'][:5]:
            out.append(f'  - {_pair_line(p)}')
    out.append('')

    out.append('## How armour is measured\n')
    out.append('A troop\'s armour is the four engine regions (head, body, arm, leg), each summed over the five armour '
               'slots (Head, Body, Cape, Gloves, Leg) exactly as `Equipment.GetHeadArmorSum` / `GetHumanBodyArmorSum` / '
               '`GetArmArmorSum` / `GetLegArmorSum` do, so a chest contributes arm armour and a cape contributes body '
               'armour. Each region is the mean over the troop\'s battle equipment sets, an unfilled slot counting 0 '
               'because the engine draws each slot from an independently chosen set; civilian sets are excluded. Total '
               '= head + body + arm + leg. Engine tier is `clamp(ceil((level - 5) / 5), 0, 10)`, the tier the party '
               'screen shows. The item curve (`tools/rebalance_armor.py`) has ONE row, "elite", for levels 31 to 51, '
               'so tiers 6 to 10 share a target; the curve view below shows what that does per kingdom. The '
               'validator\'s `_ARMOUR_LADDER_EXEMPT` troops (kit off the ladder by design) are left out of the '
               'matrices, the pair list and the gate alike, and appear only in the per-culture troop tables, tagged '
               '`ladder_exempt`. An unworn item\'s tier in the ceiling tables is judged on the curve of the Armory '
               'folder it lives in, not on the wearer\'s culture.\n')

    out.append('## Culture x engine-tier matrices\n')
    out.append(render_matrix(ctx['matrices']['total'], cultures, 'Total armour (head + body + arm + leg)', 'total'))
    for reg in REGIONS:
        out.append(render_matrix(ctx['matrices'][reg], cultures, f'{REGION_LABELS[reg].capitalize()} armour',
                                 REGION_LABELS[reg]))

    out.append(f'\n## Cross-culture inversions (threshold {ctx["threshold"]}, minimum tier gap {ctx["min_gap"]})\n')
    out.append(f'A pair is listed when a troop of another culture at least the gap in tiers LOWER totals more than the '
               f'threshold above the higher-tier troop, every item scaled to the {ts.REFERENCE_CAP} chest cap of its own line first '
               f'(`rebalance_armor.KINGDOM_CAPS`: kingdoms differ in armour power by design, so a pair is a roster '
               f'finding, not a cap finding). Bare-chested pairs are compared on Head, Gloves and Leg only.\n')
    if agg:
        out.append('| Stronger culture | Weaker culture | Pairs | Weaker troops hit | Tiers (strong->weak) | Worst pair |')
        out.append('|---|---|---|---|---|---|')
        for (sc, wc), a in sorted(agg.items(), key=lambda kv: (-kv[1]['count'], kv[0])):
            if ctx['only_culture'] and ctx['only_culture'] not in (sc, wc):
                continue
            tiers = ', '.join(f'{s}->{w}' for s, w in sorted(a['tiers']))
            out.append(f'| {sc} | {wc} | {a["count"]} | {len(a["weak_ids"])} | {tiers} | {_pair_line(a["worst"])} |')
        shown = [p for p in ctx['pairs']
                 if not ctx['only_culture'] or ctx['only_culture'] in (p['strong_culture'], p['weak_culture'])]
        out.append(f'\n### Worst {min(WORST_PAIRS_MD, len(shown))} pairs\n')
        out.append('| Stronger troop | Culture | L/T | Total | Weaker troop | Culture | L/T | Total | Diff |')
        out.append('|---|---|---|---|---|---|---|---|---|')
        for p in shown[:WORST_PAIRS_MD]:
            out.append(f'| `{p["strong"]}` | {p["strong_culture"]} | L{p["strong_level"]}/T{p["strong_tier"]} | '
                       f'{p["strong_total"]:.0f} | `{p["weak"]}` | {p["weak_culture"]} | '
                       f'L{p["weak_level"]}/T{p["weak_tier"]} | {p["weak_total"]:.0f} | +{p["diff"]:.0f} |')
    else:
        out.append('None at this threshold.')
    out.append('')

    v = ts.Validator
    out.append('## Validator gate preview (`CROSS_CULTURE_ARMOUR_INVERSION`)\n')
    out.append(f'Computed by `taom_schema.cross_culture_armour_inversions` with the validator\'s constants: margin '
               f'{v._CROSS_CULTURE_ARMOUR_MARGIN}, tier gap {v._CROSS_CULTURE_ARMOUR_TIER_GAP}, at least '
               f'{v._CROSS_CULTURE_ARMOUR_MIN_CULTURES} other cultures with a cell at that tier. A cell is a culture\'s '
               f'troops at one engine tier (median total); it warns when the median sits more than the margin under the '
               f'median of the OTHER cultures\' medians at tier minus gap, every item scaled to the {ts.REFERENCE_CAP} '
               f'chest cap of its own line first. Excluded here as in the validator: the '
               f'bare-chested-by-design troops and `_ARMOUR_LADDER_EXEMPT` ('
               + ', '.join(f'`{k}`' for k in sorted(v._ARMOUR_LADDER_EXEMPT)) + ').\n')
    if ctx['gate']:
        out.append('| Cell | Median | n | Field tier | Field median | Shortfall | Field (culture: median) |')
        out.append('|---|---|---|---|---|---|---|')
        for h in ctx['gate']:
            field = ', '.join(f'{c} {m:.0f}' for c, m in h['field'])
            out.append(f'| {h["culture"]}/tier{h["tier"]} | {h["median"]:.0f} | {h["n"]} | {h["field_tier"]} | '
                       f'{h["field_median"]:.0f} | {h["shortfall"]:.0f} | {field} |')
    else:
        out.append('No cell is flagged.')
    out.append('')

    out.append('## Per-culture detail\n')
    for c in ctx['detail_cultures']:
        out.append(render_culture(c, ctx))

    out.append('## Data quality\n')
    aliases = ', '.join(f'{k} -> {v}' for k, v in sorted(CURVE_CULTURE_ALIASES.items()))
    out.append(f'- Curve-culture aliases (file culture -> `rebalance_armor.CULTURAL_MODS` key, for the curve view '
               f'only): {aliases}; `iron_hills_*` ids -> iron_hills. These follow the folder each culture wears; '
               f'the skill curve (`rebalance_troops.detect_culture`) keeps goblin as its own culture.')
    out.append('- Items worn only by an excluded troop (the troll plate on `cave_troll`) count as worn, so they '
               'are not in any reserve list, and their folder counts as worn by nobody, so they are not in any '
               'ceiling table.')
    for tag, label in (('creature', 'Creature troops (excluded)'), ('bespoke_rider', 'Bespoke mount riders (excluded)'),
                       ('no_sets', 'Troops with no battle set (excluded)'), ('no_level', 'Troops with no level= (excluded)'),
                       ('bodyless', 'Bare-chested by design (judged without Body/Cape, left out of the matrices)'),
                       ('ladder_exempt', 'Exempt from the ladder: out of the matrices, the pair list and the gate'),
                       ('mount_rider', 'Mount riders (tagged only)'),
                       ('militia', 'Militia (tagged only)'), ('standalone', 'Standalone: no upgrade edge in or out (tagged only)')):
        ids = [r['id'] for r in tagged(tag)]
        if ids:
            out.append(f'- {label}: {len(ids)}: ' + ', '.join(f'`{i}`' for i in ids))
    out.append('')
    out.append(render_off_line(ctx['off_line'], ctx['only_culture']))
    out.append('_Nothing was applied. Fixing an inversion is a roster, item or curve decision taken with this report '
               'in hand; see docs/features/armor-balance.md "Kingdom armour ladder"._\n')
    return '\n'.join(out)


def render_off_line(obs, only_culture=None):
    """The two observation tables (kit from another line; uncurved kit above the ceiling)."""
    imports = [r for r in obs['imports'] if not only_culture or r['culture'] == only_culture]
    uncurved = [r for r in obs['uncurved'] if not only_culture or r['culture'] == only_culture]
    out = ["## Kit off the culture's line (observations for the roster pass, not findings)\n",
           f"- Worn items whose line cap differs from the wearer culture's cap: {len(imports)} "
           f'(troop, slot, item) rows over {len({r["troop"] for r in imports})} troop(s). The gate already '
           f'scales each item to its own line, so these are not inversions; they are the kit a roster pass '
           f'would look at first.']
    if imports:
        out += ['', '| Culture (cap) | Troop | T | Slot | Item | Line (cap) |', '|---|---|---|---|---|---|']
        out += [f'| {r["culture"]} ({r["culture_cap"]}) | `{r["troop"]}` | {r["tier"]} | {r["slot"]} | `{r["item"]}` '
                f'| {r["line"]} ({r["line_cap"]}) |' for r in imports]
    out += ['', f"- Worn items with no cap (vanilla, or a folder off the curve) above the culture's elite value for "
                f'the slot: {len(uncurved)}. No restat reaches them; the fix is a roster swap.']
    if uncurved:
        out += ['', '| Culture | Troop | T | Slot | Item | Primary | Elite value |', '|---|---|---|---|---|---|---|']
        out += [f'| {r["culture"]} | `{r["troop"]}` | {r["tier"]} | {r["slot"]} | `{r["item"]}` | {r["primary"]} '
                f'| {r["ceiling"]} |' for r in uncurved]
    return '\n'.join(out) + '\n'



def render_culture(culture, ctx):
    recs = [r for r in ctx['records'] if r['culture'] == culture]
    out = [f'\n### {culture}  ({len(recs)} troops)\n']
    ceil = ctx['ceilings'].get(culture)
    if ceil:
        folders = ', '.join(f'{k or "vanilla/repo"} {v:.0%}' for k, v in
                            sorted(ceil['folders'].items(), key=lambda kv: -kv[1]))
        out.append(f'**Armory ceiling** (folders worn: {folders})\n')
        out.append('| Slot | Stat | Max available (troop-eligible) | incl. hero-only | Max worn here |')
        out.append('|---|---|---|---|---|')
        for slot, s in ceil['slots'].items():
            avail = f'{_fmt(s["avail_max"])} `{s["avail_max_item"]}`' if s['avail_max_item'] else '·'
            worn = f'{_fmt(s["worn_max"])} `{s["worn_max_item"]}`' if s['worn_max_item'] else '·'
            out.append(f'| {slot} | {s["stat"]} | {avail} | {_fmt(s["avail_max_incl_hero"])} | {worn} |')
        out.append('')
        if ceil['unworn_elite']:
            out.append(f'Unworn elite/lord-row items in these folders ({len(ceil["unworn_elite"])}, the re-slot reserve):')
            for u in ceil['unworn_elite']:
                out.append(f'- {u["slot"]}: `{u["id"]}` ({u["name"]}) {u["primary"]} [{u["tier"]}, {u["folder"]}]')
        else:
            out.append('No unworn elite/lord-row item in these folders.')
        out.append('')
    cv = ctx['curve'].get(culture, {})
    if cv:
        out.append('**Curve view** (a generic benchmark, not a per-item target: the culture\'s default line at the '
                   'troop\'s band with every slot filled and secondaries at the legacy proportion, vs actual medians; '
                   'a troop in another line\'s kit is held against the default line)\n')
        out.append('| Tier | n | Predicted total | Actual median | Delta | d head | d body | d arm | d leg | Filled slots |')
        out.append('|---|---|---|---|---|---|---|---|---|---|')
        for t in sorted(cv):
            s = cv[t]
            rd = s['region_delta_median']
            out.append(f'| T{t} | {s["n"]} | {s["predicted_total"]:.0f} | {s["actual_median"]:.0f} | '
                       f'{s["delta_median"]:+.0f} | {rd["head_armor"]:+.0f} | {rd["body_armor"]:+.0f} | '
                       f'{rd["arm_armor"]:+.0f} | {rd["leg_armor"]:+.0f} | {s["filled_mean"]:.0%} |')
        out.append('')
    out.append('| Troop | L | T | Tags | Head | Body | Arm | Leg | Total | Predicted | Delta | Sets |')
    out.append('|---|---|---|---|---|---|---|---|---|---|---|---|')
    for r in sorted(recs, key=lambda r: (r['tier'], -r['total'], r['id'])):
        tags = ', '.join(r['tags']) or ''
        rg = r['regions']
        out.append(f'| `{r["id"]}` | {r["level"]} | {r["tier"]} | {tags} | {rg["head_armor"]:.0f} | '
                   f'{rg["body_armor"]:.0f} | {rg["arm_armor"]:.0f} | {rg["leg_armor"]:.0f} | {r["total"]:.0f} | '
                   f'{r["predicted_total"]:.0f} | {r["delta"]:+.0f} | {r["n_sets"]} |')
    out.append('')
    return '\n'.join(out)


# =============================================================================
# JSON
# =============================================================================

def build_json(ctx):
    def pair(p):
        return {k: (list(v) if isinstance(v, tuple) else v) for k, v in p.items()}
    return {
        'generatedAt': ctx['generated_at'], 'gameModules': ctx['game_modules'],
        'moduledata': ctx['moduledata'], 'threshold': ctx['threshold'], 'minTierGap': ctx['min_gap'],
        'troops': [dict(r, tags=list(r['tags'])) for r in ctx['records']],
        'matrices': {k: {c: {str(t): s for t, s in row.items()} for c, row in table.items()}
                     for k, table in ctx['matrices'].items()},
        'inversions': {
            'aggregated': [{'strongCulture': sc, 'weakCulture': wc, 'count': a['count'],
                            'weakTroops': sorted(a['weak_ids']),
                            'tiers': sorted(list(t) for t in a['tiers']), 'worst': pair(a['worst'])}
                           for (sc, wc), a in sorted(ctx['aggregated'].items(),
                                                     key=lambda kv: (-kv[1]['count'], kv[0]))],
            'worst': [pair(p) for p in ctx['pairs'][:WORST_PAIRS_JSON]],
        },
        'gatePreview': ctx['gate'],
        'ceilings': {c: {'folders': {k or 'vanilla/repo': v for k, v in ce['folders'].items()},
                         'slots': ce['slots'], 'unworn': ce['unworn_elite']}
                     for c, ce in ctx['ceilings'].items()},
        'curve': {c: {str(t): s for t, s in row.items()} for c, row in ctx['curve'].items()},
        'offLine': ctx['off_line'],
        'exclusions': {tag: [r['id'] for r in ctx['records'] if tag in r['tags']]
                       for tag in ('creature', 'bespoke_rider', 'no_sets', 'no_level', 'bodyless', 'ladder_exempt',
                                   'militia', 'standalone', 'mount_rider')},
    }


# =============================================================================
# HTML (the matrices and the two tables people compare by eye)
# =============================================================================

def _html_matrix(table, curve, cultures, title):
    tiers = _tiers_present(table)
    out = [f'<h3>{_html.escape(title)}</h3>', '<table><thead><tr><th>Culture</th>'
           + ''.join(f'<th>T{t}</th>' for t in tiers) + '</tr></thead><tbody>']
    for c in cultures:
        row = table.get(c, {})
        tds = []
        for t in tiers:
            s = row.get(t)
            if not s:
                tds.append(atb._cell('·'))
                continue
            pred = curve.get(c, {}).get(t, {}).get('predicted_total')
            ratio = (s['median'] / pred) if pred else None
            tds.append(atb._cell(f'{s["median"]:.0f} ({s["min"]:.0f}-{s["max"]:.0f}) n{s["n"]}', atb._heat_bg(ratio)))
        out.append(f'<tr><th>{_html.escape(c)}</th>' + ''.join(tds) + '</tr>')
    out.append('</tbody></table>')
    return '\n'.join(out)


def render_html(ctx):
    v = ts.Validator
    parts = ['<!DOCTYPE html><html><head><meta charset="utf-8"><title>TAOM Kingdom Armour</title>',
             f'<style>{atb.HTML_CSS}</style></head><body>',
             '<h1>TAOM Kingdom Armour Overview</h1>',
             f'<p class="meta">Generated {_html.escape(ctx["generated_at"])}; read-only. '
             f'Threshold {ctx["threshold"]}, tier gap {ctx["min_gap"]}. Heat = median / curve prediction.</p>',
             '<p class="legend">'
             '<span style="background:#922b21">&lt;55%</span><span style="background:#c0392b">55-70%</span>'
             '<span style="background:#b9770e">70-85%</span><span style="background:#1e7a45">85-112%</span>'
             '<span style="background:#1f5d8c">112-145%</span><span style="background:#5e3a8c">&gt;145%</span></p>']
    parts.append(_html_matrix(ctx['matrices']['total'], ctx['curve'], ctx['cultures'], 'Total armour, median (min-max) n'))
    for reg in REGIONS:
        parts.append(_html_matrix(ctx['matrices'][reg], {}, ctx['cultures'], f'{REGION_LABELS[reg].capitalize()} armour'))
    parts.append('<h2>Cross-culture inversions</h2><table><thead><tr><th>Stronger</th><th>Weaker</th><th>Pairs</th>'
                 '<th>Worst pair</th></tr></thead><tbody>')
    for (sc, wc), a in sorted(ctx['aggregated'].items(), key=lambda kv: (-kv[1]['count'], kv[0])):
        parts.append(f'<tr><td>{_html.escape(sc)}</td><td>{_html.escape(wc)}</td><td>{a["count"]}</td>'
                     f'<td>{_html.escape(_pair_line(a["worst"]).replace("`", ""))}</td></tr>')
    parts.append('</tbody></table>')
    parts.append(f'<h2>Gate preview: CROSS_CULTURE_ARMOUR_INVERSION (margin {v._CROSS_CULTURE_ARMOUR_MARGIN}, '
                 f'gap {v._CROSS_CULTURE_ARMOUR_TIER_GAP})</h2><table><thead><tr><th>Cell</th><th>Median</th><th>n</th>'
                 '<th>Field tier</th><th>Field median</th><th>Shortfall</th></tr></thead><tbody>')
    for h in ctx['gate']:
        parts.append(f'<tr><td>{_html.escape(h["culture"])}/tier{h["tier"]}</td><td>{h["median"]:.0f}</td><td>{h["n"]}</td>'
                     f'<td>{h["field_tier"]}</td><td>{h["field_median"]:.0f}</td><td>{h["shortfall"]:.0f}</td></tr>')
    parts.append('</tbody></table>')
    for c in ctx['detail_cultures']:
        ce = ctx['ceilings'][c]
        parts.append(f'<h2>{_html.escape(c)}</h2><h3>Armory ceiling</h3><table><thead><tr><th>Slot</th>'
                     '<th>Max available</th><th>incl. hero-only</th><th>Max worn</th></tr></thead><tbody>')
        for slot, s in ce['slots'].items():
            parts.append(f'<tr><td>{slot}</td><td>{_fmt(s["avail_max"])} {_html.escape(s["avail_max_item"] or "")}</td>'
                         f'<td>{_fmt(s["avail_max_incl_hero"])}</td>'
                         f'<td>{_fmt(s["worn_max"])} {_html.escape(s["worn_max_item"] or "")}</td></tr>')
        parts.append('</tbody></table>')
        cv = ctx['curve'].get(c, {})
        if cv:
            parts.append('<h3>Curve view</h3><table><thead><tr><th>Tier</th><th>n</th><th>Predicted</th><th>Actual median</th>'
                         '<th>Delta</th></tr></thead><tbody>')
            for t in sorted(cv):
                s = cv[t]
                delta = atb._cell('%+.0f' % s['delta_median'], atb._delta_bg(s['delta_median']))
                parts.append(f'<tr><td>T{t}</td><td>{s["n"]}</td><td>{s["predicted_total"]:.0f}</td>'
                             f'<td>{s["actual_median"]:.0f}</td>{delta}</tr>')
            parts.append('</tbody></table>')
    parts.append('</body></html>')
    return '\n'.join(parts)


# =============================================================================
# Main
# =============================================================================

def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--game-modules', default=rb.DEFAULT_GAME_MODULES,
                    help='.../Mount & Blade II Bannerlord/Modules (item armour comes from the install)')
    ap.add_argument('--moduledata', default=fx.MODULEDATA_DIR,
                    help='TAOM ModuleData root holding troops/ and characters/')
    ap.add_argument('--culture', default=None, help='narrow the detail sections and the pair list to one file culture')
    ap.add_argument('--threshold', type=float, default=DEFAULT_THRESHOLD,
                    help='minimum total-armour lead for a cross-culture inversion (default %(default)s)')
    ap.add_argument('--min-tier-gap', type=int, default=DEFAULT_MIN_TIER_GAP,
                    help='minimum engine-tier gap between the two troops of a pair (default %(default)s)')
    ap.add_argument('--report-dir', default=REPORT_DIR, help='where the three reports go')
    ap.add_argument('--stdout', action='store_true', help='print the executive summary after writing')
    args = ap.parse_args(argv)

    if not os.path.isdir(args.game_modules):
        print('ERROR: Bannerlord Modules folder not found: %s\n'
              '       Item armour values come from the install; pass --game-modules.'
              % args.game_modules)
        return 2

    items = fx.load_item_armour(args.game_modules, args.moduledata)
    troops = fx.load_troops(args.moduledata)
    militia = rb.militia_troop_ids(args.moduledata)
    cultures = sorted({file_culture(t) for t in troops.values() if not t['external']})
    if args.culture and args.culture not in cultures:
        print('ERROR: unknown culture %r. Known: %s' % (args.culture, ', '.join(cultures)))
        return 2

    ctx = build_context(troops, items, militia, args.threshold, args.min_tier_gap, args.culture,
                        args.game_modules, args.moduledata)
    os.makedirs(args.report_dir, exist_ok=True)
    md = render_report(ctx)
    with open(os.path.join(args.report_dir, REPORT_MD), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(md)
    with open(os.path.join(args.report_dir, REPORT_HTML), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(render_html(ctx))
    with open(os.path.join(args.report_dir, REPORT_JSON), 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(build_json(ctx), fh, indent=2)

    print('Item armour index: %s items. Troops: %s (%d in troop files). Reports: %s'
          % (format(len(items), ','), format(len(troops), ','), len(ctx['records']), args.report_dir))
    if args.stdout:
        print(md.split('## How armour is measured')[0])
    return 0


if __name__ == '__main__':
    sys.exit(main())
