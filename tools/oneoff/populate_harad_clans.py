#!/usr/bin/env python3
"""Populate Shaghâna + Âbanissa clans to 10 heroes each, Harad-named.

Both kingdoms split off from Harad and today have exactly ONE lord per clan
(the owner, lord_SH{k}_1 / lord_AB{k}_1). This brings every clan to 10 heroes:
the existing male owner + 5 new males (_2.._6) + 4 new females (_7.._10).

  Shaghâna: 9 clans  -> 9 x 9 = 81 new lords (Culture.shaghana, Cavalry)
  Âbanissa: 8 clans  -> 8 x 9 = 72 new lords (Culture.abanissa, Infantry)
  Total: 153 new lords + 153 Hero registrations.

Names are curated Harad/Southron (Perso-Babylonian) given names matching the
mod's existing Âbanissa/Shaghâna aesthetic (Phaxsharân, Kûmaraknis, ...).

The generator CLONES real lord blocks verbatim and substitutes
id/name/age/culture/group/equipment, so inserted XML is structurally identical
to its siblings (CRLF + indentation preserved). New lords are inserted right
after each clan's owner block (lords.xml) / owner Hero (heroes.xml).

Idempotent: re-running after lord_SH1_2 exists is a no-op.

Usage:  python tools/populate_harad_clans.py [--dry-run]
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MD = ROOT / "Main" / "_Module" / "ModuleData" / "characters"
LORDS = MD / "lords.xml"
HEROES = MD / "heroes.xml"

DRY = "--dry-run" in sys.argv

# Template lord ids cloned from the live file (extracted at runtime).
SH_MALE_TPL_ID, SH_MALE_TPL_NAME = "lord_SH1_1", "Zarkan"
AB_MALE_TPL_ID, AB_MALE_TPL_NAME = "lord_AB1_1", "Phaxar"
FEM_TPL_ID, FEM_TPL_NAME = "lord_3_13_1", "Sira"

KINGDOMS = [
    {"prefix": "SH", "clanid": "clan_shaghana", "culture": "shaghana",
     "clans": 9, "male_tpl": SH_MALE_TPL_ID, "male_name": SH_MALE_TPL_NAME,
     "fem_group": "Cavalry"},
    {"prefix": "AB", "clanid": "clan_abanissa", "culture": "abanissa",
     "clans": 8, "male_tpl": AB_MALE_TPL_ID, "male_name": AB_MALE_TPL_NAME,
     "fem_group": "Infantry"},
]

# Curated Harad/Southron male given names (Perso-Babylonian flavour). Need 85.
MALE_NAMES = [
    "Suladân", "Farzûk", "Azimûr", "Bahram", "Khorzan", "Rastûm", "Sohrak", "Kavûs",
    "Tahmûr", "Faramûz", "Goshtân", "Zahhûk", "Mardûk", "Sargûn", "Ashûr", "Nabûz",
    "Sennûr", "Tigharos", "Hammûr", "Kambûz", "Cyrûn", "Xerûk", "Dariûsh", "Behnûm",
    "Farzâd", "Hormûz", "Jahûn", "Kasrûn", "Manûchar", "Narîman", "Parvîz", "Qobâd",
    "Sâsan", "Shapûr", "Tûranos", "Varûz", "Yazdâr", "Zalûk", "Anûsh", "Bîjan",
    "Esfanos", "Gîvar", "Hûshang", "Irâj", "Jamshûd", "Karûn", "Lohrâs", "Mihrak",
    "Nûzar", "Ohrmûz", "Pirûn", "Rûzbeh", "Siyâk", "Tûsar", "Ardûsh", "Bahmûr",
    "Farrûk", "Garshûp", "Hûman", "Isfanar", "Kûrash", "Mazdûk", "Narseh", "Orodûn",
    "Pakûr", "Rashnûr", "Shahrûm", "Tirdûn", "Vologar", "Zamûsp", "Azarûn", "Borzûy",
    "Dânûsh", "Farûhar", "Gûdarz", "Hûtan", "Kûhram", "Mahûz", "Nastûr", "Pûlvand",
    "Sâveh", "Tûmar", "Vârazûn", "Zangûr", "Aspûr", "Bûzan", "Faryâd", "Gorûn",
    "Hazûr", "Khalûz",
]

# Curated Harad/Southron female given names. Need 68.
FEMALE_NAMES = [
    "Banûshad", "Roshanak", "Gordâfar", "Manîzheh", "Farangîs", "Katayûn", "Rûdabeh",
    "Sûdabeh", "Tahmîneh", "Sindûkht", "Shîrîn", "Azadeh", "Gûlnar", "Pûrandar",
    "Âzarmî", "Banûra", "Delâram", "Estharâ", "Faranak", "Gûlrukh", "Homâra", "Jârya",
    "Kûshyâra", "Lâleh", "Mahsâ", "Nâhid", "Parisâ", "Rûzâ", "Shahrnâz", "Tûraja",
    "Vâshti", "Yâsmin", "Zarrîn", "Anâhita", "Bahâra", "Cyrâ", "Dûria", "Farâh",
    "Gûlbahar", "Hûria", "Jahâna", "Kûrina", "Mihrî", "Narghîs", "Parvâneh", "Rûshanâ",
    "Sûsan", "Tûrana", "Vîda", "Yaldâ", "Zûleikha", "Âbâna", "Banîn", "Châhra",
    "Delkash", "Farzâneh", "Gûhara", "Hûma", "Jasmûna", "Kûlsûm", "Mehrî", "Nasrîn",
    "Pârmida", "Rûshan", "Saharâ", "Tûba", "Zhâleh", "Yâra",
]

EQUIP = ["a", "b", "c", "d", "e"]
MALE_AGES = [39, 31, 46, 27, 51]      # slots 2..6
FEMALE_AGES = [34, 24, 42, 29]        # slots 7..10

NEW_MALES = sum(k["clans"] for k in KINGDOMS) * 5    # 85
NEW_FEMALES = sum(k["clans"] for k in KINGDOMS) * 4  # 68
assert len(MALE_NAMES) >= NEW_MALES, (len(MALE_NAMES), NEW_MALES)
assert len(FEMALE_NAMES) >= NEW_FEMALES, (len(FEMALE_NAMES), NEW_FEMALES)
assert len(set(MALE_NAMES)) == len(MALE_NAMES), "dup male name"
assert len(set(FEMALE_NAMES)) == len(FEMALE_NAMES), "dup female name"


def extract_block(text, lord_id):
    m = re.search(r'    <NPCCharacter id="%s".*?</NPCCharacter>' % re.escape(lord_id),
                  text, re.DOTALL)
    if not m:
        raise SystemExit(f"Lord template not found: {lord_id}")
    return m.group(0)


def sub_age(block, age):
    return re.sub(r'age="[\d.]+"', f'age="{age}"', block, count=1)


def make_male(tpl, tpl_id, tpl_name, new_id, new_name, age, equip):
    b = tpl
    b = b.replace(tpl_id, new_id)                       # id + name key
    b = b.replace("}" + tpl_name + '"', "}" + new_name + '"')
    b = sub_age(b, age)
    b = b.replace("harad_bat_template_medium_a", f"harad_bat_template_medium_{equip}")
    b = b.replace("harad_civ_template_a", f"harad_civ_template_{equip}")
    return b


def make_female(tpl, new_id, new_name, age, culture, group, equip):
    b = tpl
    b = b.replace(FEM_TPL_ID, new_id)                   # id + name key
    b = b.replace("}" + FEM_TPL_NAME + '"', "}" + new_name + '"')
    b = b.replace('culture="Culture.aserai"', f'culture="Culture.{culture}"')
    b = b.replace('default_group="Cavalry"', f'default_group="{group}"')
    b = sub_age(b, age)
    b = b.replace("harad_bat_template_medium_b", f"harad_bat_template_medium_{equip}")
    b = b.replace("harad_civ_template_b", f"harad_civ_template_{equip}")
    return b


def main():
    lords = LORDS.read_text(encoding="utf-8")
    heroes = HEROES.read_text(encoding="utf-8")
    if "lord_SH1_2" in lords:
        print("lord_SH1_2 already present - nothing to do (idempotent).")
        return

    sh_male_tpl = extract_block(lords, SH_MALE_TPL_ID)
    ab_male_tpl = extract_block(lords, AB_MALE_TPL_ID)
    fem_tpl = extract_block(lords, FEM_TPL_ID)
    male_tpls = {"SH": (sh_male_tpl, SH_MALE_TPL_ID, SH_MALE_TPL_NAME),
                 "AB": (ab_male_tpl, AB_MALE_TPL_ID, AB_MALE_TPL_NAME)}

    mi = fi = 0
    new_lords = new_heroes = 0
    for kd in KINGDOMS:
        tpl, tpl_id, tpl_name = male_tpls[kd["prefix"]]
        for c in range(1, kd["clans"] + 1):
            owner = f"lord_{kd['prefix']}{c}_1"
            faction = f"Faction.{kd['clanid']}_{c}"
            lord_blocks = []
            hero_blocks = []
            # males _2.._6
            for slot in range(2, 7):
                lid = f"lord_{kd['prefix']}{c}_{slot}"
                name = MALE_NAMES[mi]; mi += 1
                age = MALE_AGES[slot - 2] + (c % 3)
                equip = EQUIP[slot % len(EQUIP)]
                lord_blocks.append(make_male(tpl, tpl_id, tpl_name, lid, name, age, equip))
                hero_blocks.append(f'\t<Hero\n\t\tid="{lid}"\n\t\tfaction="{faction}" />')
            # females _7.._10
            for slot in range(7, 11):
                lid = f"lord_{kd['prefix']}{c}_{slot}"
                name = FEMALE_NAMES[fi]; fi += 1
                age = FEMALE_AGES[slot - 7] + (c % 3)
                equip = EQUIP[slot % len(EQUIP)]
                lord_blocks.append(make_female(fem_tpl, lid, name, age,
                                               kd["culture"], kd["fem_group"], equip))
                hero_blocks.append(f'\t<Hero\n\t\tid="{lid}"\n\t\tfaction="{faction}" />')

            # Insert lords after the owner's NPCCharacter block.
            owner_block = extract_block(lords, owner)
            ins_lords = owner_block + "\n" + "\n".join(lord_blocks)
            assert lords.count(owner_block) == 1, owner
            lords = lords.replace(owner_block, ins_lords, 1)
            new_lords += len(lord_blocks)

            # Insert heroes after the owner's <Hero ...> block.
            owner_hero = re.search(
                r'\t<Hero\n\t\tid="%s"\n\t\tfaction="%s" />' % (re.escape(owner), re.escape(faction)),
                heroes)
            if not owner_hero:
                raise SystemExit(f"Owner hero not found: {owner}")
            oh = owner_hero.group(0)
            ins_heroes = oh + "\n" + "\n".join(hero_blocks)
            assert heroes.count(oh) == 1, owner
            heroes = heroes.replace(oh, ins_heroes, 1)
            new_heroes += len(hero_blocks)

    print(f"lords.xml : +{new_lords} NPCCharacter blocks ({mi} male, {fi} female)")
    print(f"heroes.xml: +{new_heroes} Hero blocks")

    if DRY:
        print("\n--dry-run: no files written. lord_SH1_2 + lord_SH1_7 preview:\n")
        print(re.search(r'    <NPCCharacter id="lord_SH1_2".*?</NPCCharacter>', lords, re.DOTALL).group(0))
        print()
        print(re.search(r'    <NPCCharacter id="lord_SH1_7".*?</NPCCharacter>', lords, re.DOTALL).group(0))
        return

    LORDS.write_text(lords, encoding="utf-8")
    HEROES.write_text(heroes, encoding="utf-8")
    print("\nWrote lords.xml, heroes.xml.")


if __name__ == "__main__":
    main()
