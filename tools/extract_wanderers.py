"""
Extract and convert LOTRAOM wanderer data into TAOM format.

Produces 4 output files:
1. taom_wanderers.xml - NPCCharacter templates
2. taom_wanderer_skill_sets.xml - SkillSet definitions
3. taom_wanderer_equipment.xml - Per-culture companion equipment rosters
4. taom_wanderer_strings.xml - Backstory dialogue strings
"""

import xml.etree.ElementTree as ET
import os
import re
import copy
from collections import defaultdict

LOTRAOM_BASE = r"E:\LOTRAOMAssets\LOTRAOM_Jan_1_Patreon\Modules\LOTRAOM\ModuleData"
TAOM_BASE = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                         "Main", "_Module", "ModuleData")

# LOTRAOM source files
LOTRAOM_SPECIAL = os.path.join(LOTRAOM_BASE, "spspecialcharacters.xml")
LOTRAOM_SKILLS = os.path.join(LOTRAOM_BASE, "LOTRAOM_skill_sets.xml")
LOTRAOM_STRINGS = os.path.join(LOTRAOM_BASE, "LOTRAOM_wanderer_strings.xml")
LOTRAOM_EQUIPMENT = os.path.join(LOTRAOM_BASE, "LOTRAOM_sandbox_equipment_sets.xml")

# Kingdom config: culture_id -> equipment template replacement
# LOTRAOM wanderers all ref npc_companion_equipment_template_sturgia
# We replace with kingdom-specific templates
KINGDOM_CONFIG = {
    "gondor": {
        "culture": "Culture.gondor",
        "equip_template": "npc_companion_equipment_template_gondor",
    },
    "mordor": {
        "culture": "Culture.mordor",
        "equip_template": "npc_companion_equipment_template_mordor",
    },
    "gundabad": {
        "culture": "Culture.gundabad",
        "equip_template": "npc_companion_equipment_template_gundabad",
    },
    "isengard": {
        "culture": "Culture.isengard",
        "equip_template": "npc_companion_equipment_template_isengard",
    },
    "erebor": {
        "culture": "Culture.erebor",
        "equip_template": "npc_companion_equipment_template_erebor",
    },
    "rohan": {
        "culture": "Culture.vlandia",
        "equip_template": "npc_companion_equipment_template_rohan",
    },
}

# Equipment items per kingdom for companion equipment rosters
KINGDOM_EQUIPMENT = {
    "gondor": {
        "weapons": ["wm_gondor_sword_a01", "wm_gondor_sword_a03", "wm_gondor_sword_a05"],
        "shields": ["wm_gondor_shield_a02"],
        "helmets": ["gondor_helmet", "citidel_guard_helmet1", "citidel_guard_helmet3"],
        "bodies": ["gondor_chainmaila", "citidel_guard_armor1", "citidel_guard_armor2", "gondor_noble_jerkin_a"],
        "gloves": ["citidel_guard_gloves", "citidel_guard_bracers"],
        "boots": ["gondor_boots", "citidel_guard_boots"],
        "capes": ["citidel_guard_armor_pauldrons", "gondor_pauldrons"],
        "civ_bodies": ["gondor_noble_jerkin_a", "gondor_noble_jerkin_b", "ithilien_jerkin_short", "ithilien_jerkin_long"],
        "civ_boots": ["gondor_boots", "ithilien_boots"],
    },
    "mordor": {
        "weapons": ["isengard_1h_sword_b", "aserai_sword_3_t3"],
        "shields": [],
        "helmets": ["sk_uruk_mordor_helmet_a1", "sk_uruk_mordor_helmet_a2"],
        "bodies": ["sk_uruk_mordor_chainmail_light_a", "sk_uruk_mordor_chainmail_light_b", "sk_uruk_mordor_chainmail_medium_a1"],
        "gloves": ["sk_uruk_mordor_bracer_a1"],
        "boots": ["strapped_leather_boots"],
        "capes": [],
        "civ_bodies": ["sk_uruk_mordor_chainmail_light_a", "sk_uruk_mordor_chainmail_light_b"],
        "civ_boots": ["strapped_leather_boots"],
    },
    "gundabad": {
        "weapons": ["sturgia_axe_2_t2", "aserai_sword_3_t3"],
        "shields": [],
        "helmets": ["roughhide_cap", "northern_fur_cap"],
        "bodies": ["nordic_hauberk", "nordic_sloven", "nordic_tunic"],
        "gloves": ["leather_gloves"],
        "boots": ["mail_cavalier_boots", "leather_boots"],
        "capes": ["scarf"],
        "civ_bodies": ["nordic_tunic", "long_woolen_tunic", "nordic_sloven"],
        "civ_boots": ["woven_leather_boots", "leather_boots"],
    },
    "isengard": {
        "weapons": ["isengard_1h_sword_b", "isengard_pike_a"],
        "shields": ["wm_isengard_shield_a03"],
        "helmets": ["sk_uruk_hai_helmet_spear_med_a4", "sk_uruk_hai_helmet_sword_med_a4", "sk_uruk_hai_helmet_cross_a4"],
        "bodies": ["sk_uruk_hai_plate_light_a2", "sk_uruk_hai_plate_light_b2", "sk_uruk_hai_chainmail_a1"],
        "gloves": ["sk_uruk_hai_bracer_light_a1", "sk_uruk_hai_bracer_light_a2"],
        "boots": ["sk_uruk_hai_shoes_a1"],
        "capes": [],
        "civ_bodies": ["sk_uruk_hai_chainmail_a1", "sk_uruk_hai_chainmail_a2"],
        "civ_boots": ["sk_uruk_hai_shoes_a1"],
    },
    "erebor": {
        "weapons": ["vlandia_sword_1_t2", "aserai_sword_3_t3"],
        "shields": ["fortified_kite_shield"],
        "helmets": ["leather_cap", "northern_fur_cap"],
        "bodies": ["padded_leather_shirt", "footmans_tunic", "tunic_with_shoulder_pads"],
        "gloves": ["leather_gloves"],
        "boots": ["strapped_shoes", "wrapped_leather_boots"],
        "capes": ["scarf"],
        "civ_bodies": ["tunic_with_shoulder_pads", "padded_leather_shirt", "footmans_tunic"],
        "civ_boots": ["strapped_shoes", "wrapped_leather_boots"],
    },
    "rohan": {
        "weapons": ["vlandia_sword_1_t2", "vlandia_sword_2_t3", "wm_rohan_shield_a01_gsw"],
        "shields": ["wm_rohan_shield_a01_gsw"],
        "helmets": ["cts_rohan_helmet1", "cts_rohan_helmet2"],
        "bodies": ["rohan_militia_armour_a", "rohan_militia_armour_b", "rohan_militia_armour_c", "cts_rohan_armor3"],
        "gloves": ["leather_gloves"],
        "boots": ["strapped_shoes", "wrapped_leather_boots"],
        "capes": [],
        "civ_bodies": ["padded_leather_shirt", "gambeson_b", "footmans_tunic"],
        "civ_boots": ["strapped_shoes", "wrapped_leather_boots"],
    },
}


def extract_wanderer_npcs():
    """Extract wanderer NPCCharacter entries from LOTRAOM spspecialcharacters.xml"""
    tree = ET.parse(LOTRAOM_SPECIAL)
    root = tree.getroot()

    wanderers = []
    for npc in root.findall("NPCCharacter"):
        npc_id = npc.get("id", "")
        if npc_id.startswith("spc_wanderer_"):
            wanderers.append(npc)

    print(f"Found {len(wanderers)} LOTRAOM wanderer NPCs")

    # Group by kingdom
    by_kingdom = defaultdict(list)
    for w in wanderers:
        wid = w.get("id")
        # Extract kingdom from id: spc_wanderer_{kingdom}_{number}
        parts = wid.split("_")
        # kingdom is everything between "wanderer" and the last numeric part
        kingdom = "_".join(parts[2:-1])
        by_kingdom[kingdom].append(w)

    for k, v in sorted(by_kingdom.items()):
        print(f"  {k}: {len(v)} wanderers")

    return wanderers, by_kingdom


def extract_wanderer_skills():
    """Extract wanderer skill sets from LOTRAOM skill sets file"""
    tree = ET.parse(LOTRAOM_SKILLS)
    root = tree.getroot()

    skill_sets = []
    for ss in root.findall("SkillSet"):
        ss_id = ss.get("id", "")
        if "spc_wanderer_" in ss_id:
            skill_sets.append(ss)

    print(f"Found {len(skill_sets)} LOTRAOM wanderer skill sets")
    return skill_sets


def extract_wanderer_strings():
    """Extract wanderer backstory strings from LOTRAOM"""
    tree = ET.parse(LOTRAOM_STRINGS)
    root = tree.getroot()

    strings = []
    for s in root.findall("string"):
        sid = s.get("id", "")
        if "spc_wanderer_" in sid:
            strings.append(s)

    print(f"Found {len(strings)} LOTRAOM wanderer strings")
    return strings


def generate_equipment_roster(kingdom, config):
    """Generate a companion equipment roster for a kingdom"""
    items = KINGDOM_EQUIPMENT[kingdom]
    template_id = config["equip_template"]
    culture = config["culture"]

    lines = []
    lines.append(f'\t<EquipmentRoster id="{template_id}" culture="{culture}">')

    # Generate 10-15 equipment sets mixing available items
    weapons = items["weapons"]
    shields = items.get("shields", [])
    helmets = items["helmets"]
    bodies = items["bodies"]
    gloves_list = items["gloves"]
    boots_list = items["boots"]
    capes = items.get("capes", [])

    set_count = 0
    for body_idx, body in enumerate(bodies):
        for weapon in weapons[:2]:  # 2 weapons * N bodies
            lines.append('\t\t<EquipmentSet>')
            lines.append(f'\t\t\t<Equipment slot="Item0" id="Item.{weapon}" />')
            if shields and body_idx % 2 == 0:
                lines.append(f'\t\t\t<Equipment slot="Item1" id="Item.{shields[0]}" />')
            if helmets:
                lines.append(f'\t\t\t<Equipment slot="Head" id="Item.{helmets[body_idx % len(helmets)]}" />')
            lines.append(f'\t\t\t<Equipment slot="Body" id="Item.{body}" />')
            if capes and body_idx % 3 == 0:
                lines.append(f'\t\t\t<Equipment slot="Cape" id="Item.{capes[body_idx % len(capes)]}" />')
            if gloves_list:
                lines.append(f'\t\t\t<Equipment slot="Gloves" id="Item.{gloves_list[body_idx % len(gloves_list)]}" />')
            lines.append(f'\t\t\t<Equipment slot="Leg" id="Item.{boots_list[body_idx % len(boots_list)]}" />')
            lines.append('\t\t</EquipmentSet>')
            set_count += 1
            if set_count >= 12:
                break
        if set_count >= 12:
            break

    # Add civilian sets
    civ_bodies = items["civ_bodies"]
    civ_boots = items["civ_boots"]
    for i, civ_body in enumerate(civ_bodies[:3]):
        lines.append('\t\t<EquipmentSet equipmentType="Civilian">')
        if weapons:
            lines.append(f'\t\t\t<Equipment slot="Item0" id="Item.{weapons[0]}" />')
        lines.append(f'\t\t\t<Equipment slot="Body" id="Item.{civ_body}" />')
        lines.append(f'\t\t\t<Equipment slot="Leg" id="Item.{civ_boots[i % len(civ_boots)]}" />')
        lines.append('\t\t</EquipmentSet>')

    lines.append('\t</EquipmentRoster>')
    return "\n".join(lines)


def write_wanderer_npcs(wanderers_by_kingdom, output_path):
    """Write converted wanderer NPCs to output file"""
    lines = ['<?xml version="1.0" encoding="utf-8"?>', '<NPCCharacters>', '']

    for kingdom in ["gondor", "mordor", "gundabad", "isengard", "erebor", "rohan"]:
        if kingdom not in wanderers_by_kingdom:
            continue

        config = KINGDOM_CONFIG[kingdom]
        equip_template = config["equip_template"]

        lines.append(f'\t<!-- ==================== {kingdom.upper()} WANDERERS ==================== -->')

        for npc in wanderers_by_kingdom[kingdom]:
            npc_copy = copy.deepcopy(npc)

            # Replace equipment template references
            equips = npc_copy.find("Equipments")
            if equips is not None:
                for es in equips.findall("EquipmentSet"):
                    old_id = es.get("id", "")
                    if "npc_companion_equipment_template_" in old_id:
                        es.set("id", equip_template)

            # Serialize the NPC element
            npc_str = ET.tostring(npc_copy, encoding="unicode")
            # Pretty-print with tabs
            npc_str = format_npc_xml(npc_copy, equip_template)
            lines.append(npc_str)

        lines.append('')

    lines.append('</NPCCharacters>')

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print(f"Wrote {output_path}")


def format_npc_xml(npc, equip_template):
    """Format an NPCCharacter element as indented XML string"""
    attrs = []
    # Order attributes nicely
    attr_order = ["id", "race", "name", "voice", "age", "is_template", "default_group",
                  "is_hero", "culture", "occupation", "skill_template"]

    for attr in attr_order:
        val = npc.get(attr)
        if val is not None:
            attrs.append(f'\t\t{attr}="{val}"')

    lines = ['\t<NPCCharacter']
    lines.extend(attrs)
    lines[-1] += '>'

    # Face
    face = npc.find("face")
    if face is not None:
        fkt = face.find("face_key_template")
        if fkt is not None:
            lines.append('\t\t<face>')
            lines.append(f'\t\t\t<face_key_template value="{fkt.get("value")}" />')
            lines.append('\t\t</face>')
        else:
            # Has BodyProperties - convert to face_key_template based on culture
            culture = npc.get("culture", "")
            body_prop = get_body_property_for_culture(culture)
            lines.append('\t\t<face>')
            lines.append(f'\t\t\t<face_key_template value="{body_prop}" />')
            lines.append('\t\t</face>')

    # Traits
    traits = npc.find("Traits")
    if traits is not None and len(traits) > 0:
        lines.append('\t\t<Traits>')
        for trait in traits:
            lines.append(f'\t\t\t<Trait id="{trait.get("id")}" value="{trait.get("value")}" />')
        lines.append('\t\t</Traits>')

    # Equipment
    lines.append('\t\t<Equipments>')
    lines.append(f'\t\t\t<EquipmentSet id="{equip_template}" equipmentType="Civilian" />')
    lines.append(f'\t\t\t<EquipmentSet id="{equip_template}" />')
    lines.append('\t\t</Equipments>')

    lines.append('\t</NPCCharacter>')
    return "\n".join(lines)


def get_body_property_for_culture(culture):
    """Map culture to BodyProperty"""
    mapping = {
        "Culture.gondor": "BodyProperty.fighter_gondor",
        "Culture.mordor": "BodyProperty.fighter_uruk_mordor",
        "Culture.gundabad": "BodyProperty.fighter_gundabad",
        "Culture.isengard": "BodyProperty.fighter_empire",
        "Culture.erebor": "BodyProperty.fighter_erebor",
        "Culture.vlandia": "BodyProperty.fighter_rohan",
    }
    return mapping.get(culture, "BodyProperty.fighter_empire")


def write_skill_sets(skill_sets, output_path):
    """Write skill sets to output file"""
    lines = ['<?xml version="1.0" encoding="utf-8"?>', '<SkillSets>', '']

    # Group by kingdom
    by_kingdom = defaultdict(list)
    for ss in skill_sets:
        ssid = ss.get("id")
        parts = ssid.replace("_skills", "").split("_")
        kingdom = "_".join(parts[2:-1])
        by_kingdom[kingdom].append(ss)

    for kingdom in ["gondor", "mordor", "gundabad", "isengard", "erebor", "rohan"]:
        if kingdom not in by_kingdom:
            continue

        lines.append(f'\t<!-- {kingdom.upper()} wanderer skills -->')
        for ss in by_kingdom[kingdom]:
            lines.append(f'\t<SkillSet id="{ss.get("id")}">')
            for skill in ss:
                lines.append(f'\t\t<skill id="{skill.get("id")}" value="{skill.get("value")}" />')
            lines.append('\t</SkillSet>')
        lines.append('')

    lines.append('</SkillSets>')

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print(f"Wrote {output_path}")


def write_equipment(output_path):
    """Write per-culture companion equipment rosters"""
    lines = ['<?xml version="1.0" encoding="utf-8"?>', '<EquipmentRosters>', '']

    for kingdom in ["gondor", "mordor", "gundabad", "isengard", "erebor", "rohan"]:
        config = KINGDOM_CONFIG[kingdom]
        lines.append(f'\t<!-- {kingdom.upper()} companion equipment -->')
        lines.append(generate_equipment_roster(kingdom, config))
        lines.append('')

    lines.append('</EquipmentRosters>')

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print(f"Wrote {output_path}")


def write_strings(strings, output_path):
    """Write wanderer backstory strings"""
    lines = ['<?xml version="1.0" encoding="utf-8"?>', '<strings>', '']

    # Group by kingdom
    by_kingdom = defaultdict(list)
    for s in strings:
        sid = s.get("id")
        # Extract kingdom: backstory_a.spc_wanderer_{kingdom}_{num}
        match = re.search(r'spc_wanderer_([a-z]+)_\d+', sid)
        if match:
            by_kingdom[match.group(1)].append(s)

    for kingdom in ["gondor", "mordor", "gundabad", "isengard", "erebor", "rohan"]:
        if kingdom not in by_kingdom:
            continue

        lines.append(f'\t<!-- {kingdom.upper()} wanderer backstories -->')
        for s in by_kingdom[kingdom]:
            text = s.get("text", "")
            # Escape XML special chars in text attribute
            text = text.replace("&", "&amp;").replace('"', "&quot;").replace("<", "&lt;").replace(">", "&gt;")
            # But the text already has these escaped from the source, so undo double-escape
            text = text.replace("&amp;amp;", "&amp;")
            text = text.replace("&amp;quot;", "&quot;")
            text = text.replace("&amp;lt;", "&lt;")
            text = text.replace("&amp;gt;", "&gt;")
            lines.append(f'\t<string id="{s.get("id")}" text="{text}" />')
        lines.append('')

    lines.append('</strings>')

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print(f"Wrote {output_path}")


def main():
    print("=== LOTRAOM Wanderer Extraction ===\n")

    # 1. Extract wanderer NPCs
    wanderers, by_kingdom = extract_wanderer_npcs()

    # 2. Extract skill sets
    skill_sets = extract_wanderer_skills()

    # 3. Extract backstory strings
    strings = extract_wanderer_strings()

    # 4. Write output files
    print("\n=== Writing output files ===\n")

    write_wanderer_npcs(
        by_kingdom,
        os.path.join(TAOM_BASE, "taom_wanderers.xml")
    )

    write_skill_sets(
        skill_sets,
        os.path.join(TAOM_BASE, "taom_wanderer_skill_sets.xml")
    )

    write_equipment(
        os.path.join(TAOM_BASE, "taom_wanderer_equipment.xml")
    )

    write_strings(
        strings,
        os.path.join(TAOM_BASE, "taom_wanderer_strings.xml")
    )

    print("\n=== Summary ===")
    print(f"Wanderer NPCs: {len(wanderers)}")
    print(f"Skill sets: {len(skill_sets)}")
    print(f"Backstory strings: {len(strings)}")
    print(f"Equipment rosters: {len(KINGDOM_CONFIG)}")


if __name__ == "__main__":
    main()
