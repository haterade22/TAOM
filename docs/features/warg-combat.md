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
Alliance.Wargs (XML: monster, items, animations)
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
| `MaxSpeedDamage` | 20 | Maximum speed-based damage component |
| `MaxBaseDamage` | 40 | Maximum base damage component |
| `SpeedForMaxDamage` | 8.0f | Velocity for max speed damage |
| `DamageToFlinch` | 10 | Damage threshold for flinch animation |
| `DamageToFall` | 20 | Damage threshold for fall animation |
| `rageChance` | 0.1 | 10% chance to enter rage on hit |
| `minDamageReceivedForRage` | 10 | Minimum damage to trigger rage roll |
| `minRageAttacks` | 2 | Minimum attacks in rage mode |
| `maxRageAttacks` | 3 | Maximum attacks in rage mode |
| `maxDistanceFromWargToRollForRage` | 10.0f | Max distance from attacker for rage |

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
| `Main/Adapters/MissionAdapterFactory.cs` | ConcurrentDictionary cache by agent index |
| `Main/Adapters/IAgentVisualsAdapter.cs` | Skeleton/frame access interface |
| `Main/Adapters/AgentVisualsAdapter.cs` | Wraps MBAgentVisuals |
| `Main/Adapters/DamageAnimation.cs` | Enum: Nothing, Flinch, Fall |
| **AdvancedCombat** | |
| `Main/Features/AdvancedCombat/SpatialGrid.cs` | Cell-based spatial partitioning (CellSize=20) |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | Reflection-based Mission.RegisterBlow |
| `Main/Features/AdvancedCombat/BoneCheck.cs` | Frame-by-frame bone collision |
| `Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs` | Collision during action progress range |
| `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` | MissionLogic: SpatialGrid + BoneCollision ticking |
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

- `Main/BehaviorTrees/` + `Main/BehaviorTreeWrapper/` — TAOM-inlined BT framework (decompiled from formerly-vendored `BehaviorTrees.dll` + `BehaviorTreeWrapper.dll` on 2026-05-24, full source ownership; compiles into `TAOM.dll`)
- `Alliance.Wargs` — XML module: Monster id="warg", animations, sounds, items
- `IModLogger` (Core) — Logging
- `IMissionAdapterFactory` (Adapters) — Agent wrapping

## Tests

- **Current:** `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs` — 7 tests covering the pure damage formula in `CalculateWargAttackDamage` via a testable subclass that stubs the sealed armor lookup.
- **Coverage gap (tracked in #178):** `HandleWargTargetHit` and `WargAttack` accept sealed `Agent` directly in their signatures (ADR-007 violation), so they cannot be unit-tested without the engine runtime. Closing #178 requires refactoring `IWargAttackService` to accept `IAgentAdapter` instead; once that lands, the missing tests can be added.
- **Other planned tests:** `TAOM.Tests/Features/AdvancedCombat/SpatialGridTests.cs` (still not present — Spatial grid logic uses live engine types and requires its own adapter work first).

## How to Add a New Creature with Custom Attacks

1. Create XML module with Monster definition (like Alliance.Wargs)
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

## Performance

- **SpatialGrid**: O(1) cell lookup but allocates new `List<Agent>` per query — consider list pooling for high-frequency paths
- **BoneCheck**: Allocates bone position list per tick — should be cached as class field
- **IoC.Resolve in BT evaluators**: Called every frame for factory lookups — should cache resolved instances
- **Grid updates**: Every 5 ticks via AdvancedCombatBehavior, not every frame

## GitHub Issue

- **Issue:** #44 — [feat: Port warg combat system from LOTRAOM](https://github.com/haterade22/TAOM/issues/44)
- **Status:** Open
