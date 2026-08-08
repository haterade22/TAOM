#!/usr/bin/env python3
"""Add the 4 Blue Craig castles + their 11 castle-villages to the LIVE TAOM_Map/settlements.xml.

The map author placed castle_GBC1..4 + castle_village_GBC{1..4}_* entities in the worldmap scene
(Main_map/scene.xscene) but settlements.xml had no data for them, so they were inert (not fiefs).
Every settlement id here is verified present in scene.xscene (positions are taken FROM the scene, so
the settlement icon and the placed entity align). Adding settlement DATA for an id with no scene
entity would crash SettlementVisual.OnStartup at map load -- so this script ONLY adds the ids the
author already placed.

Ownership is spread across the 5 Blue Craig clans: clan_bluecraig_1 keeps the capital (town_GBC1);
the 4 castles go to clans 2-5 (one fief per clan). Castles are goblin-culture fortifications cloned
from the castle_MM1 structure; castle-villages clone castle_village_MM1_1 with the orc per-fief
economy (animal food + a mine; the 4-village fief adds a lumberjack). All village_type ids are
verified against the v1.4.5 DefaultVillageTypes registry.

Idempotent: skips if castle_GBC1 is already present. Preserves UTF-8 BOM + CRLF. Writes a backup.

Usage:
  python tools/add_bluecraig_castles.py            # dry-run (prints plan)
  python tools/add_bluecraig_castles.py --apply    # writes the live file (+ backup)
"""
import argparse, re, shutil, datetime, xml.etree.ElementTree as ET
from _gamedir import game_dir

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
LIVE = GAME + r"/Modules/TAOM_Map/ModuleData/settlements.xml"
VALID_VILLAGE_TYPES = {"swine_farm", "cattle_farm", "sheep_farm", "wheat_farm", "vineyard",
                       "fisherman", "lumberjack", "iron_mine", "silver_mine", "clay_mine",
                       "salt_mine", "flax_plant", "date_farm", "olive_trees", "silk_plant", "trapper"}

# (id, display name, posX, posY, owner clan) -- positions FROM scene.xscene transforms.
CASTLES = [
    ("castle_GBC1", "Krathol",   "250.684", "1200.344", "clan_bluecraig_2"),
    ("castle_GBC2", "Gorgrim",   "223.550", "1208.933", "clan_bluecraig_3"),
    ("castle_GBC3", "Skarnak",   "212.130", "1279.103", "clan_bluecraig_4"),
    ("castle_GBC4", "Bolgkrag",  "280.601", "1311.216", "clan_bluecraig_5"),
]
# (id, display name, posX, posY, bound castle, village_type)
CASTLE_VILLAGES = [
    ("castle_village_GBC1_1", "Grishmor", "260.425", "1238.418", "castle_GBC1", "swine_farm"),
    ("castle_village_GBC1_2", "Nazdush",  "267.705", "1208.308", "castle_GBC1", "iron_mine"),
    ("castle_village_GBC2_1", "Mokra",    "199.975", "1213.617", "castle_GBC2", "cattle_farm"),
    ("castle_village_GBC2_2", "Vorbag",   "211.860", "1221.086", "castle_GBC2", "silver_mine"),
    ("castle_village_GBC3_1", "Lugbol",   "184.269", "1272.203", "castle_GBC3", "swine_farm"),
    ("castle_village_GBC3_2", "Snagri",   "166.103", "1265.098", "castle_GBC3", "cattle_farm"),
    ("castle_village_GBC3_3", "Durgol",   "175.419", "1286.000", "castle_GBC3", "iron_mine"),
    ("castle_village_GBC3_4", "Mazgar",   "204.568", "1306.858", "castle_GBC3", "lumberjack"),
    ("castle_village_GBC4_1", "Ufthak",   "287.148", "1337.777", "castle_GBC4", "swine_farm"),
    ("castle_village_GBC4_2", "Radbug",   "267.843", "1332.525", "castle_GBC4", "cattle_farm"),
    ("castle_village_GBC4_3", "Gorbag",   "246.831", "1325.109", "castle_GBC4", "silver_mine"),
    ("castle_village_GBC4_4", "Lagduf",   "220.095", "1314.570", "castle_GBC4", "lumberjack"),
]

CASTLE_BUILDINGS = """          <Building id="building_castle_fortifications" level="1" />
          <Building id="building_castle_barracks" level="1" />
          <Building id="building_castle_training_fields" level="1" />
          <Building id="building_castle_guard_house" level="0" />
          <Building id="building_castle_siege_workshop" level="0" />
          <Building id="building_castle_castallans_office" level="1" />
          <Building id="building_castle_granary" level="1" />
          <Building id="building_castle_craftmans_quarters" level="0" />
          <Building id="building_castle_farmlands" level="1" />
          <Building id="building_castle_mason" level="0" />
          <Building id="building_castle_roads_and_paths" level="1" />"""


def castle_block(cid, name, px, py, owner, nl):
    return (
        f'  <Settlement id="{cid}" name="{{=Settlements.Settlement.name.{cid}}}{name}" owner="Faction.{owner}" posX="{px}" posY="{py}" culture="Culture.goblin" gate_posX="{px}" gate_posY="{py}">\n'
        f'    <Components>\n'
        f'      <Town id="castle_comp_{cid}" is_castle="true" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="600">\n'
        f'        <Buildings>\n{CASTLE_BUILDINGS}\n        </Buildings>\n'
        f'      </Town>\n'
        f'    </Components>\n'
        f'    <Locations complex_template="LocationComplexTemplate.castle_complex">\n'
        f'      <Location id="center" scene_name="sturgia_castle_siege_001" scene_name_1="sturgia_castle_siege_001" scene_name_2="sturgia_castle_siege_001" scene_name_3="sturgia_castle_siege_001" />\n'
        f'      <Location id="lordshall" scene_name_1="sturgia_castle_keep_a_l1_interior" scene_name_2="sturgia_castle_keep_a_l2_interior" scene_name_3="sturgia_castle_keep_a_l3_interior" />\n'
        f'      <Location id="prison" scene_name="sturgia_dungeon_stealth" />\n'
        f'    </Locations>\n'
        f'  </Settlement>\n'
    ).replace("\n", nl)


def village_block(vid, name, px, py, bound, vtype, nl):
    if vtype not in VALID_VILLAGE_TYPES:
        raise ValueError(f"invalid VillageType id {vtype!r}")
    return (
        f'  <Settlement id="{vid}" name="{{=Settlements.Settlement.name.{vid}}}{name}" posX="{px}" posY="{py}" culture="Culture.goblin">\n'
        f'    <Components>\n'
        f'      <Village id="village_comp_{vid}" village_type="VillageType.{vtype}" hearth="300" bound="Settlement.{bound}" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />\n'
        f'    </Components>\n'
        f'    <Locations complex_template="LocationComplexTemplate.village_complex">\n'
        f'      <Location id="village_center" scene_name="sturgia_village_f" />\n'
        f'    </Locations>\n'
        f'    <CommonAreas>\n'
        f'      <Area type="Pasture" name="{{=fOUsLdZR}}Pasture" />\n'
        f'      <Area type="Thicket" name="{{=66Mzk0NZ}}Thicket" />\n'
        f'      <Area type="Bog" name="{{=iXA5SttU}}Bog" />\n'
        f'    </CommonAreas>\n'
        f'  </Settlement>\n'
    ).replace("\n", nl)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    rawb = open(LIVE, "rb").read()
    had_bom = rawb.startswith(b"\xef\xbb\xbf")
    text = rawb.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"

    # per-id idempotency: add only the settlements not already present (so re-running picks up a
    # newly-placed castle-village, e.g. GBC4_4, without duplicating the rest).
    missing_castles = [c for c in CASTLES if f'id="{c[0]}"' not in text]
    missing_villages = [v for v in CASTLE_VILLAGES if f'id="{v[0]}"' not in text]
    if not missing_castles and not missing_villages:
        print("all Blue Craig castles + castle-villages already present — nothing to do (idempotent).")
        return

    blocks = [castle_block(*c, nl) for c in missing_castles] + [village_block(*v, nl) for v in missing_villages]
    print(f"Plan: +{len(missing_castles)} castle(s), +{len(missing_villages)} castle-village(s) (only ids not already in settlements.xml)")
    for cid, name, _, _, owner in missing_castles:
        print(f"  {cid:22} {name:9} -> {owner}")
    for vid, name, _, _, bound, vtype in missing_villages:
        print(f"  {vid:22} {name:9} -> bound {bound} ({vtype})")
    if not args.apply:
        print("\nDRY RUN — re-run with --apply to write the live file.")
        return

    idx = text.rfind("</Settlements>")
    if idx == -1:
        raise RuntimeError("</Settlements> close tag not found")
    new = text[:idx] + "".join(blocks) + text[idx:]
    ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    shutil.copy2(LIVE, LIVE + f".bak_gbccastles_{ts}")
    open(LIVE, "wb").write((("﻿" if had_bom else "") + new).encode("utf-8"))
    ET.parse(LIVE)
    print(f"\nApplied: +{len(blocks)} settlements (backup .bak_gbccastles_{ts}); XML well-formed.")


if __name__ == "__main__":
    main()
