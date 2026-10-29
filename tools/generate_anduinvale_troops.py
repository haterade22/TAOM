#!/usr/bin/env python3
"""Generate the Anduin Vale troop tree and its nine soldier party templates.

Writes:
  Main/_Module/ModuleData/troops/troops_anduinvale.xml           (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-ANDUINVALE-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: tools/promote_borrowed_cultures.py clones
them from Dale with the culture's townsfolk, as for the Arnor realms.

Design (approved by Mike 2026-10-28). Anduin Vale is the upper river country between the Misty
Mountains and Mirkwood: the Beornings of the Carrock and the Woodmen of the Vales, farmers and
hunters who keep the High Pass and the Old Ford. A small realm of axemen and bowmen with a short
cavalry branch for the river meadows:

  Vale Levy (basic, L6) -> Vale Axeman (L11) -> Beorning Axeman (L16) -> Beorning Axe-warrior
      -> Veteran Beorning -> Beorning Champion (L31)             axe + shield
  Beorning Axeman also -> Beorning Great-axe (L21) -> Beorning Axe-lord (L26), two-handed
  Vale Levy also -> Woodman Bowman (L16) -> Woodman Archer -> Woodman Marksman (L26)
  Beorning Youth (elite basic, L11) splits two ways:
      Riders:  Vale Rider (L16) -> Vale Horseman (L21)
      Bears:   Carrock Guard (L16) -> Bear-guard (L26) -> Bear-warden of the Carrock (L36, the
               capstone, on foot, a poleaxe)
  Militia spearman / archer (L6) -> veteran militia (L16)

The militia archer is level 6, not 11, because the Dale bow ladder has cells only at engine tiers 1,
3, 4, 5 and 6 (levels 6, 16, 21, 26 and 31): every archer sits on one of those levels.

Armour is a PLACEHOLDER from Dale's kit (sk_dale_*), until the realm has its own. Dale's ids carry
no tier keyword, so ARMOUR_MESH_TIER_LADDER cannot check them; the guard is instead a test
(tools/tests/test_realm_troops.py, BorrowedArmourAnchorTests): a Dale piece is never worn here
below the lowest level at which a Dale troop wears it, so the armour class table does not move.

Weapons are Dale's, placed by sustained DPS against the melee ladder (tools/melee_damage.py,
2026-10-28): spear_a 34, sword_c 34, spear_b 42, winged_spear_b 49, war_axe_a 51, sword_b 52,
great_axe_a 52, winged_spear_a 60, sword_a 60, war_axe_b 61, war_axe_c 69, poleaxe_a 78. Dale has
no kingdom offset in tools/melee_ladders.json, so neither has this realm. Bows are the Dale ladder's
items (tools/ranged_ladders.json: the `dale` line claims troops_anduinvale.xml), with the skill from
tools/ranged_ladder.py.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md). The
machinery is tools/realm_troops.py, shared with the Arnor realms.

Usage:
  python tools/generate_anduinvale_troops.py            # dry run: report what would change
  python tools/generate_anduinvale_troops.py --apply    # write both files
  python tools/generate_anduinvale_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import one, paired, same  # noqa: E402

CULTURE = "anduinvale"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Beorning Youth, the elite basic troop (weight 2 against the Levy's 6).
MERC_SOURCE = "anduinvale_beorning_youth"
MILITIA = {"anduinvale_militia_spearman": "anduinvale_militia_veteran_spearman",
           "anduinvale_militia_archer": "anduinvale_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[Anduin Vale]", script="generate_anduinvale_troops.py",
                 pt_marker="TAOM-ANDUINVALE-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Beorning Sellsword", armour_prefix="sk_dale_",
                 body="fighter_sturgia", ladder_line="dale")

SKILLS = rt.SKILLS
MILITIA_ELITE_BONUS = rt.MILITIA_ELITE_BONUS
Troop = functools.partial(rt.Troop, realm=REALM)
armour = functools.partial(rt.armour, REALM)
bow = functools.partial(rt.bow, REALM)

# Dale melee by sustained DPS (tools/melee_damage.py, 2026-10-28). Tier bands (no kingdom offset):
# t1 26-42, t2 32-54, t3 39-65, t4 45-75, t5 52-86, t6 58-98, t7 65-109.
SWORD_LOW = "dale_sword_c"         # 34
SWORD_MID = "dale_sword_b"         # 52
SWORD_HIGH = "dale_sword_a"        # 60
SPEAR_LOW = "dale_spear_a"         # 34
WINGED_LOW = "dale_winged_spear_b"  # 49
WINGED_HIGH = "dale_winged_spear_a"  # 60
AXE_LOW = "dale_war_axe_a"         # 51
AXE_MID = "dale_war_axe_b"         # 61
AXE_HIGH = "dale_war_axe_c"        # 69
GREAT_AXE = "dale_great_axe_a"     # 52, two-handed: never beside a shield
POLEAXE = "dale_poleaxe_a"         # 78, two-handed: never beside a shield
SHIELD_LOW = ("wm_dale_shield_a06", "wm_dale_shield_a08")
SHIELD_MID = ("wm_dale_shield_a03", "wm_dale_shield_b01")
SHIELD_HIGH = ("wm_dale_shield_a02", "wm_dale_shield_b02")
SHIELD_TOP = ("wm_dale_shield_a01", "wm_dale_shield_a02")
ARROWS = "bodkin_arrows_a"
ARROWS_B = "bodkin_arrows_b"
HORSE_LIGHT = ("sturgia_horse", "chain_horse_harness")
HORSE_HEAVY = ("charger", "chain_horse_harness")


# Dale armour, by family. `a` and `b` are the variant ("a01", "b02") each roster wears. Each family's
# lowest wearer in Dale's own troops (the level a piece must not be worn below):
#   lake_town mariner  a01 6, a02 16, a03 21, a04 26; b01 16, b02 21, b03 26, b04 31
#   infantry           a01 11, b01 16, a02 and b02 21, a03 and b03 26, a04 and b04 31
#   archer             a01 6, b01 16, a02 and b02 21, a03 and b03 26, a04 and b04 31
#   chivalry           a01 and b01 16, a02 and b02 21, a03 and b03 26, a04 31
# The lake_town and archer families have no shoulder for every variant, so a shoulder variant is passed.
def lake(a, b, sa=None, sb=None):
    return armour(*(((f"lake_town_{slot}_mariner_{a}", f"lake_town_{slot}_mariner_{b}"))
                    for slot in ("helmet", "chest", "boots", "bracers")),
                  (f"lake_town_shoulder_mariner_{sa or a}", f"lake_town_shoulder_mariner_{sb or b}"))


def family(name, helmet, chest, boots, gauntlet, shoulder):
    def build_set(a, b, sa=None, sb=None):
        return armour(*((f"{piece}_{name}_{a}", f"{piece}_{name}_{b}")
                        for piece in (helmet, chest, boots, gauntlet)),
                      (f"{shoulder}_{name}_{sa or a}", f"{shoulder}_{name}_{sb or b}"))
    return build_set


infantry = family("infrantry", "helmet", "chest", "boots", "gauntlet", "shoulder")
archer = family("archer", "helmet", "chest", "boots", "gauntlet", "shoulder")
chivalry_set = family("chivlary", "helmet", "chest", "boots", "gauntlet", "shoulder")


def chivalry(a, b):
    """Dale's mounted set: the chest is spelt 'chivalry' where the other pieces say 'chivlary'."""
    gear = chivalry_set(a, b)
    for roster, variant in zip(gear, (a, b)):
        roster["Body"] = f"sk_dale_chest_chivalry_{variant}"
    return gear


def build():
    t = []

    # ---- Levy and the axe line ----
    t.append(Troop("anduinvale_levy", "Vale Levy", 6, "Infantry", "sword",
                   paired(SWORD_LOW, SHIELD_LOW),
                   armour(None, one("lake_town_chest_mariner_a01"), one("lake_town_boots_mariner_a01"), None, None),
                   ["anduinvale_axeman", "anduinvale_woodman_bowman"], basic=True))
    t.append(Troop("anduinvale_axeman", "Vale Axeman", 11, "Infantry", "sword",
                   paired(AXE_LOW, SHIELD_LOW),
                   infantry("a01", "a01"),
                   ["anduinvale_beorning_axeman"]))
    t.append(Troop("anduinvale_beorning_axeman", "Beorning Axeman", 16, "Infantry", "sword",
                   paired(AXE_MID, SHIELD_MID),
                   infantry("a01", "b01"),
                   ["anduinvale_axe_warrior", "anduinvale_great_axe"]))
    t.append(Troop("anduinvale_axe_warrior", "Beorning Axe-warrior", 21, "Infantry", "sword",
                   paired(AXE_HIGH, SHIELD_MID, WINGED_LOW),
                   infantry("a02", "b02"),
                   ["anduinvale_veteran_beorning"]))
    t.append(Troop("anduinvale_veteran_beorning", "Veteran Beorning", 26, "Infantry", "sword",
                   paired(AXE_HIGH, SHIELD_HIGH, WINGED_HIGH),
                   infantry("a03", "b03"),
                   ["anduinvale_champion"]))
    t.append(Troop("anduinvale_champion", "Beorning Champion", 31, "Infantry", "sword",
                   paired(AXE_HIGH, SHIELD_TOP, WINGED_HIGH),
                   infantry("a04", "b04")))

    # ---- The great-axe branch: two-handed, a sidearm axe, no shield ----
    t.append(Troop("anduinvale_great_axe", "Beorning Great-axe", 21, "Infantry", "twohand",
                   same(GREAT_AXE, SWORD_MID),
                   lake("a03", "b02", "a03", "b01"),
                   ["anduinvale_axe_lord"]))
    t.append(Troop("anduinvale_axe_lord", "Beorning Axe-lord", 26, "Infantry", "twohand",
                   same(GREAT_AXE, AXE_HIGH),
                   lake("a04", "b03", "a03", "b03")))

    # ---- Woodman bowmen: bow in Item0 and arrows in Item1/Item2 in BOTH rosters, sword in Item3 ----
    t.append(Troop("anduinvale_woodman_bowman", "Woodman Bowman", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, SWORD_MID),
                   archer("a01", "b01"),
                   ["anduinvale_woodman_archer"]))
    t.append(Troop("anduinvale_woodman_archer", "Woodman Archer", 21, "Ranged", "ranged",
                   same(bow(21), ARROWS, ARROWS, SWORD_MID),
                   archer("a02", "b02", "a01", "b01"),
                   ["anduinvale_woodman_marksman"]))
    t.append(Troop("anduinvale_woodman_marksman", "Woodman Marksman", 26, "Ranged", "ranged",
                   same(bow(26), ARROWS_B, ARROWS, SWORD_HIGH),
                   archer("a03", "b03")))

    # ---- Beorning Youth, the two-way split ----
    t.append(Troop("anduinvale_beorning_youth", "Beorning Youth", 11, "Infantry", "sword",
                   paired(AXE_LOW, SHIELD_MID),
                   lake("a01", "a01"),
                   ["anduinvale_vale_rider", "anduinvale_carrock_guard"], basic=True))

    # Riders of the river meadows: a short branch, spear and shield on a Dale horse.
    t.append(Troop("anduinvale_vale_rider", "Vale Rider", 16, "Cavalry", "cavalry",
                   paired(WINGED_LOW, SHIELD_MID, SWORD_MID),
                   chivalry("a01", "b01"),
                   ["anduinvale_vale_horseman"], horse=HORSE_LIGHT))
    t.append(Troop("anduinvale_vale_horseman", "Vale Horseman", 21, "Cavalry", "cavalry",
                   paired(WINGED_HIGH, SHIELD_HIGH, AXE_MID),
                   chivalry("a02", "b02"),
                   horse=HORSE_HEAVY))

    # Bears of the Carrock: axe and shield, ending in the poleaxe of the capstone.
    t.append(Troop("anduinvale_carrock_guard", "Carrock Guard", 16, "Infantry", "sword",
                   paired(AXE_MID, SHIELD_MID, WINGED_LOW),
                   lake("a02", "b01", "a01", "b01"),
                   ["anduinvale_bear_guard"]))
    t.append(Troop("anduinvale_bear_guard", "Bear-guard", 26, "Infantry", "sword",
                   paired(AXE_HIGH, SHIELD_HIGH, WINGED_HIGH),
                   lake("a04", "b03", "a03", "b03"),
                   ["anduinvale_bear_warden"]))
    t.append(Troop("anduinvale_bear_warden", "Bear-warden of the Carrock", 36, "Infantry", "spear",
                   paired(POLEAXE, AXE_HIGH),
                   lake("a04", "b04", "a03", "b03")))

    # ---- Militia, named by the culture's four militia attributes. Level 6: the Dale bow ladder has no
    # level-11 cell, and the spearman follows the archer as in Dale's own militia. ----
    t.append(Troop("anduinvale_militia_spearman", "Militia Spearman", 6, "Infantry", "spear",
                   paired(SPEAR_LOW, SWORD_LOW, SHIELD_LOW),
                   lake("a01", "a01"),
                   ["anduinvale_militia_veteran_spearman"]))
    t.append(Troop("anduinvale_militia_archer", "Militia Archer", 6, "Ranged", "ranged",
                   same(bow(6), "default_arrows", SWORD_LOW),
                   archer("a01", "a01"),
                   ["anduinvale_militia_veteran_archer"]))
    t.append(Troop("anduinvale_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(WINGED_LOW, SWORD_MID, SHIELD_MID),
                   infantry("b01", "b01")))
    t.append(Troop("anduinvale_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, SWORD_MID),
                   archer("b01", "b01")))
    return t


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the men's realms' target in
# tools/rebalance_party_template_maxes.py (as Cardolan's); this generator owns them.
def party_templates():
    p = "anduinvale_"
    return [
        ("kingdom_hero_party_anduinvale_template", [
            (p + "levy", 3, 26), (p + "axeman", 2, 22), (p + "beorning_youth", 1, 19),
            (p + "beorning_axeman", 1, 19), (p + "woodman_bowman", 1, 19), (p + "vale_rider", 0, 15),
            (p + "axe_warrior", 0, 15), (p + "woodman_archer", 0, 13), (p + "carrock_guard", 0, 13),
            (p + "vale_horseman", 0, 10), (p + "veteran_beorning", 0, 8), (p + "woodman_marksman", 0, 8),
            (p + "great_axe", 0, 8), (p + "champion", 0, 5)]),
        ("kingdom_hero_party_mercenary_anduinvale_template", [
            (p + "axeman", 8, 16), (p + "woodman_bowman", 4, 8), (p + "beorning_axeman", 4, 8),
            (p + "woodman_archer", 2, 4), (p + "vale_rider", 2, 4)]),
        ("kingdom_hero_party_outlaw_anduinvale_template", [
            (p + "levy", 8, 16), (p + "militia_spearman", 4, 8), (p + "militia_archer", 4, 8),
            (p + "woodman_bowman", 2, 4)]),
        ("militia_anduinvale_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_anduinvale_template_level_1", [
            (p + "levy", 6, 6), (p + "axeman", 3, 3), (p + "woodman_bowman", 2, 2)]),
        ("patrol_party_anduinvale_template_level_2", [
            (p + "axeman", 5, 5), (p + "beorning_axeman", 4, 4), (p + "woodman_archer", 3, 3),
            (p + "vale_rider", 2, 2)]),
        ("patrol_party_anduinvale_template_level_3", [
            (p + "axe_warrior", 5, 5), (p + "veteran_beorning", 3, 3), (p + "woodman_marksman", 3, 3),
            (p + "vale_horseman", 2, 2), (p + "great_axe", 1, 1)]),
        ("rebels_anduinvale_template", [
            (p + "levy", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_anduinvale", [
            (p + "bear_warden", 1, 1), (p + "champion", 3, 3), (p + "bear_guard", 2, 2)]),
    ]


clamp_skills = functools.partial(rt.clamp_skills, REALM)
add_mercenary = functools.partial(rt.add_mercenary, REALM)
self_check = functools.partial(rt.self_check, party_templates)
troops_xml = functools.partial(rt.troops_xml, REALM)
templates_xml = functools.partial(rt.templates_xml, party_templates)


def main():
    return rt.run(REALM, build, party_templates)


if __name__ == "__main__":
    sys.exit(main())
