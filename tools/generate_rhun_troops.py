#!/usr/bin/env python3
"""Generate the complete Rhûn troop tree XML for TAOM.

Usage:
    python tools/generate_rhun_troops.py > Main/_Module/ModuleData/troops/troops_rhun.xml
    python tools/generate_rhun_troops.py --dry-run   # print summary only
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

# Rhûn cultural modifiers: cavalry-oriented
# Ath+5, Rid+15, 1H+0, 2H+5, Pol+10, Bow+5, Xbow+0, Thr-5
RHUN_MODS = [5, 15, 0, 5, 10, 5, 0, -5]

TIER_TO_LEVEL = {1: 6, 2: 11, 3: 16, 4: 21, 5: 26, 6: 31, 7: 36, 8: 41, 9: 46}


def calc_skills(group: str, tier: int, weapon_spec: str = "") -> list[int]:
    """Calculate skills for a troop given group, tier, and weapon specialization."""
    level = TIER_TO_LEVEL[tier]
    base = list(BASELINES[group][level])
    skills = [max(0, b + m) for b, m in zip(base, RHUN_MODS)]
    # Indices: 0=Ath, 1=Rid, 2=1H, 3=2H, 4=Pol, 5=Bow, 6=Xbow, 7=Thr
    if weapon_spec == "sword":
        skills[2] += 15; skills[4] -= 15
    elif weapon_spec == "axe":
        skills[3] += 15; skills[2] -= 15
    elif weapon_spec in ("spear", "pike", "glaive", "halberd"):
        skills[4] += 15; skills[2] -= 15
    elif weapon_spec == "crossbow":
        skills[5], skills[6] = skills[6], skills[5]
    elif weapon_spec == "mace":
        skills[3] += 15; skills[2] -= 15
    elif weapon_spec == "lance":
        skills[4] += 15; skills[2] -= 15
    skills = [max(0, s) for s in skills]
    return skills


# =============================================================================
# DATA MODEL
# =============================================================================
@dataclass
class Troop:
    id: str
    name: str
    tier: int
    group: str  # Infantry, Ranged, Cavalry, HorseArcher
    is_basic_troop: bool = False
    weapon_spec: str = ""
    upgrades: list[str] = field(default_factory=list)
    weapons: list[str] = field(default_factory=list)
    shield: str = ""
    head: str = ""
    body: str = ""
    leg: str = ""
    cape: str = ""
    gloves: str = ""
    horse: str = ""
    horse_harness: str = ""
    civilian_template: str = "aserai_troop_civilian_template_t3"
    face_template: str = "BodyProperty.fighter_khuzait"

    @property
    def level(self) -> int:
        return TIER_TO_LEVEL[self.tier]

    @property
    def loc_key(self) -> str:
        return f"aom_{self.id}_name"

    @property
    def full_name(self) -> str:
        return f"{{={self.loc_key}}}[Rh\u00fbn] {self.name}"

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
        attrs.append('culture="Culture.khuzait"')

        lines.append(f'    <NPCCharacter')
        for attr in attrs:
            lines.append(f'        {attr}')
        lines[-1] += '>'

        lines.append('        <face>')
        lines.append(f'            <face_key_template value="{self.face_template}" />')
        lines.append('        </face>')

        skills = self.skills()
        lines.append('        <skills>')
        for name, val in zip(SKILL_NAMES, skills):
            lines.append(f'            <skill id="{name}" value="{val}" />')
        lines.append('        </skills>')

        if self.upgrades:
            lines.append('        <upgrade_targets>')
            for uid in self.upgrades:
                lines.append(f'            <upgrade_target id="NPCCharacter.{uid}" />')
            lines.append('        </upgrade_targets>')
        else:
            lines.append('        <upgrade_targets />')

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
        lines.append(f'            <EquipmentSet id="{self.civilian_template}" civilian="true" />')

        if self.horse:
            lines.append(f'            <equipment slot="Horse" id="Item.{self.horse}" />')
        if self.horse_harness:
            lines.append(f'            <equipment slot="HorseHarness" id="Item.{self.horse_harness}" />')

        lines.append('        </Equipments>')
        lines.append('    </NPCCharacter>')
        return '\n'.join(lines)


# =============================================================================
# EASTERLING REGULAR ARMOR (T1-T5) — spiky Loke variants
# Also used by Balcoth, Far-Rhun, Kharaghûl
# =============================================================================
EAST_REG_HEAD = {
    1: "sk_rh_loke_helmet_east_light_a",
    2: "sk_rh_loke_helmet_east_med_a",
    3: "sk_rh_loke_helmet_east_med_b",
    4: "sk_rh_loke_helmet_east_heavy_a",
    5: "sk_rh_loke_helmet_east_heavy_b",
}
EAST_REG_BODY = {
    1: "sk_rh_loke_tunic_a",
    2: "sk_rh_drag_chainmail_a",
    3: "sk_rh_loke_chest_light_c",
    4: "sk_rh_loke_chest_med_c",
    5: "sk_rh_loke_chest_heavy_c",
}
EAST_REG_CAPE = {
    1: "", 2: "",
    3: "sk_rh_loke_pauldron_light_a",
    4: "sk_rh_loke_pauldron_med_a",
    5: "sk_rh_loke_pauldron_heavy_a",
}
EAST_REG_GLOVES = {
    1: "", 2: "",
    3: "sk_rh_loke_bracer_light_a",
    4: "sk_rh_loke_bracer_med_a",
    5: "sk_rh_loke_bracer_heavy_a",
}
EAST_REG_LEG = {
    1: "sk_rh_loke_boots_a",
    2: "sk_rh_loke_boots_a",
    3: "sk_rh_loke_grvs_light_a",
    4: "sk_rh_loke_grvs_heavy_a",
    5: "sk_rh_loke_grvs_heavy_b",
}

# Easterling Regular weapons by tier
EAST_REG_SWORDS = {1: "aserai_sword_3_t3", 2: "aserai_sword_3_t3", 3: "aserai_sword_3_t3", 4: "aserai_sword_5_t4", 5: "aserai_sword_5_t4"}
EAST_REG_SPEARS = {1: "eastern_spear_3_t3", 2: "eastern_spear_3_t3", 3: "eastern_spear_3_t3", 4: "easterling_spear", 5: "mirkwood_spear_a01"}
EAST_REG_SHIELDS = {1: "desert_round_shield", 2: "desert_round_shield", 3: "desert_round_shield", 4: "easterling_shield", 5: "easterling_shield"}
EAST_REG_BOWS = {1: "composite_bow", 2: "composite_bow", 3: "steppe_heavy_bow", 4: "composite_steppe_bow", 5: "steppe_war_bow"}
EAST_REG_LANCES = {3: "eastern_spear_3_t3", 4: "eastern_spear_4_t4", 5: "khuzait_lance_2_t4"}
EAST_REG_HORSES = {4: "t3_aserai_horse", 5: "t3_aserai_horse"}
EAST_REG_HARNESS = {4: "lrd_horse_armour_4", 5: "lrd_horse_armour_5_c"}


def equip_easterling_regular(t: Troop):
    """Apply Easterling Regular armor by tier."""
    tier = t.tier
    t.head = EAST_REG_HEAD.get(tier, "")
    t.body = EAST_REG_BODY.get(tier, "")
    t.cape = EAST_REG_CAPE.get(tier, "")
    t.gloves = EAST_REG_GLOVES.get(tier, "")
    t.leg = EAST_REG_LEG.get(tier, "")


# =============================================================================
# LOKE-RIM NOBLE ARMOR (T3-T7) — half-plate → plate, role-specific helmets
# =============================================================================
# Infantry / Shieldguard / Gilded Shieldguard helmets
LOKE_INF_HEAD = {
    3: "sk_rh_loke_helmet_inf_light_a",
    4: "sk_rh_loke_helmet_inf_med_a",
    5: "sk_rh_loke_helmet_inf_med_b",
    6: "sk_rh_loke_helmet_inf_heavy_a",
    7: "sk_rh_loke_helmet_inf_elite_a",
}
# Maceman / Gilded Champion helmets
LOKE_MACE_HEAD = {
    5: "sk_rh_loke_helmet_cult_med_a",
    6: "sk_rh_loke_helmet_cult_heavy_a",
    7: "sk_rh_loke_helmet_cult_elite_a",
}
# Archer / Marksman / Gilded Marksman helmets
LOKE_ARCH_HEAD = {
    4: "sk_rh_loke_hood_med_a",
    5: "sk_rh_loke_hood_med_b",
    6: "sk_rh_loke_hood_heavy_a",
    7: "sk_rh_loke_hood_heavy_b",
}
# Cavalry / Cataphract / Gilded Cataphract helmets
LOKE_CAV_HEAD = {
    5: "sk_rh_loke_helmet_cav_light_a",
    6: "sk_rh_loke_helmet_cav_med_a",
    7: "sk_rh_loke_helmet_cav_heavy_a",
}
# Half-plate body (T3-T5)
LOKE_HPLATE_BODY = {
    3: "sk_rh_loke_hplate_light_a",
    4: "sk_rh_loke_hplate_med_a",
    5: "sk_rh_loke_hplate_heavy_a",
}
# Plate body (T6-T7)
LOKE_PLATE_BODY = {
    6: "sk_rh_loke_plate_light_a",
    7: "sk_rh_loke_plate_med_a",
}
# Half-plate pauldrons (T3-T5)
LOKE_HPLATE_CAPE = {
    3: "sk_rh_loke_pauldron_hplate_light_a",
    4: "sk_rh_loke_pauldron_hplate_med_a",
    5: "sk_rh_loke_pauldron_hplate_heavy_a",
}
# Plate pauldrons (T6-T7)
LOKE_PLATE_CAPE = {
    6: "sk_rh_loke_pauldron_plate_light_a",
    7: "sk_rh_loke_pauldron_plate_heavy_a",
}
# Half-plate bracers
LOKE_HPLATE_GLOVES = {
    3: "sk_rh_loke_bracer_hplate_light_a",
    4: "sk_rh_loke_bracer_hplate_med_a",
    5: "sk_rh_loke_bracer_hplate_heavy_a",
}
# Plate bracers
LOKE_PLATE_GLOVES = {
    6: "sk_rh_loke_bracer_plate_light_a",
    7: "sk_rh_loke_bracer_plate_med_a",
}
# Half-plate boots
LOKE_HPLATE_LEG = {
    3: "sk_rh_loke_grvs_hplate_light_a",
    4: "sk_rh_loke_grvs_hplate_light_a",
    5: "sk_rh_loke_grvs_hplate_heavy_a",
}
# Plate boots
LOKE_PLATE_LEG = {
    6: "sk_rh_loke_grvs_plate_light_a",
    7: "sk_rh_loke_grvs_plate_med_a",
}

# Loke-Rim weapons
LOKE_MACE_1H = "sm_rh_loke_1h_mace_a"
LOKE_MACE_2H = "sm_rh_loke_2h_mace_a"
LOKE_SPEAR = "sm_rh_loke_spear_a"
LOKE_LONGBOW = "sm_rh_loke_longbow_a"
LOKE_LANCE = "khuzait_lance_2_t4"  # placeholder until Loke lance exists
LOKE_SHIELDS_CAV = {5: "sm_rh_loke_shield_cav_med_a", 6: "sm_rh_loke_shield_cav_med_b", 7: "sm_rh_loke_shield_cav_heavy_a"}
LOKE_SHIELDS_INF = {5: "sm_rh_loke_shield_med_a", 6: "sm_rh_loke_shield_heavy_a", 7: "sm_rh_loke_shield_heavy_c"}
LOKE_SHIELDS_TOWER = {6: "sm_rh_loke_shield_tower_med_a", 7: "sm_rh_loke_shield_tower_heavy_a"}


def _loke_body(tier: int) -> str:
    if tier <= 5:
        return LOKE_HPLATE_BODY.get(tier, "")
    return LOKE_PLATE_BODY.get(tier, "")

def _loke_cape(tier: int) -> str:
    if tier <= 5:
        return LOKE_HPLATE_CAPE.get(tier, "")
    return LOKE_PLATE_CAPE.get(tier, "")

def _loke_gloves(tier: int) -> str:
    if tier <= 5:
        return LOKE_HPLATE_GLOVES.get(tier, "")
    return LOKE_PLATE_GLOVES.get(tier, "")

def _loke_leg(tier: int) -> str:
    if tier <= 5:
        return LOKE_HPLATE_LEG.get(tier, "")
    return LOKE_PLATE_LEG.get(tier, "")


def equip_loke_infantry(t: Troop):
    tier = t.tier
    t.head = LOKE_INF_HEAD.get(tier, "")
    t.body = _loke_body(tier)
    t.cape = _loke_cape(tier)
    t.gloves = _loke_gloves(tier)
    t.leg = _loke_leg(tier)
    t.weapons = [LOKE_MACE_1H]
    if tier >= 6:
        t.weapons.append(LOKE_SPEAR)
    t.shield = LOKE_SHIELDS_INF.get(tier, "sm_rh_loke_shield_med_a")

def equip_loke_maceman(t: Troop):
    tier = t.tier
    t.head = LOKE_MACE_HEAD.get(tier, "")
    t.body = _loke_body(tier)
    t.cape = _loke_cape(tier)
    t.gloves = _loke_gloves(tier)
    t.leg = _loke_leg(tier)
    t.weapons = [LOKE_MACE_2H]
    t.shield = ""

def equip_loke_archer(t: Troop):
    tier = t.tier
    t.head = LOKE_ARCH_HEAD.get(tier, "")
    t.body = _loke_body(tier)
    t.cape = _loke_cape(tier)
    t.gloves = _loke_gloves(tier)
    t.leg = _loke_leg(tier)
    if tier >= 6:
        t.weapons = [LOKE_LONGBOW, "bodkin_arrows_b", "bodkin_arrows_b", "rhun_1h_sword_b"]
    else:
        t.weapons = ["composite_steppe_bow", "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""

def equip_loke_cavalry(t: Troop):
    tier = t.tier
    t.head = LOKE_CAV_HEAD.get(tier, "")
    t.body = _loke_body(tier)
    t.cape = _loke_cape(tier)
    t.gloves = _loke_gloves(tier)
    t.leg = _loke_leg(tier)
    t.weapons = [LOKE_LANCE, LOKE_MACE_1H]
    t.shield = LOKE_SHIELDS_CAV.get(tier, "sm_rh_loke_shield_cav_med_a")
    t.horse = "t3_aserai_horse"
    t.horse_harness = "lrd_horse_armour_4"


# =============================================================================
# DRAGON-WRATH ARMOR (T5-T9) — half-plate → plate, role-specific helmets
# =============================================================================
DRAG_INF_HEAD = {
    5: "sk_rh_drag_helmet_inf_light_a",
    6: "sk_rh_drag_helmet_inf_med_a",
    7: "sk_rh_drag_helmet_inf_med_b",
    8: "sk_rh_drag_helmet_inf_heavy_a",
    9: "sk_rh_drag_helmet_inf_elite_a",
}
DRAG_ARCH_HEAD = {
    6: "sk_rh_drag_helmet_cult_elite_d",
    7: "sk_rh_drag_helmet_cult_elite_d",
    8: "sk_rh_drag_helmet_cult_elite_e",
    9: "sk_rh_drag_helmet_cult_elite_f",
}
DRAG_CAV_HEAD = {
    7: "sk_rh_drag_helmet_cav_light_a",
    8: "sk_rh_drag_helmet_cav_med_a",
    9: "sk_rh_drag_helmet_cav_heavy_a",
}
DRAG_HPLATE_BODY = {
    5: "sk_rh_drag_hplate_light_a",
    6: "sk_rh_drag_hplate_med_a",
    7: "sk_rh_drag_hplate_heavy_a",
}
DRAG_PLATE_BODY = {
    8: "sk_rh_drag_plate_light_a",
    9: "sk_rh_drag_plate_med_a",
}
DRAG_HPLATE_CAPE = {
    5: "sk_rh_drag_pauldron_hplate_light_a",
    6: "sk_rh_drag_pauldron_hplate_med_a",
    7: "sk_rh_drag_pauldron_hplate_heavy_a",
}
DRAG_PLATE_CAPE = {
    8: "sk_rh_drag_pauldron_plate_light_a",
    9: "sk_rh_drag_pauldron_plate_heavy_a",
}
DRAG_HPLATE_GLOVES = {
    5: "sk_rh_drag_bracer_hplate_light_a",
    6: "sk_rh_drag_bracer_hplate_med_a",
    7: "sk_rh_drag_bracer_hplate_heavy_a",
}
DRAG_PLATE_GLOVES = {
    8: "sk_rh_drag_bracer_plate_light_a",
    9: "sk_rh_drag_bracer_plate_med_a",
}
DRAG_HPLATE_LEG = {
    5: "sk_rh_drag_grvs_hplate_light_a",
    6: "sk_rh_drag_grvs_hplate_light_a",
    7: "sk_rh_drag_grvs_hplate_heavy_a",
}
DRAG_PLATE_LEG = {
    8: "sk_rh_drag_grvs_plate_light_a",
    9: "sk_rh_drag_grvs_plate_med_a",
}

# Dragon-Wrath weapons
DRAG_MACE_1H = "sm_rh_drag_1h_mace_a"
DRAG_MACE_2H = "sm_rh_drag_2h_mace_a"
DRAG_SPEAR = "sm_rh_drag_spear_a"
DRAG_LONGBOW = "sm_rh_drag_longbow_a"
DRAG_LANCE = "khuzait_lance_2_t4"
DRAG_AXE_1H = "sm_rh_drag_1h_axe_a"
DRAG_AXE_2H = "sm_rh_drag_2h_axe_a"
DRAG_SHIELDS_CAV = {7: "sm_rh_drag_shield_cav_med_a", 8: "sm_rh_drag_shield_cav_med_b", 9: "sm_rh_drag_shield_cav_heavy_a"}
DRAG_SHIELDS_INF = {6: "sm_rh_drag_shield_med_a", 7: "sm_rh_drag_shield_heavy_a", 8: "sm_rh_drag_shield_heavy_a", 9: "sm_rh_drag_shield_heavy_c"}
DRAG_SHIELDS_TOWER = {8: "sm_rh_drag_shield_tower_med_a", 9: "sm_rh_drag_shield_tower_heavy_a"}


def _drag_body(tier: int) -> str:
    if tier <= 7:
        return DRAG_HPLATE_BODY.get(tier, "")
    return DRAG_PLATE_BODY.get(tier, "")

def _drag_cape(tier: int) -> str:
    if tier <= 7:
        return DRAG_HPLATE_CAPE.get(tier, "")
    return DRAG_PLATE_CAPE.get(tier, "")

def _drag_gloves(tier: int) -> str:
    if tier <= 7:
        return DRAG_HPLATE_GLOVES.get(tier, "")
    return DRAG_PLATE_GLOVES.get(tier, "")

def _drag_leg(tier: int) -> str:
    if tier <= 7:
        return DRAG_HPLATE_LEG.get(tier, "")
    return DRAG_PLATE_LEG.get(tier, "")


def equip_dragon_infantry(t: Troop):
    tier = t.tier
    t.head = DRAG_INF_HEAD.get(tier, "")
    t.body = _drag_body(tier)
    t.cape = _drag_cape(tier)
    t.gloves = _drag_gloves(tier)
    t.leg = _drag_leg(tier)
    t.weapons = [DRAG_MACE_1H]
    if tier >= 8:
        t.weapons.append(DRAG_SPEAR)
    t.shield = DRAG_SHIELDS_INF.get(tier, "sm_rh_drag_shield_med_a")

def equip_dragon_executioner(t: Troop):
    tier = t.tier
    t.head = DRAG_INF_HEAD.get(tier, "")
    t.body = _drag_body(tier)
    t.cape = _drag_cape(tier)
    t.gloves = _drag_gloves(tier)
    t.leg = _drag_leg(tier)
    t.weapons = [DRAG_MACE_2H]
    t.shield = ""

def equip_dragon_archer(t: Troop):
    tier = t.tier
    t.head = DRAG_ARCH_HEAD.get(tier, "")
    t.body = _drag_body(tier)
    t.cape = _drag_cape(tier)
    t.gloves = _drag_gloves(tier)
    t.leg = _drag_leg(tier)
    if tier >= 8:
        t.weapons = [DRAG_LONGBOW, "bodkin_arrows_b", "bodkin_arrows_b", DRAG_MACE_1H]
    else:
        t.weapons = ["steppe_war_bow", "bodkin_arrows_b", "bodkin_arrows_b", DRAG_MACE_1H]
    t.shield = ""

def equip_dragon_cavalry(t: Troop):
    tier = t.tier
    t.head = DRAG_CAV_HEAD.get(tier, "")
    t.body = _drag_body(tier)
    t.cape = _drag_cape(tier)
    t.gloves = _drag_gloves(tier)
    t.leg = _drag_leg(tier)
    t.weapons = [DRAG_LANCE, DRAG_MACE_1H]
    t.shield = DRAG_SHIELDS_CAV.get(tier, "sm_rh_drag_shield_cav_med_a")
    t.horse = "t3_aserai_horse"
    t.horse_harness = "lrd_horse_armour_4"


# =============================================================================
# WAINRIDER ARMOR (T3-T7) — Loke arch helmets + lamellar
# =============================================================================
WAIN_HEAD = {
    3: "sk_rh_loke_helmet_arch_light_a",
    4: "sk_rh_loke_helmet_arch_med_a",
    5: "sk_rh_loke_helmet_arch_med_b",
    6: "sk_rh_loke_helmet_arch_heavy_a",
    7: "sk_rh_loke_helmet_arch_elite_a",
}
WAIN_BODY = {
    3: "sk_rh_loke_chest_light_a",
    4: "sk_rh_loke_chest_light_b",
    5: "sk_rh_loke_chest_med_a",
    6: "sk_rh_loke_chest_heavy_a",
    7: "sk_rh_loke_chest_elite_a",
}
WAIN_CAPE = {
    3: "", 4: "",
    5: "sk_rh_loke_pauldron_lam_light_a",
    6: "sk_rh_loke_pauldron_lam_med_a",
    7: "sk_rh_loke_pauldron_lam_heavy_a",
}
WAIN_GLOVES = {
    3: "", 4: "",
    5: "sk_rh_loke_bracer_lam_light_a",
    6: "sk_rh_loke_bracer_lam_med_a",
    7: "sk_rh_loke_bracer_lam_heavy_a",
}
WAIN_LEG = {
    3: "sk_rh_loke_boots_a",
    4: "sk_rh_loke_boots_a",
    5: "sk_rh_loke_grvs_scale_light_a",
    6: "sk_rh_loke_grvs_scale_light_a",
    7: "sk_rh_loke_grvs_scale_heavy_a",
}

WAIN_GLAIVES = {3: "rhun_polearm_a", 4: "rhun_polearm_a", 5: "rhun_polearm_b", 6: "rhun_polearm_c", 7: "rhun_polearm_c"}
WAIN_SHIELDS = {3: "rhun_round_shield_a", 4: "rhun_round_shield_a", 5: "rhun_round_shield_b", 6: "rhun_round_shield_b", 7: "rhun_round_shield_c"}
WAIN_SWORDS = {3: "rhun_1h_sword_a", 4: "rhun_1h_sword_a", 5: "rhun_1h_sword_a", 6: "rhun_1h_sword_b", 7: "rhun_1h_sword_b"}
WAIN_LANCES = {5: "rhun_lance_a", 6: "rhun_lance_b", 7: "rhun_lance_c"}
WAIN_HORSES = {5: "khuzait_horse", 6: "khuzait_war_horse", 7: "khuzait_charger"}
WAIN_HARNESS = {5: "light_harness", 6: "chain_horse_harness", 7: "half_scale_barding"}


def equip_wainrider_infantry(t: Troop):
    tier = t.tier
    t.head = WAIN_HEAD.get(tier, "")
    t.body = WAIN_BODY.get(tier, "")
    t.cape = WAIN_CAPE.get(tier, "")
    t.gloves = WAIN_GLOVES.get(tier, "")
    t.leg = WAIN_LEG.get(tier, "")
    t.weapons = [WAIN_GLAIVES[tier], WAIN_SWORDS[tier]]
    t.shield = WAIN_SHIELDS.get(tier, "")
    if tier == 7:  # Darkhan gets javelins
        t.weapons.append("eastern_javelin_3_t4")

def equip_wainrider_cavalry(t: Troop):
    tier = t.tier
    t.head = WAIN_HEAD.get(tier, "")
    t.body = WAIN_BODY.get(tier, "")
    t.cape = WAIN_CAPE.get(tier, "")
    t.gloves = WAIN_GLOVES.get(tier, "")
    t.leg = WAIN_LEG.get(tier, "")
    t.weapons = [WAIN_LANCES.get(tier, "rhun_lance_a"), WAIN_SWORDS[tier]]
    t.shield = WAIN_SHIELDS.get(tier, "")
    t.horse = WAIN_HORSES.get(tier, "khuzait_horse")
    t.horse_harness = WAIN_HARNESS.get(tier, "light_harness")


# =============================================================================
# BLACK SUN MERCENARIES ARMOR (T2-T8) — Drag lamellar (shock) + spiky (archer)
# =============================================================================
BSUN_SHOCK_HEAD = {
    2: "sk_rh_drag_helmet_arch_light_a",
    3: "sk_rh_drag_helmet_arch_light_a",
    4: "sk_rh_drag_helmet_arch_med_a",
    5: "sk_rh_drag_helmet_arch_med_b",
    6: "sk_rh_drag_helmet_arch_heavy_a",
    7: "sk_rh_drag_helmet_arch_elite_a",
}
BSUN_SHOCK_BODY = {
    2: "sk_rh_drag_chest_light_a",
    3: "sk_rh_drag_chest_med_a",
    4: "sk_rh_drag_chest_light_a",
    5: "sk_rh_drag_chest_med_a",
    6: "sk_rh_drag_chest_heavy_a",
    7: "sk_rh_drag_chest_elite_a",
}
BSUN_SHOCK_CAPE = {
    2: "", 3: "sk_rh_drag_pauldron_lam_light_a",
    4: "sk_rh_drag_pauldron_lam_light_a",
    5: "sk_rh_drag_pauldron_lam_med_a",
    6: "sk_rh_drag_pauldron_lam_heavy_a",
    7: "sk_rh_drag_cape_scale_heavy_a",
}
BSUN_SHOCK_GLOVES = {
    2: "", 3: "sk_rh_drag_bracer_lam_light_a",
    4: "sk_rh_drag_bracer_lam_light_a",
    5: "sk_rh_drag_bracer_lam_med_a",
    6: "sk_rh_drag_bracer_lam_heavy_a",
    7: "sk_rh_drag_bracer_lam_heavy_a",
}
BSUN_SHOCK_LEG = {
    2: "sk_rh_drag_grvs_scale_light_a",
    3: "sk_rh_drag_grvs_scale_light_a",
    4: "sk_rh_drag_grvs_scale_light_a",
    5: "sk_rh_drag_grvs_scale_light_a",
    6: "sk_rh_drag_grvs_scale_heavy_a",
    7: "sk_rh_drag_grvs_scale_heavy_a",
}

BSUN_ARCHER_HEAD = {
    4: "sk_rh_drag_hood_med_a",
    5: "sk_rh_drag_hood_med_b",
    6: "sk_rh_drag_hood_heavy_a",
    7: "sk_rh_drag_hood_heavy_b",
    8: "sk_rh_drag_hood_heavy_c",
}
BSUN_ARCHER_BODY = {
    4: "sk_rh_drag_chest_light_c",
    5: "sk_rh_drag_chest_light_d",
    6: "sk_rh_drag_chest_med_c",
    7: "sk_rh_drag_chest_heavy_c",
    8: "sk_rh_drag_chest_heavy_d",
}
BSUN_ARCHER_CAPE = {
    4: "sk_rh_drag_pauldron_light_a",
    5: "sk_rh_drag_pauldron_med_a",
    6: "sk_rh_drag_pauldron_heavy_a",
    7: "sk_rh_drag_cape_heavy_a",
    8: "sk_rh_drag_cape_heavy_b",
}
BSUN_ARCHER_GLOVES = {
    4: "sk_rh_drag_bracer_light_a",
    5: "sk_rh_drag_bracer_med_a",
    6: "sk_rh_drag_bracer_heavy_a",
    7: "sk_rh_drag_bracer_heavy_b",
    8: "sk_rh_drag_bracer_heavy_c",
}
BSUN_ARCHER_LEG = {
    4: "sk_rh_drag_grvs_light_a",
    5: "sk_rh_drag_grvs_light_b",
    6: "sk_rh_drag_grvs_heavy_a",
    7: "sk_rh_drag_grvs_heavy_a",
    8: "sk_rh_drag_grvs_heavy_b",
}


def equip_bsun_shock(t: Troop):
    tier = t.tier
    t.head = BSUN_SHOCK_HEAD.get(tier, "")
    t.body = BSUN_SHOCK_BODY.get(tier, "")
    t.cape = BSUN_SHOCK_CAPE.get(tier, "")
    t.gloves = BSUN_SHOCK_GLOVES.get(tier, "")
    t.leg = BSUN_SHOCK_LEG.get(tier, "")
    if tier >= 6:
        t.weapons = [DRAG_AXE_2H]
    else:
        t.weapons = [DRAG_AXE_1H]
    t.shield = ""

def equip_bsun_archer(t: Troop):
    tier = t.tier
    t.head = BSUN_ARCHER_HEAD.get(tier, "")
    t.body = BSUN_ARCHER_BODY.get(tier, "")
    t.cape = BSUN_ARCHER_CAPE.get(tier, "")
    t.gloves = BSUN_ARCHER_GLOVES.get(tier, "")
    t.leg = BSUN_ARCHER_LEG.get(tier, "")
    if tier >= 6:
        t.weapons = [DRAG_LONGBOW, "bodkin_arrows_b", "bodkin_arrows_b", DRAG_MACE_1H]
    else:
        t.weapons = ["long_bow", "bodkin_arrows_b", "bodkin_arrows_b"]
    t.shield = ""


# =============================================================================
# DARKHÛN MERCENARIES ARMOR (T2-T8) — Khamul (sk_dg_khml_)
# Infantry = half-plate, Cavalry = lamellar
# =============================================================================
DARKH_INF_HEAD = {
    2: "sk_dg_khml_helmet_arch_light_a",
    3: "sk_dg_khml_helmet_arch_light_a",
    4: "sk_dg_khml_helmet_arch_med_a",
    5: "sk_dg_khml_helmet_arch_med_b",
    6: "sk_dg_khml_helmet_arch_heavy_a",
    7: "sk_dg_khml_helmet_arch_elite_a",
}
DARKH_INF_BODY = {
    2: "", 3: "",
    4: "sk_dg_khml_hplate_light_a",
    5: "sk_dg_khml_hplate_med_a",
    6: "sk_dg_khml_hplate_heavy_a",
    7: "sk_dg_khml_hplate_elite_a",
}
DARKH_INF_CAPE = {
    2: "", 3: "",
    4: "sk_dg_khml_pauldron_hplate_light_a",
    5: "sk_dg_khml_pauldron_hplate_med_a",
    6: "sk_dg_khml_pauldron_hplate_heavy_a",
    7: "sk_dg_khml_cape_hplate_heavy_a",
}
DARKH_INF_GLOVES = {
    2: "", 3: "",
    4: "", 5: "sk_dg_khml_bracer_hplate_light_a",
    6: "sk_dg_khml_bracer_hplate_med_a",
    7: "sk_dg_khml_bracer_hplate_heavy_a",
}
DARKH_INF_LEG = {
    2: "", 3: "",
    4: "sk_dg_khml_grvs_hplate_light_a",
    5: "sk_dg_khml_grvs_hplate_light_a",
    6: "sk_dg_khml_grvs_hplate_heavy_a",
    7: "sk_dg_khml_grvs_hplate_heavy_a",
}

DARKH_CAV_HEAD = {
    4: "sk_dg_khml_helmet_cult_med_a",
    5: "sk_dg_khml_helmet_cult_med_a",
    6: "sk_dg_khml_helmet_cult_heavy_a",
    7: "sk_dg_khml_helmet_cult_heavy_b",
    8: "sk_dg_khml_helmet_cult_elite_a",
}
DARKH_CAV_BODY = {
    4: "sk_dg_khml_chest_light_a",
    5: "sk_dg_khml_chest_med_a",
    6: "sk_dg_khml_chest_heavy_a",
    7: "sk_dg_khml_chest_elite_a",
    8: "sk_dg_khml_chest_elite_b",
}
DARKH_CAV_CAPE = {
    4: "",
    5: "sk_dg_khml_pauldron_lam_light_a",
    6: "sk_dg_khml_pauldron_lam_med_a",
    7: "sk_dg_khml_pauldron_lam_heavy_a",
    8: "sk_dg_khml_cape_scale_heavy_a",
}
DARKH_CAV_GLOVES = {
    4: "",
    5: "sk_dg_khml_bracer_lam_light_a",
    6: "sk_dg_khml_bracer_lam_med_a",
    7: "sk_dg_khml_bracer_lam_heavy_a",
    8: "sk_dg_khml_bracer_lam_heavy_a",
}
DARKH_CAV_LEG = {
    4: "sk_dg_khml_grvs_scale_light_a",
    5: "sk_dg_khml_grvs_scale_light_a",
    6: "sk_dg_khml_grvs_scale_heavy_a",
    7: "sk_dg_khml_grvs_scale_heavy_a",
    8: "sk_dg_khml_grvs_scale_heavy_a",
}

KHML_AXE_1H = "sm_dg_khml_1h_axe_a"
KHML_SWORD_1H = "sm_dg_khml_1h_sword_a"
KHML_SPEAR = "sm_dg_khml_spear_a"
KHML_LANCE = "khuzait_lance_2_t4"
KHML_SHIELDS_INF = {4: "sm_dg_khml_shield_med_a", 5: "sm_dg_khml_shield_med_b", 6: "sm_dg_khml_shield_heavy_a", 7: "sm_dg_khml_shield_heavy_b"}
KHML_SHIELDS_TOWER = {6: "sm_dg_khml_shield_tower_med_a", 7: "sm_dg_khml_shield_tower_heavy_a"}
KHML_SHIELDS_CAV = {4: "sm_dg_khml_shield_cav_med_a", 5: "sm_dg_khml_shield_cav_med_a", 6: "sm_dg_khml_shield_cav_med_b", 7: "sm_dg_khml_shield_cav_heavy_a", 8: "sm_dg_khml_shield_cav_heavy_a"}


def equip_darkhun_infantry(t: Troop):
    tier = t.tier
    t.head = DARKH_INF_HEAD.get(tier, "")
    t.body = DARKH_INF_BODY.get(tier, "")
    t.cape = DARKH_INF_CAPE.get(tier, "")
    t.gloves = DARKH_INF_GLOVES.get(tier, "")
    t.leg = DARKH_INF_LEG.get(tier, "")
    t.weapons = [KHML_AXE_1H, KHML_SPEAR]
    if tier >= 6:
        t.shield = KHML_SHIELDS_TOWER.get(tier, "sm_dg_khml_shield_tower_med_a")
    else:
        t.shield = KHML_SHIELDS_INF.get(tier, "sm_dg_khml_shield_med_a")

def equip_darkhun_cavalry(t: Troop):
    tier = t.tier
    t.head = DARKH_CAV_HEAD.get(tier, "")
    t.body = DARKH_CAV_BODY.get(tier, "")
    t.cape = DARKH_CAV_CAPE.get(tier, "")
    t.gloves = DARKH_CAV_GLOVES.get(tier, "")
    t.leg = DARKH_CAV_LEG.get(tier, "")
    t.weapons = [KHML_LANCE, KHML_SWORD_1H]
    t.shield = KHML_SHIELDS_CAV.get(tier, "sm_dg_khml_shield_cav_med_a")
    t.horse = "t3_aserai_horse"
    t.horse_harness = "lrd_horse_armour_4"


# =============================================================================
# SAGARÛN ARMOR (T3-T7) — Marine = Loke Scalemail, Naffatun/Arbalest = Drag Scalemail
# =============================================================================
SAG_MARINE_HEAD = {
    3: "sk_rh_loke_helmet_cult_med_a",
    4: "sk_rh_loke_helmet_cult_med_a",
    5: "sk_rh_loke_helmet_cult_heavy_a",
    6: "sk_rh_loke_helmet_cult_heavy_b",
    7: "sk_rh_loke_helmet_cult_elite_a",
}
SAG_MARINE_BODY = {
    3: "sk_rh_loke_scalemail_light_a",
    4: "sk_rh_loke_scalemail_light_b",
    5: "sk_rh_loke_scalemail_med_a",
    6: "sk_rh_loke_scalemail_heavy_a",
    7: "sk_rh_loke_scalemail_elite_a",
}
SAG_MARINE_CAPE = {
    3: "", 4: "", 5: "",
    6: "sk_rh_loke_pauldron_scale_heavy_a",
    7: "sk_rh_loke_cape_scale_heavy_a",
}
SAG_MARINE_GLOVES = {
    3: "", 4: "",
    5: "sk_rh_loke_bracer_scale_med_a",
    6: "sk_rh_loke_bracer_scale_heavy_a",
    7: "sk_rh_loke_bracer_scale_heavy_a",
}
SAG_MARINE_LEG = {
    3: "sk_rh_loke_grvs_scale_light_a",
    4: "sk_rh_loke_grvs_scale_light_a",
    5: "sk_rh_loke_grvs_scale_light_a",
    6: "sk_rh_loke_grvs_scale_heavy_a",
    7: "sk_rh_loke_grvs_scale_heavy_a",
}

SAG_NAFF_HEAD = {
    5: "sk_rh_drag_helmet_arch_light_a",
    6: "sk_rh_drag_helmet_arch_med_a",
    7: "sk_rh_drag_helmet_arch_heavy_a",
}
SAG_NAFF_BODY = {
    5: "sk_rh_drag_scalemail_light_a",
    6: "sk_rh_drag_scalemail_med_a",
    7: "sk_rh_drag_scalemail_heavy_a",
}
SAG_NAFF_CAPE = {
    5: "", 6: "sk_rh_drag_pauldron_scale_heavy_a",
    7: "sk_rh_drag_cape_scale_heavy_a",
}
SAG_NAFF_GLOVES = {
    5: "", 6: "sk_rh_drag_bracer_scale_med_a",
    7: "sk_rh_drag_bracer_scale_heavy_a",
}
SAG_NAFF_LEG = {
    5: "sk_rh_drag_grvs_scale_light_a",
    6: "sk_rh_drag_grvs_scale_light_a",
    7: "sk_rh_drag_grvs_scale_heavy_a",
}


def equip_sagarun_marine(t: Troop):
    tier = t.tier
    t.head = SAG_MARINE_HEAD.get(tier, "")
    t.body = SAG_MARINE_BODY.get(tier, "")
    t.cape = SAG_MARINE_CAPE.get(tier, "")
    t.gloves = SAG_MARINE_GLOVES.get(tier, "")
    t.leg = SAG_MARINE_LEG.get(tier, "")
    t.weapons = [EAST_REG_SWORDS.get(tier, "aserai_sword_3_t3")]
    t.shield = EAST_REG_SHIELDS.get(tier, "desert_round_shield")

def equip_sagarun_naffatun(t: Troop):
    tier = t.tier
    t.head = SAG_NAFF_HEAD.get(tier, "")
    t.body = SAG_NAFF_BODY.get(tier, "")
    t.cape = SAG_NAFF_CAPE.get(tier, "")
    t.gloves = SAG_NAFF_GLOVES.get(tier, "")
    t.leg = SAG_NAFF_LEG.get(tier, "")
    t.weapons = ["throwing_spear", "throwing_spear"]
    t.shield = ""

def equip_sagarun_arbalest(t: Troop):
    tier = t.tier
    t.head = SAG_NAFF_HEAD.get(tier, "")
    t.body = SAG_NAFF_BODY.get(tier, "")
    t.cape = SAG_NAFF_CAPE.get(tier, "")
    t.gloves = SAG_NAFF_GLOVES.get(tier, "")
    t.leg = SAG_NAFF_LEG.get(tier, "")
    t.weapons = ["crossbow_b", "bolt_c", "bolt_c", EAST_REG_SWORDS.get(tier, "aserai_sword_3_t3")]
    t.shield = ""


# =============================================================================
# BUILD ALL TROOPS
# =============================================================================
def build_all_troops() -> list[Troop]:
    troops = []

    # =========================================================================
    # EASTERLING REGULAR UNITS (T1-T5)
    # =========================================================================
    t = Troop("easterling_recruit", "Easterling Recruit", 1, "Infantry", is_basic_troop=True)
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[1]]
    t.shield = EAST_REG_SHIELDS[1]
    t.upgrades = ["easterling_militia", "easterling_bowman"]
    troops.append(t)

    t = Troop("easterling_militia", "Easterling Militia", 2, "Infantry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[2]]
    t.shield = EAST_REG_SHIELDS[2]
    t.upgrades = ["easterling_footman_new"]
    troops.append(t)

    t = Troop("easterling_bowman", "Easterling Bowman", 2, "Ranged")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[2], "bodkin_arrows_b", "bodkin_arrows_b"]
    t.shield = ""
    t.upgrades = ["easterling_skirmisher_new"]
    troops.append(t)

    t = Troop("easterling_footman_new", "Easterling Footman", 3, "Infantry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[3]]
    t.shield = EAST_REG_SHIELDS[3]
    t.upgrades = ["easterling_swordsman_new", "easterling_halberdier_new", "easterling_cavalry_new"]
    troops.append(t)

    t = Troop("easterling_skirmisher_new", "Easterling Skirmisher", 3, "Ranged")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[3], "bodkin_arrows_b", "bodkin_arrows_b"]
    t.shield = ""
    t.upgrades = ["easterling_archer_new"]
    troops.append(t)

    t = Troop("easterling_swordsman_new", "Easterling Swordsman", 4, "Infantry", weapon_spec="sword")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[4]]
    t.shield = EAST_REG_SHIELDS[4]
    t.upgrades = ["easterling_veteran_swordsman_new"]
    troops.append(t)

    t = Troop("easterling_halberdier_new", "Easterling Halberdier", 4, "Infantry", weapon_spec="halberd")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SPEARS[4], EAST_REG_SWORDS[4]]
    t.shield = EAST_REG_SHIELDS[4]
    t.upgrades = ["easterling_veteran_halberdier_new"]
    troops.append(t)

    t = Troop("easterling_cavalry_new", "Easterling Cavalry", 4, "Cavalry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_LANCES[4], EAST_REG_SWORDS[4]]
    t.shield = "studded_adarga"
    t.horse = EAST_REG_HORSES[4]
    t.horse_harness = EAST_REG_HARNESS[4]
    t.upgrades = ["easterling_veteran_cavalry"]
    troops.append(t)

    t = Troop("easterling_archer_new", "Easterling Archer", 4, "Ranged")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[4], "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""
    t.upgrades = ["easterling_veteran_archer_new"]
    troops.append(t)

    t = Troop("easterling_veteran_swordsman_new", "Easterling Veteran Swordsman", 5, "Infantry", weapon_spec="sword")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[5]]
    t.shield = EAST_REG_SHIELDS[5]
    troops.append(t)

    t = Troop("easterling_veteran_halberdier_new", "Easterling Veteran Halberdier", 5, "Infantry", weapon_spec="halberd")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SPEARS[5], EAST_REG_SWORDS[5]]
    t.shield = EAST_REG_SHIELDS[5]
    troops.append(t)

    t = Troop("easterling_veteran_cavalry", "Easterling Veteran Cavalry", 5, "Cavalry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_LANCES[5], "khuzait_sword_4_t4"]
    t.shield = EAST_REG_SHIELDS[5]
    t.horse = EAST_REG_HORSES[5]
    t.horse_harness = EAST_REG_HARNESS[5]
    troops.append(t)

    t = Troop("easterling_veteran_archer_new", "Easterling Veteran Archer", 5, "Ranged")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[5], "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""
    troops.append(t)

    # =========================================================================
    # LOKE-RIM NOBLE UNITS (T3-T7)
    # =========================================================================
    t = Troop("loke_rim_initiate", "Loke-Rim Initiate", 3, "Infantry", is_basic_troop=True)
    equip_loke_infantry(t)
    t.upgrades = ["loke_rim_footman", "loke_rim_bowman"]
    troops.append(t)

    t = Troop("loke_rim_footman", "Loke-Rim Footman", 4, "Infantry")
    equip_loke_infantry(t)
    t.upgrades = ["loke_rim_cavalry", "loke_rim_infantry"]
    troops.append(t)

    t = Troop("loke_rim_bowman", "Loke-Rim Bowman", 4, "Ranged")
    equip_loke_archer(t)
    t.upgrades = ["loke_rim_archer"]
    troops.append(t)

    t = Troop("loke_rim_cavalry", "Loke-Rim Cavalry", 5, "Cavalry")
    equip_loke_cavalry(t)
    t.upgrades = ["loke_rim_cataphract"]
    troops.append(t)

    t = Troop("loke_rim_infantry", "Loke-Rim Infantry", 5, "Infantry")
    equip_loke_infantry(t)
    t.upgrades = ["loke_rim_shieldguard", "loke_rim_maceman"]
    troops.append(t)

    t = Troop("loke_rim_archer", "Loke-Rim Archer", 5, "Ranged")
    equip_loke_archer(t)
    t.upgrades = ["loke_rim_marksman"]
    troops.append(t)

    t = Troop("loke_rim_cataphract", "Loke-Rim Cataphract", 6, "Cavalry")
    equip_loke_cavalry(t)
    t.upgrades = ["loke_rim_gilded_cataphract"]
    troops.append(t)

    t = Troop("loke_rim_shieldguard", "Loke-Rim Shieldguard", 6, "Infantry")
    equip_loke_infantry(t)
    t.upgrades = ["loke_rim_gilded_shieldguard"]
    troops.append(t)

    t = Troop("loke_rim_maceman", "Loke-Rim Maceman", 6, "Infantry", weapon_spec="mace")
    equip_loke_maceman(t)
    t.upgrades = ["loke_rim_gilded_champion"]
    troops.append(t)

    t = Troop("loke_rim_marksman", "Loke-Rim Marksman", 6, "Ranged")
    equip_loke_archer(t)
    t.upgrades = ["loke_rim_gilded_marksman"]
    troops.append(t)

    t = Troop("loke_rim_gilded_cataphract", "Loke-Rim Gilded Cataphract", 7, "Cavalry")
    equip_loke_cavalry(t)
    troops.append(t)

    t = Troop("loke_rim_gilded_shieldguard", "Loke-Rim Gilded Shieldguard", 7, "Infantry")
    equip_loke_infantry(t)
    troops.append(t)

    t = Troop("loke_rim_gilded_champion", "Loke-Rim Gilded Champion", 7, "Infantry", weapon_spec="mace")
    equip_loke_maceman(t)
    troops.append(t)

    t = Troop("loke_rim_gilded_marksman", "Loke-Rim Gilded Marksman", 7, "Ranged")
    equip_loke_archer(t)
    troops.append(t)

    # =========================================================================
    # DRAGON-WRATH REGIONAL UNITS (T5-T9)
    # =========================================================================
    t = Troop("dragon_wrath_acolyte", "Dragon-Wrath Acolyte", 5, "Infantry", is_basic_troop=True)
    equip_dragon_infantry(t)
    t.upgrades = ["dragon_wrath_disciple", "dragon_wrath_archer"]
    troops.append(t)

    t = Troop("dragon_wrath_disciple", "Dragon-Wrath Disciple", 6, "Infantry")
    equip_dragon_infantry(t)
    t.upgrades = ["dragon_wrath_lancer", "dragon_wrath_infantry"]
    troops.append(t)

    t = Troop("dragon_wrath_archer", "Dragon-Wrath Archer", 6, "Ranged")
    equip_dragon_archer(t)
    t.upgrades = ["dragon_wrath_longbowman"]
    troops.append(t)

    t = Troop("dragon_wrath_lancer", "Dragon-Wrath Lancer", 7, "Cavalry")
    equip_dragon_cavalry(t)
    t.upgrades = ["dragon_wrath_ash_knight"]
    troops.append(t)

    t = Troop("dragon_wrath_infantry", "Dragon-Wrath Infantry", 7, "Infantry")
    equip_dragon_infantry(t)
    t.upgrades = ["dragon_wrath_ash_shieldguard", "dragon_wrath_ash_executioner"]
    troops.append(t)

    t = Troop("dragon_wrath_longbowman", "Dragon-Wrath Longbowman", 7, "Ranged")
    equip_dragon_archer(t)
    t.upgrades = ["dragon_wrath_ash_marksman"]
    troops.append(t)

    t = Troop("dragon_wrath_ash_knight", "Dragon-Wrath Ash Knight", 8, "Cavalry")
    equip_dragon_cavalry(t)
    t.upgrades = ["dragon_wrath_obsidian_knight"]
    troops.append(t)

    t = Troop("dragon_wrath_ash_shieldguard", "Dragon-Wrath Ash Shieldguard", 8, "Infantry")
    equip_dragon_infantry(t)
    t.upgrades = ["dragon_wrath_obsidian_shieldmaster"]
    troops.append(t)

    t = Troop("dragon_wrath_ash_executioner", "Dragon-Wrath Ash Executioner", 8, "Infantry", weapon_spec="mace")
    equip_dragon_executioner(t)
    t.upgrades = ["dragon_wrath_obsidian_war_reaver"]
    troops.append(t)

    t = Troop("dragon_wrath_ash_marksman", "Dragon-Wrath Ash Marksman", 8, "Ranged")
    equip_dragon_archer(t)
    t.upgrades = ["dragon_wrath_obsidian_warbow"]
    troops.append(t)

    t = Troop("dragon_wrath_obsidian_knight", "Dragon-Wrath Obsidian Knight", 9, "Cavalry")
    equip_dragon_cavalry(t)
    troops.append(t)

    t = Troop("dragon_wrath_obsidian_shieldmaster", "Dragon-Wrath Obsidian Shieldmaster", 9, "Infantry")
    equip_dragon_infantry(t)
    troops.append(t)

    t = Troop("dragon_wrath_obsidian_war_reaver", "Dragon-Wrath Obsidian War-Reaver", 9, "Infantry", weapon_spec="mace")
    equip_dragon_executioner(t)
    troops.append(t)

    t = Troop("dragon_wrath_obsidian_warbow", "Dragon-Wrath Obsidian Warbow", 9, "Ranged")
    equip_dragon_archer(t)
    troops.append(t)

    # =========================================================================
    # WAINRIDERS REGIONAL UNITS (T3-T7)
    # =========================================================================
    t = Troop("wain_youngblood", "Wain Youngblood", 3, "Infantry", is_basic_troop=True, weapon_spec="glaive")
    equip_wainrider_infantry(t)
    t.upgrades = ["wain_footman"]
    troops.append(t)

    t = Troop("wain_footman", "Wain Footman", 4, "Infantry", weapon_spec="glaive")
    equip_wainrider_infantry(t)
    t.upgrades = ["wain_glaiveman", "wainrider_horseman"]
    troops.append(t)

    t = Troop("wain_glaiveman", "Wain Glaiveman", 5, "Infantry", weapon_spec="glaive")
    equip_wainrider_infantry(t)
    t.upgrades = ["wain_iron_glaive"]
    troops.append(t)

    t = Troop("wainrider_horseman", "Wainrider Horseman", 5, "Cavalry")
    equip_wainrider_cavalry(t)
    t.upgrades = ["wainrider_cavalry"]
    troops.append(t)

    t = Troop("wain_iron_glaive", "Wain Iron-Glaive", 6, "Infantry", weapon_spec="glaive")
    equip_wainrider_infantry(t)
    t.upgrades = ["wain_darkhan"]
    troops.append(t)

    t = Troop("wainrider_cavalry", "Wainrider Cavalry", 6, "Cavalry")
    equip_wainrider_cavalry(t)
    t.upgrades = ["wainrider_khans_chosen"]
    troops.append(t)

    t = Troop("wain_darkhan", "Wain Darkhan", 7, "Infantry", weapon_spec="glaive")
    equip_wainrider_infantry(t)
    troops.append(t)

    t = Troop("wainrider_khans_chosen", "Wainrider Khan's Chosen", 7, "Cavalry")
    equip_wainrider_cavalry(t)
    troops.append(t)

    # =========================================================================
    # BLACK SUN MERCENARIES (T2-T8)
    # =========================================================================
    t = Troop("black_sun_trainee", "Black Sun Trainee", 2, "Infantry", is_basic_troop=True)
    equip_bsun_shock(t)
    t.upgrades = ["black_sun_footman"]
    troops.append(t)

    t = Troop("black_sun_footman", "Black Sun Footman", 3, "Infantry")
    equip_bsun_shock(t)
    t.upgrades = ["black_sun_raider", "black_sun_scout"]
    troops.append(t)

    t = Troop("black_sun_raider", "Black Sun Raider", 4, "Infantry", weapon_spec="axe")
    equip_bsun_shock(t)
    t.upgrades = ["black_sun_reaver"]
    troops.append(t)

    t = Troop("black_sun_scout", "Black Sun Scout", 4, "Ranged")
    equip_bsun_archer(t)
    t.upgrades = ["black_sun_archer"]
    troops.append(t)

    t = Troop("black_sun_reaver", "Black Sun Reaver", 5, "Infantry", weapon_spec="axe")
    equip_bsun_shock(t)
    t.upgrades = ["black_sun_executioner"]
    troops.append(t)

    t = Troop("black_sun_archer", "Black Sun Archer", 5, "Ranged")
    equip_bsun_archer(t)
    t.upgrades = ["black_sun_longbowman"]
    troops.append(t)

    t = Troop("black_sun_executioner", "Black Sun Executioner", 6, "Infantry", weapon_spec="axe")
    equip_bsun_shock(t)
    t.upgrades = ["black_sun_scourge"]
    troops.append(t)

    t = Troop("black_sun_longbowman", "Black Sun Longbowman", 6, "Ranged")
    equip_bsun_archer(t)
    t.upgrades = ["black_sun_marksman"]
    troops.append(t)

    t = Troop("black_sun_scourge", "Black Sun Scourge", 7, "Infantry", weapon_spec="axe")
    equip_bsun_shock(t)
    troops.append(t)

    t = Troop("black_sun_marksman", "Black Sun Marksman", 7, "Ranged")
    equip_bsun_archer(t)
    t.upgrades = ["black_sun_chosen_marksman"]
    troops.append(t)

    t = Troop("black_sun_chosen_marksman", "Black Sun Chosen Marksman", 8, "Ranged")
    equip_bsun_archer(t)
    troops.append(t)

    # =========================================================================
    # DARKHÛN MERCENARIES (T2-T8)
    # =========================================================================
    t = Troop("darkhun_recruit", "Darkh\u00fbn Recruit", 2, "Infantry", is_basic_troop=True)
    equip_darkhun_infantry(t)
    t.weapons = [KHML_AXE_1H]
    t.shield = ""
    t.upgrades = ["darkhun_footman"]
    troops.append(t)

    t = Troop("darkhun_footman", "Darkh\u00fbn Footman", 3, "Infantry")
    equip_darkhun_infantry(t)
    t.weapons = [KHML_AXE_1H]
    t.shield = ""
    t.upgrades = ["darkhun_infantry", "darkhun_horseman"]
    troops.append(t)

    t = Troop("darkhun_infantry", "Darkh\u00fbn Infantry", 4, "Infantry")
    equip_darkhun_infantry(t)
    t.upgrades = ["darkhun_veteran_infantry"]
    troops.append(t)

    t = Troop("darkhun_horseman", "Darkh\u00fbn Horseman", 4, "Cavalry")
    equip_darkhun_cavalry(t)
    t.upgrades = ["darkhun_cavalry"]
    troops.append(t)

    t = Troop("darkhun_veteran_infantry", "Darkh\u00fbn Veteran Infantry", 5, "Infantry")
    equip_darkhun_infantry(t)
    t.upgrades = ["darkhun_guard"]
    troops.append(t)

    t = Troop("darkhun_cavalry", "Darkh\u00fbn Cavalry", 5, "Cavalry")
    equip_darkhun_cavalry(t)
    t.upgrades = ["darkhun_veteran_cavalry"]
    troops.append(t)

    t = Troop("darkhun_guard", "Darkh\u00fbn Guard", 6, "Infantry")
    equip_darkhun_infantry(t)
    t.upgrades = ["darkhun_ironbound"]
    troops.append(t)

    t = Troop("darkhun_veteran_cavalry", "Darkh\u00fbn Veteran Cavalry", 6, "Cavalry")
    equip_darkhun_cavalry(t)
    t.upgrades = ["darkhun_knight"]
    troops.append(t)

    t = Troop("darkhun_ironbound", "Darkh\u00fbn Ironbound", 7, "Infantry")
    equip_darkhun_infantry(t)
    troops.append(t)

    t = Troop("darkhun_knight", "Darkh\u00fbn Knight", 7, "Cavalry")
    equip_darkhun_cavalry(t)
    t.upgrades = ["darkhun_cultist_knight"]
    troops.append(t)

    t = Troop("darkhun_cultist_knight", "Darkh\u00fbn Cultist Knight", 8, "Cavalry")
    equip_darkhun_cavalry(t)
    troops.append(t)

    # =========================================================================
    # SAGARÛN REGIONAL UNITS (T3-T7)
    # =========================================================================
    t = Troop("sagarun_deckhand", "Sagar\u00fbn Deckhand", 3, "Infantry", is_basic_troop=True)
    equip_sagarun_marine(t)
    t.upgrades = ["sagarun_watchman"]
    troops.append(t)

    t = Troop("sagarun_watchman", "Sagar\u00fbn Watchman", 4, "Infantry")
    equip_sagarun_marine(t)
    t.upgrades = ["sagarun_skirmisher", "sagarun_crossbowman"]
    troops.append(t)

    t = Troop("sagarun_skirmisher", "Sagar\u00fbn Skirmisher", 5, "Infantry")
    equip_sagarun_marine(t)
    t.upgrades = ["sagarun_marine"]
    troops.append(t)

    t = Troop("sagarun_crossbowman", "Sagar\u00fbn Crossbowman", 5, "Ranged", weapon_spec="crossbow")
    equip_sagarun_arbalest(t)
    t.upgrades = ["sagarun_naffatun", "sagarun_arbalest"]
    troops.append(t)

    t = Troop("sagarun_marine", "Sagar\u00fbn Marine", 6, "Infantry")
    equip_sagarun_marine(t)
    t.upgrades = ["sagarun_storm_forged_marine"]
    troops.append(t)

    t = Troop("sagarun_naffatun", "Sagar\u00fbn Naffatun", 6, "Ranged")
    equip_sagarun_naffatun(t)
    t.upgrades = ["sagarun_storm_helmed_naffatun"]
    troops.append(t)

    t = Troop("sagarun_arbalest", "Sagar\u00fbn Arbalest", 6, "Ranged", weapon_spec="crossbow")
    equip_sagarun_arbalest(t)
    t.upgrades = ["sagarun_storm_marked_arbalest"]
    troops.append(t)

    t = Troop("sagarun_storm_forged_marine", "Sagar\u00fbn Storm Forged Marine", 7, "Infantry")
    equip_sagarun_marine(t)
    troops.append(t)

    t = Troop("sagarun_storm_helmed_naffatun", "Sagar\u00fbn Storm Helmed Naffatun", 7, "Ranged")
    equip_sagarun_naffatun(t)
    troops.append(t)

    t = Troop("sagarun_storm_marked_arbalest", "Sagar\u00fbn Storm Marked Arbalest", 7, "Ranged", weapon_spec="crossbow")
    equip_sagarun_arbalest(t)
    troops.append(t)

    # =========================================================================
    # BALCOTH REGIONAL UNITS (T2-T6) — uses Easterling Regular armor
    # =========================================================================
    t = Troop("balcoth_volunteer", "Balcoth Volunteer", 2, "Infantry", is_basic_troop=True)
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[2]]
    t.shield = EAST_REG_SHIELDS[2]
    t.upgrades = ["balcoth_footman"]
    troops.append(t)

    t = Troop("balcoth_footman", "Balcoth Footman", 3, "Infantry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[3]]
    t.shield = EAST_REG_SHIELDS[3]
    t.upgrades = ["balcoth_axeman", "balcoth_archer"]
    troops.append(t)

    t = Troop("balcoth_axeman", "Balcoth Axeman", 4, "Infantry", weapon_spec="axe")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[4]]
    t.shield = EAST_REG_SHIELDS[4]
    t.upgrades = ["balcoth_veteran_axeman"]
    troops.append(t)

    t = Troop("balcoth_archer", "Balcoth Archer", 4, "Ranged")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[4], "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""
    t.upgrades = ["wainrider_veteran_archer", "balcoth_horse_archer"]
    troops.append(t)

    t = Troop("balcoth_veteran_axeman", "Balcoth Veteran Axeman", 5, "Infantry", weapon_spec="axe")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[5]]
    t.shield = EAST_REG_SHIELDS[5]
    troops.append(t)

    t = Troop("wainrider_veteran_archer", "Wainrider Veteran Archer", 5, "Ranged")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[5], "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""
    t.upgrades = ["wainrider_wind_arrow_sharpshooter"]
    troops.append(t)

    t = Troop("balcoth_horse_archer", "Balcoth Horse Archer", 5, "HorseArcher")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[5], "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""
    t.horse = "t3_aserai_horse"
    t.horse_harness = "lrd_horse_armour_4"
    t.upgrades = ["balcoth_farshot_horse_archer"]
    troops.append(t)

    t = Troop("wainrider_wind_arrow_sharpshooter", "Wainrider Wind-Arrow Sharpshooter", 6, "Ranged")
    equip_easterling_regular(t)
    t.tier = 6  # Override for T6 — uses T5 armor since EAST_REG only goes to 5
    t.head = EAST_REG_HEAD[5]
    t.body = EAST_REG_BODY[5]
    t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]
    t.leg = EAST_REG_LEG[5]
    t.weapons = ["steppe_war_bow", "bodkin_arrows_b", "bodkin_arrows_b", "rhun_1h_sword_b"]
    t.shield = ""
    troops.append(t)

    t = Troop("balcoth_farshot_horse_archer", "Balcoth Farshot Horse Archer", 6, "HorseArcher")
    t.head = EAST_REG_HEAD[5]
    t.body = EAST_REG_BODY[5]
    t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]
    t.leg = EAST_REG_LEG[5]
    t.weapons = ["steppe_war_bow", "bodkin_arrows_b", "bodkin_arrows_b", "rhun_1h_sword_b"]
    t.shield = ""
    t.horse = "t3_aserai_horse"
    t.horse_harness = "lrd_horse_armour_4"
    troops.append(t)

    # =========================================================================
    # FAR-RHUN REGIONAL UNITS (T3-T7) — uses Easterling Regular armor
    # =========================================================================
    t = Troop("far_rhun_levy", "Far-Rhun Levy", 3, "Infantry", is_basic_troop=True)
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[3]]
    t.shield = EAST_REG_SHIELDS[3]
    t.upgrades = ["far_rhun_footman", "far_rhun_horseman"]
    troops.append(t)

    t = Troop("far_rhun_footman", "Far-Rhun Footman", 4, "Infantry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[4]]
    t.shield = EAST_REG_SHIELDS[4]
    t.upgrades = ["far_rhun_infantry"]
    troops.append(t)

    t = Troop("far_rhun_horseman", "Far-Rhun Horseman", 4, "Cavalry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_LANCES[4], EAST_REG_SWORDS[4]]
    t.shield = "studded_adarga"
    t.horse = EAST_REG_HORSES[4]
    t.horse_harness = EAST_REG_HARNESS[4]
    t.upgrades = ["far_rhun_cavalry"]
    troops.append(t)

    t = Troop("far_rhun_infantry", "Far-Rhun Infantry", 5, "Infantry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[5]]
    t.shield = EAST_REG_SHIELDS[5]
    t.upgrades = ["far_rhun_gate_guard"]
    troops.append(t)

    t = Troop("far_rhun_cavalry", "Far-Rhun Cavalry", 5, "Cavalry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_LANCES[5], "khuzait_sword_4_t4"]
    t.shield = EAST_REG_SHIELDS[5]
    t.horse = EAST_REG_HORSES[5]
    t.horse_harness = EAST_REG_HARNESS[5]
    t.upgrades = ["far_rhun_cataphract"]
    troops.append(t)

    # Far-Rhun T6-T7 use T5 armor (max for Easterling Regular)
    t = Troop("far_rhun_gate_guard", "Far-Rhun Gate Guard", 6, "Infantry")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = [EAST_REG_SPEARS[5], EAST_REG_SWORDS[5]]
    t.shield = EAST_REG_SHIELDS[5]
    t.upgrades = ["far_rhun_iron_legionary"]
    troops.append(t)

    t = Troop("far_rhun_cataphract", "Far-Rhun Cataphract", 6, "Cavalry")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = ["khuzait_lance_2_t4", "khuzait_sword_4_t4"]
    t.shield = EAST_REG_SHIELDS[5]
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_5_c"
    t.upgrades = ["far_rhun_iron_cataphract"]
    troops.append(t)

    t = Troop("far_rhun_iron_legionary", "Far-Rhun Iron Legionary", 7, "Infantry")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = [EAST_REG_SPEARS[5], EAST_REG_SWORDS[5]]
    t.shield = EAST_REG_SHIELDS[5]
    troops.append(t)

    t = Troop("far_rhun_iron_cataphract", "Far-Rhun Iron Cataphract", 7, "Cavalry")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = ["khuzait_lance_2_t4", "khuzait_sword_4_t4"]
    t.shield = EAST_REG_SHIELDS[5]
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_5_c"
    troops.append(t)

    # =========================================================================
    # KHARAGHÛL REGIONAL UNITS (T2-T7) — uses Easterling Regular armor
    # =========================================================================
    t = Troop("kharaghul_youth", "Kharagh\u00fbl Youth", 2, "Cavalry", is_basic_troop=True)
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[2]]
    t.shield = EAST_REG_SHIELDS[2]
    t.horse = "khuzait_horse"; t.horse_harness = "light_harness"
    t.upgrades = ["kharaghul_rider"]
    troops.append(t)

    t = Troop("kharaghul_rider", "Kharagh\u00fbl Rider", 3, "Cavalry")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_SWORDS[3]]
    t.shield = EAST_REG_SHIELDS[3]
    t.horse = "khuzait_horse"; t.horse_harness = "light_harness"
    t.upgrades = ["kharaghul_raider", "kharaghul_horse_scout"]
    troops.append(t)

    t = Troop("kharaghul_raider", "Kharagh\u00fbl Raider", 4, "Cavalry")
    equip_easterling_regular(t)
    t.weapons = ["khuzait_lance_1_t3", EAST_REG_SWORDS[4]]
    t.shield = EAST_REG_SHIELDS[4]
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    t.upgrades = ["kharaghul_horse_master"]
    troops.append(t)

    t = Troop("kharaghul_horse_scout", "Kharagh\u00fbl Horse Scout", 4, "HorseArcher")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[4], "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    t.upgrades = ["kharaghul_horse_archer"]
    troops.append(t)

    t = Troop("kharaghul_horse_master", "Kharagh\u00fbl Horse Master", 5, "Cavalry")
    equip_easterling_regular(t)
    t.weapons = ["khuzait_lance_2_t4", EAST_REG_SWORDS[5]]
    t.shield = EAST_REG_SHIELDS[5]
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    t.upgrades = ["kharaghul_nokor"]
    troops.append(t)

    t = Troop("kharaghul_horse_archer", "Kharagh\u00fbl Horse Archer", 5, "HorseArcher")
    equip_easterling_regular(t)
    t.weapons = [EAST_REG_BOWS[5], "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_sword_3_t3"]
    t.shield = ""
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    t.upgrades = ["kharaghul_keshig"]
    troops.append(t)

    # T6-T7 use T5 armor
    t = Troop("kharaghul_nokor", "Kharagh\u00fbl Nokor", 6, "Cavalry")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = ["khuzait_lance_2_t4", "rhun_1h_sword_d"]
    t.shield = EAST_REG_SHIELDS[5]
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    t.upgrades = ["kharaghul_ashkur_nokor"]
    troops.append(t)

    t = Troop("kharaghul_keshig", "Kharagh\u00fbl Keshig", 6, "HorseArcher")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = ["steppe_war_bow", "bodkin_arrows_b", "bodkin_arrows_b", "khuzait_polearm_1_t4"]
    t.shield = ""
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    t.upgrades = ["kharaghul_karash_keshig"]
    troops.append(t)

    t = Troop("kharaghul_ashkur_nokor", "Kharagh\u00fbl Ashkur Nokor", 7, "Cavalry")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = ["khuzait_lance_2_t4", "rhun_1h_sword_a"]
    t.shield = EAST_REG_SHIELDS[5]
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    troops.append(t)

    t = Troop("kharaghul_karash_keshig", "Kharagh\u00fbl Karash Keshig", 7, "HorseArcher")
    t.head = EAST_REG_HEAD[5]; t.body = EAST_REG_BODY[5]; t.cape = EAST_REG_CAPE[5]
    t.gloves = EAST_REG_GLOVES[5]; t.leg = EAST_REG_LEG[5]
    t.weapons = ["steppe_war_bow", "bodkin_arrows_b", "bodkin_arrows_b", "rhun_1h_sword_a"]
    t.shield = ""
    t.horse = "t3_aserai_horse"; t.horse_harness = "lrd_horse_armour_4"
    troops.append(t)

    # =========================================================================
    # RHÛN MILITIA (T2-T3)
    # =========================================================================
    t = Troop("rhun_militia_spearman", "Militia Spearman", 2, "Infantry", weapon_spec="spear")
    t.face_template = "BodyProperty.fighter_rhun"
    equip_easterling_regular(t)
    t.weapons = ["eastern_spear_3_t3"]
    t.shield = "desert_round_shield"
    t.upgrades = ["rhun_militia_veteran_spearman"]
    troops.append(t)

    t = Troop("rhun_militia_archer", "Militia Archer", 2, "Ranged")
    t.face_template = "BodyProperty.fighter_rhun"
    equip_easterling_regular(t)
    t.weapons = ["hunting_bow", "default_arrows", "aserai_sword_3_t3"]
    t.shield = ""
    t.upgrades = ["rhun_militia_veteran_archer"]
    troops.append(t)

    t = Troop("rhun_militia_veteran_spearman", "Militia Veteran Spearman", 3, "Infantry", weapon_spec="spear")
    t.face_template = "BodyProperty.fighter_rhun"
    equip_easterling_regular(t)
    t.weapons = ["eastern_spear_4_t4", "aserai_sword_3_t3"]
    t.shield = "desert_round_shield"
    troops.append(t)

    t = Troop("rhun_militia_veteran_archer", "Militia Veteran Archer", 3, "Ranged")
    t.face_template = "BodyProperty.fighter_rhun"
    equip_easterling_regular(t)
    t.weapons = ["long_bow", "bodkin_arrows_a", "aserai_sword_3_t3"]
    t.shield = ""
    troops.append(t)

    return troops


# =============================================================================
# SECTION HEADERS
# =============================================================================
SECTION_HEADERS = [
    ("easterling_recruit", "EASTERLING REGULAR UNITS (T1-T5)"),
    ("loke_rim_initiate", "LOKE-RIM NOBLE UNITS (T3-T7)"),
    ("dragon_wrath_acolyte", "DRAGON-WRATH REGIONAL UNITS (T5-T9)"),
    ("wain_youngblood", "WAINRIDERS REGIONAL UNITS (T3-T7)"),
    ("black_sun_trainee", "BLACK SUN MERCENARIES (T2-T8)"),
    ("darkhun_recruit", "DARKH\u00dbN MERCENARIES (T2-T8)"),
    ("sagarun_deckhand", "SAGAR\u00dbN REGIONAL UNITS (T3-T7)"),
    ("balcoth_volunteer", "BALCOTH REGIONAL UNITS (T2-T6)"),
    ("far_rhun_levy", "FAR-RHUN REGIONAL UNITS (T3-T7)"),
    ("kharaghul_youth", "KHARAGH\u00dbL REGIONAL UNITS (T2-T7)"),
    ("rhun_militia_spearman", "RH\u00dbN MILITIA"),
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
        sys.stdout.buffer.write(xml.encode('utf-8'))
        sys.stdout.buffer.write(b'\n')
