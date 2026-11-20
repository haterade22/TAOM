# Plan 056: Park the mission diagnostics' off-thread agent removals, the last unguarded OnAgentRemoved writer

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat 3164713da..HEAD -- Main/Features/MissionDiagnostic TAOM.Tests/Features/MissionDiagnostic docs/features/mission-diagnostic.md Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs Main/Features/AdvancedCombat/MissionThreadGuard.cs TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`
> must print nothing. Then run the census `git grep -n "override void OnAgentRemoved" -- Main`; it must
> list exactly the 16 lines in the census table under "Current state" (same files, same line numbers).
> If either differs, compare the "Current state" excerpts with the live code; a mismatch is a STOP
> condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW (one diagnostics behavior and its engine-free queue; on the main thread nothing changes)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `3164713da`, 2026-11-18
- **Baseline at that commit**: build exit 0; dotnet `Failed: 3, Passed: 42911, Skipped: 9, Total: 42923`,
  failing: `EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems`,
  `EveryKey_IsSeededInAllTwelveLanguageFiles` (translation files owned by the project lead, unrelated to
  this plan); Python suite: 10453 ran, 3 failures (`tools.tests.test_run_long_defeat.PhaseTwoJudgeTests`,
  three sally tests), 13 skipped. This plan touches no Python tool, so the Python suite is not rerun.
- **Issue**: #607

## Why this matters

Bannerlord's native code raises `MissionBehavior.OnAgentRemoved` and `OnAgentDeleted` on whatever
thread it chooses, and player logs have caught `OnAgentRemoved` off the main mission thread (#634,
#607). Issue #607 named five TAOM overrides that wrote main-thread state from that callback without a
guard. At `3164713da` all five are fixed, and a census of all 16 `OnAgentRemoved` overrides in `Main/`
(below) finds exactly one writer still unguarded: `MissionDiagnosticBehavior`, which runs in every
mission and removes the agent from a plain `List` plus `HashSet` (`PendingAgentReads`) on the calling
thread while the main thread adds to and drains the same collections. A `HashSet` mutated from two
threads can corrupt its buckets, and a corrupted .NET Framework hash collection can spin forever, which
is the freeze shape #634 investigated. After this plan an off-thread removal is parked and applied by
the next mission tick before any agent is read, the first one per callback writes one WARNING line to
the debug log, and the main-thread path is unchanged.

## Current state

### The census: every `OnAgentRemoved` override in `Main/` at `3164713da`

`git grep -n "override void OnAgentRemoved" -- Main` lists 16 lines: 15 `MissionBehavior` overrides and
one `AgentComponent.OnAgentRemoved()`. The writer read each whole method and what it calls.

| # | Override (file:line) | Verdict | Why |
|---|---|---|---|
| 1 | `Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs:41` (agent component) | guarded | parks `Retire` through `BehaviorTreeMissionLogic.RunOnMissionThread` |
| 2 | `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs:348` | guarded | `OffMainThread(...)` then `Defer(...)`, replayed from `OnMissionTick` |
| 3 | `Main/Features/ArmourAcquisition/Hooks/HeroKillCounterMissionLogic.cs:55` | safe | reads only; its one write is `HeroKillTally.Add`, an `Interlocked.Add` (`HeroKillTally.cs:15-18`) |
| 4 | `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:147` | guarded | `EndAbilitiesForFallenHero` runs `_deferred.RunOrDefer` (`:162-163`) |
| 5 | `Main/Features/CreatureBandits/CreatureBanditMissionBehavior.cs:104` | guarded | scoreboard bridge is a `ConcurrentDictionary` (`CreatureScoreboardBridge.cs:26`), counter is `Interlocked`, diagnostics go to a `ConcurrentQueue`; `Origin.SetRouted` is the call vanilla makes from the same callback (class doc, `:35-37`) |
| 6 | `Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagnosticsBehavior.cs:55` | guarded | serials are a `ConcurrentDictionary` (`CreatureBanditDiag.cs:39`), events a `ConcurrentQueue` (`:36`), counters `Interlocked`, `Ledger.Get` reads a `ConcurrentDictionary` (`CreatureDiagLedger.cs:95,125`) |
| 7 | `Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs:112` | guarded | `Interlocked.Increment(ref _kills)` and a `ConcurrentQueue<string> _killed` (`:41`) |
| 8 | `Main/Features/FieldCommission/Hooks/FieldCommissionMissionLogic.cs:32` | guarded | reads only, then `FieldCommissionMeritService.RegisterKill` takes `lock (_battleKills)` (`FieldCommissionMeritService.cs:84-91`); a lock, not a concurrent map, by design (kill ties break on insertion order) |
| 9 | `Main/Features/FordsOfWar/Hooks/FordsOfWarMissionLogic.cs:103` | guarded | `FordsMissionDirector.OnAgentRemoved` (`:262-295`) takes the unit from locked registries (`FordsCampAdapter.TryRemove`, declared at `Main/Adapters/FordsCampAdapter.cs:135`, locks at `:142`) and parks the match work through `_deferred.RunOrDefer` |
| 10 | `Main/Features/HeroAbilities/Hooks/HeroAbilitiesMissionLogic.cs:118` | guarded | `_deferred.RunOrDefer(...)` |
| 11 | `Main/Features/HeroAbilities/Hooks/HeroCombatLogic.cs:77` | guarded | reads into locals, then `_deferred.RunOrDefer(...)` |
| 12 | `Main/Features/HeroAuras/Hooks/HeroAurasMissionLogic.cs:119` | guarded | `HeroAuraLedger.Remove` is `ConcurrentDictionary.TryRemove` (`HeroAuraLedger.cs:16,46`) |
| 13 | `Main/Features/LongDefeat/Hooks/LongDefeatMissionLogic.cs:58` | guarded | `LongDefeatDirector.OnAgentRemoved` (`:188-197`) classifies through the locked `LongDefeatAgentRegistry` and `LongDefeatLordWatch`, then `_deferred.RunOrDefer`; `OnBodyEvent` writes the lord watch under its lock |
| 14 | `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs:73` | **unguarded** | `_pending.Remove(...)` on the calling thread; `PendingAgentReads` is a `List` plus a `HashSet` with no lock |
| 15 | `Main/Features/MountDespawn/Hooks/MountDespawnMissionBehavior.cs:56` | guarded | `RecordKill` runs `_deferred.RunOrDefer` (`:79-80`) |
| 16 | `Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs:113` | guarded | reads into locals, then `_deferred.RunOrDefer(...)` |

Only row 14 changes. Its sibling `OnAgentDeleted` (`MissionDiagnosticBehavior.cs:76`) writes the same
collections and is fixed with it (lesson "Hardening a defect class covers every member of the class",
`docs/reviews/lessons/harmony-il.md:619`).

The thread rule the issue asked to correct is already correct at `3164713da`.
`.claude/rules/harmony-patches.md:113` reads: "`[MBCallback]` methods run where native decides;
off-main is observed for `OnAgentRemoved`, `OnAgentHit` and siblings." The engine table
`docs/reference/engine/mission-frame-threads-and-native-costs.md:141` lists `Mission.OnAgentRemoved`
under "Either: native decides, and off-main is observed". No rule or doc edit is needed for that row.

### The files you change

- `Main/Features/MissionDiagnostic/PendingAgentReads.cs`: the engine-free queue of agents waiting for
  their one read. It gains a `DeferredCallbackQueue` and a `RemoveOrDefer` method.
- `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs`: the thin `MissionLogic`
  boundary. It routes both drops through `RemoveOrDefer` and marks the main thread on each tick.
- `TAOM.Tests/Features/MissionDiagnostic/PendingAgentReadsOffThreadTests.cs` (new, engine-free).
- `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticOffThreadTests.cs` (new, `RequiresGame`).
- `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticBehaviorTests.cs`: gains a `[TestCleanup]`,
  because the behavior's tick now marks a static thread id.
- `docs/features/mission-diagnostic.md`: the feature doc.

`Main/Features/MissionDiagnostic/PendingAgentReads.cs` at `3164713da` (whole file, CRLF):

```csharp
using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionDiagnostic;

// Agents seen at creation and waiting for the next mission tick to be read. Holds object references, never
// Agent.Index (indices recycle). Engine-free so the bookkeeping can be tested without the game.
public sealed class PendingAgentReads<T> where T : class
{
    private readonly List<T> _order = new();
    private readonly HashSet<T> _members = new();

    public void Add(T item)
    {
        if (item != null && _members.Add(item)) _order.Add(item);
    }

    public void Remove(T item)
    {
        if (item != null && _members.Remove(item)) _order.Remove(item);
    }

    public void Clear()
    {
        _order.Clear();
        _members.Clear();
    }

    // Hands every pending item to read, in the order added, and empties the queue first: an item is read at most
    // once, and a reader that throws leaves nothing to retry.
    public void Drain(Action<T> read)
    {
        if (_order.Count == 0) return;
        var batch = _order.ToArray();
        Clear();
        foreach (var item in batch) read(item);
    }
}
```

`Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs` at `3164713da` (CRLF; the class
runs from line 13 to line 161, 149 lines):

```csharp
// lines 1-8
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionDiagnostic.Hooks;
```

```csharp
// lines 21-44
    private bool _missionStartLogged;
    private bool _censusOpened;
    private bool _readingStopped;
    private readonly PendingAgentReads<Agent> _pending = new();
    private float _actionSetWindowSecondsLeft = ActionSetWindowSeconds;

    public MissionDiagnosticBehavior(IMissionDiagnosticService service, IRaceManager raceManager, IModLogger logger)
    {
        _service = service;
        _raceManager = raceManager;
        _logger = logger;
    }

    public override void OnMissionTick(float dt)
    {
        // First-tick snapshot: dump the full MissionBehaviors + MissionLogics view
        // AFTER vanilla and all mods have added their behaviors. Mission ctor adds
        // them before tick begins, so first OnMissionTick is the right gate.
        if (!_missionStartLogged)
        {
            _missionStartLogged = true;
            DumpMissionStart();
            OpenCensusOnce();
        }
```

```csharp
// lines 46-49: the tick reads pending agents after the first-tick block
        // The census header and summary still frame a 5 s window. Agents are not polled every frame: each one is
        // queued in OnAgentCreated and read once, here, on the next tick, so a reinforcement that spawns after the
        // window still gets its line. The summary is written once, when the window runs out or the mission ends first.
        ReadPendingAgents();
```

```csharp
// lines 59-76
    // Mission.CreateAgent raises OnAgentCreated; SpawnAgent, its mount and SpawnMonster all reach it. OnAgentBuild
    // is not enough: SpawnMonster ends with BuildAgent(agent, null) and never raises it. The final action set is
    // set after SpawnAgent returns: SandBox MissionAgentHandler runs agent.SetActionSet(..GetActionSet(
    // locationCharacter.ActionSetCode)..) after Mission.SpawnAgent, as SpawnTroopWithAgentBuildData does for a
    // suffix. So the agent is only queued here and read on the next tick. The census may open here: an agent can
    // spawn before the first mission tick.
    public override void OnAgentCreated(Agent agent)
    {
        if (agent == null || _readingStopped) return;
        OpenCensusOnce();
        _pending.Add(agent);
    }

    // Mission.OnAgentRemoved and Mission.OnAgentDeleted raise these; an agent gone before the next tick is not read.
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow) =>
        _pending.Remove(affectedAgent);

    public override void OnAgentDeleted(Agent affectedAgent) => _pending.Remove(affectedAgent);
```

`ReadPendingAgents` (`:78-92`) calls `_pending.Drain(CaptureActionSet)` unless `_readingStopped`, and on
a throw sets `_readingStopped`, calls `_pending.Clear()` and logs
`[MissionDiag] CaptureActionSet failed, census reads stopped for this mission: ...` at WARNING.
`CaptureActionSet` (`:124-146`) reads `agent.ActionSet` (a native call) and then calls
`_service.TryMarkActionSetKey(int, int, bool)`. `OnEndMissionInternal` (`:148-160`) calls
`_pending.Clear()` among its resets. The behavior is constructed once per mission in
`Main/SubModule.cs:2380` with `IoC.Resolve<IModLogger>()`, which is `FileLogger`
(`Main/IoC.cs:276`; `FileLogger.LogWarning` writes a durable `[WARNING]` line,
`Main/Core/Logging/FileLogger.cs:129`). The constructor signature does not change, so `SubModule.cs`
needs no edit.

### The existing pattern you apply

`Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs` (namespace `BehaviorTreeWrapper`, engine-free):

```csharp
// :22
    public DeferredCallbackQueue(Action<string>? report = null) => _report = report;
// :25
    public int Count => _queue.Count;
// :39-49
    public bool RunOrDefer(string site, Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (MissionThreadGuard.NoteCall(site, _report))
        {
            _queue.Enqueue(action);
            return true;
        }
        action();
        return false;
    }
// :56-66 Drain runs, in order, every callback queued before the call; :69-72 Clear drops them all.
```

`Main/Features/AdvancedCombat/MissionThreadGuard.cs` (namespace `TAOM.Features.AdvancedCombat`, static,
engine-free): `MarkMainThread()` (`:25`) records the calling thread; `IsOnMainThread` (`:34-41`) is true
on the marked thread and before any mark; `NoteCall(site, report)` (`:48-60`) returns true off the
marked thread and reports once per site with the text
`$"[TAOM] {site} ran off the main mission thread (thread {current}, main {main}). ..."`; before any mark
it returns false (runs inline). `ResetForTests()` (`:62-67`) clears the mark and the reported sites.

The exemplar is `Main/Features/MountDespawn/Hooks/MountDespawnMissionBehavior.cs`: a
`DeferredCallbackQueue` built with `message => _logger.LogWarning(message)` (`:50`), removals routed
through `_deferred.RunOrDefer("MountDespawnMissionBehavior.OnAgentRemoved", ...)` (`:80`), and
`MissionThreadGuard.MarkMainThread(); _deferred.Drain();` at the top of `OnMissionTick` (`:120-121`),
because each queue's owner marks the main thread itself (RCA #634 finding 6,
`docs/reviews/rca-offthread-agent-removed-2026-09-22.md:100`). Its tests,
`TAOM.Tests/Features/MountDespawn/MountDespawnOffThreadTests.cs`, are the test pattern: a bare
`Agent` from `FormatterServices.GetUninitializedObject`, a worker `Thread` joined at once, and
`MissionThreadGuard.ResetForTests()` plus `MarkMainThread()` in `[TestInitialize]`.

Here the queue goes inside `PendingAgentReads`, the object that owns the collections, as
`LongDefeatDirector` and `FordsMissionDirector` own theirs. Two reasons: the entry point has no line
budget left (next section), and the queue's tests then run without the game.

### The ADR-002 line gate

`TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs` fails any `MissionLogic` class of 150 lines
or more that is not in `EntryPointLineCountBaseline.txt` (counted from the `class` line to its closing
brace, comments included). `MissionDiagnosticBehavior` is not in the baseline and is 149 lines today.
The edit in Step 5 adds three lines and condenses two comments, which the writer counted at 147. The
baseline only shrinks: never add a line to it.

### Engine facts (v1.5.7, from `pwsh tools/taom-src.ps1 path Agent` and its siblings)

- `TaleWorlds.MountAndBlade.MissionBehavior.cs`: `public virtual void OnAgentCreated(Agent agent)`
  (`:57`), `public virtual void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)`
  (`:85`), `public virtual void OnAgentDeleted(Agent affectedAgent)` (`:89`),
  `public virtual void OnEndMissionInternal()` (`:125`), `public virtual void OnMissionTick(float dt)`
  (`:150`); `public Mission Mission { get; internal set; }` (`:11`), so a behavior not added to a
  mission has a null `Mission`.
- `TaleWorlds.MountAndBlade.Mission.cs:2980-2996` and `:2998-3016`: both are
  `[MBCallback(null, false)] internal void OnAgentDeleted(Agent)` / `OnAgentRemoved(...)` with no
  managed caller, so native picks the thread. `OnAgentRemoved` loops
  `missionBehavior2.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);`.
- `TaleWorlds.MountAndBlade.Agent.cs`: `public sealed class Agent` (`:14`); `GetHashCode()` returns
  `_creationIndex` (`:5026-5029`), a `private readonly int` (`:606`); `Agent` does not override
  `Equals` (`grep -c "override bool Equals"` gives 0). Its base class
  `TaleWorlds.DotNet.DotNetObject.cs` (`pwsh tools/taom-src.ps1 path DotNetObject`) overrides neither
  (`grep -c "override bool Equals"` and `grep -c "override int GetHashCode"` both give 0). So `HashSet<Agent>.Remove` and
  `List<Agent>.Remove` hash a managed field and compare references: a parked removal never reads
  native memory, even for an agent the engine has deleted by the time it runs.
- `Agent.ActionSet` is `new MBActionSet(MBAPI.IMBAgent.GetActionSetNo(GetPtr()))` (`:722`), a native
  call: reading an agent after its removal is what the queue exists to avoid.
- `KillingBlow` is a `struct` in `TaleWorlds.MountAndBlade` (`TaleWorlds.MountAndBlade.KillingBlow.cs:9`);
  `AgentState` is an enum in `TaleWorlds.Core` with `Killed`.

### How each deferred body handles a deleted agent

The only deferred body is `() => Remove(item)` inside `PendingAgentReads.RemoveOrDefer`. It captures
the agent reference only, hashes it through `Agent.GetHashCode()` (a managed field) and compares
references; it never reads a property of the agent. The next tick drains the parked removals before
`Drain` hands any agent to `CaptureActionSet`, so an agent removed off the main thread is never read.

### Blast radius

`python tools/graphify_taom.py status --brief` at `3164713da` printed
`graphify code graph: MISSING`, so the callers come from `git grep -n -w`:

- `PendingAgentReads`: used only by `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs:24`
  and its tests `TAOM.Tests/Features/MissionDiagnostic/PendingAgentReadsTests.cs`.
- `MissionDiagnosticBehavior`: constructed in `Main/SubModule.cs:2380` (signature unchanged), named in
  `Main/Features/CoopInterop/Missions/CoopMissionRoles.cs:118` (`nameof`, unchanged) and as text in
  `TAOM.Tests/Composition/FeatureModulesTests.cs:293` (unchanged); tested by
  `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticBehaviorTests.cs`.

### Conventions that bind this change

- ADR-002 (`docs/adrs/002-thin-entry-points.md`): entry points stay under 150 lines and delegate;
  gate `EntryPointLineCountGateTests`.
- ADR-007 (`docs/adrs/007-adapter-pattern.md`): logic takes no sealed TaleWorlds type. Keep
  `PendingAgentReads<T>` generic and engine-free.
- ADR-008 (`docs/adrs/008-testability-requirements.md`): logic is unit-tested; the boundary's engine
  calls are not.
- ADR-003, ADR-004, ADR-005: no `#region`, no `[Obsolete]`, no `#if`; gate `BannedConstructsGateTests`.
- `.claude/rules/csharp-architecture.md:125` (rule 2): "A collection an engine callback writes goes
  through `DeferredCallbackQueue.RunOrDefer`, or a lock when order does not matter. Wire
  `MissionThreadGuard.NoteCall` into a new native write." (`RunOrDefer` calls `NoteCall`.)
- `.claude/rules/traps/mission.md:32`: "Indices recycle and callbacks run off-thread: never key on
  `Agent.Index`, write via `RunOrDefer`".
- Lesson `docs/reviews/lessons/harmony-il.md:633`: an off-thread report delegate goes to the locked
  file log (`IModLogger`), never to an on-screen logger.
- No gameplay number or behaviour changes; this is a diagnostics-only path.

## Commands you will need

Prefix every `dotnet` command with the `TEMP="<tmp>" TMP="<tmp>"` your dispatch rules give.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the Step 1 totals with Passed and Total each 7 higher (7 new tests), the same failing names (at planning: `Failed: 3, Passed: 42918, Skipped: 9, Total: 42930`) |
| One test class | the same, plus `--filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Entry point gate | the same, plus `--filter "FullyQualifiedName~EntryPointLineCountGateTests"` | 11 run, 11 pass (the class has 11 `[TestMethod]`s at `3164713da`) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | the exit code recorded in Step 1 |
| RefAsm unit step | see Step 7 | the engine-free tests pass on stubs |

Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify or create):
- `Main/Features/MissionDiagnostic/PendingAgentReads.cs`
- `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs`
- `TAOM.Tests/Features/MissionDiagnostic/PendingAgentReadsOffThreadTests.cs` (new)
- `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticOffThreadTests.cs` (new)
- `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticBehaviorTests.cs`
- `docs/features/mission-diagnostic.md`

**Out of scope** (do NOT touch, even though they look related):
- The other 15 overrides in the census table: they are guarded; changing them is not this plan.
- The 8 other `OnAgentDeleted` overrides in `Main/` (see Maintenance notes).
- `.claude/rules/harmony-patches.md` and the engine thread table: already correct.
- `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`: single-owner; nothing here needs them. If
  the fix seems to, STOP and report the exact line.
- `DeferredCallbackQueue.cs` and `MissionThreadGuard.cs`: used as they are, never changed.
- `CHANGELOG.md`, `plans/README.md`.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding a line to `EntryPointLineCountBaseline.txt`. STOP and report
  instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value="...">` in `Main/_Module/SubModule.xml` when you commit (`v2.0.40` at planning).
  Suggested: `fix(diagnostics): v2.0.40 - park off-thread agent removals` (58 characters).
- Write the message to a file in your scratch folder and run `git commit -F "<file>"`. Stage the six
  in-scope paths by name. No AI attribution trailer. Never `--no-verify`.
- Draft body (re-check every claim against your diff before you use it; wrap at 72):

  ```text
  The mission diagnostics queue each new agent and read its action set
  on the next tick. Native raises OnAgentRemoved and OnAgentDeleted on
  the thread it chooses, and player logs have caught OnAgentRemoved off
  the main mission thread (#634, #607). Both callbacks removed the agent
  from the queue's list and set on that thread while the main thread
  could add to or read them.

  An off-thread removal is now parked and applied by the next mission
  tick, before any agent is read, so only the main thread touches the
  queue. The first off-thread removal per callback writes one WARNING
  line to the debug log. On the main thread nothing changes. A census
  of the other OnAgentRemoved overrides in TAOM found none that writes
  shared state from that callback without a deferral, a lock or a
  concurrent collection.

  Not-tested: native raising these callbacks off the main thread in a
  live battle; an in-game field battle is owed.
  ```

## Steps

### Step 1: record the base

Run the drift check and the census from the header. Then run the build and the full test command and
write the totals line and the failing test names into your report. Run
`python tools/lint_docs.py --fail-on-drift` and record its exit code. Confirm the engine fact the fix
relies on: `pwsh tools/taom-src.ps1 path Agent` prints a path; in that file,
`grep -c "override bool Equals"` gives `0` and `grep -n "public override int GetHashCode" -A3` shows
`return _creationIndex;`. Then `pwsh tools/taom-src.ps1 path DotNetObject` prints a path; in that file
`grep -c "override bool Equals"` and `grep -c "override int GetHashCode"` each give `0`. Finally run
`git status --porcelain` and record every line it prints (the run worktree may already hold untracked
`plans/` files); Steps 7 and 8 and the Done criteria exempt exactly these pre-existing paths.

**Verify**: totals `Failed: 3, Passed: 42911, Skipped: 9, Total: 42923` with the three translation
tests named in Status, or the difference explained (later steps compare against the totals you record
here); neither `Agent` nor `DotNetObject` overrides `Equals` or `GetHashCode` other than `Agent`'s
`return _creationIndex;`.

### Step 2: add the seam with today's behaviour, and write the failing tests (RED)

2a. In `Main/Features/MissionDiagnostic/PendingAgentReads.cs`, add these three members right after the
`_members` field, and nothing else. They keep today's behaviour (the removal still runs on the calling
thread), so the tests below compile and fail on their assertions:

```csharp
    public PendingAgentReads(Action<string>? report = null) { }

    public int Count => _members.Count;

    public void RemoveOrDefer(string site, T item) => Remove(item);
```

2b. Create `TAOM.Tests/Features/MissionDiagnostic/PendingAgentReadsOffThreadTests.cs` (engine-free, no
`TestCategory`):

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MissionDiagnostic;

namespace TAOM.Tests.Features.MissionDiagnostic;

// Native raises OnAgentRemoved and OnAgentDeleted on the thread it chooses (#634, #607) while the main thread adds
// agents and drains the queue. A removal off the main thread is parked, and the next Drain applies it before it reads
// anything. Plain objects stand in for agents; MissionThreadGuard and DeferredCallbackQueue are engine-free.
[TestClass]
public class PendingAgentReadsOffThreadTests
{
    private const string Site = "PendingAgentReadsOffThreadTests.Remove";
    private PendingAgentReads<object> _sut = null!;
    private List<string> _reports = null!;
    private List<object> _read = null!;

    private static void OnWorker(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
        _reports = new List<string>();
        _read = new List<object>();
        _sut = new PendingAgentReads<object>(_reports.Add);
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void RemoveOrDefer_OnTheMainThread_RemovesAtOnce()
    {
        var a = new object();
        _sut.Add(a);

        _sut.RemoveOrDefer(Site, a);

        Assert.AreEqual(0, _sut.Count);
        Assert.AreEqual(0, _reports.Count, "a main-thread removal is not reported");
    }

    [TestMethod]
    public void RemoveOrDefer_OffTheMainThread_WaitsForTheNextDrain_WhichAppliesItBeforeReading()
    {
        var gone = new object();
        var kept = new object();
        _sut.Add(gone);
        _sut.Add(kept);

        OnWorker(() => _sut.RemoveOrDefer(Site, gone));

        Assert.AreEqual(2, _sut.Count, "the worker must not touch the pending set");
        _sut.Drain(_read.Add);
        CollectionAssert.AreEqual(new[] { kept }, _read);
    }

    [TestMethod]
    public void RemoveOrDefer_OffTheMainThread_ReportsTheSiteOnce()
    {
        OnWorker(() =>
        {
            _sut.RemoveOrDefer(Site, new object());
            _sut.RemoveOrDefer(Site, new object());
        });

        Assert.AreEqual(1, _reports.Count);
        StringAssert.Contains(_reports[0], Site + " ran off the main mission thread");
    }

    [TestMethod]
    public void Clear_DropsParkedRemovals_SoNoneIsAppliedAfterIt()
    {
        var a = new object();
        _sut.Add(a);
        OnWorker(() => _sut.RemoveOrDefer(Site, a));

        _sut.Clear();
        _sut.Add(a);
        _sut.Drain(_read.Add);

        CollectionAssert.AreEqual(new[] { a }, _read);
    }
}
```

2c. Create `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticOffThreadTests.cs`:

```csharp
using System;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MissionDiagnostic;
using TAOM.Features.MissionDiagnostic.Hooks;

namespace TAOM.Tests.Features.MissionDiagnostic;

// The behavior's wiring of the off-thread drop (#607): both engine callbacks park their drop when native raises them
// off the main thread, report the site once to the file log, and the next tick applies the drop before it reads any
// agent. The tick marks the main thread itself, so the deferral never leans on another behavior's mark (#634).
[TestClass]
[TestCategory("RequiresGame")]   // MissionDiagnosticBehavior derives from the engine's MissionLogic
public class MissionDiagnosticOffThreadTests
{
    private IMissionDiagnosticService _service = null!;
    private IModLogger _logger = null!;
    private MissionDiagnosticBehavior _sut = null!;

    // The drop keeps the reference and never reads it (Agent.GetHashCode is a managed field), so a bare instance is
    // enough, as in MountDespawnOffThreadTests. Reading it (CaptureActionSet) would reach native code and fail.
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private static void OnWorker(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
        _service = Substitute.For<IMissionDiagnosticService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new MissionDiagnosticBehavior(_service, Substitute.For<IRaceManager>(), _logger);
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void OnMissionTick_MarksTheMainThreadItself()
    {
        MissionThreadGuard.ResetForTests();
        _sut.OnMissionTick(0.016f);

        var onMain = true;
        OnWorker(() => onMain = MissionThreadGuard.IsOnMainThread);

        Assert.IsFalse(onMain, "once the behavior has ticked, a worker must read as off the main thread");
    }

    [TestMethod]
    public void OnAgentRemoved_OffTheMainThread_IsParked_AndTheAgentIsNeverRead()
    {
        var agent = BareAgent();
        _sut.OnAgentCreated(agent);

        OnWorker(() => _sut.OnAgentRemoved(agent, null!, AgentState.Killed, default));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("MissionDiagnosticBehavior.OnAgentRemoved ran off the main mission thread")));

        _sut.OnMissionTick(0.016f);

        _service.DidNotReceiveWithAnyArgs().TryMarkActionSetKey(default, default, default);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("CaptureActionSet failed")));
    }

    [TestMethod]
    public void OnAgentDeleted_OffTheMainThread_IsParked_AndTheAgentIsNeverRead()
    {
        var agent = BareAgent();
        _sut.OnAgentCreated(agent);

        OnWorker(() => _sut.OnAgentDeleted(agent));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("MissionDiagnosticBehavior.OnAgentDeleted ran off the main mission thread")));

        _sut.OnMissionTick(0.016f);

        _service.DidNotReceiveWithAnyArgs().TryMarkActionSetKey(default, default, default);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("CaptureActionSet failed")));
    }
}
```

If the compiler reports an unused `using` as an error, remove that `using` only.

2d. In `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticBehaviorTests.cs`, add
`using TAOM.Features.AdvancedCombat;` to the usings and this member after `Setup()`, so the static mark
the tick will set (Step 5) never leaks into a later test:

```csharp
    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();
```

**Verify** (RED): build `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` exits 0. Then
run the test command with `--filter "FullyQualifiedName~PendingAgentReadsOffThreadTests"`: 4 tests run,
exactly these 2 fail on their assertions:
`RemoveOrDefer_OffTheMainThread_WaitsForTheNextDrain_WhichAppliesItBeforeReading` (the stub removes
`gone` at once and `kept` stays, so MSTest reports `Expected:<2>. Actual:<1>`) and `RemoveOrDefer_OffTheMainThread_ReportsTheSiteOnce` (0 reports, expected 1);
`RemoveOrDefer_OnTheMainThread_RemovesAtOnce` and `Clear_DropsParkedRemovals_SoNoneIsAppliedAfterIt`
pass (today's behaviour; the second fails in Step 3). Then with
`--filter "FullyQualifiedName~MissionDiagnosticOffThreadTests"`: 3 run, all 3 fail
(`OnMissionTick_MarksTheMainThreadItself` on its `IsFalse`; the two `..._IsParked_...` tests on
`Received(1)`, an NSubstitute `ReceivedCallsException` because no warning was logged). Quote both
summaries in your report.

### Step 3: park off-thread removals in `PendingAgentReads` (GREEN for the queue)

Replace the Step 2 stub. The file becomes, apart from `Clear` (Step 4), and keeps its CRLF line
endings (`file Main/Features/MissionDiagnostic/PendingAgentReads.cs` must still say "with CRLF line
terminators"; if a rewrite left LF, convert it back before you go on):

```csharp
using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;

namespace TAOM.Features.MissionDiagnostic;

// Agents seen at creation and waiting for the next mission tick to be read. Holds object references, never
// Agent.Index (indices recycle). Engine-free so the bookkeeping can be tested without the game.
// Threads: the mission tick drains this queue on the main mission thread. Native raises OnAgentRemoved and
// OnAgentDeleted on the thread it chooses (#634, #607), so RemoveOrDefer parks an off-thread removal and the next
// Drain applies it before it reads anything.
public sealed class PendingAgentReads<T> where T : class
{
    private readonly List<T> _order = new();
    private readonly HashSet<T> _members = new();
    private readonly DeferredCallbackQueue _deferred;

    // report: where a site's first off-thread removal is reported. It runs on the reporting thread, so it must be the
    // thread-safe file log, never an on-screen logger.
    public PendingAgentReads(Action<string>? report = null) => _deferred = new DeferredCallbackQueue(report);

    public int Count => _members.Count;

    public void Add(T item)
    {
        if (item != null && _members.Add(item)) _order.Add(item);
    }

    public void Remove(T item)
    {
        if (item != null && _members.Remove(item)) _order.Remove(item);
    }

    // Remove on the main mission thread; anywhere else the removal waits for the next Drain. The parked call keeps
    // only the reference: Remove hashes and compares it and never reads the item, so an agent the engine deletes in
    // between is safe (Agent.GetHashCode returns a managed field, and Agent does not override Equals).
    public void RemoveOrDefer(string site, T item)
    {
        if (item != null) _deferred.RunOrDefer(site, () => Remove(item));
    }

    public void Clear()
    {
        _order.Clear();
        _members.Clear();
    }

    // Applies the parked removals, then hands every pending item to read, in the order added, and empties the queue
    // first: an item is read at most once, and a reader that throws leaves nothing to retry.
    public void Drain(Action<T> read)
    {
        _deferred.Drain();
        if (_order.Count == 0) return;
        var batch = _order.ToArray();
        Clear();
        foreach (var item in batch) read(item);
    }
}
```

`_deferred.Drain()` must be the first statement of `Drain`, before the early return: a parked removal
of an agent waiting in the batch must land before that agent is read.

**Verify**: the filter `FullyQualifiedName~PendingAgentReadsOffThreadTests`: 4 run, 3 pass, and
`Clear_DropsParkedRemovals_SoNoneIsAppliedAfterIt` now fails (the parked removal replays at the Drain,
so `_read` is empty where `[a]` is expected). That is its RED; quote it. The filter
`FullyQualifiedName~PendingAgentReadsTests`: 9 run, 9 pass.

### Step 4: `Clear` drops parked removals

Replace `Clear` and the `Clear();` call inside `Drain` so that `Drain` empties only the items and
`Clear` empties the items and the parked removals:

```csharp
    // Drops the pending items and every parked removal (mission end, a failed read).
    public void Clear()
    {
        ClearItems();
        _deferred.Clear();
    }
```

In `Drain`, change `Clear();` to `ClearItems();`, and add at the end of the class:

```csharp
    private void ClearItems()
    {
        _order.Clear();
        _members.Clear();
    }
```

**Verify**: `FullyQualifiedName~PendingAgentReadsOffThreadTests`: 4 run, 4 pass.
`FullyQualifiedName~PendingAgentReadsTests`: 9 run, 9 pass.

### Step 5: route the behavior's drops through `RemoveOrDefer` and mark the main thread

Edit `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs` (CRLF; keep it), exactly
these changes. The code blocks below sit inside a numbered list, so each shows 3 extra spaces of
markdown indent: write the lines with the file's own indent, 4 spaces for a member or member comment
and 8 for an expression-body continuation line.

1. Add `using TAOM.Features.AdvancedCombat;` after `using TAOM.Core.Logging;`.
2. Field (line 24): `private readonly PendingAgentReads<Agent> _pending = new();` becomes
   `private readonly PendingAgentReads<Agent> _pending;`.
3. Constructor: after `_logger = logger;` add
   `_pending = new PendingAgentReads<Agent>(message => _logger.LogWarning(message));`.
4. `OnMissionTick`: insert as its first statement, directly above the `// First-tick snapshot` comment
   and with no blank line after it:
   `MissionThreadGuard.MarkMainThread(); // this behavior owns a deferral queue, so it marks the thread itself (#634)`.
5. Replace the six-line comment above `OnAgentCreated` (lines 59-64; the full reasoning is already in
   `docs/features/mission-diagnostic.md`, "Why the next tick and why `OnAgentCreated`") with:

   ```csharp
       // Mission.CreateAgent raises this for SpawnAgent, its mount and SpawnMonster; the action set an agent keeps is set after
       // SpawnAgent returns, so the agent is queued here and read on the next tick, and the census may open here (feature doc).
   ```

6. Replace lines 72-76 (the comment, `OnAgentRemoved` and `OnAgentDeleted`) with:

   ```csharp
       // Native raises these on the thread it chooses (#634, #607): an off-thread drop waits for the next tick, before any read.
       public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow) =>
           _pending.RemoveOrDefer("MissionDiagnosticBehavior.OnAgentRemoved", affectedAgent);

       public override void OnAgentDeleted(Agent affectedAgent) => _pending.RemoveOrDefer("MissionDiagnosticBehavior.OnAgentDeleted", affectedAgent);
   ```

Nothing else in the file changes. `ReadPendingAgents` and `OnEndMissionInternal` keep calling
`_pending.Drain` and `_pending.Clear()`, which now apply and drop the parked removals.

**Verify**:
- Build `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: exit 0.
- `awk '/public sealed class MissionDiagnosticBehavior/{s=NR} END{print NR-s+1}' Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs`
  prints a number below 150 (the writer's draft gave 147; the file ends with the class's closing brace).
- `FullyQualifiedName~MissionDiagnosticOffThreadTests`: 3 run, 3 pass.
- `FullyQualifiedName~MissionDiagnosticBehaviorTests`: 3 run, 3 pass.
- `FullyQualifiedName~EntryPointLineCountGateTests`: 11 run, 11 pass.
- `git grep -n "_pending.Remove(" -- Main/Features/MissionDiagnostic` returns nothing (exit 1). At
  `3164713da` it prints the two lines this step replaces (`MissionDiagnosticBehavior.cs:74` and `:76`).
  Keep the path scope: across all of `Main` the same text also matches
  `MountDespawnMissionBehavior.cs:113`, `:170` and `TitleLedger.cs:110`, which this plan does not touch.

### Step 6: the feature doc

Edit `docs/features/mission-diagnostic.md` (CRLF; no em or en dash in any line you write):

1. In the "Solution Approach" paragraph that begins "The same behavior logs every unique", after the
   sentence ending "an agent deleted or removed before that tick is dropped.", insert: "Native raises
   `OnAgentRemoved` and `OnAgentDeleted` on the thread it chooses (#634, #607), so an off-thread drop
   is parked (`PendingAgentReads.RemoveOrDefer`, through `DeferredCallbackQueue.RunOrDefer`) and the
   next tick applies it before any read; the first one per callback writes one WARNING line. The
   behavior marks the main thread at the top of each tick."
2. In the "Key Files" row for `MissionDiagnosticBehavior.cs`, change "drops them in `OnAgentRemoved`
   and `OnAgentDeleted`, reads them on the next tick." to "drops them in `OnAgentRemoved` and
   `OnAgentDeleted` (parked for the next tick when native raises them off the main thread), reads them
   on the next tick."
3. In "Tests", replace the last sentence ("The read of an engine `Agent` ... (no `Agent` can be built in
   the tests).") with: "`TAOM.Tests/Features/MissionDiagnostic/PendingAgentReadsOffThreadTests.cs`
   covers the off-thread removal (parked until the next drain, applied before any read, reported once,
   dropped by `Clear`), and `MissionDiagnosticOffThreadTests.cs` (`RequiresGame`) covers the behavior's
   wiring with a bare uninitialized `Agent`: the tick marks the main thread, and an agent removed or
   deleted off the main thread is never read. The read of a live engine `Agent` is exercised in game on
   every launch; the census-opened-by-a-spawn-before-the-first-tick path is not unit tested."
4. Under "Changelog", add a line: "- 2026-11-18: the removal and deletion drops are parked for the next
   tick when native raises them off the main thread (#607)."

**Verify**: `python tools/lint_docs.py --fail-on-drift` gives the exit code recorded in Step 1 and
reports no dash on the lines you added.

### Step 7: full suite and the no-game replay of the new engine-free tests

Run these in this order: the RefAsm build writes the same `bin` and `obj` folders as the normal
build, so the normal build must come after it.

1. The RefAsm unit step from `.ai/verification.md`, filtered to the queue's engine-free classes (they
   touch `MissionThreadGuard` and `DeferredCallbackQueue` only):
   `dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` then
   `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PendingAgentReads&TestCategory!=RequiresGame"`.
   **Verify**: 13 run (9 + 4), 13 pass. If the RefAsm restore fails for a machine or network reason,
   report the exact error and continue; that is an environment gap, not a STOP.
2. Rebuild against the real game references, forcing a full compile:
   `dotnet build TAOM.Tests --no-incremental -p:DisableModuleCopy=true -p:ModuleId=`. **Verify**: exit 0.
3. The full test command. **Verify**: the Step 1 totals with Passed and Total each 7 higher, Failed
   and Skipped unchanged, and the same failing names as Step 1 (at planning:
   `Failed: 3, Passed: 42918, Skipped: 9, Total: 42930`).
4. `git status --porcelain` lists the six in-scope paths and, apart from them, only the lines you
   recorded in Step 1.

### Step 8: commit

Stage the six in-scope paths by name and commit with the message from "Git workflow", re-checked
against your diff (version from `Main/_Module/SubModule.xml`).

**Verify**: `git log -1 --stat` shows one commit with exactly those six files; `git status --porcelain`
prints only the lines you recorded in Step 1 (nothing, if Step 1 recorded nothing).

## Test plan

- `PendingAgentReadsOffThreadTests` (4, engine-free): the main-thread removal is immediate and
  unreported; an off-thread removal leaves the set alone until the next `Drain`, which applies it
  before reading; the site is reported once; `Clear` drops a parked removal. Model after
  `TAOM.Tests/Features/MountDespawn/MountDespawnOffThreadTests.cs`.
- `MissionDiagnosticOffThreadTests` (3, `RequiresGame`): the tick marks the main thread; an off-thread
  `OnAgentRemoved` and an off-thread `OnAgentDeleted` each report their site to `IModLogger.LogWarning`
  and the agent is never read on the next tick (no `TryMarkActionSetKey` call, no
  `CaptureActionSet failed` warning).
- RED evidence: Step 2 (2 queue tests and 3 behavior tests fail), Step 3
  (`Clear_DropsParkedRemovals_SoNoneIsAppliedAfterIt` fails before Step 4).
- What cannot be tested: native raising the two callbacks off the main thread in a live battle
  (the `Not-tested:` trailer).

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] The full test command (run after Step 7.2's normal rebuild) prints the Step 1 totals with Passed
      and Total each 7 higher, and the same failing names as Step 1 (at planning:
      `Failed: 3, Passed: 42918, Skipped: 9, Total: 42930`, the three translation tests)
- [ ] `--filter "FullyQualifiedName~PendingAgentReadsOffThreadTests"`: 4 run, 4 pass;
      `--filter "FullyQualifiedName~MissionDiagnosticOffThreadTests"`: 3 run, 3 pass
- [ ] `--filter "FullyQualifiedName~EntryPointLineCountGateTests"`: 11 run, 11 pass, and
      `git diff 3164713da -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` is empty
- [ ] `git grep -n "_pending.Remove(" -- Main/Features/MissionDiagnostic` returns nothing, and
      `git grep -n "RemoveOrDefer(\"MissionDiagnosticBehavior.OnAgent" -- Main` returns 2 lines
- [ ] `git grep -n "MarkMainThread" -- Main/Features/MissionDiagnostic` returns 1 line
- [ ] `python tools/lint_docs.py --fail-on-drift` gives the Step 1 exit code
- [ ] After the commit, `git status --porcelain` prints only the lines recorded in Step 1, and
      `git show --stat HEAD` lists only the six in-scope files
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes

## STOP conditions

Stop and report (do not improvise) if:

- The drift check prints a change, or the census lists a line not in the table: an override added or
  changed since `3164713da` may be a new unguarded writer, which this plan has not classified.
- The code at the "Current state" locations does not match the excerpts.
- Step 1 finds an `Equals` override on `Agent` or `DotNetObject`, a `GetHashCode` override on
  `DotNetObject`, or `Agent.GetHashCode` no longer returns `_creationIndex`:
  the claim that a parked removal never reads native memory would be false.
- The Step 2 RED set differs from the one stated (a test expected to fail passes, or the reverse).
- The line count in Step 5 is 150 or more, or `EntryPointLineCountGateTests` fails. Do not add a
  baseline line and do not trim comments other than the two this plan rewrites; report the count.
- A test outside this plan's classes fails that the base did not fail (for example from the static
  thread mark leaking): report the names.
- The fix seems to need `Main/SubModule.cs`, `Main/IoC.cs`, the csproj, `DeferredCallbackQueue.cs` or
  `MissionThreadGuard.cs`.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue #607: comment with the census table from "Current state" (the issue's body still lists five
  unguarded overrides, all fixed before this plan), and, once merged, close it with the label
  `triage-needs-ingame` for the in-game check below.
- `/deep-review`, tier 1 (mission callbacks, threading).
- No `/localize`: the new text is a debug-log line, not player-facing.
- `docs/features/mission-diagnostic.md` is updated by the executor; no new feature, so no
  `docs/reference/feature-map.md` row changes.

## After merge: the maintainer's actions

In-game check owed (no in-game run this tick): one field battle of a few minutes with a TAOM build from
the merged commit. In that session's `taom_debug_*.log`, expect the census lines
(`[MissionDiag] ActionSet census open` and `closed`), no `[MissionDiag] CaptureActionSet failed` line,
and no freeze. If native raised a removal off the main thread in that battle, the log carries
`[WARNING] [TAOM] MissionDiagnosticBehavior.OnAgentRemoved ran off the main mission thread` (or
`OnAgentDeleted`) once; its absence means only that this battle raised none off-thread.

## Maintenance notes

- Every `MissionBehavior` that writes mission state from an engine callback needs this guard; the
  census command is `git grep -n "override void OnAgentRemoved" -- Main`. Re-run it when a feature adds
  one.
- Not covered here: the 8 other `OnAgentDeleted` overrides in `Main/`
  (`git grep -n "override void OnAgentDeleted" -- Main`). The writer saw that `BehaviorTreeMissionLogic`
  and `MountDespawnMissionBehavior` defer and `SignatureStrikesMissionLogic` uses a concurrent roster
  (RCA #634), but did not verify `CareerPerkMissionBehavior.OnAgentDeleted`
  (`CareerAbilityBuffTracker.ClearAllyBuff`), `AdvancedCombatBehavior.OnAgentDeleted`
  (`_adapterFactory.Evict`) or `MixedFormationsMissionBehavior.OnAgentDeleted`
  (`_service.ForgetAgent`): UNVERIFIED, a candidate follow-up census.
- `MissionThreadGuard`'s mark is static. This behavior is in every mission, so after this plan every
  mission marks the main thread from its first tick; 14 call sites in 13 files already mark it
  (`git grep -n "MarkMainThread()" -- Main`, which also prints the declaration), always from a mission
  tick on the main thread, so the marks agree.
- `Add` runs from `OnAgentCreated`, which `Mission.CreateAgent` raises during a spawn. The engine
  thread table (`docs/reference/engine/mission-frame-threads-and-native-costs.md:138`) puts
  `Mission.SpawnAgent` on the main thread but does not list `OnAgentCreated` itself, and its "off-main is
  observed" row (`:141`) does not name it. This plan leaves `Add` inline; if a log ever catches
  `OnAgentCreated` off the main thread, it needs the same deferral.
- While census reads are stopped (`_readingStopped`), `ReadPendingAgents` skips `Drain`, so parked
  removals wait until `OnEndMissionInternal` clears them; each holds only a closure and a reference.
- What the review should probe: `_deferred.Drain()` being first in `Drain`; `Clear` (mission end, a
  failed read) dropping parked removals while `Drain` does not; the reporter going to `IModLogger`
  (file log), never an on-screen logger; the class line count against the gate.
- `FordsOfWarMissionLogic.Fault` increments `_faults` without `Interlocked` and can be reached from an
  off-thread `OnAgentRemoved`; a lost increment only changes how many of the first five faults are
  logged. Left alone as harmless.
