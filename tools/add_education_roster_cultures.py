#!/usr/bin/env python3
"""Add the missing culture="Culture.<name>" attribute to every child-education
EquipmentRoster in taom_education_equipment_templates.xml.

Vanilla education_equipment_templates.xml declares a `culture` attribute on each
<EquipmentRoster>; TAOM's mirror omitted it, producing ~980 startup warnings:
    EquipmentRoster with id: child_education_equipments_..._gundabad don't have culture definition

The culture is the final underscore token of the roster id. Each id line, e.g.

            id="child_education_equipments_stage_1_page_0_branch_default_gundabad">

becomes the vanilla two-attribute layout:

            id="child_education_equipments_stage_1_page_0_branch_default_gundabad"
            culture="Culture.gundabad">

Idempotent (a processed id line ends in '"' not '">', so it won't re-match).
Preserves CRLF, tab indentation, and UTF-8-no-BOM encoding.
"""
import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
TARGET = REPO / "Main" / "_Module" / "ModuleData" / "equipmentsets" / "taom_education_equipment_templates.xml"

# The 10 cultures that own child-education rosters — all verified present in
# taom_spcultures.xml, so Culture.<name> resolves cleanly.
KNOWN_CULTURES = {
    "dolguldur", "erebor", "goblin", "gondor", "gundabad",
    "isengard", "mirkwood", "mistymountainorcs", "mordor", "rivendell",
}

# Greedy [a-z0-9_]+ backtracks so group 3 is the trailing culture token,
# whether the branch segment is `default`, `child`, or a digit.
ID_LINE = re.compile(r'^(\s*)id="(child_education_equipments_[a-z0-9_]+_([a-z]+))">\s*$')


def transform(text: str):
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(nl)
    out = []
    counts = {}
    skipped = []
    for line in lines:
        m = ID_LINE.match(line)
        if not m:
            out.append(line)
            continue
        indent, full_id, culture = m.group(1), m.group(2), m.group(3)
        if culture not in KNOWN_CULTURES:
            # Never fabricate a Culture.X for an unrecognised id — pass through untouched.
            skipped.append(full_id)
            out.append(line)
            continue
        out.append(f'{indent}id="{full_id}"')
        out.append(f'{indent}culture="Culture.{culture}">')
        counts[culture] = counts.get(culture, 0) + 1
    return nl.join(out), counts, skipped


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the file (default is dry-run)")
    ap.add_argument("--path", default=str(TARGET), help="target XML (default: TAOM education templates)")
    args = ap.parse_args()

    path = Path(args.path)
    if not path.is_file():
        print(f"ERROR: target not found: {path}", file=sys.stderr)
        return 1

    original = path.read_bytes().decode("utf-8")
    updated, counts, skipped = transform(original)

    total = sum(counts.values())
    print(f"Target: {path}")
    for culture in sorted(counts):
        print(f"  {culture:20s} +{counts[culture]}")
    print(f"  {'TOTAL':20s} +{total}")
    if skipped:
        print(f"WARNING: {len(skipped)} id(s) had an unknown culture token and were skipped:", file=sys.stderr)
        for s in skipped[:20]:
            print(f"    {s}", file=sys.stderr)

    if total == 0:
        print("No changes (file already has culture attributes, or no matching rosters).")
        return 0

    if not args.apply:
        print("\nDRY RUN — re-run with --apply to write. (.bak backup will be created.)")
        return 0

    backup = path.with_suffix(path.suffix + ".bak")
    backup.write_bytes(original.encode("utf-8"))
    path.write_bytes(updated.encode("utf-8"))
    print(f"\nApplied. Backup written to {backup}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
