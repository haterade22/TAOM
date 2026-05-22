#!/usr/bin/env python3
"""Audit TAOM custom-culture equipment roster coverage per v1.4.3 mandatory matrix.

Spec: docs/migration/v1.4.x-equipment-overhaul.md ("TAOM mandatory roster matrix").

For each TAOM custom culture (enumerated from taom_spcultures.xml, is_main_culture="true"),
verify the existence of at least these 8 mandatory rosters:

  1. IsLordTemplate                                                 + Battle
  2. IsLordTemplate | IsFemaleTemplate                              + Battle
  3. IsLordTemplate                                                 + Civilian
  4. IsLordTemplate | IsFemaleTemplate                              + Civilian
  5. IsLordTemplate | IsChildEquipmentTemplate                      + Civilian
  6. IsLordTemplate | IsChildEquipmentTemplate | IsFemaleTemplate   + Civilian
  7. IsLordTemplate | IsTeenagerEquipmentTemplate                   + Civilian
  8. IsLordTemplate | IsTeenagerEquipmentTemplate | IsFemaleTemplate+ Civilian

Optional (kingdom-tier cultures, recommended):

  9. IsKingdomRulerTemplate                                         + Battle
 10. IsKingdomRulerTemplate | IsFemaleTemplate                      + Battle
 11. IsKingdomRulerTemplate                                         + Civilian
 12. IsKingdomRulerTemplate | IsFemaleTemplate                      + Civilian

Scans Main/_Module/ModuleData/equipmentsets/*.xml.

Output:
  - docs/migration/equipment-roster-coverage.csv (matrix)
  - console summary (pass/fail per culture)

Usage:
    python tools/audit_equipment_roster_coverage.py
    python tools/audit_equipment_roster_coverage.py --path Main/_Module/ModuleData
"""
from __future__ import annotations

import argparse
import csv
import sys
from collections import defaultdict
from pathlib import Path
from typing import Dict, FrozenSet, List, Set, Tuple

try:
    from lxml import etree as LET
    HAVE_LXML = True
except ImportError:
    import xml.etree.ElementTree as LET  # type: ignore
    HAVE_LXML = False


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_MODULE_DATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
DEFAULT_CULTURES_XML = DEFAULT_MODULE_DATA / "taom_spcultures.xml"
DEFAULT_EQUIP_DIR = DEFAULT_MODULE_DATA / "equipmentsets"
DEFAULT_OUTPUT = REPO_ROOT / "docs" / "migration" / "equipment-roster-coverage.csv"


# v1.4.3 flag set.
NEW_FLAGS = {
    "IsFemaleTemplate",
    "IsLordTemplate",
    "IsChildEquipmentTemplate",
    "IsTeenagerEquipmentTemplate",
    "IsKingdomRulerTemplate",
}

# Required (culture, frozenset-of-flags, equipmentType) combos. Order matters for CSV.
MANDATORY_COMBOS: List[Tuple[FrozenSet[str], str, str]] = [
    (frozenset({"IsLordTemplate"}), "Battle", "Male lord battle"),
    (frozenset({"IsLordTemplate", "IsFemaleTemplate"}), "Battle", "Female lord battle"),
    (frozenset({"IsLordTemplate"}), "Civilian", "Male lord civilian"),
    (frozenset({"IsLordTemplate", "IsFemaleTemplate"}), "Civilian", "Female lord civilian"),
    (frozenset({"IsLordTemplate", "IsChildEquipmentTemplate"}), "Civilian", "Male lord child"),
    (frozenset({"IsLordTemplate", "IsChildEquipmentTemplate", "IsFemaleTemplate"}), "Civilian", "Female lord child"),
    (frozenset({"IsLordTemplate", "IsTeenagerEquipmentTemplate"}), "Civilian", "Male lord teen"),
    (frozenset({"IsLordTemplate", "IsTeenagerEquipmentTemplate", "IsFemaleTemplate"}), "Civilian", "Female lord teen"),
]

OPTIONAL_COMBOS: List[Tuple[FrozenSet[str], str, str]] = [
    (frozenset({"IsKingdomRulerTemplate"}), "Battle", "Male king battle"),
    (frozenset({"IsKingdomRulerTemplate", "IsFemaleTemplate"}), "Battle", "Female queen battle"),
    (frozenset({"IsKingdomRulerTemplate"}), "Civilian", "Male king civilian"),
    (frozenset({"IsKingdomRulerTemplate", "IsFemaleTemplate"}), "Civilian", "Female queen civilian"),
]


def parse(path: Path):
    if HAVE_LXML:
        parser = LET.XMLParser(remove_blank_text=False, remove_comments=False)
        return LET.parse(str(path), parser)
    return LET.parse(str(path))


def enumerate_taom_cultures(cultures_xml: Path) -> List[str]:
    """Return culture string-ids from taom_spcultures.xml where is_main_culture='true'."""
    tree = parse(cultures_xml)
    root = tree.getroot() if hasattr(tree, "getroot") else tree
    cultures: List[str] = []
    for c in root.iter("Culture"):
        if c.get("is_main_culture") == "true":
            cid = c.get("id")
            if cid:
                cultures.append(cid)
    return cultures


def normalize_culture(raw: str | None) -> str | None:
    """Strip 'Culture.' prefix; lowercase."""
    if not raw:
        return None
    if raw.startswith("Culture."):
        raw = raw[len("Culture."):]
    return raw.strip().lower() or None


def extract_flags(flags_elem) -> Set[str]:
    """Return set of flag names whose attribute value is 'true'."""
    flags: Set[str] = set()
    if flags_elem is None:
        return flags
    for k, v in flags_elem.attrib.items():
        if isinstance(v, str) and v.strip().lower() == "true":
            flags.add(k)
    return flags


def equipment_type_of(equipment_set) -> str:
    """Return 'Civilian' / 'Battle' / 'Stealth'. Defaults to 'Battle' if unset/no civilian=true."""
    et = equipment_set.get("equipmentType")
    if et:
        return et
    # Fallback for pre-migration XML.
    if (equipment_set.get("civilian") or "").strip().lower() == "true":
        return "Civilian"
    return "Battle"


def scan_rosters(equip_dir: Path) -> Dict[str, Dict[Tuple[FrozenSet[str], str], int]]:
    """Return {culture_id: {(flag_set, equipment_type): count}}.

    A roster contributes one entry per (flag_set, type) for each child EquipmentSet
    it contains — so a roster with 3 civilian sets and 1 battle set is counted as
    3 civilian + 1 battle for that flag combination.
    """
    out: Dict[str, Dict[Tuple[FrozenSet[str], str], int]] = defaultdict(lambda: defaultdict(int))

    for xml_path in sorted(equip_dir.rglob("*.xml")):
        try:
            tree = parse(xml_path)
        except Exception as e:
            print(f"WARN: parse error in {xml_path}: {e}", file=sys.stderr)
            continue
        root = tree.getroot() if hasattr(tree, "getroot") else tree

        for roster in root.iter("EquipmentRoster"):
            culture_raw = roster.get("culture")
            culture = normalize_culture(culture_raw)
            if not culture:
                continue

            flags_elem = None
            for child in roster:
                tag = getattr(child, "tag", None)
                if isinstance(tag, str) and tag == "Flags":
                    flags_elem = child
                    break
            flags = extract_flags(flags_elem)
            # Only consider the v1.4.3 flag set for matrix matching.
            flag_subset = flags & NEW_FLAGS
            flag_fs = frozenset(flag_subset)

            for es in roster.iter("EquipmentSet"):
                et = equipment_type_of(es)
                out[culture][(flag_fs, et)] += 1

    return out


def audit(cultures: List[str],
          coverage: Dict[str, Dict[Tuple[FrozenSet[str], str], int]]
          ) -> List[Dict[str, str]]:
    rows: List[Dict[str, str]] = []
    for culture in cultures:
        culture_cov = coverage.get(culture, {})
        for flags_fs, et, purpose in MANDATORY_COMBOS:
            count = culture_cov.get((flags_fs, et), 0)
            rows.append({
                "culture": culture,
                "required_combo": " | ".join(sorted(flags_fs)) if flags_fs else "(no flags)",
                "purpose": purpose,
                "equipmentType": et,
                "found_count": str(count),
                "status": "PASS" if count > 0 else "MISSING",
                "tier": "mandatory",
            })
        for flags_fs, et, purpose in OPTIONAL_COMBOS:
            count = culture_cov.get((flags_fs, et), 0)
            rows.append({
                "culture": culture,
                "required_combo": " | ".join(sorted(flags_fs)) if flags_fs else "(no flags)",
                "purpose": purpose,
                "equipmentType": et,
                "found_count": str(count),
                "status": "PASS" if count > 0 else "MISSING-OPTIONAL",
                "tier": "optional",
            })
    return rows


def write_csv(rows: List[Dict[str, str]], output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    fields = ["culture", "required_combo", "purpose", "equipmentType", "found_count", "status", "tier"]
    with output.open("w", encoding="utf-8", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)


def print_summary(cultures: List[str], rows: List[Dict[str, str]]) -> None:
    by_culture: Dict[str, List[Dict[str, str]]] = defaultdict(list)
    for r in rows:
        by_culture[r["culture"]].append(r)

    print()
    print("=== Per-culture coverage summary ===")
    print(f"{'CULTURE':<14} {'MAND_PASS':>9} {'MAND_MISS':>9} {'OPT_PASS':>8} {'OPT_MISS':>8}  STATUS")
    print("-" * 80)
    total_mand_miss = 0
    fail_cultures: List[str] = []
    for culture in cultures:
        cr = by_culture[culture]
        mand = [r for r in cr if r["tier"] == "mandatory"]
        opt = [r for r in cr if r["tier"] == "optional"]
        mp = sum(1 for r in mand if r["status"] == "PASS")
        mm = sum(1 for r in mand if r["status"] == "MISSING")
        op = sum(1 for r in opt if r["status"] == "PASS")
        om = sum(1 for r in opt if r["status"] == "MISSING-OPTIONAL")
        status = "OK" if mm == 0 else f"FAIL ({mm} missing)"
        total_mand_miss += mm
        if mm > 0:
            fail_cultures.append(culture)
        print(f"{culture:<14} {mp:>9} {mm:>9} {op:>8} {om:>8}  {status}")

    print("-" * 80)
    print(f"Cultures audited: {len(cultures)}")
    print(f"Mandatory misses total: {total_mand_miss}")
    print(f"Cultures FAILING mandatory coverage: {len(fail_cultures)}")

    if fail_cultures:
        print()
        print("=== Detailed missing rosters (mandatory) ===")
        for culture in fail_cultures:
            print(f"\n[{culture}]")
            for r in by_culture[culture]:
                if r["tier"] == "mandatory" and r["status"] == "MISSING":
                    print(f"  MISSING: {r['purpose']:<24} flags=[{r['required_combo']}] type={r['equipmentType']}")


def main(argv: List[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--path", type=Path, default=DEFAULT_MODULE_DATA,
                    help=f"ModuleData root (default: {DEFAULT_MODULE_DATA.relative_to(REPO_ROOT)})")
    ap.add_argument("--output", type=Path, default=DEFAULT_OUTPUT,
                    help=f"CSV output path (default: {DEFAULT_OUTPUT.relative_to(REPO_ROOT)})")
    args = ap.parse_args(argv)

    module_data: Path = args.path
    cultures_xml = module_data / "taom_spcultures.xml"
    equip_dir = module_data / "equipmentsets"

    if not cultures_xml.exists():
        print(f"ERROR: cultures xml not found at {cultures_xml}", file=sys.stderr)
        return 2
    if not equip_dir.exists():
        print(f"ERROR: equipmentsets dir not found at {equip_dir}", file=sys.stderr)
        return 2

    cultures = enumerate_taom_cultures(cultures_xml)
    print(f"=== audit_equipment_roster_coverage.py ===")
    print(f"Cultures XML: {cultures_xml}")
    print(f"Equipment sets dir: {equip_dir}")
    print(f"lxml available: {HAVE_LXML}")
    print()
    print(f"TAOM custom cultures (is_main_culture=true): {len(cultures)}")
    print(f"  {', '.join(cultures)}")

    coverage = scan_rosters(equip_dir)
    rows = audit(cultures, coverage)
    write_csv(rows, args.output)
    print()
    print(f"CSV written: {args.output}")

    print_summary(cultures, rows)

    # Exit 1 if any mandatory miss.
    mand_misses = sum(1 for r in rows if r["tier"] == "mandatory" and r["status"] == "MISSING")
    return 0 if mand_misses == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
