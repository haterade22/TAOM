#!/usr/bin/env python3
"""Check that the TAOM_Map and LOTRLOME_Armory a release ships pair with this TAOM, and pin them.

Both modules are unversioned installs outside git, so nothing else records which copies a release
shipped or says they were built for this line. Four checks read the package being released:

  MODULE_VERSION  a hand-typed fourth part or a 1.5.x number in either <Version> (decision D8)
  VILLAGE         a tools/add_map_villages.py table id the map lacks (recruitment pools name them)
  HIDEOUT         a bandit culture with no den: a new game throws on it
  PIN_MISSING / PIN_MISMATCH  the ModuleData fingerprints differ from tools/release_pins.json

Monster attributes Monsters.xsd does not declare are printed as a PAIRING SIGNAL on every run and
stored in the pin. Decision D15: a signal means a 1.5.x-era Armory; it is never suppressed, never
allowlisted and never changes the exit code.

Usage:
  python tools/check_release_pairing.py --modules "<Modules root>" [--game-root "<game>"]
      [--pin tools/release_pins.json] [--record PATH]

--game-root defaults to the parent of --modules. Read-only except --record, which refuses while any
non-pin finding exists and otherwise writes the pin to PATH.

Exit codes: 0 no finding; 1 findings; 2 bad input (missing module folder, settlements.xml or
Monsters.xsd).
"""
import argparse
import datetime
import hashlib
import json
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import add_map_villages as amv  # noqa: E402
import package_release as pr  # noqa: E402

TAOM_MAP = "TAOM_Map"
ARMORY = "LOTRLOME_Armory"
PINNED_MODULES = (TAOM_MAP, ARMORY)
REPO_CLANS = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "characters" / "clans.xml"
DEFAULT_PIN = Path(__file__).resolve().parent / "release_pins.json"
XS = "{http://www.w3.org/2001/XMLSchema}"


def _read_version(module_root: Path):
    """<Version value> from the module's SubModule.xml, or None."""
    try:
        root = ET.parse(module_root / "SubModule.xml").getroot()
    except (OSError, ET.ParseError):
        return None
    node = root.find("Version")
    return node.get("value") if node is not None else None


def module_identity(module_root: Path) -> dict:
    """{version, files, fingerprint}. The fingerprint hashes every ModuleData file the packager would
    copy, so backups do not count; SubModule.xml stays outside it, so setting the release number
    does not move the pin."""
    data = module_root / "ModuleData"
    lines = []
    for path in sorted(p for p in data.rglob("*") if p.is_file()):
        rel = path.relative_to(data).as_posix()
        if pr.classify("ModuleData/" + rel).action != pr.COPY:
            continue
        lines.append(f"{rel}\0{hashlib.sha256(path.read_bytes()).hexdigest()}\n")
    lines.sort()
    digest = hashlib.sha256("".join(lines).encode("utf-8")).hexdigest()
    return {"version": _read_version(module_root), "files": len(lines), "fingerprint": f"sha256:{digest}"}


def version_findings(identities: dict) -> list:
    found = []
    for name, identity in identities.items():
        problem = pr.release_number_problem(identity["version"])
        if problem:
            found.append(f"MODULE_VERSION: {name}: {problem}")
    return found


def undeclared_monster_attributes(armory_root: Path, xsd: Path) -> dict:
    """{attribute: sorted ModuleData-relative files} for every Monster attribute, in an Armory XML
    whose root is Monsters, that the schema's Monster element does not declare."""
    declared = set()
    for element in ET.parse(xsd).getroot().iter(f"{XS}element"):
        if element.get("name") == "Monster":
            declared.update(a.get("name") for a in element.iter(f"{XS}attribute"))
    signals: dict = {}
    for path in sorted((armory_root / "ModuleData").rglob("*.xml")):
        if pr.classify(path.relative_to(armory_root).as_posix()).action != pr.COPY:
            continue
        try:
            root = ET.parse(path).getroot()
        except (OSError, ET.ParseError):
            print(f"note: {path.relative_to(armory_root).as_posix()} does not parse; "
                  "its Monster attributes are not checked")
            continue
        if root.tag != "Monsters":
            continue
        rel = path.relative_to(armory_root).as_posix()
        for monster in root.iter("Monster"):
            for attribute in monster.attrib:
                if attribute not in declared:
                    signals.setdefault(attribute, set()).add(rel)
    return {attribute: sorted(files) for attribute, files in sorted(signals.items())}


def _read_text(path: Path) -> str:
    return path.read_bytes().decode("utf-8-sig", errors="replace")


def village_findings(map_root: Path) -> tuple:
    """(findings, note) for the add_map_villages table against this map. The note says the scene
    file was absent, in which case the per-entity findings are dropped."""
    master = _read_text(map_root / "ModuleData" / "settlements.xml")
    scene_path = map_root / "SceneObj" / "Main_map" / "scene.xscene"
    scene = _read_text(scene_path) if scene_path.is_file() else None
    locs = {}
    found = []
    for lang in amv.LANGS:
        path = map_root / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
        if path.is_file():
            locs[lang] = _read_text(path)
        else:
            found.append(f"VILLAGE: no ModuleData/Languages/{lang}/loc_settlements.xml")
    checked = amv.check(amv.VILLAGES, master, scene or "", locs)
    note = None
    if scene is None:
        checked = [f for f in checked if "no entity in scene.xscene" not in f]
        note = "note: no SceneObj/Main_map/scene.xscene in the package; scene entities not checked"
    return found + [f"VILLAGE: {f}" for f in checked], note


def hideout_findings(clans_xml: Path, settlements_xml: Path) -> list:
    """Every is_bandit Faction needs a Settlement of its culture with a Hideout component."""
    dens = {s.get("culture") for s in ET.parse(settlements_xml).getroot().iter("Settlement")
            if s.find(".//Hideout") is not None}
    return [f"HIDEOUT: {f.get('id')}: no Settlement of {f.get('culture')} has a Hideout component"
            for f in ET.parse(clans_xml).getroot().iter("Faction")
            if f.get("is_bandit") == "true" and f.get("culture") not in dens]


def pin_findings(identities: dict, pin: Path) -> list:
    try:
        pinned = json.loads(pin.read_text(encoding="utf-8"))["modules"]
    except (OSError, ValueError, KeyError, TypeError) as e:
        return [f"PIN_MISSING: cannot read {pin} ({type(e).__name__})"]
    found = []
    for name, identity in identities.items():
        expected = pinned.get(name, {}).get("fingerprint")
        if expected != identity["fingerprint"]:
            found.append(f"PIN_MISMATCH: {name}: {identity['fingerprint'][:19]} differs from the pinned "
                         f"{str(expected)[:19]}")
    return found


def main(argv=None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # village names carry accents
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--modules", required=True, help="Modules root of the package being released")
    ap.add_argument("--game-root", default=None, help="game folder holding XmlSchemas (default: the parent of --modules)")
    ap.add_argument("--pin", default=str(DEFAULT_PIN), help="pin file (default: tools/release_pins.json)")
    ap.add_argument("--record", metavar="PATH", default=None, help="write a new pin to PATH when no non-pin finding exists")
    args = ap.parse_args(argv)

    modules = Path(args.modules)
    game_root = Path(args.game_root) if args.game_root else modules.parent
    xsd = game_root / "XmlSchemas" / "Monsters.xsd"
    settlements = modules / TAOM_MAP / "ModuleData" / "settlements.xml"
    required = [modules / name for name in PINNED_MODULES] + [settlements, xsd]
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        for path in missing:
            print(f"ERROR: missing {path}", file=sys.stderr)
        return 2
    for path in (settlements, xsd):
        try:
            ET.parse(path)
        except ET.ParseError as e:
            print(f"ERROR: {path} does not parse ({e})", file=sys.stderr)
            return 2

    identities = {name: module_identity(modules / name) for name in PINNED_MODULES}
    print("modules: " + ", ".join(
        f"{name} {i['version']} {i['fingerprint'][:19]} ({i['files']} files)" for name, i in identities.items()))

    signals = undeclared_monster_attributes(modules / ARMORY, xsd)
    for attribute, files in signals.items():
        print(f"PAIRING SIGNAL (decision D15, never an error): {ARMORY} Monster attribute {attribute} is "
              f"not in Monsters.xsd, so this Armory is a 1.5.x-era build: {', '.join(files[:6])}"
              f"{f' (+{len(files) - 6} more)' if len(files) > 6 else ''}")

    villages, note = village_findings(modules / TAOM_MAP)
    if note:
        print(note)
    findings = version_findings(identities) + villages + hideout_findings(REPO_CLANS, settlements)

    if args.record:
        for finding in findings:
            print(finding)
        if findings:
            print(f"ERROR: not recording a pin with {len(findings)} findings open", file=sys.stderr)
            return 1
        Path(args.record).write_text(json.dumps({
            "recorded_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
            "modules": identities,
            "pairing_signals": signals,
        }, indent=2) + "\n", encoding="utf-8")
        print(f"pin recorded: {args.record}")
        return 0

    findings += pin_findings(identities, Path(args.pin))
    for finding in findings:
        print(finding)
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
