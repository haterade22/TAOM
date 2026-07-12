#!/usr/bin/env python3
"""Expand the eight Rhûn (khuzait-culture) clans 12-19 to ~10 members each.

TAOM's Rhûn clans clan_khuzait_12 .. _19 each ship with at most a single owner
hero (and clans 12 + 13 are *ownerless* -- clans.xml points their owner at
Hero.lord_6_23 / lord_6_24, which are defined nowhere). This script fills every
clan out to a 10-strong noble house: 6 male + 4 female (the owner counts as one
of the six males), matching the multi-member-family precedent already used by the
Gondor houses (clan_empire_west_12 et al.).

For each clan it emits, in document order:
  * the owner NPCCharacter + Hero, **only when the owner does not yet exist**
    (clans 12 + 13 -> lord_6_23 / lord_6_24);
  * nine kinsfolk lord_6_<owner>_1 .. _9.

Each new lord is authored "full treatment": a complete <NPCCharacter> in
characters/lords.xml (face / skills / traits / battle + civilian equipment) and a
<Hero> in characters/heroes.xml carrying `faction="Faction.clan_khuzait_NN"`
(the actual clan-membership mechanism -- verified against the existing data) plus
a short lore bio. Family wiring is deliberately shallow: owner stays single, and
the nine kin contain two married couples (spouse links), the rest faction-only
house kin. No father/mother links are emitted, because the existing owners are
young (age 31-41) and adult children would be age-inconsistent -- and so that no
existing owner Hero entry has to be edited.

Names are drawn from researched Easterling-flavoured pools: Tolkien-canon
Easterling names (Borlad, Ulfast, Brodda, Lorgan ...), Mongol/Turkic Khuzait
given names (the khuzait culture is the Tartar/Hun counterpart), and the
invented-but-consistent style already in TAOM (Ethacali, Yurzal, Rurazaur ...).
Each new lord takes the clan surname (e.g. "Borlad Hûz").

Localization: lord names + bios use the inline `{=key}Default` form. The inline
default renders without any matching <string> entry, exactly like the existing
TAOM lords -- so no Languages/ edits are required.

Usage:
    python tools/generate_rhun_clan_lords.py            # dry-run (default): summary + a sample block
    python tools/generate_rhun_clan_lords.py --apply    # write lords.xml + heroes.xml (.bak backups)

Idempotent: --apply refuses to run if lord_6_23 already exists in lords.xml.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LORDS = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"
HEROES = REPO / "Main" / "_Module" / "ModuleData" / "characters" / "heroes.xml"

# --------------------------------------------------------------------------- #
# Per-clan map: owner lord number -> (clan id, surname, owner_is_new)
# --------------------------------------------------------------------------- #
CLANS = [
    (23, "clan_khuzait_12", "Hûz", True),
    (24, "clan_khuzait_13", "Adekig", True),
    (25, "clan_khuzait_14", "Cilzeron", False),
    (26, "clan_khuzait_15", "Kalkian", False),
    (27, "clan_khuzait_16", "Zorian", False),
    (28, "clan_khuzait_17", "Mithruntai", False),
    (29, "clan_khuzait_18", "Vazevian", False),
    (30, "clan_khuzait_19", "Bozorganith", False),
]

# Real, in-use khuzait BodyProperties face keys harvested from the existing
# Rhûn lords (lords.xml). Rotated for face variety; valid khuzait morphs.
MALE_FACE_KEYS = [
    "0016FC0FC000274D9EBF61FFB762A46EA4959AF6F9A58E3DC8756149D64B84A8000C265309B1E944000000000000000000000000000000000000000000F07080",
    "0026F80E40002343B8708B896D682237D5D88B3D557E344C17243FB5B88988680077760307967D3B00000000000000000000000000000000000000003EF41002",
]
FEMALE_FACE_KEYS = [
    "0016FC0E5000100158708BD6CDC852299D4AB89EB77F390E84269783AA69586C017776130756C3A900000000000000000000000000000000000000003EF43083",
    "0016F80E5000200EB8708BD6CDC85229D3698B3ABDFE344CD22D3DD538898868017776130796723B00000000000000000000000000000000000000003EF41003",
    "0016E00B400006D3507044293BC820482534773EE5DE57546D6B291183394315056AC05706556766000000000000000000000000000000000000000028C82104",
    "0011D40FD4CC300BCF7616D8AD922604F4116550F7EF4310E0C91E1974071056016776130600120E000000000000000000000000000000000000000034C42083",
    "0016F80E4000200AB8B5A8AB37C5891C4BA88B3ABDFE344C5ADD18434234CBA6017776130746491E00000000000000000000000000000000000000003EF410C3",
    "0016F80CD400100318B5A8AB37C5891C4BA88B3A22AF3A4C6388C1954234CBA601777613076B5C2300000000000000000000000000000000000000003EF41003",
    "000BF80CD100100318B664517CBB94C8D5398B3A22AF3A4C5B2C76989C692815017776130799894800000000000000000000000000000000000000003EF41043",
    "0011C80E50E410026053AA569B16689986BA77528958D98CA8622BA78AC9695201577613052AB956000000000000000000000000000000000000000042B41084",
    "000BC80FD1000010185895BAB8A7A974AA7C383AE2FF3A45868B383997479BD70177761307BB856400000000000000000000000000000000000000003EF410C3",
]
BODY_JITTER = [(0.5, 0.5), (0.6, 0.55), (0.45, 0.6), (0.7, 0.5), (0.4, 0.45), (0.55, 0.65)]

# Easterling SkillSets (engine reads `skill_template`; the inline <skills> below
# mirror these values verbatim, as documentation, matching existing lords).
SKILLS = {
    "taom_easterling_lord_skills": [
        ("OneHanded", 230), ("TwoHanded", 170), ("Polearm", 235), ("Bow", 260),
        ("Crossbow", 110), ("Throwing", 180), ("Riding", 275), ("Athletics", 235),
        ("Crafting", 120), ("Scouting", 240), ("Tactics", 235), ("Roguery", 170),
        ("Charm", 190), ("Leadership", 235), ("Trade", 180), ("Steward", 200),
        ("Medicine", 140), ("Engineering", 170),
    ],
    "taom_easterling_archer_skills": [
        ("OneHanded", 210), ("TwoHanded", 140), ("Polearm", 210), ("Bow", 275),
        ("Crossbow", 130), ("Throwing", 190), ("Riding", 265), ("Athletics", 245),
        ("Crafting", 100), ("Scouting", 255), ("Tactics", 200), ("Roguery", 150),
        ("Charm", 150), ("Leadership", 170), ("Trade", 150), ("Steward", 160),
        ("Medicine", 120), ("Engineering", 130),
    ],
    "taom_easterling_lady_skills": [
        ("OneHanded", 80), ("TwoHanded", 50), ("Polearm", 70), ("Bow", 170),
        ("Crossbow", 80), ("Throwing", 80), ("Riding", 215), ("Athletics", 160),
        ("Crafting", 160), ("Scouting", 180), ("Tactics", 150), ("Roguery", 100),
        ("Charm", 215), ("Leadership", 160), ("Trade", 190), ("Steward", 225),
        ("Medicine", 180), ("Engineering", 140),
    ],
}

# Trait profiles (Honor, Generosity, Calculating, Mercy, Valor, Egalitarian,
# Oligarchic, Authoritarian). Easterlings serve Sauron -> lean cruel/authoritarian,
# with a few honourable warriors for spread. Rotated per lord.
TRAITS_ORDER = ["Honor", "Generosity", "Calculating", "Mercy", "Valor",
                "Egalitarian", "Oligarchic", "Authoritarian"]
TRAIT_PROFILES = [
    (-2, -1, 1, -2, 1, -1, 1, 1),   # cruel raider
    (-1, 0, 2, -1, 0, -1, 1, 1),    # cold schemer
    (0, -1, 1, -1, 1, -2, 0, 2),    # zealot
    (1, 0, 1, 0, 2, -1, 1, 0),      # stern warrior
    (2, 1, 0, 1, 2, 0, 0, -1),      # honourable
    (0, 1, 1, 1, 0, 0, 1, 0),       # proud noble
]

EQUIP_LETTERS = ["a", "b", "c", "d", "e"]

MALE_NAMES = [
    "Borlad", "Borlach", "Borthand", "Ulfast", "Ulwarth", "Uldor", "Brodda",
    "Lorgan", "Böri", "Kustîg", "Yumruk", "Adarkidai", "Ganbaatar", "Gantulga",
    "Khenbish", "Muunokhoi", "Subetai", "Jelme", "Boroghul", "Mukhali",
    "Chilaun", "Berke", "Toqto", "Arghun", "Baidar", "Kadan", "Orda", "Sartaq",
    "Nogai", "Tamir", "Otgon", "Temur", "Achiq", "Qutlugh", "Vethkar", "Aurzan",
    "Khorlug", "Saghost", "Dûrkan", "Mazdûr", "Hethûl", "Taurkan", "Belghûr",
    "Sorqan", "Naranbaatar", "Bataar", "Asurang", "Dorgon", "Esükai", "Targ",
]
FEMALE_NAMES = [
    "Armaga", "Cotota", "Chinua", "Gerel", "Goksun", "Khulan", "Konsha", "Samga",
    "Sarnai", "Sertac", "Suna", "Tsetseg", "Tuya", "Yesugen", "Yosma", "Aigiarn",
    "Alagh", "Alaqai", "Altani", "Bayalun", "Börte", "Bulgan", "Chimeg", "Doquz",
    "Enkhtuya", "Oyuun", "Sarangerel", "Tselmeg", "Udval", "Zaya", "Nergui",
    "Narangerel",
]

# Kin slot layout (slots 1..9). gender, role, married-partner-slot (or None),
# template key, default_group, voice, age.
# Males among kin: 1,3,5,6,7 (5). Females: 2,4,8,9 (4). + male owner = 6M / 4F.
KIN_SLOTS = [
    # slot, gender, role,      partner, template,                        group,        voice,       age
    (1, "M", "warrior", 2, "taom_easterling_lord_skills",   "Cavalry",    "curt",       34),
    (2, "F", "lady",    1, "taom_easterling_lady_skills",   "Cavalry",    "ironic",     31),
    (3, "M", "warrior", 4, "taom_easterling_lord_skills",   "Cavalry",    "earnest",    29),
    (4, "F", "lady",    3, "taom_easterling_lady_skills",   "Cavalry",    "softspoken", 27),
    (5, "M", "archer",  None, "taom_easterling_archer_skills", "HorseArcher", "curt",    24),
    (6, "M", "warrior", None, "taom_easterling_lord_skills", "Cavalry",    "curt",       41),
    (7, "M", "warrior", None, "taom_easterling_lord_skills", "Cavalry",    "earnest",    22),
    (8, "F", "lady",    None, "taom_easterling_lady_skills", "Cavalry",    "ironic",     26),
    (9, "F", "lady",    None, "taom_easterling_lady_skills", "Cavalry",    "softspoken", 20),
]

# Bio templates keyed by role (no double-quotes / ampersands -> XML-safe inline).
BIO = {
    "owner": "{name}, lord of House {sur}, holds the saddle-thrones of his Easterling kin east of the Sea of Rhûn and bends the tribes to the will of the Dark Tower.",
    "warrior": "{name} of House {sur} rides at the head of the clan's lancers, a hard-handed kinsman whose banner is feared across the plains of Rhûn.",
    "archer": "{name} of House {sur} is counted among the keen horse-archers of Rhûn, loosing his shafts from the saddle at a full gallop.",
    "lady": "{name} of House {sur} is a proud woman of the Easterlings, mistress of hearth and granary and no stranger to the horse and the bow.",
}


def render_skills(template: str) -> str:
    lines = [f'            <skill id="{sid}" value="{val}" />' for sid, val in SKILLS[template]]
    return "\n".join(lines)


def render_traits(idx: int) -> str:
    prof = TRAIT_PROFILES[idx % len(TRAIT_PROFILES)]
    lines = [f'            <Trait id="{tid}" value="{val}" />' for tid, val in zip(TRAITS_ORDER, prof)]
    return "\n".join(lines)


def render_npc(lord: dict, idx: int) -> str:
    female = lord["gender"] == "F"
    weight, build = BODY_JITTER[idx % len(BODY_JITTER)]
    if female:
        key = FEMALE_FACE_KEYS[idx % len(FEMALE_FACE_KEYS)]
    else:
        key = MALE_FACE_KEYS[idx % len(MALE_FACE_KEYS)]
    letter = EQUIP_LETTERS[idx % len(EQUIP_LETTERS)]
    female_attr = ' is_female="true"' if female else ""
    beard = "" if female else (
        "            <beard_tags>\n"
        '                <beard_tag name="khuzait" />\n'
        "            </beard_tags>\n"
    )
    return (
        f'    <NPCCharacter id="{lord["id"]}" name="{{={lord["namekey"]}}}{lord["name"]}"'
        f' age="{lord["age"]}" voice="{lord["voice"]}"{female_attr} default_group="{lord["group"]}"'
        f' is_hero="true" culture="Culture.khuzait" occupation="Lord" face_mesh_cache="true"'
        f' skill_template="SkillSet.{lord["template"]}">\n'
        "        <face>\n"
        f'            <BodyProperties version="4" weight="{weight}" build="{build}" key="{key}" />\n'
        "            <hair_tags>\n"
        '                <hair_tag name="khuzait" />\n'
        "            </hair_tags>\n"
        f"{beard}"
        "            <tattoo_tags>\n"
        '                <tattoo_tag name="Cleanface" />\n'
        "            </tattoo_tags>\n"
        "        </face>\n"
        "        <skills>\n"
        f"{render_skills(lord['template'])}\n"
        "        </skills>\n"
        "        <Traits>\n"
        f"{render_traits(idx)}\n"
        "        </Traits>\n"
        "        <Equipments>\n"
        f'            <EquipmentSet id="rhun_bat_template_medium_{letter}" />\n'
        f'            <EquipmentSet id="rhun_civ_template_{letter}" equipmentType="Civilian" />\n'
        "        </Equipments>\n"
        "    </NPCCharacter>"
    )


def render_hero(lord: dict) -> str:
    spouse = ""
    if lord["spouse"]:
        spouse = f'\n\t\tspouse="Hero.{lord["spouse"]}"'
    bio = BIO[lord["role"]].format(name=lord["name"].split()[0], sur=lord["surname"])
    return (
        "\t<Hero\n"
        f'\t\tid="{lord["id"]}"{spouse}\n'
        f'\t\tfaction="Faction.{lord["clan"]}"\n'
        f'\t\ttext="{{={lord["biokey"]}}}{bio}" />'
    )


def build_lords() -> list[dict]:
    lords: list[dict] = []
    male_i = 0
    female_i = 0
    for owner_num, clan, surname, owner_new in CLANS:
        # Owner -- only authored when missing (clans 12 + 13).
        if owner_new:
            given = MALE_NAMES[male_i]; male_i += 1
            oid = f"lord_6_{owner_num}"
            lords.append(dict(
                id=oid, name=f"{given} {surname}", namekey=f"aom_{oid}_name",
                biokey=f"aom_{oid}_bio", gender="M", role="owner", template="taom_easterling_lord_skills",
                group="Cavalry", voice="curt", age=48, spouse=None, clan=clan, surname=surname,
            ))
        # Nine kin.
        for slot, gender, role, partner, template, group, voice, age in KIN_SLOTS:
            kid = f"lord_6_{owner_num}_{slot}"
            if gender == "M":
                given = MALE_NAMES[male_i]; male_i += 1
            else:
                given = FEMALE_NAMES[female_i]; female_i += 1
            spouse = f"lord_6_{owner_num}_{partner}" if partner else None
            lords.append(dict(
                id=kid, name=f"{given} {surname}", namekey=f"aom_{kid}_name",
                biokey=f"aom_{kid}_bio", gender=gender, role=role, template=template,
                group=group, voice=voice, age=age, spouse=spouse, clan=clan, surname=surname,
            ))
    return lords


def splice(text: str, anchor_re: str, insertion: str, label: str) -> str:
    m = re.search(anchor_re, text)
    if not m:
        sys.exit(f"ERROR: could not find {label} anchor -- aborting (no changes made).")
    end = m.end()
    return text[:end] + "\n" + insertion + text[end:]


def main() -> None:
    ap = argparse.ArgumentParser(description="Expand Rhûn clans 12-19 to 10 members each.")
    ap.add_argument("--apply", action="store_true", help="write the files (default: dry-run)")
    args = ap.parse_args()

    lords = build_lords()
    males = sum(1 for l in lords if l["gender"] == "M")
    females = len(lords) - males

    lords_txt = LORDS.read_text(encoding="utf-8")
    heroes_txt = HEROES.read_text(encoding="utf-8")

    if 'id="lord_6_23"' in lords_txt:
        sys.exit("ERROR: lord_6_23 already present in lords.xml -- generator appears already applied. Aborting.")

    npc_block = "\n".join(render_npc(l, i) for i, l in enumerate(lords))
    hero_block = "\n".join(render_hero(l) for l in lords)

    # Anchor after the lord_6_30 NPCCharacter / Hero (content-anchored -> immune to line drift).
    npc_anchor = r'<NPCCharacter\b[^>]*?id="lord_6_30"[^>]*?>[\s\S]*?</NPCCharacter>'
    hero_anchor = r'<Hero\b[^>]*?id="lord_6_30"[^>]*?/>'

    new_lords = splice(lords_txt, npc_anchor, npc_block, "lords.xml lord_6_30 NPCCharacter")
    new_heroes = splice(heroes_txt, hero_anchor, hero_block, "heroes.xml lord_6_30 Hero")

    print(f"Lords to add: {len(lords)}  ({males} male / {females} female)")
    print(f"Per clan: " + ", ".join(
        f"{clan.split('_')[-1]}={'10' if new else '9 kin'}" for _, clan, _, new in CLANS))
    print(f"New owners (filling dangling refs): lord_6_23 (Hûz), lord_6_24 (Adekig)")
    print()
    print("----- sample NPCCharacter (first lord) -----")
    print(render_npc(lords[0], 0))
    print()
    print("----- sample Hero (married kin, lord_6_23_1) -----")
    print(render_hero(next(l for l in lords if l["id"] == "lord_6_23_1")))

    if not args.apply:
        print("\n[dry-run] no files written. Re-run with --apply to write.")
        return

    LORDS.with_suffix(".xml.bak").write_text(lords_txt, encoding="utf-8")
    HEROES.with_suffix(".xml.bak").write_text(heroes_txt, encoding="utf-8")
    LORDS.write_text(new_lords, encoding="utf-8")
    HEROES.write_text(new_heroes, encoding="utf-8")
    print(f"\n[apply] wrote {LORDS.relative_to(REPO)} and {HEROES.relative_to(REPO)} (.bak backups saved).")


if __name__ == "__main__":
    main()
