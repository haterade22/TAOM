#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Apply curated per-fief building levels to the LIVE TAOM_Map settlements.xml.

Writer half of the dump/apply pair (dump_settlement_buildings.py is the read side). Source of
truth = per-culture decision JSONs at tools/data/settlement_building_levels/<culture>.json, each:
    { "<settlement_id>": { "<building_id>": <level 0-3>, ... }, ... }
listing the full intended roster per fief (self-documenting; re-apply = 0 changes -> idempotent).

Safe-edit discipline copied from rebalance_settlement_prosperity.py, with the ONE required
difference: building ids repeat across every fief, so the write is TWO-LEVEL — anchor on the
unique <Settlement id="X"> block, then match the specific <Building id="B" level="(\\d+)" /> inside
that block. Exactly-once assertion per (settlement, building); fail loud, no partial write.
Byte-level UTF-8 round-trip (BOM + CRLF preserved); feature-named timestamped .bak so it never
clobbers the prosperity tool's settlements.xml.bak.

Validation before any write (all fatal): level in [0,3]; fortifications >= 1; building_id belongs
to the correct town/castle set (grounded in installed vanilla DefaultBuildingTypes); settlement
exists and its type matches the roster. Ids and bound= are never touched.

Building levels seed NEW campaigns only (Town.Deserialize, consumed once at creation); existing
saves are unaffected.

Usage:
    python3 tools/apply_settlement_buildings.py                     # DRY-RUN, all culture JSONs
    python3 tools/apply_settlement_buildings.py --culture gondor    # DRY-RUN, one culture
    python3 tools/apply_settlement_buildings.py --apply             # write, all cultures, .bak
Options:
    --culture X       apply only <culture>.json (default: every json in the data dir)
    --apply           write changes (default: dry-run)
    --game-dir DIR    Bannerlord install root (default: E:\\Steam\\... or $BANNERLORD_GAME_DIR)
"""
import argparse
import datetime
import glob
import json
import os
import re
import sys

import dump_settlement_buildings as dsb  # shared parse + path helpers + building sets

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data", "settlement_building_levels")


def load_decisions(culture=None):
    """Merge per-culture JSONs -> {settlement_id: {building_id: level}}. Fail loud on dup ids."""
    if not os.path.isdir(DATA_DIR):
        raise SystemExit(f"FATAL: decision dir not found:\n  {DATA_DIR}")
    if culture:
        files = [os.path.join(DATA_DIR, f"{culture}.json")]
    else:
        files = sorted(glob.glob(os.path.join(DATA_DIR, "*.json")))
    if not files:
        raise SystemExit(f"FATAL: no decision JSON found in {DATA_DIR}" + (f" for culture '{culture}'." if culture else "."))
    merged = {}
    origin = {}
    for path in files:
        if not os.path.isfile(path):
            raise SystemExit(f"FATAL: decision file missing:\n  {path}")
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        for sid, rosters in data.items():
            if sid in merged:
                raise SystemExit(f"FATAL: settlement '{sid}' defined in both {origin[sid]} and {os.path.basename(path)}.")
            merged[sid] = {str(k): int(v) for k, v in rosters.items()}
            origin[sid] = os.path.basename(path)
    return merged


def validate(decisions, fiefs_by_id):
    """Fail loud on any invalid entry BEFORE touching the file."""
    errors = []
    for sid, roster in sorted(decisions.items()):
        fief = fiefs_by_id.get(sid)
        if fief is None:
            errors.append(f"{sid}: no such town/castle in live settlements.xml")
            continue
        allowed = dsb.CASTLE_BUILDINGS if fief["is_castle"] else dsb.TOWN_BUILDINGS
        fort_id = dsb.FORT_CASTLE if fief["is_castle"] else dsb.FORT_TOWN
        for bid, level in roster.items():
            if bid not in allowed:
                kind = "castle" if fief["is_castle"] else "town"
                errors.append(f"{sid}: '{bid}' is not a valid {kind} building")
                continue
            if not (0 <= level <= 3):
                errors.append(f"{sid}: {bid} level {level} out of range 0-3")
            if bid == fort_id and level < 1:
                errors.append(f"{sid}: fortifications must be >= 1 (engine StartLevel), got {level}")
    if errors:
        raise SystemExit("FATAL: decision validation failed ({} error(s)):\n  ".format(len(errors)) + "\n  ".join(errors))


def compute_changes(decisions, fiefs_by_id):
    """-> {sid: [(building_id, old, new), ...]} for entries whose level actually changes."""
    changes = {}
    for sid, roster in decisions.items():
        cur = fiefs_by_id[sid]["buildings"]
        diffs = [(bid, cur.get(bid, 0), lvl) for bid, lvl in roster.items() if cur.get(bid, 0) != lvl]
        if diffs:
            # keep a stable, roster-order display
            order = dsb.CASTLE_BUILDINGS if fiefs_by_id[sid]["is_castle"] else dsb.TOWN_BUILDINGS
            diffs.sort(key=lambda d: order.index(d[0]) if d[0] in order else 99)
            changes[sid] = diffs
    return changes


def edit_block(text, sid, building_targets):
    """Two-level, position-safe edit of ONE settlement's building levels. Returns new text.

    Locates the unique <Settlement id="sid"> block (bounded by the next <Settlement id=" or
    </Settlements>), rewrites each <Building id="B" level="N"> inside it, splices the block back.
    Exactly-once assertion on both the block and each building line.
    """
    blockpat = re.compile(r'<Settlement\s+id="' + re.escape(sid) + r'".*?(?=<Settlement\s+id="|</Settlements>)', re.DOTALL)
    blocks = blockpat.findall(text)
    if len(blocks) != 1:
        raise SystemExit(f"FATAL: settlement id '{sid}' matched {len(blocks)} blocks (expected 1). Aborting; no file written.")
    m = blockpat.search(text)
    block = m.group(0)
    for bid, level in building_targets:
        bpat = re.compile(r'(<Building\s+id="' + re.escape(bid) + r'"\s+level=")(\d+)(")')
        if len(bpat.findall(block)) != 1:
            raise SystemExit(f"FATAL: {sid}/{bid} matched {len(bpat.findall(block))} times (expected 1). Aborting; no file written.")
        block = bpat.sub(lambda mm, v=str(level): mm.group(1) + v + mm.group(3), block, count=1)
    return text[:m.start()] + block + text[m.end():]


def main():
    ap = argparse.ArgumentParser(description="Apply curated per-fief building levels to LIVE TAOM_Map settlements.xml.")
    ap.add_argument("--culture", default=None, help="apply only <culture>.json (default: all)")
    ap.add_argument("--apply", action="store_true", help="write changes (default: dry-run)")
    ap.add_argument("--game-dir", default=None)
    args = ap.parse_args()

    live = dsb.live_settlements_path(args.game_dir)
    fiefs = dsb.parse_fiefs(live)
    fiefs_by_id = {f["id"]: f for f in fiefs}

    decisions = load_decisions(args.culture)
    validate(decisions, fiefs_by_id)
    changes = compute_changes(decisions, fiefs_by_id)

    # Report
    total = 0
    for sid in sorted(changes):
        fief = fiefs_by_id[sid]
        kind = "castle" if fief["is_castle"] else "town"
        print(f"\n{fief['name']} ({sid}, {kind}, pros {fief['prosperity']})")
        for bid, old, new in changes[sid]:
            print(f"  {dsb.short(bid):<16} {old} -> {new}")
            total += 1
    print(f"\n{len(decisions)} fiefs specified; {len(changes)} fief(s) changed; {total} building level(s) altered.")

    if not changes:
        print("Nothing to change (idempotent — file already matches the decision JSONs).")
        return

    if not args.apply:
        print("\nDRY-RUN ONLY -- no file written. Re-run with --apply to write.")
        return

    with open(live, "rb") as f:
        original = f.read()
    text = original.decode("utf-8")
    for sid in sorted(changes):
        text = edit_block(text, sid, [(bid, new) for bid, _old, new in changes[sid]])

    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    bak = f"{live}.bak-buildings-{stamp}"
    with open(bak, "wb") as f:
        f.write(original)
    with open(live, "wb") as f:
        f.write(text.encode("utf-8"))
    print(f"\nAPPLIED. Backup written: {bak}")


if __name__ == "__main__":
    sys.exit(main())
