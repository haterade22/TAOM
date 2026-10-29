#!/usr/bin/env python3
"""Generate the Angaladh troop tree and its nine soldier party templates.

Writes:
  Main/_Module/ModuleData/troops/troops_angaladh.xml             (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-ANGALADH-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: tools/promote_borrowed_cultures.py clones
them from Dale with the culture's townsfolk, as for the Arnor realms.

Design (approved by Mike 2026-10-28). Angaladh is the realm of the Woodmen of Mirkwood: the forest
folk who hold Woodmen-town under the eaves of the Greenwood and keep the Ironwood. They fight on
foot only, spear in front and the best bowmen of the three new realms behind:

  Woodman Levy (basic, L6) -> Woodman Spearman (L11) -> Forest Spearman (L16) -> Forest Guard
      -> Veteran Forest Guard -> Forest Warden (L31)            spear + shield, a poleaxe at L31
  Woodman Spearman also -> Woodman Axeman (L16) -> Woodman Axe-warrior (L21)
      -> Ironwood Hewer (L26), two-handed
  Woodman Levy also -> Woodman Bowman (L16) -> Woodman Archer -> Forest Archer
      -> Master Woodman Archer (L31)
  Ironwood Youth (elite basic, L11) -> Ironwood Guard (L16) and Woodman Bowman; the guard goes on to
      Ironwood Sentinel (L26) -> Guard of Woodmen-town (L36, the capstone, a poleaxe)
  Militia spearman / archer (L6) -> veteran militia (L16)

No troop rides. The militia archer is level 6, not 11, because the Dale bow ladder has cells only at
engine tiers 1, 3, 4, 5 and 6 (levels 6, 16, 21, 26 and 31): every archer sits on one of those levels.

Armour is a PLACEHOLDER from Dale's kit (sk_dale_*), until the realm has its own. Dale's ids carry
no tier keyword, so ARMOUR_MESH_TIER_LADDER cannot check them; the guard is instead a test
(tools/tests/test_realm_troops.py, BorrowedArmourAnchorTests): a Dale piece is never worn here
below the lowest level at which a Dale troop wears it, so the armour class table does not move.

Weapons are Dale's, placed by sustained DPS against the melee ladder (tools/melee_damage.py,
2026-10-28): spear_a 34, sword_c 34, spear_b 42, winged_spear_b 49, war_axe_a 51, sword_b 52,
great_axe_a 52, winged_spear_a 60, sword_a 60, war_axe_b 61, war_axe_c 69, poleaxe_a 78. Dale has
no kingdom offset in tools/melee_ladders.json, so neither has this realm. Bows are the Dale ladder's
items (tools/ranged_ladders.json: the `dale` line claims troops_angaladh.xml), with the skill from
tools/ranged_ladder.py.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md). The
machinery is tools/realm_troops.py, shared with the Arnor realms.

Usage:
  python tools/generate_angaladh_troops.py            # dry run: report what would change
  python tools/generate_angaladh_troops.py --apply    # write both files
  python tools/generate_angaladh_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import one, paired, same  # noqa: E402

CULTURE = "angaladh"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Ironwood Youth, the elite basic troop (weight 2 against the Levy's 6).
MERC_SOURCE = "angaladh_ironwood_youth"
MILITIA = {"angaladh_militia_spearman": "angaladh_militia_veteran_spearman",
           "angaladh_militia_archer": "angaladh_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[Angáladh]", script="generate_angaladh_troops.py",
                 pt_marker="TAOM-ANGALADH-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Woodman Sellsword", armour_prefix="sk_dale_",
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
AXE_MID = "dale_war_axe_b"         # 61
AXE_HIGH = "dale_war_axe_c"        # 69
GREAT_AXE = "dale_great_axe_a"     # 52, two-handed: never beside a shield
POLEAXE = "dale_poleaxe_a"         # 78, two-handed: never beside a shield
SHIELD_LOW = ("wm_dale_shield_a06", "wm_dale_shield_a08")
SHIELD_MID = ("wm_dale_shield_a03", "wm_dale_shield_b01")
SHIELD_HIGH = ("wm_dale_shield_a02", "wm_dale_shield_b02")
ARROWS = "bodkin_arrows_a"
ARROWS_B = "bodkin_arrows_b"


# Dale armour, by family. `a` and `b` are the variant ("a01", "b02") each roster wears. Each family's
# lowest wearer in Dale's own troops (the level a piece must not be worn below):
#   lake_town mariner  a01 6, a02 16, a03 21, a04 26; b01 16, b02 21, b03 26, b04 31
#   infantry           a01 11, b01 16, a02 and b02 21, a03 and b03 26, a04 and b04 31
#   archer             a01 6, b01 16, a02 and b02 21, a03 and b03 26, a04 and b04 31
# The lake_town and archer families have no shoulder for every variant, so a shoulder variant is passed.
def lake(a, b, sa=None, sb=None):
    return armour(*(((f"lake_town_{slot}_mariner_{a}", f"lake_town_{slot}_mariner_{b}"))
                    for slot in ("helmet", "chest", "boots", "bracers")),
                  (f"lake_town_shoulder_mariner_{sa or a}", f"lake_town_shoulder_mariner_{sb or b}"))


def family(name):
    def build_set(a, b, sa=None, sb=None):
        return armour(*((f"{piece}_{name}_{a}", f"{piece}_{name}_{b}")
                        for piece in ("helmet", "chest", "boots", "gauntlet")),
                      (f"shoulder_{name}_{sa or a}", f"shoulder_{name}_{sb or b}"))
    return build_set


infantry = family("infrantry")
archer = family("archer")


def build():
    t = []

    # ---- Levy and the spear line ----
    t.append(Troop("angaladh_levy", "Woodman Levy", 6, "Infantry", "spear",
                   paired(SPEAR_LOW, SWORD_LOW, SHIELD_LOW),
                   armour(None, one("lake_town_chest_mariner_a01"), one("lake_town_boots_mariner_a01"), None, None),
                   ["angaladh_spearman", "angaladh_woodman_bowman"], basic=True))
    t.append(Troop("angaladh_spearman", "Woodman Spearman", 11, "Infantry", "spear",
                   paired(SPEAR_MID, SWORD_LOW, SHIELD_LOW),
                   lake("a01", "a01"),
                   ["angaladh_forest_spearman", "angaladh_woodman_axeman"]))
    t.append(Troop("angaladh_forest_spearman", "Forest Spearman", 16, "Infantry", "spear",
                   paired(WINGED_LOW, SWORD_MID, SHIELD_MID),
                   lake("a02", "b01", "a01", "b01"),
                   ["angaladh_forest_guard"]))
    t.append(Troop("angaladh_forest_guard", "Forest Guard", 21, "Infantry", "spear",
                   paired(WINGED_HIGH, SWORD_MID, SHIELD_MID),
                   lake("a03", "b02", "a03", "b01"),
                   ["angaladh_veteran_forest_guard"]))
    t.append(Troop("angaladh_veteran_forest_guard", "Veteran Forest Guard", 26, "Infantry", "spear",
                   paired(WINGED_HIGH, SWORD_HIGH, SHIELD_HIGH),
                   lake("a04", "b03", "a03", "b03"),
                   ["angaladh_forest_warden"]))
    t.append(Troop("angaladh_forest_warden", "Forest Warden", 31, "Infantry", "spear",
                   paired(POLEAXE, SWORD_HIGH),
                   lake("a04", "b04", "a03", "b03")))

    # ---- The axe line: axe and shield, then the Ironwood Hewer's two-handed great-axe ----
    t.append(Troop("angaladh_woodman_axeman", "Woodman Axeman", 16, "Infantry", "sword",
                   paired(AXE_MID, SHIELD_MID),
                   infantry("a01", "b01"),
                   ["angaladh_woodman_axe_warrior"]))
    t.append(Troop("angaladh_woodman_axe_warrior", "Woodman Axe-warrior", 21, "Infantry", "sword",
                   paired(AXE_HIGH, SHIELD_MID, WINGED_LOW),
                   infantry("a02", "b02"),
                   ["angaladh_ironwood_hewer"]))
    t.append(Troop("angaladh_ironwood_hewer", "Ironwood Hewer", 26, "Infantry", "twohand",
                   same(GREAT_AXE, AXE_HIGH),
                   infantry("a03", "b03")))

    # ---- Woodman bowmen, the realm's strongest line: bow in Item0 and arrows in Item1/Item2 in
    # BOTH rosters, sword in Item3 ----
    t.append(Troop("angaladh_woodman_bowman", "Woodman Bowman", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, SWORD_MID),
                   archer("a01", "b01"),
                   ["angaladh_woodman_archer"]))
    t.append(Troop("angaladh_woodman_archer", "Woodman Archer", 21, "Ranged", "ranged",
                   same(bow(21), ARROWS, ARROWS, SWORD_MID),
                   archer("a02", "b02", "a01", "b01"),
                   ["angaladh_forest_archer"]))
    t.append(Troop("angaladh_forest_archer", "Forest Archer", 26, "Ranged", "ranged",
                   same(bow(26), ARROWS_B, ARROWS, SWORD_HIGH),
                   archer("a03", "b03"),
                   ["angaladh_master_archer"]))
    t.append(Troop("angaladh_master_archer", "Master Woodman Archer", 31, "Ranged", "ranged",
                   same(bow(31), ARROWS_B, ARROWS_B, SWORD_HIGH),
                   archer("a04", "b04")))

    # ---- Ironwood Youth and the guard line to the capstone ----
    t.append(Troop("angaladh_ironwood_youth", "Ironwood Youth", 11, "Infantry", "spear",
                   paired(SPEAR_MID, SWORD_MID, SHIELD_MID),
                   infantry("a01", "a01"),
                   ["angaladh_ironwood_guard", "angaladh_woodman_bowman"], basic=True))
    t.append(Troop("angaladh_ironwood_guard", "Ironwood Guard", 16, "Infantry", "spear",
                   paired(WINGED_LOW, SWORD_MID, SHIELD_MID),
                   infantry("a01", "b01"),
                   ["angaladh_ironwood_sentinel"]))
    t.append(Troop("angaladh_ironwood_sentinel", "Ironwood Sentinel", 26, "Infantry", "spear",
                   paired(WINGED_HIGH, SWORD_HIGH, SHIELD_HIGH),
                   infantry("a03", "b03"),
                   ["angaladh_woodmen_guard"]))
    t.append(Troop("angaladh_woodmen_guard", "Guard of Woodmen-town", 36, "Infantry", "spear",
                   paired(POLEAXE, SWORD_HIGH),
                   infantry("a04", "b04")))

    # ---- Militia, named by the culture's four militia attributes. Level 6: the Dale bow ladder has no
    # level-11 cell, and the spearman follows the archer as in Dale's own militia. ----
    t.append(Troop("angaladh_militia_spearman", "Militia Spearman", 6, "Infantry", "spear",
                   paired(SPEAR_LOW, SWORD_LOW, SHIELD_LOW),
                   lake("a01", "a01"),
                   ["angaladh_militia_veteran_spearman"]))
    t.append(Troop("angaladh_militia_archer", "Militia Archer", 6, "Ranged", "ranged",
                   same(bow(6), "default_arrows", SWORD_LOW),
                   archer("a01", "a01"),
                   ["angaladh_militia_veteran_archer"]))
    t.append(Troop("angaladh_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(WINGED_LOW, SWORD_MID, SHIELD_MID),
                   infantry("b01", "b01")))
    t.append(Troop("angaladh_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, SWORD_MID),
                   archer("b01", "b01")))
    return t


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the men's realms' target in
# tools/rebalance_party_template_maxes.py (as Cardolan's); this generator owns them.
def party_templates():
    p = "angaladh_"
    return [
        ("kingdom_hero_party_angaladh_template", [
            (p + "levy", 3, 26), (p + "spearman", 2, 22), (p + "ironwood_youth", 1, 19),
            (p + "forest_spearman", 1, 19), (p + "woodman_bowman", 1, 19), (p + "woodman_archer", 0, 15),
            (p + "forest_guard", 0, 15), (p + "woodman_axeman", 0, 13), (p + "forest_archer", 0, 13),
            (p + "ironwood_guard", 0, 10), (p + "veteran_forest_guard", 0, 8), (p + "master_archer", 0, 8),
            (p + "woodman_axe_warrior", 0, 8), (p + "forest_warden", 0, 5)]),
        ("kingdom_hero_party_mercenary_angaladh_template", [
            (p + "spearman", 8, 16), (p + "woodman_bowman", 4, 8), (p + "forest_spearman", 4, 8),
            (p + "woodman_archer", 2, 4), (p + "woodman_axeman", 2, 4)]),
        ("kingdom_hero_party_outlaw_angaladh_template", [
            (p + "levy", 8, 16), (p + "militia_spearman", 4, 8), (p + "militia_archer", 4, 8),
            (p + "woodman_bowman", 2, 4)]),
        ("militia_angaladh_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_angaladh_template_level_1", [
            (p + "levy", 6, 6), (p + "spearman", 3, 3), (p + "woodman_bowman", 2, 2)]),
        ("patrol_party_angaladh_template_level_2", [
            (p + "spearman", 5, 5), (p + "forest_spearman", 4, 4), (p + "woodman_archer", 3, 3),
            (p + "woodman_axeman", 2, 2)]),
        ("patrol_party_angaladh_template_level_3", [
            (p + "forest_guard", 5, 5), (p + "veteran_forest_guard", 3, 3), (p + "forest_archer", 3, 3),
            (p + "woodman_axe_warrior", 2, 2), (p + "ironwood_hewer", 1, 1)]),
        ("rebels_angaladh_template", [
            (p + "levy", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_angaladh", [
            (p + "woodmen_guard", 1, 1), (p + "forest_warden", 3, 3), (p + "ironwood_sentinel", 2, 2)]),
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
