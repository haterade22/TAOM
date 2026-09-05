#!/usr/bin/env python3
"""Raise a troop's armour to at least the armour of the troop it upgrades from.

The skill side of this rule already exists: rebalance_troops.clamp_upgrade_monotonicity walks the
upgrade DAG and sets child[skill] = max(child, parent), and UPGRADE_SKILL_REGRESSION fails the
validator when an edge still reads backwards. Equipment was never held to the same ladder. On
2026-09-04, 62 upgrade edges across 13 cultures LOWERED the troop's armour total on promotion, the
worst by 60 to 74 points: the Rhun ash capstones wear light plate where their parents wear heavy
plate, the Dol Guldur shadow archer wears a hood and a light chest over a plated disciple, and the
uruk skirmisher simply has no gloves or cape. A player reads the troop tree as a ladder, so an
upgrade that makes the troop easier to kill reads as a bug however it got there.

WHAT COUNTS
-----------
A troop's armour in a slot is the average over its BATTLE equipment sets of that slot's item
armour (head + body + arm + leg of the item, all four, because a chest contributes arm armour and a
pauldron contributes body armour). A set that does not fill the slot counts as 0: the engine draws
each slot from an independently chosen set (.claude/rules/troops.md), so an unfilled slot in one
set is a real chance of spawning bare there. The total is the sum over the five armour slots
(Head, Body, Cape, Gloves, Leg). An edge regresses when child total < parent total.

WHAT THE FIX DOES
-----------------
Per regressing edge, per slot where the child's slot average is below the parent's, every battle
set of the child whose value in that slot is below the parent's slot average gets a replacement:

  1. an explicit OVERRIDES entry for (troop, slot), when a hand decision exists;
  2. else the cheapest item in the child's own culture folder and slot file that shares the child's
     item FAMILY (its id with the trailing tier tokens stripped, so sk_rh_drag_plate_light_a and
     sk_rh_drag_plate_elite_c are one family) and meets the parent's value, so the troop keeps its
     visual identity and simply steps up its own ladder;
  3. else the parent's own item, which by construction meets the value.

Before any edge is judged, DEMOTE runs unconditionally over every troop below its listed level:
hero-tier items (the elf lord torsos and circlets a level-21 recruit was wearing) are swapped for
the troop's own strongest ordinary item in that slot, because raising a whole tree to lord kit is
the wrong fix when the parent is the anomaly. A slot the parent fills and the child never fills
gets the chosen item appended to every battle set. The same old item is replaced by the same new item in every set, so sets stay interchangeable
slot by slot. Militia-to-militia edges are exempt for the same reason the skill clamp exempts them:
militia are a design island. Upgrade sources in characters/npcs_*.xml (the villager_* entries)
take part read-only.

Runs in topological order over the whole DAG so a raise propagates down a chain. Dry-run by
default; --apply writes byte-faithfully (BOM and newline style preserved, ElementTree parse before
any write) and then re-checks itself from disk, exiting 1 if an edge still regresses. Idempotent.

Usage:
    python tools/fix_upgrade_armour_regressions.py            # report only
    python tools/fix_upgrade_armour_regressions.py --apply
    python tools/fix_upgrade_armour_regressions.py --game-modules "<.../Modules>"
"""

import argparse
import codecs
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_troops as rb  # noqa: E402  militia_troop_ids, DEFAULT_GAME_MODULES, MODULEDATA_DIR

MODULEDATA_DIR = os.path.abspath(rb.MODULEDATA_DIR)
TROOPS_DIR = os.path.join(MODULEDATA_DIR, 'troops')
CHARACTERS_DIR = os.path.join(MODULEDATA_DIR, 'characters')

ARMOUR_SLOTS = ('Head', 'Body', 'Cape', 'Gloves', 'Leg')
ARMOUR_STATS = ('head_armor', 'body_armor', 'arm_armor', 'leg_armor')

# (troop id, slot) -> item id. A hand decision that beats the family rule. State why.
OVERRIDES = {}

# Troops that never fill the Body slot on purpose (a slave in rags, the bare-chested Uruk-hai
# champions). Kept in one place, taom_schema.Validator._BODYLESS_BY_DESIGN, so the validator's
# MISSING_BODY_ARMOUR gate and this clamp cannot disagree about who is allowed a bare torso. For
# these troops the Body slot is left out of the comparison on BOTH sides of the edge.
try:
    import taom_schema as _ts
    BODYLESS_BY_DESIGN = frozenset(_ts.Validator._BODYLESS_BY_DESIGN)
except Exception:  # the schema module needs its JSON schemas; a bare checkout still gets the ids
    BODYLESS_BY_DESIGN = frozenset({'dg_goblin_slave', 'urukhai_champion', 'urukhai_berserker'})

# The parent is the anomaly, not the child. Some low troops carry a few HERO-tier sets among a
# dozen ordinary ones (imladris_recruit, level 21, wears a lord's circlet in 8 of its 13 sets and
# a lord's torso in 7), and raising every troop above them to lord kit is the wrong fix. Items
# matching a pattern here are swapped, on any troop BELOW the listed level, for the troop's own
# strongest non-matching item in the same slot. A troop with no such item is left alone. Runs
# before the clamp so the parent's need is measured on what it should be wearing.
DEMOTE = (
    # Rivendell and Lindon share these ids. Level 46 keeps noldorin_lancer's two lord torsos.
    (re.compile(r'^rivendell_(torso_lord|helmet_lord_circlet)'), 46),
)

# Tokens a family strip removes from the RIGHT of an item id, one at a time, until none match.
# tier\d+ and the silver / silvergold / gold colour suffixes are the Rivendell and Lindon
# vocabulary (rivendell_helmet_archer_tier1_silver). Without them the family collapsed to the
# whole id, no candidate ever matched, and two archer troops were handed the parent's cavalry
# helmet (deep review, 2026-09-04).
_TIER_TOKEN_RE = re.compile(
    r'_(?:[a-z]|\d+|[a-z]\d+|tier\d+|light|lite|med|medium|heavy|heav|elite|lord|noble|civ|'
    r'civilian|cape|nocape|slim|silver|silvergold|gold|bronze)$')


def family(item_id):
    """The item id with its trailing tier / variant tokens stripped."""
    fam = item_id
    while True:
        m = _TIER_TOKEN_RE.search(fam)
        if not m or m.start() == 0:
            return fam
        fam = fam[:m.start()]


# =============================================================================
# Item armour index
# =============================================================================

def load_item_armour(game_modules, moduledata=None):
    """item id -> {'value': int, 'folder': str|None, 'file': str}.

    folder is the LOTRLOME_items culture folder for Armory items, None for vanilla and repo items;
    file is the defining file's basename (head_armors.xml etc.), which is what keeps a helmet from
    being offered as a chest.
    """
    items = {}
    roots = []
    if game_modules:
        roots.append(os.path.join(game_modules, 'LOTRLOME_Armory', 'ModuleData'))
        roots.append(os.path.join(game_modules, 'SandBoxCore', 'ModuleData', 'items'))
    roots.append(moduledata or MODULEDATA_DIR)
    for root in roots:
        # *.xml only: the Armory folders carry *.xml.bak-* backups that must not be read.
        for fp in sorted(glob.glob(os.path.join(root, '**', '*.xml'), recursive=True)):
            try:
                tree = ET.parse(fp).getroot()
            except ET.ParseError:
                continue
            m = re.search(r'LOTRLOME_items[\\/]([^\\/]+)[\\/]', fp)
            folder = m.group(1) if m else None
            for it in tree.iter('Item'):
                a = it.find('.//Armor')
                iid = it.get('id')
                if a is None or not iid:
                    continue
                items[iid] = {
                    'value': sum(int(a.get(k, '0') or 0) for k in ARMOUR_STATS),
                    'folder': folder,
                    'file': os.path.basename(fp),
                }
    return items


# =============================================================================
# Troop index
# =============================================================================

def _is_civilian(elem):
    return elem.get('civilian') == 'true' or elem.get('equipmentType') == 'Civilian'


def load_troops(moduledata=None):
    """troop id -> record. Battle sets are lists of {slot: item id} in file order."""
    md = moduledata or MODULEDATA_DIR
    troops = {}
    sources = (sorted(glob.glob(os.path.join(md, 'troops', 'troops_*.xml')))
               + sorted(glob.glob(os.path.join(md, 'characters', 'npcs_*.xml'))))
    for fp in sources:
        try:
            root = ET.parse(fp).getroot()
        except ET.ParseError:
            continue
        external = os.path.basename(os.path.dirname(fp)) == 'characters'
        for npc in root.findall('.//NPCCharacter'):
            tid = npc.get('id', '')
            if not tid:
                continue
            sets = []
            for es in list(npc.iter('EquipmentRoster')) + list(npc.iter('EquipmentSet')):
                if _is_civilian(es):
                    continue
                eqs = es.findall('equipment')
                if not eqs:
                    continue
                slots = {}
                for eq in eqs:
                    slot = eq.get('slot', '')
                    if slot in ARMOUR_SLOTS:
                        slots[slot] = (eq.get('id') or '').replace('Item.', '', 1)
                sets.append(slots)
            upgrades = [(u.get('id') or '').replace('NPCCharacter.', '', 1)
                        for u in npc.findall('./upgrade_targets/upgrade_target')]
            troops[tid] = {
                'id': tid, 'file': fp, 'external': external,
                'level': int(npc.get('level', '0') or 0),
                'sets': sets,
                'upgrades': [u for u in upgrades if u],
            }
    return troops


def slot_values(troop, items):
    """{slot: [value per battle set]} with an unfilled slot counted as 0."""
    out = {s: [] for s in ARMOUR_SLOTS}
    for st in troop['sets']:
        for s in ARMOUR_SLOTS:
            iid = st.get(s)
            out[s].append(items.get(iid, {}).get('value', 0) if iid else 0)
    return out


def slot_avg(troop, items):
    vals = slot_values(troop, items)
    return {s: (sum(v) / len(v) if v else 0.0) for s, v in vals.items()}


def compared_slots(parent, child):
    """The slots an edge is judged on: every armour slot, minus Body and Cape when either end is
    bare-chested by design (a chest is not a stat such a troop could ever keep or hand down)."""
    # The bare-chested Uruk-hai wear a 70-armour skirt in the Cape slot as the stand-in for the
    # chest they never fill, so Cape goes with Body for them: an armoured nazg-hai in a heavy
    # pauldron is not a regression from a berserker in a skirt.
    if child['id'] in BODYLESS_BY_DESIGN or parent['id'] in BODYLESS_BY_DESIGN:
        return tuple(s for s in ARMOUR_SLOTS if s not in ('Body', 'Cape'))
    return ARMOUR_SLOTS


def total(troop, items, slots=ARMOUR_SLOTS):
    avg = slot_avg(troop, items)
    return sum(avg[s] for s in slots)


def demote_hero_kit(troops, items):
    """Apply DEMOTE (see the constant). Returns the change list, same shape as plan_fixes."""
    changes = []
    for troop in troops.values():
        if troop['external'] or not troop['sets']:
            continue
        for pattern, below_level in DEMOTE:
            if troop['level'] >= below_level:
                continue
            for slot in ARMOUR_SLOTS:
                worn = [st.get(slot) for st in troop['sets'] if st.get(slot)]
                flagged = [i for i in worn if pattern.search(i)]
                if not flagged:
                    continue
                keep = [i for i in worn if not pattern.search(i) and i in items]
                if not keep:
                    print('  NOTE: %s wears only hero kit in %s (%s); nothing ordinary to demote to'
                          % (troop['id'], slot, ', '.join(sorted(set(flagged)))))
                    continue
                best = max(set(keep), key=lambda i: (items[i]['value'], i))
                for st in troop['sets']:
                    old = st.get(slot)
                    if old and pattern.search(old):
                        st[slot] = best
                for old in sorted(set(flagged)):
                    changes.append({'troop': troop['id'], 'file': troop['file'], 'slot': slot,
                                    'old': old, 'new': best, 'how': 'demote',
                                    'parent': '(hero kit below level %d)' % below_level,
                                    'need': items[best]['value'],
                                    'old_value': items.get(old, {}).get('value', 0),
                                    'new_value': items[best]['value']})
    return changes


# =============================================================================
# Regressions and fixes
# =============================================================================

def topological_order(troops):
    indegree = defaultdict(int)
    children = defaultdict(list)
    for t in troops.values():
        for c in t['upgrades']:
            if c in troops:
                indegree[c] += 1
                children[t['id']].append(c)
    queue = [i for i in troops if indegree[i] == 0]
    order = []
    while queue:
        node = queue.pop()
        order.append(node)
        for c in children[node]:
            indegree[c] -= 1
            if indegree[c] == 0:
                queue.append(c)
    if len(order) != len(troops):
        cyclic = sorted(set(troops) - set(order))
        raise RuntimeError(
            'The upgrade graph is not acyclic, so there is no order to clamp in. Troops in or '
            'below a cycle: ' + ', '.join(cyclic[:10]))
    return order, children


def find_regressions(troops, items, militia):
    """[(parent id, child id, parent total, child total)] for every edge that drops."""
    out = []
    for t in troops.values():
        if not t['sets']:
            continue
        for cid in t['upgrades']:
            c = troops.get(cid)
            if not c or not c['sets']:
                continue
            if t['id'] in militia and cid in militia:
                continue
            slots = compared_slots(t, c)
            pt, ct = total(t, items, slots), total(c, items, slots)
            if ct + 1e-9 < pt:
                out.append((t['id'], cid, pt, ct))
    return sorted(out, key=lambda r: -(r[2] - r[3]))


def pick_replacement(child, slot, old_item, need, parent_item, items):
    """The item to put in `slot` so its value is >= need. See the module docstring."""
    key = (child['id'], slot)
    if key in OVERRIDES:
        return OVERRIDES[key], 'override'
    if old_item and old_item in items:
        fam = family(old_item)
        here = items[old_item]
        cands = [
            (v['value'], iid) for iid, v in items.items()
            if v['folder'] == here['folder'] and v['file'] == here['file']
            and family(iid) == fam and v['value'] + 1e-9 >= need
        ]
        if cands:
            cands.sort()
            return cands[0][1], 'family'
    if parent_item and parent_item in items and items[parent_item]['value'] + 1e-9 >= need:
        return parent_item, 'parent'
    return None, 'none'


def plan_fixes(troops, items, militia):
    """Mutates the in-memory troops so a raise propagates, and returns the change list.

    Each change: {'troop', 'file', 'slot', 'old', 'new', 'how', 'parent', 'need', ...}.
    """
    order, children = topological_order(troops)
    changes = demote_hero_kit(troops, items)
    for pid in order:
        parent = troops[pid]
        if not parent['sets']:
            continue
        for cid in children[pid]:
            child = troops[cid]
            if not child['sets'] or child['external']:
                continue
            if pid in militia and cid in militia:
                continue
            slots = compared_slots(parent, child)
            if total(child, items, slots) + 1e-9 >= total(parent, items, slots):
                continue
            pavg = slot_avg(parent, items)
            cavg = slot_avg(child, items)
            for slot in slots:
                need = pavg[slot]
                if cavg[slot] + 1e-9 >= need or need <= 0:
                    continue
                # The parent's STRONGEST item in this slot, for the fallback: the need is the
                # parent's average, so its best item always meets it while its most common
                # item may not (imladris_recruit averages 120 body over gold and silver sets).
                parent_items = [st.get(slot) for st in parent['sets'] if st.get(slot)]
                parent_item = (max(set(parent_items),
                                   key=lambda i: (items.get(i, {}).get('value', 0), i))
                               if parent_items else None)
                replaced = {}
                for st in child['sets']:
                    old = st.get(slot)
                    val = items.get(old, {}).get('value', 0) if old else 0
                    if val + 1e-9 >= need:
                        continue
                    if old in replaced:
                        if replaced[old] is not None:
                            st[slot] = replaced[old]
                        continue
                    new, how = pick_replacement(child, slot, old, need, parent_item, items)
                    if new is None:
                        replaced[old] = None
                        changes.append({'troop': cid, 'file': child['file'], 'slot': slot,
                                        'old': old, 'new': None, 'how': 'UNRESOLVED',
                                        'parent': pid, 'need': need})
                        continue
                    replaced[old] = new
                    st[slot] = new
                    changes.append({'troop': cid, 'file': child['file'], 'slot': slot,
                                    'old': old, 'new': new, 'how': how, 'parent': pid,
                                    'need': need,
                                    'old_value': val, 'new_value': items[new]['value']})
    return changes


# =============================================================================
# Writing
# =============================================================================

_NPC_RE = re.compile(r'<NPCCharacter\b[^>]*(?:/>|>.*?</NPCCharacter>)', re.S)
# The /> alternation is load-bearing (the hazard taom_schema._INLINE_ROSTER_RE documents): a
# self-closing <EquipmentSet ... /> civilian-template reference otherwise matches on its own ">"
# and runs forward to an unrelated close tag, swallowing the next set into its "body".
_SET_RE = re.compile(r'<(EquipmentRoster|EquipmentSet)\b([^>]*?)(?:/>|>(.*?)</\1>)', re.S)
_EQ_RE = re.compile(r'<equipment\b[^>]*?/>')


def _rewrite_block(block, edits):
    """Apply {(slot, old): new} to every battle set in one NPCCharacter block.

    old None means the slot was unfilled: the element is appended after the last equipment
    element of the set, cloning its indentation.
    """
    def fix_set(m):
        tag, attrs, body = m.group(1), m.group(2), m.group(3)
        if body is None:  # self-closing: a template reference, nothing to rewrite
            return m.group(0)
        if 'civilian="true"' in attrs or 'equipmentType="Civilian"' in attrs:
            return m.group(0)
        present = {}
        for em in _EQ_RE.finditer(body):
            sm = re.search(r'\bslot="([^"]+)"', em.group(0))
            if sm:
                present[sm.group(1)] = em
        new_body = body
        # Replace present slots first (right to left so spans stay valid).
        for slot, em in sorted(present.items(), key=lambda kv: -kv[1].start()):
            im = re.search(r'\bid="Item\.([^"]+)"', em.group(0))
            old = im.group(1) if im else None
            new = edits.get((slot, old))
            if new is None:
                continue
            tag_text = em.group(0).replace('id="Item.%s"' % old, 'id="Item.%s"' % new, 1)
            new_body = new_body[:em.start()] + tag_text + new_body[em.end():]
        # Then append the slots this set never filled.
        for (slot, old), new in edits.items():
            if old is not None or slot in present:
                continue
            last = None
            for em in _EQ_RE.finditer(new_body):
                last = em
            if last is None:
                continue
            indent_m = re.search(r'\n([ \t]*)$', new_body[:last.start()])
            indent = indent_m.group(1) if indent_m else ''
            elem = '\n%s<equipment slot="%s" id="Item.%s" />' % (indent, slot, new)
            new_body = new_body[:last.end()] + elem + new_body[last.end():]
        return '<%s%s>%s</%s>' % (tag, attrs, new_body, tag)
    return _SET_RE.sub(fix_set, block)


def write_changes(changes):
    by_file = defaultdict(lambda: defaultdict(dict))
    for c in changes:
        if c['new'] is None:
            continue
        by_file[c['file']][c['troop']][(c['slot'], c['old'])] = c['new']
    written = 0
    for fp, per_troop in by_file.items():
        with open(fp, 'rb') as fh:
            raw = fh.read()
        bom = raw.startswith(codecs.BOM_UTF8)
        text = raw.decode('utf-8-sig' if bom else 'utf-8')
        newline = '\r\n' if '\r\n' in text else '\n'
        text = text.replace('\r\n', '\n')
        original = text

        def fix_npc(m):
            head = m.group(0)[:m.group(0).find('>') + 1]
            idm = re.search(r'\bid="([^"]*)"', head)
            if not idm or idm.group(1) not in per_troop:
                return m.group(0)
            return _rewrite_block(m.group(0), per_troop[idm.group(1)])
        text = _NPC_RE.sub(fix_npc, text)
        if text == original:
            continue
        try:
            ET.fromstring(text.encode('utf-8'))
        except ET.ParseError as exc:
            raise RuntimeError(
                '%s would no longer be well-formed XML after the armour rewrite, so nothing '
                'was written: %s' % (os.path.basename(fp), exc))
        out = text.replace('\n', newline).encode('utf-8')
        if bom:
            out = codecs.BOM_UTF8 + out
        with open(fp, 'wb') as fh:
            fh.write(out)
        written += 1
    return written


# =============================================================================
# Main
# =============================================================================

def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--apply', action='store_true', help='write the rosters (default: report only)')
    ap.add_argument('--game-modules', default=rb.DEFAULT_GAME_MODULES,
                    help='.../Mount & Blade II Bannerlord/Modules (item armour comes from the install)')
    args = ap.parse_args(argv)

    if not os.path.isdir(args.game_modules):
        print('ERROR: Bannerlord Modules folder not found: %s\n'
              '       Item armour values come from the install; pass --game-modules.'
              % args.game_modules)
        return 2

    items = load_item_armour(args.game_modules)
    troops = load_troops()
    militia = rb.militia_troop_ids()
    print('Item armour index: %s items. Troops: %s. Militia bound: %d.'
          % (format(len(items), ','), format(len(troops), ','), len(militia)))

    before = find_regressions(troops, items, militia)
    print('\nUpgrade edges that lower armour: %d' % len(before))
    for p, c, pt, ct in before:
        print('  %s (%.0f) -> %s (%.0f)  drop %.0f' % (p, pt, c, ct, pt - ct))

    changes = plan_fixes(troops, items, militia)
    unresolved = [c for c in changes if c['new'] is None]
    resolved = [c for c in changes if c['new'] is not None]
    print('\nSlot replacements planned: %d (%d family, %d parent fallback, %d override, '
          '%d hero-kit demotions), unresolved: %d' % (
              len(resolved),
              sum(1 for c in resolved if c['how'] == 'family'),
              sum(1 for c in resolved if c['how'] == 'parent'),
              sum(1 for c in resolved if c['how'] == 'override'),
              sum(1 for c in resolved if c['how'] == 'demote'),
              len(unresolved)))
    for c in resolved:
        print('  %-38s %-6s %-44s -> %-44s %3d -> %3d (%s, needs %.0f for %s)' % (
            c['troop'], c['slot'], str(c['old']), c['new'], c['old_value'], c['new_value'],
            c['how'], c['need'], c['parent']))
    for c in unresolved:
        print('  UNRESOLVED %s %s old=%s needs %.0f for %s' % (
            c['troop'], c['slot'], c['old'], c['need'], c['parent']))

    after = find_regressions(troops, items, militia)
    print('\nEdges still dropping after the planned fixes: %d' % len(after))
    for p, c, pt, ct in after:
        print('  %s (%.0f) -> %s (%.0f)  drop %.0f' % (p, pt, c, ct, pt - ct))

    if not args.apply:
        print('\n(dry run; pass --apply to write)')
        return 0 if not after and not unresolved else 1

    written = write_changes(changes)
    print('\n*** Changes written to %d files ***' % written)
    # Re-read from disk: the proof is what landed, not what was planned.
    troops = load_troops()
    final = find_regressions(troops, items, militia)
    if final:
        print('STILL REGRESSING after write: %d edge(s)' % len(final))
        for p, c, pt, ct in final:
            print('  %s (%.0f) -> %s (%.0f)' % (p, pt, c, ct))
        return 1
    print('Re-check from disk: 0 upgrade edges lower armour.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
