"""
Generate character creation equipment rosters for TAOM's 12 custom cultures.

Outputs: Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml
Each culture gets 55 equipment rosters following vanilla Bannerlord patterns.
"""

import os
import sys

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
        "spear": "wm_gondor_spear",
        "bow": "gondor_steel_bow_starter",
        "arrows": "default_arrows",
        "shield": "gond_shld2",
        "axe_or_mace": "wm_gondor_lossarnach_1h_axe_a",
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
        "leg_alt": "easterling_boots",
        "head_light": "sk_rh_loke_hood_light_a",
        "head_military": "sk_rh_loke_helmet_inf_light_a",
        "gloves": "easterling_glove",
        "cape": "easterling_cape",
        "cape_alt": "easterlingwarriors01_cape",
        "sword": "rhun_1h_sword_a_t1",
        "spear": "easterling_spear",
        "bow": "hunting_bow",
        "arrows": "default_arrows",
        "shield": "easterling_shield",
        "axe_or_mace": None,
    },
    "shaghana": {
        "body_civilian": "haradrim_torso",
        "body_alt": "haradrim02_toso",
        "body_military": "haradrim02a_toso",
        "leg": "haradrim_boots",
        "leg_alt": "haradrim02_boots",
        "head_light": "haradrim_head",
        "head_military": "haradrim02_head",
        "gloves": "haradrim_gloves",
        "cape": "haradrim02_pauldrons",
        "cape_alt": "haradrim02_pauldrons",
        "sword": "aserai_sword_3_t3",
        "spear": "eastern_javelin_3_t4",
        "bow": "composite_steppe_bow",
        "arrows": "steppe_arrows",
        "shield": "desert_round_shield",
        "axe_or_mace": None,
        "javelin": "eastern_javelin_3_t4",
    },
    "abanissa": {
        "body_civilian": "haradrim02_toso",
        "body_alt": "haradrim02a_toso",
        "body_military": "harad03_torso",
        "leg": "haradrim02_boots",
        "leg_alt": "harad03_boots",
        "head_light": "haradrim02_head",
        "head_military": "harad03_helmet",
        "gloves": "harad03_glove",
        "cape": "harad03_cape",
        "cape_alt": "haradrim02_pauldrons",
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
    items = [equip("Body", c["body_civilian"])]
    items.append(equip("Leg", c["leg"]))
    if occupation == "retainer" and c.get("head_light"):
        items.append(equip("Head", c["head_light"]))
    if occupation in ("retainer", "artisan") and c.get("cape"):
        items.append(equip("Cape", c["cape"]))
    return items


def father_items(c, occupation):
    """Father equipment: occupation-appropriate gear."""
    items = []
    if occupation in ("retainer", "hunter"):
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


def generate_all():
    """Generate all equipment rosters for all cultures."""
    all_rosters = []
    total_count = 0

    for culture_id in CULTURES:
        rosters, count = generate_culture(culture_id)
        all_rosters.extend(rosters)
        total_count += count

    return all_rosters, total_count


def generate_culture(culture_id):
    """Generate the 55 rosters for a single culture. Returns (lines, count)."""
    c = CULTURES[culture_id]
    rosters = []
    rosters.append(f'\t<!-- ═══ Culture: {culture_id.upper()} ═══ -->')
    total_count = 0

    # 1. Parent fallback (none)
    rosters.append(f'\t<!-- Parent fallback -->')
    mother_none = [equip("Body", c["body_civilian"]), equip("Leg", c["leg"])]
    if c.get("cape"):
        mother_none.append(equip("Cape", c["cape"]))
    rosters.append(build_roster(
        f"mother_char_creation_none_{culture_id}", culture_id,
        [{"items": mother_none}]
    ))
    total_count += 1

    father_none = [equip("Body", c["body_civilian"]), equip("Leg", c["leg"])]
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
    `wm_gondor_shield_a02` in 8 rosters), so re-running the whole generator silently reverts those
    edits. Until the tables are reconciled with the file, only ever append.

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
    if "--append" in argv:
        i = argv.index("--append")
        cultures = [a for a in argv[i + 1:] if not a.startswith("-")]
        if not cultures:
            raise SystemExit("--append needs at least one culture id")
        append_cultures(cultures, apply="--apply" in argv)
        return

    rosters, count = generate_all()

    xml_lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<!--',
        f'  TAOM Character Creation Equipment Rosters ({count} rosters for {len(CULTURES)} custom cultures)',
        '  Generated by tools/generate_char_creation_equipment.py',
        '  Items sourced from LOTRLOME_Armory module',
        '-->',
        '<EquipmentRosters>',
    ]
    xml_lines.extend(rosters)
    xml_lines.append('</EquipmentRosters>')

    output = os.path.normpath(OUTPUT_PATH)
    with open(output, "w", encoding="utf-8") as f:
        f.write("\n".join(xml_lines) + "\n")

    print(f"Generated {count} equipment rosters -> {output}")


if __name__ == "__main__":
    main()
