#!/usr/bin/env python3
"""One-off: add 5 new clans (clan_gundabad_6..10) to the Gundabad kingdom.

Each new clan:
  * carries the Gundabad KINGDOM banner key (user request),
  * is fiefless, homed at Settlement.town_G1 (matches existing minor Gundabad clans),
  * gets 10 lords = 6 male + 4 female, all race="pale_uruk".

The generator CLONES the real clan_gundabad_5 / lord_G5_1 / lord_G5_2 / lord_G5_9
blocks verbatim and substitutes ids/names/colors, so the inserted XML is
byte-identical in structure/whitespace to its hand-authored siblings.

Idempotent: re-running after clan_gundabad_6 exists is a no-op.

Usage:  python tools/add_gundabad_clans.py [--dry-run]
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MD = ROOT / "Main" / "_Module" / "ModuleData" / "characters"
CLANS = MD / "clans.xml"
LORDS = MD / "lords.xml"
HEROES = MD / "heroes.xml"

DRY = "--dry-run" in sys.argv

KINGDOM_BANNER = (
    "11.330.166.1528.1528.764.764.1.0.0.22000.149.171.700.700.764.764."
    "0.0.0.22001.142.116.350.350.765.854.1.0.0"
)
GENERIC_PARTY_TEMPLATE = "PartyTemplate.kingdom_hero_party_gundabad_template"

# clan number -> (name, color, color2)
CLANS_NEW = {
    6:  ("Grishûk", "FF1F1B18", "FF5E4636"),
    7:  ("Lugdush",      "FF26221C", "FF6B5238"),
    8:  ("Maukrim",      "FF302B24", "FF74603E"),
    9:  ("Skarzag",      "FF3A352C", "FF7E6B45"),
    10: ("Throkmaw",     "FF45413A", "FF8A8050"),
}

# 30 male first names (6 per clan), 20 female (4 per clan) - all distinct
MALE_NAMES = [
    "Grok", "Muzgash", "Gorbag", "Shagrat", "Ufthak", "Radbug",
    "Mauhur", "Uglûk", "Yazneg", "Brogg", "Durbuz", "Hrog",
    "Mogdar", "Skraat", "Vrasku", "Lagduf", "Muzgur", "Orbug",
    "Zogdush", "Karash", "Dushgar", "Gribnak", "Rukhash", "Targ",
    "Buzgra", "Hrakgar", "Molghor", "Snagduf", "Wruk", "Drubog",
]
FEMALE_NAMES = [
    "Gorza", "Murza", "Shelka", "Ghaaz", "Brakka", "Uzdra", "Lurgza", "Skarla",
    "Vroka", "Murga", "Throga", "Druzga", "Nazga", "Bolga", "Grisha", "Ushka",
    "Zogra", "Lakhza", "Urzga", "Maghra",
]
MALE_AGES = [42, 33, 38, 29, 45, 31]   # slot 1 (owner/chieftain) oldest
FEMALE_AGES = [27, 35, 24, 31]

assert len(MALE_NAMES) == 30 and len(set(MALE_NAMES)) == 30
assert len(FEMALE_NAMES) == 20 and len(set(FEMALE_NAMES)) == 20


def extract(text, pattern):
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise SystemExit(f"Template not found: {pattern!r}")
    return m


def build_clans(clans_text):
    krug = extract(clans_text, r"  <!-- Krûgash -->.*?/>")
    template = krug.group(0)
    blocks = []
    for n, (name, color, color2) in CLANS_NEW.items():
        b = template
        b = b.replace("clan_gundabad_5", f"clan_gundabad_{n}")
        b = b.replace("lord_G5_1", f"lord_G{n}_1")
        b = b.replace("Krûgash", name)            # comment + name value
        b = b.replace('color="FF423F36"', f'color="{color}"')
        b = b.replace('color2="FF857D4D"', f'color2="{color2}"')
        b = b.replace(
            'default_party_template="PartyTemplate.kingdom_hero_party_gundabad_gundabad_5_template"',
            f'default_party_template="{GENERIC_PARTY_TEMPLATE}"',
        )
        b = b.replace(
            "11.331.331.1528.1528.764.764.1.0.0.19002.2004.171.700.700.764.764.0.0.0",
            KINGDOM_BANNER,
        )
        blocks.append(b)
    new_block = "\n\n" + "\n\n".join(blocks)
    out = clans_text[:krug.end()] + new_block + clans_text[krug.end():]
    return out


def make_lord(template, old_id, new_id, old_name, new_name, new_age):
    b = template
    b = b.replace(old_id, new_id)                       # id + name key + facekey-safe
    b = b.replace("}" + old_name + '"', "}" + new_name + '"')
    b = re.sub(r'age="[\d.]+"', f'age="{new_age}"', b, count=1)
    return b


def build_lords(lords_text):
    chief = extract(lords_text, r'    <NPCCharacter id="lord_G5_1".*?</NPCCharacter>').group(0)
    warr = extract(lords_text, r'    <NPCCharacter id="lord_G5_2".*?</NPCCharacter>').group(0)
    fem = extract(lords_text, r'    <NPCCharacter id="lord_G5_9".*?</NPCCharacter>').group(0)
    g5_10 = extract(lords_text, r'    <NPCCharacter id="lord_G5_10".*?</NPCCharacter>')

    blocks = []
    for ci, n in enumerate(CLANS_NEW):
        clan_name = CLANS_NEW[n][0]
        blocks.append(f"    <!-- Gundabad Clan {n} ({clan_name}) -->")
        # males 1..6 (slot 1 = chieftain template, 2..6 = warrior template)
        for slot in range(1, 7):
            mname = MALE_NAMES[ci * 6 + (slot - 1)]
            age = MALE_AGES[slot - 1]
            if slot == 1:
                blocks.append(make_lord(chief, "lord_G5_1", f"lord_G{n}_1",
                                        "Vorzak", mname, age))
            else:
                blocks.append(make_lord(warr, "lord_G5_2", f"lord_G{n}_{slot}",
                                        "Krogar", mname, age))
        # females 7..10
        for slot in range(7, 11):
            fname = FEMALE_NAMES[ci * 4 + (slot - 7)]
            age = FEMALE_AGES[slot - 7]
            blocks.append(make_lord(fem, "lord_G5_9", f"lord_G{n}_{slot}",
                                    "Kralza", fname, age))
    new_block = "\n" + "\n".join(blocks)
    out = lords_text[:g5_10.end()] + new_block + lords_text[g5_10.end():]
    return out


def build_heroes(heroes_text):
    g5_10 = extract(heroes_text, r'\t<Hero\n\t\tid="lord_G5_10".*?/>')
    blocks = []
    for n in CLANS_NEW:
        clan_name = CLANS_NEW[n][0]
        blocks.append(f"\t<!-- Gundabad Clan {n} ({clan_name}) -->")
        for slot in range(1, 11):
            blocks.append(
                f'\t<Hero\n\t\tid="lord_G{n}_{slot}"\n'
                f'\t\tfaction="Faction.clan_gundabad_{n}" />'
            )
    new_block = "\n" + "\n".join(blocks)
    out = heroes_text[:g5_10.end()] + new_block + heroes_text[g5_10.end():]
    return out


def main():
    clans_text = CLANS.read_text(encoding="utf-8")
    if "clan_gundabad_6" in clans_text:
        print("clan_gundabad_6 already present - nothing to do (idempotent).")
        return
    lords_text = LORDS.read_text(encoding="utf-8")
    heroes_text = HEROES.read_text(encoding="utf-8")

    new_clans = build_clans(clans_text)
    new_lords = build_lords(lords_text)
    new_heroes = build_heroes(heroes_text)

    print(f"clans.xml : +{new_clans.count(chr(10)) - clans_text.count(chr(10))} lines, "
          f"{len(CLANS_NEW)} new Faction blocks")
    print(f"lords.xml : +{new_lords.count(chr(10)) - lords_text.count(chr(10))} lines, "
          f"{len(CLANS_NEW) * 10} new NPCCharacter blocks")
    print(f"heroes.xml: +{new_heroes.count(chr(10)) - heroes_text.count(chr(10))} lines, "
          f"{len(CLANS_NEW) * 10} new Hero blocks")

    if DRY:
        print("\n--dry-run: no files written. clan_gundabad_6 preview:\n")
        print(re.search(r"  <!-- Grishûk -->.*?/>", new_clans, re.DOTALL).group(0))
        return

    CLANS.write_text(new_clans, encoding="utf-8")
    LORDS.write_text(new_lords, encoding="utf-8")
    HEROES.write_text(new_heroes, encoding="utf-8")
    print("\nWrote clans.xml, lords.xml, heroes.xml.")


if __name__ == "__main__":
    main()
