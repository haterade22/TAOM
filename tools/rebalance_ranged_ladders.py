#!/usr/bin/env python3
"""Ranged ladders: report every archer's reach, damage, accuracy and skill against the ladder,
and rewrite the rosters onto it (#582, per-tier and ranked since #617).

READ-ONLY by default. Writes tools/reports/ranged-ladders/{REPORT.md,REPORT.html,ranged-ladders.json}
and the tracked copy docs/reference/ranged-troops.html (the reports dir is gitignored).
`--apply` rewrites the launcher slots AND the Bow or Crossbow skill of the ladder troops in
Main/_Module/ModuleData/troops/troops_*.xml.

WHAT IT MEASURES
----------------
tools/ranged_ladder.py explains the engine: reach is the launcher's missile_speed, damage is
almost all the launcher's thrust_damage, spread is mostly the launcher's accuracy, and the troop's
skill adds a little damage and spread and drives the AI's aim and fire rate. The spec,
tools/ranged_ladders.json, ranks every LINE (a troop file, or an id prefix inside one) on three
lists and gives every tier the line lists a CELL: an item `ladder_<line>_<bow|xbow>_t<tier>` that
tools/generate_ranged_ladder_items.py writes into the Armory, plus the skill the troop carries.
This tool moves every bow and crossbow slot of every battle set onto its cell and sets the skill.
Ammo is never touched, a bow never becomes a crossbow.

THE SKILL WRITE
---------------
Only Bow or Crossbow, only on troops carrying that launcher, through rebalance_troops'
byte-faithful writer (never a full rebaseline: its dry run changes 77 troops for other reasons).
The clamp keeps UPGRADE_SKILL_REGRESSION green: a child below its upgrade source on that skill is
raised to it, never a ladder troop, which would leave its cell and is refused. Militia bindings
come from the culture files; when they cannot be read the tool refuses rather than guess.

THE REPORT
----------
The cells; the two rules' inversions before and after, per stat (same function as the
validator's RANGED_LADDER_INVERSION); per line, every ranged troop with its launcher and skill
before and after; troops no line claims. REPORT.html is the same roster as a sortable document per
kingdom: skills beside the weapon (speed, reach, accuracy, spread, cadence, the mounted open-fire
distance, bow plus ammo damage, shots), for reading what a troop actually fields.

APPLY
-----
Refuses while any ladder_* id the edits need is missing from the launcher index: the items are
generated first, and a roster naming an item the engine cannot find spawns the troop with no
bow and no error. Slots go through fix_upgrade_armour_regressions.write_changes, skills through
rebalance_troops.apply_skills_via_regex (both byte-faithful, both re-parse before writing).
Idempotent: a second --apply writes nothing.

USAGE
-----
    python tools/rebalance_ranged_ladders.py                  # report only
    python tools/rebalance_ranged_ladders.py --stdout         # also print the summary
    python tools/rebalance_ranged_ladders.py --apply          # rewrite the rosters
    python tools/rebalance_ranged_ladders.py --spec other.json --game-modules "<.../Modules>"
"""
from __future__ import annotations

import argparse
import html as _html
import json
import os
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fix_upgrade_armour_regressions as fx  # noqa: E402  write_changes
import ranged_ladder as rl  # noqa: E402
import rebalance_troops as rb  # noqa: E402  DEFAULT_GAME_MODULES, apply_skills_via_regex, militia

REPORT_DIR = rl.REPO_ROOT / "tools" / "reports" / "ranged-ladders"
REPORT_MD = "REPORT.md"
REPORT_JSON = "ranged-ladders.json"
# The tracked copy of the HTML: tools/reports/ is gitignored, docs/reference/ is not.
DOCS_HTML = rl.REPO_ROOT / "docs" / "reference" / "ranged-troops.html"


# --------------------------------------------------------------------------- #
# Context                                                                       #
# --------------------------------------------------------------------------- #
def simulated_launchers(spec: dict, launchers: dict) -> dict:
    """The index as it will be once the items exist: every planned cell added (or replaced)
    with its speed, damage and accuracy, so the after-view is computed the same way the
    validator will compute it on the real files."""
    out = dict(launchers)
    for item in rl.planned_items(spec):
        out[item.id] = rl.Launcher(
            id=item.id, cls=item.cls, speed=item.speed, accuracy=item.accuracy, damage=item.damage,
            name=f"(planned) {item.id}", file="ranged_ladder.xml")
    return out


def apply_in_memory(troops: dict, edits: list, skill_edits: list = ()) -> dict:
    """A copy of the troops with the slot and skill edits applied, for the after-view."""
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
    for s in skill_edits:
        out[s.troop].skills[s.skill] = s.new
    return out


def build_context(spec: dict, launchers: dict, troops: dict, game_modules, moduledata, ammo: dict | None = None,
                  barred: set | None = None, militia=frozenset(), exempt=None, sources=None) -> dict:
    edits = rl.planned_edits(troops, launchers, spec, barred)
    skill_edits = rl.planned_skill_edits(troops, launchers, spec, militia=militia,
                                         exempt_edges=exempt, sources=sources)
    before = rl.inversions(troops, launchers, spec)
    after_idx = simulated_launchers(spec, launchers)
    after_troops = apply_in_memory(troops, edits, skill_edits)
    after = rl.inversions(after_troops, after_idx, spec)
    return {
        "spec": spec, "launchers": launchers, "after_launchers": after_idx,
        "troops": troops, "after_troops": after_troops, "edits": edits, "skill_edits": skill_edits,
        "before": before, "after": after,
        "unassigned": rl.unassigned(troops, launchers, spec),
        "unlisted": rl.unlisted(troops, launchers, spec),
        "mount_conflicts": rl.mount_conflicts(troops, launchers, barred) if barred else [],
        "barred": barred,
        "items": rl.planned_items(spec), "ammo": ammo or {},
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
    best = max((launchers[i] for i in ids), key=lambda l: (l.speed, l.damage, l.accuracy))
    label = ", ".join(f"`{i}`" for i in ids)
    return label, best


def render_grid(spec: dict) -> str:
    """One row per line and class: every listed tier's cell as speed / damage / accuracy / skill."""
    tiers = list(range(0, rl.MAX_TIER + 1))
    lines = ["Each cell is `speed / damage / accuracy / skill`.", "",
             "| Line | Ranks (overall, damage, accuracy) | Class | " + " | ".join(f"T{t}" for t in tiers) + " |",
             "|---|---|---|" + "---|" * len(tiers)]
    for line in spec["lines"]:
        r = line["ranks"]
        for cls in rl.CLASSES:
            listed = rl.tiers_for(line["id"], cls, spec)
            if not listed:
                continue
            cells = []
            for t in tiers:
                if t in listed:
                    c = rl.cell(line["id"], cls, t, spec)
                    cells.append(f"{c.speed}/{c.damage}/{c.accuracy}/{rl.skill_cell(line['id'], t, spec)}")
                else:
                    cells.append("")
            lines.append(f"| {line['id']} | {r['overall']}, {r['damage']}, {r['accuracy']} | {cls} | "
                         + " | ".join(cells) + " |")
    return "\n".join(lines)


def render_groups(found: list) -> str:
    groups = rl.summarize(found)
    if not groups:
        return "None."
    lines = ["| Rule | Stat | Class | Scope | Pairs | Worst pair |", "|---|---|---|---|---|---|"]
    for g in groups:
        w = g.worst
        lines.append(
            f"| {g.kind} | {g.stat} | {g.cls} | {g.scope} | {g.count} | `{w.low}` (T{w.low_tier}, {w.low_line}, "
            f"{w.low_value}) over `{w.high}` (T{w.high_tier}, {w.high_line}, {w.high_value}) |")
    return "\n".join(lines)


def _fmt(rec):
    return f"{rec.speed} / {rec.damage} / {rec.accuracy}" if rec else ""


def render_line_tables(ctx: dict) -> str:
    spec, launchers, after_idx = ctx["spec"], ctx["launchers"], ctx["after_launchers"]
    troops, after_troops = ctx["troops"], ctx["after_troops"]
    by_line = defaultdict(list)
    for tid in sorted(troops):
        t = troops[tid]
        if not rl.troop_launchers(t, launchers):
            continue
        by_line[rl.line_of(t, spec) or "(unassigned)"].append(t)
    out = []
    ids = [ln["id"] for ln in spec["lines"]]
    for line in ids + ["(unassigned)"]:
        rows = sorted(by_line.get(line, []), key=lambda t: (t.tier, t.level, t.id))
        if not rows:
            continue
        if line in ids:
            r = rl.line_spec(line, spec)["ranks"]
            label = f"rank {r['overall']}, damage {r['damage']}, accuracy {r['accuracy']}"
        else:
            label = "no line"
        out.append(f"\n### {line} ({label}, {len(rows)} troops)\n")
        out.append("| T | Troop (lvl) | Group | Skill | Launcher before | Spd / Dmg / Acc | Launcher after | Spd / Dmg / Acc |")
        out.append("|---|---|---|---|---|---|---|---|")
        for t in rows:
            for cls in rl.CLASSES:
                label_b, best_b = _launcher_cell(t, launchers, cls)
                if best_b is None:
                    continue
                label_a, best_a = _launcher_cell(after_troops[t.id], after_idx, cls)
                sk_b, sk_a = t.skills.get(cls, 0), after_troops[t.id].skills.get(cls, 0)
                skill = f"{sk_b}" if sk_a == sk_b else f"{sk_b} to {sk_a}"
                out.append(f"| {t.tier} | `{t.id}` ({t.level}) | {t.group} | {cls} {skill} | {label_b} | "
                           f"{_fmt(best_b)} | {label_a} | {_fmt(best_a)} |")
    return "\n".join(out)


def render_report(ctx: dict) -> str:
    spec = ctx["spec"]
    edits, skill_edits = ctx["edits"], ctx["skill_edits"]
    touched = len({e.troop for e in edits} | {s.troop for s in skill_edits})
    ranged = sum(1 for t in ctx["troops"].values() if rl.troop_launchers(t, ctx["launchers"]))
    clamps = [s for s in skill_edits if s.reason == "clamp"]
    head = [
        "# Ranged ladders",
        "",
        f"Generated {ctx['generated']} by `tools/rebalance_ranged_ladders.py` from `{ctx['moduledata']}` "
        f"and the launchers in `{ctx['game_modules']}`. READ-ONLY output; `--apply` rewrites the rosters.",
        "",
        "Rule 1: inside a line a lower tier never beats a higher tier. Rule 2: at the same tier a "
        "better-ranked line is never worse. Both per launcher class and per stat (speed, damage, "
        "accuracy, skill); the formulas are in `tools/ranged_ladders.json`. Reach metres are the engine "
        "drag model on flat ground (relative; the native range function is closed).",
        "",
        "## Summary",
        "",
        f"- Ranged troops: {ranged} of {len(ctx['troops'])} in the troop files; lines: {len(spec['lines'])}; "
        f"planned items: {len(ctx['items'])}.",
        f"- Pending edits: {len(edits)} launcher slots and {len(skill_edits)} skill values "
        f"({len(clamps)} of them clamps) over {touched} troops.",
        (f"- Clamped (raised to their upgrade source, not ladder troops): "
         + ", ".join(f"`{s.troop}` {s.skill} {s.old} to {s.new}" for s in clamps) + ".") if clamps else
        "- Clamped: none.",
        f"- Inversions before: {len(ctx['before'])} pairs; after the edits: {len(ctx['after'])}.",
        f"- Troops no line claims: {', '.join(t.id for t in ctx['unassigned']) or 'none'}.",
        f"- Troops at a tier their line lists no cell for: "
        f"{', '.join(f'{t.id} ({c} T{t.tier})' for t, c in ctx['unlisted']) or 'none'}.",
        (f"- Mounted troops holding a launcher they cannot draw from the saddle (usage requires_no_mount): "
         + (', '.join(f'`{c.troop}` ({c.launcher}, {c.usage})' for c in ctx['mount_conflicts']) or 'none') + '.')
        if ctx.get('barred') is not None else
        "- Mounted-usage check skipped: no item_usage_sets.xml could be read.",
        "",
        "## The cells",
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
        "Before is the best launcher over the troop's battle sets; after, every set carries the cell.",
        render_line_tables(ctx),
        "",
    ]
    return "\n".join(head)


def build_json(ctx: dict) -> dict:
    spec = ctx["spec"]
    return {
        "generated": ctx["generated"],
        "moduledata": ctx["moduledata"], "game_modules": ctx["game_modules"],
        "stats": spec["stats"],
        "lines": {ln["id"]: ln["ranks"] for ln in spec["lines"]},
        "cells": {i.id: {"speed": i.speed, "damage": i.damage, "accuracy": i.accuracy,
                         "skill": rl.skill_cell(i.line, i.tier, spec)} for i in ctx["items"]},
        "items": [i.__dict__ for i in ctx["items"]],
        "edits_pending": len(ctx["edits"]),
        "edits": [e.__dict__ for e in ctx["edits"]],
        "skill_edits_pending": len(ctx["skill_edits"]),
        "skill_edits": [s.__dict__ for s in ctx["skill_edits"]],
        "inversions_before": len(ctx["before"]),
        "inversions_after": len(ctx["after"]),
        "worst_before": [g.worst.__dict__ | {"count": g.count} for g in rl.summarize(ctx["before"])],
        "unassigned": [t.id for t in ctx["unassigned"]],
        "unlisted": [f"{t.id}:{c}:{t.tier}" for t, c in ctx["unlisted"]],
        "mount_conflicts": [c.__dict__ for c in ctx["mount_conflicts"]],
    }


# --------------------------------------------------------------------------- #
# HTML: every ranged troop per kingdom, skill and weapon side by side           #
# --------------------------------------------------------------------------- #
REPORT_HTML = "REPORT.html"

LINE_LABELS = {
    "mirkwood": "Mirkwood", "rivendell": "Rivendell and Lindon",
    "ithilien": "Gondor: Ithil Guard and Ithilien Rangers", "blackroot": "Gondor: Blackroot Vale",
    "mordor_num": "Mordor: Black Numenoreans", "dale": "Dale", "isengard": "Isengard",
    "harad": "Harad", "mordor_uruk": "Mordor: Black Uruks", "rhun_new": "Rhun", "umbar": "Umbar",
    "gondor": "Gondor: the other regions", "goblin": "Goblin-town and Bluecraig", "erebor": "Erebor, Iron Hills, Ironpass",
    "mordor": "Mordor: orcs, Morannon, militia", "gundabad": "Gundabad", "dolguldur": "Dol Guldur",
    "rohan": "Rohan", "dunland": "Dunland",
}
GROUP_LABELS = {"Ranged": "Foot", "HorseArcher": "Horse", "Cavalry": "Cavalry", "Infantry": "Infantry"}


def _esc(text) -> str:
    return _html.escape(str(text), quote=True)


def ai_level(skill: int) -> float:
    """CalculateAILevel at Normal+ Combat AI: clamp(skill / 300 * 0.96, 0, 1)."""
    return max(0.0, min(1.0, skill / 300.0 * 0.96))


# DefaultSkillEffects: BowAccuracy AddFactor -0.0009 per level, CrossbowAccuracy -0.0005.
ACCURACY_FACTOR = {"Bow": 0.0009, "Crossbow": 0.0005}


def weapon_inaccuracy(accuracy: int, skill: int, cls: str) -> float:
    """SandboxAgentStatCalculateModel.GetWeaponInaccuracy for a bow or crossbow: (100 - accuracy)
    times the class's skill factor (1 - ACCURACY_FACTOR[cls] per level) times 0.001."""
    return (100 - accuracy) * (1.0 - ACCURACY_FACTOR[cls] * skill) * 0.001


def troop_rows(ctx: dict) -> dict:
    """{line id: [row dict]} over the CURRENT rosters (what the game fields now)."""
    spec, launchers, ammo = ctx["spec"], ctx["launchers"], ctx.get("ammo") or {}
    rows = defaultdict(list)
    for tid in sorted(ctx["troops"]):
        t = ctx["troops"][tid]
        carried = rl.troop_launchers(t, launchers)
        if not carried:
            continue
        line = rl.line_of(t, spec) or "(unassigned)"
        mounted = t.group in ("HorseArcher", "Cavalry")
        for cls in rl.CLASSES:
            ids = carried.get(cls)
            if not ids:
                continue
            best = max((launchers[i] for i in ids), key=lambda l: l.speed)
            skill = t.skills.get(cls, 0)
            want = rl.AMMO_CLASSES[cls]
            # Ammo from the sets that field the chosen launcher, so the row is a kit a set
            # actually spawns, never one set's bow with another set's quiver.
            own_sets = [st for st in t.sets if best.id in st.values()]
            shots = [ammo[st[s]] for st in own_sets for s in rl.LAUNCHER_SLOTS
                     if st.get(s) in ammo and ammo[st[s]].cls == want]
            shot = max(shots, key=lambda a: a.damage) if shots else None
            reach = rl.flight_range(best.speed)
            lvl = ai_level(skill)
            rows[line].append({
                "tier": t.tier, "band": rl.band_of(t.tier, spec), "id": tid,
                "name": t.name or tid,
                "level": t.level, "group": GROUP_LABELS.get(t.group, t.group or "?"), "mounted": mounted,
                "bow": t.skills.get("Bow", 0), "crossbow": t.skills.get("Crossbow", 0),
                "athletics": t.skills.get("Athletics", 0), "riding": t.skills.get("Riding", 0),
                "skill": skill, "cls": cls, "launcher": best.id, "launcher_name": best.name,
                "speed": best.speed, "accuracy": best.accuracy, "weapon_dmg": best.damage,
                "ammo": shot.id if shot else "", "ammo_dmg": shot.damage if shot else 0,
                "ammo_stack": shot.stack if shot else 0,
                "total_dmg": best.damage + (shot.damage if shot else 0),
                "reach": reach, "spread": weapon_inaccuracy(best.accuracy, skill, cls) * 1000,
                "cadence": 0.3 + 0.7 * lvl,
                "opens": reach * (0.3 + 0.4 * lvl) if mounted else None,
                "sets": len(t.sets), "alts": len(ids) - 1,
            })
    for line in rows:
        rows[line].sort(key=lambda r: (r["tier"], r["level"], r["id"], r["cls"]))
    return rows


_HTML_CSS = """
:root{--ground:#EEF0EA;--paper:#F7F8F5;--ink:#1C221E;--ink-2:#5B665F;--rule:#C9CFC6;--rule-2:#DFE3DC;
--bow:#3E6B4F;--xbow:#3F5C7A;--bow-soft:#DCE5DA;--xbow-soft:#D9E1EA;--hi:#F1E9C6;
--b-e:#E4EAE1;--b-r:#CFDCCD;--b-v:#A9C3AE;--b-x:#7EA08A;--b-c:#3E6B4F;--b-c-ink:#F3F6F1;--focus:#8A5A2B;}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--ground:#161A17;--paper:#1E2320;--ink:#E3E7E0;--ink-2:#9AA69E;--rule:#3A423C;--rule-2:#2B3230;
--bow:#7FB393;--xbow:#8FB0D0;--bow-soft:#243A2C;--xbow-soft:#22303F;--hi:#3A3520;
--b-e:#242A26;--b-r:#2C3A30;--b-v:#3A5442;--b-x:#4E7A5E;--b-c:#7FB393;--b-c-ink:#111511;--focus:#D9A066;}}
:root[data-theme="dark"]{--ground:#161A17;--paper:#1E2320;--ink:#E3E7E0;--ink-2:#9AA69E;--rule:#3A423C;--rule-2:#2B3230;
--bow:#7FB393;--xbow:#8FB0D0;--bow-soft:#243A2C;--xbow-soft:#22303F;--hi:#3A3520;
--b-e:#242A26;--b-r:#2C3A30;--b-v:#3A5442;--b-x:#4E7A5E;--b-c:#7FB393;--b-c-ink:#111511;--focus:#D9A066;}
*{box-sizing:border-box}
body{margin:0;background:var(--ground);color:var(--ink);font:15px/1.5 "IBM Plex Sans",system-ui,Segoe UI,Roboto,sans-serif;padding-block:0 48px;padding-inline:clamp(16px,3vw,40px)}
h1,h2,h3{font-family:"Alegreya Sans","Gill Sans",Candara,sans-serif;text-wrap:balance;margin:0}
h1{font-size:clamp(30px,4.5vw,44px);font-weight:700;letter-spacing:-.01em;line-height:1.05}
h2{font-size:clamp(22px,3vw,28px);font-weight:700;line-height:1.15}
.eyebrow{font-family:"Alegreya Sans",sans-serif;text-transform:uppercase;letter-spacing:.12em;font-size:12px;color:var(--ink-2);font-weight:700}
header.mast{padding-block:40px 20px;max-width:1400px;margin:0 auto}
header.mast p{max-width:68ch;color:var(--ink-2);margin:12px 0 0}
main{max-width:1400px;margin:0 auto;display:grid;gap:36px}
.strip{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:1px;background:var(--rule);border:1px solid var(--rule)}
.strip div{background:var(--paper);padding:14px 16px}
.strip b{display:block;font-family:"Alegreya Sans",sans-serif;font-size:28px;font-weight:700;line-height:1;font-variant-numeric:tabular-nums}
.strip span{color:var(--ink-2);font-size:13px}
.read{display:grid;grid-template-columns:1fr 1fr;gap:24px 40px;border-top:2px solid var(--ink);padding-top:16px}
.read p{margin:0 0 8px;max-width:62ch}
.read code,td code,.grid code{font-family:"IBM Plex Mono",ui-monospace,Consolas,monospace;font-size:.92em}
nav.kingdoms{display:flex;flex-wrap:wrap;gap:6px 10px;border-top:1px solid var(--rule);border-bottom:1px solid var(--rule);padding:12px 0}
nav.kingdoms a{color:var(--ink);text-decoration:none;font-size:14px;padding:4px 8px;border:1px solid var(--rule);background:var(--paper)}
nav.kingdoms a b{font-family:"Alegreya Sans",sans-serif;color:var(--ink-2);margin-right:6px}
nav.kingdoms a:hover,nav.kingdoms a:focus-visible{border-color:var(--ink);outline:none}
.controls{display:flex;flex-wrap:wrap;gap:12px 20px;align-items:center;font-size:14px}
.controls input[type=search]{font:inherit;padding:6px 10px;border:1px solid var(--rule);background:var(--paper);color:var(--ink);min-width:240px}
.controls label{display:inline-flex;gap:6px;align-items:center}
section.kingdom{display:grid;gap:12px}
section.kingdom header{display:flex;flex-wrap:wrap;align-items:baseline;gap:8px 18px;border-top:2px solid var(--ink);padding-top:10px}
section.kingdom header .rank{font-family:"Alegreya Sans",sans-serif;font-size:14px;color:var(--ink-2);text-transform:uppercase;letter-spacing:.1em}
section.kingdom header .count{color:var(--ink-2);font-size:14px;margin-left:auto}
.grid{display:flex;flex-wrap:wrap;gap:6px 14px;font-size:13px;color:var(--ink-2)}
.grid span b{font-variant-numeric:tabular-nums;color:var(--ink)}
.wrap{overflow-x:auto;border:1px solid var(--rule);background:var(--paper)}
table{border-collapse:collapse;width:100%;font-size:13.5px;font-variant-numeric:tabular-nums;min-width:1180px}
thead th{position:sticky;top:0;background:var(--paper);text-align:left;font-weight:600;font-size:12px;letter-spacing:.04em;text-transform:uppercase;color:var(--ink-2);padding:9px 8px;border-bottom:2px solid var(--ink);cursor:pointer;white-space:nowrap;user-select:none}
thead th.num,td.num{text-align:right}
thead th[aria-sort="ascending"]::after{content:" \\2191"}thead th[aria-sort="descending"]::after{content:" \\2193"}
thead th:focus-visible{outline:2px solid var(--focus);outline-offset:-2px}
tbody td{padding:6px 8px;border-bottom:1px solid var(--rule-2);vertical-align:top}
tbody tr:hover td{background:var(--hi)}
td.id{font-family:"IBM Plex Mono",ui-monospace,Consolas,monospace;font-size:12.5px;white-space:nowrap}
td .nm{display:block;color:var(--ink-2);font-size:12px;font-family:"IBM Plex Sans",sans-serif;white-space:normal}
.chip{display:inline-block;padding:1px 7px;font-size:12px;border-radius:2px;font-weight:600;white-space:nowrap}
.chip.bow{background:var(--bow-soft);color:var(--bow)}.chip.xbow{background:var(--xbow-soft);color:var(--xbow)}
.band{display:inline-block;min-width:26px;text-align:center;padding:1px 6px;font-weight:700;font-size:12px;border-radius:2px;color:var(--ink)}
.band.E{background:var(--b-e)}.band.R{background:var(--b-r)}.band.V{background:var(--b-v)}.band.X{background:var(--b-x)}.band.C{background:var(--b-c);color:var(--b-c-ink)}
tr[hidden]{display:none}
.foot{color:var(--ink-2);font-size:13px;max-width:70ch}
@media (max-width:700px){.read{grid-template-columns:1fr}}
@media (prefers-reduced-motion:no-preference){nav.kingdoms a{transition:border-color .15s}}
"""

_HTML_JS = """
(function(){
  var q=document.getElementById('q'),bow=document.getElementById('f-bow'),xb=document.getElementById('f-xbow');
  function apply(){
    var s=(q.value||'').toLowerCase();
    document.querySelectorAll('tbody tr').forEach(function(tr){
      var cls=tr.getAttribute('data-cls');
      var okc=(cls==='Bow'&&bow.checked)||(cls==='Crossbow'&&xb.checked);
      var okq=!s||tr.textContent.toLowerCase().indexOf(s)>=0;
      tr.hidden=!(okc&&okq);
    });
    document.querySelectorAll('section.kingdom').forEach(function(sec){
      var n=sec.querySelectorAll('tbody tr:not([hidden])').length;
      sec.querySelector('.count').textContent=n+' of '+sec.getAttribute('data-n')+' rows';
    });
  }
  [q,bow,xb].forEach(function(el){el.addEventListener('input',apply)});
  document.querySelectorAll('thead th').forEach(function(th){
    th.setAttribute('tabindex','0');
    function sort(){
      var table=th.closest('table'),tbody=table.tBodies[0],i=th.cellIndex;
      var dir=th.getAttribute('aria-sort')==='ascending'?'descending':'ascending';
      table.querySelectorAll('th').forEach(function(o){o.removeAttribute('aria-sort')});
      th.setAttribute('aria-sort',dir);
      var rows=Array.prototype.slice.call(tbody.rows);
      rows.sort(function(a,b){
        var x=a.cells[i].getAttribute('data-v'),y=b.cells[i].getAttribute('data-v');
        var nx=parseFloat(x),ny=parseFloat(y);
        var c=(!isNaN(nx)&&!isNaN(ny))?nx-ny:String(x).localeCompare(String(y));
        return dir==='ascending'?c:-c;
      });
      rows.forEach(function(r){tbody.appendChild(r)});
    }
    th.addEventListener('click',sort);
    th.addEventListener('keydown',function(e){if(e.key==='Enter'||e.key===' '){e.preventDefault();sort();}});
  });
})();
"""


def render_html(ctx: dict) -> str:
    spec = ctx["spec"]
    rows = troop_rows(ctx)
    all_rows = [r for rs in rows.values() for r in rs]
    speeds = [r["speed"] for r in all_rows]
    reaches = [r["reach"] for r in all_rows]
    line_ids = [ln["id"] for ln in spec["lines"]]
    lines = line_ids + (["(unassigned)"] if rows.get("(unassigned)") else [])

    def overall(line_id):
        return rl.rank(line_id, "overall", spec) if line_id in line_ids else None

    def grid_cells(line_id):
        if line_id not in line_ids:
            return ""
        out = []
        for cls in rl.CLASSES:
            for t in rl.tiers_for(line_id, cls, spec):
                c = rl.cell(line_id, cls, t, spec)
                out.append(f'<span><span class="chip {"bow" if cls == "Bow" else "xbow"}">T{t}</span> '
                           f'<b>{c.speed}</b>/<b>{c.damage}</b>/<b>{c.accuracy}</b>/<b>{rl.skill_cell(line_id, t, spec)}</b></span>')
        return "".join(out)

    nav = "".join(
        f'<a href="#kingdom-{_esc(l)}"><b>{overall(l) if l != "(unassigned)" else "?"}</b>{_esc(LINE_LABELS.get(l, l))}</a>'
        for l in lines if rows.get(l))

    head_cols = [
        ("Tier", "num"), ("Band", ""), ("Troop", ""), ("Lvl", "num"), ("Group", ""),
        ("Bow", "num"), ("Xbow", "num"), ("Athl", "num"), ("Ride", "num"),
        ("Launcher", ""), ("Class", ""), ("Speed", "num"), ("Reach m", "num"), ("Acc", "num"),
        ("Spread", "num"), ("Cadence", "num"), ("Opens m", "num"),
        ("Bow dmg", "num"), ("Ammo", ""), ("Ammo dmg", "num"), ("Shots", "num"), ("Total dmg", "num"),
    ]
    thead = "<tr>" + "".join(f'<th scope="col" class="{c}">{h}</th>' for h, c in head_cols) + "</tr>"

    def cell(v, cls="", shown=None):
        shown = _esc(v if shown is None else shown)
        return f'<td class="{cls}" data-v="{_esc(v)}">{shown}</td>'

    sections = []
    for i, l in enumerate(lines):
        rs = rows.get(l)
        if not rs:
            continue
        body = []
        for r in rs:
            chip = f'<span class="chip {"bow" if r["cls"] == "Bow" else "xbow"}">{_esc(r["cls"])}</span>'
            alt = f" (+{r['alts']} alt)" if r["alts"] else ""
            body.append(
                f'<tr data-cls="{r["cls"]}">'
                + cell(r["tier"], "num")
                + f'<td data-v="{_esc(r["band"])}"><span class="band {_esc(r["band"])}">{_esc(r["band"])}</span></td>'
                + f'<td class="id" data-v="{_esc(r["id"])}">{_esc(r["id"])}<span class="nm">{_esc(r["name"])}</span></td>'
                + cell(r["level"], "num") + cell(r["group"])
                + cell(r["bow"], "num") + cell(r["crossbow"], "num") + cell(r["athletics"], "num") + cell(r["riding"], "num")
                + f'<td class="id" data-v="{_esc(r["launcher"])}">{_esc(r["launcher"])}{_esc(alt)}<span class="nm">{_esc(r["launcher_name"])}</span></td>'
                + f'<td data-v="{_esc(r["cls"])}">{chip}</td>'
                + cell(r["speed"], "num") + cell(f'{r["reach"]:.0f}', "num") + cell(r["accuracy"], "num")
                + cell(f'{r["spread"]:.1f}', "num") + cell(f'{r["cadence"]:.2f}', "num")
                + cell(f'{r["opens"]:.0f}' if r["opens"] is not None else "", "num")
                + cell(r["weapon_dmg"], "num")
                + f'<td class="id" data-v="{_esc(r["ammo"])}">{_esc(r["ammo"])}</td>'
                + cell(r["ammo_dmg"], "num") + cell(r["ammo_stack"], "num") + cell(r["total_dmg"], "num")
                + "</tr>")
        if l != "(unassigned)":
            r = rl.line_spec(l, spec)["ranks"]
            rank = f"Rank {r['overall']} (damage {r['damage']}, accuracy {r['accuracy']})"
        else:
            rank = "No line"
        sections.append(
            f'<section class="kingdom" id="kingdom-{_esc(l)}" data-n="{len(rs)}">'
            f'<header><span class="rank">{rank}</span><h2>{_esc(LINE_LABELS.get(l, l))}</h2>'
            f'<span class="count">{len(rs)} of {len(rs)} rows</span></header>'
            f'<div class="grid">{grid_cells(l)}</div>'
            f'<div class="wrap"><table><thead>{thead}</thead><tbody>{"".join(body)}</tbody></table></div>'
            f'</section>')

    title = "Ranged Troops of Middle-earth"
    n_lines = sum(1 for l in lines if rows.get(l))
    # A tracked generated file carries no clock: same data, same bytes. The document is a
    # complete page (doctype, charset) because docs/reference/ is opened straight from disk.
    return (
        "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n"
        '<meta name="viewport" content="width=device-width, initial-scale=1">\n'
        "<title>" + title + "</title>\n"
        '<link rel="preconnect" href="https://fonts.googleapis.com">\n'
        '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Alegreya+Sans:wght@400;700&family=IBM+Plex+Sans:wght@400;600&family=IBM+Plex+Mono&display=swap">\n'
        "<style>" + _HTML_CSS + "</style>\n</head>\n<body>\n"
        '<header class="mast"><div class="eyebrow">TAOM, generated by tools/rebalance_ranged_ladders.py from the troop files and the live Armory</div>'
        f"<h1>{title}</h1>"
        "<p>Every troop that carries a bow or crossbow, per kingdom in the ladder's rank order, with the skill the troop "
        "brings and the reach the weapon gives. Reach is the launcher's <code>missile_speed</code>; skill never changes it. "
        "Click a column to sort; the search box and the class boxes filter every table.</p></header>\n"
        "<main>\n"
        '<div class="strip">'
        f"<div><b>{len(all_rows)}</b><span>ranged troop rows ({len(set(r['id'] for r in all_rows))} troops)</span></div>"
        f"<div><b>{n_lines}</b><span>kingdom lines</span></div>"
        f"<div><b>{min(speeds) if speeds else 0} to {max(speeds) if speeds else 0}</b><span>missile speed span</span></div>"
        f"<div><b>{min(reaches) if reaches else 0:.0f} to {max(reaches) if reaches else 0:.0f} m</b><span>reach span, flat ground</span></div>"
        f"<div><b>{len(ctx['before'])}</b><span>ladder inversions now</span></div>"
        f"<div><b>{len(ctx['edits'])}</b><span>roster edits pending</span></div>"
        "</div>\n"
        '<div class="read">'
        "<div><div class=\"eyebrow\">What the weapon decides</div>"
        "<p><b>Speed</b> is the launcher's <code>missile_speed</code>, the one number that sets how far an arrow flies. "
        "<b>Reach</b> is that speed run through the engine's drag model on flat ground (gravity 9.806, "
        "<code>AirFrictionArrow</code> 0.003, best elevation); the native range function is closed, so read it as relative. "
        "<b>Acc</b> is the launcher's <code>accuracy</code>. <b>Bow dmg</b> and <b>Ammo dmg</b> add at launch "
        "(<code>Mission.cs:4932</code>), so <b>Total dmg</b> is what the arrow leaves the string with; <b>Shots</b> is the "
        "quiver's <code>stack_amount</code>.</p></div>"
        "<div><div class=\"eyebrow\">What the skill decides</div>"
        "<p><b>Bow</b> / <b>Xbow</b> are the troop's skills. <b>Spread</b> is the engine's <code>WeaponInaccuracy</code> "
        "x1000, <code>(100 - accuracy) x (1 - f x skill)</code> with f 0.0009 for a bow and 0.0005 for a crossbow "
        "(<code>DefaultSkillEffects</code>): lower is tighter, and it is the only place the "
        "skill meets the weapon. <b>Cadence</b> is <code>AiShootFreq</code>, <code>0.3 + 0.7 x aiLevel</code> with "
        "<code>aiLevel = skill / 300 x 0.96</code> at Normal combat AI. <b>Opens m</b>, mounted troops only, is the "
        "distance a horse archer starts shooting from, <code>reach x (0.3 + 0.4 x aiLevel)</code>; foot archers use the "
        "full reach. A troop with several battle sets shows its fastest launcher; <code>(+n alt)</code> counts the others.</p></div>"
        "</div>\n"
        '<nav class="kingdoms" aria-label="Kingdoms">' + nav + "</nav>\n"
        '<div class="controls"><input type="search" id="q" placeholder="Filter by troop, item, ammo" aria-label="Filter rows">'
        '<label><input type="checkbox" id="f-bow" checked> Bows</label>'
        '<label><input type="checkbox" id="f-xbow" checked> Crossbows</label></div>\n'
        + "\n".join(sections) + "\n"
        '<p class="foot">The row under each kingdom is its ladder, one cell per tier it fields: '
        "speed / damage / accuracy of the tier's item and the Bow or Crossbow the troop carries, from "
        "<code>tools/ranged_ladders.json</code>. The band (E tier 0 to 2, R 3 to 4, V 5 to 6, X 7 to 8, C 9 to 10) "
        "only picks which donor bow the tier's item looks like. "
        "Rain or snow cut bow and crossbow speed by 10% and fog cuts range by 20% in the mission; neither is in these numbers.</p>\n"
        "</main>\n<script>" + _HTML_JS + "</script>\n</body>\n</html>\n"
    )


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
    ap.add_argument("--report-dir", default=str(REPORT_DIR), help="where REPORT.md, REPORT.html and the JSON go")
    ap.add_argument("--docs-html", default=str(DOCS_HTML),
                    help="the tracked copy of REPORT.html (default %(default)s); '-' skips it")
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
    printed = list(failures)
    problems += rl.validate_spec(spec, launchers, cultures=rl.troop_file_cultures(args.moduledata))
    if problems:
        print("ERROR: the spec contradicts itself or the install; nothing was written:")
        for p in sorted(set(problems)):
            print(f"  - {p}")
        return 2

    troops = rl.load_ranged_troops(args.moduledata, failures=failures)
    retired = rl.retired_ladder_launchers(troops, launchers)
    # A CURRENT cell has the retired-id shape too. Planned as a placeholder it would pass the
    # "item must exist" guard below, so a roster naming a cell no file defines (the generator never
    # ran, or an Armory reinstall removed the items) would read clean and gain more troops.
    planned_ids = {i.id for i in rl.planned_items(spec)}
    undefined = sorted(i for i in retired if i in planned_ids)
    if undefined:
        print(f"ERROR: the rosters name {len(undefined)} ladder cell(s) no item file defines; run "
              f"tools/generate_ranged_ladder_items.py --apply (or the Armory was reinstalled), then "
              f"restart the game. Nothing was written. First few: {', '.join(undefined[:6])}")
        return 2
    if retired:
        # The generator already replaced these ids; the rosters still name them. Plan them as
        # launchers so every slot is repointed, never left naming an item that no longer exists.
        print(f"NOTE: {len(retired)} retired ladder id(s) still named by the rosters will be repointed "
              f"(first few: {', '.join(sorted(retired)[:4])})")
        launchers = {**launchers, **retired}
    ammo = rl.index_ammo(rl.default_item_roots(game_modules, args.moduledata), failures=failures)
    barred = rl.mount_barred_usages(game_modules)
    if barred is None:
        print("WARNING: no item_usage_sets.xml under the Modules folder; the mounted-usage check is skipped")
    for f in failures:
        if f not in printed:
            print(f"WARNING: {f}")
    try:
        # Fail closed, like rebalance_troops: without the bindings the clamp would treat every
        # militia promotion as an ordinary edge and raise it.
        militia = rb.militia_troop_ids(args.moduledata)
    except RuntimeError as exc:
        print(f"ERROR: {exc} Nothing was written.")
        return 2
    sources = rl.load_upgrade_sources(args.moduledata)
    try:
        ctx = build_context(spec, launchers, troops, game_modules, args.moduledata, ammo, barred,
                            militia=militia, exempt=rb.RESPECIALIZATION_EXEMPT_EDGES, sources=sources)
    except rl.LadderError as exc:
        print(f"ERROR: {exc}")
        return 2

    os.makedirs(args.report_dir, exist_ok=True)
    md = render_report(ctx)
    with open(os.path.join(args.report_dir, REPORT_MD), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(md)
    with open(os.path.join(args.report_dir, REPORT_JSON), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(build_json(ctx), fh, indent=2)
    html = render_html(ctx)
    with open(os.path.join(args.report_dir, REPORT_HTML), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(html)
    if args.docs_html != "-":
        os.makedirs(os.path.dirname(args.docs_html), exist_ok=True)
        with open(args.docs_html, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(html)
    print(f"Launchers: {len(launchers)}. Ranged troops: "
          f"{sum(1 for t in troops.values() if rl.troop_launchers(t, launchers))}. "
          f"Inversions: {len(ctx['before'])} before, {len(ctx['after'])} after. "
          f"Pending edits: {len(ctx['edits'])} slots, {len(ctx['skill_edits'])} skills. Reports: {args.report_dir}")
    if args.stdout:
        print(md.split("## Per line")[0])

    if not args.apply:
        return 0
    edits, skill_edits = ctx["edits"], ctx["skill_edits"]
    if not edits and not skill_edits:
        print("Nothing to apply: every launcher slot and skill already carries its cell.")
        return 0
    missing = sorted({e.new for e in edits} - set(launchers))
    if missing:
        print(f"ERROR: {len(missing)} ladder item(s) the rosters would name are not in the launcher "
              f"index; run tools/generate_ranged_ladder_items.py --apply first, then restart the game. "
              f"Nothing was written. First few: {', '.join(missing[:6])}")
        return 2
    written = 0
    if edits:
        changes = [{"file": e.file, "troop": e.troop, "slot": e.slot, "old": e.old, "new": e.new} for e in edits]
        written = fx.write_changes(changes)
    skill_files = write_skills(troops, skill_edits)
    after = rl.load_ranged_troops(args.moduledata)
    remaining = rl.planned_edits(after, launchers, spec, barred)
    remaining_skills = rl.planned_skill_edits(after, launchers, spec, militia=militia,
                                              exempt_edges=rb.RESPECIALIZATION_EXEMPT_EDGES, sources=sources)
    print(f"Applied {len(edits)} slot edits in {written} file(s) and {len(skill_edits)} skill values in "
          f"{skill_files} file(s); {len(remaining)} slot and {len(remaining_skills)} skill edit(s) still pending.")
    return 0 if not remaining and not remaining_skills else 1


def write_skills(troops: dict, skill_edits: list) -> int:
    """Write the planned Bow / Crossbow values through rebalance_troops' byte-faithful writer. Each
    troop gets its full declared skill set back with only the planned values replaced, so no other
    skill moves. Returns the number of files written."""
    by_file: dict[str, dict] = defaultdict(dict)
    for s in skill_edits:
        full = by_file[s.file].setdefault(s.troop, dict(troops[s.troop].skills))
        full[s.skill] = s.new
    for path, troop_map in by_file.items():
        rb.apply_skills_via_regex(path, troop_map)
    return len(by_file)


if __name__ == "__main__":
    sys.exit(main())
