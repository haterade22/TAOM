"""
Generate Batch 2 wanderer data for 8 kingdoms without LOTRAOM data.
Appends to existing taom_wanderers.xml, taom_wanderer_skill_sets.xml,
taom_wanderer_equipment.xml, taom_wanderer_strings.xml.
"""
import random
import os

random.seed(42)  # Reproducible output

BASE_DIR = r"c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData"

# Kingdom configurations
KINGDOMS = {
    'rivendell': {
        'culture_id': 'rivendell',
        'body_property': 'BodyProperty.fighter_rivendell',
        'race': None,
        'voices': ['softspoken', 'earnest', 'ironic', 'softspoken', 'earnest', 'curt', 'softspoken', 'ironic', 'earnest', 'softspoken'],
        'ages': [45, 38, 52, 30, 41, 35, 48, 33, 40, 37],
    },
    'mirkwood': {
        'culture_id': 'mirkwood',
        'body_property': 'BodyProperty.fighter_mirkwood',
        'race': None,
        'voices': ['curt', 'earnest', 'ironic', 'softspoken', 'curt', 'earnest', 'softspoken', 'ironic', 'curt', 'earnest'],
        'ages': [35, 42, 28, 50, 33, 45, 30, 38, 41, 36],
    },
    'lothlorien': {
        'culture_id': 'lothlorien',
        'body_property': 'BodyProperty.fighter_rivendell',  # Use rivendell body for now
        'race': None,
        'voices': ['softspoken', 'softspoken', 'earnest', 'ironic', 'softspoken', 'earnest', 'softspoken', 'ironic', 'earnest', 'softspoken'],
        'ages': [50, 40, 55, 35, 48, 42, 53, 38, 44, 47],
    },
    'dolguldur': {
        'culture_id': 'dolguldur',
        'body_property': 'BodyProperty.fighter_empire',
        'race': 'orc',
        'voices': ['curt', 'curt', 'ironic', 'curt', 'earnest', 'curt', 'ironic', 'curt', 'earnest', 'curt'],
        'ages': [25, 30, 22, 28, 35, 24, 32, 27, 29, 26],
    },
    'dunland': {
        'culture_id': 'empire',
        'body_property': 'BodyProperty.fighter_battania',
        'race': None,
        'voices': ['curt', 'earnest', 'ironic', 'curt', 'earnest', 'softspoken', 'curt', 'ironic', 'earnest', 'curt'],
        'ages': [28, 35, 22, 40, 30, 25, 33, 27, 38, 31],
    },
    'harad': {
        'culture_id': 'aserai',
        'body_property': 'BodyProperty.fighter_haradrim',
        'race': None,
        'voices': ['earnest', 'curt', 'ironic', 'softspoken', 'earnest', 'curt', 'ironic', 'earnest', 'softspoken', 'curt'],
        'ages': [30, 25, 35, 28, 40, 22, 33, 27, 45, 32],
    },
    'rhun': {
        'culture_id': 'khuzait',
        'body_property': 'BodyProperty.fighter_rhun',
        'race': None,
        'voices': ['curt', 'earnest', 'softspoken', 'ironic', 'curt', 'earnest', 'curt', 'ironic', 'softspoken', 'earnest'],
        'ages': [32, 28, 38, 25, 35, 30, 27, 42, 33, 29],
    },
    'umbar': {
        'culture_id': 'umbar',
        'body_property': 'BodyProperty.fighter_gondor',
        'race': None,
        'voices': ['ironic', 'curt', 'earnest', 'ironic', 'curt', 'softspoken', 'ironic', 'earnest', 'curt', 'ironic'],
        'ages': [30, 35, 25, 28, 40, 33, 27, 38, 32, 29],
    },
}

# Wanderer names per kingdom (epithet/surname style)
WANDERER_NAMES = {
    'rivendell': [
        'Erestelionath', 'Calenglorionath', 'Noldorionath',
        'Lindonionath', 'Aranwionath', 'Celebrindionath',
        'Ithilionath', 'Gildorionath', 'Peredhilionath', 'Earendilionath'
    ],
    'mirkwood': [
        'Tawarionath', 'Galadhelionath', 'Lasgalionath',
        'Thranduilionath', 'Legolionath', 'Mornelionath',
        'Falasgalionath', 'Celebionath', 'Aranionath', 'Brethilionath'
    ],
    'lothlorien': [
        'Carasionath', 'Nimrothionath', 'Mallorionath',
        'Amrothionath', 'Celebornionath', 'Galadhrionath',
        'Nimlodelionath', 'Cerinionath', 'Haldhirionath', 'Oropherionath'
    ],
    'dolguldur': [
        'Ghashnak', 'Krimgul', 'Durbatuk',
        'Nazgash', 'Grimbarg', 'Sharkul',
        'Azgoth', 'Lugdush', 'Krimpash', 'Gorbag'
    ],
    'dunland': [
        'Breghorn', 'Cerdic', 'Dunhere',
        'Wulfgar', 'Eorlund', 'Grimbold',
        'Haldred', 'Brychan', 'Morcar', 'Thuringwethil'
    ],
    'harad': [
        'al-Harushan', 'ibn Khamul', 'al-Suthek',
        'ibn Marushan', 'al-Fahran', 'ibn Zarkesh',
        'al-Rashadin', 'ibn Suladan', 'al-Makhtar', 'ibn Tarakan'
    ],
    'rhun': [
        'Khamakul', 'Dordagan', 'Balchoth',
        'Wainagar', 'Khuldan', 'Bortagan',
        'Vardamir', 'Ulgarion', 'Khandagar', 'Rhunagar'
    ],
    'umbar': [
        'Castamir', 'Sangahyando', 'Angamaitë',
        'Fuinur', 'Herumor', 'Earnil',
        'Pelendur', 'Falastur', 'Hyarmendacil', 'Ciryandil'
    ],
}

# 10 archetype skill distributions (Engineer, Warrior, Scout, Healer, Trader, Rogue, Tactician, Smith, Cavalryman, Archer)
SKILL_ARCHETYPES = [
    # Engineer/Scholar
    {'OneHanded': 100, 'TwoHanded': 60, 'Polearm': 80, 'Bow': 60, 'Crossbow': 40, 'Throwing': 40,
     'Riding': 80, 'Athletics': 100, 'Roguery': 40, 'Crafting': 120, 'Scouting': 120,
     'Tactics': 140, 'Charm': 100, 'Leadership': 120, 'Trade': 100, 'Steward': 160, 'Medicine': 140, 'Engineering': 220},
    # Warrior/Champion
    {'OneHanded': 220, 'TwoHanded': 200, 'Polearm': 180, 'Bow': 80, 'Crossbow': 60, 'Throwing': 100,
     'Riding': 120, 'Athletics': 200, 'Roguery': 40, 'Crafting': 60, 'Scouting': 80,
     'Tactics': 140, 'Charm': 60, 'Leadership': 160, 'Trade': 40, 'Steward': 80, 'Medicine': 60, 'Engineering': 40},
    # Scout/Ranger
    {'OneHanded': 140, 'TwoHanded': 80, 'Polearm': 100, 'Bow': 200, 'Crossbow': 60, 'Throwing': 120,
     'Riding': 160, 'Athletics': 180, 'Roguery': 100, 'Crafting': 60, 'Scouting': 220,
     'Tactics': 120, 'Charm': 60, 'Leadership': 80, 'Trade': 60, 'Steward': 80, 'Medicine': 100, 'Engineering': 60},
    # Healer/Herbalist
    {'OneHanded': 80, 'TwoHanded': 40, 'Polearm': 60, 'Bow': 80, 'Crossbow': 40, 'Throwing': 40,
     'Riding': 80, 'Athletics': 100, 'Roguery': 40, 'Crafting': 100, 'Scouting': 120,
     'Tactics': 80, 'Charm': 140, 'Leadership': 100, 'Trade': 120, 'Steward': 140, 'Medicine': 220, 'Engineering': 100},
    # Trader/Merchant
    {'OneHanded': 100, 'TwoHanded': 60, 'Polearm': 80, 'Bow': 60, 'Crossbow': 60, 'Throwing': 40,
     'Riding': 120, 'Athletics': 100, 'Roguery': 80, 'Crafting': 80, 'Scouting': 100,
     'Tactics': 80, 'Charm': 180, 'Leadership': 120, 'Trade': 220, 'Steward': 180, 'Medicine': 80, 'Engineering': 60},
    # Rogue/Thief
    {'OneHanded': 180, 'TwoHanded': 100, 'Polearm': 80, 'Bow': 120, 'Crossbow': 100, 'Throwing': 160,
     'Riding': 100, 'Athletics': 180, 'Roguery': 220, 'Crafting': 60, 'Scouting': 160,
     'Tactics': 100, 'Charm': 120, 'Leadership': 60, 'Trade': 100, 'Steward': 60, 'Medicine': 60, 'Engineering': 40},
    # Tactician/Commander
    {'OneHanded': 160, 'TwoHanded': 120, 'Polearm': 140, 'Bow': 100, 'Crossbow': 80, 'Throwing': 80,
     'Riding': 140, 'Athletics': 140, 'Roguery': 60, 'Crafting': 60, 'Scouting': 140,
     'Tactics': 220, 'Charm': 140, 'Leadership': 200, 'Trade': 80, 'Steward': 140, 'Medicine': 80, 'Engineering': 100},
    # Smith/Craftsman
    {'OneHanded': 120, 'TwoHanded': 140, 'Polearm': 100, 'Bow': 60, 'Crossbow': 60, 'Throwing': 60,
     'Riding': 60, 'Athletics': 140, 'Roguery': 40, 'Crafting': 220, 'Scouting': 60,
     'Tactics': 80, 'Charm': 80, 'Leadership': 80, 'Trade': 140, 'Steward': 120, 'Medicine': 60, 'Engineering': 180},
    # Cavalryman/Rider
    {'OneHanded': 180, 'TwoHanded': 120, 'Polearm': 200, 'Bow': 100, 'Crossbow': 60, 'Throwing': 80,
     'Riding': 220, 'Athletics': 140, 'Roguery': 40, 'Crafting': 60, 'Scouting': 120,
     'Tactics': 140, 'Charm': 80, 'Leadership': 140, 'Trade': 60, 'Steward': 80, 'Medicine': 60, 'Engineering': 40},
    # Archer/Marksman
    {'OneHanded': 120, 'TwoHanded': 60, 'Polearm': 80, 'Bow': 220, 'Crossbow': 180, 'Throwing': 140,
     'Riding': 100, 'Athletics': 160, 'Roguery': 60, 'Crafting': 80, 'Scouting': 160,
     'Tactics': 100, 'Charm': 60, 'Leadership': 80, 'Trade': 40, 'Steward': 60, 'Medicine': 60, 'Engineering': 40},
]

# Trait sets per archetype
TRAIT_SETS = [
    [('Calculating', 1), ('Mercy', -1)],               # Engineer
    [('Valor', 1), ('Mercy', -1)],                      # Warrior
    [('Calculating', 1), ('Honor', 1)],                 # Scout
    [('Mercy', 1), ('Generosity', 1)],                  # Healer
    [('Calculating', 1), ('Generosity', -1)],           # Trader
    [('Calculating', 1), ('Honor', -1)],                # Rogue
    [('Valor', 1), ('Calculating', 1)],                 # Tactician
    [('Honor', 1), ('Mercy', 1)],                       # Smith
    [('Valor', 1), ('Honor', 1)],                       # Cavalryman
    [('Calculating', 1), ('Valor', -1)],                # Archer
]

# Equipment items per kingdom for companion rosters
KINGDOM_EQUIPMENT = {
    'rivendell': {
        'weapons': [
            'Item.rivendell_sword_a01', 'Item.rivendell_sword_b01',
            'Item.rivendell_spear_a01', 'Item.rivendell_glaive_a01',
        ],
        'shields': ['Item.rivendell_shield_a01'],
        'bows': ['Item.highelf_longbowa', 'Item.highelf_longbowb'],
        'heads': ['Item.rivendell_helmet_gold', 'Item.rivendell_helmet_silver', 'Item.rivendell_helmet_elite', 'Item.rivendell_helmet_lord_circlet'],
        'bodies': ['Item.rivendell_body_gold_a', 'Item.rivendell_body_gold_b', 'Item.rivendell_body_silver_a', 'Item.rivendell_body_silver_b'],
        'capes': ['Item.rivendell_cape_a', 'Item.rivendell_cape_b', 'Item.rivendell_pauldron_elite_robe'],
        'gloves': ['Item.rivendell_gloves_gold', 'Item.rivendell_gloves_silver'],
        'legs': ['Item.rivendell_boots_greaves3', 'Item.rivendell_boots_greaves4', 'Item.rivendell_boots_leather1'],
        'civilian_bodies': ['Item.rivendell_body_silver_a'],
        'civilian_legs': ['Item.rivendell_boots_leather1', 'Item.rivendell_boots_leather2'],
    },
    'mirkwood': {
        'weapons': [
            'Item.mirkwood_sword_a01', 'Item.mirkwood_glaive_a01',
            'Item.mirkwood_spear_a01', 'Item.mirkwood_spear_a02',
        ],
        'shields': [],
        'bows': ['Item.highelf_longbowa'],
        'heads': ['Item.mkwd_inf1_t4_helmet_gold_hc', 'Item.mkwd_inf3_t2_helmet_gold_hc', 'Item.mkwd_inf3_t3_helmet_gold_hc'],
        'bodies': ['Item.mirkwood_body', 'Item.mkwd_inf3_chest', 'Item.mkwd_inf3_chest_bronze', 'Item.mkwd_inf3_chest_silver'],
        'capes': ['Item.mirkwood_shoulder', 'Item.mkwd_inf3_pauldrons', 'Item.mkwd_inf3_pauldrons_bronze'],
        'gloves': ['Item.mirkwood_gloves'],
        'legs': ['Item.mirkwood_boots', 'Item.mkwd_inf3_greaves', 'Item.mkwd_inf3_greaves_bronze'],
        'civilian_bodies': ['Item.mirkwood_body'],
        'civilian_legs': ['Item.mirkwood_boots'],
    },
    'lothlorien': {
        # Use rivendell items as Lothlorien shares elven aesthetic
        'weapons': [
            'Item.rivendell_sword_a01', 'Item.rivendell_sword_b01',
            'Item.rivendell_spear_a01',
        ],
        'shields': ['Item.rivendell_shield_a01'],
        'bows': ['Item.highelf_longbowa', 'Item.highelf_longbowb', 'Item.highelf_longbowc'],
        'heads': ['Item.rivendell_helmet_gold', 'Item.rivendell_helmet_silver', 'Item.rivendell_helmet_lord_circlet'],
        'bodies': ['Item.rivendell_body_gold_a', 'Item.rivendell_body_gold_b', 'Item.rivendell_body_silver_a'],
        'capes': ['Item.rivendell_cape_a', 'Item.rivendell_cape_b'],
        'gloves': ['Item.rivendell_gloves_gold', 'Item.rivendell_gloves_silver'],
        'legs': ['Item.rivendell_boots_greaves3', 'Item.rivendell_boots_leather1'],
        'civilian_bodies': ['Item.rivendell_body_silver_a'],
        'civilian_legs': ['Item.rivendell_boots_leather1', 'Item.rivendell_boots_leather2'],
    },
    'dolguldur': {
        'weapons': [
            'Item.sk_dg_uruk_1h_sword_a', 'Item.sk_dg_uruk_1h_axe_a',
            'Item.sk_dg_uruk_2h_sword_a', 'Item.sk_dg_uruk_polearm_a',
        ],
        'shields': ['Item.sk_dg_uruk_shield_a'],
        'bows': [],
        'heads': ['Item.sk_dg_uruk_head_elite_a', 'Item.sk_dg_uruk_head_elite_b', 'Item.sk_dg_uruk_head_heavy_a', 'Item.sk_dg_uruk_head_heavy_b'],
        'bodies': ['Item.sk_dg_uruk_chest_elite_a', 'Item.sk_dg_uruk_chest_elite_b', 'Item.sk_dg_uruk_chest_heavy_a', 'Item.sk_dg_uruk_chest_heavy_b'],
        'capes': [],
        'gloves': ['Item.sk_dg_uruk_bracer_elite_a', 'Item.sk_dg_uruk_bracer_heavy_a', 'Item.sk_dg_uruk_bracer_heavy_b'],
        'legs': ['Item.sk_dg_uruk_boots_elite_a', 'Item.sk_dg_uruk_boots_heavy_a', 'Item.sk_dg_uruk_boots_heavy_b'],
        'civilian_bodies': ['Item.sk_dg_uruk_chest_light_a'],
        'civilian_legs': ['Item.sk_dg_uruk_boots_light_a'],
    },
    'dunland': {
        'weapons': [
            'Item.dunland_caerdh_axe_1h_a', 'Item.dunland_caerdh_axe_1h_b',
            'Item.dunland_caerdh_axe_2h_a', 'Item.dunland_caerdh_spear_a',
        ],
        'shields': ['Item.dunland_caerdh_shield_a'],
        'bows': ['Item.hunting_bow'],
        'heads': ['Item.dunland_caerdh_helmet_elite_a', 'Item.dunland_caerdh_helmet_heavy_a', 'Item.dunland_caerdh_helmet_heavy_b'],
        'bodies': ['Item.dunland_caerdh_chainmail_elite_a', 'Item.dunland_caerdh_chainmail_heavy_a', 'Item.dunland_caerdh_chainmail_heavy_b', 'Item.dunland_caerdh_chainmail_medium_a'],
        'capes': [],
        'gloves': ['Item.dunland_caerdh_bracer_elite_a', 'Item.dunland_caerdh_bracer_heavy_a', 'Item.dunland_caerdh_bracer_medium_a'],
        'legs': ['Item.dunland_caerdh_boots_heavy_a', 'Item.dunland_caerdh_boots_medium_a'],
        'civilian_bodies': ['Item.battania_civil_a', 'Item.battania_woodland_outfit'],
        'civilian_legs': ['Item.battania_leather_boots'],
    },
    'harad': {
        'weapons': [
            'Item.aserai_sword_3_t3', 'Item.aserai_sword_5_t4',
            'Item.aserai_sword_6_t4', 'Item.aserai_lance_1_t5',
        ],
        'shields': ['Item.desert_round_shield'],
        'bows': ['Item.composite_bow'],
        'heads': ['Item.haradrim02_head', 'Item.haradrim02a_head'],
        'bodies': ['Item.haradrim02_toso', 'Item.haradrim02a_toso'],
        'capes': ['Item.haradrim02_pauldrons'],
        'gloves': ['Item.haradrim02_gloves', 'Item.haradrim02a_gloves', 'Item.haradrim02c_gloves'],
        'legs': ['Item.haradrim02_boots'],
        'civilian_bodies': ['Item.aserai_civil_c', 'Item.aserai_civil_d', 'Item.aserai_civil_e'],
        'civilian_legs': ['Item.haradrim02_boots'],
    },
    'rhun': {
        'weapons': [
            'Item.rhun_1h_sword_b', 'Item.aserai_sword_3_t3',
        ],
        'shields': ['Item.rhun_round_shield_c', 'Item.desert_round_shield'],
        'bows': ['Item.composite_bow'],
        'heads': ['Item.easterling_head', 'Item.easterlingwarriors04_helmet'],
        'bodies': ['Item.easterling_torso', 'Item.easterlingwarriors01_torso', 'Item.easterlingwarriors04_torso'],
        'capes': ['Item.easterlingwarriors04_cape'],
        'gloves': ['Item.easterling_glove', 'Item.easterlingwarriors01_gloves'],
        'legs': ['Item.easterling_boots', 'Item.easterlingwarriors01_boots'],
        'civilian_bodies': ['Item.khuzait_civil_coat_a', 'Item.khuzait_civil_coat_b', 'Item.khuzait_civil_coat_c'],
        'civilian_legs': ['Item.khuzait_civil_boots', 'Item.khuzait_leather_boots'],
    },
    'umbar': {
        'weapons': [
            'Item.aserai_sword_3_t3', 'Item.aserai_sword_5_t4',
            'Item.gond_spear2', 'Item.isengard_pike_a',
        ],
        'shields': ['Item.desert_round_shield'],
        'bows': ['Item.lowland_longbow', 'Item.composite_bow'],
        'heads': ['Item.dunland_caerdh_helmet_heavy_e', 'Item.gondor_nobke_helmet1', 'Item.ithilien_hood_masked_var'],
        'bodies': ['Item.ar_ardunian_elite_armour', 'Item.gondor_chainmaila', 'Item.ithilien_jerkin_long'],
        'capes': ['Item.citidel_guard_armor_pauldrons', 'Item.fountain_shoulders2'],
        'gloves': ['Item.gondor_nobke_bracers', 'Item.ithilien_bracers'],
        'legs': ['Item.ithilien_boots', 'Item.ithilien_boots_heavy', 'Item.gondor_nobke_boots'],
        'civilian_bodies': ['Item.layered_leather_tunic', 'Item.bandit_envelope_dress_v1'],
        'civilian_legs': ['Item.ithilien_boots', 'Item.dunland_caerdh_boots_light_a'],
    },
}

# Backstory data per kingdom
BACKSTORY_DATA = {
    'rivendell': {
        'region': 'Imladris',
        'places': ['the Last Homely House', 'the Valley of Rivendell', 'the Ford of Bruinen', 'the libraries of Elrond', 'the gardens of Imladris'],
        'leader': 'Lord Elrond',
        'people': 'the Eldar',
        'enemy': 'the forces of darkness',
        'titles': ['Loremaster', 'Blade-singer', 'Pathfinder', 'Healer', 'Trader', 'Shadow-walker', 'War-counselor', 'Forge-master', 'Rider', 'Bow-master'],
    },
    'mirkwood': {
        'region': 'the Woodland Realm',
        'places': ['the halls of Thranduil', 'the forest paths', 'the Enchanted River', 'the Spider Nests', 'the northern borders'],
        'leader': 'King Thranduil',
        'people': 'the Silvan Elves',
        'enemy': 'the spiders and orcs of Dol Guldur',
        'titles': ['Forest Sage', 'Blade of the Wood', 'Pathfinder', 'Herb-speaker', 'River Trader', 'Shadow-stalker', 'Captain of the Guard', 'Woodwright', 'Elk-rider', 'Sharp-eye'],
    },
    'lothlorien': {
        'region': 'the Golden Wood',
        'places': ['Caras Galadhon', 'the borders of Lórien', 'the Nimrodel stream', 'the mellyrn groves', 'Cerin Amroth'],
        'leader': 'Lord Celeborn and Lady Galadriel',
        'people': 'the Galadhrim',
        'enemy': 'the Shadow in the East',
        'titles': ['Keeper of Lore', 'Warden', 'Marchwarden', 'Silver Healer', 'Jewel-trader', 'Night-walker', 'Battle-sage', 'Mirror-smith', 'Swan-rider', 'Golden Archer'],
    },
    'dolguldur': {
        'region': 'Dol Guldur',
        'places': ['the Hill of Sorcery', 'the dark pits', 'southern Mirkwood', 'the ruined fortress', 'the slave pens'],
        'leader': 'the Necromancer',
        'people': 'the uruks of the fortress',
        'enemy': 'the elves and men of the West',
        'titles': ['Pit-boss', 'Skull-crusher', 'Shadow-scout', 'Poison-mixer', 'Loot-keeper', 'Throat-cutter', 'War-chief', 'Iron-bender', 'Warg-rider', 'Eye-poker'],
    },
    'dunland': {
        'region': 'the hills of Dunland',
        'places': ['the Dunland highlands', 'the Gap of Rohan', 'the hill-forts', 'the wild moors', 'the border marches'],
        'leader': 'the clan chiefs',
        'people': 'the Dunlendings',
        'enemy': 'the Horse-lords of Rohan',
        'titles': ['Hill Sage', 'Axe-brother', 'Moor-runner', 'Herb-woman', 'Pack-trader', 'Night-raider', 'War-leader', 'Iron-worker', 'Hill-rider', 'Keen-eye'],
    },
    'harad': {
        'region': 'the lands of Harad',
        'places': ['the desert cities', 'the oases', 'the southern ports', 'the tribal camps', 'the caravan routes'],
        'leader': 'the Serpent Lords',
        'people': 'the Haradrim',
        'enemy': 'the men of Gondor',
        'titles': ['Sand Scholar', 'Scimitar Master', 'Desert Tracker', 'Oasis Healer', 'Spice Merchant', 'Shadow Blade', 'War Captain', 'Bronze Smith', 'Mumak Handler', 'Hawk-eye'],
    },
    'rhun': {
        'region': 'the eastern steppes',
        'places': ['the great camps', 'the Sea of Rhûn', 'the wainwright towns', 'the eastern marches', 'the trade posts'],
        'leader': 'the Kagan',
        'people': 'the Easterlings',
        'enemy': 'the western kingdoms',
        'titles': ['Lore-keeper', 'Iron Champion', 'Steppe Scout', 'Wind Healer', 'Silk Trader', 'Horse Thief', 'War Drummer', 'Blade Smith', 'Thunder Rider', 'Storm Archer'],
    },
    'umbar': {
        'region': 'the Haven of Umbar',
        'places': ['the corsair docks', 'the sea fortress', 'the smuggler coves', 'the slave markets', 'the pirate fleet'],
        'leader': 'the Corsair Lords',
        'people': 'the Corsairs of Umbar',
        'enemy': 'the navy of Gondor',
        'titles': ['Navigator', 'Blade Captain', 'Sea Scout', 'Ship Surgeon', 'Prize Merchant', 'Cutthroat', 'Fleet Commander', 'Anchor Smith', 'Galley Master', 'Crossbow Ace'],
    },
}

# Backstory templates per archetype
BACKSTORY_TEMPLATES = [
    # 0: Engineer/Scholar
    {
        'prebackstory': "Knowledge is the truest weapon, and I have spent a lifetime gathering it...",
        'backstory_a': "I studied the ancient arts of {region}, learning the secrets of construction and siege-craft from {leader}'s own engineers. My family served as builders for generations, raising walls and forging defenses.",
        'backstory_b': "But when a rival accused me of weakening our fortifications for the enemy, I was cast out despite my innocence. The walls I built still stand, though my name has been struck from the builders' rolls.",
        'backstory_c': "Now I wander, offering my knowledge to those who value skill over politics. The Shadow grows, and {people} need fortifications more than ever.",
        'response_1': "A builder's skills are worth more than a warrior's in these dark times.",
        'response_2': "Stone walls won't stop what's coming.",
        'backstory_d': "If you need one who can build as well as fight, I am at your service.",
    },
    # 1: Warrior/Champion
    {
        'prebackstory': "My blade has tasted the blood of many foes, yet I fight on...",
        'backstory_a': "I was a champion of {region}, trained since youth in the arts of war. My skill with blade and shield earned me renown among {people}, and I served {leader} with unwavering loyalty.",
        'backstory_b': "But honor demanded I challenge a corrupt commander who sent soldiers to die for personal glory. I won the duel but lost my position - the commander had powerful allies who saw me banished.",
        'backstory_c': "Now I sell my sword to worthy causes. {enemy} grows bolder each day, and there is no shortage of battles to fight for those with the courage to face them.",
        'response_1': "A warrior who fights for honor deserves respect.",
        'response_2': "Perhaps you should have been more careful about choosing your battles.",
        'backstory_d': "My blade is yours, if you'll have it.",
    },
    # 2: Scout/Ranger
    {
        'prebackstory': "The wild places hold secrets that town-folk never dream of...",
        'backstory_a': "I served as a scout in {region}, ranging far beyond our borders to watch for threats. I know every path through {places[1]} and can track a fox through running water.",
        'backstory_b': "During a patrol, I discovered a plot among our own people - smugglers working with {enemy}. When I reported it, I learned the smugglers had bought protection from someone powerful. I was told to forget what I'd seen.",
        'backstory_c': "I refused to look away and paid the price. Now I range alone, using my skills to survive and help those who cross my path. The wilds are dangerous, but at least they're honest.",
        'response_1': "The truth is worth any price.",
        'response_2': "Sometimes survival requires compromise.",
        'backstory_d': "I offer my eyes, my bow, and my knowledge of the wild.",
    },
    # 3: Healer/Herbalist
    {
        'prebackstory': "I have seen too many die from wounds that could have been healed...",
        'backstory_a': "I trained as a healer in {region}, learning the properties of every herb and remedy known to {people}. I tended the wounded after every skirmish with {enemy}, saving countless lives.",
        'backstory_b': "But when a noble's son died despite my best efforts - the wound was beyond any healer's skill - his father blamed me and demanded my execution. I fled rather than face a rigged tribunal.",
        'backstory_c': "Now I practice my art wherever I can, helping the sick and wounded regardless of their allegiance. Death does not discriminate, and neither should healing.",
        'response_1': "A healer's compassion is a rare gift.",
        'response_2': "Healing enemies only makes more work for warriors.",
        'backstory_d': "If you value the lives of your people, let me tend to them.",
    },
    # 4: Trader/Merchant
    {
        'prebackstory': "Gold opens doors that swords cannot...",
        'backstory_a': "I built a trading network across {region}, connecting {people} with goods from distant lands. My caravans brought prosperity and rare supplies that {leader} valued highly.",
        'backstory_b': "But a jealous merchant house conspired to frame me for smuggling contraband. They seized my warehouses, scattered my contacts, and left me with nothing but the clothes on my back and the knowledge in my head.",
        'backstory_c': "Now I seek to rebuild, one deal at a time. The coming war will need supplies and logistics, and I know every trade route from {places[0]} to the far reaches.",
        'response_1': "Commerce is the lifeblood of any campaign.",
        'response_2': "Pretty words from someone who lost everything.",
        'backstory_d': "I can make your gold stretch further than you'd believe. Hire me.",
    },
    # 5: Rogue/Thief
    {
        'prebackstory': "In the shadows, one learns the true nature of power...",
        'backstory_a': "I grew up in the back alleys of {region}, learning to survive by wit and quick hands. Eventually, I was recruited by {leader}'s intelligence network, using my skills for {people}'s benefit.",
        'backstory_b': "But when I uncovered a conspiracy reaching into the highest circles of power, I became a liability. Those I served decided I knew too much and sent assassins. I killed two of them and fled.",
        'backstory_c': "Now I trust no one but myself, though I'd rather use my skills openly than skulk in shadows forever. {enemy} has spies everywhere, and someone needs to counter them.",
        'response_1': "A shadow-worker who serves the light is valuable indeed.",
        'response_2': "How can anyone trust a self-admitted spy?",
        'backstory_d': "I work best when no one sees me coming. Will you have me?",
    },
    # 6: Tactician/Commander
    {
        'prebackstory': "Battles are won in the mind before a single blade is drawn...",
        'backstory_a': "I commanded forces in {region}, studying the art of war under {leader}'s most experienced captains. My tactical innovations won several engagements against {enemy} when we were outnumbered.",
        'backstory_b': "But a jealous superior took credit for my victories and blamed me for a retreat that saved our army from annihilation. The council sided with him - he had connections I lacked.",
        'backstory_c': "Stripped of rank, I now offer my mind and experience to those who judge by merit, not birth. The coming war will need commanders who can think, not just those who can swing a sword.",
        'response_1': "A thinking warrior is the most dangerous kind.",
        'response_2': "Plans rarely survive contact with the enemy.",
        'backstory_d': "Let me plan your campaigns, and I'll show you what strategy can achieve.",
    },
    # 7: Smith/Craftsman
    {
        'prebackstory': "The ring of hammer on steel is the music I was born to play...",
        'backstory_a': "I was a master smith in {region}, crafting weapons and armor that {people} prized above all others. My forge produced blades that could cleave through enemy armor like parchment.",
        'backstory_b': "But I refused to craft weapons for a corrupt lord who planned to use them against innocent people. He burned my forge and had me beaten. I survived, but my workshop was destroyed.",
        'backstory_c': "Now I carry my skills wherever I go, maintaining and improving the arms of those I travel with. A good blade in trained hands can change the course of a battle.",
        'response_1': "A craftsman of principle is rare and precious.",
        'response_2': "Principles don't keep you warm in winter.",
        'backstory_d': "Keep me fed and give me a forge, and I'll keep your steel sharp.",
    },
    # 8: Cavalryman/Rider
    {
        'prebackstory': "There is no freedom like the wind in your face atop a swift horse...",
        'backstory_a': "I served in the mounted forces of {region}, patrolling the borders and striking fast against {enemy}'s raiding parties. My horsemanship was unmatched among {people}.",
        'backstory_b': "During a raid, my commander ordered us to trample fleeing civilians - they might be enemy sympathizers, he said. I refused and was court-martialed for disobedience.",
        'backstory_c': "Now I ride alone, my lance for hire to those who fight with honor. The open road suits me better than garrison duty ever did.",
        'response_1': "A rider who values life over orders has true courage.",
        'response_2': "Disobedience in battle can cost more lives than it saves.",
        'backstory_d': "Give me a mount and a cause worth riding for.",
    },
    # 9: Archer/Marksman
    {
        'prebackstory': "An arrow loosed at the right moment can change history...",
        'backstory_a': "I was the finest marksman in {region}, able to split an arrow at two hundred paces. I served as {leader}'s personal guard archer, watching over {people} from the highest vantage points.",
        'backstory_b': "But I witnessed my captain murder a prisoner in cold blood and reported it. The captain was connected to powerful families, and suddenly I was the one accused of crimes I didn't commit.",
        'backstory_c': "I escaped before they could silence me permanently. Now I offer my bow to those who still believe in justice. Every arrow I loose carries the memory of that murdered prisoner.",
        'response_1': "Justice is worth fighting for, even at great cost.",
        'response_2': "The dead don't care about justice.",
        'backstory_d': "My bow is steady and my aim is true. Will you have me?",
    },
]


def generate_npc_xml(kingdom, idx, config):
    """Generate a single wanderer NPCCharacter XML block."""
    name = WANDERER_NAMES[kingdom][idx]
    voice = config['voices'][idx]
    age = config['ages'][idx]
    culture = config['culture_id']
    body = config['body_property']
    race = config['race']

    race_attr = f'\n\t\trace="{race}"' if race else ''
    traits_xml = '\n'.join([f'\t\t\t<Trait id="{t[0]}" value="{t[1]}" />' for t in TRAIT_SETS[idx]])

    return f'''\t<NPCCharacter
\t\tid="spc_wanderer_{kingdom}_{idx}"
\t\tname="{{=aom_spc_wanderer_{kingdom}_{idx}_name}}{{FIRSTNAME}} {name}"
\t\tvoice="{voice}"
\t\tage="{age}"
\t\tis_template="true"
\t\tdefault_group="Infantry"
\t\tis_hero="false"
\t\tculture="Culture.{culture}"
\t\toccupation="Wanderer"{race_attr}
\t\tskill_template="SkillSet.spc_wanderer_{kingdom}_{idx}_skills">
\t\t<face>
\t\t\t<face_key_template value="{body}" />
\t\t</face>
\t\t<Traits>
{traits_xml}
\t\t</Traits>
\t\t<Equipments>
\t\t\t<EquipmentSet id="npc_companion_equipment_template_{kingdom}" equipmentType="Civilian" />
\t\t\t<EquipmentSet id="npc_companion_equipment_template_{kingdom}" />
\t\t</Equipments>
\t</NPCCharacter>'''


def generate_skill_set_xml(kingdom, idx):
    """Generate a single SkillSet XML block."""
    skills = SKILL_ARCHETYPES[idx]
    # Add some random variation (+/- 20)
    rng = random.Random(hash(f'{kingdom}_{idx}'))
    skills_xml = '\n'.join([
        f'\t\t<skill id="{s}" value="{max(0, v + rng.randint(-20, 20))}" />'
        for s, v in skills.items()
    ])
    return f'''\t<SkillSet id="spc_wanderer_{kingdom}_{idx}_skills">
{skills_xml}
\t</SkillSet>'''


def generate_equipment_roster(kingdom, items):
    """Generate companion equipment roster for a kingdom."""
    def random_set(rng, battle=True):
        slots = []
        # Weapon
        if items['weapons']:
            slots.append(f'\t\t\t<Equipment slot="Item0" id="{rng.choice(items["weapons"])}" />')
        # Shield or bow
        if battle and items.get('shields') and rng.random() > 0.5:
            slots.append(f'\t\t\t<Equipment slot="Item1" id="{rng.choice(items["shields"])}" />')
        elif battle and items.get('bows') and rng.random() > 0.5:
            slots.append(f'\t\t\t<Equipment slot="Item2" id="{rng.choice(items["bows"])}" />')
        # Head
        if battle and items['heads']:
            slots.append(f'\t\t\t<Equipment slot="Head" id="{rng.choice(items["heads"])}" />')
        # Body
        body_list = items['civilian_bodies'] if not battle else items['bodies']
        if body_list:
            slots.append(f'\t\t\t<Equipment slot="Body" id="{rng.choice(body_list)}" />')
        # Cape
        if battle and items.get('capes'):
            slots.append(f'\t\t\t<Equipment slot="Cape" id="{rng.choice(items["capes"])}" />')
        # Gloves
        if battle and items.get('gloves'):
            slots.append(f'\t\t\t<Equipment slot="Gloves" id="{rng.choice(items["gloves"])}" />')
        # Legs
        leg_list = items['civilian_legs'] if not battle else items['legs']
        if leg_list:
            slots.append(f'\t\t\t<Equipment slot="Leg" id="{rng.choice(leg_list)}" />')
        return '\n'.join(slots)

    culture = KINGDOMS[kingdom]['culture_id']
    rng = random.Random(hash(kingdom))

    sets = []
    # 12 battle sets
    for i in range(12):
        sets.append(f'\t\t<EquipmentSet>\n{random_set(rng, battle=True)}\n\t\t</EquipmentSet>')
    # 3 civilian sets
    for i in range(3):
        sets.append(f'\t\t<EquipmentSet equipmentType="Civilian">\n{random_set(rng, battle=False)}\n\t\t</EquipmentSet>')

    sets_xml = '\n'.join(sets)
    return f'''\t<EquipmentRoster id="npc_companion_equipment_template_{kingdom}" culture="Culture.{culture}">
{sets_xml}
\t</EquipmentRoster>'''


def generate_backstory_strings(kingdom, idx):
    """Generate the 8 backstory strings for a single wanderer."""
    data = BACKSTORY_DATA[kingdom]
    template = BACKSTORY_TEMPLATES[idx]

    def fill(text):
        text = text.replace('{region}', data['region'])
        text = text.replace('{leader}', data['leader'])
        text = text.replace('{people}', data['people'])
        text = text.replace('{enemy}', data['enemy'])
        for i, place in enumerate(data['places']):
            text = text.replace(f'{{places[{i}]}}', place)
        return text

    wid = f'spc_wanderer_{kingdom}_{idx}'
    strings = []
    for key in ['prebackstory', 'backstory_a', 'backstory_b', 'backstory_c', 'response_1', 'response_2', 'backstory_d']:
        text = fill(template[key])
        loc_key = f'aom_{key}.{wid}_text'
        strings.append(f'\t<string id="{key}.{wid}" text="{{={loc_key}}}{text}" />')

    # Generic backstory (short summary)
    title = data['titles'][idx]
    generic = f"A {title.lower()} from {data['region']}, seeking purpose and adventure."
    loc_key = f'aom_generic_backstory.{wid}_text'
    strings.append(f'\t<string id="generic_backstory.{wid}" text="{{={loc_key}}}{generic}" />')

    return '\n'.join(strings)


def main():
    # Read existing files to append
    wanderers_file = os.path.join(BASE_DIR, 'taom_wanderers.xml')
    skills_file = os.path.join(BASE_DIR, 'taom_wanderer_skill_sets.xml')
    equip_file = os.path.join(BASE_DIR, 'taom_wanderer_equipment.xml')
    strings_file = os.path.join(BASE_DIR, 'taom_wanderer_strings.xml')

    # Read existing content
    with open(wanderers_file, 'r', encoding='utf-8') as f:
        wanderers_content = f.read()
    with open(skills_file, 'r', encoding='utf-8') as f:
        skills_content = f.read()
    with open(equip_file, 'r', encoding='utf-8') as f:
        equip_content = f.read()
    with open(strings_file, 'r', encoding='utf-8') as f:
        strings_content = f.read()

    # Generate new content for each kingdom
    new_wanderers = []
    new_skills = []
    new_equipment = []
    new_strings = []

    for kingdom, config in KINGDOMS.items():
        # NPC characters
        new_wanderers.append(f'\n\t<!-- ==================== {kingdom.upper()} WANDERERS ==================== -->')
        for idx in range(10):
            new_wanderers.append(generate_npc_xml(kingdom, idx, config))

        # Skill sets
        new_skills.append(f'\n\t<!-- {kingdom.upper()} wanderer skills -->')
        for idx in range(10):
            new_skills.append(generate_skill_set_xml(kingdom, idx))

        # Equipment roster
        new_equipment.append(f'\n\t<!-- {kingdom.upper()} companion equipment -->')
        new_equipment.append(generate_equipment_roster(kingdom, KINGDOM_EQUIPMENT[kingdom]))

        # Backstory strings
        new_strings.append(f'\n\t<!-- {kingdom.upper()} wanderer backstories -->')
        for idx in range(10):
            new_strings.append(generate_backstory_strings(kingdom, idx))

    # Append to existing files (before closing tags)
    wanderers_content = wanderers_content.replace('</NPCCharacters>', '\n'.join(new_wanderers) + '\n\n</NPCCharacters>')
    skills_content = skills_content.replace('</SkillSets>', '\n'.join(new_skills) + '\n\n</SkillSets>')
    equip_content = equip_content.replace('</EquipmentRosters>', '\n'.join(new_equipment) + '\n\n</EquipmentRosters>')
    strings_content = strings_content.replace('</strings>', '\n'.join(new_strings) + '\n\n</strings>')

    # Write files
    with open(wanderers_file, 'w', encoding='utf-8') as f:
        f.write(wanderers_content)
    with open(skills_file, 'w', encoding='utf-8') as f:
        f.write(skills_content)
    with open(equip_file, 'w', encoding='utf-8') as f:
        f.write(equip_content)
    with open(strings_file, 'w', encoding='utf-8') as f:
        f.write(strings_content)

    print("Batch 2 wanderers generated successfully!")
    print(f"  Kingdoms: {', '.join(KINGDOMS.keys())}")
    print(f"  Wanderers per kingdom: 10")
    print(f"  Total new wanderers: {len(KINGDOMS) * 10}")


if __name__ == '__main__':
    main()
