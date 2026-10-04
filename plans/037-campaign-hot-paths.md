# Plan 037: Cut per-party, per-frame and per-day costs on the campaign map without changing behaviour

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat 0912e1b7..HEAD -- Main/Features/CaravanTrade Main/Features/CastleRecruitment Main/Features/AlignmentDesertion Main/Features/RealmBorders/RealmBordersSettingsProvider.cs Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs Main/Features/QuickActions/QuickActionsSettingsProvider.cs Main/Features/PartyIconScale/PartyIconScaleConfig.cs Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs Main/Features/CulturalFeats/ICulturalFeatsService.cs Main/Features/CulturalFeats/CulturalFeatsService.cs Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs Main/Adapters/ITownRosterAdapter.cs Main/Adapters/TownRosterAdapter.cs TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs TAOM.Tests/Features/CaravanTrade TAOM.Tests/Features/CastleRecruitment TAOM.Tests/Features/AlignmentDesertion TAOM.Tests/Features/Refuge/RefugeCampaignBehaviorTests.cs TAOM.Tests/Features/CulturalFeats TAOM.Tests/Features/CultureMarketplace TAOM.Tests/Migration/TranspilerSiteBindingTests.cs docs/features/caravan-trade.md docs/features/culture-marketplace.md docs/reference/harmony-patch-registry.md`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Sibling plans planned near the same commit may touch
> `docs/reference/harmony-patch-registry.md` (other sections) and `TAOM.Tests/Migration/` (other
> files); that is expected drift as long as the Patch59 section and every excerpt below still match.
> Plan 031 creates `TAOM.Tests/Features/HotPathSettingsProvidersTests.cs` for the mission-side
> providers; this plan creates a DIFFERENT file (`CampaignHotPathSettingsProvidersTests.cs`). Do not
> merge the two.

## Status

- **Priority**: P2
- **Effort**: L (four independent stages: eight settings providers, four cheap-filter reorders, the
  marketplace pass, the caravan distance hand-off)
- **Risk**: MED (behaviour-preserving by design; the breadth and one new transpiler are the risk)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `0912e1b7`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), measured at `dffdf879` and unchanged at `0912e1b7` (only documentation and run records
  changed between them; `git diff --stat dffdf879..0912e1b7 -- Main TAOM.Tests` is empty). Failing:
  `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the other languages;
  the paid translator run waits on the maintainer). Skipped: `WargAttack_FastWarg_InvokesRunningAttack`,
  `WargAttack_SlowWarg_InvokesStandingAttack`. Python suite: not needed (this plan touches no
  `tools/` file); for the record it is `Ran 2962 tests`, `FAILED (failures=3, skipped=8)`
  (`test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`,
  `test_default_is_on_the_e_drive`). Trunk CI under reference assemblies also fails
  `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`
  (`FileNotFoundException` for `TaleWorlds.MountAndBlade.View`).
- **Issue**: filed by the orchestrator before execution

## Why this matters

The campaign map multiplies every per-party, per-frame and per-day cost: 2,033 mobile parties at +30 s
on a new campaign (302 of them caravans), rising to 3,042 during fast-forward, 4,770 to 4,879 living
heroes, about 1,000 settlements (78 towns), and fast-forward multipliers of 4x, 8x and 16x on campaign
time. The maintainer's desktop logged 7 to 8 fps in fast-forward while lords and villagers spawned
(`docs/migration/v1.5.2-impact.md`, the "Time running" row). TAOM adds avoidable work on these paths:
eight campaign settings providers walk MCM's settings containers on every property read (per party per
hour, per caravan score, per party per day, every map frame), four hooks do per-party work before the
cheap check that would have skipped it, the culture marketplace rebuilds a whole culture's item-id
sets for every town every day, and caravan scoring asks the engine for a travel distance vanilla
computed a moment earlier. This plan removes that work and changes nothing a player can see: every AI
choice, price, stock decision, speed, desertion and log line stays the same, except one consolidated
error line and three new caravan hand-off lines (Stage D), which the commit bodies and feature docs
describe. On the desktop the map is not frame-bound (median 169.8 fps in fast-forward over 648 heartbeat
windows); a slower CPU is where this matters.

## Current state

Every excerpt below was read at `0912e1b7`. Line numbers are that commit's.

### The pattern to copy for every settings provider (Stage A)

`Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs` (commit `7feca96b`, refined by `02157b18`):

```csharp
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public BattleBalanceSettingsProvider() { }
    internal BattleBalanceSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool EnableCustomTroopPower      => Settings?.EnableCustomTroopPower      ?? true;
```

Its tests (`TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs`) hold the shapes
this plan reuses: no-MCM default pins (`TaomSettings.Instance` is null in the test host because MCM is
never initialised there), a read-through test that builds the provider on `new TaomSettings()` through
the internal constructor and edits the object, the DryIoc resolve test `Provider_ResolvesFromARealContainer`,
and the IL rule `Getters_NeverReadTaomSettingsInstance_TheLazyAccessorDoes`, which uses
`TAOM.Tests/Migration/IlCallScanner.cs` (`ExtractCalledMethods(MethodBase, byte[])`, yields called
methods in IL order) and this predicate:

```csharp
        bool IsInstanceGetter(MethodBase m) =>
            m.Name == "get_Instance" && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom(typeof(TaomSettings));
```

Why lazy, never in the constructor: a constructor read caches null for the whole session if the
provider is resolved before MCM sets `BaseSettingsProvider.Instance`. The `??=` accessor keeps
resolving until it gets a non-null instance, so no provider depends on when it is first resolved, and
until MCM is up every getter falls back exactly as today.

MCM facts (MCM v5 decompile in the local decompile dump, `_modules_build/TAOM.Dependencies__MCMv5.cs`, read for this plan):

- `GlobalSettings<T>.Instance` (line 6321): `if (!GlobalSettings.Cache.ContainsKey(typeof(T))) GlobalSettings.Cache.TryAdd(...); return BaseSettingsProvider.Instance?.GetSettings(GlobalSettings.Cache[typeof(T)]) as T;`
  (two `ConcurrentDictionary` operations, then `GetSettings`).
- `DefaultSettingsProvider.GetSettings(string id)` (line 2214) loops over every settings container and
  every external provider.
- A container keeps one instance per id (`LoadedSettings.Add(settings.Id, settings)`, about line 1964;
  `GetSettings` returns `LoadedSettings[id]`, about line 1971). `OverrideSettings` (about line 1995)
  calls `SettingsUtils.OverrideSettings(LoadedSettings[settings.Id], settings)`, which copies values
  into the existing object (`OverrideValues`, line 4449). The instance is never swapped, so a cached
  reference sees every MCM edit, reset and preset. `TaomSettings` is
  `public class TaomSettings : AttributeGlobalSettings<TaomSettings>` (`Main/Features/TaomSettings.cs:11`).

### Stage A: the eight campaign providers (every `TaomSettings.Instance` read at `0912e1b7`)

All are registered `Reuse.Singleton` (one instance per process): `CaravanTradeIoC.cs:10`,
`CastleRecruitmentIoC.cs:10`, `AlignmentDesertionIoC.cs:10`, `RealmBordersIoC.cs:11`,
`QuickActionsIoC.cs:12`, `TimeAccelerationIoC.cs:11` use `Register<IX, X>(Reuse.Singleton)`;
`FieldCommissionIoC.cs:38-40` uses `RegisterDelegate<IFieldCommissionConfigProvider>(r => new FieldCommissionSettingsProvider(r.Resolve<FieldCommissionConfigProvider>()), Reuse.Singleton)`;
`PartyIconScaleConfig` is a static class.

| File | Reads | Hot path that reaches it |
|---|---|---|
| `Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs` | `:21` `Enabled => TaomSettings.Instance?.EnableCaravanTrade ?? Cfg.Enabled;`, `:22` `ApplyToPlayerCaravans`, `:23` `RangeMultiplier`, `:33` `BudgetFactorFloor`, `:41` `var dropdown = TaomSettings.Instance?.CaravanWarTradePolicy;` | every caravan destination score (`CaravanTradeService.IsActiveFor`, `:145-151`) and every very-far range read (`ScaleVeryFarDistance`, `:70-77`) |
| `Main/Features/CastleRecruitment/CastleRecruitmentSettingsProvider.cs` | `:20` `IsEnabled`, `:22` `IsAiEnabled`, `:24-25` `NotablesPerCastle` (`SettingClamp.Clamp(..., 1, 5)`) | `Patch42_HourlyTickParty_Postfix` for every party every hour; `CastleAiToggle.IsCastleAndAiDisabled` inside two transpiled vanilla AI loops |
| `Main/Features/AlignmentDesertion/AlignmentDesertionSettingsProvider.cs` | `:19` `IsEnabled`, `:21` `Rate`, `:23` `ApplyToAi`, `:25` `ApplyToPlayer`, `:27` `ApplyToParties`, `:29` `ApplyToGarrisons` | every party and settlement every day |
| `Main/Features/RealmBorders/RealmBordersSettingsProvider.cs` | `:90, :92, :94, :96, :102, :104, :106, :108, :110, :112, :114, :116`, `:122` (`ColourVersion`: `var settings = TaomSettings.Instance;`), `:224` (`Fade()`, two reads) | `RealmBorderService.OnMapFrame` (`RealmBorderService.cs:239-299`) reads about 11 of these every map frame |
| `Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs` | `:117-124`, `private static FieldCommissionMcmSnapshot Capture()`, seven reads | `GetConfig()` (`:50`) from `FieldCommissionBehavior.OnTick` (`TickEvent`, every frame) |
| `Main/Features/QuickActions/QuickActionsSettingsProvider.cs` | every getter, `:7-27` | `InventorySearchCampaignBehavior.OnTick` reads `EnableInventorySearch` every frame (`InventorySearchCampaignBehavior.cs:80-86`) |
| `Main/Features/PartyIconScale/PartyIconScaleConfig.cs` | `:47` `public static float GetScale() => Resolve(TaomSettings.Instance?.MapFigureScale);` | every party-icon build (called from Patch53's rewritten engine IL) |
| `Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs` | `:5` `FastForwardMultiplier`, `:12` `ExtraFastForwardMultiplier` (`ClampExtra(FastForwardMultiplier, TaomSettings.Instance?.ExtraFastForwardMultiplier ?? 8)`), `:14` `CtrlSpaceMultiplier` | `TimeAccelerationMixin.OnRefresh` every frame (`TimeAccelerationMixin.cs:87-91`) through `TimeAccelerationService.IsExtraFastForwardActive` (`:118-121`) |

Constructors today: `CaravanTradeSettingsProvider(ICaravanTradeConfigProvider configProvider)` (`:14-17`);
`CastleRecruitmentSettingsProvider(ICastleRecruitmentConfigProvider configProvider)` (`:15-18`, stores
`configProvider.GetConfig()`); `AlignmentDesertionSettingsProvider(IAlignmentDesertionConfigProvider configProvider)`
(`:14-17`, same); `RealmBordersSettingsProvider(IModLogger logger)` (`:85-88`, throws on a null logger);
`FieldCommissionSettingsProvider(IFieldCommissionConfigProvider jsonProvider)` (`:45-48`);
`QuickActionsSettingsProvider` and `TimeAccelerationSettingsProvider` have NO constructor (the compiler
supplies a public parameterless one). Adding an internal constructor to those two removes the implicit
public one, so each must also get an explicit `public X() { }`, exactly as `BattleBalanceSettingsProvider`
has.

`FieldCommissionSettingsProvider.cs:18-20`, the class comment this plan must rewrite:

```csharp
/// <c>TaomSettings.Instance</c> is read on every call rather than captured, which is what makes the
/// MCM properties honestly <c>RequireRestart = false</c> — the JSON file still needs a full
/// application restart (the decorated provider is a <c>Lazy</c> singleton), but the knobs do not.
```

Compiled MCM defaults the Stage A tests rely on (`Main/Features/TaomSettings.cs`): `EnableCaravanTrade`
true (:111), `CaravanTradeApplyToPlayer` true (:116), `CaravanRangeMultiplier` 1.6f (:121),
`EnableCastleRecruitmentAi` true (:151), `CastleNotablesPerCastle` 3 (:156), `FastForwardMultiplier` 4
(:427), `ExtraFastForwardMultiplier` 8 (:432), `CtrlSpaceMultiplier` 16 (:437), `EnableFieldCommission`
true (:604), `EnableInventorySearch` true (:707), `EnableAlignmentDesertion` true (:1096),
`AlignmentDesertionRate` 0.5f (:1101), `MapFigureScale` 0.15f (:1281), `EnableRealmBorders` true
(:1756), `RealmBordersHeraldicBands` false (:1761). `FieldCommissionConfig.Enabled` defaults true
(`Main/Features/FieldCommission/Domain/FieldCommissionConfig.cs:12`).

### Stage B1: castle recruitment reads its toggles before its filter

`Main/Features/CastleRecruitment/Hooks/Patch42_HourlyTickParty_Postfix.cs:57-77`:

```csharp
    [HarmonyPostfix]
    public static void Postfix(RecruitmentCampaignBehavior __instance, MobileParty mobileParty)
    {
        if (_settings == null || _checkRecruiting == null || !_settings.IsEnabled || !_settings.IsAiEnabled)
            return;
        // Vanilla HourlyTickParty's own guards (line 293): AI lord/caravan parties only, not in a
        // map event, never the main party.
        if ((!mobileParty.IsCaravan && !mobileParty.IsLordParty) || mobileParty.MapEvent != null || mobileParty == MobileParty.MainParty)
            return;
        Settlement settlement = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(mobileParty);
        if (settlement == null || !settlement.IsCastle || settlement.IsUnderSiege)
            return;
        try
        {
            _checkRecruiting(__instance, mobileParty, settlement);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CastleRecruitment] CheckRecruiting failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
```

Engine facts (`pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior`,
v1.5.3): `public void HourlyTickParty(MobileParty mobileParty)` (line 300) dereferences `mobileParty` on
its first line (`if ((!mobileParty.IsCaravan && !mobileParty.IsLordParty) || ...`), so the postfix
never sees a null party. `pwsh tools/taom-src.ps1 path Helpers.MobilePartyHelper` (line 266):
`public static Settlement GetCurrentSettlementOfMobilePartyForAICalculation(MobileParty mobileParty)`
reads `CurrentSettlement`, `LastVisitedSettlement` and positions and writes nothing. Every filter
operand is a side-effect-free read, so moving the two settings reads to the end changes no outcome.
`CampaignPeriodicEventManager` dispatches `HourlyTickParty` with `doParallel: false` (main thread).
`CastleAiToggle.cs:20-24` reads both toggles through the same provider and needs no edit: it gets
cheaper through Stage A.

### Stage B2: alignment desertion snapshots the roster before its gates

`Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs:84-102`:

```csharp
    private void ApplyDesertion(TroopRoster roster, string kingdomId, bool isPlayerOwned, bool isGarrison, bool showPlayerPopup)
    {
        if (roster == null)
            return;

        // Snapshot first — never mutate the roster while scanning it.
        var snapshot = new List<DesertionTroopInfo>(roster.Count);
        foreach (var element in roster.GetTroopRoster())
        {
            var character = element.Character;
            if (character == null)
                continue;
            snapshot.Add(new DesertionTroopInfo(character.StringId, character.Culture?.StringId, character.IsHero, element.Number));
        }

        if (snapshot.Count == 0)
            return;

        var desertions = _service.CalculateDesertion(kingdomId, isPlayerOwned, isGarrison, snapshot);
```

`Main/Features/AlignmentDesertion/AlignmentDesertionService.cs:26-51` (the gates that do not depend on
the roster come AFTER the snapshot is built):

```csharp
    public IReadOnlyList<TroopDesertionResult> CalculateDesertion(
        string ownerKingdomId, bool isPlayerOwned, bool isGarrison, IReadOnlyList<DesertionTroopInfo> troops)
    {
        var result = new List<TroopDesertionResult>();

        if (!_settings.IsEnabled || troops == null || troops.Count == 0)
            return result;

        // Owner gate (player / AI).
        if (isPlayerOwned && !_settings.ApplyToPlayer) return result;
        if (!isPlayerOwned && !_settings.ApplyToAi) return result;

        // Location gate (parties / garrisons).
        if (isGarrison && !_settings.ApplyToGarrisons) return result;
        if (!isGarrison && !_settings.ApplyToParties) return result;

        var ownerSide = _alignment.GetKingdomSide(ownerKingdomId);
        // ...
        if (ownerSide == FactionSide.Neutral) return result;

        var rate = _settings.Rate;
        // ...
        if (rate <= 0f) return result;
```

NaN note (load-bearing): `rate <= 0f` is FALSE for NaN, so today a NaN rate proceeds and deserts
`Math.Max(1, (int)(count * NaN))` = 1 per opposed type. The JSON provider rejects NaN
(`AlignmentDesertionConfigProvider.cs:73-74`), but the MCM value is not clamped
(`AlignmentDesertionSettingsProvider.cs:21`). This plan keeps that exact behaviour (parity) and pins it
with a test; changing it is out of scope.

`IAlignmentDesertionService` (`Main/Features/AlignmentDesertion/IAlignmentDesertionService.cs`) has one
implementation (`AlignmentDesertionService`) and is resolved once at `Main/SubModule.cs:1478`. Its
tests: `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionServiceTests.cs` (21 `CalculateDesertion_*`
tests; `Setup` defaults every toggle on, rate 0.5, `empire_s` Evil, `empire_w` Free, `umbar` Neutral).

### Stage B3: refuge walks every world battle with no refuge

`Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs:152-163` and `:192-211`:

```csharp
    private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        foreach (var refugeId in RefugePartyIds(mapEvent))
            _refuges.OnMapEventStarted(refugeId);
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        foreach (var refugeId in RefugePartyIds(mapEvent))
            _refuges.OnMapEventEnded(refugeId);
        // ...
    }
    // ...
    private static List<string> RefugePartyIds(MapEvent mapEvent)
    {
        var ids = new List<string>();
        if (mapEvent == null)
            return ids;
        try
        {
            foreach (var party in mapEvent.InvolvedParties)
            // ...
```

`Main/Features/Refuge/RefugeService.cs`: `public IReadOnlyCollection<RefugeData> AllRefuges => _refuges.Values;`
(`:161`); `OnMapEventStarted(string partyId)` begins `if (partyId == null || !_refuges.TryGetValue(partyId, out var data)) return;`
(`:487-490`) and `OnMapEventEnded` begins with the same line (`:522-525`). So when the book is empty
every id is ignored, and returning before the walk is behaviour-identical.
`TAOM.Tests/Features/Refuge/RefugeCampaignBehaviorTests.cs` is `[TestCategory("RequiresGame")]` and
builds the behaviour on NSubstitute fakes (`Setup`, lines 29-41).

### Stage B4: party speed walks the roster for every party

`Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:22-43` and `:70-80`:

```csharp
    public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
    {
        var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);
        // ...
        var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party);
        var terrain = MapTerrain(
            Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
        // ...
        var isNight = (Campaign.Current?.IsNight ?? false) && !mobileParty.IsCurrentlyAtSea;
        var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster);

        _feats.ApplyTerrainSpeedFeats(culture, terrain, isNight, ref result);
        _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);

        return result;
    }
    // ...
    private static (int mounted, int total) CountMountedAndTotal(TroopRoster roster)
    {
        int total = roster.TotalManCount;
        int mounted = 0;
        foreach (var element in roster.GetTroopRoster())
        {
            if (element.Character?.IsMounted == true)
                mounted += element.Number;
        }
        return (mounted, total);
    }
```

The counts serve exactly one consumer, `Main/Features/CulturalFeats/CulturalFeatsService.cs:134-143`:

```csharp
    public void ApplyRohanInfantryPenalty(
        ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result)
    {
        if (culture == null || totalCount <= 0)
            return;
        if (!culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
            return;
        if (mountedCount * 2 < totalCount)
            result.AddFactor(TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus, CultureText);
    }
```

So for a null culture or a culture without `RohanInfantrySpeedFeat`, the counts never matter: passing
`(0, 0)` returns at `totalCount <= 0` with the same (unchanged) result. `ICulturalFeatsService`
(`Main/Features/CulturalFeats/ICulturalFeatsService.cs:39-40`) has one implementation,
`CulturalFeatsService` (sealed). Engine fact (`pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CultureObject`,
line 245): `public bool HasFeat(FeatObject feat) { return _cultureFeats.Contains(feat); }`.
`TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs` is `[TestCategory("RequiresGame")]`
(it builds `FeatObject`s by reflection, `EnsureFeatsInitialised`) and has the helper
`AdapterWith(params FeatObject[] present)` (line 1313) and four `ApplyRohanInfantryPenalty_*` tests
(lines 259-308).

### Stage C: the culture marketplace's daily town pass

`CultureMarketplaceBehavior.OnDailyTickSettlement` (`Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs:146-178`)
runs for every town every day: `EnsureGuaranteedStock(settlement, cultureId)` then
`FilterForeignCultureItems(settlement, cultureId, _tuning.MaxFilterRemovalsPerTick)`. The one-time
new-game sweep (`:97-123`) also calls `FilterForeignCultureItems` for every town.

`Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs:26-43` (one roster walk per
guaranteed item):

```csharp
    public int EnsureGuaranteedStock(Settlement settlement, string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return 0;
        var routed = _poolService.GetRoutedItemsForCulture(cultureId);
        if (routed.Count == 0) return 0;
        var totalAdded = 0;
        for (var i = 0; i < routed.Count; i++)
        {
            var entry = routed[i];
            if (entry.MinStock <= 0) continue;
            var have = _townAdapter.GetItemCount(settlement, entry.ItemId);
            if (have >= entry.MinStock) continue;
            var need = entry.MinStock - have;
            if (_townAdapter.AddItem(settlement, entry.ItemId, need))
                totalAdded += need;
        }
        return totalAdded;
    }
```

`:53-77` (two HashSets of the culture's whole routed list and whole pool, rebuilt on every call):

```csharp
    public int FilterForeignCultureItems(Settlement settlement, string cultureId, int removalCap)
    {
        if (string.IsNullOrEmpty(cultureId)) return 0;
        if (removalCap <= 0) return 0;

        var snapshot = _townAdapter.EnumerateRoster(settlement);
        if (snapshot.Count == 0) return 0;

        var routedHere = _poolService.GetRoutedItemsForCulture(cultureId);
        HashSet<string> routedIdsHere = null;
        if (routedHere.Count > 0)
        {
            routedIdsHere = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < routedHere.Count; i++)
                routedIdsHere.Add(routedHere[i].ItemId);
        }

        HashSet<string> pooledHere = null;
        var pool = _poolService.GetPool(cultureId);
        if (pool != null && pool.Items.Count > 0)
        {
            pooledHere = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < pool.Items.Count; i++)
                pooledHere.Add(pool.Items[i].ItemId);
        }
```

(`:79-95`, the removal loop, stays as it is.)

`Main/Adapters/TownRosterAdapter.cs:59-86`, `GetItemCount` (one `MBObjectManager` lookup plus one full
roster walk per call):

```csharp
    public int GetItemCount(Settlement settlement, string itemId)
    {
        if (settlement == null || string.IsNullOrEmpty(itemId)) return 0;
        try
        {
            var itemObject = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
            if (itemObject == null) return 0;
            // ...
            var roster = settlement.ItemRoster;
            var total = 0;
            for (var i = 0; i < roster.Count; i++)
            {
                if (roster.GetItemAtIndex(i) == itemObject)
                    total += roster.GetElementNumber(i);
            }
            return total;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CultureMarketplace] GetItemCount('{itemId}' @ {settlement.StringId}) failed: {ex.Message}");
            return 0;
        }
    }
```

Why the caches are valid for the whole process, and need no session reset:

- `CultureItemPoolService.BuildPools()` begins `if (_pools != null) return;` (`CultureItemPoolService.cs:44`);
  `_pools` and each pool's `Items` list are written only inside `BuildPools`. `GetPool` throws
  `InvalidOperationException` before `BuildPools` (`:197-203`), so `EnsureGuaranteedStock`, which never
  called `GetPool`, must still not call it.
- `GetRoutedItemsForCulture` (`:224-242`) builds a NEW list on every call from
  `_config.GetItemRouting()`; `CultureMarketplaceConfigProvider` writes `_routing` only in
  `EnsureLoaded` (`_routing = new ...` line 62, `_routing[itemId] = ...` line 228), which runs once.
  The result is therefore a pure function of `cultureId` after the first call.
- Routing is keyed by item id (`StringComparer.Ordinal`), so a culture's routed list holds each id at
  most once. The config routes 4 items to each evil culture, 10 to `erebor`, 4 to `mirkwood`, all
  `min_stock="1"` (`Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml:55-93`).
- Adding one item cannot change another item's count: `Town.OnInventoryUpdated` only calls
  `MarketData.OnTownInventoryUpdated(item, count)` (`pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Settlements.Town`,
  line 753-756). So counting every guaranteed item before any top-up gives the same counts as counting
  each just before its own top-up.
- ItemObjects are NOT cached across ticks: `MBObjectManager.Init()` creates a new manager
  (`Instance = new MBObjectManager();`, `TaleWorlds.ObjectSystem.MBObjectManager` decompile line
  363-366) and `Destroy()` nulls it (`:370-373`), so an `ItemObject` held across campaigns could go
  stale. The single walk resolves each id once per town tick, the same number of lookups as today.

`ITownRosterAdapter.GetItemCount` has no caller in `Main/` other than `EnsureGuaranteedStock`
(`git grep -n "GetItemCount(" -- Main` shows only `TownRosterAdapter.cs`, `ITownRosterAdapter.cs`,
`CultureMarketplaceMaintenanceService.cs:36`, plus the unrelated `IPartyItemRosterAdapter.GetItemCount(string)`).
Its test callers are the nine tests in
`TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceMaintenanceServiceGuaranteedStockTests.cs`,
which stub it (`_townAdapter.GetItemCount(null, "warg_brown").Returns(0);`). `ArmourStockSweepService`
uses `ITownRosterAdapter` but not `GetItemCount`.

### Stage D: caravan scoring repeats vanilla's distance query

`Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs` (64 lines,
category `Patch59_CaravanTrade`, applied by `TryPatchCategory("Patch59_CaravanTrade")` inside
`SubModule.OnSubModuleLoad`, after `IoC.Configure()`), lines 20-63:

```csharp
[HarmonyPatch(typeof(CaravansCampaignBehavior), "GetTradeScoreForTown")]
[HarmonyPatchCategory("Patch59_CaravanTrade")]
public static class CaravansCampaignBehavior_GetTradeScoreForTown_Patch
{
    private static ICaravanTradeService _service;
    private static ICaravanVisitMemory _memory;

    [HarmonyPostfix]
    public static void Postfix(ref float __result, MobileParty caravanParty, Town town)
    {
        // Positive-requirement gate: vanilla rejections (-1) and any NaN pass through untouched.
        if (!(__result > 0f)) return;
        if (caravanParty == null || town?.Settlement == null) return;

        try
        {
            _service ??= IoC.Resolve<ICaravanTradeService>();
            _memory ??= IoC.Resolve<ICaravanVisitMemory>();

            bool isNaval = caravanParty.HasNavalNavigationCapability;
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                caravanParty, town.Settlement, isNaval, out var navType, out var navDistance, out _);
            if (navType == MobileParty.NavigationType.None) return;

            float speed = isNaval
                ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed
                : Campaign.Current.EstimatedAverageCaravanPartySpeed;
            float days = navDistance / (speed * CampaignTime.HoursInDay);

            bool isHome = town.Settlement == caravanParty.HomeSettlement;
            bool isPlayer = caravanParty.Owner?.Clan == Clan.PlayerClan;

            // Recency penalty from the per-caravan visit memory (string ids at the boundary, ADR-007).
            // Replaces the old LastVisitedSettlement check, which was inert (it only ever matched the
            // parked/current town, which vanilla already excludes from candidates).
            float recency = _memory.GetRecencyPenaltyFactor(caravanParty.StringId, town.Settlement.StringId);

            __result = _service.ReweightTradeScore(__result, days, isNaval, isHome, recency, isPlayer);
        }
        catch (Exception)
        {
            // Degrade gracefully to the vanilla score.
        }
    }
}
```

Engine facts (`pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior`, v1.5.3):

- `private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isTargetingPort)` (line 966). Its first two statements (lines 968-969):
  ```csharp
  bool flag = (isTargetingPort = caravanParty.HasNavalNavigationCapability);
  AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(caravanParty, town.Settlement, isTargetingPort, out bestNavigationType, out var bestNavigationDistance, out var _);
  ```
  The same party, the same settlement and the same port flag TAOM's postfix passes. Vanilla keeps the
  distance in a local, so a postfix cannot read it. It returns `-1f` (with `bestNavigationType = None`)
  on every rejection; a positive result only comes from the branch where `bestNavigationType != None`.
  The rest of the body only reads party, town and market state.
- `GetTradeScoreForTown` has one caller, `FindNextDestinationForCaravan` (line 912-940), which scores
  every town once per pass. `ThinkNextDestination` (line 901-909) runs a second pass
  (`distanceCut: false`) only when the first found no town with a positive score. TAOM's reweight turns
  no positive score non-positive (a multiplier above 0 and a recency factor in (0, 1]), so in a second
  pass TAOM computed nothing in the first. A per-(party, town) cache over one destination search would
  therefore never hit; that is why the brief's fallback option is rejected below.
- `HourlyTickParty` (line 617) is dispatched with `doParallel: false`; the other callers
  (`OnSettlementEntered` and friends) are campaign events on the main thread.
- `pwsh tools/taom-src.ps1 path Helpers.AiHelper` (line 11):
  `public static void GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(MobileParty mobileParty, Settlement settlement, bool isTargetingPort, out MobileParty.NavigationType bestNavigationType, out float bestNavigationDistance, out bool isFromPort)`.
  For a land caravan on the map it costs `DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement`
  then `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement, ...)` (an entrance lookup plus the
  settlement distance cache); inside a settlement it reads the settlement-to-settlement cache.
- Harmony rebuilds a patched method from its ORIGINAL IL and every transpiler each time the method's
  patch set changes (`PatchFunctions.UpdateWrapper`, the local decompile dump's
  `_modules_build/TAOM.Dependencies__0Harmony.cs:4853-4863`).
  PatchShield's second pass adds a finalizer to this target, so the transpiler runs at least twice per
  process; it must be idempotent on original IL and must log its outcome once, not once per run.

A reviewed decision this stage revisits: `docs/features/caravan-trade.md` "## Performance" and
`docs/reviews/rca-caravan-trade-2026-07-04.md` finding 2 (MED downgraded to LOW-accepted) kept the
second `AiHelper` call because it is cache-backed and cheap. The 2026-10-02 perf audit counts it on a
loop of 302 caravans times 78 towns times up to two passes. Stage D is a separate commit so it can be
reviewed, or dropped, on its own (see Orchestrator steps).

Exemplars for Stage D: `Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs` (a stack-identical
call-operand swap that bails, logs and returns the stream unchanged when its site is not found) and
`TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` (feeds real engine IL through a production matcher,
`[TestCategory("RequiresGameIL")]` plus `[TestCategory("BindingVerification")]`).
`TAOM.Tests/Features/CaravanTrade/CaravanTradeBindingTests.cs` already pins that `AiHelper` resolves
(`DistanceHelper_Resolves_AgainstInstalledEngine`).

Doc lines Stage D updates (find each by its text):

- `docs/features/caravan-trade.md`, the lever-2 table row (line 25), contains
  ``Recomputes raw travel days from the same public inputs vanilla used (`AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` + caravan-speed props)``.
- `docs/features/caravan-trade.md`, "## Performance" (line 98), contains
  ``and recomputes the distance via `AiHelper`; this was reviewed twice (deep-review + Codex) and **verified cache-backed**``.
- `docs/reference/harmony-patch-registry.md`, "## Patch59_CaravanTrade" (line 496), the line
  ``**Target:** `CaravansCampaignBehavior.CanTradeWith` + `.GetTradeScoreForTown` + `.GetDistanceLimitVeryFarAsDaysForNavigationType` + `.CalculateBudgetFactor` (all private, Postfix ×4)``.
- `Main/Features/CaravanTrade/ICaravanTradeService.cs:48`:
  ``/// <param name="days">Raw travel time in days (vanilla's <c>num</c>), recomputed from the same public inputs.</param>``.

### Conventions that bind this change

- ADR-002 (`docs/adrs/002-thin-entry-points.md`): entry points (Harmony patches, campaign behaviours,
  GameModels) stay under 150 lines and delegate; logic lives in services or pure helpers.
- ADR-007 (`docs/adrs/007-adapter-pattern.md`): no sealed TaleWorlds type inside a service. The new
  `CaravanDistanceHandoff` keys on `object`; `ITownRosterAdapter.GetItemCounts` keeps the engine walk in
  the adapter.
- ADR-008 (`docs/adrs/008-testability-requirements.md`): services and pure helpers 100% covered;
  Harmony, GameModel and behaviour bodies are game-tested (IL-order tests below stand in where useful).
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG`.
- `.claude/rules/csharp-architecture.md`: "IoC Lifetimes" (no lifetime changes); "Config Providers
  MUST Validate" (keep every `SettingClamp`, fallback and validation exactly as is); "Engine-Float
  Decision Gates": a moved or extracted gate is a new gate and gets its NaN test in the same commit,
  and when parity keeps an inverted form the test pins it and a comment says it is deliberate (Stage B2).
- `.claude/rules/gamemodels.md` rule 4: a GameModel override body holds only boundary conversion and
  service delegation, no `if`; the branch for Stage B4 goes in the boundary helper, the decision in the
  service.
- `.claude/rules/harmony-patches.md`: before editing a patch, read `docs/reviews/lessons/harmony-il.md`
  and the patch's registry section (`docs/reference/harmony-patch-registry.md`, "## Patch42_CastleRecruitment"
  line 315 and "## Patch59_CaravanTrade" line 496). Lessons that apply: "Make IL-mutating transpilers
  soft-fail on a missing anchor", "Pin a single-occurrence transpiler swap ... and bail (never fall
  through)", "Static cache state inside a Harmony patch is untestable by construction, so extract the
  decision", "An IL call-presence test does not pin control flow, and must not be described as if it
  does". `.claude/rules/csharp-patterns.md` "Transpiler Note": a single operand swap needs no
  `CodeMatcher`; manual iteration matches `CastleAiTranspiler`.
- `.claude/rules/tests.md`: MSTest plus NSubstitute, names `Method_State_Expected`; a test that loads or
  executes engine members carries `[TestCategory("RequiresGame")]`; a test reading engine IL carries
  `[TestCategory("RequiresGameIL")]`.
- `.claude/rules/simplicity-criterion.md`: two leads were rejected under it (Maintenance notes); do not
  add them back.

### Logging (the maintainer's instruction D6: taom_debug.log is the critical record)

`Main/Core/Logging/FileLogger.cs`: INFO, WARNING and ERROR flush synchronously on the calling thread and
survive a crash; DEBUG is queued. Per-call or per-frame lines are never INFO. What this plan logs:

- **Stages A, B and C remove and add no information.** The provider caches, the reordered filters and
  the marketplace caches skip only work whose result was never used; nothing disables itself or falls
  back in a new way, and the no-MCM fallback is today's. No log line changes, except Stage C's one
  error line, which consolidates per-item lines into one line naming every item (commit body and
  feature doc say so; the format is pinned by a test).
- **Stage D (new code that binds a transpiler and can fall back) logs:** a one-line header when the
  hand-off binds (INFO) or fails to bind (WARNING, with the reason and the consequence), logged when
  the outcome first appears or changes, never once per transpiler run; one WARNING the first time a
  bound hand-off misses at runtime (the first occurrence in full: caravan id, town id, port flag); and
  a periodic INFO line at most once every 300 s of real time with reused and recomputed counts for the
  window and since start. There is no campaign-end hook to hang a summary on, so the periodic line
  carries running totals. Exact formats are in Steps 21 and 23 and are pinned by tests.

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`, graph built before `0912e1b7`, stale only by `plans/` evidence files)

- `CaravansCampaignBehavior_GetTradeScoreForTown_Patch`, `CaravanTradeSettingsProvider`,
  `CastleRecruitmentSettingsProvider`, `AlignmentDesertionBehavior`, `AlignmentDesertionSettingsProvider`,
  `TaomPartySpeedModel`, `CulturalFeatsService`, `QuickActionsSettingsProvider`, `TownRosterAdapter`:
  "No affected nodes found" (reached through DryIoc, Harmony or `SubModule`; the graph does not follow those).
- `Patch42_HourlyTickParty_Postfix`: `.OnSubModuleLoad() [calls] Main/SubModule.cs:L654` (the
  `Initialize` call; unchanged).
- `AlignmentDesertionService`: `AlignmentDesertionServiceTests` and its 21 `CalculateDesertion_*` tests (21 `[TestMethod]`s in the file).
- `RealmBordersSettingsProvider`: 16 graph-listed tests in `TAOM.Tests/Features/RealmBorders/RealmBordersProviderTests.cs` (26 `[TestMethod]`s in the file)
  (they use the public constructor, which stays).
- `FieldCommissionSettingsProvider`: 22 graph-listed tests in `TAOM.Tests/Features/FieldCommission/FieldCommissionSettingsProviderTests.cs` (25 `[TestMethod]`s in the file).
- `PartyIconScaleConfig`: 10 `Resolve_*` tests in `PartyIconScaleConfigTests.cs`.
- `TimeAccelerationSettingsProvider`: `TimeAccelerationSettingsProviderTests.cs` (`Provider()` uses `new TimeAccelerationSettingsProvider()`).
- `RefugeCampaignBehavior`: `RefugeCampaignBehaviorTests.cs` (10 `[TestMethod]`s; its `Setup` constructs the behaviour).
- `CultureMarketplaceMaintenanceService`: the 9 `EnsureGuaranteedStock_*` and 11 `FilterForeignCultureItems_*` tests.
- `ITownRosterAdapter`: `ArmourStockSweepService` (`SweepTown`), `CultureMarketplaceBehavior`,
  `CultureMarketplaceMaintenanceService`, `TownRosterAdapter`, `ArmourStockSweepServiceTests`, both
  maintenance test classes, `ArmourAcquisitionCampaignBehavior.OnDailyTickSettlement`.
- `IAlignmentDesertionService`, `ICulturalFeatsService`: "No unique node match" (each has one
  implementation, listed above).

## Commands you will need

Prefix every dotnet command with the `TEMP="<tmp>" TMP="<tmp>"` your dispatch rules give.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 1` (only `EveryLanguage_DeclaresARowForEveryEnglishKey`), `Skipped: 2`, `Passed` = 12345 plus the new test results |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Binding gate | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | the base's result plus the new binding tests, which pass |
| Data | `python tools/validate_moduledata.py` | not required: no ModuleData change |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 (it exits 0 at the planned-at tree) |

Both MSBuild flags go on build AND test. Never `./build.ps1`: it deploys into the game install.
`dotnet test` builds `Main` and `TAOM.Tests` itself, so a filtered run is also the RED build check.

## Scope

**In scope** (the only files you modify or create):

Stage A
- `Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs`
- `Main/Features/CastleRecruitment/CastleRecruitmentSettingsProvider.cs`
- `Main/Features/AlignmentDesertion/AlignmentDesertionSettingsProvider.cs`
- `Main/Features/RealmBorders/RealmBordersSettingsProvider.cs`
- `Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs`
- `Main/Features/QuickActions/QuickActionsSettingsProvider.cs`
- `Main/Features/PartyIconScale/PartyIconScaleConfig.cs`
- `Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs`
- `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs` (new)

Stage B
- `Main/Features/CastleRecruitment/Hooks/Patch42_HourlyTickParty_Postfix.cs` (order only)
- `Main/Features/AlignmentDesertion/IAlignmentDesertionService.cs`, `Main/Features/AlignmentDesertion/AlignmentDesertionService.cs`
- `Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs` (one gate line)
- `Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs` (two gate lines)
- `Main/Features/CulturalFeats/ICulturalFeatsService.cs`, `Main/Features/CulturalFeats/CulturalFeatsService.cs`
- `Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs` (the helper and its one call)
- `TAOM.Tests/Features/CastleRecruitment/Patch42HourlyTickPartyPostfixTests.cs` (new)
- `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionServiceTests.cs`
- `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs` (new)
- `TAOM.Tests/Features/Refuge/RefugeCampaignBehaviorTests.cs`
- `TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs`
- `TAOM.Tests/Features/CulturalFeats/TaomPartySpeedModelTests.cs` (new)

Stage C
- `Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs`
- `Main/Adapters/ITownRosterAdapter.cs`, `Main/Adapters/TownRosterAdapter.cs`
- `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceMaintenanceServiceGuaranteedStockTests.cs` (stub arrangement only)
- `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceMaintenanceServiceCacheTests.cs` (new)
- `docs/features/culture-marketplace.md`

Stage D
- `Main/Features/CaravanTrade/CaravanDistanceHandoff.cs` (new)
- `Main/Features/CaravanTrade/Hooks/CaravanDistanceTranspiler.cs` (new)
- `Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs`
- `Main/Features/CaravanTrade/ICaravanTradeService.cs` (the one doc comment, line 48)
- `TAOM.Tests/Features/CaravanTrade/CaravanDistanceHandoffTests.cs` (new)
- `TAOM.Tests/Features/CaravanTrade/CaravanDistanceTranspilerTests.cs` (new)
- `TAOM.Tests/Features/CaravanTrade/CaravanTradeBindingTests.cs`
- `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs`
- `docs/features/caravan-trade.md`, `docs/reference/harmony-patch-registry.md` (the Patch59 section only)

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` (single-owner) and every `*IoC.cs`: no
  registration or lifetime changes are needed (the new hand-off is created by the patch class, the
  transpiler joins `Patch59_CaravanTrade` through its class attributes, and `TAOM.csproj` globs `*.cs`).
  If one seems needed, STOP and report the exact line.
- Protected files (`.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`,
  `docs/adrs/*.md`): nothing here needs them; there is no Step 0.
- `Main/Features/TaomSettings.cs`: any MCM default, any fallback (the "rename to change a default" trap
  makes defaults the maintainer's call).
- Diagnostics defaults: the `[MapLoad]` heartbeat, the AutoResolve battle log, EconomyDiagnostics (a
  maintainer decision, FOR-MIKE). The XML merge (plan 042). PatchShield's per-call cost on these targets
  (plan 034). Mission-side providers and per-frame mission work (plans 030 to 033). Map-frame profiling
  (plan 039).
- Consumers that get cheaper without edits: `CastleAiToggle.cs`, `CastleAiTranspiler.cs`,
  `RealmBorderService.cs`, `FieldCommissionBehavior.cs`, `InventorySearchCampaignBehavior.cs`,
  `TimeAccelerationMixin.cs`, `TimeAccelerationService.cs`, `CaravanTradeService.cs`,
  `CaravanVisitMemory*.cs`, `CultureMarketplaceBehavior.cs`, `CultureItemPoolService.cs`,
  `ArmourStockSweepService.cs`.
- `CultureFeatAdapter.FromOrNull`'s allocation per speed call; the `Settlement` parameters on
  `ICultureMarketplaceMaintenanceService` (a pre-existing ADR-007 gap); the unclamped MCM desertion rate
  (Maintenance notes).
- Any save-format change. `CHANGELOG.md`. `plans/README.md`.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an entry to a validator allowlist. STOP and report instead. Step 18
  changes how nine existing tests ARRANGE their fake (`GetItemCount` stubs become `GetItemCounts`
  stubs); every Act and Assert line in them stays byte-identical except the one `DidNotReceive` named
  there.

## Git workflow

- Commit on the branch you were given; never push or open a PR. Four commits, one per stage
  (Steps 6, 17, 20 and 25). Stage explicit paths only.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value="...">` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at `0912e1b7`; a
  hook refuses any other). Write the message to a file and run `git commit -F "<file>"`; never
  `--no-verify`.
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. No AI attribution trailer. Trailers you will use: `Not-tested:`, and `Rejected:` in Stage D.

## Steps

### Step 1: drift check and the base

Run the drift check from the top of this file. Then, before any edit:

1. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
2. The binding gate command from "Commands you will need".
3. `python tools/lint_docs.py --fail-on-drift`

**Verify**: the totals line reads `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` and the
one failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`; the lint exits 0. Write all three results
(the binding gate's totals and any failing or inconclusive names) into your report; they are your base.
A different dotnet failure set is a STOP condition.

### Step 2: read the conventions

Read `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`,
`TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs`,
`TAOM.Tests/Migration/IlCallScanner.cs` (`ExtractCalledMethods`), `docs/reviews/lessons/harmony-il.md`
(at least the four lessons named in "Conventions"), the Patch42 and Patch59 sections of
`docs/reference/harmony-patch-registry.md`, `Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs`
and `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs`.

**Verify**: `git grep -n "_settings ??= TaomSettings.Instance" -- Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`
prints one line. If it prints nothing, STOP (the exemplar changed).

Also run these two premise checks and STOP if either finds something:
- `git grep -n "new TaomSettings(" -- Main` prints nothing (no TAOM code replaces MCM's instance).
- `git grep -n -E "new (CaravanTrade|CastleRecruitment|AlignmentDesertion|RealmBorders|QuickActions|TimeAcceleration)SettingsProvider\(" -- Main`
  prints nothing, and `git grep -n "new FieldCommissionSettingsProvider(" -- Main` prints exactly the
  one line in `FieldCommissionIoC.cs` (inside the singleton `RegisterDelegate`).

### Step 3: RED, the IL rule for the eight campaign providers

Create `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`. Draft (re-check it compiles and
that every name it reflects on exists):

```csharp
using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.CaravanTrade;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.FieldCommission;
using TAOM.Features.PartyIconScale;
using TAOM.Features.QuickActions;
using TAOM.Features.RealmBorders;
using TAOM.Features.TimeAcceleration;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features;

/// <summary>
/// Campaign settings providers are read per party per hour, per caravan score, per party per day and
/// every map frame. Each takes the MCM settings reference once, in a private lazy <c>Settings</c>
/// accessor, and reads through it; no other member may resolve MCM's <c>Instance</c>, which walks MCM's
/// settings containers on every call. The accessor caches only a non-null instance, so a resolve before
/// MCM is up cannot pin the defaults. Pattern: BattleBalanceSettingsProvider (02157b18).
/// </summary>
[TestClass]
public class CampaignHotPathSettingsProvidersTests
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                                          | BindingFlags.Instance | BindingFlags.Static;

    // MCM declares Instance on a generic base (GlobalSettings<T>), so match any type TaomSettings derives from.
    private static bool IsInstanceGetter(MethodBase m) =>
        m.Name == "get_Instance" && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom(typeof(TaomSettings));

    private static bool CallsInstance(MethodBase m)
    {
        var il = m.GetMethodBody()?.GetILAsByteArray();
        return il != null && IlCallScanner.ExtractCalledMethods(m, il).Any(IsInstanceGetter);
    }

    [DataTestMethod]
    [DataRow(typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(FieldCommissionSettingsProvider))]
    [DataRow(typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(PartyIconScaleConfig))]
    [DataRow(typeof(TimeAccelerationSettingsProvider))]
    public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance(Type provider)
    {
        var accessor = provider.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            ?.GetGetMethod(true);
        Assert.IsNotNull(accessor, provider.Name + ": the private lazy Settings accessor");
        Assert.IsTrue(CallsInstance(accessor), provider.Name + ": the lazy accessor takes the settings reference");

        var offenders = provider.GetMethods(Declared).Cast<MethodBase>()
            .Concat(provider.GetConstructors(Declared))
            .Where(m => m.Name != "get_Settings" && CallsInstance(m))
            .Select(m => m.Name)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            provider.Name + " resolves the MCM instance outside the lazy accessor: " + string.Join(", ", offenders));
    }

    // A second (internal, test-only) constructor must leave exactly one public constructor, or DryIoc
    // cannot select one at registration. FieldCommission registers through a delegate and
    // PartyIconScaleConfig is static, so neither is a row here.
    [DataTestMethod]
    [DataRow(typeof(ICaravanTradeSettingsProvider), typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(ICastleRecruitmentSettingsProvider), typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(IAlignmentDesertionSettingsProvider), typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(IRealmBordersSettings), typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(IQuickActionsSettingsProvider), typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(ITimeAccelerationSettingsProvider), typeof(TimeAccelerationSettingsProvider))]
    public void Provider_ResolvesFromARealContainer(Type service, Type implementation)
    {
        using var container = new Container();
        foreach (var parameter in implementation.GetConstructors().Single().GetParameters())
            container.RegisterInstance(parameter.ParameterType,
                Substitute.For(new[] { parameter.ParameterType }, new object[0]));
        container.Register(service, implementation, Reuse.Singleton);

        Assert.IsInstanceOfType(container.Resolve(service), implementation);
    }
}
```

If DryIoc's non-generic `RegisterInstance(Type, object)` or `Register(Type, Type, IReuse)` overloads
differ in this DryIoc version, adapt the call shape (it is test code) and say so in your report; do not
drop the test.

Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CampaignHotPathSettingsProvidersTests"`.

**Verify**: the build succeeds; all eight `OnlyTheLazySettingsAccessor_ReadsTheMcmInstance` rows FAIL on
the `Assert.IsNotNull` message ending "the private lazy Settings accessor"; all six
`Provider_ResolvesFromARealContainer` rows PASS (they guard Step 5; they are not RED). Quote one failure
message per provider in your report.

### Step 4: RED, read-through tests and default pins

Add to the same file (a `using TAOM.Core.Logging;` is needed for `IModLogger`):

| Test | Arrange | Before the edit | Edit on `mcm` | After the edit |
|---|---|---|---|---|
| `CaravanTrade_ReadsThroughTheCachedSettings` | `var cfg = Substitute.For<ICaravanTradeConfigProvider>(); cfg.GetConfig().Returns(new CaravanTradeConfig()); var sut = new CaravanTradeSettingsProvider(cfg, mcm);` | `Enabled` true, `RangeMultiplier` 1.6f | `EnableCaravanTrade = false; CaravanRangeMultiplier = 2.5f;` | `Enabled` false, `RangeMultiplier` 2.5f |
| `CastleRecruitment_ReadsThroughTheCachedSettings` | config substitute returning `new CastleRecruitmentConfig()`; `new CastleRecruitmentSettingsProvider(cfg, mcm)` | `IsAiEnabled` true, `NotablesPerCastle` 3 | `EnableCastleRecruitmentAi = false; CastleNotablesPerCastle = 5;` | `IsAiEnabled` false, `NotablesPerCastle` 5 |
| `AlignmentDesertion_ReadsThroughTheCachedSettings` | config substitute returning `new AlignmentDesertionConfig()`; `new AlignmentDesertionSettingsProvider(cfg, mcm)` | `IsEnabled` true, `Rate` 0.5f | `EnableAlignmentDesertion = false; AlignmentDesertionRate = 0.25f;` | `IsEnabled` false, `Rate` 0.25f |
| `RealmBorders_ReadsThroughTheCachedSettings` | `new RealmBordersSettingsProvider(Substitute.For<IModLogger>(), mcm)` | `Enabled` true, `HeraldicBands` false | `EnableRealmBorders = false; RealmBordersHeraldicBands = true;` | `Enabled` false, `HeraldicBands` true |
| `FieldCommission_ReadsThroughTheCachedSettings` | `var inner = Substitute.For<IFieldCommissionConfigProvider>(); inner.GetConfig().Returns(new FieldCommissionConfig());` then `new FieldCommissionSettingsProvider(inner, mcm)` | `GetConfig().Enabled` true | `EnableFieldCommission = false;` | `GetConfig().Enabled` false |
| `QuickActions_ReadsThroughTheCachedSettings` | `new QuickActionsSettingsProvider(mcm)` | `EnableInventorySearch` true | `EnableInventorySearch = false;` | `EnableInventorySearch` false |
| `TimeAcceleration_ReadsThroughTheCachedSettings` | `new TimeAccelerationSettingsProvider(mcm)` | `FastForwardMultiplier` 4, `ExtraFastForwardMultiplier` 8, `CtrlSpaceMultiplier` 16 | `FastForwardMultiplier = 10; CtrlSpaceMultiplier = 32;` | 10, 10 (the floor at fast), 32 |

Each test builds `var mcm = new TaomSettings();` first, asserts every "before" value (they are the
compiled defaults listed in Current state, so a wrong getter fails), applies the edit, and asserts every
"after" value (floats with delta 0.0001f). Also add these pins (green before and after; they guard the
no-MCM path):

- `QuickActions_NoMcm_InventorySearchDefaultsOn`: `Assert.IsTrue(new QuickActionsSettingsProvider().EnableInventorySearch);`
- `PartyIconScale_NoMcm_GetScaleIsTheDefault`: `Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.GetScale(), 0.0001f);`
- `CastleRecruitment_NoMcm_TogglesFallBackToJson`: config substitute returning
  `new CastleRecruitmentConfig { Enabled = false, AiEnabled = false }` (check the property names in
  `CastleRecruitmentConfig.cs`; adapt if they differ), public constructor; assert `IsEnabled` and
  `IsAiEnabled` are false.

No read-through test for `PartyIconScaleConfig`: it is static, and an injection seam added only for a
test fails the simplicity criterion; the IL rule is its guard.

Run the `CampaignHotPathSettingsProvidersTests` filter.

**Verify**: the test project FAILS TO BUILD, and every error is a constructor-arity diagnostic (such as
CS1729) on the two-argument `CaravanTradeSettingsProvider`, `CastleRecruitmentSettingsProvider`,
`AlignmentDesertionSettingsProvider`, `RealmBordersSettingsProvider`, `FieldCommissionSettingsProvider`
constructions or the one-argument `QuickActionsSettingsProvider(mcm)` and `TimeAccelerationSettingsProvider(mcm)`.
Any other compile error is yours to fix before moving on.

### Step 5: GREEN, cache the eight providers

In each instance provider add, after its existing fields:

```csharp
    // Read on a campaign hot path (per party, per score, per day or every map frame). Resolving
    // TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its first
    // non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in place
    // (reset and presets copy values into it), so live MCM edits still apply. Lazy, not in the
    // constructor, so a resolve before MCM is up cannot pin the fallbacks. Same contract as
    // BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;
```

then replace every `TaomSettings.Instance` in that file's members with `Settings`, and add the test
constructor:

| File | Constructors after the edit |
|---|---|
| `CaravanTradeSettingsProvider.cs` | keep the public one; add `internal CaravanTradeSettingsProvider(ICaravanTradeConfigProvider configProvider, TaomSettings settings) : this(configProvider) => _settings = settings;` |
| `CastleRecruitmentSettingsProvider.cs` | keep the public one; add `internal CastleRecruitmentSettingsProvider(ICastleRecruitmentConfigProvider configProvider, TaomSettings settings) : this(configProvider) => _settings = settings;` |
| `AlignmentDesertionSettingsProvider.cs` | keep the public one; add `internal AlignmentDesertionSettingsProvider(IAlignmentDesertionConfigProvider configProvider, TaomSettings settings) : this(configProvider) => _settings = settings;` |
| `RealmBordersSettingsProvider.cs` | keep the public one; add `internal RealmBordersSettingsProvider(IModLogger logger, TaomSettings settings) : this(logger) => _settings = settings;` |
| `FieldCommissionSettingsProvider.cs` | keep the public one; add `internal FieldCommissionSettingsProvider(IFieldCommissionConfigProvider jsonProvider, TaomSettings settings) : this(jsonProvider) => _settings = settings;` |
| `QuickActionsSettingsProvider.cs` | add BOTH `public QuickActionsSettingsProvider() { }` and `internal QuickActionsSettingsProvider(TaomSettings settings) => _settings = settings;` |
| `TimeAccelerationSettingsProvider.cs` | add BOTH `public TimeAccelerationSettingsProvider() { }` and `internal TimeAccelerationSettingsProvider(TaomSettings settings) => _settings = settings;` |

Per-file specifics:

- `RealmBordersSettingsProvider.cs`: `ColourVersion` (`:122`) becomes `var settings = Settings;`; `Fade()`
  (`:224`) becomes `CheckedFade(Settings?.RealmBordersFadeStartDistance, Settings?.RealmBordersFullOpacityDistance);`.
  Nothing else changes: same `Report` warnings, same `Checked*` and `Choice` calls.
- `FieldCommissionSettingsProvider.cs`: `Capture()` (`:117-124`) becomes a private INSTANCE method that
  reads `var s = Settings;` once and passes `s?.EnableFieldCommission`, ..., `s?.EnableFieldCommissionDiagnostics`
  in the same order. Rewrite the class comment lines 18-20 to: "The MCM settings object is cached on
  its first non-null read and read through on every call, never snapshotted: MCM edits its one
  registered instance in place, which is what keeps the MCM properties honestly `RequireRestart = false`.
  The JSON file still needs a full application restart (the decorated provider is a `Lazy` singleton),
  but the knobs do not." (inside the existing `<c>`-style XML comment; no em dash). The comment on
  `Capture` (`:113-116`) keeps its meaning; adjust "Reads the MCM statics" to "Reads the cached MCM
  settings" if it no longer reads true.
- `PartyIconScaleConfig.cs` (static): add
  ```csharp
      // Called on every party-icon build. The MCM reference is cached on its first non-null read and read
      // through (BattleBalanceSettingsProvider pattern), so a slider change still applies on the next build.
      private static TaomSettings? _settings;
      private static TaomSettings? Settings => _settings ??= TaomSettings.Instance;
  ```
  and `public static float GetScale() => Resolve(Settings?.MapFigureScale);`. `GetScale` must stay a
  public, parameterless, `float`-returning static (Patch53's rewritten IL calls it). Update the
  `GetScale` doc comment if it says it reads the live value per call: it still does, through the cache.
- `CaravanTradeSettingsProvider.cs` and `CastleRecruitmentSettingsProvider.cs` and
  `AlignmentDesertionSettingsProvider.cs`: leave the class comments' `TaomSettings.Instance` mentions
  (lines 4, 7-8, 6-7) as they are unless a sentence becomes false; the `?? default` fallback is unchanged.

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- The `CampaignHotPathSettingsProvidersTests` filter passes with 0 failed and 24 results (8 + 6 data
  rows, 7 read-through tests, 3 pins; MSTest may count data rows differently, so check every name ran).
- The filters `RealmBordersProviderTests`, `FieldCommissionSettingsProviderTests`,
  `TimeAccelerationSettingsProviderTests`, `PartyIconScale`, `InventorySearchCampaignBehaviorTests` pass, 0 failed.
- `git grep -n "TaomSettings.Instance?" -- Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs Main/Features/CastleRecruitment/CastleRecruitmentSettingsProvider.cs Main/Features/AlignmentDesertion/AlignmentDesertionSettingsProvider.cs Main/Features/RealmBorders/RealmBordersSettingsProvider.cs Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs Main/Features/QuickActions/QuickActionsSettingsProvider.cs Main/Features/PartyIconScale/PartyIconScaleConfig.cs Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs`
  prints only comment lines (the IL rule is the authoritative check).

### Step 6: stage A full suite and commit

Run the full suite and the docs lint.

**Verify**: `Failed: 1` (the known one), `Skipped: 2`, `Passed` = 12345 plus this stage's new results;
lint exits 0. `git status --porcelain` lists exactly the nine Stage A paths (the test file as `??`). Stage
each by name. Subject draft: `perf(campaign): v2.0.32 - read campaign MCM settings once, not per call`.
Body draft:

```
Eight campaign settings providers looked up TAOM's Mod Options object
on every read. Each lookup walks MCM's settings containers, and these
reads run per party per hour (castle recruitment), per caravan
destination score, per party and garrison per day (alignment
desertion), on every map frame (realm borders, battlefield promotions,
the inventory search toggle, time acceleration) and per party icon
build (map figure scale). Each provider now keeps the settings object
from its first successful lookup and reads through it, so a change in
Mod Options still applies at once, and until MCM is up the providers
fall back to their defaults exactly as before. No setting, default or
log line changes.

Not-tested: in game; change Realm Borders, Battlefield Promotions,
the fast-forward multiplier and Map Figure Scale in Mod Options on
the campaign map and confirm each applies without a restart.
```

`git status --porcelain` is empty after the commit.

### Step 7: RED, the desertion pre-snapshot gate in the service

In `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionServiceTests.cs` (keep every test) add:

- `ShouldEvaluate_EveryToggleOn_SidedOwner_IsTrue`: `Assert.IsTrue(_service.ShouldEvaluate(EvilKingdom, false, false));`
- One test per gate, each false: `ShouldEvaluate_Disabled_IsFalse` (`IsEnabled` false),
  `ShouldEvaluate_PlayerOwner_ApplyToPlayerOff_IsFalse` (`(EvilKingdom, true, false)`),
  `ShouldEvaluate_AiOwner_ApplyToAiOff_IsFalse`, `ShouldEvaluate_Garrison_ApplyToGarrisonsOff_IsFalse`
  (`(EvilKingdom, false, true)`), `ShouldEvaluate_Party_ApplyToPartiesOff_IsFalse`,
  `ShouldEvaluate_NeutralOwner_IsFalse` (`NeutralKingdom`), `ShouldEvaluate_RateZero_IsFalse`.
- `ShouldEvaluate_NaNRate_IsTrue_ParityWithTheOriginalGate`: `_settings.Rate.Returns(float.NaN);`
  assert true, AND assert `_service.CalculateDesertion(EvilKingdom, false, false, Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20)))`
  returns one result with `DesertCount` 1 (today's behaviour: `(int)(20 * NaN)` is `int.MinValue`, the
  floor makes it 1). Comment: the inverted `rate <= 0f` gate is kept on purpose for parity; NaN is
  reachable only through a hand-edited MCM file.
- `ShouldEvaluate_False_ImpliesCalculateDesertionIsEmpty_ForEveryGate`: a loop over every combination of
  `IsEnabled`, `ApplyToAi`, `ApplyToPlayer`, `ApplyToParties`, `ApplyToGarrisons` (each true/false),
  `Rate` in {0f, 0.5f}, owner in {EvilKingdom, FreeKingdom, NeutralKingdom}, `isPlayerOwned` and
  `isGarrison` (each true/false); for each, with a roster holding one Free troop (`gondor`, 20) and one
  Evil troop (`mordor`, 10), assert `ShouldEvaluate(...) == (CalculateDesertion(...).Count > 0)`. This
  pins that the gate never hides a desertion and never lets through a roster that would shed nothing
  because of an owner-level gate. Set the `_settings` returns inside the loop.

Run the `AlignmentDesertionServiceTests` filter.

**Verify**: the test project fails to build; the errors are "does not contain a definition for
ShouldEvaluate" diagnostics (such as CS1061) and nothing else.

### Step 8: GREEN, `ShouldEvaluate`

`IAlignmentDesertionService.cs`, after `IsEnabled`:

```csharp
    /// <summary>
    /// False when no roster of this owner and location can lose a troop today (feature off, owner or
    /// location toggle off, a Neutral or kingdomless owner, a zero rate), so the behavior can skip the
    /// roster snapshot. True exactly when <see cref="CalculateDesertion"/> would look at the troops.
    /// </summary>
    bool ShouldEvaluate(string ownerKingdomId, bool isPlayerOwned, bool isGarrison);
```

`AlignmentDesertionService.cs`: extract the owner-level gates into one private method and use it from
both members, so the two cannot drift:

```csharp
    public bool ShouldEvaluate(string ownerKingdomId, bool isPlayerOwned, bool isGarrison)
        => TryGetPurge(ownerKingdomId, isPlayerOwned, isGarrison, out _, out _);

    public IReadOnlyList<TroopDesertionResult> CalculateDesertion(
        string ownerKingdomId, bool isPlayerOwned, bool isGarrison, IReadOnlyList<DesertionTroopInfo> troops)
    {
        var result = new List<TroopDesertionResult>();

        if (troops == null || troops.Count == 0)
            return result;
        if (!TryGetPurge(ownerKingdomId, isPlayerOwned, isGarrison, out var ownerSide, out var rate))
            return result;

        foreach (var troop in troops)
        { /* the existing loop, unchanged */ }

        return result;
    }

    /// <summary>The roster-independent gates, in the order CalculateDesertion always applied them.</summary>
    private bool TryGetPurge(string ownerKingdomId, bool isPlayerOwned, bool isGarrison, out FactionSide ownerSide, out float rate)
    {
        ownerSide = FactionSide.Neutral;
        rate = 0f;
        if (!_settings.IsEnabled) return false;
        // Owner gate (player / AI).
        if (isPlayerOwned && !_settings.ApplyToPlayer) return false;
        if (!isPlayerOwned && !_settings.ApplyToAi) return false;
        // Location gate (parties / garrisons).
        if (isGarrison && !_settings.ApplyToGarrisons) return false;
        if (!isGarrison && !_settings.ApplyToParties) return false;
        ownerSide = _alignment.GetKingdomSide(ownerKingdomId);
        // (keep the existing Neutral / mercenary comment here)
        if (ownerSide == FactionSide.Neutral) return false;
        rate = _settings.Rate;
        // (keep the existing "Rate 0 = no desertion" comment here)
        // Parity: written as !(rate <= 0f) on purpose, so a NaN rate proceeds exactly as the original
        // `if (rate <= 0f) return result;` let it (pinned by ShouldEvaluate_NaNRate_IsTrue_ParityWithTheOriginalGate).
        return !(rate <= 0f);
    }
```

The troops null/empty check now runs before the master toggle; both are side-effect-free reads, so no
result changes (all 21 existing tests prove it).

**Verify**: the `AlignmentDesertionServiceTests` filter passes, 0 failed, and the totals include the 21
existing tests plus the new ones.

### Step 9: RED, the behavior asks before it snapshots

Create `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs`, class
`[TestCategory("RequiresGame")]` (it resolves `TroopRoster` members), one test:

`ApplyDesertion_AsksTheServiceBeforeSnapshottingTheRoster_InIlOrder`: get
`typeof(AlignmentDesertionBehavior).GetMethod("ApplyDesertion", BindingFlags.NonPublic | BindingFlags.Instance)`
(assert not null), list `IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList()`,
find the index of the first call named `ShouldEvaluate` declared on `IAlignmentDesertionService` and the
first call named `GetTroopRoster`; assert both are found (messages name which) and that the first index
is lower. Name and comment it honestly: it pins call order in the IL of a straight-line method, not
control flow.

**Verify**: the filter `AlignmentDesertionBehaviorTests` builds and the test FAILS on the "ShouldEvaluate
is called" assertion.

### Step 10: GREEN, wire the behavior

`AlignmentDesertionBehavior.ApplyDesertion`, right after `if (roster == null) return;`:

```csharp
        // Owner-level gates first (feature, owner and location toggles, a sided owner, a positive rate):
        // a Neutral kingdom's parties and garrisons would build a snapshot every day only to shed nothing.
        if (!_service.ShouldEvaluate(kingdomId, isPlayerOwned, isGarrison))
            return;
```

Nothing else changes. The file stays under 150 lines (142 today, 146 after).

**Verify**: the `AlignmentDesertion` filter passes, 0 failed.

### Step 11: RED, Patch42 reads its toggles last

Create `TAOM.Tests/Features/CastleRecruitment/Patch42HourlyTickPartyPostfixTests.cs`, class
`[TestCategory("RequiresGame")]`, one test:

`Postfix_ReadsTheMcmTogglesAfterTheCastleFilter_InIlOrder`: take
`typeof(Patch42_HourlyTickParty_Postfix).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static)`,
list its calls with `IlCallScanner.ExtractCalledMethods`, find the first call named `get_IsCastle` and the
first call to `get_IsEnabled` or `get_IsAiEnabled` declared on `ICastleRecruitmentSettingsProvider`;
assert both are found and that the toggle index is higher than the `get_IsCastle` index.

**Verify**: the `Patch42HourlyTickPartyPostfixTests` filter builds and the test FAILS on the order
assertion (today the toggles come first).

### Step 12: GREEN, reorder Patch42's postfix

`Patch42_HourlyTickParty_Postfix.Postfix` becomes:

```csharp
    [HarmonyPostfix]
    public static void Postfix(RecruitmentCampaignBehavior __instance, MobileParty mobileParty)
    {
        if (_settings == null || _checkRecruiting == null)
            return;
        // Vanilla HourlyTickParty's own guards (line 293): AI lord/caravan parties only, not in a
        // map event, never the main party.
        if ((!mobileParty.IsCaravan && !mobileParty.IsLordParty) || mobileParty.MapEvent != null || mobileParty == MobileParty.MainParty)
            return;
        Settlement settlement = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(mobileParty);
        if (settlement == null || !settlement.IsCastle || settlement.IsUnderSiege)
            return;
        // The two MCM toggles last: this runs for every party every hour, and the plain reads above
        // reject every party that is not an AI party sitting in a castle.
        if (!_settings.IsEnabled || !_settings.IsAiEnabled)
            return;
        try
        {
            _checkRecruiting(__instance, mobileParty, settlement);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CastleRecruitment] CheckRecruiting failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
```

Keep the existing comment lines as they are (including "(line 293)"); change only the gate order.

**Verify**: the `Patch42HourlyTickPartyPostfixTests` and `CastleRecruitment` filters pass, 0 failed.

### Step 13: RED, refuge checks the book before the battle walk

In `TAOM.Tests/Features/Refuge/RefugeCampaignBehaviorTests.cs` (keep every test) add a helper that
invokes a private handler by name through reflection (as `InvokeOnGameLoaded` does, lines 51-57) and:

- `OnMapEventStarted_NoRefuge_ChecksTheBookFirst`: `_refuges.AllRefuges.Returns(new List<RefugeData>());`
  invoke `OnMapEventStarted` with `(null, null, null)`; assert `_ = _refuges.Received(1).AllRefuges;` and
  `_refuges.DidNotReceiveWithAnyArgs().OnMapEventStarted(default!);`.
- `OnMapEventEnded_NoRefuge_ChecksTheBookFirst`: the same for `OnMapEventEnded` with `(null)` and
  `OnMapEventEnded(default!)`.
- `OnMapEventStarted_WithARefuge_NullEvent_RalliesNothing` (a pin, green before and after):
  `AllRefuges` returns a list with one `new RefugeData { PartyId = "refuge_1" }`; invoke with nulls;
  assert `DidNotReceiveWithAnyArgs().OnMapEventStarted(default!)`.

Run the `RefugeCampaignBehaviorTests` filter.

**Verify**: the two `*_NoRefuge_ChecksTheBookFirst` tests FAIL on `Received(1)` (no call to
`AllRefuges` today); every other test in the class passes.

### Step 14: GREEN, the refuge early return

In `RefugeCampaignBehavior.OnMapEventStarted` and `OnMapEventEnded`, as the first statement:

```csharp
        // No refuge, nothing to rally or stand down: skip the walk over every party of every world
        // battle (RefugeService ignores ids it does not hold, so this changes no outcome).
        if (_refuges.AllRefuges.Count == 0)
            return;
```

**Verify**: the `RefugeCampaignBehaviorTests` filter passes, 0 failed.

### Step 15: RED, the speed model's roster walk

In `TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs` (keep every test), in the PartySpeed
region, add:

- `NeedsMountedCount_NullCulture_IsFalse`
- `NeedsMountedCount_CultureWithoutTheRohanFeat_IsFalse` (`AdapterWith()` with no feats)
- `NeedsMountedCount_CultureWithTheRohanFeat_IsTrue` (`AdapterWith(TaomCulturalFeats.RohanInfantrySpeedFeat)`)
- `ApplyRohanInfantryPenalty_WhenNotNeeded_ZeroCountsGiveTheSameSpeed`: for culture in
  {`null`, `AdapterWith()`, `AdapterWith(TaomCulturalFeats.RohanPlainSpeedFeat)`} and (mounted, total) in
  {(0, 10), (4, 10), (6, 10), (10, 10), (0, 0)}: assert `NeedsMountedCount(culture)` is false, then
  apply the penalty once with the real counts and once with (0, 0) to two `new ExplainedNumber(1f)` and
  assert equal `ResultNumber` (delta 0.0001f). This pins identical speeds for every party the skip
  affects.

Create `TAOM.Tests/Features/CulturalFeats/TaomPartySpeedModelTests.cs`, class `[TestCategory("RequiresGame")]`:

- `CountMountedAndTotal_NotNeeded_ReturnsZeroesWithoutReadingTheRoster`: get the private static
  `CountMountedAndTotal` by reflection (`BindingFlags.NonPublic | BindingFlags.Static`), invoke it with
  `new object?[] { null, false }`, assert the boxed result equals `(0, 0)` (a null roster proves it was
  not read).
- `CalculateFinalSpeed_AsksTheServiceBeforeCountingTheRoster_InIlOrder`: in the IL of
  `TaomPartySpeedModel.CalculateFinalSpeed`, the first call named `NeedsMountedCount` (declared on
  `ICulturalFeatsService`) comes before the call to `CountMountedAndTotal`; assert both are found.

Run the `CulturalFeatsServiceTests` and `TaomPartySpeedModelTests` filters.

**Verify**: the test project fails to build; the errors are a missing `NeedsMountedCount` member
(CS1061 or similar) and nothing else.

### Step 16: GREEN, walk the roster only for the Rohan feat

`ICulturalFeatsService.cs`, after `ApplyRohanInfantryPenalty`:

```csharp
    /// <summary>
    /// True when <see cref="ApplyRohanInfantryPenalty"/> can change the result for this culture, so the
    /// speed model walks the party roster only then (it runs on every speed recompute of every party).
    /// </summary>
    bool NeedsMountedCount(ICultureFeatAdapter? culture);
```

`CulturalFeatsService.cs`, beside `ApplyRohanInfantryPenalty`:

```csharp
    public bool NeedsMountedCount(ICultureFeatAdapter? culture)
        => culture != null && culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat);
```

`TaomPartySpeedModel.cs`: the call becomes
`var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster, _feats.NeedsMountedCount(culture));`
and the helper:

```csharp
    /// <summary>
    /// Boundary helper: collapses a sealed <see cref="TroopRoster"/> down to the two primitives
    /// <see cref="ICulturalFeatsService.ApplyRohanInfantryPenalty"/> needs, keeping the service free of
    /// TaleWorlds types per ADR-007. When the service says the penalty cannot apply, it returns (0, 0)
    /// without touching the roster; the penalty then returns at its own <c>totalCount &lt;= 0</c> gate,
    /// exactly as it returned at its feat gate before.
    /// </summary>
    private static (int mounted, int total) CountMountedAndTotal(TroopRoster roster, bool needed)
    {
        if (!needed)
            return (0, 0);
        int total = roster.TotalManCount;
        int mounted = 0;
        foreach (var element in roster.GetTroopRoster())
        {
            if (element.Character?.IsMounted == true)
                mounted += element.Number;
        }
        return (mounted, total);
    }
```

The override body gains no `if` (gamemodels.md rule 4); the branch sits in the boundary helper.

**Verify**: the `CulturalFeatsServiceTests` and `TaomPartySpeedModelTests` filters pass, 0 failed; the
four existing `ApplyRohanInfantryPenalty_*` tests still pass.

### Step 17: stage B full suite and commit

Run the full suite and the docs lint.

**Verify**: `Failed: 1` (the known one), `Skipped: 2`; lint exits 0. `git status --porcelain` lists
exactly the Stage B paths from Scope (three new test files as `??`). Stage each by name. Subject draft:
`perf(campaign): v2.0.32 - run cheap filters before per-party work`. Body draft:

```
Four campaign hooks now run their cheapest checks first. AI castle
recruitment reads its two Mod Options toggles only for an AI party
that is sitting in a castle, not for every party every hour.
Alignment desertion asks whether the owner can lose troops at all
(feature, owner and location toggles, a Free or Evil kingdom, a rate
above zero) before copying the roster, so the parties and garrisons
of Neutral kingdoms cost nothing each day. The refuge listener skips
the walk over every party in a world battle while the player has no
refuge. Party speed walks the roster for the Rohan infantry penalty
only for parties of a culture with that feat. Every recruitment,
desertion and speed is unchanged, and no log line changes.

Not-tested: in game; an AI lord recruits in a castle, a Free lord's
party sheds Evil troops overnight, a refuge rallies militia in a
battle, and a Rohan infantry party is still slowed.
```

### Step 18: RED, the marketplace caches and the single roster walk

**18a. Record the doc's test total first.** Run
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CultureMarketplace|FullyQualifiedName~ItemPoolAdapter"`
and write its total into your report (the doc says "Total: 117 tests across 10 classes").

**18b. Migrate the fake in the nine guaranteed-stock tests.** In
`CultureMarketplaceMaintenanceServiceGuaranteedStockTests.cs` add a helper:

```csharp
    // The service counts every guaranteed item in one roster walk (ITownRosterAdapter.GetItemCounts);
    // this stubs that walk from (id, count) pairs, 0 for any id not listed.
    private void StubCounts(params (string Id, int Count)[] counts)
    {
        var table = counts.ToDictionary(c => c.Id, c => c.Count);
        _townAdapter.GetItemCounts(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(ci => ((IReadOnlyList<string>)ci[1]).Select(id => table.TryGetValue(id, out var n) ? n : 0).ToArray());
    }
```

and replace each `_townAdapter.GetItemCount(null, "<id>").Returns(<n>);` line with one `StubCounts(...)`
call carrying the same pairs (in `EnsureGuaranteedStock_MultipleRoutedItems_TopsUpEachIndependently` the
three stubs become one call with three pairs). In `EnsureGuaranteedStock_MinStockZero_Skipped` the
assertion `_townAdapter.DidNotReceive().GetItemCount(Arg.Any<...Settlement>(), Arg.Any<string>());`
becomes `_townAdapter.DidNotReceive().GetItemCounts(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>(), Arg.Any<IReadOnlyList<string>>());`
(the same claim: no count query for a zero floor). Every other Act and Assert line stays byte-identical.
Add `using System.Linq;` if missing.

**18c. New tests.** Create `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceMaintenanceServiceCacheTests.cs`
(namespace `TAOM.Tests.Features.CultureMarketplace`; model the fixture on the two existing maintenance
test classes; `null` settlements):

- `EnsureGuaranteedStock_CountsEveryGuaranteedItemInOneWalk`: routed `a` (MinStock 1), `b` (MinStock 0),
  `c` (MinStock 2); counts a=0, c=1; assert `GetItemCounts` received exactly once with the list
  `["a", "c"]` (use `Arg.Is<IReadOnlyList<string>>(l => l.SequenceEqual(new[] { "a", "c" }))`), added 2
  (1 + 1), `AddItem(null, "a", 1)` and `AddItem(null, "c", 1)` received once each.
- `EnsureGuaranteedStock_SameTopUpsAsCountingEachItemAlone`: routed `a` (1), `b` (3), `c` (2); for every
  `have` triple in {0..3}^3 build a fresh service, stub counts, `AddItem` returning true, and compare
  the returned total and the `AddItem` calls received with an oracle written in the test as the old
  per-item loop (`need = MinStock - have` when `have < MinStock`). This pins identical stock decisions.
- `EnsureGuaranteedStock_ShortCountArray_TreatsMissingCountsAsZero`: `GetItemCounts` returns
  `new int[0]`; routed `a` (1); assert `AddItem(null, "a", 1)` received (a failed read counted 0 before).
- `RoutedItems_AreRequestedOncePerCulture`: call `EnsureGuaranteedStock(null, "isengard")` and
  `FilterForeignCultureItems(null, "isengard", 6)` three times each on ONE service (roster snapshot with
  one row so the filter reaches its lookups); assert `_poolService.Received(1).GetRoutedItemsForCulture("isengard")`.
- `FilterForeignCultureItems_PoolIdSetIsBuiltOncePerPool`: give `GetPool("isengard")` a `CultureItemPool`
  whose `Items` is a test-local `IReadOnlyList<ItemPoolEntry>` that counts indexer reads; reset the
  counter AFTER constructing the `CultureItemPool` (its constructor reads every item to sum the weights,
  `CultureItemPool.cs:16-18`); call the filter three times; assert the indexer was read `Items.Count`
  times in total (one build).
- `FilterForeignCultureItems_NewPoolObject_RebuildsTheSet`: `GetPool` returns pool P1 (holds `x`) on the
  first call and P2 (holds `y`) afterwards; roster row `y` (culture classified as foreign); assert the
  second call keeps `y` (it is in the current pool) while the first removed it.
- `FilterForeignCultureItems_TwoCulturesOnOneService_KeepTheirOwnSets`: one service, towns of
  `isengard` and `lindon` with different pools and routing; assert each town keeps only its own
  culture's pooled and routed items. Both calls pass a `null` settlement, so give
  `EnumerateRoster(null)` one return per call (`.Returns(isengardRows, lindonRows)`).
- `CountFailureLine_IsTheLiteralErrorLine`: `Assert.AreEqual("[CultureMarketplace] GetItemCounts('warg_brown','warg_dark' @ town_I1) failed: boom", TownRosterAdapter.CountFailureLine("town_I1", new[] { "warg_brown", "warg_dark" }, "boom"));`

Read `ItemPoolEntry` and `CultureItemPool` constructors before writing the pool fakes; if
`ClassifyEffectiveCulture` needs stubbing for the filter tests, copy how
`CultureMarketplaceMaintenanceServiceFilterTests.SetupRoster` does it.

Run the `CultureMarketplaceMaintenanceService` filter.

**Verify**: the test project fails to build; the errors are missing `GetItemCounts` and
`CountFailureLine` members only.

### Step 19: GREEN, one walk per town and per-culture sets

`ITownRosterAdapter.cs`: REPLACE `GetItemCount` (lines 13-15, with its doc comment) by:

```csharp
    /// <summary>For each id, the count <c>GetItemCount</c> used to return: the sum over every stack of
    /// that item, whatever its modifier, from ONE walk of the roster. 0 for a null or empty id, an id the
    /// object manager does not know, or a null settlement. The array has one entry per id, in order.</summary>
    int[] GetItemCounts(Settlement settlement, IReadOnlyList<string> itemIds);
```

`TownRosterAdapter.cs`: replace `GetItemCount` (lines 59-86) by:

```csharp
    public int[] GetItemCounts(Settlement settlement, IReadOnlyList<string> itemIds)
    {
        var counts = new int[itemIds?.Count ?? 0];
        if (settlement == null || counts.Length == 0) return counts;
        try
        {
            // Resolve each id once; the reference comparison below is the one GetItemCount made.
            var items = new ItemObject[counts.Length];
            for (var j = 0; j < counts.Length; j++)
                items[j] = string.IsNullOrEmpty(itemIds[j]) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemIds[j]);

            // One walk, summing every stack of each item (different ItemModifiers make separate stacks;
            // see the 2026-05-21 deep-review note this replaces).
            var roster = settlement.ItemRoster;
            for (var i = 0; i < roster.Count; i++)
            {
                var item = roster.GetItemAtIndex(i);
                for (var j = 0; j < items.Length; j++)
                {
                    if (items[j] != null && item == items[j])
                        counts[j] += roster.GetElementNumber(i);
                }
            }
            return counts;
        }
        catch (Exception ex)
        {
            _logger.LogError(CountFailureLine(settlement.StringId, itemIds, ex.Message));
            return new int[counts.Length];
        }
    }

    /// <summary>The one error line a failed count logs, naming every id it was asked for.</summary>
    internal static string CountFailureLine(string settlementId, IReadOnlyList<string> itemIds, string message)
        => $"[CultureMarketplace] GetItemCounts({string.Join(",", itemIds.Select(id => "'" + id + "'"))} @ {settlementId}) failed: {message}";
```

(Keep the modifier-split rationale from the old comment in one line, as above. Add `using System.Linq;`.)

`CultureMarketplaceMaintenanceService.cs`:

- Fields and the two cache helpers:

```csharp
    // Per-culture id sets, built on first use and kept for the process: the routing table is loaded once
    // (CultureMarketplaceConfigProvider.EnsureLoaded) and the pools are built once
    // (CultureItemPoolService.BuildPools returns early once built), so neither changes after the first
    // daily tick. A pool set is rebuilt if GetPool ever hands back a different pool object. Main thread
    // only (daily ticks and the new-game sweep). Not campaign state: no session reset needed.
    private readonly Dictionary<string, RoutedSets> _routed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (CultureItemPool Pool, HashSet<string> Ids)> _pooled = new(StringComparer.Ordinal);

    private sealed class RoutedSets
    {
        public RoutedItem[] Guaranteed = Array.Empty<RoutedItem>();    // MinStock > 0, routed order
        public string[] GuaranteedIds = Array.Empty<string>();
        public HashSet<string> Ids;                                    // null when nothing is routed here, as before
    }

    private RoutedSets RoutedFor(string cultureId)
    {
        if (_routed.TryGetValue(cultureId, out var sets)) return sets;
        var routed = _poolService.GetRoutedItemsForCulture(cultureId);
        sets = new RoutedSets();
        if (routed.Count > 0)
        {
            sets.Ids = new HashSet<string>(StringComparer.Ordinal);
            var guaranteed = new List<RoutedItem>();
            for (var i = 0; i < routed.Count; i++)
            {
                sets.Ids.Add(routed[i].ItemId);
                if (routed[i].MinStock > 0) guaranteed.Add(routed[i]);
            }
            sets.Guaranteed = guaranteed.ToArray();
            sets.GuaranteedIds = sets.Guaranteed.Select(e => e.ItemId).ToArray();
        }
        _routed[cultureId] = sets;
        return sets;
    }

    private HashSet<string> PooledIdsFor(string cultureId)
    {
        var pool = _poolService.GetPool(cultureId);
        if (pool == null || pool.Items.Count == 0) return null;
        if (_pooled.TryGetValue(cultureId, out var cached) && ReferenceEquals(cached.Pool, pool)) return cached.Ids;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < pool.Items.Count; i++)
            ids.Add(pool.Items[i].ItemId);
        _pooled[cultureId] = (pool, ids);
        return ids;
    }
```

- `EnsureGuaranteedStock` becomes (it still never calls `GetPool`):

```csharp
    public int EnsureGuaranteedStock(Settlement settlement, string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return 0;
        var routed = RoutedFor(cultureId);
        if (routed.Guaranteed.Length == 0) return 0;
        // One roster walk for every guaranteed item. Counting them all before any top-up gives the counts
        // the old per-item loop saw: adding one item changes no other item's count.
        var counts = _townAdapter.GetItemCounts(settlement, routed.GuaranteedIds);
        var totalAdded = 0;
        for (var i = 0; i < routed.Guaranteed.Length; i++)
        {
            var entry = routed.Guaranteed[i];
            var have = i < counts.Length ? counts[i] : 0;   // a failed read counted 0 before too
            if (have >= entry.MinStock) continue;
            var need = entry.MinStock - have;
            if (_townAdapter.AddItem(settlement, entry.ItemId, need))
                totalAdded += need;
        }
        return totalAdded;
    }
```

- `FilterForeignCultureItems`: replace lines 61-77 (the two set builds) with
  `var routedIdsHere = RoutedFor(cultureId).Ids;` and `var pooledHere = PooledIdsFor(cultureId);`, in
  that order, AFTER the `snapshot.Count == 0` return exactly where the builds were. The removal loop is
  unchanged.

Add `using System.Linq;` and `using TAOM.Features.CultureMarketplace.Domain;` if missing. Before you edit,
confirm the cache premises: `git grep -n "_pools\b\|_pools\[" -- Main/Features/CultureMarketplace/CultureItemPoolService.cs`
shows writes only inside `BuildPools`, and `git grep -n "_routing" -- Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs`
shows writes only inside `EnsureLoaded`. If either is written anywhere else, STOP.

**Verify**: `git grep -n "GetItemCount(" -- Main/Adapters/ITownRosterAdapter.cs Main/Adapters/TownRosterAdapter.cs Main/Features/CultureMarketplace TAOM.Tests/Features/CultureMarketplace`
prints nothing; the filters `CultureMarketplace` and `ArmourStockSweep` pass, 0 failed, and the nine
guaranteed-stock and eleven filter tests are among the passes.

### Step 20: stage C docs, full suite, commit

`docs/features/culture-marketplace.md` (find each line by its text):

- The component-diagram line `ITownRosterAdapter → Settlement.ItemRoster.AddToCounts / GetItemCount / RemoveItem / EnumerateRoster`:
  `GetItemCount` becomes `GetItemCounts`.
- The Key Files row for `Main/Adapters/ITownRosterAdapter.cs` + `TownRosterAdapter.cs`: in its list of
  exposed members, `GetItemCount` becomes `GetItemCounts`.
- "## Guaranteed Stock (min_stock)": "checks the town's current count of each routed item whose
  `Cultures` list includes the town's owner culture" becomes "counts, in one walk of the town's market,
  every routed item whose `Cultures` list includes the town's owner culture".
- "## Performance": add the bullet "- `CultureMarketplaceMaintenanceService` builds each culture's routed
  and pooled id sets once per process (the pools and the routing table never change after the first
  build) and counts a town's guaranteed items in one roster walk (`ITownRosterAdapter.GetItemCounts`). A
  failed walk logs one ERROR line naming every item, for example
  `[CultureMarketplace] GetItemCounts('warg_brown','warg_dark' @ town_I1) failed: <message>`, where the
  per-item count logged one line per item."
- "## Tests": add a bullet for `CultureMarketplaceMaintenanceServiceCacheTests.cs` with its test count
  (`grep -c "\[TestMethod\]"` on the file) and what it covers; then, ONLY if Step 18a's total was 117,
  run the same filter again and replace "Total: 117 tests across 10 classes" with the new total and
  "11 classes". If 18a's total was not 117, leave the line and report the mismatch.

Run the full suite and the docs lint.

**Verify**: `Failed: 1` (the known one), `Skipped: 2`; lint exits 0. `git status --porcelain` lists
exactly the six Stage C paths (the new test file as `??`). Subject draft:
`perf(marketplace): v2.0.32 - build culture id sets once per culture`. Body draft:

```
The culture marketplace's daily pass over every town rebuilt two id
sets covering a culture's whole item pool for each town, each day,
and walked the town's market once per guaranteed item. The sets are
now built once per culture, and a town's guaranteed items are counted
in one walk of its market. Stock decisions are unchanged. A failed
market read now logs one line naming every guaranteed item, where it
logged one line per item:
[CultureMarketplace] GetItemCounts('warg_brown','warg_dark' @ town_I1)
failed: <message>

Not-tested: in game; an Isengard town still stocks one of each warg
item and an Erebor town each ram item after a few days.
```

(A log example may exceed 72 columns only if it cannot be wrapped; here it is split across two lines.)

### Step 21: RED, the distance hand-off

Create `TAOM.Tests/Features/CaravanTrade/CaravanDistanceHandoffTests.cs` (untagged: the class takes
`object` keys and needs no engine type). Use `new object()` for parties and settlements. Tests:

- `TryTake_SameCallKeys_ReturnsTheRememberedValues`: `Remember(p, s, false, 1, 42.5f)`; `TryTake(p, s, false, out var nav, out var d)` is true, `nav` 1, `d` 42.5f.
- `TryTake_OtherParty_Misses`, `TryTake_OtherSettlement_Misses`, `TryTake_OtherPortFlag_Misses`.
- `TryTake_NothingRemembered_Misses`.
- `TryTake_Twice_SecondMisses` (a take always clears).
- `Clear_DropsTheRememberedValue`.
- `Remember_Overwrites_ThePreviousCall`.
- `TakeReportIfDue_FirstCall_StartsTheWindow`: returns null.
- `TakeReportIfDue_BeforeTheInterval_ReturnsNull`: start at tick 1000, one hit, call at 1000 + 299_999: null.
- `TakeReportIfDue_AfterTheInterval_IsTheLiteralLine`: start at 1000; two hits and one miss; call at
  1000 + 300_000: exactly
  `"[CaravanTrade] distance hand-off: reused=2 recomputed=1 over 300s (since start: reused=2 recomputed=1)"`.
- `TakeReportIfDue_ResetsTheWindowButKeepsTheTotals`: after the line above, one more hit, call at
  1000 + 600_000: `"[CaravanTrade] distance hand-off: reused=1 recomputed=0 over 300s (since start: reused=3 recomputed=1)"`.
- `TakeReportIfDue_NoTakesInTheWindow_ReturnsNull`.
- `TakeReportIfDue_TickCountWraps_StillReports`: start at `int.MaxValue - 1000`, one hit, call at
  `unchecked(int.MinValue + 299_001)` (elapsed 300_002 ms): a line beginning
  `"[CaravanTrade] distance hand-off: reused=1 recomputed=0 over 300s"`.
- `FormatMiss_IsTheLiteralLine`: `CaravanDistanceHandoff.FormatMiss("caravan_1", "town_A1", false)` equals
  `"[CaravanTrade] distance hand-off missed while bound (caravan=caravan_1, town=town_A1, port=False); recomputed the distance. Later misses are counted in the periodic hand-off line."`

The window starts on the first `TakeReportIfDue` call; tests call it once at the start tick before the
takes they count (state that in a comment).

**Verify**: the test project fails to build; the errors name the missing `CaravanDistanceHandoff` type
only.

### Step 22: GREEN, `CaravanDistanceHandoff`

Create `Main/Features/CaravanTrade/CaravanDistanceHandoff.cs`:

```csharp
using System;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Carries the travel distance vanilla's <c>CaravansCampaignBehavior.GetTradeScoreForTown</c> just
/// computed to TAOM's score postfix on the SAME call, so the postfix does not ask the engine for that
/// distance a second time. The Patch59 transpiler routes vanilla's one distance query through a
/// recorder that calls <see cref="Remember"/>; the postfix calls <see cref="TryTake"/> with the keys it
/// would have passed to the query. Keys compare by reference, and every take clears, so a value never
/// outlives the call that produced it. Also counts reused and recomputed distances for the periodic
/// taom_debug line. Engine-free (keys are objects) per ADR-007; one instance per thread (the patch holds
/// it in a [ThreadStatic] field), so it needs no lock.
/// </summary>
internal sealed class CaravanDistanceHandoff
{
    internal const int ReportIntervalMs = 300_000;

    private object? _party;
    private object? _settlement;
    private bool _isTargetingPort;
    private int _navigationType;
    private float _distance;

    private long _reused, _recomputed, _totalReused, _totalRecomputed;
    private bool _windowStarted;
    private int _windowStartTick;

    public void Remember(object party, object settlement, bool isTargetingPort, int navigationType, float distance)
    {
        _party = party;
        _settlement = settlement;
        _isTargetingPort = isTargetingPort;
        _navigationType = navigationType;
        _distance = distance;
    }

    public void Clear()
    {
        _party = null;
        _settlement = null;
    }

    public bool TryTake(object party, object settlement, bool isTargetingPort, out int navigationType, out float distance)
    {
        bool hit = _party != null && ReferenceEquals(_party, party) && ReferenceEquals(_settlement, settlement)
                   && _isTargetingPort == isTargetingPort;
        navigationType = hit ? _navigationType : 0;
        distance = hit ? _distance : 0f;
        Clear();
        if (hit) { _reused++; _totalReused++; }
        else { _recomputed++; _totalRecomputed++; }
        return hit;
    }

    /// <summary>The periodic INFO line once <see cref="ReportIntervalMs"/> has passed since the window
    /// began and the window saw a take; null otherwise. The first call only starts the window.
    /// <paramref name="nowTick"/> is <c>Environment.TickCount</c>; the unchecked difference survives its
    /// wrap.</summary>
    public string? TakeReportIfDue(int nowTick)
    {
        if (!_windowStarted)
        {
            _windowStarted = true;
            _windowStartTick = nowTick;
            return null;
        }
        int elapsed = unchecked(nowTick - _windowStartTick);
        if (elapsed < ReportIntervalMs || _reused + _recomputed == 0)
            return null;
        string line = $"[CaravanTrade] distance hand-off: reused={_reused} recomputed={_recomputed} over {elapsed / 1000}s (since start: reused={_totalReused} recomputed={_totalRecomputed})";
        _reused = 0;
        _recomputed = 0;
        _windowStartTick = nowTick;
        return line;
    }

    internal static string FormatMiss(string partyId, string townId, bool isTargetingPort)
        => $"[CaravanTrade] distance hand-off missed while bound (caravan={partyId}, town={townId}, port={isTargetingPort}); recomputed the distance. Later misses are counted in the periodic hand-off line.";
}
```

(Remove `using System;` if the compiler flags it unused.)

**Verify**: the `CaravanDistanceHandoffTests` filter passes, 0 failed, every test listed in Step 21 ran.

### Step 23: RED, the transpiler matcher and its engine binding

**23a.** Create `TAOM.Tests/Features/CaravanTrade/CaravanDistanceTranspilerTests.cs` (untagged; synthetic
instruction streams with test-local stub methods, the `PartyIconScaleTranspilerTests` shape). Stubs: a
static `void GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(object a, object b, bool c, out int d, out float e, out bool f)`
on the test class as the target, a same-named static method on a SECOND nested class (a decoy), and a
static `Recorder(...)` with the same signature as the replacement. Tests:

- `SwapDistanceCall_OneCall_SwapsTheOperandAndKeepsLabels`: a stream `[ldarg.1, call target, ret]` where
  the call carries a `Label`; after the swap `sites` is 1, the call's operand is the recorder, its
  opcode is `call`, and the label is still on it; the stream length is unchanged.
- `SwapDistanceCall_NoCall_LeavesTheStream` (`sites` 0, same operands).
- `SwapDistanceCall_TwoCalls_LeavesTheStream` (`sites` 2, both operands still the target).
- `SwapDistanceCall_SameNameOnAnotherType_IsNotASite` (only the decoy: `sites` 0).
- `SwapDistanceCall_UnresolvedTarget_LeavesTheStream` (`target` null: `sites` -1; `replacement` null: `sites` -1).
- `DescribeOutcome_Bound_IsTheLiteralInfoLine`: `CaravanDistanceTranspiler.DescribeOutcome(1)` equals
  `"[CaravanTrade] Patch59 distance hand-off bound: GetTradeScoreForTown's one AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty call now records its result for the score postfix."`
- `DescribeOutcome_NotBound_IsTheLiteralWarningLine`: `DescribeOutcome(2)` equals
  `"[CaravanTrade] Patch59 distance hand-off not bound: found 2 AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty calls in GetTradeScoreForTown, expected 1. Vanilla IL is unchanged; the score postfix queries the distance itself (one extra distance query per scored town)."`
- `DescribeOutcome_Unresolved_IsTheLiteralWarningLine`: `DescribeOutcome(-1)` equals
  `"[CaravanTrade] Patch59 distance hand-off not bound: the distance helper or the recorder did not resolve. Vanilla IL is unchanged; the score postfix queries the distance itself (one extra distance query per scored town)."`

**23b.** In `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` add (same attributes as its siblings:
`[TestCategory("RequiresGameIL")]`, `[TestCategory("BindingVerification")]`, the `_gameLoaded`
Inconclusive guard):

`CaravanDistanceHandoff_FindsTheOneDistanceCall_InInstalledEngine`: resolve
`AccessTools.Method(AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior"), "GetTradeScoreForTown")`
(assert not null), read `PatchProcessor.GetOriginalInstructions(target).ToList()`, run
`CaravanDistanceTranspiler.SwapDistanceCall(il, CaravanDistanceTranspiler.DistanceHelper(), CaravanDistanceTranspiler.Recorder(), out int sites)`,
assert `sites` is 1 (message: `CaravanDistanceTranspiler.DescribeOutcome(sites)`) and that exactly one
instruction in the result calls a method named `GetBestNavigationAndRemember`.

**23c.** In `TAOM.Tests/Features/CaravanTrade/CaravanTradeBindingTests.cs` add
`[TestCategory("BindingVerification")]` `Recorder_HasTheDistanceHelpersExactSignature`: with the class's
`RequireType` guard, assert `CaravanDistanceTranspiler.DistanceHelper()` and
`CaravanDistanceTranspiler.Recorder()` are not null, both are static, both return `void`, and their
parameter types are equal in order (a stack-identical swap needs this).

Run the `CaravanDistanceTranspilerTests`, `TranspilerSiteBindingTests` and `CaravanTradeBindingTests`
filters.

**Verify**: the test project fails to build; the errors name the missing `CaravanDistanceTranspiler` type
(and its members) only.

### Step 24: GREEN, the transpiler, the recorder and the postfix

Create `Main/Features/CaravanTrade/Hooks/CaravanDistanceTranspiler.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CaravanTrade.Hooks;

/// <summary>
/// IL surgery for the Patch59 distance hand-off. Vanilla's GetTradeScoreForTown makes exactly one
/// call to AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty; this swaps that
/// call's operand for <see cref="CaravansCampaignBehavior_GetTradeScoreForTown_Patch.GetBestNavigationAndRemember"/>,
/// which has the same signature (a stack-identical swap; labels stay on the instruction). Any other
/// count, or a lookup that does not resolve, leaves the stream unchanged: the postfix then queries the
/// distance itself, as it always did. Harmony re-runs transpilers on the ORIGINAL IL whenever the
/// method's patch set changes, so this must be idempotent on original IL, which a single swap is.
/// </summary>
internal static class CaravanDistanceTranspiler
{
    internal static MethodInfo? DistanceHelper() => AccessTools.Method(
        typeof(AiHelper),
        nameof(AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty),
        new[]
        {
            typeof(MobileParty), typeof(Settlement), typeof(bool),
            typeof(MobileParty.NavigationType).MakeByRefType(), typeof(float).MakeByRefType(), typeof(bool).MakeByRefType(),
        });

    internal static MethodInfo? Recorder() => AccessTools.Method(
        typeof(CaravansCampaignBehavior_GetTradeScoreForTown_Patch),
        nameof(CaravansCampaignBehavior_GetTradeScoreForTown_Patch.GetBestNavigationAndRemember));

    /// <summary>Swaps the one call; <paramref name="sites"/> is the number of calls found, or -1 when
    /// <paramref name="target"/> or <paramref name="replacement"/> is null. Only sites == 1 swaps.</summary>
    internal static List<CodeInstruction> SwapDistanceCall(
        IEnumerable<CodeInstruction> instructions, MethodInfo? target, MethodInfo? replacement, out int sites)
    {
        var list = new List<CodeInstruction>(instructions);
        if (target == null || replacement == null)
        {
            sites = -1;
            return list;
        }

        int found = -1;
        sites = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if ((list[i].opcode == OpCodes.Call || list[i].opcode == OpCodes.Callvirt)
                && list[i].operand is MethodInfo mi && mi.Name == target.Name && mi.DeclaringType == target.DeclaringType)
            {
                sites++;
                found = i;
            }
        }

        if (sites == 1)
        {
            list[found].opcode = OpCodes.Call;
            list[found].operand = replacement;
        }
        return list;
    }

    internal static string DescribeOutcome(int sites) => sites switch
    {
        1 => "[CaravanTrade] Patch59 distance hand-off bound: GetTradeScoreForTown's one AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty call now records its result for the score postfix.",
        -1 => "[CaravanTrade] Patch59 distance hand-off not bound: the distance helper or the recorder did not resolve. Vanilla IL is unchanged; the score postfix queries the distance itself (one extra distance query per scored town).",
        _ => $"[CaravanTrade] Patch59 distance hand-off not bound: found {sites} AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty calls in GetTradeScoreForTown, expected 1. Vanilla IL is unchanged; the score postfix queries the distance itself (one extra distance query per scored town).",
    };
}
```

Edit `CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs`:

1. Update the class summary: the postfix now reuses the distance vanilla just computed (handed off by
   the transpiler) and queries it itself only when no hand-off matches. Keep the rest of its meaning.
   No em dash in the new text.
2. Add fields: `private static IModLogger? _logger;`, `private static bool? _loggedBound;`,
   `private static bool _missWarned;`, `internal static bool HandoffBound { get; private set; }`, and
   ```csharp
       [ThreadStatic] private static CaravanDistanceHandoff? t_handoff;
       private static CaravanDistanceHandoff Handoff => t_handoff ??= new CaravanDistanceHandoff();
   ```
3. Add the transpiler (it must never throw: an exception here would fail the whole Patch59 category):
   ```csharp
       [HarmonyTranspiler]
       public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
       {
           var original = new List<CodeInstruction>(instructions);
           try
           {
               var result = CaravanDistanceTranspiler.SwapDistanceCall(
                   original, CaravanDistanceTranspiler.DistanceHelper(), CaravanDistanceTranspiler.Recorder(), out int sites);
               HandoffBound = sites == 1;
               LogOutcomeOnce(sites);
               return result;
           }
           catch (Exception ex)
           {
               HandoffBound = false;
               Logger()?.LogWarning($"[CaravanTrade] Patch59 distance hand-off not bound: {ex.GetType().Name}: {ex.Message}. Vanilla IL is unchanged; the score postfix queries the distance itself.");
               return original;
           }
       }

       // Harmony re-runs every transpiler when the method's patch set changes (PatchShield's second pass
       // adds a finalizer here), so the outcome is logged when it first appears or changes, not per run.
       private static void LogOutcomeOnce(int sites)
       {
           if (_loggedBound == HandoffBound) return;
           _loggedBound = HandoffBound;
           var line = CaravanDistanceTranspiler.DescribeOutcome(sites);
           if (HandoffBound) Logger()?.LogInfo(line); else Logger()?.LogWarning(line);
       }

       private static IModLogger? Logger()
       {
           try { return _logger ??= IoC.Resolve<IModLogger>(); }
           catch (Exception) { return null; }
       }
   ```
4. Add the recorder (public static, exact `AiHelper` signature; no Harmony attribute):
   ```csharp
       /// <summary>Called from GetTradeScoreForTown's IL in place of vanilla's one AiHelper distance query
       /// (CaravanDistanceTranspiler): runs that same query with the same arguments, then records the
       /// result for the postfix. An exception propagates exactly as vanilla's would.</summary>
       public static void GetBestNavigationAndRemember(MobileParty mobileParty, Settlement settlement, bool isTargetingPort,
           out MobileParty.NavigationType bestNavigationType, out float bestNavigationDistance, out bool isFromPort)
       {
           AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
               mobileParty, settlement, isTargetingPort, out bestNavigationType, out bestNavigationDistance, out isFromPort);
           Handoff.Remember(mobileParty, settlement, isTargetingPort, (int)bestNavigationType, bestNavigationDistance);
       }
   ```
5. The postfix becomes (only the marked parts change):
   ```csharp
       [HarmonyPostfix]
       public static void Postfix(ref float __result, MobileParty caravanParty, Town town)
       {
           var handoff = Handoff;
           // Positive-requirement gate: vanilla rejections (-1) and any NaN pass through untouched.
           // A recorded distance belongs to this call only, so every early return drops it.
           if (!(__result > 0f) || caravanParty == null || town?.Settlement == null)
           {
               handoff.Clear();
               return;
           }

           try
           {
               _service ??= IoC.Resolve<ICaravanTradeService>();
               _memory ??= IoC.Resolve<ICaravanVisitMemory>();

               bool isNaval = caravanParty.HasNavalNavigationCapability;
               // Vanilla's own distance for this caravan and town, recorded a moment ago by the
               // transpiled query; asked again only when no record matches.
               if (!handoff.TryTake(caravanParty, town.Settlement, isNaval, out int nav, out float navDistance))
               {
                   WarnFirstMissWhileBound(caravanParty, town, isNaval);
                   AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                       caravanParty, town.Settlement, isNaval, out var navType, out navDistance, out _);
                   nav = (int)navType;
               }
               var report = handoff.TakeReportIfDue(Environment.TickCount);
               if (report != null) Logger()?.LogInfo(report);
               if ((MobileParty.NavigationType)nav == MobileParty.NavigationType.None) return;

               // ... from `float speed = isNaval` to `__result = _service.ReweightTradeScore(...)`: unchanged ...
           }
           catch (Exception)
           {
               // Degrade gracefully to the vanilla score.
           }
       }

       private static void WarnFirstMissWhileBound(MobileParty caravanParty, Town town, bool isNaval)
       {
           if (!HandoffBound || _missWarned) return;
           _missWarned = true;
           Logger()?.LogWarning(CaravanDistanceHandoff.FormatMiss(caravanParty.StringId, town.Settlement.StringId, isNaval));
       }
   ```

Add the usings the file needs (`System.Collections.Generic`, `TAOM.Core.Logging`). Keep the file under
150 lines (`wc -l`); if it is over, move `LogOutcomeOnce`, `Logger` and `WarnFirstMissWhileBound` into
`CaravanDistanceTranspiler.cs` as internal statics rather than trimming comments that carry rationale.

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- The `CaravanDistanceTranspilerTests`, `CaravanDistanceHandoffTests`, `CaravanTrade` and
  `TranspilerSiteBindingTests` filters pass, 0 failed; `CaravanDistanceHandoff_FindsTheOneDistanceCall_InInstalledEngine`
  PASSED (not Inconclusive; if it is Inconclusive, the game assemblies did not load: report it, it is
  an environment gap).
- `HarmonyPatchBindingTests` and `CoopVetoClassificationTests` filters pass, 0 failed.
- If the site-binding test reports `sites` other than 1, STOP (Stage D's premise is false on this
  engine); Stages A to C stand on their own.

### Step 25: stage D docs, full suite, binding gate, commit

Docs (find each line by its text, quoted in Current state):

- `docs/features/caravan-trade.md`, lever 2's row: replace "Recomputes raw travel days from the same
  public inputs vanilla used (`AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` + caravan-speed props)"
  with "Takes the raw travel distance vanilla's own score just computed (a Patch59 transpiler records
  vanilla's `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` result; the
  postfix asks the engine itself only when no record matches) and converts it to days with the
  caravan-speed props".
- Same file, "## Performance": replace "and recomputes the distance via `AiHelper`; this was reviewed
  twice (deep-review + Codex) and **verified cache-backed**" through the end of that sentence's
  parenthesis with: "and reuses the distance vanilla computed for the same caravan and town a moment
  earlier, through `CaravanDistanceHandoff` and the Patch59 transpiler (2026-10, plan 037). The query it
  replaces was reviewed twice (deep-review + Codex) and verified cache-backed: `AiHelper` →
  `DistanceHelper` → `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement)` serves from the
  settlement distance cache plus a couple of `Vec2.Distance` ops. It still ran once per candidate town
  per destination think for 302+ caravans, so it is now skipped; when the hand-off is unavailable the
  postfix makes the same query, cache-backed and terrain-accurate." (Use a plain arrow `->` if the doc
  lint flags the Unicode arrow; check what the existing line uses and keep it.)
- Same file: add a section "## Log lines" before "## Known limitations / playtest items", listing the
  four Stage D lines with their fields and an example each: the bound header (INFO, once per outcome),
  the not-bound header (WARNING, the two variants), the first miss while bound (WARNING, once per
  process: caravan id, town id, port flag), the periodic line (INFO, at most every 300 s real time:
  reused and recomputed in the window, the window in seconds, totals since start). Copy the exact
  strings from Steps 21 and 23.
- Same file, "## Tests": add bullets for `CaravanDistanceHandoffTests.cs`, `CaravanDistanceTranspilerTests.cs`,
  the new `CaravanTradeBindingTests` test and the `TranspilerSiteBindingTests` row.
- `docs/reference/harmony-patch-registry.md`, "## Patch59_CaravanTrade": the Target line becomes
  "... `.CalculateBudgetFactor` (all private, Postfix ×4) + a Transpiler on `.GetTradeScoreForTown`
  (the distance hand-off)", and append to the description: "The `GetTradeScoreForTown` transpiler swaps
  vanilla's one `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` call for a
  same-signature recorder so the postfix reuses vanilla's distance; any other site count leaves the IL
  unchanged and logs why, and the postfix then queries the distance itself (`CaravanDistanceTranspiler`,
  pinned by `TranspilerSiteBindingTests`)." Keep the existing `×` character as it is in that line.
- `Main/Features/CaravanTrade/ICaravanTradeService.cs:48`: the `days` param doc becomes "Raw travel time
  in days (vanilla's <c>num</c>), from the distance vanilla computed for this caravan and town."

Run the full suite, the binding gate and the docs lint.

**Verify**: `Failed: 1` (the known one), `Skipped: 2`; the binding gate shows the base's result plus the
new binding tests, all passing; lint exits 0. `git status --porcelain` lists exactly the ten Stage D
paths (the four new files as `??`). Stage each by name. Subject draft:
`perf(caravans): v2.0.32 - reuse vanilla's distance in trade scoring`. Body draft:

```
Caravan trade scoring asked the engine for each candidate town's
travel distance a second time, right after vanilla's own scoring had
asked the same question for the same caravan and town. A transpiler
now records vanilla's answer as vanilla computes it, and the score
postfix reuses it. When no record matches (the transpiler did not
bind on this engine, or another mod skipped vanilla's scoring) the
postfix asks the engine itself, as before. Destination choices are
unchanged.

New taom_debug.log lines: one line when Patch59 binds or fails to
bind the hand-off, one warning the first time a bound hand-off
misses, and an INFO line at most every five minutes counting the
distances reused and recomputed.

Rejected: caching distances per caravan and town for one destination
search; a search scores each town once, and its second pass runs only
when the first scored nothing, so such a cache never hits.
Not-tested: in game; start a campaign, check taom_debug.log for the
bound line, and after five minutes of fast-forward a periodic line
with recomputed=0.
```

### Step 26: final checks

1. Run every Done criterion below and paste each output into your report.
2. The reference-assembly unit step (rule: tests touching engine types). On this machine, in a shell
   where `BANNERLORD_GAME_DIR` and `BANNERLORD_OVERRIDE_DIR` are unset, from the repository root:
   ```
   dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=
   dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"
   ```
   Expected: the build succeeds; the failures are at most `EveryLanguage_DeclaresARowForEveryEnglishKey`,
   `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches` (trunk
   CI's known three). A NEW test name failing there needs the `RequiresGame` tag it is missing: STOP and
   report it. If the RefAsm restore cannot download its reference assemblies (no network), report "not
   run (environment)" with the error; do not STOP. This step writes only build output.

## Test plan

- `CampaignHotPathSettingsProvidersTests`: the IL rule over eight providers (RED against every old
  provider: no accessor), six DryIoc resolve rows (a guard: one public constructor survives), seven
  read-through tests on a cached settings object (RED by compile), three no-MCM pins.
- `AlignmentDesertionServiceTests`: seven gate tests, a NaN-parity pin, and an exhaustive gate-matrix
  equivalence test against `CalculateDesertion` (RED by compile).
- IL-order tests (RequiresGame; they pin call order in straight-line IL, not control flow):
  `AlignmentDesertionBehaviorTests`, `Patch42HourlyTickPartyPostfixTests`, `TaomPartySpeedModelTests`
  (plus a behavioural test that the speed helper never reads the roster when not needed).
- `RefugeCampaignBehaviorTests`: two "checks the book first" tests (RED) and a pin.
- `CulturalFeatsServiceTests`: three `NeedsMountedCount` tests and a speed-parity matrix.
- `CultureMarketplaceMaintenanceServiceCacheTests`: one-walk counting, an exhaustive top-up parity
  oracle, the short-array guard, once-per-culture routing, once-per-pool sets, a pool-swap rebuild, two
  cultures on one service, and the literal error line. The nine guaranteed-stock tests keep their
  assertions with the fake migrated.
- `CaravanDistanceHandoffTests` (keys, clearing, the periodic line literally, tick wrap, the miss line),
  `CaravanDistanceTranspilerTests` (one, zero, two, decoy, unresolved, three literal outcome lines),
  `TranspilerSiteBindingTests` (the real engine IL has exactly one site), `CaravanTradeBindingTests`
  (the recorder's signature equals the helper's).
- Patterns to model: `TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs`,
  `TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs`,
  `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs`,
  `TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs` (`CalledNames`).
- Not testable in a unit test (the `Not-tested:` trailers): the live MCM round trip, the Harmony-applied
  postfixes and transpiler in game, `TownRosterAdapter.GetItemCounts` against a real roster, the speed
  model and the behaviours' engine I/O.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows `Failed: 1` (only
      `EveryLanguage_DeclaresARowForEveryEnglishKey`), `Skipped: 2`, and `Passed` equal to 12345 plus
      the number of new test results, all of which pass
- [ ] The binding gate shows Step 1's result plus the new binding tests, which pass
- [ ] `git grep -n "TaomSettings.Instance?" -- Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs Main/Features/CastleRecruitment/CastleRecruitmentSettingsProvider.cs Main/Features/AlignmentDesertion/AlignmentDesertionSettingsProvider.cs Main/Features/RealmBorders/RealmBordersSettingsProvider.cs Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs Main/Features/QuickActions/QuickActionsSettingsProvider.cs Main/Features/PartyIconScale/PartyIconScaleConfig.cs Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs`
      prints only comment lines, and `CampaignHotPathSettingsProvidersTests` passes (the authoritative check)
- [ ] `git grep -n "GetItemCount(" -- Main/Adapters/ITownRosterAdapter.cs Main/Adapters/TownRosterAdapter.cs Main/Features/CultureMarketplace TAOM.Tests/Features/CultureMarketplace docs/features/culture-marketplace.md`
      prints nothing
- [ ] `git grep -n -e "recomputes the distance via" -e "Recomputes raw travel days from the same public inputs" -e "recomputed from the same public inputs" -- docs Main`
      prints nothing outside `docs/reviews/` (review records are history, not claims)
- [ ] `git diff --stat 0912e1b7..HEAD -- Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj Main/Features/TaomSettings.cs ':(glob)Main/**/*IoC.cs' .claude/settings.json Directory.Build.props docs/adrs`
      prints nothing
- [ ] `wc -l Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs Main/Features/CastleRecruitment/Hooks/Patch42_HourlyTickParty_Postfix.cs`
      shows each under 150, except `RefugeCampaignBehavior.cs`, which was 218 before this plan and may
      grow by at most 8 lines (a pre-existing ADR-002 overrun this plan does not fix)
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `git log --oneline <base>..HEAD`, where `<base>` is the commit your dispatch names as your starting
      point, shows exactly four commits, each subject at most 72 characters with the SubModule.xml
      version; `git status --porcelain` lists nothing
- [ ] Every comment, doc line, log string and test oracle this plan supplied was re-checked against the
      code it describes (say so in the report, naming any you changed)

## STOP conditions

Stop and report (do not improvise) if:

- The code at a "Current state" location does not match its excerpt.
- A step's verification fails twice after a reasonable fix.
- Any pre-existing test fails after a change. In particular a failing `CaravanTradeServiceTests`,
  `AlignmentDesertionServiceTests`, `CulturalFeatsServiceTests`, guaranteed-stock or filter test,
  `RefugeCampaignBehaviorTests`, `FieldCommissionSettingsProviderTests`, `RealmBordersProviderTests` or
  `TimeAccelerationSettingsProviderTests` means an AI decision, a price, a stock result, a speed, a
  desertion or a setting changed: that is forbidden here.
- A parity test this plan adds (the desertion gate matrix, the speed parity matrix, the top-up oracle)
  fails after the GREEN step: the change alters a decision. Do not adjust the oracle.
- A provider is constructed per call or registered with a lifetime other than `Reuse.Singleton`, or TAOM
  code replaces MCM's registered `TaomSettings` instance (Step 2's premise checks): the cache would then
  buy nothing or serve a stale object. Report the location.
- A provider turns out to be resolved and cached in a way that would pin a null (for example a consumer
  that copies a getter's value into a static at load): the lazy accessor alone would not then preserve
  today's behaviour. Report the consumer.
- An existing test arranges `GetRoutedItemsForCulture` or `GetPool` differently across two calls on ONE
  service instance and relies on the second answer: the per-culture cache changes its oracle. Report it;
  do not edit the test's assertion.
- `CultureItemPoolService` or `CultureMarketplaceConfigProvider` writes its pools or routing table
  outside `BuildPools` or `EnsureLoaded` (Step 19's premise greps).
- The installed engine's `GetTradeScoreForTown` has a number of `AiHelper` distance calls other than 1,
  or the recorder's signature differs from the helper's (Steps 23-24): stop Stage D; Stages A to C stand.
- Any reordered operand turns out to have a side effect (a getter that logs, counts or writes).
- The fix seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, any `*IoC.cs`
  registration or lifetime change, `Main/Features/TaomSettings.cs`, or a protected file.
- A TaleWorlds signature differs from "Current state" (`AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty`,
  `CaravansCampaignBehavior.GetTradeScoreForTown`, `RecruitmentCampaignBehavior.HourlyTickParty`,
  `CultureObject.HasFeat`): report the mismatch, do not decompile and improvise.
- The assumption "within one `GetTradeScoreForTown` call, nothing between vanilla's distance query and
  TAOM's postfix changes the caravan's or the town's position or navigation state" turns out false (for
  example a TAOM or engine patch that moves parties from inside that method): the hand-off would then
  differ from a recompute.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (the brief names none) and give the executor its number for the report.
- `/localize`: none; this plan adds no player-facing text (log lines are not localized).
- Feature docs: the doc edits in Steps 20 and 25 are in scope; no new feature doc or feature-map row.
- Stage D revisits a reviewed decision (`docs/features/caravan-trade.md` "## Performance";
  `docs/reviews/rca-caravan-trade-2026-07-04.md` finding 2 kept the second distance query as cheap). Put
  the trade-off to the maintainer before merging Stage D's commit; it is the last commit, so dropping it
  leaves Stages A to C intact.
- Review: `/deep-review` on the four commits before any merge, with the probes in Maintenance notes.
- A lesson, if the review finds one: `docs/reviews/lessons/harmony-il.md` (the hand-off) or
  `docs/reviews/lessons/` for the subsystem concerned.

## After merge: the maintainer's actions

Pull. In game, once (the `Not-tested:` lines): change Realm Borders, Battlefield Promotions, the
fast-forward multipliers and Map Figure Scale in Mod Options on the campaign map and see each apply
without a restart; let an AI lord recruit in a castle; check a Free lord's party sheds Evil troops
overnight; check taom_debug.log for the Patch59 hand-off bound line and, after five minutes of
fast-forward, a periodic hand-off line with `recomputed=0`. On a slower machine, plan 039's map-frame
profiler is the measurement.

## Maintenance notes

- A new campaign settings provider read per party, per frame or per day starts in the lazy-accessor
  shape and is added as a row to both data tests in `CampaignHotPathSettingsProvidersTests`.
- The read-through contract rests on MCM editing its registered instance in place. An MCM upgrade that
  swaps instances on reset or preset load would freeze every cached provider; the read-through tests
  build their own settings objects and cannot catch that. Re-check `SettingsUtils.OverrideValues` and
  the containers' `LoadedSettings` after an MCM bump. While MCM is not up, the lazy accessors resolve on
  every read exactly as before (no regression, no gain).
- `CaravanDistanceHandoff` is per thread and keyed by reference; a take always clears, and every early
  return of the postfix clears. The one theoretical stale case: vanilla's score body throws after the
  recorder ran (the postfix is skipped), AND a later call for the SAME caravan and town has its
  original skipped by another mod's prefix; the postfix would then reuse the earlier distance. Both
  conditions together are not known to occur.
- Review probes: the `??=` accessors on any worker thread (idempotent, no lock needed; confirm nothing
  else in a provider is mutable, `RealmBordersSettingsProvider.ColourVersion` excepted, which is
  unchanged); the Patch42 order (every moved operand side-effect-free); `TryGetPurge`'s NaN parity and
  the gate matrix; that `CountMountedAndTotal(…, false)` returns before `TotalManCount`; the marketplace
  caches' validity (process-lifetime inputs, pool reference check, `EnsureGuaranteedStock` never calling
  `GetPool`); the transpiler's idempotence on original IL, its never-throw wrapper, the once-per-outcome
  header, and the recorder's exact signature.
- Considered and rejected under `simplicity-criterion.md`:
  - **Cache `ItemObject`s across ticks in `TownRosterAdapter`.** `MBObjectManager.Init()` builds a new
    manager per game and `Destroy()` nulls it, so a cache would need invalidation keyed on the manager;
    the single walk already resolves each guaranteed id once per town tick (at most 10). Tiny win, real
    staleness risk.
  - **A per-(caravan, town) distance cache for one destination search** (the brief's fallback): it never
    hits (Current state, Stage D engine facts).
- Deferred, not this plan: the MCM `AlignmentDesertionRate` is not clamped or NaN-checked
  (`AlignmentDesertionSettingsProvider.cs:21`), so a hand-edited NaN deserts one troop per opposed type;
  a default-and-validation question for its own change. `RefugeCampaignBehavior.cs` is over ADR-002's
  150 lines (218) before this plan. `CultureFeatAdapter.FromOrNull` allocates per speed recompute.
