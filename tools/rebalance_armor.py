#!/usr/bin/env python3
"""
Armor Rebalancing Script for TAOM

Applies uniform baseline + cultural modifier formula to all armor items.
Each item is classified into a tier (civilian/light/medium/heavy/elite/lord),
then stats are computed from: Final Stat = Baseline[tier][slot] + Cultural Mod

Usage:
    python rebalance_armor.py --dry-run       # Preview changes
    python rebalance_armor.py --apply         # Write changes to XML files
    python rebalance_armor.py --export-csv    # Export tier classification to CSV
"""

import argparse
import xml.etree.ElementTree as ET
import math
import os
import sys
import glob
import re
import csv
from collections import defaultdict


def _default_armory_dir():
    """Resolve the LIVE LOTRLOME_Armory item tree (the one the game loads).

    Honors $BANNERLORD_GAME_DIR if set (the game install is the user's domain and
    may move); otherwise falls back to the known Steam install. The previous target
    (../../taommod/src/data/armory) was a STALE shadow tree the engine never reads —
    see docs/features/armor-balance.md.
    """
    game_dir = os.environ.get('BANNERLORD_GAME_DIR') or \
        r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    return os.path.join(game_dir, 'Modules', 'LOTRLOME_Armory', 'ModuleData', 'LOTRLOME_items')


ARMORY_DIR = _default_armory_dir()

# Hand-authored cultures (#99 Gondor; #211/#212 Mordor/Isengard/Dol Guldur/Gundabad/Erebor/Iron Hills).
# A blanket --apply would flatten deliberate tuning, so applying across ALL cultures requires the
# explicit --all flag. Scope a normal run with --cultures. See docs/features/armor-balance.md.
PRESERVE_CULTURES = {
    'gondor', 'mordor', 'isengard', 'dol_guldur', 'gundabad', 'erebor', 'iron_hills',
}

SLOT_TYPES = {
    'head_armors.xml': 'head',
    'body_armors.xml': 'body',
    'arm_armors.xml': 'arm',
    'leg_armors.xml': 'leg',
    'shoulder_armors.xml': 'shoulder',
}

TIERS = ['civilian', 'light', 'medium', 'heavy', 'elite', 'lord']

# =============================================================================
# Baseline Armor Values per Tier per Slot
# =============================================================================

BODY_BASELINES = {
    'civilian': {'body_armor': 5,  'leg_armor': 3,  'weight': 3.0},
    'light':    {'body_armor': 20, 'leg_armor': 10, 'weight': 8.0},
    'medium':   {'body_armor': 32, 'leg_armor': 16, 'weight': 13.0},
    'heavy':    {'body_armor': 42, 'leg_armor': 22, 'weight': 18.0},
    'elite':    {'body_armor': 50, 'leg_armor': 28, 'weight': 22.0},
    'lord':     {'body_armor': 60, 'leg_armor': 36, 'weight': 24.0},
}

HEAD_BASELINES = {
    'civilian': {'head_armor': 5,  'weight': 0.5},
    'light':    {'head_armor': 15, 'weight': 1.5},
    'medium':   {'head_armor': 24, 'weight': 2.5},
    'heavy':    {'head_armor': 32, 'weight': 3.5},
    'elite':    {'head_armor': 40, 'weight': 4.5},
    'lord':     {'head_armor': 48, 'weight': 5.0},
}

ARM_BASELINES = {
    'civilian': {'arm_armor': 3,  'weight': 0.3},
    'light':    {'arm_armor': 8,  'weight': 0.6},
    'medium':   {'arm_armor': 14, 'weight': 1.0},
    'heavy':    {'arm_armor': 20, 'weight': 1.5},
    'elite':    {'arm_armor': 26, 'weight': 2.0},
    'lord':     {'arm_armor': 34, 'weight': 2.5},
}

LEG_BASELINES = {
    'civilian': {'leg_armor': 4,  'weight': 0.5},
    'light':    {'leg_armor': 12, 'weight': 1.5},
    'medium':   {'leg_armor': 20, 'weight': 2.5},
    'heavy':    {'leg_armor': 28, 'weight': 3.5},
    'elite':    {'leg_armor': 34, 'weight': 4.0},
    'lord':     {'leg_armor': 42, 'weight': 4.5},
}

# Capes are the modifier-fragile slot: they carry TWO independently-boosted stats and use the
# game's highest tier multiplier (x1.8). The old curve spanned only 16 points across all six
# tiers -- less than one legendary_plate roll (+12) -- so a tier-2 pauldron could out-armor a
# tier-6 one. Widened 2026-07-31 to satisfy the two-tier invariant; see check_curve_invariant().
# civilian arm_armor=0 is load-bearing: a 0 stat is modifier-immune (the engine guards on
# `num > 0`), and a nonzero value here trips the max(1,...) clamp for -3 protection cultures.
SHOULDER_BASELINES = {
    'civilian': {'body_armor': 2,  'arm_armor': 0,  'weight': 1.0},
    'light':    {'body_armor': 5,  'arm_armor': 3,  'weight': 3.0},
    'medium':   {'body_armor': 9,  'arm_armor': 6,  'weight': 5.0},
    'heavy':    {'body_armor': 13, 'arm_armor': 11, 'weight': 7.0},
    'elite':    {'body_armor': 19, 'arm_armor': 17, 'weight': 9.0},
    'lord':     {'body_armor': 25, 'arm_armor': 22, 'weight': 11.0},
}

SLOT_BASELINES = {
    'head': HEAD_BASELINES,
    'body': BODY_BASELINES,
    'arm': ARM_BASELINES,
    'leg': LEG_BASELINES,
    'shoulder': SHOULDER_BASELINES,
}

# Armor delta of each `legendary_*` ItemModifier, from Native/ModuleData/item_modifiers.xml.
# The engine applies it FLAT and INDEPENDENTLY to every nonzero armor stat, so a two-stat cape
# gets it twice. Pinned by test_legendary_table_matches_shipped_item_modifiers_xml.
LEGENDARY_ARMOR = {'cloth_unarmoured': 3, 'cloth': 5, 'leather': 7, 'chain': 9, 'plate': 12}

# extract_variant_number() maps roman numerals I..XVIII to +0..+17 and adds straight into the
# stat. Uncapped it eats the invariant margin (which runs as thin as +1), so cap it.
VARIANT_CAP = 1

# Per-slot loot-roll magnitude, overriding MATERIAL_MAP's modifier_group. material_type is NOT
# touched (it drives hit sounds/FX and stays lore-correct) -- the two attributes are read
# independently by the engine. Capes need this because the native ladder's deltas are calibrated
# for chest-scale bases (30-60), not cape-scale (2-25): +12 on a 42-armor cuirass is +29%, but
# +12 on a 12-armor pauldron is +100%.
# Since the kingdom-cap curve (#583, 2026-09-13) bracers and greaves sit at 0.6 and 0.5 of the
# chest cap, as compressed as capes, so all three extremity slots share one table: medium rolls
# cloth (+5), not leather (+7), because at a 35 cap the elite piece leads the medium one by only
# 7.6 (arm) or 6.3 (leg) points and a +7 roll plus the variant step would pass it. Heavy keeps
# chain for its modifier names (Loose / Rusty read fine on plate); it is never the low side of a
# judged pair, since the lord band equals the elite band on the cap curve.
_EXTREMITY_MODIFIER_GROUPS = {
    'civilian': 'cloth_unarmoured',  # +3
    'light':    'cloth',             # +5
    'medium':   'cloth',             # +5
    'heavy':    'chain',             # +9 -- naming-safe on Plate (Loose / Rusty)
    'elite':    'chain',             # +9
    'lord':     'chain',             # +9
}
SLOT_MODIFIER_GROUPS = {
    'shoulder': _EXTREMITY_MODIFIER_GROUPS,
    'arm': _EXTREMITY_MODIFIER_GROUPS,
    'leg': _EXTREMITY_MODIFIER_GROUPS,
}

# Every stat whose ladder must satisfy the two-tier invariant for a slot. Shoulder governs BOTH
# stats: arm_armor on capes was invisible to every analyzer (_get_primary_stat returned only
# body_armor), which is the blind spot that let the inversion ship.
GOVERNED_STATS = {
    'head':     ['head_armor'],
    'body':     ['body_armor', 'leg_armor'],
    'arm':      ['arm_armor'],
    'leg':      ['leg_armor'],
    'shoulder': ['body_armor', 'arm_armor'],
}

# Secondary stats take 60% of the cultural protection mod (see calculate_stats).
SECONDARY_STATS = {('body', 'leg_armor'), ('shoulder', 'arm_armor')}


def modifier_group_for(slot_type, tier):
    """The modifier_group (loot-roll table) for a slot+tier, honoring SLOT_MODIFIER_GROUPS."""
    return SLOT_MODIFIER_GROUPS.get(slot_type, {}).get(tier, MATERIAL_MAP[tier]['modifier_group'])


def governed_stats(slot_type):
    """Every stat whose ladder must satisfy the two-tier invariant for this slot."""
    return GOVERNED_STATS.get(slot_type, [])

# =============================================================================
# Cultural Modifiers
# =============================================================================

CULTURAL_MODS = {
    # Dwarves: master smiths, heavy armor
    'erebor':      {'protection': 4,  'weight_mult': 1.05},
    'iron_hills':  {'protection': 5,  'weight_mult': 1.10},

    # Elves: high protection, light weight (masterwork)
    'rivendell':   {'protection': 5,  'weight_mult': 0.70},
    'mirkwood':    {'protection': 5,  'weight_mult': 0.65},
    'lothlorien':  {'protection': 5,  'weight_mult': 0.70},

    # Men of the West/North: balanced
    'gondor':      {'protection': 1,  'weight_mult': 1.00},
    'rohan':       {'protection': -2, 'weight_mult': 0.90},
    'arnor':       {'protection': 2,  'weight_mult': 1.00},
    'dale':        {'protection': 1,  'weight_mult': 1.05},  # hardy northern men; was absent (ran on neutral default)

    # Evil Men
    'harad':       {'protection': -3, 'weight_mult': 0.85},
    'rhun':        {'protection': 0,  'weight_mult': 0.90},  # best cavalry in the game -> mobile (was 1.00)
    'umbar':       {'protection': -1, 'weight_mult': 0.90},
    'dunland':     {'protection': -2, 'weight_mult': 0.85},  # raiders/skirmishers by nature -> light (was 0.95)

    # Orcs & Uruk-hai
    'isengard':    {'protection': 2,  'weight_mult': 1.15},
    'mordor':      {'protection': -1, 'weight_mult': 1.10},
    'gundabad':    {'protection': 0,  'weight_mult': 1.15},
    'dol_guldur':  {'protection': 0,  'weight_mult': 1.10},

    # Special
    'thenn':       {'protection': -3, 'weight_mult': 1.05},
    'troll':       {'protection': 8,  'weight_mult': 2.00},
    'mercenary':   {'protection': 0,  'weight_mult': 1.00},
}

# =============================================================================
# Kingdom caps (the curve since 2026-09-13, #583)
# =============================================================================
# Each kingdom's armour power is one number, the chest (body_armor) of its elite band; the other
# slots and the bands below are fixed ratios of it. This replaced the flat SLOT_BASELINES +
# CULTURAL_MODS.protection model for every kingdom listed here (the tables above stay as the
# fallback for the civilian tier, weights, and cultures without a cap, such as the troll). The
# maintainer's table, verbatim; named hero armour (HERO_NAMES) is excluded from the curve.
# The lord band equals the elite band: nothing but named kit sits above a kingdom's cap.
KINGDOM_CAPS = {
    'erebor': 70, 'iron_hills': 70,
    'rivendell': 68, 'lindon': 68,
    'mirkwood': 63,
    'lothlorien': 60,           # no armour item today; recorded for when one exists
    'gondor': 57, 'rhun': 57, 'mordor_numenorean': 57, 'arnor': 57,
    'gundabad': 49,
    'dol_guldur': 46, 'khand': 46,   # Khand owns no armour item today
    'isengard': 45,
    'dale': 44, 'harad': 44, 'umbar': 44, 'mercenary': 44,
    'mordor_uruk': 43,
    'rohan': 40, 'dunland': 40,
    'mordor_orc': 38, 'mistymountainorcs': 38, 'goblin': 38,   # one shared orc kit
    'thenn': 35,
}
SLOT_CAP_RATIO = {'body': 1.0, 'head': 0.9, 'arm': 0.6, 'shoulder': 0.6, 'leg': 0.5}
BAND_RATIO = {'light': 0.40, 'medium': 0.64, 'heavy': 0.84, 'elite': 1.0, 'lord': 1.0}

# Sub-lines that share an Armory folder, routed by id prefix; anything else takes the folder.
# The mordor folder holds three kits at three caps and two misfiled strangers; the rhun folder
# holds Dol Guldur's Khamul line. First match wins.
LINE_PREFIXES = (
    ('sk_md_num_', 'mordor_numenorean'), ('sm_md_num_', 'mordor_numenorean'),
    ('sk_uruk_mordor_', 'mordor_uruk'),
    ('sk_md_mor_', 'mordor_orc'), ('sk_md_orc_', 'mordor_orc'), ('sk_gn_orc_', 'mordor_orc'),
    ('urukscout_', 'isengard'),
    ('ar_ardunian_', 'umbar'),
    ('sk_dg_', 'dol_guldur'),
)
FOLDER_DEFAULT_LINE = {'mordor': 'mordor_orc'}


def kingdom_key(item_id, culture):
    """The KINGDOM_CAPS key for an item, or None when the culture has no cap (legacy curve)."""
    for prefix, key in LINE_PREFIXES:
        if (item_id or '').startswith(prefix):
            return key
    key = FOLDER_DEFAULT_LINE.get(culture, culture)
    return key if key in KINGDOM_CAPS else None


def level_to_band(level):
    """The armour band a troop level sits in; one source for the writer and derive_armor_tiers."""
    if level <= 13:
        return 'light'
    if level <= 18:
        return 'medium'
    if level <= 30:
        return 'heavy'
    return 'elite'  # L31-51


def cap_value(cap, slot_type, tier):
    """round-half-up of cap x slot ratio x band ratio."""
    return int(math.floor(cap * SLOT_CAP_RATIO[slot_type] * BAND_RATIO[tier] + 0.5))


# =============================================================================
# Mesh-tier ladder (#609): which artist tiers a troop LEVEL may wear
# =============================================================================
#
# The stat band above prices an item by its lowest wearer. That is right for the item and wrong
# for the roster that put a recruit in it: six Gundabad `_lord_` chests, fanned out onto the
# level-11 snaga in 2026-05, anchored the whole lord line at 20 body armour (the medium line,
# worn from level 16, sat at 31). "Lord" is the mesh the artist named; nothing tied it to a
# level until this table (the maintainer's, 2026-09-16). Rows are inclusive upper bounds; TAOM
# levels run 6, 11, 16 ... so a level between rows takes the row above it. Lord kit is for
# level 41+ troops or lords (hero kit never anchors and is out of scope here).
MESH_TIER_ORDER = ('light', 'medium', 'heavy', 'elite', 'lord')
MESH_TIER_LADDER = (
    (6, ('light',)),
    (16, ('light', 'medium')),
    (21, ('medium',)),
    (26, ('heavy',)),
    (31, ('heavy', 'elite')),
    (36, ('elite',)),
    (10 ** 6, ('elite', 'lord')),
)
MESH_LADDER_SLOTS = ('Head', 'Body', 'Cape', 'Gloves', 'Leg')


def allowed_mesh_tiers(level):
    """The mesh tiers a troop of this level may wear, as a frozenset."""
    for bound, tiers in MESH_TIER_LADDER:
        if level <= bound:
            return frozenset(tiers)
    return frozenset(MESH_TIER_LADDER[-1][1])


def mesh_tier_of(item_id):
    """The tier an armour item's id encodes: light/medium/heavy/elite/lord, 'civilian' for `_civ`
    kit (off the ladder whoever wears it, checked first because `_civ_heavy_coat` exists), or
    None when the id carries no tier token (Dale's `_a03` lines)."""
    idl = (item_id or '').lower()
    if not idl:
        return None
    if '_civ' in idl or 'civilian' in idl:
        return 'civilian'
    for kw, tier in (('_lord', 'lord'), ('_elite', 'elite'), ('_heavy', 'heavy'),
                     ('_medium', 'medium'), ('_med', 'medium'), ('_light', 'light')):
        if kw in idl:
            return tier
    return None


def substitute_mesh_tiers(level):
    """The tiers to swap an over-dressed troop's item to, best first: every allowed tier that is
    not above the troop's STAT band (`level_to_band`), highest first, then the allowed tiers
    above the band, lowest first. A level-11 troop may wear medium, but its band is light, and
    a snaga put in `_med_a` anchors that medium variant to the light band, which is the bug
    this ladder exists to stop; so light comes first. Medium stays on the list because a line
    with no light variant still has a ladder-legal swap, and a medium mesh dragged to the
    light band beats a heavy one left there."""
    band = level_to_band(level)
    # The lord band prices as the elite band (BAND_RATIO), so lord kit is not above it.
    ceiling = MESH_TIER_ORDER.index('lord' if band == 'elite' else band)
    allowed = allowed_mesh_tiers(level)
    below = [t for t in reversed(MESH_TIER_ORDER) if t in allowed and MESH_TIER_ORDER.index(t) <= ceiling]
    above = [t for t in MESH_TIER_ORDER if t in allowed and MESH_TIER_ORDER.index(t) > ceiling]
    return tuple(below + above)


def mesh_ladder_violations(troops, exempt=()):
    """Every (troop, slot, item) where a battle set wears a tier the troop's level does not allow.

    `troops` is {id: {'level': int|None, 'sets': [{slot: item_id}], ...}}, the shape both the
    validator's troop index and fix_upgrade_armour_regressions.load_troops produce; a record's
    'file' and 'line' are carried through when present. Unlevelled and exempt troops are skipped,
    so are civilian-token and token-less items and non-armour slots. One row per distinct
    (troop, slot, item) with the number of sets wearing it; 'over' means the tier is above the
    highest allowed (it drags the mesh's stats down for every troop above), 'under' below the
    lowest (cosmetic). Sorted by troop, slot, item.
    """
    rows = []
    for tid in sorted(troops):
        rec = troops[tid]
        level = rec.get('level')
        if level is None or tid in exempt:
            continue
        allowed = allowed_mesh_tiers(int(level))
        order = [MESH_TIER_ORDER.index(t) for t in allowed]
        lo, hi = min(order), max(order)
        seen = {}
        for st in rec.get('sets') or ():
            for slot in MESH_LADDER_SLOTS:
                iid = st.get(slot)
                if not iid:
                    continue
                tier = mesh_tier_of(iid)
                if tier is None or tier == 'civilian' or tier in allowed:
                    continue
                key = (slot, iid)
                if key in seen:
                    seen[key]['sets'] += 1
                    continue
                seen[key] = {
                    'troop': tid, 'level': int(level), 'file': rec.get('file'), 'line': rec.get('line'),
                    'slot': slot, 'item': iid, 'tier': tier,
                    'allowed': tuple(t for t in MESH_TIER_ORDER if t in allowed),
                    'direction': 'over' if MESH_TIER_ORDER.index(tier) > hi else 'under',
                    'sets': 1,
                }
        rows.extend(seen[k] for k in sorted(seen))
    return rows

# =============================================================================
# Material Type Mapping (strict tier-based)
# =============================================================================

MATERIAL_MAP = {
    'civilian': {'material_type': 'Cloth',     'modifier_group': 'cloth'},
    'light':    {'material_type': 'Leather',   'modifier_group': 'leather'},
    'medium':   {'material_type': 'Chainmail', 'modifier_group': 'chain'},
    'heavy':    {'material_type': 'Plate',     'modifier_group': 'plate'},
    'elite':    {'material_type': 'Plate',     'modifier_group': 'plate'},
    'lord':     {'material_type': 'Plate',     'modifier_group': 'plate'},
}

# =============================================================================
# Tier Detection
# =============================================================================

# Hero names that always classify as 'lord' tier
HERO_NAMES = {
    'sauron', 'thranduil', 'legolas', 'faramir', 'boromir', 'imrahil',
    'theodred', 'theoden', 'golasgil', 'hirluin', 'angbor', 'forlong',
    'dain', 'thorin', 'glorfindel', 'elrond', 'galadriel', 'celeborn',
    'haldir', 'eomer', 'eowyn', 'aragorn', 'gandalf', 'saruman',
    'witch king', 'nazgul', "nazgul's", "n\u00e2zgul", "n\u00e2zgul's",
    'khamul', 'gothmog', 'lurtz', 'sharku',
    'grimbold', 'erkenbrand', 'gamling', 'hama',
}

# Hero / boss / fixed-display item id substrings excluded from re-stat (in addition to HERO_NAMES,
# matched against the display name). Mirrors analyze_armor_balance.EXCLUDE_ID_SUBSTRINGS.
# 'md_num' (Black Numenorean) was here from 2026-08-17 to 2026-09-13 because that set anchors
# its stats to the wearer's LEVEL rather than to the mesh's tier token, and the keyword detector
# would have flattened it. The kingdom-cap curve tiers a worn item by its wearer's level
# (--tier-source roster-first), which is what the set wanted all along, and it has its own cap
# (mordor_numenorean, 57), so the exclusion is lifted (#583).
EXCLUDE_ID_SUBSTRINGS = ('lotr_troll', 'cave_troll', 'glorfindel', 'gf_', 'dain_crown')


# Generated troop kit whose display names carry a hero's name: the Dol Guldur "Khamul ..." line
# (194 `sk_dg_khml_*` items worn by the dg_khamul_* troops) is named after its captain, not worn by
# him. A display-name match on HERO_NAMES must not exclude it; Khamul's own kit is `khamul_*`.
# Mirrored in analyze_armor_balance.is_excluded; a test pins the two together.
HERO_NAME_FALSE_POSITIVE_PREFIXES = ('sk_dg_khml_',)


def is_excluded(item_id, display_name):
    """True for hero / boss / fixed-display items that must NOT be re-stated onto the troop curve."""
    idl = (item_id or '').lower()
    nl = (display_name or '').lower()
    if any(s in idl for s in EXCLUDE_ID_SUBSTRINGS):
        return True
    if idl.startswith(HERO_NAME_FALSE_POSITIVE_PREFIXES):
        return False
    return any(h and h in nl for h in HERO_NAMES)


def load_roster_tier_map():
    """Load {item_id: tier} from the derive_armor_tiers.py map (tools/data/armor_roster_tiers.json).

    Returns ({item_id: tier}, generatedAt) — tier is the roster/id-derived tier ('light'..'lord') or
    None for unworn items. Used by --tier-source roster so the apply uses authoritative roster tiers
    instead of the brittle name-keyword detector.
    """
    map_path = os.path.join(os.path.dirname(__file__), 'data', 'armor_roster_tiers.json')
    if not os.path.exists(map_path):
        return None, None
    import json as _json
    with open(map_path, 'r', encoding='utf-8') as f:
        data = _json.load(f)
    return {iid: rec.get('tier') for iid, rec in data.get('items', {}).items()}, data.get('generatedAt')


def load_roster_records():
    """The full derive_armor_tiers.py records ({item_id: record}) for --tier-source roster-first,
    which needs anchorLevel and tierSource, not just the tier column (anchor first since #583)."""
    map_path = os.path.join(os.path.dirname(__file__), 'data', 'armor_roster_tiers.json')
    if not os.path.exists(map_path):
        return None, None
    import json as _json
    with open(map_path, 'r', encoding='utf-8') as f:
        data = _json.load(f)
    return data.get('items', {}), data.get('generatedAt')

# Roman numeral pattern for variant detection
ROMAN_NUMERAL_RE = re.compile(r'\b(I{1,3}|IV|V|VI{0,3}|IX|X{0,3}I{0,3}V?I{0,3})\b')

# Color variant pattern
COLOR_VARIANT_RE = re.compile(
    r'\s*[-–]\s*(Red|Blue|Green|Gold|Silver|Bronze|Maroon|Brown|Orange|Black|Yellow|Grey|White|Purple)\s*$',
    re.IGNORECASE
)


def extract_variant_number(display_name):
    """
    Extract the variant number from a display name.
    Returns 0-based index (I=0, II=1, III=2, etc.) or 0 if no variant found.
    """
    # Check for roman numerals at end of name
    roman_map = {
        'I': 0, 'II': 1, 'III': 2, 'IV': 3, 'V': 4,
        'VI': 5, 'VII': 6, 'VIII': 7, 'IX': 8, 'X': 9,
        'XI': 10, 'XII': 11, 'XIII': 12, 'XIV': 13, 'XV': 14,
        'XVI': 15, 'XVII': 16, 'XVIII': 17,
    }

    # Strip color variant first
    name = COLOR_VARIANT_RE.sub('', display_name).strip()

    # Find last roman numeral in name
    matches = list(ROMAN_NUMERAL_RE.finditer(name))
    if matches:
        last_match = matches[-1]
        roman = last_match.group(1)
        if roman in roman_map:
            return roman_map[roman]

    # Check for letter suffix (a, b, c, d pattern in IDs)
    # This is handled separately via item_id

    return 0


def extract_variant_from_id(item_id):
    """
    Extract variant index from item ID suffix like _a, _b, _c, _d.
    Returns 0-based index or 0 if not found.
    """
    match = re.search(r'_([a-f])(\d?)$', item_id)
    if match:
        letter = match.group(1)
        return ord(letter) - ord('a')
    return 0


def detect_tier(item_id, display_name, current_values, slot_type):
    """
    Detect armor tier from item name/id with keyword priority + value fallback.
    Returns one of: civilian, light, medium, heavy, elite, lord
    """
    name_lower = display_name.lower()
    id_lower = item_id.lower()
    combined = name_lower + ' ' + id_lower

    # Priority 1: Civilian
    civilian_keywords = ['civilian', 'dress', 'noble coat', 'noble jerkin',
                         'noble tunic', 'tunic', 'jerkin', 'robe']
    # Exclude "robe" when it's part of elven military gear or elite gear
    for kw in civilian_keywords:
        if kw in name_lower:
            # Don't classify elven robes as civilian - they are military
            if kw == 'robe' and any(elf in combined for elf in ['rivendell', 'noldor', 'mirkwood', 'lothlorien', 'lorien', 'elite', 'pauldron']):
                continue
            # Don't classify "Noble" items that are clearly military
            if 'noble' in kw and any(mil in combined for mil in ['armour', 'armor', 'helm', 'bracer', 'gauntlet']):
                continue
            return 'civilian'

    # Priority 2: Lord (hero names + keywords)
    for hero in HERO_NAMES:
        if hero in name_lower:
            return 'lord'
    lord_keywords = [' lord ', 'lord chest', 'lord helm', "lord's", "king's",
                     "captain's", 'captain chest', 'captain helm']
    for kw in lord_keywords:
        if kw in combined:
            return 'lord'
    # ID-based lord detection
    if '_lord_' in id_lower or '_lord' == id_lower[-5:]:
        return 'lord'

    # Priority 3: Elite
    #
    # 'black numenorean' was REMOVED here 2026-08-17. It was a line-name keyword,
    # not a tier keyword, and once the sk_md_num_ / sm_md_num_ set shipped it
    # matched all 78 of those items and nothing else in the Armory: every one of
    # their display names is "[Mordor] Black Numenorean <something>", so a LIGHT
    # hood was classified elite. That mis-tiered 45 of 78 and would have made
    # `rebalance_armor.py --apply --cultures mordor` flatten the whole set onto
    # the elite row. With the keyword gone the id-based _light_/_med_/_heavy_/
    # _elite_ tokens decide, which is correct for that set. Verified before
    # removal that it matched no other item.
    elite_keywords = ['elite', 'gold platemail', 'palace guard', 'citadel',
                      'citidel', 'fountain guard', 'fountain helm',
                      'swan knight', 'swanknight', 'royal guard',
                      'serpent guard', 'berserker']
    for kw in elite_keywords:
        if kw in combined:
            return 'elite'
    if '_elite_' in id_lower:
        return 'elite'

    # Priority 4: Heavy
    heavy_keywords = ['heavy', 'platemail', 'plate armour', 'plate armor',
                      'plate helmet', 'plate helm', 'plate greaves',
                      'plate gauntlet', 'heavy chainmail', 'heavy chain',
                      'heavy plate']
    for kw in heavy_keywords:
        if kw in combined:
            return 'heavy'
    if '_heavy_' in id_lower:
        return 'heavy'

    # Priority 5: Medium
    medium_keywords = ['medium', 'chainmail', 'chain mail', 'scalemail',
                       'scale mail', 'scale armour', 'scale armor']
    for kw in medium_keywords:
        if kw in combined:
            return 'medium'
    if '_med_' in id_lower or '_medium_' in id_lower or '_chain_' in id_lower:
        return 'medium'

    # Priority 6: Light
    light_keywords = ['light', 'leather', 'mail armour',
                      'mail armor', 'militia', 'scout', 'cloth armor',
                      'cloth armour', 'padded']
    for kw in light_keywords:
        if kw in combined:
            return 'light'
    if '_light_' in id_lower or '_leather_' in id_lower:
        return 'light'

    # Fallback: use current armor value to classify
    primary_stat = _get_primary_stat(current_values, slot_type)
    if primary_stat is not None:
        if primary_stat <= 12:
            return 'civilian'
        elif primary_stat <= 25:
            return 'light'
        elif primary_stat <= 35:
            return 'medium'
        elif primary_stat <= 45:
            return 'heavy'
        elif primary_stat <= 55:
            return 'elite'
        else:
            return 'lord'

    # Default to medium if nothing matches
    return 'medium'


def _get_primary_stat(values, slot_type):
    """The SINGLE stat used for tier detection, weight laddering and report columns.

    NOT the full governed set -- use governed_stats() for any balance judgment, or shoulder
    arm_armor stays invisible (the blind spot behind the 2026-07-31 cape inversion).
    """
    stats = GOVERNED_STATS.get(slot_type)
    return values.get(stats[0]) if stats else None


def detect_culture(filepath, item_name):
    """
    Detect culture from directory name, with override from item name tags.
    Items like [Iron Hills] in erebor directory should use iron_hills culture.
    """
    # Get culture from directory
    dir_name = os.path.basename(os.path.dirname(filepath))

    # Check for culture tag override in item name
    name_lower = item_name.lower()
    if '[iron hills]' in name_lower or '[ironfist]' in name_lower:
        return 'iron_hills'
    if '[erebor]' in name_lower and dir_name == 'iron_hills':
        return 'erebor'

    return dir_name


def get_display_name(name_attr):
    """Extract display name from localization tag like {=tag}Display Name."""
    if '}' in name_attr:
        return name_attr.split('}', 1)[1]
    return name_attr


# =============================================================================
# Stat Calculation
# =============================================================================

def calculate_stats(tier, slot_type, culture, variant_num=0, item_id=None):
    """
    Calculate armor stats for a given tier, slot, and culture.
    variant_num adds +1 per step for numbered variants.
    Returns dict of {stat_name: value, 'weight': value, 'material_type': str, 'modifier_group': str}

    On the kingdom-cap curve (every culture in KINGDOM_CAPS, combat tiers) the slot's PRIMARY stat
    is cap x slot ratio x band ratio; the row's secondary (a chest's leg_armor, a cape's arm_armor)
    keeps the legacy row's secondary-to-primary proportion for that tier. The writer then scales
    an ITEM's own secondaries with its primary instead, so each folder keeps its convention (a
    Gondor chest carries arm_armor, a Rhun chest leg_armor). item_id routes the sub-lines that
    share a folder. The civilian tier, weights and cultures without a cap stay on the
    SLOT_BASELINES model below.
    """
    baselines = SLOT_BASELINES[slot_type]
    baseline = baselines[tier]
    mods = CULTURAL_MODS.get(culture, {'protection': 0, 'weight_mult': 1.0})

    key = kingdom_key(item_id, culture) if tier in BAND_RATIO else None
    if key is not None:
        stat_name = GOVERNED_STATS[slot_type][0]
        primary = max(1, cap_value(KINGDOM_CAPS[key], slot_type, tier) + min(variant_num, VARIANT_CAP))
        result = {stat_name: primary}
        for stat, base_val in baseline.items():
            if stat == 'weight' or stat == stat_name:
                continue
            base_primary = baseline[stat_name]
            result[stat] = (int(math.floor(primary * base_val / base_primary + 0.5))
                            if base_val > 0 and base_primary > 0 else 0)
        result['weight'] = round(baseline['weight'] * mods['weight_mult'], 1)
        result['material_type'] = MATERIAL_MAP[tier]['material_type']
        result['modifier_group'] = modifier_group_for(slot_type, tier)
        return result

    result = {}
    for stat, base_val in baseline.items():
        if stat == 'weight':
            # Weight uses multiplicative modifier
            result['weight'] = round(base_val * mods['weight_mult'], 1)
        else:
            # Protection stats use additive modifier + variant progression
            # Secondary stats (leg on body, arm on shoulder) get 60% of protection mod
            if (slot_type, stat) in SECONDARY_STATS:
                prot_mod = int(round(mods['protection'] * 0.6))
            else:
                prot_mod = mods['protection']
            # A baseline of exactly 0 means "this slot does not carry this stat" (cape arm armor
            # below heavy). It must stay 0 -- the engine's `num > 0` guard makes a 0 stat
            # modifier-immune, which is what keeps low tiers out of the ladder entirely.
            # variant_num is capped so a `Pauldron VI` cannot climb out of its own tier.
            if base_val == 0:
                result[stat] = 0
            else:
                result[stat] = max(1, base_val + prot_mod + min(variant_num, VARIANT_CAP))

    # Material type from tier. material_type stays on the MATERIAL_MAP ladder (hit sounds/FX);
    # modifier_group may be re-scoped per slot so the loot roll fits the slot's stat scale.
    mat = MATERIAL_MAP[tier]
    result['material_type'] = mat['material_type']
    result['modifier_group'] = modifier_group_for(slot_type, tier)

    return result


def tier_from_value(primary, slot_type, culture, item_id=None):
    """Map an item's CURRENT primary armor to the nearest combat tier (for --weights-only laddering).

    Used when we want weight to track the item's existing (correct) armor without re-stating armor or
    relying on the brittle name-keyword detector. Compares the value to each tier's baseline+mod target
    and returns the closest combat tier. Pass item_id so a prefix-routed sub-line (sk_dg_ in the rhun
    folder, the Mordor lines) is judged on its own cap, not the folder's.
    """
    if primary is None:
        return 'medium'
    best, best_d = 'light', None
    for tier in ['light', 'medium', 'heavy', 'elite', 'lord']:
        target = _get_primary_stat(calculate_stats(tier, slot_type, culture, item_id=item_id), slot_type)
        if target is None:
            continue
        d = abs(primary - target)
        if best_d is None or d < best_d:
            best, best_d = tier, d
    return best


# =============================================================================
# Regex-based XML Replacement
# =============================================================================

def _inside_comment(text, pos):
    """True when `pos` falls inside an unclosed <!-- ... --> that opened before it."""
    start = text.rfind('<!--', 0, pos)
    return start != -1 and text.find('-->', start, pos) == -1


def apply_changes_via_regex(filepath, item_changes, backup_tag=None):
    """
    Apply stat/weight/material changes to XML file using regex.
    Preserves all formatting, comments, whitespace, the BOM and the line endings (a binary
    round-trip: the Armory mixes LF and CRLF files, and a text-mode write rewrote whole files).
    With backup_tag, a `<file>.bak-<tag>` copy of the original is taken once, before the first
    write; a `.bak-*` name never matches the engine's `*.xml` glob.

    item_changes: dict of item_id -> {
        'weight': new_weight,
        'body_armor': new_val, 'arm_armor': new_val, etc.,
        'material_type': 'Plate', 'modifier_group': 'plate'
    }
    """
    raw = open(filepath, 'rb').read()
    content = raw.decode('utf-8')  # a BOM survives inside the string and is written back as is

    for item_id, changes in item_changes.items():
        # Find the item block: from id="item_id" to </Item>, skipping any match that sits inside
        # an XML comment (the Armory keeps commented-out <Item> blocks for reference; editing the
        # dead copy would report success and leave the live item untouched).
        item_pattern = re.compile(
            r'(id="' + re.escape(item_id) + r'")(.*?)(</Item>)',
            re.DOTALL
        )
        item_match = next((m for m in item_pattern.finditer(content)
                           if not _inside_comment(content, m.start())), None)
        if not item_match:
            print(f"  WARNING: Could not find item {item_id} in {filepath}")
            continue

        item_block = item_match.group(2)
        new_block = item_block

        # Replace weight on <Item> element (top-level attribute)
        if 'weight' in changes:
            weight_pattern = re.compile(r'(weight=")([^"]*?)(")')
            new_block = weight_pattern.sub(
                lambda m: m.group(1) + str(changes['weight']) + m.group(3),
                new_block, count=1
            )

        # Replace armor stats on <Armor> element
        for stat in ['head_armor', 'body_armor', 'arm_armor', 'leg_armor']:
            if stat in changes:
                stat_pattern = re.compile(
                    r'(' + re.escape(stat) + r'=")([^"]*?)(")'
                )
                # Only replace if the attribute already exists
                if stat_pattern.search(new_block):
                    new_block = stat_pattern.sub(
                        lambda m, v=changes[stat]: m.group(1) + str(v) + m.group(3),
                        new_block, count=1
                    )

        # Replace material_type and modifier_group
        if 'material_type' in changes:
            mat_pattern = re.compile(r'(material_type=")([^"]*?)(")')
            if mat_pattern.search(new_block):
                new_block = mat_pattern.sub(
                    lambda m: m.group(1) + changes['material_type'] + m.group(3),
                    new_block, count=1
                )

        if 'modifier_group' in changes:
            mod_pattern = re.compile(r'(modifier_group=")([^"]*?)(")')
            if mod_pattern.search(new_block):
                new_block = mod_pattern.sub(
                    lambda m: m.group(1) + changes['modifier_group'] + m.group(3),
                    new_block, count=1
                )

        content = content[:item_match.start(2)] + new_block + content[item_match.end(2):]

    out = content.encode('utf-8')
    if out == raw:
        return
    # Parse before writing: a substitution that broke the document must never reach the tree.
    ET.fromstring(out[3:] if out.startswith(b'\xef\xbb\xbf') else out)
    if backup_tag:
        backup = filepath + '.bak-' + backup_tag
        if not os.path.exists(backup):
            with open(backup, 'wb') as f:
                f.write(raw)
    with open(filepath, 'wb') as f:
        f.write(out)


# =============================================================================
# File Processing
# =============================================================================

def parse_current_values(armor_elem, slot_type):
    """Extract current armor values from an <Armor> XML element."""
    values = {}
    for attr in ['head_armor', 'body_armor', 'arm_armor', 'leg_armor']:
        val = armor_elem.get(attr)
        if val is not None:
            values[attr] = int(val)
    return values


def _roster_first_tier(roster_map, item_id):
    """The band for --tier-source roster-first: the lowest troop wearer's level decides; the id
    keyword decides only for kit no troop wears; civilian keyword kit and kit with neither is
    skipped (None). roster_map values may be the tier string (legacy map) or the full record."""
    rec = roster_map.get(item_id)
    if rec is None:
        return None
    if isinstance(rec, str):
        return 'elite' if rec == 'lord' else rec
    kw_tier = rec.get('tier') if rec.get('tierSource') == 'id-keyword' else None
    if kw_tier == 'civilian':
        return None
    anchor = rec.get('anchorLevel')
    if anchor is not None:
        return level_to_band(int(anchor))
    return kw_tier


def process_file(filepath, slot_type, dry_run=True, weights_only=False,
                 tier_source='keyword', roster_map=None, no_lower_armor=False,
                 materials_only=False, keep_materials=False, keep_weights=False,
                 keep_material_type=False, backup_tag=None):
    """Process a single armor XML file. Returns list of change records.

    tier_source='roster' picks each item's tier from the derive_armor_tiers.py map (authoritative —
    the level of the troop that wears the item) instead of the brittle name-keyword detector; unworn
    and hero/boss items are skipped (left untouched). tier_source='keyword' is the legacy detector.
    tier_source='roster-first' (the kingdom-cap curve, #583) takes the band of the item's LOWEST
    troop wearer even when the id carries a tier keyword (the Fountain Guard helmet is `_heavy_`
    and worn only at level 46), falls back to the keyword for unworn kit, and skips civilian kit,
    hero kit and kit with neither; it needs the full-record map (load_roster_records).

    keep_weights=True leaves weight alone in a full re-stat; keep_material_type=True leaves
    material_type (hit sounds and FX) alone while modifier_group (the loot table) still follows the
    slot's ladder. backup_tag names the one-time `.bak-<tag>` copy the writer takes per file.
    On the kingdom-cap curve a secondary stat the item carries is scaled by the same factor as
    its primary, so a folder's convention survives the restat.

    weights_only=True writes ONLY the weight (armor + material untouched). Combined with the keyword
    source it ladders by the item's current armor value and is guarded to currently-monolithic slots;
    combined with the roster source it sets weight by the roster tier (no guard needed — roster tiers
    already vary). Default (weights_only=False) is a full re-stat: armor + weight + material.

    materials_only=True writes ONLY material_type + modifier_group — no armor, no weight. This is the
    zero-combat-impact pass that puts each item on the loot-roll table its tier actually warrants
    (the flat legendary bonus is what lets a low-tier item out-armor a high-tier one). Hero/boss
    items are skipped in this mode regardless of tier_source.

    keep_materials=True freezes material_type + modifier_group in a full re-stat. This used to be
    implied by no_lower_armor, which is how ~1000 items kept a `plate` group they never earned —
    the 2026-06-30 sweep ran --no-lower-armor across dale/rohan/arnor/mirkwood.
    """
    roster_map = roster_map or {}
    filename = os.path.basename(filepath)

    tree = ET.parse(filepath)
    root = tree.getroot()

    changes = []
    item_changes = {}  # For regex writing

    # weights-only safety: ONLY ladder a slot that is CURRENTLY monolithic-weight (all items one
    # weight), so we can never collapse an already-varied slot (e.g. harad shoulder, whose uniform
    # armor would otherwise map every item to one tier and one weight). A non-monolithic slot is left
    # untouched; a monolithic slot with uniform armor stays monolithic (a no-op, never a regression).
    ladder_this_slot = True
    if weights_only:
        cur_weights = []
        for it in root.findall('.//Item'):
            if it.find('.//Armor') is not None and it.get('weight') is not None:
                try:
                    cur_weights.append(float(it.get('weight')))
                except ValueError:
                    pass
        ladder_this_slot = len(set(cur_weights)) <= 1 and len(cur_weights) >= 4

    for item in root.findall('.//Item'):
        item_id = item.get('id', '')
        item_name = get_display_name(item.get('name', ''))
        current_weight = float(item.get('weight', '0'))

        # Find the Armor element
        armor_elem = item.find('.//Armor')
        if armor_elem is None:
            continue

        current_values = parse_current_values(armor_elem, slot_type)
        current_material = armor_elem.get('material_type', '')
        current_modifier = armor_elem.get('modifier_group', '')

        # Detect culture
        culture = detect_culture(filepath, item_name)
        variant_num = 0

        # --- pick the tier and the write mode ('skip' | 'weight' | 'material' | 'full') ---
        if materials_only:
            # Loot-roll retag only. Never touch hero/boss kit, whose material is hand-authored.
            if is_excluded(item_id, item_name):
                tier, mode = 'medium', 'skip'
            elif tier_source == 'roster':
                roster_tier = roster_map.get(item_id)
                if roster_tier is None:
                    tier, mode = 'medium', 'skip'   # unworn: no authoritative tier
                else:
                    tier = 'elite' if roster_tier == 'lord' else roster_tier
                    mode = 'material'
            else:
                tier, mode = detect_tier(item_id, item_name, current_values, slot_type), 'material'
        elif tier_source == 'roster-first':
            band = None if is_excluded(item_id, item_name) else _roster_first_tier(roster_map, item_id)
            if band is None:
                tier, mode = 'medium', 'skip'   # hero/boss, civilian, or nothing to anchor on
            else:
                tier, mode = band, ('weight' if weights_only else 'full')
        elif tier_source == 'roster':
            roster_tier = roster_map.get(item_id)
            if isinstance(roster_tier, dict):
                roster_tier = roster_tier.get('tier')
            if is_excluded(item_id, item_name) or roster_tier is None:
                tier, mode = 'medium', 'skip'   # hero/boss or unworn: leave untouched
            else:
                # Troops cap at elite (owner decision: elite = levels 31-51; 'lord' is hero-only).
                tier = 'elite' if roster_tier == 'lord' else roster_tier
                mode = 'weight' if weights_only else 'full'
        elif weights_only:
            if ladder_this_slot:
                tier = tier_from_value(_get_primary_stat(current_values, slot_type), slot_type, culture,
                                       item_id=item_id)
                mode = 'weight'
            else:
                tier, mode = 'medium', 'skip'   # slot already weight-varied; never collapse it
        else:
            tier = detect_tier(item_id, item_name, current_values, slot_type)
            variant_num = extract_variant_number(item_name) or extract_variant_from_id(item_id)
            mode = 'full'

        # --- compute the new stats for the chosen mode ---
        if mode == 'skip':
            new_values, new_weight = {}, current_weight
            new_material, new_modifier, has_change = current_material, current_modifier, False
        else:
            new_stats = calculate_stats(tier, slot_type, culture, variant_num, item_id=item_id)
            new_weight = current_weight if keep_weights else new_stats['weight']
            if mode == 'weight':
                new_values, new_material, new_modifier = {}, current_material, current_modifier
                has_change = new_weight != current_weight
            elif mode == 'material':
                # Loot-roll retag only: armor and weight are held at their current values.
                new_values, new_weight = {}, current_weight
                new_material, new_modifier = new_stats['material_type'], new_stats['modifier_group']
                has_change = (new_material != current_material or new_modifier != current_modifier)
            else:  # full re-stat: armor + weight + material
                new_values = {}
                for s in ['head_armor', 'body_armor', 'arm_armor', 'leg_armor']:
                    if s in current_values and s in new_stats:
                        tgt = new_stats[s]
                        # no_lower_armor: never reduce a stat (raise under-tiered items, keep the rest).
                        new_values[s] = max(tgt, current_values[s]) if no_lower_armor else tgt
                # Kingdom-cap curve: the row carries the primary only; every other stat the item
                # declares keeps its ratio to the primary (a 0 stays 0), so a Gondor chest keeps
                # its arm_armor convention and a cape its arm_armor.
                primary_stat = GOVERNED_STATS[slot_type][0]
                if kingdom_key(item_id, culture) is not None and primary_stat in new_values:
                    old_p, new_p = current_values.get(primary_stat, 0), new_values[primary_stat]
                    for s, cur in current_values.items():
                        if s == primary_stat:
                            continue
                        scaled = int(math.floor(cur * new_p / old_p + 0.5)) if old_p > 0 and cur > 0 else cur
                        new_values[s] = max(scaled, cur) if no_lower_armor else scaled
                # material/modifier is a CURVE-CORRECTNESS attribute, not an armor value, so
                # no_lower_armor must NOT freeze it — that is how ~1000 items kept an unearned
                # `plate` group. Use --keep-materials for the explicit opt-out.
                if keep_materials:
                    new_material, new_modifier = current_material, current_modifier
                else:
                    new_material = current_material if keep_material_type else new_stats['material_type']
                    new_modifier = new_stats['modifier_group']
                has_change = (
                    new_weight != current_weight or
                    new_material != current_material or
                    new_modifier != current_modifier or
                    any(new_values.get(s) != current_values.get(s) for s in current_values)
                )

        # Record
        change_record = {
            'file': os.path.join(os.path.basename(os.path.dirname(filepath)), filename),
            'id': item_id,
            'name': item_name,
            'culture': culture,
            'slot': slot_type,
            'tier': tier,
            'variant': variant_num,
            'status': 'CHANGED' if has_change else 'UNCHANGED',
            'old_values': current_values,
            'new_values': new_values,
            'old_weight': current_weight,
            'new_weight': new_weight,
            'old_material': current_material,
            'new_material': new_material,
            'old_modifier': current_modifier,
            'new_modifier': new_modifier,
        }
        changes.append(change_record)

        if has_change:
            if mode == 'weight':
                item_changes[item_id] = {'weight': new_weight}
            elif mode == 'material':
                # Strictly materials: writing 'weight' here would round-trip weight="5" to "5.0".
                item_changes[item_id] = {'material_type': new_material,
                                         'modifier_group': new_modifier}
            else:
                write_changes = dict(new_values)
                write_changes['weight'] = new_weight
                write_changes['material_type'] = new_material
                write_changes['modifier_group'] = new_modifier
                item_changes[item_id] = write_changes

    # Apply via regex
    if not dry_run and item_changes:
        apply_changes_via_regex(filepath, item_changes, backup_tag=backup_tag)

    return changes


# =============================================================================
# Reporting
# =============================================================================

def print_report(all_changes):
    """Print a formatted report of all changes."""
    changed = [c for c in all_changes if c['status'] == 'CHANGED']
    unchanged = [c for c in all_changes if c['status'] == 'UNCHANGED']

    print(f"\n{'='*130}")
    print(f"ARMOR REBALANCING REPORT")
    print(f"{'='*130}")
    print(f"Total items: {len(all_changes)}")
    print(f"Changed: {len(changed)}, Unchanged: {len(unchanged)}")

    # Summary by culture
    print(f"\n--- BY CULTURE ---")
    by_culture = defaultdict(lambda: defaultdict(int))
    for c in all_changes:
        by_culture[c['culture']][c['tier']] += 1
        by_culture[c['culture']]['total'] += 1

    print(f"  {'Culture':<14} {'Total':>6} {'Civ':>5} {'Light':>6} {'Med':>5} {'Heavy':>6} {'Elite':>6} {'Lord':>5}")
    print(f"  {'-'*60}")
    for culture in sorted(by_culture.keys()):
        t = by_culture[culture]
        print(f"  {culture:<14} {t['total']:>6} {t.get('civilian',0):>5} {t.get('light',0):>6} "
              f"{t.get('medium',0):>5} {t.get('heavy',0):>6} {t.get('elite',0):>6} {t.get('lord',0):>5}")

    # Detail by slot
    for slot in ['head', 'body', 'arm', 'leg', 'shoulder']:
        slot_changes = [c for c in all_changes if c['slot'] == slot]
        if not slot_changes:
            continue

        print(f"\n{'='*130}")
        print(f"{slot.upper()} ARMOR ({len(slot_changes)} items)")
        print(f"{'='*130}")

        for tier in TIERS:
            tier_items = [c for c in slot_changes if c['tier'] == tier]
            if not tier_items:
                continue

            print(f"\n  --- {tier.upper()} ({len(tier_items)} items) ---")

            if slot == 'body':
                print(f"  {'Name':<50} {'Culture':<12} {'Var':>3} {'Body':>5} {'Arm':>5} {'Wgt':>6} {'Mat':<10} {'Old->New':>20}")
                print(f"  {'-'*118}")
            elif slot == 'head':
                print(f"  {'Name':<50} {'Culture':<12} {'Var':>3} {'Head':>5} {'Wgt':>6} {'Mat':<10} {'Old->New':>20}")
                print(f"  {'-'*108}")
            elif slot == 'arm':
                print(f"  {'Name':<50} {'Culture':<12} {'Var':>3} {'Arm':>5} {'Wgt':>6} {'Mat':<10} {'Old->New':>20}")
                print(f"  {'-'*108}")
            elif slot == 'leg':
                print(f"  {'Name':<50} {'Culture':<12} {'Var':>3} {'Leg':>5} {'Wgt':>6} {'Mat':<10} {'Old->New':>20}")
                print(f"  {'-'*108}")
            elif slot == 'shoulder':
                print(f"  {'Name':<50} {'Culture':<12} {'Var':>3} {'Body':>5} {'Arm':>5} {'Wgt':>6} {'Mat':<10} {'Old->New':>20}")
                print(f"  {'-'*118}")

            for c in sorted(tier_items, key=lambda x: (x['culture'], x['name'])):
                old_primary = _get_primary_stat(c['old_values'], slot)
                new_primary = _get_primary_stat(c['new_values'], slot)
                old_str = str(old_primary) if old_primary is not None else '—'
                new_str = str(new_primary) if new_primary is not None else '—'
                if not c['new_values'] and c['old_modifier'] != c['new_modifier']:
                    # materials-only pass: the armor columns don't move, the loot table does
                    delta = f"{c['old_modifier']}->{c['new_modifier']}"
                else:
                    delta = f"{old_str}->{new_str}"

                marker = " ***" if c['status'] == 'CHANGED' else ""
                name_display = c['name'][:50]
                # modes that write no armor (weight/material) still show the item's real stats
                shown = c['new_values'] or c['old_values']

                if slot == 'body':
                    print(f"  {name_display:<50} {c['culture']:<12} {c['variant']:>3} "
                          f"{shown.get('body_armor','—'):>5} {shown.get('arm_armor','—'):>5} "
                          f"{c['new_weight']:>6.1f} {c['new_material']:<10} {delta:>20}{marker}")
                elif slot == 'head':
                    print(f"  {name_display:<50} {c['culture']:<12} {c['variant']:>3} "
                          f"{shown.get('head_armor','—'):>5} "
                          f"{c['new_weight']:>6.1f} {c['new_material']:<10} {delta:>20}{marker}")
                elif slot == 'arm':
                    print(f"  {name_display:<50} {c['culture']:<12} {c['variant']:>3} "
                          f"{shown.get('arm_armor','—'):>5} "
                          f"{c['new_weight']:>6.1f} {c['new_material']:<10} {delta:>20}{marker}")
                elif slot == 'leg':
                    print(f"  {name_display:<50} {c['culture']:<12} {c['variant']:>3} "
                          f"{shown.get('leg_armor','—'):>5} "
                          f"{c['new_weight']:>6.1f} {c['new_material']:<10} {delta:>20}{marker}")
                elif slot == 'shoulder':
                    print(f"  {name_display:<50} {c['culture']:<12} {c['variant']:>3} "
                          f"{shown.get('body_armor','—'):>5} {shown.get('arm_armor','—'):>5} "
                          f"{c['new_weight']:>6.1f} {c['new_material']:<10} {delta:>20}{marker}")

    # Warnings: items with large changes
    big_changes = []
    for c in changed:
        old_p = _get_primary_stat(c['old_values'], c['slot'])
        new_p = _get_primary_stat(c['new_values'], c['slot'])
        if old_p is not None and new_p is not None:
            delta = abs(new_p - old_p)
            if delta > 20:
                big_changes.append((c, delta))

    if big_changes:
        print(f"\n{'='*130}")
        print(f"WARNING: {len(big_changes)} items with primary stat change > 20 points")
        print(f"{'='*130}")
        for c, delta in sorted(big_changes, key=lambda x: x[1], reverse=True)[:50]:
            old_p = _get_primary_stat(c['old_values'], c['slot'])
            new_p = _get_primary_stat(c['new_values'], c['slot'])
            print(f"  {c['name']:<50} {c['culture']:<12} {c['slot']:<8} {c['tier']:<8} "
                  f"old={old_p} new={new_p} delta={new_p - old_p:+d}")


def export_csv(all_changes, csv_path):
    """Export tier classification to CSV for manual review."""
    with open(csv_path, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow([
            'File', 'ID', 'Name', 'Culture', 'Slot', 'Tier', 'Variant',
            'Old Primary', 'New Primary', 'Delta',
            'Old Weight', 'New Weight',
            'Old Material', 'New Material',
            'Status'
        ])
        for c in all_changes:
            old_p = _get_primary_stat(c['old_values'], c['slot'])
            new_p = _get_primary_stat(c['new_values'], c['slot'])
            delta = (new_p - old_p) if old_p is not None and new_p is not None else ''
            writer.writerow([
                c['file'], c['id'], c['name'], c['culture'], c['slot'],
                c['tier'], c['variant'],
                old_p, new_p, delta,
                c['old_weight'], c['new_weight'],
                c['old_material'], c['new_material'],
                c['status']
            ])
    print(f"\nExported {len(all_changes)} items to {csv_path}")


# =============================================================================
# Main
# =============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Armor rebalancing for TAOM (writes to the LIVE LOTRLOME_Armory tree).")
    grp = parser.add_mutually_exclusive_group(required=True)
    grp.add_argument('--dry-run', action='store_true', help='Preview changes (no writes)')
    grp.add_argument('--apply', action='store_true', help='Write changes to the live armory XML')
    grp.add_argument('--export-csv', action='store_true', help='Export tier classification to CSV')
    parser.add_argument('--armory-path', default=ARMORY_DIR,
                        help='Override the armory base dir (default: live LOTRLOME_Armory)')
    parser.add_argument('--cultures', default='',
                        help='Comma-separated culture folders to restrict to (e.g. dale,rhun)')
    parser.add_argument('--slots', default='',
                        help='Comma-separated slots to restrict to (head,body,arm,leg,shoulder); default all')
    parser.add_argument('--all', action='store_true',
                        help='Required to --apply across ALL cultures (safety guard for preserved cultures)')
    parser.add_argument('--weights-only', action='store_true',
                        help='Ladder ONLY weights (tier follows current armor value); leave armor + material untouched')
    parser.add_argument('--tier-source', choices=['keyword', 'roster', 'roster-first'], default='keyword',
                        help="Tier source: 'roster' uses the derive_armor_tiers map (authoritative roster tiers, "
                             "skips hero/unworn); 'roster-first' takes the lowest wearer's band even over an id "
                             "keyword and falls back to the keyword for unworn kit (the kingdom-cap curve); "
                             "'keyword' uses the legacy name detector (default)")
    parser.add_argument('--keep-weights', action='store_true',
                        help='Leave weight alone during a full re-stat (armour and loot table only).')
    parser.add_argument('--keep-material-type', action='store_true',
                        help='Leave material_type (hit sounds/FX) alone; modifier_group still follows the ladder.')
    parser.add_argument('--backup-tag', default='',
                        help='Take a one-time <file>.bak-<tag> copy of every file written (recommended for --apply).')
    parser.add_argument('--no-lower-armor', action='store_true',
                        help='Never reduce an armor stat (raise under-tiered items, keep the rest). '
                             'Use for "do not nerf" cultures (e.g. dunland).')
    parser.add_argument('--materials-only', action='store_true',
                        help='Write ONLY material_type + modifier_group (no armor, no weight). '
                             'Puts each item on the loot-roll table its tier warrants; skips hero kit.')
    parser.add_argument('--keep-materials', action='store_true',
                        help='Freeze material_type + modifier_group during a full re-stat. '
                             '(Used to be implied by --no-lower-armor.)')
    args = parser.parse_args()

    if args.materials_only and args.weights_only:
        print("ERROR: --materials-only and --weights-only are mutually exclusive.")
        sys.exit(2)

    dry_run = not args.apply
    requested = {c.strip() for c in args.cultures.split(',') if c.strip()}
    requested_slots = {s.strip() for s in args.slots.split(',') if s.strip()}

    roster_map = None
    if args.tier_source in ('roster', 'roster-first'):
        roster_map, gen = (load_roster_records() if args.tier_source == 'roster-first'
                           else load_roster_tier_map())
        if roster_map is None:
            print(f"ERROR: --tier-source {args.tier_source} needs tools/data/armor_roster_tiers.json. "
                  "Run: python tools/derive_armor_tiers.py")
            sys.exit(2)
        print(f"Roster tier map: {len(roster_map)} items (generated {gen})")

    # Safety guard: refuse a blanket --apply that could flatten hand-authored cultures.
    if args.apply and not requested and not args.all:
        print("ERROR: refusing a blanket --apply. Scope it with --cultures a,b,c, "
              "or pass --all to deliberately rewrite EVERY culture (incl. preserved: "
              f"{', '.join(sorted(PRESERVE_CULTURES))}).")
        sys.exit(2)
    if args.apply and args.all:
        print("\n*** APPLYING CHANGES TO ALL CULTURES ***\n")
    elif args.apply:
        print(f"\n*** APPLYING CHANGES (scoped: {', '.join(sorted(requested))}) ***\n")
    elif args.export_csv:
        print("\n*** EXPORT CSV MODE ***\n")
    else:
        print("\n*** DRY RUN ***\n")

    # Resolve armory path
    armory_dir = os.path.normpath(args.armory_path)
    if not os.path.isdir(armory_dir):
        print(f"ERROR: Armory directory not found: {armory_dir}")
        sys.exit(1)

    print(f"Armory directory: {armory_dir}")

    # Find all culture directories (optionally restricted by --cultures)
    culture_dirs = sorted([
        d for d in os.listdir(armory_dir)
        if os.path.isdir(os.path.join(armory_dir, d)) and (not requested or d in requested)
    ])
    if requested:
        missing = requested - set(culture_dirs)
        if missing:
            print(f"WARNING: requested cultures not found as folders: {', '.join(sorted(missing))}")
    print(f"Found {len(culture_dirs)} culture directories: {', '.join(culture_dirs)}")

    all_changes = []
    files_processed = 0

    for culture_dir in culture_dirs:
        culture_path = os.path.join(armory_dir, culture_dir)

        for armor_file, slot_type in SLOT_TYPES.items():
            if requested_slots and slot_type not in requested_slots:
                continue
            filepath = os.path.join(culture_path, armor_file)
            if not os.path.exists(filepath):
                continue

            print(f"  Processing {culture_dir}/{armor_file}...")
            changes = process_file(filepath, slot_type, dry_run=dry_run, weights_only=args.weights_only,
                                    tier_source=args.tier_source, roster_map=roster_map,
                                    no_lower_armor=args.no_lower_armor,
                                    materials_only=args.materials_only,
                                    keep_materials=args.keep_materials,
                                    keep_weights=args.keep_weights,
                                    keep_material_type=args.keep_material_type,
                                    backup_tag=args.backup_tag or None)
            all_changes.extend(changes)
            files_processed += 1

    print_report(all_changes)

    if args.export_csv:
        csv_path = os.path.join(os.path.dirname(__file__), 'armor_rebalance.csv')
        export_csv(all_changes, csv_path)

    if args.apply:
        print(f"\n*** Changes written to {files_processed} files ***")
    else:
        print(f"\n*** {files_processed} files analyzed (no changes written) ***")


if __name__ == '__main__':
    main()
