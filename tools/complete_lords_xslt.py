#!/usr/bin/env python3
"""
Lords XSLT Completion Script for TAOM

Reads vanilla lords.xml and current lords.xslt, then regenerates lords.xslt
with ALL attributes explicit (no passthrough). Also adds missing lords.

Usage:
    python complete_lords_xslt.py --dry-run     # Preview changes
    python complete_lords_xslt.py --apply        # Write new lords.xslt
    python complete_lords_xslt.py --export-csv   # Export attribute inventory to CSV
"""

import xml.etree.ElementTree as ET
import re
import os
import sys
import csv

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(SCRIPT_DIR, '..')

VANILLA_LORDS = os.path.join(
    'E:', os.sep, 'Steam', 'steamapps', 'common',
    'Mount & Blade II Bannerlord', 'Modules', 'SandBox', 'ModuleData', 'lords.xml'
)
XSLT_PATH = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'lords.xslt')
CSV_PATH = os.path.join(SCRIPT_DIR, 'lords_inventory.csv')

# Attribute output order
ATTR_ORDER = [
    'id', 'name', 'age', 'voice', 'default_group', 'is_hero',
    'is_female', 'culture', 'skill_template', 'occupation', 'face_mesh_cache',
]

# Skip main_hero (player character)
SKIP_IDS = {'main_hero'}

# Lords TAOM re-genders relative to vanilla.
# The vanilla id is reused for a different canon character, so vanilla's
# is_female is wrong and must not be copied through on regeneration.
# lord_4_6 is vanilla Countess Calatild, reused as Grimbold of Grimslade.
GENDER_OVERRIDES = {
    'lord_4_6': None,   # None = male (attribute omitted entirely)
}

# Faction groupings for organizing output
FACTION_SECTIONS = [
    {
        'header': 'Faction 1 - Empire/Dunland',
        'prefixes': ['lord_1_'],
        'dead_prefixes': [],
    },
    {
        'header': 'Faction 2 - Sturgia/Dale',
        'prefixes': ['lord_2_'],
        'dead_prefixes': ['dead_lord_2_'],
    },
    {
        'header': 'Faction 3 - Aserai/Harad',
        'prefixes': ['lord_3_'],
        'dead_prefixes': ['dead_lord_3_'],
    },
    {
        'header': 'Faction 4 - Vlandia/Rohan',
        'prefixes': ['lord_4_', 'lord_V'],
        'dead_prefixes': [],
    },
    {
        'header': 'Faction 5 - Battania/Woodland Realm',
        'prefixes': ['lord_5_'],
        'dead_prefixes': [],
    },
    {
        'header': 'Faction 6 - Khuzait/Rhun',
        'prefixes': ['lord_6_', 'lord_K'],
        'dead_prefixes': ['dead_lord_6_'],
    },
]

# Skills list
SKILL_IDS = [
    'OneHanded', 'TwoHanded', 'Polearm', 'Bow', 'Crossbow', 'Throwing',
    'Riding', 'Athletics', 'Crafting', 'Scouting', 'Tactics', 'Roguery',
    'Charm', 'Leadership', 'Trade', 'Steward', 'Medicine', 'Engineering',
]


def esc(text):
    """Escape XML special chars for XSLT attribute values."""
    if text is None:
        return ''
    return text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')


def normalize_indent(xml_block, target_indent=12):
    """Normalize indentation of an XML block to target indent level (in spaces)."""
    lines = xml_block.split('\n')
    result = []
    for line in lines:
        stripped = line.strip()
        if not stripped:
            continue
        # Determine depth relative to root element
        # Count leading spaces in original to estimate depth
        orig_spaces = len(line) - len(line.lstrip())
        result.append(stripped)

    if not result:
        return xml_block

    # Re-indent: first line at target_indent, nested lines get +4
    indent = ' ' * target_indent
    output = []
    for line in result:
        if line.startswith('</'):
            # Closing tag — same indent as opening
            output.append(f'{indent}{line}')
        elif line.startswith('<') and not line.startswith('<?'):
            # Check if it's a nested element
            # Count nesting by checking tag names
            output.append(f'{indent}{line}')
        else:
            output.append(f'{indent}{line}')

    # Better approach: use the structure of the XML
    # The face block is: <face> -> <BodyProperties .../> -> </face>
    # The Traits block is: <Traits> -> <Trait .../> ... -> </Traits>
    # Re-indent based on nesting level
    output = []
    depth = 0
    for line in result:
        if line.startswith('</'):
            depth -= 1
            output.append(' ' * (target_indent + depth * 4) + line)
        elif line.endswith('/>'):
            # Self-closing tag at current depth
            output.append(' ' * (target_indent + depth * 4) + line)
        elif line.startswith('<') and not line.endswith('/>'):
            output.append(' ' * (target_indent + depth * 4) + line)
            if not line.startswith('</') and '>' in line and not line.endswith('/>'):
                depth += 1
        else:
            output.append(' ' * (target_indent + depth * 4) + line)

    return '\n'.join(output)


def parse_vanilla_lords(filepath):
    """Parse vanilla lords.xml and return dict of {lord_id: lord_data}."""
    tree = ET.parse(filepath)
    root = tree.getroot()

    lords = {}
    for npc in root.findall('.//NPCCharacter'):
        lord_id = npc.get('id')
        if not lord_id or lord_id in SKIP_IDS:
            continue

        # Extract attributes
        attrs = dict(npc.attrib)

        # Extract face XML
        face_elem = npc.find('face')
        face_xml = extract_vanilla_face(face_elem) if face_elem is not None else None

        # Extract skills
        skills_elem = npc.find('skills')
        skills = {}
        if skills_elem is not None:
            for skill in skills_elem.findall('skill'):
                skills[skill.get('id')] = skill.get('value', '0')

        # Extract traits
        traits_elem = npc.find('Traits')
        traits = []
        if traits_elem is not None:
            for trait in traits_elem.findall('Trait'):
                traits.append({'id': trait.get('id'), 'value': trait.get('value', '0')})

        lords[lord_id] = {
            'attrs': attrs,
            'face_xml': face_xml,
            'skills': skills,
            'traits': traits,
        }

    return lords


def extract_vanilla_face(face_elem):
    """Extract face element as raw XML string from ET element."""
    bp = face_elem.find('BodyProperties')
    if bp is None:
        return None

    parts = []
    # Build BodyProperties tag
    bp_attrs = []
    for attr in ['version', 'age', 'weight', 'build', 'key']:
        val = bp.get(attr)
        if val is not None:
            bp_attrs.append(f'{attr}="{val}"')
    parts.append(f'                <BodyProperties {" ".join(bp_attrs)}/>')

    return '\n'.join(parts)


def parse_current_xslt(filepath):
    """Parse current lords.xslt with regex to extract TAOM overrides."""
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    lords = {}

    # Match each template block
    template_pattern = re.compile(
        r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]\">(.*?)</xsl:template>",
        re.DOTALL
    )

    for match in template_pattern.finditer(content):
        lord_id = match.group(1)
        block = match.group(2)

        # Extract explicit attribute overrides
        attr_pattern = re.compile(
            r'<xsl:attribute name="([^"]+)">([^<]*)</xsl:attribute>'
        )
        overrides = {}
        for attr_match in attr_pattern.finditer(block):
            overrides[attr_match.group(1)] = attr_match.group(2)

        # Extract face block (raw XML between <face> and </face>)
        face_match = re.search(r'(<face>.*?</face>)', block, re.DOTALL)
        face_xml = face_match.group(1) if face_match else None

        # Extract skills block
        # Handle populated skills
        skills_match = re.search(r'<skills>(.*?)</skills>', block, re.DOTALL)
        skills_xml = None
        if skills_match:
            skills_content = skills_match.group(1).strip()
            if skills_content:
                skills_xml = skills_content
            else:
                skills_xml = ''  # Empty skills block
        else:
            # Handle self-closing <skills/>
            if re.search(r'<skills\s*/>', block):
                skills_xml = ''

        # Extract traits block
        traits_match = re.search(r'(<Traits>.*?</Traits>)', block, re.DOTALL)
        traits_xml = traits_match.group(1) if traits_match else None

        lords[lord_id] = {
            'overrides': overrides,
            'face_xml': face_xml,
            'skills_xml': skills_xml,
            'traits_xml': traits_xml,
        }

    # Also extract the ordering of lord IDs as they appear in the XSLT
    lord_order = [m.group(1) for m in template_pattern.finditer(content)]

    return lords, lord_order


def merge_lords(vanilla, xslt_data):
    """Merge vanilla attributes with TAOM overrides."""
    merged = {}

    for lord_id, v_data in vanilla.items():
        if lord_id in SKIP_IDS:
            continue

        x_data = xslt_data.get(lord_id)
        is_new = x_data is None

        # Build merged attributes
        attrs = {}
        for attr_name in ATTR_ORDER:
            vanilla_val = v_data['attrs'].get(attr_name)

            # Forced gender: TAOM reuses some vanilla ids for a different
            # canon character, so vanilla's is_female must never win here.
            if attr_name == 'is_female' and lord_id in GENDER_OVERRIDES:
                forced = GENDER_OVERRIDES[lord_id]
                if forced is not None:
                    attrs[attr_name] = forced
                continue

            # TAOM overrides for specific attributes
            if x_data and attr_name in x_data['overrides']:
                attrs[attr_name] = x_data['overrides'][attr_name]
            elif vanilla_val is not None:
                if attr_name == 'is_female' and vanilla_val == 'false':
                    # Skip is_female="false" — only include when true or overridden
                    if x_data and 'is_female' in x_data['overrides']:
                        attrs[attr_name] = x_data['overrides']['is_female']
                    # else omit
                else:
                    attrs[attr_name] = vanilla_val

        # For new lords, generate placeholder TAOM name
        if is_new and 'name' not in attrs:
            vanilla_name = v_data['attrs'].get('name', lord_id)
            attrs['name'] = vanilla_name
        elif is_new:
            # Keep vanilla name but add aom prefix
            vanilla_name = v_data['attrs'].get('name', lord_id)
            # Extract display name from {=tag}Name format
            if '}' in vanilla_name:
                display_name = vanilla_name.split('}', 1)[1]
            else:
                display_name = vanilla_name
            attrs['name'] = f'{{=aom_{lord_id}_name}}{display_name}'

        # Face: TAOM face wins if exists, else vanilla
        if x_data and x_data['face_xml']:
            face_xml = x_data['face_xml']
        elif v_data['face_xml']:
            face_xml = f'            <face>\n{v_data["face_xml"]}\n            </face>'
        else:
            face_xml = '            <face>\n                <BodyProperties version="4" key="0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"/>\n            </face>'

        # Skills: TAOM skills win if exists
        if x_data and x_data['skills_xml'] is not None:
            skills_xml = x_data['skills_xml']
        else:
            skills_xml = ''  # Empty skills for new lords

        # Traits: TAOM traits win if exists
        if x_data and x_data['traits_xml']:
            traits_xml = x_data['traits_xml']
        else:
            # Build from vanilla traits
            traits_xml = build_traits_xml(v_data['traits'])

        merged[lord_id] = {
            'attrs': attrs,
            'face_xml': face_xml,
            'skills_xml': skills_xml,
            'traits_xml': traits_xml,
            'is_new': is_new,
            'is_dead': lord_id.startswith('dead_lord'),
        }

    return merged


def build_traits_xml(traits):
    """Build a Traits XML block from a list of trait dicts."""
    if not traits:
        return '            <Traits>\n            </Traits>'
    lines = ['            <Traits>']
    for t in traits:
        lines.append(f'                <Trait id="{t["id"]}" value="{t["value"]}"/>')
    lines.append('            </Traits>')
    return '\n'.join(lines)


def build_skills_xml(skills_content):
    """Build the skills XML from raw content or empty marker."""
    if not skills_content:
        return '            <skills/>'
    # skills_content is the inner content of <skills>...</skills>
    return f'            <skills>\n{skills_content}\n            </skills>'


def assign_to_faction(lord_id):
    """Determine which faction section a lord belongs to."""
    for i, section in enumerate(FACTION_SECTIONS):
        for prefix in section['prefixes']:
            if lord_id.startswith(prefix):
                return i
        for prefix in section['dead_prefixes']:
            if lord_id.startswith(prefix):
                return i
    return -1  # Unknown


def generate_lord_template(lord_id, data):
    """Generate a single XSLT template for a lord."""
    lines = []

    # Comment
    if data['is_new'] and data['is_dead']:
        lines.append(f'    <!-- {lord_id} — DEAD LORD: needs TAOM customization -->')
    elif data['is_new']:
        lines.append(f'    <!-- {lord_id} — NEW: needs TAOM customization -->')
    else:
        lines.append(f'    <!-- {lord_id} -->')

    lines.append(f"    <xsl:template match=\"NPCCharacter[@id='{lord_id}']\">")
    lines.append('        <xsl:copy>')

    # All explicit attributes
    for attr_name in ATTR_ORDER:
        if attr_name in data['attrs']:
            val = esc(data['attrs'][attr_name])
            lines.append(f'            <xsl:attribute name="{attr_name}">{val}</xsl:attribute>')

    # Face
    face = data['face_xml']
    lines.append(normalize_indent(face, 12))

    # Skills
    skills = data['skills_xml']
    if isinstance(skills, str) and skills.strip():
        # Wrap inner content in <skills> tags and normalize
        skills_block = f'<skills>\n{skills}\n</skills>'
        lines.append(normalize_indent(skills_block, 12))
    else:
        lines.append('            <skills/>')

    # Traits
    traits = data['traits_xml']
    lines.append(normalize_indent(traits, 12))

    # Node passthrough for Equipments etc.
    lines.append('            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>')
    lines.append('        </xsl:copy>')
    lines.append('    </xsl:template>')

    return '\n'.join(lines)


def generate_xslt(merged, xslt_order):
    """Generate the complete XSLT file."""
    parts = []

    # Header
    parts.append('<?xml version="1.0" encoding="utf-8"?>')
    parts.append('<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">')
    parts.append('    <xsl:output method="xml" indent="yes" encoding="utf-8"/>')
    parts.append('')
    parts.append('    <!-- Identity transformation - copies everything by default -->')
    parts.append('    <xsl:template match="@*|node()">')
    parts.append('        <xsl:copy>')
    parts.append('            <xsl:apply-templates select="@*|node()"/>')
    parts.append('        </xsl:copy>')
    parts.append('    </xsl:template>')

    # Group lords by faction
    faction_lords = {i: [] for i in range(len(FACTION_SECTIONS))}
    unassigned = []

    # First, add existing lords in their original order
    existing_assigned = set()
    for lord_id in xslt_order:
        if lord_id in merged:
            faction_idx = assign_to_faction(lord_id)
            if faction_idx >= 0:
                faction_lords[faction_idx].append(lord_id)
                existing_assigned.add(lord_id)

    # Then, add new lords to their faction sections
    new_lords = sorted([lid for lid in merged if lid not in existing_assigned])
    for lord_id in new_lords:
        faction_idx = assign_to_faction(lord_id)
        if faction_idx >= 0:
            faction_lords[faction_idx].append(lord_id)
        else:
            unassigned.append(lord_id)

    # Generate each faction section
    for i, section in enumerate(FACTION_SECTIONS):
        lords_in_section = faction_lords[i]
        if not lords_in_section:
            continue

        parts.append('')
        parts.append('')
        parts.append(f'    <!-- ============================================== -->')
        parts.append(f'    <!-- {section["header"]} -->')
        parts.append(f'    <!-- ============================================== -->')

        for lord_id in lords_in_section:
            parts.append('')
            parts.append(generate_lord_template(lord_id, merged[lord_id]))

    # Any unassigned lords
    if unassigned:
        parts.append('')
        parts.append('')
        parts.append('    <!-- ============================================== -->')
        parts.append('    <!-- Unassigned Lords -->')
        parts.append('    <!-- ============================================== -->')
        for lord_id in unassigned:
            parts.append('')
            parts.append(generate_lord_template(lord_id, merged[lord_id]))

    parts.append('')
    parts.append('</xsl:stylesheet>')

    return '\n'.join(parts)


def export_csv(merged, csv_path):
    """Export lord inventory to CSV."""
    headers = ['id', 'name', 'age', 'voice', 'default_group', 'is_hero',
               'is_female', 'culture', 'skill_template', 'occupation',
               'face_mesh_cache', 'is_new', 'is_dead']
    # Add skill columns
    headers.extend(SKILL_IDS)

    with open(csv_path, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(headers)

        for lord_id in sorted(merged.keys()):
            data = merged[lord_id]
            row = []
            for h in ['id', 'name', 'age', 'voice', 'default_group', 'is_hero',
                       'is_female', 'culture', 'skill_template', 'occupation',
                       'face_mesh_cache']:
                row.append(data['attrs'].get(h, ''))
            row.append('YES' if data['is_new'] else '')
            row.append('YES' if data['is_dead'] else '')

            # Parse skills from skills_xml
            skills_content = data['skills_xml'] if isinstance(data['skills_xml'], str) else ''
            skill_values = {}
            for skill_match in re.finditer(r'id="(\w+)"\s+value="(\d+)"', skills_content):
                skill_values[skill_match.group(1)] = skill_match.group(2)
            for skill_id in SKILL_IDS:
                row.append(skill_values.get(skill_id, ''))

            writer.writerow(row)

    print(f'Exported to {csv_path}')


def dry_run(merged, xslt_order):
    """Print summary of what would change."""
    existing = [lid for lid in merged if not merged[lid]['is_new']]
    new_lords = [lid for lid in merged if merged[lid]['is_new']]
    dead_lords = [lid for lid in new_lords if merged[lid]['is_dead']]
    living_new = [lid for lid in new_lords if not merged[lid]['is_dead']]

    print('=' * 50)
    print('LORDS XSLT COMPLETION REPORT')
    print('=' * 50)
    print(f'Existing XSLT templates: {len(existing)}')
    print(f'New lords to add:        {len(new_lords)}')
    print(f'  Dead lords:            {len(dead_lords)}')
    print(f'  Living lords:          {len(living_new)}')
    print(f'Total output templates:  {len(merged)}')
    print(f'Skipped:                 main_hero')
    print()

    if new_lords:
        print('New lords:')
        for lid in sorted(new_lords):
            culture = merged[lid]['attrs'].get('culture', '?')
            label = 'DEAD LORD' if merged[lid]['is_dead'] else 'NEW'
            name = merged[lid]['attrs'].get('name', lid)
            # Extract display name
            if '}' in name:
                display = name.split('}', 1)[1]
            else:
                display = name
            print(f'  {lid} ({culture}) — {label} — {display}')
        print()

    # Count attributes per template
    attr_counts = []
    for lid in merged:
        attr_counts.append(len(merged[lid]['attrs']))
    if attr_counts:
        print(f'Attributes per template: {min(attr_counts)}-{max(attr_counts)} (was 2-3)')

    # Count lords with populated skills vs empty
    with_skills = sum(1 for lid in merged
                      if isinstance(merged[lid]['skills_xml'], str)
                      and merged[lid]['skills_xml'].strip())
    without_skills = len(merged) - with_skills
    print(f'Lords with explicit skills: {with_skills}')
    print(f'Lords with empty skills:    {without_skills}')

    # Show faction distribution
    print()
    print('Faction distribution:')
    for i, section in enumerate(FACTION_SECTIONS):
        count = sum(1 for lid in merged if assign_to_faction(lid) == i)
        new_count = sum(1 for lid in merged
                        if assign_to_faction(lid) == i and merged[lid]['is_new'])
        if count:
            new_str = f' (+{new_count} new)' if new_count else ''
            print(f'  {section["header"]}: {count}{new_str}')


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('--dry-run', '--apply', '--export-csv'):
        print(__doc__)
        sys.exit(1)

    mode = sys.argv[1]

    # Parse inputs
    print(f'Parsing vanilla lords from {VANILLA_LORDS}...')
    vanilla = parse_vanilla_lords(VANILLA_LORDS)
    print(f'  Found {len(vanilla)} lords (excluding main_hero)')

    print(f'Parsing current XSLT from {XSLT_PATH}...')
    xslt_data, xslt_order = parse_current_xslt(XSLT_PATH)
    print(f'  Found {len(xslt_data)} templates')

    # Merge
    merged = merge_lords(vanilla, xslt_data)
    print(f'  Merged: {len(merged)} lords')
    print()

    if mode == '--dry-run':
        dry_run(merged, xslt_order)

    elif mode == '--apply':
        xslt_content = generate_xslt(merged, xslt_order)
        with open(XSLT_PATH, 'w', encoding='utf-8') as f:
            f.write(xslt_content)
        template_count = len(re.findall(r'xsl:template match', xslt_content))
        attr_count = len(re.findall(r'xsl:attribute name=', xslt_content))
        print(f'Written to {XSLT_PATH}')
        print(f'  Templates: {template_count} (including identity)')
        print(f'  Explicit attributes: {attr_count}')
        print(f'  File size: {len(xslt_content):,} chars')

    elif mode == '--export-csv':
        export_csv(merged, CSV_PATH)


if __name__ == '__main__':
    main()
