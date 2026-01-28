"""
Replace custom LOTRAOM equipment templates with vanilla Bannerlord equivalents.

Custom cultures in lords.xml reference equipment templates that don't exist in TAOM.
This script replaces them with vanilla equivalents based on the culture mapping
defined in taom_spcultures.xml.
"""

import re
import sys

LORDS_XML = r'c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\lords.xml'

# Custom prefix -> vanilla culture prefix mapping
PREFIX_MAP = {
    'gondor_':    'emp_',
    'rohan_':     'vla_',
    'vlandia_':   'vla_',    # extended vlandia (Theoden/Eomer/Eowyn character-specific)
    'dunland_':   'emp_',
    'rivendell_': 'emp_',
    'mirkwood_':  'emp_',
    'lothlorien_':'emp_',
    'erebor_':    'stu_',
    'isengard_':  'stu_',
    'gundabad_':  'stu_',
    'dolguldur_': 'stu_',
    'mordor_':    'stu_',
    'nazgul_':    'emp_',
    'khamul_':    'khu_',
    'witch_king_':'emp_',
    'dain_':      'stu_',
}

# Vanilla prefixes that should NOT be replaced
VANILLA_PREFIXES = ('emp_', 'stu_', 'ase_', 'vla_', 'bat_', 'khu_')

# Regex to match EquipmentSet lines
EQUIPMENT_SET_RE = re.compile(r'(<EquipmentSet\s+id=")([^"]+)(".*?/>)')

def get_vanilla_prefix(template_id):
    """Return the vanilla prefix for a custom template ID, or None if already vanilla."""
    if template_id.startswith(VANILLA_PREFIXES):
        return None
    for custom_prefix, vanilla_prefix in PREFIX_MAP.items():
        if template_id.startswith(custom_prefix):
            return vanilla_prefix
    return None  # Unknown prefix - leave unchanged

def replace_template(match, line):
    """Replace a custom equipment template with its vanilla equivalent."""
    prefix = match.group(1)
    template_id = match.group(2)
    suffix = match.group(3)

    vanilla_prefix = get_vanilla_prefix(template_id)
    if vanilla_prefix is None:
        return None  # No replacement needed

    is_civilian = 'civilian="true"' in line

    if is_civilian:
        new_id = f'{vanilla_prefix}civ_template_default'
    else:
        new_id = f'{vanilla_prefix}bat_template_medium'

    return f'{prefix}{new_id}{suffix}'

def main():
    with open(LORDS_XML, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    replacements = 0
    replaced_templates = {}  # old_id -> new_id for reporting

    new_lines = []
    for line in lines:
        match = EQUIPMENT_SET_RE.search(line)
        if match:
            template_id = match.group(2)
            result = replace_template(match, line)
            if result is not None:
                new_line = line[:match.start()] + result + line[match.end():]
                new_lines.append(new_line)
                replacements += 1

                # Track what was replaced
                new_match = EQUIPMENT_SET_RE.search(new_line)
                new_id = new_match.group(2) if new_match else '?'
                if template_id not in replaced_templates:
                    replaced_templates[template_id] = new_id
            else:
                new_lines.append(line)
        else:
            new_lines.append(line)

    with open(LORDS_XML, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)

    print(f"Replaced {replacements} equipment template references")
    print(f"Unique templates replaced: {len(replaced_templates)}")
    print()
    print("Mapping:")
    for old_id, new_id in sorted(replaced_templates.items()):
        print(f"  {old_id} -> {new_id}")

if __name__ == '__main__':
    main()
