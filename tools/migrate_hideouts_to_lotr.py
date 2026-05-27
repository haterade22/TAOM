#!/usr/bin/env python3
"""
Migrate the 99 vanilla-bandit hideouts in TAOM_Map/settlements.xml to LOTR-themed
bandit cultures. Two-attribute swap per hideout:

    name="{=Settlements.Settlement.name.hideout_forest_1}Hideout"
                                                          ^^^^^^^
                                                          rewritten

    culture="Culture.forest_bandits"
                     ^^^^^^^^^^^^^^^
                     swapped to new LOTR bandit culture id

Per memory feedback_bulk_xml_rename_via_python.md: regex anchored on the unique
settlement id keeps surgical edits safe (no risk of bleeding into other Settlements
or unrelated XML). UTF-8/CRLF/BOM preserved.

12 loc_settlements.xml files (one per language) get the same default-text update so
EVERY locale sees the LOTR name; localized translation is a separate future pass.

Usage:
    python migrate_hideouts_to_lotr.py --dry-run     # show diff
    python migrate_hideouts_to_lotr.py --apply       # write changes

Default base: $BANNERLORD_GAME_DIR/Modules/TAOM_Map -- override via --tm-path.
"""

from __future__ import annotations
import argparse
import os
import re
import sys
from pathlib import Path

# Vanilla bandit culture -> (new LOTR culture id, name template)
# Name template per user spec 2026-05-27: <Faction> Raider's Camp (or Corsair's Cove for Umbar).
CULTURE_MAP = {
    "forest_bandits":   ("dunland_raiders",  "Dunlending Raider's Camp"),
    "mountain_bandits": ("gundabad_raiders", "Gundabad Orc Raider's Camp"),
    "desert_bandits":   ("harad_raiders",    "Haradrim Raider's Camp"),
    "steppe_bandits":   ("rhun_raiders",     "Rhûn Raider's Camp"),
    "sea_raiders":      ("umbar_corsairs",   "Corsair's Cove"),
}

# id="hideout_X_N" type="Hideout" ... culture="Culture.Y"
# Strict pattern: must have type="Hideout" between id and culture to avoid editing
# any non-hideout settlements that happen to share an "hideout_*" id prefix.
HIDEOUT_LINE_RE = re.compile(
    r'(<Settlement\s+id="(hideout_[a-z0-9_]+)"\s+name="\{=Settlements\.Settlement\.name\.\2\})'
    r'([^"]*)("[^>]*?type="Hideout"[^>]*?culture="Culture\.)([a-z_]+)(")'
)

LOC_STRING_RE = re.compile(
    r'(<string\s+id="Settlements\.Settlement\.name\.hideout_[a-z0-9_]+"\s+text=")([^"]*)(")'
)


def rewrite_master_xml(content: str) -> tuple[str, int]:
    """Rewrite the master settlements.xml. Returns (new_content, hideouts_modified)."""
    modified = 0

    def repl(m: re.Match) -> str:
        nonlocal modified
        prefix_with_name_key = m.group(1)
        # group(2) = settlement id (used for grouping)
        # group(3) = original default text (e.g., "Hideout")
        mid_attrs = m.group(4)
        old_culture = m.group(5)
        culture_close = m.group(6)
        if old_culture not in CULTURE_MAP:
            return m.group(0)
        new_culture, new_name = CULTURE_MAP[old_culture]
        modified += 1
        # default-text is rewritten to the new LOTR camp name
        return f"{prefix_with_name_key}{new_name}{mid_attrs}{new_culture}{culture_close}"

    new_content = HIDEOUT_LINE_RE.sub(repl, content)
    return new_content, modified


def lookup_hideout_culture_from_master(master_content: str) -> dict[str, str]:
    """Parse master XML to build hideout_id -> old_culture map. Used to drive the loc-file
    rewrite: each loc string keyed by hideout_id should match the new culture's name."""
    mapping = {}
    # Match Settlement tag with hideout id, type="Hideout", culture="Culture.X"
    pat = re.compile(
        r'<Settlement\s+id="(hideout_[a-z0-9_]+)"[^>]*?type="Hideout"[^>]*?culture="Culture\.([a-z_]+)"'
    )
    for m in pat.finditer(master_content):
        mapping[m.group(1)] = m.group(2)
    return mapping


def rewrite_loc_xml(content: str, hideout_culture_map: dict[str, str]) -> tuple[str, int]:
    """For each <string id="Settlements.Settlement.name.hideout_X"> in the loc file,
    look up hideout_X's culture from the (post-migration) master and set text to the
    matching LOTR camp name."""
    modified = 0

    def repl(m: re.Match) -> str:
        nonlocal modified
        prefix = m.group(1)
        old_text = m.group(2)
        close = m.group(3)
        # extract hideout id from prefix
        id_match = re.search(r'hideout_[a-z0-9_]+', prefix)
        if not id_match:
            return m.group(0)
        hideout_id = id_match.group(0)
        old_culture = hideout_culture_map.get(hideout_id)
        if old_culture is None:
            return m.group(0)
        # The master mapping post-migration uses NEW culture ids (the rewrite already ran).
        # Reverse lookup: find which LOTR culture matches old_culture (== new_culture in post-state).
        new_name = None
        for src_culture, (new_culture_id, name) in CULTURE_MAP.items():
            if new_culture_id == old_culture:
                new_name = name
                break
        if new_name is None or old_text == new_name:
            return m.group(0)
        modified += 1
        return f"{prefix}{new_name}{close}"

    new_content = LOC_STRING_RE.sub(repl, content)
    return new_content, modified


def find_taom_map_dir(arg_path: str | None) -> Path:
    if arg_path:
        p = Path(arg_path)
        if not p.is_dir():
            sys.exit(f"ERROR: --tm-path {p} is not a directory")
        return p
    game_dir = os.environ.get("BANNERLORD_GAME_DIR")
    if not game_dir:
        # Fall back to canonical TAOM dev install path
        game_dir = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    p = Path(game_dir) / "Modules" / "TAOM_Map"
    if not p.is_dir():
        sys.exit(
            f"ERROR: TAOM_Map module not found at {p}.\n"
            f"Set BANNERLORD_GAME_DIR or pass --tm-path."
        )
    return p


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="Show what would change without writing")
    ap.add_argument("--apply", action="store_true", help="Write changes to disk")
    ap.add_argument("--backup", action="store_true", help="Write a .bak copy of each modified file before overwriting (recommended for first apply)")
    ap.add_argument("--tm-path", default=None, help="Override TAOM_Map module path")
    args = ap.parse_args()

    if not (args.dry_run or args.apply):
        ap.error("Pass --dry-run or --apply")

    tm_dir = find_taom_map_dir(args.tm_path)
    master = tm_dir / "ModuleData" / "settlements.xml"
    loc_dir = tm_dir / "ModuleData" / "Languages"

    if not master.exists():
        sys.exit(f"ERROR: {master} not found")
    if not loc_dir.is_dir():
        sys.exit(f"ERROR: {loc_dir} not found")

    # Read master
    master_text = master.read_text(encoding="utf-8")
    new_master_text, master_modified = rewrite_master_xml(master_text)

    if args.apply and args.backup and master_modified > 0:
        bak = master.with_suffix(master.suffix + ".bak")
        bak.write_text(master_text, encoding="utf-8")

    if args.apply and master_modified > 0:
        master.write_text(new_master_text, encoding="utf-8")

    # Build hideout -> new-culture map from post-rewrite master so loc files match
    post_master_map = lookup_hideout_culture_from_master(new_master_text)

    # Process loc files
    loc_results: list[tuple[Path, int]] = []
    for lang_dir in sorted(loc_dir.iterdir()):
        if not lang_dir.is_dir():
            continue
        loc_file = lang_dir / "loc_settlements.xml"
        if not loc_file.exists():
            continue
        loc_text = loc_file.read_text(encoding="utf-8")
        new_loc_text, loc_modified = rewrite_loc_xml(loc_text, post_master_map)
        loc_results.append((loc_file, loc_modified))
        if args.apply and loc_modified > 0:
            if args.backup:
                bak = loc_file.with_suffix(loc_file.suffix + ".bak")
                bak.write_text(loc_text, encoding="utf-8")
            loc_file.write_text(new_loc_text, encoding="utf-8")

    # Summary
    print(f"Master settlements.xml: {master_modified} hideouts modified")
    for loc_file, n in loc_results:
        lang = loc_file.parent.name
        print(f"  loc/{lang}/loc_settlements.xml: {n} strings modified")

    if args.dry_run:
        print()
        print("[DRY RUN] No files written. Re-run with --apply to commit changes.")
        # Show preview of first 3 master changes
        diff_count = 0
        for old_line, new_line in zip(master_text.splitlines(), new_master_text.splitlines()):
            if old_line != new_line and diff_count < 5:
                print(f"  - OLD: {old_line.strip()}")
                print(f"  + NEW: {new_line.strip()}")
                diff_count += 1
        return 0

    print()
    print(f"[APPLY] Wrote master + {sum(1 for _, n in loc_results if n > 0)} loc files.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
