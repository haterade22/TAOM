#!/usr/bin/env python
"""tools/run_scenario.py: one unattended run in the real game (#1075 slice 1, used by #1283; the story-mission kind is #1368, the story-offer kind #1105), judged by its done file and log.

A wrapper on run_fords_autorun.py, in the way run_long_defeat.py is: it keeps that tool's idle gate, launch recipe, dialog handling,
crash scan and log collection, and swaps in this feature's names, arm file, judge and printer. It arms TAOM_AUTORUN with an arm file
under E:\\Temp\\scenario-runs\\<yyyyMMdd-HHmmss>\\. The game starts a new campaign, runs the wanted days at the wanted speed, writes
autorun_done.txt and quits.

The kind story-mission starts a new campaign, opens one story mission through the dev menu's own click path, waits for the mission to end and
the map to come back, and quits. It runs no campaign days of its own, so --days, --speed and --stall-seconds are refused with it.

The kind story-offer drives the real story offer of the War Chronicle (#1105 slices 3 to 5): the armed console command taom.chronicle_open
opens the event's window on the settled map, the driver presses the offer popup's accept button, picks the wanted option of the menu
taom_story_offer, plays the mission with the bot, clicks Continue and runs taom.war_effects and taom.chronicle_status. --choice fate picks "Let fate decide" and plays
no mission. It refuses --mission, --days and --stall-seconds, as the story-mission kind does, and --speed unless --by-date is given.
With --by-date it arms no open command: the campaign runs on fast forward until the event's window opens on its own day, and the driver
waits for the popup as long as the wall limit. A siege window (hornburg) opens only in the Full War, day 44 by default, so it needs this.

Own arguments (everything else goes to the Fords runner, for example --dry-run, --runs, --wall-limit, --require-idle-minutes):
  --kind campaign-new|story-mission|story-offer   --days N (campaign-new only: 1 to 1000, default 3)   --culture ID (default gondor)
  --speed X (campaign-new, and story-offer with --by-date: above 0, at most 30, default 15)   --scenario LABEL (default the kind)
  --stall-seconds N (campaign-new only, default 600)
  --command LINE (repeatable, both kinds: a console command run once the map is ready; output in the TAOM log and commands.txt.
  A taom.* command needs cheat_mode = 1 in engine_config.txt, or its output says cheat mode is disabled)
  --mission ID (story-mission only, required: a story mission id of Main/_Module/ModuleData/story_missions/story_missions.json, for
  example helms_deep_defend or helms_deep_attack; an unknown id is refused)
  --by-date (story-offer only: no open command, fast forward at --speed, default 15, until the window opens by its date)
  --event ID --choice defend|attack|fate (story-offer only, both required: a War Chronicle event of war_chronicle.json that has a story block,
  breaking or hornburg; attack needs an event with an attackMission, which both have). --culture decides the sides the offer shows: the
  default gondor is a free people (defend); attack needs an evil one that character creation offers, for example isengard. The sides are read
  as StorySideGate reads them, from execution/alignment.json and the event's defender and first attacker: a culture that file does not list,
  or a choice the culture is never offered (fate needs one side), is refused.
  A taom.* command needs cheat_mode = 1, so the run needs it too: without it the driver ends as offer_refused and the log quotes the refusal.

This tool does not build or deploy: deploy this tree with `pwsh ./build.ps1` first. It reads the checkout (HEAD and the uncommitted files
under the build's own pathspec) once, at launch, with tools/autorun_build.py, and refuses to start when git cannot say.

Exit 0 needs reason=ok, days_run at least days_wanted, the [Check] pass line in the TAOM log, no CrashReport error block, the clean
module unload line, a done file that echoes the armed plan, no arm file warning, a [BuildStamp] that is the checkout as read at
launch (autorun_build.stale_build: the sha is HEAD, no MISMATCH, the .dirty mark agrees with the tree, no uncommitted watched file is newer
than the build), and a game that left by itself. The PASS line names that build. Then the war ledger of the run is saved as ledger.txt in
the run folder.

Exit 0 for story-mission needs reason=ok, a Won or Lost verdict echoed by the done file for the armed mission, the story done [Check] pass
line, the hosted.boot and ended pass lines of the story mission (a reason=Abandoned end is not one), exactly one [StoryMissions] launching
line (the armed id, autoplay True) and no other launching line, and the same log, crash, unload, arm warning and build conditions. The ended
line must name the row's own side (side=Defending for a defence row, side=Attacking for an attack row, read from the checkout's
story_missions.json). A build
that does not know the kind stays unarmed and writes no done file, so the runner ends it as a 10 minute stall and never calls the judge; the
printers then quote the log's first "not armed:" line. The default wall limit is the campaign-new one plus the mission limit (1200 s, the
arm file's mission_limit_seconds). The driver reads no wall limit for this kind: --wall-limit only moves the runner's give-up time.

Exit 0 for story-offer needs reason=ok, the done [Check] pass line (verdict Won or Lost for defend and attack, "-" for fate) for the armed
event and choice, the offer.shown pass line for the event's story mission, and the common log, crash, unload, arm warning and build
conditions. For defend and attack also the hosted.boot pass line (siege or field, by the mission's row), the ended pass line of the side,
the offer.result pass line and exactly one launching line (the chosen mission, autoplay True). For fate the line the chronicle logs when
the decline lands (a hero story resolves Occurred at once; a fortress window logs the decline) and no launching line at all.

Run:  python tools/run_scenario.py --days 30 --dry-run
      python tools/run_scenario.py --kind story-offer --event breaking --choice defend --dry-run
      python tools/run_scenario.py --kind story-offer --event breaking --choice attack --culture isengard --dry-run
      python tools/run_scenario.py --kind story-offer --event hornburg --choice defend --by-date --dry-run
      python tools/run_scenario.py --kind story-mission --mission helms_deep_defend --dry-run
      python tools/run_scenario.py --kind story-mission --mission helms_deep_attack --dry-run
Tests: python -m unittest tools.tests.test_run_scenario
"""
import argparse
import json
import math
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import autorun_build  # noqa: E402
import run_fords_autorun as runner  # noqa: E402
# Imported here, never after main swaps runner.handle_dialogs: run_long_defeat captures the Fords handler at import.
import run_long_defeat as long_defeat  # noqa: E402

ENV_VAR = "TAOM_AUTORUN"
DONE_FILE = "autorun_done.txt"
STATUS_FILE = "autorun_status.txt"
COMMANDS_FILE = "commands.txt"  # ScenarioAutorunDriver.CommandsFile
RUNS_ROOT = r"E:\Temp\scenario-runs"
KIND_CAMPAIGN = "campaign-new"
KIND_STORY = "story-mission"
KIND_OFFER = "story-offer"
KINDS = (KIND_CAMPAIGN, KIND_STORY, KIND_OFFER)
CHECK_ID = "taom.autorun.campaign-new.done"
STORY_CHECK_ID = "taom.autorun.story-mission.done"
OFFER_CHECK_ID = "taom.autorun.story-offer.done"  # ScenarioOutcomes.StoryOfferDoneId
PASS_LINE = re.compile(r"\[Check\] id=" + re.escape(CHECK_ID) + r"\s+result=pass\b")
# The same patterns as tools/ingame_checks/1105.toml (a test holds them equal): the boot line, and the end line that names only the three
# ways a run ends by itself, so a reason=Abandoned retreat is not a pass. Matched line by line.
STORY_BOOT_PASS = r"\[Check\] id=taom\.story-missions\.hosted\.boot result=pass deployment_finished=true siege_ai=true main=true friendly=false"
# A mission on a field scene (#1105 slice 2: its row has a "field" block) boots with no siege team AI and says so. The judge anchors both patterns at the line's end, so a
# siege line never passes for a field mission, nor a field line for a siege mission.
STORY_FIELD_BOOT_PASS = (r"\[Check\] id=taom\.story-missions\.hosted\.boot result=pass deployment_finished=true siege_ai=false main=true friendly=false"
                         r" field=true$")
ROOT = Path(__file__).resolve().parent.parent
STORY_MISSIONS_FILE = ROOT / "Main" / "_Module" / "ModuleData" / "story_missions" / "story_missions.json"
STORY_SIDES = {"Defend": "Defending", "Attack": "Attacking"}
CHRONICLE_FILE = ROOT / "Main" / "_Module" / "ModuleData" / "war_chronicle" / "war_chronicle.json"
ALIGNMENT_FILE = ROOT / "Main" / "_Module" / "ModuleData" / "execution" / "alignment.json"
OFFER_CHOICES = ("defend", "attack", "fate")
# The popup needs campaign ticks with time moving (Campaign.Tick runs the tick event only when the campaign dt is above 0); the driver
# holds fast forward at this multiplier while it waits, so as few days as possible pass.
OFFER_SPEED = 1
BY_DATE_DAYS = 120  # the campaign days a --by-date run may take to reach its window: the Full War starts on day 44 by default
OFFER_EVIL_CULTURE = "isengard"  # in cultures.json and alignment.json "evil"


def story_ended_pass(side, must_live=False, verdict=None):
    """The ended line's pass pattern for a row's side (Defending or Attacking), as tools/ingame_checks/1105.toml (Defending) and 1409.toml
    (Attacking) hold it. The verdict is tied to the reason as StoryVerdict.Of maps them: the defence wins by TargetReached and loses by a
    fall (DefendersDead, KeepLost), the attack the reverse. A row that names a hero who must live ("mustLive") can also end by his fall
    (reason=HeroFell), as SurvivalRun.HeroDown ends it. verdict (Won or Lost) narrows the pattern to that verdict."""
    falls = "(DefendersDead|KeepLost" + ("|HeroFell" if must_live else "") + ")"
    reason = {"Won": "TargetReached", "Lost": falls} if side == "Defending" else {"Won": falls, "Lost": "TargetReached"}
    pairs = "|".join(f"{v} side={side} reason={reason[v]}" for v in ("Won", "Lost") if verdict in (None, v))
    return r"\[Check\] id=taom\.story-missions\.ended result=pass verdict=(" + pairs + r") waves=\d+$"


def offer_result_pass(side, hero, verdict=None):
    """The offer.result pass pattern as StoryOfferService.Apply writes the line. What the defence guards is saved when the defending side
    won: the walls are Held, the hero's event is Averted. Otherwise the walls Fell to a taker, or the event Occurred (DeathOwed while the
    engine has not yet granted the hero's death). verdict (Won or Lost) narrows the pattern to that verdict."""
    attacked = side == "Attacking"
    saved, lost = ("Averted", "(Occurred|DeathOwed)") if hero else ("Held", r"Fell taker=\S+")
    cells = "|".join(f"{v} result={saved if (v == 'Won') != attacked else lost}" for v in ("Won", "Lost") if verdict in (None, v))
    return (r"\[Check\] id=taom\.story-missions\.offer\.result result=pass " + ("side=Attacking " if attacked else "") + "verdict=(" + cells + ")"
            + (r" hero=\S+$" if hero else "$"))


def story_rows():
    """{lowercase mission id: "Defending" or "Attacking"} read from this checkout's story_missions.json. An unreadable file is an empty table."""
    try:
        data = json.loads(STORY_MISSIONS_FILE.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    rows = data.get("missions", []) if isinstance(data, dict) else []
    return {str(row.get("id", "")).lower(): STORY_SIDES[row["side"]] for row in rows if isinstance(row, dict) and row.get("side") in STORY_SIDES}


def story_row(mission):
    """The checkout's row for the story mission id, or an empty dict for an id it does not hold or a file it cannot read."""
    try:
        data = json.loads(STORY_MISSIONS_FILE.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    rows = data.get("missions", []) if isinstance(data, dict) else []
    wanted = str(mission).strip().lower()
    return next((row for row in rows if isinstance(row, dict) and str(row.get("id", "")).lower() == wanted), {})


def story_field(mission):
    """True when the mission's row has a "field" block (a mission on a field scene), as StoryMissionCatalog.TryField reads it."""
    return isinstance(story_row(mission).get("field"), dict)


def story_must_live(mission):
    """True when the mission's row names a hero who must live, whose fall ends the run with reason=HeroFell."""
    return isinstance(story_row(mission).get("mustLive"), str)


def story_side(mission):
    """"Defending" or "Attacking" for a story mission id of the checkout, None for an id it does not hold."""
    return story_rows().get(str(mission).strip().lower())


def chronicle_stories():
    """{event id: {"story": the event's story block, "hero": True for a hero story, "defender": its defender id, "attackers": its attacker ids}}
    for the events of this checkout's war_chronicle.json that carry a story block. An unreadable file is an empty table."""
    try:
        data = json.loads(CHRONICLE_FILE.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}
    events = data.get("events", []) if isinstance(data, dict) else []
    return {str(e.get("id", "")).lower(): {"story": e["story"], "hero": isinstance(e["story"].get("hero"), str),
                                           "defender": str(e.get("defender") or ""), "attackers": [str(a) for a in e.get("attackers") or []]}
            for e in events if isinstance(e, dict) and isinstance(e.get("story"), dict)}


def culture_alignment(culture):
    """"free", "evil" or "neutral" for a culture id of execution/alignment.json, None for an id it does not list or a file it cannot read."""
    try:
        data = json.loads(ALIGNMENT_FILE.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None
    value = data.get(str(culture).strip().lower()) if isinstance(data, dict) else None
    return value if isinstance(value, str) else None


def offer_sides(player, row):
    """(defend shown, attack shown) for a player of this alignment ("free", "evil" or "neutral") at the event row, as StorySideGate.Choose
    decides it: a side shows when its mission ships and the player's side is not opposed to the defender (defend) or the first attacker
    (attack); neutral is opposed to nobody. A kingdom the table does not list is neutral (AlignmentService.GetSide). The runner cannot know
    the tie rule (a player already in the defender's or an attacker's kingdom never takes the other side): a new campaign has none."""
    def opposed(a, b):
        return a != "neutral" and b != "neutral" and a != b

    defender = culture_alignment(row["defender"]) or "neutral"
    attackers = row.get("attackers") or [""]
    lead = culture_alignment(attackers[0]) or "neutral"
    story = row["story"]
    return (isinstance(story.get("mission"), str) and not opposed(player, defender),
            isinstance(story.get("attackMission"), str) and not opposed(player, lead))


# Both launchers log "[X] launching ..." once per run; a story run holds exactly one such line.
ANY_LAUNCH = re.compile(r"\[(LongDefeat|StoryMissions)\] launching ")
# ScenarioAutorunStart logs this when the arm file is refused; a build that lacks the story kind refuses it.
NOT_ARMED = "[Autorun][Scenario] not armed: "
DEFAULT_MISSION_LIMIT_SECONDS = 1200
UNLOAD_LINE = "[Shutdown] clean module unload"
DEFAULT_DAYS = 3
DEFAULT_CULTURE = "gondor"
DEFAULT_SPEED = 15.0
DEFAULT_STALL_SECONDS = 600
MAX_DAYS = 1000
MAX_SPEED = 30.0
LEDGER_FILE = "ledger.txt"
LEDGER_TOOL = Path(__file__).resolve().parent / "analyze_war_ledger.py"
LEDGER_TIMEOUT_SECONDS = 300
# ScenarioAutorunDriver.cs logs every arm file warning with this text (Prefix + "arm file: "); the game then ran a default instead.
ARM_WARNING = "[Autorun][Scenario] arm file: "
RUNNER_OPTIONS = ("Fords runner options this tool passes on: --wall-limit SECONDS (default max(1800, 600 + days * 20), plus 1200 for "
                  "story-mission and story-offer), --runs N, "
                  "--runs-root DIR, --require-idle-minutes N, --keep-on-crash MINUTES, --dry-run. The Fords match options (factions, maps, "
                  "AI profile) are parsed and ignored; --speed is this tool's campaign speed.")


def days_arg(text):
    try:
        value = int(text)
    except ValueError:
        raise argparse.ArgumentTypeError(f"days must be a whole number from 1 to {MAX_DAYS}, got '{text}'") from None
    if not 1 <= value <= MAX_DAYS:
        raise argparse.ArgumentTypeError(f"days must be from 1 to {MAX_DAYS}, got {value}")
    return value


def speed_arg(text):
    try:
        value = float(text)
    except ValueError:
        raise argparse.ArgumentTypeError(f"speed must be a number above 0 and at most {MAX_SPEED:g}, got '{text}'") from None
    if math.isnan(value) or not 0 < value <= MAX_SPEED:
        raise argparse.ArgumentTypeError(f"speed must be above 0 and at most {MAX_SPEED:g}, got '{text}'")
    return value


def stall_arg(text):
    try:
        value = int(text)
    except ValueError:
        raise argparse.ArgumentTypeError(f"stall seconds must be a whole number above 0, got '{text}'") from None
    if value <= 0:
        raise argparse.ArgumentTypeError(f"stall seconds must be above 0, got {value}")
    return value


def command_arg(text):
    if "\n" in text or "\r" in text or not text.strip():
        raise argparse.ArgumentTypeError("a console command is one non-empty line")
    return text.strip()


def build_own_parser():
    parser = argparse.ArgumentParser(description="Run one unattended scenario in the real game: a new campaign, or a new campaign and one story mission.",
                                     epilog=RUNNER_OPTIONS)
    parser.add_argument("--kind", choices=KINDS, default=KIND_CAMPAIGN, help="the scenario kind (default campaign-new)")
    parser.add_argument("--event", default=None,
                        help="story-offer only, required: the War Chronicle event whose story offer the run takes, for example breaking or hornburg")
    parser.add_argument("--choice", choices=OFFER_CHOICES, default=None,
                        help="story-offer only, required: defend (defend or stand), attack (attack or hunt; needs --culture evil, for example "
                             f"{OFFER_EVIL_CULTURE}) or fate (Let fate decide, no mission)")
    parser.add_argument("--by-date", action="store_true",
                        help="story-offer only: arm no open command; run the campaign on fast forward (--speed, default "
                             f"{DEFAULT_SPEED:g}) until the event's window opens on its own day. A siege window such as hornburg opens only in the "
                             "Full War, so it needs this")
    parser.add_argument("--days", type=days_arg, default=None,
                        help=f"campaign-new only: campaign days to run, 1 to {MAX_DAYS} (default {DEFAULT_DAYS})")
    parser.add_argument("--mission", default=None, help="story-mission only, required: the story mission id, for example helms_deep_defend")
    parser.add_argument("--culture", default=DEFAULT_CULTURE,
                        help=f"the player's culture id (default {DEFAULT_CULTURE}, a free people; story-offer attack needs an evil one such as {OFFER_EVIL_CULTURE})")
    parser.add_argument("--speed", type=speed_arg, default=None,
                        help=f"campaign-new, and story-offer with --by-date: campaign speed, above 0 and at most {MAX_SPEED:g} (default {DEFAULT_SPEED:g})")
    parser.add_argument("--scenario", default=None, help="label written to the arm and done files (default the kind)")
    parser.add_argument("--command", action="append", default=[], type=command_arg,
                        help="a console command line to run once the map is ready, repeatable; the output goes to the TAOM log and "
                             "commands.txt in the run folder (#1434)")
    parser.add_argument("--stall-seconds", type=stall_arg, default=None,
                        help=f"campaign-new only: seconds without campaign progress before the game ends the run (default {DEFAULT_STALL_SECONDS})")
    return parser


def parse_own(argv):
    """(own arguments, the rest for the Fords runner). A bad value exits through argparse with a message that names it."""
    parser = build_own_parser()
    known, rest = parser.parse_known_args(argv)
    if known.kind != KIND_OFFER and (known.event is not None or known.choice is not None):
        parser.error("--event and --choice are only for --kind story-offer")
    if known.kind != KIND_OFFER and known.by_date:
        parser.error("--by-date is only for --kind story-offer")
    if known.kind == KIND_OFFER:
        parse_offer(parser, known)
    elif known.kind == KIND_STORY:
        if known.days is not None:
            parser.error("--days is only for --kind campaign-new; a story-mission run ends when the map is back")
        if known.mission is None or not known.mission.strip():
            parser.error("--kind story-mission needs --mission <story mission id>")
        for flag, value in (("--speed", known.speed), ("--stall-seconds", known.stall_seconds)):
            if value is not None:
                parser.error(f"{flag} is only for --kind campaign-new; a story-mission run reads neither")
        # The game takes the id in any case and logs the catalog's lowercase id, which the judge and tools/ingame_checks/1368.toml match.
        known.mission = known.mission.strip().lower()
        # The side and the two traits below are read once, here, at launch: the judge uses these values, so an edit of the JSON while the
        # game runs cannot fail a good run.
        rows = story_rows()
        known.side = rows.get(known.mission)
        known.field, known.must_live = story_field(known.mission), story_must_live(known.mission)
        if known.side is None:
            parser.error(f"--mission '{known.mission}' is not a story mission of this checkout"
                         + (f" (known: {', '.join(sorted(rows))})" if rows else f" ({STORY_MISSIONS_FILE} could not be read or holds no row with side Defend or Attack)"))
    else:
        if known.mission is not None:
            parser.error("--mission is only for --kind story-mission")
        if known.days is None:
            known.days = DEFAULT_DAYS
        if known.speed is None:
            known.speed = DEFAULT_SPEED
        if known.stall_seconds is None:
            known.stall_seconds = DEFAULT_STALL_SECONDS
    if known.scenario is None:
        known.scenario = known.kind
    return known, rest


def parse_offer(parser, known):
    """Checks the story-offer arguments and adds what the arm text and the judge read: event, story, hero, mission (the one the choice launches,
    None for fate), shown_mission (the one the offer.shown line names) and side. All of it is read once here, at launch."""
    for flag, value in (("--days", known.days), ("--stall-seconds", known.stall_seconds)):
        if value is not None:
            parser.error(f"{flag} is only for --kind campaign-new; a story-offer run reads neither")
    if known.speed is not None and not known.by_date:
        parser.error("--speed with --kind story-offer needs --by-date; without it the run holds speed 1 while it waits for the popup")
    if known.mission is not None:
        parser.error("--mission is only for --kind story-mission; a story-offer run takes the event's own mission")
    stories = chronicle_stories()
    event = (known.event or "").strip().lower()
    if not event:
        parser.error("--kind story-offer needs --event <chronicle event id>")
    if known.choice is None:
        parser.error("--kind story-offer needs --choice defend|attack|fate")
    row = stories.get(event)
    if row is None:
        parser.error(f"--event '{event}' is not a story event of this checkout"
                     + (f" (known: {', '.join(sorted(stories))})" if stories else f" ({CHRONICLE_FILE} could not be read or holds no event with a story block)"))
    story = row["story"]
    mission = {"defend": story.get("mission"), "attack": story.get("attackMission"), "fate": None}[known.choice]
    if known.choice != "fate" and not isinstance(mission, str):
        parser.error(f"--choice {known.choice} is not offered by '{event}': its story has no {'attackMission' if known.choice == 'attack' else 'mission'}")
    alignment = culture_alignment(known.culture)
    if alignment is None:
        parser.error(f"--culture '{known.culture}' is not listed in {ALIGNMENT_FILE} (or the file could not be read), so the sides of the offer are unknown")
    defend, attack = offer_sides(alignment, row)
    if not {"defend": defend, "attack": attack, "fate": defend or attack}[known.choice]:
        what = "sees no side of" if known.choice == "fate" else f"is not offered the {known.choice} at"
        parser.error(f"culture {known.culture} ({alignment}) {what} '{event}' (defend={defend}, attack={attack}), so --choice {known.choice} never shows"
                     + (f" (try --culture {OFFER_EVIL_CULTURE})" if known.choice == "attack" else ""))
    known.event = event
    known.story = story
    known.hero = row["hero"]
    known.mission = mission.lower() if mission else None
    known.shown_mission = story.get("mission")
    known.side = story_rows().get(known.mission) if known.mission else None
    known.field, known.must_live = (story_field(known.mission), story_must_live(known.mission)) if known.mission else (False, False)
    wanted = {"defend": "Defending", "attack": "Attacking"}.get(known.choice)
    if wanted and known.side != wanted:
        parser.error(f"story mission '{known.mission}' has side {known.side}, not {wanted} ({STORY_MISSIONS_FILE})")


def default_wall_limit(days, mission_limit=0):
    return max(1800, 600 + days * 20) + mission_limit


def number_text(value):
    return str(int(value)) if float(value).is_integer() else repr(float(value))


def arm_text_for(out, known, args):
    """(arm file text, wall limit). The wall limit is the runner's --wall-limit when given, else computed from the days (campaign-new) or the
    boot allowance plus the mission limit (story-mission and story-offer). For campaign-new the Fords runner stops the process 15 minutes after
    this limit, so the game's own limit trips first. The two story kinds write the key. Their driver reads it only in a --by-date offer run,
    where the wait for the popup is this limit less the mission limit (ScenarioRules.OfferPopupLimit) and the default covers BY_DATE_DAYS of
    campaign. Otherwise --wall-limit only moves the runner's give-up time (this limit plus 15 minutes), which must stay above
    the boot time plus the story phases' own bounds, 2,880 s after the menu: creation 600 (ScenarioRules.CreationWaitSeconds), map 600
    (MapWaitSeconds), four steps of 120 (StoryStepWaitSeconds, or OfferPopupWaitSeconds for the offer's first: menu or popup, open or
    menu, mission seen, return) and the 1200 s mission limit (ScenarioPlan.DefaultMissionLimitSeconds). The story-mission kind writes no days,
    speed or stall seconds: its driver reads none of them. The story-offer kind writes the speed it holds while it waits for the popup."""
    if known.kind == KIND_OFFER:
        # The same steps as the story kind (the popup wait, the menu, the mission seen and the return, four waits of 120 s) and the same 1200 s mission.
        # By date, the popup wait is the days until the window opens: the wall limit covers BY_DATE_DAYS of campaign and the driver waits that long.
        by_date = getattr(known, "by_date", False)
        wall_limit = args.wall_limit if args.wall_limit else default_wall_limit(BY_DATE_DAYS if by_date else DEFAULT_DAYS, DEFAULT_MISSION_LIMIT_SECONDS)
        speed = (known.speed if known.speed is not None else DEFAULT_SPEED) if by_date else OFFER_SPEED
        pairs = [("kind", known.kind), ("out", out), ("scenario", known.scenario), ("event", known.event), ("choice", known.choice),
                 ("mission_limit_seconds", DEFAULT_MISSION_LIMIT_SECONDS), ("culture", known.culture), ("speed", number_text(speed)),
                 ("wall_limit_seconds", number_text(wall_limit)), ("quit", 1)]
        pairs += [("open_by_date", 1)] if by_date else [("command", f"taom.chronicle_open {known.event} confirm")]
        # The effects first, the status last: the status entry is the one the printer shows in its own block; commands.txt holds both.
        pairs += [("command", c) for c in known.command] + [("after", "taom.war_effects"), ("after", "taom.chronicle_status")]
        return "".join(f"{key}={value}\n" for key, value in pairs), wall_limit
    if known.kind == KIND_STORY:
        wall_limit = args.wall_limit if args.wall_limit else default_wall_limit(DEFAULT_DAYS, DEFAULT_MISSION_LIMIT_SECONDS)
        pairs = [("kind", known.kind), ("out", out), ("scenario", known.scenario), ("mission", known.mission),
                 ("mission_limit_seconds", DEFAULT_MISSION_LIMIT_SECONDS), ("culture", known.culture),
                 ("wall_limit_seconds", number_text(wall_limit)), ("quit", 1)] + [("command", c) for c in known.command]
        return "".join(f"{key}={value}\n" for key, value in pairs), wall_limit
    wall_limit = args.wall_limit if args.wall_limit else default_wall_limit(known.days)
    pairs = [("kind", known.kind), ("out", out), ("scenario", known.scenario), ("culture", known.culture), ("days", known.days),
             ("speed", number_text(known.speed)), ("stall_seconds", known.stall_seconds),
             ("wall_limit_seconds", number_text(wall_limit)), ("quit", 1)] + [("command", c) for c in known.command]
    return "".join(f"{key}={value}\n" for key, value in pairs), wall_limit


def parse_count(done, key):
    try:
        return int(done.get(key, ""))
    except ValueError:
        return None


def said_verdict(done_pass, lines):
    """The Won or Lost verdict of the first line the done pattern matches (its first group), or None when no line matches."""
    found = next((m for m in (re.search(done_pass, line) for line in lines) if m), None)
    return found.group(1) if found else None


def story_log_problems(log_text, mission, side, field=None, must_live=None):
    """The story kind's log conditions, one reason each. The driver's done line alone could pass a mission the player's own agent walked out
    of (reason=Abandoned reads as a loss), so the story's end line and its boot line are required too, and a run holds one launch only.
    side is the row's side, Defending or Attacking: an end line of the other side is no pass, nor is one whose verdict is not the done
    line's. field and must_live are the row's traits as the launch read them; None reads the checkout (a caller with no armed plan)."""
    lines = log_text.splitlines()
    problems = []
    done_pass = (r"\[Check\] id=" + re.escape(STORY_CHECK_ID) + r" result=pass reason=ok mission=" + re.escape(mission)
                 + r" verdict=(Won|Lost) mission_seconds=\S+ culture=\S+$")
    said = said_verdict(done_pass, lines)
    if said is None:
        problems.append(f"the TAOM log has no [Check] id={STORY_CHECK_ID} result=pass line for mission {mission} with a Won or Lost verdict")
    field = story_field(mission) if field is None else field
    boot = STORY_FIELD_BOOT_PASS if field else STORY_BOOT_PASS + "$"
    if not any(re.search(boot, line) for line in lines):
        problems.append(f"the TAOM log has no taom.story-missions.hosted.boot pass line for a {'field' if field else 'siege'} mission")
    ended = story_ended_pass(side, story_must_live(mission) if must_live is None else must_live, said)
    if not any(re.search(ended, line) for line in lines):
        problems.append(f"the TAOM log has no taom.story-missions.ended pass line with side={side}" + (f" and verdict={said}, as the done line says" if said else "")
                        + " (a reason=Abandoned end is not one, nor a reason that cannot give its verdict)")
    launch = r"\[StoryMissions\] launching \S+ for '" + re.escape(mission) + r"' \(story\d+, autoplay True\)"
    wanted = [line for line in lines if re.search(launch, line)]
    others = [line for line in lines if ANY_LAUNCH.search(line)]
    if len(wanted) != 1 or len(others) != 1:
        problems.append(f"the TAOM log holds {len(wanted)} launching lines for '{mission}' with autoplay True and {len(others)} launching "
                        "lines in all (wanted one and one)")
    return problems


def offer_log_problems(log_text, armed):
    """The story-offer kind's log conditions, one reason each (armed: event, choice, hero, mission, side, field, must_live, shown_mission, as parse_offer set them).
    The offer.shown line names the window's own mission whichever side was chosen; a fate run launches nothing."""
    lines = log_text.splitlines()
    problems = []
    event, choice = armed.event, armed.choice
    verdict = "-" if choice == "fate" else r"(Won|Lost)"
    done_pass = (r"\[Check\] id=" + re.escape(OFFER_CHECK_ID) + r" result=pass reason=ok event=" + re.escape(event) + " choice=" + choice
                 + " verdict=" + verdict + r" mission_seconds=\S+ culture=\S+$")
    said = said_verdict(done_pass, lines) if choice != "fate" else None
    if not any(re.search(done_pass, line) for line in lines):
        problems.append(f"the TAOM log has no [Check] id={OFFER_CHECK_ID} result=pass line for event {event}, choice {choice}"
                        + ("" if choice == "fate" else " and a Won or Lost verdict"))
    shown = r"\[Check\] id=taom\.story-missions\.offer\.shown result=pass mission=" + re.escape(str(armed.shown_mission)) + " "
    if not any(re.search(shown, line) for line in lines):
        problems.append(f"the TAOM log has no taom.story-missions.offer.shown pass line for mission {armed.shown_mission}")
    others = [line for line in lines if ANY_LAUNCH.search(line)]
    if choice == "fate":
        resolved = (r"\[WarChronicle\] '" + re.escape(event) + r"' Occurred on day \d+ \(the player let fate decide" if armed.hero
                    else r"\[WarChronicle\] '" + re.escape(event) + r"': the player let fate decide the story offer\.")
        if not any(re.search(resolved, line) for line in lines):
            problems.append(f"the TAOM log has no [WarChronicle] line that says the decline of '{event}' landed")
        if others:
            problems.append(f"the TAOM log holds {len(others)} launching lines, but a fate run opens no mission")
        return problems
    mission, side = armed.mission, armed.side
    # The traits parse_offer read at launch; an armed plan without them (older callers) reads the checkout.
    field, must_live = getattr(armed, "field", None), getattr(armed, "must_live", None)
    field = story_field(mission) if field is None else field
    boot = STORY_FIELD_BOOT_PASS if field else STORY_BOOT_PASS + "$"
    if not any(re.search(boot, line) for line in lines):
        problems.append(f"the TAOM log has no taom.story-missions.hosted.boot pass line for a {'field' if field else 'siege'} mission")
    same = f" and verdict={said}, as the done line says" if said else ""
    ended = story_ended_pass(side, story_must_live(mission) if must_live is None else must_live, said)
    if not any(re.search(ended, line) for line in lines):
        problems.append(f"the TAOM log has no taom.story-missions.ended pass line with side={side}{same}"
                        " (a reason=Abandoned end is not one, nor a reason that cannot give its verdict)")
    if not any(re.search(offer_result_pass(side, armed.hero, said), line) for line in lines):
        problems.append(f"the TAOM log has no taom.story-missions.offer.result pass line with side={side}{same} and the result that verdict gives"
                        " (the verdict did not reach the chronicle, or the lines contradict each other)")
    launch = r"\[StoryMissions\] launching \S+ for '" + re.escape(mission) + r"' \(story\d+, autoplay True\)"
    wanted = [line for line in lines if re.search(launch, line)]
    if len(wanted) != 1 or len(others) != 1:
        problems.append(f"the TAOM log holds {len(wanted)} launching lines for '{mission}' with autoplay True and {len(others)} launching "
                        "lines in all (wanted one and one)")
    return problems


def judge(done, report_json, log_text=None, armed=None, checkout=None):
    """(exit code, why). Every failed condition adds one reason; the reasons are joined with '; '. armed is the parsed plan the done file
    must echo; checkout is (HEAD sha, {path: mtime}) as main read it with autorun_build.checkout_state() before the game started, which the
    log's [BuildStamp] must match (autorun_build.stale_build). main passes both; a judge called without a checkout fails."""
    if not done:
        return 1, "no done file"
    kind = armed.kind if armed is not None else done.get("kind")
    offer = kind == KIND_OFFER
    story = kind == KIND_STORY
    mission = armed.mission if armed is not None and story else done.get("mission", "")
    problems = []
    if armed is not None:
        echoed = [("kind", armed.kind), ("scenario", armed.scenario), ("culture", armed.culture)]
        if offer:
            echoed += [("event", armed.event), ("choice", armed.choice)]
        else:
            echoed.append(("mission", armed.mission) if story else ("days_wanted", str(armed.days)))
        for key, want in echoed:
            if done.get(key) != want:
                problems.append(f"{key}={done.get(key, '?')} in the done file, but the arm file asked for {want}")
    reason = done.get("reason", "")
    if reason != "ok":
        problems.append(f"reason={reason or '?'} (wanted ok)")
    if story or offer:
        fate = offer and armed is not None and armed.choice == "fate"
        if fate and done.get("verdict") != "-":
            problems.append(f"verdict={done.get('verdict') or '?'} in the done file (a fate run plays no mission: wanted -)")
        elif not fate and done.get("verdict") not in ("Won", "Lost"):
            problems.append(f"verdict={done.get('verdict') or '?'} in the done file (wanted Won or Lost)")
        days_run = days_wanted = None
    else:
        days_run, days_wanted = parse_count(done, "days_run"), parse_count(done, "days_wanted")
        if days_run is None or days_wanted is None:
            problems.append(f"days_run={done.get('days_run', '?')} or days_wanted={done.get('days_wanted', '?')} is not a whole number")
        elif days_run < days_wanted:
            problems.append(f"days_run={days_run} is below days_wanted={days_wanted}")
    if log_text is None:
        problems.append("no TAOM log copy, so the result=pass line and the crash scan could not be checked")
    else:
        if offer:
            if armed is None:
                problems.append("the story-offer kind is judged against the armed plan only")
            else:
                problems.extend(offer_log_problems(log_text, armed))
        elif story:
            # The side parse_own read at launch; a judge called without it (older callers) reads the checkout.
            side = getattr(armed, "side", None) if armed is not None else None
            if side is None:
                side = story_side(mission)
            if side is None:
                problems.append(f"'{mission}' is not a story mission of this checkout, so its side is unknown")
            else:
                problems.extend(story_log_problems(log_text, mission, side, getattr(armed, "field", None), getattr(armed, "must_live", None)))
        elif not PASS_LINE.search(log_text):
            problems.append(f"the TAOM log has no [Check] id={CHECK_ID} result=pass line")
        # The whole log is this session's (the runner takes the newest log created after launch), and TAOM's load comes before the driver.
        block = runner.crash_report_line(log_text, None)
        if block is not None:
            problems.append("the TAOM log holds a CrashReport block: " + block[:200])
        if UNLOAD_LINE not in log_text:
            problems.append("the log has no clean module unload line")
        warning = next((line for line in log_text.splitlines() if ARM_WARNING in line), None)
        if warning is not None:
            problems.append("the game did not take the arm file as written: " + warning[:200])
    build = None
    if checkout is None:
        problems.append("the checkout was not read at launch, so the build cannot be judged")
    elif log_text is not None:
        problem = autorun_build.stale_build(log_text, *checkout)
        if problem is not None:
            problems.append("the game ran a build that is not this checkout: " + problem)
        else:
            build = autorun_build.build_label(log_text)
    if problems:
        return 1, "; ".join(problems)
    proved = f"build {build}, " if build else ""
    if offer:
        return 0, (f"{proved}reason=ok, event {armed.event} choice {armed.choice} verdict {done['verdict']}, back on the map, pass lines present, "
                   "no CrashReport block, clean unload")
    if story:
        return 0, (f"{proved}reason=ok, mission {mission} verdict {done['verdict']}, back on the map, pass line present, "
                   "no CrashReport block, clean unload")
    return 0, f"{proved}reason=ok, {days_run} of {days_wanted} days, pass line present, no CrashReport block, clean unload"


def run_ledger(log_copy, say):
    """Runs analyze_war_ledger.py on the copied log and prints it; on exit 0 saves ledger.txt beside the log. A failure is reported,
    never fatal, and leaves no ledger.txt."""
    try:
        done = subprocess.run([sys.executable, "-X", "utf8", str(LEDGER_TOOL), str(log_copy)], capture_output=True, text=True,
                              encoding="utf-8", errors="replace", timeout=LEDGER_TIMEOUT_SECONDS)
    except (OSError, subprocess.SubprocessError) as error:
        say(f"war ledger not produced: {error}")
        return
    text = done.stdout + (("\n" + done.stderr) if done.stderr else "")
    print(f"---- war ledger (exit {done.returncode}) ----\n" + text.strip())
    if done.returncode != 0:
        say(f"war ledger tool exited {done.returncode}; no {LEDGER_FILE}: " + text.strip()[:200])
        return
    try:
        (Path(log_copy).parent / LEDGER_FILE).write_text(text, encoding="utf-8")
    except OSError as error:
        say(f"war ledger not saved: {error}")


def say_if_unarmed(text, say):
    """An unarmed build writes no done file and ends as the runner's stall, so the judge never runs for it: the printers name the cause
    from the first 'not armed:' line of the collected log."""
    line = next((line for line in text.splitlines() if NOT_ARMED in line), None)
    if line is not None:
        say("this build did not arm the run: " + line[line.index(NOT_ARMED):][:300])


def show_results(done_text, log_copy, report_json, say):
    print(f"---- {DONE_FILE} ----\n" + done_text.strip())
    if log_copy is None:
        say("no TAOM log copy, so no [Check] lines and no war ledger")
        return
    text = Path(log_copy).read_text(encoding="utf-8", errors="replace")
    say_if_unarmed(text, say)
    print("---- [Check] lines of the log ----")
    for line in text.splitlines():
        if "[Check] id=taom.autorun." in line:
            print(line)
    stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
    print(stamp[0] if stamp else "no [BuildStamp] line")
    say("clean module unload line: " + ("present" if UNLOAD_LINE in text else "ABSENT"))
    show_commands(Path(log_copy).parent, say)
    run_ledger(log_copy, say)


def show_commands(folder, say):
    """Prints commands.txt, the armed console commands' output, when the run wrote one (#1434)."""
    path = Path(folder) / COMMANDS_FILE
    if path.is_file():
        print(f"---- {COMMANDS_FILE} ----\n" + path.read_text(encoding="utf-8", errors="replace").strip())
        return
    arm = Path(folder) / "arm.txt"
    if arm.is_file() and "\ncommand=" in "\n" + arm.read_text(encoding="utf-8", errors="replace"):
        say(f"the arm file asked for console commands, but the run wrote no {COMMANDS_FILE}")


def show_story_results(done_text, log_copy, report_json, say):
    """The story kind's printer: the done file, the driver's and the story's [Check] lines, the launch line and the build stamp. It runs no
    war ledger: a story run plays no campaign days."""
    print(f"---- {DONE_FILE} ----\n" + done_text.strip())
    if log_copy is None:
        say("no TAOM log copy, so no [Check] lines")
        return
    text = Path(log_copy).read_text(encoding="utf-8", errors="replace")
    say_if_unarmed(text, say)
    print("---- [Check] and launch lines of the log ----")
    for line in text.splitlines():
        if "[Check] id=taom.autorun." in line or "[Check] id=taom.story-missions." in line or ANY_LAUNCH.search(line):
            print(line)
    stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
    print(stamp[0] if stamp else "no [BuildStamp] line")
    say("clean module unload line: " + ("present" if UNLOAD_LINE in text else "ABSENT"))
    show_commands(Path(log_copy).parent, say)


# ScenarioAutorunDriver logs one warning per refused run: "[Autorun][Scenario] <reason>: <why> open_menu=<id>".
DRIVER_REFUSAL = re.compile(r"\[Autorun\]\[Scenario\] (offer_\w+|return_failed|mission_\w+|tick failed in )")
LOG_STAMP = re.compile(r"^\[\d{4}-\d\d-\d\d ")


def status_block(text):
    """The driver logs each console command as one entry, its output on the lines after it; this returns the taom.chronicle_status entry
    (the command line and the lines up to the next timestamped log line), or an empty list."""
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if "[Autorun][Scenario] command taom.chronicle_status" in line:
            block = [line]
            for follow in lines[index + 1:]:
                if LOG_STAMP.match(follow):
                    break
                block.append(follow)
            return block
    return []


def show_offer_results(done_text, log_copy, report_json, say):
    """The story-offer kind's printer: the done file, the driver's and the story's [Check] lines, the launch line, the chronicle's own lines
    for the event, the driver's refusal warning (offer_refused, offer_not_shown, return_failed or a mission_ reason, with its cause), the
    chronicle_status output and the build stamp. It runs no war ledger: an offer run plays no campaign days."""
    print(f"---- {DONE_FILE} ----\n" + done_text.strip())
    if log_copy is None:
        say("no TAOM log copy, so no [Check] lines")
        return
    text = Path(log_copy).read_text(encoding="utf-8", errors="replace")
    say_if_unarmed(text, say)
    found = re.search(r"^event=(\S+)", done_text, re.MULTILINE)
    chronicle = f"[WarChronicle] '{found.group(1)}'" if found else "[WarChronicle] '"
    print("---- [Check], launch and chronicle lines of the log ----")
    for line in text.splitlines():
        if ("[Check] id=taom.autorun." in line or "[Check] id=taom.story-missions." in line or ANY_LAUNCH.search(line)
                or chronicle in line or DRIVER_REFUSAL.search(line)):
            print(line)
    block = status_block(text)
    print("---- taom.chronicle_status ----\n" + ("\n".join(block) if block else "no taom.chronicle_status entry in the log"))
    stamp = [line for line in text.splitlines() if "[BuildStamp]" in line]
    print(stamp[0] if stamp else "no [BuildStamp] line")
    say("clean module unload line: " + ("present" if UNLOAD_LINE in text else "ABSENT"))
    show_commands(Path(log_copy).parent, say)


def main(argv=None):
    known, rest = parse_own(argv)

    def armed_text(out, args):
        return arm_text_for(str(out), known, args)

    runner.ENV_VAR = ENV_VAR
    runner.DONE_FILE = DONE_FILE
    runner.STATUS_FILE = STATUS_FILE
    runner.RUNS_ROOT = RUNS_ROOT
    runner.arm_text_for = armed_text
    # Read once, before the game starts: a commit or an edit made during the run is not the deployed build.
    checkout, refusal = autorun_build.read_checkout_for_launch()
    if refusal:
        print(refusal)
        return 1
    runner.judge = lambda done, report_json, log_text=None: judge(done, report_json, log_text, known, checkout)
    if known.kind in (KIND_STORY, KIND_OFFER):
        # Helm's Deep can stop its load at the terrain shader dialog, which the Long Defeat runner's handler presses Cancel on.
        runner.handle_dialogs = long_defeat.handle_dialogs
        runner.show_results = show_story_results if known.kind == KIND_STORY else show_offer_results
    else:
        runner.show_results = show_results
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
