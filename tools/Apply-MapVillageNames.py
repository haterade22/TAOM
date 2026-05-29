#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Apply lore-appropriate display names to TAOM map villages in TAOM_Map module.

Targets the LIVE engine-loaded files at:
    E:\\Steam\\...\\Modules\\TAOM_Map\\ModuleData\\settlements.xml
    E:\\Steam\\...\\Modules\\TAOM_Map\\ModuleData\\Languages\\<LANG>\\loc_settlements.xml  (x12)

Do NOT confuse with TAOM repo's Main/_Module/ModuleData/settlements.xml, which is a
stale snapshot last touched 2026-04-06 and is NOT registered in SubModule.xml.

The NAMES dict below maps Settlement IDs to display-name defaults. The script:
  1. Updates `name="{=key}DEFAULT"` in the master settlements.xml (EN fallback)
  2. Updates `<string ... text="..."/>` in 12 per-language loc_settlements.xml files
     (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR — same name in every language
     because proper nouns of invented Tolkien languages don't translate)

Properties:
  - Idempotent (regex-anchored on unique key IDs; safe to re-run after edits)
  - UTF-8 + CRLF preserved (UTF-8 round-trip via bytes)
  - Settlement IDs untouched (save compatibility preserved)
  - Validates uniqueness before applying (aborts on dup names within the dict)

Usage:
  Edit the NAMES dict, then run:
    python3 tools/Apply-MapVillageNames.py

See docs/reference/taom-map-settlement-naming.md for:
  - Region-prefix → culture → linguistic idiom mapping
  - How to add new mappings safely
  - Why the TAOM repo settlements.xml is NOT the source of truth

Last bulk-applied: 2026-05-26 (345 village display names across 15 regions).
"""
import os, re

NAMES = {
    # V - Rohan (Anglo-Saxon)
    "castle_village_V1_2": "Seolforhamm",
    "castle_village_V4_2": "Linhamm",
    "castle_village_V4_3": "Sídwic",
    "village_V1_3": "Lámford",
    "village_V3_1": "Sealtburg",
    "village_V4_1": "Eldholt",
    "village_V4_2": "Swinmoor",
    "village_V4_3": "Wulfhamm",
    "village_V7_3": "Hwætland",

    # L - Lothlórien (Sindarin)
    "castle_village_L1_1": "Glórinant",
    "castle_village_L1_2": "Mallorlas",
    "castle_village_L1_3": "Anglhad",
    "castle_village_L2_1": "Nimphroth",
    "castle_village_L2_2": "Celebloth",
    "castle_village_L2_3": "Glanlin",
    "castle_village_L3_1": "Idhralas",
    "castle_village_L3_2": "Lothbar",
    "castle_village_L3_3": "Lasrond",
    "village_L1_1": "Tawarmir",
    "village_L1_2": "Imloth",
    "village_L1_3": "Sîrgalad",
    "village_L1_4": "Iauphen",
    "village_L1_5": "Naerondol",
    "village_L1_6": "Anglond",

    # E - Erebor (Khuzdul + Old Norse)
    "castle_village_E1_1": "Hammerstand",
    "castle_village_E1_2": "Cleyhold",
    "castle_village_E1_3": "Saltstead",
    "castle_village_E2_1": "Skogfold",
    "castle_village_E2_2": "Boarhall",
    "castle_village_E3_1": "Beorgsnar",
    "castle_village_E3_2": "Kornholt",
    "castle_village_E4_1": "Bizar-mund",
    "castle_village_E4_2": "An-baruk",
    "castle_village_E4_3": "Zâram-gun",
    "castle_village_E5_1": "Kibilûl",
    "castle_village_E5_2": "Mazalbund",
    "castle_village_E5_3": "Khelednâr",
    "castle_village_E6_1": "Grymmstad",
    "castle_village_E6_2": "Gríthhalla",
    "castle_village_E7_1": "Zul-gathol",
    "castle_village_E7_2": "Zul-zigil",
    "castle_village_E7_3": "Zul-bund",
    "castle_village_E8_1": "Frôstkorn",
    "castle_village_E8_2": "Helmstead",
    "castle_village_E9_1": "Vânholt",
    "castle_village_E9_2": "Gronnar",
    "castle_village_E9_3": "Saevarn",
    "village_E1_1": "Saltvale",
    "village_E1_2": "Lakefoot",
    "village_E1_3": "Mirkholt",
    "village_E1_4": "Kibilbund",
    "village_E2_1": "Linenhold",
    "village_E2_2": "Sídvang",
    "village_E2_3": "Lerlund",
    "village_E2_4": "Halsalt",
    "village_E3_1": "Skôrholt",
    "village_E3_2": "Grimsvald",
    "village_E3_3": "Vargholm",
    "village_E3_4": "Korndale",
    "village_E4_1": "Azanrandol",
    "village_E4_2": "Baruk-dûm",
    "village_E4_3": "Ûlzâram",
    "village_E4_4": "Kibil-bizar",

    # EN - Dunland (Welsh/Brythonic)
    "castle_village_EN3_3": "Caer Dunwyr",
    "village_EN2_3": "Lhan Penrhos",
    "village_EN3_2": "Caer Haearn",

    # I - Isengard (Sindarin)
    "castle_village_I1_1": "Nan Methed",
    "castle_village_I1_2": "Anggath",
    "castle_village_I1_3": "Sarn-orod",
    "castle_village_I2_1": "Galadbost",
    "castle_village_I2_2": "Sornhirost",
    "castle_village_I2_3": "Bar-noss",

    # ES - Mordor (Black Speech + Sindarin)
    "castle_village_ES3_3": "Lhûgsen",
    "castle_village_ES4_3": "Gûl-mauz",
    "castle_village_ES5_3": "Naurghai",
    "castle_village_ES5_4": "Nûrn-kâlan",
    "castle_village_ES7_3": "Wath-bûrz",
    "castle_village_ES8_3": "Lûg-salth",
    "castle_village_ES8_4": "Lúgthrak",
    "village_ES1_1": "Borzhai",
    "village_ES2_1": "Morgul-hai",
    "village_ES4_2": "Seregfain",
    "village_ES6_3": "Naerlug",
    "village_ES6_4": "Naerkrimp",

    # K - Khand (Eastern/Mongol-Arabic)
    "castle_village_K1_3": "Sûragh-Tün",
    "castle_village_K2_3": "Varnokh-Dol",
    "castle_village_K3_3": "Ôvath-Khûr",
    "castle_village_K4_3": "Krôk-Vasht",
    "castle_village_K5_3": "Mathar-Düz",
    "castle_village_K6_3": "Khond-Vol",
    "castle_village_K7_3": "Klagh-Sîr",
    "village_K1_3": "Sturlûsh",
    "village_K2_3": "Lôrm-Argan",
    "village_K2_4": "Sakûn-Loi",
    "village_K3_4": "Ardamün",
    "village_K4_1": "Anlô-Dab",

    # EW - Gondor (Sindarin)
    "castle_village_EW10_1": "Anduinmir",
    "castle_village_EW10_2": "Celebnir",
    "castle_village_EW11_1": "Lin-Harn",
    "castle_village_EW11_2": "Idhren-Harn",
    "castle_village_EW12_1": "Linnost",
    "castle_village_EW12_2": "Aearnir",
    "castle_village_EW13_1": "Tawarhirn",
    "castle_village_EW13_2": "Beor-Harn",
    "castle_village_EW14_1": "Tumlonn",
    "castle_village_EW15_1": "Amonbain",
    "castle_village_EW15_2": "Roch-Amon",
    "castle_village_EW15_3": "Angost",
    "castle_village_EW16_1": "Eithelthir",
    "castle_village_EW16_2": "Celeberein",
    "castle_village_EW16_3": "Erengwath",
    "village_EW3_3": "Anduinbrethil",

    # R - Rivendell (Sindarin)
    "castle_village_R2_1": "Hithaeglin",
    "castle_village_R2_2": "Hithtawar",
    "castle_village_R2_3": "Bruinmer",
    "castle_village_R3_1": "Tornaethrim",
    "castle_village_R3_2": "Iauphen-tor",
    "castle_village_R3_3": "Rochthir",
    "castle_village_R4_1": "Angloss",
    "castle_village_R4_2": "Bruinost",
    "castle_village_R4_3": "Celebcharn",
    "castle_village_R5_1": "Linfennas",
    "castle_village_R5_2": "Drûnasil",
    "castle_village_R5_3": "Pelan-fennas",
    "village_R1_1": "Bruinael",
    "village_R1_2": "Imladtawar",
    "village_R1_3": "Imladrochrim",
    "village_R1_4": "Glanduin-host",

    # G - Gundabad (Black Speech)
    "castle_village_G1_1": "Bagmosh",
    "castle_village_G1_2": "Skarn-uruk",
    "castle_village_G2_1": "Düglar-tang",
    "castle_village_G2_2": "Düglar-mauz",
    "castle_village_G2_3": "Krimp-Düg",
    "castle_village_G3_1": "Mazūg-zâr",
    "castle_village_G3_2": "Mazūg-mîr",
    "castle_village_G3_4": "Mazūg-bash",
    "castle_village_G5_1": "Shôrd-krish",
    "castle_village_G5_2": "Shôrd-glob",
    "castle_village_G5_3": "Shôrd-salth",
    "castle_village_G5_4": "Shôrd-thrak",
    "village_G1_1": "Gundbosh",
    "village_G1_2": "Gund-snar",
    "village_G1_3": "Gund-tang",
    "village_G1_4": "Gund-mauz",
    "village_G2_1": "Gram-krimp",
    "village_G2_2": "Gram-zâr",
    "village_G2_3": "Gram-mîr",
    "village_G2_4": "Gram-bash",

    # FH - Far Harad (Variag/Mongol-Arabic)
    "castle_village_FH1_1": "Archad-Khoth",
    "castle_village_FH1_2": "Archad-Lub",
    "castle_village_FH1_3": "Dízar-Sîr",
    "castle_village_FH2_1": "Erak-Vand",
    "castle_village_FH2_2": "Erak-Bûsh",
    "castle_village_FH2_3": "Kônd-Vargh",
    "castle_village_FH3_1": "Bara-Tün",
    "castle_village_FH3_2": "Bara-Mukh",
    "castle_village_FH3_3": "Bara-Pôsh",
    "castle_village_FH4_1": "Shôla-Krim",
    "castle_village_FH4_2": "Shôla-Bahr",
    "castle_village_FH4_3": "Shôla-Argûn",
    "castle_village_FH5_1": "Kôth-Lîn",
    "castle_village_FH5_2": "Rau-Krish",
    "castle_village_FH5_3": "Rau-Glôb",
    "castle_village_FH6_1": "Onab-Sîr",
    "castle_village_FH6_2": "Onab-Vand",
    "castle_village_FH6_3": "Onab-Bûsh",
    "castle_village_FH7_1": "Tarid-Vargh",
    "castle_village_FH7_2": "Tarid-Tün",
    "castle_village_FH7_3": "Tarid-Mukh",
    "castle_village_FH8_1": "Urut-Krim",
    "castle_village_FH8_2": "Urut-Bahr",
    "castle_village_FH8_3": "Urut-Argûn",
    "castle_village_FH9_1": "Zakh-Lîn",
    "castle_village_FH9_2": "Zakh-Krish",
    "castle_village_FH9_3": "Zakh-Glôb",

    # U - Umbar (Adunaic / Black Númenórean)
    "castle_village_U1_1": "Bej-Phazân",
    "castle_village_U1_2": "Magha-Mîr",
    "castle_village_U1_3": "Magha-Khar",
    "castle_village_U2_1": "Azrubar",
    "castle_village_U2_2": "Azar-Mîr",
    "castle_village_U3_1": "Bôluz-Lîn",
    "castle_village_U3_2": "Bôluz-Sêr",
    "castle_village_U4_1": "Zimrath-Lub",
    "castle_village_U4_2": "Zimrath-Sîr",
    "castle_village_U4_3": "Zimrath-Vand",
    "castle_village_U5_1": "Zamarz-Bûsh",
    "castle_village_U5_2": "Zamarz-Vargh",
    "castle_village_U5_3": "Zamarz-Tün",
    "castle_village_U6_1": "Hôtab-Mukh",
    "castle_village_U6_2": "Hôtab-Khar",
    "castle_village_U6_3": "Hôtab-Bahr",
    "castle_village_U7_1": "Khôthra-Mîr",
    "castle_village_U7_2": "Khôthra-Lîn",
    "castle_village_U7_3": "Khôthra-Sêr",
    "castle_village_U8_1": "Rakhôx-Lub",
    "castle_village_U8_2": "Rakhôx-Sîr",
    "castle_village_U8_3": "Rakhôx-Vand",
    "village_U1_1": "Umbar-Bûsh",
    "village_U1_2": "Umbar-Vargh",
    "village_U1_3": "Umbar-Tün",
    "village_U1_4": "Umbar-Mukh",
    "village_U2_1": "Jax-Khar",
    "village_U2_2": "Jax-Bahr",
    "village_U2_3": "Phanal-Khar",
    "village_U2_4": "Phanal-Lîn",

    # A - Khand-Aserai (Eastern/Mongol-Arabic)
    "castle_village_A11_1": "Shatag-Lub",
    "castle_village_A11_2": "Shatag-Sîr",
    "castle_village_A12_1": "Khatz-Vand",
    "castle_village_A12_2": "Khatz-Bûsh",
    "castle_village_A13_1": "Bôdras-Vargh",
    "castle_village_A13_2": "Bôdras-Tün",
    "castle_village_A14_1": "Kôna-Mukh",
    "castle_village_A14_2": "Kôna-Khar",
    "castle_village_A15_1": "Lajôr-Bahr",
    "castle_village_A15_2": "Lajôr-Argûn",
    "castle_village_A16_1": "Gatôr-Lîn",
    "castle_village_A16_2": "Gatôr-Sêr",
    "castle_village_A17_1": "Sôkhaz-Lub",
    "castle_village_A17_2": "Sôkhaz-Sîr",
    "castle_village_A18_1": "Kalmôkh-Vand",
    "castle_village_A18_2": "Kalmôkh-Bûsh",
    "castle_village_A19_1": "Kitôx-Vargh",
    "castle_village_A19_2": "Kitôx-Tün",
    "village_A10_1": "Khanôg-Mukh",
    "village_A10_2": "Khanôg-Khar",
    "village_A10_3": "Khanôg-Tün",
    "village_A10_4": "Gôr-Mukh",
    "village_A11_1": "Menôsh-Bahr",
    "village_A11_2": "Menôsh-Argûn",
    "village_A11_3": "Menôsh-Khar",
    "village_A12_1": "Jôret-Bahr",
    "village_A12_2": "Jôret-Sêr",
    "village_A12_3": "Jôret-Lub",
    "village_A13_1": "Naxar-Sîr",
    "village_A13_2": "Naxar-Argûn",
    "village_A13_3": "Dôl-Vand",
    "village_A14_1": "Damud-Bûsh",
    "village_A14_2": "Damud-Vargh",
    "village_A14_3": "Damud-Lîn",
    "village_A9_1": "Charg-Tün",
    "village_A9_2": "Charg-Mukh",
    "village_A9_3": "Charg-Khar",
    "village_A9_4": "Pôta-Bahr",

    # RU - Rhûn (Easterling/Wainrider)
    "castle_RU9": "Carndûr",  # castle renamed from placeholder "Castle RU9" (Carnen/Redwater corridor)
    "castle_village_RU1_1": "Mûrdûn-Krish",
    "castle_village_RU1_2": "Mûrdûn-Lub",
    "castle_village_RU1_3": "Mûrdûn-Sîr",
    "castle_village_RU2_1": "Tarlat-Krish",
    "castle_village_RU2_2": "Tarlat-Lub",
    "castle_village_RU2_3": "Arlan-Sîr",
    "castle_village_RU3_1": "Khôsar-Vand",
    "castle_village_RU3_2": "Khôsar-Bûsh",
    "castle_village_RU3_3": "Khôsar-Vargh",
    "castle_village_RU4_1": "Samôrn-Tün",
    "castle_village_RU4_2": "Samôrn-Mukh",
    "castle_village_RU4_3": "Samôrn-Khar",
    "castle_village_RU5_1": "Ulathar-Bahr",
    "castle_village_RU5_2": "Ulathar-Argûn",
    "castle_village_RU5_3": "Ulathar-Lîn",
    "castle_village_RU6_1": "Rôartar-Krish",
    "castle_village_RU6_2": "Rôartar-Lub",
    "castle_village_RU6_3": "Rôartar-Sîr",
    "castle_village_RU7_1": "Tôrcôin-Vand",
    "castle_village_RU7_2": "Tôrcôin-Bûsh",
    "castle_village_RU7_3": "Tôrcôin-Vargh",
    "castle_village_RU8_1": "Kôrash-Tün",
    "castle_village_RU8_2": "Kôrash-Mukh",
    "castle_village_RU8_3": "Kôrash-Khar",
    "castle_village_RU9_1": "Pôshtirn",
    "castle_village_RU9_2": "Argunbar",
    "castle_village_RU9_3": "Linberd",
    "castle_village_RU10_1": "Nôrakh-Vand",
    "castle_village_RU10_2": "Nôrakh-Bûsh",
    "castle_village_RU10_3": "Nôrakh-Vargh",
    "castle_village_RU11_1": "Ulbarath-Tün",
    "castle_village_RU11_2": "Ulbarath-Mukh",
    "castle_village_RU11_3": "Ulbarath-Khar",
    "castle_village_RU12_1": "Chôya-Bahr",
    "castle_village_RU12_2": "Chôya-Argûn",
    "castle_village_RU12_3": "Chôya-Lîn",
    "village_RU1_1": "Mistrand-Krish",
    "village_RU1_2": "Mistrand-Lub",
    "village_RU1_3": "Mistrand-Sîr",
    "village_RU1_4": "Mistrand-Vand",
    "village_RU2_1": "Lest-Bûsh",
    "village_RU2_2": "Lest-Vargh",
    "village_RU2_3": "Lest-Tün",
    "village_RU2_4": "Lest-Mukh",
    "village_RU3_1": "Vorgav-Khar",
    "village_RU3_2": "Vorgav-Bahr",
    "village_RU3_3": "Vorgav-Argûn",
    "village_RU3_4": "Vorgav-Lîn",
    "village_RU4_1": "Ôrush-Krish",
    "village_RU4_2": "Ôrush-Lub",
    "village_RU4_3": "Ôrush-Sîr",
    "village_RU4_4": "Ôrush-Vand",
    "village_RU5_1": "Sôrt-Bûsh",
    "village_RU5_2": "Sôrt-Vargh",
    "village_RU5_3": "Sôrt-Tün",
    "village_RU5_4": "Sôrt-Mukh",
    "village_RU6_1": "Kelep-Khar",
    "village_RU6_2": "Kelep-Bahr",
    "village_RU6_3": "Kelep-Argûn",
    "village_RU6_4": "Kelep-Lîn",
    "village_RU7_1": "Khôndol-Krish",
    "village_RU7_2": "Khôndol-Lub",
    "village_RU7_3": "Khôndol-Sîr",
    "village_RU7_4": "Khôndol-Vand",
    "village_RU8_1": "Iôrig-Bûsh",
    "village_RU8_2": "Iôrig-Vargh",
    "village_RU8_3": "Iôrig-Tün",
    "village_RU8_4": "Iôrig-Mukh",
}

print(f"Total names: {len(NAMES)}")
if len(set(NAMES.values())) != len(NAMES.values()):
    from collections import Counter
    c = Counter(NAMES.values())
    dups = {k: v for k, v in c.items() if v > 1}
    print("DUPLICATES:", dups)
    raise SystemExit("Duplicate names; aborting")
print("All names unique.")

ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData"
LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]


def patch(path, mode):
    with open(path, "rb") as f:
        data = f.read()
    text = data.decode("utf-8")
    count = 0
    for sid, new in NAMES.items():
        if mode == "master":
            pat = re.compile(r'(name="\{=Settlements\.Settlement\.name\.' + re.escape(sid) + r'\})[^"]*(")')
        else:
            pat = re.compile(r'(id="Settlements\.Settlement\.name\.' + re.escape(sid) + r'"\s+text=")[^"]*(")')
        text, n = pat.subn(lambda m, v=new: m.group(1) + v + m.group(2), text)
        count += n
    with open(path, "wb") as f:
        f.write(text.encode("utf-8"))
    return count


c = patch(os.path.join(ROOT, "settlements.xml"), "master")
print(f"\nsettlements.xml: {c} replacements")
for lang in LANGS:
    c = patch(os.path.join(ROOT, "Languages", lang, "loc_settlements.xml"), "loc")
    print(f"{lang}: {c} replacements")
print(f"\nGrand total: {c * 13 + len(NAMES)} string replacements (approx)")
