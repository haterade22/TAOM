#!/usr/bin/env python3
"""Expand the Misty Mountain Orcs to 15 clans, each a 10-strong orc warband (6♂/4♀).

The Misty Mountain Orcs (`Culture.mistymountainorcs` / `Kingdom.mistymountainorcs`)
ship with 5 clans (`clan_mistymountainorcs_1..5`), each 8 members (6 male + 2
female). This script brings the faction to **15 clans, every clan 6♂ / 4♀**, so
the orcs are numerous in keeping with their Misty-Mountains location:

  * **10 new clans** `clan_mistymountainorcs_6..15`, each a fresh 10-lord warband
    (owner chieftain `lord_MM<N>_1` + 5 male warriors + 4 females);
  * **top-up** the 5 existing clans from 6♂/2♀ to 6♂/4♀ by adding two female
    kin each (`lord_MM<N>_9`, `_10`) — pure additions, no existing entry edited.

Total: 10 new clans, 110 new lords (100 in the new clans + 10 top-up females).

Every lord is `race="orc"`, `culture="Culture.mistymountainorcs"`, `Infantry`,
authored full: an `<NPCCharacter>` in characters/lords.xml (the shared orc face,
orc skill set, evil-leaning traits, `mistymountainorcs_bat_template_medium_*` +
civilian equipment) and a `<Hero>` in characters/heroes.xml with
`faction="Faction.clan_mistymountainorcs_N"` + a short bio. New clans get a couple
of internal marriages; top-up females are faction-only kin.

New clan rows go in characters/clans.xml: `Kingdom.mistymountainorcs` super-faction,
the shared MM kingdom banner_key, the generic `kingdom_hero_party_mistymountainorcs_template`
party template (per-clan templates exist only for clans 1-5), tier 3, homed at the
existing MM settlements (shared homes -- minor fiefless clans).

All three insertions are anchored to the `TAOM-NEWFACTIONS:mistymountainorcs:END`
markers in each file (content-anchored -> immune to line drift).

Names are orcish: Tolkien-canon orc names (Grishnak, Ugluk, Shagrat, Lugdush,
Muzgash...) + invented Black-Speech-style names, deduped against the names already
in use. Localization uses the inline `{=key}Default` form (no Languages/ edits).

Usage:
    python tools/generate_mistymountain_clans.py            # dry-run
    python tools/generate_mistymountain_clans.py --apply    # write 3 files (.bak backups)

Idempotent: --apply aborts if clan_mistymountainorcs_6 already exists.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CHARS = REPO / "Main" / "_Module" / "ModuleData" / "characters"
LORDS, HEROES, CLANS = CHARS / "lords.xml", CHARS / "heroes.xml", CHARS / "clans.xml"
END_MARK = "<!-- TAOM-NEWFACTIONS:mistymountainorcs:END -->"

BANNER = "11.330.166.1528.1528.764.764.1.0.0.22000.149.171.700.700.764.764.0.0.0.22001.142.116.350.350.765.854.1.0.0"
FACE_KEY = "0000F00FC00030008771188F38770F8801F188778888888888888888546AF0F900C836030C8888880000000000000000000000000000000000000000439C0140"
HOMES = ["town_MM1", "town_MM2", "town_MM3", "castle_MM4", "castle_MM6"]
# (color, color2) palette reused from the existing 5 MM clans.
PALETTE = [
    ("FF181818", "FF573D33"), ("FF212121", "FF634B39"), ("FF2A2A2A", "FF6E5A40"),
    ("FF333333", "FF796B47"), ("FF3C3C3C", "FF857D4D"),
]
PARTY_TEMPLATE = "PartyTemplate.kingdom_hero_party_mistymountainorcs_template"

# Ten new orc clan (tribe/house) names for clans 6-15.
CLAN_NAMES = ["Grobûrz", "Skarnâk", "Maughâsh", "Throgmaw", "Lugbúrz",
              "Hrakdûr", "Uzgnâsh", "Vrakmaw", "Gashrim", "Morzûk"]

SKILLS = {
    "taom_orc_chieftain_skills": [
        ("OneHanded", 275), ("TwoHanded", 265), ("Polearm", 240), ("Bow", 170),
        ("Crossbow", 110), ("Throwing", 190), ("Riding", 190), ("Athletics", 265),
        ("Crafting", 140), ("Scouting", 240), ("Tactics", 255), ("Roguery", 240),
        ("Charm", 180), ("Leadership", 270), ("Trade", 140), ("Steward", 200),
        ("Medicine", 90), ("Engineering", 180),
    ],
    "taom_orc_warrior_skills": [
        ("OneHanded", 235), ("TwoHanded", 220), ("Polearm", 215), ("Bow", 140),
        ("Crossbow", 90), ("Throwing", 160), ("Riding", 150), ("Athletics", 240),
        ("Crafting", 110), ("Scouting", 200), ("Tactics", 180), ("Roguery", 200),
        ("Charm", 120), ("Leadership", 160), ("Trade", 100), ("Steward", 130),
        ("Medicine", 70), ("Engineering", 120),
    ],
    "taom_orc_female_skills": [
        ("OneHanded", 170), ("TwoHanded", 160), ("Polearm", 160), ("Bow", 160),
        ("Crossbow", 110), ("Throwing", 140), ("Riding", 110), ("Athletics", 200),
        ("Crafting", 180), ("Scouting", 170), ("Tactics", 160), ("Roguery", 210),
        ("Charm", 130), ("Leadership", 150), ("Trade", 130), ("Steward", 180),
        ("Medicine", 120), ("Engineering", 160),
    ],
}
TRAITS_ORDER = ["Honor", "Generosity", "Calculating", "Mercy", "Valor",
                "Egalitarian", "Oligarchic", "Authoritarian"]
TRAIT_PROFILES = [
    (-2, -1, 2, -2, 2, -1, 1, 2),   # chieftain (matches lord_MM1_1)
    (-2, -2, 1, -2, 1, -1, 1, 1),   # brutal warrior
    (-1, -1, 2, -1, 0, -1, 1, 1),   # cunning
    (-2, 0, 1, -2, 2, -1, 0, 1),    # savage
    (-1, -2, 1, -1, 1, -2, 0, 2),   # tyrant
    (-2, -1, 0, -2, 2, -1, 1, 0),   # bloodthirsty
]
EQUIP_LETTERS = ["a", "b", "c", "d", "e"]

# Curated orc name pools (deduped at runtime against names already in lords.xml).
MALE_NAMES = [
    "Grishnak", "Ugluk", "Shagrat", "Lugdush", "Muzgash", "Lagduf", "Mauhur",
    "Radbug", "Ufthak", "Grukhash", "Bolg", "Yazneg", "Othrod", "Boldog",
    "Golfimbul", "Grakmaw", "Skardush", "Throgar", "Burzlug", "Mokrash",
    "Naglur", "Vrakdush", "Hrumgash", "Uzdur", "Gorlak", "Snagrat", "Durbghash",
    "Mughrim", "Krithak", "Lurtzag", "Naznur", "Ufgrish", "Brakghash", "Morznak",
    "Gashzur", "Druzhak", "Krumbash", "Nargrim", "Skrunak", "Vughar", "Lazgar",
    "Hrokmaw", "Buzgar", "Maukrim", "Throkdush",
]
FEMALE_NAMES = [
    "Mogza", "Urgha", "Shazga", "Ulzga", "Skrelga", "Hagra", "Burzga", "Gralka",
    "Gnasha", "Vugra", "Mauza", "Snazga", "Krimza", "Throga", "Lugza", "Bargha",
    "Hrunza", "Uzga", "Gashka", "Durza", "Naghza", "Vrelga", "Hroza", "Skarza",
    "Lazga", "Brughza", "Nurzga", "Gulza", "Razga", "Mughza", "Drozga", "Krunza",
    "Nashga", "Vughza", "Snurga", "Hralga", "Bolzga", "Ufza",
]
_ROOTS = ["Gor", "Maukh", "Skarn", "Throg", "Lug", "Burz", "Hrak", "Uz", "Grish",
          "Mog", "Naz", "Vrak", "Durb", "Krith", "Lurt", "Ufth", "Brak", "Morz",
          "Gash", "Snag", "Grukh", "Lagd", "Muzg", "Shag", "Grom", "Krum", "Drub"]
_MSUF = ["ash", "uk", "nak", "dush", "maw", "zur", "gar", "lug", "rim", "rod", "bag", "mok"]
_FSUF = ["za", "ga", "ka", "sha", "ra", "gha", "zga", "lza", "na"]


def _pool(curated, suffixes, used):
    """Yield curated names first, then root+suffix combos, all unique vs `used`."""
    for n in curated:
        if n not in used:
            used.add(n); yield n
    for s in _FSUF if suffixes is _FSUF else _MSUF:
        for r in _ROOTS:
            n = r + s
            if n not in used:
                used.add(n); yield n


BIO_OWNER = "{name}, chieftain of the {clan} orcs, holds the deep galleries and cold passes of the Misty Mountains for the Shadow."
BIO_M = "{name} of the {clan} warband is a thick-necked orc captain, his cleaver notched from the raiding of the mountain roads."
BIO_F = "{name} of the {clan} orcs is a snarling she-orc of the deep places, as quick with the knife as any of her brood."


def render_npc(lord: dict, idx: int) -> str:
    female = lord["gender"] == "F"
    letter = EQUIP_LETTERS[idx % len(EQUIP_LETTERS)]
    female_attr = ' is_female="true"' if female else ""
    beard = "" if female else (
        "            <beard_tags>\n"
        '                <beard_tag name="battania" />\n'
        "            </beard_tags>\n"
    )
    prof = TRAIT_PROFILES[idx % len(TRAIT_PROFILES)]
    skills = "\n".join(f'            <skill id="{s}" value="{v}" />' for s, v in SKILLS[lord["template"]])
    traits = "\n".join(f'            <Trait id="{t}" value="{v}" />' for t, v in zip(TRAITS_ORDER, prof))
    return (
        f'    <NPCCharacter id="{lord["id"]}" race="orc" name="{{={lord["namekey"]}}}{lord["name"]}"'
        f' age="{lord["age"]}" voice="earnest"{female_attr} is_hero="true"'
        f' culture="Culture.mistymountainorcs" occupation="Lord" default_group="Infantry"'
        f' face_mesh_cache="true" skill_template="SkillSet.{lord["template"]}">\n'
        "        <face>\n"
        f'            <BodyProperties version="4" age="22.01" weight="0.2084" build="0.5231" key="{FACE_KEY}" />\n'
        "            <hair_tags>\n"
        '                <hair_tag name="battania" />\n'
        "            </hair_tags>\n"
        f"{beard}"
        "            <tattoo_tags>\n"
        '                <tattoo_tag name="Cleanface" />\n'
        "            </tattoo_tags>\n"
        "        </face>\n"
        "        <skills>\n"
        f"{skills}\n"
        "        </skills>\n"
        "        <Traits>\n"
        f"{traits}\n"
        "        </Traits>\n"
        "        <Equipments>\n"
        f'            <EquipmentSet id="mistymountainorcs_bat_template_medium_{letter}" />\n'
        f'            <EquipmentSet id="mistymountainorcs_civ_template_default_{letter}" equipmentType="Civilian" />\n'
        "        </Equipments>\n"
        "    </NPCCharacter>"
    )


def render_hero(lord: dict) -> str:
    spouse = f'\n\t\tspouse="Hero.{lord["spouse"]}"' if lord["spouse"] else ""
    tmpl = BIO_OWNER if lord["role"] == "owner" else (BIO_F if lord["gender"] == "F" else BIO_M)
    bio = tmpl.format(name=lord["name"], clan=lord["clan_name"])
    return (
        "\t<Hero\n"
        f'\t\tid="{lord["id"]}"{spouse}\n'
        f'\t\tfaction="Faction.{lord["clan"]}"\n'
        f'\t\ttext="{{={lord["biokey"]}}}{bio}" />'
    )


def render_clan(c: dict) -> str:
    return (
        "  <Faction\n"
        f'\t\tid="{c["id"]}"\n'
        f'\t\tinitial_home_settlement="Settlement.{c["home"]}"\n'
        f'\t\tname="{{=aom_{c["id"]}_name}}{c["name"]}"\n'
        '\t\ttier="3"\n'
        f'\t\towner="Hero.{c["owner"]}"\n'
        '\t\tculture="Culture.mistymountainorcs"\n'
        '\t\tsuper_faction="Kingdom.mistymountainorcs"\n'
        '\t\tis_noble="true"\n'
        f'\t\tcolor="{c["color"]}"\n'
        f'\t\tcolor2="{c["color2"]}"\n'
        f'\t\tdefault_party_template="{PARTY_TEMPLATE}"\n'
        f'\t\tbanner_key="{BANNER}" />'
    )


def build(used_names: set):
    males = _pool(MALE_NAMES, _MSUF, set(used_names))
    females = _pool(FEMALE_NAMES, _FSUF, set(used_names))
    clans, lords = [], []

    # New clans 6-15: full 10-lord warband each (6M/4F).
    for i in range(10):
        n = 6 + i
        cid = f"clan_mistymountainorcs_{n}"
        cname = CLAN_NAMES[i]
        color, color2 = PALETTE[i % len(PALETTE)]
        clans.append(dict(id=cid, name=cname, home=HOMES[i % len(HOMES)],
                          color=color, color2=color2, owner=f"lord_MM{n}_1"))
        # member layout: _1 owner(M chief), _2.._6 warrior(M), _7.._10 female
        # marriages: _2<->_7, _3<->_8
        layout = [
            (1, "M", "owner", "taom_orc_chieftain_skills", None, 35),
            (2, "M", "warrior", "taom_orc_warrior_skills", 7, 31),
            (3, "M", "warrior", "taom_orc_warrior_skills", 8, 29),
            (4, "M", "warrior", "taom_orc_warrior_skills", None, 27),
            (5, "M", "warrior", "taom_orc_warrior_skills", None, 24),
            (6, "M", "warrior", "taom_orc_warrior_skills", None, 22),
            (7, "F", "lady", "taom_orc_female_skills", 2, 28),
            (8, "F", "lady", "taom_orc_female_skills", 3, 26),
            (9, "F", "lady", "taom_orc_female_skills", None, 23),
            (10, "F", "lady", "taom_orc_female_skills", None, 21),
        ]
        for slot, g, role, tmpl, partner, age in layout:
            lid = f"lord_MM{n}_{slot}"
            name = next(males) if g == "M" else next(females)
            lords.append(dict(
                id=lid, name=name, namekey=f"aom_{lid}_name", biokey=f"aom_{lid}_bio",
                gender=g, role=role, template=tmpl, age=age,
                spouse=(f"lord_MM{n}_{partner}" if partner else None),
                clan=cid, clan_name=cname))

    # Top-up existing clans 1-5: +2 females each (_9, _10), faction-only.
    existing_names = {1: "Bûrzghâsh", 2: "Krimpâsh", 3: "Dushnakh", 4: "Morgrim", 5: "Vargrim"}
    for n in range(1, 6):
        cid = f"clan_mistymountainorcs_{n}"
        for slot, age in [(9, 23), (10, 21)]:
            lid = f"lord_MM{n}_{slot}"
            lords.append(dict(
                id=lid, name=next(females), namekey=f"aom_{lid}_name", biokey=f"aom_{lid}_bio",
                gender="F", role="lady", template="taom_orc_female_skills", age=age,
                spouse=None, clan=cid, clan_name=existing_names[n]))
    return clans, lords


def splice_before(text: str, marker: str, block: str, label: str) -> str:
    i = text.find(marker)
    if i == -1:
        sys.exit(f"ERROR: marker for {label} not found -- aborting.")
    return text[:i] + block + "\n  " + text[i:]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    lords_txt = LORDS.read_text(encoding="utf-8")
    heroes_txt = HEROES.read_text(encoding="utf-8")
    clans_txt = CLANS.read_text(encoding="utf-8")

    if 'id="clan_mistymountainorcs_6"' in clans_txt:
        sys.exit("ERROR: clan_mistymountainorcs_6 already present -- generator already applied. Aborting.")

    used = set(re.findall(r'name="\{=aom_lord_MM[0-9_]+_name\}([^"]+)"', lords_txt))
    clans, lords = build(used)
    males = sum(1 for l in lords if l["gender"] == "M")

    npc_block = "\n".join(render_npc(l, i) for i, l in enumerate(lords))
    hero_block = "\n".join(render_hero(l) for l in lords)
    clan_block = "\n".join(render_clan(c) for c in clans)

    new_lords = splice_before(lords_txt, END_MARK, npc_block, "lords.xml")
    new_heroes = splice_before(heroes_txt, END_MARK, hero_block, "heroes.xml")
    new_clans = splice_before(clans_txt, END_MARK, clan_block, "clans.xml")

    print(f"New clans: {len(clans)}  (clan_mistymountainorcs_6..15)")
    print(f"New lords: {len(lords)}  ({males} male / {len(lords)-males} female)")
    print(f"  - 100 in new clans 6-15 (10 each, 6M/4F)")
    print(f"  - 10 top-up females for existing clans 1-5 (-> 6M/4F)")
    print("\n----- sample clan -----")
    print(render_clan(clans[0]))
    print("\n----- sample owner NPCCharacter -----")
    print(render_npc(lords[0], 0))
    print("\n----- sample Hero (married) -----")
    print(render_hero(lords[1]))

    if not args.apply:
        print("\n[dry-run] no files written. Re-run with --apply.")
        return
    for p, txt in [(LORDS, lords_txt), (HEROES, heroes_txt), (CLANS, clans_txt)]:
        p.with_suffix(".xml.bak").write_text(txt, encoding="utf-8")
    LORDS.write_text(new_lords, encoding="utf-8")
    HEROES.write_text(new_heroes, encoding="utf-8")
    CLANS.write_text(new_clans, encoding="utf-8")
    print("\n[apply] wrote lords.xml, heroes.xml, clans.xml (.bak backups saved).")


if __name__ == "__main__":
    main()
