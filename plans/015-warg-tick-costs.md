# Plan 015: Cut the warg behaviour tree's per-tick resolves, allocations and native wrapper churn

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. The orchestrator maintains `plans/README.md`
> for this run: do NOT edit it; report your status in your final message.
>
> **Where you work (read this twice).** All work happens in ONE worktree:
> `E:\repos\wt-015-warg-tick-costs` (Git Bash form `E:/repos/wt-015-warg-tick-costs`, called **W**
> below). The main checkout `E:\repos\TAOM` holds another session's uncommitted edits (to
> `Main/SubModule.cs`, `Main/IoC.cs`, `CHANGELOG.md`, `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`
> and many creature files); touching it can commit their work. Your shell's working directory resets
> to `E:\repos\TAOM` on EVERY call, so a bare relative path lands in the wrong tree. Therefore,
> without exception:
> - Every shell call starts with `cd E:/repos/wt-015-warg-tick-costs && `.
> - Every git call is `git -C E:/repos/wt-015-warg-tick-costs ...` (the only exceptions are the
>   drift check below and the Step 0 `worktree add`, which name `E:/repos/TAOM` on purpose and write
>   nothing into its files).
> - Every Read, Edit or Write path is absolute and starts with `E:\repos\wt-015-warg-tick-costs\`.
>   Every repo-relative path in this plan (for example `Main/Features/AdvancedCombat/SpatialGrid.cs`)
>   means that path under W. Never edit a path under `E:\repos\TAOM\`.
> - If the orchestrator gave you a different worktree cut from `b2e387db`, substitute its absolute
>   path for W everywhere.
>
> **Shell**: run every command in this plan in **Git Bash (the Bash tool)**, not PowerShell. Use the
> Write and Edit tools for file changes, never `sed -i` (the repo has CRLF files).
>
> **Drift check (run first)**:
> `git -C E:/repos/TAOM diff --stat b2e387db..bannerlord-1.5.x -- Main/Features/AdvancedCombat/SpatialGrid.cs Main/Features/AdvancedCombat/BoneCheck.cs Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs Main/Features/Warg/WargMissionBehavior.cs Main/Features/Warg/WargRiderHandManager.cs Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs TAOM.Tests/Features/Warg/WargTickCostTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs docs/features/warg-combat.md docs/features/advanced-combat.md`
> Expected: no output (verified empty on 2026-09-23 against `4b5662b2`, the branch tip then). It
> compares commits only and writes nothing. You work on a branch cut from `b2e387db`, so the
> excerpts below are exact for your worktree. If the command prints any file, the trunk moved under
> this plan: finish on your branch anyway, but list those files in your final report so the merge
> can be planned. If an excerpt below does not match YOUR worktree, that is a STOP condition.

## Status

- **Priority**: P3
- **Effort**: M (nine production files touched, each by a few lines; three new test files; two doc edits)
- **Risk**: LOW to MED (no save data, no XML, no Harmony; the risk is a subtle change in which agent
  a warg bite hits, which the tests below pin)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: #659

> **Amendment 2 (orchestrator, 2026-09-24, Mike's decision 32).** The node services are no longer
> resolved in field initializers (Step 2's excerpts): `WargBehaviorTree.BuildTree` resolves
> `IMissionAdapterFactory` and `IWargAttackService` once per tree and passes them to the node
> constructors, so the four nodes hold no `IoC.Resolve` (commit `23f6f85b` on
> `improve/015-warg-tick-costs`, reviewed again in its second review). Step 2's code shows what the first
> execution did; the branch tip is the reference for a port.

> **Amendment (orchestrator, 2026-09-24, after the first execution stopped at Step 8).** The claim
> that a substitute returning null keeps the tests away from native code is wrong for any null test
> written with `==` on a `NativeObject` subtype (`Skeleton`, `MBAgentVisuals`): `==` binds to
> `NativeObject.operator ==`, and calling any static member of `NativeObject` runs its static
> constructor, which calls native code and throws `TypeInitializationException` in the test host.
> The operator itself (v1.5.3 `TaleWorlds.DotNet.NativeObject.cs:221-232`) returns `true` only for the
> same reference and `false` when exactly one side is null, so `x == null` and `x is null` are
> behaviour-identical. **Amended Steps 8 and 9:** in `BoneCheck.CheckTargets` (and any other null
> test on a `NativeObject` value the new tests reach), write the null checks as `is null` /
> `is object` instead of `== null` / `!= null`; the RED test then fails for the intended reason (the
> range gate is missing), not in the engine's static constructor. Record this as a lesson.

## Why this matters

Every warg's behaviour tree re-runs its root on every mission tick, and the nodes it runs for an
engaged, ridden warg each pay avoidable costs per frame: a DryIoc container lookup (`IoC.Resolve`)
in four node types and in the player's rider-hand manager, a fresh `List<Agent>` from each of three
grid scans (60 m and twice 10 m), and 343 dictionary lookups for each 60 m scan because the grid
is keyed on x, y AND z while a battlefield is a few metres tall. While a warg's bite is live, the
bone check also asks the engine for a new native `Skeleton` wrapper (a native ref-count call, a
process-wide lock, a `GCHandle` and a finalizer) for every agent it captured within 20 m on every
frame, before it tests whether that agent is within the roughly 4.5 m it can actually hit. None of
this changes gameplay; it lands as main-thread time and GC pressure in the heaviest phase of a
creature battle (magnitude UNMEASURED: the audit's scale figures are estimates from assumed crowd
densities). After this plan the resolves happen once per tree, the scans reuse buffers, a 60 m
scan looks up 49 cells, and a far target's skeleton is never fetched. Every scan returns the same
set of agents and a bite can reach the same set of targets; only the ORDER of agents inside one
20 m column can change, which can change which of two in-reach targets a stop-on-first-hit bite
lands on (Maintenance notes, "Order within a column").

## Current state

All excerpts below were read with `git show b2e387db:<path>`; line numbers are at `b2e387db`.

### Files and their roles

- `Main/Features/Warg/WargBehaviorTree.cs`: builds the tree. Read only. Line 22
  `public WargBehaviorTree(Agent agent) : base(10)` (10 ms root delay).
- `Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs:65`: read only.
  `if ((Tree._rootEvaluationDelay / 1000) < timeSinceLastEvaluation || Tree.ShouldRunNextTick)`.
  `_rootEvaluationDelay` is an `int`, so `10 / 1000` is `0` and the root runs every tick. **Do not
  change this file** (every creature tree shares it; see Out of scope).
- `Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs` (68 lines): two
  classes, `PeriodicallyCheckIfCanAttackAnyone` and `CheckOnceIfCanAttackEnemy`. Both resolve per
  call and both allocate a scan list. **You edit both.**
- `Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs` (28 lines): the 60 m scan.
  **You edit it.**
- `Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs` (30 lines): resolves
  per call. **You edit it.**
- `Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs` (35 lines): resolves twice per attack.
  **You edit it.**
- `Main/Features/Warg/WargRiderHandManager.cs` (44 lines): static class ticked from
  `WargMissionBehavior.OnMissionTick`; resolves every frame. **You edit it.**
- `Main/Features/Warg/WargMissionBehavior.cs` (207 lines): the `MissionLogic`; line 115 calls
  `WargRiderHandManager.Tick();`. **You edit three lines.**
- `Main/Features/AdvancedCombat/SpatialGrid.cs` (146 lines): the cell grid every creature tree scans.
  **You refactor it.**
- `Main/Features/AdvancedCombat/BoneCheck.cs` (148 lines) and `BoneCheckDuringAnimation.cs` (60
  lines): the per-frame bite hit test. **You refactor both.**
- `Main/Adapters/AgentAdapter.cs:171-227` (`CustomAttack`): builds the bone check's target list.
  Read only (see Out of scope).
- `Main/IoC.cs`: single-owner. `Resolve` at lines 236-239 is a plain container call:
  ```csharp
      public static T Resolve<T>()
      {
          return _container.Resolve<T>();
      }
  ```
  Line 226 registers `IMissionAdapterFactory` as `Reuse.Singleton`.
  `Main/Features/Warg/WargIoC.cs` registers `IWargAttackService` as `Reuse.Transient`.

### Excerpt: `Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs:12-37` and `:42-67`

```csharp
public class PeriodicallyCheckIfCanAttackAnyone : WaitNSecondsTickDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();

    public PeriodicallyCheckIfCanAttackAnyone() : base(0.2) { }
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
        List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg);
        foreach (Agent agent in nearbyAgents)
        {
            if (agent == warg || agent == warg.RiderAgent || agent.IsMount) continue;
            if (agent.IsActive() && agent.Team?.Side != wargSide)
            {
                var agentAdapter = AdapterFactory.GetAgentAdapter(agent);
                var wargAdapter = AdapterFactory.GetAgentAdapter(warg);
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }
...
public class CheckOnceIfCanAttackEnemy : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();

    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg);
        BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
        foreach (Agent agent in nearbyAgents)
        {
            if (agent == warg || agent == warg.RiderAgent) continue;
            if (agent.IsActive() && agent.Team?.Side != wargSide && !agent.IsMount)
            {
                var agentAdapter = AdapterFactory.GetAgentAdapter(agent);
                var wargAdapter = AdapterFactory.GetAgentAdapter(warg);
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }
}
```

The file's `using` lines (1-8) are `BehaviorTrees`, `BehaviorTreeWrapper.BlackBoardClasses`,
`BehaviorTreeWrapper.Decorators`, `TAOM.Adapters`, `TAOM.Features.AdvancedCombat`,
`System.Collections.Generic`, `TaleWorlds.Core`, `TaleWorlds.MountAndBlade`. Line 39 is
`    public override void Notify(object[] data) { }` inside the first class; keep it.

### Excerpt: `NoEnemyCloseDecorator.cs:9-28` (whole class)

```csharp
public class NoEnemyCloseDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        Agent agent = Agent.GetValue();
        List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(60, agent);
        Team wargTeam = agent.RiderAgent?.Team ?? agent.Team;
        foreach (Agent agent2 in nearbyAgents)
        {
            if (agent2 == agent || agent2 == agent.RiderAgent)
                continue;
            if (agent2.IsActive() && agent2.Team != null && agent2.Team != wargTeam)
                return false;
        }
        return true;
    }
}
```

### Excerpt: `WargAiControlledIsNotFacingEnemy.cs:16` and `:24-29`

```csharp
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();
...
    public override bool Evaluate()
    {
        var agentHitByAdapter = AdapterFactory.GetAgentAdapter(AgentHitBy.GetValue());
        var agentAdapter = AdapterFactory.GetAgentAdapter(Agent.GetValue());
        return !agentHitByAdapter.IsAttackLikelyToHit(agentAdapter, 30, WargConfig.WargAttackRange);
    }
```

### Excerpt: `WargAttackTask.cs:23-34`

```csharp
    public override BTTaskStatus Execute()
    {
        RageAttackAmount.SetValue(RageAttackAmount.GetValue() - 1);
        Agent warg = Agent.GetValue();
        if (warg != null)
        {
            // Boundary: wrap sealed Agent into adapter before crossing into service (ADR-007).
            var wargAdapter = IoC.Resolve<IMissionAdapterFactory>().GetAgentAdapter(warg);
            IoC.Resolve<IWargAttackService>().WargAttack(wargAdapter);
        }
        return BTTaskStatus.FinishedWithTrue;
    }
```

`WargAttackService` (`Main/Features/Warg/WargAttackService.cs:18-25`) holds only two readonly fields
(`_adapterFactory`, `_logger`) set in its constructor: no per-call state, so keeping one instance
per task is equivalent to resolving a Transient per attack.

### Excerpt: `WargRiderHandManager.cs:8-18`

```csharp
internal static class WargRiderHandManager
{
    public static void Tick()
    {
        if (Agent.Main == null) return;

        if (Agent.Main.HasMount && IoC.Resolve<IMissionAdapterFactory>().GetAgentAdapter(Agent.Main.MountAgent).IsWarg())
        {
            UpdateWargRiderHandle();
        }
    }
```

`Tick`'s only caller is `WargMissionBehavior.cs:115`. Proof: `git grep -n "WargRiderHandManager\." -- Main`
at `b2e387db` prints exactly three lines, all in `WargMissionBehavior.cs`: `:68` (a comment), `:73`
(`WargRiderHandManager.OnMainAgentDismount()`, a different method this plan does not touch) and
`:115` (`WargRiderHandManager.Tick();`). `WargMissionBehavior`'s constructor (lines 38-43) already resolves its services:

```csharp
    public WargMissionBehavior()
    {
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
        _logger = IoC.Resolve<IModLogger>();
        _deferred = new DeferredCallbackQueue(message => _logger.LogWarning(message));
    }
```

Its fields are declared at lines 16-36; its `using` lines (1-10) do NOT include `TAOM.Adapters`.
It is constructed with no arguments at `Main/SubModule.cs:1960` (`AddTaomBehavior(new WargMissionBehavior());`),
so its constructor signature must not change.

### Excerpt: `Main/Features/AdvancedCombat/SpatialGrid.cs:17-52`, `:77-129`

```csharp
public class SpatialGrid
{
    public static SpatialGrid Instance { get; internal set; }

    private Dictionary<(int, int, int), List<Agent>> _grid = new();
    public float CellSize = 20f;
...
    public void UpdateGrid(List<Agent> agents)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.UpdateGrid", ReportOffThread);
        // A fresh map, published by one reference write: a reader that still holds the old one walks a
        // finished structure rather than a map being cleared under it.
        var grid = new Dictionary<(int, int, int), List<Agent>>();
        foreach (Agent agent in agents)
        {
            if (!agent.IsActive())
                continue;
            var cell = GetCell(agent.Position);
            if (!grid.TryGetValue(cell, out List<Agent> list))
            {
                list = new List<Agent>();
                grid[cell] = list;
            }
            list.Add(agent);
        }
        _grid = grid;
    }
...
    private (int, int, int) GetCell(Vec3 pos)
    {
        return (
            (int)Math.Floor(pos.x / CellSize),
            (int)Math.Floor(pos.y / CellSize),
            (int)Math.Floor(pos.z / CellSize)
        );
    }

    public List<Agent> GetAgentsInRadius(Vec3 center, float radius)
    {
        List<Agent> agents = new();
        GetAgentsInRadius(center, radius, agents);
        return agents;
    }
...
    public void GetAgentsInRadius(Vec3 center, float radius, List<Agent> buffer)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.GetAgentsInRadius", ReportOffThread);
        buffer.Clear();
        var grid = _grid;
        float radiusSquared = radius * radius;
        int minX = (int)Math.Floor((center.x - radius) / CellSize);
        int maxX = (int)Math.Floor((center.x + radius) / CellSize);
        int minY = (int)Math.Floor((center.y - radius) / CellSize);
        int maxY = (int)Math.Floor((center.y + radius) / CellSize);
        int minZ = (int)Math.Floor((center.z - radius) / CellSize);
        int maxZ = (int)Math.Floor((center.z + radius) / CellSize);

        // Enumerate ONLY the cells in the radius bounding box (TryGetValue per cell) rather than scanning every
        // occupied cell in the grid and filtering by key — the bbox is tiny for the creature scan ranges (≤~27
        // cells at CellSize 20) while the grid can hold hundreds of cells in a full battle (deep-review 2026-06-15).
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            if (!grid.TryGetValue((x, y, z), out List<Agent> cell)) continue;
            foreach (Agent agent in cell)
            {
                float dx = agent.Position.x - center.x;
                float dy = agent.Position.y - center.y;
                float dz = agent.Position.z - center.z;
                if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                    buffer.Add(agent);
            }
        }
    }
```

Lines 131-145 hold three `GetNearAliveAgentsInRange` overloads: `(float range, Agent target)` and
`(float range, Vec3 targetPos)` return a new list; `(float range, Agent target, List<Agent> buffer)`
fills the buffer. They stay unchanged. Lines 54-75 (`Remove`, `ApplyPendingRemovals`,
`PendingRemovalCount`, `RemoveNow`, which iterates `_grid.Values`) stay unchanged. The comment
"≤~27 cells" is wrong: for radius 60 at `CellSize` 20 each axis spans exactly 7 cells
(`(c+60)/20 - (c-60)/20 = 6`), so 343 lookups; radius 10 spans 2 per axis (8 lookups).

Callers of the grid at `b2e387db` (`git grep -n "GetNearAliveAgentsInRange\|GetAgentsInRadius" -- Main`):
the three warg nodes above (allocating overload), `SpiderEngageDecorator.cs:50` and
`AgentAdapter.cs:255` (buffer overload), `AgentAdapter.cs:207` and
`SpatialGridDebugService.cs:16` (allocating overload, once per attack or debug frame; untouched).
Nothing reads `_grid`'s key type or calls `GetCell` outside this file, and no test reflects on `_grid`
(`git grep -n '"_grid"\|GetCell' -- Main TAOM.Tests` prints only two lines, both inside
`SpatialGrid.cs` itself: `:43` `var cell = GetCell(agent.Position);` and `:77` the `GetCell`
declaration).

### Excerpt: `Main/Features/AdvancedCombat/BoneCheck.cs:53-147`

```csharp
    protected bool CheckBoneCollision()
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            Logger.LogWarning($"Agent {_agent?.Name ?? "null"} is no longer valid for bone collision check");
            return false;
        }

        IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
        if (agentVisuals == null)
        {
            Logger.LogWarning($"Failed to get visuals for {_agent.Name}");
            return false;
        }

        Skeleton agentSkeleton = agentVisuals.GetSkeleton();
        if (agentSkeleton == null)
        {
            Logger.LogWarning($"Failed to get skeleton for {_agent.Name}");
            return false;
        }
        MatrixFrame agentGlobalFrame = agentVisuals.GetGlobalFrame();

        List<(sbyte, Vec3)> agentBonePositions = new();
        int boneCount = agentSkeleton.GetBoneCount();
        foreach (sbyte bone in _boneIds)
        {
            if (bone < 0 || bone >= boneCount)
            {
                Logger.LogError($"Invalid bone index {bone} for agent {_agent.Name}");
                continue;
            }
            MatrixFrame agentBoneFrame = agentSkeleton.GetBoneEntitialFrameWithIndex(bone);
            Vec3 agentBoneGlobalPos = agentGlobalFrame.TransformToParent(agentBoneFrame.origin);
            agentBonePositions.Add((bone, agentBoneGlobalPos));
        }

        for (int i = 0; i < _targets.Count; i++)
        {
            IAgentAdapter target = _targets[i];

            if (target == null || !target.IsActive() || target.IsFadingOut())
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }

            IAgentVisualsAdapter targetVisuals = target.AgentVisuals;
            if (targetVisuals == null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }

            Skeleton targetSkeleton = targetVisuals.GetSkeleton();
            if (targetSkeleton == null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }
            MatrixFrame targetGlobalFrame = targetVisuals.GetGlobalFrame();
            sbyte boneId = FindBoneInRange(agentGlobalFrame, agentBonePositions, targetSkeleton, targetGlobalFrame);
            if (boneId != -1)
            {
                _targets.RemoveAt(i);
                _onCollisionCallback?.Invoke(_agent, target, boneId);
                if (_stopOnFirstHit)
                    return false;
            }
        }
        return true;
    }

    protected sbyte FindBoneInRange(MatrixFrame agentGlobalFrame, List<(sbyte boneId, Vec3 position)> agentBonePositions, Skeleton targetSkeleton, MatrixFrame targetGlobalFrame)
    {
        int targetBoneCount = targetSkeleton.GetBoneCount();
        if ((targetGlobalFrame.origin - agentGlobalFrame.origin).LengthSquared > _maxRangeForCheck)
            return -1;

        for (int i = 0; i < targetBoneCount; i++)
        {
            MatrixFrame targetBoneFrame = targetSkeleton.GetBoneEntitialFrameWithIndex((sbyte)i);
            Vec3 targetBoneGlobalPos = targetGlobalFrame.TransformToParent(targetBoneFrame.origin);
            foreach (var (boneId, agentBonePos) in agentBonePositions)
            {
                float distanceSquared = (targetBoneGlobalPos - agentBonePos).LengthSquared;
                if (distanceSquared <= _collisionRadiusSquared)
                    return (sbyte)i;
            }
        }
        return -1;
    }
```

Line 30 (constructor): `_maxRangeForCheck = Math.Max(20f, _collisionRadiusSquared * 20f);`. The warg
passes radius 1.0 (running) or 0.5 (standing) (`WargAttackService.cs:137,148`), so the gate is 20
square metres (about 4.47 m) in both cases. `CheckBoneCollision` and `FindBoneInRange` are called
only from `BoneCheck.cs:45`, `:117` and `BoneCheckDuringAnimation.cs:51`
(`git grep -n "FindBoneInRange\|CheckBoneCollision" -- Main TAOM.Tests`). Production code
constructs `BoneCheckDuringAnimation` in two places (`git grep -n "new BoneCheckDuringAnimation" -- Main`):
`Main/Adapters/AgentAdapter.cs:215` (inside `CustomAttack`) and
`Main/Features/AdvancedCombat/Services/BoneCollisionService.cs:25` (`CreateAnimationBoneCheck`, a
factory method). Both only construct it; the constructor signature does not change, so neither file
is edited. The only production caller of `CustomAttack` is `WargAttackService.cs:151`.

### Excerpt: `Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs:14-59`

```csharp
    public BoneCheckDuringAnimation(ActionIndexCache action, IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds, float actionProgressMin, float actionProgressMax, float boneCollisionRadius, bool stopAfterFirstHit, Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration)
        // 2026-05-24 (#219): base() previously received `actionProgressMax` (a 0.0-1.0
        // progress fraction) as the maxDuration parameter, which the base class then
        // used as `_maxRangeForCheck` (a squared-meters distance gate). Result: a hard
        // 0.84m cap on agent-to-agent distance (sqrt(0.7)≈0.84) before bone iteration
        // could even run. At 8-10 m/s the warg crossed that gate in <100ms — usually
        // too narrow a window for the per-frame check to land. Passing 100f (≈10m
        // distance cap) makes the gate a real perf optimization (skip distant agents)
        // instead of an unintended hit-rate killer. Spider attacks use this same
        // class and benefit from the same fix.
        : base(agent, targets, boneIds, 100f, boneCollisionRadius, stopAfterFirstHit, onCollisionCallback, onExpiration)
    {
        _action = action;
        _actionProgressMin = actionProgressMin;
        _actionProgressMax = actionProgressMax;
    }

    public override bool Tick(float dt)
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            _onExpiration?.Invoke();
            return false;
        }

        IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
        if (_targets == null || _targets.Count == 0
            || agentVisuals?.GetSkeleton() == null
            || _agent.GetCurrentAction(0) != _action
            || _agent.GetCurrentActionProgress(0) >= _actionProgressMax)
        {
            _onExpiration?.Invoke();
            return false;
        }

        if (_agent.GetCurrentActionProgress(0) >= _actionProgressMin)
        {
            if (!CheckBoneCollision())
            {
                _onExpiration?.Invoke();
                return false;
            }
        }

        return true;
    }
```

The constructor comment is wrong today: `100f` lands in `maxDuration`, which `BoneCheck.Tick` reads
but this class overrides `Tick` and never reads it; the real gate is the constructor's
`_maxRangeForCheck`; spiders no longer use this class (they use `AgentAdapter.RadialStrike`). The
file's `using` lines are `TAOM.Adapters`, `System`, `System.Collections.Generic`,
`TaleWorlds.MountAndBlade`; it does NOT import `TaleWorlds.Engine` (where `Skeleton` lives).

### Engine facts (v1.5.3, read from the `taom-src` cache `C:\Users\mikew\.taom-src\v1.5.3\`)

- `TaleWorlds.MountAndBlade.MBAgentVisuals.cs` (`pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.MBAgentVisuals`
  prints this path; the short name `MBAgentVisuals` does not resolve):
  ```csharp
  	public MatrixFrame GetGlobalFrame()                                   // lines 32-37
  	{
  		MatrixFrame outFrame = default(MatrixFrame);
  		MBAPI.IMBAgentVisuals.GetGlobalFrame(GetPtr(), ref outFrame);
  		return outFrame;
  	}
  ...
  	public Skeleton GetSkeleton()                                         // lines 145-148
  	{
  		return MBAPI.IMBAgentVisuals.GetSkeleton(GetPtr());
  	}
  ```
- `ManagedCallbacks.ScriptingInterfaceOfIMBAgentVisuals.cs:776-786`: every call builds a NEW wrapper.
  ```csharp
  	public Skeleton GetSkeleton(UIntPtr agentVisualsPtr)
  	{
  		NativeObjectPointer nativeObjectPointer = call_GetSkeletonDelegate(agentVisualsPtr);
  		Skeleton result = null;
  		if (nativeObjectPointer.Pointer != UIntPtr.Zero)
  		{
  			result = new Skeleton(nativeObjectPointer.Pointer);
  			LibraryApplicationInterface.IManaged.DecreaseReferenceCount(nativeObjectPointer.Pointer);
  		}
  		return result;
  	}
  ```
- `TaleWorlds.DotNet.NativeObject.cs:32-51`: `Construct` makes a native `IncreaseReferenceCount`, takes
  `lock (_nativeObjectKeepReferences)`, and does `GCHandle.Alloc(this)` (held for 10 ticks); the class
  has a finalizer that makes another native call. `NativeObject` also has a static constructor
  (line 62) that calls native (`GetClassTypeDefinitionCount`): **no test may construct a `Skeleton`
  or touch a `NativeObject` static.** The tests below never do (a substitute returns `null`).
- `TaleWorlds.MountAndBlade.Agent.cs:706` `public Vec3 Position => AgentHelper.GetAgentPosition(PositionPointer);`
  and `:710` `public Vec3 VisualPosition => MBAPI.IMBAgent.GetVisualPosition(GetPtr());`: the engine
  keeps a separate visual position, so `Agent.Position` and the visuals frame origin are NOT proven
  equal. This is why this plan gates on the visuals frame origin (exactly today's gate) and not on
  `Position`.
- `TaleWorlds.Library.MatrixFrame.cs:110-114` `public MatrixFrame(in Mat3 rot, in Vec3 o)` assigns
  `rotation` and `origin` only (pure managed); `TaleWorlds.Library.Mat3.cs:45`
  `public static Mat3 Identity => new Mat3(new Vec3(1f), new Vec3(0f, 1f), new Vec3(0f, 0f, 1f));`;
  `TaleWorlds.Library.Vec3.cs:160` `public float LengthSquared => x * x + y * y + z * z;` and `:334-336`
  `operator -` subtracts x, y, z. Tests can build frames and vectors freely (existing tests already
  build `Vec3`, for example `TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs:16`).
- `Mission.AllAgents` is `AgentReadOnlyList`, which derives `MBReadOnlyList<Agent>`, which derives
  `List<Agent>` (`TaleWorlds.MountAndBlade.Missions.AgentReadOnlyList.cs:6`,
  `TaleWorlds.Library.MBReadOnlyList`1.cs:5`), which is why `UpdateGrid(List<Agent>)` accepts it.

### Test infrastructure you will reuse

- `TAOM.Tests/Migration/IlCallScanner.cs:111`
  `public static IEnumerable<MethodBase> ExtractCalledMethods(MethodBase method, byte[] il)` yields every
  method a body calls (call, callvirt, newobj, ldftn), resolving generic instantiations such as
  `IoC.Resolve<IMissionAdapterFactory>` to a `MethodInfo` whose `DeclaringType` is `TAOM.IoC` and
  `Name` is `Resolve`. It does NOT see field reads, so a node reading a cached field never shows a
  call. Existing use: `TAOM.Tests/Features/Warg/WargOffThreadTests.cs` (whole file, 41 lines).
- `TAOM.Tests/Features/AdvancedCombat/BoneCollisionServiceTests.cs:16-42` shows the established way to
  construct a `BoneCheck` subclass in a test (`Substitute.For<IAgentAdapter>()` in the base call).
- `InternalsVisibleTo("TAOM.Tests")` is set on `Main` (many `internal ... for TAOM.Tests` comments, for
  example `Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:100`), so tests can call `internal`
  members and name the `internal static class WargRiderHandManager`.
- MSTest 3.1.1 + NSubstitute 5.1.0 (`TAOM.Tests/TAOM.Tests.csproj:9-12`), file-scoped namespaces.

### Conventions that bind this change

- **AGENTS.md "Verify before reference"**: "Cache `IoC.Resolve` lazily on a hot path." A BT node's
  `Evaluate` and `Execute`, and a mission tick, are hot paths.
- **Instance fields, not statics, for the node caches (decided here).** The orchestrator's brief
  suggested static `??=` caches. This plan resolves into `private readonly` INSTANCE fields at node
  construction instead, because: (1) a node is constructed once per warg per mission inside
  `WargMissionBehavior.TryAttachWargTree` (a `try`/`catch`, after `IoC.Configure`), which is not a hot
  path; (2) a static cache survives a module unload and reload in the same process and would keep a
  service from the disposed container, and the only place such caches are reset is the
  `ResetForUnload` calls at `Main/SubModule.cs:2133-2146`, just after `IoC.Dispose()` at line 2127
  (read with `git show b2e387db:Main/SubModule.cs`), a single-owner file
  this plan must not edit; (3) `SpiderEngageDecorator.cs:25,52` and `WargMissionBehavior`'s own
  constructor already keep services in instance fields. For `WargRiderHandManager` (a static class)
  the factory is passed in from `WargMissionBehavior`, which resolves it once in its constructor.
- **ADR-002 (thin entry points, under 150 lines)**: entry points delegate. `WargMissionBehavior` is
  already 207 lines at `b2e387db`; this plan adds three lines and does not refactor it (separate work).
- **ADR-007 (adapters)**: services take adapters, never sealed TaleWorlds types. BT nodes and
  `BoneCheck` are boundary code and already hold `Agent`/`Skeleton`; the new `SpatialGrid` helpers are
  generic and never touch `Agent`, which is what makes them testable.
- **ADR-008 (testability)**: services must be unit testable without game initialization. The new tests
  run with no engine: IL scans, plain points and substitutes.
- **ADR-003 / ADR-004 / ADR-005**: no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **`.claude/rules/csharp-architecture.md` "Mission-scope agent handles and the engine's threads"**:
  a deleted `Agent` is a live handle to whoever inherits its index; never trust a handle held across
  frames without `AgentSlotIdentity.IsCurrentOccupant` (which `AgentAdapter.IsActive()` calls,
  `AgentAdapter.cs:68`). How this plan respects it: the reused scan buffers are cleared and refilled
  by `SpatialGrid` before every read, so no earlier frame's handle is ever dereferenced; in the bone
  check the target's `IsActive()` / `IsFadingOut()` test stays FIRST, before any visuals read.
  Never key anything on `Agent.Index`.
- **Every tree reader and the grid rebuild run on the mission thread** (`SpatialGrid.cs:9-16`,
  `MissionThreadGuard`): the buffers need no locking.

### Decisions already taken (do NOT change them)

- **Do not filter same-team targets in `AgentAdapter.CustomAttack`.** An adversarial check found that
  with `stopOnFirstHit` an ally within bone reach currently ends the warg's swing (the callback
  returns early for a same-team victim, `WargAttackService.cs:43-45`, then `BoneCheck` returns
  false). Dropping allies at capture would change which swings land: a gameplay change for the
  maintainer to decide, not a perf fix.
- **Do not pre-filter targets on `IAgentAdapter.Position`.** The gate this plan moves compares
  visuals frame origins; `Position` is a different engine value (`Agent.VisualPosition` exists
  separately), so a `Position` cut could drop a target the current gate would test.
- **Do not change the 2 s grid rebuild in `AdvancedCombatBehavior` or the 0.1 s one in
  `WargMissionBehavior`** (a separate finding that needs a decision).
- **Key the grid on (x, y), not a clamped z range.** Both give the same result set; (x, y) keying is
  less code and always 49 lookups for a 60 m scan.

### Test baseline at `b2e387db` (so you do not chase known failures)

The orchestrator's run at `b2e387db`: 10,239 tests, 10,235 passed, 2 failed, 2 not executed
(ignored). In the console banner (format under "Reading `dotnet test` output") that is
`Failed!  - Failed:     2, Passed: 10235, Skipped:     2, Total: 10239, ...`, and the command exits 1
because of those 2 failures: a non-zero exit from the FULL run is expected and is not by itself a
failure of this plan. The 2 failures are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (`TAOM.Tests/Features/Elk/ElkConfigTests.cs:93`)
and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`
(`TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs:127`). Both read the
live, unversioned Armory install that another session is editing right now; neither is caused by or
related to this plan (it touches no data and no Elk or Animalia code), so they may pass, fail, or
change while you work. Do not investigate or fix them. The 2 not executed are deliberate `[Ignore]`s in `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs:349,372`
(`WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack`). The rule
for every later full run: the failures must be a subset of those 2 Armory tests plus whatever Step 0
recorded.

## Commands you will need

Every command runs in Git Bash and starts with `cd E:/repos/wt-015-warg-tick-costs && `. Never run
`./build.ps1`, never launch the game, never write under `E:\Steam\` or `E:\repos\TAOM\`.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `cd E:/repos/wt-015-warg-tick-costs && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test (full) | `cd E:/repos/wt-015-warg-tick-costs && mkdir -p TestResults && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > TestResults/plan015-full.log 2>&1; echo exit=$?` | a banner line and a failure list read as below; failures a subset of the baseline rule above. The orchestrator's full run took 40 s wall on a fresh worktree; still call the Bash tool with `timeout: 600000` |
| Test (filtered) | `cd E:/repos/wt-015-warg-tick-costs && mkdir -p TestResults && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>" > TestResults/plan015-<ClassName>.log 2>&1; echo exit=$?` | the banner counts named in the step, checked with the regex the step gives |
| Data | `cd E:/repos/wt-015-warg-tick-costs && python tools/validate_moduledata.py 2>/dev/null \| grep -A1 "=== SUMMARY ===" \| tail -1` | one line such as `  0 error(s), 1591 warning(s)` (the orchestrator's baseline at `b2e387db`); the error count no higher than Step 0's (this plan changes no data; the validator also reads the live Armory another session is editing) |
| Docs | `cd E:/repos/wt-015-warg-tick-costs && python tools/lint_docs.py \| grep "Dead links:"` | `- Dead links: **0**` |
| Tree guard | `git -C E:/repos/wt-015-warg-tick-costs rev-parse --abbrev-ref HEAD` | `plan-015-warg-tick-costs` |

### Reading `dotnet test` output

Test output always goes to a log under `W/TestResults/` (git ignores both `TestResults/` and `*.log`,
so the logs never show in `git status`): the build output before the results runs to thousands of
lines and the Bash tool truncates at about 30 KB. Never print a whole log; read it with the `grep`s
below and the ones a step gives. Where a step names the log file (for example
`plan015-WargFolder.log` for a filter that is not a class name), use that name.

1. **The banner.** `grep -E '^(Passed|Failed)!' TestResults/<log>` prints ONE line. There is no
   `Total tests:` line at default verbosity; the banner is the only summary. Its counts are padded
   with spaces, for example (a real TAOM run):
   `Passed!  - Failed:     0, Passed:  9692, Skipped:     2, Total:  9694, Duration: 16 s - TAOM.Tests.dll (net472)`.
   It starts `Failed!` when any test failed. **Never grep a literal `Passed: 9` or `Failed: 0`**
   (the padding breaks it). Every count check in this plan is a `grep -E` with `\s+`, for example
   `grep -cE 'Failed:\s+0, Passed:\s+9, Skipped:\s+0, Total:\s+9,' TestResults/<log>` printing `1`.
   If the banner line is missing, the build failed: `grep -E 'error CS[0-9]+' TestResults/<log>`
   shows why.
2. **The failing tests.** `grep -E '^  Failed ' TestResults/<log>` prints one line per failure, by
   METHOD name only, for example `  Failed TheElkItem_DeclaresTheScaleTheReachIsTunedFor [103 ms]`.
   The assertion message follows it on the lines after `  Error Message:`
   (`grep -A2 '^  Failed ' TestResults/<log>` shows both).
3. **Which class and folder a failing method belongs to.** The output never names the class. Map
   each method with `git -C E:/repos/wt-015-warg-tick-costs grep -n "void <MethodName>(" -- TAOM.Tests`;
   the printed path (for example `TAOM.Tests/Features/Elk/ElkConfigTests.cs:93:`) gives the folder
   that the STOP rules below refer to.

## Scope

Every path below is relative to W (`E:\repos\wt-015-warg-tick-costs\`).

**In scope** (the only files you may modify or create):

- `Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs`
- `Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs`
- `Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs`
- `Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs`
- `Main/Features/Warg/WargRiderHandManager.cs`
- `Main/Features/Warg/WargMissionBehavior.cs` (one `using`, one field, one constructor line, one call)
- `Main/Features/AdvancedCombat/SpatialGrid.cs`
- `Main/Features/AdvancedCombat/BoneCheck.cs`
- `Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs`
- `TAOM.Tests/Features/Warg/WargTickCostTests.cs` (create)
- `TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs` (create)
- `TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs` (create)
- `docs/features/warg-combat.md` (lines 126 and 150-152 only)
- `docs/features/advanced-combat.md` (lines 50, 74 and 76 only)

**Single-owner files**: `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`, `Directory.Build.props`:
**recommend, don't edit.** None needs a change: `WargMissionBehavior`'s constructor keeps its
signature (`SubModule.cs:1960` is untouched), no registration changes, and SDK-style `Main/TAOM.csproj`
and `TAOM.Tests` pick up new `.cs` files by globbing. If the build reports a new test file is not
compiled, STOP and report the file name for the owner.

**Out of scope** (do NOT touch, even though they look related):

- `Main/Adapters/AgentAdapter.cs` (`CustomAttack`, `RadialStrike`): see "Decisions already taken".
- `Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs:65` (`/ 1000` integer division) and
  `Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs` (`Selector.Prepare` allocations): shared by every
  creature tree; separate findings.
- `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs`, `WargMissionBehavior`'s grid cadence,
  `Main/Features/Spider/**`, `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` (another session is
  editing it in the main checkout).
- `CHANGELOG.md` and `docs/ai-includes/orientation.md`: another session has uncommitted edits to them
  in the main checkout. Give the CHANGELOG text in your final report instead (Step 11).
- `docs/features/warg-combat.md:153` ("Grid updates: Every 5 ticks") is stale but belongs to the grid
  cadence finding; leave it.

## Git workflow

- **Worktree and branch** (Step 0):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-015-warg-tick-costs -b plan-015-warg-tick-costs b2e387db`.
  This creates W and the branch; it does not modify the main checkout's files.
- **Before every commit**, run both and confirm the exact results:
  1. The Tree guard prints `plan-015-warg-tick-costs`.
  2. `git -C E:/repos/wt-015-warg-tick-costs diff --cached --name-only` lists exactly the files the
     step names, nothing else.
- **Stage and commit** with `git -C E:/repos/wt-015-warg-tick-costs add <path> <path>` then
  `git -C E:/repos/wt-015-warg-tick-costs commit -m "<subject>" -m "<body>"`. Explicit paths only;
  never `git add -A`, `git add .` or `git commit -a`.
- **Commit subject:** `<type>(<scope>): v<version> - <description>`, at most 72 characters, where
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` (it reads `v2.0.30` at
  `b2e387db`, line 6; re-read it with `grep -n "Version value" Main/_Module/SubModule.xml | head -1`).
  The example subjects in the steps are 55 to 67 characters. Body wrapped at 72, prose without em or
  en dashes. After each commit,
  `git -C E:/repos/wt-015-warg-tick-costs log -1 --format=%b | awk 'length($0)>72{n++} END{print n+0}'`
  prints `0`, and
  `git -C E:/repos/wt-015-warg-tick-costs log -1 --format=%s | python -X utf8 -c "import sys; s=sys.stdin.read().strip(); print(len(s), s)"`
  prints a number at most 72. If either check fails, rewrite the message of that commit (it is your
  own, unpushed, on your own branch) with
  `git -C E:/repos/wt-015-warg-tick-costs commit --amend -m "<shorter subject>" -m "<rewrapped body>"`
  and run both checks again. Amend only the commit you just made, never an earlier one. If the amend
  is refused or the checks still fail, STOP.
- **No AI attribution trailer** (no `Co-Authored-By`, no "Generated with" line; a hook refuses them).
  Optional trailers: `Not-tested:`, `Research:`.
- **Never push**, never open a PR, never merge. You commit C# without `/deep-review` because you
  cannot invoke skills; the orchestrator runs `/deep-review` on this branch before any merge (the
  CLAUDE.md gate).

## Steps

### Step 0: Create the worktree and record the baseline

1. Run the drift check (top of this file) and note its output.
2. Create the worktree (command in Git workflow). If it fails because the directory or the branch
   already exists, STOP. Run the Tree guard: it prints `plan-015-warg-tick-costs`.
3. Confirm the excerpts. `git -C E:/repos/wt-015-warg-tick-costs log -1 --format=%h` prints
   `b2e387db`, then run this block; it must print exactly `2 1 2 1 1 1 1 1 1 1`:
   ```bash
   cd E:/repos/wt-015-warg-tick-costs && for pair in \
     "Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs|private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();" \
     "Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs|private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();" \
     "Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs|IoC.Resolve<" \
     "Main/Features/Warg/WargRiderHandManager.cs|IoC.Resolve<IMissionAdapterFactory>()" \
     "Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs|GetNearAliveAgentsInRange(60, agent);" \
     "Main/Features/AdvancedCombat/SpatialGrid.cs|for (int z = minZ; z <= maxZ; z++)" \
     "Main/Features/AdvancedCombat/BoneCheck.cs|if ((targetGlobalFrame.origin - agentGlobalFrame.origin).LengthSquared > _maxRangeForCheck)" \
     "Main/Features/AdvancedCombat/BoneCheck.cs|List<(sbyte, Vec3)> agentBonePositions = new();" \
     "Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs||| agentVisuals?.GetSkeleton() == null" \
     "Main/Features/Warg/WargMissionBehavior.cs|WargRiderHandManager.Tick();"; do \
     f="${pair%%|*}"; p="${pair#*|}"; printf "%s " "$(grep -cF -- "$p" "$f")"; done; echo
   ```
   (The `BoneCheckDuringAnimation` pattern is `|| agentVisuals?.GetSkeleton() == null`; the extra
   `|` is the separator.)
4. Confirm the engine fact:
   `sed -n 776,786p /c/Users/mikew/.taom-src/v1.5.3/ManagedCallbacks.ScriptingInterfaceOfIMBAgentVisuals.cs | grep -c "result = new Skeleton(nativeObjectPointer.Pointer);"`
   prints `1`. If the file is missing, run
   `cd E:/repos/wt-015-warg-tick-costs && pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.MBAgentVisuals 2>/dev/null | tail -1`
   (it populates the cache; about two minutes) and retry.
5. Record the data baseline: run the Data command and note the error count.
6. Run Test (full) with `timeout: 600000`. Read the log as "Reading `dotnet test` output" says:
   the banner (expected shape at `b2e387db`: `Failed!  - Failed:     2, Passed: 10235, Skipped:     2, Total: 10239,`),
   then every `  Failed ` line. Write down every failing method name; this is the Step 0 list. Map
   each one to its file with the `git grep "void <MethodName>("` command. Any failure whose file is
   under `TAOM.Tests/Features/Warg/` or `TAOM.Tests/Features/AdvancedCombat/`: STOP. Any other
   failure beyond the two Armory tests: record it with its file, do not fix it, continue.

**Verify**: the Tree guard prints the branch name; item 3 prints `b2e387db` and
`2 1 2 1 1 1 1 1 1 1`; item 4 prints `1`; item 5 printed an error count;
`grep -cE '^(Passed|Failed)!  - Failed:\s+[0-9]+, Passed:\s+[0-9]+, Skipped:\s+[0-9]+, Total:\s+[0-9]+,' TestResults/plan015-full.log`
prints `1`, and you have the Step 0 list with a file for each name.

### Step 1 (RED): Pin the per-tick rules for the warg nodes in the IL

Create `E:\repos\wt-015-warg-tick-costs\TAOM.Tests\Features\Warg\WargTickCostTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Warg;
using TAOM.Features.Warg.BehaviorTreeElements;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// A warg tree's root runs on every mission tick (the tree is built with a 10 ms delay and
/// BehaviorTreeAgentComponent compares it in whole seconds), so whatever a node's Evaluate or
/// Execute calls, it calls per frame per engaged warg. Two rules, pinned in the IL (plan 015): a
/// per-tick method never reaches IoC.Resolve, directly or through a getter of its own type; and a
/// grid scan fills a reused buffer through the three-argument SpatialGrid overload instead of
/// allocating a list per call.
/// </summary>
[TestClass]
public class WargTickCostTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static IEnumerable<MethodBase> CallsIn(MethodBase method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray();
        return il == null ? Enumerable.Empty<MethodBase>() : IlCallScanner.ExtractCalledMethods(method, il);
    }

    /// <summary>The method's own calls plus the calls of each same-type method it calls: a static
    /// getter such as the old AdapterFactory property hides its Resolve one level down.</summary>
    private static List<MethodBase> CallsReachedFrom(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, Declared);
        Assert.IsNotNull(method, $"{type.Name}.{methodName} not found");
        List<MethodBase> direct = CallsIn(method).ToList();
        IEnumerable<MethodBase> oneDown = direct.Where(m => m.DeclaringType == type).SelectMany(CallsIn);
        return direct.Concat(oneDown).ToList();
    }

    private static void AssertNeverResolves(Type type, string methodName)
    {
        List<string> resolves = CallsReachedFrom(type, methodName)
            .Where(m => m.DeclaringType == typeof(global::TAOM.IoC) && m.Name == "Resolve")
            .Select(m => m.ToString())
            .ToList();
        Assert.AreEqual(0, resolves.Count,
            $"{type.Name}.{methodName} runs every tick; use a service resolved once, not IoC.Resolve: {string.Join(", ", resolves)}");
    }

    private static void AssertScansIntoABuffer(Type type)
    {
        List<MethodBase> scans = CallsReachedFrom(type, "Evaluate")
            .Where(m => m.DeclaringType == typeof(SpatialGrid) && m.Name == nameof(SpatialGrid.GetNearAliveAgentsInRange))
            .ToList();
        Assert.IsTrue(scans.Count > 0, $"{type.Name}.Evaluate no longer scans the grid; this test needs re-planning");
        Assert.IsTrue(scans.All(m => m.GetParameters().Length == 3),
            $"{type.Name}.Evaluate calls an allocating GetNearAliveAgentsInRange overload; pass a reused List<Agent> buffer");
    }

    [TestMethod]
    public void PeriodicallyCheckIfCanAttackAnyone_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(PeriodicallyCheckIfCanAttackAnyone), "Evaluate");

    [TestMethod]
    public void CheckOnceIfCanAttackEnemy_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(CheckOnceIfCanAttackEnemy), "Evaluate");

    [TestMethod]
    public void WargAiControlledIsNotFacingEnemy_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(WargAiControlledIsNotFacingEnemy), "Evaluate");

    [TestMethod]
    public void WargAttackTask_Execute_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(WargAttackTask), "Execute");

    [TestMethod]
    public void WargRiderHandManager_Tick_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(WargRiderHandManager), "Tick");

    [TestMethod]
    public void NoEnemyCloseDecorator_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(NoEnemyCloseDecorator), "Evaluate");

    [TestMethod]
    public void NoEnemyCloseDecorator_Evaluate_ScansIntoAReusedBuffer()
        => AssertScansIntoABuffer(typeof(NoEnemyCloseDecorator));

    [TestMethod]
    public void PeriodicallyCheckIfCanAttackAnyone_Evaluate_ScansIntoAReusedBuffer()
        => AssertScansIntoABuffer(typeof(PeriodicallyCheckIfCanAttackAnyone));

    [TestMethod]
    public void CheckOnceIfCanAttackEnemy_Evaluate_ScansIntoAReusedBuffer()
        => AssertScansIntoABuffer(typeof(CheckOnceIfCanAttackEnemy));
}
```

**Verify**: run Test (filtered) with `WargTickCostTests` (log `TestResults/plan015-WargTickCostTests.log`).
`grep -cE '^Failed!  - Failed:\s+8, Passed:\s+1, Skipped:\s+0, Total:\s+9,' TestResults/plan015-WargTickCostTests.log`
prints `1`, and `grep -E '^  Failed ' TestResults/plan015-WargTickCostTests.log` lists 8 methods, none
of them `NoEnemyCloseDecorator_Evaluate_NeverResolvesFromIoC` (the one that already passes; it guards
the node against regressing). A compile error, an `AmbiguousMatchException`, or any other count: STOP.

### Step 2 (GREEN): Resolve once per node, scan into reused buffers

Make these edits. Keep every other line of each file as it is.

(a) `Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs`: in BOTH classes,
replace the line
`    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();`
with

```csharp
    // Resolved once when the tree is built (once per warg per mission), never per evaluation: the
    // tree's root runs every mission tick. An instance field, not a static, so a module reload can
    // never keep a factory from a disposed container.
    private readonly IMissionAdapterFactory _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
    // Reused scan buffer. SpatialGrid clears and refills it on every call, so no handle from an
    // earlier tick is ever read.
    private readonly List<Agent> _scratch = new();
```

Then make the two `Evaluate` bodies read exactly:

```csharp
    // PeriodicallyCheckIfCanAttackAnyone.Evaluate
    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
        SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg, _scratch);
        var wargAdapter = _adapterFactory.GetAgentAdapter(warg);
        foreach (Agent agent in _scratch)
        {
            if (agent == warg || agent == warg.RiderAgent || agent.IsMount) continue;
            if (agent.IsActive() && agent.Team?.Side != wargSide)
            {
                var agentAdapter = _adapterFactory.GetAgentAdapter(agent);
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }
```

```csharp
    // CheckOnceIfCanAttackEnemy.Evaluate
    public override bool Evaluate()
    {
        Agent warg = Agent.GetValue();
        SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg, _scratch);
        BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
        var wargAdapter = _adapterFactory.GetAgentAdapter(warg);
        foreach (Agent agent in _scratch)
        {
            if (agent == warg || agent == warg.RiderAgent) continue;
            if (agent.IsActive() && agent.Team?.Side != wargSide && !agent.IsMount)
            {
                var agentAdapter = _adapterFactory.GetAgentAdapter(agent);
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }
```

Hoisting `GetAgentAdapter(warg)` out of the loop is safe: `MissionAdapterFactory.GetAgentAdapter`
(`Main/Adapters/MissionAdapterFactory.cs:27-33`) returns the cached adapter for the same `Agent`
object every time. Do not comment the `// ...Evaluate` label lines into the file; they only mark
which body is which here.

(b) `Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs`: after the `_agent` field add

```csharp
    // Reused 60 m scan buffer: this runs on every tick of every ridden warg's tree. SpatialGrid
    // clears and refills it on every call, so no handle from an earlier tick is ever read.
    private readonly List<Agent> _scratch = new();
```

and replace the two lines
`        List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(60, agent);` and
`        foreach (Agent agent2 in nearbyAgents)` with
`        SpatialGrid.Instance.GetNearAliveAgentsInRange(60, agent, _scratch);` and
`        foreach (Agent agent2 in _scratch)`.

(c) `Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs`: replace line 16 with

```csharp
    // Resolved once when the tree is built, never per evaluation (the root runs every tick).
    private readonly IMissionAdapterFactory _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
```

and in `Evaluate` replace both `AdapterFactory.GetAgentAdapter(` with `_adapterFactory.GetAgentAdapter(`.

(d) `Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs`: after the `_firstAttack` field (line 16) add

```csharp
    // Resolved once when the tree is built, not on every attack. IWargAttackService is registered
    // Transient, but WargAttackService keeps no per-call state, so one instance per task is equivalent.
    private readonly IMissionAdapterFactory _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
    private readonly IWargAttackService _attackService = IoC.Resolve<IWargAttackService>();
```

and replace lines 30-31 with

```csharp
            var wargAdapter = _adapterFactory.GetAgentAdapter(warg);
            _attackService.WargAttack(wargAdapter);
```

(keep the ADR-007 comment on line 29).

(e) `Main/Features/Warg/WargRiderHandManager.cs`: change `Tick` to

```csharp
    /// <summary>Called every mission tick by WargMissionBehavior, which resolves the factory once.</summary>
    public static void Tick(IMissionAdapterFactory adapterFactory)
    {
        if (Agent.Main == null) return;

        if (Agent.Main.HasMount && adapterFactory.GetAgentAdapter(Agent.Main.MountAgent).IsWarg())
        {
            UpdateWargRiderHandle();
        }
    }
```

(f) `Main/Features/Warg/WargMissionBehavior.cs`: add `using TAOM.Adapters;` to the `using` block;
add the field `    private readonly IMissionAdapterFactory _adapterFactory;` after line 17
(`private readonly IModLogger _logger;`); in the constructor add
`        _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();` after the `_logger = ...` line; and
change line 115 to `            WargRiderHandManager.Tick(_adapterFactory);`.

**Verify**:
- Build exits 0 with `0 Error(s)`.
- Test (filtered) with `WargTickCostTests`:
  `grep -cE '^Passed!  - Failed:\s+0, Passed:\s+9, Skipped:\s+0, Total:\s+9,' TestResults/plan015-WargTickCostTests.log`
  prints `1`.
- Test (filtered) with `TAOM.Tests.Features.Warg.` (the trailing dot keeps it to that folder's
  classes; a bare `Warg` also matches CultureMarketplace tests; name the log
  `TestResults/plan015-WargFolder.log`):
  `grep -cE '^Passed!  - Failed:\s+0, Passed:\s+[0-9]+, Skipped:\s+2,' TestResults/plan015-WargFolder.log`
  prints `1` (the two `[Ignore]`d tests are the 2 skipped).
- `cd E:/repos/wt-015-warg-tick-costs && grep -rn "IoC.Resolve" Main/Features/Warg/BehaviorTreeElements Main/Features/Warg/WargRiderHandManager.cs`
  prints exactly 5 lines, every one a `private readonly ... = IoC.Resolve<...>();` field initializer:
  2 in `PeriodicallyCheckIfCanAttackAnyone.cs`, 1 in `WargAiControlledIsNotFacingEnemy.cs`, 2 in
  `WargAttackTask.cs`, none in `WargRiderHandManager.cs`. (At `b2e387db` the same grep prints 6
  lines, all in those four files; no other file there resolves.) If a line is inside an `Evaluate`,
  `Execute` or `Tick` body, STOP.

Commit (paths: the six `Main/Features/Warg/...` files from (a) to (f) and
`TAOM.Tests/Features/Warg/WargTickCostTests.cs`), for example subject
`perf(warg): v2.0.30 - resolve tree services once, scan into buffers`.

### Step 3 (REFACTOR): Move SpatialGrid's cell logic into generic helpers, behaviour unchanged

Goal: the cell build and the radius query become `internal static` generic methods that never touch
`Agent`, so a test can drive them with plain points. The grid stays 3D in this step.

Order note: this refactor comes BEFORE its characterising tests (Step 4) on purpose. Today's query
is reachable only through `Agent`, which a test cannot construct, so the tests can only be written
against the helpers this step creates. They compare the helpers with a brute-force sphere scan,
which is the specification of the old query, so they would catch a behaviour change made here.
Steps 3 and 4 are committed together.

Edit `Main/Features/AdvancedCombat/SpatialGrid.cs`:

1. After the `_pendingRemovals` field (line 31) add:
   ```csharp
    // The Agent-typed API delegates to the generic helpers below, which never touch an Agent, so
    // tests can run the exact query on plain points. One position read per agent per query.
    private static readonly Func<Agent, Vec3> AgentPosition = agent => agent.Position;
    private static readonly Func<Agent, bool> IsLiveAgent = agent => agent.IsActive();
   ```
2. Replace the body of `UpdateGrid` (lines 35-51) with:
   ```csharp
        MissionThreadGuard.NoteCall("SpatialGrid.UpdateGrid", ReportOffThread);
        // A fresh map, published by one reference write: a reader that still holds the old one walks a
        // finished structure rather than a map being cleared under it.
        _grid = BuildCells(agents, IsLiveAgent, AgentPosition, CellSize);
   ```
3. Delete `GetCell` (lines 77-84) and add in its place:
   ```csharp
    /// <summary>Buckets every included item by the cell of its position. Pure; tests drive it with plain points.</summary>
    internal static Dictionary<(int, int, int), List<T>> BuildCells<T>(List<T> items, Func<T, bool> include, Func<T, Vec3> positionOf, float cellSize)
    {
        var cells = new Dictionary<(int, int, int), List<T>>();
        foreach (T item in items)
        {
            if (!include(item))
                continue;
            Vec3 pos = positionOf(item);
            var key = ((int)Math.Floor(pos.x / cellSize), (int)Math.Floor(pos.y / cellSize), (int)Math.Floor(pos.z / cellSize));
            if (!cells.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                cells[key] = list;
            }
            list.Add(item);
        }
        return cells;
    }
   ```
4. Replace the body of `GetAgentsInRadius(Vec3 center, float radius, List<Agent> buffer)` (lines
   101-128) with:
   ```csharp
        MissionThreadGuard.NoteCall("SpatialGrid.GetAgentsInRadius", ReportOffThread);
        CollectInRadius(_grid, center, radius, CellSize, AgentPosition, buffer);
   ```
   and add after that method:
   ```csharp
    /// <summary>
    /// Clears <paramref name="buffer"/> and fills it with the items whose position is within
    /// <paramref name="radius"/> of <paramref name="center"/> (3D distance, inclusive), looking up only
    /// the cells in the query's bounding box. Returns how many cells it looked up. Pure.
    /// </summary>
    internal static int CollectInRadius<T>(Dictionary<(int, int, int), List<T>> cells, Vec3 center, float radius, float cellSize, Func<T, Vec3> positionOf, List<T> buffer)
    {
        buffer.Clear();
        float radiusSquared = radius * radius;
        int minX = (int)Math.Floor((center.x - radius) / cellSize);
        int maxX = (int)Math.Floor((center.x + radius) / cellSize);
        int minY = (int)Math.Floor((center.y - radius) / cellSize);
        int maxY = (int)Math.Floor((center.y + radius) / cellSize);
        int minZ = (int)Math.Floor((center.z - radius) / cellSize);
        int maxZ = (int)Math.Floor((center.z + radius) / cellSize);

        int probes = 0;
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            probes++;
            if (!cells.TryGetValue((x, y, z), out List<T> cell)) continue;
            foreach (T item in cell)
            {
                Vec3 pos = positionOf(item);
                float dx = pos.x - center.x;
                float dy = pos.y - center.y;
                float dz = pos.z - center.z;
                if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                    buffer.Add(item);
            }
        }
        return probes;
    }
   ```
   (`_grid` is read once as the argument, which keeps the old `var grid = _grid;` snapshot semantics.)

Leave the class summary, `Instance`, `_grid`, `CellSize`, both `Report*` fields, `Remove`,
`ApplyPendingRemovals`, `PendingRemovalCount`, `RemoveNow`, the allocating
`GetAgentsInRadius(Vec3, float)`, the buffer overload's doc comment and the three
`GetNearAliveAgentsInRange` overloads unchanged.

**Verify**:
- Build exits 0 with `0 Error(s)`.
- Test (filtered) with `SpatialGrid` (matches `SpatialGridRemovalTests` and
  `SpatialGridDebugServiceTests`):
  `grep -cE '^Passed!  - Failed:\s+0,' TestResults/plan015-SpatialGrid.log` prints `1`.
- Test (filtered) with `WargTickCostTests`:
  `grep -cE '^Passed!  - Failed:\s+0, Passed:\s+9, Skipped:\s+0, Total:\s+9,' TestResults/plan015-WargTickCostTests.log`
  prints `1`.

### Step 4 (characterisation): Pin the query results before changing the keys

Create `E:\repos\wt-015-warg-tick-costs\TAOM.Tests\Features\AdvancedCombat\SpatialGridQueryTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Pins SpatialGrid's radius query against a brute-force sphere scan. The Agent-typed API reads
/// native positions, so these tests drive the generic BuildCells / CollectInRadius helpers it
/// delegates to, on plain points (plan 015).
/// </summary>
[TestClass]
public class SpatialGridQueryTests
{
    private const float CellSize = 20f;

    private sealed class Point
    {
        public Point(float x, float y, float z) => P = new Vec3(x, y, z);
        public Vec3 P { get; }
        public override string ToString() => $"({P.x}, {P.y}, {P.z})";
    }

    private static readonly Func<Point, Vec3> PositionOf = p => p.P;
    private static readonly Func<Point, bool> Everyone = _ => true;

    private static List<Point> Query(List<Point> points, Vec3 center, float radius)
    {
        var cells = SpatialGrid.BuildCells(points, Everyone, PositionOf, CellSize);
        var buffer = new List<Point>();
        SpatialGrid.CollectInRadius(cells, center, radius, CellSize, PositionOf, buffer);
        return buffer;
    }

    private static List<Point> BruteForce(List<Point> points, Vec3 center, float radius)
    {
        float radiusSquared = radius * radius;
        return points.Where(p =>
        {
            float dx = p.P.x - center.x;
            float dy = p.P.y - center.y;
            float dz = p.P.z - center.z;
            return dx * dx + dy * dy + dz * dz <= radiusSquared;
        }).ToList();
    }

    [TestMethod]
    public void CollectInRadius_MatchesABruteForceSphereScan()
    {
        var rng = new Random(15);
        float Next(float min, float max) => (float)(min + rng.NextDouble() * (max - min));
        var points = new List<Point>();
        for (int i = 0; i < 600; i++)
            points.Add(new Point(Next(-150f, 150f), Next(-150f, 150f), Next(-45f, 45f)));

        foreach (float radius in new[] { 0.5f, 10f, 20f, 60f, 75f })
        {
            for (int c = 0; c < 25; c++)
            {
                var center = new Vec3(Next(-120f, 120f), Next(-120f, 120f), Next(-30f, 30f));
                CollectionAssert.AreEquivalent(BruteForce(points, center, radius), Query(points, center, radius),
                    $"radius {radius}, center ({center.x}, {center.y}, {center.z})");
            }
        }
    }

    [TestMethod]
    public void CollectInRadius_PointExactlyOnTheSphere_IsIncluded()
    {
        var onTheSphere = new Point(3f, 4f, 0f); // 3-4-5 triangle: distance squared is exactly 25
        var result = Query(new List<Point> { onTheSphere }, new Vec3(0f, 0f, 0f), 5f);
        CollectionAssert.AreEqual(new List<Point> { onTheSphere }, result);
    }

    [TestMethod]
    public void CollectInRadius_SameColumnDifferentHeights_KeepsOnlyThoseInsideTheSphere()
    {
        var ground = new Point(1f, 1f, 0f);
        var below = new Point(1f, 1f, -9f);    // 83 square metres: inside a 10 m sphere
        var wallTop = new Point(1f, 1f, 45f);  // same (x, y) column, 45 m up: outside
        var result = Query(new List<Point> { ground, below, wallTop }, new Vec3(0f, 0f, 0f), 10f);
        CollectionAssert.AreEquivalent(new List<Point> { ground, below }, result);
    }

    [TestMethod]
    public void BuildCells_ExcludedItems_AreNeverReturned()
    {
        var kept = new Point(1f, 1f, 0f);
        var excluded = new Point(2f, 2f, 0f);
        var cells = SpatialGrid.BuildCells(new List<Point> { kept, excluded }, p => p != excluded, PositionOf, CellSize);
        var buffer = new List<Point>();
        SpatialGrid.CollectInRadius(cells, new Vec3(0f, 0f, 0f), 10f, CellSize, PositionOf, buffer);
        CollectionAssert.AreEqual(new List<Point> { kept }, buffer);
    }

    [TestMethod]
    public void CollectInRadius_ClearsTheBufferBeforeFilling()
    {
        var stale = new Point(500f, 500f, 0f);
        var near = new Point(1f, 0f, 0f);
        var cells = SpatialGrid.BuildCells(new List<Point> { near }, Everyone, PositionOf, CellSize);
        var buffer = new List<Point> { stale };
        SpatialGrid.CollectInRadius(cells, new Vec3(0f, 0f, 0f), 10f, CellSize, PositionOf, buffer);
        CollectionAssert.AreEqual(new List<Point> { near }, buffer);
    }
}
```

**Verify**: Test (filtered) with `SpatialGridQueryTests`:
`grep -cE '^Passed!  - Failed:\s+0, Passed:\s+5, Skipped:\s+0, Total:\s+5,' TestResults/plan015-SpatialGridQueryTests.log`
prints `1`. These pass on the 3D grid by design (they characterise today's results). If
`CollectInRadius_MatchesABruteForceSphereScan` fails, the Step 3 refactor changed behaviour: STOP.

Commit (paths: `Main/Features/AdvancedCombat/SpatialGrid.cs`,
`TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs`), for example subject
`refactor(combat): v2.0.30 - pin SpatialGrid queries behind helpers`.

### Step 5 (RED): Pin the cell lookup count

Append these two methods inside `SpatialGridQueryTests` (after the last test):

```csharp
    [TestMethod]
    public void CollectInRadius_SixtyMetreQuery_ProbesSevenBySevenColumns()
    {
        var cells = SpatialGrid.BuildCells(new List<Point>(), Everyone, PositionOf, CellSize);
        int probes = SpatialGrid.CollectInRadius(cells, new Vec3(5f, 5f, 5f), 60f, CellSize, PositionOf, new List<Point>());
        Assert.AreEqual(49, probes, "the warg's 60 m scan should look up 7 x 7 columns, not 7 x 7 x 7 cells");
    }

    [TestMethod]
    public void CollectInRadius_TenMetreQuery_ProbesTwoByTwoColumns()
    {
        var cells = SpatialGrid.BuildCells(new List<Point>(), Everyone, PositionOf, CellSize);
        int probes = SpatialGrid.CollectInRadius(cells, new Vec3(5f, 5f, 5f), 10f, CellSize, PositionOf, new List<Point>());
        Assert.AreEqual(4, probes);
    }
```

**Verify**: Test (filtered) with `SpatialGridQueryTests`:
`grep -cE '^Failed!  - Failed:\s+2, Passed:\s+5, Skipped:\s+0, Total:\s+7,' TestResults/plan015-SpatialGridQueryTests.log`
prints `1`, and `grep -oE 'Expected:<[0-9]+>. Actual:<[0-9]+>' TestResults/plan015-SpatialGridQueryTests.log`
prints exactly the two lines `Expected:<49>. Actual:<343>` and `Expected:<4>. Actual:<8>` (in either
order). Any other result: STOP.

### Step 6 (GREEN): Key the grid on (x, y)

In `Main/Features/AdvancedCombat/SpatialGrid.cs`:

1. Change the three key types `(int, int, int)` to `(int, int)`: the `_grid` field declaration, the
   `BuildCells` return type and its `new Dictionary<...>`, and the `CollectInRadius` `cells` parameter.
2. In `BuildCells`, the key becomes
   `var key = ((int)Math.Floor(pos.x / cellSize), (int)Math.Floor(pos.y / cellSize));`
3. In `CollectInRadius`, delete the `minZ`/`maxZ` lines and the `for (int z ...)` line, change the
   lookup to `cells.TryGetValue((x, y), out List<T> cell)`, and put this comment directly above the
   `int probes = 0;` line:
   ```csharp
        // Cells are keyed on (x, y) only: a battlefield's vertical spread is a few metres, so a z axis
        // mostly added empty lookups (7 x 7 x 7 = 343 for the warg's 60 m scan, now 7 x 7 = 49). The
        // distance test below stays 3D, so the result is the same sphere.
   ```
   Keep `dz` in the distance test.
4. The buffer overload's doc comment (lines 93-98) stays; nothing else changes.

**Verify**:
- Build exits 0 with `0 Error(s)`.
- Test (filtered) with `SpatialGridQueryTests`:
  `grep -cE '^Passed!  - Failed:\s+0, Passed:\s+7, Skipped:\s+0, Total:\s+7,' TestResults/plan015-SpatialGridQueryTests.log`
  prints `1`.
- Test (filtered) with `SpatialGrid`: `grep -cE '^Passed!  - Failed:\s+0,' TestResults/plan015-SpatialGrid.log`
  prints `1`.
- `cd E:/repos/wt-015-warg-tick-costs && grep -c "int, int, int\|minZ\|maxZ" Main/Features/AdvancedCombat/SpatialGrid.cs` prints `0`.

Commit (paths: `Main/Features/AdvancedCombat/SpatialGrid.cs`,
`TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs`), for example subject
`perf(combat): v2.0.30 - key SpatialGrid cells on x and y only`.

### Step 7 (REFACTOR): Split the bone check's target loop out, fetch the attacker skeleton once

Behaviour is unchanged in this step: the per-target order (skeleton before range gate) stays.

(a) `Main/Features/AdvancedCombat/BoneCheck.cs`:

1. After the `_onExpiration` field (line 22) add:
   ```csharp
    // The attacker's tracked bone positions, refilled every tick (was a new list per tick).
    private readonly List<(sbyte, Vec3)> _agentBonePositions = new();
   ```
2. Replace `CheckBoneCollision()` (lines 53-127) with these three methods. The body of
   `CheckTargets` is the old target loop verbatim, except that it uses its own parameters:
   ```csharp
    protected bool CheckBoneCollision()
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            Logger.LogWarning($"Agent {_agent?.Name ?? "null"} is no longer valid for bone collision check");
            return false;
        }

        IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
        if (agentVisuals == null)
        {
            Logger.LogWarning($"Failed to get visuals for {_agent.Name}");
            return false;
        }

        Skeleton agentSkeleton = agentVisuals.GetSkeleton();
        if (agentSkeleton == null)
        {
            Logger.LogWarning($"Failed to get skeleton for {_agent.Name}");
            return false;
        }
        return CheckBoneCollision(agentVisuals, agentSkeleton);
    }

    /// <summary>
    /// The collision pass for an attacker the caller has already validated this tick, reusing the
    /// skeleton it fetched: MBAgentVisuals.GetSkeleton builds a new native wrapper on every call.
    /// </summary>
    protected bool CheckBoneCollision(IAgentVisualsAdapter agentVisuals, Skeleton agentSkeleton)
    {
        MatrixFrame agentGlobalFrame = agentVisuals.GetGlobalFrame();

        _agentBonePositions.Clear();
        int boneCount = agentSkeleton.GetBoneCount();
        foreach (sbyte bone in _boneIds)
        {
            if (bone < 0 || bone >= boneCount)
            {
                Logger.LogError($"Invalid bone index {bone} for agent {_agent.Name}");
                continue;
            }
            MatrixFrame agentBoneFrame = agentSkeleton.GetBoneEntitialFrameWithIndex(bone);
            Vec3 agentBoneGlobalPos = agentGlobalFrame.TransformToParent(agentBoneFrame.origin);
            _agentBonePositions.Add((bone, agentBoneGlobalPos));
        }

        return CheckTargets(agentGlobalFrame, _agentBonePositions);
    }

    /// <summary>
    /// Tests every held target against the attacker's bone positions. Returns false when a hit ends
    /// the check (stop on first hit), true otherwise. Internal for TAOM.Tests.
    /// </summary>
    internal bool CheckTargets(MatrixFrame agentGlobalFrame, List<(sbyte, Vec3)> agentBonePositions)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            IAgentAdapter target = _targets[i];

            if (target == null || !target.IsActive() || target.IsFadingOut())
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }

            IAgentVisualsAdapter targetVisuals = target.AgentVisuals;
            if (targetVisuals == null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }

            Skeleton targetSkeleton = targetVisuals.GetSkeleton();
            if (targetSkeleton == null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }
            MatrixFrame targetGlobalFrame = targetVisuals.GetGlobalFrame();
            sbyte boneId = FindBoneInRange(agentGlobalFrame, agentBonePositions, targetSkeleton, targetGlobalFrame);
            if (boneId != -1)
            {
                _targets.RemoveAt(i);
                _onCollisionCallback?.Invoke(_agent, target, boneId);
                if (_stopOnFirstHit)
                    return false;
            }
        }
        return true;
    }
   ```
   `FindBoneInRange` stays exactly as it is in this step.

(b) `Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs`:

1. Add `using TaleWorlds.Engine;` to the `using` block.
2. Replace the constructor comment (lines 15-23, the nine `//` lines between the parameter list and
   `: base(...)`) with:
   ```csharp
        // 2026-05-24 (#219): this argument once fed the base class's distance gate, capping reach at
        // about 0.84 m. It is now maxDuration, which this class never reads (Tick below ends the check
        // on the action window instead). The distance gate is BoneCheck's _maxRangeForCheck,
        // max(20, radius squared x 20) square metres: about 4.5 m for the warg's 0.5 and 1.0 radii.
   ```
3. Replace `Tick` (lines 31-59) with:
   ```csharp
    public override bool Tick(float dt)
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            _onExpiration?.Invoke();
            return false;
        }

        if (_targets == null || _targets.Count == 0)
        {
            _onExpiration?.Invoke();
            return false;
        }

        // One skeleton fetch per tick, handed to CheckBoneCollision: every GetSkeleton call builds a
        // new native wrapper (a ref-count call, a lock, a GCHandle and a finalizer).
        IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
        Skeleton agentSkeleton = agentVisuals?.GetSkeleton();
        if (agentSkeleton == null
            || _agent.GetCurrentAction(0) != _action
            || _agent.GetCurrentActionProgress(0) >= _actionProgressMax)
        {
            _onExpiration?.Invoke();
            return false;
        }

        if (_agent.GetCurrentActionProgress(0) >= _actionProgressMin)
        {
            if (!CheckBoneCollision(agentVisuals, agentSkeleton))
            {
                _onExpiration?.Invoke();
                return false;
            }
        }

        return true;
    }
   ```
   This keeps the old short-circuit order (targets checked before the skeleton is fetched) and the
   same expiry outcomes. The attacker liveness check inside the parameterless `CheckBoneCollision`
   is not needed here because `Tick` just made it.

**Verify**:
- Build exits 0 with `0 Error(s)`.
- Test (filtered) with `BoneCollisionServiceTests` (11 tests at `b2e387db`):
  `grep -cE '^Passed!  - Failed:\s+0, Passed:\s+11, Skipped:\s+0, Total:\s+11,' TestResults/plan015-BoneCollisionServiceTests.log`
  prints `1`.
- `cd E:/repos/wt-015-warg-tick-costs && grep -c "new();" Main/Features/AdvancedCombat/BoneCheck.cs`
  prints `1` (only the new field; the per-tick `new()` is gone).

Do not commit yet; Step 9 commits Steps 7 to 9 together.

### Step 8 (RED): Pin the target range gate

Create `E:\repos\wt-015-warg-tick-costs\TAOM.Tests\Features\AdvancedCombat\BoneCheckRangeGateTests.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// A warg's bite holds every agent captured within 20 m for the whole attack window, but only a
/// target whose visuals frame is inside the bone check's 20 square-metre gate (about 4.5 m) can be
/// hit. Fetching a target's skeleton builds a new native wrapper on every call, so the gate runs
/// first and a far target's skeleton is never fetched (plan 015). The attacker's bone math needs a
/// live skeleton, so these tests drive CheckTargets directly with no attacker bones. The substitute's
/// GetSkeleton returns null, so no native Skeleton is ever constructed here.
/// </summary>
[TestClass]
public class BoneCheckRangeGateTests
{
    // The warg's running bite: gate = max(20, 1 x 1 x 20) = 20 square metres.
    private const float WargRunningRadius = 1.0f;

    private sealed class Probe : BoneCheck
    {
        public Probe(List<IAgentAdapter> targets)
            : base(Substitute.For<IAgentAdapter>(), targets, new List<sbyte> { 17 }, 999f, WargRunningRadius, true, null, null) { }

        public List<IAgentAdapter> Targets => _targets;
    }

    private static readonly List<(sbyte, Vec3)> NoAttackerBones = new();

    private static MatrixFrame FrameAt(float x, float y, float z) => new MatrixFrame(Mat3.Identity, new Vec3(x, y, z));

    private static (IAgentAdapter target, IAgentVisualsAdapter visuals) LiveTargetAt(float x, float y, float z)
    {
        var visuals = Substitute.For<IAgentVisualsAdapter>();
        visuals.GetGlobalFrame().Returns(FrameAt(x, y, z));
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.AgentVisuals.Returns(visuals);
        return (target, visuals);
    }

    [TestMethod]
    public void CheckTargets_TargetBeyondTheRangeGate_NeverFetchesItsSkeleton()
    {
        var (target, visuals) = LiveTargetAt(10f, 0f, 0f); // 100 square metres; the gate is 20
        var sut = new Probe(new List<IAgentAdapter> { target });

        bool keepChecking = sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.IsTrue(keepChecking);
        visuals.DidNotReceive().GetSkeleton();
    }

    [TestMethod]
    public void CheckTargets_TargetBeyondTheRangeGate_StaysATarget()
    {
        var (target, _) = LiveTargetAt(10f, 0f, 0f);
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        CollectionAssert.AreEqual(new List<IAgentAdapter> { target }, sut.Targets,
            "a far target may close in on a later frame of the attack window");
    }

    [TestMethod]
    public void CheckTargets_TargetOnTheRangeGateEdge_FetchesItsSkeleton()
    {
        var (target, visuals) = LiveTargetAt(4f, 2f, 0f); // exactly 20 square metres: inside
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        visuals.Received(1).GetSkeleton();
    }

    [TestMethod]
    public void CheckTargets_TargetInsideTheRangeGateWithNoSkeleton_IsDropped()
    {
        var (target, _) = LiveTargetAt(1f, 0f, 0f);
        var sut = new Probe(new List<IAgentAdapter> { target });

        bool keepChecking = sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.IsTrue(keepChecking);
        Assert.AreEqual(0, sut.Targets.Count);
    }

    [TestMethod]
    public void CheckTargets_InactiveTarget_IsDroppedBeforeItsVisualsAreRead()
    {
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(false);
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.AreEqual(0, sut.Targets.Count);
        _ = target.DidNotReceive().AgentVisuals;
    }
}
```

**Verify**: Test (filtered) with `BoneCheckRangeGateTests`:
`grep -cE '^Failed!  - Failed:\s+2, Passed:\s+3, Skipped:\s+0, Total:\s+5,' TestResults/plan015-BoneCheckRangeGateTests.log`
prints `1`, and `grep -E '^  Failed ' TestResults/plan015-BoneCheckRangeGateTests.log` names the two
failures: `CheckTargets_TargetBeyondTheRangeGate_NeverFetchesItsSkeleton` (NSubstitute reports
one `GetSkeleton()` call received) and `CheckTargets_TargetBeyondTheRangeGate_StaysATarget` (the
list is empty, because today a far target's null skeleton removes it). A `TypeInitializationException`
naming `NativeObject` or `ActionIndexCache`, or any other count: STOP.

### Step 9 (GREEN): Range-gate targets before fetching their skeletons

In `Main/Features/AdvancedCombat/BoneCheck.cs`:

1. In `CheckTargets`, replace the block from `Skeleton targetSkeleton = targetVisuals.GetSkeleton();`
   through `sbyte boneId = FindBoneInRange(agentGlobalFrame, agentBonePositions, targetSkeleton, targetGlobalFrame);`
   with:
   ```csharp
            MatrixFrame targetGlobalFrame = targetVisuals.GetGlobalFrame();
            // Range gate BEFORE the skeleton: GetSkeleton builds a new native wrapper on every call (a
            // ref-count call, a lock, a GCHandle and a finalizer), and most agents captured at 20 m are
            // outside this gate on any given frame. A target out of range stays for a later frame.
            if ((targetGlobalFrame.origin - agentGlobalFrame.origin).LengthSquared > _maxRangeForCheck)
                continue;

            Skeleton targetSkeleton = targetVisuals.GetSkeleton();
            if (targetSkeleton == null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }
            sbyte boneId = FindBoneInRange(agentBonePositions, targetSkeleton, targetGlobalFrame);
   ```
2. Change `FindBoneInRange` to drop the attacker frame and its gate (the gate now lives in
   `CheckTargets`, with the same comparison and the same origins):
   ```csharp
    protected sbyte FindBoneInRange(List<(sbyte boneId, Vec3 position)> agentBonePositions, Skeleton targetSkeleton, MatrixFrame targetGlobalFrame)
    {
        int targetBoneCount = targetSkeleton.GetBoneCount();
        for (int i = 0; i < targetBoneCount; i++)
        {
            MatrixFrame targetBoneFrame = targetSkeleton.GetBoneEntitialFrameWithIndex((sbyte)i);
            Vec3 targetBoneGlobalPos = targetGlobalFrame.TransformToParent(targetBoneFrame.origin);
            foreach (var (boneId, agentBonePos) in agentBonePositions)
            {
                float distanceSquared = (targetBoneGlobalPos - agentBonePos).LengthSquared;
                if (distanceSquared <= _collisionRadiusSquared)
                    return (sbyte)i;
            }
        }
        return -1;
    }
   ```

The only observable difference from `b2e387db`: a target outside the gate whose skeleton would have
come back null is no longer dropped from the list; it is re-examined next frame. Which targets can be
hit, and in which order, is unchanged by this step.

**Verify**:
- Build exits 0 with `0 Error(s)`.
- Test (filtered) with `BoneCheckRangeGateTests`:
  `grep -cE '^Passed!  - Failed:\s+0, Passed:\s+5, Skipped:\s+0, Total:\s+5,' TestResults/plan015-BoneCheckRangeGateTests.log`
  prints `1`.
- Test (filtered) with `BoneCollisionServiceTests`:
  `grep -cE '^Passed!  - Failed:\s+0, Passed:\s+11, Skipped:\s+0, Total:\s+11,' TestResults/plan015-BoneCollisionServiceTests.log`
  prints `1`.
- `cd E:/repos/wt-015-warg-tick-costs && grep -c "_maxRangeForCheck" Main/Features/AdvancedCombat/BoneCheck.cs`
  prints `3` (declaration, constructor, the gate in `CheckTargets`).

Commit (paths: `Main/Features/AdvancedCombat/BoneCheck.cs`,
`Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs`,
`TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs`), for example subject
`perf(combat): v2.0.30 - range-gate bone targets before skeletons`, with a body line
`Not-tested: BoneCheckDuringAnimation.Tick (ActionIndexCache needs the engine).`

### Step 10: Update the two feature docs

(a) `docs/features/warg-combat.md`:
- Replace line 126 (starts `- **Other planned tests:**`) with:
  `- **Tick-cost tests (plan 015):** \`TAOM.Tests/Features/Warg/WargTickCostTests.cs\` pins, in the IL, that no per-tick node method reaches \`IoC.Resolve\` and that the grid scans use the buffer overload; \`TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs\` checks the grid query against a brute-force sphere scan through its generic helpers; \`BoneCheckRangeGateTests.cs\` pins that a target's skeleton is fetched only inside the range gate.`
- Replace lines 150-152 (the `SpatialGrid`, `BoneCheck` and `IoC.Resolve in BT evaluators` bullets)
  with:
  ```markdown
  - **SpatialGrid**: cells are keyed on (x, y) only (the distance test stays 3D), so the 60 m "no enemy close" scan looks up 49 cells instead of 343; every warg node scans into a reused buffer through the zero-allocation overload.
  - **BoneCheck**: the attacker's bone positions reuse one list and its skeleton is fetched once per tick; a target's skeleton is fetched only inside the 20 square-metre gate (about 4.5 m), because `MBAgentVisuals.GetSkeleton()` builds a new finalizable native wrapper on every call.
  - **IoC.Resolve in BT nodes**: none; `WargBehaviorTree.BuildTree` resolves once per tree and injects the services through the node constructors (amendment 2); `WargRiderHandManager.Tick` reads `WargConfig.IsWargMonster` and resolves nothing.
  ```
(b) `docs/features/advanced-combat.md`:
- Line 50: replace `3D cell-hash grid for fast radius queries; singleton pattern` with
  `Cell grid keyed on (x, y) for fast radius queries (the distance test stays 3D); singleton pattern`.
- Line 74: replace the whole line with
  `- \`CustomAttacksUtils\` needs a live engine for most paths. \`SpatialGrid\`'s query logic is covered by \`SpatialGridQueryTests.cs\` through its generic helpers; its \`Agent\`-typed wrappers are not.`
- Line 76: replace the whole line with
  `- \`BoneCheck\`'s bone math uses live \`Skeleton\` matrices and is not unit-testable; its per-target range gate is (\`BoneCheckRangeGateTests.cs\`).`

(The backslashes above only escape the backticks for this plan; the file gets plain backticks.)

**Verify**:
- `cd E:/repos/wt-015-warg-tick-costs && grep -c "plan 015" docs/features/warg-combat.md` prints `1`, and
  `grep -c "keyed on (x, y)" docs/features/advanced-combat.md docs/features/warg-combat.md` prints `1` for each file.
- `cd E:/repos/wt-015-warg-tick-costs && git diff b2e387db -- docs/ | python -X utf8 -c "import sys; print(sum(1 for l in sys.stdin if l.startswith('+') and ('\u2013' in l or '\u2014' in l)))"`
  prints `0`.
- The Docs command prints `- Dead links: **0**`.

Commit (paths: the two docs), for example subject
`docs(warg): v2.0.30 - record the warg tick-cost changes`.

### Step 11: Final verification and report

Run every Done-criteria command. Then put these in your final report (do not write them to files):

- **CHANGELOG entry** for the orchestrator to add (no dashes): "Warg battles do less work per frame.
  The warg behaviour tree no longer looks services up in the IoC container on every tick, its three
  enemy scans reuse buffers instead of allocating a list each call, the spatial grid looks up 49
  cells for a 60 m scan instead of 343, and a live bite no longer builds a native skeleton wrapper
  for every agent within 20 m on every frame, only for those within reach. The set of targets a bite
  can hit is unchanged; when two targets are in reach on the same frame at slightly different
  heights, which one takes the hit can differ."
- **Lesson** to propose for `docs/reviews/lessons/adapters-taleworlds-api.md` (the orchestrator
  appends it): "`MBAgentVisuals.GetSkeleton()` marshals a new finalizable `Skeleton` wrapper on every
  call (native ref count, a lock, a GCHandle); in per-frame code, gate on the visuals frame first and
  fetch a skeleton once per tick (plan 015, `BoneCheckRangeGateTests`)."
- The commit hashes and subjects, the drift-check output, the Step 0 failure list, the final test
  counts and failure names, and the owed in-game check (Maintenance notes).

## Test plan

- **New tests** (21):
  - `TAOM.Tests/Features/Warg/WargTickCostTests.cs` (9): no `IoC.Resolve` reachable from
    `PeriodicallyCheckIfCanAttackAnyone.Evaluate`, `CheckOnceIfCanAttackEnemy.Evaluate`,
    `WargAiControlledIsNotFacingEnemy.Evaluate`, `WargAttackTask.Execute`, `WargRiderHandManager.Tick`,
    `NoEnemyCloseDecorator.Evaluate` (a guard; it passed before); the buffer overload in the three
    scanning nodes.
  - `TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs` (7): brute-force equivalence over
    600 points, 5 radii (0.5, 10, 20, 60, 75 m) and 25 centres each, with z spread across several
    20 m layers and negative coordinates; the inclusive boundary; same column at different heights;
    the include filter; buffer clearing; the 49 and 4 lookup counts.
  - `TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs` (5): the (target state x gate)
    cells: inactive (dropped, visuals never read); live beyond the gate (skeleton never fetched,
    stays a target); live exactly on the gate (skeleton fetched); live inside with no skeleton
    (dropped).
- **Structural patterns**: `TAOM.Tests/Features/Warg/WargOffThreadTests.cs` (IL scans) and
  `TAOM.Tests/Features/AdvancedCombat/BoneCollisionServiceTests.cs` (a `BoneCheck` subclass with
  substitutes).
- **Untestable here** (commit trailer `Not-tested:`): `BoneCheckDuringAnimation.Tick` (its
  `ActionIndexCache` field's static constructor needs the engine, per the note at
  `BoneCollisionServiceTests.cs:202-208`); the bone math with a real skeleton; that the node
  constructors' resolves succeed in game (they run after `IoC.Configure`, like
  `WargMissionBehavior`'s own); the frame-time and GC saving. All need an in-game session.
- **Verification**: Test (full) prints a banner whose `Total:` is 10,260 (10,239 at `b2e387db` plus
  the 21 new tests; `grep -cE 'Total:\s+10260,' TestResults/plan015-full.log` prints `1`), and every
  `  Failed ` line names a method in the baseline rule's set. The full run's banner shows only
  totals, so the three per-class filtered runs in Done criteria are the proof that the 21 new tests
  pass.

## Done criteria

ALL must hold (every command starts with `cd E:/repos/wt-015-warg-tick-costs && `):

- [ ] Build exits 0 with `0 Error(s)`.
- [ ] Test (full): `grep -cE 'Total:\s+10260,' TestResults/plan015-full.log` prints `1`, and every
      method `grep -E '^  Failed ' TestResults/plan015-full.log` prints is one of the 2 Armory tests
      or on the Step 0 list (an empty list is fine; a non-zero exit from this run alone is not a failure).
- [ ] Fresh filtered runs, each checked with `grep -cE` printing `1`:
      `WargTickCostTests` matches `'^Passed!  - Failed:\s+0, Passed:\s+9, Skipped:\s+0, Total:\s+9,'`;
      `SpatialGridQueryTests` matches `'^Passed!  - Failed:\s+0, Passed:\s+7, Skipped:\s+0, Total:\s+7,'`;
      `BoneCheckRangeGateTests` matches `'^Passed!  - Failed:\s+0, Passed:\s+5, Skipped:\s+0, Total:\s+5,'`
      (the full run's banner shows only totals, so these are the per-class proof).
- [ ] `grep -rn "AdapterFactory => IoC.Resolve" Main/Features/Warg` prints nothing.
- [ ] `grep -rn "GetNearAliveAgentsInRange(60, agent);\|GetNearAliveAgentsInRange(10, warg);" Main/Features/Warg` prints nothing.
- [ ] `grep -c "int, int, int" Main/Features/AdvancedCombat/SpatialGrid.cs` prints `0`.
- [ ] `grep -n "WargRiderHandManager.Tick(_adapterFactory);" Main/Features/Warg/WargMissionBehavior.cs` prints exactly one line.
- [ ] `git -C E:/repos/wt-015-warg-tick-costs diff --name-only b2e387db` lists exactly the 14 paths
      in Scope "In scope", nothing else.
- [ ] `git -C E:/repos/wt-015-warg-tick-costs status --porcelain` prints nothing (all committed).
- [ ] Every commit subject is at most 72 characters (length command in Git workflow), and
      `git -C E:/repos/wt-015-warg-tick-costs log b2e387db..HEAD --format=%B | grep -c Co-Authored-By` prints `0`.
- [ ] `git -C E:/repos/wt-015-warg-tick-costs log b2e387db..HEAD --format=%b | awk 'length($0)>72{n++} END{print n+0}'` prints `0`.
- [ ] The Data command's error count is no higher than Step 0's; the Docs command prints `- Dead links: **0**`.
## STOP conditions

Stop and report back (do not improvise) if:

- Any Step 0 excerpt check does not print the expected count, or an excerpt in "Current state" does
  not match your worktree.
- A RED step fails with anything other than the counts it names (for example Step 1 reports fewer
  than 8 failures, which would mean someone already changed a node).
- `CollectInRadius_MatchesABruteForceSphereScan` fails at any point: the grid no longer returns the
  same sphere. Do not loosen the test (no tolerance, no fewer points).
- A test throws `TypeInitializationException` for `TaleWorlds.DotNet.NativeObject`,
  `TaleWorlds.Engine.Skeleton` or `ActionIndexCache`: a test touched the engine. Do not `[Ignore]` it
  or make it `Assert.Inconclusive`; report the stack.
- The fix appears to need `AgentAdapter.cs`, `IAgentAdapter.cs`, `BehaviorTreeAgentComponent.cs`,
  `AdvancedCombatBehavior.cs`, `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj` or any file
  outside Scope, or a change to `WargMissionBehavior`'s constructor signature.
- You find a second caller of `WargRiderHandManager.Tick`, `BoneCheck.CheckBoneCollision` or
  `FindBoneInRange` that this plan does not list (`git grep` in W).
- Any existing test under `TAOM.Tests/Features/Warg/`, `TAOM.Tests/Features/AdvancedCombat/`,
  `TAOM.Tests/Features/Spider/` or `TAOM.Tests/Adapters/` newly fails (map each failing method to
  its file with the `git grep "void <MethodName>("` command in "Reading `dotnet test` output").
- A step's verification fails twice after a reasonable fix attempt.
- `git worktree add` fails because `E:/repos/wt-015-warg-tick-costs` or the branch
  `plan-015-warg-tick-costs` already exists (for example on a re-run). Report it; do not delete or
  reuse either.
- A Read, Edit, Write or `cd` into W is denied by permissions (W is outside the project directory).
- A commit is denied by a hook. The PreToolUse commit hooks (for example
  `check-changelog-changed.sh`) read `CLAUDE_PROJECT_DIR`, the main checkout, not W, so another
  session's staged files there (it has `.claude/` edits in flight) can deny your commit. Expect this
  to fire if that session stages. Report the hook's message verbatim; do not retry around it.
- In each of the last three cases, never fall back to editing, staging or committing under
  `E:\repos\TAOM`.

## Maintenance notes

- **Owed in-game check** (label `triage-needs-ingame` when the issue closes): a Custom Battle with
  warg riders on both the player's side and the enemy's (say 40 wargs a side). Check that (1) wargs
  still engage, bite and knock down enemies, and still sometimes whiff; (2) the player riding a warg
  still gets the rider hand pose; (3) the `taom_debug_*.log` has no new `[Warg] Tree build failed`
  or `[Warg] tree attach threw` line (a node constructor's resolve failing would surface there);
  (4) optionally, compare `[MissionPerf]` heartbeat `gc0`/`gc1` per 5 s window against a pre-change
  build in the same battle (`grep "\[MissionPerf\]"`); no target number is claimed.
- **What a reviewer should probe**: that every per-tick path still validates a held target with
  `IsActive()` (which includes `AgentSlotIdentity.IsCurrentOccupant`) BEFORE any visuals read; that
  the moved gate compares the same two origins (target visuals frame vs attacker visuals frame) with
  the same `>`; that `BoneCheckDuringAnimation.Tick` still expires in the same cases; that no node
  field is `static`.
- **Order within a column**: with (x, y) keys, two agents in the same 20 m (x, y) column now come
  back in rebuild order (`Mission.AllAgents` order) instead of lower height band first. They were in
  different bands whenever a multiple of 20 m lies between their heights, so ADJACENT bands reorder
  too: two agents at z = 19.5 and z = 20.5 are enough. The SET of agents returned is identical. The
  order matters only to a bite with `stopOnFirstHit` (the warg's), whose target list is built from
  this grid (`AgentAdapter.cs:207`): when two targets are within bone reach on the same frame and
  straddle a z = 20k plane, a different one of them may take the hit. Any slope crossing such a
  height can produce this. It is a tie-break change, not a change in who can be hit.
- **Interactions**: `SpiderEngageDecorator` and `AgentAdapter.RadialStrike` share the grid and
  benefit from the (x, y) keys with no change. Any future creature node should follow the pattern
  here (instance-field services resolved at construction, a reused scan buffer).
- **Deferred, separate findings**: dropping same-team targets at `CustomAttack` capture (an ally
  currently ends a warg's swing; a gameplay decision); the `/ 1000` integer division in
  `BehaviorTreeAgentComponent.cs:65`; `Selector.Prepare` allocating two lists per entry
  (`Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs:134-135`); the 2 s and 0.1 s grid rebuild cadences (June PERF-01); the
  stale "Every 5 ticks" line in `docs/features/warg-combat.md:153`; `WargMissionBehavior` at 207 lines
  (ADR-002); `WargRiderHandManager.UpdateWargRiderHandle` calling `Mission.GetMissionBehavior` every
  frame.
