#!/usr/bin/env python3
"""Generate kingdoms + clans + lords + heroes for the 3 new factions and insert them
into taom_spkingdoms.xml / characters/clans.xml / characters/lords.xml / characters/heroes.xml.

Templates are read by id from the live files (gundabad for the two orc kingdoms, rivendell
for Lindon) and parametrized — no hand-copied XML. Idempotent via TAOM-NEWFACTIONS markers.

Run: python tools/generate_new_faction_kingdoms.py
"""
import os, re, json
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MD = os.path.join(ROOT, "Main", "_Module", "ModuleData")
CH = os.path.join(MD, "characters")

# ---------- design data ----------
KINGDOMS = {
    "goblin": dict(
        region="GB", culture="goblin", race="goblin", capital="town_GT1", tmpl="gundabad",
        equip="goblin", side="evil", color="0xFF1C2A1C", color2="0xFF44582E",
        name="Goblins", short="Goblins", title="Goblin-Realm of the High Pass", ruler="Great Goblin",
        desc="The Goblins of the High Pass swarm the tunnels beneath the Misty Mountains, the spawn of the host the Great Goblin once led against dwarf and wayfarer alike. Numberless and vicious, they pour from Goblin-town to raid the mountain roads.",
        lord_skill=("taom_orc_chieftain_skills", "taom_orc_warrior_skills", "taom_orc_female_skills"),
        lords_per_clan=8,  # numerous goblins: 5 clans x 8 = 40 lords (6 male + 2 female each)
        clans=[("clan_goblin_1", 6, "town_GT1", "Skrithak"),
               ("clan_goblin_2", 5, "town_GT1", "Yaghûl"),
               ("clan_goblin_3", 4, "town_GT1", "Gorkil"),
               ("clan_goblin_4", 4, "town_GT1", "Snaga-host"),
               ("clan_goblin_5", 3, "town_GT1", "Mughrat")],
    ),
    "mistymountainorcs": dict(
        region="MM", culture="mistymountainorcs", race="orc", capital="town_MM1", tmpl="gundabad",
        equip="mistymountainorcs", side="evil", color="0xFF2E2E2E", color2="0xFF6E5A40",
        name="Misty Mountain Orcs", short="Misty Orcs", title="Orc-Host of the Misty Mountains", ruler="Warlord",
        desc="The orc-host of the Misty Mountains holds the high places and deep delvings from Mount Gram to the gates of Moria. Bred for war in the cold peaks, they answer the call of the Shadow and hunger to reclaim the mountains for their own.",
        lord_skill=("taom_orc_chieftain_skills", "taom_orc_warrior_skills", "taom_orc_female_skills"),
        lords_per_clan=8,  # 5 clans x 8 = 40 lords (6 male + 2 female each)
        clans=[("clan_mistymountainorcs_1", 6, "town_MM1", "Bûrzghâsh"),
               ("clan_mistymountainorcs_2", 5, "town_MM2", "Krimpâsh"),
               ("clan_mistymountainorcs_3", 5, "town_MM3", "Dushnakh"),
               ("clan_mistymountainorcs_4", 4, "castle_MM4", "Morgrim"),
               ("clan_mistymountainorcs_5", 4, "castle_MM6", "Vargrim")],
    ),
    "bluecraig": dict(
        region="BC", culture="goblin", race="goblin", capital="town_GBC1", tmpl="gundabad",
        equip="goblin", side="evil", color="0xFF20303A", color2="0xFF4A6478",
        name="Goblins of Blue Craig", short="Blue Craig", title="Goblin-Realm of Blue Craig", ruler="Great Goblin",
        desc="The goblin-warrens of Blue Craig in the Blue Mountains (Ered Luin) of the far west, hard by the Grey Havens of Lindon. A distinct, numberless host of goblin-kind — separate from their Misty Mountains cousins of Goblin Town — they swarm the western crags and trouble the Elves of the western shore.",
        lord_skill=("taom_orc_chieftain_skills", "taom_orc_warrior_skills", "taom_orc_female_skills"),
        lords_per_clan=8,  # second goblin kingdom: 5 clans x 8 = 40 lords (6 male + 2 female each)
        clans=[("clan_bluecraig_1", 6, "town_GBC1", "Blue Craig Warband"),
               ("clan_bluecraig_2", 5, "town_GBC1", "Crag-spawn"),
               ("clan_bluecraig_3", 4, "town_GBC1", "Lune-skulkers"),
               ("clan_bluecraig_4", 4, "town_GBC1", "Ered Luin Raiders"),
               ("clan_bluecraig_5", 3, "town_GBC1", "Mirkstone Tribe")],
    ),
    "lindon": dict(
        region="LN", culture="rivendell", race="elf", capital="town_LN1", tmpl="rivendell",
        equip="rivendell", side="free", color="0xFF50A090", color2="0xFFC8E0D8",
        name="Lindon", short="Lindon", title="High Kingdom of Lindon", ruler="Lord of the Havens",
        desc="Lindon, the green land west of the Blue Mountains, is the last realm of the High Elves upon the shores of Middle-earth. From the Grey Havens of Mithlond the white ships sail into the West, while Círdan the Shipwright keeps watch over the fading light of the Eldar.",
        lord_skill=("taom_elf_king_skills", "taom_elf_warrior_skills", "taom_elf_lady_skills"),
        clans=[("clan_lindon_1", 6, "town_LN1", "Falathrim"), ("clan_lindon_2", 4, "town_LN1", "Edhil Mithlond")],
    ),
}
# Leader (clan-1 lord-1) descriptions, period-correct for TA 3018 (War of the Ring).
LEADER_BLURB = {
    "goblin": "the Great Goblin of Goblin Town, master of the warrens of the High Pass.",
    "mistymountainorcs": "Warlord of the Misty Mountains, who holds the deep places of Moria and the cold peaks above.",
    "bluecraig": "the Great Goblin of Blue Craig, whose swarms infest the Blue Mountains of the far west, hard by the Grey Havens of Lindon.",
    "lindon": "the Shipwright, Lord of the Grey Havens and eldest of the Eldar in Middle-earth, who kept the Ring Narya until he gave it to Mithrandir.",
}
# lords per clan: ruling clan (first) 6, vassals 4
LORDS_RULING, LORDS_VASSAL = 6, 4

ORC_M = ["Grishnák", "Muzgash", "Lugdush", "Radbug", "Gorbag", "Shagrat", "Ufthak", "Lagduf", "Orcobal",
         "Yazneg", "Narzug", "Grukhash", "Mauhur", "Uglûk", "Bûrzum", "Throkmaw", "Skarsnik", "Gnashrak",
         "Vrakthar", "Dushgar", "Krimpük", "Gashnar", "Morgrub", "Snagbut", "Hrakdush", "Boldog", "Lugbol",
         "Grizznak", "Frakû", "Mughash"]
ORC_F = ["Gralza", "Shelza", "Ulga", "Brakza", "Gnarla", "Vugza", "Skralga", "Hagza", "Mogra", "Urshza", "Lazgha", "Burza"]
# Third-Age (TA 3018) Lindon / Grey Havens elves: Círdan + Galdor (Council of Elrond) + Gildor
# Inglorion (a wandering Noldo of FotR) + generic Sindarin mariner-names. No First-Age figures.
ELF_M = ["Galdor", "Gildor", "Aearion", "Falphen", "Lindael", "Mírdan", "Thalion", "Calemir",
         "Nárdir", "Lhúnedhel", "Faelon", "Aeglir", "Celebrin", "Edronil", "Mithlas", "Orvael"]
ELF_F = ["Nimwen", "Lindis", "Galadwen", "Faelwen", "Nárwen", "Lhúneth", "Celebriel", "Mírwen", "Aewen", "Eälinde"]
ELF_LEADER = "Círdan"


def get_block(text, pattern):
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise RuntimeError("not found: " + pattern[:70])
    return m.group(0)


def upsert(text, close_tag, payload, marker):
    text = re.sub(r'[ \t]*<!-- ' + re.escape(marker) + r':BEGIN -->.*?<!-- ' + re.escape(marker) + r':END -->\n',
                  '', text, flags=re.DOTALL)
    idx = text.rfind(close_tag)
    block = f"  <!-- {marker}:BEGIN -->\n{payload}  <!-- {marker}:END -->\n"
    return text[:idx] + block + text[idx:]


def main():
    spk = open(os.path.join(MD, "taom_spkingdoms.xml"), encoding="utf-8").read()
    clans = open(os.path.join(CH, "clans.xml"), encoding="utf-8").read()
    lords = open(os.path.join(CH, "lords.xml"), encoding="utf-8").read()
    heroes = open(os.path.join(CH, "heroes.xml"), encoding="utf-8").read()

    # templates
    k_gund = get_block(spk, r'<Kingdom\b[^>]*\bid="gundabad".*?</Kingdom>')
    k_riven = get_block(spk, r'<Kingdom\b[^>]*\bid="rivendell".*?</Kingdom>')
    clan_gund = get_block(clans, r'<Faction\b\s*\n\s*id="clan_gundabad_1".*?/>')
    lord_orc_m = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_G1_1".*?</NPCCharacter>')
    lord_orc_f = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_G1_9".*?</NPCCharacter>')
    lord_elf_m = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_R1_1".*?</NPCCharacter>')
    lord_elf_f = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_R1_2".*?</NPCCharacter>')

    new_kingdoms, new_clans, new_lords, new_heroes = {}, {}, {}, {}

    sib = list(KINGDOMS.keys())
    for kid, k in KINGDOMS.items():
        tmpl = k_gund if k["tmpl"] == "gundabad" else k_riven
        blk = tmpl
        # opening-tag attribute rewrites (targeted, leave banner_key/relationships/policies)
        blk = re.sub(r'\bid="(gundabad|rivendell)"', f'id="{kid}"', blk, count=1)
        blk = re.sub(r'owner="Hero\.lord_[A-Z0-9]+_1"', f'owner="Hero.lord_{k["region"]}1_1"', blk, count=1)
        blk = re.sub(r'initial_home_settlement="Settlement\.[a-zA-Z0-9_]+"',
                     f'initial_home_settlement="Settlement.{k["capital"]}"', blk, count=1)
        blk = re.sub(r'\bculture="Culture\.[a-z]+"', f'culture="Culture.{k["culture"]}"', blk, count=1)
        blk = re.sub(r'\bcolor="0x[0-9A-Fa-f]+"', f'color="{k["color"]}"', blk, count=1)
        blk = re.sub(r'\bcolor2="0x[0-9A-Fa-f]+"', f'color2="{k["color2"]}"', blk, count=1)
        blk = re.sub(r'primary_banner_color="0x[0-9A-Fa-f]+"', f'primary_banner_color="{k["color"]}"', blk, count=1)
        blk = re.sub(r'secondary_banner_color="0x[0-9A-Fa-f]+"', f'secondary_banner_color="{k["color2"]}"', blk, count=1)
        blk = re.sub(r'name="\{=[^}]*\}[^"]*"', f'name="{{=taom_{kid}_name}}{k["name"]}"', blk, count=1)
        blk = re.sub(r'short_name="\{=[^}]*\}[^"]*"', f'short_name="{{=taom_{kid}_short_name}}{k["short"]}"', blk, count=1)
        blk = re.sub(r'title="\{=[^}]*\}[^"]*"', f'title="{{=taom_{kid}_title}}{k["title"]}"', blk, count=1)
        blk = re.sub(r'ruler_title="\{=[^}]*\}[^"]*"', f'ruler_title="{{=taom_{kid}_ruler_title}}{k["ruler"]}"', blk, count=1)
        blk = re.sub(r'text="\{=[^}]*\}[^"]*"', f'text="{{=taom_{kid}_desc}}{k["desc"]}"', blk, count=1)
        # sibling relationships
        rels = ""
        for other in sib:
            if other == kid:
                continue
            same_side = KINGDOMS[other]["side"] == k["side"]
            val = "1" if same_side else "0"
            rels += (f'            <relationship\n                kingdom="Kingdom.{other}"\n'
                     f'                value="{val}"\n                isAtWar="false" />\n')
        blk = blk.replace("</relationships>", rels + "        </relationships>", 1)
        new_kingdoms[kid] = "    " + blk + "\n"

        # ---- clans ----
        for ci, (clan_id, tier, home, clan_name) in enumerate(k["clans"]):
            c = clan_gund
            c = re.sub(r'id="clan_gundabad_1"', f'id="{clan_id}"', c)
            c = re.sub(r'initial_home_settlement="Settlement\.[a-zA-Z0-9_]+"',
                       f'initial_home_settlement="Settlement.{home}"', c)
            c = re.sub(r'name="\{=[^}]*\}[^"]*"', f'name="{{=aom_{clan_id}_name}}{clan_name}"', c)
            c = re.sub(r'tier="\d+"', f'tier="{tier}"', c)
            c = re.sub(r'owner="Hero\.lord_[A-Z0-9]+_1"', f'owner="Hero.lord_{k["region"]}{ci+1}_1"', c)
            c = re.sub(r'culture="Culture\.[a-z]+"', f'culture="Culture.{k["culture"]}"', c)
            c = re.sub(r'super_faction="Kingdom\.[a-z]+"', f'super_faction="Kingdom.{kid}"', c)
            new_clans.setdefault(kid, "")
            new_clans[kid] += "  " + c + "\n"

            # ---- lords + heroes for this clan ----
            lpc = k.get("lords_per_clan")
            nlords = lpc if lpc else (LORDS_RULING if ci == 0 else LORDS_VASSAL)
            n_female = 2 if lpc else (2 if ci == 0 else 1)  # >=2 females per clan on the big orc hosts
            # within-clan spouse pairs so the females bear children (orc precedent: Gundabad _1_8<->_1_9)
            spouse = {}
            for f_li in range(nlords - n_female + 1, nlords + 1):
                m_li = f_li - n_female
                if m_li >= 1:
                    spouse[f_li] = m_li
                    spouse[m_li] = f_li
            for li in range(1, nlords + 1):
                lord_id = f"lord_{k['region']}{ci+1}_{li}"
                female = li > nlords - n_female  # last n_female per clan are female (male-dominant)
                is_elf = k["tmpl"] == "rivendell"
                tmpl_l = (lord_elf_f if female else lord_elf_m) if is_elf else (lord_orc_f if female else lord_orc_m)
                # skill template
                if ci == 0 and li == 1:
                    skill = k["lord_skill"][0]
                elif female:
                    skill = k["lord_skill"][2]
                else:
                    skill = k["lord_skill"][1]
                # name
                if ci == 0 and li == 1 and is_elf:
                    nm = ELF_LEADER
                else:
                    pool = (ELF_F if female else ELF_M) if is_elf else (ORC_F if female else ORC_M)
                    seed = {"goblin": 0, "mistymountainorcs": 13, "lindon": 5, "bluecraig": 21}[kid]
                    nm = pool[(seed + ci * 5 + li) % len(pool)]
                letter = "abcde"[(li - 1) % 5]
                age = (22 + (li % 6)) if female else (30 + (li * 3) % 16)  # females childbearing-age
                L = tmpl_l
                L = re.sub(r'\bid="lord_[A-Z0-9_]+"', f'id="{lord_id}"', L, count=1)
                L = re.sub(r'name="\{=[^}]*\}[^"]*"', f'name="{{=aom_{lord_id}_name}}{nm}"', L, count=1)
                L = re.sub(r'\bage="[0-9.]+"', f'age="{age}"', L, count=1)
                L = re.sub(r'\bculture="Culture\.[a-z]+"', f'culture="Culture.{k["culture"]}"', L, count=1)
                L = re.sub(r'skill_template="SkillSet\.[a-zA-Z0-9_]+"', f'skill_template="SkillSet.{skill}"', L, count=1)
                if not is_elf:  # orc race swap (elf template already race="elf")
                    L = re.sub(r'\brace="pale_uruk"', f'race="{k["race"]}"', L, count=1)
                # equipment rosters -> this faction's prefix + letter
                L = re.sub(r'id="[a-z]+_bat_template_medium_[a-e]"', f'id="{k["equip"]}_bat_template_medium_{letter}"', L)
                L = re.sub(r'id="[a-z]+_civ_template_default_[a-e]"', f'id="{k["equip"]}_civ_template_default_{letter}"', L)
                new_lords.setdefault(kid, "")
                new_lords[kid] += "    " + L + "\n"
                # hero row
                text_attr = ""
                if ci == 0 and li == 1:
                    text_attr = f'\n\t\ttext="{{={lord_id}_description}}{nm}, {LEADER_BLURB[kid]}"'
                spouse_attr = ""
                if li in spouse:
                    spouse_attr = f'\n\t\tspouse="Hero.lord_{k["region"]}{ci+1}_{spouse[li]}"'
                hero = (f'\t<Hero\n\t\tid="{lord_id}"\n\t\tfaction="Faction.{clan_id}"{spouse_attr}{text_attr} />\n')
                new_heroes.setdefault(kid, "")
                new_heroes[kid] += hero

    # insert all (idempotent), one marker per kingdom per file
    for kid in KINGDOMS:
        spk = upsert(spk, "</Kingdoms>", new_kingdoms[kid], f"TAOM-NEWFACTIONS:{kid}")
        clans = upsert(clans, "</Factions>", new_clans[kid], f"TAOM-NEWFACTIONS:{kid}")
        lords = upsert(lords, "</NPCCharacters>", new_lords[kid], f"TAOM-NEWFACTIONS:{kid}")
        heroes = upsert(heroes, "</Heroes>", new_heroes[kid], f"TAOM-NEWFACTIONS:{kid}")

    for path, content, label in [(os.path.join(MD, "taom_spkingdoms.xml"), spk, "taom_spkingdoms.xml"),
                                 (os.path.join(CH, "clans.xml"), clans, "clans.xml"),
                                 (os.path.join(CH, "lords.xml"), lords, "lords.xml"),
                                 (os.path.join(CH, "heroes.xml"), heroes, "heroes.xml")]:
        open(path, "w", encoding="utf-8").write(content)
        ET.parse(path)
        print(f"  inserted + well-formed: {label}")

    nl = sum(len(re.findall(r'<NPCCharacter\b', v)) for v in new_lords.values())
    nh = sum(v.count("<Hero") for v in new_heroes.values())
    nc = sum(v.count("<Faction") for v in new_clans.values())
    print(f"\n  kingdoms={len(KINGDOMS)}  clans={nc}  lords={nl}  heroes={nh}")
    print("Done.")


if __name__ == "__main__":
    main()
