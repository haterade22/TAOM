#!/usr/bin/env python3
"""Plays every Fords of War faction against every other once, and sums the results per faction, so balance is judged on all
pairs and not on one match.

    python tools/fords_round_robin.py schedule               every pair once, one per line: "<west> <east>"
    python tools/fords_round_robin.py todo --build SHA       the pairs with no finished match on that build yet
    python tools/fords_round_robin.py summary --build SHA    the per-faction table of that build's matches, in Markdown

With an odd number of factions each one plays West in exactly half its matches, so the map side does not skew a faction's
numbers. A match counts for the build whose commit its report names (`taom` in fords_report_*.json); the newest run of a
pair counts once, and only when it is a final report with a winner, a clean build (not `.dirty` or `.nogit`), and a scheduled pair in
its scheduled West and East order. A run with no winner did not finish (the game always names one) and does not count. Gold
trade is the gold value of the units its own troops killed, divided by what it lost; kills with no known killer, such as
tower volleys, count for neither side. A troop id both factions field counts for neither. The schedule and the troop tables
come from the factions.json the build committed (git show), and only a 4x match with both sides AI and no control check
counts, whose log holds exactly one [FordsOfWar][Speed] speed=4 line (a second one means the speed changed during play). It reads the run folders under E:/Temp/fords-runs through tools/fords_match_review.py.
"""
import argparse
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from tools import fords_match_review as review  # noqa: E402

RUNS_ROOT = Path("E:/Temp/fords-runs")
SHA = re.compile(r"\+([0-9a-f]{7,40})$")
COLUMNS = ("Faction", "Played", "Won", "Lost", "Won by keep", "Won on time", "Gold trade", "Towers taken", "Towers lost", "Minutes")


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
    done = {(r["west"], r["east"]) for r in results}
    return [p for p in pairs if tuple(p) not in done]


def _blank():
    return {"played": 0, "wins": 0, "losses": 0, "keep_wins": 0, "time_wins": 0, "killed": 0, "lost": 0,
            "towers_taken": 0, "towers_lost": 0, "seconds": 0.0}


def tally(results):
    rows = {}
    for r in results:
        for side, other in (("West", "East"), ("East", "West")):
            row = rows.setdefault(r["west"] if side == "West" else r["east"], _blank())
            row["played"] += 1
            row["seconds"] += r["mt"]
            row["killed"] += r["values"][side]["killed"]
            row["lost"] += r["values"][side]["lost"]
            row["towers_lost"] += r["towers_lost"][side]
            row["towers_taken"] += r["towers_lost"][other]
            if r["winner"] == side:
                row["wins"] += 1
                row["time_wins" if r["time_win"] else "keep_wins"] += 1
            else:
                row["losses"] += 1
    for row in rows.values():
        row["trade"] = row["killed"] / row["lost"] if row["lost"] else (float("inf") if row["killed"] else None)
        row["minutes"] = row["seconds"] / row["played"] / 60.0
    return rows


def table(rows):
    lines = ["| " + " | ".join(COLUMNS) + " |", "|" + "---|" * len(COLUMNS)]
    order = sorted(rows.items(), key=lambda item: (-(item[1]["wins"] / item[1]["played"]), -(item[1]["trade"] or 0.0), item[0]))
    for name, r in order:
        cells = (name, r["played"], r["wins"], r["losses"], r["keep_wins"], r["time_wins"], "-" if r["trade"] is None else f"{r['trade']:.2f}",
                 r["towers_taken"], r["towers_lost"], f"{r['minutes']:.1f}")
        lines.append("| " + " | ".join(str(c) for c in cells) + " |")
    return "\n".join(lines)


def names_build(taom, build):
    """Whether a report's `taom` field, build.<UTC stamp>+<sha>[.dirty|.nogit], names a clean build starting with that sha."""
    found = SHA.search(taom or "")
    return bool(found) and found.group(1).startswith(build.lower())


def _skip(folder, why):
    print(f"left out {folder.name}: {why}", file=sys.stderr)


def results_for(build, pairs, runs_root=RUNS_ROOT, factions_path=review.FACTIONS):
    """The newest finished run of each scheduled pair, in its scheduled West and East order, whose report names that clean build.
    A run with no winner did not finish (the game always names one) and does not count; neither does a run that was not a
    4x match with both sides AI and no control check, or whose log does not hold exactly one 4x [Speed] line. Folders of other builds are skipped silently; a folder of this build that
    cannot be read or is left out is named on stderr."""
    scheduled = {tuple(p) for p in pairs}
    newest = {}
    for folder in sorted(Path(runs_root).iterdir()):
        reports = sorted(folder.glob("fords_report_*.json")) if folder.is_dir() else []
        if not reports:
            continue
        try:
            if not names_build(json.loads(reports[0].read_text(encoding="utf-8")).get("taom"), build):
                continue
            facts = review.gather(folder, factions_path)
        except (OSError, ValueError, KeyError, AttributeError) as error:
            _skip(folder, f"unreadable ({type(error).__name__}: {error})")
            continue
        if not facts:
            _skip(folder, "no log")
        elif not facts["final"] or facts.get("winner") not in review.SIDES:
            _skip(folder, "it did not finish with a winner")
        elif facts["arm"].get("west_ai") != "1":
            _skip(folder, "only the East side was AI (no west_ai=1 in arm.txt)")
        elif facts["arm"].get("speed") != str(review.BALANCE_SPEED):
            _skip(folder, f"speed was {facts['arm'].get('speed', 'unset')}, not {review.BALANCE_SPEED}")
        elif facts.get("speed_changes") != 1 or facts["speed"] != review.BALANCE_SPEED:
            # A clean match logs one [Speed] line, at the start; more means someone changed the speed during play (Codex, 2026-11-05).
            _skip(folder, f"the logged speed was not one {review.BALANCE_SPEED}x line ({facts.get('speed_changes', 0)} [Speed] line(s), up to {facts['speed']}x)")
        elif "control_check" in facts["arm"]:
            _skip(folder, "a control check ran in it")
        elif (facts["west"], facts["east"]) not in scheduled:
            _skip(folder, f"{facts['west']} v {facts['east']} is not a scheduled pair in that order")
        else:
            newest[(facts["west"], facts["east"])] = {
                "run": folder.name, "west": facts["west"], "east": facts["east"], "winner": facts["winner"], "mt": review.match_seconds(facts),
                "time_win": review.won_on_time(facts), "values": review.side_values(facts), "towers_lost": facts["towers_lost"]}
    return list(newest.values())


def git_show_factions(build):
    """factions.json as the build committed it."""
    path = review.FACTIONS.relative_to(review.ROOT).as_posix()
    return subprocess.check_output(["git", "-C", str(review.ROOT), "show", f"{build}:{path}"], text=True, encoding="utf-8", stderr=subprocess.DEVNULL)


def main(argv=None, show=git_show_factions):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("command", choices=("schedule", "todo", "summary"))
    parser.add_argument("--build", help="a commit sha, or its first characters, that the reports must name")
    parser.add_argument("--runs-root", default=str(RUNS_ROOT))
    args = parser.parse_args(argv)
    if args.command == "schedule":
        print("\n".join(f"{w} {e}" for w, e in schedule(factions())))
        return 0
    if not args.build:
        parser.error("--build is required for todo and summary")
    try:
        text = show(args.build)
    except (OSError, subprocess.CalledProcessError):
        parser.error(f"unknown build {args.build}: git cannot show its factions.json")
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / "factions.json"
        path.write_text(text, encoding="utf-8")
        pairs = schedule(factions(path))
        results = results_for(args.build, pairs, args.runs_root, path)
    if args.command == "todo":
        print("\n".join(f"{w} {e}" for w, e in todo(pairs, results)))
        return 0
    print(f"{len(results)} of {len(pairs)} pairs played on build {args.build}.\n")
    print(table(tally(results)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
