#!/usr/bin/env python3
"""Migrate <EquipmentSet civilian="true"> to equipmentType="Civilian" per v1.4.3 spec.

Spec: docs/migration/v1.4.x-equipment-overhaul.md
Templates reference: docs/migration/templates/equipment-rosters.md

Per dev migration checklist (REVISED 2026-05-22 after vanilla 1.4.5 inspection):
  - <EquipmentSet civilian="true">  -> drop civilian, add equipmentType="Civilian"
  - <EquipmentSet> with no equipmentType / no civilian -> LEAVE BARE.
    Vanilla 1.4.5 does NOT add explicit equipmentType="Battle"; Battle is implicit.
  - <EquipmentRoster civilian="true"> inline inside <NPCCharacter>/<Equipments> -> LEAVE ALONE.
    Vanilla 1.4.5 still uses this 1,097x in spnpccharacters.xml.
  - <EquipmentRoster> without culture= attribute -> warn (do not modify).

ONLY touches <EquipmentSet>. Leaves civilian="true" on <EquipmentRoster> alone
(vanilla 1.4.5 confirms this pattern is still valid for inline rosters).

Usage:
    python tools/migrate_equipment_type_1_4_3.py --dry-run
    python tools/migrate_equipment_type_1_4_3.py --apply
    python tools/migrate_equipment_type_1_4_3.py --report-missing-culture
    python tools/migrate_equipment_type_1_4_3.py --path Main/_Module/ModuleData
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from typing import List, Tuple

try:
    from lxml import etree as LET
    HAVE_LXML = True
except ImportError:
    import xml.etree.ElementTree as LET  # type: ignore
    HAVE_LXML = False


# Regex-based attribute migration. Preserves all original formatting (whitespace,
# attribute order, comments, XML declaration, self-closing style). Only mutates
# the `civilian="true"` attribute inside opening <EquipmentSet> tags.
#
# Why regex instead of lxml.tree.write?  lxml serializes from scratch — it
# reformats attribute spacing, collapses multi-line tags, changes self-closing
# style, and re-emits the XML declaration with single-quote attribute values.
# That produces an unreviewable 130K-line diff for what should be a 1,628-line
# attribute change. Regex-on-text is the surgical option.
#
# Tolerant of: civilian = "true" (extra whitespace), single-quote variants,
# multi-line opening tags. Does NOT touch <EquipmentRoster civilian="true">
# (still valid in 1.4.5 per the spec — vanilla uses this 1,097x in
# spnpccharacters.xml).
EQUIPMENTSET_TAG_RE = re.compile(r'<EquipmentSet\b[^>]*>', re.DOTALL)
CIVILIAN_ATTR_RE = re.compile(r'''civilian\s*=\s*"true"''')
CIVILIAN_ATTR_SINGLEQUOTE_RE = re.compile(r"""civilian\s*=\s*'true'""")


def apply_text_migration(content: str) -> Tuple[str, int]:
    """Return (new_content, count_of_replacements)."""
    count = 0

    def replace_in_tag(match: re.Match) -> str:
        nonlocal count
        full_tag = match.group(0)
        # Only mutate if civilian="true" appears INSIDE the EquipmentSet tag.
        new_tag = full_tag
        if CIVILIAN_ATTR_RE.search(new_tag):
            new_tag = CIVILIAN_ATTR_RE.sub('equipmentType="Civilian"', new_tag)
            count += 1
        elif CIVILIAN_ATTR_SINGLEQUOTE_RE.search(new_tag):
            new_tag = CIVILIAN_ATTR_SINGLEQUOTE_RE.sub("equipmentType='Civilian'", new_tag)
            count += 1
        return new_tag

    new_content = EQUIPMENTSET_TAG_RE.sub(replace_in_tag, content)
    return new_content, count


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SCAN = REPO_ROOT / "Main" / "_Module" / "ModuleData"


def find_xml_files(root: Path) -> List[Path]:
    return sorted(p for p in root.rglob("*.xml") if p.is_file())


def migrate_tree(tree, *, file_path: Path) -> Tuple[int, int, List[str]]:
    """Mutate the tree in place. Returns (sets_changed, rosters_missing_culture, warnings)."""
    sets_changed = 0
    rosters_missing_culture = 0
    warnings: List[str] = []

    if HAVE_LXML:
        es_iter = tree.iter("EquipmentSet")
        er_iter = list(tree.iter("EquipmentRoster"))
    else:
        # ElementTree iter only walks from root; cope.
        root = tree.getroot() if hasattr(tree, "getroot") else tree
        es_iter = root.iter("EquipmentSet")
        er_iter = list(root.iter("EquipmentRoster"))

    for es in es_iter:
        civ = es.get("civilian")
        et = es.get("equipmentType")

        if civ is not None and civ.strip().lower() == "true":
            # Drop civilian, set equipmentType=Civilian.
            del es.attrib["civilian"]
            if et is None:
                es.set("equipmentType", "Civilian")
            elif et != "Civilian":
                warnings.append(
                    f"{file_path}: EquipmentSet had civilian=true AND equipmentType={et!r}; "
                    f"overriding to Civilian (civilian=true wins per migration rule)"
                )
                es.set("equipmentType", "Civilian")
            sets_changed += 1
        elif civ is not None and civ.strip().lower() != "true":
            # Edge case: civilian="false" or other; just strip it. Don't add explicit Battle —
            # vanilla 1.4.5 leaves bare <EquipmentSet> implicitly Battle.
            warnings.append(
                f"{file_path}: EquipmentSet had civilian={civ!r} (not 'true'); stripping attribute"
            )
            del es.attrib["civilian"]
            sets_changed += 1
        # else: no civilian attribute. Leave bare sets alone — vanilla 1.4.5 omits equipmentType
        # when the set is Battle (Battle is implicit). Only Civilian and Stealth are explicit.

    for er in er_iter:
        if er.get("culture") in (None, ""):
            rosters_missing_culture += 1

    return sets_changed, rosters_missing_culture, warnings


def _parse_lxml(path: Path):
    parser = LET.XMLParser(remove_blank_text=False, remove_comments=False)
    return LET.parse(str(path), parser)


def _parse_etree(path: Path):
    return LET.parse(str(path))


def process_file(path: Path, *, apply: bool, report_missing_culture: bool) -> Tuple[int, int, List[str]]:
    # Parse via lxml for accurate detection (counts EquipmentSet vs EquipmentRoster,
    # detects culture-missing EquipmentRosters). The IN-MEMORY mutation in
    # migrate_tree is used only for the count — we DO NOT serialize it.
    try:
        if HAVE_LXML:
            tree = _parse_lxml(path)
        else:
            tree = _parse_etree(path)
    except Exception as e:
        return (0, 0, [f"{path}: PARSE ERROR ({type(e).__name__}): {e}"])

    sets_changed, missing_culture, warnings = migrate_tree(tree, file_path=path)

    if apply and sets_changed > 0:
        # Write via text-substitution to preserve all formatting (whitespace,
        # attribute order, comments, self-closing style, XML declaration).
        # Do NOT use tree.write() — it reformats from scratch and produces a
        # 130K-line diff for what should be a 1.6K-line change.
        try:
            with open(path, "r", encoding="utf-8", newline="") as f:
                raw = f.read()
            new_raw, regex_count = apply_text_migration(raw)
            # Sanity check: regex count should match the lxml-detected count.
            # A mismatch implies tag-detection drift (e.g., regex missed
            # a multi-line EquipmentSet opening tag).
            if regex_count != sets_changed:
                warnings.append(
                    f"{path}: COUNT MISMATCH — lxml detected {sets_changed} "
                    f"<EquipmentSet civilian='true'> but regex replaced {regex_count}. "
                    f"Inspect manually before re-running."
                )
                # Don't write a partial migration on count mismatch.
                return sets_changed, missing_culture, warnings
            with open(path, "w", encoding="utf-8", newline="") as f:
                f.write(new_raw)
        except Exception as e:
            warnings.append(f"{path}: WRITE FAILED ({type(e).__name__}): {e}")

    return sets_changed, missing_culture, warnings


def main(argv: List[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="Default. Print summary, no writes.")
    ap.add_argument("--apply", action="store_true", help="Write modified XML back to disk.")
    ap.add_argument("--report-missing-culture", action="store_true",
                    help="List EquipmentRoster elements lacking a culture= attribute.")
    ap.add_argument("--path", type=Path, default=DEFAULT_SCAN,
                    help=f"Scan path (default: {DEFAULT_SCAN.relative_to(REPO_ROOT)})")
    args = ap.parse_args(argv)

    if args.apply and args.dry_run:
        print("ERROR: --apply and --dry-run are mutually exclusive.", file=sys.stderr)
        return 2

    apply = bool(args.apply)
    scan_root: Path = args.path
    if not scan_root.exists():
        print(f"ERROR: scan path does not exist: {scan_root}", file=sys.stderr)
        return 2

    files = find_xml_files(scan_root)
    if not files:
        print(f"No XML files found under {scan_root}", file=sys.stderr)
        return 1

    mode = "APPLY" if apply else "DRY-RUN"
    print(f"=== migrate_equipment_type_1_4_3.py [{mode}] ===")
    print(f"Scan root: {scan_root}")
    print(f"Files scanned: {len(files)}")
    print(f"lxml available: {HAVE_LXML}")
    print()
    print(f"{'FILE':<80} {'SETS':>5} {'MISSING_CULTURE':>17}")
    print("-" * 105)

    total_sets = 0
    total_missing = 0
    all_warnings: List[str] = []
    changed_files: List[Tuple[Path, int, int]] = []

    for path in files:
        sets_changed, missing_culture, warnings = process_file(
            path,
            apply=apply,
            report_missing_culture=args.report_missing_culture,
        )
        total_sets += sets_changed
        total_missing += missing_culture
        all_warnings.extend(warnings)

        if sets_changed > 0 or missing_culture > 0:
            rel = path.relative_to(REPO_ROOT) if path.is_relative_to(REPO_ROOT) else path
            rel_str = str(rel)
            if len(rel_str) > 78:
                rel_str = "..." + rel_str[-75:]
            print(f"{rel_str:<80} {sets_changed:>5} {missing_culture:>17}")
            if sets_changed > 0:
                changed_files.append((path, sets_changed, missing_culture))

    print("-" * 105)
    print(f"{'TOTAL':<80} {total_sets:>5} {total_missing:>17}")
    print()
    print(f"Files needing changes: {len(changed_files)}")
    print(f"Total EquipmentSet elements to migrate: {total_sets}")
    print(f"Total EquipmentRoster elements missing culture: {total_missing}")

    if all_warnings:
        print()
        print(f"=== WARNINGS ({len(all_warnings)}) ===")
        for w in all_warnings[:50]:
            print("  " + w)
        if len(all_warnings) > 50:
            print(f"  ... and {len(all_warnings) - 50} more")

    if args.report_missing_culture and total_missing > 0:
        print()
        print("=== EquipmentRoster elements missing culture= attribute ===")
        for path in files:
            try:
                tree = _parse_lxml(path) if HAVE_LXML else _parse_etree(path)
            except Exception:
                continue
            root = tree.getroot() if hasattr(tree, "getroot") else tree
            for er in root.iter("EquipmentRoster"):
                if er.get("culture") in (None, ""):
                    rid = er.get("id", "<no-id>")
                    rel = path.relative_to(REPO_ROOT) if path.is_relative_to(REPO_ROOT) else path
                    print(f"  {rel}: <EquipmentRoster id={rid!r}> missing culture")

    if not apply:
        print()
        print("Dry run complete. Re-run with --apply to write changes.")
    else:
        print()
        print(f"Applied changes to {len(changed_files)} files.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
