#!/usr/bin/env python3
"""Author generic Orc + Morannon armor items per KEYforce Mordor spec.

Source-of-truth: E:\\repos\\lotraom-assets\\tools\\mordor_armor_and_troops.md
Issue: KEYforce mesh-first revamp (Mordor pass + Morannon sub-line)

Adds `sk_gn_orc_*` (generic cross-faction) and `sk_md_orc_*` (Mordor paint)
helmets, chests, pauldrons, bracers, and boots, PLUS the KEYforce Morannon
sub-line `sk_md_mor_*` (92 items: Arc/Inf/Pik combat lines across light/med/
heavy/elite tiers; bracers Arc+Inf only — Pik shares Inf bracers; greaves
shared across all lines). Black Uruk (`sk_uruk_mordor_*`) items are already
authored — not touched.

Per project convention:
- All helmets emit hair_cover_type="all" + beard_cover_type="all" (matches
  the in-game Armory state; explicit `hair_cover=` overrides apply).
- All bracers explicitly emit covers_hands="true|false" (no longer omitted
  when False) so Morannon bracers carry covers_hands="false" verbatim.

Usage:
    python tools/generate_mordor_armor.py --dry-run
    python tools/generate_mordor_armor.py --apply
"""
import argparse
import os
import sys
from dataclasses import dataclass
from typing import Optional

DEFAULT_ARMORY_BASE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\mordor"
)

# Stat tiers — same table used for Gondor, applied culture-wide for consistency.
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
    hair_cover: str = "all"
    beard_cover: str = "all"
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


def _h(prefix, shape, weight, variant, name, tier, material="Plate"):
    """Helmet shorthand."""
    return ArmorItem(
        f"{prefix}_{shape}_helmet_{weight}_{variant}",
        name,
        "head", tier, material,
    )


def _helmet_set(prefix, shape, label):
    """Generate a typical helmet set (light/med a-c/heavy a-c) for a shape.
    Returns items per actual list — caller passes which weights+variants to include."""
    return []


# =============================================================================
# GENERIC ORC HELMETS (sk_gn_orc_*) — cross-faction, no paint
# =============================================================================
HEAD_ARMORS = []

# Generic Light (cross-line base pool)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_helmet_light_a", "Orc Light Helmet I", "head", "light", "Leather"),
    ArmorItem("sk_gn_orc_helmet_light_b", "Orc Light Helmet II", "head", "light", "Leather"),
]

# Arc Helmets (archer shape)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_arc_helmet_med_a", "Orc Archer Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_arc_helmet_med_b", "Orc Archer Helmet II", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_arc_helmet_med_c", "Orc Archer Helmet III", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_arc_helmet_heavy_a", "Orc Archer Heavy Helmet I", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_arc_helmet_heavy_b", "Orc Archer Heavy Helmet II", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_arc_helmet_heavy_c", "Orc Archer Heavy Helmet III", "head", "heavy", "Plate"),
]

# Brt Helmets (brute shape)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_brt_helmet_light_a", "Orc Brute Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_gn_orc_brt_helmet_med_a", "Orc Brute Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_brt_helmet_med_b", "Orc Brute Helmet II", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_brt_helmet_heavy_a", "Orc Brute Heavy Helmet", "head", "heavy", "Plate"),
]

# Inf Helmets (infantry shape)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_inf_helmet_med_a", "Orc Infantry Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_inf_helmet_med_b", "Orc Infantry Helmet II", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_inf_helmet_med_c", "Orc Infantry Helmet III", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_inf_helmet_heavy_a", "Orc Infantry Heavy Helmet I", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_inf_helmet_heavy_b", "Orc Infantry Heavy Helmet II", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_inf_helmet_heavy_c", "Orc Infantry Heavy Helmet III", "head", "heavy", "Plate"),
]

# Mrd Helmets (Moria-shape — IS/DG only; included for cross-faction lookups)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_mrd_helmet_light_a", "Orc Moria Light Helmet I", "head", "light", "Leather"),
    ArmorItem("sk_gn_orc_mrd_helmet_light_b", "Orc Moria Light Helmet II", "head", "light", "Leather"),
    ArmorItem("sk_gn_orc_mrd_helmet_med_a", "Orc Moria Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_mrd_helmet_med_b", "Orc Moria Helmet II", "head", "medium", "Plate"),
]

# Pik Helmets (pike shape — MD/DG only)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_pik_helmet_med_a", "Orc Pike Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_pik_helmet_med_b", "Orc Pike Helmet II", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_pik_helmet_heavy_a", "Orc Pike Heavy Helmet I", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_pik_helmet_heavy_b", "Orc Pike Heavy Helmet II", "head", "heavy", "Plate"),
]

# Rdr Helmets (rider shape — MD/DG only)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_rdr_helmet_light_a", "Orc Rider Light Helmet I", "head", "light", "Leather"),
    ArmorItem("sk_gn_orc_rdr_helmet_light_b", "Orc Rider Light Helmet II", "head", "light", "Leather"),
    ArmorItem("sk_gn_orc_rdr_helmet_med_a", "Orc Rider Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_rdr_helmet_med_b", "Orc Rider Helmet II", "head", "medium", "Plate"),
]

# Sct Helmets (scout shape — MD/DG only)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_sct_helmet_med_a", "Orc Scout Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_sct_helmet_med_b", "Orc Scout Helmet II", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_sct_helmet_heavy_a", "Orc Scout Heavy Helmet I", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_sct_helmet_heavy_b", "Orc Scout Heavy Helmet II", "head", "heavy", "Plate"),
]

# Sly Helmets (Sallet-style — IS/DG only)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_sly_helmet_med_a", "Orc Sallet Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_sly_helmet_med_b", "Orc Sallet Helmet II", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_sly_helmet_heavy_a", "Orc Sallet Heavy Helmet I", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_sly_helmet_heavy_b", "Orc Sallet Heavy Helmet II", "head", "heavy", "Plate"),
]

# Vgd Helmets (vanguard shape — MD/IS only)
HEAD_ARMORS += [
    ArmorItem("sk_gn_orc_vgd_helmet_med_a", "Orc Vanguard Helmet I", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_vgd_helmet_med_b", "Orc Vanguard Helmet II", "head", "medium", "Plate"),
    ArmorItem("sk_gn_orc_vgd_helmet_heavy_a", "Orc Vanguard Heavy Helmet I", "head", "heavy", "Plate"),
    ArmorItem("sk_gn_orc_vgd_helmet_heavy_b", "Orc Vanguard Heavy Helmet II", "head", "heavy", "Plate"),
]

# ===== Mordor-painted orc helmets (sk_md_orc_*) =====
HEAD_ARMORS += [
    ArmorItem("sk_md_orc_arc_helmet_med_a", "Mordor Orc Archer Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_md_orc_arc_helmet_heavy_a", "Mordor Orc Archer Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_md_orc_brt_helmet_light_a", "Mordor Orc Brute Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_md_orc_brt_helmet_med_a", "Mordor Orc Brute Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_md_orc_brt_helmet_heavy_a", "Mordor Orc Brute Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_md_orc_inf_helmet_med_a", "Mordor Orc Infantry Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_md_orc_inf_helmet_heavy_a", "Mordor Orc Infantry Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_md_orc_pik_helmet_med_a", "Mordor Orc Pike Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_md_orc_pik_helmet_heavy_a", "Mordor Orc Pike Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_md_orc_rdr_helmet_light_a", "Mordor Orc Rider Light Helmet", "head", "light", "Leather"),
    ArmorItem("sk_md_orc_rdr_helmet_med_a", "Mordor Orc Rider Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_md_orc_sct_helmet_med_a", "Mordor Orc Scout Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_md_orc_sct_helmet_heavy_a", "Mordor Orc Scout Heavy Helmet", "head", "heavy", "Plate"),
    ArmorItem("sk_md_orc_vgd_helmet_med_a", "Mordor Orc Vanguard Helmet", "head", "medium", "Plate"),
    ArmorItem("sk_md_orc_vgd_helmet_heavy_a", "Mordor Orc Vanguard Heavy Helmet", "head", "heavy", "Plate"),
]


# =============================================================================
# MORDOR ORC CHESTS — sk_md_orc_*
# =============================================================================
BODY_ARMORS = [
    # Arc chest (tunic-based, lighter)
    ArmorItem("sk_md_orc_arc_chest_light_a", "Mordor Orc Tunic I", "body", "light", "Cloth", covers_body=True),
    ArmorItem("sk_md_orc_arc_chest_light_b", "Mordor Orc Tunic II", "body", "light", "Cloth", covers_body=True),
    ArmorItem("sk_md_orc_arc_chest_light_c", "Mordor Orc Tunic III", "body", "light", "Cloth", covers_body=True),
    ArmorItem("sk_md_orc_arc_chest_med_a", "Mordor Orc Leather Chest", "body", "medium", "Leather", covers_body=True, arm_armor_stat=8),
    ArmorItem("sk_md_orc_arc_chest_med_b", "Mordor Orc Scale Chest", "body", "medium", "Plate", covers_body=True, arm_armor_stat=8),
    ArmorItem("sk_md_orc_arc_chest_med_c", "Mordor Orc Strap Chest", "body", "medium", "Leather", covers_body=True, arm_armor_stat=8),
    ArmorItem("sk_md_orc_arc_chest_heavy_a", "Mordor Orc Heavy Leather Chest I", "body", "heavy", "Leather", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_arc_chest_heavy_b", "Mordor Orc Heavy Leather Chest II", "body", "heavy", "Leather", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_arc_chest_heavy_c", "Mordor Orc Heavy Scale Chest I", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_arc_chest_heavy_d", "Mordor Orc Heavy Scale Chest II", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_arc_chest_heavy_e", "Mordor Orc Heavy Strap Chest I", "body", "heavy", "Leather", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_arc_chest_heavy_f", "Mordor Orc Heavy Strap Chest II", "body", "heavy", "Leather", covers_body=True, arm_armor_stat=10),

    # Inf chest (chainmail-based, heavier)
    ArmorItem("sk_md_orc_inf_chainmail_light_a", "Mordor Orc Chainmail Light", "body", "light", "Chainmail", covers_body=True),
    ArmorItem("sk_md_orc_inf_chainmail_med_a", "Mordor Orc Chainmail I", "body", "medium", "Chainmail", covers_body=True, arm_armor_stat=8),
    ArmorItem("sk_md_orc_inf_chainmail_med_b", "Mordor Orc Chainmail II", "body", "medium", "Chainmail", covers_body=True, arm_armor_stat=8),
    ArmorItem("sk_md_orc_inf_chest_med_a", "Mordor Orc Chain+Leather I", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_inf_chest_med_b", "Mordor Orc Chain+Scale I", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_inf_chest_med_c", "Mordor Orc Chain+Plate I", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_inf_chest_med_d", "Mordor Orc Chain+Plate II", "body", "medium", "Plate", covers_body=True, arm_armor_stat=10),
    ArmorItem("sk_md_orc_inf_chest_heavy_a", "Mordor Orc Chain+Leather Heavy I", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_md_orc_inf_chest_heavy_b", "Mordor Orc Chain+Leather Heavy II", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_md_orc_inf_chest_heavy_c", "Mordor Orc Chain+Scale Heavy I", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_md_orc_inf_chest_heavy_d", "Mordor Orc Chain+Scale Heavy II", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_md_orc_inf_chest_heavy_e", "Mordor Orc Chain+Plate Heavy I", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),
    ArmorItem("sk_md_orc_inf_chest_heavy_f", "Mordor Orc Chain+Plate Heavy II", "body", "heavy", "Plate", covers_body=True, arm_armor_stat=14),

    # Elite (lord/NPC only)
    ArmorItem("sk_md_orc_inf_chest_elite_a", "Mordor Orc Full Plate (Captain)", "body", "elite", "Plate", covers_body=True, arm_armor_stat=20),
]


# =============================================================================
# MORDOR ORC PAULDRONS, BRACERS, BOOTS (sk_md_orc_*)
# =============================================================================
SHOULDER_ARMORS = [
    ArmorItem("sk_md_orc_pauldron_light_a", "Mordor Orc Light Pauldron I", "shoulder", "light", "Leather"),
    ArmorItem("sk_md_orc_pauldron_light_b", "Mordor Orc Light Pauldron II", "shoulder", "light", "Leather"),
    ArmorItem("sk_md_orc_pauldron_light_c", "Mordor Orc Light Pauldron III", "shoulder", "light", "Leather"),
    ArmorItem("sk_md_orc_pauldron_med_a", "Mordor Orc Pauldron I", "shoulder", "medium", "Plate"),
    ArmorItem("sk_md_orc_pauldron_med_b", "Mordor Orc Pauldron II", "shoulder", "medium", "Plate"),
    ArmorItem("sk_md_orc_pauldron_heavy_a", "Mordor Orc Heavy Pauldron I", "shoulder", "heavy", "Plate"),
    ArmorItem("sk_md_orc_pauldron_heavy_b", "Mordor Orc Heavy Pauldron II", "shoulder", "heavy", "Plate"),
]

ARM_ARMORS = [
    ArmorItem("sk_md_orc_bracer_light_a", "Mordor Orc Light Bracer I", "arm", "light", "Leather", covers_hands=True),
    ArmorItem("sk_md_orc_bracer_light_b", "Mordor Orc Light Bracer II", "arm", "light", "Leather", covers_hands=True),
    ArmorItem("sk_md_orc_bracer_med_a", "Mordor Orc Bracer I", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_md_orc_bracer_med_b", "Mordor Orc Bracer II", "arm", "medium", "Plate", covers_hands=True),
    ArmorItem("sk_md_orc_bracer_heavy_a", "Mordor Orc Heavy Bracer I", "arm", "heavy", "Plate", covers_hands=True),
    ArmorItem("sk_md_orc_bracer_heavy_b", "Mordor Orc Heavy Bracer II", "arm", "heavy", "Plate", covers_hands=True),
]

LEG_ARMORS = [
    ArmorItem("sk_md_orc_boots_a", "Mordor Orc Boots", "leg", "light", "Leather", covers_legs=True),
    ArmorItem("sk_md_orc_boots_light_a", "Mordor Orc Light Boots I", "leg", "light", "Leather", covers_legs=True),
    ArmorItem("sk_md_orc_boots_light_b", "Mordor Orc Light Boots II", "leg", "light", "Leather", covers_legs=True),
    ArmorItem("sk_md_orc_boots_med_a", "Mordor Orc Boots I", "leg", "medium", "Leather", covers_legs=True),
    ArmorItem("sk_md_orc_boots_med_b", "Mordor Orc Boots II", "leg", "medium", "Leather", covers_legs=True),
    ArmorItem("sk_md_orc_boots_heavy_a", "Mordor Orc Heavy Boots I", "leg", "heavy", "Plate", covers_legs=True),
    ArmorItem("sk_md_orc_boots_heavy_b", "Mordor Orc Heavy Boots II", "leg", "heavy", "Plate", covers_legs=True),
]


# =============================================================================
# KEYforce Morannon sub-line (sk_md_mor_*)
# Spec: E:\repos\lotraom-assets\tools\mordor_armor_and_troops.md (lines 375-591)
# Three combat lines: Arc (archer), Inf (infantry), Pik (polearm).
# Pik shares Inf bracers (no separate Pik bracer items). Greaves shared.
# 92 items total: 33 helmets + 20 chests + 18 pauldrons + 12 bracers + 9 greaves.
# All helmets carry hair_cover_type="all" (via dataclass default).
# All bracers carry covers_hands="false" (explicit; matches existing Mordor pattern).
# =============================================================================
def _morannon_items():
    head, body, shoulder, arm, leg = [], [], [], [], []
    roman = {"a": "I", "b": "II", "c": "III", "d": "IV"}
    lines3 = [("arc", "Archer"), ("inf", "Infantry"), ("pik", "Polearm")]
    lines2 = [("arc", "Archer"), ("inf", "Infantry")]

    # Helmets — 3 lines x (3 light + 3 med + 3 heavy + 2 elite) = 33
    helmet_tiers = [
        ("light", "Light",  "Leather", "abc"),
        ("med",   "Medium", "Plate",   "abc"),
        ("heavy", "Heavy",  "Plate",   "abc"),
        ("elite", "Elite",  "Plate",   "ab"),
    ]
    for lid, lname in lines3:
        for tid, tname, mat, letters in helmet_tiers:
            tkey = "medium" if tid == "med" else tid
            for v in letters:
                head.append(ArmorItem(
                    f"sk_md_mor_{lid}_helmet_{tid}_{v}",
                    f"Morannon {lname} {tname} Helmet {roman[v]}",
                    "head", tkey, mat,
                ))

    # Chests — Arc + Inf only, light (2) + med (4) + heavy (4) = 10 each, 20 total
    # Arc line = tunic/leather, Inf line = chainmail/plate. arm_armor follows existing pattern.
    chest_data = [
        ("arc", "Archer",   {"light": "Cloth",     "medium": "Leather",   "heavy": "Leather"}, {"medium": 8,  "heavy": 10}),
        ("inf", "Infantry", {"light": "Chainmail", "medium": "Chainmail", "heavy": "Plate"},    {"medium": 10, "heavy": 14}),
    ]
    chest_tiers = [
        ("light", "Light",  "ab"),
        ("med",   "Medium", "abcd"),
        ("heavy", "Heavy",  "abcd"),
    ]
    for lid, lname, mats, arm_stats in chest_data:
        for tid, tname, letters in chest_tiers:
            tkey = "medium" if tid == "med" else tid
            for v in letters:
                body.append(ArmorItem(
                    f"sk_md_mor_{lid}_chest_{tid}_{v}",
                    f"Morannon {lname} {tname} Chest {roman[v]}",
                    "body", tkey, mats[tkey],
                    covers_body=True,
                    arm_armor_stat=arm_stats.get(tkey),
                ))

    # Pauldrons — 3 lines x (2 light + 2 med + 2 heavy) = 18
    pauld_tiers = [
        ("light", "Light",  "Leather"),
        ("med",   "Medium", "Plate"),
        ("heavy", "Heavy",  "Plate"),
    ]
    for lid, lname in lines3:
        for tid, tname, mat in pauld_tiers:
            tkey = "medium" if tid == "med" else tid
            for v in "ab":
                shoulder.append(ArmorItem(
                    f"sk_md_mor_{lid}_pauld_{tid}_{v}",
                    f"Morannon {lname} {tname} Pauldron {roman[v]}",
                    "shoulder", tkey, mat,
                ))

    # Bracers — Arc + Inf only (Pik shares Inf), 2 lines x (2+2+2) = 12
    # covers_hands explicitly False per user directive (matches Mordor convention).
    bracer_tiers = [
        ("light", "Light",  "Leather"),
        ("med",   "Medium", "Plate"),
        ("heavy", "Heavy",  "Plate"),
    ]
    for lid, lname in lines2:
        for tid, tname, mat in bracer_tiers:
            tkey = "medium" if tid == "med" else tid
            for v in "ab":
                arm.append(ArmorItem(
                    f"sk_md_mor_{lid}_bracer_{tid}_{v}",
                    f"Morannon {lname} {tname} Bracer {roman[v]}",
                    "arm", tkey, mat,
                    covers_hands=False,
                ))

    # Greaves — shared, no line distinction, (3+3+3) = 9
    grvs_tiers = [
        ("light", "Light",  "Leather"),
        ("med",   "Medium", "Leather"),
        ("heavy", "Heavy",  "Plate"),
    ]
    for tid, tname, mat in grvs_tiers:
        tkey = "medium" if tid == "med" else tid
        for v in "abc":
            leg.append(ArmorItem(
                f"sk_md_mor_grvs_{tid}_{v}",
                f"Morannon {tname} Greaves {roman[v]}",
                "leg", tkey, mat,
                covers_legs=True,
            ))

    return head, body, shoulder, arm, leg


_mor_head, _mor_body, _mor_shoulder, _mor_arm, _mor_leg = _morannon_items()
HEAD_ARMORS     += _mor_head
BODY_ARMORS     += _mor_body
SHOULDER_ARMORS += _mor_shoulder
ARM_ARMORS      += _mor_arm
LEG_ARMORS      += _mor_leg


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

CULTURE = "mordor"
ITEM_NAME_PREFIX = "Mordor"


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
        armor_attrs.append(f'covers_hands="{"true" if item.covers_hands else "false"}"')
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
            "    <!--  KEYforce Mordor armor: orc pool + Morannon sub-line           -->\n"
            "    <!--  (sk_gn_orc_*, sk_md_orc_*, sk_md_mor_*)                       -->\n"
            "    <!-- ============================================================== -->\n\n"
        )
        content = content.replace(closing_tag, f"{section_comment}{new_xml}\n\n{closing_tag}")

        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)

        print(f"    -> wrote {len(new_items)} items to {filepath}")
        grand_added += len(new_items)

    print(f"\nDone. Added {grand_added} new items, skipped {grand_skipped} already present.")


def main():
    parser = argparse.ArgumentParser(description="Mordor generic Orc armor generator")
    parser.add_argument("--dry-run", action="store_true", help="List items only")
    parser.add_argument("--apply", action="store_true", help="Append to XML files")
    parser.add_argument("--armory-path", default=DEFAULT_ARMORY_BASE,
                        help=f"Path to LOTRLOME_Armory mordor/ (default: {DEFAULT_ARMORY_BASE})")
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
