# Plan 032: Make the worker-thread formation patch and the wield getters cheap and thread-safe

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat dffdf879..HEAD -- Main/Features/MixedFormations Main/Adapters/FormationAdapter.cs Main/Adapters/IFormationAdapter.cs Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs TAOM.Tests/Features/MixedFormations TAOM.Tests/Features/CreatureBandits docs/reference/harmony-patch-registry.md docs/features/mixed-formations.md docs/reviews/lessons/harmony-il.md .claude/rules/harmony-patches.md .claude/rules/csharp-architecture.md`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Expected, not a STOP: plan 031 may have changed
> `Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs`,
> `Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs`, a new
> `Main/Features/MixedFormations/CachedEnumParse.cs`, their tests and lines of
> `docs/features/mixed-formations.md`. Those files are not edited here; only the excerpts this plan
> quotes must still match.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED (a lock-free read on engine worker threads; a hot predicate every agent asks)
- **Depends on**: none. Plan 031 caches `IMixedFormationsSettingsProvider`; this plan only calls that
  provider through its existing interface (`_settings.IsEnabled`) and never edits it.
- **Category**: perf
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (untranslated keys; the paid
  translator run waits on the maintainer). Python suite: not recorded, and this plan touches no
  `tools/` file.
- **Issue**: filed by the orchestrator before execution

## Why this matters

Two TAOM Harmony prefixes run on the engine's TWParallel agent-tick workers for every agent in a
battle. `Patch30_FormationGetOrderPositionOfUnit` (Mixed Formations) runs about twice a second for every
AI unit in a formation, and on each call in a field battle it allocates a `FormationAdapter`; for a
formation holding position it also reads `FormationQuerySystem.IsCavalryFormation` (which re-evaluates
eleven class-count queries on the calling worker, unlocked, once the cached value is five seconds old)
and takes the service's single global lock, whether or not the formation has a layout. Almost no
formation has one: layouts go only to the player team's mixed infantry and archer formations. The
Patch93 wield guards (`Agent.GetPrimaryWieldedItemIndex`, `GetOffhandWieldedItemIndex`,
`GetMissileRange`) are asked from dozens of engine sites on every thread, and each one reads four
members of every human agent before deciding "not a creature". After this plan a formation with no
layout returns to vanilla with no lock, no allocation and no query read, a laid-out formation reuses the
adapter the main thread built, the cavalry check reads the main thread's cached value, and a human is
ruled out after two reads. Positions and wield answers stay the same for every formation and agent.

## Current state

### Files and their roles

- `Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs` (80 lines): the
  prefix on `Formation.GetOrderPositionOfUnit`. Allocates an adapter per call (line 41). Its comments
  at lines 13-14 and 27 claim "up to 40,000x per frame", which is wrong (see "Call rate").
- `Main/Features/MixedFormations/FormationLayoutService.cs` (244 lines): the singleton holding the
  per-formation layout (`_layoutByFormation`) and slot cache (`_assignmentCache`) under one `_lock`.
- `Main/Features/MixedFormations/IFormationLayoutService.cs` (50 lines): its interface; the patch and
  `MixedFormationsMissionBehavior` resolve it.
- `Main/Adapters/FormationAdapter.cs` (158 lines) and `Main/Adapters/IFormationAdapter.cs` (94 lines):
  the adapter over the sealed `Formation`; `FormationAdapter` is the only implementer of the interface
  (`git grep -n ": IFormationAdapter" -- Main TAOM.Tests` shows only `FormationAdapter.cs:11`).
- `Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs` (71 lines): `Is(Agent?)`, the
  creature-bandit predicate every Patch93 guard calls.
- `Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs` (146 lines): the three wield-getter
  guards at lines 106-146 and their doc comment at lines 95-105.
- `TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`: the service tests
  (class-tagged `[TestCategory("RequiresGame")]`, NSubstitute fakes built by `MakeFormation`).
- Docs that repeat the wrong call rate or describe the changed design:
  `docs/reference/harmony-patch-registry.md` (section `## Patch30_MixedFormations`, lines 212-227),
  `docs/features/mixed-formations.md` (lines 30, 32, 34, 134, 148, 150, 152), `docs/reviews/lessons/harmony-il.md`
  (line 125), and the two rule lines that say worker-thread shared state "takes a lock":
  `.claude/rules/harmony-patches.md:148-149` and `.claude/rules/csharp-architecture.md:270-271`.

Every existing file this plan edits is CRLF in the worktree (`core.autocrlf` is true; the committed
blobs are LF): edit with the Edit tool, never `sed -i`, and keep each file's existing endings. Give the
two new test files the same CRLF endings as their neighbours.

### Patch30 today (`Patch30_FormationGetOrderPositionOfUnit.cs:9-80`, from the planned-at commit)

```csharp
[HarmonyPatch(typeof(Formation), nameof(Formation.GetOrderPositionOfUnit))]
[HarmonyPatchCategory("Patch30_MixedFormations")]
public static class Patch30_FormationGetOrderPositionOfUnit
{
    // Cached service reference — the Prefix fires per-unit-per-formation-position-recalculation
    // (up to 40,000× per frame in worst-case 200-unit formations). DryIoc singleton resolves are
    // fast, but caching the singleton in a static field skips the dict lookup entirely. Per
    // .claude/rules/harmony-patches.md hot-path reflection caching pattern.
    private static IFormationLayoutService? _service;

    [HarmonyPrefix]
    public static bool Prefix(Formation __instance, Agent unit, ref WorldPosition __result)
    {
        try
        {
            // Open-field-only: skip all mixed-formation repositioning in siege / sally-out / hideout /
            // naval / settlement missions (Mission.IsFieldBattle is FALSE for all of them). Returning
            // true lets vanilla GetOrderPositionOfUnit compute the slot. Placed first to short-circuit
            // this per-unit hot path (~40,000×/frame) before any IoC resolve or adapter allocation.
            if (Mission.Current?.IsFieldBattle != true) return true;

            // Banner bearers own their slot. ... (lines 30-35 unchanged by this plan)
            if (unit?.Banner != null) return true;

            var service = _service ??= IoC.Resolve<IFormationLayoutService>();
            if (service == null) return true;

            var formation = new FormationAdapter(__instance);
            var agentIsRanged = unit?.Character?.IsRanged ?? false;
            var agentIndex = unit?.Index ?? -1;
            if (agentIndex < 0) return true;

            var planePosition = service.ComputeUnitPlanePosition(formation, agentIndex, agentIsRanged);
            if (!planePosition.HasValue) return true;

            var scene = Mission.Current?.Scene;
            if (scene == null) return true;

            var p = planePosition.Value;
            var probe = new Vec3(p.x, p.y, 0f, -1f);
            var groundHeight = scene.GetGroundHeightAtPosition(probe, BodyFlags.CommonCollisionExcludeFlags);
            var candidate = new WorldPosition(scene, new Vec3(p.x, p.y, groundHeight, -1f));
            // (lines 57-70: the IsFormationUnitPositionAvailable check, unchanged by this plan)
            ...
            __result = candidate;
            return false;
        }
        catch
        {
            return true;
        }
    }
}
```

### The service today (`FormationLayoutService.cs`)

Fields and the lock comment, lines 19-31:

```csharp
    private readonly Dictionary<object, FormationLayoutType> _layoutByFormation = new();
    private readonly Dictionary<object, SlotAssignment> _assignmentCache = new();

    // Codex review #35 finding 2 (MEDIUM): Bannerlord runs Formation positioning across worker
    // threads (Formation.OrderPositionLock; Mission.IsFormationUnitPositionAvailableAuxMT uses
    // TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock); the "MT" suffix on
    // CreateNewOrderWorldPositionMT etc. denotes multi-threaded helpers). Patch30 fires from
    // those threads, so EnsureAssignment cache writes + ByAgentIndex inserts could race against
    // OnMissionTick-driven CycleLayouts/ApplyDefaultsToFormations writes from the main thread.
    // All dict + SlotAssignment.ByAgentIndex mutations now hold this lock. Reads on the hot
    // path also lock briefly to ensure consistency. Single uncontended lock acquisition is ~25ns
    // on x86, well below the per-call budget.
    private readonly object _lock = new();
```

`SetLayout`, lines 54-63:

```csharp
    public void SetLayout(IFormationAdapter formation, FormationLayoutType layout)
    {
        if (formation == null) return;
        lock (_lock)
        {
            _layoutByFormation[formation.FormationKey] = layout;
            // Layout changed → assignment cache for this formation is stale. Drop it; will rebuild lazily.
            _assignmentCache.Remove(formation.FormationKey);
        }
    }
```

`ComputeUnitPlanePosition`, lines 65-102 (the gates run in this order; the dictionary check is last):

```csharp
    public Vec2? ComputeUnitPlanePosition(IFormationAdapter formation, int agentIndex, bool agentIsRanged)
    {
        if (formation == null) return null;
        if (!_settings.IsEnabled) return null;
        if (!formation.IsHolding) return null;
        if (!formation.OrderPositionIsValid) return null;
        // Cross-feature handshake: SmartCavalryAI (Patch31) owns cavalry formation
        // positioning during charge maneuvers. Skip Patch30 even if a layout was
        // manually assigned to a cavalry formation via the cycle hotkey.
        if (formation.RepresentativeIsCavalry) return null;

        // The dict reads + writes happen under the lock; the math after is pure and uses
        // already-captured values, so it doesn't need the lock.
        (int row, int file) slot;
        lock (_lock)
        {
            if (!_layoutByFormation.TryGetValue(formation.FormationKey, out var layout))
                return null;
            if (layout == FormationLayoutType.Vanilla) return null;

            var assignment = EnsureAssignmentLocked(formation, layout);
            if (assignment == null) return null;

            if (!assignment.ByAgentIndex.TryGetValue(agentIndex, out slot))
            {
                slot = _positioner.AssignNextSlot(assignment, new FormationUnit(agentIndex, agentIsRanged));
                assignment.Assign(agentIndex, agentIsRanged, slot);
            }
        }

        // Convert slot (row, file) to local-space offset, then transform by formation direction
        // and add formation order position to get the final plane position. Pure math; lock-free.
        var unitInterval = LayoutPositioner.UnitPitch(formation);
        var localOffset = new Vec2(slot.file * unitInterval, -slot.row * unitInterval);
        var direction = formation.Direction;
        var rotated = direction.TransformToParentUnitF(localOffset);
        return formation.OrderPosition + rotated;
    }
```

`CycleLayouts`, lines 104-128 (its lock block, lines 109-122):

```csharp
        lock (_lock)
        {
            foreach (var formation in formations)
            {
                if (formation == null || formation.CountOfUnits == 0) continue;
                if (!_layoutByFormation.TryGetValue(formation.FormationKey, out var current)) continue;

                var next = NextLayout(current);
                _layoutByFormation[formation.FormationKey] = next;
                _assignmentCache.Remove(formation.FormationKey);
                lastLayout = next;
                affected++;
            }
        }
```

`NextLayout` (lines 236-243) never returns `Vanilla`, and maps `Vanilla` itself to
`InfantryFrontRangedBack` (the `_ =>` arm, line 242). So a cycle keeps every non-Vanilla formation
laid out, but a formation whose stored layout is `Vanilla` (only `SetLayout(f, Vanilla)` stores one,
line 59) GAINS a layout: at the base, `SetLayout(f, Vanilla)` then `CycleLayouts` makes
`ComputeUnitPlanePosition` return a position. The snapshot must follow that (Step 7.5).

`ApplyDefaultsToFormations`, lines 130-158:

```csharp
    public int ApplyDefaultsToFormations(IReadOnlyList<IFormationAdapter> formations)
    {
        if (!_settings.IsEnabled) return 0;
        var defaultLayout = _settings.DefaultLayout;
        if (defaultLayout == FormationLayoutType.Vanilla) return 0;

        var assigned = 0;
        // (comment lines 137-140)
        lock (_lock)
        {
            foreach (var formation in formations)
            {
                if (formation == null || formation.CountOfUnits < 2) continue;
                if (_layoutByFormation.ContainsKey(formation.FormationKey)) continue;
                if (!IsMixedFormationInternal(formation)) continue;

                _layoutByFormation[formation.FormationKey] = defaultLayout;
                assigned++;
            }
        }
        // (debug log, return)
    }
```

`OnMissionEnd`, lines 180-191: under `_lock`, `_layoutByFormation.Clear(); _assignmentCache.Clear();`.

`IsMixedFormationInternal` (lines 193-220) reads `formation.RepresentativeIsCavalry` on the MAIN thread
(from `ApplyDefaultsToFormations`) before a layout is ever assigned. That read stays as it is.

**The writers of the layout set, all of them.** `git grep -n "_layoutByFormation" -- Main` at the
planned-at commit lists reads and writes only inside `FormationLayoutService.cs`: `GetLayout` (read),
`SetLayout` (write), `ComputeUnitPlanePosition` (read), `CycleLayouts` (write; a stored `Vanilla` becomes a layout),
`ApplyDefaultsToFormations` (write), `OnMissionEnd` (clear). Production callers: `ApplyDefaultsToFormations`
from `MixedFormationsMissionBehavior.ApplyDefaultsToPlayerTeam` (`OnMissionTick`, once a second, main
thread), `CycleLayouts` from its hotkey handler (main thread), `OnMissionEnd` from `OnEndMission`.
`SetLayout` has no production caller (`git grep -n "SetLayout(" -- Main` shows only the interface and the
service); the tests use it.

### The adapter today (`FormationAdapter.cs:38-41`, `IFormationAdapter.cs:37-45`)

```csharp
    public object FormationKey => _formation;

    public bool RepresentativeIsCavalry =>
        _formation?.QuerySystem != null && _formation.QuerySystem.IsCavalryFormation;
```

```csharp
    /// <summary>Opaque identity for caching — typically the underlying <c>Formation</c>
    /// reference. Two adapters wrapping the same formation must return the same key.</summary>
    object FormationKey { get; }

    // -- SmartCavalryAI extensions (Patch31) -------------------------------------

    /// <summary>True when the formation's representative class is cavalry — backed by
    /// <c>FormationQuerySystem.IsCavalryFormation</c> in v1.3.15.</summary>
    bool RepresentativeIsCavalry { get; }
```

`RepresentativeIsCavalry` is also read on the main thread by SmartCavalryAI
(`CavalryChargeService.cs:122, 206`, `CavalryPathPlanner.cs:40`, `Patch31_FormationSetMovementOrder.cs:98`,
`SmartCavalryAIMissionBehavior.cs:90`). It must keep its current meaning; this plan ADDS a second member
for the worker thread and leaves this one alone.

Exemplar for a per-formation adapter built on the main thread and reused:
`Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs:27-30` keeps
`Dictionary<Formation, FormationAdapter> _cavCache`. Here the reuse is simpler: the service already
receives a `FormationAdapter` (built on the main thread by `MixedFormationsMissionBehavior.TryGetTeamAdapters`)
at the moment it assigns a layout, so it keeps that instance.

### The creature predicate today

`CreatureBanditAgents.cs:8-23`:

```csharp
/// <summary>
/// The engine side of <see cref="CreatureBanditRules.IsCreatureBandit"/>: reads the three facts off an agent in
/// cheapest-first order, because the weapon guards ask it for every agent. <c>Character</c> is a managed field
/// and null for every ordinary mount; <c>IsHuman</c> is a flags-pointer read; only a creature bandit reaches the
/// id lookup. It asks IsHuman, not IsMount: route A clears Mountable once the creature is built, and every guard
/// must keep recognising it after that. Thread-safe (reads only), so the guards on the engine's worker threads
/// may call it.
/// </summary>
internal static class CreatureBanditAgents
{
    internal static bool Is(Agent? agent)
    {
        var characterId = agent?.Character?.StringId;
        return characterId != null
            && CreatureBanditRules.IsCreatureBandit(characterId, agent!.IsHuman, agent.RiderAgent != null);
    }
```

C# evaluates every argument before the call, so for a soldier (Character set, StringId set) `Is` reads
`Character`, `StringId`, `IsHuman` and `RiderAgent`, then `IsCreatureBandit` returns false on
`!isHuman`. For an ordinary mount (`Character` null) it stops after one read.

`CreatureBanditRules.cs:20, 54-55`:

```csharp
    public static bool IsCreatureTroop(string? troopId) => troopId != null && Troops.Contains(troopId);
    ...
    public static bool IsCreatureBandit(string? characterId, bool isHuman, bool hasRider)
        => !isHuman && !hasRider && IsCreatureTroop(characterId);
```

The three guards, `Patch93_CreatureBandits.cs:106-146` (primary shown; offhand at 120-132 is identical
with `CreatureGuardKind.OffhandWield`; missile range at 134-146 sets `__result = 0f` with
`CreatureGuardKind.MissileRange`):

```csharp
[HarmonyPatch(typeof(Agent), nameof(Agent.GetPrimaryWieldedItemIndex))]
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditPrimaryWieldGuard
{
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance, ref EquipmentIndex __result)
    {
        if (!CreatureBanditAgents.Is(__instance)) return true;
        CreatureBanditDiag.NoteGuard(CreatureGuardKind.PrimaryWield, __instance);
        __result = EquipmentIndex.None;
        return false;
    }
}
```

Their doc comment, lines 101-103, claims "the check is read-only and exits at the first field read for
any agent that is not a creature", which is false for a human today (four reads).

Tests that pin this file by source text and must stay green:
`CreatureBanditsWiringTests.Fingerprint_DoesNotAskIsMount` (the stripped source of
`CreatureBanditAgents.cs` must not contain `IsMount` and must contain `IsHuman`) and
`Unmount_AndUnlist_LiveOnlyInTheRouteAStep` (the file must not contain `RemoveMountWithoutRider` or
`CombatantFlags`).

### Engine facts (v1.5.3, from `pwsh tools/taom-src.ps1 path <Type>` and the decompile dump)

- `Formation` (`TaleWorlds.MountAndBlade.Formation.cs`):
  `public WorldPosition GetOrderPositionOfUnit(Agent unit)` at line 1374. Formation overrides
  `GetHashCode` (`return (int)(Team.TeamIndex * 10 + FormationIndex);`, lines 2595-2598) and does NOT
  override `Equals`, so a `Dictionary<object, ...>` keyed by a `Formation` compares by reference, as
  `_layoutByFormation` already does. `public FormationQuerySystem QuerySystem { get; private set; }`
  at line 253.
- `FormationQuerySystem` (`TaleWorlds.MountAndBlade.FormationQuerySystem.cs`):
  ```csharp
  197:	public bool IsCavalryFormation => _isCavalryFormation.Value;
  199:	public bool IsCavalryFormationReadOnly => _isCavalryFormation.GetCachedValueUnlessTooOld();
  444:		_isCavalryFormation = new QueryData<bool>(() => formationQuerySystem.CavalryUnitRatio > ... , 5f);
  446:		QueryData<float>.SetupSyncGroup(_infantryUnitRatio, _hasShieldUnitRatio, _rangedUnitRatio, _cavalryUnitRatio, _rangedCavalryUnitRatio, _isMeleeFormation, _isInfantryFormation, _hasShield, _isRangedFormation, _isCavalryFormation, _isRangedCavalryFormation);
  ```
- `QueryData<T>` (file ``TaleWorlds.MountAndBlade.QueryData`1.cs``): `Value` (lines 18-37) reads
  `Mission.Current.CurrentTime` and, once `currentTime >= _expireTime`, calls `Evaluate` on every member
  of its sync group and then itself, on whatever thread asked. `GetCachedValueUnlessTooOld()` (lines
  73-76) is `return _cachedValue;` with no time check and no evaluation. The class-count lambdas walk
  `Formation.Arrangement.GetAllUnits()` (`Formation.GetCountOfUnitsBelongingToPhysicalClass`, line 969),
  a collection the main thread changes.
- **Where the cached value is refreshed on the main thread**: `Formation.OnUnitAddedOrRemoved` and
  `OnBatchUnitRemovalEnd` call `QuerySystem.ExpireAfterUnitAddRemove()` (Formation.cs:1983, 1998), which
  evaluates `_isCavalryFormation` at once (FormationQuerySystem.cs:744); `DefaultBattleMissionAgentSpawnLogic`
  calls `EvaluateAllPreliminaryQueryData()` at spawn (line 254); and any main-thread `.IsCavalryFormation`
  read after the 5 s lifetime re-evaluates it (MixedFormations' own `IsMixedFormationInternal` does,
  before a formation gets a layout; SmartCavalryAI's tick does every frame for the player's formations
  when that feature is on).
- **Call rate** (the corrected fact for the comments and docs): `Agent.TickParallel` (Agent.cs:4718) runs
  on the TWParallel workers; at lines 4757-4760 it checks `_cachedAndFormationValuesUpdateTimer`
  (created at line 1626 with a duration of `0.45f + MBRandom.RandomFloat * 0.1f`), and when it fires
  calls `ParallelUpdateCachedAndFormationValuesForAIAgent`, which reaches
  `HumanAIComponent.ParallelUpdateFormationMovement` (HumanAIComponent.cs:666) and through
  `GetFormationFrame` (line 608) `Agent.GetBaseFormationFrame` (Agent.cs:2086), whose line 2091 calls
  `Formation.GetOrderPositionOfUnit(this)`. The `AdjustStartTime(-5f)` at line 4759 only makes the same
  frame's `TickAsAI` (line 5575-5577) fire too; `TaleWorlds.Core.Timer.Check` (dump
  `TaleWorlds.Core.cs:21628-21650`) then advances the start past the current time, so each AI agent in
  a formation calls the target about twice a second. Other callers: every caller of
  `Agent.ForceUpdateCachedAndFormationValues` (Formation, Team, OrderController, BannerBearerLogic and the
  deployment handlers; a burst of one call per unit) and `OrderController.SimulateDestinationFrames`
  (OrderController.cs:1443-1452, the player's order preview, main thread). Nothing supports "40,000 per
  frame".
- `Agent` (`TaleWorlds.MountAndBlade.Agent.cs`):
  ```csharp
  520:	private Agent _cachedRiderAgent;
  522:	private BasicCharacterObject _character;
  538:	private UIntPtr _flagsPointer;
  642:	public bool IsHuman => (GetAgentFlags() & AgentFlag.IsHumanoid) != 0;
  744:	public Agent RiderAgent => GetRiderAgentAux();          // 5312-5314: return _cachedRiderAgent;
  1426:	public BasicCharacterObject Character { get { return _character; } ... }
  2722:	public EquipmentIndex GetPrimaryWieldedItemIndex() { return AgentHelper.GetPrimaryWieldedItemIndex(_primaryWieldedItemIndexPointer); }
  2922:	public AgentFlag GetAgentFlags() { return AgentHelper.GetAgentFlags(FlagsPointer); }   // FlagsPointer => _flagsPointer (1483)
  3159:	public float GetMissileRange() { return MBAPI.IMBAgent.GetMissileRange(GetPtr()); }
  ```
  `AgentHelper.GetAgentFlags` (AgentHelper.cs:33-37) is an inlined unsafe read: `return *(AgentFlag*)flagsPtr.ToPointer();`.
  `AgentFlag` is `enum AgentFlag : uint` (TaleWorlds.Core.AgentFlag.cs), `Mountable = 1u`,
  `IsHumanoid = 0x800u`, `CanWieldWeapon = 0x4000u`.
  `MBObjectBase.StringId` is a public auto-property `{ get; set; }` (TaleWorlds.ObjectSystem.MBObjectBase.cs:12).
  So `Character` is one managed field load, `IsHuman` one native-memory read, `RiderAgent` one field
  load, `StringId` one auto-property load: `Character` rules out every mount, `IsHuman` every soldier.

### Conventions that bind this change

- **ADR-002** (`docs/adrs/002-thin-entry-points.md`): entry points stay under 150 lines and delegate;
  the patch keeps its shape and gains one lookup.
- **ADR-007** (`docs/adrs/007-adapter-pattern.md`): services never take sealed TaleWorlds types. The new
  service method takes `object formationKey` (the opaque identity `IFormationAdapter.FormationKey`
  already exposes) and returns an `IFormationAdapter`; the new query read goes on the adapter.
- **ADR-008** (`docs/adrs/008-testability-requirements.md`): services 100% covered with mocks; the
  patch body is engine-bound, so its shape is pinned by IL tests instead.
- **`.claude/rules/harmony-patches.md` "Which thread runs your target"** (line 139 names
  `GetOrderPositionOfUnit` on the worker pool; lines 148-149): shared state reachable from a worker-thread
  patch takes a lock. This plan adds the second permitted shape (an immutable snapshot written under the
  lock and swapped by reference) and amends that line and `.claude/rules/csharp-architecture.md:270-271`
  to say so. The lesson `docs/reviews/lessons/harmony-il.md:80` already lists "read an immutable
  snapshot" as a fix for this class.
- **`.claude/rules/tests.md` "Test categories"**: a test that executes engine code (a `Vec2`, a bare
  `Agent`) carries `[TestCategory("RequiresGame")]` on its class; IL-only tests that merely resolve
  member tokens run on the hosted CI's reference assemblies untagged (precedent:
  `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs`).
- **IL scanning helper**: `TAOM.Tests/Migration/IlCallScanner.cs`, `ExtractCalledMethods(MethodBase, byte[])`
  walks the IL in order and yields every call, callvirt and newobj target.
- **Bare agents in tests**: `TAOM.Tests/Features/AdvancedCombat/SpatialGridRemovalTests.cs:23`,
  `(Agent)FormatterServices.GetUninitializedObject(typeof(Agent))`, class-tagged `RequiresGame`.
- `TAOM.Tests` sees TAOM internals (`Main/TAOM.csproj:112-117`, `InternalsVisibleTo`).

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`, graph refreshed at the worktree tip, code identical to `dffdf879`)

- `FormationLayoutService`: only `FormationLayoutServiceTests` (every listed test method is in
  `TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`, which holds 37
  `[TestMethod]`/`[DataTestMethod]` attributes at the planned-at commit).
- `IFormationLayoutService`: "No unique node match". By grep: `Patch30_FormationGetOrderPositionOfUnit.cs:17,38`,
  `MixedFormationsMissionBehavior.cs:13,28`, `MixedFormationsIoC.cs:11`. No test substitutes it.
- `FormationAdapter`: "No affected nodes found". By `git grep -n "new FormationAdapter" -- Main`: built in
  `Patch30_FormationGetOrderPositionOfUnit.cs:41`, `MixedFormationsMissionBehavior.cs:88, 96`,
  `BattlefieldQueryAdapter.cs:36`, `BattleActionBarMissionView.cs:124`,
  `Patch31_FormationSetMovementOrder.cs:97`, `Patch31b_FormationSetTargetFormation.cs:53` and
  `SmartCavalryAIMissionBehavior.cs:84`. Adding a member changes none of them; only the Patch30 one is
  removed.
- `IFormationAdapter`: `BattlefieldQueryAdapter`, `CavalryPathPlanner`, `CavalryChargeService` (8 methods),
  `BattleActionBarService`, `FormationCompositionAnalyzer`, `BattleActionBarVM`, every
  `FormationLayoutService` method, `LayoutPositioner`, `CavalryChargeServiceTests.MakeBattlefield`.
  A new interface member needs only `FormationAdapter` (the sole implementer); NSubstitute fakes return
  `false` for it by default.
- `Patch30_FormationGetOrderPositionOfUnit`: "No affected nodes found" (Harmony applies it by category,
  `Main/SubModule.cs:593`).
- `CreatureBanditAgents`: `IsCreatureBanditDecorator.Evaluate`, `CreatureBanditMissionBehavior.OnAgentBuild`
  and `.OnAgentRemoved`, `CreatureBanditDamage.Reduce`, `CreatureScoreboardBridge.OnRemoved`, the five
  `Patch93_CreatureBandits.cs` hooks (lines 68, 89, 113, 127, 141), `TaomCustomBattleAgentStatCalculateModel.CanAgentRideMount`,
  both morale models' `CanPanicDueToMorale`, `TaomBattleRewardModel.CanTroopBeTakenPrisoner` and
  `.GetLootPrisonerChances`, `TaomCustomBattleCreatureDamageModel.ApplyDamageReductions`,
  `CreatureBanditsWiringTests.RefusesPrisoner_CreatureAndTrollBanditTroopsOnly`. All of them call `Is`
  (or its siblings) and get the same answer after the reorder.

Re-run these queries at your tip (after `python tools/graphify_taom.py refresh --if-stale`); a caller not
listed here that writes the layout set is a STOP condition.

## Step 0: the maintainer's edit

None. No protected file changes.

## Commands you will need

Prefix every `dotnet` command with `TEMP="<tmp>" TMP="<tmp>"` from your dispatch rules.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests; the only failure is `EveryLanguage_DeclaresARowForEveryEnglishKey` |
| One test class | the same, plus `--filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Data | `python tools/validate_moduledata.py` | 0 ERRORs (no data changes here; run once at the end) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | the same exit code as your Step 1 run |
| Engine check | `timeout 900 pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.FormationQuerySystem` | prints a path to a `.cs` file (it can take minutes) |
| RefAsm unit step | see Step 9 | the new untagged IL tests pass on reference assemblies |

Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify or create):
- `Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs`
- `Main/Features/MixedFormations/FormationLayoutService.cs`
- `Main/Features/MixedFormations/IFormationLayoutService.cs`
- `Main/Adapters/FormationAdapter.cs`
- `Main/Adapters/IFormationAdapter.cs`
- `Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs`
- `Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs` (doc comment lines 101-103 only)
- `TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`
- `TAOM.Tests/Features/MixedFormations/Patch30ThreadSafetyIlTests.cs` (new)
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditWieldGuardTests.cs` (new)
- `docs/reference/harmony-patch-registry.md` (section `## Patch30_MixedFormations` only)
- `docs/features/mixed-formations.md` (lines 30, 32, 34, 148, 150, 152, and one new bullet after 134)
- `docs/reviews/lessons/harmony-il.md` (one phrase in line 125)
- `.claude/rules/harmony-patches.md` (lines 148-149)
- `.claude/rules/csharp-architecture.md` (lines 270-271)

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: single-owner, and nothing here needs them
  (no registration or category changes). If one seems needed, STOP and report the exact line.
- `Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs`,
  `Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs`: plan 031 owns them.
- PatchShield and `PatchShieldPolicy` (plan 034 owns the finalizer on this target).
- The two native queries in Patch30 (`scene.GetGroundHeightAtPosition`, `mission.IsFormationUnitPositionAvailable`,
  lines 54 and 68): left as they are. A cached slot that could go stale would change behaviour; that is a
  maintainer decision (FOR-MIKE), not this plan's.
- `LayoutPositioner.cs` and `FormationAdapter.Units` (LINQ on a slot-cache miss, which happens only when
  a layout is assigned or cycled).
- `FormationAdapter.RepresentativeIsCavalry` and every SmartCavalryAI caller: unchanged.
- `CreatureBanditRules.IsCreatureBandit` and its tests: unchanged; `Is` keeps calling it.
- `CHANGELOG.md` (generated at `/release`; the commit bodies are the changelog entries), `plans/README.md`.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry. The two cavalry-handshake tests get one added
  setup line each (Step 5); their assertions stay as they are.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at the planned-at commit; a
  hook refuses any other).
- Two commits, one per concern (Step 11): the creature predicate, then the formation patch.
- Stage explicit paths only. Write each message to a file and run `git commit -F "<file>"`. Never
  `--no-verify`.
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. No AI attribution trailer. Use `Not-tested:`, `Constraint:` and `Research:` trailers as given.

## Steps

### Step 1: record the base

Run the full test suite and the docs linter before any edit:

```
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
python tools/lint_docs.py --fail-on-drift
```

Write the dotnet totals line, the failing test names and the linter's exit code into your report. Also
run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FormationLayoutServiceTests"`
and record its total (all pass at the base); Step 11 counts the new results against it. Save the output
of `git status --porcelain` to a file in your scratch folder: your worktree may already carry untracked
files that are not yours (the run's plan files), and Steps 10 and 11 compare against this snapshot.

**Verify**: the totals are `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` with the one
failure `EveryLanguage_DeclaresARowForEveryEnglishKey`, or the difference is explained (another plan
merged first). Then run the engine check from the Commands table and confirm, in the file it prints,
that line 197 is `public bool IsCavalryFormation => _isCavalryFormation.Value;` and line 199 is
`public bool IsCavalryFormationReadOnly => _isCavalryFormation.GetCachedValueUnlessTooOld();`; then run
the command below (the type name ends in a backtick and a 1, quoted as shown) and confirm, in the file it
prints, that `GetCachedValueUnlessTooOld` returns `_cachedValue` and nothing else. Either check failing
is a STOP.

```
timeout 900 pwsh tools/taom-src.ps1 path 'TaleWorlds.MountAndBlade.QueryData`1'
```

### Step 2: the creature predicate's tests (RED: the read order)

Create `TAOM.Tests/Features/CreatureBandits/CreatureBanditWieldGuardTests.cs`, namespace
`TAOM.Tests.Features.CreatureBandits`, class `CreatureBanditWieldGuardTests`, tagged
`[TestClass]` and `[TestCategory("RequiresGame")]` (it builds bare `Agent` objects). Usings it needs:
`System`, `System.Collections.Generic`, `System.Linq`, `System.Reflection`,
`System.Runtime.InteropServices`, `System.Runtime.Serialization`,
`Microsoft.VisualStudio.TestTools.UnitTesting`, `TaleWorlds.Core`, `TaleWorlds.MountAndBlade`,
`TAOM.Features.CreatureBandits`, `TAOM.Features.CreatureBandits.Hooks`, `TAOM.Tests.Migration`.

The fixture (adapt names freely; the shape is load-bearing):

```csharp
    private const string Brood = "taom_spider_brood_forest";   // a catalogue creature troop (CreatureBanditRulesTests)
    private readonly List<IntPtr> _flagBlocks = new();

    [TestCleanup]
    public void FreeFlagBlocks()
    {
        foreach (var block in _flagBlocks) Marshal.FreeHGlobal(block);
        _flagBlocks.Clear();
    }

    private static FieldInfo AgentField(string name)
    {
        var field = typeof(Agent).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Agent.{name} is gone; the bare-agent fixture no longer matches the engine.");
        return field;
    }

    // A bare agent whose IsHuman, Character and RiderAgent read what the row says. IsHuman dereferences
    // _flagsPointer (AgentHelper.GetAgentFlags), so every agent gets a real block of unmanaged memory.
    private Agent MakeAgent(AgentFlag flags, string? characterId, bool hasRider)
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var block = Marshal.AllocHGlobal(sizeof(uint));
        _flagBlocks.Add(block);
        Marshal.WriteInt32(block, unchecked((int)(uint)flags));
        AgentField("_flagsPointer").SetValue(agent, new UIntPtr((ulong)block.ToInt64()));
        if (characterId != null)
        {
            var character = (BasicCharacterObject)FormatterServices.GetUninitializedObject(typeof(BasicCharacterObject));
            character.StringId = characterId;
            AgentField("_character").SetValue(agent, character);
        }
        if (hasRider)
            AgentField("_cachedRiderAgent").SetValue(agent, FormatterServices.GetUninitializedObject(typeof(Agent)));
        return agent;
    }

    // The predicate as it stood before plan 032: every cell must give the answer it gave.
    private static bool OldPredicate(AgentFlag flags, string? characterId, bool hasRider)
        => characterId != null
           && CreatureBanditRules.IsCreatureBandit(characterId, (flags & AgentFlag.IsHumanoid) != 0, hasRider);
```

The agent kinds, as `[DataRow]`s shared by the three guard tests (columns: flags, character id, has
rider, is a creature bandit):

| Kind | flags | id | rider | creature |
|---|---|---|---|---|
| soldier | `AgentFlag.IsHumanoid \| AgentFlag.CanWieldWeapon` | `"taom_test_soldier"` | false | false |
| husk rider of a creature troop (vanilla fallback spawn) | `AgentFlag.IsHumanoid` | `Brood` | false | false |
| half-built humanoid, no Character (the 2026-08-10 warg case) | `AgentFlag.IsHumanoid` | `null` | false | false |
| ordinary riderless mount | `AgentFlag.Mountable` | `null` | false | false |
| ridden mount | `AgentFlag.Mountable` | `null` | true | false |
| creature bandit, route A applied (Mountable cleared) | `AgentFlag.CanAttack \| AgentFlag.CanDefend` | `Brood` | false | true |
| creature bandit, route A skipped (still Mountable) | `AgentFlag.Mountable` | `Brood` | false | true |
| creature troop agent with a rider | `AgentFlag.Mountable` | `Brood` | true | false |
| non-humanoid with a non-creature Character | `AgentFlag.Mountable` | `"taom_test_soldier"` | false | false |

Use the literal `"taom_spider_brood_forest"` in the attributes (a `const` works too). Give each row a
`DisplayName`.

Tests:

1. `PrimaryWieldGuard_EveryAgentKind_DecidesAsTheOldPredicate(AgentFlag flags, string? id, bool hasRider, bool creature)`
   (`[DataTestMethod]`, the nine rows): first `Assert.AreEqual(creature, OldPredicate(flags, id, hasRider), "the row disagrees with the old predicate")`;
   then build the agent, set `var result = EquipmentIndex.Weapon2;`, call
   `Patch93_CreatureBanditPrimaryWieldGuard.Prefix(agent, ref result)`, and assert it returns `!creature`
   and `result` is `EquipmentIndex.None` for a creature, `EquipmentIndex.Weapon2` (untouched) otherwise.
2. `OffhandWieldGuard_EveryAgentKind_DecidesAsTheOldPredicate`: the same through
   `Patch93_CreatureBanditOffhandWieldGuard.Prefix`.
3. `MissileRangeGuard_EveryAgentKind_DecidesAsTheOldPredicate`: the same through
   `Patch93_CreatureBanditMissileRangeGuard.Prefix` with `var result = 123f;`, expecting `0f` for a creature
   and `123f` otherwise.
4. `Is_NullAgent_IsFalse`: `Assert.IsFalse(CreatureBanditAgents.Is(null));`
5. `Is_RulesOutAHumanoidBeforeReadingTheTroopId` (the RED test): get
   `typeof(CreatureBanditAgents).GetMethod("Is", BindingFlags.NonPublic | BindingFlags.Static)`, list the
   names of `IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray())` in
   order, assert both `get_IsHuman` and `get_StringId` occur, and assert the first `get_IsHuman` comes
   before the first `get_StringId`, message "a humanoid agent must be ruled out by its flags before the
   troop id is read".

For a creature cell the guard also calls `CreatureBanditDiag.NoteGuard`, which only counts through
`Interlocked` and writes one warning through a null-tolerant `Logger?.` (CreatureBanditDiag.cs:252-277);
it needs no setup.

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CreatureBanditWieldGuardTests"`
reports exactly one failure, `Is_RulesOutAHumanoidBeforeReadingTheTroopId`, failing on the
"ruled out by its flags" assertion; every guard cell and `Is_NullAgent_IsFalse` passes (this proves the
rows match the code as it is). Quote the totals line. If any guard cell fails here, the fixture or the
rows are wrong: STOP.

### Step 3: rule out a humanoid before the troop id (GREEN)

In `Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs`, replace the body of `Is` with:

```csharp
    internal static bool Is(Agent? agent)
    {
        var character = agent?.Character;
        if (character == null || agent!.IsHuman)
            return false;
        return CreatureBanditRules.IsCreatureBandit(character.StringId, isHuman: false, agent.RiderAgent != null);
    }
```

It gives the old answer for every input: the old form is
`id != null && !isHuman && !hasRider && IsCreatureTroop(id)`, and `IsCreatureTroop(null)` is false,
so testing `character != null` and `!isHuman` first changes only which members are read. A soldier now
costs `Character` and `IsHuman`; a mount still costs `Character` alone.

Replace the class summary's sentence "only a creature bandit reaches the id lookup" so the summary says:
`Character` first (a managed field, null for every ordinary mount), then `IsHuman` (one flags read that
rules out every soldier and husk rider), and only a non-humanoid agent with a `Character` reads the troop
id and the rider. Keep the IsMount sentence and the thread-safety sentence. No em or en dash.

In `Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs`, lines 101-103, replace "the check is
read-only and exits at the first field read for any agent that is not a creature" with "the check is
read-only; a mount is ruled out by its managed `Character` field and a humanoid by one flags read, before
the troop id is read". Nothing else in that file changes.

**Verify**: the Step 2 filter now passes in full (quote the totals), and
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CreatureBandit"`
has no failures (this runs `CreatureBanditsWiringTests.Fingerprint_DoesNotAskIsMount` and the rules tests).

### Step 4: the formation tests that compile today (RED)

In `TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs` add (usings as needed:
`System`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`):

1. `ComputeUnitPlanePosition_FormationWithoutLayout_ReadsNoFormationState`:
   `var f = MakeFormation(8, 6); f.ClearReceivedCalls();` then
   `Assert.IsNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false));` then
   `_ = f.DidNotReceive().IsHolding; _ = f.DidNotReceive().OrderPositionIsValid; _ = f.DidNotReceive().RepresentativeIsCavalry;`.
   (The cavalry read is the one that evaluates `FormationQuerySystem` on a worker.)
2. `ComputeUnitPlanePosition_FormationWithoutLayout_DoesNotWaitForTheLock`: build a second service whose
   positioner blocks while it holds the lock, then ask about a formation with no layout from another
   thread:
   ```csharp
   var entered = new ManualResetEventSlim();
   var release = new ManualResetEventSlim();
   var positioner = Substitute.For<ILayoutPositioner>();
   positioner.BuildInitialAssignment(Arg.Any<IFormationAdapter>(), Arg.Any<FormationLayoutType>())
       .Returns(ci =>
       {
           entered.Set();
           release.Wait(TimeSpan.FromSeconds(30));
           return new SlotAssignment(ci.ArgAt<FormationLayoutType>(1), 4);
       });
   var sut = new FormationLayoutService(_settings, positioner, _logger);
   var laidOut = MakeFormation(8, 6);
   var plain = MakeFormation(8, 6);
   sut.SetLayout(laidOut, FormationLayoutType.InfantryFrontRangedBack);
   var holder = Task.Run(() => sut.ComputeUnitPlanePosition(laidOut, 0, false));
   try
   {
       Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)), "the holder never took the service lock");
       var plainCall = Task.Run(() => sut.ComputeUnitPlanePosition(plain, 0, false));
       Assert.IsTrue(plainCall.Wait(TimeSpan.FromSeconds(5)), "a formation with no layout waited for the service lock");
       Assert.IsNull(plainCall.Result);
   }
   finally
   {
       release.Set();
       holder.Wait(TimeSpan.FromSeconds(30));
   }
   ```
3. `ComputeUnitPlanePosition_EveryLayout_PlacesEachUnitAtItsAssignedSlot(FormationLayoutType layout)`,
   a `[DataTestMethod]` with one `[DataRow]` per non-Vanilla layout (`InfantryFrontRangedBack`,
   `RangedFrontInfantryBack`, `RangedWingsInfantryCenter`, `Checkerboard`). It pins today's positions as
   the oracle:
   ```csharp
   var f = MakeFormation(8, 6);
   f.UnitDiameter.Returns(0.76f);
   _sut.SetLayout(f, layout);
   var slots = new LayoutPositioner().BuildInitialAssignment(f, layout).ByAgentIndex;
   var pitch = LayoutPositioner.UnitPitch(f);
   foreach (var unit in f.Units)
   {
       var (row, file) = slots[unit.Index];
       var expected = f.OrderPosition + f.Direction.TransformToParentUnitF(new Vec2(file * pitch, -row * pitch));
       var actual = _sut.ComputeUnitPlanePosition(f, unit.Index, unit.IsRanged);
       Assert.IsNotNull(actual, $"{layout}: unit {unit.Index} got no position");
       Assert.AreEqual(expected.x, actual.Value.x, 1e-4f, $"{layout}: unit {unit.Index} x");
       Assert.AreEqual(expected.y, actual.Value.y, 1e-4f, $"{layout}: unit {unit.Index} y");
   }
   ```

Create `TAOM.Tests/Features/MixedFormations/Patch30ThreadSafetyIlTests.cs`, namespace
`TAOM.Tests.Features.MixedFormations`, `[TestClass]` with NO category (it only reads IL and resolves
member tokens, like `BehaviorTreeAgentComponentThreadingTests`). Helper:
`private static MethodBase[] CallsIn(MethodBase m) => IlCallScanner.ExtractCalledMethods(m, m.GetMethodBody().GetILAsByteArray()).ToArray();`
and `Prefix = typeof(Patch30_FormationGetOrderPositionOfUnit).GetMethod(nameof(Patch30_FormationGetOrderPositionOfUnit.Prefix))`.
Add now:

4. `Prefix_NeverConstructsAFormationAdapter`: assert no call target is a `ConstructorInfo` whose
   `DeclaringType == typeof(FormationAdapter)`, message "the worker-thread prefix must reuse the adapter
   the service holds, never allocate one per call".
5. `RepresentativeIsCavalry_StillReadsTheEvaluatingQuery`: the getter of
   `FormationAdapter.RepresentativeIsCavalry` calls `FormationQuerySystem.get_IsCavalryFormation`
   (`m.DeclaringType == typeof(FormationQuerySystem) && m.Name == "get_IsCavalryFormation"`). This pins
   that SmartCavalryAI's main-thread member keeps its meaning.

**Verify**: build the test project, then
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FormationLayoutServiceTests|FullyQualifiedName~Patch30ThreadSafetyIlTests"`.
Expected: exactly three failures, `ComputeUnitPlanePosition_FormationWithoutLayout_ReadsNoFormationState`
(NSubstitute reports a received call to `IsHolding`), `ComputeUnitPlanePosition_FormationWithoutLayout_DoesNotWaitForTheLock`
(the "waited for the service lock" assertion) and `Prefix_NeverConstructsAFormationAdapter`. The four
`EveryLayout` rows, `RepresentativeIsCavalry_StillReadsTheEvaluatingQuery` and every pre-existing test
pass. Quote the totals. An `EveryLayout` row failing here means the oracle is wrong: STOP.

### Step 5: the formation tests that need the new members (RED: compile)

Add to `FormationLayoutServiceTests.cs`:

6. `ComputeUnitPlanePosition_ReadsTheCachedCavalryFlag_NeverTheEvaluatingOne`: `MakeFormation(8, 6)`,
   `SetLayout(..., InfantryFrontRangedBack)`, `f.ClearReceivedCalls()`, compute once, then
   `_ = f.Received().RepresentativeIsCavalryReadOnly; _ = f.DidNotReceive().RepresentativeIsCavalry;`.
7. `FindLaidOutFormation_NoLayout_ReturnsNull`: `Assert.IsNull(_sut.FindLaidOutFormation(MakeFormation(8, 6).FormationKey));`
8. `FindLaidOutFormation_AfterApplyDefaults_ReturnsTheSameAdapter`: `ApplyDefaultsToFormations(new List<IFormationAdapter> { f })`
   returns 1, and `Assert.AreSame(f, _sut.FindLaidOutFormation(f.FormationKey))`.
9. `FindLaidOutFormation_SetLayoutVanilla_DropsTheFormation`: `SetLayout(f, Checkerboard)` makes it
   non-null; `SetLayout(f, Vanilla)` makes it null.
10. `FindLaidOutFormation_AfterCycleLayouts_StillReturnsTheFormation`: `SetLayout(f, InfantryFrontRangedBack)`,
    `CycleLayouts(new List<IFormationAdapter> { f })`, still `AreSame(f, ...)`.
11. `FindLaidOutFormation_SetVanillaThenCycle_ReturnsTheFormation` (the base gives this formation a
    position, see "`CycleLayouts`" in Current state; the snapshot must too):
    ```csharp
    var f = MakeFormation(8, 6);
    _sut.SetLayout(f, FormationLayoutType.Vanilla);
    Assert.IsNull(_sut.FindLaidOutFormation(f.FormationKey));
    _sut.CycleLayouts(new List<IFormationAdapter> { f });
    Assert.AreEqual(FormationLayoutType.InfantryFrontRangedBack, _sut.GetLayout(f));
    Assert.AreSame(f, _sut.FindLaidOutFormation(f.FormationKey), "a cycle from Vanilla must publish the formation");
    Assert.IsNotNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false),
        "a cycle from Vanilla gives a position at the base, and must still");
    ```
12. `FindLaidOutFormation_AfterOnMissionEnd_ReturnsNull`:
    ```csharp
    var f = MakeFormation(8, 6);
    _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
    Assert.IsNotNull(_sut.FindLaidOutFormation(f.FormationKey));
    _sut.OnMissionEnd();
    Assert.IsNull(_sut.FindLaidOutFormation(f.FormationKey));
    ```
13. `LaidOutSnapshot_TakenBeforeAWrite_IsUnchangedByIt`:
    ```csharp
    var f = MakeFormation(8, 6);
    var before = _sut.LaidOutSnapshot;
    Assert.AreEqual(1, _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { f }));
    Assert.AreEqual(0, before.Count, "a published snapshot was changed in place");
    Assert.IsTrue(_sut.LaidOutSnapshot.ContainsKey(f.FormationKey));
    ```
14. `LaidOutSnapshot_ConcurrentReaders_SeeBothOrNeitherFormationOfOneApplyPass`:
    ```csharp
    var a = MakeFormation(8, 6);
    var b = MakeFormation(8, 6);
    var keyA = a.FormationKey;   // read the keys once: NSubstitute records every call
    var keyB = b.FormationKey;
    var stop = 0;
    var torn = 0;
    var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
    {
        while (Volatile.Read(ref stop) == 0)
        {
            var snapshot = _sut.LaidOutSnapshot;
            if (snapshot.ContainsKey(keyA) != snapshot.ContainsKey(keyB)) Interlocked.Increment(ref torn);
        }
    })).ToArray();
    for (var i = 0; i < 2000; i++)
    {
        _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { a, b });
        _sut.OnMissionEnd();
    }
    Volatile.Write(ref stop, 1);
    Assert.IsTrue(Task.WaitAll(readers, TimeSpan.FromSeconds(30)), "a reader never finished");
    Assert.AreEqual(0, torn, "a reader saw one formation of a single apply pass without the other");
    ```

Change the setup of the two cavalry-handshake tests, adding one line each and nothing else:
- `ComputeUnitPlanePosition_CavalryFormation_ReturnsNull_HonoringSmartCavalryHandshake`: after
  `cavalry.RepresentativeIsCavalry.Returns(true);` add `cavalry.RepresentativeIsCavalryReadOnly.Returns(true);`
  and update its first comment line to name `formation.RepresentativeIsCavalryReadOnly` (the service's
  worker-thread read) instead of quoting `FormationLayoutService.cs:74`.
- `CavalryHandshake_NonCavalry_DoesNotShortCircuit_BaselineAssertion`: after
  `infantry.RepresentativeIsCavalry.Returns(false);` add `infantry.RepresentativeIsCavalryReadOnly.Returns(false);`.

Add to `Patch30ThreadSafetyIlTests.cs`:

15. `Prefix_AsksTheServiceForTheLaidOutFormation`: some call target has
    `DeclaringType == typeof(IFormationLayoutService)` and `Name == nameof(IFormationLayoutService.FindLaidOutFormation)`.
16. `RepresentativeIsCavalryReadOnly_ReadsTheCachedQueryValue`: the getter of
    `FormationAdapter.RepresentativeIsCavalryReadOnly` calls `FormationQuerySystem.get_IsCavalryFormationReadOnly`
    and never `FormationQuerySystem.get_IsCavalryFormation`.

**Verify**: `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails, and the errors name
only the missing members `FindLaidOutFormation`, `LaidOutSnapshot` and `RepresentativeIsCavalryReadOnly`
(CS1061 or CS0117 class diagnostics). Any other error is yours to fix before going on.

### Step 6: the adapter member

`Main/Adapters/IFormationAdapter.cs`: after `RepresentativeIsCavalry` add

```csharp
    /// <summary><see cref="RepresentativeIsCavalry"/> as the main thread last evaluated it
    /// (<c>FormationQuerySystem.IsCavalryFormationReadOnly</c>): it never re-evaluates the formation's class
    /// counts, so the engine's worker threads may read it. Refreshed whenever a unit joins or leaves the
    /// formation and on any main-thread read of the evaluating member after its 5 s lifetime.</summary>
    bool RepresentativeIsCavalryReadOnly { get; }
```

`Main/Adapters/FormationAdapter.cs`: after `RepresentativeIsCavalry` add

```csharp
    public bool RepresentativeIsCavalryReadOnly =>
        _formation?.QuerySystem != null && _formation.QuerySystem.IsCavalryFormationReadOnly;
```

Do not change `RepresentativeIsCavalry`.

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.

### Step 7: the snapshot in the service (GREEN for the service tests)

`Main/Features/MixedFormations/IFormationLayoutService.cs`: add

```csharp
    /// <summary>
    /// The adapter of a formation that holds a non-Vanilla layout, or <c>null</c>. Answered from an immutable
    /// snapshot with no lock, no allocation and no formation read, because Patch30 asks it for every unit
    /// of every formation on the engine's worker threads. <paramref name="formationKey"/> is the
    /// <see cref="IFormationAdapter.FormationKey"/> identity (the engine formation itself).
    /// </summary>
    IFormationAdapter? FindLaidOutFormation(object formationKey);
```

`Main/Features/MixedFormations/FormationLayoutService.cs`:

1. Below `_assignmentCache` add the snapshot and its two readers:
   ```csharp
    // The formations that hold a non-Vanilla layout, each with the adapter the main thread passed when it
    // assigned the layout. Never changed once published: every write builds a new dictionary under _lock
    // and swaps the reference, so a worker-thread reader needs no lock and sees the whole old set or the
    // whole new one.
    private volatile Dictionary<object, IFormationAdapter> _laidOut = new();

    public IFormationAdapter? FindLaidOutFormation(object formationKey) =>
        formationKey != null && _laidOut.TryGetValue(formationKey, out var formation) ? formation : null;

    /// <summary>The published snapshot, for the swap tests.</summary>
    internal IReadOnlyDictionary<object, IFormationAdapter> LaidOutSnapshot => _laidOut;
   ```
2. Rewrite the lock comment (lines 22-30) to end: all dictionary and `SlotAssignment.ByAgentIndex`
   mutations hold `_lock`, and so does the slot lookup for a laid-out formation; a formation with no
   layout is answered from `_laidOut` without it. Keep the Codex review #35 reference; drop the "~25ns"
   sentence. No em or en dash.
3. `SetLayout`: inside the lock, after the two existing statements, publish:
   ```csharp
            var next = new Dictionary<object, IFormationAdapter>(_laidOut);
            if (layout == FormationLayoutType.Vanilla) next.Remove(formation.FormationKey);
            else next[formation.FormationKey] = formation;
            _laidOut = next;
   ```
4. `ComputeUnitPlanePosition`: directly after `if (formation == null) return null;` insert
   ```csharp
        // Lock-free: almost no formation has a layout, and this is asked for each of their units on the
        // engine's worker threads.
        if (!_laidOut.ContainsKey(formation.FormationKey)) return null;
   ```
   and change `if (formation.RepresentativeIsCavalry) return null;` to
   `if (formation.RepresentativeIsCavalryReadOnly) return null;`, adding one comment line that it is the
   main thread's cached value because this runs on the workers. Leave the lock block and the math as they are.
5. `CycleLayouts`: a formation whose stored layout was `Vanilla` gains a layout here (`NextLayout` maps
   `Vanilla` to `InfantryFrontRangedBack`), so publish it, once per pass like Step 7.6. Inside the lock,
   declare the copy before the loop, add it after `_assignmentCache.Remove(...)`, and swap after the loop:
   ```csharp
        lock (_lock)
        {
            Dictionary<object, IFormationAdapter>? laidOut = null;
            foreach (var formation in formations)
            {
                if (formation == null || formation.CountOfUnits == 0) continue;
                if (!_layoutByFormation.TryGetValue(formation.FormationKey, out var current)) continue;

                var next = NextLayout(current);
                _layoutByFormation[formation.FormationKey] = next;
                _assignmentCache.Remove(formation.FormationKey);
                // NextLayout never yields Vanilla: a formation stored as Vanilla (SetLayout) gains a
                // layout here, so the snapshot gains it; every other formation is already in it.
                if (current == FormationLayoutType.Vanilla)
                    (laidOut ??= new Dictionary<object, IFormationAdapter>(_laidOut))[formation.FormationKey] = formation;
                lastLayout = next;
                affected++;
            }
            if (laidOut != null) _laidOut = laidOut;
        }
   ```
   The name `next` is already the layout here, hence `laidOut` for the copy. Nothing else in the method
   changes.
6. `ApplyDefaultsToFormations`: inside the lock, publish once per pass, never per formation:
   ```csharp
        lock (_lock)
        {
            Dictionary<object, IFormationAdapter>? next = null;
            foreach (var formation in formations)
            {
                if (formation == null || formation.CountOfUnits < 2) continue;
                if (_layoutByFormation.ContainsKey(formation.FormationKey)) continue;
                if (!IsMixedFormationInternal(formation)) continue;

                _layoutByFormation[formation.FormationKey] = defaultLayout;
                (next ??= new Dictionary<object, IFormationAdapter>(_laidOut))[formation.FormationKey] = formation;
                assigned++;
            }
            if (next != null) _laidOut = next;
        }
   ```
7. `OnMissionEnd`: inside the lock, after the two `Clear()` calls, add
   `_laidOut = new Dictionary<object, IFormationAdapter>();`.

`IsMixedFormationInternal` keeps reading `RepresentativeIsCavalry` (main thread, before assignment).

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FormationLayoutServiceTests"`
passes in full: the two Step 4 lock and read tests, the four `EveryLayout` rows, tests 6 to 14 and every
pre-existing test (the cavalry handshake ones included). Quote the totals. Run test 14 and test 2 two
more times with `--filter "FullyQualifiedName~ConcurrentReaders|FullyQualifiedName~DoesNotWaitForTheLock"`;
all runs must pass. A run that fails intermittently is a STOP, never a reason to raise a timeout.

### Step 8: the patch (GREEN for the IL tests)

In `Patch30_FormationGetOrderPositionOfUnit.cs`:

1. Replace the comment on lines 13-16 with (no em or en dash):
   ```csharp
    // Cached service reference. The prefix runs about twice a second for every AI unit in a formation
    // (Agent.TickParallel's 0.45 to 0.55 s formation timer, on the TWParallel worker threads), plus one call
    // per unit whenever the engine forces a formation's values and per unit of the player's order preview
    // on the main thread. Caching the singleton skips the container lookup
    // (.claude/rules/harmony-patches.md hot-path caching pattern).
   ```
2. Replace the last sentence of the comment on lines 24-27 ("Placed first to short-circuit this per-unit
   hot path (~40,000×/frame) before any IoC resolve or adapter allocation.") with "Placed first so a
   siege, hideout or naval mission returns before the service is resolved."
3. Replace `var formation = new FormationAdapter(__instance);` (line 41) with, directly after
   `if (service == null) return true;`:
   ```csharp
            // Worker-thread fast path: a formation with no layout (every AI formation, and every player
            // formation that is not mixed) goes straight back to vanilla. The service answers from an
            // immutable snapshot, with no lock, no allocation and no FormationQuerySystem read, and hands
            // back the adapter the main thread built when it assigned the layout.
            var formation = service.FindLaidOutFormation(__instance);
            if (formation == null) return true;
   ```
   Everything after it (`agentIsRanged`, `agentIndex`, `ComputeUnitPlanePosition(formation, ...)`, the
   ground height and availability checks, the catch) stays as it is. Then delete the line
   `using TAOM.Adapters;` (line 5): `FormationAdapter` was the file's only type from that namespace, and
   `var formation` names none. The build will not warn about an unused using here, so do not wait for one.

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with no new
warning in the touched files, the file stays under 150 lines (`wc -l` on it), and
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Patch30ThreadSafetyIlTests"`
passes all four tests.

### Step 9: the IL tests on reference assemblies

The untagged `Patch30ThreadSafetyIlTests` will run on the hosted CI against metadata-only reference
assemblies. Prove it here (`.ai/verification.md`). In one shell, with `BANNERLORD_GAME_DIR` and
`BANNERLORD_OVERRIDE_DIR` unset before the build:

```
env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR TEMP="<tmp>" TMP="<tmp>" dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=
env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification&FullyQualifiedName~Patch30ThreadSafetyIlTests"
```

Then rebuild normally so the next run does not use reference-assembly binaries:
`TEMP="<tmp>" TMP="<tmp>" dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental`.

**Verify**: the RefAsm filtered run executes 4 tests and passes them. If it fails with one of the
exceptions `.claude/rules/tests.md` lists for engine code on stubs (a `NullReferenceException` from a
`TaleWorlds` frame, `TypeInitializationException`, `FileNotFoundException`), tag the class
`[TestCategory("RequiresGame")]` instead (never catch the exception) and say so in your report. If the
RefAsm build itself fails for an environment reason (no network for the BUTR packages), report it as
not run, with the error; do not change build files.

### Step 10: docs, rules and the stale call-rate claims

Write every sentence below as a draft you re-check against the code you just changed. No em or en dash
in any new prose.

1. `docs/reference/harmony-patch-registry.md`, section `## Patch30_MixedFormations`:
   - In the **Thread** paragraph (lines 214-218), replace "`FormationLayoutService._lock` is load-bearing;"
     with "`FormationLayoutService._lock` guards every write and the slot cache, and a formation with no
     layout is answered without it from the immutable snapshot the writers publish under it
     (`FindLaidOutFormation`);".
   - In line 222, replace the sentence that starts "HOT PATH" and ends "per the harmony-patches hot-path
     rule." with: "HOT PATH: it runs about twice a second for every AI unit in a formation (the 0.45 to
     0.55 s timer in `Agent.TickParallel`, on the TWParallel workers), plus one call per unit when the
     engine forces a formation's values (`Agent.ForceUpdateCachedAndFormationValues`) and per unit of the
     player's order preview (`OrderController.SimulateDestinationFrames`, main thread). The service
     singleton is cached in a static field; a formation with no layout returns to vanilla after one
     lock-free snapshot lookup, and a laid-out formation reuses the adapter the main thread built and
     reads `FormationQuerySystem.IsCavalryFormationReadOnly`, never the re-evaluating `IsCavalryFormation`."
2. `docs/features/mixed-formations.md`:
   - Line 30 (the `FormationLayoutService` bullet): append "It also publishes the laid-out formations as
     an immutable snapshot (`FindLaidOutFormation`), rebuilt under its lock whenever a layout is assigned
     or cleared and swapped by reference, so the worker-thread prefix answers a formation with no layout
     without the lock."
   - Line 32 (the `Patch30` bullet): change "intercepts vanilla; calls the service;" to "intercepts
     vanilla; returns `true` at once for a formation the service's snapshot does not list; otherwise calls
     the service;".
   - Line 34: replace "before the ~40,000×/frame hot path resolves the service or allocates an adapter"
     with "before the per-unit worker-thread path resolves the service".
   - After line 134 (the `FormationLayoutServiceTests.cs` bullet), add a bullet in the same link style for
     `TAOM.Tests/Features/MixedFormations/Patch30ThreadSafetyIlTests.cs`: "IL pins: the prefix never
     builds a `FormationAdapter` and asks `FindLaidOutFormation`; `RepresentativeIsCavalryReadOnly` reads
     the cached query value".
   - Line 148: after "+ `Dictionary<object, SlotAssignment>` (~4 entries)" insert ", plus the published
     laid-out snapshot (`Dictionary<object, IFormationAdapter>`, rebuilt on each layout write)". The
     line's "Two dictionaries" stays (they are the two that are mutated).
   - Line 150: replace "Per-position-query work: lock acquire + dictionary lookup + 1 conditional
     `BuildInitialAssignment` if the cache miss, lock release, then `Vec2` math (lock-free)." with
     "Per-position-query work: a formation with no layout costs one lock-free snapshot lookup and nothing
     else; a laid-out formation takes the lock for the dictionary lookup and 1 conditional
     `BuildInitialAssignment` on a cache miss, then does the `Vec2` math (lock-free)."
   - Line 152: replace "Reads on the hot path lock briefly (~25ns uncontended); pure math runs outside the
     critical section." with "The slot lookup for a laid-out formation locks briefly; a formation with no
     layout is answered with no lock from the immutable snapshot the writers publish under it; pure math
     runs outside the critical section."
3. `docs/reviews/lessons/harmony-il.md`, line 125: replace "to keep the ~40,000x/frame path cheap" with
   "to keep the per-unit worker-thread path cheap".
4. `.claude/rules/harmony-patches.md`, lines 148-149: change "Its shared state takes a lock
   (`FormationLayoutService`, `CavalryChargeService`, `TroopStanceManager` are the shape), it" to "Its shared
   state takes a lock (`CavalryChargeService`, `TroopStanceManager` are the shape) or is an immutable
   snapshot written under that lock and swapped by reference, so readers need none
   (`FormationLayoutService.FindLaidOutFormation`), it". Keep the rest of the sentence.
5. `.claude/rules/csharp-architecture.md`, lines 270-271: change "A store reachable from a patch on any of
   those engine methods takes a lock." to "A store reachable from a patch on any of those engine methods
   takes a lock, or is an immutable snapshot published under one and swapped by reference."

Then the stale-claim sweep, over the whole repo, tests included:

```
git grep -n -I -E '40,000(×|x)|per-unit-per-formation-position-recalculation' -- . ':!docs/changelog-archive' ':!docs/reviews/rca-*' ':!docs/reviews/codex-prompt-*' ':!plans'
```

At the planned-at commit this prints exactly six lines, the ones this plan rewrites:
`Patch30_FormationGetOrderPositionOfUnit.cs:13`, `:14`, `:27`, `docs/features/mixed-formations.md:34`,
`docs/reference/harmony-patch-registry.md:222` and `docs/reviews/lessons/harmony-il.md:125`.

**Verify**: the sweep now prints nothing (the excluded paths are dated historical records: the archived
changelog, an RCA and a Codex prompt). `python tools/lint_docs.py --fail-on-drift` exits with the same
code as in Step 1. `git grep -n -F "QuerySystem.IsCavalryFormationReadOnly;" -- Main` prints exactly
one line, in `Main/Adapters/FormationAdapter.cs` (the code form, ending in `;`: the Step 6 doc comment in
`IFormationAdapter.cs` names the same member and is expected; it does not match this pattern).
`git grep -n "~25ns" -- docs/features/mixed-formations.md Main/Features/MixedFormations` prints nothing.

### Step 11: full suite and the two commits

Run the full suite and the data validator:

```
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
python tools/validate_moduledata.py
```

**Verify**: the only failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`; Passed equals the Step 1
Passed plus the new test results, counted as: every result of the `CreatureBanditWieldGuardTests` run in
Step 3, plus the `FormationLayoutServiceTests` total in Step 7 minus that class's total recorded in
Step 1, plus the four `Patch30ThreadSafetyIlTests` results in Step 8. State the arithmetic. The validator
reports 0 ERRORs. `git status --porcelain`, compared with the Step 1 snapshot, adds or changes only
in-scope files (lines already in the snapshot are not yours; leave them alone).

Commit 1 (stage exactly `Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs`,
`Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs`,
`TAOM.Tests/Features/CreatureBandits/CreatureBanditWieldGuardTests.cs`). Subject (69 characters at
v2.0.32): `perf(creature-bandits): v2.0.32 - rule out humans before the troop id`. Draft body, to re-check
against the code:

```
Every agent in a battle passes through the creature-bandit guards on
GetPrimaryWieldedItemIndex, GetOffhandWieldedItemIndex and
GetMissileRange, on the engine's worker threads as well as the main
thread. The check now rules a humanoid out by its flags before it
reads the troop id or the rider, so a soldier costs two reads instead
of four. The answer is unchanged for every kind of agent: soldiers,
husk riders, half-built humanoids, mounts with and without riders, and
creature bandits with route A applied or skipped.

Not-tested: the live Harmony invocation; the guards are driven
directly on bare agents.
```

Commit 2 (stage the remaining in-scope files explicitly, each by path). Subject (69 characters at
v2.0.32): `perf(mixed-formations): v2.0.32 - lock-free path for plain formations`. Draft body:

```
Mixed Formations patches Formation.GetOrderPositionOfUnit, which the
engine calls about twice a second for every AI unit in a formation, on
its worker threads. Each call in a field battle allocated an adapter,
and for a formation holding position it also read the cavalry query
(re-evaluating the formation's class counts on the worker once that
value was five seconds old) and took the service's global lock, all
for formations that almost never have a layout.

A formation with no layout now goes back to vanilla after one lookup
in an immutable snapshot of the laid-out formations: no lock, no
allocation and no formation query. The snapshot is rebuilt under the
lock whenever a layout is assigned or cleared and swapped whole. A
laid-out formation reuses the adapter the main thread built and reads
the cavalry flag the main thread last evaluated. Positions for every
layout are unchanged. The comments and docs that claimed 40,000 calls
a frame are corrected.

Constraint: the cavalry handshake now sees the cavalry flag as of its
last main-thread evaluation (a unit joining or leaving, or a
main-thread read after its 5 s lifetime) instead of re-evaluating it
on a worker.
Not-tested: the prefix live on the engine's worker threads.
Research: v1.5.3 Agent.TickParallel, FormationQuerySystem, QueryData.
```

**Verify**: `git log --oneline -2` shows both subjects; `git status --porcelain` is empty for in-scope
paths.

## Test plan

- `TAOM.Tests/Features/CreatureBandits/CreatureBanditWieldGuardTests.cs` (new, `RequiresGame`): nine agent
  kinds times three guards (27 cells), each checked against the old predicate's formula and against the
  guard's real `Prefix`; `Is(null)`; and the IL order pin, the RED for Step 3.
- `TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs` (existing, `RequiresGame`): no
  formation state read and no lock wait for a formation without a layout (RED on the base); the four
  layouts' positions pinned against `LayoutPositioner` plus today's transform (green on the base, the
  equivalence oracle); the cached cavalry flag read; `FindLaidOutFormation` across apply, set, cycle (from a stored
  `Vanilla` too, which the base lays out) and mission end; the snapshot is never changed in place; concurrent readers never see half of one apply
  pass. Pattern: the existing tests in the same file (`MakeFormation`, NSubstitute).
- `TAOM.Tests/Features/MixedFormations/Patch30ThreadSafetyIlTests.cs` (new, untagged): the prefix never
  builds a `FormationAdapter` (RED on the base) and asks `FindLaidOutFormation`; the new adapter member
  reads `IsCavalryFormationReadOnly`; the old member still reads `IsCavalryFormation`. Pattern:
  `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs`.
- Not testable here, for the `Not-tested:` trailers: the live Harmony prefixes on the engine's worker
  threads (`Patch30.Prefix` needs `Mission.Current` and a `Scene`; the IL tests pin its shape), and the
  engine's own refresh cadence of `IsCavalryFormationReadOnly`.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: the only failure is
      `EveryLanguage_DeclaresARowForEveryEnglishKey`, and Passed is the Step 1 count plus the new results
- [ ] The Step 9 RefAsm run of `Patch30ThreadSafetyIlTests` passed (or the class is tagged `RequiresGame`
      with the reason reported)
- [ ] `git grep -n "new FormationAdapter" -- Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs`
      prints nothing (`MixedFormationsMissionBehavior.cs:88, 96` keep theirs: main thread, once a second)
- [ ] `git grep -n -F "QuerySystem.IsCavalryFormationReadOnly;" -- Main` prints exactly one line, in
      `Main/Adapters/FormationAdapter.cs`
- [ ] `FindLaidOutFormation_SetVanillaThenCycle_ReturnsTheFormation` passes (the snapshot follows every
      writer, `CycleLayouts` from `Vanilla` included)
- [ ] The Step 10 stale-claim sweep prints nothing
- [ ] `python tools/lint_docs.py --fail-on-drift` exits as it did in Step 1
- [ ] `wc -l Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs` is under 150
- [ ] `git status --porcelain` shows nothing outside the in-scope files beyond the Step 1 snapshot, and
      both commits exist
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts.
- Step 1's engine check fails: `IsCavalryFormationReadOnly` is missing, is not
  `_isCavalryFormation.GetCachedValueUnlessTooOld()`, or `GetCachedValueUnlessTooOld` does more than return
  the cached value on the installed engine.
- A writer of the layout set exists that this plan does not list: `git grep -n "_layoutByFormation" -- Main`
  shows a write outside `SetLayout`, `CycleLayouts`, `ApplyDefaultsToFormations` and `OnMissionEnd`, or a
  new production caller writes layouts some other way. A snapshot that misses a writer is a correctness
  bug.
- Any `EveryLayout` position row, or any wield-guard cell, fails on the base (the oracle is wrong) or
  after your change (behaviour changed).
- A thread test (`DoesNotWaitForTheLock`, `ConcurrentReaders`) passes and fails across repeated runs.
  Report it; never raise its timeout or loosen it.
- `Agent._character`, `_flagsPointer` or `_cachedRiderAgent` is missing (the fixture's assertion fires).
- The work seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, PatchShield, the settings
  provider or `MixedFormationsMissionBehavior.cs`.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (perf: Patch30 worker-thread fast path and the Patch93 wield-guard
  predicate).
- `/deep-review` on both commits before merge (C# change on a worker-thread patch). Ask the data-flow lens
  to check that every write to `_layoutByFormation` that moves a formation into or out of a non-Vanilla
  layout publishes `_laidOut` in the same lock (`CycleLayouts` from a stored `Vanilla` included), and
  that no reader path mutates a published dictionary.
- No `/localize`: no player-facing text changes. `docs/features/mixed-formations.md` is updated by the
  executor; its feature-map row does not change.

## After merge: the maintainer's actions

- An in-game field battle (FOR-MIKE): a mixed player infantry and archer formation of at least 10 units
  ordered to Hold still forms its layout, the `L` cycle hotkey still rotates it, and AI formations
  behave as before. A spider brood battle still runs (the creature guards answer as before).
- FOR-MIKE decision, not done here: the two native queries per laid-out unit
  (`Scene.GetGroundHeightAtPosition` and `Mission.IsFormationUnitPositionAvailable`) remain; caching
  them could return a stale slot, which would be a behaviour change.

## Maintenance notes

- Any new way to assign or clear a layout must publish `_laidOut` under `_lock`, in the same critical
  section as the `_layoutByFormation` write; the swap tests cover the existing writers only. A change to
  `NextLayout` that could return `Vanilla` must remove the formation from `_laidOut` in `CycleLayouts`.
- The adapter in the snapshot is the instance `TryGetTeamAdapters` built on the main thread. It reads the
  engine formation live, so it never goes stale, but anything that adds per-instance state to
  `FormationAdapter` (as the composition TTL fields do for CompanionTactics) must not be used from
  `ComputeUnitPlanePosition`.
- `RepresentativeIsCavalryReadOnly` can lag `RepresentativeIsCavalry` by the time between the 5 s expiry
  and the next main-thread refresh (a unit joining or leaving, or a main-thread read; SmartCavalryAI
  does one every frame for the player's formations when it is on). A laid-out formation that turns
  cavalry-majority by mounting horses without any unit joining keeps its layout until that refresh.
  Review should confirm this matches the maintainer's acceptance.
- Deferred: the LINQ in `LayoutPositioner.BuildInitialAssignment` and `FormationAdapter.Units` runs inside
  the lock on a slot-cache miss (a layout assigned or cycled), which is rare and main-thread driven.
- Review probes: `FormationLayoutService.ApplyDefaultsToFormations` (one publish per pass),
  `SetLayout` (Vanilla removes), `CycleLayouts` (a stored Vanilla is published), `ComputeUnitPlanePosition` (snapshot check before any adapter read), the
  `Is` reorder in `CreatureBanditAgents.cs`.
