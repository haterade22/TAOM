#!/usr/bin/env python3
"""Audit `covers_legs` on every leg armour item that TAOM's own modules define.

WHY THIS EXISTS

`covers_legs="true"` clears `SkinMask.LegsVisible` for the whole outfit (`ArmorComponent.Deserialize`, v1.5.6), so a
greave or boot that draws the shin needs it, and a shoe that leaves the shin bare must not have it
(docs/modding/items-armor.md rule 5). Mike's rule of 2026-11-06: every leg piece is true, except the Uruk-hai shoes.

Two defects got through with no gate noticing. The Lorien armour generator wrote `covers_legs="false"` on all eight of
its leg pieces. A one-off script then flipped every false in the Isengard starter file, which included three boots that
draw the Uruk-hai shoe mesh, because it selected items by id and the decision was about a mesh.

The checks, over LOTRLOME_Armory, TAOM_Map and this repo's ModuleData (vanilla modules are not scanned):

1. every LegArmor item names `covers_legs` explicitly (the engine default is false);
2. `covers_legs="false"` appears only on a mesh listed in BARE_SHIN_MESHES;
3. every item that draws a BARE_SHIN_MESHES mesh is false (the shoes show the shin, whatever the item is called);
4. items that draw one mesh agree on `covers_legs`.

Usage:
  python tools/audit_leg_cover_flags.py                  # report, then exit
  python tools/audit_leg_cover_flags.py --modules <dir>  # a different Modules folder

Exit: 1 on any violation or any leg file that does not parse, 2 when no leg item was found (an empty scan is not a
pass, and a wrong root is the caller's to fix), else 0.
"""
from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _gamedir import DEFAULT_GAME_ROOT, game_modules  # noqa: E402

REPO = Path(__file__).resolve().parent.parent

# mesh -> why its items are false. Add a mesh here only on the maintainer's word.
BARE_SHIN_MESHES = {
    "sk_uruk_hai_shoes_a1": "the Uruk-hai shoes leave the shin bare (Mike, 2026-11-06)",
}

SCAN_ROOTS = (
    ("LOTRLOME_Armory", "ModuleData"),
    ("TAOM_Map", "ModuleData"),
)


@dataclass(frozen=True)
class Leg:
    item_id: str
    mesh: str
    covers: str | None   # "true", "false" or None when the attribute is absent
    source: str


def read_legs(path: Path, label: str) -> tuple[list[Leg], str | None]:
    """The LegArmor items of one XML file, and an error text when the file does not parse."""
    raw = path.read_bytes()
    if b'Type="LegArmor"' not in raw:
        return [], None
    try:
        root = ET.fromstring(raw.lstrip(b"\xef\xbb\xbf"))
    except ET.ParseError as exc:
        return [], f"{label}: does not parse ({exc})"
    legs = []
    for item in root.iter("Item"):
        if item.get("Type") != "LegArmor":
            continue
        armor = item.find(".//Armor")
        covers = armor.get("covers_legs") if armor is not None else None
        legs.append(Leg(item.get("id", ""), item.get("mesh", ""), covers, label))
    return legs, None


def audit(legs: list[Leg]) -> list[str]:
    """Every violation of the four rules, as one line each."""
    problems = []
    by_mesh: dict[str, set[str]] = {}
    for leg in legs:
        shin_bare = leg.mesh in BARE_SHIN_MESHES
        if leg.covers is None:
            problems.append(f"{leg.source}: {leg.item_id} has no covers_legs attribute")
        elif leg.covers == "false" and not shin_bare:
            problems.append(f"{leg.source}: {leg.item_id} is covers_legs=false on mesh {leg.mesh}, "
                            f"which is not a bare-shin mesh")
        elif leg.covers == "true" and shin_bare:
            problems.append(f"{leg.source}: {leg.item_id} is covers_legs=true on mesh {leg.mesh}, but "
                            f"{BARE_SHIN_MESHES[leg.mesh]}")
        by_mesh.setdefault(leg.mesh, set()).add(leg.covers or "absent")
    for mesh, values in sorted(by_mesh.items()):
        if len(values) > 1 and "absent" not in values:
            problems.append(f"mesh {mesh}: items disagree on covers_legs ({', '.join(sorted(values))})")
    return problems


def scan(modules: Path, repo: Path | None = None) -> tuple[list[Leg], list[str]]:
    repo = repo or REPO
    roots =[modules / module / sub for module, sub in SCAN_ROOTS] + [repo / "Main" / "_Module" / "ModuleData"]
    legs: list[Leg] = []
    errors: list[str] = []
    for root in roots:
        if not root.is_dir():
            continue
        for path in sorted(root.rglob("*.xml")):
            if ".bak" in path.name or "Languages" in path.parts:
                continue
            found, error = read_legs(path, f"{root.parent.name}/{path.relative_to(root).as_posix()}")
            legs.extend(found)
            if error:
                errors.append(error)
    return legs, errors


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--modules", help="the Bannerlord Modules folder (default: from BANNERLORD_GAME_DIR)")
    args = ap.parse_args(argv)
    modules = Path(args.modules) if args.modules else game_modules(DEFAULT_GAME_ROOT)

    legs, errors = scan(modules, REPO)
    if not legs:
        print(f"ERROR: no LegArmor item found under {modules}; nothing was checked")
        return 2
    problems = errors + audit(legs)
    false_count = sum(1 for leg in legs if leg.covers == "false")
    print(f"{len(legs)} leg items scanned, {len(legs) - false_count} covers_legs true, {false_count} false "
          f"(bare-shin meshes: {', '.join(sorted(BARE_SHIN_MESHES))})")
    for line in problems:
        print("VIOLATION " + line)
    print("OK" if not problems else f"{len(problems)} violation(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
