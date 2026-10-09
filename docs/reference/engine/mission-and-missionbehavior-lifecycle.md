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
  → EarlyStart → AfterStart   (TAOM's path: OnBehaviorInitialize and OnAfterMissionCreated never fire for it)
  [a behaviour handed to MissionState.OpenNew: OnAfterMissionCreated → OnCreated → OnBehaviorInitialize → EarlyStart
   → AfterStart; Mission.cs:3823-3831, MissionState.cs:263]
  → per frame: OnPreMissionTick → OnMissionTick (+ OnFixedMissionTick)
  → per agent spawn: OnAgentCreated → OnAgentBuild
  → on hit: OnAgentHit / OnScoreHit; on removal: OnAgentRemoved
  → end: MissionEnded? → OnBattleEnded (Logic) → OnEndMissionInternal → OnEndMission → OnRemoveBehavior
```

### Teardown paths: `OnEndMission` is not the only way out (v1.5.3)

Two separate engine sequences take a mission out of play. A behavior that must act on every exit (flush a summary, reset
a singleton) needs both callbacks.

- **`EndMission`.** `Mission.EndMission()` (Mission.cs:4635) marks the mission `EndingNextFrame`; the next `Mission.Tick`
  reaches `EndMissionInternal` (4644, through `CheckMissionEnd`: 4876-4878 in single player and on a host, 4885-4888 on a
  network client), which calls every behavior's `OnEndMissionInternal` and so `OnEndMission`, in registration order, in a
  plain `foreach` (4654-4657: a throw from one behavior skips the later ones). Its callers include `RetreatMission`
  (2869), `SurrenderMission` (2885), `OnEndMissionResult` (4767), `MissionState.OnTick` once `MissionEndTime` passes
  (MissionState.cs:105-107) and `MBGameManager.EndGame` (MBGameManager.cs:204), which the escape menu's "exit to main
  menu" calls (`MissionGauntletSingleplayerEscapeMenu.OnExitToMainMenu`, :220-225).
- **State finalisation.** `MissionState.OnFinalize` (MissionState.cs:49) calls `Mission.OnMissionStateFinalize`
  (Mission.cs:2216): every behavior's `OnMissionStateFinalized`, then `RemoveMissionBehavior` (4714) on each behavior,
  last registered first, which calls `OnRemoveBehavior`. Nothing requires an `EndMission` before it.
  `GameStateManager.CleanStates` finalises every state, an active mission's included (GameStateManager.cs:345-366), and
  it is reached from `Game.OnFinalize` (Game.cs:419; the engine's `CoreManaged.Finalize` callback ends there, through
  `Module.FinalizeModule`, when the application shuts down). On that path `OnEndMission` never runs for the live
  mission. What runs is `OnMissionStateDeactivated` (Mission.cs:2204; only if the state was active), then every
  behavior's `OnMissionStateFinalized`, then `OnRemoveBehavior`. Whether closing the window mid-mission reaches
  `CoreManaged.Finalize` is native and unverified; if it does not, no vanilla exit traced here skips `EndMission`, and a
  write in `OnRemoveBehavior` is defensive. Loading a save adds no vanilla exit: `SavedGameVM.StartGame` calls
  `CleanStates(0)` (SavedGameVM.cs:748-753), but only the map's escape-menu Load and the main menu's Saved Games option
  open the load screen (`SandBoxViewCreator.CreateSaveLoadScreen`), so no mission is live, and the mission escape menu
  (`MissionGauntletSingleplayerEscapeMenu.GetEscapeMenuItems`) has no Load item. A mod that loads a save mid-mission
  would take that path.

### An exception in `AfterStart` reloads the mission forever

`MissionState.FinishMissionLoading` (MissionState.cs:333-352) sets `_missionInitializing = false` first and only
then calls `Mission.AfterStart` (:345). If `AfterStart`, and so any behavior's `AfterStart`, throws, the next
`TickLoading` (:221-233), still reached because the mission has not left loading, sees `_missionInitializing`
false and runs `LoadMission` again. The loading screen never ends, the exception is swallowed at the managed boundary, and the
engine log shows only `Mission-AddTeam-<side>` per attempt. yotthani hit it when a Harmony patch applied at game
start compiled `Team.Tick` and ran `MovementOrder`'s static initializer with no mission, leaving the type broken
for the session (`TypeInitializationException` in every `Team.Initialize`). So keep every `AfterStart` throw-safe,
and apply a patch that touches a type with a static initializer only after the game has initialised that type
(TAOM's deferred categories, [submodule-lifecycle-and-harmony.md](submodule-lifecycle-and-harmony.md) "Deferred
application"). (yotthani, MithrilForge `docs/engine/bugs.md` B3, v1.5.3; v1.5.4: the managed sequence confirmed in
the v1.5.4 decompile via `taom-src`, the swallowed exception and the log line not re-checked.)

Since 2026-10-08 TAOM's mission-start guard (Patch103,
[mission-start-guard.md](../../features/mission-start-guard.md)) wraps the six start calls inside `Mission.AfterStart`
(both submodule callbacks, each behaviour's `OnBehaviorInitialize`, `EarlyStart` and `AfterStart`, each mission
object's `AfterMissionStart`): a throw from one of them is logged as `[MissionStartGuard]` and survived, and the battle
starts with that behaviour's call cut short. A throw from the calls it does not wrap (the spawn-path selector, and the
deployment plan and the weather model, which a mod can supply) still makes the engine load the mission again, and so
does a throw from a behaviour's `OnMissionScreenPreLoad`, which `MissionState.LoadMission` calls before `AfterStart`. Keeping every `AfterStart`
throw-safe stays the rule: the guard saves the battle, not the behaviour's setup.

## ⚠️ The `: MissionLogic` gotcha (confirmed at the source)

If a behavior is `: MissionBehavior` and **manually** returns `BehaviorType => MissionBehaviorType.Logic` **without**
inheriting `MissionLogic`, then in `AddMissionBehavior` `missionBehavior as MissionLogic` evaluates to **null** and
`MissionLogics.Add(null)` puts a **null in the `MissionLogics` list**. The engine then NREs the next time it
iterates `MissionLogics` (e.g. `CheckMissionEnded` → `MissionEnded`) — **every tick, immediately.** Fix: **inherit
`: MissionLogic`** (it sets `BehaviorType.Logic` for you). This is `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance`
(it has crashed TAOM twice: 3 ports in 2026-05 + the inlined `BehaviorTreeWrapper.dll` in 2026-05-24, whose `BehaviorTreeMissionLogic` now derives from
`MissionLogic`: [RCA](../../reviews/rca-looter-battle-nre-2026-05-24.md)). Phase-4
confirmation: the null-cast is `Mission.cs:4610`.

## TAOM relevance
- **All in-battle TAOM behaviors** are `MissionLogic` subclasses added in `Main/SubModule.cs`
  `OnMissionBehaviorInitialize` (`ElephantMissionBehavior`, `SpiderMissionBehavior`, `WargMissionBehavior`,
  career, banner persistence, etc.). They override `OnMissionTick`/`OnAgentBuild`/`OnRemoveBehavior` (Phases 1–2).
- Use `GetMissionBehavior<T>()` for cross-behavior lookups (e.g. a feature checking whether another combat behavior
  is already managing `SpatialGrid`/`BoneCollision` — `SpiderMissionBehavior` does this).
- `OnAgentBuild` is the per-spawn hook (TAOM elephant adds spawned elephants to its shadow list here; ADOD_Beasts spawns
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
