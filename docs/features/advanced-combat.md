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
- `SpatialGrid` divides the map into 20-unit cells. `AdvancedCombatBehavior.OnMissionTick` asks the grid to rebuild every 2 seconds from `Mission.AllAgents`, keeping spatial lookups to O(agents-in-nearby-cells). A build nobody queried turns the next scheduled rebuild into a skip, and the next query rebuilds first, so a battle without wargs or spiders stops paying for it. The rebuild fills the spare of two maps and publishes it by one reference write (never cleared in place under a reader, #595), reusing the cell lists; `OnAgentDeleted` evicts the deleted agent from its one cell in each of the two maps through a per-map agent-to-cell index (the spare map too: no rebuild reaches it while nothing queries the grid, and a deleted agent keeps its `Mission` reference, so a handle left there would hold the finished mission until the next mission, a town visit included, replaced the grid; the base held no deleted agent. The surviving agents of the last build still reach that mission through `Agent.Team` until then, a pre-existing retention this does not change, see the 2026-10-04 changelog entry), and both the rebuild and the query carry the `MissionThreadGuard` tripwire: every reader is on the mission tick, and the third player freeze of 2026-09-13 had only warg trees reading this grid from the asynchronous tick while it was rebuilt.
- `BoneCheck` and `BoneCheckDuringAnimation` hold references to attacker and target lists (via `IAgentAdapter`), fetch skeleton transforms each tick (`BoneCheckDuringAnimation` only inside the action's hit window), and compare world-space bone positions against a configurable radius.
- `CustomAttacksUtils` caches the `Mission.RegisterBlow` delegate at static construction. It also exposes `TakeDamage` which builds a full `Blow`/`AttackCollisionData`/`CombatLogData` struct and feeds it to the cached delegate. **The damage bypasses armor:** it sets `blow.InflictedDamage = damage` and `DamageCalculated = true`, so the engine applies the number as given (measured 2026-09-18: 976 war ram head-butts on Armored Trolls averaged 23.1, exactly the raw 18-28 roll). Every caller's damage band (elephant/mumakil trample, war ram head-butt, warg and spider bites, signature strikes) is therefore a post-armor number.
- `AdvancedCombatBehavior` is a `MissionLogic` that owns the `BoneCollisionService`. External code (e.g., `WargMissionBehavior`) calls `AddBoneCheckComponent` to register an active check.

### Component Diagram
```
AdvancedCombatBehavior (MissionLogic)
  |-- OnMissionTick(dt)
  |     |-- SpatialGrid.Instance.UpdateGrid(Mission.AllAgents)   [every 2s, skipped while unread]
  |     |-- ISpatialGridDebugService.RenderDebugVisualization()
  |     `-- IBoneCollisionService.TickBoneChecks(dt)
  |           `-- BoneCheck / BoneCheckDuringAnimation.Tick(dt)
  |                 `-- CheckBoneCollision(visuals, skeleton)   [skeleton fetched once per tick]
  |                       `-- CheckTargets(...)                 [range gate before each target's skeleton]
  |                             `-- _onCollisionCallback(attacker, target, boneId)
  |                                   `-- CustomAttacksUtils.TakeDamage(...)
  |                                         `-- cached Mission.RegisterBlow(...)
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

`SpatialGridQueryTests.cs` (9) runs the grid query through its generic helpers against a brute-force sphere scan, and pins column order and a point that moved since the rebuild. `SpatialGridReuseTests.cs` (5) pins the reused build against the cells the positions name (worked out in the test, not by the helper under test) and the O(cell) removal against the old walk, and two maps built in turn over one spare stack (a build never touches or shares a list with the published map), and `SpatialGridDormancyTests.cs` (10) pins when a rebuild is skipped and when a query rebuilds first (build counts), the two resets in `Rebuild` among them (a rebuilt grid is unread again; a woken grid does not rebuild on its next query), and both log lines literally. `SpatialGridWiringTests.cs` (8) drives the instance on bare agents through the grid's position and liveness seam: the first query after skipped rebuilds answers from a fresh build for an agent that spawned, moved into range or died during the idle spell; a query with no skipped rebuild answers from the last scheduled build; a removal drops the agent from the next answer and from both map generations, on the mission tick only, after each of several alternating builds. `BoneCheckRangeGateTests.cs` (10) drives `BoneCheck.CheckTargets`: the range gate, a NaN frame, and every per-target skip, singly and in a mixed list. `BoneCheckDuringAnimationTickTests.cs` (7) drives `BoneCheckDuringAnimation.Tick` with substitutes and `default(ActionIndexCache)`: a wind-up frame never touches the attacker's visuals, the window end and a missing skeleton or visuals in the window expire the check, and a NaN progress keeps it without a hit test. One IL rule, with a control fixture, pins a single progress read per tick.

**Coverage gaps (tracked elsewhere):**
- `CustomAttacksUtils` needs a live engine for most paths. `SpatialGrid`'s query logic is covered by `SpatialGridQueryTests.cs` through its generic helpers; its `UpdateGrid`, buffered `GetAgentsInRadius`, `Remove` and `ApplyPendingRemovals` run in `SpatialGridWiringTests.cs` on bare agents. Unrun: the seam's two native reads (`Agent.Position`, `IsActive()`), every `GetNearAliveAgentsInRange` overload (the entry point of each production scan; the two that take an `Agent` also read `target.Position` outside the seam, so they cannot run on a bare agent) and the allocating `GetAgentsInRadius(Vec3, float)`; each only forwards to the buffered query.
- `SpatialGridDebugService.RenderDebugVisualization` has two IL rules (it scans into a reused buffer and empties it afterwards, so the singleton keeps no finished mission's agents); its rendering is untested (audit issue #185).
- `BoneCheck`'s bone math uses live `Skeleton` matrices and is not unit-testable; its per-target range gate is (`BoneCheckRangeGateTests.cs`).
- `BoneCheckDuringAnimation.Tick`'s hit-window path with a live skeleton needs a real `Skeleton`, which a test cannot build; the owed in-game warg Custom Battle is the proof that bites still land and end as before (#659). The other paths are driven with substitutes: `ActionIndexCache` is beforefieldinit and its `!=` reads only `Index`, so `default(ActionIndexCache)` never runs its engine-backed static constructor.

## How to Add a New Bone-Based Attack
1. Obtain an `IAgentAdapter` for the attacker and a `List<IAgentAdapter>` for targets (use `SpatialGrid.Instance.GetAgentsInRadius` to find nearby agents).
2. Determine which `sbyte` bone indices on the attacker to track (see `HumanAnimationConstants` or inspect the monster skeleton).
3. Call `IBoneCollisionService.CreateAnimationBoneCheck(...)` or `CreateTimedBoneCheck(...)` with an `onCollisionCallback` that calls `CustomAttacksUtils.TakeDamage(target, attacker, damage)`.
4. Pass the returned `BoneCheck` to `AdvancedCombatBehavior.AddBoneCheckComponent(check)` — the behavior is accessible via `Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>()`.
5. The check runs automatically each tick until it expires or all targets are hit.

## Log lines

`SpatialGrid` writes two INFO lines to `taom_debug.log`, each at most once per grid; `AdvancedCombatBehavior` makes a new grid for every mission, so that is once per mission (plan 033). `SpatialGridDormancyTests` pins both formats literally.

| When | Line | Fields |
|---|---|---|
| The first scheduled rebuild skipped because nothing queried the last build | `[SpatialGrid] Scheduled rebuild skipped: nothing queried build 1, so rebuilds pause while nothing reads the grid and the next query rebuilds first. Logged once per grid (one grid per mission).` | the number of the unread build |
| The first query that rebuilds the grid after skipped rebuilds | `[SpatialGrid] A query found 2 scheduled rebuilds skipped and rebuilt the grid before answering (build 2). Logged once per grid (one grid per mission).` | how many scheduled rebuilds were skipped; the number of the build the query made |

The grid writes no mission-end summary: it has no mission-end hook of its own, and the behaviour that owns its lifetime was outside plan 033's scope.

## Changelog
- 2026-10-04 (plan 033, Codex review of the final tip, #715): a removal now also drops the agent from the spare map and its index, so the grid no longer holds a deleted agent's handle while it idles (the base held none). That does not release the finished mission: the grid's surviving agents still reach it through `Agent.Team` until the next mission's `AdvancedCombatBehavior` replaces the grid, the open R18 follow-up in `../reviews/deep-review-033-creature-battle-allocations-2026-10-02.md`. `SpatialGrid` gains `PositionOf` and `IsLive` internal seams (the engine reads by default) and a test inspector `HeldAgents()`, so `SpatialGridWiringTests` runs the instance on bare agents; the reuse tests compare against cells computed in the test.
- 2026-10-02 (plan 033): `SpatialGrid` reuses its two maps and cell lists, removes a deleted agent from its one cell, and skips a scheduled rebuild nobody queried (the next query rebuilds first); the debug overlay scans into a reused buffer and empties it after each frame. The first skip and the first query rebuild per grid each write one INFO line.
- 2026-09-24 (#659, maintainer decision on the plan 015 review): `BoneCheckDuringAnimation.Tick` tests the action and the progress upper bound first, reads the progress once into a local, and fetches the attacker's skeleton only once the progress reaches the hit window; a null skeleton there ends the check. The one behaviour difference: a missing attacker skeleton no longer ends the bite during the wind-up; it ends it only if still missing at the first in-window tick, and one back by then lets the bite go on. Proof owed: the in-game warg Custom Battle (bites must still land and end as before).
- 2026-09-24 (plan 015, #659): `SpatialGrid` keys cells on (x, y) behind the generic `BuildCells` / `CollectInRadius` helpers; `BoneCheck` fetches the attacker's skeleton once per tick, reuses its bone list and range-gates each target before fetching its skeleton (a positive requirement, so a NaN frame fails it). Review: `../reviews/deep-review-015-warg-tick-costs-2026-09-24.md`.
- 2026-05-13 — Added `SpatialGridDebugServiceTests.cs` (2 minimum-coverage tests) for `#185`, and updated this doc's Tests section to reflect `BoneCollisionServiceTests.cs` (`#198`).
- 2026-04-06 — Decoupled the bone-check tick from the 2-second spatial-grid update throttle.

## GitHub Issue
- **Issue:** Unknown for the original feature; the plan 015 tick-cost work is #659, [Warg battles: cut per-tick service lookups, scan allocations and skeleton wrappers](https://github.com/haterade22/TAOM/issues/659)
- **Status:** #659 open (in-game warg Custom Battle owed)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
