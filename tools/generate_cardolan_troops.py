#!/usr/bin/env python3
"""Generate the Cardolan troop tree and its seven soldier party templates (#978).

Writes:
  Main/_Module/ModuleData/troops/troops_cardolan.xml            (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-CARDOLAN-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: tools/promote_borrowed_cultures.py clones
them from Gondor with the culture's townsfolk, as for Arthedain.

Design (approved by Mike 2026-10-23, #978). Cardolan is the southern of Arnor's three successor
kingdoms (LOTR Appendix A I iii), the plains and river country between the Baranduin and the
Gwathlo, with Tharbad on the Greyflood and the royal barrows of Tyrn Gorthad. In TAOM its line
survives, as Arthedain's does. Weak in numbers like Arthedain, but the horse realm of the three:

  Levy (basic, L6) -> Spearman -> Cardolan Spearman -> Barrow Guard -> Veteran Barrow Guard
      -> Warden of Tyrn Gorthad (L31)                     spear + shield + sword
      Veteran Barrow Guard -> Tharbad Bridge Guard (L31, 2026-11-06)   a pike in both hands, no shield
  Dunadan of Cardolan (elite basic, L11) splits two ways:
      Knights:  Rider of Cardolan (L16) -> Lancer -> Knight of Cardolan -> Knight-Captain
                -> King's Guard of Caras Gorthad (L36, the capstone, mounted)
      Bowmen:   Minhiriath Bowman (L16) -> Bowman of Minhiriath -> Veteran Minhiriath Bowman (L26)
  Militia spearman / archer (L11) -> veteran militia (L16)

Armour is a PLACEHOLDER (Mike, 2026-10-23: Arnor or Gondor armour until Cardolan has its own):
Gondor's Anfalas, Pinnath Gelin and Dol Amroth pieces (sk_gd_*), placed by the id tier keyword
so the ARMOUR_MESH_TIER_LADDER gate holds, the same levels Gondor's own troops wear them at. The
Cardolan spears (wm_gondor_*_cardolan) are the one piece of the realm's own in the Armory.

Weapons follow Arthedain's per-level picks (generate_arthedain_troops.py, placed by sustained DPS
against the melee ladder); `cardolan` has its own kingdom offset in tools/melee_ladders.json.

Bows are the generated ladder_cardolan_bow_t<engine tier> items (tools/ranged_ladders.json line
"cardolan"), with the Bow skill from tools/ranged_ladder.py.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md). The
machinery is tools/realm_troops.py, shared with Arthedain and Rhudaur.

Usage:
  python tools/generate_cardolan_troops.py            # dry run: report what would change
  python tools/generate_cardolan_troops.py --apply    # write both files
  python tools/generate_cardolan_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import ab, one, paired, same  # noqa: E402

CULTURE = "cardolan"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Dunadan of Cardolan (VolunteerRecruitmentService.Cardolan.cs, weight 2 against the Levy's 6).
MERC_SOURCE = "cardolan_dunadan"
MILITIA = {"cardolan_militia_spearman": "cardolan_militia_veteran_spearman",
           "cardolan_militia_archer": "cardolan_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[Cardolan]", script="generate_cardolan_troops.py",
                 pt_marker="TAOM-CARDOLAN-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Dunadan Sellsword", armour_prefix="sk_gd_")

SKILLS = rt.SKILLS
MILITIA_ELITE_BONUS = rt.MILITIA_ELITE_BONUS
Troop = functools.partial(rt.Troop, realm=REALM)
armour = functools.partial(rt.armour, REALM)
bow = functools.partial(rt.bow, REALM)

SPEAR = "wm_gondor_spear_b_cardolan"
LIGHT_SPEAR = "wm_gondor_light_spear_cardolan"
PIKE = "fine_pike_t4"
LANCE = "wm_swan_knight_lance_a"
SHIELD_LOW = ("sm_gd_shield_a1", "sm_gd_shield_a3")
SHIELD_MID = ("sm_gd_shield_a3", "sm_gd_shield_a4")
SHIELD_HIGH = ("sm_gd_shield_b1", "sm_gd_shield_b2")
ARROWS = "bodkin_arrows_a"
HORSE_LIGHT = ("t2_empire_horse", "gondor_horse_armor_4")
HORSE_HEAVY = ("noble_horse_imperial", "gondor_horse_armor_3")
BASTARD_HEAVY = ("numenorean_bastard_heavy_a", "numenorean_bastard_heavy_b")
BASTARD_ELITE = ("numenorean_bastard_elite_a", "numenorean_bastard_elite_b")

# Armour pieces (prefix sk_gd_), by band.
LIGHT_FOOT = dict(head=None, body=ab("anf_inf_chainmail"), leg=one("ano_boots_a"), gloves=None, cape=None)


def build():
    t = []

    # ---- Levy and the spear line ----
    t.append(Troop("cardolan_levy", "Levy", 6, "Infantry", "spear",
                   paired(LIGHT_SPEAR, SHIELD_LOW),
                   armour(**LIGHT_FOOT),
                   ["cardolan_spearman"], basic=True))
    t.append(Troop("cardolan_spearman", "Spearman", 11, "Infantry", "spear",
                   paired(SPEAR, SHIELD_LOW, "wm_gondor_sword_a01"),
                   armour(one("pin_inf_helmet_med_a"), ab("pin_chainmail"), one("ano_grvs_inf_light_a"),
                          one("ano_gloves_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_river_spearman"]))
    t.append(Troop("cardolan_river_spearman", "Cardolan Spearman", 16, "Infantry", "spear",
                   paired(SPEAR, SHIELD_MID, "wm_gondor_sword_a07"),
                   armour(one("pin_inf_helmet_med_a"), ab("pin_inf_chest_med"), one("ano_grvs_inf_med_a"),
                          one("ano_gloves_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_barrow_guard"]))
    t.append(Troop("cardolan_barrow_guard", "Barrow Guard", 21, "Infantry", "spear",
                   paired(SPEAR, SHIELD_MID, BASTARD_HEAVY),
                   armour(ab("anf_inf_helmet_med"), ab("anf_inf_chest_med"), one("ano_grvs_inf_med_a"),
                          one("ano_bracer_inf_med_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_veteran_barrow_guard"]))
    # The Barrow Guard keeps one target. A second one here would send about half of the AI lords' Barrow Guards
    # to a dead end: the engine gives each party leader's hash one target of a two-target troop weight 9999.
    t.append(Troop("cardolan_veteran_barrow_guard", "Veteran Barrow Guard", 26, "Infantry", "spear",
                   paired(SPEAR, SHIELD_HIGH, BASTARD_ELITE),
                   armour(ab("pin_spear_helmet_heavy"), ab("pin_inf_chest_heavy"), one("ano_grvs_inf_heavy_a"),
                          one("ano_bracer_noble_heavy_a"), one("ano_pauld_inf_heavy_a")),
                   ["cardolan_warden", "cardolan_tharbad_bridge_guard"]))
    t.append(Troop("cardolan_warden", "Warden of Tyrn Gorthad", 31, "Infantry", "sword",
                   paired(BASTARD_ELITE, SHIELD_HIGH, SPEAR),
                   armour(ab("pin_spear_helmet_heavy"), ab("pin_inf_chest_heavy"), one("ano_grvs_inf_heavy_a"),
                          one("ano_bracer_noble_heavy_a"), one("ano_pauld_cape_inf_elite_a"))))
    # Holds the Greyflood crossing at Tharbad, the Warden's sibling at the top of the line (2026-11-06, from
    # player feedback): a pike in both hands and no shield. fine_pike_t4 is vanilla's, the pike of the Gondor
    # Serelond Pikeman; the Armory has no Gondor or Arnor pike. Role spear: Item0 is the weapon in hand at spawn
    # and the engine scores a pike with the Polearm skill.
    t.append(Troop("cardolan_tharbad_bridge_guard", "Tharbad Bridge Guard", 31, "Infantry", "spear",
                   paired(PIKE, BASTARD_ELITE),
                   armour(ab("pin_spear_helmet_heavy"), ab("pin_inf_chest_heavy"), one("ano_grvs_inf_heavy_a"),
                          one("ano_bracer_noble_heavy_a"), one("ano_pauld_cape_inf_elite_a"))))

    # ---- Dunadan of Cardolan, the two-way split ----
    t.append(Troop("cardolan_dunadan", "Dunadan of Cardolan", 11, "Infantry", "sword",
                   paired(BASTARD_HEAVY, SHIELD_LOW),
                   armour(ab("anf_inf_helmet_med"), ab("anf_inf_chainmail"), one("ano_grvs_inf_light_a"),
                          one("ano_gloves_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_rider", "cardolan_bowman"], basic=True))

    # Knights: the horse realm's long cavalry line, ending in the mounted capstone.
    t.append(Troop("cardolan_rider", "Rider of Cardolan", 16, "Cavalry", "cavalry",
                   paired(SPEAR, SHIELD_MID, "wm_gondor_sword_a07"),
                   armour(ab("anf_inf_helmet_med"), ab("anf_inf_chest_med"), one("ano_grvs_inf_med_a"),
                          one("ano_gloves_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_lancer"], horse=HORSE_LIGHT))
    t.append(Troop("cardolan_lancer", "Lancer of Cardolan", 21, "Cavalry", "cavalry",
                   paired(LANCE, SHIELD_MID, BASTARD_HEAVY),
                   armour(ab("anf_inf_helmet_med"), ab("anf_inf_chest_med"), one("ano_grvs_inf_med_a"),
                          one("ano_bracer_inf_med_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_knight"], horse=HORSE_LIGHT))
    t.append(Troop("cardolan_knight", "Knight of Cardolan", 26, "Cavalry", "cavalry",
                   paired(LANCE, SHIELD_HIGH, BASTARD_ELITE),
                   armour(one("anf_cav_helmet_heavy_a"), one("anf_inf_chest_heavy_a"), one("ano_grvs_inf_heavy_a"),
                          one("ano_bracer_noble_heavy_a"), one("ano_pauld_inf_heavy_a")),
                   ["cardolan_knight_captain"], horse=HORSE_HEAVY))
    t.append(Troop("cardolan_knight_captain", "Knight-Captain of Cardolan", 31, "Cavalry", "cavalry",
                   paired(LANCE, SHIELD_HIGH, BASTARD_ELITE),
                   armour(ab("dol_helmet_heavy"), ab("dol_chest_heavy"), one("dol_grvs_heavy_a"),
                          one("dol_bracer_heavy_a"), one("dol_pauld_cape_noble_heavy_a")),
                   ["cardolan_kings_guard"], horse=HORSE_HEAVY))
    t.append(Troop("cardolan_kings_guard", "King's Guard of Caras Gorthad", 36, "Cavalry", "cavalry",
                   paired(LANCE, SHIELD_HIGH, BASTARD_ELITE),
                   armour(ab("dol_cav_helmet_elite"), ab("dol_chest_elite"), one("dol_grvs_elite_a"),
                          one("dol_bracer_elite_a"), one("dol_pauld_cape_noble_elite_a")),
                   horse=HORSE_HEAVY))

    # Bowmen of Minhiriath: bow in Item0 and arrows in Item1/Item2 in BOTH rosters, sword in Item3.
    t.append(Troop("cardolan_bowman", "Minhiriath Bowman", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "wm_gondor_sword_a07"),
                   armour(one("pin_inf_helmet_med_a"), ab("pin_chainmail"), one("ano_grvs_inf_light_a"),
                          one("ano_gloves_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_minhiriath_bowman"]))
    t.append(Troop("cardolan_minhiriath_bowman", "Bowman of Minhiriath", 21, "Ranged", "ranged",
                   same(bow(21), ARROWS, ARROWS, "wm_gondor_sword_a03"),
                   armour(one("pin_inf_helmet_med_a"), ab("pin_inf_chest_med"), one("ano_grvs_inf_med_a"),
                          one("ano_bracer_inf_med_a"), ab("ano_pauld_inf_med")),
                   ["cardolan_veteran_bowman"]))
    t.append(Troop("cardolan_veteran_bowman", "Veteran Minhiriath Bowman", 26, "Ranged", "ranged",
                   same(bow(26), ARROWS, ARROWS, "numenorean_bastard_heavy_a"),
                   armour(ab("pin_arc_helmet_heavy"), ab("pin_inf_chest_heavy"), one("ano_grvs_inf_heavy_a"),
                          one("ano_bracer_noble_heavy_a"), one("ano_pauld_inf_heavy_a"))))

    # ---- Militia, named by the culture's four militia attributes ----
    t.append(Troop("cardolan_militia_spearman", "Militia Spearman", 11, "Infantry", "spear",
                   paired(SPEAR, SHIELD_LOW, "wm_gondor_sword_a01"),
                   armour(None, ab("anf_inf_chainmail"), one("ano_boots_a"), None, None),
                   ["cardolan_militia_veteran_spearman"]))
    t.append(Troop("cardolan_militia_archer", "Militia Archer", 11, "Ranged", "ranged",
                   same(bow(11), ARROWS, ARROWS, "wm_gondor_sword_a01"),
                   armour(None, ab("pin_chainmail"), one("ano_boots_a"), None, None),
                   ["cardolan_militia_veteran_archer"]))
    t.append(Troop("cardolan_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(SPEAR, SHIELD_MID, "wm_gondor_sword_a03"),
                   armour(one("pin_inf_helmet_med_a"), ab("pin_inf_chest_med"), one("ano_grvs_inf_med_a"),
                          one("ano_gloves_a"), ab("ano_pauld_inf_med"))))
    t.append(Troop("cardolan_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "wm_gondor_sword_a03"),
                   armour(one("pin_inf_helmet_med_a"), ab("pin_inf_chest_med"), one("ano_grvs_inf_med_a"),
                          one("ano_gloves_a"), ab("ano_pauld_inf_med"))))
    return t


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the men's realms' target in
# tools/rebalance_party_template_maxes.py (as Arthedain's since #818); this generator owns them.
def party_templates():
    p = "cardolan_"
    return [
        ("kingdom_hero_party_cardolan_template", [
            (p + "levy", 3, 26), (p + "spearman", 2, 22), (p + "dunadan", 1, 19),
            (p + "river_spearman", 1, 19), (p + "bowman", 1, 19), (p + "rider", 0, 15),
            (p + "barrow_guard", 0, 15), (p + "minhiriath_bowman", 0, 13), (p + "lancer", 0, 13),
            (p + "veteran_barrow_guard", 0, 10), (p + "veteran_bowman", 0, 8), (p + "knight", 0, 8),
            (p + "warden", 0, 8), (p + "knight_captain", 0, 5)]),
        ("militia_cardolan_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_cardolan_template_level_1", [
            (p + "levy", 6, 6), (p + "spearman", 3, 3), (p + "bowman", 2, 2)]),
        ("patrol_party_cardolan_template_level_2", [
            (p + "spearman", 5, 5), (p + "river_spearman", 4, 4), (p + "minhiriath_bowman", 3, 3),
            (p + "rider", 2, 2)]),
        ("patrol_party_cardolan_template_level_3", [
            (p + "barrow_guard", 5, 5), (p + "veteran_barrow_guard", 3, 3), (p + "veteran_bowman", 3, 3),
            (p + "lancer", 2, 2), (p + "knight", 1, 1)]),
        ("rebels_cardolan_template", [
            (p + "levy", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_cardolan", [
            (p + "kings_guard", 1, 1), (p + "knight_captain", 3, 3), (p + "warden", 2, 2)]),
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
