TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You reviewed an uncommitted change for issue #1079 (The Long Defeat, Phase 4, Slices 2 and 3) in this working tree (branch `feat/ld-phase4`, HEAD `5cf79902c`) earlier today; your report is `docs/reviews/raw/codex-prereview-ld-phase4-s2-s3-2026-11-21.md` (the findings start near the end of that file). The implementer has since changed the tree to fix your five P2 findings and the findings of three other reviewers. This is a read-only re-review of those fixes: do not edit, stage, build or commit anything. Show the whole uncommitted change with `git diff HEAD` and `git ls-files --others --exclude-standard`.

## What was fixed, and where

1. `--lord-falls` was armed while nothing fells the lord, the judge passed such a run, and an unanswered ask marked a later natural death as forced.
   - `Main/Features/LongDefeat/Domain/LordLife.cs`: `ForcedFallDue(dt, fighting, wave)` stores the wave; `Fell` books `forced` only when the body is the lord's own and the wave is the ask's.
   - `tools/run_long_defeat.py`: `LORD_FALLS_DELIVERED = False` makes `lord_falls_problem` refuse the flag; `lord_problem` requires a death in the `lordFalls` wave once that wave was cleared.
   - `TAOM.Tests/Features/LongDefeat/LongDefeatEffectsServedTests.cs` (new): every `RunEffects` property is read by a `LongDefeatDirector*.cs` file or named in `Unserved`.
2. A lord whose body left with no death (an AI-driven lord who routs, a refused first spawn) was no longer raised at a clear.
   - `Main/Features/LongDefeat/LongDefeatDirector.cs`: the tick's `RunSnapshot` now carries `_field.ReadLord(false)`.
   - `TAOM.Tests/Features/LongDefeat/FakeLongDefeatField.cs`: `ReadLord` returns no lord when `MainBody` is 0.
3. The lord's clocks could ask on the tick that cleared the wave: `SurvivalRun.Lord.cs` now has `ObserveLordLife` (top of `Tick`) and `TickLordClocks` (last step of `Tick`, in `SurvivalRun.cs`).
4. Death rows carried no troop: `LongDefeatDirector.LordFell(troopId)`.
5. An errand ran on for a fallen lord: `SurvivalRun.Locations.cs` `TickErrand` closes it; `LocationsView.LordStands`; `ShopBot.Place`.
6. The judge: `lord_problem` requires the `lord` block and `playerDeaths`, bounds every row to the waves the run played, allows one rise a clear, and no longer ties a takeover or a rise to a death (a rout leaves no death). `purchase_problem` no longer crashes on a non-text gear set id. The build hint in `spent_on_keys_note` covers the three newest sinks.
7. Not fixed in code, recorded for a later slice: an errand's `End` order is lost when the run ends (no code walks an errand yet; the director must release it at `Finish`, Slice 5).

## What to check

1. Each fix: does it close the finding, and does it open a new defect? Give a concrete state for any that it does not close.
2. The lord reading now reaches the run every tick in today's game. Trace what changes for a shipped run (nothing is offered at a place): the body state, the takeover and seat effects (still unserved), the report's `takeovers` and `rises`, a co-op host, a story mission that hosts the run in a campaign (`Main/Features/StoryMissions/`). Does any path now play or report differently from trunk in a way the judge (`tools/run_long_defeat.py`) or a player would meet? Note the sally: a mounted lord's one-step remount, and `ReadLord`'s `Own` flag (`Main/Adapters/LongDefeatBattlefieldAdapter.Player.cs`).
3. `lord_problem` after the change: a report this build can write that it refuses, and an edited report it should refuse and accepts. The game books a fall in the preparation to `max(1, wave)` where `wave` is `startWave - 1`.
4. `LongDefeatEffectsServedTests`: can it pass while an effect is in fact unserved, or fail for a served one (the regex, comments stripped, an effect read under another name)?

## Output

Findings most severe first, each with a severity (P1 changes shipped play, loses data or breaks a co-op run; P2 a defect in the new code a later slice will meet; P3 minor), the file and line, the concrete state and the line you read. Say UNVERIFIED for any claim you did not read the source for. End with what you checked and found correct.
