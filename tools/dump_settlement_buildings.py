#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Read-only dump of per-fief building levels from the LIVE TAOM_Map settlements.xml.

Companion / input generator for tools/apply_settlement_buildings.py (the writer). Mirrors the
analyze/rebalance pair convention of the prosperity tools: this is the READ side — it sources
accurate current levels for review, feeds the curation workflow (current_state.json), and is the
before/after verification diff for the applier.

Targets ONLY the LIVE engine-loaded file:
    E:\\Steam\\...\\Modules\\TAOM_Map\\ModuleData\\settlements.xml
(The repo's Main/_Module/ModuleData/settlements.xml is a stale shadow -- never read for truth.)

Building levels are the STARTING levels a new campaign seeds (Town.Deserialize, consumed once at
campaign creation). Valid range 0-3; fortifications floors at 1. Towns carry 12 building_settlement_*
buildings; castles carry 11 building_castle_* buildings (verified against vanilla DefaultBuildingTypes).

Usage:
    python3 tools/dump_settlement_buildings.py --culture gondor              # one culture, human table
    python3 tools/dump_settlement_buildings.py --culture gondor --towns-only
    python3 tools/dump_settlement_buildings.py --all --json                  # write current_state.json
    python3 tools/dump_settlement_buildings.py --culture gondor --json       # per-culture json too
Options:
    --culture X       filter to Culture.X (omit with --all for every fief)
    --all             every town + castle across all cultures
    --towns-only / --castles-only
    --json            also write JSON report(s) under tools/reports/settlement-buildings/
    --game-dir DIR    Bannerlord install root (default: E:\\Steam\\... or $BANNERLORD_GAME_DIR)
"""
import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
REPORT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "reports", "settlement-buildings")

# Canonical building sets, grounded in installed vanilla DefaultBuildingTypes (MaxLevel=3 all;
# fortifications StartLevel=1). Order = the vanilla settlements.xml roster order.
TOWN_BUILDINGS = [
    "building_settlement_fortifications", "building_settlement_barracks",
    "building_settlement_training_fields", "building_settlement_guard_house",
    "building_settlement_siege_workshop", "building_settlement_tax_office",
    "building_settlement_marketplace", "building_settlement_warehouse",
    "building_settlement_mason", "building_settlement_waterworks",
    "building_settlement_courthouse", "building_settlement_roads_and_paths",
]
CASTLE_BUILDINGS = [
    "building_castle_fortifications", "building_castle_barracks",
    "building_castle_training_fields", "building_castle_guard_house",
    "building_castle_siege_workshop", "building_castle_castallans_office",
    "building_castle_granary", "building_castle_craftmans_quarters",
    "building_castle_farmlands", "building_castle_mason",
    "building_castle_roads_and_paths",
]
FORT_TOWN = "building_settlement_fortifications"
FORT_CASTLE = "building_castle_fortifications"

# Short labels for compact display (strip the building_settlement_/building_castle_ prefix).
def short(bid):
    return bid.replace("building_settlement_", "").replace("building_castle_", "")


def game_dir(cli_value=None):
    return cli_value or os.environ.get("BANNERLORD_GAME_DIR") or DEFAULT_GAME_DIR


def live_settlements_path(cli_game_dir=None):
    return os.path.join(game_dir(cli_game_dir), "Modules", "TAOM_Map", "ModuleData", "settlements.xml")


def display_name(name_attr):
    """'{=Settlements.Settlement.name.town_EW1}Minas Tirith' -> 'Minas Tirith'."""
    return re.sub(r"^\{=+[^}]*\}", "", name_attr or "").strip()


def parse_fiefs(path):
    """Read-only parse -> list of town/castle records with their building levels.

    ET handles the UTF-8 BOM. Never used for writing (the applier does a byte round-trip instead).
    Villages/hideouts (no <Town>) are skipped.
    """
    if not os.path.isfile(path):
        raise SystemExit(f"FATAL: settlements.xml not found:\n  {path}")
    root = ET.parse(path).getroot()
    fiefs = []
    for s in root.iter("Settlement"):
        town = s.find(".//Town")
        if town is None:
            continue  # village or hideout
        is_castle = town.get("is_castle") == "true"
        buildings = {}
        b_el = town.find("Buildings")
        if b_el is not None:
            for b in b_el.findall("Building"):
                bid = b.get("id", "")
                try:
                    buildings[bid] = int(b.get("level", "0"))
                except ValueError:
                    buildings[bid] = 0
        fiefs.append({
            "id": s.get("id", ""),
            "name": display_name(s.get("name", "")),
            "culture": (s.get("culture") or "").replace("Culture.", ""),
            "is_castle": is_castle,
            "prosperity": int(float(town.get("prosperity", "0"))),
            "buildings": buildings,
        })
    return fiefs


def select(fiefs, culture=None, towns_only=False, castles_only=False):
    out = []
    for f in fiefs:
        if culture and f["culture"] != culture:
            continue
        if towns_only and f["is_castle"]:
            continue
        if castles_only and not f["is_castle"]:
            continue
        out.append(f)
    # towns first, then castles; within each, by prosperity desc then id
    out.sort(key=lambda f: (f["is_castle"], -f["prosperity"], f["id"]))
    return out


def print_fiefs(fiefs):
    cur_section = None
    for f in fiefs:
        section = ("Castles" if f["is_castle"] else "Towns", f["culture"])
        if section != cur_section:
            print(f"\n=== {f['culture']} — {section[0]} ===")
            cur_section = section
        order = CASTLE_BUILDINGS if f["is_castle"] else TOWN_BUILDINGS
        cells = " · ".join(f"{short(b)} {f['buildings'].get(b, '-')}" for b in order)
        print(f"\n{f['name']} ({f['id']}) — pros {f['prosperity']}")
        print(f"  {cells}")


def main():
    ap = argparse.ArgumentParser(description="Dump per-fief building levels from the LIVE TAOM_Map settlements.xml (read-only).")
    ap.add_argument("--culture", default=None, help="filter to Culture.X (e.g. gondor)")
    ap.add_argument("--all", action="store_true", help="every town + castle across all cultures")
    ap.add_argument("--towns-only", action="store_true")
    ap.add_argument("--castles-only", action="store_true")
    ap.add_argument("--json", action="store_true", help="also write JSON under tools/reports/settlement-buildings/")
    ap.add_argument("--game-dir", default=None)
    args = ap.parse_args()

    if not args.culture and not args.all:
        raise SystemExit("Specify --culture X or --all.")

    live = live_settlements_path(args.game_dir)
    fiefs = parse_fiefs(live)
    sel = select(fiefs, args.culture, args.towns_only, args.castles_only)
    print_fiefs(sel)

    towns = sum(1 for f in sel if not f["is_castle"])
    castles = sum(1 for f in sel if f["is_castle"])
    print(f"\n{len(sel)} fiefs ({towns} towns, {castles} castles).")

    if args.json:
        os.makedirs(REPORT_DIR, exist_ok=True)
        if args.all and not args.culture:
            out_path = os.path.join(REPORT_DIR, "current_state.json")
        else:
            tag = args.culture or "all"
            out_path = os.path.join(REPORT_DIR, f"current_{tag}.json")
        with open(out_path, "w", encoding="utf-8") as fh:
            json.dump(sel, fh, ensure_ascii=False, indent=2)
        print(f"JSON written: {out_path}")


if __name__ == "__main__":
    sys.exit(main())
