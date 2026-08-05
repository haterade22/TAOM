"""
Assign LOTR equipment templates to TAOM lords.

Replaces vanilla SandBoxCore equipment template references (ase_, bat_, emp_,
khu_, stu_, vla_) with TAOM culture-specific equipment templates from the
taom_equipment_sets_*.xml files.

Usage:
    python tools/assign_lord_equipment.py --dry-run   # Preview changes
    python tools/assign_lord_equipment.py --apply      # Apply changes
"""

import os
import re
import sys
from collections import defaultdict

LORDS_XML = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                         'Main', '_Module', 'ModuleData', 'characters', 'lords.xml')

# Vanilla prefixes that should be replaced
VANILLA_PREFIXES = ('emp_', 'stu_', 'ase_', 'vla_', 'bat_', 'khu_')

# Culture -> (template_family, has_default_in_civ_name)
# has_default_in_civ_name: True = {family}_civ_template_default_{x}
#                          False = {family}_civ_template_{x}
CULTURE_MAP = {
    'Culture.gondor':     ('gondor',     True),
    'Culture.vlandia':    ('rohan',      True),
    'Culture.mordor':     ('mordor',     True),
    'Culture.isengard':   ('isengard',   False),
    'Culture.dolguldur':  ('dolguldur',  True),
    'Culture.gundabad':   ('gundabad',   True),
    'Culture.erebor':     ('erebor',     True),
    'Culture.mirkwood':   ('mirkwood',   True),
    'Culture.rivendell':  ('rivendell',  True),
    'Culture.aserai':     ('harad',      False),
    'Culture.empire':     ('dunland',    True),
    'Culture.battania':   ('dunland',    True),
    'Culture.khuzait':    ('rhun',       False),
    'Culture.sturgia':    ('dale',       True),
    'Culture.lothlorien': ('lothlorien', True),
    'Culture.umbar':      ('umbar',      True),
}

SUFFIXES = ['_a', '_b', '_c', '_d', '_e']

# Regex patterns
NPC_START_RE = re.compile(r'<NPCCharacter\s')
NPC_END_RE = re.compile(r'</NPCCharacter>')
CULTURE_RE = re.compile(r'culture="([^"]+)"')
EQUIPMENT_SET_RE = re.compile(r'(<EquipmentSet\s+id=")([^"]+)(".*?/>)')
EQUIPMENT_SLOT_RE = re.compile(r'<equipment\s+slot=')
EQUIPMENTS_START_RE = re.compile(r'<Equipments>')
EQUIPMENTS_END_RE = re.compile(r'</Equipments>')


def build_template_id(family, has_default, is_battle, suffix):
    """Build the full TAOM template ID."""
    if is_battle:
        return f'{family}_bat_template_medium{suffix}'
    else:
        if has_default:
            return f'{family}_civ_template_default{suffix}'
        else:
            return f'{family}_civ_template{suffix}'


def is_vanilla_template(template_id):
    """Check if a template ID is a vanilla SandBoxCore template."""
    return template_id.startswith(VANILLA_PREFIXES)


def has_inline_equipment(lines, start_idx, end_idx):
    """Check if an NPCCharacter block has inline equipment (equipment slot= lines)."""
    for i in range(start_idx, min(end_idx + 1, len(lines))):
        if EQUIPMENT_SLOT_RE.search(lines[i]):
            return True
    return False


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('--dry-run', '--apply'):
        print("Usage: python tools/assign_lord_equipment.py [--dry-run | --apply]")
        sys.exit(1)

    dry_run = sys.argv[1] == '--dry-run'

    with open(LORDS_XML, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    # First pass: identify all NPCCharacter blocks and their properties
    lords = []  # list of (culture, has_inline, equipment_line_indices)
    current_npc_start = None
    current_culture = None

    for i, line in enumerate(lines):
        if NPC_START_RE.search(line):
            current_npc_start = i
            m = CULTURE_RE.search(line)
            current_culture = m.group(1) if m else None
        elif NPC_END_RE.search(line) and current_npc_start is not None:
            # Find all EquipmentSet lines within this NPC
            inline = has_inline_equipment(lines, current_npc_start, i)
            eq_lines = []
            for j in range(current_npc_start, i + 1):
                if EQUIPMENT_SET_RE.search(lines[j]):
                    eq_lines.append(j)
            if eq_lines:
                lords.append({
                    'culture': current_culture,
                    'has_inline': inline,
                    'eq_lines': eq_lines,
                    'npc_start': current_npc_start,
                    'npc_end': i,
                })
            current_npc_start = None
            current_culture = None

    # Second pass: assign templates with round-robin per culture
    culture_counters = defaultdict(int)
    replacements = 0
    skipped_inline = 0
    skipped_unknown_culture = 0
    skipped_already_taom = 0
    mapping_log = []  # (lord_line, old_id, new_id)

    for lord in lords:
        culture = lord['culture']
        if culture not in CULTURE_MAP:
            skipped_unknown_culture += len(lord['eq_lines'])
            continue

        family, has_default = CULTURE_MAP[culture]

        for line_idx in lord['eq_lines']:
            line = lines[line_idx]
            m = EQUIPMENT_SET_RE.search(line)
            if not m:
                continue

            old_id = m.group(2)

            # Skip if already a TAOM template
            if not is_vanilla_template(old_id):
                skipped_already_taom += 1
                continue

            # Skip if this lord has inline equipment BUT only skip the
            # battle template (keep processing civilian refs)
            # v1.4.3+: read accepts both new equipmentType="Civilian" and legacy civilian="true".
            is_civilian = 'equipmentType="Civilian"' in line or 'civilian="true"' in line
            if lord['has_inline'] and not is_civilian:
                skipped_inline += 1
                continue

            # Determine battle vs civilian
            is_battle = not is_civilian

            # Get suffix via round-robin
            counter_key = (culture, is_battle)
            idx = culture_counters[counter_key] % len(SUFFIXES)
            suffix = SUFFIXES[idx]
            culture_counters[counter_key] += 1

            new_id = build_template_id(family, has_default, is_battle, suffix)

            if dry_run:
                mapping_log.append((line_idx + 1, old_id, new_id))
            else:
                new_line = line[:m.start(2)] + new_id + line[m.end(2):]
                lines[line_idx] = new_line
                replacements += 1
                mapping_log.append((line_idx + 1, old_id, new_id))

    # Output results
    if dry_run:
        print("=== DRY RUN - No changes made ===\n")
    else:
        with open(LORDS_XML, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        print(f"=== Applied {len(mapping_log)} replacements ===\n")

    # Summary by culture
    culture_summary = defaultdict(lambda: {'battle': 0, 'civilian': 0})
    for line_num, old_id, new_id in mapping_log:
        family = new_id.split('_bat_')[0] if '_bat_' in new_id else new_id.split('_civ_')[0]
        if '_bat_' in new_id:
            culture_summary[family]['battle'] += 1
        else:
            culture_summary[family]['civilian'] += 1

    print(f"{'Culture':<15} {'Battle':>8} {'Civilian':>10} {'Total':>8}")
    print("-" * 45)
    total_b, total_c = 0, 0
    for family in sorted(culture_summary.keys()):
        s = culture_summary[family]
        print(f"{family:<15} {s['battle']:>8} {s['civilian']:>10} {s['battle']+s['civilian']:>8}")
        total_b += s['battle']
        total_c += s['civilian']
    print("-" * 45)
    print(f"{'TOTAL':<15} {total_b:>8} {total_c:>10} {total_b+total_c:>8}")

    print(f"\nSkipped: {skipped_inline} inline, {skipped_already_taom} already TAOM, {skipped_unknown_culture} unknown culture")

    if dry_run:
        # Show sample mappings
        print("\n--- Sample mappings (first 20) ---")
        for line_num, old_id, new_id in mapping_log[:20]:
            print(f"  L{line_num}: {old_id} -> {new_id}")
        if len(mapping_log) > 20:
            print(f"  ... and {len(mapping_log) - 20} more")


if __name__ == '__main__':
    main()
