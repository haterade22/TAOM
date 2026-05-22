#!/usr/bin/env python3
"""Generate the missing v1.4.3 mandatory equipment rosters across TAOM's 12 cultures.

Per the v1.4.3 dev spec, every culture must provide at minimum 8 rosters with
specific Flags combinations:
    1. IsLordTemplate                          Battle      (male lord battle)
    2. IsLordTemplate + IsFemaleTemplate       Battle      (female lord battle)
    3. IsLordTemplate                          Civilian    (male lord civilian)
    4. IsLordTemplate + IsFemaleTemplate       Civilian    (female lord civilian)
    5. IsLordTemplate + IsChildEquipmentTemplate
    6. IsLordTemplate + IsChildEquipmentTemplate + IsFemaleTemplate
    7. IsLordTemplate + IsTeenagerEquipmentTemplate
    8. IsLordTemplate + IsTeenagerEquipmentTemplate + IsFemaleTemplate

#5 and #6 (child) are already satisfied for 10 cultures by
taom_child_equipment_templates.xml (post-S5a migration of IsNobleTemplate
to IsLordTemplate). Shaghana and Abanissa lack child entries entirely.

This script reads existing per-culture equipment files
(taom_equipment_sets_<culture>.xml), extracts the items from the FIRST
battle template and FIRST civilian template per culture, and writes a
new centralized file containing the missing rosters tagged with the
right Flags combinations. For shaghana and abanissa (which have no
per-culture equipment files), it falls back to vanilla aserai items
(closest Eastern aesthetic match).

Output: Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml

This is additive — it does NOT modify any existing equipment files.
The new file must be registered in Main/_Module/SubModule.xml as a new
<XmlNode> (the script prints the registration block to add).

Usage:
    python tools/generate_lord_template_equipment.py
    python tools/generate_lord_template_equipment.py --apply   # actually write
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from typing import Dict, List, Optional, Tuple

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
EQUIP_DIR = MODULE_DATA / "equipmentsets"
OUTPUT_FILE = EQUIP_DIR / "taom_lord_template_equipment.xml"

# 12 TAOM custom cultures from taom_spcultures.xml (is_main_culture="true").
# Mapping: culture_id -> per-culture equipment file stem (None = use fallback).
CULTURES: List[Tuple[str, Optional[str], Optional[str]]] = [
    # (culture_id, equipment_file_stem, fallback_for_missing_battle)
    ("gondor", "gondor", None),
    ("mordor", "mordor", None),
    ("erebor", "erebor", None),
    ("rivendell", "rivendell", None),
    ("lothlorien", "lothlorien", None),
    ("mirkwood", "mirkwood", None),
    ("isengard", "isengard", None),
    ("gundabad", "gundabad", None),
    ("dolguldur", "dolguldur", None),
    ("umbar", "umbar", None),
    # Shaghana + Abanissa are Harad sub-cultures (per kingdom-culture-mapping memory);
    # use harad equipment as fallback for both.
    ("shaghana", None, "harad"),
    ("abanissa", None, "harad"),
]


def extract_first_roster(file_stem: str, id_pattern: str) -> Optional[str]:
    """Return the contents (items only, not the wrapper EquipmentRoster tags)
    of the first matching <EquipmentRoster id="<file_stem>_<id_pattern>_*">.

    id_pattern is 'bat_template' or 'civ_template'. Returns the inner items as
    a single string with proper indentation, or None if no roster found.
    """
    file_path = EQUIP_DIR / f"taom_equipment_sets_{file_stem}.xml"
    if not file_path.exists():
        return None

    raw = file_path.read_text(encoding="utf-8", newline="")

    # Find first matching EquipmentRoster block.
    pattern = re.compile(
        r'<EquipmentRoster\s+id="' + re.escape(file_stem) + '_' + re.escape(id_pattern) + r'_[^"]*"[^>]*>'
        r'(.*?)'
        r'</EquipmentRoster>',
        re.DOTALL,
    )
    match = pattern.search(raw)
    if not match:
        return None

    inner = match.group(1)
    # Extract just the EquipmentSet children (drop any whitespace-only outer text).
    set_pattern = re.compile(r'(<EquipmentSet\b[^>]*>.*?</EquipmentSet>|<EquipmentSet\b[^>]*/>)', re.DOTALL)
    sets = set_pattern.findall(inner)
    if not sets:
        return None

    # Return the first EquipmentSet's content (it's enough — we don't need all).
    return sets[0]


def build_roster(roster_id: str, culture: str, equipment_set_xml: str, flags_attrs: str) -> str:
    """Build a complete <EquipmentRoster> block with the given flags + content."""
    # Reindent the equipment set to be a child of the new roster.
    lines = equipment_set_xml.split("\n")
    indented = "\n".join("    " + ln if ln.strip() else ln for ln in lines)

    return (
        f'  <EquipmentRoster id="{roster_id}" culture="Culture.{culture}">\n'
        f'{indented}\n'
        f'    <Flags {flags_attrs} />\n'
        f'  </EquipmentRoster>'
    )


def build_civilian_clone(equipment_set_xml: str) -> str:
    """If the source set is Battle (no equipmentType), make it Civilian for cloning."""
    if 'equipmentType="Civilian"' in equipment_set_xml:
        return equipment_set_xml
    # Add equipmentType="Civilian" attribute. Find the opening tag.
    m = re.match(r'<EquipmentSet\b([^>]*)>', equipment_set_xml)
    if not m:
        return equipment_set_xml
    attrs = m.group(1)
    if 'equipmentType=' in attrs:
        # Already has equipmentType, leave alone.
        return equipment_set_xml
    new_open = f'<EquipmentSet equipmentType="Civilian"{attrs}>'
    return new_open + equipment_set_xml[m.end():]


def build_battle_clone(equipment_set_xml: str) -> str:
    """If the source set is Civilian, strip equipmentType to make it Battle (implicit)."""
    return re.sub(r'\s+equipmentType="Civilian"', '', equipment_set_xml)


def generate() -> str:
    """Return the complete XML content as a string."""
    sections = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<!-- Generated by tools/generate_lord_template_equipment.py — DO NOT EDIT MANUALLY.',
        '     Provides v1.4.3 mandatory equipment rosters (IsLordTemplate variants) for each',
        '     TAOM custom culture. Items are sourced from existing per-culture equipment files;',
        '     shaghana and abanissa fall back to harad items. Regenerate this file if any of the',
        '     source per-culture equipment files change significantly. Registered in SubModule.xml.',
        '-->',
        '<EquipmentRosters>',
    ]

    for culture, file_stem, fallback in CULTURES:
        # Source items.
        battle_set = None
        civilian_set = None

        if file_stem:
            battle_set = extract_first_roster(file_stem, "bat_template")
            civilian_set = extract_first_roster(file_stem, "civ_template")

        # Fallback for cultures with no per-culture file.
        if (battle_set is None or civilian_set is None) and fallback:
            if battle_set is None:
                battle_set = extract_first_roster(fallback, "bat_template")
            if civilian_set is None:
                civilian_set = extract_first_roster(fallback, "civ_template")

        if battle_set is None or civilian_set is None:
            print(f"WARN: {culture}: missing source data — battle={battle_set is not None}, civilian={civilian_set is not None}", file=sys.stderr)
            continue

        # Derive variant sets.
        # For battle rosters, items should be battle-shaped (no equipmentType).
        # For civilian rosters, items should have equipmentType="Civilian".
        battle_set_clean = build_battle_clone(battle_set)
        civilian_set_clean = build_civilian_clone(civilian_set)
        # Teen rosters: civilian items are appropriate (TAOM doesn't have separate teen items).
        teen_set = civilian_set_clean

        sections.append(f"")
        sections.append(f"  <!-- ==================== {culture.upper()} ==================== -->")

        sections.append(build_roster(
            roster_id=f"taom_{culture}_lord_battle_male",
            culture=culture,
            equipment_set_xml=battle_set_clean,
            flags_attrs='IsLordTemplate="true"',
        ))

        sections.append(build_roster(
            roster_id=f"taom_{culture}_lord_battle_female",
            culture=culture,
            equipment_set_xml=battle_set_clean,
            flags_attrs='IsLordTemplate="true" IsFemaleTemplate="true"',
        ))

        sections.append(build_roster(
            roster_id=f"taom_{culture}_lord_civilian_male",
            culture=culture,
            equipment_set_xml=civilian_set_clean,
            flags_attrs='IsLordTemplate="true"',
        ))

        sections.append(build_roster(
            roster_id=f"taom_{culture}_lord_civilian_female",
            culture=culture,
            equipment_set_xml=civilian_set_clean,
            flags_attrs='IsLordTemplate="true" IsFemaleTemplate="true"',
        ))

        sections.append(build_roster(
            roster_id=f"taom_{culture}_lord_teen_male",
            culture=culture,
            equipment_set_xml=teen_set,
            flags_attrs='IsLordTemplate="true" IsTeenagerEquipmentTemplate="true"',
        ))

        sections.append(build_roster(
            roster_id=f"taom_{culture}_lord_teen_female",
            culture=culture,
            equipment_set_xml=teen_set,
            flags_attrs='IsLordTemplate="true" IsTeenagerEquipmentTemplate="true" IsFemaleTemplate="true"',
        ))

        # For shaghana + abanissa, also generate the child rosters that the other 10
        # cultures get via taom_child_equipment_templates.xml.
        if not file_stem:
            sections.append(build_roster(
                roster_id=f"taom_{culture}_lord_child_male",
                culture=culture,
                equipment_set_xml=civilian_set_clean,
                flags_attrs='IsLordTemplate="true" IsChildEquipmentTemplate="true"',
            ))
            sections.append(build_roster(
                roster_id=f"taom_{culture}_lord_child_female",
                culture=culture,
                equipment_set_xml=civilian_set_clean,
                flags_attrs='IsLordTemplate="true" IsChildEquipmentTemplate="true" IsFemaleTemplate="true"',
            ))

    sections.append("")
    sections.append("</EquipmentRosters>")
    return "\n".join(sections) + "\n"


def main(argv: List[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true",
                    help="Write the generated XML to the output file. Default: dry-run (print to stdout).")
    args = ap.parse_args(argv)

    xml_content = generate()
    if args.apply:
        OUTPUT_FILE.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT_FILE.write_text(xml_content, encoding="utf-8", newline="\n")
        print(f"Written: {OUTPUT_FILE}")
        # Count rosters generated.
        roster_count = xml_content.count("<EquipmentRoster ")
        print(f"Rosters generated: {roster_count}")
        print()
        print("Register in Main/_Module/SubModule.xml under <Xmls>:")
        print('    <XmlNode>')
        print('      <XmlName id="EquipmentRosters" path="equipmentsets/taom_lord_template_equipment"/>')
        print('      <IncludedGameTypes>')
        print('        <GameType value="Campaign"/>')
        print('        <GameType value="CampaignStoryMode"/>')
        print('        <GameType value="CustomGame"/>')
        print('        <GameType value="EditorGame"/>')
        print('      </IncludedGameTypes>')
        print('    </XmlNode>')
    else:
        print(xml_content)
        print()
        print("=== DRY-RUN — re-run with --apply to write ===", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
