#!/usr/bin/env python3
"""Generate the Arthedain troop tree and its nine soldier party templates.

Writes:
  Main/_Module/ModuleData/troops/troops_arthedain.xml           (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-ARTHEDAIN-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: they name the culture's townsfolk
(villager_arthedain, caravan_guard_arthedain, ...), which tools/promote_borrowed_cultures.py clones
from Gondor together with the templates that use them.

Design (approved by Mike 2026-10-07; plan i-am-creating-a-iridescent-robin.md). Arthedain is the
what-if northern realm in which Arvedui's line survived. It is meant to be weak in NUMBERS (low-tier
clans, small parties, poor towns) while the Dunedain lines stay good, so the tree caps at engine
tier 6 except one tier 7 capstone:

  Levy (basic, L6) -> Spearman -> Fornost Footman -> Fornost Shieldman -> Fornost Guard
      -> Warden of Fornost (L31)                         spear + shield + sword, infantry armour
  Dunadan Youth (elite basic, L11) splits three ways:
      Rangers:  Ranger of the North -> Ranger -> Veteran Ranger -> Ranger Captain (L31)
      Citadel:  Dunadan Squire -> Guard of the Citadel -> Veteran Citadel Guard
                -> Knight of Arthedain -> King's Guard of Fornost (L36, the capstone)
      Cavalry:  Outrider (L21) -> Lancer (L26)   short on purpose: Arthedain's horse at Fornost
                came from Gondor and Lindon, not from its own stables
  Militia spearman / archer (L11) -> veteran militia (L21), referenced by the culture

Lore: the Dunedain of the North (LOTR Appendix A I iii) kept the Numenorean arms and the long
watch against Angmar; Fornost Erain was the royal seat of Arthedain until 1974 TA. The Rangers of
the North are their descendants, which is why the ranger line sits near Blackroot on the ranged
ladder rather than with the levy cultures.

Armour is the Arnor kit in LOTRLOME_items/arnor (sk_ar_art_*), placed by the id tier keyword so
the ARMOUR_MESH_TIER_LADDER gate holds: light to L6, light/medium to L16, medium to L21, heavy to
L26, heavy/elite to L31, elite at L36. Pieces with no tier keyword (half and full chainmail, boots,
gloves, capes) are free to sit anywhere.

Weapons: Arnor ships only Numenorean swords and poleaxes, so spears, shields and lances are
Gondor's, in loadouts Gondor already fields with a shield. numenorean_bastard_* is one-handed
capable (its blades are listed under OneHandedBastardSword first), so it is the shield sidearm;
numenorean_greatsword_* is two-handed only and never sits beside a shield.

Bows are the generated ladder_arthedain_bow_t<engine tier> items (tools/ranged_ladders.json line
"arthedain", stats by tools/generate_ranged_ladder_items.py). The bow id and the troops' Bow skill
come from tools/ranged_ladder.py, the same functions tools/rebalance_ranged_ladders.py writes with,
so this generator and the ladder tool never disagree.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md): the
same item class in every slot index of both, differing only in the a/b look.

Usage:
  python tools/generate_arthedain_troops.py            # dry run: report what would change
  python tools/generate_arthedain_troops.py --apply    # write both files
  python tools/generate_arthedain_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import argparse
import functools
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ranged_ladder as rl  # noqa: E402  engine_tier, skill_cell, ladder_id: the ladder's own functions

ROOT = Path(__file__).resolve().parents[1]
MD = ROOT / "Main" / "_Module" / "ModuleData"
TROOPS = MD / "troops" / "troops_arthedain.xml"
PARTY = MD / "taom_partyTemplates.xml"
PT_MARKER = "TAOM-ARTHEDAIN-PT"
CULTURE = "arthedain"
CIVILIAN_SET = "battania_troop_civilian_template_t2"  # what every Gondor troop uses
BODY = "fighter_gondor"

SKILLS = ("Athletics", "Riding", "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing")

# Skill curves by level, calibrated a step under Gondor's per-level medians (troops_gondor.xml,
# 2026-10-07) and a step over Dale's: good soldiers, few of them.
ATH = {6: 38, 11: 52, 16: 80, 21: 95, 26: 105, 31: 120, 36: 140}
PRIMARY = {6: 40, 11: 70, 16: 100, 21: 130, 26: 175, 31: 225, 36: 270}
SECONDARY = {6: 30, 11: 55, 16: 80, 21: 105, 26: 140, 31: 180, 36: 220}
LOW = {6: 10, 11: 15, 16: 20, 21: 25, 26: 30, 31: 35, 36: 40}
RIDING = {21: 120, 26: 160, 31: 205}


@functools.lru_cache(maxsize=None)
def bow_skill(level):
    """The ladder cell for this level's engine tier: what rebalance_ranged_ladders.py writes too."""
    return rl.skill_cell(CULTURE, rl.engine_tier(level), rl.load_spec())


def skills(level, role):
    """One skill block per role. Roles: spear, sword, twohand, ranged, cavalry."""
    s = dict.fromkeys(SKILLS, 0)
    s["Athletics"] = ATH[level]
    s["Throwing"] = LOW[level]
    s["Crossbow"] = LOW[level]
    if role == "spear":
        s.update(Polearm=PRIMARY[level], OneHanded=SECONDARY[level], TwoHanded=LOW[level] * 2,
                 Bow=LOW[level])
    elif role == "sword":
        s.update(OneHanded=PRIMARY[level], Polearm=SECONDARY[level], TwoHanded=SECONDARY[level] // 2,
                 Bow=LOW[level])
    elif role == "twohand":
        s.update(TwoHanded=PRIMARY[level], Polearm=SECONDARY[level], OneHanded=SECONDARY[level],
                 Bow=LOW[level])
    elif role == "ranged":
        s.update(Bow=bow_skill(level), OneHanded=SECONDARY[level], Polearm=LOW[level] * 2,
                 TwoHanded=LOW[level] * 2)
    elif role == "cavalry":
        s.update(Riding=RIDING[level], Polearm=PRIMARY[level], OneHanded=SECONDARY[level],
                 TwoHanded=LOW[level] * 2, Bow=LOW[level], Athletics=ATH[level] - 10)
    else:
        raise ValueError(role)
    return s


def a(slot_id):
    return f"sk_ar_art_{slot_id}"


def armour(head, body, leg, gloves, cape):
    """Two looks of one armour set. Each argument is a (roster_a, roster_b) pair of piece ids
    without the sk_ar_art_ prefix, or None to leave the slot empty in BOTH rosters."""
    out = ({}, {})
    for slot, pair in (("Head", head), ("Body", body), ("Leg", leg), ("Gloves", gloves), ("Cape", cape)):
        if pair is None:
            continue
        out[0][slot] = a(pair[0])
        out[1][slot] = a(pair[1])
    return out


def ab(stem):
    """The usual a/b pair of one piece: ab('chest_inf_med') -> ('chest_inf_med_a', 'chest_inf_med_b')."""
    return (f"{stem}_a", f"{stem}_b")


def one(piece):
    """A piece with a single variant, worn in both rosters."""
    return (piece, piece)


@dataclass
class Troop:
    id: str
    name: str
    level: int
    group: str
    role: str
    weapons: tuple          # (roster_a weapons, roster_b weapons): each a list of Item0.. ids
    gear: tuple             # (roster_a armour dict, roster_b armour dict)
    upgrades: list = field(default_factory=list)
    basic: bool = False
    horse: tuple = None     # (horse, harness), the same in both rosters
    sk: dict = None         # set by clamp_skills(); the curve value before that
    occupation: str = "Soldier"

    def xml(self, nl):
        sk = self.sk or skills(self.level, self.role)
        lines = [
            "  <NPCCharacter",
            f'      id="{self.id}"',
            f'      default_group="{self.group}"',
            f'      level="{self.level}"',
            f'      name="{{=aom_{self.id}_name}}[Arthedain] {self.name}"',
            f'      occupation="{self.occupation}"',
            f'      culture="Culture.{CULTURE}"' + ("" if self.basic else ">"),
        ]
        if self.basic:
            lines.append('      is_basic_troop="true">')
        lines += ["    <face>", f'      <face_key_template value="BodyProperty.{BODY}" />', "    </face>",
                  "    <skills>"]
        lines += [f'      <skill id="{k}" value="{sk[k]}" />' for k in SKILLS]
        lines.append("    </skills>")
        if self.upgrades:
            lines.append("    <upgrade_targets>")
            lines += [f'      <upgrade_target id="NPCCharacter.{u}" />' for u in self.upgrades]
            lines.append("    </upgrade_targets>")
        else:
            lines.append("    <upgrade_targets></upgrade_targets>")
        lines.append("    <Equipments>")
        for weapons, gear in zip(self.weapons, self.gear):
            lines.append("      <EquipmentRoster>")
            for i, w in enumerate(weapons):
                lines.append(f'        <equipment slot="Item{i}" id="Item.{w}" />')
            for slot in ("Head", "Body", "Cape", "Gloves", "Leg"):
                if slot in gear:
                    lines.append(f'        <equipment slot="{slot}" id="Item.{gear[slot]}" />')
            if self.horse:
                lines.append(f'        <equipment slot="Horse" id="Item.{self.horse[0]}" />')
                lines.append(f'        <equipment slot="HorseHarness" id="Item.{self.horse[1]}" />')
            lines.append("      </EquipmentRoster>")
        lines.append(f'      <EquipmentSet id="{CIVILIAN_SET}" equipmentType="Civilian" />')
        lines += ["    </Equipments>", "  </NPCCharacter>"]
        return nl.join(lines)


def same(*weapons):
    """The same weapon list in both rosters."""
    return (list(weapons), list(weapons))


def paired(*pairs):
    """Per-slot (roster_a, roster_b) weapon picks: paired(('x_a', 'x_b'), 'shield')."""
    ra, rb = [], []
    for p in pairs:
        if isinstance(p, tuple):
            ra.append(p[0])
            rb.append(p[1])
        else:
            ra.append(p)
            rb.append(p)
    return (ra, rb)


def bow(level):
    return rl.ladder_id(CULTURE, "Bow", rl.engine_tier(level))


def external_floors(troop_ids):
    """Skills of the NPCs outside this file that upgrade INTO the tree (the cloned
    villager_arthedain -> arthedain_levy edge), so the clamp can hold the target above them."""
    npcs = MD / "characters" / f"npcs_{CULTURE}.xml"
    floors = {}
    if not npcs.exists():
        return floors
    text = npcs.read_bytes().decode("utf-8-sig")
    for block in re.findall(r"<NPCCharacter\b.*?</NPCCharacter>", text, re.DOTALL):
        targets = [t for t in re.findall(r'upgrade_target id="NPCCharacter\.([^"]+)"', block) if t in troop_ids]
        if not targets:
            continue
        sk = {k: int(v) for k, v in re.findall(r'<skill id="(\w+)" value="(\d+)"', block)}
        for t in targets:
            floors.setdefault(t, dict.fromkeys(SKILLS, 0))
            for k in SKILLS:
                floors[t][k] = max(floors[t][k], sk.get(k, 0))
    return floors


def clamp_skills(troops):
    """An upgrade never lowers a skill (UPGRADE_SKILL_REGRESSION). Every edge raises its level,
    so one pass in level order carries each source's skills into its targets."""
    by_id = {t.id: t for t in troops}
    for t in troops:
        t.sk = skills(t.level, t.role)
    for tid, floor in external_floors(set(by_id)).items():
        for k in SKILLS:
            by_id[tid].sk[k] = max(by_id[tid].sk[k], floor[k])
    militia_step(by_id)
    for t in sorted(troops, key=lambda x: x.level):
        for u in t.upgrades:
            for k in SKILLS:
                by_id[u].sk[k] = max(by_id[u].sk[k], t.sk[k])
    return troops


# The culture's four militia slots follow the TAOM-wide militia rule (tools/rebalance_troops.py,
# tools/tests/test_militia_elite_bonus.py): every militia slot takes the level-21 baseline whatever
# its level, and the elite slot is the basic one plus MILITIA_ELITE_BONUS on every skill. The one
# exception is a laddered archer's Bow, which is its own ladder cell (#617).
MILITIA = {"arthedain_militia_spearman": "arthedain_militia_veteran_spearman",
           "arthedain_militia_archer": "arthedain_militia_veteran_archer"}
MILITIA_BASELINE_LEVEL = 21
MILITIA_ELITE_BONUS = 15  # rebalance_troops.MILITIA_ELITE_BONUS


def militia_step(by_id):
    for basic_id, vet_id in MILITIA.items():
        basic, vet = by_id[basic_id], by_id[vet_id]
        basic.sk = skills(MILITIA_BASELINE_LEVEL, basic.role)
        vet.sk = {k: v + MILITIA_ELITE_BONUS for k, v in basic.sk.items()}
        if basic.role == "ranged":
            basic.sk["Bow"] = bow_skill(basic.level)
            vet.sk["Bow"] = bow_skill(vet.level)


# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests):
# occupation Mercenary, so the hire dialogue fires, and no upgrade targets, so the engine's
# upgrade walk cannot drift onto a Soldier line troop. The pool's rarest entry is the Dunadan Youth
# (VolunteerRecruitmentService.Arthedain.cs, weight 2 against the Levy's 6).
MERC_SOURCE = "arthedain_dunadan_youth"


def add_mercenary(troops):
    src = next(t for t in troops if t.id == MERC_SOURCE)
    merc = Troop(f"{src.id}_merc", "Dunadan Sellsword", src.level, src.group, src.role,
                 src.weapons, src.gear, occupation="Mercenary")
    merc.sk = dict(src.sk)
    return troops + [merc]


# Weapons are placed by sustained DPS against the melee ladder band of each engine tier
# (tools/melee_ladders.json, `arthedain` offset +2%), measured with the gate's own pricing
# (melee_catalogue.price_all, 2026-10-07): spear_b_cardolan 31, gondor_sword_a01 35, a07 44, a03 54,
# numenorean_bastard_heavy 52-53, poleaxe_medium 62, greatsword_heavy 70-71, bastard_elite 71,
# greatsword_medium 79-80, poleaxe_heavy 80, greatsword_elite 93-99. The names do not order:
# numenorean_bastard_medium sustains 105, above every elite blade, so no troop carries it.
SPEAR = "wm_gondor_spear_b_cardolan"
LANCE = "wm_swan_knight_lance_a"
SHIELD_LOW = ("sm_gd_shield_a1", "sm_gd_shield_a3")
SHIELD_MID = ("sm_gd_shield_a3", "sm_gd_shield_a4")
SHIELD_HIGH = ("sm_gd_shield_b1", "sm_gd_shield_b2")
ARROWS = "bodkin_arrows_a"
HORSE_LIGHT = ("t2_empire_horse", "gondor_horse_armor_4")
HORSE_HEAVY = ("noble_horse_imperial", "gondor_horse_armor_3")


def build():
    t = []

    # ---- Levy and the Fornost infantry line (spear + shield + sword) ----
    t.append(Troop("arthedain_levy", "Levy", 6, "Infantry", "spear",
                   paired(SPEAR, SHIELD_LOW),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("boots_a"), None, ab("cape")),
                   ["arthedain_spearman"], basic=True))
    t.append(Troop("arthedain_spearman", "Spearman", 11, "Infantry", "spear",
                   paired(SPEAR, SHIELD_LOW, "wm_gondor_sword_a01"),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("grvs_inf_light_a"),
                          one("gloves_a"), ab("pauld_inf_light")),
                   ["arthedain_footman"]))
    t.append(Troop("arthedain_footman", "Fornost Footman", 16, "Infantry", "spear",
                   paired(SPEAR, SHIELD_MID, "wm_gondor_sword_a07"),
                   armour(ab("helmet_inf_med"), ab("chainmail_full"), one("grvs_inf_med_a"),
                          one("bracer_inf_med_a"), ab("pauld_inf_light")),
                   ["arthedain_shieldman"]))
    t.append(Troop("arthedain_shieldman", "Fornost Shieldman", 21, "Infantry", "spear",
                   paired(SPEAR, SHIELD_MID, ("numenorean_bastard_heavy_a", "numenorean_bastard_heavy_b")),
                   armour(ab("helmet_inf_med"), ab("chest_inf_med"), one("grvs_inf_med_a"),
                          one("bracer_inf_med_a"), ab("pauld_inf_med")),
                   ["arthedain_fornost_guard"]))
    t.append(Troop("arthedain_fornost_guard", "Fornost Guard", 26, "Infantry", "spear",
                   paired(SPEAR, SHIELD_HIGH, ("numenorean_bastard_elite_a", "numenorean_bastard_elite_b")),
                   armour(ab("helmet_warden_heavy"), ab("chest_inf_heavy"), one("grvs_inf_heavy_a"),
                          one("bracer_noble_heavy_a"), one("pauld_inf_heavy_a")),
                   ["arthedain_warden"]))
    t.append(Troop("arthedain_warden", "Warden of Fornost", 31, "Infantry", "sword",
                   paired(("numenorean_bastard_elite_a", "numenorean_bastard_elite_b"), SHIELD_HIGH, SPEAR),
                   armour(ab("helmet_warden_elite"), ab("chest_inf_heavy"), one("grvs_inf_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("pauld_cape_inf_elite"))))

    # ---- Dunadan Youth, the three-way split ----
    t.append(Troop("arthedain_dunadan_youth", "Dunadan Youth", 11, "Infantry", "sword",
                   paired(("numenorean_bastard_heavy_a", "numenorean_bastard_heavy_b"), SHIELD_LOW),
                   armour(one("helmet_noble_light_a"), ab("chainmail_half"), one("grvs_noble_light_a"),
                          one("gloves_a"), ab("cape")),
                   ["arthedain_ranger_of_the_north", "arthedain_squire", "arthedain_outrider"], basic=True))

    # Rangers: bow in Item0 and arrows in Item1/Item2 in BOTH rosters, sword in Item3.
    t.append(Troop("arthedain_ranger_of_the_north", "Ranger of the North", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "numenorean_bastard_heavy_a"),
                   armour(one("helmet_noble_light_a"), ab("chainmail_half"), one("grvs_noble_light_a"),
                          one("gloves_a"), ab("cape")),
                   ["arthedain_ranger"]))
    t.append(Troop("arthedain_ranger", "Ranger", 21, "Ranged", "ranged",
                   same(bow(21), ARROWS, ARROWS, "numenorean_bastard_heavy_b"),
                   armour(ab("helmet_noble_med"), ab("chainmail_full"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ab("cape")),
                   ["arthedain_veteran_ranger"]))
    t.append(Troop("arthedain_veteran_ranger", "Veteran Ranger", 26, "Ranged", "ranged",
                   same(bow(26), ARROWS, ARROWS, "numenorean_bastard_elite_a"),
                   armour(ab("helmet_noble_heavy"), ab("chainmail_full"), one("grvs_noble_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("cape")),
                   ["arthedain_ranger_captain"]))
    t.append(Troop("arthedain_ranger_captain", "Ranger Captain", 31, "Ranged", "ranged",
                   same(bow(31), ARROWS, ARROWS, "numenorean_bastard_elite_b"),
                   armour(ab("helmet_noble_heavy"), ab("chest_noble_med"), one("grvs_noble_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("cape"))))

    # Citadel: two-handed, never a shield. Greatsword in Item0, poleaxe in Item1 from T4.
    t.append(Troop("arthedain_squire", "Dunadan Squire", 16, "Infantry", "twohand",
                   same("numenorean_poleaxe_medium"),
                   armour(ab("helmet_noble_med"), ab("chainmail_full"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ab("pauld_noble_med")),
                   ["arthedain_citadel_guard"]))
    t.append(Troop("arthedain_citadel_guard", "Guard of the Citadel", 21, "Infantry", "twohand",
                   paired(("numenorean_greatsword_heavy_a", "numenorean_greatsword_heavy_b"),
                          "numenorean_poleaxe_medium"),
                   armour(ab("helmet_noble_med"), ab("chest_noble_med"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ("pauld_noble_med_a", "pauld_noble_med_c")),
                   ["arthedain_veteran_citadel_guard"]))
    t.append(Troop("arthedain_veteran_citadel_guard", "Veteran Citadel Guard", 26, "Infantry", "twohand",
                   paired(("numenorean_greatsword_medium_a", "numenorean_greatsword_medium_b"),
                          "numenorean_poleaxe_heavy"),
                   armour(ab("helmet_guard_heavy"), ab("chest_noble_heavy"), one("grvs_noble_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("pauld_noble_heavy")),
                   ["arthedain_knight"]))
    # The knight trades the two-hander for sword and shield; the capstone takes it back up.
    t.append(Troop("arthedain_knight", "Knight of Arthedain", 31, "Infantry", "sword",
                   paired(("numenorean_bastard_elite_a", "numenorean_bastard_elite_b"), SHIELD_HIGH),
                   armour(ab("helmet_noble_elite"), ab("chest_noble_heavy"), one("grvs_noble_elite_a"),
                          one("bracer_noble_elite_a"), ab("pauld_noble_elite")),
                   ["arthedain_kings_guard"]))
    t.append(Troop("arthedain_kings_guard", "King's Guard of Fornost", 36, "Infantry", "twohand",
                   paired(("numenorean_greatsword_elite_a", "numenorean_greatsword_elite_b"),
                          "numenorean_poleaxe_heavy"),
                   armour(ab("helmet_guard_elite"), ab("chest_noble_heavy"), one("grvs_noble_elite_a"),
                          one("bracer_noble_elite_a"), ab("pauld_cape_noble_elite"))))

    # Cavalry: short by design.
    t.append(Troop("arthedain_outrider", "Outrider", 21, "Cavalry", "cavalry",
                   paired(SPEAR, SHIELD_MID, ("numenorean_bastard_heavy_a", "numenorean_bastard_heavy_b")),
                   armour(ab("helmet_noble_med"), ab("chest_noble_med"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ab("cape")),
                   ["arthedain_lancer"], horse=HORSE_LIGHT))
    t.append(Troop("arthedain_lancer", "Lancer", 26, "Cavalry", "cavalry",
                   paired(LANCE, SHIELD_HIGH, ("numenorean_bastard_elite_a", "numenorean_bastard_elite_b")),
                   armour(ab("helmet_cav_heavy"), ab("chest_noble_heavy"), one("grvs_noble_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("pauld_noble_heavy")),
                   horse=HORSE_HEAVY))

    # ---- Militia, named by the culture's four militia attributes ----
    t.append(Troop("arthedain_militia_spearman", "Militia Spearman", 11, "Infantry", "spear",
                   paired(SPEAR, SHIELD_LOW, "wm_gondor_sword_a01"),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("boots_a"), None, ab("cape")),
                   ["arthedain_militia_veteran_spearman"]))
    t.append(Troop("arthedain_militia_archer", "Militia Archer", 11, "Ranged", "ranged",
                   same(bow(11), ARROWS, ARROWS, "wm_gondor_sword_a01"),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("boots_a"), None, ab("cape")),
                   ["arthedain_militia_veteran_archer"]))
    # Veterans at level 16 (tier 3), as every culture's are: a weak realm's militia, not a stronger one.
    t.append(Troop("arthedain_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(SPEAR, SHIELD_MID, "wm_gondor_sword_a03"),
                   armour(ab("helmet_inf_med"), ab("chainmail_full"), one("grvs_inf_med_a"),
                          one("gloves_a"), ab("pauld_inf_med"))))
    t.append(Troop("arthedain_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "wm_gondor_sword_a03"),
                   armour(ab("helmet_inf_med"), ab("chainmail_full"), one("grvs_inf_med_a"),
                          one("gloves_a"), ab("cape"))))
    return t


# (id, [(troop, min, max), ...]). Small on purpose: Arthedain is the realm its neighbours carve up.
# max_value is a spawn ceiling, not a party size (docs/reference/party-template-sizing.md). This
# generator owns these numbers: tools/rebalance_party_template_maxes.py has no arthedain target, and
# giving it one would set two writers on the TAOM-ARTHEDAIN-PT region.
def party_templates():
    p = "arthedain_"
    return [
        ("kingdom_hero_party_arthedain_template", [
            (p + "levy", 3, 12), (p + "spearman", 2, 10), (p + "dunadan_youth", 1, 8),
            (p + "footman", 1, 8), (p + "ranger_of_the_north", 1, 8), (p + "squire", 0, 5),
            (p + "shieldman", 0, 6), (p + "ranger", 0, 6), (p + "outrider", 0, 5),
            (p + "citadel_guard", 0, 4), (p + "fornost_guard", 0, 3), (p + "veteran_ranger", 0, 3),
            (p + "lancer", 0, 3), (p + "veteran_citadel_guard", 0, 2)]),
        ("kingdom_hero_party_mercenary_arthedain_template", [
            (p + "spearman", 8, 16), (p + "ranger_of_the_north", 4, 8), (p + "shieldman", 4, 8),
            (p + "ranger", 2, 4), (p + "outrider", 2, 4)]),
        ("kingdom_hero_party_outlaw_arthedain_template", [
            (p + "levy", 8, 16), (p + "militia_spearman", 4, 8), (p + "militia_archer", 4, 8),
            (p + "ranger_of_the_north", 2, 4)]),
        ("militia_arthedain_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_arthedain_template_level_1", [
            (p + "levy", 6, 6), (p + "spearman", 3, 3), (p + "ranger_of_the_north", 2, 2)]),
        ("patrol_party_arthedain_template_level_2", [
            (p + "spearman", 5, 5), (p + "footman", 4, 4), (p + "ranger", 3, 3), (p + "outrider", 2, 2)]),
        ("patrol_party_arthedain_template_level_3", [
            (p + "shieldman", 5, 5), (p + "fornost_guard", 3, 3), (p + "veteran_ranger", 3, 3),
            (p + "lancer", 2, 2), (p + "knight", 1, 1)]),
        ("rebels_arthedain_template", [
            (p + "levy", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_arthedain", [
            (p + "kings_guard", 1, 1), (p + "knight", 3, 3), (p + "ranger_captain", 2, 2)]),
    ]


def templates_xml(nl):
    out = []
    for tid, stacks in party_templates():
        out.append(f'\t<MBPartyTemplate id="{tid}">')
        out.append("\t\t<stacks>")
        for troop, lo, hi in stacks:
            out.append(f'\t\t\t<PartyTemplateStack min_value="{lo}" max_value="{hi}" troop="NPCCharacter.{troop}" />')
        out.append("\t\t</stacks>")
        out.append("\t</MBPartyTemplate>")
        out.append("")
    return nl.join(out)


def troops_xml(troops, nl):
    head = ['<?xml version="1.0" encoding="utf-8"?>',
            "<!-- GENERATED by tools/generate_arthedain_troops.py. Edit the script, not this file. -->",
            "<NPCCharacters>"]
    body = (nl + nl).join(tr.xml(nl) for tr in troops)
    return nl.join(head) + nl + body + nl + "</NPCCharacters>" + nl


def self_check(troops):
    """Structural invariants the validator would otherwise report after the fact."""
    ids = [tr.id for tr in troops]
    errors = []
    if len(ids) != len(set(ids)):
        errors.append("duplicate troop id")
    known = set(ids)
    by_id = {tr.id: tr for tr in troops}
    for tr in troops:
        for u in tr.upgrades:
            if u not in known:
                errors.append(f"{tr.id}: upgrade target {u} is not defined")
            elif rl.engine_tier(by_id[u].level) <= rl.engine_tier(tr.level):
                errors.append(f"{tr.id} -> {u}: target tier does not rise (UPGRADE_TIER_COLLAPSE)")
        ra, rb = tr.weapons
        if len(ra) != len(rb):
            errors.append(f"{tr.id}: the two rosters fill different weapon slots")
        if set(tr.gear[0]) != set(tr.gear[1]):
            errors.append(f"{tr.id}: the two rosters fill different armour slots")
        if "Body" not in tr.gear[0]:
            errors.append(f"{tr.id}: no Body armour (MISSING_BODY_ARMOUR)")
        shields = any("shield" in w for w in ra + rb)
        twohanders = any("greatsword" in w or "poleaxe" in w for w in ra + rb)
        if shields and twohanders:
            errors.append(f"{tr.id}: a shield and a two-handed weapon in the same troop")
    for tid, stacks in party_templates():
        for troop, lo, hi in stacks:
            if troop not in known:
                errors.append(f"{tid}: names undefined troop {troop}")
            if lo > hi:
                errors.append(f"{tid}: {troop} min {lo} > max {hi}")
    return errors


def read_bytes_faithful(path):
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw[3:].decode("utf-8") if bom else raw.decode("utf-8")
    nl = "\r\n" if "\r\n" in text else "\n"
    return text, nl, bom


def upsert_templates(text, nl, payload):
    """Replace this script's region in place, or insert it before </partyTemplates> the first time.
    In place, not strip-and-append: promote_borrowed_cultures.py appends its own region at the same
    spot, and strip-and-append would swap the two regions on every alternate run."""
    block = f"\t<!-- {PT_MARKER}:BEGIN -->{nl}{payload}\t<!-- {PT_MARKER}:END -->{nl}"
    region = re.compile(r"[ \t]*<!-- " + PT_MARKER + r":BEGIN -->.*?<!-- " + PT_MARKER + r":END -->\r?\n",
                        re.DOTALL)
    if region.search(text):
        return region.sub(lambda _m: block, text, count=1)
    idx = text.rfind("</partyTemplates>")
    if idx < 0:
        raise SystemExit("taom_partyTemplates.xml: </partyTemplates> not found")
    return text[:idx] + block + text[idx:]


def main():
    ap = argparse.ArgumentParser()
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--check", action="store_true")
    args = ap.parse_args()

    troops = add_mercenary(clamp_skills(build()))
    errors = self_check(troops)
    if errors:
        print("REFUSING: the design breaks an invariant:\n  " + "\n  ".join(errors))
        return 1

    nl = "\r\n"  # troops_*.xml ship CRLF without a BOM
    new_troops = troops_xml(troops, nl)
    pt_text, pt_nl, pt_bom = read_bytes_faithful(PARTY)
    new_party = upsert_templates(pt_text, pt_nl, templates_xml(pt_nl))

    # Parse both results before anything is written.
    ET.fromstring(new_troops.encode("utf-8"))
    ET.fromstring(new_party.encode("utf-8"))

    old_troops = TROOPS.read_bytes().decode("utf-8") if TROOPS.exists() else None
    troops_changed = old_troops != new_troops
    party_changed = pt_text != new_party

    print(f"{len(troops)} troops, {len(party_templates())} party templates")
    print(f"  troops_arthedain.xml: {'NEW' if old_troops is None else ('changed' if troops_changed else 'unchanged')}")
    print(f"  taom_partyTemplates.xml: {'changed' if party_changed else 'unchanged'}")

    if args.check:
        return 1 if (troops_changed or party_changed) else 0
    if not args.apply:
        print("DRY RUN: re-run with --apply to write")
        return 0
    if troops_changed:
        TROOPS.write_bytes(new_troops.encode("utf-8"))
    if party_changed:
        PARTY.write_bytes((b"\xef\xbb\xbf" if pt_bom else b"") + new_party.encode("utf-8"))
    print("written")
    return 0


if __name__ == "__main__":
    sys.exit(main())
