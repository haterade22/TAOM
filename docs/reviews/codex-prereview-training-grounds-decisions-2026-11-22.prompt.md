# Codex independent pre-review: three Training Grounds decisions (#1528, #1529), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, start no game, run no build or test (another process
is building in this worktree), run no git command that changes anything. Report defects with
evidence (file, line, the input or state that fails). Engine facts come from the installed game
(`pwsh tools/taom-src.ps1 path <Type>`), never from a name. A claim you cannot prove is UNVERIFIED.

## What to review

The uncommitted change in this worktree (branch `feat/training-grounds-decisions`, base 27f213dc4):
`git status --short` and `git diff` list it. Five source files, four test files, two docs.

1. `Main/Features/TrainingGrounds/Teams/TeamPracticeRules.cs` (`WinCountSlipped`) and
   `Hooks/TeamPracticeFight.cs` (`TryDeclareWin`): after a team win is declared, vanilla's alive
   count is put back at zero when it has gone below.
2. `Main/Features/TrainingGrounds/Waves/Domain/StakeOptions.cs`, `Domain/WaveRun.cs` (`SmallStake`,
   `MaxStakeOf`) and `WaveBetService.cs` (`StakeChoices`): the lowest of the three stakes on offer is
   a quarter of the FIRST wave's limit on every wave.
3. `Main/Features/TrainingGrounds/Waves/Domain/WaveRun.cs` (`RecordPaid`) and `WaveBetService.cs`
   (`PayClearedWave`): when less gold arrives than the payout, the run's account follows it.

The feature docs are `docs/features/team-practice.md` and `docs/features/training-waves.md`.

## Intent (the maintainer's decisions; do not relitigate them)

- Team practice fight: an ally who falls in the seconds between the declared win and the player's
  return to the master must not cost the player the top prize or the team win text.
- Wave fight: clearing waves raises what the player may wager, never what he must. A small wager
  stays on offer in every wave.
- Wave fight: the run's record of what was won must be the gold that arrived, also when the engine
  stops a purse at its limit.
- A lost wager into a town treasury at its limit is left as it is.

## What to try to disprove

1. That the count is put back only after a team win. Walk vanilla's
   `ArenaPracticeFightMissionController.OnMissionTick`, `OnAgentRemoved` and `StartPractice` with the
   prefix: after a loss, after a vanilla-style win, when the player is not practising, in a town
   that is not a team town, and when the reflection access is missing. Can the new write turn a loss
   into a win, pay the top prize when vanilla would not, hide a fighter who is still to fall, or run
   after the practice was reset?
2. That the small stake is right in every state: wave one, a later wave, the MCM tournament cap at
   0, below the minimum stake, and between values, a perk factor above 1, a table whose first row
   has a larger cap than a later row cannot have (the reader raises a later cap), a table with one
   row. Can the offer show with no stake a player can click, can a stake above the wave's limit or
   under the table's minimum be offered or accepted, can the three choices be out of order or
   repeat, and does every reader of the smallest stake (the "too poor" rule and its text, the stake
   lines, `PlaceStake`) agree?
3. That the account is right: a purse at the int limit before the payout, a payout that arrives in
   part, a grant the purse refuses, a negative or larger change, the last wave, and what the notice,
   the debrief lines, the `payout` and `run-end` outcome lines and the returned value then say. Can
   `TotalPaid` or `Net` go wrong, can a second call correct twice, and does the day rule or the
   refund path read a value this changes?
4. That the docs and tests say only what the code does, and that each new test can fail for the
   defect it names.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the evidence you read, and the smallest input or state that shows the defect. Then a
list of what you checked and found correct, and a list of what you could not verify and why.
