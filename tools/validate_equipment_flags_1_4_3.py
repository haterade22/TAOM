#!/usr/bin/env python3
"""Detect deprecated v1.3.15 EquipmentFlags values in TAOM XML + C# source.

Spec: docs/migration/v1.4.x-equipment-overhaul.md

The 13 removed flag names (replaced by IsLordTemplate / IsChildEquipmentTemplate /
IsTeenagerEquipmentTemplate / IsKingdomRulerTemplate / IsFemaleTemplate in 1.4.3):

  - IsWandererEquipment
  - IsGentryEquipment
  - IsRebelHeroEquipment
  - IsNoncombatantTemplate
  - IsCombatantTemplate
  - IsCivilianTemplate
  - IsNobleTemplate
  - IsMediumTemplate
  - IsHeavyTemplate
  - IsFlamboyantTemplate
  - IsStoicTemplate
  - IsNomadTemplate
  - IsWoodlandTemplate

Walks Main/_Module/ModuleData/** and Main/**/*.cs. Reports file:line:flag_name.

Exit code: 0 if no hits, 1 if any hits remain.

Usage:
    python tools/validate_equipment_flags_1_4_3.py
    python tools/validate_equipment_flags_1_4_3.py --path Main
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from typing import Iterable, List, Tuple


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SCAN_ROOTS = [
    REPO_ROOT / "Main" / "_Module" / "ModuleData",
    REPO_ROOT / "Main",
]

DEPRECATED_FLAGS = [
    "IsWandererEquipment",
    "IsGentryEquipment",
    "IsRebelHeroEquipment",
    "IsNoncombatantTemplate",
    "IsCombatantTemplate",
    "IsCivilianTemplate",
    "IsNobleTemplate",
    "IsMediumTemplate",
    "IsHeavyTemplate",
    "IsFlamboyantTemplate",
    "IsStoicTemplate",
    "IsNomadTemplate",
    "IsWoodlandTemplate",
]

# Word-bounded match per flag.
FLAG_PATTERN = re.compile(r"\b(" + "|".join(re.escape(f) for f in DEPRECATED_FLAGS) + r")\b")

SCANNABLE_EXTENSIONS = {".xml", ".cs", ".xslt"}


def iter_files(roots: Iterable[Path]) -> Iterable[Path]:
    seen: set[Path] = set()
    for root in roots:
        if not root.exists():
            print(f"WARN: scan root missing: {root}", file=sys.stderr)
            continue
        for path in root.rglob("*"):
            if not path.is_file():
                continue
            if path.suffix.lower() not in SCANNABLE_EXTENSIONS:
                continue
            # Skip bin / obj / packages noise.
            parts_lower = {p.lower() for p in path.parts}
            if parts_lower & {"bin", "obj", "packages", ".vs", "node_modules"}:
                continue
            if path in seen:
                continue
            seen.add(path)
            yield path


def scan_file(path: Path) -> List[Tuple[int, str, str]]:
    """Return list of (line_number, flag_name, line_content)."""
    hits: List[Tuple[int, str, str]] = []
    try:
        with path.open("r", encoding="utf-8", errors="replace") as fh:
            for lineno, line in enumerate(fh, start=1):
                for m in FLAG_PATTERN.finditer(line):
                    hits.append((lineno, m.group(1), line.rstrip("\n")))
    except OSError as e:
        print(f"WARN: could not read {path}: {e}", file=sys.stderr)
    return hits


def main(argv: List[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--path", type=Path, action="append", default=None,
                    help="Scan root (repeatable). Defaults: Main/_Module/ModuleData + Main")
    ap.add_argument("--verbose", action="store_true",
                    help="Print the matching line content alongside file:line:flag")
    args = ap.parse_args(argv)

    roots = args.path if args.path else DEFAULT_SCAN_ROOTS

    print(f"=== validate_equipment_flags_1_4_3.py ===")
    for r in roots:
        print(f"Scanning: {r}")
    print(f"Looking for {len(DEPRECATED_FLAGS)} deprecated flag names")
    print()

    total_hits = 0
    flag_counts: dict[str, int] = {f: 0 for f in DEPRECATED_FLAGS}
    file_counts: dict[Path, int] = {}

    for path in iter_files(roots):
        hits = scan_file(path)
        if not hits:
            continue
        rel = path.relative_to(REPO_ROOT) if path.is_relative_to(REPO_ROOT) else path
        file_counts[path] = len(hits)
        total_hits += len(hits)
        for lineno, flag, line_content in hits:
            flag_counts[flag] += 1
            if args.verbose:
                print(f"{rel}:{lineno}:{flag}\t{line_content.strip()}")
            else:
                print(f"{rel}:{lineno}:{flag}")

    print()
    print("=== Summary ===")
    print(f"Total deprecated-flag occurrences: {total_hits}")
    print(f"Files affected: {len(file_counts)}")
    if total_hits > 0:
        print()
        print("Per-flag counts:")
        for flag in DEPRECATED_FLAGS:
            if flag_counts[flag] > 0:
                print(f"  {flag:<30} {flag_counts[flag]:>6}")

    if total_hits == 0:
        print("CLEAN: no deprecated EquipmentFlags found.")
        return 0
    return 1


if __name__ == "__main__":
    sys.exit(main())
