#!/usr/bin/env python3
"""Drift gate: no settlement in the live TAOM_Map still enters at a known unreachable value (#1434).

Fourteen entrances in TAOM_Map 2.0.34 sat on navmesh faces land parties could not reach: on the main
island, but on terrain land navigation excludes or cut off by it. AI parties that targeted them failed their
path query, the distance cache held them unreachable, and armies never arrived. Silas He's report named seven;
the audit run of 2026-11-17 confirmed five of them (Orthanc and Isengard passed its path check) and found nine more.
An entrance is `gate_posX`/`gate_posY` when the row has `gate_posX`, else `posX`/`posY`, as
`Settlement.Deserialize` reads it (v1.5.7 Settlement.cs:961-967). The fix moves each one in the map
editor (a fortification's `main_map_city_gate` child, or the entity), which writes the row, or in scene.xscene
and the row together; a value pasted into settlements.xml alone reverts at the next editor save
(docs/modding/settlements.md step 7).

TAOM_Map is unversioned, so a reinstall of an older package brings the old values back without a
sign. This gate covers only the entrances in ISLAND_ENTRANCES. `taom.audit_settlement_entrances` finds new
ones (unattended: `python tools/run_scenario.py --days 1 --command taom.audit_settlement_entrances`);
after the editor fix, add each old value here.

Exit 0 when none of them holds its old value, 1 when one does or its row is missing or unreadable,
2 when the live file is missing or does not parse.

Usage:
  python tools/check_settlement_entrances.py --check
"""
import argparse
import math
import os
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir  # noqa: E402

GAME = game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
LIVE = GAME + r"/Modules/TAOM_Map/ModuleData/settlements.xml"

# The 2.0.34 entrances the audit reported on 2026-11-17, read from the live file before the move.
ISLAND_ENTRANCES = {
    "castle_EN6": (591.8705, 866.4027),
    "castle_A19": (775.5239, 11.34126),
    "castle_DO3": (1204.896, 1051.759),
    "hideout_forest_9": (737.61, 827.423),
    "hideout_desert_7": (1072.412, 374.593),
    "village_LN1_3": (232.057, 1078.803),
    "village_CA2_1": (456.817, 908.667),
    "village_CA3_1": (390.894, 839.236),
    "castle_village_LN1_2": (171.105, 1040.946),
    "castle_village_LN2_1": (223.413, 964.504),
    "castle_village_EL1_2": (199.365, 1175.712),
    "castle_village_EL4_2": (298.802, 948.579),
    "castle_village_AND1_1": (934.867, 1079.858),
    "village_SRH1_1": (947.424, 1052.796),
}

# The editor writes about four decimals; anything closer than this is the same point.
TOLERANCE = 0.01


def _number(text):
    try:
        value = float(text)
    except (TypeError, ValueError):
        return None
    return value if math.isfinite(value) else None


def entrances(text):
    """{id: (x, y)} the engine enters each settlement at; None for a coordinate that is missing or not a number.
    Raises ET.ParseError on a file that does not parse."""
    out = {}
    for s in ET.fromstring(text).iter("Settlement"):
        kx, ky = ("gate_posX", "gate_posY") if s.get("gate_posX") is not None else ("posX", "posY")
        out[s.get("id")] = (_number(s.get(kx)), _number(s.get(ky)))
    return out


def findings(text):
    found = entrances(text)
    out = []
    for sid, (bx, by) in ISLAND_ENTRANCES.items():
        x, y = found.get(sid, (None, None))
        if x is None or y is None:
            out.append(f"{sid}: no row, or no readable entrance, in settlements.xml")
        elif abs(x - bx) <= TOLERANCE and abs(y - by) <= TOLERANCE:
            out.append(f"{sid}: entrance {x}, {y} is the 2.0.34 unreachable value; move it in the map editor "
                       f"(docs/modding/settlements.md step 7) and rebuild the distance cache")
    return out


def main(argv=None):
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="the gate (the default and only mode)")
    ap.parse_args(argv)
    if not os.path.isfile(LIVE):
        print(f"ERROR: not found: {LIVE} (set BANNERLORD_GAME_DIR, see tools/_gamedir.py)", file=sys.stderr)
        return 2
    with open(LIVE, "rb") as f:
        text = f.read().decode("utf-8-sig", errors="replace")
    try:
        found = findings(text)
    except ET.ParseError as error:
        print(f"ERROR: {LIVE} does not parse: {error}", file=sys.stderr)
        return 2
    for line in found:
        print("FAIL " + line)
    if found:
        print(f"\n{len(found)} of {len(ISLAND_ENTRANCES)} entrances still unreachable (#1434).")
        return 1
    print(f"OK: none of the {len(ISLAND_ENTRANCES)} known unreachable entrances remains (#1434).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
