#!/usr/bin/env python3
"""Generate the South Rhovanion troop tree and its nine soldier party templates (#1062).

Writes:
  Main/_Module/ModuleData/troops/troops_southrhovanion.xml       (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-SOUTHRHOVANION-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: tools/promote_borrowed_cultures.py clones
them from Rohan with the culture's townsfolk, as for the Arnor realms.

Design (Mike, 2026-10-29). South Rhovanion is the plains south of Mirkwood: the Northmen horse-lords
who are kin to the Eothéod and have held the grass against the Wainriders.
A middling, neutral horse realm, scaffolded from Rohan's kit:

  Plains Levy (basic, L6) -> Plainsman Spearman (L11) -> Ford Spearman (L16) -> Ford Guard
      -> Veteran Ford Guard (L26)                                      spear + shield
  Plains Levy also -> Plains Bowman (L16) -> Southwood Archer -> Southwood Marksman (L26)
  Northman Youth (elite basic, L11) splits two ways:
      Riders:        Rhovanion Rider (L16) -> Rhovanion Lancer -> Horse-thane -> Rider-captain
                     -> Horse-lord of Rhovanion (L36, the capstone, mounted)
      Horse-archers: Plains Horse-scout (L16) -> Rhovanion Horse-archer -> Veteran Horse-archer (L26)
  Militia spearman / archer (L11) -> veteran militia (L16)

Every horse wears a harness. The mounts and their harnesses are Rohan's own ladder (troops_rohan.xml,
the Wold, Westemnet and Eastemnet lines), by level: noble Battania at L16, noble northern at L21, t3
Empire at L26, t3 Aserai at L31, noble southern with the scalemail harness at L36. The youth is on foot.

Armour is a PLACEHOLDER from Rohan's kit (cts_rohan_*, rohan_militia_*, roh_nbl_*), until the realm has
its own. Rohan's ids carry no tier keyword, so ARMOUR_MESH_TIER_LADDER cannot check them; the guard is
instead a test (tools/tests/test_realm_troops.py, RohanRealmTests): a Rohan piece is never worn here
below the lowest level at which a Rohan troop wears it, so the armour class table does not move.

Weapons are Rohan's, placed by sustained DPS against the melee ladder (tools/melee_damage.py,
2026-10-29), the band being tools/melee_ladders.json's tier target plus or minus 25 percent. Rohan has
no kingdom offset of its own (vlandia 0.0), and this realm has none. The Armory's wm_rohan_* weapons
top out at 47 DPS, which is the tier 3 and 4 band; the L26+ tiers (band from 52) take the vanilla
vlandia_* and sturgia_* blades Rohan's own troops already carry:
  wm_rohan_spear_b 27, ws_spear_a01 34, ws_sword_a01 34, ws_sword_a02 43, ws_spear_a02 47,
  vlandia_sword_1_t2 51, sturgia_sword_2_t3 54, vlandia_sword_2_t3 60, vlandia_sword_5_t5 67,
  vlandia_axe_2_t4 68, vlandia_noble_sword_1_t5 68, eastern_spear_4_t4 38.
The validator prices only the Armory, so from L26 a troop carries no wm_rohan_* melee that would read
as "under" beside the vanilla blade that actually sets its output. Bows are the Rohan ladder's items
(tools/ranged_ladders.json: the `rohan` line claims troops_southrhovanion.xml), with the skill from
tools/ranged_ladder.py; the line has a cell for every engine tier, so every archer level is free.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md). The
machinery is tools/realm_troops.py, shared with the Arnor realms.

Usage:
  python tools/generate_southrhovanion_troops.py            # dry run: report what would change
  python tools/generate_southrhovanion_troops.py --apply    # write both files
  python tools/generate_southrhovanion_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import one, paired, same  # noqa: E402

CULTURE = "southrhovanion"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Northman Youth, the elite basic troop (weight 2 against the Levy's 6).
MERC_SOURCE = "southrhovanion_northman_youth"
MILITIA = {"southrhovanion_militia_spearman": "southrhovanion_militia_veteran_spearman",
           "southrhovanion_militia_archer": "southrhovanion_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[South Rhovanion]", script="generate_southrhovanion_troops.py",
                 pt_marker="TAOM-SOUTHRHOVANION-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Rhovanion Sellsword", armour_prefix="",
                 body="fighter_rohan", ladder_line="rohan")

SKILLS = rt.SKILLS
MILITIA_ELITE_BONUS = rt.MILITIA_ELITE_BONUS
Troop = functools.partial(rt.Troop, realm=REALM)
armour = functools.partial(rt.armour, REALM)
bow = functools.partial(rt.bow, REALM)

# Rohan melee by sustained DPS (tools/melee_damage.py, 2026-10-29). Tier bands (no kingdom offset):
# t1 26-42, t2 32-54, t3 39-65, t4 45-75, t5 52-86, t6 58-98, t7 65-109.
SPEAR_L6 = "wm_rohan_spear_b"            # 27
SPEAR_L11 = "wm_rohan_ws_spear_a01"      # 34
SPEAR_L16 = "wm_rohan_ws_spear_a02"      # 47
SPEAR_VANILLA = "eastern_spear_4_t4"     # 38, vanilla: the spear of every rank from L26
SWORD_L6 = "wm_rohan_ws_sword_a01"       # 34
SWORD_L11 = "wm_rohan_ws_sword_a02"      # 43
SWORD_L16 = "vlandia_sword_1_t2"         # 51
SWORD_L21 = "sturgia_sword_2_t3"         # 54
SWORD_L26 = "vlandia_sword_2_t3"         # 60
SWORD_L31 = "vlandia_sword_5_t5"         # 67
SWORD_L36 = "vlandia_noble_sword_1_t5"   # 68
AXE_HIGH = "vlandia_axe_2_t4"            # 68
JAVELIN = "eastern_javelin_3_t4"
SHIELD_LOW = ("wm_rohan_shield_a01_gsw", "wm_rohan_shield_a01_rbw")
SHIELD_MID = ("wm_rohan_shield_a02_gsw", "wm_rohan_shield_a02_rbw")
SHIELD_HIGH = ("wm_rohan_shield_a03_gsw", "wm_rohan_shield_a03_ybw")
SHIELD_TOP = ("wm_rohan_shield_a03_rgw", "wm_rohan_shield_b01_gbw")
ARROWS_A = "bodkin_arrows_a"
ARROWS_B = "bodkin_arrows_b"
PIERCING = "piercing_arrows"

# Rohan's mount ladder (troops_rohan.xml), horse and harness by level.
HORSE_16 = ("noble_horse_battania", "rohan_horse_armor_three")
HORSE_21 = ("noble_horse_northern", "rohan_horse_armor_three")
HORSE_26 = ("t3_empire_horse", "rohan_horse_armor_one")
HORSE_31 = ("t3_aserai_horse", "rohan_horse_armor_four")
HORSE_36 = ("noble_horse_southern", "rohan_horse_armor_scalemail")

# Rohan armour, with the lowest level a Rohan troop wears each piece (the level it must not be worn
# below, as tools/derive_armor_tiers.py reads it):
#   head  helmet1a/1b/4b/4d 6; helmet1/2a/4a 11; helmet1c/2/3a/4/4c 16; helmet3 and royal_guard_helmet_tail 21;
#         roh_nbl_hlm_cf 26; whiterun_helm 31; helmet_captain/captain1 36
#   body  militia_tunic_a/b/c 6; militia_armour_a/b/d/g 11; armor3/4/444/4444 and militia_armour_f 16;
#         armor1/111/1111/22/2222/3333 21; armor2/222, roh_nbl_arm_1/b/g 26; royal_guard_armor 31;
#         armor_captain/captain1 36
#   arm   armguard1/4 11; armguard2, captain_bracer 16; armguard3, roh_nbl_glv 26; royal_guard_gloves 31
#   leg   boots3 6; boots4, roh_sol_bts 11; boots1 16; captain_boots, royal_guard_boots 21; roh_nbl_bts 26;
#         captain_boots1 31
#   cape  militia_pauldron_both 6; cape3 11; roha_cape1, cape2, cape4 16; royal_guard_shoulder_armor 31;
#         roh_nbl_shldr_cgs_2 36
# Every piece id is a full id: the realm's armour prefix is empty.
ROYAL_HELM = "cts_cts_rohan_royal_guard_helmet_tail"


def build():
    t = []

    # ---- Levy and the spear line ----
    t.append(Troop("southrhovanion_levy", "Plains Levy", 6, "Infantry", "spear",
                   paired(SPEAR_L6, SHIELD_LOW, SWORD_L6),
                   armour(None, ("rohan_militia_tunic_a", "rohan_militia_tunic_b"), one("cts_rohan_boots3"),
                          None, one("rohan_militia_pauldron_both")),
                   ["southrhovanion_spearman", "southrhovanion_plains_bowman"], basic=True))
    t.append(Troop("southrhovanion_spearman", "Plainsman Spearman", 11, "Infantry", "spear",
                   paired(SPEAR_L11, SHIELD_LOW, SWORD_L11),
                   armour(("cts_rohan_helmet4a", "cts_rohan_helmet1"),
                          ("rohan_militia_armour_a", "rohan_militia_armour_b"), one("cts_rohan_boots4"),
                          ("cts_rohan_armguard4", "cts_rohan_armguard1"), one("rohan_militia_pauldron_both")),
                   ["southrhovanion_ford_spearman"]))
    t.append(Troop("southrhovanion_ford_spearman", "Ford Spearman", 16, "Infantry", "spear",
                   paired(SPEAR_L16, SHIELD_MID, SWORD_L11),
                   armour(("cts_rohan_helmet4c", "cts_rohan_helmet4"), ("cts_rohan_armor4", "cts_rohan_armor444"),
                          ("cts_rohan_boots4", "cts_rohan_boots1"),
                          ("cts_rohan_armguard2", "cts_rohan_captain_bracer"),
                          ("cts_rohan_cape4", "cts_rohan_cape2")),
                   ["southrhovanion_ford_guard"]))
    t.append(Troop("southrhovanion_ford_guard", "Ford Guard", 21, "Infantry", "spear",
                   paired(SPEAR_L16, SHIELD_HIGH, SWORD_L16),
                   armour(("cts_rohan_helmet3", ROYAL_HELM), ("cts_rohan_armor1", "cts_rohan_armor111"),
                          ("cts_rohan_boots1", "cts_rohan_royal_guard_boots"),
                          ("cts_rohan_armguard2", "cts_rohan_armguard1"),
                          ("cts_rohan_cape4", "cts_roha_cape1")),
                   ["southrhovanion_veteran_ford_guard"]))
    # From L26 the melee kit is vanilla only: no wm_rohan_* blade below the tier 5 band beside it.
    t.append(Troop("southrhovanion_veteran_ford_guard", "Veteran Ford Guard", 26, "Infantry", "spear",
                   paired(SPEAR_VANILLA, SHIELD_TOP, AXE_HIGH),
                   armour(("cts_rohan_helmet3", "roh_nbl_hlm_cf"), ("cts_rohan_armor2", "cts_rohan_armor222"),
                          ("cts_rohan_boots1", "roh_nbl_bts"), ("cts_rohan_armguard3", "roh_nbl_glv"),
                          ("cts_rohan_cape2", "cts_roha_cape1"))))

    # ---- Foot bowmen: bow in Item0 and arrows in Item1/Item2 in BOTH rosters, sword in Item3 ----
    t.append(Troop("southrhovanion_plains_bowman", "Plains Bowman", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS_A, ARROWS_B, SWORD_L16),
                   armour(("cts_rohan_helmet1c", "cts_rohan_helmet2"), ("cts_rohan_armor4444", "cts_rohan_armor4"),
                          one("cts_rohan_boots4"), ("cts_rohan_armguard2", "cts_rohan_armguard4"),
                          one("cts_rohan_cape4")),
                   ["southrhovanion_southwood_archer"]))
    t.append(Troop("southrhovanion_southwood_archer", "Southwood Archer", 21, "Ranged", "ranged",
                   same(bow(21), PIERCING, PIERCING, SWORD_L21),
                   armour(("cts_rohan_helmet1c", "cts_rohan_helmet3"),
                          ("cts_rohan_armor1111", "cts_rohan_armor111"), one("cts_rohan_boots4"),
                          ("cts_rohan_armguard1", "cts_rohan_armguard2"), one("cts_rohan_cape4")),
                   ["southrhovanion_southwood_marksman"]))
    t.append(Troop("southrhovanion_southwood_marksman", "Southwood Marksman", 26, "Ranged", "ranged",
                   same(bow(26), PIERCING, PIERCING, SWORD_L26),
                   armour(("cts_rohan_helmet3", ROYAL_HELM), ("cts_rohan_armor2222", "cts_rohan_armor222"),
                          ("cts_rohan_boots1", "roh_nbl_bts"), ("cts_rohan_armguard3", "roh_nbl_glv"),
                          ("cts_rohan_cape2", "cts_roha_cape1"))))

    # ---- Northman Youth, the two-way split: he is on foot, the horse starts at level 16 ----
    t.append(Troop("southrhovanion_northman_youth", "Northman Youth", 11, "Infantry", "sword",
                   paired(SWORD_L11, SHIELD_MID, SPEAR_L11),
                   armour(one("cts_rohan_helmet2a"), ("rohan_militia_armour_b", "rohan_militia_armour_d"),
                          one("cts_rohan_boots4"), one("cts_rohan_armguard4"), one("rohan_militia_pauldron_both")),
                   ["southrhovanion_rider", "southrhovanion_horse_scout"], basic=True))

    # Riders: the horse realm's long cavalry line, ending in the mounted capstone.
    t.append(Troop("southrhovanion_rider", "Rhovanion Rider", 16, "Cavalry", "cavalry",
                   paired(SPEAR_L16, SHIELD_MID, SWORD_L11),
                   armour(("cts_rohan_helmet2", "cts_rohan_helmet3a"), ("cts_rohan_armor4444", "cts_rohan_armor3"),
                          one("cts_rohan_boots4"), ("cts_rohan_armguard2", "cts_rohan_captain_bracer"),
                          one("cts_rohan_cape4")),
                   ["southrhovanion_lancer"], horse=HORSE_16))
    t.append(Troop("southrhovanion_lancer", "Rhovanion Lancer", 21, "Cavalry", "cavalry",
                   paired(SPEAR_L16, SHIELD_HIGH, SWORD_L16),
                   armour(("cts_rohan_helmet3a", "cts_rohan_helmet3"), ("cts_rohan_armor1", "cts_rohan_armor3333"),
                          ("cts_rohan_boots4", "cts_rohan_captain_boots"),
                          ("cts_rohan_armguard2", "cts_rohan_captain_bracer"), one("cts_rohan_cape4")),
                   ["southrhovanion_horse_thane"], horse=HORSE_21))
    t.append(Troop("southrhovanion_horse_thane", "Horse-thane", 26, "Cavalry", "cavalry",
                   paired(SPEAR_VANILLA, SHIELD_TOP, AXE_HIGH, JAVELIN),
                   armour(("cts_rohan_helmet3", "roh_nbl_hlm_cf"), ("cts_rohan_armor2", "roh_nbl_arm_g"),
                          ("cts_rohan_boots1", "roh_nbl_bts"), ("cts_rohan_armguard3", "roh_nbl_glv"),
                          ("cts_rohan_cape2", "cts_roha_cape1")),
                   ["southrhovanion_rider_captain"], horse=HORSE_26))
    t.append(Troop("southrhovanion_rider_captain", "Rider-captain", 31, "Cavalry", "cavalry",
                   paired(SPEAR_VANILLA, SHIELD_TOP, SWORD_L31, JAVELIN),
                   armour((ROYAL_HELM, "whiterun_helm"), one("cts_rohan_royal_guard_armor"),
                          one("cts_rohan_captain_boots1"), one("cts_rohan_royal_guard_gloves"),
                          one("cts_rohan_royal_guard_shoulder_armor")),
                   ["southrhovanion_horse_lord"], horse=HORSE_31))
    t.append(Troop("southrhovanion_horse_lord", "Horse-lord of Rhovanion", 36, "Cavalry", "cavalry",
                   paired(SPEAR_VANILLA, SHIELD_TOP, SWORD_L36, JAVELIN),
                   armour(("cts_rohan_helmet_captain1", "cts_rohan_helmet_captain"),
                          ("cts_rohan_armor_captain1", "cts_rohan_armor_captain"),
                          one("cts_rohan_captain_boots1"), one("cts_rohan_royal_guard_gloves"),
                          one("roh_nbl_shldr_cgs_2")),
                   horse=HORSE_36))

    # Horse-archers: bow in Item0, arrows in Item1, a shield in Item2 and a sword in Item3, both rosters.
    t.append(Troop("southrhovanion_horse_scout", "Plains Horse-scout", 16, "HorseArcher", "horsearcher",
                   paired(bow(16), ARROWS_B, SHIELD_LOW, SWORD_L16),
                   armour(("cts_rohan_helmet2", "cts_rohan_helmet1c"), ("cts_rohan_armor4444", "cts_rohan_armor444"),
                          one("cts_rohan_boots4"), one("cts_rohan_armguard2"), one("cts_rohan_cape4")),
                   ["southrhovanion_horse_archer"], horse=HORSE_16))
    t.append(Troop("southrhovanion_horse_archer", "Rhovanion Horse-archer", 21, "HorseArcher", "horsearcher",
                   paired(bow(21), PIERCING, SHIELD_MID, SWORD_L21),
                   armour(("cts_rohan_helmet3a", "cts_rohan_helmet3"), ("cts_rohan_armor1111", "cts_rohan_armor1"),
                          one("cts_rohan_boots4"), ("cts_rohan_armguard2", "cts_rohan_armguard1"),
                          one("cts_rohan_cape4")),
                   ["southrhovanion_veteran_horse_archer"], horse=HORSE_21))
    t.append(Troop("southrhovanion_veteran_horse_archer", "Veteran Horse-archer", 26, "HorseArcher", "horsearcher",
                   paired(bow(26), PIERCING, SHIELD_HIGH, SWORD_L26),
                   armour(("cts_rohan_helmet3", "roh_nbl_hlm_cf"), ("cts_rohan_armor2222", "cts_rohan_armor2"),
                          ("cts_rohan_boots1", "roh_nbl_bts"), ("cts_rohan_armguard3", "roh_nbl_glv"),
                          ("cts_rohan_cape2", "cts_roha_cape1")),
                   horse=HORSE_26))

    # ---- Militia, named by the culture's four militia attributes (level 11 and 16, as Rohan's) ----
    t.append(Troop("southrhovanion_militia_spearman", "Militia Spearman", 11, "Infantry", "spear",
                   paired(SPEAR_L11, SHIELD_LOW, SWORD_L6),
                   armour(None, ("rohan_militia_armour_a", "rohan_militia_armour_b"),
                          ("cts_rohan_boots3", "cts_rohan_boots4"), None, None),
                   ["southrhovanion_militia_veteran_spearman"]))
    t.append(Troop("southrhovanion_militia_archer", "Militia Archer", 11, "Ranged", "ranged",
                   same(bow(11), "default_arrows", SWORD_L16),
                   armour(None, ("rohan_militia_tunic_a", "rohan_militia_tunic_b"),
                          ("cts_rohan_boots3", "cts_rohan_boots4"), None, None),
                   ["southrhovanion_militia_veteran_archer"]))
    t.append(Troop("southrhovanion_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(SPEAR_L16, SHIELD_MID, SWORD_L11),
                   armour(("cts_rohan_helmet1", "cts_rohan_helmet2"),
                          ("cts_rohan_armor4", "rohan_militia_armour_f"), one("cts_rohan_boots4"),
                          ("cts_rohan_armguard4", "cts_rohan_armguard2"), one("rohan_militia_pauldron_both"))))
    t.append(Troop("southrhovanion_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS_B, SWORD_L26),
                   armour(("cts_rohan_helmet1", "cts_rohan_helmet2"),
                          ("cts_rohan_armor4", "rohan_militia_armour_f"), one("cts_rohan_boots4"),
                          ("cts_rohan_armguard4", "cts_rohan_armguard2"), None)))
    return t


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the men's realms' target in
# tools/rebalance_party_template_maxes.py (as Cardolan's); this generator owns them.
def party_templates():
    p = "southrhovanion_"
    return [
        ("kingdom_hero_party_southrhovanion_template", [
            (p + "levy", 3, 26), (p + "spearman", 2, 22), (p + "northman_youth", 1, 19),
            (p + "rider", 1, 19), (p + "plains_bowman", 1, 19), (p + "ford_spearman", 0, 15),
            (p + "horse_scout", 0, 15), (p + "southwood_archer", 0, 13), (p + "lancer", 0, 13),
            (p + "ford_guard", 0, 10), (p + "horse_thane", 0, 8), (p + "horse_archer", 0, 8),
            (p + "southwood_marksman", 0, 8), (p + "rider_captain", 0, 5)]),
        ("kingdom_hero_party_mercenary_southrhovanion_template", [
            (p + "spearman", 8, 16), (p + "plains_bowman", 4, 8), (p + "ford_spearman", 4, 8),
            (p + "southwood_archer", 2, 4), (p + "rider", 2, 4)]),
        ("kingdom_hero_party_outlaw_southrhovanion_template", [
            (p + "levy", 8, 16), (p + "militia_spearman", 4, 8), (p + "militia_archer", 4, 8),
            (p + "plains_bowman", 2, 4)]),
        ("militia_southrhovanion_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_southrhovanion_template_level_1", [
            (p + "levy", 6, 6), (p + "spearman", 3, 3), (p + "plains_bowman", 2, 2)]),
        ("patrol_party_southrhovanion_template_level_2", [
            (p + "spearman", 5, 5), (p + "ford_spearman", 4, 4), (p + "southwood_archer", 3, 3),
            (p + "rider", 2, 2)]),
        ("patrol_party_southrhovanion_template_level_3", [
            (p + "ford_guard", 5, 5), (p + "veteran_ford_guard", 3, 3), (p + "southwood_marksman", 3, 3),
            (p + "lancer", 2, 2), (p + "horse_thane", 1, 1)]),
        ("rebels_southrhovanion_template", [
            (p + "levy", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_southrhovanion", [
            (p + "horse_lord", 1, 1), (p + "rider_captain", 3, 3), (p + "horse_thane", 2, 2)]),
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
