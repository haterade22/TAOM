#!/usr/bin/env python3
"""Apply KEYforce Isengard troop tree revamp per spec.

Issue: #212
Source-of-truth: E:\\repos\\lotraom-assets\\tools\\isengard_armors_and_troops.txt

Three unit groups (per spec):
  1. ISENGARD ORC TROOPS (race="orc", T1-T5) — Section 1, ALL NEW
     - Melee:  Grunt -> Brawler -> Warrior -> Marauder -> Reaver
     - Shock:  (from Brawler) -> Berserker -> Ravager -> Butcher -> Slayer
     - Warg:   (from Grunt)   -> Warg Scout v2 -> Rider v2 -> Raider v2 -> Ravager v2

  2. URUK-HAI LEGION (race kept as existing uruk_hai, T2-T8)
     - Refit equipment to spec armor guide. Per-line tier chains intact.

  3. URUK-HAI SCOUTS MERCENARY (race kept, T2-T6)
     - Refit equipment + LOWER LEVELS to match spec tiers
       (current file has them at T3-T7, spec says T2-T6).

Per user direction (2026-05-23):
  - KEEP orthanc_* line untouched (TAOM-canonical, lore-rooted).
  - KEEP existing orc_warg_scout/overseer/enforcer/lieutenant as
    race="uruk_hai" light cavalry (save compat). The new spec-canonical
    orc-race wargs use _v2 suffix to avoid ID collision.
  - DELETE_IDS is intentionally empty.

Save-compat: All existing IDs preserved. New troop IDs (orc_*, _v2)
do not collide with existing. Adds 13 new troops only.

Usage:
    python tools/apply_isengard_troop_revamp.py --dry-run
    python tools/apply_isengard_troop_revamp.py --apply
"""
import argparse
import os
import re
import sys
from typing import Dict, List, Tuple

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_isengard.xml"
)

# Per user direction: KEEP orthanc_* line; nothing to delete.
DELETE_IDS: set = set()

# Display name + level updates. Only the Scout MERCENARY line needs level
# adjustments (spec tiers T2-T6 vs current T3-T7). Display names already
# carry the "[Isengard]" prefix; we standardize them where needed.
RENAMES: Dict[str, Dict[str, str]] = {
    # Scout MERC line — bring levels into spec alignment
    "urukhai_scout":          {"name": "Uruk-Hai Scout",           "level": "6"},
    "urukhai_feller":         {"name": "Uruk-Hai Feller",          "level": "11"},
    "urukhai_slayer":         {"name": "Uruk-Hai Slayer",          "level": "16"},
    "urukhai_reaver":         {"name": "Uruk-Hai Reaver",          "level": "21"},
    "urukhai_hunter":         {"name": "Uruk-Hai Hunter",          "level": "11"},
    "urukhai_tracker":        {"name": "Uruk-Hai Tracker",         "level": "16"},
    "urukhai_archer":         {"name": "Uruk-Hai Archer",          "level": "21"},
    "urukhai_chosenmarksman": {"name": "Uruk-Hai Chosen Marksman", "level": "26"},
}

# Upgrade chain rewires (Uruk-Hai Legion + Scout + existing orc_warg + new orc tree).
# Existing chains that already match spec are listed for idempotency.
UPGRADE_TARGETS: Dict[str, List[str]] = {
    # ---------- URUK-HAI LEGION ----------
    "urukhai_recruit":             ["urukhai_fighter"],
    "urukhai_fighter":             ["urukhai_warrior", "urukhai_skirmisher"],
    "urukhai_warrior":             ["urukhai_spearman", "urukhai_swordman"],
    # Spear branch
    "urukhai_spearman":            ["urukhai_pikeman", "urukhai_veteranspearman"],
    "urukhai_pikeman":             ["urukhai_veteranpikeman"],
    "urukhai_veteranpikeman":      [],
    "urukhai_veteranspearman":     ["urukhai_pavise"],
    "urukhai_pavise":              [],
    # Sword branch
    "urukhai_swordman":            ["urukhai_infantry", "urukhai_champion"],
    "urukhai_infantry":            ["urukhai_veteraninfantry"],
    "urukhai_veteraninfantry":     [],
    "urukhai_champion":            ["urukhai_berserker"],
    "urukhai_berserker":           ["urukhai_nazg_hai"],
    "urukhai_nazg_hai":            [],
    # Crossbow branch
    "urukhai_skirmisher":          ["urukhai_arbalest"],
    "urukhai_arbalest":            ["urukhai_crossbowman"],
    "urukhai_crossbowman":         ["urukhai_veterancrossbowman"],
    "urukhai_veterancrossbowman":  [],
    # ---------- URUK-HAI SCOUT MERC ----------
    "urukhai_scout":               ["urukhai_feller", "urukhai_hunter"],
    "urukhai_feller":              ["urukhai_slayer"],
    "urukhai_slayer":              ["urukhai_reaver"],
    "urukhai_reaver":              [],
    "urukhai_hunter":              ["urukhai_tracker"],
    "urukhai_tracker":             ["urukhai_archer"],
    "urukhai_archer":              ["urukhai_chosenmarksman"],
    "urukhai_chosenmarksman":      [],
    # ---------- EXISTING ORC_WARG (uruk_hai cavalry — kept as-is per user direction) ----------
    "orc_warg_scout":              ["orc_warg_overseer"],
    "orc_warg_overseer":           ["orc_warg_enforcer"],
    "orc_warg_enforcer":           ["orc_warg_lieutenant"],
    "orc_warg_lieutenant":         [],
    # ---------- NEW ORC TREE (race="orc") ----------
    "isengard_orc_grunt":          ["isengard_orc_brawler", "isengard_orc_berserker", "isengard_orc_warg_scout_v2"],
    # Melee line
    "isengard_orc_brawler":        ["isengard_orc_warrior"],
    "isengard_orc_warrior":        ["isengard_orc_marauder"],
    "isengard_orc_marauder":       ["isengard_orc_reaver"],
    "isengard_orc_reaver":         [],
    # Shock line
    "isengard_orc_berserker":      ["isengard_orc_ravager"],
    "isengard_orc_ravager":        ["isengard_orc_butcher"],
    "isengard_orc_butcher":        ["isengard_orc_slayer"],
    "isengard_orc_slayer":         [],
    # Warg line (orc race)
    "isengard_orc_warg_scout_v2":  ["isengard_orc_warg_rider_v2"],
    "isengard_orc_warg_rider_v2":  ["isengard_orc_warg_raider_v2"],
    "isengard_orc_warg_raider_v2": ["isengard_orc_warg_ravager_v2"],
    "isengard_orc_warg_ravager_v2": [],
}

# Per-troop equipment replacement. Replaces the ENTIRE <Equipments>...</Equipments>
# block with a single new <EquipmentRoster> built from the slot list below.
# Existing variant rosters are dropped — single canonical roster per spec.
EQUIPMENT: Dict[str, List[Tuple[str, str]]] = {
    # ============================================================
    # URUK-HAI LEGION — Sword/Infantry, Spear, Pike, Crossbow, Bers
    # ============================================================
    # Shared low tiers
    "urukhai_recruit": [
        ("Item0",  "isengard_1h_sword_b"),
        ("Item1",  "wm_isengard_shield_a04"),
        ("Head",   "sk_uruk_hai_helmet_sword_light_a1"),
        ("Body",   "sk_uruk_hai_tunic_a1"),
        ("Cape",   "sk_uruk_hai_pauldron_light_a1"),
        ("Gloves", "sk_uruk_hai_gloves_a1"),
        ("Leg",    "sk_uruk_hai_shoes_a1"),
    ],
    "urukhai_fighter": [
        ("Item0",  "isengard_1h_sword_b"),
        ("Item1",  "wm_isengard_shield_a03"),
        ("Head",   "sk_uruk_hai_helmet_sword_light_a2"),
        ("Body",   "sk_uruk_hai_chainmail_a1"),
        ("Cape",   "sk_uruk_hai_pauldron_light_a2"),
        ("Gloves", "sk_uruk_hai_bracer_light_a1"),
        ("Leg",    "sk_uruk_hai_greaves_medium_a1"),
    ],
    # Sword/Infantry line
    "urukhai_warrior": [
        ("Item0",  "isengard_1h_sword_a"),
        ("Item1",  "wm_isengard_shield_a03"),
        ("Head",   "sk_uruk_hai_helmet_sword_light_a4"),
        ("Body",   "sk_uruk_hai_plate_light_b1"),
        ("Cape",   "sk_uruk_hai_pauldron_light_a3"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a1"),
        ("Leg",    "sk_uruk_hai_greaves_medium_a2"),
    ],
    "urukhai_swordman": [
        ("Item0",  "isengard_1h_sword_a"),
        ("Item1",  "wm_isengard_shield_a02"),
        ("Head",   "sk_uruk_hai_helmet_sword_med_a2"),
        ("Body",   "sk_uruk_hai_plate_med_b1"),
        ("Cape",   "sk_uruk_hai_pauldron_med_a1"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a2"),
        ("Leg",    "sk_uruk_hai_greaves_medium_a3"),
    ],
    "urukhai_infantry": [
        ("Item0",  "isengard_1h_sword_a"),
        ("Item1",  "wm_isengard_shield_a02"),
        ("Head",   "sk_uruk_hai_helmet_sword_med_a4"),
        ("Body",   "sk_uruk_hai_plate_heavy_b1"),
        ("Cape",   "sk_uruk_hai_pauldron_med_a3"),
        ("Gloves", "sk_uruk_hai_bracer_heavy_a1"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a1"),
    ],
    "urukhai_veteraninfantry": [
        ("Item0",  "isengard_1h_sword_a"),
        ("Item1",  "wm_isengard_shield_a01"),
        ("Head",   "sk_uruk_hai_helmet_sword_heavy_a2"),
        ("Body",   "sk_uruk_hai_plate_heavy_b2"),
        ("Cape",   "sk_uruk_hai_pauldron_heavy_a1"),
        ("Gloves", "sk_uruk_hai_bracer_elite_a1"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a2"),
    ],
    # Spear line
    "urukhai_spearman": [
        ("Item0",  "isengard_spear_a"),
        ("Item1",  "wm_isengard_shield_c01"),
        ("Head",   "sk_uruk_hai_helmet_spear_light_a3"),
        ("Body",   "sk_uruk_hai_plate_med_b2"),
        ("Cape",   "sk_uruk_hai_pauldron_med_a1"),
        ("Gloves", "sk_uruk_hai_bracer_medium_b1"),
        ("Leg",    "sk_uruk_hai_greaves_medium_b1"),
    ],
    "urukhai_veteranspearman": [
        ("Item0",  "isengard_spear_a"),
        ("Item1",  "wm_isengard_shield_c01"),
        ("Head",   "sk_uruk_hai_helmet_spear_med_a2"),
        ("Body",   "sk_uruk_hai_plate_heavy_b1"),
        ("Cape",   "sk_uruk_hai_pauldron_heavy_a1"),
        ("Gloves", "sk_uruk_hai_bracer_heavy_b1"),
        ("Leg",    "sk_uruk_hai_greaves_medium_b2"),
    ],
    "urukhai_pavise": [
        ("Item0",  "isengard_spear_a"),
        ("Item1",  "wm_isengard_shield_c02"),
        ("Head",   "sk_uruk_hai_helmet_spear_heavy_a1"),
        ("Body",   "sk_uruk_hai_plate_elite_b1"),
        ("Cape",   "sk_uruk_hai_pauldron_heavy_b1"),
        ("Gloves", "sk_uruk_hai_bracer_elite_b1"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a2"),
    ],
    # Pike line
    "urukhai_pikeman": [
        ("Item0",  "isengard_pike_a"),
        ("Item1",  "isengard_1h_sword_b"),
        ("Head",   "sk_uruk_hai_helmet_spear_med_a3"),
        ("Body",   "sk_uruk_hai_plate_heavy_a1"),
        ("Cape",   "sk_uruk_hai_pauldron_heavy_a2"),
        ("Gloves", "sk_uruk_hai_bracer_heavy_b1"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a1"),
    ],
    "urukhai_veteranpikeman": [
        ("Item0",  "isengard_pike_b"),
        ("Item1",  "isengard_1h_sword_a"),
        ("Head",   "sk_uruk_hai_helmet_spear_heavy_a2"),
        ("Body",   "sk_uruk_hai_plate_elite_a1"),
        ("Cape",   "sk_uruk_hai_pauldron_heavy_b2"),
        ("Gloves", "sk_uruk_hai_bracer_elite_b2"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a2"),
    ],
    # Crossbow line
    "urukhai_skirmisher": [
        ("Item0",  "wm_isengard_crossbow_a01"),
        ("Item1",  "isengard_1h_sword_b"),
        ("Item2",  "wm_isengard_bolt_a01"),
        ("Head",   "sk_uruk_hai_helmet_skir_a2"),
        ("Body",   "sk_uruk_hai_plate_light_a1"),
        ("Cape",   "sk_uruk_hai_pauldron_light_a2"),
        ("Gloves", "sk_uruk_hai_bracer_light_a2"),
        ("Leg",    "sk_uruk_hai_greaves_medium_a1"),
    ],
    "urukhai_arbalest": [
        ("Item0",  "wm_isengard_crossbow_a01"),
        ("Item1",  "isengard_1h_sword_b"),
        ("Item2",  "wm_isengard_bolt_a01"),
        ("Head",   "sk_uruk_hai_helmet_sapper_a1"),
        ("Body",   "sk_uruk_hai_plate_light_a2"),
        ("Cape",   "sk_uruk_hai_pauldron_med_a1"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a1"),
        ("Leg",    "sk_uruk_hai_greaves_medium_a2"),
    ],
    "urukhai_crossbowman": [
        ("Item0",  "wm_isengard_crossbow_a01"),
        ("Item1",  "isengard_1h_sword_a"),
        ("Item2",  "wm_isengard_bolt_a01"),
        ("Head",   "sk_uruk_hai_helmet_cross_a1"),
        ("Body",   "sk_uruk_hai_plate_med_a1"),
        ("Cape",   "sk_uruk_hai_pauldron_med_a2"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a3"),
        ("Leg",    "sk_uruk_hai_greaves_medium_a3"),
    ],
    "urukhai_veterancrossbowman": [
        ("Item0",  "wm_isengard_crossbow_a01"),
        ("Item1",  "isengard_1h_sword_a"),
        ("Item2",  "wm_isengard_bolt_a01"),
        ("Head",   "sk_uruk_hai_helmet_cross_a3"),
        ("Body",   "sk_uruk_hai_plate_heavy_a1"),
        ("Cape",   "sk_uruk_hai_pauldron_heavy_a1"),
        ("Gloves", "sk_uruk_hai_bracer_heavy_a2"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a1"),
    ],
    # Berserker/Champion line (NO chest — Berserker basemesh)
    "urukhai_champion": [
        ("Item0",  "isengard_berserker_sword_2h"),
        ("Head",   "sk_uruk_hai_helmet_officer_a2"),
        ("Cape",   "sk_uruk_hai_skirt_a1"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a3"),
        ("Leg",    "sk_uruk_hai_greaves_medium_a3"),
    ],
    "urukhai_berserker": [
        ("Item0",  "isengard_berserker_sword_2h"),
        ("Head",   "sk_uruk_hai_helmet_bers_a2"),
        ("Cape",   "sk_uruk_hai_skirt_a1"),
        ("Gloves", "sk_uruk_hai_bracer_heavy_a1"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a1"),
    ],
    "urukhai_nazg_hai": [
        ("Item0",  "isengard_berserker_sword_2h"),
        ("Head",   "sk_uruk_hai_helmet_nazg_a2"),
        ("Body",   "sk_uruk_hai_plate_elite_a2"),
        ("Cape",   "sk_uruk_hai_pauldron_heavy_b1"),
        ("Gloves", "sk_uruk_hai_bracer_elite_a2"),
        ("Leg",    "sk_uruk_hai_greaves_heavy_a3"),
    ],
    # ============================================================
    # URUK-HAI SCOUT MERCENARY (T2-T6)
    # ============================================================
    "urukhai_scout": [
        ("Item0",  "isengard_2h_axe_b"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Gloves", "urukscout_gloves"),
        ("Leg",    "urukscout_boots"),
    ],
    "urukhai_feller": [
        ("Item0",  "isengard_2h_axe_b"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Gloves", "urukscout_gloves"),
        ("Leg",    "urukscout_boots"),
    ],
    "urukhai_slayer": [
        ("Item0",  "isengard_2h_axe_a"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Gloves", "sk_uruk_hai_bracer_light_a1"),
        ("Leg",    "urukscout_boots"),
    ],
    "urukhai_reaver": [
        ("Item0",  "isengard_2h_axe_a"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Cape",   "sk_uruk_hai_pauldron_light_a2"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a1"),
        ("Leg",    "urukscout_boots"),
    ],
    "urukhai_hunter": [
        ("Item0",  "wm_isengard_bow_a01"),
        ("Item1",  "isengard_1h_sword_b"),
        ("Item2",  "wm_isengard_arrow_a01"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Gloves", "urukscout_gloves"),
        ("Leg",    "urukscout_boots"),
    ],
    "urukhai_tracker": [
        ("Item0",  "wm_isengard_bow_a01"),
        ("Item1",  "isengard_1h_sword_b"),
        ("Item2",  "wm_isengard_arrow_a01"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Gloves", "sk_uruk_hai_bracer_light_a2"),
        ("Leg",    "urukscout_boots"),
    ],
    "urukhai_archer": [
        ("Item0",  "wm_isengard_bow_a02"),
        ("Item1",  "isengard_1h_sword_b"),
        ("Item2",  "wm_isengard_shield_a04"),
        ("Item3",  "wm_isengard_arrow_a01"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Cape",   "sk_uruk_hai_pauldron_light_a3"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a2"),
        ("Leg",    "urukscout_boots"),
    ],
    "urukhai_chosenmarksman": [
        ("Item0",  "wm_isengard_bow_a02"),
        ("Item1",  "isengard_1h_sword_a"),
        ("Item2",  "wm_isengard_shield_a03"),
        ("Item3",  "wm_isengard_arrow_a01"),
        ("Head",   "urukscout_helmet"),
        ("Body",   "urukscout_body"),
        ("Cape",   "sk_uruk_hai_pauldron_med_a2"),
        ("Gloves", "sk_uruk_hai_bracer_medium_a3"),
        ("Leg",    "urukscout_boots"),
    ],
    # ============================================================
    # EXISTING ORC_WARG_* — keep race=uruk_hai light cavalry,
    # refit armor to be consistent with scout-class gear.
    # ============================================================
    "orc_warg_scout": [
        ("Item0",        "isengard_spear_a"),
        ("Item1",        "isengard_1h_sword_b"),
        ("Item2",        "wm_isengard_shield_a04"),
        ("Head",         "urukscout_helmet"),
        ("Body",         "urukscout_body"),
        ("Gloves",       "urukscout_gloves"),
        ("Leg",          "urukscout_boots"),
        ("Horse",        "warg_brown"),
        ("HorseHarness", "warg_saddle"),
    ],
    "orc_warg_overseer": [
        ("Item0",        "isengard_spear_a"),
        ("Item1",        "isengard_1h_sword_b"),
        ("Item2",        "wm_isengard_shield_a03"),
        ("Head",         "urukscout_helmet"),
        ("Body",         "urukscout_body"),
        ("Cape",         "sk_uruk_hai_pauldron_light_a2"),
        ("Gloves",       "sk_uruk_hai_bracer_light_a2"),
        ("Leg",          "urukscout_boots"),
        ("Horse",        "warg_brown"),
        ("HorseHarness", "warg_saddle"),
    ],
    "orc_warg_enforcer": [
        ("Item0",        "isengard_spear_a"),
        ("Item1",        "isengard_1h_sword_a"),
        ("Item2",        "wm_isengard_shield_a02"),
        ("Head",         "urukscout_helmet"),
        ("Body",         "urukscout_body"),
        ("Cape",         "sk_uruk_hai_pauldron_med_a1"),
        ("Gloves",       "sk_uruk_hai_bracer_medium_a2"),
        ("Leg",          "urukscout_boots"),
        ("Horse",        "warg_dark"),
        ("HorseHarness", "warg_saddle"),
    ],
    "orc_warg_lieutenant": [
        ("Item0",        "isengard_spear_b"),
        ("Item1",        "isengard_1h_sword_a"),
        ("Item2",        "wm_isengard_shield_a01"),
        ("Head",         "urukscout_helmet"),
        ("Body",         "urukscout_body"),
        ("Cape",         "sk_uruk_hai_pauldron_med_a3"),
        ("Gloves",       "sk_uruk_hai_bracer_medium_a3"),
        ("Leg",          "urukscout_boots"),
        ("Horse",        "warg_dark"),
        ("HorseHarness", "warg_saddle"),
    ],
}


# Helper: build a complete NEW NPCCharacter XML block for an orc-race troop.
def _new_orc_npc(
    tid: str,
    name: str,
    level: int,
    skills: Dict[str, int],
    equipment: List[Tuple[str, str]],
    is_basic: bool = False,
    default_group: str = "Infantry",
) -> str:
    skill_lines = "\n".join(
        f'            <skill\n                id="{k}"\n                value="{v}" />'
        for k, v in skills.items()
    )
    equip_lines = "\n".join(
        f'                <equipment\n                    slot="{slot}"\n                    id="Item.{item_id}" />'
        for slot, item_id in equipment
    )
    is_basic_attr = '\n        is_basic_troop="true"' if is_basic else ""
    name_attr = '{=aom_' + tid + '_name}[Isengard] ' + name
    return f'''
    <NPCCharacter
        id="{tid}"
        race="orc"
        default_group="{default_group}"
        level="{level}"
        name="{name_attr}"
        occupation="Soldier"{is_basic_attr}
        culture="Culture.isengard">
        <face>
            <face_key_template
                value="BodyProperty.fighter_empire" />
        </face>
        <skills>
{skill_lines}
        </skills>
        <upgrade_targets />
        <Equipments>
            <EquipmentRoster>
{equip_lines}
            </EquipmentRoster>
        </Equipments>
    </NPCCharacter>
'''


# Tier-scaled skill profiles for orc-race troops (loosely matched to existing
# uruk_hai equivalents but lower across the board — orcs are not Uruks).
_ORC_SKILLS: Dict[int, Dict[str, int]] = {
    1: {"Athletics": 30, "Riding": 0,  "OneHanded": 25, "TwoHanded": 20, "Polearm": 25, "Bow": 5,  "Crossbow": 5,  "Throwing": 15},
    2: {"Athletics": 50, "Riding": 10, "OneHanded": 50, "TwoHanded": 45, "Polearm": 50, "Bow": 10, "Crossbow": 10, "Throwing": 25},
    3: {"Athletics": 70, "Riding": 30, "OneHanded": 75, "TwoHanded": 70, "Polearm": 75, "Bow": 15, "Crossbow": 15, "Throwing": 40},
    4: {"Athletics": 90, "Riding": 50, "OneHanded": 105, "TwoHanded": 100, "Polearm": 105, "Bow": 20, "Crossbow": 20, "Throwing": 55},
    5: {"Athletics": 115, "Riding": 70, "OneHanded": 140, "TwoHanded": 135, "Polearm": 140, "Bow": 25, "Crossbow": 25, "Throwing": 70},
}


# Build all 13 new orc-race troop XML blocks.
NEW_TROOPS_XML = "".join([
    # ---------- ORC MELEE LINE (Grunt T1 -> Reaver T5) ----------
    _new_orc_npc(
        "isengard_orc_grunt", "Orc Grunt", level=1,
        skills=_ORC_SKILLS[1],
        is_basic=True,
        equipment=[
            ("Item0",  "isengard_1h_sword_b"),
            ("Head",   "sk_gn_orc_helmet_light_a"),
            ("Body",   "sk_md_orc_arc_chest_light_a"),
            ("Gloves", "sk_md_orc_bracer_light_a"),
            ("Leg",    "sk_md_orc_boots_a"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_brawler", "Orc Brawler", level=6,
        skills=_ORC_SKILLS[2],
        equipment=[
            ("Item0",  "isengard_1h_sword_b"),
            ("Item1",  "wm_isengard_shield_a04"),
            ("Head",   "sk_gn_orc_helmet_light_b"),
            ("Body",   "sk_md_orc_arc_chest_light_b"),
            ("Cape",   "sk_md_orc_pauldron_light_a"),
            ("Gloves", "sk_md_orc_bracer_light_a"),
            ("Leg",    "sk_md_orc_boots_light_a"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_warrior", "Orc Warrior", level=11,
        skills=_ORC_SKILLS[3],
        equipment=[
            ("Item0",  "isengard_1h_sword_b"),
            ("Item1",  "wm_isengard_shield_a03"),
            ("Head",   "sk_is_orc_inf_helmet_med_a"),
            ("Body",   "sk_md_orc_inf_chest_med_a"),
            ("Cape",   "sk_md_orc_pauldron_light_b"),
            ("Gloves", "sk_md_orc_bracer_light_b"),
            ("Leg",    "sk_md_orc_boots_light_b"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_marauder", "Orc Marauder", level=16,
        skills=_ORC_SKILLS[4],
        equipment=[
            ("Item0",  "isengard_1h_sword_a"),
            ("Item1",  "wm_isengard_shield_a02"),
            ("Head",   "sk_is_orc_vgd_helmet_med_a"),
            ("Body",   "sk_md_orc_inf_chest_med_c"),
            ("Cape",   "sk_md_orc_pauldron_med_a"),
            ("Gloves", "sk_md_orc_bracer_med_a"),
            ("Leg",    "sk_md_orc_boots_med_a"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_reaver", "Orc Reaver", level=21,
        skills=_ORC_SKILLS[5],
        equipment=[
            ("Item0",  "isengard_1h_sword_a"),
            ("Item1",  "wm_isengard_shield_a01"),
            ("Head",   "sk_is_orc_inf_helmet_heavy_a"),
            ("Body",   "sk_md_orc_inf_chest_heavy_b"),
            ("Cape",   "sk_md_orc_pauldron_med_b"),
            ("Gloves", "sk_md_orc_bracer_med_b"),
            ("Leg",    "sk_md_orc_boots_med_b"),
        ],
    ),
    # ---------- ORC SHOCK LINE (Berserker T2 -> Slayer T5) ----------
    _new_orc_npc(
        "isengard_orc_berserker", "Orc Berserker", level=6,
        skills={**_ORC_SKILLS[2], "TwoHanded": 60, "OneHanded": 35},
        equipment=[
            ("Item0",  "isengard_2h_axe_b"),
            ("Head",   "sk_gn_orc_brt_helmet_light_a"),
            ("Body",   "sk_md_orc_arc_chest_light_a"),
            ("Gloves", "sk_md_orc_bracer_light_a"),
            ("Leg",    "sk_md_orc_boots_light_a"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_ravager", "Orc Ravager", level=11,
        skills={**_ORC_SKILLS[3], "TwoHanded": 90, "OneHanded": 55},
        equipment=[
            ("Item0",  "isengard_2h_axe_b"),
            ("Head",   "sk_gn_orc_mrd_helmet_light_a"),
            ("Body",   "sk_md_orc_arc_chest_med_a"),
            ("Cape",   "sk_md_orc_pauldron_light_a"),
            ("Gloves", "sk_md_orc_bracer_light_b"),
            ("Leg",    "sk_md_orc_boots_light_a"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_butcher", "Orc Butcher", level=16,
        skills={**_ORC_SKILLS[4], "TwoHanded": 130, "OneHanded": 80},
        equipment=[
            ("Item0",  "isengard_2h_axe_a"),
            ("Head",   "sk_is_orc_brt_helmet_med_a"),
            ("Body",   "sk_md_orc_arc_chest_med_c"),
            ("Cape",   "sk_md_orc_pauldron_light_b"),
            ("Gloves", "sk_md_orc_bracer_med_a"),
            ("Leg",    "sk_md_orc_boots_med_a"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_slayer", "Orc Slayer", level=21,
        skills={**_ORC_SKILLS[5], "TwoHanded": 175, "OneHanded": 110},
        equipment=[
            ("Item0",  "isengard_2h_axe_a"),
            ("Head",   "sk_is_orc_brt_helmet_heavy_a"),
            ("Body",   "sk_md_orc_inf_chest_heavy_a"),
            ("Cape",   "sk_md_orc_pauldron_med_a"),
            ("Gloves", "sk_md_orc_bracer_med_a"),
            ("Leg",    "sk_md_orc_boots_med_b"),
        ],
    ),
    # ---------- ORC WARG LINE v2 (Scout T2 -> Ravager T5) ----------
    _new_orc_npc(
        "isengard_orc_warg_scout_v2", "Orc Warg Scout", level=6,
        skills={**_ORC_SKILLS[2], "Riding": 45, "Polearm": 60},
        default_group="Cavalry",
        equipment=[
            ("Item0",        "isengard_spear_a"),
            ("Item1",        "isengard_1h_sword_b"),
            ("Item2",        "wm_isengard_shield_a04"),
            ("Head",         "sk_gn_orc_mrd_helmet_light_a"),
            ("Body",         "sk_md_orc_arc_chest_light_a"),
            ("Gloves",       "sk_md_orc_bracer_light_a"),
            ("Leg",          "sk_md_orc_boots_light_a"),
            ("Horse",        "warg_brown"),
            ("HorseHarness", "warg_saddle"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_warg_rider_v2", "Orc Warg Rider", level=11,
        skills={**_ORC_SKILLS[3], "Riding": 70, "Polearm": 90},
        default_group="Cavalry",
        equipment=[
            ("Item0",        "isengard_spear_a"),
            ("Item1",        "isengard_1h_sword_b"),
            ("Item2",        "wm_isengard_shield_a03"),
            ("Head",         "sk_is_orc_mrd_helmet_med_a"),
            ("Body",         "sk_md_orc_arc_chest_med_a"),
            ("Cape",         "sk_md_orc_pauldron_light_b"),
            ("Gloves",       "sk_md_orc_bracer_light_b"),
            ("Leg",          "sk_md_orc_boots_med_a"),
            ("Horse",        "warg_brown"),
            ("HorseHarness", "warg_saddle"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_warg_raider_v2", "Orc Warg Raider", level=16,
        skills={**_ORC_SKILLS[4], "Riding": 95, "Polearm": 120},
        default_group="Cavalry",
        equipment=[
            ("Item0",        "isengard_spear_a"),
            ("Item1",        "isengard_1h_sword_a"),
            ("Item2",        "wm_isengard_shield_a02"),
            ("Head",         "sk_is_orc_sly_helmet_med_a"),
            ("Body",         "sk_md_orc_inf_chainmail_med_a"),
            ("Cape",         "sk_md_orc_pauldron_med_a"),
            ("Gloves",       "sk_md_orc_bracer_med_a"),
            ("Leg",          "sk_md_orc_boots_med_b"),
            ("Horse",        "warg_dark"),
            ("HorseHarness", "warg_saddle"),
        ],
    ),
    _new_orc_npc(
        "isengard_orc_warg_ravager_v2", "Orc Warg Ravager", level=21,
        skills={**_ORC_SKILLS[5], "Riding": 125, "Polearm": 155},
        default_group="Cavalry",
        equipment=[
            ("Item0",        "isengard_spear_b"),
            ("Item1",        "isengard_1h_sword_a"),
            ("Item2",        "wm_isengard_shield_a01"),
            ("Head",         "sk_is_orc_sly_helmet_heavy_a"),
            ("Body",         "sk_md_orc_inf_chest_heavy_a"),
            ("Cape",         "sk_md_orc_pauldron_med_b"),
            ("Gloves",       "sk_md_orc_bracer_med_b"),
            ("Leg",          "sk_md_orc_boots_med_b"),
            ("Horse",        "warg_albino"),
            ("HorseHarness", "warg_saddle"),
        ],
    ),
])


# -----------------------------------------------------------------
# Patch helpers
# -----------------------------------------------------------------

def find_npc_block(content: str, troop_id: str) -> Tuple[int, int]:
    """Locate the full <NPCCharacter ... id="..."> ... </NPCCharacter> block."""
    pattern = re.compile(
        r'    <NPCCharacter\s+id="' + re.escape(troop_id) + r'"'
        r'.*?'
        r'    </NPCCharacter>',
        re.DOTALL,
    )
    m = pattern.search(content)
    if not m:
        return -1, -1
    end = m.end()
    if content[end:end+2] == '\n\n':
        end += 2
    elif content[end:end+1] == '\n':
        end += 1
    return m.start(), end


# Replace the entire <Equipments>...</Equipments> block (drops all variant
# rosters in favor of the single canonical roster from the EQUIPMENT table).
EQUIPMENTS_BLOCK_RE = re.compile(
    r'(\s+)<Equipments>.*?</Equipments>',
    re.DOTALL,
)


def build_equipments_xml(slots: List[Tuple[str, str]]) -> str:
    """Build <Equipments>...</Equipments> with a single canonical roster."""
    inner_indent = "                "  # 16 spaces (matches existing file)
    lines = []
    for slot, item_id in slots:
        lines.append(
            f'{inner_indent}<equipment\n{inner_indent}    slot="{slot}"\n'
            f'{inner_indent}    id="Item.{item_id}" />'
        )
    roster = "\n".join(lines)
    return (
        '\n        <Equipments>\n'
        '            <EquipmentRoster>\n'
        f'{roster}\n'
        '            </EquipmentRoster>\n'
        '        </Equipments>'
    )


def replace_equipment(npc_block: str, slots: List[Tuple[str, str]]) -> str:
    """Replace the entire Equipments block with a single canonical roster."""
    m = EQUIPMENTS_BLOCK_RE.search(npc_block)
    if not m:
        raise RuntimeError("No <Equipments> block found")
    new_block = build_equipments_xml(slots)
    return npc_block[:m.start()] + new_block + npc_block[m.end():]


NAME_RE = re.compile(r'(name="\{=[^}]+\})[^"]*(")')
LEVEL_RE = re.compile(r'(level=")[^"]*(")')


def update_name_and_level(npc_block: str, new_name: str, new_level: str) -> str:
    npc_block = NAME_RE.sub(rf'\g<1>[Isengard] {new_name}\g<2>', npc_block, count=1)
    npc_block = LEVEL_RE.sub(rf'\g<1>{new_level}\g<2>', npc_block, count=1)
    return npc_block


# Match both self-closing and open-form upgrade_targets.
UPGRADE_BLOCK_RE = re.compile(
    r'<upgrade_targets\s*/>|<upgrade_targets>.*?</upgrade_targets>',
    re.DOTALL,
)


def update_upgrade_targets(npc_block: str, targets: List[str]) -> str:
    if not targets:
        new_block = '<upgrade_targets />'
    else:
        # Match file's 4-space-indented multi-line format
        lines = '\n'.join(
            f'            <upgrade_target\n                id="NPCCharacter.{t}" />'
            for t in targets
        )
        new_block = f'<upgrade_targets>\n{lines}\n        </upgrade_targets>'
    return UPGRADE_BLOCK_RE.sub(new_block, npc_block, count=1)


# -----------------------------------------------------------------
# Main apply
# -----------------------------------------------------------------

def apply(dry_run: bool = False):
    if not os.path.exists(TROOPS_FILE):
        print(f"ERROR: {TROOPS_FILE} not found", file=sys.stderr)
        sys.exit(1)

    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        content = f.read()
    orig_len = len(content)

    # 1. Apply equipment + name + level + upgrade_targets to existing troops.
    refit = 0
    renamed = 0
    rewired = 0
    missing: List[str] = []
    # Use union of all IDs we touch on existing troops.
    touch_ids = set(EQUIPMENT.keys()) | set(RENAMES.keys()) | set(UPGRADE_TARGETS.keys())
    # New orc-race troops aren't in the file yet — skip from this pass.
    new_ids = {
        "isengard_orc_grunt", "isengard_orc_brawler", "isengard_orc_warrior",
        "isengard_orc_marauder", "isengard_orc_reaver",
        "isengard_orc_berserker", "isengard_orc_ravager", "isengard_orc_butcher",
        "isengard_orc_slayer",
        "isengard_orc_warg_scout_v2", "isengard_orc_warg_rider_v2",
        "isengard_orc_warg_raider_v2", "isengard_orc_warg_ravager_v2",
    }
    touch_ids -= new_ids

    for troop_id in sorted(touch_ids):
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            missing.append(troop_id)
            continue
        npc_block = content[start:end]
        if troop_id in EQUIPMENT:
            npc_block = replace_equipment(npc_block, EQUIPMENT[troop_id])
            refit += 1
        if troop_id in RENAMES:
            npc_block = update_name_and_level(
                npc_block,
                RENAMES[troop_id]["name"],
                RENAMES[troop_id]["level"],
            )
            renamed += 1
        if troop_id in UPGRADE_TARGETS:
            npc_block = update_upgrade_targets(npc_block, UPGRADE_TARGETS[troop_id])
            rewired += 1
        content = content[:start] + npc_block + content[end:]

    # 2. Add the 13 NEW orc-race troops (idempotent — skip if already present).
    added = 0
    new_xml_to_inject = ""
    for nid in new_ids:
        if f'id="{nid}"' not in content:
            # Pull just this troop's block out of NEW_TROOPS_XML.
            pat = re.compile(
                r'\n    <NPCCharacter\s+id="' + re.escape(nid) + r'".*?</NPCCharacter>\n',
                re.DOTALL,
            )
            mm = pat.search(NEW_TROOPS_XML)
            if mm:
                new_xml_to_inject += mm.group(0)
                added += 1
    if new_xml_to_inject:
        # Insert before final </NPCCharacters>
        content = content.replace(
            '</NPCCharacters>',
            new_xml_to_inject.rstrip() + '\n\n</NPCCharacters>',
            1,
        )

    # 2b. After insertion, apply upgrade_target rewires for the new orc-race troops.
    orc_rewired = 0
    for nid in new_ids:
        if nid not in UPGRADE_TARGETS:
            continue
        start, end = find_npc_block(content, nid)
        if start < 0:
            print(f"  WARN: new troop {nid} not found after injection", file=sys.stderr)
            continue
        npc_block = content[start:end]
        npc_block = update_upgrade_targets(npc_block, UPGRADE_TARGETS[nid])
        content = content[:start] + npc_block + content[end:]
        orc_rewired += 1

    # 3. Delete redundant troops (none for Isengard per user direction).
    deleted = 0
    for troop_id in DELETE_IDS:
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            print(f"  WARN: delete target {troop_id} not found", file=sys.stderr)
            continue
        content = content[:start] + content[end:]
        deleted += 1

    # 4. Remove orphan upgrade_target refs to deleted troops.
    removed_refs = 0
    for troop_id in DELETE_IDS:
        pattern = re.compile(
            r'\s+<upgrade_target\s+id="NPCCharacter\.' + re.escape(troop_id) + r'"\s*/>'
        )
        content, n = pattern.subn('', content)
        removed_refs += n

    print(f"\nIsengard troop revamp:")
    print(f"  Equipment refits:     {refit}/{len(EQUIPMENT)}")
    print(f"  Renames/level updates:{renamed}/{len(RENAMES)}")
    print(f"  Upgrade rewires:      {rewired}/{len(UPGRADE_TARGETS) - len(new_ids)}")
    print(f"  Orc chain rewires:    {orc_rewired}/{len(new_ids)}")
    print(f"  New troops added:     {added}/{len(new_ids)}")
    print(f"  Troops deleted:       {deleted}/{len(DELETE_IDS)}")
    print(f"  Orphan refs removed:  {removed_refs}")
    print(f"  File size: {orig_len:,} -> {len(content):,} bytes")
    if missing:
        print(f"  MISSING (no NPCCharacter found): {missing}")

    if dry_run:
        print("\n(dry-run — no file written)")
        return

    with open(TROOPS_FILE, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"\nWrote: {TROOPS_FILE}")


def main():
    p = argparse.ArgumentParser(description="Apply Isengard troop revamp (issue #212)")
    p.add_argument("--dry-run", action="store_true")
    p.add_argument("--apply", action="store_true")
    args = p.parse_args()
    if args.dry_run:
        apply(dry_run=True)
    elif args.apply:
        apply(dry_run=False)
    else:
        p.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
