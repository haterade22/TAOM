#!/usr/bin/env python3
"""Author Dol Guldur paint helmets per KEYforce spec.

Source-of-truth: E:\\repos\\lotraom-assets\\tools\\dol_guldur_armors_and_troops.txt
Issue: KEYforce mesh-first revamp (Dol Guldur pass)

Adds `sk_dg_orc_*` paint helmets (Dol-Guldur variants of generic orc shapes).
The 113 sk_dg_uruk_* Uruk items already exist; sk_dg_khml_* Khamul items live
under rhun/ already. Only the missing paint helmets are authored here.

Usage:
    python tools/generate_dolguldur_armor.py --dry-run
    python tools/generate_dolguldur_armor.py --apply
"""
import argparse
import os
import sys
from dataclasses import dataclass

DEFAULT_ARMORY_BASE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\dol_guldur"
)

STAT_TIERS = {
    "head": {
        "light":  {"head_armor": 15, "weight": 1.5},
        "medium": {"head_armor": 24, "weight": 2.5},
        "heavy":  {"head_armor": 32, "weight": 3.5},
        "elite":  {"head_armor": 40, "weight": 4.5},
    },
}

APPEARANCE = {"light": 1, "medium": 3, "heavy": 4, "elite": 6}


@dataclass
class ArmorItem:
    id: str
    display_name: str
    slot: str
    tier: str
    material: str
    modifier_group: str = ""

    def __post_init__(self):
        if not self.modifier_group:
            self.modifier_group = {
                "Plate": "plate", "Leather": "leather",
            }.get(self.material, "plate")


# DG-paint helmets (per spec: GN + DG pool; DG variants exist for Arc/Inf/Mrd/Pik/Rdr/Sct/Sly shapes;
# avoid Brt (no DG variant) + Vgd (no DG variant))
HEAD_ARMORS = [
    ArmorItem("sk_dg_orc_arc_helmet_med_a", "Dol Guldur Orc Archer Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_dg_orc_arc_helmet_heavy_a", "Dol Guldur Orc Archer Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_dg_orc_inf_helmet_med_a", "Dol Guldur Orc Infantry Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_dg_orc_inf_helmet_heavy_a", "Dol Guldur Orc Infantry Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_dg_orc_mrd_helmet_light_a", "Dol Guldur Orc Moria Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_dg_orc_mrd_helmet_med_a", "Dol Guldur Orc Moria Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_dg_orc_pik_helmet_med_a", "Dol Guldur Orc Pike Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_dg_orc_pik_helmet_heavy_a", "Dol Guldur Orc Pike Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_dg_orc_rdr_helmet_light_a", "Dol Guldur Orc Rider Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_dg_orc_rdr_helmet_med_a", "Dol Guldur Orc Rider Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_dg_orc_sct_helmet_med_a", "Dol Guldur Orc Scout Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_dg_orc_sct_helmet_heavy_a", "Dol Guldur Orc Scout Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_dg_orc_sly_helmet_med_a", "Dol Guldur Orc Sallet Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_dg_orc_sly_helmet_heavy_a", "Dol Guldur Orc Sallet Heavy Helmet", "head", "heavy", "Plate"),
]

SLOT_MAP = {
    "head": (HEAD_ARMORS, "head_armors.xml"),
}

CULTURE = "dolguldur"
ITEM_NAME_PREFIX = "Dol Guldur"


def generate_item_xml(item: ArmorItem) -> str:
    stats = STAT_TIERS[item.slot][item.tier]
    weight = stats["weight"]
    appearance = APPEARANCE[item.tier]

    armor_attrs = [
        f'head_armor="{stats["head_armor"]}"',
        'has_gender_variations="false"',
        'hair_cover_type="type2"',
        f'modifier_group="{item.modifier_group}"',
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
    total = 0
    for slot_name, (items, filename) in SLOT_MAP.items():
        print(f"\n=== {filename} ({len(items)} items) ===")
        for item in items:
            print(f"  {item.id:50s} [{item.tier:6s}]")
        total += len(items)
    print(f"\nTotal: {total} items")


def apply(armory_base: str):
    print(f"Target: {armory_base}\n")
    grand_added = 0
    grand_skipped = 0
    for slot_name, (items, filename) in SLOT_MAP.items():
        filepath = os.path.join(armory_base, filename)
        if not os.path.exists(filepath):
            print(f"ERROR: {filepath} not found", file=sys.stderr)
            continue

        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()

        existing_ids = {item.id for item in items if f'id="{item.id}"' in content}
        new_items = [i for i in items if i.id not in existing_ids]
        grand_skipped += len(existing_ids)

        if not new_items:
            print(f"  {filename}: all {len(items)} items already exist, skipping")
            continue
        if existing_ids:
            print(f"  {filename}: {len(existing_ids)} already exist, adding {len(new_items)} new")
        else:
            print(f"  {filename}: adding {len(new_items)} new items")

        new_xml = "\n\n".join(generate_item_xml(item) for item in new_items)
        closing_tag = "</Items>"
        if closing_tag not in content:
            print(f"ERROR: {closing_tag} not found in {filepath}", file=sys.stderr)
            continue

        section_comment = (
            "\n    <!-- ============================================================== -->\n"
            "    <!--  KEYforce Dol Guldur paint helmets (sk_dg_orc_*)                -->\n"
            "    <!-- ============================================================== -->\n\n"
        )
        content = content.replace(closing_tag, f"{section_comment}{new_xml}\n\n{closing_tag}")

        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)

        print(f"    -> wrote {len(new_items)} items to {filepath}")
        grand_added += len(new_items)

    print(f"\nDone. Added {grand_added} new items, skipped {grand_skipped} already present.")


def main():
    parser = argparse.ArgumentParser(description="Dol Guldur paint helmet generator")
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
