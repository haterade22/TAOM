#!/usr/bin/env python3
"""Apply KEYforce Gondor armor revamp to troops_gondor.xml.

Issue: #99
Source-of-truth: E:\\repos\\lotraom-assets\\tools\\gondor_armors_and_troops.txt

Mechanically replaces each troop's <EquipmentRoster> with the new loadout
per the artist's per-tier armor + weapon guide. Preserves Horse/HorseHarness
lines on cavalry. Deletes 5 retired Lossarnach extras and removes any
upgrade_target references pointing at deleted ids.

Usage:
    python tools/apply_gondor_troop_revamp.py --dry-run
    python tools/apply_gondor_troop_revamp.py --apply
"""
import argparse
import os
import re
import sys
from typing import Dict, List, Tuple

TROOPS_FILE = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Main", "_Module", "ModuleData", "troops", "troops_gondor.xml"
)

# Slot ordering in output XML — matches existing file convention
SLOT_ORDER = [
    "Item0", "Item1", "Item2", "Item3",
    "Head", "Body", "Cape", "Gloves", "Leg",
    "Horse", "HorseHarness", "HorseEquipmentRoster",
]


# =============================================================================
# DELETIONS — 5 retired Lossarnach extras (new mod version, no save-compat)
# =============================================================================
DELETE_IDS = {
    "gondor_loss_noble",
    "gondor_loss_axeman",
    "gondor_loss_axeguard",
    "gondor_loss_axewarden",
    "gondor_loss_high_axewarden",
}


# =============================================================================
# EQUIPMENT BLUEPRINTS — 112 troops, organized by region
# Each value is an ordered list of (slot, item_id) pairs.
# Horse/HorseHarness slots for cavalry are auto-preserved from existing XML
# unless explicitly included here.
# =============================================================================
EQUIPMENT: Dict[str, List[Tuple[str, str]]] = {

    # ---------------- ANORIEN (13) ----------------
    "gondor_ano_peasant": [
        ("Item0", "wm_gondor_sword_a01"),
        ("Item1", "gond_shield_three_black"),
        ("Body",  "sk_gd_ano_chainmail_half_b"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_ano_militia": [
        ("Item0", "wm_gondor_sword_a02"),
        ("Item1", "gond_shield_three_black"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_ano_chainmail_half_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_ano_footman": [
        ("Item0", "wm_gondor_sword_a03"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_ano_chainmail_full_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_ano_guardsman": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_ano_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_ano_infantry": [
        ("Item0", "wm_gondor_spear_a"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_sword_a07"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_ano_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_ano_vet_infantry": [
        ("Item0", "wm_gondor_spear_a"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_sword_a08"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_ano_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_ano_mt_cavalry": [
        ("Item0", "wm_gondor_spear"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_cav_helmet_heavy_a"),
        ("Body",  "sk_gd_ano_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_ano_mt_heavy_cavalry": [
        ("Item0", "wm_gondor_spear"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_cav_helmet_heavy_b"),
        ("Body",  "sk_gd_ano_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_ano_mt_knight": [
        ("Item0", "wm_gondor_spear"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_cav_helmet_heavy_b"),
        ("Body",  "sk_gd_ano_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_ano_archer_militia": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_ano_chainmail_half_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_ano_skirmisher": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_ano_chainmail_full_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_ano_bowman": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_ano_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_ano_archer": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_ano_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- LOSSARNACH (9 mainline) ----------------
    "gondor_loss_lumberman": [
        ("Item0", "wm_gondor_lossarnach_1h_axe_a"),
        ("Item1", "gond_shield_one_greyscale"),
        ("Body",  "sk_gd_los_inf_chainmail_a"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_loss_woodsman": [
        ("Item0", "wm_gondor_lossarnach_1h_axe_a"),
        ("Item1", "gond_shield_one_greyscale"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_los_inf_chainmail_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_loss_skirmisher": [
        ("Item0", "wm_gondor_lossarnach_1h_axe_a"),
        ("Item1", "wm_gondor_lossarnach_1h_axe_b"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_los_inf_chest_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_loss_axe_thrower": [
        ("Item0", "wm_gondor_lossarnach_1h_axe_a"),
        ("Item1", "wm_gondor_lossarnach_1h_axe_b"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_los_inf_chest_med_a"),
        ("Cape",  "sk_gd_los_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_los_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_loss_vet_axe_thrower": [
        ("Item0", "wm_gondor_lossarnach_1h_axe_a"),
        ("Item1", "wm_gondor_lossarnach_1h_axe_b"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_los_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_los_pauld_inf_elite_a"),
        ("Gloves","sk_gd_los_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_loss_axebearer": [
        ("Item0", "wm_gondor_lossarnach_1h_axe_a"),
        ("Item1", "gond_shield_one_greyscale"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_los_inf_chest_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_loss_vet_axebearer": [
        ("Item0", "wm_gondor_lossarnach_1h_axe_a"),
        ("Item1", "gond_shield_one_greyscale"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_los_inf_chest_med_a"),
        ("Cape",  "sk_gd_los_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_los_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_loss_guard": [
        ("Item0", "wm_gondor_lossarnach_2h_axe_a"),
        ("Item1", "gond_shield_one_greyscale"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_los_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_los_pauld_inf_elite_a"),
        ("Gloves","sk_gd_los_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_loss_vet_guard": [
        ("Item0", "wm_gondor_lossarnach_2h_axe_a"),
        ("Item1", "gond_shield_one_greyscale"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_los_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_los_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_los_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- PINNATH GELIN (8) ----------------
    "gondor_pg_volunteer": [
        ("Item0", "wm_gondor_spear"),
        ("Body",  "sk_gd_pin_chainmail_a"),
    ],
    "gondor_pg_militia": [
        ("Item0", "wm_gondor_spear"),
        ("Head",  "sk_gd_pin_inf_helmet_med_a"),
        ("Body",  "sk_gd_pin_chainmail_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_pg_footman": [
        ("Item0", "wm_gondor_spear"),
        ("Item1", "gond_shield_three_green"),
        ("Head",  "sk_gd_pin_inf_helmet_med_a"),
        ("Body",  "sk_gd_pin_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_pg_archer": [
        ("Item0", "composite_steppe_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_pin_arc_helmet_heavy_a"),
        ("Body",  "sk_gd_pin_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_pg_vet_archer": [
        ("Item0", "steppe_war_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_pin_arc_helmet_heavy_b"),
        ("Body",  "sk_gd_pin_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_pg_spearman": [
        ("Item0", "wm_gondor_spear"),
        ("Item1", "gond_shield_three_green"),
        ("Head",  "sk_gd_pin_spear_helmet_heavy_a"),
        ("Body",  "sk_gd_pin_inf_chest_med_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_pg_vet_spearman": [
        ("Item0", "wm_gondor_pg_speara"),
        ("Item1", "gond_shield_three_green"),
        ("Head",  "sk_gd_pin_spear_helmet_heavy_b"),
        ("Body",  "sk_gd_pin_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_pg_spearwarden": [
        ("Item0", "wm_gondor_pg_speara"),
        ("Item1", "gond_shield_three_green"),
        ("Head",  "sk_gd_pin_spear_helmet_heavy_b"),
        ("Body",  "sk_gd_pin_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- ANFALAS (8) ----------------
    "gondor_anf_levy": [
        ("Item0", "wm_gondor_sword_a01"),
        ("Body",  "sk_gd_anf_inf_chainmail_a"),
    ],
    "gondor_anf_militia": [
        ("Item0", "wm_gondor_sword_a02"),
        ("Head",  "sk_gd_anf_inf_helmet_med_a"),
        ("Body",  "sk_gd_anf_inf_chainmail_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_anf_footman": [
        ("Item0", "eastern_mace"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_anf_inf_helmet_med_b"),
        ("Body",  "sk_gd_anf_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_anf_guardsman": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_anf_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_anf_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_anf_infantry": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_anf_inf_helmet_heavy_b"),
        ("Body",  "sk_gd_anf_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_anf_vet_infantry": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_anf_inf_helmet_heavy_c"),
        ("Body",  "sk_gd_anf_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_anf_cavalry": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_anf_cav_helmet_heavy_a"),
        ("Body",  "sk_gd_anf_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_anf_vet_cavalry": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_anf_cav_helmet_heavy_b"),
        ("Body",  "sk_gd_anf_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],

    # ---------------- LEBENNIN (8) ----------------
    "gondor_leb_militia": [
        ("Item0", "wm_gondor_sword_a02"),
        ("Body",  "sk_gd_leb_chainmail_a"),
    ],
    "gondor_leb_skirmisher": [
        ("Item0", "wm_gondor_sword_a03"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Body",  "sk_gd_leb_chainmail_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_leb_archer": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Item3", "wm_gondor_sword_a03"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_leb_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_cape_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_leb_vet_archer": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Item3", "wm_gondor_sword_a05"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_leb_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_cape_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_leb_longbowman": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Item3", "wm_gondor_sword_a07"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_leb_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_cape_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_leb_infantry": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "javelin_1_t3"),
        ("Head",  "sk_gd_ano_cav_helmet_heavy_a"),
        ("Body",  "sk_gd_leb_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_leb_vet_infantry": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "javelin_1_t3"),
        ("Head",  "sk_gd_ano_cav_helmet_heavy_b"),
        ("Body",  "sk_gd_leb_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_leb_sea_guard": [
        ("Item0", "wm_gondor_sword_a09"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "javelin_1_t3"),
        ("Item3", "javelin_1_t3"),
        ("Head",  "sk_gd_ano_cav_helmet_heavy_b"),
        ("Body",  "sk_gd_leb_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- BELFALAS (10) ----------------
    "gondor_bel_recruit": [
        ("Item0", "wm_gondor_sword_a01"),
        ("Body",  "sk_gd_bel_inf_chainmail_a"),
    ],
    "gondor_bel_hunter": [
        ("Item0", "composite_steppe_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Item3", "wm_gondor_sword_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_bel_inf_chainmail_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_bel_bowman": [
        ("Item0", "composite_steppe_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Item3", "wm_gondor_sword_a03"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_bel_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    # T4/T5 archer: drop second arrow stack to keep within Item0-Item3 (4 weapon slots max)
    "gondor_bel_archer": [
        ("Item0", "composite_steppe_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "wm_gondor_sword_a05"),
        ("Item3", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_bel_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_bel_vet_archer": [
        ("Item0", "composite_steppe_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "wm_gondor_sword_a07"),
        ("Item3", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_bel_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_bel_footman": [
        ("Item0", "wm_gondor_sword_a02"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_bel_inf_chainmail_b"),
        ("Cape",  "sk_gd_ano_cape_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_bel_soldier": [
        ("Item0", "wm_gondor_sword_a03"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_bel_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_cape_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_bel_infantry": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_bel_inf_chest_med_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_bel_vet_infantry": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_bel_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_bel_coastguard": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "wm_gondor_spear"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_bel_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- LAMEDON (5) ----------------
    "gondor_lam_clansman": [
        ("Item0", "wm_gondor_lamedon_1h_sword_a"),
        ("Head",  "sk_gd_lam_inf_helmet_light_a"),
        ("Body",  "sk_gd_lam_inf_chainmail_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_lam_footman": [
        ("Item0", "wm_gondor_lamedon_1h_sword_a"),
        ("Item1", "gond_shield_one_red"),
        ("Head",  "sk_gd_lam_inf_helmet_med_a"),
        ("Body",  "sk_gd_lam_inf_chainmail_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_lam_swordman": [
        ("Item0", "wm_gondor_lamedon_1h_sword_a"),
        ("Item1", "gond_shield_one_red"),
        ("Head",  "sk_gd_lam_inf_helmet_heavy_a1"),
        ("Body",  "sk_gd_lam_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_lam_vet_swordman": [
        ("Item0", "wm_gondor_lamedon_1h_sword_a"),
        ("Item1", "gond_shield_one_red"),
        ("Head",  "sk_gd_lam_inf_helmet_heavy_a2"),
        ("Body",  "sk_gd_lam_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_lam_hill_warden": [
        ("Item0", "wm_gondor_lamedon_1h_sword_a"),
        ("Item1", "gond_shield_one_red"),
        ("Head",  "sk_gd_lam_inf_helmet_elite_a"),
        ("Body",  "sk_gd_lam_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- HARONDOR (9) ----------------
    "gondor_har_conscript": [
        ("Item0", "wm_gondor_sword_a01"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_har_inf_helmet_light_a"),
        ("Body",  "sk_gd_har_inf_chainmail_a"),
        ("Leg",   "sk_gd_ano_boots_a"),
    ],
    "gondor_har_militia": [
        ("Item0", "wm_gondor_sword_a02"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_har_inf_helmet_med_a"),
        ("Body",  "sk_gd_har_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_har_footman": [
        ("Item0", "wm_gondor_sword_a03"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_har_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_har_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_har_guardsman": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_har_inf_helmet_heavy_b"),
        ("Body",  "sk_gd_har_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_har_infantry": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "fine_glaive_t4"),
        ("Head",  "sk_gd_har_inf_helmet_elite_a"),
        ("Body",  "sk_gd_har_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_har_frontier_guard": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "fine_glaive_t4"),
        ("Head",  "sk_gd_har_inf_helmet_elite_b"),
        ("Body",  "sk_gd_har_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_har_skirmisher": [
        ("Item0", "western_javelin_3_t4"),
        ("Item1", "wm_gondor_sword_a03"),
        ("Head",  "sk_gd_har_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_har_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_har_vet_skirmisher": [
        ("Item0", "western_javelin_3_t4"),
        ("Item1", "western_javelin_3_t4"),
        ("Item2", "wm_gondor_sword_a05"),
        ("Item3", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_har_inf_helmet_heavy_b"),
        ("Body",  "sk_gd_har_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_har_javelineer": [
        ("Item0", "western_javelin_3_t4"),
        ("Item1", "western_javelin_3_t4"),
        ("Item2", "wm_gondor_sword_a07"),
        ("Item3", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_har_inf_helmet_elite_a"),
        ("Body",  "sk_gd_har_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- SERELOND (8) — full new sere set ----------------
    "gondor_ser_noble": [
        ("Item0", "wm_gondor_sword_a03"),
        ("Head",  "sk_gd_sere_helmet_heavy_a"),
        ("Body",  "sk_gd_sere_chest_med_a"),
        ("Cape",  "sk_gd_sere_pauld_light_a"),
        ("Gloves","sk_gd_sere_bracer_med_a"),
        ("Leg",   "sk_gd_sere_grvs_med_a"),
    ],
    "gondor_ser_veteran": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Head",  "sk_gd_sere_helmet_heavy_a"),
        ("Body",  "sk_gd_sere_chest_med_a"),
        ("Cape",  "sk_gd_sere_pauld_med_a"),
        ("Gloves","sk_gd_sere_bracer_med_a"),
        ("Leg",   "sk_gd_sere_grvs_med_a"),
    ],
    "gondor_ser_pikeman": [
        ("Item0", "fine_pike_t4"),
        ("Head",  "sk_gd_sere_helmet_elite_c"),
        ("Body",  "sk_gd_sere_chest_heavy_a"),
        ("Cape",  "sk_gd_sere_pauld_heavy_a"),
        ("Gloves","sk_gd_sere_bracer_heavy_a"),
        ("Leg",   "sk_gd_sere_grvs_heavy_a"),
    ],
    "gondor_ser_pikewarden": [
        ("Item0", "fine_pike_t4"),
        ("Head",  "sk_gd_sere_helmet_elite_c"),
        ("Body",  "sk_gd_sere_chest_heavy_b"),
        ("Cape",  "sk_gd_sere_pauld_heavy_a"),
        ("Gloves","sk_gd_sere_bracer_heavy_a"),
        ("Leg",   "sk_gd_sere_grvs_elite_a"),
    ],
    "gondor_ser_phalanx": [
        ("Item0", "vlandia_pike_1_t5"),
        ("Head",  "sk_gd_sere_helmet_elite_c"),
        ("Body",  "sk_gd_sere_chest_elite_a"),
        ("Cape",  "sk_gd_sere_pauld_elite_a"),
        ("Gloves","sk_gd_sere_bracer_elite_a"),
        ("Leg",   "sk_gd_sere_grvs_elite_a"),
    ],
    "gondor_ser_maceman": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_sere_helmet_elite_a"),
        ("Body",  "sk_gd_sere_chest_heavy_a"),
        ("Cape",  "sk_gd_sere_pauld_heavy_a"),
        ("Gloves","sk_gd_sere_bracer_heavy_a"),
        ("Leg",   "sk_gd_sere_grvs_heavy_a"),
    ],
    "gondor_ser_vet_maceman": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "empire_spear_1_t3"),
        ("Head",  "sk_gd_sere_helmet_elite_b"),
        ("Body",  "sk_gd_sere_chest_heavy_b"),
        ("Cape",  "sk_gd_sere_pauld_cape_heavy_a"),
        ("Gloves","sk_gd_sere_bracer_heavy_a"),
        ("Leg",   "sk_gd_sere_grvs_elite_a"),
    ],
    "gondor_ser_coastwarden": [
        ("Item0", "wm_gondor_sword_a09"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "empire_spear_1_t3"),
        ("Head",  "sk_gd_sere_helmet_elite_b"),
        ("Body",  "sk_gd_sere_chest_elite_a"),
        ("Cape",  "sk_gd_sere_pauld_cape_elite_a"),
        ("Gloves","sk_gd_sere_bracer_elite_a"),
        ("Leg",   "sk_gd_sere_grvs_elite_a"),
    ],

    # ---------------- CAIR ANDROS (8) ----------------
    "gondor_ca_noble": [
        ("Item0", "wm_gondor_sword_a03"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_cair_chainmail_half_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_ca_veteran": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a_cair_andros"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_cair_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_b"),
        ("Gloves","sk_gd_cair_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_ca_spearman": [
        ("Item0", "fine_pike_t4"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_cair_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_cair_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_ca_pikeman": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "fine_pike_t4"),
        ("Head",  "sk_gd_cair_noble_helmet_heavy_a"),
        ("Body",  "sk_gd_cair_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_cair_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_ca_pikewarden": [
        ("Item0", "wm_gondor_sword_a09"),
        ("Item1", "vlandia_pike_1_t5"),
        ("Head",  "sk_gd_cair_noble_helmet_heavy_b"),
        ("Body",  "sk_gd_cair_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_ano_pauld_cape_noble_elite_a"),
        ("Gloves","sk_gd_cair_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_ca_infantry": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a_cair_andros"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_cair_inf_chest_med_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_cair_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_ca_guard": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_a_cair_andros"),
        ("Head",  "sk_gd_cair_ward_helmet_heavy_a"),
        ("Body",  "sk_gd_cair_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_elite_a"),
        ("Gloves","sk_gd_cair_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_ca_warden": [
        ("Item0", "wm_gondor_sword_a09"),
        ("Item1", "wm_gondor_shield_a_cair_andros"),
        ("Head",  "sk_gd_cair_ward_helmet_heavy_b"),
        ("Body",  "sk_gd_cair_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_ano_pauld_cape_noble_elite_b"),
        ("Gloves","sk_gd_cair_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],

    # ---------------- OSGILIATH (7) ----------------
    "gondor_osg_veteran": [
        ("Item0", "wm_gondor_sword_a03"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_med_a"),
        ("Body",  "sk_gd_ano_chainmail_half_b"),
        ("Cape",  "sk_gd_ano_pauld_inf_med_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_osg_skirmisher": [
        ("Item0", "wm_gondor_sword_a05"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_ano_inf_helmet_heavy_a"),
        ("Body",  "sk_gd_osg_inf_chest_med_a"),
        ("Cape",  "sk_gd_ano_pauld_inf_heavy_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_osg_infantry": [
        ("Item0", "wm_gondor_sword_a07"),
        ("Item1", "wm_gondor_shield_a02"),
        ("Item2", "fine_pike_t4"),
        ("Head",  "sk_gd_osg_noble_helmet_heavy_a"),
        ("Body",  "sk_gd_osg_inf_chest_med_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_osg_bracer_noble_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_osg_guard": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "gond_shield_four_black"),
        ("Item2", "fine_pike_t4"),
        ("Head",  "sk_gd_osg_noble_helmet_heavy_a"),
        ("Body",  "sk_gd_osg_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_osg_bracer_noble_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_osg_dome_guard": [
        ("Item0", "wm_gondor_sword_a09"),
        ("Item1", "gond_shield_four_black"),
        ("Item2", "vlandia_pike_1_t5"),
        ("Item3", "javelin_2_t4"),
        ("Head",  "sk_gd_osg_noble_helmet_heavy_b"),
        ("Body",  "sk_gd_osg_inf_chest_heavy_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_osg_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    # Archer line: shield + sword + bow + arrows. Drop second arrow stack to fit Item0-Item3.
    "gondor_osg_archer": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "wm_gondor_sword_a07"),
        ("Item3", "wm_gondor_shield_a02"),
        ("Head",  "sk_gd_osg_ward_helmet_heavy_a"),
        ("Body",  "sk_gd_osg_inf_chest_med_b"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_osg_bracer_noble_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_osg_longbowman": [
        ("Item0", "wm_gondor_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "wm_gondor_sword_a08"),
        ("Item3", "gond_shield_four_black"),
        ("Head",  "sk_gd_osg_ward_helmet_heavy_b"),
        ("Body",  "sk_gd_osg_inf_chest_heavy_a"),
        ("Cape",  "sk_gd_osg_pauld_cape_inf_elite_b"),
        ("Gloves","sk_gd_osg_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],

    # ---------------- MINAS ITHIL (7) ----------------
    "gondor_ith_watcher": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_a_minas_ithil"),
        ("Head",  "sk_gd_ano_noble_helmet_med_a"),
        ("Body",  "sk_gd_ano_chainmail_full_b"),
        ("Cape",  "sk_gd_ano_cape_noble_a"),
        ("Gloves","sk_gd_ano_bracer_noble_med_a"),
        ("Leg",   "sk_gd_ano_grvs_noble_light_a"),
    ],
    "gondor_ith_veteran": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_a_minas_ithil"),
        ("Head",  "sk_gd_ano_noble_helmet_heavy_a"),
        ("Body",  "sk_gd_ith_chest_noble_med_a"),
        ("Cape",  "sk_gd_ano_cape_noble_b"),
        ("Gloves","sk_gd_ano_bracer_noble_med_a"),
        ("Leg",   "sk_gd_ano_grvs_noble_med_a"),
    ],
    "gondor_ith_sergeant": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "wm_gondor_shield_d_new_minas_ithil"),
        ("Item2", "fine_pike_t4"),
        ("Head",  "sk_gd_ano_noble_helmet_heavy_b"),
        ("Body",  "sk_gd_ith_chest_noble_med_b"),
        ("Cape",  "sk_gd_ano_pauld_cape_noble_elite_a"),
        ("Gloves","sk_gd_ano_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_noble_med_a"),
    ],
    "gondor_ith_captain": [
        ("Item0", "wm_gondor_sword_a10"),
        ("Item1", "wm_gondor_shield_d_new_minas_ithil"),
        ("Item2", "vlandia_pike_1_t5"),
        ("Head",  "sk_gd_ano_noble_helmet_heavy_b"),
        ("Body",  "sk_gd_ith_chest_noble_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_cape_noble_elite_b"),
        ("Gloves","sk_gd_ano_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_noble_heavy_a"),
    ],
    "gondor_ith_longbowman": [
        ("Item0", "wm_ithilien_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Head",  "sk_gd_osg_ward_helmet_heavy_a"),
        ("Body",  "sk_gd_ith_chest_noble_med_b"),
        ("Cape",  "sk_gd_ano_pauld_cape_noble_elite_a"),
        ("Gloves","sk_gd_ano_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_noble_med_a"),
    ],
    "gondor_ith_sharpshooter": [
        ("Item0", "wm_ithilien_bow_b"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Head",  "sk_gd_osg_ward_helmet_heavy_b"),
        ("Body",  "sk_gd_ith_chest_noble_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_cape_noble_elite_b"),
        ("Gloves","sk_gd_ano_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_noble_heavy_a"),
    ],
    "gondor_ith_moon_guard": [
        ("Item0", "gondor_steel_bow_b"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Head",  "sk_gd_ith_noble_helmet_heavy_a"),
        ("Body",  "sk_gd_ith_chest_noble_heavy_b"),
        ("Cape",  "sk_gd_ano_pauld_cape_noble_elite_a"),
        ("Gloves","sk_gd_ano_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_noble_heavy_a"),
    ],

    # ---------------- MINAS TIRITH (7) ----------------
    "gondor_mt_trainee": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "gond_shield_four_black"),
        ("Head",  "sk_gd_mns_noble_helmet_med_a"),
        ("Body",  "sk_gd_mns_citadel_chest_med_a"),
        ("Cape",  "sk_gd_ano_cape_noble_b"),
        ("Gloves","sk_gd_ano_gloves_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_light_a"),
    ],
    "gondor_mt_veteran": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "gond_shield_four_black"),
        ("Head",  "sk_gd_mns_noble_helmet_heavy_a"),
        ("Body",  "sk_gd_mns_citadel_chest_med_a"),
        ("Cape",  "sk_gd_ano_cape_noble_b"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_mt_sergeant": [
        ("Item0", "wm_gondor_sword_a08"),
        ("Item1", "gond_shield_four_black"),
        ("Item2", "fine_pike_t4"),
        ("Head",  "sk_gd_mns_cita_helmet_heavy_a"),
        ("Body",  "sk_gd_mns_citadel_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_mt_captain": [
        ("Item0", "wm_gondor_sword_a10"),
        ("Item1", "gond_shield_four_black"),
        ("Item2", "vlandia_pike_1_t5"),
        ("Head",  "sk_gd_mns_cita_helmet_heavy_b"),
        ("Body",  "sk_gd_mns_citadel_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_mt_fountain_guard": [
        ("Item0", "wm_gondor_sword_a10"),
        ("Item1", "gond_shield_four_black"),
        ("Item2", "vlandia_pike_1_t5"),
        ("Head",  "sk_gd_mns_fount_helmet_heavy_a"),
        ("Body",  "sk_gd_mns_fount_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_cape_fount_elite_a"),
        ("Gloves","sk_gd_ano_bracer_noble_heavy_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
    "gondor_mt_longbowman": [
        ("Item0", "wm_ithilien_bow"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Item3", "wm_gondor_sword_a08"),
        ("Head",  "sk_gd_mns_cita_helmet_heavy_a"),
        ("Body",  "sk_gd_mns_citadel_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_med_a"),
    ],
    "gondor_mt_sharpshooter": [
        ("Item0", "wm_ithilien_bow_b"),
        ("Item1", "bodkin_arrows_a"),
        ("Item2", "bodkin_arrows_a"),
        ("Item3", "wm_gondor_sword_a10"),
        ("Head",  "sk_gd_mns_cita_helmet_heavy_b"),
        ("Body",  "sk_gd_mns_citadel_chest_heavy_a"),
        ("Cape",  "sk_gd_ano_pauld_cape_inf_elite_a"),
        ("Gloves","sk_gd_ano_bracer_inf_med_a"),
        ("Leg",   "sk_gd_ano_grvs_inf_heavy_a"),
    ],
}


# =============================================================================
# Apply logic
# =============================================================================

NPC_BLOCK_RE_TMPL = (
    r'  <NPCCharacter\s+\n'
    r'      id="{id}"'
    r'.*?'
    r'  </NPCCharacter>\n\n?'
)

EQUIP_ROSTER_RE = re.compile(
    r'(\s+)<EquipmentRoster>(.*?)</EquipmentRoster>',
    re.DOTALL,
)

UPGRADE_TARGET_RE_TMPL = r'\s*<upgrade_target id="NPCCharacter\.{id}" />'


def find_npc_block(content: str, troop_id: str) -> Tuple[int, int]:
    """Return (start, end) indices of the <NPCCharacter id="X">...</NPCCharacter> block."""
    pattern = re.compile(
        r'  <NPCCharacter\s+id="' + re.escape(troop_id) + r'"'
        r'.*?'
        r'  </NPCCharacter>',
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


def extract_horse_lines(equip_block: str) -> List[str]:
    """Pull existing Horse / HorseHarness lines for cavalry preservation."""
    lines = []
    for line in equip_block.splitlines():
        stripped = line.strip()
        if stripped.startswith('<equipment slot="Horse"') or \
           stripped.startswith('<equipment slot="HorseHarness"') or \
           stripped.startswith('<equipment slot="Mount"') or \
           stripped.startswith('<equipment slot="MountHarness"'):
            lines.append(line)
    return lines


def build_equipment_xml(slots: List[Tuple[str, str]], indent: str, horse_lines: List[str]) -> str:
    """Construct the <EquipmentRoster> body with given indent."""
    inner = "\n".join(
        f'{indent}  <equipment slot="{slot}" id="Item.{item_id}" />'
        for slot, item_id in slots
    )
    if horse_lines:
        inner += "\n" + "\n".join(horse_lines)
    return f'{indent}<EquipmentRoster>\n{inner}\n{indent}</EquipmentRoster>'


def replace_equipment(npc_block: str, troop_id: str, slots: List[Tuple[str, str]]) -> str:
    """Replace the <EquipmentRoster> within an NPCCharacter block."""
    m = EQUIP_ROSTER_RE.search(npc_block)
    if not m:
        raise RuntimeError(f"No <EquipmentRoster> found in {troop_id}")
    indent_after_newline = m.group(1).lstrip("\n")
    indent = indent_after_newline if indent_after_newline else "      "
    horse_lines = extract_horse_lines(m.group(2))
    new_roster = build_equipment_xml(slots, indent, horse_lines)
    return npc_block[:m.start()] + "\n" + new_roster + npc_block[m.end():]


def remove_upgrade_targets_to(content: str, deleted_ids: set) -> Tuple[str, int]:
    """Remove <upgrade_target id="NPCCharacter.X" /> lines pointing at deleted ids."""
    count = 0
    for tid in deleted_ids:
        pattern = re.compile(UPGRADE_TARGET_RE_TMPL.format(id=re.escape(tid)))
        new_content, n = pattern.subn('', content)
        if n:
            content = new_content
            count += n
    return content, count


def apply(dry_run: bool = False):
    if not os.path.exists(TROOPS_FILE):
        print(f"ERROR: {TROOPS_FILE} not found", file=sys.stderr)
        sys.exit(1)

    with open(TROOPS_FILE, "r", encoding="utf-8") as f:
        content = f.read()

    original_len = len(content)

    # 1. Replace equipment for each troop
    replaced = 0
    missing = []
    for troop_id, slots in EQUIPMENT.items():
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            missing.append(troop_id)
            continue
        npc_block = content[start:end]
        new_block = replace_equipment(npc_block, troop_id, slots)
        if new_block != npc_block:
            content = content[:start] + new_block + content[end:]
            replaced += 1

    # 2. Delete the 5 Lossarnach extras (full <NPCCharacter> blocks)
    deleted = 0
    for troop_id in DELETE_IDS:
        start, end = find_npc_block(content, troop_id)
        if start < 0:
            print(f"  WARN: delete target {troop_id} not found", file=sys.stderr)
            continue
        content = content[:start] + content[end:]
        deleted += 1

    # 3. Remove upgrade_target references to deleted ids
    content, removed_targets = remove_upgrade_targets_to(content, DELETE_IDS)

    print(f"\nSummary:")
    print(f"  Equipment replaced: {replaced}/{len(EQUIPMENT)}")
    print(f"  Troops deleted:     {deleted}/{len(DELETE_IDS)}")
    print(f"  upgrade_target refs removed: {removed_targets}")
    print(f"  File size: {original_len:,} -> {len(content):,} bytes")
    if missing:
        print(f"  MISSING (no NPCCharacter found): {missing}")

    if dry_run:
        print("\n(dry-run — no file written)")
        return

    with open(TROOPS_FILE, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"\nWrote: {TROOPS_FILE}")


def main():
    p = argparse.ArgumentParser(description="Apply Gondor armor revamp to troops_gondor.xml (issue #99)")
    p.add_argument("--dry-run", action="store_true", help="Compute changes but don't write")
    p.add_argument("--apply", action="store_true", help="Apply changes to troops_gondor.xml")
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
