#!/usr/bin/env python3
"""Distribute the real generic Gondor scenes across Gondor settlements.

TAOM's Gondor settlements point their center / village_center Location scenes at a
mix of vanilla `empire_*` scenes and (now-broken) LOTR-named placeholder refs. Real
generic Gondor scenes now exist in `TAOM_Map/SceneObj`:

  - 3 castle scenes : taom_gondor_castle_001/002/003_forceatmo
  - 4 village scenes: taom_gondor_village_001/002/003/004_forceatmo
  - 4 named towns   : minas_tirith / lossarnach / osgiliath_e / osgiliath_w

This script rewrites ONLY the relevant `<Location>` `scene_name*` attribute VALUES in
the live settlements.xml (TAOM_Map external module), preserving all other formatting
byte-for-byte. It is a line-oriented state machine, NOT an XML re-serializer.

Rules (confirmed with project owner):
  * Castles (is_castle="true")           -> center      : round-robin over 3 castle scenes
  * Villages (independent + castle-bound) -> village_center : round-robin over 4 village scenes
  * Named towns (town_EW1/2/3/7)         -> center      : fixed mapping
  * Every other Location (lordshall/prison/arena/tavern/house_*/alley), the 7 non-named
    Gondor towns, and all non-Gondor settlements are left untouched.

Default is a dry run (prints a per-settlement table + distribution counts). Pass --apply
to write the file (a .bak is written first).

Usage:
  python tools/assign_gondor_scenes.py [--dry-run] [--apply] [--settlements-path PATH]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

DEFAULT_SETTLEMENTS = Path(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\TAOM_Map\ModuleData\settlements.xml"
)

GONDOR_CULTURE = 'culture="Culture.gondor"'

CASTLE_SCENES = [
    "taom_gondor_castle_001_forceatmo",
    "taom_gondor_castle_002_forceatmo",
    "taom_gondor_castle_003_forceatmo",
]
VILLAGE_SCENES = [
    "taom_gondor_village_001_forceatmo",
    "taom_gondor_village_002_forceatmo",
    "taom_gondor_village_003_forceatmo",
    "taom_gondor_village_004_forceatmo",
]
NAMED_TOWN_SCENES = {
    "town_EW1": "taom_gondor_town_minas_tirith_forceatmo",
    "town_EW2": "taom_gondor_town_osgiliath_w_forceatmo",
    "town_EW3": "taom_gondor_town_osgiliath_e_forceatmo",
    "town_EW7": "taom_gondor_town_lossarnach_forceatmo",
}

# matchers
RE_SETTLEMENT = re.compile(r'<Settlement\s+id="([^"]+)"')
RE_TOWN = re.compile(r'<Town\b[^>]*\bis_castle="(true|false)"')
RE_VILLAGE = re.compile(r"<Village\b")
RE_LOC_CENTER = re.compile(r'<Location\s+id="center"')
RE_LOC_VILLAGE_CENTER = re.compile(r'<Location\s+id="village_center"')
RE_SCENE_ATTR = re.compile(r'(scene_name(?:_\d)?=")[^"]*(")')
RE_FIRST_SCENE = re.compile(r'scene_name(?:_\d)?="([^"]*)"')


def first_scene(line: str) -> str:
    m = RE_FIRST_SCENE.search(line)
    return m.group(1) if m else "(none)"


def set_all_scenes(line: str, scene: str) -> str:
    return RE_SCENE_ATTR.sub(r"\g<1>" + scene + r"\g<2>", line)


def process(lines: list[str]):
    """Return (new_lines, changes). changes = list of (id, type, old, new)."""
    new_lines: list[str] = []
    changes: list[tuple[str, str, str, str]] = []

    cur_id = None
    is_gondor = False
    cur_type = None  # "castle" | "town" | "village" | None
    village_idx = 0
    castle_idx = 0

    for line in lines:
        m_set = RE_SETTLEMENT.search(line)
        if m_set:
            cur_id = m_set.group(1)
            is_gondor = GONDOR_CULTURE in line
            cur_type = None
            new_lines.append(line)
            continue

        if is_gondor and cur_type is None:
            m_town = RE_TOWN.search(line)
            if m_town:
                cur_type = "castle" if m_town.group(1) == "true" else "town"
                new_lines.append(line)
                continue
            if RE_VILLAGE.search(line):
                cur_type = "village"
                new_lines.append(line)
                continue

        if is_gondor:
            # Castle / named-town center line
            if cur_type == "castle" and RE_LOC_CENTER.search(line):
                scene = CASTLE_SCENES[castle_idx % len(CASTLE_SCENES)]
                castle_idx += 1
                old = first_scene(line)
                newline = set_all_scenes(line, scene)
                if newline != line:
                    changes.append((cur_id, "castle", old, scene))
                new_lines.append(newline)
                continue

            if cur_type == "town" and RE_LOC_CENTER.search(line):
                scene = NAMED_TOWN_SCENES.get(cur_id)
                if scene:  # only the 4 named towns; others untouched
                    old = first_scene(line)
                    newline = set_all_scenes(line, scene)
                    if newline != line:
                        changes.append((cur_id, "town", old, scene))
                    new_lines.append(newline)
                    continue
                new_lines.append(line)
                continue

            if cur_type == "village" and RE_LOC_VILLAGE_CENTER.search(line):
                scene = VILLAGE_SCENES[village_idx % len(VILLAGE_SCENES)]
                village_idx += 1
                old = first_scene(line)
                newline = set_all_scenes(line, scene)
                if newline != line:
                    changes.append((cur_id, "village", old, scene))
                new_lines.append(newline)
                continue

        new_lines.append(line)

    return new_lines, changes


def report(changes):
    print(f"{'SETTLEMENT':<22} {'TYPE':<8} {'OLD SCENE':<36} -> NEW SCENE")
    print("-" * 110)
    for sid, typ, old, new in changes:
        print(f"{sid:<22} {typ:<8} {old:<36} -> {new}")

    print("\nDistribution counts:")
    for label, scenes in (("castle", CASTLE_SCENES), ("village", VILLAGE_SCENES)):
        counts = {s: 0 for s in scenes}
        for _, typ, _, new in changes:
            if typ == label:
                counts[new] = counts.get(new, 0) + 1
        total = sum(counts.values())
        print(f"  {label}s ({total}):")
        for s, c in counts.items():
            print(f"    {s:<36} {c}")

    town_changes = [c for c in changes if c[1] == "town"]
    print(f"  towns ({len(town_changes)}):")
    for sid, _, old, new in town_changes:
        print(f"    {sid:<10} {new}")

    by_type = {}
    for _, typ, _, _ in changes:
        by_type[typ] = by_type.get(typ, 0) + 1
    print(f"\nTotal lines changed: {len(changes)}  ({by_type})")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the file (default: dry run)")
    ap.add_argument("--dry-run", action="store_true", help="preview only (default)")
    ap.add_argument("--settlements-path", type=Path, default=DEFAULT_SETTLEMENTS)
    args = ap.parse_args()

    path: Path = args.settlements_path
    if not path.is_file():
        print(f"ERROR: settlements file not found: {path}", file=sys.stderr)
        return 1

    text = path.read_text(encoding="utf-8")
    # keepends so we round-trip line endings exactly
    lines = text.splitlines(keepends=True)
    new_lines, changes = process(lines)

    report(changes)

    if not changes:
        print("\nNothing to change.")
        return 0

    if args.apply:
        bak = path.with_suffix(path.suffix + ".bak")
        bak.write_text(text, encoding="utf-8")
        path.write_text("".join(new_lines), encoding="utf-8")
        print(f"\nAPPLIED. Backup written to: {bak}")
    else:
        print("\nDRY RUN — no files written. Re-run with --apply to write.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
