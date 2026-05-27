# Test Coverage Audit — Phase 7

Last updated: 2026-05-13
Scope: 44 features × test directory presence + test depth analysis (manifest claims 43, disk shows 44 — off-by-one in manifest text vs. table).

## Executive summary

| Severity | Count | Features |
|---|---|---|
| **P1** | 3 | CulturalFeats, FiefManagement, Warg |
| **P2** | 17 | RaceAge, TroopProgression, CharacterCreation, CompanionTactics, HeroRace, NamedCompanions, AdvancedCombat, Spider, BannerColorPersistence, FactionMap, MixedFormations, SmartCavalryAI, Messengers, SettlementGuards, SiegeDismount, SpecialResources, TroopWeight |
| **P3** | 8 | Encyclopedia, EquipPresets, MainMenuCustomizer, ShaderPrecompilation, CustomBattles, LocalizationOverride, CareerSystem, QuickActions |
| **OK** | 16 | Arena, ArmyTargeting, BattleBalance, Diplomacy, BannerInjection, InitialChildGeneration, StartupResources, EditorCacheRebuild, RevoltTuning, Siege, TimeAcceleration, AtmospherePersistence, BattleScenes, CharacterSelection, WeatherBoundsGuard, Execution |

**Phase-7 verdict:** TAOM is in better test-coverage shape than the Phase 3 P1 count (24) suggested. Of 44 features, 36% (16) meet ADR-008 outright; 18% (8) have shallow but functional coverage; 39% (17) have a concrete cross-feature or callback gap; 7% (3) have a fundamental P1 problem.

**Dominant Phase-7 bug class:** **wiring / hook regression tests are missing** even when service-level tests are strong. The Messengers crash (#121) symptom — IoC drop, no test would catch it — still ships untested for SettlementGuards and SiegeDismount (both use manual `_harmony.Patch(...)`), and TroopWeight has 4 IOn* hooks with zero tests.

**Phase 0 carryovers resolved:**
- ✅ **CharacterSelection** test absence resolved — pure `[HarmonyTranspiler]` patch, documented untestable in `docs/features/character-selection.md`. Not a gap.
- ✅ **BattleScenes** test absence resolved — feature disabled per CLAUDE.md; test absence correct.
- ✅ **Phase 5 #168 (TimeAcceleration wrong state signal)** verified RESOLVED — all state-signal paths covered. Recommend closing #168.

**No `*Engine.cs` files exist in TAOM** — the "100% engines" rule from ADR-008's spirit is vacuous in this repo. Future engine-like additions (`*Builder.cs` in EditorCacheRebuild, planning helpers) should inherit the 100%-service rule; documented for future rule scope.

## Master findings table

| # | Severity | Feature | Test gap | File:Line (production) | Test file | Issue |
|---|---|---|---|---|---|---|
| 1 | **P1** | CulturalFeats | 16 GameModels, only 1 structural test file (8 reflection tests). Zero behavior-hook tests of override calculation logic. 8 models inline `CareerPassiveHelper.ApplyFactor` from override body (cross-feature static coupling). | `Main/Features/CulturalFeats/Models/*.cs` (16 files) | `TaomCulturalFeatsDefinitionTests.cs` (structural only) | #176 |
| 2 | **P1** | FiefManagement | Strong service tests (22 for 8 methods), but **5 behavior callbacks completely untested**: `OnSessionLaunched`, `OnNewGameCreated`, `OnGameLoaded`, `SyncData`, `RegisterEvents`. ADR-008 80% hook target unmet. | `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs` | `FiefHubServiceTests.cs` (service only) | #177 |
| 3 | **P1** | Warg | Two of four `WargAttackService` public methods (`HandleWargTargetHit`, `WargAttack`) accept sealed `Agent` — **untestable** per test-file comment lines 9-20. ADR-007 violation blocks ADR-008 100% target. | `Main/Features/Warg/WargAttackService.cs:32, :79` | `WargAttackServiceTests.cs` | #178 |
| 4 | **P2** | RaceAge | `TaomPregnancyModel.GetDailyChanceOfPregnancyForHero` (22 lines, 5 branches) has no test. Cross-tick consistency with `RaceAgeBehavior.OnDailyTick` (Phase 6 #3, #13) unverified. | `Main/Features/RaceAge/Models/TaomPregnancyModel.cs` | `RaceAgeServiceTests.cs` + 2 others | #179 (refs #131) |
| 5 | **P2** | TroopProgression | `TaomPartyWageModel.GetTotalWage` (~50 lines, multi-branch) has no test. Inlines `CareerPassiveHelper.ApplyFactor` from override body (Phase 6 #32, #34, #148). | `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` | `*ServiceTests.cs` (services tested) | #180 (refs #148) |
| 6 | **P2** | CharacterCreation | Cross-feature race ID round-trip via save/load untested (Phase 6 #171). Service surface itself is strongly covered. | `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | 13 test files | #181 (refs #171) |
| 7 | **P2** | CompanionTactics | Shares deferred `Patch_MissionTime_SetMovementOrder` category with SmartCavalryAI. No test for behavior coexistence / `SetMovementOrder` postfix ordering (Phase 6 #170). | `Main/Features/CompanionTactics/Hooks/FormationPresetCampaignBehavior.cs` | 6 test files | #182 (refs #170) |
| 8 | **P2** | HeroRace | `RacePersistenceBehaviorTests` only cover `SyncData` delegation, not `OnSessionLaunched` restore. Cross-feature invariant (post-CC race ID survives save/load) untested (Phase 6 #171). | `Main/Features/HeroRace/Hooks/RacePersistenceBehavior.cs` | `RacePersistenceBehaviorTests.cs` | #183 (refs #171) |
| 9 | **P2** | NamedCompanions | `EnsureCompanionsPlaced` state matrix (recruited / traveling / prisoner / fugitive) incomplete; Phase 3 #139 Review #23 regression risk. | `Main/Features/NamedCompanions/NamedCompanionService.cs` | `NamedCompanionServiceTests.cs` | #184 (refs #139) |
| 10 | **P2** | AdvancedCombat | `SpatialGridDebugService.RenderDebugVisualization` has zero tests; consumption path unknown (Phase 0). | `Main/Features/AdvancedCombat/Services/SpatialGridDebugService.cs:11` | `BoneCollisionServiceTests.cs` (other service tested) | #185 |
| 11 | **P2** | Spider | `SpawnSpiders` invocation contract unverified; tests verify shape, not actual spawn behavior or team/position logic (Phase 0). | `Main/Features/Spider/SpiderSpawnerService.cs:56` | `SpiderSpawnerServiceTests.cs` | #186 |
| 12 | **P2** | BannerColorPersistence | Triplet event-ordering and re-entry sequencing untested across `Clan.UpdateBannerColor` / `UpdateBannerColorsAccordingToKingdom` / `SPInventoryVM_UpdateCurrentCharacterIfPossible` (Phase 6 #16 / #172). | `Main/Features/BannerColorPersistence/Patches/*.cs` | 5 test files | #187 (refs #172) |
| 13 | **P2** | FactionMap | CultureStageView re-entry lifecycle (pending pins + stale `_factionVM`) untested across `OnCreated → OnTick × N → OnFinalize` boundary (Phase 6 #18 / #175). | `Main/Features/FactionMap/Views/CultureStageView*.cs` | 7 service test files (services covered, VMs not) | #188 (refs #175) |
| 14 | **P2** | MixedFormations | `RepresentativeIsCavalry` guards have ZERO tests. | `Main/Features/MixedFormations/FormationLayoutService.cs:74, :191` | `FormationLayoutServiceTests.cs` | #189 (refs #170) |
| 15 | **P2** | SmartCavalryAI | Own guards tested in isolation; cross-feature contract with MixedFormations (cavalry exclusion across call boundary) untested (Phase 6 #1 / #170). | `Main/Features/SmartCavalryAI/CavalryChargeService.cs` | 2 test files | #190 (refs #170) |
| 16 | **P2** | Messengers | Wiring-class regression test missing. A behavior-registration smoke test ("`RegisterEvents` fires after IoC registration") would have caught the audit-motivating crash (#121). All current tests mock the adapter chain. | `Main/Features/Messengers/Hooks/MessengerCampaignBehavior.cs` | 3 test files (service + config + state-store) | #191 (refs #121) |
| 17 | **P2** | SettlementGuards | Manual `_harmony.Patch(...)` (no patch category — Phase 0 #5). No test verifies the patch binds or service is invoked at mission-init. | `Main/Features/SettlementGuards/Hooks/*.cs` | `SettlementGuardServiceTests.cs` (service only) | #192 |
| 18 | **P2** | SiegeDismount | Manual `_harmony.Patch(...)`. No test verifies the patch registers or `OnMissionStart` / `OnMissionEnd` are invoked at the right mission-lifecycle hook. | `Main/Features/SiegeDismount/Hooks/*.cs` | `SiegeDismountServiceTests.cs` (service only) | #193 |
| 19 | **P2** | SpecialResources | Discount-not-applied-to-debit bug (Phase 6 #14 / #174) fixed without a tiered-cost + passive-discount regression test. | `Main/Features/SpecialResources/SpecialResourceService.cs:160-187` | `SpecialResourceServiceTests.cs` + 4 others | #194 (refs #174) |
| 20 | **P2** | TroopWeight | 4 `IOn*` hook implementations (`PartyBaseNumberOfAllMembersHook` + 3 siblings) have **zero tests**. ADR-008 80% hook coverage unmet. | `Main/Features/TroopWeight/Hooks/*.cs` (4 files) | `TroopWeightServiceTests.cs` (service only) | #195 |
| 21 | P3 | Encyclopedia | 2 tests cover 2 code paths; no boundary cases | — | `TaomInformationRestrictionModelTests.cs` | doc only |
| 22 | P3 | EquipPresets | Adapter mocked; `InventoryLogic.TransferCommand` path not exercised (Codex review #5 CRITICAL risk class) | — | 4 test files | doc only |
| 23 | P3 | MainMenuCustomizer | 5 happy-path tests; no error / null-adapter / exception paths | — | `MainMenuCustomizerServiceTests.cs` | doc only |
| 24 | P3 | ShaderPrecompilation | 7 happy-path retrieval tests; no error / fallback / missing-adapter paths | — | `ShaderPrecompilationServiceTests.cs` | doc only |
| 25 | P3 | CustomBattles | `TaomFactionSelectionVM` nav methods untested (low complexity) | `Main/Features/CustomBattles/TaomFactionSelectionVM.cs:14` | 5 test files | doc only |
| 26 | P3 | LocalizationOverride | Loader exhaustively tested; no malformed-XML / large-file boundary | — | 2 test files | doc only |
| 27 | P3 | CareerSystem | `CareerPassiveHelper.ApplyFactor` multi-passive composition (decimal truncation, race override precedence) untested in integration fixture (Phase 6 #173) | `Main/Features/CareerSystem/CareerPassiveHelper.cs` | 19 test files | doc only (refs #173) |
| 28 | P3 | QuickActions | Sell-loop vanilla re-entry (capacity-budget, settlement-gold post-sale) untested (Codex review #36) | `Main/Features/QuickActions/QuickActionsService.cs` | 3 test files | doc only |

## Per-feature reports

Per-feature details are folded into the master findings table above; the master table cites file:line and the per-feature severity / cross-feature touchpoint. The five batch agent reports (one per cluster: A=GameModels, B=CampaignBehaviors, C=Services, D=Patches/UI, E=Heavy mixed) were materialized to `_phase7_drafts/` during aggregation and deleted per the Phase 6 cleanup precedent — full contents are reproduced in the master findings table and cross-cuts below.

## Cross-cuts

### ADR-008 compliance summary

| Threshold | Status |
|---|---|
| 100% service unit-testable | 39 of 44 features (89%) have service coverage at ≥1.0 ratio; 5 features fall below (CulturalFeats 0.14, FiefManagement 2.75 services-only with no behavior tests, Warg 1.75 with 2 untestable methods, AdvancedCombat 1.83 one service untested, Spider 5.0 ratio but one service untested) |
| 80% behavior-hook coverage | **8 features fall below** (CulturalFeats, FiefManagement, TroopWeight, Messengers regression-class, SettlementGuards, SiegeDismount, CharacterCreation cross-feature, HeroRace cross-feature). The lowest-quality coverage layer in TAOM. |
| No static TaleWorlds calls in services | Spot-checked services in audit; no new violations surfaced. Phase 3 cluster doc remains the canonical record. |

### Cross-feature contract test gaps (Phase 6 carryovers)

Phase 6 explicitly named 5 test gaps; Phase 7 confirms each:

| Phase 6 Finding | Phase 7 confirmation |
|---|---|
| #1 / #170 — SmartCavalryAI × MixedFormations handshake | Confirmed in MixedFormations P2 (FormationLayoutService.cs:74, :191 guards untested) AND SmartCavalryAI P2 (own guards tested in isolation, cross-feature contract untested) |
| #3 / #13 / #131 — RaceAge same-tick consistency | Confirmed (TaomPregnancyModel + RaceAgeBehavior.OnDailyTick both read `hero.Race`; no consistency test) |
| #8 — `TaomSettlementLoyaltyModel` (RevoltTuning consumer) | Confirmed (CulturalFeats P1 — model untested; producer-side RevoltTuning is fine) |
| #14 / #174 — SpecialResources discount-not-applied-to-debit | Confirmed (bug fixed, regression test absent — tiered-cost + passive-discount case missing) |
| #173 — CareerPassiveHelper multi-passive composition | Confirmed (10 callers; helper composition logic exercised only at single-passive level; multi-passive aggregation untested) |

### Pattern: services well-tested, hooks / wiring not

Across batches B and E, the recurring pattern is **mean service ratio ~6 : mean behavior-hook test count ~0**. Features ship strong service test suites and then leave the campaign-behavior callbacks and Harmony patch wiring untested. This is precisely the regression class that motivated the audit (Messengers crash #121) — the unit-tested service produces correct outputs but is never invoked because wiring is silently dropped.

### Pattern: manual Harmony patches without wiring tests

3 features use manual `_harmony.Patch(...)` (no `[HarmonyPatchCategory]`): SettlementGuards, SiegeDismount, BannerColorPersistence (partial). Of these, SettlementGuards and SiegeDismount have NO test for the patch wiring — same regression class as Messengers #121. BannerColorPersistence has patch-class tests but the cross-patch event-ordering is untested.

### No `*Engine.cs` files

Verified across all 44 features: TAOM has zero `*Engine.cs` files. The closest analogues are EditorCacheRebuild's `*Builder.cs` files (Phase1/2 parallel + serial builders), all of which are tested. Future "engine"-like classes should inherit the 100%-service rule. Documenting for future rule scope.

### Manifest 43 vs disk 44 off-by-one

The Phase 0 manifest text reads "43 features" but the manifest table and `ls Main/Features/` show 44 (excluding `TaomSettings.cs` file). Minor; flagged for manifest correction in Phase 8.

## GitHub issues opened (20 — P1×3 + P2×17)

All issues are labelled `audit-impl` + `audit-tests`. Issue numbers #176–#195 (in severity then batch order):

| # | Severity | Title | Refs |
|---|---|---|---|
| #176 | P1 | audit-tests: CulturalFeats — 16 GameModels with zero behavior-hook tests | refs #144, #135 |
| #177 | P1 | audit-tests: FiefManagement — 5 behavior callbacks untested (ADR-008 80% hook target unmet) | refs #143, #121 |
| #178 | P1 | audit-tests: Warg — refactor IWargAttackService to use IAgentAdapter (ADR-007); 2 methods currently untestable | new |
| #179 | P2 | audit-tests: RaceAge — TaomPregnancyModel.GetDailyChanceOfPregnancyForHero untested + cross-tick consistency | refs #131, Phase 6 #3, #13 |
| #180 | P2 | audit-tests: TroopProgression — TaomPartyWageModel.GetTotalWage untested + CareerPassiveHelper coupling | refs #148 |
| #181 | P2 | audit-tests: CharacterCreation+HeroRace — race ID round-trip via save/load | refs #171, #125 |
| #182 | P2 | audit-tests: CompanionTactics+SmartCavalryAI — behavior ordering + shared SetMovementOrder postfix | refs #170 |
| #183 | P2 | audit-tests: HeroRace — RacePersistenceBehavior.OnSessionLaunched restore + cross-feature persistence | refs #171, #130 |
| #184 | P2 | audit-tests: NamedCompanions — EnsureCompanionsPlaced state-matrix coverage | refs #139 |
| #185 | P2 | audit-tests: AdvancedCombat — SpatialGridDebugService.RenderDebugVisualization untested | new |
| #186 | P2 | audit-tests: Spider — SpawnSpiders invocation contract + integration hook | new |
| #187 | P2 | audit-tests: BannerColorPersistence — triplet event-ordering and re-entry sequencing | refs #172, #122 |
| #188 | P2 | audit-tests: FactionMap — CultureStageView re-entry lifecycle (pending pins, stale VM) | refs #175 |
| #189 | P2 | audit-tests: MixedFormations — SmartCavalryAI handshake (RepresentativeIsCavalry guards) | refs #170 |
| #190 | P2 | audit-tests: SmartCavalryAI — cross-feature integration with MixedFormations (cavalry exclusion) | refs #170 |
| #191 | P2 | audit-tests: Messengers — IoC + RegisterEvents wiring regression smoke test | refs #121, #123 |
| #192 | P2 | audit-tests: SettlementGuards — manual Harmony patch wiring regression test | refs Phase 0 #5, #121 |
| #193 | P2 | audit-tests: SiegeDismount — manual Harmony patch wiring regression test | refs Phase 0 #5, #121 |
| #194 | P2 | audit-tests: SpecialResources — tiered-cost + passive-discount regression test (#174) | refs #174 |
| #195 | P2 | audit-tests: TroopWeight — 4 IOn* hook implementations untested; ADR-008 80% hook coverage unmet | new |

(Phase 9 will batch the actual test additions, likely grouped by pattern: behavior-callback smoke tests, manual-Harmony wiring tests, cross-feature contract tests, model-override behavior tests.)

### Cleanup recommendations (no issues opened)
- Phase 5 #168 (TimeAcceleration wrong state signal) — Phase 7 verified RESOLVED. Recommend closing.
- Phase 0 manifest text says "43 features" but the table and `Main/Features/` show 44 (excluding `TaomSettings.cs`). Trivial; correct in Phase 8 manifest sweep.

## Phase 7 complete

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/phase-8-kickoff.md](./phase-8-kickoff.md)
- [docs/audits/phase-9-kickoff.md](./phase-9-kickoff.md)

<!-- backlinks-end -->
