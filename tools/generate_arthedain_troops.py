#!/usr/bin/env python3
"""Generate the Arthedain troop tree and its seven soldier party templates.

Writes:
  Main/_Module/ModuleData/troops/troops_arthedain.xml           (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-ARTHEDAIN-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: they name the culture's townsfolk
(villager_arthedain, caravan_guard_arthedain, ...), which tools/promote_borrowed_cultures.py clones
from Gondor together with the templates that use them.

Design (approved by Mike 2026-10-07; plan i-am-creating-a-iridescent-robin.md). Arthedain is the
what-if northern realm in which Arvedui's line survived. It is meant to be weak in NUMBERS (low-tier
clans, small parties, poor towns) while the Dunedain lines stay good, so the lord party templates
stop at engine tier 6. Three tier 7 (L36) troops sit above them and are reached by upgrading: the
King's Guard of Fornost, the Evendim Warden and the Dunedain Steel Bowman (the last two and the
Knight of Annuminas added 2026-11-06 from player feedback; the engine's AI upgrader and the
notables' volunteer growth read these edges too):

  Levy (basic, L6) -> Spearman -> Fornost Footman -> Fornost Shieldman -> Fornost Guard
      -> Warden of Fornost (L31) -> Evendim Warden (L36)  spear + shield + sword, infantry armour;
                                                           the Evendim Warden drops the shield for a poleaxe
  Dunadan Youth (elite basic, L11) splits three ways:
      Rangers:  Ranger of the North -> Ranger -> Veteran Ranger -> Ranger Captain (L31)
                -> Dunedain Steel Bowman (L36)
      Citadel:  Dunadan Squire -> Guard of the Citadel -> Veteran Citadel Guard
                -> Knight of Arthedain -> King's Guard of Fornost (L36, the capstone)
      Cavalry:  Outrider (L21) -> Lancer (L26) -> Knight of Annuminas (L31)   short on purpose:
                Arthedain's horse at Fornost came from Gondor and Lindon, not from its own stables
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

The machinery (skill curves, the upgrade skill clamp, the militia rule, the tavern mercenary, the
self-check and the writes) is tools/realm_troops.py, shared with the Cardolan and Rhudaur
generators (#978); this file is Arthedain's design only.

Usage:
  python tools/generate_arthedain_troops.py            # dry run: report what would change
  python tools/generate_arthedain_troops.py --apply    # write both files
  python tools/generate_arthedain_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import ab, one, paired, same  # noqa: E402,F401  used by build()

CULTURE = "arthedain"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Dunadan Youth (VolunteerRecruitmentService.Arthedain.cs, weight 2 against the Levy's 6).
MERC_SOURCE = "arthedain_dunadan_youth"
MILITIA = {"arthedain_militia_spearman": "arthedain_militia_veteran_spearman",
           "arthedain_militia_archer": "arthedain_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[Arthedain]", script="generate_arthedain_troops.py",
                 pt_marker="TAOM-ARTHEDAIN-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Dunadan Sellsword")

SKILLS = rt.SKILLS
MILITIA_ELITE_BONUS = rt.MILITIA_ELITE_BONUS
Troop = functools.partial(rt.Troop, realm=REALM)
armour = functools.partial(rt.armour, REALM)
bow = functools.partial(rt.bow, REALM)


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
                          one("bracer_noble_heavy_a"), ab("pauld_cape_inf_elite")),
                   ["arthedain_evendim_warden"]))
    # The Wardens of Evendim trade the shield for a two-handed polearm. Role spear, not twohand: Item0 is the
    # weapon in hand at spawn, and the engine reads a TwoHandedPolearm with the Polearm skill, so the poleaxe
    # fights at the primary curve. The bastard sidearm has a shieldless one-handed usage (OneHanded 225, the
    # OneHandedBastardSwordAlternative description) and a two-handed one (TwoHanded 90); which the AI draws is native.
    t.append(Troop("arthedain_evendim_warden", "Evendim Warden", 36, "Infantry", "spear",
                   paired("numenorean_poleaxe_heavy", ("numenorean_bastard_elite_a", "numenorean_bastard_elite_b")),
                   armour(ab("helmet_warden_elite"), ab("chest_noble_heavy"), one("grvs_noble_elite_a"),
                          one("bracer_noble_elite_a"), ab("pauld_cape_inf_elite"))))

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
                          one("bracer_noble_heavy_a"), ab("cape")),
                   ["arthedain_steel_bowman"]))
    # The ladder's tier 7 bow: the one Dunadan archer who wears the elite kit.
    t.append(Troop("arthedain_steel_bowman", "Dunedain Steel Bowman", 36, "Ranged", "ranged",
                   same(bow(36), ARROWS, ARROWS, "numenorean_bastard_elite_b"),
                   armour(ab("helmet_noble_elite"), ab("chest_noble_heavy"), one("grvs_noble_elite_a"),
                          one("bracer_noble_elite_a"), ab("cape"))))

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
                   ["arthedain_knight_of_annuminas"], horse=HORSE_HEAVY))
    # Arnor's answer to the Swan Knights: the Lancer's lance and shield in the elite kit.
    t.append(Troop("arthedain_knight_of_annuminas", "Knight of Annuminas", 31, "Cavalry", "cavalry",
                   paired(LANCE, SHIELD_HIGH, ("numenorean_bastard_elite_a", "numenorean_bastard_elite_b")),
                   armour(ab("helmet_cav_heavy"), ab("chest_noble_heavy"), one("grvs_noble_elite_a"),
                          one("bracer_noble_elite_a"), ab("pauld_noble_elite")),
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


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the men's realms' target in
# tools/rebalance_party_template_maxes.py. It shipped at 83 ("the realm its neighbours carve up") and was
# raised on 2026-10-18 because a 200-day balance simulation had Arthedain losing 66 % of its men, the
# most of any free realm; the mins are unchanged. max_value is a spawn ceiling, not a party size
# (docs/reference/party-template-sizing.md). This generator owns these numbers:
# rebalance_party_template_maxes.py has no arthedain target, and giving it one would set two writers on
# the TAOM-ARTHEDAIN-PT region.
def party_templates():
    p = "arthedain_"
    return [
        ("kingdom_hero_party_arthedain_template", [
            (p + "levy", 3, 26), (p + "spearman", 2, 22), (p + "dunadan_youth", 1, 19),
            (p + "footman", 1, 19), (p + "ranger_of_the_north", 1, 19), (p + "squire", 0, 13),
            (p + "shieldman", 0, 15), (p + "ranger", 0, 15), (p + "outrider", 0, 13),
            (p + "citadel_guard", 0, 10), (p + "fornost_guard", 0, 8), (p + "veteran_ranger", 0, 8),
            (p + "lancer", 0, 8), (p + "veteran_citadel_guard", 0, 5)]),
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


clamp_skills = functools.partial(rt.clamp_skills, REALM)
add_mercenary = functools.partial(rt.add_mercenary, REALM)
self_check = functools.partial(rt.self_check, party_templates)
troops_xml = functools.partial(rt.troops_xml, REALM)
templates_xml = functools.partial(rt.templates_xml, party_templates)


def main():
    return rt.run(REALM, build, party_templates)


if __name__ == "__main__":
    sys.exit(main())
