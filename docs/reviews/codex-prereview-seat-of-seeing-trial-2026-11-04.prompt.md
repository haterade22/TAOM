# Codex pre-review: Seat of Seeing field trial harness, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The harness landed as the top commit of this tree. It plays one real custom
battle unattended, presses the Seat's own controls, and writes one `[SeatTrial]` line per fact. It is armed only
by the environment variable `TAOM_COMMAND_TRIAL`.

The contract is `docs/features/seat-of-seeing/build-spec.md` section 12 and Deviations rows 19, 31, 33, 34 and 35.
Its text for S2 and later describes stages not yet built, so their absence is not a finding. Engine source for
v1.5.6 is in `C:\Users\mikew\.taom-src\v1.5.6\` (for example `TaleWorlds.MountAndBlade.Mission.cs`);
`pwsh tools/taom-src.ps1 path <FullTypeName>` decompiles a type that is not cached yet. Mark anything you cannot
prove from code as UNVERIFIED. The review record is `docs/reviews/rca-seat-of-seeing-trial-2026-11-04.md`: do not
repeat a finding in it unless the fix did not close it or the fix made it worse. Read `tools/run_fords_autorun.py`
for what the wrapper reuses, and report a defect there as a follow-up. The earlier passes on S1 itself are
closed, so review S1's own files only where the trial's members touch them.

Files: `Client/Features/SeatOfSeeing/Trial/*.cs`, the trial's members in `Client/Features/SeatOfSeeing/SeatView.cs`
and `SeatSession.cs`, its lines in `Client/ClientSubModule.cs`, `Main/Features/SeatOfSeeing/Domain/SeatTrialRoster.cs`,
`SeatTrialRows.cs`, `SeatTrialRules.cs`, `SeatTrialSteps.cs`, `tools/run_command_trial.py`, and their tests.

## Questions to answer with evidence

- **An unarmed session.** Trace every trial member from a player session. Can any of them change behaviour, or cost
  more than the contract's section 12 sentence states? Does the override of `IsOpeningEscapeMenuOnFocusChangeAllowed`
  answer exactly what the base answers? Does the closure at `SeatView.OnMissionScreenInitialize` keep anything alive
  past the mission?
- **Can a step pass with the feature broken?** For each of the six steps in `CommandTrialScript.Seat.cs`, take
  `SeatTrialRules` and the iterator, and name one Seat defect the step would catch and one it would miss. Report any
  step that cannot fail, or whose facts could read true for the wrong reason. For example: does `fly` measure the
  view or the flight state? Does `hero_stands` sample a moment when the hero could not move anyway? Does
  `reenter_keeps_height` compare the same reference twice?
- **The driver.** `CommandTrialScript` holds one enumerator. Walk `Begin`, `Next`, `Settle`, `Verdict.InMissionTick`
  and `Abort`. Can a step run past a done verdict, resume in the wrong tick, or end with no line? Can the budget fail
  a step that already passed? Does a throw inside an iterator leave the run with an outcome?
- **The exit matrix.** List every route from an armed launch to process exit, over `CommandTrialAutostart`,
  `ICommandTrialGameHost` and `CommandTrialRun.Script`. Can a route end with no done file and no quit, leave the
  mission open, or report `final` for a run whose steps did not all run? Walk a throw at each phase, a mission that
  closes under the script, and a game that never reaches the main menu. Does the fake in the tests behave as
  `CommandTrialGameHost` does at each member?
- **The stale deploy check.** Can `stale_build` and `git_state` let a stale deploy pass, or fail a good one? Check the
  time zones of the stamp and the file times, a file newer than the build by less than a second, a `.dirty` stamp
  against each tree state, a path with spaces, a merge that moves `HEAD` after the deploy, and whether
  `WATCHED_PATHS` covers what decides the stamp in `Directory.Build.props`.
- **Reuse through module globals.** `run_command_trial.py` sets globals of `run_fords_autorun.py`. Does any read of
  those names bind early? Can a failure leave them set for the next call in the same process?
- **Threads and ticks.** `FinishDeployment` runs from the screen tick, and agent reads run in the mission tick.
  Compare both with the engine. Is anything written to an agent from the screen tick? Is the deployment call safe
  during the same frame's agent tick?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
