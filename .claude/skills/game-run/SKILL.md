---
name: game-run
description: Use when in-game evidence is owed. Gates on no game and 10 idle minutes, deploys the tree, runs an unattended custom battle or Fords match, judges the run folder, reports.
argument-hint: "[--steps enter,<step>,...] | [--live-seconds N] [--scene S] [--west W] [--east E] [--ai A] [--west-ai] [--speed 1|2|4|10] [--control-check N] [--runs N]"
---

# Unattended game run

Starts Bannerlord with nobody at the keyboard, lets one of TAOM's env-armed drivers play, and judges the result from
the logs. The permission, the launch recipe, every game-side file touched and the risks are in
[unattended-game-runs.md](../../../docs/reference/unattended-game-runs.md); read it once if you have not this session.

**Test your own work in a custom battle** (Mike, 2026-11-20). A session that changes mission code proves it in the game
by itself, and the custom battle of the command trial is the first driver to try: its armies are chosen by the code, it
has a camera that can follow one soldier, and it saves pictures. The four drivers, all under the same gate:

| Your change | Driver | Runs in |
|---|---|---|
| A thing you can watch in an ordinary battle: troops, mounts, creatures and their crews, formations, orders, the Seat of Seeing, Marshal's Word | the custom battle: `python tools/run_command_trial.py --steps enter,<step>,...` | `E:/Temp/command-trial-runs` |
| Fords of War code, or a whole match with towers, heroes and upgrades | `python tools/run_fords_autorun.py` | `E:/Temp/fords-runs` |
| Code under `LongDefeat` (#1079) | `python tools/run_long_defeat.py --waves 5` (add `--lose` for the loss case) | its own folder |
| A new campaign for N days (#1075) | `python tools/run_scenario.py --days N` (it does not build, so Phase 3 comes first) | its own folder |

Run these phases in order. `$ARGUMENTS` are passed to the launcher in Phase 4.

## Phase 1: Decide a run is needed

A run is owed when in-game evidence is missing for the change, not for its own sake:

- a self-check or an [in-game verdicts](../../../docs/features/ingame-verdicts.md) manifest reads `NOT_SEEN` or `NOT REACHED`, or
- the change touches mission or engine-facing code that a Fords match exercises (mission start, agents, formations, destructibles,
  the Fords driver or report; for soldier control (`FordsControlView`, `FordsAgentControl`) add `--control-check 240 --live-seconds 540`), or code under `LongDefeat` (run `tools/run_long_defeat.py` instead of the Fords runner).

- the change touches mission code that a custom battle shows and no step of the command trial proves it yet. Add the step
  first, then run it: the recipe is "A custom battle check for your own work" in
  [unattended-game-runs.md](../../../docs/reference/unattended-game-runs.md#a-custom-battle-check-for-your-own-work). The
  steps that exist are the tuples in `tools/run_command_trial.py` (`SEAT_STEPS`, `MARSHAL_STEPS`, `CREW_STEPS`, `STAMINA_STEPS`, `SPIDER_STEPS`, `WARG_STEPS`).

If none holds, or no driver can reach the check (campaign screens, a town or tavern scene, a siege, co-op), stop and say what a
person must play instead. Do not run the game to feel safe.

**Run now, or wait and run once** (Mike, 2026-11-18). The game takes the whole PC, so ask one question before each run: does the
next step of your task depend on this result?

- **Yes:** run now.
- **No:** do not run yet. Write the owed check down where the run will find it (its manifest under `tools/ingame_checks/`, or a line
  in your task list), go on with your tasks and sub-tasks, and do one run at the end that covers every check you collected.

Either way, one run proves as much as it can. Before Phase 4, list what else is owed with
`python tools/ingame_verdict.py --owed`, and choose the driver, scene and arguments that reach the most of those checks beside your
own. A longer run that settles five checks beats five runs.

## Phase 2: Gate

The maintainer's standing decision (2026-11-01): start without asking only when **no `Bannerlord.exe` is running and the PC has had
no keyboard or mouse input for 10 minutes**. A co-op game run always goes first (Mike, 2026-11-13): when another session's co-op
test needs the game, do not start or queue a run ahead of it.

**Armory drift first** (harness retro 2026-11-25, P3). The session-start banner prints once per drift state and per checkout,
so check before every run, from the checkout under test:

```bash
python tools/generate_armory_catalogue.py --check
```

It rebuilds the catalogue in memory from the live Armory and compares it with the catalogue file of this checkout, in about
two seconds. It writes nothing and reads no file time, so it is right in a fresh worktree too. Read its output, not only its
exit code:

- `OK: catalogue.tsv reproduces exactly` (exit 0): go on.
- A line that starts `DRIFT:` (exit 1): stop, run `/armory-audit` and repoint every MISSING_BODY row before any battle,
  because a missing collision body is the #352 infinite load.
- `UNCLASSIFIED:` with no `DRIFT:` line (exit 1): the meshes are the committed ones and some have no culture or category.
  That does not block a run; name it in the report.
- `ERROR:` (exit 2, no install found) or a `Traceback`: unchecked, not absent. Say so in the report and do not run a battle
  on that basis.

A `/manage` tick does not run the audit: on `DRIFT:` it skips the run and lists it as still owed.

```bash
python tools/run_fords_autorun.py --dry-run
```

For the custom battle the dry run is `python tools/run_command_trial.py --steps <list> --dry-run`; it also refuses a step list
the game would refuse. The dry run prints the idle state and the preflight. If it says `ok`, go to Phase 3. If a game is running or the input age is under
600 s, ask once with `AskUserQuestion` (run now, run later, or skip) unless the maintainer already said yes to a run this session.
Only after that yes, pass `--require-idle-minutes 0` in Phase 4. Never kill or wait out someone's running game.
A `/manage` tick never asks: when the dry run does not say `ok`, it skips the run and lists the check as still owed.

## Phase 3: Build and deploy the tree under test

Main session only; a subagent never deploys.

```bash
pwsh ./build.ps1
```

Run it from the checkout that holds the change. It deploys that checkout over the game's `Modules/TAOM`, which replaces the maintainer's
own deploy (his next F5 redeploys trunk). Note the checkout and the commit for the report. To test other numbers, edit only the
deployed ModuleData file, after backing it up; Phase 6 restores it. A failed build ends the skill here with the compiler output.

## Phase 4: Run

```bash
python tools/run_fords_autorun.py --live-seconds 120 $ARGUMENTS
```

120 mission seconds is a smoke; the game default is 600, and an argument of the same name overrides it. If the change alters something drawn on the screen, add `--screenshots 30,90` (live seconds, UI included).

The custom battle, in place of the Fords line:

```bash
python tools/run_command_trial.py --steps enter,crew_mumakil,crew_howdah
```

Name every step your check needs, and `enter` first when a step needs the Seat's view. A run with no `--steps` plays the Seat's
and Marshal's Word's steps, not yours. Its run folder is under `E:/Temp/command-trial-runs/`, and a battle of about 75 s took
2 to 4 minutes of wall time (runs `20261120-132358` and `20261120-133122`). Tell the maintainer to keep off the keyboard: a
real key press reaches the game, and the Seat's own key ends a step.

Start it in the background (`run_in_background`) and wait for its exit notice rather than polling. Its limit is the wall limit
(2400 s by default, or `--wall-limit`) plus 15 minutes; set the Bash timeout above that, never below. The game owns the screen while it
runs. Exit 0 is a pass; any other exit still leaves a run folder
under `E:/Temp/fords-runs/<yyyyMMdd-HHmmss>/`. Never rerun blindly after a crash: read the folder first.

## Phase 5: Judge from the run folder

Read, in this order: `runner_log.txt` and `runner_status.txt`, `fords_autorun_done.txt` (`reason=final` is the only success), the
`fords_report_*_run1.json` (`final` true, the verdict rows, any FAIL), the copied TAOM log (the `[FordsOfWar][Verdict]` lines with
`final=1`, any `[ERROR] [CrashReport]` block, `[Shutdown] clean module unload`, and the `[BuildStamp]` line, which must name the
build you deployed), and the `rgl_log` and `watchdog_log` copies if a crash was flagged. Exit code `4294967295` after the game's own
`QuitGame` is normal when the unload line is present. Copy the run folder name into the evidence (commit body, issue comment,
review). For an owed manifest, `python tools/ingame_verdict.py --issue <N>` reads the newest session; confirm its `[BuildStamp]`
is the run's.

With `--control-check`, also read the `[FordsOfWar][Control]` lines in the copied TAOM log: `took`, `returned`, `fell`, and a `rejoin` line 10 seconds after each return. The launcher does not judge them, so quote each one in the report. The run proves the take, the hand-back and the fall; the feel of the fight and the pointer pick stay a player's to check, and the match runs at 1x while a soldier is held, so it is not a balance sample.

With `--screenshots`, the Fords runner leaves PNG files in the run folder: Read each one and quote what it shows against the claim. A `[[human]]` row stays a person's to check.

For a custom battle, read `runner_log.txt` (its last line is the verdict: PASS, or FAIL with the steps), `command_trial_done.txt`
(`reason=final`), then in the copied TAOM log every `[SeatTrial] step=<name> result=<PASS|FAIL> <facts>` line, your feature's own
`[Check]` and status lines, and the same crash, unload and `[BuildStamp]` lines as above. Read every `hud_*.png` and quote what it
shows: the wrapper does not judge pictures. A step that failed before yours (`enter` above all) means your step did not run, so
read its line first.

A failed row is a finding to investigate, not to explain away. A crash goes to `/native-crash-triage` or `/investigate`.

Then judge everything else the run reached, not only your own check: `python tools/ingame_verdict.py --owed --format md` (never
`--post`). A verdict for another issue counts only when the deployed build holds that issue's change; put the run folder and the
`[BuildStamp]` in a comment on that issue.

**A defect outside your task** (an error, a failed row or a wrong result in code your change does not touch): do not fix it and do
not drop it. In this order (Mike, 2026-11-18):

1. Deconflict. `python tools/work_claims.py list --others` and `gh issue list --state open --search "<symptom or file>"`. When a
   `manager-tick` claim is live, tell that session (SendMessage) and let it route the finding. With no manager, ask the session whose
   claim covers that area.
2. An open issue exists: add the run folder and the log lines to it as a comment.
3. Nobody owns it and no issue exists: file one with `/issue`, with the run folder as its evidence. This is the maintainer's
   standing word for a defect that a game run shows; no further ask is needed.
4. Go on with your own task. Name the finding and its issue in the Phase 7 report.

## Phase 6: Restore

Put back anything you changed only for the test: the deployed ModuleData file from its backup, a numeric override, a config toggle.
Confirm the file matches the backup. If the maintainer's own deploy mattered, say that the game folder now holds your build.

## Phase 7: Report

Tell the maintainer, in prose: the run folder, the verdicts (pass, fail and not-seen counts, each FAIL with its reason), the build
deployed (checkout, commit, `[BuildStamp]`), anything restored, and what the run did not cover (no look at the screen unless you read its PNG files, no feel of a soldier fight or the pointer pick, and only what the chosen driver plays).
Every run is reported, including one the launcher refused.
