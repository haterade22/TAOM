#!/usr/bin/env python3
"""Author Iron Hills dwarf armor items per KEYforce spec.

Source-of-truth: E:\\repos\\lotraom-assets\\tools\\erebor_armors_and_troops.txt
Issue: KEYforce mesh-first revamp (Erebor pass — Iron Hills)

Adds `sk_dwarf_iron_*` items. The 174 sk_dwarf_erebor_* core dwarven items
already exist — not touched.

Item IDs are auto-classified by parsing the spec file. Stats follow the
standard STAT_TIERS table.

Usage:
    python tools/generate_erebor_armor.py --dry-run
    python tools/generate_erebor_armor.py --apply
"""
import argparse
import os
import re
import sys
from dataclasses import dataclass
from typing import Optional

DEFAULT_ARMORY_BASE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\iron_hills"
)

SPEC_FILE = r"E:\repos\lotraom-assets\tools\erebor_armors_and_troops.txt"

STAT_TIERS = {
    "head": {
        "light":  {"head_armor": 15, "weight": 1.5},
        "medium": {"head_armor": 24, "weight": 2.5},
        "heavy":  {"head_armor": 32, "weight": 3.5},
        "elite":  {"head_armor": 40, "weight": 4.5},
        "lord":   {"head_armor": 48, "weight": 5.0},
    },
    "body": {
        "light":  {"body_armor": 20, "leg_armor": 10, "weight": 8.0},
        "medium": {"body_armor": 32, "leg_armor": 16, "weight": 13.0},
        "heavy":  {"body_armor": 42, "leg_armor": 22, "weight": 18.0},
        "elite":  {"body_armor": 50, "leg_armor": 28, "weight": 22.0},
        "lord":   {"body_armor": 58, "leg_armor": 32, "weight": 24.0},
    },
    # Must mirror rebalance_armor.SHOULDER_BASELINES (pinned by
    # tools/tests/test_armor_curve_invariant.py::GeneratorCurveSyncTests).
    "shoulder": {
        "light":  {"body_armor": 5,  "arm_armor": 3,  "weight": 3.0},
        "medium": {"body_armor": 9,  "arm_armor": 6,  "weight": 5.0},
        "heavy":  {"body_armor": 13, "arm_armor": 11, "weight": 7.0},
        "elite":  {"body_armor": 19, "arm_armor": 17, "weight": 9.0},
        "lord":   {"body_armor": 25, "arm_armor": 22, "weight": 11.0},
    },
    "arm": {
        "light":  {"arm_armor": 8,  "weight": 0.6},
        "medium": {"arm_armor": 14, "weight": 1.0},
        "heavy":  {"arm_armor": 20, "weight": 1.5},
        "elite":  {"arm_armor": 26, "weight": 2.0},
        "lord":   {"arm_armor": 30, "weight": 2.2},
    },
    "leg": {
        "light":  {"leg_armor": 12, "weight": 1.5},
        "medium": {"leg_armor": 20, "weight": 2.5},
        "heavy":  {"leg_armor": 28, "weight": 3.5},
        "elite":  {"leg_armor": 34, "weight": 4.0},
        "lord":   {"leg_armor": 38, "weight": 4.2},
    },
}

APPEARANCE = {"light": 1, "medium": 3, "heavy": 4, "elite": 6, "lord": 7}

# Map ID category → slot
CATEGORY_TO_SLOT = {
    "helmet":   "head",
    "chest":    "body",
    "pauldron": "shoulder",
    "bracer":   "arm",
    "bracers":  "arm",   # plural form in spec
    "boots":    "leg",
    "greaves":  "leg",
}

# Map tier word → tier key
TIER_NORMALIZE = {
    "light":  "light",
    "med":    "medium",
    "medium": "medium",
    "heavy":  "heavy",
    "elite":  "elite",
    "lord":   "lord",
}


@dataclass
class ArmorItem:
    id: str
    display_name: str
    slot: str
    tier: str
    material: str = "Plate"
    modifier_group: str = ""
    hair_cover: str = "type2"
    beard_cover: str = "none"
    covers_body: bool = False
    covers_hands: bool = False
    covers_legs: bool = False
    arm_armor_stat: Optional[int] = None

    def __post_init__(self):
        if not self.modifier_group:
            self.modifier_group = {
                "Plate": "plate", "Chainmail": "chain",
                "Leather": "leather", "Cloth": "cloth",
            }.get(self.material, "plate")


def parse_iron_hills_items():
    """Extract sk_dwarf_iron_* item IDs from spec file and classify each."""
    with open(SPEC_FILE, "r", encoding="utf-8") as f:
        text = f.read()
    ids = sorted(set(re.findall(r'sk_dwarf_iron_[a-z0-9_]+', text)))
    items = []
    for item_id in ids:
        # Detect "_cape" infix forms (e.g., pauldron_heavy_cape_a)
        cape = "_cape" in item_id
        # Split-based parse: ["sk", "dwarf", "iron", "category", ..., "tier", "variant"]
        parts = item_id.split("_")
        if len(parts) < 5:
            print(f"  WARN: cannot parse {item_id}", file=sys.stderr)
            continue
        # parts[3] is the category (helmet/chest/pauldron/bracer/boots)
        category = parts[3]
        # Find tier in remaining parts
        tier_idx = None
        for i in range(4, len(parts)):
            if parts[i] in TIER_NORMALIZE:
                tier_idx = i
                break
        if tier_idx is None:
            print(f"  WARN: no tier in {item_id}", file=sys.stderr)
            continue
        tier_raw = parts[tier_idx]
        tier = TIER_NORMALIZE[tier_raw]
        # Variant suffix = everything after tier (excluding the tier word itself)
        variant = "_".join(parts[tier_idx + 1:])
        # If there's a "cape" in between (e.g., pauldron_heavy_cape_a), variant includes it
        # That's fine.
        slot = CATEGORY_TO_SLOT.get(category)
        if not slot:
            print(f"  WARN: unknown category '{category}' in {item_id}", file=sys.stderr)
            continue

        # Material heuristic (explicit precedence)
        if "leather" in item_id or (category == "boots" and tier == "light"):
            material = "Leather"
        elif "chain" in item_id:
            material = "Chainmail"
        else:
            material = "Plate"

        # Display name
        readable_category = {
            "helmet":   "Helmet",
            "chest":    "Armour",
            "pauldron": "Pauldron",
            "bracer":   "Bracer",
            "bracers":  "Bracer",
            "boots":    "Boots",
            "greaves":  "Greaves",
        }[category]
        tier_label = {
            "light":  "Light",
            "medium": "",
            "heavy":  "Heavy",
            "elite":  "Elite",
            "lord":   "Lord",
        }[tier]
        cape_label = " (Cape)" if cape else ""
        display = f"Iron Hills {tier_label} {readable_category}{cape_label} {variant.upper()}".strip()
        display = re.sub(r'\s+', ' ', display)

        items.append(ArmorItem(
            id=item_id,
            display_name=display,
            slot=slot,
            tier=tier,
            material=material,
            covers_body=(slot == "body"),
            covers_hands=(slot == "arm"),
            covers_legs=(slot == "leg"),
            arm_armor_stat=(10 if slot == "body" and tier in ("medium", "heavy") else
                            (14 if slot == "body" and tier == "elite" else
                             (16 if slot == "body" and tier == "lord" else None))),
        ))
    return items


SLOT_TYPES = {
    "head":     ("HeadArmor", "head_armor"),
    "body":     ("BodyArmor", "body_armor"),
    "shoulder": ("Cape",      "head_armor"),
    "arm":      ("HandArmor", "hand_armor"),
    "leg":      ("LegArmor",  "leg_armor"),
}

CULTURE = "erebor"
ITEM_NAME_PREFIX = "Erebor"


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
    elif item.slot == "shoulder":
        armor_attrs += [
            f'body_armor="{stats["body_armor"]}"',
            f'arm_armor="{stats["arm_armor"]}"',
            f'modifier_group="{item.modifier_group}"',
            f'material_type="{item.material}"',
        ]
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
    if item.slot == "shoulder":
        subtype = "head_armor"

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
    items = parse_iron_hills_items()
    by_slot = {}
    for item in items:
        by_slot.setdefault(item.slot, []).append(item)
    for slot, sl_items in sorted(by_slot.items()):
        print(f"\n=== {slot} ({len(sl_items)} items) ===")
        for item in sl_items:
            stats = STAT_TIERS[item.slot][item.tier]
            stat_str = ", ".join(f"{k}={v}" for k, v in stats.items() if k != "weight")
            print(f"  {item.id:55s} [{item.tier:6s}] {stat_str}")
    print(f"\nTotal: {len(items)} items")


SLOT_TO_FILENAME = {
    "head":     "head_armors.xml",
    "body":     "body_armors.xml",
    "shoulder": "shoulder_armors.xml",
    "arm":      "arm_armors.xml",
    "leg":      "leg_armors.xml",
}


def apply(armory_base: str):
    items = parse_iron_hills_items()
    print(f"Target: {armory_base}\nTotal items parsed from spec: {len(items)}\n")

    by_slot = {}
    for item in items:
        by_slot.setdefault(item.slot, []).append(item)

    grand_added = 0
    grand_skipped = 0
    for slot, sl_items in by_slot.items():
        filename = SLOT_TO_FILENAME[slot]
        filepath = os.path.join(armory_base, filename)
        if not os.path.exists(filepath):
            print(f"ERROR: {filepath} not found", file=sys.stderr)
            continue

        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()

        existing_ids = {item.id for item in sl_items if f'id="{item.id}"' in content}
        new_items = [i for i in sl_items if i.id not in existing_ids]
        grand_skipped += len(existing_ids)

        if not new_items:
            print(f"  {filename}: all {len(sl_items)} items already exist, skipping")
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
            "    <!--  KEYforce Iron Hills armor (sk_dwarf_iron_*)                    -->\n"
            "    <!-- ============================================================== -->\n\n"
        )
        content = content.replace(closing_tag, f"{section_comment}{new_xml}\n\n{closing_tag}")

        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)

        print(f"    -> wrote {len(new_items)} items to {filepath}")
        grand_added += len(new_items)

    print(f"\nDone. Added {grand_added} new items, skipped {grand_skipped} already present.")


def main():
    parser = argparse.ArgumentParser(description="Erebor Iron Hills armor generator")
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
