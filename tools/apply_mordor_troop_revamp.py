#!/usr/bin/env python3
"""Apply KEYforce Mordor troop tree revamp per spec.

Issue: #212
Source-of-truth: E:\\repos\\lotraom-assets\\tools\\mordor_armor_and_troops.txt

Per spec, Mordor has THREE unit groups:
  1. Mordor Orc Line (T1-T5, race="orc"):
       Recruit -> Lackey -> Fighter -> Warrior -> Infantry
                                    -> Impaler -> Reaver
                          -> Hunter -> Scout -> Archer
  2. Nurn Warg Riders (T1-T6, race="orc"):
       Tamer -> Rider -> Raider -> Reaver -> Ravager -> Beast Master
  3. Black Uruk Line (T2-T7, race="uruk"):
       Grunt -> Fighter -> Warrior -> Infantry -> Vanguard -> Captain
                                   -> Shieldbearer -> Shield Guard -> Barad-Dur Guard
                       -> Skirmisher -> Archer -> Heavy Archer
                                     -> Crossbow -> Heavy Crossbow

Strategy: keep existing Black Uruk IDs (refit only); DELETE 13 redundant troops
plus mordor_black_numenorean (per user 2026-05-23); ADD 21 new troops covering
the new Orc + Warg + Black Uruk additions.

Race assignment (per user 2026-05-23):
- All Mordor orcs (recruit, lackey, fighter, warrior, infantry, impaler, reaver,
  hunter, scout, archer) use race="orc"
- All Nurn Warg Riders use race="orc"
- All Black Uruk troops use race="uruk"

Usage:
    python tools/apply_mordor_troop_revamp.py --dry-run
    python tools/apply_mordor_troop_revamp.py --apply
"""
import argparse
import os
import re
import sys
from typing import Dict, List, Tuple

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_mordor.xml"
)

# 14 deletions: old uruk extras + old orc stubs + black_numenorean (per user 2026-05-23)
DELETE_IDS = {
    # Old uruk extras (replaced by spec Black Uruk tree)
    "mordor_uruk_feller",
    "mordor_uruk_ravager",
    "mordor_uruk_executioner",
    "mordor_uruk_darkblade",
    "mordor_uruk_halberdier",
    "mordor_uruk_reaper",
    "mordor_uruk_deathwarden",
    "mordor_uruk_marksman",
    "mordor_uruk_raider",
    "mordor_uruk_scout",
    # Old orc stubs (canonical names reused by spec orc tree as NEW troops)
    "mordor_orc_warrior",
    "mordor_orc_warbrute",
    "mordor_orc_blackguard",
    # User added (2026-05-23)
    "mordor_black_numenorean",
}

# Display name + level updates for kept Black Uruk troops (refit in-place)
RENAMES: Dict[str, Dict[str, str]] = {
    "mordor_uruk_grunt":         {"name": "Black Uruk Grunt",            "level": "11"},
    "mordor_uruk_warrior":       {"name": "Black Uruk Warrior",          "level": "21"},
    "mordor_uruk_infantry":      {"name": "Black Uruk Infantry",         "level": "26"},
    "mordor_uruk_vanguard":      {"name": "Black Uruk Vanguard",         "level": "31"},
    "mordor_uruk_captain":       {"name": "Black Uruk Captain",          "level": "36"},
    "mordor_uruk_shieldbearer":  {"name": "Black Uruk Shieldbearer",     "level": "26"},
    "mordor_uruk_shieldguard":   {"name": "Black Uruk Shield Guard",     "level": "31"},
    "mordor_uruk_baraddurguard": {"name": "Black Uruk Barad-Dur Guard",  "level": "36"},
    "mordor_uruk_archer":        {"name": "Black Uruk Archer",           "level": "26"},
}

# Upgrade chain rewires per spec tree (kept-existing troops only;
# NEW troops carry their own upgrade_targets in NEW_TROOPS_XML)
UPGRADE_TARGETS: Dict[str, List[str]] = {
    # Black Uruk infantry / shield branch (kept IDs)
    "mordor_uruk_grunt":         ["mordor_uruk_fighter"],
    "mordor_uruk_warrior":       ["mordor_uruk_infantry", "mordor_uruk_shieldbearer"],
    "mordor_uruk_infantry":      ["mordor_uruk_vanguard"],
    "mordor_uruk_vanguard":      ["mordor_uruk_captain"],
    "mordor_uruk_captain":       [],
    "mordor_uruk_shieldbearer":  ["mordor_uruk_shieldguard"],
    "mordor_uruk_shieldguard":   ["mordor_uruk_baraddurguard"],
    "mordor_uruk_baraddurguard": [],
    # Black Uruk archer (kept ID)
    "mordor_uruk_archer":        ["mordor_uruk_heavy_archer"],
}

# Per-troop equipment replacement for KEPT Black Uruk troops
# (NEW troops carry their own equipment in NEW_TROOPS_XML)
EQUIPMENT: Dict[str, List[Tuple[str, str]]] = {
    # ---------- BLACK URUK INFANTRY LINE (kept IDs) ----------
    "mordor_uruk_grunt": [
        ("Item0",  "sm_uruk_sword_a"),
        ("Item1",  "sm_mordor_shield_small_a"),
        ("Head",   "sk_uruk_mordor_helmet_medium_a1"),
        ("Body",   "sk_uruk_mordor_chainmail_light_a"),
        ("Cape",   "sk_uruk_mordor_pauldron_medium_a"),
        ("Gloves", "sk_uruk_mordor_bracer_medium_a"),
        ("Leg",    "sk_uruk_mordor_boots_a"),
    ],
    "mordor_uruk_warrior": [
        ("Item0",  "sm_uruk_sword_c"),
        ("Item1",  "sm_mordor_shield_small_a"),
        ("Head",   "sk_uruk_mordor_helmet_heavy_a1"),
        ("Body",   "sk_uruk_mordor_chainmail_medium_a1"),
        ("Cape",   "sk_uruk_mordor_pauldron_medium_b"),
        ("Gloves", "sk_uruk_mordor_bracer_medium_b"),
        ("Leg",    "sk_uruk_mordor_greaves_medium_a"),
    ],
    "mordor_uruk_infantry": [
        ("Item0",  "sm_uruk_sword_d"),
        ("Item1",  "sm_mordor_shield_small_a"),
        ("Head",   "sk_uruk_mordor_helmet_heavy_a2"),
        ("Body",   "sk_uruk_mordor_chainmail_heavy_a1"),
        ("Cape",   "sk_uruk_mordor_pauldron_heavy_a"),
        ("Gloves", "sk_uruk_mordor_bracer_heavy_a"),
        ("Leg",    "sk_uruk_mordor_greaves_medium_b"),
    ],
    "mordor_uruk_vanguard": [
        ("Item0",  "sm_uruk_sword_e"),
        ("Item1",  "sm_mordor_shield_mid_a"),
        ("Item2",  "isengard_berserker_sword_2h"),
        ("Head",   "sk_uruk_mordor_helmet_elite_a1"),
        ("Body",   "sk_uruk_mordor_chainmail_heavy_a2"),
        ("Cape",   "sk_uruk_mordor_pauldron_heavy_b"),
        ("Gloves", "sk_uruk_mordor_bracer_heavy_b"),
        ("Leg",    "sk_uruk_mordor_greaves_heavy_a"),
    ],
    "mordor_uruk_captain": [
        ("Item0",  "sm_uruk_sword_g"),
        ("Item1",  "sm_mordor_shield_mid_a"),
        ("Item2",  "isengard_berserker_sword_2h"),
        ("Head",   "sk_uruk_mordor_helmet_elite_a2"),
        ("Body",   "sk_uruk_mordor_chainmail_elite_a1"),
        ("Cape",   "sk_uruk_mordor_pauldron_heavy_c"),
        ("Gloves", "sk_uruk_mordor_bracer_heavy_c"),
        ("Leg",    "sk_uruk_mordor_greaves_heavy_b"),
    ],
    # ---------- BLACK URUK SHIELD LINE (kept IDs) ----------
    "mordor_uruk_shieldbearer": [
        ("Item0",  "isengard_spear_a"),
        ("Item1",  "sm_mordor_shield_tower_a"),
        ("Head",   "sk_uruk_mordor_helmet_heavy_b1"),
        ("Body",   "sk_uruk_mordor_chainmail_heavy_b1"),
        ("Cape",   "sk_uruk_mordor_pauldron_heavy_a"),
        ("Gloves", "sk_uruk_mordor_bracer_heavy_a"),
        ("Leg",    "sk_uruk_mordor_greaves_medium_c"),
    ],
    "mordor_uruk_shieldguard": [
        ("Item0",  "isengard_spear_b"),
        ("Item1",  "sm_mordor_shield_tower_a"),
        ("Item2",  "sm_uruk_halberd_a"),
        ("Head",   "sk_uruk_mordor_helmet_heavy_b2"),
        ("Body",   "sk_uruk_mordor_chainmail_heavy_b2"),
        ("Cape",   "sk_uruk_mordor_pauldron_heavy_b"),
        ("Gloves", "sk_uruk_mordor_bracer_heavy_b"),
        ("Leg",    "sk_uruk_mordor_greaves_heavy_c"),
    ],
    "mordor_uruk_baraddurguard": [
        ("Item0",  "isengard_spear_b"),
        ("Item1",  "sm_mordor_shield_tower_b"),
        ("Item2",  "sm_uruk_halberd_b"),
        ("Head",   "sk_uruk_mordor_helmet_elite_b1"),
        ("Body",   "sk_uruk_mordor_chainmail_elite_b1"),
        ("Cape",   "sk_uruk_mordor_pauldron_heavy_c"),
        ("Gloves", "sk_uruk_mordor_bracer_heavy_c"),
        ("Leg",    "sk_uruk_mordor_greaves_heavy_d"),
    ],
    # ---------- BLACK URUK ARCHER (kept ID) ----------
    "mordor_uruk_archer": [
        ("Item0",  "sm_uruk_bow_a"),
        ("Item1",  "barbed_arrows"),
        ("Item2",  "isengard_berserker_sword_2h"),
        ("Head",   "sk_uruk_mordor_helmet_heavy_c1"),
        ("Body",   "sk_uruk_mordor_chainmail_heavy_c1"),
        ("Cape",   "sk_uruk_mordor_pauldron_heavy_e"),
        ("Gloves", "sk_uruk_mordor_bracer_medium_c"),
        ("Leg",    "sk_uruk_mordor_greaves_heavy_e"),
    ],
}

# 21 NEW troops:
#   - 10 Mordor Orc (race="orc")
#   - 6 Nurn Warg Riders (race="orc")
#   - 5 new Black Uruk (race="uruk"): fighter, skirmisher, heavy_archer, crossbow, heavy_crossbow
NEW_TROOPS_XML = """
  <!--Mordor Orc Line (T1-T5)-->
  <NPCCharacter
      id="mordor_orc_recruit"
      race="orc"
      default_group="Infantry"
      level="1"
      name="{=aom_mordor_orc_recruit_name}[Mordor] Orc Recruit"
      occupation="Soldier"
      is_basic_troop="true"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="20" />
      <skill id="OneHanded" value="20" />
      <skill id="TwoHanded" value="15" />
      <skill id="Polearm" value="15" />
      <skill id="Bow" value="10" />
      <skill id="Throwing" value="10" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_orc_lackey" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_sword_a01" />
        <equipment slot="Head" id="Item.sk_gn_orc_helmet_light_a" />
        <equipment slot="Body" id="Item.sk_md_orc_arc_chest_light_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_lackey"
      race="orc"
      default_group="Infantry"
      level="6"
      name="{=aom_mordor_orc_lackey_name}[Mordor] Orc Lackey"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="35" />
      <skill id="OneHanded" value="35" />
      <skill id="TwoHanded" value="25" />
      <skill id="Polearm" value="25" />
      <skill id="Bow" value="20" />
      <skill id="Throwing" value="20" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_orc_fighter" />
      <upgrade_target id="NPCCharacter.mordor_orc_hunter" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_axe_a02" />
        <equipment slot="Item1" id="Item.sm_mordor_shield_small_a" />
        <equipment slot="Head" id="Item.sk_gn_orc_helmet_light_b" />
        <equipment slot="Body" id="Item.sk_md_orc_arc_chest_light_b" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_light_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_fighter"
      race="orc"
      default_group="Infantry"
      level="11"
      name="{=aom_mordor_orc_fighter_name}[Mordor] Orc Fighter"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="50" />
      <skill id="OneHanded" value="55" />
      <skill id="TwoHanded" value="40" />
      <skill id="Polearm" value="40" />
      <skill id="Throwing" value="25" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_orc_warrior" />
      <upgrade_target id="NPCCharacter.mordor_orc_impaler" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_sword_a02" />
        <equipment slot="Item1" id="Item.sm_mordor_shield_small_a" />
        <equipment slot="Head" id="Item.sk_gn_orc_inf_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chainmail_light_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_light_a" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_light_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_light_b" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_warrior"
      race="orc"
      default_group="Infantry"
      level="16"
      name="{=aom_mordor_orc_warrior_name}[Mordor] Orc Warrior"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="70" />
      <skill id="OneHanded" value="80" />
      <skill id="TwoHanded" value="60" />
      <skill id="Polearm" value="60" />
      <skill id="Throwing" value="30" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_orc_infantry" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_axe_a01" />
        <equipment slot="Item1" id="Item.sm_mordor_shield_small_b" />
        <equipment slot="Head" id="Item.sk_md_orc_inf_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_med_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_a" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_infantry"
      race="orc"
      default_group="Infantry"
      level="21"
      name="{=aom_mordor_orc_infantry_name}[Mordor] Orc Infantry"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="90" />
      <skill id="OneHanded" value="110" />
      <skill id="TwoHanded" value="80" />
      <skill id="Polearm" value="80" />
      <skill id="Throwing" value="40" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_sword_a02" />
        <equipment slot="Item1" id="Item.sm_mordor_shield_mid_a" />
        <equipment slot="Head" id="Item.sk_md_orc_inf_helmet_heavy_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_heavy_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_b" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_b" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_b" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_impaler"
      race="orc"
      default_group="Infantry"
      level="16"
      name="{=aom_mordor_orc_impaler_name}[Mordor] Orc Impaler"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="70" />
      <skill id="OneHanded" value="55" />
      <skill id="TwoHanded" value="85" />
      <skill id="Polearm" value="85" />
      <skill id="Throwing" value="30" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_orc_reaver" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_polearm_a02" />
        <equipment slot="Item1" id="Item.wm_mordor_set1_sword_a01" />
        <equipment slot="Head" id="Item.sk_gn_orc_vgd_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_med_b" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_a" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_reaver"
      race="orc"
      default_group="Infantry"
      level="21"
      name="{=aom_mordor_orc_reaver_name}[Mordor] Orc Reaver"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="90" />
      <skill id="OneHanded" value="70" />
      <skill id="TwoHanded" value="110" />
      <skill id="Polearm" value="110" />
      <skill id="Throwing" value="40" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_polearm_a01" />
        <equipment slot="Item1" id="Item.wm_mordor_set1_mace_a01" />
        <equipment slot="Head" id="Item.sk_md_orc_vgd_helmet_heavy_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_heavy_e" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_b" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_b" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_b" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_hunter"
      race="orc"
      default_group="Ranged"
      level="11"
      name="{=aom_mordor_orc_hunter_name}[Mordor] Orc Hunter"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="50" />
      <skill id="OneHanded" value="35" />
      <skill id="Bow" value="60" />
      <skill id="Throwing" value="25" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_orc_scout" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.hunting_bow" />
        <equipment slot="Item1" id="Item.barbed_arrows" />
        <equipment slot="Item2" id="Item.wm_mordor_set1_sword_a01" />
        <equipment slot="Head" id="Item.sk_gn_orc_arc_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_md_orc_arc_chest_light_c" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_light_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_light_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_scout"
      race="orc"
      default_group="Ranged"
      level="16"
      name="{=aom_mordor_orc_scout_name}[Mordor] Orc Scout"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="70" />
      <skill id="OneHanded" value="55" />
      <skill id="Bow" value="90" />
      <skill id="Throwing" value="35" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_orc_archer" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.hunting_bow" />
        <equipment slot="Item1" id="Item.barbed_arrows" />
        <equipment slot="Item2" id="Item.wm_mordor_set1_sword_a02" />
        <equipment slot="Head" id="Item.sk_gn_orc_sct_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_md_orc_arc_chest_med_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_light_b" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_light_b" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_orc_archer"
      race="orc"
      default_group="Ranged"
      level="21"
      name="{=aom_mordor_orc_archer_name}[Mordor] Orc Archer"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="90" />
      <skill id="OneHanded" value="70" />
      <skill id="Bow" value="120" />
      <skill id="Throwing" value="45" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.composite_bow" />
        <equipment slot="Item1" id="Item.bodkin_arrows_a" />
        <equipment slot="Item2" id="Item.wm_mordor_set1_sword_a02" />
        <equipment slot="Head" id="Item.sk_md_orc_arc_helmet_heavy_a" />
        <equipment slot="Body" id="Item.sk_md_orc_arc_chest_heavy_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_a" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_b" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <!--Nurn Warg Riders (T1-T6)-->
  <NPCCharacter
      id="mordor_warg_tamer"
      race="orc"
      default_group="Infantry"
      level="1"
      name="{=aom_mordor_warg_tamer_name}[Mordor] Nurn Warg Tamer"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="25" />
      <skill id="Riding" value="15" />
      <skill id="OneHanded" value="25" />
      <skill id="Polearm" value="15" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_warg_rider" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_sword_a01" />
        <equipment slot="Head" id="Item.sk_gn_orc_helmet_light_a" />
        <equipment slot="Body" id="Item.sk_md_orc_arc_chest_light_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_warg_rider"
      race="orc"
      default_group="Infantry"
      level="6"
      name="{=aom_mordor_warg_rider_name}[Mordor] Nurn Warg Rider"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="35" />
      <skill id="Riding" value="35" />
      <skill id="OneHanded" value="40" />
      <skill id="Polearm" value="25" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_warg_raider" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_axe_a02" />
        <equipment slot="Item1" id="Item.sm_mordor_shield_small_a" />
        <equipment slot="Head" id="Item.sk_gn_orc_helmet_light_b" />
        <equipment slot="Body" id="Item.sk_md_orc_arc_chest_med_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_light_a" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_light_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_light_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_warg_raider"
      race="orc"
      default_group="Cavalry"
      level="11"
      name="{=aom_mordor_warg_raider_name}[Mordor] Nurn Warg Raider"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="50" />
      <skill id="Riding" value="60" />
      <skill id="OneHanded" value="60" />
      <skill id="Polearm" value="40" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_warg_reaver" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_sword_a02" />
        <equipment slot="Head" id="Item.sk_md_orc_rdr_helmet_light_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chainmail_light_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_light_b" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_light_b" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_light_b" />
        <equipment slot="Horse" id="Item.warg_brown" />
        <equipment slot="HorseHarness" id="Item.warg_saddle" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_warg_reaver"
      race="orc"
      default_group="Cavalry"
      level="16"
      name="{=aom_mordor_warg_reaver_name}[Mordor] Nurn Warg Reaver"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="65" />
      <skill id="Riding" value="85" />
      <skill id="OneHanded" value="80" />
      <skill id="Polearm" value="70" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_warg_ravager" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_axe_a01" />
        <equipment slot="Item1" id="Item.wm_mordor_set1_polearm_a03" />
        <equipment slot="Head" id="Item.sk_md_orc_rdr_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chainmail_med_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_a" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_a" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_a" />
        <equipment slot="Horse" id="Item.warg_brown" />
        <equipment slot="HorseHarness" id="Item.warg_saddle" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_warg_ravager"
      race="orc"
      default_group="Cavalry"
      level="21"
      name="{=aom_mordor_warg_ravager_name}[Mordor] Nurn Warg Ravager"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="80" />
      <skill id="Riding" value="110" />
      <skill id="OneHanded" value="100" />
      <skill id="Polearm" value="100" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_warg_beastmaster" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_mace_a01" />
        <equipment slot="Item1" id="Item.wm_mordor_set1_polearm_a02" />
        <equipment slot="Head" id="Item.sk_md_orc_inf_helmet_heavy_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_heavy_a" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_b" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_b" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_b" />
        <equipment slot="Horse" id="Item.warg_dark" />
        <equipment slot="HorseHarness" id="Item.warg_saddle" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_warg_beastmaster"
      race="orc"
      default_group="Cavalry"
      level="26"
      name="{=aom_mordor_warg_beastmaster_name}[Mordor] Nurn Beast Master"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="95" />
      <skill id="Riding" value="140" />
      <skill id="OneHanded" value="125" />
      <skill id="Polearm" value="125" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_mordor_set1_sword_a02" />
        <equipment slot="Item1" id="Item.wm_mordor_set1_polearm_a01" />
        <equipment slot="Head" id="Item.sk_md_orc_brt_helmet_heavy_a" />
        <equipment slot="Body" id="Item.sk_md_orc_inf_chest_heavy_e" />
        <equipment slot="Cape" id="Item.sk_md_orc_pauldron_med_b" />
        <equipment slot="Gloves" id="Item.sk_md_orc_bracer_med_b" />
        <equipment slot="Leg" id="Item.sk_md_orc_boots_med_b" />
        <equipment slot="Horse" id="Item.warg_dark" />
        <equipment slot="HorseHarness" id="Item.warg_saddle" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <!--Black Uruk additions: Fighter T3, Skirmisher T4, Heavy Archer T6, Crossbow T5, Heavy Crossbow T6-->
  <NPCCharacter
      id="mordor_uruk_fighter"
      race="uruk"
      default_group="Infantry"
      level="16"
      name="{=aom_mordor_uruk_fighter_name}[Mordor] Black Uruk Fighter"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="70" />
      <skill id="OneHanded" value="80" />
      <skill id="TwoHanded" value="55" />
      <skill id="Polearm" value="55" />
      <skill id="Bow" value="40" />
      <skill id="Crossbow" value="30" />
      <skill id="Throwing" value="35" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_uruk_warrior" />
      <upgrade_target id="NPCCharacter.mordor_uruk_skirmisher" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.sm_uruk_sword_b" />
        <equipment slot="Item1" id="Item.sm_mordor_shield_small_a" />
        <equipment slot="Head" id="Item.sk_uruk_mordor_helmet_medium_b1" />
        <equipment slot="Body" id="Item.sk_uruk_mordor_chainmail_light_b" />
        <equipment slot="Cape" id="Item.sk_uruk_mordor_pauldron_medium_a" />
        <equipment slot="Gloves" id="Item.sk_uruk_mordor_bracer_medium_a" />
        <equipment slot="Leg" id="Item.sk_uruk_mordor_boots_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_uruk_skirmisher"
      race="uruk"
      default_group="Ranged"
      level="21"
      name="{=aom_mordor_uruk_skirmisher_name}[Mordor] Black Uruk Skirmisher"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="85" />
      <skill id="OneHanded" value="80" />
      <skill id="Bow" value="100" />
      <skill id="Crossbow" value="80" />
      <skill id="Throwing" value="50" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_uruk_archer" />
      <upgrade_target id="NPCCharacter.mordor_uruk_crossbow" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.sm_uruk_bow_starter" />
        <equipment slot="Item1" id="Item.barbed_arrows" />
        <equipment slot="Item2" id="Item.sm_uruk_sword_b" />
        <equipment slot="Head" id="Item.sk_uruk_mordor_helmet_heavy_c2" />
        <equipment slot="Body" id="Item.sk_uruk_mordor_chainmail_medium_c1" />
        <equipment slot="Cape" id="Item.sk_uruk_mordor_pauldron_medium_c" />
        <equipment slot="Gloves" id="Item.sk_uruk_mordor_bracer_medium_c" />
        <equipment slot="Leg" id="Item.sk_uruk_mordor_greaves_medium_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_uruk_heavy_archer"
      race="uruk"
      default_group="Ranged"
      level="31"
      name="{=aom_mordor_uruk_heavy_archer_name}[Mordor] Black Uruk Heavy Archer"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="110" />
      <skill id="OneHanded" value="90" />
      <skill id="TwoHanded" value="100" />
      <skill id="Bow" value="160" />
      <skill id="Throwing" value="55" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.sm_uruk_bow_a" />
        <equipment slot="Item1" id="Item.bodkin_arrows_a" />
        <equipment slot="Item2" id="Item.isengard_berserker_sword_2h" />
        <equipment slot="Head" id="Item.sk_uruk_mordor_helmet_elite_c1" />
        <equipment slot="Body" id="Item.sk_uruk_mordor_chainmail_elite_c1" />
        <equipment slot="Cape" id="Item.sk_uruk_mordor_pauldron_heavy_f" />
        <equipment slot="Gloves" id="Item.sk_uruk_mordor_bracer_heavy_d" />
        <equipment slot="Leg" id="Item.sk_uruk_mordor_greaves_heavy_e" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_uruk_crossbow"
      race="uruk"
      default_group="Ranged"
      level="26"
      name="{=aom_mordor_uruk_crossbow_name}[Mordor] Black Uruk Crossbow"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="95" />
      <skill id="OneHanded" value="90" />
      <skill id="Crossbow" value="130" />
      <skill id="Throwing" value="45" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.mordor_uruk_heavy_crossbow" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.crossbow_b" />
        <equipment slot="Item1" id="Item.bolt_a" />
        <equipment slot="Item2" id="Item.sm_uruk_sword_c" />
        <equipment slot="Head" id="Item.sk_uruk_mordor_helmet_heavy_d1" />
        <equipment slot="Body" id="Item.sk_uruk_mordor_chainmail_heavy_d1" />
        <equipment slot="Cape" id="Item.sk_uruk_mordor_pauldron_heavy_e" />
        <equipment slot="Gloves" id="Item.sk_uruk_mordor_bracer_medium_d" />
        <equipment slot="Leg" id="Item.sk_uruk_mordor_greaves_heavy_a" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>

  <NPCCharacter
      id="mordor_uruk_heavy_crossbow"
      race="uruk"
      default_group="Ranged"
      level="31"
      name="{=aom_mordor_uruk_heavy_crossbow_name}[Mordor] Black Uruk Heavy Crossbow"
      occupation="Soldier"
      culture="Culture.mordor">
    <face>
      <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
      <skill id="Athletics" value="110" />
      <skill id="OneHanded" value="100" />
      <skill id="Crossbow" value="170" />
      <skill id="Throwing" value="55" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.crossbow_c" />
        <equipment slot="Item1" id="Item.bolt_b" />
        <equipment slot="Item2" id="Item.sm_uruk_sword_d" />
        <equipment slot="Head" id="Item.sk_uruk_mordor_helmet_elite_d1" />
        <equipment slot="Body" id="Item.sk_uruk_mordor_chainmail_heavy_d2" />
        <equipment slot="Cape" id="Item.sk_uruk_mordor_pauldron_heavy_g" />
        <equipment slot="Gloves" id="Item.sk_uruk_mordor_bracer_heavy_e" />
        <equipment slot="Leg" id="Item.sk_uruk_mordor_greaves_heavy_f" />
      </EquipmentRoster>
    </Equipments>
  </NPCCharacter>
"""


# ---------- Shared helpers ----------

def find_npc_block(content: str, troop_id: str) -> Tuple[int, int]:
    """Find a single <NPCCharacter id="X">...</NPCCharacter> block, including the
    trailing newline(s) so deletion leaves clean spacing."""
    pattern = re.compile(
        r'( {2,4})<NPCCharacter\s+id="' + re.escape(troop_id) + r'"'
        r'.*?'
        r'\1</NPCCharacter>',
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


EQUIP_ROSTER_RE = re.compile(
    r'(\s+)<EquipmentRoster>(.*?)</EquipmentRoster>',
    re.DOTALL,
)


def build_equipment_xml(slots: List[Tuple[str, str]], indent: str) -> str:
    inner = "\n".join(
        f'{indent}    <equipment\n'
        f'{indent}        slot="{slot}"\n'
        f'{indent}        id="Item.{item_id}" />'
        for slot, item_id in slots
    )
    return f'{indent}<EquipmentRoster>\n{inner}\n{indent}</EquipmentRoster>'


def replace_equipment(npc_block: str, slots: List[Tuple[str, str]]) -> str:
    m = EQUIP_ROSTER_RE.search(npc_block)
    if not m:
        raise RuntimeError("No <EquipmentRoster> found")
    indent_after_newline = m.group(1).lstrip("\n") or "            "
    new_roster = build_equipment_xml(slots, indent_after_newline)
    return npc_block[:m.start()] + "\n" + new_roster + npc_block[m.end():]


NAME_RE = re.compile(r'(name="\{=[^}]+\})[^"]*(")')
LEVEL_RE = re.compile(r'(level=")[^"]*(")')


def update_name_and_level(npc_block: str, new_name: str, new_level: str) -> str:
    npc_block = NAME_RE.sub(rf'\g<1>[Mordor] {new_name}\g<2>', npc_block, count=1)
    npc_block = LEVEL_RE.sub(rf'\g<1>{new_level}\g<2>', npc_block, count=1)
    return npc_block


UPGRADE_BLOCK_RE = re.compile(
    r'<upgrade_targets\s*/?>'
    r'(?:.*?</upgrade_targets>)?',
    re.DOTALL,
)


def update_upgrade_targets(npc_block: str, targets: List[str], indent: str = "        ") -> str:
    if not targets:
        new_block = '<upgrade_targets />'
    else:
        lines = '\n'.join(f'{indent}    <upgrade_target id="NPCCharacter.{t}" />' for t in targets)
        new_block = f'<upgrade_targets>\n{lines}\n{indent}</upgrade_targets>'
    return UPGRADE_BLOCK_RE.sub(new_block, npc_block, count=1)


def apply(dry_run: bool = False):
    if not os.path.exists(TROOPS_FILE):
        print(f"ERROR: {TROOPS_FILE} not found", file=sys.stderr)
        sys.exit(1)

    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        content = f.read()
    orig_len = len(content)

    # 1. Apply equipment + name + level + upgrade_targets to existing kept troops
    refit = 0
    missing = []
    for troop_id, slots in EQUIPMENT.items():
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            missing.append(troop_id)
            continue
        npc_block = content[start:end]
        npc_block = replace_equipment(npc_block, slots)
        if troop_id in RENAMES:
            npc_block = update_name_and_level(
                npc_block,
                RENAMES[troop_id]["name"],
                RENAMES[troop_id]["level"],
            )
        if troop_id in UPGRADE_TARGETS:
            npc_block = update_upgrade_targets(npc_block, UPGRADE_TARGETS[troop_id])
        content = content[:start] + npc_block + content[end:]
        refit += 1

    # 2. Delete redundant troops FIRST (some old stub IDs collide with new spec IDs
    # like mordor_orc_warrior — must delete before inserting new versions)
    deleted = 0
    for troop_id in DELETE_IDS:
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            print(f"  WARN: delete target {troop_id} not found", file=sys.stderr)
            continue
        content = content[:start] + content[end:]
        deleted += 1

    # 3. Remove orphan upgrade_target refs to deleted troops.
    # The file uses both styles: single-line `<upgrade_target id="..." />` AND
    # multi-line `<upgrade_target\n    id="..." />`. Pattern handles both.
    removed_refs = 0
    for troop_id in DELETE_IDS:
        pattern = re.compile(
            r'\s*<upgrade_target\s+id="NPCCharacter\.'
            + re.escape(troop_id)
            + r'"\s*/>',
            re.DOTALL,
        )
        content, n = pattern.subn('', content)
        removed_refs += n

    # 4. Add new troops — insert before </NPCCharacters>
    added = 0
    new_block = NEW_TROOPS_XML.rstrip()
    # Idempotency: scan each NEW NPCCharacter id and only insert ones not present
    new_ids = re.findall(r'<NPCCharacter\s+id="([^"]+)"', new_block)
    missing_new_ids = [tid for tid in new_ids if f'id="{tid}"' not in content]
    if missing_new_ids:
        # If all are missing, insert whole block; otherwise insert per-id blocks
        if len(missing_new_ids) == len(new_ids):
            content = content.replace(
                '</NPCCharacters>',
                new_block + '\n</NPCCharacters>',
                1
            )
            added = len(new_ids)
        else:
            # Partial — insert only the missing ones individually
            for tid in missing_new_ids:
                m = re.search(
                    r'(  <NPCCharacter\s+id="' + re.escape(tid) + r'".*?</NPCCharacter>)',
                    new_block,
                    re.DOTALL,
                )
                if m:
                    content = content.replace(
                        '</NPCCharacters>',
                        '\n' + m.group(1) + '\n</NPCCharacters>',
                        1
                    )
                    added += 1

    print(f"\nMordor troop revamp:")
    print(f"  Equipment refits:     {refit}/{len(EQUIPMENT)}")
    print(f"  New troops added:     {added}/{len(new_ids)}")
    print(f"  Troops deleted:       {deleted}/{len(DELETE_IDS)}")
    print(f"  Upgrade refs removed: {removed_refs}")
    print(f"  File size: {orig_len:,} -> {len(content):,} bytes")
    if missing:
        print(f"  MISSING (no NPCCharacter found): {missing}")

    if dry_run:
        print("\n(dry-run -- no file written)")
        return

    with open(TROOPS_FILE, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"\nWrote: {TROOPS_FILE}")


def main():
    p = argparse.ArgumentParser(description="Apply Mordor troop revamp (issue #212)")
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
