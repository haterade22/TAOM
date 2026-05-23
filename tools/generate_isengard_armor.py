#!/usr/bin/env python3
"""Author Isengard paint helmets + scout cloth variants per KEYforce spec.

Source-of-truth: E:\\repos\\lotraom-assets\\tools\\isengard_armors_and_troops.txt
Issue: KEYforce mesh-first revamp (Isengard pass)

Adds `sk_is_orc_*` paint helmets (Isengard variants of generic orc shapes —
spec excludes Pik/Rdr/Sct, no IS variants exist for those) and the missing
`clo_urukscout_*` cloth-overlay variants. The 137 sk_uruk_hai_* Legion items
and the 4 urukscout base pieces already exist — not touched.

Usage:
    python tools/generate_isengard_armor.py --dry-run
    python tools/generate_isengard_armor.py --apply
"""
import argparse
import os
import sys
from dataclasses import dataclass
from typing import Optional

DEFAULT_ARMORY_BASE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\isengard"
)

STAT_TIERS = {
    "head": {
        "light":  {"head_armor": 15, "weight": 1.5},
        "medium": {"head_armor": 24, "weight": 2.5},
        "heavy":  {"head_armor": 32, "weight": 3.5},
        "elite":  {"head_armor": 40, "weight": 4.5},
    },
    "body": {
        "light":  {"body_armor": 20, "leg_armor": 10, "weight": 8.0},
        "medium": {"body_armor": 32, "leg_armor": 16, "weight": 13.0},
        "heavy":  {"body_armor": 42, "leg_armor": 22, "weight": 18.0},
        "elite":  {"body_armor": 50, "leg_armor": 28, "weight": 22.0},
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
    hair_cover: str = "type2"
    beard_cover: str = "all"
    covers_body: bool = False
    arm_armor_stat: Optional[int] = None

    def __post_init__(self):
        if not self.modifier_group:
            self.modifier_group = {
                "Plate": "plate", "Chainmail": "chain",
                "Leather": "leather", "Cloth": "cloth",
            }.get(self.material, "plate")


# Isengard-paint helmets (spec: GN + IS pool; IS variants exist for Arc/Brt/Inf/Mrd/Sly/Vgd shapes)
HEAD_ARMORS = [
    ArmorItem("sk_is_orc_arc_helmet_med_a", "Isengard Orc Archer Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_is_orc_arc_helmet_heavy_a", "Isengard Orc Archer Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_is_orc_brt_helmet_light_a", "Isengard Orc Brute Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_is_orc_brt_helmet_med_a", "Isengard Orc Brute Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_is_orc_brt_helmet_heavy_a", "Isengard Orc Brute Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_is_orc_inf_helmet_med_a", "Isengard Orc Infantry Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_is_orc_inf_helmet_heavy_a", "Isengard Orc Infantry Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_is_orc_mrd_helmet_light_a", "Isengard Orc Moria Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_is_orc_mrd_helmet_med_a", "Isengard Orc Moria Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_is_orc_sly_helmet_med_a", "Isengard Orc Sallet Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_is_orc_sly_helmet_heavy_a", "Isengard Orc Sallet Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_is_orc_vgd_helmet_med_a", "Isengard Orc Vanguard Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_is_orc_vgd_helmet_heavy_a", "Isengard Orc Vanguard Heavy Helmet", "head", "heavy", "Plate"),
]

# Uruk-Hai Scout cloth overlay variants
BODY_ARMORS = [
    ArmorItem("clo_urukscout_body", "Uruk-Hai Scout Cloth Overlay", "body", "light", "Cloth", covers_body=True),
]

HEAD_ARMORS_BODY = [  # Cloth helmet overlay goes in head_armors but uses cloth material
    ArmorItem("clo_urukscout_helmet", "Uruk-Hai Scout Hood", "head", "light", "Cloth"),
]

HEAD_ARMORS = HEAD_ARMORS + HEAD_ARMORS_BODY


SLOT_MAP = {
    "head":     (HEAD_ARMORS, "head_armors.xml"),
    "body":     (BODY_ARMORS, "body_armors.xml"),
}

SLOT_TYPES = {
    "head": ("HeadArmor", "head_armor"),
    "body": ("BodyArmor", "body_armor"),
}

CULTURE = "isengard"
ITEM_NAME_PREFIX = "Isengard"


def generate_item_xml(item: ArmorItem) -> str:
    slot_type, subtype = SLOT_TYPES[item.slot]
    stats = STAT_TIERS[item.slot][item.tier]
    weight = stats["weight"]
    appearance = APPEARANCE[item.tier]

    armor_attrs = []
    if item.slot == "head":
        armor_attrs += [
            f'head_armor="{stats["head_armor"]}"',
            'has_gender_variations="false"',
            f'hair_cover_type="{item.hair_cover}"',
            f'modifier_group="{item.modifier_group}"',
            f'material_type="{item.material}"',
            f'beard_cover_type="{item.beard_cover}"',
        ]
    elif item.slot == "body":
        armor_attrs.append(f'body_armor="{stats["body_armor"]}"')
        if item.arm_armor_stat is not None:
            armor_attrs.append(f'arm_armor="{item.arm_armor_stat}"')
        armor_attrs.append('has_gender_variations="false"')
        if item.covers_body:
            armor_attrs.append('covers_body="true"')
        armor_attrs.append(f'modifier_group="{item.modifier_group}"')
        armor_attrs.append(f'material_type="{item.material}"')

    armor_str = " ".join(armor_attrs)

    return (
        f'    <Item\n'
        f'        id="{item.id}"\n'
        f'        name="{{=aom_{item.id}_name}}[{ITEM_NAME_PREFIX}] {item.display_name}"\n'
        f'        subtype="{subtype}"\n'
        f'        mesh="{item.id}"\n'
        f'        culture="Culture.{CULTURE}"\n'
        f'        is_merchandise="true"\n'
        f'        weight="{weight}"\n'
        f'        difficulty="0"\n'
        f'        appearance="{appearance}"\n'
        f'        Type="{slot_type}">\n'
        f'        <ItemComponent>\n'
        f'            <Armor {armor_str} />\n'
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
            "    <!--  KEYforce Isengard paint helmets (sk_is_orc_*) + scout cloth   -->\n"
            "    <!-- ============================================================== -->\n\n"
        )
        content = content.replace(closing_tag, f"{section_comment}{new_xml}\n\n{closing_tag}")

        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)

        print(f"    -> wrote {len(new_items)} items to {filepath}")
        grand_added += len(new_items)

    print(f"\nDone. Added {grand_added} new items, skipped {grand_skipped} already present.")


def main():
    parser = argparse.ArgumentParser(description="Isengard armor generator (paint helmets + scout cloth)")
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
