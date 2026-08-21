#!/usr/bin/env python3
"""Analyse TAOM's war-theater targeting data against the live map.

Read-only by default, per the tools/README.md convention for ``analyze_*``. Pass ``--apply`` to
prune the priority lists it reports as out of reach.

What it answers
---------------
1.  What is G, the engine's average distance between the closest two towns? Everything in the
    ArmyTargeting config is expressed in multiples of it, so a wrong G silently rescales the
    whole feature.
2.  For every ``Hostile`` pair in ``diplomacy.json``: how far apart are the two kingdoms really,
    measured fortification to fortification rather than centroid to centroid? Centroids hid the
    fact that Rohan and Mordor border each other, which is how an early draft of the theater table
    came to sever four genuine fronts.
3.  Which kingdoms would be left with no enemy inside the march radius? Those are the ones whose
    armies gather, fail to find a target, patrol, and get disbanded by ``Army.CheckInactivity``.
4.  Which ``FactionPriorityTargets`` entries now sit beyond the radius? Those are inert: the reach
    falloff pins them at the floor no matter how high their priority boost is. Keeping them in the
    file just misleads the next person to edit it.

Distances
---------
Straight-line between settlement GATE positions, taken from the live navigation snapshot. The game
uses real navmesh path distance, which is never shorter.

That does NOT make every number here an underestimate, because the config is expressed as a ratio
and the same bias sits in both halves of it: this tool divides Euclidean distance by a Euclidean G,
and the engine divides path distance by a path G. Measured, the two Gs are 78.7 and about 94, so the
bias largely cancels and the ratios are comparable.

What does not cancel is terrain. A pair separated by the Misty Mountains has a path far longer than
its straight line, so this tool understates that pair's gap and will keep a priority entry the game
will treat as further away. That is the safe direction for ``--apply``: it can leave a too-far entry
in place, but it will not prune one the game considers near.

Data sources are the LIVE module installs, not the repo shadows. ``Main/_Module/ModuleData/
settlements.xml`` is 125 settlements stale and must not be used here.
"""

from __future__ import annotations

import argparse
import io
import json
import math
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

GAME_DIR = os.environ.get(
    "BANNERLORD_GAME_DIR",
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord",
)

LIVE_SETTLEMENTS = os.path.join(GAME_DIR, "Modules", "TAOM_Map", "ModuleData", "settlements.xml")
LIVE_SNAPSHOT = os.path.join(
    GAME_DIR, "Modules", "TAOM_Map", "ModuleData", "DistanceCaches", "settlements_snapshot.json"
)
VANILLA_CLANS = os.path.join(GAME_DIR, "Modules", "SandBox", "ModuleData", "spclans.xml")

TAOM_CLANS = os.path.join(REPO_ROOT, "Main", "_Module", "ModuleData", "characters", "clans.xml")
TAOM_CLANS_XSLT = os.path.join(REPO_ROOT, "Main", "_Module", "ModuleData", "spclans.xslt")
DIPLOMACY = os.path.join(REPO_ROOT, "Main", "_Module", "ModuleData", "diplomacy", "diplomacy.json")
ARMY_TARGETING = os.path.join(REPO_ROOT, "Main", "_Module", "ModuleData", "configs", "army_targeting.json")


def read_text(path: str) -> str:
    with io.open(path, encoding="utf-8-sig") as handle:
        return handle.read()


def read_json(path: str):
    return json.loads(read_text(path))


def require(path: str, what: str) -> None:
    if not os.path.exists(path):
        sys.exit(
            f"ERROR: {what} not found at {path}\n"
            "  This tool reads the LIVE module installs. Set BANNERLORD_GAME_DIR if your install moved."
        )


# --------------------------------------------------------------------------- loading


def load_clan_to_kingdom() -> dict:
    """clan StringId -> kingdom StringId, merged vanilla then TAOM then the XSLT overrides."""
    mapping = {}
    pattern = re.compile(r'<Faction\b[^>]*?\bid="([^"]+)"[^>]*?\bsuper_faction="Kingdom\.([^"]+)"', re.S)
    reverse = re.compile(r'<Faction\b[^>]*?\bsuper_faction="Kingdom\.([^"]+)"[^>]*?\bid="([^"]+)"', re.S)

    for path in (VANILLA_CLANS, TAOM_CLANS, TAOM_CLANS_XSLT):
        if not os.path.exists(path):
            continue
        text = read_text(path)
        for clan, kingdom in pattern.findall(text):
            mapping[clan] = kingdom
        for kingdom, clan in reverse.findall(text):
            mapping.setdefault(clan, kingdom)

    # The XSLT rewrites a clan inside an <xsl:template match="Faction[@id='X']"> block, where the
    # emitted <Faction> carries no id of its own. Bind the match id to whatever super_faction the
    # block emits.
    if os.path.exists(TAOM_CLANS_XSLT):
        text = read_text(TAOM_CLANS_XSLT)
        for block in re.findall(
            r"<xsl:template\s+match=\"Faction\[@id='([^']+)'\]\"(.*?)</xsl:template>", text, re.S
        ):
            clan, body = block
            found = re.search(r'super_faction="Kingdom\.([^"]+)"', body)
            if found:
                mapping[clan] = found.group(1)

    return mapping


def load_settlements(clan_to_kingdom: dict) -> dict:
    """settlement StringId -> {kingdom, kind, owner, bound}."""
    root = ET.parse(LIVE_SETTLEMENTS).getroot()
    raw = {}

    # Only DIRECT children are settlements. The nested <Town>/<Village>/<Hideout> under
    # <Components> are component definitions and carry ids of their own (castle_comp_A1 and
    # friends); iterating the whole tree picks up 828 of those and reports them as unowned.
    for element in root:
        if element.tag != "Settlement":
            continue
        sid = element.get("id")
        if not sid:
            continue

        owner = element.get("owner") or ""
        clan = owner.split(".")[-1] if owner else None

        # The component element is authoritative for the kind. is_castle="true" on a <Town> is
        # what separates the map's 143 castles from its 78 towns; the id prefix agrees today but
        # is a naming convention, not data.
        kind = "unknown"
        bound = ""
        components = element.find("Components")
        if components is not None:
            for component in components:
                if component.tag == "Town":
                    kind = "castle" if (component.get("is_castle") or "").lower() == "true" else "town"
                elif component.tag == "Village":
                    kind = "village"
                elif component.tag == "Hideout":
                    kind = "hideout"
                # bound="Settlement.castle_A11" sits on the COMPONENT, not on the settlement.
                bound = component.get("bound") or ""
                break

        raw[sid] = {
            "kingdom": clan_to_kingdom.get(clan) if clan else None,
            "kind": kind,
            "owner": clan,
            "bound": bound.split(".")[-1] if bound else None,
        }

    # Villages carry no owner; they inherit from the fortification they are bound to.
    for sid, data in raw.items():
        if data["kingdom"] is None and data["bound"]:
            parent = raw.get(data["bound"])
            if parent:
                data["kingdom"] = parent["kingdom"]

    return raw


def load_gate_positions() -> dict:
    """settlement StringId -> (x, y, is_fortification), from the live navigation snapshot."""
    snapshot = read_json(LIVE_SNAPSHOT)
    positions = {}
    for entry in snapshot.get("Settlements", []):
        sid = entry.get("Id")
        if not sid:
            continue
        positions[sid] = (
            float(entry.get("GateX", 0.0)),
            float(entry.get("GateY", 0.0)),
            bool(entry.get("IsFortification", False)),
        )
    return positions


def load_hostile_pairs() -> list:
    data = read_json(DIPLOMACY)
    pairs = []
    for row in data.get("relationships", []):
        if row.get("tier") == "Hostile":
            pairs.append((row["kingdomA"], row["kingdomB"]))
    return pairs


# --------------------------------------------------------------------------- geometry


def distance(a, b) -> float:
    return math.hypot(a[0] - b[0], a[1] - b[1])


def average_town_gap(positions: dict, settlements: dict) -> tuple:
    """Engine-faithful G: mean over towns of the distance to that town's nearest other town."""
    towns = [
        (sid, positions[sid][:2])
        for sid in settlements
        if settlements[sid]["kind"] == "town" and sid in positions
    ]
    if len(towns) < 2:
        sys.exit("ERROR: fewer than two towns resolved; the settlement parse is wrong.")

    total = 0.0
    for sid, pos in towns:
        nearest = min(distance(pos, other) for other_id, other in towns if other_id != sid)
        total += nearest
    return total / len(towns), len(towns)


def fortifications_by_kingdom(settlements: dict, positions: dict) -> dict:
    result = defaultdict(list)
    for sid, data in settlements.items():
        if data["kind"] not in ("town", "castle"):
            continue
        if not data["kingdom"] or sid not in positions:
            continue
        result[data["kingdom"]].append((sid, positions[sid][:2]))
    return result


def min_border_gap(forts_a: list, forts_b: list) -> tuple:
    best = (float("inf"), None, None)
    for sid_a, pos_a in forts_a:
        for sid_b, pos_b in forts_b:
            d = distance(pos_a, pos_b)
            if d < best[0]:
                best = (d, sid_a, sid_b)
    return best


def nearest_fort_distance(target_pos, forts: list) -> float:
    if not forts:
        return float("inf")
    return min(distance(target_pos, pos) for _sid, pos in forts)


# --------------------------------------------------------------------------- reporting


def theater_verdict(config: dict, attacker: str, target: str) -> str:
    table = config.get("KingdomTheaters", {})
    a = table.get(attacker)
    b = table.get(target)
    if a is None or b is None:
        return "neutral (absent from table)"
    if not a or not b:
        return "neutral (passive)"
    if a[0] in b:
        return f"primary ({a[0]})"
    shared = [t for t in a[1:] if t in b]
    if shared:
        return f"secondary ({shared[0]})"
    return "foreign"


def reach_multiplier(config: dict, gaps: float) -> float:
    inner = float(config.get("ReachInnerRadiusInTownGaps", 1.5))
    radius = float(config.get("ReachRadiusInTownGaps", 3.0))
    floor = float(config.get("ReachFloor", 0.05))
    inner = min(inner, radius * 0.5)
    if gaps <= inner:
        return 1.0
    if gaps >= radius:
        return floor
    return 1.0 - ((gaps - inner) / (radius - inner)) * (1.0 - floor)


def compact_arrays(dumped):
    """Collapse arrays of scalars onto one line.

    json.dumps(indent=2) puts every settlement id on its own row, which turns a config a human
    maintains into three times the lines with one word on each. Objects keep their indentation;
    only arrays whose elements are all scalars are folded, and only when the result still fits.

    Written as a line scan rather than a regex on purpose: the pattern needs literal brackets and
    newlines, and getting those escapes wrong is a silent corruption of the config this rewrites.
    """
    out = []
    source = dumped.split(chr(10))
    index = 0
    while index < len(source):
        line = source[index]
        if not line.rstrip().endswith("["):
            out.append(line)
            index += 1
            continue

        close = index + 1
        items = []
        scalar = True
        while close < len(source) and source[close].strip() not in ("]", "],"):
            token = source[close].strip().rstrip(",")
            if token.startswith("{") or token.startswith("["):
                scalar = False
            items.append(token)
            close += 1

        indent = line[: len(line) - len(line.lstrip())]
        joined = ", ".join(items)
        fits = len(line) + len(joined) + 2 <= 110
        if scalar and close < len(source) and fits:
            trailing = "," if source[close].strip() == "]," else ""
            out.append(line.rstrip() + joined + "]" + trailing)
            index = close + 1
        else:
            out.append(line)
            index += 1

    return chr(10).join(out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true",
                        help="prune FactionPriorityTargets entries that sit beyond the march radius")
    parser.add_argument("--radius", type=float, default=None,
                        help="override the march radius in town gaps (default: read from army_targeting.json)")
    args = parser.parse_args()

    require(LIVE_SETTLEMENTS, "live TAOM_Map settlements.xml")
    require(LIVE_SNAPSHOT, "live settlements_snapshot.json")
    require(DIPLOMACY, "diplomacy.json")
    require(ARMY_TARGETING, "army_targeting.json")

    config = read_json(ARMY_TARGETING)
    if args.radius is not None:
        config["ReachRadiusInTownGaps"] = args.radius
    radius = float(config.get("ReachRadiusInTownGaps", 3.0))

    clan_to_kingdom = load_clan_to_kingdom()
    settlements = load_settlements(clan_to_kingdom)
    positions = load_gate_positions()

    gap, town_count = average_town_gap(positions, settlements)
    forts = fortifications_by_kingdom(settlements, positions)

    print("=" * 78)
    print("TAOM war-theater analysis")
    print("=" * 78)
    print(f"settlements parsed      : {len(settlements)} (live TAOM_Map)")
    print(f"clans resolved          : {len(clan_to_kingdom)}")
    print(f"kingdoms with forts     : {len(forts)}")
    print(f"average town gap (G)    : {gap:.2f} map units over {town_count} towns")
    print(f"march radius            : {radius} G  =  {radius * gap:.0f} map units")
    print("NOTE straight-line gate distance. Both halves of the ratio carry the same bias, so these")
    print("     numbers track the engine's; terrain is what does not cancel, and it only ever makes")
    print("     the real gap larger. Safe direction for --apply.")
    print()

    unresolved = [
        s for s, d in settlements.items()
        if d["kingdom"] is None and d["kind"] not in ("hideout", "unknown")
    ]
    if unresolved:
        print(f"WARNING {len(unresolved)} non-hideout settlements did not resolve to a kingdom, e.g. "
              f"{', '.join(sorted(unresolved)[:5])}")
        print()

    # ----------------------------------------------------------------- hostile pairs
    print("-" * 78)
    print("HOSTILE PAIRS  (minimum fortification-to-fortification gap)")
    print("-" * 78)
    print(f"{'kingdom A':<20}{'kingdom B':<20}{'gap':>9}{'G':>7}  {'reach':>6}  theater")

    hostile = load_hostile_pairs()
    reachable_enemies = defaultdict(list)
    rows = []
    for a, b in hostile:
        d, _sa, _sb = min_border_gap(forts.get(a, []), forts.get(b, []))
        gaps = d / gap if d != float("inf") else float("inf")
        rows.append((gaps, a, b, d))
        if gaps <= radius:
            reachable_enemies[a].append(b)
            reachable_enemies[b].append(a)

    for gaps, a, b, d in sorted(rows):
        shown = f"{d:9.1f}" if d != float("inf") else "     n/a "
        gap_shown = f"{gaps:7.2f}" if gaps != float("inf") else "    inf"
        mult = reach_multiplier(config, gaps) if gaps != float("inf") else float(config.get("ReachFloor", 0.05))
        print(f"{a:<20}{b:<20}{shown}{gap_shown}  {mult:6.3f}  {theater_verdict(config, a, b)}")

    print()
    print(f"{len(hostile)} hostile pairs, "
          f"{sum(1 for g, _a, _b, _d in rows if g <= radius)} of them inside the march radius")
    print()

    # ----------------------------------------------------------------- stranded kingdoms
    print("-" * 78)
    print("KINGDOMS WITH NO HOSTILE NEIGHBOUR INSIDE THE RADIUS")
    print("-" * 78)
    belligerents = sorted({k for pair in hostile for k in pair})
    stranded = [k for k in belligerents if not reachable_enemies.get(k)]
    if stranded:
        for k in stranded:
            nearest = min((g for g, a, b, _d in rows if k in (a, b)), default=float("inf"))
            near_txt = f"{nearest:.2f} G" if nearest != float("inf") else "unreachable"
            print(f"  {k:<20} nearest hostile at {near_txt}")
        print()
        print("  These kingdoms gather an army, find no target inside the radius, patrol, and lose")
        print("  the army to Army.CheckInactivity about two days later. The soft theater weighting")
        print("  keeps their score above zero, but distance alone will still starve them.")
    else:
        print("  none")
    print()

    # ----------------------------------------------------------------- priority lists
    print("-" * 78)
    print("PRIORITY-LIST ENTRIES BEYOND THE MARCH RADIUS  (inert: pinned at the reach floor)")
    print("-" * 78)

    priority = config.get("FactionPriorityTargets", {})
    prunable = defaultdict(list)
    kept = defaultdict(list)

    for faction, targets in priority.items():
        own = forts.get(faction, [])
        for target in targets or []:
            if target not in positions:
                print(f"  {faction:<12} {target:<16} UNRESOLVED settlement id")
                prunable[faction].append(target)
                continue
            d = nearest_fort_distance(positions[target][:2], own)
            gaps = d / gap if d != float("inf") else float("inf")
            if gaps > radius:
                prunable[faction].append(target)
                gap_txt = f"{gaps:.2f}" if gaps != float("inf") else "inf"
                owner = settlements.get(target, {}).get("kingdom", "?")
                print(f"  {faction:<12} {target:<16} {gap_txt:>6} G  owner={owner}")
            else:
                kept[faction].append(target)

    total_entries = sum(len(v or []) for v in priority.values())
    total_prunable = sum(len(v) for v in prunable.values())
    print()
    print(f"{total_prunable} of {total_entries} priority entries are beyond {radius} G and do nothing.")
    print()

    if args.apply:
        for faction in list(priority.keys()):
            survivors = kept.get(faction, [])
            if survivors:
                priority[faction] = survivors
            else:
                del priority[faction]
                print(f"  removed empty priority list for {faction}")
        config["FactionPriorityTargets"] = priority
        if args.radius is not None:
            config["ReachRadiusInTownGaps"] = read_json(ARMY_TARGETING).get("ReachRadiusInTownGaps", 3.0)
        # Preserve the file's existing line endings and BOM rather than normalising them, so
        # the diff shows the entries that changed instead of every line in the file.
        original = open(ARMY_TARGETING, "rb").read()
        had_bom = original.startswith(b"\xef\xbb\xbf")
        newline = "\r\n" if b"\r\n" in original else "\n"
        body = compact_arrays(json.dumps(config, indent=2, ensure_ascii=False)) + "\n"
        if newline != "\n":
            body = body.replace("\n", newline)
        open(ARMY_TARGETING, "wb").write((b"\xef\xbb\xbf" if had_bom else b"") + body.encode("utf-8"))
        print(f"APPLIED: pruned {total_prunable} entries and rewrote {ARMY_TARGETING}")
        print("Re-run the test suite: the config invariant tests read this file directly.")
    elif total_prunable:
        print("Re-run with --apply to prune them.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
