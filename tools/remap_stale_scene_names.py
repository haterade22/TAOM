#!/usr/bin/env python3
"""
Remap stale settlement scene_name references in TAOM_Map/settlements.xml to scenes that
actually exist on disk (v1.4.5). Found via tools/audit_scene_names.py (2026-05-28):

  - 4 vanilla house-interior scenes were RENAMED in v1.4.5; the towns' house_1/2/3
    locations still point at the old names -> crash on entering those houses.
  - East Osgiliath referenced a misspelled custom scene (lotraom vs lotrtaom).
  - 3 Isengard settlements reference custom scenes that don't exist on disk -> replaced
    with a rugged vanilla scene of the matching type to stop crashes (aesthetic downgrade;
    rebuild proper Isengard scenes later).

Every replacement is verified to exist as a SceneObj folder (case-insensitive) before writing.

Usage:
    python remap_stale_scene_names.py --dry-run
    python remap_stale_scene_names.py --apply [--backup] [--shadow]
"""
from __future__ import annotations
import argparse
import re
from pathlib import Path

GAME = Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
MODULES = GAME / "Modules"
LIVE = MODULES / "TAOM_Map" / "ModuleData" / "settlements.xml"
SHADOW = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "settlements.xml"

REMAP = {
    # vanilla house-interior renames (v1.4.5)
    "battania_house_a_interior_house": "battania_town_house_b_interior_b_house",
    "khuzait_house_a_interior_house": "khuzait_house_c_interior_a_house",
    "sturgia_house_a_interior_house": "sturgia_town_house_d1_interior_b_house",
    "vlandia_house_d_interior_house": "vlandia_city_house_a_interior_house",
    # custom Osgiliath scene typo (scene exists as lotrtaom_*)
    "lotraom_e_osgiliath_i_forceatmo": "lotrtaom_e_osgiliath_i_forceatmo",
    # Isengard custom scenes absent on disk -> rugged vanilla of matching type
    "castle_orthanc_gate": "battania_castle_a",
    "castle_village_isengard_a": "battania_village_c",
    "village_isengard_a": "battania_village_e",
}


def scene_folders_lower() -> set[str]:
    out = set()
    for so in MODULES.glob("*/SceneObj"):
        for c in so.iterdir():
            if c.is_dir():
                out.add(c.name.lower())
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--backup", action="store_true")
    ap.add_argument("--shadow", action="store_true", help="also remap the repo shadow settlements.xml")
    args = ap.parse_args()
    if not (args.dry_run or args.apply):
        ap.error("pass --dry-run or --apply")

    folders = scene_folders_lower()
    bad = [new for new in REMAP.values() if new.lower() not in folders]
    if bad:
        print("ABORT — replacement scene(s) not found on disk:")
        for b in bad:
            print(f"  {b}")
        return 1
    print(f"All {len(set(REMAP.values()))} replacement scenes verified present on disk.\n")

    targets = [LIVE]
    if args.shadow:
        targets.append(SHADOW)

    for path in targets:
        if not path.exists():
            print(f"skip (missing): {path}")
            continue
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        had_bom = path.read_bytes().startswith(b"\xef\xbb\xbf")
        total = 0
        print(f"== {path} ==")
        for old, new in REMAP.items():
            pat = f'scene_name="{old}"'
            n = text.count(pat)
            if n:
                text = text.replace(pat, f'scene_name="{new}"')
                total += n
                print(f"  {old} -> {new}  ({n})")
        print(f"  total replacements: {total}")
        if args.apply and total:
            if args.backup:
                path.with_suffix(".xml.bak_scenes").write_bytes(path.read_bytes())
            path.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))
            print("  [APPLIED]")
        elif args.dry_run:
            print("  [DRY RUN]")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
