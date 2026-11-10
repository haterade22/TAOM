#!/usr/bin/env python
"""Writes the Hero Defence troop list, ModuleData/hero_defence/rosters.json, from the culture troop trees.

Usage:
  python tools/generate_hero_defence_rosters.py --out <path>    write the list
  python tools/generate_hero_defence_rosters.py --check         exit 1 if the shipped list has drifted

The wave generator and the wave preview read the list with no game running, so it ships as data.
ShippedHeroDefenceDataTests applies the same filters to the XML and fails on any drift.

Exit codes: 0 the list was written or is current; 1 the shipped list has drifted or is missing (--check);
2 bad arguments; 3 no troop passed the filters, or a troop file could not be read or names an unknown
default_group. A run that fails writes nothing.

A troop is kept when all of these hold, in this order:
  - its file is not troops_feral_orcs.xml (a roaming band, not a culture);
  - its culture is the main culture of its file (this drops *_raiders, *_soldiers, *_corsairs and the like);
  - its occupation is Soldier (this drops bosses and mercenaries);
  - its id is not settlement militia (real line units such as rohan_westfold_militiaman stay);
  - it has an upgrade edge in or out (this drops trolls, howdah crew and other lone rows).

Pure stdlib.
"""
from __future__ import annotations

import argparse
import collections
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
TROOPS = REPO / "Main" / "_Module" / "ModuleData" / "troops"
SHIPPED = REPO / "Main" / "_Module" / "ModuleData" / "hero_defence" / "rosters.json"

SKIPPED_FILES = {"troops_feral_orcs.xml"}
CULTURE_MERGES = {"shaghana": "aserai"}
# Settlement militia ids end in _militia_spearman, _militia_archer or their _veteran_ forms.
# Line units with "militia" elsewhere in the id, such as rohan_westfold_militiaman, do not match.
MILITIA = re.compile(r"_militia_(veteran_)?(spearman|archer)$")
CLASSES = {"Infantry", "Ranged", "Cavalry", "HorseArcher"}


def _read_file(path: Path) -> list[dict]:
    rows = []
    for npc in ET.parse(path).getroot().findall("NPCCharacter"):
        upgrades = npc.find("upgrade_targets")
        targets = [] if upgrades is None else [t.get("id", "").replace("NPCCharacter.", "") for t in upgrades.findall("upgrade_target")]
        culture = (npc.get("culture") or "").replace("Culture.", "")
        rows.append({
            "id": npc.get("id", ""),
            "level": int(npc.get("level", "0")),
            "class": npc.get("default_group", ""),
            "occupation": npc.get("occupation", ""),
            "culture": CULTURE_MERGES.get(culture, culture),
            "targets": targets,
        })
    return rows


def _main_culture(rows: list[dict]) -> str:
    counts = collections.Counter(row["culture"] for row in rows)
    return sorted(counts, key=lambda culture: (-counts[culture], culture))[0]


def build(folder: Path) -> dict[str, list[dict]]:
    """The kept troops per culture, sorted by level and then id."""
    files = [_read_file(path) for path in sorted(folder.glob("troops_*.xml")) if path.name not in SKIPPED_FILES]
    upgraded_into = {target for rows in files for row in rows for target in row["targets"]}
    rosters: dict[str, list[dict]] = collections.defaultdict(list)
    for rows in files:
        if not rows:
            continue
        own = _main_culture(rows)
        for row in rows:
            linked = row["targets"] or row["id"] in upgraded_into
            if row["culture"] != own or row["occupation"] != "Soldier" or MILITIA.search(row["id"]) or not linked:
                continue
            if row["class"] not in CLASSES:
                raise ValueError(f"{row['id']}: unknown default_group {row['class']!r}")
            rosters[own].append({"id": row["id"], "level": row["level"], "class": row["class"]})
    return {culture: sorted(rows, key=lambda row: (row["level"], row["id"])) for culture, rows in rosters.items()}


def render(rosters: dict[str, list[dict]]) -> str:
    """One row per line, so a drift shows as a small diff."""
    lines = ["{"]
    cultures = sorted(rosters)
    for index, culture in enumerate(cultures):
        lines.append(f"  {json.dumps(culture)}: [")
        rows = rosters[culture]
        for row_index, row in enumerate(rows):
            comma = "," if row_index < len(rows) - 1 else ""
            lines.append(f'    {{"id": {json.dumps(row["id"])}, "level": {row["level"]}, "class": {json.dumps(row["class"])}}}{comma}')
        lines.append("  ]" + ("," if index < len(cultures) - 1 else ""))
    lines.append("}")
    return "\n".join(lines) + "\n"


def is_current(expected: str, actual: str) -> bool:
    return expected.replace("\r\n", "\n") == actual.replace("\r\n", "\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--out", type=Path, help="write the list to this path")
    mode.add_argument("--check", action="store_true", help="compare with the shipped list")
    args = parser.parse_args(argv)

    try:
        rosters = build(TROOPS)
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"ERROR: could not build the list from {TROOPS}: {error}; nothing written", file=sys.stderr)
        return 3
    if not rosters:
        print(f"ERROR: no troop in {TROOPS} passed the filters; nothing written", file=sys.stderr)
        return 3

    text = render(rosters)
    total = sum(len(rows) for rows in rosters.values())
    if args.check:
        if not SHIPPED.exists() or not is_current(text, SHIPPED.read_text(encoding="utf-8-sig")):
            print(f"DRIFT: {SHIPPED} does not match the troop trees; regenerate it with --out", file=sys.stderr)
            return 1
        print(f"current: {total} troops, {len(rosters)} cultures")
        return 0

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    print(f"wrote {args.out}: {total} troops, {len(rosters)} cultures")
    return 0


if __name__ == "__main__":
    sys.exit(main())
