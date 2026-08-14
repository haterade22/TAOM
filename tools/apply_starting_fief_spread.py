#!/usr/bin/env python3
r"""Spread starting fief ownership across a kingdom's clans (#458).

Three kingdoms opened every campaign with ONE clan holding every town and castle:
Lasgalen (7 of 7), Imladris (5 of 5) and Lothlorien (4 of 4). That is not an election
outcome, it is authored starting state, so no amount of fief-grant rebalancing touches
it. This tool reassigns the `owner` attribute on the affected fortifications.

The assignments are an explicit curated table rather than an algorithm. They are a
one-time lore judgement (the king keeps his seat, lesser houses hold the marches), and
a general "balance the fiefs" routine would invent a knob nobody will ever tune while
producing worse placements. Reproducibility is what matters here, and the table gives
that.

Why a tool at all, for what is a handful of attribute edits: the target file lives in
`Modules/TAOM_Map/`, which is NOT tracked by this repo. A module reinstall silently
reverts the edit, and "untracked here" is not "unfixed" (see CLAUDE.md, Traps). Running
`--check` after any TAOM_Map update tells you whether the spread survived.

Two other things worth knowing before running this:

  * Villages are untouched on purpose. Not one of the 607 villages in the live file
    carries an explicit `owner`; each follows its bound fortification, and
    `Settlement.OwnerClan` hops village -> bound town in the engine.

  * Settlement ownership IS engine-saved, unlike `Settlement.Culture`. So this lands on
    NEW CAMPAIGNS ONLY. An existing save keeps whatever its own state says.

Usage:
    python tools/apply_starting_fief_spread.py            # check only, exits 1 on drift
    python tools/apply_starting_fief_spread.py --apply    # rewrite, keeping a .bak
"""
import argparse
import collections
import os
import re
import shutil
import sys
from pathlib import Path

from _gamedir import game_modules

DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

# The live settlements.xml ships with a UTF-8 BOM. tools/README.md's XML I/O convention requires
# detecting and re-emitting it explicitly rather than trusting a codec round-trip to be symmetric.
BOM = b"\xef\xbb\xbf"

# fortification id -> clan that should own it at campaign start.
# Only the reassignments are listed; anything already correct is absent.
ASSIGNMENTS = {
    # --- Lasgalen: 6 clans, 2 towns + 5 castles. The Elvenking keeps Felegoth and the
    # castle nearest it; the five remaining holds go one apiece to the other houses.
    "town_M2": "clan_mirkwood_2",
    "castle_M2": "clan_mirkwood_4",
    "castle_M3": "clan_mirkwood_3",
    "castle_M4": "clan_mirkwood_6",
    "castle_M5": "clan_mirkwood_5",
    # --- Imladris: 3 clans, 1 town + 4 castles. Elrond's house keeps Rivendell itself
    # plus one outpost; the second house takes two marches, the third the last.
    "castle_R3": "clan_rivendell_2",
    "castle_R4": "clan_rivendell_2",
    "castle_R5": "clan_rivendell_3",
    # --- Lothlorien: 3 clans, 1 town + 3 castles. Caras Galadhon and Cerin Amroth stay
    # with the ruling house (Cerin Amroth is the Lady's own hill); the other two split.
    "castle_L2": "clan_lothlorien_2",
    "castle_L3": "clan_lothlorien_3",
}

# `<Settlement id="x" ... owner="Faction.y"` — attribute order is stable in this file and
# the owner always follows the id on the same element. Matching the element start rather
# than parsing and re-serialising keeps the diff to the attributes actually changed; the
# file is 1.1 MB of hand-authored XML with formatting worth preserving.
SETTLEMENT_RE = re.compile(
    r'(<Settlement\s+id="(?P<id>[^"]+)"(?P<mid>[^>]*?)owner="Faction\.(?P<owner>[^"]+)")'
)


def target_path():
    modules = game_modules(DEFAULT_GAME_DIR)
    return Path(modules) / "TAOM_Map" / "ModuleData" / "settlements.xml"


def scan(text):
    """Current owner of every fortification named in ASSIGNMENTS."""
    found = {}
    for match in SETTLEMENT_RE.finditer(text):
        sid = match.group("id")
        if sid in ASSIGNMENTS:
            found[sid] = match.group("owner")
    return found


def rewrite(text):
    changed = {}

    def replace(match):
        sid = match.group("id")
        want = ASSIGNMENTS.get(sid)
        if want is None or match.group("owner") == want:
            return match.group(0)
        changed[sid] = (match.group("owner"), want)
        return (
            f'<Settlement id="{sid}"{match.group("mid")}owner="Faction.{want}"'
        )

    return SETTLEMENT_RE.sub(replace, text), changed


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true",
                        help="rewrite the live file (a .bak copy is kept)")
    args = parser.parse_args()

    path = target_path()
    if not path.exists():
        # Not a failure: CI and any machine without the map module installed lands here.
        # An install path is the caller's to fix (.claude/rules/environment-failures.md).
        print(f"SKIP: {path} not found. Set BANNERLORD_GAME_DIR if your install is elsewhere.")
        return 0

    # Byte-level I/O, per the MANDATORY convention in tools/README.md. This file carries a UTF-8
    # BOM and is uniformly CRLF, and `read_text`/`write_text` would round-trip both only by
    # coincidence: plain "utf-8" decodes the BOM to a literal U+FEFF instead of stripping it, and
    # text-mode write translates "\n" to os.linesep. Both happen to be symmetric here on Windows,
    # which is precisely the accident that turns a 10-attribute edit into a whole-file rewrite the
    # first time someone runs this under WSL or points it at one of the doubled-CR language files.
    raw = path.read_bytes()
    had_bom = raw.startswith(BOM)
    text = raw.decode("utf-8-sig")
    found = scan(text)

    missing = sorted(set(ASSIGNMENTS) - set(found))
    if missing:
        print(f"FAIL: {len(missing)} fortification(s) in the table are not in the map file: "
              f"{', '.join(missing)}")
        print("      The map module changed. Re-derive the table before applying.")
        return 2

    drift = {sid: (found[sid], want) for sid, want in ASSIGNMENTS.items() if found[sid] != want}

    if not drift:
        print(f"OK: all {len(ASSIGNMENTS)} reassignments already in place.")
        return 0

    for sid, (have, want) in sorted(drift.items()):
        print(f"  {sid:14} {have:20} -> {want}")

    if not args.apply:
        print(f"\n{len(drift)} fortification(s) need reassigning. Re-run with --apply.")
        print("If TAOM_Map was just reinstalled, this is the expected revert (CLAUDE.md, Traps).")
        return 1

    backup = path.with_suffix(path.suffix + ".bak")
    shutil.copy2(path, backup)
    updated, changed = rewrite(text)

    # Atomic replace, not a truncating write. `write_bytes` opens the live file with O_TRUNC, so a
    # kill, a full disk or a power loss partway through leaves Bannerlord with a half-written
    # settlements.xml and TAOM_Map failing to load. Writing a sibling temp file, flushing it to the
    # platter, then handing it over with os.replace means the destination is either the old file or
    # the new one and never a fragment. Same directory so the replace stays on one volume.
    payload = (BOM if had_bom else b"") + updated.encode("utf-8")
    tmp = path.with_suffix(path.suffix + ".tmp")
    with open(tmp, "wb") as handle:
        handle.write(payload)
        handle.flush()
        os.fsync(handle.fileno())
    os.replace(tmp, path)

    print(f"\nAPPLIED {len(changed)} reassignment(s). Backup: {backup}")

    per_clan = collections.Counter(want for _, want in changed.values())
    for clan, count in sorted(per_clan.items()):
        print(f"  {clan:24} +{count}")
    # ASCII only: this goes to a Windows console, which is cp1252 by default.
    print("\nNew campaigns only. Settlement ownership is engine-saved, so existing saves keep theirs.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
