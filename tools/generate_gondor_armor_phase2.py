#!/usr/bin/env python3
"""Phase-2 Gondor armor: author the missing regional armor families.

Issue: #99
Source-of-truth: E:\\repos\\lotraom-assets\\tools\\gondor_armors_and_troops.md

Adds ~99 items for Lossarnach, Pinnath Gelin, Harondor, Anfalas, Serelond,
Lebennin, Belfalas, Lamedon families, plus the KEYforce noble drops:
2026-06 (Anfalas/Lossarnach noble) and 2026-07 (Dol-Amroth, Linhir, Blackroot
Vale, Pinnath Gelin "Arndir" noble). Idempotent — skips items already present
in the target XML files. All 2026-07 item ids were verified against the geo-tpac
mesh TOCs (Assets/gondor_assets/{belfalas,pinnath_gelin,anfalas}/*_geo.tpac).

The 2026-07 drop RENAMED the Lond-Galen meshes: sk_gd_anf_lon_helmet_* ->
sk_gd_lon_helmet_* and sk_gd_lon_nob_chest_* -> sk_gd_lon_chest_* (the old names
are absent from every geo tpac). The Jun-06 "Anfalas Noble" entries below are
renamed accordingly; the old ids linger in the live XML as dead-mesh cruft and
their two troop refs (troops_gondor.xml) must be repointed to the new ids.

Usage:
    python tools/generate_gondor_armor_phase2.py --dry-run
    python tools/generate_gondor_armor_phase2.py --apply
    python tools/generate_gondor_armor_phase2.py --apply --armory-path "E:\\\\custom\\\\path"

Default --armory-path is the Steam install location.
"""
import argparse
import os
import sys
from dataclasses import dataclass
from typing import Optional

DEFAULT_ARMORY_BASE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\gondor"
)

# Stat tiers — copied verbatim from tools/generate_gondor_armor.py to keep
# phase-2 items numerically consistent with phase-1.
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


# =============================================================================
# HEAD ARMORS — 39 helmets across pin, har, anf, sere, lam
# =============================================================================
HEAD_ARMORS = [
    # ---- Pinnath Gelin (5) ----
    ArmorItem("sk_gd_pin_inf_helmet_med_a", "Pinnath Gelin Infantry Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_gd_pin_spear_helmet_heavy_a", "Pinnath Gelin Spearman Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_spear_helmet_heavy_b", "Pinnath Gelin Spearman Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_arc_helmet_heavy_a", "Pinnath Gelin Archer Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_arc_helmet_heavy_b", "Pinnath Gelin Archer Helmet B", "head", "heavy", "Plate"),

    # ---- Harondor (6) ----
    ArmorItem("sk_gd_har_inf_helmet_light_a", "Harondor Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_gd_har_inf_helmet_med_a", "Harondor Medium Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_gd_har_inf_helmet_heavy_a", "Harondor Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_har_inf_helmet_heavy_b", "Harondor Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_har_inf_helmet_elite_a", "Harondor Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_har_inf_helmet_elite_b", "Harondor Elite Helmet B", "head", "elite", "Plate"),

    # ---- Anfalas (7) ----
    ArmorItem("sk_gd_anf_inf_helmet_med_a", "Anfalas Infantry Helmet A", "head", "medium", "Plate"),
    ArmorItem("sk_gd_anf_inf_helmet_med_b", "Anfalas Infantry Helmet B", "head", "medium", "Plate"),
    ArmorItem("sk_gd_anf_inf_helmet_heavy_a", "Anfalas Infantry Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_anf_inf_helmet_heavy_b", "Anfalas Infantry Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_anf_inf_helmet_heavy_c", "Anfalas Infantry Heavy Helmet C", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_anf_cav_helmet_heavy_a", "Anfalas Cavalry Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_anf_cav_helmet_heavy_b", "Anfalas Cavalry Helmet B", "head", "heavy", "Plate"),

    # ---- Lond-Galen Noble (6) — KEYforce; mesh renamed sk_gd_anf_lon_helmet_* -> sk_gd_lon_helmet_* (2026-07) ----
    ArmorItem("sk_gd_lon_helmet_med_a", "Lond-Galen Noble Helmet A", "head", "medium", "Plate"),
    ArmorItem("sk_gd_lon_helmet_med_b", "Lond-Galen Noble Helmet B", "head", "medium", "Plate"),
    ArmorItem("sk_gd_lon_helmet_heavy_a", "Lond-Galen Noble Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lon_helmet_heavy_b", "Lond-Galen Noble Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lon_helmet_elite_a", "Lond-Galen Noble Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lon_helmet_elite_b", "Lond-Galen Noble Elite Helmet B", "head", "elite", "Plate"),

    # ---- Lossarnach Noble (6) — KEYforce noble drop 2026-06 ----
    ArmorItem("sk_gd_los_noble_helmet_med_a", "Lossarnach Noble Helmet A", "head", "medium", "Plate"),
    ArmorItem("sk_gd_los_noble_helmet_med_b", "Lossarnach Noble Helmet B", "head", "medium", "Plate"),
    ArmorItem("sk_gd_los_noble_helmet_heavy_a", "Lossarnach Noble Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_los_noble_helmet_heavy_b", "Lossarnach Noble Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_los_noble_helmet_elite_a", "Lossarnach Noble Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_los_noble_helmet_elite_b", "Lossarnach Noble Elite Helmet B", "head", "elite", "Plate"),

    # ---- Serelond (4) ----
    ArmorItem("sk_gd_sere_helmet_heavy_a", "Serelond Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_sere_helmet_elite_a", "Serelond Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_sere_helmet_elite_b", "Serelond Elite Helmet B", "head", "elite", "Plate"),
    ArmorItem("sk_gd_sere_helmet_elite_c", "Serelond Elite Helmet C", "head", "elite", "Plate"),

    # ---- Lamedon (17) ----
    # Note: lord-tier helmets are reserved for NPC heroes (per source-of-truth)
    # but still authored here so heroes can equip them.
    ArmorItem("sk_gd_lam_inf_helmet_light_a", "Lamedon Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_gd_lam_inf_helmet_med_a", "Lamedon Medium Helmet A", "head", "medium", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_med_b", "Lamedon Medium Helmet B", "head", "medium", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_heavy_a1", "Lamedon Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_heavy_b1", "Lamedon Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_heavy_c1", "Lamedon Heavy Helmet C", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_heavy_a2", "Lamedon Heavy Helmet A (Gold)", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_heavy_b2", "Lamedon Heavy Helmet B (Gold)", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_heavy_c2", "Lamedon Heavy Helmet C (Gold)", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_elite_a", "Lamedon Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_elite_b", "Lamedon Elite Helmet B", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_elite_c", "Lamedon Elite Helmet C", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lam_inf_helmet_elite_d", "Lamedon Elite Helmet D", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lam_nob_helmet_lord_a", "Lamedon Lord Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lam_nob_helmet_lord_b", "Lamedon Lord Helmet B", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lam_nob_helmet_lord_c", "Lamedon Lord Helmet C", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lam_nob_helmet_lord_d", "Lamedon Lord Helmet D", "head", "elite", "Plate"),

    # ---- Pinnath Gelin "Arndir" Noble (10) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_pin_noble_helmet_med_a", "Arndir Noble Helmet A", "head", "medium", "Plate"),
    ArmorItem("sk_gd_pin_noble_helmet_med_b", "Arndir Noble Helmet B", "head", "medium", "Plate"),
    ArmorItem("sk_gd_pin_noble_helmet_heavy_a", "Arndir Noble Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_noble_helmet_heavy_b", "Arndir Noble Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_noble_helmet_elite_a", "Arndir Noble Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_pin_noble_helmet_elite_b", "Arndir Noble Elite Helmet B", "head", "elite", "Plate"),
    ArmorItem("sk_gd_pin_noble_cav_helmet_heavy_a", "Arndir Knight Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_noble_cav_helmet_heavy_b", "Arndir Knight Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_noble_cav_helmet_elite_a", "Arndir Hill-Knight Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_pin_noble_cav_helmet_elite_b", "Arndir Hill-Knight Helmet B", "head", "elite", "Plate"),

    # ---- Blackroot Vale Noble (7) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_vale_hood_a", "Blackroot Vale Hood A", "head", "light", "Leather"),
    ArmorItem("sk_gd_vale_hood_b", "Blackroot Vale Hood B", "head", "light", "Leather"),
    ArmorItem("sk_gd_vale_helmet_med_a", "Blackroot Vale Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_gd_vale_helmet_heavy_a", "Blackroot Vale Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_vale_helmet_heavy_b", "Blackroot Vale Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_vale_helmet_elite_a", "Blackroot Vale Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_vale_helmet_elite_b", "Blackroot Vale Elite Helmet B", "head", "elite", "Plate"),

    # ---- Dol Amroth Noble (13) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_dol_helmet_med_a", "Dol Amroth Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_gd_dol_helmet_heavy_a", "Dol Amroth Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_dol_helmet_heavy_b", "Dol Amroth Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_dol_cav_helmet_elite_a", "Dol Amroth Knight Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_cav_helmet_elite_b", "Dol Amroth Knight Helmet B", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_cav_helmet_elite_c", "Dol Amroth Knight Helmet C", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_cav_helmet_elite_d", "Dol Amroth Knight Helmet D", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_inf_helmet_elite_a", "Dol Amroth Foot Knight Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_inf_helmet_elite_b", "Dol Amroth Foot Knight Helmet B", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_inf_helmet_elite_c", "Dol Amroth Foot Knight Helmet C", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_inf_helmet_elite_d", "Dol Amroth Foot Knight Helmet D", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_ward_helmet_elite_a", "Dol Amroth Warden Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_dol_ward_helmet_elite_b", "Dol Amroth Warden Helmet B", "head", "elite", "Plate"),

    # ---- Linhir Noble (6) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_lin_helmet_heavy_a", "Linhir Heavy Helmet A", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lin_helmet_heavy_b", "Linhir Heavy Helmet B", "head", "heavy", "Plate"),
    ArmorItem("sk_gd_lin_helmet_elite_a", "Linhir Elite Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lin_helmet_elite_b", "Linhir Elite Helmet B", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lin_helmet_lord_a", "Linhir Lord Helmet A", "head", "elite", "Plate"),
    ArmorItem("sk_gd_lin_helmet_lord_b", "Linhir Lord Helmet B", "head", "elite", "Plate"),
]


# =============================================================================
# BODY ARMORS — 42 chests
# =============================================================================
BODY_ARMORS = [
    # ---- Lossarnach (4) ----
    ArmorItem("sk_gd_los_inf_chainmail_a", "Lossarnach Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_los_inf_chest_med_a", "Lossarnach Armour", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_los_inf_chest_heavy_a", "Lossarnach Heavy Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_los_inf_chest_elite_a", "Lossarnach Elite Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),

    # ---- Lossarnach Noble (5) — KEYforce noble drop 2026-06 ----
    ArmorItem("sk_gd_los_nob_chainmail_a", "Lossarnach Noble Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_los_nob_chest_med_a", "Lossarnach Noble Armour", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_los_nob_chest_heavy_a", "Lossarnach Noble Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_los_nob_chest_heavy_b", "Lossarnach Noble Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_los_nob_chest_lord_a", "Lossarnach Noble Lord Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),

    # ---- Pinnath Gelin (6) ----
    ArmorItem("sk_gd_pin_chainmail_a", "Pinnath Gelin Chainmail A", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_pin_chainmail_b", "Pinnath Gelin Chainmail B", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_pin_inf_chest_med_a", "Pinnath Gelin Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_pin_inf_chest_med_b", "Pinnath Gelin Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_pin_inf_chest_heavy_a", "Pinnath Gelin Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_pin_inf_chest_heavy_b", "Pinnath Gelin Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),

    # ---- Harondor (3) ----
    ArmorItem("sk_gd_har_inf_chainmail_a", "Harondor Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_har_inf_chest_med_a", "Harondor Armour", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_har_inf_chest_heavy_a", "Harondor Heavy Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),

    # ---- Anfalas (6) ----
    ArmorItem("sk_gd_anf_inf_chainmail_a", "Anfalas Chainmail A", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_anf_inf_chainmail_b", "Anfalas Chainmail B", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_anf_inf_chest_med_a", "Anfalas Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_anf_inf_chest_med_b", "Anfalas Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_anf_inf_chest_heavy_a", "Anfalas Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_anf_inf_chest_heavy_b", "Anfalas Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),

    # ---- Lond-Galen Noble (4) — KEYforce; mesh renamed sk_gd_lon_nob_chest_* -> sk_gd_lon_chest_* (2026-07) ----
    ArmorItem("sk_gd_lon_chainmail_a", "Lond-Galen Noble Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_lon_chest_med_a", "Lond-Galen Noble Armour", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_lon_chest_heavy_a", "Lond-Galen Noble Heavy Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_lon_chest_lord_a", "Lond-Galen Noble Lord Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),

    # ---- Serelond (4) ----
    ArmorItem("sk_gd_sere_chest_med_a", "Serelond Armour", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_sere_chest_heavy_a", "Serelond Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_sere_chest_heavy_b", "Serelond Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_sere_chest_elite_a", "Serelond Elite Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),

    # ---- Lebennin (6) ----
    ArmorItem("sk_gd_leb_chainmail_a", "Lebennin Chainmail A", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_leb_chainmail_b", "Lebennin Chainmail B", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_leb_inf_chest_med_a", "Lebennin Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_leb_inf_chest_med_b", "Lebennin Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_leb_inf_chest_heavy_a", "Lebennin Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_leb_inf_chest_heavy_b", "Lebennin Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),

    # ---- Belfalas (6) ----
    ArmorItem("sk_gd_bel_inf_chainmail_a", "Belfalas Chainmail A", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_bel_inf_chainmail_b", "Belfalas Chainmail B", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_bel_inf_chest_med_a", "Belfalas Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_bel_inf_chest_med_b", "Belfalas Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_bel_inf_chest_heavy_a", "Belfalas Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_bel_inf_chest_heavy_b", "Belfalas Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),

    # ---- Lamedon (7) ----
    ArmorItem("sk_gd_lam_inf_chainmail_a", "Lamedon Chainmail A", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_lam_inf_chainmail_b", "Lamedon Chainmail B", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_lam_inf_chest_med_a", "Lamedon Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_lam_inf_chest_med_b", "Lamedon Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_lam_inf_chest_heavy_a", "Lamedon Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_lam_inf_chest_heavy_b", "Lamedon Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_lam_inf_chest_lord_a", "Lamedon Lord Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),

    # ---- Pinnath Gelin "Arndir" Noble (6) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_pin_noble_chainmail_a", "Arndir Noble Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_pin_nob_chest_med_a", "Arndir Noble Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_pin_nob_chest_med_b", "Arndir Noble Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_pin_nob_chest_heavy_a", "Arndir Noble Heavy Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_pin_nob_chest_elite_a", "Arndir Hill-Knight Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),
    ArmorItem("sk_gd_pin_nob_chest_elite_b", "Arndir Lord Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),

    # ---- Blackroot Vale Noble (5) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_vale_chest_med_a", "Blackroot Vale Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_vale_chest_med_b", "Blackroot Vale Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_vale_chest_heavy_a", "Blackroot Vale Shadowhunter Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_vale_chest_heavy_b", "Blackroot Vale Ranger Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_vale_chest_heavy_c", "Blackroot Vale Ranger Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),

    # ---- Dol Amroth Noble (7) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_dol_chainmail_a", "Dol Amroth Noble Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_dol_chest_med_a", "Dol Amroth Noble Armour A", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_dol_chest_med_b", "Dol Amroth Noble Armour B", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_dol_chest_heavy_a", "Dol Amroth Noble Heavy Armour A", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_dol_chest_heavy_b", "Dol Amroth Noble Heavy Armour B", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_dol_chest_elite_a", "Dol Amroth Swan Knight Armour A", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),
    ArmorItem("sk_gd_dol_chest_elite_b", "Dol Amroth Swan Knight Armour B", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),

    # ---- Linhir Noble (4) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_lin_chainmail_a", "Linhir Noble Chainmail", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_gd_lin_chest_med_a", "Linhir Noble Armour", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_gd_lin_chest_heavy_a", "Linhir Noble Heavy Armour", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_gd_lin_chest_elite_a", "Linhir Lord Armour", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),
]


# =============================================================================
# SHOULDER ARMORS — 9 pauldrons
# =============================================================================
SHOULDER_ARMORS = [
    # ---- Lossarnach (3) ----
    ArmorItem("sk_gd_los_pauld_inf_heavy_a", "Lossarnach Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_los_pauld_inf_elite_a", "Lossarnach Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_los_pauld_cape_inf_elite_a", "Lossarnach Cape Pauldron", "shoulder", "elite", "Plate"),

    # ---- Lossarnach Noble (4) — KEYforce noble drop 2026-06 ----
    ArmorItem("sk_gd_los_pauld_nob_heavy_a", "Lossarnach Noble Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_los_pauld_nob_elite_a", "Lossarnach Noble Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_los_pauld_cape_nob_elite_a", "Lossarnach Noble Cape Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_los_pauld_cape_nob_lord_a", "Lossarnach Noble Lord Cape Pauldron", "shoulder", "elite", "Plate"),

    # ---- Serelond (6) ----
    ArmorItem("sk_gd_sere_pauld_light_a", "Serelond Light Pauldron", "shoulder", "light", "Plate"),
    ArmorItem("sk_gd_sere_pauld_med_a", "Serelond Medium Pauldron", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_sere_pauld_heavy_a", "Serelond Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_sere_pauld_elite_a", "Serelond Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_sere_pauld_cape_heavy_a", "Serelond Cape Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_sere_pauld_cape_elite_a", "Serelond Cape Elite Pauldron", "shoulder", "elite", "Plate"),

    # ---- Pinnath Gelin "Arndir" Noble (5) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_pin_pauld_noble_med_a", "Arndir Noble Pauldron A", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_pin_pauld_noble_med_b", "Arndir Noble Pauldron B", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_pin_pauld_noble_heavy_a", "Arndir Noble Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_pin_pauld_noble_elite_a", "Arndir Noble Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_pin_pauld_cape_noble_elite_a", "Arndir Noble Cape Pauldron", "shoulder", "elite", "Plate"),

    # ---- Blackroot Vale Noble (8) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_vale_cape_a", "Blackroot Vale Cape A", "shoulder", "light", "Plate"),
    ArmorItem("sk_gd_vale_cape_b", "Blackroot Vale Cape B", "shoulder", "light", "Plate"),
    ArmorItem("sk_gd_vale_cape_c", "Blackroot Vale Cape C", "shoulder", "light", "Plate"),
    ArmorItem("sk_gd_vale_pauld_med_a", "Blackroot Vale Pauldron", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_vale_pauld_heavy_a", "Blackroot Vale Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_vale_pauld_cape_med_a", "Blackroot Vale Cape Pauldron", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_vale_pauld_cape_heavy_a", "Blackroot Vale Heavy Cape Pauldron A", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_vale_pauld_cape_heavy_b", "Blackroot Vale Heavy Cape Pauldron B", "shoulder", "heavy", "Plate"),

    # ---- Dol Amroth Noble (6) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_dol_pauld_noble_med_a", "Dol Amroth Pauldron A", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_dol_pauld_noble_med_b", "Dol Amroth Pauldron B", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_dol_pauld_noble_heavy_a", "Dol Amroth Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_dol_pauld_noble_elite_a", "Dol Amroth Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_dol_pauld_cape_noble_heavy_a", "Dol Amroth Cape Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_dol_pauld_cape_noble_elite_a", "Dol Amroth Elite Cape Pauldron", "shoulder", "elite", "Plate"),

    # ---- Linhir Noble (5) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_lin_pauld_noble_med_a", "Linhir Pauldron", "shoulder", "medium", "Plate"),
    ArmorItem("sk_gd_lin_pauld_noble_heavy_a", "Linhir Heavy Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_lin_pauld_noble_elite_a", "Linhir Elite Pauldron", "shoulder", "elite", "Plate"),
    ArmorItem("sk_gd_lin_pauld_cape_noble_heavy_a", "Linhir Cape Pauldron", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_gd_lin_pauld_cape_noble_elite_a", "Linhir Elite Cape Pauldron", "shoulder", "elite", "Plate"),
]


# =============================================================================
# ARM ARMORS — 5 bracers
# =============================================================================
ARM_ARMORS = [
    # ---- Lossarnach (1) ----
    ArmorItem("sk_gd_los_bracer_inf_med_a", "Lossarnach Infantry Bracer", "arm", "medium", "Plate", covers_hands=True),

    # ---- Lossarnach Noble (3) — KEYforce noble drop 2026-06 ----
    ArmorItem("sk_gd_los_bracer_noble_med_a", "Lossarnach Noble Bracer", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_los_bracer_noble_heavy_a", "Lossarnach Noble Heavy Bracer", "arm", "heavy", "Plate", covers_hands=True),
    ArmorItem("sk_gd_los_bracer_noble_elite_a", "Lossarnach Noble Elite Bracer", "arm", "elite", "Plate", covers_hands=True),

    # ---- Serelond (4) ----
    ArmorItem("sk_gd_sere_bracer_med_a", "Serelond Medium Bracer", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_sere_bracer_heavy_a", "Serelond Heavy Bracer", "arm", "heavy", "Plate", covers_hands=True),
    ArmorItem("sk_gd_sere_bracer_elite_a", "Serelond Elite Bracer", "arm", "elite", "Plate", covers_hands=True),
    ArmorItem("sk_gd_sere_bracer_lord_a", "Serelond Lord Bracer", "arm", "elite", "Plate", covers_hands=True),

    # ---- Dol Amroth Noble (3) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_dol_bracer_med_a", "Dol Amroth Bracer", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_dol_bracer_heavy_a", "Dol Amroth Heavy Bracer", "arm", "heavy", "Plate", covers_hands=True),
    ArmorItem("sk_gd_dol_bracer_elite_a", "Dol Amroth Elite Bracer", "arm", "elite", "Plate", covers_hands=True),

    # ---- Linhir Noble (4) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_lin_bracer_med_a", "Linhir Bracer A", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_lin_bracer_med_b", "Linhir Bracer B", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_gd_lin_bracer_heavy_a", "Linhir Heavy Bracer", "arm", "heavy", "Plate", covers_hands=True),
    ArmorItem("sk_gd_lin_bracer_elite_a", "Linhir Elite Bracer", "arm", "elite", "Plate", covers_hands=True),
]


# =============================================================================
# LEG ARMORS — 4 greaves (Serelond only)
# =============================================================================
LEG_ARMORS = [
    ArmorItem("sk_gd_sere_grvs_med_a", "Serelond Medium Greaves", "leg", "medium", "Plate", covers_legs=True),
    ArmorItem("sk_gd_sere_grvs_heavy_a", "Serelond Heavy Greaves", "leg", "heavy", "Plate", covers_legs=True),
    ArmorItem("sk_gd_sere_grvs_elite_a", "Serelond Elite Greaves", "leg", "elite", "Plate", covers_legs=True),
    ArmorItem("sk_gd_sere_grvs_lord_a", "Serelond Lord Greaves", "leg", "elite", "Plate", covers_legs=True),

    # ---- Dol Amroth Noble (4) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_dol_grvs_light_a", "Dol Amroth Light Greaves", "leg", "light", "Plate", covers_legs=True),
    ArmorItem("sk_gd_dol_grvs_med_a", "Dol Amroth Greaves", "leg", "medium", "Plate", covers_legs=True),
    ArmorItem("sk_gd_dol_grvs_heavy_a", "Dol Amroth Heavy Greaves", "leg", "heavy", "Plate", covers_legs=True),
    ArmorItem("sk_gd_dol_grvs_elite_a", "Dol Amroth Elite Greaves", "leg", "elite", "Plate", covers_legs=True),

    # ---- Linhir Noble (3) — KEYforce noble drop 2026-07 ----
    ArmorItem("sk_gd_lin_grvs_light_a", "Linhir Light Greaves", "leg", "light", "Plate", covers_legs=True),
    ArmorItem("sk_gd_lin_grvs_med_a", "Linhir Greaves", "leg", "medium", "Plate", covers_legs=True),
    ArmorItem("sk_gd_lin_grvs_heavy_a", "Linhir Heavy Greaves", "leg", "heavy", "Plate", covers_legs=True),
]


SLOT_MAP = {
    "head":     (HEAD_ARMORS,     "head_armors.xml"),
    "body":     (BODY_ARMORS,     "body_armors.xml"),
    "shoulder": (SHOULDER_ARMORS, "shoulder_armors.xml"),
    "arm":      (ARM_ARMORS,      "arm_armors.xml"),
    "leg":      (LEG_ARMORS,      "leg_armors.xml"),
}

SLOT_TYPES = {
    "head":     ("HeadArmor", "head_armor"),
    "body":     ("BodyArmor", "body_armor"),
    "shoulder": ("Cape",      "head_armor"),
    "arm":      ("HandArmor", "hand_armor"),
    "leg":      ("LegArmor",  "leg_armor"),
}


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
        f'        name="{{=aom_{item.id}_name}}[Gondor] {item.display_name}"\n'
        f'        subtype="{subtype}"\n'
        f'        mesh="{item.id}"\n'
        f'        culture="Culture.gondor"\n'
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
            stats = STAT_TIERS[item.slot][item.tier]
            stat_str = ", ".join(f"{k}={v}" for k, v in stats.items() if k != "weight")
            print(f"  {item.id:50s} [{item.tier:6s}] {stat_str}")
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
            "    <!--  Phase-2 Gondor regional armor (issue #99, KEYforce)            -->\n"
            "    <!--  incl. noble drops: Anfalas/Lossarnach (2026-06),               -->\n"
            "    <!--  Dol-Amroth, Linhir, Blackroot Vale, Arndir/Pinnath (2026-07)   -->\n"
            "    <!-- ============================================================== -->\n\n"
        )
        content = content.replace(closing_tag, f"{section_comment}{new_xml}\n\n{closing_tag}")

        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)

        print(f"    -> wrote {len(new_items)} items to {filepath}")
        grand_added += len(new_items)

    print(f"\nDone. Added {grand_added} new items, skipped {grand_skipped} already present.")


def main():
    parser = argparse.ArgumentParser(description="Phase-2 Gondor armor item generator (issue #99)")
    parser.add_argument("--dry-run", action="store_true", help="List items only")
    parser.add_argument("--apply", action="store_true", help="Append to XML files")
    parser.add_argument("--armory-path", default=DEFAULT_ARMORY_BASE,
                        help=f"Path to LOTRLOME_Armory gondor/ (default: {DEFAULT_ARMORY_BASE})")
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
