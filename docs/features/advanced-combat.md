# Advanced Combat

## Overview
Advanced Combat provides the infrastructure for custom melee collision and hit registration in TAOM battles. It introduces a spatial partitioning grid for fast agent proximity lookups and a bone-level collision system that lets custom attacks (such as Warg bites) detect hits against specific skeleton bones and trigger damage callbacks outside the normal Bannerlord weapon-swing pipeline.

## Why This Exists
- **Vanilla behavior:** Bannerlord registers hits through the `Mission.RegisterBlow` pipeline triggered by weapon collisions. Non-humanoid agents (e.g., Wargs) and special abilities have no path to register custom blows without going through that sealed system.
- **TAOM requirement:** Warg bite attacks and any future ability-driven attacks need to check whether a specific animated bone on the attacker intersects bones on nearby targets, then inject a `Blow` into the engine with correct damage and knockdown flags.
- **Without this feature:** Custom attacks cannot land — the Warg combat system has no collision detection and `WargAttackService` cannot register damage.

## Architecture

### Design Challenge
Two problems arise simultaneously:
1. `Mission` and `Agent` are sealed TaleWorlds types. Their internal `RegisterBlow` method is non-public, requiring reflection to obtain a delegate at startup.
2. Iterating `Mission.AllAgents` every tick for bone proximity checks against all targets is O(n²). At large battle sizes this is prohibitive.

### Solution Approach
- `SpatialGrid` divides the map into 20-unit cells. `AdvancedCombatBehavior.OnMissionTick` rebuilds the grid every 2 seconds from `Mission.AllAgents`, keeping spatial lookups to O(agents-in-nearby-cells). Since #595 the rebuild replaces the map (never cleared in place under a reader), `OnAgentDeleted` evicts the deleted agent, and both the rebuild and the query carry the `MissionThreadGuard` tripwire: every reader is on the mission tick, and the third player freeze of 2026-09-13 had only warg trees reading this grid from the asynchronous tick while it was rebuilt.
- `BoneCheck` and `BoneCheckDuringAnimation` hold references to attacker and target lists (via `IAgentAdapter`), fetch skeleton transforms each tick, and compare world-space bone positions against a configurable radius.
- `CustomAttacksUtils` caches the `Mission.RegisterBlow` delegate at static construction. It also exposes `TakeDamage` which builds a full `Blow`/`AttackCollisionData`/`CombatLogData` struct and feeds it to the cached delegate. **The damage bypasses armor:** it sets `blow.InflictedDamage = damage` and `DamageCalculated = true`, so the engine applies the number as given (measured 2026-09-18: 976 war ram head-butts on Armored Trolls averaged 23.1, exactly the raw 18-28 roll). Every caller's damage band (elephant/mumakil trample, war ram head-butt, warg and spider bites, signature strikes) is therefore a post-armor number.
- `AdvancedCombatBehavior` is a `MissionLogic` that owns the `BoneCollisionService`. External code (e.g., `WargMissionBehavior`) calls `AddBoneCheckComponent` to register an active check.

### Component Diagram
```
AdvancedCombatBehavior (MissionLogic)
  |-- OnMissionTick(dt)
  |     |-- SpatialGrid.Instance.UpdateGrid(Mission.AllAgents)   [every 2s]
  |     |-- ISpatialGridDebugService.RenderDebugVisualization()
  |     `-- IBoneCollisionService.TickBoneChecks(dt)
  |           `-- BoneCheck / BoneCheckDuringAnimation.Tick(dt)
  |                 `-- CheckBoneCollision(visuals, skeleton)   [skeleton fetched once per tick]
  |                       `-- CheckTargets(...)                 [range gate before each target's skeleton]
  |                             `-- _onCollisionCallback(attacker, target, boneId)
  |                             `-- CustomAttacksUtils.TakeDamage(...)
  |                                   `-- cached Mission.RegisterBlow(...)
  |
  `-- AddBoneCheckComponent(BoneCheck)   <-- called by WargAttackService

SpatialGrid (singleton)
  `-- GetAgentsInRadius(center, radius)  <-- called from attack services
```

## Configuration
None. Grid cell size is a hardcoded constant (`CellSize = 20f`) in `SpatialGrid.cs`. Grid update interval is hardcoded (`GridUpdateInterval = 2f` seconds) in `AdvancedCombatBehavior.cs`.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` | `MissionLogic` entry point; owns tick loop and grid rebuild |
| `Main/Features/AdvancedCombat/SpatialGrid.cs` | Cell grid keyed on (x, y) for fast radius queries (the distance test stays 3D); singleton pattern |
| `Main/Features/AdvancedCombat/BoneCheck.cs` | Time-limited bone collision check; fires callback on hit |
| `Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs` | Subclass of `BoneCheck`; active only during a specific animation window |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | Reflection-cached `RegisterBlow` delegate; `TakeDamage` utility |
| `Main/Features/AdvancedCombat/BlowDirection.cs` | Enum for front/back/left/right hit direction, used by `GetDirectionOfBlow` |
| `Main/Features/AdvancedCombat/HumanAnimationConstants.cs` | Animation index constants shared by attack implementations |
| `Main/Features/AdvancedCombat/Services/IBoneCollisionService.cs` | Service interface: create/add/tick/clear bone checks |
| `Main/Features/AdvancedCombat/Services/BoneCollisionService.cs` | Manages the list of active `BoneCheck` instances; ticks them in reverse order to allow safe removal |
| `Main/Features/AdvancedCombat/Services/ISpatialGridDebugService.cs` | Interface for debug visualization (stub-able in tests) |
| `Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs` | Renders debug overlay in development builds |
| `Main/Features/AdvancedCombat/AdvancedCombatIoC.cs` | Registers `IBoneCollisionService` and `ISpatialGridDebugService` as singletons |
| `Main/Features/AdvancedCombat/BaseBehaviorTree/` | Shared BT decorators and tasks used by Warg and future AI |

## Dependencies
- `IAgentAdapter` — wraps sealed `Agent`; used in all bone check APIs
- `IAgentVisualsAdapter` — wraps `AgentVisuals`; used to retrieve `Skeleton` and `MatrixFrame`
- `IModLogger` — used inside `BoneCheck` for invalid-bone and null-agent warnings
- `IBoneCollisionService` — resolved via `IoC` in `AdvancedCombatBehavior`
- `ISpatialGridDebugService` — resolved via `IoC` in `AdvancedCombatBehavior`

## Tests
`TAOM.Tests/Features/AdvancedCombat/BoneCollisionServiceTests.cs` — 11 tests covering `IBoneCollisionService.CreateAnimationBoneCheck` / `CreateTimedBoneCheck` and the bone-tracking lifecycle via `IAgentAdapter` + `IAgentVisualsAdapter` substitutes.

`SpatialGridQueryTests.cs` (9) runs the grid query through its generic helpers against a brute-force sphere scan, and pins column order and a point that moved since the rebuild. `BoneCheckRangeGateTests.cs` (10) drives `BoneCheck.CheckTargets`: the range gate, a NaN frame, and every per-target skip, singly and in a mixed list.

**Coverage gaps (tracked elsewhere):**
- `CustomAttacksUtils` needs a live engine for most paths. `SpatialGrid`'s query logic is covered by `SpatialGridQueryTests.cs` through its generic helpers; its `Agent`-typed wrappers are not.
- `SpatialGridDebugService.RenderDebugVisualization` is untested (audit issue #185).
- `BoneCheck`'s bone math uses live `Skeleton` matrices and is not unit-testable; its per-target range gate is (`BoneCheckRangeGateTests.cs`).

## How to Add a New Bone-Based Attack
1. Obtain an `IAgentAdapter` for the attacker and a `List<IAgentAdapter>` for targets (use `SpatialGrid.Instance.GetAgentsInRadius` to find nearby agents).
2. Determine which `sbyte` bone indices on the attacker to track (see `HumanAnimationConstants` or inspect the monster skeleton).
3. Call `IBoneCollisionService.CreateAnimationBoneCheck(...)` or `CreateTimedBoneCheck(...)` with an `onCollisionCallback` that calls `CustomAttacksUtils.TakeDamage(target, attacker, damage)`.
4. Pass the returned `BoneCheck` to `AdvancedCombatBehavior.AddBoneCheckComponent(check)` — the behavior is accessible via `Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>()`.
5. The check runs automatically each tick until it expires or all targets are hit.

## Changelog
- 2026-09-24 (plan 015): `SpatialGrid` keys cells on (x, y) behind the generic `BuildCells` / `CollectInRadius` helpers; `BoneCheck` fetches the attacker's skeleton once per tick, reuses its bone list and range-gates each target before fetching its skeleton (a positive requirement, so a NaN frame fails it). Review: `../reviews/deep-review-015-warg-tick-costs-2026-09-24.md`.
- 2026-05-13 — Added `SpatialGridDebugServiceTests.cs` (2 minimum-coverage tests) for `#185`, and updated this doc's Tests section to reflect `BoneCollisionServiceTests.cs` (`#198`).
- 2026-04-06 — Decoupled the bone-check tick from the 2-second spatial-grid update throttle.

## GitHub Issue
- **Issue:** Unknown
- **Status:** Unknown

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
