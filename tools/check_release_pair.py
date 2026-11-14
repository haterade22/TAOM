#!/usr/bin/env python3
"""Do a release channel's TAOM and TAOM_Map agree on cultures, clans and settlements?

The two modules ship as separate packages and name each other's rows. A map ahead of its TAOM
points settlements at cultures and clans that TAOM does not define. A TAOM ahead of its map names
home settlements the map lacks and can leave a culture with no settlement, the daily clan tick
crash LANDLESS_CULTURE reports (#374). This one check replaces the hand-written grep lines per
realm in docs/reference/release-process.md (rca-ered-luin-2026-10-27.md row 2).

It also checks the map's prison scenes (PRISON_SCENE_NOT_STEALTH). TAOM_Map is in no repo, so a
repair to one install's settlements.xml reaches players only through a package that carries it:
Orthanc and Orthanc Gate shipped the pre-stealth empire_dungeon_a (2026-11-13), and this is where a
package that still holds it is stopped. Scenes are looked up in the channel and in the install's
official modules (Native, SandBoxCore, SandBox, StoryMode, CustomBattle), which hold the vanilla
dungeons. Another module in the install is not in the package, so its scenes do not count. Which copy
the game loads when a scene name is in both is not verified.

A channel holds TAOM's modules and no vanilla module, so vanilla's clans come from the install.
Only the pair's own codes are reported: with no vanilla items in the channel, the validator's
other codes would read as a flood of false findings. Read-only.

Usage: python tools/check_release_pair.py E:/LOTRAOM_Releases/<channel>/Modules

Exit code: 0 the pair agrees, 1 a disagreement (a prison scene no module ships is one, although the
validator only warns), 2 a folder or file it needs is missing or does not parse, or the map has no
settlement or prison row, so nothing was judged, 3 the tool crashed (the traceback is on stderr), so
nothing was judged either. Never read 2 or 3 as a pass.
"""
from __future__ import annotations

import argparse
import sys
import traceback
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import taom_schema as ts  # noqa: E402
from validate_moduledata import (  # noqa: E402
    DEFAULT_GAME_MODULES, PRISON_CODE, _scene_files, build_extra_ref_roots, prison_scene_issues)

PAIR_CODES = frozenset({"UNKNOWN_CULTURE", "BROKEN_FACTION_REF", "BROKEN_SETTLEMENT_REF", "LANDLESS_CULTURE"})
OFFICIAL_MODULES = frozenset({"Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle"})


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("modules", type=Path, help="The channel's Modules folder")
    ap.add_argument("--vanilla-modules", type=Path, default=DEFAULT_GAME_MODULES,
                    help="A Modules folder that holds SandBox, for vanilla's clans and dungeon scenes "
                         "(default: the install)")
    args = ap.parse_args(argv)

    moduledata = args.modules / "TAOM" / "ModuleData"
    settlements_xml = args.modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
    # settlements.xslt strips vanilla's settlement rows: without it they return in game.
    needed = (moduledata / "taom_spcultures.xml", moduledata / "characters" / "clans.xml", settlements_xml,
              settlements_xml.with_suffix(".xslt"), args.vanilla_modules / "SandBox" / "ModuleData" / "spclans.xml")
    # The registries read these files with regexes, which take a half-written file for a short one.
    unreadable = []
    for path in needed:
        try:
            ET.parse(path)
        except (ET.ParseError, OSError) as exc:
            unreadable.append((path, exc))
    if unreadable:
        for path, exc in unreadable:
            print(f"ERROR: cannot read: {path}: {exc}", file=sys.stderr)
        print("No verdict: the pair was not checked.", file=sys.stderr)
        return 2

    registries = ts.build_registries(moduledata, args.modules, vanilla_modules=args.vanilla_modules)
    # An empty registry switches its pass off, which reads as a clean run.
    if not registries.settlements or not registries.settled_cultures:
        print(f"ERROR: no settlement row in the world this channel builds: {settlements_xml}", file=sys.stderr)
        print("No verdict: the pair was not checked.", file=sys.stderr)
        return 2

    # prison_scene_issues reports a map with no prison rows as a finding; here it is no verdict, like
    # every other check that judged nothing.
    if not any(loc.get("id") == "prison" for loc in ET.parse(settlements_xml).iter("Location")):
        print(f"ERROR: no prison Location row in {settlements_xml}", file=sys.stderr)
        print("No verdict: the prison scenes were not checked.", file=sys.stderr)
        return 2

    validator = ts.Validator(moduledata, [], registries, extra_ref_roots=build_extra_ref_roots(args.modules))
    issues = [i for i in validator._ref_sweep() + validator._broken_settlement_xslt_refs()
              + validator._landless_cultures() if i.code in PAIR_CODES]
    # The install stands in for vanilla only: a mod or a TAOM module in the install is not in the package.
    # This merge lets the channel's copy win; whether the game does the same is not verified, and no test pins it.
    scenes = {**_scene_files(args.vanilla_modules, OFFICIAL_MODULES), **_scene_files(args.modules)}
    prison_issues = prison_scene_issues(args.modules, scene_files=lambda _modules: scenes)
    issues += prison_issues
    issues.sort(key=lambda i: i.sort_key())
    if issues:
        print(ts.format_report(issues))
        if any(i.code == PRISON_CODE for i in prison_issues):
            print("The channel is Mike's package: repair the live TAOM_Map as each finding says (a name "
                  "in SCENE_REMAP: python tools/remap_stale_scene_names.py --dry-run, then --apply), "
                  "then repackage TAOM_Map; never edit the channel.")
        return 1
    print(f"PASS: TAOM and TAOM_Map agree: {len(registries.settlements)} settlements, "
          f"{len(registries.cultures)} cultures, {len(registries.factions)} clans; every prison scene "
          f"has the prison break spawn points.")
    return 0


def entry() -> int:
    """main() with a crash turned into exit 3. Python exits 1 for an uncaught exception, the code that
    means "a disagreement", so a folder named folder.xml under TAOM/ModuleData read as a verdict on the
    pair. argparse's SystemExit passes through."""
    try:
        return main()
    except Exception:  # noqa: BLE001 - any crash is reported, never mistaken for a finding
        traceback.print_exc()
        print("ERROR: tools/check_release_pair.py crashed (traceback above); no verdict was reached, "
              "exit 3.", file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(entry())
