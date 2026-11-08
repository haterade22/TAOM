---
name: game-run
description: Use when in-game evidence is owed. Gates on no running game and 10 idle minutes, deploys the tree under test, runs the unattended Fords match, judges the run folder, reports.
argument-hint: "[--live-seconds N] [--scene S] [--west W] [--east E] [--ai A] [--west-ai] [--speed 1|2|4|10] [--control-check N] [--runs N]"
---

# Unattended game run

Starts Bannerlord with nobody at the keyboard, lets TAOM's env-armed driver play one Fords of War match, and judges the result from
the logs. The permission, the launch recipe, every game-side file touched and the risks are in
[unattended-game-runs.md](../../../docs/reference/unattended-game-runs.md); read it once if you have not this session. Today the
driver plays Fords of War, and The Long Defeat (#1079) has its own wrapper, `python tools/run_long_defeat.py --waves 5` (add `--lose` for the loss case), under the same gate. A general scenario driver is planned separately and does not exist yet.

Run these phases in order. `$ARGUMENTS` are passed to the launcher in Phase 4.

## Phase 1: Decide a run is needed

A run is owed when in-game evidence is missing for the change, not for its own sake:

- a self-check or an [in-game verdicts](../../../docs/features/ingame-verdicts.md) manifest reads `NOT_SEEN` or `NOT REACHED`, or
- the change touches mission or engine-facing code that a Fords match exercises (mission start, agents, formations, destructibles,
  the Fords driver or report; for soldier control (`FordsControlView`, `FordsAgentControl`) add `--control-check 240 --live-seconds 540`), or code under `LongDefeat` (run `tools/run_long_defeat.py` instead of the Fords runner).

If neither holds, or the check is outside Fords and Long Defeat (no driver exists for it), stop and say what a person must play instead. Do not run
the game to feel safe.

## Phase 2: Gate

The maintainer's standing decision (2026-11-01): start without asking only when **no `Bannerlord.exe` is running and the PC has had
no keyboard or mouse input for 10 minutes**.

```bash
python tools/run_fords_autorun.py --dry-run
```

The dry run prints the idle state and the preflight. If it says `ok`, go to Phase 3. If a game is running or the input age is under
600 s, ask once with `AskUserQuestion` (run now, run later, or skip) unless the maintainer already said yes to a run this session.
Only after that yes, pass `--require-idle-minutes 0` in Phase 4. Never kill or wait out someone's running game.

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

A failed row is a finding to investigate, not to explain away. A crash goes to `/native-crash-triage` or `/investigate`.

## Phase 6: Restore

Put back anything you changed only for the test: the deployed ModuleData file from its backup, a numeric override, a config toggle.
Confirm the file matches the backup. If the maintainer's own deploy mattered, say that the game folder now holds your build.

## Phase 7: Report

Tell the maintainer, in prose: the run folder, the verdicts (pass, fail and not-seen counts, each FAIL with its reason), the build
deployed (checkout, commit, `[BuildStamp]`), anything restored, and what the run did not cover (no look at the screen unless you read its PNG files, no feel of a soldier fight or the pointer pick, Fords only).
Every run is reported, including one the launcher refused.
