# Batch D verification (#176–#195)

Verified by: general-purpose agent, Phase 9a, 2026-05-13
Inputs: triage-input-batch-D.json + test-coverage.md
HEAD: b4b4de1 fix(messengers): wire IoC + CampaignBehavior (#121)

## Summary

| Verdict | Count |
|---|---|
| VALID | 19 |
| STALE | 0 |
| FALSE-POSITIVE | 0 |
| DUPLICATE | 0 |
| SEVERITY-DRIFT | 1 |
| **Total** | 20 |

`git log --oneline --since="2026-05-13" -- TAOM.Tests/` returns no commits — no test additions since the audit. Every claimed gap re-confirmed against the current test directories. One issue (#193 SiegeDismount) has the gap (no wiring test) but the audit's mechanism description (`manual _harmony.Patch`) is incorrect — the feature uses `mission.AddMissionBehavior` instead. That is mechanism-drift, not severity-drift in the strict sense, but it changes the proposed fix substantially so it is flagged here.

## Per-issue verification table

| # | Feature | Verdict | Re-confirmed severity | Proposed fix scope | Depends on | Notes |
|---|---|---|---|---|---|---|
| 176 | CulturalFeats — 16 GameModels with zero behavior-hook tests | VALID | P1 | Per-model behavior-hook tests for the calculation logic in each of 16 `TaomXxxModel` classes; service extraction (issue #144) unblocks fuller coverage | #144 (service extraction) | Test file is reflection-only (counts properties); 16 GameModel files still on disk |
| 177 | FiefManagement — 5 behavior callbacks untested | VALID | P1 | Add `FiefHubCampaignBehaviorTests.cs` with `RegisterEvents_RegistersHandlers`, one delegation test per callback, `SyncData_RoundTrips` | — | Behavior class exists at `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs`; only `FiefHubServiceTests.cs` present |
| 178 | Warg — refactor IWargAttackService to use IAgentAdapter | VALID | P1 | Introduce `IAgentAdapter` (or extend existing), refactor `HandleWargTargetHit`/`WargAttack` to accept adapter; convert at Harmony patch boundary | New `IAgentAdapter` | `IWargAttackService.cs:7-9` still uses sealed `Agent`; test file lines 9-20 still document the gap |
| 179 | RaceAge — TaomPregnancyModel.GetDailyChanceOfPregnancyForHero untested | VALID | P2 | Add `TaomPregnancyModelTests.cs` (5-branch coverage + cross-tick consistency vs `RaceAgeBehavior.OnDailyTick`) | Phase 6 #3, #13 (consistency contract) | `TaomPregnancyModel.cs` exists; only `TaomAgeModelTests.cs` is in the test dir |
| 180 | TroopProgression — TaomPartyWageModel.GetTotalWage untested | VALID | P2 | Add `TaomPartyWageModelTests.cs` covering tier 0-10 + culture wage feats + Rohan mounted feat + career TroopWages passive (with no-passive baseline) | #148 (CareerPassiveHelper) | Test dir has 3 service-test files; no model test |
| 181 | CharacterCreation+HeroRace — race ID round-trip via save/load | VALID | P2 | Cross-feature test: assign race via CC → drive `CaptureHeroRaces` → SyncData round-trip → `RestoreHeroRaces` → assert `Hero.Race` preserved | #171 (CC stale race ID) | 13 CC test files; only round-trip in `FaceGenRaceSelectorRebuilderTests.cs:203` is an index round-trip (filtered↔global), not race-ID persistence |
| 182 | CompanionTactics+SmartCavalryAI — shared SetMovementOrder postfix ordering | VALID | P2 | Integration test driving one `Formation` through both postfixes in deterministic order; assert each subscriber's invariant survives | #170 (SmartCavalry handshake) | `TAOM.Tests/Features/CompanionTactics/` (BattleActionBar/FormationPresets/Roles subdirs) — no `SetMovementOrder` references |
| 183 | HeroRace — RacePersistenceBehavior.OnSessionLaunched restore untested | VALID | P2 | Test mocking `CapturedHeroRaces` store + driving `OnSessionLaunched`; assert each hero's race re-applied; pair with #181 round-trip | #171 | `RacePersistenceBehaviorTests.cs` only covers `SyncData_DelegatesToService` + `Behavior_IsCampaignBehaviorBase` |
| 184 | NamedCompanions — EnsureCompanionsPlaced state-matrix coverage | VALID | P2 | One test per state: NotSpawned / Spawned-Idle / Recruited-InPlayerParty / Recruited-Traveling / Prisoner / Fugitive / Dead | #139, #127 | `NamedCompanionServiceTests.cs` covers Recruited / AlreadyPlaced / Dead; **no Prisoner / Fugitive** matches in grep |
| 185 | AdvancedCombat — SpatialGridDebugService.RenderDebugVisualization untested | VALID | P2 | Verify invocation path (likely scene-render); add unit tests for empty/full grid + debug-mode skip; if not invoked, dead code under /deslop | — | `SpatialGridDebugService.cs` exists; test dir has only `BoneCollisionServiceTests.cs` |
| 186 | Spider — SpawnSpiders invocation contract + integration | VALID | P2 | Add tests for spawn count, team assignment, monster ID resolution; position math already tested at lines 84-114 (audit slightly overstated) | — | `SpiderSpawnerServiceTests.cs` has 6 tests including `ComputeSpawnPosition` math + null-handling; gap is team-assignment + monster-lookup |
| 187 | BannerColorPersistence — triplet event-ordering + re-entry | VALID | P2 | Integration test triggering Clan_UpdateBannerColor + Clan_UpdateBannerColorsAccordingToKingdom + SPInventoryVM patches in re-entry sequence; assert color stability | #172, #122 | 5 patch/service test files; no grep hits for "Triplet/Sequence/ReEntry" |
| 188 | FactionMap — CultureStageView re-entry lifecycle | VALID | P2 | Re-entry test: select factions → Finalize → re-enter without OnGameLoaded; assert pending pins cleared + `_factionVM` fresh | #175 | 7 service test files (services covered); no VM-level / lifecycle tests |
| 189 | MixedFormations — RepresentativeIsCavalry guards untested | VALID | P2 | Two tests: `ComputeUnitPlanePosition_CavalryFormation_ReturnsNull` + `IsMixedFormationInternal_CavalryFormation_ReturnsFalse` | #170 | `FormationLayoutService.cs:74, :191` guards still present; no grep hits in test dir |
| 190 | SmartCavalryAI — cross-feature integration with MixedFormations | VALID | P2 | Cross-cutting test: cavalry formation set to MixedFormations layout → assert MixedFormations skips → drive SmartCavalryAI charge state machine → assert no contention | #170, #189 | 2 SmartCavalryAI test files; no `MixedFormations` / `FormationLayout` references |
| 191 | Messengers — IoC + RegisterEvents wiring regression smoke test | VALID | **P1** (recommend bumping; this is the #121 regression class root) | Add `MessengerCampaignBehaviorTests.cs` with `RegisterEvents_RegistersListenersForAllExpectedEvents` (listener count > 0) + `SyncData_RoundTrips` | #121 | Test dir has Service / Config / StateStore files; **no CampaignBehavior file** — the exact gap that motivated #121 |
| 192 | SettlementGuards — manual Harmony patch wiring regression | VALID | P2 | Add `SettlementGuardPatchWiringTests.cs`: build Harmony instance + call patch-application code + assert patches register on `TakeGuardAgentDataFromGarrisonTroopList` + `GetSuitableSpear` target methods | Phase 0 #5, #121 | Confirmed manual `_harmony.Patch(...)` in `SubModule.cs:427-442`; SettlementGuards Hooks dir contains plain patch classes (no `[HarmonyPatchCategory]`); test dir has 2 service/config files only |
| 193 | SiegeDismount — manual Harmony patch wiring regression | SEVERITY-DRIFT (gap is real, mechanism description is wrong) | P2 | **Mechanism correction:** SiegeDismount uses `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` in `SubModule.cs:493`, NOT manual `_harmony.Patch`. The wiring gap is real (no test asserts the MissionBehavior is registered or `OnBehaviorInitialize`/`OnEndMission` fire); fix scope is "test the MissionBehavior wiring path", not "test Harmony patch binding". | Phase 0 #5, #121 | `Main/Features/SiegeDismount/` has no `_harmony.Patch` strings; production uses `MissionBehavior` lifecycle |
| 194 | SpecialResources — tiered-cost + passive-discount regression | VALID | P2 | Add `CanAffordUpgrade_TieredCostWithPassiveDiscount_GateAndDebitParity` — base 10 × tier-2 mult 1.5 = 15, then -30% passive = 10.5, assert CanAffordUpgrade + SpendForUpgrade use same final cost | #174 | Existing `SpendForUpgrade_CustomResourceUpgradeCostModifier_ReducesCost` at line 535 covers simple cost × discount; **no tiered-cost+discount test** in grep |
| 195 | TroopWeight — 4 IOn* hook implementations untested | VALID | P2 | One test file per hook: `PartyBaseNumberOfAllMembersHookTests.cs` + 3 siblings; ~2-3 tests each (invoke path, service routing, null guards) | — | All 4 hooks present at `Main/Features/TroopWeight/Hooks/`; test dir has 2 files (`TroopWeightServiceTests.cs`, `TroopWeightXmlLoaderTests.cs`) |

## Detailed per-issue verification

### #176 — CulturalFeats: 16 GameModels with zero behavior-hook tests

**Test dir contents at HEAD:** `TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs` (single file).

**Existing test surface** (`TaomCulturalFeatsDefinitionTests.cs:18-28`):
```csharp
[TestMethod]
public void AllFeatProperties_ReturnFeatObject_CountIs59()
{
    var properties = typeof(TaomCulturalFeats)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => p.PropertyType == typeof(FeatObject))
        .ToList();
    Assert.AreEqual(59, properties.Count, ...);
}
```

**Production state:** 16 `TaomXxxModel.cs` files still present in `Main/Features/CulturalFeats/Models/` (TaomArmyManagementModel through TaomVillageProductionModel). Verdict: **VALID**.

### #177 — FiefManagement: 5 behavior callbacks untested

**Test dir contents:** `TAOM.Tests/Features/FiefManagement/FiefHubServiceTests.cs` (single file). Grep for `RegisterEvents|OnSessionLaunched|OnNewGameCreated|OnGameLoaded|SyncData|FiefHubCampaignBehavior` returns no files. Behavior class still present at `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs`. Verdict: **VALID**.

### #178 — Warg: 2 methods untestable per ADR-007 violation

**Production state at HEAD** (`Main/Features/Warg/IWargAttackService.cs:7-9`):
```csharp
int CalculateWargAttackDamage(Agent target, float velocity);
void HandleWargTargetHit(Agent attacker, Agent target, sbyte boneId);
void WargAttack(Agent warg);
```

`Agent` is still sealed; service still takes it directly. Test file comment block at lines 9-20 still documents the gap. Verdict: **VALID**.

### #179 — RaceAge: TaomPregnancyModel untested

`Main/Features/RaceAge/Models/TaomPregnancyModel.cs` exists. Test dir: `RaceAgeConfigProviderTests.cs`, `RaceAgeServiceTests.cs`, `TaomAgeModelTests.cs` — no Pregnancy test. Grep for `TaomPregnancyModel|GetDailyChanceOfPregnancyForHero` returns no files. Verdict: **VALID**.

### #180 — TroopProgression: TaomPartyWageModel.GetTotalWage untested

Test dir: `TroopCostServiceTests.cs`, `VolunteerRecruitmentServiceTests.cs`, `VolunteerTierServiceTests.cs` — services only. Grep for `TaomPartyWageModel|GetTotalWage` returns no files. Verdict: **VALID**.

### #181 — CharacterCreation+HeroRace: race ID round-trip via save/load

13 test files cover CharacterCreation services. Grep for `RoundTrip|SaveLoad|RestoreHeroRaces|race.*persist|CaptureHeroRaces` returns one file — `FaceGenRaceSelectorRebuilderTests.cs:203` — but that test is `RoundTrip_FilteredToGlobalAndBack_IsIdentity`, an index round-trip helper, not a hero-race-ID persistence test. Verdict: **VALID**.

### #182 — CompanionTactics+SmartCavalryAI: shared SetMovementOrder postfix

Test subdirs: `BattleActionBar/`, `FormationPresets/`, `Roles/` (6 files). Grep for `SetMovementOrder|SmartCavalry|MissionTime|Patch_MissionTime` returns no files. Verdict: **VALID**.

### #183 — HeroRace: OnSessionLaunched restore untested

Full contents of `RacePersistenceBehaviorTests.cs` (37 lines) — only `SyncData_DelegatesToService` (line 22) + `Behavior_IsCampaignBehaviorBase` (line 32). Verdict: **VALID**.

### #184 — NamedCompanions: state-matrix coverage incomplete

Grep hits in `NamedCompanionServiceTests.cs` cover: NotRecruited+NotPlaced → places (line 178); Recruited → skips (line 186); AlreadyPlaced → skips (line 205); DeadHero → skips (line 224). Grep for `Prisoner|Fugitive` returns **no matches**. Audit's claim ("test suite covers ~2 of these" out of 7 states) is conservative — actually ~3-4 are covered, but Prisoner / Fugitive / Recruited-Traveling are missing. Verdict: **VALID**.

### #185 — AdvancedCombat: SpatialGridDebugService.RenderDebugVisualization untested

`Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs` exists. Test dir contains only `BoneCollisionServiceTests.cs`. Verdict: **VALID**.

### #186 — Spider: SpawnSpiders invocation contract gap

`SpiderSpawnerServiceTests.cs` has 6 tests: `AnchorCharacterNotFound_ReturnsEmpty` (line 46), `count: 0` and `count: -3` shape tests (lines 62, 74), `ComputeSpawnPosition_DistanceFromReference_WithinRadiusBounds` (line 84), `ComputeSpawnPosition_PreservesZAndWComponents` (line 105), `Constructor_PublicCtor_InjectsDefaultDelegates` (line 122). **Position math IS tested** (audit's claim "no verification of … position logic" is partially false); team assignment, monster lookup, and actual spawn behavior remain untested. SpiderMissionBehavior call site exists. Verdict: **VALID** (gap exists, audit description slightly overstates).

### #187 — BannerColorPersistence: triplet event-ordering + re-entry

5 test files (`AgentColorStoreTests.cs`, `BannerColorConfigProviderTests.cs`, `BannerColorServiceTests.cs`, `Clan_UpdateBannerColor_PatchTests.cs`, `Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs`). Grep for `Triplet|ReEntry|EventOrder|Sequence|sequencing|reentry` returns no files. All triplet patches still present at `Main/Features/BannerColorPersistence/Hooks/`. Verdict: **VALID**.

### #188 — FactionMap: CultureStageView re-entry lifecycle

7 service-level test files. Grep for `ReEntry|reentry|CultureStageView|pendingPins|stale|OnFinalize` returns no files. Verdict: **VALID**.

### #189 — MixedFormations: RepresentativeIsCavalry guards untested

Production guards still present at `Main/Features/MixedFormations/FormationLayoutService.cs:74` (`if (formation.RepresentativeIsCavalry) return null;`) and `:191` (`if (formation.RepresentativeIsCavalry) return false;`). Grep in test dir for `RepresentativeIsCavalry|Cavalry.*ReturnsNull|Cavalry.*ReturnsFalse|cavalryFormation` returns no matches. Verdict: **VALID**.

### #190 — SmartCavalryAI: cross-feature integration with MixedFormations

Test files: `CavalryChargeServiceTests.cs`, `CavalryPathPlannerTests.cs`. Grep for `MixedFormations|FormationLayout|cross.*feature|crossfeature` returns no files. Verdict: **VALID**.

### #191 — Messengers: IoC + RegisterEvents wiring regression smoke test

Test dir contents: `MessengerConfigProviderTests.cs`, `MessengerServiceTests.cs`, `MessengerStateStoreTests.cs`. Grep for `RegisterEvents|MessengerCampaignBehavior|OnSessionLaunched|OnNewGameCreated|OnGameLoaded` returns no files. Grep for `CampaignBehavior` returns no files. **No MessengerCampaignBehaviorTests.cs exists** — exactly the gap that motivated #121 (the fix landed in `b4b4de1` without a regression test). Verdict: **VALID**.

Recommend bumping severity (audit set P2 but per audit body: "this is the canonical Phase 7 regression class") — fixing the wiring without adding the gating test means the bug class can recur.

### #192 — SettlementGuards: manual Harmony patch wiring regression test

**Production state at HEAD** (`Main/SubModule.cs:427-442`):
```csharp
// Manual patches for private GuardsCampaignBehavior methods (SandBox.dll)
var takeGuardTarget = GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod();
if (takeGuardTarget != null)
    _harmony.Patch(takeGuardTarget, prefix: new HarmonyMethod(
        typeof(GuardsCampaignBehavior_TakeGuardAgentData_Patch),
        nameof(GuardsCampaignBehavior_TakeGuardAgentData_Patch.Prefix)));
...
var spearTarget = GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod();
if (spearTarget != null)
    _harmony.Patch(spearTarget, prefix: new HarmonyMethod(...));
```

Test dir: `SettlementGuardConfigProviderTests.cs`, `SettlementGuardServiceTests.cs`. No wiring test. Verdict: **VALID**.

### #193 — SiegeDismount: manual Harmony patch wiring regression test

**Mechanism mismatch — flagged SEVERITY-DRIFT.**

Audit body states: *"the feature uses manual `_harmony.Patch(...)` calls. NO test verifies the patches bind or `OnMissionStart` / `OnMissionEnd` are invoked at the right mission-lifecycle hook."*

**Actual production code at HEAD** (`Main/SubModule.cs:493`):
```csharp
mission.AddMissionBehavior(new SiegeDismountMissionBehavior());
```

And `Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs:10-32`:
```csharp
public class SiegeDismountMissionBehavior : MissionBehavior
{
    ...
    public override void OnBehaviorInitialize()
    {
        ...
        _service.OnMissionStart(isSiegeBattle, sceneName);
    }
    protected override void OnEndMission()
    {
        ...
        _service.OnMissionEnd();
    }
}
```

There are **no `_harmony.Patch(...)` calls** for SiegeDismount in `Main/SubModule.cs` and no `Harmony` references in `Main/Features/SiegeDismount/`. The feature is wired via `MissionBehavior` lifecycle, not Harmony.

**The wiring gap is still real**: `SiegeDismountServiceTests.cs` covers the service's `OnMissionStart`/`OnMissionEnd` methods directly (lines 39, 51, 60, 71, 100), but **no test asserts `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` is called or that `OnBehaviorInitialize` invokes `_service.OnMissionStart`**. So the regression class (drop the AddMissionBehavior line and the feature silently no-ops) is unprotected, just like SettlementGuards #192 — the mechanism is different.

**Closing comment for the issue:**
> Mechanism description in the body is incorrect: SiegeDismount uses `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` (SubModule.cs:493), not manual `_harmony.Patch(...)`. The wiring-test gap is real — no test asserts the `MissionBehavior` is registered in the mission pipeline or that `OnBehaviorInitialize`/`OnEndMission` route to `ISiegeDismountService`. Fix scope should be a MissionBehavior wiring smoke test (instantiate behavior + drive `OnBehaviorInitialize` against a stub mission + assert service is called), not a Harmony patch binding test.

Verdict: **SEVERITY-DRIFT** (gap stands at P2; mechanism description wrong; fix scope changes).

### #194 — SpecialResources: tiered-cost + passive-discount regression test

Existing tests at `SpecialResourceServiceTests.cs:534` (`SpendForUpgrade_CustomResourceUpgradeCostModifier_ReducesCost`, simple cost × discount); line 549 (`ClampUpgradeCount_CustomResourceUpgradeCostModifier_AllowsMore`); line 563 (`SpendForUpgrade_NoCareerPassive_CostUnchanged`). None combine tiered cost (resource tier multiplier) × passive discount × CanAffordUpgrade-vs-SpendForUpgrade parity. Grep for `tiered.*passive|tier.*Discount|GateAndDebitParity|TieredCost` in `SpecialResourceTierServiceTests.cs` and other test files returns no matches. Verdict: **VALID**.

### #195 — TroopWeight: 4 IOn* hook implementations untested

Production hooks (`Main/Features/TroopWeight/Hooks/`):
- `PartyBaseNumberOfAllMembersHook.cs`
- `PartyBaseNumberOfRegularMembersHook.cs`
- `PartyVMPopulatePartyListLabelHook.cs`
- `RecruitmentVMRefreshPartyPropertiesHook.cs`

Test dir: `TroopWeightServiceTests.cs`, `TroopWeightXmlLoaderTests.cs`. Grep for any of the 4 hook class names returns no files. Verdict: **VALID**.

## No new findings.

All 20 issues mapped to one of: VALID (19) or SEVERITY-DRIFT (1, #193). No STALE / FALSE-POSITIVE / DUPLICATE verdicts.

Counts per verdict:
- VALID: 19
- STALE: 0
- FALSE-POSITIVE: 0
- DUPLICATE: 0
- SEVERITY-DRIFT: 1 (#193)
