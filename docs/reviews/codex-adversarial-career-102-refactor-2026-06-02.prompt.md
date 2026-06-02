# Codex Adversarial Review — Issue #102 CareerPerkMissionBehavior Decomposition

## Context

TAOM is a Mount & Blade II: Bannerlord v1.4.5 total-conversion mod (.NET Framework 4.7.2). This change closes GitHub issue #102: the legacy `CareerPerkMissionBehavior.cs` was 302 lines (>2× the ADR-002 thin-entry-point ceiling of 150 lines) with four inline state machines (per-frame ability tick, V-key + ready-state notification + charging-throttle, GauntletUI HUD lifecycle, per-activation effect dispatch). None were unit-testable because every dependency was a sealed TaleWorlds static (`Mission.Current`, `Campaign.Current`, `CharacterObject.PlayerCharacter`, `ScreenManager.TopScreen`, `Input`, `InformationManager`).

The refactor extracts three controllers + two adapters and reduces the behavior to a 126-line thin delegator.

## Diff under review

Three modified files + 11 new files (see below). Vanilla DLL targets unchanged.

### Modified

- `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` — 302 → 126 lines, thin delegator only
- `Main/Features/CareerSystem/CareerSystemIoC.cs` — +5 Singleton registrations
- `Main/SubModule.cs` — 8-arg constructor → 6-arg constructor at line 683

### New

- `Main/Features/CareerSystem/Abilities/IAbilityInputAdapter.cs` — `bool IsActivationKeyPressed()`
- `Main/Features/CareerSystem/Abilities/AbilityInputAdapter.cs` — wraps `Input.IsKeyPressed(InputKey.V)`
- `Main/Features/CareerSystem/Abilities/IMissionTimeProvider.cs` — `float CurrentTime { get; }`
- `Main/Features/CareerSystem/Abilities/MissionTimeProvider.cs` — wraps `Mission.Current?.CurrentTime ?? 0f`
- `Main/Features/CareerSystem/Abilities/IAbilityActivationController.cs` — `AbilityActivationOutcome Tick(float dt, string heroStringId, bool hasCareer); void Reset()` + enum `{None, JustBecameReady, Activated, Charging}`
- `Main/Features/CareerSystem/Abilities/AbilityActivationController.cs` — pure state machine, 75 lines
- `Main/Features/CareerSystem/UI/IAbilityHudController.cs` — `TryInitialize() / Refresh(string) / Cleanup()`
- `Main/Features/CareerSystem/UI/AbilityHudController.cs` — owns GauntletLayer + ScreenManager attach, 115 lines
- `Main/Features/CareerSystem/Abilities/IAbilityEffectExecutor.cs` — `Execute(string heroStringId, Action<MissionAbilityExecutionContext> registerContext)`
- `Main/Features/CareerSystem/Abilities/AbilityEffectExecutor.cs` — per-activation pipeline (template mutation + executor dispatch + toast), 90 lines
- `TAOM.Tests/Features/CareerSystem/AbilityActivationControllerTests.cs` — 12 unit tests

## Mandatory verification surface

1. **Vanilla decompilation.** Confirm against installed `TaleWorlds.MountAndBlade.dll` / `TaleWorlds.Core.dll` / `TaleWorlds.InputSystem.dll` / `TaleWorlds.Engine.GauntletUI.dll`:
   - `MissionBehavior.OnMissionTick(float dt)` is invoked once per frame from `Mission.Tick` and `dt` is the wall-clock delta. ✓ if true; flag if there's now a fixed-step variant in 1.4.5.
   - `Mission.Current.CurrentTime` is the mission-relative clock (resets per mission). Flag if it ever wraps or jumps in 1.4.5.
   - `Input.IsKeyPressed(InputKey.V)` returns `true` only on the edge frame the key was pressed (NOT held). The state machine assumes edge-trigger semantics — if 1.4.5 changed this to held-key reporting, the activation+charging logic breaks.
   - `ScreenManager.TopScreen` semantics: when does this become non-null for a battle mission? `AbilityHudController.TryInitialize()` is idempotent and called every tick — confirm it's safe to keep calling `topScreen.AddLayer(_hudLayer)` if `_hudInitialized` is already true (it shouldn't, but verify the guard).
   - `GauntletLayer` ctor + `LoadMovie` + `topScreen.AddLayer` + the symmetric `RemoveLayer` + `ReleaseMovie` + `OnFinalize` sequence is the v1.4.5-correct teardown order. Codex Review #31 (CareerScreen) caught a similar finalize-ordering bug — re-verify here.

2. **Adapter pattern compliance.** Per ADR-007, services may never accept sealed TaleWorlds types. The new controllers/adapters are tight. Audit:
   - `AbilityActivationController` — confirm it touches NO `Mission`/`Campaign`/`CharacterObject`/`Input` statics directly (it should only see `ICareerAbilityService` + `IAbilityInputAdapter` + `IMissionTimeProvider`).
   - `AbilityEffectExecutor` — boundary-allowed reaches: `Mission.Current?.MainAgent`, `Campaign.Current`, `CharacterObject.PlayerCharacter`, `InformationManager.DisplayMessage`. Flag any deeper drift.
   - `AbilityHudController` — boundary-allowed reaches: `ScreenManager.TopScreen`, `GauntletLayer`, `TextObject`. Flag any deeper drift.

3. **Thin entry-point compliance.** Per ADR-002, `CareerPerkMissionBehavior` overrides must be thin delegators. Review the new `OnMissionTick` switch (lines 67-83) — is the per-outcome branching too much logic for an entry point, or is the translation from `AbilityActivationOutcome` to `InformationManager.DisplayMessage` the legitimate "convert at boundary" responsibility? The plan accepts this as boundary code. Push back if you disagree.

4. **State machine correctness.** The activation controller has two flags: `_abilityReadyNotified` (re-armed on each `Activated`) and `_lastChargingMessageTime = -2f` sentinel for first-throttle-eligible. Confirm:
   - Reset path on `OnEndMission` clears both flags before the next mission.
   - Same mission, ability re-armed after activation, then V pressed while still-charging: expected — `Charging` if 2s throttle window elapsed since the *last* Charging emission, else `None`. Verify the test `Tick_VPressedTwiceWithinThrottle_SecondReturnsNone` actually exercises this. (Note: throttle resets on Reset(), so a mission restart shows the first Charging immediately even at `CurrentTime=0`.)
   - There's no path where `JustBecameReady` is emitted multiple times for the same cooldown completion (the `_abilityReadyNotified` flag must hold across frames until re-armed).
   - **The Tick CALLS `_abilityService.Tick(heroStringId, dt)` BEFORE checking `IsAbilityReady` — so if a tick crosses the cooldown boundary, the ready check sees the post-tick state. Confirm this is correct (it is, per the legacy behavior).**

5. **HUD per-mission cache invalidation.** `AbilityHudController` caches `_cachedHudHeroId/Name/Sprite` and only refreshes when `heroStringId` changes (Codex Review #31 perf fix). If career changes WITHOUT hero changing (player picks a new career in the same battle — implausible but possible via dev console), the cache stays stale. Flag if this is a real risk; otherwise accept as bounded.

6. **`Cleanup()` ordering.** `AbilityHudController.Cleanup` calls `RemoveLayer` BEFORE `ReleaseMovie`. Confirm against vanilla — Codex #31 found that `OnFinalize` must come after the layer is detached, and `ReleaseMovie` after the layer is detached too. The current order is `RemoveLayer` → `ReleaseMovie` → `_hudVM?.OnFinalize`. Validate.

7. **DI lifetime correctness.** All five new services are registered `Reuse.Singleton`. The activation controller HAS per-mission state (`_abilityReadyNotified`, `_lastChargingMessageTime`). The behavior calls `_activationController.Reset()` in `OnEndMission`. **Question: if two missions ever overlap (start of next mission before `OnEndMission` fires on the previous), does the singleton state carry over?** Mission lifecycle in 1.4.5: confirm `OnEndMission` ALWAYS fires before the next mission's `OnMissionTick` begins. Same question for `AbilityHudController` — its `_hudLayer / _hudVM / _hudMovie / _hudInitialized` fields are also singleton state. If `Cleanup()` ever throws or is skipped, the next mission starts with stale GauntletLayer references.
   - **Alternative consideration:** would Transient lifetime + per-mission resolution be cleaner here? The behavior is per-mission, so resolving its dependencies fresh per mission would auto-reset state. Argue your preferred approach.

8. **`MissionAbilityExecutionContext` allocation in executor.** `AbilityEffectExecutor.Execute` creates `new MissionAbilityExecutionContext(..., Mission.Current, _logger)` and hands it to the registerContext callback (which the behavior wires to `_activeContexts.Add`). Confirm:
   - The context is added to `_activeContexts` BEFORE `executor.Execute(context)` runs. If the executor.Execute throws, is the partially-initialized context still in the list? — Look at the ordering.
   - `_activeContexts` is cleared on `OnEndMission` and on `affectedAgent == mainAgent` in `OnAgentRemoved`. Good. Is there a missing clear path? (E.g., player switches careers mid-mission, retire, etc.)

9. **`OnAgentDeleted` does NOT clear the context for the deleted agent.** Only `CareerAbilityBuffTracker.ClearAllyBuff(agent.Index)`. Is this correct? — confirm the contexts in `_activeContexts` are owned by the PLAYER (mainAgent), not allies. If an ally dies mid-Activated, no context cleanup needed. ✓ if true.

10. **Logging discipline.** Per CLAUDE.md "C++ Native Hook Standards" — that's for C++. This is C#. Still, the controllers + behavior emit InfoLog on every mission start ("CareerSystem: Mission started — ...") and every Activated. Confirm these are not per-frame; they should be edge-triggered. Verify by reading the call sites.

## Known Suspects (TAOM-historic class)

Apply these from `docs/reviews/REVIEW-GUIDE.md`:

- **Float NaN propagation through Clamp** (`simplicity-criterion.md`, `feedback_clamp_nan_infinity_propagates.md`). The state machine has `now - _lastChargingMessageTime >= ChargingMessageThrottleSeconds`. If `Mission.Current.CurrentTime` ever returns NaN (unlikely but check 1.4.5 cctor behavior), the comparison is `false` and Charging is never re-emitted. Acceptable degradation if true.
- **Reflection alloc per call** (`feedback_hotpath_private_method_open_delegate.md`). The new controllers use NO reflection. ✓
- **MovementOrder cctor crash in OnSubModuleLoad** (`feedback_movementorder_cctor_mission_current.md`). The behavior is per-mission, not subscribed in OnSubModuleLoad. ✓ Confirm.
- **Cross-feature handshake** (`feedback_cross_feature_handshake_via_shared_adapter.md`). No shared adapter touched. ✓
- **Editor-fields-are-config** (`feedback_editor_fields_are_config.md`). No new editor fields. ✓
- **Worktree base stale** (`feedback_worktree_base_stale_in_parallel_agents.md`). Single-session, no parallel agents. ✓

## What you should NOT do

- Do not refactor for style. ADR-002 ceiling met (126 lines), and the controllers are TDD-mandated.
- Do not propose adding `IAbilityHudController` unit tests. It's a boundary class (per ADR-008 "Entry Points: Not required") and verifies in-battle only.
- Do not suggest replacing `Reuse.Singleton` with `Reuse.Transient` without strong evidence — the singleton + explicit Reset() pattern is established for other CareerSystem services.

## Output

Per `docs/reviews/REVIEW-GUIDE.md` Phase 3 format: emit a numbered findings list (HIGH/MED/LOW), each with file:line, evidence (vanilla decompile excerpt or test trace), and a concrete fix recommendation. Then a Phase 3e RCA section: for each confirmed finding, "why did 12 controller tests + Claude's deep-review pass not catch this?"
