# Cluster: CampaignBehaviors — Phase 3

Last updated: 2026-05-13
Inputs: [feature-manifest.md](feature-manifest.md) (16 features × 19 behaviors)

## Pre-flight gap (prerequisites that were skipped)

This phase ran **without** the Phase 1 wiring matrix or any Phase 2 cluster review preceding it. The user invoked Phase 3 directly using the `session-prompts.md` template; the session preserved the audit charter by deriving scope from `feature-manifest.md`'s `Behavior` column and verifying wiring inline per agent.

Consequences for this phase:

- Wiring sanity findings are recorded under the `audit-impl` label (per the user's instruction in the Phase 3 prompt) rather than `audit-wiring`. A future Phase 1 backfill should re-classify if needed.
- The Messengers wiring fix (Main/IoC.cs + Main/SubModule.cs, 7 lines from the original Phase 0 fix) is **still uncommitted** on this branch. Recommend a dedicated commit + retroactive issue per CLAUDE.md mandate before further audit work.

## Recurring patterns across the cluster (synthesis)

The 16 reviews surfaced **5 recurring TAOM bug classes** in CampaignBehavior code. Naming them upfront because they explain the bulk of the findings and the fixes will be highly repetitive:

| # | Pattern | Affected features (count) | Representative file |
|---|---|---|---|
| **R1** | `Reuse.Singleton` service field never reset on new-campaign-in-same-process | NamedCompanions, HeroRace, Diplomacy (WarOfTheRing), RaceAge, Siege, SpecialResources, FiefManagement, CharacterCreation (8) | `RacePersistenceService.cs:13` |
| **R2** | `SyncData` empty body or unconditional save-side mutation | Siege (empty), CareerPersistence (mutates on save), SpecialResources (clamps on save) | `SiegeDefenseBehavior.cs:29` |
| **R3** | Config provider deserializes without semantic validation (missing `FiniteFloatValidator`, no ordering invariants, no range checks) | InitialChildGeneration, RaceAge, Diplomacy, Siege, SpecialResources, StartupResources, CareerSystem (7) | `RaceAgeConfigProvider.cs:34` |
| **R4** | Lookup-with-fallback called without input validation (Lookup→default-name pattern) | RaceAge (`GetRaceNameFromId`) | `RaceAgeService.cs:47-49` |
| **R5** | Load-path mutation lacks Entity State Matrix coverage (NamedCompanions Review #23 class) | NamedCompanions (Prisoner + Fugitive states), SpecialResources (desertion-on-load), TaomPregnancyModel (state pre-checks) | `NamedCompanionAdapter.cs:27-31` |

These are the **systemic** lessons. Phase 9's fix execution should consider them as cross-cutting work batches rather than per-feature one-offs (e.g., a single PR adding `OnSessionLaunched`-bound `Reset()` calls across all R1-affected services would close 8 P1 findings at once).

## Master matrix

Per feature: ✅ green = correct; ⚠ degraded = one or more P2 findings; ❌ broken = one or more P1 findings; — = N/A.

| Feature | RegisterEvents | SyncData | OnSessionLaunched re-init | OnGameLoaded idempotent | Singleton state-reset | Findings |
|---|:--:|:--:|:--:|:--:|:--:|---|
| BannerInjection | ✅ | ❌ R1+R2 (stale + null guard wrong) | — | ⚠ Perf | ❌ | 2 P1 + 2 P2 + 1 P3 |
| CareerSystem | ✅ | ❌ R2 (unconditional restore on save) | ✅ (Campaign + Persistence) | ⚠ | ⚠ R1 (AbilityService) | 1 P1 + 3 P2 + 2 P3 |
| CharacterCreation | ✅ | — | — | — (new-game only) | ⚠ R1 (CareerMenuService field) | 0 P1 + 3 P2 + 1 P3 |
| CompanionTactics | ✅ | ❌ container-collision risk | — | ⚠ silent-catch | ✅ | 2 P1 + 1 P2 + 2 P3 |
| Diplomacy (Diplomacy) | ✅ | — | ✅ idempotent guard | — | ✅ | 1 P1 + 2 P2 + 2 P3 (combined w/ WotR) |
| Diplomacy (WarOfTheRing) | ✅ | ❌ R2 (CurrentPhase unsaved) | ⚠ replays transitions | ❌ | ❌ R1 | (combined above) |
| EquipPresets | ✅ | ✅ | — | ✅ orphan-prune | ✅ | 1 P1 + 1 P2 + 4 P3 (doc rot) |
| FiefManagement | ✅ | — (transient state) | ✅ Reset() | ✅ (UI-only) | ⚠ R1 (Reset incomplete) | 1 P1 + 2 P2 + 2 P3 |
| HeroRace | ✅ | ✅ format-clean | ✅ via OnSessionLaunched | ❌ R1 (stale map) | ❌ R1 | 1 P1 + 2 P2 + 2 P3 |
| InitialChildGeneration | ✅ | — (new-game only) | — | — | ✅ | 2 P1 + 2 P2 + 2 P3 |
| Messengers | ✅ | ✅ | ✅ partial reset | ✅ | ⚠ flag-stale | 2 P1 + 2 P2 + 2 P3 |
| NamedCompanions | ✅ | — (delegated) | — | ❌ R5 (Prisoner+Fugitive) | ❌ R1 (`_spawned`) | 3 P1 + 2 P2 + 1 P3 |
| QuickActions | ✅ | ✅ but overwritten on load | — | ❌ R2 (unconditional MCM-wins) | ✅ | 0 P1 + 2 P2 + 1 P3 |
| RaceAge | ✅ | — (delegated) | — | ⚠ R4 (missing validate-before-lookup) | ❌ R1 (`_raceIdCache`) | 2 P1 + 2 P2 + 1 P3 |
| Siege | ✅ | ❌ EMPTY — events lost | — | ❌ | ❌ R1 (`_activeEvents`) | 2 P1 + 3 P2 + 0 P3 |
| SpecialResources | ✅ | ❌ R2 (clamps all keys w/ wrong cap) | ⚠ legacy seed fires | ⚠ R5 (desertion on load) | ⚠ event leak | 3 P1 + 3 P2 + 1 P3 |
| StartupResources | ✅ | — (new-game only) | — | — | ✅ | 1 P1 + 1 P2 + 1 P3 |

**Totals:** 24 P1, 33 P2, 25 P3 (and out-of-scope notes) — 82 findings across 16 features. 16 GitHub issues will be opened (one per feature, body containing all P1+P2 + a checklist).

## Special-target verdicts

### Messengers (newly wired — full review)
The Codex #34 reset path (`_lastSessionStarter` / `_justLoadedFromSave`) is **structurally present and partially correct**, but contains a subtle defect: `_justLoadedFromSave = false` is set inside the same `if (_lastSessionStarter != starter)` block it controls, so the flag is only reset when the starter changes — never on within-campaign save-load cycles. No direct runtime consequence today (the flag is not consumed outside that block) but a contract violation that will bite if future code reads the flag. Plus a real P1 leak: `_processingArrivedMessenger` stays `true` forever if `OpenConversationMission` returns null. Verdict: **2 P1 + 2 P2 + 2 P3, all latent or narrow-scope.** Not a Messengers-class crash, but the singleton-reset story isn't airtight.

### NamedCompanions (Review #23 regression check)
**Original Review #23 bug IS fixed.** The traveling-with-party state is now correctly handled by `IsRecruitedOrInParty` checking `PartyBelongedTo != null`. The test `EnsureCompanionsPlaced_RecruitedCompanion_SkipsPlacement` covers it. **However, two new state-class bugs were introduced:**
- **Prisoner companion** (`PartyBelongedTo=null`, `PartyBelongedToAsPrisoner != null`, traveling with mobile captor) bypasses both guards and gets force-placed, breaking prisoner state.
- **Fugitive companion** (`HeroState=Fugitive`, all part fields null) bypasses both guards and gets force-placed every load.
Plus a P1 singleton bug: `_spawned` field never reset → campaign 2 in same process gets zero spawned companions. Verdict: **the Entity State Matrix rule was applied to the original 4 states but not extended to the 6 states the rule itself names.** This is the exact pattern the rule exists to prevent.

## Per-feature findings

> Format: each feature shows **Wiring sanity**, **Behavior surface**, **Findings** (P1/P2/P3), **Confidence**. Out-of-scope notes (Phase 4 GameModel/Patch concerns) are flagged.

### Messengers ✅ wiring / ⚠ findings

- **Wiring:** AddBehavior @ `Main/SubModule.cs:368`, IoC @ `Main/IoC.cs:90` (`MessengerIoC`), all Singleton, all 5 ctor deps registered.
- **SyncData key:** `_taom_messengers` → `Dictionary<string, string>`.
- **P1** — `_justLoadedFromSave = false` is inside the `if (_lastSessionStarter != starter)` block at `MessengerCampaignBehavior.cs:154-165`. After the first save-load cycle, the flag remains `true` forever within the campaign. Today no consumer reads it post-block; future authors will be misled. Fix: move the reset OUTSIDE the gate, unconditional.
- **P1** — `_processingArrivedMessenger` permanently stuck `true` if `OpenConversationMission` returns null at `MessengerCampaignBehavior.cs:389`. `_currentMission?.AddListener(this)` no-ops; `OnEndMission` never fires; flag never clears. All future arrived-messenger processing silently blocked for the campaign session. Fix: null-guard after `AddListener`, reset flag + clear `_activeMessenger` if mission null.
- **P2** — `CampaignEvents.TickEvent.ClearListeners(this)` at `MessengerCampaignBehavior.cs:445` removes ALL `TickEvent` listeners on `this`, not just the specific one. Fragile pattern. Fix: use `RemoveNonSerializedListener(this, CleanUpSettlementEncounter)`.
- **P2** — Dialog re-registration on a fresh `starter` instance within same campaign would silently shadow prior registrations. Fix: track `_dialogsRegistered` independently of `_lastSessionStarter`.
- **P3** — `MessengerEncyclopediaMixin` uses `IoC.Resolve<>` in ctor (UIExtenderEx framework constraint, document the exemption inline).
- **P3** — Wanderer with no `BornSettlement` produces self-encounter at `MessengerCampaignBehavior.cs:350-365`. Cosmetic broken; no crash.
- **Confidence:** High on both P1s, Medium on P2s (depends on TaleWorlds starter lifecycle), High on P3s.

### BannerInjection ✅ wiring / ❌ load-path stale state

- **Wiring:** AddBehavior @ `SubModule.cs:230`, IoC @ `IoC.cs:60`, all Singleton.
- **SyncData key:** `_taom_playerModifiedBanners` → `List<string>`.
- **P1** — `_playerModifiedIds` (singleton) carries stale exclusions across campaigns at `BannerExclusionService.cs:8`. New campaign 2 inherits campaign 1's exclusion set; TAOM canon banners not re-injected on excluded entities. Fix: `OnNewGameCreatedEvent` clear, or unconditional `_playerModifiedIds = new HashSet<string>(list ?? Empty)`.
- **P1** — `SyncData` on a save predating this feature retains in-memory state because `list` is initialized from current `_playerModifiedIds` and the `if (list != null)` branch re-assigns identical state. Fix: initialize `list` to `null` or check `dataStore.IsSaving`.
- **P2** — `InvalidateVisuals` performance: batched update would reduce frame stutter on load in large playthroughs.
- **P2** — `BannerConfigProvider` re-parses XML+XSLT on every call to `GetKingdomBannerKeys()` / `GetClanBannerKeys()`. Singleton with no cache fields. Fix: lazy-initialize private dict caches.
- **P3** — `GauntletBannerEditorScreen_OnDone_Patch._hook` static field uses null-conditional, silently swallowing player banner edits if `Initialize` not yet called. Fix: assert + log.
- **P3** — Out of Phase 3 scope: `ClanBannerAdapter.SetBanner` constructs `new Banner(bannerCode)` directly — verify v1.3.15 setter side effects via ilspycmd.
- **Confidence:** High on P1s (mechanical SyncData defect), Medium-High on P2s.

### CharacterCreation ⚠ ADR violations only

- **Wiring:** AddBehavior @ `SubModule.cs:234`, IoC @ `IoC.cs:64`, behavior `new`'d, services Singleton.
- **SyncData key:** none (correct, no per-save state).
- **Behavior is new-game-effective only** — listener is `OnCharacterCreationInitializedEvent` which only fires for new games (verified via decompiled `SandBoxGameManager.cs:125-146`).
- **P2** — `CharacterCreationContentService` uses sealed `Hero`/`MobileParty`/`Settlement`/`MBObjectManager` directly (ADR-007). 7 violation sites at lines 166-176, 218, 235, 327-332, 347. Service is untestable. Fix: extract `IPlayerHeroAdapter`, `IPlayerPartyAdapter`, `ISettlementAdapter`, `ICultureCreationDataProvider`.
- **P2** — `IoC.Resolve<ICareerCreationHandler>()` and `IoC.Resolve<ICareerRegistry>()` inside service body at lines 218, 235 (Review #26 violation pattern). Fix: constructor inject both.
- **P2** — `CareerMenuService.SelectedCareerStringId` is mutable singleton field cleared only inside `RegisterCareerMenu`; if user starts → abandons → restarts CC without menu rebuild, stale value carries forward. Fix: clear in `OnSessionLaunched` or via dedicated `ResetSession()`.
- **P3** — `MobileParty.MainParty.Position =` at `TeleportToStartingSettlement` (line 327-332) lacks null-guard on `MobileParty.MainParty`. Reachable NRE at CC finalize.
- **Confidence:** High on all (ADR violations + missing null-guard are mechanical).

### InitialChildGeneration ❌ config validation gap + crash path

- **Wiring:** AddBehavior @ `SubModule.cs:238`, IoC @ `IoC.cs:65`, all Singleton.
- **SyncData:** empty body (correct — new-game-only one-shot).
- **Trigger:** `OnNewGameCreatedPartialFollowUpEvent` at `index == 0` (P2 below).
- **P1** — Config double fields (`FemaleRatio`, `ChildCountMultiplier`) accept NaN/Infinity at `InitialChildGenerationConfigProvider.cs:60-61, 79-80, 93-94`. `_random.NextDouble() < NaN` always `false` → every child male. `Math.Ceiling(baseCount * NaN)` produces NaN → `(int)NaN` is implementation-defined. R3 pattern. Fix: `FiniteFloatValidator.IsFiniteInRange(0.0, 1.0)` + `IsFiniteAtLeast(0.0)`.
- **P1** — `SelectTemplate` throws `ArgumentOutOfRangeException` when a clan has zero adults of both genders AND `FixedChildCount` > 0 at `InitialChildGenerationService.cs:134-137`. Fallback path indexes empty list. Fix: top-of-method guard `if (clan.AdultMaleHeroIds.Count == 0 && clan.AdultFemaleHeroIds.Count == 0) return null;` and skip with warning.
- **P2** — `OnNewGameCreatedPartialFollowUpEvent` at `index == 0` may fire before clan roster fully initialized. NamedCompanions uses `index == 1` — possibly for this reason. Verify via `ilspycmd` on SandBox.
- **P2** — `MinAge` / `MaxAge` ints have no ordering invariant validation. `Random.Next(15, 5+1)` throws `ArgumentOutOfRangeException` and propagates. R3 pattern. Fix: post-parse validate `MinAge <= MaxAge`; log warning + revert.
- **P3** — `IRandomSource` Singleton holds `System.Random` (not thread-safe in .NET 4.7.2). Latent hazard if any future call from worker thread.
- **P3** — Tests don't cover semantically-invalid-but-parseable values.
- **Confidence:** High on both P1s (mechanical IEEE-754 + Random API contract).

### NamedCompanions ❌ R1 + R5 (special target — Review #23 regressed in new states)

- **Wiring:** AddBehavior @ `SubModule.cs:342`, IoC @ `IoC.cs:84`, all Singleton.
- **SyncData:** empty body.
- **Original Review #23 bug:** **CONFIRMED FIXED.** Recruited-and-traveling-on-map state correctly skipped via `IsRecruitedOrInParty` checking `PartyBelongedTo != null`.
- **P1 (R1)** — `_spawned` singleton field at `NamedCompanionService.cs:15,33` survives across campaigns. New campaign 2 in same process: `SpawnCompanions()` returns immediately, no companions placed. Fix: `OnSessionLaunched`-bound `ResetSession()`.
- **P1 (R5)** — Prisoner companion (`PartyBelongedTo=null`, `PartyBelongedToAsPrisoner != null`, mobile captor) bypasses both guards and gets force-placed at `NamedCompanionAdapter.cs:27-31` → `NamedCompanionService.cs:79`. `PlaceInSettlement` calls `ChangeState(Active)` + `EnterSettlementAction` → corrupts prisoner state. Review #23-class regression on a state Review #23 didn't cover.
- **P1 (R5)** — Fugitive companion (`HeroState=Fugitive`, all party fields null, `CompanionOf` cleared) passes both guards, gets force-placed every load → silently rescued + teleported back to spawn settlement. Same pattern.
- **P2** — Tests `EnsureCompanionsPlaced_Prisoner_SkipsPlacement` and `EnsureCompanionsPlaced_Fugitive_SkipsPlacement` are missing per `tests.md` "Skip-Guard Exhaustion" rule.
- **P2** — `IsRecruitedOrInParty` doesn't check `PartyBelongedToAsPrisoner` at `NamedCompanionAdapter.cs:31`. Fix: broaden to `|| hero.PartyBelongedToAsPrisoner != null || hero.IsPrisoner`.
- **P3** — Config provider doesn't validate `CharacterId`/`SpawnSettlement`/`Race` fields for null/empty.
- **Confidence:** High on R1 P1, High on R5 P1s (with caveat: verify `PartyBelongedToAsPrisoner` exists in v1.3.15 via `ilspycmd`).

### CareerSystem ❌ SyncData mutates on save

- **Wiring:** 3 behaviors registered at `SubModule.cs:319, 321, 326` in correct order (Persistence first → Campaign → Dialogue). All deps Singleton. SyncData keys (`_taom_careerIds`, `_taom_careerChoices`, `_taom_careerTiers`) namespaced and non-colliding.
- **P1** — `CareerPersistenceBehavior.SyncData` runs the `RestoreData(restored)` reconstruction path UNCONDITIONALLY at `CareerPersistenceBehavior.cs:90`. On save, this replaces `_heroData` with a copy of itself, dropping any in-flight `_heroData` mutations and re-creating the dict. R2 pattern. Fix: gate the reconstruct block on `dataStore.IsLoading`.
- **P2** — `CareerConfigProvider.ParseFloat` at lines 427-433 lacks NaN/Infinity guard for `Duration`/`Radius`/`MaxCharge`/`DamageBonus`/`DamageReduction`/all `*Tuning` floats. Only `CooldownSeconds` got the Career #31 fix (via dedicated `ParseGlobalTuning`). R3 pattern. Same bug class as memory `feedback_clamp_nan_infinity_propagates.md`. Fix: helper-level finiteness check.
- **P2** — `CareerAbilityService._abilities` (Singleton) cleared only in `OnEndMission`, not on campaign start. New campaign 2 with same hero StringId reuses stale `CareerAbility` with old `CooldownDuration`. R1 pattern. Fix: inject `ICareerAbilityService` into `CareerCampaignBehavior`, call `ClearAll()` in `OnSessionLaunched`.
- **P2** — `CareerSwitchDialogueBehavior` dialog presents implicit career switch (no selection menu, no DisplayName confirmation) — silent UX bug if `CanSwitch` returns true for multiple careers.
- **P3** — Dialog double-registration risk on save-load mid-campaign (depends on vanilla dedup behavior).
- **P3** — `OnHeroKilled` skips `Hero.MainHero`, leaving orphan career data on character death. Phase 9 should reconcile player-permadeath path.
- **Confidence:** Medium-High overall, P1 confidence 95.

### Diplomacy ❌ WarOfTheRing CurrentPhase unsaved (R2)

- **Wiring:** Both behaviors `AddBehavior`'d at `SubModule.cs:259, 265`. IoC at `IoC.cs:60` (`DiplomacyIoC`). Both Singleton.
- **SyncData:** Both behaviors have empty SyncData bodies.
- **P1 (R2)** — `WarOfTheRingService.CurrentPhase` is unserialized singleton state at `WarOfTheRingService.cs:16`. On load, re-derived from elapsed days via two sequential non-nested `if` checks. For any campaign past `phase2Day`, both transitions fire on every load (`Peace → IsengardWar → FullWar`). Currently idempotent (war declarations have `!AreAtWar` guard). **Latent design fault:** any non-idempotent side effect added later (influence rewards, story flags, banner changes) replays on every load. Fix: persist `CurrentPhase` in SyncData, only fire transitions for unmet phases.
- **P2 (R3)** — `WarOfTheRingConfigProvider` lacks ordering invariant `Phase2.TriggerDay > Phase1.TriggerDay`, lacks `>= 1` minimum check, lacks empty-string check on attacker/defender. Shipped config has both at `1` (violates ordering). Fix: post-deserialize validation pass per "Config Providers MUST Validate".
- **P2** — `DiplomacyConfigProvider` and `WarOfTheRingConfigProvider` lack `?? new T()` fallback at `cs:34` for both. NRE risk on null-literal JSON.
- **P3** — `GetEffectivePhaseDays` precedence inversion: when MCM TestMode is OFF, JSON TestMode wins anyway.
- **P3 (out of scope)** — `Patch11_Diplomacy` / `Patch12_WarOfTheRing` re-applied on every `OnGameInitializationFinished`.
- **P3 (out of scope)** — `AllianceCampaignBehavior_EndAlliance_Patch.Initialize` called twice with different args at `SubModule.cs:126-129`.
- **Phase 2 cross-reference:** [`cluster-gamemodels.md`](cluster-gamemodels.md) Diplomacy section adds rule-4 findings on `TaomAllianceModel`, `TaomDiplomacyModel`, `TaomKingdomDecisionPermissionModel`. Phase 2 explicitly chose NOT to open a duplicate issue — these notes are tracked under #129 for Phase 9 fix work.
- **Confidence:** High on P1 (mechanical), High on both P2s.

### HeroRace ❌ R1 (singleton race map stale across campaigns)

- **Wiring:** AddBehavior @ `SubModule.cs:227`, IoC @ `IoC.cs:59`, all Singleton.
- **SyncData key:** `_taom_heroRaceMap` → `Dictionary<string, int>`.
- **P1 (R1)** — `_heroRaceMap` is Singleton field initialized once at field declaration. New campaign 2 in same process: TaleWorlds `dataStore.SyncData(key, ref dict)` for an absent key leaves the ref unchanged → prior campaign's map carries over → `RestoreHeroRaces` overwrites new campaign's heroes with old race assignments for any colliding `StringId` (which is every common vanilla lord). Fix: `OnNewGameCreatedEvent`-bound `ResetForNewCampaign()`.
- **P2** — `HeroRosterAdapter.GetAllAliveHeroRaces` accesses `h.CharacterObject.Race` without `?.` at `HeroRosterAdapter.cs:12`. NRE during `OnBeforeSaveEvent` aborts the save. Fix: `?.Race ?? 0`.
- **P2** — `CaptureHeroRaces` excludes race `0` (human) at `RacePersistenceService.cs:30`. Asymmetric: a hero deliberately reset to human silently reverts to old non-human race on next load (because old map entry is stale). Fix: capture all races including 0.
- **P3** — Tests don't cover `RegisterEvents` wiring (silent inert if subscriptions removed).
- **P3** — `CapturedRaceCount` not on `IRacePersistenceService` interface; tests bind to concrete.
- **P3 (out of scope)** — `HeroRaceIoC.cs:18-19` calls `Initialize(eyeHeightHook)` from inside IoC registration.
- **Confidence:** High on P1, High on P2 (NRE), Medium on race-0 asymmetry.

### RaceAge ❌ ADR-007 + R3 + R4

- **Wiring:** AddBehavior @ `SubModule.cs:251`, IoC @ `IoC.cs:67`, all Singleton.
- **SyncData:** empty body (correct — no per-save state).
- **P1** — `TaomPregnancyModel.GetDailyChanceOfPregnancyForHero` contains 32 lines of inline business logic (sealed `Hero` access, `Math.Min`, `ExplainedNumber`, `GetPerkValue`, full vanilla pregnancy reimplementation) at `TaomPregnancyModel.cs:18-58`. Out of Phase 3 scope (Phase 2 GameModel territory) but flagged P1 because it's an ADR-007 + GameModel rule double-violation. Fix: extract to `IRaceAgeService.GetDailyPregnancyChance(IHeroAgeInfo)`.
- **P1 (R1)** — `RaceAgeService._raceIdCache` Singleton never cleared between campaigns at `RaceAgeService.cs:11`. If race IDs shift between starts (HeroRace P1 above could cause this), cache serves stale entries. Fix: Reset on new-game.
- **P2 (R3)** — `RaceAgeConfigProvider.LoadConfig` has no semantic validation: `FertilityMod` (float) accepts NaN/Infinity → propagates through `baseChance *= NaN`; `MaxAge`/`BecomeOld`/`ComesOfAge`/`MiddleAge`/`FertilityEnd` ints have no ordering invariants. Fix: per-field validation.
- **P2 (R4)** — `RaceAgeService.GetEntry` calls `_raceManager.GetRaceNameFromId(raceId)` without prior `IsValidRaceId(raceId)` check at lines 47-49. The "human" fallback silently typed for invalid IDs. Memory `feedback_validate_before_lookup_with_fallback` rule. Fix: validate-before-lookup gate.
- **P3** — `_deathList` instance field unnecessary (style).
- **P3 (out of scope)** — `TaomAgeModel.MaxAge` / `BecomeOldAge` hardcoded to `10000`/`5000` constants — Phase 4 GameModel territory.
- **Phase 2 cross-reference:** [`cluster-gamemodels.md`](cluster-gamemodels.md) RaceAge section adds new null-safety findings (`hero.Spouse` / `hero.Clan` chains at `TaomPregnancyModel.cs:40-41`). Phase 2 explicitly chose NOT to open a duplicate issue — these notes are tracked under #131 for Phase 9 fix work.
- **Confidence:** High on R3 P2, High on R4 P2, High on R1 P1, Medium on hardcoded ages (out of scope).

### Siege ❌ R1 + R2 (empty SyncData — events lost every load)

- **Wiring:** AddBehavior @ `SubModule.cs:269`, IoC @ `IoC.cs:78`, all Singleton.
- **SyncData:** **EMPTY BODY** at `SiegeDefenseBehavior.cs:29`.
- **P1 (R2)** — Complete save-loss of all active siege defense events. `_activeEvents` (Singleton dict of `ActiveSiegeDefenseEvent` w/ CampaignTime deadline + `PlayerAccepted`/`RewardClaimed`) never serialized. Save-load: dictionary empty, siege continues in-world but no re-trigger / no reward / no untrack. **Most user-visible bug in the cluster — first save-load with active siege loses everything.** Fix: implement SyncData properly.
- **P1 (R1)** — `_activeEvents` not reset between campaigns in same process. Stale events from campaign 1 suppress new events for matching settlement IDs in campaign 2. Fix: `OnSessionLaunched`-bound `Reset()`.
- **P2** — `CampaignTime.DaysFromNow` exception swallowed silently at `SiegeDefenseService.cs:93-100`, deadline replaced with `default(CampaignTime)` (campaign epoch — instantly past). Event cleaned up next hourly tick before player can respond. Fix: log + sensible fallback.
- **P2** — Dead config fields `RelationshipThreshold` (-20) and `ResponseWindowDays` (3) declared but never consumed in any callsite. R3 pattern + Simplicity Criterion violation. Fix: wire or delete.
- **P2** — Reward delivery race when siege ends just before player arrives. Fix: also grant in `OnSiegeEnded` if `PlayerAccepted && !RewardClaimed && player at settlement`.
- **P3 (out of scope)** — `TaomSiegeEventModel`, `Patch8_SiegeCampGuard` — Phase 2/4 territory.
- **Confidence:** Very High (P1 #1 is mechanically certain; SyncData body is literally empty).

### SpecialResources ❌ R1 + R2 + R3 + R5 (multi-pattern bug cluster)

- **Wiring:** AddBehavior @ `SubModule.cs:312`, IoC @ `IoC.cs:81`, all Singleton. `IOnPartyUpgradeResourceCheck` impl registered correctly.
- **SyncData key:** `_taom_specialResources` → `Dictionary<string, float>`. Composite key `heroId:resourceId` (unparsed — write-only).
- **P1 (R2)** — `SpecialResourcesBehavior.SyncData` calls `_storage.ClampAll(resource.Cap)` at lines 57-64 with the player's current resource cap, applied to ALL keys regardless of resource type. Multi-resource saves silently corrupted (e.g., gems clamped to war_spoils cap). `SyncData` should not mutate. Fix: remove ClampAll from SyncData; per-resource cap inside `RestoreData`.
- **P1** — `ScreenManager.OnPushScreen += OnScreenPushed` at line 46 never unsubscribed. Static event leaks across campaigns. New campaign in same process accumulates stale handlers calling `BeginPartyScreenSession()` on shared singleton service, cancelling legitimate pending spends. Fix: subscribe `OnSessionEndedEvent`, unsubscribe in handler.
- **P1 (R3)** — `SpecialResourceConfigProvider.ParseFloat` uses `float.Parse` (throws on malformed) and lacks NaN/Infinity guard. `cap="NaN"` would make `Math.Min(cap, val) == NaN` for every entry. Fix: `TryParse` + `FiniteFloatValidator`.
- **P2 (R1)** — `_loggedResolveKeys` + `_pendingSpend` + `_inSession` Singleton fields never reset on new-campaign. `_pendingSpend` from prior session can deduct against new hero's balance. Fix: `OnNewGameCreatedEvent` clear.
- **P2 (R5)** — Desertion fires on first DailyTickHero after load if balance was already 0 pre-save. No grace period. Fix: `_isFirstTickAfterLoad` flag, skip first tick.
- **P2** — `OnSessionLaunched` legacy-save seed fires on every kingdom-change, gifting fresh `StartingAmount`. Fix: versioned SyncData seed flag.
- **P3 (out of scope)** — Patch26 `SetTotalNumber` reflection field cached on first use rather than `Initialize`.
- **Confidence:** Very High on P1 (`ClampAll` mechanically wrong; `OnPushScreen` leak provable; `ParseFloat` directly readable).

### StartupResources ⚠ R3 + Simplicity Criterion

- **Wiring:** AddBehavior @ `SubModule.cs:339`, IoC @ `IoC.cs:69`, all Singleton.
- **SyncData:** empty body (correct — new-game-only).
- **Trigger:** `OnNewGameCreatedPartialFollowUpEvent` at `index == 1`.
- **P1 (R3)** — `StartupResourcesConfigProvider.ParseConfig` parses `Gold` (int) and `Influence` (float) at lines 54-55 with bare `int.Parse` / `float.Parse`. Asymmetric with `ParsePlayerGold` which has range validation. NaN influence silently `> 0f` evaluates false → silent skip with no log. Fix: extract `ParseGold`/`ParseInfluence` mirroring `ParsePlayerGold`.
- **P2** — `_goldDistributed` / `_influenceDistributed` booleans are dead guards (`index != 1` check already prevents double-fire; no save-load re-grant path exists). Simplicity Criterion violation per `simplicity-criterion.md`. Fix: remove the booleans + guards.
- **P3** — Index 1 timing concern for clan roster initialization. Verify via `ilspycmd` on SandBox.
- **Confidence:** High on P1, High on P2 (Simplicity Criterion straightforward).

### CompanionTactics ❌ SaveableTypeDefiner container collision

- **Wiring:** AddBehavior @ `SubModule.cs:361`, IoC @ `IoC.cs:92`, all Singleton.
- **SyncData key:** `TAOM_FormationPresets` → `List<HoNFormationPreset>` (via `SaveableTypeDefiner` BaseId 726900601).
- **P1** — `FormationPresetSaveableTypeDefiner.DefineContainerDefinitions` registers `Dictionary<string,int>`, `Dictionary<int,int>`, `List<string>` at lines 27-30. These are likely already pre-registered by vanilla SandBox. Catch-block in SyncData swallows the collision exception silently → presets reset to empty without player-facing notification. CareerSystem deliberately avoided `SaveableTypeDefiner` for this reason. Fix: switch to primitive-dict SyncData like CareerSystem.
- **P1** — Silent `LogWarning`-only catch on SyncData failure. Player sees no in-game indication, just lost presets. Fix: `InformationManager.DisplayMessage` after the LogWarning.
- **P2** — `OnNewGameCreated` calls `_service.OnGameLoaded(empty)` for reset (semantic mismatch). Fix: add `void Reset()` to interface.
- **P3** — Cross-mod `BaseId 726900601` collision risk if original developer's mod is also installed.
- **P3 (out of scope)** — `_heroFormationAssignments` doesn't prune dead heroes on load (delegate to OOBOverlayService review).
- **Confidence:** Medium-High on the container-collision P1 (would lift to High with `ilspycmd` on `TaleWorlds.SaveSystem.dll` to enumerate vanilla container registrations).

### EquipPresets ✅ inventory mutations correct + ⚠ minor

- **Wiring:** AddBehavior @ `SubModule.cs:351`, IoC @ `IoC.cs:91`, all Singleton.
- **SyncData key:** `EquipPresets_HeroPresets` → `Dictionary<string, List<HoNEquipmentPreset>>` (composite per-hero).
- **Inventory mutations:** ✅ Confirmed using `InventoryLogic.TransferCommand` + `AddTransferCommands` at `InventoryScreenAdapter.cs:66-195`. Codex review #5 finding addressed. Modifier preservation via `ItemRosterElement` value (carries `ItemModifier` as-found). `SlotApplyOutcome.SlotLocked` confirmed removed.
- **P1** — `Patch33_SPInventoryVMRefresh` does `IoC.Resolve<IInventoryScreenAdapter>() as InventoryScreenAdapter` concrete cast at line 37 — silent null on resolution failure. `SetActive` not on interface. Fix: expose `SetActive` on `IInventoryScreenAdapter`, drop the cast.
- **P2** — `equipped` counter at `InventoryScreenAdapter.cs:147-153` overcounts no-op cases (slot already correct). Player sees "8 items applied" with no transfer commands issued. User-facing promise mismatch. Fix: separate `alreadyEquipped` count.
- **P3 × 4** — Doc rot: `docs/features/equip-presets.md` and several `///` comments still reference removed `SlotApplyOutcome` and `ApplySlot`. Fix: doc updates only.
- **Confidence:** High on all.

### FiefManagement ✅ GameState pattern correct + ⚠ swap restore + R1

- **Wiring:** AddBehavior @ `SubModule.cs:355`, IoC @ `IoC.cs:93`, all services Singleton.
- **SyncData:** empty body — intentional (selected-index is transient). Documented.
- **GameState:** ✅ `manager.CreateState<FiefManagementGameState>()` + `PushState` pattern correct at `FiefHubCampaignBehavior.cs:78`. Codex #36 fix verified. `IGameStateListener` implemented (empty stubs are acceptable given `IsMenuState=true`).
- **P1** — `RemoteFiefSettlementSwapper.Restore` at lines 42-47 silently bails if `MobileParty.MainParty == null` at `OnFinalize` time. Global `MobileParty._currentSettlement` left pointing at remote fief → corrupts party movement, AI, all subsequent F6 invocations. Fix: hold `_party` ref captured at `Swap` time + log error if restore can't write.
- **P2 (R1)** — `FiefHubMenuPresenter` Singleton has 4 stateful fields; `Reset()` only resets `_selectedIndex`. Stale `_menuFiefs` from prior campaign carries over → Prev/Next options visible with stale data before first `Refresh()`. Fix: clear all 4 fields in `Reset()`.
- **P2** — `Patch36_MapScreenF6` calls `service.Count` (which calls `GetOrderedFiefs()` → iterates `Settlement.All` 862 settlements) on every F6 press. Fix: presenter-cached `Count` or fast-path `Clan.PlayerClan?.Settlements.Count(...)`.
- **P3** — `FiefManagementGameState.Fief` exposes sealed `Settlement` (ADR-007). Fix: store `string SettlementId` instead.
- **P3** — `IGameStateListener` empty stubs (acceptable).
- **Confidence:** High on P1 (silent-bail mechanical), High on R1 P2.

### QuickActions ⚠ R2 (per-save semantics broken by reconciler)

- **Wiring:** AddBehavior @ `SubModule.cs:346`, IoC @ `IoC.cs:89`, all Singleton.
- **SyncData key:** `TAOM_IsInventorySearchAvailable` → `bool`.
- **Entity State Matrix N/A** (UI-only, no entity mutation).
- **P2 (R2)** — `OnGameLoaded` reconciler at lines 51-56 unconditionally overwrites saved `_isSearchAvailable` with current MCM value if they differ. CLAUDE.md key paths table promises "per-save toggle"; the implementation makes MCM authoritative. Per-save intent silently lost on every load. Fix: remove the reconciler.
- **P2** — `OnTick` writes `_isSearchAvailable = _settings.EnableInventorySearch` every campaign frame after MCM change → save value tracks MCM, not per-save. SyncData becomes redundant. Fix: remove `OnTick`; read MCM directly at Postfix time, drop SyncData entirely (if MCM-wins is intended) — OR keep SyncData and remove the reconcilers (if per-save is intended). Pick one.
- **P3** — `Patch34_SPInventoryVMCapture` and `Patch34_SPInventoryVMFinalize` use `IoC.Resolve<IInventoryVMAdapter>() as InventoryVMAdapter` concrete cast (same shape as EquipPresets P1).
- **Confidence:** High overall — the contradiction between CLAUDE.md's "per-save" promise and the implementation is mechanical.

## GitHub issues opened

### Phase 3 (CampaignBehavior cluster, 16 features × one issue each)

| # | Feature |
|---|---|
| [#123](https://github.com/haterade22/TAOM/issues/123) | Messengers |
| [#124](https://github.com/haterade22/TAOM/issues/124) | BannerInjection |
| [#125](https://github.com/haterade22/TAOM/issues/125) | CharacterCreation |
| [#126](https://github.com/haterade22/TAOM/issues/126) | InitialChildGeneration |
| [#127](https://github.com/haterade22/TAOM/issues/127) | NamedCompanions (special target) |
| [#128](https://github.com/haterade22/TAOM/issues/128) | CareerSystem |
| [#129](https://github.com/haterade22/TAOM/issues/129) | Diplomacy |
| [#130](https://github.com/haterade22/TAOM/issues/130) | HeroRace |
| [#131](https://github.com/haterade22/TAOM/issues/131) | RaceAge |
| [#132](https://github.com/haterade22/TAOM/issues/132) | Siege |
| [#133](https://github.com/haterade22/TAOM/issues/133) | SpecialResources |
| [#136](https://github.com/haterade22/TAOM/issues/136) | StartupResources |
| [#139](https://github.com/haterade22/TAOM/issues/139) | CompanionTactics |
| [#141](https://github.com/haterade22/TAOM/issues/141) | EquipPresets |
| [#143](https://github.com/haterade22/TAOM/issues/143) | FiefManagement |
| [#146](https://github.com/haterade22/TAOM/issues/146) | QuickActions |

### Cross-reference to Phase 2 (GameModel cluster, also completed 2026-05-13)

Phase 2 ran in parallel with this Phase 3 session and produced [`cluster-gamemodels.md`](cluster-gamemodels.md) + 10 GameModel issues (#134, #135, #137, #138, #140, #142, #144, #145, #147, #148). Phase 2's cluster doc explicitly notes overlap with two of this Phase 3 cluster's issues:

- **Phase 3 #131 (RaceAge)** already covers `TaomPregnancyModel` rule-4 violation, NaN/Infinity config gap, and validate-before-lookup pattern. Phase 2 added new null-safety findings on `hero.Spouse`/`hero.Clan` chains and noted them in `cluster-gamemodels.md` RaceAge section without opening a duplicate issue.
- **Phase 3 #129 (Diplomacy)** already covers `WarOfTheRingConfigProvider` ordering invariants and `DiplomacyConfigProvider` `?? new T()` fallback. Phase 2 added rule-4 findings on `TaomAllianceModel`, `TaomDiplomacyModel`, `TaomKingdomDecisionPermissionModel` and noted them in `cluster-gamemodels.md` without duplicating.

For Phase 9 fix work: when fixing #129 or #131, also pick up the per-feature additional notes from `cluster-gamemodels.md`. The model body fixes naturally co-locate with the behavior/service fixes.

### Cross-reference to Phase 1 (wiring matrix, also completed 2026-05-13)

Phase 1's [`wiring-matrix.md`](wiring-matrix.md) found exactly **1 P2 wiring miss** (`MobilePartyVisual_AddCharacterToPartyIcon_Patch` is Harmony-bound but never `Initialize`-d → silent no-op on world-map party icons; issue #122). That's BannerColorPersistence territory, not in Phase 3's scope. Phase 1 also confirmed all 16 features in this Phase 3 cluster are correctly wired (`AddBehavior` calls present, IoC registrations balanced). The inline wiring sanity checks each Phase 3 agent performed converged with Phase 1's findings — no Phase-1-vs-Phase-3 contradictions.

### Process note (carry forward)

The two phases completing in parallel without contention validates that Phase 0's "1 session per phase, parallel subagents within" model is actually a "phases parallelize too" model when the cluster docs are disjoint. Phase 9's fix-execution cadence may want to keep this in mind.

## Phase 3 complete

- **16 features fully reviewed** — all 19 `CampaignBehaviorBase` subclasses inspected.
- **24 P1 + 33 P2 = 57 actionable findings** + 25 P3/out-of-scope notes.
- **16 GitHub issues opened** (one per feature, body containing all P1+P2 with `[ ]` checklist for Phase 9 fix tracking) — label `audit-impl`.
- **Special-target verdicts:**
  - **Messengers:** clean wiring; 2 latent P1s in singleton-reset flag handling and mission-null leak. Codex #34's fix is partially applied — the `_justLoadedFromSave` flag has a subtle reset gap.
  - **NamedCompanions:** Review #23 original bug **fixed**, but 2 NEW P1 state-class bugs (Prisoner + Fugitive) introduced — the Entity State Matrix rule wasn't extended to all 6 states the rule itself names. This is the exact pattern the rule exists to prevent; suggest adding a meta-rule to the rule itself (or to `tests.md`'s "Skip-Guard Exhaustion") that requires the test suite to cover ALL 6 states for any `EnsureXxxPlaced`-style operation.
- **Phase 4+ targets surfaced:**
  - GameModel reviews (Phase 2 / future): `TaomPregnancyModel` ADR-007 violation; `TaomAgeModel` hardcoded constants; `TaomSiegeEventModel`; `TaomTargetScoreModel` (untouched).
  - Harmony patch reviews (Phase 4): Patch11/Patch12 lifecycle re-application; `AllianceCampaignBehavior_EndAlliance_Patch.Initialize` double-call; Patch26 reflection caching; Patch33/Patch34 concrete casts; Patch36 swap-stuck risk.
  - Cross-feature handshake (Phase 6): RevoltTuning vs CulturalFeats `TaomSettlementLoyaltyModel`; HeroRace race ID stability across CharacterCreation/RaceAge/NamedCompanions/SpecialResources.
- **Recurring patterns (R1-R5)** documented at top — Phase 9 should batch fixes by pattern, not per-feature, to amortize effort.

## Phase log

| Date | Phase | Session | Output | Findings count |
|---|---|---|---|---|
| 2026-05-13 | 0 | initial | `feature-manifest.md`, `README.md` | 17 queued for Phase 1+ |
| 2026-05-13 | 3 | (skipped Phase 1+2) | `cluster-campaign-behaviors.md`, 16 GitHub issues | 24 P1 + 33 P2 + 25 P3 |
