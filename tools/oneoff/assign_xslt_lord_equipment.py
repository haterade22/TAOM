"""
Assign LOTR equipment to XSLT-transformed lords in lords.xslt.

These lords currently pass through vanilla Calradia equipment via the identity
transform. This script adds Equipments blocks with TAOM template references
and excludes Equipments from the passthrough.

Uses the splords.xslt section comments to determine which LOTR faction each
lord belongs to (e.g., Empire lords split into Dunland/Gondor/Mordor).

Usage:
    python tools/assign_xslt_lord_equipment.py --dry-run   # Preview changes
    python tools/assign_xslt_lord_equipment.py --apply      # Apply changes
"""

import os
import re
import sys
from collections import defaultdict

_REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LORDS_XSLT = os.path.join(_REPO, 'Main', '_Module', 'ModuleData', 'lords.xslt')
SPLORDS_XSLT = os.path.join(_REPO, 'Main', '_Module', 'ModuleData', 'splords.xslt')

# Faction -> (template_family, has_default_in_civ_name)
FACTION_EQUIPMENT = {
    'dunland':   ('dunland',   True),
    'gondor':    ('gondor',    True),
    'mordor':    ('mordor',    True),
    'dale':      ('dale',      True),
    'harad':     ('harad',     False),
    'rohan':     ('rohan',     True),
    'khand':     ('dunland',   True),   # Khand uses Dunland equipment (no Khand-specific items)
    'rhun':      ('rhun',      False),
}

SUFFIXES = ['_a', '_b', '_c', '_d', '_e']

# Section markers in splords.xslt with their line ranges and faction names
SPLORDS_SECTIONS = [
    (14,   198,  'dunland'),
    (199,  255,  'gondor'),
    (256,  1040, 'mordor'),
    (1041, 1426, 'dale'),
    (1427, 1812, 'harad'),
    (1813, 2198, 'rohan'),
    (2199, 2504, 'khand'),
    (2505, 2819, 'rhun'),
]

TEMPLATE_MATCH_RE = re.compile(r"<xsl:template\s+match=\"NPCCharacter\[@id='([^']+)'\]\"")
PASSTHROUGH_RE = re.compile(
    r'(\s*)<xsl:apply-templates select="node\(\)\[not\(self::face or self::skills or self::Traits(.*?)\)\]"/>'
)


# Region code prefix -> faction (for lords using TAOM region code IDs)
REGION_PREFIX_MAP = {
    'WE': 'gondor',   # West Empire
    'SE': 'mordor',   # South Empire
    'NE': 'dunland',  # North Empire
    'A':  'harad',    # Aserai region
    'B':  'dunland',  # Battania region
    'K':  'rhun',     # Khuzait region
    'S':  'dale',     # Sturgia region
    'V':  'rohan',    # Vlandia region
}


def get_faction_from_region_code(lord_id):
    """Try to determine faction from region code in lord ID (e.g., lord_WE9_l)."""
    # Extract the region part after 'lord_'
    parts = lord_id.split('_', 1)
    if len(parts) < 2:
        return None
    suffix = parts[1]
    # Try two-letter prefix first, then single-letter
    for prefix_len in (2, 1):
        prefix = suffix[:prefix_len]
        if prefix in REGION_PREFIX_MAP:
            return REGION_PREFIX_MAP[prefix]
    return None


def build_faction_map():
    """Build lord_id -> faction mapping from splords.xslt section structure."""
    lord_to_faction = {}
    id_re = re.compile(r"id='(lord_[^']+)'")

    with open(SPLORDS_XSLT, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    for start, end, faction in SPLORDS_SECTIONS:
        for i in range(start - 1, min(end, len(lines))):
            for m in id_re.finditer(lines[i]):
                lord_to_faction[m.group(1)] = faction

    return lord_to_faction


def build_equipment_id(family, has_default, is_battle, suffix):
    """Build the full TAOM template ID."""
    if is_battle:
        return f'{family}_bat_template_medium{suffix}'
    else:
        if has_default:
            return f'{family}_civ_template_default{suffix}'
        else:
            return f'{family}_civ_template{suffix}'


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('--dry-run', '--apply'):
        print("Usage: python tools/assign_xslt_lord_equipment.py [--dry-run | --apply]")
        sys.exit(1)

    dry_run = sys.argv[1] == '--dry-run'

    # Build faction mapping from splords.xslt
    lord_to_faction = build_faction_map()
    print(f"Built faction map: {len(lord_to_faction)} lords mapped")
    faction_counts = defaultdict(int)
    for faction in lord_to_faction.values():
        faction_counts[faction] += 1
    for f, c in sorted(faction_counts.items()):
        print(f"  {f}: {c}")
    print()

    # Read lords.xslt
    with open(LORDS_XSLT, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    # Process each template
    faction_counters = defaultdict(int)  # (faction, is_battle) -> counter
    modifications = 0
    skipped_boromir = 0
    skipped_no_faction = 0
    skipped_already_equipped = 0
    new_lines = []
    i = 0

    while i < len(lines):
        line = lines[i]

        # Check for template start
        tm = TEMPLATE_MATCH_RE.search(line)
        if tm:
            lord_id = tm.group(1)

            # Check if this lord already has an Equipments block
            # Look ahead for Equipments or the passthrough line
            has_equipment = False
            passthrough_idx = None
            j = i + 1
            while j < len(lines) and '</xsl:template>' not in lines[j]:
                if '<Equipments>' in lines[j]:
                    has_equipment = True
                if PASSTHROUGH_RE.search(lines[j]):
                    passthrough_idx = j
                j += 1
            template_end = j

            if has_equipment:
                # Already has equipment (e.g., Boromir) - skip
                skipped_boromir += 1
                for k in range(i, template_end + 1):
                    new_lines.append(lines[k])
                i = template_end + 1
                continue

            if lord_id not in lord_to_faction:
                # Try region code fallback
                region_faction = get_faction_from_region_code(lord_id)
                if region_faction:
                    lord_to_faction[lord_id] = region_faction
                else:
                    # No faction mapping - skip (dead lords, etc.)
                    skipped_no_faction += 1
                    for k in range(i, template_end + 1):
                        new_lines.append(lines[k])
                    i = template_end + 1
                    continue

            if passthrough_idx is None:
                # No passthrough line found - unusual, skip
                for k in range(i, template_end + 1):
                    new_lines.append(lines[k])
                i = template_end + 1
                continue

            faction = lord_to_faction[lord_id]
            family, has_default = FACTION_EQUIPMENT[faction]

            # Get suffixes via round-robin
            bat_key = (faction, True)
            civ_key = (faction, False)
            bat_idx = faction_counters[bat_key] % len(SUFFIXES)
            civ_idx = faction_counters[civ_key] % len(SUFFIXES)
            bat_suffix = SUFFIXES[bat_idx]
            civ_suffix = SUFFIXES[civ_idx]
            faction_counters[bat_key] += 1
            faction_counters[civ_key] += 1

            bat_id = build_equipment_id(family, has_default, True, bat_suffix)
            civ_id = build_equipment_id(family, has_default, False, civ_suffix)

            # Output lines up to (but not including) the passthrough line
            for k in range(i, passthrough_idx):
                new_lines.append(lines[k])

            # Get the indentation from the passthrough line
            pm = PASSTHROUGH_RE.search(lines[passthrough_idx])
            indent = pm.group(1)

            # Insert Equipments block before the passthrough
            new_lines.append(f'{indent}<Equipments>\n')
            new_lines.append(f'{indent}    <EquipmentSet id="{bat_id}" />\n')
            new_lines.append(f'{indent}    <EquipmentSet id="{civ_id}" equipmentType="Civilian" />\n')
            new_lines.append(f'{indent}</Equipments>\n')

            # Modify the passthrough line to exclude Equipments
            old_passthrough = lines[passthrough_idx]
            if 'self::Equipments' not in old_passthrough:
                new_passthrough = old_passthrough.replace(
                    'self::Traits',
                    'self::Traits or self::Equipments'
                )
                new_lines.append(new_passthrough)
            else:
                new_lines.append(old_passthrough)

            # Output remaining lines of the template
            for k in range(passthrough_idx + 1, template_end + 1):
                new_lines.append(lines[k])

            modifications += 1
            i = template_end + 1
            continue

        new_lines.append(line)
        i += 1

    # Write results
    if dry_run:
        print(f"=== DRY RUN - No changes made ===\n")
    else:
        with open(LORDS_XSLT, 'w', encoding='utf-8') as f:
            f.writelines(new_lines)
        print(f"=== Applied changes ===\n")

    print(f"Modified: {modifications} lord templates")
    print(f"Skipped: {skipped_boromir} (already has equipment), "
          f"{skipped_no_faction} (no faction mapping)")

    # Summary by faction
    print(f"\n{'Faction':<12} {'Lords':>8}")
    print("-" * 22)
    faction_mod_counts = defaultdict(int)
    # Reconstruct from counters
    for (faction, is_battle), count in faction_counters.items():
        if is_battle:
            faction_mod_counts[faction] = count
    for faction in sorted(faction_mod_counts.keys()):
        print(f"{faction:<12} {faction_mod_counts[faction]:>8}")
    print("-" * 22)
    print(f"{'TOTAL':<12} {modifications:>8}")


if __name__ == '__main__':
    main()
