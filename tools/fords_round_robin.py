#!/usr/bin/env python3
"""Plays every Fords of War faction against every other once, and sums the results per faction, so balance is judged on all
pairs and not on one match.

    python tools/fords_round_robin.py schedule               every pair once, one per line: "<west> <east>"
    python tools/fords_round_robin.py todo --build SHA       the pairs with no finished match on that build yet
    python tools/fords_round_robin.py summary --build SHA    the per-faction table of that build's matches, in Markdown

With an odd number of factions each one plays West in exactly half its matches, so the map side does not skew a faction's
numbers. A match counts for the build whose commit its report names (`taom` in fords_report_*.json); the newest run of a
pair counts once. A match with no winner is a draw. Gold trade is the gold value a faction killed divided by what it lost,
summed over its matches. It reads the run folders under E:/Temp/fords-runs through tools/fords_match_review.py.
"""
import argparse
import json
import sys
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from tools import fords_match_review as review  # noqa: E402

RUNS_ROOT = Path("E:/Temp/fords-runs")
COLUMNS = ("Faction", "Played", "Won", "Lost", "Draw", "Won by keep", "Won on time", "Gold trade", "Towers taken", "Towers lost", "Minutes")


def factions(path=review.FACTIONS):
    return list(json.loads(Path(path).read_text(encoding="utf-8"))["factions"])


def schedule(names):
    """Every unordered pair once, as (west, east). For an odd count, the earlier faction is West when the gap is odd."""
    pairs = []
    for i, first in enumerate(names):
        for j in range(i + 1, len(names)):
            second = names[j]
            pairs.append((first, second) if (j - i) % 2 == 1 else (second, first))
    return pairs


def todo(pairs, results):
    done = {frozenset((r["west"], r["east"])) for r in results}
    return [p for p in pairs if frozenset(p) not in done]


def _blank():
    return {"played": 0, "wins": 0, "losses": 0, "draws": 0, "keep_wins": 0, "time_wins": 0, "killed": 0, "lost": 0,
            "towers_taken": 0, "towers_lost": 0, "seconds": 0.0}


def tally(results):
    rows = {}
    for r in results:
        for side, other in (("West", "East"), ("East", "West")):
            row = rows.setdefault(r["west"] if side == "West" else r["east"], _blank())
            row["played"] += 1
            row["seconds"] += r["mt"]
            row["killed"] += r["killed"][side]
            row["lost"] += r["lost"][side]
            row["towers_lost"] += r["towers_lost"][side]
            row["towers_taken"] += r["towers_lost"][other]
            if r["winner"] is None:
                row["draws"] += 1
            elif r["winner"] == side:
                row["wins"] += 1
                row["keep_wins" if r["keep_fell"] is not None else "time_wins"] += 1
            else:
                row["losses"] += 1
    for row in rows.values():
        row["trade"] = row["killed"] / row["lost"] if row["lost"] else float("inf")
        row["minutes"] = row["seconds"] / row["played"] / 60.0
    return rows


def table(rows):
    lines = ["| " + " | ".join(COLUMNS) + " |", "|" + "---|" * len(COLUMNS)]
    order = sorted(rows.items(), key=lambda item: (-(item[1]["wins"] / item[1]["played"]), -item[1]["trade"], item[0]))
    for name, r in order:
        cells = (name, r["played"], r["wins"], r["losses"], r["draws"], r["keep_wins"], r["time_wins"], f"{r['trade']:.2f}",
                 r["towers_taken"], r["towers_lost"], f"{r['minutes']:.1f}")
        lines.append("| " + " | ".join(str(c) for c in cells) + " |")
    return "\n".join(lines)


def results_for(build, runs_root=RUNS_ROOT):
    """The newest finished run of each pair whose report names that build."""
    newest = {}
    for folder in sorted(Path(runs_root).iterdir()):
        if not folder.is_dir() or not list(folder.glob("fords_report_*.json")):
            continue
        facts = review.gather(folder)
        if not facts or build not in (facts.get("build") or "") or facts.get("crashed") or "mt" not in facts:
            continue
        values = review._side_values(facts)
        newest[frozenset((facts["west"], facts["east"]))] = {
            "run": folder.name, "west": facts["west"], "east": facts["east"], "winner": facts.get("winner"), "mt": facts["mt"],
            "keep_fell": facts.get("keep_fell"), "killed": {s: values[s]["killed"] for s in review.SIDES},
            "lost": {s: values[s]["lost"] for s in review.SIDES}, "towers_lost": facts["towers_lost"]}
    return list(newest.values())


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("command", choices=("schedule", "todo", "summary"))
    parser.add_argument("--build", help="a commit sha, or its first characters, that the reports must name")
    parser.add_argument("--runs-root", default=str(RUNS_ROOT))
    args = parser.parse_args(argv)
    pairs = schedule(factions())
    if args.command == "schedule":
        print("\n".join(f"{w} {e}" for w, e in pairs))
        return 0
    if not args.build:
        parser.error("--build is required for todo and summary")
    results = results_for(args.build, args.runs_root)
    if args.command == "todo":
        print("\n".join(f"{w} {e}" for w, e in todo(pairs, results)))
        return 0
    print(f"{len(results)} of {len(pairs)} pairs played on build {args.build}.\n")
    print(table(tally(results)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
