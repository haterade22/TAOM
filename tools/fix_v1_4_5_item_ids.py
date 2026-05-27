#!/usr/bin/env python3
"""
One-shot find-and-replace for v1.3 → v1.4.5 broken item ID mappings discovered
by tools/audit_item_refs.py. Targets the top-10 highest-reference broken IDs
(covers ~210 ref sites of the 359 total audit findings).

Each mapping is `Item.<old> → Item.<new>` where the new ID has been verified
to exist in the v1.4.5 item registry (SandBoxCore or LOTRLOME_Armory).

Usage:  python tools/fix_v1_4_5_item_ids.py [--dry-run|--apply]
"""
import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TAOM_MODULEDATA = ROOT / "Main/_Module/ModuleData"

# v1.3 broken ID  →  v1.4.5 equivalent (item ID, no `Item.` prefix)
MAPPINGS = [
    # Vanilla v1.3 items renamed/removed in v1.4.5 — replacements selected by
    # role/culture match in SandBoxCore body_armors.xml / weapons.xml.
    ("thick_padded_leather",  "padded_leather_overcoat"),   # 38 refs — padded body armor
    ("khuzait_civil_coat_a",  "khuzait_civil_coat"),        # 31 refs — drop _a suffix
    ("sturgia_civil_a",       "nordic_padded_cloth"),       # 29 refs — sturgia civilian equiv
    ("highland_jerkin",       "highland_cloth"),            # 29 refs — battanian highland equiv
    ("khuzait_civil_b",       "khuzait_civil_coat_b"),      # 27 refs — add coat_ infix
    ("long_bow",              "noble_long_bow"),            # 14 refs — direct rename
    ("fur_dress",             "fur_skirt"),                 # 11 refs — fur civilian equiv

    # LOTRLOME missing `wm_` prefix typos — items exist with prefix.
    ("rivendell_sword_a01",   "wm_rivendell_sword_a01"),    # 11 refs
    ("rivendell_spear_a01",   "wm_rivendell_spear_a01"),    # 11 refs
    ("rivendell_shield_a01",  "wm_rivendell_shield_a02"),   # 11 refs — shield only ships _a02

    # Vanilla v1.3 second-tier items (lower ref counts but still real breakage)
    ("empire_horseman_tunic", "empire_horseman_armor"),     # 6 refs — drop _tunic suffix
    ("empire_formal_armor",   "empire_plate_vest_armor"),   # 3 refs — closest formal equiv
    ("padded_leather_coat",   "padded_leather_overcoat"),   # 3 refs — _coat → _overcoat
]


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--apply", action="store_true")
    g.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    total_replacements = 0
    files_touched = set()

    for xml in TAOM_MODULEDATA.rglob("*.xml"):
        try:
            text = xml.read_text(encoding="utf-8", errors="ignore")
        except Exception as e:
            print(f"WARN read failed {xml}: {e}", file=sys.stderr)
            continue
        original = text
        file_replacements = 0
        for old_id, new_id in MAPPINGS:
            old_ref = f'"Item.{old_id}"'
            new_ref = f'"Item.{new_id}"'
            count = text.count(old_ref)
            if count:
                text = text.replace(old_ref, new_ref)
                file_replacements += count
        if text != original:
            files_touched.add(xml)
            total_replacements += file_replacements
            if args.apply:
                xml.write_text(text, encoding="utf-8")
                print(f"  {xml.relative_to(ROOT)}: {file_replacements} replacements")
            else:
                print(f"  WOULD {xml.relative_to(ROOT)}: {file_replacements} replacements")

    print()
    print(f"Total: {total_replacements} replacements across {len(files_touched)} files")
    print(f"Mappings applied: {len(MAPPINGS)}")
    if not args.apply:
        print("Run with --apply to write.")


if __name__ == "__main__":
    main()
