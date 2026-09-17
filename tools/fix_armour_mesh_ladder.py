#!/usr/bin/env python3
"""Take low-level troops out of armour meshes their level may not wear (#609).

An Armory item id names the tier the artist modelled (_light_, _med_, _heavy_, _elite_, _lord_).
Nothing tied that to a troop level until rebalance_armor.MESH_TIER_LADDER (the maintainer's
table, 2026-09-16): lord kit is for level 41+ troops or lords. The 2026-05 roster fan-out reached
for every chest in the Gundabad folder, so the level-11 snaga and hunter wear all six `_lord_`
chests, and because the kingdom-cap curve prices an item by its LOWEST wearer, the whole lord
line restatted to the light band: 20 body armour under a 25 kg plate mesh, while the medium
line, worn from level 16, sits at 31. The stats followed the roster; the roster is the defect.

WHAT IT DOES
------------
For every battle set of every troop in troops/troops_*.xml (civilian sets, unlevelled troops and
the validator's _ARMOUR_LADDER_EXEMPT / _BODYLESS_BY_DESIGN troops skipped), each armour slot
whose item carries a tier token is held to the ladder:

  OVER-dressed  (tier above the highest the level allows) is swapped to the same LINE at the
                substitute tier: the highest allowed tier not above the troop's stat band
                (rebalance_armor.substitute_mesh_tiers), so a level-11 snaga goes to `_light_`,
                not `_med_`; put in `_med_a` he would anchor the medium line to the light band
                and recreate the bug one notch down. The line is the id up to the tier token
                plus the defining slot file (a helmet is never offered as a chest). Same variant
                suffix preferred (`lord_c` -> `light_c`), else the nearest suffix, so fan-out
                variety survives. The same old item becomes the same new item in every set of
                the troop, so its sets stay interchangeable slot by slot (.claude/rules/troops.md).
                A line with nothing at any substitute tier is REPORTED and left alone: that is
                a hand decision (76 pairs on 2026-09-16).
  UNDER-dressed (tier below the lowest allowed) is REPORTED, never written: it is cosmetic, the
                troop-level gates already price it, and 1,285 pairs pre-exist.

Dry-run by default. --apply writes byte-faithfully through fix_upgrade_armour_regressions
.write_changes (BOM and newline style preserved, ElementTree parse before any write), re-reads
the rosters from disk and exits 1 if an over-dressed pair with a substitute remains. Idempotent.
The rosters it writes are git-tracked, so git is the backup (the Armory restat that follows
takes its own `.bak-<tag>`). Two inherited limits of that writer: it is not comment-aware, so a
whole `<EquipmentRoster>` block inside `<!-- -->` would be rewritten too (none exists today, and
the engine drops comments anyway); and a troop's culture here is its FILE's (`troops_goblin.xml`
holds three cultures), which is what `--cultures` filters on.
Afterwards re-derive and restat, because the anchors moved:

    python tools/derive_armor_tiers.py
    python tools/rebalance_armor.py --dry-run --tier-source roster-first --keep-weights \\
           --keep-material-type --cultures <culture>

Usage:
    python tools/fix_armour_mesh_ladder.py                    # report only
    python tools/fix_armour_mesh_ladder.py --cultures gundabad,dunland
    python tools/fix_armour_mesh_ladder.py --report-under     # list the under-dressed rows too
    python tools/fix_armour_mesh_ladder.py --apply
"""

import argparse
import os
import re
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fix_upgrade_armour_regressions as fu  # noqa: E402
import rebalance_armor as ra  # noqa: E402
import rebalance_troops as rb  # noqa: E402

try:
    import taom_schema as _ts
    EXEMPT_TROOPS = frozenset(_ts.Validator._ARMOUR_LADDER_EXEMPT) | frozenset(_ts.Validator._BODYLESS_BY_DESIGN)
except Exception:  # the schema module needs its JSON schemas; a bare checkout still gets the ids
    EXEMPT_TROOPS = frozenset({'cave_troll', 'harad_elephant_rider', 'harad_mumakil_rider',
                               'gondor_ithilien_ranger', 'dg_goblin_slave', 'urukhai_champion',
                               'urukhai_berserker'})

# The token that spells each tier inside an id, bounded by underscores or the end of the id.
_TOKEN_PATTERNS = {'lord': 'lord', 'elite': 'elite', 'heavy': 'heavy', 'medium': 'medium|med', 'light': 'light'}


def split_id(item_id):
    """(stem, tier, suffix) for a tokened armour id, or None. The tier is the one
    rebalance_armor.mesh_tier_of reads (so the two never disagree); the stem is everything before
    that tier's token and the suffix everything after it, underscores left in place:
    'sk_gb_uruk_chest_lord_c' -> ('sk_gb_uruk_chest', 'lord', '_c'). A digit may follow the
    token (`rivendell_torso_lord3_silver`, `thenn_armor_med1`), so the split reads every id
    mesh_tier_of tiers. A `_civ` id, an untokened id, or a token followed by a letter
    (`_lordly_`) returns None."""
    tier = ra.mesh_tier_of(item_id)
    if tier in (None, 'civilian'):
        return None
    m = re.search(r'_(?:%s)(?=[_0-9]|$)' % _TOKEN_PATTERNS[tier], item_id.lower())
    if not m:
        return None
    return item_id[:m.start()], tier, item_id[m.end():]


def build_line_index(items):
    """{(stem, slot file, tier): [item ids]} over every tokened armour item, so a substitute is
    always the same line in the same slot."""
    index = defaultdict(list)
    for iid, rec in items.items():
        parts = split_id(iid)
        if parts is None:
            continue
        stem, tier, _ = parts
        index[(stem, rec.get('file'), tier)].append(iid)
    for ids in index.values():
        ids.sort()
    return index


def _suffix_distance(a, b):
    """How far two variant suffixes are: 0 for equal, else by the last letter, so `_c` prefers
    `_b`/`_d` over `_a`, and a structurally different suffix (`_cape_a` vs `_a`) sorts last."""
    if a == b:
        return (0, 0)
    la = re.sub(r'[^a-z]', '', a)[-1:] or ' '
    lb = re.sub(r'[^a-z]', '', b)[-1:] or ' '
    shape_a = re.sub(r'[a-z0-9]', '', a)
    shape_b = re.sub(r'[a-z0-9]', '', b)
    return (1 if shape_a == shape_b else 2, abs(ord(la) - ord(lb)))


def line_anchors(troops):
    """{item id: lowest level of an in-scope troop wearing it in a battle set}, the anchor the
    kingdom-cap curve prices the item by (derive_armor_tiers does the same over the same sets)."""
    anchors = {}
    for rec in troops.values():
        level = rec.get('level')
        if level is None:
            continue
        for st in rec.get('sets') or ():
            for slot in ra.MESH_LADDER_SLOTS:
                iid = st.get(slot)
                if iid and (iid not in anchors or int(level) < anchors[iid]):
                    anchors[iid] = int(level)
    return anchors


def _band_index(level):
    return ra.MESH_TIER_ORDER.index(ra.level_to_band(level))


def _anchor_rank(cand, level, anchors):
    """How a candidate variant's current stat band sits against the troop's: (distance in
    bands, 0 below / 1 above). Two `_med_` chests can be two bands apart, because the curve
    prices each variant by its LOWEST wearer: med_a worn by a level-11 recruit is light-band
    kit, med_c worn from level 21 is heavy-band kit. Distance 0 (same band, or unworn, which
    takes the troop's band) moves nothing. Below: this troop wears less than its band and
    nobody else moves. Above: this troop becomes the new anchor and every higher wearer loses
    a band, so it ranks last at equal distance."""
    anchor = anchors.get(cand)
    if anchor is None:
        return (0, 0)
    diff = _band_index(anchor) - _band_index(level)
    return (abs(diff), 1 if diff > 0 else 0)


def pick_substitute(item_id, level, items, index, anchors=None):
    """The same line's item at the best substitute tier for the level, or None when the line
    has nothing at any of them. Among a tier's variants the one whose current anchor band is
    nearest the troop's band wins (see _anchor_rank), then the nearest variant suffix, then
    the id. Returns (new id, tier)."""
    parts = split_id(item_id)
    if parts is None or item_id not in items:
        return None
    stem, _, suffix = parts
    slot_file = items[item_id].get('file')
    anchors = anchors or {}
    for tier in ra.substitute_mesh_tiers(level):
        cands = index.get((stem, slot_file, tier))
        if not cands:
            continue
        best = min(cands, key=lambda c: (_anchor_rank(c, level, anchors),
                                          _suffix_distance(suffix, split_id(c)[2]), c))
        return best, tier
    return None


def in_scope(troops, cultures=None):
    """The troops the ladder governs: troops/troops_<culture>.xml entries with a level, minus the
    exempt set; `cultures` narrows to those file cultures."""
    out = {}
    for tid, rec in troops.items():
        if rec.get('external') or not rec.get('has_level', rec.get('level') is not None):
            continue
        fname = os.path.basename(rec['file'])
        if not (fname.startswith('troops_') and fname.endswith('.xml')):
            continue
        culture = fname[len('troops_'):-len('.xml')]
        if cultures and culture not in cultures:
            continue
        if tid in EXEMPT_TROOPS:
            continue
        out[tid] = dict(rec, culture=culture)
    return out


def plan(troops, items, cultures=None):
    """(changes, unresolved, under): changes are write_changes rows for the over-dressed pairs
    with a substitute; unresolved the over-dressed pairs with none; under the under-dressed rows."""
    scoped = in_scope(troops, cultures)
    index = build_line_index(items)
    # Anchors over EVERY in-scope troop, not the culture filter: a variant is priced by its
    # lowest wearer wherever that wearer's file is.
    anchors = line_anchors(in_scope(troops))
    changes, unresolved = [], []
    hits = [dict(h, culture=scoped[h['troop']]['culture']) for h in ra.mesh_ladder_violations(scoped)]
    under = [h for h in hits if h['direction'] == 'under']
    # Lowest troops place first, and each pick moves the anchor it lands on, so a higher troop
    # placed later sees the variant a lower one just took and prefers one still at its own
    # band. With one pre-run snapshot two troops converging on an unworn variant could not
    # see each other and the higher one was priced a band low (deep review, 2026-09-16).
    for hit in sorted((h for h in hits if h['direction'] == 'over'),
                      key=lambda h: (h['level'], h['troop'], h['slot'], h['item'])):
        pick = pick_substitute(hit['item'], hit['level'], items, index, anchors)
        if pick is None:
            unresolved.append(hit)
            continue
        new, tier = pick
        distance, above = _anchor_rank(new, hit['level'], anchors)
        note = ''
        if distance:
            note = 'anchor L%d, %s band%s %s' % (anchors[new], 'a' if distance == 1 else str(distance),
                                                 '' if distance == 1 else 's', 'above' if above else 'below')
        anchors[new] = min(anchors.get(new, hit['level']), hit['level'])
        changes.append({
            'troop': hit['troop'], 'file': hit['file'], 'culture': hit['culture'],
            'level': hit['level'], 'slot': hit['slot'],
            'old': hit['item'], 'new': new, 'old_tier': hit['tier'], 'new_tier': tier,
            'sets': hit['sets'], 'anchor_note': note,
            'old_value': items.get(hit['item'], {}).get('value', 0),
            'new_value': items.get(new, {}).get('value', 0),
        })
    changes.sort(key=lambda c: (c['troop'], c['slot'], c['old']))
    unresolved.sort(key=lambda h: (h['troop'], h['slot'], h['item']))
    return changes, unresolved, under


def _print_changes(changes):
    per = Counter(c['culture'] for c in changes)
    print('\nOver-dressed pairs with a same-line substitute: %d (%d troops, %d items, %d equipment lines): %s' % (
        len(changes), len({c['troop'] for c in changes}), len({c['old'] for c in changes}),
        sum(c['sets'] for c in changes),
        ', '.join('%s %d' % kv for kv in sorted(per.items(), key=lambda kv: -kv[1])) or 'none'))
    off_band = sum(1 for c in changes if c['anchor_note'])
    if off_band:
        print("  (%d land on a variant priced off the troop's band, marked at the end of the row; "
              'the line had nothing at the band)' % off_band)
    for c in changes:
        print('  %-12s %-38s L%-3d %-6s %-44s -> %-44s %-6s -> %-6s x%d sets  armour %3d -> %3d  %s' % (
            c['culture'], c['troop'], c['level'], c['slot'], c['old'], c['new'],
            c['old_tier'], c['new_tier'], c['sets'], c['old_value'], c['new_value'], c['anchor_note']))


def _print_unresolved(unresolved):
    print('\nOver-dressed pairs with NOTHING in the line at an allowed tier (hand decision): %d' % len(unresolved))
    for h in unresolved:
        print('  %-12s %-38s L%-3d %-6s %-44s %-6s allowed %s' % (
            h['culture'], h['troop'], h['level'], h['slot'], h['item'], h['tier'], '/'.join(h['allowed'])))


def _print_under(under, rows):
    per = Counter(h['culture'] for h in under)
    print('\nUnder-dressed pairs (reported only): %d over %d troops: %s' % (
        len(under), len({h['troop'] for h in under}),
        ', '.join('%s %d' % kv for kv in sorted(per.items(), key=lambda kv: -kv[1])) or 'none'))
    if rows:
        for h in under:
            print('  %-12s %-38s L%-3d %-6s %-44s %-6s allowed %s' % (
                h['culture'], h['troop'], h['level'], h['slot'], h['item'], h['tier'], '/'.join(h['allowed'])))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--apply', action='store_true', help='write the rosters (default: report only)')
    ap.add_argument('--cultures', default='', help='comma-separated troops_<culture> names to restrict to')
    ap.add_argument('--report-under', action='store_true', help='list every under-dressed row, not just counts')
    ap.add_argument('--game-modules', default=rb.DEFAULT_GAME_MODULES,
                    help='.../Mount & Blade II Bannerlord/Modules (the item index comes from the install)')
    args = ap.parse_args(argv)

    if not os.path.isdir(args.game_modules):
        print('ERROR: Bannerlord Modules folder not found: %s\n'
              '       The substitute must be a defined Armory item; pass --game-modules.' % args.game_modules)
        return 2
    cultures = {c.strip() for c in args.cultures.split(',') if c.strip()} or None

    items = fu.load_item_armour(args.game_modules)
    troops = fu.load_troops()
    print('Item index: %s items. Troops: %s. Exempt: %d.' % (
        format(len(items), ','), format(len(troops), ','), len(EXEMPT_TROOPS)))

    changes, unresolved, under = plan(troops, items, cultures)
    _print_changes(changes)
    _print_unresolved(unresolved)
    _print_under(under, args.report_under)

    if not args.apply:
        print('\n(dry run; pass --apply to write)')
        return 0

    written = fu.write_changes(changes)
    print('\nWrote %d file(s).' % written)
    after, _, _ = plan(fu.load_troops(), items, cultures)
    if after:
        print('ERROR: %d over-dressed pair(s) with a substitute remain after the write:' % len(after))
        _print_changes(after)
        return 1
    print('Re-check from disk: no over-dressed pair with a substitute remains. Now re-derive and restat '
          '(see the module docstring).')
    return 0


if __name__ == '__main__':
    sys.exit(main())
