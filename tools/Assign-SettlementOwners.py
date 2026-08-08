#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Generalized, multi-region settlement-ownership distributor for the LIVE TAOM_Map module.

Applies the same "break up the single-ruling-clan monopoly" rules used for Rhûn
(tools/Assign-RhunSettlementOwners.py) to a set of regions, driven by a per-region SPEC
built from VERIFIED clan/tier/settlement data (discover+verify workflow, 2026-05-29).

Distribution rules (user-chosen conflict policy = "Land every high clan"):
  - The ruling clan keeps the region CAPITAL (its lore initial_home town).
  - Towns -> clans of tier >= 4 ("rank 4 and above"): each high clan is seated in a town
    (remaining towns by prosperity desc).
  - OVERFLOW: when there are MORE tier>=4 clans than towns, each high clan with no town
    takes ONE castle (highest-prosperity first) as its seat -> guarantees EVERY tier>=4
    clan ends with >=1 holding (the hard rule), at the cost of some castles going to high clans.
  - Castles -> remaining castles spread one-each (round-robin) across tier<=3 clans.
  - Villages are never touched (no owner attribute; they bound= to a parent and inherit).

Targets ONLY the LIVE engine-loaded file:
    E:\\Steam\\...\\Modules\\TAOM_Map\\ModuleData\\settlements.xml
(The repo's Main/_Module/ModuleData/settlements.xml is a stale shadow -- never edited here.)

Properties (mirror Apply-MapVillageNames.py / Assign-RhunSettlementOwners.py):
  - Idempotent, regex anchored on the unique Settlement id (trailing '"' prevents
    castle_X1 matching castle_X10), exactly-once match assertion (fail loud), .bak backup,
    byte-level UTF-8 round-trip (BOM + CRLF preserved).

Usage:
    python3 tools/Assign-SettlementOwners.py            # dry-run: print plan + diagnostics, no write
    python3 tools/Assign-SettlementOwners.py --apply    # write (after .bak backup)
"""
import argparse
import os
import re
import sys
from collections import Counter
from _gamedir import game_dir

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
ROOT = GAME + r"\Modules\TAOM_Map\ModuleData"
SETTLEMENTS = os.path.join(ROOT, "settlements.xml")

# ---------------------------------------------------------------------------
# SPEC: one entry per region (verified discover+verify workflow wf_9e04c205-c70).
#   "ruling_clan": clan that keeps the capital
#   "capital":     the ruling clan's lore initial_home town id (must be in "towns")
#   "clans":   { clan_id: tier }                        every super_faction=Kingdom.X clan
#   "towns":   [ (settlement_id, prosperity), ... ]     is_castle="false"
#   "castles": [ (settlement_id, prosperity), ... ]     is_castle="true"
# Owner strings are written as "Faction.<clan_id>".
# ---------------------------------------------------------------------------
SPEC = {
    "Dale": {
        "ruling_clan": "clan_sturgia_1", "capital": "town_S1",
        "clans": {"clan_sturgia_1": 6, "clan_sturgia_2": 5, "clan_sturgia_3": 4, "clan_sturgia_4": 4,
                  "clan_sturgia_5": 3, "clan_sturgia_6": 3, "clan_sturgia_7": 4, "clan_sturgia_8": 1, "clan_sturgia_9": 2},
        "towns": [("town_S1", 3100), ("town_S2", 2300), ("town_S3", 3200), ("town_S4", 3300), ("town_S5", 1700)],
        "castles": [("castle_S1", 890), ("castle_S2", 830), ("castle_S3", 470), ("castle_S4", 1000),
                    ("castle_S5", 800), ("castle_S6", 680), ("castle_S7", 660)],
    },
    "Isengard": {
        "ruling_clan": "clan_isengard_1", "capital": "town_isengard",
        "clans": {"clan_isengard_1": 6, "clan_isengard_2": 5, "clan_isengard_3": 4, "clan_isengard_4": 3,
                  "clan_isengard_5": 3, "clan_isengard_6": 3, "clan_isengard_7": 3, "clan_isengard_8": 3,
                  "clan_isengard_9": 3, "clan_isengard_10": 4, "clan_isengard_11": 3},
        "towns": [("town_isengard", 4000)],
        "castles": [("castle_I1", 600), ("castle_I2", 600), ("castle_orthanc_gate", 800)],
    },
    "Dunland": {
        "ruling_clan": "clan_empire_north_1", "capital": "town_EN2",
        "clans": {"clan_empire_north_1": 6, "clan_empire_north_2": 5, "clan_empire_north_3": 4,
                  "clan_empire_north_4": 4, "clan_empire_north_5": 3, "clan_empire_north_6": 2,
                  "clan_empire_north_7": 1, "clan_empire_north_8": 3, "clan_empire_north_9": 2},
        "towns": [("town_EN1", 5100), ("town_EN2", 4700), ("town_EN3", 4600)],
        "castles": [("castle_EN3", 720), ("castle_EN4", 860), ("castle_EN5", 560),
                    ("castle_EN6", 960), ("castle_EN7", 990), ("castle_EN8", 560)],
    },
    "Harad": {
        "ruling_clan": "clan_aserai_1", "capital": "town_A1",
        "clans": {"clan_aserai_1": 5, "clan_aserai_2": 4, "clan_aserai_3": 4, "clan_aserai_4": 4,
                  "clan_aserai_5": 4, "clan_aserai_6": 3, "clan_aserai_7": 3, "clan_aserai_8": 3, "clan_aserai_9": 4},
        "towns": [("town_A1", 4000), ("town_A2", 4200), ("town_A3", 1800), ("town_A4", 4500), ("town_A5", 3200)],
        "castles": [("castle_A1", 460), ("castle_A2", 890), ("castle_A3", 980), ("castle_A4", 750),
                    ("castle_A5", 610), ("castle_A6", 570), ("castle_A7", 550), ("castle_A8", 590),
                    ("castle_A9", 610), ("castle_A11", 600), ("castle_A12", 600)],
    },
    "Umbar": {
        "ruling_clan": "clan_umbar_1", "capital": "town_U1",
        "clans": {"clan_umbar_1": 6, "clan_umbar_2": 5, "clan_umbar_3": 4, "clan_umbar_4": 3,
                  "clan_umbar_5": 2, "clan_umbar_6": 3},
        "towns": [("town_U1", 3500), ("town_U2", 3500)],
        "castles": [("castle_U1", 600), ("castle_U2", 600), ("castle_U3", 600), ("castle_U4", 600),
                    ("castle_U5", 600), ("castle_U6", 600), ("castle_U7", 600), ("castle_U8", 600)],
    },
    "Khand": {
        "ruling_clan": "clan_battania_1", "capital": "town_K1",
        "clans": {"clan_battania_1": 5, "clan_battania_2": 4, "clan_battania_3": 4, "clan_battania_4": 4,
                  "clan_battania_5": 4, "clan_battania_6": 3, "clan_battania_7": 3, "clan_battania_8": 3},
        "towns": [("town_K1", 1800), ("town_K2", 1700), ("town_K3", 3700), ("town_K4", 3200)],
        "castles": [("castle_K1", 420), ("castle_K2", 480), ("castle_K3", 660),
                    ("castle_K5", 520), ("castle_K6", 620), ("castle_K7", 420)],
    },
    "Dol Guldur": {
        "ruling_clan": "clan_dolguldur_1", "capital": "town_DG1",
        "clans": {"clan_dolguldur_1": 6, "clan_dolguldur_2": 5, "clan_dolguldur_3": 4,
                  "clan_dolguldur_4": 5, "clan_dolguldur_5": 3, "clan_dolguldur_6": 4},
        "towns": [("town_DG1", 3500)],
        "castles": [("castle_DG1", 600), ("castle_DG2", 600), ("castle_DG3", 600),
                    ("castle_DG4", 600), ("castle_DG5", 600)],
    },
    "Gundabad": {
        "ruling_clan": "clan_gundabad_1", "capital": "town_G1",
        "clans": {"clan_gundabad_1": 6, "clan_gundabad_2": 5, "clan_gundabad_3": 4,
                  "clan_gundabad_4": 5, "clan_gundabad_5": 3},
        "towns": [("town_G1", 3500), ("town_G2", 3500)],
        "castles": [("castle_G1", 600), ("castle_G2", 600), ("castle_G3", 600),
                    ("castle_G4", 600), ("castle_G5", 600)],
    },
}


def natural_key(s):
    """Sort key that orders clan_x_2 before clan_x_10 (numeric-aware)."""
    return [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", s)]


def assign_region(name, spec):
    """Return (owners: {settlement_id: clan_id}, diagnostics: list[str]) -- 'land every high clan'."""
    clans = spec["clans"]
    ruling = spec["ruling_clan"]
    capital = spec.get("capital")
    diags = []

    high_sorted = sorted([c for c in clans if clans[c] >= 4], key=lambda c: (c != ruling, -clans[c], natural_key(c)))
    low_sorted = sorted([c for c in clans if clans[c] <= 3], key=lambda c: (-clans[c], natural_key(c)))

    town_ids = [t[0] for t in sorted(spec["towns"], key=lambda x: (-x[1], natural_key(x[0])))]
    castle_ids = [c[0] for c in sorted(spec["castles"], key=lambda x: (-x[1], natural_key(x[0])))]

    owners = {}
    seated = []

    # 1) capital -> ruling clan (fallback to highest-prosperity town if capital missing)
    cap = capital if capital in town_ids else (town_ids[0] if town_ids else None)
    if cap:
        seat_clan = ruling if ruling in high_sorted else (high_sorted[0] if high_sorted else ruling)
        if seat_clan != ruling:
            diags.append(f"ruling clan {ruling} is tier<4; capital {cap} given to top high clan {seat_clan}.")
        owners[cap] = seat_clan
        seated.append(seat_clan)

    # 2) seat remaining high clans in remaining towns (prosperity desc)
    remaining_towns = [t for t in town_ids if t not in owners]
    overflow_high = []
    ti = 0
    for clan in [c for c in high_sorted if c not in seated]:
        if ti < len(remaining_towns):
            owners[remaining_towns[ti]] = clan
            ti += 1
            seated.append(clan)
        else:
            overflow_high.append(clan)
    # leftover towns (towns > high clans): round-robin across all high clans
    for i, t in enumerate(remaining_towns[ti:]):
        owners[t] = high_sorted[i % len(high_sorted)]

    # 3) overflow high clans each take one (highest-prosperity) castle as their seat
    ci = 0
    for clan in overflow_high:
        if ci < len(castle_ids):
            owners[castle_ids[ci]] = clan
            ci += 1
        else:
            diags.append(f"RULE VIOLATION: high clan {clan} unlandable (no town and no castle left).")
    if overflow_high:
        diags.append(f"{len(overflow_high)} overflow high clan(s) seated in castles (more tier>=4 clans than towns): {overflow_high}")

    # 4) remaining castles spread one-each across low clans
    remaining_castles = castle_ids[ci:]
    if remaining_castles and low_sorted:
        for i, sid in enumerate(remaining_castles):
            owners[sid] = low_sorted[i % len(low_sorted)]
    elif remaining_castles and not low_sorted:
        for i, sid in enumerate(remaining_castles):
            owners[sid] = high_sorted[i % len(high_sorted)]
        diags.append(f"no tier<=3 clans; {len(remaining_castles)} castle(s) distributed among high clans.")

    # 5) diagnostics
    held = set(owners.values())
    landless_low = [c for c in low_sorted if c not in held]
    if landless_low:
        diags.append(f"low clans left landless (too few castles -- acceptable): {landless_low}")
    unhoused_high = [c for c in high_sorted if c not in held]
    if unhoused_high:
        diags.append(f"RULE VIOLATION: tier>=4 clans with NO holding: {unhoused_high}")

    return owners, diags


def build_owner_map():
    owner_map, all_diags, summary = {}, [], []
    for name, spec in SPEC.items():
        owners, diags = assign_region(name, spec)
        for sid, clan in owners.items():
            if sid in owner_map:
                raise SystemExit(f"FATAL: settlement {sid} assigned in two regions.")
            owner_map[sid] = f"Faction.{clan}"
        all_diags.extend(f"[{name}] {d}" for d in diags)
        summary.append((name, spec, owners, Counter(owners.values())))
    return owner_map, all_diags, summary


def apply_to_file(owner_map, do_write):
    with open(SETTLEMENTS, "rb") as f:
        original = f.read()
    text = original.decode("utf-8")

    changed = 0
    for sid, new_owner in owner_map.items():
        pat = re.compile(r'(<Settlement\s+id="' + re.escape(sid) + r'"[^>]*?\bowner=")([^"]*)(")')
        matches = pat.findall(text)
        if len(matches) != 1:
            raise SystemExit(f"FATAL: id '{sid}' matched {len(matches)} times (expected 1). Aborting; no file written.")
        if matches[0][1] != new_owner:
            changed += 1
        text = pat.sub(lambda m, v=new_owner: m.group(1) + v + m.group(3), text)

    print(f"\n{len(owner_map)} settlement owners mapped; {changed} differ from current.")
    if not do_write:
        print("DRY-RUN ONLY -- no file written. Re-run with --apply to write.")
        return
    with open(SETTLEMENTS + ".bak", "wb") as f:
        f.write(original)
    with open(SETTLEMENTS, "wb") as f:
        f.write(text.encode("utf-8"))
    print(f"APPLIED. Backup written: {SETTLEMENTS}.bak")


def main():
    ap = argparse.ArgumentParser(description="Distribute settlement ownership across clans (TAOM_Map).")
    ap.add_argument("--apply", action="store_true", help="write changes (default: dry-run)")
    args = ap.parse_args()

    if not SPEC:
        raise SystemExit("SPEC is empty.")
    if not os.path.isfile(SETTLEMENTS):
        raise SystemExit(f"Live settlements.xml not found:\n  {SETTLEMENTS}")

    owner_map, diags, summary = build_owner_map()

    for name, spec, owners, counts in summary:
        clans = spec["clans"]
        print(f"\n=== {name} (ruling {spec['ruling_clan']}, {len(owners)} fiefs) ===")
        town_ids = {t[0] for t in spec["towns"]}
        for sid in sorted(owners, key=natural_key):
            clan = owners[sid]
            kind = "town  " if sid in town_ids else "castle"
            print(f"  {kind} {sid:<20} -> {clan} (t{clans.get(clan,'?')})")
        landed = set(owners.values())
        print(f"  clans landed: {len(landed)}/{len(clans)}; holdings/clan: "
              + ", ".join(f"{c}={n}" for c, n in sorted(counts.items(), key=lambda x: natural_key(x[0]))))

    if diags:
        print("\n--- DIAGNOSTICS ---")
        for d in diags:
            print("  " + d)

    apply_to_file(owner_map, args.apply)


if __name__ == "__main__":
    sys.exit(main())
