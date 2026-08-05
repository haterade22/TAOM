#!/usr/bin/env python3
"""Audit enlistment roster coverage (#375 Phase 4, E1 gate).

Asserts every tree-culture x rank cell (16 cultures from troops_*.xml x 4 ranks
= 64) has an enlist_{runtimeCultureId}_{rank} roster in
Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml, or is a
DOCUMENTED fallback (listed in DOCUMENTED_FALLBACKS with a reason). The four
enlist_default_{rank} rosters are the resolver's last resort and are always
mandatory. Also enforces the file's structural invariants: armor slots only
(Head/Body/Leg/Gloves/Cape), roster culture attribute matching the id's
culture token, and neutral_culture on the defaults.

Non-tree cultures (no troops_*.xml: lothlorien, battania/Khand) are not cells;
they fall through to enlist_default_{rank} at runtime by design.

Modeled on tools/audit_equipment_roster_coverage.py. Exit 0 = full coverage.

Usage:
    python tools/audit_enlistment_roster_coverage.py
"""
from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
TROOPS_DIR = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "troops"
DEFAULT_XML = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "equipmentsets" / "taom_enlistment_equipment.xml"

RANKS = ["recruit", "soldier", "veteran", "sergeant"]
ARMOR_SLOTS = {"Head", "Body", "Leg", "Gloves", "Cape"}
DEFAULT_CULTURE = "neutral_culture"

# (culture, rank) cells allowed to be missing, each with the reason the runtime
# fallback (lower rank -> enlist_default_{rank}) is acceptable. Empty today:
# all 64 cells are generated. Add entries here ONLY with a stated reason.
DOCUMENTED_FALLBACKS: dict[tuple[str, str], str] = {}


def tree_cultures() -> list[str]:
    """Runtime culture StringIds of the 16 troop trees (from troops_*.xml)."""
    cultures = set()
    for path in sorted(TROOPS_DIR.glob("troops_*.xml")):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as e:
            print(f"WARN: parse error in {path.name}: {e}", file=sys.stderr)
            continue
        npc = root.find(".//NPCCharacter")
        raw = npc.get("culture", "") if npc is not None else ""
        if raw.startswith("Culture."):
            cultures.add(raw.split(".", 1)[1])
    return sorted(cultures)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--xml", type=Path, default=DEFAULT_XML,
                    help=f"roster file (default: {DEFAULT_XML.relative_to(REPO_ROOT)})")
    args = ap.parse_args(argv)

    if not args.xml.exists():
        print(f"ERROR: roster file not found: {args.xml}", file=sys.stderr)
        return 2
    try:
        root = ET.fromstring(args.xml.read_bytes().decode("utf-8-sig"))
    except ET.ParseError as e:
        print(f"ERROR: {args.xml.name} is not well-formed XML: {e}", file=sys.stderr)
        return 2

    rosters = {r.get("id"): r for r in root.findall("EquipmentRoster") if r.get("id")}
    cultures = tree_cultures()
    print(f"=== audit_enlistment_roster_coverage.py ===")
    print(f"Roster file: {args.xml}")
    print(f"Tree-cultures ({len(cultures)}): {', '.join(cultures)}")
    print(f"Rosters found: {len(rosters)}")

    failures: list[str] = []
    documented: list[str] = []

    # 1. Coverage: every culture x rank cell.
    for culture in cultures:
        for rank in RANKS:
            rid = f"enlist_{culture}_{rank}"
            if rid in rosters:
                continue
            reason = DOCUMENTED_FALLBACKS.get((culture, rank))
            if reason:
                documented.append(f"{rid}: documented fallback -- {reason}")
            else:
                failures.append(f"MISSING cell: {rid} (no documented fallback)")

    # 2. Mandatory defaults (the resolver's last resort).
    for rank in RANKS:
        rid = f"enlist_default_{rank}"
        if rid not in rosters:
            failures.append(f"MISSING default: {rid} (mandatory -- resolver last resort)")

    # 3. Structural invariants.
    for rid, roster in sorted(rosters.items()):
        if not rid.startswith("enlist_"):
            failures.append(f"FOREIGN roster id in enlistment file: {rid}")
            continue
        culture_attr = roster.get("culture") or ""
        body = rid[len("enlist_"):]
        rank = next((r for r in RANKS if body.endswith("_" + r)), None)
        if rank is None:
            failures.append(f"BAD id (no rank suffix): {rid}")
            continue
        culture_token = body[: -(len(rank) + 1)]
        expected = DEFAULT_CULTURE if culture_token == "default" else culture_token
        if culture_attr != f"Culture.{expected}":
            failures.append(f"CULTURE MISMATCH: {rid} carries culture={culture_attr!r}, "
                            f"expected 'Culture.{expected}'")
        for eq in roster.iter("Equipment"):
            slot = eq.get("slot")
            if slot not in ARMOR_SLOTS:
                failures.append(f"NON-ARMOR slot in {rid}: {slot!r} "
                                f"(armor-only contract: {sorted(ARMOR_SLOTS)})")
        if not any(True for _ in roster.iter("Equipment")):
            failures.append(f"EMPTY roster: {rid}")

    covered = len(cultures) * len(RANKS) - sum(1 for f in failures if f.startswith("MISSING cell"))
    print(f"\nCells covered: {covered}/{len(cultures) * len(RANKS)}"
          f" + {sum(1 for r in RANKS if f'enlist_default_{r}' in rosters)}/{len(RANKS)} defaults")
    for line in documented:
        print(f"  DOCUMENTED: {line}")

    if failures:
        print(f"\nFAIL: {len(failures)} problem(s):")
        for f in failures:
            print(f"  {f}")
        return 1
    print("\nPASS: full 64-cell coverage + 4 defaults; armor-only + culture invariants hold.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
