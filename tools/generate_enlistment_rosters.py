#!/usr/bin/env python3
"""Generate per-culture/per-rank enlistment armor rosters (#375 Phase 4, E1).

Emits Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml: one
<EquipmentRoster id="enlist_{runtimeCultureId}_{rank}"> per tree-culture x rank
(rank in recruit|soldier|veteran|sergeant), armor slots ONLY (Head/Body/Leg/
Gloves/Cape), no gender dimension. Looked up at rank-up by
EnlistmentEquipmentService via EnlistmentRosterResolver (fallback chain:
exact -> lower ranks -> enlist_default_{rank} -> none).

Culture ids are RUNTIME StringIds (the #1 TAOM data bug): vlandia=Rohan,
empire=Dunland, aserai=Harad, khuzait=Rhun, sturgia=Dale, battania=Khand.
Cultures are enumerated from Main/_Module/ModuleData/troops/troops_*.xml (the
16 tree-cultures); lothlorien and battania (Khand) have no troop tree and fall
through to the hand-authored enlist_default_{rank} rosters at runtime.

Donor selection per (culture, rank): pick the troop whose level sits in the
rank's band -- consistent with derive_armor_tiers.level_to_tier (recruit<=13,
soldier 14-18, veteran 19-30, sergeant 31+) -- preferring infantry-line donors,
and penalizing overshoot (2x) harder than undershoot so a recruit never seeds
elite plate. The donor's armor-slot items are emitted; every emitted item id
must resolve against the LIVE armory index (derive_armor_tiers.build_armory_index);
unresolvable items are skipped with a warning.

The 4 enlist_default_{rank} rosters are HAND-AUTHORED (broadly-available human
militia armor, verified against the armory index) and carried as a literal
block here so a full --apply regeneration preserves them.

Usage:
    python tools/generate_enlistment_rosters.py                    # dry-run (default)
    python tools/generate_enlistment_rosters.py --apply            # write the XML
    python tools/generate_enlistment_rosters.py --culture vlandia  # restrict
    python tools/generate_enlistment_rosters.py --apply --seed-missing
        # append-only: adds ONLY rosters whose ids are absent (preserves hand edits)

XML I/O per tools/README.md "XML I/O convention": UTF-8 (BOM-preserving on
rewrite; new file written without BOM, matching the equipmentsets siblings),
CRLF line endings, backup on a non-.xml extension before destructive writes.
"""

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derive_armor_tiers as dat  # noqa: E402  (armory index + level_to_tier bands)

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
TROOPS_DIR = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'troops')
OUT_XML = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData',
                       'equipmentsets', 'taom_enlistment_equipment.xml')

RANKS = ['recruit', 'soldier', 'veteran', 'sergeant']

# Rank -> troop-level band. MUST stay consistent with derive_armor_tiers.level_to_tier
# (light<=13, medium 14-18, heavy 19-30, elite 31+). Checked at import below.
RANK_BANDS = {
    'recruit':  (1, 13),
    'soldier':  (14, 18),
    'veteran':  (19, 30),
    'sergeant': (31, 999),
}
assert dat.level_to_tier(13) == 'light' and dat.level_to_tier(14) == 'medium' \
    and dat.level_to_tier(18) == 'medium' and dat.level_to_tier(19) == 'heavy' \
    and dat.level_to_tier(30) == 'heavy' and dat.level_to_tier(31) == 'elite', \
    'RANK_BANDS drifted from derive_armor_tiers.level_to_tier -- realign before running'

# Output order for armor slots (armor ONLY -- no weapons, no Horse/HorseHarness).
ARMOR_SLOTS = ['Head', 'Body', 'Leg', 'Gloves', 'Cape']

# Overshoot is penalized 2x undershoot: issuing under-tier armor is a mild
# disappointment; issuing elite plate to a recruit breaks economy + fiction.
OVERSHOOT_WEIGHT = 2

# Hand-authored culture-neutral fallbacks (enlist_default_{rank}). Broadly-available
# human militia armor; every id verified against the live armory index on generation
# (missing ids fail the run loudly). neutral_culture is the engine's culture-less
# StringId (validator-known). PRESERVED verbatim by full --apply regeneration.
DEFAULT_ROSTER_ITEMS = {
    'recruit': [
        ('Body', 'rohan_militia_tunic_a'),
        ('Leg', 'cts_rohan_boots3'),
    ],
    'soldier': [
        ('Head', 'cts_rohan_helmet1'),
        ('Body', 'rohan_militia_tunic_b'),
        ('Leg', 'cts_rohan_boots4'),
    ],
    'veteran': [
        ('Head', 'sk_dale_helmet_infrantry_a01'),
        ('Body', 'rohan_militia_armour_a'),
        ('Leg', 'dunland_caerdh_boots_light_a'),
        ('Gloves', 'sk_dale_gauntlet_infrantry_a01'),
    ],
    'sergeant': [
        ('Head', 'cts_rohan_helmet1b'),
        ('Body', 'rohan_militia_armour_b'),
        ('Leg', 'dunland_caerdh_boots_light_b'),
        ('Gloves', 'dunland_caerdh_bracer_light_a'),
        ('Cape', 'dunland_wulf_cape_short_a'),
    ],
}
DEFAULT_CULTURE = 'neutral_culture'


# =============================================================================
# Parsing
# =============================================================================

def parse_troops():
    """Return {runtime_culture_id: [troop dicts]} from troops_*.xml.

    Troop dict: {id, level, group, slots: {ArmorSlot: item_id}} using the FIRST
    non-civilian inline EquipmentRoster. Heroes / non-Soldier occupations are
    skipped (troop trees are all occupation="Soldier" today; guard is cheap).
    """
    by_culture = {}
    if not os.path.isdir(TROOPS_DIR):
        raise SystemExit(f'ERROR: troops dir not found: {TROOPS_DIR}')
    for fn in sorted(os.listdir(TROOPS_DIR)):
        if not (fn.startswith('troops_') and fn.endswith('.xml')):
            continue
        try:
            root = ET.parse(os.path.join(TROOPS_DIR, fn)).getroot()
        except ET.ParseError as e:
            print(f'  WARN: parse error in {fn}: {e}', file=sys.stderr)
            continue
        for npc in root.findall('.//NPCCharacter'):
            culture_raw = npc.get('culture', '')
            culture = culture_raw.split('.', 1)[1] if culture_raw.startswith('Culture.') else None
            if not culture:
                continue
            if npc.get('is_hero') == 'true':
                continue
            if npc.get('occupation', 'Soldier') != 'Soldier':
                continue
            try:
                level = int(npc.get('level', '0'))
            except ValueError:
                continue
            slots = {}
            for roster in npc.findall('.//EquipmentRoster'):
                if roster.get('civilian') == 'true':
                    continue
                for eq in roster.findall('equipment'):
                    slot = eq.get('slot', '')
                    if slot not in ARMOR_SLOTS:
                        continue
                    raw = eq.get('id', '')
                    item_id = raw.split('.', 1)[1] if raw.startswith('Item.') else raw
                    if item_id and slot not in slots:
                        slots[slot] = item_id
                break  # first non-civilian roster only
            by_culture.setdefault(culture, []).append({
                'id': npc.get('id', ''),
                'level': level,
                'group': npc.get('default_group', ''),
                'slots': slots,
            })
    return by_culture


# =============================================================================
# Donor selection
# =============================================================================

def band_score(level, rank):
    lo, hi = RANK_BANDS[rank]
    if level < lo:
        return lo - level
    if level > hi:
        return OVERSHOOT_WEIGHT * (level - hi)
    return 0


def resolvable_slots(troop, index):
    return {s: i for s, i in troop['slots'].items() if i in index}


def pick_donor(troops, rank, index):
    """Best donor for a rank: in-band level, infantry preferred, overshoot 2x
    penalized, more armory-resolvable armor slots preferred. Requires a
    resolvable Body item (a chestless issue kit is not worth emitting)."""
    candidates = []
    for t in troops:
        resolved = resolvable_slots(t, index)
        if 'Body' not in resolved:
            continue
        candidates.append((
            band_score(t['level'], rank),
            0 if t['group'] == 'Infantry' else 1,
            -len(resolved),
            t['level'],
            t['id'],
            t,
            resolved,
        ))
    if not candidates:
        return None, {}
    candidates.sort(key=lambda c: c[:5])
    best = candidates[0]
    return best[5], best[6]


# =============================================================================
# XML emission
# =============================================================================

def roster_block(roster_id, culture, slot_items):
    lines = [f'    <EquipmentRoster id="{roster_id}" culture="Culture.{culture}">',
             '        <EquipmentSet>']
    for slot in ARMOR_SLOTS:
        if slot in slot_items:
            lines.append(f'            <Equipment slot="{slot}" id="Item.{slot_items[slot]}" />')
    lines.append('        </EquipmentSet>')
    lines.append('    </EquipmentRoster>')
    return '\n'.join(lines)


def default_blocks(index):
    blocks = ['\n    <!-- ═══ HAND-AUTHORED culture-neutral fallbacks (enlist_default_{rank}) ═══',
              '         Broadly-available human militia armor. The resolver falls through here',
              '         for cultures with no roster (lothlorien, battania/Khand). Do not let a',
              '         regeneration drop these: the generator carries them as a literal. -->\n']
    for rank in RANKS:
        items = DEFAULT_ROSTER_ITEMS[rank]
        missing = [i for _, i in items if i not in index]
        if missing:
            raise SystemExit(f'ERROR: hand-authored default items missing from armory index: {missing}')
        blocks.append(roster_block(f'enlist_default_{rank}', DEFAULT_CULTURE, dict(items)))
    return blocks


def file_header():
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<!--\n'
        '  TAOM enlistment service-issue armor rosters (#375 Phase 4).\n'
        '\n'
        '  Roster ID convention: enlist_{runtimeCultureId}_{rank}, rank in\n'
        '  recruit|soldier|veteran|sergeant. Armor slots ONLY (Head/Body/Leg/Gloves/Cape);\n'
        '  no gender dimension. Issued to the party INVENTORY (not equipped) once per rank\n'
        '  by EnlistmentEquipmentService; fallback chain exact -> lower ranks ->\n'
        '  enlist_default_{rank} -> none (EnlistmentRosterResolver).\n'
        '\n'
        '  RUNTIME culture ids (the #1 TAOM data bug; lore names are WRONG here):\n'
        '  vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Rhun, sturgia=Dale,\n'
        '  battania=Khand. lothlorien + battania have no troop tree -> no rosters ->\n'
        '  runtime fallthrough to enlist_default_{rank}.\n'
        '\n'
        '  GENERATED by tools/generate_enlistment_rosters.py from per-culture donor troops\n'
        '  (rank -> level band per derive_armor_tiers.level_to_tier) + the live\n'
        '  LOTRLOME_Armory index. Hand-tune freely, then keep re-runs append-only with the\n'
        '  seed-missing flag; a full apply regeneration overwrites tuned culture rosters.\n'
        '  (XML comments cannot contain double hyphens, hence the spelled-out flag names.)\n'
        '  The enlist_default_{rank} block is hand-authored and survives regeneration.\n'
        '-->\n'
        '<EquipmentRosters>\n'
    )


# =============================================================================
# I/O (tools/README.md XML I/O convention)
# =============================================================================

def write_xml(path, text):
    """CRLF + BOM-preserving byte write. New files: no BOM (equipmentsets convention)."""
    had_bom = False
    if os.path.exists(path):
        had_bom = open(path, 'rb').read(3) == b'\xef\xbb\xbf'
        bak = path + '.bak-enlist'
        if not os.path.exists(bak):
            with open(path, 'rb') as src, open(bak, 'wb') as dst:
                dst.write(src.read())
    crlf = text.replace('\r\n', '\n').replace('\n', '\r\n')
    with open(path, 'wb') as fh:
        fh.write((b'\xef\xbb\xbf' if had_bom else b'') + crlf.encode('utf-8'))


# =============================================================================
# Main
# =============================================================================

def main():
    ap = argparse.ArgumentParser(description='Generate enlistment armor rosters (dry-run default).')
    ap.add_argument('--apply', action='store_true', help='write the XML (default: dry-run)')
    ap.add_argument('--culture', default='', help='restrict to one runtime culture id (e.g. vlandia)')
    ap.add_argument('--seed-missing', action='store_true',
                    help='append-only: add ONLY rosters whose ids are absent from the existing file')
    args = ap.parse_args()

    index, armory_dir = dat.build_armory_index()
    if not index:
        # environment-failures.md: report, don't self-heal. The generator still can't
        # verify item resolvability without the live armory -- refuse to emit.
        raise SystemExit(f'ERROR: no armory items found under {armory_dir} '
                         '(set $BANNERLORD_GAME_DIR to the game install). '
                         'Refusing to emit unverifiable rosters.')
    print(f'Armory index: {len(index)} items from {armory_dir}')

    by_culture = parse_troops()
    cultures = sorted(by_culture)
    if args.culture:
        if args.culture not in by_culture:
            raise SystemExit(f'ERROR: culture {args.culture!r} has no troop tree. '
                             f'Known: {", ".join(cultures)}')
        cultures = [args.culture]
    print(f'Tree-cultures: {len(cultures)} ({", ".join(cultures)})')

    blocks = []
    donor_table = []  # (culture, rank, donor_id, level, group, slots emitted, slots skipped)
    for culture in cultures:
        troops = by_culture[culture]
        blocks.append(f'\n    <!-- ═══ {culture.upper()} ═══ -->\n')
        for rank in RANKS:
            donor, resolved = pick_donor(troops, rank, index)
            rid = f'enlist_{culture}_{rank}'
            if donor is None:
                print(f'  WARN: {rid}: no donor with an armory-resolvable Body item -- '
                      'roster NOT emitted (runtime falls through)')
                donor_table.append((culture, rank, None, None, None, [], []))
                continue
            skipped = sorted(set(donor['slots']) - set(resolved))
            for slot in skipped:
                print(f'  WARN: {rid}: donor {donor["id"]} slot {slot} item '
                      f'{donor["slots"][slot]!r} not in armory index -- skipped')
            blocks.append(roster_block(rid, culture, resolved))
            donor_table.append((culture, rank, donor['id'], donor['level'], donor['group'],
                                sorted(resolved), skipped))

    blocks.extend(default_blocks(index))
    content = file_header() + '\n'.join(blocks) + '\n\n</EquipmentRosters>\n'

    emitted = sum(1 for _, _, d, *_ in donor_table if d) + len(RANKS)
    print(f'\nDonor table ({sum(1 for r in donor_table if r[2])} culture rosters '
          f'+ {len(RANKS)} defaults = {emitted} total):')
    print(f'  {"culture":<18}{"rank":<10}{"donor troop":<40}{"L":>3}  {"group":<12}slots')
    for culture, rank, donor_id, level, group, slots, skipped in donor_table:
        if donor_id is None:
            print(f'  {culture:<18}{rank:<10}{"-- NONE --":<40}')
            continue
        skip_note = f'  (skipped: {",".join(skipped)})' if skipped else ''
        print(f'  {culture:<18}{rank:<10}{donor_id:<40}{level:>3}  {group:<12}'
              f'{",".join(slots)}{skip_note}')

    if not args.apply:
        print('\nDRY-RUN: no file written. Re-run with --apply.')
        return

    if args.seed_missing and os.path.exists(OUT_XML):
        raw = open(OUT_XML, 'rb').read()
        existing = raw.decode('utf-8-sig')
        present = set(re.findall(r'<EquipmentRoster id="([^"]+)"', existing))
        new_blocks = []
        for culture, rank, donor_id, *_ in donor_table:
            rid = f'enlist_{culture}_{rank}'
            if donor_id is None or rid in present:
                continue
            donor, resolved = pick_donor(by_culture[culture], rank, index)
            new_blocks.append(roster_block(rid, culture, resolved))
        for rank in RANKS:
            rid = f'enlist_default_{rank}'
            if rid not in present:
                items = DEFAULT_ROSTER_ITEMS[rank]
                new_blocks.append(roster_block(rid, DEFAULT_CULTURE, dict(items)))
        if not new_blocks:
            print('\n--seed-missing: nothing to add (all roster ids already present).')
            return
        insert = '\n    <!-- seeded by generate_enlistment_rosters.py --seed-missing -->\n' \
            + '\n'.join(new_blocks) + '\n\n</EquipmentRosters>'
        updated = existing.replace('</EquipmentRosters>', insert, 1)
        write_xml(OUT_XML, updated)
        print(f'\n--seed-missing: appended {len(new_blocks)} roster(s) to {OUT_XML}')
        return

    if args.culture:
        raise SystemExit('ERROR: --apply with --culture would write a partial file; '
                         'use --culture only for dry-run inspection, or --seed-missing.')

    write_xml(OUT_XML, content)
    print(f'\nWROTE {OUT_XML}')
    print('Next: python tools/validate_moduledata.py  (equipmentsets schema covers this file)')
    print('NOTE: the file loads only once registered in Main/_Module/SubModule.xml '
          '(<XmlName id="EquipmentRosters" path="equipmentsets/taom_enlistment_equipment"/>) '
          'and only at a full game restart.')


if __name__ == '__main__':
    main()
