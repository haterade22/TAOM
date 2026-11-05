#!/usr/bin/env python3
"""Generate the Rhudaur troop tree and its seven soldier party templates (#978).

Writes:
  Main/_Module/ModuleData/troops/troops_rhudaur.xml             (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-RHUDAUR-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: tools/promote_borrowed_cultures.py clones
them from Gondor with the culture's townsfolk, as for Arthedain.

Design (approved by Mike 2026-10-23, #978). Rhudaur is the eastern of Arnor's three successor
kingdoms (LOTR Appendix A I iii), the hill country between the Hoarwell and the Loudwater, with
Cameth Brin as its chief fortress and the Dunedain of the Angle (Egladil) as its old nobility. In
the books its Dunedain line failed and hill-men allied to Angmar took it; in TAOM (Mike's option
B) the line survives and rules the hill-men, so the realm is Free:

  Hillman Levy (basic, L6) -> Hill Spearman -> Rhudaur Axeman -> Veteran Axeman
      -> Fell Warden (L26)                                hill-men in Dunland axes and shields
  Hill Spearman also -> Hill Rider (L21), the realm's one light cavalry: the country is hills
  Dunadan of the Angle (elite basic, L11) splits two ways:
      Rangers:  Angle Ranger (L16) -> Ranger of the Angle -> Veteran Angle Ranger
                -> Angle Ranger Captain (L31)
      Guard:    Squire of the Angle (L16) -> Guard of Cameth Brin -> Veteran Guard of Cameth Brin
                -> Captain of Cameth Brin -> Lord of the Angle (L36, the capstone)
  Militia spearman / archer (L11) -> veteran militia (L16)

Armour is a PLACEHOLDER (Mike, 2026-10-23): the Arnor kit in LOTRLOME_items/arnor (sk_ar_art_*),
at the same levels Arthedain wears each piece, so the ARMOUR_MESH_TIER_LADDER gate holds. The
hill-men's axes and shields are Dunland's (dunland_caerdh_*, dunland_wulf_*), carried at the
levels Dunland's own troops carry them; the Dunedain lines take Arthedain's weapon picks.

Bows are the generated ladder_rhudaur_bow_t<engine tier> items (tools/ranged_ladders.json line
"rhudaur"), with the Bow skill from tools/ranged_ladder.py.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md). The
machinery is tools/realm_troops.py, shared with Arthedain and Cardolan.

Usage:
  python tools/generate_rhudaur_troops.py            # dry run: report what would change
  python tools/generate_rhudaur_troops.py --apply    # write both files
  python tools/generate_rhudaur_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import ab, one, paired, same  # noqa: E402

CULTURE = "rhudaur"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Dunadan of the Angle (VolunteerRecruitmentService.Rhudaur.cs, weight 2 against the Levy's 6).
MERC_SOURCE = "rhudaur_dunadan"
MILITIA = {"rhudaur_militia_spearman": "rhudaur_militia_veteran_spearman",
           "rhudaur_militia_archer": "rhudaur_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[Rhudaur]", script="generate_rhudaur_troops.py",
                 pt_marker="TAOM-RHUDAUR-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Angle Sellsword")

SKILLS = rt.SKILLS
MILITIA_ELITE_BONUS = rt.MILITIA_ELITE_BONUS
Troop = functools.partial(rt.Troop, realm=REALM)
armour = functools.partial(rt.armour, REALM)
bow = functools.partial(rt.bow, REALM)

SPEAR = "wm_gondor_spear_a"
LANCE = "wm_swan_knight_lance_a"
AXE_LOW = "sturgia_axe_2_t2"
AXE_MID = ("dunland_caerdh_axe_1h_a", "dunland_caerdh_axe_1h_b")
AXE_HIGH = "dunland_caerdh_axe_1h_b"
GREAT_AXE = ("dunland_caerdh_axe_2h_b", "dunland_caerdh_axe_2h_c")
HILL_SHIELD_LOW = ("dunland_wulf_shield_light_a", "dunland_wulf_shield_light_b")
HILL_SHIELD_MID = ("dunland_wulf_shield_medium_a", "dunland_wulf_shield_medium_b")
HILL_SHIELD_HIGH = ("dunland_wulf_shield_heavy_a", "dunland_wulf_shield_heavy_b")
SHIELD_LOW = ("sm_gd_shield_a1", "sm_gd_shield_a3")
SHIELD_MID = ("sm_gd_shield_a3", "sm_gd_shield_a4")
SHIELD_HIGH = ("sm_gd_shield_b1", "sm_gd_shield_b2")
ARROWS = "bodkin_arrows_a"
HORSE_LIGHT = ("t2_empire_horse", "gondor_horse_armor_4")
BASTARD_HEAVY = ("numenorean_bastard_heavy_a", "numenorean_bastard_heavy_b")
BASTARD_ELITE = ("numenorean_bastard_elite_a", "numenorean_bastard_elite_b")


def build():
    t = []

    # ---- The hill-men line: axe and shield ----
    t.append(Troop("rhudaur_levy", "Hillman Levy", 6, "Infantry", "sword",
                   paired(AXE_LOW, HILL_SHIELD_LOW),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("boots_a"), None, ab("cape")),
                   ["rhudaur_hill_spearman"], basic=True))
    t.append(Troop("rhudaur_hill_spearman", "Hill Spearman", 11, "Infantry", "spear",
                   paired(SPEAR, HILL_SHIELD_LOW, AXE_LOW),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("grvs_inf_light_a"),
                          one("gloves_a"), ab("pauld_inf_light")),
                   ["rhudaur_axeman", "rhudaur_hill_rider"]))
    t.append(Troop("rhudaur_axeman", "Rhudaur Axeman", 16, "Infantry", "sword",
                   paired(AXE_MID, HILL_SHIELD_MID),
                   armour(ab("helmet_inf_med"), ab("chainmail_full"), one("grvs_inf_med_a"),
                          one("bracer_inf_med_a"), ab("pauld_inf_light")),
                   ["rhudaur_veteran_axeman"]))
    t.append(Troop("rhudaur_veteran_axeman", "Veteran Axeman", 21, "Infantry", "sword",
                   paired(AXE_HIGH, HILL_SHIELD_HIGH),
                   armour(ab("helmet_inf_med"), ab("chest_inf_med"), one("grvs_inf_med_a"),
                          one("bracer_inf_med_a"), ab("pauld_inf_med")),
                   ["rhudaur_fell_warden"]))
    t.append(Troop("rhudaur_fell_warden", "Fell Warden", 26, "Infantry", "twohand",
                   paired(GREAT_AXE),
                   armour(ab("helmet_warden_heavy"), ab("chest_inf_heavy"), one("grvs_inf_heavy_a"),
                          one("bracer_noble_heavy_a"), one("pauld_inf_heavy_a"))))

    # The realm's one cavalry troop.
    t.append(Troop("rhudaur_hill_rider", "Hill Rider", 21, "Cavalry", "cavalry",
                   paired(SPEAR, SHIELD_MID, BASTARD_HEAVY),
                   armour(ab("helmet_noble_med"), ab("chest_noble_med"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ab("cape")),
                   horse=HORSE_LIGHT))

    # ---- Dunadan of the Angle, the two-way split ----
    t.append(Troop("rhudaur_dunadan", "Dunadan of the Angle", 11, "Infantry", "sword",
                   paired(BASTARD_HEAVY, SHIELD_LOW),
                   armour(one("helmet_noble_light_a"), ab("chainmail_half"), one("grvs_noble_light_a"),
                          one("gloves_a"), ab("cape")),
                   ["rhudaur_angle_ranger", "rhudaur_squire"], basic=True))

    # Rangers: bow in Item0 and arrows in Item1/Item2 in BOTH rosters, sword in Item3.
    t.append(Troop("rhudaur_angle_ranger", "Angle Ranger", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "numenorean_bastard_heavy_a"),
                   armour(one("helmet_noble_light_a"), ab("chainmail_half"), one("grvs_noble_light_a"),
                          one("gloves_a"), ab("cape")),
                   ["rhudaur_ranger"]))
    t.append(Troop("rhudaur_ranger", "Ranger of the Angle", 21, "Ranged", "ranged",
                   same(bow(21), ARROWS, ARROWS, "numenorean_bastard_heavy_b"),
                   armour(ab("helmet_noble_med"), ab("chainmail_full"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ab("cape")),
                   ["rhudaur_veteran_ranger"]))
    t.append(Troop("rhudaur_veteran_ranger", "Veteran Angle Ranger", 26, "Ranged", "ranged",
                   same(bow(26), ARROWS, ARROWS, "numenorean_bastard_elite_a"),
                   armour(ab("helmet_noble_heavy"), ab("chainmail_full"), one("grvs_noble_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("cape")),
                   ["rhudaur_ranger_captain"]))
    t.append(Troop("rhudaur_ranger_captain", "Angle Ranger Captain", 31, "Ranged", "ranged",
                   same(bow(31), ARROWS, ARROWS, "numenorean_bastard_elite_b"),
                   armour(ab("helmet_noble_heavy"), ab("chest_noble_med"), one("grvs_noble_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("cape"))))

    # Guard of Cameth Brin: two-handed, never a shield, until the Captain takes sword and shield.
    t.append(Troop("rhudaur_squire", "Squire of the Angle", 16, "Infantry", "twohand",
                   same("numenorean_poleaxe_medium"),
                   armour(ab("helmet_noble_med"), ab("chainmail_full"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ab("pauld_noble_med")),
                   ["rhudaur_guard"]))
    t.append(Troop("rhudaur_guard", "Guard of Cameth Brin", 21, "Infantry", "twohand",
                   paired(("numenorean_greatsword_heavy_a", "numenorean_greatsword_heavy_b"),
                          "numenorean_poleaxe_medium"),
                   armour(ab("helmet_noble_med"), ab("chest_noble_med"), one("grvs_noble_med_a"),
                          one("bracer_noble_med_a"), ("pauld_noble_med_a", "pauld_noble_med_c")),
                   ["rhudaur_veteran_guard"]))
    t.append(Troop("rhudaur_veteran_guard", "Veteran Guard of Cameth Brin", 26, "Infantry", "twohand",
                   paired(("numenorean_greatsword_medium_a", "numenorean_greatsword_medium_b"),
                          "numenorean_poleaxe_heavy"),
                   armour(ab("helmet_guard_heavy"), ab("chest_noble_heavy"), one("grvs_noble_heavy_a"),
                          one("bracer_noble_heavy_a"), ab("pauld_noble_heavy")),
                   ["rhudaur_guard_captain"]))
    t.append(Troop("rhudaur_guard_captain", "Captain of Cameth Brin", 31, "Infantry", "sword",
                   paired(BASTARD_ELITE, SHIELD_HIGH),
                   armour(ab("helmet_noble_elite"), ab("chest_noble_heavy"), one("grvs_noble_elite_a"),
                          one("bracer_noble_elite_a"), ab("pauld_noble_elite")),
                   ["rhudaur_lord_of_the_angle"]))
    t.append(Troop("rhudaur_lord_of_the_angle", "Lord of the Angle", 36, "Infantry", "twohand",
                   paired(("numenorean_greatsword_elite_a", "numenorean_greatsword_elite_b"),
                          "numenorean_poleaxe_heavy"),
                   armour(ab("helmet_guard_elite"), ab("chest_noble_heavy"), one("grvs_noble_elite_a"),
                          one("bracer_noble_elite_a"), ab("pauld_cape_noble_elite"))))

    # ---- Militia, named by the culture's four militia attributes ----
    t.append(Troop("rhudaur_militia_spearman", "Militia Spearman", 11, "Infantry", "spear",
                   paired(SPEAR, HILL_SHIELD_LOW, AXE_LOW),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("boots_a"), None, ab("cape")),
                   ["rhudaur_militia_veteran_spearman"]))
    t.append(Troop("rhudaur_militia_archer", "Militia Archer", 11, "Ranged", "ranged",
                   same(bow(11), ARROWS, ARROWS, AXE_LOW),
                   armour(one("helmet_inf_light_a"), ab("chainmail_half"), one("boots_a"), None, ab("cape")),
                   ["rhudaur_militia_veteran_archer"]))
    t.append(Troop("rhudaur_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(SPEAR, HILL_SHIELD_MID, "dunland_caerdh_axe_1h_a"),
                   armour(ab("helmet_inf_med"), ab("chainmail_full"), one("grvs_inf_med_a"),
                          one("gloves_a"), ab("pauld_inf_med"))))
    t.append(Troop("rhudaur_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "dunland_caerdh_axe_1h_a"),
                   armour(ab("helmet_inf_med"), ab("chainmail_full"), one("grvs_inf_med_a"),
                          one("gloves_a"), ab("cape"))))
    return t


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the men's realms' target in
# tools/rebalance_party_template_maxes.py (as Arthedain's since #818); this generator owns them.
def party_templates():
    p = "rhudaur_"
    return [
        ("kingdom_hero_party_rhudaur_template", [
            (p + "levy", 3, 26), (p + "hill_spearman", 2, 22), (p + "dunadan", 1, 19),
            (p + "axeman", 1, 19), (p + "angle_ranger", 1, 19), (p + "squire", 0, 13),
            (p + "veteran_axeman", 0, 15), (p + "ranger", 0, 15), (p + "hill_rider", 0, 13),
            (p + "guard", 0, 10), (p + "fell_warden", 0, 8), (p + "veteran_ranger", 0, 8),
            (p + "veteran_guard", 0, 8), (p + "guard_captain", 0, 5)]),
        ("militia_rhudaur_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_rhudaur_template_level_1", [
            (p + "levy", 6, 6), (p + "hill_spearman", 3, 3), (p + "angle_ranger", 2, 2)]),
        ("patrol_party_rhudaur_template_level_2", [
            (p + "hill_spearman", 5, 5), (p + "axeman", 4, 4), (p + "ranger", 3, 3), (p + "hill_rider", 2, 2)]),
        ("patrol_party_rhudaur_template_level_3", [
            (p + "veteran_axeman", 5, 5), (p + "fell_warden", 3, 3), (p + "veteran_ranger", 3, 3),
            (p + "veteran_guard", 2, 2), (p + "guard_captain", 1, 1)]),
        ("rebels_rhudaur_template", [
            (p + "levy", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_rhudaur", [
            (p + "lord_of_the_angle", 1, 1), (p + "guard_captain", 3, 3), (p + "ranger_captain", 2, 2)]),
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
