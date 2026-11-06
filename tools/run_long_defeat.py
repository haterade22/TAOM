#!/usr/bin/env python
"""Run The Long Defeat (#1079) in the real game with nobody at the keyboard, and judge its [Check] lines.

Run:  python tools/run_long_defeat.py [--fortress helms] [--waves N] [--lose] [--garrison-scale X] [--live-limit S] [--screenshots 5,60]
                                      [--speed 1|2|4|10] [--class ID|all] [--seed N] [--start-wave N] [--start-rerolls N] [--start-gold N] [--deep]
                                      [--require-idle-minutes N] [--runs N] [--dry-run]

Deploy the tree under test with ./build.ps1 first and check the [BuildStamp] line: a build without the autorun idles at the main menu and
the run ends as a stall. The in-game LongDefeatAutorunDriver starts the run from the main menu with the shop bot and an AI-driven main
agent, leaves it when the run ends or a limit trips, writes long_defeat_done.txt and quits.

Exit 0 needs, in order: the done reason (final, or lost_* under --lose), the JSON report (final true and wavesCleared of at least N, not
under --lose), no "spawn level WRONG" line, no [Check] fail line of this feature, a [BuildStamp] that starts with this tree's HEAD, a
pass line for every id in REQUIRED_CHECKS (a shorter list under --lose), both READ_BACK lines, as many wave pass lines as waves cleared
(not under --lose), no CrashReport block, and a game that left by itself. The best run goes under the run folder, never to the player's
own file.

Phase 2 test options: --seed fixes every roll, --start-wave begins the run at that wave number (the clears still count from 0, so
--start-wave 12 --waves 4 stops after 4 clears), --start-rerolls seeds the boon reroll bank (armed as 1 unless given, so a 5-wave run proves
a reroll at clear 3), and --deep is --start-wave 12 --waves 4 judged by DEEP_CHECKS (a final or lost_* run). --class all gives each of --runs
the next class. The report must be schema 2 and must echo the seed, start wave, rerolls and start gold that were armed (a run armed with no start gold must report startGold null); any owner_missing above 0 fails.
--start-gold sets the gold the run starts with and makes the bot buy fortifications before packs (phase3-plan.md D27, 5000 for a fortification run; field care's proof is --waves 2 --garrison-scale 1.3 --start-gold 5000, which owes the field-care pass at clear 1). The report's purchases and gold books must replay from the shop (purchase_problem: leaderboard rules 8, 19 and 20), and a purchase of a kind or level this build does not offer (FORT_OFFERED) fails.
The gate's proof run is --waves 3 --garrison-scale 1.3 --start-gold 5000 (phase3-plan.md Slice 5): wave 1 meets the vanilla gate, the bot repairs it at clear 1 when the ram broke or dented it and then buys gate level 1, and level 2 at clear 2. A report that lists a gate level owes a gate-level pass, one that lists a repair owes gate-repair, one whose repaired gate met a later wave owes gate-attacked (a skip with reason=no-blow is named and fails), and a fortress that records tower or volley points owes a fort-points pass (it names a reader that accepted fewer points than the file records).
The Elven Volley's proof run is --waves 3 --garrison-scale 1.3 --start-gold 5000 at a fortress with recorded volley.origins (phase3-plan.md Slice 6; Helm's Deep records none until 2026-11-06, and then the volley is not for sale): the bot buys level 1 in the preparation and level 2 at clear 1 and looses the volley in every wave. A report that bought a level before a wave the run played owes a volley pass line (an auto one, and, once level 2 was bought, one with mode=crosshair); a skip (a window cut short) does not pay it.
The report's class must be the one armed (--class, run by run for --class all) or, with none armed, the bot's draw from the seed (class_problem), and the class check must pass. --class takes the classes of classes.json only (the three foot classes and cavalry), so a name the game does not offer is refused here instead of being replaced by the game.
A cavalry run also owes sally and sally-return, except that both read skip with reason=no-sally-point while no sally point is recorded in fortresses.json (the run rides from Begin): the judge accepts that skip for that reason alone, and only while this tree's fortress row holds no sally key at all (sally_recorded: the game writes the same reason when it drops a malformed block), and sally-return is owed only once a wave was cleared (sally_waived, owes_return).
A report with a wave row whose healthFactor or damageFactor is above 1 (a wave past the authored list, as in --start-wave 12 --waves 2) also owes the scale check.
A wave row's affixes must replay from the report's seed (affix_roll, the same SplitMix64 draw as the game's AffixPlan.Roll, cut by the Legend boons taken before that wave: affix_cut), and a run that cleared 5 waves or whose report shows a wave with affixes also owes the affix check, and a row with a stat power, hide, hammer, terror or blades an affix-effect line (pass or skip).
The report's boons[] must replay too (boon_problem: each draw is the game's BoonDeck draw over the shipped boons.json rows the run's class and the fortress's troop groups make eligible, the rerolls add up), a run that cleared 4 waves or took a pick owes the boon check, one that took a boon with a stat or health key owes a read= token on that pick's boon line (read_owed_picks), and one that banked a reroll at its first pick or spent one owes the reroll check.

This reuses the Fords runner (tools/run_fords_autorun.py): its preflight, idle gate, launch recipe, dialog handling, crash scan and log
collection. It changes the arm variable, the done and status files, the arm text and the judge, and presses Cancel on the Helm's Deep
terrain shader dialog before the Fords dialog handling. The standing permission and its gate are in docs/reference/unattended-game-runs.md.
"""
import argparse
import itertools
import json
import math
import re
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import ingame_verdict as iv  # noqa: E402
import run_fords_autorun as runner  # noqa: E402

ENV_VAR = "TAOM_LONGDEFEAT_AUTORUN"
DONE_FILE = "long_defeat_done.txt"
STATUS_FILE = "long_defeat_status.txt"
RUNS_ROOT = r"E:\Temp\long-defeat-runs"

# Five waves took about 460 s each in the spike's 3-wave run (1,380 s), so the live limit is mission seconds with room, and the wall limit
# sits above it. DEFAULT_WALL_LIMIT_SECONDS of the Fords runner is 2400, which a 5-wave run would trip.
DEFAULT_WALL_LIMIT = 3900
DEFAULT_LIVE_LIMIT = 3600
LOSE_GARRISON_SCALE = 0.2
SPEED_LEVELS = (1, 2, 4, 10)

CHECK_PREFIX = "taom.long-defeat."

# The check ids a full run must write, each with a row in the manifest. menu is written when the main
# menu first asks whether the option is hidden (LongDefeatMenuEntry), so it precedes the run and the unattended driver's own start reads it too.
REQUIRED_CHECKS = ("menu", "boot", "general", "muster", "keep", "class", "orders", "hud", "wave", "no-rout", "gold", "shop", "early-start", "end", "best-run")

# A run built to lose clears no wave on purpose, so the checks that need a cleared wave, a bought pack or a horn are not asked of it.
LOSE_CHECKS = ("menu", "boot", "general", "muster", "keep", "class", "orders", "hud", "end", "best-run")

LOSE_REASONS = ("lost_defenders", "lost_keep")

# Phase 2. The report is schema 2 (leaderboard-plan.md "Schema 2"); a schema 1 file is a build from before the new fields.
SCHEMA = 2
DEEP_WAVE = 12
DEEP_WAVES = 4
DEFAULT_START_REROLLS = 1
# 2^53-1: a JavaScript reader (the leaderboard bot may be one) keeps every digit of a number up to there and rounds above it.
MAX_SEED = 2 ** 53 - 1

# A --deep run starts at wave 12 and ends final or lost: a general, two then three affixes and the endless factors (phase2-plan.md section 6).
DEEP_CHECKS = ("boot", "general", "class", "commander", "affix", "scale", "end")

# The affixes (phase2-plan.md section 2): eight powers in the game's fixed order, of which AFFIX_POOL are rolled so far. A wave writes the affix check when it
# starts, so wave 5 (the first with an affix) must have started: a run from wave 1 that cleared AFFIX_FROM_CLEARS waves, or whose report shows a wave with
# affixes, owes it. The plan's "4 or more clears" would fail a --waves 4 run, which stops at the clear of wave 4 and never starts wave 5.
AFFIX_IDS = ("lash", "drums", "eye", "discipline", "hide", "hammer", "terror", "blades")
# The powers with an effect in the game (AffixPlan.Pool): hammer and terror joined in Slice 8, blades in Slice 9 when its poison and its kill owner landed: all eight roll now.
AFFIX_POOL = ("lash", "drums", "eye", "discipline", "hide", "hammer", "terror", "blades")
AFFIX_FILE = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "long_defeat" / "affixes.json"
AFFIX_DEFAULT_SCHEDULE = ((5, 1), (10, 2), (15, 3))
AFFIX_FROM_CLEARS = 5
AFFIX_CHECK = "affix"
# A cleared wave that rolled one of these has written an affix-effect line (pass, or skip for a soldier that could not show it): a run with none never read the effect back.
# The stat powers and hide are read from a sampled attacker, hammer from the engines it hardened (with the gate's state), terror from its own pulses, blades from the poison the wave dealt
# (doses, hits, damage and owner_missing); all record under this id.
AFFIX_EFFECT_CHECK = "affix-effect"
AFFIX_EFFECT_IDS = ("lash", "drums", "eye", "discipline", "hide", "hammer", "terror", "blades")

# The classes (phase2-plan.md section 2, "Classes"): the game plays the ones in classes.json that have a kit for the fortress's defender culture, and the bot draws one
# from RunRandom stream 3 when none is armed. Cavalry joined the file in Slice 11.
MODULE_DATA = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "long_defeat"
CLASS_FILE = MODULE_DATA / "classes.json"
FORTRESS_FILE = MODULE_DATA / "fortresses.json"
CLASS_CHECK = "class"
DEFAULT_FORTRESS = "helms"

# What a class adds to the list, read from the report's own "class".
CLASS_CHECKS = {"cavalry": ("sally", "sally-return")}

# While fortresses.json records no sally point, Cavalry rides from Begin and the game writes a skip for the sally at each wave's start and for the return at each clear, both with this
# reason (LongDefeatDirector.Sally.cs). The judge accepts a skip for no other reason, none once a pass shows that a point exists, and none while the fortress row holds a sally key.
SALLY_CHECK = "sally"
SALLY_RETURN_CHECK = "sally-return"
NO_SALLY_POINT = "no-sally-point"

# The third check of the sally (spike S9) is reported, not required: a hit by or on the rider or his horse in a wave's first 180 s within 40 m of the sally point. The game writes it as a
# pass at the first such hit and as a skip (never a fail) when none came, so a quiet ride neither fails the run nor reads as a pass; Mike reads it from the line.
CONTACT_CHECK = "sally-contact"

# A wave past the authored list (waves.json: Helm's Deep has ten) carries a health or damage factor above 1 in the report, so a run that reached one owes
# the scale check whatever its flags. The Slice 2 proof is --start-wave 12 --waves 2: with a tier step every 3 extra waves, wave 13 is the first to step a troop.
SCALE_CHECK = "scale"

# The poison owner rule: a kill whose owner was not active at the call. Any is a failure.
OWNER_MISSING = re.compile(r"\bowner_missing=(\d+)")

# The director's own read-back of every batch; "WRONG" means soldiers on the wrong floor or off the navmesh.
SPAWN_LEVEL_WRONG = "spawn level WRONG"

# The director reads back every batch, so a run that placed both sides wrote one ok line for each. Attackers have no other floor check
# (LongDefeatDirector.Effects.cs), so a log with no attackers line proves nothing about where they stood.
READ_BACK = ("spawn level ok: defenders", "spawn level ok: attackers")

# Helm's Deep can stop its load at this dialog: the terrain shader's vista variant fails to compile (terrain_pixel_functions.rsh:818, X4008
# raised to an error by X3129). Retry compiles the same source and fails again. Cancel lets the mission load, and the same answer works
# for a person at the keyboard. Only this variant and this error are pressed: another shader, or another error in this one, is a new fact.
SHADER_DIALOG = re.compile(r"Shaders/Sources/pbr_terrain(_gbuffer)?\.rs$")
SHADER_ERROR = "X4008"
FORDS_HANDLE_DIALOGS = runner.handle_dialogs

# Set by main() before the runner starts: the wave target the judge holds the report to, and the commit of this tree, which the log's
# [BuildStamp] must carry (a stale deploy otherwise passes on the old build's lines).
target_waves = None
tree_head = None
# The --speed the run was armed with, or None: the report's maxSpeed must equal it, or the arm key never took effect.
expected_speed = None
# The report fields the arm file fixed ({"seed": N, "startWave": N, "startRerolls": N, "startGold": N}): each must come back equal, or the arm key never took effect.
expected_report = {}


class SplitMix64:
    """The game's RunRandom (Main/Features/LongDefeat/Domain/RunRandom.cs): SplitMix64 started at seed ^ (stream * G) ^ (a * A) ^ (b * B), all
    unchecked 64-bit. The vectors in test_run_long_defeat.py are pinned in RunRandomTests.cs too, so the bot's replay cannot drift from the game."""
    MASK = (1 << 64) - 1
    GAMMA = 0x9E3779B97F4A7C15
    MIX_A = 0xBF58476D1CE4E5B9
    MIX_B = 0x94D049BB133111EB

    def __init__(self, seed, stream, a, b):
        m = self.MASK
        self.state = (seed ^ (stream * self.GAMMA & m) ^ (a * self.MIX_A & m) ^ (b * self.MIX_B & m)) & m

    def next(self):
        m = self.MASK
        self.state = (self.state + self.GAMMA) & m
        z = self.state
        z = ((z ^ (z >> 30)) * self.MIX_A) & m
        z = ((z ^ (z >> 27)) * self.MIX_B) & m
        return z ^ (z >> 31)

    def below(self, n):
        draw = self.next()
        return 0 if n <= 0 else draw % n


def affix_roll(seed, wave, count):
    """The game's AffixPlan.Roll: a partial Fisher-Yates over AFFIX_POOL drawn from stream 1 at (wave, 0), so a shorter roll is a prefix of a longer one.
    The vectors in test_run_long_defeat.py are pinned in AffixPlanTests.cs too."""
    gen = SplitMix64(seed, 1, wave, 0)
    ids = list(AFFIX_POOL)
    for i in range(min(count, len(ids))):
        j = i + gen.below(len(ids) - i)
        ids[i], ids[j] = ids[j], ids[i]
    return ids[:max(count, 0)]


def shipped_classes(path=None):
    """[(class id, {cultures with a kit})] of this tree's classes.json in file order. A missing or unreadable file, or a root that is not an object, has no classes; a
    row that is not an object with a text id and an object of kits is left out (the game's ClassesReader refuses such a row too, with a warning)."""
    try:
        root = json.loads(Path(path or CLASS_FILE).read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return []
    rows = root.get("classes") if isinstance(root, dict) else None
    found = []
    for row in rows if isinstance(rows, list) else []:
        if isinstance(row, dict) and isinstance(row.get("id"), str) and row["id"] and isinstance(row.get("kits"), dict) and row["kits"]:
            found.append((row["id"], {culture.lower() for culture in row["kits"]}))
    return found


def fortress_culture(fortress_id):
    """The defenderCulture of a fortress row of fortresses.json, lower case, or None."""
    try:
        root = json.loads(FORTRESS_FILE.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None
    for row in root.get("fortresses", []) if isinstance(root, dict) else []:
        if isinstance(row, dict) and row.get("id") == fortress_id and isinstance(row.get("defenderCulture"), str):
            return row["defenderCulture"].lower()
    return None


def class_offer(fortress_id=None):
    """The class ids a fortress offers, in file order: those with a kit for its defender culture (the game's ClassPlan.OnOffer)."""
    culture = fortress_culture(fortress_id or DEFAULT_FORTRESS)
    return tuple(cid for cid, cultures in shipped_classes() if culture is not None and culture in cultures)


# Every class of classes.json for Helm's Deep, read here and pinned to the file again by a test.
CLASSES = class_offer(DEFAULT_FORTRESS)


def bot_class(seed, fortress_id=None):
    """The class the game's bot plays when none is armed: ClassPlan.Choose, the offer's entry at RunRandom stream 3, (0, 0), Below(count). The vectors in
    test_run_long_defeat.py are pinned in ClassPlanTests.cs too. None when the fortress offers no class."""
    offer = class_offer(fortress_id)
    if not offer:
        return None
    return offer[SplitMix64(seed, 3, 0, 0).below(len(offer))]


def class_problem(report_json):
    """None when the report's class is right, else what is wrong. With no class on offer the report names none. Otherwise it names a class on offer, and, when the arm
    file named none (judge() has already compared an armed class), the bot's draw from the report's seed."""
    fortress = report_json.get("fortress")
    fortress = fortress if isinstance(fortress, str) and fortress else None
    offer = class_offer(fortress)
    got = report_json.get("class")
    if not offer:
        return None if got in ("", None) else f"the report names the class {got!r}, but classes.json offers none"
    if not isinstance(got, str) or got not in offer:
        return f"the report's class is {got!r}, not one of the classes on offer ({', '.join(offer)})"
    seed = report_json.get("seed")
    if "class" not in expected_report and _is_int(seed):
        drawn = bot_class(seed, fortress)
        if got != drawn:
            return f"the report's class is {got!r}, but the bot draws {drawn!r} from seed {seed} when no class is armed"
    return None


def _whole(value, low, high):
    return isinstance(value, int) and not isinstance(value, bool) and low <= value <= high


def shipped_affix_schedule():
    """The (fromWave, count) rows of this tree's affixes.json, read by the rules of the game's AffixesReader.ReadSchedule: no file or no JSON object means
    no affixes (); no "schedule" key, a schedule that is not a list, a row that is not a whole fromWave of 1 to 1000 and a whole count of 0 to 8, or a
    fromWave that does not rise means the shipped default; an empty list stays empty."""
    try:
        root = json.loads(AFFIX_FILE.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return ()
    if not isinstance(root, dict):
        return ()
    rows = root.get("schedule")
    if not isinstance(rows, list):
        return AFFIX_DEFAULT_SCHEDULE
    stages = []
    for row in rows:
        from_wave, count = (row.get("fromWave"), row.get("count")) if isinstance(row, dict) else (None, None)
        if not _whole(from_wave, 1, 1000) or not _whole(count, 0, 8) or (stages and from_wave <= stages[-1][0]):
            return AFFIX_DEFAULT_SCHEDULE
        stages.append((from_wave, count))
    return tuple(stages)


def affix_count(schedule, wave):
    """How many affixes a wave rolls: the count of the last schedule row at or below it, 0 before the first."""
    count = 0
    for from_wave, rows in schedule:
        if from_wave <= wave:
            count = rows
    return count


def affix_problem(report_json):
    """None when every wave row's affixes are the replayed roll of the report's seed, else what is wrong. A report with no wave rows has nothing to replay.
    A Legend boon that cuts a roll is allowed on the wave it was taken before and every wave after (affix_cut): the row must be the first
    max(0, len(roll) + cut) powers of the roll the game takes (the schedule count held to the pool), a prefix, since a shorter roll is a prefix of a longer one."""
    rows = report_json.get("waves")
    seed = report_json.get("seed")
    if not isinstance(rows, list) or isinstance(seed, bool) or not isinstance(seed, int):
        return None
    schedule = shipped_affix_schedule()
    boon_rows = shipped_boons()[0]
    for row in rows:
        if not isinstance(row, dict) or not isinstance(row.get("wave"), int) or isinstance(row.get("wave"), bool):
            continue
        wave = row["wave"]
        got = row.get("affixes")
        if not isinstance(got, list):
            return f"wave {wave} has no affixes list"
        scheduled = affix_count(schedule, wave)
        cut = affix_cut(report_json, wave, boon_rows)
        roll = affix_roll(seed, wave, scheduled)
        roll = roll[:max(0, len(roll) + cut)]
        if got != roll:
            return f"wave {wave} reports affixes {got}, but seed {seed} rolls {roll} for a schedule of {scheduled} and a boon cut of {cut}"
    return None


# The boons (phase2-plan.md section 2, "Boons"): a pick owed at every third cleared wave, three weighted cards a draw from stream 2 at (pick, draw), a reroll
# earned at every fifth clear into a bank of three. The pool a draw uses is the war chest, fortress and Legend rows, the class rows of the report's own class, and a troop
# category's rows while the fortress's garrison or shop holds a troop of that group (troop_groups, from static data, so the replay sees what the game saw).
BOON_FILE = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "long_defeat" / "boons.json"
BOON_CHECK = "boon"
REROLL_CHECK = "reroll"
BOON_FROM_CLEARS = 3
BOON_CARDS = 3
BOON_ROW_WEIGHT = 10
BOON_MAX_BANK = 3
BOON_REROLL_EVERY = 5
BOON_FREE_CATEGORIES = ("fortress", "warchest", "legend")
BOON_TROOP_CATEGORIES = ("infantry", "archers", "cavalry")
# The keys a boon read-back checks on a soldier of its target (the game's BoonKeys.StatKeys and healthPercent): a taken boon with one owes a read= token on its boon line.
BOON_READ_KEYS = ("swingSpeedPercent", "moveSpeedPercent", "drawSpeedPercent", "reloadSpeedPercent", "blockPercent", "parryPercent", "aimErrorPercent", "healthPercent")

# The group of a defender troop is its character's default_group through the engine's FormationClass flags: mounted is cavalry, else ranged is archers, else infantry. A test in
# LongDefeatBindingTests.cs pins these two tuples to the engine's IsMounted() and IsRanged() for every formation class. An absent or unknown default_group is infantry.
MOUNTED_GROUPS = ("cavalry", "horsearcher", "heavycavalry")
RANGED_GROUPS = ("ranged", "horsearcher", "skirmisher", "lightcavalry")
TROOP_FILE = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "fords_of_war" / "fords_troops.xml"
SHOP_FILE = MODULE_DATA / "shop.json"


def _json_object(path):
    try:
        root = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    return root if isinstance(root, dict) else {}


def troop_group(default_group):
    """infantry, archers or cavalry for a troop's default_group text (the game's BoonTargets.OfCharacter over the engine's flags)."""
    name = (default_group or "").lower()
    return "cavalry" if name in MOUNTED_GROUPS else "archers" if name in RANGED_GROUPS else "infantry"


def troop_groups(fortress_id=None, troop_file=None):
    """The troop categories a fortress's garrison or shop holds a troop of (the game's RunPlanTroops.DefenderGroups): each troop's default_group in fords_troops.xml, or its
    formation class when the file does not hold it (the ranged slot is archers, the other infantry). A fortress the file does not know holds none."""
    fortress = next((f for f in _json_object(FORTRESS_FILE).get("fortresses", []) if isinstance(f, dict) and f.get("id") == (fortress_id or DEFAULT_FORTRESS)), None)
    if fortress is None:
        return set()
    wanted = [(g.get("troop"), g.get("class")) for g in fortress.get("garrison", []) if isinstance(g, dict)]
    shop = _json_object(SHOP_FILE).get("shops", {}).get(fortress.get("shop"), {})
    wanted += [(o.get("troop"), o.get("class")) for o in shop.get("offers", []) if isinstance(o, dict)] if isinstance(shop, dict) else []
    try:
        known = {c.get("id"): c.get("default_group") for c in ET.parse(troop_file or TROOP_FILE).getroot().iter("NPCCharacter")}
    except (OSError, ET.ParseError):
        known = {}
    return {troop_group(known[t]) if t in known else ("archers" if c == "Ranged" else "infantry") for t, c in wanted}


def shipped_boons(path=None):
    """(rows, legend weight) of this tree's boons.json: each row {id, category, class, cut, reads}, where cut is the row's affixCount (0 or negative), reads is whether a taken
    boon of the row owes a read-back on a soldier (a stat or health key) and the order is the file's.
    The shipped file is held valid by LongDefeatShippedConfigTests, so a row is taken as it stands; a missing or unreadable file has no rows."""
    try:
        root = json.loads(Path(path or BOON_FILE).read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return [], 1
    if not isinstance(root, dict):
        return [], 1
    weight = root.get("legendWeight")
    weight = weight if _whole(weight, 1, 100) else 1
    rows = []
    for row in root.get("boons") if isinstance(root.get("boons"), list) else []:
        if not isinstance(row, dict) or not isinstance(row.get("id"), str):
            continue
        cut = sum(e.get("value", 0) for e in row.get("effects", []) if isinstance(e, dict) and e.get("key") == "affixCount" and isinstance(e.get("value"), int))
        reads = any(isinstance(e, dict) and e.get("key") in BOON_READ_KEYS for e in list(row.get("effects", [])) + list(row.get("drawback", [])))
        rows.append({"id": row["id"], "category": row.get("category"), "class": row.get("class") or "", "cut": cut, "reads": reads})
    return rows, weight


def boon_cards(seed, pick, draw, pool, legend_weight, shown):
    """The game's BoonDeck.Draw over pool (the eligible rows in file order, owned ones already out): the rows just shown are left out unless fewer than
    three others remain; each card is r = below(total weight), walked to the row it falls in, taken and removed. The vectors in test_run_long_defeat.py
    are pinned in BoonDeckTests.cs and LongDefeatShippedConfigTests.cs too."""
    others = [row for row in pool if row["id"] not in shown]
    rows = list(others if shown and len(others) >= BOON_CARDS else pool)
    gen = SplitMix64(seed, 2, pick, draw)
    cards = []
    for _ in range(min(BOON_CARDS, len(rows))):
        roll = gen.below(sum(_boon_weight(r, legend_weight) for r in rows))
        index = 0
        while index < len(rows) - 1 and roll >= _boon_weight(rows[index], legend_weight):
            roll -= _boon_weight(rows[index], legend_weight)
            index += 1
        cards.append(rows.pop(index)["id"])
    return cards


def _boon_weight(row, legend_weight):
    return legend_weight if row["category"] == "legend" else BOON_ROW_WEIGHT


def _is_int(value):
    return isinstance(value, int) and not isinstance(value, bool)


def boon_eligible(row, class_id, groups):
    """Whether a row may be drawn: a free category, a class row of the lord's class, or a troop category row whose group the garrison or shop holds (the game's BoonDeck.IsEligible)."""
    category = row["category"]
    return category in BOON_FREE_CATEGORIES or (category == "class" and row["class"] == class_id) or (category in BOON_TROOP_CATEGORIES and category in groups)


def boon_problem(report_json, path=None):
    """None when every pick of the report's boons[] replays from its seed, else what is wrong: each draw equals the game's draw over the rows eligible after the
    boons taken before it (the shown cards left out of a reroll), the taken boon was among the last draw, only the last pick may be left open, and the
    rerolls add up (used is the draws after each first, and earned minus used is the bank, at most three). A report with no boons list has nothing to replay."""
    picks = report_json.get("boons")
    seed = report_json.get("seed")
    if not isinstance(picks, list) or not _is_int(seed):
        return None
    rows, legend_weight = shipped_boons(path)
    class_id = report_json.get("class") or ""
    fortress = report_json.get("fortress")
    groups = troop_groups(fortress) if isinstance(fortress, str) else set()
    owned = []
    used = 0
    for number, pick in enumerate(picks, 1):
        if not isinstance(pick, dict) or pick.get("pick") != number:
            return f"pick {number} is missing or numbered {pick.get('pick') if isinstance(pick, dict) else pick!r}"
        pool = [r for r in rows if r["id"] not in owned and boon_eligible(r, class_id, groups)]
        offered = pick.get("offered")
        if not isinstance(offered, list) or not all(isinstance(d, list) for d in offered):
            return f"pick {number} has no list of draws"
        if not offered and pool:
            return f"pick {number} lapsed, but {len(pool)} rows were eligible"
        shown = []
        for draw, got in enumerate(offered):
            cards = boon_cards(seed, number, draw, pool, legend_weight, shown)
            if got != cards:
                return f"pick {number} draw {draw} offers {got}, but seed {seed} draws {cards}"
            shown = cards
        used += max(0, len(offered) - 1)
        taken = pick.get("taken")
        if taken is None:
            if offered and number < len(picks):
                return f"pick {number} took nothing, yet a later pick was offered"
            continue
        if not shown or taken not in shown:
            return f"pick {number} took {taken!r}, which the last draw {shown} did not show"
        if not _is_int(pick.get("takenBeforeWave")) or pick["takenBeforeWave"] < 1:
            return f"pick {number} has no takenBeforeWave"
        owned.append(taken)
    cleared = report_json.get("wavesCleared")
    if isinstance(cleared, int) and not isinstance(cleared, bool):
        owed = cleared // BOON_FROM_CLEARS
        last = picks[-1] if picks else None
        still_open = isinstance(last, dict) and last.get("taken") is None and bool(last.get("offered"))
        if len(picks) > owed or (len(picks) < owed and not still_open):
            return f"{len(picks)} picks were listed, but {cleared} clears owe {owed}"
    rerolls = report_json.get("rerolls")
    if not isinstance(rerolls, dict) or not all(_is_int(rerolls.get(k)) for k in ("earned", "used", "banked")):
        return "the report's rerolls block is missing or not whole numbers"
    if rerolls["used"] != used:
        return f"rerolls.used is {rerolls['used']}, but the picks show {used} rerolled draws"
    if rerolls["earned"] - rerolls["used"] != rerolls["banked"] or not 0 <= rerolls["banked"] <= BOON_MAX_BANK:
        return f"rerolls earned {rerolls['earned']} less used {rerolls['used']} is not the bank {rerolls['banked']}, or the bank is outside 0 to {BOON_MAX_BANK}"
    start = report_json.get("startRerolls")
    if _is_int(start) and rerolls["earned"] < min(max(start, 0), BOON_MAX_BANK):
        return f"rerolls.earned is {rerolls['earned']}, less than the start bank of {start}"
    if _is_int(start) and isinstance(cleared, int) and not isinstance(cleared, bool) and rerolls["earned"] > min(start, BOON_MAX_BANK) + cleared // BOON_REROLL_EVERY:
        return f"rerolls.earned is {rerolls['earned']}, more than a start bank of {start} and {cleared // BOON_REROLL_EVERY} fifth clears give"
    return None


def affix_cut(report_json, wave, rows=None):
    """The affixes the boons taken before this wave cut from its schedule (zero or negative): the affixCount of each boon of the report's boons[] whose
    takenBeforeWave is this wave or earlier. A pick that took nothing, or names a boon the file does not hold, cuts nothing."""
    picks = report_json.get("boons")
    if not isinstance(picks, list):
        return 0
    by_id = {row["id"]: row for row in (shipped_boons()[0] if rows is None else rows)}
    cut = 0
    for pick in picks:
        if isinstance(pick, dict) and _is_int(pick.get("takenBeforeWave")) and pick["takenBeforeWave"] <= wave and pick.get("taken") in by_id:
            cut += by_id[pick["taken"]]["cut"]
    return cut


def read_owed_picks(report_json, rows=None):
    """The numbers of the picks whose boon line must carry a read= token: a pick that took a boon with a stat or health key (BOON_READ_KEYS), in a run that cleared a wave
    after the pick's own (the bot takes the pick in the intermission, and the director writes the line on the next tick, so a run that ended at the pick's clear never wrote it)."""
    picks = report_json.get("boons")
    cleared = report_json.get("wavesCleared")
    if not isinstance(picks, list) or not _is_int(cleared):
        return []
    by_id = {row["id"]: row for row in (shipped_boons()[0] if rows is None else rows)}
    return [p["pick"] for p in picks if isinstance(p, dict) and p.get("taken") in by_id and by_id[p["taken"]]["reads"]
            and _is_int(p.get("pick")) and _is_int(p.get("wave")) and cleared > p["wave"]]


def boon_read_pattern(pick):
    """The boon pass line of one pick with its read-back token (pass, or skip for a group with no soldier standing): the shape LongDefeatBoonChecks.Pick writes."""
    return re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX + BOON_CHECK) + rf" result=pass p={pick} cards=[^\n]* read=(?:pass|skip)\b")


def reached_boons(report_json):
    """True when the report lists a pick: the director wrote a boon check for it, unless the run ended with the pick still open."""
    picks = report_json.get("boons")
    return isinstance(picks, list) and any(isinstance(p, dict) and p.get("taken") is not None for p in picks)


def owes_boon(report_json):
    """The boon check is owed once a pick was taken, or more than three waves were cleared: a pick is owed at the third clear and the bot takes it in the
    intermission after it, which a run that stops at that clear (--waves 3) never reaches."""
    cleared = report_json.get("wavesCleared")
    return reached_boons(report_json) or (_is_int(cleared) and cleared > BOON_FROM_CLEARS)


def owes_reroll(report_json):
    """The reroll check is owed once a reroll was spent, or a pick was open in an intermission with one banked (the bot spends the first banked reroll it ever holds on the first offer)."""
    rerolls = report_json.get("rerolls")
    if isinstance(rerolls, dict) and _is_int(rerolls.get("used")) and rerolls["used"] > 0:
        return True
    cleared, start = report_json.get("wavesCleared"), report_json.get("startRerolls")
    return _is_int(cleared) and cleared > BOON_FROM_CLEARS and _is_int(start) and start >= 1


def reached_affixes(report_json):
    """True when a wave row of the report carried an affix: that wave wrote the affix check."""
    rows = report_json.get("waves")
    return isinstance(rows, list) and any(isinstance(row, dict) and isinstance(row.get("affixes"), list) and row["affixes"] for row in rows)


def reached_effect_affixes(report_json):
    """True when a wave row of the report carried a power whose effect the game reads back (AFFIX_EFFECT_IDS): that wave wrote an affix-effect line."""
    rows = report_json.get("waves")
    return isinstance(rows, list) and any(
        isinstance(row, dict) and isinstance(row.get("affixes"), list) and any(a in AFFIX_EFFECT_IDS for a in row["affixes"]) for row in rows)


def reached_endless(report_json):
    """True when a wave row of the report took a health or damage factor above 1: the run played a wave past the authored list."""
    waves = report_json.get("waves")
    if not isinstance(waves, list):
        return False
    for row in waves:
        if not isinstance(row, dict):
            continue
        for key in ("healthFactor", "damageFactor"):
            factor = row.get(key)
            if isinstance(factor, (int, float)) and not isinstance(factor, bool) and factor > 1:
                return True
    return False


# The commanders (phase2-plan.md section 2, "Commanders"): waves.json gives each roster an `every`, the ranks in order and the abilities, and the report lists the commander of every wave
# that had one. COMMANDER_CHECK is written when his wave clears, SUMMON_CHECK for each summon of it.
WAVES_FILE = MODULE_DATA / "waves.json"
COMMANDER_CHECK = "commander"
SUMMON_CHECK = "summon"
COMMANDER_OUTCOMES = ("killed", "felled", "left")


def shipped_commanders(fortress_id=None, path=None):
    """{every, ranks: [(rank id, troop)], summon: {ability ids of kind Summon}, max_steps} of the commanders the fortress's roster plays, or None when the file or the block is missing
    or the fortress is unknown. The shipped file is held valid by LongDefeatShippedCommandersTests, so a rank is taken as it stands."""
    fortress = next((f for f in _json_object(FORTRESS_FILE).get("fortresses", []) if isinstance(f, dict) and f.get("id") == (fortress_id or DEFAULT_FORTRESS)), None)
    roster = _json_object(path or WAVES_FILE).get("rosters", {})
    block = roster.get(fortress.get("waves")) if fortress is not None and isinstance(roster, dict) else None
    block = block.get("commanders") if isinstance(block, dict) else None
    if not isinstance(block, dict) or not _is_int(block.get("every")) or block["every"] <= 0:
        return None
    ranks = [(r.get("rank"), r.get("troop")) for r in block.get("ranks", []) if isinstance(r, dict) and isinstance(r.get("rank"), str) and isinstance(r.get("troop"), str)]
    abilities = block.get("abilities") if isinstance(block.get("abilities"), dict) else {}
    beyond = block.get("beyond") if isinstance(block.get("beyond"), dict) else {}
    max_steps = beyond.get("maxSteps") if _is_int(beyond.get("maxSteps")) else 10
    summon = {k for k, a in abilities.items() if isinstance(a, dict) and a.get("kind") == "Summon"}
    return {"every": block["every"], "ranks": ranks, "summon": summon, "max_steps": max_steps} if ranks else None


def commander_expected(spec, wave):
    """(rank id, troop, steps) of the commander of this wave, or None: a multiple of `every`, the rank min(n - 1, ranks - 1) for n = wave // every, and the steps
    min(maxSteps, max(0, n - ranks)) (the game's CommanderPlan.ForWave)."""
    if not _is_int(wave) or wave <= 0 or wave % spec["every"] != 0:
        return None
    n = wave // spec["every"]
    ranks = spec["ranks"]
    rank, troop = ranks[min(n - 1, len(ranks) - 1)]
    return rank, troop, min(spec["max_steps"], max(0, n - len(ranks)))


def _commander_spec(report_json):
    fortress = report_json.get("fortress")
    return shipped_commanders(fortress if isinstance(fortress, str) else None)


def commander_problem(report_json):
    """None when the report's waves[] holds a commander exactly at the multiples of the roster's `every` with the configured troop, rank and steps, each one ended and with no ability used
    after his end, and every cleared wave's summons killed or felled (leaderboard rules 16 and 17); else what is wrong. A report with no waves list, or a fortress with no commanders, has nothing to replay."""
    rows = report_json.get("waves")
    spec = _commander_spec(report_json)
    if not isinstance(rows, list) or spec is None:
        return None
    for row in rows:
        if not isinstance(row, dict) or not _is_int(row.get("wave")):
            continue
        wave, got = row["wave"], row.get("commander")
        summoned, kills, felled = row.get("summoned"), row.get("summonKills"), row.get("summonFelled")
        if all(_is_int(v) for v in (summoned, kills, felled)) and kills + felled != summoned:
            return f"wave {wave} summoned {summoned} but {kills} were killed and {felled} felled"
        expected = commander_expected(spec, wave)
        if expected is None:
            if got is not None:
                return f"wave {wave} lists a commander, but it is not a multiple of {spec['every']}"
            continue
        if not isinstance(got, dict):
            return f"wave {wave} should have a commander ({expected[0]}, {expected[1]}) and lists none"
        for field, want in zip(("rank", "troop", "steps"), expected):
            if got.get(field) != want or (field == "steps" and not _is_int(got.get(field))):
                return f"wave {wave} commander {field} is {got.get(field)!r}, wanted {want!r}"
        if got.get("outcome") not in COMMANDER_OUTCOMES:
            return f"wave {wave} commander outcome is {got.get('outcome')!r} at the clear, wanted one of {', '.join(COMMANDER_OUTCOMES)}"
        if not _is_int(got.get("usesAfterEnd")) or got["usesAfterEnd"] != 0:
            return f"wave {wave} commander used an ability after his end (usesAfterEnd={got.get('usesAfterEnd')!r})"
    return None


def owes_commander(report_json):
    """The commander check is owed once a wave of the report that had a commander was cleared; with no wave rows, once `every` waves were cleared from wave 1."""
    spec = _commander_spec(report_json)
    if spec is None:
        return False
    rows = report_json.get("waves")
    if isinstance(rows, list) and rows:
        return any(isinstance(r, dict) and commander_expected(spec, r.get("wave")) is not None for r in rows)
    cleared, start = report_json.get("wavesCleared"), report_json.get("startWave")
    return _is_int(cleared) and _is_int(start) and start == 1 and cleared >= spec["every"]


def owes_summon(report_json):
    """The summon check is owed once a cleared wave's commander used a Summon ability."""
    spec = _commander_spec(report_json)
    rows = report_json.get("waves")
    if spec is None or not isinstance(rows, list):
        return False
    for row in rows:
        commander = row.get("commander") if isinstance(row, dict) else None
        uses = commander.get("uses") if isinstance(commander, dict) else None
        if isinstance(uses, dict) and any(_is_int(uses.get(a)) and uses[a] > 0 for a in spec["summon"]):
            return True
    return False


# The fortifications (phase3-plan.md, Phase 3). shop.json carries a `fortifications` block per shop; the report lists every purchase and the gold spent on each sink.
# read_fortifications mirrors FortificationsReader rule for rule (both suites read tools/tests/long_defeat_fort_cases.json), FORT_OFFERED mirrors FortOffer.Offered (D31), and
# purchase_problem replays rules 8, 19 and 20 of leaderboard-plan.md over a report.
MAX_PRICE = 1_000_000
FORT_OFFERED = {"gate": 3, "towers": 0, "fieldCare": True, "volley": 2}  # field care landed in Slice 4, the gate in Slice 5 and the Elven Volley in Slice 6; each later slice raises its kind here and in FortOffer.Offered
PURCHASE_KINDS = ("pack", "gateLevel", "gateRepair", "towerLevel", "fieldCare", "volleyLevel")
SPENT_ON = ("packs", "gate", "towers", "fieldCare", "volley")
KIND_SINK = {"pack": "packs", "gateLevel": "gate", "gateRepair": "gate", "towerLevel": "towers", "fieldCare": "fieldCare", "volleyLevel": "volley"}
SHARE_TOLERANCE = 0.0005


def _fort_int(value, low, high):
    """LongDefeatConfigScope.TryInt: a JSON integer (a float such as 100.0 and a boolean are refused) inside the range."""
    return isinstance(value, int) and not isinstance(value, bool) and low <= value <= high


def _fort_float(value, low, high):
    """LongDefeatConfigScope.TryFloat: a finite JSON number inside the range."""
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and low <= value <= high


def _fort_text(value):
    """LongDefeatConfigScope.Text: a string with something in it, trimmed."""
    return value.strip() or None if isinstance(value, str) else None


def _volley_level(level):
    """One Elven Volley level as [price, arrows, cooldownSeconds, radius, range, aim], or None when any value is out of its range."""
    aim = _fort_text(level.get("aim"))
    if not (_fort_int(level.get("price"), 1, MAX_PRICE) and _fort_int(level.get("arrows"), 1, 40) and _fort_float(level.get("cooldownSeconds"), 5, 600)
            and _fort_float(level.get("radius"), 1, 30) and _fort_float(level.get("range"), 10, 200) and aim in ("auto", "crosshair")):
        return None
    return [level["price"], level["arrows"], level["cooldownSeconds"], level["radius"], level["range"], aim]


def read_fortifications(shop):
    """(plan, warnings) of one shop's `fortifications` block, as FortificationsReader reads it. The plan is None when the shop has no such key; otherwise a dict
    {gate, towers, care, volley}, each None when its row is dropped. `warnings` counts the values that reverted or were skipped, one per problem, as the game logs them."""
    if "fortifications" not in shop:
        return None, 0
    block = shop["fortifications"]
    warnings = []
    if not isinstance(block, dict):
        return {"gate": None, "towers": None, "care": None, "volley": None}, 1

    def group(parent, key):
        if key not in parent:
            return None
        if isinstance(parent[key], dict):
            return parent[key]
        warnings.append(f"{key} is not an object")
        return None

    def levels_of(row, what):
        if isinstance(row.get("levels"), list):
            return row["levels"]
        warnings.append(f"{what}: levels is missing or not an array")
        return None

    gate = None
    row = group(block, "gate")
    if row is not None:
        if not _fort_int(row.get("repairPrice"), 1, MAX_PRICE):
            warnings.append("gate.repairPrice")
        elif (raw := levels_of(row, "gate")) is not None:
            levels = []
            for level in raw:
                if not isinstance(level, dict):
                    warnings.append("gate level is not an object")
                    continue
                percent = level.get("hitPointsPercent")
                if not _fort_float(percent, 1, 1000):
                    warnings.append("gate level hitPointsPercent")
                    break
                if levels and not _f32(percent) > _f32(levels[-1][0]):  # C# compares the 32-bit floats: 100 and 100.000001 are one value
                    warnings.append("gate level hitPointsPercent does not rise")
                    break
                if not _fort_int(level.get("price"), 1, MAX_PRICE):
                    warnings.append("gate level price")
                    continue
                levels.append([percent, level["price"]])
            if levels:
                gate = {"repairPrice": row["repairPrice"], "levels": levels}
            else:
                warnings.append("gate has no valid level")

    towers = None
    row = group(block, "towers")
    if row is not None:
        troop = _fort_text(row.get("troop"))
        raw = levels_of(row, "towers") if troop is not None else None
        if troop is None:
            warnings.append("towers.troop")
        elif raw is not None:
            levels = []
            for i, level in enumerate(raw):
                if not isinstance(level, dict):
                    warnings.append("towers level is not an object")
                    continue
                posts = level.get("posts")
                if not _fort_int(posts, 1, 16):
                    warnings.append("towers level posts")
                    continue
                if levels and posts < levels[-1][0]:
                    warnings.append("towers level posts fall")
                    continue
                if not _fort_int(level.get("price"), 1, MAX_PRICE):
                    warnings.append("towers level price")
                    continue
                flag = False
                if "volley" in level:
                    if not isinstance(level["volley"], bool):
                        warnings.append("towers level volley is not a boolean")
                    elif level["volley"] and i != len(raw) - 1:
                        warnings.append("towers volley flag on a level that is not the last")
                    else:
                        flag = level["volley"]
                levels.append([posts, flag, level["price"]])
            if not levels:
                warnings.append("towers has no valid level")
            else:
                volley = None
                block_volley = group(row, "volley")
                if block_volley is not None:
                    item = _fort_text(block_volley.get("item"))
                    spec = (("arrows", 1, 40), ("firstSeconds", 5, 600), ("cooldownSeconds", 5, 600), ("radius", 1, 30), ("range", 10, 200), ("originHeight", 0, 10))
                    if item is None or not (_fort_int(block_volley.get("arrows"), 1, 40) and all(_fort_float(block_volley.get(k), lo, hi) for k, lo, hi in spec[1:])):
                        warnings.append("towers.volley is dropped")
                    else:
                        volley = {"item": item, **{k: block_volley[k] for k, _, _ in spec}}
                if volley is None and any(level[1] for level in levels):
                    warnings.append("towers volley flag without a volley block")
                    levels = [[p, False, price] for p, _, price in levels]
                towers = {"troop": troop, "levels": levels, "volley": volley}

    care = None
    row = group(block, "fieldCare")
    if row is not None:
        if not _fort_float(row.get("share"), 0, 1) or not _f32(row["share"]) > 0:  # 1e-50 is a float 0 in the game
            warnings.append("fieldCare.share")
        elif not _fort_int(row.get("price"), 1, MAX_PRICE):
            warnings.append("fieldCare.price")
        else:
            per = row.get("perIntermission", 1)
            if not _fort_int(per, 1, 5):
                warnings.append("fieldCare.perIntermission")
                per = 1
            care = {"share": row["share"], "price": row["price"], "perIntermission": per}

    volley = None
    row = group(block, "elvenVolley")
    if row is not None:
        item = _fort_text(row.get("item"))
        if item is None:
            warnings.append("elvenVolley.item")
        elif not _fort_float(row.get("originHeight"), 0, 10):
            warnings.append("elvenVolley.originHeight")
        elif (raw := levels_of(row, "elvenVolley")) is not None:
            levels = []
            for level in raw:
                parsed = _volley_level(level) if isinstance(level, dict) else None
                if parsed is None:
                    warnings.append("elvenVolley level is dropped")
                elif not levels and parsed[5] != "auto":
                    warnings.append("elvenVolley first level must aim itself")
                else:
                    levels.append(parsed)
            if levels:
                volley = {"item": item, "originHeight": row["originHeight"], "levels": levels}
            else:
                warnings.append("elvenVolley has no valid level")
    return {"gate": gate, "towers": towers, "care": care, "volley": volley}, len(warnings)


def shipped_fortifications(fortress_id=None, path=None):
    """The `fortifications` plan of this tree's shop.json for the fortress's shop (read_fortifications' plan), or None when the file, the fortress or the block is missing."""
    fortress = next((f for f in _json_object(FORTRESS_FILE).get("fortresses", []) if isinstance(f, dict) and f.get("id") == (fortress_id or DEFAULT_FORTRESS)), None)
    shops = _json_object(path or SHOP_FILE).get("shops", {})
    shop = shops.get(fortress.get("shop")) if fortress is not None and isinstance(shops, dict) else None
    return read_fortifications(shop)[0] if isinstance(shop, dict) else None


def shipped_offers(fortress_id=None, path=None):
    """{offer id: price} of the packs of the fortress's shop in this tree's shop.json."""
    fortress = next((f for f in _json_object(FORTRESS_FILE).get("fortresses", []) if isinstance(f, dict) and f.get("id") == (fortress_id or DEFAULT_FORTRESS)), None)
    shops = _json_object(path or SHOP_FILE).get("shops", {})
    shop = shops.get(fortress.get("shop")) if fortress is not None and isinstance(shops, dict) else None
    offers = shop.get("offers", []) if isinstance(shop, dict) else []
    return {o["id"]: o["price"] for o in offers if isinstance(o, dict) and isinstance(o.get("id"), str) and _is_int(o.get("price"))}


def shipped_price_percents(path=None):
    """{boon id: the sum of its pricePercent effects and drawbacks} of this tree's boons.json, for the boons that change a price."""
    rows = _json_object(path or BOON_FILE).get("boons", [])
    out = {}
    for row in rows if isinstance(rows, list) else []:
        if not isinstance(row, dict) or not isinstance(row.get("id"), str):
            continue
        total = sum(e.get("value", 0) for e in list(row.get("effects", [])) + list(row.get("drawback", [])) if isinstance(e, dict) and e.get("key") == "pricePercent" and isinstance(e.get("value"), (int, float)))
        if total:
            out[row["id"]] = total
    return out


def _f32(value):
    """A number as the game's 32-bit float holds it."""
    return struct.unpack("f", struct.pack("f", value))[0]


def price_factor(percent_sum):
    """The game's RunModifiers.Factor for the price: Math.Max(0f, 1f + sum / 100f) in 32-bit float."""
    return max(0.0, _f32(1.0 + _f32(percent_sum / 100.0)))


def apply_price(amount, factor):
    """GoldFactors.Apply, then the shop's Math.Max(1, ...): the amount times the factor (a 32-bit float widened to a double), half away from zero, at least 1."""
    if amount <= 0:
        return max(1, amount)
    product = amount * factor
    floor = math.floor(product)
    return max(1, int(floor + 1 if product - floor >= 0.5 else floor))


def repair_prices(whole, share):
    """The prices GateMath.RepairPrice can give for a whole price and a share the report wrote to three decimals: the report's share is rounded, so every share within half a thousandth counts."""
    def one(s):
        return max(1, math.ceil(round(max(1, whole) * min(1.0, max(0.0, s)), 3)))
    return range(one(share - SHARE_TOLERANCE), one(share + SHARE_TOLERANCE) + 1)


def repair_total(whole, shares):
    """Every total a repair can cost: the game charges each gate its own max(1, ceil(whole * share)) (QuoteRepair), so the total is the sum over the gates, each within the report's rounding."""
    ranges = [repair_prices(whole, s) for s in shares]
    return range(sum(r.start for r in ranges), sum(r.stop - 1 for r in ranges) + 1)


def purchase_problem(report_json, fortress_id=None, fort=None, offers=None, percents=None):
    """None when the report's gold books and purchases replay from the shop, else what is wrong (leaderboard-plan.md rules 8, 19 and 20, phase3-plan.md section 7).

    Rule 8: gold.spent equals the sum of gold.spentOn, and each spentOn part equals the prices of its purchases. Rule 19: each purchase's basePrice is shop.json's for its kind and level
    (the offer's for a pack), its price is max(1, the base through the price factor of the boons owned before that wave) and, for a repair, max(1, ceil(that * share)); levels are bought in order,
    this build offers the kind and level (FORT_OFFERED), care is bought at most perIntermission times a wave, and the final levels are the highest bought. A boon taken in the same intermission may
    come before or after a purchase, so a price is accepted at any factor that intermission could have held. Rule 20: a wave row's volley counts are 0 before the first level and
    never above the arrows of the level in force."""
    fortress_id = fortress_id or report_json.get("fortress")
    fortress_id = fortress_id if isinstance(fortress_id, str) else None
    plan = (shipped_fortifications(fortress_id) if fort is None else fort) or {}
    offers = shipped_offers(fortress_id) if offers is None else offers
    percents = shipped_price_percents() if percents is None else percents
    purchases, gold, final = report_json.get("purchases"), report_json.get("gold"), report_json.get("fortifications")
    if not isinstance(purchases, list):
        return f"purchases is {purchases!r}, wanted a list"
    spent_on = gold.get("spentOn") if isinstance(gold, dict) else None
    if not isinstance(spent_on, dict) or set(spent_on) != set(SPENT_ON) or not all(_is_int(v) and v >= 0 for v in spent_on.values()):
        return f"gold.spentOn is {spent_on!r}, wanted a whole number for each of {', '.join(SPENT_ON)}"
    if not isinstance(final, dict) or not all(_is_int(final.get(k)) for k in ("gateLevel", "towerLevel", "volleyLevel")):
        return f"fortifications is {final!r}, wanted gateLevel, towerLevel and volleyLevel"
    if not _is_int(gold.get("spent")) or gold["spent"] != sum(spent_on.values()):
        return f"gold.spent is {gold.get('spent')!r}, but gold.spentOn sums to {sum(spent_on.values())}"

    picks = [p for p in report_json.get("boons") or [] if isinstance(p, dict) and isinstance(p.get("taken"), str)]

    def factors(wave):
        """Every price factor the intermission before this wave could have held: the boons taken before it, then each further boon of that intermission in pick order."""
        total = sum(percents.get(p["taken"], 0) for p in picks if _is_int(p.get("takenBeforeWave")) and p["takenBeforeWave"] < wave)
        out = [price_factor(total)]
        for p in sorted((p for p in picks if p.get("takenBeforeWave") == wave), key=lambda p: p.get("pick", 0)):
            total += percents.get(p["taken"], 0)
            out.append(price_factor(total))
        return out

    owned = {"gateLevel": 0, "towerLevel": 0, "volleyLevel": 0}
    by_sink = dict.fromkeys(SPENT_ON, 0)
    care_by_wave = {}
    bought_at = {"volleyLevel": [], "towerLevel": []}
    for number, p in enumerate(purchases):
        where = f"purchase {number}"
        if not isinstance(p, dict) or p.get("kind") not in PURCHASE_KINDS:
            return f"{where} is {p!r}, wanted a purchase with a kind of {', '.join(PURCHASE_KINDS)}"
        kind, wave, level, base, price, share = p["kind"], p.get("beforeWave"), p.get("level"), p.get("basePrice"), p.get("price"), p.get("share")
        if not (_is_int(wave) and wave >= 1 and _is_int(level) and level >= 0 and _is_int(base) and base >= 1 and _is_int(price) and price >= 1 and isinstance(share, (int, float)) and not isinstance(share, bool)):
            return f"{where} ({kind}) has a field that is not a number: {p!r}"
        by_sink[KIND_SINK[kind]] += price
        possible = {apply_price(base, f) for f in factors(wave)}
        if kind == "pack":
            if offers.get(p.get("id")) != base:
                return f"{where}: pack {p.get('id')!r} has basePrice {base}, shop.json's offer is {offers.get(p.get('id'))!r}"
        elif kind == "fieldCare":
            care = plan.get("care")
            if not FORT_OFFERED["fieldCare"] or not care:
                return f"{where}: field care is not offered by this build"
            care_by_wave[wave] = care_by_wave.get(wave, 0) + 1
            if care_by_wave[wave] > care["perIntermission"]:
                return f"{where}: more than {care['perIntermission']} field care before wave {wave}"
            if base != care["price"] or abs(share - care["share"]) > SHARE_TOLERANCE:
                return f"{where}: field care basePrice {base} share {share}, shop.json's is {care['price']} share {care['share']}"
        elif kind == "gateRepair":
            gate = plan.get("gate")
            if not FORT_OFFERED["gate"] or not gate:
                return f"{where}: the gate is not offered by this build"
            shares = p.get("shares")
            if not isinstance(shares, list) or not shares or not all(isinstance(s, (int, float)) and not isinstance(s, bool) and 0 < s <= 1 for s in shares):
                return f"{where}: repair shares is {shares!r}, wanted a list with a share above 0 and at most 1 for each repaired gate"
            if abs(share - sum(shares)) > SHARE_TOLERANCE:
                return f"{where}: repair share {share} is not the sum {sum(shares)} of its shares {shares}"
            if base != gate["repairPrice"]:
                return f"{where}: repair basePrice {base}, shop.json's price is {gate['repairPrice']}"
            possible = {x for f in factors(wave) for x in repair_total(apply_price(base, f), shares)}
        else:
            row = {"gateLevel": ("gate", "gate"), "towerLevel": ("towers", "towers"), "volleyLevel": ("volley", "volley")}[kind]
            rows = (plan.get(row[0]) or {}).get("levels") or []
            offered = FORT_OFFERED[row[1]]
            if level != owned[kind] + 1:
                return f"{where}: {kind} {level} bought after level {owned[kind]}, levels are bought in order"
            if level > offered or level > len(rows):
                return f"{where}: {kind} {level} is not offered by this build (offers {offered}, shop.json holds {len(rows)})"
            expected = rows[level - 1][1] if kind == "gateLevel" else rows[level - 1][2] if kind == "towerLevel" else rows[level - 1][0]
            if base != expected:
                return f"{where}: {kind} {level} basePrice {base}, shop.json's is {expected}"
            owned[kind] = level
            if kind in bought_at:
                bought_at[kind].append((wave, level))
        if price not in possible:
            return f"{where} ({kind}) paid {price} for a base of {base}; the price factor of the boons owned allows {sorted(possible)}"

    for sink in SPENT_ON:
        if spent_on[sink] != by_sink[sink]:
            return f"gold.spentOn.{sink} is {spent_on[sink]}, but its purchases sum to {by_sink[sink]}"
    for key, kind in (("gateLevel", "gateLevel"), ("towerLevel", "towerLevel"), ("volleyLevel", "volleyLevel")):
        if final[key] != owned[kind]:
            return f"fortifications.{key} is {final[key]}, but the highest bought is {owned[kind]}"
    return volley_problem(report_json, plan, bought_at)


def volley_problem(report_json, plan, bought_at):
    """Rule 20: a wave row's volley.uses is 0 before the wave after the first volleyLevel purchase, and its arrows are at most uses times the arrows of the level in force, its kills at most its arrows;
    the same for towerVolley against tower level 3 (the level that adds the tower volley) and the tower volley's arrows."""
    rows = report_json.get("waves")
    levels = (plan.get("volley") or {}).get("levels") or []
    tower_volley = (plan.get("towers") or {}).get("volley") or {}
    tower_levels = (plan.get("towers") or {}).get("levels") or []
    first_tower_volley = next((i + 1 for i, level in enumerate(tower_levels) if level[1]), None)
    for row in rows if isinstance(rows, list) else []:
        if not isinstance(row, dict):
            continue
        wave = row.get("wave")
        if not _is_int(wave):
            return f"wave row {row!r} has no wave number"
        for key, arrows_of in (("volley", lambda w: _level_arrows(levels, bought_at["volleyLevel"], w)), ("towerVolley", lambda w: _tower_arrows(tower_volley, first_tower_volley, bought_at["towerLevel"], w))):
            counts = row.get(key)
            if not isinstance(counts, dict) or not all(_is_int(counts.get(k)) and counts[k] >= 0 for k in ("uses", "arrows", "hits", "kills", "withheld")):
                return f"wave {wave}: {key} is {counts!r}, wanted uses, arrows, hits, kills and withheld"
            arrows = arrows_of(wave)
            if arrows == 0 and counts["uses"] != 0:
                return f"wave {wave}: {key}.uses is {counts['uses']}, but no level that fires it was bought before wave {wave}"
            if counts["arrows"] > counts["uses"] * arrows:
                return f"wave {wave}: {key}.arrows is {counts['arrows']}, above uses {counts['uses']} times the {arrows} arrows of the level in force"
            if counts["kills"] > counts["arrows"]:
                return f"wave {wave}: {key}.kills is {counts['kills']}, above its arrows {counts['arrows']}"
    return None


def _level_arrows(levels, bought, wave):
    """The arrows of the Elven Volley level in force for a wave (bought before it), 0 when none."""
    held = [level for at, level in bought if at <= wave]
    return levels[max(held) - 1][1] if held and max(held) <= len(levels) else 0


def _tower_arrows(volley, first_level, bought, wave):
    """The arrows of a tower volley use when the tower level that adds it was bought before this wave, else 0."""
    held = [level for at, level in bought if at <= wave]
    return volley.get("arrows", 0) if first_level is not None and held and max(held) >= first_level else 0


# Field care (Slice 4): the game heals the standing defenders on the tick after the buy and writes one field-care line per heal (LongDefeatFortChecks.FieldCare). A report that lists
# a fieldCare purchase owes at least one pass line; a skip (nobody was missing health) proves nothing and does not pay it.
FIELD_CARE_CHECK = "field-care"


def owes_field_care(report_json):
    """The field-care check is owed once the report's purchases hold field care."""
    purchases = report_json.get("purchases")
    return isinstance(purchases, list) and any(isinstance(p, dict) and p.get("kind") == "fieldCare" for p in purchases)


# The gates (Slice 5). The game writes gate-level (key l<N>) after each level it writes, gate-repair (key w<N>) after each repair and gate-attacked (key w<N>) at the next clear or the run's end for the
# repaired gate; fort-points once at the boot. A report that lists the purchase owes the pass; a skip proves nothing (gate-attacked's reason=no-blow means the ram never struck the repaired gate, R4).
GATE_LEVEL_CHECK = "gate-level"
GATE_REPAIR_CHECK = "gate-repair"
GATE_ATTACKED_CHECK = "gate-attacked"
FORT_POINTS_CHECK = "fort-points"


def _bought(report_json, kind):
    purchases = report_json.get("purchases")
    return [p for p in purchases if isinstance(p, dict) and p.get("kind") == kind] if isinstance(purchases, list) else []


def owes_gate_level(report_json):
    """The gate-level check is owed once the report's purchases hold a gate level."""
    return bool(_bought(report_json, "gateLevel"))


def owes_gate_repair(report_json):
    """The gate-repair check is owed once the report's purchases hold a repair."""
    return bool(_bought(report_json, "gateRepair"))


def _played(report_json):
    """The last wave the run reached: the wave it ended in (the report's wave) or the last wave it cleared (a row of waves), 0 when neither is a number."""
    reached = report_json.get("wave")
    cleared = [w.get("wave") for w in report_json.get("waves") or [] if isinstance(w, dict)]
    return max([n for n in [reached] + cleared if _is_int(n)], default=0)


def owes_gate_attacked(report_json):
    """The gate-attacked check is owed when a repair was bought before a wave the run then played: the repair precedes wave beforeWave, and the run reached it when the wave it ended in (the report's
    wave) is at least that number, a wave it cleared (a row of waves) or one it ended in."""
    played = _played(report_json)
    return any(_is_int(p.get("beforeWave")) and p["beforeWave"] <= played for p in _bought(report_json, "gateRepair"))


# The Elven Volley (Slice 6). The game writes one volley line per use (key elven-w<N>u<k>) when the use's six-second hit window closes, the clear cuts it or the run ends. A report that bought a level before a
# wave the run then played owes a pass line, whether or not the report shows a use: a bot that never armed the volley leaves uses at 0, which rule 20 accepts, and only this debt catches it. Level two aims the
# crosshair, so it owes a pass line with mode=crosshair. A skip (a window cut short before the share was met) proves nothing. The proof needs recorded volley origins in fortresses.json.
VOLLEY_CHECK = "volley"
VOLLEY_CROSSHAIR_PASS = re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX + VOLLEY_CHECK) + r" result=pass\b[^\n]*\bmode=crosshair\b")


def _volley_bought_before_play(report_json, level):
    played = _played(report_json)
    return any(_is_int(p.get("beforeWave")) and p["beforeWave"] <= played and _is_int(p.get("level")) and p["level"] >= level for p in _bought(report_json, "volleyLevel"))


def owes_volley(report_json):
    """The volley check is owed once a volley level was bought before a wave the run then played."""
    return _volley_bought_before_play(report_json, 1)


def owes_volley_crosshair(report_json):
    """A crosshair pass is owed once level two was bought before a wave the run then played."""
    return _volley_bought_before_play(report_json, 2)


def recorded_fort_points(fortress_id, root=None):
    """Whether the fortress row of fortresses.json records a tower post, a tower volley spot or a volley origin (the points the game's fort-points check reads); Mike records them in play."""
    root = _json_object(FORTRESS_FILE) if root is None else root
    fortress = next((f for f in root.get("fortresses", []) if isinstance(f, dict) and f.get("id") == (fortress_id or DEFAULT_FORTRESS)), None) if isinstance(root, dict) else None
    if fortress is None:
        return False
    towers = fortress.get("towers")
    volley = fortress.get("volley")
    tower_points = isinstance(towers, list) and any(isinstance(t, dict) and (t.get("posts") or t.get("volley")) for t in towers)
    origins = isinstance(volley, dict) and bool(volley.get("origins"))
    return bool(tower_points or origins)


def recorded_fort_point_count(fortress_id, root=None):
    """How many points the fortress row of fortresses.json records: every tower post, tower volley spot and volley origin. The game's fort-points line counts only the points its reader accepted."""
    root = _json_object(FORTRESS_FILE) if root is None else root
    fortress = next((f for f in root.get("fortresses", []) if isinstance(f, dict) and f.get("id") == (fortress_id or DEFAULT_FORTRESS)), None) if isinstance(root, dict) else None
    if fortress is None:
        return 0
    towers = fortress.get("towers")
    volley = fortress.get("volley")
    total = 0
    for tower in towers if isinstance(towers, list) else []:
        if isinstance(tower, dict):
            total += sum(len(tower[key]) for key in ("posts", "volley") if isinstance(tower.get(key), list))
    origins = volley.get("origins") if isinstance(volley, dict) else None
    return total + (len(origins) if isinstance(origins, list) else 0)


FORT_POINTS_LINE = re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX + FORT_POINTS_CHECK) + r" result=\w+ points=(\d+)")


def fort_points_note(report_json, log_text, root=None):
    """", fort-points: the game's reader accepted 3 of 5 recorded points" when the line counts fewer points than the file records, else "". A dropped point is a config warning, not a failed run."""
    recorded = recorded_fort_point_count(report_json.get("fortress"), root)
    found = FORT_POINTS_LINE.search(log_text or "")
    if not recorded or not found or int(found.group(1)) >= recorded:
        return ""
    return f", fort-points: the game's reader accepted {found.group(1)} of {recorded} recorded points (read the config warnings)"


def owes_fort_points(report_json, root=None):
    """The fort-points pass is owed once the fortress records a point; with none recorded the game writes a skip."""
    return recorded_fort_points(report_json.get("fortress"), root)


def check_pattern(name, result):
    return re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX + name) + r" result=" + result + r"\b")


def contact_note(report_json, log_text):
    """", sally-contact: 2 pass, 1 skip" for a class that sallies, ", sally-contact: not reached" when the log has neither, and "" for every other class. Reported, never required."""
    if "sally" not in CLASS_CHECKS.get(report_json.get("class"), ()):
        return ""
    passes = len(check_pattern(CONTACT_CHECK, "pass").findall(log_text))
    skips = len(check_pattern(CONTACT_CHECK, "skip").findall(log_text))
    if not passes and not skips:
        return f", {CONTACT_CHECK}: not reached"
    return f", {CONTACT_CHECK}: {passes} pass, {skips} skip"


FAIL_LINE = re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX) + r"\S+ result=fail\b")


def skip_reasons(name, log_text):
    """The reason= of every skip line the log holds for a check (an empty string for a skip that names none)."""
    pattern = re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX + name) + r" result=skip\b(.*)")
    found = [re.search(r"\breason=(\S+)", rest) for rest in pattern.findall(log_text)]
    return [m.group(1) if m else "" for m in found]


def sally_recorded(fortress_id):
    """Whether this tree's fortresses.json row for the fortress holds a `sally` key. The game also writes reason=no-sally-point when FortressesReader drops a malformed block, and the judge
    does not fail on config warnings, so the log's reason alone would waive a block somebody pasted in wrongly."""
    fortress = next((f for f in _json_object(FORTRESS_FILE).get("fortresses", []) if isinstance(f, dict) and f.get("id") == (fortress_id or DEFAULT_FORTRESS)), None)
    return fortress is not None and "sally" in fortress


def sally_waived(name, log_text, recorded=False):
    """Whether a sally check is waived because no sally point is recorded: the fortress row holds no sally block (recorded), the log holds no pass for either sally check (a pass shows
    that a point exists, so then both are owed again), at least one skip of this check, and every skip of it reads reason=no-sally-point. Any other reason (point-unusable, taken-over,
    no-lord) is a real miss."""
    if recorded or any(check_pattern(n, "pass").search(log_text) for n in (SALLY_CHECK, SALLY_RETURN_CHECK)):
        return False
    reasons = skip_reasons(name, log_text)
    return bool(reasons) and all(reason == NO_SALLY_POINT for reason in reasons)


def owes_return(report_json, lose):
    """The sally-return pass is owed only when a wave was cleared (the return happens at a clear), never to a run built to lose. A deep run lost in its first wave has cleared none.
    Plan item 17 wanted "a wave cleared after a sally"; this is looser and accepted: the point is per fortress, so every wave sallies or none does, and a false FAIL would need a
    cleared wave that did not sally beside one that did, which a recorded point never gives (a taken-over wave writes its own return skip with its reason, a real miss)."""
    cleared = report_json.get("wavesCleared")
    return not lose and _is_int(cleared) and cleared >= 1


def judge(done, report_json, log_text=None):
    """(exit code, why). The whole log counts for the CrashReport check: it opens at this launch, so every line is this run's, and a time
    cut would only hide a block from the load."""
    if not done:
        return 1, "no done file"
    reason = done.get("reason", "")
    lose = done.get("expect") == "lost"
    deep = done.get("expect") == "deep"
    wrong = next((row for row in (log_text or "").splitlines() if SPAWN_LEVEL_WRONG in row), None)
    note = f"; the log holds {wrong[:400]}" if wrong else ""
    accepted = LOSE_REASONS if lose else (("final",) + LOSE_REASONS if deep else ("final",))
    lost = reason in LOSE_REASONS
    if reason not in accepted:
        return 1, f"reason={reason or '?'} (wanted {' or '.join(accepted)}){note}"
    if report_json is None:
        return 1, f"reason={reason} but the JSON report could not be read"
    schema = report_json.get("schema")
    if isinstance(schema, bool) or schema != SCHEMA:
        return 1, f"the report's schema is {schema!r}, wanted {SCHEMA}: the game under test is not a schema {SCHEMA} build{note}"
    if not isinstance(report_json.get("waves"), list):
        return 1, f"the report's waves is {report_json.get('waves')!r}: a schema {SCHEMA} report must carry a waves list, or the affixes, boons and commanders have nothing to replay{note}"
    # A run that armed no start wave is a run from wave 1, so the default is checked too, not only an armed value.
    for key, wanted in {"startWave": 1, "startGold": None, **expected_report}.items():
        got = report_json.get(key)
        if isinstance(got, bool) or got != wanted:
            return 1, f"armed with {key}={wanted!r} but the report's {key}={report_json.get(key)!r}: the arm key never took effect"
    seed = report_json.get("seed")
    if isinstance(seed, bool) or not isinstance(seed, int) or not 0 <= seed <= MAX_SEED:
        return 1, f"the report's seed is {seed!r}, wanted a whole number from 0 to {MAX_SEED}"
    mismatch = class_problem(report_json)
    if mismatch:
        return 1, f"the report's class is wrong: {mismatch}"
    mismatch = affix_problem(report_json)
    if mismatch:
        return 1, f"the report's affixes do not replay from its seed: {mismatch}"
    mismatch = boon_problem(report_json)
    if mismatch:
        return 1, f"the report's boons do not replay from its seed: {mismatch}"
    mismatch = commander_problem(report_json)
    if mismatch:
        return 1, f"the report's commanders do not replay from the roster: {mismatch}"
    mismatch = purchase_problem(report_json)
    if mismatch:
        return 1, f"the report's gold books and purchases do not replay from the shop: {mismatch}"
    poison = report_json.get("poison")
    reported_missing = poison.get("ownerMissing") if isinstance(poison, dict) else None
    if isinstance(reported_missing, bool) or not isinstance(reported_missing, int):
        return 1, f"the report's poison.ownerMissing is {reported_missing!r}, wanted a whole number"
    if expected_speed is not None and report_json.get("maxSpeed") != expected_speed:
        return 1, f"armed with --speed {expected_speed} but the report's maxSpeed={report_json.get('maxSpeed')!r}: the speed never took effect"
    if not lose and not (deep and lost):
        if report_json.get("final") is not True:
            return 1, "the JSON report is not final"
        cleared = report_json.get("wavesCleared")
        if not isinstance(cleared, int) or isinstance(cleared, bool) or (target_waves is not None and cleared < target_waves):
            return 1, f"wavesCleared={cleared!r}, wanted at least {target_waves}"
    if not log_text:
        return 1, f"reason={reason} but no TAOM log was copied, and the [Check] lines are the evidence"
    if wrong:
        return 1, f"reason={reason}" + note
    failed = FAIL_LINE.search(log_text)
    if failed:
        return 1, "the log holds a failed check: " + failed.group(0)
    build, problem = build_of(log_text)
    if problem:
        return 1, f"reason={reason} but {problem}"
    over = max([reported_missing] + [int(n) for n in OWNER_MISSING.findall(log_text)])
    if over > 0:
        return 1, f"owner_missing={over} (the report's poison.ownerMissing and the log's tokens, worst of them): a poison tick found its owner gone, so a kill could reach the engine with a stale hitter"
    required = LOSE_CHECKS if lose else (DEEP_CHECKS if deep else REQUIRED_CHECKS)
    required = required + tuple(name for name in CLASS_CHECKS.get(report_json.get("class"), ()) if name not in required and (name != SALLY_RETURN_CHECK or owes_return(report_json, lose)))
    recorded = sally_recorded(report_json.get("fortress"))
    waived = [name for name in (SALLY_CHECK, SALLY_RETURN_CHECK) if name in required and sally_waived(name, log_text, recorded)]
    required = tuple(name for name in required if name not in waived)
    if reached_endless(report_json) and SCALE_CHECK not in required:
        required = required + (SCALE_CHECK,)
    cleared_waves = report_json.get("wavesCleared")
    owes_affix = reached_affixes(report_json) or (isinstance(cleared_waves, int) and not isinstance(cleared_waves, bool) and cleared_waves >= AFFIX_FROM_CLEARS)
    if not lose and owes_affix and AFFIX_CHECK not in required:
        required = required + (AFFIX_CHECK,)
    if not lose and owes_commander(report_json) and COMMANDER_CHECK not in required:
        required = required + (COMMANDER_CHECK,)
    if not lose and owes_summon(report_json) and SUMMON_CHECK not in required:
        required = required + (SUMMON_CHECK,)
    if not lose and owes_boon(report_json) and BOON_CHECK not in required:
        required = required + (BOON_CHECK,)
    if not lose and owes_reroll(report_json) and REROLL_CHECK not in required:
        required = required + (REROLL_CHECK,)
    if not lose and owes_field_care(report_json) and FIELD_CARE_CHECK not in required:
        required = required + (FIELD_CARE_CHECK,)
    for owed, name in ((owes_gate_level, GATE_LEVEL_CHECK), (owes_gate_repair, GATE_REPAIR_CHECK), (owes_gate_attacked, GATE_ATTACKED_CHECK), (owes_fort_points, FORT_POINTS_CHECK),
                       (owes_volley, VOLLEY_CHECK)):
        if not lose and owed(report_json) and name not in required:
            required = required + (name,)
    missing = [name for name in required if not check_pattern(name, "pass").search(log_text)]
    if not lose and owes_volley_crosshair(report_json) and not VOLLEY_CROSSHAIR_PASS.search(log_text):
        missing.append(VOLLEY_CHECK + " (a pass with mode=crosshair: level two aims where the lord looks)")
    if not lose and reached_effect_affixes(report_json) and not check_pattern(AFFIX_EFFECT_CHECK, "(?:pass|skip)").search(log_text):
        missing.append(AFFIX_EFFECT_CHECK)
    if missing:
        hint = ""
        if GATE_ATTACKED_CHECK in missing and "no-blow" in skip_reasons(GATE_ATTACKED_CHECK, log_text):
            hint = f" ({CHECK_PREFIX}{GATE_ATTACKED_CHECK} read reason=no-blow: no blow reached the repaired gate, so read the [Engines] lines for a ram that died earlier, R4)"
        if FORT_POINTS_CHECK in missing and "none-recorded" in skip_reasons(FORT_POINTS_CHECK, log_text):
            hint += f" ({CHECK_PREFIX}{FORT_POINTS_CHECK} read reason=none-recorded while fortresses.json records points: the game's reader accepted none, so read the config warnings)"
        return 1, f"reason={reason} but no pass line for: " + ", ".join(CHECK_PREFIX + name for name in missing) + hint
    unread_boons = [n for n in read_owed_picks(report_json) if not boon_read_pattern(n).search(log_text)]
    if not lose and unread_boons:
        return 1, f"reason={reason} but the boon check of pick(s) {', '.join(map(str, unread_boons))} has no read= token: a troop or class boon was never read back from a soldier"
    unread = [line for line in READ_BACK if line not in log_text]
    if unread:
        return 1, f"reason={reason} but the log has no '{unread[0]}' line, so that side's spawn floor was never read back"
    if not lose and not (deep and lost):
        passes = len(check_pattern("wave", "pass").findall(log_text))
        if passes < cleared:
            return 1, f"the report cleared {cleared} waves but the log holds {passes} wave pass lines"
    block = runner.crash_report_line(log_text, None)
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block: " + block[:200]
    felled = report_json.get("wavesFelled", 0)
    speed = f"speed {expected_speed}x (report maxSpeed matches), " if expected_speed is not None else ""
    return 0, (f"build {build}, reason={reason}, {speed}{report_json.get('wavesCleared', 0)} waves cleared, checks {', '.join(required)} passed, "
               f"no spawn level WRONG, no CrashReport block, {felled} waves ended by felling stragglers{contact_note(report_json, log_text)}{waived_note(waived)}{fort_points_note(report_json, log_text)}")


def waived_note(waived):
    """"; no sally point recorded, so sally and sally-return skipped" for the checks the no-sally-point skip stood in for, and "" when none was waived."""
    return f"; no sally point recorded, so {' and '.join(waived)} skipped" if waived else ""


def build_of(log_text):
    """(build label, problem). The log's [BuildStamp] must name this tree's HEAD; .dirty is allowed and shown, because a build from the
    working tree is what a run under test is."""
    stamp = next((s for s in (iv.parse_build_stamp(row) for row in log_text.splitlines() if "[BuildStamp]" in row) if s), None)
    if stamp is None:
        return None, "the log has no [BuildStamp] line with a git sha"
    sha, dirty = stamp
    label = sha[:9] + (".dirty" if dirty else "")
    if not tree_head:
        return label, "this tree's HEAD is unknown, so the build cannot be matched"
    if not sha.startswith(tree_head):
        return label, f"the log's build {label} is not this tree's HEAD {tree_head[:9]}: deploy this tree and run again"
    return label, None


def tree_head_sha():
    """The commit of the tree this wrapper lives in, or None when git cannot say."""
    try:
        done = subprocess.run(["git", "-C", str(Path(__file__).resolve().parent.parent), "rev-parse", "HEAD"],
                              capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.SubprocessError):
        return None
    head = done.stdout.strip()
    return head if done.returncode == 0 and re.fullmatch(r"[0-9a-f]{40}", head) else None


def handle_dialogs(pid, seen, say):
    """Presses Cancel on the terrain shader's compile dialog, then hands every dialog to the Fords runner, which logs a dialog it has not
    seen and reports a failed check."""
    try:
        dialogs = runner.game_dialogs(pid)
    except (OSError, AttributeError):
        dialogs = []
    for hwnd, title, texts, buttons in dialogs:
        if SHADER_DIALOG.search(title or "") and "Cancel" in buttons and any(SHADER_ERROR in t for t in texts) and runner.click_button(hwnd, buttons["Cancel"]) and hwnd not in seen:
            seen.add(hwnd)
            say(f"pressed Cancel on the shader compile dialog '{title}': {' | '.join(t for t in texts if t)}")
    return FORDS_HANDLE_DIALOGS(pid, seen, say)


def positive_int(text):
    value = int(text)
    if value <= 0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a positive whole number")
    return value


def garrison_scale(text):
    value = float(text)
    if not 0.01 <= value <= 10.0:
        raise argparse.ArgumentTypeError(f"'{text}' is not a number from 0.01 to 10")
    return value


def seed_value(text):
    value = int(text)
    if not 0 <= value <= MAX_SEED:
        raise argparse.ArgumentTypeError(f"'{text}' is not a whole number from 0 to {MAX_SEED}")
    return value


def class_choice(text):
    """A class the game can play, or 'all'. A name classes.json does not offer is refused here, because the game would replace it with another class."""
    if text == "all" or text in CLASSES:
        return text
    raise argparse.ArgumentTypeError(f"'{text}' is not a class; choose one of {', '.join(CLASSES)} or all")


def whole_in_range(low, high):
    def parse(text):
        value = int(text)
        if not low <= value <= high:
            raise argparse.ArgumentTypeError(f"'{text}' is not a whole number from {low} to {high}")
        return value
    return parse


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0],
                                     epilog="The Fords runner's flags also apply: --require-idle-minutes, --wall-limit, --runs, --keep-on-crash, --runs-root, --dry-run.")
    parser.add_argument("--fortress", default="helms", help="a fortress id of fortresses.json (default helms)")
    parser.add_argument("--waves", type=positive_int, default=5, help="waves to clear (default 5)")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--lose", action="store_true",
                        help=f"a run built to lose: garrison scale {LOSE_GARRISON_SCALE}, expect=lost, judged by the shorter check list")
    parser.add_argument("--garrison-scale", type=garrison_scale, help="scale every garrison row's count (default 1; --lose sets 0.2)")
    parser.add_argument("--live-limit", type=runner.positive_seconds, default=DEFAULT_LIVE_LIMIT,
                        help=f"mission seconds before the run is ended (default {DEFAULT_LIVE_LIMIT})")
    parser.add_argument("--screenshots", help="live seconds at which the game saves a picture of the screen into the run folder, comma separated")
    parser.add_argument("--speed", type=int, choices=SPEED_LEVELS,
                        help="the mission speed from the first frame (default 1); live seconds and the report's wave seconds stay mission seconds, so 10 ends a run in about a tenth of the wall time")
    parser.add_argument("--class", dest="klass", type=class_choice,
                        help=f"the lord's class, one of {', '.join(CLASSES)} (the game's bot picks one from the seed when left out); 'all' gives each of --runs the next of them in turn, so --runs 4 plays all four")
    parser.add_argument("--seed", type=seed_value, help=f"the seed of every roll, 0 to {MAX_SEED} (default: the game takes the start time's ticks)")
    parser.add_argument("--start-wave", type=whole_in_range(1, 100),
                        help="begin the run at this wave number (a test option: the clears still count from 0, so the report is not a board run)")
    parser.add_argument("--start-gold", type=whole_in_range(0, 100000),
                        help="the gold the run starts with instead of the shop's, and the sign that the bot buys fortifications before packs (a test option: the report is not a board run; fortification runs use 5000)")
    parser.add_argument("--start-rerolls", type=whole_in_range(0, 3), default=DEFAULT_START_REROLLS,
                        help=f"boon rerolls in the bank at the start (default {DEFAULT_START_REROLLS}, so a 5-wave run proves a reroll at clear 3)")
    group.add_argument("--deep", action="store_true",
                       help=f"the deep run: --start-wave {DEEP_WAVE} --waves {DEEP_WAVES}, judged by DEEP_CHECKS and accepting a final or lost_* end")
    return parser


def apply_deep(known):
    """--deep is --start-wave 12 --waves 4, whatever those two flags said."""
    if known.deep:
        known.start_wave = DEEP_WAVE
        known.waves = DEEP_WAVES
    return known


def class_for_run(choice, run_index):
    """The class id this run arms: a fixed choice, the next class of CLASSES for 'all', or None to leave it to the game's bot."""
    return CLASSES[run_index % len(CLASSES)] if choice == "all" else choice


def arm_pairs(known, out, wall_limit, class_id=None):
    scale = known.garrison_scale if known.garrison_scale is not None else (LOSE_GARRISON_SCALE if known.lose else None)
    expect = "deep" if known.deep else ("lost" if known.lose else None)
    return [("fortress", known.fortress), ("waves", known.waves), ("garrison_scale", scale), ("expect", expect),
            ("class", class_id), ("seed", known.seed), ("start_wave", known.start_wave), ("start_rerolls", known.start_rerolls), ("start_gold", known.start_gold),
            ("live_limit_seconds", known.live_limit), ("wall_limit_seconds", wall_limit), ("screenshots", known.screenshots),
            ("speed", known.speed), ("out", out), ("quit", 1)]


def armed_report_fields(known):
    """The report fields the arm file fixes, which the judge holds the report to."""
    fields = {"startRerolls": known.start_rerolls}
    if known.seed is not None:
        fields["seed"] = known.seed
    if known.start_wave is not None:
        fields["startWave"] = known.start_wave
    if known.start_gold is not None:
        fields["startGold"] = known.start_gold
    return fields


def show_results(done_text, log_copy, report_json, say):
    """The Fords printer labels verdict rows this feature does not write: print what the judge reads instead."""
    print(f"---- {DONE_FILE} ----\n" + done_text.strip())
    if report_json is not None:
        print("---- report (final=%s, reason=%s, wavesCleared=%s) ----" % (report_json.get("final"), report_json.get("reason"), report_json.get("wavesCleared")))
    if log_copy is not None:
        text = log_copy.read_text(encoding="utf-8", errors="replace")
        print("---- [Check] lines of the log ----")
        for line in text.splitlines():
            if "[Check] id=" + CHECK_PREFIX in line:
                print(line)
        stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
        print(stamp[0] if stamp else "no [BuildStamp] line")
        say("clean module unload line: " + ("present" if "[Shutdown] clean module unload" in text else "ABSENT"))


def main(argv=None):
    global target_waves, tree_head, expected_speed, expected_report
    parser = build_parser()
    known, rest = parser.parse_known_args(argv)
    apply_deep(known)
    target_waves = known.waves
    expected_speed = known.speed
    expected_report = armed_report_fields(known)
    run_counter = itertools.count()
    tree_head = tree_head_sha()
    if tree_head is None:
        print("cannot read this tree's HEAD with git, so a run could not be matched to its build")
        return 1

    def arm_text_for(out, args):
        wall_limit = args.wall_limit if args.wall_limit else DEFAULT_WALL_LIMIT
        class_id = class_for_run(known.klass, next(run_counter))
        # The judge holds this run's report to the class this run armed, so --class all is compared run by run; none armed leaves the seed's draw to class_problem.
        if class_id is None:
            expected_report.pop("class", None)
        else:
            expected_report["class"] = class_id
        text = "".join(f"{key}={value}\n" for key, value in arm_pairs(known, out, wall_limit, class_id) if value is not None and str(value) != "")
        return text, wall_limit

    runner.ENV_VAR = ENV_VAR
    runner.DONE_FILE = DONE_FILE
    runner.STATUS_FILE = STATUS_FILE
    runner.RUNS_ROOT = RUNS_ROOT
    runner.arm_text_for = arm_text_for
    runner.judge = judge
    runner.handle_dialogs = handle_dialogs
    runner.show_results = show_results
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
