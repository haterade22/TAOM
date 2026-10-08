#!/usr/bin/env python3
"""Generate kingdoms + clans + lords + heroes for the 3 new factions and insert them
into taom_spkingdoms.xml / characters/clans.xml / characters/lords.xml / characters/heroes.xml.

Templates are read by id from the live files (gundabad for the two orc kingdoms, rivendell
for Lindon) and parametrized — no hand-copied XML. Idempotent via TAOM-NEWFACTIONS markers.

Run: python tools/generate_new_faction_kingdoms.py --only arthedain [--check]
"""
import os, re, json, sys
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
    # Arthedain (2026-10-07, Mike): the what-if North-kingdom where Arvedui's line survived, on its
    # own culture (tools/promote_borrowed_cultures.py + tools/generate_arthedain_troops.py). Weak in
    # numbers by design: the ruling clan is tier 4 and two vassals tier 2, which caps the realm at
    # about 8 lord parties (DefaultClanTierModel.GetPartyLimitForTier: <3 -> 1, <5 -> 2). The keys
    # below `clans` are opt-in and only this entry uses them.
    "arthedain": dict(
        region="AN", culture="arthedain", race=None, capital="town_AN2", tmpl="rivendell", lords="human",
        equip="arthedain", side="free", color="0xFF4870B8", color2="0xFFC8CCD8",
        name="Kingdom of Arthedain", short="Arthedain", title="North-Kingdom of Arthedain", ruler="King",
        desc="The last of the three realms into which Arnor was sundered, Arthedain did not fall. Arvedui's line still holds Fornost upon the North Downs, though its lords are few, its towns poor, and Angmar's shadow lies always on the northern hills.",
        # The templates' own skill sets: lords.xml rows carry inline skills equal to their template
        # (SKILL_TEMPLATE_MISMATCH), so a clone keeps its source's set until /lord-skills retunes it.
        lord_skill=("taom_gondor_knight_skills", "taom_gondor_knight_skills", "taom_gondor_young_lady_skills"),
        lords_per_clan=4,  # 5 clans x 4 = 20 lords (2 male + 2 female, married within the clan)
        clans=[("clan_arthedain_1", 4, "town_AN2", "House of Arvedui"),
               ("clan_arthedain_2", 3, "town_AN1", "House of Evendim"),
               ("clan_arthedain_3", 3, "town_AN3", "Wardens of Amon Sûl"),
               ("clan_arthedain_4", 2, "castle_AN5", "House of the Baranduin"),
               ("clan_arthedain_5", 2, "castle_AN8", "House of Emyn Beraid")],
        leader_name="Arvegil II",
        male_names=["Malvegil", "Amlaith", "Celepharn", "Argeleb", "Araphor", "Halbarad", "Aranuir",
                    "Celebrindor", "Arveleg", "Dírhael"],
        female_names=["Gilraen", "Ivorwen", "Fíriel", "Elanwen", "Meneliel", "Silivren", "Ithilwen",
                      "Calwen", "Maethil", "Tirwen"],
        party_template="PartyTemplate.kingdom_hero_party_arthedain_template",
        # Arnor icon group 110 (Main/_Module/ModuleData/banner_icons.xml) in silver (palette 110) on
        # Arnor blue (palette 190); one icon per clan so no two clans fly the same banner.
        banner="11.190.190.1528.1528.764.764.1.0.0.{icon}.110.110.700.700.764.764.0.0.0",
        kingdom_icon=11000, clan_icons=[11000, 11001, 11002, 11003, 11004],
        clan_colors=[("FF4870B8", "FFC8CCD8"), ("FF3A6298", "FFC8CCD8"), ("FF5960A8", "FFD2D6D5"),
                     ("FF2E4A78", "FFCACCCB"), ("FF4E75BC", "FFEAEEEF")],
        # Day-one stances (Mike approved the diplomacy). value < 0 or isAtWar declares war at load
        # (Kingdom.Deserialize, Kingdom.cs:780-790); the rest of the stance lives in diplomacy.json.
        relationships={"gundabad": (-1, True), "empire": (0, False), "isengard": (0, False),
                       "rivendell": (1, False), "lindon": (1, False), "empire_w": (1, False)},
        # Each lord takes the <face> of a different shipped lord of this culture and sex, in file
        # order, instead of the template's: otherwise all ten men share one face and all ten women
        # another (Lindon's generated lords do).
        faces_from="gondor",
        # The ruler alone wears the Arnor crown (Mike, 2026-10-07): a direct Head override under his
        # <Equipments>, laid over that slot in each of his sets.
        leader_head="sk_ar_art_crown_king_a",
    ),
}
# The realms a regeneration reproduces (main() --only). See the note there.
REGENERATABLE = ("arthedain",)
# Leader (clan-1 lord-1) descriptions, period-correct for TA 3018 (War of the Ring).
LEADER_BLURB = {
    "goblin": "the Great Goblin of Goblin Town, master of the warrens of the High Pass.",
    "mistymountainorcs": "Warlord of the Misty Mountains, who holds the deep places of Moria and the cold peaks above.",
    "bluecraig": "the Great Goblin of Blue Craig, whose swarms infest the Blue Mountains of the far west, hard by the Grey Havens of Lindon.",
    "lindon": "the Shipwright, Lord of the Grey Havens and eldest of the Eldar in Middle-earth, who kept the Ring Narya until he gave it to Mithrandir.",
    "arthedain": "King of Arthedain, heir of Arvedui and of Isildur, who holds Fornost against Angmar with fewer swords each year.",
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


def read_doc(path):
    """Text with LF line ends, plus what it takes to write the file back byte-faithfully
    (.claude/rules/moduledata-validation.md, idiom A): its BOM and its own newline."""
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), nl, bom


def write_doc(path, text, nl, bom):
    data = text.replace("\n", nl).encode("utf-8")
    ET.fromstring(data)  # refuse to write a document that no longer parses
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + data)


def relationship_rows(rels):
    return "".join(f'            <relationship\n                kingdom="Kingdom.{other}"\n'
                   f'                value="{val}"\n                isAtWar="{"true" if war else "false"}" />\n'
                   for other, (val, war) in rels.items())


def main():
    import argparse
    ap = argparse.ArgumentParser()
    # Only the realms a regeneration reproduces byte for byte. The older entries have drifted from
    # their shipped blocks (Lindon was retagged to Culture.lindon and given its own party templates
    # after generation; --only goblin would rewrite 15,661 lines of lords.xml), so they stay in
    # KINGDOMS as history and templates, not as targets.
    ap.add_argument("--only", action="append", choices=REGENERATABLE, required=True)
    ap.add_argument("--check", action="store_true", help="exit 1, writing nothing, if a run would change a file")
    args = ap.parse_args()
    only = set(args.only)

    paths = {"spk": os.path.join(MD, "taom_spkingdoms.xml"), "clans": os.path.join(CH, "clans.xml"),
             "lords": os.path.join(CH, "lords.xml"), "heroes": os.path.join(CH, "heroes.xml")}
    docs = {key: read_doc(p) for key, p in paths.items()}
    spk, clans, lords, heroes = (docs[k][0] for k in ("spk", "clans", "lords", "heroes"))

    # templates
    k_gund = get_block(spk, r'<Kingdom\b[^>]*\bid="gundabad".*?</Kingdom>')
    k_riven = get_block(spk, r'<Kingdom\b[^>]*\bid="rivendell".*?</Kingdom>')
    clan_gund = get_block(clans, r'<Faction\b\s*\n\s*id="clan_gundabad_1".*?/>')
    lord_orc_m = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_G1_1".*?</NPCCharacter>')
    lord_orc_f = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_G1_9".*?</NPCCharacter>')
    lord_elf_m = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_R1_1".*?</NPCCharacter>')
    lord_elf_f = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_R1_2".*?</NPCCharacter>')
    # Human lords (Arthedain): a Gondor knight and a Gondor lady, both on the generic
    # *_bat_template_medium_* rosters the equipment rewrite below expects.
    lord_human_m = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_WE9_l_1".*?</NPCCharacter>')
    lord_human_f = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_1_57_1".*?</NPCCharacter>')
    lord_templates = {"orc": (lord_orc_m, lord_orc_f), "elf": (lord_elf_m, lord_elf_f),
                      "human": (lord_human_m, lord_human_f)}

    new_kingdoms, new_clans, new_lords, new_heroes = {}, {}, {}, {}

    def face_pool(culture):
        """(male faces, female faces) of the shipped lords of a culture, generated blocks excluded."""
        shipped = re.sub(r"<!-- TAOM-NEWFACTIONS:[a-z]+:BEGIN -->.*?<!-- TAOM-NEWFACTIONS:[a-z]+:END -->", "",
                         lords, flags=re.DOTALL)
        pools, seen = ([], []), set()
        for m in re.finditer(r'<NPCCharacter\b([^>]*\bculture="Culture\.%s"[^>]*)>(.*?)</NPCCharacter>' % culture,
                             shipped, re.DOTALL):
            face = re.search(r"<face>.*?</face>", m.group(2), re.DOTALL)
            key = re.search(r'\bkey="([0-9A-Fa-f]+)"', face.group(0)) if face else None
            # Shipped lords share placeholder faces among themselves; one copy of each.
            if face and key and key.group(1) not in seen and 'occupation="Lord"' in m.group(1):
                seen.add(key.group(1))
                pools[1 if re.search(r'is_female="true"', m.group(1), re.I) else 0].append(face.group(0))
        return pools

    sib = list(KINGDOMS.keys())
    for kid, k in KINGDOMS.items():
        if kid not in only:
            continue
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
        if k.get("banner"):
            blk, n = re.subn(r'banner_key="[^"]*"', f'banner_key="{k["banner"].format(icon=k["kingdom_icon"])}"', blk, count=1)
            if n != 1:
                raise RuntimeError(f"{kid}: template kingdom has no banner_key to replace")
        if k.get("relationships"):
            # Stated outright: the template's own rows are the template kingdom's stances.
            blk, n = re.subn(r"(<relationships>\n).*?([ \t]*</relationships>)",
                             lambda m: m.group(1) + relationship_rows(k["relationships"]) + m.group(2),
                             blk, count=1, flags=re.DOTALL)
            if n != 1:
                raise RuntimeError(f"{kid}: template kingdom has no <relationships> block")
        else:
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

        # One pass over each pool per realm: names in order and never repeated, a distinct face per
        # lord. Running out of either is an error, never a wrap-around.
        names = {False: iter(k.get("male_names", ())), True: iter(k.get("female_names", ()))}
        faces = face_pool(k["faces_from"]) if k.get("faces_from") else None
        next_face = {False: iter(faces[0]), True: iter(faces[1])} if faces else None

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
            # The template clan's party template, colours and banner are Gundabad's; a kingdom that
            # names its own replaces all three (every one must match, or the clan keeps an orc's).
            for key, pattern, value in (
                    ("party_template", r'default_party_template="[^"]*"',
                     lambda: f'default_party_template="{k["party_template"]}"'),
                    ("clan_colors", r'\bcolor="[0-9A-Fa-f]+"', lambda: f'color="{k["clan_colors"][ci][0]}"'),
                    ("clan_colors", r'\bcolor2="[0-9A-Fa-f]+"', lambda: f'color2="{k["clan_colors"][ci][1]}"'),
                    ("clan_icons", r'banner_key="[^"]*"',
                     lambda: f'banner_key="{k["banner"].format(icon=k["clan_icons"][ci])}"')):
                if k.get(key):
                    c, n = re.subn(pattern, value(), c, count=1)
                    if n != 1:
                        raise RuntimeError(f"{clan_id}: template clan has no {pattern}")
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
                is_elf = k["tmpl"] == "rivendell" and k.get("lords") is None
                kind = k.get("lords") or ("elf" if is_elf else "orc")
                tmpl_l = lord_templates[kind][1 if female else 0]
                # skill template
                if ci == 0 and li == 1:
                    skill = k["lord_skill"][0]
                elif female:
                    skill = k["lord_skill"][2]
                else:
                    skill = k["lord_skill"][1]
                # name
                if ci == 0 and li == 1 and (k.get("leader_name") or is_elf):
                    nm = k.get("leader_name") or ELF_LEADER
                elif k.get("male_names"):
                    nm = next(names[female], None)
                    if nm is None:
                        raise RuntimeError(f"{kid}: its {'female' if female else 'male'} name pool ran out")
                else:
                    pool = (ELF_F if female else ELF_M) if is_elf else (ORC_F if female else ORC_M)
                    seed = {"goblin": 0, "mistymountainorcs": 13, "lindon": 5, "bluecraig": 21}[kid]
                    nm = pool[(seed + ci * 5 + li) % len(pool)]
                letter = "abcde"[(li - 1) % 5]
                age = (22 + (li % 6)) if female else (30 + (li * 3) % 16)  # females childbearing-age
                L = tmpl_l
                L, n = re.subn(r'\bid="lord_[A-Za-z0-9_]+"', f'id="{lord_id}"', L, count=1)
                if n != 1:
                    raise RuntimeError(f"{lord_id}: template lord id was not rewritten")
                L = re.sub(r'name="\{=[^}]*\}[^"]*"', f'name="{{=aom_{lord_id}_name}}{nm}"', L, count=1)
                L = re.sub(r'\bage="[0-9.]+"', f'age="{age}"', L, count=1)
                L = re.sub(r'\bculture="Culture\.[a-z]+"', f'culture="Culture.{k["culture"]}"', L, count=1)
                L = re.sub(r'skill_template="SkillSet\.[a-zA-Z0-9_]+"', f'skill_template="SkillSet.{skill}"', L, count=1)
                if next_face:
                    face = next(next_face[female], None)
                    if face is None:
                        raise RuntimeError(f"{kid}: only {len(faces[female])} {k['faces_from']} faces for its lords")
                    L, n = re.subn(r"<face>.*?</face>", lambda _m, f=face: f, L, count=1, flags=re.DOTALL)
                    if n != 1:
                        raise RuntimeError(f"{lord_id}: template lord has no <face>")
                if kind == "orc":  # orc race swap (elf template already race="elf"; humans carry none)
                    L = re.sub(r'\brace="pale_uruk"', f'race="{k["race"]}"', L, count=1)
                if ci == 0 and li == 1 and k.get("leader_head"):
                    # After the set refs: NPCCharacters.xsd wants direct <equipment> rows last.
                    L, n = re.subn(r"([ \t]*</Equipments>)", lambda m: f'            <equipment slot="Head" id="Item.{k["leader_head"]}" />\n'
                                   + m.group(1), L, count=1)
                    if n != 1:
                        raise RuntimeError(f"{lord_id}: template lord has no <Equipments> for the leader's head")
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

    # insert (idempotent), one marker per kingdom per file, only for the kingdoms named
    for kid in (x for x in KINGDOMS if x in only):
        spk = upsert(spk, "</Kingdoms>", new_kingdoms[kid], f"TAOM-NEWFACTIONS:{kid}")
        clans = upsert(clans, "</Factions>", new_clans[kid], f"TAOM-NEWFACTIONS:{kid}")
        lords = upsert(lords, "</NPCCharacters>", new_lords[kid], f"TAOM-NEWFACTIONS:{kid}")
        heroes = upsert(heroes, "</Heroes>", new_heroes[kid], f"TAOM-NEWFACTIONS:{kid}")

    outputs = (("spk", spk), ("clans", clans), ("lords", lords), ("heroes", heroes))
    if args.check:
        changed = [os.path.basename(paths[key]) for key, content in outputs if content != docs[key][0]]
        print("  would change: " + ", ".join(changed) if changed else "  unchanged: all four files")
        return 1 if changed else 0
    for key, content in outputs:
        _, nl_, bom = docs[key]
        if content == docs[key][0]:
            print(f"  unchanged: {os.path.basename(paths[key])}")
            continue
        write_doc(paths[key], content, nl_, bom)
        ET.parse(paths[key])
        print(f"  inserted + well-formed: {os.path.basename(paths[key])}")

    nl = sum(len(re.findall(r'<NPCCharacter\b', v)) for v in new_lords.values())
    nh = sum(v.count("<Hero") for v in new_heroes.values())
    nc = sum(v.count("<Faction") for v in new_clans.values())
    print(f"\n  kingdoms={len(only)}  clans={nc}  lords={nl}  heroes={nh}")
    print("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
