#!/usr/bin/env python3
"""Generate the Ered Luin troop tree and its seven soldier party templates.

Writes:
  Main/_Module/ModuleData/troops/troops_eredluin.xml            (whole file, generated)
  Main/_Module/ModuleData/taom_partyTemplates.xml               (one marker region,
                                                                  <!-- TAOM-EREDLUIN-PT:BEGIN/END -->)

The villager and caravan templates are NOT written here: tools/promote_borrowed_cultures.py clones
them from Erebor with the culture's townsfolk, as for the Arnor realms.

Design (2026-10-27). Ered Luin is the dwarf kingdom of the Blue Mountains: Durin's Folk of Thorin's
Halls, and in TAOM the Broadbeams of Belegost and the Firebeards of Nogrod, whose old cities stand
again. A small realm of good soldiers, on foot like every dwarf host:

  Delver (basic, L6) -> Axeman (L11) -> Hearth-guard -> Hall Warden -> Veteran Hall Warden
      -> Warden of Thorin's Halls (L31)                      axe + shield
  Axeman also -> Mattock Hewer (L16) -> Mattock Warrior -> Veteran Mattock Warrior (L26), two-handed,
      each with a throwing axe in Item1
  Delver also -> Bowman (L11) -> Veteran Bowman -> Blue Mountain Bowman (L21)
  Noble (elite basic, L11) splits two ways:
      Broadbeams: Broadbeam Guard (L16) -> Broadbeam Shield-guard -> Gatewarden of Belegost
                  -> Ironbound Warden -> King's Guard of Thorin's Halls (L36, the capstone)
      Firebeards: Firebeard Bowman (L16) -> Firebeard Arbalester (L21, crossbow) -> Firebeard
                  Marksman -> Master Arbalester of Nogrod (L31)
  Militia spearman / archer (L11) -> veteran militia (L16)

No troop rides: the dwarf rider bone is misaligned (MOUNTED_DWARF), and Ered Luin has no war rams.

Armour is a PLACEHOLDER from the dwarf kit in the Armory: the Iron Hills pieces (sk_dwarf_iron_*) and
Erebor's (sk_dwarf_erebor_*), placed by the id tier keyword so ARMOUR_MESH_TIER_LADDER holds. The
upper ranks wear the Iron Hills elite helmets, chests and pauldrons no other troop wears, so Ered
Luin's best do not look like Erebor's. A piece another kingdom already wears is worn here only at
or above that kingdom's lowest wearer's band (rebalance_armor.level_to_band): the class table and
the restat band a piece by its lowest wearer, so an Ered Luin level-26 troop in a piece Erebor
wears from level 31 moves Erebor's piece from elite to heavy (still gated, but sold one armoury
level lower), and a level-16 troop moves one to medium, out of the armoury gate (deep review XML-1).

Weapons follow Erebor's per-level picks; `eredluin` has its own kingdom offset in
tools/melee_ladders.json. Bows and crossbows are Erebor's ladder items (tools/ranged_ladders.json:
the `erebor` line claims troops_eredluin.xml), with the skill from tools/ranged_ladder.py.

Every troop's two battle rosters are interchangeable slot by slot (.claude/rules/troops.md). The
machinery is tools/realm_troops.py, shared with the Arnor realms.

Usage:
  python tools/generate_eredluin_troops.py            # dry run: report what would change
  python tools/generate_eredluin_troops.py --apply    # write both files
  python tools/generate_eredluin_troops.py --check    # exit 1 if the files differ from a fresh run
"""
import functools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import realm_troops as rt  # noqa: E402  the shared machinery
from realm_troops import ab, one, paired, same  # noqa: E402

CULTURE = "eredluin"
# The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests): the
# Noble (VolunteerRecruitmentService.EredLuin.cs, weight 2 against the Delver's 6).
MERC_SOURCE = "eredluin_noble"
MILITIA = {"eredluin_militia_spearman": "eredluin_militia_veteran_spearman",
           "eredluin_militia_archer": "eredluin_militia_veteran_archer"}
REALM = rt.Realm(culture=CULTURE, label="[Ered Luin]", script="generate_eredluin_troops.py",
                 pt_marker="TAOM-EREDLUIN-PT", militia=MILITIA, merc_source=MERC_SOURCE,
                 merc_name="Hired Noble", armour_prefix="sk_dwarf_", race="dwarf",
                 body="fighter_erebor", ladder_line="erebor")

SKILLS = rt.SKILLS
MILITIA_ELITE_BONUS = rt.MILITIA_ELITE_BONUS
Troop = functools.partial(rt.Troop, realm=REALM)
armour = functools.partial(rt.armour, REALM)
bow = functools.partial(rt.bow, REALM)
crossbow = functools.partial(rt.crossbow, REALM)

# One-handed axes by sustained DPS (tools/melee_damage.py, 2026-10-27): c 38, d 43, a and b 45, e 50,
# a2 63, b2 84. The Delver's band at tier 1 ends at 44.
AXE_RECRUIT = ("sm_dwarf_erebor_1h_axe_c", "sm_dwarf_erebor_1h_axe_d")
AXE_LOW = ("sm_dwarf_erebor_1h_axe_b", "sm_dwarf_erebor_1h_axe_a")
AXE_MID = ("sm_dwarf_erebor_1h_axe_a2", "sm_dwarf_erebor_1h_axe_a2")
AXE_HIGH = ("sm_dwarf_erebor_1h_axe_b2", "sm_dwarf_erebor_1h_axe_b2")
SPEAR = "sm_dwarf_erebor_spear_a"
SPEAR_B = "sm_dwarf_erebor_spear_b"
SHIELD_LEATHER = ("sm_dwarf_erebor_shield_leather_a", "sm_dwarf_erebor_shield_leather_b")
SHIELD_LEATHER_HIGH = ("sm_dwarf_erebor_shield_leather_c", "sm_dwarf_erebor_shield_leather_d")
SHIELD_METAL_LOW = ("sm_dwarf_erebor_shield_metal_a", "sm_dwarf_erebor_shield_metal_a2")
SHIELD_METAL_MID = ("sm_dwarf_erebor_shield_metal_b", "sm_dwarf_erebor_shield_metal_b2")
SHIELD_METAL_HIGH = ("sm_dwarf_erebor_shield_metal_c", "sm_dwarf_erebor_shield_metal_c2")
SHIELD_METAL_TOP = ("sm_dwarf_erebor_shield_metal_d", "sm_dwarf_erebor_shield_metal_d2")
TOWER_LOW = ("sm_dwarf_erebor_shield_tower_a", "sm_dwarf_erebor_shield_tower_b")
TOWER_MID = ("sm_dwarf_erebor_shield_tower_c", "sm_dwarf_erebor_shield_tower_d")
TOWER_HIGH = ("sm_iron_shield_tower_a", "sm_iron_shield_tower_b")
TOWER_TOP = ("sm_iron_shield_tower_c", "sm_dwarf_erebor_shield_tower_e")
ARROWS = "sk_dwarf_erebor_arrow_a"
ARROWS_B = "sk_dwarf_erebor_arrow_b"
BOLTS = "sm_dwarf_iron_bolt_a"
# Vanilla's Tribesman Throwing Axe (class ThrowingAxe), the one throwing weapon Erebor's troops carry. The
# mattock line from the Mattock Hewer (L16) up throws it from Item1, and the two-handed career kit takes
# it from the Hewer (generate_career_kits.py, #1045).
THROWING_AXE = "southern_throwing_axe_1_t4"


def build():
    t = []

    # ---- Delver and the axe line ----
    t.append(Troop("eredluin_delver", "Delver", 6, "Infantry", "sword",
                   paired(AXE_RECRUIT, SHIELD_LEATHER),
                   armour(("iron_helmet_light_a1", "iron_helmet_light_a2"),
                          ab("iron_chest_light"), one("iron_boots_light_a"), None, None),
                   ["eredluin_axeman", "eredluin_bowman"], basic=True))
    t.append(Troop("eredluin_axeman", "Axeman", 11, "Infantry", "sword",
                   paired(AXE_LOW, SHIELD_LEATHER),
                   armour(ab("erebor_helmet_leather_light"), ab("erebor_chest_leather_light"),
                          ab("erebor_boots_light"), one("iron_gloves_a"), None),
                   ["eredluin_hearthguard", "eredluin_hewer"]))
    t.append(Troop("eredluin_hearthguard", "Hearth-guard", 16, "Infantry", "sword",
                   paired(AXE_LOW, SHIELD_METAL_LOW),
                   armour(ab("erebor_helmet_plate_light"), ("erebor_chest_chain_a", "erebor_chest_scale_a"),
                          ab("erebor_boots_med"), ab("erebor_bracers_med"),
                          ("iron_pauldron_light_a", "erebor_pauldron_scale_a")),
                   ["eredluin_hall_warden"]))
    t.append(Troop("eredluin_hall_warden", "Hall Warden", 21, "Infantry", "sword",
                   paired(AXE_MID, SHIELD_METAL_MID, SPEAR_B),
                   armour(("iron_helmet_med_c1", "iron_helmet_med_c2"), one("iron_chest_scale_a"),
                          ("erebor_boots_med_d", "erebor_boots_med_e"), ("iron_bracer_med_b1", "iron_bracer_med_b2"),
                          ("iron_pauldron_medium_b", "iron_pauldron_medium_d")),
                   ["eredluin_veteran_hall_warden"]))
    t.append(Troop("eredluin_veteran_hall_warden", "Veteran Hall Warden", 26, "Infantry", "sword",
                   paired(AXE_MID, SHIELD_METAL_HIGH, SPEAR_B),
                   armour(("iron_helmet_heavy_a1", "iron_helmet_heavy_b1"), ab("iron_chest_heavy"),
                          ("iron_boots_heavy_a1", "iron_boots_heavy_a2"), ("iron_bracer_heavy_c6", "iron_bracer_heavy_c7"),
                          one("iron_pauldron_heavy_cape_a")),
                   ["eredluin_halls_warden"]))
    t.append(Troop("eredluin_halls_warden", "Warden of Thorin's Halls", 31, "Infantry", "sword",
                   paired(AXE_HIGH, SHIELD_METAL_TOP, SPEAR),
                   armour(("iron_helmet_elite_a3", "iron_helmet_elite_a4"), ab("iron_chest_elite"),
                          ("iron_boots_elite_b1", "iron_boots_elite_b3"), one("iron_bracer_elite_c4"),
                          ("iron_pauldron_elite_a", "iron_pauldron_elite_cape_a"))))

    # ---- The mattock line: two-handed axes and mattocks, a throwing axe in Item1, no shield ----
    t.append(Troop("eredluin_hewer", "Mattock Hewer", 16, "Infantry", "twohand",
                   paired(("sm_dwarf_erebor_axe_2h_a", "sm_dwarf_iron_axe_b"), THROWING_AXE),
                   armour(ab("erebor_helmet_leather_med"), one("iron_chest_chain_a"),
                          ("iron_boots_med_a1", "iron_boots_med_c1"), ("iron_bracer_light_b1", "iron_bracer_light_c1"),
                          ("iron_pauldron_med_c", "erebor_pauldron_scale_a")),
                   ["eredluin_mattock_warrior"]))
    t.append(Troop("eredluin_mattock_warrior", "Mattock Warrior", 21, "Infantry", "twohand",
                   same("sm_dwarf_erebor_axe_2h_b", THROWING_AXE),
                   armour(("iron_helmet_med_d1", "iron_helmet_med_d2"), one("iron_chest_scale_a"),
                          one("iron_boots_med_b1"), ("iron_bracer_med_a1", "iron_bracer_med_b1"),
                          ("iron_pauldron_medium_b", "iron_pauldron_medium_d")),
                   ["eredluin_veteran_mattock_warrior"]))
    t.append(Troop("eredluin_veteran_mattock_warrior", "Veteran Mattock Warrior", 26, "Infantry", "twohand",
                   paired(("sm_dwarf_iron_mattock_a", "sm_dwarf_iron_mattock_b"), THROWING_AXE),
                   armour(("iron_helmet_heavy_c1", "iron_helmet_heavy_c2"), ("iron_chest_heavy_c", "iron_chest_heavy_d"),
                          ("iron_boots_heavy_b1", "iron_boots_heavy_b2"), ("iron_bracer_heavy_c8", "iron_bracer_heavy_c9"),
                          one("iron_pauldron_heavy_cape_a"))))

    # ---- Bowmen: bow in Item0 and arrows in Item1/Item2 in BOTH rosters, axe in Item3 ----
    t.append(Troop("eredluin_bowman", "Bowman", 11, "Ranged", "ranged",
                   same(bow(11), ARROWS, ARROWS, "sm_dwarf_erebor_1h_axe_b"),
                   armour(ab("erebor_helmet_leather_light"), ab("iron_chest_light"),
                          ab("erebor_boots_light"), None, None),
                   ["eredluin_veteran_bowman"]))
    t.append(Troop("eredluin_veteran_bowman", "Veteran Bowman", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "sm_dwarf_erebor_1h_axe_b"),
                   armour(ab("erebor_helmet_leather_med"), ab("erebor_chest_leather_med"),
                          ab("erebor_boots_med"), ab("erebor_bracers_med"), None),
                   ["eredluin_mountain_bowman"]))
    t.append(Troop("eredluin_mountain_bowman", "Blue Mountain Bowman", 21, "Ranged", "ranged",
                   same(bow(21), ARROWS_B, ARROWS, "sm_dwarf_erebor_1h_axe_a2"),
                   armour(ab("erebor_helmet_leather_med"), ab("erebor_chest_leather_med"),
                          ("erebor_boots_med_c", "erebor_boots_med_d"), one("erebor_gloves_a"),
                          ("iron_pauldron_medium_a", "iron_pauldron_medium_d"))))

    # ---- Noble, the two-way split ----
    t.append(Troop("eredluin_noble", "Noble", 11, "Infantry", "sword",
                   paired(AXE_LOW, SHIELD_METAL_LOW),
                   armour(("iron_helmet_light_a1", "iron_helmet_light_a2"), ab("iron_chest_light"),
                          ("iron_boots_med_a1", "erebor_boots_light_a"), one("iron_gloves_a"), None),
                   ["eredluin_broadbeam_guard", "eredluin_firebeard_bowman"], basic=True))

    # Broadbeams of Belegost: spear and tower shield, ending in the King's Guard.
    t.append(Troop("eredluin_broadbeam_guard", "Broadbeam Guard", 16, "Infantry", "spear",
                   paired(SPEAR, TOWER_LOW, AXE_LOW),
                   armour(("erebor_helmet_plate_light_c", "erebor_helmet_plate_light_d"), one("erebor_chest_scale_a"),
                          ("erebor_boots_med_b", "erebor_boots_med_c"), ("erebor_bracers_med_b", "erebor_bracers_med_c"),
                          ("erebor_pauldron_scale_a", "iron_pauldron_light_a")),
                   ["eredluin_broadbeam_shieldguard"]))
    t.append(Troop("eredluin_broadbeam_shieldguard", "Broadbeam Shield-guard", 21, "Infantry", "spear",
                   paired(SPEAR, TOWER_MID, AXE_MID),
                   armour(ab("erebor_helmet_plate_med"), one("iron_chest_scale_a"),
                          ("erebor_boots_med_e", "erebor_boots_med_f"), ("iron_bracer_med_a1", "erebor_gloves_a"),
                          ab("erebor_pauldron_plate_med")),
                   ["eredluin_gatewarden"]))
    t.append(Troop("eredluin_gatewarden", "Gatewarden of Belegost", 26, "Infantry", "spear",
                   paired(SPEAR_B, TOWER_MID, AXE_MID),
                   armour(ab("erebor_helmet_leather_heavy"),
                          ("iron_chest_heavy_a", "iron_chest_heavy_d"),
                          ("iron_boots_heavy_a1", "iron_boots_heavy_b3"), ("iron_bracer_heavy_c6", "iron_bracer_heavy_c8"),
                          one("iron_pauldron_heavy_cape_a")),
                   ["eredluin_ironbound_warden"]))
    t.append(Troop("eredluin_ironbound_warden", "Ironbound Warden", 31, "Infantry", "sword",
                   paired(AXE_HIGH, TOWER_HIGH, SPEAR),
                   armour(("iron_helmet_elite_b1", "iron_helmet_elite_b2"), ("iron_chest_elite_c", "iron_chest_elite_d"),
                          ("iron_boots_elite_a1", "iron_boots_elite_a3"), ab("erebor_bracers_elite"),
                          ("iron_pauldron_elite_cape_a", "iron_pauldron_elite_cape_b")),
                   ["eredluin_kings_guard"]))
    t.append(Troop("eredluin_kings_guard", "King's Guard of Thorin's Halls", 36, "Infantry", "sword",
                   paired(AXE_HIGH, TOWER_TOP, SPEAR),
                   armour(("iron_helmet_elite_d2", "iron_helmet_elite_d4"), ("iron_chest_elite_c", "iron_chest_elite_d"),
                          ab("erebor_greaves_elite"), ("erebor_bracers_elite_c", "erebor_bracers_elite_d"),
                          ("iron_pauldron_elite_cape_a", "iron_pauldron_elite_cape_b"))))

    # Firebeards of Nogrod: the bow at first, then the heavy crossbows Nogrod's smiths make.
    t.append(Troop("eredluin_firebeard_bowman", "Firebeard Bowman", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "sm_dwarf_erebor_1h_axe_b"),
                   armour(ab("erebor_helmet_leather_med"), ab("erebor_chest_leather_med"),
                          ab("erebor_boots_med"), ab("erebor_bracers_med"), ("iron_pauldron_light_a", "iron_pauldron_med_c")),
                   ["eredluin_firebeard_arbalester"]))
    t.append(Troop("eredluin_firebeard_arbalester", "Firebeard Arbalester", 21, "Ranged", "crossbow",
                   same(crossbow(21), BOLTS, BOLTS, "sm_dwarf_erebor_1h_axe_e"),
                   armour(("iron_helmet_med_d1", "iron_helmet_med_d2"), one("iron_chest_scale_a"),
                          ("erebor_boots_med_c", "erebor_boots_med_d"), ("iron_bracer_med_a1", "iron_bracer_med_a2"),
                          ("iron_pauldron_medium_b", "iron_pauldron_medium_d")),
                   ["eredluin_firebeard_marksman"]))
    t.append(Troop("eredluin_firebeard_marksman", "Firebeard Marksman", 26, "Ranged", "crossbow",
                   same(crossbow(26), BOLTS, BOLTS, "sm_dwarf_erebor_1h_axe_a2"),
                   armour(("iron_helmet_heavy_b1", "iron_helmet_heavy_c1"), ("iron_chest_heavy_b", "iron_chest_heavy_c"),
                          ("iron_boots_heavy_a2", "iron_boots_heavy_c2"), ("iron_bracer_heavy_c7", "iron_bracer_heavy_c9"),
                          one("iron_pauldron_heavy_cape_a")),
                   ["eredluin_firebeard_master"]))
    t.append(Troop("eredluin_firebeard_master", "Master Arbalester of Nogrod", 31, "Ranged", "crossbow",
                   same(crossbow(31), BOLTS, BOLTS, "sm_dwarf_erebor_1h_axe_b2"),
                   armour(("iron_helmet_elite_b3", "iron_helmet_elite_b4"), ("iron_chest_elite_e", "iron_chest_elite_f"),
                          ("iron_boots_elite_c1", "iron_boots_elite_c2"),
                          ("iron_bracer_elite_c1", "iron_bracer_elite_c2"), one("iron_pauldron_elite_a"))))

    # ---- Militia, named by the culture's four militia attributes ----
    t.append(Troop("eredluin_militia_spearman", "Militia Spearman", 11, "Infantry", "spear",
                   paired(SPEAR, SHIELD_LEATHER, "sm_dwarf_erebor_1h_axe_b"),
                   armour(None, ab("erebor_chest_leather_light"), ab("erebor_boots_light"), None, None),
                   ["eredluin_militia_veteran_spearman"]))
    t.append(Troop("eredluin_militia_archer", "Militia Archer", 11, "Ranged", "ranged",
                   same(bow(11), ARROWS, ARROWS, "sm_dwarf_erebor_1h_axe_b"),
                   armour(None, ab("erebor_chest_leather_light"), ab("erebor_boots_light"), None, None),
                   ["eredluin_militia_veteran_archer"]))
    t.append(Troop("eredluin_militia_veteran_spearman", "Veteran Militia Spearman", 16, "Infantry", "spear",
                   paired(SPEAR, SHIELD_METAL_LOW, "sm_dwarf_erebor_1h_axe_b"),
                   armour(ab("erebor_helmet_leather_med"), ab("erebor_chest_leather_med"), ab("erebor_boots_med"),
                          ab("erebor_bracers_med"), None)))
    t.append(Troop("eredluin_militia_veteran_archer", "Veteran Militia Archer", 16, "Ranged", "ranged",
                   same(bow(16), ARROWS, ARROWS, "sm_dwarf_erebor_1h_axe_a"),
                   armour(ab("erebor_helmet_leather_med"), ab("erebor_chest_leather_med"), ab("erebor_boots_med"),
                          ab("erebor_bracers_med"), None)))
    return t


# (id, [(troop, min, max), ...]). The lord template's max sum is 200, the target for the newer
# realms in tools/rebalance_party_template_maxes.py (as the Arnor realms'); this generator owns them.
def party_templates():
    p = "eredluin_"
    return [
        ("kingdom_hero_party_eredluin_template", [
            (p + "delver", 3, 26), (p + "axeman", 2, 22), (p + "noble", 1, 19),
            (p + "bowman", 1, 19), (p + "hearthguard", 1, 17), (p + "hewer", 0, 15),
            (p + "veteran_bowman", 0, 13), (p + "broadbeam_guard", 0, 13), (p + "firebeard_bowman", 0, 10),
            (p + "hall_warden", 0, 10), (p + "mattock_warrior", 0, 8), (p + "broadbeam_shieldguard", 0, 8),
            (p + "firebeard_arbalester", 0, 8), (p + "veteran_hall_warden", 0, 5), (p + "gatewarden", 0, 4),
            (p + "firebeard_marksman", 0, 3)]),
        ("militia_eredluin_template", [
            (p + "militia_spearman", 1, 1), (p + "militia_archer", 1, 1)]),
        ("patrol_party_eredluin_template_level_1", [
            (p + "delver", 6, 6), (p + "axeman", 3, 3), (p + "bowman", 2, 2)]),
        ("patrol_party_eredluin_template_level_2", [
            (p + "axeman", 5, 5), (p + "hearthguard", 4, 4), (p + "veteran_bowman", 3, 3),
            (p + "hewer", 2, 2)]),
        ("patrol_party_eredluin_template_level_3", [
            (p + "hall_warden", 5, 5), (p + "veteran_hall_warden", 3, 3), (p + "mountain_bowman", 3, 3),
            (p + "mattock_warrior", 2, 2), (p + "firebeard_arbalester", 1, 1)]),
        ("rebels_eredluin_template", [
            (p + "delver", 24, 32), (p + "militia_spearman", 2, 3), (p + "militia_archer", 2, 3)]),
        ("vassal_reward_troops_eredluin", [
            (p + "kings_guard", 1, 1), (p + "ironbound_warden", 3, 3), (p + "halls_warden", 2, 2)]),
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
