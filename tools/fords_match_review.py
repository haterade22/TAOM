#!/usr/bin/env python3
"""Reads one unattended Fords of War match and says what looks wrong: for the balance pass after every battle.

    python tools/fords_match_review.py <run folder> [<run folder> ...]

A run folder is what tools/run_fords_autorun.py wrote (E:/Temp/fords-runs/<stamp>). It prints the match as a table, then one line per
finding, and writes the same text to review.md in the folder. The findings flag a number that needs a person's reading: they name no
fix, and one match proves little. It reads the log's [FordsOfWar] lines, the report's evidence and factions.json, and writes nothing else.
"""
import glob
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FACTIONS = ROOT / "Main" / "_Module" / "ModuleData" / "fords_of_war" / "factions.json"
SIDES = ("West", "East")

GOLD_TRADE_FLAG = 1.5
TOWER_SWING_FLAG = 3
TROOP_MIN_DEATHS = 10
TROOP_MIN_KILLS = 30
TROOP_STRONG_FLAG = 2.0
TROOP_WEAK_FLAG = 0.5
CREATURE_WEAK_FLAG = 0.5
FREE_SHARE_FLAG = 0.7
UNSPENT_FLAG = 1000
SPECIAL_SHARE_FLAG = 0.5
AVG_MS_FLAG = 7.0
P95_MS_FLAG = 12.0
SHADER_COMPILE_FLAG = 100
FULL_MATCH_SECONDS = 880.0
HERO_MIN_SECONDS = 60.0
HERO_KILLS_PER_MINUTE_FLAG = 2.0
TOWER_CLASSES = ("melee", "ranged", "hero", "siege", "other")

SUMMARY = re.compile(r"\[Summary\] .*?mt=([\d.]+) .*?deathsW=(\d+) deathsE=(\d+) .*?towersW=(\d+) towersE=(\d+)")
TROOP = re.compile(r"\[Balance\] troop=(\S+) kills=(\d+) deaths=(\d+) valueKilled=(\d+) valueLost=(\d+)")
GOLD = re.compile(r"\[Balance\] side=(\w+) slot=\d+ (earned=\d+ spent=\d+ left=\d+.*)")
POISON = re.compile(r"\[Balance\] poison side=(\w+) doses=(\d+) damage=(\d+)")
UPGRADE = re.compile(r"\[Upgrade\] side=(\w+) track=(?:\{=[^}]*\})?(.+?) price=\d+")
HOUSEKEEP = re.compile(r"\[Housekeep\] .*?wargsRemoved=(\d+) .*?riderlessOthersPeak=(\S+)")
SPEED = re.compile(r"\[Speed\] speed=(\d+)")


def parse_log(text):
    """What the [FordsOfWar] lines of one match's log say. The last Summary and Housekeep lines stand for the match."""
    facts = {
        "mt": 0.0, "deaths": dict.fromkeys(SIDES, 0), "towers_lost": dict.fromkeys(SIDES, 0), "troops": {}, "gold": {}, "poison": {},
        "upgrades": {side: [] for side in SIDES}, "wargs": 0, "riderless_others": "-", "esc_pauses": 0, "errors": [], "other_errors": 0, "warnings": 0, "speed": 1,
    }
    for line in text.splitlines():
        if (found := SUMMARY.search(line)):
            facts["mt"] = float(found[1])
            facts["deaths"] = {"West": int(found[2]), "East": int(found[3])}
            facts["towers_lost"] = {"West": int(found[4]), "East": int(found[5])}
        elif (found := TROOP.search(line)):
            facts["troops"][found[1]] = {"kills": int(found[2]), "deaths": int(found[3]), "valueKilled": int(found[4]), "valueLost": int(found[5])}
        elif (found := GOLD.search(line)):
            facts["gold"][found[1]] = {name: int(gold) for name, gold in re.findall(r"(\w+)=(\d+)", found[2])}
        elif (found := POISON.search(line)):
            facts["poison"][found[1]] = {"doses": int(found[2]), "damage": int(found[3])}
        elif (found := UPGRADE.search(line)):
            facts["upgrades"][found[1]].append(found[2])
        elif (found := HOUSEKEEP.search(line)):
            facts["wargs"] = int(found[1])
            facts["riderless_others"] = found[2]
        elif (found := SPEED.search(line)):
            facts["speed"] = max(facts["speed"], int(found[1]))
        elif "[EscMenu]" in line and "paused=true" in line:
            facts["esc_pauses"] += 1
        elif "[ERROR]" in line:
            if "[FordsOfWar]" in line:
                facts["errors"].append(line[:200])
            else:
                facts["other_errors"] += 1
        elif "[WARNING]" in line:
            facts["warnings"] += 1
    return facts


def runner_outcome(runner_text):
    """The launcher's last verdict as (reason, did the game die). "No CrashReport block" in a PASS is no crash."""
    outcomes = re.findall(r"(PASS|FAIL): (.*)", runner_text)
    if not outcomes:
        return "no outcome line", False
    verdict, reason = outcomes[-1]
    return reason, verdict == "FAIL" and "game exited" in reason


def _ratio(killed, lost):
    return killed / lost if lost > 0 else float("inf")


def _side_values(facts):
    values = {side: {"killed": 0, "lost": 0} for side in SIDES}
    for troop, row in facts["troops"].items():
        side = facts["faction_of"].get(troop)
        if side in values:
            values[side]["killed"] += row["valueKilled"]
            values[side]["lost"] += row["valueLost"]
    return values


def hero_lives(marks, end):
    """hero -> side, lives and seconds on the field, from the report's hero marks. A life with no death counts up to the end of the match."""
    lives, spawned_at = {}, {}
    for mark in marks:
        hero, kind = mark["HeroId"], mark["Kind"]
        row = lives.setdefault(hero, {"side": mark["Side"], "lives": 0, "seconds": 0.0})
        if kind == "spawned":
            row["lives"] += 1
            spawned_at[hero] = mark["Mt"]
        elif kind == "died" and hero in spawned_at:
            row["seconds"] += mark["Mt"] - spawned_at.pop(hero)
    for hero, start in spawned_at.items():
        lives[hero]["seconds"] += end - start
    return lives


def _kills_per_minute(facts, hero, row):
    return facts["troops"].get(hero, {}).get("kills", 0) * 60.0 / row["seconds"] if row["seconds"] > 0 else 0.0


def _name(facts, side):
    return facts["west"] if side == "West" else facts["east"]


def _health_findings(facts):
    found = []
    if facts["speed"] > 1:
        found.append(f"SPEED: this match ran at up to {facts['speed']}x; do not read its numbers against a normal-speed match without saying so")
    if facts["crashed"]:
        found.append(f"CRASH: the game died at {facts['mt']:.0f} s ({facts['reason']}); run /native-crash-triage before reading the rest")
    elif facts["mt"] < FULL_MATCH_SECONDS:
        found.append(f"SHORT: the match stopped at {facts['mt']:.0f} s ({facts['reason']}), so its totals are partial")
    if facts["errors"]:
        found.append(f"ERROR: {len(facts['errors'])} error line(s) in the log; first: {facts['errors'][0]}")
    if facts["esc_pauses"]:
        found.append(f"PAUSE: the Escape menu paused this match {facts['esc_pauses']} time(s)")
    if facts["perf"]["avg"] > AVG_MS_FLAG or facts["perf"]["p95"] > P95_MS_FLAG:
        found.append(f"PERF: frame time average {facts['perf']['avg']:.1f} ms, worst p95 {facts['perf']['p95']:.1f} ms")
    if facts["shader_compiles"] > SHADER_COMPILE_FLAG:
        found.append(f"PERF: {facts['shader_compiles']} shader compiles: a cold cache; was the Kit open?")
    return found


def _balance_findings(facts):
    found = []
    values = _side_values(facts)
    west, east = values["West"], values["East"]
    if west["lost"] and east["lost"]:
        west_trade, east_trade = _ratio(west["killed"], west["lost"]), _ratio(east["killed"], east["lost"])
        if max(west_trade, east_trade) >= GOLD_TRADE_FLAG:
            winner, trade = ("West", west_trade) if west_trade >= east_trade else ("East", east_trade)
            found.append(f"BALANCE: {_name(facts, winner)} killed {trade:.1f} times the gold value it lost")
    swing = facts["towers_lost"]["East"] - facts["towers_lost"]["West"]
    if abs(swing) >= TOWER_SWING_FLAG:
        winner = "West" if swing > 0 else "East"
        found.append(f"BALANCE: {_name(facts, winner)} won the towers by {abs(swing)} (lost {facts['towers_lost']})")
    return found


def _troop_findings(facts):
    rows = [(troop, row, _ratio(row["valueKilled"], row["valueLost"])) for troop, row in facts["troops"].items()
            if troop in facts["faction_of"] and (row["deaths"] >= TROOP_MIN_DEATHS or row["kills"] >= TROOP_MIN_KILLS)]
    rows.sort(key=lambda r: r[2], reverse=True)
    found = []
    for troop, row, ratio in rows[:3]:
        if ratio >= TROOP_STRONG_FLAG:
            found.append(f"TROOP strong: {troop} ({_name(facts, facts['faction_of'][troop])}) {row['kills']} kills, {row['deaths']} deaths, killed {ratio:.1f} times its value")
    for troop, row, ratio in rows[-3:]:
        if ratio <= TROOP_WEAK_FLAG:
            found.append(f"TROOP weak: {troop} ({_name(facts, facts['faction_of'][troop])}) {row['kills']} kills, {row['deaths']} deaths, killed {ratio:.2f} times its value")
    return found


def _hero_findings(facts):
    found = []
    for hero, row in sorted(facts.get("hero_lives", {}).items()):
        rate = _kills_per_minute(facts, hero, row)
        if row["seconds"] >= HERO_MIN_SECONDS and rate < HERO_KILLS_PER_MINUTE_FLAG:
            kills = facts["troops"].get(hero, {}).get("kills", 0)
            found.append(f"HERO weak: {hero} ({_name(facts, row['side'])}) {kills} kills in {row['seconds']:.0f} s on the field, {rate:.1f} per minute")
    return found


def _economy_findings(facts):
    found = []
    for side in SIDES:
        troops = facts["creatures"].get(side, set())
        lost = sum(facts["troops"][t]["valueLost"] for t in troops if t in facts["troops"])
        killed = sum(facts["troops"][t]["valueKilled"] for t in troops if t in facts["troops"])
        if lost > 0 and killed / lost < CREATURE_WEAK_FLAG:
            found.append(f"ECONOMY: {_name(facts, side)}'s creature calls lost {lost} gold of value and killed {killed}")
        gold = facts["gold"].get(side)
        if gold and gold["left"] >= UNSPENT_FLAG:
            found.append(f"ECONOMY: {_name(facts, side)} ended with {gold['left']} gold unspent")
        for use in ("creatures", "heroes", "upgrades", "regions"):
            if gold and gold["spent"] and gold.get(use, 0) / gold["spent"] >= SPECIAL_SHARE_FLAG:
                found.append(f"ECONOMY: {_name(facts, side)} spent {gold[use] * 100 // gold['spent']}% of its gold on {use}")
        spawned = {t: n for t, n in facts["spawned"].items() if facts["faction_of"].get(t) == side}
        total = sum(spawned.values())
        free = sum(n for t, n in spawned.items() if t in facts["free_troops"])
        if total and free / total >= FREE_SHARE_FLAG:
            found.append(f"ECONOMY: {free * 100 // total}% of {_name(facts, side)}'s units were troops that only come free ({free} of {total})")
    return found


def _feature_findings(facts):
    found = []
    for side in SIDES:
        if not facts["upgrades"].get(side):
            found.append(f"FEATURE: {_name(facts, side)} bought no upgrade")
        if not facts["heroes"].get(side):
            found.append(f"FEATURE: {_name(facts, side)} fielded no hero")
    for side in facts["poison_factions"]:
        if side not in facts["poison"]:
            found.append(f"FEATURE: {_name(facts, side)} has poison, but no poison dose landed")
    return found


def findings(facts):
    """One line per number worth a person's reading, the most urgent kind first."""
    return _health_findings(facts) + _balance_findings(facts) + _troop_findings(facts) + _hero_findings(facts) + _economy_findings(facts) + _feature_findings(facts)


def _faction_tables(factions_path, west, east):
    """troop -> side, the troops each side calls, the troops that only come free, and the sides with poison. A mirror match reads as West."""
    factions = json.loads(Path(factions_path).read_text(encoding="utf-8"))["factions"]
    faction_of, creatures, free, poison, on_sale = {}, {"West": set(), "East": set()}, set(), [], set()
    for side, name in (("East", east), ("West", west)):
        faction = factions.get(name, {})
        for offer in faction.get("units", []):
            faction_of[offer["troop"]] = side
            on_sale.add(offer["troop"])
        for band in (faction.get("rotation") or {}).get("regions", []):
            for offer in band.get("units", []):
                faction_of[offer["troop"]] = side
                on_sale.add(offer["troop"])
        for offer in (faction.get("creatures") or {}).get("units", []):
            faction_of[offer["troop"]] = side
            creatures[side].add(offer["troop"])
        for wave in faction.get("freeWaves", []):
            for unit in wave["units"]:
                faction_of[unit["troop"]] = side
                free.add(unit["troop"])
        for hero in faction.get("heroes", []):
            faction_of[hero["id"]] = side
        if faction.get("feats", {}).get("poisonPerSecond"):
            poison.append(side)
    return faction_of, creatures, free - on_sale, poison


def gather(run, factions_path=FACTIONS):
    """The facts of one run folder, or None when it holds no log."""
    folder = Path(run)
    logs = sorted(folder.glob("taom_debug_*.log"))
    if not logs:
        return None
    facts = parse_log(logs[0].read_text(encoding="utf-8", errors="replace"))
    arm = dict(re.findall(r"^(\w+)=(.*)$", (folder / "arm.txt").read_text(encoding="utf-8"), re.M)) if (folder / "arm.txt").exists() else {}
    facts["west"], facts["east"] = arm.get("west", "West"), arm.get("east", "East")
    runner = (folder / "runner_log.txt").read_text(encoding="utf-8", errors="replace") if (folder / "runner_log.txt").exists() else ""
    facts["reason"], facts["crashed"] = runner_outcome(runner)
    facts["faction_of"], facts["creatures"], facts["free_troops"], facts["poison_factions"] = _faction_tables(factions_path, facts["west"], facts["east"])
    rgl = glob.glob(str(folder / "rgl_log_*.txt"))
    facts["shader_compiles"] = Path(rgl[0]).read_text(encoding="utf-8", errors="replace").count("compile_shader:") if rgl else 0
    facts.update({"spawned": {}, "heroes": dict.fromkeys(SIDES, 0), "tower_damage": dict.fromkeys(SIDES, 0), "perf": {"avg": 0.0, "p95": 0.0}, "hero_lives": {},
                  "tower_classes": {side: dict.fromkeys(TOWER_CLASSES, 0) for side in SIDES}})
    reports = sorted(folder.glob("fords_report_*.json"))
    if reports:
        evidence = json.loads(reports[0].read_text(encoding="utf-8"))["evidence"]
        facts["spawned"] = evidence["Spawn"].get("SpawnedByTroop", {})
        for hero in evidence.get("Heroes", []):
            if hero.get("Kind") == "fielded":
                facts["heroes"][hero["Side"]] += 1
        facts["hero_lives"] = hero_lives(evidence.get("Heroes", []), evidence.get("Mt") or facts["mt"])
        for name, tower in evidence.get("Towers", {}).items():
            attacker = "West" if "east" in name else "East"
            applied = tower.get("AppliedDamage") or []
            facts["tower_damage"][attacker] += sum(applied)
            for kind, damage in zip(TOWER_CLASSES, applied):
                facts["tower_classes"][attacker][kind] += damage
        windows = evidence.get("Perf", {}).get("Windows") or []
        if windows:
            facts["perf"] = {"avg": sum(w["AvgMs"] for w in windows) / len(windows), "p95": max(w["P95Ms"] for w in windows)}
    return facts


def render(facts, found):
    """The match as text: the outcome, the two sides, each troop's trade, then the findings."""
    lines = [f"# {facts['west']} (West) v {facts['east']} (East): {facts['mt']:.0f} s, {facts['reason']}", ""]
    lines.append("| | " + facts["west"] + " | " + facts["east"] + " |")
    lines.append("|---|---|---|")
    for label, row in (("units lost", facts["deaths"]), ("towers lost", facts["towers_lost"]), ("tower damage", facts["tower_damage"]), ("heroes fielded", facts["heroes"])):
        lines.append(f"| {label} | {row['West']} | {row['East']} |")
    for kind in TOWER_CLASSES:
        classes = facts.get("tower_classes", {})
        if classes.get("West", {}).get(kind) or classes.get("East", {}).get(kind):
            lines.append(f"| tower damage by {kind} | {classes['West'][kind]} | {classes['East'][kind]} |")
    for key in ("earned", "income", "bounty", "towers", "camps", "captures", "refund", "spent", "units", "creatures", "heroes", "upgrades", "regions", "left"):
        lines.append(f"| gold {key} | {facts['gold'].get('West', {}).get(key, '-')} | {facts['gold'].get('East', {}).get(key, '-')} |")
    lines.append(f"| upgrades | {', '.join(facts['upgrades']['West']) or '-'} | {', '.join(facts['upgrades']['East']) or '-'} |")
    lines.append(f"| poison | {facts['poison'].get('West', '-')} | {facts['poison'].get('East', '-')} |")
    lines += ["", f"Wargs faded: {facts['wargs']}. Other riderless peak: {facts['riderless_others']}. Warnings: {facts['warnings']}. Errors outside the mode: {facts.get('other_errors', 0)}. "
              f"Frames: average {facts['perf']['avg']:.1f} ms, worst p95 {facts['perf']['p95']:.1f} ms. Shader compiles: {facts['shader_compiles']}.", ""]
    lines += ["| troop | kills | deaths | value killed | value lost |", "|---|---|---|---|---|"]
    for troop, row in sorted(facts["troops"].items(), key=lambda p: -p[1]["valueKilled"]):
        lines.append(f"| {troop} | {row['kills']} | {row['deaths']} | {row['valueKilled']} | {row['valueLost']} |")
    if facts.get("hero_lives"):
        lines += ["", "| hero | side | lives | seconds on the field | kills | kills per minute |", "|---|---|---|---|---|---|"]
        for hero, row in sorted(facts["hero_lives"].items()):
            kills = facts["troops"].get(hero, {}).get("kills", 0)
            lines.append(f"| {hero} | {_name(facts, row['side'])} | {row['lives']} | {row['seconds']:.0f} | {kills} | {_kills_per_minute(facts, hero, row):.1f} |")
    lines += ["", "## Findings", ""] + ([f"- {line}" for line in found] or ["- nothing flagged"])
    return "\n".join(lines) + "\n"


def main(argv=None):
    runs = (sys.argv[1:] if argv is None else argv)
    if not runs:
        print(__doc__)
        return 2
    code = 0
    for run in runs:
        facts = gather(run)
        if facts is None:
            print(f"{run}: no log file")
            code = 1
            continue
        out = render(facts, findings(facts))
        (Path(run) / "review.md").write_text(out, encoding="utf-8")
        print(out)
    return code


if __name__ == "__main__":
    sys.exit(main())
