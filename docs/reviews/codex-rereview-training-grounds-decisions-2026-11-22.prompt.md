# Codex re-review: fixes to the three Training Grounds decisions (#1528, #1529), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, start no game, run no build or test (another process
is building in this worktree), run no git command that changes anything. Report defects with
evidence (file, line, the input or state that fails). Engine facts come from the installed game
(`pwsh tools/taom-src.ps1 path <Type>`), never from a name. A claim you cannot prove is UNVERIFIED.

## What to review

The uncommitted change in this worktree (branch `feat/training-grounds-decisions`, base 27f213dc4)
was reviewed once (prompt `docs/reviews/codex-prereview-training-grounds-decisions-2026-11-22.prompt.md`,
intent unchanged). Fixes followed. Review the code as it stands, as new code.

1. `Main/Features/TrainingGrounds/Waves/Domain/StakeOptions.cs` and `Domain/WaveRun.cs`
   (`SmallStake`): the small stake is now the smallest wager the first wave offers, after rounding
   and the table's minimum, and `StakeOptions.For` falls back to the quarter when a small stake is
   under the minimum.
2. `Main/Features/TrainingGrounds/Teams/TeamPracticeRules.cs` (`WinCountSlipped`) and
   `Hooks/TeamPracticeFight.cs` (`TryDeclareWin`): the count is put back only for a player who
   survived, and one log line is written when it is.
3. `Main/Features/TrainingGrounds/Waves/WaveBetService.cs` (`PayClearedWave`): the correction is now
   `run.RecordPaid(rose); payout = run.LastPayout;`.

## What to try to disprove

1. That clearing waves can never raise the smallest wager on offer. Try every table the reader
   accepts (`Config/WaveTableReader.cs`): a minimum stake from 1 to 1,000, caps from 10 up, later
   caps equal to or above earlier ones, one row, with the MCM tournament cap at 0, low and between
   values, and a perk factor from 1 to 10. Name any table and purse where the smallest choice of a
   later wave is above the smallest choice of an earlier wave, where the choices are out of order or
   repeat, where a choice is above the wave's limit or under the minimum, or where the offer shows
   with no stake a player can click.
2. That `PayClearedWave` returns and records the right amount in every case after the
   simplification: the full payout, a part, nothing, more than the payout, a purse that fell, and
   the last wave.
3. That the survived condition is read at the right time (`IsPlayerSurvived` is set by vanilla in
   the tick that declares the end) and cannot block the guard for a player who did win.
4. That the tests can fail for the defects they name and the docs say only what the code does.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the evidence you read, and the smallest input or state that shows the defect. Then a
list of what you checked and found correct, and a list of what you could not verify and why.
