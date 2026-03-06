#!/usr/bin/env python3
"""Generate spcultures.xslt from LOTRAOM reference data."""

import xml.etree.ElementTree as ET
import re

# Parse LOTRAOM reference
tree = ET.parse('E:/LOTRAOMAssets/LOTRAOM_Jan_1_Patreon/Modules/LOTRAOM/ModuleData/LOTRAOM_spcultures.xml')
root = tree.getroot()

# Configuration for each kingdom
kingdoms = {
    'vlandia': {
        'taom_name': 'Rohirrim', 'kingdom': 'rohan',
        'militia_melee': 'rohan_militia_spearman',
        'militia_ranged': 'rohan_militia_archer',
        'militia_vet_melee': 'rohan_militia_veteran_spearman',
        'militia_vet_ranged': 'rohan_militia_veteran_archer',
        'npc_suffix': 'rohan',
        'bat_roster': 'rohan_bat_template_medium',
        'civ_roster': 'rohan_civ_template_default',
    },
    'empire': {
        'taom_name': 'Dunlending', 'kingdom': 'dunland',
        'militia_melee': 'dunland_militia_spearman',
        'militia_ranged': 'dunland_militia_archer',
        'militia_vet_melee': 'dunland_militia_veteran_spearman',
        'militia_vet_ranged': 'dunland_militia_veteran_archer',
        'npc_suffix': 'dunland',
        'bat_roster': 'dunland_bat_template_medium',
        'civ_roster': 'dunland_civ_template_default',
    },
    'aserai': {
        'taom_name': 'Haradrim', 'kingdom': 'harad',
        'militia_melee': 'harad_militia_spearman',
        'militia_ranged': 'harad_militia_archer',
        'militia_vet_melee': 'harad_militia_veteran_spearman',
        'militia_vet_ranged': 'harad_militia_veteran_archer',
        'npc_suffix': 'harad',
        'bat_roster': 'harad_bat_template_medium',
        'civ_roster': 'harad_civ_template_default',
    },
    'khuzait': {
        'taom_name': 'Easterling', 'kingdom': 'rhun',
        'militia_melee': 'rhun_militia_spearman',
        'militia_ranged': 'rhun_militia_archer',
        'militia_vet_melee': 'rhun_militia_veteran_spearman',
        'militia_vet_ranged': 'rhun_militia_veteran_archer',
        'npc_suffix': 'rhun',
        'bat_roster': 'rhun_bat_template_medium',
        'civ_roster': 'rhun_civ_template_default',
    },
}

KEEP_ATTRS = {'id', 'is_main_culture', 'can_have_settlement', 'town_edge_number',
              'faction_banner_key', 'default_face_key', 'prosperity_bonus',
              'duel_preset_equipment_roster'}

NPC_ATTRS = [
    'tournament_master', 'villager', 'caravan_master', 'armed_trader',
    'caravan_guard', 'veteran_caravan_guard', 'prison_guard', 'guard',
    'blacksmith', 'weaponsmith', 'townswoman', 'townswoman_infant',
    'townswoman_child', 'townswoman_teenager', 'townsman', 'townsman_infant',
    'townsman_child', 'townsman_teenager', 'village_woman', 'villager_male_child',
    'villager_male_teenager', 'villager_female_child', 'villager_female_teenager',
    'ransom_broker', 'gangleader_bodyguard', 'merchant_notary', 'artisan_notary',
    'preacher_notary', 'rural_notable_notary', 'shop_worker', 'tavernkeeper',
    'taverngamehost', 'musician', 'tavern_wench', 'armorer', 'horseMerchant',
    'barber', 'merchant', 'beggar', 'female_beggar', 'female_dancer',
    'gear_practice_dummy', 'weapon_practice_stage_1', 'weapon_practice_stage_2',
    'weapon_practice_stage_3', 'gear_dummy'
]

NPC_ID_MAP = {
    'villager_male_child': 'villager_child',
    'villager_male_teenager': 'villager_teenager',
    'villager_female_child': 'village_woman_child',
    'villager_female_teenager': 'village_woman_teenager',
}


def esc(text):
    """Escape XML special chars for XSLT attribute values."""
    if text is None:
        return ''
    return text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('"', '&quot;')


def generate_xslt_template(culture_id, config, culture_elem):
    kingdom = config['kingdom']
    npc_suffix = config['npc_suffix']

    replaced_attrs = set()
    replaced_attrs.add('name')
    replaced_attrs.add('text')
    replaced_attrs.add('color')
    replaced_attrs.add('color2')

    for attr in culture_elem.attrib:
        if attr not in KEEP_ATTRS:
            replaced_attrs.add(attr)
    for attr in NPC_ATTRS:
        replaced_attrs.add(attr)
    for extra in ['basic_troop', 'elite_basic_troop', 'melee_militia_troop', 'ranged_militia_troop',
                   'melee_elite_militia_troop', 'ranged_elite_militia_troop',
                   'villager_party_template', 'default_party_template',
                   'caravan_party_template', 'elite_caravan_party_template',
                   'militia_party_template', 'rebels_party_template', 'vassal_reward_party_template',
                   'encounter_background_mesh', 'board_game_type',
                   'default_battle_equipment_roster', 'default_civilian_equipment_roster']:
        replaced_attrs.add(extra)

    excl = " and ".join(f"local-name() != '{a}'" for a in sorted(replaced_attrs))

    L = []
    L.append(f'\t<!-- Rename {culture_id} to {config["taom_name"]} -->')
    L.append(f'\t<xsl:template match="Culture[@id=\'{culture_id}\']">')
    L.append(f'\t\t<xsl:copy>')
    L.append(f'\t\t\t<xsl:apply-templates select="@*[{excl}]"/>')
    L.append(f'')
    L.append(f'\t\t\t<!-- Name and description -->')
    L.append(f'\t\t\t<xsl:attribute name="name">{esc(culture_elem.get("name"))}</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="text">{esc(culture_elem.get("text"))}</xsl:attribute>')

    color = culture_elem.get('color')
    color2 = culture_elem.get('color2')
    if color:
        L.append(f'\t\t\t<xsl:attribute name="color">{color}</xsl:attribute>')
    if color2:
        L.append(f'\t\t\t<xsl:attribute name="color2">{color2}</xsl:attribute>')

    L.append(f'')
    L.append(f'\t\t\t<!-- Troop references -->')
    L.append(f'\t\t\t<xsl:attribute name="basic_troop">{culture_elem.get("basic_troop")}</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="elite_basic_troop">{culture_elem.get("elite_basic_troop")}</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="melee_militia_troop">NPCCharacter.{config["militia_melee"]}</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="ranged_militia_troop">NPCCharacter.{config["militia_ranged"]}</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="melee_elite_militia_troop">NPCCharacter.{config["militia_vet_melee"]}</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="ranged_elite_militia_troop">NPCCharacter.{config["militia_vet_ranged"]}</xsl:attribute>')

    L.append(f'')
    L.append(f'\t\t\t<!-- Party templates -->')
    party_attrs = ['villager_party_template', 'default_party_template',
                   'caravan_party_template', 'elite_caravan_party_template',
                   'militia_party_template', 'rebels_party_template', 'vassal_reward_party_template']
    for attr in party_attrs:
        val = culture_elem.get(attr)
        if val:
            L.append(f'\t\t\t<xsl:attribute name="{attr}">{val}</xsl:attribute>')
        else:
            if attr == 'villager_party_template':
                L.append(f'\t\t\t<xsl:attribute name="{attr}">PartyTemplate.villager_{kingdom}_template</xsl:attribute>')
            elif attr == 'militia_party_template':
                L.append(f'\t\t\t<xsl:attribute name="{attr}">PartyTemplate.militia_{kingdom}_template</xsl:attribute>')
            elif attr == 'rebels_party_template':
                L.append(f'\t\t\t<xsl:attribute name="{attr}">PartyTemplate.rebels_{kingdom}_template</xsl:attribute>')
            elif attr == 'vassal_reward_party_template':
                L.append(f'\t\t\t<xsl:attribute name="{attr}">PartyTemplate.vassal_reward_troops_{kingdom}</xsl:attribute>')

    L.append(f'')
    L.append(f'\t\t\t<!-- Encounter/display -->')
    enc = culture_elem.get('encounter_background_mesh', f'encounter_{culture_id}')
    bg = culture_elem.get('board_game_type', 'Tablut')
    L.append(f'\t\t\t<xsl:attribute name="encounter_background_mesh">{enc}</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="board_game_type">{bg}</xsl:attribute>')

    L.append(f'')
    L.append(f'\t\t\t<!-- Equipment rosters -->')
    L.append(f'\t\t\t<xsl:attribute name="default_battle_equipment_roster">EquipmentRoster.{config["bat_roster"]}_a</xsl:attribute>')
    L.append(f'\t\t\t<xsl:attribute name="default_civilian_equipment_roster">EquipmentRoster.{config["civ_roster"]}_a</xsl:attribute>')

    L.append(f'')
    L.append(f'\t\t\t<!-- NPC references -->')
    for attr in NPC_ATTRS:
        npc_id = NPC_ID_MAP.get(attr, attr)
        L.append(f'\t\t\t<xsl:attribute name="{attr}">NPCCharacter.{npc_id}_{npc_suffix}</xsl:attribute>')

    # Child elements
    L.append(f'')
    L.append(f'\t\t\t<!-- Vassal reward items -->')
    vr = culture_elem.find('vassal_reward_items')
    if vr is not None:
        L.append(f'\t\t\t<vassal_reward_items>')
        for item in vr.findall('item'):
            L.append(f'\t\t\t\t<item id="{item.get("id")}" />')
        L.append(f'\t\t\t</vassal_reward_items>')

    L.append(f'\t\t\t<!-- Banner bearer weapons -->')
    bb = culture_elem.find('banner_bearer_replacement_weapons')
    if bb is not None:
        L.append(f'\t\t\t<banner_bearer_replacement_weapons>')
        for item in bb.findall('item'):
            L.append(f'\t\t\t\t<item id="{item.get("id")}" />')
        L.append(f'\t\t\t</banner_bearer_replacement_weapons>')

    L.append(f'\t\t\t<!-- Default policies -->')
    dp = culture_elem.find('default_policies')
    if dp is not None:
        L.append(f'\t\t\t<default_policies>')
        for pol in dp.findall('policy'):
            L.append(f'\t\t\t\t<policy id="{pol.get("id")}" />')
        L.append(f'\t\t\t</default_policies>')

    L.append(f'')
    L.append(f'\t\t\t<!-- LOTR names -->')
    mn = culture_elem.find('male_names')
    if mn is not None:
        L.append(f'\t\t\t<male_names>')
        for name in mn.findall('name'):
            L.append(f'\t\t\t\t<name name="{esc(name.get("name"))}" />')
        L.append(f'\t\t\t</male_names>')

    fn = culture_elem.find('female_names')
    if fn is not None:
        L.append(f'\t\t\t<female_names>')
        for name in fn.findall('name'):
            L.append(f'\t\t\t\t<name name="{esc(name.get("name"))}" />')
        L.append(f'\t\t\t</female_names>')

    cn = culture_elem.find('clan_names')
    if cn is not None:
        L.append(f'\t\t\t<clan_names>')
        for name in cn.findall('name'):
            L.append(f'\t\t\t\t<name name="{esc(name.get("name"))}" />')
        L.append(f'\t\t\t</clan_names>')

    L.append(f'')
    L.append(f'\t\t\t<!-- Copy cultural_feats, possible_clan_banner_icon_ids, notable_and_wanderer_templates -->')
    L.append(f'\t\t\t<xsl:apply-templates select="cultural_feats|possible_clan_banner_icon_ids|notable_and_wanderer_templates"/>')

    L.append(f'\t\t</xsl:copy>')
    L.append(f'\t</xsl:template>')

    return '\n'.join(L)


# Find culture elements
cultures_data = {}
for culture in root.findall('.//Culture'):
    cid = culture.get('id')
    if cid in kingdoms:
        cultures_data[cid] = culture

# Generate full XSLT
xslt_header = """<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
\t<!-- Identity transformation - copies everything by default -->
\t<xsl:output omit-xml-declaration="no" indent="yes"/>

\t<xsl:template match="@*|node()">
\t\t<xsl:copy>
\t\t\t<xsl:apply-templates select="@*|node()"/>
\t\t</xsl:copy>
\t</xsl:template>
"""

xslt_footer = '\n</xsl:stylesheet>\n'

parts = [xslt_header]
for cid in ['empire', 'aserai', 'vlandia', 'khuzait']:
    if cid in cultures_data:
        parts.append('')
        parts.append(generate_xslt_template(cid, kingdoms[cid], cultures_data[cid]))

# Also handle sturgia and battania (just name/text like before)
sturgia_text = "The Bardings of Dale, named for Bard the Bowman, are a proud and industrious people who rose to prominence after reclaiming their homeland from the shadow of Smaug. Nestled between the Lonely Mountain and the Long Lake, Dale thrives as a hub of trade and culture. Known for their resilience and craftsmanship, the Bardings excel in forging weapons and armor, rivaling even the Dwarves of Erebor. Their armies, composed of disciplined archers, stalwart swordsmen, and agile skirmishers, defend their lands with fierce determination. United under noble leaders, the Bardings are ever watchful, guarding against the encroaching darkness and preserving their rich heritage."
battania_text = "The Variags of Khand are a fierce and warlike people, hailing from the dry and rugged lands east of Mordor. Known for their mercenary prowess and loyalty to Sauron, the Variags fight with unmatched ferocity. They ride swift warhorses into battle, wielding curved blades and long spears with deadly precision. Their bronze and crimson armor, adorned with intricate designs, reflects their proud and martial heritage. Divided into tribes and clans, the Variags unite under powerful warlords, bringing fear and chaos to the enemies of the Dark Lord."

parts.append(f"""
\t<!-- Rename sturgia to Barding -->
\t<xsl:template match="Culture[@id='sturgia']">
\t\t<xsl:copy>
\t\t\t<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
\t\t\t<xsl:attribute name="name">{{=TAOM_sturgia_culture}}Barding</xsl:attribute>
\t\t\t<xsl:attribute name="text">{{=TAOM_sturgia_desc}}{sturgia_text}</xsl:attribute>
\t\t\t<xsl:apply-templates select="node()"/>
\t\t</xsl:copy>
\t</xsl:template>

\t<!-- Rename battania to Variag -->
\t<xsl:template match="Culture[@id='battania']">
\t\t<xsl:copy>
\t\t\t<xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'text']"/>
\t\t\t<xsl:attribute name="name">{{=TAOM_battania_culture}}Variag</xsl:attribute>
\t\t\t<xsl:attribute name="text">{{=TAOM_battania_desc}}{battania_text}</xsl:attribute>
\t\t\t<xsl:apply-templates select="node()"/>
\t\t</xsl:copy>
\t</xsl:template>""")

parts.append(xslt_footer)

xslt_content = '\n'.join(parts)

# Write the file
with open('c:/Users/mikew/source/repos/TAOM/Main/_Module/ModuleData/spcultures.xslt', 'w', encoding='utf-8') as f:
    f.write(xslt_content)

print(f"Generated XSLT: {len(xslt_content)} chars")
print(f"Culture templates: {sum(1 for c in ['empire', 'aserai', 'vlandia', 'khuzait'] if c in cultures_data)}")

template_count = len(re.findall(r'xsl:template match', xslt_content))
attr_count = len(re.findall(r'xsl:attribute name=', xslt_content))
print(f"Templates: {template_count}, Attributes set: {attr_count}")
