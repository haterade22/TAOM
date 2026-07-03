#!/usr/bin/env python3
"""Apply lore-driven skills + traits per culture across lords.xml + lords.xslt.

Usage:
    python tools/apply_culture_skills_traits.py --culture rohan [--apply]

Adds skills+traits to every adult NPC of a given culture. Children (age<14) skipped.

Per-culture canonical-overrides live in CULTURES dict; archetypes are shared.
"""
import argparse
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LORDS_XML = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"
LORDS_XSLT = REPO / "Main" / "_Module" / "ModuleData" / "lords.xslt"
HEROES_XSLT = REPO / "Main" / "_Module" / "ModuleData" / "heroes.xslt"
LORD_SKILL_SETS = REPO / "Main" / "_Module" / "ModuleData" / "taom_lord_skill_sets.xml"

SKILL_ORDER = ['OneHanded','TwoHanded','Polearm','Bow','Crossbow','Throwing','Riding','Athletics',
               'Crafting','Scouting','Tactics','Roguery','Charm','Leadership','Trade','Steward',
               'Medicine','Engineering']
TRAIT_ORDER = ['Honor','Generosity','Calculating','Mercy','Valor','Egalitarian','Oligarchic','Authoritarian']

# ============================================================================
# SHARED ARCHETYPES — base templates, mutable per culture
# ============================================================================
BASE_ARCHETYPES = {
    'lord':         dict(skills=dict(OneHanded=220,TwoHanded=160,Polearm=210,Bow=120,Crossbow=90,Throwing=120,
                                     Riding=220,Athletics=220,Crafting=80,Scouting=180,Tactics=220,Roguery=70,
                                     Charm=190,Leadership=240,Trade=140,Steward=210,Medicine=110,Engineering=150),
                         traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),
    'knight':       dict(skills=dict(OneHanded=230,TwoHanded=180,Polearm=220,Bow=100,Crossbow=70,Throwing=120,
                                     Riding=250,Athletics=240,Crafting=60,Scouting=140,Tactics=180,Roguery=60,
                                     Charm=160,Leadership=160,Trade=90,Steward=130,Medicine=90,Engineering=100),
                         traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=0)),
    'ranger':       dict(skills=dict(OneHanded=200,TwoHanded=140,Polearm=180,Bow=270,Crossbow=140,Throwing=170,
                                     Riding=170,Athletics=260,Crafting=100,Scouting=270,Tactics=210,Roguery=130,
                                     Charm=170,Leadership=190,Trade=100,Steward=150,Medicine=140,Engineering=100),
                         traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
    'lady':         dict(skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=70,Crossbow=50,Throwing=50,
                                     Riding=140,Athletics=110,Crafting=140,Scouting=110,Tactics=130,Roguery=60,
                                     Charm=240,Leadership=160,Trade=180,Steward=240,Medicine=210,Engineering=130),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=0,Egalitarian=1,Oligarchic=1,Authoritarian=0)),
    'matriarch':    dict(skills=dict(OneHanded=50,TwoHanded=25,Polearm=40,Bow=60,Crossbow=40,Throwing=40,
                                     Riding=130,Athletics=90,Crafting=170,Scouting=130,Tactics=190,Roguery=70,
                                     Charm=285,Leadership=220,Trade=240,Steward=285,Medicine=245,Engineering=160),
                         traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=0,Egalitarian=1,Oligarchic=1,Authoritarian=1)),
    'elder_lord':   dict(skills=dict(OneHanded=200,TwoHanded=150,Polearm=190,Bow=90,Crossbow=70,Throwing=90,
                                     Riding=180,Athletics=160,Crafting=110,Scouting=180,Tactics=270,Roguery=80,
                                     Charm=220,Leadership=260,Trade=180,Steward=240,Medicine=160,Engineering=190),
                         traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=2,Authoritarian=1)),
    'young_lord':   dict(skills=dict(OneHanded=160,TwoHanded=120,Polearm=140,Bow=90,Crossbow=70,Throwing=100,
                                     Riding=190,Athletics=180,Crafting=60,Scouting=140,Tactics=130,Roguery=60,
                                     Charm=140,Leadership=120,Trade=80,Steward=110,Medicine=80,Engineering=80),
                         traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=0)),
    'young_lady':   dict(skills=dict(OneHanded=40,TwoHanded=20,Polearm=30,Bow=70,Crossbow=40,Throwing=40,
                                     Riding=120,Athletics=100,Crafting=110,Scouting=90,Tactics=100,Roguery=50,
                                     Charm=180,Leadership=120,Trade=130,Steward=170,Medicine=150,Engineering=100),
                         traits=dict(Honor=1,Generosity=1,Calculating=1,Mercy=2,Valor=0,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
    'steward':      dict(skills=dict(OneHanded=110,TwoHanded=70,Polearm=100,Bow=70,Crossbow=60,Throwing=70,
                                     Riding=170,Athletics=140,Crafting=140,Scouting=140,Tactics=210,Roguery=90,
                                     Charm=260,Leadership=220,Trade=240,Steward=275,Medicine=170,Engineering=200),
                         traits=dict(Honor=1,Generosity=1,Calculating=2,Mercy=0,Valor=0,Egalitarian=0,Oligarchic=2,Authoritarian=1)),
    'errand_rider': dict(skills=dict(OneHanded=160,TwoHanded=110,Polearm=140,Bow=140,Crossbow=90,Throwing=110,
                                     Riding=280,Athletics=240,Crafting=60,Scouting=270,Tactics=160,Roguery=110,
                                     Charm=150,Leadership=140,Trade=130,Steward=120,Medicine=90,Engineering=80),
                         traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=0)),

    # ROHAN-SPECIFIC
    'rider':        dict(skills=dict(OneHanded=240,TwoHanded=180,Polearm=255,Bow=140,Crossbow=40,Throwing=130,
                                     Riding=270,Athletics=240,Crafting=70,Scouting=200,Tactics=200,Roguery=70,
                                     Charm=180,Leadership=200,Trade=110,Steward=160,Medicine=110,Engineering=100),
                         traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
    'shieldmaiden': dict(skills=dict(OneHanded=240,TwoHanded=200,Polearm=225,Bow=150,Crossbow=60,Throwing=140,
                                     Riding=255,Athletics=240,Crafting=80,Scouting=180,Tactics=210,Roguery=80,
                                     Charm=200,Leadership=180,Trade=120,Steward=190,Medicine=170,Engineering=100),
                         traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
    'horse_breeder':dict(skills=dict(OneHanded=170,TwoHanded=120,Polearm=180,Bow=130,Crossbow=40,Throwing=110,
                                     Riding=290,Athletics=210,Crafting=190,Scouting=200,Tactics=160,Roguery=80,
                                     Charm=170,Leadership=170,Trade=210,Steward=200,Medicine=150,Engineering=120),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=1,Valor=1,Egalitarian=1,Oligarchic=0,Authoritarian=0)),

    # DALE / NORTHMEN — Bowman culture (Bard's line)
    'dale_lord':    dict(skills=dict(OneHanded=215,TwoHanded=155,Polearm=205,Bow=260,Crossbow=120,Throwing=170,
                                     Riding=215,Athletics=240,Crafting=110,Scouting=230,Tactics=220,Roguery=80,
                                     Charm=200,Leadership=235,Trade=210,Steward=215,Medicine=140,Engineering=170),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=0)),
    'dale_bowman':  dict(skills=dict(OneHanded=190,TwoHanded=130,Polearm=170,Bow=270,Crossbow=140,Throwing=170,
                                     Riding=180,Athletics=245,Crafting=110,Scouting=240,Tactics=200,Roguery=100,
                                     Charm=170,Leadership=180,Trade=180,Steward=180,Medicine=140,Engineering=130),
                         traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),

    # DWARVES — Heavy melee, high Smithing/Crafting/Engineering, slow but tough
    'dwarf_king':   dict(skills=dict(OneHanded=275,TwoHanded=280,Polearm=240,Bow=130,Crossbow=180,Throwing=140,
                                     Riding=150,Athletics=250,Crafting=275,Scouting=180,Tactics=265,Roguery=80,
                                     Charm=230,Leadership=290,Trade=240,Steward=275,Medicine=170,Engineering=275),
                         traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=2,Authoritarian=1)),
    'dwarf_lord':   dict(skills=dict(OneHanded=240,TwoHanded=255,Polearm=215,Bow=110,Crossbow=160,Throwing=130,
                                     Riding=130,Athletics=225,Crafting=240,Scouting=170,Tactics=215,Roguery=70,
                                     Charm=190,Leadership=225,Trade=200,Steward=220,Medicine=150,Engineering=240),
                         traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=2,Authoritarian=1)),
    'dwarf_warrior':dict(skills=dict(OneHanded=235,TwoHanded=265,Polearm=200,Bow=100,Crossbow=140,Throwing=130,
                                     Riding=120,Athletics=240,Crafting=180,Scouting=160,Tactics=180,Roguery=70,
                                     Charm=140,Leadership=155,Trade=130,Steward=160,Medicine=110,Engineering=180),
                         traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=0)),
    'dwarf_lady':   dict(skills=dict(OneHanded=140,TwoHanded=130,Polearm=110,Bow=80,Crossbow=110,Throwing=90,
                                     Riding=100,Athletics=170,Crafting=255,Scouting=140,Tactics=170,Roguery=70,
                                     Charm=240,Leadership=190,Trade=220,Steward=260,Medicine=220,Engineering=200),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),
    'dwarf_young':  dict(skills=dict(OneHanded=170,TwoHanded=190,Polearm=150,Bow=80,Crossbow=130,Throwing=100,
                                     Riding=110,Athletics=200,Crafting=170,Scouting=140,Tactics=140,Roguery=60,
                                     Charm=140,Leadership=130,Trade=140,Steward=150,Medicine=110,Engineering=160),
                         traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=0)),

    # ELVES — Centuries of mastery; combat + diplomacy + crafting all high
    'elf_king':     dict(skills=dict(OneHanded=290,TwoHanded=250,Polearm=295,Bow=295,Crossbow=200,Throwing=210,
                                     Riding=280,Athletics=290,Crafting=270,Scouting=290,Tactics=290,Roguery=120,
                                     Charm=285,Leadership=290,Trade=240,Steward=285,Medicine=275,Engineering=240),
                         traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=2,Authoritarian=1)),
    'elf_lord':     dict(skills=dict(OneHanded=270,TwoHanded=230,Polearm=265,Bow=275,Crossbow=180,Throwing=190,
                                     Riding=255,Athletics=280,Crafting=240,Scouting=270,Tactics=265,Roguery=110,
                                     Charm=250,Leadership=255,Trade=200,Steward=255,Medicine=240,Engineering=210),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=0)),
    'elf_warrior':  dict(skills=dict(OneHanded=275,TwoHanded=240,Polearm=275,Bow=265,Crossbow=170,Throwing=190,
                                     Riding=240,Athletics=285,Crafting=170,Scouting=255,Tactics=240,Roguery=110,
                                     Charm=210,Leadership=220,Trade=160,Steward=200,Medicine=190,Engineering=170),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
    'elf_archer':   dict(skills=dict(OneHanded=235,TwoHanded=180,Polearm=240,Bow=295,Crossbow=190,Throwing=210,
                                     Riding=220,Athletics=290,Crafting=180,Scouting=290,Tactics=240,Roguery=110,
                                     Charm=220,Leadership=210,Trade=160,Steward=200,Medicine=190,Engineering=160),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
    'elf_lady':     dict(skills=dict(OneHanded=180,TwoHanded=130,Polearm=170,Bow=220,Crossbow=150,Throwing=150,
                                     Riding=210,Athletics=230,Crafting=240,Scouting=210,Tactics=210,Roguery=80,
                                     Charm=270,Leadership=200,Trade=220,Steward=265,Medicine=260,Engineering=190),
                         traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),
    'elf_queen':    dict(skills=dict(OneHanded=240,TwoHanded=200,Polearm=240,Bow=270,Crossbow=190,Throwing=200,
                                     Riding=260,Athletics=270,Crafting=275,Scouting=265,Tactics=290,Roguery=110,
                                     Charm=295,Leadership=290,Trade=265,Steward=290,Medicine=285,Engineering=230),
                         traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=2,Authoritarian=1)),
    'elf_young':    dict(skills=dict(OneHanded=210,TwoHanded=160,Polearm=200,Bow=230,Crossbow=140,Throwing=160,
                                     Riding=210,Athletics=240,Crafting=170,Scouting=220,Tactics=190,Roguery=90,
                                     Charm=200,Leadership=180,Trade=170,Steward=190,Medicine=180,Engineering=170),
                         traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=0)),

    # ORCS / URUKS — Brutal combat, low diplomacy/mercy
    'orc_chieftain':dict(skills=dict(OneHanded=275,TwoHanded=265,Polearm=240,Bow=170,Crossbow=110,Throwing=190,
                                     Riding=190,Athletics=265,Crafting=140,Scouting=240,Tactics=255,Roguery=240,
                                     Charm=180,Leadership=270,Trade=140,Steward=200,Medicine=90,Engineering=180),
                         traits=dict(Honor=-2,Generosity=-1,Calculating=2,Mercy=-2,Valor=2,Egalitarian=-1,Oligarchic=1,Authoritarian=2)),
    'orc_warrior':  dict(skills=dict(OneHanded=235,TwoHanded=220,Polearm=215,Bow=140,Crossbow=90,Throwing=160,
                                     Riding=150,Athletics=240,Crafting=110,Scouting=200,Tactics=180,Roguery=200,
                                     Charm=120,Leadership=160,Trade=100,Steward=130,Medicine=70,Engineering=120),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=1,Mercy=-2,Valor=2,Egalitarian=-1,Oligarchic=0,Authoritarian=1)),
    'orc_berserker':dict(skills=dict(OneHanded=240,TwoHanded=285,Polearm=210,Bow=120,Crossbow=80,Throwing=170,
                                     Riding=130,Athletics=275,Crafting=100,Scouting=180,Tactics=140,Roguery=210,
                                     Charm=80,Leadership=130,Trade=80,Steward=100,Medicine=60,Engineering=100),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=-1,Mercy=-2,Valor=2,Egalitarian=-1,Oligarchic=-1,Authoritarian=0)),
    'orc_scout':    dict(skills=dict(OneHanded=210,TwoHanded=170,Polearm=200,Bow=255,Crossbow=140,Throwing=210,
                                     Riding=210,Athletics=275,Crafting=110,Scouting=270,Tactics=180,Roguery=240,
                                     Charm=110,Leadership=140,Trade=90,Steward=110,Medicine=80,Engineering=110),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=2,Mercy=-2,Valor=2,Egalitarian=-1,Oligarchic=-1,Authoritarian=0)),
    'orc_warg':     dict(skills=dict(OneHanded=240,TwoHanded=200,Polearm=240,Bow=180,Crossbow=100,Throwing=180,
                                     Riding=285,Athletics=240,Crafting=100,Scouting=255,Tactics=200,Roguery=210,
                                     Charm=120,Leadership=180,Trade=90,Steward=120,Medicine=70,Engineering=110),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=1,Mercy=-2,Valor=2,Egalitarian=-1,Oligarchic=0,Authoritarian=1)),
    'orc_female':   dict(skills=dict(OneHanded=170,TwoHanded=160,Polearm=160,Bow=160,Crossbow=110,Throwing=140,
                                     Riding=110,Athletics=200,Crafting=180,Scouting=170,Tactics=160,Roguery=210,
                                     Charm=130,Leadership=150,Trade=130,Steward=180,Medicine=120,Engineering=160),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=2,Mercy=-2,Valor=1,Egalitarian=-1,Oligarchic=0,Authoritarian=1)),
    'nazgul':       dict(skills=dict(OneHanded=290,TwoHanded=270,Polearm=290,Bow=210,Crossbow=180,Throwing=210,
                                     Riding=290,Athletics=285,Crafting=210,Scouting=290,Tactics=295,Roguery=290,
                                     Charm=280,Leadership=295,Trade=190,Steward=260,Medicine=170,Engineering=240),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=2,Mercy=-2,Valor=2,Egalitarian=-2,Oligarchic=2,Authoritarian=2)),
    # sauron — hand-tuned in 1f7a7a9a (legendary-lord hierarchy); synced from live XML
    'sauron':       dict(skills=dict(OneHanded=320,TwoHanded=310,Polearm=320,Bow=283,Crossbow=281,Throwing=281,Riding=281,Athletics=320,Crafting=330,Scouting=281,Tactics=330,Roguery=320,Charm=281,Leadership=330,Trade=281,Steward=281,Medicine=300,Engineering=320),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=2,Mercy=-2,Valor=2,Egalitarian=-2,Oligarchic=2,Authoritarian=2)),
    # witch_king — hand-tuned in 1f7a7a9a (legendary-lord hierarchy); synced from live XML
    'witch_king':       dict(skills=dict(OneHanded=315,TwoHanded=251,Polearm=300,Bow=246,Crossbow=246,Throwing=246,Riding=305,Athletics=300,Crafting=246,Scouting=246,Tactics=315,Roguery=310,Charm=246,Leadership=320,Trade=246,Steward=246,Medicine=235,Engineering=246),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=2,Mercy=-2,Valor=2,Egalitarian=-2,Oligarchic=2,Authoritarian=2)),
    'black_numenorean': dict(skills=dict(OneHanded=255,TwoHanded=210,Polearm=240,Bow=210,Crossbow=170,Throwing=180,
                                         Riding=240,Athletics=255,Crafting=200,Scouting=240,Tactics=260,Roguery=240,
                                         Charm=270,Leadership=260,Trade=240,Steward=255,Medicine=190,Engineering=240),
                            traits=dict(Honor=-2,Generosity=-1,Calculating=2,Mercy=-2,Valor=2,Egalitarian=-2,Oligarchic=2,Authoritarian=2)),
    'bn_sorceress': dict(skills=dict(OneHanded=140,TwoHanded=100,Polearm=130,Bow=180,Crossbow=140,Throwing=140,
                                     Riding=180,Athletics=200,Crafting=240,Scouting=210,Tactics=240,Roguery=250,
                                     Charm=275,Leadership=230,Trade=240,Steward=260,Medicine=250,Engineering=210),
                         traits=dict(Honor=-2,Generosity=-2,Calculating=2,Mercy=-2,Valor=1,Egalitarian=-2,Oligarchic=2,Authoritarian=2)),

    # DUNLAND — Norse-themed shieldmaidens + raiders, Saruman's allies
    'dunland_warrior': dict(skills=dict(OneHanded=245,TwoHanded=215,Polearm=215,Bow=170,Crossbow=80,Throwing=170,
                                        Riding=180,Athletics=255,Crafting=120,Scouting=220,Tactics=200,Roguery=200,
                                        Charm=160,Leadership=200,Trade=120,Steward=170,Medicine=140,Engineering=120),
                            traits=dict(Honor=1,Generosity=0,Calculating=1,Mercy=-1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
    'dunland_raider': dict(skills=dict(OneHanded=235,TwoHanded=200,Polearm=210,Bow=200,Crossbow=80,Throwing=190,
                                       Riding=200,Athletics=265,Crafting=110,Scouting=255,Tactics=190,Roguery=240,
                                       Charm=140,Leadership=180,Trade=130,Steward=150,Medicine=120,Engineering=110),
                           traits=dict(Honor=0,Generosity=0,Calculating=1,Mercy=-1,Valor=2,Egalitarian=1,Oligarchic=-1,Authoritarian=0)),
    'dunland_brenin': dict(skills=dict(OneHanded=265,TwoHanded=235,Polearm=240,Bow=180,Crossbow=80,Throwing=180,
                                       Riding=200,Athletics=255,Crafting=130,Scouting=230,Tactics=250,Roguery=210,
                                       Charm=200,Leadership=265,Trade=170,Steward=220,Medicine=150,Engineering=150),
                           traits=dict(Honor=1,Generosity=1,Calculating=2,Mercy=-1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),

    # HARADRIM — desert peoples, scimitar + mumakil, scarlet+gold
    'haradrim_lord': dict(skills=dict(OneHanded=235,TwoHanded=170,Polearm=215,Bow=210,Crossbow=110,Throwing=180,
                                      Riding=260,Athletics=235,Crafting=120,Scouting=235,Tactics=230,Roguery=130,
                                      Charm=220,Leadership=235,Trade=220,Steward=215,Medicine=140,Engineering=170),
                          traits=dict(Honor=1,Generosity=1,Calculating=1,Mercy=0,Valor=2,Egalitarian=0,Oligarchic=2,Authoritarian=1)),
    'haradrim_cav': dict(skills=dict(OneHanded=230,TwoHanded=160,Polearm=240,Bow=210,Crossbow=100,Throwing=180,
                                     Riding=280,Athletics=235,Crafting=80,Scouting=225,Tactics=180,Roguery=120,
                                     Charm=160,Leadership=180,Trade=130,Steward=150,Medicine=110,Engineering=100),
                         traits=dict(Honor=1,Generosity=0,Calculating=0,Mercy=-1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=0)),
    'mumak_rider': dict(skills=dict(OneHanded=210,TwoHanded=200,Polearm=255,Bow=240,Crossbow=110,Throwing=200,
                                    Riding=255,Athletics=240,Crafting=130,Scouting=240,Tactics=215,Roguery=110,
                                    Charm=170,Leadership=210,Trade=180,Steward=170,Medicine=120,Engineering=140),
                        traits=dict(Honor=1,Generosity=1,Calculating=1,Mercy=0,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),
    'desert_lady': dict(skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=110,Crossbow=70,Throwing=70,
                                    Riding=180,Athletics=130,Crafting=170,Scouting=140,Tactics=140,Roguery=80,
                                    Charm=255,Leadership=170,Trade=235,Steward=240,Medicine=215,Engineering=130),
                        traits=dict(Honor=1,Generosity=1,Calculating=2,Mercy=1,Valor=0,Egalitarian=0,Oligarchic=1,Authoritarian=0)),

    # VARIAGS OF KHAND — Slavic-Mongol cavalry/raiders
    'variag_lord': dict(skills=dict(OneHanded=240,TwoHanded=200,Polearm=245,Bow=240,Crossbow=80,Throwing=190,
                                    Riding=275,Athletics=240,Crafting=110,Scouting=240,Tactics=215,Roguery=170,
                                    Charm=180,Leadership=225,Trade=160,Steward=190,Medicine=130,Engineering=130),
                        traits=dict(Honor=1,Generosity=0,Calculating=1,Mercy=-1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),
    'variag_lady': dict(skills=dict(OneHanded=130,TwoHanded=80,Polearm=110,Bow=160,Crossbow=70,Throwing=120,
                                    Riding=235,Athletics=180,Crafting=170,Scouting=200,Tactics=170,Roguery=110,
                                    Charm=210,Leadership=170,Trade=190,Steward=220,Medicine=180,Engineering=140),
                        traits=dict(Honor=1,Generosity=0,Calculating=1,Mercy=0,Valor=1,Egalitarian=1,Oligarchic=0,Authoritarian=0)),

    # EASTERLINGS / RHÛN — chariot/wainrider cavalry, Mongol-Turkic flavor
    'easterling_lord': dict(skills=dict(OneHanded=230,TwoHanded=170,Polearm=235,Bow=260,Crossbow=110,Throwing=180,
                                        Riding=275,Athletics=235,Crafting=120,Scouting=240,Tactics=235,Roguery=170,
                                        Charm=190,Leadership=235,Trade=180,Steward=200,Medicine=140,Engineering=170),
                            traits=dict(Honor=1,Generosity=0,Calculating=2,Mercy=-1,Valor=2,Egalitarian=0,Oligarchic=2,Authoritarian=2)),
    'easterling_archer': dict(skills=dict(OneHanded=210,TwoHanded=140,Polearm=210,Bow=275,Crossbow=130,Throwing=190,
                                          Riding=265,Athletics=245,Crafting=100,Scouting=255,Tactics=200,Roguery=150,
                                          Charm=150,Leadership=170,Trade=150,Steward=160,Medicine=120,Engineering=130),
                              traits=dict(Honor=1,Generosity=0,Calculating=1,Mercy=-1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),
    'easterling_lady': dict(skills=dict(OneHanded=80,TwoHanded=50,Polearm=70,Bow=170,Crossbow=80,Throwing=80,
                                        Riding=215,Athletics=160,Crafting=160,Scouting=180,Tactics=150,Roguery=100,
                                        Charm=215,Leadership=160,Trade=190,Steward=225,Medicine=180,Engineering=140),
                            traits=dict(Honor=1,Generosity=0,Calculating=2,Mercy=0,Valor=1,Egalitarian=0,Oligarchic=1,Authoritarian=1)),

    # UMBAR CORSAIRS — Black Numenorean pirate lords, sea-warfare specialists
    'corsair_lord': dict(skills=dict(OneHanded=260,TwoHanded=215,Polearm=245,Bow=200,Crossbow=170,Throwing=200,
                                     Riding=200,Athletics=265,Crafting=160,Scouting=240,Tactics=260,Roguery=265,
                                     Charm=240,Leadership=255,Trade=265,Steward=230,Medicine=160,Engineering=210),
                         traits=dict(Honor=-1,Generosity=0,Calculating=2,Mercy=-1,Valor=2,Egalitarian=0,Oligarchic=2,Authoritarian=1)),
    'corsair_captain': dict(skills=dict(OneHanded=240,TwoHanded=200,Polearm=225,Bow=190,Crossbow=160,Throwing=190,
                                        Riding=170,Athletics=255,Crafting=140,Scouting=230,Tactics=225,Roguery=240,
                                        Charm=200,Leadership=215,Trade=220,Steward=200,Medicine=130,Engineering=180),
                            traits=dict(Honor=-1,Generosity=0,Calculating=2,Mercy=-1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),
}


# ============================================================================
# CULTURE DATA — keyword → archetype mapping + canonical character overrides
# ============================================================================
CULTURES = {
    # ====================================================================
    # GONDOR — Men of the West
    # ====================================================================
    'gondor': {
        'culture_id': 'gondor',
        'lore_name': 'Gondor',
        'race': 'man',
        'keyword_archetypes': [
            (['ranger', 'archer', 'bowmen', 'morthond', 'black root', 'blackroot'], 'ranger'),
            (['errand-rider', 'messenger', 'rider of gondor'], 'errand_rider'),
            (['keeper of the lore'], 'steward'),
            (['captain', 'commander', 'commands', 'rules', 'lord of'], 'lord'),
            (['knight', 'rides with', 'cavalry'], 'knight'),
        ],
        'canonical': {
            # Stewards
            'lord_1_7': dict(skills=dict(OneHanded=170,TwoHanded=120,Polearm=150,Bow=130,Crossbow=110,Throwing=100,Riding=190,Athletics=187,Crafting=187,Scouting=187,Tactics=255,Roguery=187,Charm=255,Leadership=290,Trade=225,Steward=310,Medicine=187,Engineering=235),
                          traits=dict(Honor=1,Generosity=0,Calculating=2,Mercy=-1,Valor=1,Egalitarian=-1,Oligarchic=2,Authoritarian=2)),  # Denethor
            'lord_1_75': dict(skills=dict(OneHanded=325,TwoHanded=210,Polearm=290,Bow=210,Crossbow=210,Throwing=210,Riding=210,Athletics=315,Crafting=210,Scouting=210,Tactics=295,Roguery=210,Charm=210,Leadership=310,Trade=210,Steward=210,Medicine=210,Engineering=210),
                          traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),  # Boromir
            'lord_1_34': dict(skills=dict(OneHanded=250,TwoHanded=186,Polearm=189,Bow=310,Crossbow=189,Throwing=189,Riding=189,Athletics=280,Crafting=189,Scouting=300,Tactics=295,Roguery=189,Charm=255,Leadership=189,Trade=189,Steward=189,Medicine=189,Engineering=189),
                          traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=2,Oligarchic=0,Authoritarian=-1)),  # Faramir
            'lord_1_8':  dict(skills=dict(OneHanded=130,TwoHanded=90,Polearm=110,Bow=80,Crossbow=70,Throwing=70,Riding=170,Athletics=140,Crafting=160,Scouting=150,Tactics=230,Roguery=80,Charm=250,Leadership=210,Trade=230,Steward=275,Medicine=180,Engineering=210),
                          traits=dict(Honor=2,Generosity=1,Calculating=2,Mercy=1,Valor=1,Egalitarian=0,Oligarchic=2,Authoritarian=1)),  # Hurioneth
            'lord_1_44': dict(skills=dict(OneHanded=260,TwoHanded=210,Polearm=250,Bow=110,Crossbow=90,Throwing=120,Riding=180,Athletics=250,Crafting=80,Scouting=160,Tactics=220,Roguery=70,Charm=170,Leadership=210,Trade=100,Steward=170,Medicine=110,Engineering=130),
                          traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=2)),  # Nemos
            # Dol Amroth
            'lord_1_9':  dict(skills=dict(OneHanded=285,TwoHanded=209,Polearm=295,Bow=203,Crossbow=203,Throwing=203,Riding=320,Athletics=203,Crafting=203,Scouting=203,Tactics=203,Roguery=203,Charm=280,Leadership=300,Trade=203,Steward=203,Medicine=203,Engineering=203),
                          traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),  # Imrahil
            'lord_1_9_5': dict(skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=70,Crossbow=50,Throwing=50,Riding=160,Athletics=110,Crafting=180,Scouting=140,Tactics=210,Roguery=80,Charm=295,Leadership=240,Trade=250,Steward=295,Medicine=260,Engineering=170),
                          traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=0,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Lothwen
            'lord_1_25': dict(skills=dict(OneHanded=265,TwoHanded=190,Polearm=250,Bow=140,Crossbow=100,Throwing=140,Riding=285,Athletics=255,Crafting=80,Scouting=180,Tactics=230,Roguery=70,Charm=210,Leadership=235,Trade=140,Steward=200,Medicine=130,Engineering=140),
                          traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),  # Elphir
            'lord_1_35': dict(skills=dict(OneHanded=250,TwoHanded=200,Polearm=235,Bow=140,Crossbow=100,Throwing=150,Riding=270,Athletics=245,Crafting=70,Scouting=170,Tactics=200,Roguery=80,Charm=180,Leadership=190,Trade=110,Steward=150,Medicine=110,Engineering=110),
                          traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=0)),  # Erchirion
            'lord_1_24': dict(skills=dict(OneHanded=210,TwoHanded=170,Polearm=195,Bow=130,Crossbow=80,Throwing=120,Riding=240,Athletics=230,Crafting=70,Scouting=150,Tactics=170,Roguery=70,Charm=180,Leadership=160,Trade=100,Steward=140,Medicine=100,Engineering=100),
                          traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Amrothos
            'lord_1_10': dict(skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=80,Crossbow=60,Throwing=50,Riding=150,Athletics=110,Crafting=150,Scouting=120,Tactics=160,Roguery=70,Charm=255,Leadership=180,Trade=200,Steward=255,Medicine=215,Engineering=150),
                          traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=0,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Aranel
            'lord_1_23': dict(skills=dict(OneHanded=70,TwoHanded=40,Polearm=60,Bow=90,Crossbow=70,Throwing=60,Riding=160,Athletics=130,Crafting=130,Scouting=130,Tactics=140,Roguery=60,Charm=230,Leadership=170,Trade=180,Steward=235,Medicine=210,Engineering=130),
                          traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Caladwen
            # Earnurionath
            'lord_1_11': dict(skills=dict(OneHanded=235,TwoHanded=185,Polearm=225,Bow=160,Crossbow=120,Throwing=140,Riding=215,Athletics=225,Crafting=110,Scouting=250,Tactics=255,Roguery=110,Charm=190,Leadership=240,Trade=140,Steward=215,Medicine=150,Engineering=180),
                          traits=dict(Honor=2,Generosity=1,Calculating=2,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),  # Ciryandur
            'lord_1_111': dict(skills=dict(OneHanded=80,TwoHanded=50,Polearm=70,Bow=110,Crossbow=80,Throwing=70,Riding=170,Athletics=150,Crafting=170,Scouting=160,Tactics=160,Roguery=80,Charm=220,Leadership=180,Trade=200,Steward=240,Medicine=220,Engineering=160),
                          traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=1,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Elarwen
            'lord_1_12': dict(skills=dict(OneHanded=70,TwoHanded=40,Polearm=60,Bow=110,Crossbow=80,Throwing=60,Riding=170,Athletics=140,Crafting=160,Scouting=170,Tactics=170,Roguery=70,Charm=215,Leadership=170,Trade=180,Steward=235,Medicine=210,Engineering=150),
                          traits=dict(Honor=2,Generosity=1,Calculating=2,Mercy=1,Valor=1,Egalitarian=0,Oligarchic=1,Authoritarian=0)),  # Findariel
            'lord_1_26': dict(skills=dict(OneHanded=170,TwoHanded=110,Polearm=150,Bow=160,Crossbow=100,Throwing=120,Riding=285,Athletics=255,Crafting=70,Scouting=275,Tactics=170,Roguery=120,Charm=160,Leadership=150,Trade=140,Steward=130,Medicine=100,Engineering=90),
                          traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Hirgon
            # Barahirionath
            'lord_1_40': dict(skills=dict(OneHanded=180,TwoHanded=130,Polearm=170,Bow=110,Crossbow=90,Throwing=100,Riding=160,Athletics=140,Crafting=120,Scouting=170,Tactics=265,Roguery=80,Charm=235,Leadership=255,Trade=200,Steward=255,Medicine=170,Engineering=200),
                          traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=1,Valor=1,Egalitarian=0,Oligarchic=2,Authoritarian=1)),  # Borhador
            'lord_1_40_1': dict(skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=80,Crossbow=60,Throwing=50,Riding=160,Athletics=120,Crafting=170,Scouting=140,Tactics=180,Roguery=70,Charm=255,Leadership=200,Trade=220,Steward=265,Medicine=230,Engineering=160),
                          traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Lindariel
            'lord_1_46': dict(archetype='lord'),  # Malrior
            'lord_1_46_1': dict(skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=80,Crossbow=60,Throwing=50,Riding=140,Athletics=110,Crafting=140,Scouting=110,Tactics=140,Roguery=60,Charm=215,Leadership=160,Trade=185,Steward=235,Medicine=200,Engineering=140),
                          traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=1,Valor=0,Egalitarian=0,Oligarchic=1,Authoritarian=1)),  # Thorwen
            # Lossarnach
            'lord_1_45': dict(skills=dict(OneHanded=240,TwoHanded=295,Polearm=200,Bow=194,Crossbow=194,Throwing=194,Riding=194,Athletics=194,Crafting=194,Scouting=194,Tactics=194,Roguery=194,Charm=194,Leadership=285,Trade=194,Steward=258,Medicine=194,Engineering=194),
                          traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Forlong
            'lord_1_45_1': dict(skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=80,Crossbow=60,Throwing=50,Riding=160,Athletics=120,Crafting=200,Scouting=140,Tactics=180,Roguery=70,Charm=245,Leadership=190,Trade=230,Steward=275,Medicine=225,Engineering=170),
                          traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Berethiel
            'lord_1_45_2': dict(skills=dict(OneHanded=180,TwoHanded=150,Polearm=170,Bow=110,Crossbow=80,Throwing=110,Riding=200,Athletics=200,Crafting=60,Scouting=130,Tactics=130,Roguery=60,Charm=140,Leadership=120,Trade=90,Steward=120,Medicine=80,Engineering=90),
                          traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Caldamir/Brandir
            'lord_1_45_3': dict(archetype='elder_lord'),  # Rumil/Borlong
            'lord_1_57':   dict(archetype='knight', skills=dict(OneHanded=240,TwoHanded=180,Polearm=230,Bow=130,Crossbow=100,Throwing=120,Riding=210,Athletics=235,Crafting=70,Scouting=180,Tactics=200,Roguery=80,Charm=180,Leadership=210,Trade=110,Steward=170,Medicine=100,Engineering=120),
                          traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),  # Baranor
            'lord_1_57_1': dict(archetype='young_lady'),  # Calathiel
            # Pinnath Gelin
            'lord_1_52': dict(skills=dict(OneHanded=250,TwoHanded=180,Polearm=255,Bow=184,Crossbow=184,Throwing=184,Riding=275,Athletics=184,Crafting=184,Scouting=184,Tactics=184,Roguery=184,Charm=184,Leadership=248,Trade=184,Steward=184,Medicine=184,Engineering=184),
                          traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Hirluin
            'lord_1_52_1': dict(archetype='young_lady', skills=dict(OneHanded=140,TwoHanded=80,Polearm=120,Bow=170,Crossbow=110,Throwing=110,Riding=180,Athletics=190,Crafting=110,Scouting=160,Tactics=140,Roguery=70,Charm=200,Leadership=130,Trade=130,Steward=170,Medicine=140,Engineering=100),
                          traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Anariel/Arador
            'lord_1_52_2': dict(archetype='young_lord', skills=dict(OneHanded=180,TwoHanded=140,Polearm=180,Bow=200,Crossbow=110,Throwing=140,Riding=200,Athletics=210,Crafting=70,Scouting=200,Tactics=160,Roguery=70,Charm=170,Leadership=160,Trade=100,Steward=130,Medicine=100,Engineering=100),
                          traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Barandor/Arvedui
            'lord_1_52_4': dict(archetype='matriarch'),  # Nauriel
            'lord_1_62':   dict(archetype='knight'),  # Oromar
            # Lamedon
            'lord_1_53': dict(skills=dict(OneHanded=270,TwoHanded=172,Polearm=242,Bow=177,Crossbow=177,Throwing=177,Riding=255,Athletics=177,Crafting=177,Scouting=177,Tactics=177,Roguery=177,Charm=177,Leadership=260,Trade=177,Steward=177,Medicine=177,Engineering=177),
                          traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),  # Angbor
            'lord_1_73':   dict(archetype='knight', skills=dict(OneHanded=235,TwoHanded=190,Polearm=225,Bow=120,Crossbow=90,Throwing=130,Riding=220,Athletics=235,Crafting=70,Scouting=170,Tactics=180,Roguery=70,Charm=180,Leadership=180,Trade=110,Steward=150,Medicine=100,Engineering=110),
                          traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Narmir
            'lord_1_73_1': dict(archetype='lady'),  # Belwen/Popilia
            # Anfalas
            'lord_1_71':   dict(skills=dict(OneHanded=290,TwoHanded=200,Polearm=195,Bow=205,Crossbow=195,Throwing=195,Riding=195,Athletics=195,Crafting=195,Scouting=195,Tactics=195,Roguery=195,Charm=195,Leadership=275,Trade=195,Steward=195,Medicine=195,Engineering=195),
                          traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Golasgil
            'lord_1_71_1': dict(skills=dict(OneHanded=65,TwoHanded=35,Polearm=55,Bow=90,Crossbow=70,Throwing=55,Riding=160,Athletics=130,Crafting=180,Scouting=160,Tactics=200,Roguery=80,Charm=255,Leadership=210,Trade=240,Steward=265,Medicine=235,Engineering=170),
                          traits=dict(Honor=2,Generosity=2,Calculating=2,Mercy=2,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),  # Laswen
            # Morthond
            'lord_WE9_l':  dict(skills=dict(OneHanded=205,TwoHanded=172,Polearm=177,Bow=290,Crossbow=177,Throwing=177,Riding=177,Athletics=177,Crafting=177,Scouting=255,Tactics=177,Roguery=177,Charm=177,Leadership=177,Trade=177,Steward=177,Medicine=177,Engineering=177),
                          traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=0,Valor=2,Egalitarian=0,Oligarchic=1,Authoritarian=1)),  # Duinhir
            'lord_WE9_u':  dict(archetype='ranger', skills=dict(OneHanded=200,TwoHanded=150,Polearm=190,Bow=275,Crossbow=130,Throwing=160,Riding=170,Athletics=255,Crafting=100,Scouting=255,Tactics=215,Roguery=90,Charm=170,Leadership=210,Trade=120,Steward=170,Medicine=130,Engineering=120),
                          traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Duilin
            'lord_WE9_u2': dict(archetype='lady'),  # Rosfin
            # Anfalas family
            'lord_WE8_c':  dict(archetype='young_lord', skills=dict(OneHanded=180,TwoHanded=140,Polearm=170,Bow=200,Crossbow=110,Throwing=140,Riding=190,Athletics=215,Crafting=80,Scouting=220,Tactics=150,Roguery=80,Charm=170,Leadership=140,Trade=130,Steward=140,Medicine=100,Engineering=100),
                          traits=dict(Honor=2,Generosity=1,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Pelendur
            'lord_WE8_u':  dict(archetype='knight', skills=dict(OneHanded=215,TwoHanded=165,Polearm=205,Bow=215,Crossbow=135,Throwing=160,Riding=180,Athletics=235,Crafting=90,Scouting=245,Tactics=210,Roguery=100,Charm=180,Leadership=200,Trade=200,Steward=180,Medicine=120,Engineering=140),
                          traits=dict(Honor=2,Generosity=1,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),  # Barandil
            'lord_WE8_1':  dict(archetype='lady'),  # Dorwen (Anfalas)
        },
    },

    # ====================================================================
    # EREBOR — Dwarves of the Lonely Mountain
    # ====================================================================
    'erebor': {
        'culture_id': 'erebor',
        'lore_name': 'Erebor (Dwarves of the Lonely Mountain)',
        'race': 'dwarf',
        'keyword_archetypes': [
            (['king under the mountain', 'king of erebor', 'lord of erebor'], 'dwarf_king'),
            (['warrior', 'axe', 'iron hills', 'longbeard'], 'dwarf_warrior'),
            (['matron','matriarch','wife','lady','daughter'], 'dwarf_lady'),
            (['young','heir','apprentice','prince'], 'dwarf_young'),
            (['captain','lord','noble'], 'dwarf_lord'),
        ],
        'canonical': {
            'lord_E1_1': dict(archetype='dwarf_king'),  # Dáin II Ironfoot — King under the Mountain, dies at Battle of Dale
            'lord_E1_2': dict(archetype='dwarf_lord',   # Thorin III Stonehelm — Dáin's son, becomes King after
                skills=dict(OneHanded=250,TwoHanded=265,Polearm=225,Bow=120,Crossbow=170,Throwing=140,
                            Riding=130,Athletics=240,Crafting=255,Scouting=180,Tactics=235,Roguery=70,
                            Charm=220,Leadership=250,Trade=220,Steward=240,Medicine=160,Engineering=255)),
            'lord_E1_3': dict(archetype='dwarf_lady'),  # Dísa — royal kin lady
            'lord_E1_4': dict(archetype='dwarf_lord'),  # Náin — royal kin
            'lord_E1_5': dict(archetype='dwarf_lord'),  # Durin — royal kin (heir-name)
            'lord_E1_6': dict(archetype='dwarf_lady'),  # Fin — matriarch
        },
    },

    # ====================================================================
    # DALE / NORTHMEN of Esgaroth — Bardings (sturgia)
    # ====================================================================
    'dale': {
        'culture_id': 'sturgia',
        'lore_name': 'Dale / Bardings',
        'race': 'man',
        'keyword_archetypes': [
            (['king of dale', 'lord of dale'], 'lord'),
            (['bard ii', 'king bard'], 'lord'),
            (['archer','bowman','dragon-bowman','dragonbowman'], 'dale_bowman'),
            (['wife of gloin','wife of bofur','wife of oin','wife of dwalin','wife of'], 'lady'),
            (['matriarch','grandmother','elder lady'], 'matriarch'),
            (['daughter','noblewoman','court'], 'young_lady'),
            (['prince','heir','young noble','young nobleman'], 'young_lord'),
            (['veteran','grizzled','garrison'], 'dale_lord'),
            (['noble','lord','captain'], 'dale_lord'),
        ],
        'canonical': {
            # The TAOM Dale roster is largely TAOM-invented wives + nobles; few canonical heroes here.
        },
    },

    # ====================================================================
    # MIRKWOOD — Thranduil's Woodland Realm (Silvan Elves)
    # ====================================================================
    'mirkwood': {
        'culture_id': 'mirkwood',
        'lore_name': 'Mirkwood (Woodland Realm)',
        'race': 'elf',
        'keyword_archetypes': [
            (['elvenking','king of','woodland king'], 'elf_king'),
            (['queen','lady of the woodland'], 'elf_queen'),
            (['captain of the guard','captain of mirkwood','marchwarden'], 'elf_warrior'),
            (['archer','bow','huntress'], 'elf_archer'),
            (['prince','heir'], 'elf_warrior'),
            (['lady','wife','daughter'], 'elf_lady'),
            (['lord','noble'], 'elf_lord'),
        ],
        'canonical': {
            'lord_M1_1': dict(archetype='elf_king',    # Thranduil — split from shared elf_king set (1f7a7a9a)
                              skills=dict(OneHanded=300,TwoHanded=255,Polearm=300,Bow=300,Crossbow=260,Throwing=260,Riding=285,Athletics=300,Crafting=260,Scouting=260,Tactics=260,Roguery=260,Charm=260,Leadership=300,Trade=260,Steward=260,Medicine=260,Engineering=260)),
            'lord_M1_11': dict(archetype='elf_archer',  # Legolas — prince of Mirkwood, master archer
                skills=dict(OneHanded=285,TwoHanded=242,Polearm=238,Bow=330,Crossbow=238,Throwing=260,Riding=265,Athletics=305,Crafting=238,Scouting=295,Tactics=238,Roguery=238,Charm=238,Leadership=238,Trade=238,Steward=238,Medicine=238,Engineering=238)),
            'lord_M1_2': dict(archetype='elf_queen'),   # Lothuial — TAOM-invented queen
            'lord_M1_3': dict(archetype='elf_warrior'), # Feren — captain
            'lord_M1_4': dict(archetype='elf_lord'),    # Galion — butler/steward
        },
    },

    # ====================================================================
    # RIVENDELL / IMLADRIS — Elrond's house (Half-elven + Noldor)
    # ====================================================================
    'rivendell': {
        'culture_id': 'rivendell',
        'lore_name': 'Rivendell (Imladris)',
        'race': 'elf',
        'keyword_archetypes': [
            (['lord of imladris','lord of rivendell','master of rivendell'], 'elf_king'),
            (['queen','lady of imladris','daughter of elrond','arwen'], 'elf_queen'),
            (['captain of the noldor','captain in arms','glorfindel','balrog'], 'elf_warrior'),
            (['twin','son of elrond','warrior'], 'elf_warrior'),
            (['counsellor','chief counsellor','erestor'], 'elf_lord'),
            (['lady'], 'elf_lady'),
        ],
        'canonical': {
            'lord_R1_1': dict(archetype='elf_king',     # Elrond — master of Rivendell, ancient (~6500 yrs)
                skills=dict(OneHanded=290,TwoHanded=260,Polearm=260,Bow=260,Crossbow=260,Throwing=260,Riding=260,Athletics=290,Crafting=260,Scouting=260,Tactics=305,Roguery=260,Charm=300,Leadership=310,Trade=260,Steward=300,Medicine=325,Engineering=260)),
            'lord_R1_2': dict(archetype='elf_queen'),   # Celebrían — wife of Elrond, daughter of Galadriel
            'lord_R1_3': dict(archetype='elf_warrior', # Elladan — twin son, fought with Dúnedain rangers
                skills=dict(OneHanded=280,TwoHanded=240,Polearm=275,Bow=275,Crossbow=180,Throwing=200,
                            Riding=275,Athletics=290,Crafting=180,Scouting=275,Tactics=260,Roguery=130,
                            Charm=225,Leadership=235,Trade=160,Steward=200,Medicine=215,Engineering=170)),
            'lord_R1_4': dict(archetype='elf_warrior', # Elrohir — twin
                skills=dict(OneHanded=280,TwoHanded=240,Polearm=275,Bow=275,Crossbow=180,Throwing=200,
                            Riding=275,Athletics=290,Crafting=180,Scouting=275,Tactics=260,Roguery=130,
                            Charm=225,Leadership=235,Trade=160,Steward=200,Medicine=215,Engineering=170)),
            'lord_R1_5': dict(archetype='elf_lady',    # Arwen Undómiel — Evenstar
                skills=dict(OneHanded=180,TwoHanded=130,Polearm=170,Bow=230,Crossbow=160,Throwing=160,
                            Riding=245,Athletics=240,Crafting=240,Scouting=220,Tactics=215,Roguery=80,
                            Charm=290,Leadership=210,Trade=220,Steward=265,Medicine=280,Engineering=200)),
            'lord_R2_1': dict(archetype='elf_warrior', # Glorfindel — slayer of a Balrog, prince of the Noldor
                skills=dict(OneHanded=315,TwoHanded=295,Polearm=300,Bow=241,Crossbow=242,Throwing=242,Riding=285,Athletics=310,Crafting=242,Scouting=242,Tactics=242,Roguery=242,Charm=242,Leadership=242,Trade=242,Steward=242,Medicine=242,Engineering=242)),
        },
    },

    # ====================================================================
    # MORDOR — Sauron's realm: Nazgûl, Black Númenóreans, Orcs of Barad-dûr
    # ====================================================================
    'mordor': {
        'culture_id': 'mordor',
        'lore_name': 'Mordor',
        'race': 'orc',  # default
        'keyword_archetypes': [
            (['nazgul','nazgûl','ringwraith','wraith','witch-king','witch king'], 'nazgul'),
            (['black numenorean','black númenórean','black numénoréan','tower of barad'], 'black_numenorean'),
            (['sorceress','sorcerer','dark arts','spells'], 'bn_sorceress'),
            (['captain','chieftain','lord','commander'], 'orc_chieftain'),
            (['warg-rider','warg rider'], 'orc_warg'),
            (['berserker'], 'orc_berserker'),
            (['scout','raider','tracker','huntress'], 'orc_scout'),
            (['warrior','garrison','guard','armies','servant','dwells'], 'orc_warrior'),
        ],
        'canonical': {
            'lord_1_48_1': dict(archetype='nazgul'),         # Nazgûl, the Tainted
            'lord_1_48_2': dict(archetype='nazgul'),         # Nazgûl, Shadow of Northmen
            'lord_1_48_3': dict(archetype='nazgul'),         # Nazgûl, Shadow of Umbar
            'lord_1_27_1': dict(archetype='bn_sorceress'),   # Verina — Black Númenórean sorceress
            'lord_1_30_1': dict(archetype='orc_chieftain',   # Svala Redfang — under Gothmog
                skills=dict(OneHanded=255,TwoHanded=245,Polearm=225,Bow=160,Crossbow=100,Throwing=170,
                            Riding=180,Athletics=250,Crafting=130,Scouting=220,Tactics=235,Roguery=230,
                            Charm=170,Leadership=240,Trade=130,Steward=180,Medicine=80,Engineering=160)),
            'lord_SE9_l': dict(archetype='orc_chieftain',    # Grishnâkh
                skills=dict(OneHanded=265,TwoHanded=255,Polearm=235,Bow=170,Crossbow=110,Throwing=190,
                            Riding=180,Athletics=255,Crafting=130,Scouting=240,Tactics=240,Roguery=255,
                            Charm=170,Leadership=255,Trade=130,Steward=180,Medicine=80,Engineering=170)),
            'lord_SE9_s': dict(archetype='bn_sorceress'),    # Jonna — BN sorceress
            'lord_SE9_c1': dict(archetype='black_numenorean'),  # Pagarios
            'lord_SE9_c2': dict(archetype='bn_sorceress'),      # Diasca
        },
    },

    # ====================================================================
    # DOL GULDUR — Khamûl's fortress in southern Mirkwood
    # ====================================================================
    'dolguldur': {
        'culture_id': 'dolguldur',
        'lore_name': 'Dol Guldur',
        'race': 'orc',
        'keyword_archetypes': [
            (['shadow of the east','second chief','khamul','khamûl','black easterling'], 'nazgul'),
            (['captain','chieftain','lord','overseer'], 'orc_chieftain'),
            (['scout','tracker','hunter'], 'orc_scout'),
            (['warg'], 'orc_warg'),
            (['berserker'], 'orc_berserker'),
            (['warrior','garrison'], 'orc_warrior'),
        ],
        'canonical': {
            # TAOM lords.xml has no canonical Khamûl entry by name; D_* IDs are generic orc captains.
            # First D1 lord is typically the leader — treat as chieftain.
            'lord_D1_2': dict(archetype='orc_chieftain'),  # Thrangul — first listed, treat as chieftain
            'lord_D2_1': dict(archetype='orc_chieftain'),  # Narzugh — second compound leader
            'lord_D3_1': dict(archetype='orc_chieftain'),  # Lorgath
            'lord_D4_1': dict(archetype='orc_chieftain'),  # Urzara
            'lord_D5_1': dict(archetype='orc_chieftain'),  # Throrgash
            'lord_D6_1': dict(archetype='orc_chieftain'),  # Thrurg
        },
    },

    # ====================================================================
    # GUNDABAD — Pale uruks of Mount Gundabad (Bolg's heirs)
    # ====================================================================
    'gundabad': {
        'culture_id': 'gundabad',
        'lore_name': 'Mount Gundabad',
        'race': 'orc',
        'keyword_archetypes': [
            (['bolg','azog','king'], 'orc_chieftain'),
            (['captain','chieftain','warlord'], 'orc_chieftain'),
            (['berserker'], 'orc_berserker'),
            (['warg'], 'orc_warg'),
            (['scout','tracker'], 'orc_scout'),
        ],
        'canonical': {
            # Each G{N}_1 is the cohort leader
            'lord_G1_1': dict(archetype='orc_chieftain'),  # Azgar (Azog-evoking name)
            'lord_G2_1': dict(archetype='orc_chieftain'),  # Vorgoth
            'lord_G3_1': dict(archetype='orc_chieftain'),  # Kragloth
            'lord_G4_1': dict(archetype='orc_chieftain',
                skills=dict(OneHanded=285,TwoHanded=275,Polearm=250,Bow=180,Crossbow=120,Throwing=200,
                            Riding=200,Athletics=275,Crafting=140,Scouting=250,Tactics=265,Roguery=250,
                            Charm=190,Leadership=280,Trade=140,Steward=210,Medicine=90,Engineering=190)),  # Bolgath — Bolg-evoking
            'lord_G5_1': dict(archetype='orc_chieftain'),  # Vorzak
        },
    },

    # ====================================================================
    # ISENGARD — Saruman's Uruk-hai (stronger than orcs, sunlight-resistant)
    # ====================================================================
    'isengard': {
        'culture_id': 'isengard',
        'lore_name': 'Isengard (Saruman)',
        'race': 'uruk_hai',
        'keyword_archetypes': [
            (['commander','chieftain','captain'], 'orc_chieftain'),
            (['warg-rider','warg rider'], 'orc_warg'),
            (['berserker'], 'orc_berserker'),
            (['scout','tracker'], 'orc_scout'),
            (['siege','sapper','engineer'], 'orc_warrior'),
            (['battle','foreguard','war','marauder','slaver','huntmaster','enforcer','pack'], 'orc_warrior'),
        ],
        'canonical': {
            'lord_I1_1': dict(archetype='orc_chieftain',   # Uglûk — Saruman's captain, leader of Amon Hen raid
                skills=dict(OneHanded=275,TwoHanded=265,Polearm=240,Bow=170,Crossbow=110,Throwing=200,
                            Riding=180,Athletics=275,Crafting=150,Scouting=250,Tactics=255,Roguery=255,
                            Charm=200,Leadership=275,Trade=140,Steward=210,Medicine=90,Engineering=190)),
            'lord_I2_1': dict(archetype='orc_chieftain',   # Mauhûr — Uruk-Hai War leader (rescued Uglûk's band)
                skills=dict(OneHanded=265,TwoHanded=255,Polearm=235,Bow=170,Crossbow=110,Throwing=190,
                            Riding=170,Athletics=270,Crafting=140,Scouting=255,Tactics=240,Roguery=240,
                            Charm=160,Leadership=255,Trade=120,Steward=190,Medicine=80,Engineering=170)),
            'lord_I3_1': dict(archetype='orc_warrior',     # Lugdush — Uruk-hai with Uglûk
                skills=dict(OneHanded=245,TwoHanded=240,Polearm=225,Bow=160,Crossbow=110,Throwing=180,
                            Riding=160,Athletics=255,Crafting=130,Scouting=220,Tactics=200,Roguery=230,
                            Charm=140,Leadership=200,Trade=120,Steward=170,Medicine=80,Engineering=160)),
            'lord_I4_1': dict(archetype='orc_chieftain',   # Lurtz — Uruk-hai Commander (film canon, killed Boromir)
                skills=dict(OneHanded=270,TwoHanded=265,Polearm=240,Bow=180,Crossbow=110,Throwing=200,
                            Riding=170,Athletics=280,Crafting=140,Scouting=240,Tactics=235,Roguery=240,
                            Charm=170,Leadership=240,Trade=120,Steward=190,Medicine=90,Engineering=180)),
            'lord_I2_3': dict(archetype='orc_warg',        # Sharku — Warg-Rider Captain (killed at Helm's Deep)
                skills=dict(OneHanded=245,TwoHanded=210,Polearm=240,Bow=190,Crossbow=110,Throwing=190,
                            Riding=295,Athletics=250,Crafting=110,Scouting=270,Tactics=215,Roguery=235,
                            Charm=140,Leadership=210,Trade=110,Steward=140,Medicine=80,Engineering=130)),
        },
    },

    # ====================================================================
    # LOTHLÓRIEN — Galadriel + Celeborn's realm
    # ====================================================================
    'lothlorien': {
        'culture_id': 'lothlorien',
        'lore_name': 'Lothlórien',
        'race': 'elf',
        'keyword_archetypes': [
            (['lady of lothl','galadriel'], 'elf_queen'),
            (['lord of lothl','celeborn'], 'elf_king'),
            (['marchwarden','march-warden','warden'], 'elf_archer'),
            (['captain','warrior'], 'elf_warrior'),
            (['lady','wife'], 'elf_lady'),
        ],
        'canonical': {
            'lord_L1_1': dict(archetype='elf_queen',   # Galadriel — Ring-bearer, ancient Noldor; max-tier
                skills=dict(OneHanded=285,TwoHanded=260,Polearm=260,Bow=260,Crossbow=260,Throwing=260,Riding=260,Athletics=290,Crafting=260,Scouting=300,Tactics=305,Roguery=260,Charm=325,Leadership=325,Trade=260,Steward=315,Medicine=305,Engineering=260)),
            'lord_L1_2': dict(archetype='elf_king',    # Celeborn — Lord of Lothlórien, ancient Sindar
                skills=dict(OneHanded=320,TwoHanded=300,Polearm=310,Bow=245,Crossbow=250,Throwing=250,Riding=250,Athletics=315,Crafting=250,Scouting=250,Tactics=250,Roguery=250,Charm=250,Leadership=290,Trade=250,Steward=250,Medicine=250,Engineering=250)),
        },
    },

    # ====================================================================
    # DUNLAND — Hillmen of Dunland (empire), Saruman's auxiliaries
    # ====================================================================
    'dunland': {
        'culture_id': 'empire',
        'lore_name': 'Dunland',
        'race': 'man',
        'keyword_archetypes': [
            (['brenin','king of dunland','hereditary chief'], 'dunland_brenin'),
            (['raid','raider','strike without warning','silently'], 'dunland_raider'),
            (['scout','hawkeye','wingdart'], 'dunland_raider'),
            (['shield-maiden','shieldmaiden','warrior woman','warband','fierce','warrior'], 'dunland_warrior'),
            (['matriarch','elder'], 'matriarch'),
            (['heir'], 'young_lord'),
        ],
        'canonical': {},
    },

    # ====================================================================
    # HARAD — Haradrim Southrons (aserai)
    # ====================================================================
    'harad': {
        'culture_id': 'aserai',
        'lore_name': 'Harad (Haradrim)',
        'race': 'man',
        'keyword_archetypes': [
            (['mumak','mumakil','mûmakil','mumak lord'], 'mumak_rider'),
            (['serpent guard','golden banner chief','chieftain','prince'], 'haradrim_lord'),
            (['heir','young','training'], 'young_lord'),
            (['merchant','trade routes','trade connections'], 'haradrim_lord'),
            (['wife of','lady','daughter'], 'desert_lady'),
            (['frontier','garrison','warrior'], 'haradrim_cav'),
        ],
        'canonical': {},
    },

    # ====================================================================
    # KHAND — Variags (battania)
    # ====================================================================
    'khand': {
        'culture_id': 'battania',
        'lore_name': 'Khand (Variags)',
        'race': 'man',
        'keyword_archetypes': [
            (['high warlord','warlord'], 'variag_lord'),
            (['mountain guardian','passes','patrols'], 'variag_lord'),
            (['raid','raiding band'], 'dunland_raider'),
            (['wife of','daughter'], 'variag_lady'),
            (['heir'], 'young_lord'),
            (['captain','warband','host'], 'variag_lord'),
        ],
        'canonical': {},
    },

    # ====================================================================
    # EASTERLINGS / RHÛN (khuzait)
    # ====================================================================
    'easterling': {
        'culture_id': 'khuzait',
        'lore_name': 'Easterlings of Rhûn',
        'race': 'man',
        'keyword_archetypes': [
            (['chieftain of the','khan','chieftain'], 'easterling_lord'),
            (['wainrider','chariot','wagon'], 'easterling_lord'),
            (['archer','bowman','horse-bowman'], 'easterling_archer'),
            (['scout','tracker'], 'easterling_archer'),
            (['wife of','lady'], 'easterling_lady'),
            (['heir'], 'young_lord'),
            (['veteran','seasoned warrior','advises'], 'easterling_lord'),
        ],
        'canonical': {},
    },

    # ====================================================================
    # UMBAR — Corsairs (umbar)
    # ====================================================================
    'umbar': {
        'culture_id': 'umbar',
        'lore_name': 'Umbar Corsairs',
        'race': 'man',
        'keyword_archetypes': [
            (['ar-','king of umbar','admiral'], 'corsair_lord'),
            (['captain','pirate','corsair'], 'corsair_captain'),
            (['black numen','sorcerer'], 'black_numenorean'),
            (['lady','wife','queen'], 'lady'),
            (['heir','young','prince'], 'young_lord'),
        ],
        'canonical': {
            'lord_U1_1':  dict(archetype='corsair_lord'),     # Ar-Gimilkhâd — top noble (Adunaic Ar- prefix = king)
            'lord_U1_11': dict(archetype='young_lord'),       # Lord Gimilzâr — heir
            'lord_U1_12': dict(archetype='young_lord'),       # Gimilthân
            'lord_U1_13': dict(archetype='young_lord'),       # Pharaz�n
            'lord_U1_2':  dict(archetype='lady'),             # Zimraphel — wife
            'lord_U2_1':  dict(archetype='corsair_lord'),     # Azraphel
            'lord_U3_1':  dict(archetype='corsair_captain'),  # Belkazar
            'lord_U4_1':  dict(archetype='corsair_lord'),     # Pharakhân
            'lord_U5_1':  dict(archetype='corsair_captain'),  # Inkaldâr
            'lord_U6_1':  dict(archetype='corsair_lord'),     # Zimrathâr
        },
    },

    # ====================================================================
    # SHAGHANA — TAOM-invented southern desert culture
    # ====================================================================
    'shaghana': {
        'culture_id': 'shaghana',
        'lore_name': 'Shaghana',
        'race': 'man',
        'keyword_archetypes': [],
        'canonical': {},  # All use default elder_lord / lord by age
    },

    # ====================================================================
    # ABANISSA — TAOM-invented coastal/southern culture
    # ====================================================================
    'abanissa': {
        'culture_id': 'abanissa',
        'lore_name': 'Abanissa',
        'race': 'man',
        'keyword_archetypes': [],
        'canonical': {},
    },

    'rohan': {  # vlandia
        'culture_id': 'vlandia',
        'lore_name': 'Rohan',
        'race': 'man',
        # Bio-keyword → archetype hints (checked in order)
        'keyword_archetypes': [
            (['shieldmaiden'], 'shieldmaiden'),
            (['marshal of the mark', 'marshal of rohan', 'commands the riders', 'leads the riders'], 'lord'),
            (['horse breeder', 'warhorse', 'horse warden', 'horse master'], 'horse_breeder'),
            (['rider', 'horsemaster', 'cavalry', 'rohirric', 'rohirrim'], 'rider'),
            (['errand-rider', 'messenger'], 'errand_rider'),
            (['queen', 'princess', 'lady of'], 'lady'),
            (['captain'], 'knight'),
        ],
        'canonical': {
            # Théoden — King of Rohan, restored to vigor, dies Pelennor (~71)
            'lord_4_1': dict(
                skills=dict(OneHanded=270,TwoHanded=220,Polearm=255,Bow=140,Crossbow=50,Throwing=130,
                            Riding=290,Athletics=235,Crafting=90,Scouting=190,Tactics=275,Roguery=80,
                            Charm=240,Leadership=295,Trade=160,Steward=255,Medicine=140,Engineering=160),
                traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),
            # Éomer — Third Marshal, future King
            'lord_4_3_1': dict(
                skills=dict(OneHanded=285,TwoHanded=235,Polearm=285,Bow=160,Crossbow=50,Throwing=160,
                            Riding=295,Athletics=275,Crafting=80,Scouting=240,Tactics=265,Roguery=80,
                            Charm=220,Leadership=275,Trade=130,Steward=200,Medicine=130,Engineering=140),
                traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),
            # Théodred — Second Marshal, dies at Fords of Isen
            'lord_4_7': dict(
                skills=dict(OneHanded=275,TwoHanded=225,Polearm=275,Bow=140,Crossbow=50,Throwing=140,
                            Riding=290,Athletics=265,Crafting=80,Scouting=220,Tactics=245,Roguery=70,
                            Charm=210,Leadership=255,Trade=130,Steward=200,Medicine=130,Engineering=130),
                traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),
            # Éowyn — slew Witch-King, House of Healing later
            'lord_4_24_1': dict(
                skills=dict(OneHanded=250,TwoHanded=215,Polearm=230,Bow=160,Crossbow=60,Throwing=140,
                            Riding=260,Athletics=255,Crafting=100,Scouting=200,Tactics=220,Roguery=80,
                            Charm=215,Leadership=200,Trade=130,Steward=210,Medicine=200,Engineering=110),
                traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
            # Erkenbrand — Lord of Westfold, Hornburg commander
            'lord_4_16': dict(
                skills=dict(OneHanded=265,TwoHanded=215,Polearm=270,Bow=150,Crossbow=50,Throwing=140,
                            Riding=265,Athletics=245,Crafting=90,Scouting=205,Tactics=250,Roguery=70,
                            Charm=205,Leadership=265,Trade=150,Steward=225,Medicine=140,Engineering=170),
                traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=1,Authoritarian=1)),
            # Grimbold — Westfold marshal, hero of Fords of Isen, dies Pelennor
            'lord_4_6': dict(
                skills=dict(OneHanded=265,TwoHanded=215,Polearm=275,Bow=140,Crossbow=50,Throwing=140,
                            Riding=260,Athletics=255,Crafting=80,Scouting=215,Tactics=240,Roguery=70,
                            Charm=195,Leadership=235,Trade=120,Steward=185,Medicine=130,Engineering=130),
                traits=dict(Honor=2,Generosity=2,Calculating=0,Mercy=1,Valor=2,Egalitarian=1,Oligarchic=0,Authoritarian=0)),
            # Théodwyn — Théoden's sister, mother of Eomer/Eowyn (deceased in canon — but in roster)
            'lord_4_3': dict(
                skills=dict(OneHanded=70,TwoHanded=40,Polearm=60,Bow=90,Crossbow=40,Throwing=60,
                            Riding=200,Athletics=140,Crafting=140,Scouting=130,Tactics=160,Roguery=70,
                            Charm=235,Leadership=180,Trade=190,Steward=240,Medicine=205,Engineering=130),
                traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=1,Egalitarian=1,Oligarchic=1,Authoritarian=0)),
            # Elfhild — Théoden's queen, died in childbirth (in roster as past character)
            'lord_4_2': dict(
                skills=dict(OneHanded=60,TwoHanded=30,Polearm=50,Bow=80,Crossbow=40,Throwing=50,
                            Riding=180,Athletics=130,Crafting=160,Scouting=120,Tactics=150,Roguery=60,
                            Charm=250,Leadership=200,Trade=210,Steward=255,Medicine=215,Engineering=140),
                traits=dict(Honor=2,Generosity=2,Calculating=1,Mercy=2,Valor=0,Egalitarian=1,Oligarchic=1,Authoritarian=0)),
            # Merthu — Lady of the Hornburg, Erkenbrand's wife
            'lord_4_16_1': dict(archetype='lady'),
            # Amalgun — heir to Erkenbrand (young heir lord)
            'lord_4_18': dict(archetype='young_lord',
                skills=dict(OneHanded=200,TwoHanded=160,Polearm=210,Bow=130,Crossbow=40,Throwing=130,
                            Riding=235,Athletics=215,Crafting=70,Scouting=170,Tactics=160,Roguery=70,
                            Charm=170,Leadership=160,Trade=110,Steward=140,Medicine=100,Engineering=100)),
            # Deorwyn — heir to Grimbold
            'lord_4_6_1': dict(archetype='young_lord',
                skills=dict(OneHanded=210,TwoHanded=170,Polearm=225,Bow=140,Crossbow=40,Throwing=130,
                            Riding=240,Athletics=220,Crafting=70,Scouting=190,Tactics=180,Roguery=70,
                            Charm=170,Leadership=170,Trade=110,Steward=150,Medicine=100,Engineering=110)),
            # Varmund — commands garrison of Aldburg
            'lord_4_20': dict(archetype='lord',
                skills=dict(OneHanded=245,TwoHanded=190,Polearm=255,Bow=140,Crossbow=50,Throwing=130,
                            Riding=255,Athletics=235,Crafting=80,Scouting=200,Tactics=230,Roguery=70,
                            Charm=190,Leadership=230,Trade=140,Steward=205,Medicine=130,Engineering=150)),
            # Ingeltrud — Lady of Aldburg
            'lord_4_20_1': dict(archetype='lady'),
            # Marhath — leads horse breeders
            'lord_4_23': dict(archetype='horse_breeder'),
            'lord_4_23_2': dict(archetype='horse_breeder',
                skills=dict(OneHanded=180,TwoHanded=130,Polearm=190,Bow=140,Crossbow=40,Throwing=120,
                            Riding=290,Athletics=215,Crafting=200,Scouting=200,Tactics=170,Roguery=70,
                            Charm=180,Leadership=180,Trade=215,Steward=200,Medicine=160,Engineering=120)),
            'lord_4_23_3': dict(archetype='horse_breeder',
                skills=dict(OneHanded=170,TwoHanded=120,Polearm=180,Bow=130,Crossbow=40,Throwing=110,
                            Riding=275,Athletics=200,Crafting=180,Scouting=190,Tactics=140,Roguery=70,
                            Charm=160,Leadership=150,Trade=190,Steward=180,Medicine=140,Engineering=110)),
            # Lucand — commands Eastemnet riders
            'lord_4_25': dict(archetype='rider',
                skills=dict(OneHanded=255,TwoHanded=195,Polearm=270,Bow=150,Crossbow=40,Throwing=140,
                            Riding=275,Athletics=250,Crafting=70,Scouting=215,Tactics=235,Roguery=70,
                            Charm=200,Leadership=235,Trade=140,Steward=200,Medicine=130,Engineering=140)),
            # Peric — commands Gap of Rohan
            'lord_4_26': dict(archetype='rider',
                skills=dict(OneHanded=250,TwoHanded=190,Polearm=265,Bow=150,Crossbow=40,Throwing=140,
                            Riding=270,Athletics=245,Crafting=70,Scouting=235,Tactics=230,Roguery=80,
                            Charm=185,Leadership=220,Trade=130,Steward=185,Medicine=130,Engineering=130)),
            # Fasthelm Morcargas — southern watch commander
            'lord_4_28': dict(archetype='lord',
                skills=dict(OneHanded=235,TwoHanded=180,Polearm=245,Bow=160,Crossbow=50,Throwing=140,
                            Riding=255,Athletics=235,Crafting=80,Scouting=245,Tactics=225,Roguery=80,
                            Charm=180,Leadership=215,Trade=130,Steward=195,Medicine=130,Engineering=140)),
            'lord_4_28_2': dict(archetype='young_lady'),
            # Silvind — shieldmaiden of Westfold
            'lord_4_12': dict(archetype='shieldmaiden',
                skills=dict(OneHanded=220,TwoHanded=190,Polearm=205,Bow=140,Crossbow=50,Throwing=130,
                            Riding=235,Athletics=225,Crafting=90,Scouting=170,Tactics=190,Roguery=80,
                            Charm=190,Leadership=170,Trade=120,Steward=180,Medicine=160,Engineering=100)),
            # Lasand — Westfold garrison
            'lord_4_121': dict(archetype='knight',
                skills=dict(OneHanded=220,TwoHanded=170,Polearm=235,Bow=140,Crossbow=50,Throwing=120,
                            Riding=240,Athletics=225,Crafting=70,Scouting=170,Tactics=190,Roguery=70,
                            Charm=160,Leadership=170,Trade=110,Steward=150,Medicine=100,Engineering=100)),
            # Elbet — Erkenbrand's household
            'lord_4_17': dict(archetype='knight'),
            # Unthery — Westfold captain
            'lord_4_5': dict(archetype='knight',
                skills=dict(OneHanded=235,TwoHanded=180,Polearm=245,Bow=140,Crossbow=50,Throwing=130,
                            Riding=250,Athletics=230,Crafting=70,Scouting=190,Tactics=200,Roguery=70,
                            Charm=170,Leadership=180,Trade=110,Steward=160,Medicine=110,Engineering=110)),
            # Furnhard — Eomer's trusted rider
            'lord_4_8': dict(archetype='rider'),
            # Thomund — Westfold company commander
            'lord_4_9': dict(archetype='rider'),
            # Siegeberht — border patrol
            'lord_4_24_4': dict(archetype='rider'),
            # Hereswith — southern watch daughter
            # Already young_lady via auto
        },
    },
}

# Default archetype if no canonical override and no keyword match
def default_archetype(age: int, female: bool, culture_data: dict = None) -> str:
    race = (culture_data or {}).get('race', 'man')
    if race == 'dwarf':
        if female: return 'dwarf_lady'
        if age <= 22: return 'dwarf_young'
        return 'dwarf_warrior'
    if race == 'elf':
        # Elves don't really age in TAOM age numbers (placeholder ages); treat all as mature
        if female: return 'elf_lady'
        return 'elf_warrior'
    if race == 'orc':
        if female: return 'orc_female'
        return 'orc_warrior'
    if race == 'uruk_hai':
        if female: return 'orc_female'
        return 'orc_warrior'  # uruk_hai variant slightly stronger but use orc_warrior as base
    if race == 'nazgul':
        return 'nazgul'
    # Men (default)
    if female:
        if age >= 60: return 'matriarch'
        if age <= 25: return 'young_lady'
        return 'lady'
    if age >= 60: return 'elder_lord'
    if age <= 25: return 'young_lord'
    return 'knight'  # safer default than 'lord'; canonical lords are overridden


def archetype_from_bio(bio: str, age: int, female: bool, culture_data: dict) -> str:
    bl = bio.lower() if bio else ''
    race = culture_data.get('race', 'man')
    for keywords, arch in culture_data.get('keyword_archetypes', []):
        if any(k in bl for k in keywords):
            # Race-specific gender adjustment
            if female and arch in ('rider','lord','knight','shieldmaiden','dwarf_warrior','dwarf_lord','elf_warrior','elf_archer','elf_lord') and arch != 'shieldmaiden':
                if race == 'dwarf': return 'dwarf_lady'
                if race == 'elf': return 'elf_lady'
                return 'lady'
            return arch
    return default_archetype(age, female, culture_data)


def get_skills_traits(npc_id: str, age: int, female: bool, bio: str, culture_data: dict) -> tuple[dict, dict]:
    canonical = culture_data.get('canonical', {})
    if npc_id in canonical:
        c = canonical[npc_id]
        if 'skills' in c and 'traits' in c:
            return c['skills'], c['traits']
        arch_name = c.get('archetype', archetype_from_bio(bio, age, female, culture_data))
        arch = BASE_ARCHETYPES[arch_name]
        sk = dict(arch['skills']); sk.update(c.get('skills', {}))
        tr = dict(arch['traits']); tr.update(c.get('traits', {}))
        return sk, tr
    arch = BASE_ARCHETYPES[archetype_from_bio(bio, age, female, culture_data)]
    return dict(arch['skills']), dict(arch['traits'])


def get_skill_template_name(npc_id: str, age: int, female: bool, bio: str, culture_data: dict) -> str:
    """Return the TAOM SkillSet id this NPC should reference via skill_template.

    - Canonical NPC with explicit `skills=` override: taom_canonical_<id>_skills
    - Otherwise: taom_<archetype>_skills
    """
    canonical = culture_data.get('canonical', {})
    if npc_id in canonical and 'skills' in canonical[npc_id]:
        return f'taom_canonical_{npc_id}_skills'
    if npc_id in canonical and 'archetype' in canonical[npc_id]:
        return f"taom_{canonical[npc_id]['archetype']}_skills"
    return f'taom_{archetype_from_bio(bio, age, female, culture_data)}_skills'


def build_skill_sets_xml() -> str:
    """Generate the full taom_lord_skill_sets.xml content.

    Includes:
    - One <SkillSet> per archetype in BASE_ARCHETYPES (taom_<arch>_skills)
    - One <SkillSet> per canonical NPC with explicit `skills=` (taom_canonical_<id>_skills)

    Re-running is deterministic — output is sorted by id.
    """
    sets = {}
    # Archetype sets
    for arch_name, arch_data in BASE_ARCHETYPES.items():
        sets[f'taom_{arch_name}_skills'] = arch_data['skills']
    # Canonical sets (across all cultures)
    for cul_name, cdata in CULTURES.items():
        for npc_id, c in cdata.get('canonical', {}).items():
            if 'skills' in c:
                # Resolve full skill dict (archetype base + canonical override)
                if 'archetype' in c:
                    base = dict(BASE_ARCHETYPES[c['archetype']]['skills'])
                    base.update(c['skills'])
                    sets[f'taom_canonical_{npc_id}_skills'] = base
                else:
                    # Full explicit skills dict (no archetype base)
                    sets[f'taom_canonical_{npc_id}_skills'] = dict(c['skills'])

    out = ['<?xml version="1.0" encoding="utf-8"?>',
           '<SkillSets>',
           '  <!-- Generated by tools/apply_culture_skills_traits.py. Do not edit by hand. -->',
           '  <!-- Lord skill templates — referenced from NPCCharacter[@skill_template]. -->',
           '']
    for sid in sorted(sets):
        out.append(f'  <SkillSet id="{sid}">')
        for skill_name in SKILL_ORDER:
            if skill_name in sets[sid]:
                out.append(f'    <skill id="{skill_name}" value="{sets[sid][skill_name]}" />')
        out.append('  </SkillSet>')
    out.append('</SkillSets>')
    out.append('')
    return '\n'.join(out)


def update_skill_template_in_block(block: str, mode: str, new_template: str) -> str:
    """Replace the skill_template attribute value in an NPCCharacter block."""
    if mode == 'xml':
        # Inline attribute on <NPCCharacter ... skill_template="SkillSet.X" ...>
        return re.sub(r'(skill_template=")SkillSet\.[A-Za-z0-9_]+(")', r'\1SkillSet.' + new_template + r'\2', block, count=1)
    elif mode == 'xslt':
        # <xsl:attribute name="skill_template">SkillSet.X</xsl:attribute>
        return re.sub(
            r'(<xsl:attribute name="skill_template">)SkillSet\.[A-Za-z0-9_]+(</xsl:attribute>)',
            r'\1SkillSet.' + new_template + r'\2', block, count=1)
    return block


# ============================================================================
# RENDERING + REPLACEMENT (same as Gondor script)
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


def replace_skills_traits_in_block(block: str, skills: dict, traits: dict) -> str:
    for pat, render in [
        (re.compile(r'(\s*)<skills\s*/>', re.DOTALL), render_skills_block),
        (re.compile(r'(\s*)<skills>.*?</skills>', re.DOTALL), render_skills_block),
    ]:
        m = pat.search(block)
        if m:
            leading = m.group(1)
            new = ('\n' if leading.startswith('\n') else '') + render(skills, leading.lstrip('\n'))
            block = block[:m.start()] + new + block[m.end():]
            break
    for pat, render in [
        (re.compile(r'(\s*)<Traits\s*/>', re.DOTALL), render_traits_block),
        (re.compile(r'(\s*)<Traits>.*?</Traits>', re.DOTALL), render_traits_block),
    ]:
        m = pat.search(block)
        if m:
            leading = m.group(1)
            new = ('\n' if leading.startswith('\n') else '') + render(traits, leading.lstrip('\n'))
            block = block[:m.start()] + new + block[m.end():]
            break
    return block


def find_npc_block(text: str, npc_id: str, mode: str) -> tuple[int, int, dict]:
    if mode == 'xml':
        m = re.search(r'<NPCCharacter id="' + re.escape(npc_id) + r'"([^>]*)>', text)
        if not m: return None
        close = text.find('</NPCCharacter>', m.end())
        if close == -1: return None
        attrs = m.group(1)
        fem = bool(re.search(r'is_female="(?:[Tt]rue|TRUE)"', attrs))
        age = re.search(r'\sage="(\d+)"', attrs)
        return (m.start(), close + len('</NPCCharacter>'),
                dict(female=fem, age=int(age.group(1)) if age else 0))
    elif mode == 'xslt':
        op = re.search(r'<xsl:template match="NPCCharacter\[@id=\'' + re.escape(npc_id) + r'\'\]', text)
        if not op: return None
        close = text.find('</xsl:template>', op.end())
        if close == -1: return None
        end = close + len('</xsl:template>')
        body = text[op.start():end]
        fem = bool(re.search(r'name="is_female">([Tt]rue|TRUE)', body))
        age = re.search(r'name="age">(\d+)', body)
        return (op.start(), end, dict(female=fem, age=int(age.group(1)) if age else 0))


def npcs_of_culture(text: str, culture_id: str, mode: str) -> list[str]:
    if mode == 'xml':
        return re.findall(r'<NPCCharacter id="([^"]+)"[^>]*culture="Culture\.' + re.escape(culture_id) + r'"', text)
    elif mode == 'xslt':
        out = []
        for m in re.finditer(r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]", text):
            end = text.find('</xsl:template>', m.end())
            body = text[m.end():end]
            if f'name="culture">Culture.{culture_id}' in body:
                out.append(m.group(1))
        return out


def load_bios() -> dict:
    hx = HEROES_XSLT.read_text(encoding='utf-8')
    bios = {}
    for m in re.finditer(r"<xsl:template match=\"Hero\[@id='([^']+)'\]", hx):
        end = hx.find('</xsl:template>', m.end())
        body = hx[m.end():end]
        tm = re.search(r'\{=[A-Za-z_0-9]+\}([^<]+)', body)
        if tm: bios[m.group(1)] = tm.group(1).strip()
    return bios


def process_file(text: str, culture_data: dict, mode: str, bios: dict, label: str) -> tuple[str, int, int]:
    ids = npcs_of_culture(text, culture_data['culture_id'], mode)
    canonical = culture_data.get('canonical', {})
    touched = skipped = 0
    print(f"  {label}: {len(ids)} NPCs of culture={culture_data['culture_id']}")
    for nid in ids:
        info = find_npc_block(text, nid, mode)
        if not info: continue
        start, end, attrs = info
        # Skip children UNLESS this NPC has a canonical override (e.g., Nazgûl with placeholder age).
        if attrs['age'] < 14 and nid not in canonical:
            print(f"    skip child {nid} (age {attrs['age']})")
            skipped += 1
            continue
        sk, tr = get_skills_traits(nid, attrs['age'], attrs['female'], bios.get(nid, ''), culture_data)
        template = get_skill_template_name(nid, attrs['age'], attrs['female'], bios.get(nid, ''), culture_data)
        block = text[start:end]
        new_block = replace_skills_traits_in_block(block, sk, tr)
        new_block = update_skill_template_in_block(new_block, mode, template)
        if new_block != block:
            text = text[:start] + new_block + text[end:]
            touched += 1
    return text, touched, skipped


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--culture', help='Culture key (e.g. rohan); omit with --all-cultures')
    ap.add_argument('--all-cultures', action='store_true', help='Process every culture in one run')
    ap.add_argument('--skillsets-only', action='store_true',
                    help='Only regenerate taom_lord_skill_sets.xml; skip lords.xml/xslt edits')
    ap.add_argument('--apply', action='store_true')
    args = ap.parse_args()

    bios = load_bios()

    # Always regenerate the SkillSets file (deterministic, fast).
    sets_xml = build_skill_sets_xml()
    n_sets = sets_xml.count('<SkillSet id=')
    print(f"Generated taom_lord_skill_sets.xml ({n_sets} SkillSets)")
    if args.apply:
        LORD_SKILL_SETS.write_text(sets_xml, encoding='utf-8')
        print(f"  WROTE {LORD_SKILL_SETS.relative_to(REPO)}")

    if args.skillsets_only:
        return 0

    if args.all_cultures:
        targets = list(CULTURES.keys())
    elif args.culture:
        if args.culture not in CULTURES:
            print(f"Unknown culture {args.culture!r}. Available: {sorted(CULTURES)}")
            return 1
        targets = [args.culture]
    else:
        print("Specify --culture <name> or --all-cultures (or use --skillsets-only)")
        return 1

    xml = LORDS_XML.read_text(encoding='utf-8')
    xslt = LORDS_XSLT.read_text(encoding='utf-8')
    total_xml = total_xslt = 0
    for cul in targets:
        cdata = CULTURES[cul]
        print(f"\nCulture: {cul} ({cdata['lore_name']}) — culture_id={cdata['culture_id']}")
        xml, x_touched, _ = process_file(xml, cdata, 'xml', bios, 'lords.xml')
        xslt, t_touched, _ = process_file(xslt, cdata, 'xslt', bios, 'lords.xslt')
        total_xml += x_touched
        total_xslt += t_touched

    print(f"\nTOTAL touched: lords.xml={total_xml}, lords.xslt={total_xslt}")

    if args.apply:
        LORDS_XML.write_text(xml, encoding='utf-8')
        LORDS_XSLT.write_text(xslt, encoding='utf-8')
        print("WROTE lords.xml + lords.xslt")
    else:
        print("(dry-run — pass --apply to write)")
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
