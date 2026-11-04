#!/usr/bin/env python3
"""Do a release channel's TAOM and TAOM_Map agree on cultures, clans and settlements?

The two modules ship as separate packages and name each other's rows. A map ahead of its TAOM
points settlements at cultures and clans that TAOM does not define. A TAOM ahead of its map names
home settlements the map lacks and can leave a culture with no settlement, the daily clan tick
crash LANDLESS_CULTURE reports (#374). This one check replaces the hand-written grep lines per
realm in docs/reference/release-process.md (rca-ered-luin-2026-10-27.md row 2).

A channel holds TAOM's modules and no vanilla module, so vanilla's clans come from the install.
Only the pair's own codes are reported: with no vanilla items in the channel, the validator's
other codes would read as a flood of false findings. Read-only.

Usage: python tools/check_release_pair.py E:/LOTRAOM_Releases/<channel>/Modules

Exit code: 0 the pair agrees, 1 a disagreement, 2 a folder or file it needs is missing or does not
parse, so nothing was judged, 3 the tool crashed (the traceback is on stderr), so nothing was judged
either. Never read 2 or 3 as a pass.
"""
from __future__ import annotations

import argparse
import sys
import traceback
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import taom_schema as ts  # noqa: E402
from validate_moduledata import DEFAULT_GAME_MODULES, build_extra_ref_roots  # noqa: E402

PAIR_CODES = frozenset({"UNKNOWN_CULTURE", "BROKEN_FACTION_REF", "BROKEN_SETTLEMENT_REF", "LANDLESS_CULTURE"})


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("modules", type=Path, help="The channel's Modules folder")
    ap.add_argument("--vanilla-modules", type=Path, default=DEFAULT_GAME_MODULES,
                    help="A Modules folder that holds SandBox, for vanilla's clans (default: the install)")
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

    validator = ts.Validator(moduledata, [], registries, extra_ref_roots=build_extra_ref_roots(args.modules))
    issues = [i for i in validator._ref_sweep() + validator._broken_settlement_xslt_refs()
              + validator._landless_cultures() if i.code in PAIR_CODES]
    issues.sort(key=lambda i: i.sort_key())
    if issues:
        print(ts.format_report(issues))
        return 1
    print(f"PASS: TAOM and TAOM_Map agree: {len(registries.settlements)} settlements, "
          f"{len(registries.cultures)} cultures, {len(registries.factions)} clans.")
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
