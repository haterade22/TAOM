#!/usr/bin/env python3
"""Generate the Gundabad Snow Orc armour items (LOTRLOME_items/gundabad_snow/*_armors.xml) from
KEYforce's snow_orc pack (lotraom-assets v1.5/LOTRLOME_Armory/Assets/gundabad_armors/snow_orc).

The pack holds chests and cape pauldrons only (lotraom-assets tools/gundabad_armors_and_troops.md,
"Snow Orc Armors"); helmets, plain pauldrons, bracers and greaves come from Mordor's Morannon pool
(sk_md_mor_*), which already exists. One id family, sk_gb_orc_<line>_<slot>_<tier>_<variant>:
- inf_chest_*       infantry and spearmen (light/medium/heavy)
- arc_chest_*       archers and wolf riders (light/medium/heavy); arc_tunic_* = the T1 recruit
- inf_pauld_cape_*  cape pauldrons for infantry and wolf riders (light/medium/heavy)

Values come from rebalance_armor.calculate_stats on the kingdom-cap curve (KINGDOM_CAPS 'gundabad')
at the band of the id token. The tunic has no tier token and prices as light. The kit has its own
folder with the standard slot files (the curve, derive_armor_tiers and generate_armour_classes read
only those), and rebalance_armor.FOLDER_DEFAULT_LINE prices that folder on Gundabad's cap. The
English names sit in loc_gundabad.xml.
clo_* meshes in the pack are cloth overlays and get no item.

Usage:
    python tools/generate_snow_orc_armor.py              # dry run: prints the item table
    python tools/generate_snow_orc_armor.py --apply      # writes the two files
    python tools/generate_snow_orc_armor.py --apply --armory <LOTRLOME_Armory folder>
"""
import argparse
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import DEFAULT_GAME_ROOT, ensure_exists, game_modules  # noqa: E402
import _generated_files as gf  # noqa: E402
import rebalance_armor as ra  # noqa: E402

CULTURE_KEY = "gundabad"
CULTURE_ATTR = "Culture.gundabad"
TAG = "[Gundabad]"
FOLDER = "gundabad_snow"
FILES = {"body": "body_armors.xml", "shoulder": "shoulder_armors.xml"}

# (item id, slot, display name). Slot keys follow rebalance_armor.SLOT_TYPES.
PIECES = [
    ("sk_gb_orc_arc_tunic_a", "body", "Snow Orc Tunic A"),
    ("sk_gb_orc_arc_tunic_b", "body", "Snow Orc Tunic B"),
    ("sk_gb_orc_inf_chest_light_a", "body", "Snow Orc Light Armour A"),
    ("sk_gb_orc_inf_chest_light_b", "body", "Snow Orc Light Armour B"),
    ("sk_gb_orc_inf_chest_med_a", "body", "Snow Orc Medium Armour A"),
    ("sk_gb_orc_inf_chest_med_b", "body", "Snow Orc Medium Armour B"),
    ("sk_gb_orc_inf_chest_heavy_a", "body", "Snow Orc Heavy Armour A"),
    ("sk_gb_orc_inf_chest_heavy_b", "body", "Snow Orc Heavy Armour B"),
    ("sk_gb_orc_arc_chest_light_a", "body", "Snow Orc Light Hunting Garb A"),
    ("sk_gb_orc_arc_chest_light_b", "body", "Snow Orc Light Hunting Garb B"),
    ("sk_gb_orc_arc_chest_med_a", "body", "Snow Orc Hunting Armour A"),
    ("sk_gb_orc_arc_chest_med_b", "body", "Snow Orc Hunting Armour B"),
    ("sk_gb_orc_arc_chest_heavy_a", "body", "Snow Orc Heavy Hunting Armour A"),
    ("sk_gb_orc_arc_chest_heavy_b", "body", "Snow Orc Heavy Hunting Armour B"),
    ("sk_gb_orc_inf_pauld_cape_light_a", "shoulder", "Snow Orc Light Fur Mantle A"),
    ("sk_gb_orc_inf_pauld_cape_light_b", "shoulder", "Snow Orc Light Fur Mantle B"),
    ("sk_gb_orc_inf_pauld_cape_med_a", "shoulder", "Snow Orc Fur Mantle A"),
    ("sk_gb_orc_inf_pauld_cape_med_b", "shoulder", "Snow Orc Fur Mantle B"),
    ("sk_gb_orc_inf_pauld_cape_heavy_a", "shoulder", "Snow Orc Heavy Fur Mantle A"),
    ("sk_gb_orc_inf_pauld_cape_heavy_b", "shoulder", "Snow Orc Heavy Fur Mantle B"),
]

ITEM_TYPE = {"body": "BodyArmor", "shoulder": "Cape"}
SUBTYPE = {"body": "body_armor", "shoulder": None}
APPEARANCE = {"light": 2, "medium": 3, "heavy": 4}


def band_of(item_id):
    return ra.mesh_tier_of(item_id) or "light"


def stats_for(item_id, slot):
    return ra.calculate_stats(band_of(item_id), slot, CULTURE_KEY, item_id=item_id)


def armor_attrs(slot, st):
    if slot == "body":
        parts = [f'body_armor="{st["body_armor"]}"', f'leg_armor="{st["leg_armor"]}"',
                 'has_gender_variations="false"', 'covers_body="true"']
    else:
        parts = [f'body_armor="{st["body_armor"]}"', f'arm_armor="{st["arm_armor"]}"']
    parts.append(f'modifier_group="{st["modifier_group"]}"')
    parts.append(f'material_type="{st["material_type"]}"')
    return " ".join(parts)


def item_xml(item_id, slot, name, st):
    subtype = SUBTYPE[slot]
    sub = f' subtype="{subtype}"' if subtype else ""
    diff = ' difficulty="0"' if slot != "shoulder" else ""
    return (
        f'    <Item id="{item_id}" name="{{=aom_{item_id}_name}}{TAG} {name}"{sub} mesh="{item_id}" '
        f'culture="{CULTURE_ATTR}" is_merchandise="true" '
        f'weight="{st["weight"]}"{diff} appearance="{APPEARANCE[band_of(item_id)]}" Type="{ITEM_TYPE[slot]}">\n'
        f"        <ItemComponent>\n"
        f"            <Armor {armor_attrs(slot, st)} />\n"
        f"        </ItemComponent>\n"
        f'        <Flags UseTeamColor="true" />\n'
        f"    </Item>\n"
    )


def build():
    items, rows = {slot: [] for slot in FILES}, []
    for item_id, slot, name in PIECES:
        st = stats_for(item_id, slot)
        items[slot].append(item_xml(item_id, slot, name, st))
        rows.append((item_id, slot, band_of(item_id), {k: v for k, v in st.items() if k.endswith("_armor")}, st["weight"]))
    return items, rows


def render(items):
    head = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<!-- Generated by TAOM tools/generate_snow_orc_armor.py; edit the generator, not this file. -->\n"
        "<Items>\n"
    )
    return head + "".join(items) + "</Items>\n"


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--apply", action="store_true", help="write the files (default: dry run)")
    p.add_argument("--check", action="store_true", help="write nothing; exit 1 when the Armory differs from the render")
    p.add_argument("--armory", help="LOTRLOME_Armory folder (default: <game>/Modules/LOTRLOME_Armory)")
    args = p.parse_args()

    items, rows = build()
    for item_id, slot, band, armour, weight in rows:
        vals = " ".join(f"{k.split('_')[0]}={v}" for k, v in armour.items())
        print(f"{item_id:34} {slot:9} {band:7} {vals:28} w={weight}")
    print(f"{len(rows)} items")
    armory = Path(args.armory) if args.armory else game_modules(DEFAULT_GAME_ROOT) / "LOTRLOME_Armory"
    writing = args.apply or args.check
    if writing:
        armory = ensure_exists(armory, "the LOTRLOME_Armory module")
        ensure_exists(armory / "ModuleData" / "LOTRLOME_items", "LOTRLOME_Armory/ModuleData/LOTRLOME_items")
    elif not armory.exists():
        print("dry run; pass --apply to write (no Armory here to compare with)")
        return 0
    out = armory / "ModuleData" / "LOTRLOME_items" / FOLDER
    rendered = {fname: render(items[slot]) for slot, fname in FILES.items()}
    return gf.sync(out, rendered, apply=args.apply, check=args.check)

if __name__ == "__main__":
    sys.exit(main())
