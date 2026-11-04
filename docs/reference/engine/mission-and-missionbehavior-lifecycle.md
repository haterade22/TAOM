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

### Removing a behaviour during `OnMissionTick` (v1.5.5)

(TAOM-verified 2026-10-15 from the v1.5.5 managed decompile; engine behaviour read from the code, not run in game.)

`Mission.Tick` ticks the behaviours with a loop that counts down by index over the live `MissionBehaviors` list:
`for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--) MissionBehaviors[num2].OnMissionTick(dt);`
(`Mission.cs:3750-3753`; the `OnPreDisplayMissionTick` loop just above it has the same shape). The count is read
once, when the loop starts. `RemoveMissionBehavior` (`Mission.cs:4707-4724`) ends with `MissionBehaviors.Remove(...)`,
a `List<T>` removal that shifts every later element down by one.

What follows from those two facts:

- **Removing the behaviour that is ticking, or one at a higher index,** is safe for this loop: the elements it
  has not reached yet (lower indexes) do not move.
- **Removing one at a lower index than the current one** moves the current behaviour, and every behaviour between
  the two, down one slot, so the next index the loop visits holds the current behaviour again and its tick runs a
  second time in the same frame. Every other behaviour still ticks once; only the removed one misses its tick that
  frame.
- **Removing several** can leave the next index at or past the new `Count`, and `MissionBehaviors[num2]` then throws
  `ArgumentOutOfRangeException`. Whether a given sequence reaches that depends on which indexes go, so treat any
  removal other than the ticking behaviour's own as unsafe.

`DeploymentMissionController.FinishDeployment()` is such a removal: its last statement is
`base.Mission.RemoveMissionBehavior(this)` (`DeploymentMissionController.cs:41-80`, re-checked on v1.5.5). Calling it
from another behaviour's `OnMissionTick` removes the controller from inside the tick loop (TAOM's parked
`ShaderPrecompilePlayerAgentGuard` does, see [shader-precompilation.md](../../features/shader-precompilation.md)).
The engine's own call is the auto-finish at the end of `SetupTeams` (`:181-184`), and the controller's own `OnMissionTick`
runs `SetupTeams` (`:83-96`, v1.5.6), so that call sits inside the tick loop. It holds because the loop runs downward and the
removals take the running controller and, for a battle, the handler above it (`BattleDeploymentMissionController.cs:51-55`;
a 2026-11-04 mission list had them at 26 and 27), so no index below the cursor moves. A removal from a behaviour higher in
the list is the case the rule below forbids: it moves the running behaviour down, and the loop ticks two behaviours twice.

**Rule:** never remove a behaviour from inside `OnMissionTick`. Set a flag there and act on it at the start of
the next frame's tick (or from a callback that is not inside the behaviour loop). This is an inference from the
loop and the list semantics above; no TAOM run has hit the repeated tick.

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

**Never spawn an agent inside `AfterStart`.** The engine runs the behaviours' `AfterStart` with
`foreach (MissionBehavior missionBehavior2 in MissionBehaviors)` (Mission.cs:3833-3836, v1.5.6), and
`AddMissionBehavior` appends to that same list (Mission.cs:4682, v1.5.6). An agent spawned inside the loop runs every
`SpawnAgent` hook, and a hook that adds a behaviour on the first spawn changes the list mid-loop: DualWield's
`SpawnAgent` postfix calls `AddMissionBehavior` once the mode is a combat mode, and `MissionCombatantsLogic.AfterStart`
sets Battle (MissionCombatantsLogic.cs:242, v1.5.6) before later behaviours run. The throw comes from the enumerator's
`MoveNext`, outside every call the guard wraps, so the engine loads the mission again on the same mission object:
the initialize and early-start calls ran twice, and `AfterStart` ran twice for the behaviours up to the one that spawned, and the second pass fails with "Number of teams is not 0" and "An element with the
same name already exists". The engine also walks `MissionBehaviors` with `foreach` in `EarlyStart` (3827), `OnAfterMissionLoadingFinished` (3850),
`OnAgentCreated` (4080) and `OnAgentBuild` (4375, 4389) (all v1.5.6), so never spawn from a callback the engine
dispatches with `foreach`; `OnMissionTick` is an index loop (3751-3754). Vanilla battles spawn troops from mission
ticks. Spawn on the first mission tick instead
(Fords of War, 2026-10-31: `FordsOfWarMissionLogic` marks its start as due in `AfterStart` and starts on the first
`OnMissionTick`).

### A custom siege mission must answer two engine questions (v1.5.6)

Leaving vanilla's spawn logic out of a mission's list breaks two engine paths whose call sites name neither it nor the removed behaviors. This section is read from the decompile for Long Defeat (#1079). Two Phase 1 runs (20261104-033424 and 20261104-035343) booted with `LongDefeatMissionLogic` implementing `IBattlePowerCalculationLogic` and no vanilla spawn logic, passed the `boot` check, and ran 2 and 3 waves with the team AI ticking and no crash. They did not show the retreat itself: no wave retreats in this mode.

- **Team power.** `TeamQuerySystem` takes `GetMissionBehavior<IBattlePowerCalculationLogic>()` with no null check (TeamQuerySystem.cs:132, :454-470). `TacticDefendCastle.ManageFormationCounts` (:276-278) and `TacticBreachWalls` (:26) read it on the first team AI tick, on the asynchronous AI thread, outside every mission logic's try. Vanilla's `BattlePowerCalculationLogic` does not help, because it reads `IMissionAgentSpawnLogic` (:38-42). A behavior must implement the interface. A real running total keeps vanilla's retreat: a new `TacticBreachWalls` takes its start from `RemainingPowerRatio` (:26, reached through `ManageFormationCounts` :460-463; :596 runs only if it first saw no enemy), so each wave starts at 1.0 and retreats when the ratio falls under the lane threshold (:34-45, :120), about a 40 percent drop with every lane closed. `Retreating` is one-way (:484-487). `LongDefeatMissionLogic` returns a large constant, so power never orders that retreat or `TacticDefendCastle`'s sally (:136), and no Long Defeat run can show that retreat.
- **Player deployment.** `GeneralsAndCaptainsAssignmentLogic.OnBattleSideSpawned` (:49-51) asks `BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle`, and `CustomBattleInitializationModel` (:42) reads `DefaultBattleMissionAgentSpawnLogic` with no null check. Leave that logic out, or wrap the call in `BattleInitializationModel.SetBypassPlayerDeployment`, a static that must be set back. `AssignPlayerRoleInTeamMissionController` sets the order owner by itself, so the order bar does not need it.

Three more engine facts for a mission that fights in waves:

- **A breach of 100 attackers or fewer leaves its engines.** `TacticBreachWalls.DetermineCurrentLanes` calls `StopUsingAllMachines` and keeps one lane when `InitialUnitCount` is at most 100 (:544-553, :558-572). It counts when the tactic first sees an enemy, so a tactic that first ticks on a partial spawn batch latches that for good, and `Team.ResetTactic` keeps the same object: give each wave a new `TacticBreachWalls`. `StopUsingAllMachines` also clears `ForcedUse` on every engine of the side (TacticComponent.cs:534-540), and for towers and rams only `SiegeWeapon.OnInit` and a player's order set it back, so a new tactic alone does not restore it: call `SetForcedUse(true)` before the reset.
- **A destroyed machine leaves `ActiveMissionObjects`.** `Mission.OnObjectDisabled` disables a `UsableMachine` in the same call (Mission.cs:3060), so a list read from the active objects cannot show a destroyed gate, tower or ram. Take the list once.
- **Vanilla's undo of a destroyed gate** is `CastleGate.OnMissionReset` (protected internal, :341-348). It restores the hit points, the door references and the tick. It does not close the navmesh that `OnDestroyed` opened: `SetAutoOpenState(false)` does that. It does not remake the gate's detachment, which `UsableMachine.Disable` destroyed (:659-661).

## ⚠️ The `: MissionLogic` gotcha (confirmed at the source)

If a behavior is `: MissionBehavior` and **manually** returns `BehaviorType => MissionBehaviorType.Logic` **without**
inheriting `MissionLogic`, then in `AddMissionBehavior` `missionBehavior as MissionLogic` evaluates to **null** and
`MissionLogics.Add(null)` puts a **null in the `MissionLogics` list**. The engine then NREs the next time it
iterates `MissionLogics` (e.g. `CheckMissionEnded` → `MissionEnded`) — **every tick, immediately.** Fix: **inherit
`: MissionLogic`** (it sets `BehaviorType.Logic` for you). This is the [lesson](../../reviews/lessons/gamemodels-services.md)
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

- [docs/ai-includes/orientation.md](../../ai-includes/orientation.md)
- [docs/features/long-defeat.md](../../features/long-defeat.md)
- [docs/features/shader-precompilation.md](../../features/shader-precompilation.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/reviews/lessons/adapters-taleworlds-api.md](../../reviews/lessons/adapters-taleworlds-api.md)

<!-- backlinks-end -->
