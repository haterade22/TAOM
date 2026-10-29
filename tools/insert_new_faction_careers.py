#!/usr/bin/env python3
"""Clone Gundabad's three orc careers for the new orc cultures (goblin, mistymountainorcs).

Both cultures shipped CC-selectable in June 2026 with zero eligible careers, so the career stage
offered only the "No specialization" fallback. The crash that a zero-option culture would otherwise
cause is already guarded (TrySwitchToNextMenu_Patch), which is exactly why this stayed invisible.

Cloning rather than authoring from scratch is the house pattern for these two cultures: their troop
trees are Gundabad clones (docs/features/new-factions-misty-mountains-lindon.md) and their CC
narrative menus were cloned from Gundabad by tools/insert_new_faction_cc_menus.py. This script is
the third instance of the same move, and it deliberately mirrors that script's conventions.

What is cloned, per career, into four files:
  taom_careers.xml            1 <Career>          (LF line endings — unlike its three siblings)
  taom_ability_templates.xml  1 <AbilityTemplate>
  taom_career_choices.xml     1 root <Choice> + 6 <ChoiceGroup> x 5 <Choice> = 31 choices
  career_menu.json            1 entry

Art and FX ids (portrait_sprite, icon_sprite, sprite, particle_effect, sound_effect) are REUSED from
the source career, never renamed. A renamed sprite id would point at an asset nobody has authored;
reusing the source's keeps the clone exactly as well-resourced as its original. Only 21 of the 50
shipped careers have a portrait registered in TAOMSpriteData.xml, so inventing six more ids would
have widened an existing gap rather than closed it.

Textual insert (not an XML re-serialize) so every existing element stays byte-for-byte identical and
each file's own line-ending convention survives. Idempotent: a culture already present is skipped.

Run:  python tools/insert_new_faction_careers.py            # dry run
      python tools/insert_new_faction_careers.py --apply
      python tools/insert_new_faction_careers.py --check    # exit 1 when --apply would write or refuse
"""
import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAREER_DIR = ROOT / "Main" / "_Module" / "ModuleData" / "career_system"
CAREERS_XML = CAREER_DIR / "taom_careers.xml"
ABILITIES_XML = CAREER_DIR / "taom_ability_templates.xml"
CHOICES_XML = CAREER_DIR / "taom_career_choices.xml"
MENU_JSON = ROOT / "Main" / "_Module" / "ModuleData" / "charactercreation" / "career_menu.json"

# (source career id, source loc abbreviation) -> per-target (new id, new abbreviation, display name)
#
# The abbreviation is a second, independent id-space: choice descriptions key off `{=taom_ctm_t1_a_key}`
# and `career_choice_ctm_t1_a_key`, not off the career id. Remapping only the career id would leave
# every cloned choice sharing Gundabad's localization keys, so the goblin player would read Gundabad's
# wording no matter what the Career element said.
SOURCES = [
    ("cave_troll_master", "ctm"),
    ("goblin_sniper", "gbs"),
    ("warg_pack_leader", "wpl"),
]

# Blue Craig and Lindon were promoted out of Goblin-town and Rivendell, so they clone the careers of
# the culture they were carved from rather than Gundabad's. Lindon's source set is Rivendell's three
# elven careers, which is a different SOURCES list entirely — hence the per-culture override below.
SOURCES_FOR = {
    "bluecraig": [("goblin_troll_driver", "gtd"), ("goblin_tunnel_stalker", "gts"),
                  ("goblin_warg_master", "gwm")],
    "lindon": [("blade_dancer", "bd"), ("elven_archer", "elar"), ("rivendell_knight", "rvk")],
    # Shaghâna (Near Harad) and Âbanissa (Far Harad) are independent Harad-region kingdoms that
    # never had careers at all — they sat in CareerCultureCoverageTests' documentedExceptions from
    # Review #24 until 2026-08-11. Aserai is the nearest peer on the evidence: Shaghâna's own NPC
    # file says it "uses harad face templates and aserai civilian equipment", both cultures carry
    # a *DesertSpeedFeat, and VolunteerRecruitmentService then routed shaghana volunteers to the
    # Harad levy. Roles stay faithful to the source trio — infantry, javelineer, beast-rider.
    "shaghana": [("tribesman_of_jelut", "toj"), ("pezarsani_javelineer", "pj"),
                 ("mahud_beast_rider", "mbr")],
    "abanissa": [("tribesman_of_jelut", "toj"), ("pezarsani_javelineer", "pj"),
                 ("mahud_beast_rider", "mbr")],
    # Arthedain (2026-10-07) takes Gondor's three: the other Dunedain realm, and the roles map
    # straight across (ranger scout, infantry captain, heavy horse).
    "arthedain": [("ranger_of_ithilien", "roi"), ("captain_of_osgiliath", "cop"),
                  ("knight_of_belfalas", "kob")],
    # Cardolan and Rhudaur (#978) take Gondor's three the same way.
    "cardolan": [("ranger_of_ithilien", "roi"), ("captain_of_osgiliath", "cop"),
                 ("knight_of_belfalas", "kob")],
    "rhudaur": [("ranger_of_ithilien", "roi"), ("captain_of_osgiliath", "cop"),
                ("knight_of_belfalas", "kob")],
    # Ered Luin takes Erebor's foot careers. Dwarves never ride, so Erebor's Ram Rider is not
    # cloned (its passives are mount charge and mount health); Ironguard is cloned twice instead,
    # once per infantry career, through the list in TARGETS below.
    "eredluin": [("ironguard", "ig"), ("crossbow_master", "cbm")],
    # Anduin Vale and Angaladh are cut from Dale (culture id `sturgia`), so they take Dale's
    # careers. Anduin Vale: an axe infantry career, a bowman and a Vale Rider. Angaladh is a realm
    # of foot soldiers with no riders (Dale's outrider is not cloned): the infantry career is cloned
    # twice, once as the spear career and once as the two-handed axe career, as Ered Luin cloned
    # Ironguard, and Dale's marksman once.
    "anduinvale": [("dale_guardsman", "dg2"), ("dale_marksman", "dm"), ("dale_outrider", "dor")],
    "angaladh": [("dale_guardsman", "dg2"), ("dale_marksman", "dm")],
}

TARGETS = {
    "goblin": {
        "cave_troll_master": ("goblin_troll_driver", "gtd", "Goblin-town Troll-Driver"),
        "goblin_sniper": ("goblin_tunnel_stalker", "gts", "Goblin-town Tunnel Stalker"),
        "warg_pack_leader": ("goblin_warg_master", "gwm", "Goblin-town Warg Master"),
    },
    "mistymountainorcs": {
        "cave_troll_master": ("misty_troll_goad", "mtg", "Moria Troll-Goad"),
        "goblin_sniper": ("misty_deep_marksman", "mdm", "Moria Deep-Marksman"),
        "warg_pack_leader": ("misty_warg_chieftain", "mwc", "Misty Mountain Warg Chieftain"),
    },
    "bluecraig": {
        "goblin_troll_driver": ("craig_pit_driver", "cpd", "Blue Craig Pit-Driver"),
        "goblin_tunnel_stalker": ("craig_crag_stalker", "ccs", "Blue Craig Crag Stalker"),
        "goblin_warg_master": ("craig_warg_master", "cwm", "Blue Craig Warg Master"),
    },
    "lindon": {
        "blade_dancer": ("falathrim_blade_dancer", "fbd", "Falathrim Blade-Dancer"),
        "elven_archer": ("falathrim_sentinel", "fsn", "Falathrim Sentinel"),
        "rivendell_knight": ("falathrim_mariner", "fmr", "Falathrim Mariner"),
    },
    # Named for each kingdom's own towns in the live TAOM_Map settlements.xml — Zajâna, Chatâk and
    # Sormedân are Shaghâna's; Damudûr and Jîret are Âbanissa's. Âbanissa's notables are ivory,
    # gold, gem and spice traders with caravan lords and house stewards, so its wording is
    # mercantile-dynastic where Shaghâna's stays tribal.
    "shaghana": {
        "tribesman_of_jelut": ("shaghana_zajana_tribesman", "szt", "Tribesman of Zajâna"),
        "pezarsani_javelineer": ("shaghana_chatak_javelineer", "scj", "Chatâk Javelineer"),
        "mahud_beast_rider": ("shaghana_beast_rider", "sbr", "Sormedân Beast Rider"),
    },
    "abanissa": {
        "tribesman_of_jelut": ("abanissa_house_guard", "ahg", "House-Guard of Damudûr"),
        "pezarsani_javelineer": ("abanissa_jiret_javelineer", "ajj", "Jîret Javelineer"),
        "mahud_beast_rider": ("abanissa_ivory_rider", "air", "Ivory-Road Beast Rider"),
    },
    "arthedain": {
        "ranger_of_ithilien": ("ranger_of_the_north", "rotn", "Ranger of the North"),
        "captain_of_osgiliath": ("warden_of_fornost", "wof", "Warden of Fornost"),
        "knight_of_belfalas": ("knight_of_arthedain", "koa", "Knight of Arthedain"),
    },
    "cardolan": {
        "ranger_of_ithilien": ("bowman_of_minhiriath", "bom", "Bowman of Minhiriath"),
        "captain_of_osgiliath": ("warden_of_tyrn_gorthad", "wotg", "Warden of Tyrn Gorthad"),
        "knight_of_belfalas": ("knight_of_cardolan", "koc", "Knight of Cardolan"),
    },
    "rhudaur": {
        "ranger_of_ithilien": ("ranger_of_the_angle", "rota", "Ranger of the Angle"),
        "captain_of_osgiliath": ("captain_of_cameth_brin", "cocb", "Captain of Cameth Brin"),
        "knight_of_belfalas": ("rider_of_the_fells", "rotf", "Rider of the Fells"),
    },
    # A LIST clones that source once per entry. The ids avoid the troop ids
    # eredluin_firebeard_arbalester and eredluin_hewer, which the display names echo.
    "eredluin": {
        "ironguard": [("warden_of_thorins_halls", "woth", "Warden of Thorin's Halls"),
                      ("hewer_of_belegost", "hob", "Mattock Hewer of Belegost")],
        "crossbow_master": ("nogrod_arbalester", "nga", "Firebeard Arbalester"),
    },
    # The ids avoid the troop ids anduinvale_vale_rider and angaladh_ironwood_hewer, which the
    # display names echo.
    "anduinvale": {
        "dale_guardsman": ("axe_warden_of_beorns_hall", "awbh", "Axe-warden of Beorn's Hall"),
        "dale_marksman": ("archer_of_eastbank", "aoe", "Archer of Eastbank"),
        "dale_outrider": ("rider_of_the_vales", "rotv", "Rider of the Vales"),
    },
    "angaladh": {
        "dale_guardsman": [("warden_of_the_forest_gate", "wfg", "Warden of the Forest Gate"),
                           ("hewer_of_ironwood_hall", "hiwh", "Hewer of Ironwood Hall")],
        "dale_marksman": ("woodward_of_the_forest_road", "wfr", "Woodward of the Forest Road"),
    },
}


def targets_for(culture):
    """Yield (src_id, src_abbr, new_id, new_abbr, display) for every career a culture clones, in
    SOURCES_FOR order. A TARGETS value that is a list clones its source once per entry."""
    for src_id, src_abbr in SOURCES_FOR.get(culture, SOURCES):
        target = TARGETS[culture][src_id]
        for new_id, new_abbr, display in (target if isinstance(target, list) else [target]):
            yield src_id, src_abbr, new_id, new_abbr, display

# Extra source-culture rows to remove from a cloned <Career>'s eligibility. Gondor's careers list
# both its culture (gondor) and its kingdom id (empire_w); swapping only the first would leave an
# Arthedain career selectable by Gondor's players too.
ELIGIBILITY_DROP = {"arthedain": ["empire_w"], "cardolan": ["empire_w"], "rhudaur": ["empire_w"]}

# The source culture whose <Culture id="..."> row is swapped for the target's. Gundabad for the two
# orc kingdoms; the promoted cultures take theirs from the culture they were carved out of.
ELIGIBILITY_SRC = {
    "goblin": "gundabad", "mistymountainorcs": "gundabad",
    "bluecraig": "goblin", "lindon": "rivendell",
    "shaghana": "aserai", "abanissa": "aserai",
    "arthedain": "gondor",
    "cardolan": "gondor", "rhudaur": "gondor",
    "eredluin": "erebor",
    "anduinvale": "sturgia", "angaladh": "sturgia",
}

# Player-facing wording. Longest phrase FIRST so a substring never double-substitutes — the same
# ordering discipline insert_new_faction_cc_menus.py uses. The bare "Gundabad" catch-all runs last.
#
# The clone-contamination assertion at the end of this script is what actually enforces this table
# being complete; it fails the run if any source display word survives into a generated field.
TEXT_REMAP = {
    "goblin": [
        ("Gundabad Fell Warg Pack Leader", "Goblin-town Warg Master"),
        ("Gundabad Orc Hunter", "Goblin-town Tunnel Stalker"),
        ("Gundabad Berserker", "Goblin-town Frenzy"),
        ("Gundabad", "Goblin-town"),
    ],
    "mistymountainorcs": [
        ("Gundabad Fell Warg Pack Leader", "Misty Mountain Warg Chieftain"),
        ("Gundabad Orc Hunter", "Moria Deep-Marksman"),
        ("Gundabad Berserker", "Moria Frenzy"),
        ("Gundabad", "Moria"),
    ],
    # Blue Craig clones Goblin-town's careers, so the words being replaced are Goblin-town's own —
    # which are themselves this script's earlier output. The chain is deliberate: Gundabad ->
    # Goblin-town -> Blue Craig, each step renaming only what the previous step wrote.
    "bluecraig": [
        ("Goblin-town Tunnel Stalker", "Blue Craig Crag Stalker"),
        ("Goblin-town Warg Master", "Blue Craig Warg Master"),
        ("Goblin-town Troll-Driver", "Blue Craig Pit-Driver"),
        ("Goblin-town Frenzy", "Blue Craig Frenzy"),
        ("Goblin-town", "Blue Craig"),
    ],
    # Lindon clones Rivendell's three elven careers. Rivendell's are named for the Ñoldor; Círdan's
    # people are Falathrim Sindar, so the people-word changes even where the discipline does not.
    "lindon": [
        ("Ñoldor Blademaster", "Falathrim Blade-Dancer"),
        ("Ñoldor Sentinel", "Falathrim Sentinel"),
        ("Ñoldor Knight", "Falathrim Mariner"),
        ("Ñoldor", "Falathrim"),
        ("Rivendell", "Lindon"),
        ("Imladris", "Mithlond"),
    ],
    # Both Harad kingdoms clone Aserai, so only the words that name ASERAI's own places and peoples
    # change — Jelut, Pezarsan and Mahûd. The inherited group names that say "Haradwaith",
    # "Far Harad", "Southrons", "Scarlet" or "Mûmakan" stay: unlike the Ñoldor -> Falathrim case
    # those words are already correct for a Harad culture, and Tolkien gives the Haradrim chieftain
    # a black serpent upon scarlet, so Shaghâna keeps the serpent and the scarlet too.
    "shaghana": [
        ("Jelut Tribesman", "Zajâna Tribesman"),
        ("Pezarsan Javelineer", "Chatâk Javelineer"),
        ("Mahûd Beast-Tamer", "Sormedân Beast-Tamer"),
        ("Echoes of Mûmakan", "Horns of Mûmakan"),
        ("Poisoned Blades", "Serpent-Venom Blades"),
        ("Sunfire Volley", "Chatâk Volley"),
        ("Pezarsani", "Chatâk"),
        ("Pezarsan", "Chatâk"),
        ("Mahûd", "Sormedân"),
        ("Jelut", "Zajâna"),
    ],
    # Âbanissa's own notables are ivory, gold and gem traders, spice merchants, caravan lords and
    # house stewards, so its wording is mercantile-dynastic where Shaghâna's stays tribal.
    "abanissa": [
        ("Serpent-Banner Chieftain", "Ivory-Throne Chieftain"),
        ("Scarlet Spear-Master", "Gilded Spear-Master"),
        ("Jelut Tribesman", "Damudûr House-Guard"),
        ("Pezarsan Javelineer", "Jîret Javelineer"),
        ("Mahûd Beast-Tamer", "Ivory-Road Beast-Tamer"),
        ("Echoes of Mûmakan", "Tusk-Thunder of Mûmakan"),
        ("Sandstorm Raider", "Caravan Outrider"),
        ("Poisoned Blades", "Ivory-Ward Blades"),
        ("Sunfire Volley", "Jîret Volley"),
        ("Pezarsani", "Jîret"),
        ("Pezarsan", "Jîret"),
        ("Mahûd", "Ivory-Road"),
        ("Jelut", "Damudûr"),
    ],
    # Arthedain: every Gondor place, person and order in the three careers moves north. Ranks and
    # group names first (longest phrase first), then the catch-all place and people words.
    "arthedain": [
        # The Last Bridge spans the Hoarwell in Rhudaur; Arthedain's sword keeps the North Downs.
        ("Sword of the Last Bridge", "Sword of the North Downs"),
        ("shadows of Ithilien, ambushing enemies of Gondor", "wilds of Eriador, ambushing the servants of Angmar"),
        ("commands Gondor's frontline defense at the ruins of Osgiliath, forging order from chaos in brutal urban combat",
         "holds the walls of Fornost against the hosts of Angmar, forging order from chaos when the North Downs burn"),
        ("the coastal fiefdom of Belfalas", "the North-kingdom"),
        ("The Captain holds the line", "The Warden holds the line"),
        ("Captain of the White Company", "Chieftain of the Rangers"),
        ("Hunter of the Ithilien Road", "Hunter of the Greenway"),
        ("Watcher of Henneth Annûn", "Watcher of Weathertop"),
        ("Scout of North Ithilien", "Scout of the North Downs"),
        ("Bowman of Faramir", "Dúnedain Rangers"),
        ("Arrow of the Forbidden Pool", "Arrow of Nenuial"),
        ("Shadow of Emyn Arnen", "Shadow of the Chetwood"),
        ("Ithilien Scout", "Scout of Eriador"),
        ("Ranger of Ithilien", "Ranger of the North"),
        ("Ohtar of the Crossing", "Ohtar of the North Downs"),
        ("Warden of Osgiliath", "Warden of the Walls"),
        ("Captain of Osgiliath", "Warden of Fornost"),
        ("Warden of the East Bank", "Warden of the Dike"),
        ("Keeper of the Anduin Crossing", "Keeper of the Baranduin Crossing"),
        ("Vanguard of the Rammas", "Vanguard of the North Downs"),
        ("Host of Faramir", "Host of Arvedui"),
        ("Lord of the Dome of Stars", "Keeper of Amon Sûl"),
        ("Squire of Dol Amroth", "Squire of Fornost"),
        ("Knight of the Silver Swan", "Knight of the Seven Stars"),
        ("Swan-Knight", "Silver Knight"),
        ("Knight of Belfalas", "Knight of Arthedain"),
        ("Lancer of Dol Amroth", "Lancer of the North Downs"),
        ("Warden of the Bay", "Warden of Evendim"),
        ("Rider of the Silver Swan", "Rider of the Seven Stars"),
        ("Shieldwall of Imrahil", "Shieldwall of Arvegil"),
        ("Knight of the Swan-Prow", "Knight of the Silver Crown"),
        ("Charge of Dol Amroth", "Charge of Fornost"),
        ("charge of Dol Amroth", "charge of Fornost"),
        ("host of Belfalas", "host of Arthedain"),
        ("Dol Amroth", "Fornost"),
        ("Belfalas", "Arthedain"),
        ("Osgiliath", "Fornost"),
        ("Ithilien", "Eriador"),
        ("Faramir", "Arvedui"),
        ("Imrahil", "Arvegil"),
        ("Gondorian", "Arthedain"),  # before the bare name, or "Gondorian" becomes "Arthedainian"
        ("Gondor", "Arthedain"),
    ],
    # Cardolan (#978): Gondor's places move to the plains and rivers of the southern kingdom.
    "cardolan": [
        # The Last Bridge spans the Hoarwell in Rhudaur, so Cardolan's sword holds its own ford.
        ("Sword of the Last Bridge", "Sword of Sarn Ford"),
        ("shadows of Ithilien, ambushing enemies of Gondor", "river country of Minhiriath, ambushing raiders from Dunland"),
        ("commands Gondor's frontline defense at the ruins of Osgiliath, forging order from chaos in brutal urban combat",
         "keeps the barrows of Tyrn Gorthad and the walls of Caras Gorthad, forging order from chaos when the downs burn"),
        ("the coastal fiefdom of Belfalas", "the southern kingdom"),
        ("The Captain holds the line", "The Warden holds the line"),
        ("Captain of the White Company", "Captain of the Bowmen"),
        ("Hunter of the Ithilien Road", "Hunter of the Greenway"),
        ("Watcher of Henneth Annûn", "Watcher of the Marshes"),
        ("Scout of North Ithilien", "Scout of the South Downs"),
        ("Bowman of Faramir", "Bowman of the Greyflood"),
        ("Arrow of the Forbidden Pool", "Arrow of the Gwathló"),
        ("Shadow of Emyn Arnen", "Shadow of Eryn Vorn"),
        ("Ithilien Scout", "Scout of Minhiriath"),
        ("Ranger of Ithilien", "Bowman of Minhiriath"),
        ("Ohtar of the Crossing", "Ohtar of Sarn Ford"),
        ("Warden of Osgiliath", "Warden of the Barrows"),
        ("Captain of Osgiliath", "Warden of Tyrn Gorthad"),
        ("Warden of the East Bank", "Warden of the Downs"),
        ("Keeper of the Anduin Crossing", "Keeper of the Greyflood Crossing"),
        ("Vanguard of the Rammas", "Vanguard of the South Downs"),
        ("Host of Faramir", "Host of Aranthir"),
        ("Lord of the Dome of Stars", "Keeper of the Royal Barrows"),
        ("Squire of Dol Amroth", "Squire of Caras Gorthad"),
        ("Knight of the Silver Swan", "Knight of the Barrow-Kings"),
        ("Swan-Knight", "Barrow-Knight"),
        ("Knight of Belfalas", "Knight of Cardolan"),
        ("Lancer of Dol Amroth", "Lancer of Minhiriath"),
        ("Warden of the Bay", "Warden of Tharbad"),
        ("Rider of the Silver Swan", "Rider of the Plains"),
        ("Shieldwall of Imrahil", "Shieldwall of Aranthir"),
        ("Knight of the Swan-Prow", "Knight of the Greenway"),
        ("Charge of Dol Amroth", "Charge of Caras Gorthad"),
        ("charge of Dol Amroth", "charge of Caras Gorthad"),
        ("host of Belfalas", "host of Cardolan"),
        ("Dol Amroth", "Caras Gorthad"),
        ("Belfalas", "Cardolan"),
        ("Osgiliath", "Tharbad"),
        ("Ithilien", "Minhiriath"),
        ("Faramir", "Aranthir"),
        ("Imrahil", "Aranthir"),
        ("Gondorian", "Cardolan"),
        ("Gondor", "Cardolan"),
    ],
    # Rhudaur (#978): Gondor's places move to the hills of the Angle.
    "rhudaur": [
        ("shadows of Ithilien, ambushing enemies of Gondor", "fells of the Angle, ambushing orcs and trolls from the mountains"),
        ("commands Gondor's frontline defense at the ruins of Osgiliath, forging order from chaos in brutal urban combat",
         "holds the tower of Cameth Brin against the mountains, forging order from chaos when the fells burn"),
        ("the coastal fiefdom of Belfalas", "the hill kingdom"),
        ("The Captain holds the line", "The Captain holds the line"),
        ("Captain of the White Company", "Captain of the Angle Rangers"),
        ("Hunter of the Ithilien Road", "Hunter of the East Road"),
        ("Watcher of Henneth Annûn", "Watcher of the Last Bridge"),
        ("Scout of North Ithilien", "Scout of the Ettenmoors"),
        ("Bowman of Faramir", "Bowman of the Hoarwell"),
        ("Arrow of the Forbidden Pool", "Arrow of the Loudwater"),
        ("Shadow of Emyn Arnen", "Shadow of the Trollshaws"),
        ("Ithilien Scout", "Scout of the Angle"),
        ("Ranger of Ithilien", "Ranger of the Angle"),
        ("Ohtar of the Crossing", "Ohtar of the Last Bridge"),
        ("Warden of Osgiliath", "Warden of the Fells"),
        ("Captain of Osgiliath", "Captain of Cameth Brin"),
        ("Warden of the East Bank", "Warden of the Ettendale"),
        ("Keeper of the Anduin Crossing", "Keeper of the Hoarwell Crossing"),
        ("Vanguard of the Rammas", "Vanguard of the Coldfells"),
        ("Host of Faramir", "Host of Belegorn"),
        ("Lord of the Dome of Stars", "Lord of the Tower of Cameth Brin"),
        ("Squire of Dol Amroth", "Squire of the Angle"),
        ("Knight of the Silver Swan", "Knight of the Fells"),
        ("Swan-Knight", "Fell-Knight"),
        ("Knight of Belfalas", "Rider of the Fells"),
        ("Lancer of Dol Amroth", "Lancer of the Angle"),
        ("Warden of the Bay", "Warden of the Loudwater"),
        ("Rider of the Silver Swan", "Rider of the High Fells"),
        ("Shieldwall of Imrahil", "Shieldwall of Belegorn"),
        ("Knight of the Swan-Prow", "Knight of the Ettenmoors"),
        ("Charge of Dol Amroth", "Charge of Cameth Brin"),
        ("charge of Dol Amroth", "charge of Cameth Brin"),
        ("host of Belfalas", "host of Rhudaur"),
        ("Dol Amroth", "Cameth Brin"),
        ("Belfalas", "Rhudaur"),
        ("Osgiliath", "the Last Bridge"),
        ("Ithilien", "the Angle"),
        ("Faramir", "Belegorn"),
        ("Imrahil", "Belegorn"),
        ("Gondorian", "Rhudaur"),
        ("Gondor", "Rhudaur"),
    ],
    # Ered Luin clones Ironguard twice, so its wording lives per career in CAREER_TEXT_REMAP.
    "eredluin": [],
    # Anduin Vale and Angaladh clone Dale's careers, so their wording lives per career in
    # CAREER_TEXT_REMAP.
    "anduinvale": [],
    "angaladh": [],
}

# Per CLONED career, applied before the culture's TEXT_REMAP: a culture that clones one source twice
# needs two different rewrites of the same words. Each Ered Luin table replaces the whole source
# tooltip opening, which also drops the source's dash. Longest phrase first, as above.
CAREER_TEXT_REMAP = {
    # The gate-keepers of Thorin's Halls, King Farin's seat in the Blue Mountains.
    "warden_of_thorins_halls": [
        ("Invoke the strength of dwarven craft — boosts", "Invoke the strength of the mountain halls: boosts"),
        ("An elite dwarven warrior clad in mithril-forged plate who holds the deeps of Erebor against orc and dragon alike.",
         "An elite dwarven warrior in heavy mail of Blue Mountain iron who holds the gates of Thorin's Halls against goblin and orc alike."),
        ("Ironguard of Erebor", "Warden of Thorin's Halls"),
        ("Shieldwall of Durin", "Shieldwall of the Blue Mountains"),
        ("Wrath of Durin's Folk", "Wrath of the Blue Mountains"),
        ("Shieldwall of Erebor", "Shieldwall of Thorin's Halls"),
        ("Guard of the Arkenstone", "Guard of the King's Hall"),
        ("Mithril Bulwark", "Iron Bulwark"),
        ("Mithril Bastion", "Bastion of the Halls"),
    ],
    # The Broadbeam hewers of Belegost: the two-handed mattock and great axe line (#1045).
    "hewer_of_belegost": [
        ("Invoke the strength of dwarven craft — boosts", "Swing the great mattock with your kin: boosts"),
        ("An elite dwarven warrior clad in mithril-forged plate who holds the deeps of Erebor against orc and dragon alike.",
         "A Broadbeam hewer of Belegost who swings a great mattock in both hands, breaking shields and the orcs behind them."),
        ("dwarven craft protecting you", "the weight of the mattock behind you"),
        ("Ironguard of Erebor", "Hewer of Belegost"),
        ("Shieldwall of Durin", "Stonebreaker of the Blue Mountains"),
        ("Wrath of Durin's Folk", "Wrath of the Broadbeams"),
        ("Shieldwall of Erebor", "Hewing of Belegost"),
        ("Guard of the Arkenstone", "Breaker of the Broad Gate"),
        ("Hammer of the Deeps", "Mattock of the Deeps"),
        ("Gatewall Warden", "Shield-Breaker"),
        ("Mithril Bulwark", "Stonebreaker's Fury"),
        ("Mithril Bastion", "Mattock Storm"),
        ("Deep Warden", "Mattock-bearer"),
    ],
    # The Firebeards of Nogrod, crossbow-smiths of the southern halls.
    "nogrod_arbalester": [
        ("Load masterwork dwarven bolts — boosts", "Load bolts forged in Nogrod: boosts"),
        ("A dwarven engineer-warrior who wields masterwork crossbows of incredible stopping power, each bolt precision-crafted.",
         "A Firebeard engineer-warrior of Nogrod who wields masterwork crossbows of great stopping power, each bolt forged in the furnaces of the Blue Mountains."),
        ("Engine-Lord of the Iron Hills", "Forge-Lord of the Firebeards"),
        ("Master Quarreler", "Firebeard Quarreler"),
        ("Iron Hills Volley", "Nogrod Volley"),
        ("Watcher of Dale", "Watcher of the Dwarf-road"),
        ("Engine of Erebor", "Engine of Nogrod"),
        ("Precision Barrage", "Firebeard Volley"),
        ("Bolt-Smith", "Bolt-Smith of Nogrod"),
    ],

    # Anduin Vale: the Beornings and Woodmen of the Vales under Grimbeorn, son of Beorn.
    # The axe infantry career (Dale's guardsman: shield and spear in the source, the axe here).
    "axe_warden_of_beorns_hall": [
        ("Plant your feet and hold the gate — boosts", "Plant your feet and hold the ford: boosts"),
        ("A stalwart protector of the rebuilt city of Dale who holds the gate against all comers with shield and spear.",
         "A broad-shouldered axeman of the House of Beorn who holds the fords and passes of the Vales against goblin and wolf alike."),
        ("Gate-Warden of Dale", "Vale Axeman"),
        ("Shieldman of Girion", "Axe-warden of Beorn's Hall"),
        ("Knight of Dale", "Champion of the Carrock"),
        ("Gate Warden of Dale", "Axeman of the Vales"),
        ("Shieldwall of Esgaroth", "Axewall of the Old Ford"),
        ("Spear of Girion", "Axe of Beorn"),
        ("Bulwark of the Running", "Bulwark of the Gladden"),
        ("Vanguard of King Brand", "Vanguard of Grimbeorn"),
        ("The Last Stand at Erebor", "The Last Stand at the Carrock"),
        ("Gate Hold", "Carrock Stand"),
    ],
    # The Woodman bowmen of the Vales (Dale's marksman).
    "archer_of_eastbank": [
        ("Channel the legacy of Bard the Bowman — boosts", "Loose the arrows of the Woodmen: boosts"),
        ("A legendary bowman of Dale whose ancestors slew Smaug, carrying on the tradition of unerring marksmanship.",
         "A Woodman bowman of the Vales who shoots from the eaves of Mirkwood and the banks of the Gladden, carrying on a tradition of unerring marksmanship."),
        ("in honor of Bard the Bowman", "in honor of the Woodmen bowmen of the Vales"),
        ("Bowman of Esgaroth", "Bowman of the Gladden"),
        ("Dragon-Shooter", "Hawk-Eye of Eastbank"),
        ("Bearer of the Black Arrow", "Bow-lord of the Vales"),
        ("Watch of the Long Lake", "Watch of the Mirkwood Edge"),
        ("Heir of the Black Arrow", "Heir of the Woodmen"),
        ("Thrush-Sighted Archer", "Hawk-Sighted Archer"),
        ("Dragonslayer of Dale", "Wolfbane of the Vales"),
        ("The Bow of Girion's Line", "The Bow of the Eaves"),
        ("Black Arrow", "Carrock Volley"),
    ],
    # The Vale Rider cavalry career (Dale's outrider).
    "rider_of_the_vales": [
        ("Spur your horse along familiar roads — boosts", "Spur your horse along the Vale roads: boosts"),
        ("A mounted patrol of the northern trade roads who safeguards caravans between Dale and the Lonely Mountain.",
         "A mounted patrol of the Vales who rides the Old Ford road and the foot of the Misty Mountains, safeguarding farms and caravans from goblin raiders."),
        ("Road-Rider of Dale", "Road-Rider of the Vales"),
        ("Warden of the North-Road", "Warden of the Old Ford Road"),
        ("Marshal of the Bardings", "Marshal of the Vales"),
        ("Rider of the Running", "Rider of the Gladden"),
        ("Warden of the Caravan Road", "Warden of the Old Road"),
        ("Lance of the Long Valley", "Lance of the Vales"),
        ("Outrider of Erebor's Friendship", "Outrider of the Woodmen's Friendship"),
        ("The Northman Charge", "The Charge of the Carrock"),
        ("Courier of the Two Kings", "Courier of the Chieftain"),
        ("Trade Road Sprint", "Vale Gallop"),
    ],
    # Angaladh: the Woodmen of western Mirkwood under King Ingomar, on foot only.
    # The spear career (Dale's guardsman keeps its shield and spear).
    "warden_of_the_forest_gate": [
        ("Plant your feet and hold the gate — boosts", "Set your spear in the path and hold the line: boosts"),
        ("A stalwart protector of the rebuilt city of Dale who holds the gate against all comers with shield and spear.",
         "A stalwart guardian of the Forest Gate who holds the narrow way under the trees against orc, wolf and spider with shield and spear."),
        ("Gate-Warden of Dale", "Spear of the Forest Gate"),
        ("Shieldman of Girion", "Warden of the Forest Gate"),
        ("Knight of Dale", "Captain of Woodmen-town"),
        ("Gate Warden of Dale", "Spearman of Woodmen-town"),
        ("Shieldwall of Esgaroth", "Shieldwall of the Forest Gate"),
        ("Spear of Girion", "Spear of Ingomar"),
        ("Bulwark of the Running", "Bulwark of the Forest Road"),
        ("Vanguard of King Brand", "Vanguard of King Ingomar"),
        ("The Last Stand at Erebor", "The Last Stand at Thornhold"),
        ("Gate Hold", "Thornwall"),
    ],
    # The two-handed axe career (the same source, a second time).
    "hewer_of_ironwood_hall": [
        ("Plant your feet and hold the gate — boosts", "Swing the great axe with your kin: boosts"),
        ("A stalwart protector of the rebuilt city of Dale who holds the gate against all comers with shield and spear.",
         "An Ironwood hewer of Angáladh who swings a great axe in both hands, splitting shields and the orcs behind them."),
        ("Gate-Warden of Dale", "Ironwood Axeman"),
        ("Shieldman of Girion", "Hewer of Ironwood Hall"),
        ("Knight of Dale", "Great-axe of the Emyn Duir"),
        ("Gate Warden of Dale", "Axe-bearer of Ironwood Hall"),
        ("Shieldwall of Esgaroth", "Splitter of the Forest Road"),
        ("Spear of Girion", "Axe of the Heartwood"),
        ("Bulwark of the Running", "Hewing of the Emyn Duir"),
        ("Vanguard of King Brand", "Great-axe of King Ingomar"),
        ("The Last Stand at Erebor", "The Felling of the Dark Wood"),
        ("Gate Hold", "Heartwood Storm"),
    ],
    # The woodwards' bowmen (Dale's marksman).
    "woodward_of_the_forest_road": [
        ("Channel the legacy of Bard the Bowman — boosts", "Loose the arrows of the woodwards: boosts"),
        ("A legendary bowman of Dale whose ancestors slew Smaug, carrying on the tradition of unerring marksmanship.",
         "A woodward bowman of western Mirkwood who shoots from the platforms of the Forest Gate and the trees of the road, carrying on a tradition of unerring marksmanship."),
        ("in honor of Bard the Bowman", "in honor of the woodwards of the Forest Road"),
        ("Bowman of Esgaroth", "Bowman of Thornhold"),
        ("Dragon-Shooter", "Spider-Shooter"),
        ("Bearer of the Black Arrow", "Bow-Master of the Woodwards"),
        ("Watch of the Long Lake", "Watch of the Forest Road"),
        ("Heir of the Black Arrow", "Heir of the Woodwards"),
        ("Thrush-Sighted Archer", "Owl-Sighted Archer"),
        ("Dragonslayer of Dale", "Spider-Slayer of the Emyn Duir"),
        ("The Bow of Girion's Line", "The Bow of Ingomar's Line"),
        ("Black Arrow", "Ironwood Volley"),
    ],
}


def read(path):
    """Byte-faithful read. Returns (text_with_lf, newline) so the write can restore the original."""
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        raise SystemExit(f"{path}: unexpected BOM — none of the career files ship one")
    text = raw.decode("utf-8")
    newline = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), newline


def write(path, text, newline):
    path.write_bytes(text.replace("\n", newline).encode("utf-8"))


# Attributes whose value names an ART or FX asset on disk rather than a data id. These must survive
# the rename untouched: a cloned career that asks for `goblin_troll_driver_particle` is asking for a
# particle system nobody has authored, and the same goes for its sprite and its activation sound.
# Only 21 of the 50 shipped careers even have a portrait registered in TAOMSpriteData.xml, so
# minting six more ids would have widened that gap instead of reusing what exists.
ASSET_ATTRS = ("portrait_sprite", "sprite", "icon_sprite", "particle_effect", "sound_effect")


def rename(block, src_id, src_abbr, new_id, new_abbr):
    """Rewrite both id-spaces, then put the asset references back."""
    block = block.replace(src_id, new_id)
    # `_ctm_` / `taom_ctm_` / `career_choice_ctm_` — always delimited by underscores, so anchoring on
    # them avoids mangling an unrelated word that merely contains the three letters.
    block = re.sub(rf"(?<=_){re.escape(src_abbr)}(?=_)", new_abbr, block)

    def restore(m):
        value = m.group(2).replace(new_id, src_id)
        value = re.sub(rf"(?<=_){re.escape(new_abbr)}(?=_)", src_abbr, value)
        return f'{m.group(1)}="{value}"'

    return re.sub(rf'({"|".join(ASSET_ATTRS)})="([^"]*)"', restore, block)


def retext(block, culture, new_id=None):
    for old, new in CAREER_TEXT_REMAP.get(new_id, []) + TEXT_REMAP[culture]:
        block = block.replace(old, new)
    return block


def splice(name, text, anchor, block):
    """Insert `block` before the file's one closing `anchor`. A str.replace on a missing anchor
    returns the text unchanged, which still parses, and a second anchor would take the block twice,
    so anything but exactly one is refused before a file is written."""
    found = text.count(anchor)
    if found != 1:
        raise SystemExit(f"{name}: expected one {anchor.strip()!r} at the start of a line to insert "
                         f"before, found {found}; nothing written")
    return text.replace(anchor, block + anchor)


def extract(text, pattern, what):
    m = re.search(pattern, text, re.S)
    if not m:
        raise SystemExit(f"could not find {what} — the source file's shape has changed")
    return m.group(0)


def build(culture, src_id, src_abbr, new_id, new_abbr, display, careers, abilities, choices):
    """Return the four rendered blocks for one cloned career."""
    career = extract(careers, rf'  <Career id="{src_id}".*?</Career>', f"<Career> {src_id}")
    ability = extract(abilities, rf'  <AbilityTemplate id="{src_id}_ability".*?/>',
                      f"<AbilityTemplate> {src_id}_ability")
    root = extract(choices, rf'  <Choice id="{src_id}_root".*?</Choice>', f"root choice {src_id}")
    groups = re.findall(rf'  <ChoiceGroup id="{src_id}_[^"]*".*?</ChoiceGroup>', choices, re.S)
    if len(groups) != 6:
        raise SystemExit(f"{src_id}: expected 6 ChoiceGroups, found {len(groups)}")

    def fix(block):
        return retext(rename(block, src_id, src_abbr, new_id, new_abbr), culture, new_id)

    career = fix(career)
    # Repoint eligibility at the target culture. The source is single-culture, so a plain replace is
    # unambiguous; assert that rather than trust it.
    elig = ELIGIBILITY_SRC[culture]
    if career.count(f'<Culture id="{elig}" />') != 1:
        raise SystemExit(f"{src_id}: expected exactly one {elig} <Culture> row in <Career>")
    career = career.replace(f'<Culture id="{elig}" />', f'<Culture id="{culture}" />')
    for extra in ELIGIBILITY_DROP.get(culture, []):
        career, n = re.subn(rf'[ \t]*<Culture id="{re.escape(extra)}" />\n', "", career)
        if n != 1:
            raise SystemExit(f"{src_id}: expected exactly one {extra} <Culture> row to drop, found {n}")
    # display_name carries the career's own name, which the TEXT_REMAP table cannot know — it would
    # otherwise inherit whatever the source ability was called ("Gundabad Berserker" -> "Goblin-town
    # Frenzy", which is the ABILITY's name, not the career's). The key is `{=taom_career_<id>}`.
    career, n = re.subn(rf'(display_name="\{{=taom_career_{re.escape(new_id)}\}})[^"]*"',
                        rf'\g<1>{display}"', career)
    if n != 1:
        raise SystemExit(f"{new_id}: expected 1 display_name to rewrite, rewrote {n}")

    return career, fix(ability), fix(root) + "\n\n" + "\n\n".join(fix(g) for g in groups)


def main(argv=None):
    ap = argparse.ArgumentParser()
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the files (default: dry run)")
    mode.add_argument("--check", action="store_true",
                      help="write nothing; exit 1 when --apply would write or refuse. --apply writes "
                           "for a culture in TARGETS with no <Culture> row in taom_careers.xml; the "
                           "four files are checked against each other by the C# tests "
                           "CareerChoiceIntegrityTests and CareerCultureCoverageTests, not here")
    args = ap.parse_args(argv)

    careers, careers_nl = read(CAREERS_XML)
    abilities, abilities_nl = read(ABILITIES_XML)
    choices, choices_nl = read(CHOICES_XML)
    menu_text, menu_nl = read(MENU_JSON)

    new_careers, new_abilities, new_choices, new_menu, planned = [], [], [], [], []

    for culture in TARGETS:
        if f'<Culture id="{culture}" />' in careers:
            print(f"skip {culture}: already has careers")
            continue
        for src_id, src_abbr, new_id, new_abbr, display in targets_for(culture):
            c, a, ch = build(culture, src_id, src_abbr, new_id, new_abbr, display,
                             careers, abilities, choices)
            new_careers.append(c)
            new_abilities.append(a)
            new_choices.append(
                f"  <!-- ═══ {display} ({culture}), cloned from {src_id} ═══ -->\n" + ch)
            new_menu.append({"career_string_id": new_id, "skills": [], "attribute": "",
                             "focus_to_add": 0, "skill_level_to_add": 0, "attribute_level_to_add": 0})
            planned.append((culture, new_id, display))

    if not planned:
        print("nothing to do")
        return 0

    # Clone-contamination gate. An id-rename is case-sensitive and leaves player-facing strings
    # naming the source faction — the exact defect the new-factions RCA recorded, caught there only
    # after it shipped. Fail the run rather than emit it.
    generated = "\n".join(new_careers + new_abilities + new_choices)
    # Two things legitimately still name the source and must not trip the gate: provenance comments
    # (recording where a clone came from is the point) and asset references (deliberately reused —
    # see ASSET_ATTRS). Everything else naming Gundabad is contamination.
    payload = re.sub(r"<!--.*?-->", "", generated, flags=re.S)
    payload = re.sub(rf'({"|".join(ASSET_ATTRS)})="[^"]*"', "", payload)
    # Remove the ids this run MINTED before scanning for source ids. A target id can contain its own
    # source id as a substring — `blade_dancer` -> `falathrim_blade_dancer` — and reporting that as
    # contamination flags a correct rename as a failure. Longest first so nested names clear cleanly.
    for _, new_id, _ in planned:
        payload = payload.replace(new_id, "")
    for _, new_abbr in sorted({(t, row[3]) for t in TARGETS for row in targets_for(t)},
                              key=lambda x: -len(x[1])):
        payload = payload.replace(f"_{new_abbr}_", "_")
    # Display words the id/abbr scan below cannot see. Every source faction whose careers get cloned
    # needs its own people- and place-words listed here, or a TEXT_REMAP entry missed by the author
    # ships Aserai wording under a Harad career exactly the way "Gundabad" once shipped under a
    # Goblin-town one. Longest first so a nested word is reported at its most specific form.
    SOURCE_DISPLAY_WORDS = ["Gundabad", "Pezarsani", "Pezarsan", "Mahûd", "Jelut",
                            "Gondor", "Ithilien", "Osgiliath", "Belfalas", "Dol Amroth", "Faramir",
                            "Imrahil", "Henneth", "Emyn Arnen", "Rammas", "Anduin", "Swan",
                            # Erebor's (Ered Luin): no other clone source's careers use these.
                            "Lonely Mountain", "Iron Hills", "Arkenstone", "Erebor", "Mithril",
                            "mithril", "of Dale", "Dáin",
                            # Dale's (Anduin Vale and Angaladh): none of these survives a clone.
                            "Dale", "Dalian", "Barding", "Bard", "Esgaroth", "Smaug", "Girion",
                            "Brand", "Long Lake", "Black Arrow", "Gate Hold", "Trade Road",
                            "Two Kings", "Dragon"]
    source_words = SOURCE_DISPLAY_WORDS + [s for s, _ in SOURCES] + [f"_{a}_" for _, a in SOURCES]
    for tgt in TARGETS:
        for s, a in SOURCES_FOR.get(tgt, []):
            source_words += [s, f"_{a}_"]
    for word in source_words:
        if word in payload:
            raise SystemExit(f"clone contamination: '{word}' survived into generated output")
    ids = re.findall(r'<(?:Career|Choice|ChoiceGroup|AbilityTemplate) id="([^"]+)"', generated)
    clash = sorted(i for i in set(ids) if f'id="{i}"' in careers + abilities + choices)
    if clash:
        raise SystemExit(f"id collision with existing data: {clash[:8]}")
    if len(ids) != len(set(ids)):
        raise SystemExit("duplicate ids within generated output")

    print(f"{len(planned)} careers to add:")
    for culture, new_id, display in planned:
        print(f"  {culture:20s} {new_id:24s} {display}")
    print(f"  -> {len(ids)} new elements total")

    # Every output is spliced and parsed in every mode before anything is written (tools/README.md
    # "XML I/O convention"), so a dry run and --check refuse exactly what --apply would.
    careers = splice(CAREERS_XML.name, careers, "\n</Careers>",
                     "\n" + "\n\n".join(new_careers) + "\n")
    abilities = splice(ABILITIES_XML.name, abilities, "\n</AbilityTemplates>",
                       "\n" + "\n".join(new_abilities) + "\n")
    choices = splice(CHOICES_XML.name, choices, "\n</CareerChoices>",
                     "\n" + "\n\n".join(new_choices) + "\n")
    for text in (careers, abilities, choices):
        ET.fromstring(text.encode("utf-8"))

    menu = json.loads(menu_text)
    menu.extend(new_menu)
    menu_out = json.dumps(menu, indent=4, ensure_ascii=False) + "\n"

    if args.check:
        return 1
    if not args.apply:
        print("DRY RUN — re-run with --apply to write")
        return 0

    write(CAREERS_XML, careers, careers_nl)
    write(ABILITIES_XML, abilities, abilities_nl)
    write(CHOICES_XML, choices, choices_nl)
    write(MENU_JSON, menu_out, menu_nl)
    print(f"applied -> {CAREERS_XML.name}, {ABILITIES_XML.name}, {CHOICES_XML.name}, {MENU_JSON.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
