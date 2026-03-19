#!/usr/bin/env python3
"""Generate new Gondor armor item XML definitions and append to LOTRLOME_Armory.

Usage:
    python tools/generate_gondor_armor.py --dry-run   # list items only
    python tools/generate_gondor_armor.py --apply      # append to XML files
"""
import argparse
import os
import sys
from dataclasses import dataclass
from typing import Optional

ARMORY_BASE = os.path.join(
    "E:", os.sep, "repos", "lotraom-assets", "shared", "LOTRLOME_Armory",
    "ModuleData", "LOTRLOME_items", "gondor"
)

# =============================================================================
# STAT BASELINES (from rebalance_armor.py)
# =============================================================================
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
    "shoulder": {
        "light":  {"body_armor": 5,  "arm_armor": 5,  "weight": 3.0},
        "medium": {"body_armor": 8,  "arm_armor": 8,  "weight": 5.0},
        "heavy":  {"body_armor": 12, "arm_armor": 10, "weight": 7.0},
        "elite":  {"body_armor": 15, "arm_armor": 12, "weight": 9.0},
    },
    "arm": {
        "light":  {"arm_armor": 8,  "weight": 0.6},
        "medium": {"arm_armor": 14, "weight": 1.0},
        "heavy":  {"arm_armor": 20, "weight": 1.5},
        "elite":  {"arm_armor": 26, "weight": 2.0},
    },
    "leg": {
        "light":  {"leg_armor": 12, "weight": 1.5},
        "medium": {"leg_armor": 20, "weight": 2.5},
        "heavy":  {"leg_armor": 28, "weight": 3.5},
        "elite":  {"leg_armor": 34, "weight": 4.0},
    },
}

# Appearance values by tier
APPEARANCE = {"light": 1, "medium": 3, "heavy": 4, "elite": 6}


@dataclass
class ArmorItem:
    id: str
    display_name: str
    slot: str          # head, body, shoulder, arm, leg
    tier: str          # light, medium, heavy, elite
    material: str      # Plate, Chainmail, Leather, Cloth
    modifier_group: str = ""  # plate, chain, leather, cloth
    hair_cover: str = "type2"       # helmets only
    beard_cover: str = "all"        # helmets only
    covers_body: bool = False       # body armor
    covers_hands: bool = False      # arm armor
    covers_legs: bool = False       # leg armor
    arm_armor_stat: Optional[int] = None  # override for body armors with arm coverage

    def __post_init__(self):
        if not self.modifier_group:
            self.modifier_group = {
                "Plate": "plate", "Chainmail": "chain",
                "Leather": "leather", "Cloth": "cloth"
            }.get(self.material, "plate")


# =============================================================================
# ITEM DEFINITIONS — ALL 93 ITEMS
# =============================================================================

HEAD_ARMORS = [
    # Anorien Infantry
    ArmorItem("sk_gd_ano_inf_helmet_med_a", "Anorien Infantry Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_gd_ano_inf_helmet_heavy_a", "Anorien Infantry Heavy Helmet", "head", "heavy", "Plate"),
    # Anorien Cavalry
    ArmorItem("sk_gd_ano_cav_helmet_heavy_a", "Anorien Cavalry Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_ano_cav_helmet_heavy_b", "Anorien Cavalry Helmet B", "head", "heavy", "Plate"),
    # Anorien Noble
    ArmorItem("sk_gd_ano_noble_helmet_med_a", "Anorien Noble Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_gd_ano_noble_helmet_heavy_a", "Anorien Noble Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_ano_noble_helmet_heavy_b", "Anorien Noble Heavy Helmet B", "head", "heavy", "Plate"),
    # Minas Tirith Noble
    ArmorItem("sk_gd_mns_noble_helmet_med_a", "Minas Tirith Noble Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_gd_mns_noble_helmet_heavy_a", "Minas Tirith Noble Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_mns_noble_helmet_heavy_b", "Minas Tirith Noble Heavy Helmet B", "head", "heavy", "Plate"),
    # Citadel Guard
    ArmorItem("sk_gd_mns_cita_helmet_heavy_a", "Citadel Guard Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_mns_cita_helmet_heavy_b", "Citadel Guard Helmet B", "head", "heavy", "Plate"),
    # Fountain Guard
    ArmorItem("sk_gd_mns_fount_helmet_heavy_a", "Fountain Guard Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_mns_fount_helmet_heavy_b", "Fountain Guard Helmet B", "head", "heavy", "Plate"),
    # Osgiliath Noble
    ArmorItem("sk_gd_osg_noble_helmet_heavy_a", "Osgiliath Noble Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_osg_noble_helmet_heavy_b", "Osgiliath Noble Helmet B", "head", "heavy", "Plate"),
    # Osgiliath Warden
    ArmorItem("sk_gd_osg_ward_helmet_heavy_a", "Osgiliath Warden Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_osg_ward_helmet_heavy_b", "Osgiliath Warden Helmet B", "head", "heavy", "Plate"),
    # Cair Andros Noble
    ArmorItem("sk_gd_cair_noble_helmet_heavy_a", "Cair Andros Noble Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_cair_noble_helmet_heavy_b", "Cair Andros Noble Helmet B", "head", "heavy", "Plate"),
    # Cair Andros Warden
    ArmorItem("sk_gd_cair_ward_helmet_heavy_a", "Cair Andros Warden Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_cair_ward_helmet_heavy_b", "Cair Andros Warden Helmet B", "head", "heavy", "Plate"),
    # Ithil Guard
    ArmorItem("sk_gd_ith_noble_helmet_heavy_a", "Ithil Guard Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_ith_noble_helmet_heavy_b", "Ithil Guard Helmet B", "head", "heavy", "Plate"),
]

BODY_ARMORS = [
    # Anorien chainmail variants
    ArmorItem("sk_gd_ano_chainmail_half_a", "Anorien Half Chainmail A", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_ano_chainmail_half_b", "Anorien Half Chainmail B", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_ano_chainmail_full_a", "Anorien Full Chainmail A", "body", "medium", "Chainmail", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_ano_chainmail_full_b", "Anorien Full Chainmail B", "body", "medium", "Chainmail", covers_body=True, arm_armor_stat=10),
    # Anorien Infantry chest
    ArmorItem("sk_gd_ano_inf_chest_med_a", "Anorien Infantry Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_ano_inf_chest_med_b", "Anorien Infantry Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_ano_inf_chest_heavy_a", "Anorien Infantry Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_ano_inf_chest_heavy_b", "Anorien Infantry Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    # Citadel Guard chest
    ArmorItem("sk_gd_mns_citadel_chest_med_a", "Citadel Guard Armour", "body", "medium", "Plate", covers_body=True, arm_armor_stat=12),
    ArmorItem("sk_gd_mns_citadel_chest_heavy_a", "Citadel Guard Heavy Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=16),
    # Fountain Guard chest
    ArmorItem("sk_gd_mns_fount_chest_heavy_a", "Fountain Guard Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=16),
    ArmorItem("sk_gd_mns_fount_chest_elite_a", "Fountain Guard Elite Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),
    # Osgiliath chest
    ArmorItem("sk_gd_osg_inf_chest_med_a", "Osgiliath Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_osg_inf_chest_med_b", "Osgiliath Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_osg_inf_chest_heavy_a", "Osgiliath Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_osg_inf_chest_heavy_b", "Osgiliath Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_osg_inf_chest_elite_a", "Osgiliath Elite Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),
    # Cair Andros chest
    ArmorItem("sk_gd_cair_chainmail_half_b", "Cair Andros Half Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_cair_inf_chest_med_a", "Cair Andros Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_cair_inf_chest_med_b", "Cair Andros Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_cair_inf_chest_heavy_a", "Cair Andros Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_cair_inf_chest_heavy_b", "Cair Andros Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_cair_inf_chest_elite_a", "Cair Andros Elite Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),
    # Ithil Guard chest
    ArmorItem("sk_gd_ith_chest_noble_med_a", "Ithil Guard Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=12),
    ArmorItem("sk_gd_ith_chest_noble_med_b", "Ithil Guard Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=12),
    ArmorItem("sk_gd_ith_chest_noble_heavy_a", "Ithil Guard Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=16),
    ArmorItem("sk_gd_ith_chest_noble_heavy_b", "Ithil Guard Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=16),
]

SHOULDER_ARMORS = [
    # Anorien Infantry pauldrons
    ArmorItem("sk_gd_ano_pauld_inf_med_a", "Anorien Infantry Pauldron A", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_ano_pauld_inf_med_b", "Anorien Infantry Pauldron B", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_ano_pauld_inf_heavy_a", "Anorien Infantry Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_ano_pauld_inf_elite_a", "Anorien Infantry Elite Pauldron", "shoulder", "elite", "Plate"),
    # Anorien Noble capes
    ArmorItem("sk_gd_ano_cape_noble_a", "Anorien Noble Cape A", "shoulder", "light", "Cloth"),
    ArmorItem("sk_gd_ano_cape_noble_b", "Anorien Noble Cape B", "shoulder", "light", "Cloth"),
    # Anorien Generic capes
    ArmorItem("sk_gd_ano_cape_a", "Anorien Cape A", "shoulder", "light", "Cloth"),
    ArmorItem("sk_gd_ano_cape_b", "Anorien Cape B", "shoulder", "light", "Cloth"),
    # Anorien Infantry cape+pauldron
    ArmorItem("sk_gd_ano_pauld_cape_inf_elite_a", "Anorien Infantry Cape Pauldron", "shoulder", "elite", "Plate"),
    # Fountain Guard pauldrons/capes
    ArmorItem("sk_gd_ano_pauld_fount_heavy_a", "Fountain Guard Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_ano_pauld_fount_elite_a", "Fountain Guard Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_ano_pauld_cape_fount_elite_a", "Fountain Guard Cape Pauldron", "shoulder", "elite", "Plate"),
    # Anorien Noble pauldrons
    ArmorItem("sk_gd_ano_pauld_noble_med_a", "Anorien Noble Pauldron A", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_ano_pauld_noble_med_b", "Anorien Noble Pauldron B", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_ano_pauld_noble_med_c", "Anorien Noble Pauldron C", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_ano_pauld_noble_heavy_a", "Anorien Noble Heavy Pauldron A", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_ano_pauld_noble_heavy_b", "Anorien Noble Heavy Pauldron B", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_ano_pauld_noble_elite_a", "Anorien Noble Elite Pauldron A", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_ano_pauld_noble_elite_b", "Anorien Noble Elite Pauldron B", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_ano_pauld_cape_noble_elite_a", "Anorien Noble Cape Pauldron A", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_ano_pauld_cape_noble_elite_b", "Anorien Noble Cape Pauldron B", "shoulder", "elite", "Plate"),
    # Osgiliath pauldrons
    ArmorItem("sk_gd_osg_pauld_inf_med_a", "Osgiliath Pauldron", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_osg_pauld_inf_heavy_a", "Osgiliath Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_osg_pauld_inf_elite_a", "Osgiliath Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_osg_pauld_cape_inf_elite_a", "Osgiliath Cape Pauldron A", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_osg_pauld_cape_inf_elite_b", "Osgiliath Cape Pauldron B", "shoulder", "elite", "Plate"),
]

ARM_ARMORS = [
    ArmorItem("sk_gd_ano_gloves_a", "Anorien Gloves", "arm", "light", "Leather", covers_hands=True),
    ArmorItem("sk_gd_ano_bracer_inf_med_a", "Anorien Infantry Bracer", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_ano_bracer_noble_med_a", "Anorien Noble Bracer", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_ano_bracer_noble_heavy_a", "Anorien Noble Heavy Bracer", "arm", "heavy", "Plate", covers_hands=True),
    ArmorItem("sk_gd_ano_bracer_noble_elite_a", "Anorien Noble Elite Bracer", "arm", "elite", "Plate", covers_hands=True),
    ArmorItem("sk_gd_osg_bracer_noble_med_a", "Osgiliath Noble Bracer", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_osg_bracer_noble_heavy_a", "Osgiliath Noble Heavy Bracer", "arm", "heavy", "Plate", covers_hands=True),
    ArmorItem("sk_gd_osg_bracer_noble_elite_a", "Osgiliath Noble Elite Bracer", "arm", "elite", "Plate", covers_hands=True),
    ArmorItem("sk_gd_cair_bracer_inf_med_a", "Cair Andros Infantry Bracer", "arm", "medium", "Plate", covers_hands=True),
]

LEG_ARMORS = [
    ArmorItem("sk_gd_ano_boots_a", "Anorien Boots", "leg", "light", "Leather", covers_legs=True),
    ArmorItem("sk_gd_ano_grvs_inf_light_a", "Anorien Infantry Light Greaves", "leg", "light", "Plate", covers_legs=True),
    ArmorItem("sk_gd_ano_grvs_inf_med_a", "Anorien Infantry Greaves", "leg", "medium", "Plate", covers_legs=True),
    ArmorItem("sk_gd_ano_grvs_inf_heavy_a", "Anorien Infantry Heavy Greaves", "leg", "heavy", "Plate", covers_legs=True),
    ArmorItem("sk_gd_ano_grvs_noble_light_a", "Anorien Noble Light Greaves", "leg", "light", "Plate", covers_legs=True),
    ArmorItem("sk_gd_ano_grvs_noble_med_a", "Anorien Noble Greaves", "leg", "medium", "Plate", covers_legs=True),
    ArmorItem("sk_gd_ano_grvs_noble_heavy_a", "Anorien Noble Heavy Greaves", "leg", "heavy", "Plate", covers_legs=True),
]

# Map slot to (item list, filename)
SLOT_MAP = {
    "head":     (HEAD_ARMORS,     "head_armors.xml"),
    "body":     (BODY_ARMORS,     "body_armors.xml"),
    "shoulder": (SHOULDER_ARMORS, "shoulder_armors.xml"),
    "arm":      (ARM_ARMORS,      "arm_armors.xml"),
    "leg":      (LEG_ARMORS,      "leg_armors.xml"),
}

# Slot to Bannerlord Type/subtype
SLOT_TYPES = {
    "head":     ("HeadArmor", "head_armor"),
    "body":     ("BodyArmor", "body_armor"),
    "shoulder": ("Cape", "head_armor"),  # shoulders use Cape type, subtype varies
    "arm":      ("HandArmor", "hand_armor"),
    "leg":      ("LegArmor", "leg_armor"),
}


def generate_item_xml(item: ArmorItem) -> str:
    """Generate the XML string for a single armor item."""
    slot_type, subtype = SLOT_TYPES[item.slot]
    stats = STAT_TIERS[item.slot][item.tier]
    weight = stats["weight"]
    appearance = APPEARANCE[item.tier]

    # Build armor attributes
    armor_attrs = []

    if item.slot == "head":
        armor_attrs.append(f'head_armor="{stats["head_armor"]}"')
        armor_attrs.append('has_gender_variations="false"')
        armor_attrs.append(f'hair_cover_type="{item.hair_cover}"')
        armor_attrs.append(f'modifier_group="{item.modifier_group}"')
        armor_attrs.append(f'material_type="{item.material}"')
        armor_attrs.append(f'beard_cover_type="{item.beard_cover}"')

    elif item.slot == "body":
        armor_attrs.append(f'body_armor="{stats["body_armor"]}"')
        if item.arm_armor_stat is not None:
            armor_attrs.append(f'arm_armor="{item.arm_armor_stat}"')
        armor_attrs.append('has_gender_variations="false"')
        if item.covers_body:
            armor_attrs.append('covers_body="true"')
        armor_attrs.append(f'modifier_group="{item.modifier_group}"')
        armor_attrs.append(f'material_type="{item.material}"')

    elif item.slot == "shoulder":
        armor_attrs.append(f'body_armor="{stats["body_armor"]}"')
        armor_attrs.append(f'arm_armor="{stats["arm_armor"]}"')
        armor_attrs.append(f'modifier_group="{item.modifier_group}"')
        armor_attrs.append(f'material_type="{item.material}"')

    elif item.slot == "arm":
        armor_attrs.append(f'arm_armor="{stats["arm_armor"]}"')
        if item.covers_hands:
            armor_attrs.append('covers_hands="true"')
        armor_attrs.append(f'modifier_group="{item.modifier_group}"')
        armor_attrs.append(f'material_type="{item.material}"')

    elif item.slot == "leg":
        armor_attrs.append(f'leg_armor="{stats["leg_armor"]}"')
        if item.covers_legs:
            armor_attrs.append('covers_legs="true"')
        armor_attrs.append(f'modifier_group="{item.modifier_group}"')
        armor_attrs.append(f'material_type="{item.material}"')

    armor_str = " ".join(armor_attrs)

    # Determine subtype for shoulders
    if item.slot == "shoulder":
        subtype = "head_armor"  # Bannerlord uses head_armor subtype for capes/pauldrons

    xml = f'''    <Item
        id="{item.id}"
        name="{{=aom_{item.id}_name}}[Gondor] {item.display_name}"
        subtype="{subtype}"
        mesh="{item.id}"
        culture="Culture.gondor"
        is_merchandise="true"
        weight="{weight}"
        difficulty="0"
        appearance="{appearance}"
        Type="{slot_type}">
        <ItemComponent>
            <Armor {armor_str} />
        </ItemComponent>
        <Flags UseTeamColor="true" />
    </Item>'''
    return xml


def dry_run():
    """Print summary of all items to be created."""
    total = 0
    for slot_name, (items, filename) in SLOT_MAP.items():
        print(f"\n=== {filename} ({len(items)} items) ===")
        for item in items:
            stats = STAT_TIERS[item.slot][item.tier]
            stat_str = ", ".join(f"{k}={v}" for k, v in stats.items() if k != "weight")
            print(f"  {item.id:50s} [{item.tier:6s}] {stat_str}")
        total += len(items)
    print(f"\nTotal: {total} items")


def apply(armory_base: str):
    """Append new items to the existing LOTRLOME_Armory XML files."""
    for slot_name, (items, filename) in SLOT_MAP.items():
        filepath = os.path.join(armory_base, filename)
        if not os.path.exists(filepath):
            print(f"ERROR: {filepath} not found!", file=sys.stderr)
            continue

        # Read existing file
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()

        # Check for duplicates
        existing_ids = set()
        for item in items:
            if f'id="{item.id}"' in content:
                existing_ids.add(item.id)

        new_items = [i for i in items if i.id not in existing_ids]
        if not new_items:
            print(f"  {filename}: all {len(items)} items already exist, skipping")
            continue

        if existing_ids:
            print(f"  {filename}: {len(existing_ids)} already exist, adding {len(new_items)} new")
        else:
            print(f"  {filename}: adding {len(new_items)} new items")

        # Generate XML for new items
        new_xml = "\n\n".join(generate_item_xml(item) for item in new_items)

        # Insert before closing </Items> tag
        closing_tag = "</Items>"
        if closing_tag not in content:
            print(f"ERROR: {closing_tag} not found in {filepath}!", file=sys.stderr)
            continue

        # Add a section comment
        section_comment = f"\n    <!-- ======== NEW GONDOR REGIONAL ARMOR (auto-generated) ======== -->\n\n"
        content = content.replace(closing_tag, f"{section_comment}{new_xml}\n\n{closing_tag}")

        # Write back
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)

        print(f"    -> wrote {len(new_items)} items to {filepath}")


def main():
    parser = argparse.ArgumentParser(description="Generate Gondor armor items")
    parser.add_argument("--dry-run", action="store_true", help="List items only")
    parser.add_argument("--apply", action="store_true", help="Append to XML files")
    parser.add_argument("--armory-path", default=ARMORY_BASE, help="Path to LOTRLOME_Armory gondor/ directory")
    args = parser.parse_args()

    if args.dry_run:
        dry_run()
    elif args.apply:
        apply(args.armory_path)
    else:
        print("Usage: specify --dry-run or --apply")
        sys.exit(1)


if __name__ == "__main__":
    main()
