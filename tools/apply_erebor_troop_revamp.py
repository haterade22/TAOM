#!/usr/bin/env python3
"""Apply KEYforce Erebor / Iron Hills / Ironpass troop revamp per spec.

Issue: #212
Source-of-truth: E:\\repos\\lotraom-assets\\tools\\erebor_armors_and_troops.txt

Per spec, Erebor culture has THREE unit groups:
  Erebor Regular (T2-T6):    Miner > Militia > {Skirmisher > Bowman} / {Company > Fighter > {Mattock Warrior, Warrior}}
  Erebor Noble   (T3-T9):    Noble > {Ranger > Archer > Veteran Archer} / {Longbeard > Infantry > {Guard > Shield-Guard > Gate Warden > Royal Warden, Axe-Guard > Veteran Axe-Guard > Shield-Breaker}}
  Erebor Oathsworn (T7-T9):  Oathsworn > Legionary > Royal Legionary

Plus two Iron Hills groups:
  Iron Hills Regular (T2-T6): Recruit > Militia > {Skirmisher > Bowman} / {Company > Fighter > {Axe Warrior, Warrior}}
  Iron Hills Noble (T3-T9):   Noble > {Scout > Sharpshooter > Veteran Sharpshooter} / {Swordsman > Infantry > {Guard > Shield-Guard > Gate Warden > Royal Warden, Hammer-Guard > Anvilguard > Ironbreaker}}

And one Ironpass regional group:
  Ironpass (T2-T7): Recruit > Warrior > {Infantry > Axeman > Veteran Axeman > Mountain Guard, Arbalest > Veteran Arbalest > Sharpshooter}

Strategy:
  - Iron Hills Noble line (T3-T9, 11 troops) is ENTIRELY NEW — none of those IDs exist in the file today.
  - All other existing Erebor / Iron Hills Regular / Ironpass troops keep their IDs (save-compat).
    Only their <EquipmentRoster> blocks are rewritten to match the spec armor + weapon guide.

Deletes nothing — every existing troop maps to a spec role.

Usage:
    python tools/apply_erebor_troop_revamp.py --dry-run
    python tools/apply_erebor_troop_revamp.py --apply
"""
import argparse
import os
import re
import sys
from typing import Dict, List, Tuple

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_erebor.xml"
)

# No deletions — every existing troop maps to a spec role
DELETE_IDS: set = set()

# Renames/level updates — keep existing display names since they already match spec
RENAMES: Dict[str, Dict[str, str]] = {}

# Upgrade chain rewires (existing chains already match spec topology — add new noble line)
UPGRADE_TARGETS: Dict[str, List[str]] = {
    # ---- Iron Hills Noble (new) ----
    "iron_hills_noble":                        ["iron_hills_noble_scout", "iron_hills_noble_swordsman"],
    "iron_hills_noble_scout":                  ["iron_hills_noble_sharpshooter"],
    "iron_hills_noble_sharpshooter":           ["iron_hills_noble_veteran_sharpshooter"],
    "iron_hills_noble_veteran_sharpshooter":   [],
    "iron_hills_noble_swordsman":              ["iron_hills_noble_infantry"],
    "iron_hills_noble_infantry":               ["iron_hills_noble_guard", "iron_hills_noble_hammer_guard"],
    "iron_hills_noble_guard":                  ["iron_hills_noble_shield_guard"],
    "iron_hills_noble_shield_guard":           ["iron_hills_noble_gate_warden"],
    "iron_hills_noble_gate_warden":            ["iron_hills_noble_royal_warden"],
    "iron_hills_noble_royal_warden":           [],
    "iron_hills_noble_hammer_guard":           ["iron_hills_noble_anvilguard"],
    "iron_hills_noble_anvilguard":             ["iron_hills_noble_ironbreaker"],
    "iron_hills_noble_ironbreaker":            [],
}

# =============================================================================
# EQUIPMENT — 40 existing troops refit per spec armor + weapon guide.
# Each value is an ordered list of (slot, item_id) pairs.
# Horse/HorseHarness lines (none in Erebor — all infantry) are auto-preserved
# from existing XML by extract_horse_lines() if any happen to be present.
# =============================================================================
EQUIPMENT: Dict[str, List[Tuple[str, str]]] = {

    # =================================================================
    # EREBOR REGULAR LINE (T2-T6) — 8 troops
    # =================================================================
    # T2 Miner — root, light leather, just 1H axe
    "erebor_reg_miner": [
        ("Item0", "sm_dwarf_erebor_1h_axe_d"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_light_a"),
        ("Body",  "sk_dwarf_erebor_chest_leather_light_a"),
        ("Leg",   "sk_dwarf_erebor_boots_light_a"),
    ],
    # T3 Militia — shield introduced
    "erebor_reg_militia": [
        ("Item0", "sm_dwarf_erebor_1h_axe_c"),
        ("Item1", "sm_dwarf_erebor_shield_leather_a"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_light_b"),
        ("Body",  "sk_dwarf_erebor_chest_leather_light_b"),
        ("Leg",   "sk_dwarf_erebor_boots_light_b"),
    ],
    # T4 Skirmisher (Archer branch — lighter than infantry)
    "erebor_reg_skirmisher": [
        ("Item0", "sm_dwarf_erebor_bow_a"),
        ("Item1", "sk_dwarf_erebor_arrow_a"),
        ("Item2", "sm_dwarf_erebor_1h_axe_d"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_med_a"),
        ("Body",  "sk_dwarf_erebor_chest_leather_med_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_a"),
    ],
    # T5 Bowman (Archer branch top)
    "erebor_reg_bowman": [
        ("Item0", "sm_dwarf_erebor_bow_b"),
        ("Item1", "sk_dwarf_erebor_arrow_b"),
        ("Item2", "sm_dwarf_erebor_1h_axe_c"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_med_b"),
        ("Body",  "sk_dwarf_erebor_chest_leather_med_b"),
        ("Leg",   "sk_dwarf_erebor_boots_med_b"),
    ],
    # T4 Company (Infantry branch)
    "erebor_reg_company": [
        ("Item0", "sm_dwarf_erebor_1h_axe_c"),
        ("Item1", "sm_dwarf_erebor_shield_leather_b"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_med_a"),
        ("Body",  "sk_dwarf_erebor_chest_leather_med_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_b"),
    ],
    # T5 Fighter (Infantry mid — pauldron introduced)
    "erebor_reg_fighter": [
        ("Item0", "sm_dwarf_erebor_1h_axe_b"),
        ("Item1", "sm_dwarf_erebor_shield_leather_c"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_heavy_a"),
        ("Body",  "sk_dwarf_erebor_chest_chain_a"),
        ("Cape",  "sk_dwarf_erebor_pauldron_scale_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_c"),
    ],
    # T6 Mattock Warrior (2H branch — uses iron mattock)
    "erebor_reg_mattock_warrior": [
        ("Item0", "sm_dwarf_iron_mattock_a"),
        ("Item1", "sm_dwarf_iron_mattock_b"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_heavy_c"),
        ("Body",  "sk_dwarf_erebor_chest_scale_a"),
        ("Cape",  "sk_dwarf_erebor_pauldron_scale_cape_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_e"),
    ],
    # T6 Warrior (Infantry top — heavy shield)
    "erebor_reg_warrior": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a"),
        ("Item1", "sm_dwarf_erebor_shield_leather_d"),
        ("Head",  "sk_dwarf_erebor_helmet_leather_heavy_b"),
        ("Body",  "sk_dwarf_erebor_chest_scale_a"),
        ("Cape",  "sk_dwarf_erebor_pauldron_scale_cape_b"),
        ("Leg",   "sk_dwarf_erebor_boots_med_f"),
    ],

    # =================================================================
    # EREBOR NOBLE LINE (T3-T9) — 13 troops (12 plus erebor_noble root)
    # =================================================================
    # T3 Noble — root, plate light, 1H axe + metal shield
    "erebor_noble": [
        ("Item0", "sm_dwarf_erebor_1h_axe_c"),
        ("Item1", "sm_dwarf_erebor_shield_metal_a"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_light_a"),
        ("Body",  "sk_dwarf_erebor_chest_chain_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_a"),
    ],
    # T4 Ranger (Archer branch — bow + 1H axe + tower shield)
    "erebor_noble_ranger": [
        ("Item0", "sm_dwarf_erebor_bow_a"),
        ("Item1", "sk_dwarf_erebor_arrow_a"),
        ("Item2", "sm_dwarf_erebor_1h_axe_c"),
        ("Item3", "sm_dwarf_erebor_shield_tower_a"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_light_b"),
        ("Body",  "sk_dwarf_erebor_chest_scale_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_b"),
    ],
    # T5 Archer (Archer mid)
    "erebor_noble_archer": [
        ("Item0", "sm_dwarf_erebor_bow_a"),
        ("Item1", "sk_dwarf_erebor_arrow_a"),
        ("Item2", "sm_dwarf_erebor_1h_axe_b"),
        ("Item3", "sm_dwarf_erebor_shield_tower_b"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_med_a"),
        ("Body",  "sk_dwarf_erebor_chest_plate_heavy_a"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_med_a"),
        ("Gloves","sk_dwarf_erebor_bracers_med_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_c"),
    ],
    # T6 Veteran Archer (Archer top)
    "erebor_noble_veteran_archer": [
        ("Item0", "sm_dwarf_erebor_bow_b"),
        ("Item1", "sk_dwarf_erebor_arrow_b"),
        ("Item2", "sm_dwarf_erebor_1h_axe_a"),
        ("Item3", "sm_dwarf_erebor_shield_tower_c"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_med_b"),
        ("Body",  "sk_dwarf_erebor_chest_plate_heavy_b"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_med_b"),
        ("Gloves","sk_dwarf_erebor_bracers_med_b"),
        ("Leg",   "sk_dwarf_erebor_boots_med_d"),
    ],
    # T4 Longbeard (Infantry branch — 1H axe + metal shield)
    "erebor_noble_longbeard": [
        ("Item0", "sm_dwarf_erebor_1h_axe_b"),
        ("Item1", "sm_dwarf_erebor_shield_metal_a"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_light_c"),
        ("Body",  "sk_dwarf_erebor_chest_scale_a"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_med_a"),
        ("Leg",   "sk_dwarf_erebor_boots_med_d"),
    ],
    # T5 Infantry — heavy plate begins, spear + axe combo
    "erebor_noble_infantry": [
        ("Item0", "sm_dwarf_erebor_1h_axe_b"),
        ("Item1", "sm_dwarf_erebor_spear_b"),
        ("Item2", "sm_dwarf_erebor_shield_metal_b"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_med_c"),
        ("Body",  "sk_dwarf_erebor_chest_plate_heavy_c"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_med_b"),
        ("Gloves","sk_dwarf_erebor_bracers_med_c"),
        ("Leg",   "sk_dwarf_erebor_boots_med_e"),
    ],
    # T6 Guard (Shield-Guard branch — heavy plate, axe+spear+shield)
    "erebor_noble_guard": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a"),
        ("Item1", "sm_dwarf_erebor_spear_a"),
        ("Item2", "sm_dwarf_erebor_shield_metal_c"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_heavy_a"),
        ("Body",  "sk_dwarf_erebor_chest_plate_heavy_c"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_heavy_e"),
        ("Gloves","sk_dwarf_erebor_bracers_heavy_a"),
        ("Leg",   "sk_dwarf_erebor_greaves_heavy_a"),
    ],
    # T7 Shield-Guard — throwable axe introduced, elite helmet
    "erebor_noble_shield_guard": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a"),
        ("Item1", "sm_dwarf_erebor_spear_a"),
        ("Item2", "sm_dwarf_erebor_shield_metal_d"),
        ("Item3", "southern_throwing_axe_1_t4"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_elite_a"),
        ("Body",  "sk_dwarf_erebor_chest_plate_elite_a"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_cape_heavy_a"),
        ("Gloves","sk_dwarf_erebor_bracers_heavy_b"),
        ("Leg",   "sk_dwarf_erebor_greaves_heavy_b"),
    ],
    # T8 Gate Warden — heavy gold shield
    "erebor_noble_gate_warden": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a2"),
        ("Item1", "sm_dwarf_erebor_spear_a"),
        ("Item2", "sm_dwarf_erebor_shield_metal_c2"),
        ("Item3", "southern_throwing_axe_1_t4"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_lord_a"),
        ("Body",  "sk_dwarf_erebor_chest_plate_elite_c"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_cape_heavy_c"),
        ("Gloves","sk_dwarf_erebor_bracers_elite_a"),
        ("Leg",   "sk_dwarf_erebor_greaves_elite_a"),
    ],
    # T9 Royal Warden — elite gold shield, gold ornament helmet
    "erebor_noble_royal_warden": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a2"),
        ("Item1", "sm_dwarf_erebor_spear_a"),
        ("Item2", "sm_dwarf_erebor_shield_metal_d2"),
        ("Item3", "southern_throwing_axe_1_t4"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_lord_a2"),
        ("Body",  "sk_dwarf_erebor_chest_plate_elite_a2"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_cape_elite_a"),
        ("Gloves","sk_dwarf_erebor_bracers_elite_b"),
        ("Leg",   "sk_dwarf_erebor_greaves_elite_b"),
    ],
    # T6 Axe-Guard (2H branch — 2H axe, heavy_a/b chest+pauldron)
    "erebor_noble_axe_guard": [
        ("Item0", "sm_dwarf_erebor_axe_2h_a"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_heavy_b"),
        ("Body",  "sk_dwarf_erebor_chest_plate_heavy_a"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_heavy_a"),
        ("Gloves","sk_dwarf_erebor_bracers_heavy_c"),
        ("Leg",   "sk_dwarf_erebor_greaves_heavy_c"),
    ],
    # T7 Veteran Axe-Guard — throwable axe introduced
    "erebor_noble_veteran_axe_guard": [
        ("Item0", "sm_dwarf_erebor_axe_2h_b"),
        ("Item1", "southern_throwing_axe_1_t4"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_elite_b"),
        ("Body",  "sk_dwarf_erebor_chest_plate_heavy_b"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_heavy_b"),
        ("Gloves","sk_dwarf_erebor_bracers_heavy_d"),
        ("Leg",   "sk_dwarf_erebor_greaves_heavy_d"),
    ],
    # T8 Shield-Breaker — gold 2H axe, no shield
    "erebor_noble_shield_breaker": [
        ("Item0", "sm_dwarf_erebor_axe_2h_a2"),
        ("Item1", "southern_throwing_axe_1_t4"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_lord_b"),
        ("Body",  "sk_dwarf_erebor_chest_plate_elite_b"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_elite_a"),
        ("Gloves","sk_dwarf_erebor_bracers_elite_c"),
        ("Leg",   "sk_dwarf_erebor_greaves_elite_a"),
    ],

    # =================================================================
    # EREBOR OATHSWORN LINE (T7-T9) — 3 troops
    # =================================================================
    # T7 Oathsworn — root, axe+spear, heavy shield, legionary helmet
    "erebor_oathsworn": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a"),
        ("Item1", "sm_dwarf_erebor_spear_b"),
        ("Item2", "sm_dwarf_erebor_shield_metal_b"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_legionary_a"),
        ("Body",  "sk_dwarf_erebor_chest_plate_elite_c"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_elite_a"),
        ("Gloves","sk_dwarf_erebor_bracers_heavy_c"),
        ("Leg",   "sk_dwarf_erebor_greaves_heavy_c"),
    ],
    # T8 Legionary — 2H hammer alternative loadout, stripe cape
    "erebor_oathsworn_legionary": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a"),
        ("Item1", "sm_dwarf_erebor_spear_a"),
        ("Item2", "sm_dwarf_erebor_mace_a"),
        ("Item3", "sm_dwarf_erebor_shield_metal_c"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_legionary_b"),
        ("Body",  "sk_dwarf_erebor_chest_plate_elite_d"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_cape_heavy_e"),
        ("Gloves","sk_dwarf_erebor_bracers_elite_a"),
        ("Leg",   "sk_dwarf_erebor_greaves_elite_a"),
    ],
    # T9 Royal Legionary — elite gold stripe cape
    "erebor_oathsworn_royal_legionary": [
        ("Item0", "sm_dwarf_erebor_1h_axe_a"),
        ("Item1", "sm_dwarf_erebor_spear_a"),
        ("Item2", "sm_dwarf_erebor_mace_b"),
        ("Item3", "sm_dwarf_erebor_shield_metal_d"),
        ("Head",  "sk_dwarf_erebor_helmet_plate_legionary_c"),
        ("Body",  "sk_dwarf_erebor_chest_plate_elite_e"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_cape_elite_a"),
        ("Gloves","sk_dwarf_erebor_bracers_elite_d"),
        ("Leg",   "sk_dwarf_erebor_greaves_elite_b"),
    ],

    # =================================================================
    # IRON HILLS REGULAR LINE (T2-T6) — 8 troops
    # A-series helmets/bracers/boots throughout
    # =================================================================
    # T2 Recruit — root
    "iron_hills_reg_recruit": [
        ("Item0", "sm_dwarf_iron_sword_a"),
        ("Item1", "sm_iron_shield_a"),
        ("Head",  "sk_dwarf_iron_helmet_light_a1"),
        ("Body",  "sk_dwarf_iron_chest_light_a"),
        ("Leg",   "sk_dwarf_iron_boots_light_a"),
    ],
    # T3 Militia
    "iron_hills_reg_militia": [
        ("Item0", "sm_dwarf_iron_sword_a"),
        ("Item1", "sm_iron_shield_a"),
        ("Head",  "sk_dwarf_iron_helmet_light_b1"),
        ("Body",  "sk_dwarf_iron_chest_light_b"),
        ("Leg",   "sk_dwarf_iron_boots_light_a"),
    ],
    # T4 Skirmisher (Archer branch — A1 archer helmet)
    "iron_hills_reg_skirmisher": [
        ("Item0", "sm_dwarf_erebor_bow_a"),
        ("Item1", "sk_dwarf_erebor_arrow_a"),
        ("Item2", "sm_dwarf_iron_sword_a"),
        ("Head",  "sk_dwarf_iron_helmet_med_a1"),
        ("Body",  "sk_dwarf_iron_chest_chain_a"),
        ("Cape",  "sk_dwarf_iron_pauldron_light_a"),
        ("Leg",   "sk_dwarf_iron_boots_med_a1"),
    ],
    # T5 Bowman (Archer top)
    "iron_hills_reg_bowman": [
        ("Item0", "sm_dwarf_erebor_bow_b"),
        ("Item1", "sk_dwarf_erebor_arrow_b"),
        ("Item2", "sm_dwarf_iron_sword_b"),
        ("Head",  "sk_dwarf_iron_helmet_med_a2"),
        ("Body",  "sk_dwarf_iron_chest_scale_a"),
        ("Cape",  "sk_dwarf_iron_pauldron_med_a"),
        ("Gloves","sk_dwarf_iron_bracer_light_a1"),
        ("Leg",   "sk_dwarf_iron_boots_med_b1"),
    ],
    # T4 Company (Infantry branch — B helmet)
    "iron_hills_reg_company": [
        ("Item0", "sm_dwarf_iron_sword_a"),
        ("Item1", "sm_iron_shield_a"),
        ("Head",  "sk_dwarf_iron_helmet_med_b1"),
        ("Body",  "sk_dwarf_iron_chest_chain_a"),
        ("Cape",  "sk_dwarf_iron_pauldron_light_b"),
        ("Leg",   "sk_dwarf_iron_boots_med_a1"),
    ],
    # T5 Fighter (Infantry mid)
    "iron_hills_reg_fighter": [
        ("Item0", "sm_dwarf_iron_sword_b"),
        ("Item1", "sm_iron_shield_b"),
        ("Head",  "sk_dwarf_iron_helmet_heavy_b1"),
        ("Body",  "sk_dwarf_iron_chest_scale_a"),
        ("Cape",  "sk_dwarf_iron_pauldron_med_a"),
        ("Gloves","sk_dwarf_iron_bracer_light_a2"),
        ("Leg",   "sk_dwarf_iron_boots_heavy_a2"),
    ],
    # T6 Axe Warrior (2H branch)
    "iron_hills_reg_axe_warrior": [
        ("Item0", "sm_dwarf_iron_axe_a"),
        ("Item1", "sm_dwarf_iron_axe_b"),
        ("Head",  "sk_dwarf_iron_helmet_heavy_b2"),
        ("Body",  "sk_dwarf_iron_chest_heavy_a"),
        ("Cape",  "sk_dwarf_iron_pauldron_med_b"),
        ("Gloves","sk_dwarf_iron_bracer_med_a1"),
        ("Leg",   "sk_dwarf_iron_boots_heavy_a3"),
    ],
    # T6 Warrior (Infantry top)
    "iron_hills_reg_warrior": [
        ("Item0", "sm_dwarf_iron_sword_b"),
        ("Item1", "sm_iron_shield_b"),
        ("Head",  "sk_dwarf_iron_helmet_heavy_b2"),
        ("Body",  "sk_dwarf_iron_chest_heavy_b"),
        ("Cape",  "sk_dwarf_iron_pauldron_med_b"),
        ("Gloves","sk_dwarf_iron_bracer_med_a2"),
        ("Leg",   "sk_dwarf_iron_boots_elite_a2"),
    ],

    # =================================================================
    # IRONPASS LINE (T2-T7) — 9 troops
    # C-series helmets; B-series bracers/boots
    # Infantry/Axeman line uses fur-cape pauldrons; Crossbow line uses Erebor plate (no cape)
    # =================================================================
    # T2 Recruit
    "ironpass_recruit": [
        ("Item0", "sm_dwarf_erebor_spear_b"),
        ("Item1", "sm_iron_shield_tower_a"),
        ("Head",  "sk_dwarf_iron_helmet_light_c1"),
        ("Body",  "sk_dwarf_iron_chest_heavy_a"),
        ("Gloves","sk_dwarf_iron_gloves_a"),
        ("Leg",   "sk_dwarf_iron_boots_light_a"),
    ],
    # T3 Warrior
    "ironpass_warrior": [
        ("Item0", "sm_dwarf_erebor_spear_b"),
        ("Item1", "sm_iron_shield_tower_b"),
        ("Head",  "sk_dwarf_iron_helmet_light_c2"),
        ("Body",  "sk_dwarf_iron_chest_heavy_b"),
        ("Cape",  "sk_dwarf_iron_pauldron_light_a"),
        ("Gloves","sk_dwarf_iron_bracer_light_b1"),
        ("Leg",   "sk_dwarf_iron_boots_med_c1"),
    ],
    # T4 Infantry (Infantry branch)
    "ironpass_infantry": [
        ("Item0", "sm_dwarf_erebor_spear_a"),
        ("Item1", "sm_iron_shield_tower_c"),
        ("Head",  "sk_dwarf_iron_helmet_med_c1"),
        ("Body",  "sk_dwarf_iron_chest_heavy_b"),
        ("Cape",  "sk_dwarf_iron_pauldron_light_b"),
        ("Gloves","sk_dwarf_iron_bracer_light_b2"),
        ("Leg",   "sk_dwarf_iron_boots_heavy_a1"),
    ],
    # T5 Axeman (2H axe picked up)
    "ironpass_axeman": [
        ("Item0", "sm_dwarf_erebor_spear_a"),
        ("Item1", "sm_dwarf_iron_axe_a"),
        ("Item2", "sm_dwarf_erebor_shield_tower_a"),
        ("Head",  "sk_dwarf_iron_helmet_med_c2"),
        ("Body",  "sk_dwarf_iron_chest_heavy_c"),
        ("Cape",  "sk_dwarf_iron_pauldron_heavy_cape_a"),
        ("Gloves","sk_dwarf_iron_bracer_med_b1"),
        ("Leg",   "sk_dwarf_iron_boots_heavy_b1"),
    ],
    # T6 Veteran Axeman
    "ironpass_veteran_axeman": [
        ("Item0", "sm_dwarf_erebor_spear_a"),
        ("Item1", "sm_dwarf_iron_axe_b"),
        ("Item2", "sm_dwarf_erebor_shield_tower_b"),
        ("Head",  "sk_dwarf_iron_helmet_heavy_c1"),
        ("Body",  "sk_dwarf_iron_chest_heavy_d"),
        ("Cape",  "sk_dwarf_iron_pauldron_heavy_cape_b"),
        ("Gloves","sk_dwarf_iron_bracer_med_b3"),
        ("Leg",   "sk_dwarf_iron_boots_heavy_b2"),
    ],
    # T7 Mountain Guard (Infantry top — specified elite bracers/boots)
    "ironpass_mountain_guard": [
        ("Item0", "sm_dwarf_erebor_spear_a"),
        ("Item1", "sm_dwarf_iron_axe_c"),
        ("Item2", "sm_dwarf_erebor_shield_tower_e"),
        ("Head",  "sk_dwarf_iron_helmet_elite_c1"),
        ("Body",  "sk_dwarf_iron_chest_heavy_e"),
        ("Cape",  "sk_dwarf_iron_pauldron_heavy_cape_c"),
        ("Gloves","sk_dwarf_iron_bracer_heavy_c1"),
        ("Leg",   "sk_dwarf_iron_boots_elite_a1"),
    ],
    # T4 Arbalest (Crossbow branch — crossbow + 2H axe sidearm)
    "ironpass_arbalest": [
        ("Item0", "sm_dwarf_iron_crossbow_heavy_a"),
        ("Item1", "sm_dwarf_iron_bolt_a"),
        ("Item2", "sm_dwarf_iron_axe_a"),
        ("Head",  "sk_dwarf_iron_helmet_med_c1"),
        ("Body",  "sk_dwarf_iron_chest_heavy_b"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_med_a"),
        ("Gloves","sk_dwarf_iron_bracer_light_b3"),
        ("Leg",   "sk_dwarf_iron_boots_heavy_b1"),
    ],
    # T5 Veteran Arbalest
    "ironpass_veteran_arbalest": [
        ("Item0", "sm_dwarf_iron_crossbow_heavy_a"),
        ("Item1", "sm_dwarf_iron_bolt_a"),
        ("Item2", "sm_dwarf_iron_axe_b"),
        ("Head",  "sk_dwarf_iron_helmet_med_c2"),
        ("Body",  "sk_dwarf_iron_chest_heavy_c"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_med_b"),
        ("Gloves","sk_dwarf_iron_bracer_med_b2"),
        ("Leg",   "sk_dwarf_iron_boots_heavy_b3"),
    ],
    # T6 Sharpshooter (Crossbow top — caps at T6 per spec)
    "ironpass_sharpshooter": [
        ("Item0", "sm_dwarf_iron_crossbow_heavy_b"),
        ("Item1", "sm_dwarf_iron_bolt_a"),
        ("Item2", "sm_dwarf_iron_axe_c"),
        ("Head",  "sk_dwarf_iron_helmet_heavy_c2"),
        ("Body",  "sk_dwarf_iron_chest_heavy_d"),
        ("Cape",  "sk_dwarf_erebor_pauldron_plate_med_b"),
        ("Gloves","sk_dwarf_iron_bracer_med_b4"),
        ("Leg",   "sk_dwarf_iron_boots_elite_b2"),
    ],
}

# =============================================================================
# NEW TROOPS — 11 new Iron Hills Noble line troops
# All race="dwarf", culture="Culture.erebor" (single Erebor culture covers all groups)
# Skills are tier-appropriate (rough Athletics + class-focused weapon skills)
# =============================================================================

def _new_troop(troop_id: str, name: str, level: int, group: str,
               skills: Dict[str, int], equipment: List[Tuple[str, str]],
               is_basic: bool = False) -> str:
    """Build a fully-formed <NPCCharacter> block in the file's 4-space indent style."""
    basic_attr = '\n        is_basic_troop="true"' if is_basic else ''
    skill_lines = "\n".join(
        f'            <skill\n                id="{sk}"\n                value="{v}" />'
        for sk, v in skills.items()
    )
    equip_lines = "\n".join(
        f'                <equipment\n                    slot="{slot}"\n                    id="Item.{item_id}" />'
        for slot, item_id in equipment
    )
    return f"""
    <NPCCharacter
        id="{troop_id}"
        race="dwarf"
        default_group="{group}"
        level="{level}"
        name="{{={troop_id}_name}}[Iron Hills] {name}"
        occupation="Soldier"{basic_attr}
        culture="Culture.erebor">
        <face>
            <face_key_template
                value="BodyProperty.fighter_empire" />
        </face>
        <skills>
{skill_lines}
        </skills>
        <upgrade_targets></upgrade_targets>
        <Equipments>
            <EquipmentRoster>
{equip_lines}
            </EquipmentRoster>
        </Equipments>
    </NPCCharacter>
"""


# Skill profiles per tier/role (rough estimates aligned with existing Erebor troops)
_SKILLS_T3_BAL = {"Athletics": 55, "Riding": 5, "OneHanded": 60, "TwoHanded": 30,
                  "Polearm": 40, "Bow": 25, "Crossbow": 5, "Throwing": 25}
_SKILLS_T4_RNG = {"Athletics": 70, "Riding": 5, "OneHanded": 50, "TwoHanded": 20,
                  "Polearm": 20, "Bow": 5, "Crossbow": 100, "Throwing": 15}
_SKILLS_T5_RNG = {"Athletics": 85, "Riding": 5, "OneHanded": 65, "TwoHanded": 20,
                  "Polearm": 25, "Bow": 5, "Crossbow": 140, "Throwing": 15}
_SKILLS_T6_RNG = {"Athletics": 100, "Riding": 5, "OneHanded": 75, "TwoHanded": 20,
                  "Polearm": 30, "Bow": 5, "Crossbow": 180, "Throwing": 20}
_SKILLS_T4_INF = {"Athletics": 75, "Riding": 5, "OneHanded": 90, "TwoHanded": 30,
                  "Polearm": 70, "Bow": 10, "Crossbow": 5, "Throwing": 20}
_SKILLS_T5_INF = {"Athletics": 90, "Riding": 5, "OneHanded": 120, "TwoHanded": 40,
                  "Polearm": 100, "Bow": 10, "Crossbow": 5, "Throwing": 25}
_SKILLS_T6_INF = {"Athletics": 110, "Riding": 5, "OneHanded": 150, "TwoHanded": 50,
                  "Polearm": 130, "Bow": 10, "Crossbow": 5, "Throwing": 30}
_SKILLS_T7_INF = {"Athletics": 130, "Riding": 5, "OneHanded": 180, "TwoHanded": 60,
                  "Polearm": 160, "Bow": 10, "Crossbow": 5, "Throwing": 35}
_SKILLS_T8_INF = {"Athletics": 150, "Riding": 5, "OneHanded": 210, "TwoHanded": 70,
                  "Polearm": 190, "Bow": 10, "Crossbow": 5, "Throwing": 40}
_SKILLS_T9_INF = {"Athletics": 170, "Riding": 5, "OneHanded": 240, "TwoHanded": 80,
                  "Polearm": 220, "Bow": 10, "Crossbow": 5, "Throwing": 45}
_SKILLS_T6_2H = {"Athletics": 110, "Riding": 5, "OneHanded": 70, "TwoHanded": 170,
                 "Polearm": 50, "Bow": 5, "Crossbow": 5, "Throwing": 25}
_SKILLS_T7_2H = {"Athletics": 130, "Riding": 5, "OneHanded": 80, "TwoHanded": 200,
                 "Polearm": 55, "Bow": 5, "Crossbow": 5, "Throwing": 30}
_SKILLS_T8_2H = {"Athletics": 150, "Riding": 5, "OneHanded": 90, "TwoHanded": 230,
                 "Polearm": 60, "Bow": 5, "Crossbow": 5, "Throwing": 35}


NEW_TROOPS: List[Tuple[str, str]] = [
    # ---- Iron Hills Noble root (T3, is_basic_troop) ----
    ("iron_hills_noble", _new_troop(
        "iron_hills_noble", "Noble", 16, "Infantry", _SKILLS_T3_BAL,
        [
            ("Item0", "sm_dwarf_iron_sword_a"),
            ("Item1", "sm_iron_shield_a"),
            ("Head",  "sk_dwarf_iron_helmet_light_d1"),
            ("Body",  "sk_dwarf_iron_chest_chain_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_light_a"),
            ("Gloves","sk_dwarf_iron_bracer_light_c1"),
            ("Leg",   "sk_dwarf_iron_boots_heavy_c1"),
        ],
        is_basic=True,
    )),

    # ---- Archer (Sharpshooter) branch — Scout T4, Sharpshooter T5, Veteran Sharpshooter T6
    ("iron_hills_noble_scout", _new_troop(
        "iron_hills_noble_scout", "Scout", 21, "Ranged", _SKILLS_T4_RNG,
        [
            ("Item0", "sm_dwarf_iron_crossbow_heavy_a"),
            ("Item1", "sm_dwarf_iron_bolt_a"),
            ("Item2", "sm_dwarf_iron_sword_a"),
            ("Head",  "sk_dwarf_iron_helmet_light_d2"),
            ("Body",  "sk_dwarf_iron_chest_chain_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_light_b"),
            ("Gloves","sk_dwarf_iron_bracer_light_c2"),
            ("Leg",   "sk_dwarf_iron_boots_heavy_c1"),
        ],
    )),
    ("iron_hills_noble_sharpshooter", _new_troop(
        "iron_hills_noble_sharpshooter", "Sharpshooter", 26, "Ranged", _SKILLS_T5_RNG,
        [
            ("Item0", "sm_dwarf_iron_crossbow_heavy_a"),
            ("Item1", "sm_dwarf_iron_bolt_a"),
            ("Item2", "sm_dwarf_iron_sword_b"),
            ("Head",  "sk_dwarf_iron_helmet_med_d1"),
            ("Body",  "sk_dwarf_iron_chest_scale_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_med_c"),
            ("Gloves","sk_dwarf_iron_bracer_med_c1"),
            ("Leg",   "sk_dwarf_iron_boots_heavy_c2"),
        ],
    )),
    ("iron_hills_noble_veteran_sharpshooter", _new_troop(
        "iron_hills_noble_veteran_sharpshooter", "Veteran Sharpshooter", 31, "Ranged", _SKILLS_T6_RNG,
        [
            ("Item0", "sm_dwarf_iron_crossbow_heavy_b"),
            ("Item1", "sm_dwarf_iron_bolt_a"),
            ("Item2", "sm_dwarf_iron_sword_b"),
            ("Head",  "sk_dwarf_iron_helmet_med_d2"),
            ("Body",  "sk_dwarf_iron_chest_heavy_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_med_d"),
            ("Gloves","sk_dwarf_iron_bracer_med_c3"),
            ("Leg",   "sk_dwarf_iron_boots_elite_c1"),
        ],
    )),

    # ---- Infantry/Guard branch — Swordsman T4, Infantry T5, Guard T6, Shield-Guard T7, Gate Warden T8, Royal Warden T9
    ("iron_hills_noble_swordsman", _new_troop(
        "iron_hills_noble_swordsman", "Swordsman", 21, "Infantry", _SKILLS_T4_INF,
        [
            ("Item0", "sm_dwarf_iron_sword_a"),
            ("Item1", "sm_iron_shield_a"),
            ("Head",  "sk_dwarf_iron_helmet_med_d1"),
            ("Body",  "sk_dwarf_iron_chest_chain_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_med_a"),
            ("Gloves","sk_dwarf_iron_bracer_light_c3"),
            ("Leg",   "sk_dwarf_iron_boots_heavy_c1"),
        ],
    )),
    ("iron_hills_noble_infantry", _new_troop(
        "iron_hills_noble_infantry", "Infantry", 26, "Infantry", _SKILLS_T5_INF,
        [
            ("Item0", "sm_dwarf_iron_sword_b"),
            ("Item1", "sm_dwarf_erebor_spear_b"),
            ("Item2", "sm_iron_shield_a"),
            ("Head",  "sk_dwarf_iron_helmet_med_d2"),
            ("Body",  "sk_dwarf_iron_chest_scale_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_med_b"),
            ("Gloves","sk_dwarf_iron_bracer_med_c1"),
            ("Leg",   "sk_dwarf_iron_boots_heavy_c2"),
        ],
    )),
    ("iron_hills_noble_guard", _new_troop(
        "iron_hills_noble_guard", "Guard", 31, "Infantry", _SKILLS_T6_INF,
        [
            ("Item0", "sm_dwarf_iron_sword_b"),
            ("Item1", "sm_dwarf_erebor_spear_a"),
            ("Item2", "sm_iron_shield_b"),
            ("Head",  "sk_dwarf_iron_helmet_heavy_d1"),
            ("Body",  "sk_dwarf_iron_chest_heavy_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_heavy_cape_a"),
            ("Gloves","sk_dwarf_iron_bracer_med_c2"),
            ("Leg",   "sk_dwarf_iron_boots_elite_c1"),
        ],
    )),
    ("iron_hills_noble_shield_guard", _new_troop(
        "iron_hills_noble_shield_guard", "Shield-Guard", 36, "Infantry", _SKILLS_T7_INF,
        [
            ("Item0", "sm_dwarf_iron_sword_b"),
            ("Item1", "sm_dwarf_erebor_spear_a"),
            ("Item2", "sm_iron_shield_b"),
            ("Head",  "sk_dwarf_iron_helmet_heavy_d2"),
            ("Body",  "sk_dwarf_iron_chest_heavy_c"),
            ("Cape",  "sk_dwarf_iron_pauldron_heavy_cape_b"),
            ("Gloves","sk_dwarf_iron_bracer_heavy_c1"),
            ("Leg",   "sk_dwarf_iron_boots_elite_c2"),
        ],
    )),
    ("iron_hills_noble_gate_warden", _new_troop(
        "iron_hills_noble_gate_warden", "Gate Warden", 41, "Infantry", _SKILLS_T8_INF,
        [
            ("Item0", "sm_dwarf_iron_sword_b"),
            ("Item1", "sm_dwarf_erebor_spear_a"),
            ("Item2", "sm_iron_shield_tower_c"),
            ("Head",  "sk_dwarf_iron_helmet_elite_d1"),
            ("Body",  "sk_dwarf_iron_chest_elite_a"),
            ("Cape",  "sk_dwarf_iron_pauldron_heavy_cape_c"),
            ("Gloves","sk_dwarf_iron_bracer_elite_c1"),
            ("Leg",   "sk_dwarf_iron_boots_elite_c2"),
        ],
    )),
    ("iron_hills_noble_royal_warden", _new_troop(
        "iron_hills_noble_royal_warden", "Royal Warden", 46, "Infantry", _SKILLS_T9_INF,
        [
            ("Item0", "sm_dwarf_iron_sword_b"),
            ("Item1", "sm_dwarf_erebor_spear_a"),
            ("Item2", "sm_iron_shield_tower_c_gold"),
            ("Head",  "sk_dwarf_iron_helmet_lord_d1"),
            ("Body",  "sk_dwarf_iron_chest_elite_d"),
            ("Cape",  "sk_dwarf_iron_pauldron_elite_cape_a"),
            ("Gloves","sk_dwarf_iron_bracer_elite_c3"),
            ("Leg",   "sk_dwarf_iron_boots_lord_c1"),
        ],
    )),

    # ---- Shock (Hammer-Guard) branch — Hammer-Guard T6, Anvilguard T7, Ironbreaker T8
    # No-cape heavy/elite pauldrons throughout
    ("iron_hills_noble_hammer_guard", _new_troop(
        "iron_hills_noble_hammer_guard", "Hammer-Guard", 31, "Infantry", _SKILLS_T6_2H,
        [
            ("Item0", "sm_dwarf_iron_hammer_a"),
            ("Head",  "sk_dwarf_iron_helmet_heavy_d1"),
            ("Body",  "sk_dwarf_iron_chest_heavy_b"),
            ("Cape",  "sk_dwarf_iron_pauldron_heavy_a"),
            ("Gloves","sk_dwarf_iron_bracer_med_c4"),
            ("Leg",   "sk_dwarf_iron_boots_heavy_c2"),
        ],
    )),
    ("iron_hills_noble_anvilguard", _new_troop(
        "iron_hills_noble_anvilguard", "Anvilguard", 36, "Infantry", _SKILLS_T7_2H,
        [
            ("Item0", "sm_dwarf_iron_hammer_b"),
            ("Head",  "sk_dwarf_iron_helmet_heavy_d2"),
            ("Body",  "sk_dwarf_iron_chest_heavy_d"),
            ("Cape",  "sk_dwarf_iron_pauldron_heavy_b"),
            ("Gloves","sk_dwarf_iron_bracer_heavy_c5"),
            ("Leg",   "sk_dwarf_iron_boots_elite_c1"),
        ],
    )),
    ("iron_hills_noble_ironbreaker", _new_troop(
        "iron_hills_noble_ironbreaker", "Ironbreaker", 41, "Infantry", _SKILLS_T8_2H,
        [
            ("Item0", "sm_dwarf_iron_hammer_c"),
            ("Head",  "sk_dwarf_iron_helmet_elite_d3"),
            ("Body",  "sk_dwarf_iron_chest_elite_b"),
            ("Cape",  "sk_dwarf_iron_pauldron_elite_a"),
            ("Gloves","sk_dwarf_iron_bracer_elite_c2"),
            ("Leg",   "sk_dwarf_iron_boots_elite_c2"),
        ],
    )),
]


# =============================================================================
# Apply logic
# =============================================================================

# 4-space outer indent (troops_erebor.xml is 4-space, unlike Gondor's 2-space)
NPC_OUTER_INDENT = "    "


def find_npc_block(content: str, troop_id: str) -> Tuple[int, int]:
    """Return (start, end) indices of the <NPCCharacter id="X">...</NPCCharacter> block."""
    pattern = re.compile(
        NPC_OUTER_INDENT + r'<NPCCharacter\s+id="' + re.escape(troop_id) + r'"'
        r'.*?'
        + NPC_OUTER_INDENT + r'</NPCCharacter>',
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


def extract_horse_lines(equip_block: str) -> List[str]:
    """Pull existing Horse / HorseHarness lines for cavalry preservation."""
    lines = []
    in_horse = False
    buf: List[str] = []
    for line in equip_block.splitlines():
        stripped = line.strip()
        if stripped.startswith('<equipment') and (
                'slot="Horse"' in line or 'slot="HorseHarness"' in line):
            in_horse = True
            buf = [line]
            if line.rstrip().endswith('/>'):
                lines.extend(buf)
                in_horse = False
                buf = []
            continue
        if in_horse:
            buf.append(line)
            if line.rstrip().endswith('/>'):
                lines.extend(buf)
                in_horse = False
                buf = []
    return lines


def build_equipment_xml(slots: List[Tuple[str, str]], indent: str, horse_lines: List[str]) -> str:
    """Construct the <EquipmentRoster> body with given indent (matches file's vertical style)."""
    inner_lines = []
    for slot, item_id in slots:
        inner_lines.append(
            f'{indent}    <equipment\n'
            f'{indent}        slot="{slot}"\n'
            f'{indent}        id="Item.{item_id}" />'
        )
    inner = "\n".join(inner_lines)
    if horse_lines:
        inner += "\n" + "\n".join(horse_lines)
    return f'{indent}<EquipmentRoster>\n{inner}\n{indent}</EquipmentRoster>'


def replace_equipment(npc_block: str, troop_id: str, slots: List[Tuple[str, str]]) -> str:
    """Replace the FIRST <EquipmentRoster> within an NPCCharacter block.

    Subsequent rosters (variations, civilian) are left untouched — typical
    troop files have 4 EquipmentRoster variants; we only replace the first.
    """
    m = EQUIP_ROSTER_RE.search(npc_block)
    if not m:
        raise RuntimeError(f"No <EquipmentRoster> found in {troop_id}")
    indent_after_newline = m.group(1).lstrip("\n")
    indent = indent_after_newline if indent_after_newline else "            "
    horse_lines = extract_horse_lines(m.group(2))
    new_roster = build_equipment_xml(slots, indent, horse_lines)
    return npc_block[:m.start()] + "\n" + new_roster + npc_block[m.end():]


UPGRADE_BLOCK_RE = re.compile(
    r'<upgrade_targets\s*(?:/>|>\s*(?:<upgrade_target[^/]*/>\s*)*</upgrade_targets>)',
    re.DOTALL,
)


def update_upgrade_targets(npc_block: str, targets: List[str]) -> str:
    """Replace <upgrade_targets>...</upgrade_targets> with the new chain."""
    if not targets:
        new_block = '<upgrade_targets></upgrade_targets>'
    else:
        lines = '\n'.join(
            f'            <upgrade_target\n                id="NPCCharacter.{t}" />'
            for t in targets
        )
        new_block = f'<upgrade_targets>\n{lines}\n        </upgrade_targets>'
    new_content, n = UPGRADE_BLOCK_RE.subn(new_block, npc_block, count=1)
    if n == 0:
        raise RuntimeError("No <upgrade_targets> found in block")
    return new_content


def remove_upgrade_targets_to(content: str, deleted_ids: set) -> Tuple[str, int]:
    """Remove <upgrade_target id="NPCCharacter.X" /> lines pointing at deleted ids.

    Handles both single-line and multi-line forms.
    """
    count = 0
    for tid in deleted_ids:
        # Multi-line form:
        #     <upgrade_target
        #         id="NPCCharacter.X" />
        pattern = re.compile(
            r'\s*<upgrade_target\s*\n\s*id="NPCCharacter\.' + re.escape(tid) + r'"\s*/>'
        )
        new_content, n = pattern.subn('', content)
        count += n
        content = new_content
        # Single-line fallback
        pattern2 = re.compile(
            r'\s*<upgrade_target\s+id="NPCCharacter\.' + re.escape(tid) + r'"\s*/>'
        )
        new_content, n = pattern2.subn('', content)
        count += n
        content = new_content
    return content, count


def apply(dry_run: bool = False):
    if not os.path.exists(TROOPS_FILE):
        print(f"ERROR: {TROOPS_FILE} not found", file=sys.stderr)
        sys.exit(1)

    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        content = f.read()
    orig_len = len(content)

    # 1. Equipment refits + upgrade_target rewires for existing troops
    refit = 0
    upgrade_rewires = 0
    missing = []
    for troop_id, slots in EQUIPMENT.items():
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            missing.append(troop_id)
            continue
        npc_block = content[start:end]
        npc_block = replace_equipment(npc_block, troop_id, slots)
        if troop_id in UPGRADE_TARGETS:
            npc_block = update_upgrade_targets(npc_block, UPGRADE_TARGETS[troop_id])
            upgrade_rewires += 1
        content = content[:start] + npc_block + content[end:]
        refit += 1

    # 2. Add new troops (Iron Hills Noble line) — insert before </NPCCharacters>
    added = 0
    new_blocks = []
    for troop_id, troop_xml in NEW_TROOPS:
        if f'id="{troop_id}"' in content:
            continue  # idempotent
        new_blocks.append(troop_xml)
        added += 1
        # Also apply upgrade_target chain (new troops are written with empty
        # <upgrade_targets></upgrade_targets>; rewrite to the spec chain)
    if new_blocks:
        joined = "".join(new_blocks)
        # Rewrite upgrade_targets on the new blocks before insertion
        for troop_id, targets in UPGRADE_TARGETS.items():
            if not targets:
                continue
            if f'id="{troop_id}"' not in joined:
                continue
            lines = '\n'.join(
                f'            <upgrade_target\n                id="NPCCharacter.{t}" />'
                for t in targets
            )
            target_block = f'<upgrade_targets>\n{lines}\n        </upgrade_targets>'
            # Replace the empty upgrade_targets on this specific troop block
            block_pat = re.compile(
                r'(<NPCCharacter\s+id="' + re.escape(troop_id) + r'".*?)'
                r'<upgrade_targets></upgrade_targets>'
                r'(.*?</NPCCharacter>)',
                re.DOTALL,
            )
            new_joined, n = block_pat.subn(
                lambda m: m.group(1) + target_block + m.group(2),
                joined, count=1,
            )
            if n:
                joined = new_joined
                upgrade_rewires += 1
        content = content.replace(
            '</NPCCharacters>',
            joined + '\n</NPCCharacters>',
            1,
        )

    # 3. Delete redundant troops (empty in this revamp) + remove orphan upgrade_target refs
    deleted = 0
    for troop_id in DELETE_IDS:
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            print(f"  WARN: delete target {troop_id} not found", file=sys.stderr)
            continue
        content = content[:start] + content[end:]
        deleted += 1
    content, removed_refs = remove_upgrade_targets_to(content, DELETE_IDS)

    print(f"\nErebor / Iron Hills / Ironpass troop revamp (issue #212):")
    print(f"  Equipment refits:        {refit}/{len(EQUIPMENT)}")
    print(f"  Upgrade chain rewires:   {upgrade_rewires}")
    print(f"  New troops added:        {added}/{len(NEW_TROOPS)}")
    print(f"  Troops deleted:          {deleted}/{len(DELETE_IDS)}")
    print(f"  Orphan upgrade refs removed: {removed_refs}")
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
    p = argparse.ArgumentParser(
        description="Apply Erebor/Iron Hills/Ironpass troop revamp (issue #212)"
    )
    p.add_argument("--dry-run", action="store_true", help="Compute changes but don't write")
    p.add_argument("--apply", action="store_true", help="Apply changes to troops_erebor.xml")
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
