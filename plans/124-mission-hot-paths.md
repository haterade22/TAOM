# Plan 124: Make battle hot paths cheaper: a lock-free formation prefix, cheaper creature ticks and a capped tree walk

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do not edit `plans/README.md` (the integrator records status and test
> counts); put the "Hand-off report" numbers in your final message. Two parts on one branch, each
> ending green, strictly in this order: **Part A** (creature battles), then **Part B** (Mixed
> Formations). Their code is disjoint, but Step A2 brings the `ReferenceIdentity` helper both
> need. Inside Part A the steps are strictly ordered too.
>
> **Drift check (run first, Step A0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Adapters/AgentAdapter.cs Main/Adapters/IAgentAdapter.cs Main/Adapters/FormationAdapter.cs Main/Adapters/IFormationAdapter.cs Main/Features/AdvancedCombat Main/Features/Warg Main/Features/Spider/BehaviorTreeElements Main/Features/ElephantLike/BehaviorTreeElements Main/BehaviorTreeWrapper Main/BehaviorTrees Main/Features/MixedFormations Main/Core TAOM.Tests/Features/AdvancedCombat TAOM.Tests/Features/Warg TAOM.Tests/Features/MixedFormations TAOM.Tests/BehaviorTreeWrapper TAOM.Tests/BehaviorTrees TAOM.Tests/Core .claude/rules/csharp-architecture.md .claude/rules/harmony-patches.md docs/features/advanced-combat.md docs/features/warg-combat.md docs/features/elephant.md docs/features/bannerlord-together-compat.md docs/features/mixed-formations.md docs/reference/harmony-patch-registry.md`
> Earlier WPs changed some of these on purpose (WP106: `SpatialGrid.cs`, the behaviour-tree
> wrapper, `WargMissionBehavior.cs`, the two rule files, `warg-combat.md`; WP111 and WP123:
> MixedFormations settings files and `mixed-formations.md`; many WPs: the registry). The decisive
> check is the blob comparison in Step A0.4, not this list.

## Status

- **Priority**: P2
- **Effort**: M overall. Part A: M (one squashed transplant, one cherry-pick plus one helper file
  from another commit, one two-hunk hand port, one TDD change). Part B: S to M (one cherry-pick
  with a one-line hand-merge, test and doc adaptations)
- **Risk**: MED (mission hot paths, partly on engine worker threads; cost cuts that keep
  behaviour, except the differences listed under "Maintenance notes")
- **Depends on**: WP106 (`plans/106-offthread-and-tick-safety.md`, the #634 port this builds on)
  and WP123 (edits MixedFormations settings and doc lines next to Part B); through them WP101-105
- **Category**: perf
- **Wave / lane**: wave 3, lane B
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (nothing to
  wire: no new module, no `IoC.cs` or `SubModule.cs` edit), D8 (subjects carry `v2.0.28`), D9 (no
  CI; plan 010's `RequiresGame` test category is not assumed on 1.4.x), D10 (an ordinary session
  executes this plan), D16 (no locked RNG: `9f58c58de` is not ported), D19 (git only through the
  Bash tool); 1.5.x `plans/_audit/2026-09-23-opus/DECISIONS.md` #28 (the maintainer chose the
  1.4.5 port of plan 015)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. (During planning the 1.5.x tip moved to `e65cfda37`; the newer commits
  are features, see "Maintenance notes", and change none of the commits ported here.)
- **Issue**: port of #659 (plan 015, warg tick costs), #715 (plan 033, creature allocations) and
  #714 (plan 032, the lock-free formation path); Step A3 carries shared hunks of the #730 commits.
  Step A4 is new work with no issue yet: the orchestrator creates the issue before A4 lands.

## Why this matters

Players on the public 1.4.8 build report battles that stutter and freeze (brief evidence item 3).
This plan does not fix those freezes (WP106 to WP113 own them); it removes per-frame and
per-worker work from every battle and one unbounded loop on the mission thread:

1. **Mixed Formations (RUNTIME-PERF-05).** v1.4.8 calls `Formation.GetOrderPositionOfUnit` for
   every AI unit about twice a second on the `TWParallel` workers (`Agent.cs:1592`, `:4684-4726`),
   plus main-thread bursts (`:3708`, `OrderController.cs:1443-1452`). On 1.4.x each field-battle
   call of `Patch30` allocates a `FormationAdapter`, reads the MCM switch, evaluates the cavalry
   query (once expired, `QueryData.Value` re-evaluates an 11-member sync group with no lock,
   ``QueryData`1.cs:18-37``) and takes one global lock, before learning whether the formation has
   a layout. Almost none do: layouts go to the player's mixed formations.
2. **Creature battles (RUNTIME-PERF-10).** Warg nodes resolve services per evaluation; every scan
   allocates a `List<Agent>`; a live bite builds a native `Skeleton` wrapper (ref count, global
   lock, `GCHandle`, finalizer) per captured target before any range gate; a 60 m scan does 343
   cell lookups; the grid rebuilds every 2 s in every mission (`SubModule.cs:1757`, `:1803-1804`
   add `BehaviorTreeMissionLogic` and `AdvancedCombatBehavior` to every mission); tree nodes read
   `DateTime.Now`; the selector allocates two lists per re-entry; and every agent removal
   allocates boxed argument arrays in the tree dispatcher.
3. **An uncapped walk (FREEZE-THREADING-09).** `BehaviorTree.RunTree` loops until the walk returns
   to the root or a node waits. No reachable spin is known, but a future node bug would freeze
   the mission thread with no exception, the hardest report to diagnose.

Magnitudes are unmeasured on both lines (E4's missions had no creature); WP117's instruments can
measure before and after (optional in-game check 8).

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
  (`BANNERLORD_GAME_DIR`). Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101, plus every WP merged before this one (WP106
  and WP123 must be among them).
- Worktree: `C:\Users\mikew\source\repos\taom-port\124`, branch `port148/124-mission-hot-paths`.
  A `cd` does not persist between Bash calls: start EVERY command with
  `cd /c/Users/mikew/source/repos/taom-port/124 &&`.
- Commit message files go in `C:\Users\mikew\source\repos\taom-port\` (outside the worktree).
- Python: `python` (or `/c/Python314/python.exe`). Git config here: `core.autocrlf=true` (the
  index holds LF, the worktree CRLF); `git apply` and `git cherry-pick` handle that.

### Files and roles

| File | Role in this WP |
|---|---|
| `Main/Features/Warg/WargBehaviorTree.cs`, four nodes in `BehaviorTreeElements/`, `WargRiderHandManager.cs`, `WargConfig.cs`, `Main/Adapters/{AgentAdapter,IAgentAdapter}.cs` | A1: services resolved once per tree and injected; reused scan buffers; warg-ness from the mount's `Monster`; `IsWarg()` deleted |
| `Main/Features/AdvancedCombat/{BoneCheck,BoneCheckDuringAnimation,SpatialGrid}.cs` | A1: range gate before each target's skeleton, one attacker skeleton and progress read per tick, (x, y) grid cells; A2: two reused grid maps, O(cell) removal, rebuilds skipped while unread |
| `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs`, new `CallbackSkipLedger.cs`, `BehaviorTreeAgentComponent.cs` | A2: callbacks return early when no tree listens; A3: schedule copy without `AddRange`, slot checks only when due |
| `SleepTask.cs`, `WaitNSecondsTickDecorator.cs`, the ElephantLike, Spider and Warg cooldown and rage nodes; `Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs` | A2: tree clocks on `DateTime.UtcNow`; `Selector.Prepare` reuses its lists |
| new `Main/Core/Collections/ReferenceIdentity.cs` | A2: reference-equality comparer that `SpatialGridReuseTests` (A2) and `FormationLayoutService` (B1) use |
| `Main/BehaviorTrees/BehaviorTreesCore.cs` | A4 (new): `RunTree` capped |
| `Main/Features/MixedFormations/{FormationLayoutService,IFormationLayoutService}.cs`, `Hooks/Patch30_FormationGetOrderPositionOfUnit.cs`, `FormationAdapter.cs` (comment) | B1: lock-free lookup keyed by reference, reused adapter, fallback accounting |
| the two `.claude/rules` files; `docs/features/{advanced-combat,warg-combat,elephant,bannerlord-together-compat,mixed-formations}.md`, `docs/reference/harmony-patch-registry.md` | rule lines that document the ported mechanisms (per-port exception); feature docs travel with features; the registry is the crash-triage lookup |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`, read this session)

Mixed Formations:

```text
Patch30_FormationGetOrderPositionOfUnit.cs:28,38  IsFieldBattle check; var service = _service ??= IoC.Resolve<IFormationLayoutService>();
Patch30_FormationGetOrderPositionOfUnit.cs:41,46  var formation = new FormationAdapter(__instance); ... service.ComputeUnitPlanePosition(formation, agentIndex, agentIsRanged);
FormationLayoutService.cs:19-20  Dictionary<object, FormationLayoutType> _layoutByFormation = new(); Dictionary<object, SlotAssignment> _assignmentCache = new();
FormationLayoutService.cs:65-74  ComputeUnitPlanePosition: IsEnabled, IsHolding, OrderPositionIsValid, RepresentativeIsCavalry, then lock (_lock)
FormationLayoutService.cs:97     var unitInterval = System.Math.Max(1f, formation.Interval + 1f);
FormationAdapter.cs:37,39-40     FormationKey => _formation; RepresentativeIsCavalry => ... _formation.QuerySystem.IsCavalryFormation;
```

`FormationLayoutService` is the only implementer of `IFormationLayoutService`. 1.4.x has no
`LayoutPositioner.UnitPitch`, no `IFormationAdapter.UnitDiameter` and no `ReferenceIdentity`.

Creatures:

```text
PeriodicallyCheckIfCanAttackAnyone.cs:15,45  private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();
PeriodicallyCheckIfCanAttackAnyone.cs:23,52  List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg);
WargAiControlledIsNotFacingEnemy.cs:16       private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();
WargAttackTask.cs:30-31                      IoC.Resolve<IMissionAdapterFactory>()... IoC.Resolve<IWargAttackService>().WargAttack(wargAdapter);
WargRiderHandManager.cs:14                   ...GetAgentAdapter(Agent.Main.MountAgent).IsWarg()   (the only IsWarg() caller)
BoneCheck.cs:30    _maxRangeForCheck = Math.Max(20f, _collisionRadiusSquared * 20f);
BoneCheck.cs:76    List<(sbyte, Vec3)> agentBonePositions = new();      (per tick)
BoneCheck.cs:109   Skeleton targetSkeleton = targetVisuals.GetSkeleton(); (every target, before the range gate)
SpatialGrid.cs:20  private Dictionary<(int, int, int), List<Agent>> _grid = new();
AdvancedCombatBehavior.cs:16,44  private const float GridUpdateInterval = 2f; ... SpatialGrid.Instance.UpdateGrid(Mission.Current.AllAgents);
BehaviorTreesNodes.cs:134-135  Selector.Prepare: currentlyExecutableChildren = new List<BTNode>(); childrenWithTasks = new List<BTNode>();
BehaviorTreesCore.cs:52-79     RunTree: do { CurrentNode = CurrentNode.HandleExecute(); if (CurrentNode.ShouldExitTree()) return; } while (CurrentNode != RootNode);
```

Every tree-clock `DateTime.Now` in `Main` on 1.4.x (`git grep`), all converted by Step A2:
`WaitNSecondsTickDecorator.cs:21,24`, `SleepTask.cs:22,30`, `ElephantLikeAttackOffCooldownDecorator.cs:37`,
`ElephantLikeAttackTasks.cs:41` (comment), `:55`, `SpiderAttackOffCooldownDecorator.cs:36`,
`SpiderAttackTaskBase.cs:34` (comment), `:42`, `SetRageAttackTimer.cs:21`,
`WargCanNotFindEnemyDecorator.cs:29`. These nodes are the only stamp writers, so writer and reader
clocks change together; the other `DateTime.Now` uses (logger, dumps) are not tree clocks.

### State after WP106 and WP123 (what HEAD must hold)

WP106 ported `f75288ebf` (#634) cleanly, so these files are byte-identical to the 1.5.x files
plans 015 and 033 were written against (Step A0.4 proves it). `SpatialGrid.cs` parks `Remove`
through `_pendingRemovals.RunOrDefer(...)` and still keys on `(int, int, int)`.
`BehaviorTreeMissionLogic.cs` copies the schedule with `_tickScratch.AddRange(_scheduled);`
(`:137`), builds `new object[]` arguments on every `OnAgentRemoved` (`:289`, `:293`), and only
`Subscribe`/`UnSubscribe` write its private `actions` map. `BehaviorTreeAgentComponent.cs`
`TickOnMissionThread` (`:59`) checks `Agent.IsActive()` and `AgentSlotIdentity.IsCurrentOccupant`
(`:63`) before adding `dt`. `MissionThreadGuard.NoteCall` returns `true` off the main thread. The
warg nodes, `BoneCheck*.cs` and `AgentAdapter.cs`/`IAgentAdapter.cs` equal the plan 015 base
`7f02fc8d3`. No other earlier-wave WP edits a code file this plan touches (every PORT row in
`classified.jsonl` checked): WP107 leaves `ElephantLikeAttackTasks.cs` (it skips `c79a58521`'s
#643 rider blow) and WP113 leaves the MixedFormations pitch out (`25995dc91`, OUT).

### What the ported 1.5.x commits carry (read this session)

- **Plan 015**: the linear series `7f02fc8d3..516380d2e`, merged by `59b90e2ac` (#659) with no
  resolution in these paths. Net diff: 12 code files, 6 test files (5 new), the 2-line rule in
  `csharp-architecture.md`, two feature docs, 1.5.x records. Later commits replace earlier ones
  (`87f9f862e`'s per-node resolves by `23f6f85bd`, its `Tick(factory)` plumbing by `fe8f30c7a`,
  the IL order test by `5d9d4cc97`) and the net diff never touches `WargMissionBehavior.cs`:
  hence one squashed end-state transplant.
- **`efe326242`** (plan 033): 13 code files (1 new), 8 test files (7 new), 4 feature docs. Its
  code applies cleanly after A1 (planning simulation); only four namespaces in one test's prefix
  list are 1.5.x-specific (its spider-hunt experiment was reverted inside plan 033). It keeps the
  two V05 section 4 invariants: `Remove` stays parked (`efe326242:SpatialGrid.cs:112`) and an
  off-thread query never rebuilds (`:196-198`).
- **`a498e357e`**, **`70320bbe1`**: Race Abilities commits (OUT) with one shared hunk each (the
  `TickOnMissionThread` reorder; the element-by-element schedule copy plus two IL tests).
- **`13f34c1cb`** (plan 032): the service, Patch30, the interface, a `FormationAdapter` comment,
  tests, two rule phrases, docs, and a CreatureBandits reorder (OUT). Its one-line conflict is the
  pitch (`25995dc91` made it `LayoutPositioner.UnitPitch` on 1.5.x). It and `efe326242`'s
  `SpatialGridReuseTests.cs` need `TAOM.Core.Collections.ReferenceIdentity` from `30abf550b`, whose
  notes say to leave that file (they missed these uses); without it Step A2 fails to compile with
  CS0234, so Step A2 brings it.

### Engine facts the ported code binds (installed v1.4.8 via taom-src, read this session)

From `C:\Users\mikew\.taom-src\v1.4.8\`:

```csharp
public sealed class Formation : IFormation                                   // Formation.cs:14
public WorldPosition GetOrderPositionOfUnit(Agent unit)                      // :1239 (Patch30 target)
public override int GetHashCode() { return (int)(Team.TeamIndex * 10 + FormationIndex); }  // :2423-2425
public float Interval  // :512-522, reads CalculateHasSignificantNumberOfMounted (:524-534) -> QuerySystem.CavalryUnitRatio
// QuerySystem.Expire(): :748, :768, OnMassUnitTransferEnd :1810-1813, Split :1943/:1954, TransferUnits :1969/:1982-1983
public Vec2 GetCurrentGlobalPositionOfUnit(Agent unit, bool blendWithOrderDirection)  // :1278, reads Interval at :1284
public bool IsCavalryFormation => _isCavalryFormation.Value;                 // FormationQuerySystem.cs:195
public bool IsCavalryFormationReadOnly => _isCavalryFormation.GetCachedValueUnlessTooOld();  // :197
// _cavalryUnitRatio lifetime 2.5f (:431); SetupSyncGroup(... _cavalryUnitRatio, ..., _isCavalryFormation, ...) (:440)
// OrderController.cs:99, :1338 new Formation(null, -1) (team-less preview copies); SimulateDestinationFrames :1443 -> GetOrderPositionOfUnit :1452
_cachedAndFormationValuesUpdateTimer = new Timer(Mission.CurrentTime, 0.45f + MBRandom.RandomFloat * 0.1f);  // Agent.cs:1592
// Agent.ForceUpdateCachedAndFormationValues(bool, bool) :3708; Agent.TickParallel(float) :4684 -> :4726
// Agent (sealed, Agent.cs:14): Main :622, IsMount :642, Position :680, RiderAgent :718, HasMount :720,
// Monster :782, Index :853, Team :875, AgentVisuals :976, MountAgent :1001, Character :1392,
// GetCurrentAction(int) :2748, GetCurrentActionProgress(int) :2773, IsActive() :3294
public override int GetHashCode() { return _creationIndex; }                 // Agent.cs:4992-4995 (Equals not overridden)
public Skeleton GetSkeleton() { return MBAPI.IMBAgentVisuals.GetSkeleton(GetPtr()); }  // MBAgentVisuals.cs:145-148
public sealed class Skeleton : NativeObject  // Skeleton.cs:9; sbyte GetBoneCount() :75; MatrixFrame GetBoneEntitialFrameWithIndex(sbyte) :95
// NativeObject.Construct: IncreaseReferenceCount, lock (_nativeObjectKeepReferences), GCHandle.Alloc  // NativeObject.cs:32-43
static NativeObject() { ... LibraryApplicationInterface.IManaged.GetClassTypeDefinitionCount() ... }  // :62 (native)
public static bool operator ==(NativeObject a, NativeObject b)              // :221 (so tests use `is null`)
public readonly struct ActionIndexCache : IEquatable<ActionIndexCache>       // ActionIndexCache.cs:5, no explicit static ctor
public static bool operator !=(ActionIndexCache action0, ActionIndexCache action1) { return action0.Index != action1.Index; }  // :482-485
```

`ActionIndexCache` has 215 engine-backed static fields but no explicit static constructor, so it is
`beforefieldinit`; `default(ActionIndexCache)` and the `Index`-only `!=` never touch those fields,
which is what the plan 015 tick tests rely on (they confirm it when they pass). `HumanAIComponent`
`ParallelUpdateFormationMovement` (`:666`) calls `GetCurrentGlobalPositionOfUnit` (`:685`), so
vanilla also reads `Interval` on the workers. Installed .NET Framework `mscorlib`
`List<T>.InsertRange` copies another collection through `new T[count]` (ilspycmd, this session).

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types (the warg nodes and Patch30 are
  entry points; `FormationLayoutService` sees only `IFormationAdapter`). ADR-002 thin entry points
  (under 150 lines); ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside
  IoC registration.
- TDD: new work (Steps A3 and A4) writes the failing test first; ported commits bring theirs.
- MCM json2 rule (rename a persisted setting, never flip its default): no setting changes here.
- `.claude/rules/harmony-patches.md`: `MovementOrder` postfixes use the shared deferred category
  (not touched here); a store a worker-thread patch reaches takes a lock, or (after B1) is a
  concurrent collection written under the lock and read without it. `csharp-architecture.md`: a
  moved engine-float gate gets its NaN test in the same commit (A1: `BoneCheckRangeGateTests`).

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/124 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed:     0`; give the call a 600000 ms timeout |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<A>\|FullyQualifiedName~<B>"` (the backslash only escapes the table; type a plain pipe, as the steps' filters show) | as each step states |
| Simulate | `git merge-tree --write-tree --name-only --no-messages --merge-base=<base> HEAD <sha>; echo "exit=$?"` | as each step states (writes git objects only) |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links`, `orphan_features`, `missing_features` equal Step A0.6's values; `ai_dashes: 0` |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate
CopyModule on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game. Never
`./build.ps1`. Never two builds at once in this worktree. Always build before a `--no-build` test.

## Scope

**In scope** (the only files you create, modify or delete):

- Part A: the 49 paths listed in Part A's done criteria.
- Part B: the 10 paths staged in Step B6.
- Single-owner files: none. `Main/IoC.cs`, `Main/SubModule.cs` and every csproj stay untouched
  (no registration changes; SDK globbing picks up new files; `InternalsVisibleTo("TAOM.Tests")`
  exists at `Main/TAOM.csproj:117-122`).

**Out of scope** (do not touch):

- `CHANGELOG.md` (D1), `CLAUDE.md`, `AGENTS.md`, `docs/reference/feature-map.md`, `docs/INDEX.md`,
  `tools/README.md`, all of `docs/reviews/` (lessons, logs, 1.5.x records: WP142 syncs them),
  `docs/adrs/**`, `.claude/settings*.json`, `Directory.Build.props`, `plans/README.md`.
- `Main/Features/CreatureBandits/**`, `Main/Features/RaceAbilities/**` (1.5.x-only; stay absent);
  `LayoutPositioner.UnitPitch`, `IFormationAdapter.UnitDiameter` (`25995dc91`, OUT: it widened
  mounted mixed formations); `WargMissionBehavior.cs` (plan 015 nets to no change),
  `AdvancedCombatBehavior.cs` (2 s cadence stays), `TaomHowdahMachine.cs`, `Dependencies/**`.

## Git workflow

- Create the worktree in Step A0.1; work and commit only there, on
  `port148/124-mission-hot-paths`. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`.
  Run git only through the Bash tool (D19), never through PowerShell.
- Per-port procedure (binding): a single 1.5.x commit goes in with `git cherry-pick -n <sha>`;
  never let `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit
  (`GIT_EDITOR=true` here commits the 1.5.x message unedited). Plan 015 comes from its squashed end
  state (A1) and the two shared hunks by path-limited `git apply` (A3). Drop bookkeeping: a
  conflicted path with `git checkout --ours <path>` then `git add <path>`, a cleanly changed one
  with `git checkout HEAD <path>`, a newly added one with `git rm -f -q <path>`. Never write
  a `git checkout` with a bare double dash, and never `git restore` (the dangerous-git hook asks on those forms).
- Stage with explicit `git add <paths>` (never `-A`, `-u` or `.`), then run `git commit -F <file>`
  in the NEXT sequential Bash call: never the same command, never a parallel sibling call. Write
  each message with the Write tool to the file named in its step and pass it as `C:/...`.
- Never `--no-verify`, `--amend`, `-C` or `-c`. A hook that denies a commit: STOP, report verbatim.
- Message template: subject `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters; a
  body for 1.4.x readers wrapped at 72 with no long dashes; trailers `Research:`, `Not-tested:`,
  `Save-compat:` (and `Constraint:`, `Rejected:` when relevant), then one `Ported-from: <40-hex>`
  per included 1.5.x commit. No `Co-Authored-By`, no "Generated with" line. Exact messages below.

## Part A: creature battle hot paths (plans 015 and 033, shared tree hunks, the walk cap)

### Step A0: Worktree, prerequisites, baseline

**A0.1**

```bash
cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/124 -b port148/124-mission-hot-paths bannerlord-1.4.5
```

**Verify**: `Preparing worktree (new branch 'port148/124-mission-hot-paths')`. If the branch or
folder already exists: STOP.

**A0.2** Run the drift check in the header. Read the list; A0.3 and A0.4 decide.

**A0.3** Prerequisites and not-yet-run markers:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && grep -c 'public void ApplyPendingRemovals()' Main/Features/AdvancedCombat/SpatialGrid.cs; \
grep -c 'RunOrDefer' Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs; \
test -f Main/Features/BattleLoadDiagnostics/MissionTickStallWatchdog.cs && echo wp106-ok; \
test ! -d Main/Features/AutoResolveDiagnostics && echo wp123-ok; \
ls Main/BehaviorTreeWrapper/CallbackSkipLedger.cs Main/Core/Collections/ReferenceIdentity.cs TAOM.Tests/BehaviorTrees/BehaviorTreeRunawayCapTests.cs 2>&1 | grep -c 'No such file'; \
grep -c 'bool IsWarg();' Main/Adapters/IAgentAdapter.cs; \
ls -d Main/Features/CreatureBandits Main/Features/RaceAbilities 2>&1 | grep -c 'No such file'
```

**Verify**, in order: `1`; a number of at least `1`; `wp106-ok`; `wp123-ok`; `3`; `1`; `2`.
Anything else: STOP (a prerequisite WP is not merged, or this WP already ran). WP123's marker is
its binding AutoResolveDiagnostics deletion.

**A0.4** Blob preconditions (the decisive drift check). Every file below must be byte-identical to
the 1.5.x parent of the commit that edits it (or, for files no WP has touched, to `1b00d1acd`):

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && chk(){ ref=$1; shift; for p in "$@"; do [ "$(git rev-parse "HEAD:$p" 2>/dev/null)" = "$(git rev-parse "$ref:$p")" ] || echo "DIFFERS vs $ref: $p"; done; }; \
chk 7f02fc8d3 Main/Adapters/AgentAdapter.cs Main/Adapters/IAgentAdapter.cs Main/Features/AdvancedCombat/BoneCheck.cs Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs Main/Features/AdvancedCombat/SpatialGrid.cs Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs Main/Features/Warg/WargBehaviorTree.cs Main/Features/Warg/WargConfig.cs Main/Features/Warg/WargRiderHandManager.cs TAOM.Tests/Features/Warg/WargMonsterIdTests.cs; \
chk efe326242^ Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs Main/BehaviorTreeWrapper/Decorators/WaitNSecondsTickDecorator.cs Main/BehaviorTreeWrapper/Tasks/SleepTask.cs Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackOffCooldownDecorator.cs Main/Features/Spider/BehaviorTreeElements/SpiderAttackOffCooldownDecorator.cs Main/Features/Warg/BehaviorTreeElements/SetRageAttackTimer.cs Main/Features/Warg/BehaviorTreeElements/WargCanNotFindEnemyDecorator.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridDebugServiceTests.cs; \
chk a498e357e^ Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs; \
chk 70320bbe1^ TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs; \
chk 13f34c1cb^ Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs Main/Features/MixedFormations/IFormationLayoutService.cs; \
chk 1b00d1acd Main/Features/MixedFormations/FormationLayoutService.cs Main/Adapters/FormationAdapter.cs Main/Adapters/IFormationAdapter.cs Main/BehaviorTrees/BehaviorTreesCore.cs; \
echo blob-check-done
```

**Verify**: exactly one line, `blob-check-done` (this command printed only that against a
synthetic "1.4.x plus `f75288ebf`" commit during planning). Any `DIFFERS` line: STOP and report
it (an earlier WP changed a file this plan assumes untouched).

**A0.5** Engine spot check (the plan's line numbers):

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && F=$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Formation | tail -1) && grep -n "public WorldPosition GetOrderPositionOfUnit(Agent unit)\|return (int)(Team.TeamIndex \* 10 + FormationIndex);" "$F"; \
Q=$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.FormationQuerySystem | tail -1) && grep -n "public bool IsCavalryFormation => \|SetupSyncGroup(_infantryUnitRatio" "$Q"; \
V=$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.MBAgentVisuals | tail -1) && grep -n "public Skeleton GetSkeleton()" "$V"; \
A=$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.ActionIndexCache | tail -1) && grep -n "public static bool operator !=\|static ActionIndexCache()" "$A"
```

**Verify**: lines `1239` and `2425`; `195` and `440`; `145`; `482` only (no static constructor
line). Different lines or members: STOP (engine changed under the plan).

**A0.6** Baseline: Restore, Build, Test. **Verify**: build exit 0; `Failed: 0`. Record `Total`,
`Passed`, `Skipped` as **T0** (WP101's baseline was 9154/9151/3; later WPs added tests, so T0
should equal the latest count the integrator recorded in `plans/README.md`). Any failure: STOP.
Then run the Docs command and record `dead_links`, `orphan_features`, `missing_features`.

### Step A1: Transplant plan 015's end state (warg tick costs)

**Method**: end-state transplant of the linear series `7f02fc8d3..516380d2e`, squashed, limited
to code, tests, the one rule file and the two feature docs (bookkeeping never enters).

**A1.1** Simulation on your HEAD:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git merge-tree --write-tree --name-only --no-messages --merge-base=7f02fc8d3 HEAD 516380d2e; echo "exit=$?"
```

**Verify**: `exit=1`; the conflicted paths (after the tree id) are `CHANGELOG.md`,
`docs/features/advanced-combat.md` and paths under `docs/reviews/` (planning, on 1.4.x plus
`f75288ebf`), possibly also `.claude/rules/csharp-architecture.md` (WP110 and WP123 edit it). Any
`Main/` or `TAOM.Tests/` path: STOP and report the list.

**A1.2** Apply:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git diff --full-index --binary 7f02fc8d3 516380d2e -- Main TAOM.Tests .claude/rules/csharp-architecture.md docs/features/advanced-combat.md docs/features/warg-combat.md | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U
```

**Verify**: `exit=1`; the unmerged list is `docs/features/advanced-combat.md`, possibly also
`.claude/rules/csharp-architecture.md`. Every other path "Applied ... cleanly". An unmerged
`Main/` or `TAOM.Tests/` path: STOP.

**A1.3** Resolve and rewrite:

1. `docs/features/advanced-combat.md` has one conflict block under `### Solution Approach`.
   Replace the whole block (markers included) with these two lines, the incoming `BoneCheck`
   bullet and HEAD's `CustomAttacksUtils` bullet (the incoming one carries a 1.5.x measurement):
   ```text
   - `BoneCheck` and `BoneCheckDuringAnimation` hold references to attacker and target lists (via `IAgentAdapter`), fetch skeleton transforms each tick (`BoneCheckDuringAnimation` only inside the action's hit window), and compare world-space bone positions against a configurable radius.
   - `CustomAttacksUtils` caches the `Mission.RegisterBlow` delegate at static construction. It also exposes `TakeDamage` which builds a full `Blow`/`AttackCollisionData`/`CombatLogData` struct and feeds it to the cached delegate.
   ```
2. If `csharp-architecture.md` is unmerged: take HEAD's side of every block, then insert this
   paragraph (with a blank line before and after) directly after the code fence that ends the
   example containing `if (float.IsNaN(speedFactor) || float.IsNaN(context.VictimKnockDownResistance)) return null;`
   and before the paragraph starting `**Third category`:
   ```text
   **A moved, extracted or reordered gate is a new gate:** it gets its NaN test in the same commit, whichever polarity it keeps; when parity keeps an inverted form, the test pins that NaN behaviour and the comment says it is deliberate ([testing-qa](../../docs/reviews/lessons/testing-qa.md), plan 015 twice).
   ```
3. The review records stay on 1.5.x. With the Edit tool and `replace_all`, in
   `docs/features/advanced-combat.md` and in `docs/features/warg-combat.md`, replace the first
   string below (it starts with a backtick) with the second (1 occurrence in `advanced-combat.md`,
   2 in `warg-combat.md`; the existing `rca-warg-clip-on-horse-2026-09-13.md` link in
   `warg-combat.md` is a different string and must stay):
   ```text
   old: `../reviews/deep-review-0
   new: 1.5.x `docs/reviews/deep-review-0
   ```

`git add docs/features/advanced-combat.md docs/features/warg-combat.md .claude/rules/csharp-architecture.md`.

**Verify**: `git diff --name-only --diff-filter=U` prints nothing;
`grep -c '^<<<<<<<\|^>>>>>>>' docs/features/advanced-combat.md` prints `0`;
`grep -c '\.\./reviews/deep-review-0' docs/features/advanced-combat.md docs/features/warg-combat.md`
prints `0` for each; `grep -c 'A moved, extracted or reordered gate is a new gate' .claude/rules/csharp-architecture.md`
prints `1`; and the code is exactly plan 015's end state:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git diff --cached --stat 516380d2e -- Main/Adapters/AgentAdapter.cs Main/Adapters/IAgentAdapter.cs Main/Features/AdvancedCombat/BoneCheck.cs Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs Main/Features/AdvancedCombat/SpatialGrid.cs Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs Main/Features/Warg/WargBehaviorTree.cs Main/Features/Warg/WargConfig.cs Main/Features/Warg/WargRiderHandManager.cs TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs TAOM.Tests/Features/Warg/WargMonsterIdTests.cs TAOM.Tests/Features/Warg/WargTickCostTests.cs TAOM.Tests/Features/Warg/WargTreeNodeInjectionTests.cs; echo end-state-checked
```

prints only `end-state-checked`.

**A1.4** Build. Filtered with
`"FullyQualifiedName~WargTickCostTests|FullyQualifiedName~WargTreeNodeInjectionTests|FullyQualifiedName~BoneCheckDuringAnimationTickTests|FullyQualifiedName~BoneCheckRangeGateTests|FullyQualifiedName~SpatialGridQueryTests|FullyQualifiedName~WargMonsterIdTests|FullyQualifiedName~SpatialGridRemovalTests|FullyQualifiedName~WargOffThreadTests|FullyQualifiedName~WargAttackServiceTests|FullyQualifiedName~BoneCollisionServiceTests"`.
**Verify**: `Failed: 0`. Then the full Test: `Failed: 0`, `Total` = T0 + 47, `Skipped` = T0's.

**A1.5** Commit.

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git add .claude/rules/csharp-architecture.md Main/Adapters/AgentAdapter.cs Main/Adapters/IAgentAdapter.cs Main/Features/AdvancedCombat/BoneCheck.cs Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs Main/Features/AdvancedCombat/SpatialGrid.cs Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs Main/Features/Warg/WargBehaviorTree.cs Main/Features/Warg/WargConfig.cs Main/Features/Warg/WargRiderHandManager.cs TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs TAOM.Tests/Features/Warg/WargMonsterIdTests.cs TAOM.Tests/Features/Warg/WargTickCostTests.cs TAOM.Tests/Features/Warg/WargTreeNodeInjectionTests.cs docs/features/advanced-combat.md docs/features/warg-combat.md && git status --porcelain
```

**Verify**: exactly 21 lines, all staged (`M ` or `A `), none with a second-column mark. Write
`C:\Users\mikew\source\repos\taom-port\124-msg-A1.txt` with exactly:

```text
perf(warg): v2.0.28 - warg trees resolve once, scan buffers, x,y grid

Warg battles paid per-tick costs for nothing. The tree nodes resolved
their services from the container on every evaluation, each enemy
scan allocated a new List<Agent>, and a live bite fetched a native
Skeleton wrapper (ref count, global lock, GCHandle, finalizer) for
every captured target before any range check.

WargBehaviorTree.BuildTree now resolves both services once per tree
and injects them into the four nodes that use them; the rider-hand
check reads the mount's Monster, so IAgentAdapter.IsWarg is gone.
Scans fill reused buffers. SpatialGrid keys cells on x and y only (a
60 m scan looks up 49 cells, not 343; the distance test stays 3D).
BoneCheck range-gates each target before its skeleton (a positive
test, so a NaN frame fails it) and fetches the attacker's skeleton
once per tick. BoneCheckDuringAnimation reads the action progress
once and fetches the skeleton only inside the hit window, so a
skeleton missing during the wind-up ends the bite only if it is
still missing when the window opens.

Research: taom-src v1.4.8 MBAgentVisuals.cs 145-148, NativeObject.cs
32-43 and 62, Skeleton.cs 9, 75 and 95, ActionIndexCache.cs 5 and
482-485, Agent.cs 782, 1001, 2748 and 2773
Not-tested: a live bite in game (warg Custom Battle owed, #659)
Save-compat: no saved state touched
Ported-from: 87f9f862e10323d22efffd693686b4d75ac8b3be
Ported-from: b8b1446353491f2ba06c2edd35d03c492165b7c0
Ported-from: e5f644da4ade79caf67263a7f5b314f6856b2cd6
Ported-from: 7577894d4623f5a9aecb432e7ca4184864d64f4c
Ported-from: 2420761ba6b9231ce5b26a9309445fc3d19d98df
Ported-from: fe8f30c7abfca4ba1cfb590df8013b82757709e4
Ported-from: 56eb4bc81414615ad9fde5cc8e773a4d7521cd68
Ported-from: 23f6f85bd1433cb0306533093b3b935f46c53c65
Ported-from: 5d9d4cc974d21c1980a9f20a8a06a9a3de5b2afd
Ported-from: 516380d2efcc890f948f5cef3d8b2987e280e107
```

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git commit -F C:/Users/mikew/source/repos/taom-port/124-msg-A1.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the subject prints; `git status --porcelain` prints nothing.

### Step A2: Port `efe326242` (fewer allocations in creature battles)

**A2.1** Precondition and simulation:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && for p in Main/Features/AdvancedCombat/SpatialGrid.cs Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs; do [ "$(git rev-parse HEAD:$p)" = "$(git rev-parse efe326242^:$p)" ] || echo "DIFFERS $p"; done; git merge-tree --write-tree --name-only --no-messages --merge-base=efe326242^ HEAD efe326242; echo "exit=$?"
```

**Verify**: no `DIFFERS` line; `exit=1`; conflicted paths only under `docs/reviews/` and possibly
`docs/features/advanced-combat.md`. Any `Main/` or `TAOM.Tests/` path: STOP.

**A2.2** Apply and drop the bookkeeping:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git cherry-pick -n efe326242; echo "exit=$?"; U=$(git diff --name-only --diff-filter=U); \
for p in docs/reviews/REVIEW-LOG.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/state-lifecycle-save.md docs/reviews/lessons/testing-qa.md; do \
  if printf '%s\n' "$U" | grep -qxF "$p"; then git checkout --ours "$p" && git add "$p"; else git checkout HEAD "$p"; fi; done; \
git rm -f -q docs/reviews/deep-review-033-creature-battle-allocations-2026-10-02.md docs/reviews/rca-creature-battle-allocations-2026-10-02.md; \
git checkout 30abf550b Main/Core/Collections/ReferenceIdentity.cs TAOM.Tests/Core/Collections/ReferenceIdentityTests.cs; \
git diff --cached --name-only HEAD -- docs/reviews; echo ---; git diff --name-only --diff-filter=U
```

**Verify**: `exit=1` if A2.1 listed conflicts (expected), `exit=0` otherwise; nothing printed
before `---`; after `---` at most `docs/features/advanced-combat.md`. (`ReferenceIdentity.cs` and
its test are unchanged on 1.5.x from `30abf550b` to the tip; `SpatialGridReuseTests.cs` needs them.)

**A2.3** Docs. If `docs/features/advanced-combat.md` is unmerged, its block is the `SpatialGrid`
and `CustomAttacksUtils` bullets: keep the incoming `SpatialGrid` bullet (it starts
`` - `SpatialGrid` divides the map into 20-unit cells. `AdvancedCombatBehavior.OnMissionTick` asks
the grid to rebuild ``) and HEAD's `CustomAttacksUtils` bullet (the one without "bypasses armor").
Then, with the Edit tool, make this one replacement (in the new 2026-10-04 changelog entry; the
old string starts with a backtick):

```text
old: `../reviews/deep-review-033
new: 1.5.x `docs/reviews/deep-review-033
```

`git add docs/features/advanced-combat.md`.

**Verify**: `git diff --name-only --diff-filter=U` prints nothing;
`grep -c 'bypasses armor\|\.\./reviews/deep-review-0' docs/features/advanced-combat.md` prints `0`.

**A2.4** Adapt two test details:

1. In `TAOM.Tests/BehaviorTrees/CreatureTreeClockTests.cs` replace the whole
   `CreatureTreePrefixes` initializer with the namespaces that exist on 1.4.x (it lists six that
   do not: Animalia, two CreatureBandits, Elk, two TrollBruteForce, plus their comment line):
   ```csharp
       private static readonly string[] CreatureTreePrefixes =
       {
           "BehaviorTreeWrapper.",
           "BehaviorTrees.",
           "TAOM.Features.AdvancedCombat.BaseBehaviorTree.",
           "TAOM.Features.Elephant.",
           "TAOM.Features.ElephantLike.",
           "TAOM.Features.Mumakil.",
           "TAOM.Features.Spider.",
           "TAOM.Features.Warg.",
           "TAOM.Features.WarRam.",
       };
   ```
2. Run `git grep -l 'TestCategory("RequiresGame")' HEAD -- TAOM.Tests | wc -l`. If it prints `0`
   (no plan 010 category on this line, D9), delete the one `[TestCategory("RequiresGame")]` line
   (column 0, above the class) in `BehaviorTreeMissionLogicDispatchTests.cs`,
   `SpatialGridDormancyTests.cs`, `SpatialGridReuseTests.cs` and `SpatialGridWiringTests.cs`;
   otherwise keep them.

`git add` both edited areas. **Verify**:
`grep -c 'Animalia\|CreatureBandit\|Features\.Elk\|TrollBruteForce' TAOM.Tests/BehaviorTrees/CreatureTreeClockTests.cs`
prints `0`; and the two V05 invariants and the clock change hold:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && grep -c '_pendingRemovals.RunOrDefer("SpatialGrid.Remove"' Main/Features/AdvancedCombat/SpatialGrid.cs; grep -c '_skippedRebuilds > 0 && !offThread' Main/Features/AdvancedCombat/SpatialGrid.cs; grep -c 'if (!Listens(Callback\.' Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs; grep -rn 'DateTime\.Now\b' Main/BehaviorTreeWrapper Main/BehaviorTrees Main/Features/Warg Main/Features/Spider Main/Features/ElephantLike Main/Features/Elephant Main/Features/Mumakil Main/Features/WarRam; echo clock-checked
```

prints `1`, `1`, `14`, then only `clock-checked`.

**A2.5** Build. Filtered with
`"FullyQualifiedName~BehaviorTreeMissionLogicDispatchTests|FullyQualifiedName~CallbackSkipLedgerTests|FullyQualifiedName~CreatureTreeClockTests|FullyQualifiedName~SelectorListReuseTests|FullyQualifiedName~ReferenceIdentityTests|FullyQualifiedName~SpatialGrid|FullyQualifiedName~BehaviorTreeAgentComponentThreadingTests|FullyQualifiedName~DeferredCallbackQueueTests|FullyQualifiedName~BehaviorTreeBuilderTests|FullyQualifiedName~BehaviorTreeMissionLogicInheritanceTests|FullyQualifiedName~WargTickCostTests"`.
**Verify**: `Failed: 0` (`CreatureTreeClockTests.CreatureTreeCode_NeverReadsDateTimeNow` must
pass, not be Inconclusive: `BANNERLORD_GAME_DIR` is set on the laptop). Full Test: `Failed: 0`,
`Total` = T0 + 98.

**A2.6** Commit.

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git add Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs Main/BehaviorTreeWrapper/CallbackSkipLedger.cs Main/BehaviorTreeWrapper/Decorators/WaitNSecondsTickDecorator.cs Main/BehaviorTreeWrapper/Tasks/SleepTask.cs Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs Main/Core/Collections/ReferenceIdentity.cs TAOM.Tests/Core/Collections/ReferenceIdentityTests.cs Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs Main/Features/AdvancedCombat/SpatialGrid.cs Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackOffCooldownDecorator.cs Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs Main/Features/Spider/BehaviorTreeElements/SpiderAttackOffCooldownDecorator.cs Main/Features/Spider/BehaviorTreeElements/SpiderAttackTaskBase.cs Main/Features/Warg/BehaviorTreeElements/SetRageAttackTimer.cs Main/Features/Warg/BehaviorTreeElements/WargCanNotFindEnemyDecorator.cs TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicDispatchTests.cs TAOM.Tests/BehaviorTreeWrapper/CallbackSkipLedgerTests.cs TAOM.Tests/BehaviorTrees/CreatureTreeClockTests.cs TAOM.Tests/BehaviorTrees/SelectorListReuseTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridDebugServiceTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridDormancyTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridReuseTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridWiringTests.cs docs/features/advanced-combat.md docs/features/bannerlord-together-compat.md docs/features/elephant.md docs/features/warg-combat.md && git status --porcelain
```

**Verify**: exactly 27 lines, all staged, no `docs/reviews/` path. Write
`C:\Users\mikew\source\repos\taom-port\124-msg-A2.txt` with exactly:

```text
perf(creatures): v2.0.28 - fewer allocations in creature battles

Creature trees and the spatial grid did work in every battle, with
or without creatures. The trees (wargs, spiders, elephants, mumakil,
war rams) now time sleeps, waits, cooldowns and rage with
DateTime.UtcNow: no time-zone conversion per node per frame, no hour
jump at a daylight-saving change, still wall-clock time. The selector
reuses its child lists. BehaviorTreeMissionLogic returns before
building arguments or parking an off-thread replay when no tree
listens; taom_debug.log gets one INFO line at the first such skip
and a mission-end summary.

SpatialGrid reuses two maps and their lists, removes a deleted agent
from its one cell, and skips a scheduled rebuild nothing queried; the
next query on the mission thread rebuilds first, an off-thread query
never builds, and removals stay parked for the mission tick (#634).
One INFO line each per mission records the first skip and the first
rebuild a query made. The grid's reuse test compares lists by
reference through ReferenceIdentity, a small comparer added here.

Research: taom-src v1.4.8 Agent.cs 4992-4995 (GetHashCode returns the
managed creation index, so the grid's Agent-keyed maps are safe)
Not-tested: warg, spider and elephant battles in game; the
[SpatialGrid] and [BehaviorTree] lines in a battle without creatures
Save-compat: no saved state touched
Ported-from: efe326242585a920e33238b3da20e88ee50f0a32
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
```

Then commit in the NEXT Bash call (as A1.5, with `124-msg-A2.txt`). **Verify**: clean status.

### Step A3: Shared tree hunks from `a498e357e` and `70320bbe1` (TDD)

**A3.1 (RED)** Precondition, then the two IL tests:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && for p in Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs:a498e357e^ Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs:70320bbe1^ TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs:70320bbe1^; do f=${p%%:*}; r=${p#*:}; [ "$(git rev-parse HEAD:$f)" = "$(git rev-parse $r:$f)" ] || echo "DIFFERS $f"; done; \
git diff --full-index 70320bbe1^ 70320bbe1 -- TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs | git apply --3way; echo "exit=$?"
```

**Verify**: no `DIFFERS`; `exit=0`. Then with the Edit tool rewrite the two new comments (their
1.5.x text names Race Abilities, absent here):

```text
    // new tenant. Since the race abilities it is checked only when a tree is due, so pin that it still comes
->  // new tenant. It is checked only when a tree is due, so pin that it still comes

    // The schedule is copied every frame, and the race abilities put every profiled soldier on it: List.AddRange
    // of a collection allocates a temporary array of the whole list each time.
->  // The schedule is copied every frame: on .NET Framework List.AddRange of a collection allocates a
    // temporary array of the whole list each time.
```

Build; Filtered `FullyQualifiedName~BehaviorTreeAgentComponentThreadingTests`. **Verify**:
`Failed: 1`, the failure is `MissionTick_CopiesTheScheduleWithoutAllocating` (it finds
`AddRange`); `TickOnMissionThread_ChecksTheSlotBeforeRunningTheTree` passes already (a pin).

**A3.2 (GREEN)** Apply the two code hunks:

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git diff --full-index a498e357e^ a498e357e -- Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs | git apply --3way; echo "exit=$?"; git diff --full-index 70320bbe1^ 70320bbe1 -- Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs | git apply --3way; echo "exit=$?"
```

**Verify**: `exit=0` twice. Rewrite the two code comments with the Edit tool:

```text
In BehaviorTreeAgentComponent.cs:
        // Checked only when the tree is due: both are native reads, and a race-ability tree runs once a
        // second on every soldier of its race, where a creature tree runs every tick on a few.
->      // Checked only when the tree is due: both are native reads.

In BehaviorTreeMissionLogic.cs:
        // List.AddRange of a collection allocates a temporary array of the whole schedule, every frame, and the
        // race abilities put every profiled soldier on it.
->      // on .NET Framework List.AddRange of a collection allocates a temporary array of the whole schedule,
        // every frame.
```

`git add` the three files. **Verify**:
`grep -ci 'race abilit\|race-abilit' Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs`
prints `0` three times; `grep -c '_tickScratch.AddRange' Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs`
prints `0`. Build; the same filter: `Failed: 0`. Full Test: `Total` = T0 + 100, `Failed: 0`.

**A3.3** Commit: `git add Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs && git status --porcelain`
(3 staged lines), message file `124-msg-A3.txt`:

```text
perf(creatures): v2.0.28 - tree tick checks slots only when due

BehaviorTreeAgentComponent.TickOnMissionThread made two native reads
(Agent.IsActive and the slot identity check) on every tick of every
scheduled tree, due or not. It now adds the elapsed time first and
makes both reads only when the tree is due, still before RunTree.
BehaviorTreeMissionLogic.OnMissionTick copied the schedule with
List.AddRange, which on .NET Framework allocates a temporary array of
the whole schedule every frame; it now copies element by element.

Research: installed .NET Framework mscorlib List<T>.InsertRange
copies another collection through a new T[count] (ilspycmd)
Not-tested: nothing beyond the IL pins; behaviour is unchanged
Save-compat: no saved state touched
Ported-from: a498e357e43e9a5a834c1fd7af0d8a5788cfbf3c
Ported-from: 70320bbe1b16a1b5dc5fa4f151f2a95fcc3403ad
```

Commit in the NEXT Bash call. **Verify**: clean status.

### Step A4: Cap one `RunTree` walk (new work, FREEZE-THREADING-09, TDD)

The orchestrator creates the GitHub issue before this step lands; put its number in the commit
body's last prose line ("Refs #NNN") if you have it, otherwise leave the body as given.

**A4.1 (RED)** In `Main/BehaviorTrees/BehaviorTreesCore.cs`, inside `public abstract class
BehaviorTree`, directly after `public readonly int _rootEvaluationDelay = 2000;`, add the
declaration only (no behaviour yet):

```csharp

    // The most node transitions one RunTree call may make. A walk ends at the root or at a node that
    // waits; a longer one is a cycle that would hold the mission thread for ever (plan 124, FREEZE-THREADING-09).
    internal const int MaxNodeTransitionsPerRun = 1000;
```

Create `TAOM.Tests/BehaviorTrees/BehaviorTreeRunawayCapTests.cs` (pattern:
`BehaviorTreeBuilderTests.cs` in the same folder):

```csharp
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.BehaviorTrees;

/// <summary>
/// RunTree walks nodes until the walk returns to the root or a node waits, and nothing bounded that loop: two
/// nodes that hand control back and forth would hold the mission thread for ever (plan 124, FREEZE-THREADING-09).
/// These pin the cap, the exact-length boundary and the once-per-tree-type log line.
/// </summary>
[TestClass]
public class BehaviorTreeRunawayCapTests
{
    // Ends an uncapped walk so a missing cap fails these tests instead of hanging the run.
    private const int UncappedEscapeCall = 100_000;

    private sealed class CaptureLogger : ILogger
    {
        public List<string> Lines { get; } = new();
        public void LogMessage(string message) => Lines.Add(message);
    }

    private sealed class CallCounter { public int Calls; }

    // Hands control to Next on every call and never waits; on call number escapeCall it hands control to End.
    private sealed class HandOffNode : BTNode
    {
        private readonly CallCounter _counter;
        private readonly int _escapeCall;
        public HandOffNode(CallCounter counter, int escapeCall) { _counter = counter; _escapeCall = escapeCall; }
        public BTNode Next { get; set; } = null!;
        public BTNode End { get; set; } = null!;
        public override BTNode HandleExecute() => ++_counter.Calls >= _escapeCall ? End : Next;
    }

    // The root: a walk that reaches it is complete.
    private sealed class EndNode : BTNode { public override BTNode HandleExecute() => this; }

    private sealed class CyclingTree : BehaviorTree { }
    private sealed class BoundaryTree : BehaviorTree { }
    private sealed class ReportedTree : BehaviorTree { }

    private ILogger? _previousLogger;
    private CaptureLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousLogger = BTRegister.Logger;
        _logger = new CaptureLogger();
        BTRegister.AddLogger(_logger);
    }

    [TestCleanup]
    public void Cleanup() => BTRegister.AddLogger(_previousLogger!);

    private static CallCounter Wire(BehaviorTree tree, int escapeCall)
    {
        var counter = new CallCounter();
        var end = new EndNode();
        var first = new HandOffNode(counter, escapeCall) { End = end };
        var second = new HandOffNode(counter, escapeCall) { End = end, Next = first };
        first.Next = second;
        tree.RootNode = end;
        tree.CurrentNode = first;
        return counter;
    }

    [TestMethod]
    public void RunTree_NodesThatHandControlBackAndForth_StopAtTheCap()
    {
        var tree = new CyclingTree();
        CallCounter counter = Wire(tree, UncappedEscapeCall);
        tree.RunTree();
        Assert.IsFalse(tree.IsRunning, "a walk that never reaches the root or a waiting node must stop the tree");
        Assert.AreEqual(BehaviorTree.MaxNodeTransitionsPerRun + 1, counter.Calls,
            "one call for the unexecuted first node, then exactly the capped number of transitions");
        tree.RunTree();
        Assert.AreEqual(BehaviorTree.MaxNodeTransitionsPerRun + 1, counter.Calls, "a stopped tree never walks again");
    }

    [TestMethod]
    public void RunTree_AWalkOfExactlyTheCap_CompletesAndKeepsRunning()
    {
        var tree = new BoundaryTree();
        // The first call is the unexecuted first node's; the last allowed transition returns the root.
        CallCounter counter = Wire(tree, BehaviorTree.MaxNodeTransitionsPerRun + 1);
        tree.RunTree();
        Assert.IsTrue(tree.IsRunning, "a walk that reaches the root within the cap is a normal walk");
        Assert.AreEqual(BehaviorTree.MaxNodeTransitionsPerRun + 1, counter.Calls);
        Assert.AreEqual(0, _logger.Lines.Count, "a normal walk logs nothing");
    }

    [TestMethod]
    public void RunTree_RunawaysOfOneTreeType_AreLoggedOnce()
    {
        var first = new ReportedTree();
        var second = new ReportedTree();
        Wire(first, UncappedEscapeCall);
        Wire(second, UncappedEscapeCall);
        first.RunTree();
        second.RunTree();
        Assert.AreEqual(1, _logger.Lines.Count, "one line per tree type, however many of its trees run away");
        StringAssert.Contains(_logger.Lines[0], nameof(ReportedTree));
        Assert.IsFalse(second.IsRunning, "the second tree stops even though its runaway is not logged again");
    }
}
```

Build; Filtered `FullyQualifiedName~BehaviorTreeRunawayCapTests`. **Verify**: `Failed: 2`:
`RunTree_NodesThatHandControlBackAndForth_StopAtTheCap` (message "a walk that never reaches the
root or a waiting node must stop the tree") and `RunTree_RunawaysOfOneTreeType_AreLoggedOnce`
("one line per tree type"); `RunTree_AWalkOfExactlyTheCap_CompletesAndKeepsRunning` passes. A
compile error instead (for example `ILogger` ambiguous): fix the test file only, then confirm
this failure pattern.

**A4.2 (GREEN)** In `BehaviorTreesCore.cs`: add `using System.Collections.Concurrent;` to the
usings; after the constant add

```csharp

    // Tree types whose runaway has been logged: one line per type per process, however many of its trees stop.
    private static readonly ConcurrentDictionary<Type, byte> RunawayReported = new();
```

replace the `do ... while` loop inside `RunTree` with

```csharp
            int transitions = 0;
            do
            {
                if (++transitions > MaxNodeTransitionsPerRun)
                {
                    StopRunaway();
                    return;
                }
                CurrentNode = CurrentNode.HandleExecute();
                if (CurrentNode.ShouldExitTree())
                    return;
            } while (CurrentNode != RootNode);
```

and add this method directly after `RunTree`:

```csharp

    // Stops the tree the way the exception path does, then logs the first runaway of this tree type. The log
    // call is guarded: a log line must never throw out of the mission tick.
    private void StopRunaway()
    {
        IsRunning = false;
        if (!RunawayReported.TryAdd(GetType(), 0))
            return;
        try
        {
            BTRegister.Logger?.LogMessage("Behavior tree " + GetType().Name + " made " + MaxNodeTransitionsPerRun
                + " node transitions in one RunTree call without reaching its root or a waiting node; the tree is stopped.");
        }
        catch
        {
            // never throw from the tree tick
        }
    }
```

Leave the rest of `RunTree` (the pre-walk `HandleExecute`, the root reset, the `catch`) unchanged.
In the file header, after the line ending `No behavior change.`, add:

```csharp
//
// Changed since the port: Selector.Prepare reuses its child lists (plan 033, ported by plan 124) and
// RunTree caps the node transitions of one call (plan 124, FREEZE-THREADING-09).
```

Build; Filtered `"FullyQualifiedName~BehaviorTreeRunawayCapTests|FullyQualifiedName~BehaviorTreeBuilderTests"`.
**Verify**: `Failed: 0`. `grep -c 'MaxNodeTransitionsPerRun' Main/BehaviorTrees/BehaviorTreesCore.cs`
prints at least `3`.

**A4.3** Doc. In `docs/features/warg-combat.md`, under `## Performance`, directly after the
`**Tree framework (plan 033)**` bullet, add:

```text
- **Tree walk cap (plan 124)**: `BehaviorTree.RunTree` stops a tree after `MaxNodeTransitionsPerRun` (1,000) node transitions in one call, the way the exception path already stops one (`IsRunning` turns false and the creature falls back to the engine's AI), and logs the first runaway of each tree type through `BTRegister.Logger`, the exception path's sink. No reachable cycle is known (FREEZE-THREADING-09); the cap only bounds the loop. `BehaviorTreeRunawayCapTests` (3) pins the cap, the exact-length boundary and the once-per-type line.
```

and, as the newest entry under `## Changelog` (above the plan 033 entry), with today's date from
`date +%F`:

```text
- YYYY-MM-DD - plan 124 (FREEZE-THREADING-09): `BehaviorTree.RunTree` caps one call at 1,000
  node transitions and stops a tree that exceeds it, logging the first runaway of each tree type.
```

**A4.4** Full Test: `Failed: 0`, `Total` = T0 + 103. Docs command: values equal A0.6's,
`ai_dashes: 0`. Commit: `git add Main/BehaviorTrees/BehaviorTreesCore.cs TAOM.Tests/BehaviorTrees/BehaviorTreeRunawayCapTests.cs docs/features/warg-combat.md && git status --porcelain`
(3 staged lines), message file `124-msg-A4.txt`:

```text
fix(creatures): v2.0.28 - cap one behaviour tree walk at 1,000 nodes

BehaviorTree.RunTree walks nodes until the walk returns to the root
or reaches a node that waits, and nothing bounded that loop. No
reachable cycle is known, but a future node or listener that left an
unexpected state would hold the mission thread for ever: a frozen
battle with no exception, the hardest report to diagnose.

RunTree now stops a tree after 1,000 node transitions in one call,
the way the exception path already stops one: IsRunning turns false
and the creature falls back to the engine's AI. The first runaway of
each tree type is logged through the tree logger.

Research: Main/BehaviorTrees/BehaviorTreesCore.cs RunTree, identical
on both lines; Selector and Sequence in BehaviorTreesNodes.cs
Not-tested: a live runaway (none is known to occur)
Save-compat: no saved state touched
```

Commit in the NEXT Bash call. **Verify**: clean status.

### Part A done criteria

- [ ] Build exit 0; full Test `Failed: 0`, `Total` = T0 + 103, `Skipped` = T0's.
- [ ] `grep -rn 'IoC.Resolve' Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs Main/Features/Warg/WargRiderHandManager.cs`
      prints nothing; `grep -c 'IsWarg' Main/Adapters/IAgentAdapter.cs Main/Adapters/AgentAdapter.cs` prints `0` twice.
- [ ] Step A2.4's invariant command prints `1`, `1`, `14`, then only `clock-checked`.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | sort -u | wc -l` prints
      `49` (more only if a review follow-up commit touched extra in-scope files), and the list
      holds no `CHANGELOG.md`, `CLAUDE.md`, `docs/reviews/`, `feature-map.md`, `Main/IoC.cs` or
      `Main/SubModule.cs`. The 49: the 21 paths of A1.5, the 27 of A2.6 (minus `SpatialGrid.cs`,
      `advanced-combat.md` and `warg-combat.md`, already counted), `BehaviorTreeAgentComponent.cs`,
      `BehaviorTreeAgentComponentThreadingTests.cs`, `BehaviorTreesCore.cs`,
      `BehaviorTreeRunawayCapTests.cs`.
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'` prints nothing.
- [ ] Docs command: values equal A0.6's; `ai_dashes: 0`. `git status --porcelain` prints nothing.

## Part B: the lock-free Mixed Formations prefix (plan 032)

### Step B0: Preconditions (always run; Part A must be committed on this branch, so do not re-run A0.1)

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && for p in Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs:13f34c1cb^ Main/Features/MixedFormations/IFormationLayoutService.cs:13f34c1cb^ Main/Features/MixedFormations/FormationLayoutService.cs:1b00d1acd Main/Adapters/FormationAdapter.cs:1b00d1acd; do f=${p%%:*}; r=${p#*:}; [ "$(git rev-parse HEAD:$f)" = "$(git rev-parse $r:$f)" ] || echo "DIFFERS $f"; done; test -f Main/Core/Collections/ReferenceIdentity.cs && echo refid-from-a2; \
git merge-tree --write-tree --name-only --no-messages --merge-base=13f34c1cb^ HEAD 13f34c1cb; echo "exit=$?"
```

**Verify**: no `DIFFERS`; `refid-from-a2`; `exit=1`; the conflicted paths are among
`Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs`,
`Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs`,
`Main/Features/MixedFormations/FormationLayoutService.cs`, `docs/features/creature-bandits.md`,
`docs/features/mixed-formations.md`, `docs/reviews/REVIEW-LOG.md`, three
`docs/reviews/lessons/*.md`, and possibly the two `.claude/rules` files and
`docs/reference/harmony-patch-registry.md`. Any other `Main/` or `TAOM.Tests/` path: STOP. No
`refid-from-a2` line means Part A has not landed on this branch: STOP.

### Step B1: Apply, drop OUT and bookkeeping

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git cherry-pick -n 13f34c1cb; echo "exit=$?"; U=$(git diff --name-only --diff-filter=U); \
for p in docs/reviews/REVIEW-LOG.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/harmony-il.md docs/reviews/lessons/testing-qa.md; do \
  if printf '%s\n' "$U" | grep -qxF "$p"; then git checkout --ours "$p" && git add "$p"; else git checkout HEAD "$p"; fi; done; \
git rm -f -q Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs TAOM.Tests/Features/CreatureBandits/CreatureBanditWieldGuardTests.cs docs/features/creature-bandits.md docs/reviews/deep-review-032-worker-thread-formation-patch-2026-10-02.md docs/reviews/rca-worker-thread-formation-patch-2026-10-02.md; \
ls -d Main/Features/CreatureBandits TAOM.Tests/Features/CreatureBandits 2>&1 | grep -c 'No such file'; git diff --cached --name-only HEAD -- docs/reviews docs/features/creature-bandits.md; echo ---; git diff --name-only --diff-filter=U
```

**Verify**: `exit=1`; `2`; nothing printed before `---`; after `---` exactly
`Main/Features/MixedFormations/FormationLayoutService.cs` and `docs/features/mixed-formations.md`,
plus any of the two rule files or the registry that B0 listed.

### Step B2: Resolve `FormationLayoutService.cs` for the 1.4.x pitch

1. The one conflict block sits at the pitch line. Replace the whole block (markers included)
   with HEAD's pitch and the incoming comment:
   ```csharp
           // and add formation order position to get the final plane position. Runs outside the lock.
           var unitInterval = System.Math.Max(1f, formation.Interval + 1f);
   ```
2. Two cleanly merged comments name `UnitPitch`, which 1.4.x does not have; on v1.4.8 the same
   reasoning holds through `Formation.Interval` (`Formation.cs:512-534` reads `CavalryUnitRatio`).
   With the Edit tool:
   ```text
   CavalryUnitRatio that UnitPitch reads below refreshes  ->  CavalryUnitRatio that Formation.Interval reads for the pitch below refreshes
   // UnitPitch can re-evaluate the class-ratio queries    ->  // Formation.Interval can re-evaluate the class-ratio queries
   ```

`git add Main/Features/MixedFormations/FormationLayoutService.cs`. **Verify**:
`grep -c 'UnitPitch\|UnitDiameter' Main/Features/MixedFormations/FormationLayoutService.cs` prints
`0`; `grep -c 'System.Math.Max(1f, formation.Interval + 1f)' Main/Features/MixedFormations/FormationLayoutService.cs`
prints `1`; `grep -c 'ReferenceIdentity.Instance' Main/Features/MixedFormations/FormationLayoutService.cs`
prints `2`; `grep -c 'new FormationAdapter' Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs`
prints `0`.

### Step B3: Docs and rules

1. `docs/features/mixed-formations.md`, block under `## Performance + Thread Safety`: keep every
   HEAD line, except two bullets. HEAD's bullet starting `- Service state is per-mission` becomes
   the incoming "Two maps" bullet, exactly:
   ```text
   - Service state is per-mission, cleared on `OnEndMission`. Two maps: the layouts (`ConcurrentDictionary<object, (IFormationAdapter, FormationLayoutType)>`, ~4 entries) + `Dictionary<object, SlotAssignment>` (~4 entries), both keyed by reference (`ReferenceIdentity`). Negligible.
   ```
   and HEAD's bullet starting `- Per-position-query work:` becomes:
   ```text
   - Per-position-query work: a formation with no layout costs one lock-free lookup and nothing else; a laid-out formation takes the lock for the dictionary lookup and 1 conditional `BuildInitialAssignment` on a cache miss, then does the `Vec2` math (lock-free). The laid-out path still reads engine query state on the worker, as it did before this change: its cavalry gate reads the evaluating `IsCavalryFormation`, and the pitch (`Math.Max(1, formation.Interval + 1)`) reads `Formation.Interval`, which reads the evaluating `CavalryUnitRatio` (2.5 s lifetime; v1.4.8 `Formation.cs:512-534`, `FormationQuerySystem.cs:431`). A read of an expired member refreshes the formation's whole class-ratio sync group (`FormationQuerySystem.cs:440`) on whichever thread asks first, and while the group is fresh the gate is a clock check. The gate keeps the evaluating read on purpose: a mass transfer or split only expires the group (`QuerySystem.Expire()` in `Formation.OnMassUnitTransferEnd`, `TransferUnits` and `Split`), so a cached flag would answer from the old composition and the pitch's `Interval` read would then refresh it after the call had already returned a position (1.5.x Codex review of plan 032, finding 1). Vanilla reads `Interval` on these workers too: `HumanAIComponent.ParallelUpdateFormationMovement` calls `Formation.GetCurrentGlobalPositionOfUnit` (v1.4.8 `HumanAIComponent.cs:685`), which reads it (`Formation.cs:1284`). The `BuildInitialAssignment` is the only ω(N) operation and runs once per layout change per formation.
   ```
   Drop the incoming "Per-frame work", "Cycle hotkey log line" and "What counts as a key name"
   bullets: they belong to WP123's #713 port, and whatever HEAD has for them stays.
2. Same file, block under `## Changelog`: keep every HEAD line and add only this entry as the
   newest (drop the incoming #713 and #606 entries; WP123 and WP111 own them), with today's date:
   ```text
   - YYYY-MM-DD (port of 1.5.x plan 032, #714): almost no formation has a layout, so
     `FindLaidOutFormation` answers such a formation with no lock, no adapter allocation and no
     `FormationQuerySystem` read. The layouts live in one `ConcurrentDictionary` keyed by reference
     (`ReferenceIdentity`); every write and the laid-out slot lookup still hold `_lock`, so the #595
     rule below holds for every write. The cavalry gate keeps its evaluating read. A throw inside the
     prefix, silent before, logs its first occurrence per mission in full and a count at mission end.
   ```
3. Same file, the cleanly merged Tests line, one replacement:
   ```text
   old: `LayoutPositioner`'s own slots and pitch
   new: `LayoutPositioner`'s own slots and the service's pitch formula
   ```
4. `docs/reference/harmony-patch-registry.md`: if unmerged, keep HEAD's side and re-apply
   `13f34c1cb`'s two Patch30 paragraph changes by hand
   (`git show --format= 13f34c1cb -- docs/reference/harmony-patch-registry.md` shows them). Then,
   merged or not, one replacement (1.4.x has no `UnitDiameter`):
   ```text
   old: (`Formation.Interval`, `UnitDiameter`)
   new: (`Formation.Interval`)
   ```
5. Rules, only if unmerged (take HEAD's side first, then make the replacement by hand):
   ```text
   .claude/rules/csharp-architecture.md
   old: a lock. **No engine callback
   new: a lock, or is a concurrent collection written under one and read without it. **No engine callback

   .claude/rules/harmony-patches.md
   old: takes a lock (`FormationLayoutService`, `CavalryChargeService`, `TroopStanceManager` are the shape), it
   new: takes a lock (`CavalryChargeService`, `TroopStanceManager` are the shape), or writes a concurrent collection
        under that lock so a hot reader needs none (`FormationLayoutService.FindLaidOutFormation`), it
   ```

`git add` the edited files. **Verify**: `git diff --name-only --diff-filter=U` prints nothing;
`grep -c '^<<<<<<<\|^>>>>>>>' docs/features/mixed-formations.md` prints `0`;
`grep -c 'UnitPitch\|UnitDiameter' docs/features/mixed-formations.md docs/reference/harmony-patch-registry.md`
prints `0` twice; `grep -c 'FindLaidOutFormation' .claude/rules/harmony-patches.md docs/reference/harmony-patch-registry.md`
prints at least `1` each; `grep -c 'concurrent collection written under one and read without it' .claude/rules/csharp-architecture.md`
prints `1`.

### Step B4: Adapt `FormationLayoutServiceTests.cs` to 1.4.x

The incoming tests use `IFormationAdapter.UnitDiameter` and `LayoutPositioner.UnitPitch` (both
from `25995dc91`, OUT). With the Edit tool:

```text
In ComputeUnitPlanePosition_EveryLayout_PlacesEachUnitAtItsAssignedSlot:
  delete: f.UnitDiameter.Returns(0.76f);
  old:    var pitch = LayoutPositioner.UnitPitch(f);
  new:    var pitch = System.Math.Max(1f, f.Interval + 1f);
  old:    spaced by LayoutPositioner.UnitPitch and rotated
  new:    spaced by the service's pitch (Math.Max(1, Interval + 1)) and rotated
In ExpiringCavalryQueries (keep its Interval line: on v1.4.8 Formation.Interval reads CavalryUnitRatio):
  delete: formation.UnitDiameter.Returns(_ => { Refresh(); return 0.76f; });
  old:    that Formation.Interval and UnitDiameter read.
  new:    that Formation.Interval reads.
  old:    composition, and UnitPitch, a few lines later,
  new:    composition, and the pitch's Interval read, a few lines later,
```

`git add TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`. **Verify**:
`grep -c 'UnitPitch\|UnitDiameter' TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`
prints `0`. (`13f34c1cb` adds no `RequiresGame` line to either MixedFormations test file.)

### Step B5: Build and test

Build. Filtered `"FullyQualifiedName~MixedFormations|FullyQualifiedName~ReferenceIdentityTests|FullyQualifiedName~HarmonyPatchBindingTests"`.
**Verify**: `Failed: 0`. Full Test: `Failed: 0`, `Total` = the count when Part B started + 27.

### Step B6: Commit

```bash
cd /c/Users/mikew/source/repos/taom-port/124 && git add .claude/rules/csharp-architecture.md .claude/rules/harmony-patches.md Main/Adapters/FormationAdapter.cs Main/Features/MixedFormations/FormationLayoutService.cs Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs Main/Features/MixedFormations/IFormationLayoutService.cs TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs TAOM.Tests/Features/MixedFormations/Patch30ThreadSafetyIlTests.cs docs/features/mixed-formations.md docs/reference/harmony-patch-registry.md && git status --porcelain
```

**Verify**: exactly 10 lines, all staged; nothing under `Main/Features/CreatureBandits`,
`TAOM.Tests/Features/CreatureBandits` or `docs/reviews/`. Message file `124-msg-B1.txt`:

```text
perf(mixed-formations): v2.0.28 - lock-free path for plain formations

Patch30 on Formation.GetOrderPositionOfUnit runs about twice a second
for every AI unit on the engine's worker threads. Each field-battle
call allocated a FormationAdapter, read the MCM switch, took the
service's global lock and asked for the cavalry flag before learning
whether the formation had a layout, which almost none do.

FormationLayoutService now keeps its layouts in a ConcurrentDictionary
keyed by reference, with the adapter the mission behaviour passed;
every write still holds the lock. Patch30 asks FindLaidOutFormation
first: no layout means vanilla with no lock, allocation or formation
read; a laid-out formation reuses the stored adapter and keeps the
evaluating cavalry gate. A throw in the prefix, silent before, is
logged in full once per mission with a count at mission end. Keys use
ReferenceIdentity because v1.4.8 Formation.GetHashCode reads
Team.TeamIndex and the order preview builds team-less formations.

Research: taom-src v1.4.8 Formation.cs 1239, 2423-2425, 512-534,
1810-1813, 1943-1983; FormationQuerySystem.cs 195-197, 431-440;
OrderController.cs 99, 1338, 1443-1452; Agent.cs 1592, 3708, 4684-4726
Not-tested: the prefix on the worker threads (game-tested, ADR-008)
Save-compat: no saved state touched
Ported-from: 13f34c1cbc7c43f9d52f8c9df94af60e01065bc2
```

Commit in the NEXT Bash call. **Verify**: clean status.

### Part B done criteria

- [ ] Build exit 0; full Test `Failed: 0`, `Total` = the count when Part B started + 27.
- [ ] `grep -rn 'UnitPitch\|UnitDiameter' Main/Features/MixedFormations Main/Adapters/FormationAdapter.cs Main/Adapters/IFormationAdapter.cs TAOM.Tests/Features/MixedFormations docs/features/mixed-formations.md`
      prints nothing.
- [ ] `ls -d Main/Features/CreatureBandits TAOM.Tests/Features/CreatureBandits 2>&1 | grep -c 'No such file'` prints `2`.
- [ ] Subject check (as Part A) prints nothing; Docs command values equal A0.6's, `ai_dashes: 0`;
      `git status --porcelain` prints nothing.

## Disposition (every commit in the pack, plus the shared hunk this plan needed)

Simulation re-run this session onto the 1.4.x tip:
`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.
The second column gives the exit code and the non-bookkeeping conflicted paths.

| 1.5.x commit | Subject | Simulation onto `1b00d1acd` | Disposition |
|---|---|---|---|
| `87f9f862e` | perf(warg): v2.0.30 - resolve tree services once, scan into buffers | 1: `WargMissionBehavior.cs` | **Transplanted** (A1, squashed end state). Its `WargMissionBehavior.cs` hunk (the `Tick(factory)` plumbing) is reverted inside the series by `fe8f30c7a`, so that file is untouched |
| `b8b144635` | refactor(combat): v2.0.30 - pin SpatialGrid queries behind helpers | 1: `SpatialGrid.cs` (only `f75288ebf` context, ported by WP106) | **Transplanted** (A1) |
| `e5f644da4` | perf(combat): v2.0.30 - key SpatialGrid cells on x and y only | 1: `SpatialGrid.cs`, `SpatialGridQueryTests.cs` | **Transplanted** (A1) |
| `7577894d4` | perf(combat): v2.0.30 - range-gate bone targets before skeletons | 0 | **Transplanted** (A1) |
| `2420761ba` | docs(warg): v2.0.30 - record the warg tick-cost changes | 0 | **Transplanted** (A1: its doc lines are in the net diff) |
| `66a85b085` | docs(changelog): v2.0.30 - warg tick-cost entry for plan 015 | 1: `CHANGELOG.md` only | **Skipped** (D1: CHANGELOG bookkeeping) |
| `fe8f30c7a` | fix(warg): v2.0.30 - review follow-ups for plan 015 | 1: 9 code and test paths (series prerequisites plus `f75288ebf` context) | **Transplanted** (A1). Its review record, Codex prompt, RCA, REVIEW-LOG rows and lessons are not ported (1.5.x review artefacts; lessons go to WP142) |
| `56eb4bc81` | fix(warg): v2.0.30 - convergence fixes for plan 015 | 1: `WargRiderHandManager.cs`, `BoneCheckRangeGateTests.cs` | **Transplanted** (A1), including the `IsWarg()` deletion (its only 1.4.x caller is rewritten in the same transplant) |
| `23f6f85bd` | fix(warg): v2.0.30 - apply maintainer decisions for plan 015 | 1: 5 code and test paths | **Transplanted** (A1); its IL order test is superseded inside the series by `5d9d4cc97` |
| `5d9d4cc97` | fix(warg): v2.0.30 - review follow-ups for plan 015 | 1: 4 code and test paths | **Transplanted** (A1); records not ported |
| `516380d2e` | fix(warg): v2.0.30 - convergence fixes for plan 015 | 1: `WargTickCostTests.cs` | **Transplanted** (A1), with its two-line `csharp-architecture.md` rule; RCA and records not ported |
| `13f34c1cb` | perf(mixed-formations): v2.0.32 - lock-free path for plain formations | 1: `CreatureBanditAgents.cs`, `Patch93_CreatureBandits.cs`, `FormationLayoutService.cs` (+ rules, docs) | **Ported, adapted** (B1): 1.4.x pitch kept, `UnitPitch`/`UnitDiameter` test lines adapted, doc lines rewritten. CreatureBandits wield-guard reorder and its test: **skipped** (1.5.x-only feature). Records and lessons not ported |
| `efe326242` | perf(creatures): v2.0.32 - fewer allocations in creature battles | 1: `SpatialGrid.cs` (+ docs) | **Ported** (A2) after A1; the clock test's 1.5.x-only prefixes pruned; `RequiresGame` lines dropped unless the category exists (D9). Records and lessons not ported |
| `a498e357e` | feat(race-abilities): v2.0.32 - battle abilities per race and culture | 1: 8 `Main` code paths (`FeatureModules.cs`, models), `taom_module_strings.xml`, `SettingsFingerprintTests.cs`, `gamemodels.md` | **Partial** (A3): only the `TickOnMissionThread` reorder, comment rewritten. Race Abilities and every model edit: **skipped** (1.5.x-only feature) |
| `70320bbe1` | fix(race-abilities): v2.0.32 - second review: kill credit, leaner scans | 1: 20+ code paths | **Partial** (A3): only the `OnMissionTick` schedule copy and its two IL tests, comments rewritten. The rest: **skipped** (1.5.x-only feature) |
| `30abf550b` (shared hunk) | Add unit tests for various tools and functionalities | not needed whole | **Adapted** (A2): only `Main/Core/Collections/ReferenceIdentity.cs` and `TAOM.Tests/Core/Collections/ReferenceIdentityTests.cs`, taken as files (hidden prerequisite of `efe326242`'s `SpatialGridReuseTests.cs` and of `13f34c1cb`; the row's note "only TrollBruteForce uses it" missed these uses). Everything else stays with WP107 and WP130 |

Findings this WP owns: **RUNTIME-PERF-05**, Part B. **RUNTIME-PERF-10**, Steps A1 and A2 (its
howdah item, `TaomHowdahMachine.cs:201`, is unfixed on both lines and plan 033 excluded it:
nothing to port). **FREEZE-THREADING-09**, Step A4 (new work; 1.5.x's `RunTree` is identical).

Related items, not ported here: `f75288ebf` (#634) is WP106, the prerequisite A0.4 checks;
`25995dc91`'s pitch and `UnitDiameter` are OUT (WP113 records it; this plan keeps 1.4.x's pitch);
the #713 (`74e9172d4`) and #606 (`fac17e05c`) lines of `mixed-formations.md` are WP123's and
WP111's; the `RequiresGame` tags (`b8c000452`, `e9b28e2ad`) are not ported (D9; A2.4 adapts);
1.4.x plan 003's PERF-07 (warg resolves) is delivered by A1 and its PERF-01 (grid cadence) stays
as on 1.5.x; 1.5.x plan 043 is TODO there (#739), nothing to port; `9f58c58de` is D16.

## Test plan

| Step | New test results | Files (count) |
|---|---|---|
| A1 | 47 | `WargTickCostTests` 17, `WargTreeNodeInjectionTests` 4 (2 methods, one 2-row data test), `BoneCheckDuringAnimationTickTests` 7, `BoneCheckRangeGateTests` 10, `SpatialGridQueryTests` 9 |
| A2 | 51 | `BehaviorTreeMissionLogicDispatchTests` 18, `CallbackSkipLedgerTests` 4, `CreatureTreeClockTests` 2, `SelectorListReuseTests` 1, `SpatialGridDebugServiceTests` +2, `SpatialGridDormancyTests` 10, `SpatialGridReuseTests` 5, `SpatialGridWiringTests` 8, `ReferenceIdentityTests` 1 |
| A3 | 2 | `BehaviorTreeAgentComponentThreadingTests` (one RED first) |
| A4 | 3 | `BehaviorTreeRunawayCapTests` (cap, exact-length boundary, once-per-type line; two RED first) |
| B1 | 27 | `FormationLayoutServiceTests` +22 (18 methods, one 4-row data test), `Patch30ThreadSafetyIlTests` 5 |

Totals: T0 + 103 after Part A, + 27 for Part B, + 130 for both. Existing tests that must stay
green: `SpatialGridRemovalTests`, `WargOffThreadTests`, `DeferredCallbackQueueTests` (WP106's),
`BehaviorTreeMissionLogicInheritanceTests`, `BehaviorTreeBuilderTests`, `WargAttackServiceTests`.
Not unit-testable (commit trailers): a live bite in the hit window, the prefix on the worker
threads, real creature trees under the cap, the engine's callback threads.

## Done criteria (whole plan)

- [ ] Part A and Part B done criteria all hold.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | sort -u | wc -l` prints
      `58` (more only for review follow-ups inside scope).
- [ ] Full Test `Total` = T0 + 130, `Failed: 0`.
- [ ] `grep -rn 'TaleWorlds' Main/Core/Collections/ReferenceIdentity.cs Main/BehaviorTreeWrapper/CallbackSkipLedger.cs` prints nothing.
- [ ] Five commits (A1 to A4, B1), plus review follow-ups if any; every subject passes the check.

## STOP conditions

Stop and report (do not improvise) if:

- A0.3 fails (WP106 or WP123 missing, or this WP already ran), A0.4 prints a `DIFFERS` line, or
  A0.5 shows other engine lines or a static constructor on `ActionIndexCache`.
- A simulation (A1.1, A2.1, B0) lists a conflicted `Main/` or `TAOM.Tests/` path the step does not
  name, `git apply --3way` leaves one unmerged, or the A1.3 end-state check prints a file.
- `CreatureTreeClockTests` names a `DateTime.Now` caller or reports Inconclusive: convert nothing
  beyond Step A2's list; report it.
- A test this plan does not touch fails (`SpatialGridRemovalTests`, `WargOffThreadTests` above all),
  `BehaviorTreeRunawayCapTests` fails after A4.2, `BehaviorTreeBuilderTests` fails at any point, or
  a step's verification fails twice after a reasonable fix attempt.
- After B1 anything exists under `Main/Features/CreatureBandits` or `Main/Features/RaceAbilities`.
- The work seems to need `Main/IoC.cs`, `Main/SubModule.cs`, a csproj, `Dependencies/`, an MCM
  setting, `LayoutPositioner.UnitPitch`, `IFormationAdapter.UnitDiameter`, or any file outside Scope.
- A hook denies a commit (report its message verbatim; never bypass it).

## In-game checks owed (maintainer)

Executors cannot run the game. Logs: the session's `taom_debug_<timestamp>.log` in TAOM's `Logs`
folder (`Main/Core/Logging/FileLogger.cs:26-40`). No TAOM.Dependencies change, so no pairing need.

1. **Warg Custom Battle** (#659): wargs bite running and standing, bites land and end as before,
   rage mode starts and finishes.
2. **Spider battle**: pounce and side attacks fire with their usual cooldowns.
3. **Elephant or mumakil, and a war ram**: trample, side attacks and head-butts keep their
   cooldowns (the timers moved to UTC; no interval may change length).
4. **A battle with no creature**: one `[SpatialGrid] Scheduled rebuild skipped: ...` INFO line and,
   at mission end, one `[BehaviorTree] Mission end: N callbacks skipped with no tree listener ...`
   line; no WARNING or ERROR from either.
5. **No runaway**: in checks 1 to 4, no `node transitions in one RunTree call` line, logged or on screen.
6. **Mixed Formations**: in a field battle a mixed infantry and archer player formation gets its
   default layout within a second, the cycle hotkey rotates layouts, units take their slots, AI
   formations move as before, the order preview works over a laid-out formation; no
   `Patch30 position prefix threw` WARNING and no `fell back to vanilla` line at mission end.
7. **Mixed cavalry**: a laid-out formation that receives cavalry returns to vanilla positioning
   (the SmartCavalryAI handshake) on the next position update. In a siege or hideout Mixed
   Formations stays inert, as before.
8. Optional measurement: if WP117's `[MissionPerf]` lines exist, compare a creature battle and a
   600-agent field battle before and after this branch.

## Hand-off report

Report: the branch; each commit's short SHA and subject; T0 and the count after each step; the
A1.1, A2.1 and B0 simulation outputs; any conflict that needed more than this plan says; the A2.4
`RequiresGame` count and action; whether `CreatureTreeClockTests` passed; the `/deep-review`
outcome per part (or that it could not run); the in-game checks owed. Do not edit
`plans/README.md`; name this row for the integrator: old plan 003 (Step A1 delivers its PERF-07;
this plan does not take its PERF-01, because the 2 s cadence stays as on 1.5.x, so the "WP124 takes
PERF-01" line in the README's old-plan table needs correcting).

## Maintenance notes

- **Review**: CLAUDE.md requires `/deep-review` before C# commits: run it once per part over the
  part's diff if your session can, and land CONFIRMED runtime findings inside Scope as
  `fix(creatures): v2.0.28 - review follow-ups for plan 124 part A` (or `fix(mixed-formations):`
  `... part B`). Probe: `SpatialGrid.Remove` stays parked and off-thread queries never rebuild (V05
  section 4); every tree-clock writer and reader is on `UtcNow`; listener counts change only in
  `Subscribe`/`UnSubscribe`; `FindLaidOutFormation` reads without the lock while writes and the
  slot path hold it; the cavalry gate still evaluates; `StopRunaway` stops before logging and
  cannot throw; the reordered cadence gate keeps its NaN behaviour (a NaN `dt` still never runs
  the tree unless `ShouldRunNextTick`).
- **Behaviour differences to expect**: a skeleton missing during a bite's wind-up ends the bite
  only if still missing when the hit window opens (#659); an off-thread engine callback that no
  tree listens to is dropped instead of parked (the main thread already dropped it); the grid
  skips scheduled rebuilds while unread and rebuilds on the next query; between rebuilds a column
  can return an agent that moved up or down into range (x, y cells); a tree that exceeds 1,000
  transitions in one call stops.
- **Open follow-ups, not in scope**: the grid still holds the last battle's surviving agents, and
  through `Agent.Team` the finished mission, until the next mission replaces `SpatialGrid.Instance`
  (1.5.x review R18, pre-existing). The 2 s grid cadence (1.4.x plan 003 PERF-01) and the
  per-frame howdah skeleton fetch are unchanged.
- **Later WPs**: WP130 (`d96f349f1`, `726975a52`) edits the warg tree after A1's injection and must
  not re-add `ReferenceIdentity` with `30abf550b`; WP143/WP144 (`709649c3f`, `4520af802`) edit
  `ElephantLikeAttackTasks.cs` after A2; WP139 (`cd4fb3719`) edits `BehaviorTreeBannerlordWrapper.cs`;
  WP142 syncs the plan 015, 032 and 033 lessons; the `RequiresGame` tags are not ported (D9), so
  this plan's grid and dispatch tests stay untagged. Same wave: WP127 (`9c29d7324`) edits both rule files and WP125
  the registry; the integrator resolves those at merge.
- **1.5.x after planning**: `53cf195ac` (dire wolves: `WargConfig.cs`, `AgentAdapter.cs`,
  `IAgentAdapter.cs`, `WargAttackService.cs`), `f2ee9a16f` (`IAutonomousMovementController.cs`)
  and merge `e65cfda37` landed after `e45f6df2c`: features, unclassified by this run; a later
  sync should check them for shared fixes.
