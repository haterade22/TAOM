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
    "castle_village_isengard_a": "battania_village_e",  # was battania_village_c (1.4.7-removed)
    "village_isengard_a": "battania_village_e",
    # town-center scenes deleted by the post-2026-05-28 SceneObj rename wave (plan 004).
    # NOTE: each town center repeats the scene across scene_name / _1 / _2 / _3 slots,
    # so the matcher below must cover all four (a bare scene_name= match fixes only 1/4).
    "HART_isengard": "taom_isengard_town_orthanc_forceatmo",
    "Helms_Deep_Town_forceatmo": "taom_rohan_castle_helms_deep_forceatmo",
    "lotrtaom_hat_gondor_town_calembel": "empire_town_h",
    # v1.4.7 removed these vanilla scenes (leaving empty SceneObj husks), re-breaking refs a
    # prior wave had pointed AT them. Repoint at scenes that survived 1.4.7. Both stalled a
    # land load: scene_name=battania_village_c (Nan Angren land_raid hang) and
    # scene_name_1=sturgia_castle_keep_a_l1_interior (36 Dale/Sturgia L1 lord's-halls). (2026-07-17)
    "battania_village_c": "battania_village_e",
    "sturgia_castle_keep_a_l1_interior": "sturgia_castle_keep_a_l2_interior",
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
    # Only require a replacement scene to exist for entries that ACTUALLY match the
    # live file. A no-op entry (its old ref already removed by a prior run / later
    # rename) must not abort the whole run just because its now-unused replacement
    # value was itself renamed away. (e.g. the Osgiliath typo entry: old ref long
    # gone from the file; its replacement scene was deleted in a later wave.)
    live_check_text = LIVE.read_text(encoding="utf-8-sig", errors="replace") if LIVE.exists() else ""

    def _matches_live(old: str) -> bool:
        return re.search(r'scene_name(?:_\d+)?="' + re.escape(old) + r'"', live_check_text) is not None

    active = {old: new for old, new in REMAP.items() if _matches_live(old)}
    bad = [new for new in set(active.values()) if new.lower() not in folders]
    if bad:
        print("ABORT — replacement scene(s) for matched remaps not found on disk:")
        for b in bad:
            print(f"  {b}")
        return 1
    print(f"{len(active)} of {len(REMAP)} remaps match the live file; "
          f"all {len(set(active.values()))} of their replacement scenes exist on disk.\n")

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
            # Match every level slot: scene_name="X" AND scene_name_1/_2/_3="X".
            # Town-center Locations repeat the scene across all four; a bare
            # scene_name="X" match would fix only the first slot and leave the
            # crash live in the others. Preserve the slot suffix on replace.
            pat = re.compile(r'(scene_name(?:_\d+)?=")' + re.escape(old) + r'"')
            n = len(pat.findall(text))
            if n:
                text = pat.sub(lambda m: m.group(1) + new + '"', text)
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
