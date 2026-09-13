#!/usr/bin/env python3
"""Ranged range ladders: report every troop's reach against the ladder grid, and rewrite the
rosters onto it (#582).

READ-ONLY by default. Writes tools/reports/ranged-ladders/{REPORT.md,ranged-ladders.json}.
`--apply` rewrites the launcher slots of Main/_Module/ModuleData/troops/troops_*.xml.

WHAT IT MEASURES
----------------
Reach is the launcher's missile_speed (tools/ranged_ladder.py explains why the skill is not).
The spec, tools/ranged_ladders.json, turns a troop's LINE (its file, or an id prefix inside one,
in kingdom rank order) and BAND (engine tier: E T0-2, R T3-4, V T5-6, X T7-8, C T9-10) into a
cell of the grid `speed = band_base[band] + rank_step * (n_lines - rank)`; every cell is an item
`ladder_<line>_<bow|xbow>_<band>` that tools/generate_ranged_ladder_items.py writes into the
Armory. This tool moves every bow and crossbow slot of every battle set onto its cell. Ammo is
never touched, a bow never becomes a crossbow.

THE REPORT
----------
The grid; the two rules' inversions before and after (same function as the validator's
RANGED_LADDER_INVERSION); per line, every ranged troop with its launcher, speed and estimated
reach before and after; troops no line claims.

APPLY
-----
Refuses while any ladder_* id the edits need is missing from the launcher index: the items are
generated first, and a roster naming an item the engine cannot find spawns the troop with no
bow and no error. Writes through fix_upgrade_armour_regressions.write_changes (byte-faithful:
BOM and line endings kept, re-parsed before writing). Idempotent: a second --apply writes nothing.

USAGE
-----
    python tools/rebalance_ranged_ladders.py                  # report only
    python tools/rebalance_ranged_ladders.py --stdout         # also print the summary
    python tools/rebalance_ranged_ladders.py --apply          # rewrite the rosters
    python tools/rebalance_ranged_ladders.py --spec other.json --game-modules "<.../Modules>"
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fix_upgrade_armour_regressions as fx  # noqa: E402  write_changes
import ranged_ladder as rl  # noqa: E402
import rebalance_troops as rb  # noqa: E402  DEFAULT_GAME_MODULES

REPORT_DIR = rl.REPO_ROOT / "tools" / "reports" / "ranged-ladders"
REPORT_MD = "REPORT.md"
REPORT_JSON = "ranged-ladders.json"


# --------------------------------------------------------------------------- #
# Context                                                                       #
# --------------------------------------------------------------------------- #
def simulated_launchers(spec: dict, launchers: dict) -> dict:
    """The index as it will be once the items exist: every planned cell added (or replaced)
    with its grid speed and its donor's other stats, so the after-view is computed the same
    way the validator will compute it on the real files."""
    out = dict(launchers)
    for item in rl.planned_items(spec):
        donor = launchers.get(item.donor)
        out[item.id] = rl.Launcher(
            id=item.id, cls=item.cls, speed=item.speed,
            accuracy=donor.accuracy if donor else 0, damage=donor.damage if donor else 0,
            name=f"(planned) {item.id}", file="ranged_ladder.xml")
    return out


def apply_in_memory(troops: dict, edits: list) -> dict:
    """A copy of the troops with the edits applied, for the after-view."""
    import copy
    out = {tid: copy.deepcopy(t) for tid, t in troops.items()}
    by = defaultdict(dict)
    for e in edits:
        by[e.troop][(e.slot, e.old)] = e.new
    for tid, changes in by.items():
        for st in out[tid].sets:
            for slot in rl.LAUNCHER_SLOTS:
                new = changes.get((slot, st.get(slot, "")))
                if new is not None:
                    st[slot] = new
    return out


def build_context(spec: dict, launchers: dict, troops: dict, game_modules, moduledata) -> dict:
    edits = rl.planned_edits(troops, launchers, spec)
    before = rl.inversions(troops, launchers, spec)
    after_idx = simulated_launchers(spec, launchers)
    after_troops = apply_in_memory(troops, edits)
    after = rl.inversions(after_troops, after_idx, spec)
    return {
        "spec": spec, "launchers": launchers, "after_launchers": after_idx,
        "troops": troops, "after_troops": after_troops, "edits": edits,
        "before": before, "after": after,
        "unassigned": rl.unassigned(troops, launchers, spec),
        "items": rl.planned_items(spec),
        "game_modules": str(game_modules), "moduledata": str(moduledata),
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
    }


# --------------------------------------------------------------------------- #
# Rendering                                                                     #
# --------------------------------------------------------------------------- #
def _launcher_cell(troop, launchers, cls):
    ids = sorted(rl.troop_launchers(troop, launchers).get(cls, ()))
    if not ids:
        return "", None
    speed = max(launchers[i].speed for i in ids)
    label = ", ".join(f"`{i}`" for i in ids)
    return label, speed


def render_grid(spec: dict) -> str:
    bands = rl.band_order(spec)
    lines = ["| # | Line | Class | " + " | ".join(bands) + " |",
             "|---|---|---|" + "---|" * len(bands)]
    for line in spec["lines"]:
        for cls in rl.CLASSES:
            if cls not in (line.get("donor") or {}):
                continue
            speeds = " | ".join(str(rl.grid_speed(line["id"], b, spec)) for b in bands)
            lines.append(f"| {rl.rank_of(line['id'], spec)} | {line['id']} | {cls} | {speeds} |")
    return "\n".join(lines)


def render_groups(found: list) -> str:
    groups = rl.summarize(found)
    if not groups:
        return "None."
    lines = ["| Rule | Class | Scope | Pairs | Worst pair |", "|---|---|---|---|---|"]
    for g in groups:
        w = g.worst
        lines.append(
            f"| {g.kind} | {g.cls} | {g.scope} | {g.count} | `{w.low}` (T{w.low_tier}, {w.low_line}, {w.low_speed}) "
            f"over `{w.high}` (T{w.high_tier}, {w.high_line}, {w.high_speed}) |")
    return "\n".join(lines)


def render_line_tables(ctx: dict) -> str:
    spec, launchers, after_idx = ctx["spec"], ctx["launchers"], ctx["after_launchers"]
    troops, after_troops = ctx["troops"], ctx["after_troops"]
    by_line = defaultdict(list)
    for tid in sorted(troops):
        t = troops[tid]
        if not rl.troop_launchers(t, launchers):
            continue
        line = rl.line_of(t, spec)
        by_line[line or "(unassigned)"].append(t)
    out = []
    order = [ln["id"] for ln in spec["lines"]] + ["(unassigned)"]
    for line in order:
        rows = sorted(by_line.get(line, []), key=lambda t: (t.tier, t.level, t.id))
        if not rows:
            continue
        rank = f"rank {rl.rank_of(line, spec)}" if line in [ln["id"] for ln in spec["lines"]] else "no line"
        out.append(f"\n### {line} ({rank}, {len(rows)} troops)\n")
        out.append("| T | Band | Troop (lvl) | Group | Skill | Launcher before | Speed | Reach | Launcher after | Speed | Reach |")
        out.append("|---|---|---|---|---|---|---|---|---|---|---|")
        for t in rows:
            band = rl.band_of(t.tier, spec)
            for cls in rl.CLASSES:
                label, speed = _launcher_cell(t, launchers, cls)
                if speed is None:
                    continue
                skill = t.skills.get(cls, "")
                a_label, a_speed = _launcher_cell(after_troops[t.id], after_idx, cls)
                out.append(
                    f"| {t.tier} | {band} | `{t.id}` ({t.level}) | {t.group} | {skill} | {label} | {speed} | "
                    f"{rl.flight_range(speed):.0f} m | {a_label} | {a_speed} | {rl.flight_range(a_speed):.0f} m |")
    return "\n".join(out)


def render_report(ctx: dict) -> str:
    spec = ctx["spec"]
    edits = ctx["edits"]
    touched = len({e.troop for e in edits})
    ranged = sum(1 for t in ctx["troops"].values() if rl.troop_launchers(t, ctx["launchers"]))
    head = [
        "# Ranged range ladders",
        "",
        f"Generated {ctx['generated']} by `tools/rebalance_ranged_ladders.py` from `{ctx['moduledata']}` "
        f"and the launchers in `{ctx['game_modules']}`. READ-ONLY output; `--apply` rewrites the rosters.",
        "",
        "Reach is the launcher's `missile_speed`; the metres are the engine drag model on flat ground "
        "(relative, the native range function is closed). Rule 1: inside a line a lower tier is never "
        "faster than a higher tier. Rule 2: inside a band a better-ranked line is never slower than a "
        "worse-ranked one. `speed = band_base[band] + rank_step * (n_lines - rank)`.",
        "",
        "## Summary",
        "",
        f"- Ranged troops: {ranged} of {len(ctx['troops'])} in the troop files; lines: {len(spec['lines'])}; "
        f"planned items: {len(ctx['items'])}.",
        f"- Pending roster edits: {len(edits)} slots over {touched} troops.",
        f"- Inversions before: {len(ctx['before'])} pairs; after the edits: {len(ctx['after'])}.",
        f"- Troops no line claims: {', '.join(t.id for t in ctx['unassigned']) or 'none'}.",
        "",
        "## The grid",
        "",
        f"Bands: {', '.join(f'{b} T{lo}-{hi}' for b, (lo, hi) in sorted(spec['bands'].items(), key=lambda kv: kv[1][0]))}. "
        f"band_base {spec['band_base']}, rank_step {spec['rank_step']}.",
        "",
        render_grid(spec),
        "",
        "## Inversions before",
        "",
        render_groups(ctx["before"]),
        "",
        "## Inversions after the edits",
        "",
        render_groups(ctx["after"]),
        "",
        "## Per line",
        "",
        "Speed and reach before are the MAX over the troop's battle sets; after, every set carries the cell.",
        render_line_tables(ctx),
        "",
    ]
    return "\n".join(head)


def build_json(ctx: dict) -> dict:
    spec = ctx["spec"]
    return {
        "generated": ctx["generated"],
        "moduledata": ctx["moduledata"], "game_modules": ctx["game_modules"],
        "knobs": {"bands": spec["bands"], "band_base": spec["band_base"], "rank_step": spec["rank_step"]},
        "lines": [ln["id"] for ln in spec["lines"]],
        "grid": {ln["id"]: {cls: {b: rl.grid_speed(ln["id"], b, spec) for b in rl.band_order(spec)}
                            for cls in rl.CLASSES if cls in (ln.get("donor") or {})}
                 for ln in spec["lines"]},
        "items": [i.__dict__ for i in ctx["items"]],
        "edits_pending": len(ctx["edits"]),
        "edits": [e.__dict__ for e in ctx["edits"]],
        "inversions_before": len(ctx["before"]),
        "inversions_after": len(ctx["after"]),
        "worst_before": [g.worst.__dict__ | {"count": g.count} for g in rl.summarize(ctx["before"])],
        "unassigned": [t.id for t in ctx["unassigned"]],
    }


# --------------------------------------------------------------------------- #
# Main                                                                          #
# --------------------------------------------------------------------------- #
def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--game-modules", default=rb.DEFAULT_GAME_MODULES,
                    help=".../Mount & Blade II Bannerlord/Modules (launchers come from the install)")
    ap.add_argument("--moduledata", default=str(rl.MODULEDATA_DIR),
                    help="TAOM ModuleData root holding troops/")
    ap.add_argument("--spec", default=str(rl.DEFAULT_SPEC), help="the ladder spec (default %(default)s)")
    ap.add_argument("--report-dir", default=str(REPORT_DIR), help="where REPORT.md and the JSON go")
    ap.add_argument("--apply", action="store_true", help="rewrite the troop rosters onto the grid")
    ap.add_argument("--stdout", action="store_true", help="print the summary after writing")
    args = ap.parse_args(argv)

    game_modules = Path(args.game_modules)
    if not (game_modules / "LOTRLOME_Armory" / "ModuleData").is_dir():
        print(f"ERROR: LOTRLOME_Armory not found under {game_modules}\n"
              "       Launcher speeds come from the install; pass --game-modules. Nothing was written.")
        return 2
    try:
        spec = rl.load_spec(args.spec)
    except (OSError, ValueError) as exc:
        print(f"ERROR: cannot read spec {args.spec}: {exc}")
        return 2
    problems = rl.validate_spec(spec)
    failures: list[str] = []
    launchers = rl.index_launchers(rl.default_item_roots(game_modules, args.moduledata), failures=failures)
    for f in failures:
        print(f"WARNING: {f}")
    problems += rl.validate_spec(spec, launchers, cultures=rl.troop_file_cultures(args.moduledata))
    if problems:
        print("ERROR: the spec contradicts itself or the install; nothing was written:")
        for p in sorted(set(problems)):
            print(f"  - {p}")
        return 2

    troops = rl.load_ranged_troops(args.moduledata)
    try:
        ctx = build_context(spec, launchers, troops, game_modules, args.moduledata)
    except rl.LadderError as exc:
        print(f"ERROR: {exc}")
        return 2

    os.makedirs(args.report_dir, exist_ok=True)
    md = render_report(ctx)
    with open(os.path.join(args.report_dir, REPORT_MD), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(md)
    with open(os.path.join(args.report_dir, REPORT_JSON), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(build_json(ctx), fh, indent=2)
    print(f"Launchers: {len(launchers)}. Ranged troops: "
          f"{sum(1 for t in troops.values() if rl.troop_launchers(t, launchers))}. "
          f"Inversions: {len(ctx['before'])} before, {len(ctx['after'])} after. "
          f"Pending edits: {len(ctx['edits'])}. Reports: {args.report_dir}")
    if args.stdout:
        print(md.split("## Per line")[0])

    if not args.apply:
        return 0
    edits = ctx["edits"]
    if not edits:
        print("Nothing to apply: every launcher slot already carries its cell.")
        return 0
    missing = sorted({e.new for e in edits} - set(launchers))
    if missing:
        print(f"ERROR: {len(missing)} ladder item(s) the rosters would name are not in the launcher "
              f"index; run tools/generate_ranged_ladder_items.py --apply first, then restart the game. "
              f"Nothing was written. First few: {', '.join(missing[:6])}")
        return 2
    changes = [{"file": e.file, "troop": e.troop, "slot": e.slot, "old": e.old, "new": e.new} for e in edits]
    written = fx.write_changes(changes)
    remaining = rl.planned_edits(rl.load_ranged_troops(args.moduledata), launchers, spec)
    print(f"Applied {len(edits)} slot edits over {len({e.troop for e in edits})} troops in {written} file(s); "
          f"{len(remaining)} edit(s) still pending.")
    return 0 if not remaining else 1


if __name__ == "__main__":
    sys.exit(main())
