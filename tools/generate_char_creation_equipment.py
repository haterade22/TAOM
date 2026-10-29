"""
Generate character creation equipment rosters for TAOM's 12 custom cultures.

Outputs: Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml
Each culture gets 55 equipment rosters following vanilla Bannerlord patterns.
"""

import os
import sys
import xml.etree.ElementTree as ET

OUTPUT_PATH = os.path.join(
    os.path.dirname(__file__),
    "..", "Main", "_Module", "ModuleData", "equipmentsets", "taom_char_creation_equipment.xml"
)

# ─── Per-culture item definitions ───────────────────────────────────────────

# Keys used per culture:
#   body_civilian, body_military  - torso armor (light civilian vs military)
#   body_alt                      - alternate civilian body (for variety)
#   leg, leg_alt                  - boots
#   head_light, head_military     - headgear
#   gloves                        - hand armor
#   cape, cape_alt                - shoulder/cloak
#   sword                         - 1H weapon
#   spear                         - polearm
#   bow, arrows                   - ranged (optional)
#   shield                        - shield
#   axe_or_mace                   - secondary melee (optional)
#   javelin                       - thrown weapon (optional, for skirmisher)
#   mother_body, father_body      - optional torso for every mother / father roster (Erebor's dress
#                                   and noble tunic); without them the parents wear the body_* pieces

CULTURES = {
    "gondor": {
        "body_civilian": "gondor_noble_coat_a",
        "body_alt": "gondor_noble_coat_b",
        "body_military": "gondor_noble_jerkin_a",
        "leg": "sk_gd_ano_boots_a",
        "leg_alt": "ithilien_boots",
        "head_light": "ithilien_hood",
        "head_military": "sk_gd_ano_inf_helmet_med_a",
        "gloves": "sk_gd_ano_gloves_a",
        "cape": "ithilien_cloak",
        "cape_alt": "ithilien_cloak_var",
        "sword": "wm_gondor_sword_a01",
        # 2026-09-13: wm_gondor_spear and the plain Lossarnach axe left the
        # Armory on 2026-09-11; these are the ids KEYforce put in the shipped
        # rosters (lotraom-assets d7d5f75b). gond_shld2 never resolved; the
        # file's hand fix went wm_gondor_shield_a02 -> sm_gd_shield_a1.
        "spear": "wm_gondor_light_spear",
        "bow": "gondor_steel_bow_starter",
        "arrows": "default_arrows",
        "shield": "sm_gd_shield_a1",
        "axe_or_mace": "wm_gondor_lossarnach_1h_axe_black_ash_a",
    },
    "mordor": {
        "body_civilian": "sk_uruk_mordor_chainmail_light_a",
        "body_alt": "sk_uruk_mordor_chainmail_light_a",
        "body_military": "sk_uruk_mordor_chainmail_light_a",
        "leg": "sk_uruk_mordor_boots_a",
        "leg_alt": "sk_uruk_mordor_boots_a",
        "head_light": "sk_uruk_mordor_helmet_medium_a1",
        "head_military": "sk_uruk_mordor_helmet_medium_a1",
        "gloves": "sk_uruk_mordor_bracer_medium_a",
        "cape": None,
        "cape_alt": None,
        "sword": "wm_mordor_set1_sword_a01",
        "spear": "wm_mordor_set1_polearm_a01",
        "bow": "sm_uruk_bow_starter",
        "arrows": "default_arrows",
        "shield": "sm_mordor_shield_small_a",
        "axe_or_mace": "wm_mordor_set1_axe_a01",
    },
    "erebor": {
        "body_civilian": "sk_dwarf_erebor_chest_leather_light_a",
        "body_alt": "sk_dwarf_erebor_chest_leather_light_b",
        "body_military": "sk_dwarf_erebor_chest_leather_med_a",
        "leg": "sk_dwarf_erebor_boots_light_a",
        "leg_alt": "sk_dwarf_erebor_boots_light_b",
        "head_light": "sk_dwarf_erebor_helmet_leather_light_a",
        "head_military": "sk_dwarf_erebor_helmet_leather_light_b",
        "gloves": "sk_dwarf_erebor_gloves_a",
        "cape": "sk_dwarf_erebor_pauldron_scale_a",
        "cape_alt": "sk_dwarf_erebor_pauldron_scale_a",
        "sword": "sm_dwarf_iron_sword_a",
        "spear": "sm_dwarf_erebor_spear_a",
        "bow": "sm_dwarf_erebor_bow_a",
        "arrows": "sk_dwarf_erebor_arrow_a",
        "shield": "sm_dwarf_erebor_shield_leather_a",
        "axe_or_mace": "sm_dwarf_erebor_1h_axe_a",
        # what the shipped Erebor parent rosters wear since 307df401a, so mother and father differ
        "mother_body": "sk_dwarf_dress_normal_a",
        "father_body": "sk_dwarf_tunic_noble_a",
    },
    "rivendell": {
        "body_civilian": "rivendell_torso_light_light_tier1",
        "body_alt": "rivendell_torso_light_light_tier1",
        "body_military": "rivendell_torso_light_light_tier2",
        "leg": "rivendell_boots_leather1",
        "leg_alt": "rivendell_boots_leather2",
        "head_light": "gf_helmet_1",
        "head_military": "gf_helmet_1",
        "gloves": "rivendell_vambraces_gloves1",
        "cape": "rivendell_cape_a",
        "cape_alt": "rivendell_cape_b",
        "sword": "wm_rivendell_sword_a01",
        "spear": "wm_rivendell_spear_a01",
        "bow": "highelf_longbow_starter",
        "arrows": "wm_elven_arrow_v1_a",
        "shield": "wm_elven_shield_a",
        "axe_or_mace": None,
    },
    "mirkwood": {
        "body_civilian": "mkwd_inf3_chest",
        "body_alt": "mkwd_inf3_chest_silver",
        "body_military": "mkwd_inf3_chest_bronze",
        "leg": "mirkwood_boots",
        "leg_alt": "mirkwood_boots",
        "head_light": "mkwd_inf1_t1_helmet_gold_hc",
        "head_military": "mkwd_inf1_t1_helmet_silver_hc",
        "gloves": "mkwd_inf3_vambraces_bronze",
        "cape": "mirkwood_shoulder",
        "cape_alt": "mkwd_inf3_pauldrons",
        "sword": "mirkwood_sword_a01",
        "spear": "mirkwood_spear_a01",
        "bow": "wm_mirkwood_bow_a03",
        "arrows": "wm_mirkwood_arrow_a01",
        "shield": "wm_mirkwood_shield_a01",
        "axe_or_mace": None,
    },
    "isengard": {
        "body_civilian": "sk_uruk_hai_tunic_a1",
        "body_alt": "sk_uruk_hai_tunic_a2",
        "body_military": "sk_uruk_hai_tunic_a2",
        "leg": "sk_uruk_hai_shoes_a1",
        "leg_alt": "sk_uruk_hai_shoes_a1",
        "head_light": "sk_uruk_hai_helmet_sword_light_a1",
        "head_military": "sk_uruk_hai_helmet_spear_light_a1",
        "gloves": "sk_uruk_hai_gloves_a1",
        "cape": "sk_uruk_hai_pauldron_light_a1",
        "cape_alt": "sk_uruk_hai_pauldron_light_a1",
        "sword": "isengard_1h_sword_a",
        "spear": "isengard_spear_a",
        "bow": "wm_isengard_bow_starter",
        "arrows": "wm_isengard_arrow_a01",
        "shield": "wm_isengard_shield_a03",
        "axe_or_mace": "isengard_2h_axe_a",
    },
    "gundabad": {
        "body_civilian": "sk_gb_uruk_chest_light_a",
        "body_alt": "sk_gb_uruk_chest_light_b",
        "body_military": "sk_gb_uruk_chest_light_a",
        "leg": "sk_gb_uruk_boots_light_a",
        "leg_alt": "sk_gb_uruk_boots_light_b",
        "head_light": "sk_gb_uruk_helmet_light_a",
        "head_military": "sk_gb_uruk_helmet_light_b",
        "gloves": "sk_gb_uruk_bracer_med_a",
        "cape": "sk_gb_uruk_cape_a",
        "cape_alt": "sk_gb_uruk_cape_a",
        "sword": "wm_gundabad_sword_a01",
        "spear": "wm_gundabad_spear_a01",
        "bow": None,
        "arrows": None,
        "shield": "wm_gundabad_shield_a03",
        "axe_or_mace": "wm_gundabad_axe_a01",
        "javelin": "wm_gundabad_javelin_a01",
    },
    "dolguldur": {
        "body_civilian": "sk_dg_uruk_chest_light_a",
        "body_alt": "sk_dg_uruk_chest_light_b",
        "body_military": "sk_dg_uruk_chest_light_a",
        "leg": "sk_dg_uruk_boots_light_a",
        "leg_alt": "sk_dg_uruk_boots_light_a",
        "head_light": "sk_dg_uruk_helmet_light_a",
        "head_military": "sk_dg_uruk_helmet_light_b",
        "gloves": "sk_dg_uruk_bracer_med_a",
        "cape": "sk_dg_uruk_pauldron_med_a",
        "cape_alt": "sk_dg_uruk_pauldron_med_a",
        "sword": "wm_dol_goldur_1h_sword_a01",
        "spear": "wm_dol_goldur_halberd_a01",
        "bow": None,
        "arrows": None,
        "shield": "sm_dg_khml_shield_med_a",
        "axe_or_mace": "wm_dol_goldur_1h_mace_a01",
    },
    "umbar": {
        "body_civilian": "sk_rh_loke_tunic_a",
        "body_alt": "sk_rh_loke_tunic_a",
        "body_military": "sk_rh_loke_chest_light_a",
        "leg": "sk_rh_loke_boots_a",
        # 2026-09-13: the easterling_* art went on 2026-08-28 and the spear on
        # 2026-09-11. These are the Loke-Rim ids the shipped rosters carry
        # (apply_dead_mesh_item_swaps.py for the armour, KEYforce's
        # lotraom-assets d7d5f75b for the spear); both capes collapsed onto one.
        "leg_alt": "sk_rh_loke_grvs_plate_light_a",
        "head_light": "sk_rh_loke_hood_light_a",
        "head_military": "sk_rh_loke_helmet_inf_light_a",
        "gloves": "sk_rh_loke_bracer_heavy_b",
        "cape": "sk_rh_loke_pauldron_lam_heavy_a",
        "cape_alt": "sk_rh_loke_pauldron_lam_heavy_a",
        "sword": "sm_rh_loke_1h_sword_a",
        "spear": "sm_rh_loke_spear_a",
        "bow": "hunting_bow",
        "arrows": "default_arrows",
        "shield": "sm_rh_loke_shield_med_a",
        "axe_or_mace": None,
    },
    "shaghana": {
        "body_civilian": "sk_hr_st_chest_inf_chest_light_a",
        "body_alt": "sk_hr_st_chest_inf_chest_light_a",
        "body_military": "sk_hr_st_chest_inf_chest_light_a",
        "leg": "eastern_leather_boots",
        "leg_alt": "eastern_leather_boots",
        "head_light": "sk_hr_st_helmet_light_a",
        "head_military": "sk_hr_st_helmet_med_a",
        "gloves": "sk_hr_st_bracer_light_a",
        "cape": "sk_hr_st_pauld_inf_med_a",
        "cape_alt": "sk_hr_st_pauld_inf_med_a",
        "sword": "aserai_sword_3_t3",
        "spear": "eastern_javelin_3_t4",
        "bow": "composite_steppe_bow",
        "arrows": "steppe_arrows",
        "shield": "desert_round_shield",
        "axe_or_mace": None,
        "javelin": "eastern_javelin_3_t4",
    },
    "abanissa": {
        "body_civilian": "sk_hr_st_chest_inf_chest_light_a",
        "body_alt": "sk_hr_st_chest_inf_chest_light_a",
        "body_military": "aserai_scale_armor_on_chain",
        "leg": "eastern_leather_boots",
        "leg_alt": "eastern_leather_boots",
        "head_light": "sk_hr_st_helmet_med_a",
        "head_military": "brass_aserai_helmet_b_open",
        "gloves": "rough_tied_bracers",
        "cape": None,  # shipped abanissa rosters carry no cape
        "cape_alt": None,
        "sword": "aserai_sword_5_t4",
        "spear": "eastern_spear_4_t4",
        "bow": "composite_bow",
        "arrows": "bodkin_arrows_b",
        "shield": "desert_round_shield",
        "axe_or_mace": None,
    },
}

# Lothlorien uses Rivendell items per user instruction
CULTURES["lothlorien"] = dict(CULTURES["rivendell"])

# Goblin-town and the Moria orcs. Every item below is taken from the slot it already occupies in
# the shipped troop trees (troops_goblin.xml / troops_mistymountainorcs.xml), so nothing here can
# reference an item the Armory does not define — the "underwear bug" gate.
#
# `no_mount: True` — both troop trees contain zero `slot="Horse"` entries, so the player must not
# be handed a horse the culture never fields. See docs/features/no-mount-cultures.md; Patch20
# detects the absence at runtime and skips vanilla's horse actor in the narrative stages.
CULTURES["goblin"] = {
    "body_civilian": "sk_md_orc_arc_chest_light_a",
    "body_alt": "sk_md_orc_arc_chest_light_b",
    "body_military": "sk_md_orc_inf_chest_med_a",
    "leg": "sk_md_orc_boots_light_a",
    "leg_alt": "sk_md_orc_boots_med_a",
    "head_light": "sk_gn_orc_helmet_light_a",
    "head_military": "sk_gn_orc_inf_helmet_med_a",
    "gloves": "sk_md_orc_bracer_med_a",
    "cape": "sk_md_orc_pauldron_light_a",
    "cape_alt": "sk_md_orc_pauldron_med_a",
    "sword": "wm_mordor_set1_sword_a02",
    "spear": "wm_gundabad_spear_a01",
    "bow": "sm_uruk_bow_a",
    "arrows": "bodkin_arrows_c",
    "shield": "wm_gundabad_shield_a02",
    "axe_or_mace": "wm_gundabad_axe_a01",
    "javelin": "wm_gundabad_javelin_a01",
    "no_mount": True,
}

# The Misty Mountain tree is a leaf-for-leaf clone of the Goblin-town one (same 23 troop names,
# same item pool — see docs/features/new-factions-misty-mountains-lindon.md, which flags both trees
# as placeholder Gundabad clones). Sharing the table is therefore accurate today rather than lazy;
# when either tree is given its own identity, split this into a literal dict.
CULTURES["mistymountainorcs"] = dict(CULTURES["goblin"])

# Cultures promoted out of a borrowed one (2026-08-10). Their troop trees are clones, and the items
# in them are LOTRLOME_Armory ids that the promotion deliberately did not rename — an Armory id
# names a real asset, so renaming it invents a reference to nothing. Reusing the source culture's
# verified item table is therefore accurate rather than lazy: it is literally the same gear.
#
# Blue Craig is no-mount like the goblin tree it came from (zero `slot="Horse"` entries). Lindon is
# NOT — the Rivendell tree it came from fields elven cavalry, so it keeps horse and harness.
CULTURES["bluecraig"] = dict(CULTURES["goblin"])
CULTURES["lindon"] = dict(CULTURES["rivendell"])

# Arthedain (2026-10-07): the Arnor kit for anything worn in the field, every piece one the
# culture's own troops already wear (tools/generate_arthedain_troops.py), and Gondor's civilian
# coats, ranger hood and starter weapons for what the Arnor pack does not cover (it ships armour
# only, no civilian dress or light weapons). The Dunedain of the North are the same people as
# Gondor's, so the borrow is lore-true. Mounted: the tree fields cavalry.
CULTURES["arthedain"] = {
    "body_civilian": "gondor_noble_coat_a",
    "body_alt": "gondor_noble_coat_b",
    "body_military": "sk_ar_art_chainmail_half_a",
    "leg": "sk_ar_art_boots_a",
    "leg_alt": "sk_ar_art_grvs_inf_light_a",
    "head_light": "ithilien_hood",
    "head_military": "sk_ar_art_helmet_inf_light_a",
    "gloves": "sk_ar_art_gloves_a",
    "cape": "sk_ar_art_cape_a",
    "cape_alt": "sk_ar_art_cape_b",
    "sword": "wm_gondor_sword_a01",
    "spear": "wm_gondor_light_spear_cardolan",
    "bow": "gondor_steel_bow_starter",
    "arrows": "default_arrows",
    "shield": "sm_gd_shield_a1",
}

# Cardolan and Rhudaur (#978): each culture's starting kit from the pieces its own troops wear
# (tools/generate_cardolan_troops.py: Gondor's armour; generate_rhudaur_troops.py: the Arnor kit and
# Dunland's axes), with Gondor's civilian coats and starter weapons as Arthedain has. Both mounted.
CULTURES["cardolan"] = {
    "body_civilian": "gondor_noble_coat_a",
    "body_alt": "gondor_noble_coat_b",
    "body_military": "sk_gd_anf_inf_chainmail_a",
    "leg": "sk_gd_ano_boots_a",
    "leg_alt": "sk_gd_ano_grvs_inf_light_a",
    "head_light": "ithilien_hood",
    "head_military": "sk_gd_pin_inf_helmet_med_a",
    "gloves": "sk_gd_ano_gloves_a",
    "cape": "sk_gd_ano_pauld_inf_med_a",
    "cape_alt": "sk_gd_ano_pauld_inf_med_b",
    "sword": "wm_gondor_sword_a01",
    "spear": "wm_gondor_light_spear_cardolan",
    "bow": "gondor_steel_bow_starter",
    "arrows": "default_arrows",
    "shield": "sm_gd_shield_a1",
}
CULTURES["rhudaur"] = dict(CULTURES["arthedain"], sword="sturgia_axe_2_t2", shield="dunland_wulf_shield_light_a")

# Ered Luin: the Blue Mountains dwarves wear Erebor's dwarf kit, piece for piece, so nothing human
# reaches a dwarf. Unlike Erebor's table it is no-mount: dwarves do not ride a horse and Ered Luin
# fields no war ram, so its rosters carry no Horse or HorseHarness slot (Patch20 skips the horse
# actor in the narrative stages for a roster without one).
CULTURES["eredluin"] = dict(CULTURES["erebor"], no_mount=True)

# Anduin Vale and Angaladh are cut from Dale (culture id `sturgia`), which has no table here and no
# player_char_creation_sturgia_* rosters, so each table is authored from the Dale items the realm's
# own troops wear (troops_anduinvale.xml, troops_angaladh.xml: every piece below is on a troop).
# Dale ships armour and weapons only, no civilian dress, so the lightest Dale pieces (the a01
# archer and infantry sets) stand in for the civilian body and the bow is Dale's recurve bow.
# Anduin Vale fights with axes and fields cavalry, so it is mounted and its sword slot holds a
# Dale war axe. Angaladh is a spear and axe realm of foot soldiers with no rider: no_mount, so
# its rosters carry no Horse or HorseHarness slot (Patch20 skips the horse actor in the narrative
# stages for a roster without one).
CULTURES["anduinvale"] = {
    "body_civilian": "sk_dale_chest_archer_a01",
    "body_alt": "sk_dale_chest_infrantry_a01",
    "body_military": "sk_dale_chest_infrantry_b01",
    "leg": "sk_dale_boots_infrantry_a01",
    "leg_alt": "sk_dale_boots_archer_a01",
    "head_light": "sk_dale_helmet_archer_a01",
    "head_military": "sk_dale_helmet_infrantry_b01",
    "gloves": "sk_dale_gauntlet_infrantry_a01",
    "cape": "sk_dale_shoulder_archer_a01",
    "cape_alt": "sk_dale_shoulder_infrantry_a01",
    "sword": "dale_war_axe_c",
    "spear": "dale_spear_a",
    "bow": "dale_recurve_bow_a",
    "arrows": "default_arrows",
    "shield": "wm_dale_shield_a06",
    "axe_or_mace": "dale_war_axe_a",
}
CULTURES["angaladh"] = dict(CULTURES["anduinvale"], sword="dale_sword_b", shield="wm_dale_shield_a08",
                            axe_or_mace="dale_war_axe_c", no_mount=True)

# Dorwinion (#1062) is cut from Dale (culture id `sturgia`), so like Anduin Vale its table is
# authored from the Dale items its own troops wear (troops_dorwinion.xml): the L6 levy's archer and
# mariner sets, the infantry set of its spear line, its sword, spear and shield, and the first rung
# of the `dale` ranged ladder. It fields river-riders, so it is mounted.
CULTURES["dorwinion"] = {
    "body_civilian": "sk_dale_chest_archer_a01",
    "body_alt": "sk_dale_lake_town_chest_mariner_a01",
    "body_military": "sk_dale_chest_infrantry_a01",
    "leg": "sk_dale_boots_archer_a01",
    "leg_alt": "sk_dale_lake_town_boots_mariner_a01",
    "head_light": "sk_dale_helmet_archer_a01",
    "head_military": "sk_dale_helmet_infrantry_a01",
    "gloves": "sk_dale_gauntlet_archer_a01",
    "cape": "sk_dale_shoulder_archer_a01",
    "cape_alt": "sk_dale_lake_town_shoulder_mariner_a01",
    "sword": "dale_sword_c",
    "spear": "dale_spear_a",
    "bow": "ladder_dale_bow_t1",
    "arrows": "default_arrows",
    "shield": "wm_dale_shield_a06",
    "axe_or_mace": None,
}

# South Rhovanion (#1062) is cut from Rohan (culture id `vlandia`), which has no table here and no
# player_char_creation_vlandia_* rosters, so its table is authored from the Rohan items its own
# troops wear (troops_southrhovanion.xml): the militia tunics and the militia armour of the early
# levels, the cts_rohan boots, helm, armguards and pauldron, the ws sword and spear, the first
# round shield and the L11 rung of the `rohan` ranged ladder. Rohan ships no civilian dress, so the
# two militia tunics stand in for it. It is the horse realm, so it is mounted (horse and harness).
CULTURES["southrhovanion"] = {
    "body_civilian": "rohan_militia_tunic_a",
    "body_alt": "rohan_militia_tunic_b",
    "body_military": "rohan_militia_armour_b",
    "leg": "cts_rohan_boots3",
    "leg_alt": "cts_rohan_boots3",
    "head_light": "cts_rohan_helmet1",
    "head_military": "cts_rohan_helmet2a",
    "gloves": "cts_rohan_armguard1",
    "cape": "rohan_militia_pauldron_both",
    "cape_alt": "rohan_militia_pauldron_both",
    "sword": "wm_rohan_ws_sword_a01",
    "spear": "wm_rohan_ws_spear_a01",
    "bow": "ladder_rohan_bow_t2",
    "arrows": "default_arrows",
    "shield": "wm_rohan_shield_a01_gsw",
    "axe_or_mace": None,
}

OCCUPATIONS = ["retainer", "merchant", "farmer", "artisan", "hunter", "vagabond"]
CAREERS = ["retainer", "mercenary", "guard", "hunter", "infantry", "skirmisher", "bard", "6"]


def item(item_id):
    """Return Item.{id} format."""
    return f"Item.{item_id}"


def equip(slot, item_id):
    """Create an Equipment element dict."""
    return {"slot": slot, "id": item(item_id)}


def build_roster(roster_id, culture_id, equipment_sets):
    """Build an EquipmentRoster element with given sets."""
    lines = []
    lines.append(f'\t<EquipmentRoster')
    lines.append(f'\t\tid="{roster_id}"')
    lines.append(f'\t\tculture="Culture.{culture_id}">')

    for eq_set in equipment_sets:
        is_civilian = eq_set.get("civilian", False)
        if is_civilian:
            lines.append(f'\t\t<EquipmentSet')
            lines.append(f'\t\t\tequipmentType="Civilian">')
        else:
            # v1.4.3+: Battle is the implicit default — vanilla omits equipmentType for battle sets.
            lines.append(f'\t\t<EquipmentSet>')

        for eq in eq_set["items"]:
            lines.append(f'\t\t\t<Equipment')
            lines.append(f'\t\t\t\tslot="{eq["slot"]}"')
            lines.append(f'\t\t\t\tid="{eq["id"]}" />')

        lines.append(f'\t\t</EquipmentSet>')

    lines.append(f'\t</EquipmentRoster>')
    return "\n".join(lines)


def mother_items(c, occupation):
    """Mother equipment: civilian dress-like items."""
    items = [equip("Body", c.get("mother_body") or c["body_civilian"])]
    items.append(equip("Leg", c["leg"]))
    if occupation == "retainer" and c.get("head_light"):
        items.append(equip("Head", c["head_light"]))
    if occupation in ("retainer", "artisan") and c.get("cape"):
        items.append(equip("Cape", c["cape"]))
    return items


def father_items(c, occupation):
    """Father equipment: occupation-appropriate gear."""
    items = []
    if c.get("father_body"):
        items.append(equip("Body", c["father_body"]))
    elif occupation in ("retainer", "hunter"):
        items.append(equip("Body", c["body_military"]))
    elif occupation == "merchant":
        items.append(equip("Body", c["body_alt"]))
    else:
        items.append(equip("Body", c["body_civilian"]))

    items.append(equip("Leg", c["leg_alt"] or c["leg"]))

    if occupation == "retainer":
        if c.get("head_military"):
            items.append(equip("Head", c["head_military"]))
    elif occupation == "hunter":
        if c.get("cape"):
            items.append(equip("Cape", c["cape"]))
        if c.get("head_light"):
            items.append(equip("Head", c["head_light"]))
    elif occupation == "artisan":
        pass  # just body + leg
    elif occupation == "merchant":
        if c.get("head_light"):
            items.append(equip("Head", c["head_light"]))

    return items


def childhood_items_m(c, occupation):
    """Childhood age male: simple clothing."""
    if occupation in ("retainer", "merchant"):
        return [equip("Body", c["body_alt"]), equip("Leg", c["leg"])]
    elif occupation in ("farmer", "artisan"):
        return [equip("Body", c["body_civilian"]), equip("Leg", c["leg"])]
    elif occupation == "hunter":
        return [equip("Body", c["body_civilian"]), equip("Leg", c["leg_alt"] or c["leg"])]
    else:  # vagabond
        return [equip("Body", c["body_civilian"]), equip("Leg", c["leg"])]


def childhood_items_f(c, occupation):
    """Childhood age female: same as male but potentially lighter boots."""
    return [equip("Body", c["body_civilian"]), equip("Leg", c["leg"])]


def career_battle_items(c, career):
    """Battle equipment set for adult career."""
    items = []

    if career == "retainer":
        items.append(equip("Item0", c["sword"]))
        items.append(equip("Item1", c["shield"]))
        items.append(equip("Item2", c["spear"]))
        items.append(equip("Body", c["body_military"]))
        items.append(equip("Leg", c["leg"]))
    elif career == "mercenary":
        items.append(equip("Item0", c["sword"]))
        items.append(equip("Item1", c["shield"]))
        items.append(equip("Item2", c["spear"]))
        items.append(equip("Body", c["body_military"]))
        items.append(equip("Leg", c["leg"]))
    elif career == "guard":
        items.append(equip("Item0", c["shield"]))
        items.append(equip("Item1", c["sword"]))
        if c.get("bow"):
            items.append(equip("Item2", c["bow"]))
            items.append(equip("Item3", c["arrows"]))
        elif c.get("javelin"):
            items.append(equip("Item2", c["javelin"]))
        else:
            items.append(equip("Item2", c["axe_or_mace"] or c["spear"]))
        items.append(equip("Body", c["body_military"]))
        items.append(equip("Leg", c["leg"]))
    elif career == "hunter":
        items.append(equip("Item0", c["sword"]))
        if c.get("bow"):
            items.append(equip("Item2", c["bow"]))
            items.append(equip("Item3", c["arrows"]))
        elif c.get("javelin"):
            items.append(equip("Item2", c["javelin"]))
        else:
            items.append(equip("Item2", c["spear"]))
        items.append(equip("Body", c["body_military"]))
        items.append(equip("Leg", c["leg"]))
    elif career == "infantry":
        items.append(equip("Item0", c["sword"]))
        items.append(equip("Item2", c["spear"]))
        items.append(equip("Body", c["body_military"]))
        items.append(equip("Leg", c["leg"]))
    elif career == "skirmisher":
        items.append(equip("Item0", c["sword"]))
        if c.get("javelin"):
            items.append(equip("Item2", c["javelin"]))
            items.append(equip("Item3", c["javelin"]))
        elif c.get("bow"):
            items.append(equip("Item2", c["bow"]))
            items.append(equip("Item3", c["arrows"]))
        else:
            items.append(equip("Item2", c["axe_or_mace"] or c["spear"]))
        items.append(equip("Body", c["body_military"]))
        items.append(equip("Leg", c["leg"]))
    elif career == "bard":
        items.append(equip("Item0", c["spear"]))
        if c.get("javelin"):
            items.append(equip("Item2", c["javelin"]))
        elif c.get("bow"):
            items.append(equip("Item2", c["bow"]))
            items.append(equip("Item3", c["arrows"]))
        items.append(equip("Body", c["body_civilian"]))
        items.append(equip("Leg", c["leg"]))
    elif career == "6":
        # Fallback career
        if c.get("axe_or_mace"):
            items.append(equip("Item2", c["axe_or_mace"]))
        else:
            items.append(equip("Item2", c["spear"]))
        items.append(equip("Item3", c["shield"]))
        items.append(equip("Body", c["body_military"]))
        items.append(equip("Leg", c["leg"]))

    # Mounted cultures get horse + harness. A no-mount culture must not, or the player rides out of
    # character creation on an animal its own troop tree never fields.
    if not c.get("no_mount"):
        items.append(equip("Horse", "sumpter_horse"))
        items.append(equip("HorseHarness", "light_harness"))

    return items


def career_civilian_items(c, career, is_female):
    """Civilian equipment set for adult career."""
    items = [equip("Item0", c["sword"])]

    if career in ("bard", "6"):
        items[0] = equip("Item0", c["sword"])
        items.append(equip("Body", c["body_civilian"]))
    elif career in ("retainer", "mercenary"):
        items.append(equip("Body", c["body_alt"]))
    else:
        items.append(equip("Body", c["body_civilian"]))

    if is_female and c.get("cape"):
        items.append(equip("Cape", c["cape"]))
    elif not is_female and c.get("cape"):
        items.append(equip("Cape", c["cape"]))

    items.append(equip("Leg", c["leg"]))

    return items


def generate_culture(culture_id):
    """Generate the 55 rosters for a single culture. Returns (lines, count)."""
    c = CULTURES[culture_id]
    rosters = []
    rosters.append(f'\t<!-- ═══ Culture: {culture_id.upper()} ═══ -->')
    total_count = 0

    # 1. Parent fallback (none)
    rosters.append(f'\t<!-- Parent fallback -->')
    mother_none = [equip("Body", c.get("mother_body") or c["body_civilian"]), equip("Leg", c["leg"])]
    if c.get("cape"):
        mother_none.append(equip("Cape", c["cape"]))
    rosters.append(build_roster(
        f"mother_char_creation_none_{culture_id}", culture_id,
        [{"items": mother_none}]
    ))
    total_count += 1

    father_none = [equip("Body", c.get("father_body") or c["body_civilian"]), equip("Leg", c["leg"])]
    rosters.append(build_roster(
        f"father_char_creation_none_{culture_id}", culture_id,
        [{"items": father_none}]
    ))
    total_count += 1

    # 2. Parent occupations (6 × mother + father = 12)
    for i, occ in enumerate(OCCUPATIONS, 1):
        rosters.append(f'\t<!--{i}.{occ.capitalize()}-->')

        m_items = mother_items(c, occ)
        rosters.append(build_roster(
            f"mother_char_creation_{occ}_{culture_id}", culture_id,
            [{"items": m_items}]
        ))
        total_count += 1

        f_items = father_items(c, occ)
        rosters.append(build_roster(
            f"father_char_creation_{occ}_{culture_id}", culture_id,
            [{"items": f_items}]
        ))
        total_count += 1

    # 3. Childhood/Education age (6 occ × 2 ages × 2 genders = 24)
    rosters.append(f'\t<!-- Childhood & Education age -->')
    for occ in OCCUPATIONS:
        for gender in ["m", "f"]:
            if gender == "m":
                ch_items = childhood_items_m(c, occ)
            else:
                ch_items = childhood_items_f(c, occ)

            # Childhood and education use same items (matching vanilla pattern)
            rosters.append(build_roster(
                f"player_char_creation_childhood_age_{culture_id}_{occ}_{gender}",
                culture_id, [{"items": ch_items}]
            ))
            total_count += 1

            rosters.append(build_roster(
                f"player_char_creation_education_age_{culture_id}_{occ}_{gender}",
                culture_id, [{"items": ch_items}]
            ))
            total_count += 1

    # 4. Adult careers (8 careers × 2 genders = 16)
    rosters.append(f'\t<!-- Adult careers -->')
    for career in CAREERS:
        for gender in ["m", "f"]:
            is_female = gender == "f"
            battle = career_battle_items(c, career)
            civilian = career_civilian_items(c, career, is_female)

            rosters.append(build_roster(
                f"player_char_creation_{culture_id}_{career}_{gender}",
                culture_id,
                [
                    {"items": battle},
                    {"items": civilian, "civilian": True},
                ]
            ))
            total_count += 1

    # 5. Show roster (1)
    rosters.append(f'\t<!-- Show -->')
    show_items = [equip("Body", c["body_military"])]
    if c.get("gloves"):
        show_items.append(equip("Gloves", c["gloves"]))
    show_items.append(equip("Leg", c["leg"]))

    show_civilian = [equip("Body", c["body_civilian"]), equip("Leg", c["leg"])]
    if c.get("axe_or_mace"):
        show_civilian.insert(0, equip("Item0", c["axe_or_mace"]))
    else:
        show_civilian.insert(0, equip("Item0", c["sword"]))

    rosters.append(build_roster(
        f"player_char_creation_show_{culture_id}", culture_id,
        [
            {"items": show_items},
            {"items": show_civilian, "civilian": True},
        ]
    ))
    total_count += 1

    return rosters, total_count


def append_cultures(culture_ids, apply=False):
    """
    Splice one or more cultures' rosters into the existing file, leaving every other culture's
    bytes untouched.

    A full rewrite is NOT safe here. The shipped file has been hand-corrected since it was last
    generated (verified 2026-08-10: Gondor's shield was fixed from `gond_shld2` to
    `wm_gondor_shield_a02` in 8 rosters, since re-pointed to `sm_gd_shield_a1`), and since #569
    every id the rosters hand out is a `starter_<donor>` twin written by
    `wire_starter_kit_rosters.py`, which `StarterKitCoverageTests` enforces. Re-running the whole
    generator reverts both. Until the tables are reconciled with the file, only ever append, and
    run the starter-kit wiring over anything appended.

    Idempotent: a culture that already has rosters in the file is skipped.
    """
    output = os.path.normpath(OUTPUT_PATH)
    original = open(output, encoding="utf-8", newline="").read()
    newline = "\r\n" if "\r\n" in original else "\n"
    text = original.replace("\r\n", "\n")

    closing = "</EquipmentRosters>"
    if closing not in text:
        raise SystemExit(f"{output}: no {closing} — refusing to guess where to append")

    added, skipped, lines = 0, [], []
    for culture_id in culture_ids:
        if culture_id not in CULTURES:
            raise SystemExit(f"unknown culture '{culture_id}' — add it to CULTURES first")
        if f'culture="Culture.{culture_id}"' in text:
            skipped.append(culture_id)
            continue
        rosters, count = generate_culture(culture_id)
        lines.extend(rosters)
        added += count

    for culture_id in skipped:
        print(f"skip {culture_id}: already present")
    if not lines:
        print("nothing to do")
        return

    head, _, tail = text.rpartition(closing)
    updated = head + "\n".join(lines) + "\n" + closing + tail
    try:  # tools/README.md "XML I/O convention": never write a document that no longer parses
        ET.fromstring(updated.lstrip("﻿").encode("utf-8"))
    except ET.ParseError as exc:
        raise SystemExit(f"{output}: the appended rosters do not parse ({exc}); nothing written")

    if not apply:
        print(f"DRY RUN: would add {added} rosters for {[c for c in culture_ids if c not in skipped]}")
        print(f"         {output}: {text.count(chr(10))} -> {updated.count(chr(10))} lines")
        print("         re-run with --apply to write")
        return

    with open(output, "w", encoding="utf-8", newline="") as f:
        f.write(updated.replace("\n", newline))
    print(f"Added {added} equipment rosters -> {output}")


def main():
    argv = sys.argv[1:]
    if "--append" not in argv:
        # A full regeneration reverted every hand fix and stripped the Item.starter_* wiring that
        # wire_starter_kit_rosters.py adds, and any --dry-run or --help run did it (2026-10-07 review).
        raise SystemExit("usage: generate_char_creation_equipment.py --append <culture> [<culture> ...] [--apply]")
    i = argv.index("--append")
    cultures = [a for a in argv[i + 1:] if not a.startswith("-")]
    if not cultures:
        raise SystemExit("--append needs at least one culture id")
    append_cultures(cultures, apply="--apply" in argv)


if __name__ == "__main__":
    main()
