#!/usr/bin/env python3
"""Well-formedness gate for every TAOM XSLT stylesheet, including the live ones.

TAOM ships 16 stylesheets across three modules and, until #462, only the repo's 8
had any gate at all. CI's "Validate XML & XSLT" job globs
`Main/_Module/ModuleData/**/*.xslt`, and `/xslt-check` resolves its target under the
same path, so neither can reach `TAOM_Map`'s one or `LOTRLOME_Armory`'s seven. CI
*structurally* cannot: those modules are not in the checkout. This script is the
developer-side counterpart, which is the only place such a check can live.

Why it matters more than "a malformed file would be obvious": `taom_schema.py`
decides whether vanilla's settlements were stripped by regex-matching
`TAOM_Map/ModuleData/settlements.xslt` for an empty `<xsl:template match="Settlement"/>`.
The file is never parsed. If it were malformed, or the strip rewritten to an
equivalent the regex misses, the match returns nothing, vanilla's settlements count
as live, and every culture reports as landed. That is the exact false-clean the
LANDLESS_CULTURE check exists to prevent (#374).

Two levels of checking:
  * ALWAYS: XML well-formedness, via stdlib ElementTree. No dependency.
  * WHEN AVAILABLE: stylesheet compilation via lxml, which catches a file that
    parses as XML but is not a legal stylesheet. Skipped with a note if lxml is
    absent, never a hard failure.

Usage:
    python tools/check_external_xslt.py [--game-modules PATH] [--json]

Exit codes: 0 clean, 1 a stylesheet failed, 2 bad input (no game install).
"""
from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _gamedir import game_modules as resolve_game_modules  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
REPO_MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
DEFAULT_GAME_MODULES = resolve_game_modules(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")

#: Live, unversioned modules whose stylesheets no other tool reaches.
EXTERNAL_MODULES = ("TAOM_Map", "LOTRLOME_Armory")

XSLT_NS = "http://www.w3.org/1999/XSL/Transform"


def find_stylesheets(repo_moduledata: Path, game_modules) -> dict:
    """Map module label -> sorted list of stylesheet paths.

    The repo entry is always present, even when empty, so a caller can tell
    "no stylesheets found" apart from "that module was not looked at".
    """
    found = {"TAOM (repo)": sorted(Path(repo_moduledata).rglob("*.xslt"))}
    if game_modules:
        game_modules = Path(game_modules)
        for name in EXTERNAL_MODULES:
            root = game_modules / name / "ModuleData"
            found[name] = sorted(root.rglob("*.xslt")) if root.exists() else []
    return found


def check_stylesheet(path: Path) -> list:
    """Problems with one stylesheet. Empty list means clean."""
    problems = []
    try:
        raw = Path(path).read_bytes()
    except OSError as exc:
        return [f"unreadable: {exc}"]

    try:
        root = ET.fromstring(raw)
    except ET.ParseError as exc:
        return [f"not well-formed XML: {exc}"]

    # A stylesheet whose root is not xsl:stylesheet/xsl:transform will be ignored
    # by the engine's merge, which looks like "my transform silently did nothing".
    tag = root.tag
    if tag not in (f"{{{XSLT_NS}}}stylesheet", f"{{{XSLT_NS}}}transform"):
        problems.append(
            f"root element is {tag!r}, expected xsl:stylesheet or xsl:transform "
            f"in the {XSLT_NS} namespace")
    return problems


def compile_stylesheet(path: Path):
    """Compile with lxml when present. Returns (attempted, problem_or_None)."""
    try:
        from lxml import etree
    except ImportError:
        return False, None
    try:
        etree.XSLT(etree.parse(str(path)))
    except Exception as exc:  # lxml raises several distinct types
        return True, f"does not compile as a stylesheet: {exc}"
    return True, None


def run(repo_moduledata: Path, game_modules) -> dict:
    """Check every stylesheet. Returns a report dict (so --json is free)."""
    report = {"modules": {}, "problems": [], "compiled": False}
    for label, paths in find_stylesheets(repo_moduledata, game_modules).items():
        report["modules"][label] = [str(p) for p in paths]
        for p in paths:
            for problem in check_stylesheet(p):
                report["problems"].append({"file": str(p), "problem": problem})
            attempted, problem = compile_stylesheet(p)
            report["compiled"] = report["compiled"] or attempted
            if problem:
                report["problems"].append({"file": str(p), "problem": problem})
    return report


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game-modules", default=str(DEFAULT_GAME_MODULES),
                    help="Bannerlord Modules folder (holds TAOM_Map and LOTRLOME_Armory)")
    ap.add_argument("--moduledata", default=str(REPO_MODULEDATA))
    ap.add_argument("--json", dest="json_out", action="store_true")
    args = ap.parse_args()

    game_modules = Path(args.game_modules)
    if not game_modules.exists():
        print(f"ERROR: Bannerlord Modules folder not found: {game_modules}\n"
              f"       The repo's stylesheets can still be checked, but the 8 live ones\n"
              f"       cannot, and a partial run must not report clean.", file=sys.stderr)
        return 2

    report = run(Path(args.moduledata), game_modules)

    if args.json_out:
        print(json.dumps(report, indent=2))
    else:
        total = sum(len(v) for v in report["modules"].values())
        for label, paths in report["modules"].items():
            print(f"{label}: {len(paths)} stylesheet(s)")
            for p in paths:
                print(f"    {p}")
        if not report["compiled"]:
            print("\nNOTE: lxml not installed, so only well-formedness was checked.\n"
                  "      Install lxml for stylesheet compilation.", file=sys.stderr)
        if report["problems"]:
            print(f"\nFAIL: {len(report['problems'])} problem(s) across {total} stylesheet(s)")
            for pr in report["problems"]:
                print(f"  {pr['file']}\n      {pr['problem']}")
        else:
            print(f"\nPASS: {total} stylesheet(s) clean")

    return 1 if report["problems"] else 0


if __name__ == "__main__":
    sys.exit(main())
