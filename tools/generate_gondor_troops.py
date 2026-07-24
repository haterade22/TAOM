#!/usr/bin/env python3
"""Generate the complete Gondor troop tree XML for TAOM.

Usage:
    python tools/generate_gondor_troops.py > Main/_Module/ModuleData/troops/troops_gondor.xml
    python tools/generate_gondor_troops.py --dry-run   # print to stdout only
"""
import sys
from dataclasses import dataclass, field
from typing import Optional

# =============================================================================
# SKILL BASELINES (from rebalance_troops.py)
# Order: Athletics, Riding, OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing
# =============================================================================
SKILL_NAMES = ["Athletics", "Riding", "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing"]

BASELINES = {
    "Infantry": {
        6:  [25, 5, 25, 15, 20, 5, 0, 10],
        11: [48, 10, 60, 55, 70, 10, 5, 20],
        16: [80, 15, 90, 85, 95, 15, 10, 30],
        21: [95, 15, 125, 110, 130, 15, 10, 50],
        26: [100, 20, 175, 155, 170, 20, 15, 70],
        31: [120, 25, 230, 210, 235, 25, 20, 80],
        36: [140, 30, 270, 250, 275, 30, 25, 90],
        41: [160, 35, 310, 290, 310, 35, 25, 100],
        46: [175, 40, 335, 310, 330, 40, 30, 100],
    },
    "Ranged": {
        6:  [40, 5, 20, 10, 20, 30, 5, 10],
        11: [50, 10, 30, 25, 30, 55, 15, 20],
        16: [85, 15, 70, 45, 45, 85, 25, 30],
        21: [95, 15, 100, 65, 70, 130, 30, 40],
        26: [105, 20, 140, 85, 90, 170, 35, 50],
        31: [115, 25, 165, 100, 100, 205, 40, 60],
        36: [130, 30, 200, 120, 120, 240, 50, 70],
        41: [150, 35, 225, 140, 140, 275, 55, 80],
        46: [165, 40, 245, 160, 160, 295, 60, 80],
    },
    "Cavalry": {
        6:  [50, 50, 30, 10, 30, 5, 0, 10],
        11: [60, 65, 55, 35, 60, 10, 5, 15],
        16: [85, 95, 80, 50, 100, 15, 10, 25],
        21: [100, 120, 115, 60, 145, 25, 15, 30],
        26: [115, 160, 170, 80, 195, 30, 20, 40],
        31: [130, 210, 220, 100, 250, 35, 25, 50],
        36: [150, 270, 270, 130, 300, 45, 30, 60],
        41: [170, 320, 310, 160, 340, 50, 35, 70],
        46: [185, 360, 340, 180, 370, 55, 40, 70],
    },
    "HorseArcher": {
        6:  [50, 50, 25, 10, 20, 40, 0, 10],
        11: [65, 75, 40, 25, 40, 65, 10, 20],
        16: [90, 110, 70, 40, 65, 100, 15, 30],
        21: [110, 150, 100, 55, 80, 145, 20, 45],
        26: [125, 200, 140, 75, 110, 195, 25, 55],
        31: [145, 255, 180, 95, 140, 245, 30, 65],
        36: [160, 300, 210, 115, 170, 280, 35, 75],
        41: [180, 340, 240, 135, 200, 310, 40, 85],
        46: [195, 370, 265, 155, 225, 335, 45, 85],
    },
}

# Gondor cultural modifiers: Ath+5, Rid+5, 1H+10, 2H+5, Pol+5, Bow+0, Xbow+0, Thr-10
GONDOR_MODS = [5, 5, 10, 5, 5, 0, 0, -10]

TIER_TO_LEVEL = {1: 6, 2: 11, 3: 16, 4: 21, 5: 26, 6: 31, 7: 36, 8: 41, 9: 46}


def calc_skills(group: str, tier: int, weapon_spec: str = "") -> list[int]:
    """Calculate skills for a troop given group, tier, and weapon specialization."""
    level = TIER_TO_LEVEL[tier]
    base = list(BASELINES[group][level])
    # Apply Gondor cultural modifiers
    skills = [max(0, b + m) for b, m in zip(base, GONDOR_MODS)]
    # Apply weapon specialization
    # Indices: 0=Ath, 1=Rid, 2=1H, 3=2H, 4=Pol, 5=Bow, 6=Xbow, 7=Thr
    if weapon_spec == "sword":
        skills[2] += 15  # 1H
        skills[4] -= 15  # Pol
    elif weapon_spec == "axe":
        skills[3] += 15  # 2H
        skills[2] -= 15  # 1H
    elif weapon_spec == "spear" or weapon_spec == "pike" or weapon_spec == "glaive":
        skills[4] += 15  # Pol
        skills[2] -= 15  # 1H
    elif weapon_spec == "crossbow":
        # Swap Bow and Xbow
        skills[5], skills[6] = skills[6], skills[5]
    elif weapon_spec == "mace":
        skills[3] += 15  # 2H
        skills[2] -= 15  # 1H
    # Clamp to 0
    skills = [max(0, s) for s in skills]
    return skills


# =============================================================================
# DATA MODEL
# =============================================================================
@dataclass
class Troop:
    id: str
    name: str  # Display name (without [Gondor] prefix)
    tier: int
    group: str  # Infantry, Ranged, Cavalry, HorseArcher
    is_basic_troop: bool = False
    weapon_spec: str = ""  # sword, axe, spear, pike, glaive, crossbow, mace, ""
    upgrades: list[str] = field(default_factory=list)
    # Equipment
    weapons: list[str] = field(default_factory=list)  # Item IDs
    shield: str = ""
    head: str = ""
    body: str = ""
    leg: str = ""
    cape: str = ""
    gloves: str = ""
    horse: str = ""
    horse_harness: str = ""
    civilian_template: str = "battania_troop_civilian_template_t2"

    @property
    def level(self) -> int:
        return TIER_TO_LEVEL[self.tier]

    @property
    def loc_key(self) -> str:
        return f"aom_{self.id}_name"

    @property
    def full_name(self) -> str:
        return f"{{={self.loc_key}}}[Gondor] {self.name}"

    def skills(self) -> list[int]:
        return calc_skills(self.group, self.tier, self.weapon_spec)

    def to_xml(self) -> str:
        lines = []
        attrs = [
            f'id="{self.id}"',
            f'default_group="{self.group}"',
            f'level="{self.level}"',
            f'name="{self.full_name}"',
            f'occupation="Soldier"',
        ]
        if self.is_basic_troop:
            attrs.append('is_basic_troop="true"')
        attrs.append('culture="Culture.gondor"')

        lines.append(f'    <NPCCharacter')
        for i, attr in enumerate(attrs):
            lines.append(f'        {attr}')
        lines[-1] += '>'

        # Face
        lines.append('        <face>')
        lines.append('            <face_key_template value="BodyProperty.fighter_empire" />')
        lines.append('        </face>')

        # Skills
        skills = self.skills()
        lines.append('        <skills>')
        for name, val in zip(SKILL_NAMES, skills):
            lines.append(f'            <skill id="{name}" value="{val}" />')
        lines.append('        </skills>')

        # Upgrade targets
        if self.upgrades:
            lines.append('        <upgrade_targets>')
            for uid in self.upgrades:
                lines.append(f'            <upgrade_target id="NPCCharacter.{uid}" />')
            lines.append('        </upgrade_targets>')
        else:
            lines.append('        <upgrade_targets />')

        # Equipment
        lines.append('        <Equipments>')
        lines.append('            <EquipmentRoster>')

        slot_idx = 0
        for w in self.weapons:
            lines.append(f'                <equipment slot="Item{slot_idx}" id="Item.{w}" />')
            slot_idx += 1
        if self.shield:
            lines.append(f'                <equipment slot="Item{slot_idx}" id="Item.{self.shield}" />')
            slot_idx += 1

        if self.head:
            lines.append(f'                <equipment slot="Head" id="Item.{self.head}" />')
        if self.body:
            lines.append(f'                <equipment slot="Body" id="Item.{self.body}" />')
        if self.cape:
            lines.append(f'                <equipment slot="Cape" id="Item.{self.cape}" />')
        if self.gloves:
            lines.append(f'                <equipment slot="Gloves" id="Item.{self.gloves}" />')
        if self.leg:
            lines.append(f'                <equipment slot="Leg" id="Item.{self.leg}" />')

        lines.append('            </EquipmentRoster>')
        lines.append(f'            <EquipmentSet id="{self.civilian_template}" equipmentType="Civilian" />')

        if self.horse:
            lines.append(f'            <equipment slot="Horse" id="Item.{self.horse}" />')
        if self.horse_harness:
            lines.append(f'            <equipment slot="HorseHarness" id="Item.{self.horse_harness}" />')

        lines.append('        </Equipments>')
        lines.append('    </NPCCharacter>')
        return '\n'.join(lines)


# =============================================================================
# EQUIPMENT PRESETS BY REGION AND TIER
# =============================================================================
# Generic Gondor equipment tiers
GENERIC_SWORDS = {
    1: "wm_gondor_sword_a01", 2: "wm_gondor_sword_a02", 3: "wm_gondor_sword_a03",
    4: "wm_gondor_sword_a05", 5: "wm_gondor_sword_a07", 6: "wm_gondor_sword_a08",
    7: "wm_gondor_sword_a09", 8: "wm_gondor_sword_a10", 9: "wm_gondor_sword_a10",
}
GENERIC_SPEARS = {
    1: "gond_spear2", 2: "gond_spear2", 3: "wm_gondor_spear", 4: "wm_gondor_spear",
    5: "wm_gondor_spear_b", 6: "wm_gondor_spear_b", 7: "wm_gondor_spear_a",
    8: "wm_gondor_spear_a", 9: "wm_gondor_spear_a",
}
GENERIC_SHIELDS = {
    1: "wm_gondor_shield_a02", 2: "wm_gondor_shield_a02", 3: "wm_gondor_shield_a02",
    4: "wm_gondor_shield_a02", 5: "wm_gondor_shield_a02", 6: "wm_gondor_shield_a02",
    7: "wm_gondor_shield_a02", 8: "wm_gondor_shield_a02", 9: "wm_gondor_shield_a02",
}
GENERIC_BOWS = {
    1: "gondor_steel_bow_starter", 2: "gondor_steel_bow_starter", 3: "gondor_steel_bow",
    4: "gondor_steel_bow", 5: "gondor_steel_bow_b", 6: "gondor_steel_bow_b",
    7: "wm_ithilien_bow", 8: "wm_ithilien_bow_b", 9: "wm_ithilien_bow_c",
}
GENERIC_ARROWS = "bodkin_arrows_a"

# Armor by tier (generic gondor — Anorien Regular guide)
GENERIC_HEAD = {
    1: "", 2: "sk_gd_ano_inf_helmet_med_a", 3: "sk_gd_ano_inf_helmet_med_a",
    4: "sk_gd_ano_inf_helmet_heavy_a", 5: "sk_gd_ano_inf_helmet_heavy_a",
    6: "sk_gd_ano_inf_helmet_heavy_a",
    7: "sk_gd_ano_inf_helmet_heavy_a", 8: "sk_gd_ano_inf_helmet_heavy_a", 9: "sk_gd_ano_inf_helmet_heavy_a",
}
GENERIC_BODY = {
    1: "sk_gd_ano_chainmail_half_a", 2: "sk_gd_ano_chainmail_full_a",
    3: "sk_gd_ano_inf_chest_med_a", 4: "sk_gd_ano_inf_chest_med_b",
    5: "sk_gd_ano_inf_chest_heavy_a", 6: "sk_gd_ano_inf_chest_heavy_b",
    7: "sk_gd_ano_inf_chest_heavy_b", 8: "sk_gd_ano_inf_chest_heavy_b", 9: "sk_gd_ano_inf_chest_heavy_b",
}
GENERIC_LEG = {
    1: "sk_gd_ano_boots_a", 2: "sk_gd_ano_grvs_inf_light_a",
    3: "sk_gd_ano_grvs_inf_light_a", 4: "sk_gd_ano_grvs_inf_med_a",
    5: "sk_gd_ano_grvs_inf_med_a", 6: "sk_gd_ano_grvs_inf_heavy_a",
    7: "sk_gd_ano_grvs_inf_heavy_a", 8: "sk_gd_ano_grvs_inf_heavy_a", 9: "sk_gd_ano_grvs_inf_heavy_a",
}
GENERIC_CAPE = {
    1: "", 2: "", 3: "sk_gd_ano_pauld_inf_med_a",
    4: "sk_gd_ano_pauld_inf_med_b", 5: "sk_gd_ano_pauld_inf_heavy_a",
    6: "sk_gd_ano_pauld_inf_elite_a",
    7: "sk_gd_ano_pauld_inf_elite_a", 8: "sk_gd_ano_pauld_inf_elite_a", 9: "sk_gd_ano_pauld_inf_elite_a",
}
GENERIC_GLOVES = {
    1: "", 2: "sk_gd_ano_gloves_a", 3: "sk_gd_ano_gloves_a",
    4: "sk_gd_ano_bracer_inf_med_a", 5: "sk_gd_ano_bracer_inf_med_a",
    6: "sk_gd_ano_bracer_inf_med_a",
    7: "sk_gd_ano_bracer_inf_med_a", 8: "sk_gd_ano_bracer_inf_med_a", 9: "sk_gd_ano_bracer_inf_med_a",
}


def equip_generic_infantry(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SWORDS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    t.head = GENERIC_HEAD[tier]
    t.body = GENERIC_BODY[tier]
    t.leg = GENERIC_LEG[tier]
    t.cape = GENERIC_CAPE[tier]
    t.gloves = GENERIC_GLOVES[tier]

def equip_generic_spear(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SPEARS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    t.head = GENERIC_HEAD[tier]
    t.body = GENERIC_BODY[tier]
    t.leg = GENERIC_LEG[tier]
    t.cape = GENERIC_CAPE[tier]
    t.gloves = GENERIC_GLOVES[tier]

def equip_generic_ranged(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    t.head = GENERIC_HEAD[tier]
    t.body = GENERIC_BODY[tier]
    t.leg = GENERIC_LEG[tier]
    t.cape = GENERIC_CAPE[tier]
    t.gloves = GENERIC_GLOVES[tier]

def equip_generic_cavalry(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SPEARS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    t.head = GENERIC_HEAD[tier]
    t.body = GENERIC_BODY[tier]
    t.leg = GENERIC_LEG[tier]
    t.cape = GENERIC_CAPE[tier]
    t.gloves = GENERIC_GLOVES[tier]
    if tier <= 4:
        t.horse = "empire_horse"
        t.horse_harness = "gondor_horse_armor_2"
    else:
        t.horse = "t2_empire_horse"
        t.horse_harness = "gondor_horse_armor_4"

# =============================================================================
# REGION-SPECIFIC ARMOR DICTIONARIES
# tier -> item_id. Empty string ("") falls back to GENERIC_* dictionaries.
# Fill in with region-specific sk_gd_* IDs when armor guides are provided.
# =============================================================================

def _apply_region_armor(t: Troop, head_d, body_d, leg_d, cape_d, gloves_d):
    """Apply region armor dicts. Empty string falls back to GENERIC_*."""
    tier = t.tier
    t.head = head_d.get(tier, "") or GENERIC_HEAD.get(tier, "")
    t.body = body_d.get(tier, "") or GENERIC_BODY.get(tier, "")
    t.leg = leg_d.get(tier, "") or GENERIC_LEG.get(tier, "")
    t.cape = cape_d.get(tier, "") or GENERIC_CAPE.get(tier, "")
    t.gloves = gloves_d.get(tier, "") or GENERIC_GLOVES.get(tier, "")

# ---- LOSSARNACH REGULAR (T1-T6) ----
LOSS_HEAD    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LOSS_BODY    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LOSS_LEG     = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LOSS_CAPE    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LOSS_GLOVES  = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}

# ---- LOSSARNACH NOBLE (T4-T8) ----
LOSS_NOB_HEAD    = {4: "", 5: "", 6: "", 7: "", 8: ""}
LOSS_NOB_BODY    = {4: "", 5: "", 6: "", 7: "", 8: ""}
LOSS_NOB_LEG     = {4: "", 5: "", 6: "", 7: "", 8: ""}
LOSS_NOB_CAPE    = {4: "", 5: "", 6: "", 7: "", 8: ""}
LOSS_NOB_GLOVES  = {4: "", 5: "", 6: "", 7: "", 8: ""}

# ---- LEBENNIN (T2-T7) ----
LEB_HEAD    = {2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
LEB_BODY    = {2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
LEB_LEG     = {2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
LEB_CAPE    = {2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
LEB_GLOVES  = {2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}

# ---- PELARGIR (T4-T8) ----
PEL_HEAD    = {4: "", 5: "", 6: "", 7: "", 8: ""}
PEL_BODY    = {4: "", 5: "", 6: "", 7: "", 8: ""}
PEL_LEG     = {4: "", 5: "", 6: "", 7: "", 8: ""}
PEL_CAPE    = {4: "", 5: "", 6: "", 7: "", 8: ""}
PEL_GLOVES  = {4: "", 5: "", 6: "", 7: "", 8: ""}

# ---- LAMEDON (T1-T6) ----
LAM_HEAD    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LAM_BODY    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LAM_LEG     = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LAM_CAPE    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
LAM_GLOVES  = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}

# ---- CALEMBEL (T4-T8) ----
CAL_HEAD    = {4: "", 5: "", 6: "", 7: "", 8: ""}
CAL_BODY    = {4: "", 5: "", 6: "", 7: "", 8: ""}
CAL_LEG     = {4: "", 5: "", 6: "", 7: "", 8: ""}
CAL_CAPE    = {4: "", 5: "", 6: "", 7: "", 8: ""}
CAL_GLOVES  = {4: "", 5: "", 6: "", 7: "", 8: ""}

# ---- RINGLO VALE (T1-T7) ----
RING_HEAD    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
RING_BODY    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
RING_LEG     = {1: "", 2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
RING_CAPE    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}
RING_GLOVES  = {1: "", 2: "", 3: "", 4: "", 5: "", 6: "", 7: ""}

# ---- BELFALAS (T1-T6) ----
BEL_HEAD    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
BEL_BODY    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
BEL_LEG     = {1: "sk_gd_bel_boots_a", 2: "", 3: "", 4: "", 5: "", 6: ""}
BEL_CAPE    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
BEL_GLOVES  = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}

# ---- DOL AMROTH INFANTRY (T3-T8) ----
DA_INF_HEAD    = {3: "sk_gd_dol_helmet_med_a", 4: "sk_gd_dol_helmet_heavy_a", 5: "sk_gd_dol_helmet_heavy_a", 6: "sk_gd_dol_helmet_heavy_b", 7: "sk_gd_dol_inf_helmet_elite_a", 8: "sk_gd_dol_inf_helmet_elite_b"}
DA_INF_BODY    = {3: "sk_gd_dol_chainmail_a", 4: "sk_gd_dol_chest_med_a", 5: "sk_gd_dol_chest_med_a", 6: "sk_gd_dol_chest_heavy_a", 7: "sk_gd_dol_chest_heavy_b", 8: "sk_gd_dol_chest_heavy_b"}
DA_INF_LEG     = {3: "sk_gd_dol_grvs_light_a", 4: "sk_gd_dol_grvs_med_a", 5: "sk_gd_dol_grvs_heavy_a", 6: "sk_gd_dol_grvs_heavy_a", 7: "sk_gd_dol_grvs_heavy_a", 8: "sk_gd_dol_grvs_elite_a"}
DA_INF_CAPE    = {3: "sk_gd_dol_pauld_noble_med_a", 4: "sk_gd_dol_pauld_noble_med_b", 5: "sk_gd_dol_pauld_noble_heavy_a", 6: "sk_gd_dol_pauld_noble_heavy_a", 7: "sk_gd_dol_pauld_noble_heavy_a", 8: "sk_gd_dol_pauld_noble_elite_a"}
DA_INF_GLOVES  = {3: "sk_gd_dol_bracer_med_a", 4: "sk_gd_dol_bracer_med_a", 5: "sk_gd_dol_bracer_heavy_a", 6: "sk_gd_dol_bracer_heavy_a", 7: "sk_gd_dol_bracer_heavy_a", 8: "sk_gd_dol_bracer_elite_a"}

# ---- DOL AMROTH CAVALRY (T5-T9) ----
DA_CAV_HEAD    = {5: "sk_gd_dol_helmet_heavy_b", 6: "sk_gd_dol_helmet_heavy_a", 7: "sk_gd_dol_cav_helmet_elite_a", 8: "sk_gd_dol_cav_helmet_elite_c", 9: "sk_gd_dol_cav_helmet_elite_b"}
DA_CAV_BODY    = {5: "sk_gd_dol_chest_med_b", 6: "sk_gd_dol_chest_heavy_a", 7: "sk_gd_dol_chest_heavy_b", 8: "sk_gd_dol_chest_heavy_a", 9: "sk_gd_dol_chest_elite_a"}
DA_CAV_LEG     = {5: "sk_gd_dol_grvs_heavy_a", 6: "sk_gd_dol_grvs_heavy_a", 7: "sk_gd_dol_grvs_heavy_a", 8: "sk_gd_dol_grvs_elite_a", 9: "sk_gd_dol_grvs_elite_a"}
DA_CAV_CAPE    = {5: "sk_gd_dol_pauld_cape_noble_heavy_a", 6: "sk_gd_dol_pauld_cape_noble_heavy_a", 7: "sk_gd_dol_pauld_cape_noble_heavy_a", 8: "sk_gd_dol_pauld_cape_noble_elite_a", 9: "sk_gd_dol_pauld_cape_noble_elite_a"}
DA_CAV_GLOVES  = {5: "sk_gd_dol_bracer_heavy_a", 6: "sk_gd_dol_bracer_heavy_a", 7: "sk_gd_dol_bracer_heavy_a", 8: "sk_gd_dol_bracer_elite_a", 9: "sk_gd_dol_bracer_elite_a"}

# ---- LINHIR (T3-T7) ----
LIN_HEAD    = {3: "", 4: "", 5: "", 6: "", 7: ""}
LIN_BODY    = {3: "", 4: "", 5: "", 6: "", 7: ""}
LIN_LEG     = {3: "", 4: "", 5: "", 6: "", 7: ""}
LIN_CAPE    = {3: "", 4: "", 5: "", 6: "", 7: ""}
LIN_GLOVES  = {3: "", 4: "", 5: "", 6: "", 7: ""}

# ---- TOLFALAS (T3-T7) ----
TOL_HEAD    = {3: "", 4: "", 5: "", 6: "", 7: ""}
TOL_BODY    = {3: "", 4: "", 5: "", 6: "", 7: ""}
TOL_LEG     = {3: "", 4: "", 5: "", 6: "", 7: ""}
TOL_CAPE    = {3: "", 4: "", 5: "", 6: "", 7: ""}
TOL_GLOVES  = {3: "", 4: "", 5: "", 6: "", 7: ""}

# ---- PINNATH GELIN (T1-T6) ----
PG_HEAD    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
PG_BODY    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
PG_LEG     = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
PG_CAPE    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
PG_GLOVES  = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}

# ---- ARNDIR INFANTRY (T3-T7) ----
ARN_INF_HEAD    = {3: "", 4: "", 5: "", 6: "", 7: ""}
ARN_INF_BODY    = {3: "", 4: "", 5: "", 6: "", 7: ""}
ARN_INF_LEG     = {3: "", 4: "", 5: "", 6: "", 7: ""}
ARN_INF_CAPE    = {3: "", 4: "", 5: "", 6: "", 7: ""}
ARN_INF_GLOVES  = {3: "", 4: "", 5: "", 6: "", 7: ""}

# ---- ARNDIR CAVALRY (T5-T8) ----
ARN_CAV_HEAD    = {5: "", 6: "", 7: "", 8: ""}
ARN_CAV_BODY    = {5: "", 6: "", 7: "", 8: ""}
ARN_CAV_LEG     = {5: "", 6: "", 7: "", 8: ""}
ARN_CAV_CAPE    = {5: "", 6: "", 7: "", 8: ""}
ARN_CAV_GLOVES  = {5: "", 6: "", 7: "", 8: ""}

# ---- BLACKROOT VALE (T3-T8) ----
BRV_HEAD    = {3: "", 4: "", 5: "", 6: "", 7: "", 8: ""}
BRV_BODY    = {3: "", 4: "", 5: "", 6: "", 7: "", 8: ""}
BRV_LEG     = {3: "", 4: "", 5: "", 6: "", 7: "", 8: ""}
BRV_CAPE    = {3: "", 4: "", 5: "", 6: "", 7: "", 8: ""}
BRV_GLOVES  = {3: "", 4: "", 5: "", 6: "", 7: "", 8: ""}

# ---- ANFALAS (T1-T6) ----
ANF_HEAD    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
ANF_BODY    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
ANF_LEG     = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
ANF_CAPE    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
ANF_GLOVES  = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}

# ---- SERELOND (T3-T7) --- shared by pike + mace branches ----
SER_HEAD    = {3: "", 4: "", 5: "", 6: "", 7: ""}
SER_BODY    = {3: "", 4: "", 5: "", 6: "", 7: ""}
SER_LEG     = {3: "", 4: "", 5: "", 6: "", 7: ""}
SER_CAPE    = {3: "", 4: "", 5: "", 6: "", 7: ""}
SER_GLOVES  = {3: "", 4: "", 5: "", 6: "", 7: ""}

# ---- LOND-GALEN (T4-T8) ----
LG_HEAD    = {4: "", 5: "", 6: "", 7: "", 8: ""}
LG_BODY    = {4: "", 5: "", 6: "", 7: "", 8: ""}
LG_LEG     = {4: "", 5: "", 6: "", 7: "", 8: ""}
LG_CAPE    = {4: "", 5: "", 6: "", 7: "", 8: ""}
LG_GLOVES  = {4: "", 5: "", 6: "", 7: "", 8: ""}

# ---- HARONDOR (T1-T6) ----
HAR_HEAD    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
HAR_BODY    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
HAR_LEG     = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
HAR_CAPE    = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}
HAR_GLOVES  = {1: "", 2: "", 3: "", 4: "", 5: "", 6: ""}

# ---- METHIR (T4-T8) --- shared by glaive + archer branches ----
MET_HEAD    = {4: "", 5: "", 6: "", 7: "", 8: ""}
MET_BODY    = {4: "", 5: "", 6: "", 7: "", 8: ""}
MET_LEG     = {4: "", 5: "", 6: "", 7: "", 8: ""}
MET_CAPE    = {4: "", 5: "", 6: "", 7: "", 8: ""}
MET_GLOVES  = {4: "", 5: "", 6: "", 7: "", 8: ""}


# Region-specific equipment helpers
def equip_lossarnach(t: Troop, is_throwing=False):
    tier = t.tier
    if is_throwing:
        t.weapons = ["wm_gondor_lossarnach_1h_axe_a", "wm_gondor_lossarnach_1h_axe_b"]
        t.shield = ""
    else:
        if tier <= 4:
            t.weapons = ["wm_gondor_lossarnach_1h_axe_a"]
            t.shield = "gond_shield_one_greyscale"
        else:
            t.weapons = ["wm_gondor_lossarnach_2h_axe_a"]
            t.shield = "gond_shield_one_greyscale"
    _apply_region_armor(t, LOSS_HEAD, LOSS_BODY, LOSS_LEG, LOSS_CAPE, LOSS_GLOVES)

def equip_lossarnach_noble(t: Troop):
    tier = t.tier
    if tier <= 5:
        t.weapons = ["wm_gondor_lossarnach_2h_axe_a"]
    else:
        t.weapons = ["wm_gondor_lossarnach_2h_axe_b"]
    t.shield = "gond_shield_one_greyscale"
    _apply_region_armor(t, LOSS_NOB_HEAD, LOSS_NOB_BODY, LOSS_NOB_LEG, LOSS_NOB_CAPE, LOSS_NOB_GLOVES)

def equip_lebennin(t: Troop, is_ranged=False):
    tier = t.tier
    if is_ranged:
        t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    else:
        t.weapons = [GENERIC_SWORDS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, LEB_HEAD, LEB_BODY, LEB_LEG, LEB_CAPE, LEB_GLOVES)

def equip_pelargir(t: Troop):
    tier = t.tier
    t.weapons = ["wm_pelargir_sword_a01" if tier <= 5 else "wm_pelargir_sword_a02"]
    t.shield = "gond_shield_one_greyscale"
    _apply_region_armor(t, PEL_HEAD, PEL_BODY, PEL_LEG, PEL_CAPE, PEL_GLOVES)

def equip_lamedon(t: Troop):
    tier = t.tier
    if tier <= 4:
        t.weapons = ["wm_gondor_lamedon_1h_sword_a"]
    else:
        t.weapons = ["wm_gondor_lamedon_2h_sword_a"]
    t.shield = "gond_shield_one_red"
    _apply_region_armor(t, LAM_HEAD, LAM_BODY, LAM_LEG, LAM_CAPE, LAM_GLOVES)

def equip_calembel(t: Troop):
    tier = t.tier
    if tier <= 5:
        t.weapons = ["wm_gondor_lamedon_1h_sword_a"]
    elif tier <= 6:
        t.weapons = ["wm_gondor_lamedon_2h_sword_b"]
    else:
        t.weapons = ["wm_gondor_lamedon_2h_sword_c"]
    t.shield = "gond_shield_one_red"
    _apply_region_armor(t, CAL_HEAD, CAL_BODY, CAL_LEG, CAL_CAPE, CAL_GLOVES)

def equip_ringlo(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SPEARS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, RING_HEAD, RING_BODY, RING_LEG, RING_CAPE, RING_GLOVES)

def equip_belfalas(t: Troop, is_ranged=False):
    tier = t.tier
    if is_ranged:
        t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    else:
        t.weapons = [GENERIC_SWORDS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, BEL_HEAD, BEL_BODY, BEL_LEG, BEL_CAPE, BEL_GLOVES)

def equip_dol_amroth_infantry(t: Troop):
    tier = t.tier
    if tier <= 4:
        t.weapons = ["wm_gondor_sword_a01"]
    elif tier <= 6:
        t.weapons = ["wm_swan_knight_sworda"]
    elif tier <= 7:
        t.weapons = ["numenorean_sword_2h_a"]
    else:
        t.weapons = ["numenorean_sword_2h_b"]
    t.shield = "gond_shield_two_swan"
    _apply_region_armor(t, DA_INF_HEAD, DA_INF_BODY, DA_INF_LEG, DA_INF_CAPE, DA_INF_GLOVES)

def equip_dol_amroth_cavalry(t: Troop):
    tier = t.tier
    if tier <= 5:
        t.weapons = ["wm_gondor_swanknight_speara"]
    else:
        t.weapons = ["wm_gondor_swanknight_spearb"]
    t.shield = "gond_shield_two_swan"
    t.horse = "noble_horse_imperial"
    t.horse_harness = "gondor_swan_horse_armor_1" if tier <= 5 else "gondor_swan_horse_armor_2"
    _apply_region_armor(t, DA_CAV_HEAD, DA_CAV_BODY, DA_CAV_LEG, DA_CAV_CAPE, DA_CAV_GLOVES)

def equip_linhir(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SPEARS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, LIN_HEAD, LIN_BODY, LIN_LEG, LIN_CAPE, LIN_GLOVES)

def equip_tolfalas(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    _apply_region_armor(t, TOL_HEAD, TOL_BODY, TOL_LEG, TOL_CAPE, TOL_GLOVES)

def equip_pinnath_gelin(t: Troop, is_ranged=False, is_spear=False):
    tier = t.tier
    if is_ranged:
        t.weapons = ["composite_steppe_bow" if tier <= 4 else "steppe_war_bow", GENERIC_ARROWS, GENERIC_ARROWS]
    elif is_spear:
        t.weapons = ["wm_gondor_spear_b" if tier <= 4 else "wm_gondor_pg_speara"]
        t.shield = "gond_shield_three_green"
    else:
        t.weapons = [GENERIC_SWORDS[tier]]
        t.shield = "gond_shield_three_green"
    _apply_region_armor(t, PG_HEAD, PG_BODY, PG_LEG, PG_CAPE, PG_GLOVES)

def equip_arndir_infantry(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SWORDS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, ARN_INF_HEAD, ARN_INF_BODY, ARN_INF_LEG, ARN_INF_CAPE, ARN_INF_GLOVES)

def equip_arndir_cavalry(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SPEARS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    t.horse = "t2_empire_horse"
    t.horse_harness = "gondor_horse_armor_4"
    _apply_region_armor(t, ARN_CAV_HEAD, ARN_CAV_BODY, ARN_CAV_LEG, ARN_CAV_CAPE, ARN_CAV_GLOVES)

def equip_blackroot(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS, GENERIC_SWORDS[tier]]
    _apply_region_armor(t, BRV_HEAD, BRV_BODY, BRV_LEG, BRV_CAPE, BRV_GLOVES)

def equip_anfalas(t: Troop, is_cavalry=False):
    tier = t.tier
    if is_cavalry:
        t.weapons = [GENERIC_SPEARS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
        t.horse = "t2_empire_horse"
        t.horse_harness = "gondor_horse_armor_4"
    else:
        t.weapons = [GENERIC_SWORDS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, ANF_HEAD, ANF_BODY, ANF_LEG, ANF_CAPE, ANF_GLOVES)

def equip_serelond_pike(t: Troop):
    tier = t.tier
    if tier >= 5:
        t.weapons = ["fine_pike_t4" if tier <= 6 else "vlandia_pike_1_t5"]
        t.shield = ""
    else:
        t.weapons = [GENERIC_SPEARS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, SER_HEAD, SER_BODY, SER_LEG, SER_CAPE, SER_GLOVES)

def equip_serelond_mace(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SWORDS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, SER_HEAD, SER_BODY, SER_LEG, SER_CAPE, SER_GLOVES)

def equip_lond_galen(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    if tier >= 6:
        t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, LG_HEAD, LG_BODY, LG_LEG, LG_CAPE, LG_GLOVES)

def equip_harondor(t: Troop, is_skirmisher=False):
    tier = t.tier
    if is_skirmisher:
        t.weapons = ["western_javelin_3_t4", "western_javelin_3_t4", GENERIC_SWORDS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    else:
        t.weapons = [GENERIC_SWORDS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, HAR_HEAD, HAR_BODY, HAR_LEG, HAR_CAPE, HAR_GLOVES)

def equip_methir_glaive(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_SPEARS[tier]]
    t.shield = GENERIC_SHIELDS[tier]
    _apply_region_armor(t, MET_HEAD, MET_BODY, MET_LEG, MET_CAPE, MET_GLOVES)

def equip_methir_archer(t: Troop):
    tier = t.tier
    t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    _apply_region_armor(t, MET_HEAD, MET_BODY, MET_LEG, MET_CAPE, MET_GLOVES)

def equip_citadel(t: Troop, is_ranged=False):
    """MT Citadel Guard Noble (T5-T9) — from Citadel Guard + Fountain Guard armor guides."""
    tier = t.tier
    if is_ranged:
        t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    else:
        t.weapons = ["wm_gondor_sword_a10" if tier >= 8 else "wm_gondor_sword_a08"]
        t.shield = "gond_shield_four_black"
    if tier == 5:
        t.head = "sk_gd_mns_noble_helmet_med_a"
        t.body = "sk_gd_mns_citadel_chest_med_a"
        t.leg = "sk_gd_ano_grvs_inf_light_a"
        t.cape = "sk_gd_ano_cape_noble_b"
        t.gloves = "sk_gd_ano_gloves_a"
    elif tier == 6:
        t.head = "sk_gd_mns_noble_helmet_heavy_a"
        t.body = "sk_gd_mns_citadel_chest_med_a"
        t.leg = "sk_gd_ano_grvs_inf_med_a"
        t.cape = "sk_gd_ano_pauld_inf_med_b"
        t.gloves = "sk_gd_ano_bracer_inf_med_a"
    elif tier == 7:
        t.head = "sk_gd_mns_cita_helmet_heavy_a"
        t.body = "sk_gd_mns_citadel_chest_heavy_a"
        t.leg = "sk_gd_ano_grvs_inf_med_a"
        t.cape = "sk_gd_ano_pauld_inf_heavy_a"
        t.gloves = "sk_gd_ano_bracer_inf_med_a"
    elif tier == 8:
        t.head = "sk_gd_mns_cita_helmet_heavy_b"
        t.body = "sk_gd_mns_citadel_chest_heavy_a"
        t.leg = "sk_gd_ano_grvs_inf_heavy_a"
        t.cape = "sk_gd_ano_pauld_inf_elite_a"
        t.gloves = "sk_gd_ano_bracer_inf_med_a"
    else:
        # T9 Fountain Guard
        t.head = "sk_gd_mns_fount_helmet_heavy_a"
        t.body = "sk_gd_mns_fount_chest_heavy_a"
        t.leg = "sk_gd_ano_grvs_inf_heavy_a"
        t.cape = "sk_gd_ano_pauld_cape_fount_elite_a"
        t.gloves = "sk_gd_ano_bracer_noble_heavy_a"

def equip_ithil(t: Troop, is_ranged=False):
    """Minas Ithil Noble (T5-T9) — from Minas Ithil Noble armor guide."""
    tier = t.tier
    if is_ranged:
        t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    else:
        t.weapons = ["wm_gondor_sword_a10" if tier >= 8 else "wm_gondor_sword_a08"]
        t.shield = "gond_shield_four_black"
    if tier == 5:
        t.head = "sk_gd_ano_noble_helmet_med_a"
        t.body = "sk_gd_ano_chainmail_full_b"
        t.leg = "sk_gd_ano_grvs_noble_light_a"
        t.cape = "sk_gd_ano_pauld_noble_med_a"
        t.gloves = "sk_gd_ano_bracer_noble_med_a"
    elif tier == 6:
        t.head = "sk_gd_ano_noble_helmet_heavy_a"
        t.body = "sk_gd_ith_chest_noble_med_a"
        t.leg = "sk_gd_ano_grvs_noble_med_a"
        t.cape = "sk_gd_ano_pauld_noble_med_b"
        t.gloves = "sk_gd_ano_bracer_noble_med_a"
    elif tier == 7:
        t.head = "sk_gd_osg_ward_helmet_heavy_a"
        t.body = "sk_gd_ith_chest_noble_med_b"
        t.leg = "sk_gd_ano_grvs_noble_med_a"
        t.cape = "sk_gd_ano_pauld_noble_heavy_a"
        t.gloves = "sk_gd_ano_bracer_noble_heavy_a"
    elif tier == 8:
        t.head = "sk_gd_osg_ward_helmet_heavy_b"
        t.body = "sk_gd_ith_chest_noble_heavy_a"
        t.leg = "sk_gd_ano_grvs_noble_heavy_a"
        t.cape = "sk_gd_ano_pauld_noble_elite_a"
        t.gloves = "sk_gd_ano_bracer_noble_heavy_a"
    else:
        # T9 Moon Guard
        t.head = "sk_gd_ith_noble_helmet_heavy_a"
        t.body = "sk_gd_ith_chest_noble_heavy_b"
        t.leg = "sk_gd_ano_grvs_noble_heavy_a"
        t.cape = "sk_gd_ano_pauld_cape_noble_elite_a"
        t.gloves = "sk_gd_ano_bracer_noble_heavy_a"

def equip_osgiliath(t: Troop, is_ranged=False):
    """Osgiliath Noble (T3-T7) — from Osgiliath Noble armor guide."""
    tier = t.tier
    if is_ranged:
        t.weapons = [GENERIC_BOWS[tier], GENERIC_ARROWS, GENERIC_ARROWS]
    else:
        t.weapons = [GENERIC_SWORDS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    if tier == 3:
        t.head = "sk_gd_ano_inf_helmet_med_a"
        t.body = "sk_gd_ano_chainmail_half_b"
        t.leg = "sk_gd_ano_grvs_inf_light_a"
        t.cape = "sk_gd_ano_pauld_inf_med_a"
        t.gloves = "sk_gd_ano_bracer_inf_med_a"
    elif tier == 4:
        t.head = "sk_gd_ano_inf_helmet_heavy_a"
        t.body = "sk_gd_osg_inf_chest_med_a"
        t.leg = "sk_gd_ano_grvs_inf_light_a"
        t.cape = "sk_gd_ano_pauld_inf_med_b"
        t.gloves = "sk_gd_ano_bracer_inf_med_a"
    elif tier == 5:
        if is_ranged:
            t.head = "sk_gd_ano_inf_helmet_heavy_a"
        else:
            t.head = "sk_gd_ano_inf_helmet_heavy_a"
        t.body = "sk_gd_osg_inf_chest_med_b"
        t.leg = "sk_gd_ano_grvs_inf_med_a"
        t.cape = "sk_gd_osg_pauld_inf_med_a"
        t.gloves = "sk_gd_osg_bracer_noble_med_a"
    elif tier == 6:
        if is_ranged:
            t.head = "sk_gd_osg_ward_helmet_heavy_a"
        else:
            t.head = "sk_gd_osg_noble_helmet_heavy_a"
        t.body = "sk_gd_osg_inf_chest_heavy_a"
        t.leg = "sk_gd_ano_grvs_inf_med_a"
        t.cape = "sk_gd_osg_pauld_inf_heavy_a"
        t.gloves = "sk_gd_osg_bracer_noble_med_a"
    else:
        # T7 — Dome Guard or Longbow top tier
        if is_ranged:
            t.head = "sk_gd_osg_ward_helmet_heavy_b"
        else:
            t.head = "sk_gd_osg_noble_helmet_heavy_b"
        t.body = "sk_gd_osg_inf_chest_heavy_b"
        t.leg = "sk_gd_ano_grvs_inf_heavy_a"
        t.cape = "sk_gd_osg_pauld_inf_elite_a"
        t.gloves = "sk_gd_osg_bracer_noble_heavy_a"

def equip_cair_andros(t: Troop, is_pike=False):
    """Cair Andros Noble (T3-T7) — from Cair Andros Noble armor guide."""
    tier = t.tier
    if is_pike:
        t.weapons = ["fine_pike_t4" if tier <= 6 else "vlandia_pike_1_t5"]
        t.shield = ""
    else:
        t.weapons = [GENERIC_SWORDS[tier]]
        t.shield = GENERIC_SHIELDS[tier]
    if tier == 3:
        t.head = "sk_gd_ano_inf_helmet_med_a"
        t.body = "sk_gd_cair_chainmail_half_b"
        t.leg = "sk_gd_ano_grvs_inf_light_a"
        t.cape = "sk_gd_ano_pauld_inf_med_a"
        t.gloves = "sk_gd_ano_bracer_inf_med_a"
    elif tier == 4:
        t.head = "sk_gd_ano_inf_helmet_heavy_a"
        t.body = "sk_gd_cair_inf_chest_med_a"
        t.leg = "sk_gd_ano_grvs_inf_light_a"
        t.cape = "sk_gd_ano_pauld_inf_med_b"
        t.gloves = "sk_gd_ano_bracer_inf_med_a"
    elif tier == 5:
        t.head = "sk_gd_ano_inf_helmet_heavy_a"
        t.body = "sk_gd_cair_inf_chest_med_b"
        t.leg = "sk_gd_ano_grvs_inf_med_a"
        t.cape = "sk_gd_ano_pauld_inf_heavy_a"
        t.gloves = "sk_gd_cair_bracer_inf_med_a"
    elif tier == 6:
        if is_pike:
            t.head = "sk_gd_cair_noble_helmet_heavy_a"
        else:
            t.head = "sk_gd_cair_ward_helmet_heavy_a"
        t.body = "sk_gd_cair_inf_chest_heavy_a"
        t.leg = "sk_gd_ano_grvs_inf_med_a"
        t.cape = "sk_gd_ano_pauld_inf_elite_a"
        t.gloves = "sk_gd_cair_bracer_inf_med_a"
    else:
        # T7
        if is_pike:
            t.head = "sk_gd_cair_noble_helmet_heavy_b"
        else:
            t.head = "sk_gd_cair_ward_helmet_heavy_b"
        t.body = "sk_gd_cair_inf_chest_heavy_b"
        t.leg = "sk_gd_ano_grvs_inf_heavy_a"
        t.cape = "sk_gd_ano_pauld_cape_noble_elite_a"
        t.gloves = "sk_gd_cair_bracer_inf_med_a"

def equip_anorien(t: Troop, is_ranged=False, is_cavalry=False):
    """Anorien Regular (T1-T6) — from Anorien Regular Generic Armor Guide."""
    if is_cavalry:
        equip_generic_cavalry(t)
        # Cavalry-specific helmets
        if t.tier >= 4:
            t.head = "sk_gd_ano_cav_helmet_heavy_a"
        if t.tier >= 5:
            t.head = "sk_gd_ano_cav_helmet_heavy_b"
    elif is_ranged:
        equip_generic_ranged(t)
    else:
        equip_generic_infantry(t)


# =============================================================================
# TROOP TREE DEFINITIONS
# =============================================================================
def build_all_troops() -> list[Troop]:
    troops = []

    # =========================================================================
    # LOSSARNACH REGULAR (T1-T6) — 9 troops
    # =========================================================================
    t = Troop("gondor_loss_lumberman", "Lossarnach Lumberman", 1, "Infantry", is_basic_troop=True, weapon_spec="axe",
              upgrades=["gondor_loss_woodsman"])
    equip_lossarnach(t)
    troops.append(t)

    t = Troop("gondor_loss_woodsman", "Lossarnach Woodsman", 2, "Infantry", weapon_spec="axe",
              upgrades=["gondor_loss_skirmisher", "gondor_loss_axebearer"])
    equip_lossarnach(t)
    troops.append(t)

    # Throwing branch
    t = Troop("gondor_loss_skirmisher", "Lossarnach Skirmisher", 3, "Ranged", weapon_spec="axe",
              upgrades=["gondor_loss_axe_thrower"])
    equip_lossarnach(t, is_throwing=True)
    troops.append(t)

    t = Troop("gondor_loss_axe_thrower", "Lossarnach Axe-Thrower", 4, "Ranged", weapon_spec="axe",
              upgrades=["gondor_loss_vet_axe_thrower"])
    equip_lossarnach(t, is_throwing=True)
    troops.append(t)

    t = Troop("gondor_loss_vet_axe_thrower", "Lossarnach Veteran Axe-Thrower", 5, "Ranged", weapon_spec="axe")
    equip_lossarnach(t, is_throwing=True)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_loss_axebearer", "Lossarnach Axebearer", 3, "Infantry", weapon_spec="axe",
              upgrades=["gondor_loss_vet_axebearer"])
    equip_lossarnach(t)
    troops.append(t)

    t = Troop("gondor_loss_vet_axebearer", "Lossarnach Veteran Axebearer", 4, "Infantry", weapon_spec="axe",
              upgrades=["gondor_loss_guard"])
    equip_lossarnach(t)
    troops.append(t)

    t = Troop("gondor_loss_guard", "Lossarnach Guard", 5, "Infantry", weapon_spec="axe",
              upgrades=["gondor_loss_vet_guard"])
    equip_lossarnach(t)
    troops.append(t)

    t = Troop("gondor_loss_vet_guard", "Lossarnach Veteran Guard", 6, "Infantry", weapon_spec="axe")
    equip_lossarnach(t)
    troops.append(t)

    # NOTE: The Lossarnach noble branch (gondor_loss_noble/axeman/axeguard/axewarden/
    # high_axewarden) was retired in the KEYforce armor revamp (issue #99). The
    # mainline axebearer line covers the same role; recruitment now upgrades into
    # gondor_loss_axebearer for the elite slot.

    # =========================================================================
    # LEBENNIN REGULAR (T2-T7) — 7 troops
    # =========================================================================
    t = Troop("gondor_leb_militia", "Lebennin Militia", 2, "Infantry", is_basic_troop=True,
              upgrades=["gondor_leb_skirmisher"])
    equip_lebennin(t)
    troops.append(t)

    t = Troop("gondor_leb_skirmisher", "Lebennin Skirmisher", 3, "Infantry",
              upgrades=["gondor_leb_archer", "gondor_leb_infantry"])
    equip_lebennin(t)
    troops.append(t)

    # Ranged branch
    t = Troop("gondor_leb_archer", "Lebennin Archer", 4, "Ranged",
              upgrades=["gondor_leb_vet_archer"])
    equip_lebennin(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_leb_vet_archer", "Lebennin Veteran Archer", 5, "Ranged",
              upgrades=["gondor_leb_longbowman"])
    equip_lebennin(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_leb_longbowman", "Lebennin Longbowman", 6, "Ranged")
    equip_lebennin(t, is_ranged=True)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_leb_infantry", "Lebennin Infantry", 4, "Infantry",
              upgrades=["gondor_leb_vet_infantry"])
    equip_lebennin(t)
    troops.append(t)

    t = Troop("gondor_leb_vet_infantry", "Lebennin Veteran Infantry", 5, "Infantry",
              upgrades=["gondor_leb_sea_guard"])
    equip_lebennin(t)
    troops.append(t)

    t = Troop("gondor_leb_sea_guard", "Lebennin Sea Guard", 7, "Infantry")
    equip_lebennin(t)
    troops.append(t)

    # =========================================================================
    # PELARGIR NOBLE (T4-T8) — 5 troops
    # =========================================================================
    t = Troop("gondor_pel_skirmisher", "Pelargir Skirmisher", 4, "Infantry", is_basic_troop=True,
              upgrades=["gondor_pel_veteran"])
    equip_pelargir(t)
    troops.append(t)

    t = Troop("gondor_pel_veteran", "Pelargir Veteran", 5, "Infantry",
              upgrades=["gondor_pel_infantry"])
    equip_pelargir(t)
    troops.append(t)

    t = Troop("gondor_pel_infantry", "Pelargir Infantry", 6, "Infantry",
              upgrades=["gondor_pel_vet_infantry"])
    equip_pelargir(t)
    troops.append(t)

    t = Troop("gondor_pel_vet_infantry", "Pelargir Veteran Infantry", 7, "Infantry",
              upgrades=["gondor_pel_anchor_guard"])
    equip_pelargir(t)
    troops.append(t)

    t = Troop("gondor_pel_anchor_guard", "Pelargir Anchor Guard", 8, "Infantry")
    equip_pelargir(t)
    troops.append(t)

    # =========================================================================
    # LAMEDON REGULAR (T1-T6) — 5 troops (skips T4)
    # =========================================================================
    t = Troop("gondor_lam_clansman", "Lamedon Clansman", 1, "Infantry", is_basic_troop=True, weapon_spec="sword",
              upgrades=["gondor_lam_footman"])
    equip_lamedon(t)
    troops.append(t)

    t = Troop("gondor_lam_footman", "Lamedon Footman", 2, "Infantry", weapon_spec="sword",
              upgrades=["gondor_lam_swordman"])
    equip_lamedon(t)
    troops.append(t)

    t = Troop("gondor_lam_swordman", "Lamedon Swordman", 3, "Infantry", weapon_spec="sword",
              upgrades=["gondor_lam_vet_swordman"])
    equip_lamedon(t)
    troops.append(t)

    # Skips T4 intentionally
    t = Troop("gondor_lam_vet_swordman", "Lamedon Veteran Swordman", 5, "Infantry", weapon_spec="sword",
              upgrades=["gondor_lam_hill_warden"])
    equip_lamedon(t)
    troops.append(t)

    t = Troop("gondor_lam_hill_warden", "Lamedon Hill-Warden", 6, "Infantry", weapon_spec="sword")
    equip_lamedon(t)
    troops.append(t)

    # =========================================================================
    # CALEMBEL NOBLE (T4-T8) — 5 troops
    # =========================================================================
    t = Troop("gondor_cal_noble", "Calembel Noble", 4, "Infantry", is_basic_troop=True, weapon_spec="sword",
              upgrades=["gondor_cal_swordsman"])
    equip_calembel(t)
    troops.append(t)

    t = Troop("gondor_cal_swordsman", "Calembel Swordsman", 5, "Infantry", weapon_spec="sword",
              upgrades=["gondor_cal_heavy_swordsman"])
    equip_calembel(t)
    troops.append(t)

    t = Troop("gondor_cal_heavy_swordsman", "Calembel Heavy Swordsman", 6, "Infantry", weapon_spec="sword",
              upgrades=["gondor_cal_sergeant"])
    equip_calembel(t)
    troops.append(t)

    t = Troop("gondor_cal_sergeant", "Calembel Sergeant", 7, "Infantry", weapon_spec="sword",
              upgrades=["gondor_cal_vale_knight"])
    equip_calembel(t)
    troops.append(t)

    t = Troop("gondor_cal_vale_knight", "Calembel Vale-Knight", 8, "Infantry", weapon_spec="sword")
    equip_calembel(t)
    troops.append(t)

    # =========================================================================
    # RINGLO VALE NOBLE (T1-T7) — 6 troops (skips T5)
    # =========================================================================
    t = Troop("gondor_ring_peasant", "Ringlo Vale Peasant", 1, "Infantry", is_basic_troop=True, weapon_spec="spear",
              upgrades=["gondor_ring_militia"])
    equip_ringlo(t)
    troops.append(t)

    t = Troop("gondor_ring_militia", "Ringlo Vale Militia", 2, "Infantry", weapon_spec="spear",
              upgrades=["gondor_ring_footman"])
    equip_ringlo(t)
    troops.append(t)

    t = Troop("gondor_ring_footman", "Ringlo Vale Footman", 3, "Infantry", weapon_spec="spear",
              upgrades=["gondor_ring_guardsman"])
    equip_ringlo(t)
    troops.append(t)

    t = Troop("gondor_ring_guardsman", "Ringlo Vale Guardsman", 4, "Infantry", weapon_spec="spear",
              upgrades=["gondor_ring_spearman"])
    equip_ringlo(t)
    troops.append(t)

    # Skips T5 intentionally
    t = Troop("gondor_ring_spearman", "Ringlo Vale Spearman", 6, "Infantry", weapon_spec="spear",
              upgrades=["gondor_ring_warden"])
    equip_ringlo(t)
    troops.append(t)

    t = Troop("gondor_ring_warden", "Ringlo Vale Warden", 7, "Infantry", weapon_spec="spear")
    equip_ringlo(t)
    troops.append(t)

    # =========================================================================
    # BELFALAS REGULAR (T1-T6) — 10 troops
    # =========================================================================
    t = Troop("gondor_bel_recruit", "Belfalas Recruit", 1, "Infantry", is_basic_troop=True,
              upgrades=["gondor_bel_hunter", "gondor_bel_footman"])
    equip_belfalas(t)
    troops.append(t)

    # Ranged branch
    t = Troop("gondor_bel_hunter", "Belfalas Hunter", 2, "Ranged",
              upgrades=["gondor_bel_bowman"])
    equip_belfalas(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_bel_bowman", "Belfalas Bowman", 3, "Ranged",
              upgrades=["gondor_bel_archer"])
    equip_belfalas(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_bel_archer", "Belfalas Archer", 4, "Ranged",
              upgrades=["gondor_bel_vet_archer"])
    equip_belfalas(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_bel_vet_archer", "Belfalas Veteran Archer", 5, "Ranged")
    equip_belfalas(t, is_ranged=True)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_bel_footman", "Belfalas Footman", 2, "Infantry",
              upgrades=["gondor_bel_soldier"])
    equip_belfalas(t)
    troops.append(t)

    t = Troop("gondor_bel_soldier", "Belfalas Soldier", 3, "Infantry",
              upgrades=["gondor_bel_infantry"])
    equip_belfalas(t)
    troops.append(t)

    t = Troop("gondor_bel_infantry", "Belfalas Infantry", 4, "Infantry",
              upgrades=["gondor_bel_vet_infantry"])
    equip_belfalas(t)
    troops.append(t)

    t = Troop("gondor_bel_vet_infantry", "Belfalas Veteran Infantry", 5, "Infantry",
              upgrades=["gondor_bel_coastguard"])
    equip_belfalas(t)
    troops.append(t)

    t = Troop("gondor_bel_coastguard", "Belfalas Coastguard", 6, "Infantry")
    equip_belfalas(t)
    troops.append(t)

    # =========================================================================
    # DOL AMROTH NOBLE (T3-T9) — 10 troops
    # =========================================================================
    t = Troop("gondor_da_noble", "Dol Amroth Noble", 3, "Infantry", is_basic_troop=True,
              upgrades=["gondor_da_footman"])
    equip_dol_amroth_infantry(t)
    troops.append(t)

    t = Troop("gondor_da_footman", "Dol Amroth Footman", 4, "Infantry",
              upgrades=["gondor_da_squire", "gondor_da_infantry"])
    equip_dol_amroth_infantry(t)
    troops.append(t)

    # Cavalry branch
    t = Troop("gondor_da_squire", "Dol Amroth Squire", 5, "Cavalry", weapon_spec="spear",
              upgrades=["gondor_da_cavalry"])
    equip_dol_amroth_cavalry(t)
    troops.append(t)

    t = Troop("gondor_da_cavalry", "Dol Amroth Cavalry", 6, "Cavalry", weapon_spec="spear",
              upgrades=["gondor_da_knight"])
    equip_dol_amroth_cavalry(t)
    troops.append(t)

    t = Troop("gondor_da_knight", "Dol Amroth Knight", 7, "Cavalry", weapon_spec="spear",
              upgrades=["gondor_da_vet_knight"])
    equip_dol_amroth_cavalry(t)
    troops.append(t)

    t = Troop("gondor_da_vet_knight", "Dol Amroth Veteran Knight", 8, "Cavalry", weapon_spec="spear",
              upgrades=["gondor_da_swan_knight"])
    equip_dol_amroth_cavalry(t)
    troops.append(t)

    t = Troop("gondor_da_swan_knight", "Swan Knight", 9, "Cavalry", weapon_spec="spear")
    equip_dol_amroth_cavalry(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_da_infantry", "Dol Amroth Infantry", 5, "Infantry",
              upgrades=["gondor_da_vet_infantry"])
    equip_dol_amroth_infantry(t)
    troops.append(t)

    t = Troop("gondor_da_vet_infantry", "Dol Amroth Veteran Infantry", 6, "Infantry",
              upgrades=["gondor_da_foot_knight"])
    equip_dol_amroth_infantry(t)
    troops.append(t)

    t = Troop("gondor_da_foot_knight", "Dol Amroth Foot Knight", 7, "Infantry",
              upgrades=["gondor_da_swan_guard"])
    equip_dol_amroth_infantry(t)
    troops.append(t)

    t = Troop("gondor_da_swan_guard", "Dol Amroth Swan Guard", 8, "Infantry")
    equip_dol_amroth_infantry(t)
    troops.append(t)

    # =========================================================================
    # LINHIR NOBLE (T3-T7) — 5 troops
    # =========================================================================
    t = Troop("gondor_lin_noble", "Linhir Noble", 3, "Infantry", is_basic_troop=True, weapon_spec="spear",
              upgrades=["gondor_lin_footman"])
    equip_linhir(t)
    troops.append(t)

    t = Troop("gondor_lin_footman", "Linhir Footman", 4, "Infantry", weapon_spec="spear",
              upgrades=["gondor_lin_spearman"])
    equip_linhir(t)
    troops.append(t)

    t = Troop("gondor_lin_spearman", "Linhir Spearman", 5, "Infantry", weapon_spec="spear",
              upgrades=["gondor_lin_vet_spearman"])
    equip_linhir(t)
    troops.append(t)

    t = Troop("gondor_lin_vet_spearman", "Linhir Veteran Spearman", 6, "Infantry", weapon_spec="spear",
              upgrades=["gondor_lin_high_guard"])
    equip_linhir(t)
    troops.append(t)

    t = Troop("gondor_lin_high_guard", "Linhir High Guard", 7, "Infantry", weapon_spec="spear")
    equip_linhir(t)
    troops.append(t)

    # =========================================================================
    # TOLFALAS NOBLE (T3-T7) — 5 troops
    # =========================================================================
    t = Troop("gondor_tol_arbalest", "Tolfalas Arbalest", 3, "Ranged", is_basic_troop=True, weapon_spec="crossbow",
              upgrades=["gondor_tol_crossbowman"])
    equip_tolfalas(t)
    troops.append(t)

    t = Troop("gondor_tol_crossbowman", "Tolfalas Crossbowman", 4, "Ranged", weapon_spec="crossbow",
              upgrades=["gondor_tol_vet_crossbowman"])
    equip_tolfalas(t)
    troops.append(t)

    t = Troop("gondor_tol_vet_crossbowman", "Tolfalas Veteran Crossbowman", 5, "Ranged", weapon_spec="crossbow",
              upgrades=["gondor_tol_marksman"])
    equip_tolfalas(t)
    troops.append(t)

    t = Troop("gondor_tol_marksman", "Tolfalas Marksman", 6, "Ranged", weapon_spec="crossbow",
              upgrades=["gondor_tol_sharpshooter"])
    equip_tolfalas(t)
    troops.append(t)

    t = Troop("gondor_tol_sharpshooter", "Tolfalas Sharpshooter", 7, "Ranged", weapon_spec="crossbow")
    equip_tolfalas(t)
    troops.append(t)

    # =========================================================================
    # PINNATH GELIN REGULAR (T1-T6) — 8 troops
    # =========================================================================
    t = Troop("gondor_pg_volunteer", "Pinnath Gelin Volunteer", 1, "Infantry", is_basic_troop=True,
              upgrades=["gondor_pg_militia"])
    equip_pinnath_gelin(t)
    troops.append(t)

    t = Troop("gondor_pg_militia", "Pinnath Gelin Militia", 2, "Infantry",
              upgrades=["gondor_pg_footman"])
    equip_pinnath_gelin(t)
    troops.append(t)

    t = Troop("gondor_pg_footman", "Pinnath Gelin Footman", 3, "Infantry",
              upgrades=["gondor_pg_archer", "gondor_pg_spearman"])
    equip_pinnath_gelin(t)
    troops.append(t)

    # Ranged branch
    t = Troop("gondor_pg_archer", "Pinnath Gelin Archer", 4, "Ranged",
              upgrades=["gondor_pg_vet_archer"])
    equip_pinnath_gelin(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_pg_vet_archer", "Pinnath Gelin Veteran Archer", 5, "Ranged")
    equip_pinnath_gelin(t, is_ranged=True)
    troops.append(t)

    # Spear branch
    t = Troop("gondor_pg_spearman", "Pinnath Gelin Spearman", 4, "Infantry", weapon_spec="spear",
              upgrades=["gondor_pg_vet_spearman"])
    equip_pinnath_gelin(t, is_spear=True)
    troops.append(t)

    t = Troop("gondor_pg_vet_spearman", "Pinnath Gelin Veteran Spearman", 5, "Infantry", weapon_spec="spear",
              upgrades=["gondor_pg_spearwarden"])
    equip_pinnath_gelin(t, is_spear=True)
    troops.append(t)

    t = Troop("gondor_pg_spearwarden", "Pinnath Gelin Spearwarden", 6, "Infantry", weapon_spec="spear")
    equip_pinnath_gelin(t, is_spear=True)
    troops.append(t)

    # =========================================================================
    # ARNDIR NOBLE (T3-T8) — 8 troops
    # =========================================================================
    t = Troop("gondor_arn_noble", "Arndir Noble", 3, "Infantry", is_basic_troop=True,
              upgrades=["gondor_arn_noble_t4"])
    equip_arndir_infantry(t)
    troops.append(t)

    t = Troop("gondor_arn_noble_t4", "Arndir Noble", 4, "Infantry",
              upgrades=["gondor_arn_cavalry", "gondor_arn_infantry"])
    equip_arndir_infantry(t)
    troops.append(t)

    # Cavalry branch
    t = Troop("gondor_arn_cavalry", "Arndir Cavalry", 5, "Cavalry",
              upgrades=["gondor_arn_knight"])
    equip_arndir_cavalry(t)
    troops.append(t)

    t = Troop("gondor_arn_knight", "Arndir Knight", 6, "Cavalry",
              upgrades=["gondor_arn_vet_knight"])
    equip_arndir_cavalry(t)
    troops.append(t)

    t = Troop("gondor_arn_vet_knight", "Arndir Veteran Knight", 7, "Cavalry",
              upgrades=["gondor_arn_hill_knight"])
    equip_arndir_cavalry(t)
    troops.append(t)

    t = Troop("gondor_arn_hill_knight", "Arndir Hill-Knight", 8, "Cavalry")
    equip_arndir_cavalry(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_arn_infantry", "Arndir Infantry", 5, "Infantry",
              upgrades=["gondor_arn_vet_infantry"])
    equip_arndir_infantry(t)
    troops.append(t)

    t = Troop("gondor_arn_vet_infantry", "Arndir Veteran Infantry", 6, "Infantry",
              upgrades=["gondor_arn_foot_knight"])
    equip_arndir_infantry(t)
    troops.append(t)

    t = Troop("gondor_arn_foot_knight", "Arndir Foot-Knight", 7, "Infantry")
    equip_arndir_infantry(t)
    troops.append(t)

    # =========================================================================
    # BLACKROOT VALE NOBLE (T3-T8) — 7 troops
    # =========================================================================
    t = Troop("gondor_brv_bowman", "Blackroot Vale Bowman", 3, "Ranged", is_basic_troop=True,
              upgrades=["gondor_brv_scout"])
    equip_blackroot(t)
    troops.append(t)

    t = Troop("gondor_brv_scout", "Blackroot Vale Scout", 4, "Ranged",
              upgrades=["gondor_brv_archer"])
    equip_blackroot(t)
    troops.append(t)

    t = Troop("gondor_brv_archer", "Blackroot Vale Archer", 5, "Ranged",
              upgrades=["gondor_brv_vet_archer"])
    equip_blackroot(t)
    troops.append(t)

    t = Troop("gondor_brv_vet_archer", "Blackroot Vale Veteran Archer", 6, "Ranged",
              upgrades=["gondor_brv_ranger", "gondor_brv_shadowhunter"])
    equip_blackroot(t)
    troops.append(t)

    t = Troop("gondor_brv_ranger", "Blackroot Vale Ranger", 7, "Ranged",
              upgrades=["gondor_brv_shadowbow"])
    equip_blackroot(t)
    troops.append(t)

    t = Troop("gondor_brv_shadowbow", "Blackroot Vale Shadowbow", 8, "Ranged")
    equip_blackroot(t)
    troops.append(t)

    t = Troop("gondor_brv_shadowhunter", "Blackroot Vale Shadowhunter", 7, "Ranged")
    equip_blackroot(t)
    troops.append(t)

    # =========================================================================
    # ANFALAS REGULAR (T1-T6) — 8 troops
    # =========================================================================
    t = Troop("gondor_anf_levy", "Anfalas Levy", 1, "Infantry", is_basic_troop=True,
              upgrades=["gondor_anf_militia"])
    equip_anfalas(t)
    troops.append(t)

    t = Troop("gondor_anf_militia", "Anfalas Militia", 2, "Infantry",
              upgrades=["gondor_anf_footman"])
    equip_anfalas(t)
    troops.append(t)

    t = Troop("gondor_anf_footman", "Anfalas Footman", 3, "Infantry",
              upgrades=["gondor_anf_guardsman", "gondor_anf_cavalry"])
    equip_anfalas(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_anf_guardsman", "Anfalas Guardsman", 4, "Infantry",
              upgrades=["gondor_anf_infantry"])
    equip_anfalas(t)
    troops.append(t)

    t = Troop("gondor_anf_infantry", "Anfalas Infantry", 5, "Infantry",
              upgrades=["gondor_anf_vet_infantry"])
    equip_anfalas(t)
    troops.append(t)

    t = Troop("gondor_anf_vet_infantry", "Anfalas Veteran Infantry", 6, "Infantry")
    equip_anfalas(t)
    troops.append(t)

    # Cavalry branch
    t = Troop("gondor_anf_cavalry", "Anfalas Cavalry", 4, "Cavalry",
              upgrades=["gondor_anf_vet_cavalry"])
    equip_anfalas(t, is_cavalry=True)
    troops.append(t)

    t = Troop("gondor_anf_vet_cavalry", "Anfalas Veteran Cavalry", 5, "Cavalry")
    equip_anfalas(t, is_cavalry=True)
    troops.append(t)

    # =========================================================================
    # SERELOND NOBLE (T3-T7) — 7 troops
    # =========================================================================
    t = Troop("gondor_ser_noble", "Serelond Noble", 3, "Infantry", is_basic_troop=True,
              upgrades=["gondor_ser_veteran"])
    equip_generic_infantry(t)
    troops.append(t)

    t = Troop("gondor_ser_veteran", "Serelond Veteran", 4, "Infantry",
              upgrades=["gondor_ser_pikeman", "gondor_ser_maceman"])
    equip_generic_infantry(t)
    troops.append(t)

    # Pike branch
    t = Troop("gondor_ser_pikeman", "Serelond Pikeman", 5, "Infantry", weapon_spec="pike",
              upgrades=["gondor_ser_pikewarden"])
    equip_serelond_pike(t)
    troops.append(t)

    t = Troop("gondor_ser_pikewarden", "Serelond Pikewarden", 6, "Infantry", weapon_spec="pike",
              upgrades=["gondor_ser_phalanx"])
    equip_serelond_pike(t)
    troops.append(t)

    t = Troop("gondor_ser_phalanx", "Serelond Phalanx", 7, "Infantry", weapon_spec="pike")
    equip_serelond_pike(t)
    troops.append(t)

    # Mace branch
    t = Troop("gondor_ser_maceman", "Serelond Maceman", 5, "Infantry", weapon_spec="mace",
              upgrades=["gondor_ser_vet_maceman"])
    equip_serelond_mace(t)
    troops.append(t)

    t = Troop("gondor_ser_vet_maceman", "Serelond Veteran Maceman", 6, "Infantry", weapon_spec="mace",
              upgrades=["gondor_ser_coastwarden"])
    equip_serelond_mace(t)
    troops.append(t)

    t = Troop("gondor_ser_coastwarden", "Serelond Coastwarden", 7, "Infantry", weapon_spec="mace")
    equip_serelond_mace(t)
    troops.append(t)

    # =========================================================================
    # LOND-GALEN NOBLE (T4-T8) — 5 troops
    # =========================================================================
    t = Troop("gondor_lg_noble", "Lond-Galen Noble", 4, "Ranged", is_basic_troop=True, weapon_spec="crossbow",
              upgrades=["gondor_lg_crossbowman"])
    equip_lond_galen(t)
    troops.append(t)

    t = Troop("gondor_lg_crossbowman", "Lond-Galen Crossbowman", 5, "Ranged", weapon_spec="crossbow",
              upgrades=["gondor_lg_pavise_crossbowman"])
    equip_lond_galen(t)
    troops.append(t)

    t = Troop("gondor_lg_pavise_crossbowman", "Lond-Galen Pavise Crossbowman", 6, "Ranged", weapon_spec="crossbow",
              upgrades=["gondor_lg_pavise_guard"])
    equip_lond_galen(t)
    troops.append(t)

    t = Troop("gondor_lg_pavise_guard", "Lond-Galen Pavise Guard", 7, "Ranged", weapon_spec="crossbow",
              upgrades=["gondor_lg_haven_guard"])
    equip_lond_galen(t)
    troops.append(t)

    t = Troop("gondor_lg_haven_guard", "Lond-Galen Haven Guard", 8, "Ranged", weapon_spec="crossbow")
    equip_lond_galen(t)
    troops.append(t)

    # =========================================================================
    # HARONDOR REGULAR (T1-T6) — 8 troops
    # =========================================================================
    t = Troop("gondor_har_conscript", "Harondor Conscript", 1, "Infantry", is_basic_troop=True,
              upgrades=["gondor_har_militia"])
    equip_harondor(t)
    troops.append(t)

    t = Troop("gondor_har_militia", "Harondor Militia", 2, "Infantry",
              upgrades=["gondor_har_footman", "gondor_har_skirmisher"])
    equip_harondor(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_har_footman", "Harondor Footman", 3, "Infantry",
              upgrades=["gondor_har_guardsman"])
    equip_harondor(t)
    troops.append(t)

    t = Troop("gondor_har_guardsman", "Harondor Guardsman", 4, "Infantry",
              upgrades=["gondor_har_infantry"])
    equip_harondor(t)
    troops.append(t)

    t = Troop("gondor_har_infantry", "Harondor Infantry", 5, "Infantry",
              upgrades=["gondor_har_frontier_guard"])
    equip_harondor(t)
    troops.append(t)

    t = Troop("gondor_har_frontier_guard", "Harondor Frontier Guard", 6, "Infantry")
    equip_harondor(t)
    troops.append(t)

    # Skirmisher branch
    t = Troop("gondor_har_skirmisher", "Harondor Skirmisher", 3, "Ranged",
              upgrades=["gondor_har_vet_skirmisher"])
    equip_harondor(t, is_skirmisher=True)
    troops.append(t)

    t = Troop("gondor_har_vet_skirmisher", "Harondor Veteran Skirmisher", 4, "Ranged",
              upgrades=["gondor_har_javelineer"])
    equip_harondor(t, is_skirmisher=True)
    troops.append(t)

    t = Troop("gondor_har_javelineer", "Harondor Javelineer", 5, "Ranged")
    equip_harondor(t, is_skirmisher=True)
    troops.append(t)

    # =========================================================================
    # METHIR NOBLE (T4-T8) — 7 troops
    # =========================================================================
    t = Troop("gondor_met_noble", "Methir Noble", 4, "Infantry", is_basic_troop=True,
              upgrades=["gondor_met_glaiveman", "gondor_met_archer"])
    equip_generic_infantry(t)
    troops.append(t)

    # Glaive branch
    t = Troop("gondor_met_glaiveman", "Methir Glaiveman", 5, "Infantry", weapon_spec="glaive",
              upgrades=["gondor_met_glaive_guard"])
    equip_methir_glaive(t)
    troops.append(t)

    t = Troop("gondor_met_glaive_guard", "Methir Glaive Guard", 6, "Infantry", weapon_spec="glaive",
              upgrades=["gondor_met_sun_warden"])
    equip_methir_glaive(t)
    troops.append(t)

    t = Troop("gondor_met_sun_warden", "Methir Sun Warden", 7, "Infantry", weapon_spec="glaive",
              upgrades=["gondor_met_sun_knight"])
    equip_methir_glaive(t)
    troops.append(t)

    t = Troop("gondor_met_sun_knight", "Methir Sun Knight", 8, "Infantry", weapon_spec="glaive")
    equip_methir_glaive(t)
    troops.append(t)

    # Archer branch
    t = Troop("gondor_met_archer", "Methir Archer", 5, "Ranged",
              upgrades=["gondor_met_vet_archer"])
    equip_methir_archer(t)
    troops.append(t)

    t = Troop("gondor_met_vet_archer", "Methir Veteran Archer", 6, "Ranged",
              upgrades=["gondor_met_composite_archer"])
    equip_methir_archer(t)
    troops.append(t)

    t = Troop("gondor_met_composite_archer", "Methir Composite Archer", 7, "Ranged")
    equip_methir_archer(t)
    troops.append(t)

    # =========================================================================
    # MINAS ITHIL NOBLE (T5-T9) — 6 troops
    # =========================================================================
    t = Troop("gondor_ith_watcher", "Ithil Guard Watcher", 5, "Infantry", is_basic_troop=True,
              upgrades=["gondor_ith_veteran"])
    equip_ithil(t)
    troops.append(t)

    t = Troop("gondor_ith_veteran", "Ithil Guard Veteran", 6, "Infantry",
              upgrades=["gondor_ith_sergeant", "gondor_ith_longbowman"])
    equip_ithil(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_ith_sergeant", "Ithil Guard Sergeant", 7, "Infantry",
              upgrades=["gondor_ith_captain"])
    equip_ithil(t)
    troops.append(t)

    t = Troop("gondor_ith_captain", "Ithil Guard Captain", 8, "Infantry")
    equip_ithil(t)
    troops.append(t)

    # Ranged branch
    t = Troop("gondor_ith_longbowman", "Ithil Guard Longbowman", 7, "Ranged",
              upgrades=["gondor_ith_sharpshooter"])
    equip_ithil(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_ith_sharpshooter", "Ithil Guard Sharpshooter", 8, "Ranged",
              upgrades=["gondor_ith_moon_guard"])
    equip_ithil(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_ith_moon_guard", "Moon Guard", 9, "Ranged")
    equip_ithil(t, is_ranged=True)
    troops.append(t)

    # =========================================================================
    # CAIR ANDROS NOBLE (T3-T7) — 7 troops
    # =========================================================================
    t = Troop("gondor_ca_noble", "Cair Andros Noble", 3, "Infantry", is_basic_troop=True,
              upgrades=["gondor_ca_veteran"])
    equip_cair_andros(t)
    troops.append(t)

    t = Troop("gondor_ca_veteran", "Cair Andros Veteran", 4, "Infantry",
              upgrades=["gondor_ca_spearman", "gondor_ca_infantry"])
    equip_cair_andros(t)
    troops.append(t)

    # Spear/Pike branch
    t = Troop("gondor_ca_spearman", "Cair Andros Spearman", 5, "Infantry", weapon_spec="spear",
              upgrades=["gondor_ca_pikeman"])
    equip_cair_andros(t, is_pike=True)
    troops.append(t)

    t = Troop("gondor_ca_pikeman", "Cair Andros Pikeman", 6, "Infantry", weapon_spec="pike",
              upgrades=["gondor_ca_pikewarden"])
    equip_cair_andros(t, is_pike=True)
    troops.append(t)

    t = Troop("gondor_ca_pikewarden", "Cair Andros Pikewarden", 7, "Infantry", weapon_spec="pike")
    equip_cair_andros(t, is_pike=True)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_ca_infantry", "Cair Andros Infantry", 5, "Infantry",
              upgrades=["gondor_ca_guard"])
    equip_cair_andros(t)
    troops.append(t)

    t = Troop("gondor_ca_guard", "Cair Andros Guard", 6, "Infantry",
              upgrades=["gondor_ca_warden"])
    equip_cair_andros(t)
    troops.append(t)

    t = Troop("gondor_ca_warden", "Cair Andros Warden", 7, "Infantry")
    equip_cair_andros(t)
    troops.append(t)

    # =========================================================================
    # OSGILIATH NOBLE (T3-T7) — 6 troops
    # =========================================================================
    t = Troop("gondor_osg_veteran", "Osgiliath Veteran", 3, "Infantry", is_basic_troop=True,
              upgrades=["gondor_osg_skirmisher"])
    equip_osgiliath(t)
    troops.append(t)

    t = Troop("gondor_osg_skirmisher", "Osgiliath Skirmisher", 4, "Infantry",
              upgrades=["gondor_osg_infantry", "gondor_osg_archer"])
    equip_osgiliath(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_osg_infantry", "Osgiliath Infantry", 5, "Infantry",
              upgrades=["gondor_osg_guard"])
    equip_osgiliath(t)
    troops.append(t)

    t = Troop("gondor_osg_guard", "Osgiliath Guard", 6, "Infantry",
              upgrades=["gondor_osg_dome_guard"])
    equip_osgiliath(t)
    troops.append(t)

    t = Troop("gondor_osg_dome_guard", "Osgiliath Dome Guard", 7, "Infantry")
    equip_osgiliath(t)
    troops.append(t)

    # Ranged branch
    t = Troop("gondor_osg_archer", "Osgiliath Archer", 5, "Ranged",
              upgrades=["gondor_osg_longbowman"])
    equip_osgiliath(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_osg_longbowman", "Osgiliath Longbowman", 6, "Ranged")
    equip_osgiliath(t, is_ranged=True)
    troops.append(t)

    # =========================================================================
    # MINAS-TIRITH NOBLE (T5-T9) — 7 troops
    # =========================================================================
    t = Troop("gondor_mt_trainee", "Citadel Guard Trainee", 5, "Infantry", is_basic_troop=True,
              upgrades=["gondor_mt_veteran"])
    equip_citadel(t)
    troops.append(t)

    t = Troop("gondor_mt_veteran", "Citadel Guard Veteran", 6, "Infantry",
              upgrades=["gondor_mt_sergeant", "gondor_mt_longbowman"])
    equip_citadel(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_mt_sergeant", "Citadel Guard Sergeant", 7, "Infantry",
              upgrades=["gondor_mt_captain"])
    equip_citadel(t)
    troops.append(t)

    t = Troop("gondor_mt_captain", "Citadel Guard Captain", 8, "Infantry",
              upgrades=["gondor_mt_fountain_guard"])
    equip_citadel(t)
    troops.append(t)

    t = Troop("gondor_mt_fountain_guard", "Fountain Guard", 9, "Infantry")
    equip_citadel(t)
    troops.append(t)

    # Ranged branch
    t = Troop("gondor_mt_longbowman", "Citadel Guard Longbowman", 7, "Ranged",
              upgrades=["gondor_mt_sharpshooter"])
    equip_citadel(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_mt_sharpshooter", "Citadel Guard Sharpshooter", 8, "Ranged")
    equip_citadel(t, is_ranged=True)
    troops.append(t)

    # =========================================================================
    # ANORIEN REGULAR (T1-T6) — 12 troops
    # =========================================================================
    t = Troop("gondor_ano_peasant", "Anorien Peasant", 1, "Infantry", is_basic_troop=True,
              upgrades=["gondor_ano_militia", "gondor_ano_archer_militia"])
    equip_anorien(t)
    troops.append(t)

    # Infantry line
    t = Troop("gondor_ano_militia", "Anorien Militia", 2, "Infantry",
              upgrades=["gondor_ano_footman"])
    equip_anorien(t)
    troops.append(t)

    t = Troop("gondor_ano_footman", "Anorien Footman", 3, "Infantry",
              upgrades=["gondor_ano_guardsman", "gondor_ano_mt_cavalry"])
    equip_anorien(t)
    troops.append(t)

    # Infantry branch
    t = Troop("gondor_ano_guardsman", "Anorien Guardsman", 4, "Infantry",
              upgrades=["gondor_ano_infantry"])
    equip_anorien(t)
    troops.append(t)

    t = Troop("gondor_ano_infantry", "Anorien Infantry", 5, "Infantry",
              upgrades=["gondor_ano_vet_infantry"])
    equip_anorien(t)
    troops.append(t)

    t = Troop("gondor_ano_vet_infantry", "Anorien Veteran Infantry", 6, "Infantry")
    equip_anorien(t)
    troops.append(t)

    # Cavalry branch
    t = Troop("gondor_ano_mt_cavalry", "Minas-Tirith Cavalry", 4, "Cavalry",
              upgrades=["gondor_ano_mt_heavy_cavalry"])
    equip_anorien(t, is_cavalry=True)
    troops.append(t)

    t = Troop("gondor_ano_mt_heavy_cavalry", "Minas-Tirith Heavy Cavalry", 5, "Cavalry",
              upgrades=["gondor_ano_mt_knight"])
    equip_anorien(t, is_cavalry=True)
    troops.append(t)

    t = Troop("gondor_ano_mt_knight", "Minas-Tirith Knight", 6, "Cavalry")
    equip_anorien(t, is_cavalry=True)
    troops.append(t)

    # Ranged line
    t = Troop("gondor_ano_archer_militia", "Anorien Archer Militia", 2, "Ranged",
              upgrades=["gondor_ano_skirmisher"])
    equip_anorien(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_ano_skirmisher", "Anorien Skirmisher", 3, "Ranged",
              upgrades=["gondor_ano_bowman"])
    equip_anorien(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_ano_bowman", "Anorien Bowman", 4, "Ranged",
              upgrades=["gondor_ano_archer"])
    equip_anorien(t, is_ranged=True)
    troops.append(t)

    t = Troop("gondor_ano_archer", "Anorien Archer", 5, "Ranged")
    equip_anorien(t, is_ranged=True)
    troops.append(t)

    # =========================================================================
    # MILITIA (preserved from existing) — 4 troops
    # =========================================================================
    t = Troop("gondor_militia_spearman", "Gondor Militia Spearman", 2, "Infantry", weapon_spec="spear")
    equip_generic_spear(t)
    # Override with militia-specific gear (use generic Anorien items)
    t.head = "gondor_generic_helmet_5_b"
    t.body = "sk_gd_ano_chainmail_full_a"
    t.leg = "sk_gd_ano_boots_a"
    t.cape = "sk_gd_ano_pauld_inf_med_a"
    t.gloves = ""
    troops.append(t)

    t = Troop("gondor_militia_archer", "Gondor Militia Archer", 2, "Ranged")
    equip_generic_ranged(t)
    t.head = ""
    t.body = "sk_gd_ano_chainmail_half_a"
    t.leg = "sk_gd_ano_boots_a"
    t.cape = ""
    t.gloves = ""
    t.weapons = ["hunting_bow", "default_arrows", "default_arrows"]
    troops.append(t)

    t = Troop("gondor_militia_veteran_spearman", "Gondor Veteran Militia Spearman", 3, "Infantry", weapon_spec="spear")
    equip_generic_spear(t)
    t.head = "gondor_generic_helmet_5_a"
    t.body = "sk_gd_ano_chainmail_full_b"
    t.leg = "sk_gd_ano_boots_a"
    t.cape = "sk_gd_ano_pauld_inf_med_a"
    t.gloves = ""
    troops.append(t)

    t = Troop("gondor_militia_veteran_archer", "Gondor Veteran Militia Archer", 3, "Ranged")
    equip_generic_ranged(t)
    t.head = "gondor_generic_helmet_5_b"
    t.body = "sk_gd_ano_chainmail_half_b"
    t.leg = "ithilien_boots"
    t.cape = ""
    t.gloves = ""
    t.weapons = ["long_bow", "default_arrows", "default_arrows", "wm_gondor_sword_a02"]
    troops.append(t)

    return troops


# =============================================================================
# SECTION HEADERS
# =============================================================================
SECTION_HEADERS = [
    ("gondor_loss_lumberman", "LOSSARNACH REGULAR (T1-T6)"),
    ("gondor_leb_militia", "LEBENNIN REGULAR (T2-T7)"),
    ("gondor_pel_skirmisher", "PELARGIR NOBLE (T4-T8)"),
    ("gondor_lam_clansman", "LAMEDON REGULAR (T1-T6)"),
    ("gondor_cal_noble", "CALEMBEL NOBLE (T4-T8)"),
    ("gondor_ring_peasant", "RINGLO VALE NOBLE (T1-T7)"),
    ("gondor_bel_recruit", "BELFALAS REGULAR (T1-T6)"),
    ("gondor_da_noble", "DOL AMROTH NOBLE (T3-T9)"),
    ("gondor_lin_noble", "LINHIR NOBLE (T3-T7)"),
    ("gondor_tol_arbalest", "TOLFALAS NOBLE (T3-T7)"),
    ("gondor_pg_volunteer", "PINNATH GELIN REGULAR (T1-T6)"),
    ("gondor_arn_noble", "ARNDIR NOBLE (T3-T8)"),
    ("gondor_brv_bowman", "BLACKROOT VALE NOBLE (T3-T8)"),
    ("gondor_anf_levy", "ANFALAS REGULAR (T1-T6)"),
    ("gondor_ser_noble", "SERELOND NOBLE (T3-T7)"),
    ("gondor_lg_noble", "LOND-GALEN NOBLE (T4-T8)"),
    ("gondor_har_conscript", "HARONDOR REGULAR (T1-T6)"),
    ("gondor_met_noble", "METHIR NOBLE (T4-T8)"),
    ("gondor_ith_watcher", "MINAS ITHIL NOBLE (T5-T9)"),
    ("gondor_ca_noble", "CAIR ANDROS NOBLE (T3-T7)"),
    ("gondor_osg_veteran", "OSGILIATH NOBLE (T3-T7)"),
    ("gondor_mt_trainee", "MINAS-TIRITH NOBLE (T5-T9)"),
    ("gondor_ano_peasant", "ANORIEN REGULAR (T1-T6)"),
    ("gondor_militia_spearman", "MILITIA"),
]


def generate_xml() -> str:
    troops = build_all_troops()
    header_map = {tid: hdr for tid, hdr in SECTION_HEADERS}

    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<NPCCharacters>',
    ]

    for troop in troops:
        if troop.id in header_map:
            lines.append(f'    <!-- {"=" * 60} -->')
            lines.append(f'    <!-- {header_map[troop.id]} -->')
            lines.append(f'    <!-- {"=" * 60} -->')
        lines.append(troop.to_xml())
        lines.append('')

    lines.append('</NPCCharacters>')
    return '\n'.join(lines)


if __name__ == "__main__":
    xml = generate_xml()
    if "--dry-run" in sys.argv:
        # Count troops and print summary
        troops = build_all_troops()
        basic_troops = [t for t in troops if t.is_basic_troop]
        print(f"Total troops: {len(troops)}")
        print(f"Basic troops (is_basic_troop=true): {len(basic_troops)}")
        for t in basic_troops:
            print(f"  - {t.id} ({t.name}, T{t.tier}, {t.group})")
        print(f"\nAll troop IDs:")
        for t in troops:
            print(f"  {t.id} (T{t.tier}/L{t.level}, {t.group}, spec={t.weapon_spec or 'none'})")
    else:
        print(xml)
