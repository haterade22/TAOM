# Bannerlord Mission + MissionBehavior lifecycle (Phase 4)

> **One process, traced from the decompile** (v1.4.5): the in-battle runtime backbone — how `MissionBehavior`s are
> registered, dispatched (per-frame + per-event), and torn down. **Every in-battle TAOM feature plugs in here**
> (spider, elephant, warg, career, banner persistence, smart-cavalry, etc.), and it's the source of the
> `MissionBehaviorType.Logic` ⇒ `: MissionLogic` gotcha. Part of the phased engine study; the container that drives
> the `OnAgentBuild`/`OnMissionTick`/`OnAgentHit`/`OnEndMission` hooks referenced in Phases 1–3.

## WHAT it is

A `Mission` (one battle/encounter) holds a list of **`MissionBehavior`s** — modular units of in-mission logic.
The engine calls each behavior's virtuals at the right moments (init, every frame, on each agent spawn, on each
hit, at teardown). A mod adds its behaviors and overrides the virtuals it cares about. This is the standard,
sanctioned extension point for in-battle behavior — no Harmony needed.

## HOW it works

### The base contract — `MissionBehavior` (MissionBehavior.cs:9)
An abstract class with one abstract member, **`BehaviorType`** (MissionBehavior.cs:15), and a large set of
**virtual hooks** (all no-op by default — override what you need):
- **Lifecycle:** `OnBehaviorInitialize` (21), `OnCreated` (25), `OnAfterMissionCreated` (17), `EarlyStart`/`AfterStart`.
- **Agent:** `OnAgentCreated` (53), `OnAgentBuild(agent, banner)` (57), `OnAgentTeamChanged`, `OnAgentControllerSetToPlayer`, `OnAgentMount`/`OnAgentDismount` (150/154), `OnAgentRemoved`/`OnAgentDeleted`/`OnAgentFleeing`/`OnAgentPanicked`, `OnEarlyAgentRemoved`.
- **Combat:** `OnAgentHit(affected, affector, weapon, blow, collisionData)` (69), `OnScoreHit`, `OnMeleeHit`, `OnMissileHit`, `OnMissileCollisionReaction`.
- **Tick:** `OnPreMissionTick(dt)` (138), `OnMissionTick(dt)` (146), `OnFixedMissionTick(dt)`, `OnPreDisplayMissionTick(dt)`.
- **Teardown:** `OnEndMissionInternal`→`OnEndMission` (121/126), `OnRemoveBehavior` (130), `OnClearScene`.

### The two behavior kinds — `BehaviorType` + `MissionLogic` (MissionLogic.cs:7)
`MissionBehaviorType` distinguishes **`Logic`** vs **`Other`** (the two cases handled in `AddMissionBehavior`).
**`MissionLogic : MissionBehavior`** (MissionLogic.cs:7) hard-codes `BehaviorType => MissionBehaviorType.Logic`
(line 9) and adds the **battle-flow virtuals** only logic behaviors get: `OnBattleEnded` (22), `MissionEnded(ref
result)` (17), `OnEndMissionRequest(out canLeave)` (11), `ShowBattleResults`, `OnRetreatMission`/`OnSurrenderMission`,
`OnAutoDeployTeam`. **A behavior that needs Logic semantics (most gameplay behaviors) must inherit `: MissionLogic`.**

### Registration — `AddMissionBehavior` (Mission.cs:4603) ⭐
```
MissionBehaviors.Add(missionBehavior);
missionBehavior.Mission = this;
switch (missionBehavior.BehaviorType) {
  case Logic: MissionLogics.Add(missionBehavior as MissionLogic); break;   // ← the cast
  case Other: _otherMissionBehaviors.Add(missionBehavior); break;
}
missionBehavior.OnCreated();
```
A behavior goes into the master `MissionBehaviors` list **and** a typed list (`MissionLogics` or
`_otherMissionBehaviors`). `GetMissionBehavior<T>()` (Mission.cs:4619) is a linear `is T` search over
`MissionBehaviors` — how one behavior finds another (e.g. cross-feature lookups). `RemoveMissionBehavior`
(Mission.cs:4631) calls `OnRemoveBehavior` then removes from both lists.

### Dispatch
The engine iterates `MissionBehaviors` (or the typed lists) and calls the relevant virtual at each moment:
`OnAgentCreated`/`OnAgentBuild` during the spawn chain (Phase 1 — `CreateAgent` 4049-4052, `BuildAgent`),
`OnMissionTick` every frame, `OnAgentHit` on each blow, `OnEndMissionInternal`→`OnEndMission` + `OnRemoveBehavior`
at teardown, and (for `MissionLogics`) `OnBattleEnded`/`MissionEnded`.

### Lifecycle order (typical)
```
SubModule.OnMissionBehaviorInitialize(mission)   → mission.AddMissionBehavior(new XxxBehavior())  [OnCreated]
  → OnBehaviorInitialize → OnAfterMissionCreated → EarlyStart → AfterStart
  → per frame: OnPreMissionTick → OnMissionTick (+ OnFixedMissionTick)
  → per agent spawn: OnAgentCreated → OnAgentBuild
  → on hit: OnAgentHit / OnScoreHit; on removal: OnAgentRemoved
  → end: MissionEnded? → OnBattleEnded (Logic) → OnEndMissionInternal → OnEndMission → OnRemoveBehavior
```

## ⚠️ The `: MissionLogic` gotcha (confirmed at the source)

If a behavior is `: MissionBehavior` and **manually** returns `BehaviorType => MissionBehaviorType.Logic` **without**
inheriting `MissionLogic`, then in `AddMissionBehavior` `missionBehavior as MissionLogic` evaluates to **null** and
`MissionLogics.Add(null)` puts a **null in the `MissionLogics` list**. The engine then NREs the next time it
iterates `MissionLogics` (e.g. `CheckMissionEnded` → `MissionEnded`) — **every tick, immediately.** Fix: **inherit
`: MissionLogic`** (it sets `BehaviorType.Logic` for you). This is `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance`
(it has crashed TAOM twice — 3 ports in 2026-05 + the inlined `BehaviorTreeWrapper.dll` in 2026-05-24). Phase-4
confirmation: the null-cast is `Mission.cs:4610`.

## TAOM relevance
- **All in-battle TAOM behaviors** are `MissionLogic` subclasses added in `Main/SubModule.cs`
  `OnMissionBehaviorInitialize` (`ElephantMissionBehavior`, `SpiderMissionBehavior`, `WargMissionBehavior`,
  career, banner persistence, etc.). They override `OnMissionTick`/`OnAgentBuild`/`OnRemoveBehavior` (Phases 1–2).
- Use `GetMissionBehavior<T>()` for cross-behavior lookups (e.g. a feature checking whether another combat behavior
  is already managing `SpatialGrid`/`BoneCollision` — `SpiderMissionBehavior` does this).
- `OnAgentBuild` is the per-spawn hook (TAOM elephant adds spawned elephants to its shadow list here; the upstream pack spawns
  the howdah here).
- `OnRemoveBehavior` (per-mission teardown) is where TAOM clears its per-mission lists (broader than `OnBattleEnded`).
- **Never** declare `BehaviorType.Logic` without `: MissionLogic`. When porting a 3rd-party behavior, check its base
  class first (the `MissionBehaviorType=Logic`-without-`MissionLogic` pattern is a common external-mod bug).

## The native boundary
`MissionBehavior` dispatch is **managed** (the `Mission` C# iterates the behavior lists). The *agents* the behaviors
operate on, and the per-frame `Agent.Tick` (which auto-calls `AgentComponent.OnTick`), cross into native — but the
behavior framework itself is managed, which is why it's the safe, no-Harmony extension point.

## Evidence (file:line, v1.4.5)
- `MissionBehavior.cs`:9 (base), 15 (`BehaviorType` abstract), 21/25/17 (init), 53/57 (agent create/build), 69 (`OnAgentHit`), 146/138 (`OnMissionTick`/`OnPreMissionTick`), 121/126 (`OnEndMissionInternal`/`OnEndMission`), 130 (`OnRemoveBehavior`).
- `MissionLogic.cs`:7-9 (`: MissionBehavior`, `BehaviorType => Logic`), 11/17/22 (`OnEndMissionRequest`/`MissionEnded`/`OnBattleEnded`).
- `Mission.cs`:4603 `AddMissionBehavior` (the `as MissionLogic` null-cast @4610), 4619 `GetMissionBehavior<T>`, 4631 `RemoveMissionBehavior`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
