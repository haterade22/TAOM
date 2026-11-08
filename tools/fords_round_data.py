#!/usr/bin/env python3
"""Writes the round robin matches of one build as per-troop records, so a round survives the loss of its run folders.

    python tools/fords_round_data.py --build SHA --out data.json [--runs-root E:/Temp/fords-runs]

A match counts exactly as `tools/fords_round_robin.py summary` counts it (the same function picks the runs). Each record
holds the outcome, the towers lost and when each fell, the tower damage per class, the heroes fielded and, per troop of
each side, how many spawned, killed and died. A troop id both factions field is left out, as the round robin leaves it
out, and kills with no known killer (mostly tower volleys) are kept as one number. The committed rounds are in
docs/features/fords-of-war/rounds/."""
import argparse
import json
import sys
import tempfile
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from tools import fords_match_review as review  # noqa: E402
from tools import fords_round_robin as robin  # noqa: E402

UNKNOWN = "(unknown)"


def match_record(facts, run):
    """One match as the fit reads it."""
    troops = {}
    for troop, side in facts["faction_of"].items():
        if side not in review.SIDES:
            continue
        spawned = facts["spawned"].get(troop, 0)
        row = facts["troops"].get(troop)
        if not spawned and not row:
            continue
        row = row or {}
        troops[troop] = {"side": side, "spawned": spawned, "kills": row.get("kills", 0), "deaths": row.get("deaths", 0)}
    return {
        "run": run, "west": facts["west"], "east": facts["east"], "winner": facts["winner"], "minutes": facts["mt"] / 60.0,
        "on_time": bool(facts.get("on_time")), "towers_lost": dict(facts["towers_lost"]), "deaths": dict(facts["deaths"]),
        "unknown_kills": facts["troops"].get(UNKNOWN, {}).get("kills", 0), "troops": troops,
        "tower_damage": {side: _classes(facts.get("tower_classes", {}).get(side, {})) for side in review.SIDES},
        "heroes": dict(facts.get("heroes", dict.fromkeys(review.SIDES, 0))),
        "tower_falls": {tower: None if mt is None else mt / 60.0 for tower, mt in facts.get("tower_falls", {}).items()},
    }


def _classes(damage):
    """Melee, ranged and hero as the game classes tower damage; siege and other together as other."""
    return {"melee": damage.get("melee", 0), "ranged": damage.get("ranged", 0), "hero": damage.get("hero", 0),
            "other": damage.get("siege", 0) + damage.get("other", 0)}


def tower_falls(folder):
    """tower id -> the mission second it fell, or None, from the run's report."""
    reports = sorted(Path(folder).glob("fords_report_*.json"))
    if not reports:
        return {}
    towers = json.loads(reports[0].read_text(encoding="utf-8"))["evidence"].get("Towers", {})
    return {tower: row.get("FellMt") for tower, row in towers.items()}


def _gather(folder, factions_path):
    facts = review.gather(folder, factions_path)
    facts["tower_falls"] = tower_falls(folder)
    return facts


def dataset(build, runs_root=robin.RUNS_ROOT, show=robin.git_show_factions):
    """Every counted match of the build, with the factions.json it was played with."""
    text = show(build)
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / "factions.json"
        path.write_text(text, encoding="utf-8")
        pairs = robin.schedule(robin.factions(path))
        results = robin.results_for(build, pairs, runs_root, path)
        matches = [match_record(_gather(Path(runs_root) / r["run"], path), r["run"]) for r in results]
    return {"build": build, "pairs": len(pairs), "matches": matches}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--build", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--runs-root", default=str(robin.RUNS_ROOT))
    args = parser.parse_args(argv)
    result = dataset(args.build, args.runs_root)
    Path(args.out).write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(f"{len(result['matches'])} of {result['pairs']} pairs on {args.build} written to {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
