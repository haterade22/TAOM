#!/usr/bin/env python
"""Run The Long Defeat (#1079) in the real game with nobody at the keyboard, and judge its [Check] lines.

Run:  python tools/run_long_defeat.py [--fortress helms] [--waves N] [--lose] [--garrison-scale X] [--live-limit S] [--screenshots 5,60]
                                      [--speed 1|2|4|10] [--class ID|all] [--seed N] [--start-wave N] [--start-rerolls N] [--deep]
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
the next class. The report must be schema 2 and must echo the seed, start wave and rerolls that were armed; any owner_missing above 0 fails.
A report with a wave row whose healthFactor or damageFactor is above 1 (a wave past the authored list, as in --start-wave 12 --waves 2) also owes the scale check.
A wave row's affixes must replay from the report's seed (affix_roll, the same SplitMix64 draw as the game's AffixPlan.Roll), and a run that cleared 5 waves or whose report shows a wave with affixes also owes the affix check, and a row with a stat power or hide an affix-effect line (pass or skip).

This reuses the Fords runner (tools/run_fords_autorun.py): its preflight, idle gate, launch recipe, dialog handling, crash scan and log
collection. It changes the arm variable, the done and status files, the arm text and the judge, and presses Cancel on the Helm's Deep
terrain shader dialog before the Fords dialog handling. The standing permission and its gate are in docs/reference/unattended-game-runs.md.
"""
import argparse
import itertools
import json
import re
import subprocess
import sys
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
REQUIRED_CHECKS = ("menu", "boot", "general", "muster", "keep", "orders", "hud", "wave", "no-rout", "gold", "shop", "early-start", "end", "best-run")

# A run built to lose clears no wave on purpose, so the checks that need a cleared wave, a bought pack or a horn are not asked of it.
LOSE_CHECKS = ("menu", "boot", "general", "muster", "keep", "orders", "hud", "end", "best-run")

LOSE_REASONS = ("lost_defenders", "lost_keep")

# Phase 2. The report is schema 2 (leaderboard-plan.md "Schema 2"); a schema 1 file is a build from before the new fields.
SCHEMA = 2
CLASSES = ("sword_and_board", "two_handed", "archer", "cavalry")
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
# The powers with an effect in the game (AffixPlan.Pool): hammer, terror and blades join in Slices 8 and 9, when each one's effect lands.
AFFIX_POOL = ("lash", "drums", "eye", "discipline", "hide")
AFFIX_FILE = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "long_defeat" / "affixes.json"
AFFIX_DEFAULT_SCHEDULE = ((5, 1), (10, 2), (15, 3))
AFFIX_FROM_CLEARS = 5
AFFIX_CHECK = "affix"
# A cleared wave that rolled one of these has written an affix-effect line (pass, or skip for a soldier that could not show it): a run with none never read the effect back.
AFFIX_EFFECT_CHECK = "affix-effect"
AFFIX_EFFECT_IDS = ("lash", "drums", "eye", "discipline", "hide")

# What a class adds to the list, read from the report's own "class".
CLASS_CHECKS = {"cavalry": ("sally", "sally-return")}

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
# The report fields the arm file fixed ({"seed": N, "startWave": N, "startRerolls": N}): each must come back equal, or the arm key never took effect.
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
    The Legend boon that cuts a roll (Slice 4) will key its allowance on the wave it was taken."""
    rows = report_json.get("waves")
    seed = report_json.get("seed")
    if not isinstance(rows, list) or isinstance(seed, bool) or not isinstance(seed, int):
        return None
    schedule = shipped_affix_schedule()
    for row in rows:
        if not isinstance(row, dict) or not isinstance(row.get("wave"), int) or isinstance(row.get("wave"), bool):
            continue
        wave = row["wave"]
        got = row.get("affixes")
        if not isinstance(got, list):
            return f"wave {wave} has no affixes list"
        scheduled = affix_count(schedule, wave)
        roll = affix_roll(seed, wave, scheduled)
        if got != roll:
            return f"wave {wave} reports affixes {got}, but seed {seed} rolls {roll} for a schedule of {scheduled}"
    return None


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


def check_pattern(name, result):
    return re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX + name) + r" result=" + result + r"\b")


FAIL_LINE = re.compile(r"\[Check\] id=" + re.escape(CHECK_PREFIX) + r"\S+ result=fail\b")


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
    # A run that armed no start wave is a run from wave 1, so the default is checked too, not only an armed value.
    for key, wanted in {"startWave": 1, **expected_report}.items():
        got = report_json.get(key)
        if isinstance(got, bool) or got != wanted:
            return 1, f"armed with {key}={wanted!r} but the report's {key}={report_json.get(key)!r}: the arm key never took effect"
    seed = report_json.get("seed")
    if isinstance(seed, bool) or not isinstance(seed, int) or not 0 <= seed <= MAX_SEED:
        return 1, f"the report's seed is {seed!r}, wanted a whole number from 0 to {MAX_SEED}"
    mismatch = affix_problem(report_json)
    if mismatch:
        return 1, f"the report's affixes do not replay from its seed: {mismatch}"
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
    required = required + tuple(name for name in CLASS_CHECKS.get(report_json.get("class"), ()) if name not in required)
    if reached_endless(report_json) and SCALE_CHECK not in required:
        required = required + (SCALE_CHECK,)
    cleared_waves = report_json.get("wavesCleared")
    owes_affix = reached_affixes(report_json) or (isinstance(cleared_waves, int) and not isinstance(cleared_waves, bool) and cleared_waves >= AFFIX_FROM_CLEARS)
    if not lose and owes_affix and AFFIX_CHECK not in required:
        required = required + (AFFIX_CHECK,)
    missing = [name for name in required if not check_pattern(name, "pass").search(log_text)]
    if not lose and reached_effect_affixes(report_json) and not check_pattern(AFFIX_EFFECT_CHECK, "(?:pass|skip)").search(log_text):
        missing.append(AFFIX_EFFECT_CHECK)
    if missing:
        return 1, f"reason={reason} but no pass line for: " + ", ".join(CHECK_PREFIX + name for name in missing)
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
               f"no spawn level WRONG, no CrashReport block, {felled} waves ended by felling stragglers")


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
    parser.add_argument("--class", dest="klass", choices=CLASSES + ("all",),
                        help="the lord's class (the game's bot picks one from the seed when left out); 'all' gives each of --runs the next class in turn")
    parser.add_argument("--seed", type=seed_value, help=f"the seed of every roll, 0 to {MAX_SEED} (default: the game takes the start time's ticks)")
    parser.add_argument("--start-wave", type=whole_in_range(1, 100),
                        help="begin the run at this wave number (a test option: the clears still count from 0, so the report is not a board run)")
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
            ("class", class_id), ("seed", known.seed), ("start_wave", known.start_wave), ("start_rerolls", known.start_rerolls),
            ("live_limit_seconds", known.live_limit), ("wall_limit_seconds", wall_limit), ("screenshots", known.screenshots),
            ("speed", known.speed), ("out", out), ("quit", 1)]


def armed_report_fields(known):
    """The report fields the arm file fixes, which the judge holds the report to."""
    fields = {"startRerolls": known.start_rerolls}
    if known.seed is not None:
        fields["seed"] = known.seed
    if known.start_wave is not None:
        fields["startWave"] = known.start_wave
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
