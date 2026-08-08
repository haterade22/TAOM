#!/usr/bin/env python3
"""P0 fix: rollback erebor-folder sk_dwarf_iron_* items written to wrong folder.

The Erebor generator (`generate_erebor_armor.py`) wrote 123 Iron Hills items to
`LOTRLOME_items/erebor/` but ~118 of those IDs already exist in `iron_hills/`.
Both folders load at runtime — duplicate IDs cause silent shadowing.

This script removes the KEYforce Iron Hills sections from erebor/*.xml. The
canonical home for sk_dwarf_iron_* items is iron_hills/.

Usage:
    python tools/rollback_erebor_iron_misfile.py --dry-run
    python tools/rollback_erebor_iron_misfile.py --apply
"""
import argparse
import os
import re
import sys

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = os.environ.get("BANNERLORD_GAME_DIR") or r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
EREBOR_DIR = GAME + r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\erebor"

# Section header inserted by generate_erebor_armor.py
SECTION_RE = re.compile(
    r'\n    <!-- ={4,} -->\n'
    r'    <!--  KEYforce Iron Hills armor \(sk_dwarf_iron_\*\)\s*-->\n'
    r'    <!-- ={4,} -->\n\n',
    re.DOTALL
)

# Each <Item> block we added
ITEM_RE = re.compile(
    r'    <Item\n        id="sk_dwarf_iron_[^"]+"[^<]*<ItemComponent>.*?</Item>(?:\n\n)?',
    re.DOTALL
)


def rollback(dry_run: bool):
    files = ["head_armors.xml", "body_armors.xml", "shoulder_armors.xml",
             "arm_armors.xml", "leg_armors.xml"]
    grand_removed = 0
    for filename in files:
        path = os.path.join(EREBOR_DIR, filename)
        if not os.path.exists(path):
            print(f"  SKIP: {filename} not found")
            continue
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()
        # Count items before
        before = len(re.findall(r'id="sk_dwarf_iron_', content))
        if before == 0:
            print(f"  {filename}: no sk_dwarf_iron_* items, skip")
            continue
        # Remove the section header
        new_content = SECTION_RE.sub('\n', content)
        # Remove every sk_dwarf_iron_* <Item> block
        new_content = ITEM_RE.sub('', new_content)
        after = len(re.findall(r'id="sk_dwarf_iron_', new_content))
        removed = before - after
        grand_removed += removed
        print(f"  {filename}: {before} sk_dwarf_iron_* -> {after} (removed {removed})")
        if not dry_run:
            with open(path, "w", encoding="utf-8") as f:
                f.write(new_content)
    print(f"\nTotal removed: {grand_removed}")
    if dry_run:
        print("(dry-run — no files written)")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    if args.dry_run:
        rollback(dry_run=True)
    elif args.apply:
        rollback(dry_run=False)
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
