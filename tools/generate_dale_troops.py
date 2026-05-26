#!/usr/bin/env python3
"""Generate Main/_Module/ModuleData/troops/troops_dale.xml from a concrete
troop manifest.

Tree design (lore-grounded — see docs/features/dale.md for sources):
- Dale uses vanilla Culture.sturgia (renamed "Barding" via spcultures.xslt).
- Lake-Town levy line (T2-T6): Lake-Town Peasant → Lake-Town Militia branches
  into two parallel infantry lines:
    Watch line — vanilla pikes (2H) + 1H sword sidearm, no shield, anti-
      cavalry role: Watchman → Veteran Watchman → Officer of the Watch.
    Pikeman line — 2H halberds/polearms, shock infantry: Patrolman → Pikeman
      → Veteran Pikeman.
  Both Lake-Town lines wear mariner-class armor (light-medium, Esgaroth theme).
- Royal line (T3-T7) — Dale Levy (basic_troop) branches into four (Dale caps
  at T7; no T8 troops):
    Excellent Archers — Yeoman → Bowman → Marksman of Dale → Barding
      Marksman (T7 terminal). +10-15 Bow over standard tier baseline.
    Great Infantry — Dale Militia → Dalian Guardsman → Dalian Swordsman →
      Dalian Master Swordsman (T7 terminal).
    Riverman / Shipmen / Dalian Mariner — spear + shield + 1H sword, Lake-
      Town armor; royal-tier water-folk line (T4-T6).
    Decent Cavalry (T4-T7 capped — Dale isn't horse-country per Tolkien):
      Merchant Guard → Northman Scout → Dalian Cavalry → Dalian Heavy
      Cavalry. Skill curve ~70% of Rohan tier-matched parity.
- The Pikeman line extends to T7 via the Lake-Town Hearthguard (off Veteran
  Pikeman) — Lake-Town's royal-tier shock infantry.
- Plus 4 militia troops referenced by spcultures.xslt for garrison spawns.

Equipment uses:
- Dale armor items authored by tools/generate_dale_armor.py (sk_dale_*)
- vanilla Sturgia weapons (sturgia_*, northern_spear_*, sturgia_polearm/2haxe)
- vanilla pikes (fine_pike_t4, vlandia_pike_1_t5) for the Watch line
- shared LOTRAOM weapons/shields/horses where appropriate
- "lowland_longbow" / "lowland_yew_bow" / "noble_bow" represent Bard's
  great-bow tradition

Usage:
    python tools/generate_dale_troops.py --dry-run
    python tools/generate_dale_troops.py --apply
"""
import argparse
import os
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

OUTPUT_DEFAULT = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "Main", "_Module", "ModuleData", "troops", "troops_dale.xml"
)

# Tier -> level (mirrors Erebor/Rohan baselines)
TIER_LEVEL = {2: 6, 3: 12, 4: 19, 5: 25, 6: 32, 7: 39, 8: 46}


@dataclass
class Skills:
    """8 skills tracked by Bannerlord NPCCharacters."""
    Athletics: int = 0
    Riding: int = 0
    OneHanded: int = 0
    TwoHanded: int = 0
    Polearm: int = 0
    Bow: int = 0
    Crossbow: int = 0
    Throwing: int = 0

    def to_xml(self) -> str:
        lines = []
        for name in ("Athletics", "Riding", "OneHanded", "TwoHanded",
                     "Polearm", "Bow", "Crossbow", "Throwing"):
            v = getattr(self, name)
            lines.append(f'            <skill id="{name}" value="{v}" />')
        return "\n".join(lines)


@dataclass
class EquipmentRoster:
    """One equipment variant (the engine picks one per agent spawn)."""
    items: dict[str, str]  # slot -> item_id (e.g. "Head" -> "sk_dale_helmet_archer_a01")

    def to_xml(self) -> str:
        lines = ["            <EquipmentRoster>"]
        for slot, item_id in self.items.items():
            lines.append(f'                <equipment slot="{slot}" id="Item.{item_id}" />')
        lines.append("            </EquipmentRoster>")
        return "\n".join(lines)


@dataclass
class Troop:
    id: str                           # dale_xxx
    display_name: str                 # "Dale Bowman"
    tier: int                         # 2..8
    default_group: str                # Infantry / Ranged / Cavalry / HorseArcher
    skills: Skills
    rosters: list[EquipmentRoster]
    upgrades: list[str] = field(default_factory=list)  # target troop IDs
    is_basic_troop: bool = False      # T2 root + T3 squire
    occupation: str = "Soldier"
    body_property: str = "fighter_sturgia"  # vanilla Sturgia body type

    def to_xml(self) -> str:
        upgrade_lines = []
        if self.upgrades:
            for u in self.upgrades:
                upgrade_lines.append(f'            <upgrade_target id="NPCCharacter.{u}" />')
            upgrades_block = "\n".join(upgrade_lines)
            upgrade_section = (
                "        <upgrade_targets>\n"
                f"{upgrades_block}\n"
                "        </upgrade_targets>"
            )
        else:
            upgrade_section = "        <upgrade_targets></upgrade_targets>"

        rosters_block = "\n".join(r.to_xml() for r in self.rosters)
        basic_attr = '\n        is_basic_troop="true"' if self.is_basic_troop else ""
        return f'''    <NPCCharacter
        id="{self.id}"
        default_group="{self.default_group}"
        level="{TIER_LEVEL[self.tier]}"
        name="{{=aom_{self.id}_name}}[Dale] {self.display_name}"
        occupation="{self.occupation}"{basic_attr}
        culture="Culture.sturgia">
        <face>
            <face_key_template value="BodyProperty.{self.body_property}" />
        </face>
        <skills>
{self.skills.to_xml()}
        </skills>
{upgrade_section}
        <Equipments>
{rosters_block}
        </Equipments>
    </NPCCharacter>'''


# =============================================================================
# SKILL CURVES (per tier, per role)
# =============================================================================
# Baseline skill curve mirroring TAOM convention (~25 per tier on primary,
# +10-15 over baseline for "excellent archer" Bow values).
def s_recruit() -> Skills:
    return Skills(Athletics=45, OneHanded=30, Polearm=35, Bow=25, Throwing=20)

def s_militia() -> Skills:  # T3 militia / squire root
    return Skills(Athletics=60, OneHanded=50, Polearm=55, Bow=45, Throwing=30)


# ---------- Lake-Town Watch line (vanilla pikes + 1H sword sidearm, no shield) ----------
# Polearm-primary (pikes). OneHanded as sidearm. Some retained Throwing skill
# from the original javelin-skirmisher curve — small enough to ignore.
def s_lake_skirmisher_t4() -> Skills:  # now: Lake-Town Watchman
    return Skills(Athletics=75, OneHanded=70, Polearm=95, Throwing=25)

def s_lake_mariner_t5() -> Skills:  # now: Lake-Town Veteran Watchman
    return Skills(Athletics=95, OneHanded=100, Polearm=125, Throwing=30)

def s_lake_veteran_t6() -> Skills:  # now: Lake-Town Officer of the Watch
    return Skills(Athletics=115, OneHanded=130, Polearm=160, Throwing=35)

# ---------- Lake-Town Pikeman line (2H halberds/polearms, no shield) ----------
# Polearm-primary (2H polearm). OneHanded sidearm. Mild TwoHanded for overhead
# polearm swings.
def s_footman_t4() -> Skills:  # now: Lake-Town Patrolman
    return Skills(Athletics=80, OneHanded=70, TwoHanded=30, Polearm=100, Bow=20)

def s_spearman_t5() -> Skills:  # now: Lake-Town Pikeman
    return Skills(Athletics=100, OneHanded=85, TwoHanded=50, Polearm=135, Bow=25)

def s_veteran_spearman_t6() -> Skills:  # now: Lake-Town Veteran Pikeman
    return Skills(Athletics=120, OneHanded=100, TwoHanded=70, Polearm=170, Bow=30)

# ---------- Riverman / Shipmen / Dalian Mariner line (royal spear+shield+sword) ----------
# Royal-tier T4-T6 off Dale Levy. Spear+shield+sword balanced kit.
def s_riverman_t4() -> Skills:
    return Skills(Athletics=80, OneHanded=80, Polearm=95, Bow=20)

def s_shipman_t5() -> Skills:
    return Skills(Athletics=100, OneHanded=100, Polearm=125, Bow=25)

def s_dalian_mariner_t6() -> Skills:
    return Skills(Athletics=120, OneHanded=125, Polearm=160, Bow=30)


# ---------- Royal Archer branch ("Excellent Archers" — +10-15 Bow over baseline) ----------
def s_bowman_t4() -> Skills:
    return Skills(Athletics=75, OneHanded=55, Polearm=40, Bow=90, Throwing=20)

def s_longbowman_t5() -> Skills:
    return Skills(Athletics=95, OneHanded=70, Polearm=50, Bow=125, Throwing=25)

def s_royal_archer_t6() -> Skills:
    return Skills(Athletics=115, OneHanded=85, Polearm=60, Bow=160, Throwing=30)

def s_black_arrow_t7() -> Skills:
    return Skills(Athletics=135, OneHanded=100, Polearm=70, Bow=195, Throwing=35)


# ---------- Royal Infantry branch ("Great Infantry") ----------
def s_man_at_arms_t4() -> Skills:
    return Skills(Athletics=80, OneHanded=95, Polearm=85, Bow=20)

def s_guardsman_t5() -> Skills:
    return Skills(Athletics=100, OneHanded=130, Polearm=115, Bow=25)

def s_royal_guard_t6() -> Skills:
    return Skills(Athletics=120, OneHanded=160, Polearm=145, Bow=30)

def s_river_warden_t7() -> Skills:
    return Skills(Athletics=140, OneHanded=190, Polearm=175, TwoHanded=130, Bow=35)

# ---------- Lake-Town Hearthguard (T7 terminal off the Pikeman line) ----------
# 2H polearm shock infantry — the Lake-Town royal-tier counterpart to the Watch
# line's anti-cavalry pikemen. Polearm-primary, mild TwoHanded for overhead swing.
def s_hearthguard_t7() -> Skills:
    return Skills(Athletics=145, OneHanded=150, TwoHanded=150, Polearm=220, Bow=35)


# ---------- Royal Cavalry branch ("Decent Cavalry" — capped at T7, ~30% under Rohan parity) ----------
# Per Codex review #227: original numbers were 40-45% under Rohan, which crossed "decent" into
# "weak". Bumped Riding/Polearm by ~35-45% so Dale cavalry now lands at roughly 70% of Rohan
# tier-matched parity — clearly inferior to Rohirrim horse-lords (lore-correct per Tolkien's
# Éothéod-vs-Bardings split) but a useable third branch alongside Excellent Archers and
# Great Infantry.
def s_outrider_t4() -> Skills:
    return Skills(Athletics=65, Riding=115, OneHanded=80, Polearm=110, Bow=40)

def s_knight_t5() -> Skills:
    return Skills(Athletics=80, Riding=150, OneHanded=115, Polearm=155, Bow=50)

def s_royal_cavalier_t6() -> Skills:
    return Skills(Athletics=95, Riding=185, OneHanded=145, Polearm=195, Bow=60)

def s_kinsman_eorl_t7() -> Skills:
    return Skills(Athletics=110, Riding=220, OneHanded=180, Polearm=235, Bow=70)


# ---------- Militia (XSLT references) ----------
def s_militia_spear_t2() -> Skills:
    return Skills(Athletics=50, OneHanded=40, Polearm=70, Bow=15)

def s_militia_archer_t2() -> Skills:
    return Skills(Athletics=50, OneHanded=30, Bow=80, Throwing=15)

def s_militia_vet_spear_t4() -> Skills:
    return Skills(Athletics=85, OneHanded=85, Polearm=120, Bow=20)

def s_militia_vet_archer_t4() -> Skills:
    return Skills(Athletics=80, OneHanded=60, Bow=125, Throwing=25)


# =============================================================================
# EQUIPMENT BUILDERS
# =============================================================================
# Dale armor tier mapping: a01->T2/T3 light, a02->T2/T3 light (alt), a03/a04->T4/T5 medium,
# b01/b02->T6/T7 heavy, b03/b04->T8 elite.
# We pick TWO variants per troop (a + b suffix variants) so each EquipmentRoster
# can offer a different look. The engine picks one at agent spawn.
def archer_armor(tier: int, variant: str) -> dict[str, str]:
    # variant in {"a", "b"} controls which sub-variant of the armor we use
    # Tier->base suffix:
    #   T2: a01 / a02     T3: a01 / a02
    #   T4: a03 / a04     T5: a03 / a04
    #   T6: b01 / b02     T7: b01 / b02
    #   T8: b03 / b04
    suffix = _armor_suffix(tier, variant)
    return {
        "Head": f"sk_dale_helmet_archer_{suffix}",
        "Body": f"sk_dale_chest_archer_{suffix}",
        "Gloves": f"sk_dale_gauntlet_archer_{suffix}",
        "Leg": f"sk_dale_boots_archer_{suffix}",
        # Shoulder: archer line is missing a02/b02 variants — fall back to a01/b01
        "Cape": f"sk_dale_shoulder_archer_{_shoulder_suffix_archer(tier, variant)}",
    }


def infantry_armor(tier: int, variant: str) -> dict[str, str]:
    suffix = _armor_suffix(tier, variant)
    return {
        "Head": f"sk_dale_helmet_infrantry_{suffix}",
        "Body": f"sk_dale_chest_infrantry_{suffix}",
        "Gloves": f"sk_dale_gauntlet_infrantry_{suffix}",
        "Leg": f"sk_dale_boots_infrantry_{suffix}",
        "Cape": f"sk_dale_shoulder_infrantry_{suffix}",
    }


def cavalry_armor(tier: int, variant: str) -> dict[str, str]:
    suffix = _armor_suffix(tier, variant)
    # Cavalry uses "chivlary" class (Solus's typo preserved verbatim)
    # chest mesh uses "chivalry" spelling (Solus's typo only on the chest slot)
    chest_suffix = _armor_suffix(tier, variant)
    return {
        "Head": f"sk_dale_helmet_chivlary_{suffix}",
        "Body": f"sk_dale_chest_chivalry_{chest_suffix}",
        "Gloves": f"sk_dale_gauntlet_chivlary_{suffix}",
        "Leg": f"sk_dale_boots_chivlary_{suffix}",
        "Cape": f"sk_dale_shoulder_chivlary_{suffix}",
    }


def lake_town_armor(tier: int, variant: str) -> dict[str, str]:
    suffix = _armor_suffix(tier, variant)
    # Shoulder: mariner line only has a01, a03, b01, b03 — pick closest
    return {
        "Head": f"sk_dale_lake_town_helmet_mariner_{suffix}",
        "Body": f"sk_dale_lake_town_chest_mariner_{suffix}",
        "Gloves": f"sk_dale_lake_town_bracers_mariner_{suffix}",
        "Leg": f"sk_dale_lake_town_boots_mariner_{suffix}",
        "Cape": f"sk_dale_lake_town_shoulder_mariner_{_shoulder_suffix_mariner(tier, variant)}",
    }


def lake_town_armor_explicit(suffix: str, *, no_helmet: bool = False, no_shoulder: bool = False) -> dict[str, str]:
    """Build a Lake-Town armor set keyed by an EXPLICIT mariner-suffix (a01..b04),
    not the tier-derived implicit suffix. Used for the Lake-Town Watch / Pikeman /
    levy lines per user spec — each tier gets a specific suffix:
        Peasant a01 (no helmet/shoulder) → Militia a01 → Watchman a02 →
        Veteran Watchman a03 → Officer of the Watch a04;
        Patrolman b01 → Pikeman b02 → Veteran Pikeman b03 → Hearthguard b04.

    Solus's mariner shoulder mesh exists only at a01/a03/b01/b03; suffixes
    a02/a04/b02/b04 fall back to the next-lower available shoulder.
    """
    armor: dict[str, str] = {}
    if not no_helmet:
        armor["Head"] = f"sk_dale_lake_town_helmet_mariner_{suffix}"
    armor["Body"] = f"sk_dale_lake_town_chest_mariner_{suffix}"
    armor["Gloves"] = f"sk_dale_lake_town_bracers_mariner_{suffix}"
    armor["Leg"] = f"sk_dale_lake_town_boots_mariner_{suffix}"
    if not no_shoulder:
        shoulder_fallback = {
            "a01": "a01", "a02": "a01", "a03": "a03", "a04": "a03",
            "b01": "b01", "b02": "b01", "b03": "b03", "b04": "b03",
        }
        armor["Cape"] = f"sk_dale_lake_town_shoulder_mariner_{shoulder_fallback[suffix]}"
    return armor


def _armor_suffix(tier: int, variant: str) -> str:
    """Map (tier, variant) -> armor suffix (a01/a02/a03/a04/b01/b02/b03/b04)."""
    table = {
        (2, "a"): "a01", (2, "b"): "a02",
        (3, "a"): "a01", (3, "b"): "a02",
        (4, "a"): "a03", (4, "b"): "a04",
        (5, "a"): "a03", (5, "b"): "a04",
        (6, "a"): "b01", (6, "b"): "b02",
        (7, "a"): "b01", (7, "b"): "b02",
        (8, "a"): "b03", (8, "b"): "b04",
    }
    return table[(tier, variant)]


def _shoulder_suffix_archer(tier: int, variant: str) -> str:
    # Archer shoulders exist for: a01, a03, a04, b01, b03, b04 (missing a02, b02)
    suffix = _armor_suffix(tier, variant)
    if suffix == "a02":
        return "a01"
    if suffix == "b02":
        return "b01"
    return suffix


def _shoulder_suffix_mariner(tier: int, variant: str) -> str:
    # Mariner shoulders exist for: a01, a03, b01, b03 (missing a02, a04, b02, b04)
    suffix = _armor_suffix(tier, variant)
    fallback = {"a01": "a01", "a02": "a01", "a03": "a03", "a04": "a03",
                "b01": "b01", "b02": "b01", "b03": "b03", "b04": "b03"}
    return fallback[suffix]


# =============================================================================
# TROOP DEFINITIONS — 26 NPCCharacters
# =============================================================================
def build_troops() -> list[Troop]:
    troops: list[Troop] = []

    # ----- Lake-Town Levy Root -----
    # Per user spec: explicit per-tier mariner armor suffixes.
    # Peasant: a01 chest/bracers/boots only (no helmet, no shoulder).
    # Militia: a01 across all 5 slots.
    troops.append(Troop(
        id="dale_recruit",
        display_name="Lake-Town Peasant",
        tier=2, default_group="Infantry",
        is_basic_troop=True,
        skills=s_recruit(),
        upgrades=["dale_militia"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_sword_1_t2",
                "Item1": "sturgia_old_shield_a",
                **lake_town_armor_explicit("a01", no_helmet=True, no_shoulder=True),
            }),
            EquipmentRoster({
                "Item0": "northern_spear_1_t2",
                "Item1": "sturgia_old_shield_b",
                **lake_town_armor_explicit("a01", no_helmet=True, no_shoulder=True),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_militia",
        display_name="Lake-Town Militia",
        tier=3, default_group="Infantry",
        skills=s_militia(),
        upgrades=["dale_lake_town_skirmisher", "dale_footman"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_sword_2_t3",
                "Item1": "sturgia_old_shield_c",
                **lake_town_armor_explicit("a01"),
            }),
            EquipmentRoster({
                "Item0": "northern_spear_2_t3",
                "Item1": "sturgia_infantry_shield_a",
                **lake_town_armor_explicit("a01"),
            }),
        ],
    ))

    # ----- Lake-Town Watch line (T4-T6) — vanilla pikes + 1H sword sidearm, no shield -----
    # Armor: Watchman a02, Veteran Watchman a03, Officer of the Watch a04 (full 5-slot mariner).
    troops.append(Troop(
        id="dale_lake_town_skirmisher",
        display_name="Lake-Town Watchman",
        tier=4, default_group="Infantry",
        skills=s_lake_skirmisher_t4(),
        upgrades=["dale_lake_town_mariner"],
        rosters=[
            EquipmentRoster({
                "Item0": "fine_pike_t4",
                "Item1": "sturgia_sword_4_t4",
                **lake_town_armor_explicit("a02"),
            }),
            EquipmentRoster({
                "Item0": "military_fork_pike_t3",
                "Item1": "sturgia_sword_5_t4",
                **lake_town_armor_explicit("a02"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_lake_town_mariner",
        display_name="Lake-Town Veteran Watchman",
        tier=5, default_group="Infantry",
        skills=s_lake_mariner_t5(),
        upgrades=["dale_lake_town_veteran"],
        rosters=[
            EquipmentRoster({
                "Item0": "vlandia_pike_1_t5",
                "Item1": "sturgia_sword_5_t5",
                **lake_town_armor_explicit("a03"),
            }),
            EquipmentRoster({
                "Item0": "thamaskene_pike_t4",
                "Item1": "sturgia_noble_sword_1_t5",
                **lake_town_armor_explicit("a03"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_lake_town_veteran",
        display_name="Lake-Town Officer of the Watch",
        tier=6, default_group="Infantry",
        skills=s_lake_veteran_t6(),
        upgrades=[],  # terminal in Watch line
        rosters=[
            EquipmentRoster({
                "Item0": "vlandia_pike_1_t5",
                "Item1": "sturgia_noble_sword_2_t5",
                **lake_town_armor_explicit("a04"),
            }),
            EquipmentRoster({
                "Item0": "vlandia_pike_1_t5",
                "Item1": "sturgia_noble_sword_3_t5",
                **lake_town_armor_explicit("a04"),
            }),
        ],
    ))

    # ----- Lake-Town Pikeman line (T4-T7) — 2H halberds/polearms, no shield -----
    # Armor: Patrolman b01, Pikeman b02, Veteran Pikeman b03, Hearthguard b04.
    troops.append(Troop(
        id="dale_footman",
        display_name="Lake-Town Patrolman",
        tier=4, default_group="Infantry",
        skills=s_footman_t4(),
        upgrades=["dale_spearman"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_2haxe_1_t4",
                "Item1": "sturgia_sword_4_t4",
                **lake_town_armor_explicit("b01"),
            }),
            EquipmentRoster({
                "Item0": "billhook_polearm_t2",
                "Item1": "sturgia_sword_5_t4",
                **lake_town_armor_explicit("b01"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_spearman",
        display_name="Lake-Town Pikeman",
        tier=5, default_group="Infantry",
        skills=s_spearman_t5(),
        upgrades=["dale_veteran_spearman"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_polearm_1_t5",
                "Item1": "sturgia_sword_5_t5",
                **lake_town_armor_explicit("b02"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_2haxe_2_t5",
                "Item1": "sturgia_noble_sword_1_t5",
                **lake_town_armor_explicit("b02"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_veteran_spearman",
        display_name="Lake-Town Veteran Pikeman",
        tier=6, default_group="Infantry",
        skills=s_veteran_spearman_t6(),
        upgrades=["dale_lake_town_hearthguard"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_polearm_1_t5",
                "Item1": "sturgia_noble_sword_2_t5",
                **lake_town_armor_explicit("b03"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_2haxe_2_t5",
                "Item1": "sturgia_noble_sword_3_t5",
                **lake_town_armor_explicit("b03"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_lake_town_hearthguard",
        display_name="Lake-Town Hearthguard",
        tier=7, default_group="Infantry",
        skills=s_hearthguard_t7(),
        upgrades=[],  # T7 terminal (Dale caps at T7)
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_polearm_1_t5",
                "Item1": "sturgia_noble_sword_3_t5",
                **lake_town_armor_explicit("b04"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_2haxe_2_t5",
                "Item1": "sturgia_noble_sword_4_t5",
                **lake_town_armor_explicit("b04"),
            }),
        ],
    ))

    # ----- Royal root (T3, branches to archer/infantry/cavalry/riverman) -----
    troops.append(Troop(
        id="dale_squire",
        display_name="Dale Levy",
        tier=3, default_group="Infantry",
        is_basic_troop=True,
        skills=s_militia(),
        upgrades=["dale_riverman", "dale_man_at_arms", "dale_bowman", "dale_outrider"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_sword_2_t3",
                "Item1": "sturgia_infantry_shield_a",
                **infantry_armor(3, "a"),
            }),
            EquipmentRoster({
                "Item0": "northern_spear_2_t3",
                "Item1": "sturgia_infantry_shield_b",
                **infantry_armor(3, "b"),
            }),
        ],
    ))

    # ----- Royal Archer branch (T4-T8) -----
    troops.append(Troop(
        id="dale_bowman",
        display_name="Yeoman",
        tier=4, default_group="Ranged",
        skills=s_bowman_t4(),
        upgrades=["dale_longbowman"],
        rosters=[
            EquipmentRoster({
                "Item0": "hunting_bow",
                "Item1": "default_arrows",
                "Item2": "sturgia_sword_3_t3",
                **archer_armor(4, "a"),
            }),
            EquipmentRoster({
                "Item0": "mountain_hunting_bow",
                "Item1": "default_arrows",
                "Item2": "sturgia_sword_4_t4",
                **archer_armor(4, "b"),
            }),
        ],
    ))

    # Bow progression (vanilla v1.4.5 stats, ascending power):
    #   hunting_bow < mountain_hunting_bow < lowland_longbow < lowland_yew_bow < noble_bow
    # Codex review caught the inversion of lowland_longbow vs lowland_yew_bow
    # (yew is the stronger of the two). T5 keeps the longbow, T6 graduates to yew.
    troops.append(Troop(
        id="dale_longbowman",
        display_name="Bowman",
        tier=5, default_group="Ranged",
        skills=s_longbowman_t5(),
        upgrades=["dale_royal_archer"],
        rosters=[
            EquipmentRoster({
                "Item0": "lowland_longbow",
                "Item1": "bodkin_arrows_a",
                "Item2": "sturgia_sword_5_t4",
                **archer_armor(5, "a"),
            }),
            EquipmentRoster({
                "Item0": "lowland_longbow",
                "Item1": "bodkin_arrows_b",
                "Item2": "sturgia_sword_5_t5",
                **archer_armor(5, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_royal_archer",
        display_name="Marksman of Dale",
        tier=6, default_group="Ranged",
        skills=s_royal_archer_t6(),
        upgrades=["dale_black_arrow_marksman"],
        rosters=[
            EquipmentRoster({
                "Item0": "lowland_yew_bow",
                "Item1": "bodkin_arrows_b",
                "Item2": "sturgia_noble_sword_1_t5",
                **archer_armor(6, "a"),
            }),
            EquipmentRoster({
                "Item0": "lowland_yew_bow",
                "Item1": "bodkin_arrows_c",
                "Item2": "sturgia_noble_sword_2_t5",
                **archer_armor(6, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_black_arrow_marksman",
        display_name="Barding Marksman",
        tier=7, default_group="Ranged",
        skills=s_black_arrow_t7(),
        upgrades=[],  # T7 terminal (Dale caps at T7)
        rosters=[
            EquipmentRoster({
                "Item0": "noble_bow",
                "Item1": "barbed_arrows",
                "Item2": "sturgia_noble_sword_3_t5",
                **archer_armor(7, "a"),
            }),
            EquipmentRoster({
                "Item0": "noble_bow",
                "Item1": "barbed_arrows",
                "Item2": "sturgia_noble_sword_4_t5",
                **archer_armor(7, "b"),
            }),
        ],
    ))

    # ----- Royal Infantry branch (T4-T8, "Great Infantry") -----
    troops.append(Troop(
        id="dale_man_at_arms",
        display_name="Dale Militia",
        tier=4, default_group="Infantry",
        skills=s_man_at_arms_t4(),
        upgrades=["dale_guardsman"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_sword_4_t4",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "northern_spear_3_t4",
                **infantry_armor(4, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_sword_5_t4",
                "Item1": "sturgia_infantry_shield_b",
                "Item2": "sturgia_axe_4_t4",
                **infantry_armor(4, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_guardsman",
        display_name="Dalian Guardsman",
        tier=5, default_group="Infantry",
        skills=s_guardsman_t5(),
        upgrades=["dale_royal_guard"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_sword_5_t5",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "northern_spear_4_t5",
                **infantry_armor(5, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_axe_5_t5",
                "Item1": "sturgia_infantry_shield_b",
                "Item2": "sturgia_polearm_1_t5",
                **infantry_armor(5, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_royal_guard",
        display_name="Dalian Swordsman",
        tier=6, default_group="Infantry",
        skills=s_royal_guard_t6(),
        upgrades=["dale_running_river_warden"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_noble_sword_1_t5",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "sturgia_polearm_1_t5",
                **infantry_armor(6, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_noble_sword_2_t5",
                "Item1": "sturgia_infantry_shield_b",
                "Item2": "sturgia_2haxe_1_t4",
                **infantry_armor(6, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_running_river_warden",
        display_name="Dalian Master Swordsman",
        tier=7, default_group="Infantry",
        skills=s_river_warden_t7(),
        upgrades=[],  # T7 terminal (Dale caps at T7)
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_noble_sword_3_t5",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "sturgia_polearm_1_t5",
                **infantry_armor(7, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_2haxe_2_t5",
                "Item2": "sturgia_noble_sword_4_t5",
                **infantry_armor(7, "b"),
            }),
        ],
    ))

    # ----- Royal Cavalry branch (T4-T7, "Decent Cavalry") -----
    troops.append(Troop(
        id="dale_outrider",
        display_name="Merchant Guard",
        tier=4, default_group="Cavalry",
        skills=s_outrider_t4(),
        upgrades=["dale_knight"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_sword_4_t4",
                "Item1": "horsemans_heater_shield",
                "Item2": "northern_spear_3_t4",
                "Horse": "sturgia_horse",
                "HorseHarness": "chain_horse_harness",
                **cavalry_armor(4, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_sword_5_t4",
                "Item1": "horsemans_heater_shield",
                "Item2": "northern_spear_4_t4",
                "Horse": "sturgia_horse",
                "HorseHarness": "chain_horse_harness",
                **cavalry_armor(4, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_knight",
        display_name="Northman Scout",
        tier=5, default_group="Cavalry",
        skills=s_knight_t5(),
        upgrades=["dale_royal_cavalier"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_lance_1_t4",
                "Item1": "horsemans_heater_shield",
                "Item2": "sturgia_sword_5_t5",
                "Horse": "charger",
                "HorseHarness": "chain_horse_harness",
                **cavalry_armor(5, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_lance_2_t5",
                "Item1": "heavy_horsemans_kite_shield",
                "Item2": "sturgia_noble_sword_1_t5",
                "Horse": "charger",
                "HorseHarness": "chain_horse_harness",
                **cavalry_armor(5, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_royal_cavalier",
        display_name="Dalian Cavalry",
        tier=6, default_group="Cavalry",
        skills=s_royal_cavalier_t6(),
        upgrades=["dale_kinsman_of_eorl"],
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_lance_2_t5",
                "Item1": "heavy_horsemans_kite_shield",
                "Item2": "sturgia_noble_sword_2_t5",
                "Horse": "charger",
                "HorseHarness": "chain_horse_harness",
                **cavalry_armor(6, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_polearm_1_t5",
                "Item1": "heavy_horsemans_kite_shield",
                "Item2": "sturgia_noble_sword_3_t5",
                "Horse": "charger",
                "HorseHarness": "chain_horse_harness",
                **cavalry_armor(6, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_kinsman_of_eorl",
        display_name="Dalian Heavy Cavalry",
        tier=7, default_group="Cavalry",
        skills=s_kinsman_eorl_t7(),
        upgrades=[],  # cavalry capped at T7 per lore — Dale isn't horse-country
        rosters=[
            EquipmentRoster({
                "Item0": "sturgia_lance_2_t5",
                "Item1": "heavy_horsemans_kite_shield",
                "Item2": "sturgia_noble_sword_3_t5",
                "Horse": "charger",
                "HorseHarness": "rohan_horse_armor_scalemail",
                **cavalry_armor(7, "a"),
            }),
            EquipmentRoster({
                "Item0": "sturgia_polearm_1_t5",
                "Item1": "heavy_horsemans_kite_shield",
                "Item2": "sturgia_noble_sword_4_t5",
                "Horse": "charger",
                "HorseHarness": "rohan_horse_armor_scalemail",
                **cavalry_armor(7, "b"),
            }),
        ],
    ))

    # ----- Riverman line (T4-T6, off Dale Levy) — spear + shield + sword, Lake-Town armor -----
    troops.append(Troop(
        id="dale_riverman",
        display_name="Riverman",
        tier=4, default_group="Infantry",
        skills=s_riverman_t4(),
        upgrades=["dale_shipman"],
        rosters=[
            EquipmentRoster({
                "Item0": "northern_spear_3_t4",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "sturgia_sword_4_t4",
                **lake_town_armor(4, "a"),
            }),
            EquipmentRoster({
                "Item0": "northern_spear_4_t4",
                "Item1": "sturgia_infantry_shield_b",
                "Item2": "sturgia_sword_5_t4",
                **lake_town_armor(4, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_shipman",
        display_name="Shipmen",
        tier=5, default_group="Infantry",
        skills=s_shipman_t5(),
        upgrades=["dale_dalian_mariner"],
        rosters=[
            EquipmentRoster({
                "Item0": "northern_spear_4_t5",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "sturgia_sword_5_t5",
                **lake_town_armor(5, "a"),
            }),
            EquipmentRoster({
                "Item0": "eastern_spear_5_t5",
                "Item1": "sturgia_infantry_shield_b",
                "Item2": "sturgia_noble_sword_1_t5",
                **lake_town_armor(5, "b"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_dalian_mariner",
        display_name="Dalian Mariner",
        tier=6, default_group="Infantry",
        skills=s_dalian_mariner_t6(),
        upgrades=[],  # terminal in Riverman line
        rosters=[
            EquipmentRoster({
                "Item0": "northern_spear_4_t5",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "sturgia_noble_sword_2_t5",
                **lake_town_armor(6, "a"),
            }),
            EquipmentRoster({
                "Item0": "eastern_spear_5_t5",
                "Item1": "sturgia_infantry_shield_b",
                "Item2": "sturgia_noble_sword_3_t5",
                **lake_town_armor(6, "b"),
            }),
        ],
    ))

    # ----- Militia (XSLT references) -----
    troops.append(Troop(
        id="dale_militia_spearman",
        display_name="Militia Spearman",
        tier=2, default_group="Infantry",
        skills=s_militia_spear_t2(),
        upgrades=["dale_militia_veteran_spearman"],
        rosters=[
            EquipmentRoster({
                "Item0": "northern_spear_1_t2",
                "Item1": "sturgia_old_shield_a",
                **lake_town_armor(2, "a"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_militia_archer",
        display_name="Militia Archer",
        tier=2, default_group="Ranged",
        skills=s_militia_archer_t2(),
        upgrades=["dale_militia_veteran_archer"],
        rosters=[
            EquipmentRoster({
                "Item0": "hunting_bow",
                "Item1": "default_arrows",
                "Item2": "sturgia_sword_1_t2",
                **archer_armor(2, "a"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_militia_veteran_spearman",
        display_name="Veteran Militia Spearman",
        tier=4, default_group="Infantry",
        skills=s_militia_vet_spear_t4(),
        upgrades=[],
        rosters=[
            EquipmentRoster({
                "Item0": "northern_spear_3_t4",
                "Item1": "sturgia_infantry_shield_a",
                "Item2": "sturgia_sword_4_t4",
                **infantry_armor(4, "a"),
            }),
        ],
    ))

    troops.append(Troop(
        id="dale_militia_veteran_archer",
        display_name="Veteran Militia Archer",
        tier=4, default_group="Ranged",
        skills=s_militia_vet_archer_t4(),
        upgrades=[],
        rosters=[
            EquipmentRoster({
                "Item0": "lowland_yew_bow",
                "Item1": "bodkin_arrows_a",
                "Item2": "sturgia_sword_4_t4",
                **archer_armor(4, "a"),
            }),
        ],
    ))

    return troops


# =============================================================================
# DRIVER
# =============================================================================
def build_xml(troops: list[Troop]) -> str:
    blocks = [t.to_xml() for t in troops]
    body = "\n\n".join(blocks)
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '\n'
        '<NPCCharacters>\n'
        '    <!-- Dale troop tree — auto-generated by tools/generate_dale_troops.py -->\n'
        '    <!-- Three branches: Excellent Archers, Great Infantry, Decent Cavalry. -->\n'
        '    <!-- Plus Lake-town smallfolk line + 4 militia troops referenced by XSLT. -->\n'
        '\n'
        + body
        + '\n\n</NPCCharacters>\n'
    )


def dry_run(troops: list[Troop]):
    print(f"\n=== Dale Troop Tree — {len(troops)} troops ===\n")
    by_branch: dict[str, list[Troop]] = {
        "Levy": [], "Lake-town": [], "Royal Archer": [],
        "Royal Infantry": [], "Royal Cavalry": [], "Militia": [],
    }
    for t in troops:
        if "lake_town" in t.id:
            by_branch["Lake-town"].append(t)
        elif t.id.startswith("dale_militia"):
            by_branch["Militia"].append(t)
        elif t.id in ("dale_recruit", "dale_militia", "dale_squire", "dale_footman",
                      "dale_spearman", "dale_veteran_spearman"):
            if t.id in ("dale_footman", "dale_spearman", "dale_veteran_spearman"):
                by_branch["Royal Infantry"].append(t)  # actually levy-spear branch but goes there
            else:
                by_branch["Levy"].append(t)
        elif t.default_group == "Ranged":
            by_branch["Royal Archer"].append(t)
        elif t.default_group == "Cavalry":
            by_branch["Royal Cavalry"].append(t)
        else:
            by_branch["Royal Infantry"].append(t)

    for branch, items in by_branch.items():
        if not items:
            continue
        print(f"--- {branch} ---")
        for t in sorted(items, key=lambda x: (x.tier, x.id)):
            up = " -> " + ",".join(t.upgrades) if t.upgrades else " [terminal]"
            print(f"  T{t.tier} L{TIER_LEVEL[t.tier]:>2} [{t.default_group:10s}] {t.id:35s}{up}")
        print()
    # Check upgrade refs resolve
    ids = {t.id for t in troops}
    for t in troops:
        for u in t.upgrades:
            if u not in ids:
                print(f"  ERROR: {t.id} upgrades to unknown troop {u}")


def apply(output_path: str, troops: list[Troop]):
    p = Path(output_path).resolve()
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(build_xml(troops), encoding="utf-8")
    print(f"Wrote {len(troops)} troops -> {p}")


def main():
    parser = argparse.ArgumentParser(description="Generate troops_dale.xml")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--output", default=OUTPUT_DEFAULT)
    args = parser.parse_args()

    if not args.dry_run and not args.apply:
        parser.print_help()
        sys.exit(2)

    troops = build_troops()
    if args.dry_run:
        dry_run(troops)
    else:
        apply(args.output, troops)


if __name__ == "__main__":
    main()
