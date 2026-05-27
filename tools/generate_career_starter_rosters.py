#!/usr/bin/env python3
"""
Generate per-culture career starter equipment rosters in
Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml.

For each TAOM culture with a dedicated LOTRLOME_Armory folder, parse the
folder's body_armors.xml + leg_armors.xml to find the lowest-armor body
and leg item, then emit Infantry / Ranged / Cavalry rosters that reference
those armor items + per-culture weapon picks from a hardcoded config table.

Existing Gondor rosters are PRESERVED — they reference custom-tuned
starter_*_gondor_* items and the user wants those kept.

Cultures without a dedicated Armory folder (lothlorien, umbar, battania)
are skipped — they fall through to culture-default via the runtime grant
service's graceful fallback.

Usage:  python tools/generate_career_starter_rosters.py [--dry-run|--apply]
"""
from __future__ import annotations
import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ROSTERS_XML = ROOT / "Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml"
ARMORY_ROOT = Path("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items")

# Per-culture config.
# `folder` = LOTRLOME_Armory subfolder name (under LOTRLOME_items/).
# `extra_folders` = additional folders to scan for armor (e.g. erebor + iron_hills).
# `body_override` / `leg_override` = explicit item ID that wins over auto-pick.
#   Use when the auto-picked "lowest armor" item is visually wrong (e.g. a "dress"
#   mesh that has low armor but isn't appropriate for a soldier). The user's
#   convention: prefer what the culture's lowest-tier troop actually wears
#   (look up in troops_<culture>.xml). If even THAT is too heavy stat-wise,
#   create a `starter_<archetype>_<culture>_body_a` duplicate in LOTRLOME_Armory
#   with reduced stats — same pattern as Gondor's starter_*_gondor_* items.
# `horse` / `harness` = Item IDs for cavalry archetype.
# `arrows` = Item ID for ranged archetype.
# `inf` / `ranged` / `cavalry` = per-archetype 3-weapon picks (Item0, Item1, Item2 slot IDs).
CULTURES = {
    "mordor": {
        "folder": "mordor",
        "horse": "warg_brown", "harness": "warg_saddle",
        "arrows": "bodkin_arrows_a",
        "inf":     ["wm_mordor_set1_sword_a01", "battered_kite_shield", "wm_mordor_set1_polearm_a01"],
        "ranged":  ["hunting_bow", "bodkin_arrows_a", "wm_mordor_set1_sword_a01"],
        "cavalry": ["wm_mordor_set1_polearm_a01", "battered_kite_shield", "wm_mordor_set1_sword_a01"],
    },
    "isengard": {
        "folder": "isengard",
        "horse": "warg_brown", "harness": "warg_saddle",
        "arrows": "bodkin_arrows_a",
        "inf":     ["empire_sword_1_t2", "wm_isengard_shield_a01", "wm_gundabad_spear_a01"],
        "ranged":  ["wm_isengard_bow_a01", "bodkin_arrows_a", "empire_sword_1_t2"],
        "cavalry": ["wm_gundabad_spear_a01", "wm_isengard_shield_a01", "empire_sword_1_t2"],
    },
    "gundabad": {
        "folder": "gundabad",
        "horse": "warg_brown", "harness": "warg_saddle",
        "arrows": "bodkin_arrows_a",
        "inf":     ["wm_gundabad_sword_a01", "wm_gundabad_shield_a01", "wm_gundabad_spear_a01"],
        "ranged":  ["hunting_bow", "bodkin_arrows_a", "wm_gundabad_sword_a01"],
        "cavalry": ["wm_gundabad_spear_a01", "wm_gundabad_shield_a01", "wm_gundabad_sword_a01"],
    },
    "dolguldur": {
        "folder": "dol_guldur",
        "horse": "warg_brown", "harness": "warg_saddle",
        "arrows": "bodkin_arrows_a",
        "inf":     ["wm_dol_goldur_1h_sword_a01", "battered_kite_shield", "wm_dol_goldur_axe_a01"],
        "ranged":  ["hunting_bow", "bodkin_arrows_a", "wm_dol_goldur_1h_sword_a01"],
        "cavalry": ["wm_dol_goldur_halberd_a01", "battered_kite_shield", "wm_dol_goldur_1h_sword_a01"],
    },
    "erebor": {
        "folder": "erebor", "extra_folders": ["iron_hills"],
        # Override auto-pick: the lowest-armor item in erebor/body_armors.xml is
        # `sk_dwarf_dress_normal_a` (a "dress" mesh, body_armor=5), unsuitable for
        # a soldier. Use the lowest militia troop's actual body armor instead
        # (erebor_militia_spearman in troops_erebor.xml line 60).
        "body_override": "sk_dwarf_erebor_chest_leather_light_a",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "sk_dwarf_erebor_arrow_a",
        "inf":     ["sm_dwarf_erebor_1h_axe_a", "battered_kite_shield", "sm_dwarf_erebor_spear_a"],
        "ranged":  ["sm_dwarf_erebor_bow_a", "sk_dwarf_erebor_arrow_a", "sm_dwarf_erebor_1h_axe_a"],
        "cavalry": ["sm_dwarf_erebor_spear_a", "battered_kite_shield", "sm_dwarf_erebor_1h_axe_a"],
    },
    "rivendell": {
        "folder": "rivendell",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "wm_elven_arrow_v1_a",
        "inf":     ["wm_rivendell_sword_a01", "battered_kite_shield", "wm_rivendell_spear_a01"],
        "ranged":  ["wm_mirkwood_bow_a01", "wm_elven_arrow_v1_a", "wm_rivendell_sword_a01"],
        "cavalry": ["wm_rivendell_spear_a01", "battered_kite_shield", "wm_rivendell_sword_a01"],
    },
    "mirkwood": {
        "folder": "mirkwood",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "wm_elven_arrow_v1_a",
        "inf":     ["wm_rivendell_sword_a01", "wm_mirkwood_shield_a01", "wm_rivendell_spear_a01"],
        "ranged":  ["wm_mirkwood_bow_a01", "wm_elven_arrow_v1_a", "wm_rivendell_sword_a01"],
        "cavalry": ["wm_rivendell_spear_a01", "wm_mirkwood_shield_a01", "wm_rivendell_sword_a01"],
    },
    "vlandia": {  # Rohan (XSLT culture)
        "culture_id": "vlandia",
        "folder": "rohan",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "bodkin_arrows_a",
        "inf":     ["wm_rohan_ws_sword_a01", "battered_kite_shield", "wm_rohan_ws_spear_a01"],
        "ranged":  ["wm_rohan_ws_bow_starter", "bodkin_arrows_a", "wm_rohan_ws_sword_a01"],
        "cavalry": ["wm_rohan_ws_spear_a01", "battered_kite_shield", "wm_rohan_ws_sword_a01"],
    },
    "empire": {  # Dunland (XSLT culture)
        "culture_id": "empire",
        "folder": "dunland",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "bodkin_arrows_a",
        "inf":     ["empire_sword_1_t2", "battered_kite_shield", "dunland_caerdh_spear_a"],
        "ranged":  ["hunting_bow", "bodkin_arrows_a", "empire_sword_1_t2"],
        "cavalry": ["dunland_caerdh_spear_a", "battered_kite_shield", "empire_sword_1_t2"],
    },
    "aserai": {  # Harad (XSLT culture)
        "culture_id": "aserai",
        "folder": "harad",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "bodkin_arrows_a",
        "inf":     ["wm_harad_sword_a01", "battered_kite_shield", "wm_harad_spear_a01"],
        "ranged":  ["wm_harad_bow_a01", "bodkin_arrows_a", "wm_harad_sword_a01"],
        "cavalry": ["wm_harad_spear_a01", "battered_kite_shield", "wm_harad_sword_a01"],
    },
    "khuzait": {  # Rhun (XSLT culture). No prefixed weapons — use vanilla khuzait gear.
        "culture_id": "khuzait",
        "folder": "rhun",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "bodkin_arrows_a",
        "inf":     ["empire_sword_1_t2", "battered_kite_shield", "easterling_spear"],
        "ranged":  ["composite_bow", "bodkin_arrows_a", "empire_sword_1_t2"],
        "cavalry": ["easterling_spear", "battered_kite_shield", "empire_sword_1_t2"],
    },
    "sturgia": {  # Dale (XSLT culture). No prefixed weapons — use vanilla northern gear.
        "culture_id": "sturgia",
        "folder": "dale",
        "horse": "saddle_horse", "harness": "light_harness",
        "arrows": "bodkin_arrows_a",
        "inf":     ["empire_sword_1_t2", "battered_kite_shield", "northern_spear_1_t2"],
        "ranged":  ["hunting_bow", "bodkin_arrows_a", "empire_sword_1_t2"],
        "cavalry": ["northern_spear_1_t2", "battered_kite_shield", "empire_sword_1_t2"],
    },
}


ARMOR_PATTERN = re.compile(
    r'<Item\s+([^>]*?)>\s*<ItemComponent>\s*<Armor\s+([^/]*?)\s*/>',
    re.DOTALL,
)


def parse_lowest_armor(file_path: Path, armor_attr: str) -> tuple[str, int]:
    """
    Parse an armor XML file and return (item_id, armor_value) for the item with
    the lowest <Armor armor_attr="N"> value. Skips items without the attribute.
    """
    if not file_path.exists():
        return (None, None)
    text = file_path.read_text(encoding="utf-8")
    best_id = None
    best_armor = None
    for m in ARMOR_PATTERN.finditer(text):
        item_attrs, armor_attrs = m.group(1), m.group(2)
        id_match = re.search(r'id="([^"]+)"', item_attrs)
        if not id_match:
            continue
        item_id = id_match.group(1)
        armor_match = re.search(rf'{armor_attr}="(\d+(?:\.\d+)?)"', armor_attrs)
        if not armor_match:
            continue
        armor_val = float(armor_match.group(1))
        if best_armor is None or armor_val < best_armor:
            best_armor = armor_val
            best_id = item_id
    if best_armor is None:
        return (None, None)
    return (best_id, int(best_armor))


def pick_lowest(folder_paths: list[Path], filename: str, armor_attr: str) -> tuple[str, int]:
    """Pick the lowest-armor item across one or more folders."""
    best = (None, None)
    for fp in folder_paths:
        candidate = parse_lowest_armor(fp / filename, armor_attr)
        if candidate[1] is not None and (best[1] is None or candidate[1] < best[1]):
            best = candidate
    return best


def build_roster_xml(roster_id: str, culture_id: str, items: list[tuple[str, str]]) -> str:
    """Build an EquipmentRoster element from (slot, item_id) tuples."""
    lines = [f'    <EquipmentRoster id="{roster_id}" culture="Culture.{culture_id}">',
             '        <EquipmentSet>']
    for slot, item_id in items:
        lines.append(f'            <Equipment slot="{slot}" id="Item.{item_id}" />')
    lines.append('        </EquipmentSet>')
    lines.append('    </EquipmentRoster>')
    return "\n".join(lines)


def generate_rosters_for_culture(taom_culture: str, cfg: dict) -> list[str]:
    """Generate all 6 rosters (3 archetypes x 2 genders) for one culture."""
    culture_id = cfg.get("culture_id", taom_culture)
    folder = ARMORY_ROOT / cfg["folder"]
    extra = [ARMORY_ROOT / f for f in cfg.get("extra_folders", [])]
    all_folders = [folder] + extra

    body_override = cfg.get("body_override")
    leg_override = cfg.get("leg_override")
    if body_override:
        body_id, body_armor = body_override, "override"
    else:
        body_id, body_armor = pick_lowest(all_folders, "body_armors.xml", "body_armor")
    if leg_override:
        leg_id, leg_armor = leg_override, "override"
    else:
        leg_id, leg_armor = pick_lowest(all_folders, "leg_armors.xml", "leg_armor")
    if not body_id or not leg_id:
        print(f"  WARN  {taom_culture}: missing body or leg armor (body={body_id}, leg={leg_id})", file=sys.stderr)
        return []

    print(f"  {taom_culture}: body={body_id} ({body_armor}) / leg={leg_id} ({leg_armor})")

    rosters = []
    for archetype in ("ranged", "cavalry", "infantry"):
        weapons_key = "inf" if archetype == "infantry" else archetype
        weapons = cfg[weapons_key]
        if archetype == "ranged":
            slots = [("Item0", weapons[0]), ("Item1", weapons[1]), ("Item2", weapons[2]),
                     ("Body", body_id), ("Leg", leg_id)]
        elif archetype == "cavalry":
            slots = [("Item0", weapons[0]), ("Item1", weapons[1]), ("Item2", weapons[2]),
                     ("Body", body_id), ("Leg", leg_id),
                     ("Horse", cfg["horse"]), ("HorseHarness", cfg["harness"])]
        else:  # infantry
            slots = [("Item0", weapons[0]), ("Item1", weapons[1]), ("Item2", weapons[2]),
                     ("Body", body_id), ("Leg", leg_id)]
        for gender in ("m", "f"):
            roster_id = f"player_career_{taom_culture}_{archetype}_{gender}"
            rosters.append(build_roster_xml(roster_id, culture_id, slots))
    return rosters


# Preserved Gondor rosters — copied verbatim from the original file.
GONDOR_BLOCK = """    <!-- ═══ GONDOR — RANGED (Ranger of Ithilien) ═══ -->

    <EquipmentRoster id="player_career_gondor_ranged_m" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_bow" />
            <Equipment slot="Item1" id="Item.bodkin_arrows_a" />
            <Equipment slot="Item2" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Body"  id="Item.starter_ranged_gondor_body_a" />
            <Equipment slot="Leg"   id="Item.starter_ranged_gondor_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>

    <EquipmentRoster id="player_career_gondor_ranged_f" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_bow" />
            <Equipment slot="Item1" id="Item.bodkin_arrows_a" />
            <Equipment slot="Item2" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Body"  id="Item.starter_ranged_gondor_body_a" />
            <Equipment slot="Leg"   id="Item.starter_ranged_gondor_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>

    <!-- ═══ GONDOR — CAVALRY (Knight of Belfalas) ═══ -->

    <EquipmentRoster id="player_career_gondor_cavalry_m" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_spear_a" />
            <Equipment slot="Item1" id="Item.wm_gondor_shield_a02" />
            <Equipment slot="Item2" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Body"  id="Item.starter_cavalry_gondor_body_a" />
            <Equipment slot="Leg"   id="Item.starter_cavalry_gondor_leg_a" />
            <Equipment slot="Horse" id="Item.saddle_horse" />
            <Equipment slot="HorseHarness" id="Item.starter_cavalry_gondor_horse_armor_a" />
        </EquipmentSet>
    </EquipmentRoster>

    <EquipmentRoster id="player_career_gondor_cavalry_f" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_spear_a" />
            <Equipment slot="Item1" id="Item.wm_gondor_shield_a02" />
            <Equipment slot="Item2" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Body"  id="Item.starter_cavalry_gondor_body_a" />
            <Equipment slot="Leg"   id="Item.starter_cavalry_gondor_leg_a" />
            <Equipment slot="Horse" id="Item.saddle_horse" />
            <Equipment slot="HorseHarness" id="Item.starter_cavalry_gondor_horse_armor_a" />
        </EquipmentSet>
    </EquipmentRoster>

    <!-- ═══ GONDOR — INFANTRY (Captain of Osgiliath); Gondor=spear ═══ -->

    <EquipmentRoster id="player_career_gondor_infantry_m" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Item1" id="Item.wm_gondor_shield_a02" />
            <Equipment slot="Item2" id="Item.wm_gondor_spear_a" />
            <Equipment slot="Body"  id="Item.starter_infantry_gondor_body_a" />
            <Equipment slot="Leg"   id="Item.starter_infantry_gondor_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>

    <EquipmentRoster id="player_career_gondor_infantry_f" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Item1" id="Item.wm_gondor_shield_a02" />
            <Equipment slot="Item2" id="Item.wm_gondor_spear_a" />
            <Equipment slot="Body"  id="Item.starter_infantry_gondor_body_a" />
            <Equipment slot="Leg"   id="Item.starter_infantry_gondor_leg_a" />
        </EquipmentSet>
    </EquipmentRoster>"""


HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!--
  TAOM career-archetype starting equipment rosters.

  Roster ID convention: player_career_{cultureId}_{archetype}_{f|m}
  Looked up at end of character creation by CareerStartingEquipmentService.
  Layered on top of the culture-default roster applied by PlayerEquipmentService via
  Equipment.FillFrom (slot-by-slot merge — slots NOT mentioned here keep whatever the
  culture default set).

  Archetypes (CareerArchetype.cs):
    ranged   — bow + arrows + sword
    cavalry  — spear + shield + sword + horse + harness
    infantry — 1H + shield + (2H or spear, per-culture decided)

  Scope: career rosters override only chest (Body) + boots (Leg) + weapons (Item0..2)
  + horse/harness for cavalry. Head, Cape, Gloves inherit from culture-default.

  Gondor was the proof-of-life and uses custom starter_*_gondor_* items with
  manually tuned stats. All other cultures point at the lowest-armor item in their
  LOTRLOME_Armory folder, picked mechanically by tools/generate_career_starter_rosters.py.
  Lothlorien, Umbar, and Khand (battania) have no dedicated Armory folder and fall
  through to culture-default via ICareerStartingEquipmentService graceful fallback.
-->
<EquipmentRosters>

"""

FOOTER = "\n\n</EquipmentRosters>\n"


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--apply", action="store_true")
    g.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    parts = [GONDOR_BLOCK]
    for taom_culture, cfg in CULTURES.items():
        rosters = generate_rosters_for_culture(taom_culture, cfg)
        if rosters:
            parts.append(f"\n    <!-- ═══ {taom_culture.upper()} ═══ -->\n")
            parts.append("\n\n".join(rosters))

    body = HEADER + "\n\n".join(parts) + FOOTER

    if args.apply:
        ROSTERS_XML.write_text(body, encoding="utf-8")
        print(f"WROTE {ROSTERS_XML.name} ({len(body)} chars)")
    else:
        print(f"\nWOULD WRITE {ROSTERS_XML.name} ({len(body)} chars)")
        print(f"Total rosters: 6 (gondor preserved) + {len(CULTURES) * 6} (generated) = {6 + len(CULTURES) * 6}")
        print("Run with --apply to write.")


if __name__ == "__main__":
    main()
