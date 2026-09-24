# Warg Combat System

## Overview

Wargs are autonomous combat agents with their own AI behavior tree. When mounted by a rider, wargs independently attack nearby enemies and can enter a "rage mode" when damaged, temporarily taking over control from the player or AI rider. The system uses bone-based collision detection and a spatial grid for efficient enemy proximity queries.

## Why This Exists

- **Vanilla behavior:** Mounts are passive — they carry riders but never attack independently
- **TAOM requirement:** Wargs (Gundabad, Dol Guldur, Isengard) are predatory creatures that should actively fight, consistent with Middle-earth lore where wargs are intelligent, aggressive beasts
- **Without this feature:** Warg-mounted troops behave identically to horse cavalry, breaking immersion

## Architecture

### Design Challenge

The `Agent` class is sealed (cannot subclass). Warg AI must run alongside the existing mount behavior system without breaking rider controls. The tree's nodes are built with `new` in `WargBehaviorTree.BuildTree`, which resolves the services once per tree and passes them to the nodes' constructors (#659); no warg service node calls `IoC.Resolve<>()` itself.

### Solution Approach

- **BehaviorTreeAgentComponent** (TAOM-inlined at `Main/BehaviorTreeWrapper/`, was vendored `BehaviorTreeWrapper.dll` until 2026-05-24) — attached to each warg `Agent` via `AddComponent`, manually ticked by `WargMissionBehavior.OnMissionTick`
- **AutonomousMovementPlayerController** — `[DefaultView]` MissionView that takes over player movement during rage mode
- **SpatialGrid** — O(1) cell-based spatial partitioning for enemy proximity queries
- **CustomAttacksUtils** — reflection-based `Mission.RegisterBlow` access for programmatic damage

### Component Diagram

```
LOTRLOME_Armory (XML: monster, items, animations, sounds)
        |
  WargMissionBehavior (MissionLogic)
     |         |
     |    BehaviorTreeMissionLogic (ticks all BTs)
     |         |
     |    WargBehaviorTree (BT structure)
     |      /     |        \
     |   Rage   Attack    Movement
     |  (decorators+tasks using IBTWargBlackboard)
     |         |
     |    WargAttackService (damage calc, bone collision)
     |      /        \
  SpatialGrid    CustomAttacksUtils
  (proximity)    (RegisterBlow reflection)
     |
  IMissionAdapterFactory -> IAgentAdapter
  (wraps sealed Agent)
```

## Configuration

### WargConfig Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `WargAttackRange` | 1.0f | Attack hit range |
| `SleepAfterAttack` | 3 | Seconds idle after non-rage attack |
| `TargetDetectionRange` | 20f | Target scan radius (m) |
| `MaxSpeedDamage` | 20 | Maximum speed-based damage component |
| `MaxBaseDamage` | 40 | Maximum base damage component |
| `SpeedForMaxDamage` | 20f | Velocity (m/s) for max speed damage |
| `DamageToFlinch` | 10 | Damage threshold for flinch animation |
| `DamageToFall` | 40 | Damage threshold for fall animation |
| `rageChance` | 0.1 | 10% chance to enter rage on hit |
| `minDamageReceivedForRage` | 10 | Minimum damage to trigger rage roll |
| `minRageAttacks` | 2 | Minimum attacks in rage mode |
| `maxRageAttacks` | 3 | Maximum attacks in rage mode |
| `maxDistanceFromWargToRollForRage` | 20 | Max distance from attacker for rage |

### Rage Mode Flow

1. Warg takes >10 damage from enemy within 10 units
2. 10% chance to enter rage (2-3 attacks)
3. Player: loses control, sees "Your warg entered into a rage" message
4. AI: rider leaves formation, warg navigates to enemy
5. Warg attacks, decrementing rage counter
6. On completion/timeout (6s)/enemy death: control returns

## Key Files

| File | Purpose |
|------|---------|
| **Adapters** | |
| `Main/Adapters/IAgentAdapter.cs` | Mission-scope agent interface (IsWarg, CustomAttack, ProjectAgent) |
| `Main/Adapters/AgentAdapter.cs` | Wraps sealed Agent for mission-time operations |
| `Main/Adapters/IMissionAdapterFactory.cs` | Factory creating IAgentAdapter instances |
| `Main/Adapters/MissionAdapterFactory.cs` | Adapter cache keyed by agent OBJECT, evicted on `OnAgentDeleted`, logs index reuse (#592) |
| `Main/Adapters/AgentAdapterCache.cs` | The pure store behind the factory: reference identity, eviction, reuse count |
| `Main/Adapters/IAgentVisualsAdapter.cs` | Skeleton/frame access interface |
| `Main/Adapters/AgentVisualsAdapter.cs` | Wraps MBAgentVisuals |
| `Main/Adapters/DamageAnimation.cs` | Enum: Nothing, Flinch, Fall |
| **AdvancedCombat** | |
| `Main/Features/AdvancedCombat/SpatialGrid.cs` | Cell-based spatial partitioning (CellSize=20) |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | Reflection-based Mission.RegisterBlow |
| `Main/Features/AdvancedCombat/BoneCheck.cs` | Frame-by-frame bone collision |
| `Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs` | Collision during action progress range |
| `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` | MissionLogic: SpatialGrid + BoneCollision ticking; owns the adapter cache lifecycle (build, delete, mission end, #592) |
| `Main/Features/AdvancedCombat/MissionThreadGuard.cs` | Marks the main mission thread; reports once per site when a blow or creature action runs off it (#592) |
| `Main/Features/AdvancedCombat/AgentSlotIdentity.cs` | Is this managed `Agent` still its slot's occupant (`Mission.FindAgentWithIndex`); the guard behind `AgentAdapter.IsActive()` and `TakeDamage` (#592) |
| `Main/Features/AdvancedCombat/AutonomousMovementPlayerController.cs` | [DefaultView] MissionView for rage mode |
| `Main/Features/AdvancedCombat/AdvancedCombatIoC.cs` | Registers IBoneCollisionService, ISpatialGridDebugService |
| `Main/Features/AdvancedCombat/TaomBTLogger.cs` | ILogger forwarding to IModLogger |
| **Warg** | |
| `Main/Features/Warg/WargBehaviorTree.cs` | BT structure definition (root node) |
| `Main/Features/Warg/WargMissionBehavior.cs` | MissionLogic: registers BT, manages warg agents |
| `Main/Features/Warg/WargAttackService.cs` | Damage calculation, attack execution |
| `Main/Features/Warg/IWargAttackService.cs` | Attack service interface |
| `Main/Features/Warg/WargConfig.cs` | Constants |
| `Main/Features/Warg/WargRiderHandManager.cs` | Player hand positioning on warg mane |
| `Main/Features/Warg/WargIoC.cs` | Registers IWargAttackService |
| `Main/Features/Warg/BehaviorTreeElements/` | 15 BT nodes (decorators, tasks, listeners) |

## Dependencies

- `Main/BehaviorTrees/` + `Main/BehaviorTreeWrapper/`: TAOM-inlined BT framework; since #592 `BehaviorTreeMissionLogic.OnMissionTick` ticks every scheduled `BehaviorTreeAgentComponent` on the main thread and the component's engine-driven `OnTick` is a no-op (decompiled from formerly-vendored `BehaviorTrees.dll` + `BehaviorTreeWrapper.dll` on 2026-05-24, full source ownership; compiles into `TAOM.dll`)
- `LOTRLOME_Armory` (external, untracked) - Monster id="warg", `as_warg` action sets, 80 `act_warg_*` types,
  the `warg` usage set, animations, sounds and the four warg items. Absorbed from the retired
  `Alliance.Wargs` module on 2026-08-28; ledger: [lotrlome-warg-changes.md](../reference/lotrlome-warg-changes.md)
- `IModLogger` (Core) — Logging
- `IMissionAdapterFactory` (Adapters) — Agent wrapping

## Tests

- **Adapter cache (#592):** `TAOM.Tests/Adapters/AgentAdapterCacheTests.cs` (15) and `MissionAdapterFactoryTests.cs` (6, on bare uninitialized `Agent` objects): reference identity, eviction, index-reuse count and the once-per-mission reuse log.
- **Current:** `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs` — 7 tests covering the pure damage formula in `CalculateWargAttackDamage` via a testable subclass that stubs the sealed armor lookup.
- **Coverage gap (tracked in #178):** `HandleWargTargetHit` and `WargAttack` accept sealed `Agent` directly in their signatures (ADR-007 violation), so they cannot be unit-tested without the engine runtime. Closing #178 requires refactoring `IWargAttackService` to accept `IAgentAdapter` instead; once that lands, the missing tests can be added.
- **Tick-cost tests (plan 015, #659):** `TAOM.Tests/Features/Warg/WargTickCostTests.cs` (15) pins, in the IL, that no per-tick node method reaches `IoC.Resolve`, that the four service nodes reach it from no body at all (constructors and field initializers included) while `WargBehaviorTree.BuildTree` resolves each service exactly once, and that the grid scans use the buffer overload without constructing a list; it checks by reflection that no node keeps a service or buffer in a static field, and proves the IL checks against control fixtures; `TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs` (4) pins in the IL that the bite's `Tick` reads the action progress once and fetches the attacker skeleton only after the progress tests; `TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs` (9) checks the grid query against a brute-force sphere scan through its generic helpers, plus column order and a point that moved since the rebuild; `BoneCheckRangeGateTests.cs` (10) pins that a target's skeleton is fetched only inside the range gate, that a NaN frame fails the gate, and every per-target skip.

## How to Add a New Creature with Custom Attacks

1. Add the Monster definition to `LOTRLOME_Armory` (the warg is the reference shape)
2. Create `BehaviorTreeElements/` folder with BT nodes implementing your creature's AI
3. Create a `{Creature}BehaviorTree.cs` using the fluent BT builder API
4. Create `{Creature}MissionBehavior.cs` to register the BT and attach components
5. Create `{Creature}AttackService.cs` for damage calculation
6. Register services in IoC, add MissionBehavior in SubModule.cs `OnMissionBehaviorInitialize`
7. Identify warg/creature via `agent.Monster.StringId == "your_monster_id"`

## Bannerlord 1.3.12 Gotchas

- **`OnBehaviorInitialize` not called**: Behaviors added during `SubModule.OnMissionBehaviorInitialize` do NOT get `OnBehaviorInitialize` called in 1.3.12. Use first-tick initialization via `_initialized` flag instead.
- **`OnTickAsAI` for mount agents**: May not be called by the engine for mount agents. WargMissionBehavior manually ticks BT components as a safety net.
- **`WeakGameEntity` not `GameEntity`**: `Mission.RegisterBlow` parameter 3 is `WeakGameEntity` in 1.3.12, not `GameEntity`. Pass `WeakGameEntity.Invalid` (struct, not null).
- **`MBAgentVisuals` not `AgentVisuals`**: `Agent.AgentVisuals` returns `MBAgentVisuals` in 1.3.12.
- **`OnMainAgentChangedDelegate(Agent oldAgent)`**: Single parameter in 1.3.12, not `(object sender, PropertyChangedEventArgs e)`.
- **Trees tick on the main thread (v1.4.8, #592)**: single-player runs `Agent.Tick`, and with it every `AgentComponent.OnTick`, on the engine's asynchronous AI thread (`MissionState.cs:201`, `Mission.TickAgentsAndTeams`). A tree that registers a blow from there runs the engine's hit pipeline and TAOM's own collections against the engine's agent-removed callbacks, which native raises on the thread it chooses (a v1.4.8 player log caught one off the main thread, #634); two player freezes. `BehaviorTreeAgentComponent.OnAgentRemoved` hands its cleanup to `BehaviorTreeMissionLogic.RunOnMissionThread`, so the schedule list and tree map change only on the mission tick (#634). `BehaviorTreeAgentComponent.OnTick` is a no-op and `BehaviorTreeMissionLogic.OnMissionTick` ticks every scheduled tree. `MissionThreadGuard` logs once if a blow or creature action ever runs off the main thread. Behaviors run in reverse registration order, and `AdvancedCombatBehavior` is registered after the tree logic so its bone checks tick before the trees: a bite's first check lands next frame, as it did when the trees ran on the async thread (Codex review 109 recommended restoring that order because whether `GetCurrentAction(0)` reflects `SetActionChannel` in the same frame is unverified). The grid the tree scans, `SpatialGrid`, is main-thread-only too: rebuilt by replacing the map, evicted on deletion (a removal raised off the main thread waits for `ApplyPendingRemovals` on the mission tick, #634), tripwired. The third player freeze of 2026-09-13 had only warg trees running.
- **Agent indices are recycled within a mission (v1.4.8, #592)**: a deleted agent's index goes to the next agent built, and the dead managed `Agent` keeps its native pointers on the recycled slot, so `IsActive()`, velocity and `SetActionChannel` act on the new occupant while `Monster` and `Name` describe the old one. Decide "is this a warg" from `agent.Monster?.StringId`, never from a cached adapter, and evict any index-keyed store in `OnAgentDeleted`. A handle held across frames (a bone check's targets) re-validates with `AgentSlotIdentity.IsCurrentOccupant` (`Mission.FindAgentWithIndex(index) == agent`) inside `AgentAdapter.IsActive()` and again in `CustomAttacksUtils.TakeDamage`. Every `SetActionChannel` in `AgentAdapter` refuses a clip the agent's action set lacks and logs it once.

## Performance

- **SpatialGrid**: cells are keyed on (x, y) only (the distance test stays 3D), so the 60 m "no enemy close" scan looks up 49 cells instead of 343; every warg node scans into a reused buffer through the zero-allocation overload.
- **BoneCheck**: the attacker's bone positions reuse one list and its skeleton is fetched once per tick; a target's skeleton is fetched only inside the 20 square-metre gate (about 4.5 m), because `MBAgentVisuals.GetSkeleton()` builds a new finalizable native wrapper on every call. `BoneCheckDuringAnimation.Tick` reads the action progress once per tick and fetches the attacker's skeleton only once the progress reaches the hit window, so a wind-up frame builds no wrapper. The one behaviour difference: a missing attacker skeleton during the wind-up now ends the bite when the hit window opens, not at once. The owed in-game warg Custom Battle is the proof for this change (bites must still land and end as before); no unit test can call `Tick`.
- **Services in BT nodes**: `WargBehaviorTree.BuildTree` resolves `IMissionAdapterFactory` and `IWargAttackService` once per tree and passes them to the constructors of the four nodes that need them (`PeriodicallyCheckIfCanAttackAnyone`, `CheckOnceIfCanAttackEnemy`, `WargAiControlledIsNotFacingEnemy`, `WargAttackTask`), which keep them in private readonly instance fields and never call `IoC.Resolve` (#659). The tree's attack tasks share one `WargAttackService`, which keeps no per-call state. `LogTask` still resolves its logger per Execute; it runs only when the tree changes branch. `WargRiderHandManager.Tick` decides warg-ness from the mount's `Monster` with `WargConfig.IsWargMonster`, with no container or adapter-cache lookup.
- **Grid updates**: Every 5 ticks via AdvancedCombatBehavior, not every frame

## Changelog

- 2026-09-24 - #659, maintainer decisions on the plan 015 review: `WargBehaviorTree.BuildTree`
  resolves the node services once per tree and injects them, so the nodes hold no `IoC.Resolve`;
  `BoneCheckDuringAnimation.Tick` reads the action progress once and fetches the attacker skeleton
  only inside the hit window (a missing attacker skeleton during the wind-up now ends the bite when
  the hit window opens, not at once; the owed in-game warg Custom Battle is the proof that bites
  still land and end as before). The wider scan results between grid rebuilds are kept.
- 2026-09-24 - plan 015 (#659): per-tick costs cut. The tree nodes resolve their services once, the three
  scans reuse buffers, the grid keys cells on (x, y) (49 lookups for a 60 m scan instead of 343),
  and a live bite fetches a target's skeleton only inside the 20 square-metre gate. Between grid
  rebuilds a scan can now return an agent that moved up or down into range since the rebuild, and
  agents in one column come back in rebuild order. Review:
  `../reviews/deep-review-015-warg-tick-costs-2026-09-24.md`.
- 2026-09-13 - #592: a reinforcement horse that inherited a dead warg's engine index was served the
  warg's cached adapter, got a warg tree, and asked the engine to play `act_warg_attack_running` on
  `as_horse` in the second a player's game froze. The adapter cache now keys by agent object, with
  its lifecycle in `AdvancedCombatBehavior`; `IsActive()` and `TakeDamage` re-validate slot identity;
  attach decisions read `Monster` directly; every `SetActionChannel` checks the clip exists first.
  RCA: `../reviews/rca-warg-clip-on-horse-2026-09-13.md`.
- 2026-08-28 - Absorbed the standalone `Alliance.Wargs` module into `LOTRLOME_Armory` so players no
  longer install it: Monster, `as_warg` action sets, 80 `act_warg_*` types, the `warg` usage set and its
  22 rider XSLT rows, physics/collision classes, 17 sound events, four items and the cooked asset pack.
  Ids unchanged, so no C# or troop-roster edit was needed. The spider's rider overlay borrows 12
  `rider_warg_*` clips, so this was a prerequisite for ever removing that module. Ledger:
  [lotrlome-warg-changes.md](../reference/lotrlome-warg-changes.md).
- 2026-05-14 — Phase 9b: refactored `IWargAttackService.HandleWargTargetHit`/`WargAttack` to take `IAgentAdapter` instead of sealed `Agent`, making all three attack methods directly unit-testable (closes #178).
- 2026-05-13 — Updated the Tests section to reflect `WargAttackServiceTests.cs` (7 tests); cross-referenced #178 ADR-007 blocker (#199).
- 2026-03-27 — Fixed BT runtime failures: moved tree init to first `OnMissionTick`, made the no-rider tree construction null-safe, so wargs actually attack in combat.
- 2026-03-26 — Initial port of the autonomous warg combat system from LOTRAOM (behavior-tree AI, rage mode, SpatialGrid, bone collision) for Bannerlord 1.3.12 (#44).

## GitHub Issue

- **Issue:** #44 — [feat: Port warg combat system from LOTRAOM](https://github.com/haterade22/TAOM/issues/44)
- **Status:** Closed (2026-08-08 issue triage)
- **Issue:** #659, [Warg battles: cut per-tick service lookups, scan allocations and skeleton wrappers](https://github.com/haterade22/TAOM/issues/659) (plan 015)
- **Status:** Open (in-game warg Custom Battle owed)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
