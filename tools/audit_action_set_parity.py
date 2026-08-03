#!/usr/bin/env python3
"""Audit EVERY action_set for completeness vs Native's `as_human_warrior` surface.

The dwarf water-CTD (issue #300) was a STANDALONE action_set (no `base_set`) silently
missing engine actions. This tool finds every other set with the same latent gap by
computing each set's EFFECTIVE action surface — its own actions PLUS everything it
inherits through the `base_set` chain PLUS the cross-module field-merge (the engine
merges same-id `<action_set>` nodes across Native + LOTRLOME; see Module.cs
`CreateProcessedActionSetsXMLForNative`) — and comparing it against Native's
`as_human_warrior` active type set.

Only HUMANOID sets are expected to carry the human combat surface. A set is humanoid
if its standalone root resolves to `as_human_warrior` or `as_dwarf_warrior`. Creature
sets (root = `as_spider` / `as_elephant` / `as_chariot`) use creature movement systems
and are reported separately — they must NOT be force-fed human actions.

XML comments are ignored (ElementTree), so Native's ~126 commented-out actions never
count toward the reference surface. Read-only; prints a report and a non-zero-ish summary.

Companion to `tools/patch_dwarf_action_parity.py` (the fixer). Run after every engine
bump (wired into /engine-bump Phase 4).
"""
from __future__ import annotations

import argparse
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

HUMANOID_ROOTS = {"as_human_warrior", "as_dwarf_warrior"}
CREATURE_ROOTS = {"as_spider", "as_elephant", "as_chariot"}


def load(path: str) -> dict:
    """id -> {own:set[type], base:str|None, skel:str|None}. Comments ignored by ET."""
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as exc:
        sys.exit(f"ERROR: cannot read {path}: {exc}")
    d: dict = {}
    for s in root.findall("action_set"):
        sid = s.get("id")
        if not sid:
            continue
        rec = d.setdefault(sid, {"own": set(), "base": None, "skel": None})
        rec["own"] |= {a.get("type") for a in s.findall("action") if a.get("type")}
        if s.get("base_set"):
            rec["base"] = s.get("base_set")
        if s.get("skeleton"):
            rec["skel"] = s.get("skeleton")
    return d


def root_level_actions(path: str) -> list:
    """`<action>` elements parented by `<action_sets>` instead of an `<action_set>`.

    Structurally illegal, but build 1.4.7.117484 tolerates it silently, so it only surfaces
    somewhere else entirely: build 117131 -- which TaleWorlds' DEDICATED SERVER engine ships --
    throws KeyNotFoundException in MBObjectManager.MergeElements at schema path
    /action_sets/action and takes the server down on boot. Reported from multiplayer field
    testing 2026-08-03; 168 such elements existed at the time, from twelve
    `as_<race>_female_villager_in_aserai_tavern` sets authored SELF-CLOSING, which orphaned the
    14 female-conversation overrides that belong nested inside each (vanilla's own
    `as_human_female_villager_in_aserai_tavern` nests exactly those 14).

    Returns the orphaned `type` values so the report can name them.
    """
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as exc:
        sys.exit(f"ERROR: cannot read {path}: {exc}")
    return [a.get("type") or "?" for a in root.findall("action")]


def merge(*universes: dict) -> dict:
    """Field-merge same-id sets across modules (the engine's behavior)."""
    out: dict = {}
    for uni in universes:
        for sid, info in uni.items():
            rec = out.setdefault(sid, {"own": set(), "base": None, "skel": None})
            rec["own"] |= info["own"]
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


def effective(sid: str, universe: dict, memo: dict) -> set:
    if sid in memo:
        return memo[sid]
    memo[sid] = set()  # cycle guard placeholder
    info = universe.get(sid)
    if info is None:
        return set()
    eff = set(info["own"])
    if info["base"]:
        eff |= effective(info["base"], universe, memo)
    memo[sid] = eff
    return eff


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--native", default=DEFAULT_NATIVE)
    ap.add_argument("--live", default=DEFAULT_LIVE)
    ap.add_argument("--show-complete", action="store_true", help="also list the complete humanoid sets")
    args = ap.parse_args()

    native = load(args.native)
    live = load(args.live)
    universe = merge(native, live)

    orphans = root_level_actions(args.live)

    if "as_human_warrior" not in native:
        sys.exit("ERROR: as_human_warrior not found in Native")
    reference = set(native["as_human_warrior"]["own"])  # the parity target (active, comment-free)

    memo: dict = {}
    humanoid_gaps = []
    humanoid_ok = []
    creature = []
    other = []
    missing_base = []

    for sid in sorted(universe):
        info = universe[sid]
        # flag dangling base pointers
        if info["base"] and info["base"] not in universe:
            missing_base.append((sid, info["base"]))
        root = resolve_root(sid, universe)
        if root in HUMANOID_ROOTS:
            missing = reference - effective(sid, universe, memo)
            if missing:
                humanoid_gaps.append((sid, root, sorted(missing)))
            else:
                humanoid_ok.append(sid)
        elif root in CREATURE_ROOTS:
            creature.append((sid, root))
        else:
            other.append((sid, root))

    print(f"Reference: Native as_human_warrior = {len(reference)} active action types")
    print(f"Total action_sets (merged Native+LOTRLOME): {len(universe)}")
    print(f"  humanoid (root as_human_warrior/as_dwarf_warrior): {len(humanoid_ok) + len(humanoid_gaps)}")
    print(f"  creature (root spider/elephant/chariot):           {len(creature)}")
    print(f"  other root:                                        {len(other)}")
    print()

    if humanoid_gaps:
        print(f"!! {len(humanoid_gaps)} HUMANOID set(s) MISSING part of the human surface (need fixing):")
        for sid, root, missing in humanoid_gaps:
            sample = ", ".join(missing[:6])
            print(f"   {sid:36s} root={root:18s} missing {len(missing)}  e.g. {sample}")
    else:
        print("OK: every humanoid set has the full Native as_human_warrior surface (0 gaps).")

    if orphans:
        print()
        print(f"!! {len(orphans)} root-level <action> element(s) in {args.live}:")
        print("   These are parented by <action_sets>, not an <action_set>. Build 117131 (the")
        print("   DEDICATED SERVER engine) throws KeyNotFoundException on this file at boot.")
        print("   Fix: nest each under the action_set it belongs to (make that set non-self-closing).")
        for t in sorted(set(orphans))[:14]:
            print(f"   {t}")

    if missing_base:
        print()
        print(f"WARN: {len(missing_base)} set(s) reference a base_set that doesn't exist in the merged universe:")
        for sid, base in missing_base:
            print(f"   {sid} -> base_set={base}")

    if other:
        print()
        print(f"NOTE: {len(other)} set(s) with a non-human/creature root (inspect if unexpected):")
        for sid, root in other[:20]:
            print(f"   {sid:36s} root={root}")

    print()
    print(f"Creature sets (NOT human-audited — separate creature-mount parity): {len(creature)} "
          f"(roots: {sorted(set(r for _, r in creature))})")

    if args.show_complete:
        print()
        print("Complete humanoid sets:")
        for sid in humanoid_ok:
            print(f"   {sid}")

    # exit non-zero if a real humanoid gap OR a root-level <action> exists, so the engine-bump
    # check can gate on both. The orphan check is cheap and catches a server-only boot crash that
    # no single-player test run can reproduce.
    sys.exit(1 if (humanoid_gaps or orphans) else 0)


if __name__ == "__main__":
    main()
