#!/usr/bin/env python
"""Run the field trial of the Seat of Seeing and Marshal's Word (build-spec section 12) in the real game with nobody at the keyboard.

Run:  python tools/run_command_trial.py [--scene battle_terrain_a] [--live-limit S] [--steps enter,fly,...]
                                        [--require-idle-minutes N] [--keep-on-crash S] [--dry-run]

Deploy the tree under test with ./build.ps1 first: a build without the trial idles at the main menu, and the run ends as a stall after
10 minutes. The game plays one custom battle by itself (see docs/features/seat-of-seeing.md, "Field trial"), presses the Seat's own
controls in order, and writes one line per step:

    [SeatTrial] step=<name> result=<PASS|FAIL> <facts>

Exit 0 means every step in --steps has a PASS line and no FAIL line, the done file says reason=final, the log has no CrashReport block,
no failed Seat, Marshal's Word or crew seat check line, no [ERROR] line of the Seat or its trial (a Seat patch that failed to apply and the thread guard's
line for a trial or Seat site count), no [ERROR] line of Marshal's Word (a hotkey registration that failed, a session that could not start, a patch
class of its category that failed or was skipped, the Patch_MissionTime_MarshalsWord category that failed to apply (the two thrower patches), the thrower stand that failed, the order panel's registration failure, the spacing repair's degraded method, and the thread guard's line for one of its adapters' sites), and a clean module unload line, the game left by itself, and the log's [BuildStamp] is this checkout, read once at launch before the game starts
(tools/autorun_build.py, shared with run_scenario.py and run_long_defeat.py): the stamp's sha is HEAD, no tier of the stamp says MISMATCH, its .dirty mark agrees with the tree (dirty with uncommitted files,
clean without), and no uncommitted file under the build's own pathspec (autorun_build.WATCHED_PATHS) is newer than the stamp's build time. A deploy made before the last edit fails the run when
the edited file is still uncommitted (docs/features/seat-of-seeing.md names the cases only a content hash in the stamp could close),
and the PASS text names the build that was judged and the [Session] swallowed counts. A commit or an edit made while the game runs is not read, so it cannot fail a good run. Exit 1 is any failure, including a preflight refusal or an unreadable git at launch (nothing is started); exit
2 is a bad argument; a --dry-run that exits 0 only means the preflight passed. A person still looks at the hud_*.png pictures the run
saves.

A run with no --steps arms every step, the Seat's and Marshal's Word's together, in ONE battle. Prove each stage by its subset; the Seat's rows ran in one battle once.
Marshal's Word's do not all fit it, because the battle runs out of time and soldiers before the volley and mark steps (docs/features/marshals-word.md, "How to verify in game",
lists the subsets). Run each Marshal's Word subset with its own --steps list, one battle each.

The crew steps (#1503) are not in that list: --steps enter,crew_mumakil,crew_howdah fields one mumakil and two war elephants in the player's army,
keeps the Seat's view to an archer of each beast in turn and saves hud_crew_*.png pictures (docs/features/mumakil.md, "The crew steps of the command trial").
A failed taom.crew-seats check line fails the run.

The spider step (#1541) is not in that list either: --steps enter,spider_gaits fields twelve spider riders in the player's army against 300 melee orcs, holds the army, charges it,
holds it again while the Seat follows one rider, saves hud_spider_gaits_*.png pictures and reports the mounts' speeds (docs/features/spider.md, "The spider gait step of the command trial").

The warg step (#1563) is not in that list either: --steps enter,warg_gaits adds twelve warg riders to the player's army against 300 melee orcs, holds the army,
charges it and reports the wargs' top speed, speed limit and scale beside the horsemen's top speed. It cannot see which gait clip
plays, so a PASS means the wargs ran, not that their gaits are right (docs/reference/lotrlome-warg-gait-changes.md).

The stamina steps (#1506) are not in that list either: --steps stamina_gate,stamina_walk,stamina_fight (stamina_gate alone, or with either or both of the others) turns the stamina rules on
for the run, fields 300 orc fighters against the player's army and forces bands on soldiers (docs/features/stamina.md, "The stamina steps of the command
trial"). They run beside no step of another feature. A failed taom.stamina.* check line or an [ERROR] line of the stamina feature fails every run of
this tool, a Seat, Marshal's Word or crew run too: the stamina mission logic and its patch load in every mission.

This reuses the Fords runner (tools/run_fords_autorun.py): its preflight, idle gate, launch recipe, dialog handling, crash scan and
log collection. It only changes the arm variable, the done and status files, the arm text and the judge. The standing permission
and its gate are in docs/reference/unattended-game-runs.md. A builder never launches this; the orchestrator does.
"""
import argparse
import functools
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import autorun_build  # noqa: E402
import run_fords_autorun as runner  # noqa: E402

ENV_VAR = "TAOM_COMMAND_TRIAL"
DONE_FILE = "command_trial_done.txt"
STATUS_FILE = "command_trial_status.txt"
RUNS_ROOT = r"E:\Temp\command-trial-runs"

DEFAULT_SCENE = "battle_terrain_a"
DEFAULT_LIVE_LIMIT = 420

# The step names of Main/Features/SeatOfSeeing/Domain/SeatTrialRows.cs, in its order. A later Seat stage adds its rows to both; another
# feature adds a tuple of its own beside this one and joins it into STEPS, each tied to its row list by a test.
SEAT_STEPS = (
    "refuse_in_deployment",
    "enter",
    "fly",
    "hero_stands",
    "leave_glide",
    "reenter_keeps_height",
    "cursor_with_panel",
    "focus_follows_cursor",
    "keep_panel_reopens",
    "hide_bars_restored",
    "keep_to_soldier",
    "keep_to_company",
    "let_go_on_death",
    "fight_on",
    "morale_held",
    "back_to_stand",
    "pause_flies",
    "slow",
    "fast",
    "adopt_flag",
    "speed_cleared",
    "rise_with_panel",
    "sink_on_close",
    "panel_click_card",
    "panel_click_order",
    "edge_pan",
    "rotate_pivot",
    "zoom_smooth",
    "hero_falls",
    "take_soldier",
)
# The step names of Main/Features/MarshalsWord/Domain/MarshalTrialRows.cs, in its order.
MARSHAL_STEPS = (
    "queue_two_moves",
    "direct_order_clears",
    "array_keeps_offsets",
    "arrive_together",
    "volley_holds",
    "volley_looses",
    "mark_keeps_order",
    "outline_clears",
    "volley_massed",
    "square_side_kept",
    "spacing_one_rider",
    "hold_line_proof",
    "hold_line_slot",
    "hold_line_strike",
    "hold_line_flag",
    "advance_throwers",
    "advance_throwers_vanilla",
)
STEPS = SEAT_STEPS + MARSHAL_STEPS
# The crew steps (#1503), the step names of Main/Features/Elephant/CrewTrialRows.cs: a custom battle with one mumakil and two war elephants in the
# player's army. They run only when --steps names them, because the other steps were proved in a battle with no beast on the field.
CREW_STEPS = ("crew_mumakil", "crew_howdah")
# The stamina steps (#1506), the step names of Main/Features/Stamina/Domain/StaminaTrialRows.cs: 300 orc fighters against the player's army, with the stamina
# rules switched on for the run. They run only when --steps names them, in a battle of their own.
STAMINA_STEPS = ("stamina_gate", "stamina_walk", "stamina_fight")
# The spider step (#1541), the step name of Main/Features/Spider/SpiderTrialRows.cs: twelve spider riders in the player's army against the crew battle's melee enemy,
# held, charged and held again while the Seat follows one of them. It runs only when --steps names it, beside enter and no other step.
SPIDER_STEPS = ("spider_gaits",)
# The warg step (#1563), the step name of Main/Features/Warg/WargTrialRows.cs: twelve warg riders beside the army's horsemen against the crew battle's melee enemy,
# held and charged. It runs only when --steps names it, beside enter and no other step.
WARG_STEPS = ("warg_gaits",)
KNOWN_STEPS = STEPS + CREW_STEPS + STAMINA_STEPS + SPIDER_STEPS + WARG_STEPS
# The two thrower steps each run in a battle of their own: the roster swaps javelineers in for the spearmen for the whole battle (SeatTrialRoster.PlayerFor).
THROWER_STEPS = ("advance_throwers", "advance_throwers_vanilla")

STEP_LINE = re.compile(r"\[SeatTrial\] step=(\w+) result=(PASS|FAIL)\b")
SEAT_CHECK_FAIL = re.compile(r"\bid=(taom\.seat-of-seeing\.[a-z0-9.-]+) result=fail\b")
MARSHAL_CHECK_FAIL = re.compile(r"\bid=(taom\.marshals-word\.[a-z0-9.-]+) result=fail\b")
# A crew seat that needed a rescue teleport writes a failed held line when it lets its archer go (Main/Features/Elephant/CrewSeatChecks.cs, #1482).
# The line is written at the mission's end too, so it can come after the crew step's own PASS line.
CREW_CHECK_FAIL = re.compile(r"\bid=(taom\.crew-seats\.[a-z0-9.-]+) result=fail\b")
# A failed stamina check line (Main/Features/Stamina/StaminaChecks.cs). A run that turns the stamina rules on writes the armed line at its first tick and the lock and refresh lines at its end; a lock line that
# says result=skip does not fail.
STAMINA_CHECK_FAIL = re.compile(r"\bid=(taom\.stamina\.[a-z0-9.-]+) result=fail\b")
# An [ERROR] line of the stamina feature: its own tag, the config provider that fell back to its defaults, and a patch class of its namespace or the
# Patch171_Stamina category that failed to apply or was skipped.
STAMINA_ERROR = re.compile(
    r"\] \[ERROR\] (?:\[Stamina\]|StaminaConfigProvider: |\[PatchApply\] (?:Patch171_Stamina|TAOM\.Features\.Stamina\.\S+) (?:FAILED|SKIPPED))")
# An [ERROR] line of the Seat or its trial (stage S2 onward): a fault, a failed start or a script failure; a Seat patch that failed to apply
# or was skipped (Harmony stops a category at its first failing class, so one line can leave every later patch of it off: Main/PatchCategoryApplier.cs); the
# thread guard's line for a trial or Seat site (MissionThreadGuard.NoteCall, written at LogError by CommandTrialHost, SeatHeroAdapter and SeatScreenAdapter). The
# hot key failure is logged at LogError for the same reason. Another feature's patch or guard line does not fail this run.
SEAT_ERROR = re.compile(
    r"\] \[ERROR\] (?:\[Seat(?:OfSeeing|Trial)\]|\[PatchApply\] \S*Patch140_|\[TAOM\] (?:CommandTrialHost|SeatHeroAdapter|SeatScreenAdapter)\.\w+ ran off the main mission thread)")
# An [ERROR] line of Marshal's Word: its own tag (the hotkey registration that failed, which leaves every key unbound, a session that could not
# start, the order panel's registration that failed, the spacing repair's transpiler that left a method as it was, and the thrower stand that failed); the category Patch141 or the
# Patch_MissionTime_MarshalsWord (the two thrower patches) that failed to apply (the applier writes the category name, one line is the whole category off) or a class of its
# namespace that was skipped (the applier writes the class's full name, which holds no "Patch141_"); and the thread guard's line for one of its
# adapters' native writes. Its fault and its array fault also write a failed check line, which MARSHAL_CHECK_FAIL reads.
MARSHAL_ERROR = re.compile(
    r"\] \[ERROR\] (?:\[MarshalsWord\]"
    r"|\[PatchApply\] (?:\S*Patch141_\S*|Patch_MissionTime_MarshalsWord|TAOM\.Features\.MarshalsWord\.\S+) (?:FAILED|SKIPPED)"
    r"|\[TAOM\] Marshal(?:Order|Fire|Outline)Adapter\.\w+ ran off the main mission thread)")
SESSION_SUMMARY = re.compile(r"\[Session\] summary reason=\S+ .*?\bswallowed=(\d+) shield_swallowed=(\d+)(?:.*?\berrors=(\d+))?")
SESSION_SITE = re.compile(r"\[Session\] swallowed feature=(\S+) site=(\S+) n=(\d+)")
UNLOAD_LINE = "[Shutdown] clean module unload"


def session_swallowed(log_text):
    """The [Session] swallowed counts of the log, for the PASS text: the last summary line's two counters and errors, and every site that was
    written. A PASS with a swallowed exception is still a PASS, and the reader sees the number beside it. errors above zero means a check
    line may be missing (ingame_verdict.py warns of it), so the text carries it too."""
    summaries = SESSION_SUMMARY.findall(log_text)
    if not summaries:
        return "no [Session] summary line in the log"
    swallowed, shielded, errors = summaries[-1]
    text = f"[Session] swallowed={swallowed} shield_swallowed={shielded}"
    if errors:
        text += f" errors={errors}"
    sites = {(feature, site): count for feature, site, count in SESSION_SITE.findall(log_text)}
    if sites:
        text += " (" + ", ".join(f"{feature}.{site}={count}" for (feature, site), count in sites.items()) + ")"
    return text


def judge(done, report_json, log_text=None, steps=STEPS, checkout=None):
    """(exit code, why). A trial run has no JSON report: its evidence is the tagged log lines. The whole log counts for the CrashReport
    check: it opens at this launch, so a boot-time swallow is this run's too. The log must come from this checkout's build.
    checkout is (HEAD sha, {path: mtime}) as main read it with autorun_build.checkout_state() before the game started; a commit or an edit made
    while the game ran is not seen. None fails the run."""
    if not done:
        return 1, "no done file"
    reason = done.get("reason", "")
    if reason != "final":
        return 1, f"reason={reason or '?'}"
    if not log_text:
        return 1, "reason=final but no TAOM log was copied, and that log is the trial's evidence"
    if checkout is None:
        return 1, "the checkout was not read at launch, so the build cannot be judged"
    stale = autorun_build.stale_build(log_text, *checkout)
    if stale is not None:
        return 1, "the game ran a build that is not this checkout: " + stale

    lines = log_text.splitlines()
    results = {}
    for line in lines:
        match = STEP_LINE.search(line)
        if match:
            results.setdefault(match.group(1), []).append(match.group(2))
    failed = sorted({step for step, seen in results.items() if "FAIL" in seen})
    other_fails = [line.strip() for line in lines if "result=FAIL" in line and not STEP_LINE.search(line)]
    faults = sorted({match.group(1) for pattern in (SEAT_CHECK_FAIL, MARSHAL_CHECK_FAIL, CREW_CHECK_FAIL, STAMINA_CHECK_FAIL) for match in map(pattern.search, lines) if match})
    if failed or other_fails:
        named = ", ".join(failed + [line[:120] for line in other_fails])
        return 1, f"FAIL lines: {named}" + (f"; failed checks: {', '.join(faults)}" if faults else "")
    missing = [step for step in steps if step not in results]
    if missing:
        return 1, "reason=final but the log has no [SeatTrial] line for: " + ", ".join(missing)
    block = runner.crash_report_line(log_text, None)
    if block is not None:
        return 1, "the TAOM log holds a CrashReport block: " + block[:200]
    if faults:
        return 1, "a Seat, Marshal's Word, crew seat or stamina check line failed: " + ", ".join(faults)
    errors = [line.strip() for line in lines if SEAT_ERROR.search(line) or MARSHAL_ERROR.search(line) or STAMINA_ERROR.search(line)]
    if errors:
        return 1, f"{len(errors)} [ERROR] line(s) of the Seat, Marshal's Word, the trial or stamina in the log, the first: " + errors[0][:200]
    if UNLOAD_LINE not in log_text:
        return 1, "reason=final but the TAOM log has no clean module unload line"
    build = autorun_build.build_label(log_text)
    return 0, f"reason=final, {len(steps)} steps with a PASS line, no FAIL line, no CrashReport block, no failed Seat, Marshal's Word, crew seat or stamina check, no Seat, Marshal's Word or stamina [ERROR] line, clean unload, {session_swallowed(log_text)}, build {build}"


def normalised_steps(text):
    """The steps in the table's order, names lower-cased and trimmed, duplicates dropped. None means every step of STEPS, which holds no crew and no stamina step.
    A name not in the table raises ValueError, and so does a list that names a thrower step beside any other step, a crew step beside any step but enter, the spider step or the warg step beside any step but enter, or a stamina step beside any step but another stamina step (the game refuses those lists too: SeatTrialSteps.TryChoose). Prerequisites are the game's to check: it refuses to start on a list that skips one, and the done file says start_refused."""
    if text is None:
        return STEPS
    names = {name.strip().lower() for name in text.split(",") if name.strip()}
    if not names:
        raise ValueError("--steps names no step")
    unknown = sorted(names - set(KNOWN_STEPS))
    if unknown:
        raise ValueError("unknown step: " + ", ".join(unknown) + "; known: " + ", ".join(KNOWN_STEPS))
    if len(names) > 1 and names & set(THROWER_STEPS):
        raise ValueError("advance_throwers and advance_throwers_vanilla each run alone, in their own battle")
    spider_beside = sorted(names - set(SPIDER_STEPS) - {"enter"})
    if names & set(SPIDER_STEPS) and spider_beside:
        raise ValueError("a spider step runs with enter only, in a battle of its own; it cannot share it with: " + ", ".join(spider_beside))
    warg_beside = sorted(names - set(WARG_STEPS) - {"enter"})
    if names & set(WARG_STEPS) and warg_beside:
        raise ValueError("a warg step runs with enter only, in a battle of its own; it cannot share it with: " + ", ".join(warg_beside))
    beside = sorted(names - set(CREW_STEPS) - {"enter"})
    if names & set(CREW_STEPS) and beside:
        raise ValueError("a crew step runs with enter only, in a battle of its own; it cannot share it with: " + ", ".join(beside))
    outside = sorted(names - set(STAMINA_STEPS))
    if names & set(STAMINA_STEPS) and outside:
        raise ValueError("a stamina step runs beside other stamina steps only, in a battle of its own; it cannot share it with: " + ", ".join(outside))
    return tuple(step for step in KNOWN_STEPS if step in names)


EPILOG = (
    "Flags of the Fords runner that apply here: --require-idle-minutes, --runs, --keep-on-crash, --runs-root and --dry-run. "
    "--wall-limit moves only the runner's own deadline (the arm file never carries it) and does not end the match. "
    "The Fords match flags (--west, --east, --ai, --west-ai, --speed, --live-seconds, --screenshots) parse and are ignored. "
    "Keep --live-limit well under the wall limit plus 15 minutes, which is the runner's deadline (3300 s by default); at 100000 or more the game keeps its default of 420."
)


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0], epilog=EPILOG)
    parser.add_argument("--scene", default=DEFAULT_SCENE)
    parser.add_argument("--live-limit", type=runner.positive_seconds, default=DEFAULT_LIVE_LIMIT, help="real seconds with a mission open before the game ends the run (default %(default)s)")
    parser.add_argument("--steps", help="a comma list of steps (default: all of " + ", ".join(STEPS) + "). The crew steps run only when named, each with enter: "
                        + ", ".join(CREW_STEPS) + ". The stamina steps run only when named, beside each other and no other step: " + ", ".join(STAMINA_STEPS)
                        + ". The spider step runs only when named, with enter and no other step: " + ", ".join(SPIDER_STEPS)
                        + ". The warg step runs only when named, with enter and no other step: " + ", ".join(WARG_STEPS))
    return parser


def arm_pairs(known, out):
    pairs = [("scene", known.scene), ("live_limit_seconds", f"{known.live_limit:g}")]
    if known.steps:
        pairs.append(("steps", known.steps))
    return pairs + [("out", out), ("quit", 1)]


def main(argv=None):
    parser = build_parser()
    known, rest = parser.parse_known_args(argv)
    try:
        steps = normalised_steps(known.steps)
    except ValueError as error:
        parser.error(str(error))
    known.steps = ",".join(steps) if known.steps else None

    # The checkout is read once, here, before the game starts: the judge uses this state, so a commit or an edit made during the run is not seen.
    checkout, refusal = autorun_build.read_checkout_for_launch()
    if refusal:
        print(refusal)
        return 1

    def arm_text_for(out, args):
        wall_limit = args.wall_limit if args.wall_limit else runner.DEFAULT_WALL_LIMIT_SECONDS
        return "".join(f"{key}={value}\n" for key, value in arm_pairs(known, out)), wall_limit

    runner.ENV_VAR = ENV_VAR
    runner.DONE_FILE = DONE_FILE
    runner.STATUS_FILE = STATUS_FILE
    runner.RUNS_ROOT = RUNS_ROOT
    runner.arm_text_for = arm_text_for
    runner.judge = functools.partial(judge, steps=steps, checkout=checkout)
    return runner.main(rest)


if __name__ == "__main__":
    sys.exit(main())
