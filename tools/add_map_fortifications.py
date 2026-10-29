#!/usr/bin/env python3
"""Add town and castle <Settlement> rows to the LIVE TAOM_Map/settlements.xml, plus their 12 loc rows.

The sibling of tools/add_map_villages.py for fortifications, sharing its byte discipline and helpers
(binary read, each file's BOM and newline kept, a non-.xml backup, every written document parsed).
Villages bound to these fortifications go in add_map_villages.py's VILLAGES table and are added after
this script has run, because that tool refuses a village whose bound fortification has no row.

Positions come from the saved Main_map scene: posX/posY from the entity's transform, and the gate
from its child tagged `main_map_city_gate`, which is exactly what the editor's own save path reads
(SandBox.View SettlementPositionScript.LoadSettlementData / SaveSettlementPositions; the last
tagged child of GetChildrenRecursive's pre-order wins). The gate's world position is the child's
frame composed through every ancestor, rotation_euler applied in Z, X, Y order with the scale on
the columns. Before using that maths the script recomputes the gate of every fortification the
file already records and refuses to write unless all of them match within GATE_TOLERANCE
(2026-10-07: 231 gates, worst 0.0065; see GATE_TOLERANCE for why the worst is not 0). An entity
missing from the scene, or one with no gate child, is an error, never a placeholder: a
fortification row without its entity NREs SettlementVisual.OnStartup at map load (#269).

New rows are appended at the end of the file, towns first (the save-order rule,
add_map_villages.append_settlements). `after` places only the loc rows.

Idempotent per id: an id already in the master is skipped. `--check` fails on any table id whose
row, loc row or scene entity is missing, whose master name differs from the table's (the live map
is unversioned, so the table is the repo's only record of each name), or whose position or gate
drifted from the scene, and when this batch's rows (BATCH: the FORTS ids not in RELEASED, plus the villages bound to them) are
not the file's tail under a town. `--check` also names, and `--apply` refuses, a table name another
settlement already carries, case ignored (add_map_villages.name_collisions, #1042).

Usage:
  python tools/add_map_fortifications.py            # dry run
  python tools/add_map_fortifications.py --apply    # writes the live master + 12 loc files (+ backups)
  python tools/add_map_fortifications.py --check
"""
import argparse
import datetime
import math
import os
import re
import sys
from collections import namedtuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import add_map_villages as amv  # noqa: E402  LIVE, SCENE, LANGS, _read, _write, _loc_path, insert_* helpers

# The scene file stores transforms to 3 decimals. The measured worst recomputation is 0.0065
# (castle_AN10, 2026-10-07), and rounding its stored rotation by +-0.0005 alone could move the gate
# up to about 0.009, so 0.01 sat barely above the noise floor. A wrong rotation order misses by up to 4.8 units: 0.02 still
# tells maths from noise.
GATE_TOLERANCE = 0.02
GATE_TAG = "main_map_city_gate"

Fort = namedtuple("Fort", "id name owner culture kind scene prosperity after")

# Arthedain (2026-10-07). Mike placed the entities in the editor and approved the names, the
# capital (Fornost, town_AN2) and the fief split: clan 1 the King's, clans 2 and 3 tier 3, clans 4
# and 5 tier 2. A weak realm on purpose: prosperity well under Gondor's (EW towns 4500, castles
# 1000) and NOT on tools/settlement_economy_floor.json, whose 4800/950 floor would lift it.
# Scenes: unused vanilla empire towns (the entities are copies of Gondor's) and Gondor's castles.
ANCHOR = "castle_village_GBC4_4"  # `after` places only the loc rows: Arthedain's follow Blue Craig's
FORTS = [
    Fort("town_AN2", "Fornost Erain", "clan_arthedain_1", "arthedain", "town", "empire_town_c", 3000, ANCHOR),
    Fort("town_AN1", "Annúminas", "clan_arthedain_2", "arthedain", "town", "empire_town_d", 2600, "town_AN2"),
    Fort("town_AN3", "Bree", "clan_arthedain_3", "arthedain", "town", "empire_town_i", 2400, "town_AN1"),
    Fort("castle_AN9", "Barad Forn", "clan_arthedain_1", "arthedain", "castle", "taom_gondor_castle_001_forceatmo", 800, "town_AN3"),
    Fort("castle_AN10", "Emyn Uial", "clan_arthedain_2", "arthedain", "castle", "taom_gondor_castle_002_forceatmo", 800, "castle_AN9"),
    Fort("castle_AN4", "Amon Sûl", "clan_arthedain_3", "arthedain", "castle", "taom_gondor_castle_003_forceatmo", 800, "castle_AN10"),
    Fort("castle_AN5", "Hoarwell Watch", "clan_arthedain_4", "arthedain", "castle", "taom_gondor_castle_001_forceatmo", 800, "castle_AN4"),
    Fort("castle_AN6", "Brandywine Bridge", "clan_arthedain_4", "arthedain", "castle", "taom_gondor_castle_002_forceatmo", 800, "castle_AN5"),
    Fort("castle_AN8", "Emyn Beraid", "clan_arthedain_5", "arthedain", "castle", "taom_gondor_castle_003_forceatmo", 800, "castle_AN6"),
    Fort("castle_AN11", "Forochel", "clan_arthedain_5", "arthedain", "castle", "taom_gondor_castle_001_forceatmo", 800, "castle_AN8"),
    # Blue Craig's second town (Mike placed it 2026-10-07), held by the Crag-spawn (tier 5), who
    # had only Krathol. Prosperity at the 4800 town floor Blue Craig sits on
    # (tools/settlement_economy_floor.json); sturgia_town_e is the one sturgia town scene unused.
    Fort("town_GBC2", "Luinkrag", "clan_bluecraig_2", "bluecraig", "town", "sturgia_town_e", 4800, "village_GBC1_3"),
    # Gundabad's third town, Carn Dûm (Mike placed it 2026-10-15), held by Morzûg (tier 5), who had
    # only Bagbûrz. Prosperity at the 4800 Gundabad town floor (tools/settlement_economy_floor.json);
    # sturgia_town_a is the least used sturgia town scene.
    Fort("town_G3", "Carn Dûm", "clan_gundabad_4", "gundabad", "town", "sturgia_town_a", 4800, "village_G2_4"),
    # Gundabad's sixth castle (Mike placed it 2026-10-15), Lithur (Mike's name), the first fief of
    # Grishûk (tier 3, landless until now). Prosperity at the 950 Gundabad castle floor;
    # sturgia_castle_003 is the least used Gundabad castle scene.
    Fort("castle_G6", "Lithur", "clan_gundabad_6", "gundabad", "castle", "sturgia_castle_003", 950, "town_G3"),
    # Cardolan and Rhudaur (#978; Mike placed the entities 2026-10-23 and approved the names). Weak
    # realms like Arthedain: prosperity under Gondor's and no economy-floor row. The ruling clan holds
    # the capital and the castle nearest it; each other clan one fief. Scenes: unused vanilla empire
    # towns and Gondor's castles, as Arthedain's.
    Fort("town_CA2", "Caras Gorthad", "clan_cardolan_1", "cardolan", "town", "empire_town_f", 2800, "castle_village_G6_3"),
    Fort("town_CA1", "Tharbad", "clan_cardolan_2", "cardolan", "town", "empire_town_k", 2400, "town_CA2"),
    Fort("town_CA3", "Minas Eithel", "clan_cardolan_3", "cardolan", "town", "empire_town_l", 2400, "town_CA1"),
    Fort("castle_CA1", "Tyrn Gorthad", "clan_cardolan_1", "cardolan", "castle", "taom_gondor_castle_001_forceatmo", 800, "town_CA3"),
    Fort("castle_CA2", "Sarn Ford", "clan_cardolan_4", "cardolan", "castle", "taom_gondor_castle_002_forceatmo", 800, "castle_CA1"),
    Fort("town_RH1", "Cameth Brin", "clan_rhudaur_1", "rhudaur", "town", "empire_town_m", 2400, "castle_CA2"),
    Fort("castle_RH1", "Egladil", "clan_rhudaur_1", "rhudaur", "castle", "taom_gondor_castle_003_forceatmo", 800, "town_RH1"),
    Fort("castle_RH2", "Ettendale Hold", "clan_rhudaur_2", "rhudaur", "castle", "taom_gondor_castle_001_forceatmo", 800, "castle_RH1"),
    Fort("castle_RH3", "Coldfells", "clan_rhudaur_3", "rhudaur", "castle", "taom_gondor_castle_002_forceatmo", 800, "castle_RH2"),
    # Lindon (#1039; Mike placed the entities 2026-10-27 and named the two havens). One fief per clan:
    # Edhil Mithlond takes Harlond, the four new houses (tools/oneoff/author_lindon_clans.py) the rest.
    # Prosperity at the Lindon floor (tools/settlement_economy_floor.json). Battania scenes like the
    # other elven fiefs; battania_town_e and battania_castle_c were unused.
    Fort("town_LN2", "Harlond", "clan_lindon_2", "lindon", "town", "battania_town_e", 4800, "village_LN1_4"),
    Fort("town_LN3", "Forlond", "clan_lindon_3", "lindon", "town", "battania_town_c", 4800, "town_LN2"),
    Fort("castle_LN1", "Ost Falas", "clan_lindon_4", "lindon", "castle", "battania_castle_a", 950, "town_LN3"),
    Fort("castle_LN2", "Barad Luin", "clan_lindon_5", "lindon", "castle", "battania_castle_b", 950, "castle_LN1"),
    Fort("castle_LN3", "Tirith Aear", "clan_lindon_6", "lindon", "castle", "battania_castle_c", 950, "castle_LN2"),
    # Ered Luin, the dwarf kingdom of the Blue Mountains (Mike placed the entities 2026-10-27). The
    # ruling House of Borin holds Thorin's Halls (the capital) and the castle nearest it; the
    # Broadbeams Belegost, the Firebeards Nogrod, each with its nearest castle; the Wardens of Dolmed
    # the two castles between. Prosperity a step under Erebor's (towns 3500) and no economy-floor
    # row. Sturgia scenes, as every Erebor fief uses.
    Fort("town_EL3", "Thorin's Halls", "clan_eredluin_1", "eredluin", "town", "sturgia_town_g", 3200, "castle_village_LN3_3"),
    Fort("town_EL1", "Belegost", "clan_eredluin_2", "eredluin", "town", "sturgia_town_c", 3000, "town_EL3"),
    Fort("town_EL2", "Nogrod", "clan_eredluin_3", "eredluin", "town", "sturgia_town_b", 3000, "town_EL1"),
    Fort("castle_EL5", "Dwarrowgate", "clan_eredluin_1", "eredluin", "castle", "sturgia_castle_c", 800, "town_EL2"),
    Fort("castle_EL1", "Ironcrag", "clan_eredluin_2", "eredluin", "castle", "sturgia_castle_003", 800, "castle_EL5"),
    Fort("castle_EL4", "Stonewatch", "clan_eredluin_3", "eredluin", "castle", "sturgia_castle_c", 800, "castle_EL1"),
    Fort("castle_EL2", "Dolmed Hold", "clan_eredluin_4", "eredluin", "castle", "sturgia_castle_003", 800, "castle_EL4"),
    Fort("castle_EL3", "Hammerdeep", "clan_eredluin_4", "eredluin", "castle", "sturgia_castle_siege_001", 800, "castle_EL2"),
    # Anduin Vale (Beorn's Northmen) and Angaladh (the Woodmen of the Emyn Duir), two weak realms in
    # the Cardolan mould: capitals 2800, the other town 2400, castles 800, no economy-floor row. The
    # ruling clan holds the capital and the castle nearest it; each other clan one or two fiefs.
    # Dale's sturgia scenes, the least used town scenes first (sturgia_town_e once, _a and _d twice)
    # and Dale's three castle scenes in rotation.
    Fort("town_AV1", "Beorn's Hall", "clan_anduinvale_1", "anduinvale", "town", "sturgia_town_e", 2800, "castle_village_EL5_2"),
    Fort("town_AV2", "Old Ford", "clan_anduinvale_2", "anduinvale", "town", "sturgia_town_a", 2400, "town_AV1"),
    Fort("castle_AV4", "Eastbank", "clan_anduinvale_1", "anduinvale", "castle", "sturgia_castle_c", 800, "town_AV2"),
    Fort("castle_AV2", "High Pass Watch", "clan_anduinvale_2", "anduinvale", "castle", "sturgia_castle_003", 800, "castle_AV4"),
    Fort("castle_AV1", "Carrock", "clan_anduinvale_3", "anduinvale", "castle", "taom_dale_castle_001_forceatmo", 800, "castle_AV2"),
    Fort("castle_AV3", "Gladden Hold", "clan_anduinvale_4", "anduinvale", "castle", "sturgia_castle_c", 800, "castle_AV1"),
    Fort("town_AND1", "Woodmen-town", "clan_angaladh_1", "angaladh", "town", "sturgia_town_d", 2800, "castle_AV3"),
    Fort("castle_AND4", "Ironwood Hall", "clan_angaladh_1", "angaladh", "castle", "sturgia_castle_003", 800, "town_AND1"),
    Fort("castle_AND3", "Forest Gate", "clan_angaladh_2", "angaladh", "castle", "taom_dale_castle_001_forceatmo", 800, "castle_AND4"),
    Fort("castle_AND1", "Thornhold", "clan_angaladh_2", "angaladh", "castle", "sturgia_castle_c", 800, "castle_AND3"),
    Fort("castle_AND2", "Emyn Duir Watch", "clan_angaladh_3", "angaladh", "castle", "sturgia_castle_003", 800, "castle_AND1"),
]

# FORTS ids a release has shipped. The save-order check protects saves made BEFORE a batch only until the
# distance cache is rebuilt with the batch's ids; after that such a save is expected not to load (#1041). Once
# a release carries a batch, add its ids here and the next batch's rows become the required tail.
# Limit: a village a later batch binds to an already released fortification is not counted as new
# (batch_ids follows the bound fortification), so --check would name it; widen batch_ids first.
# Filled 2026-10-27 from the newest published TAOM_Map package (testing channel, v2.0.35), which
# carries 13 of FORTS; Cardolan, Rhudaur, Lindon and Ered Luin are not in it, so they are one batch:
#   python -c "import sys; sys.path.insert(0, 'tools'); import add_map_fortifications as m; t = open(sys.argv[1], 'rb').read().decode('utf-8-sig'); print([f.id for f in m.FORTS if '<Settlement id=\"%s\"' % f.id in t])" E:/LOTRAOM_Releases/testing/Modules/TAOM_Map/ModuleData/settlements.xml
# The public and patreon channels (v2.0.29.5) carry none of FORTS, so this no longer judges the
# boundary their saves meet, before town_GBC2.
RELEASED = {
    "town_AN2", "town_AN1", "town_AN3", "castle_AN9", "castle_AN10", "castle_AN4", "castle_AN5",
    "castle_AN6", "castle_AN8", "castle_AN11", "town_GBC2", "town_G3", "castle_G6",
}


def batch_ids(forts=None, villages=None, released=None):
    """This batch's rows: the unreleased FORTS ids plus every village bound to one of them."""
    forts = FORTS if forts is None else forts
    villages = amv.VILLAGES if villages is None else villages
    released = RELEASED if released is None else released
    ids = {f.id for f in forts if f.id not in released}
    return ids | {v.id for v in villages if v.bound in ids}


def append_order(forts):
    """Towns first (a town heads the appended tail, see save_order_findings), else table order."""
    return sorted(forts, key=lambda f: f.kind != "town")

# Low levels: a poor realm's walls and markets. Wall level 1 everywhere, so sieges are short.
TOWN_BUILDINGS = [
    ("building_settlement_fortifications", 1), ("building_settlement_barracks", 1),
    ("building_settlement_training_fields", 1), ("building_settlement_guard_house", 0),
    ("building_settlement_siege_workshop", 1), ("building_settlement_tax_office", 1),
    ("building_settlement_marketplace", 1), ("building_settlement_warehouse", 1),
    ("building_settlement_mason", 1), ("building_settlement_waterworks", 0),
    ("building_settlement_courthouse", 0), ("building_settlement_roads_and_paths", 1),
]
CASTLE_BUILDINGS = [
    ("building_castle_fortifications", 1), ("building_castle_barracks", 1),
    ("building_castle_training_fields", 1), ("building_castle_guard_house", 0),
    ("building_castle_siege_workshop", 0), ("building_castle_castallans_office", 1),
    ("building_castle_granary", 1), ("building_castle_craftmans_quarters", 0),
    ("building_castle_farmlands", 1), ("building_castle_mason", 0),
    ("building_castle_roads_and_paths", 1),
]

# Per culture, everything a fortification row needs beyond its own table fields, each copied from a
# live row of that culture: Arthedain from Gondor's (town_EW7, castle_EW1), Blue Craig from its own
# town_GBC1 (sturgia scenes, gate rotation 0.378, its building levels).
STYLE = {
    "arthedain": dict(town_menu="menu_empire_3", castle_menu="menu_empire_1", wait="wait_empire_town",
                      gate_rotation={"town": "0.808", "castle": "0.908"},
                      arena="arena_empire_a", tavern="empire_house_c_tavern_a",
                      keep=("empire_castle_keep_a_l1_interior", "empire_castle_keep_a_l2_interior",
                            "empire_castle_keep_a_l3_interior"),
                      dungeon="empire_dungeon_stealth", house="empire_house_d_interior_house",
                      town_buildings=TOWN_BUILDINGS, castle_buildings=CASTLE_BUILDINGS),
    "bluecraig": dict(town_menu="gui_bg_town_sturgia", castle_menu="gui_bg_castle_sturgia", wait="wait_sturgia_town",
                      gate_rotation={"town": "0.378", "castle": "0.908"},
                      arena="arena_sturgia_a", tavern="sturgia_house_b_interior_tavern",
                      keep=("sturgia_castle_keep_a_l2_interior", "sturgia_castle_keep_a_l2_interior",
                            "sturgia_castle_keep_a_l3_interior"),
                      dungeon="sturgia_dungeon_stealth", house="sturgia_town_house_d1_interior_b_house",
                      town_buildings=[
                          ("building_settlement_fortifications", 2), ("building_settlement_barracks", 2),
                          ("building_settlement_training_fields", 1), ("building_settlement_guard_house", 1),
                          ("building_settlement_siege_workshop", 2), ("building_settlement_tax_office", 1),
                          ("building_settlement_marketplace", 1), ("building_settlement_warehouse", 1),
                          ("building_settlement_mason", 1), ("building_settlement_waterworks", 0),
                          ("building_settlement_courthouse", 0), ("building_settlement_roads_and_paths", 1)],
                      castle_buildings=CASTLE_BUILDINGS),
    # Gundabad from its own town_G1 and castle_G1 (sturgia scenes and meshes, gate rotations, building levels).
    "gundabad": dict(town_menu="gui_bg_town_sturgia", castle_menu="gui_bg_castle_sturgia", wait="wait_sturgia_town",
                     gate_rotation={"town": "0.378", "castle": "0.908"},
                     arena="arena_sturgia_a", tavern="sturgia_house_b_interior_tavern",
                     keep=("sturgia_castle_keep_a_l2_interior", "sturgia_castle_keep_a_l2_interior",
                           "sturgia_castle_keep_a_l3_interior"),
                     dungeon="sturgia_dungeon_stealth", house="sturgia_town_house_d1_interior_b_house",
                     town_buildings=[
                         ("building_settlement_fortifications", 3), ("building_settlement_barracks", 3),
                         ("building_settlement_training_fields", 3), ("building_settlement_guard_house", 2),
                         ("building_settlement_siege_workshop", 3), ("building_settlement_tax_office", 2),
                         ("building_settlement_marketplace", 2), ("building_settlement_warehouse", 3),
                         ("building_settlement_mason", 3), ("building_settlement_waterworks", 2),
                         ("building_settlement_courthouse", 2), ("building_settlement_roads_and_paths", 2)],
                     castle_buildings=[
                         ("building_castle_fortifications", 2), ("building_castle_barracks", 2),
                         ("building_castle_training_fields", 1), ("building_castle_guard_house", 0),
                         ("building_castle_siege_workshop", 2), ("building_castle_castallans_office", 1),
                         ("building_castle_granary", 1), ("building_castle_craftmans_quarters", 0),
                         ("building_castle_farmlands", 1), ("building_castle_mason", 0),
                         ("building_castle_roads_and_paths", 1)]),
}
# Cardolan and Rhudaur take Arthedain's style: the same Gondor-built scenes and weak building levels.
STYLE["cardolan"] = STYLE["rhudaur"] = STYLE["arthedain"]
# Lindon from its own town_LN1 (battania scenes and meshes, gate rotation, building levels) and the
# elven castle_M1 (Lindon had no castle).
STYLE["lindon"] = dict(town_menu="gui_bg_town_battania", castle_menu="gui_bg_castle_battania", wait="wait_battania_town",
                       gate_rotation={"town": "0.378", "castle": "0.908"},
                       arena="arena_battania_a", tavern="battania_town_house_a_interior_a_tavern",
                       keep=("battania_castle_keep_a_l1_interior", "battania_castle_keep_a_l2_interior",
                             "battania_castle_keep_a_l3_interior"),
                       dungeon="battania_dungeon_stealth", house="battania_town_house_b_interior_b_house",
                       town_buildings=[
                           ("building_settlement_fortifications", 2), ("building_settlement_barracks", 2),
                           ("building_settlement_training_fields", 1), ("building_settlement_guard_house", 1),
                           ("building_settlement_siege_workshop", 1), ("building_settlement_tax_office", 2),
                           ("building_settlement_marketplace", 3), ("building_settlement_warehouse", 3),
                           ("building_settlement_mason", 2), ("building_settlement_waterworks", 3),
                           ("building_settlement_courthouse", 1), ("building_settlement_roads_and_paths", 3)],
                       castle_buildings=[
                           ("building_castle_fortifications", 2), ("building_castle_barracks", 1),
                           ("building_castle_training_fields", 1), ("building_castle_guard_house", 0),
                           ("building_castle_siege_workshop", 1), ("building_castle_castallans_office", 1),
                           ("building_castle_granary", 1), ("building_castle_craftmans_quarters", 0),
                           ("building_castle_farmlands", 1), ("building_castle_mason", 1),
                           ("building_castle_roads_and_paths", 2)])
# Ered Luin from Erebor's town_E2 and castle_E1 (sturgia scenes and meshes, gate rotations), with
# building levels a step under Erebor's: strong walls and masons, poorer markets.
STYLE["eredluin"] = dict(STYLE["gundabad"],
                         town_buildings=[
                             ("building_settlement_fortifications", 2), ("building_settlement_barracks", 2),
                             ("building_settlement_training_fields", 1), ("building_settlement_guard_house", 1),
                             ("building_settlement_siege_workshop", 1), ("building_settlement_tax_office", 1),
                             ("building_settlement_marketplace", 1), ("building_settlement_warehouse", 2),
                             ("building_settlement_mason", 2), ("building_settlement_waterworks", 1),
                             ("building_settlement_courthouse", 0), ("building_settlement_roads_and_paths", 1)],
                         castle_buildings=[
                             ("building_castle_fortifications", 2), ("building_castle_barracks", 1),
                             ("building_castle_training_fields", 1), ("building_castle_guard_house", 0),
                             ("building_castle_siege_workshop", 1), ("building_castle_castallans_office", 1),
                             ("building_castle_granary", 1), ("building_castle_craftmans_quarters", 1),
                             ("building_castle_farmlands", 1), ("building_castle_mason", 1),
                             ("building_castle_roads_and_paths", 1)])

# Anduin Vale and Angaladh from Dale's own rows (town_S1, castle_S2): sturgia menus, wait mesh,
# arena, tavern, keeps, dungeon and house are Gundabad's, and the gate rotations are Dale's
# (town_S1 0.608, the S castles 0.908). Building levels are Cardolan's, a weak realm's.
STYLE["anduinvale"] = STYLE["angaladh"] = dict(STYLE["gundabad"], gate_rotation={"town": "0.608", "castle": "0.908"},
                                               town_buildings=TOWN_BUILDINGS, castle_buildings=CASTLE_BUILDINGS)


# ---------------------------------------------------------------- scene transforms

_TOK = re.compile(r'<game_entity\b[^>]*>|</game_entity>|<transform\b[^>]*/>|<tag name="([^"]+)"/>')


def entity_subtree(scene_text, name):
    m = re.search(r'<game_entity name=' + re.escape(f'"{name}"') + r'[^>]*>', scene_text)
    if not m:
        return None
    depth = 0
    for t in re.finditer(r'<game_entity\b[^>]*>|</game_entity>', scene_text[m.start():]):
        depth += 1 if t.group(0).startswith("<game_entity") else -1
        if depth == 0:
            return scene_text[m.start():m.start() + t.end()]
    return None


def _parse_tree(txt):
    stack, root = [], None
    for t in _TOK.finditer(txt):
        s = t.group(0)
        if s.startswith("<game_entity"):
            node = {"tf": None, "tags": [], "kids": []}
            (stack[-1]["kids"] if stack else []).append(node)
            root = root or node
            stack.append(node)
        elif s == "</game_entity>":
            stack.pop()
        elif s.startswith("<transform"):
            if stack and stack[-1]["tf"] is None:
                stack[-1]["tf"] = s
        elif t.group(1) and stack:
            stack[-1]["tags"].append(t.group(1))
    return root


def _vec(tf, key, default):
    m = re.search(key + r'="([^"]+)"', tf or "")
    return [float(x) for x in m.group(1).split(",")] if m else list(default)


def _mm(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def _mv(a, v):
    return [sum(a[i][k] * v[k] for k in range(3)) for i in range(3)]


def _rot(axis, ang):
    c, s = math.cos(ang), math.sin(ang)
    return {"x": [[1, 0, 0], [0, c, -s], [0, s, c]],
            "y": [[c, 0, s], [0, 1, 0], [-s, 0, c]],
            "z": [[c, -s, 0], [s, c, 0], [0, 0, 1]]}[axis]


def _frame(tf):
    e = dict(zip("xyz", _vec(tf, "rotation_euler", (0, 0, 0))))
    sc = _vec(tf, "scale", (1, 1, 1))
    r = _mm(_mm(_rot("z", e["z"]), _rot("x", e["x"])), _rot("y", e["y"]))
    return [[r[i][j] * sc[j] for j in range(3)] for i in range(3)], _vec(tf, "position", (0, 0, 0))


def _find_gate(node, rot=None, pos=None):
    """World position of the LAST gate-tagged descendant in pre-order, as the editor resolves it
    (GetChildrenRecursive, overwriting on every match), or None."""
    rl, pl = _frame(node["tf"])
    if rot is None:
        rg, pg = rl, pl
    else:
        rg, pg = _mm(rot, rl), [a + b for a, b in zip(_mv(rot, pl), pos)]
    found = pg if rot is not None and GATE_TAG in node["tags"] else None
    for kid in node["kids"]:
        found = _find_gate(kid, rg, pg) or found
    return found


def scene_gate(scene_text, sid):
    """(x, y) of the fortification's gate child in world space, or None."""
    sub = entity_subtree(scene_text, sid)
    if not sub:
        return None
    g = _find_gate(_parse_tree(sub))
    return (g[0], g[1]) if g else None


def validate_gate_maths(scene_text, master_text):
    """Recompute every recorded gate. Returns (checked, worst error)."""
    worst, n = 0.0, 0
    for m in re.finditer(r'<Settlement id="((?:town|castle)_[^"]+)"[^>]*\sgate_posX="([^"]+)"\s+gate_posY="([^"]+)"',
                         master_text):
        g = scene_gate(scene_text, m.group(1))
        if g is None:
            continue
        n += 1
        worst = max(worst, math.hypot(g[0] - float(m.group(2)), g[1] - float(m.group(3))))
    return n, worst


# ---------------------------------------------------------------- rows


def fort_block(f, pos, gate, nl):
    if f.culture not in STYLE:
        raise ValueError(f"{f.id}: no STYLE recorded for culture {f.culture!r}")
    st = STYLE[f.culture]
    castle = f.kind == "castle"
    key = f.id.split("_", 1)[1]
    comp = f"castle_comp_{key}" if castle else f"town_comp_{key}"
    buildings = "".join(f'          <Building id="{b}" level="{lv}" />\n'
                        for b, lv in (st["castle_buildings"] if castle else st["town_buildings"]))
    sc = f.scene
    center = f'      <Location id="center" scene_name="{sc}" scene_name_1="{sc}" scene_name_2="{sc}" scene_name_3="{sc}" />\n'
    keep = (f'      <Location id="lordshall" scene_name_1="{st["keep"][0]}" '
            f'scene_name_2="{st["keep"][1]}" scene_name_3="{st["keep"][2]}" />\n'
            f'      <Location id="prison" scene_name="{st["dungeon"]}" />\n')
    if castle:
        locations, template, common = center + keep, "castle_complex", ""
    else:
        locations = (center + f'      <Location id="arena" scene_name="{st["arena"]}" />\n'
                     f'      <Location id="tavern" scene_name="{st["tavern"]}" />\n' + keep
                     + "".join(f'      <Location id="house_{i}" scene_name="{st["house"]}" />\n'
                               for i in (1, 2, 3))
                     + '      <Location id="alley" />\n')
        template = "town_complex"
        common = ('    <CommonAreas>\n'
                  '      <Area type="Backstreet" name="{=a0MVffcN}Backstreet" />\n'
                  '      <Area type="Clearing" name="{=LWHIVshb}Clearing" />\n'
                  '      <Area type="Waterfront" name="{=Rr1cy5Sk}Waterfront" />\n'
                  '    </CommonAreas>\n')
    return (
        f'  <Settlement id="{f.id}" name="{{=Settlements.Settlement.name.{f.id}}}{f.name}" owner="Faction.{f.owner}" '
        f'posX="{pos[0]}" posY="{pos[1]}" culture="Culture.{f.culture}" gate_posX="{gate[0]:.4f}" gate_posY="{gate[1]:.4f}">\n'
        f'    <Components>\n'
        f'      <Town id="{comp}" is_castle="{"true" if castle else "false"}" background_crop_position="0.0" '
        f'background_mesh="{st["castle_menu"] if castle else st["town_menu"]}" wait_mesh="{st["wait"]}" '
        f'gate_rotation="{st["gate_rotation"][f.kind]}" '
        f'prosperity="{f.prosperity}">\n'
        f'        <Buildings>\n{buildings}        </Buildings>\n'
        f'      </Town>\n'
        f'    </Components>\n'
        f'    <Locations complex_template="LocationComplexTemplate.{template}">\n{locations}    </Locations>\n'
        f'{common}'
        f'  </Settlement>\n'
    ).replace("\n", nl)


def master_gate(master_text, sid):
    m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"') + r'[^>]*?\sgate_posX="([^"]*)"\s+gate_posY="([^"]*)"', master_text)
    return (float(m.group(1)), float(m.group(2))) if m else None


def master_name(master_text, sid):
    m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"') + r'[^>]*?\sname="\{=[^}]*\}([^"]*)"', master_text)
    return m.group(1) if m else None


def check(forts, master_text, scene_text, loc_texts, villages=(), released=frozenset()):
    findings = amv.save_order_findings(master_text, batch_ids(forts, villages, released))
    findings += amv.name_collisions([(f.id, f.name) for f in forts], master_text)
    for f in forts:
        pos = amv.settlement_position(master_text, f.id)
        if not pos:
            findings.append(f"{f.id}: no <Settlement> row in settlements.xml")
        name = master_name(master_text, f.id)
        if name is not None and name != f.name:
            findings.append(f"{f.id}: settlements.xml names it {name!r}, the table {f.name!r}")
        for lang, text in loc_texts.items():
            if f'id="Settlements.Settlement.name.{f.id}"' not in text:
                findings.append(f"{f.id}: no loc row in Languages/{lang}/loc_settlements.xml")
        scene = amv.scene_position(scene_text, f.id)
        gate = scene_gate(scene_text, f.id)
        if not scene:
            findings.append(f"{f.id}: no entity in scene.xscene (a campaign load would NRE SettlementVisual.OnStartup)")
            continue
        if gate is None:
            findings.append(f"{f.id}: entity has no child tagged {GATE_TAG}")
        if pos and (abs(float(pos[0]) - float(scene[0])) > GATE_TOLERANCE or abs(float(pos[1]) - float(scene[1])) > GATE_TOLERANCE):
            findings.append(f"{f.id}: settlements.xml posX={pos[0]} posY={pos[1]} but the scene has {scene[0]}, {scene[1]}")
        mg = master_gate(master_text, f.id)
        if pos and gate and (not mg or math.hypot(mg[0] - gate[0], mg[1] - gate[1]) > GATE_TOLERANCE):
            findings.append(f"{f.id}: gate in settlements.xml {mg} does not match the scene's {gate[0]:.4f}, {gate[1]:.4f}")
    return findings


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    for p in (amv.LIVE, amv.SCENE):
        if not os.path.isfile(p):
            print(f"ERROR: not found: {p}", file=sys.stderr)
            return 2
    master_bom, master = amv._read(amv.LIVE)
    scene = open(amv.SCENE, "rb").read().decode("utf-8", errors="replace")
    locs = {lang: amv._read(amv._loc_path(lang)) for lang in amv.LANGS if os.path.isfile(amv._loc_path(lang))}
    if len(locs) != len(amv.LANGS):
        print(f"ERROR: expected {len(amv.LANGS)} loc_settlements.xml files, found {len(locs)}", file=sys.stderr)
        return 2
    loc_texts = {lang: text for lang, (_, text) in locs.items()}

    if args.check:
        findings = check(FORTS, master, scene, loc_texts, amv.VILLAGES, RELEASED)
        for f in findings:
            print("FAIL " + f)
        if findings:
            print(f"\n{len(findings)} finding(s) across {len(FORTS)} table id(s).")
            return 1
        print(f"OK: all {len(FORTS)} fortifications present with loc rows; names match the table and no other settlement carries them; positions and gates match the scene; "
              f"the batch's {len(batch_ids())} rows are the file's tail under a town.")
        return 0

    n, worst = validate_gate_maths(scene, master)
    print(f"gate maths: {n} recorded gates recomputed, worst error {worst:.5f}")
    if n == 0 or worst > GATE_TOLERANCE:
        print("REFUSING: the gate computation no longer reproduces the recorded gates", file=sys.stderr)
        return 1

    todo = append_order([f for f in FORTS if f'id="{f.id}"' not in master])
    loc_plan = {lang: [(f.id, f.name) for f in FORTS if f'id="Settlements.Settlement.name.{f.id}"' not in text]
                for lang, text in loc_texts.items()}
    if not todo and not any(loc_plan.values()):
        print(f"all {len(FORTS)} fortifications already present; nothing to do. Try --check.")
        return 0

    nl = amv.detect_newline(master)
    errors, blocks = [], []
    print(f"Plan: +{len(todo)} fortification(s) ({len(FORTS) - len(todo)} already present)")
    for f in todo:
        pos = amv.scene_position(scene, f.id)
        gate = scene_gate(scene, f.id)
        if not pos or gate is None:
            errors.append(f"{f.id}: {'no scene entity' if not pos else 'no gate child'}")
            continue
        blocks.append((f, fort_block(f, pos, gate, nl)))
        print(f"  {f.id:12} {f.name:18} {f.kind:6} owner {f.owner:17} at {pos[0]}, {pos[1]}  gate {gate[0]:.3f}, {gate[1]:.3f}  {f.scene}")
    errors += amv.name_collisions([(f.id, f.name) for f in todo], master)
    if errors:
        print("REFUSING:\n  " + "\n  ".join(errors), file=sys.stderr)
        return 1
    for lang in amv.LANGS:
        print(f"  loc {lang:3}: +{len(loc_plan[lang])} row(s)")
    if not args.apply:
        print("\nDRY RUN: re-run with --apply to write the live files.")
        return 0

    tag = "mapforts_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    amv._write(amv.LIVE, master_bom, amv.append_settlements(master, [block for _, block in blocks]), tag)
    by_id = {f.id: f for f in FORTS}
    for lang, (bom, ltext) in locs.items():
        if loc_plan[lang]:
            for sid, name in loc_plan[lang]:
                ltext = amv.insert_loc_rows(ltext, by_id[sid].after, [(sid, name)])
            amv._write(amv._loc_path(lang), bom, ltext, tag)
    print(f"\nApplied: +{len(blocks)} fortification(s), loc rows in {sum(1 for l in loc_plan if loc_plan[l])} "
          f"language(s); backups *.bak_{tag}; every written file re-parsed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
