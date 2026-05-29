#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Distribute Rhûn (RU-region) settlement ownership across the Khuzait/Rhûn clans
in the LIVE TAOM_Map module, replacing the single-clan monopoly
(every RU town/castle owned by Faction.clan_khuzait_1).

Targets ONLY the LIVE engine-loaded file:
    E:\\Steam\\...\\Modules\\TAOM_Map\\ModuleData\\settlements.xml

Do NOT confuse with the TAOM repo's Main/_Module/ModuleData/settlements.xml, which is a
stale shadow last touched 2026-04-06 and is NOT registered in SubModule.xml.
(See docs/reference/taom-map-settlement-naming.md + memory feedback_taom_map_live_vs_stale_shadow.md.)

Lore-mapping: Rhûn == the Khuzait kingdom (Kingdom.khuzait, Culture.khuzait). "Rank" == clan `tier`.

Distribution rules (per user request):
  - Lest (town_RU2) + Mistrand (town_RU1) stay with Clan 1 (clan_khuzait_1).
  - Towns   -> clans of tier >= 4  (only clans 1, 2, 3, 6 qualify), 2 each.
  - Castles -> clans of tier <= 3, spread one-each across 12 distinct low clans.
  - Every tier>=4 clan ends with >= 1 holding.
  - castle_RU9 ("Carndûr", renamed separately via Apply-MapVillageNames.py) goes to
    Mithruntai (clan_khuzait_17) as its sole holding.

Only the 8 town + 12 castle <Settlement> `owner=` attributes change. Villages have no
`owner` attribute -- they `bound` to a parent town/castle and inherit its owner automatically.

Properties (mirrors Apply-MapVillageNames.py):
  - Idempotent (regex anchored on the unique Settlement id; re-running is a no-op once applied)
  - UTF-8 + CRLF preserved (UTF-8 round-trip via bytes)
  - Settlement IDs untouched (save compatibility preserved)
  - Asserts each id matches EXACTLY once (fails loud on 0 or >1 matches)
  - Writes a .bak backup before applying

Usage:
    python3 tools/Assign-RhunSettlementOwners.py            # dry-run: report only, no write
    python3 tools/Assign-RhunSettlementOwners.py --apply    # write (after .bak backup)
"""
import argparse
import os
import re
import sys

ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData"
SETTLEMENTS = os.path.join(ROOT, "settlements.xml")

# settlement id -> new owner faction reference
OWNERS = {
    # --- Towns (tier >= 4): clans 1 (6), 2 (4), 3 (5), 6 (4); 2 each ---
    "town_RU1": "Faction.clan_khuzait_1",   # Mistrand  -> Hûz (ruling) [user-mandated]
    "town_RU2": "Faction.clan_khuzait_1",   # Lest      -> Hûz (ruling) [user-mandated]
    "town_RU3": "Faction.clan_khuzait_2",   # Vorgavuld -> Salurian
    "town_RU4": "Faction.clan_khuzait_2",   # Ûrushban  -> Salurian
    "town_RU5": "Faction.clan_khuzait_3",   # Sârt      -> Nikathian
    "town_RU6": "Faction.clan_khuzait_3",   # Kelepar   -> Nikathian
    "town_RU7": "Faction.clan_khuzait_6",   # Khûndol   -> Khundolar
    "town_RU8": "Faction.clan_khuzait_6",   # Iôrig     -> Khundolar

    # --- Castles (tier <= 3): one each across 12 distinct low clans ---
    "castle_RU1":  "Faction.clan_khuzait_4",    # Mârdûn       -> Karmian (3)
    "castle_RU2":  "Faction.clan_khuzait_5",    # Tarlat Arlan -> Amdûrid (3)
    "castle_RU3":  "Faction.clan_khuzait_7",    # Khûsar       -> Kuzaithian (3)
    "castle_RU4":  "Faction.clan_khuzait_8",    # Samârnûl     -> Mashakian (1)
    "castle_RU5":  "Faction.clan_khuzait_9",    # Ulathar      -> Bozorganith (2)
    "castle_RU6":  "Faction.clan_khuzait_10",   # Rûartar      -> Illnoria (3)
    "castle_RU7":  "Faction.clan_khuzait_11",   # Tôrcâin      -> Shakhalian (3)
    "castle_RU8":  "Faction.clan_khuzait_12",   # Kârashûn     -> Hûz II (2)
    "castle_RU9":  "Faction.clan_khuzait_17",   # Carndûr      -> Mithruntai (3) [sole holding]
    "castle_RU10": "Faction.clan_khuzait_13",   # Nîrakh       -> Adekig (2)
    "castle_RU11": "Faction.clan_khuzait_14",   # Ulbarath     -> Cilzeron (2)
    "castle_RU12": "Faction.clan_khuzait_15",   # Chêya        -> Kalkian (2)
}


def process(text):
    """Return (new_text, per_id_report). Asserts each id matches exactly once."""
    report = []  # (sid, old_owner, new_owner, n_matches)
    for sid, new_owner in OWNERS.items():
        # Capture the current owner so we can report old -> new, and anchor on the
        # unique Settlement id. The trailing '"' after the id prevents prefix
        # collisions (id="castle_RU1" never matches id="castle_RU10").
        pat = re.compile(
            r'(<Settlement\s+id="' + re.escape(sid) + r'"[^>]*?\bowner=")([^"]*)(")'
        )
        matches = pat.findall(text)
        if len(matches) != 1:
            raise SystemExit(
                f"FATAL: id '{sid}' matched {len(matches)} times (expected 1). Aborting; no file written."
            )
        old_owner = matches[0][1]
        text = pat.sub(lambda m, v=new_owner: m.group(1) + v + m.group(3), text)
        report.append((sid, old_owner, new_owner, 1))
    return text, report


def main():
    ap = argparse.ArgumentParser(description="Distribute Rhûn settlement ownership (TAOM_Map).")
    ap.add_argument("--apply", action="store_true", help="write changes (default: dry-run report only)")
    args = ap.parse_args()

    if len(set(OWNERS)) != len(OWNERS):
        raise SystemExit("Duplicate settlement ids in OWNERS; aborting.")
    if not os.path.isfile(SETTLEMENTS):
        raise SystemExit(f"Live settlements.xml not found:\n  {SETTLEMENTS}")

    with open(SETTLEMENTS, "rb") as f:
        original_bytes = f.read()
    text = original_bytes.decode("utf-8")

    new_text, report = process(text)

    print(f"settlements.xml: {SETTLEMENTS}")
    print(f"{'settlement':<12} {'old owner':<26} -> new owner")
    print("-" * 72)
    changed = 0
    for sid, old, new, _ in report:
        flag = "" if old != new else "  (unchanged)"
        if old != new:
            changed += 1
        print(f"{sid:<12} {old:<26} -> {new}{flag}")

    # Sanity: clan_khuzait_1 should retain exactly the 2 named towns among the RU set.
    clan1 = [sid for sid, _, new, _ in report if new == "Faction.clan_khuzait_1"]
    print("-" * 72)
    print(f"{len(report)} RU town/castle owners mapped; {changed} differ from current.")
    print(f"clan_khuzait_1 retains: {sorted(clan1)} (expected: town_RU1, town_RU2)")

    if not args.apply:
        print("\nDRY-RUN ONLY — no file written. Re-run with --apply to write.")
        return

    backup = SETTLEMENTS + ".bak"
    with open(backup, "wb") as f:
        f.write(original_bytes)
    with open(SETTLEMENTS, "wb") as f:
        f.write(new_text.encode("utf-8"))
    print(f"\nAPPLIED. Backup written: {backup}")


if __name__ == "__main__":
    sys.exit(main())
