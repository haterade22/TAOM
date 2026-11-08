# Codex pre-review: Fords of War culture tactics (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`. A Bannerlord match runs on this machine and needs the CPU. The orchestrator ran the Fords filter after the change (FordsOfWar, the gate tests, the architecture tests: 2,489 passed, 0 failed, 3 skipped).
- Read files, `git diff`, `git status`, and the installed engine's decompiled sources: `pwsh tools/taom-src.ps1 path <Type>` prints the file (cache `C:\Users\mikew\.taom-src\v1.5.6-202611041234`). Look up one type at a time; never decompile every module DLL. Engine: Bannerlord v1.5.6.

## The request (Mike, 2026-11-07)

"We should make tactics for each culture. I feel like Isengard for example should fight in more formations. Advance as a formation, get close and then attack, etc. Same with dwarves. That is where their strength is formations and coordinated attacks. If possible"

Mike delegates Fords of War balance and rosters to the session, so the per-faction values are the session's choice, not his.

## The change (this worktree, branch `feat/fords-culture-tactics`, base `6791dbc75`, uncommitted; `git diff` and `git status` show all of it)

- **Data:** ten factions in `Main/_Module/ModuleData/fords_of_war/factions.json` get a `tactics` block: `march` and `close` name an arrangement, `closeRange` and `chargeRange` are metres. `FordsTacticsReader` reads it into `LaneTactic` (`Domain/LaneTactic.cs`, `Domain/LaneArrangement.cs`).
- **Rules:** `LaneMarchRules.Evaluate` charges when the nearest enemy is within `LaneTactic.ChargeAt(engageRange)`. Between that and `CloseAt(engageRange)` a new branch `LaneBranch.Close` moves the lane to the lane path vertex nearest the enemy, returns nothing when that vertex is past the next enemy tower along the lane, and keeps the hold point under the Hold order. The decision carries `Arrangement`: the close arrangement on Close, the march arrangement on every other branch, null when the faction names none.
- **Match:** `FordsMatch` builds the rules with each side's tactic and passes the nearest enemy's position (`NearestEnemyFrom` gained an `out` parameter).
- **Engine:** `FordsMissionDirector.ApplyArrangement` sends the arrangement through `IFordsBattlefieldAdapter.SetArrangement` (`Formation.SetArrangementOrder`) before the movement order, only when it changed, and retries when the adapter returns false.

## Questions (answer each with file:line evidence; missing engine evidence is UNVERIFIED)

1. **The engine call.** Read `Formation.SetArrangementOrder`, `ArrangementOrder.OnApply` and `OnCancel`. On a lane formation under `NoTeamAI` with mixed classes (infantry, archers, riders, creatures), while a Move, Charge or AttackEntity order is active: can the call throw, detach units, break an AttackEntity detachment, or leave a unit with an enforced shield usage after the arrangement changes back to Line? Do units that join the formation later (the adapter adds fresh spawns) get the arrangement's shield usage?
2. **Order of calls.** The director sends the arrangement, then the movement order. On Close to Charge it sends Line (or Loose), then Charge. Is that order right, or does `SetMovementOrder` undo or depend on what `SetArrangementOrder` did?
3. **The cache.** `_arranged` in the director is never cleared. Does the adapter ever replace a lane's `Formation` object, or does the engine change a formation's arrangement by itself (for example `Formation.Reset`, or `_overridenHasAnyMountedUnit` making it Skein), so that the cache no longer matches the engine?
4. **The default.** The doc says a faction without tactics keeps the engine's default, Line (`Formation.Reset`). Is that true for every lane formation the adapter makes, mounted ones included?
5. **The close target.** Check `LaneMarchRules.Close` for both sides: the past-tower test with `LanePath.ArcPosition` (arc from the West end), the keep as the next target (the keep is not on any lane path), an enemy from another lane at the keep (`anyLane`), and the Hold case.
6. **Readers of the decision.** Does anything else read `LaneBranch`, `LaneDecision`, `FormationCommand` or the lane command kinds and now count Close wrongly: the field probe and check 9 (`bypass`), the other run verdicts, `FordsRunReport.LaneCommand`, the AI commander, the HUD?
7. **The doc's engine claims.** `docs/features/fords-of-war.md` "Culture tactics" says a shield wall holds soldiers to 0.3 of run speed, and that it makes the front rank hold shields to the front and the flank files to the side. Confirm or correct each from the engine source.
8. **Anything else** in the diff that is wrong: the reader (`Enum.TryParse`, ranges), the tests, the data, the balance log.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question above.
