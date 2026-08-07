#!/usr/bin/env python3
"""Validate every sk_*/clo_*/urukscout_* armor reference across all troop XML files
against the LOTRLOME_Armory item ids.

This is the multi-culture generalization of `validate_gondor_refs.py`. It is the
gate against the underwear bug for the full TAOM faction lineup.

Out of scope: weapons, arrows, mounts, harnesses — those live in other modules
(LOTRAOM_weapons, Alliance.Wargs, Native).

Usage:
    python tools/validate_all_troop_refs.py
"""
import re
import os
import sys
import glob

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
ARMORY_ROOT = os.path.join(
    os.environ.get(
        "BANNERLORD_GAME_DIR",
        r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord",
    ),
    "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items",
)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TROOPS_DIR = os.path.join(REPO_ROOT, "Main", "_Module", "ModuleData", "troops")

# Only these prefixes are TAOM-owned armor items. Non-armor refs (weapons,
# arrows, mounts) are intentionally excluded — they belong to other modules.
# Includes ar_* (Arnor-style heroic gear), sk_* (KEYforce systematic naming),
# and the two Uruk Scout cloth-overlay prefixes.
ARMOR_PREFIX_RE = re.compile(r"^(sk_[a-z]+_|ar_[a-z]+_|clo_urukscout_|urukscout_)")


def collect_armory_ids() -> set:
    ids = set()
    for p in glob.glob(os.path.join(ARMORY_ROOT, "**", "*.xml"), recursive=True):
        with open(p, "r", encoding="utf-8") as f:
            ids.update(re.findall(r'id="([a-zA-Z][^"]+)"', f.read()))
    return ids


def validate_culture(culture: str, armory_ids: set) -> int:
    """Return count of missing armor refs (0 = PASS)."""
    path = os.path.join(TROOPS_DIR, f"troops_{culture}.xml")
    if not os.path.exists(path):
        print(f"  {culture:<14} (not present)")
        return 0
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()
    troops = len(re.findall(r"<NPCCharacter", text))
    refs = set(re.findall(r"Item\.([a-zA-Z][a-zA-Z0-9_]+)", text))
    armor_refs = {r for r in refs if ARMOR_PREFIX_RE.match(r)}
    missing = sorted(r for r in armor_refs if r not in armory_ids)
    status = "PASS" if not missing else f"FAIL ({len(missing)} missing)"
    print(f"  {culture:<14} troops={troops:<4} armor_refs={len(armor_refs):<4} missing={len(missing):<3} {status}")
    for m in missing:
        print(f"      - {m}")
    return len(missing)


def main():
    cultures = [
        "gondor", "mordor", "isengard", "dolguldur",
        "gundabad", "erebor", "rhun_new", "dale",
    ]
    armory_ids = collect_armory_ids()
    print(f"Armory IDs (recursive): {len(armory_ids):,}\n")
    total_missing = 0
    for culture in cultures:
        total_missing += validate_culture(culture, armory_ids)
    print()
    if total_missing:
        print(f"FAIL: {total_missing} armor refs do not resolve.")
        sys.exit(1)
    print("PASS: all armor refs resolve across all cultures.")


if __name__ == "__main__":
    main()
