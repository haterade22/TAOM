# Codex convergence pass: the zero baseline at a fresh split (#1128)

**Role:** reviewer (`.ai/roles/reviewer.md`). Read only: edit no file, run no build that deploys, start no game. Report findings; fix nothing.

**Scope:** only the fix for your earlier HIGH finding, in the uncommitted working tree of `E:\repos\taom-ld-split` (base `4cafca4ab`). Your earlier report is `docs/reviews/raw/codex-prereview-ld-split-release-2026-11-13.md`. The fix:

1. `Main/Features/LongDefeat/LongDefeatDirector.Split.cs`, method `Split`: a fresh split now sets `_closeAtHold = new int[_fort.ReadCloseAttackers().Count]` (zeros of the read's length) instead of the read itself. The comment above the method gives the reason and the remaining limit.
2. `TAOM.Tests/Features/LongDefeat/LongDefeatDirectorSplitTests.cs`, test `AFreshSplit_StartsFromZero_SoAStaleCountFromTheLastWave_DoesNotHideTheNextContact`. It failed on the old line (the intermission split logged `close 2,0,0`) and passes on the new one.
3. The wording in `docs/features/long-defeat.md` ("The release." paragraph), RCA rows 15 to 22 in `docs/reviews/rca-long-defeat-split-2026-11-12.md`, and the comments in `Main/Features/LongDefeat/Domain/DefenderSplit.cs` (`Contact`) and `Main/Adapters/LongDefeatBattlefieldAdapter.Split.cs` (`ReadCloseAttackers`).

Engine sources (v1.5.7): `C:\Users\mikew\.taom-src\v1.5.7-202611101605\` (`TaleWorlds.MountAndBlade.SiegeQuerySystem.cs`, `SiegeLane.cs`, `SiegeWeapon.cs`, `QueryData`1.cs`).

## Questions

1. Does the zero baseline close the stale-baseline path you found, with no new path where a hold releases early or never releases? Consider: the preparation split, the intermission split, a split again inside a hold (`fresh == false`), and `ReadCloseAttackers` returning an empty list.
2. Is a calm really free of attackers whenever a fresh split happens (`SplitHold.Next`, `SurvivalRun` phases)? Name any path where attackers stand in a calm.
3. Does the new test test what its name says, and would it fail on the old line for the reason it names?
4. Are the corrected engine sentences now accurate against `DetermineLaneState`, `SiegeWeapon.IsUsed` and the `SiegeQuerySystem` constructor (the right-origin fallback)?

## Output

Findings first, ranked by severity, each with file:line and evidence, marked observed or inferred. Then a one-line verdict.
