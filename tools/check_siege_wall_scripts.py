#!/usr/bin/env python3
"""Does every siege wall of the map scenes carry what the base game's siege set-up reads?

`SiegeMissionPreparationHandler.ArrangeDestructedMeshes` takes every `WallSegment` whose side is set
(`SideTag` left, middle or right) and that has a direct child tagged `broken_child`, and calls
`WallSegment.OnChooseUsedWallSegment` on it. That method takes the first direct child tagged
`solid_child` and the first tagged `broken_child`, and calls `GetFirstScriptOfType<WallSegment>()` on
each with no null check (TaleWorlds.MountAndBlade, the same body in v1.5.6 and v1.5.7). A child with
the tag and no `WallSegment` script throws at the start of every siege mission on that scene. TAOM's
mission start guard catches the exception, so the mission goes on with the set-up half done. On
2026-11-10 that was Helm's Deep, where an art pass had replaced the right breach mesh without the
script, and the first wave of The Long Defeat ended in a stand-off
(docs/reviews/rca-helms-deep-siege-wall-2026-11-10.md).

The same loop takes the walls in pairs: it runs once per wall hit-point entry, and the campaign and
The Long Defeat pass two. A wall with no pair throws there too, so a scene with an odd number of such
walls is a fault. An even wrong number is not judged: base game scenes hold two walls for each of
three upgrade levels, and how a scene file's levels load was not verified.

The live TAOM_Map is unversioned, so this reads the install by default. Read-only. It judges only
what a scene file spells out: a wall placed as a bare prefab instance has its children in the prefab
file, which this tool does not open.

Usage:
    python tools/check_siege_wall_scripts.py
    python tools/check_siege_wall_scripts.py --scenes E:/LOTRAOM_Releases/<channel>/Modules/TAOM_Map/SceneObj
    python tools/check_siege_wall_scripts.py <a scene.xscene file> [...]

With files, only those files are judged (tools/data_gates.py passes the scene files you named). With
neither a file nor --scenes, the live TAOM_Map is.

Exit code: 0 every wall read is sound, 1 a fault (a sided wall with a breach child lacks its
solid_child or one of the two scripts, or a scene holds an odd number of such walls), 2 a folder or
file is missing, a folder holds no scene.xscene, or a scene file does not parse, so not everything
was judged, 3 the tool crashed (the traceback is on stderr). Never read 2 or 3 as a pass.
"""
from __future__ import annotations

import argparse
import sys
import traceback
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _gamedir import DEFAULT_GAME_ROOT, game_dir  # noqa: E402

DEFAULT_SCENES = Path(game_dir(DEFAULT_GAME_ROOT)) / "Modules" / "TAOM_Map" / "SceneObj"
SCENE_FILE = "scene.xscene"
SCRIPT = "WallSegment"
# WallSegment.OnInit maps exactly these to a DefenseSide; any other value leaves the side not set.
SIDES = frozenset({"left", "middle", "right"})


def _tags(entity) -> list:
    return [tag.get("name") for tag in entity.findall("./tags/tag")]


def _has_script(entity) -> bool:
    return any(script.get("name") == SCRIPT for script in entity.findall("./scripts/script"))


def _side(entity) -> str:
    for script in entity.findall("./scripts/script"):
        if script.get("name") == SCRIPT:
            for variable in script.findall("./variables/variable"):
                if variable.get("name") == "SideTag":
                    return variable.get("value") or ""
    return ""


def _label(entity) -> str:
    return entity.get("name") or entity.get("prefab") or "(no name)"


def scene_faults(root) -> tuple:
    """(number of sided walls with a breach child, list of fault texts) for one parsed scene."""
    walls, faults = 0, []
    for entity in root.iter("game_entity"):
        side = _side(entity) if _has_script(entity) else ""
        if side not in SIDES:
            continue
        children = entity.findall("./children/game_entity")
        broken = next((c for c in children if "broken_child" in _tags(c)), None)
        if broken is None:
            continue
        walls += 1
        where = f"wall '{_label(entity)}' (side {side})"
        solid = next((c for c in children if "solid_child" in _tags(c)), None)
        if solid is None:
            faults.append(f"{where}: no child tagged solid_child")
        elif not _has_script(solid):
            faults.append(f"{where}: solid_child '{_label(solid)}' has no {SCRIPT} script")
        if not _has_script(broken):
            faults.append(f"{where}: broken_child '{_label(broken)}' has no {SCRIPT} script")
    if walls % 2:
        faults.append(f"{walls} sided wall(s) with a breach, an odd number: the set-up takes them in pairs, "
                      "so one wall has lost its pair (a dropped broken_child tag or SideTag)")
    return walls, faults


def _scene_files(args) -> tuple:
    """(scene files to judge, number of folders or files that cannot be judged)."""
    scenes, unjudged = [], 0
    for path in args.files:
        if path.name.lower() != SCENE_FILE:
            print(f"ERROR: not a {SCENE_FILE}: {path}", file=sys.stderr)
            unjudged += 1
        elif not path.is_file():
            print(f"ERROR: scene file not found: {path}", file=sys.stderr)
            unjudged += 1
        else:
            scenes.append(path)
    for folder in args.scenes or ([] if args.files else [DEFAULT_SCENES]):
        if not folder.is_dir():
            print(f"ERROR: scene folder not found: {folder}", file=sys.stderr)
            unjudged += 1
            continue
        found = sorted(folder.glob(f"*/{SCENE_FILE}"))
        if not found:
            print(f"ERROR: no {SCENE_FILE} under {folder}", file=sys.stderr)
            unjudged += 1
        scenes += found
    return scenes, unjudged


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*", type=Path, help=f"{SCENE_FILE} files to judge, in place of the default folder")
    ap.add_argument("--scenes", type=Path, action="append",
                    help=f"A SceneObj folder; repeat for more (default with no file: {DEFAULT_SCENES})")
    args = ap.parse_args(argv)

    scenes, unjudged = _scene_files(args)
    read = walls = faults = 0
    for path in scenes:
        try:
            root = ET.parse(path).getroot()
        except (ET.ParseError, OSError) as exc:
            print(f"ERROR: cannot read {path.parent.name}: {exc}", file=sys.stderr)
            unjudged += 1
            continue
        read += 1
        scene_walls, scene_fault_list = scene_faults(root)
        walls += scene_walls
        faults += len(scene_fault_list)
        for fault in scene_fault_list:
            print(f"{path.parent.name}: {fault}")

    counts = f"{read} scene(s) read, {walls} sided wall(s) with a breach, {faults} fault(s)"
    if unjudged:
        print(f"No verdict: {unjudged} folder(s) or scene file(s) could not be read; {counts}.", file=sys.stderr)
        return 2
    if faults:
        print(f"FAIL: {counts}. In the Modding Kit, give each named child a {SCRIPT} script (copy its twin's "
              "block), and give a wall with no pair its broken_child tag and SideTag back.")
        return 1
    print(f"PASS: {counts}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except SystemExit:
        raise
    except Exception:  # noqa: BLE001 - a crash must not read as a verdict
        traceback.print_exc()
        sys.exit(3)
