#!/usr/bin/env python3
"""Generate the Lothlorien armour items (LOTRLOME_items/lothlorien/*_armors.xml) from KEYforce's
lorien_armors pack (lotraom-assets v1.5/LOTRLOME_Armory/Assets/lorien_armors, 2026-11-04).

Four kits, one id family each (sk_ll_<kit>_<slot>_<tier>_<variant>):
- rg  Regular Lorien: the light line, worn by the regular tree (T4-T7). Lorien's worst armour.
- mw  Marchwardens: the middle line, worn by the Marchwarden tree (T5-T8) of Talas Duiren.
      `_lord_` helmets are for the Marchwarden lords only.
- gl  Galadhrim: the noble line (T6-T9). Helmet variant a = open (infantry), b = hood (archers),
      c = hood and mask (blades).
- ml  Mallorn: the Galadhrim kit one step up (KEYforce: "ML medium is roughly GL heavy"), so an
      ML piece is priced one band above its id token. ML `_elite_` chests and helmets are for lords.
- gl_galad: Galadriel's own pieces (chest, pauldron, bracer, crown), worn by her only. The display
  name carries her name, so rebalance_armor.is_excluded keeps them off the curve.

Values come from rebalance_armor.calculate_stats on the kingdom-cap curve (KINGDOM_CAPS
'lothlorien' = 60) at the band of the id token, the same numbers a restat writes for a piece whose
lowest wearer sits in that band. clo_* meshes in the pack are cloth overlays and get no item.

Usage:
    python tools/generate_lothlorien_armor.py              # dry run: prints the item table
    python tools/generate_lothlorien_armor.py --apply      # writes the five files
    python tools/generate_lothlorien_armor.py --apply --armory <LOTRLOME_Armory folder>
"""
import argparse
import os
import sys
from pathlib import Path
from xml.sax.saxutils import escape

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import DEFAULT_GAME_ROOT, ensure_exists, game_modules  # noqa: E402
import _generated_files as gf  # noqa: E402
import rebalance_armor as ra  # noqa: E402

CULTURE_KEY = "lothlorien"
CULTURE_ATTR = "Culture.lothlorien"
TAG = "[Lothlórien]"

KIT_NAMES = {"rg": "Lórien", "mw": "Marchwarden", "gl": "Galadhrim", "ml": "Mallorn"}
# A Mallorn piece prices one band above its token (see the docstring); lord prices as elite.
ML_BAND_UP = {"medium": "heavy", "heavy": "elite", "elite": "lord"}

# (item id, slot, display stem). Slot keys follow rebalance_armor.SLOT_TYPES.
PIECES = [
    # --- Regular ---
    ("sk_ll_rg_chest_light_a", "body", "Light Armour A"),
    ("sk_ll_rg_chest_light_b", "body", "Light Armour B"),
    ("sk_ll_rg_chest_med_a", "body", "Medium Armour"),
    ("sk_ll_rg_chest_heavy_a", "body", "Heavy Armour"),
    ("sk_ll_rg_helmet_light_a", "head", "Light Helmet"),
    ("sk_ll_rg_helmet_med_a", "head", "Medium Helmet A"),
    ("sk_ll_rg_helmet_med_b", "head", "Medium Helmet B"),
    ("sk_ll_rg_pauld_light_a", "shoulder", "Light Pauldrons"),
    ("sk_ll_rg_pauld_med_a", "shoulder", "Medium Pauldrons"),
    ("sk_ll_rg_bracer_med_a", "arm", "Bracers"),
    ("sk_ll_rg_boots_light_a", "leg", "Boots"),
    ("sk_ll_rg_grvs_med_a", "leg", "Greaves"),
    # --- Marchwarden ---
    ("sk_ll_mw_chest_med_a", "body", "Medium Armour A"),
    ("sk_ll_mw_chest_med_b", "body", "Medium Armour B"),
    ("sk_ll_mw_chest_heavy_a", "body", "Heavy Armour A"),
    ("sk_ll_mw_chest_heavy_b", "body", "Heavy Armour B"),
    ("sk_ll_mw_helmet_med_a", "head", "Medium Helmet"),
    ("sk_ll_mw_helmet_heavy_a", "head", "Heavy Helmet A"),
    ("sk_ll_mw_helmet_heavy_b", "head", "Heavy Helmet B"),
    ("sk_ll_mw_helmet_elite_a", "head", "Elite Helmet A"),
    ("sk_ll_mw_helmet_elite_b", "head", "Elite Helmet B"),
    ("sk_ll_mw_helmet_lord_a", "head", "Lord Helmet A"),
    ("sk_ll_mw_helmet_lord_b", "head", "Lord Helmet B"),
    ("sk_ll_mw_helmet_arc_med_a", "head", "Archer Helmet A"),
    ("sk_ll_mw_helmet_arc_med_b", "head", "Archer Helmet B"),
    ("sk_ll_mw_helmet_arc_heavy_a", "head", "Heavy Archer Helmet A"),
    ("sk_ll_mw_helmet_arc_heavy_b", "head", "Heavy Archer Helmet B"),
    ("sk_ll_mw_pauld_med_a", "shoulder", "Medium Pauldrons"),
    ("sk_ll_mw_pauld_heavy_a", "shoulder", "Heavy Pauldrons"),
    ("sk_ll_mw_pauld_elite_a", "shoulder", "Elite Pauldrons"),
    ("sk_ll_mw_bracer_med_a", "arm", "Bracers"),
    ("sk_ll_mw_bracer_heavy_a", "arm", "Heavy Bracers"),
    ("sk_ll_mw_grvs_med_a", "leg", "Greaves"),
    ("sk_ll_mw_grvs_heavy_a", "leg", "Heavy Greaves"),
    # --- Galadhrim ---
    ("sk_ll_gl_chest_med_a", "body", "Medium Armour A"),
    ("sk_ll_gl_chest_med_b", "body", "Medium Armour B"),
    ("sk_ll_gl_chest_heavy_a", "body", "Heavy Armour A"),
    ("sk_ll_gl_chest_heavy_b", "body", "Heavy Armour B"),
    ("sk_ll_gl_chest_elite_a", "body", "Elite Armour A"),
    ("sk_ll_gl_chest_elite_b", "body", "Elite Armour B"),
    ("sk_ll_gl_helmet_med_a", "head", "Medium Helmet"),
    ("sk_ll_gl_helmet_med_b", "head", "Medium Hooded Helmet"),
    ("sk_ll_gl_helmet_med_c", "head", "Medium Masked Helmet"),
    ("sk_ll_gl_helmet_heavy_a", "head", "Heavy Helmet"),
    ("sk_ll_gl_helmet_heavy_b", "head", "Heavy Hooded Helmet"),
    ("sk_ll_gl_helmet_heavy_c", "head", "Heavy Masked Helmet"),
    ("sk_ll_gl_pauld_med_a", "shoulder", "Medium Pauldrons"),
    ("sk_ll_gl_pauld_heavy_a", "shoulder", "Heavy Pauldrons"),
    ("sk_ll_gl_pauld_cape_heavy_a", "shoulder", "Heavy Caped Pauldrons"),
    ("sk_ll_gl_bracer_med_a", "arm", "Bracers"),
    ("sk_ll_gl_bracer_heavy_a", "arm", "Heavy Bracers"),
    ("sk_ll_gl_grvs_med_a", "leg", "Greaves"),
    ("sk_ll_gl_grvs_heavy_a", "leg", "Heavy Greaves"),
    # --- Mallorn ---
    ("sk_ll_ml_chest_heavy_a", "body", "Heavy Armour A"),
    ("sk_ll_ml_chest_heavy_b", "body", "Heavy Armour B"),
    ("sk_ll_ml_chest_elite_a", "body", "Elite Armour A"),
    ("sk_ll_ml_chest_elite_b", "body", "Elite Armour B"),
    ("sk_ll_ml_helmet_med_a", "head", "Medium Helmet"),
    ("sk_ll_ml_helmet_med_b", "head", "Medium Hooded Helmet"),
    ("sk_ll_ml_helmet_med_c", "head", "Medium Masked Helmet"),
    ("sk_ll_ml_helmet_heavy_a", "head", "Heavy Helmet"),
    ("sk_ll_ml_helmet_heavy_b", "head", "Heavy Hooded Helmet"),
    ("sk_ll_ml_helmet_heavy_c", "head", "Heavy Masked Helmet"),
    ("sk_ll_ml_helmet_elite_a", "head", "Elite Helmet"),
    ("sk_ll_ml_helmet_elite_b", "head", "Elite Hooded Helmet"),
    ("sk_ll_ml_helmet_elite_c", "head", "Elite Masked Helmet"),
    ("sk_ll_ml_pauld_med_a", "shoulder", "Medium Pauldrons"),
    ("sk_ll_ml_pauld_heavy_a", "shoulder", "Heavy Pauldrons"),
    ("sk_ll_ml_pauld_cape_heavy_a", "shoulder", "Heavy Caped Pauldrons"),
    ("sk_ll_ml_bracer_med_a", "arm", "Bracers"),
    ("sk_ll_ml_bracer_heavy_a", "arm", "Heavy Bracers"),
    ("sk_ll_ml_grvs_med_a", "leg", "Greaves"),
    ("sk_ll_ml_grvs_heavy_a", "leg", "Heavy Greaves"),
]

# Galadriel's own kit: off the curve (hero armour), lord-band values, never sold.
GALADRIEL = [
    ("sk_ll_gl_galad_chest_a", "body", "Galadriel's Gown"),
    ("sk_ll_gl_galad_pauld_a", "shoulder", "Galadriel's Mantle"),
    ("sk_ll_gl_galad_bracer_a", "arm", "Galadriel's Bracers"),
    ("sk_ll_gl_galad_crown_a", "head", "Galadriel's Circlet"),
]

FILES = {
    "head": "head_armors.xml",
    "body": "body_armors.xml",
    "arm": "arm_armors.xml",
    "leg": "leg_armors.xml",
    "shoulder": "shoulder_armors.xml",
}
ITEM_TYPE = {"head": "HeadArmor", "body": "BodyArmor", "arm": "HandArmor", "leg": "LegArmor",
             "shoulder": "Cape"}
SUBTYPE = {"head": "head_armor", "body": "body_armor", "arm": "hand_armor", "leg": "leg_armor",
           "shoulder": None}
APPEARANCE = {"light": 2, "medium": 3, "heavy": 4, "elite": 5, "lord": 6}


def kit_of(item_id):
    return item_id.split("_")[2]


def priced_band(item_id):
    token = ra.mesh_tier_of(item_id)
    if kit_of(item_id) == "ml":
        return ML_BAND_UP[token]
    return token


def stats_for(item_id, slot, band):
    return ra.calculate_stats(band, slot, CULTURE_KEY, item_id=item_id)


def lord_only(item_id):
    """Pieces the module docstring keeps for lords: `_lord_` helmets and the Mallorn `_elite_` chests and helmets.

    They are written is_merchandise="false". The armour gate refuses a piece the XML never sold
    (ArmourGateService), so no market stocks them; a town level only gates the ones that are merchandise.
    """
    return "_lord_" in item_id or (kit_of(item_id) == "ml" and "_elite_" in item_id)


def hair_cover(item_id):
    """Hooded and masked helmets (variant b/c of the Galadhrim and Mallorn kits) hide all hair."""
    if kit_of(item_id) in ("gl", "ml") and item_id[-1] in "bc":
        return "all"
    return "type2"


def armor_attrs(item_id, slot, st):
    if slot == "body":
        parts = [f'body_armor="{st["body_armor"]}"', f'leg_armor="{st["leg_armor"]}"',
                 'has_gender_variations="false"', 'covers_body="true"']
    elif slot == "head":
        parts = [f'head_armor="{st["head_armor"]}"', 'has_gender_variations="false"']
        if "crown" not in item_id:
            parts.append(f'hair_cover_type="{hair_cover(item_id)}"')
    elif slot == "arm":
        parts = [f'arm_armor="{st["arm_armor"]}"', 'covers_hands="true"']
    elif slot == "leg":
        parts = [f'leg_armor="{st["leg_armor"]}"', 'covers_legs="false"']
    else:
        parts = [f'body_armor="{st["body_armor"]}"', f'arm_armor="{st["arm_armor"]}"']
    parts.append(f'modifier_group="{st["modifier_group"]}"')
    parts.append(f'material_type="{st["material_type"]}"')
    return " ".join(parts)


def item_xml(item_id, slot, name, st, appearance, merchandise=True):
    subtype = SUBTYPE[slot]
    sub = f' subtype="{subtype}"' if subtype else ""
    diff = ' difficulty="0"' if slot != "shoulder" else ""
    flags = '<Flags UseTeamColor="true" />'
    return (
        f'    <Item id="{item_id}" name="{{=aom_{item_id}_name}}{TAG} {escape(name, {chr(34): "&quot;"})}"{sub} mesh="{item_id}" '
        f'culture="{CULTURE_ATTR}" is_merchandise="{"true" if merchandise else "false"}" '
        f'weight="{st["weight"]}"{diff} appearance="{appearance}" Type="{ITEM_TYPE[slot]}">\n'
        f"        <ItemComponent>\n"
        f"            <Armor {armor_attrs(item_id, slot, st)} />\n"
        f"        </ItemComponent>\n"
        f"        {flags}\n"
        f"    </Item>\n"
    )


def build():
    by_file = {slot: [] for slot in FILES}
    rows = []
    for item_id, slot, stem in PIECES:
        band = priced_band(item_id)
        st = stats_for(item_id, slot, band)
        name = f"{KIT_NAMES[kit_of(item_id)]} {stem}"
        by_file[slot].append(item_xml(item_id, slot, name, st, APPEARANCE[band], merchandise=not lord_only(item_id)))
        rows.append((item_id, slot, band, {k: v for k, v in st.items() if k.endswith("_armor")}, st["weight"]))
    for item_id, slot, name in GALADRIEL:
        st = stats_for(item_id, slot, "lord")
        by_file[slot].append(item_xml(item_id, slot, name, st, 7, merchandise=False))
        rows.append((item_id, slot, "hero", {k: v for k, v in st.items() if k.endswith("_armor")}, st["weight"]))
    return by_file, rows


def render(slot, items):
    head = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<!-- Generated by TAOM tools/generate_lothlorien_armor.py; edit the generator, not this file. -->\n"
        "<Items>\n"
    )
    return head + "".join(items) + "</Items>\n"


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--apply", action="store_true", help="write the files (default: dry run)")
    p.add_argument("--check", action="store_true", help="write nothing; exit 1 when the Armory differs from the render")
    p.add_argument("--armory", help="LOTRLOME_Armory folder (default: <game>/Modules/LOTRLOME_Armory)")
    args = p.parse_args()

    by_file, rows = build()
    for item_id, slot, band, armour, weight in rows:
        vals = " ".join(f"{k.split('_')[0]}={v}" for k, v in armour.items())
        print(f"{item_id:32} {slot:9} {band:7} {vals:28} w={weight}")
    print(f"{len(rows)} items")
    armory = Path(args.armory) if args.armory else game_modules(DEFAULT_GAME_ROOT) / "LOTRLOME_Armory"
    writing = args.apply or args.check
    if writing:
        armory = ensure_exists(armory, "the LOTRLOME_Armory module")
        ensure_exists(armory / "ModuleData" / "LOTRLOME_items", "LOTRLOME_Armory/ModuleData/LOTRLOME_items")
    elif not armory.exists():
        print("dry run; pass --apply to write (no Armory here to compare with)")
        return 0
    out = armory / "ModuleData" / "LOTRLOME_items" / CULTURE_KEY
    rendered = {fname: render(slot, by_file[slot]) for slot, fname in FILES.items()}
    return gf.sync(out, rendered, apply=args.apply, check=args.check)

if __name__ == "__main__":
    sys.exit(main())
