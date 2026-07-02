#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Starting-prosperity rebaseline for the LIVE TAOM_Map module (#317 companion, data side).

TAOM's generated regions ship flat generator defaults — 89 castles at exactly 600, 31 towns at
exactly 3500 — while vanilla-derived regions carry varied, hand-tuned values. Starting prosperity
seeds the engine's town-gold equilibrium (`10000 + prosperity * 12`, DefaultSettlementEconomyModel.
GetTownGoldChange) and market demand, so the flat clusters make whole regions economically
identical. This tool maps each fief class (towns and castles separately) onto vanilla SandBox's
empirical prosperity distribution via a rank-preserving quantile map:

  - relative ordering is preserved (hand-tuned relationships survive),
  - ties inside the flat clusters break by bound-village count then total hearth (economically
    meaningful de-clustering, deterministic -> idempotent),
  - LIFT-ONLY by default: no fief is ever lowered (--allow-lower for the pure vanilla shape),
  - targets round to the data style (nearest 10) and cap at 5600 (vanilla max; the housing model
    turns prosperity growth negative above 6000).

Starting prosperity seeds NEW campaigns only — the value is live and saved thereafter; existing
saves are covered by the C# regen knob (Main/Features/SettlementEconomy/, #317).

Targets ONLY the LIVE engine-loaded file:
    E:\\Steam\\...\\Modules\\TAOM_Map\\ModuleData\\settlements.xml
(The repo's Main/_Module/ModuleData/settlements.xml is a stale shadow -- never edited here.)
Vanilla SandBox settlements.xml is a READ-ONLY comparison input. `hearth=`, bindings, owners and
every other byte are untouched.

Write discipline mirrors Assign-SettlementOwners.py: byte-level UTF-8 round-trip (BOM + CRLF
preserved), regex anchored on the unique Settlement id and confined to the <Town> tag (so scene
`max_prosperity` can never match), exactly-once assertion (fail loud), .bak backup, idempotent:
a second run after --apply WITH THE SAME FLAG SET must report 0 changes (preserved/pinned fiefs
are excluded from the ranking population and uplift is applied pre-clamp to make this hold for
every flag combination, not just the default path).

Usage:
    python3 tools/rebalance_settlement_prosperity.py                # DRY-RUN (default)
    python3 tools/rebalance_settlement_prosperity.py --apply        # write with .bak backup
Options:
    --allow-lower          pure vanilla quantile map (may reduce values); default is lift-only
    --town-uplift N        flat add to town targets after mapping (default 0), capped at 5600
    --pin-zero-village     pin fiefs with no bound villages to their class minimum (default off)
    --preserve id[,id...]  freeze specific fiefs at their current value
    --game-dir DIR         Bannerlord install root (default: E:\\Steam\\... or $BANNERLORD_GAME_DIR)
"""
import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
PROSPERITY_CAP = 5600  # vanilla max; >6000 flips the housing-growth term negative
ROUND_TO = 10

# Known zero-bound-village fiefs (verified 2026-07-02); --pin-zero-village pins ANY fief the
# parse finds with zero villages, this list is documentation.
KNOWN_ZERO_VILLAGE = ("town_EW10", "town_EW11", "castle_G4")


def game_dir(cli_value=None):
    return cli_value or os.environ.get("BANNERLORD_GAME_DIR") or DEFAULT_GAME_DIR


def live_settlements_path(cli_game_dir=None):
    return os.path.join(game_dir(cli_game_dir), "Modules", "TAOM_Map", "ModuleData", "settlements.xml")


def vanilla_settlements_path(cli_game_dir=None):
    return os.path.join(game_dir(cli_game_dir), "Modules", "SandBox", "ModuleData", "settlements.xml")


def region_prefix(settlement_id):
    m = re.match(r"(?:town|castle|village|castle_village)_([A-Za-z]+)\d", settlement_id)
    return m.group(1) if m else "other"


def parse_settlements(path):
    """Read-only parse -> list of fief/village records. ET handles BOM; never used for writing."""
    if not os.path.isfile(path):
        raise SystemExit(f"FATAL: settlements.xml not found:\n  {path}")
    root = ET.parse(path).getroot()
    records = []
    for s in root.iter("Settlement"):
        sid = s.get("id", "")
        culture = (s.get("culture") or "").replace("Culture.", "")
        town = s.find(".//Town")
        village = s.find(".//Village")
        if town is not None:
            records.append({
                "id": sid,
                "kind": "castle" if town.get("is_castle") == "true" else "town",
                "name": s.get("name", ""),
                "culture": culture,
                "region": region_prefix(sid),
                "prosperity": int(float(town.get("prosperity", "0"))),
            })
        elif village is not None:
            bound = (village.get("bound") or "").replace("Settlement.", "")
            records.append({
                "id": sid,
                "kind": "village",
                "name": s.get("name", ""),
                "culture": culture,
                "region": region_prefix(sid),
                "hearth": int(float(village.get("hearth", "0"))),
                "bound": bound,
            })
        # else: hideouts etc — no economy component, skipped
    return records


def village_index(records):
    by_fief = defaultdict(list)
    for r in records:
        if r["kind"] == "village" and r.get("bound"):
            by_fief[r["bound"]].append(r)
    return by_fief


def _quantile_map(fiefs, vanilla_values, allow_lower, uplift=0):
    """Rank-preserving per-class quantile map. `fiefs` must already carry n_villages/sum_hearth.

    `uplift` is added to the quantile target BEFORE the lift-only clamp — this keeps re-runs
    idempotent (after --apply, current >= quantile+uplift, so max(current, ...) is a no-op),
    unlike a post-hoc add which would stack on every run."""
    vv = sorted(vanilla_values)
    if not vv:
        raise SystemExit("FATAL: vanilla class value list is empty — wrong vanilla file?")
    ordered = sorted(fiefs, key=lambda f: (f["prosperity"], f["n_villages"], f["sum_hearth"], f["id"]))
    n = len(ordered)
    targets = {}
    for rank, fief in enumerate(ordered):
        pos = (rank / (n - 1)) * (len(vv) - 1) if n > 1 else (len(vv) - 1) / 2.0
        lo = int(pos)
        hi = min(lo + 1, len(vv) - 1)
        target = vv[lo] + (pos - lo) * (vv[hi] - vv[lo]) + uplift
        target = int(round(target / ROUND_TO) * ROUND_TO)
        target = min(target, PROSPERITY_CAP)
        if not allow_lower:
            target = max(fief["prosperity"], target)
        targets[fief["id"]] = min(target, PROSPERITY_CAP)
    return targets


def compute_targets(taom_records, vanilla_records, allow_lower=False, town_uplift=0,
                    pin_zero_village=False, preserve_ids=()):
    """Single source of truth for the target curve — the analyzer imports this.

    Idempotency contract: re-running with the SAME flag set after --apply reports 0 changes.
    Preserved/pinned fiefs are therefore EXCLUDED from the quantile-map ranking population
    (a frozen fief that kept its old value would otherwise shift every free fief's rank on
    the next run — unbounded drift, deep-review 2026-07-02 HIGH finding)."""
    villages = village_index(taom_records)
    by_class = {"town": [], "castle": []}
    for r in taom_records:
        if r["kind"] in by_class:
            bound = villages.get(r["id"], [])
            r = dict(r, n_villages=len(bound), sum_hearth=sum(v["hearth"] for v in bound))
            by_class[r["kind"]].append(r)

    vanilla_values = {
        "town": [r["prosperity"] for r in vanilla_records if r["kind"] == "town"],
        "castle": [r["prosperity"] for r in vanilla_records if r["kind"] == "castle"],
    }

    def frozen(r):
        return r["id"] in preserve_ids or (pin_zero_village and r["n_villages"] == 0)

    targets = {}
    for kind in ("town", "castle"):
        free = [r for r in by_class[kind] if not frozen(r)]
        uplift = town_uplift if kind == "town" else 0
        targets.update(_quantile_map(free, vanilla_values[kind], allow_lower, uplift))

    if pin_zero_village:
        floors = {k: min(vanilla_values[k]) for k in vanilla_values}
        for kind in ("town", "castle"):
            for r in by_class[kind]:
                if r["n_villages"] == 0 and r["id"] not in preserve_ids:
                    targets[r["id"]] = floors[kind]

    # Preserve wins over pin (an explicit human override beats automatic pinning).
    for kind in ("town", "castle"):
        for r in by_class[kind]:
            if r["id"] in preserve_ids:
                targets[r["id"]] = r["prosperity"]

    fiefs = by_class["town"] + by_class["castle"]
    return targets, fiefs


def apply_to_file(path, changes, do_write):
    """changes: {settlement_id: new_prosperity}. Byte round-trip, exactly-once, .bak, idempotent.

    Every key in `changes` MUST be a fief parsed WITH a <Town> element (compute_targets guarantees
    this). A <Town>-less id (e.g. a village) would let the DOTALL `.*?` leak into the NEXT
    settlement's <Town> tag and silently rewrite the wrong fief — do not reuse this function with
    externally-constructed `changes`."""
    with open(path, "rb") as f:
        original = f.read()
    text = original.decode("utf-8")

    for sid, new_value in sorted(changes.items()):
        pat = re.compile(
            r'(<Settlement\s+id="' + re.escape(sid) + r'".*?<Town\s+[^>]*?\bprosperity=")(\d+)(")',
            re.DOTALL)
        matches = pat.findall(text)
        if len(matches) != 1:
            raise SystemExit(f"FATAL: id '{sid}' matched {len(matches)} times (expected 1). Aborting; no file written.")
        text = pat.sub(lambda m, v=str(new_value): m.group(1) + v + m.group(3), text, count=1)

    if not do_write:
        print("DRY-RUN ONLY -- no file written. Re-run with --apply to write.")
        return
    with open(path + ".bak", "wb") as f:
        f.write(original)
    with open(path, "wb") as f:
        f.write(text.encode("utf-8"))
    print(f"APPLIED. Backup written: {path}.bak")


def main():
    ap = argparse.ArgumentParser(description="Rebaseline TAOM_Map starting prosperity onto vanilla's per-class distribution (#317).")
    ap.add_argument("--apply", action="store_true", help="write changes (default: dry-run)")
    ap.add_argument("--allow-lower", action="store_true", help="pure quantile map — may LOWER values (default lift-only)")
    ap.add_argument("--town-uplift", type=int, default=0, help="flat add to town targets after mapping (capped at 5600)")
    ap.add_argument("--pin-zero-village", action="store_true", help="pin fiefs with no bound villages to their class minimum")
    ap.add_argument("--preserve", default="", help="comma-separated settlement ids to freeze at current value")
    ap.add_argument("--game-dir", default=None, help="Bannerlord install root")
    args = ap.parse_args()

    live = live_settlements_path(args.game_dir)
    vanilla = vanilla_settlements_path(args.game_dir)
    preserve_ids = tuple(s.strip() for s in args.preserve.split(",") if s.strip())

    taom = parse_settlements(live)
    van = parse_settlements(vanilla)
    targets, fiefs = compute_targets(taom, van, args.allow_lower, args.town_uplift,
                                     args.pin_zero_village, preserve_ids)

    changes = {}
    by_region = defaultdict(list)
    for f in fiefs:
        target = targets[f["id"]]
        if target != f["prosperity"]:
            changes[f["id"]] = target
        by_region[f["region"]].append((f, target))

    lowered = raised = 0
    for region in sorted(by_region):
        rows = [(f, t) for f, t in by_region[region] if t != f["prosperity"]]
        if not rows:
            continue
        print(f"\n=== region {region} ({len(rows)} change(s)) ===")
        for f, t in sorted(rows, key=lambda x: (x[0]["kind"], x[0]["id"])):
            delta = t - f["prosperity"]
            raised += delta > 0
            lowered += delta < 0
            flag = " ***" if abs(delta) >= 1000 else ""
            print(f"  {f['kind']:<6} {f['id']:<22} villages={f['n_villages']} {f['prosperity']:>5} -> {t:>5} ({delta:+d}){flag}")

    print(f"\n{len(fiefs)} fiefs evaluated; {len(changes)} change ({raised} raised, {lowered} lowered).")
    zero_village = [f["id"] for f in fiefs if f["n_villages"] == 0]
    if zero_village:
        print(f"zero-bound-village fiefs ({'pinned to class floor' if args.pin_zero_village else 'flag only'}): {', '.join(sorted(zero_village))}")

    apply_to_file(live, changes, args.apply)


if __name__ == "__main__":
    sys.exit(main())
