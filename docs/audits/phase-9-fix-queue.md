# Phase 9 Fix Queue (post-9a verification)

Built: 2026-05-13 (Phase 9a end-state)
Total VALID issues: **77** (closed STALE: #154)
HEAD at queue construction: `b4b4de1`
Detail: [`triage-results.md`](triage-results.md) and per-batch files

Categories per the Phase 9 prompt:

| # | Category | Count | Commit grain |
|---|---|---|---|
| 1 | Mechanical wiring | 2 | Batch 1 commit |
| 2 | Per-feature semantic | 45 | 1 commit per fix |
| 3 | Cross-feature | 6 | 1 commit per cluster pair |
| 4 | Test additions | 20 | Batch by feature |
| 5 | Doc updates | 4 | Batch by feature |
| **Total** | | **77** | |

Within each category, items are listed in **fix-priority order**: dependencies first, P1s before P2s, P3s last.

---

## Category 1 — Mechanical wiring (2 issues — 1 commit)

These are the audit-motivating regression class — missing IoC / `Initialize` / patch-binding wiring that compiles cleanly and ships dormant. Should be the FASTEST visible win after docs.

| # | Severity | Feature | Fix (one-liner) | Depends on |
|---|---|---|---|---|
| **#122** | P2 | BannerColorPersistence | Add `MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter)` to `SubModule.OnSubModuleLoad` (near lines 161-180, mirroring the 19 sibling `Initialize` calls there) | — |
| **#158** | P3 | BannerColorPersistence | Add `else IoC.Resolve<IModLogger>().LogWarning("[BannerColor] AgentVisuals.Create not found — clan color randomness suppression will not apply");` after the manual patch call at the relevant `SubModule.cs` site | — |

**Recommended commit:** `fix(banner-color): wire missing Initialize + diagnostic LogWarning (#122, #158)`. Both findings are in the same feature, both in `SubModule.cs`, both mechanical. Single commit; matches `phase-9-kickoff.md` R6 mechanical-wiring guidance.

---

## Category 2 — Per-feature semantic fixes (45 issues — 1 commit per fix)

Group internally by source phase + recurring pattern. Within a feature, multiple sub-bullets in one issue body may need separate commits — split per Phase 9 "Don't combine fixes from different categories" rule, but within a single issue/feature it's fine to bundle related sub-items.

### 2a. Phase 2 GameModel rule-4 + null-guard violations (10 issues)

Per `gamemodels.md` rule 4 ("no inline if/foreach/switch/yield-branching in override body"). Most P2 except the 2 P1 NREs.

| # | Severity | Feature × Model | Smallest fix |
|---|---|---|---|
| **#134** | **P1** | Siege / `TaomSiegeEventModel` | Guard `party.MobileParty` with `?.HasPerk(...) == true` before perk check (garrison defenders have null `MobileParty`). Update issue body — current code references different perk names (`Stonecutters`/`SiegeEngineer`) than the audit cited. |
| **#135** | **P1** | CulturalFeats / `TaomPartySpeedModel` | Add null-guard `Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(...)` on hot per-tick path |
| #137 | P2 | Arena / `TaomTournamentModel` | Extract `GetTournamentStartChance`, `BuildPrizePool`, `GetParticipantArmor` to `ITournamentRulesService`; null-guard `Campaign.Current.Models.AgeModel.HeroComesOfAge` chain |
| #138 | P2 | ArmyTargeting / `TaomTargetScoreModel` | Extract effective-strength ternary + routing branch to `ITargetScoreCalculator` service |
| #140 | P2 | BattleBalance / `TaomPartyHealingModel` + `TaomMilitaryPowerModel` | Constructor-inject `ICareerPassiveService` (no service-locator); extract 41-line override to `IPartyHealingCalculator`; add `FiniteFloatValidator` to `BattleBalanceConfigProvider` |
| #142 | P2 | CareerSystem / `TaomAgentStatCalculateModel`, `TaomAgentApplyDamageModel`, `TaomInventoryCapacityModel`, `TaomMapVisibilityModel` | Extract override bodies to `ICareerCombatModifier`/`ICareerInventoryModifier`; replace `CareerPassiveHelper.ApplyFactor` static calls with injected `ICareerPassiveService` (see cross-issue dependency below) |
| #144 | P2 | CulturalFeats / all 16 models | Create `ICulturalFeatsService` covering Caravan/BattleReward/ClanFinance/Prosperity/Smithing/PartySpeed asymmetric coalesce + `CharacterObject.PlayerCharacter` semantics |
| #145 | P2 | Encyclopedia / `TaomInformationRestrictionModel` | Replace `TaomSettings.Instance?.ShowAllEncyclopediaCharacters` with constructor-injected `IInformationRestrictionConfig` (or similar) |
| #147 | P2 | Execution / `TaomExecutionRelationModel` + SubModule:272-273 | Replace `IOnExecutionAction` hook-injected-into-model anti-pattern with `IExecutionRelationService`; extract override body. Update issue body — current code uses `Hero.MainHero?.Clan?.Kingdom?.StringId` not `Hero.MainHero.MapFaction.StringId`. |
| #148 | P2 | TroopProgression / `TaomPartyWageModel` | Extract `GetTotalWage`/`GetTroopRecruitmentCost` override bodies to `IPartyWageCalculator`; replace `CareerPassiveHelper.ApplyFactor` static (see cross-issue dependency); move `IVolunteerContextAdapter` registration to feature IoC |

**Cross-issue dependency (HIGH-PRIORITY refactor):** **`CareerPassiveHelper` static-to-service** affects #142, #144, #148, #173. Doing this refactor FIRST closes 4 issues and unlocks single-commit-per-feature fixes for the consumer models. Recommended: ship `CareerPassiveHelper` deletion + `ICareerPassiveService` consumer wiring as commit 1; then per-model fixes as separate commits.

### 2b. Phase 3 CampaignBehavior R1/R2/R3/R4/R5 patterns (16 issues)

Pattern keys from `cluster-campaign-behaviors.md`:
- **R1** — Singleton state reset across new-campaign-in-same-process
- **R2** — Empty / drop-on-load SyncData
- **R3** — Config validation gaps (missing `FiniteFloatValidator`, ordering invariants)
- **R4** — Lookup-with-fallback without input validation
- **R5** — Load-path Entity State Matrix gap

| # | Severity | Feature | Patterns | Smallest fix |
|---|---|---|---|---|
| **#127** | **P1** | NamedCompanions | R1+R5 | Add `IsHeroPrisoner` / `IsHeroFugitive` to adapter; skip force-place in both states; reset `_spawned` on `OnNewGameCreatedEvent` |
| **#132** | **P1** | Siege | R1+R2 | Implement `SyncData` (serialize `_activeEvents`); `OnSessionLaunched` reset of singleton; remove bare-catch; wire-or-delete `RelationshipThreshold` / `ResponseWindowDays`; grant reward in `OnSiegeEnded` too |
| **#133** | **P1** | SpecialResources | R1+R2+R3+R5 | Remove `ClampAll` from `SyncData` (move per-resource cap into `RestoreData`); `OnSessionEndedEvent` → unsubscribe `OnPushScreen`; `TryParse` + `FiniteFloatValidator`; `OnNewGameCreatedEvent` clear singletons; first-tick-after-load grace; versioned seed flag |
| **#123** | P1+P2 | Messengers | R1 partial | Move `_justLoadedFromSave=false` outside the starter-changed gate; null-guard `_currentMission` post-`AddListener`; replace `ClearListeners` with `RemoveNonSerializedListener`; decouple `_dialogsRegistered` from starter gate |
| **#124** | P1 | BannerInjection | R1+R2 | Reset `_playerModifiedIds` on `OnNewGameCreatedEvent` OR drop null-guard logic; init SyncData `list=null` (don't rebind to current state); lazy-cache config provider; batch `InvalidateVisuals` |
| #125 | P2 | CharacterCreation | (not R1-R5; ADR-007) | Extract `IPlayerHeroAdapter` / `IPlayerPartyAdapter` / `ISettlementAdapter` / `ICultureCreationDataProvider`; constructor-inject `ICareerCreationHandler` + `ICareerRegistry`; widen adapter to include `IsFemale` (per A1 batch note); reset `SelectedCareerStringId` in `OnSessionLaunched` |
| **#126** | P1+P1+P2 | InitialChildGeneration | R3 | `FiniteFloatValidator` for `FemaleRatio` (0..1) + `ChildCountMultiplier` (≥0); top-of-method guard on `SelectTemplate` for empty-list fallback; post-parse `MinAge ≤ MaxAge` invariant |
| #128 | P1+P2 | CareerSystem | R1+R2+R3 | Gate `RestoreData` on `dataStore.IsLoading`; `FiniteFloatValidator` in `ParseFloat` helper; inject `ICareerAbilityService` into `CareerCampaignBehavior` + `OnSessionLaunched` clear; confirmation menu for multi-eligible career switch |
| #129 | P1+P2 | Diplomacy (WotR) | R2+R3 | Persist `CurrentPhase` in SyncData; skip already-attained phases in `CheckPhaseTransition`; add `?? new WarOfTheRingConfig()` / `?? new DiplomacyConfig()` fallback; post-deserialize semantic validation |
| #130 | P1+P2 | HeroRace | R1 | Add `ResetForNewCampaign()` subscribed to `OnNewGameCreatedEvent`; `?.Race ?? 0` in adapter get; null-guard in set; purge stale entries (don't just filter `>0`) |
| #131 | P1+P1+P2 | RaceAge | R1+R3+R4 | Extract `GetDailyPregnancyChance(IHeroAgeInfo)` to `IRaceAgeService`; reset `_raceIdCache` on new-campaign; `FiniteFloatValidator` + ordering invariants; `IsValidRaceId` gate before `GetRaceNameFromId` (linked fix per A1 batch note) |
| #136 | P1 | StartupResources | R1+R3 | Gold/Influence config validation; remove dead guards |
| #139 | P1+P2 | CompanionTactics | (R2 container collision) | Resolve `SaveableTypeDefiner` container collision risk; silent SyncData-reset path needs explicit `IsLoading` gate |
| #141 | P1+P2 | EquipPresets | (concrete cast) | Patch33 concrete-cast → use adapter (shared with #146 — see cross-issue dep); UX over-count fix |
| #143 | P1+P2 | FiefManagement | R1 partial | Complete `Reset()` (the partial reset is incomplete); silent swap-restore failure needs surfacing |
| #146 | P2 | QuickActions | R2 | Per-save `IsSearchAvailable` contract: gate reconcilers; share adapter widening with #141 |

**Cross-issue dependency:** **Concrete-cast adapter widening** ties #141 + #146 — single adapter-widening commit closes both.

### 2c. Phase 4 Harmony patch issues (14 issues — minus #154 closed)

| # | Severity | Feature × Patch | Smallest fix |
|---|---|---|---|
| **#149** | **P1** | CompanionTactics / Patch35 `SetMovementOrder` | One-line team filter: `if (__instance?.Team != Mission.Current?.PlayerTeam) return;` before `ClearStance` |
| **#150** | **P1** | BannerColorPersistence / MapConversationTableau leader+bodyguard | Move color injection into Prefix on `AgentVisuals.Create` (Site 5 pattern) OR use native `SetClothColor` if available in the installed version |
| #151 | P2 | HeroRace / `ActionSetCode_GenerateActionSetNameWithSuffix_Patch` | `public class` → `public static class`; AND investigate whether the patch is a no-op duplicate of vanilla (Cluster D flag) |
| #152 | P2 | Diplomacy / `AllianceCampaignBehavior_EndAlliance_Patch` | Gate `ShouldPreventAllianceEnd` on alliance-expiry triggers only (not war-declaration) OR suppress the downstream `AddAllianceDecision` |
| #153 | P2 | Diplomacy / `DeclareWarAction_ApplyInternal_Patch` | Document the `OnWarDeclared` event suppression; force-declare paths must dispatch it manually OR use `DeclareWarAction.ApplyByX` |
| #155 | P2 | SmartCavalryAI / `CavalryChargeService._states` | Add `private readonly object _lock = new();` + wrap state-dict accesses (mirror `FormationLayoutService` pattern) |
| #156 | P2 (dormant) | BattleScenes / `MBMapScene_GetBattleSceneIndexMap_Patch` | `volatile static bool _isRetrying` OR `[ThreadStatic]`; move retry to background thread. Dormant; OK to defer until feature re-enabled |
| #157 | P2 | SettlementGuards / `PrepareGuardAgentDataFromGarrison` | Verify staticness via ilspycmd on the installed version (Cluster F already cleared); also: replace bare `catch{}` with log + meaningful fallback |
| #159 | P2 | BannerColorPersistence / `MobilePartyVisual.AddCharacterToPartyIcon` | Drop the `typeof(ActionIndexCache).MakeByRefType()` param-type array; let Harmony resolve unique-name overload |
| #160 | P2 | CharacterSelection / `RefreshCharacterEntityAuxPatch` | Replace hard-throw on IL mismatch with logged degradation; add defensive null-guard |
| #161 | P2 | ArmyTargeting / Patch22 | Cache 3 `IoC.Resolve` calls outside the hot-path loop |
| #162 | P2 | CustomBattles / Patch19 `OnCultureSelection` | Verify private-method patch on the installed version via ilspycmd (Cluster C/E `v1.3.15-unverified` residual) |
| #163 | P2 | CharacterCreation / `SpawnNonHuman` Finalizer | Don't unconditionally swallow NREs — log + rethrow OR scoped suppression |
| #164 | P3 (consolidated) | Multiple | Cleanups: bare `catch{}`, missing `[HarmonyPostfix]` attrs, missing `LogWarning` fallbacks on manual `Patch(...)` calls. Touches multiple files — bundle as one commit per file or split per-feature |

### 2d. Phase 5 UI / Mixin / Prefab (5 issues)

| # | Severity | Feature | Smallest fix |
|---|---|---|---|
| **#165** | **P1** | CareerSystem UI | Author + register 29 portraits + 41 ability + ~120 choice-icon sprites (5 of these are atlas/Config.xml updates; rest is sprite authoring). 2 P3 localization gaps (hardcoded `"Career"` button label, missing `DisplayName` field on `CareerChoiceDefinition`) |
| **#166** | **P1** | Messengers UI | Mixin notifies wrong VM — change all 3 setters at `MessengerEncyclopediaMixin.cs:143,160,174` from `ViewModel?.OnPropertyChangedWithValue(...)` to `OnPropertyChanged(nameof(...))` on `this`; remove unused `[DataSourceProperty] SendMessengerCost` OR bind it |
| **#167** | **P1+P2+P3** | SpecialResources UI | Author + register 8 missing resource sprite PNGs (1 P1); replace `SecondaryInfoItems.Add` rule violation (1 P2); fix `_baseInitialized` guard ordering (1 P2); 3 P3 minor. **Note from 9a: P2 #8 ordering claim is FALSE-POSITIVE — `Value` already precedes `IntValue`; drop that sub-finding from the fix PR.** |
| #168 | P2 | TimeAcceleration UI | `IsExtraFastForwardActive` watches `SpeedUpMultiplier > 4f` but command never raises that — use `TimeControlMode` OR raise `SpeedUpMultiplier` from the command; localize tooltip |
| #169 | P2+P3 | Custom Widgets (FactionMap × 4 + SpecialResources × 1) | Hoist per-frame `SimpleMaterial` allocations (PolygonWidget edge + shadow, BannerWidget glow); fix static-list race; move cross-thread `HoveredFactionName` write; `Initialize`-inject services in SpecialResourceSpriteWidget; doc thread/portability smells |

**Cross-category dependency:** Sprite authoring (#165, #167) is partly asset work, partly code work. Recommend splitting commits: (a) asset authoring + atlas registration; (b) code-only fixes (localization, dead properties, ordering).

---

## Category 3 — Cross-feature handshake fixes (6 issues — 1 commit per cluster)

| # | Severity | Pair / Triplet | Smallest fix |
|---|---|---|---|
| #170 | P2+P3 | SmartCavalryAI × MixedFormations × CompanionTactics | (Code work overlaps with #155 lock-add.) Document threading asymmetry + add `[HarmonyAfter]` annotation to Patch_MissionTime_SetMovementOrder |
| #171 | P2+P3 | CharacterCreation × HeroRace × RaceAge | Race-ID round-trip: validate stored race ID on load; document Patch20+Patch29 ordering; null-coalesce `playerChar.Race==0` window in `PlayerBodyPropertiesAdapter` |
| #172 | P1(cross-ref #122)+P2+P3 | BannerColorPersistence × BannerInjection × Patch24 | Scope `UpdateBannerColorsAccordingToKingdom` Prefix to PLAYER-clan only (NPC clans should retain vanilla kingdom-sync); add `TargetMethod()` null-guard; doc event ordering |
| **#173** | **P1+P2** | CareerSystem × TroopProgression via `CareerPassiveHelper` | **Deletion of `CareerPassiveHelper` static + `ICareerPassiveService` consumer wiring.** Closes 4 issues simultaneously: #142, #144, #148, #173. Add `_lock` to `CareerPassiveService._cache`. Fix int truncation in `TaomSmithingModel` (cast to `int` after multiplier, not before). |
| #174 | P2+P3 | SpecialResources × CareerSystem | Apply discounted cost to BOTH `ClampUpgradeCount` gate AND `QueueUpgradeSpend` debit (player currently overpays by the passive discount %). Doc UI-hint discrepancy as separate sub-item. |
| #175 | P2 | FactionMap × CharacterCreation | Clear `_factionVM` in `OnDisconnectedFromParent` (or on next `Constructor` entry); clear `_pendingPins` in `ResetSession` |

**Recommended sequence:** **#173 first** — its `CareerPassiveHelper` deletion unblocks #142, #144, #148 from Category 2a (collapses 4 commits into 1 cross-feature refactor). #170 second because its lock-add overlaps with #155.

---

## Category 4 — Test additions (20 issues — batch by feature)

These are the **audit-motivating regression class** — wiring + behavior-callback + cross-feature coverage gaps. Group by test pattern:

### 4a. Wiring regression tests (4 issues — one shared test helper)

| # | Severity | Feature | Test surface |
|---|---|---|---|
| **#191** | **P2 → P1 candidate** | Messengers | `MessengerCampaignBehaviorTests` — assert IoC resolution + `RegisterEvents` invocation. **Audit-motivating test gap.** Recommend P1 reclassification. |
| #192 | P2 | SettlementGuards | Manual `_harmony.Patch(...)` binding verification + service-invocation-at-mission-init test |
| **#193** | **P2 (mechanism-corrected)** | SiegeDismount | **MissionBehavior wiring test** (NOT Harmony binding test — see 9a #193 comment). Assert `OnMissionBehaviorInitialize` adds `SiegeDismountMissionBehavior` |
| #195 | P2 | TroopWeight | 4 `IOn*` hook implementations — each needs a focused test |

**Pattern:** all 4 will likely share a test helper (mock `Mission`, mock `_harmony` for #192, assert behavior-list contains expected type). Single test-helper-class commit + 4 per-feature commits using it.

### 4b. Behavior-callback coverage (3 issues — ADR-008 80% hook target)

| # | Severity | Feature | Test surface |
|---|---|---|---|
| **#176** | **P1** | CulturalFeats | 16 GameModels with zero behavior-hook tests. Adding ≥3 tests per model = 48+ tests. Heaviest single test work in queue. |
| **#177** | **P1** | FiefManagement | 5 behavior callbacks untested (`OnSessionLaunched`, `OnNewGameCreated`, `OnGameLoaded`, `SyncData`, `RegisterEvents`) |
| #195 | (above) | TroopWeight | Hook tests already grouped under 4a |

### 4c. Cross-feature contract tests (Phase 6 dependencies — 7 issues)

These verify the handshakes that Category 3 fixes:

| # | Severity | Feature pair | Test surface |
|---|---|---|---|
| #181 | P2 | CharacterCreation + HeroRace | Race ID round-trip via save/load (refs #171) |
| #182 | P2 | CompanionTactics + SmartCavalryAI | Behavior ordering + shared `SetMovementOrder` postfix (refs #170) |
| #183 | P2 | HeroRace | `RacePersistenceBehavior.OnSessionLaunched` restore + cross-feature persistence (refs #171, #130) |
| #187 | P2 | BannerColorPersistence | Triplet event-ordering + re-entry sequencing (refs #172, #122) |
| #188 | P2 | FactionMap | `CultureStageView` re-entry lifecycle — pending pins, stale VM (refs #175) |
| #189 | P2 | MixedFormations | `RepresentativeIsCavalry` guards (refs #170) |
| #190 | P2 | SmartCavalryAI | Cross-feature integration with MixedFormations cavalry exclusion (refs #170) |

### 4d. Model-layer untested branches (4 issues)

| # | Severity | Feature × Model | Test surface |
|---|---|---|---|
| #176 | (4b above) | CulturalFeats | — |
| #179 | P2 | RaceAge / `TaomPregnancyModel.GetDailyChanceOfPregnancyForHero` | 22 lines, 5 branches, ZERO tests |
| #180 | P2 | TroopProgression / `TaomPartyWageModel.GetTotalWage` | ~50 lines, multi-branch |
| #194 | P2 | SpecialResources | Tiered-cost + passive-discount regression test (refs #174 fix) |

### 4e. Service-method coverage gaps (3 issues — straightforward unit tests)

| # | Severity | Feature | Test surface |
|---|---|---|---|
| **#178** | **P1** | Warg | **Refactor `IWargAttackService` to accept `IAgentAdapter` per ADR-007 FIRST**, then add tests for `HandleWargTargetHit` + `WargAttack`. The refactor is the unlock — without it, 2 methods stay untestable. |
| #184 | P2 | NamedCompanions | `EnsureCompanionsPlaced` state-matrix coverage (Prisoner, Fugitive — refs #127) |
| #185 | P2 | AdvancedCombat | `SpatialGridDebugService.RenderDebugVisualization` |
| #186 | P2 | Spider | `SpawnSpiders` invocation contract (audit slightly overstates — team-assignment and monster-lookup are the actual gaps per 9a Batch D note) |

---

## Category 5 — Doc updates (4 issues — fast win, batch single session)

| # | Severity | Feature | Fix |
|---|---|---|---|
| **#196** | **P1** | Execution | **Write `docs/features/execution.md`** using `docs/features/TEMPLATE.md`. Phase 0 #19 carryover. Closing this silences the `detect-docs-gaps.sh` hook that has flagged it for 9+ sessions. |
| **#197** | **P3 (drift)** | CompanionTactics | Remove the stale "TEMP-SMARTCAVALRY-EXCLUDE" / "build-disabled" line from `companion-tactics.md` (around line 185). Single-line edit. Severity dropped from P2 per 9a Codex-confirmed mechanism check. |
| #198 | P2 | AdvancedCombat | Update `advanced-combat.md:71` Tests section — `BoneCollisionServiceTests.cs` exists with 252 lines / 11 tests, no longer "no tests exist" |
| #199 | P2 | Warg | Update `warg-combat.md:117` Tests section — `WargAttackServiceTests.cs` exists with 7 tests; cross-reference #178 ADR-007 blocker for the 2 untestable methods |

**Recommended commit:** Either one combined commit `docs(features): close stale audit-docs (#196, #197, #198, #199)` or 4 separate commits per feature. The Phase 9 prompt says "Batch by feature" — separate commits is the literal reading. Single combined commit is cleaner; user choice at fix time.

---

## Recommended Phase 9b session order

Per `phase-9-kickoff.md` preflight order, adapted for verified queue:

1. **Session 1 — Category 5 (Doc updates).** Fastest visible wins. Closes 4 issues. Sets up the doc-fix pattern.
2. **Session 2 — Category 1 (Mechanical wiring) + Category 4a (Wiring regression tests, 4 issues).** Closes 6 issues. Establishes the wiring-test pattern.
3. **Session 3 — Category 3 #173 (CareerPassiveHelper deletion).** Unlocks 4 Category 2a issues (#142, #144, #148, #173) plus enables Category 4d #180. Heavy lift but high-leverage.
4. **Session 4 — Category 2b R3 (config validation cluster).** #126, #128, #129, #131, #132, #133, #136 — `FiniteFloatValidator` + ordering invariants pattern. 7 issues.
5. **Session 5 — Category 2b R1 (singleton state reset cluster).** #124, #127, #128, #130, #131, #132, #133, #136, #143 — `OnNewGameCreatedEvent` retrofit pattern.
6. **Session 6 — Category 2b R2 (empty SyncData).** #128, #132, #133, #136, #141, #146.
7. **Session 7 — Category 2c P1 patches.** #149, #150 (1-line team filter + Prefix-style color injection).
8. **Session 8 — Category 4 (remaining test additions).** Mostly Phase 7 issues.
9. **Sessions 9-15 — remaining Category 2 (semantic) + Category 3 (cross-feature).**

**Total estimate:** 11–15 fix sessions (matches `phase-9-kickoff.md`).

---

## Tracking note for Phase 9b

When commit-closing each issue:
- Reference in commit message: `fix(<feature>): <action> (#<issue>)` or `test(<feature>): add <description> (#<issue>)`.
- Include in commit body: `Closes #<N>` for each closed issue.
- `gh issue close <N> --comment "Implemented in <SHA>. <one-line summary>."`.
- Update `CHANGELOG.md` with one line per closed issue.

Per CLAUDE.md "Completion Workflow (MANDATORY)":
- `/verify` before AND after every fix.
- `/deep-review` for ≥2-file changes or feature modules.
- `/codex-verify` (or focused `codex exec`) for P1 fixes, GameModel changes, cross-feature changes.
- Skip `/deep-review` for one-line wiring or pure doc edits.
