#!/usr/bin/env python3
"""
Lords XSLT Completion Script for TAOM

Reads vanilla lords.xml and the current lords.xslt, and adds a template for every vanilla lord the file lacks.

--apply never rewrites a byte already in lords.xslt. It inserts a generated template for each missing vanilla lord
(main_hero excepted) into that lord's faction section, after the section's last template, and creates a missing
section where the file would have put it. It keeps the file's BOM and dominant line ending, parses the result before
writing, and writes only when the bytes differ (a file with mixed line endings comes back with its dominant one). On a
file that lacks no lord it is a byte-for-byte no-op. Everything else in lords.xslt (the #758 and #957 blocks,
Equipments, race, hand layout, comments) is the file's own.

Usage:
    python complete_lords_xslt.py --dry-run     # Preview: how many templates --apply would add
    python complete_lords_xslt.py --apply        # Insert the missing templates
    python complete_lords_xslt.py --export-csv   # Export attribute inventory to CSV
"""

import xml.etree.ElementTree as ET
from xml.sax.saxutils import unescape
import re
import os
import sys
import csv

from _gamedir import game_dir, ensure_exists

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(SCRIPT_DIR, '..')

VANILLA_LORDS = os.path.join(
    game_dir(r'E:\Steam\steamapps\common\Mount & Blade II Bannerlord'),
    'Modules', 'SandBox', 'ModuleData', 'lords.xml'
)
XSLT_PATH = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'lords.xslt')
CSV_PATH = os.path.join(SCRIPT_DIR, 'lords_inventory.csv')

# Attributes a generated template carries, in output order. Any other vanilla attribute never reaches a template:
# add it here (new lords) and hand-edit the existing templates.
ATTR_ORDER = [
    'id', 'name', 'age', 'voice', 'default_group', 'is_hero',
    'is_female', 'culture', 'skill_template', 'occupation', 'face_mesh_cache',
]

# Skip main_hero (player character)
SKIP_IDS = {'main_hero'}

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

RULE = '    <!-- ============================================== -->'
HEADERS = [section['header'] for section in FACTION_SECTIONS] + ['Unassigned Lords']

# A vanilla face with no BodyProperties gets this all-zero key.
BLANK_BODY = 'version="4" key="' + '0' * 130 + '"'

# Skills list
SKILL_IDS = [
    'OneHanded', 'TwoHanded', 'Polearm', 'Bow', 'Crossbow', 'Throwing',
    'Riding', 'Athletics', 'Crafting', 'Scouting', 'Tactics', 'Roguery',
    'Charm', 'Leadership', 'Trade', 'Steward', 'Medicine', 'Engineering',
]

# One parser for --dry-run, --export-csv and --apply. A template counts for a lord when its match names that
# NPCCharacter id in quotes, however the match is spelled (spaces, single or double quotes); a self-closing template
# has no block.
TEMPLATE = re.compile(r"<xsl:template\b([^>]*?)(?:/>|>(.*?)</xsl:template>)", re.DOTALL)
MATCH_ATTR = re.compile(r"""\bmatch\s*=\s*(?:"([^"]*)"|'([^']*)')""")
LORD_MATCH = re.compile(r"""\s*NPCCharacter\s*\[\s*@id\s*=\s*(?:'([^']+)'|"([^"]+)")\s*\]\s*$""")


def esc(text):
    """Escape XML special chars for XSLT attribute values."""
    if text is None:
        return ''
    return text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')


def parse_templates(text):
    """[(lord_id, block)] for every xsl:template of text that matches an NPCCharacter by quoted id."""
    found = []
    for template in TEMPLATE.finditer(text):
        attr = MATCH_ATTR.search(template.group(1))
        if attr:
            lord = LORD_MATCH.match(attr.group(1) if attr.group(1) is not None else attr.group(2))
            if lord:
                found.append((lord.group(1) or lord.group(2), template.group(2) or ''))
    return found


def template_ids(text):
    return [lord_id for lord_id, _ in parse_templates(text)]


def parse_vanilla_lords(filepath):
    """Parse vanilla lords.xml and return dict of {lord_id: lord_data}. Raises ValueError when it has no lords."""
    root = ET.parse(filepath).getroot()

    lords = {}
    for npc in root.findall('.//NPCCharacter'):
        lord_id = npc.get('id')
        if not lord_id or lord_id in SKIP_IDS:
            continue

        body = None
        face_elem = npc.find('face')
        bp = face_elem.find('BodyProperties') if face_elem is not None else None
        if bp is not None:
            body = ' '.join(f'{a}="{bp.get(a)}"' for a in ('version', 'age', 'weight', 'build', 'key')
                            if bp.get(a) is not None)

        skills = {}
        skills_elem = npc.find('skills')
        if skills_elem is not None:
            for skill in skills_elem.findall('skill'):
                skills[skill.get('id')] = skill.get('value', '0')

        traits = []
        traits_elem = npc.find('Traits')
        if traits_elem is not None:
            for trait in traits_elem.findall('Trait'):
                traits.append({'id': trait.get('id'), 'value': trait.get('value', '0')})

        lords[lord_id] = {'attrs': dict(npc.attrib), 'face': body, 'skills': skills, 'traits': traits}

    if not lords:
        raise ValueError(f'{filepath} has no NPCCharacter lords; refusing to treat it as the vanilla lord list')
    return lords


def parse_current_xslt(filepath):
    """{lord_id: {'overrides': the template's own xsl:attribute values, 'skills': its skill rows}} for the report."""
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    lords = {}
    attr_pattern = re.compile(r'<xsl:attribute name="([^"]+)">([^<]*)</xsl:attribute>')
    skill_pattern = re.compile(r'<skill id="(\w+)"\s+value="(\d+)"')
    for lord_id, block in parse_templates(content):
        overrides = {m.group(1): unescape(m.group(2), {'&apos;': "'", '&quot;': '"'})
                     for m in attr_pattern.finditer(block)}
        lords[lord_id] = {'overrides': overrides, 'skills': dict(skill_pattern.findall(block))}
    return lords


def merge_lords(vanilla, xslt_data):
    """Report data per vanilla lord: an existing template's own attributes, or what a new template would carry."""
    merged = {}

    for lord_id, v_data in vanilla.items():
        if lord_id in SKIP_IDS:
            continue

        x_data = xslt_data.get(lord_id)
        is_new = x_data is None
        extra = sorted(n for n in v_data['attrs'] if n not in ATTR_ORDER)

        if is_new:
            attrs = {n: v_data['attrs'][n] for n in ATTR_ORDER if n in v_data['attrs']}
            # is_female="false" is the default, so only a true is written.
            if attrs.get('is_female') == 'false':
                del attrs['is_female']
            vanilla_name = v_data['attrs'].get('name', lord_id)
            display_name = vanilla_name.split('}', 1)[1] if '}' in vanilla_name else vanilla_name
            attrs['name'] = f'{{=aom_{lord_id}_name}}{display_name}' if 'name' in v_data['attrs'] else lord_id
            attrs = {n: attrs[n] for n in ATTR_ORDER if n in attrs}
            # Rows beside a skill_template must equal its SkillSet (#626): vanilla's rows only when none is named.
            skills = {} if 'skill_template' in attrs else v_data['skills']
        else:
            attrs = x_data['overrides']
            skills = x_data['skills']

        merged[lord_id] = {
            'attrs': attrs,
            'face': v_data['face'],
            'skills': skills,
            'traits': v_data['traits'],
            'extra_attrs': extra,
            'is_new': is_new,
            'is_dead': lord_id.startswith('dead_lord'),
        }

    return merged


def build_traits_xml(traits):
    """Build a Traits XML block from a list of trait dicts."""
    lines = ['            <Traits>']
    lines.extend(f'                <Trait id="{t["id"]}" value="{t["value"]}"/>' for t in traits)
    lines.append('            </Traits>')
    return '\n'.join(lines)


def build_skills_xml(skills):
    """The <skills> block from {id: value}: an empty element when there are no rows."""
    if not skills:
        return '            <skills/>'
    rows = '\n'.join(f'                <skill id="{k}" value="{v}"/>' for k, v in skills.items())
    return f'            <skills>\n{rows}\n            </skills>'


def assign_to_faction(lord_id):
    """Determine which faction section a lord belongs to."""
    for i, section in enumerate(FACTION_SECTIONS):
        for prefix in section['prefixes'] + section['dead_prefixes']:
            if lord_id.startswith(prefix):
                return i
    return -1  # Unknown


def section_of(lord_id):
    """Index into HEADERS: the lord's faction section, or the Unassigned one."""
    index = assign_to_faction(lord_id)
    return index if index >= 0 else len(FACTION_SECTIONS)


def generate_lord_template(lord_id, data):
    """Generate a single XSLT template for a lord."""
    lines = []

    if data['is_new'] and data['is_dead']:
        lines.append(f'    <!-- {lord_id} \u2014 DEAD LORD: needs TAOM customization -->')
    else:
        lines.append(f'    <!-- {lord_id} \u2014 NEW: needs TAOM customization -->')

    lines.append(f"    <xsl:template match=\"NPCCharacter[@id='{lord_id}']\">")
    lines.append('        <xsl:copy>')
    for attr_name, attr_value in data['attrs'].items():
        lines.append(f'            <xsl:attribute name="{attr_name}">{esc(attr_value)}</xsl:attribute>')
    lines.append('            <face>')
    lines.append(f'                <BodyProperties {data["face"] or BLANK_BODY}/>')
    lines.append('            </face>')
    lines.append(build_skills_xml(data['skills']))
    lines.append(build_traits_xml(data['traits']))
    lines.append('            <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits)]"/>')
    lines.append('        </xsl:copy>')
    lines.append('    </xsl:template>')

    return '\n'.join(lines)


def banner(index):
    return f'{RULE}\n    <!-- {HEADERS[index]} -->\n{RULE}'


def insertion_point(text, index):
    """(offset, prefix) for a template of section `index`: after that section's last template, or where the section
    would go when the file has none. Insert prefix + text at the offset. However many blank lines sit around a banner
    or before </xsl:stylesheet>, a new section goes right after the preceding content."""
    close = text.rindex('</xsl:stylesheet>')
    start = text.find('\n' + banner(index), 0, close)
    if start >= 0:
        body_start = start + 1 + len(banner(index))
        next_rule = text.find('\n' + RULE, body_start, close)
        last = text.rfind('</xsl:template>', body_start, next_rule if next_rule >= 0 else close)
        return (last + len('</xsl:template>') if last >= 0 else body_start), '\n\n'
    for later in range(index + 1, len(HEADERS)):
        found = text.find('\n' + banner(later), 0, close)
        if found >= 0:
            return len(text[:found].rstrip()), '\n\n\n' + banner(index) + '\n\n'
    return len(text[:close].rstrip()), '\n\n\n' + banner(index) + '\n\n'


def complete_text(text, vanilla):
    """text (LF line endings, no BOM) with a generated template added for each vanilla lord it lacks."""
    have = set(template_ids(text))
    new = merge_lords({k: v for k, v in vanilla.items() if k not in have}, {})
    for lord_id in sorted(new, key=lambda i: (section_of(i), i)):
        offset, prefix = insertion_point(text, section_of(lord_id))
        text = text[:offset] + prefix + generate_lord_template(lord_id, new[lord_id]) + text[offset:]
    return text


def dominant_eol(old_bytes):
    """The file's own line terminator by majority (tools/generate_clan_heraldry.py, tools/README.md XML I/O)."""
    crlf = old_bytes.count(b'\r\n')
    return '\r\n' if crlf > old_bytes.count(b'\n') - crlf else '\n'


def encode_like(text, old_bytes):
    """text as bytes in the shape old_bytes had: its BOM if it had one and its dominant line ending."""
    bom = b'\xef\xbb\xbf' if old_bytes.startswith(b'\xef\xbb\xbf') else b''
    return bom + text.replace('\n', dominant_eol(old_bytes)).encode('utf-8')


def apply_to(xslt_path=XSLT_PATH, vanilla_path=VANILLA_LORDS):
    """Add the missing templates to xslt_path in place; returns the ids added. Raises ET.ParseError, writing nothing,
    when the result is not well-formed. A file that lacks no lord is not touched."""
    with open(xslt_path, 'rb') as f:
        old_bytes = f.read()
    old_text = old_bytes.decode('utf-8-sig').replace('\r\n', '\n')
    vanilla = parse_vanilla_lords(vanilla_path)
    text = complete_text(old_text, vanilla)
    ET.fromstring(text.encode('utf-8'))
    added = sorted(set(template_ids(text)) - set(template_ids(old_text)))
    new_bytes = encode_like(text, old_bytes)
    if added and new_bytes != old_bytes:
        with open(xslt_path, 'wb') as f:
            f.write(new_bytes)
    return added


def load_merged(vanilla_path=VANILLA_LORDS, xslt_path=XSLT_PATH):
    """Read both inputs and merge them for the report. Writes nothing."""
    return merge_lords(parse_vanilla_lords(vanilla_path), parse_current_xslt(xslt_path))


def export_csv(merged, csv_path):
    """Export lord inventory to CSV."""
    columns = ['id', 'name', 'age', 'voice', 'default_group', 'is_hero', 'is_female', 'culture', 'skill_template',
               'occupation', 'face_mesh_cache', 'race']
    headers = columns + ['is_new', 'is_dead'] + SKILL_IDS

    with open(csv_path, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(headers)

        for lord_id in sorted(merged.keys()):
            data = merged[lord_id]
            row = [data['attrs'].get(h, '') for h in columns]
            row.append('YES' if data['is_new'] else '')
            row.append('YES' if data['is_dead'] else '')
            row.extend(data['skills'].get(skill_id, '') for skill_id in SKILL_IDS)
            writer.writerow(row)

    print(f'Exported to {csv_path}')


def dry_run(merged):
    """Print summary of what --apply would add."""
    existing = [lid for lid in merged if not merged[lid]['is_new']]
    new_lords = [lid for lid in merged if merged[lid]['is_new']]
    dead_lords = [lid for lid in new_lords if merged[lid]['is_dead']]

    print('=' * 50)
    print('LORDS XSLT COMPLETION REPORT')
    print('=' * 50)
    print(f'Existing XSLT templates: {len(existing)}')
    print(f'New lords to add:        {len(new_lords)}')
    print(f'  Dead lords:            {len(dead_lords)}')
    print(f'  Living lords:          {len(new_lords) - len(dead_lords)}')
    print(f'Skipped:                 main_hero')
    print()

    for lid in sorted(new_lords):
        name = merged[lid]['attrs'].get('name', lid)
        display = name.split('}', 1)[1] if '}' in name else name
        label = 'DEAD LORD' if merged[lid]['is_dead'] else 'NEW'
        print(f'  {lid} ({merged[lid]["attrs"].get("culture", "?")}) - {label} - {display}')
    if new_lords:
        print()

    extras = {}
    for lid, data in merged.items():
        for name in data['extra_attrs']:
            extras.setdefault(name, []).append(lid)
    if extras:
        print('Vanilla attributes outside ATTR_ORDER (never written to a template; add one to ATTR_ORDER for new lords'
              ' and hand-edit the existing templates):')
        for name in sorted(extras):
            print(f'  {name}: {len(extras[name])} lord(s), e.g. {extras[name][0]}')
        print()

    print(f'--apply would add {len(new_lords)} template(s); bytes already in the file are never rewritten.')


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('--dry-run', '--apply', '--export-csv'):
        print(__doc__)
        sys.exit(1)

    mode = sys.argv[1]
    ensure_exists(VANILLA_LORDS, 'the SandBox lords.xml')

    if mode == '--apply':
        added = apply_to()
        if added:
            print(f'Added {len(added)} template(s) to {XSLT_PATH}: {", ".join(added)}')
        else:
            print(f'{XSLT_PATH} lacks no vanilla lord; unchanged.')
        return

    print(f'Parsing vanilla lords from {VANILLA_LORDS}...')
    print(f'Parsing current XSLT from {XSLT_PATH}...')
    merged = load_merged()
    print(f'  Merged: {len(merged)} lords')
    print()

    if mode == '--dry-run':
        dry_run(merged)
    elif mode == '--export-csv':
        export_csv(merged, CSV_PATH)


if __name__ == '__main__':
    main()
