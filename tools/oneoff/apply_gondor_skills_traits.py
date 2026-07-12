#!/usr/bin/env python3
"""Apply lore-driven skills + traits to every Gondor lord (lords.xml + lords.xslt).

Approach:
- 10 archetypes (lord/knight/ranger/lady/matriarch/elder_lord/young_lord/young_lady/steward/errand_rider).
- ~25 canonical Tolkien characters get explicit per-NPC overrides on top of an archetype.
- Supplementary characters auto-classified by bio keywords (heroes.xslt) + gender + age.
- For each NPC, the entire <skills>...</skills> and <Traits>...</Traits> blocks are rewritten.

Skill ceilings: most adults 0-300. Special characters can push 280-295. Children skipped.
Trait range: -2 to 2.

Run --dry-run first to preview. --apply to write.
"""
import argparse
import re
from pathlib import Path

LORDS_XML = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"
LORDS_XSLT = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "lords.xslt"
HEROES_XSLT = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "heroes.xslt"

SKILL_ORDER = ['OneHanded','TwoHanded','Polearm','Bow','Crossbow','Throwing','Riding','Athletics',
               'Crafting','Scouting','Tactics','Roguery','Charm','Leadership','Trade','Steward',
               'Medicine','Engineering']
TRAIT_ORDER = ['Honor','Generosity','Calculating','Mercy','Valor','Egalitarian','Oligarchic','Authoritarian']


# ============================================================================
# ARCHETYPE TEMPLATES — values are typical Gondor adult ranges; adjusted per NPC.
# ============================================================================
ARCHETYPES = {
    'lord': {  # Region-ruling adult male, well-rounded military leader
        'skills': dict(OneHanded=220, TwoHanded=160, Polearm=210, Bow=120, Crossbow=90, Throwing=120,
                       Riding=220, Athletics=220, Crafting=80, Scouting=180, Tactics=220, Roguery=70,
                       Charm=190, Leadership=240, Trade=140, Steward=210, Medicine=110, Engineering=150),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=1),
    },
    'knight': {  # Young/middle adult male warrior, cavalry-focused
        'skills': dict(OneHanded=230, TwoHanded=180, Polearm=220, Bow=100, Crossbow=70, Throwing=120,
                       Riding=250, Athletics=240, Crafting=60, Scouting=140, Tactics=180, Roguery=60,
                       Charm=160, Leadership=160, Trade=90, Steward=130, Medicine=90, Engineering=100),
        'traits': dict(Honor=2, Generosity=1, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=0),
    },
    'ranger': {  # Faramir-archetype: archer, scout, woodsman
        'skills': dict(OneHanded=200, TwoHanded=140, Polearm=180, Bow=270, Crossbow=140, Throwing=170,
                       Riding=170, Athletics=260, Crafting=100, Scouting=270, Tactics=210, Roguery=130,
                       Charm=170, Leadership=190, Trade=100, Steward=150, Medicine=140, Engineering=100),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=2, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lady': {  # Adult female noble/wife — court manager, low combat
        'skills': dict(OneHanded=60, TwoHanded=30, Polearm=50, Bow=70, Crossbow=50, Throwing=50,
                       Riding=140, Athletics=110, Crafting=140, Scouting=110, Tactics=130, Roguery=60,
                       Charm=240, Leadership=160, Trade=180, Steward=240, Medicine=210, Engineering=130),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=2, Valor=0,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'matriarch': {  # Elder female (60+), wisdom + management peak
        'skills': dict(OneHanded=50, TwoHanded=25, Polearm=40, Bow=60, Crossbow=40, Throwing=40,
                       Riding=130, Athletics=90, Crafting=170, Scouting=130, Tactics=190, Roguery=70,
                       Charm=285, Leadership=220, Trade=240, Steward=285, Medicine=245, Engineering=160),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=2, Valor=0,
                       Egalitarian=1, Oligarchic=1, Authoritarian=1),
    },
    'elder_lord': {  # Older male (60+), retired warrior, wise counsel
        'skills': dict(OneHanded=200, TwoHanded=150, Polearm=190, Bow=90, Crossbow=70, Throwing=90,
                       Riding=180, Athletics=160, Crafting=110, Scouting=180, Tactics=270, Roguery=80,
                       Charm=220, Leadership=260, Trade=180, Steward=240, Medicine=160, Engineering=190),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=2, Authoritarian=1),
    },
    'young_lord': {  # 14-25 male heir, trained but green
        'skills': dict(OneHanded=160, TwoHanded=120, Polearm=140, Bow=90, Crossbow=70, Throwing=100,
                       Riding=190, Athletics=180, Crafting=60, Scouting=140, Tactics=130, Roguery=60,
                       Charm=140, Leadership=120, Trade=80, Steward=110, Medicine=80, Engineering=80),
        'traits': dict(Honor=2, Generosity=1, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=0),
    },
    'young_lady': {  # 14-25 female, courtly training
        'skills': dict(OneHanded=40, TwoHanded=20, Polearm=30, Bow=70, Crossbow=40, Throwing=40,
                       Riding=120, Athletics=100, Crafting=110, Scouting=90, Tactics=100, Roguery=50,
                       Charm=180, Leadership=120, Trade=130, Steward=170, Medicine=150, Engineering=100),
        'traits': dict(Honor=1, Generosity=1, Calculating=1, Mercy=2, Valor=0,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'steward': {  # Diplomat/administrator type (Húrioneth-archetype)
        'skills': dict(OneHanded=110, TwoHanded=70, Polearm=100, Bow=70, Crossbow=60, Throwing=70,
                       Riding=170, Athletics=140, Crafting=140, Scouting=140, Tactics=210, Roguery=90,
                       Charm=260, Leadership=220, Trade=240, Steward=275, Medicine=170, Engineering=200),
        'traits': dict(Honor=1, Generosity=1, Calculating=2, Mercy=0, Valor=0,
                       Egalitarian=0, Oligarchic=2, Authoritarian=1),
    },
    'errand_rider': {  # Hirgon-archetype: messenger, scout, peerless rider
        'skills': dict(OneHanded=160, TwoHanded=110, Polearm=140, Bow=140, Crossbow=90, Throwing=110,
                       Riding=280, Athletics=240, Crafting=60, Scouting=270, Tactics=160, Roguery=110,
                       Charm=150, Leadership=140, Trade=130, Steward=120, Medicine=90, Engineering=80),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=0),
    },
}


# ============================================================================
# CANONICAL TOLKIEN OVERRIDES — full skill+trait replacement per character.
# ============================================================================
CANONICAL = {
    # ---- House of the Stewards (clan_empire_west_1) ----
    'lord_1_7': {  # Denethor II — master statesman, scholar, Palantir-user, paranoid
        'skills': dict(OneHanded=170, TwoHanded=130, Polearm=180, Bow=130, Crossbow=90, Throwing=100,
                       Riding=200, Athletics=170, Crafting=150, Scouting=220, Tactics=280, Roguery=130,
                       Charm=270, Leadership=290, Trade=230, Steward=300, Medicine=160, Engineering=240),
        'traits': dict(Honor=1, Generosity=0, Calculating=2, Mercy=-1, Valor=1,
                       Egalitarian=-1, Oligarchic=2, Authoritarian=2),
    },
    'lord_1_75': {  # Boromir — Captain-General, master swordsman, "great warrior of renown"
        'skills': dict(OneHanded=295, TwoHanded=255, Polearm=240, Bow=160, Crossbow=110, Throwing=170,
                       Riding=260, Athletics=285, Crafting=80, Scouting=210, Tactics=250, Roguery=70,
                       Charm=230, Leadership=285, Trade=110, Steward=180, Medicine=130, Engineering=160),
        'traits': dict(Honor=2, Generosity=2, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=1),
    },
    'lord_1_34': {  # Faramir — Captain of the Rangers of Ithilien, scholar, merciful
        'skills': dict(OneHanded=240, TwoHanded=190, Polearm=220, Bow=275, Crossbow=140, Throwing=180,
                       Riding=210, Athletics=275, Crafting=110, Scouting=290, Tactics=270, Roguery=120,
                       Charm=230, Leadership=250, Trade=120, Steward=220, Medicine=170, Engineering=160),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=2, Valor=2,
                       Egalitarian=2, Oligarchic=0, Authoritarian=-1),
    },
    'lord_1_8': {  # Húrioneth — "steadfast keeper of the lore of Gondor's stewards"
        'skills': dict(OneHanded=130, TwoHanded=90, Polearm=110, Bow=80, Crossbow=70, Throwing=70,
                       Riding=170, Athletics=140, Crafting=160, Scouting=150, Tactics=230, Roguery=80,
                       Charm=250, Leadership=210, Trade=230, Steward=275, Medicine=180, Engineering=210),
        'traits': dict(Honor=2, Generosity=1, Calculating=2, Mercy=1, Valor=1,
                       Egalitarian=0, Oligarchic=2, Authoritarian=1),
    },
    'lord_1_44': {  # Nemos — Tower Guard captain, sworn to protect the Steward
        'skills': dict(OneHanded=260, TwoHanded=210, Polearm=250, Bow=110, Crossbow=90, Throwing=120,
                       Riding=180, Athletics=250, Crafting=80, Scouting=160, Tactics=220, Roguery=70,
                       Charm=170, Leadership=210, Trade=100, Steward=170, Medicine=110, Engineering=130),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=2),
    },

    # ---- House of Imrazôrionath / Dol Amroth (clan_empire_west_2) ----
    'lord_1_9': {  # Imrahil II — Prince of Dol Amroth, "greatest knight of Gondor", elven blood
        'skills': dict(OneHanded=290, TwoHanded=220, Polearm=275, Bow=180, Crossbow=120, Throwing=160,
                       Riding=290, Athletics=260, Crafting=110, Scouting=230, Tactics=275, Roguery=90,
                       Charm=270, Leadership=290, Trade=180, Steward=250, Medicine=170, Engineering=190),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=2, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=1),
    },
    'lord_1_9_5': {  # Lothwen — Princess of Dol Amroth, decades of grace
        'skills': dict(OneHanded=60, TwoHanded=30, Polearm=50, Bow=70, Crossbow=50, Throwing=50,
                       Riding=160, Athletics=110, Crafting=180, Scouting=140, Tactics=210, Roguery=80,
                       Charm=295, Leadership=240, Trade=250, Steward=295, Medicine=260, Engineering=170),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=2, Valor=0,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_25': {  # Elphir — eldest son, heir, commands Swan Knights
        'skills': dict(OneHanded=265, TwoHanded=190, Polearm=250, Bow=140, Crossbow=100, Throwing=140,
                       Riding=285, Athletics=255, Crafting=80, Scouting=180, Tactics=230, Roguery=70,
                       Charm=210, Leadership=235, Trade=140, Steward=200, Medicine=130, Engineering=140),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=1),
    },
    'lord_1_35': {  # Erchirion — second son, bold knight
        'skills': dict(OneHanded=250, TwoHanded=200, Polearm=235, Bow=140, Crossbow=100, Throwing=150,
                       Riding=270, Athletics=245, Crafting=70, Scouting=170, Tactics=200, Roguery=80,
                       Charm=180, Leadership=190, Trade=110, Steward=150, Medicine=110, Engineering=110),
        'traits': dict(Honor=2, Generosity=1, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_24': {  # Amrothos — youngest son, "still young but martial spirit"
        'skills': dict(OneHanded=210, TwoHanded=170, Polearm=195, Bow=130, Crossbow=80, Throwing=120,
                       Riding=240, Athletics=230, Crafting=70, Scouting=150, Tactics=170, Roguery=70,
                       Charm=180, Leadership=160, Trade=100, Steward=140, Medicine=100, Engineering=100),
        'traits': dict(Honor=2, Generosity=2, Calculating=0, Mercy=2, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_10': {  # Aranel — wife of Elphir, "dignity and keen mind"
        'skills': dict(OneHanded=60, TwoHanded=30, Polearm=50, Bow=80, Crossbow=60, Throwing=50,
                       Riding=150, Athletics=110, Crafting=150, Scouting=120, Tactics=160, Roguery=70,
                       Charm=255, Leadership=180, Trade=200, Steward=255, Medicine=215, Engineering=150),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=2, Valor=0,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_23': {  # Caladwen — wife of Erchirion, "quiet courage"
        'skills': dict(OneHanded=70, TwoHanded=40, Polearm=60, Bow=90, Crossbow=70, Throwing=60,
                       Riding=160, Athletics=130, Crafting=130, Scouting=130, Tactics=140, Roguery=60,
                       Charm=230, Leadership=170, Trade=180, Steward=235, Medicine=210, Engineering=130),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=2, Valor=1,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },

    # ---- House of Eärnurionath / Northern marches (clan_empire_west_3) ----
    'lord_1_11': {  # Ciryandur — northern defender, "seasoned veteran of many orc raids"
        'skills': dict(OneHanded=235, TwoHanded=185, Polearm=225, Bow=160, Crossbow=120, Throwing=140,
                       Riding=215, Athletics=225, Crafting=110, Scouting=250, Tactics=255, Roguery=110,
                       Charm=190, Leadership=240, Trade=140, Steward=215, Medicine=150, Engineering=180),
        'traits': dict(Honor=2, Generosity=1, Calculating=2, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=1),
    },
    'lord_1_111': {  # Elarwen — wife of Hirgon, "resilience and practical wisdom"
        'skills': dict(OneHanded=80, TwoHanded=50, Polearm=70, Bow=110, Crossbow=80, Throwing=70,
                       Riding=170, Athletics=150, Crafting=170, Scouting=160, Tactics=160, Roguery=80,
                       Charm=220, Leadership=180, Trade=200, Steward=240, Medicine=220, Engineering=160),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=1, Valor=1,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_12': {  # Findariel — Ciryandur's wife
        'skills': dict(OneHanded=70, TwoHanded=40, Polearm=60, Bow=110, Crossbow=80, Throwing=60,
                       Riding=170, Athletics=140, Crafting=160, Scouting=170, Tactics=170, Roguery=70,
                       Charm=215, Leadership=170, Trade=180, Steward=235, Medicine=210, Engineering=150),
        'traits': dict(Honor=2, Generosity=1, Calculating=2, Mercy=1, Valor=1,
                       Egalitarian=0, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_26': {  # Hirgon — errand-rider of Gondor, "swift and reliable"
        'skills': dict(OneHanded=170, TwoHanded=110, Polearm=150, Bow=160, Crossbow=100, Throwing=120,
                       Riding=285, Athletics=255, Crafting=70, Scouting=275, Tactics=170, Roguery=120,
                       Charm=160, Leadership=150, Trade=140, Steward=130, Medicine=100, Engineering=90),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },

    # ---- House of Barahirionath / Old nobility (clan_empire_west_4) ----
    'lord_1_40': {  # Borhador — "one of the eldest lords of Gondor", wisdom but weakening
        'skills': dict(OneHanded=180, TwoHanded=130, Polearm=170, Bow=110, Crossbow=90, Throwing=100,
                       Riding=160, Athletics=140, Crafting=120, Scouting=170, Tactics=265, Roguery=80,
                       Charm=235, Leadership=255, Trade=200, Steward=255, Medicine=170, Engineering=200),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=1, Valor=1,
                       Egalitarian=0, Oligarchic=2, Authoritarian=1),
    },
    'lord_1_40_1': {  # Lindariel — "strength of their house through long years"
        'skills': dict(OneHanded=60, TwoHanded=30, Polearm=50, Bow=80, Crossbow=60, Throwing=50,
                       Riding=160, Athletics=120, Crafting=170, Scouting=140, Tactics=180, Roguery=70,
                       Charm=255, Leadership=200, Trade=220, Steward=265, Medicine=230, Engineering=160),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=2, Valor=1,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_46': {  # Malrior — Barahirionath house member
        'archetype': 'lord',
    },
    'lord_1_46_1': {  # Thorwen — wife of Malrior, "capable woman manages with quiet determination"
        'skills': dict(OneHanded=60, TwoHanded=30, Polearm=50, Bow=80, Crossbow=60, Throwing=50,
                       Riding=140, Athletics=110, Crafting=140, Scouting=110, Tactics=140, Roguery=60,
                       Charm=215, Leadership=160, Trade=185, Steward=235, Medicine=200, Engineering=140),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=1, Valor=0,
                       Egalitarian=0, Oligarchic=1, Authoritarian=1),
    },

    # ---- Lossarnach / Forlong (clan_empire_west_5) ----
    'lord_1_45': {  # Forlong "the Fat" / "the Old" — fierce warrior despite girth, killed at Pelennor
        'skills': dict(OneHanded=235, TwoHanded=210, Polearm=255, Bow=110, Crossbow=90, Throwing=130,
                       Riding=160, Athletics=150, Crafting=110, Scouting=170, Tactics=235, Roguery=70,
                       Charm=210, Leadership=250, Trade=210, Steward=250, Medicine=140, Engineering=190),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_45_1': {  # Berethiel — Lady of Lossarnach, oversees fertile valleys
        'skills': dict(OneHanded=60, TwoHanded=30, Polearm=50, Bow=80, Crossbow=60, Throwing=50,
                       Riding=160, Athletics=120, Crafting=200, Scouting=140, Tactics=180, Roguery=70,
                       Charm=245, Leadership=190, Trade=230, Steward=275, Medicine=225, Engineering=170),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=2, Valor=1,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_45_2': {  # Caldamir (lords.xslt name) / Brandir (lords.xml) — "young son of Forlong, just come of age"
        'skills': dict(OneHanded=180, TwoHanded=150, Polearm=170, Bow=110, Crossbow=80, Throwing=110,
                       Riding=200, Athletics=200, Crafting=60, Scouting=130, Tactics=130, Roguery=60,
                       Charm=140, Leadership=120, Trade=90, Steward=120, Medicine=80, Engineering=90),
        'traits': dict(Honor=2, Generosity=1, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_45_3': {  # Rúmil (lords.xslt) / Borlong (lords.xml) — "elder brother of Forlong"
        'archetype': 'elder_lord',
    },
    'lord_1_57': {  # Baranor — "captain in Lossarnach contingent, trusted officer of Forlong"
        'archetype': 'knight',
        'skills': dict(OneHanded=240, TwoHanded=180, Polearm=230, Bow=130, Crossbow=100, Throwing=120,
                       Riding=210, Athletics=235, Crafting=70, Scouting=180, Tactics=200, Roguery=80,
                       Charm=180, Leadership=210, Trade=110, Steward=170, Medicine=100, Engineering=120),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=1),
    },

    # ---- Pinnath Gelin / Hirluin (clan_empire_west_6) ----
    'lord_1_52': {  # Hirluin "the Fair" — young valorous lord, green-clad warriors
        'skills': dict(OneHanded=235, TwoHanded=180, Polearm=245, Bow=230, Crossbow=120, Throwing=160,
                       Riding=215, Athletics=255, Crafting=80, Scouting=230, Tactics=215, Roguery=80,
                       Charm=250, Leadership=235, Trade=140, Steward=195, Medicine=140, Engineering=140),
        'traits': dict(Honor=2, Generosity=2, Calculating=0, Mercy=2, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lord_1_52_1': {  # Anariel (lords.xslt) / Arador (lords.xml) — "daughter of Hirluin... can hold her own"
        'archetype': 'young_lady',
        'skills': dict(OneHanded=140, TwoHanded=80, Polearm=120, Bow=170, Crossbow=110, Throwing=110,
                       Riding=180, Athletics=190, Crafting=110, Scouting=160, Tactics=140, Roguery=70,
                       Charm=200, Leadership=130, Trade=130, Steward=170, Medicine=140, Engineering=100),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lord_1_52_2': {  # Barandor (lords.xslt) / Arvedui (lords.xml) — "trains to lead green-clad company"
        'archetype': 'young_lord',
        'skills': dict(OneHanded=180, TwoHanded=140, Polearm=180, Bow=200, Crossbow=110, Throwing=140,
                       Riding=200, Athletics=210, Crafting=70, Scouting=200, Tactics=160, Roguery=70,
                       Charm=170, Leadership=160, Trade=100, Steward=130, Medicine=100, Engineering=100),
        'traits': dict(Honor=2, Generosity=1, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lord_1_52_4': {  # Nauriel — "wife of Hirluin, Lady of Pinnath Gelin, steadfast"
        'archetype': 'matriarch',
    },
    'lord_1_62': {  # Oromar — "husband of Anariel, serves Lord of Pinnath Gelin"
        'archetype': 'knight',
    },

    # ---- Lamedon / Angbor (clan_empire_west_7) ----
    'lord_1_53': {  # Angbor "the Fearless" — Lord of Lamedon, rallied vs the Dead
        'skills': dict(OneHanded=265, TwoHanded=215, Polearm=255, Bow=130, Crossbow=110, Throwing=150,
                       Riding=230, Athletics=250, Crafting=100, Scouting=210, Tactics=245, Roguery=80,
                       Charm=200, Leadership=260, Trade=150, Steward=210, Medicine=140, Engineering=160),
        'traits': dict(Honor=2, Generosity=2, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=1),
    },
    'lord_1_73': {  # Narmir — "younger brother of Angbor... courage if not experience"
        'archetype': 'knight',
        'skills': dict(OneHanded=235, TwoHanded=190, Polearm=225, Bow=120, Crossbow=90, Throwing=130,
                       Riding=220, Athletics=235, Crafting=70, Scouting=170, Tactics=180, Roguery=70,
                       Charm=180, Leadership=180, Trade=110, Steward=150, Medicine=100, Engineering=110),
        'traits': dict(Honor=2, Generosity=2, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },
    'lord_1_73_1': {  # Belwen (lords.xslt) / Popilia (lords.xml) — "stands steadfastly by husband in defense"
        'archetype': 'lady',
    },

    # ---- Anfalas / Golasgil (clan_empire_west_8 in spclans) ----
    'lord_1_71': {  # Golasgil — "weathered sea-lord, defended Gondor shores for decades"
        'skills': dict(OneHanded=215, TwoHanded=165, Polearm=210, Bow=225, Crossbow=140, Throwing=170,
                       Riding=180, Athletics=245, Crafting=110, Scouting=255, Tactics=230, Roguery=110,
                       Charm=195, Leadership=225, Trade=230, Steward=210, Medicine=140, Engineering=170),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lord_1_71_1': {  # Laswen — wife of Golasgil, oversees coastal settlements (age 53)
        'skills': dict(OneHanded=65, TwoHanded=35, Polearm=55, Bow=90, Crossbow=70, Throwing=55,
                       Riding=160, Athletics=130, Crafting=180, Scouting=160, Tactics=200, Roguery=80,
                       Charm=255, Leadership=210, Trade=240, Steward=265, Medicine=235, Engineering=170),
        'traits': dict(Honor=2, Generosity=2, Calculating=2, Mercy=2, Valor=1,
                       Egalitarian=1, Oligarchic=1, Authoritarian=0),
    },

    # ---- Morthond / Duinhir (clan_empire_west_9) ----
    'lord_WE9_l': {  # Duinhir — Lord of Morthond, "leads archers from Black Root Vale", stern
        'skills': dict(OneHanded=205, TwoHanded=160, Polearm=215, Bow=290, Crossbow=140, Throwing=160,
                       Riding=190, Athletics=265, Crafting=110, Scouting=265, Tactics=245, Roguery=90,
                       Charm=180, Leadership=240, Trade=150, Steward=210, Medicine=140, Engineering=140),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=0, Valor=2,
                       Egalitarian=0, Oligarchic=1, Authoritarian=1),
    },
    'lord_WE9_u': {  # Duilin — "elder son of Duinhir, leads own company of archers"
        'archetype': 'ranger',
        'skills': dict(OneHanded=200, TwoHanded=150, Polearm=190, Bow=275, Crossbow=130, Throwing=160,
                       Riding=170, Athletics=255, Crafting=100, Scouting=255, Tactics=215, Roguery=90,
                       Charm=170, Leadership=210, Trade=120, Steward=170, Medicine=130, Engineering=120),
        'traits': dict(Honor=2, Generosity=1, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lord_WE9_u2': {  # Rosfin (now lord_WE9_u2 per text "wife of Duinhir manages affairs in absence")
        'archetype': 'lady',
    },

    # ---- Anfalas family / Olindurionath (WE8_*) ----
    'lord_WE8_c': {  # Pelendur — "son of Golasgil, being trained"
        'archetype': 'young_lord',
        'skills': dict(OneHanded=180, TwoHanded=140, Polearm=170, Bow=200, Crossbow=110, Throwing=140,
                       Riding=190, Athletics=215, Crafting=80, Scouting=220, Tactics=150, Roguery=80,
                       Charm=170, Leadership=140, Trade=130, Steward=140, Medicine=100, Engineering=100),
        'traits': dict(Honor=2, Generosity=1, Calculating=0, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lord_WE8_u': {  # Barandil — "brother of Golasgil, captain of coastal watch"
        'archetype': 'knight',
        'skills': dict(OneHanded=215, TwoHanded=165, Polearm=205, Bow=215, Crossbow=135, Throwing=160,
                       Riding=180, Athletics=235, Crafting=90, Scouting=245, Tactics=210, Roguery=100,
                       Charm=180, Leadership=200, Trade=200, Steward=180, Medicine=120, Engineering=140),
        'traits': dict(Honor=2, Generosity=1, Calculating=1, Mercy=1, Valor=2,
                       Egalitarian=1, Oligarchic=0, Authoritarian=0),
    },
    'lord_WE8_1': {  # Dorwen (Anfalas) — "came to House of Olindurionath from lesser fief upon windward coast"
        'archetype': 'lady',
    },

    # ---- Lord_1_57_1 Calathiel — "young noblewoman of Lossarnach"
    'lord_1_57_1': {
        'archetype': 'young_lady',
    },
}


# ============================================================================
# AUTO-ARCHETYPE FROM HEROES.XSLT BIO + GENDER + AGE
# ============================================================================

def infer_archetype(bio: str, age: int, female: bool) -> str:
    """Infer archetype from bio keywords + gender + age."""
    bl = bio.lower() if bio else ''
    # Specific role keywords win first
    if any(k in bl for k in ['ranger', 'archer', 'bowmen', 'scout', 'morthond', 'black root', 'blackroot']):
        return 'ranger' if not female else 'lady'
    if 'errand-rider' in bl or 'messenger' in bl or 'rider of gondor' in bl:
        return 'errand_rider'
    if 'steward' in bl and 'house of' in bl and not female:
        return 'steward'
    if any(k in bl for k in ['keeper of the lore', 'steward', 'manage', 'oversees', 'administers']) and female:
        return 'lady'
    if female:
        # matriarch if old + female + heads household
        if age >= 60 or any(k in bl for k in ['matriarch', 'princess', 'long years', 'enduring']):
            return 'matriarch'
        if age <= 25:
            return 'young_lady'
        return 'lady'
    # Male
    if age >= 60:
        return 'elder_lord'
    if age <= 25 or any(k in bl for k in ['young', 'youngest', 'trains', 'training', 'come of age', 'youngest sword']):
        return 'young_lord'
    if any(k in bl for k in ['knight', 'rides with', 'cavalry']):
        return 'knight'
    if any(k in bl for k in ['captain', 'commander', 'lord of', 'commands', 'rules']):
        return 'lord'
    return 'knight'


def get_skills_traits(npc_id: str, age: int, female: bool, bio: str = '') -> tuple[dict, dict]:
    """Resolve final skills + traits for an NPC."""
    if npc_id in CANONICAL:
        c = CANONICAL[npc_id]
        if 'skills' in c and 'traits' in c:
            return c['skills'], c['traits']
        # Partial override: apply archetype, then patch with c['skills']/c['traits'] if present
        arch = ARCHETYPES[c.get('archetype', infer_archetype(bio, age, female))]
        sk = dict(arch['skills']);  sk.update(c.get('skills', {}))
        tr = dict(arch['traits']);  tr.update(c.get('traits', {}))
        return sk, tr
    arch = ARCHETYPES[infer_archetype(bio, age, female)]
    return dict(arch['skills']), dict(arch['traits'])


# ============================================================================
# RENDERING + REPLACEMENT
# ============================================================================

def render_skills_block(skills: dict, indent: str) -> str:
    lines = [f'{indent}<skills>']
    for s in SKILL_ORDER:
        if s in skills:
            lines.append(f'{indent}    <skill id="{s}" value="{skills[s]}" />')
    lines.append(f'{indent}</skills>')
    return '\n'.join(lines)


def render_traits_block(traits: dict, indent: str) -> str:
    lines = [f'{indent}<Traits>']
    for t in TRAIT_ORDER:
        if t in traits:
            lines.append(f'{indent}    <Trait id="{t}" value="{traits[t]}" />')
    lines.append(f'{indent}</Traits>')
    return '\n'.join(lines)


def detect_indent_from_block(block: str, block_open_tag: str) -> str:
    """Return the leading whitespace before the block open tag, e.g. '        '."""
    m = re.search(r'(\n)?(\s*)' + re.escape(block_open_tag), block)
    if m:
        return m.group(2)
    return '        '


def replace_skills_traits_in_block(block: str, skills: dict, traits: dict) -> tuple[str, bool, bool]:
    """Replace <skills>...</skills> and <Traits>...</Traits> (or self-closed) in block.
    Returns (new_block, skills_changed, traits_changed)."""
    # Skills
    sk_replaced = False
    sk_self = re.search(r'(\s*)<skills\s*/>', block)
    sk_block = re.search(r'(\s*)<skills>.*?</skills>', block, re.DOTALL)
    if sk_self:
        indent = sk_self.group(1).lstrip('\n').rstrip(' ')  # preserve newline if any
        leading = sk_self.group(1)
        new_block_str = render_skills_block(skills, leading.lstrip('\n'))
        block = block[:sk_self.start()] + ('\n' if leading.startswith('\n') else '') + new_block_str + block[sk_self.end():]
        sk_replaced = True
    elif sk_block:
        leading = sk_block.group(1)
        new_block_str = render_skills_block(skills, leading.lstrip('\n'))
        block = block[:sk_block.start()] + ('\n' if leading.startswith('\n') else '') + new_block_str + block[sk_block.end():]
        sk_replaced = True
    # Traits
    tr_replaced = False
    tr_self = re.search(r'(\s*)<Traits\s*/>', block)
    tr_block = re.search(r'(\s*)<Traits>.*?</Traits>', block, re.DOTALL)
    if tr_self:
        leading = tr_self.group(1)
        new_block_str = render_traits_block(traits, leading.lstrip('\n'))
        block = block[:tr_self.start()] + ('\n' if leading.startswith('\n') else '') + new_block_str + block[tr_self.end():]
        tr_replaced = True
    elif tr_block:
        leading = tr_block.group(1)
        new_block_str = render_traits_block(traits, leading.lstrip('\n'))
        block = block[:tr_block.start()] + ('\n' if leading.startswith('\n') else '') + new_block_str + block[tr_block.end():]
        tr_replaced = True
    return block, sk_replaced, tr_replaced


# ============================================================================
# MAIN
# ============================================================================

def find_npc_in_lords_xml(text: str, npc_id: str) -> tuple[int, int, dict]:
    """Find NPCCharacter block in lords.xml; return (start, end, attrs_dict)."""
    pattern = re.compile(r'<NPCCharacter id="' + re.escape(npc_id) + r'"([^>]*)>')
    m = pattern.search(text)
    if not m:
        return None
    start = m.start()
    end_close = text.find('</NPCCharacter>', m.end())
    if end_close == -1:
        return None
    end = end_close + len('</NPCCharacter>')
    attrs = m.group(1)
    fem = bool(re.search(r'is_female="(?:[Tt]rue|TRUE)"', attrs))
    age_m = re.search(r'\sage="(\d+)"', attrs)
    age = int(age_m.group(1)) if age_m else 0
    cult_m = re.search(r'culture="([^"]+)"', attrs)
    cult = cult_m.group(1) if cult_m else ''
    return (start, end, dict(female=fem, age=age, culture=cult))


def find_npc_in_lords_xslt(text: str, npc_id: str) -> tuple[int, int, dict]:
    """Find NPCCharacter template in lords.xslt; return (start, end, attrs_dict)."""
    op = re.search(r'<xsl:template match="NPCCharacter\[@id=\'' + re.escape(npc_id) + r'\'\]', text)
    if not op:
        return None
    end_close = text.find('</xsl:template>', op.end())
    if end_close == -1:
        return None
    end = end_close + len('</xsl:template>')
    body = text[op.start():end]
    # Pull is_female/age/culture from the xsl:attribute lines
    fem_m = re.search(r'name="is_female">([Tt]rue|TRUE)', body)
    age_m = re.search(r'name="age">(\d+)', body)
    cult_m = re.search(r'name="culture">([^<]+)<', body)
    return (op.start(), end, dict(
        female=bool(fem_m),
        age=int(age_m.group(1)) if age_m else 0,
        culture=cult_m.group(1) if cult_m else '',
    ))


def load_bios() -> dict:
    """Pull bio text per hero_id from heroes.xslt for archetype inference."""
    hx = HEROES_XSLT.read_text(encoding='utf-8')
    bios = {}
    for m in re.finditer(r"<xsl:template match=\"Hero\[@id='([^']+)'\]", hx):
        hid = m.group(1)
        start = m.end()
        end = hx.find('</xsl:template>', start)
        body = hx[start:end]
        tm = re.search(r'\{=[A-Za-z_0-9]+\}([^<]+)', body)
        if tm:
            bios[hid] = tm.group(1).strip()
    return bios


def gondor_npcs_in_lords_xml(text: str) -> list[str]:
    """All NPC ids with culture=Culture.gondor in lords.xml."""
    out = []
    for m in re.finditer(r'<NPCCharacter id="([^"]+)"[^>]*culture="Culture\.gondor"', text):
        out.append(m.group(1))
    return out


def gondor_npcs_in_lords_xslt(text: str) -> list[str]:
    """All NPC template ids in lords.xslt whose body sets culture=Culture.gondor."""
    out = []
    for m in re.finditer(r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]", text):
        op_end = m.end()
        end = text.find('</xsl:template>', op_end)
        body = text[op_end:end]
        if 'name="culture">Culture.gondor' in body:
            out.append(m.group(1))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--apply', action='store_true')
    args = ap.parse_args()

    bios = load_bios()
    print(f"Loaded {len(bios)} bios from heroes.xslt")

    # ---- lords.xml ----
    xml_text = LORDS_XML.read_text(encoding='utf-8')
    original_xml = xml_text
    gondor_xml = gondor_npcs_in_lords_xml(xml_text)
    print(f"\nGondor NPCs in lords.xml: {len(gondor_xml)}")
    touched_xml = 0
    skipped_xml = 0
    for nid in gondor_xml:
        info = find_npc_in_lords_xml(xml_text, nid)
        if not info:
            continue
        start, end, attrs = info
        # Skip children (<14)
        if attrs['age'] < 14:
            print(f"  skip child {nid} (age {attrs['age']})")
            skipped_xml += 1
            continue
        sk, tr = get_skills_traits(nid, attrs['age'], attrs['female'], bios.get(nid, ''))
        block = xml_text[start:end]
        new_block, sk_done, tr_done = replace_skills_traits_in_block(block, sk, tr)
        if new_block != block:
            xml_text = xml_text[:start] + new_block + xml_text[end:]
            touched_xml += 1
        else:
            print(f"  WARN {nid}: no skills/traits block found to replace")

    # ---- lords.xslt ----
    xslt_text = LORDS_XSLT.read_text(encoding='utf-8')
    original_xslt = xslt_text
    gondor_xslt = gondor_npcs_in_lords_xslt(xslt_text)
    print(f"\nGondor NPCs in lords.xslt: {len(gondor_xslt)}")
    touched_xslt = 0
    skipped_xslt = 0
    for nid in gondor_xslt:
        info = find_npc_in_lords_xslt(xslt_text, nid)
        if not info:
            continue
        start, end, attrs = info
        if attrs['age'] < 14:
            print(f"  skip child {nid} (age {attrs['age']})")
            skipped_xslt += 1
            continue
        sk, tr = get_skills_traits(nid, attrs['age'], attrs['female'], bios.get(nid, ''))
        block = xslt_text[start:end]
        new_block, sk_done, tr_done = replace_skills_traits_in_block(block, sk, tr)
        if new_block != block:
            xslt_text = xslt_text[:start] + new_block + xslt_text[end:]
            touched_xslt += 1

    print(f"\nlords.xml: touched {touched_xml}, skipped {skipped_xml}")
    print(f"lords.xslt: touched {touched_xslt}, skipped {skipped_xslt}")

    if args.apply:
        LORDS_XML.write_text(xml_text, encoding='utf-8')
        LORDS_XSLT.write_text(xslt_text, encoding='utf-8')
        print("WROTE lords.xml + lords.xslt")
    else:
        print("(dry-run — pass --apply to write)")


if __name__ == '__main__':
    main()
