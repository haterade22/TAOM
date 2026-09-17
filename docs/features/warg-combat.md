# Warg Combat System

## Overview

Wargs are autonomous combat agents with their own AI behavior tree. When mounted by a rider, wargs independently attack nearby enemies and can enter a "rage mode" when damaged, temporarily taking over control from the player or AI rider. The system uses bone-based collision detection and a spatial grid for efficient enemy proximity queries.

## Why This Exists

- **Vanilla behavior:** Mounts are passive — they carry riders but never attack independently
- **TAOM requirement:** Wargs (Gundabad, Dol Guldur, Isengard) are predatory creatures that should actively fight, consistent with Middle-earth lore where wargs are intelligent, aggressive beasts
- **Without this feature:** Warg-mounted troops behave identically to horse cavalry, breaking immersion

## Architecture

### Design Challenge

The `Agent` class is sealed (cannot subclass). Warg AI must run alongside the existing mount behavior system without breaking rider controls. The BehaviorTree framework (pre-compiled DLLs) constructs nodes internally, preventing constructor injection — `IoC.Resolve<>()` is required in BT elements.

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
- **Other planned tests:** `TAOM.Tests/Features/AdvancedCombat/SpatialGridTests.cs` (still not present — Spatial grid logic uses live engine types and requires its own adapter work first).

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
- **Trees tick on the main thread (v1.4.8, #592)**: single-player runs `Agent.Tick`, and with it every `AgentComponent.OnTick`, on the engine's asynchronous AI thread (`MissionState.cs:201`, `Mission.TickAgentsAndTeams`). A tree that registers a blow from there runs the engine's hit pipeline and TAOM's own collections against the main thread's agent-removed callbacks; two player freezes. `BehaviorTreeAgentComponent.OnTick` is a no-op and `BehaviorTreeMissionLogic.OnMissionTick` ticks every scheduled tree. `MissionThreadGuard` logs once if a blow or creature action ever runs off the main thread. Behaviors run in reverse registration order, and `AdvancedCombatBehavior` is registered after the tree logic so its bone checks tick before the trees: a bite's first check lands next frame, as it did when the trees ran on the async thread (Codex review 109 recommended restoring that order because whether `GetCurrentAction(0)` reflects `SetActionChannel` in the same frame is unverified). The grid the tree scans, `SpatialGrid`, is main-thread-only too: rebuilt by replacing the map, evicted on deletion, tripwired. The third player freeze of 2026-09-13 had only warg trees running.
- **Agent indices are recycled within a mission (v1.4.8, #592)**: a deleted agent's index goes to the next agent built, and the dead managed `Agent` keeps its native pointers on the recycled slot, so `IsActive()`, velocity and `SetActionChannel` act on the new occupant while `Monster` and `Name` describe the old one. Decide "is this a warg" from `agent.Monster?.StringId`, never from a cached adapter, and evict any index-keyed store in `OnAgentDeleted`. A handle held across frames (a bone check's targets) re-validates with `AgentSlotIdentity.IsCurrentOccupant` (`Mission.FindAgentWithIndex(index) == agent`) inside `AgentAdapter.IsActive()` and again in `CustomAttacksUtils.TakeDamage`. Every `SetActionChannel` in `AgentAdapter` refuses a clip the agent's action set lacks and logs it once.

## Performance

- **SpatialGrid**: O(1) cell lookup but allocates new `List<Agent>` per query — consider list pooling for high-frequency paths
- **BoneCheck**: Allocates bone position list per tick — should be cached as class field
- **IoC.Resolve in BT evaluators**: Called every frame for factory lookups — should cache resolved instances
- **Grid updates**: Every 5 ticks via AdvancedCombatBehavior, not every frame

## Changelog

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

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
