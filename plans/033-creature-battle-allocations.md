# Plan 033: Cut creature-battle allocations and O(N) scans without changing what a creature does

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. This plan's work
> lands on its own branch: never commit it onto a branch that carries another plan's work. The
> orchestrator keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat dffdf879..HEAD -- Main/BehaviorTreeWrapper Main/BehaviorTrees Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackOffCooldownDecorator.cs Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs Main/Features/Spider/BehaviorTreeElements/SpiderAttackOffCooldownDecorator.cs Main/Features/Spider/BehaviorTreeElements/SpiderAttackTaskBase.cs Main/Features/Warg/BehaviorTreeElements/SetRageAttackTimer.cs Main/Features/Warg/BehaviorTreeElements/WargCanNotFindEnemyDecorator.cs Main/Features/AdvancedCombat/SpatialGrid.cs Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs Main/Features/CreatureBandits/BehaviorTreeElements/CreatureHuntTask.cs TAOM.Tests/BehaviorTrees TAOM.Tests/BehaviorTreeWrapper TAOM.Tests/Features/AdvancedCombat TAOM.Tests/Features/CreatureBandits docs/features/advanced-combat.md docs/features/creature-bandits.md`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Expected drift that is not a mismatch: plan 030 edits
> `docs/features/creature-bandits.md` near line 271 and files under `Main/Features/CreatureBandits/Diagnostics`
> and `TAOM.Tests/Features/CreatureBandits/CreatureDiag*`; this plan touches none of those lines or files.
> Also run `cat .claude/pinned-game-version.txt`: it must print `v1.5.3` (the native facts below were
> read from that binary). Anything else is a STOP.
>
> `docs/reference/engine/mission-frame-threads-and-native-costs.md`, cited below for engine facts, is not in
> `dffdf879`: it arrives in `761b20fe` on the program branch. Every fact this plan takes from it is inlined
> where it is cited, so if your tree lacks the file, that is not a STOP; never create or edit it.

## Status

- **Priority**: P2
- **Effort**: L (four independent changes, each with its own tests and commit)
- **Risk**: MED (every change is designed to be behaviour-preserving; the risk is a missed reader or listener)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` (net472),
  failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the other languages;
  the paid translator run waits on the maintainer). Python suite: not recorded and not needed (this plan
  touches no `tools/` file).
- **Issue**: filed by the orchestrator before execution

## Why this matters

Creature battles (wargs, spiders, elephants and mumakil, elk, war rams, trolls, creature bandits) pay for
work that changes nothing. On the .NET Framework CLR every main-thread allocation feeds gen0 collections,
which pause every managed thread. Today: the creature trees' sleep, wait, cooldown and rage timers read
`DateTime.Now` (a time-zone conversion) each time their node evaluates, which for a running tree is every
frame; every `Selector` re-entry allocates two `List`s; every agent
removal in every battle allocates boxed argument arrays whether or not a tree listens, and an off-thread
callback nobody listens to (for example `OnAgentShootMissile`, which no TAOM tree subscribes to) still
parks a closure; the always-on `SpatialGrid` rebuilds from every agent every 2 s in every battle, creatures
or not, with a fresh `Dictionary` and per-cell `List`s, and each agent deletion walks every cell; and each
creature bandit walks every hostile agent four times a second to find its nearest target. After this plan
the trees read `DateTime.UtcNow`, the selector reuses its lists, the dispatcher returns before allocating
or parking when nothing listens, the grid reuses its storage, removes in O(cell) and stops rebuilding while
nothing reads it, and the hunt asks the engine for nearby humans first. Each creature still makes the same
decisions: where a result could differ at all (a fresher grid on the first read after an idle spell, an
off-thread event raised in the frame a first listener appears, an exact float tie in the hunt) the plan
says so and why it is within today's behaviour.

The howdah skeleton fetch the brief named is NOT changed here: neither proposed fix is provably equivalent
(see "Excluded at planning: the howdah skeleton").

## Current state

### Plan 015 already landed: do not redo it

Plan 015 (#659) fixed the warg tree's per-tick resolves, scan buffers and skeleton fetches:
`87f9f862` (resolve tree services once, scan into buffers), `7577894d` (range-gate bone targets before
skeletons), `e5f644da` (key SpatialGrid cells on x and y), `fe8f30c7`, `5d9d4cc9`, `56eb4bc8` (review
follow-ups), `2420761b` (docs). Read `git show 87f9f862 --stat` and `TAOM.Tests/Features/Warg/WargTickCostTests.cs`
before Step 2: that test file is the pattern for every IL rule in this plan, and it must stay green.

### The tree cadence (context only: do NOT change it)

`Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs:59-70`:

```csharp
    internal void TickOnMissionThread(float dt)
    {
        // The engine's IsActive() answers for whoever occupies the agent's slot; a component left
        // scheduled for a deleted agent must not run its tree against the slot's new tenant (#592).
        if (Tree == null || !Agent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(Agent)) return;
        timeSinceLastEvaluation += dt;
        if ((Tree._rootEvaluationDelay / 1000) < timeSinceLastEvaluation || Tree.ShouldRunNextTick)
        {
            Tree.RunTree();
            timeSinceLastEvaluation = 0f;
        }
    }
```

`_rootEvaluationDelay` is an `int` (`Main/BehaviorTrees/BehaviorTreesCore.cs:36`), and the TAOM trees pass
10, so `10 / 1000 == 0` and every tree runs every frame. Changing that changes creature reaction time: it is
out of scope. Everything below only removes cost that has no effect.

### Change A: the creature-tree clock (10 call sites in 8 methods, 5 writer/reader pairs)

`DateTime.Now` converts UTC to local time on every call. `DateTime.UtcNow` gives identical intervals
(`DateTime` subtraction ignores `Kind`), and is immune to a daylight-saving step, which today makes a
`Now`-based interval jump by an hour. The stamps are written by one class and compared by another, so a
writer and its reader must change together; a mixed pair would be off by the UTC offset.

| Pair | Writer | Reader |
|---|---|---|
| Sleep | `Main/BehaviorTreeWrapper/Tasks/SleepTask.cs:30` `_lastTime = DateTime.Now;` | same file `:22` `if (DateTime.Now - _lastTime > _sleepDuration)` |
| Wait | `Main/BehaviorTreeWrapper/Decorators/WaitNSecondsTickDecorator.cs:21` `_lastTime = DateTime.Now;` | same file `:24` `if (DateTime.Now - _lastTime > _waitSeconds)` |
| Elephant-like cooldown | `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs:56` `StampCooldown(DateTime.Now);` | `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackOffCooldownDecorator.cs:37` `return _service.IsOffCooldown(stamp.GetValue(), DateTime.Now, _cooldownSeconds);` |
| Spider cooldown | `Main/Features/Spider/BehaviorTreeElements/SpiderAttackTaskBase.cs:47` `StampCooldown(DateTime.Now);` | `Main/Features/Spider/BehaviorTreeElements/SpiderAttackOffCooldownDecorator.cs:36` `return _service.IsOffCooldown(stamp.GetValue(), DateTime.Now, _cooldownSeconds);` |
| Warg rage timer | `Main/Features/Warg/BehaviorTreeElements/SetRageAttackTimer.cs:21` `RageAttackStartTime.SetValue(DateTime.Now);` | `Main/Features/Warg/BehaviorTreeElements/WargCanNotFindEnemyDecorator.cs:29` `if (DateTime.Now - RageAttackStartTime.GetValue() > waitSeconds)` |

The brief named only the first three readers; the cooldown and rage stamps are written elsewhere, so the
writers and the two further pairs are in scope too. Two doc comments name the clock and change with it:
`ElephantLikeAttackTasks.cs:42` and `SpiderAttackTaskBase.cs:39`, both
`/// <summary>Stamp this ... cooldown (write DateTime.Now into the matching blackboard value).</summary>`.
No other code reads these blackboard values against a clock:
`git grep -n -i "rageAttackStartTime\.\(Get\|Set\)Value\|LastFired\.\(Get\|Set\)Value" -- Main` prints only
the four `StampCooldown` overrides (`ElephantLikeAttackTasks.cs:117, 133`, `SpiderAttackTaskBase.cs:70, 79`),
`CleanIfEnemyDied.cs:29` and `SetRageAttackTimer.cs:39` (both `SetValue(null)`), the rage pair above, and
the troll's `BruteForceReadyDecorator.cs:34` / `BruteForceTask.cs:74`, which use `float` mission time and are
untouched; the two cooldown decorators read their stamp through a local (`var stamp = ...; stamp.GetValue()`).
The other nodes only declare the blackboard properties. The services' `IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds)`
(`ElephantLikeAttackService.cs:47`, `SpiderAttackService.cs:206`) are pure and unchanged; their tests use
fixed dates. `Main/Core/Logging/FileLogger.cs`, `EngineMemoryStatsReader.cs` and `InputStateDumpCheats.cs`
also call `DateTime.Now`, for human-readable timestamps: out of scope.

### Change B: `Selector.Prepare` allocates two lists per re-entry

`Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs:131-136` (the start of `Prepare`, which runs to `:155`):

```csharp
    private bool Prepare()
    {
        alreadyExecutedNodes = 0;
        currentlyExecutableChildren = new List<BTNode>();
        childrenWithTasks = new List<BTNode>();
        foreach (var child in allChildren)
        {
```

`currentlyExecutableChildren` is a `protected List<BTNode>` field of `BTControlNode` (`:34`, initialised
there); `childrenWithTasks` is a `protected List<BTNode>` field of `Selector` (`:124`, initialised there).
Nothing outside `Selector` reads either (`Sequence` never touches them; no class derives from `Selector`:
check with `git grep -n ": Selector\b" -- Main`, which must print nothing). Every use after `Prepare` is
`Add`, `Remove`, `First()` (no allocation on a `List`) or a `foreach`, all inside `Selector`, so clearing
the existing lists gives the same contents as fresh ones.

### Change C: the event dispatcher allocates and defers with no listener

`Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs` routes 14 engine callbacks to tree listeners.
Listeners subscribe per `SubscriptionPossibilities` value (`Main/BehaviorTreeWrapper/SubscriptionPossibilities.cs`,
20 values). `:64-77`:

```csharp
    public void Subscribe(BannerlordBTListener listener)
    {
        if (!actions.TryGetValue(listener.SubscribesTo, out List<BannerlordBTListener> value))
        {
            value = new List<BannerlordBTListener>();
            actions[listener.SubscribesTo] = value;
        }
        value.Add(listener);
    }

    public void UnSubscribe(BannerlordBTListener listener)
    {
        actions[listener.SubscribesTo].Remove(listener);
    }
```

`:281-298` (every removal in every battle allocates `selfRemovedArgs`, boxing `AgentState` and the
`KillingBlow` struct, before it knows whether anyone listens):

```csharp
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentRemoved"))
        {
            Defer((affectedAgent, affectorAgent, agentState, blow),
                t => OnAgentRemoved(t.affectedAgent, t.affectorAgent, t.agentState, t.blow));
            return;
        }
        var selfRemovedArgs = new object[] { affectorAgent, agentState, blow };
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfRemoved), selfRemovedArgs);
        if (affectorAgent != null)
        {
            var killedArgs = new object[] { affectedAgent, agentState, blow };
            NotifyAll(FindCalledListeners(affectorAgent, SubscriptionPossibilities.OnSelfKilledEnemy), killedArgs);
        }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentRemoved, out var globalListeners) && globalListeners != null)
            NotifyAll(globalListeners, new object[] { affectedAgent, affectorAgent, agentState, blow });
    }
```

`:300-310` (the off-thread path parks a closure even though nothing can listen):

```csharp
    public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentShootMissile"))
        {
            Defer((shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex),
                t => OnAgentShootMissile(t.shooterAgent, t.weaponIndex, t.position, t.velocity, t.orientation, t.hasRigidBody, t.forcedMissileIndex));
            return;
        }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentShootMissile, out var listeners) && listeners != null)
            NotifyAll(listeners, new object[] { shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex });
    }
```

`:248-255` `OnAgentFleeing` also allocates `selfArgs` before looking. The other overrides (`:239-379`)
follow the same shape: `if (OffMainThread("<site>")) { Defer(...); return; }`, then a
`FindCalledListeners` / `actions.TryGetValue` lookup, with argument arrays built only when a list is
non-empty. `OffMainThread` (`:167-172`) calls `MissionThreadGuard.NoteCall`, which counts the call and
reports the site once per process; keep it as the first statement of every override so that report still
fires. `OnEndMissionInternal` (`:382-396`) clears the listener state.

Which values TAOM code ever subscribes (`git grep -n "SubscriptionPossibilities\.\w*" -- Main` outside the
two dispatcher files): `OnSelfRemoved` (`OnWargDied`, `OnSpiderDied`, constant listeners subscribed when the
tree is built), `OnSelfIsHit` (`WargTryToGoRage`, a constant listener) and `OnAgentRemoved`
(`WargEnemyDiedDecorator`, an event decorator that subscribes while its node waits). Nothing subscribes
`OnAgentShootMissile`, the focus, mount, dismount, fleeing, panic, alarm, object-use or object-disabled
values. `BannerlordBTListener.SubscribesTo` has a public setter (`:7`) but is assigned only in its
constructor (`Main/BehaviorTreeWrapper/AbstractDecoratorsListeners/BannerlordBTListener.cs:12`); the count
below relies on that.

The frame order that bounds the off-thread window (`docs/reference/engine/mission-frame-threads-and-native-costs.md`
section 1, TAOM-verified on v1.5.3): every behaviour's `OnMissionTick` runs on the main thread, then the
asynchronous agent tick runs; callbacks raised off the main thread come from that agent tick and are
replayed by `_deferred.Drain()` at the top of the next `BehaviorTreeMissionLogic.OnMissionTick`.

### Change D: the always-on SpatialGrid

`Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs:16` `private const float GridUpdateInterval = 2f;`
and `:29-48`: every mission tick applies parked removals and ticks bone checks; every 2 s it calls
`SpatialGrid.Instance.UpdateGrid(Mission.Current.AllAgents)` and then `_debugService.RenderDebugVisualization()`.
`AdvancedCombatBehavior` is added to every mission TAOM equips (`Main/SubModule.cs:2051`), and its
constructor sets `SpatialGrid.Instance = new();` (`:22`), so each mission starts with a fresh grid.
`WargMissionBehavior` also calls `UpdateGrid` every 0.1 s, but only when `AdvancedCombatBehavior` is absent
(`Main/Features/Warg/WargMissionBehavior.cs:58-62, 89-99`). Do not change either caller.

`Main/Features/AdvancedCombat/SpatialGrid.cs:38-44, 50-67, 70-87, 102-106` (the `///` doc comments
between those members are left out here):

```csharp
    public void UpdateGrid(List<Agent> agents)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.UpdateGrid", ReportOffThread);
        // A fresh map, published by one reference write: a reader that still holds the old one walks a
        // finished structure rather than a map being cleared under it.
        _grid = BuildCells(agents, IsLiveAgent, AgentPosition, CellSize);
    }

    public void Remove(Agent agent)
    {
        if (agent == null) return;
        _pendingRemovals.RunOrDefer("SpatialGrid.Remove", () => RemoveNow(agent));
    }

    public void ApplyPendingRemovals() => _pendingRemovals.Drain();

    internal int PendingRemovalCount => _pendingRemovals.Count;

    private void RemoveNow(Agent agent)
    {
        foreach (List<Agent> cell in _grid.Values)
        {
            if (cell.Remove(agent)) return;
        }
    }

    internal static Dictionary<(int, int), List<T>> BuildCells<T>(List<T> items, Func<T, bool> include, Func<T, Vec3> positionOf, float cellSize)
    {
        var cells = new Dictionary<(int, int), List<T>>();
        foreach (T item in items)
        {
            if (!include(item))
                continue;
            Vec3 pos = positionOf(item);
            var key = ((int)Math.Floor(pos.x / cellSize), (int)Math.Floor(pos.y / cellSize));
            if (!cells.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                cells[key] = list;
            }
            list.Add(item);
        }
        return cells;
    }

    public void GetAgentsInRadius(Vec3 center, float radius, List<Agent> buffer)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.GetAgentsInRadius", ReportOffThread);
        CollectInRadius(_grid, center, radius, CellSize, AgentPosition, buffer);
    }
```

Every query path funnels into `GetAgentsInRadius(Vec3, float, List<Agent>)`: the allocating
`GetAgentsInRadius(Vec3, float)` and the three `GetNearAliveAgentsInRange` overloads (`:89-94, 148-162`) all
delegate to it. Nothing else reads `_grid` except `RemoveNow`. Who queries the grid
(`git grep -n "SpatialGrid" -- Main`): the warg tree (`NoEnemyCloseDecorator.cs:20`,
`PeriodicallyCheckIfCanAttackAnyone.cs:32, 74`), the spider engage gate (`SpiderEngageDecorator.cs:60`, also
used by creature bandits), `AgentAdapter.CustomAttack` (`Main/Adapters/AgentAdapter.cs:205`, warg bites) and
`AgentAdapter.RadialStrike` (`:253`, spider strikes), and the debug overlay. The elephant-like creatures,
trolls, elk and mumakil use `Mission.GetNearbyAgents` instead. So "who holds a creature" is the wrong
gate: the warg does not use `CreatureTreeTracker` at all. This plan gates on what actually matters, whether
anything queried the last build.

`Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs:11-23`:

```csharp
    public void RenderDebugVisualization()
    {
        if (Agent.Main == null || !Input.IsKeyDown(InputKey.LeftAlt))
            return;

        List<Agent> nearbyAllAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(20f, Agent.Main);
```

The class comment (`SpatialGrid.cs:9-16`) records why the rebuild publishes a new map by one reference
write instead of clearing in place: the third player freeze of 2026-09-13 had warg trees reading the grid
from the asynchronous agent tick while it was rebuilt (#592, #595). All readers are on the mission tick now,
and `MissionThreadGuard.NoteCall` reports any that comes back. This plan keeps the property with two maps:
it fills the spare one and publishes it by one reference write, so the map a reader holds is never cleared
until a later rebuild.

Engine facts: `Mission.AllAgents` is `AgentReadOnlyList` (`public AgentReadOnlyList AllAgents => _allAgents;`,
v1.5.3 `Mission.cs:1392`), and `AgentReadOnlyList : MBReadOnlyList<Agent>`, `MBReadOnlyList<T> : List<T>`:
the `List<Agent>` `UpdateGrid` receives is the mission's live list. The engine removes a deleted agent from
it after every behaviour's `OnAgentDeleted` (`Mission.OnAgentDeleted`, v1.5.3 `Mission.cs:2987-3000`).
`Agent` (sealed, derives `DotNetObject`) does not override `Equals`; its `GetHashCode` returns
`_creationIndex` (`Agent.cs:5026-5029`), unique per created agent, so a `Dictionary<Agent, ...>` keys by
object, the same equality `List<Agent>.Remove` uses today.

### Change E: the creature-bandit hunt walks every hostile agent

`Main/Features/CreatureBandits/BehaviorTreeElements/CreatureHuntTask.cs:73-97`, run every 250 ms per
creature (`SleepTask` 250 ms after the hunt task, `CreatureBanditBehaviorTree.cs:63-66`):

```csharp
    /// <summary>The nearest active humanoid on a team hostile to the creature's; mounts are never hunted.</summary>
    private static Agent? FindNearestEnemy(Mission mission, Agent creature)
    {
        Team team = creature.Team;
        if (team == null) return null;

        Vec3 origin = creature.Position;
        Agent? best = null;
        float bestDistanceSquared = float.MaxValue;
        foreach (Team other in mission.Teams)
        {
            if (other == team || !other.IsEnemyOf(team)) continue;
            foreach (Agent agent in other.ActiveAgents)
            {
                if (!agent.IsHuman || !agent.IsActive()) continue;
                float distanceSquared = agent.Position.DistanceSquared(origin);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    best = agent;
                }
            }
        }
        return best;
    }
```

The class summary (`:14-29`) says "The search covers the whole mission, not a radius" (the June spider
wandered because a 16 m gate saw no enemy, `rca-spider-troop-2026-06-04.md:63`). That stays true: the new
search still returns the nearest enemy in the whole mission; it only asks the engine first.

Engine facts the equivalence rests on, all re-derived for this plan on the installed v1.5.3 client
(`TaleWorlds.Native.dll`, 14,209,376 bytes) and its managed decompile:

- `Mission.GetNearbyAgents(Vec2 center, float radius, MBList<Agent> agents)` clears `agents` and calls
  `GetNearbyAgentsAux(center, radius, MBTeam.InvalidTeam, GetNearbyAgentsAuxType.All, agents)`
  (v1.5.3 `Mission.cs:6660-6664`), which pages 40 ids at a time under a static lock and adds
  `DotNetObject.GetManagedObjectWithId(id) as Agent` for each (`:2330-2352`), so an entry can be null.
- Native `IMBMission.GetNearbyAgentsAux` (`python tools/native_decompile.py --engine-method IMBMission.GetNearbyAgentsAux`,
  RVA `0x6F70E0`): for a radius **above 15.0** (`if (DAT_180b2e010 < param_3)`; the float at RVA
  `0xB2E010` is 15.0 per `docs/reference/engine/mission-frame-threads-and-native-costs.md` section 4) it walks
  every used agent slot and keeps one when `(flags & 0x800) != 0` (`AgentFlag.IsHumanoid`), state `== 1`
  (`AgentState.Active`) and `dx * dx + dy * dy <= radius * radius` (inclusive, horizontal only), reading the
  position at `*(slot agent + 0x20) + 0xc` (x) and `+ 0x10` (y). For type `All` (3) it skips the team test.
  At 15.0 or below it walks the engine's proximity grid instead, whose cell traversal pre-filters on
  positions stored in the grid entries: that path is NOT proven current, so this plan never uses a radius
  of 15 or below.
- Native `IMBAgent.GetPosition` (RVA `0x6E0F90`), which `Agent.Position` calls, returns the three floats at
  `*(agent + 0x20) + 0xc`: the same field the slot walk tests. `Agent.IsHuman` is
  `(GetAgentFlags() & AgentFlag.IsHumanoid) != 0` (`Agent.cs:642`); `Agent.IsActive()` is
  `State == AgentState.Active` (`Agent.cs:3328-3331`), and `State` reads the native state field
  (`Agent.cs:1563-1576`).
- `Team.ActiveAgents` holds exactly the team's agents that have not been removed: `AddAgentToTeam` adds to
  it (`Team.cs:655-659`, called from `Agent.SetTeam`), `Mission.OnAgentRemoved` sets the removed state and
  calls `Team.DeactivateAgent` in the same callback (`Mission.cs:3005-3014`), and `SetTeam` moves an agent
  between teams. So "in a hostile team's `ActiveAgents`, human, active" is the same set as "human, active,
  `Team` hostile".
- `Vec3.DistanceSquared(Vec3 v)` is `(v.x - x)^2 + (v.y - y)^2 + (v.z - z)^2` in float (`Vec3.cs:571-574`).

The proof the new search returns the full scan's target: the query for radius `r > 15` holds every active
humanoid whose horizontal distance is at most `r`, read from the same position `Agent.Position` reads. Any
eligible agent the query did not return is more than `r` away horizontally, hence more than `r` away in 3D.
So if the nearest eligible agent in the result is within `r - margin` in 3D, nothing outside the result can
be nearer, and it is the global nearest. The margin (0.5 m) absorbs float rounding between the native
horizontal test and the managed 3D distance. If no eligible agent in the result is that close, the search
tries the next radius, then falls back to today's full scan. The only possible difference is an exact float
tie between two agents' squared distances, where the full scan keeps the first in team order and the query
keeps the first in slot order; both are "the nearest".

The cost this trades, which the commit body must state: both radii are above 15 m on purpose, so each query
is a native walk over every used agent slot under a static lock, plus a managed lookup for every active
humanoid of ANY team within the radius (type `All` skips the team test). Once an enemy is within about 19.5 m
the hunt pays one such walk instead of up to three native reads (`IsHuman`, `IsActive()`, `Position`) per
hostile agent. While no enemy is within about
59.5 m (before contact, the usual state of a creature bandit at battle start) it pays two walks AND today's
full scan, more than today. The win is argued from the call counts, not measured; plan 028's profiler is
what can measure it.

### Excluded at planning: the howdah skeleton (record for the maintainer, do not implement)

`Main/Features/Elephant/TaomHowdahMachine.cs:268-278` reads the elephant's spine bone every frame for every
crewed howdah (`OnTick` -> `RepositionToElephant` `:244` -> `RepositionToFixedOffset` `:346`, `:357` ->
`TryReadAnchorBoneWorld`):

```csharp
    private bool TryReadAnchorBoneWorld(out MatrixFrame world)
    {
        world = default;
        try
        {
            var visuals = elephantAgent?.AgentVisuals;
            Skeleton skel = visuals?.GetSkeleton();
```

`MBAgentVisuals.GetSkeleton()` builds a new `Skeleton` wrapper (a finalizable `NativeObject`) on every call
(managed `ScriptingInterfaceOfIMBAgentVisuals.GetSkeleton`, v1.5.3 decompile line 776). The brief allowed two
fixes, each only if provably equivalent, and neither is:

- **Cache the wrapper per elephant.** Native `IMBAgentVisuals.GetSkeleton` (RVA `0x6E88D0`) returns whatever
  skeleton is stored at `*(visuals + 0x8b8) + 0x298` at call time, and that field is writable at runtime:
  native `IMBAgentVisuals.SetSkeleton` (RVA `0x6E75C0`) replaces it, managed `MBAgentVisuals.SetSkeleton`
  is public and is called by `View.AgentVisuals.Refresh` when the skeleton type changes
  (`TaleWorlds.MountAndBlade.View.AgentVisuals.cs:306`), and `GameEntity.Skeleton` has a public setter. A
  cached wrapper would keep reading a replaced skeleton; no managed or native evidence proves that never
  happens to an elephant in a battle.
- **Read the bone every N frames.** The platform follows the spine's height to cancel a 0.18 m bob
  (comment at `:348-355`); updating it every N frames makes it step, a visible difference.
- A third route was checked and rejected: `MBAgentVisuals.GetBoneEntitialFrame` and `Agent.GetBoneEntitialFrame`
  read without a wrapper, but their native bodies (RVAs `0x6E8C20` and `0x6E4120`, both registered as
  `get_quick_bone_entitial_frame`) skip the lazy frame update that `ISkeleton.GetBoneEntitialFrameWithIndex`
  (RVA `0x50B7E0`) performs when the skeleton's frames are stale, and do no bounds check.

`Main/Features/Mumakil/TaomMumakilPlatform.cs:278` also calls `GetSkeleton()`, but not per frame: it is in
`BoneProbe()` (`:273`), which only `LogStatus()` calls (`:258`), and `OnTick` calls `LogStatus()` only when
the status clock fires and diagnostics are enabled (`:93`). It is a diagnostics probe, not a battle cost, and is
left alone. The howdah fetch is reported to the orchestrator (see "Orchestrator steps"), not worked around.

### Conventions that bind this change

- **ADR-002** (thin entry points under 150 lines): no entry point grows logic; `BehaviorTreeMissionLogic`,
  `AdvancedCombatBehavior` and the tree nodes are boundary code and stay thin.
- **ADR-007** (adapters for sealed types): no new service is introduced. The pure helpers this plan adds are
  `internal static` generic methods over `T` and `Vec3` (the pattern `SpatialGrid.BuildCells` /
  `CollectInRadius` already use, plan 015), so tests drive them on plain points without an `Agent`.
- **ADR-008** (test coverage): every pure helper gets behaviour tests; engine-bound glue gets an IL rule or
  is named in the commit's `Not-tested:` trailer.
- **`.claude/rules/csharp-architecture.md`, "Engine-Float Decision Gates"**: a new gate on an engine float
  is written as a positive requirement so NaN fails it, and gets a NaN test in the same commit.
- **Same file, "Mission-scope agent handles and the engine's threads"**: no engine callback is main-thread
  by contract; a write to a main-thread collection from a callback goes through `DeferredCallbackQueue.RunOrDefer`;
  `MissionThreadGuard.NoteCall` is the tripwire and stays first in every callback.
- **`.claude/rules/tests.md`**: MSTest, names `MethodName_StateUnderTest_ExpectedBehavior`, AAA. A test that
  constructs or calls an engine type (`Agent`, `Vec3`, a `MissionLogic` subclass) carries
  `[TestCategory("RequiresGame")]`, as `TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs` does,
  or it fails hosted CI, which builds against metadata-only reference assemblies.
- **`.claude/rules/simplicity-criterion.md`**: each change below states its win and cost in its commit body.

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`, after `refresh --if-stale`)

The graph reported "current (built 2026-10-02 20:46 from e9cd8b39)"; its C# call edges are known to be
incomplete, so the `git grep` lines in this section are the authority.

- `Selector`, `SleepTask`, `SpatialGridDebugService`, `CreatureHuntTask`, `ElephantLikeAttackOffCooldownDecorator`,
  `SpiderAttackOffCooldownDecorator`, `SetRageAttackTimer`, `WargCanNotFindEnemyDecorator`: "No affected nodes found."
- `WaitNSecondsTickDecorator`: `PeriodicallyCheckIfCanAttackAnyone [inherits]` (`Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs:12`).
- `BehaviorTreeMissionLogic`: `BehaviorTreeBannerlordWrapper [references]`.
- `SpatialGrid`: the twelve `SpatialGridQueryTests` (9) / `SpatialGridRemovalTests` (3) methods (all call
  the helpers or `Remove`, whose signatures this plan keeps).
- `ElephantLikeAttackTaskBase`: `ElephantLikeSideAttackTask`, `ElephantLikeTrampleTask` [inherits].
- `SpiderAttackTaskBase`: `SpiderPounceTask`, `SpiderSideAttackTask` [inherits].

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests, the same one failure |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Hosted-CI shape (RefAsm) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` then `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | the same failing set as the base's RefAsm run (Step 1) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |

Both MSBuild flags go on build AND test; prefix every dotnet command with the TEMP and TMP your dispatch
rules give. Never `./build.ps1`: it deploys into the game install. A RefAsm build overwrites the normal
test build output: run the normal build and test again afterwards before quoting normal totals. Per
`.ai/verification.md`, a RefAsm run on a machine with the game is partial evidence; record it as such. If
the RefAsm restore cannot download its reference packages, that is an environment failure: record "RefAsm
not run" with the exact error and go on (it does not replace the normal suite, which stays mandatory).

## Scope

**In scope** (the only files you modify or create):

- `Main/BehaviorTreeWrapper/Tasks/SleepTask.cs`
- `Main/BehaviorTreeWrapper/Decorators/WaitNSecondsTickDecorator.cs`
- `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackOffCooldownDecorator.cs`
- `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs`
- `Main/Features/Spider/BehaviorTreeElements/SpiderAttackOffCooldownDecorator.cs`
- `Main/Features/Spider/BehaviorTreeElements/SpiderAttackTaskBase.cs`
- `Main/Features/Warg/BehaviorTreeElements/SetRageAttackTimer.cs`
- `Main/Features/Warg/BehaviorTreeElements/WargCanNotFindEnemyDecorator.cs`
- `Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs` (`Selector.Prepare` only)
- `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs`
- `Main/Features/AdvancedCombat/SpatialGrid.cs`
- `Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs`
- `Main/Features/CreatureBandits/BehaviorTreeElements/CreatureHuntTask.cs`
- New: `TAOM.Tests/BehaviorTrees/CreatureTreeClockTests.cs`, `TAOM.Tests/BehaviorTrees/SelectorListReuseTests.cs`,
  `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicDispatchTests.cs`,
  `TAOM.Tests/Features/AdvancedCombat/SpatialGridReuseTests.cs`, `TAOM.Tests/Features/AdvancedCombat/SpatialGridDormancyTests.cs`,
  `TAOM.Tests/Features/CreatureBandits/CreatureHuntNearestTests.cs`
- `TAOM.Tests/Features/AdvancedCombat/SpatialGridDebugServiceTests.cs` (one added test)
- `docs/features/advanced-combat.md`, `docs/features/creature-bandits.md` (the exact lines named in Steps 9 and 11)

`Main/TAOM.csproj` is SDK-style with no `<Compile Include` items (`grep -c "<Compile Include" Main/TAOM.csproj`
prints 0), so no project edit is needed; this plan adds no production file.

**Out of scope** (do NOT touch, even though they look related):

- Tree cadence: `BehaviorTreeAgentComponent`, `_rootEvaluationDelay`, any `base(10)`, any `SleepTask` or
  `WaitNSeconds` duration. Any creature timing.
- `AdvancedCombatBehavior.GridUpdateInterval` (still `2f`): plan 003 (#664) meant to shorten the grid's
  staleness and did not; changing it changes what wargs and spiders see, which is a gameplay decision for the
  maintainer (see "Maintenance notes").
- `CreatureTreeTracker.PruneDead` (`Main/Features/AdvancedCombat/CreatureTreeTracker.cs:114-122`): it already
  tests `IsActive()` (a direct read of the native state field) before the native `FindAgentWithIndex`
  identity check, and no cheaper equivalent check exists.
- `TaomHowdahMachine`, `TaomMumakilPlatform`, `HowdahDiagnosticsReporter` (the skeleton fetch, see above).
- `AgentAdapter.CustomAttack`'s allocating grid overload (`Main/Adapters/AgentAdapter.cs:205`): per warg bite,
  not per frame, and not in the brief.
- `SpatialGrid.Remove`'s closure (`:53`): one small allocation per deletion; removing it needs a second
  method to avoid the C# display class, a win too small for its cost.
- `BehaviorTreeMissionLogic.OnAgentDeleted` dispatching `OnSelfAlarmedStateChanged` (`:257-261`): an odd
  mapping kept as is; this plan only adds the listener check in front of it.
- Patch93 (plan 032), the diagnostics files and their defaults (plan 030), `Main/IoC.cs`,
  `Main/SubModule.cs`, `Main/TAOM.csproj`, `CHANGELOG.md`, `plans/README.md`.
- Any save-format change (none is needed).
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry. STOP and report instead.

## Git workflow

- Commit on the branch you were given (this plan's own branch); never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, where `<version>` is the
  `<Version value="...">` in `Main/_Module/SubModule.xml` when you commit (it reads `v2.0.32` at the
  planned-at commit; a hook refuses any other). Four commits, one per stage:
  1. `perf(creatures): <version> - read the UTC clock in creature trees` (Steps 2 and 3)
  2. `perf(behavior-trees): <version> - stop allocating per re-entry and event` (Steps 4 to 7)
  3. `perf(combat): <version> - reuse the spatial grid, skip unread rebuilds` (Steps 8 and 9)
  4. `perf(creature-bandits): <version> - hunt from a nearby query first` (Steps 10 and 11)
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at 72,
  with the simplicity trade-off in one sentence each (win, cost); commit 4's cost sentence is the one in
  "Change E" (before contact the hunt pays two slot walks plus today's scan). Write it to a file and run
  `git commit -F "<file>"`. Stage explicit paths only. Never edit `CHANGELOG.md`. No AI attribution
  trailer. Add `Not-tested:` for the engine-bound lines each step names.

## Steps

### Step 1: record the base

Run the full suite before any edit:
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` and write its totals line and failing tests
into your report. Then run the two RefAsm commands from "Commands you will need" and record their totals
and failing set (the base's own RefAsm failures, if any, are the oracle for Step 12). Then run the normal
build and test again so the test output is the normal one.

**Verify**: the normal totals match "Baseline at that commit" above (`Failed: 1, Passed: 12345, Skipped: 2,
Total: 12348`, failing `EveryLanguage_DeclaresARowForEveryEnglishKey`), or the difference is explained. Run
`git grep -n ": Selector\b" -- Main` (must print nothing) and
`git grep -n "SubscribesTo =" -- Main` (must print only `BannerlordBTListener.cs`'s constructor line). Either
differing is a STOP. Run `python tools/lint_docs.py --fail-on-drift` and record its exit code (0 at planning);
Steps 9, 11 and 12 require 0; if the base already exits non-zero, record its findings, and those steps then
require no finding beyond them.

### Step 2: RED, an IL rule that creature-tree code never reads `DateTime.Now`

Create `TAOM.Tests/BehaviorTrees/CreatureTreeClockTests.cs`, class `CreatureTreeClockTests`, modelled on
`TAOM.Tests/Features/Elephant/HowdahCrewLookupBanTests.cs` (same `[ClassInitialize]` with
`GameAssemblies.EnsureLoaded()` and `Assert.Inconclusive` when it returns false). Tests:

- `CreatureTreeCode_NeverReadsDateTimeNow`: call
  `IlCallScanner.FindCallers(typeof(global::BehaviorTreeWrapper.Tasks.SleepTask).Assembly, m => m.DeclaringType == typeof(DateTime) && m.Name == "get_Now", out var unreadable, out int scanned)`.
  Keep only the violations whose name starts with one of `"BehaviorTreeWrapper."`, `"BehaviorTrees."`,
  `"TAOM.Features.ElephantLike."`, `"TAOM.Features.Spider."`, `"TAOM.Features.Warg."` (violations are
  `"<DeclaringType.FullName>.<Method>"`, nested lambda types included). Assert `scanned > 0`, assert no
  `unreadable` entry starts with those prefixes, and assert the filtered list is empty, printing it. The
  message: "a creature tree's interval must use DateTime.UtcNow (no time-zone conversion per frame, no DST
  step), and a stamp's writer and reader must use the same clock".
- `ClockRule_ADirectNowRead_IsFound` (the control): a `private sealed class ReadsLocalClock { public DateTime Read() => DateTime.Now; }`
  in the test file; assert `IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray())`
  contains a method with `DeclaringType == typeof(DateTime)` and `Name == "get_Now"`.

Build the test project and run `--filter "FullyQualifiedName~CreatureTreeClockTests"`.

**Verify**: 2 tests run; `ClockRule_ADirectNowRead_IsFound` passes; `CreatureTreeCode_NeverReadsDateTimeNow`
fails and its message lists exactly these eight methods: `SleepTask.Execute`,
`WaitNSecondsTickDecorator.Evaluate`, `ElephantLikeAttackOffCooldownDecorator.Evaluate`,
`ElephantLikeAttackTaskBase.Execute`, `SpiderAttackOffCooldownDecorator.Evaluate`,
`SpiderAttackTaskBase.Execute`, `SetRageAttackTimer.Execute`, `WargCanNotFindEnemyDecorator.Evaluate`
(each prefixed with its namespace; ten call sites, because `SleepTask.Execute` and
`WaitNSecondsTickDecorator.Evaluate` each read the clock twice). A name outside that list is a STOP (an
unlisted writer or reader).

### Step 3: GREEN, switch every pair to `DateTime.UtcNow`

Replace `DateTime.Now` with `DateTime.UtcNow` at the ten call sites in the table under "Change A", and in the
two summaries (`ElephantLikeAttackTasks.cs:42`, `SpiderAttackTaskBase.cs:39`) change "write DateTime.Now"
to "write DateTime.UtcNow". Change nothing else in those files.

**Verify**: build exits 0; `--filter "FullyQualifiedName~CreatureTreeClockTests"` passes 2;
`--filter "FullyQualifiedName~Spider|FullyQualifiedName~Elephant|FullyQualifiedName~Warg|FullyQualifiedName~Mumakil|FullyQualifiedName~Elk|FullyQualifiedName~WarRam|FullyQualifiedName~Animalia|FullyQualifiedName~BehaviorTree"`
shows no failure; `git grep -n "DateTime.Now" -- Main/BehaviorTreeWrapper Main/BehaviorTrees Main/Features/ElephantLike Main/Features/Spider Main/Features/Warg`
prints nothing. Commit 1 (subject in "Git workflow"; `Not-tested:` the in-game feel of creature cooldowns,
which no test drives).

### Step 4: RED, the selector reuses its lists

Create `TAOM.Tests/BehaviorTrees/SelectorListReuseTests.cs`, class `SelectorListReuseTests` (no engine
types; no category). Fixture, modelled on `TAOM.Tests/BehaviorTrees/BehaviorTreeBuilderTests.cs`:

```csharp
    private sealed class CountTask : BTTask
    {
        public int Runs;
        public override BTTaskStatus Execute() { Runs++; return BTTaskStatus.FinishedWithTrue; }
    }

    private sealed class OneSelectorTree : global::BehaviorTrees.BehaviorTree
    {
        public CountTask Task { get; } = new CountTask();
        public OneSelectorTree() : base(10) { }

        public static OneSelectorTree Build()
        {
            var tree = new OneSelectorTree();
            StartBuildingTree(tree).AddSelector("selector").AddTask(tree.Task).Up().Finish();
            return tree;
        }
    }
```

Read the selector by reflection: `typeof(BTControlNode).GetField("allChildren", BindingFlags.NonPublic | BindingFlags.Instance)`
on `(BTControlNode)tree.RootNode` (`RootNode` is `internal`; the test assembly sees internals) gives the
root's children, element 0 is the `Selector`; `currentlyExecutableChildren` is a non-public instance field of
`BTControlNode`, `childrenWithTasks` of `Selector`. Test
`Prepare_OnReEntry_ReusesBothChildLists`: `RunTree()`, capture both field values, `RunTree()` again; assert
FIRST `Assert.AreEqual(2, tree.Task.Runs)` (each run re-enters `Prepare`, see the walk-through in
`BehaviorTreeBuilderTests.RunTree_SingleAlwaysTrueTask_ExecutesTaskAndStaysRunning`, which pins one run per
`RunTree`), THEN `Assert.AreSame` for `currentlyExecutableChildren`, and only after it for
`childrenWithTasks` (the Verify below expects the failure on the first).

**Verify**: the filtered run (`~SelectorListReuseTests`) fails on the `AreSame` for
`currentlyExecutableChildren`, not on the `Runs` assertion. If it fails on `Runs`, the oracle is wrong: STOP.

### Step 5: GREEN, clear instead of allocate

In `Selector.Prepare` replace the two assignments with `currentlyExecutableChildren.Clear();` and
`childrenWithTasks.Clear();`. Nothing else changes.

**Verify**: build exits 0; `~SelectorListReuseTests` passes 1; `~BehaviorTreeBuilderTests` and `~Warg`
show no failure.

### Step 6: RED, the dispatcher returns before allocating or parking when nothing listens

Create `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicDispatchTests.cs`, class
`BehaviorTreeMissionLogicDispatchTests`, `[TestCategory("RequiresGame")]` (it constructs a `MissionLogic`
subclass and passes engine structs). Patterns: `TAOM.Tests/Features/AdvancedCombat/SpatialGridRemovalTests.cs`
for the thread guard and the bare agent
(`(Agent)FormatterServices.GetUninitializedObject(typeof(Agent))`), `DeferredCallbackQueueTests.cs` for the
queue. `BehaviorTreeMissionLogic`'s constructor only sets `BehaviorTreeBannerlordWrapper.Instance.CurrentMissionLogic`;
`MissionLogic` and `MissionBehavior` have no constructor body (v1.5.3 decompile), so it builds in a test.
(The comment in `BehaviorTreeAgentComponentThreadingTests.cs:14` saying the logic cannot be built predates
the inlined BehaviorTrees port; do not edit that file.)

Setup and cleanup:

- `[TestInitialize]`: save `BTRegister.Logger`; `MissionThreadGuard.ResetForTests(); MissionThreadGuard.MarkMainThread();`
  `_logic = new BehaviorTreeMissionLogic();`
- `[TestCleanup]`: `_logic.OnEndMissionInternal();` (clears the maps and resets the wrapper's current logic),
  `MissionThreadGuard.ResetForTests();`, `BTRegister.AddLogger(saved)` (the wrapper's first `Instance` access
  installs an on-screen logger; restore what was there).
- Fixtures: `private sealed class StubTree : global::BehaviorTrees.BehaviorTree { public StubTree() : base(10) { } }`
  and a `RecordingNotifiable : IBTNotifiable` whose `HandleNotification(object[] data)` stores the last
  `data` and counts calls (`Listener`/`Tree` auto-properties, `CreateListener() { }`). A listener is
  `new BannerlordBTListener(<kind>, new StubTree(), notifiable)` (internal constructor), subscribed with
  `_logic.Subscribe(listener)`.
- `RunOnWorker(Action a)`: start a `Thread` running `a`, `Join` it (the off-main-thread path; the worker's
  report goes through a try/catch, so an uninitialised `IoC` is fine).
- Allocation probe: bind `typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)`
  once into a `Func<long>` (the machine's .NET Framework 4.8.1 has it; the net472 reference assemblies do not
  expose it, per `docs/reference/engine/mission-frame-threads-and-native-costs.md` section 2). If it is null,
  `Assert.Inconclusive`. Measure: call the action once to warm it up, read the counter, call it 1,000 times,
  read again.

Tests (RED today unless marked "passes today"):

1. `OnAgentShootMissile_OffThreadWithNoListener_ParksNothing`: on a worker, call
   `_logic.OnAgentShootMissile(null, EquipmentIndex.None, default, default, default, false, 0)`; assert
   `_logic.DeferredCount == 0` (today 1).
2. `OnAgentRemoved_OffThreadWithNoListener_ParksNothing`: worker calls
   `_logic.OnAgentRemoved(null, null, AgentState.Killed, default)`; assert 0 (today 1).
3. `OnAgentHit_OffThreadWithNoListener_ParksNothing`: worker calls `_logic.OnAgentHit(null, null, default(MissionWeapon), default(Blow), default(AttackCollisionData))`
   (pass the struct locals with `in`); assert 0 (today 1).
4. `OnAgentRemoved_OffThreadWithAGlobalListener_ParksTheReplay` (passes today): subscribe an
   `OnAgentRemoved` listener; worker call as in 2; assert 1.
5. `OnAgentRemoved_AfterTheLastListenerUnsubscribes_ParksNothing`: subscribe then `_logic.UnSubscribe(listener)`;
   worker call; assert 0 (today 1).
6. `OnEndMissionInternal_ForgetsTheListeners`: subscribe an `OnAgentRemoved` listener, call
   `_logic.OnEndMissionInternal()`, then the worker call; assert 0 (today 1).
7. `OnAgentRemoved_OnTheMainThreadWithAGlobalListener_NotifiesItWithTheRemovalArguments` (passes today):
   subscribe an `OnAgentRemoved` listener; call `_logic.OnAgentRemoved(null, null, AgentState.Killed, default)` on
   the test thread; assert one notification whose data is `{ null, null, AgentState.Killed, default(KillingBlow) }`
   (4 elements; compare element 2 with `AgentState.Killed`).
8. `OnAgentRemoved_OnTheMainThreadWithNoListener_AllocatesNothing`: probe
   `() => _logic.OnAgentRemoved(null, null, AgentState.Killed, default)`; assert fewer than 1,024 bytes for
   1,000 calls (today an `object[3]` plus a boxed `AgentState` and a boxed `KillingBlow` per call, far above).
9. `OnAgentRemoved_WithOnlyAnotherTreesSelfListener_AllocatesNothing`: subscribe an `OnSelfRemoved` listener
   (its tree is a `StubTree`, so it matches no agent); probe
   `() => _logic.OnAgentRemoved(bareAgent, null, AgentState.Killed, default)` with a bare agent (a null
   `affectedAgent` would reach `trees.TryGetValue(null)` and throw); assert fewer than 1,024 bytes.
10. `OnAgentFleeing_OnTheMainThreadWithNoListener_AllocatesNothing`: probe `() => _logic.OnAgentFleeing(null)`;
    assert fewer than 1,024 bytes.

**Verify**: `~BehaviorTreeMissionLogicDispatchTests` runs 10; tests 4 and 7 pass; 1, 2, 3, 5, 6 fail on
`DeferredCount` (expected 0, actual 1); 8, 9, 10 fail on the byte count (or are Inconclusive if the probe
cannot bind: then record "allocation probe unavailable" in your report; the claim stays UNVERIFIED, which
is not a STOP).

### Step 7: GREEN, listener counts and early returns

In `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs` (add `using System.Threading;`):

```csharp
    // How many listeners each SubscriptionPossibilities value has. Subscribe and UnSubscribe write it on the
    // mission thread; a callback reads it on whichever thread native raised it, so an event nobody listens
    // to returns before it allocates arguments or parks a replay (plan 033).
    private static readonly int SubscriptionKinds = Enum.GetValues(typeof(SubscriptionPossibilities)).Length;
    private readonly int[] _listenerCounts = new int[SubscriptionKinds];

    private bool Listens(SubscriptionPossibilities kind) => Volatile.Read(ref _listenerCounts[(int)kind]) > 0;
```

- `Subscribe(BannerlordBTListener)`: after `value.Add(listener);` add
  `Interlocked.Increment(ref _listenerCounts[(int)listener.SubscribesTo]);`.
- `UnSubscribe(BannerlordBTListener)`: `if (actions[listener.SubscribesTo].Remove(listener)) Interlocked.Decrement(ref _listenerCounts[(int)listener.SubscribesTo]);`
  (keep the indexer: it throws today for a never-subscribed value, and must keep doing so).
- `OnEndMissionInternal`: add `Array.Clear(_listenerCounts, 0, _listenerCounts.Length);` beside the other clears.
- Every one of the 14 overrides takes this shape, keeping its own site string, `Defer` call and body:

```csharp
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentShootMissile");
        if (!Listens(SubscriptionPossibilities.OnAgentShootMissile)) return;
        if (offThread)
        {
            Defer(/* unchanged */);
            return;
        }
        /* unchanged main-thread body */
```

  The values each override checks (return early only when NONE of its values listens):
  `OnAgentDismount`: `OnSelfDismount`, `OnAgentDismount`. `OnAgentFleeing`: `OnSelfFleeing`, `OnAgentFleeing`.
  `OnAgentDeleted`: `OnSelfAlarmedStateChanged` (the value it dispatches today). `OnAgentMount`: `OnSelfMount`,
  `OnAgentMount`. `OnAgentPanicked`: `OnAgentPanicked`. `OnAgentRemoved`: `OnSelfRemoved`, `OnSelfKilledEnemy`,
  `OnAgentRemoved`. `OnAgentShootMissile`: `OnAgentShootMissile`. `OnFocusGained`: `OnSelfGainedFocus`.
  `OnFocusLost`: `OnSelfLostFocus`. `OnAgentAlarmedStateChanged`: `OnSelfAlarmedStateChanged`. `OnAgentHit`:
  `OnSelfIsHit`, `OnSelfHitsEnemy`. `OnObjectUsed`: `OnSelfUsedObject`. `OnObjectDisabled`: `OnObjectDisabled`.
  `OnObjectStoppedBeingUsed`: `OnSelfStoppedUsingObject`. Cross-check each against the values the override's
  body passes to `FindCalledListeners` / `actions.TryGetValue`: they must match exactly.
- `OnAgentRemoved`'s main-thread body allocates only for a non-empty list:

```csharp
        var selfRemoved = FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfRemoved);
        if (selfRemoved.Count > 0)
            NotifyAll(selfRemoved, new object[] { affectorAgent, agentState, blow });
        if (affectorAgent != null)
        {
            var killedEnemy = FindCalledListeners(affectorAgent, SubscriptionPossibilities.OnSelfKilledEnemy);
            if (killedEnemy.Count > 0)
                NotifyAll(killedEnemy, new object[] { affectedAgent, agentState, blow });
        }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentRemoved, out var globalListeners) && globalListeners != null && globalListeners.Count > 0)
            NotifyAll(globalListeners, new object[] { affectedAgent, affectorAgent, agentState, blow });
```

- `OnAgentFleeing`'s body keeps one shared array, built on first need:
  `object[]? args = null;` then `if (self.Count > 0) NotifyAll(self, args = new object[] { affectedAgent });`
  and for the global list `if (... && globalListeners.Count > 0) NotifyAll(globalListeners, args ?? new object[] { affectedAgent });`.

Why this is equivalent: on the main thread, a value with no listener produced an empty list and
`NotifyAll` did nothing, so returning first changes only the allocation. Off the main thread, the replay
used the listeners present at the next drain; now an event raised while none of its values has a listener
is dropped, exactly as the main-thread path drops it. The two differ only if a first listener for that
value subscribes between the asynchronous agent tick and the next drain. For the values TAOM subscribes,
that is a constant listener attached in the frame a creature's tree is built (the warg attaches its trees
from its own `OnMissionTick`, which ticks before the tree logic's drain), receiving an event raised for its
own agent before that tree existed. State this window in the commit body.

**Verify**: build exits 0; `~BehaviorTreeMissionLogicDispatchTests` passes 10 (or 7 plus 3 Inconclusive,
recorded); `~BehaviorTreeAgentComponentThreadingTests`, `~DeferredCallbackQueueTests`,
`~BehaviorTreeMissionLogicInheritanceTests`, `~Warg`, `~Spider` show no failure. Commit 2 (Steps 4 to 7;
`Not-tested:` the in-game delivery order of an off-thread event in the frame a first creature tree is built).

### Step 8: RED, the grid reuses its storage, removes in O(cell) and skips unread rebuilds

Two parts, in this order: first the debug-service IL rule, which compiles today and is RED by assertion;
then the two new grid test files, which are RED by compile failure because they use members Step 9 adds.

**8a, the debug-service rule (add it and see it fail before writing anything else).** In
`TAOM.Tests/Features/AdvancedCombat/SpatialGridDebugServiceTests.cs` add the usings `System.Collections.Generic`,
`System.Linq`, `System.Reflection`, `TaleWorlds.MountAndBlade`, `TAOM.Features.AdvancedCombat` and
`TAOM.Tests.Migration` (where `IlCallScanner` lives), and this test. It is the rule of
`WargTickCostTests.AssertScansIntoABuffer` (`TAOM.Tests/Features/Warg/WargTickCostTests.cs:88-99`), written
out here because that helper is private and reads only `Evaluate`:

```csharp
    [TestMethod]
    public void RenderDebugVisualization_ScansIntoAReusedBuffer()
    {
        MethodInfo method = typeof(SpatialGridDebugService).GetMethod(nameof(SpatialGridDebugService.RenderDebugVisualization));
        List<MethodBase> calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();
        List<MethodBase> scans = calls
            .Where(m => m.DeclaringType == typeof(SpatialGrid) && m.Name == nameof(SpatialGrid.GetNearAliveAgentsInRange))
            .ToList();
        // ExtractCalledMethods skips a token it cannot resolve, so an empty list must fail, never pass.
        Assert.IsTrue(scans.Count > 0, "RenderDebugVisualization no longer scans the grid; this test needs re-planning");
        Assert.IsTrue(scans.All(m => m.GetParameters().Length == 3),
            "RenderDebugVisualization calls an allocating GetNearAliveAgentsInRange overload; pass a reused List<Agent> buffer");
        Assert.IsFalse(calls.Any(m => m is ConstructorInfo && m.DeclaringType == typeof(List<Agent>)),
            "RenderDebugVisualization constructs a List<Agent> on every call; scan into a readonly instance buffer");
    }
```

It carries no category; if Step 12's RefAsm run fails it, tag it `[TestCategory("RequiresGame")]` (it is a
new test, so tagging it is not loosening a gate) and say so in your report. Build `TAOM.Tests` and run
`--filter "FullyQualifiedName~SpatialGridDebugServiceTests"`.

**Verify 8a**: 3 tests run; the two existing ones pass; `RenderDebugVisualization_ScansIntoAReusedBuffer`
fails on the second assertion (the "allocating GetNearAliveAgentsInRange overload" message: today's call
`GetNearAliveAgentsInRange(20f, Agent.Main)` has 2 parameters). Quote the failure. A failure on the first
assertion (no scan found) means the IL could not be resolved here: STOP.

**8b, the grid tests.** Both files are `[TestCategory("RequiresGame")]` (they use `Vec3` or construct
`SpatialGrid`).

`TAOM.Tests/Features/AdvancedCombat/SpatialGridReuseTests.cs`, class `SpatialGridReuseTests`, with the
`Point` fixture copied from `SpatialGridQueryTests.cs:21-29` (a class with a settable `Vec3 P`):

- `BuildCellsInto_ReusedContainers_MatchAFreshBuild`: seeded `Random(33)`, 400 points over x, y in
  [-150, 150]; build once into `cells`, `cellOf`, `spare` with `SpatialGrid.BuildCellsInto`; move every
  point by a random offset up to 30 m; build again into the SAME three containers; compare with
  `SpatialGrid.BuildCells` on the moved points: the same key set, and for each key the same items in the
  same order (`CollectionAssert.AreEqual`).
- `BuildCellsInto_SecondBuild_TakesItsListsFromTheSpareStack`: build, record the cell list instances, build
  again with every point in the same cells; assert every list in the second build is one of the first
  build's instances (`ReferenceEquals`), and that no list is shared by two keys.
- `RemoveFromCells_MatchesTheOldWalkOverEveryCell`: build two copies of the same layout (one with
  `BuildCells` for the oracle, one with `BuildCellsInto`); remove 100 random points (and 10 points that were
  never added, and one point twice) from both: the oracle by today's walk
  (`foreach (var cell in oracle.Values) if (cell.Remove(p)) break;`), the other with
  `SpatialGrid.RemoveFromCells(cells, cellOf, p)`; assert both maps hold the same items per key in the same
  order, and that `RemoveFromCells` returned false exactly for the never-added points and the second removal.
- `BuildCellsInto_ExcludedItems_HaveNoCellEntry`: an excluded item is in neither `cells` nor `cellOf`.

`TAOM.Tests/Features/AdvancedCombat/SpatialGridDormancyTests.cs`, class `SpatialGridDormancyTests`
(`[TestInitialize]`/`[TestCleanup]` as in `SpatialGridRemovalTests`; every `UpdateGrid` gets an EMPTY
`List<Agent>`, so no native position is read; `BuildCount` is the internal counter Step 9 adds):

- `UpdateGrid_FirstCall_Builds`: one call; `BuildCount == 1`.
- `UpdateGrid_AfterABuildNobodyQueried_SkipsTheRebuild`: two calls; `BuildCount == 1`.
- `GetAgentsInRadius_OnASkippedGrid_RebuildsBeforeAnswering`: two calls, then
  `grid.GetAgentsInRadius(new Vec3(0f, 0f, 0f), 10f, buffer)`; `BuildCount == 2`.
- `UpdateGrid_AfterAQueriedBuild_Rebuilds`: call, query, call; `BuildCount == 2`.
- `GetAgentsInRadius_OffTheMainThreadOnASkippedGrid_DoesNotRebuild`: two calls, then the query on a worker
  thread; `BuildCount == 1` (only the mission thread may build).
- `GetAgentsInRadius_BeforeAnyUpdate_DoesNotBuild`: a query on a new grid; `BuildCount == 0` (the first
  2 s of a mission still answer from the empty grid, as today).

**Verify 8b**: building `TAOM.Tests` fails, and every error is one of these: CS0117 ("'SpatialGrid' does not
contain a definition for ...") naming `BuildCellsInto` or `RemoveFromCells` (static calls on the type), or
CS1061 ("... does not contain a definition for 'BuildCount' and no accessible extension method ...") naming
`BuildCount` (read on a `SpatialGrid` instance). Each of the three names appears at least once. Any other
diagnostic is your error to fix before going on.

### Step 9: GREEN, the grid change and its doc

`Main/Features/AdvancedCombat/SpatialGrid.cs`, target shape (keep `CellSize`, `Instance`, the reporters,
`Remove`, `ApplyPendingRemovals`, `PendingRemovalCount`, `CollectInRadius` and every public overload as they
are):

```csharp
    // Two maps: a rebuild fills the spare one and publishes it by one reference write, so the map a reader
    // holds is never cleared under it until a later rebuild. Each map has its agent-to-cell index, which
    // makes a removal one cell's List.Remove instead of a walk over every cell.
    private Dictionary<(int, int), List<Agent>> _grid = new();
    private Dictionary<Agent, (int, int)> _cellOf = new();
    private Dictionary<(int, int), List<Agent>> _spareGrid = new();
    private Dictionary<Agent, (int, int)> _spareCellOf = new();
    private readonly Stack<List<Agent>> _spareLists = new();

    // The list the last UpdateGrid passed (the mission's live AllAgents), whether anything queried the last
    // build, and whether a scheduled rebuild was skipped because nothing did.
    private List<Agent>? _agents;
    private bool _queriedSinceBuild;
    private bool _skippedRebuild;

    /// <summary>Builds made so far; tests read it.</summary>
    internal int BuildCount { get; private set; }

    public void UpdateGrid(List<Agent> agents)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.UpdateGrid", ReportOffThread);
        _agents = agents;
        // Nothing read the last build: skip this one. The next query rebuilds first, so a reader that
        // arrives later (a creature spawned mid-battle) never sees cells older than a rebuild would give it.
        if (BuildCount > 0 && !_queriedSinceBuild)
        {
            _skippedRebuild = true;
            return;
        }
        Rebuild(agents);
    }

    private void Rebuild(List<Agent> agents)
    {
        BuildCellsInto(_spareGrid, _spareCellOf, _spareLists, agents, IsLiveAgent, AgentPosition, CellSize);
        (_grid, _spareGrid) = (_spareGrid, _grid);
        (_cellOf, _spareCellOf) = (_spareCellOf, _cellOf);
        _queriedSinceBuild = false;
        _skippedRebuild = false;
        BuildCount++;
    }

    private void RemoveNow(Agent agent) => RemoveFromCells(_grid, _cellOf, agent);

    /// <summary>Buckets every included item by the cell of its position into <paramref name="cells"/> and
    /// records each item's cell in <paramref name="cellOf"/>, after returning the previous build's lists to
    /// <paramref name="spareLists"/>. Pure; tests drive it with plain points.</summary>
    internal static void BuildCellsInto<T>(Dictionary<(int, int), List<T>> cells, Dictionary<T, (int, int)> cellOf,
        Stack<List<T>> spareLists, List<T> items, Func<T, bool> include, Func<T, Vec3> positionOf, float cellSize)
    {
        foreach (List<T> list in cells.Values)
        {
            list.Clear();
            spareLists.Push(list);
        }
        cells.Clear();
        cellOf.Clear();
        foreach (T item in items)
        {
            if (!include(item))
                continue;
            Vec3 pos = positionOf(item);
            var key = ((int)Math.Floor(pos.x / cellSize), (int)Math.Floor(pos.y / cellSize));
            if (!cells.TryGetValue(key, out List<T> list))
            {
                list = spareLists.Count > 0 ? spareLists.Pop() : new List<T>();
                cells[key] = list;
            }
            list.Add(item);
            cellOf[item] = key;
        }
    }

    /// <summary>The fresh-container form, kept for the query tests.</summary>
    internal static Dictionary<(int, int), List<T>> BuildCells<T>(List<T> items, Func<T, bool> include, Func<T, Vec3> positionOf, float cellSize)
    {
        var cells = new Dictionary<(int, int), List<T>>();
        BuildCellsInto(cells, new Dictionary<T, (int, int)>(), new Stack<List<T>>(), items, include, positionOf, cellSize);
        return cells;
    }

    /// <summary>Removes <paramref name="item"/> from the one cell <paramref name="cellOf"/> names. Returns
    /// false when it is in no cell. Pure.</summary>
    internal static bool RemoveFromCells<T>(Dictionary<(int, int), List<T>> cells, Dictionary<T, (int, int)> cellOf, T item)
    {
        if (!cellOf.TryGetValue(item, out (int, int) key)) return false;
        cellOf.Remove(item);
        return cells.TryGetValue(key, out List<T> list) && list.Remove(item);
    }

    public void GetAgentsInRadius(Vec3 center, float radius, List<Agent> buffer)
    {
        bool offThread = MissionThreadGuard.NoteCall("SpatialGrid.GetAgentsInRadius", ReportOffThread);
        // Only the mission thread may build; an off-thread reader (a reported regression) reads what is there.
        if (_skippedRebuild && !offThread && _agents != null)
            Rebuild(_agents);
        _queriedSinceBuild = true;
        CollectInRadius(_grid, center, radius, CellSize, AgentPosition, buffer);
    }
```

Notes the executor must keep: `AllAgents` holds each agent once, so one cell entry per agent matches
today's `RemoveNow` (which stopped at the first cell that held it). Nullable is enabled project-wide
(`Directory.Build.props:6`), so `List<Agent>? _agents` compiles as written. `_agents` is the list the last
`UpdateGrid` passed: in a mission without `AdvancedCombatBehavior` (whose constructor makes a fresh
`SpatialGrid.Instance`), `WargMissionBehavior` keeps the old instance (`SpatialGrid.Instance ??= new`,
`WargMissionBehavior.cs:58-62`), so a query before that mission's first `UpdateGrid` could rebuild from the
previous mission's list; today the same query reads the previous mission's cells, so this is no worse, and
every TAOM mission adds `AdvancedCombatBehavior` (`SubModule.cs:2051`). Rewrite the class summary (`:9-16`) to say: the cells are rebuilt every two seconds
from the mission tick while something reads them; a build nobody queried makes the next scheduled rebuild a
skip, and the next query rebuilds first; the rebuild fills the spare of two maps and publishes it by one
reference write, so a reader never walks a map being cleared (#592, #595); removals use the agent-to-cell
index. No em or en dash in any comment.

`Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs`: add
`private readonly List<Agent> _nearby = new();` and replace the allocating call with
`SpatialGrid.Instance.GetNearAliveAgentsInRange(20f, Agent.Main, _nearby);`, iterating `_nearby`. The
service is an IoC singleton (`AdvancedCombatIoC.cs`), so the field is per process; it is only touched from
the mission tick.

`docs/features/advanced-combat.md`:

- Line 19: replace "rebuilds the grid every 2 seconds from `Mission.AllAgents`, keeping spatial lookups to
  O(agents-in-nearby-cells). Since #595 the rebuild replaces the map (never cleared in place under a reader),
  `OnAgentDeleted` evicts the deleted agent," with "asks the grid to rebuild every 2 seconds from
  `Mission.AllAgents`, keeping spatial lookups to O(agents-in-nearby-cells). A build nobody queried turns the
  next scheduled rebuild into a skip, and the next query rebuilds first, so a battle without wargs or spiders
  stops paying for it. The rebuild fills the spare of two maps and publishes it by one reference write (never
  cleared in place under a reader, #595), reusing the cell lists; `OnAgentDeleted` evicts the deleted agent
  from its one cell through an agent-to-cell index,". Keep the rest of the sentence.
- Line 28: `[every 2s]` becomes `[every 2s, skipped while unread]`.
- Line 74: after the `SpatialGridQueryTests.cs (9)` sentence add "`SpatialGridReuseTests.cs` (4) pins the
  reused build and the O(cell) removal against a fresh build and the old walk, and
  `SpatialGridDormancyTests.cs` (6) pins when a rebuild is skipped and when a query rebuilds first."
- Line 78: replace "is untested (audit issue #185)." with "has one IL rule (it scans into a reused buffer);
  its rendering is untested (audit issue #185)."
- Changelog (line 90, newest first): add
  "- 2026-10-02 (plan 033): `SpatialGrid` reuses its two maps and cell lists, removes a deleted agent from its
  one cell, and skips a scheduled rebuild nobody queried (the next query rebuilds first); the debug overlay
  scans into a reused buffer."

**Verify**: build exits 0; `~SpatialGridReuseTests` passes 4, `~SpatialGridDormancyTests` passes 6,
`~SpatialGridDebugServiceTests` passes 3, `~SpatialGridQueryTests` passes 9, `~SpatialGridRemovalTests`
passes 3, `~WargTickCostTests` shows no failure; `python tools/lint_docs.py --fail-on-drift` exits 0.
Commit 3 (`Not-tested:` the in-game grid with live agents: a warg Custom Battle should bite as before, and a
battle without wargs or spiders should log no grid rebuild cost, which only plan 028's profiler can show).

### Step 10: RED, the hunt picks the full scan's target from a nearby query

`TAOM.Tests/Features/CreatureBandits/CreatureHuntNearestTests.cs`, class `CreatureHuntNearestTests`,
`[TestCategory("RequiresGame")]` (it uses `Vec3`). Fixture: `private sealed class Point { public Vec3 P; public bool Target; }`.
Helpers in the test, which model the engine and today's code:

- `NativeWalk(List<Point> all, Vec3 origin, float r)`: the points whose horizontal distance
  `(p.P.x - origin.x)^2 + (p.P.y - origin.y)^2 <= r * r` (inclusive), in REVERSED list order (the query's
  order is not the scan's), with a `null` entry appended (the engine wrapper can add one).
- `FullScan(List<Point> all, Vec3 origin)`: today's loop over target points in list order with
  `p.P.DistanceSquared(origin) < best` (strict).
- `Search(List<Point> all, Vec3 origin)`: for each `r` in `CreatureHuntTask.QueryRadii`, if
  `CreatureHuntTask.TryPickNearest(NativeWalk(all, origin, r), p => p.Target, p => p.P, origin, r, out Point? found)`
  return `found`; then return `FullScan(all, origin)`. This mirrors the task's loop.

Tests:

- `Search_RandomLayouts_PicksTheFullScansTarget`: seeded `Random(659)`, 3,000 layouts of 0 to 60 points with
  x, y in [-150, 150], z in [-10, 10] (every tenth layout z in [-45, 45]), about a third not targets, a random
  origin each; assert the found point's squared distance equals the full scan's (exact float equality, both
  null when there is none), and `AreSame` whenever the full scan's minimum is unique.
- `TryPickNearest_NearestWithinTheFirstRadius_IsAccepted`: a target 5 m away; `TryPickNearest` at
  `QueryRadii[0]` returns true with it.
- `Search_NearestOnlyBeyondEveryRadius_FallsBackToTheFullScan`: the only targets 100 m and 140 m away; every
  `TryPickNearest` returns false and `Search` returns the 100 m one.
- `Search_NoTargetAnywhere_ReturnsNull`: only non-targets; null.
- `TryPickNearest_NearerNonTarget_IsSkipped`: a non-target at 1 m, a target at 5 m: the target.
- `Search_TallColumn_DoesNotHideANearerTargetOutsideTheDisk`: target A at horizontal 3 m and z +30 (3D about
  30.15 m), target B at horizontal 25 m and z 0: the 20 m query holds only A, whose 3D distance exceeds
  `20 - margin`, so it is rejected; the 60 m query holds both and returns B; `Search` equals `FullScan`.
- `TryPickNearest_BestAtTheMargin_IsAccepted` and `TryPickNearest_BestJustBeyondTheMargin_IsRejected`: origin
  `new Vec3(0f, 0f, 0f)`, `r = CreatureHuntTask.QueryRadii[0]`, one target on the x axis at
  `new Vec3(r - CreatureHuntTask.QueryMargin, 0f, 0f)` (accepted) or at `x = r - QueryMargin + 0.01f`
  (rejected; write it `CreatureHuntTask.QueryMargin` there too). Axis-aligned from a zero origin so no float rounding can flip the exact case (19.5 and 380.25 are
  exact in `float`).
- `TryPickNearest_NaNPosition_IsNeverPicked`: a target whose position is NaN and nothing else: returns false
  and `found` is null (the gate is a positive requirement, csharp-architecture.md "Engine-Float Decision Gates").
- `QueryRadii_AreAllAboveTheEngineGridThreshold`: every radius is greater than 15f and they ascend. The
  message names the native threshold: at 15 m or below the engine answers from its proximity grid, whose
  freshness is not proven, so the equivalence argument would not hold.

**Verify**: building `TAOM.Tests` fails, and every error is CS0117 ("'CreatureHuntTask' does not contain a
definition for ...") naming `TryPickNearest`, `QueryRadii` or `QueryMargin` (the tests always qualify all three
as `CreatureHuntTask.`; an unqualified use would give CS0103 instead), each at least once, and no other
diagnostic.

### Step 11: GREEN, the hunt asks the engine first, and its doc

In `Main/Features/CreatureBandits/BehaviorTreeElements/CreatureHuntTask.cs`:

```csharp
    // Radii the hunt asks the engine for before it walks every hostile team. Each is above 15 m: the native
    // nearby query (IMBMission.GetNearbyAgentsAux, v1.5.3) walks every agent slot for a radius above 15 m,
    // testing horizontal distance (inclusive) on the same position field Agent.Position reads, and keeping
    // only active humanoids; at 15 m or below it reads its own proximity grid instead (plan 033).
    internal static readonly float[] QueryRadii = { 20f, 60f };

    // Slack between the native horizontal test and the managed 3D distance, so float rounding can never
    // accept a candidate that an agent outside the query beats.
    internal const float QueryMargin = 0.5f;

    private static readonly Func<Agent, Vec3> AgentPosition = agent => agent.Position;

    private readonly MBList<Agent> _nearby = new MBList<Agent>();
    private readonly List<Team> _hostileTeams = new List<Team>();
    private readonly Func<Agent, bool> _isTarget;
```

- The constructor also sets `_isTarget = IsTarget;` (once: a method group converted per call would allocate a
  delegate each hunt), with `private bool IsTarget(Agent agent) => agent.Team != null && _hostileTeams.Contains(agent.Team) && agent.IsHuman && agent.IsActive();`.
- `TryPickNearest`:

```csharp
    /// <summary>
    /// The nearest target in a nearby query's result, accepted only when it is provably the nearest of all:
    /// the query held every candidate within <paramref name="queryRadius"/> horizontally, so a candidate it
    /// did not return is farther than that in 3D, and the pick is accepted only within
    /// <paramref name="queryRadius"/> minus <see cref="QueryMargin"/>. Otherwise returns false and the
    /// caller widens the query or walks every hostile team. Pure; tests drive it with plain points.
    /// </summary>
    internal static bool TryPickNearest<T>(List<T> nearby, Func<T, bool> isTarget, Func<T, Vec3> positionOf,
        Vec3 origin, float queryRadius, out T? nearest) where T : class
    {
        nearest = null;
        float bestDistanceSquared = float.MaxValue;
        for (int i = 0; i < nearby.Count; i++)
        {
            T item = nearby[i];
            if (item == null || !isTarget(item)) continue;
            float distanceSquared = positionOf(item).DistanceSquared(origin);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearest = item;
            }
        }
        float accepted = queryRadius - QueryMargin;
        // A positive requirement: a NaN distance fails it and the caller falls back to the full scan.
        if (nearest != null && bestDistanceSquared <= accepted * accepted) return true;
        nearest = null;
        return false;
    }
```

- `FindNearestEnemy` becomes an instance method (it uses the fields): it fills `_hostileTeams` from
  `mission.Teams` with today's test (`other != team && other.IsEnemyOf(team)`, one call per team, in team
  order), returns null when the list is empty, then for each radius calls
  `mission.GetNearbyAgents(origin.AsVec2, radius, _nearby)` and returns the pick when `TryPickNearest(_nearby, _isTarget, AgentPosition, origin, radius, out Agent? near)`
  is true; otherwise it runs today's loop unchanged except that it iterates `_hostileTeams` instead of
  re-testing `mission.Teams`. `Vec3 origin = creature.Position;` is read once, before the queries, as today.
- Signature facts to confirm with `pwsh tools/taom-src.ps1 path Mission` before relying on them (STOP on a
  mismatch): `public MBList<Agent> GetNearbyAgents(Vec2 center, float radius, MBList<Agent> agents)`.
- Add `using System.Collections.Generic;` (the file has none today).
- Class summary: replace exactly these two whole lines (`:19-20` at the planned-at commit; the summary is an
  XML `///` comment, so code names are `<c>` tags, never Markdown backticks):

```csharp
/// 16 m engage gate saw no enemy (<c>rca-spider-troop-2026-06-04.md:63</c>). The search walks the hostile teams'
/// active agents: one native <c>IsEnemyOf</c> per team, not per soldier. While the creature's own attack clip plays the
```

  with these four lines (line 18 above them and line 21, "/// task holds it where it stands ...", below them
  stay as they are):

```csharp
/// 16 m engage gate saw no enemy (<c>rca-spider-troop-2026-06-04.md:63</c>). It asks the engine for active humanoids
/// within 20 m, then 60 m, and walks the hostile teams' active agents only when neither answer holds a target provably
/// nearer than anything outside it (<see cref="TryPickNearest{T}"/>); one native <c>IsEnemyOf</c> per team, not per
/// soldier. While the creature's own attack clip plays the
```

`docs/features/creature-bandits.md`, line 268: replace "- The hunt walks the hostile teams' active agents
about four times a second per creature, allocation-free." with "- The hunt runs about four times a second per
creature, allocation-free. It asks the engine for active humanoids within 20 m, then 60 m
(`Mission.GetNearbyAgents`, which walks the agent slots above 15 m), and walks the hostile teams' active agents
only when neither answer holds a target provably nearer than anything outside it; the target is the same
nearest enemy either way."

**Verify**: build exits 0; `~CreatureHuntNearestTests` passes 10; `~CreatureBandits` shows no failure;
`python tools/lint_docs.py --fail-on-drift` exits 0. Commit 4 (`Not-tested:` the live
`Mission.GetNearbyAgents` call and the task's loop around it, which need a battle; the equivalence they rely
on is the native reading recorded in this commit body: the slot walk above 15 m, its humanoid and active
filter, and the shared position field).

### Step 12: final verification

Run the full suite and the two RefAsm commands, then the normal build and test again.

**Verify**: the normal totals are the base's plus the new tests (2 + 1 + 10 + 4 + 6 + 1 + 10 = 34 new),
with the same single failure (`EveryLanguage_DeclaresARowForEveryEnglishKey`) and no new Skipped beyond
Inconclusive allocation probes you recorded; the RefAsm run's failing set equals Step 1's (every new test
that touches an engine type is tagged `RequiresGame`, so the filter skips it); `python tools/lint_docs.py --fail-on-drift`
exits 0; the stale-claim greps in "Done criteria" print nothing; `git status --porcelain` lists nothing.

## Test plan

- `CreatureTreeClockTests` (2): the IL rule over the five namespaces and its control. Pattern:
  `HowdahCrewLookupBanTests`.
- `SelectorListReuseTests` (1): list identity across two runs plus the run count. Pattern:
  `BehaviorTreeBuilderTests`.
- `BehaviorTreeMissionLogicDispatchTests` (10): off-thread parking with no listener, with one, after the last
  unsubscribes and after mission end; main-thread delivery; main-thread allocation with none and with a
  non-matching tree listener; `OnAgentFleeing`. Pattern: `SpatialGridRemovalTests`, `DeferredCallbackQueueTests`.
- `SpatialGridReuseTests` (4), `SpatialGridDormancyTests` (6), one new `SpatialGridDebugServiceTests` rule.
  Pattern: `SpatialGridQueryTests`, `WargTickCostTests.AssertScansIntoABuffer`.
- `CreatureHuntNearestTests` (10): the randomized equivalence against the full scan, the named layouts (first
  radius, beyond every radius, none, non-target nearer, tall column, the margin both ways, NaN) and the radius
  pin.
- Not testable here, named in the `Not-tested:` trailers: the creature cooldowns' in-game feel; the delivery of
  an off-thread event in the frame a first creature tree is built; the grid with live agents in a battle; the
  live `GetNearbyAgents` call and the hunt task's loop.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] The test command reports the base's totals plus 34 new tests, which pass (or the dispatch allocation
      probes are Inconclusive and recorded), and the only failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`
- [ ] The RefAsm run's failing set equals the base's (Step 1)
- [ ] `git grep -n "DateTime.Now" -- Main/BehaviorTreeWrapper Main/BehaviorTrees Main/Features/ElephantLike Main/Features/Spider Main/Features/Warg` prints nothing
- [ ] `git grep -n "= new List<BTNode>();" -- Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs` prints only the two field initialisers (`:34`, `:124`)
- [ ] `git grep -n "foreach (List<Agent> cell in _grid.Values)" -- Main` prints nothing
- [ ] `git grep -n "GetNearAliveAgentsInRange(20f, Agent.Main)" -- Main` prints nothing
- [ ] `git grep -n -i "rebuilds the grid every 2 seconds\|walks the hostile teams' active agents about four times" -- docs Main TAOM.Tests` prints nothing
- [ ] `git grep -n "write DateTime.Now" -- Main` prints nothing
- [ ] `git log --oneline <the commit your branch started from>..HEAD` shows exactly the four commits of
      "Git workflow", each subject at most 72 characters
- [ ] `git status --porcelain` is empty and every committed path is in Scope
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it describes

## STOP conditions

Stop and report (do not improvise) if:

- `.claude/pinned-game-version.txt` is not `v1.5.3`, or `pwsh tools/taom-src.ps1 path Mission` shows a
  `GetNearbyAgents` signature other than the one in Step 11: the native facts behind the hunt were read on
  v1.5.3 only.
- The code at any "Current state" location does not match its excerpt.
- Step 2's RED lists a method outside the eight named (an unlisted clock writer or reader), or a blackboard
  `DateTime` value is read against a clock anywhere other than the pairs in "Change A".
- `git grep -n ": Selector\b" -- Main` finds a subclass, or `SubscribesTo` is assigned outside the
  `BannerlordBTListener` constructor.
- Step 4's RED fails on the run count instead of the list identity.
- A grid reader exists that reads `_grid` other than through `GetAgentsInRadius(Vec3, float, List<Agent>)`
  or `RemoveNow`, or a new caller of `UpdateGrid` appears.
- Any existing `BehaviorTree`, `Warg`, `Spider`, `Elephant`, `ElephantLike`, `Mumakil`, `CreatureBandits` or
  `AdvancedCombat` test fails after a GREEN step and the cause is not an error you can see in your own change.
- `CreatureHuntNearestTests.Search_RandomLayouts_PicksTheFullScansTarget` fails: the equivalence argument is
  wrong somewhere; do not widen the margin or the radii to make it pass.
- The fix seems to need an out-of-scope file (`IoC.cs`, `SubModule.cs`, the csproj, `AdvancedCombatBehavior.cs`,
  `TaomHowdahMachine.cs`) or a protected file.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (perf: creature-battle allocations and scans), and reference it in the
  commit bodies if the executor is given its number.
- No `/localize`: this plan adds no player-facing text.
- No new feature doc or feature-map row: Steps 9 and 11 update `docs/features/advanced-combat.md` and
  `docs/features/creature-bandits.md` in place.
- Report to the maintainer (FOR-MIKE): the howdah's per-frame `GetSkeleton()` (`TaomHowdahMachine.cs:274`)
  was not changed because neither a cached wrapper nor a reduced read rate is provably equivalent (evidence
  under "Excluded at planning"). Options for him: accept a per-elephant cached wrapper on the evidence that no
  battle path is known to call `SetSkeleton` on a mount's visuals, or leave it. The war tower's call
  (`TaomMumakilPlatform.cs:278`) is a diagnostics status probe, not per frame, and needs nothing. Also:
  `AdvancedCombatBehavior.GridUpdateInterval` is still `2f` (plan 003 did not change it); shortening it is a
  gameplay call.
- Ask plan 028's profiler to measure the creature-bandit hunt before and after commit 4: the win after
  contact and the extra cost before contact ("Change E") are argued, not measured.
- After merge, the in-game check (`triage-needs-ingame`): a warg Custom Battle (bites land, rage triggers),
  a creature-bandit battle (the spider hunts the nearest soldier from the start), an elephant battle (trample
  and side-attack cooldowns feel unchanged).

## After merge: the maintainer's actions

Pull, then a normal deploying build before the in-game checks above. Nothing is untracked and no hook or
setting changes.

## Maintenance notes

- A new tree listener for an off-thread callback now matters to `_listenerCounts`: subscribe only through
  `Subscribe`/`UnSubscribe`, and never reassign `BannerlordBTListener.SubscribesTo` after construction.
- A new grid reader must go through `GetAgentsInRadius(Vec3, float, List<Agent>)` (or an overload that
  delegates to it), or the skip logic will not see it and it will read a stale grid after an idle spell.
- After an engine bump, re-run `python tools/native_decompile.py --engine-method IMBMission.GetNearbyAgentsAux`
  and `--engine-method IMBAgent.GetPosition`: the hunt's equivalence depends on the slot walk above 15 m and
  the shared position field. `QueryRadii_AreAllAboveTheEngineGridThreshold` pins only the managed side.
- Review should probe: the 14 override value lists in Step 7 against their bodies; the two-map swap and the
  spare-list pool in `BuildCellsInto` (a list must never sit in two cells); that `GetAgentsInRadius` never
  rebuilds off the main thread; the hunt's `IsTarget` against today's filter and team order.
- Plan 030 edits `docs/features/creature-bandits.md` a few lines below line 268; a merge conflict there is
  textual only.
- Deferred, with reasons above: the howdah skeleton, `GridUpdateInterval`, `CreatureTreeTracker.PruneDead`,
  `AgentAdapter.CustomAttack`'s allocating overload, the `SpatialGrid.Remove` closure.
