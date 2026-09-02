#!/usr/bin/env python3
"""Validate every sk_*/clo_*/urukscout_* armor reference across all troop XML files
against the LOTRLOME_Armory item ids.

This is the multi-culture generalization of `validate_gondor_refs.py`. It is the
gate against the underwear bug for the full TAOM faction lineup.

Out of scope: weapons, arrows, mounts, harnesses — those live in other modules
(LOTRAOM_weapons, Native).

Usage:
    python tools/validate_all_troop_refs.py
"""
import re
import os
import sys
import glob
from _gamedir import ensure_exists, game_dir

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
ARMORY_ROOT = os.path.join(
    game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"),
    "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items",
)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TROOPS_DIR = os.path.join(REPO_ROOT, "Main", "_Module", "ModuleData", "troops")

# Only these prefixes are TAOM-owned armor items. Non-armor refs (weapons,
# arrows, mounts) are intentionally excluded — they belong to other modules.
# Includes ar_* (Arnor-style heroic gear), sk_* (KEYforce systematic naming),
# and the two Uruk Scout cloth-overlay prefixes.
# `sm_` was missing until 2026-09-01 and that was a real blind spot, not a
# nicety: the entire Black Numenorean Body, Shoulder and Leg range is named
# `sm_md_num_*`, so when Umbar was dressed from it this gate saw 4 of its ids
# and reported PASS on a kit it had barely looked at. `harad*` and `haradrim*`
# were invisible for the same reason.
ARMOR_PREFIX_RE = re.compile(
    r"^(sk_[a-z]+_|sm_[a-z]+_|ar_[a-z]+_|harad|clo_urukscout_|urukscout_)")


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
    # `<NPCCharacter` also matches the `<NPCCharacters>` container, so the bare
    # count was one too high for every culture, every run, since this tool was
    # written. The word boundary excludes the plural.
    troops = len(re.findall(r"<NPCCharacter\b(?!s)", text))
    refs = set(re.findall(r"Item\.([a-zA-Z][a-zA-Z0-9_]+)", text))
    armor_refs = {r for r in refs if ARMOR_PREFIX_RE.match(r)}
    missing = sorted(r for r in armor_refs if r not in armory_ids)
    status = "PASS" if not missing else f"FAIL ({len(missing)} missing)"
    print(f"  {culture:<14} troops={troops:<4} armor_refs={len(armor_refs):<4} missing={len(missing):<3} {status}")
    for m in missing:
        print(f"      - {m}")
    return len(missing)


def main():
    # An absent Armory root collects zero ids, so every armor ref in every
    # culture is reported missing and the run exits 1 — "1488 armor refs do not
    # resolve" against a true count of 0. This is the underwear-bug gate, so a
    # fabricated failure here is as costly as a missed one.
    ensure_exists(ARMORY_ROOT, what="the LOTRLOME_Armory item folder")

    cultures = [
        "gondor", "mordor", "isengard", "dolguldur",
        "gundabad", "erebor", "rhun_new", "dale",
        # Promoted 2026-08-10 out of a borrowed culture: bluecraig off goblin, lindon off rivendell.
        # A new culture must be appended here or its troop file is never swept for broken item refs
        # — the "underwear bug" gate (docs/ai-includes/new-culture-authoring.md Phase 4).
        #
        # bluecraig has no row: its troop file was a duplicate of goblin's and was retired, so the
        # culture now fields troops_goblin.xml. NOTE the old wording here claimed it was therefore
        # "swept under goblin" -- it is not. `goblin` is absent from this list and validate_culture
        # resolves troops_{culture}.xml by exact name, so troops_goblin.xml is never opened. Six
        # files go unswept: dunland, goblin, harad, mirkwood, rivendell, rohan. The schema check
        # MISSING_BODY_ARMOUR reads all 16 automatically; this hardcoded list is the liability.
        "lindon",
        # Added 2026-09-01. Umbar had never been swept, which is part of why its
        # troops sat in Gondor hand-me-downs and vanilla Calradian rags unnoticed.
        "umbar",
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
    # Say the scope again at the point where someone reads a green run and concludes
    # "safe to commit". The docstring already limits this tool to armor and line 55
    # filters through ARMOR_PREFIX_RE, but on 2026-08-28 two sessions in one exchange
    # gated a WEAPON change (five wm_rohan_spear_* id swaps) on this tool passing.
    # A scope note nobody reads is not a scope note.
    print("       Scope: ARMOR ids only. Weapons, arrows, mounts and harnesses are NOT")
    print("       checked here. For those run: python tools/audit_item_refs.py")


if __name__ == "__main__":
    main()
