#!/usr/bin/env python3
"""Author missing Rhun (Loke-Rim) elite helmets per KEYforce spec.

Source-of-truth: E:\\repos\\lotraom-assets\\tools\\rhun_armors_and_troops.txt
Issue: KEYforce mesh-first revamp (Rhun pass)

The vast majority of Rhun + Khamul items are already authored (564 of 586).
Only 22 Loke-Rim elite helmet variants + 1 hood are missing.

Usage:
    python tools/generate_rhun_armor.py --dry-run
    python tools/generate_rhun_armor.py --apply
"""
import argparse
import os
import sys
from dataclasses import dataclass

DEFAULT_ARMORY_BASE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\rhun"
)

STAT_TIERS = {
    "head": {
        "heavy":  {"head_armor": 32, "weight": 3.5},
        "elite":  {"head_armor": 40, "weight": 4.5},
    },
}

APPEARANCE = {"heavy": 4, "elite": 6}


@dataclass
class ArmorItem:
    id: str
    display_name: str
    tier: str
    material: str = "Plate"


# 22 missing Loke-Rim items (all elite helmets + 1 heavy hood)
ITEMS = [
    ArmorItem("sk_rh_loke_helmet_arch_elite_e", "Loke-Rim Archer Elite Helmet E", "elite"),
    ArmorItem("sk_rh_loke_helmet_arch_elite_f", "Loke-Rim Archer Elite Helmet F", "elite"),
    ArmorItem("sk_rh_loke_helmet_arch_elite_g", "Loke-Rim Archer Elite Helmet G", "elite"),
    ArmorItem("sk_rh_loke_helmet_arch_elite_h", "Loke-Rim Archer Elite Helmet H", "elite"),
    ArmorItem("sk_rh_loke_helmet_arch_elite_i", "Loke-Rim Archer Elite Helmet I", "elite"),
    ArmorItem("sk_rh_loke_helmet_cav_elite_d", "Loke-Rim Cavalry Elite Helmet D", "elite"),
    ArmorItem("sk_rh_loke_helmet_cav_elite_e", "Loke-Rim Cavalry Elite Helmet E", "elite"),
    ArmorItem("sk_rh_loke_helmet_cav_elite_f", "Loke-Rim Cavalry Elite Helmet F", "elite"),
    ArmorItem("sk_rh_loke_helmet_cult_elite_d", "Loke-Rim Cultist Elite Helmet D", "elite"),
    ArmorItem("sk_rh_loke_helmet_cult_elite_e", "Loke-Rim Cultist Elite Helmet E", "elite"),
    ArmorItem("sk_rh_loke_helmet_cult_elite_f", "Loke-Rim Cultist Elite Helmet F", "elite"),
    ArmorItem("sk_rh_loke_helmet_cult_elite_g", "Loke-Rim Cultist Elite Helmet G", "elite"),
    ArmorItem("sk_rh_loke_helmet_cult_elite_h", "Loke-Rim Cultist Elite Helmet H", "elite"),
    ArmorItem("sk_rh_loke_helmet_cult_elite_i", "Loke-Rim Cultist Elite Helmet I", "elite"),
    ArmorItem("sk_rh_loke_helmet_east_elite_b", "Loke-Rim Eastern Elite Helmet B", "elite"),
    ArmorItem("sk_rh_loke_helmet_east_elite_c", "Loke-Rim Eastern Elite Helmet C", "elite"),
    ArmorItem("sk_rh_loke_helmet_inf_elite_e", "Loke-Rim Infantry Elite Helmet E", "elite"),
    ArmorItem("sk_rh_loke_helmet_inf_elite_f", "Loke-Rim Infantry Elite Helmet F", "elite"),
    ArmorItem("sk_rh_loke_helmet_inf_elite_g", "Loke-Rim Infantry Elite Helmet G", "elite"),
    ArmorItem("sk_rh_loke_helmet_inf_elite_h", "Loke-Rim Infantry Elite Helmet H", "elite"),
    ArmorItem("sk_rh_loke_helmet_inf_elite_i", "Loke-Rim Infantry Elite Helmet I", "elite"),
    ArmorItem("sk_rh_loke_hood_heavy_d", "Loke-Rim Heavy Hood D", "heavy", "Cloth"),
]

CULTURE = "rhun"
ITEM_NAME_PREFIX = "Rhun"


def generate_item_xml(item: ArmorItem) -> str:
    stats = STAT_TIERS["head"][item.tier]
    weight = stats["weight"]
    appearance = APPEARANCE[item.tier]
    modifier_group = {"Plate": "plate", "Cloth": "cloth"}.get(item.material, "plate")

    armor_attrs = [
        f'head_armor="{stats["head_armor"]}"',
        'has_gender_variations="false"',
        'hair_cover_type="type2"',
        f'modifier_group="{modifier_group}"',
        f'material_type="{item.material}"',
        'beard_cover_type="all"',
    ]

    return (
        f'    <Item\n'
        f'        id="{item.id}"\n'
        f'        name="{{=aom_{item.id}_name}}[{ITEM_NAME_PREFIX}] {item.display_name}"\n'
        f'        subtype="head_armor"\n'
        f'        mesh="{item.id}"\n'
        f'        culture="Culture.{CULTURE}"\n'
        f'        is_merchandise="true"\n'
        f'        weight="{weight}"\n'
        f'        difficulty="0"\n'
        f'        appearance="{appearance}"\n'
        f'        Type="HeadArmor">\n'
        f'        <ItemComponent>\n'
        f'            <Armor {" ".join(armor_attrs)} />\n'
        f'        </ItemComponent>\n'
        f'        <Flags UseTeamColor="true" />\n'
        f'    </Item>'
    )


def dry_run():
    print(f"\n=== head_armors.xml ({len(ITEMS)} items) ===")
    for item in ITEMS:
        print(f"  {item.id:55s} [{item.tier:6s}]")
    print(f"\nTotal: {len(ITEMS)} items")


def apply(armory_base: str):
    filename = "head_armors.xml"
    filepath = os.path.join(armory_base, filename)
    if not os.path.exists(filepath):
        print(f"ERROR: {filepath} not found", file=sys.stderr)
        sys.exit(1)

    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    existing_ids = {item.id for item in ITEMS if f'id="{item.id}"' in content}
    new_items = [i for i in ITEMS if i.id not in existing_ids]

    if not new_items:
        print(f"  {filename}: all {len(ITEMS)} items already exist, skipping")
        return

    if existing_ids:
        print(f"  {filename}: {len(existing_ids)} already exist, adding {len(new_items)} new")
    else:
        print(f"  {filename}: adding {len(new_items)} new items")

    new_xml = "\n\n".join(generate_item_xml(item) for item in new_items)
    closing_tag = "</Items>"
    section_comment = (
        "\n    <!-- ============================================================== -->\n"
        "    <!--  KEYforce Rhun (Loke-Rim elite helmets)                         -->\n"
        "    <!-- ============================================================== -->\n\n"
    )
    content = content.replace(closing_tag, f"{section_comment}{new_xml}\n\n{closing_tag}")

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"    -> wrote {len(new_items)} items to {filepath}")


def main():
    parser = argparse.ArgumentParser(description="Rhun missing Loke-Rim helmet generator")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--armory-path", default=DEFAULT_ARMORY_BASE)
    args = parser.parse_args()

    if args.dry_run:
        dry_run()
    elif args.apply:
        apply(args.armory_path)
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
