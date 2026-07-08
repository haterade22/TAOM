#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hand-curated per-fief building-level decisions for TAOM (lore + role, balanced).

This is the SOURCE OF TRUTH for the curation. Every one of the 221 towns/castles gets an explicit,
hand-assigned role tier + a one-line rationale, plus per-building overrides for the lore-significant
fiefs where the settlement's identity (the Black Gate, Cirith Ungol, Helm's Deep, Erebor, Orthanc)
overrides its prosperity number. A pinned deterministic expander turns each (tier, culture-flavor,
overrides) into the full 11/12-building roster, so the NUMBERS are consistent across cultures while
the JUDGMENT (which tier, which overrides) is hand-made per fief.

Outputs (run with no args):
  - tools/data/settlement_building_levels/<culture>.json   (fed to apply_settlement_buildings.py)
  - docs/reviews/settlement-buildings-audit-2026-07-08.md   (per-fief morning-audit artifact)

Levels are the STARTING levels a new campaign seeds. Valid 0-3; fortifications floors at 1.
Design intent: capitals & great fortresses maxed; ordinary fiefs moderate; remote holds sparse but
defensible; every fief still a real siege. Culture flavor nudges character (orc garrisons brutal &
civic-poor; dwarves wall-and-mason heavy; elves refined; Umbar mercantile).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(HERE, "data", "settlement_building_levels")
CURRENT_STATE = os.path.join(HERE, "reports", "settlement-buildings", "current_state.json")
AUDIT_DOC = os.path.join(HERE, "..", "docs", "reviews", "settlement-buildings-audit-2026-07-08.md")

# ---- Building order (canonical short names = id minus building_settlement_/building_castle_) ----
TOWN_ORDER = ["fortifications", "barracks", "training_fields", "guard_house", "siege_workshop",
              "tax_office", "marketplace", "warehouse", "mason", "waterworks", "courthouse", "roads_and_paths"]
CASTLE_ORDER = ["fortifications", "barracks", "training_fields", "guard_house", "siege_workshop",
                "castallans_office", "granary", "craftmans_quarters", "farmlands", "mason", "roads_and_paths"]

# ---- Pinned tier profiles (base levels, before culture flavor + per-fief overrides) ----
TIER_TOWN = {
    #                  frt brk trn grd sge tax mkt  wh msn wtr crt  rd
    "capital":        [3,  3,  3,  2,  3,  2,  2,  3,  3,  3,  3,  2],
    "fortress_town":  [3,  3,  2,  1,  3,  1,  1,  2,  2,  1,  1,  1],
    "trade_town":     [2,  2,  1,  1,  1,  2,  3,  3,  1,  2,  1,  2],
    "major":          [2,  2,  2,  1,  2,  2,  2,  2,  2,  2,  1,  2],
    "standard":       [2,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1],
    "minor":          [1,  1,  0,  0,  0,  1,  1,  1,  0,  1,  0,  1],
}
TIER_CASTLE = {
    #                  frt brk trn grd sge cst grn crf frm msn  rd
    "great_fortress": [3,  3,  2,  1,  2,  2,  2,  1,  2,  2,  1],
    "major":          [2,  2,  1,  1,  1,  1,  2,  1,  2,  1,  1],
    "standard":       [2,  1,  1,  0,  1,  1,  1,  0,  1,  0,  1],
    "minor":          [1,  1,  0,  0,  0,  0,  1,  0,  1,  0,  1],
    "watchtower":     [2,  1,  0,  0,  1,  0,  1,  0,  0,  0,  0],
}

# ---- Culture flavor (delta on the expanded base, per type; clamp 0-3, fort floored >=1) ----
CULTURE_FLAVOR = {
    "military": {"town": {"siege_workshop": 1, "barracks": 1, "waterworks": -1, "courthouse": -1},
                 "castle": {"siege_workshop": 1, "barracks": 1, "craftmans_quarters": -1}},
    "trade":    {"town": {"marketplace": 1, "warehouse": 1, "tax_office": 1},
                 "castle": {"castallans_office": 1, "granary": 1}},
    "dwarven":  {"town": {"mason": 1, "fortifications": 1},
                 "castle": {"mason": 1, "fortifications": 1, "craftmans_quarters": 1}},
    "elven":    {"town": {"mason": 1, "waterworks": 1, "roads_and_paths": 1},
                 "castle": {"mason": 1, "roads_and_paths": 1}},
}
FLAVOR_MAP = {c: "military" for c in ["mordor", "isengard", "gundabad", "mistymountainorcs", "goblin", "dolguldur"]}
FLAVOR_MAP.update({"umbar": "trade", "erebor": "dwarven"})
FLAVOR_MAP.update({c: "elven" for c in ["rivendell", "lothlorien", "mirkwood"]})

ALIAS = {"fort": "fortifications", "brk": "barracks", "trn": "training_fields", "grd": "guard_house",
         "sge": "siege_workshop", "tax": "tax_office", "mkt": "marketplace", "wh": "warehouse",
         "msn": "mason", "wtr": "waterworks", "crt": "courthouse", "rd": "roads_and_paths",
         "cst": "castallans_office", "grn": "granary", "crf": "craftmans_quarters", "frm": "farmlands"}


def dec(tier, why, **ov):
    return {"tier": tier, "why": why, "ov": {ALIAS.get(k, k): v for k, v in ov.items()}}


# =====================================================================================
#  DECISIONS — every town/castle, hand-assigned. Grouped by culture; towns then castles.
# =====================================================================================
DECISIONS = {
    # ---------------- GONDOR (Kingdom of Men — fortress-cities + Anduin/coast ports) ----------------
    "town_EW1": dec("capital", "Minas Tirith — the White City, greatest fortress of Men; every ward built."),
    "town_EW8": dec("fortress_town", "Ost Arndir — 'Ost' = fortress; garrison city of the marches."),
    "town_EW7": dec("major", "Bar Melui — prosperous inland city of Gondor."),
    "town_EW9": dec("major", "Calembel — town on the fords of Ciril in Lamedon."),
    "town_EW6": dec("trade_town", "Lond Cirion — a haven ('lond'); coastal trade city."),
    "town_EW5": dec("trade_town", "Dol Amroth — fortified swan-knight port-principality.", fort=3, sge=2),
    "town_EW4": dec("trade_town", "Pelargir — the great river-port of Gondor on Anduin."),
    "town_EW2": dec("fortress_town", "West Osgiliath — the ruined former capital; contested Anduin crossing."),
    "town_EW3": dec("fortress_town", "East Osgiliath — eastern half of the war-torn crossing city."),
    "town_EW10": dec("major", "Serelond — Belfalas coastal town."),
    "town_EW11": dec("standard", "Methir — lesser Gondorian town."),
    "castle_EW1": dec("major", "Harlond — the harbour-fort of Minas Tirith on Anduin."),
    "castle_EW4": dec("great_fortress", "Cair Andros — the island fortress guarding the northern Anduin approach.", fort=3, sge=2),
    "castle_EW3": dec("major", "Edhellond — ancient elven haven-fort near Dol Amroth."),
    "castle_EW7": dec("major", "Bar-en-Siril — strong Gondorian keep."),
    "castle_EW12": dec("major", "Linhir — fort at the fords of Gilrain, gate to Lebennin."),
    "castle_EW2": dec("standard", "Glanhir — Gondorian border keep."),
    "castle_EW9": dec("standard", "Caras Tolfalas — keep on the isle of Tolfalas."),
    "castle_EW6": dec("standard", "Morlad — Gondorian hill keep."),
    "castle_EW8": dec("standard", "Hyarpendë — southern coastal keep."),
    "castle_EW15": dec("standard", "Amonost — 'fortress-hill'; watch keep.", fort=2),
    "castle_EW16": dec("standard", "Erethir — lesser Gondorian keep."),
    "castle_EW10": dec("standard", "Methrast — Lebennin keep."),
    "castle_EW11": dec("standard", "Barad Harn — 'south tower'; border watchtower keep.", fort=2),
    "castle_EW14": dec("standard", "Tumladen — keep of the hidden vale near the capital."),
    "castle_EW13": dec("standard", "Méreharn — lesser Gondorian keep."),
    "castle_EW5": dec("watchtower", "Min-Rimmon — one of Gondor's warning-beacons; hilltop signal fort.", fort=2, sge=1),

    # ---------------- MORDOR (the Dark Land — dark citadels + pass-fortresses) ----------------
    "town_ES1": dec("capital", "Barad-dûr — the Dark Tower, Sauron's supreme fortress."),
    "town_ES2": dec("capital", "Minas Morgul — the Tower of Sorcery; maxed dread citadel-city."),
    "town_ES3": dec("fortress_town", "Durthang — fortress-city commanding Udûn."),
    "town_ES4": dec("fortress_town", "Seregost — fortress-city of the north-east marches."),
    "town_ES5": dec("major", "Thaurband — walled town of the Nurn approaches."),
    "town_ES6": dec("standard", "Naerband — lesser Mordor town."),
    "castle_ES3": dec("great_fortress", "Cirith Ungol — the tower guarding the high pass into Mordor.", fort=3, sge=2),
    "castle_ES2": dec("great_fortress", "Carach Angren — the Isenmouthe, gated pass into Udûn.", fort=3),
    "castle_ES1": dec("great_fortress", "The Morannon — the Black Gate; Mordor's impregnable front door (lore over prosperity).", fort=3, sge=3),
    "castle_ES6": dec("great_fortress", "Cirith Nargil — fortified pass of the southern fence.", fort=3),
    "castle_ES4": dec("major", "Mornaur — strong dark keep, Mordor's wealthiest castle."),
    "castle_ES7": dec("major", "Barad Wath — strong Nurn keep (fort3 reserved for the legendary Mordor fortresses)."),
    "castle_ES5": dec("major", "Barad Nûrn — keep over the inland sea of Núrnen."),
    "castle_ES8": dec("standard", "Lûglurag — orc-manned border hold."),

    # ---------------- VLANDIA = ROHAN (horse-lords — muster halls, modest walls, Helm's Deep) ----------------
    "town_V1": dec("capital", "Edoras — Meduseld, seat of the Kings; great host but only a hill-dike, not stone walls.", fort=2, sge=1),
    "town_V2": dec("fortress_town", "Helm's Deep — the impregnable refuge of the Westfold.", fort=3, sge=3),
    "town_V6": dec("major", "Langhold — prosperous Rohirric town."),
    "town_V3": dec("major", "Aldburg — old seat of Eorl, muster of the Eastfold.", brk=3, trn=3),
    "town_V4": dec("major", "Eaworth — Rohirric market town of the wolds."),
    "town_V7": dec("standard", "Grimslade — home of Grimbold; Westfold town."),
    "town_V5": dec("standard", "Aldenburg — small Rohirric town."),
    "castle_V2": dec("major", "Cliving — Rohirric hill-fort."),
    "castle_V3": dec("major", "Fenmarch — muster point of the eastern marches.", brk=3, trn=2),
    "castle_V6": dec("standard", "Caleus Castle — Rohirric keep."),
    "castle_V7": dec("standard", "Gramburg — Rohirric keep."),
    "castle_V4": dec("standard", "Starkmoore — moorland keep."),
    "castle_V5": dec("standard", "Hiltbolt — Rohirric keep."),
    "castle_V1": dec("minor", "Marton — small Rohirric hold (lowest-prosperity fief)."),

    # ---------------- EMPIRE = DUNLAND (hill-men — rugged hill strongholds) ----------------
    "town_EN1": dec("fortress_town", "Epicrotea — chief Dunlending hill-stronghold town."),
    "town_EN2": dec("major", "Diathma — prosperous Dunland town."),
    "town_EN3": dec("major", "Saneopa — Dunland market town."),
    "castle_EN7": dec("major", "Thror's Coomb — strong hill keep."),
    "castle_EN6": dec("major", "Barnavon — Dunland keep."),
    "castle_EN4": dec("major", "Lhanuch — the Dunlending gathering-place; clan seat."),
    "castle_EN3": dec("standard", "Tûr Morva — 'dark tower'; hill keep.", fort=2),
    "castle_EN5": dec("standard", "Lhan Tarren — Dunland keep."),
    "castle_EN8": dec("standard", "Dûvodaiad — Dunland keep."),

    # ---------------- KHUZAIT = RHÛN / KHAND (Easterlings — cavalry cities, steppe forts) ----------------
    "town_K3": dec("major", "Ardûvar — chief Easterling city of Rhûn; capital-strength walls (matches the Khand capital Sturlurtsa).", fort=3, brk=3, trn=2),
    "town_RU7": dec("major", "Khûndol — prosperous Rhûn city."),
    "town_RU8": dec("major", "Iôrig — prosperous Rhûn city."),
    "town_RU6": dec("major", "Kelepar — Rhûn city."),
    "town_RU1": dec("major", "Mistrand — known city of the Wainriders of Rhûn."),
    "town_K1": dec("major", "Sturlurtsa Khand — capital of the Variags of Khand.", fort=3),
    "town_RU5": dec("standard", "Sârt — Rhûn town."),
    "town_RU2": dec("standard", "Lest — Rhûn town."),
    "town_RU3": dec("standard", "Vorgavuld — Rhûn town."),
    "town_RU4": dec("standard", "Ûrushban — Rhûn town."),
    "town_K4": dec("standard", "Yanûk Anlê — Khand town."),
    "town_K2": dec("standard", "Lûrmsakun — Khand town."),
    "castle_K3": dec("major", "Ôvathikor — strong Easterling keep."),
    "castle_K6": dec("standard", "Khondûr — Rhûn keep."),
    "castle_RU9": dec("standard", "Carndûr — Rhûn keep."),
    "castle_RU8": dec("standard", "Kârashûn — Rhûn keep."),
    "castle_RU7": dec("standard", "Tôrcâin — Rhûn keep."),
    "castle_RU2": dec("standard", "Tarlat Arlan — Rhûn keep."),
    "castle_RU3": dec("standard", "Khûsar — Rhûn keep."),
    "castle_RU4": dec("standard", "Samârnûl — Rhûn keep."),
    "castle_RU5": dec("standard", "Ulathar — Rhûn keep."),
    "castle_RU6": dec("standard", "Rûartar — Rhûn keep."),
    "castle_RU1": dec("standard", "Mârdûn — Rhûn keep."),
    "castle_RU10": dec("standard", "Nîrakh — Rhûn keep."),
    "castle_RU11": dec("standard", "Ulbarath — Rhûn keep."),
    "castle_RU12": dec("standard", "Chêya — Rhûn keep."),
    "castle_K4": dec("minor", "Krûk Azhanna — small Khand hold."),
    "castle_K5": dec("minor", "Dagmathar — small Khand hold."),
    "castle_K2": dec("minor", "Varnakh — small Khand hold."),
    "castle_K1": dec("minor", "Síransíra — remote steppe hold."),
    "castle_K7": dec("minor", "Klagûl — remote steppe hold."),

    # ---------------- ASERAI = HARAD (Haradrim — desert trade cities, sun-baked forts) ----------------
    "town_A4": dec("trade_town", "Chelkarâ — chief Haradrim caravan-city."),
    "town_A2": dec("major", "Kes Marzûk — prosperous Harad city."),
    "town_A1": dec("major", "Korb Taskral — Harad city."),
    "town_A5": dec("standard", "Parzee — Harad town."),
    "town_A3": dec("standard", "Hurum Kâna — small Harad town."),
    "castle_A3": dec("major", "Kadar Kâraba — strong desert keep."),
    "castle_A2": dec("major", "Kes Shadoul — Harad keep."),
    "castle_A4": dec("standard", "Yânu — desert keep."),
    "castle_A5": dec("standard", "Pazakhêra — desert keep."),
    "castle_A9": dec("standard", "Khâb Antâx — desert keep."),
    "castle_A11": dec("standard", "Dân Shatagân — desert keep."),
    "castle_A12": dec("standard", "Khatzâla — desert keep."),
    "castle_A8": dec("standard", "Sâr Marsag — desert keep."),
    "castle_A6": dec("minor", "Sul Madash — small desert hold."),
    "castle_A7": dec("minor", "Hârmaka — small desert hold."),
    "castle_A1": dec("minor", "Nagakhêdi — remote desert hold."),

    # ---------------- SHAGHANA (southern Harad group — hinterland towns & forts) ----------------
    "town_A6": dec("trade_town", "Zajâna — chief Shaghana trade-city."),
    "town_A10": dec("major", "Khanâg Gúr — Shaghana city."),
    "town_A11": dec("standard", "Menêsh — Shaghana town."),
    "town_A8": dec("standard", "Sormedân — Shaghana town."),
    "town_A7": dec("standard", "Chatâk — small Shaghana town."),
    "castle_FH8": dec("standard", "Urutúsh — Shaghana keep."),
    "castle_FH6": dec("standard", "Onabóha — Shaghana keep."),
    "castle_A13": dec("standard", "Bâdrasag — Shaghana keep."),
    "castle_A14": dec("standard", "Kâna — Shaghana keep."),
    "castle_A15": dec("standard", "Lajór — Shaghana keep."),
    "castle_A16": dec("minor", "Gatúr — small Shaghana hold."),
    "castle_A17": dec("minor", "Sâkhazai — small Shaghana hold."),
    "castle_A18": dec("minor", "Kalmâkhila — small Shaghana hold."),
    "castle_A19": dec("minor", "Khâb Kitâx — small Shaghana hold."),

    # ---------------- ABANISSA (far-southern group — frontier towns & forts) ----------------
    "town_A9": dec("major", "Charganpâta — chief Abanissa city."),
    "town_A12": dec("standard", "Jîret — Abanissa town."),
    "town_A13": dec("standard", "Naxar Dâl — Abanissa town."),
    "town_A14": dec("standard", "Damudûr — Abanissa town."),
    "castle_FH9": dec("standard", "Zakhûr — Abanissa keep."),
    "castle_FH7": dec("standard", "Taridím — Abanissa keep."),
    "castle_FH1": dec("standard", "Archadâzar — Abanissa keep."),
    "castle_FH2": dec("standard", "Erakúndo — Abanissa keep."),
    "castle_FH3": dec("minor", "Gebarakôr — small Abanissa hold."),
    "castle_FH4": dec("minor", "Shâlahin — small Abanissa hold."),
    "castle_FH5": dec("minor", "Kôth Rau — small Abanissa hold."),

    # ---------------- EREBOR (Dwarves — wall-and-mason heavy mountain holds) ----------------
    "town_E1": dec("capital", "Erebor — the Lonely Mountain, greatest dwarf-kingdom; master-built."),
    "town_E2": dec("major", "Járnfast — dwarf-hold of the Iron Hills."),
    "town_E3": dec("major", "Skárhald — dwarf-hold."),
    "town_E4": dec("major", "Azanûlibar-dûm — dwarf-hold near the Dimrill Dale."),
    "castle_E1": dec("major", "Irongap — the gate-fort of the Iron Hills."),
    "castle_E4": dec("standard", "Buzra-sâlan — dwarven keep."),
    "castle_E5": dec("standard", "Mesem-garak — dwarven keep."),
    "castle_E7": dec("standard", "Zul-mazal — dwarven keep."),
    "castle_E9": dec("standard", "(unnamed stub 'Castle E9' — dwarven keep; NAME MISSING, flag for user)."),
    "castle_E2": dec("standard", "Flogalith — dwarven keep."),
    "castle_E3": dec("standard", "Anatrâd — dwarven keep."),
    "castle_E6": dec("standard", "Grymmclúd — dwarven keep."),
    "castle_E8": dec("standard", "Frósthel — dwarven keep."),

    # ---------------- STURGIA = DALE / NORTHMEN (Dale market-city + northern keeps) ----------------
    "town_S1": dec("trade_town", "Dale — the great restored market-city of the North under the Mountain."),
    "town_S4": dec("major", "Liutburg — prosperous Northman town."),
    "town_S3": dec("major", "Eldby — Northman town."),
    "town_S2": dec("standard", "Stranding — Northman town."),
    "town_S5": dec("standard", "Vargfell — small Northman town."),
    "castle_S4": dec("major", "Tham Aeldir — strong Northman keep."),
    "castle_S1": dec("major", "Westoft — Northman keep."),
    "castle_S2": dec("standard", "Garthness — Northman keep."),
    "castle_S5": dec("standard", "Torndal — Northman keep."),
    "castle_S6": dec("standard", "Taigbarn — Northman keep."),
    "castle_S7": dec("standard", "Yoldbryn — Northman keep."),
    "castle_S3": dec("minor", "Bridgethorp — small Northman hold."),

    # ---------------- UMBAR (Corsairs — mercantile port-fortresses; trade flavor) ----------------
    "town_U1": dec("capital", "Umbar — the great corsair-haven and port-fortress of the South.", fort=3, sge=2),
    "town_U2": dec("trade_town", "Jax Phanal — corsair port-city."),
    "castle_U8": dec("major", "Rakhâx — strong corsair coastal fort."),
    "castle_U6": dec("standard", "Hâtab — corsair coastal fort."),
    "castle_U7": dec("standard", "Khûthra — corsair coastal fort."),
    "castle_U4": dec("standard", "Zimrathôr — corsair coastal fort."),
    "castle_U5": dec("standard", "Zamarzîr — corsair coastal fort."),
    "castle_U1": dec("standard", "Bej Magha — corsair coastal fort."),
    "castle_U3": dec("standard", "Bêluzir — corsair coastal fort."),
    "castle_U2": dec("standard", "Azruphâr — corsair coastal fort."),

    # ---------------- MISTYMOUNTAINORCS (Moria orcs — the deeps of Khazad-dûm + passes) ----------------
    "town_MM1": dec("fortress_town", "Western Moria — the West-hall deeps of Khazad-dûm; natural fortress."),
    "town_MM2": dec("fortress_town", "Eastern Moria — the East-hall deeps by the Dimrill Gate."),
    "town_MM3": dec("standard", "Nanduhirion — orc-holding of the Dimrill Dale."),
    "castle_MM1": dec("standard", "Caradhras — orc-fort on the Redhorn.", fort=3),
    "castle_MM4": dec("watchtower", "Redhorn Gate — the high pass over the Misty Mountains.", fort=3, sge=1),
    "castle_MM6": dec("watchtower", "Hollin Gate — the West-gate pass toward Eregion.", fort=2, sge=1),
    "castle_MM7": dec("standard", "Sirannon — the Gate-stream fort."),
    "castle_MM2": dec("standard", "Celebdil — orc-fort on the Silvertine.", fort=3),
    "castle_MM3": dec("standard", "Fanuidhol — orc-fort on the Cloudyhead.", fort=3),
    "castle_MM5": dec("standard", "Methedras — orc-fort of the last peak."),

    # ---------------- MORDOR done above ----------------

    # ---------------- DOLGULDUR (Sauron's old fastness in Mirkwood; military) ----------------
    "town_DG1": dec("capital", "Dol Guldur — the Hill of Sorcery, dark fortress of southern Mirkwood."),
    "castle_DG2": dec("major", "Maufulug — strong forest-fastness of Dol Guldur."),
    "castle_DG3": dec("standard", "Bûrzkala — dark-forest keep."),
    "castle_DG1": dec("standard", "Amon Angened — dark-forest keep."),
    "castle_DG4": dec("standard", "Dannenglor — dark-forest keep."),
    "castle_DG5": dec("standard", "Ashúrz — dark-forest keep."),

    # ---------------- ISENGARD (Saruman — the Ring of Isengard + Orthanc; military + mason) ----------------
    "town_isengard": dec("capital", "Orthanc — Saruman's tower within the Ring of Isengard; fortress and war-forge.", msn=3),
    "castle_orthanc_gate": dec("great_fortress", "Orthanc Gate — the outer wall and gate of the Ring of Isengard.", fort=3, sge=2),
    "castle_I1": dec("standard", "Nan Angranost — Isengard border keep."),
    "castle_I2": dec("standard", "Forthbrond — Isengard border keep."),

    # ---------------- GUNDABAD (northern orc-capital + mountain holds; military) ----------------
    "town_G1": dec("capital", "Mount Gundabad — the great orc-capital and desecrated dwarf-birthplace."),
    "town_G2": dec("fortress_town", "Mount Gram — orc mountain-stronghold."),
    "castle_G5": dec("major", "Shúrdmúl — strong orc mountain-fort."),
    "castle_G3": dec("standard", "Mazôglod — orc mountain-fort."),
    "castle_G2": dec("standard", "Dûglarshun — orc mountain-fort."),
    "castle_G4": dec("standard", "Framsburg — old Northman fort held by orcs."),
    "castle_G1": dec("standard", "(unnamed stub 'Castle G1' — orc mountain-fort; NAME MISSING, flag for user)."),

    # ---------------- GOBLIN (Goblin-town warrens; crude military) ----------------
    "town_GT1": dec("fortress_town", "Goblin Town — the great goblin warren under the High Pass."),
    "town_GBC1": dec("standard", "Blue Craig — goblin crag-hold."),
    "castle_GBC3": dec("standard", "Skarnak — goblin crag-fort."),
    "castle_GBC4": dec("standard", "Bolgkrag — goblin crag-fort."),
    "castle_GBC1": dec("minor", "Krathol — small goblin hold."),
    "castle_GBC2": dec("minor", "Gorgrim — small goblin hold."),

    # ---------------- MIRKWOOD (Wood-elves of Thranduil; elven flavor) ----------------
    "town_M1": dec("capital", "Felegoth — the Elvenking's caverns, Thranduil's underground hall-fortress."),
    "town_M2": dec("major", "Caras Laerolin — woodland-elf settlement."),
    "castle_M1": dec("standard", "Glad Thaw — woodland-elf outpost."),
    "castle_M2": dec("standard", "Gwígar — woodland-elf outpost."),
    "castle_M3": dec("standard", "Torech Emel — woodland-elf outpost."),
    "castle_M4": dec("standard", "Lasgalor — woodland-elf outpost."),
    "castle_M5": dec("standard", "Imnagath — woodland-elf outpost."),

    # ---------------- RIVENDELL (Imladris + the Grey Havens; elven flavor) ----------------
    "town_R1": dec("capital", "Rivendell — Imladris, the Last Homely House; hidden refuge of Elrond.", sge=2),
    "town_LN1": dec("trade_town", "Mithlond — the Grey Havens, the great elven ship-haven."),
    "castle_R5": dec("major", "Fennas Drúnin — the fortified crossing of the Hoarwell."),
    "castle_R4": dec("standard", "Baracharn — elven watch-keep."),
    "castle_R2": dec("standard", "Hithaegrist — elven watch-keep."),
    "castle_R3": dec("standard", "Nan Tornaeth — elven watch-keep."),

    # ---------------- LOTHLORIEN (Galadhrim of Caras Galadhon; elven flavor) ----------------
    "town_L1": dec("capital", "Caras Galadhon — the great tree-city of the Galadhrim, Galadriel's seat."),
    "castle_L3": dec("standard", "Tol Calan — Lórien guard-post."),
    "castle_L1": dec("standard", "Cerin Amroth — the hallowed mound at the heart of Lórien."),
    "castle_L2": dec("standard", "Talas Duiren — Lórien guard-post."),
}


def full_id(short, is_castle):
    return ("building_castle_" if is_castle else "building_settlement_") + short


def expand(fief, decision):
    """(tier, flavor, overrides) -> {full_building_id: level}. Deterministic + clamped."""
    is_castle = fief["is_castle"]
    order = CASTLE_ORDER if is_castle else TOWN_ORDER
    table = TIER_CASTLE if is_castle else TIER_TOWN
    tier = decision["tier"]
    if tier not in table:
        raise SystemExit(f"FATAL: {fief['id']} uses unknown {'castle' if is_castle else 'town'} tier '{tier}'.")
    levels = dict(zip(order, table[tier]))
    # culture flavor
    flavor = FLAVOR_MAP.get(fief["culture"])
    if flavor:
        for short, delta in CULTURE_FLAVOR[flavor]["castle" if is_castle else "town"].items():
            if short in levels:
                levels[short] += delta
    # per-fief overrides (win last)
    for short, val in decision["ov"].items():
        if short not in levels:
            raise SystemExit(f"FATAL: {fief['id']} override '{short}' not valid for a {'castle' if is_castle else 'town'}.")
        levels[short] = val
    # clamp + fortifications floor
    for short in order:
        levels[short] = max(0, min(3, levels[short]))
    levels["fortifications"] = max(1, levels["fortifications"])
    return {full_id(s, is_castle): levels[s] for s in order}


def main():
    state = json.load(open(CURRENT_STATE, encoding="utf-8"))
    by_id = {f["id"]: f for f in state}

    missing = [f["id"] for f in state if f["id"] not in DECISIONS]
    extra = [sid for sid in DECISIONS if sid not in by_id]
    if missing or extra:
        raise SystemExit(f"FATAL: coverage mismatch.\n  missing decisions: {missing}\n  unknown ids: {extra}")

    os.makedirs(DATA_DIR, exist_ok=True)
    by_culture = {}
    for fief in state:
        roster = expand(fief, DECISIONS[fief["id"]])
        by_culture.setdefault(fief["culture"], {})[fief["id"]] = roster

    for culture, rosters in sorted(by_culture.items()):
        path = os.path.join(DATA_DIR, f"{culture}.json")
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(rosters, fh, ensure_ascii=False, indent=2)
    print(f"Wrote {len(by_culture)} culture JSON files to {DATA_DIR}")

    write_audit_doc(state, by_id)
    print(f"Wrote audit doc: {os.path.normpath(AUDIT_DOC)}")


def write_audit_doc(state, by_id):
    def short(bid):
        return bid.replace("building_settlement_", "").replace("building_castle_", "")

    order_culture = ["gondor", "mordor", "vlandia", "empire", "rohan", "isengard", "gundabad",
                     "mistymountainorcs", "goblin", "dolguldur", "erebor", "mirkwood", "rivendell",
                     "lothlorien", "sturgia", "khuzait", "aserai", "shaghana", "abanissa", "umbar"]
    cultures = sorted({f["culture"] for f in state}, key=lambda c: (order_culture.index(c) if c in order_culture else 99, c))

    lines = ["# Settlement Building Levels — Curation Audit (2026-07-08)", "",
             "Lore + role, balanced. Hand-curated per fief; `->` shows current -> proposed. Levels 0-3; "
             "fortifications floors at 1. Towns = 12 `building_settlement_*`; castles = 11 `building_castle_*`.",
             "Applied to LIVE `TAOM_Map/ModuleData/settlements.xml` (seeds NEW campaigns only).", ""]
    for culture in cultures:
        fiefs = [f for f in state if f["culture"] == culture]
        towns = sorted([f for f in fiefs if not f["is_castle"]], key=lambda f: -f["prosperity"])
        castles = sorted([f for f in fiefs if f["is_castle"]], key=lambda f: -f["prosperity"])
        lines.append(f"## {culture}  ({len(towns)} towns, {len(castles)} castles)\n")
        for group, items in (("Towns", towns), ("Castles", castles)):
            if not items:
                continue
            lines.append(f"### {culture} — {group}\n")
            for f in items:
                d = DECISIONS[f["id"]]
                proposed = expand(f, d)
                order = list(proposed.keys())
                cur = f["buildings"]
                cells = []
                for bid in order:
                    o, n = cur.get(bid, 0), proposed[bid]
                    mark = f"{o}->{n}" if o != n else f"{n}"
                    cells.append(f"{short(bid)} {mark}")
                lines.append(f"**{f['name']}** (`{f['id']}`) — _{d['tier']}_ — pros {f['prosperity']}")
                lines.append("  " + " · ".join(cells))
                lines.append(f"  → {d['why']}\n")
    with open(AUDIT_DOC, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))


if __name__ == "__main__":
    sys.exit(main())
