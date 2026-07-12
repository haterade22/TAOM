#!/usr/bin/env python3
"""Audit every race's CIVILIAN action-set FAMILY against human.

Companion axis to tools/audit_action_set_parity.py:
  * audit_action_set_parity.py -> within ONE set, are all human_warrior action TYPES
                                  present? (combat completeness inside a set)
  * THIS tool                  -> does a RACE own all the human CIVILIAN action-SETS
                                  (as_<race>_villager / _lord / _beggar / _carry_* /
                                  _map ...)? (family coverage across the race)

Why it matters: at settlement spawn the engine builds a townsfolk idle-role set by
GENERATING the name `as_<baseMonster>[_female]_<suffix>` (ActionSetCode.
GenerateActionSetNameWithSuffix). If `as_<race>_<suffix>` does not exist, the lookup
silently falls back to ONE default set, so every NPC of that role plays the SAME
animation. Elves shipped with ONLY the two facegen sets -> every elf civilian shared
one idle in every town (the bug this audit was written for). This tool finds any race
with the same latent gap.

Reference family = every `as_human_*` / `as_human_female_*` set in Native EXCEPT the
pure-combat bases + facegen (see EXCLUDE). The per-race check asks, for each reference
suffix S, whether `as_<race>_<S>` (and the female twin, where human has one) exists in
the merged Native+LOTRLOME universe -- the same field-merge the engine does.

Read-only. Prints a per-race coverage table + each race's skeleton root. Exits non-zero
if any SETTLEMENT race has a gap. Re-run after every engine bump (a new human civilian
set becomes a new per-race gap). Fixer: tools/generate_race_civilian_action_sets.py.
"""
from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET

DEFAULT_NATIVE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\Native\ModuleData\action_sets.xml"
)
DEFAULT_LIVE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml"
)

# Human sets that are NOT civilian idle-roles -> not part of the reference family.
#   *_warrior / *_hideout_bandit : combat bases (the race monster references these directly)
#   *_facegen                    : Character Creation only, handled by its own checklist
EXCLUDE = {
    "as_human_warrior", "as_human_female_warrior",
    "as_human_hideout_bandit", "as_human_female_hideout_bandit",
    "as_human_facegen", "as_human_female_facegen",
}

# TAOM humanoid races (skins.xml + the README facegen checklist + the sauron NPC clone).
# Split by whether they populate SETTLEMENTS (must have the civilian family) vs
# creatures / combat-only spawns that never stand around as townsfolk (family N/A).
SETTLEMENT_RACES = [
    "human", "elf", "dwarf", "orc", "uruk", "uruk_hai", "goblin",
    "dg_uruk", "pale_uruk", "berserker", "nazghul", "saruman", "sauron",
]
NON_SETTLEMENT_RACES = ["cave_troll", "hill_troll"]

# The ONLY settlement race with its OWN skeleton (root as_dwarf_warrior / skel dwarf_skeleton_a);
# also the only extra root in audit_action_set_parity.HUMANOID_ROOTS. Everything else is a
# human-skeleton reskin (root as_human_warrior) -- incl. elf/sauron, which use as_human_warrior
# directly. A missing set for an own-skeleton race must alias to that race's OWN family (never a
# human clip on a non-human rig -- the 1.4.6 water-CTD class); every other race aliases to as_human_*.
# If a future race gets its own skeleton, add it here (confirm via tools/audit_action_set_parity.py).
OWN_SKELETON_RACES = {"dwarf"}


def load(path: str) -> dict:
    """id -> {base, skel}. Comments ignored by ET (disabled sets never count)."""
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as exc:
        sys.exit(f"ERROR: cannot read {path}: {exc}")
    d: dict = {}
    for s in root.findall("action_set"):
        sid = s.get("id")
        if not sid:
            continue
        rec = d.setdefault(sid, {"base": None, "skel": None})
        if s.get("base_set"):
            rec["base"] = s.get("base_set")
        if s.get("skeleton"):
            rec["skel"] = s.get("skeleton")
    return d


def merge(*universes: dict) -> dict:
    out: dict = {}
    for uni in universes:
        for sid, info in uni.items():
            rec = out.setdefault(sid, {"base": None, "skel": None})
            if info["base"]:
                rec["base"] = info["base"]
            if info["skel"]:
                rec["skel"] = info["skel"]
    return out


def resolve_root(sid: str, universe: dict) -> str:
    seen: set = set()
    cur = sid
    while True:
        info = universe.get(cur)
        if info is None or not info["base"] or cur in seen:
            return cur
        seen.add(cur)
        cur = info["base"]


def reference_suffixes(native: dict):
    """(male_suffixes, female_suffixes) of the human civilian family, from Native."""
    male, female = set(), set()
    for sid in native:
        if sid in EXCLUDE or sid.endswith("_facegen"):  # facegen is CC-only + doesn't chase base_set
            continue
        if sid.startswith("as_human_female_"):
            female.add(sid[len("as_human_female_"):])
        elif sid.startswith("as_human_"):
            male.add(sid[len("as_human_"):])
    return male, female


def discover_races(universe: dict) -> set:
    """Every race token that owns a *_facegen or *_warrior set (drift detection)."""
    races = set()
    for sid in universe:
        m = re.match(r"^as_(.+?)(?:_female)?_facegen$", sid) or re.match(r"^as_(.+?)(?:_female)?_warrior$", sid)
        if m:
            races.add(m.group(1))
    return races


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--native", default=DEFAULT_NATIVE)
    ap.add_argument("--live", default=DEFAULT_LIVE)
    ap.add_argument("--race", help="audit only this race")
    ap.add_argument("--list-missing", action="store_true", help="print each missing set id")
    args = ap.parse_args()

    native = load(args.native)
    universe = merge(native, load(args.live))
    male_ref, female_ref = reference_suffixes(native)

    print(f"Reference human civilian family (Native): {len(male_ref)} male + {len(female_ref)} female sets\n")

    settlement = [args.race] if args.race else SETTLEMENT_RACES
    gaps_found = False

    print(f"{'race':12s} {'skeleton root':22s} {'male have/need':14s} {'female have/need':16s} status")
    print("-" * 88)
    for race in settlement:
        have_m = [s for s in male_ref if f"as_{race}_{s}" in universe]
        miss_m = sorted(male_ref - set(have_m))
        have_f = [s for s in female_ref if f"as_{race}_female_{s}" in universe]
        miss_f = sorted(female_ref - set(have_f))
        root = resolve_root(f"as_{race}_warrior", universe)
        if root == f"as_{race}_warrior" and f"as_{race}_warrior" not in universe:
            root = "(uses as_human_warrior directly)"
        n_missing = len(miss_m) + len(miss_f)
        status = "OK" if n_missing == 0 else f"MISSING {n_missing}"
        if n_missing:
            gaps_found = True
        print(f"{race:12s} {root:22s} {len(have_m):>4d}/{len(male_ref):<9d} "
              f"{len(have_f):>4d}/{len(female_ref):<11d} {status}")
        if args.list_missing and n_missing:
            for s in miss_m:
                print(f"      - as_{race}_{s}")
            for s in miss_f:
                print(f"      - as_{race}_female_{s}")

    print("\nNon-settlement races (civilian family N/A -- never spawn as townsfolk):")
    for race in NON_SETTLEMENT_RACES:
        have_m = sum(1 for s in male_ref if f"as_{race}_{s}" in universe)
        root = resolve_root(f"as_{race}_warrior", universe)
        print(f"   {race:12s} root={root:22s} male civ sets present: {have_m}/{len(male_ref)}")

    known = set(SETTLEMENT_RACES) | set(NON_SETTLEMENT_RACES)
    unknown = sorted(r for r in discover_races(universe) if r not in known)
    if unknown:
        print(f"\nWARN: races with a facegen/warrior set but NOT in this tool's lists "
              f"(add + classify): {', '.join(unknown)}")

    print()
    sys.exit(1 if gaps_found else 0)


if __name__ == "__main__":
    main()
