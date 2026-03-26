#!/usr/bin/env python3
"""
Fix lord cultures in lords.xslt and add mounts to battle equipment sets.

Tasks:
1. Update Gondor lord cultures from Culture.empire -> Culture.gondor in lords.xslt
2. Update Mordor lord cultures from Culture.empire -> Culture.mordor in lords.xslt
3. Fix Boromir (lord_1_75) to Culture.gondor in lords.xslt
4. Update lord cultures in lords.xml for matching IDs
5. Add Horse/HorseHarness to battle equipment templates in taom_equipment_sets_*.xml

Usage:
    python fix_lord_cultures_and_mounts.py --dry-run
    python fix_lord_cultures_and_mounts.py --apply
"""

import argparse
import os
import re
import sys
from pathlib import Path

# Paths
REPO_ROOT = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
SPLORDS_XSLT = MODULE_DATA / "splords.xslt"
LORDS_XSLT = MODULE_DATA / "lords.xslt"
LORDS_XML = MODULE_DATA / "characters" / "lords.xml"

# Mount configurations per culture
HORSE_CULTURES = {
    "gondor", "rohan", "dale", "dunland", "harad", "rhun",
    "mirkwood", "rivendell", "lothlorien", "erebor", "mordor",
}
WARG_CULTURES = {"isengard", "dolguldur", "gundabad"}

# Mount items
CHARGER_HORSE = "Item.charger"
CHARGER_HARNESS = "Item.chain_horse_harness"
WARG_HORSE = "Item.warg_brown"
WARG_HARNESS = "Item.warg_saddle"

# Named lord battle equipment that needs mounts
NAMED_LORDS_CHARGER = [
    "sauron_bat_equipment",
    "witchking_bat_equipment",
    "nazgul_bat_equipment",
    "nazgul_v1_bat_equipment",
    "boromir_bat_equipment",
    "faramir_bat_equipment",
    "eowyn_bat_equipment",
    "thranduil_bat_equipment",
    "legolas_bat_equipment",
    "galadriel_bat_equipment",
]
# theoden and eomer already have charger - skip
# dain - no horse (dwarves don't ride) - skip

# Gondor section lord IDs from splords.xslt lines 199-255
GONDOR_LORD_IDS = {
    "lord_1_7", "lord_1_8", "lord_1_9", "lord_1_10",
    "lord_1_11", "lord_1_111", "lord_1_12",
}
# Extra Gondor lords (in Mordor section of splords.xslt but actually Gondor)
EXTRA_GONDOR_IDS = {"lord_1_75", "lord_1_34"}  # Boromir and Faramir


def extract_lord_ids_from_splords(filepath, start_line, end_line):
    """Extract lord IDs from splords.xslt between given line numbers."""
    ids = set()
    with open(filepath, "r", encoding="utf-8") as f:
        for i, line in enumerate(f, 1):
            if start_line <= i <= end_line:
                m = re.search(r"@id='(lord_[^']+)'", line)
                if m:
                    ids.add(m.group(1))
    return ids


def fix_lords_xslt_cultures(content, gondor_ids, mordor_ids, dry_run):
    """Fix culture attributes in lords.xslt for Gondor and Mordor lords."""
    changes = []
    lines = content.split("\n")
    new_lines = []

    i = 0
    while i < len(lines):
        line = lines[i]

        # Check if this is a lord template match line
        m = re.search(r"match=\"NPCCharacter\[@id='(lord_[^']+)'\]\"", line)
        if m:
            lord_id = m.group(1)
            target_culture = None

            if lord_id in gondor_ids:
                target_culture = "Culture.gondor"
            elif lord_id in mordor_ids:
                target_culture = "Culture.mordor"

            if target_culture:
                # Look ahead for the culture attribute line (within next 15 lines)
                new_lines.append(line)
                i += 1
                found = False
                lookahead = 0
                while i < len(lines) and lookahead < 15:
                    cur_line = lines[i]
                    culture_match = re.search(
                        r'(<xsl:attribute name="culture">)(Culture\.[^<]+)(</xsl:attribute>)',
                        cur_line,
                    )
                    if culture_match:
                        old_culture = culture_match.group(2)
                        if old_culture != target_culture:
                            new_line = cur_line.replace(
                                f">{old_culture}<", f">{target_culture}<"
                            )
                            new_lines.append(new_line)
                            changes.append(
                                f"lords.xslt: {lord_id}: {old_culture} -> {target_culture}"
                            )
                        else:
                            new_lines.append(cur_line)
                        i += 1
                        found = True
                        break
                    # Check if we hit the next template (abort)
                    if "xsl:template" in cur_line and lookahead > 0:
                        new_lines.append(cur_line)
                        i += 1
                        break
                    new_lines.append(cur_line)
                    i += 1
                    lookahead += 1
                continue

        new_lines.append(line)
        i += 1

    return "\n".join(new_lines), changes


def fix_lords_xml_cultures(content, gondor_ids, mordor_ids, dry_run):
    """Fix culture attributes in lords.xml for Gondor and Mordor lords."""
    changes = []

    def replace_culture(match):
        full_match = match.group(0)
        lord_id = match.group(1)
        old_culture = match.group(2)

        if lord_id in gondor_ids:
            target = "Culture.gondor"
        elif lord_id in mordor_ids:
            target = "Culture.mordor"
        else:
            return full_match

        if old_culture != target:
            changes.append(f"lords.xml: {lord_id}: {old_culture} -> {target}")
            return full_match.replace(
                f'culture="{old_culture}"', f'culture="{target}"'
            )
        return full_match

    # Match NPCCharacter lines with id and culture attributes
    pattern = r'<NPCCharacter[^>]*\bid="(lord_1_[^"]+)"[^>]*culture="(Culture\.[^"]+)"[^>]*>'
    new_content = re.sub(pattern, replace_culture, content)

    return new_content, changes


def add_mounts_to_equipment(filepath, dry_run):
    """Add Horse and HorseHarness to battle equipment templates."""
    changes = []
    content = filepath.read_text(encoding="utf-8")

    # Determine culture from filename
    fname = filepath.stem  # e.g. taom_equipment_sets_gondor
    culture = fname.replace("taom_equipment_sets_", "")

    # Determine mount type
    if culture in WARG_CULTURES:
        horse_item = WARG_HORSE
        harness_item = WARG_HARNESS
    elif culture in HORSE_CULTURES:
        horse_item = CHARGER_HORSE
        harness_item = CHARGER_HARNESS
    else:
        return content, changes

    # Process battle template rosters
    lines = content.split("\n")
    new_lines = []
    i = 0

    while i < len(lines):
        line = lines[i]

        # Check for battle template medium roster OR named lord battle equipment
        bat_template_match = re.search(
            r'id="(\w+_bat_template_medium_\w+)"', line
        )
        named_lord_match = re.search(
            r'id="(\w+_bat_equipment)"', line
        )

        roster_id = None
        mount_horse = None
        mount_harness = None

        if bat_template_match:
            roster_id = bat_template_match.group(1)
            mount_horse = horse_item
            mount_harness = harness_item
        elif named_lord_match:
            roster_id = named_lord_match.group(1)
            if roster_id in NAMED_LORDS_CHARGER:
                mount_horse = CHARGER_HORSE
                mount_harness = CHARGER_HARNESS

        if roster_id and mount_horse:
            # Find the </EquipmentSet> closing tag and check if Horse already exists
            new_lines.append(line)
            i += 1
            equipment_lines = []
            has_horse = False
            has_harness = False

            while i < len(lines):
                cur_line = lines[i]
                if 'slot="Horse"' in cur_line:
                    has_horse = True
                if 'slot="HorseHarness"' in cur_line:
                    has_harness = True

                if "</EquipmentSet>" in cur_line:
                    # Insert mount lines before </EquipmentSet>
                    if not has_horse:
                        # Detect indentation from previous equipment lines
                        indent = "\t\t\t"
                        for el in equipment_lines:
                            m = re.match(r"^(\s+)<Equipment", el)
                            if m:
                                indent = m.group(1)
                                break

                        if not has_harness:
                            equipment_lines.append(
                                f'{indent}<Equipment slot="HorseHarness" id="{mount_harness}" />'
                            )
                            changes.append(
                                f"{filepath.name}: {roster_id}: added HorseHarness={mount_harness}"
                            )
                        equipment_lines.append(
                            f'{indent}<Equipment slot="Horse" id="{mount_horse}" />'
                        )
                        changes.append(
                            f"{filepath.name}: {roster_id}: added Horse={mount_horse}"
                        )

                    new_lines.extend(equipment_lines)
                    new_lines.append(cur_line)
                    i += 1
                    break
                else:
                    equipment_lines.append(cur_line)
                    i += 1
            continue

        new_lines.append(line)
        i += 1

    return "\n".join(new_lines), changes


def main():
    parser = argparse.ArgumentParser(description="Fix lord cultures and add mounts")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--dry-run", action="store_true", help="Show what would change")
    group.add_argument("--apply", action="store_true", help="Apply changes")
    args = parser.parse_args()

    all_changes = []

    # Step 1: Extract lord IDs from splords.xslt
    print("=== Extracting lord IDs from splords.xslt ===")
    gondor_ids = extract_lord_ids_from_splords(SPLORDS_XSLT, 199, 255)
    mordor_ids = extract_lord_ids_from_splords(SPLORDS_XSLT, 256, 1040)

    # Add extra Gondor IDs
    gondor_ids.update(EXTRA_GONDOR_IDS)

    # Remove any overlap (Faramir/Boromir are in Mordor section of splords but should be Gondor)
    for gid in gondor_ids:
        mordor_ids.discard(gid)

    print(f"  Gondor lord IDs ({len(gondor_ids)}): {sorted(gondor_ids)}")
    print(f"  Mordor lord IDs ({len(mordor_ids)}): {sorted(mordor_ids)}")

    # Step 2: Fix lords.xslt cultures
    print("\n=== Fixing lords.xslt cultures ===")
    lords_xslt_content = LORDS_XSLT.read_text(encoding="utf-8")
    new_xslt_content, xslt_changes = fix_lords_xslt_cultures(
        lords_xslt_content, gondor_ids, mordor_ids, args.dry_run
    )
    all_changes.extend(xslt_changes)
    for c in xslt_changes:
        print(f"  {c}")

    if args.apply and xslt_changes:
        LORDS_XSLT.write_text(new_xslt_content, encoding="utf-8")
        print(f"  -> Written {len(xslt_changes)} changes to lords.xslt")

    # Step 3: Fix lords.xml cultures
    print("\n=== Fixing lords.xml cultures ===")
    lords_xml_content = LORDS_XML.read_text(encoding="utf-8")
    new_xml_content, xml_changes = fix_lords_xml_cultures(
        lords_xml_content, gondor_ids, mordor_ids, args.dry_run
    )
    all_changes.extend(xml_changes)
    for c in xml_changes:
        print(f"  {c}")

    if args.apply and xml_changes:
        LORDS_XML.write_text(new_xml_content, encoding="utf-8")
        print(f"  -> Written {len(xml_changes)} changes to lords.xml")

    # Step 4: Add mounts to equipment sets
    print("\n=== Adding mounts to battle equipment templates ===")
    equipment_files = sorted(MODULE_DATA.glob("taom_equipment_sets_*.xml"))
    for eq_file in equipment_files:
        new_content, eq_changes = add_mounts_to_equipment(eq_file, args.dry_run)
        all_changes.extend(eq_changes)
        for c in eq_changes:
            print(f"  {c}")
        if args.apply and eq_changes:
            eq_file.write_text(new_content, encoding="utf-8")
            print(f"  -> Written {len(eq_changes)} changes to {eq_file.name}")

    # Summary
    print(f"\n=== Summary ===")
    print(f"Total changes: {len(all_changes)}")
    if args.dry_run:
        print("(dry-run mode - no files modified)")
    else:
        print("(changes applied)")


if __name__ == "__main__":
    main()
