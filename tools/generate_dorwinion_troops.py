#!/usr/bin/env python3
"""Generate the Dorwinion troop tree and its nine soldier party templates (#1062).

Writes:
  Main/_Module/ModuleData/troops/troops_dorwinion.xml            (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-DORWINION-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: tools/promote_borrowed_cultures.py clones
them from Dale with the culture's townsfolk, as for Anduin Vale and Angaladh.

Design (Mike, 2026-10-29). Dorwinion is the vintners' country on the Sea of Rhun: Northmen who press
the wine of the Redwater vineyards and trade it up the River Running to Dale and Erebor. A middling,
neutral realm of spearmen and bowmen with a short river-rider cavalry branch and a household guard:

  Vineyard Levy (basic, L6) -> Vineyard Spearman (L11) -> River Spearman (L16) -> Redwater Guard
      -> Veteran Redwater Guard -> Warden of the Vines (L31)         spear + shield, a poleaxe at L31
  Vineyard Levy also -> Vintner Bowman (L16) -> Vintner Archer -> Redwater Archer
      -> Master of the Vine-bow (L31)
  Dorwinion Noble (elite basic, L11) splits two ways:
      Riders:  River Rider (L16) -> River Cavalier (L21)
      Guard:   Vineholm Retainer (L16) -> Sergeant of Vineholm (L26) -> Guard of Vineholm (L36, the
               capstone, on foot, a poleaxe)
  Militia spearman / archer (L6) -> veteran militia (L16)

The militia archer is level 6, not 11, because the Dale bow ladder has cells only at engine tiers 1,
3, 4, 5 and 6 (levels 6, 16, 21, 26 and 31): every archer sits on one of those levels.

Armour is a PLACEHOLDER from Dale's kit (sk_dale_*), until the realm has its own. Dale's ids carry
no tier keyword, so ARMOUR_MESH_TIER_LADDER cannot check them; the guard is instead a test
(tools/tests/test_realm_troops.py, DaleRealmTests): a Dale piece is never worn here below the lowest
level at which a Dale troop wears it, so the armour class table does not move.

Weapons are Dale's, placed by sustained DPS against the melee ladder (tools/melee_damage.py,
2026-10-28): spear_a 34, sword_c 34, spear_b 42, winged_spear_b 49, sword_b 52, great_axe_a 52,
winged_spear_a 60, sword_a 60, poleaxe_a 78. Dale has no kingdom offset in tools/melee_ladders.json,
so neither has this realm. Bows are the Dale ladder's items (tools/ranged_ladders.json: the `dale`
line claims troops_dorwinion.xml), with the skill from tools/ranged_ladder.py.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md). The
machinery is tools/realm_troops.py, shared with the Arnor realms.

Usage:
  python tools/generate_dorwinion_troops.py            # dry run: report what would change
  python tools/generate_dorwinion_troops.py --apply    # write both files
  python tools/generate_dorwinion_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import one, paired, same  # noqa: E402

CULTURE = "dorwinion"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Dorwinion Noble, the elite basic troop (weight 2 against the Levy's 6).
MERC_SOURCE = "dorwinion_noble"
MILITIA = {"dorwinion_militia_spearman": "dorwinion_militia_veteran_spearman",
           "dorwinion_militia_archer": "dorwinion_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[Dorwinion]", script="generate_dorwinion_troops.py",
                 pt_marker="TAOM-DORWINION-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Dorwinion Sellsword", armour_prefix="sk_dale_",
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
SPEAR_MID = "dale_spear_b"         # 42
WINGED_LOW = "dale_winged_spear_b"  # 49
WINGED_HIGH = "dale_winged_spear_a"  # 60
POLEAXE = "dale_poleaxe_a"         # 78, two-handed: never beside a shield
SHIELD_LOW = ("wm_dale_shield_a06", "wm_dale_shield_a08")
SHIELD_MID = ("wm_dale_shield_a03", "wm_dale_shield_b01")
SHIELD_HIGH = ("wm_dale_shield_a02", "wm_dale_shield_b02")
ARROWS = "bodkin_arrows_a"
ARROWS_B = "bodkin_arrows_b"
HORSE_LIGHT = ("sturgia_horse", "chain_horse_harness")
HORSE_HEAVY = ("charger", "chain_horse_harness")


# Dale armour, by family. `a` and `b` are the variant ("a01", "b02") each roster wears. Each family's
# lowest wearer in Dale's own troops (the level a piece must not be worn below):
#   lake_town mariner  a01 6, a02 16, a03 21, a04 26; b01 16, b02 21, b03 26, b04 31
#   infantry           a01 11, b01 16, a02 and b02 21, a03 and b03 26, a04 and b04 31
#   archer             a01 6, b01 16, a02 and b02 21, a03 and b03 26, a04 and b04 31
#   chivalry           a01 and b01 16, a02 and b02 21, a03 and b03 26, a04 31 (b04 26)
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

    # ---- Levy and the spear line ----
    t.append(Troop("dorwinion_levy", "Vineyard Levy", 6, "Infantry", "spear",
                   paired(SPEAR_LOW, SWORD_LOW, SHIELD_LOW),
                   armour(None, one("lake_town_chest_mariner_a01"), one("lake_town_boots_mariner_a01"), None, None),
                   ["dorwinion_spearman", "dorwinion_vintner_bowman"], basic=True))
    t.append(Troop("dorwinion_spearman", "Vineyard Spearman", 11, "Infantry", "spear",
                   paired(SPEAR_MID, SWORD_LOW, SHIELD_LOW),
                   lake("a01", "a01"),
                   ["dorwinion_river_spearman"]))
    t.append(Troop("dorwinion_river_spearman", "River Spearman", 16, "Infantry", "spear",
                   paired(WINGED_LOW, SWORD_MID, SHIELD_MID),
                   lake("a02", "b01", "a01", "b01"),
                   ["dorwinion_redwater_guard"]))
    t.append(Troop("dorwinion_redwater_guard", "Redwater Guard", 21, "Infantry", "spear",
                   paired(WINGED_HIGH, SWORD_MID, SHIELD_MID),
                   lake("a03", "b02", "a03", "b01"),
                   ["dorwinion_veteran_redwater_guard"]))
    t.append(Troop("dorwinion_veteran_redwater_guard", "Veteran Redwater Guard", 26, "Infantry", "spear",
                   paired(WINGED_HIGH, SWORD_HIGH, SHIELD_HIGH),
                   lake("a04", "b03", "a03", "b03"),
                   ["dorwinion_vine_warden"]))
    t.append(Troop("dorwinion_vine_warden", "Warden of the Vines", 31, "Infantry", "spear",
                   paired(POLEAXE, SWORD_HIGH),
                   lake("a04", "b04", "a03", "b03")))

    # ---- Vintner bowmen: bow in Item0 and arrows in Item1/Item2 in BOTH rosters, sword in Item3 ----
    t.append(Troop("dorwinion_vintner_bowman", "Vintner Bowman", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, SWORD_MID),
                   archer("a01", "b01"),
                   ["dorwinion_vintner_archer"]))
    t.append(Troop("dorwinion_vintner_archer", "Vintner Archer", 21, "Ranged", "ranged",
                   same(bow(21), ARROWS, ARROWS, SWORD_MID),
                   archer("a02", "b02", "a01", "b01"),
                   ["dorwinion_redwater_archer"]))
    t.append(Troop("dorwinion_redwater_archer", "Redwater Archer", 26, "Ranged", "ranged",
                   same(bow(26), ARROWS_B, ARROWS, SWORD_HIGH),
                   archer("a03", "b03"),
                   ["dorwinion_vine_bow_master"]))
    t.append(Troop("dorwinion_vine_bow_master", "Master of the Vine-bow", 31, "Ranged", "ranged",
                   same(bow(31), ARROWS_B, ARROWS_B, SWORD_HIGH),
                   archer("a04", "b04")))

    # ---- Dorwinion Noble, the two-way split ----
    t.append(Troop("dorwinion_noble", "Dorwinion Noble", 11, "Infantry", "sword",
                   paired(SWORD_MID, SHIELD_MID, SPEAR_MID),
                   infantry("a01", "a01"),
                   ["dorwinion_river_rider", "dorwinion_vineholm_retainer"], basic=True))

    # River riders: a short branch, spear and shield on a Dale horse.
    t.append(Troop("dorwinion_river_rider", "River Rider", 16, "Cavalry", "cavalry",
                   paired(WINGED_LOW, SHIELD_MID, SWORD_MID),
                   chivalry("a01", "b01"),
                   ["dorwinion_river_cavalier"], horse=HORSE_LIGHT))
    t.append(Troop("dorwinion_river_cavalier", "River Cavalier", 21, "Cavalry", "cavalry",
                   paired(WINGED_HIGH, SHIELD_HIGH, SWORD_HIGH),
                   chivalry("a02", "b02"),
                   horse=HORSE_HEAVY))

    # The household of Vineholm: spear and shield, ending in the poleaxe of the capstone.
    t.append(Troop("dorwinion_vineholm_retainer", "Vineholm Retainer", 16, "Infantry", "spear",
                   paired(WINGED_LOW, SWORD_MID, SHIELD_MID),
                   chivalry("a01", "b01"),
                   ["dorwinion_vineholm_sergeant"]))
    t.append(Troop("dorwinion_vineholm_sergeant", "Sergeant of Vineholm", 26, "Infantry", "spear",
                   paired(WINGED_HIGH, SWORD_HIGH, SHIELD_HIGH),
                   chivalry("a03", "b03"),
                   ["dorwinion_vineholm_guard"]))
    t.append(Troop("dorwinion_vineholm_guard", "Guard of Vineholm", 36, "Infantry", "spear",
                   paired(POLEAXE, SWORD_HIGH),
                   chivalry("a04", "b04")))

    # ---- Militia, named by the culture's four militia attributes. Level 6: the Dale bow ladder has no
    # level-11 cell, and the spearman follows the archer as in Dale's own militia. ----
    t.append(Troop("dorwinion_militia_spearman", "Militia Spearman", 6, "Infantry", "spear",
                   paired(SPEAR_LOW, SWORD_LOW, SHIELD_LOW),
                   lake("a01", "a01"),
                   ["dorwinion_militia_veteran_spearman"]))
    t.append(Troop("dorwinion_militia_archer", "Militia Archer", 6, "Ranged", "ranged",
                   same(bow(6), "default_arrows", SWORD_LOW),
                   archer("a01", "a01"),
                   ["dorwinion_militia_veteran_archer"]))
    t.append(Troop("dorwinion_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(WINGED_LOW, SWORD_MID, SHIELD_MID),
                   infantry("b01", "b01")))
    t.append(Troop("dorwinion_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, SWORD_MID),
                   archer("b01", "b01")))
    return t


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the men's realms' target in
# tools/rebalance_party_template_maxes.py (as Cardolan's); this generator owns them.
def party_templates():
    p = "dorwinion_"
    return [
        ("kingdom_hero_party_dorwinion_template", [
            (p + "levy", 3, 26), (p + "spearman", 2, 22), (p + "noble", 1, 19),
            (p + "river_spearman", 1, 19), (p + "vintner_bowman", 1, 19), (p + "river_rider", 0, 15),
            (p + "redwater_guard", 0, 15), (p + "vintner_archer", 0, 13), (p + "vineholm_retainer", 0, 13),
            (p + "river_cavalier", 0, 10), (p + "veteran_redwater_guard", 0, 8), (p + "redwater_archer", 0, 8),
            (p + "vineholm_sergeant", 0, 8), (p + "vine_warden", 0, 5)]),
        ("kingdom_hero_party_mercenary_dorwinion_template", [
            (p + "spearman", 8, 16), (p + "vintner_bowman", 4, 8), (p + "river_spearman", 4, 8),
            (p + "vintner_archer", 2, 4), (p + "river_rider", 2, 4)]),
        ("kingdom_hero_party_outlaw_dorwinion_template", [
            (p + "levy", 8, 16), (p + "militia_spearman", 4, 8), (p + "militia_archer", 4, 8),
            (p + "vintner_bowman", 2, 4)]),
        ("militia_dorwinion_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_dorwinion_template_level_1", [
            (p + "levy", 6, 6), (p + "spearman", 3, 3), (p + "vintner_bowman", 2, 2)]),
        ("patrol_party_dorwinion_template_level_2", [
            (p + "spearman", 5, 5), (p + "river_spearman", 4, 4), (p + "vintner_archer", 3, 3),
            (p + "river_rider", 2, 2)]),
        ("patrol_party_dorwinion_template_level_3", [
            (p + "redwater_guard", 5, 5), (p + "veteran_redwater_guard", 3, 3), (p + "redwater_archer", 3, 3),
            (p + "river_cavalier", 2, 2), (p + "vineholm_sergeant", 1, 1)]),
        ("rebels_dorwinion_template", [
            (p + "levy", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_dorwinion", [
            (p + "vineholm_guard", 1, 1), (p + "vine_warden", 3, 3), (p + "vineholm_sergeant", 2, 2)]),
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
