#!/usr/bin/env python3
"""
Settlement Name & Owner Merge Tool for TAOM

Copies `name` and `owner` attributes from the authoritative repo settlements.xml
into the map maker's TAOM_Map settlements.xml, preserving all positional data,
comments, indentation, and child element structure in the target file.

Usage:
    python merge_settlements.py --dry-run    # Preview changes, do not write
    python merge_settlements.py --apply      # Write changes to map file
"""

import re
import os
import sys
import io
import xml.etree.ElementTree as ET

# Force UTF-8 output on Windows
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(SCRIPT_DIR, '..')

REPO_FILE = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'settlements.xml')
MAP_FILE = os.environ.get(
    'TAOM_MAP_FILE',
    r'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml'
)


def load_repo_data(repo_path):
    """Parse repo settlements.xml and return {id: {name, owner}} dict."""
    tree = ET.parse(repo_path)
    root = tree.getroot()
    data = {}
    for settlement in root.findall('Settlement'):
        sid = settlement.get('id')
        if sid:
            data[sid] = {
                'name': settlement.get('name'),
                'owner': settlement.get('owner'),
            }
    return data


def escape_replacement(value):
    """Escape backslashes in a regex replacement string."""
    return value.replace('\\', '\\\\')


def process_lines(lines, repo_data, dry_run):
    """
    Process map file lines, replacing name/owner attributes where they differ.
    Returns (updated_lines, stats, warnings).
    """
    updated_lines = []
    stats = {'names_updated': 0, 'owners_updated': 0, 'unchanged': 0}
    warnings = []

    for line in lines:
        if '<Settlement ' not in line:
            updated_lines.append(line)
            continue

        id_match = re.search(r'\bid="([^"]*)"', line)
        if not id_match:
            updated_lines.append(line)
            continue

        sid = id_match.group(1)

        if sid not in repo_data:
            warnings.append(f'[WARN]  {sid}: in map but not in repo (skipped)')
            updated_lines.append(line)
            continue

        repo = repo_data[sid]
        changed = False

        # Update name
        if repo['name']:
            name_match = re.search(r'name="([^"]*)"', line)
            if name_match and name_match.group(1) != repo['name']:
                old_display = name_match.group(1)
                new_display = repo['name']
                print(f'[NAME]  {sid}: "{old_display}" → "{new_display}"')
                line = re.sub(r'name="[^"]*"', f'name="{escape_replacement(new_display)}"', line)
                stats['names_updated'] += 1
                changed = True

        # Update owner (only if repo has one for this settlement)
        if repo['owner']:
            owner_match = re.search(r'owner="([^"]*)"', line)
            if owner_match and owner_match.group(1) != repo['owner']:
                old_owner = owner_match.group(1)
                new_owner = repo['owner']
                print(f'[OWNER] {sid}: "{old_owner}" → "{new_owner}"')
                line = re.sub(r'owner="[^"]*"', f'owner="{escape_replacement(new_owner)}"', line)
                stats['owners_updated'] += 1
                changed = True

        if not changed:
            stats['unchanged'] += 1

        updated_lines.append(line)

    return updated_lines, stats, warnings


def main():
    dry_run = '--dry-run' in sys.argv
    apply = '--apply' in sys.argv

    if not dry_run and not apply:
        print('Usage: merge_settlements.py --dry-run | --apply')
        sys.exit(1)

    if not os.path.exists(REPO_FILE):
        print(f'ERROR: Repo file not found: {REPO_FILE}')
        sys.exit(1)

    if not os.path.exists(MAP_FILE):
        print(f'ERROR: Map file not found: {MAP_FILE}')
        sys.exit(1)

    print(f'Source (repo): {REPO_FILE}')
    print(f'Target (map):  {MAP_FILE}')
    print(f'Mode: {"DRY RUN" if dry_run else "APPLY"}')
    print()

    repo_data = load_repo_data(REPO_FILE)
    print(f'Loaded {len(repo_data)} settlements from repo.')

    with open(MAP_FILE, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    print(f'Read {len(lines)} lines from map file.')
    print()

    updated_lines, stats, warnings = process_lines(lines, repo_data, dry_run)

    print()
    for w in warnings:
        print(w)
    if warnings:
        print()

    print(
        f'Summary: {stats["names_updated"]} names updated, '
        f'{stats["owners_updated"]} owners updated, '
        f'{stats["unchanged"]} unchanged, '
        f'{len(warnings)} warnings'
    )

    if apply:
        with open(MAP_FILE, 'w', encoding='utf-8') as f:
            f.writelines(updated_lines)
        print(f'\nWrote updated file to: {MAP_FILE}')
    else:
        print('\nDry run complete — no files were modified.')


if __name__ == '__main__':
    main()
