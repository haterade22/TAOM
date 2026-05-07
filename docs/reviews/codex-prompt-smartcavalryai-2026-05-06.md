# Codex Adversarial Review Prompt — SmartCavalryAI port (2026-05-06)

## Context

Feature #3 of the 7-feature port: SmartCavalryAI was ported from external developer drop into TAOM. Original module was written against Bannerlord v1.4 decompile; TAOM targets v1.3.15 (installed game version). Five Claude /deep-review agents already produced findings; all HIGH and MEDIUM were fixed in the same session. This prompt asks Codex for an INDEPENDENT adversarial pass to catch what those agents missed.

## Files in scope

- `Main/Features/SmartCavalryAI/CavalryChargeService.cs` (state machine)
- `Main/Features/SmartCavalryAI/CavalryPathPlanner.cs` (reroute math)
- `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs` (MCM wrapper)
- `Main/Features/SmartCavalryAI/SmartCavalryRecursionGuard.cs` (thread-local)
- `Main/Features/SmartCavalryAI/Models/*.cs` (state + DTOs)
- `Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs` (per-tick driver)
- `Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs` (Postfix)
- `Main/Adapters/IFormationAdapter.cs` (extended) + `FormationAdapter.cs`
- `Main/Adapters/IBattlefieldQueryAdapter.cs` + `BattlefieldQueryAdapter.cs`
- `Main/Adapters/ICavalryCommandAdapter.cs` + `CavalryCommandAdapter.cs`
- `Main/Adapters/Models/MovementOrderType.cs`, `NearbyAgentSnapshot.cs`
- `TAOM.Tests/Features/SmartCavalryAI/CavalryChargeServiceTests.cs` (50 tests)
- `TAOM.Tests/Features/SmartCavalryAI/CavalryPathPlannerTests.cs` (12 tests)

## Pre-existing /deep-review findings already fixed

The 5 Claude review agents collectively flagged + we fixed:
1. `SmartCavalryRecursionGuard` was bool — converted to depth counter; nested `Enter()` scopes safe.
2. `SmartCavalryRecursionGuard.Reset()` static method added; called from `OnEndMission` to recover from abnormal mission termination.
3. `BattlefieldQueryAdapter._scratchBuffer` was static — converted to per-instance.
4. `SmartCavalryAIMissionBehavior` per-formation adapter cache (was creating two new adapters per cavalry formation per tick).
5. `FormationAdapter.IsAligned` LINQ allocation reduced — single pass + reused static scratch list.
6. `CavalryChargeService.UpdateForming` now checks `IsTargetAlive` before transitioning to Charging (was unconditionally setting Charging even if target had been destroyed).
7. `CavalryChargeService` distance comparisons use `LengthSquared` not `Length` (avoids sqrt) — `UpdateCharging`, `UpdatePassingThrough`.
8. `HandleChargeOrder` short-circuits if formation is already in non-Idle state (idempotency on player double-tap).
9. `HandleChargeOrder` short-circuits if `cav.FormationKey == null` (defensive).
10. `Tick` short-circuits if `cav.FormationKey == null` (defensive).
11. `InitiateLineCharge` now early-returns on zero-length charge direction BEFORE state mutation (matches decompile abort behavior).
12. `Patch31_FormationSetMovementOrder` IoC.Resolve calls cached in static lazy fields; guard order: cheapest-first.
13. New tests: nested-scope guard, Reset behavior, double-tap idempotency, zero-length direction abort, null-FormationKey defense, target-dead UpdateForming bail.

## Adversarial mission — find what the 5 Claude agents MISSED

You're an independent reviewer. The 5 Claude agents focused on standards, compatibility, efficiency, completeness, and data-flow. They are subject to the same biases I am. Hunt for blind spots:

### Specific adversarial angles

1. **Edge cases the test coverage doesn't reach.**
   - What happens if the cavalry formation is destroyed mid-state-machine? `_states` keeps a stale entry. Does it leak across battle boundaries? (`OnMissionEnd` clears, but DURING the battle it could grow.)
   - What happens if `Mission.Current` becomes null during a tick (mission ending mid-tick)?
   - What happens if `targetToken` is the cav formation itself (self-charge)?
   - What happens if friendly path-planner returns the cav's OWN waypoint as a blocker? (self-detection)

2. **Concurrency / re-entrancy.**
   - Patch31 postfix runs synchronously inside vanilla `SetMovementOrder`. The service's `IssueChargeToTarget` calls `Formation.SetMovementOrder` — re-entry. Recursion guard prevents the postfix logic from re-entering, but does it prevent double-applying the vanilla `SetMovementOrder`? (vanilla's behavior is presumably idempotent here — but is that documented anywhere?)
   - Could two cavalry formations call `BattlefieldQueryAdapter.GetNearbyAgents` in the same tick if vanilla TaleWorlds ever ran tick logic across threads? (It doesn't currently, but this is the kind of assumption that breaks.)

3. **State-machine correctness.**
   - The 5-state path Forming→Charging→PassingThrough→Reforming has 4 transitions, each gated by a condition. If any condition is wrong, the formation gets stuck. Walk through with concrete numbers (cav at (0,0), target at (100,0), reform=25, strictness=0.7) and verify each transition fires.
   - The Rerouting branch re-issues the charge order on waypoint arrival. Does the recursion guard prevent the re-issued order's postfix from creating a NEW state entry that overwrites the just-completed Rerouting state?

4. **Settings semantics.**
   - The provider clamps each setting. If MCM were to provide a NaN/Infinity, what happens? `Clamp(NaN, 0, 1)` returns NaN in `Math.Min`/`Math.Max` — could leak through. Compare against the prior `feedback_validate_before_lookup_with_fallback.md` lesson.
   - `SmartCavalryDebug` only gates HUD output. If a future bug surfaces only via HUD, the user can't capture it without enabling. File log via `_logger.LogInfo` is already unconditional in the MissionBehavior init, so per-tick state changes are NOT in the file log. Is this an observability gap?

5. **Cross-feature interactions.**
   - MixedFormations also patches Formation. They share `IFormationAdapter`. When SmartCavalryAI calls `Formation.SetPositioning` from `ApplyChargeLine`, the side effect propagates to the formation's order positions, which Patch30's Prefix on `GetOrderPositionOfUnit` will re-position. Is the cavalry actually getting positioned on the wide line, or is MixedFormations re-shuffling it?
   - The 5 Claude agents only flagged this as MEDIUM and didn't propose a fix. Is the fix actually unsafe (cross-feature dependency)? Is there a non-cross-feature mitigation?

6. **The two "bug fixes vs decompile baseline."**
   - Hardcoded 0.5f reform strictness: fix is to read `ChargeFormationStrictness`. Verify behavior parity by walking the original code at line 517 — was 0.5f intentionally used as a different/lower bar for reform alignment than for charge alignment, and did the user have a different expectation? Is it possible the original developer's intent was "reform should be looser than charge"? If so, our fix changes design intent. The feature doc claims it as a bug; verify.
   - HUD spam removal: same — was the original spam intentional debugging output left in by accident, or was it a feature?

7. **Documentation drift.**
   - `docs/features/smart-cavalry-ai.md` claims things about the implementation. Verify each claim against the actual code AFTER the /deep-review fixes were applied.
   - `CHANGELOG.md` claims things about the port. Verify.

## Severity bar

Report only:
- **CRITICAL**: a bug that crashes, corrupts state, or produces user-visible misbehavior in a normal play session.
- **HIGH**: a bug that misbehaves under adversarial conditions (e.g., the user does something unusual but reasonable).
- **MEDIUM**: an architectural smell, latent risk, or documentation/test gap that will bite a future change.

Skip LOW noise. Skip stylistic preferences. Hunt for what the 5 Claude agents biased themselves out of seeing.

## Output

Per-finding format:
```
SEVERITY (HIGH/MEDIUM/CRITICAL) — Title
Path: <file:line>
What: <one sentence>
Why it matters: <one sentence>
Repro / scenario: <one sentence>
Fix: <one sentence>
Why missed by /deep-review: <one sentence>
```
