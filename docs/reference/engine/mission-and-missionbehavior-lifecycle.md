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

### Removing a behaviour during `OnMissionTick` (v1.4.8)

(Read on the 1.5.x line 2026-10-15 from v1.5.5; every line below re-read on v1.4.8 with taom-src for plan 144; engine behaviour read from the code, not run in game.)

`Mission.Tick` ticks the behaviours with a loop that counts down by index over the live `MissionBehaviors` list:
`for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--) MissionBehaviors[num2].OnMissionTick(dt);`
(`Mission.cs:3741-3744`; the `OnPreDisplayMissionTick` loop just above it has the same shape). The count is read
once, when the loop starts. `RemoveMissionBehavior` (`Mission.cs:4643-4660`) ends with `MissionBehaviors.Remove(...)`,
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
`base.Mission.RemoveMissionBehavior(this)` (`DeploymentMissionController.cs:41-80`, re-checked on v1.4.8). Calling it
from another behaviour's `OnMissionTick` removes the controller from inside the tick loop (TAOM's `ShaderPrecompilePlayerAgentGuard` does on its Order of Battle fallback, `ShaderPrecompilePlayerAgentGuard.cs:94`, see [shader-precompilation.md](../../features/shader-precompilation.md)).
The engine's own call is the auto-finish at the end of `SetupTeams` (`:162`, the call at `:183`); which callback runs `SetupTeams` was
not traced here, so whether that call sits inside the tick loop is open.

**Rule:** never remove a behaviour from inside `OnMissionTick`. Set a flag there and act on it at the start of
the next frame's tick (or from a callback that is not inside the behaviour loop). This is an inference from the
loop and the list semantics above; no TAOM run has hit the repeated tick.

### Lifecycle order (typical)
```
SubModule.OnMissionBehaviorInitialize(mission)   → mission.AddMissionBehavior(new XxxBehavior())  [OnCreated]
  → EarlyStart → AfterStart   (TAOM's path: OnBehaviorInitialize and OnAfterMissionCreated never fire for it)
  [a behaviour handed to MissionState.OpenNew: OnAfterMissionCreated → OnCreated → OnBehaviorInitialize → EarlyStart
   → AfterStart; v1.4.8 Mission.cs:3809-3816 (OnBehaviorInitialize runs before the submodules add TAOM's
   behaviours), MissionState.cs:263]
  → per frame: OnPreMissionTick → OnMissionTick (+ OnFixedMissionTick)
  → per agent spawn: OnAgentCreated → OnAgentBuild
  → on hit: OnAgentHit / OnScoreHit; on removal: OnAgentRemoved
  → end: MissionEnded? → OnBattleEnded (Logic) → OnEndMissionInternal → OnEndMission → OnRemoveBehavior
```

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
application"). (yotthani, MithrilForge `docs/engine/bugs.md` B3, v1.5.3. On v1.4.8 the managed sequence is the same, re-read with taom-src for plan 144: `MissionState.cs:333-352`, `AfterStart` at `:345`, `TickLoading` `:221-233`, `LoadMission` `:235-243`. On this line SaveShield's MISSION-INIT finalizer on `FinishMissionLoading` swallows the throw; the log line is not re-checked.)

Since plan 109 (the 1.5.x guard of 2026-10-08, ported to this line) TAOM's mission-start guard (Patch103,
[mission-start-guard.md](../../features/mission-start-guard.md)) wraps the six start calls inside `Mission.AfterStart`
(both submodule callbacks, each behaviour's `OnBehaviorInitialize`, `EarlyStart` and `AfterStart`, each mission
object's `AfterMissionStart`): a throw from one of them is logged as `[MissionStartGuard]` and survived, and the battle
starts with that behaviour's call cut short. A throw from the calls it does not wrap (the spawn-path selector, and the
deployment plan and the weather model, which a mod can supply) still makes the engine load the mission again, and so
does a throw from a behaviour's `OnMissionScreenPreLoad`, which `MissionState.LoadMission` calls before `AfterStart`. Keeping every `AfterStart`
throw-safe stays the rule: the guard saves the battle, not the behaviour's setup.

**Never spawn an agent inside `AfterStart` (v1.4.8).** `Mission.AfterStart` runs every behaviour's
`EarlyStart` and then every behaviour's `AfterStart` with `foreach` over `MissionBehaviors`, a
`List<MissionBehavior>` (Mission.cs:1329, 3817-3826), and `AddMissionBehavior` appends to that same list
(Mission.cs:4615-4617). An agent spawned inside the loop runs every agent-creation hook, and a hook
(TAOM's or another mod's) that adds a behaviour on that spawn changes the list mid-loop, so the
enumerator's next step throws. `MissionState.FinishMissionLoading` clears `_missionInitializing` before it
calls `AfterStart` (MissionState.cs:335, 345), and `AfterStart` sets `State.Continuing` only on its last
line (Mission.cs:3835), so the next `MissionState.OnTick` reaches `LoadMission` and `Mission.Initialize`
again on the same mission object (MissionState.cs:89-96, 221-244). The engine also walks
`MissionBehaviors` with `foreach` for `OnAgentCreated` (Mission.cs:4061) and `OnAgentBuild` (4344, 4358),
so never spawn from a callback the engine dispatches with `foreach`; `OnMissionTick` is an index loop
(3741-3744). Spawn on the first mission tick instead. Line numbers: the v1.4.8 dump (`_categories_v1.4.8`).

## ⚠️ The `: MissionLogic` gotcha (confirmed at the source)

If a behavior is `: MissionBehavior` and **manually** returns `BehaviorType => MissionBehaviorType.Logic` **without**
inheriting `MissionLogic`, then in `AddMissionBehavior` `missionBehavior as MissionLogic` evaluates to **null** and
`MissionLogics.Add(null)` puts a **null in the `MissionLogics` list**. The engine then NREs the next time it
iterates `MissionLogics` (e.g. `CheckMissionEnded` → `MissionEnded`) — **every tick, immediately.** Fix: **inherit
`: MissionLogic`** (it sets `BehaviorType.Logic` for you). This is the [lesson](../../reviews/lessons/gamemodels-services.md)
(it has crashed TAOM twice — 3 ports in 2026-05 + the inlined `BehaviorTreeWrapper.dll` in 2026-05-24). Phase-4
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
