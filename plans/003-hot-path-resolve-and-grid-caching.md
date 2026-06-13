# Plan 003: Eliminate per-call IoC/MCM resolves and stale-grid on combat hot paths

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**:
> `git diff --stat 141b749..HEAD -- Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs Main/Features/TroopWeight/Hooks/ Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs Main/Features/BattleBalance/`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S (each of the 4 sub-fixes is small; the plan bundles 4 mechanical caching fixes of the same class)
- **Risk**: LOW
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `141b749`, 2026-06-13
- **Issue**: create before implementation lands — orchestrator (TAOM issue-first mandate)

## Why this matters

TAOM's documented hot-path rule (CLAUDE.md "Verify Before Reference": *"Before `IoC.Resolve` in hot path, use lazy cache"*; `.claude/rules/harmony-patches.md`: *"Reflection in hot paths ... MUST be cached"*) is violated in four combat/UI hot paths. Three of them re-resolve a settings singleton or IoC container thousands of times per second; one rebuilds the combat SpatialGrid only every 2 seconds so warg/spider/elephant behavior-tree target queries read agent positions up to ~20m stale (the exact dysfunction issue #219 was supposed to have fixed — its 0.1s fix shipped into a branch that can never run). Each fix is a behavior-preserving caching/cadence change with no save-format impact: the cached reference still reads live MCM values, so in-game tuning keeps working. After this lands, the per-frame combat loop and engine-wide party-count reads stop paying redundant `ConcurrentDictionary` walks and container resolves, and creature BT targeting sees fresh positions.

## Current state

This plan bundles four findings (PERF-01, PERF-03, PERF-04, PERF-07). All cited line numbers below were re-read from the live files at commit `141b749` and corrected where the harvest's leads were off.

### The house caching exemplar (read this first — it is the pattern to copy)

`Main/Features/SettlementNameplateFade/NameplateFadeSettingsProvider.cs` — Patch38's settings provider. It caches `TaomSettings.Instance` once at construction and reads values *through* the cached reference so live MCM edits still apply:

```csharp
// NameplateFadeSettingsProvider.cs:16-27
public class NameplateFadeSettingsProvider : INameplateFadeSettingsProvider
{
    private readonly TaomSettings _settings;

    public NameplateFadeSettingsProvider()
    {
        _settings = TaomSettings.Instance;
    }

    public bool Enabled => _settings?.EnableNameplateFade ?? true;
    public float NearDistance => _settings?.NameplateFadeNearDistance ?? 80f;
    public float FarDistance => _settings?.NameplateFadeFarDistance ?? 200f;
}
```

Its class-doc states the rule verbatim: *"The `TaomSettings.Instance` singleton accessor is cached in `_settings` at construction time ... The cached reference still picks up live MCM edits because the values are read through the reference, not snapshotted."* `TaomSettings` is `AttributeGlobalSettings<TaomSettings>` (`Main/Features/TaomSettings.cs:10`); `EnableTroopWeight` defaults `true` (`TaomSettings.cs:29`). **`TaomSettings.Instance` is `null` in the test harness (MCM v5 not loaded)** — every fix must fall back to the compiled default when `Instance`/cache is null.

### The lazy-cache exemplar for BT leaves (read this for PERF-07)

`Main/Features/Spider/BehaviorTreeElements/SpiderAttackTask.cs:16-31` — Spider/Elephant BT nodes already follow the `??=` lazy-cache pattern that the Warg nodes do not:

```csharp
private IMissionAdapterFactory _adapterFactory;
private ISpiderAttackService _attackService;
// ...
public override BTTaskStatus Execute()
{
    Agent spider = Agent.GetValue();
    if (spider == null || !spider.IsActive()) return BTTaskStatus.FinishedWithFalse;

    _adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();
    _attackService ??= IoC.Resolve<ISpiderAttackService>();
    // ...
}
```

---

### PERF-01 — SpatialGrid rebuilt every 2s; the issue #219 0.1s fix is in a dead branch

`Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` — **this is the instance that actually owns the grid rebuild in every mission** (it is `AddMissionBehavior`'d first; see SubModule excerpt below):

```csharp
// AdvancedCombatBehavior.cs:6-35
public class AdvancedCombatBehavior : MissionLogic
{
    private readonly IBoneCollisionService _boneCollisionService;
    private readonly ISpatialGridDebugService _debugService;
    private const float GridUpdateInterval = 2f;
    private float _timeSinceLastUpdate = 0f;
    // ...
    public override void OnMissionTick(float dt)
    {
        // Bone checks must tick every frame to catch short animation windows (0.5-0.7s)
        _boneCollisionService.TickBoneChecks(dt);

        // Grid rebuild and debug rendering are throttled to reduce overhead
        _timeSinceLastUpdate += dt;
        if (_timeSinceLastUpdate < GridUpdateInterval) return;
        _timeSinceLastUpdate = 0f;

        if (Mission.Current != null)
        {
            SpatialGrid.Instance.UpdateGrid(Mission.Current.AllAgents);
            _debugService.RenderDebugVisualization();
        }
    }
    // ...
}
```

**Why the #219 fix is dead:** `Main/Features/Warg/WargMissionBehavior.cs:36` declares `private const float GridUpdateInterval = 0.1f;` with the #219 comment (lines 27-35), but the 0.1f rebuild loop runs only when `_managesCombatInfrastructure` is true (lines 85-97), and that flag is set true only when `Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>() == null` (`WargMissionBehavior.cs:58-62`). `Main/SubModule.cs:663-668` adds `AdvancedCombatBehavior` **before** `WargMissionBehavior` in every mission:

```csharp
// SubModule.cs:663-668
mission.AddMissionBehavior(new AdvancedCombatBehavior());
mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
mission.AddMissionBehavior(new AutonomousMovementPlayerController());
mission.AddMissionBehavior(new WargMissionBehavior());
mission.AddMissionBehavior(new SpiderMissionBehavior());
mission.AddMissionBehavior(new Features.Elephant.ElephantMissionBehavior());
```

So `GetMissionBehavior<AdvancedCombatBehavior>()` is never null when the warg initializes → `_managesCombatInfrastructure` is always false → the warg's 0.1f branch never executes, and the 2f branch in `AdvancedCombatBehavior` always owns the rebuild. `Main/Features/Spider/SpiderMissionBehavior.cs:19-20` confirms ownership: *"SpatialGrid/bone-collision ticking is owned by `AdvancedCombatBehavior` (always co-registered in SubModule and ticking every frame) — the old conditional fallback here was dead code."*

**The in-scope fix is the cadence constant only.** Set `AdvancedCombatBehavior.GridUpdateInterval` from `2f` to `0.1f`, restoring the intended #219 100ms cadence on the instance that actually runs. The harvest's full fix sketch (delete the dead `_managesCombatInfrastructure` branch in `WargMissionBehavior`; add a creature-presence gate so creature-free battles don't pay a 10Hz full-agent rebuild) touches `WargMissionBehavior.cs` and `SubModule.cs`, both **out of scope** for this plan — see Maintenance notes.

> ENGINE FACT (do not re-derive): `Mission.Current.AllAgents` is the full live agent list; `UpdateGrid` re-buckets it. At 60fps a 0.1f interval rebuilds ~6× more often than 2f. This is the cadence the warg branch was written for and the value `WargMissionBehavior.cs:36` already uses — you are aligning the live instance to the already-chosen project value, not inventing one.

### PERF-03 — TroopWeight Postfixes re-resolve `TaomSettings.Instance` on every party-count read (8 sites)

`Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs` — the canonical case:

```csharp
// PartyBase_NumberOfAllMembers_Patch.cs:6-19
[HarmonyPatch(typeof(PartyBase), nameof(PartyBase.NumberOfAllMembers), MethodType.Getter)]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyBase_NumberOfAllMembers_Patch
{
    private static IOnPartyBaseNumberOfAllMembers? _hook;

    public static void Initialize(IOnPartyBaseNumberOfAllMembers hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyBase __instance, ref int __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnPartyBaseNumberOfAllMembers(__instance, ref __result);
    }
}
```

The `TaomSettings.Instance?.EnableTroopWeight ?? true` read recurs on **8 static patch sites** (grep-confirmed at `141b749`):

| File | Line |
|------|------|
| `Hooks/PartyBase_NumberOfAllMembers_Patch.cs` | 17 |
| `Hooks/PartyBase_NumberOfRegularMembers_Patch.cs` | 17 |
| `Hooks/RecruitmentVM_RefreshPartyProperties_Patch.cs` | 17 |
| `Hooks/PartyVM_PopulatePartyListLabel_Patch.cs` | 21 (note: `return true;`, it is a `Prefix`) |
| `Hooks/PartyBaseHelper_GetPartySizeText_Patch.cs` | 21 |
| `Hooks/GameMenuPartyItemVM_RefreshCounts_Patch.cs` | 17 |
| `Hooks/CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs` | 19 |
| `Hooks/CampaignUIHelper_GetPartyHealthTooltip_Patch.cs` | 20 |

> CORRECTION vs harvest: the harvest listed "PartyVM:21" — confirmed, but note that file's gate returns `true` (it is a Harmony **Prefix**, `PartyVM_PopulatePartyListLabel_Patch.cs:15-22`), unlike the others which are Postfixes returning `void`. The cached-read replacement must preserve each site's exact control flow (`return;` vs `return true;`).

These patch classes are **static** (not IoC-resolved), so the fix is a single shared lazily-cached static `TaomSettings` field that all 8 sites read through. There is no existing TroopWeight settings-provider interface (`Main/Features/TroopWeight/` has hook interfaces only; `TroopWeightIoC.cs` registers the loader/service/hooks, no provider). Introduce one tiny static cache helper rather than 8 separate fields, to keep the change DRY and reviewable.

### PERF-04 — `TaomMilitaryPowerModel` reads up to 7 MCM properties per `GetDefaultTroopPower` call

`Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs:19-36`:

```csharp
public override float GetDefaultTroopPower(CharacterObject troop)
{
    if (!_settings.EnableCustomTroopPower)
        return base.GetDefaultTroopPower(troop);

    var config = _configProvider.GetConfig();
    int tier = troop.IsHero ? troop.Level / 4 + 1 : troop.Tier;

    float basePower = CalculateTierPower(tier, _settings.OverrideVanillaTierPower,
        _settings.Tier7Power, _settings.Tier8Power, _settings.Tier9Power, _settings.Tier10Power,
        config.TroopPower);

    float multiplier = troop.IsHero
        ? _settings.HeroMultiplier
        : (troop.IsMounted ? _settings.MountedMultiplier : 1.0f);

    return basePower * multiplier;
}
```

Each `_settings.X` is a provider property; the provider re-derefs the MCM singleton per read:

```csharp
// BattleBalanceSettingsProvider.cs:3-12
public class BattleBalanceSettingsProvider : IBattleBalanceSettingsProvider
{
    public bool EnableCustomTroopPower      => TaomSettings.Instance?.EnableCustomTroopPower      ?? true;
    public bool OverrideVanillaTierPower    => TaomSettings.Instance?.OverrideVanillaTierPower    ?? false;
    public float Tier7Power                 => TaomSettings.Instance?.Tier7Power                  ?? 2.91f;
    public float Tier8Power                 => TaomSettings.Instance?.Tier8Power                  ?? 3.26f;
    public float Tier9Power                 => TaomSettings.Instance?.Tier9Power                  ?? 3.61f;
    public float Tier10Power                => TaomSettings.Instance?.Tier10Power                 ?? 3.96f;
    public float HeroMultiplier             => TaomSettings.Instance?.HeroMultiplier              ?? 1.5f;
    public float MountedMultiplier          => TaomSettings.Instance?.MountedMultiplier           ?? 1.2f;
    // ... (4 more for casualty ratios — out of scope for this finding)
}
```

`GetDefaultTroopPower` is invoked per-troop per-simulation-round in siege/map-event sim (engine `DefaultMilitaryPowerModel.GetTroopPower` → `GetDefaultTroopPower`). The JSON side (`_configProvider.GetConfig()`) is already cached; only the MCM side leaks. The fix mirrors PERF-03's caching, applied in `BattleBalanceSettingsProvider` (cache `TaomSettings.Instance` once in the ctor, read fields through it). `IBattleBalanceSettingsProvider` is registered `Reuse.Singleton` (`BattleBalanceIoC.cs:10`), so a ctor-cached reference lives for the process — correct lifetime.

> Note: the provider also exposes 4 casualty-ratio members (`EnableCustomCasualtyRatios`, `PlayerBluntDamageChance`, `AIBluntDamageChance`, `EnableCulturalSurvivalBonuses`). Caching the `TaomSettings` reference in the ctor benefits **all 12** members uniformly (one mechanical change), so convert the whole provider, not just the 7 troop-power members.

### PERF-07 — Warg BT nodes resolve IoC per evaluation (Spider/Elephant already lazy-cache)

`Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs` — two decorator classes in one file, each with a resolve-per-access static property:

```csharp
// PeriodicallyCheckIfCanAttackAnyone.cs:12-37  (class PeriodicallyCheckIfCanAttackAnyone)
public class PeriodicallyCheckIfCanAttackAnyone : WaitNSecondsTickDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();
    // ...
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
                var agentAdapter = AdapterFactory.GetAgentAdapter(agent);   // resolves IoC per nearby agent
                var wargAdapter = AdapterFactory.GetAgentAdapter(warg);     // re-resolves + re-adapts warg every iteration
                bool likelyToHit = agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange);
                if (likelyToHit)
                    return true;
            }
        }
        return false;
    }
    public override void Notify(object[] data) { }
}

// PeriodicallyCheckIfCanAttackAnyone.cs:42-68  (class CheckOnceIfCanAttackEnemy — same file)
public class CheckOnceIfCanAttackEnemy : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();
    // ... identical resolve-per-access + per-iteration GetAgentAdapter(warg) pattern in its Evaluate()
}
```

`static IMissionAdapterFactory AdapterFactory => IoC.Resolve<...>()` is a **getter** — it resolves the container on every access. It is hit twice per nearby agent per evaluation. `IMissionAdapterFactory` is a process singleton (`Main/Adapters/IMissionAdapterFactory.cs` — `GetAgentAdapter` + `ClearCache`), so a lazily-cached **instance** field (`??=`, matching `SpiderAttackTask`) is safe. The fix:

1. Replace the static resolve-getter with an instance `private IMissionAdapterFactory _adapterFactory;` + `_adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();` at the top of each `Evaluate()` (Spider/Elephant pattern).
2. Hoist `GetAgentAdapter(warg)` out of the per-agent loop — the warg adapter is the same every iteration; adapt it once before the `foreach`.

> SCOPE NOTE: the harvest's PERF-07 also names `WargAiControlledIsNotFacingEnemy.cs:16` and `WargRiderHandManager.cs:14`, and a `MissionAdapterFactory.GetOrAdd` closure-alloc micro-opt. Those files are **out of scope** for this plan (only `PeriodicallyCheckIfCanAttackAnyone.cs` is in the assigned 4) — see Maintenance notes. `MissionAdapterFactory.GetAgentAdapter` already caches per `agent.Index` (`MissionAdapterFactory.cs:21-25`), so the hoist in step 2 is a redundant-call reduction, not a correctness fix.

### Binding conventions that apply here

- **ADR-007 (adapters)**: services/VMs never hold sealed TaleWorlds types; the BT nodes already wrap `Agent` via `IMissionAdapterFactory.GetAgentAdapter`. Do not change that boundary — only cache the factory reference.
- **ADR-002 (thin entry points)**: Harmony patches stay <150 lines and delegate. The TroopWeight cache helper must not add logic to the patches — it only swaps `TaomSettings.Instance?.X` for a cached read.
- **`.claude/rules/harmony-patches.md` hot-path rule**: "Reflection / `IoC.Resolve` in hot paths MUST be cached in a static field during `Initialize()`, never resolved inside `Prefix`/`Postfix`." This plan is the direct application of that rule.
- **`FiniteFloatValidator` (house NaN-guard)**: NOT applicable here — these are pure caching changes that preserve the existing values verbatim. Do **not** add validation; that is a separate finding class (CORRECTNESS-05) and out of scope.
- **No behavior change**: every fix reads the same values it does today, just through a cached reference. The cached reference picks up live MCM edits (values read through it, not snapshotted) — preserve that property exactly (do NOT copy primitive values into fields at construction).

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build   | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` | exit 0, 0 errors |
| Tests   | `dotnet test TAOM.Tests -p:DisableModuleCopy=true` | all pass (existing + new) |
| Targeted tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~TroopWeightSettingsCache|FullyQualifiedName~BattleBalanceSettingsProvider"` | new tests pass |
| Grep check (PERF-03) | `grep -rn "TaomSettings.Instance?.EnableTroopWeight" Main/Features/TroopWeight/Hooks/` | no matches after fix |

`-p:DisableModuleCopy=true` is required on build AND test — the tests project builds Main, whose post-build target otherwise deploys to the game install. NEVER `./build.ps1` from an executor.

## Scope

**In scope** (the only files you may modify):

- `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` (PERF-01 — one constant)
- `Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/Hooks/PartyBase_NumberOfRegularMembers_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/Hooks/RecruitmentVM_RefreshPartyProperties_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/Hooks/PartyVM_PopulatePartyListLabel_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/Hooks/PartyBaseHelper_GetPartySizeText_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/Hooks/GameMenuPartyItemVM_RefreshCounts_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/Hooks/CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/Hooks/CampaignUIHelper_GetPartyHealthTooltip_Patch.cs` (PERF-03)
- `Main/Features/TroopWeight/TroopWeightSettingsCache.cs` (PERF-03 — NEW small static cache helper)
- `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs` (PERF-04)
- `Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs` (PERF-07)
- `TAOM.Tests/Features/TroopWeight/TroopWeightSettingsCacheTests.cs` (NEW)
- `TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs` (NEW — if not already present)

**Out of scope** (do NOT touch, even though they look related):

- `Main/Features/Warg/WargMissionBehavior.cs` — deleting the dead `_managesCombatInfrastructure` branch + the creature-presence rebuild gate is the *full* PERF-01 fix; it belongs to a follow-up (see Maintenance notes). This plan does the minimal cadence change only.
- `Main/SubModule.cs` — single-owner; the mission-behavior registration order is correct as-is. If a registration change seems needed, STOP and report.
- `Main/IoC.cs`, `Main/Features/*/IoC.cs` — single-owner registration files. The new `TroopWeightSettingsCache` is a static helper requiring **no** IoC registration. If you think it needs registering, STOP — it doesn't.
- `Main/Adapters/MissionAdapterFactory.cs` — the `GetOrAdd` closure micro-opt (PERF-07 tail) is out of scope.
- `Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs`, `Main/Features/Warg/WargRiderHandManager.cs` — same PERF-07 class, out of this plan's 4-file scope.
- `Main/Features/TroopWeight/TroopWeightXmlLoader.cs` — the NaN-guard (CORRECTNESS-05) is a different finding; do not touch.
- Any save-format change (new SyncData fields) — none needed.

## Git workflow

- Branch: work in the dispatched worktree's branch; do NOT push or open a PR.
- Commits: 50/72 rule, imperative, no AI attribution. Suggested split (or one commit if the reviewer prefers):
  - `perf(advancedcombat): rebuild spatial grid at 100ms cadence (issue #219)`
  - `perf(troopweight): cache TaomSettings ref across 8 Patch17 sites`
  - `perf(battlebalance): cache TaomSettings ref in settings provider`
  - `perf(warg): lazy-cache adapter factory in BT decorators`
  - Trailers when relevant: `Not-tested: Harmony postfix invocation / per-frame mission tick (requires live game)`, `Research: WargMissionBehavior #219 comment + SubModule mission-behavior order`.

## Steps

Order: TDD-first where a service/helper boundary exists (PERF-03 cache helper, PERF-04 provider). PERF-01 and PERF-07 are structurally untestable (a constant in a `MissionLogic`; IoC resolution inside a `MissionLogic`/BT leaf — both need a live `Mission`) — do those after the build is green from the testable fixes. Each step keeps the build green.

### Step 1 (PERF-03, RED): Write the failing test for the new TroopWeight settings cache

Create `TAOM.Tests/Features/TroopWeight/TroopWeightSettingsCacheTests.cs`, modeled structurally on `TAOM.Tests/Features/SettlementNameplateFade/NameplateFadeSettingsProviderTests.cs` (which proves "no MCM instance → compiled default"). Since `TaomSettings.Instance` is null in the harness, the cache must return the compiled default `true`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

[TestClass]
public class TroopWeightSettingsCacheTests
{
    [TestMethod]
    public void EnableTroopWeight_NoMcmInstance_DefaultsToTrue()
    {
        // TaomSettings.Instance is null in test (MCM v5 not loaded) → compiled default.
        Assert.IsTrue(TroopWeightSettingsCache.EnableTroopWeight);
    }
}
```

This will not compile until Step 2 creates `TroopWeightSettingsCache` — a failing (red) build is the RED state for a new type.

**Verify**: `dotnet build TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true` → fails with `CS0103`/`type or namespace 'TroopWeightSettingsCache'` (expected RED).

### Step 2 (PERF-03, GREEN): Create the cache helper and route the 8 patch sites through it

Create `Main/Features/TroopWeight/TroopWeightSettingsCache.cs` — a static lazily-cached `TaomSettings` reference (the Patch38 cached-reference pattern, adapted to static patch context). Lazy `??=` because `TaomSettings.Instance` can be null very early in load:

```csharp
namespace TAOM.Features.TroopWeight;

/// <summary>
/// Hot-path cache for the TaomSettings reference shared by the 8 Patch17_TroopWeight
/// entry points. PartyBase member-count getters are read engine-wide per tick; resolving
/// TaomSettings.Instance (a ConcurrentDictionary walk) per read was the leak (PERF-03).
/// The reference is cached once and read THROUGH (not snapshotted), so live MCM edits
/// still apply — same contract as NameplateFadeSettingsProvider (Patch38). Lazy ??= because
/// Instance is null very early in module load.
/// </summary>
internal static class TroopWeightSettingsCache
{
    private static TaomSettings _settings;

    public static bool EnableTroopWeight
    {
        get
        {
            _settings ??= TaomSettings.Instance;
            return _settings?.EnableTroopWeight ?? true;
        }
    }
}
```

Then replace the gate in all 8 patch files. Preserve each site's exact control flow:

- Postfix (`void`) sites — `PartyBase_NumberOfAllMembers_Patch.cs:17`, `PartyBase_NumberOfRegularMembers_Patch.cs:17`, `RecruitmentVM_RefreshPartyProperties_Patch.cs:17`, `PartyBaseHelper_GetPartySizeText_Patch.cs:21`, `GameMenuPartyItemVM_RefreshCounts_Patch.cs:17`, `CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:19`, `CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:20`:
  `if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;`
  → `if (!TroopWeightSettingsCache.EnableTroopWeight) return;`
- Prefix (`bool`) site — `PartyVM_PopulatePartyListLabel_Patch.cs:21`:
  `if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return true;`
  → `if (!TroopWeightSettingsCache.EnableTroopWeight) return true;`

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~TroopWeightSettingsCache"` → 1 test passes (GREEN).
- `grep -rn "TaomSettings.Instance?.EnableTroopWeight" Main/Features/TroopWeight/Hooks/` → no matches.

### Step 3 (PERF-04, RED then GREEN): Cache the TaomSettings ref in BattleBalanceSettingsProvider

First add a regression test. Check whether `TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs` exists (it did NOT at planning time — `TAOM.Tests/Features/BattleBalance/` had only `TaomCombatSimulationModelTests.cs`, `TaomMilitaryPowerModelTests.cs`, `TaomPartyHealingModelTests.cs`). Create it, modeled on `NameplateFadeSettingsProviderTests.cs` — assert the no-MCM-instance defaults match the inline `?? default` fallbacks so the ctor-cache refactor can't silently change them:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance;

namespace TAOM.Tests.Features.BattleBalance;

[TestClass]
public class BattleBalanceSettingsProviderTests
{
    // TaomSettings.Instance is null in test → provider returns compiled defaults.
    [TestMethod] public void EnableCustomTroopPower_NoMcm_DefaultsTrue()
        => Assert.IsTrue(new BattleBalanceSettingsProvider().EnableCustomTroopPower);
    [TestMethod] public void OverrideVanillaTierPower_NoMcm_DefaultsFalse()
        => Assert.IsFalse(new BattleBalanceSettingsProvider().OverrideVanillaTierPower);
    [TestMethod] public void Tier7Power_NoMcm_Defaults2_91()
        => Assert.AreEqual(2.91f, new BattleBalanceSettingsProvider().Tier7Power, 0.0001f);
    [TestMethod] public void Tier10Power_NoMcm_Defaults3_96()
        => Assert.AreEqual(3.96f, new BattleBalanceSettingsProvider().Tier10Power, 0.0001f);
    [TestMethod] public void HeroMultiplier_NoMcm_Defaults1_5()
        => Assert.AreEqual(1.5f, new BattleBalanceSettingsProvider().HeroMultiplier, 0.0001f);
    [TestMethod] public void MountedMultiplier_NoMcm_Defaults1_2()
        => Assert.AreEqual(1.2f, new BattleBalanceSettingsProvider().MountedMultiplier, 0.0001f);
}
```

Run it against the unmodified provider first — it should already pass (the inline form returns these defaults when `Instance` is null). That establishes the behavior the refactor must preserve.

Then refactor `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs` to cache `TaomSettings.Instance` once in the ctor and read all 12 members through it (Patch38 pattern):

```csharp
namespace TAOM.Features.BattleBalance;

public class BattleBalanceSettingsProvider : IBattleBalanceSettingsProvider
{
    private readonly TaomSettings _settings;
    public BattleBalanceSettingsProvider() => _settings = TaomSettings.Instance;

    public bool EnableCustomTroopPower      => _settings?.EnableCustomTroopPower      ?? true;
    public bool OverrideVanillaTierPower    => _settings?.OverrideVanillaTierPower    ?? false;
    public float Tier7Power                 => _settings?.Tier7Power                  ?? 2.91f;
    public float Tier8Power                 => _settings?.Tier8Power                  ?? 3.26f;
    public float Tier9Power                 => _settings?.Tier9Power                  ?? 3.61f;
    public float Tier10Power                => _settings?.Tier10Power                 ?? 3.96f;
    public float HeroMultiplier             => _settings?.HeroMultiplier              ?? 1.5f;
    public float MountedMultiplier          => _settings?.MountedMultiplier           ?? 1.2f;

    public bool EnableCustomCasualtyRatios  => _settings?.EnableCustomCasualtyRatios  ?? true;
    public float PlayerBluntDamageChance    => _settings?.PlayerBluntDamageChance     ?? 0.30f;
    public float AIBluntDamageChance        => _settings?.AIBluntDamageChance         ?? 0.10f;
    public bool EnableCulturalSurvivalBonuses => _settings?.EnableCulturalSurvivalBonuses ?? true;
}
```

> Keep every `?? default` literal byte-identical to the originals listed in "Current state". The provider is `Reuse.Singleton` (`BattleBalanceIoC.cs:10`), so one ctor read serves the whole process; the cached ref still reads live values.

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~BattleBalanceSettingsProvider"` → all new tests pass.

### Step 4 (PERF-07): Lazy-cache the adapter factory in the two Warg BT decorators

In `Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs`, for **both** classes (`PeriodicallyCheckIfCanAttackAnyone` and `CheckOnceIfCanAttackEnemy`):

1. Delete the static resolve-getter `private static IMissionAdapterFactory AdapterFactory => IoC.Resolve<IMissionAdapterFactory>();` and add an instance field `private IMissionAdapterFactory _adapterFactory;`.
2. At the top of each `Evaluate()`, after the warg is fetched, add `_adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();` (the `SpiderAttackTask` pattern).
3. Hoist the warg adapter out of the loop: compute `var wargAdapter = _adapterFactory.GetAgentAdapter(warg);` once before the `foreach`, then inside the loop only adapt the nearby `agent`. Preserve the existing skip conditions and `IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange)` call verbatim.

Target shape for `PeriodicallyCheckIfCanAttackAnyone.Evaluate()`:

```csharp
private IMissionAdapterFactory _adapterFactory;
// ...
public override bool Evaluate()
{
    Agent warg = Agent.GetValue();
    _adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();
    BattleSideEnum wargSide = warg.RiderAgent?.Team.Side ?? warg.Team.Side;
    var wargAdapter = _adapterFactory.GetAgentAdapter(warg);
    List<Agent> nearbyAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(10, warg);
    foreach (Agent agent in nearbyAgents)
    {
        if (agent == warg || agent == warg.RiderAgent || agent.IsMount) continue;
        if (agent.IsActive() && agent.Team?.Side != wargSide)
        {
            var agentAdapter = _adapterFactory.GetAgentAdapter(agent);
            if (agentAdapter.IsAttackLikelyToHit(wargAdapter, 30, WargConfig.WargAttackRange))
                return true;
        }
    }
    return false;
}
```

Apply the analogous change to `CheckOnceIfCanAttackEnemy.Evaluate()` (note its skip set is `agent == warg || agent == warg.RiderAgent` then `&& !agent.IsMount` inside — preserve it exactly; do not unify the two classes' conditions).

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0. (No unit test — see Test plan; these BT leaves need a live `Mission`/`SpatialGrid`.)

### Step 5 (PERF-01): Set the SpatialGrid rebuild cadence to 100ms

In `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs:10`, change:

```csharp
private const float GridUpdateInterval = 2f;
```

to:

```csharp
// 0.1f (100ms) restores the issue #219 cadence on the instance that actually owns the
// grid rebuild. The 2f value drifted creature-BT target positions up to ~20m stale.
// WargMissionBehavior.cs:36 already uses 0.1f for its (currently dead) fallback path.
private const float GridUpdateInterval = 0.1f;
```

Do not touch any other line in this file. Do NOT delete the warg dead branch or add a creature-presence gate here — that is the deferred follow-up (Maintenance notes).

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0.

### Step 6: Full build + test gate

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0, 0 errors.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true` → all pass (the project's existing test count + the new TroopWeight + BattleBalance tests; no regressions).

## Test plan

- **New: `TAOM.Tests/Features/TroopWeight/TroopWeightSettingsCacheTests.cs`** — `EnableTroopWeight_NoMcmInstance_DefaultsToTrue` (the no-MCM fallback contract). Model after `TAOM.Tests/Features/SettlementNameplateFade/NameplateFadeSettingsProviderTests.cs`.
- **New: `TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs`** — one test per representative member proving the cached provider returns the same compiled defaults as the inline form when `TaomSettings.Instance` is null (`EnableCustomTroopPower→true`, `OverrideVanillaTierPower→false`, `Tier7Power→2.91`, `Tier10Power→3.96`, `HeroMultiplier→1.5`, `MountedMultiplier→1.2`). Model after the same Nameplate test.
- **Existing regression coverage**: `TAOM.Tests/Features/BattleBalance/TaomMilitaryPowerModelTests.cs` exercises `CalculateTierPower` (pure static) — it must stay green, proving PERF-04's value semantics are unchanged.
- **Structurally untestable** (name these in the commit `Not-tested:` trailer):
  - PERF-01: `GridUpdateInterval` is a `const` consumed inside `OnMissionTick` — needs a live `Mission` tick loop.
  - PERF-07: the two Warg BT `Evaluate()` methods resolve IoC and read `SpatialGrid.Instance` / live `Agent`s — need a live `Mission` + populated grid. The lazy-cache is a behavior-neutral resolve-reduction; correctness is the unchanged `IsAttackLikelyToHit` call.
  - PERF-03 patch wiring: the 8 Harmony Postfix/Prefix bodies are thin entry points (test via game). Only the new cache helper is unit-tested.
- **Verification**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true` → all pass, including the new TroopWeight + BattleBalance tests.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true` exits 0; new `TroopWeightSettingsCacheTests` + `BattleBalanceSettingsProviderTests` exist and pass; no existing tests regress
- [ ] `grep -rn "TaomSettings.Instance?.EnableTroopWeight" Main/Features/TroopWeight/Hooks/` returns no matches
- [ ] `grep -n "GridUpdateInterval = 0.1f" Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` returns 1 match
- [ ] `grep -n "IoC.Resolve<IMissionAdapterFactory>" Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs` shows the resolve only inside `Evaluate()` via `??=`, not in a `static ... =>` property
- [ ] No files outside the in-scope list are modified (`git status`)
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- The code at the locations in "Current state" doesn't match the excerpts (the codebase drifted since `141b749` — run the drift-check command in the header).
- The 8th TroopWeight gate count is wrong — if `grep -rn "TaomSettings.Instance?.EnableTroopWeight" Main/Features/TroopWeight/Hooks/` returns other than the 8 files listed, the feature changed; report the delta.
- `BattleBalanceSettingsProviderTests` FAIL against the *unmodified* provider in Step 3 (means the inline defaults are not what this plan documented) — do not "fix" the provider to match the test; report the mismatch.
- A step's verification fails twice after a reasonable fix attempt.
- The fix appears to require touching an out-of-scope file — especially `Main/SubModule.cs`, `Main/IoC.cs`, any `*/IoC.cs`, `WargMissionBehavior.cs`, or `MissionAdapterFactory.cs`. Report the exact change you believe is needed instead of making it.
- You discover the assumption "`AdvancedCombatBehavior` is always registered before `WargMissionBehavior`, so the warg 0.1f branch is dead" is false (e.g., SubModule order changed) — that would mean setting `AdvancedCombatBehavior.GridUpdateInterval = 0.1f` double-counts with a now-live warg branch. Report it.

## Maintenance notes

For the human/agent who owns this code after the change lands:

- **`/deep-review` focus spots** (orchestrator runs it before commit for this multi-file C# change): (1) confirm the 8 TroopWeight patches preserved their exact return shape — `return;` for the 7 Postfixes, `return true;` for the `PartyVM_PopulatePartyListLabel_Patch` Prefix; a copy-paste that turns the Prefix's `return true` into a bare `return` would change vanilla-skip semantics. (2) confirm `BattleBalanceSettingsProvider` keeps reading **through** the cached reference (no snapshot-to-fields), so live MCM edits still apply. (3) confirm the Warg hoist did not reorder the skip conditions.
- **Deferred follow-ups (same finding class, deliberately out of this plan's 4-file scope — list in `plans/README.md` "Findings considered" or open as their own plans):**
  - **PERF-01 full fix**: delete the now-permanently-dead `_managesCombatInfrastructure` branch in `WargMissionBehavior.cs:85-97,58-62`, and add a creature-presence gate to `AdvancedCombatBehavior` so creature-free battles don't pay a 10Hz full-agent grid rebuild. Touches `WargMissionBehavior.cs` (+ possibly `SpiderMissionBehavior.cs`/`ElephantMissionBehavior.cs` for the gate) — separate PR.
  - **PERF-02**: `Patch30_FormationGetOrderPositionOfUnit` allocates a `FormationAdapter` per call + uncached `MixedFormationsSettingsProvider.IsEnabled` MCM read (up to 40,000×/frame). Cache the provider's `TaomSettings` ref (same pattern) + gate before the adapter alloc. See harvest PERF-02.
  - **PERF-05**: TroopWeight count-hook caches (`PartyBaseNumberOfAllMembersHook.cs`, `PartyBaseNumberOfRegularMembersHook.cs`) are `GetHashCode`-keyed dictionaries with no eviction — swap to `ConditionalWeakTable<PartyBase,Box>` (the fix already proven on `TroopWeightService._healthCache`). Also CORRECTNESS-04 (empty `catch`, identity-hash collision). See harvest PERF-05 / CORRECTNESS-04.
  - **PERF-06**: `SpatialGrid.GetAgentsInRadius` scans every occupied cell + allocates a `List` per query — rewrite to iterate only cells in radius via `TryGetValue`, optional caller buffer. Effort M, Risk MED (warg targeting was RCA'd in #219 — inclusion semantics are load-bearing). Becomes more impactful once PERF-01 raises rebuild frequency. See harvest PERF-06.
  - **PERF-07 remainder**: `WargAiControlledIsNotFacingEnemy.cs:16` (same static resolve-getter) and `WargRiderHandManager.cs:14` (per-tick resolve), plus `MissionAdapterFactory.GetAgentAdapter` `GetOrAdd` closure-alloc on cache hits (`MissionAdapterFactory.cs:24` — `TryGetValue` first). Same mechanical class.
- **Why no `FiniteFloatValidator` here**: this plan is caching-only and preserves existing values. The TroopWeight XML NaN-guard (CORRECTNESS-05) and Career mutation NaN-guard (CORRECTNESS-06) are real but separate — do not fold them in.
