# Codex pre-review: The Long Defeat, destroyed siege engines come back for the next wave (#1160)

**Role:** reviewer (`.ai/roles/reviewer.md`). Read only: edit no file, start no game. Run no `dotnet build` or `dotnet test` in this checkout: another process builds and tests it now, and a second build would collide on `bin/` and `obj/`. Report findings; fix nothing.

**Scope:** the uncommitted working-tree change in this checkout (`E:\repos\taom-ld-split`, branch `fix/ld-engine-restore`), base `54819b067`. Read the tracked part with `git diff 54819b067` and the new files from `git status --short` (the `??` rows). The change holds C# in `Main/` (a new adapter partial, a new director partial, a new checks class, edits to the adapter interface, `LdEngineReport.cs`, `RunOptions.cs`, `LongDefeatDirector.Effects.cs`, `LongDefeatAutorunPlan.cs`, `LongDefeatRunReport.cs`), C# tests, the binding and reflection-site gate rows, `tools/run_long_defeat.py` and its tests, a new manifest `tools/ingame_checks/1160.toml`, and docs (`docs/features/long-defeat.md`, `tools/README.md`, `docs/reference/taleworlds-api-snapshot/reflection-sites.md`).

## The request and the one maintainer decision

Players reported that when they destroy the attackers' siege engines (the siege towers and the battering ram), the next wave's attackers do not get new ones. The maintainer filed this as #1160 and asked for the engines to come back on the next wave. Nothing else about the design is a maintainer decision.

## What the change does (the builder's design, open to challenge)

- At each wave start, in `LongDefeatDirector.Apply` when the effects hold a `StartWave`, before the Hammer of the Underworld writes engine hit points, `LongDefeatBattlefieldAdapter.RestoreEngines` takes each ram and tower that the boot deployed (`_towers`, `_rams`) and that reads `IsDestroyed`, and runs, in order: `DestructionComponent.Reset()`, `DestructionComponent.SetAbilityOfFaces(true)`, then the type's declared `OnMissionReset` (protected internal, found with `AccessTools.DeclaredMethod` and invoked by reflection), then re-adds the engine to the static `TeamAISiegeComponent.SiegeLanes` lane whose `LaneSide` equals its `WeaponSide`. It saves the visibility of every `breakable_wall` entity's `destroyed` and `non_destroyed` children before and writes it back after, because the tower's reset shows its target wall as broken.
- Two `[Check]` ids: `taom.long-defeat.engine-restore` at that wave start, and `taom.long-defeat.engine-use` at that wave's clear (or at the run's end inside the wave). Words and thresholds are in `Main/Features/LongDefeat/Diagnostics/LongDefeatEngineChecks.cs`.
- A test option `break_engines` (arm key, `RunOptions.BreakEnginesAtClear`, wrapper flag `--break-engines`) destroys every standing ram and tower at one clear through `DestructableComponent.TriggerOnHit` with an empty weapon and the scene's ram as the attacker, with `DestroyedByStoneOnly` cleared for that hit, because the shop bot never attacks an engine.

## Evidence (read it yourself before you rely on it)

- Engine sources for the installed v1.5.7 DLLs: `C:\Users\mikew\.taom-src\v1.5.7-202611101605\` (for example `TaleWorlds.MountAndBlade.SiegeTower.cs`, `...BatteringRam.cs`, `...UsableMachine.cs`, `...SiegeWeapon.cs`, `...DestructableComponent.cs`, `...SiegeWeaponMovementComponent.cs`, `...SiegeLane.cs`, `...TeamAISiegeComponent.cs`, `...Mission.cs`, `...Formation.cs`). For any other engine type run `pwsh tools/taom-src.ps1 path <Type>`.
- No game run has played this change. No log exists for it.

## Questions to answer (each with file:line evidence, marked observed or inferred)

1. Does the restore undo everything the destroy did? Trace `DestructableComponent.OnHit` to `Mission.OnObjectDisabled`, `UsableMachine.Disable`, `SiegeTower.OnDestroyed` and any `OnDestroyed` handler the ram or `SiegeWeapon` registers. For each state change the destroy makes (active list, tick requirement, standing points, users, detachment, faces, entity states, flags such as `IsDisabled` and `IsDestroyed`, team or AI lists), say whether the restore reverses it, and what an unreversed one does in the next wave.
2. Is calling `OnMissionReset` in the middle of a mission safe on both types? Name every field and object it touches. Does any part assume a mission start (for example a double reset of the destructible, the tower's gate and navmesh faces, its push points, the ram's state, sounds, the movement component's path, a component that is not ticked again)?
3. Is re-adding the engine to `SiegeLane.PrimarySiegeWeapons` enough for the attackers' siege AI to use it again? Name any other list or cache that dropped it (on `TeamAISiegeComponent`, `TeamAISiegeAttacker`, the tactics, `SiegeLane` state such as its lane state or defense points), and who makes the engine's detachment again after `Disable()` destroyed it.
4. Is the order in the director correct: the restore before the Hammer's hit point write, both at the wave start, before the wave's attackers spawn? Can an engine be destroyed at a time that the restore does not see (during an intermission, by the stalemate charge, at the clear itself)? Does `JudgeEngineUse(false)` before `BreakEnginesAt` at a clear give the right verdicts in a run with `break_engines`?
5. Is `Read()` sound right after the restore: `GetTotalDistanceTraveledForPathTracker`, the machine's `HasArrivedAtTarget` (and not the movement component's), `HasCompletedAction()`, `GameEntity.IsVisibleIncludeParents()`, the hit points? Can any of them read a stale value in the same frame?
6. Is the breakable wall save and restore correct for a tower that docked and broke its wall before it fell, and for a tower that never docked? Does a wall entity's visibility alone carry the wall's state, or does a navmesh or another flag also change?
7. Does the `break_engines` path behave like a player's destroy? Compare its `TriggerOnHit` call (null attacker agent, empty weapon, a ram as the attacker behavior) with a real hit: who is credited for agents killed on the tower, does anything read `weapon.CurrentUsageItem` or the attacker agent and throw, and does the cleared `DestroyedByStoneOnly` flag always come back?
8. Do the wrapper and the manifest match the emitter? Check `tools/run_long_defeat.py` (`owes_engine_checks`, `break_engines_problem`, the echo default, the arm pair, the report field) and the regexes in `tools/ingame_checks/1160.toml` against the exact line that `LongDefeatChecks.Record` and the outcome log write.
9. Are the engine claims in the changed comments and docs accurate (the adapter's comments, `docs/features/long-defeat.md` "Engines come back each wave", the Hammer paragraph, the reflection-site rows)?
10. Anything else in the diff that is wrong: a dropped behaviour, a stale comment, a test that does not test what its name says.

## Output

Findings first, ranked by severity (CRITICAL, HIGH, MEDIUM, LOW), each with file:line, the evidence, and observed or inferred. Mark missing engine evidence UNVERIFIED. Then a one-line verdict. No fixes, no patches.
