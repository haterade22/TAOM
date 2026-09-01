#!/usr/bin/env python3
"""Generate per-culture/per-assignment/per-rank enlistment service kits (#375 Phase 4, #525).

Emits Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml: one
<EquipmentRoster id="enlist_{runtimeCultureId}_{assignment}_{rank}"> per
tree-culture x assignment x rank, assignment in infantry|archer|cavalry|support
and rank in recruit|soldier|veteran|sergeant. No gender dimension. Looked up at
the quartermaster by EnlistmentEquipmentService via EnlistmentRosterResolver.

WHAT CHANGED IN #525, AND WHY
-----------------------------
This file used to emit ARMOUR ONLY, across a (culture, rank) grid. Players
reported drawing armour and never a weapon, and they were exactly right: a slot
census of the shipped file returned 374 armour elements and zero Item0..Item3.
The C# was never at fault (EquipmentRosterCatalogAdapter already reads all 12
slots); the generator discarded every weapon the donor troops carried.

Two things follow from adding weapons, and both are load-bearing:

  * ASSIGNMENT became part of the key. A kit is only right if it matches the
    role the player chose, so the donor pool is filtered by default_group
    (infantry/support -> Infantry, archer -> Ranged, cavalry -> Cavalry).

  * The rank band became a HARD CAP rather than a sort key. OVERSHOOT_WEIGHT
    only ever reordered a candidate list, which was safe while the list was the
    whole culture pool and an in-band donor almost always existed. Filtering by
    group breaks that: 40 of the 320 cells have donors only ABOVE their band,
    worst case mirkwood cavalry recruit at L41 against a band max of 13. Left
    as a sort key, a Recruit would draw elite plate and an elite weapon.

NO MOUNTS, deliberately. Horse/HorseHarness are not emitted for any assignment,
cavalry included. Three reasons, any one sufficient: the cavalry donor pools
mount taom_mumakil, taom_war_elephant and taom_chariot_a; the roster is keyed on
the COMMANDER's culture rather than the player's race, so a dwarf serving a
horse culture would be handed a horse he spawns inside the mesh of; and
MOUNTED_DWARF (.claude/rules/moduledata-validation.md) cannot see these rosters
at all, because it walks data an NPCCharacter names and these are applied at
runtime by id. Cavalry therefore means a cavalry WEAPON set.

Culture ids are RUNTIME StringIds (the #1 TAOM data bug): vlandia=Rohan,
empire=Dunland, aserai=Harad, khuzait=Rhun, sturgia=Dale, battania=Khand,
lothlorien=Galadhrim. Cultures come from the culture= attribute of every
NPCCharacter in Main/_Module/ModuleData/troops/troops_*.xml (20 tokens; eight
files carry more than one culture and troops_goblin.xml carries three), then
extended by TREE_ALIASES.

A cell whose group pool has no in-band donor emits NOTHING. That is not a gap:
EnlistmentRosterResolver walks culture -> assignment -> rank, so the player
lands on the same culture's infantry kit rather than on another faction's. An
infantry-seeded roster authored under a cavalry id would read as a real cavalry
kit to every later reader, and would satisfy a content check by accident.

The 16 enlist_default_{assignment}_{rank} rosters are HAND-AUTHORED (broadly
available human militia gear, verified against the live indexes on every run)
and carried as a literal here so a full --apply regeneration preserves them.

Usage:
    python tools/generate_enlistment_rosters.py                    # dry-run (default)
    python tools/generate_enlistment_rosters.py --apply            # write the XML
    python tools/generate_enlistment_rosters.py --culture vlandia  # restrict
    python tools/generate_enlistment_rosters.py --apply --seed-missing
        # append-only: adds ONLY rosters whose ids are absent (preserves hand edits)

XML I/O per tools/README.md "XML I/O convention": UTF-8 (BOM-preserving on
rewrite; new file written without BOM, matching the equipmentsets siblings),
CRLF line endings, timestamped backup on a non-.xml extension before a
destructive write.
"""

import argparse
import os
import re
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derive_armor_tiers as dat  # noqa: E402  (armour index + level_to_tier bands)
import taom_schema as ts  # noqa: E402  (item universe + weapon classification)
from _gamedir import ENV_VAR, game_modules  # noqa: E402

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
MODULEDATA_DIR = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData')
TROOPS_DIR = os.path.join(MODULEDATA_DIR, 'troops')
OUT_XML = os.path.join(MODULEDATA_DIR, 'equipmentsets', 'taom_enlistment_equipment.xml')
DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

RANKS = ['recruit', 'soldier', 'veteran', 'sergeant']

# Mirrors TAOM.Features.Enlistment.Content.Domain.ServiceAssignment. The order here is this
# file's output order and carries no meaning; the C# enum's ordinals are what persist.
ASSIGNMENTS = ['infantry', 'archer', 'cavalry', 'support']

# assignment -> the troop default_group its donors are drawn from. Support has no battlefield
# group of its own (BattleFormationPolicy returns null for it on purpose: the rear-echelon
# fantasy has no line to stand in), so it borrows the Infantry pool for ARMOUR and is then cut
# back to a single sidearm by support_kit() below.
# Values are TUPLES, because default_group has FOUR values and a 1:1 map silently dropped one.
# HorseArcher (23 troops) belongs to both mounted and missile fantasies, and excluding it cost real
# cells: gundabad had no cavalry soldier because its three Cavalry donors are all over the cap while
# two HorseArchers sit under it, and Rohan's and Rhun's signature horse archers could seed neither
# kit. ALL_TROOP_GROUPS below fails the run if the troop data ever grows a fifth group, rather than
# letting it disappear the way HorseArcher did.
ASSIGNMENT_GROUPS = {
    'infantry': ('Infantry',),
    'archer': ('Ranged', 'HorseArcher'),
    'cavalry': ('Cavalry', 'HorseArcher'),
    'support': ('Infantry',),
}
ALL_TROOP_GROUPS = {g for groups in ASSIGNMENT_GROUPS.values() for g in groups}

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

# The cap: a donor may sit at most one band above the requested rank. Beyond that the cell emits
# nothing and the resolver falls back within the same culture. See the module docstring -- this is
# the rule that keeps a Recruit off a level-41 kit now that the pool is group-filtered.
#
# veteran does NOT take sergeant's band max, because sergeant is open-ended (31-999) and "one band
# above heavy" would therefore be no cap at all -- mirkwood cavalry veteran drew a L41 kit under
# exactly that reading, which is above what its own Sergeant draws. It takes the sergeant band's
# FLOOR instead, so a Veteran can just reach elite and never out-equip the rank above him.
LEVEL_CAP = {
    'recruit':  RANK_BANDS['soldier'][1],
    'soldier':  RANK_BANDS['veteran'][1],
    'veteran':  RANK_BANDS['sergeant'][0],
    'sergeant': RANK_BANDS['sergeant'][1],
}

ARMOR_SLOTS = ['Head', 'Body', 'Leg', 'Gloves', 'Cape']
WEAPON_SLOTS = ['Item0', 'Item1', 'Item2', 'Item3']
# Emission order mirrors vanilla and taom_career_starting_equipment.xml: weapons, then armour.
# Item4 is deliberately absent. The installed v1.4.8 enum maps it to ExtraWeaponSlot (=4), which a
# banner is one eligible occupant of rather than the slot's name; GetBattleSetItemIds reads slots
# 0..11, so anything placed there WOULD be issued. No troop file uses it (censused 2026-09-01).
EMIT_SLOTS = WEAPON_SLOTS + ARMOR_SLOTS
PARSE_SLOTS = set(EMIT_SLOTS)

# Ammunition needs its launcher and vice versa: a bow with no arrows is a stick, and loose arrows
# are dead weight. Donor data is clean today (all 260 ranged donors pair correctly, measured
# 2026-09-01), so this only fires when an item is dropped for failing to resolve.
AMMO_FOR = {'Bow': 'Arrows', 'Crossbow': 'Bolts'}

# Overshoot is penalized 2x undershoot WITHIN the cap above: issuing under-tier gear is a mild
# disappointment; issuing the top of the allowed range at every rank flattens progression.
OVERSHOOT_WEIGHT = 2

# Hand-authored culture-neutral fallbacks (enlist_default_{assignment}_{rank}). Broadly-available
# human militia gear; every id is verified against the live indexes on generation (a missing id
# fails the run loudly). neutral_culture is the engine's culture-less StringId (validator-known).
# PRESERVED verbatim by a full --apply regeneration.
#
# These are the LAST resort, reached only by a culture with no roster of its own at any
# assignment. They are Rohan/Dunland/Dale militia gear and so are not truly neutral -- that is
# #431, and it is the reason this block should stay small rather than grow comfortable.
DEFAULT_ROSTER_ITEMS = {
    ('infantry', 'recruit'): [
        ('Item0', 'wm_rohan_ws_sword_a01'),
        ('Body', 'rohan_militia_tunic_a'),
        ('Leg', 'cts_rohan_boots3'),
    ],
    ('infantry', 'soldier'): [
        ('Item0', 'wm_rohan_ws_sword_a01'),
        ('Item1', 'wm_rohan_shield_a01_gsg'),
        ('Head', 'cts_rohan_helmet1'),
        ('Body', 'rohan_militia_tunic_b'),
        ('Leg', 'cts_rohan_boots4'),
    ],
    ('infantry', 'veteran'): [
        ('Item0', 'wm_rohan_ws_sword_a02'),
        ('Item1', 'wm_rohan_shield_a01_gsg'),
        ('Head', 'sk_dale_helmet_infrantry_a01'),
        ('Body', 'rohan_militia_armour_a'),
        ('Leg', 'dunland_caerdh_boots_light_a'),
        ('Gloves', 'sk_dale_gauntlet_infrantry_a01'),
    ],
    ('infantry', 'sergeant'): [
        ('Item0', 'wm_rohan_ws_sword_a02'),
        ('Item1', 'wm_rohan_shield_a01_gsg'),
        ('Item2', 'wm_rohan_ws_spear_a01'),
        ('Head', 'cts_rohan_helmet1b'),
        ('Body', 'rohan_militia_armour_b'),
        ('Leg', 'dunland_caerdh_boots_light_b'),
        ('Gloves', 'dunland_caerdh_bracer_light_a'),
        ('Cape', 'dunland_wulf_cape_short_a'),
    ],
    ('archer', 'recruit'): [
        ('Item0', 'lowland_longbow'),
        ('Item1', 'default_arrows'),
        ('Body', 'rohan_militia_tunic_a'),
        ('Leg', 'cts_rohan_boots3'),
    ],
    ('archer', 'soldier'): [
        ('Item0', 'lowland_longbow'),
        ('Item1', 'default_arrows'),
        ('Item2', 'wm_rohan_ws_sword_a01'),
        ('Head', 'cts_rohan_helmet1'),
        ('Body', 'rohan_militia_tunic_b'),
        ('Leg', 'cts_rohan_boots4'),
    ],
    ('archer', 'veteran'): [
        ('Item0', 'highland_ranger_bow'),
        ('Item1', 'barbed_arrows'),
        ('Item2', 'wm_rohan_ws_sword_a02'),
        ('Head', 'sk_dale_helmet_infrantry_a01'),
        ('Body', 'rohan_militia_armour_a'),
        ('Leg', 'dunland_caerdh_boots_light_a'),
        ('Gloves', 'sk_dale_gauntlet_infrantry_a01'),
    ],
    ('archer', 'sergeant'): [
        ('Item0', 'highland_ranger_bow'),
        ('Item1', 'barbed_arrows'),
        ('Item2', 'wm_rohan_ws_sword_a02'),
        ('Head', 'cts_rohan_helmet1b'),
        ('Body', 'rohan_militia_armour_b'),
        ('Leg', 'dunland_caerdh_boots_light_b'),
        ('Gloves', 'dunland_caerdh_bracer_light_a'),
        ('Cape', 'dunland_wulf_cape_short_a'),
    ],
    ('cavalry', 'recruit'): [
        ('Item0', 'wm_rohan_ws_spear_a01'),
        ('Item1', 'wm_rohan_ws_sword_a01'),
        ('Body', 'rohan_militia_tunic_a'),
        ('Leg', 'cts_rohan_boots3'),
    ],
    ('cavalry', 'soldier'): [
        ('Item0', 'wm_rohan_ws_spear_a01'),
        ('Item1', 'wm_rohan_ws_sword_a01'),
        ('Head', 'cts_rohan_helmet1'),
        ('Body', 'rohan_militia_tunic_b'),
        ('Leg', 'cts_rohan_boots4'),
    ],
    ('cavalry', 'veteran'): [
        ('Item0', 'wm_rohan_ws_spear_a02'),
        ('Item1', 'wm_rohan_ws_sword_a02'),
        ('Head', 'sk_dale_helmet_infrantry_a01'),
        ('Body', 'rohan_militia_armour_a'),
        ('Leg', 'dunland_caerdh_boots_light_a'),
        ('Gloves', 'sk_dale_gauntlet_infrantry_a01'),
    ],
    ('cavalry', 'sergeant'): [
        ('Item0', 'wm_rohan_ws_spear_a03'),
        ('Item1', 'wm_rohan_ws_sword_a02'),
        ('Head', 'cts_rohan_helmet1b'),
        ('Body', 'rohan_militia_armour_b'),
        ('Leg', 'dunland_caerdh_boots_light_b'),
        ('Gloves', 'dunland_caerdh_bracer_light_a'),
        ('Cape', 'dunland_wulf_cape_short_a'),
    ],
    # Support carries ONE sidearm and no shield, at every rank. The armour still climbs, because
    # the quartermaster issues by rank, but the weapon does not: a Steward-track soldier who
    # out-fights the line troops would make the assignment choice meaningless.
    ('support', 'recruit'): [
        ('Item0', 'wm_rohan_ws_sword_a01'),
        ('Body', 'rohan_militia_tunic_a'),
        ('Leg', 'cts_rohan_boots3'),
    ],
    ('support', 'soldier'): [
        ('Item0', 'wm_rohan_ws_sword_a01'),
        ('Head', 'cts_rohan_helmet1'),
        ('Body', 'rohan_militia_tunic_b'),
        ('Leg', 'cts_rohan_boots4'),
    ],
    ('support', 'veteran'): [
        ('Item0', 'wm_rohan_ws_sword_a02'),
        ('Head', 'sk_dale_helmet_infrantry_a01'),
        ('Body', 'rohan_militia_armour_a'),
        ('Leg', 'dunland_caerdh_boots_light_a'),
        ('Gloves', 'sk_dale_gauntlet_infrantry_a01'),
    ],
    ('support', 'sergeant'): [
        ('Item0', 'wm_rohan_ws_sword_a02'),
        ('Head', 'cts_rohan_helmet1b'),
        ('Body', 'rohan_militia_armour_b'),
        ('Leg', 'dunland_caerdh_boots_light_b'),
        ('Gloves', 'dunland_caerdh_bracer_light_a'),
        ('Cape', 'dunland_wulf_cape_short_a'),
    ],
}
DEFAULT_CULTURE = 'neutral_culture'

# Cultures with no troops_*.xml file of their own that nevertheless BIND to another culture's
# troop tree in the culture data. Enumerating troop files alone reports them as "no troop tree",
# which is what made them fall through to the defaults; they are tree-BORROWERS, not tree-less.
# Both bindings verified 2026-08-08:
#   lothlorien -> rivendell   Main/_Module/ModuleData/taom_spcultures.xml, Culture id="lothlorien":
#                             basic_troop=NPCCharacter.imladris_recruit,
#                             elite_basic_troop=NPCCharacter.imladris_infantry
#                             -- byte-identical to Culture id="rivendell".
#   battania   -> khuzait     Main/_Module/ModuleData/spcultures.xslt, Culture[@id='battania']
#                             (the Variag/Khand re-theme; battania is a VANILLA culture, so it is
#                             NOT in taom_spcultures.xml): basic_troop=NPCCharacter.loke_rim_initiate,
#                             elite_basic_troop=NPCCharacter.loke_rim_cavalry
#                             -- identical to the Culture[@id='khuzait'] template's Rhun bindings.
# If either culture ever ships its own troops file, its real tree wins and the alias is dropped.
TREE_ALIASES = {
    'lothlorien': 'rivendell',
    'battania': 'khuzait',
}


# =============================================================================
# Parsing
# =============================================================================

def parse_troops():
    """Return {runtime_culture_id: [troop dicts]} from troops_*.xml.

    Troop dict: {id, level, group, slots: {slot: item_id}} using the FIRST non-civilian inline
    EquipmentRoster, covering weapon AND armour slots. Heroes / non-Soldier occupations are
    skipped (troop trees are all occupation="Soldier" today; the guard is cheap).
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
                    if slot not in PARSE_SLOTS:
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

    # A default_group nobody maps is a donor pool that silently does not exist. HorseArcher was in
    # exactly that state until 2026-09-01 and cost gundabad its cavalry soldier cell.
    seen = {t['group'] for troops in by_culture.values() for t in troops if t['group']}
    unmapped = sorted(seen - ALL_TROOP_GROUPS)
    if unmapped:
        raise SystemExit(f'ERROR: troop default_group(s) {unmapped} are mapped to no assignment. '
                         'Add them to ASSIGNMENT_GROUPS (or state the exclusion there) rather than '
                         'letting those donors disappear.')
    return by_culture


# =============================================================================
# Donor selection
# =============================================================================

def apply_tree_aliases(by_culture):
    """Point each tree-borrowing culture at the donor pool of the tree it binds to.

    Mutates by_culture in place. A borrower that has since grown its own troops file keeps that
    file and the alias is dropped -- the real tree always wins.
    """
    for alias, source in sorted(TREE_ALIASES.items()):
        if alias in by_culture:
            print(f'  NOTE: {alias} now owns a troop tree -- ignoring the {source} alias')
            continue
        if source not in by_culture:
            print(f'  WARN: alias {alias} -> {source}: {source} has no troop tree; '
                  f'{alias} will fall back at runtime', file=sys.stderr)
            continue
        by_culture[alias] = by_culture[source]
        print(f'  Alias: {alias} borrows the {source} troop tree '
              f'({len(by_culture[source])} donors)')


def band_score(level, rank):
    lo, hi = RANK_BANDS[rank]
    if level < lo:
        return lo - level
    if level > hi:
        return OVERSHOOT_WEIGHT * (level - hi)
    return 0


ARMOUR_ZONES = ('head_armor', 'body_armor', 'arm_armor', 'leg_armor')


def build_armour_zones():
    """item id -> (head, body, arm, leg), read straight off the <Armor> element.

    NOT derive_armor_tiers' `primary` stat, which is ONE number per item. A body piece carries
    body_armor AND leg_armor; a cape carries body_armor AND arm_armor (rebalance_armor.GOVERNED_STATS
    says so). Scoring a kit by its primary stats therefore misses most of what it protects, and that
    exact blind spot is already recorded in rebalance_armor.py:146: "arm_armor on capes was invisible
    to every analyzer (_get_primary_stat returned only body_armor), which is the blind spot that let
    the inversion ship." A promotion can be strictly worse in three zones while the primary-stat sum
    goes UP, which is what shipped for aserai cavalry before this index existed.
    """
    zones = {}
    root = game_modules(DEFAULT_GAME_ROOT)
    roots = [root / n / 'ModuleData' for n in
             ('LOTRLOME_Armory', 'SandBoxCore', 'SandBox', 'Native', 'StoryMode', 'CustomBattle')]
    roots.append(Path(MODULEDATA_DIR))
    for base in roots:
        if not base.is_dir():
            continue
        for xml in base.rglob('*.xml'):
            try:
                tree = ET.parse(xml).getroot()
            except (ET.ParseError, OSError):
                continue
            for item in tree.iter('Item'):
                iid = item.get('id')
                armor = item.find('.//Armor')
                if not iid or armor is None or iid in zones:
                    continue
                vec = []
                for key in ARMOUR_ZONES:
                    try:
                        vec.append(int(armor.get(key) or 0))
                    except ValueError:
                        vec.append(0)
                zones[iid] = tuple(vec)
    return zones


def armour_vector(slots, zones):
    """A kit's summed (head, body, arm, leg) protection."""
    total = [0, 0, 0, 0]
    for slot, item in slots.items():
        if slot not in ARMOR_SLOTS:
            continue
        for i, v in enumerate(zones.get(item, (0, 0, 0, 0))):
            total[i] += v
    return tuple(total)


def is_progression(candidate, floor_vec):
    """True when `candidate` is an acceptable promotion from `floor_vec`.

    Two conditions, because either alone lets a bad promotion through:
      * the aggregate must not fall, and
      * the kit must not be strictly dominated, i.e. worse or equal in EVERY zone and worse in at
        least one. A kit can hold its total while losing body, arm and leg to a big helmet, which
        is exactly what aserai cavalry did (head 35->35, body 45->43, arm 52->50, leg 37->30).
    """
    if sum(candidate) < sum(floor_vec):
        return False
    weaker = all(c <= f for c, f in zip(candidate, floor_vec))
    strictly = any(c < f for c, f in zip(candidate, floor_vec))
    return not (weaker and strictly)


def resolve_slots(troop, armory, universe, classes):
    """The donor's slots that actually resolve, split into (armour, weapons).

    Armour resolves against the ARMOUR index (derive_armor_tiers requires an <Armor> child, so it
    cannot see a weapon at all); weapons resolve against the full item universe, which is the only
    one of the two containing vanilla ids like composite_steppe_bow. A weapon with no class is
    dropped too: the per-assignment content rules downstream all key on the class, and an
    unclassifiable item cannot be reasoned about.
    """
    armour = {s: i for s, i in troop['slots'].items() if s in ARMOR_SLOTS and i in armory}
    weapons = {s: i for s, i in troop['slots'].items()
               if s in WEAPON_SLOTS and i in universe and classes.get(i)}
    return armour, weapons


def enforce_ammo_pairing(weapons, classes, label):
    """Drop a launcher whose ammunition did not survive, and any orphaned ammunition.

    Only reachable when an item failed to resolve above; the shipped donor data pairs correctly
    everywhere. Emitting half a pair is worse than emitting neither: a bow with no arrows is an
    item the player carries and cannot use, which is the shape of the bug this change exists to
    fix.
    """
    kept = dict(weapons)
    classed = {s: classes.get(i) for s, i in weapons.items()}
    for slot, cls in classed.items():
        ammo = AMMO_FOR.get(cls)
        if ammo and ammo not in classed.values():
            print(f'  WARN: {label}: {weapons[slot]} is a {cls} with no {ammo} left after '
                  'resolution -- dropping it')
            kept.pop(slot, None)
    wanted_ammo = {AMMO_FOR[c] for c in classed.values() if c in AMMO_FOR}
    for slot, cls in classed.items():
        if cls in ('Arrows', 'Bolts') and cls not in wanted_ammo:
            print(f'  WARN: {label}: {weapons[slot]} is loose {cls} with no launcher -- dropping it')
            kept.pop(slot, None)
    return kept


# Support's sidearm, in order of preference. OneHanded is the fantasy; the rest are here because
# preferring OneHanded and giving up was how 15 Support rosters shipped carrying armour and NO
# weapon at all -- the exact defect #525 exists to fix, reproduced inside its own fix. Every one of
# those 15 donors carried a Polearm or a TwoHanded and nothing else, so the militia spearman who
# staffs the baggage train now hands over his spear.
SUPPORT_SIDEARM_ORDER = ('OneHanded', 'Polearm', 'TwoHanded')


def support_kit(weapons, classes):
    """Support carries exactly ONE melee weapon and no shield, or nothing.

    No shield and no second weapon, because BattleFormationPolicy deliberately leaves Support out
    of every line and AssignmentSkills makes its signature skill Steward. Issuing it a full
    infantry loadout would erase the difference between choosing Support and choosing Infantry.
    Returning nothing is still possible (a donor carrying only a bow), and pick_donor turns that
    into an ABSENT cell rather than an armour-only one.
    """
    for want in SUPPORT_SIDEARM_ORDER:
        for slot in WEAPON_SLOTS:
            item = weapons.get(slot)
            if item and classes.get(item) == want:
                return {'Item0': item}
    return {}


def pick_donor(troops, rank, assignment, armory, universe, classes, enforce_cap=True,
               floor_vec=None, zones=None):
    """(donor, slots, reason) for one cell, or (None, {}, why-not).

    Gates, in order: the assignment's default_group; a resolvable Body item (a chestless issue kit
    is not worth emitting); the level cap; and the monotonicity floor. Among survivors: closest
    band fit, then the richer kit. The group filter makes the old Infantry tiebreak inert, so it
    is gone.

    `enforce_cap=False` is the rescue path described at rescue_rank() -- race correctness beats
    tier correctness, so a culture whose whole pool sits above the cap keeps its own gear rather
    than falling through to human militia.

    `floor_vec` is the (head, body, arm, leg) protection already issued at a lower rank in this
    chain. Donor troop trees are NOT monotonic in armour, so picking purely by band fit made 17
    chains hand out a promotion worse than what the player was already wearing (erebor infantry
    went 176 to 99 at the very first one). Candidates that are not a progression from the floor are
    excluded; if that empties the list the floor yields rather than lose the cell, and the caller
    is told so through the returned reason rather than silently.
    """
    groups = ASSIGNMENT_GROUPS[assignment]
    group = '/'.join(groups)
    pool = [t for t in troops if t['group'] in groups]
    if not pool:
        return None, {}, f'no {group}-group troop in this culture'

    cap = LEVEL_CAP[rank] if enforce_cap else RANK_BANDS['sergeant'][1]
    candidates = []
    over_cap = 0
    for troop in pool:
        armour, weapons = resolve_slots(troop, armory, universe, classes)
        if 'Body' not in armour:
            continue
        if troop['level'] > cap:
            over_cap += 1
            continue
        candidates.append((
            band_score(troop['level'], rank),
            -len(armour) - len(weapons),
            troop['level'],
            troop['id'],
            troop,
            armour,
            weapons,
        ))
    floor_waived = False
    if floor_vec and any(floor_vec):
        # Prefer candidates that do not move the player backwards. Applied as a FILTER rather than
        # a sort key so band fit still decides among the acceptable ones. It yields entirely when
        # nothing clears the floor, because an off-progression kit still beats no kit -- but the
        # waiver is RECORDED and printed, because a silent waiver is how the regression this rule
        # exists to stop would come back unnoticed.
        kept = [c for c in candidates if is_progression(armour_vector(c[5], zones or {}), floor_vec)]
        if kept:
            candidates = kept
        else:
            floor_waived = True
    if not candidates:
        if over_cap:
            return None, {}, (f'{over_cap} {group} donor(s), all above the L{cap} cap for '
                              f'{rank} -- resolver falls back within this culture')
        return None, {}, f'no {group} donor with an armory-resolvable Body item'

    candidates.sort(key=lambda c: c[:4])
    best = candidates[0]
    donor, armour, weapons = best[4], best[5], best[6]
    label = f'{donor["id"]}/{assignment}/{rank}'
    weapons = enforce_ammo_pairing(weapons, classes, label)
    if assignment == 'support':
        weapons = support_kit(weapons, classes)
    if not weapons:
        # The lower bound the whole change exists to enforce, applied where the donor gate already
        # lives. An armour-only cell is not merely a thin kit: EnlistmentRosterResolver probes
        # EXISTENCE, so a present-but-weaponless roster ENDS the walk and shadows the armed kit the
        # player would otherwise have fallen back to. Absent is strictly better than present-and-empty.
        return None, {}, (f'donor {donor["id"]} yielded no usable weapon for {assignment} -- '
                          'cell suppressed so the resolver falls back to an armed kit')
    slots = dict(armour)
    slots.update(weapons)
    note = ''
    if floor_waived:
        note = ('ARMOUR FLOOR WAIVED -- no in-cap donor is a progression from the rank below, so '
                'this promotion may reduce protection in some zone')
    return donor, slots, note


def rescue_rank(troops, rank, armory, universe, classes, zones=None):
    """Last-resort infantry donor for a rank, cap ignored. (donor, slots) or (None, {}).

    Reached only when EVERY assignment came back empty for this culture at this rank, which means
    the culture would otherwise own no roster there at all and the resolver would walk out to
    enlist_default_*. That default is Rohan/Dunland/Dale militia gear, and the cultures this
    actually happens to are bluecraig and mistymountainorcs -- goblin and orc races, whose troop
    files carry a single L36 donor apiece. Handing them human militia gear is #431 with a
    rendering fault on top: vanilla and cross-race armour clips or floats on a custom skeleton
    (lessons/data-content-cultures.md).

    So the cap yields here, deliberately and visibly. Race correctness beats tier correctness: an
    over-tier kit of your own people's make is a balance complaint, the other is a visual bug.
    """
    return pick_donor(troops, rank, 'infantry', armory, universe, classes, enforce_cap=False,
                      zones=zones)[:2]


# =============================================================================
# XML emission
# =============================================================================

def roster_block(roster_id, culture, slot_items):
    lines = [f'    <EquipmentRoster id="{roster_id}" culture="Culture.{culture}">',
             '        <EquipmentSet>']
    for slot in EMIT_SLOTS:
        if slot in slot_items:
            lines.append(f'            <Equipment slot="{slot}" id="Item.{slot_items[slot]}" />')
    lines.append('        </EquipmentSet>')
    lines.append('    </EquipmentRoster>')
    return '\n'.join(lines)


def default_blocks(armory, universe):
    blocks = ['\n    <!-- HAND-AUTHORED culture-neutral fallbacks (enlist_default_{assignment}_{rank}).',
              '         Broadly-available human militia gear. The resolver falls through here only',
              '         for a culture with no roster of its own at any assignment, so this is a',
              '         genuine last resort. Do not let a regeneration drop these: the generator',
              '         carries them as a literal. -->\n']
    for assignment in ASSIGNMENTS:
        for rank in RANKS:
            items = DEFAULT_ROSTER_ITEMS[(assignment, rank)]
            missing = [i for _slot, i in items if i not in armory and i not in universe]
            if missing:
                raise SystemExit('ERROR: hand-authored default items missing from both the armory '
                                 f'index and the item universe: {missing}')
            blocks.append(roster_block(f'enlist_default_{assignment}_{rank}',
                                       DEFAULT_CULTURE, dict(items)))
    return blocks


def file_header():
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<!--\n'
        '  TAOM enlistment service-issue kits (#375 Phase 4; weapons added in #525).\n'
        '\n'
        '  Roster ID convention: enlist_{runtimeCultureId}_{assignment}_{rank}, assignment in\n'
        '  infantry|archer|cavalry|support, rank in recruit|soldier|veteran|sergeant. No gender\n'
        '  dimension. Issued to the party INVENTORY (not equipped) once per rank by\n'
        '  EnlistmentEquipmentService; EnlistmentRosterResolver walks culture, then assignment,\n'
        '  then rank, so a missing cell lands on the same culture rather than another faction.\n'
        '\n'
        '  Slots: weapons Item0..Item3, then armour Head/Body/Leg/Gloves/Cape. NO Horse and no\n'
        '  HorseHarness at any assignment, cavalry included: the cavalry donor pools mount\n'
        '  mumakil, war elephants and chariots, the roster is keyed on the COMMANDER culture so a\n'
        '  dwarf could be handed a horse he spawns inside, and MOUNTED_DWARF cannot see a roster\n'
        '  that no NPCCharacter names. No Item4 either, which the engine calls ExtraWeaponSlot,\n'
        '  because GetBattleSetItemIds reads slots 0 to 11 so anything there would be issued.\n'
        '\n'
        '  RUNTIME culture ids (the #1 TAOM data bug; lore names are WRONG here):\n'
        '  vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Rhun, sturgia=Dale,\n'
        '  battania=Khand, lothlorien=Galadhrim.\n'
        '\n'
        '  lothlorien and battania define no troops_*.xml file of their OWN, but both bind\n'
        "  to another culture's tree, so they are NOT rosterless: taom_spcultures.xml gives\n"
        '  lothlorien basic_troop=imladris_recruit / elite_basic_troop=imladris_infantry,\n'
        '  i.e. the same Rivendell tree rivendell itself uses; spcultures.xslt gives\n'
        '  battania (Variag/Khand) basic_troop=loke_rim_initiate /\n'
        "  elite_basic_troop=loke_rim_cavalry, i.e. khuzait's Rhun tree. Their rosters\n"
        "  therefore mirror rivendell and khuzait rank-for-rank (the generator's\n"
        '  TREE_ALIASES map reproduces them).\n'
        '\n'
        '  A cell is ABSENT when its group pool has no donor within one band of the rank. That is\n'
        '  deliberate: an infantry-seeded roster under a cavalry id would read as an authored\n'
        '  cavalry kit to the next person who opens this file.\n'
        '\n'
        '  GENERATED by tools/generate_enlistment_rosters.py from per-culture donor troops\n'
        '  (rank -> level band per derive_armor_tiers.level_to_tier) plus the live\n'
        '  LOTRLOME_Armory index and the full item universe. Hand-tune freely, then keep re-runs\n'
        '  append-only with the seed-missing flag; a full apply regeneration overwrites tuned\n'
        '  culture rosters.\n'
        '  (XML comments cannot contain double hyphens, hence the spelled-out flag names.)\n'
        '  The enlist_default_* block is hand-authored and survives regeneration.\n'
        '-->\n'
        '<EquipmentRosters>\n'
    )


# =============================================================================
# I/O (tools/README.md XML I/O convention)
# =============================================================================

def write_xml(path, text):
    """CRLF + BOM-preserving byte write. New files: no BOM (equipmentsets convention).

    The backup is TIMESTAMPED. A write-once `.bak-enlist` meant the second run of a session kept
    the backup from the first, so the copy on disk was not the state being overwritten, which is
    the one job a backup has. Non-.xml extension because these folders are globbed and an .xml
    backup injects duplicate item ids (.claude/rules/moduledata-validation.md).
    """
    had_bom = False
    if os.path.exists(path):
        had_bom = open(path, 'rb').read(3) == b'\xef\xbb\xbf'
        bak = f'{path}.bak-enlist-{time.strftime("%Y%m%d%H%M%S")}'
        with open(path, 'rb') as src, open(bak, 'wb') as dst:
            dst.write(src.read())
        print(f'  Backup: {os.path.basename(bak)}')
    crlf = text.replace('\r\n', '\n').replace('\n', '\r\n')
    with open(path, 'wb') as fh:
        fh.write((b'\xef\xbb\xbf' if had_bom else b'') + crlf.encode('utf-8'))


def parse_written(text):
    """Reject a document that no longer parses, before it reaches disk.

    .claude/rules/moduledata-validation.md: any script that transforms XML must parse the result
    and refuse to write a malformed one. Costs microseconds and makes a whole class of defect
    unshippable -- on 2026-08-28 an offset-based edit wrote 8 malformed ModuleData files and
    nothing noticed until a hand check.
    """
    try:
        ET.fromstring(text)
    except ET.ParseError as e:
        raise SystemExit(f'ERROR: refusing to write malformed XML: {e}')


# =============================================================================
# Main
# =============================================================================

def build_indexes():
    """(armour index, item universe, weapon classes), or a loud exit.

    environment-failures.md: report, do not self-heal. Without the live install the generator
    cannot verify that what it emits resolves, and an unverifiable roster is how the underwear bug
    ships.
    """
    armory, armory_dir = dat.build_armory_index()
    if not armory:
        raise SystemExit(f'ERROR: no armory items found under {armory_dir} '
                         f'(set ${ENV_VAR} to the game install). '
                         'Refusing to emit unverifiable rosters.')
    modules = game_modules(DEFAULT_GAME_ROOT)
    if not modules.is_dir():
        raise SystemExit(f'ERROR: Bannerlord Modules folder not found: {modules} '
                         f'(set ${ENV_VAR}). Weapon ids cannot be verified without it.')
    universe = ts.build_registries(MODULEDATA_DIR, modules).items
    classes = ts.build_item_class_registry(MODULEDATA_DIR, modules)
    zones = build_armour_zones()
    print(f'Armory index:   {len(armory):,} armour items from {armory_dir}')
    print(f'Item universe:  {len(universe):,} ids from {modules}')
    print(f'Weapon classes: {len(classes):,} weapon-classed items')
    print(f'Armour zones:   {len(zones):,} items with a per-zone protection vector')
    return armory, universe, classes, zones


def main():
    ap = argparse.ArgumentParser(description='Generate enlistment service kits (dry-run default).')
    ap.add_argument('--apply', action='store_true', help='write the XML (default: dry-run)')
    ap.add_argument('--culture', default='', help='restrict to one runtime culture id (e.g. vlandia)')
    ap.add_argument('--seed-missing', action='store_true',
                    help='append-only: add ONLY rosters whose ids are absent from the existing file')
    args = ap.parse_args()

    armory, universe, classes, zones = build_indexes()

    by_culture = parse_troops()
    apply_tree_aliases(by_culture)
    cultures = sorted(by_culture)
    if args.culture:
        if args.culture not in by_culture:
            raise SystemExit(f'ERROR: culture {args.culture!r} has no troop tree. '
                             f'Known: {", ".join(cultures)}')
        cultures = [args.culture]
    print(f'Cultures (own tree + aliases): {len(cultures)} ({", ".join(cultures)})')

    blocks = []
    # (culture, assignment, rank, donor id, level, band_score, slots, skip reason)
    table = []
    # (culture, assignment) -> every item set already emitted at a LOWER rank. A cell identical to
    # one the player has already drawn is not a promotion: the ledger spends a draw and hands back
    # duplicates of what he is wearing. The resolver descends ranks, so suppressing the repeat
    # yields the same kit from one roster instead of several, and stops the file claiming a
    # progression the donor tree does not have.
    #
    # A SET, not just the previous rank: aserai cavalry ran recruit=X, soldier=Y, veteran=Z,
    # sergeant=X, so comparing only against the rank below missed the repeat that mattered most.
    seen_kits = {}
    # (culture, assignment) -> the (head, body, arm, leg) protection already issued in that chain,
    # so a promotion can never hand back a kit that is weaker in aggregate or strictly dominated.
    armour_floor = {}
    for culture in cultures:
        troops = by_culture[culture]
        blocks.append(f'\n    <!-- {culture.upper()} -->\n')
        # Rank OUTSIDE assignment: the rescue pass below has to see a whole rank at once.
        for rank in RANKS:
            filled = []
            for assignment in ASSIGNMENTS:
                floor = armour_floor.get((culture, assignment))
                donor, slots, why = pick_donor(troops, rank, assignment, armory, universe, classes,
                                               floor_vec=floor, zones=zones)
                if donor is None:
                    table.append((culture, assignment, rank, None, None, None, {}, why))
                    continue
                kit = frozenset(slots.items())
                already = seen_kits.setdefault((culture, assignment), set())
                if kit in already:
                    table.append((culture, assignment, rank, donor['id'], donor['level'],
                                  band_score(donor['level'], rank), slots,
                                  'DUPLICATE of a lower rank -- suppressed, resolver descends'))
                    continue
                already.add(kit)
                vec = armour_vector(slots, zones)
                # Keep the STRONGER of the two per zone, so a waived floor at one rank cannot lower
                # the bar for every rank above it.
                armour_floor[(culture, assignment)] = (
                    tuple(max(a, b) for a, b in zip(floor, vec)) if floor else vec)
                filled.append((assignment, slots))
                table.append((culture, assignment, rank, donor['id'], donor['level'],
                              band_score(donor['level'], rank), slots, why))
            if not filled:
                # Nothing at this rank at all, so the resolver would leave the culture entirely.
                donor, slots = rescue_rank(troops, rank, armory, universe, classes, zones)
                rescued = frozenset(slots.items()) if donor is not None else None
                if rescued is not None and rescued in seen_kits.get((culture, 'infantry'), set()):
                    donor = None   # the rescue would re-emit a lower rank; the descent covers it
                if donor is not None:
                    seen_kits.setdefault((culture, 'infantry'), set()).add(rescued)
                    filled.append(('infantry', slots))
                    table = [row for row in table
                             if not (row[0] == culture and row[1] == 'infantry' and row[2] == rank)]
                    table.append((culture, 'infantry', rank, donor['id'], donor['level'],
                                  band_score(donor['level'], rank), slots, 'RESCUE, cap waived'))
            for assignment, slots in filled:
                blocks.append(roster_block(f'enlist_{culture}_{assignment}_{rank}', culture, slots))

    blocks.extend(default_blocks(armory, universe))
    content = file_header() + '\n'.join(blocks) + '\n\n</EquipmentRosters>\n'
    parse_written(content)

    # A rescue row carries a note but IS emitted; only a suppressed duplicate is not.
    emitted = sum(1 for row in table if row[3] and not row[7].startswith('DUPLICATE'))
    defaults = len(ASSIGNMENTS) * len(RANKS)
    print(f'\nDonor table ({emitted} culture rosters + {defaults} defaults = {emitted + defaults} '
          f'total; {len(table) - emitted} cell(s) intentionally absent):')
    print(f'  {"culture":<18}{"assign":<10}{"rank":<10}{"donor troop":<38}{"L":>3}{"band":>6}  kit')
    # The rank loop now runs outside the assignment loop (the rescue pass needs a whole rank at
    # once), so the table no longer arrives in reading order. Sort it back.
    by_assignment = {a: i for i, a in enumerate(ASSIGNMENTS)}
    by_rank = {r: i for i, r in enumerate(RANKS)}
    table.sort(key=lambda row: (row[0], by_assignment[row[1]], by_rank[row[2]]))
    for culture, assignment, rank, donor_id, level, score, slots, why in table:
        if donor_id is None:
            print(f'  {culture:<18}{assignment:<10}{rank:<10}{"-- ABSENT --":<38}{"":>3}{"":>6}  {why}')
            continue
        if why.startswith('DUPLICATE'):  # the only `why` that means NOT emitted
            print(f'  {culture:<18}{assignment:<10}{rank:<10}{"-- SUPPRESSED --":<38}{level:>3}{score:>6}  {why}')
            continue
        # band_score is printed per cell so that the next time a pool loses its low-level donors
        # it shows up in the run output, not only in the shipped file.
        # A rescued cell announces itself rather than reading as an ordinary off-band pick.
        flag = f'  <-- {why}' if why else ('' if score == 0 else '  <-- off-band')
        kit = ','.join(f'{s}={slots[s]}' for s in EMIT_SLOTS if s in slots)
        print(f'  {culture:<18}{assignment:<10}{rank:<10}{donor_id:<38}{level:>3}{score:>6}  {kit}{flag}')

    if not args.apply:
        print('\nDRY-RUN: no file written. Re-run with --apply.')
        return

    if args.seed_missing and os.path.exists(OUT_XML):
        raw = open(OUT_XML, 'rb').read()
        existing = raw.decode('utf-8-sig')
        present = set(re.findall(r'<EquipmentRoster id="([^"]+)"', existing))
        new_blocks = []
        # Reuses the slots already computed in `table`. The previous version called pick_donor a
        # SECOND time here, which is a second copy of the selection rules free to drift from the
        # first, and the group filter plus the level cap would have made that drift a real hazard.
        for culture, assignment, rank, donor_id, _level, _score, slots, why in table:
            rid = f'enlist_{culture}_{assignment}_{rank}'
            # Skip only genuinely SUPPRESSED rows. An emitted row may still carry a note (a
            # rescue, or a waived armour floor), and treating any note as a suppression meant
            # --seed-missing could not restore a deleted rescue roster -- the one case the flag
            # exists for, since a missing rescue drops a whole culture to foreign defaults.
            if donor_id is None or why.startswith('DUPLICATE') or rid in present:
                continue
            new_blocks.append(roster_block(rid, culture, slots))
        for assignment in ASSIGNMENTS:
            for rank in RANKS:
                rid = f'enlist_default_{assignment}_{rank}'
                if rid not in present:
                    new_blocks.append(roster_block(
                        rid, DEFAULT_CULTURE, dict(DEFAULT_ROSTER_ITEMS[(assignment, rank)])))
        if not new_blocks:
            print('\n--seed-missing: nothing to add (all roster ids already present).')
            return
        insert = ('\n    <!-- seeded by generate_enlistment_rosters.py --seed-missing -->\n'
                  + '\n'.join(new_blocks) + '\n\n</EquipmentRosters>')
        updated = existing.replace('</EquipmentRosters>', insert, 1)
        parse_written(updated)
        write_xml(OUT_XML, updated)
        print(f'\n--seed-missing: appended {len(new_blocks)} roster(s) to {OUT_XML}')
        return

    if args.culture:
        raise SystemExit('ERROR: --apply with --culture would write a partial file; '
                         'use --culture only for dry-run inspection, or --seed-missing.')

    write_xml(OUT_XML, content)
    print(f'\nWROTE {OUT_XML}')
    print('Next: python tools/audit_enlistment_roster_coverage.py '
          '&& python tools/validate_moduledata.py '
          '&& python tools/audit_polearm_shield_parity.py')
    print('NOTE: the file loads only once registered in Main/_Module/SubModule.xml '
          '(<XmlName id="EquipmentRosters" path="equipmentsets/taom_enlistment_equipment"/>) '
          'and only at a full game restart.')


if __name__ == '__main__':
    main()
