#!/usr/bin/env python3
"""Add village <Settlement> rows to the LIVE TAOM_Map/settlements.xml, plus their 12 loc rows.

The map author places village entities in the worldmap scene (Main_map/scene.xscene). Each one
needs a matching <Settlement> row in settlements.xml or it is inert, and the editor's own save path
(SandBox.View SettlementPositionScript.OnSceneSave) only writes positions back for ids it finds in
that XML. This script writes the rows; the VILLAGES table below is the batch.

Positions: taken from the scene entity's transform when the entity is already saved (first two
numbers of `<transform position="x, y, z"`). When it is not, the bound settlement's position plus a
small per-parent offset is written and the row is printed as PLACEHOLDER. That is safe only because
the editor overwrites posX/posY from the entity on the next scene save; DO NOT start a campaign
while a PLACEHOLDER row has no scene entity, SettlementVisual.OnStartup NREs at map load (#269).
`--check` is the gate for that: it fails on any table id whose master row, loc row or scene entity
is missing, whose master position drifted from the scene transform, or whose culture or bound
differs from the table.

Master rows are appended at the END of the file, never after an anchor (append_settlements): a
save made before the batch still loads this file (until the distance cache is rebuilt with the new ids,
#1041), and a new village or town ahead of an existing
row makes that save skip the existing row (2026-10-07, docs/modding/settlements.md "Add"). The
row's `after` anchor (default INSERT_AFTER) now places only its loc rows, after that anchor's last
row in each language, with the English display name in all 12 (the translation backlog, #763,
later translates or transliterates it). A name
another settlement already carries is refused, and named by `--check` (name_collisions). A row
whose `bound` settlement has no row is refused, and named by `--check`. `hearth` is per row too (default HEARTH):
the #597 Gondor rows sit at 350 like their EW10/EW11 castle-village neighbours; the Isengard and
Gundabad rows at those cultures' 500 floor. Idempotent per id: an id already in the master is skipped. Byte discipline per
.claude/rules/moduledata-validation.md: binary read, each file's own BOM and newline preserved
(the master carries a BOM and CRLF, the loc files no BOM and CR CR LF; both are detected, not assumed), a non-.xml backup before the
write, and every written document is parsed first.

Precedent: tools/add_bluecraig_castles.py (#270). Next batch: append rows to VILLAGES.

Usage:
  python tools/add_map_villages.py            # dry run: prints the plan and each position's source
  python tools/add_map_villages.py --apply    # writes the live master + 12 loc files (+ backups)
  python tools/add_map_villages.py --check    # exit 1 on any missing row / entity / drifted position / culture or bound mismatch
"""
import argparse
import datetime
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import namedtuple
from xml.sax.saxutils import quoteattr

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import name_tags  # noqa: E402
from _gamedir import game_dir  # noqa: E402

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
MAP = GAME + r"/Modules/TAOM_Map"
LIVE = MAP + r"/ModuleData/settlements.xml"
SCENE = MAP + r"/SceneObj/Main_map/scene.xscene"
LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]
CACHE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "translation_cache")

# Every VillageType id in the live file (21) plus the ones DefaultVillageTypes registers that TAOM
# does not use yet. add_bluecraig_castles.py's copy omitted the five horse ranches.
VALID_VILLAGE_TYPES = {
    "swine_farm", "cattle_farm", "sheep_farm", "wheat_farm", "vineyard", "fisherman", "lumberjack",
    "iron_mine", "silver_mine", "clay_mine", "salt_mine", "flax_plant", "date_farm", "olive_trees",
    "silk_plant", "trapper", "steppe_horse_ranch", "vlandian_horse_ranch", "europe_horse_ranch",
    "desert_horse_ranch", "sturgian_horse_ranch",
}

# culture -> (background_mesh, wait_mesh, castle_background_mesh): the panel meshes every village
# of that culture already uses in the live file.
MESHES = {
    "isengard": ("gui_bg_village_empire", "wait_empire_village", "gui_bg_castle_empire"),
    "gondor": ("gui_bg_village_empire", "wait_empire_village", "gui_bg_castle_empire"),
    "gundabad": ("gui_bg_village_sturgia", "wait_sturgia_village", "gui_bg_castle_sturgia"),
    "arthedain": ("gui_bg_village_empire", "wait_empire_village", "gui_bg_castle_empire"),
    "cardolan": ("gui_bg_village_empire", "wait_empire_village", "gui_bg_castle_empire"),
    "rhudaur": ("gui_bg_village_empire", "wait_empire_village", "gui_bg_castle_empire"),
    "bluecraig": ("gui_bg_village_sturgia", "wait_sturgia_village", "gui_bg_castle_sturgia"),
    "lindon": ("gui_bg_village_battania", "wait_battania_village", "gui_bg_castle_battania"),
    "eredluin": ("gui_bg_village_sturgia", "wait_sturgia_village", "gui_bg_castle_sturgia"),
    "anduinvale": ("gui_bg_village_sturgia", "wait_sturgia_village", "gui_bg_castle_sturgia"),
    "angaladh": ("gui_bg_village_sturgia", "wait_sturgia_village", "gui_bg_castle_sturgia"),
}
# Dorwinion and South Rhovanion (#1062): their own cultures.
MESHES["dorwinion"] = ("gui_bg_village_sturgia", "wait_sturgia_village", "gui_bg_castle_sturgia")  # Dale's village panels, as anduinvale
MESHES["southrhovanion"] = ("gui_bg_village_vlanda", "wait_vlandia_village", "gui_bg_castle_vlanda")  # Rohan's village panels (vanilla vlandia)
HEARTH = "500"  # the SETTLEMENT_ECONOMY_FLOOR for isengard (tools/settlement_economy_floor.json)

# hearth and after are optional per row: hearth defaults to HEARTH, after to INSERT_AFTER (anchor_of).
Village = namedtuple("Village", "id name bound village_type culture scene hearth after", defaults=(HEARTH, None))

# The batch. Bindings follow the sibling ids already in the file (castle_village_isengard_a is on
# castle_orthanc_gate, village_isengard_a on town_isengard, castle_village_I2_1..3 on castle_I2).
# Names are Sindarin in the Angrenost style (docs/reference/taom-map-settlement-naming.md, region I)
# and renameable later through tools/Apply-MapVillageNames.py.
VILLAGES = [
    Village("castle_village_isengard_b", "Parth Angren",   "castle_orthanc_gate", "wheat_farm", "isengard", "empire_village_a"),
    Village("castle_village_isengard_c", "Tawarlad",       "castle_orthanc_gate", "swine_farm", "isengard", "empire_village_b"),
    Village("castle_village_isengard_d", "Eryn Methed",    "castle_orthanc_gate", "lumberjack", "isengard", "empire_village_c"),
    Village("village_isengard_b",        "Talath Curunír", "town_isengard",       "wheat_farm", "isengard", "empire_village_d"),
    Village("village_isengard_c",        "Athrad Angren",  "town_isengard",       "fisherman",  "isengard", "empire_village_e"),
    Village("village_isengard_d",        "Amon Thôn",      "town_isengard",       "sheep_farm", "isengard", "empire_village_f"),
    Village("village_isengard_e",        "Nan Gwath",      "town_isengard",       "swine_farm", "isengard", "empire_village_g"),
    Village("village_isengard_f",        "Groth Morn",     "town_isengard",       "iron_mine",  "isengard", "empire_village_h"),
    Village("castle_village_I2_4",       "Angroth",        "castle_I2",           "iron_mine",  "isengard", "empire_village_i"),
    # #597 (2026-09-13): Serelond (town_EW10) and Methir (town_EW11) held no village at all. The
    # map author placed the entities first; village_EW11_3 had none at authoring time and lands as
    # a PLACEHOLDER until the scene is saved. Sindarin names per the EW row of
    # docs/reference/taom-map-settlement-naming.md; hearth 350 matches castle_village_EW10/EW11_*;
    # scenes are the four taom_gondor_village_00N_forceatmo folders every EW village already uses.
    # The two villages rebound from castle_EW7 (village_EW10_1/_2) belong to rename_map_settlements.py.
    Village("village_EW10_3", "Aerlond",      "town_EW10", "fisherman",   "gondor", "taom_gondor_village_001_forceatmo", hearth="350", after="town_EW10"),
    Village("village_EW11_1", "Parth Mallen", "town_EW11", "wheat_farm",  "gondor", "taom_gondor_village_003_forceatmo", hearth="350", after="town_EW11"),
    Village("village_EW11_2", "Nan Laeg",     "town_EW11", "sheep_farm",  "gondor", "taom_gondor_village_004_forceatmo", hearth="350", after="town_EW11"),
    Village("village_EW11_3", "Emyn Caran",   "town_EW11", "olive_trees", "gondor", "taom_gondor_village_002_forceatmo", hearth="350", after="town_EW11"),
    # #597, same evening: Framsburg (castle_G4) was the third and last fortification with no village.
    # Gundabad hearth is that culture's 500 economy floor (the HEARTH default); panel meshes and
    # sturgia_village_* scenes as every G-region village; Black Speech names per the naming doc's G row.
    # Each Gundabad fief's villages share a stem (Düglar-, Mazūg-, Shôrd-, Gund-, Gram-); Framsburg's is
    # the Northman name the orcs kept. -bosh is what this map's swine farms carry (Bagmosh, Gundbosh).
    Village("castle_village_G4_1", "Fram-bûrz", "castle_G4", "wheat_farm", "gundabad", "sturgia_village_g", after="castle_G4"),
    Village("castle_village_G4_2", "Fram-bosh", "castle_G4", "swine_farm", "gundabad", "sturgia_village_h", after="castle_G4"),
    # Arthedain (2026-10-07): the 22 villages Mike placed with the kingdom's ten fortifications
    # (tools/add_map_fortifications.py writes those first; each village lands after its own).
    # Names approved by Mike; Bree-land's four are canon (Staddle, Combe, Archet, the Chetwood).
    # Hearth 300 against Gondor's 350-366: a poor realm, and not on the economy-floor spec.
    # Scenes are Gondor's four village scenes in rotation, the entities being Gondor copies.
    Village("village_AN2_1", "Norbury",         "town_AN2",    "wheat_farm",  "arthedain", "taom_gondor_village_001_forceatmo", hearth="300", after="town_AN2"),
    Village("village_AN2_2", "Garth Erain",     "town_AN2",    "cattle_farm", "arthedain", "taom_gondor_village_002_forceatmo", hearth="300", after="town_AN2"),
    Village("village_AN2_3", "Talath Forn",     "town_AN2",    "sheep_farm",  "arthedain", "taom_gondor_village_003_forceatmo", hearth="300", after="town_AN2"),
    Village("village_AN2_4", "Emyn Forn",       "town_AN1",    "iron_mine",   "arthedain", "taom_gondor_village_004_forceatmo", hearth="300", after="town_AN2"),
    Village("village_AN1_1", "Lond Nenuial",    "town_AN1",    "fisherman",   "arthedain", "taom_gondor_village_001_forceatmo", hearth="300", after="town_AN1"),
    Village("village_AN1_2", "Parth Uial",      "town_AN1",    "wheat_farm",  "arthedain", "taom_gondor_village_002_forceatmo", hearth="300", after="town_AN1"),
    Village("village_AN1_3", "Tol Uial",        "town_AN1",    "lumberjack",  "arthedain", "taom_gondor_village_003_forceatmo", hearth="300", after="town_AN1"),
    Village("village_AN3_1", "Staddle",         "town_AN3",    "wheat_farm",  "arthedain", "taom_gondor_village_004_forceatmo", hearth="300", after="town_AN3"),
    # Archet and Combe swapped after Mike moved the entities (2026-10-07): Archet keeps its canon
    # place beside the Chetwood (village_AN3_4).
    Village("village_AN3_2", "Archet",          "town_AN3",    "lumberjack",  "arthedain", "taom_gondor_village_001_forceatmo", hearth="300", after="town_AN3"),
    Village("village_AN3_3", "Combe",           "castle_AN8",    "swine_farm",  "arthedain", "taom_gondor_village_002_forceatmo", hearth="300", after="town_AN3"),
    Village("village_AN3_4", "Chetwood",        "town_AN3",    "trapper",     "arthedain", "taom_gondor_village_003_forceatmo", hearth="300", after="town_AN3"),
    Village("castle_village_AN9_1",  "Northgate",       "castle_AN9",  "sheep_farm",  "arthedain", "taom_gondor_village_004_forceatmo", hearth="300", after="castle_AN9"),
    Village("castle_village_AN10_1", "Uial Fold",       "castle_AN10", "sheep_farm",  "arthedain", "taom_gondor_village_001_forceatmo", hearth="300", after="castle_AN10"),
    Village("castle_village_AN4_1",  "Midgewater",      "castle_AN4",  "fisherman",   "arthedain", "taom_gondor_village_002_forceatmo", hearth="300", after="castle_AN4"),
    Village("castle_village_AN5_1",  "Mitheithel Ford", "castle_AN5",  "fisherman",   "arthedain", "taom_gondor_village_003_forceatmo", hearth="300", after="castle_AN5"),
    Village("castle_village_AN5_2",  "Rhaw Field",      "castle_AN5",  "cattle_farm", "arthedain", "taom_gondor_village_004_forceatmo", hearth="300", after="castle_AN5"),
    Village("castle_village_AN6_1",  "Stonebows",       "castle_AN6",  "wheat_farm",  "arthedain", "taom_gondor_village_001_forceatmo", hearth="300", after="castle_AN6"),
    Village("castle_village_AN6_2",  "Baranduin Mead",  "castle_AN6",  "cattle_farm", "arthedain", "taom_gondor_village_002_forceatmo", hearth="300", after="castle_AN6"),
    Village("castle_village_AN8_1",  "Undertowers",     "castle_AN8",  "sheep_farm",  "arthedain", "taom_gondor_village_003_forceatmo", hearth="300", after="castle_AN8"),
    Village("castle_village_AN8_2",  "Beraid Vale",     "castle_AN8",  "wheat_farm",  "arthedain", "taom_gondor_village_004_forceatmo", hearth="300", after="castle_AN8"),
    Village("castle_village_AN11_1", "Lossoth Strand",  "castle_AN11", "fisherman",   "arthedain", "taom_gondor_village_001_forceatmo", hearth="300", after="castle_AN11"),
    Village("castle_village_AN11_2", "Iceward",         "castle_AN11", "trapper",     "arthedain", "taom_gondor_village_002_forceatmo", hearth="300", after="castle_AN11"),
    # Two more Mike placed the same afternoon; names approved by him (Bridgefields is canon, by the
    # Brandywine Bridge).
    Village("village_AN1_4",         "Lin Uial",        "town_AN1",    "sheep_farm",  "arthedain", "taom_gondor_village_004_forceatmo", hearth="300", after="town_AN1"),
    Village("castle_village_AN6_3",  "Bridgefields",    "castle_AN6",  "wheat_farm",  "arthedain", "taom_gondor_village_003_forceatmo", hearth="300", after="castle_AN6"),
    # Blue Craig's second town, Luinkrag (town_GBC2, tools/add_map_fortifications.py). Hyphenated
    # Black Speech compounds like Blue Craig's own villages; types by the orc per-fief rule
    # (assign_orc_village_types.fief_types(3, "silver_mine")); hearth the 500 Blue Craig floor.
    Village("village_GBC2_1", "Gash-nakh", "town_GBC2", "swine_farm",  "bluecraig", "sturgia_village_d", after="town_GBC2"),
    Village("village_GBC2_2", "Mok-bûr",   "town_GBC2", "cattle_farm", "bluecraig", "sturgia_village_k", after="town_GBC2"),
    Village("village_GBC2_3", "Zog-tang",  "town_GBC2", "silver_mine", "bluecraig", "sturgia_village_c", after="town_GBC2"),
    # Gundabad's Carn Dûm (town_G3, tools/add_map_fortifications.py; Mike placed it 2026-10-15). The
    # fief's shared stem is Dûm-, with Black Speech endings: -bosh this map's swine farms, lug tower,
    # ghâsh fire, burzum darkness. Types by the orc per-fief rule (assign_orc_village_types.fief_types(4, "iron_mine")).
    Village("village_G3_1", "Dûm-bosh",   "town_G3", "swine_farm",  "gundabad", "sturgia_village_c", after="town_G3"),
    Village("village_G3_2", "Dûm-lug",    "town_G3", "cattle_farm", "gundabad", "sturgia_village_d", after="town_G3"),
    Village("village_G3_3", "Dûm-ghâsh",  "town_G3", "iron_mine",   "gundabad", "sturgia_village_k", after="town_G3"),
    Village("village_G3_4", "Dûm-burzum", "town_G3", "lumberjack",  "gundabad", "sturgia_village_l", after="town_G3"),
    # Lithur (castle_G6, Mike named it; placed 2026-10-15, its villages saved 2026-10-17). Stem Lith-, with
    # this map's Gundabad endings: -bosh swine farm (Gundbosh), -mauz cattle farm (Gund-mauz), -mîr silver
    # mine (Gram-mîr). Types by the orc per-fief rule, fief_types(3, "silver_mine"): Carn Dûm has the iron.
    Village("castle_village_G6_1", "Lith-bosh", "castle_G6", "swine_farm",  "gundabad", "sturgia_village_b", after="castle_G6"),
    Village("castle_village_G6_2", "Lith-mauz", "castle_G6", "cattle_farm", "gundabad", "sturgia_village_h", after="castle_G6"),
    Village("castle_village_G6_3", "Lith-mîr",  "castle_G6", "silver_mine", "gundabad", "sturgia_village_d", after="castle_G6"),
    # Cardolan and Rhudaur (#978): the 29 villages Mike placed 2026-10-23 with their nine fortifications
    # (tools/add_map_fortifications.py writes those first). Names in each realm's style: Bree-land and
    # Sindarin for Cardolan, with the canon Swanfleet, Andrath, Eryn Vorn and Girdley; hill names for
    # Rhudaur, with the canon Last Bridge and Ettendale. Hearth 300 and Gondor's four village scenes in
    # rotation, as Arthedain's: weak realms, off the economy-floor spec.
    Village("village_CA1_1", "Eithelford", "town_CA1", "fisherman", "cardolan", "taom_gondor_village_001_forceatmo", hearth="300", after="town_CA1"),
    Village("village_CA1_2", "Talath Eithel", "town_CA1", "wheat_farm", "cardolan", "taom_gondor_village_002_forceatmo", hearth="300", after="town_CA1"),
    Village("village_CA1_3", "Greenway Stead", "town_CA1", "cattle_farm", "cardolan", "taom_gondor_village_003_forceatmo", hearth="300", after="town_CA1"),
    Village("village_CA1_4", "Nen Galen", "town_CA1", "sheep_farm", "cardolan", "taom_gondor_village_004_forceatmo", hearth="300", after="town_CA1"),
    Village("village_CA2_1", "Gorthad Fold", "town_CA2", "sheep_farm", "cardolan", "taom_gondor_village_001_forceatmo", hearth="300", after="town_CA2"),
    Village("village_CA2_2", "Barrowfield", "town_CA2", "wheat_farm", "cardolan", "taom_gondor_village_002_forceatmo", hearth="300", after="town_CA2"),
    Village("village_CA2_4", "Downsend", "town_CA2", "cattle_farm", "cardolan", "taom_gondor_village_003_forceatmo", hearth="300", after="town_CA2"),
    Village("village_CA3_1", "Swanfleet", "town_CA3", "fisherman", "cardolan", "taom_gondor_village_004_forceatmo", hearth="300", after="town_CA3"),
    Village("village_CA3_2", "Minhiriath Mead", "town_CA3", "wheat_farm", "cardolan", "taom_gondor_village_001_forceatmo", hearth="300", after="town_CA3"),
    Village("village_CA3_3", "Gwathló Ferry", "town_CA3", "flax_plant", "cardolan", "taom_gondor_village_002_forceatmo", hearth="300", after="town_CA3"),
    Village("village_CA3_4", "Eryn Vorn", "town_CA3", "lumberjack", "cardolan", "taom_gondor_village_003_forceatmo", hearth="300", after="town_CA3"),
    Village("castle_village_CA1_1", "Downsedge", "castle_CA1", "sheep_farm", "cardolan", "taom_gondor_village_004_forceatmo", hearth="300", after="castle_CA1"),
    Village("castle_village_CA1_2", "Andrath", "castle_CA1", "cattle_farm", "cardolan", "taom_gondor_village_001_forceatmo", hearth="300", after="castle_CA1"),
    Village("castle_village_CA1_3", "Old Forest Eaves", "castle_CA1", "lumberjack", "cardolan", "taom_gondor_village_002_forceatmo", hearth="300", after="castle_CA1"),
    Village("castle_village_CA2_1", "Sarn Mead", "castle_CA2", "wheat_farm", "cardolan", "taom_gondor_village_003_forceatmo", hearth="300", after="castle_CA2"),
    Village("castle_village_CA2_2", "Girdley", "castle_CA2", "fisherman", "cardolan", "taom_gondor_village_004_forceatmo", hearth="300", after="castle_CA2"),
    Village("castle_village_CA2_3", "Stonewold", "castle_CA2", "sheep_farm", "cardolan", "taom_gondor_village_001_forceatmo", hearth="300", after="castle_CA2"),
    Village("castle_village_CA2_4", "Brandy Reach", "castle_CA2", "cattle_farm", "cardolan", "taom_gondor_village_002_forceatmo", hearth="300", after="castle_CA2"),
    Village("village_RH1_1", "Brin Hollow", "town_RH1", "sheep_farm", "rhudaur", "taom_gondor_village_003_forceatmo", hearth="300", after="town_RH1"),
    Village("village_RH1_2", "Trollshaw Edge", "town_RH1", "lumberjack", "rhudaur", "taom_gondor_village_004_forceatmo", hearth="300", after="town_RH1"),
    Village("village_RH1_3", "Last Bridge", "town_RH1", "fisherman", "rhudaur", "taom_gondor_village_001_forceatmo", hearth="300", after="town_RH1"),
    Village("castle_village_RH1_1", "Angle Fold", "castle_RH1", "wheat_farm", "rhudaur", "taom_gondor_village_002_forceatmo", hearth="300", after="castle_RH1"),
    Village("castle_village_RH1_2", "Loudwater Mead", "castle_RH1", "cattle_farm", "rhudaur", "taom_gondor_village_003_forceatmo", hearth="300", after="castle_RH1"),
    Village("castle_village_RH2_1", "Ettendale", "castle_RH2", "sheep_farm", "rhudaur", "taom_gondor_village_004_forceatmo", hearth="300", after="castle_RH2"),
    Village("castle_village_RH2_2", "Fell Steading", "castle_RH2", "iron_mine", "rhudaur", "taom_gondor_village_001_forceatmo", hearth="300", after="castle_RH2"),
    Village("castle_village_RH2_3", "Hoarwell Bend", "castle_RH2", "fisherman", "rhudaur", "taom_gondor_village_002_forceatmo", hearth="300", after="castle_RH2"),
    Village("castle_village_RH3_1", "Coldfell Shieling", "castle_RH3", "sheep_farm", "rhudaur", "taom_gondor_village_003_forceatmo", hearth="300", after="castle_RH3"),
    Village("castle_village_RH3_2", "Rimecliff", "castle_RH3", "trapper", "rhudaur", "taom_gondor_village_004_forceatmo", hearth="300", after="castle_RH3"),
    Village("castle_village_RH3_3", "Greyfell Wood", "castle_RH3", "lumberjack", "rhudaur", "taom_gondor_village_001_forceatmo", hearth="300", after="castle_RH3"),
    # Lindon (#1039; Mike placed the entities 2026-10-27). Sindarin of the Falathrim, hearth at the
    # Lindon floor (tools/settlement_economy_floor.json), the least used battania village scenes.
    Village("village_LN2_1", "Talath Falas", "town_LN2", "wheat_farm", "lindon", "battania_village_h", after="town_LN2"),
    Village("village_LN2_2", "Maewlond", "town_LN2", "fisherman", "lindon", "battania_village_i", after="town_LN2"),
    Village("village_LN2_3", "Nen Harn", "town_LN2", "vineyard", "lindon", "battania_village_j", after="town_LN2"),
    Village("village_LN2_4", "Linalph", "town_LN2", "cattle_farm", "lindon", "battania_village_k", after="town_LN2"),
    Village("village_LN3_1", "Ethir Forn", "town_LN3", "fisherman", "lindon", "battania_village_l", after="town_LN3"),
    Village("village_LN3_2", "Brithlas", "town_LN3", "salt_mine", "lindon", "battania_village", after="town_LN3"),
    Village("village_LN3_3", "Hîthlin", "town_LN3", "sheep_farm", "lindon", "battania_village_l", after="town_LN3"),
    Village("village_LN3_4", "Gwaewlas", "town_LN3", "flax_plant", "lindon", "battania_village_g", after="town_LN3"),
    Village("village_LN3_5", "Mithlin", "town_LN3", "wheat_farm", "lindon", "battania_village_h", after="town_LN3"),
    Village("castle_village_LN1_1", "Parth Harn", "castle_LN1", "sheep_farm", "lindon", "battania_village_i", after="castle_LN1"),
    Village("castle_village_LN1_2", "Nimlas", "castle_LN1", "lumberjack", "lindon", "battania_village_j", after="castle_LN1"),
    Village("castle_village_LN1_3", "Glanaear", "castle_LN1", "fisherman", "lindon", "battania_village_k", after="castle_LN1"),
    Village("castle_village_LN2_1", "Emyn Luin", "castle_LN2", "sheep_farm", "lindon", "battania_village_l", after="castle_LN2"),
    Village("castle_village_LN2_2", "Luinbar", "castle_LN2", "cattle_farm", "lindon", "battania_village", after="castle_LN2"),
    Village("castle_village_LN2_3", "Ram Luin", "castle_LN2", "iron_mine", "lindon", "battania_village_j", after="castle_LN2"),
    Village("castle_village_LN3_1", "Cirith Forn", "castle_LN3", "trapper", "lindon", "battania_village_g", after="castle_LN3"),
    Village("castle_village_LN3_2", "Forlas", "castle_LN3", "lumberjack", "lindon", "battania_village_h", after="castle_LN3"),
    Village("castle_village_LN3_3", "Falathlin", "castle_LN3", "fisherman", "lindon", "battania_village_i", after="castle_LN3"),
    # Ered Luin (Mike placed the entities 2026-10-27). Mines and upland farms in Westron, the names the
    # dwarves use before Men; hearth at Erebor's 350; the least used sturgia village scenes.
    Village("village_EL1_1", "Mickleburg Delving", "town_EL1", "iron_mine", "eredluin", "sturgia_village_k", hearth="350", after="town_EL1"),
    Village("village_EL1_2", "Lunewater", "town_EL1", "fisherman", "eredluin", "sturgia_village_c", hearth="350", after="town_EL1"),
    Village("village_EL1_3", "Broadbeam Fold", "town_EL1", "sheep_farm", "eredluin", "sturgia_village_d", hearth="350", after="town_EL1"),
    Village("village_EL1_4", "Greystone Pits", "town_EL1", "clay_mine", "eredluin", "sturgia_village_a", hearth="350", after="town_EL1"),
    Village("village_EL2_1", "Emberdeep", "town_EL2", "iron_mine", "eredluin", "sturgia_village_b", hearth="350", after="town_EL2"),
    Village("village_EL2_2", "Hollowbold Mead", "town_EL2", "wheat_farm", "eredluin", "sturgia_village_e", hearth="350", after="town_EL2"),
    Village("village_EL2_3", "Coalbrook", "town_EL2", "lumberjack", "eredluin", "sturgia_village_g", hearth="350", after="town_EL2"),
    Village("village_EL3_1", "Thrór's Steading", "town_EL3", "cattle_farm", "eredluin", "sturgia_village_h", hearth="350", after="town_EL3"),
    Village("village_EL3_2", "Ironhearth", "town_EL3", "iron_mine", "eredluin", "sturgia_village_j", hearth="350", after="town_EL3"),
    Village("village_EL3_3", "Silverbrook", "town_EL3", "silver_mine", "eredluin", "sturgia_village_l", hearth="350", after="town_EL3"),
    Village("village_EL3_4", "Roadsend", "town_EL3", "swine_farm", "eredluin", "sturgia_village_k", hearth="350", after="town_EL3"),
    Village("castle_village_EL1_1", "Craigfoot", "castle_EL1", "sheep_farm", "eredluin", "sturgia_village_c", hearth="350", after="castle_EL1"),
    Village("castle_village_EL1_2", "Goatfell", "castle_EL1", "trapper", "eredluin", "sturgia_village_d", hearth="350", after="castle_EL1"),
    Village("castle_village_EL1_3", "Rimestone", "castle_EL1", "salt_mine", "eredluin", "sturgia_village_a", hearth="350", after="castle_EL1"),
    Village("castle_village_EL2_1", "Dolmed Shieling", "castle_EL2", "sheep_farm", "eredluin", "sturgia_village_b", hearth="350", after="castle_EL2"),
    Village("castle_village_EL2_2", "Silverthread", "castle_EL2", "silver_mine", "eredluin", "sturgia_village_e", hearth="350", after="castle_EL2"),
    Village("castle_village_EL3_1", "Anvilstead", "castle_EL3", "clay_mine", "eredluin", "sturgia_village_g", hearth="350", after="castle_EL3"),
    Village("castle_village_EL3_2", "Harrowmead", "castle_EL3", "wheat_farm", "eredluin", "sturgia_village_h", hearth="350", after="castle_EL3"),
    Village("castle_village_EL4_1", "Southdelf", "castle_EL4", "iron_mine", "eredluin", "sturgia_village_j", hearth="350", after="castle_EL4"),
    Village("castle_village_EL4_2", "Greyfold", "castle_EL4", "cattle_farm", "eredluin", "sturgia_village_l", hearth="350", after="castle_EL4"),
    Village("castle_village_EL4_3", "Flintmoor", "castle_EL4", "wheat_farm", "eredluin", "sturgia_village_k", hearth="350", after="castle_EL4"),
    Village("castle_village_EL5_1", "Dwarrowstead", "castle_EL5", "wheat_farm", "eredluin", "sturgia_village_c", hearth="350", after="castle_EL5"),
    Village("castle_village_EL5_2", "Wayhearth", "castle_EL5", "swine_farm", "eredluin", "sturgia_village_d", hearth="350", after="castle_EL5"),
    # Anduin Vale and Angaladh (Mike placed the entities 2026-10-28; tools/add_map_fortifications.py writes
    # their fortifications first). Anduin Vale: Northman and Westron farm names, with canon Carrock,
    # Gladden and Beorn's bees (Honeymead, Bearward). Angaladh: forest settlements of the Woodmen, with
    # Ironwood Hall's charcoal and iron. Types are those Dale's villages use; hearth 300 as Cardolan's;
    # the sturgia village scenes in rotation, the heavily used sturgia_village_f left out.
    Village("village_AV1_1", "Honeymead", "town_AV1", "wheat_farm", "anduinvale", "sturgia_village_k", hearth="300", after="town_AV1"),
    Village("village_AV1_2", "Oxenfold", "town_AV1", "cattle_farm", "anduinvale", "sturgia_village_a", hearth="300", after="town_AV1"),
    Village("village_AV1_3", "Whitfold", "town_AV1", "sheep_farm", "anduinvale", "sturgia_village_b", hearth="300", after="town_AV1"),
    Village("village_AV1_4", "Bearward", "town_AV1", "trapper", "anduinvale", "sturgia_village_c", hearth="300", after="town_AV1"),
    Village("village_AV2_1", "Langford", "town_AV2", "fisherman", "anduinvale", "sturgia_village_d", hearth="300", after="town_AV2"),
    Village("village_AV2_2", "Millholme", "town_AV2", "wheat_farm", "anduinvale", "sturgia_village_e", hearth="300", after="town_AV2"),
    Village("village_AV2_3", "Withybed", "town_AV2", "flax_plant", "anduinvale", "sturgia_village_g", hearth="300", after="town_AV2"),
    Village("castle_village_AV1_1", "Carrockfoot", "castle_AV1", "sheep_farm", "anduinvale", "sturgia_village_h", hearth="300", after="castle_AV1"),
    Village("castle_village_AV1_2", "Rockwater", "castle_AV1", "fisherman", "anduinvale", "sturgia_village_j", hearth="300", after="castle_AV1"),
    Village("castle_village_AV1_3", "Eagle's Reach", "castle_AV1", "trapper", "anduinvale", "sturgia_village_l", hearth="300", after="castle_AV1"),
    Village("castle_village_AV2_1", "Passfold", "castle_AV2", "sheep_farm", "anduinvale", "sturgia_village_k", hearth="300", after="castle_AV2"),
    Village("castle_village_AV2_2", "Pinewatch", "castle_AV2", "lumberjack", "anduinvale", "sturgia_village_a", hearth="300", after="castle_AV2"),
    Village("castle_village_AV3_1", "Gladdenmere", "castle_AV3", "fisherman", "anduinvale", "sturgia_village_b", hearth="300", after="castle_AV3"),
    Village("castle_village_AV3_2", "Irisholm", "castle_AV3", "flax_plant", "anduinvale", "sturgia_village_c", hearth="300", after="castle_AV3"),
    Village("castle_village_AV4_1", "Eastmead", "castle_AV4", "wheat_farm", "anduinvale", "sturgia_village_d", hearth="300", after="castle_AV4"),
    Village("castle_village_AV4_2", "Bankside", "castle_AV4", "cattle_farm", "anduinvale", "sturgia_village_e", hearth="300", after="castle_AV4"),
    Village("village_AND1_1", "Oakstead", "town_AND1", "lumberjack", "angaladh", "sturgia_village_g", hearth="300", after="town_AND1"),
    Village("village_AND1_2", "Hazelglade", "town_AND1", "trapper", "angaladh", "sturgia_village_h", hearth="300", after="town_AND1"),
    Village("village_AND1_3", "Elmclearing", "town_AND1", "cattle_farm", "angaladh", "sturgia_village_j", hearth="300", after="town_AND1"),
    Village("village_AND1_4", "Brackenwick", "town_AND1", "wheat_farm", "angaladh", "sturgia_village_l", hearth="300", after="town_AND1"),
    Village("castle_village_AND1_1", "Thornbrook", "castle_AND1", "fisherman", "angaladh", "sturgia_village_k", hearth="300", after="castle_AND1"),
    Village("castle_village_AND1_2", "Briarfold", "castle_AND1", "sheep_farm", "angaladh", "sturgia_village_a", hearth="300", after="castle_AND1"),
    Village("castle_village_AND1_3", "Sloe Glade", "castle_AND1", "lumberjack", "angaladh", "sturgia_village_b", hearth="300", after="castle_AND1"),
    Village("castle_village_AND2_1", "Dimhollow", "castle_AND2", "trapper", "angaladh", "sturgia_village_c", hearth="300", after="castle_AND2"),
    Village("castle_village_AND2_2", "Greyscree", "castle_AND2", "iron_mine", "angaladh", "sturgia_village_d", hearth="300", after="castle_AND2"),
    Village("castle_village_AND3_1", "Millgate", "castle_AND3", "wheat_farm", "angaladh", "sturgia_village_e", hearth="300", after="castle_AND3"),
    Village("castle_village_AND3_2", "Fellingwick", "castle_AND3", "lumberjack", "angaladh", "sturgia_village_g", hearth="300", after="castle_AND3"),
    Village("castle_village_AND4_1", "Ironroot", "castle_AND4", "iron_mine", "angaladh", "sturgia_village_h", hearth="300", after="castle_AND4"),
    Village("castle_village_AND4_2", "Cinderdell", "castle_AND4", "lumberjack", "angaladh", "sturgia_village_j", hearth="300", after="castle_AND4"),
    Village("castle_village_AND4_3", "Hallfold", "castle_AND4", "sheep_farm", "angaladh", "sturgia_village_l", hearth="300", after="castle_AND4"),
    # Dorwinion (#1062; Mike placed the entities 2026-10-29): 23 villages of vineyards, presses, cellars and river
    # landings in Northman and Westron. Types are those Dale's villages use; hearth 350 (middling, as Ered Luin);
    # the sturgia village scenes in rotation, sturgia_village_f (54 uses) left out.
    Village("village_DO1_1", "Redwater Landing", "town_DO1", "fisherman", "dorwinion", "sturgia_village_k", hearth="350", after="town_DO1"),
    Village("village_DO1_2", "Sunbank", "town_DO1", "wheat_farm", "dorwinion", "sturgia_village_a", hearth="350", after="town_DO1"),
    Village("village_DO1_3", "Cellarbrook", "town_DO1", "cattle_farm", "dorwinion", "sturgia_village_b", hearth="350", after="town_DO1"),
    Village("village_DO1_4", "Pressmead", "town_DO1", "sheep_farm", "dorwinion", "sturgia_village_c", hearth="350", after="town_DO1"),
    Village("village_DO2_1", "Amberslope", "town_DO2", "wheat_farm", "dorwinion", "sturgia_village_d", hearth="350", after="town_DO2"),
    Village("village_DO2_2", "Vintnersted", "town_DO2", "flax_plant", "dorwinion", "sturgia_village_e", hearth="350", after="town_DO2"),
    Village("village_DO2_3", "Reedquay", "town_DO2", "fisherman", "dorwinion", "sturgia_village_g", hearth="350", after="town_DO2"),
    Village("village_DO2_4", "Terracefold", "town_DO2", "cattle_farm", "dorwinion", "sturgia_village_h", hearth="350", after="town_DO2"),
    Village("village_DO3_1", "Seagate Strand", "town_DO3", "fisherman", "dorwinion", "sturgia_village_j", hearth="350", after="town_DO3"),
    Village("village_DO3_2", "Saltmead", "town_DO3", "sheep_farm", "dorwinion", "sturgia_village_l", hearth="350", after="town_DO3"),
    Village("village_DO3_3", "Wineward", "town_DO3", "wheat_farm", "dorwinion", "sturgia_village_k", hearth="350", after="town_DO3"),
    Village("village_DO3_4", "Barrelwick", "town_DO3", "lumberjack", "dorwinion", "sturgia_village_a", hearth="350", after="town_DO3"),
    Village("castle_village_DO1_1", "Vinewatch Slope", "castle_DO1", "wheat_farm", "dorwinion", "sturgia_village_b", hearth="350", after="castle_DO1"),
    Village("castle_village_DO1_2", "Gravelford", "castle_DO1", "fisherman", "dorwinion", "sturgia_village_c", hearth="350", after="castle_DO1"),
    Village("castle_village_DO1_3", "Oakcask", "castle_DO1", "lumberjack", "dorwinion", "sturgia_village_d", hearth="350", after="castle_DO1"),
    Village("castle_village_DO2_1", "Northpress", "castle_DO2", "cattle_farm", "dorwinion", "sturgia_village_e", hearth="350", after="castle_DO2"),
    Village("castle_village_DO2_2", "Runningreach", "castle_DO2", "fisherman", "dorwinion", "sturgia_village_g", hearth="350", after="castle_DO2"),
    Village("castle_village_DO2_3", "Graperow", "castle_DO2", "wheat_farm", "dorwinion", "sturgia_village_h", hearth="350", after="castle_DO2"),
    Village("castle_village_DO3_1", "Eastvine", "castle_DO3", "wheat_farm", "dorwinion", "sturgia_village_j", hearth="350", after="castle_DO3"),
    Village("castle_village_DO3_2", "Marchcellar", "castle_DO3", "cattle_farm", "dorwinion", "sturgia_village_l", hearth="350", after="castle_DO3"),
    Village("castle_village_DO4_1", "Sunwall Terrace", "castle_DO4", "wheat_farm", "dorwinion", "sturgia_village_k", hearth="350", after="castle_DO4"),
    Village("castle_village_DO4_2", "Gullhythe", "castle_DO4", "fisherman", "dorwinion", "sturgia_village_a", hearth="350", after="castle_DO4"),
    Village("castle_village_DO4_3", "Winelees", "castle_DO4", "flax_plant", "dorwinion", "sturgia_village_b", hearth="350", after="castle_DO4"),
    # South Rhovanion (#1062): 25 villages of horse farms, meadows, fords and barrows. Types are those Rohan's
    # villages use; hearth 350; the least used vlandia village scenes in rotation (d, h, l, n, b, c, e, f, j).
    Village("village_SRH1_1", "Studfold", "town_SRH1", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_d", hearth="350", after="town_SRH1"),
    Village("village_SRH1_2", "Greenmead", "town_SRH1", "cattle_farm", "southrhovanion", "vlandia_village_h", hearth="350", after="town_SRH1"),
    Village("village_SRH1_3", "Wainford", "town_SRH1", "fisherman", "southrhovanion", "vlandia_village_l", hearth="350", after="town_SRH1"),
    Village("village_SRH1_4", "Barrowdown", "town_SRH1", "trapper", "southrhovanion", "vlandia_village_n", hearth="350", after="town_SRH1"),
    Village("village_SRH2_1", "Mareholm", "town_SRH2", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_b", hearth="350", after="town_SRH2"),
    Village("village_SRH2_2", "Vidumead", "town_SRH2", "cattle_farm", "southrhovanion", "vlandia_village_c", hearth="350", after="town_SRH2"),
    Village("village_SRH2_3", "Hartford", "town_SRH2", "fisherman", "southrhovanion", "vlandia_village_e", hearth="350", after="town_SRH2"),
    Village("village_SRH2_4", "Oldbarrow", "town_SRH2", "trapper", "southrhovanion", "vlandia_village_f", hearth="350", after="town_SRH2"),
    Village("village_SRH2_5", "Stallionfold", "town_SRH2", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_j", hearth="350", after="town_SRH2"),
    Village("village_SRH2_6", "Windmeadow", "town_SRH2", "wheat_farm", "southrhovanion", "vlandia_village_d", hearth="350", after="town_SRH2"),
    Village("castle_village_SRH1_1", "Paddockside", "castle_SRH1", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_h", hearth="350", after="castle_SRH1"),
    Village("castle_village_SRH1_2", "Riderford", "castle_SRH1", "fisherman", "southrhovanion", "vlandia_village_l", hearth="350", after="castle_SRH1"),
    Village("castle_village_SRH1_3", "Longmeadow", "castle_SRH1", "cattle_farm", "southrhovanion", "vlandia_village_n", hearth="350", after="castle_SRH1"),
    Village("castle_village_SRH1_4", "Foalfield", "castle_SRH1", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_b", hearth="350", after="castle_SRH1"),
    Village("castle_village_SRH2_1", "Meadford", "castle_SRH2", "fisherman", "southrhovanion", "vlandia_village_c", hearth="350", after="castle_SRH2"),
    Village("castle_village_SRH2_2", "Highbarrow", "castle_SRH2", "trapper", "southrhovanion", "vlandia_village_e", hearth="350", after="castle_SRH2"),
    Village("castle_village_SRH2_3", "Rydefold", "castle_SRH2", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_f", hearth="350", after="castle_SRH2"),
    Village("castle_village_SRH2_4", "Goldmead", "castle_SRH2", "wheat_farm", "southrhovanion", "vlandia_village_j", hearth="350", after="castle_SRH2"),
    Village("castle_village_SRH3_1", "Northfold", "castle_SRH3", "cattle_farm", "southrhovanion", "vlandia_village_d", hearth="350", after="castle_SRH3"),
    Village("castle_village_SRH3_2", "Greyford", "castle_SRH3", "fisherman", "southrhovanion", "vlandia_village_h", hearth="350", after="castle_SRH3"),
    Village("castle_village_SRH3_3", "Sorrelfold", "castle_SRH3", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_l", hearth="350", after="castle_SRH3"),
    Village("castle_village_SRH3_4", "Cairnmoor", "castle_SRH3", "trapper", "southrhovanion", "vlandia_village_n", hearth="350", after="castle_SRH3"),
    Village("castle_village_SRH4_1", "Westmead", "castle_SRH4", "cattle_farm", "southrhovanion", "vlandia_village_b", hearth="350", after="castle_SRH4"),
    Village("castle_village_SRH4_2", "Brownford", "castle_SRH4", "fisherman", "southrhovanion", "vlandia_village_c", hearth="350", after="castle_SRH4"),
    Village("castle_village_SRH4_3", "Horsewick", "castle_SRH4", "vlandian_horse_ranch", "southrhovanion", "vlandia_village_e", hearth="350", after="castle_SRH4"),
]
INSERT_AFTER = "village_isengard_a"   # default anchor: last settlement of the Isengard block, and its last loc row

PLACEHOLDER_STEP = (1.5, 1.0)  # map units per placeholder on the same parent


def comp_id(vid):
    """castle_village_X -> castle_village_comp_X, village_X -> village_comp_X (live convention)."""
    if vid.startswith("castle_village_"):
        return "castle_village_comp_" + vid[len("castle_village_"):]
    if vid.startswith("village_"):
        return "village_comp_" + vid[len("village_"):]
    raise ValueError(f"not a village id: {vid!r}")


def anchor_of(v):
    """The settlement whose block, and whose last loc row, this row lands after."""
    return v.after or INSERT_AFTER


def group_by_anchor(rows):
    """{anchor: [rows]} in first-seen anchor order, rows in table order within each anchor."""
    groups = {}
    for v in rows:
        groups.setdefault(anchor_of(v), []).append(v)
    return groups


def village_block(v, px, py, nl):
    if v.village_type not in VALID_VILLAGE_TYPES:
        raise ValueError(f"{v.id}: invalid VillageType id {v.village_type!r}")
    if v.culture not in MESHES:
        raise ValueError(f"{v.id}: no panel meshes recorded for culture {v.culture!r}; add it to MESHES")
    bg, wait, castle_bg = MESHES[v.culture]
    return (
        f'  <Settlement id="{v.id}" name="{{=Settlements.Settlement.name.{v.id}}}{v.name}" posX="{px}" posY="{py}" culture="Culture.{v.culture}">\n'
        f'    <Components>\n'
        f'      <Village id="{comp_id(v.id)}" village_type="VillageType.{v.village_type}" hearth="{v.hearth}" bound="Settlement.{v.bound}" background_crop_position="0.0" background_mesh="{bg}" wait_mesh="{wait}" castle_background_mesh="{castle_bg}" />\n'
        f'    </Components>\n'
        f'    <Locations complex_template="LocationComplexTemplate.village_complex">\n'
        f'      <Location id="village_center" scene_name="{v.scene}" />\n'
        f'    </Locations>\n'
        f'    <CommonAreas>\n'
        f'      <Area type="Pasture" name="{{=fOUsLdZR}}Pasture" />\n'
        f'      <Area type="Thicket" name="{{=66Mzk0NZ}}Thicket" />\n'
        f'      <Area type="Bog" name="{{=iXA5SttU}}Bog" />\n'
        f'    </CommonAreas>\n'
        f'  </Settlement>\n'
    ).replace("\n", nl)


def scene_position(scene_text, entity_id):
    """(x, y) strings from the entity's own transform, or None when the entity is not in the scene.

    The search window ends at the next `<game_entity` open tag (a child or the next sibling), so an
    entity with no transform of its own resolves to None instead of borrowing its neighbour's.
    """
    open_tag = re.search(r'<game_entity name=' + re.escape(f'"{entity_id}"') + r'[^>]*>', scene_text)
    if not open_tag:
        return None
    end = scene_text.find("<game_entity", open_tag.end())
    window = scene_text[open_tag.end():end if end != -1 else len(scene_text)]
    m = re.search(r'<transform position="\s*([-\d.]+)\s*,\s*([-\d.]+)\s*,', window)
    return (m.group(1), m.group(2)) if m else None


def settlement_position(master_text, sid):
    m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"') + r'[^>]*?\sposX="([^"]*)"\s+posY="([^"]*)"', master_text)
    return (m.group(1), m.group(2)) if m else None


def master_attr(master_text, sid, attr):
    """The value of `attr` on the <Settlement id="sid"> open tag, or None (absent row or absent attribute)."""
    m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"') + r'[^>]*?\s' + re.escape(attr) + r'="([^"]*)"', master_text)
    return m.group(1) if m else None


def master_village_bound(master_text, sid):
    """The `bound` of the <Village> component inside <Settlement id="sid">, or None."""
    m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"') + r'(?:(?!</Settlement>).)*?<Village [^>]*?\sbound="([^"]*)"', master_text, re.S)
    return m.group(1) if m else None


def resolve_position(v, scene_text, master_text, k):
    """(posX, posY, source). source is 'scene' or 'PLACEHOLDER' (parent + offset for the k-th one)."""
    pos = scene_position(scene_text, v.id)
    if pos:
        return pos[0], pos[1], "scene"
    parent = settlement_position(master_text, v.bound)
    if not parent:
        raise ValueError(f"{v.id}: bound settlement {v.bound!r} is not in settlements.xml, nothing to place it near")
    dx, dy = PLACEHOLDER_STEP
    return f"{float(parent[0]) + dx * (k + 1):.3f}", f"{float(parent[1]) + dy * (k + 1):.3f}", "PLACEHOLDER"


def detect_newline(text):
    if "\r\r\n" in text:
        return "\r\r\n"
    return "\r\n" if "\r\n" in text else "\n"


def missing_ids(master_text, villages):
    return [v for v in villages if f'id="{v.id}"' not in master_text]


def plan_loc_rows(villages, loc_texts):
    """{lang: [(id, name), ...]} of the rows each language file still lacks, from the WHOLE table.

    Planned per language rather than from the ids missing in the master, so a master-present /
    loc-missing state (a run that failed part-way through the languages, a reverted file) is
    repaired by a plain re-run instead of being reported as nothing to do.
    """
    return {lang: [(v.id, v.name) for v in villages if f'id="Settlements.Settlement.name.{v.id}"' not in text]
            for lang, text in loc_texts.items()}


def nothing_to_do(villages, master_text, loc_texts):
    return not missing_ids(master_text, villages) and not any(plan_loc_rows(villages, loc_texts).values())


def _settlement_close_after(text, sid):
    """Offset just past the newline that ends the </Settlement> closing settlement `sid`, or -1."""
    open_m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"'), text)
    if not open_m:
        return -1
    close = text.find("</Settlement>", open_m.end())
    if close == -1:
        return -1
    end = close + len("</Settlement>")
    nl_end = text.find("\n", end)
    return nl_end + 1 if nl_end != -1 else end


def insert_after_settlement(master_text, anchor_id, blocks):
    at = _settlement_close_after(master_text, anchor_id)
    if at == -1:
        at = master_text.rfind("</Settlements>")
        if at == -1:
            raise RuntimeError("</Settlements> close tag not found")
    return master_text[:at] + "".join(blocks) + master_text[at:]


def append_settlements(master_text, blocks):
    """New rows go at the END of the file, never after an anchor (the save-order rule).

    A save made before a batch still loads settlements.xml (SandBoxManager.InitializeSandboxXMLs).
    For a new town or village, Settlement.Deserialize runs Alleys[num].Initialize on the new
    object's empty list and throws; MBObjectManager.LoadXML swallows it and stops, so no row AFTER
    that one is marked ready, and the load unregisters every non-ready object (Campaign.cs:1457):
    those existing settlements vanish from that save while its parties and heroes still point at
    them. Rows at the end are dropped
    cleanly instead: IsReady is set only after Deserialize returns, and the load unregisters
    non-ready objects (Campaign.cs:1457, v1.5.4).
    """
    at = master_text.rfind("</Settlements>")
    if at == -1:
        raise RuntimeError("</Settlements> close tag not found")
    return master_text[:at] + "".join(blocks) + master_text[at:]


def save_order_findings(master_text, new_ids):
    """Findings unless the rows in `new_ids` are the file's tail, headed by a row with <CommonAreas>.

    The head must throw on an old save (a town or village), so the whole tail is dropped; a castle
    carries no <CommonAreas>, loads into the old save without its owner and is not dropped. XML
    comments are skipped, as the engine skips comment nodes.
    """
    rows = re.sub(r"<!--.*?-->", "", master_text, flags=re.S)
    order = re.findall(r'<Settlement id="([^"]+)"', rows)
    first = next((i for i, sid in enumerate(order) if sid in new_ids), None)
    if first is None:
        return []
    head = order[first]
    findings = [f"{sid}: an existing row after the new row {head}; a save made before this batch unregisters it"
                for sid in order[first:] if sid not in new_ids]
    head_row = re.search(r'<Settlement id=' + re.escape(f'"{head}"') + r'[\s>].*?</Settlement>', rows, re.S)
    if head_row and "<CommonAreas>" not in head_row.group(0):
        findings.append(f"{head}: heads the new rows but has no <CommonAreas>, so a save made before this "
                        "batch loads it without its owner instead of dropping it; put a town first")
    return findings


def master_names(master_text):
    """{settlement id: name} from each row's name= fallback, the English text every language starts from."""
    return dict(re.findall(r'<Settlement id="([^"]+)"[^>]*?\sname="\{=[^}]*\}([^"]*)"', master_text))


def name_collisions(rows, master_text):
    """Findings for each (id, name) whose name a DIFFERENT settlement already carries in settlements.xml.

    The tables check their names only against each other, so Harlond, Forlond and Edhellond each
    named two settlements before #1039 (#1042). Case is ignored; a row is never its own collision.
    """
    taken = {}
    for sid, name in master_names(master_text).items():
        taken.setdefault(name.casefold(), []).append(sid)
    findings = []
    for sid, name in rows:
        others = sorted(o for o in taken.get(name.casefold(), []) if o != sid)
        if others:
            findings.append(f"{sid}: the name {name!r} is already on the map ({', '.join(others)})")
    return findings


def evict_cached_names(ids_by_lang, cache_dir=CACHE_DIR, field="name"):
    """Remove each settlement's `field` row (name, or text for a description) from tools/translation_cache/<lang>.json; returns {lang: removed}.

    The cache keys on the string id alone (TRANSLATOR_GUIDE.md), so a name whose English changed keeps
    its old translation there and the next translator run writes it back. A file is rewritten only when
    a key goes, in its own shape: translate_with_claude.save_cache's dump options, its own newline and
    its own final newline.
    """
    removed = {}
    for lang, ids in ids_by_lang.items():
        path = os.path.join(cache_dir, f"{lang.lower()}.json")
        if not os.path.isfile(path):  # translate_with_claude.load_cache reads a missing file as an empty cache
            removed[lang] = 0
            continue
        raw = open(path, "rb").read().decode("utf-8")
        cache = json.loads(raw)
        keys = [k for k in (f"Settlements.Settlement.{field}.{sid}" for sid in ids) if k in cache]
        removed[lang] = len(keys)
        if not keys:
            continue
        for k in keys:
            del cache[k]
        nl = "\r\n" if "\r\n" in raw else "\n"
        text = json.dumps(cache, ensure_ascii=False, indent=2, sort_keys=True).replace("\n", nl)
        if raw.endswith(nl):
            text += nl
        open(path, "wb").write(text.encode("utf-8"))
    return removed


def tagged_rows(lang, rows, table):
    """[(id, name)] with each BR and IT name carrying the grammar tag the table has for its id (#1023).

    An id the table does not know yet stays plain; tools/generate_name_tags.py --write then
    tools/name_tags.py --target live-map --apply tag it.
    """
    return [(sid, name_tags.tag_row(lang, "Settlements.Settlement.name." + sid, name, table)) for sid, name in rows]


def loc_row(sid, name):
    return f'    <string id="Settlements.Settlement.name.{sid}" text={quoteattr(name)} />'


def insert_loc_rows(loc_text, anchor_id, rows):
    nl = detect_newline(loc_text)
    block = "".join(loc_row(sid, name) + nl for sid, name in rows)
    last = None
    for m in re.finditer(r'<string id="Settlements\.Settlement\.[a-z]+\.' + re.escape(anchor_id) + r'"[^>]*/>', loc_text):
        last = m
    if last is not None:
        at = loc_text.find("\n", last.end()) + 1
    else:
        at = loc_text.rfind("</strings>")
        if at == -1:
            raise RuntimeError("</strings> close tag not found")
        # back up over the indentation that precedes </strings> so the row lands on its own line
        while at > 0 and loc_text[at - 1] in " \t":
            at -= 1
    return loc_text[:at] + block + loc_text[at:]


def check(villages, master_text, scene_text, loc_texts, tolerance=0.01):
    """Findings (strings) for any table id whose data is missing, whose position drifted or whose name is taken,
    or whose master culture or bound settlement differs from the table's."""
    findings = name_collisions([(v.id, v.name) for v in villages], master_text)
    for v in villages:
        pos = settlement_position(master_text, v.id)
        if not pos:
            findings.append(f"{v.id}: no <Settlement> row in settlements.xml")
        if not settlement_position(master_text, v.bound):
            findings.append(f"{v.id}: bound settlement {v.bound} has no row in settlements.xml (a new campaign NREs)")
        culture = master_attr(master_text, v.id, "culture")
        if culture is not None and culture != f"Culture.{v.culture}":
            findings.append(f"{v.id}: settlements.xml has culture={culture} but the table says Culture.{v.culture}")
        bound = master_village_bound(master_text, v.id)
        if bound is not None and bound != f"Settlement.{v.bound}" and settlement_position(master_text, v.bound):
            findings.append(f"{v.id}: settlements.xml binds it to {bound} but the table says Settlement.{v.bound}")
        for lang, text in loc_texts.items():
            if f'id="Settlements.Settlement.name.{v.id}"' not in text:
                findings.append(f"{v.id}: no loc row in Languages/{lang}/loc_settlements.xml")
        scene = scene_position(scene_text, v.id)
        if not scene:
            findings.append(f"{v.id}: no entity in scene.xscene (a campaign load would NRE SettlementVisual.OnStartup)")
        elif pos and (abs(float(pos[0]) - float(scene[0])) > tolerance or abs(float(pos[1]) - float(scene[1])) > tolerance):
            findings.append(f"{v.id}: settlements.xml has posX={pos[0]} posY={pos[1]} but scene.xscene has {scene[0]}, {scene[1]} (save the scene, or re-run --apply)")
    return findings


def _read(path):
    raw = open(path, "rb").read()
    return raw.startswith(b"\xef\xbb\xbf"), raw.decode("utf-8-sig")


def _write(path, had_bom, text, tag):
    ET.fromstring(text.encode("utf-8"))  # refuse to write a document that no longer parses
    backup = path + f".bak_{tag}"
    open(backup, "wb").write(open(path, "rb").read())
    open(path, "wb").write((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))
    ET.parse(path)


def _loc_path(lang):
    return MAP + f"/ModuleData/Languages/{lang}/loc_settlements.xml"


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # the names carry accents
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the live files (default: dry run)")
    ap.add_argument("--check", action="store_true", help="verify every VILLAGES id is fully present, positioned, and on its table culture and bound; exit 1 otherwise")
    args = ap.parse_args()

    for p in (LIVE, SCENE):
        if not os.path.isfile(p):
            print(f"ERROR: not found: {p} (set BANNERLORD_GAME_DIR, see tools/_gamedir.py)", file=sys.stderr)
            return 2
    master_bom, master = _read(LIVE)
    scene = open(SCENE, "rb").read().decode("utf-8", errors="replace")
    locs = {lang: _read(_loc_path(lang)) for lang in LANGS if os.path.isfile(_loc_path(lang))}
    if len(locs) != len(LANGS):
        print(f"ERROR: expected {len(LANGS)} loc_settlements.xml files, found {len(locs)}", file=sys.stderr)
        return 2

    if args.check:
        findings = check(VILLAGES, master, scene, {lang: text for lang, (_, text) in locs.items()})
        for f in findings:
            print("FAIL " + f)
        if findings:
            print(f"\n{len(findings)} finding(s) across {len(VILLAGES)} table id(s).")
            return 1
        print(f"OK: all {len(VILLAGES)} villages present in settlements.xml, all {len(locs)} loc files and scene.xscene; positions, culture and bound match; no other settlement carries their names.")
        return 0

    loc_texts = {lang: text for lang, (_, text) in locs.items()}
    if nothing_to_do(VILLAGES, master, loc_texts):
        print(f"all {len(VILLAGES)} villages already present in settlements.xml and all {len(locs)} loc files; nothing to do (idempotent). Try --check.")
        return 0
    todo = missing_ids(master, VILLAGES)
    loc_plan = plan_loc_rows(VILLAGES, loc_texts)

    nl = detect_newline(master)
    orphans = [v.id for v in todo if not settlement_position(master, v.bound)]
    if orphans:
        print("REFUSING: bound settlement has no row in settlements.xml (run add_map_fortifications.py "
              "first): " + ", ".join(orphans), file=sys.stderr)
        return 1
    clashes = name_collisions([(v.id, v.name) for v in todo], master)
    if clashes:
        print("REFUSING: rename the row in VILLAGES first:\n  " + "\n  ".join(clashes), file=sys.stderr)
        return 1
    blocks, placeholders, per_parent = [], 0, {}
    print(f"Plan: +{len(todo)} village(s) ({len(VILLAGES) - len(todo)} already present), appended at the end")
    for v in todo:
        k = per_parent.get(v.bound, 0)
        px, py, source = resolve_position(v, scene, master, k)
        if source == "PLACEHOLDER":
            per_parent[v.bound] = k + 1
            placeholders += 1
        blocks.append(village_block(v, px, py, nl))
        print(f"  {v.id:28} {v.name:15} -> bound {v.bound:20} {v.village_type:11} at {px}, {py}  [{source}]  loc after {anchor_of(v)}")
    if placeholders:
        print(f"\n{placeholders} PLACEHOLDER position(s): the entity is not in the saved scene yet. Save the scene "
              f"before any campaign load; the editor writes the real posX/posY back. Then run --check.")
    for lang in LANGS:
        rows = loc_plan[lang]
        print(f"  loc {lang:3}: +{len(rows)} row(s)" + ("" if rows else " (all present, skipped)"))
    if not args.apply:
        print("\nDRY RUN: re-run with --apply to write the live files.")
        return 0

    tag = "mapvillages_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    n_blocks = len(blocks)
    if blocks:
        _write(LIVE, master_bom, append_settlements(master, blocks), tag)
    written = []
    table = name_tags.load_table()
    by_id = {v.id: v for v in VILLAGES}
    for lang, (bom, text) in locs.items():
        if loc_plan[lang]:
            for anchor, group in group_by_anchor([by_id[sid] for sid, _ in loc_plan[lang]]).items():
                text = insert_loc_rows(text, anchor, tagged_rows(lang, [(v.id, v.name) for v in group], table))
            _write(_loc_path(lang), bom, text, tag)
            written.append(lang)
    print(f"\nApplied: +{n_blocks} settlement(s) in settlements.xml, +{sum(len(loc_plan[l]) for l in written)} loc row(s) "
          f"across {len(written)} of {len(locs)} languages ({', '.join(written) or 'none'}); "
          f"backups *.bak_{tag}; every written file re-parsed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
