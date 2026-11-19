#!/usr/bin/env python3
"""
Troop Skill Rebalancing Script for TAOM

Applies the role table (#343) to every troop: the main carried weapon takes N x (1 + the
culture's percent), where N is the tier's number on TIER_CURVE, and every other skill is a share
of N (calculate_skills; docs/features/troop-skill-balance.md).

The roles are read from the troop's equipment: the item classes of its battle sets (via
tools/taom_schema.build_item_class_registry, which needs the game install for vanilla and Armory
item definitions) and its horses' difficulty (build_mount_difficulty_registry). Name keywords
once mis-statted crossbowmen named "Sharpshooter" and two-hander cavalry named "Knight"
(issues #340/#341), so no name is read.

The kit decides the ORDER of the weapon skills (tools/troop_purpose.py): the Item0 class takes
the troop's highest melee or launcher value, then the other carried classes, then the classes it
does not carry.

Usage:
    python rebalance_troops.py --dry-run    # Preview changes
    python rebalance_troops.py --apply      # Write changes to XML files
    python rebalance_troops.py --fix-purpose [--dry-run]   # reorder on-disk skills to the kit
    python rebalance_troops.py --apply --game-modules "E:/.../Modules"
"""

import xml.etree.ElementTree as ET
import os
import sys
import glob
import re
import copy
import codecs
import math
from collections import Counter, defaultdict
from fractions import Fraction
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import taom_schema as ts  # noqa: E402  build_item_class_registry
from _gamedir import game_modules as _resolve_game_modules  # noqa: E402
import ranged_ladder as rl  # noqa: E402  the Bow/Crossbow cell of a ladder troop (#617)
import troop_purpose as tp  # noqa: E402  the kit decides which skills are highest (#343)
import sync_lord_inline_skills as sl  # noqa: E402  effective_skills, load_skill_sets (#626)

TROOPS_DIR = os.path.join(os.path.dirname(__file__), '..', 'Main', '_Module', 'ModuleData', 'troops')
MODULEDATA_DIR = os.path.join(os.path.dirname(__file__), '..', 'Main', '_Module', 'ModuleData')
# $BANNERLORD_GAME_DIR wins over the literal (#404). The bare E: path was one machine's and
# errors out at startup anywhere else. analyze_troop_balance.py reads this same constant
# rather than holding its own copy, so both tools move together.
DEFAULT_GAME_MODULES = str(_resolve_game_modules(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
SKILL_NAMES = ['Athletics', 'Riding', 'OneHanded', 'TwoHanded', 'Polearm', 'Bow', 'Crossbow', 'Throwing']

SKIP_FILES = set()

# Troops EXCLUDED from the formula rebaseline — genuine non-humanoid creatures and
# hand-tuned bespoke mount riders whose skills are intentionally off the humanoid curve.
# (cave_troll = monster; harad_elephant_rider / harad_mumakil_rider = bespoke
# elephant/mumakil-back riders.)
#
# The Iron Hills noble crossbow line (iron_hills_noble_scout / _sharpshooter /
# _veteran_sharpshooter) was listed here from 2026-07-30 to keep a Crossbow hand-tune
# (175/225/275, #366). #617 put the line on the Erebor ranged ladder cells (120/160/195, the
# same as ironpass_*): Mike, 2026-09-18, the ranking holds and a crossbow's edge is its item
# (hardest-hitting crossbows at T4 to T6), so it is no longer skipped and a rebaseline gives it
# the ladder cell like every other archer.
SKIP_TROOP_IDS = {
    'cave_troll',
    'hill_troll',   # the cave troll's bespoke skills, copied (2026-09-25)
    'harad_elephant_rider',
    'harad_mumakil_rider',
    'goblin_spider_rider',   # the Dol Guldur Spider Rider skills, copied (2026-09-29)
    'goblin_spider_lord',
    'goblin_wolf_runner',   # the dire wolf lines (#1081): hand-set, gated by the upgrade-regression checks
    'goblin_dire_wolf_rider',
    'goblin_wolf_lord',
    'gundabad_wolf_runner',
    'gundabad_dire_wolf_rider',
    'gundabad_wolf_lord',
}

# Troops whose ONLY ranged option is a thrown weapon, so Throwing rather than Bow is their
# ranged identity (#554). Hardcoded rather than derived from equipment because
# LOTRLOME_Armory's ModuleData is empty on the reference install and 247 of the 315 weapon
# ids the troop files reference cannot be classified: a "carries no bow" predicate would
# read an invisible Armory bow as no bow and hand a real archer the throwing curve.
# Replace with the equipment predicate once the Armory install is restored (#555).
THROWN_PRIMARY_TROOP_IDS = frozenset({
    'gondor_har_skirmisher', 'gondor_har_vet_skirmisher', 'gondor_har_javelineer',
    'sagarun_naffatun', 'sagarun_storm_helmed_naffatun',
})

# Upgrade edges where a child deliberately re-specialises OFF a skill its parent carried for
# REAL, so the usual raise-to-parent clamp would undo the specialisation. Distinct from the
# ordinary inert baseline noise the clamp exists to protect: the parent here actually carries
# the weapon. Adding an entry is a deliberate act, so state why. MIRRORED in
# taom_schema.py's _upgrade_skill_regressions and in TroopUpgradeSkillMonotonicityTests.cs;
# all three must agree or the writer, the validator and the C# gate judge the same edge
# differently, and the clamp silently re-inflates what the writer just floored.
RESPECIALIZATION_EXEMPT_EDGES = {
    # sagarun_crossbowman carries a real crossbow at 160. Its naffatun child throws javelins
    # and carries neither bow nor crossbow, so both values are floored rather than inherited
    # (#554). Throwing takes the ranged curve in their place.
    ('sagarun_crossbowman', 'sagarun_naffatun'): {'Bow', 'Crossbow'},
}

# =============================================================================
# The role table (#343): one curve on the vanilla range, spread over TAOM's ten tiers
# =============================================================================

# N, the main weapon skill of a culture with no edge, by engine tier. Tiers 1 to 5 are vanilla's
# grid (20, 40, 70, 100, 130); tiers 6 to 10 go on in even steps to 260. The ranged ladder anchors
# its rank-7 line on the same numbers (tools/ranged_ladders.json stats.skill).
TIER_CURVE = {1: 20, 2: 40, 3: 70, 4: 100, 5: 130, 6: 156, 7: 182, 8: 208, 9: 234, 10: 260}

# Every other skill is a share of N, read off vanilla's troops (docs/features/troop-skill-balance.md).
SIDEARM = Fraction(9, 10)             # a carried weapon that is not the main one
UNCARRIED_MELEE = Fraction(1, 2)      # a melee class the troop does not carry
UNCARRIED_RANGED = Fraction(2, 5)     # a launcher or Throwing the troop does not carry
MOUNTED_ATHLETICS = Fraction(1, 2)
FOOT_RIDING = Fraction(2, 5)
ARCHER_SIDEARM = Fraction(9, 10)      # an archer's melee, of its own launcher value, rounded down

# The culture's edge, in percent of N, on each skill. A culture with no row is an error: the Lindon
# lesson (#522) was a missing key that put a whole faction on the bare curve with no word.
CULTURE_PCT = {
    'gondor': {'Athletics': 3, 'Riding': 3, 'OneHanded': 5, 'TwoHanded': 3, 'Polearm': 3, 'Bow': 0, 'Crossbow': 0, 'Throwing': -5},
    'rohan': {'Athletics': -3, 'Riding': 15, 'OneHanded': 0, 'TwoHanded': 0, 'Polearm': 5, 'Bow': -3, 'Crossbow': -5, 'Throwing': 1},
    'erebor': {'Athletics': 5, 'Riding': -10, 'OneHanded': 8, 'TwoHanded': 12, 'Polearm': 8, 'Bow': 0, 'Crossbow': 0, 'Throwing': 5},
    'iron_hills': {'Athletics': 5, 'Riding': -3, 'OneHanded': 10, 'TwoHanded': 12, 'Polearm': 12, 'Bow': 0, 'Crossbow': 3, 'Throwing': 5},
    'eredluin': {'Athletics': 5, 'Riding': -10, 'OneHanded': 8, 'TwoHanded': 12, 'Polearm': 8, 'Bow': 0, 'Crossbow': 0, 'Throwing': 5},
    'rivendell': {'Athletics': 18, 'Riding': 12, 'OneHanded': 18, 'TwoHanded': 20, 'Polearm': 20, 'Bow': 20, 'Crossbow': 20, 'Throwing': 20},
    # Lindon is a Rivendell twin: most of its troops have a rivendell_ or imladris_ counterpart.
    'lindon': {'Athletics': 18, 'Riding': 12, 'OneHanded': 18, 'TwoHanded': 20, 'Polearm': 20, 'Bow': 20, 'Crossbow': 20, 'Throwing': 20},
    'mirkwood': {'Athletics': 23, 'Riding': 3, 'OneHanded': 20, 'TwoHanded': 15, 'Polearm': 15, 'Bow': 25, 'Crossbow': 25, 'Throwing': 25},
    'lothlorien': {'Athletics': 18, 'Riding': 10, 'OneHanded': 15, 'TwoHanded': 13, 'Polearm': 15, 'Bow': 18, 'Crossbow': 18, 'Throwing': 18},
    'dale': {'Athletics': 3, 'Riding': -5, 'OneHanded': 3, 'TwoHanded': 6, 'Polearm': 13, 'Bow': 6, 'Crossbow': 6, 'Throwing': -3},
    'dunland': {'Athletics': 10, 'Riding': -3, 'OneHanded': 3, 'TwoHanded': 3, 'Polearm': 0, 'Bow': 0, 'Crossbow': 0, 'Throwing': 8},
    'rhun': {'Athletics': 3, 'Riding': 9, 'OneHanded': 0, 'TwoHanded': 0, 'Polearm': 8, 'Bow': -5, 'Crossbow': -5, 'Throwing': -3},
    'umbar': {'Athletics': 5, 'Riding': -8, 'OneHanded': 5, 'TwoHanded': 3, 'Polearm': 0, 'Bow': 0, 'Crossbow': 0, 'Throwing': 0},
    'harad': {'Athletics': 0, 'Riding': 8, 'OneHanded': 3, 'TwoHanded': -5, 'Polearm': -3, 'Bow': 5, 'Crossbow': 0, 'Throwing': 0},
    'arthedain': {'Athletics': 5, 'Riding': 2, 'OneHanded': 3, 'TwoHanded': 2, 'Polearm': 2, 'Bow': 5, 'Crossbow': 0, 'Throwing': -3},
    'cardolan': {'Athletics': 0, 'Riding': 5, 'OneHanded': 3, 'TwoHanded': 0, 'Polearm': 5, 'Bow': 0, 'Crossbow': 0, 'Throwing': -3},
    'rhudaur': {'Athletics': 5, 'Riding': 0, 'OneHanded': 2, 'TwoHanded': 5, 'Polearm': 0, 'Bow': 2, 'Crossbow': 0, 'Throwing': 5},
    'anduinvale': {'Athletics': 8, 'Riding': -10, 'OneHanded': 0, 'TwoHanded': 8, 'Polearm': 0, 'Bow': 3, 'Crossbow': 0, 'Throwing': 0},
    'angaladh': {'Athletics': 5, 'Riding': -5, 'OneHanded': 0, 'TwoHanded': 3, 'Polearm': 3, 'Bow': 5, 'Crossbow': 0, 'Throwing': 0},
    'dorwinion': {'Athletics': 0, 'Riding': 3, 'OneHanded': 0, 'TwoHanded': 0, 'Polearm': 3, 'Bow': 5, 'Crossbow': 0, 'Throwing': 0},
    'southrhovanion': {'Athletics': 0, 'Riding': 8, 'OneHanded': 0, 'TwoHanded': 0, 'Polearm': 5, 'Bow': 3, 'Crossbow': 0, 'Throwing': 0},
    'isengard': {'Athletics': 5, 'Riding': 3, 'OneHanded': 5, 'TwoHanded': 5, 'Polearm': 5, 'Bow': 0, 'Crossbow': 5, 'Throwing': 5},
    # Orthanc guard (orthanc_*), Isengard's elite line, routed by detect_culture.
    'isengard_orthanc': {'Athletics': 9, 'Riding': 3, 'OneHanded': 6, 'TwoHanded': 8, 'Polearm': 8, 'Bow': 0, 'Crossbow': 6, 'Throwing': 6},
    # Black Uruks (mordor_uruk_*), Mordor's elite line, routed by detect_culture.
    'mordor_uruk': {'Athletics': 5, 'Riding': -3, 'OneHanded': 5, 'TwoHanded': 6, 'Polearm': 5, 'Bow': 0, 'Crossbow': 0, 'Throwing': 3},
    'dolguldur': {'Athletics': 6, 'Riding': -3, 'OneHanded': 5, 'TwoHanded': 6, 'Polearm': 6, 'Bow': -3, 'Crossbow': -3, 'Throwing': 5},
    # Black Numenoreans (mordor_num_*), Men inside the mordor file, routed by detect_culture.
    'mordor_numenorean': {'Athletics': 3, 'Riding': 3, 'OneHanded': 5, 'TwoHanded': 5, 'Polearm': 5, 'Bow': 0, 'Crossbow': 0, 'Throwing': 0},
    'mordor': {'Athletics': -3, 'Riding': -3, 'OneHanded': 0, 'TwoHanded': 0, 'Polearm': -3, 'Bow': -3, 'Crossbow': -3, 'Throwing': 3},
    'gundabad': {'Athletics': 2, 'Riding': -3, 'OneHanded': 0, 'TwoHanded': 0, 'Polearm': 2, 'Bow': -5, 'Crossbow': -5, 'Throwing': 3},
    # Goblin-town: the weakest orc melee, and dangerous archers.
    'goblin': {'Athletics': -5, 'Riding': -8, 'OneHanded': -4, 'TwoHanded': -3, 'Polearm': -4, 'Bow': 8, 'Crossbow': -8, 'Throwing': -3},
    'mistymountainorcs': {'Athletics': 2, 'Riding': -3, 'OneHanded': 0, 'TwoHanded': 0, 'Polearm': 2, 'Bow': -5, 'Crossbow': -5, 'Throwing': 3},
    'feral_orcs': {'Athletics': -6, 'Riding': -3, 'OneHanded': -3, 'TwoHanded': 0, 'Polearm': -6, 'Bow': -3, 'Crossbow': -3, 'Throwing': 3},
}


class MissingCultureRow(KeyError):
    """A troop's culture has no CULTURE_PCT row."""


def round5(x):
    """To the nearest 5, a half rounding up. Exact on a Fraction, so no float lands on the wrong side."""
    return int(math.floor(Fraction(x) / 5 + Fraction(1, 2))) * 5


def floor5(x):
    return int(math.floor(Fraction(x) / 5)) * 5


def culture_row(culture):
    row = CULTURE_PCT.get(culture)
    if row is None:
        raise MissingCultureRow(f"culture {culture!r} has no CULTURE_PCT row in tools/rebalance_troops.py")
    return row


def detect_culture(troop_id, filename_culture):
    """Detect the actual culture from troop ID, handling Iron Hills in erebor file."""
    if 'iron_hills' in troop_id or troop_id.startswith('iron_hills'):
        return 'iron_hills'
    # troops_rhun_new.xml is Rhun's real roster; map it to the 'rhun' modifier key so
    # the easterling cavalry/pike deltas actually apply (the filename derives 'rhun_new').
    if filename_culture == 'rhun_new':
        return 'rhun'
    # troops_gundabad_snow.xml is Gundabad's Snow Orc tree (tools/generate_snow_orc_troops.py);
    # it takes Gundabad's modifier, not a zero one under the 'gundabad_snow' filename.
    if filename_culture == 'gundabad_snow':
        return 'gundabad'
    # Mordor Black Uruks (mordor_uruk_*) are an elite line inside the mordor file — route them
    # to their own elite modifier so they aren't dragged down to the weak Mordor-orc curve.
    if troop_id.startswith('mordor_uruk'):
        return 'mordor_uruk'
    # The Black Numenoreans are Men; the plain orc row would put them under every mannish line.
    if troop_id.startswith('mordor_num'):
        return 'mordor_numenorean'
    # Orthanc guard (orthanc_*) is Isengard's elite line inside the isengard file — route to its
    # own elite modifier so Saruman's best out-class the regular uruk-hai.
    if troop_id.startswith('orthanc'):
        return 'isengard_orthanc'
    return filename_culture


def troop_weapon_classes(npc_elem, item_classes):
    """Set of skill classes ('OneHanded'/'TwoHanded'/'Polearm'/'Bow'/'Crossbow'/
    'Throwing'/'Arrows'/'Bolts'/'Shield') the troop carries into battle, from the
    weapon slots (Item0..Item3) of its battle sets. A civilian set (flagged on the
    roster or an inner set) never cross-draws with a battle set, so a weapon only
    there does not count: imladris_recruit holds a bow in a civilian roster and
    fights with a sword. The same sets ranged_ladder.load_ranged_troops reads
    (rl.battle_sets), so the curve and the ranged ladder class a troop alike (#617)."""
    classes = set()
    for es in rl.battle_sets(npc_elem):
        for eq in es.findall('equipment'):
            if (eq.get('slot') or '').startswith('Item'):
                skill = item_classes.get((eq.get('id') or '').replace('Item.', '', 1))
                if skill:
                    classes.add(skill)
    return classes


def troop_battle_rows(npc_elem):
    """([(roster ordinal among ALL the troop's EquipmentRoster elements, {slot: item id})] for each
    battle roster, the slots that a troop-level <equipment> row fills). Troop-level rows lie over
    every roster, an empty id clearing the slot, as _battle_kits in the validator reads them."""
    def item(eq):
        return (eq.get('id') or '').replace('Item.', '', 1)
    rosters = list(npc_elem.iter('EquipmentRoster'))
    inside = {id(eq) for r in rosters for eq in r.iter('equipment')}
    loose = {eq.get('slot'): item(eq) for eq in npc_elem.iter('equipment') if id(eq) not in inside}
    rows = []
    for ordinal, roster in enumerate(rosters):
        if rl.is_civilian(roster):
            continue
        row = {eq.get('slot'): item(eq) for eq in roster.findall('equipment')}
        row.update(loose)
        rows.append((ordinal, row))
    return rows, set(loose)


def troop_battle_sets(npc_elem, item_classes):
    """[(roster ordinal among ALL the troop's EquipmentRoster elements, {slot: class})] for each
    battle roster, troop-level <equipment> rows laid over every roster (troop_battle_rows)."""
    return [(ordinal, {s: item_classes.get(i) for s, i in row.items() if s in tp.WEAPON_SLOTS})
            for ordinal, row in troop_battle_rows(npc_elem)[0]]


def troop_kit(npc_elem, item_classes, fix_item0=False, couch_ids=None):
    """(troop_purpose.Kit, item0 swaps); see troop_kit_plan."""
    kit, swaps, _ = troop_kit_plan(npc_elem, item_classes, fix_item0, couch_ids)
    return kit, swaps


def troop_kit_plan(npc_elem, item_classes, fix_item0=False, couch_ids=None):
    """(troop_purpose.Kit, swaps, detail) from the troop's battle sets (troop_battle_rows).

    swaps lists (roster ordinal among ALL the troop's EquipmentRoster elements, slot) for each
    battle set whose Item0 is a shield or ammunition beside a weapon
    (troop_purpose P2); apply_item0_swaps exchanges the two items. With fix_item0 the kit is read
    as if the swaps were made, which is what --fix-purpose writes; otherwise it is read as on disk
    and no swap is planned.

    With couch_ids (the item ids whose weapon has a couch usage) the main weapon rule runs too
    (troop_purpose P4, --main-weapon-slots) on the sets as P2 leaves them: its exchanges follow the P2
    ones in swaps, and detail holds the plan with each battle roster's item ids before and after.
    detail is None without couch_ids."""
    rows, loose = troop_battle_rows(npc_elem)

    def classes_of(row):
        return {s: item_classes.get(i) for s, i in row.items() if s in tp.WEAPON_SLOTS}

    if not fix_item0:
        return tp.kit_of(classes_of(row) for _, row in rows), [], None
    plan, p2, ids = tp.troop_main_weapon(
        [row for _, row in rows], item_classes, couch_ids, npc_elem.get('default_group', 'Infantry'),
        (get_display_name(npc_elem.get('name', '')), npc_elem.get('id', '')))
    swaps = [(ordinal, other) for (ordinal, _), other in zip(rows, p2) if other]
    if couch_ids is None:
        return tp.kit_of(classes_of(row) for row in ids), swaps, None
    # apply_item0_swaps edits ids inside a roster; a weapon row shared by every roster has none to edit.
    if plan.swaps and any(s in loose for s in ('Item0',) + tuple(slot for _, slot in plan.swaps)):
        plan = tp.SlotPlan(left="the exchange would move a troop-level row, which no roster holds")
    after = [dict(row) for row in ids]
    for index, slot in plan.swaps:
        after[index]['Item0'], after[index][slot] = after[index][slot], after[index]['Item0']
    order = tp.WEAPON_SLOTS
    detail = {
        'plan': plan,
        'before': [(o, {s: row[s] for s in order if row.get(s)}) for (o, _), row in zip(rows, ids)],
        'after': [(o, {s: row[s] for s in order if row.get(s)}) for (o, _), row in zip(rows, after)],
    }
    return (tp.kit_of(classes_of(row) for row in after),
            swaps + [(rows[i][0], slot) for i, slot in plan.swaps], detail)


def load_couchable_ids(game_modules):
    """The item ids that can be couched: taom_schema.couchable_item_ids, the source the validator
    reads too. It prices the Armory, vanilla (SandBoxCore and SandBox) and this repo's placeholders; it
    does not read the other installed modules, whose multiplayer lances no troop carries."""
    return ts.couchable_item_ids(Path(game_modules), Path(MODULEDATA_DIR) / ts.PLACEHOLDER_DIR)


_EQUIPMENT_ELEM_RE = re.compile(r'<equipment\b[^>]*?/>', re.S)
_ROSTER_ELEM_RE = re.compile(r'<EquipmentRoster\b[^>]*?(?:/>|>.*?</EquipmentRoster>)', re.S)


def _masked(text):
    """text with every comment blanked to spaces of the same length, so offsets still match."""
    return _XML_COMMENT_RE.sub(lambda m: re.sub(r'[^\n]', ' ', m.group(0)), text)


def apply_item0_swaps(filepath, swaps_by_troop):
    """Exchange the item ids of Item0 and the named slot in the named battle rosters, byte-faithfully.

    swaps_by_troop: troop id -> [(roster ordinal, slot)] from troop_kit. Only the two id values
    change, so each item keeps its line and the set keeps every other slot."""
    raw = Path(filepath).read_bytes()
    bom = raw.startswith(codecs.BOM_UTF8)
    content = raw.decode('utf-8-sig' if bom else 'utf-8')
    original = content
    for troop_id, swaps in swaps_by_troop.items():
        span = _npc_block_span(content, troop_id)
        if span is None:
            raise RuntimeError(f"{troop_id} not found in {os.path.basename(filepath)}")
        lo, hi = span
        block = content[lo:hi]
        for ordinal, other in swaps:
            masked = _masked(block)   # re-read per swap: two ids of unequal length move every offset
            r = list(_ROSTER_ELEM_RE.finditer(masked))[ordinal]
            ids = {}
            for em in _EQUIPMENT_ELEM_RE.finditer(masked, r.start(), r.end()):
                sm = re.search(r'\bslot="([^"]*)"', em.group(0))
                im = re.search(r'\bid="([^"]*)"', em.group(0))
                if sm and im and sm.group(1) in ('Item0', other):
                    ids[sm.group(1)] = (em.start() + im.start(1), em.start() + im.end(1))
            if set(ids) != {'Item0', other}:
                raise RuntimeError(f"{troop_id}: roster {ordinal} has no Item0 and {other} to exchange")
            (a0, a1), (b0, b1) = sorted(ids.values())
            first, second = block[a0:a1], block[b0:b1]
            block = block[:a0] + second + block[a1:b0] + first + block[b1:]
        content = content[:lo] + block + content[hi:]
    if content == original:
        return
    try:
        ET.fromstring(content.encode('utf-8'))
    except ET.ParseError as exc:
        raise RuntimeError(f"{os.path.basename(filepath)} would no longer parse after the Item0 "
                           f"exchange, so nothing was written: {exc}")
    out = content.encode('utf-8')
    with open(filepath, 'wb') as f:
        f.write((codecs.BOM_UTF8 if bom else b'') + out)


# A culture binds its militia in one of two encodings: a plain attribute in taom_spcultures.xml
# (militia_troop / melee_militia_troop / ranged_militia_troop / *_elite_militia_troop) and an
# <xsl:attribute name="..."> element in spcultures.xslt. Dale and Rhun use only the second, so
# both shapes have to be matched or those cultures silently fall off the militia rule.
MILITIA_BINDING_FILES = ('taom_spcultures.xml', 'spcultures.xslt')
# The validator's reader, not a copy: two copies drifted on quote style (#617 first review). The
# leading (?<![A-Za-z0-9_]) stops a longer attribute that merely ENDS in militia_troop (say a
# hypothetical reserve_melee_militia_troop) from being read as a militia binding.
# Group 1 is the `elite_` marker (empty for a basic slot), group 2 the troop id.
MILITIA_BINDING_RE = ts.Validator._MILITIA_BINDING_RE
# Militia take the tier 4 row whatever their level (siege and village defenders by design), and
# the elite (veteran) slot stands MILITIA_ELITE_BONUS above the basic one on every skill, so the
# two are not the same troop on the party screen.
MILITIA_TIER = 4
MILITIA_ELITE_BONUS = 10
_XML_COMMENT_RE = re.compile(r'<!--.*?-->', re.S)


def _strip_xml_comments(text):
    """Blank out comment bodies, keeping newlines so any line numbers stay accurate."""
    return _XML_COMMENT_RE.sub(lambda m: '\n' * m.group(0).count('\n'), text)

_militia_ids_cache = {}
_elite_militia_ids_cache = {}


def militia_troop_ids(moduledata_dir=None):
    """Troop ids a culture actually binds to a militia slot.

    This replaces a name-substring heuristic ('militia' plus spearman/archer/veteran) that had
    exactly one false positive across 871 troops: gondor_ano_archer_militia, a level-11 Anorien
    LINE troop that the heuristic handed the level-21 militia baseline. It then out-statted its
    own level-16 upgrade target on all 8 skills, -145 total, the worst upgrade edge in the game.
    Same defect family as the name-based weapon detection replaced in #340/#341: the name is a
    label, the binding is the fact.
    """
    root = os.path.abspath(moduledata_dir or MODULEDATA_DIR)
    # Both caches are filled together below; a basic entry without its elite twin (something
    # filled the basic cache directly) is re-read rather than trusted.
    if root in _militia_ids_cache and root in _elite_militia_ids_cache:
        return _militia_ids_cache[root]
    ids = set()
    elite = set()
    missing = []
    for filename in MILITIA_BINDING_FILES:
        path = os.path.join(root, filename)
        if not os.path.isfile(path):
            missing.append(filename)
            continue
        with open(path, 'r', encoding='utf-8-sig', errors='replace') as f:
            # Mask comments first: a commented-out <Culture> block is not a live binding, and
            # counting one would silently widen the militia exemption.
            for marker, tid in MILITIA_BINDING_RE.findall(_strip_xml_comments(f.read())):
                ids.add(tid)
                if marker:
                    elite.add(tid)
    # FAIL CLOSED. This decision moved from a self-contained name heuristic (which could not fail)
    # to a read of two external files. If that read comes back empty the tool would classify all 60
    # militia as ordinary troops and a --apply would cut them to their level curve, roughly 55%,
    # exiting 0 with one easily-missed line of output.
    if missing or not ids:
        raise RuntimeError(
            "Militia bindings could not be read, so every militia would be restatted as an "
            f"ordinary troop. Missing: {', '.join(missing) or 'none'}; ids found: {len(ids)}. "
            f"Expected {', '.join(MILITIA_BINDING_FILES)} under {root}.")
    _militia_ids_cache[root] = ids
    _elite_militia_ids_cache[root] = elite
    return ids


def elite_militia_troop_ids(moduledata_dir=None):
    """The subset of militia_troop_ids a culture binds to an `*elite_militia_troop` slot."""
    root = os.path.abspath(moduledata_dir or MODULEDATA_DIR)
    if root not in _elite_militia_ids_cache:
        militia_troop_ids(moduledata_dir)
    return _elite_militia_ids_cache[root]


def is_elite_militia(troop_id):
    return troop_id in elite_militia_troop_ids()


def is_militia(troop_id, troop_name=None):
    """True when a culture binds this troop to one of its militia slots.

    troop_name is accepted and ignored; it is what the old heuristic keyed off and callers still
    pass it.
    """
    return troop_id in militia_troop_ids()


_LADDER_SPEC = {}


def _ladder_spec():
    """tools/ranged_ladders.json, validated once. A writer must not quietly fall back to the level
    curve for archers when the ladder cannot be read, so a broken spec stops the run."""
    if "spec" not in _LADDER_SPEC:
        spec = rl.load_spec()
        problems = rl.validate_spec(spec)
        if problems:
            raise RuntimeError("tools/ranged_ladders.json contradicts itself, so no archer's Bow or "
                               "Crossbow can be computed: " + "; ".join(problems[:4]))
        _LADDER_SPEC["spec"] = spec
    return _LADDER_SPEC["spec"]



def undefined_ladder_ids(troop_files, item_classes):
    """Ranged ladder ids the rosters name that no loaded item file defines. Such a slot has no
    class, so the troop would get the level-curve Bow and lose the Bow/Crossbow swap: the window
    between generate_ranged_ladder_items.py retiring ids and rebalance_ranged_ladders.py
    repointing them, or an Armory reinstall. Read as XML, as the engine reads it: any quote style,
    comments dropped, every set (a civilian slot naming no item is a broken ref too)."""
    ref = 'Item.' + rl.ID_PREFIX
    found = set()
    for path in troop_files:
        for el in ET.parse(path).iter():
            iid = el.get('id') or ''
            if iid.startswith(ref):
                found.add(iid[len('Item.'):])
    return sorted(i for i in found if i not in item_classes)


def ladder_cells(troop_id, filename_culture, level, battle_classes):
    """{Bow|Crossbow: cell} for a ladder troop (#617): the value tools/rebalance_ranged_ladders.py
    writes, so a full rebaseline never undoes the ranking. Only the classes the troop carries in
    battle, only at a tier its line lists; militia included (the ladder ranks them by their own
    tier, not by the militia row). The clamp refuses to raise a value named here."""
    if not battle_classes:
        return {}
    spec = _ladder_spec()
    line = rl.line_for(troop_id, filename_culture, spec)
    if line is None:
        return {}
    tier = rl.engine_tier(level)
    return {cls: rl.skill_cell(line, tier, spec) for cls in rl.CLASSES
            if cls in battle_classes and tier in rl.tiers_for(line, cls, spec)}


def troop_mount(npc_elem, mounts=None):
    """(mounted, the highest difficulty of its horses). Mounted as the ranged ladder reads it
    (rl.load_ranged_troops): a Horse slot in any battle set, or a Cavalry or HorseArcher group.
    mounts: horse item id -> difficulty (taom_schema.build_mount_difficulty_registry); a horse it
    does not name counts 0."""
    rosters = list(rl.battle_sets(npc_elem))
    inside = {id(eq) for r in npc_elem.iter('EquipmentRoster') for eq in r.iter('equipment')}
    inside |= {id(eq) for r in npc_elem.iter('EquipmentSet') for eq in r.iter('equipment')}
    loose = [eq for eq in npc_elem.iter('equipment') if id(eq) not in inside]
    horses = [(eq.get('id') or '').replace('Item.', '', 1).strip()
              for es in rosters for eq in es.findall('equipment') if eq.get('slot') == 'Horse']
    horses += [(eq.get('id') or '').replace('Item.', '', 1).strip() for eq in loose if eq.get('slot') == 'Horse']
    horses = [h for h in horses if h]
    mounted = bool(horses) or npc_elem.get('default_group', '') in ('Cavalry', 'HorseArcher')
    return mounted, max([(mounts or {}).get(h, 0) for h in horses] + [0])


def calculate_skills(culture, level, troop_id, kit, ladder=None, mounted=False, mount_difficulty=0,
                     militia=None, elite_militia=None):
    """The role table (#343) for one troop, or None when its kit holds no weapon.

    kit: the troop's troop_purpose.Kit (troop_kit). ladder: {Bow|Crossbow: cell} (ladder_cells),
    which the troop takes as is. militia and elite_militia default to the culture bindings. Values
    round to the nearest 5; an archer's melee rounds down so it stays under its launcher."""
    weapons = [c for c in tp.WEAPON_CLASSES if kit is not None and kit.counts.get(c)]
    if not weapons:
        return None
    pct = culture_row(culture)
    ladder = dict(ladder or {})
    if militia is None:
        militia = is_militia(troop_id)
    if elite_militia is None:
        elite_militia = militia and is_elite_militia(troop_id)
    n = TIER_CURVE[MILITIA_TIER if militia else max(1, rl.engine_tier(level))]

    def share(skill, part=1):
        return round5(n * part * (1 + Fraction(pct[skill], 100)))

    main = tp.main_class(kit, {c: share(c) for c in tp.WEAPON_CLASSES})
    if main is None:   # no weapon in Item0: the class most sets carry
        main = max(weapons, key=lambda c: (kit.counts[c], share(c), -tp.WEAPON_CLASSES.index(c)))
    s = {'Athletics': share('Athletics', MOUNTED_ATHLETICS if mounted else 1),
         'Riding': share('Riding') if mounted else round5(n * FOOT_RIDING)}
    for c in tp.WEAPON_CLASSES:
        if c == main:
            s[c] = share(c)
        elif c in weapons:
            s[c] = share(c, SIDEARM)
        else:
            s[c] = round5(n * (UNCARRIED_MELEE if c in tp.MELEE else UNCARRIED_RANGED))
    if troop_id in THROWN_PRIMARY_TROOP_IDS and tp.THROWING in weapons:
        s[tp.THROWING] = share(tp.THROWING)
    if elite_militia:
        s = {k: v + MILITIA_ELITE_BONUS for k, v in s.items()}
    s.update(ladder)
    if main in tp.LAUNCHERS:
        # The archer's own launcher value decides the rest, so nothing else tops it.
        top = s[main]
        sidearm = floor5(top * ARCHER_SIDEARM)
        for c in tp.MELEE + (tp.THROWING,):
            if c in weapons:
                s[c] = sidearm
            elif c in tp.MELEE:
                s[c] = min(s[c], sidearm)
        for c in tp.LAUNCHERS:
            if c != main and c not in ladder:
                s[c] = min(s[c], top)
    if mounted:
        s['Riding'] = max(s['Riding'], mount_difficulty)
    pinned = set(ladder)
    s = tp.purpose_skills(s, kit, pinned=pinned)
    main = tp.main_class(kit, s) or main
    if main not in pinned:
        s[main] = max(s[c] for c in (main,) + tp.MELEE + tp.LAUNCHERS)
    return tp.tie_raise(s, kit, pinned=pinned)


def get_display_name(name_attr):
    """Extract display name from localization tag like {=tag}Display Name."""
    if '}' in name_attr:
        return name_attr.split('}', 1)[1]
    return name_attr


SKILL_ENTRY_RE = re.compile(r'(?ms)^([ \t]*)(<skill\b.*?/>)')


def insert_missing_skill_entries(skills_block, new_skills, troop_id):
    """Add a <skill> element for any of the 8 skills the block does not already declare.

    CharacterObject.GetSkillValue returns 0 for an undeclared skill, so a partial block is not
    "leave it alone", it is a silent zero. 34 Mordor and Morannon troops shipped that way and the
    value-only writer could never repair them: it rewrites values already present, which is why
    they reported CHANGED every run and produced no byte change.

    The new entries are cloned from the last existing entry in the same block, so a file using the
    three-line <skill/id/value> shape keeps it and a file using the one-line shape keeps that.
    """
    present = set(re.findall(r'<skill\s[^>]*?id="([A-Za-z]+)"', skills_block, re.DOTALL))
    missing = [s for s in SKILL_NAMES if s not in present]
    if not missing:
        return skills_block

    entries = list(SKILL_ENTRY_RE.finditer(skills_block))
    if not entries:
        print(f"  WARNING: {troop_id} has no parseable <skill> entry to clone; "
              f"leaving {', '.join(missing)} undeclared")
        return skills_block

    indent, template = entries[-1].group(1), entries[-1].group(2)
    addition = ''
    for skill_id in missing:
        entry = re.sub(r'id="[A-Za-z]+"', f'id="{skill_id}"', template, count=1)
        entry = re.sub(r'value="\d+"', f'value="{new_skills.get(skill_id, 0)}"', entry, count=1)
        addition += '\n' + indent + entry
    at = entries[-1].end()
    return skills_block[:at] + addition + skills_block[at:]


def _npc_block_span(content, troop_id):
    """(start, end) of the NPCCharacter element whose id attribute is exactly troop_id.

    Anchoring on the element rather than on a bare id="..." match keeps a troop's rewrite inside
    its own block, and keeps an EquipmentRoster or EquipmentSet that happens to share an id from
    being mistaken for the character.
    """
    for m in re.finditer(r'<NPCCharacter\b[^>]*(?:/>|>.*?</NPCCharacter>)', content, re.S):
        head = m.group(0)[:m.group(0).find('>') + 1]
        idm = re.search(r'\bid="([^"]*)"', head)
        if idm and idm.group(1) == troop_id:
            return m.start(), m.end()
    return None


def _render_skills_body(new_skills, indent):
    """The full 8-skill body used when a troop has no usable <skill> entry to clone."""
    skill_indent = indent + '    '
    lines = []
    for skill_id in SKILL_NAMES:
        lines.append(
            f'{skill_indent}<skill\n'
            f'{skill_indent}    id="{skill_id}"\n'
            f'{skill_indent}    value="{new_skills.get(skill_id, 0)}" />'
        )
    return '\n' + '\n'.join(lines) + '\n' + indent


def apply_skills_via_regex(filepath, troop_skill_map):
    """Apply skill changes to an XML file, preserving all formatting.

    troop_skill_map: troop_id -> {skill_id: value, ...}
    """
    # Byte-faithful I/O per .claude/rules/moduledata-validation.md: read binary and decode here so
    # the BOM (4 troop files carry one) and the file's newline style both survive. A plain text
    # read plus a text write is the forbidden mixed shape: it strips the BOM and rewrites an
    # LF-only file entirely as CRLF on Windows.
    raw = open(filepath, 'rb').read()
    bom = raw.startswith(codecs.BOM_UTF8)
    content = raw.decode('utf-8-sig' if bom else 'utf-8')
    newline = '\r\n' if '\r\n' in content else '\n'
    content = content.replace('\r\n', '\n')
    original = content

    for troop_id, new_skills in troop_skill_map.items():
        # Isolate this troop's own NPCCharacter block FIRST. The old pattern ran from
        # id="<troop>" to the next <skills> ANYWHERE in the file, so a troop with a self-closing
        # <skills /> or none at all reached past </NPCCharacter> and wrote its values into the
        # next troop while leaving itself untouched. Bounding the search makes that
        # unrepresentable rather than merely unobserved.
        span = _npc_block_span(content, troop_id)
        if span is None:
            print(f"  WARNING: {troop_id} not found in {os.path.basename(filepath)}; skipped")
            continue
        lo, hi = span
        block = content[lo:hi]

        indent_match = re.search(r'\n([ \t]*)<skills\b', block)
        indent = indent_match.group(1) if indent_match else '        '

        match = re.search(r'(<skills>)(.*?)(</skills>)', block, re.DOTALL)
        if not match:
            # Self-closing <skills /> : expand it to a full block.
            self_close = re.search(r'<skills\s*/>', block)
            if not self_close:
                print(f"  WARNING: {troop_id} has no <skills> element; skipped")
                continue
            replacement = '<skills>' + _render_skills_body(new_skills, indent) + '</skills>'
            block = block[:self_close.start()] + replacement + block[self_close.end():]
        elif not match.group(2).strip():
            # Present but empty: <skills></skills>
            block = block[:match.start(2)] + _render_skills_body(new_skills, indent) + block[match.end(2):]
        else:
            skills_block = match.group(2)
            for skill_id, value in sorted(new_skills.items()):
                skills_block = re.sub(
                    r'(id="' + re.escape(skill_id) + r'"\s+value=")(\d+)(")',
                    lambda m, v=value: m.group(1) + str(v) + m.group(3),
                    skills_block)
                # Also handle value before id: value="X" id="SkillName"
                skills_block = re.sub(
                    r'(value=")(\d+)("\s+id="' + re.escape(skill_id) + r'")',
                    lambda m, v=value: m.group(1) + str(v) + m.group(3),
                    skills_block)
            skills_block = insert_missing_skill_entries(skills_block, new_skills, troop_id)
            block = block[:match.start(2)] + skills_block + block[match.end(2):]

        content = content[:lo] + block + content[hi:]

    if content == original:
        return

    # Parse before writing. Every replacement above is regex-driven, so a malformed result would
    # otherwise reach disk and only surface as a load failure in game.
    try:
        ET.fromstring(content.encode('utf-8'))
    except ET.ParseError as exc:
        raise RuntimeError(
            f"{os.path.basename(filepath)} would no longer be well-formed XML after the skill "
            f"rewrite, so nothing was written: {exc}")

    out = content.replace('\n', newline).encode('utf-8')
    if bom:
        out = codecs.BOM_UTF8 + out
    with open(filepath, 'wb') as f:
        f.write(out)


def clamp_upgrade_monotonicity(all_changes, base_on_curve=True, restat_ids=()):
    """Raise any upgrade target that would come out worse than the troop it upgrades from.

    base_on_curve=True is the rebaseline path: start from the formula result, so --apply both
    rebaselines and clamps. base_on_curve=False is the --fix-monotonicity path: start from what is
    on disk, so a run repairs the ladder WITHOUT rebaselining the roster: a ladder repair moves only
    the values an edge needs, and a troop someone has just tuned by hand is left for --apply to
    judge. restat_ids names the troops that should take their formula value anyway, which is how a
    misclassified troop gets corrected on its own.

    A player reads the troop tree as a ladder, so an upgrade that lowers a stat reads as a bug no
    matter how it got there. The common cause is a respecialising edge: a swordsman upgrading to
    an archer keeps his parent's sword, which the role table rates under the archer's own.

    Runs over the whole roster at once because upgrade targets cross files. Troops the formula
    skipped (creatures, hand-tuned lines, troops with no weapon) take part using their current
    values: they can lift a child, and they can be lifted, but a clamp only ever raises, so a
    hand-tune is never reverted.

    Two exemptions. Militia to militia: militia take the tier 4 row regardless of level so
    village defence stays costly; the elite slot adds MILITIA_ELITE_BONUS on every
    skill, so a militia promotion is a fixed step, never a curve, and the clamp leaves it alone.
    And RESPECIALIZATION_EXEMPT_EDGES, per skill, for a child that drops a weapon its parent
    genuinely carried; without it the clamp puts back exactly what the writer just floored.
    """
    by_id = {c['id']: c for c in all_changes}
    restat_ids = set(restat_ids)
    base = {}
    for c in all_changes:
        curve = c.get('new')
        take_curve = curve is not None and (base_on_curve or c['id'] in restat_ids)
        if take_curve:
            base[c['id']] = dict(curve)
        else:
            # An undeclared skill seeds at 0, which is exactly what the engine already reads for
            # it, and the clamp then raises it to whatever a parent requires. Seeding from the
            # curve instead would smuggle a rebaseline into the mode whose whole contract is not
            # to rebaseline: it put 669 points into skills no parent asked for, including two
            # troops that sit on no upgrade edge at all. Everything already on disk is untouched.
            base[c['id']] = {s: c['old'].get(s, 0) for s in SKILL_NAMES}

    edges = [(c['id'], t) for c in all_changes for t in c.get('upgrades', []) if t in by_id]

    indegree = defaultdict(int)
    for _, child in edges:
        indegree[child] += 1
    children = defaultdict(list)
    for parent, child in edges:
        children[parent].append(child)

    queue = [i for i in base if indegree[i] == 0]
    order = []
    while queue:
        node = queue.pop()
        order.append(node)
        for child in children[node]:
            indegree[child] -= 1
            if indegree[child] == 0:
                queue.append(child)
    if len(order) != len(base):
        cyclic = sorted(set(base) - set(order))
        raise RuntimeError(
            "The upgrade graph is not acyclic, so there is no well-defined order to clamp in. "
            f"Troops in or below a cycle: {', '.join(cyclic[:10])}")

    # Snapshot before clamping so "raised" counts DISTINCT (troop, skill) pairs that ended up
    # higher. Counting each assignment instead made the number depend on the order parents happen
    # to be processed in: a child with two parents at 30 and 50 reported either 8 or 16 raises for
    # the identical final result.
    pre_clamp = {tid: dict(vals) for tid, vals in base.items()}
    for parent in order:
        for child in children[parent]:
            if is_militia(parent) and is_militia(child):
                continue
            exempt = RESPECIALIZATION_EXEMPT_EDGES.get((parent, child), ())
            for skill in SKILL_NAMES:
                if skill in exempt:
                    continue
                if base[child][skill] < base[parent][skill]:
                    base[child][skill] = base[parent][skill]
    raised = sum(1 for tid, vals in base.items() for s in SKILL_NAMES
                 if vals[s] > pre_clamp[tid][s])
    # A ladder troop's Bow or Crossbow is its cell; lifting it off the cell writes a value the
    # ladder tool and the RANGED_LADDER_INVERSION gate both call wrong, and the next ladder run
    # would put it back (#617 review). ranged_ladder.planned_skill_edits refuses the same edge.
    lifted = [f"{c['id']} {s} {pre_clamp[c['id']][s]} -> {base[c['id']][s]}"
              for c in all_changes for s in (c.get('ladder') or {})
              if base[c['id']][s] > pre_clamp[c['id']][s]]
    if lifted:
        raise RuntimeError(
            "The clamp would raise a ranged ladder troop off its cell (an upgrade source carries "
            "more Bow/Crossbow than the troop's tier cell): " + "; ".join(lifted[:8]) +
            ". Rank or tier the lines in tools/ranged_ladders.json so the cell is at least the source's.")
    # A templated character plays with its SkillSet, the inline rows laid over it (#626). A raised
    # inline row would differ from the SkillSet, which SKILL_TEMPLATE_MISMATCH refuses, so the clamp
    # refuses first. ranged_ladder.planned_skill_edits refuses the same.
    from_template = [f"{c['id']} {s} {pre_clamp[c['id']][s]} -> {base[c['id']][s]} ({c['template']})"
                     for c in all_changes if c.get('template') for s in SKILL_NAMES
                     if base[c['id']][s] > pre_clamp[c['id']][s]]
    if from_template:
        raise RuntimeError(
            "The clamp would raise a character whose skills come from a skill_template: "
            + "; ".join(from_template[:8]) + ". An inline row that differs from the SkillSet is "
            "SKILL_TEMPLATE_MISMATCH, so raise the SkillSet itself or drop the template. "
            "Nothing was written.")

    # Report against the FINAL values, not the raw curve. A clamped troop sits above the curve on
    # purpose and forever, so scoring it against the curve would print it as CHANGED on every run
    # while producing no byte change -- exactly the misleading residual the partial <skills> blocks
    # used to produce, and the reason nobody looked at them for months.
    for c in all_changes:
        final = base[c['id']]
        c['final'] = final
        # "clamped" means this troop was actually RAISED off its own values by a parent. Comparing
        # against the curve instead reported a restatted-then-clamped troop as unclamped, and
        # reported insert-only troops as clamped.
        c['clamped'] = any(final[s] > c['old'].get(s, 0) for s in SKILL_NAMES)
        c['inserted'] = [s for s in SKILL_NAMES if s not in c['old']]
        needs_write = (any(c['old'].get(s, 0) != final[s] for s in SKILL_NAMES)
                       or bool(c['inserted']))
        if c.get('external'):
            pass  # read-only source; its status stays as-is and it is never written
        elif c.get('new') is None:
            if needs_write and not c['status'].endswith(' + CLAMPED'):   # settle_purpose clamps again
                c['status'] += ' + CLAMPED'
        else:
            c['status'] = 'CHANGED' if needs_write else 'UNCHANGED'
        c['total_new'] = sum(final[s] for s in SKILL_NAMES)
        c['total_old'] = sum(c['old'].get(s, 0) for s in SKILL_NAMES)
        c['delta'] = c['total_new'] - c['total_old']
    return raised


def settle_purpose(all_changes, base_on_curve=True, restat_ids=(), max_rounds=50):
    """Clamp the roster, then restore the purpose order on every troop the clamp disturbed, until
    nothing moves. Both steps only raise, so this ends. Every mode runs it: base_on_curve and
    restat_ids pick the base as clamp_upgrade_monotonicity does, so in the clamp-only mode a troop
    starts from its on-disk values and only a troop the clamp moved is raised.

    The clamp hands a child every skill its parent has, so on a respecialising edge (a swordsman
    upgrading to a two-hander) the child's main class can end up under a skill it inherited. The
    main class is then raised to that value, a tie, rather than exempting the edge the way
    RESPECIALIZATION_EXEMPT_EDGES exempts Bow and Crossbow: an exemption would let an upgrade
    lower a stat, which the ladder rule forbids, and would have to be mirrored in two more gates.
    A ranged ladder cell is never raised (the ladder owns it).

    Records need 'new' (the base: None keeps 'old', as for a skipped troop), 'kit' and 'ladder'.
    Returns (log, raised). log holds the purpose raises as (source, troop, skill, before, after);
    source is the upgrade source that carries the raised value, or '' when the troop's own value
    was the higher one. raised counts the (troop, skill) values the clamp lifted above the base,
    which each record lists in 'clamp_lifted'; a purpose raise is not counted there."""
    restat_ids = set(restat_ids)
    if not base_on_curve:
        for c in all_changes:
            if c.get('new') is not None and not c.get('external') and c['id'] not in restat_ids:
                c['new'] = {s: c['old'].get(s, 0) for s in SKILL_NAMES}
    start = {c['id']: dict(c['new']) if c.get('new') is not None
             else {s: c['old'].get(s, 0) for s in SKILL_NAMES} for c in all_changes}
    sources = defaultdict(list)
    for c in all_changes:
        for t in c.get('upgrades', []):
            sources[t].append(c['id'])
    log = []
    for _ in range(max_rounds):
        clamp_upgrade_monotonicity(all_changes, base_on_curve=True)
        finals = {c['id']: c['final'] for c in all_changes}
        moved = False
        for c in all_changes:
            if c.get('new') is None or c.get('external') or c.get('kit') is None:
                continue
            final = c['final']
            if final == c['new']:
                continue   # the clamp left this troop alone
            up = tp.purpose_raise(final, c['new'], c['kit'], pinned=set(c.get('ladder') or {}))
            if up == final:
                continue
            for s in SKILL_NAMES:
                if up[s] != final[s]:
                    src = next((p for p in sorted(sources[c['id']]) if p in finals
                                and up[s] in finals[p].values()), '')
                    log.append((src, c['id'], s, final[s], up[s]))
            c['new'] = up
            moved = True
        if not moved:
            purpose = {(tid, s) for _, tid, s, _, _ in log}
            for c in all_changes:
                c['clamp_lifted'] = [s for s in SKILL_NAMES if c['final'][s] > start[c['id']][s]
                                     and (c['id'], s) not in purpose]
            return log, sum(len(c['clamp_lifted']) for c in all_changes)
    raise RuntimeError(f"the purpose order and the upgrade clamp did not settle in {max_rounds} rounds")


def plan_fix_purpose(all_changes):
    """--fix-purpose: the base is what is on disk, reordered to the kit (troop_purpose P3), with
    alternative classes raised to tie (the tie rule, logged in record['tie_raises'] as (skill,
    value after the reorder, new)), then the roster is settled. Nothing else rises except through the clamp. A main
    class under a skill of another kind (a launcher or Throwing under a melee skill) is not a
    reorder, and no number is borrowed for it: it is returned for a person to decide.

    Returns (raises from settle_purpose, [(record, finding, formula value)] still cross-kind)."""
    for c in all_changes:
        c['tie_raises'] = []
        if c.get('external') or c.get('no_skills') or c['id'] in SKIP_TROOP_IDS or c.get('kit') is None or (
                c.get('slots_only') and not (c.get('main_weapon') and c['main_weapon']['plan'].swaps)):
            c['formula'], c['new'] = c.get('new'), None
            continue
        c['formula'] = c.get('new')
        pinned = set(c.get('ladder') or {})
        ordered = tp.purpose_skills({s: c['old'].get(s, 0) for s in SKILL_NAMES}, c['kit'], pinned=pinned)
        new = tp.tie_raise(ordered, c['kit'], pinned=pinned)
        c['tie_raises'] = [(s, ordered[s], new[s]) for s in SKILL_NAMES if new[s] != ordered[s]]
        c['new'] = new
    log, _ = settle_purpose(all_changes)
    left = []
    for c in all_changes:
        if c.get('new') is None:
            continue
        for f in tp.kit_mismatches(c['final'], c['kit']):
            left.append((c, f, (c['formula'] or {}).get(f.cls)))
    return log, left


def _shield_swaps(c):
    """The P2 exchanges of a record: item0_swaps without the main weapon ones that follow them."""
    swaps = c.get('item0_swaps', [])
    main = len(c['main_weapon']['plan'].swaps) if c.get('main_weapon') else 0
    return swaps[:len(swaps) - main]


def _slot_order(row):
    return ' '.join(f"{slot[-1]}:{item}" for slot, item in row.items())


def print_main_weapon_report(all_changes):
    """--main-weapon-slots: every troop the rule changes (each battle roster's slot order before and
    after, and the skills), then every troop in scope that stays and why. Call it after the plan."""
    tracked = [c for c in all_changes if c.get('main_weapon')]
    planned = [c for c in tracked if c['main_weapon']['plan'].swaps]
    sets = sum(len(c['main_weapon']['plan'].swaps) for c in planned)
    print(f"\nMain weapon in Item0 (A: lance cavalry, B: named polearm troop): "
          f"{sets} battle sets on {len(planned)} troops")
    for c in planned:
        d = c['main_weapon']
        print(f"  {c['file']:<28} {c['id']:<36} rule {d['plan'].rule}  L{c['level']} {c['culture']}  {c['name']}")
        for (ordinal, before), (_, after) in zip(d['before'], d['after']):
            if before != after:
                print(f"      roster {ordinal}: {_slot_order(before)}  ->  {_slot_order(after)}")
        if c.get('final'):
            diff = ', '.join(f"{s} {c['old'].get(s, 0)}->{c['final'][s]}" for s in SKILL_NAMES
                             if c['old'].get(s, 0) != c['final'][s])
            print(f"      skills: {diff or 'unchanged'}")
    stays = [c for c in tracked if c['main_weapon']['plan'].left]
    print(f"\nIn scope of the rule and left as they are ({len(stays)} troops):")
    for c in stays:
        print(f"  {c['file']:<28} {c['id']:<36} {c['group']:<12} L{c['level']}  {c['main_weapon']['plan'].left}")


def print_purpose_report(all_changes, log, left):
    swaps = [c for c in all_changes if _shield_swaps(c)]
    print(f"\nItem0 holds a shield or ammunition beside a weapon: "
          f"{sum(len(_shield_swaps(c)) for c in swaps)} battle sets on {len(swaps)} troops")
    for c in swaps:
        print(f"  {c['id']:<40} " + ", ".join(f"roster {o}: Item0 <-> {s}" for o, s in _shield_swaps(c)))
    disputed = [c for c in all_changes if c.get('new') is not None and tp.main_disputed(c['kit'])]
    print(f"\nBattle sets disagree on the Item0 class ({len(disputed)} troops; the class most sets "
          f"hold is the main class, a tie goes to the higher skill):")
    for c in disputed:
        print(f"  {c['id']:<40} {c['kit'].item0} -> main {tp.main_class(c['kit'], c['final'])}")
    changed = [c for c in all_changes if c.get('new') is not None
               and any(c['old'].get(s, 0) != c['final'][s] for s in SKILL_NAMES)]
    print(f"\nTroops whose skills change: {len(changed)}")
    for c in changed:
        diff = ", ".join(f"{s} {c['old'].get(s, 0)}->{c['final'][s]}" for s in SKILL_NAMES
                         if c['old'].get(s, 0) != c['final'][s])
        print(f"  {c['file']:<28} {c['id']:<40} main {tp.main_class(c['kit'], c['final'])}: {diff}")
    ties = [(c, t) for c in all_changes for t in c.get('tie_raises', [])]
    print(f"\nRaised by the tie rule, alternative weapons of one role take the higher value "
          f"({len(ties)} values on {len({c['id'] for c, _ in ties})} troops):")
    for c, (s, a, b) in ties:
        print(f"  {c['file']:<28} {c['id']:<40} {s} {a} -> {b}")
    lifted = [c for c in all_changes if c.get('clamp_lifted')]
    print(f"\nThe clamp raised {sum(len(c['clamp_lifted']) for c in lifted)} values on {len(lifted)} "
          f"troops so no upgrade target sits below the troop it upgrades from")
    print(f"\nRaised after the upgrade clamp so the purpose order holds ({len(log)} values):")
    for src, tid, s, a, b in log:
        print(f"  {src or '(own value)':<36} -> {tid:<36} {s} {a} -> {b}")
    print(f"\nStill not purpose-correct ({len(left)}; values never cross kinds, a ladder cell is the "
          f"ladder's, and a thrower's Throwing keeps its value):")
    for c, f, value in left:
        note = ' (ladder cell)' if f.cls in (c.get('ladder') or {}) else ''
        if f.cls == tp.THROWING:
            note = (' (in THROWN_PRIMARY_TROOP_IDS)' if c['id'] in THROWN_PRIMARY_TROOP_IDS
                    else ' (not in THROWN_PRIMARY_TROOP_IDS)')
        print(f"  {c['file']:<28} {c['id']:<40} {f.text}; formula {f.cls} {value}{note}")


# The second troop root (troop_purpose.EXTRA_TROOP_FILES) lives under this folder; a test points it
# at a fixture.
EXTRA_ROOT_DIR = MODULEDATA_DIR
# A second-root troop named COPY_PREFIX + <id> is a copy of the campaign troop <id>.
COPY_PREFIX = 'fow_'


def extra_troop_paths():
    """The second-root files that exist (troop_purpose.EXTRA_TROOP_FILES under EXTRA_ROOT_DIR). A
    listed file that is missing is named, so a moved roster is not silently left off the curve."""
    paths = []
    for rel in tp.EXTRA_TROOP_FILES:
        path = os.path.join(EXTRA_ROOT_DIR, *rel.split('/'))
        if os.path.isfile(path):
            paths.append(path)
        else:
            print(f"  WARNING: second troop root {rel} is listed in troop_purpose.EXTRA_TROOP_FILES "
                  f"but not found under {EXTRA_ROOT_DIR}; it is not read")
    return paths


def bare_id(troop_id):
    """A copy's campaign source id: its id without COPY_PREFIX. The id lists this tool keys by troop
    id (SKIP_TROOP_IDS, the militia bindings, THROWN_PRIMARY_TROOP_IDS) name campaign troops."""
    return troop_id[len(COPY_PREFIX):] if troop_id.startswith(COPY_PREFIX) else troop_id


def file_culture(filename):
    """'troops_gondor.xml' -> 'gondor', the culture token process_file reads from a file name."""
    return filename.replace('troops_', '').replace('.xml', '')


def copy_sources(records):
    """What copy_source reads from the campaign records: each id, and for each culture attribute
    the troop file most campaign troops with that attribute live in."""
    files = defaultdict(Counter)
    for c in records:
        if c.get('culture_attr'):
            files[c['culture_attr']][file_culture(c['file'])] += 1
    return {'ids': {c['id']: c for c in records},
            'attrs': {attr: count.most_common(1)[0][0] for attr, count in files.items()}}


def copy_source(troop_id, culture_attr, sources):
    """(culture row, source id, ladder file token) for a second-root troop, or None outside one.

    A copy fights as the campaign troop it copies, so it takes that troop's routed culture, and the
    source id keys its ranged ladder line and the rest of its rules; its own level and kit set the
    rest. A copy whose id names no campaign troop
    takes the troop file its culture attribute points to (copy_sources), because the second root's
    file name says no culture."""
    if sources is None:
        return None
    bare = bare_id(troop_id)
    source = sources['ids'].get(bare)
    if source is not None:
        return source['culture'], source['id'], file_culture(source['file'])
    token = sources['attrs'].get(culture_attr)
    if token is None:
        return None
    return detect_culture(bare, token), bare, token


def templated_old(troop_id, template, inline, skill_sets):
    """A templated character's on-disk skills as the engine reads them: all eight, its SkillSet
    with the inline rows laid over it (sync_lord_inline_skills.effective_skills, #626). Such a
    character is read-only here: the clamp refuses to raise it (clamp_upgrade_monotonicity)."""
    if skill_sets is None:
        raise RuntimeError(
            f"{troop_id} names skill_template {template}, and its skills cannot be read without the "
            f"SkillSets; main() loads them from the install. Nothing was written.")
    eff = sl.effective_skills(template, inline, skill_sets)
    return {s: eff.get(s, 0) for s in SKILL_NAMES}


def process_file(filepath, item_classes=None, purpose=False, mounts=None, couch_ids=None,
                 slots_only=False, sources=None, skill_sets=None):
    """Process a single troop XML file. Returns list of change records.

    item_classes: item id -> skill class map from taom_schema.build_item_class_registry. The
    role table reads the kit from it, so without it no troop has a formula; main() always
    supplies it (hard-fails without the game install).
    mounts: horse item id -> difficulty (taom_schema.build_mount_difficulty_registry), the
    floor of a rider's Riding.
    purpose: the --fix-purpose read. The kit is read as if Item0 were fixed and the planned
    exchanges go in record['item0_swaps'].
    couch_ids: with purpose, also plan the main weapon exchanges (--main-weapon-slots).
    slots_only: a second-root file in a purpose mode: only a troop whose slots the rule changes
    has its skills reordered (plan_fix_purpose).
    sources: troop id -> campaign record, for a second-root file (troop_purpose.EXTRA_TROOP_FILES).
    A copy named fow_<id> follows that campaign troop's rules: its culture row and ranged ladder
    line, because the file's name says no culture and the copy fights as the troop it copies, and
    its exclusion, militia row, elite bonus and thrown-primary status; its own level and its own
    kit set the rest (copy_source)."""
    filename = os.path.basename(filepath)
    if filename in SKIP_FILES:
        return []

    filename_culture = filename.replace('troops_', '').replace('.xml', '')

    # Parse with ElementTree for reading only (never write with ET)
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

        # Upgrade targets feed the cross-file monotonicity clamp in main().
        upgrades = [(u.get('id') or '').replace('NPCCharacter.', '', 1)
                    for u in npc.findall('./upgrade_targets/upgrade_target')]
        upgrades = [u for u in upgrades if u]

        record = {
            'file': filename,
            'id': troop_id,
            'name': troop_name,
            'level': level,
            'group': group,
            'culture': culture,
            'old': old_skills,
            'upgrades': upgrades,
            'culture_attr': npc.get('culture', ''),
        }

        # A templated character is read as its SkillSet with the inline rows laid over it and is
        # never written (templated_old, #626), so it gets no curve and no kit plan.
        template = npc.get('skill_template')
        if template:
            record.update(old=templated_old(troop_id, template, old_skills, skill_sets),
                          template=template, new=None,
                          status=f'SKILLSET (read-only: skill_template {template})')
            changes.append(record)
            continue

        # A copy follows its campaign source's rule, so a copy of an excluded troop is excluded too.
        if (bare_id(troop_id) if sources is not None else troop_id) in SKIP_TROOP_IDS:
            record.update(status='SKIPPED (excluded: creature/hand-tuned)', new=None)
            changes.append(record)
            continue
        if sources is not None:
            record['path'] = filepath
        # The writer changes values; adding a <skills> block to a copy is not a value. Its slots
        # still follow the slot rule, so the kit and the plan are read first.
        no_skills = sources is not None and skills_elem is None

        # Calculate new skills
        new_skills = None
        if item_classes:
            weapon_classes = troop_weapon_classes(npc, item_classes)
            kit, swaps, detail = troop_kit_plan(npc, item_classes, fix_item0=purpose, couch_ids=couch_ids)
            record.update(main_weapon=detail, slots_only=slots_only, kit=kit, item0_swaps=swaps)
        if no_skills:
            record.update(status='SKIPPED (second root, no <skills> block)', new=None, no_skills=True)
            changes.append(record)
            continue
        if item_classes:
            # rule_id: the id the troop's rules are keyed by. A copy takes its campaign source's
            # militia row, elite bonus and thrown-primary status as well as its culture row and
            # ladder line (copy_source); its own level and kit set the rest.
            rule_id, ladder_file = troop_id, filename_culture
            source = copy_source(troop_id, npc.get('culture', ''), sources)
            if source is not None:
                culture, rule_id, ladder_file = source
                record['culture'] = culture
            cells = ladder_cells(rule_id, ladder_file, level, weapon_classes)
            mounted, difficulty = troop_mount(npc, mounts)
            record['ladder'] = cells
            try:
                new_skills = calculate_skills(culture, level, rule_id, kit, ladder=cells,
                                              mounted=mounted, mount_difficulty=difficulty)
            except MissingCultureRow:
                # Only --apply and --restat need the formula; main() stops those on this record.
                # The clamp and --fix-purpose read what is on disk, so they go on.
                record.update(status='SKIPPED (no culture row)', new=None, missing_culture=culture)
                changes.append(record)
                continue
        if new_skills is None:
            record.update(status='SKIPPED (no weapon in its battle sets)', new=None)
            changes.append(record)
            continue

        # Record changes. An undeclared skill is a change even when the formula value is 0: the
        # engine reads an absent <skill> as 0, so the element has to exist before anything can
        # rely on the value.
        has_change = (any(old_skills.get(s, 0) != new_skills[s] for s in SKILL_NAMES)
                      or any(s not in old_skills for s in SKILL_NAMES))
        total_old = sum(old_skills.get(s, 0) for s in SKILL_NAMES)
        total_new = sum(new_skills[s] for s in SKILL_NAMES)

        record.update({
            'status': 'CHANGED' if has_change else 'UNCHANGED',
            'new': new_skills,
            'total_old': total_old,
            'total_new': total_new,
            'delta': total_new - total_old,
        })
        changes.append(record)

    return changes


CHARACTERS_DIR = os.path.join(MODULEDATA_DIR, 'characters')


def promoted_npcs_sources():
    """npcs file culture -> the culture tools/promote_borrowed_cultures.py copies it from, for each
    promoter target whose copied files include its npcs file (no 'standalone' list copies them all)."""
    import promote_borrowed_cultures as pr
    npcs = next(rel for rel, _ in pr.STANDALONE_FILES if rel.startswith('characters/npcs_'))
    return {target: cfg['src'] for target, cfg in pr.TARGETS.items()
            if 'standalone' not in cfg or npcs in cfg['standalone']}


def load_external_sources(item_classes=None, mounts=None, cultures=None, skill_sets=None):
    """Upgrade sources that live OUTSIDE troops/ and feed into it.

    The `villager_<culture>` entries in characters/npcs_*.xml each upgrade into their culture's
    tier-1 troop. The engine treats any character with a non-empty UpgradeTargets array as
    upgradeable, so these are real edges. A villager with a <skills> block takes the role table
    from its own kit (a farm tool and throwing stones) at its level, and the writer puts the values
    in its npcs file; without that, their old 30s lifted the recruits above the curve through the
    clamp. A villager with no <skills> block stays a read-only source: the writer would have to
    add a block, which is a larger change than a value. Every other character is never written.

    item_classes and mounts: as process_file. cultures: troop id -> culture, for a villager whose
    npcs file names no CULTURE_PCT row (it takes its first upgrade target's). Without
    item_classes every villager stays read-only.

    A villager in an npcs file that tools/promote_borrowed_cultures.py copies from another culture
    takes that source culture's row (promoted_npcs_sources): the promoter owns the file, and its
    --check holds only while the copy's villager equals the source's."""
    promoted = promoted_npcs_sources()
    records = []
    for filepath in sorted(glob.glob(os.path.join(CHARACTERS_DIR, 'npcs_*.xml'))):
        try:
            root = ET.parse(filepath).getroot()
        except ET.ParseError:
            continue
        npcs_culture = os.path.basename(filepath)[len('npcs_'):-len('.xml')]
        for npc in root.findall('.//NPCCharacter'):
            upgrades = [(u.get('id') or '').replace('NPCCharacter.', '', 1)
                        for u in npc.findall('./upgrade_targets/upgrade_target')]
            upgrades = [u for u in upgrades if u]
            if not upgrades:
                continue
            skills = {s.get('id'): int(s.get('value', '0'))
                      for s in npc.findall('./skills/skill')}
            template = npc.get('skill_template')
            if template:
                skills = templated_old(npc.get('id', ''), template, skills, skill_sets)
            record = {
                'file': os.path.basename(filepath),
                'path': filepath,
                'id': npc.get('id', ''),
                'name': get_display_name(npc.get('name', '')),
                'level': int(npc.get('level', '0')),
                'group': npc.get('default_group', 'Infantry'),
                'culture': npc.get('culture', ''),
                'old': skills,
                'upgrades': upgrades,
                'status': 'EXTERNAL SOURCE (read-only, outside troops/)',
                'new': None,
                'external': True,
            }
            if template:
                record['template'] = template
            villager = (record['id'].startswith('villager_') and npc.find('skills') is not None
                        and not template)
            if villager and item_classes:
                own = promoted.get(npcs_culture, npcs_culture)
                culture = own if own in CULTURE_PCT else (cultures or {}).get(upgrades[0])
                kit, _ = troop_kit(npc, item_classes)
                mounted, difficulty = troop_mount(npc, mounts)
                new = calculate_skills(culture, record['level'], record['id'], kit, mounted=mounted,
                                       mount_difficulty=difficulty) if culture else None
                if new is not None:
                    record.update(culture=culture, kit=kit, ladder={}, new=new, external=False,
                                  villager=True, status='VILLAGER (written, outside troops/)')
            records.append(record)
    return records


def load_roster(troop_files, item_classes, mounts, purpose=False, couch_ids=None, extra_paths=None,
                skill_sets=None):
    """Every troop record, the second root and every upgrade source outside troops/, as main()
    plans them.

    The second root (extra_paths, default extra_troop_paths(): the Fords of War copies) is on the
    curve; in a purpose mode only the slot rule reads it (process_file slots_only), and only
    --main-weapon-slots (couch_ids given) plans an Item0 exchange there: trunk's --fix-purpose never
    read the second root, so it changes no equipment line in it. An upgrade edge between the two
    roots is not a clamp edge, in either direction: a copy is tuned to its own level, and a
    campaign troop is never raised for a Fords of War or Long Defeat edge.
    tools/tests/test_long_defeat_shop_skills.py reads the shop's edges instead."""
    records = []
    for filepath in troop_files:
        if os.path.basename(filepath) not in SKIP_FILES:
            records += process_file(filepath, item_classes=item_classes, purpose=purpose, mounts=mounts,
                                    couch_ids=couch_ids, skill_sets=skill_sets)
    sources = copy_sources(records)
    for filepath in (extra_troop_paths() if extra_paths is None else extra_paths):
        extra = process_file(filepath, item_classes=item_classes, purpose=purpose and couch_ids is not None,
                             mounts=mounts, couch_ids=couch_ids, slots_only=purpose, sources=sources,
                             skill_sets=skill_sets)
        second = {c['id'] for c in extra}
        for c in extra:
            c['upgrades'] = [u for u in c['upgrades'] if u in second]
        for c in records:
            c['upgrades'] = [u for u in c['upgrades'] if u not in second]
        records += extra
    records += load_external_sources(item_classes, mounts, {c['id']: c['culture'] for c in records},
                                     skill_sets=skill_sets)
    return records


def write_files(all_changes, troop_files):
    """Write every troop whose final skills differ from what is on disk.

    Final means after the monotonicity clamp, so this deliberately includes troops the formula
    skipped: a clamp only raises, so writing one cannot revert a hand-tune. External sources are
    never written; they only feed the clamp. Nor is a second-root copy with no <skills> block (the
    writer adds no block). A villager is written into its npcs file (its record
    carries the path), and only its skill values change there.
    """
    by_file = defaultdict(dict)
    paths = {os.path.basename(f): f for f in troop_files}
    for c in all_changes:
        final = c.get('final')
        if not final or c.get('external') or c.get('no_skills'):
            continue
        current = c['old']
        if any(current.get(s, 0) != final[s] for s in SKILL_NAMES) or any(s not in current for s in SKILL_NAMES):
            by_file[c['file']][c['id']] = final
            if c.get('path'):
                paths[c['file']] = c['path']

    written = 0
    for name, troop_skill_map in sorted(by_file.items()):
        if name in paths:
            apply_skills_via_regex(paths[name], troop_skill_map)
            written += 1
    return written


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
        level_changes = [c for c in by_level[level] if c.get('final') is not None]
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
                n = c['final']   # the values actually written, not the pre-clamp curve
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
            print(f"  {c['name']:<45} L{c['level']:>2} {c['culture']:<12} old={c['total_old']:>5} new={c['total_new']:>5} delta={c['delta']:+d}")


def main():
    args = [a for a in sys.argv[1:]]
    game_modules = DEFAULT_GAME_MODULES
    if '--game-modules' in args:
        i = args.index('--game-modules')
        if i + 1 >= len(args):
            print("ERROR: --game-modules requires a path argument")
            sys.exit(1)
        game_modules = args[i + 1]
        del args[i:i + 2]
    restat_ids = []
    if '--restat' in args:
        i = args.index('--restat')
        if i + 1 >= len(args):
            print("ERROR: --restat requires a comma-separated troop id list")
            sys.exit(1)
        restat_ids = [t.strip() for t in args[i + 1].split(',') if t.strip()]
        del args[i:i + 2]
    # --dry-run is a MODIFIER, not a mode. It used to be a third mutually exclusive mode token,
    # which meant the only previewable path was the full rebaseline: --fix-monotonicity had to be
    # run for real to see what it would do, and the two modes do not produce the same change, so
    # previewing with --dry-run was actively misleading rather than merely unavailable.
    dry_run = '--dry-run' in args
    args = [a for a in args if a != '--dry-run']
    if not args:
        args = ['--apply']  # bare --dry-run keeps its old meaning: preview the rebaseline

    # --fix-purpose and --main-weapon-slots preview unless --apply is named with them: they are the
    # narrow modes, so writing must be asked for.
    if '--fix-purpose' in args or '--main-weapon-slots' in args:
        dry_run = dry_run or '--apply' not in args
        args = [a for a in args if a != '--apply']
    purpose_modes = ('--fix-purpose', '--main-weapon-slots')
    if len(args) != 1 or args[0] not in ('--apply', '--fix-monotonicity') + purpose_modes or (
            restat_ids and args[0] in purpose_modes):
        print("Usage: python rebalance_troops.py (--apply|--fix-monotonicity|--fix-purpose|--main-weapon-slots) "
              "[--dry-run] "
              "[--restat <id,id>] [--game-modules <path>]\n"
              "  --apply             rebaseline the whole roster onto the curve, then clamp\n"
              "  --fix-monotonicity  clamp only: repair upgrade ladders without rebaselining\n"
              "  --fix-purpose       reorder on-disk skills to the kit and put a real weapon in Item0,\n"
              "                      then clamp; a dry run unless --apply is added\n"
              "  --main-weapon-slots everything --fix-purpose does, and a lance rider or a troop named for\n"
              "                      its polearm opens with that polearm (Item0 trades with it); a dry run\n"
              "                      unless --apply is added\n"
              "  --dry-run           preview a mode; write nothing\n"
              "  --restat            take the formula value for these ids even in clamp-only mode")
        sys.exit(1)

    purpose = args[0] in purpose_modes
    slots_mode = args[0] == '--main-weapon-slots'
    base_on_curve = args[0] == '--apply'
    label = ("main weapon slots + purpose order + clamp" if slots_mode else
             "purpose order + clamp" if purpose else
             "full rebaseline + clamp" if base_on_curve else "monotonicity clamp only")
    mode = f"{'DRY RUN' if dry_run else 'APPLYING CHANGES'} ({label})"
    print(f"\n*** {mode} ***\n")

    # The role table reads the kit from the item-class registry of the game install (vanilla
    # Type= + Armory crafting_template=) and the horses' difficulty from the same files.
    if not os.path.isdir(game_modules):
        print(f"ERROR: Bannerlord Modules folder not found: {game_modules}\n"
              f"       The role table reads each troop's weapons and horses from the game install.\n"
              f"       Pass --game-modules <path to .../Mount & Blade II Bannerlord/Modules>.")
        sys.exit(1)
    item_classes = ts.build_item_class_registry(MODULEDATA_DIR, game_modules)
    mounts = ts.build_mount_difficulty_registry(MODULEDATA_DIR, game_modules)
    print(f"Item-class registry: {len(item_classes):,} weapon-classed items, {len(mounts):,} horses")
    try:
        skill_sets = sl.load_skill_sets(str(Path(game_modules).parent), MODULEDATA_DIR, require_vanilla=True)
    except ValueError as exc:   # the vanilla modules define no SkillSet, or a file is not UTF-8
        print(f"ERROR: the SkillSets could not be read ({exc}).\n"
              f"       A templated character cannot be read without them. Nothing was written.")
        sys.exit(1)
    print(f"SkillSets: {len(skill_sets):,}")
    couch_ids = load_couchable_ids(game_modules) if slots_mode else None
    if slots_mode:
        print(f"Couchable items (a weapon description with the couch feature): {len(couch_ids):,}")

    troop_files = sorted(glob.glob(os.path.join(TROOPS_DIR, 'troops_*.xml')))
    print(f"Found {len(troop_files)} troop files")
    undefined = undefined_ladder_ids(troop_files, item_classes)
    if undefined:
        print(f"ERROR: the rosters name {len(undefined)} ranged ladder item(s) no loaded file defines "
              f"(first few: {', '.join(undefined[:6])}), so those archers cannot be classed. Run "
              f"tools/generate_ranged_ladder_items.py --apply, then tools/rebalance_ranged_ladders.py "
              f"--apply, first. Nothing was written.")
        sys.exit(1)

    print(f"Militia bound by culture: {len(militia_troop_ids())} troops "
          f"(the tier {MILITIA_TIER} row, from {' + '.join(MILITIA_BINDING_FILES)})")

    for filepath in troop_files:
        filename = os.path.basename(filepath)
        print(f"  {'SKIPPING' if filename in SKIP_FILES else 'Processing'} {filename}...")
    # The second root (troop_purpose.EXTRA_TROOP_FILES, the Fords of War copies): on the curve in
    # --apply and the clamp mode, the slot rule only in the purpose modes (process_file slots_only).
    extra_paths = extra_troop_paths()
    for path in extra_paths:
        rel = os.path.relpath(path, EXTRA_ROOT_DIR).replace(os.sep, '/')
        print(f"  Processing {rel} (second root{', slot rule only' if purpose else ''})...")
    all_changes = load_roster(troop_files, item_classes, mounts, purpose=purpose, couch_ids=couch_ids,
                              extra_paths=extra_paths, skill_sets=skill_sets)
    troop_files = troop_files + extra_paths
    external = [c for c in all_changes if c.get('external') or c.get('villager')]
    villagers = [c for c in external if c.get('villager')]
    print(f"Upgrade sources outside troops/: {len(external)}; {len(villagers)} villagers on the curve "
          f"(written into characters/npcs_*.xml), the rest read-only")

    # A missing CULTURE_PCT row is fatal only where the formula is the base: --apply, and the
    # troops --restat names. The clamp-only modes read what is on disk.
    no_row = sorted({c['missing_culture'] for c in all_changes if c.get('missing_culture')
                     and (base_on_curve or c['id'] in restat_ids)})
    if no_row:
        print(f"ERROR: culture(s) {', '.join(no_row)} have no CULTURE_PCT row in tools/rebalance_troops.py, "
              f"so the formula has nothing to apply. Add the row. Nothing was written.")
        sys.exit(1)

    if restat_ids:
        known = {c['id'] for c in all_changes}
        unknown = [t for t in restat_ids if t not in known]
        if unknown:
            print(f"ERROR: --restat names troops that do not exist: {', '.join(unknown)}")
            sys.exit(1)
        print(f"Restatting onto the formula: {', '.join(restat_ids)}")

    if purpose:
        log, left = plan_fix_purpose(all_changes)
        if slots_mode:
            print_main_weapon_report(all_changes)
        print_purpose_report(all_changes, log, left)
        if not dry_run:
            written = write_files(all_changes, troop_files)
            swaps = defaultdict(dict)
            for c in all_changes:
                if c.get('item0_swaps'):
                    swaps[c['file']][c['id']] = c['item0_swaps']
            for filepath in troop_files:
                if os.path.basename(filepath) in swaps:
                    apply_item0_swaps(filepath, swaps[os.path.basename(filepath)])
            print(f"\n*** Skills written to {written} files, Item0 fixed in {len(swaps)} files ***")
        return

    # Every mode writes the purpose order, or the clamp leaves a respecialising child with an
    # inherited skill over its main class (settle_purpose).
    log, raised = settle_purpose(all_changes, base_on_curve=base_on_curve, restat_ids=restat_ids)
    clamped = [c for c in all_changes if c.get('clamp_lifted')]
    lifted_skips = [c for c in all_changes if c['status'].endswith('+ CLAMPED')]
    print(f"\nMonotonicity clamp: the clamp raised {raised} values on {len(clamped)} troops "
          f"so no upgrade target sits below the troop it upgrades from; {len(log)} more values "
          f"were raised after it so the purpose order holds")
    if lifted_skips:
        print(f"  ...plus {len(lifted_skips)} formula-skipped troops lifted off their own values "
              f"(a clamp only raises, so the hand-tune survives): "
              f"{', '.join(c['id'] for c in lifted_skips)}")

    print_report(all_changes)

    if not dry_run:
        written = write_files(all_changes, troop_files)
        print(f"\n*** Changes written to {written} files ***")


if __name__ == '__main__':
    main()
