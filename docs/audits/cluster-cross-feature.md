# Cross-Feature Handshake Audit — Phase 6

Last updated: 2026-05-13
Scope: 8 known collision pairs/triples + global same-method-patch sweep
Inputs: [phase-6-kickoff.md](phase-6-kickoff.md) (procedure + carryovers), [feature-manifest.md](feature-manifest.md), [wiring-matrix.md](wiring-matrix.md), Phase 2–5 cluster docs

## Executive summary

Phases 2–5 reviewed features in isolation. Phase 6 reviewed the **gaps between features** — places where two TAOM features touch the same TaleWorlds API, GameModel parameter, mission state, static helper, or UI prefab, and the outcome depends on which fires last (or first).

**Findings: 41 total — 2 P1, 13 P2, 26 P3** across 8 collision pairs/triples + 1 global sweep.

Net new GitHub issues opened: **6** (one consolidated `audit(impl): <Pair>` issue per pair with active P1/P2 findings). Two pairs (CulturalFeats×RevoltTuning, TimeAcceleration×MapBar) returned all-P3 design confirmations only — no issues. The global sweep produced no net-new collision (A.collision-3 is a borderline P3 documentary note).

### Headline findings (P1)

- **Banner triplet F1 (P1):** `MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize` is never called — world-map party icons never receive clan-specific colors. **Already tracked in #122** (Phase 1); Phase 6 confirms the root cause is a single missing `Initialize` call in `SubModule.OnSubModuleLoad`.
- **CareerSystem × TroopProgression F3 (P1):** `TaomPartyWageModel.GetTotalWage` contains inline `foreach + arithmetic` (lines 65–79) — violates `gamemodels.md` "no inline if/foreach/switch in GameModel override body". Net-new.

### Risk profile (P2)

13 P2 findings span four risk shapes:
1. **Implicit precedence** (4 findings) — same-method patches without `[HarmonyPriority]`/`[HarmonyBefore]`/`[HarmonyAfter]` annotations, or event-ordering that works today but a future TaleWorlds change could break.
2. **Cross-feature contract gaps** (4 findings) — feature A trusts feature B's output without a validity check (stale race IDs, missing handshake tests, debit using base cost while gate uses discounted cost).
3. **Thread-safety asymmetry** (3 findings) — `CavalryChargeService._states` is lock-free while its sister `FormationLayoutService._layoutByFormation` holds an explicit lock; `CareerPassiveService._cache` plain Dictionary in a path that can hit worker threads.
4. **Static-state lifecycle gaps** (2 findings) — FactionMap `_factionVM` reference + `_pendingPins` list survive across CC re-entry transitions when they shouldn't.

### Carryovers settled this phase

- **Gauntlet threading verdict** (cluster-ui findings #15, #16): `GauntletLayer.LateUpdate` and `RenderTick` are strictly sequential on the engine main thread. `PolygonWidget` does NOT override `OnParallelUpdate`. Findings #15 and #16 **DOWNGRADE to P3** (design smell, not race conditions). See per-pair report 8.
- **WidgetFactory registration** (cluster-ui finding #25): the engine's `WidgetInfo.CollectWidgetTypes()` assembly-scan handles all 4 FactionMap widget types. **#25 RESOLVED for FactionMap** (SpecialResources `Replace`-based prefab extension concern is separate).

## Master findings table

| # | Severity | Pair | Touchpoint | File:Line | Finding | Issue |
|---|---|---|---|---|---|---|
| 1 | P2 | SmartCavalryAI×MixedFormations | `RepresentativeIsCavalry` guards in `FormationLayoutService` | `FormationLayoutService.cs:74, 191` | Handshake explicit in code but has no cross-feature test. Refactor could silently re-introduce the P1 charge-line overwrite. | #170 |
| 2 | P2 | SmartCavalryAI (self) | `CavalryChargeService._states` dict | `CavalryChargeService.cs:33` | Lock-free Dictionary; sibling `FormationLayoutService._layoutByFormation` holds explicit lock. Asymmetric threading posture; latent race if `SetMovementOrder` ever fires off-thread. | #170 |
| 3 | P3 | CompanionTactics×SmartCavalryAI | Patch35 `CancelStanceOnMove` Postfix | `Patch35_Formation_SetMovementOrder.cs:30` | Patch35 doesn't check `SmartCavalryRecursionGuard.IsSuppressed`; clears player stances on cavalry during SmartCavalryAI-internal `IssueStop`. Cosmetic only (stances are display-only). | — |
| 4 | P3 | SmartCavalryAI (self) | `FormationAdapter._alignmentScratch` | `FormationAdapter.cs:155` | Shared static `List<float>` — currently safe (single-threaded caller) but latent if `IsAligned` ever called from worker thread. | — |
| 5 | P3 | Shared category | `Patch_MissionTime_SetMovementOrder` ordering | `SubModule.cs:485` | No `[HarmonyAfter]` annotation; Patch31 fires before Patch35 by alphabetical class name only. | — |
| 6 | P3 | CulturalFeats | `TaomSettlementLoyaltyModel.CalculateLoyaltyChange` | `TaomSettlementLoyaltyModel.cs:34-58` | 5 inline `if (HasFeat) result.Add` branches in GameModel body — violates `gamemodels.md` rule. | — |
| 7 | P3 | RevoltTuning→CulturalFeats | Snapshot vs live provider | `TaomSettlementLoyaltyModel.cs:17-20` | Constructor stores `revoltTuning.GetConfig()` result, not the provider. Safe today (Singleton+Lazy) but undocumented dependency. | — |
| 8 | P3 | CulturalFeats/RevoltTuning | Test coverage | `TAOM.Tests/Features/CulturalFeats/` | No behavioral tests for `TaomSettlementLoyaltyModel`. | — |
| 9 | P2 | HeroRace→RaceAge | Race-ID persistence/consumption | `RacePersistenceService.cs:24-34` + `RaceAgeService.cs:42-53` | Stale/invalid race ID from removed-mod save flows unvalidated; `RaceAgeService._raceNameCache` permanently caches the human fallback. Elven immortality silently lost for affected heroes. | #171 |
| 10 | P2 | CharacterCreation (Patch20 vs Patch29) | `CharacterCreationNarrativeStageView.RefreshAgentVisuals` | `Patch20…:168` + `Patch29…BodySync_Patch.cs:1` | Two Prefixes on same method, different fields (Race vs BodyProperties). Ordering implicit; works today but undocumented. | #171 |
| 11 | P3 | CharacterCreation×HeroRace | `playerChar.Race` is 0 at culture-selection time | `PlayerBodyPropertiesAdapter.cs:32` | `UpdatePlayerCharacterBodyProperties` fires `OnPlayerBodyPropertiesChanged` with race=0 before Patch9 sets the culture race. Listener-dependent. | — |
| 12 | P3 | RaceAge | `TaomHeroCreationModel.GetCharacterTemplateForOffspring` | `TaomHeroCreationModel.cs:9-17` | Returns sealed `CharacterObject` (forced by TaleWorlds API). ADR-007 design smell. | — |
| 13 | P3 | RaceAge (self) | DailyTick read overlap | `RaceAgeBehavior.cs:37-39` + `TaomPregnancyModel.cs:20` | Both read `hero.CharacterObject.Race` in same tick. Read-only, additive. No test asserts same-hero same-day consistency. | — |
| 14 | **P1** | BannerColorPersistence | `MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize` not called | `SubModule.cs:174-180` (omission) | World-map party icons never receive clan colors — silent no-op. **Already tracked in #122.** | #122 (cross-ref) |
| 15 | P2 | BannerColorPersistence | `Clan.UpdateBannerColorsAccordingToKingdom` Prefix blocks ALL clans | `Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:17-18` | DriftGuard blocks vanilla kingdom-color sync for every clan, not just player. NPC clans that change kingdoms never get updated banner colors. | #172 |
| 16 | P2 | BannerColorPersistence | `TargetMethod()` private-method resolve has no null-guard | `Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:14-15` | If the private method is renamed in a Bannerlord patch, Harmony silently skips — no log, no warning. | #172 |
| 17 | P3 | BannerColorPersistence×BannerInjection | Banner editor coexistence | `BannerEditorView_OnTick_Patch.cs` + `GauntletBannerEditorScreen_OnDone_Patch.cs` | Different target methods; cleanly additive. Worth documenting the full call chain. | — |
| 18 | P2 | BannerInjection×Patch24 | Event ordering on new-game | `BannerInjectionBehavior.cs:17` vs `SubModule.cs:397` | `InjectBanners` fires on `OnNewGameCreated`; Patch24 activates later in `OnGameInitializationFinished`. Works today but undocumented asymmetry vs load path. | #172 |
| 19 | P3 | BannerColorPersistence | Direct `Clan.PlayerClan` access | `PartyCharacterVM_GetCharacterCode_Patch.cs:39-42` | Bypasses adapter that sibling patches use. ADR-007 inconsistency. | — |
| 20 | P2 | CareerSystem (helper) | Service locator in static helper | `CareerPassiveHelper.cs:27, 32` | `IoC.Resolve` inside a static utility called from GameModel override bodies — not a boundary. Static caches non-volatile. | #173 |
| 21 | P2 | CareerSystem (cache) | Plain Dict race vs RefreshCache | `CareerPassiveService.cs:11-12, 21, 72` | `_cache.Clear()` + rebuild while `GetPassiveMagnitude` may be invoked from AI worker threads (`GetTotalWage` desertion path, `GetPartyMemberSizeLimit` party-size). | #173 |
| 22 | **P1** | TroopProgression | Inline `foreach`/arithmetic in GameModel | `TaomPartyWageModel.cs:65-79` | Rohan mounted-wage-share block violates `gamemodels.md` rule. | #173 |
| 23 | P2 | CulturalFeats (Smithing) | Int truncation before career passive | `TaomSmithingModel.cs:46, 51` | `(int)` cast on culture-feat-modified cost before applying career passive multiplier. Result shifts by 1 vs same-line `ExplainedNumber.AddFactor` composition. | #173 |
| 24 | P3 | CareerSystem (enum) | 12 aspirational `PassiveEffectType` values | `PassiveEffectType.cs:31, 36-40, 43-49` | Zero producers + zero consumers. Plus `StealthBonus` has XML producers but no C# consumer. | — |
| 25 | P3 | SpecialResources×CareerSystem | PrefabExtensions target disjoint screens | `SpecialResourcePrefab.cs:11` + `CareerButtonPrefab.cs:7` | MapBar vs CharacterDeveloper — non-overlapping. Design confirmation. | — |
| 26 | P2 | SpecialResources | Discount applies to gate but not debit | `SpecialResourceService.cs:212-228` vs `:195-203` | `ClampUpgradeCount` uses career-discounted effective cost; `QueueUpgradeSpend` debits base cost. Player overpays special resources by the career-passive percentage. | #174 |
| 27 | P3 | SpecialResources | UI hint shows base cost, not effective | `PartyUpgradeResourceCheckHook.cs:23-27` | Display inconsistent with gate. | — |
| 28 | P3 | SpecialResources | `IOnPartyUpgradeResourceCheck` single consumer | `SpecialResourcesIoC.cs:13` | Interface not designed for multi-consumer. Document single-consumer constraint. | — |
| 29 | P3 | CareerSystem×SpecialResources | `TroopUpgradeCost` (gold) vs `CustomResourceUpgradeCostModifier` (resource) | `TaomPartyTroopUpgradeModel.cs:16-36` + `SpecialResourceService.cs:362-369` | Orthogonal cost dimensions. Confirmed clean. | — |
| 30 | P2 | SpecialResources (cross-ref) | `SecondaryInfoItems.Add` rule violation | `SpecialResourceMapBarMixin.cs:55` | **Already tracked in #167.** Phase 6 notes code-doc drift: feature doc claims "does NOT add to SecondaryInfoItems". | #167 (cross-ref) |
| 31 | P3 | TimeAcceleration×SpecialResources | PrefabExtension XPath non-overlap | `TimeAccelerationPrefab.cs:10-86` + `SpecialResourcePrefab.cs:11-33` | CenterPanel vs BottomInfoBar — non-overlapping. Design confirmation. | — |
| 32 | P3 | TimeAcceleration (cross-feature scope) | `IsExtraFastForwardActive` wrong signal isolated | `TimeAccelerationMixin.cs:54` | Phase 5 #11 P2 stands at feature level; cross-feature impact P3 (no other feature reads `SpeedUpMultiplier`). | — |
| 33 | P3 | TimeAcceleration×SiegeDefense | Deadline campaign-time-absolute | `SiegeDefenseService.cs:95` | Fast-forward does not compress siege deadlines (correct behavior). | — |
| 34 | P3 | TimeAcceleration×SpecialResources | Mixin target VMs differ | `TimeAccelerationMixin.cs:11` (`MapTimeControlVM`) + `SpecialResourceMapBarMixin.cs:12` (`MapInfoVM`) | No collision. Design confirmation. | — |
| 35 | P3 | TimeAcceleration | `SpeedUpMultiplier` sole owner | `TimeControlAdapter.cs:13-17` | Zero references to `Campaign.Current.SpeedUpMultiplier` in other features. Design confirmation. | — |
| 36 | P2 | TimeAcceleration (Phase 5 re-char) | `MapInfoVM.CreateItems()` call frequency | cluster-ui finding #7 | v1.4 decompile shows `RefreshValues` does NOT call `CreateItems`. v1.3.15 ilspycmd verification required before closing #167. | #167 (re-char) |
| 37 | P3 | FactionMap×CharacterCreation | Different CC stages | `FaceGenVM_Refresh_RaceFilter_Patch.cs:8` + `CultureStageView_Constructor_Patch.cs:37` | No lifecycle overlap. Add a clarifying comment to both patches. | — |
| 38 | P3 | FactionMap (self) | `ResetSession` does not run on non-constructor entry | `CultureStageViewCreatedHook.cs:56` | If constructor patch is bypassed, stale `_allInstances` persists. Add `OnDisconnectedFromParent` cleanup. | — |
| 39 | P3 | FactionMap (Phase 5 #15 downgrade) | `_allInstances` LateUpdate vs Render | `PolygonWidget.cs:680-686, 593` | Both serial (Gauntlet single-threaded). DOWNGRADE from P2. | — |
| 40 | P3 | FactionMap (Phase 5 #16 downgrade) | `HoveredFactionName` dual-write | `PolygonWidget.cs:636-648, 740` | Same engine thread; semantic smell only. DOWNGRADE from P2. | — |
| 41 | P3 | FactionMap (Phase 5 #25 resolved) | WidgetFactory registration | `WidgetInfo.CollectWidgetTypes()` | Engine assembly-scan handles all 4 widget types. RESOLVED. | — |
| 42 | P2 | FactionMap (self) | `_factionVM` static stale on CC backward nav | `CultureStageViewCreatedHook.cs:25` + `CultureStageViewFinalizeHook.cs:7-8` | If Finalize fires after next Constructor, old VM is briefly alive — `OnTick` could operate on stale state for 0–1 frames. | #175 |
| 43 | P2 | FactionMap (self) | `_pendingPins` not cleared in `ResetSession` | `PolygonWidget.cs:85-86, 120-130` | Static pin list survives CC re-entry; multi-render in first few frames possible. | #175 |
| 44 | P3 | Global sweep | OrderOfBattleHeroItemVM dual-Postfix | `OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:11` + `Patch35_OOBHeroItem_RefreshValues.cs:11` | BannerColorPersistence × CompanionTactics-Roles. Different methods on same VM. Borderline documentary. | — |

(P1+P2 = 15 active findings → 6 issues; the 2 P1s are 1 net-new (#22 / Career F3 in issue #173) + 1 cross-ref (#14 / Banner F1 in existing #122).)

## Per-pair reports

### 1. SmartCavalryAI × MixedFormations × CompanionTactics

**Shared touchpoint:** `Formation.SetMovementOrder(MovementOrder)` (Patch31 + Patch35 share the deferred `Patch_MissionTime_SetMovementOrder` category) and `Formation.GetOrderPositionOfUnit` (Patch30, separate category, separate worker-thread call path).

**Findings:** 0 P1, 2 P2, 3 P3.

- **F1 (P2):** Handshake between SmartCavalryAI and MixedFormations is explicit in two `RepresentativeIsCavalry` guards in `FormationLayoutService.cs` (lines 74, 191) — but has zero cross-feature tests. A refactor of either guard can silently re-introduce the original P1 (Codex 2026-05-06 charge-line overwrite). Recommended: add `ComputeUnitPlanePosition_WhenFormationIsCavalry_ReturnsNull` and `IsMixedFormation_WhenFormationIsCavalry_ReturnsFalse` tests with comments naming the cross-feature dependency.
- **F2 (P2):** `CavalryChargeService._states` is a plain `Dictionary` with no lock. Its sister `FormationLayoutService._layoutByFormation` holds an explicit `_lock` citing the `_MT`-suffix pattern. Asymmetry is a code-review smell; latent race if `SetMovementOrder` is ever invoked off-thread. v1.4 decomp shows no `_MT` caller of `SetMovementOrder` today — confirm via ilspycmd on installed v1.3.15 DLL before closing.
- **F3 (P3):** Patch35 `CancelStanceOnMove` doesn't honor `SmartCavalryRecursionGuard.IsSuppressed`; clears player-set stances during SmartCavalryAI-internal `IssueStop` calls. Cosmetic only (stances are display-only). Simplicity Criterion favors documenting over fixing.
- **F4 (P3):** `FormationAdapter._alignmentScratch` is a static shared `List<float>` used by `IsAligned`. Currently safe (main-thread caller only); latent if a future caller is off-thread. Recommended: `[ThreadStatic]` field.
- **F5 (P3):** Shared category has no `[HarmonyAfter]` annotation; Patch31 vs Patch35 ordering is alphabetical-by-class-name. Disjoint writes today; future patch with name sorting between 31 and 35 would silently reorder.

**Precedence verdict:** Explicit (SmartCavalryAI owns cavalry, MixedFormations defers via `RepresentativeIsCavalry` guards). CompanionTactics' Patch35 writes to a display-only stance dictionary disjoint from both other features.

**Issue:** #170 (audit-impl).

### 2. CulturalFeats × RevoltTuning

**Shared touchpoint:** `TaomSettlementLoyaltyModel` is the single class where the features meet. RevoltTuning supplies threshold/penalty overrides via `IRevoltTuningConfigProvider`; CulturalFeats supplies per-culture per-tick loyalty bonuses via `result.Add(...)`.

**Findings:** 0 P1, 0 P2, 3 P3.

- **F1 (P3):** `CalculateLoyaltyChange` has 5 inline `if (HasFeat) { result.Add(...) }` branches — violates `gamemodels.md` "no inline branching in GameModel body" rule. Extract to `ILoyaltyFeatService`.
- **F2 (P3):** Constructor snapshots `revoltTuning.GetConfig()` rather than holding the provider. Safe today (Singleton+Lazy). Add a clarifying comment.
- **F3 (P3):** No behavioral tests for `TaomSettlementLoyaltyModel`. RevoltTuning provider has 12 tests; the consumer side is bare.

**Precedence verdict:** Orthogonal. RevoltTuning owns thresholds + culture penalties; CulturalFeats owns the per-tick bonus additions. Composition is commutative.

**Issue:** None (all P3 design notes).

### 3. CharacterCreation × HeroRace × RaceAge

**Shared touchpoint:** `CharacterObject.Race` on `Hero` objects, mutated by `Patch3_SetRace`, `Patch5_FaceGen`, `Patch9_RaceFilter`, `Patch29_CCBodyProperties` (+ Patch1, 2, 4), persisted by `RacePersistenceService`, consumed by `TaomAgeModel`, `TaomPregnancyModel`, `RaceAgeBehavior`.

**Findings:** 0 P1, 2 P2, 3 P3.

- **F1 (P2):** `RacePersistenceService.CaptureHeroRaces` stores raw `int` race IDs with only `> 0` guard. `RaceAgeService.GetEntry(badId)` falls through `RaceManager.GetRaceNameFromId` to the `"human"` fallback and caches the wrong entry permanently for that session. Saves with disabled race mods silently lose elven immortality / dwarf aging. Fix: add `IRaceManager.IsValidRaceId` gate in `RestoreHeroRaces`.
- **F2 (P2):** Patch20 and Patch29 both Prefix `CharacterCreationNarrativeStageView.RefreshAgentVisuals` — different fields (Race vs BodyProperties), but ordering is implicit-by-Harmony registration. Works today; document the ordering or add `[HarmonyAfter]`.
- **F3 (P3):** `PlayerBodyPropertiesAdapter.UpdatePlayerCharacterBodyProperties` fires `OnPlayerBodyPropertiesChanged` with `playerChar.Race=0` during culture-selection (before Patch9 forces the race on FaceGen entry). Subscriber-dependent severity.
- **F4 (P3):** `TaomHeroCreationModel.GetCharacterTemplateForOffspring` returns sealed `CharacterObject` — TaleWorlds-forced; document as ADR-007 constraint.
- **F5 (P3):** `RaceAgeBehavior.OnDailyTick` and `TaomPregnancyModel` both read `hero.Race` in same tick. Read-only additive. No test asserts consistency.

**Precedence verdict:** Explicit on the happy path (CC finalize → capture → restore → daily read). Gap: persistence→consumption boundary trusts integer validity without a check.

**Issue:** #171 (audit-impl).

### 4. BannerColorPersistence × BannerInjection × Patch24_BannerDriftGuard

**Shared touchpoint:** Clan/Kingdom banner color state mutated by Patch23 (17 patches), Patch24 (2 drift-guard patches), and Patch6 (BannerInjection editor exclusion).

**Findings:** 1 P1, 3 P2, 2 P3.

- **F1 (P1):** `MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize` is missing from `SubModule.OnSubModuleLoad` — `_service` + `_heroAdapter` remain null → silent no-op on world-map party icons. **Already tracked in #122.** Phase 6 confirms: a single `Initialize` call (between lines 176-177) is the fix.
- **F2 (P2):** `Clan_UpdateBannerColorsAccordingToKingdom_Patch.Prefix` blocks ALL clans, not just the player. NPC clans that change kingdoms never get banner color updates. Fix: gate on `__instance == Clan.PlayerClan`.
- **F3 (P2):** `TargetMethod()` resolves a private method via string literal with no null-guard log. If TaleWorlds renames the method, Harmony silently skips. Add a logger injection and warn.
- **F4 (P3):** Banner editor session has Patch23 (tick), Patch24 (UpdateBannerColor Postfix), and Patch6 (OnDone Postfix) firing in sequence on different targets. Cleanly additive. Worth documenting the full call chain.
- **F5 (P2):** `BannerInjection.InjectBanners` fires on `OnNewGameCreated`; Patch24 activates later in `OnGameInitializationFinished`. Works today because injection happens after vanilla drift, but the asymmetry vs load-path (where Patch24 IS active) is undocumented.
- **F6 (P3):** `PartyCharacterVM_GetCharacterCode_Patch` accesses `Clan.PlayerClan` directly while sibling patches use the adapter. Inconsistent style; route through adapter.

**Precedence verdict:** Additive with two structural gaps (party icon silent inert; all-clan drift block). Patch24 protects what Patch23 reads; BannerInjection exclusion defers to player edits.

**Issue:** #172 (audit-impl). F1 stays attached to existing #122.

### 5. CareerSystem × TroopProgression (via `CareerPassiveHelper`)

**Shared touchpoint:** `CareerPassiveHelper.ApplyFactor` is called by 8 CulturalFeats models + 1 TroopProgression model + 2 CareerSystem models. The pattern: `base.X()`, then helper appends a career multiplier via `result.AddFactor(magnitude, CareerText)`.

**Findings:** 1 P1, 3 P2, 1 P3.

- **F1 (P2):** `CareerPassiveHelper.GetService()` calls `IoC.Resolve<ICareerPassiveService>()` from a static helper invoked inside GameModel bodies — not a boundary class per `feedback_no_service_locator_in_services`. Static caches are non-volatile. Fix: inject the service into each GameModel constructor.
- **F2 (P2):** `CareerPassiveService._cache` is a plain Dictionary. `RefreshCache` calls `Clear()` then rebuilds; `GetPassiveMagnitude` is invoked from GameModel paths including `GetTotalWage` (AI desertion thread) and `GetPartyMemberSizeLimit`. Race risk. Fix: snapshot-swap with `Volatile.Write`.
- **F3 (P1):** `TaomPartyWageModel.GetTotalWage` lines 65-79 contain `foreach` + `if` + arithmetic in the GameModel override body. Violates `gamemodels.md` rule. Extract Rohan mounted-share calc to `ITroopCostService.GetMountedWageShareFactor`.
- **F4 (P2):** `TaomSmithingModel` applies an `(int)` truncation to culture-feat-modified cost BEFORE applying career passive — shifts result by 1 vs same-line `ExplainedNumber` composition. All other models compose in `ExplainedNumber` end-to-end.
- **F5 (P3):** 12 `PassiveEffectType` enum values have zero producers + zero consumers; `StealthBonus` has XML producers but no C# consumer. Per `feedback_no_aspirational_enum_values`. Remove or wire.

**Precedence verdict:** Consistent and additive across all 10 call sites: `base.X()`, then helper. `ExplainedNumber.AddFactor` is multiplicative on the running result; order between culture feats and career passive is commutative.

**Issue:** #173 (audit-impl).

### 6. SpecialResources × CareerSystem (inventory upgrade screen)

**Shared touchpoint:** Both touch the party upgrade screen — SpecialResources via Patch26 (`PartyScreenLogic` + `PartyCharacterVM.InitializeUpgrades`), CareerSystem via `TaomPartyTroopUpgradeModel` (gold cost) and the `CustomResourceUpgradeCostModifier` passive consumed inside `SpecialResourceService`.

**Findings:** 0 P1, 1 P2 net-new (+ 1 P2 cross-ref to #167), 4 P3.

- **F1 (P3):** PrefabExtensions target disjoint screens (MapBar vs CharacterDeveloper). No collision.
- **F2 (P2):** Career discount applies to the gate decision (`ClampUpgradeCount` calls `GetEffectiveUpgradeCost`) but NOT to the actual resource debit (`QueueUpgradeSpend` queues base cost). Player with -30% modifier upgrades at gate-cost 3.5 but debit at 5 → ends at 0 instead of 3. Fix: `QueueUpgradeSpend` must call `GetEffectiveUpgradeCost` too.
- **F3 (P3):** UI hint displays base cost via `IOnPartyUpgradeResourceCheck.GetUpgradeCost`, not the career-discounted effective cost.
- **F4 (P3):** `IOnPartyUpgradeResourceCheck` has a single Singleton consumer — not designed for multi-consumer. Document the single-consumer constraint.
- **F5 (P3):** `TroopUpgradeCost` (gold) and `CustomResourceUpgradeCostModifier` (special resource) are orthogonal cost dimensions. Stack cleanly.
- **F6 (P2 cross-ref to #167):** `SpecialResourceMapBarMixin.SecondaryInfoItems.Add` rule violation. Feature doc says "does NOT add to SecondaryInfoItems"; code does. Code-doc drift.

**Precedence verdict:** No true cross-feature handshake conflict. The internal SpecialResources cost-inconsistency bug (F2) is triggered by the career integration but is a within-feature bug.

**Issue:** #174 (audit-impl).

### 7. TimeAcceleration × MapBar

**Shared touchpoint:** Multiple TAOM features extend the vanilla MapBar via UIExtenderEx. TimeAcceleration adds 5 PrefabExtensions on the CenterPanel; SpecialResources adds 1 on BottomInfoBar; SiegeDefense uses `CampaignTime`-absolute deadlines (no MapBar injection).

**Findings:** 0 P1, 0 P2 net-new (1 P2 re-characterization risk), 5 P3 design confirmations.

- **F1 (P3):** PrefabExtension XPath targets non-overlapping (`MapCurrentTimeVisualWidget` vs `ListPanel[@Id='BottomInfoBar']`).
- **F2 (P3 cross-feature scope):** `IsExtraFastForwardActive` wrong-state-signal isolated to mixin. Phase 5 #11 P2 stands at feature level; cross-feature impact P3 (no other feature reads `SpeedUpMultiplier`).
- **F3 (P3):** SiegeDefense deadlines are campaign-time-absolute. Fast-forward doesn't compress them. Correct behavior.
- **F4 (P3):** TimeAcceleration mixes into `MapTimeControlVM`; SpecialResources mixes into `MapInfoVM`. Different VM types → no mixin-target collision.
- **F5 (P3):** `Campaign.Current.SpeedUpMultiplier` is exclusively owned by TimeAcceleration. Zero references in other features.
- **F6 (P2 re-characterization of cluster-ui #7):** v1.4 decompile shows `MapInfoVM.RefreshValues()` does NOT call `CreateItems()`. v1.3.15 ilspycmd verification required before closing #167.

**Precedence verdict:** All Additive / Explicit. Non-overlapping DOM regions; disjoint VM types; sole ownership of `SpeedUpMultiplier`.

**Issue:** None (no net-new P1/P2; F6 attaches to existing #167 as a re-characterization task).

### 8. FactionMap × CharacterCreation

**Shared touchpoint:** `CharacterCreationCultureStage.xml` mounts FactionMap widgets; CharacterCreation patches different CC stages (`Patch9_RaceFilter` hooks `FaceGenVM.Refresh`, a LATER stage).

**Findings:** 0 P1, 2 P2 net-new, 4 P3 (incl. 2 P2-from-Phase-5 downgrades + 1 P3-from-Phase-5 resolved).

- **F1 (P3):** `Patch9_RaceFilter` and FactionMap operate on different CC stages — no lifecycle overlap. Add clarifying comments.
- **F2 (P3):** `ResetSession()` is called from constructor Postfix — correct for the happy path; risk if constructor patch is bypassed. Add `OnDisconnectedFromParent` cleanup as belt-and-suspenders.
- **F3 (P3, was P2 #15):** `_allInstances` LateUpdate write vs Render read — **DOWNGRADE: both serial.** Gauntlet threading verdict below settles this.
- **F4 (P3, was P2 #16):** `HoveredFactionName` dual-write — **DOWNGRADE: same engine thread; semantic smell only.** Move the pulse-name write to LateUpdate.
- **F5 (P3 resolved, was #25):** `WidgetInfo.CollectWidgetTypes()` assembly-scan handles all 4 FactionMap widget types. **RESOLVED.**
- **F6 (P2):** `_factionVM` static field — if Finalize fires after next Constructor (CC backward nav), old VM is briefly alive while new session initializes. 0–1 frame stale `OnTick`. Fix: call `Cleanup()` as first statement in `OnCreated` before `ResetSession`.
- **F7 (P2):** `_pendingPins` static List not cleared in `ResetSession()`. CC re-entry leaves stale pin data; first-few-frame pin-draw artifact possible. Fix: add `_pendingPins.Clear()` to `ResetSession`.

**Gauntlet threading verdict (Phase 5 carryover):** `GauntletLayer.LateUpdate` and `RenderTick` are called sequentially from the engine main loop. `EventManager.LateUpdate` and `Render` iterate widgets serially. `PolygonWidget` does NOT override `OnParallelUpdate` → never enters the parallel container. **Strictly single-threaded for TAOM's widget types.** Phase 5 findings #15 and #16 downgrade from P2 to P3.

**Precedence verdict:** No cross-feature handshake needed (different CC stages). Internal FactionMap static-state management has two implicit-ordering gaps (F6, F7) at CC re-entry.

**Issue:** #175 (audit-impl).

### 9. Global same-method-patch sweep

**Procedure:** Grep `[HarmonyPatch(typeof(X), ...)]` across all `Main/Features/**/Hooks/*.cs` + 7 manual `_harmony.Patch(AccessTools.Method(...))` sites. Group by `(TaleWorlds type, method name)`. Plus adapter write-conflict scan (`IoC.Resolve<IXxxAdapter>` ≥ 2 features both writing). Plus GameModel shared-parameter scan.

**Findings:** 0 P1, 0 P2 net-new, 1 P3 borderline (A.collision-3).

- **Sweep A (same-method patches):** 9 grouped collisions enumerated. 1 net-new borderline P3 (A.collision-3: `OrderOfBattleHeroItemVM.RefreshInformation` BannerColorPersistence + `RefreshValues` CompanionTactics-Roles — different methods on same VM; not a true collision today but worth documenting). Remaining 8 either covered by named-pair agents or same-type-different-method clusters with no overlap.
- **Sweep B (adapter write-conflicts):** Zero. Every `IXxxAdapter` in TAOM is single-owner.
- **Sweep C (GameModel shared-parameter):** Zero same-model collisions. 9 shared-parameter chains all funnel through `CareerPassiveHelper` (Pair Agent #5 scope). One outlier: `TaomPartyHealingModel` uses direct `IoC.Resolve` instead of the helper — pattern divergence noted.

**Issue:** None (only borderline P3 documentary).

## Cross-cuts

### Implicit-precedence patterns

Three places in TAOM rely on implicit ordering that works today but has no annotation:
1. `Patch_MissionTime_SetMovementOrder` shared category — Patch31 fires before Patch35 by alphabetical class name only (Finding #5).
2. Patch20 vs Patch29 on `CharacterCreationNarrativeStageView.RefreshAgentVisuals` — order set by Harmony registration sequence in `SubModule.cs` (Finding #10).
3. `BannerInjection.OnNewGameCreated` vs `Patch24_BannerDriftGuard` activation in `OnGameInitializationFinished` — relies on TaleWorlds event sequence (Finding #18).

Each could be hardened with `[HarmonyAfter]` / `[HarmonyBefore]` or with explicit event-order assertions, but the work is largely documentation rather than fix. Track as Phase 9 hygiene.

### Static-helper integration points

`CareerPassiveHelper.ApplyFactor` is the dominant cross-feature integration point (11 call sites: 9 CulturalFeats models + 1 TroopProgression + 1 CareerSystem). The helper has two issues (Findings #20, #21) that need fixing together — both motivate the same redesign: inject `ICareerPassiveService` into each consuming GameModel constructor, drop the static cache + IoC.Resolve. One outlier (`TaomPartyHealingModel`) already uses direct `IoC.Resolve`; the Phase 9 fix should unify.

`CareerPassiveHelper` lives in `TAOM.Features.CareerSystem` but 8 of 11 callers are in `TAOM.Features.CulturalFeats`. Consider moving to `TAOM.Core` or `TAOM.Shared` when fixing (Open question on draft 5).

### Gauntlet threading verdict

`GauntletLayer.LateUpdate` and `RenderTick` are strictly sequential on the engine main thread (v1.4 decomp confirmed; v1.3.15 ilspycmd not yet verified but interface stable). Widget `OnParallelUpdate` is the only path to worker threads; no TAOM widget overrides it. **Phase 5 findings #15 and #16 downgrade from P2 to P3.** Future contributors adding `OnParallelUpdate` to any FactionMap widget will need to address the `_allInstances` and `_pendingPins` static-state assumptions.

### Thread-safety asymmetry (latent class of bug)

Three findings (#2, #4, #21) share a shape: a TAOM service maintains shared state without a lock while its sibling/peer holds one. The risk is currently dormant because the specific call paths happen to be main-thread-only, but `_MT`-suffixed engine helpers exist on adjacent types. Recommended Phase 9 sweep: grep `private readonly Dictionary<` and `private static readonly List<` across `Main/Features/**` and audit each against the call-graph for off-thread reachability.

## GitHub issues opened

| # | Title | Pair | Findings |
|---|---|---|---|
| #170 | audit(impl): SmartCavalryAI × MixedFormations × CompanionTactics — handshake test gap + threading asymmetry | 1 | F1, F2 |
| #171 | audit(impl): CharacterCreation × HeroRace × RaceAge — stale race ID persistence + Prefix ordering coupling | 3 | F1, F2 |
| #172 | audit(impl): BannerColorPersistence × BannerInjection × Patch24 — all-clan drift block + TargetMethod null-guard + event ordering | 4 | F2, F3, F5 (F1 stays on #122) |
| #173 | audit(impl): CareerSystem × TroopProgression via CareerPassiveHelper — service locator + race + inline foreach + int truncation | 5 | F1, F2, F3, F4 |
| #174 | audit(impl): SpecialResources × CareerSystem inventory upgrade — career discount not applied to resource debit | 6 | F2 |
| #175 | audit(impl): FactionMap × CharacterCreation widget lifecycle — `_factionVM` stale + `_pendingPins` bleed on CC re-entry | 8 | F6, F7 |

Cross-references (no new issue):
- #122 — BannerColorPersistence `MobilePartyVisual` Initialize miss (Finding #14)
- #167 — SpecialResources `SecondaryInfoItems.Add` (Finding #30; plus #36 re-characterization task)

## Phase 6 complete

Phase 6 produced:
- 41 findings across 8 collision pairs + 1 global sweep.
- 2 P1 (1 net-new, 1 cross-ref to existing #122).
- 13 P2 (4 net-new + others within issue groupings).
- 26 P3 (design confirmations, downgrades from Phase 5, documentary notes).
- 6 net-new GitHub issues opened (`audit-impl` label).
- 2 Phase 5 carryovers settled: Gauntlet single-threaded → findings #15, #16 downgrade to P3; #25 WidgetFactory registration resolved for FactionMap.

The audit branch has now opened 36+ `audit-*` issues across Phases 1–6 (#121, #122, #123–#131, #134–#148, #149–#164, #165–#169, #170–#175). Phase 9 (Triage + Fix) queue is large; expect to span multiple sessions.

## Phase log

| Date | Phase | Output | Findings |
|---|---|---|---|
| 2026-05-13 | 6 | `cluster-cross-feature.md` + issues #170–#175 | 2 P1, 13 P2, 26 P3 = 41 total across 8 pairs + global sweep |
