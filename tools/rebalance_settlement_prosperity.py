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
  - targets round to the data style (nearest 10) and cap at 5600 (vanilla max; above 6000 the
    HOUSING TERM of the prosperity model goes negative, one component among food, buildings and
    loyalty rather than total growth, so this is a ceiling worth respecting, not a cliff edge).

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

--culture-floor: the fief-starved-culture lever (2026-08-14)
------------------------------------------------------------
The quantile map above reshapes the whole map against vanilla. It cannot express "these eight
cultures specifically are too poor for the number of lords they field", which is what the
2026-08-14 faction-economy pass needed: income per lord spans 25x across cultures, and six of
the eight worst sit below a quarter of the map median. `--culture-floor` raises every fief of a
named culture to a floor, lift-only, and is the only path here that also writes village `hearth`
(the quantile map still never touches it).

Floored fiefs are EXCLUDED from the quantile ranking population, exactly as --preserve and
--pin-zero-village are, and for the same reason: a fief whose value this run raises would
otherwise shift every free fief's rank on the next run and the re-run would not be a no-op.
--preserve still wins over a floor; an explicit human override beats an automatic one.

**That exclusion is not free, and it reaches beyond the named cultures.** Shrinking the ranking
population moves every remaining fief's rank, so the raw quantile target of an UNRELATED,
un-floored fief differs depending on whether the flag was passed. Measured on the 2026-08-14 map:
the town population drops 78 -> 66 and castles 143 -> 114, and 163 remaining targets change. Under
the default lift-only mode this is fully masked (those fiefs already sit above both targets, so
neither run moves them, verified 0 collateral changes), but the masking is a property of the
current data, not of the design. **Combining --allow-lower with a culture floor is where it stops
being masked**; treat that pair as needing a fresh review of the whole diff, not just the floored
cultures.

Usage:
    python3 tools/rebalance_settlement_prosperity.py                # DRY-RUN (default)
    python3 tools/rebalance_settlement_prosperity.py --apply        # write with .bak backup
    python3 tools/rebalance_settlement_prosperity.py \\
        --culture-floor dolguldur,goblin,gundabad:4800/950/500 --apply
Options:
    --allow-lower          pure vanilla quantile map (may reduce values); default is lift-only
    --town-uplift N        flat add to town targets after mapping (default 0), capped at 5600
    --pin-zero-village     pin fiefs with no bound villages to their class minimum (default off)
    --preserve id[,id...]  freeze specific fiefs at their current value
    --culture-floor SPEC   repeatable; `culture[,culture...]:TOWN/CASTLE/HEARTH`, lift-only.
                           Caps: town/castle 5600, hearth 825 (vanilla's observed maxima).
    --game-dir DIR         Bannerlord install root (default: E:\\Steam\\... or $BANNERLORD_GAME_DIR)
"""
import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
PROSPERITY_CAP = 5600  # vanilla max; >6000 flips the housing-growth term negative
# Vanilla SandBox's observed village maximum (measured 2026-08-14: n=273, p50=305, p90=572,
# max=825). Used only by --culture-floor; the quantile map never touches hearth.
HEARTH_CAP = 825
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


def load_culture_floor_file(path):
    """Read the committed floor spec into the same shape parse_culture_floors returns.

    This file is the single source of truth for the floor: this tool writes it into the live
    map module, and taom_schema.py's SETTLEMENT_ECONOMY_FLOOR check reads the same file to
    verify the live module still honours it. Restating the numbers in either consumer would
    put them back out of sync the first time one was retuned."""
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            spec = json.load(f)
    except OSError as exc:
        raise SystemExit(f"FATAL: cannot read --culture-floor-file {path}: {exc}")
    except json.JSONDecodeError as exc:
        raise SystemExit(f"FATAL: --culture-floor-file {path} is not valid JSON: {exc}")
    if not isinstance(spec, dict):
        raise SystemExit(f"FATAL: --culture-floor-file {path} must contain a JSON object")
    floor = spec.get("floor") or {}
    cultures = spec.get("cultures") or []
    missing = [k for k in ("town", "castle", "hearth") if k not in floor]
    if missing:
        raise SystemExit(f"FATAL: {path} floor block is missing {', '.join(missing)}")
    if not cultures:
        raise SystemExit(f"FATAL: {path} names no cultures")
    return parse_culture_floors(
        [f"{','.join(cultures)}:{floor['town']}/{floor['castle']}/{floor['hearth']}"])


def parse_culture_floors(specs):
    """['dolguldur,goblin:4800/950/500', ...] -> {culture: {'town': N, 'castle': N, 'hearth': N}}.

    Values are clamped at parse time, so a caller cannot smuggle a target past the caps by
    writing a large number on the command line."""
    floors = {}
    for spec in specs or ():
        spec = spec.strip()
        if not spec:
            continue
        if spec.count(":") != 1:
            raise SystemExit(f"FATAL: --culture-floor '{spec}' is not culture[,culture...]:TOWN/CASTLE/HEARTH")
        cultures, values = spec.split(":")
        parts = values.split("/")
        if len(parts) != 3:
            raise SystemExit(f"FATAL: --culture-floor '{spec}' needs exactly TOWN/CASTLE/HEARTH")
        try:
            town, castle, hearth = (int(p) for p in parts)
        except ValueError:
            raise SystemExit(f"FATAL: --culture-floor '{spec}' has a non-integer value")
        if min(town, castle, hearth) < 0:
            raise SystemExit(f"FATAL: --culture-floor '{spec}' has a negative value")
        entry = {
            "town": min(town, PROSPERITY_CAP),
            "castle": min(castle, PROSPERITY_CAP),
            "hearth": min(hearth, HEARTH_CAP),
        }
        for culture in (c.strip() for c in cultures.split(",")):
            if not culture:
                continue
            if culture in floors and floors[culture] != entry:
                raise SystemExit(f"FATAL: culture '{culture}' given two different --culture-floor values")
            floors[culture] = entry
    return floors


def compute_hearth_targets(records, culture_floors):
    """{village_id: new_hearth} for villages of floored cultures. Lift-only, so idempotent.

    LIFT-ONLY means max() and nothing else. An outer `min(..., HEARTH_CAP)` here would LOWER a
    village that already sits above the cap (900 -> 825), which is the one thing this path
    promises never to do. The floor itself is already capped in parse_culture_floors, so the
    cap cannot enter through this function anyway."""
    targets = {}
    for r in records:
        if r["kind"] != "village":
            continue
        floor = culture_floors.get(r["culture"])
        if not floor:
            continue
        new = max(r["hearth"], floor["hearth"])
        if new != r["hearth"]:
            targets[r["id"]] = new
    return targets


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
                    pin_zero_village=False, preserve_ids=(), culture_floors=None):
    """Single source of truth for the target curve — the analyzer imports this.

    Idempotency contract: re-running with the SAME flag set after --apply reports 0 changes.
    Preserved/pinned/floored fiefs are therefore EXCLUDED from the quantile-map ranking population
    (a frozen fief that kept its old value would otherwise shift every free fief's rank on
    the next run — unbounded drift, deep-review 2026-07-02 HIGH finding)."""
    culture_floors = culture_floors or {}
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
        return (r["id"] in preserve_ids
                or r["culture"] in culture_floors
                or (pin_zero_village and r["n_villages"] == 0))

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

    # Precedence, tightest override last: quantile map < pin < culture floor < preserve.
    # The floor sits above the pin deliberately — it is lift-only, and pinning a floored
    # culture's zero-village fief back down to the class minimum would contradict that.
    #
    # max() and nothing else, for the reason spelled out in compute_hearth_targets: an outer
    # min(..., PROSPERITY_CAP) would LOWER a fief already above the cap (6000 -> 5600) while
    # claiming to be lift-only. parse_culture_floors has already capped the floor, so the only
    # value the cap could bite here is one the floor never set.
    for kind in ("town", "castle"):
        for r in by_class[kind]:
            floor = culture_floors.get(r["culture"])
            if floor:
                targets[r["id"]] = max(r["prosperity"], floor[kind])

    # Preserve wins over everything (an explicit human override beats every automatic rule).
    for kind in ("town", "castle"):
        for r in by_class[kind]:
            if r["id"] in preserve_ids:
                targets[r["id"]] = r["prosperity"]

    fiefs = by_class["town"] + by_class["castle"]
    return targets, fiefs


def _attr_pattern(sid, tag, attr):
    """Anchored on the unique Settlement id, confined to ONE settlement and to `tag`'s own attrs.

    Three hazards this closes, each of which produces EXACTLY ONE match and so sails past the
    fail-loud assertion while rewriting the wrong bytes (Codex review, 2026-08-14):

    1. A plain DOTALL `.*?` starting from a settlement that does not contain `tag` runs past the
       end of that settlement and matches the NEXT one's tag. The tempered span
       `(?:(?!</Settlement\\s*>).)*?` stops it. `\\s*` matters: `</Settlement >` is valid XML and
       an untempered `</Settlement>` literal does not recognise it.
    2. `\\b` before the attribute name is a WORD boundary, not an XML attribute boundary. It
       correctly refuses `max_prosperity` (`_` is a word char, so there is no boundary), but it
       happily matches the `prosperity` inside `max-prosperity` (`-` is not a word char). Only
       requiring real XML whitespace before the name rejects both.
    3. An attribute VALUE containing the attribute name, e.g. `note='prosperity=\"123\"'`, is
       matched by `[^>]*?` scanning across quotes. Requiring whitespace before the name plus a
       quote immediately after `=` narrows this to genuine attribute positions.

    Callers only pass ids they parsed as carrying the tag, so this is defence in depth, but the
    target is live game data and every one of these failures is silent."""
    return re.compile(
        r'(<Settlement\s+id="' + re.escape(sid) + r'"(?:(?!</Settlement\s*>).)*?<'
        + tag + r'(?:"[^"]*"|\'[^\']*\'|[^>"\'])*?\s' + attr + r'\s*=\s*")(\d+)(")',
        re.DOTALL)


def apply_to_file(path, changes, do_write, hearth_changes=None):
    """changes: {fief_id: new_prosperity}; hearth_changes: {village_id: new_hearth}.

    Byte round-trip (BOM and CRLF survive inside the decoded string), exactly-once assertion per
    id, one .bak, idempotent. Both dicts are written in the SAME round-trip so a run produces one
    backup and one write. Every key in `changes` MUST be a fief parsed with a <Town> element and
    every key in `hearth_changes` a settlement parsed with a <Village> element; compute_targets
    and compute_hearth_targets guarantee this."""
    changes = changes or {}
    hearth_changes = hearth_changes or {}

    # A no-op --apply must not touch the disk. Writing anyway would overwrite the .bak with a
    # byte-identical copy of the current file, silently destroying the only rollback point for
    # the PREVIOUS run — and re-running the tool to confirm idempotency is exactly the moment a
    # careful person does this (Codex review, 2026-08-14).
    if not changes and not hearth_changes:
        print("No changes to apply; file and backup left untouched.")
        return

    with open(path, "rb") as f:
        original = f.read()
    text = original.decode("utf-8")

    edits = [("Town", "prosperity", changes), ("Village", "hearth", hearth_changes)]
    for tag, attr, mapping in edits:
        for sid, new_value in sorted(mapping.items()):
            pat = _attr_pattern(sid, tag, attr)
            matches = pat.findall(text)
            if len(matches) != 1:
                raise SystemExit(f"FATAL: id '{sid}' matched {len(matches)} times for {tag}/{attr} "
                                 f"(expected 1). Aborting; no file written.")
            text = pat.sub(lambda m, v=str(new_value): m.group(1) + v + m.group(3), text, count=1)

    if not do_write:
        print("DRY-RUN ONLY -- no file written. Re-run with --apply to write.")
        return
    if os.path.exists(path + ".bak"):
        # Never silently replace an older rollback point with a newer one. The stamped copy is
        # what a second apply would otherwise cost you.
        stamped = path + ".bak-" + str(os.path.getmtime(path)).replace(".", "")
        if not os.path.exists(stamped):
            os.replace(path + ".bak", stamped)
            print(f"Existing backup preserved as: {stamped}")
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
    ap.add_argument("--culture-floor", action="append", default=[], metavar="SPEC",
                    help="repeatable; culture[,culture...]:TOWN/CASTLE/HEARTH, lift-only")
    ap.add_argument("--culture-floor-file", default=None, metavar="PATH",
                    help="read the floor from a committed spec (tools/settlement_economy_floor.json)")
    ap.add_argument("--game-dir", default=None, help="Bannerlord install root")
    args = ap.parse_args()

    live = live_settlements_path(args.game_dir)
    vanilla = vanilla_settlements_path(args.game_dir)
    preserve_ids = tuple(s.strip() for s in args.preserve.split(",") if s.strip())
    if args.culture_floor and args.culture_floor_file:
        raise SystemExit("FATAL: pass --culture-floor or --culture-floor-file, not both. Two "
                         "sources for one floor is how they drift apart.")
    culture_floors = (load_culture_floor_file(args.culture_floor_file)
                      if args.culture_floor_file else parse_culture_floors(args.culture_floor))

    taom = parse_settlements(live)
    van = parse_settlements(vanilla)

    known_cultures = {r["culture"] for r in taom}
    unknown = sorted(c for c in culture_floors if c not in known_cultures)
    if unknown:
        raise SystemExit(f"FATAL: --culture-floor names cultures no settlement carries: {', '.join(unknown)}. "
                         "Check the StringId (Rohan is 'vlandia', Dale is 'sturgia', Khand is 'battania').")

    targets, fiefs = compute_targets(taom, van, args.allow_lower, args.town_uplift,
                                     args.pin_zero_village, preserve_ids, culture_floors)
    hearth_targets = compute_hearth_targets(taom, culture_floors)

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

    if culture_floors:
        by_id = {r["id"]: r for r in taom}
        villages = [r for r in taom if r["kind"] == "village"]
        for culture in sorted(culture_floors):
            floor = culture_floors[culture]
            fief_ids = {f["id"] for f in fiefs if f["culture"] == culture}
            v_ids = {r["id"] for r in villages if r["culture"] == culture}
            print(f"\n=== culture floor {culture} "
                  f"(town {floor['town']} / castle {floor['castle']} / hearth {floor['hearth']}) ===")
            print(f"  fiefs raised: {len(fief_ids & set(changes))}/{len(fief_ids)}; "
                  f"villages raised: {len(v_ids & set(hearth_targets))}/{len(v_ids)}")
            # Itemise hearth the way the per-region loop above itemises prosperity. An aggregate
            # count is not auditable: a human reviewing a live --apply could check every fief
            # change individually and only count the 110 village changes.
            for vid in sorted(v_ids & set(hearth_targets)):
                old = by_id[vid]["hearth"]
                new = hearth_targets[vid]
                print(f"  village {vid:<24} hearth {old:>4} -> {new:>4} ({new - old:+d})")
        print(f"\n{len(hearth_targets)} village hearth change(s).")

    apply_to_file(live, changes, args.apply, hearth_targets)


if __name__ == "__main__":
    sys.exit(main())
