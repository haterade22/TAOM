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
    python remap_stale_scene_names.py --apply [--shadow]

--apply refuses while the game or the Modding Kit runs, writes a timestamped
<name>.bak_scenes_YYYYMMDD-HHMMSS beside each file it changes, keeps its BOM and line endings, and
refuses a result that no longer parses as XML. --backup is accepted and ignored.
"""
from __future__ import annotations
import argparse
import re
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

from _gamedir import ensure_exists, game_dir, game_or_kit_running

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
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
    # 1.5.x prison breaks need the *_dungeon_stealth scenes (stealth_agent patrol points and
    # prison_break_reinforcement_point spawns). Orthanc and Orthanc Gate still named the pre-stealth
    # empire_dungeon_a, the only two prisons of 280 that did. Gate: PRISON_SCENE_NOT_STEALTH. (2026-11-13)
    "empire_dungeon_a": "empire_dungeon_stealth",
}


def scene_folders_lower() -> set[str]:
    out = set()
    for so in MODULES.glob("*/SceneObj"):
        for c in so.iterdir():
            if c.is_dir():
                out.add(c.name.lower())
    return out


def _pattern(old: str) -> re.Pattern:
    """Every level slot: scene_name="X" AND scene_name_1/_2/_3="X". Town-center Locations repeat the
    scene across all four; a bare scene_name="X" match would fix only the first slot. The old name
    matches case-insensitively, as validate_moduledata.prison_scene_issues looks scenes up by
    name.lower(), so a row that gate reports is a row this tool fixes."""
    return re.compile(r'(scene_name(?:_\d+)?=")(?i:' + re.escape(old) + r')"')


def read_xml_bytes(path: Path) -> tuple[bytes, bool, str]:
    """(raw bytes, had BOM, text). The decode is strict: a bad byte raises UnicodeDecodeError
    instead of becoming U+FFFD. utf-8-sig keeps CRLF, unlike read_text."""
    raw = path.read_bytes()
    return raw, raw.startswith(b"\xef\xbb\xbf"), raw.decode("utf-8-sig")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--backup", action="store_true",
                    help="accepted and ignored: --apply always writes a timestamped backup first")
    ap.add_argument("--shadow", action="store_true", help="also remap the repo shadow settlements.xml")
    args = ap.parse_args()
    if not (args.dry_run or args.apply):
        ap.error("pass --dry-run or --apply")
    if args.dry_run and args.apply:
        ap.error("--dry-run and --apply together would be ambiguous: pass one")

    # A wrong root globs no SceneObj folders and matches no remaps, so the run ends on
    # "0 of N remaps match the live file" and exits 0, which reads as "already remapped"
    # rather than "looked in the wrong place".
    ensure_exists(MODULES, what="the Bannerlord Modules folder")

    folders = scene_folders_lower()
    # Only require a replacement scene to exist for entries that ACTUALLY match the
    # live file. A no-op entry (its old ref already removed by a prior run / later
    # rename) must not abort the whole run just because its now-unused replacement
    # value was itself renamed away. (e.g. the Osgiliath typo entry: old ref long
    # gone from the file; its replacement scene was deleted in a later wave.)
    try:
        live_check_text = read_xml_bytes(LIVE)[2] if LIVE.exists() else ""
    except UnicodeDecodeError as exc:
        print(f"ABORT: {LIVE} is not valid UTF-8 ({exc}); nothing was read or written.")
        return 1

    active = {old: new for old, new in REMAP.items() if _pattern(old).search(live_check_text)}
    bad = [new for new in set(active.values()) if new.lower() not in folders]
    if bad:
        print("ABORT: replacement scene(s) for matched remaps not found on disk:")
        for b in bad:
            print(f"  {b}")
        return 1
    print(f"{len(active)} of {len(REMAP)} remaps match the live file; "
          f"all {len(set(active.values()))} of their replacement scenes exist on disk.\n")

    if args.apply and game_or_kit_running():
        print("ABORT: Bannerlord or the Modding Kit is running (or the process list could not be read); "
              "close it and run again. Nothing was written.")
        return 1

    targets = [LIVE]
    if args.shadow:
        targets.append(SHADOW)

    # Compute and parse-check every output before any file is written.
    pending = []  # (path, raw bytes, new bytes)
    for path in targets:
        if not path.exists():
            print(f"skip (missing): {path}")
            continue
        try:
            raw, had_bom, text = read_xml_bytes(path)
        except UnicodeDecodeError as exc:
            print(f"ABORT: {path} is not valid UTF-8 ({exc}); nothing was written.")
            return 1
        total = 0
        print(f"== {path} ==")
        for old, new in REMAP.items():
            pat = _pattern(old)
            n = len(pat.findall(text))
            if n:
                text = pat.sub(lambda m: m.group(1) + new + '"', text)
                total += n
                print(f"  {old} -> {new}  ({n})")
        print(f"  total replacements: {total}")
        if not total:
            continue
        out = (b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8")
        try:
            ET.fromstring(out)
        except ET.ParseError as exc:
            print(f"ABORT: the remapped {path.name} would not parse as XML ({exc}); nothing was written.")
            return 1
        pending.append((path, raw, out))

    if args.apply:
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        for path, raw, out in pending:
            # The suffix comes last: the engine globs *.xml, and tools/sweep_module_backups.ps1
            # matches .bak_<anything>.
            backup = path.with_name(f"{path.name}.bak_scenes_{stamp}")
            if backup.exists():
                print(f"ABORT: backup {backup} already exists; wait a second and run again.")
                return 1
            backup.write_bytes(raw)
            print(f"  backup: {backup}")
            path.write_bytes(out)
            print(f"  [APPLIED] {path}")
    else:
        print("  [DRY RUN]")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
