#!/usr/bin/env python3
"""
Weapon Damage Rebalancing Script for TAOM

Applies cultural damage_factor modifiers to blade crafting pieces.
Part of the balance triangle: Troop Skills + Armor + Weapons.

Modifies damage_factor values on <Thrust> and <Swing> elements within
<BladeData> for each culture's blade pieces. Hero/legendary weapons
are exempt from modifiers.

Usage:
    python rebalance_weapons.py --dry-run       # Preview changes
    python rebalance_weapons.py --apply         # Write changes to XML files
    python rebalance_weapons.py --export-csv    # Export rebalance data to CSV
"""

import xml.etree.ElementTree as ET
import os
import sys
import re
import csv
from collections import defaultdict

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ARMORY_DIR = os.path.join(SCRIPT_DIR, '..', '..', 'taommod', 'src', 'data', 'armory')

WEAPONS_XML = os.path.join(ARMORY_DIR, 'LOTRAOM_weapons.xml')
CRAFTING_PIECES_XML = os.path.join(ARMORY_DIR, 'LOTRLOME_crafting_pieces.xml')

# Also modify the Steam copy (the actual game file)
STEAM_CRAFTING_PIECES = os.path.join(
    'E:', os.sep, 'Steam', 'steamapps', 'common',
    'Mount & Blade II Bannerlord', 'Modules', 'LOTRLOME_Armory',
    'ModuleData', 'LOTRLOME_crafting_pieces.xml'
)

# =============================================================================
# Culture Mapping (vanilla Bannerlord culture IDs → TAOM faction names)
# =============================================================================

CULTURE_MAP = {
    # Custom TAOM cultures (map directly)
    'gondor':     'gondor',
    'mordor':     'mordor',
    'erebor':     'erebor',
    'rivendell':  'rivendell',
    'mirkwood':   'mirkwood',
    'lothlorien': 'lothlorien',
    'isengard':   'isengard',
    'gundabad':   'gundabad',
    'dolguldur':  'dolguldur',
    'rhun':       'rhun',
    # Remapped vanilla cultures
    'vlandia':    'rohan',
    'empire':     'dunland',
    'aserai':     'harad',
    'khuzait':    'rhun',      # Easterlings use both khuzait and rhun
    'battania':   'dunland',
    'sturgia':    'sturgia',   # Dale/North - no modifier defined, uses 1.0
    # Neutral
    'neutral_culture': 'neutral',
}

# =============================================================================
# Cultural Weapon Damage Modifiers
# =============================================================================

WEAPON_MODS = {
    # Good Peoples
    'gondor':     1.00,   # +0%  — baseline reference
    'rohan':      1.05,   # +5%  — light armor needs slight weapon compensation
    'erebor':     0.80,   # -20% — best melee skills + best dwarf armor
    'rivendell':  0.80,   # -20% — elite skills (+40) + best armor (+6)
    'mirkwood':   0.80,   # -20% — elite skills (+50) + light armor (+5)
    'lothlorien': 0.80,   # -20% — elite skills + good armor

    # Evil Factions
    'isengard':   1.05,   # +5%  — solid troops deserve solid weapons
    'mordor':     1.05,   # +5%  — weak troops/armor need weapon compensation
    'gundabad':   1.05,   # +5%  — worst armor in game needs compensation
    'dolguldur':  1.10,   # +10% — weakest troops need biggest weapon boost
    'harad':      1.10,   # +10% — worst armor needs weapon compensation
    'rhun':       1.05,   # +5%  — small buff for cavalry culture
    'dunland':    1.15,   # +15% — poor armor + skills needs significant boost

    # Unaffected
    'neutral':    1.00,
    'sturgia':    1.00,
}

# =============================================================================
# Dunland Spear Pre-Fix (applied BEFORE cultural modifier)
# =============================================================================
# These 5 spear heads are structurally underpowered at 1.9.
# Raise to 2.8 before applying the +15% cultural modifier.
# Final result: 2.8 * 1.15 = 3.22, competitive with other polearms.

DUNLAND_SPEAR_FIX = {
    'dunland_caerdh_spear_head_a': 2.8,
    'dunland_caerdh_spear_head_b': 2.8,
    'dunland_caerdh_spear_head_c': 2.8,
    'dunland_caerdh_spear_head_d': 2.8,
    'dunland_caerdh_spear_head_e': 2.8,
}

# =============================================================================
# Hero Weapon Exemptions (blade piece IDs that skip cultural modifiers)
# =============================================================================

HERO_BLADE_IDS = {
    # Gondor heroes
    'wm_anduril_sword_blade',
    'wm_glamdring_blade',
    'wm_strider_sword_blade',
    'wm_boromir_sword_blade',
    'wm_faramir_sword_blade',
    # Rohan heroes
    'wm_theoden_sword_blade',
    'wm_eomer_blade',
    'wm_eowyn_blade',
    # Mordor heroes
    'wm_witch_king_sword_blade',
    'wm_nazgul_sword_blade',
    'wm_sauron_mace_blade',
    # Lothlorien heroes
    'wm_galadriel_sword_blade',
    'wm_aranruth_sword_blade',
    'wm_aranruth_sword_ruby_blade',
    'wm_aranruth_sword_topaz_blade',
    # Mirkwood heroes
    'wm_legolas_sword_blade',
    'wm_thranduil_sword_blade',
    # Neutral heroes
    'wm_tuors_axe_blade',
}


# =============================================================================
# Step 1: Build blade piece → culture mapping from weapons XML
# =============================================================================

def build_piece_culture_map(weapons_xml_path):
    """
    Parse LOTRAOM_weapons.xml to map each blade piece ID to its culture.
    Returns: dict of blade_piece_id -> taom_culture_name
    """
    tree = ET.parse(weapons_xml_path)
    root = tree.getroot()

    piece_culture = {}
    piece_weapon = {}  # blade_piece_id -> weapon_id (for reporting)

    for item in root.findall('.//CraftedItem'):
        item_id = item.get('id', '')
        raw_culture = item.get('culture', '').replace('Culture.', '')

        # Fallback: detect culture from weapon ID prefix if no culture attribute
        if not raw_culture:
            raw_culture = _detect_culture_from_id(item_id)

        taom_culture = CULTURE_MAP.get(raw_culture, raw_culture)

        for piece in item.findall('.//Piece'):
            piece_type = piece.get('Type', '')
            piece_id = piece.get('id', '')
            if piece_type == 'Blade' and piece_id:
                # First weapon to claim this piece wins (pieces can be reused)
                if piece_id not in piece_culture:
                    piece_culture[piece_id] = taom_culture
                    piece_weapon[piece_id] = item_id

    return piece_culture, piece_weapon


# Prefix-based fallback culture detection for weapons without culture attribute
_ID_CULTURE_PREFIXES = [
    ('dunland_', 'empire'),
    ('numenorean_', 'gondor'),
    ('mirkwood_', 'mirkwood'),
    ('harad_', 'aserai'),
    ('wm_harad_', 'aserai'),
    ('he_', 'rivendell'),       # high elf
    ('gondor_', 'gondor'),
    ('rohan_', 'vlandia'),
    ('erebor_', 'erebor'),
    ('mordor_', 'mordor'),
    ('isengard_', 'isengard'),
    ('gundabad_', 'gundabad'),
    ('dolguldur_', 'dolguldur'),
    ('easterling_', 'aserai'),
]


def _detect_culture_from_id(item_id):
    """Detect culture from weapon ID prefix when culture attribute is missing."""
    item_lower = item_id.lower()
    for prefix, culture in _ID_CULTURE_PREFIXES:
        if item_lower.startswith(prefix):
            return culture
    return ''


# Prefix-based detection for blade piece IDs (not weapon IDs)
_PIECE_CULTURE_PREFIXES = [
    ('sm_dg_khml_', 'khuzait'),      # Khamul (Easterling)
    ('sm_rh_drag_', 'khuzait'),      # Dragon Guard (Easterling)
    ('sm_rh_loke_', 'khuzait'),      # Loke-rim (Easterling)
    ('sm_dwarf_', 'erebor'),
    ('wm_rohan_', 'vlandia'),
    ('wm_pelagir_', 'gondor'),
    ('wm_gondor_', 'gondor'),
    ('wm_mordor_', 'mordor'),
    ('wm_isengard_', 'isengard'),
    ('wm_gundabad_', 'gundabad'),
    ('wm_dol_goldur_', 'dolguldur'),
    ('wm_harad_', 'aserai'),
    ('wm_mirkwood_', 'mirkwood'),
    ('wm_rivendell_', 'rivendell'),
    ('wm_lothlorien_', 'lothlorien'),
    ('dunland_', 'empire'),
    ('easterling_', 'aserai'),
]


def _detect_culture_from_piece_id(piece_id):
    """Detect culture from blade piece ID prefix for unmapped pieces."""
    piece_lower = piece_id.lower()
    for prefix, culture in _PIECE_CULTURE_PREFIXES:
        if piece_lower.startswith(prefix):
            return culture
    return ''


# =============================================================================
# Step 2: Parse crafting pieces and compute changes
# =============================================================================

def compute_changes(crafting_xml_path, piece_culture, piece_weapon):
    """
    Parse LOTRLOME_crafting_pieces.xml, find all Blade pieces,
    and compute new damage_factor values.

    Returns list of change records.
    """
    tree = ET.parse(crafting_xml_path)
    root = tree.getroot()

    changes = []

    for piece in root.findall('.//CraftingPiece'):
        piece_id = piece.get('id', '')
        piece_type = piece.get('piece_type', '')

        if piece_type != 'Blade':
            continue

        blade_data = piece.find('BladeData')
        if blade_data is None:
            continue

        # Get current damage factors
        thrust_elem = blade_data.find('Thrust')
        swing_elem = blade_data.find('Swing')

        old_thrust = float(thrust_elem.get('damage_factor', '0')) if thrust_elem is not None else None
        old_swing = float(swing_elem.get('damage_factor', '0')) if swing_elem is not None else None

        # Determine culture (from weapon mapping, or fallback to piece ID prefix)
        culture = piece_culture.get(piece_id)
        if not culture:
            culture = CULTURE_MAP.get(_detect_culture_from_piece_id(piece_id), 'unknown')
        weapon_id = piece_weapon.get(piece_id, '(unused piece)')

        # Check hero exemption
        is_hero = piece_id in HERO_BLADE_IDS

        # Get modifier
        modifier = WEAPON_MODS.get(culture, 1.0)

        # Apply Dunland spear pre-fix
        pre_fix_thrust = DUNLAND_SPEAR_FIX.get(piece_id)
        pre_fix_applied = False
        effective_old_thrust = old_thrust
        if pre_fix_thrust is not None and old_thrust is not None:
            effective_old_thrust = pre_fix_thrust
            pre_fix_applied = True

        # Compute new values
        if is_hero:
            new_thrust = effective_old_thrust if pre_fix_applied else old_thrust
            new_swing = old_swing
            modifier_applied = 1.0
        else:
            modifier_applied = modifier
            new_thrust = round(effective_old_thrust * modifier, 2) if effective_old_thrust is not None else None
            new_swing = round(old_swing * modifier, 2) if old_swing is not None else None

        # Only record if something changed
        thrust_changed = old_thrust is not None and new_thrust is not None and abs(new_thrust - old_thrust) > 0.001
        swing_changed = old_swing is not None and new_swing is not None and abs(new_swing - old_swing) > 0.001

        changes.append({
            'piece_id': piece_id,
            'weapon_id': weapon_id,
            'culture': culture,
            'is_hero': is_hero,
            'modifier': modifier_applied,
            'pre_fix': pre_fix_applied,
            'old_swing': old_swing,
            'new_swing': new_swing,
            'old_thrust': old_thrust,
            'new_thrust': new_thrust,
            'swing_changed': swing_changed,
            'thrust_changed': thrust_changed,
            'changed': thrust_changed or swing_changed,
        })

    return changes


# =============================================================================
# Step 3: Apply changes via regex (preserves XML formatting)
# =============================================================================

def apply_changes_via_regex(filepath, changes):
    """
    Apply damage_factor changes to crafting pieces XML using regex.
    Preserves all formatting, comments, whitespace.
    """
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    applied = 0
    for change in changes:
        if not change['changed']:
            continue

        piece_id = change['piece_id']

        # Find the piece block: from id="piece_id" to </CraftingPiece>
        piece_pattern = re.compile(
            r'(id="' + re.escape(piece_id) + r'")(.*?)(</CraftingPiece>)',
            re.DOTALL
        )
        piece_match = piece_pattern.search(content)
        if not piece_match:
            print(f"  WARNING: Could not find piece {piece_id} in {filepath}")
            continue

        piece_block = piece_match.group(2)
        new_block = piece_block

        # Replace thrust damage_factor
        if change['thrust_changed'] and change['new_thrust'] is not None:
            # Match damage_factor within a <Thrust> context
            thrust_pattern = re.compile(
                r'(<Thrust\s[^>]*?damage_factor=")([^"]*?)(")'
            )
            if thrust_pattern.search(new_block):
                new_block = thrust_pattern.sub(
                    lambda m: m.group(1) + _fmt_factor(change['new_thrust']) + m.group(3),
                    new_block, count=1
                )

        # Replace swing damage_factor
        if change['swing_changed'] and change['new_swing'] is not None:
            swing_pattern = re.compile(
                r'(<Swing\s[^>]*?damage_factor=")([^"]*?)(")'
            )
            if swing_pattern.search(new_block):
                new_block = swing_pattern.sub(
                    lambda m: m.group(1) + _fmt_factor(change['new_swing']) + m.group(3),
                    new_block, count=1
                )

        if new_block != piece_block:
            content = content[:piece_match.start(2)] + new_block + content[piece_match.end(2):]
            applied += 1

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

    return applied


def _fmt_factor(value):
    """Format damage factor: remove trailing zeros but keep at least 1 decimal."""
    if value == int(value):
        return f"{value:.1f}"
    # Round to 2 decimal places, strip trailing zeros
    s = f"{value:.2f}".rstrip('0')
    if s.endswith('.'):
        s += '0'
    return s


# =============================================================================
# Reporting
# =============================================================================

def print_summary(changes):
    """Print a summary table of changes grouped by culture."""
    by_culture = defaultdict(list)
    for c in changes:
        by_culture[c['culture']].append(c)

    print('\n=== Weapon Damage Rebalance Summary ===\n')
    print(f"{'Culture':<14} {'Mod':>5} {'Pieces':>6} {'Changed':>8} {'Hero':>5}  Notes")
    print('-' * 70)

    total_pieces = 0
    total_changed = 0
    total_hero = 0

    for culture in sorted(by_culture.keys()):
        items = by_culture[culture]
        mod = WEAPON_MODS.get(culture, 1.0)
        n_pieces = len(items)
        n_changed = sum(1 for c in items if c['changed'])
        n_hero = sum(1 for c in items if c['is_hero'])
        n_prefix = sum(1 for c in items if c['pre_fix'])

        notes = []
        if n_hero > 0:
            notes.append(f'{n_hero} hero exempt')
        if n_prefix > 0:
            notes.append(f'{n_prefix} spear pre-fix')

        mod_str = f"{mod:.2f}x"
        print(f"{culture:<14} {mod_str:>5} {n_pieces:>6} {n_changed:>8} {n_hero:>5}  {', '.join(notes)}")

        total_pieces += n_pieces
        total_changed += n_changed
        total_hero += n_hero

    print('-' * 70)
    print(f"{'TOTAL':<14} {'':>5} {total_pieces:>6} {total_changed:>8} {total_hero:>5}")

    # Detail view of changes
    print('\n=== Detailed Changes ===\n')
    print(f"{'Piece ID':<45} {'Culture':<12} {'OldSw':>6} {'NewSw':>6} {'OldTh':>6} {'NewTh':>6} {'Hero':>5}")
    print('-' * 95)

    for c in sorted(changes, key=lambda x: (x['culture'], x['piece_id'])):
        if not c['changed'] and not c['is_hero']:
            continue
        old_sw = f"{c['old_swing']:.1f}" if c['old_swing'] is not None else '—'
        new_sw = f"{c['new_swing']:.1f}" if c['new_swing'] is not None else '—'
        old_th = f"{c['old_thrust']:.1f}" if c['old_thrust'] is not None else '—'
        new_th = f"{c['new_thrust']:.1f}" if c['new_thrust'] is not None else '—'
        hero = 'HERO' if c['is_hero'] else ''
        prefix = ' [FIX]' if c['pre_fix'] else ''
        print(f"{c['piece_id']:<45} {c['culture']:<12} {old_sw:>6} {new_sw:>6} {old_th:>6} {new_th:>6} {hero:>5}{prefix}")


def export_csv(changes, filepath):
    """Export rebalance data to CSV."""
    with open(filepath, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow([
            'piece_id', 'weapon_id', 'culture', 'modifier', 'is_hero', 'pre_fix',
            'old_swing_factor', 'new_swing_factor',
            'old_thrust_factor', 'new_thrust_factor',
            'changed'
        ])
        for c in sorted(changes, key=lambda x: (x['culture'], x['piece_id'])):
            writer.writerow([
                c['piece_id'], c['weapon_id'], c['culture'],
                c['modifier'], c['is_hero'], c['pre_fix'],
                c['old_swing'] if c['old_swing'] is not None else '',
                c['new_swing'] if c['new_swing'] is not None else '',
                c['old_thrust'] if c['old_thrust'] is not None else '',
                c['new_thrust'] if c['new_thrust'] is not None else '',
                c['changed'],
            ])
    print(f"\nCSV exported to {filepath}")


# =============================================================================
# Main
# =============================================================================

def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('--dry-run', '--apply', '--export-csv'):
        print("Usage: python rebalance_weapons.py [--dry-run | --apply | --export-csv]")
        sys.exit(1)

    mode = sys.argv[1]

    # Validate files exist
    if not os.path.exists(WEAPONS_XML):
        print(f"ERROR: Weapons XML not found: {WEAPONS_XML}")
        sys.exit(1)
    if not os.path.exists(CRAFTING_PIECES_XML):
        print(f"ERROR: Crafting pieces XML not found: {CRAFTING_PIECES_XML}")
        sys.exit(1)

    print("Building blade piece -> culture mapping from weapons XML...")
    piece_culture, piece_weapon = build_piece_culture_map(WEAPONS_XML)
    print(f"  Mapped {len(piece_culture)} blade pieces to cultures")

    print("Computing damage factor changes...")
    changes = compute_changes(CRAFTING_PIECES_XML, piece_culture, piece_weapon)
    print(f"  Analyzed {len(changes)} blade pieces")

    print_summary(changes)

    if mode == '--export-csv':
        csv_path = os.path.join(SCRIPT_DIR, 'weapon_rebalance.csv')
        export_csv(changes, csv_path)

    elif mode == '--apply':
        n_to_change = sum(1 for c in changes if c['changed'])
        if n_to_change == 0:
            print("\nNo changes to apply.")
            return

        print(f"\nApplying {n_to_change} changes...")

        # Apply to website data copy
        print(f"  Writing to {CRAFTING_PIECES_XML}...")
        applied = apply_changes_via_regex(CRAFTING_PIECES_XML, changes)
        print(f"  Applied {applied} changes to website data copy")

        # Apply to Steam game copy if it exists
        if os.path.exists(STEAM_CRAFTING_PIECES):
            print(f"  Writing to {STEAM_CRAFTING_PIECES}...")
            applied = apply_changes_via_regex(STEAM_CRAFTING_PIECES, changes)
            print(f"  Applied {applied} changes to Steam game copy")
        else:
            print(f"  Steam copy not found at {STEAM_CRAFTING_PIECES}, skipping")

        # Export CSV alongside
        csv_path = os.path.join(SCRIPT_DIR, 'weapon_rebalance.csv')
        export_csv(changes, csv_path)

        print("\nDone! Run tw-damage-calc.mjs to verify new damage values.")

    else:
        print("\n[DRY RUN] No files were modified.")


if __name__ == '__main__':
    main()
