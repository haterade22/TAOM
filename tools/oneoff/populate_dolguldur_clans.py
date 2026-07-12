#!/usr/bin/env python3
"""One-off: add 9 new clans (clan_dolguldur_7..15) to the Dol Guldur kingdom.

Dol Guldur shipped with 6 clans; the user wants 15, with 5-10 lords per new
clan, all the dg_uruk race. Each new clan:
  * carries the Dol Guldur KINGDOM banner key (user choice),
  * is homed at Settlement.town_DG1 (matches existing Dol Guldur clans),
  * gets a varied 5-10 orc lords (owner + warriors + ~40% females).

The generator CLONES the real clan_dolguldur_5 / lord_D2_1 / lord_D1_9 blocks
verbatim and substitutes ids/names/colors/equipment, so inserted XML is
structurally identical to its hand-authored siblings.

Idempotent: re-running after clan_dolguldur_7 exists is a no-op.

Usage:  python tools/populate_dolguldur_clans.py [--dry-run]
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

KINGDOM_BANNER = "11.149.166.1528.1528.764.764.1.0.0.18002.2002.2000.520.520.765.765.0.0.0"
GENERIC_PARTY_TEMPLATE = "PartyTemplate.kingdom_hero_party_dolguldur_template"

# Templates cloned from the live file.
CLAN_TPL_ID, CLAN_TPL_NAME, CLAN_TPL_OWNER = "clan_dolguldur_5", "Krâzgoth", "lord_D5_1"
CLAN_TPL_COLOR, CLAN_TPL_COLOR2 = "FF483E2A", "FF6F8845"
CLAN_TPL_PARTY = "PartyTemplate.kingdom_hero_party_dolguldur_dolguldur_5_template"
CLAN_TPL_BANNER = "11.2001.2001.1528.1528.764.764.1.0.0.19014.212.171.700.700.764.764.0.0.0"
MALE_TPL_ID, MALE_TPL_NAME = "lord_D2_1", "Narzugh"      # chieftain skills
FEM_TPL_ID, FEM_TPL_NAME = "lord_D1_9", "Thrulza"

# clan number -> (orc clan name, tier, color, color2)
CLANS_NEW = {
    7:  ("Ghâshrûz",  4, "FF5A5234", "FF6D9C4F"),
    8:  ("Morbhak",   3, "FF635B39", "FF6BA654"),
    9:  ("Skûlgath",  4, "FF6C633E", "FF69B059"),
    10: ("Drûzgûl",   3, "FF756B43", "FF67BA5E"),
    11: ("Vrûkmog",   3, "FF7E7348", "FF65C463"),
    12: ("Hlakûrz",   3, "FF877B4D", "FF63CE68"),
    13: ("Throzbûz",  4, "FF4E4530", "FF5F8A48"),
    14: ("Rhâzgor",   3, "FF565030", "FF659050"),
    15: ("Uzghûl",    3, "FF5E5634", "FF6A9858"),
}
# per clan: total lords (5-10). owner _1 male; ~40% female.
CLAN_SIZE = {7: 10, 8: 9, 9: 8, 10: 7, 11: 6, 12: 5, 13: 9, 14: 7, 15: 6}


def females_for(k):
    return {10: 4, 9: 4, 8: 3, 7: 3, 6: 2, 5: 2}[k]


# Dark Dol-Guldur orc names, distinct from the Gundabad set + existing DG lords.
MALE_NAMES = [
    "Lugmor", "Vrûkash", "Druzgûl", "Throgma", "Bûrzghash", "Ghâshnak", "Skûlgar",
    "Morbhûl", "Rhâzgar", "Uzgûth", "Mauthak", "Naltûr", "Grishûl", "Hlakûm", "Zûrghan",
    "Dûlmash", "Vrâkmug", "Shâgthul", "Gûrnak", "Brôzag", "Thûrzog", "Mokrash", "Azgûrth",
    "Hûzmog", "Skarvûk", "Lûghnar", "Vorgûl", "Dramthak", "Ghûlbag", "Rûkmash", "Throzgûr",
    "Bûlzag", "Nûrghak", "Slagthûm", "Hrûkdan", "Mazgûth", "Olghûr", "Gûthrak", "Zhâgmor",
    "Drûbnash", "Vûlkrash", "Bhargûz", "Snûrthak", "Mogthûl", "Râkzûr", "Ghâznak", "Lûthmar",
    "Throgbûz", "Urzghûl", "Dûshrak",
]
FEMALE_NAMES = [
    "Mwhazga", "Skûrla", "Ghûrnia", "Vrûzga", "Naghza", "Dûrnia", "Lûrgha", "Thûzga",
    "Morgza", "Skâlna", "Brûzha", "Ghâzla", "Uzrina", "Hlûrza", "Vûlna", "Drâzga", "Nûrgha",
    "Skûzla", "Mhargza", "Ghûlna", "Râzgha", "Bûlzna", "Throzga", "Uzghla", "Lûkzha",
    "Vrâgna", "Dhûrza", "Skrûnia", "Mogzla", "Ghûznia", "Nûzgha", "Brâklza", "Vûrgha",
    "Hûzla", "Drûnza",
]
EQUIP = ["a", "b", "c", "d", "e"]
MALE_AGES = [44, 33, 28, 39, 30, 26]      # slot 1 (owner) oldest
FEMALE_AGES = [25, 31, 22, 35]

TOTAL = sum(CLAN_SIZE.values())
NEED_M = sum(k - females_for(k) for k in CLAN_SIZE.values())
NEED_F = sum(females_for(k) for k in CLAN_SIZE.values())
assert len(MALE_NAMES) >= NEED_M, (len(MALE_NAMES), NEED_M)
assert len(FEMALE_NAMES) >= NEED_F, (len(FEMALE_NAMES), NEED_F)
assert len(set(MALE_NAMES)) == len(MALE_NAMES)
assert len(set(FEMALE_NAMES)) == len(FEMALE_NAMES)


def extract(text, pattern):
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise SystemExit(f"Template not found: {pattern!r}")
    return m


def sub_age(block, age):
    return re.sub(r'age="[\d.]+"', f'age="{age}"', block, count=1)


def build_clan(tpl, n, name, tier, color, color2):
    b = tpl
    b = b.replace(CLAN_TPL_ID, f"clan_dolguldur_{n}")          # id + name key
    b = b.replace(CLAN_TPL_OWNER, f"lord_D{n}_1")              # owner
    b = b.replace(CLAN_TPL_NAME, name)                        # comment + name value
    b = re.sub(r'tier="\d+"', f'tier="{tier}"', b, count=1)
    b = b.replace(f'color="{CLAN_TPL_COLOR}"', f'color="{color}"')
    b = b.replace(f'color2="{CLAN_TPL_COLOR2}"', f'color2="{color2}"')
    b = b.replace(CLAN_TPL_PARTY, GENERIC_PARTY_TEMPLATE)
    b = b.replace(CLAN_TPL_BANNER, KINGDOM_BANNER)
    return b


def build_male(tpl, new_id, new_name, age, equip, is_owner):
    b = tpl
    b = b.replace(MALE_TPL_ID, new_id)                        # id + name key
    b = b.replace("}" + MALE_TPL_NAME + '"', "}" + new_name + '"')
    b = sub_age(b, age)
    b = b.replace("dolguldur_bat_template_medium_e", f"dolguldur_bat_template_medium_{equip}")
    b = b.replace("dolguldur_civ_template_default_e", f"dolguldur_civ_template_default_{equip}")
    if not is_owner:
        b = b.replace("SkillSet.taom_orc_chieftain_skills", "SkillSet.taom_orc_warrior_skills")
    return b


def build_female(tpl, new_id, new_name, age, equip):
    b = tpl
    b = b.replace(FEM_TPL_ID, new_id)                         # id + name key
    b = b.replace("}" + FEM_TPL_NAME + '"', "}" + new_name + '"')
    b = sub_age(b, age)
    b = b.replace("dolguldur_bat_template_medium_c", f"dolguldur_bat_template_medium_{equip}")
    b = b.replace("dolguldur_civ_template_default_c", f"dolguldur_civ_template_default_{equip}")
    return b


def main():
    clans = CLANS.read_text(encoding="utf-8")
    if "clan_dolguldur_7" in clans:
        print("clan_dolguldur_7 already present - nothing to do (idempotent).")
        return
    lords = LORDS.read_text(encoding="utf-8")
    heroes = HEROES.read_text(encoding="utf-8")

    clan_tpl = extract(clans, r'  <!-- %s -->\s*<Faction\s+id="%s".*?/>'
                       % (re.escape(CLAN_TPL_NAME), CLAN_TPL_ID)).group(0)
    # Insert NEW clans after clan_dolguldur_6 (the last DG clan), before the bandit block.
    clan6 = extract(clans, r'<Faction\s+id="clan_dolguldur_6".*?/>')
    male_tpl = extract(lords, r'    <NPCCharacter id="%s".*?</NPCCharacter>' % MALE_TPL_ID).group(0)
    fem_tpl = extract(lords, r'    <NPCCharacter id="%s".*?</NPCCharacter>' % FEM_TPL_ID).group(0)
    d6_lord = extract(lords, r'    <NPCCharacter id="lord_D6_10".*?</NPCCharacter>')
    d6_hero = extract(heroes, r'\t<Hero\n\t\tid="lord_D6_10".*?/>')

    clan_blocks, lord_blocks, hero_blocks = [], [], []
    mi = fi = 0
    for n in sorted(CLANS_NEW):
        name, tier, color, color2 = CLANS_NEW[n]
        K = CLAN_SIZE[n]
        nfem = females_for(K)
        nmale = K - nfem
        clan_blocks.append(build_clan(clan_tpl, n, name, tier, color, color2))
        lord_blocks.append(f"    <!-- Dol Guldur Clan {n} ({name}) -->")
        hero_blocks.append(f"\t<!-- Dol Guldur Clan {n} ({name}) -->")
        for slot in range(1, K + 1):
            lid = f"lord_D{n}_{slot}"
            equip = EQUIP[slot % len(EQUIP)]
            if slot <= nmale:                       # males (slot 1 = owner/chieftain)
                nm = MALE_NAMES[mi]; mi += 1
                age = MALE_AGES[(slot - 1) % len(MALE_AGES)] + (n % 3)
                lord_blocks.append(build_male(male_tpl, lid, nm, age, equip, is_owner=(slot == 1)))
            else:                                   # females
                nm = FEMALE_NAMES[fi]; fi += 1
                age = FEMALE_AGES[(slot - nmale - 1) % len(FEMALE_AGES)] + (n % 3)
                lord_blocks.append(build_female(fem_tpl, lid, nm, age, equip))
            hero_blocks.append(f'\t<Hero\n\t\tid="{lid}"\n\t\tfaction="Faction.clan_dolguldur_{n}" />')

    new_clans = "\n\n" + "\n\n".join(clan_blocks)
    new_lords = "\n" + "\n".join(lord_blocks)
    new_heroes = "\n" + "\n".join(hero_blocks)

    clans = clans[:clan6.end()] + new_clans + clans[clan6.end():]
    lords = lords[:d6_lord.end()] + new_lords + lords[d6_lord.end():]
    heroes = heroes[:d6_hero.end()] + new_heroes + heroes[d6_hero.end():]

    print(f"clans.xml : +{len(clan_blocks)} Faction blocks (clan_dolguldur_7..15)")
    print(f"lords.xml : +{mi + fi} NPCCharacter blocks ({mi} male, {fi} female), total target {TOTAL}")
    print(f"heroes.xml: +{mi + fi} Hero blocks")

    if DRY:
        print("\n--dry-run: no files written. clan_dolguldur_7 + lord_D7_1 + first female preview:\n")
        print(re.search(r'  <!-- Ghâshrûz -->.*?/>', clans, re.DOTALL).group(0))
        print()
        print(re.search(r'    <NPCCharacter id="lord_D7_1".*?</NPCCharacter>', lords, re.DOTALL).group(0)[:400])
        return

    CLANS.write_text(clans, encoding="utf-8")
    LORDS.write_text(lords, encoding="utf-8")
    HEROES.write_text(heroes, encoding="utf-8")
    print("\nWrote clans.xml, lords.xml, heroes.xml.")


if __name__ == "__main__":
    main()
