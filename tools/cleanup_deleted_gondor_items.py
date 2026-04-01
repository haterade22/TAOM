"""
One-shot script to remove deleted Gondor armor item definitions from LOTRLOME_Armory.
Removes <Item> blocks by ID using regex to preserve whitespace/formatting exactly.
"""
import re
import sys

import os
ARMORY_BASE = os.environ.get(
    'TAOM_ARMORY_BASE',
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\gondor"
)

DELETIONS = {
    "head_armors.xml": [
        "dol_amroth_helmet_basic",
        "dol_amroth_helmet_basic_mask",
        "dol_amroth_helmet_elite",
        "dol_amroth_helmet_elite_mask",
        "dol_amroth_helmet_swan",
        "dol_amroth_helmet_swan_mask",
        "gondor_generic_helmet_5_a",
        "gondor_generic_helmet_5_a_coif",
        "gondor_generic_helmet_5_b",
        "gondor_generic_helmet_5_b_coif",
        "gondor_lamedon_helmet_4",
        "gondor_lamedon_helmet_4_b",
        "gondor_lamedon_helmet_4_c",
        "pg_helmet_4",
        "pg_helmet_4_coif",
        "pelargirmarine_helmet_1_a",
        "pelargirmarine_helmet_1_a_plume",
        "pelargirmarine_helmet_1_a_coif",
        "pelargirmarine_helmet_1_a_coif_plume",
        "pelargirmarine_helmet_1_b",
        "pelargirmarine_helmet_1_b_plume",
        "pelargirmarine_helmet_1_b_coif",
        "pelargirmarine_helmet_1_b_coif_plume",
        "pelargirmarine_helmet_6_a",
        "pelargirmarine_helmet_6_a_plume",
        "pelargirmarine_helmet_6_a_coif",
        "pelargirmarine_helmet_6_a_coif_plume",
        "pelargirmarine_helmet_6_b",
        "pelargirmarine_helmet_6_b_plume",
        "pelargirmarine_helmet_6_b_coif",
        "pelargirmarine_helmet_6_b_coif_plume",
        "swanknight_helmet_a",
        "swanknight_helmet_b",
        "swanknight_helmet_c",
        "swan_knight_helmet_winged",
        "swan_knight_helmet_winged_mask",
        "sk_gondor_lossarnach_helmet_a",
        "lossarnach_helm",
    ],
    "body_armors.xml": [
        "dol_amroth_armor_a2",
        "dol_amroth_armor_a3",
        "dol_amroth_armor_b1",
        "dol_amroth_armor_b2",
        "dol_amroth_armor_b3",
        "cts_gondor_armor1",
        "cts_gondor_armor2",
        "cts_gondor_armor3",
        "lossarnach_armor_heavy",
        "lossarnach_light",
        "lossarnach_medium_armor",
        "sk_gondor_lossarnach_chest_a",
        "pg_spearman_armor_01",
        "pg_spearman_armor_02",
        "pg_spearman_armor_03",
        "rgva_chest_ver1",
        "rgva_chest_ver2",
        "rgva_chest_ver3",
        "lrd_gondor_marines_1_armour",
        "lrd_marines_gondor_2_armour",
        "lrd_gondor_marines_3_armour",
        "swanknight_cuirass_a",
        "swanknight_cuirass_c",
        "swanknight_cuirass_e",
        "swan_knight_armor_1a_t1",
        "swan_knight_armor_1a_t2",
        "swan_knight_armor_1a_t3",
        "swan_knight_armor_1b_t1",
        "swan_knight_armor_1b_t2",
        "swan_knight_armor_1b_t3",
        "swan_knight_armor_2a_t1",
        "swan_knight_armor_2a_t2",
        "swan_knight_armor_2a_t3",
        "swan_knight_armor_2b_t1",
        "swan_knight_armor_2b_t2",
        "swan_knight_armor_2b_t3",
        "swan_knight_armor_tabard1_t1",
        "swan_knight_armor_tabard1_t2",
        "swan_knight_armor_tabard1_t3",
        "swan_knight_armor_tabard2_t1",
        "swan_knight_armor_tabard2_t2",
        "swan_knight_armor_tabard2_t3",
        "sk_gd_osg_inf_chest_elite_a",  # low-armor duplicate "Boromir's Armour"
    ],
    "shoulder_armors.xml": [
        "lossarnach_pauldrons",
        "boromir_pauldrons",
        "swanknight_shoulders_a",
        "swanknight_shoulders_b",
        "swan_knight_pauldrons_a1",
        "swan_knight_pauldrons_a2",
        "swan_knight_pauldrons_a3",
        "swan_knight_pauldrons_b1",
        "swan_knight_pauldrons_b2",
        "swan_knight_pauldrons_c1",
        "swan_knight_pauldrons_c2",
    ],
    "leg_armors.xml": [
        "boromir_boots",
        "swanknight_boots_a",
        "swanknight_boots_b",
        "sk_gondor_lossarnach_boots_a",
        "swan_knight_basic_boots",
        "swan_knight_greaves_a",
        "swan_knight_greaves_a_full",
        "swan_knight_greaves_b_full",
        "swan_knight_greaves_a_sabatons",
        "swan_knight_greaves_b_sabatons",
        "cts_gondor_boot",
        "lrd_marines_gondor_1_boots",
        "pg_spearman_boots",
    ],
    "arm_armors.xml": [
        "swanknight_gloves",
        "swan_bracer_a",
        "swan_bracer_b",
        "swan_bracer_reinforced_a",
        "swan_bracer_reinforced_b",
        "swan_gauntlet_a",
        "swan_gauntlet_b",
        "lrd_marines_gondor_1_gloves",
        "lrd_marines_gondor_2_gloves",
        "lrd_marines_gondor_3_gloves",
        "pg_spearman_glove",
    ],
}


def remove_item_blocks(content: str, ids_to_remove: list[str]) -> tuple[str, list[str], list[str]]:
    """
    Remove <Item id="X" ...> ... </Item> blocks for each given ID.
    Handles both self-closing <Item .../> and multi-line <Item ...>...</Item>.
    Returns (new_content, removed_ids, not_found_ids).
    """
    removed = []
    not_found = []

    for item_id in ids_to_remove:
        # Match the opening tag with this exact id (word boundary after id value)
        # Then consume until </Item> (multi-line) or self-close />
        # Also consume any leading whitespace/newline before the block
        pattern = re.compile(
            r'\n?[ \t]*<Item\s+id="' + re.escape(item_id) + r'"[^>]*(?:/>|>.*?</Item>)',
            re.DOTALL
        )
        new_content, count = pattern.subn("", content)
        if count > 0:
            removed.append(item_id)
            content = new_content
        else:
            not_found.append(item_id)

    return content, removed, not_found


def process_file(filename: str, ids: list[str], dry_run: bool = False) -> None:
    import os
    path = os.path.join(ARMORY_BASE, filename)
    with open(path, "r", encoding="utf-8") as f:
        original = f.read()

    new_content, removed, not_found = remove_item_blocks(original, ids)

    print(f"\n=== {filename} ===")
    print(f"  Removed ({len(removed)}): {', '.join(removed) if removed else 'none'}")
    if not_found:
        print(f"  Not found ({len(not_found)}): {', '.join(not_found)}")

    if not dry_run and removed:
        with open(path, "w", encoding="utf-8") as f:
            f.write(new_content)
        print(f"  Written back to disk.")
    elif dry_run:
        print(f"  [DRY RUN - not written]")


def main():
    dry_run = "--dry-run" in sys.argv
    if dry_run:
        print("DRY RUN MODE — no files will be modified\n")
    else:
        print("LIVE MODE — files will be modified\n")

    for filename, ids in DELETIONS.items():
        process_file(filename, ids, dry_run=dry_run)

    print("\nDone.")


if __name__ == "__main__":
    main()
