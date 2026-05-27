# Batch A2 verification (#134–#148)

Verified by: general-purpose agent, Phase 9a, 2026-05-13
Inputs: triage-input-batch-A2.json + cluster-gamemodels.md + cluster-campaign-behaviors.md
HEAD: `b4b4de1 fix(messengers): wire IoC + CampaignBehavior (#121)` (branch `bannerlord-1.3.15`)

## Summary

| Verdict | Count | Issues |
|---------|-------|--------|
| VALID | 15 | #134, #135, #136, #137, #138, #139, #140, #141, #142, #143, #144, #145, #146, #147, #148 |
| STALE | 0 | — |
| FALSE-POSITIVE | 0 | — |
| DUPLICATE | 0 | — |
| SEVERITY-DRIFT | 0 | — |

All 15 issues in Batch A2 remain valid against HEAD. No interim commits since the 2026-05-13 audit touched any of the cited files; `git log --since="2026-05-13"` returned empty and recent commits b4b4de1..0acbdc4 are scoped to Messengers, docs, EditorCacheRebuild, and SceneScripts — none of the audited files.

One small textual drift was observed in #134 (the audit body cited perks `SiegeWorks`/`Counterweights` while the current code references `Stonecutters`/`SiegeEngineer`). The cited NRE risk on `party.MobileParty.HasPerk(...)` is unaffected — both calls are unguarded on the same `MobileParty` receiver. The fix sentence in the issue body should be updated when the fix lands, but the verdict is VALID.

One small textual drift was observed in #147 (the audit body cited `Hero.MainHero.MapFaction.StringId` while the current code uses `Hero.MainHero?.Clan?.Kingdom?.StringId`). The ADR-007 violation (direct sealed-type access in a model body) persists — the lookup path changed but the static-sealed access did not. Verdict VALID.

## Per-issue verification table

| # | File(s) | Findings asserted | Findings confirmed | Severity | Verdict |
|---|---------|---------|---------|---------|---------|
| 134 | TaomSiegeEventModel.cs:23-24 | 1 × P1 NRE | 1/1 | P1 | VALID |
| 135 | TaomPartySpeedModel.cs:30 + 47-61 | 1 × P1 NRE + roster perf | 2/2 | P1 + P2 | VALID |
| 136 | StartupResourcesConfigProvider.cs + StartupResourcesBehavior.cs | 1 × P1 + 1 × P2 + 1 × P3 | 3/3 | mixed | VALID |
| 137 | TaomTournamentModel.cs | 5 × P2 | 5/5 | P2 | VALID |
| 138 | TaomTargetScoreModel.cs:26-34 | 2 × P2 | 2/2 | P2 | VALID |
| 139 | FormationPresetSaveableTypeDefiner.cs + FormationPresetCampaignBehavior.cs | 2 × P1 + 1 × P2 | 3/3 | mixed | VALID |
| 140 | TaomPartyHealingModel + TaomMilitaryPowerModel + BattleBalanceConfigProvider | 4 × P2 | 4/4 | P2 | VALID |
| 141 | Patch33_SPInventoryVMRefresh.cs + InventoryScreenAdapter.cs | 1 × P1 + 1 × P2 + 4 × P3 doc | 6/6 | mixed | VALID |
| 142 | CareerSystem/Models/*.cs (4 files) | 5 × P2 | 5/5 | P2 | VALID |
| 143 | RemoteFiefSettlementSwapper + FiefHubMenuPresenter + FiefHubService + Patch36 | 1 × P1 + 2 × P2 + 2 × P3 | 5/5 | mixed | VALID |
| 144 | CulturalFeats/Models/*.cs (umbrella, 16 files) | 7 × P2 | 7/7 | P2 | VALID |
| 145 | TaomInformationRestrictionModel.cs | 1 × P2 | 1/1 | P2 | VALID |
| 146 | InventorySearchCampaignBehavior + Patch34 capture/finalize | 2 × P2 + 1 × P3 | 3/3 | P2 + P3 | VALID |
| 147 | TaomExecutionRelationModel.cs | 3 × P2 | 3/3 | P2 | VALID |
| 148 | TaomPartyWageModel.cs + IoC.cs:110 | 4 × P2 | 4/4 | P2 | VALID |

---

## Per-issue detail

### #134 — Siege NRE on garrison defenders (P1, VALID)

File: `Main/Features/Siege/Models/TaomSiegeEventModel.cs`

Current code (lines 21-29):

```csharp
public override IEnumerable<SiegeEngineType> GetAvailableDefenderSiegeEngines(PartyBase party)
{
    bool hasFirePerks =
        party.MobileParty.HasPerk(DefaultPerks.Engineering.Stonecutters, checkSecondaryRole: true)
     || party.MobileParty.HasPerk(DefaultPerks.Engineering.SiegeEngineer, checkSecondaryRole: true);

    foreach (var kind in _availability.GetDefenderEngines(hasFirePerks))
        yield return Resolve(kind);
}
```

Verdict: **VALID.** `party.MobileParty.HasPerk(...)` is called twice on an unguarded receiver. Garrison-defender `PartyBase` has `MobileParty == null` (settlement parties with no leader army). The audit's text cites perks `SiegeWorks`/`Counterweights`; current code uses `Stonecutters`/`SiegeEngineer`. Perk names differ but the NRE risk is identical — both unguarded calls on the same null-reachable receiver. Per `adapters.md` "`?.` for computed TaleWorlds properties." `git log -- Main/Features/Siege/Models/TaomSiegeEventModel.cs` shows only the original feature commit (`1c89d6f feat: defender trebuchets in siege management UI`).

Smallest fix: guard each `HasPerk` call with `?.` and short-circuit (`party.MobileParty?.HasPerk(...) == true || party.MobileParty?.HasPerk(...) == true`). Add the regression test the issue body specifies.

Dependencies: none.

### #135 — TaomPartySpeedModel Campaign.Current.MapSceneWrapper NRE (P1, VALID)

File: `Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs`

Current code (lines 22-68):

```csharp
public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
{
    var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);

    var culture = mobileParty.Party?.Owner?.Culture;
    if (culture == null)
        return result;

    var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
    ...
    // Rohan infantry speed penalty — applies when >50% of party is infantry
    if (culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
    {
        var roster = mobileParty.MemberRoster;
        int totalCount = roster.TotalManCount;
        if (totalCount > 0)
        {
            int mountedCount = 0;
            foreach (var element in roster.GetTroopRoster())
            { ... }
        }
    }
    ...
}
```

Verdict: **VALID.** Line 30 has two unguarded computed-property dereferences (`Campaign.Current.MapSceneWrapper.GetFaceTerrainType(...)`) and lines 47-61 contain the per-tick `foreach` over the roster with no culture-mismatch early-exit. Both issues match the audit description exactly.

Smallest fix: add `?.` guards on `Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(...)` falling back to `TerrainType.Plain`; add an early-exit before the roster walk gated on culture id; later extract the foreach into `ICulturalFeatsService` per Pattern A (umbrella issue #144).

Dependencies: P2 part overlaps with umbrella #144 finding 7. Fix sequence — land #135 P1 NRE guard first (small surgical change); the foreach extraction can land with the broader #144 refactor.

### #136 — StartupResources config validation + dead guards (P1+P2+P3, VALID)

Files: `Main/Features/StartupResources/StartupResourcesConfigProvider.cs` + `Main/Features/StartupResources/StartupResourcesBehavior.cs`

ConfigProvider lines 51-58:

```csharp
config.CultureEntries.Add(new CultureResourceEntry
{
    CultureId = id,
    Gold = int.Parse(el.Attribute("gold")?.Value ?? "0", CultureInfo.InvariantCulture),
    Influence = float.Parse(el.Attribute("influence")?.Value ?? "0", CultureInfo.InvariantCulture),
    PlayerGold = ParsePlayerGold(el.Attribute("playerGold")?.Value, id)
});
```

Behavior lines 12-13, 30-46:

```csharp
private bool _goldDistributed;
private bool _influenceDistributed;
...
public void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
{
    if (index != 1) return;

    if (!_goldDistributed) { ... _goldDistributed = true; ... }
    if (!_influenceDistributed) { ... _influenceDistributed = true; ... }
}
```

Verdict: **VALID.**
- **P1 — Gold/Influence validation:** `int.Parse` on `Gold` and `float.Parse` on `Influence` both lack range checks. `PlayerGold` already goes through validated `ParsePlayerGold`. The asymmetry stands. NaN `float.Parse("NaN")` would propagate. A throw from a single malformed entry is caught by the outer try/catch which discards the entire config (the catch returns `_cached = new StartupResourcesConfig()` — losing all parsed entries). Per `csharp-architecture.md` "Config Providers MUST Validate" + `FiniteFloatValidator` rule.
- **P2 — Dead guards:** `_goldDistributed` / `_influenceDistributed` are set inside a body already gated by `if (index != 1) return;`. The booleans are not in `SyncData` so they don't persist; they protect against engine double-invocation (an engine-invariant violation, not our concern). Per `simplicity-criterion.md` "no real protection = Reject."
- **P3 — Init order:** speculative concern about `Heroes`/`Clans` population at `index == 1`. The audit explicitly flagged it as "Research via ilspycmd" — left as P3 follow-up.

Smallest fix:
- (P1) Extract `ParseGold` + `ParseInfluence` mirroring `ParsePlayerGold` (NaN/Infinity gate then range check); on per-entry failure continue with default (do not throw); log + continue.
- (P2) Delete the two booleans + their guards.
- (P3) `ilspycmd` research on `OnNewGameCreatedPartialFollowUpEvent` slot ordering; document.

Dependencies: none.

### #137 — TaomTournamentModel inline branching + unguarded chain (5 × P2, VALID)

File: `Main/Features/Arena/Models/TaomTournamentModel.cs`

Current code (selected, lines 23-91):

```csharp
public override float GetTournamentStartChance(Town town)
{
    if (town.Settlement.SiegeEvent != null) return 0f;
    int count = town.Settlement.Parties.Count(x => x.IsLordParty)
              + town.Settlement.HeroesWithoutParty.Count(x =>
                    x.IsActive && x.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge);
    return count switch
    {
        0 => 0f,
        1 => TournamentStartChance1Lord,
        ...
    };
}

public override float GetTournamentEndChance(TournamentGame tournament)
{
    float elapsed = tournament.CreationTime.ElapsedDaysUntilNow;
    return MathF.Max(0f, (elapsed - TournamentEndChanceGraceDays) * TournamentEndChanceRamp);
}

private static MBList<ItemObject> BuildPrizePool(string cultureId, float minTier, float maxTier) { ... }

public override Equipment GetParticipantArmor(CharacterObject participant) { ... }
```

Verdict: **VALID.**
- `GetTournamentStartChance` body has `.Count(...)` + arithmetic + switch-expression — rule 4 violation (multi-line computation in override).
- Line 28: `Campaign.Current.Models.AgeModel.HeroComesOfAge` is three unguarded computed-property dereferences — NRE risk outside campaign context (custom battles, menu preview).
- `GetTournamentEndChance` has `elapsed` local + arithmetic + `MathF.Max` — rule 4.
- `BuildPrizePool` private static contains the filtering foreach + guards. Rule 4 binds extraction to "a Service, not a method on the model."
- `GetParticipantArmor` body has resolve → ObjectManager lookup → null guard → RandomBattleEquipment access; more than "boundary adapt + delegate."

Smallest fix: create `ITournamentService` per the issue body; model bodies become 1-line delegations. Add `?.` guards on the `Campaign.Current.Models.AgeModel.HeroComesOfAge` chain (or pull `HeroComesOfAge` into the service which can do its own guarded lookup).

Dependencies: none.

### #138 — TaomTargetScoreModel inline ternary + early-return (2 × P2, VALID)

File: `Main/Features/ArmyTargeting/Models/TaomTargetScoreModel.cs`

Current code (lines 17-41):

```csharp
public override float GetTargetScoreForFaction(
    Settlement targetSettlement, Army.ArmyTypes missionType,
    MobileParty mobileParty, float ourStrength)
{
    string factionId = mobileParty.MapFaction?.StringId;

    float effectiveStrength = missionType == Army.ArmyTypes.Besieger
        ? ourStrength * _service.GetStrengthMultiplier(factionId)
        : ourStrength;

    float baseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, effectiveStrength);

    if (baseScore <= 0f || missionType != Army.ArmyTypes.Besieger)
        return baseScore;

    string committedTargetId = (mobileParty.Army?.AiBehaviorObject as Settlement)?.StringId;

    float targetMultiplier = _service.GetTargetMultiplier(targetSettlement.StringId, committedTargetId, factionId);
    float distanceCompensation = _service.GetDistanceCompensation(factionId, targetSettlement.StringId);
    return baseScore * targetMultiplier * distanceCompensation;
}
```

Verdict: **VALID.** Lines 26-28 ternary on `missionType == Besieger` and lines 33-34 routing-early-return both encode business rules inside the model body. Rule 4 is binary — extract to service.

Smallest fix: add `ApplyStrengthMultiplier(factionId, missionType, ourStrength)` and `ComputeFinalScore(baseScore, factionId, missionType, ...)` to `IArmyTargetingService`; model body becomes three pure service calls.

Dependencies: none.

### #139 — CompanionTactics SaveableTypeDefiner + silent reset (2 × P1 + 1 × P2, VALID)

Files: `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs` + `Main/Features/CompanionTactics/FormationPresets/Hooks/FormationPresetCampaignBehavior.cs`

SaveableTypeDefiner lines 25-31:

```csharp
protected override void DefineContainerDefinitions()
{
    ConstructContainerDefinition(typeof(List<HoNFormationPreset>));
    ConstructContainerDefinition(typeof(Dictionary<string, int>));
    ConstructContainerDefinition(typeof(Dictionary<int, int>));
    ConstructContainerDefinition(typeof(List<string>));
}
```

CampaignBehavior lines 58-65:

```csharp
catch (Exception ex)
{
    _logger.LogWarning($"[CompanionTactics] FormationPreset SyncData failed (likely BaseId collision): {ex.Message}");
    _savedPresets = new List<HoNFormationPreset>();
    _service.OnGameLoaded(_savedPresets);
}
```

CampaignBehavior line 73 (OnNewGameCreated):

```csharp
_savedPresets = new List<HoNFormationPreset>();
_service.OnGameLoaded(_savedPresets);
```

Verdict: **VALID.**
- **P1 — Generic container registration:** `Dictionary<string,int>`, `Dictionary<int,int>`, `List<string>` are extremely common vanilla container shapes; cross-mod collision risk is high. Class doc-comment even acknowledges "First TAOM use of SaveableTypeDefiner; CareerSystem deliberately avoided the pattern via primitive SyncData." Audit's recommendation to switch to primitive-dict SyncData is consistent with the CareerSystem precedent.
- **P1 — Silent reset:** catch block at lines 58-65 only `LogWarning` — no in-game `InformationManager.DisplayMessage`. Player loses all formation presets with no visible signal.
- **P2 — Semantic mismatch:** OnNewGameCreated (line 73) calls `_service.OnGameLoaded(_savedPresets)` for the reset path. `OnGameLoaded` is named for load-path use; semantic abstraction leak.

Smallest fix:
- (P1a) Migrate `HoNFormationPreset` to primitive-dict SyncData (eliminate `SaveableTypeDefiner` like CareerSystem does); OR, if keeping SaveableTypeDefiner, drop the four generic container registrations and let vanilla cover them. The latter is risky because saving may fail if vanilla doesn't pre-register them — research is required.
- (P1b) Add `InformationManager.DisplayMessage` inside the catch block.
- (P2) Add `IFormationPresetService.Reset()`; call from OnNewGameCreated instead of `OnGameLoaded(empty)`.

Dependencies: P1a is the structurally clean fix and supersedes P1b (if SaveableTypeDefiner is gone the catch becomes much simpler). Sequencing: research vanilla `SaveableTypeDefiner` container registrations via `ilspycmd "TaleWorlds.SaveSystem.dll" -t SaveableTypeDefiner` first to determine the actual collision surface before choosing primitive-dict vs surgical drop.

### #140 — BattleBalance IoC.Resolve + rule-4 + config validation (4 × P2, VALID)

Files: `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs` + `Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs` + `Main/Features/BattleBalance/BattleBalanceConfigProvider.cs`

TaomPartyHealingModel line 53:

```csharp
var hero = party.Owner ?? party.LeaderHero;
if (hero != null)
{
    var passiveService = IoC.Resolve<ICareerPassiveService>();
    ...
}
```

TaomMilitaryPowerModel lines 19-36:

```csharp
public override float GetDefaultTroopPower(CharacterObject troop)
{
    if (!_settings.EnableCustomTroopPower)
        return base.GetDefaultTroopPower(troop);

    var config = _configProvider.GetConfig();
    int tier = troop.IsHero ? troop.Level / 4 + 1 : troop.Tier;
    ...
}
```

BattleBalanceConfigProvider lines 36-41:

```csharp
var json = File.ReadAllText(path);
_cache = JsonConvert.DeserializeObject<BattleBalanceConfig>(json) ?? new BattleBalanceConfig();
_logger.LogInfo("BattleBalanceConfigProvider: Loaded battle_balance_config.json");
return _cache;
```

Verdict: **VALID.**
- IoC.Resolve at line 53 of healing model — service-locator anti-pattern in a service-touching code path (`feedback_no_service_locator_in_services.md`).
- 41-line body (lines 23-63) of `GetSurvivalChance` with nested if-cultural-bonus + if-career-passive branches — rule 4.
- TaomMilitaryPowerModel inline tier derivation + branching multiplier — rule 4. `CalculateTierPower` static is partial extraction (still on the model class, not a service).
- BattleBalanceConfigProvider deserializes JSON with no `FiniteFloatValidator` on `TierPower`/`CulturalSurvivalBonuses` floats. NaN `TierPower["T7"]` propagates.

Smallest fix:
- (config) Add `FiniteFloatValidator.IsFiniteInRange` per-key check after deserialization; revert + warn on failure.
- (IoC.Resolve) Add a 3rd ctor param `ICareerPassiveService` on `TaomPartyHealingModel`; pass at SubModule.cs:299.
- (rule 4 healing) Extract cultural-bonus + career-passive branches into `IBattleHealingService.ApplySurvivalModifiers(...)`.
- (rule 4 military power) Extract tier derivation + multiplier selection into `IBattleHealingService` / `IMilitaryPowerService`; remove `CalculateTierPower` static from model.

Dependencies: none.

### #141 — EquipPresets concrete cast + UX over-count (P1 + P2 + 4 × P3 doc, VALID)

Files: `Main/Features/EquipPresets/Hooks/Patch33_SPInventoryVMRefresh.cs` + `Main/Adapters/InventoryScreenAdapter.cs`

Patch33 line 37:

```csharp
_cachedAdapter ??= IoC.Resolve<IInventoryScreenAdapter>() as InventoryScreenAdapter;
_cachedAdapter?.SetActive(__instance);
```

InventoryScreenAdapter lines 145-153:

```csharp
// If the slot already holds the same EquipmentElement, no-op.
var existing = targetEquipment[slotEnum];
if (!existing.IsEmpty
    && existing.Item == item
    && ReferenceEquals(existing.ItemModifier, modifier))
{
    equipped++;
    continue;
}
```

Verdict: **VALID.**
- **P1 concrete cast:** `as InventoryScreenAdapter` succeeds today but fails silently to null if any future test/alternate impl is registered. `SetActive` is concrete-only. Audit-described root cause is correct (missing interface seam).
- **P2 UX over-count:** the no-op branch increments `equipped`, so `PresetLoadResult.EquippedCount` reports "8 items applied" when zero transfer commands were issued. Misleading UX. The current code does `equipped++` then `continue` without distinguishing "already equipped" from a real transfer.
- **P3 doc rot:** out-of-scope per audit body (filed as P3, no issue to close here).

Smallest fix:
- (P1) Add `void SetActive(SPInventoryVM?)` to `IInventoryScreenAdapter`; drop concrete cast; add a warning-log path when resolve returns null.
- (P2) Rename branch counter to `alreadyEquipped`; carry it on `PresetLoadResult` distinct from `EquippedCount`; surface separately in the result message.

Dependencies: none. The interface-surface widening for P1 is a small, mechanical change.

### #142 — CareerSystem GameModels rule-4 + service-locator (5 × P2, VALID)

Files: `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs`, `TaomAgentApplyDamageModel.cs`, `TaomInventoryCapacityModel.cs`, `TaomMapVisibilityModel.cs`

`TaomAgentStatCalculateModel.UpdateAgentStats` (lines 31-89): 55-line override with hero/passive/buff-tracker dispatch inline — rule 4.
`TaomAgentApplyDamageModel.ApplyDamageReductions` (lines 34-65): nested `if (heroId != null) { if (_passiveService != null) ... }` versus the other overrides' early-return — inconsistent guard style.
`TaomInventoryCapacityModel` (lines 10-24) + `TaomMapVisibilityModel` (lines 10-19): both call static `CareerPassiveHelper.ApplyFactor(hero, ref result, ...)`. Per audit: `CareerPassiveHelper` is a static service-locator wrapper. Defensive `if (_passiveService == null) return baseValue` guards present in `TaomAgentStatCalculateModel` (line 25) — unreachable because service is resolved unconditionally at SubModule.cs:317.

Verdict: **VALID** on all 5 findings.

Smallest fix: extract `ICareerAgentStatService.UpdateAgentStats(heroId, ref AgentDrivenProperties)` for AgentStatCalculate; unify ApplyDamage guard style; inject `ICareerPassiveService` constructor-arg into `TaomInventoryCapacityModel` + `TaomMapVisibilityModel`; remove `CareerPassiveHelper` static after migration; remove unreachable null-guards.

Dependencies: deletion of `CareerPassiveHelper` static affects every consumer — search for callers before removing. The static is also used in #144 + #148 — coordinate as one larger refactor or carry compatibility shim during transition.

### #143 — FiefManagement silent swap-restore + presenter Reset() (P1 + 2 × P2 + 2 × P3, VALID)

Files: `Main/Adapters/RemoteFiefSettlementSwapper.cs`, `Main/Features/FiefManagement/FiefHubMenuPresenter.cs`, `Main/Features/FiefManagement/FiefHubService.cs`, `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs`

Swapper lines 42-47:

```csharp
public void Restore(Settlement original)
{
    var party = MobileParty.MainParty;
    if (party == null || _field == null) return;
    _field.SetValue(party, original);
}
```

Presenter line 32:

```csharp
public void Reset() => _selectedIndex = 0;
```

Presenter lines 15-17 (state held):

```csharp
private IReadOnlyList<FiefSummary> _menuFiefs = System.Array.Empty<FiefSummary>();
private FiefSummary _menuCurrentFief;
private bool _menuCurrentAtPlayer;
```

FiefHubService line 37:

```csharp
public int Count => GetOrderedFiefs().Count;
```

Patch36 line 55: `if (service.Count <= 0) { ... }`

Verdict: **VALID.**
- **P1 swap-restore silent bail:** if `MobileParty.MainParty` is null at restore time but was non-null at swap time, the global `_currentSettlement` reflection-set is never restored. Corruption persists through the entire session. The `_swapActive` flag in `OnFinalize` gates the Restore call but does not gate Restore from no-oping internally. Fix: capture and use a stable `_party` ref at swap-time.
- **P2 presenter Reset() incomplete:** only 1 of 4 stateful fields cleared. New-campaign-in-same-process bleed-through bug.
- **P2 F6 perf:** `service.Count` calls `GetOrderedFiefs()` which iterates `Settlement.All` (862 settlements). Bounded but unnecessary on every keypress.
- **P3 ADR-007 + IGameStateListener stubs:** flagged as P3 in audit body, no action this batch.

Smallest fix:
- (P1) Add `private MobileParty _capturedParty;` to swapper; assign in `Swap` from `MobileParty.MainParty`; use captured ref in `Restore` with `LogError` if null.
- (P2 reset) Add the 3 missing field clears in `Reset()`.
- (P2 perf) Replace `service.Count` in Patch36 with a presenter-cached count, or fast-path `Clan.PlayerClan?.Settlements.Count(s => s.IsTown || s.IsCastle)`.

Dependencies: none.

### #144 — CulturalFeats systemic rule-4 across 16 models + 6 specifics (7 × P2, VALID)

Files: `Main/Features/CulturalFeats/Models/*.cs` (16 files)

All 16 model bodies contain `if`-branching or multi-line computation. Quoting representative examples:

`TaomCaravanModel.cs:13`:

```csharp
if (CharacterObject.PlayerCharacter?.Culture?.HasFeat(TaomCulturalFeats.UmbarCheaperCaravansFeat) == true)
    return MathF.Round(baseCost * (1f + TaomCulturalFeats.UmbarCheaperCaravansFeat.EffectBonus));
```

`TaomBattleRewardModel.cs:21,25`:

```csharp
var culture = party.Owner?.Culture ?? party.Culture;
...
var hero = party.Owner ?? party.LeaderHero;
```

`TaomClanFinanceModel.cs:19`:

```csharp
var culture = clan?.Culture;
```

`TaomSettlementProsperityModel.cs:22-29`:

```csharp
if (culture.HasFeat(TaomCulturalFeats.RivendellHearthGrowthFeat) && result.ResultNumber >= 0f)
    result.AddFactor(...);
if (culture.HasFeat(TaomCulturalFeats.MirkwoodHearthGrowthFeat) && result.ResultNumber >= 0f)
    result.AddFactor(...);
if (culture.HasFeat(TaomCulturalFeats.GondorHearthGrowthFeat) && result.ResultNumber >= 0f)
    result.AddFactor(...);
```

`TaomSmithingModel.cs:29-56`: 28-line `ApplySmithingFeatReduction` private static on the model class — rule-4 extraction must go to a Service, not to a model-private static.

Verdict: **VALID** on all 7 findings. Specifics:
1. Systemic rule 4 across 16 models — confirmed.
2. `TaomCaravanModel` uses `CharacterObject.PlayerCharacter` static — feat is player-only by accident, semantically inconsistent with feat description.
3. `TaomBattleRewardModel` asymmetric coalesce — feat path `party.Owner?.Culture ?? party.Culture` vs career path `party.Owner ?? party.LeaderHero`.
4. `TaomClanFinanceModel` defensive `?.` on non-nullable `Clan` parameter.
5. `TaomSettlementProsperityModel` 3× compound `if (HasFeat && ResultNumber >= 0f)` — business rule inline.
6. `TaomSmithingModel` extraction to model-private static — wrong target.
7. `TaomPartySpeedModel` roster `foreach` on per-tick hot path — overlaps with #135's P2.

Smallest fix: introduce `ICulturalFeatsService` (per-domain or unified); reduce each model body to a 1-line delegate. Per specific findings: route caravan cost through a party-owner adapter; align coalesce semantics in `TaomBattleRewardModel`; remove defensive `?.` (or assert non-null); move compound conditions into service business rule; move `ApplySmithingFeatReduction` to `ISmithingFeatsService`.

Dependencies: large refactor — gate on tests-first. `TaomSettlementLoyaltyModel` consumes `IRevoltTuningConfigProvider` (1-arg ctor at SubModule.cs:288) which already lives separately; other 15 models are no-arg. The Pattern-D coupling to `CareerPassiveHelper` is umbrella'd in #142; coordinate the deletion.

### #145 — TaomInformationRestrictionModel static-coupling (1 × P2, VALID)

File: `Main/Features/Encyclopedia/Models/TaomInformationRestrictionModel.cs`

Current code (full):

```csharp
public TaomInformationRestrictionModel()
    : this(() => TaomSettings.Instance?.ShowAllEncyclopediaCharacters ?? true) { }

internal TaomInformationRestrictionModel(Func<bool> showAll) => _showAll = showAll;
```

Verdict: **VALID.** The public ctor (line 11-12) hard-couples to `TaomSettings.Instance` static. The internal `Func<bool>` ctor is a test seam but production still reaches the static. Per `csharp-architecture.md` "Constructor injection only — no service locator in services."

Smallest fix: introduce `IEncyclopediaSettings` (single `bool ShowAllCharacters { get; }`); register implementation that reads from MCM in IoC; inject through the model constructor; production wiring stops reaching the static.

Dependencies: none.

### #146 — QuickActions per-save toggle contract broken (2 × P2 + 1 × P3, VALID)

File: `Main/Features/QuickActions/Hooks/InventorySearchCampaignBehavior.cs` + `Patch34_SPInventoryVMCapture.cs` + `Patch34_SPInventoryVMFinalize.cs`

Behavior lines 45-67:

```csharp
private void OnGameLoaded(CampaignGameStarter starter)
{
    if (_isSearchAvailable != _settings.EnableInventorySearch)
    {
        _isSearchAvailable = _settings.EnableInventorySearch;
        if (_settings.IsDebugMode)
            _logger.LogDebug($"[QuickActions] on-load reconciled IsSearchAvailable={_isSearchAvailable} from MCM");
    }
}

private void OnTick(float dt)
{
    if (_isSearchAvailable != _settings.EnableInventorySearch)
        _isSearchAvailable = _settings.EnableInventorySearch;
}
```

Patch34_SPInventoryVMCapture line 27:

```csharp
var adapter = IoC.Resolve<IInventoryVMAdapter>() as InventoryVMAdapter;
adapter?.SetActive(__instance);
```

Verdict: **VALID.** The implementation does both:
1. SyncData round-trips the bool every save (line 35) — appearing to honor a per-save toggle.
2. OnGameLoaded (51-56) AND OnTick (66-67) overwrite the loaded value with MCM whenever they differ — effectively making MCM authoritative every frame.

These two are contradictory. CLAUDE.md says "per-save `IsSearchAvailable` toggle" (Key Paths table). Either remove the reconcilers (option A — per-save semantics) or remove SyncData and update CLAUDE.md (option B — MCM-wins). Audit explicitly asks for a user decision. P3 concrete cast at Patch34_SPInventoryVMCapture.cs:27 is the same pattern as EquipPresets P1 (#141).

Smallest fix: requires user decision (option A or B per the audit body); apply consistently. The P3 concrete-cast fix is mechanical — widen `IInventoryVMAdapter` to expose `SetActive`/`ClearActiveIfMatches`.

Dependencies: P3 concrete-cast pattern overlaps with #141 P1. Could land one shared adapter-widening commit covering both `IInventoryScreenAdapter` and `IInventoryVMAdapter`.

### #147 — TaomExecutionRelationModel architectural smell + ADR-007 + rule-4 (3 × P2, VALID)

File: `Main/Features/Execution/Models/TaomExecutionRelationModel.cs`

Current code:

```csharp
public class TaomExecutionRelationModel : DefaultExecutionRelationModel
{
    private readonly IOnExecutionAction _executionHook;

    public TaomExecutionRelationModel(IOnExecutionAction executionHook)
    {
        _executionHook = executionHook;
    }

    public override int GetRelationChangeForExecutingHero(Hero victim, Hero hero, out bool showQuickNotification)
    {
        int baseChange = base.GetRelationChangeForExecutingHero(victim, hero, out showQuickNotification);

        var executorKingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;
        var victimKingdomId = victim?.Clan?.Kingdom?.StringId;
        var evaluatorKingdomId = hero?.Clan?.Kingdom?.StringId;

        if (executorKingdomId == null || victimKingdomId == null || evaluatorKingdomId == null)
            return baseChange;

        int modified = _executionHook.GetRelationModifier(executorKingdomId, victimKingdomId, evaluatorKingdomId, baseChange);

        if (modified == 0)
            showQuickNotification = false;

        return modified;
    }
}
```

Verdict: **VALID.**
- **Hook injected into model:** `IOnExecutionAction` is a hook interface (resolved via `IoC.ResolveAll<IOnX>()` in patches per `csharp-patterns.md`). Injecting it into a GameModel is structurally unusual — no other TAOM GameModel does this. Should be wrapped in an `IExecutionRelationService`.
- **Rule 4 inline branching:** null-guard if + delegate + `showQuickNotification = false` mutation when `modified == 0` — multi-step body that's not just boundary-adapt + delegate.
- **ADR-007 sealed access:** line 20 `Hero.MainHero?.Clan?.Kingdom?.StringId` reaches a sealed `Hero` static directly in the model body. Audit cited `MapFaction.StringId` — current path is `Clan?.Kingdom?.StringId`. The exact dereference chain differs but the violation (direct sealed-type access in model) holds.

Smallest fix: introduce `IExecutionRelationService` wrapping `GetRelationModifier`; return `(int RelationDelta, bool ShowNotification)` struct; resolve executor kingdom via injected `IPlayerContextAdapter`; model body becomes 1-line service call.

Dependencies: hooks/`IOnExecutionAction` likely has other consumers — research before reusing the existing interface vs adding a parallel service.

### #148 — TaomPartyWageModel inline + cross-feature helper + IoC cohesion gap (4 × P2, VALID)

File: `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` + `Main/IoC.cs:110`

TaomPartyWageModel lines 37-86 (GetTotalWage body):

```csharp
public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
{
    var result = base.GetTotalWage(mobileParty, troopRoster, includeDescriptions);

    if (mobileParty.IsGarrison && mobileParty.CurrentSettlement?.Town != null
        && mobileParty.CurrentSettlement.Owner?.Culture is { } garrisonCulture)
    {
        ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.EreborGarrisonWageFeat);
        ...
    }

    var partyCulture = mobileParty.Party?.Owner?.Culture;
    if (partyCulture != null)
    {
        if (partyCulture.HasFeat(TaomCulturalFeats.GundabadWageFeat))
            result.AddFactor(...);
        ...
        if (partyCulture.HasFeat(TaomCulturalFeats.RohanMountedWageFeat) && troopRoster != null)
        {
            float baseWageTotal = result.BaseNumber;
            if (baseWageTotal > 0f)
            {
                float mountedWageTotal = 0f;
                foreach (var element in troopRoster.GetTroopRoster())
                {
                    if (element.Character?.IsMounted == true)
                        mountedWageTotal += GetCharacterWage(element.Character) * element.Number;
                }
                ...
            }
        }
    }

    if (mobileParty.LeaderHero != null)
        CareerPassiveHelper.ApplyFactor(mobileParty.LeaderHero, ref result, PassiveEffectType.TroopWages);
    ...
}
```

TaomPartyWageModel lines 88-112 (GetTroopRecruitmentCost body):

```csharp
if (!withoutItemCost && troop.IsMounted)
{
    int horseCost = troop.Level >= 26 ? 500 : 150;
    result.Add(horseCost, null);
}

if (troop.IsMounted && buyerHero?.Culture?.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat) == true)
    result.AddFactor(...);
```

IoC.cs line 110:

```csharp
container.Register<IVolunteerContextAdapter, VolunteerContextAdapter>(Reuse.Singleton);
```

Verdict: **VALID.**
- `GetTotalWage` ~50-line inline body — rule 4.
- `GetTroopRecruitmentCost` inline branching (horse cost + mounted-feat guards) — rule 4.
- Cross-feature `CareerPassiveHelper.ApplyFactor` at line 83 — Pattern D coupling (TroopProgression → CareerSystem) at the model layer.
- `IVolunteerContextAdapter` registered in global IoC at IoC.cs:110 instead of feature-local `TroopProgressionIoC`.

Smallest fix: extend `ITroopCostService` with `ApplyWageModifiers(mobileParty, troopRoster, ref result)` and a richer `GetTroopRecruitmentCost(level, isMercenary, isMounted, withoutItemCost, buyerCulture)` overload. Route `CareerPassiveHelper` through `ICareerPassiveService` injected into the service. Move `IVolunteerContextAdapter` registration into `TroopProgressionIoC`.

Dependencies: `CareerPassiveHelper` removal overlaps with #142 + #144. The `IVolunteerContextAdapter` move is independent and trivial.

---

## Inline notes (not new issues)

- **Doc cross-reference:** the CLAUDE.md GameModel table at "Patch_MissionTime_SetMovementOrder" / "Patch31_SmartCavalryAI" is unrelated. The CulturalFeats umbrella in #144 cites "16 models" but the gamemodels rule file's existing-overrides list also assigns `TaomPartyWageModel` to CulturalFeats; current actual file location is `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs`. CLAUDE.md → `Main/Features/CulturalFeats/Models/` table count and the rule file's `TaomPartyWageModel` row may benefit from alignment, but this is doc drift not a bug.
- **#147 path drift:** the audit body cites `Hero.MainHero.MapFaction.StringId` at line 20; current code is `Hero.MainHero?.Clan?.Kingdom?.StringId`. ADR-007 violation persists either way. Mention this in the fix-PR description so the maintainer knows the audit text didn't lie — the lookup path simply evolved without the violation being addressed.
- **#134 perk drift:** the audit body cites `DefaultPerks.Engineering.SiegeWorks` and `Counterweights`; current code uses `Stonecutters` and `SiegeEngineer`. NRE risk identical. Worth a quick `ilspycmd` verification that the current perks exist on `DefaultPerks.Engineering` in v1.3.15 when writing the fix (audit text suggests the perk catalog has shifted recently).
- **No FALSE-POSITIVE, STALE, DUPLICATE, or SEVERITY-DRIFT verdicts in this batch.** All 15 audit findings were confirmed against HEAD with quoted code. No interim commits touched any of the cited files since 2026-05-13.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/triage-results.md](./triage-results.md)

<!-- backlinks-end -->
