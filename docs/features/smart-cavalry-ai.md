# Smart Cavalry AI

## Overview

When the player orders a cavalry formation to Charge or ChargeToTarget, intercept the order and orchestrate a coordinated line-charge state machine: form a wide line → charge → pass through the enemy → reform on the other side. Optionally reroute around friendly infantry on the charge line. Player-team cavalry only.

## Why This Exists

- **Vanilla behavior:** cavalry charges as a clump, stops on first contact with infantry, gets stuck in melee, and tramples friendly units in the way.
- **TAOM requirement:** cavalry should hit-and-run as a clean line, pass through, reform; in mixed-army Middle-earth battles where Rohirrim or Easterlings cavalry support friendly Gondorian/Dol Guldur infantry, riders should route around their allies, not over them.
- **Without this feature:** cavalry feels visually wrong (clumpy, sticky) and tactically useless (stops mid-charge, friendlies get killed by their own riders).

## Architecture

### Design Challenge

Three things make this non-trivial:

1. **Single-source MovementOrder side-channel.** The player issues `Charge` via the Tactics UI, which calls `Formation.SetMovementOrder`. We must intercept *just* the cavalry path and return zero overhead for non-cavalry orders. Postfix-on-everything is fine if it bails fast.
2. **Recursion risk.** The state machine itself responds to a charge order by issuing further `SetMovementOrder` calls (Stop, Move-to-waypoint, ChargeToTarget). Without a re-entry guard, the postfix would loop infinitely.
3. **ADR-007 — no sealed types in services.** The service tracks an "original target formation" across the Rerouting branch. We can't store a `Formation` reference in the service; we use opaque `object` tokens that only the command adapter unwraps.

### Solution Approach

Single Harmony Postfix on `Formation.SetMovementOrder`. The patch attribute lives in the shared `Patch_MissionTime_SetMovementOrder` category — applied once from `SubModule.OnMissionBehaviorInitialize` (behind a static one-shot guard) rather than in `OnSubModuleLoad`. Reason: `MovementOrder.cctor` constructs static instances whose ctor reads `Mission.Current.CurrentTime`; applying the patch any earlier crashes JIT prep with NRE. Postfix bails on:
- Recursion-guard set
- Non-cavalry formation (via `formation.QuerySystem.IsCavalryFormation`)
- Non-Charge / non-ChargeToTarget order
- Non-player-team formation
- Feature disabled in MCM

When the postfix proceeds, it hands control to `ICavalryChargeService.HandleChargeOrder(...)` which decides reroute vs line-charge. The service then drives a state machine via `MissionBehavior.OnMissionTick`:

```
Idle → Forming → Charging → PassingThrough → Reforming → Idle
                                                    ↓
                                  (or) → Rerouting → Idle (re-issues charge if target alive)
```

`SmartCavalryRecursionGuard` (a thread-local flag) is raised inside `CavalryCommandAdapter` around every `SetMovementOrder`/`SetPositioning` call. The Postfix reads the flag and bails.

### Component Diagram

```
       Player charge order  ─────────────────────────►  Formation.SetMovementOrder
                                                                  │
                                                          [HarmonyPostfix]
                                                                  │
                                                Patch31_FormationSetMovementOrder
                                                                  │ (cavalry + player team + Charge/ChargeToTarget?)
                                                                  ▼
                              SmartCavalryAIMissionBehavior  ──►  ICavalryChargeService.HandleChargeOrder
                                                                  │
                                          ┌───────────────────────┴───────────────────────┐
                                          ▼                                               ▼
                              ICavalryPathPlanner.TryGetReroutePoint           InitiateLineCharge (Forming)
                                          │ blocker found                                 │
                                          ▼                                               │ ApplyChargeLine + IssueStop
                              BeginReroute (Rerouting)                                    │
                                  IssueMoveTo(waypoint)                                   │
                                                                                          ▼
                                                                              MissionBehavior OnMissionTick
                                                                                          │
                                                                              ICavalryChargeService.Tick
                                                                                          │
                                                                          (Forming → Charging → PassingThrough → Reforming → Idle)
```

## Configuration

All knobs live in MCM under **Battle Tactics / Smart Cavalry** (GroupOrder=22).

| MCM key | Type | Range / Default | Effect |
|---------|------|-----------------|--------|
| `EnableSmartCavalryAI` | bool | true | Master toggle; off = vanilla behavior. |
| `SmartCavalryAvoidFriendlies` | bool | true | When on, cavalry reroutes around friendly non-cavalry formations on the charge line. |
| `SmartCavalryChargeStrictness` | float [0..1] | 0.7 | Tolerance for the alignment check that gates Forming→Charging AND Reforming→Idle. Higher = wait longer for tighter line. |
| `SmartCavalryReformDistance` | float [10..80]m | 25 | Meters past the target before the cavalry stops and reforms. |
| `SmartCavalryLineSpacing` | float [0.8..3.0] | 1.2 | Multiplier on per-unit spacing during the line-charge formation. |
| `SmartCavalryDebug` | bool | false | Emit `[SmartCavalryAI]` HUD diagnostics on every order interception. File log via `IModLogger` is unconditional. |

### Dead-setting audit (gate per `feedback_user_facing_promise_must_match_code.md`)

All five tuning settings are referenced by the service:

| Setting | Consumer (file:line) |
|---------|---------------------|
| `EnableSmartCavalryAI` | `SmartCavalryAISettingsProvider.cs` → service / postfix / mission behavior |
| `SmartCavalryAvoidFriendlies` | `CavalryChargeService.HandleChargeOrder` (gates the path-planner call) + mission behavior collision avoidance |
| `SmartCavalryChargeStrictness` | `CavalryChargeService.UpdateForming` AND `UpdateReforming` (the second usage is the **bug-fix** vs the v1.4 decompile baseline — see "Inherited bugs fixed" below) |
| `SmartCavalryReformDistance` | `CavalryChargeService.UpdatePassingThrough` |
| `SmartCavalryLineSpacing` | `CavalryChargeService.InitiateLineCharge` (rounded to int and passed to `ICavalryCommandAdapter.ApplyChargeLine`) |
| `SmartCavalryDebug` | `Patch31_FormationSetMovementOrder.Postfix` (HUD log gate) |

No dead promises shipped.

## Inherited bugs fixed during the port

The decompiled v1.4 source has two latent issues that the port did NOT propagate:

1. **Hardcoded `0.5f` reform strictness** (decompiled line 517). `UpdateReformingState` ignored `ChargeFormationStrictness` and always used `0.5f` — a user who set strictness to `0.9` (very tight) would still see reform complete at the looser `0.5` tolerance. Our `CavalryChargeService.UpdateReforming` reads the setting. Regression test: `Tick_ReformingAndAligned_TransitionsToIdle` asserts `cav.IsAligned(0.9f)` is what the service queries.
2. **Per-order HUD spam** (decompiled lines 852–860). The original logged every `SetMovementOrder` invocation to `InformationManager.DisplayMessage` regardless of debug state. Our Postfix gates the HUD message behind `SmartCavalryDebug`. File log via `IModLogger` is unconditional.

## v1.3.15 API drift findings

The decompiled source is v1.4. Three v1.3.15 deltas required deviations from the prompt's blueprint:

| Prompt assumption | v1.3.15 reality | Plan deviation |
|---|---|---|
| `Formation.SetPositioning` is private; reflect via `AccessTools` | **public** in v1.3.15 | Drop reflection. `CavalryCommandAdapter` calls directly. |
| `Agent.SetMovementDirection` is private; reflect via `AccessTools` | **public** in v1.3.15 (`SetMovementDirection(in Vec2)`) | Drop reflection. `MissionBehavior.ApplyCollisionAvoidance` calls directly. |
| Compare `MovementOrder.OrderType` against int 4 (Charge) and 5 (ChargeToTarget) | `MovementOrderEnum.Charge = 2`, `ChargeToTarget = 3`; property is `OrderEnum` (no `OrderType`) | Use enum names everywhere. Verbatim port silently mismatches against v1.3.15 enum values. |
| Read `Formation.MovementOrder` property after `SetMovementOrder` | No public property; use `Formation.GetReadonlyMovementOrderReference()` | `FormationAdapter.CurrentMovementOrderType` and `Patch31` use the readonly-ref accessor. |
| `Formation.IsCavalry` predicate exists | Doesn't exist on `Formation`. v1.3.15 path: `formation.QuerySystem.IsCavalryFormation` | `FormationAdapter.RepresentativeIsCavalry` queries the query-system. |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SmartCavalryAI/CavalryChargeService.cs` | State machine driver (Idle→Forming→Charging→PassingThrough→Reforming, plus Rerouting branch). |
| `Main/Features/SmartCavalryAI/ICavalryChargeService.cs` | Service interface. |
| `Main/Features/SmartCavalryAI/CavalryPathPlanner.cs` | Pure-function reroute math (port of decompiled `CavalryPathPlanner` verbatim — no API drift). |
| `Main/Features/SmartCavalryAI/ICavalryPathPlanner.cs` | Path planner interface. |
| `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs` | `TaomSettings.Instance` wrapper with `??` defaults + clamps. |
| `Main/Features/SmartCavalryAI/SmartCavalryRecursionGuard.cs` | Thread-local flag set by command adapter, read by Postfix. |
| `Main/Features/SmartCavalryAI/SmartCavalryAIIoC.cs` | DryIoc singleton registrations. |
| `Main/Features/SmartCavalryAI/Models/CavalryState.cs` | Enum: Idle/Forming/Charging/PassingThrough/Reforming/Rerouting. |
| `Main/Features/SmartCavalryAI/Models/CavalryFormationState.cs` | Per-formation state DTO. |
| `Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs` | `OnMissionTick` driver + per-mounted-unit collision avoidance + `OnEndMission` cleanup. |
| `Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs` | Postfix, hands control to the service. |
| `Main/Adapters/IFormationAdapter.cs` (extended) | +4 props: `RepresentativeIsCavalry`, `IsMoving`, `CurrentMovementOrderType`, `CurrentPosition`, `IsAligned(strictness)`. |
| `Main/Adapters/ICavalryCommandAdapter.cs` | Cavalry command surface (Issue*, ApplyChargeLine, IsTargetAlive). |
| `Main/Adapters/CavalryCommandAdapter.cs` | Implementation; raises `SmartCavalryRecursionGuard` around every TaleWorlds API call. |
| `Main/Adapters/IBattlefieldQueryAdapter.cs` | Battlefield-scope queries (`GetNearbyAgents`, `GetGroundHeightAtPosition`, friendly-formation enumeration). |
| `Main/Adapters/BattlefieldQueryAdapter.cs` | Implementation. |
| `Main/Adapters/Models/MovementOrderType.cs` | TAOM-owned enum (Charge/ChargeToTarget/Other) — wraps `MovementOrder.MovementOrderEnum` so service stays free of TaleWorlds types. |
| `Main/Adapters/Models/NearbyAgentSnapshot.cs` | Snapshot DTO returned by `IBattlefieldQueryAdapter.GetNearbyAgents`. |

## Dependencies

- `IFormationAdapter` (extended this port; reused by MixedFormations)
- `ICavalryCommandAdapter` (new)
- `IBattlefieldQueryAdapter` (new)
- `IModLogger` (existing)
- `TaomSettings` (existing)

## Tests

`TAOM.Tests/Features/SmartCavalryAI/` — 44 tests across 2 files, MSTest + NSubstitute:

| File | Count | Coverage |
|------|------:|----------|
| `CavalryPathPlannerTests.cs` | 12 | All filter conditions (no formations, all-cavalry, behind/past/off-line, target-too-close, empty formation), reroute production (basic, north/south sidestep direction, multiple-blocker closest-pick, diagonal charge). |
| `CavalryChargeServiceTests.cs` | 32 | All 6 state transitions, all 5 MCM settings (consumed assertions), mission cleanup, recursion guard, idle no-op, non-cavalry no-op, no-player-team no-op, feature-disabled no-op, target-alive vs target-dead Rerouting branches, **Reforming-uses-strictness regression test** for the inherited 0.5f bug. |

Total project: 1518 tests (1515 passing; 3 unrelated FiefManagement failures from parallel work).

## How to add a new state to the machine

1. Add the enum value to `Main/Features/SmartCavalryAI/Models/CavalryState.cs`.
2. Add a case branch in `CavalryChargeService.Tick` (the `switch` over `state.State`).
3. Implement an `Update<NewState>` method that decides when to transition out.
4. Write tests in `CavalryChargeServiceTests.cs` for entry, hold, and exit conditions.
5. Update this doc's "Solution Approach" diagram.

## Performance

The `MissionBehavior` ticks every cavalry formation on the player team each frame. Per formation:
- One `IFormationAdapter` and one `ICavalryCommandAdapter` allocation per tick (cheap; both are thin wrappers).
- One state-dictionary lookup.
- If `AvoidFriendlies` is on, one per-mounted-unit `Mission.GetNearbyAgents` query (3m radius). The mission engine caches its spatial index; cost is bounded by mounted-unit count, not battle size.

The `Patch31` Postfix runs on every `SetMovementOrder` call but bails on non-cavalry / non-charge / suppress-flag in O(1). No per-frame allocation in the postfix.

## Known limitations

- Single-player only. `Mission.PlayerTeam` is null in spectator/custom-battle missions; the postfix and behavior bail out cleanly.
- Recursion guard is a single boolean, not a counter — nested reentry inside a `using SmartCavalryRecursionGuard.Enter()` scope would clear the flag prematurely. Not exercised by current code paths, but worth a follow-up if we ever chain SetPositioning + SetMovementOrder synchronously (current implementation does both as separate top-level calls).
- The path planner's "behind cavalry / past target" filters use signed projections onto the charge direction; very-near-the-target enemies (length < 1m) cause early-exit `false`. Acceptable: the Forming→Charging gate handles these via the alignment check.

## GitHub Issue

- **Issue:** TBD — to be opened with feature port commits.
- **Status:** Pending in-game verification.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
