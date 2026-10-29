#!/usr/bin/env python3
"""Generate kingdoms + clans + lords + heroes for the 3 new factions and insert them
into taom_spkingdoms.xml / characters/clans.xml / characters/lords.xml / characters/heroes.xml.

Templates are read by id from the live files (gundabad for the two orc kingdoms, rivendell
for Lindon) and parametrized — no hand-copied XML. Idempotent via TAOM-NEWFACTIONS markers.

Run: python tools/generate_new_faction_kingdoms.py --only arthedain            # dry run: names the files it would change
     python tools/generate_new_faction_kingdoms.py --only arthedain --check    # exit 1 if a run would change a file
     python tools/generate_new_faction_kingdoms.py --only arthedain --apply
"""
import os, re, json, sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _marker_region import replace_region  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MD = os.path.join(ROOT, "Main", "_Module", "ModuleData")
CH = os.path.join(MD, "characters")

# ---------- design data ----------
# `name` is the bare realm name, as every shipped kingdom has it; "Kingdom of X" goes in `title` (and on the
# faction card), never in `name` (#1050; KingdomNameTests in tools/tests/test_generate_new_faction_kingdoms.py).
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
        name="Arthedain", short="Arthedain", title="North-Kingdom of Arthedain", ruler="King",
        desc="The last of the three realms into which Arnor was sundered, Arthedain did not fall. Arvedui's line still holds Fornost upon the North Downs, though its lords are few, its towns poor, and Angmar's shadow lies always on the northern hills.",
        # The templates' own skill sets: lords.xml rows carry inline skills equal to their template
        # (SKILL_TEMPLATE_MISMATCH), so a clone keeps its source's set until docs/ai-includes/lord-skills-authoring.md retunes it.
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
        # Arnor blue (palette 190); one icon per clan so no two clans fly the same banner. The realm and
        # its royal house fly the crown of Arnor (11004, Mike 2026-10-28); the White Tree went to clan 5.
        banner="11.190.190.1528.1528.764.764.1.0.0.{icon}.110.110.700.700.764.764.0.0.0",
        kingdom_icon=11004, clan_icons=[11004, 11001, 11002, 11003, 11000],
        clan_colors=[("FF4870B8", "FFC8CCD8"), ("FF3A6298", "FFC8CCD8"), ("FF5960A8", "FFD2D6D5"),
                     ("FF2E4A78", "FFCACCCB"), ("FF4E75BC", "FFEAEEEF")],
        # Day-one stances. value < 0 or isAtWar declares war at load (Kingdom.Deserialize,
        # Kingdom.cs:780-791), and every campaign starts with no war (Mike, 2026-10-18): Gundabad
        # attacks on day 44 through its Hostile row in diplomacy.json, where the rest of the stance lives.
        relationships={"gundabad": (0, False), "empire": (0, False), "isengard": (0, False),
                       "rivendell": (1, False), "lindon": (1, False), "empire_w": (1, False)},
        # Each lord takes the <face> of a different shipped lord of this culture and sex, in file
        # order, instead of the template's: otherwise all ten men share one face and all ten women
        # another (Lindon's generated lords do).
        faces_from="gondor",
        # The ruler alone wears the Arnor crown (Mike, 2026-10-07): a direct Head override under his
        # <Equipments>, laid over that slot in each of his sets.
        leader_head="sk_ar_art_crown_king_a",
    ),
    # Cardolan and Rhudaur (#978; Mike, 2026-10-23): the other two of the three kingdoms Earendur's
    # sons made of Arnor, each on its own culture (promote_borrowed_cultures.py and its own troop
    # generator), weak in numbers like Arthedain. Neutral realms (Mike, 2026-10-23): Neutral toward
    # every realm at the start, so the War of the Ring does not draw them in on either side.
    "cardolan": dict(
        region="CA", culture="cardolan", race=None, capital="town_CA2", tmpl="rivendell", lords="human",
        equip="cardolan", side="neutral", color="0xFF28745C", color2="0xFFFFFFFF",
        name="Cardolan", short="Cardolan", title="Kingdom of Cardolan", ruler="King",
        desc="The southern of the three realms into which Arnor was sundered. Its kings lie in the barrows of Tyrn Gorthad and its riders keep the plains between the Baranduin and the Greyflood, but its towns are few and its lords contend with Arthedain and Rhudaur for the crown of Arnor.",
        lord_skill=("taom_gondor_knight_skills", "taom_gondor_knight_skills", "taom_gondor_young_lady_skills"),
        lords_per_clan=4,  # 4 clans x 4 = 16 lords (2 male + 2 female, married within the clan)
        clans=[("clan_cardolan_1", 4, "town_CA2", "House of Tyrn Gorthad"),
               ("clan_cardolan_2", 3, "town_CA1", "House of Tharbad"),
               ("clan_cardolan_3", 2, "town_CA3", "House of Minas Eithel"),
               ("clan_cardolan_4", 2, "castle_CA2", "Wardens of Sarn Ford")],
        leader_name="Aranthir",
        male_names=["Belethor", "Caladhor", "Anardil", "Baradhel", "Calenon", "Dorthar", "Hirluin",
                    "Vorondil", "Turgil", "Faelon"],
        female_names=["Elendis", "Aerin", "Daeriel", "Eilinel", "Gwaeren", "Lindis", "Mirwen", "Saelind",
                      "Nimloth", "Andreth"],
        party_template="PartyTemplate.kingdom_hero_party_cardolan_template",
        # Cardolan green (palette 2027) with white emblems (2004), Tharbad in gold (238) (Mike 2026-10-28,
        # #1047). The realm and its royal house fly the interlace of Tyrn Gorthad (31005).
        banner="11.2027.2027.1528.1528.764.764.1.0.0.{icon}.{fg}.{fg}.700.700.764.764.0.0.0",
        kingdom_icon=31005, clan_icons=[31005, 11006, 11000, 11001],
        sigil=2004, clan_sigils=[2004, 238, 2004, 2004],
        # Shades of the banner green with white: Patch23 dresses troops and map parties in these.
        clan_colors=[("FF28745C", "FFF2F4F2"), ("FF22664F", "FFE8ECE8"), ("FF2F8068", "FFF2F4F2"),
                     ("FF1E5A48", "FFE2E8E4")],
        relationships={"gundabad": (0, False), "empire": (0, False), "isengard": (0, False),
                       "rivendell": (0, False), "lindon": (0, False), "empire_w": (0, False),
                       "arthedain": (0, False), "rhudaur": (0, False)},
        # A face template, not copied keys: each lord draws his own face from Gondor's range at
        # campaign start (Lord Face Variety rule A), where a copied key is only nudged (rule B,
        # HeroFaceVarietyDataTests' ceiling). The template's men's hair tags would bald half the
        # ladies, so they roll hair from the "empire" tag, as Gondor's ladies do.
        face_template="fighter_gondor", female_hair_tag="empire",
    ),
    "rhudaur": dict(
        region="RH", culture="rhudaur", race=None, capital="town_RH1", tmpl="rivendell", lords="human",
        equip="rhudaur", side="neutral", color="0xFF603830", color2="0xFFC8B8A8",
        name="Rhudaur", short="Rhudaur", title="Kingdom of Rhudaur", ruler="King",
        desc="The eastern of the three realms into which Arnor was sundered, a hill kingdom between the Hoarwell and the Loudwater. Its Dunedain lords hold Cameth Brin and the Angle and rule a hard people of hill-men, with trolls in the fells and orcs on every pass.",
        lord_skill=("taom_gondor_knight_skills", "taom_gondor_knight_skills", "taom_gondor_young_lady_skills"),
        lords_per_clan=4,  # 3 clans x 4 = 12 lords (2 male + 2 female, married within the clan)
        clans=[("clan_rhudaur_1", 4, "town_RH1", "House of Cameth Brin"),
               ("clan_rhudaur_2", 3, "castle_RH2", "House of Ettendale"),
               ("clan_rhudaur_3", 2, "castle_RH3", "Wardens of the Coldfells")],
        leader_name="Belegorn",
        male_names=["Cadorn", "Corran", "Eredhir", "Gorlan", "Harnoth", "Lorgan", "Talgar", "Vardil"],
        female_names=["Gilwen", "Branwen", "Eirlys", "Heledd", "Olwen", "Seren", "Tegwen", "Rhiain"],
        party_template="PartyTemplate.kingdom_hero_party_rhudaur_template",
        # Arnor icons (group 110) in silver (palette 110) on a dark hill brown (palette 99).
        banner="11.99.99.1528.1528.764.764.1.0.0.{icon}.110.110.700.700.764.764.0.0.0",
        kingdom_icon=11002, clan_icons=[11002, 11003, 11004],
        clan_colors=[("FF603830", "FFC8B8A8"), ("FF4E3A30", "FFC8B8A8"), ("FF6A4A3A", "FFCACCCB")],
        relationships={"gundabad": (0, False), "empire": (0, False), "isengard": (0, False),
                       "rivendell": (0, False), "lindon": (0, False), "empire_w": (0, False),
                       "arthedain": (0, False), "cardolan": (0, False)},
        # Its lords are Dunedain: Gondor's face range, drawn per lord as Cardolan's.
        face_template="fighter_gondor", female_hair_tag="empire",
    ),
    # Ered Luin (2026-10-27): the dwarf kingdom of the Blue Mountains, on its own culture
    # (promote_borrowed_cultures.py, cut from Erebor, and generate_eredluin_troops.py). A middling
    # realm: the ruling House of Borin at tier 5, the Broadbeams and Firebeards at 4, the Wardens of
    # Dolmed at 3, so about 9 lord parties. Free side, with Erebor and Lindon as its natural allies.
    "eredluin": dict(
        region="EL", culture="eredluin", race="dwarf", capital="town_EL3", tmpl="rivendell", lords="dwarf",
        equip="eredluin", side="free", color="0xFF3E5A7A", color2="0xFFB8C4CC",
        name="Ered Luin", short="Ered Luin", title="Kingdom of Ered Luin", ruler="King",
        desc="The dwarf kingdom of the Blue Mountains. Durin's Folk made Thorin's Halls there in their exile, and in this age the Broadbeams and the Firebeards hold Belegost and Nogrod again. Its lords are few and its halls are poorer than those of its kin in the east, but its smiths are the oldest in the West, and its axes keep the goblins of Blue Craig from the roads to Lindon.",
        # The dwarf templates' own skill sets (SKILL_TEMPLATE_MISMATCH, as Arthedain's note says).
        lord_skill=("taom_dwarf_lord_skills", "taom_dwarf_lord_skills", "taom_dwarf_lady_skills"),
        lords_per_clan=4,  # 4 clans x 4 = 16 lords (2 male + 2 female, married within the clan)
        clans=[("clan_eredluin_1", 5, "town_EL3", "House of Borin"),
               ("clan_eredluin_2", 4, "town_EL1", "Broadbeams"),
               ("clan_eredluin_3", 4, "town_EL2", "Firebeards"),
               ("clan_eredluin_4", 3, "castle_EL2", "Wardens of Dolmed")],
        leader_name="Farin II",
        male_names=["Andvari", "Fjalar", "Frosti", "Ginnar", "Hepti", "Skirfir", "Virfir", "Aurvang"],
        female_names=["Bryndís", "Gerda", "Hlíf", "Sigrún", "Ásta", "Védís", "Ragna", "Herdís"],
        party_template="PartyTemplate.kingdom_hero_party_eredluin_template",
        # Dwarf icons (Main/_Module/ModuleData/banner_icons.xml 24000-24030, ones no clan flies yet) in
        # silver (palette 110) on the slate blue of the Blue Mountains (palette 200).
        banner="11.200.200.1528.1528.764.764.1.0.0.{icon}.110.110.700.700.764.764.0.0.0",
        kingdom_icon=24000, clan_icons=[24000, 24001, 24002, 24003],
        clan_colors=[("FF3E5A7A", "FFB8C4CC"), ("FF34495E", "FFC8CCD0"), ("FF6A3A2A", "FFE0B080"),
                     ("FF2C3E50", "FFB0B8C0")],
        # No war on day one (see Arthedain's note); the enemies are in diplomacy.json.
        relationships={"bluecraig": (0, False), "goblin": (0, False), "gundabad": (0, False),
                       "mistymountainorcs": (0, False), "erebor": (1, False), "lindon": (1, False)},
        # Erebor's dwarf face range drawn per lord at campaign start. fighter_erebor's keys carry hair
        # indices 1 and 10, and a tag list that matches no hair (the template's tags are culture names,
        # the dwarf skin's are styles) draws between them (native FUN_18057c9f0, v1.5.6), so no dwarf
        # lady can start bald. TiedAcrossBack is a style choice: the 8 tied styles (dwarf_hair_c, _e to
        # _k) instead of _a to _j. main() requires a tag on every face-template realm.
        face_template="fighter_erebor", female_hair_tag="TiedAcrossBack",
    ),
    # Anduin Vale and Angáladh (2026-10-28, #1053): the Northmen of the upper Anduin and of western
    # Mirkwood, each on its own culture cut from Dale's. Weak and Neutral, as Cardolan.
    "anduinvale": dict(
        region="AV", culture="anduinvale", race=None, capital="town_AV1", tmpl="rivendell", lords="dale",
        equip="anduinvale", side="neutral", color="0xFF654D37", color2="0xFFFBC841",
        name="Anduin Vale", short="Anduin Vale", title="Anduin Vale", ruler="Chieftain",
        desc="The Beornings and the Woodmen of the Vales hold the upper Anduin between the Misty Mountains and Mirkwood. They keep the High Pass and the Old Ford open for honest travellers and take a heavy toll of orcs and wolves. Their chieftains hold Beorn's Hall and the Carrock and want no part in the wars of kings.",
        lord_skill=("taom_dale_lord_skills", "taom_dale_lord_skills", "taom_dale_lady_skills"),
        lords_per_clan=4,  # 4 clans x 4 = 16 lords (2 male + 2 female, married within the clan)
        clans=[("clan_anduinvale_1", 4, "town_AV1", "House of Beorn"),
               ("clan_anduinvale_2", 3, "town_AV2", "Folk of the Old Ford"),
               ("clan_anduinvale_3", 2, "castle_AV1", "Wardens of the Carrock"),
               ("clan_anduinvale_4", 2, "castle_AV3", "Riders of the Gladden")],
        leader_name="Grimbeorn",
        male_names=["Osbeorn", "Hathbeorn", "Eadbeorn", "Grimwulf", "Thurstan", "Wulfhere", "Brunwald",
                    "Hunbeorn", "Ingwald", "Arnbeorn"],
        female_names=["Beornhild", "Aldwyn", "Hildeburg", "Osgyth", "Eadwyn", "Wulfrun", "Mildgyth",
                      "Hrothwyn", "Aelfgifu", "Thurwyn"],
        party_template="PartyTemplate.kingdom_hero_party_anduinvale_template",
        # The Anduin Vale icons (20000-20006, flown by no clan before) in honey gold (2024) on Beorning
        # brown (palette 2007): the bear's head for the realm and the House of Beorn, the bee for the
        # Old Ford, the paw for the Carrock, the horse trident for the riders.
        banner="11.2007.2007.1528.1528.764.764.1.0.0.{icon}.{fg}.{fg}.700.700.764.764.0.0.0",
        kingdom_icon=20006, clan_icons=[20006, 20004, 20003, 20002],
        sigil=2024, clan_sigils=[2024, 2024, 2024, 2024],
        clan_colors=[("FF654D37", "FFFBC841"), ("FF5A4430", "FFF0C050"), ("FF6E563E", "FFFBC841"),
                     ("FF4E3C2A", "FFE8B840")],
        relationships={"mirkwood": (0, False), "lothlorien": (0, False), "sturgia": (0, False),
                       "erebor": (0, False), "dolguldur": (0, False), "gundabad": (0, False),
                       "goblin": (0, False), "mistymountainorcs": (0, False), "angaladh": (0, False)},
        # Dale's face range drawn per lord at campaign start; the ladies roll hair from Dale's
        # "sturgia" tag (16 adult female hairs in the native skins, none bald), as Dale's ladies do.
        face_template="fighter_sturgia", female_hair_tag="sturgia",
    ),
    "angaladh": dict(
        region="AND", culture="angaladh", race=None, capital="town_AND1", tmpl="rivendell", lords="dale",
        equip="angaladh", side="neutral", color="0xFF4F6F5A", color2="0xFFF2F4F7",
        name="Angáladh", short="Angáladh", title="Kingdom of Angáladh", ruler="King",
        desc="The Woodmen of western Mirkwood cleared their steads under the eaves of the forest and named their realm for the iron-hard trees that ring Woodmen-town. They are bowmen and woodwards, few in number, at peace with the Elves to the north and ever watchful of Dol Guldur to the south.",
        lord_skill=("taom_dale_lord_skills", "taom_dale_lord_skills", "taom_dale_lady_skills"),
        lords_per_clan=4,  # 3 clans x 4 = 12 lords (2 male + 2 female, married within the clan)
        clans=[("clan_angaladh_1", 4, "town_AND1", "House of Angáladh"),
               ("clan_angaladh_2", 3, "castle_AND3", "Woodwards of the Forest Road"),
               ("clan_angaladh_3", 2, "castle_AND2", "Wardens of Emyn Duir")],
        leader_name="Ingomar",
        male_names=["Ansila", "Hrodiwar", "Gaisa", "Liudric", "Wandil", "Athalwin", "Thrasamund",
                    "Vidimer"],
        female_names=["Gudila", "Riquila", "Hildiwara", "Sunilda", "Wiliswintha", "Ansiwara", "Ranilda",
                      "Gaila"],
        party_template="PartyTemplate.kingdom_hero_party_angaladh_template",
        # Icons no other clan flies (Mike 2026-10-28): Silvan 17000 and 17001 and the plain sun 20001.
        # Mirkwood's Nos Galengrim flies 17006, 17007 and 17008. Off-white (palette 2022, the realm's
        # color2 #F2F4F7, which the engine paints a clan's sigil in) on forest green (2021).
        banner="11.2021.2021.1528.1528.764.764.1.0.0.{icon}.{fg}.{fg}.700.700.764.764.0.0.0",
        kingdom_icon=17000, clan_icons=[17000, 17001, 20001],
        sigil=2022, clan_sigils=[2022, 2022, 2022],
        clan_colors=[("FF4F6F5A", "FFF2F4F7"), ("FF45614F", "FFE8ECE8"), ("FF5A7A64", "FFF2F4F2")],
        relationships={"mirkwood": (0, False), "lothlorien": (0, False), "sturgia": (0, False),
                       "erebor": (0, False), "dolguldur": (0, False), "gundabad": (0, False),
                       "goblin": (0, False), "mistymountainorcs": (0, False), "anduinvale": (0, False)},
        face_template="fighter_sturgia", female_hair_tag="sturgia",
    ),
}
# The realms a regeneration reproduces (main() --only). See the note there.
REGENERATABLE = ("arthedain", "cardolan", "rhudaur", "eredluin", "anduinvale", "angaladh")
# Leader (clan-1 lord-1) descriptions, period-correct for TA 3018 (War of the Ring).
LEADER_BLURB = {
    "goblin": "the Great Goblin of Goblin Town, master of the warrens of the High Pass.",
    "mistymountainorcs": "Warlord of the Misty Mountains, who holds the deep places of Moria and the cold peaks above.",
    "bluecraig": "the Great Goblin of Blue Craig, whose swarms infest the Blue Mountains of the far west, hard by the Grey Havens of Lindon.",
    "lindon": "the Shipwright, Lord of the Grey Havens and eldest of the Eldar in Middle-earth, who kept the Ring Narya until he gave it to Mithrandir.",
    "arthedain": "King of Arthedain, heir of Arvedui and of Isildur, who holds Fornost against the orcs of Gundabad and keeps an uneasy peace with his kin in Cardolan and Rhudaur.",
    "cardolan": "King of Cardolan, heir of Earendur's second son, who keeps the barrow-kings' crown at Caras Gorthad.",
    "rhudaur": "King of Rhudaur, heir of Earendur's third son, lord of Cameth Brin and of the hill-men of the Angle.",
    "eredluin": "King of Ered Luin, of the line of Borin, Lord of Thorin's Halls, who holds the Blue Mountains with the Broadbeams of Belegost and the Firebeards of Nogrod.",
    "anduinvale": "son of Beorn, Chieftain of the Beornings, who keeps the High Pass and the Old Ford and lets no orc cross the Vales of Anduin.",
    "angaladh": "King of Angáladh, lord of Woodmen-town, who keeps the eaves of Mirkwood against the shadow of Dol Guldur.",
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


def kingdom_banner(k):
    """The realm's banner key; `{fg}` in its template takes the realm's emblem colour (`sigil`)."""
    return k["banner"].format(icon=k["kingdom_icon"], fg=k.get("sigil"))


def clan_banner(k, ci):
    """Clan `ci`'s banner key: its own icon, and its own emblem colour when the realm lists `clan_sigils`."""
    fg = k["clan_sigils"][ci] if k.get("clan_sigils") else k.get("sigil")
    return k["banner"].format(icon=k["clan_icons"][ci], fg=fg)


def get_block(text, pattern):
    m = re.search(pattern, text, re.DOTALL)
    if not m:
        raise RuntimeError("not found: " + pattern[:70])
    return m.group(0)


def upsert(text, close_tag, payload, marker):
    # In place, or refused when damaged (#947, #975): see tools/_marker_region.py.
    return replace_region(text, marker, f"  <!-- {marker}:BEGIN -->\n{payload}  <!-- {marker}:END -->\n", close_tag)


def read_doc(path):
    """Text with LF line ends, plus what it takes to write the file back byte-faithfully
    (.claude/rules/moduledata-validation.md, idiom A): its BOM and its own newline."""
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), nl, bom


def encode_doc(text, nl, bom):
    """The bytes read_doc's inverse writes: the text in its own newline and BOM."""
    return (b"\xef\xbb\xbf" if bom else b"") + text.replace("\n", nl).encode("utf-8")


def unparsable(pending):
    """(path, error) for each pending (path, bytes) that does not parse. Checked before any write."""
    bad = []
    for path, data in pending:
        try:
            ET.fromstring(data)
        except ET.ParseError as ex:
            bad.append((path, str(ex)))
    return bad


def write_file(path, data):
    open(path, "wb").write(data)


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
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="exit 1, writing nothing, if a run would change a file")
    mode.add_argument("--apply", action="store_true", help="write the changed files; without it a run is a dry run")
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
    # Dwarf lords (Ered Luin): Erebor's Náin and Dísa, both race="dwarf" on the generic
    # erebor_*_template_* rosters.
    lord_dwarf_m = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_E1_4".*?</NPCCharacter>')
    lord_dwarf_f = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_E1_3".*?</NPCCharacter>')
    # Dale lords (Anduin Vale, Angáladh): a Dale lord and lady on the generic
    # dale_*_template_* rosters, with Dale's own skill sets.
    lord_dale_m = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_S10_1".*?</NPCCharacter>')
    lord_dale_f = get_block(lords, r'<NPCCharacter\b[^>]*\bid="lord_S10_2".*?</NPCCharacter>')
    lord_templates = {"orc": (lord_orc_m, lord_orc_f), "elf": (lord_elf_m, lord_elf_f),
                      "human": (lord_human_m, lord_human_f), "dwarf": (lord_dwarf_m, lord_dwarf_f),
                      "dale": (lord_dale_m, lord_dale_f)}

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
        if k.get("face_template") and not k.get("female_hair_tag"):
            raise RuntimeError(f"{kid}: face_template needs female_hair_tag (the template's hair tags are men's)")
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
            blk, n = re.subn(r'banner_key="[^"]*"', f'banner_key="{kingdom_banner(k)}"', blk, count=1)
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
                     lambda: f'banner_key="{clan_banner(k, ci)}"')):
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
                if k.get("face_template"):
                    # A lady keeps the template's men's hair tags otherwise, and about half start bald.
                    hair = (f'\n            <hair_tags>\n                <hair_tag name="{k["female_hair_tag"]}" />'
                            '\n            </hair_tags>') if re.search(r'is_female="true"', L) else ""
                    face = (f'<face>\n            <face_key_template value="BodyProperty.{k["face_template"]}" />'
                            f'{hair}\n          </face>')
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
    pending = [(paths[key], encode_doc(content, docs[key][1], docs[key][2]))
               for key, content in outputs if content != docs[key][0]]
    changed = [os.path.basename(p) for p, _ in pending]
    # Every changed output must parse before any is written: one broken file leaves all four untouched.
    bad = unparsable(pending)
    if bad:
        print("REFUSING: these outputs do not parse, so nothing was written:")
        for p, error in bad:
            print(f"  {os.path.basename(p)}: {error}")
        return 1
    if args.check:
        print("  would change: " + ", ".join(changed) if changed else "  unchanged: all four files")
        return 1 if changed else 0
    if not args.apply:
        print("  would change: " + ", ".join(changed) if changed else "  unchanged: all four files")
        print("DRY RUN: re-run with --apply to write")
        return 0
    for key, _ in outputs:
        if os.path.basename(paths[key]) not in changed:
            print(f"  unchanged: {os.path.basename(paths[key])}")
    for p, data in pending:
        write_file(p, data)
        print(f"  inserted + well-formed: {os.path.basename(p)}")

    nl = sum(len(re.findall(r'<NPCCharacter\b', v)) for v in new_lords.values())
    nh = sum(v.count("<Hero") for v in new_heroes.values())
    nc = sum(v.count("<Faction") for v in new_clans.values())
    print(f"\n  kingdoms={len(only)}  clans={nc}  lords={nl}  heroes={nh}")
    print("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
