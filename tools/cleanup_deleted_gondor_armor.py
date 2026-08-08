#!/usr/bin/env python3
"""Remove orphaned Gondor armor XML entries whose FBX source files were deleted.

Commit defb2642 in lotraom-assets ("Gondor Old Asset Cleanup") deleted 22 FBX
files. This script removes the corresponding <Item> entries from the armory XMLs.

Usage:
    python tools/cleanup_deleted_gondor_armor.py --dry-run
    python tools/cleanup_deleted_gondor_armor.py --apply
"""

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from _gamedir import ensure_exists, game_dir

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
# The E:/repos asset-repo entry is a different concept and keeps its own literal.
GAME = game_dir("E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
ARMORY_PATHS = [
    Path("E:/repos/lotraom-assets/shared/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor"),
    Path(GAME) / "Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor",
]

# Item IDs to remove, grouped by source XML file
ITEMS_TO_REMOVE = {
    "head_armors.xml": [
        # From fountain_and_citidel_helms.fbx (deleted)
        "citidel_guard_helmet1",
        "citidel_guard_helmet2",
        "citidel_guard_helmet3",
        "citidel_guard_helmet4",
        "citidel_guard_helmet5",
        "citidel_guard_helmet6",
        "citidel_guard_helmet7",
        "citidel_guard_helmet8",
        # From fountain_guard/ (deleted)
        "fountain_guard_helmet",
        "fountain_helm1",
        "fountain_helm2",
        "fountain_helm3",
        "fountain_helm4",
        "fountain_helm5",
        "fountain_helm6",
        "fountain_helm7",
        "fountain_helm8",
        "fountain_helm9",
        "fountain_helm10",
        "fountain_helm11",
        "fountain_helm12",
        "fountain_helm13",
        "fountain_helm14",
        "fountain_helm15",
        "fountain_helm16",
        "fountain_helm17",
        "fountain_helm18",
        "fountain_helm19",
        # From gondor_soldier_old/ (deleted)
        "gond_hlm_b_ld",
        "gond_hlm_ld",
        "gond_hlm_mask_b_ld",
        # Replaced by sk_gd_* items — no longer needed
        "gondor_helmet",
        "gondor_solider_helm",
        "gondor_solider_helm_gold",
        "gondor_nobke_helmet",
        "gondor_nobke_helmet1",
        "gondor_chainmail_helmeta",
        "ga_helmet021",
        "ga_helmet022",
    ],
    "body_armors.xml": [
        # From citidel_guard_armors.fbx (deleted)
        "citidel_guard_armor1",
        "citidel_guard_armor2",
        "citidel_guard_armor3",
        "citidel_guard_armor4",
        # From fountain_armor_armor.fbx (deleted)
        "fountain_armor1",
        "fountain_armor2",
        "fountain_armor3",
        # From gondor_king_armor.fbx (deleted)
        "gondor_king_armor",
        # From gondor_noble.fbx (deleted)
        "gondor_nobke_armor",
        "gondor_noble_coat_a",
        "gondor_noble_coat_b",
        "gondor_noble_jerkin_a",
        "gondor_noble_jerkin_b",
        # From gondor_soldier_old/ (deleted)
        "gond_tab_9ld",
        # Replaced by sk_gd_* items — no longer needed
        "gondor_chainmaila",
    ],
    "shoulder_armors.xml": [
        # From citidel_guard_shoulders.fbx (deleted)
        "citidel_guard_armor_pauldrons",
        "citidel_guard_armor_pauldrons_light",
        # From fountain_guard.fbx (deleted)
        "fountain_guard_pauldrons",
        "fountain_shoulders1",
        "fountain_shoulders2",
        "fountain_shoulders3",
        # From gondor_king_armor.fbx (deleted)
        "gondor_king_pauldrons",
        # From gondor_noble.fbx (deleted)
        "gondor_nobke_pauldrons",
        # From gondor_soldier_old/ (deleted) — id differs from mesh name
        "gondor_shldr_plt_ld",
        # Replaced by sk_gd_* items — no longer needed
        "gondor_pauldrons",
        "cts_gondor_pauldrons",
    ],
    "arm_armors.xml": [
        # From citidel_guard_gloves.fbx (deleted)
        "citidel_guard_bracers",
        "citidel_guard_bracers_shield",
        "citidel_guard_gloves",
        # From gondor_king_armor.fbx (deleted)
        "gondor_king_bracers",
        # From gondor_noble.fbx (deleted)
        "gondor_nobke_bracers",
        # Replaced by sk_gd_* items — no longer needed
        "boromir_gloves",
    ],
    "leg_armors.xml": [
        # From citidel_guard_boots.fbx (deleted)
        "citidel_guard_boots",
        "citidel_guard_boots_light",
        # From fountain_guard.fbx (deleted)
        "fountain_guard_boots",
        # From gondor_king_armor.fbx (deleted)
        "gondor_king_boots",
        # From gondor_noble.fbx (deleted)
        "gondor_nobke_boots",
        # From gondor_soldier_old/ (deleted)
        "gond_bts_lth_ld",
        "gond_bts_plt_b_ld",
        # Replaced by sk_gd_* items — no longer needed
        "gondor_boots",
    ],
}


def process_file(xml_file: Path, ids_to_remove: list[str], apply: bool) -> int:
    """Remove <Item> elements with matching id attributes. Returns count removed."""
    tree = ET.parse(xml_file)
    root = tree.getroot()

    ids_set = set(ids_to_remove)
    to_remove = []

    for item in root.findall("Item"):
        item_id = item.get("id", "")
        if item_id in ids_set:
            to_remove.append(item)

    if not to_remove:
        return 0

    found_ids = [item.get("id") for item in to_remove]
    missing = ids_set - set(found_ids)

    print(f"  {xml_file.name}: found {len(to_remove)} items to remove")
    for item_id in sorted(found_ids):
        print(f"    - {item_id}")

    if missing:
        print(f"  WARNING: {len(missing)} items NOT found in {xml_file.name}:")
        for item_id in sorted(missing):
            print(f"    ! {item_id}")

    if apply:
        for item in to_remove:
            root.remove(item)
        # Write back with XML declaration
        tree.write(xml_file, encoding="utf-8", xml_declaration=True)
        print(f"  -> Wrote {xml_file.name}")

    return len(to_remove)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--dry-run", action="store_true", help="List items to remove")
    group.add_argument("--apply", action="store_true", help="Remove items from XMLs")
    args = parser.parse_args()

    # With neither location present the loop prints two SKIP lines and a
    # "Grand total: 0", then exits 0 — indistinguishable from a clean run.
    if not any(p.exists() for p in ARMORY_PATHS):
        ensure_exists(ARMORY_PATHS[-1], what="the Gondor item folder")

    grand_total = 0
    for armory_base in ARMORY_PATHS:
        if not armory_base.exists():
            print(f"SKIP: {armory_base} not found")
            continue
        print(f"\n=== {armory_base} ===")
        total = 0
        for xml_name, ids in ITEMS_TO_REMOVE.items():
            xml_path = armory_base / xml_name
            if not xml_path.exists():
                print(f"WARNING: {xml_path} not found, skipping")
                continue
            count = process_file(xml_path, ids, apply=args.apply)
            total += count
        print(f"  Subtotal: {total}")
        grand_total += total

    expected = sum(len(ids) for ids in ITEMS_TO_REMOVE.values())
    print(f"\nGrand total: {grand_total} items {'removed' if args.apply else 'to remove'} "
          f"(expected up to {expected} per location)")


if __name__ == "__main__":
    main()
