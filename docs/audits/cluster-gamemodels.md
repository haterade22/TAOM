# GameModel Cluster Audit — Phase 2

Last updated: 2026-05-13
Inputs: [feature-manifest.md](feature-manifest.md), [.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md), [.claude/rules/csharp-architecture.md](../../.claude/rules/csharp-architecture.md)
Scope: 11 features, 38 `Taom*Model.cs` overrides
Method: 1 `feature-dev:code-reviewer` agent per feature, each verifying override-body purity (gamemodels.md rule 4), null-safety on TaleWorlds chains, construction integrity vs SubModule.cs, and test coverage.

## Severity rubric (normalised across agent reports)

Individual agents calibrated severities slightly differently — this doc applies a consistent rubric across all 11 features:

- **P1** — feature non-functional. Ctor signature mismatch causing silent inert model; immediate NRE on common call paths; missing wiring that breaks gameplay.
- **P2** — degraded or silently inert. Rule 4 violations (inline branching in override body); missing config-provider validation; unguarded `.X` chains where `.X` can be null at common call times; service-locator inside model body; ADR-007 sealed-type access in model body; cross-feature static helper coupling.
- **P3** — cosmetic. Manifest miscounts; inline `IoC.Resolve` vs pre-resolved-to-local; redundant null guards; doc gaps unrelated to behaviour.

Where an agent reported a rule-4 violation as P1, this doc downgrades to P2. Where an agent reported an NRE-on-common-path issue as P2, this doc upgrades to P1.

## Manifest corrections (carry forward to Phase 1+)

- **CharacterCreation** Model count: manifest says 1 → actual 0 (`TaomCharacterStatsModel` lives under TroopProgression, not CharacterCreation)
- **CulturalFeats** Model count: manifest says 17 → actual 16 (the 17th line in SubModule's "Cultural feat models" block is `TaomTournamentModel` which belongs to Arena)
- **TroopProgression** Model count: manifest says 2 → actual 3 (manifest filing of `TaomPartyWageModel` under CulturalFeats may be a historical artifact)
- **gamemodels.md rule-file table** lists `TaomPartyHealingModel` under Arena, but file lives in `Main/Features/BattleBalance/Models/`. Update rule table.

## Master findings table (sorted by severity)

| # | Severity | Feature | Model | File:Line | Finding |
|---|---|---|---|---|---|
| 1 | **P1** | Siege | TaomSiegeEventModel | TaomSiegeEventModel.cs:23–24 | `party.MobileParty.HasPerk(...)` unguarded; `MobileParty` is null for garrison defenders → NRE on common siege path |
| 1b | **P1** | CulturalFeats | TaomPartySpeedModel | TaomPartySpeedModel.cs:30 | `Campaign.Current.MapSceneWrapper.GetFaceTerrainType(...)` unguarded; both can be null on scene transitions; called every tick per party → NRE on hot path |
| 2 | P2 | Arena | TaomTournamentModel | TaomTournamentModel.cs:23–37 | `GetTournamentStartChance` inline `.Count(...)` + `switch` expression (rule 4) |
| 3 | P2 | Arena | TaomTournamentModel | TaomTournamentModel.cs:26–28 | `Campaign.Current.Models.AgeModel.HeroComesOfAge` chain — no `?.`, NRE risk outside campaign |
| 4 | P2 | Arena | TaomTournamentModel | TaomTournamentModel.cs:39–43 | `GetTournamentEndChance` inline multi-line computation (rule 4) |
| 5 | P2 | Arena | TaomTournamentModel | TaomTournamentModel.cs:45–61 | `BuildPrizePool` private-static helper holds filtering logic — extract to service (rule 4 spirit) |
| 6 | P2 | Arena | TaomTournamentModel | TaomTournamentModel.cs:80–91 | `GetParticipantArmor` 3-step body (lookup, null-guard, equipment access) — rule 4 |
| 7 | P2 | ArmyTargeting | TaomTargetScoreModel | TaomTargetScoreModel.cs:26–28 | Ternary `effectiveStrength` business rule inline in override body (rule 4) |
| 8 | P2 | ArmyTargeting | TaomTargetScoreModel | TaomTargetScoreModel.cs:33–34 | `if (baseScore <= 0 \|\| missionType != Besieger)` routing in override body (rule 4) |
| 9 | P2 | BattleBalance | TaomPartyHealingModel | TaomPartyHealingModel.cs:53 | `IoC.Resolve<ICareerPassiveService>()` inside model body — service-locator (csharp-architecture "Constructor injection only" / feedback_no_service_locator_in_services) |
| 10 | P2 | BattleBalance | TaomPartyHealingModel | TaomPartyHealingModel.cs:23–63 | 41-line override body with nested `if` × 5 (rule 4) |
| 11 | P2 | BattleBalance | TaomMilitaryPowerModel | TaomMilitaryPowerModel.cs:19–55 | Hero-tier derivation + multiplier selection inline in override (rule 4) |
| 12 | P2 | BattleBalance | BattleBalanceConfigProvider | BattleBalanceConfigProvider.cs:38–39 | No NaN/Infinity/range validation on `TierPower` (T0–T10) + `CulturalSurvivalBonuses` floats from JSON (csharp-architecture "Config Providers MUST Validate"; bug class has shipped 3× per `feedback_editor_fields_are_config`) |
| 13 | P2 | CareerSystem | TaomAgentStatCalculateModel | TaomAgentStatCalculateModel.cs:31–89 | `UpdateAgentStats` ~55-line override with nested `if` + property mutation block (rule 4) |
| 14 | P2 | CareerSystem | TaomAgentApplyDamageModel | TaomAgentApplyDamageModel.cs:19–65 | `ApplyDamageAmplifications` + `ApplyDamageReductions` + `DecideAgentShrugOffBlow` all have inline branching (rule 4); also inconsistent null-guard placement (early-exit vs nested) |
| 15 | P2 | CareerSystem | TaomInventoryCapacityModel | TaomInventoryCapacityModel.cs:10–24 | `CareerPassiveHelper.ApplyFactor` static-resolver chain → `IoC.Resolve<ICareerPassiveService>` on hot path (service-locator) |
| 16 | P2 | CareerSystem | TaomMapVisibilityModel | TaomMapVisibilityModel.cs:10–19 | Same `CareerPassiveHelper` service-locator anti-pattern; `GetPartySpottingRange` is on world-map tick hot path |
| 17 | P2 | Diplomacy | TaomAllianceModel | TaomAllianceModel.cs:34–37 | Inline `if (modifier != 0f)` zero-skip branch (rule 4) |
| 18 | P2 | Diplomacy | TaomDiplomacyModel | TaomDiplomacyModel.cs:18–25 | `IsAtConstantWar` inline two-condition branch (rule 4) |
| 19 | P2 | Diplomacy | TaomDiplomacyModel | TaomDiplomacyModel.cs:32–38 | `GetRelationChange...VotingInSettlementOwner...` Isengard feat branch inline in model — **untestable** because takes sealed `Hero` (rule 4 + ADR-007) |
| 20 | P2 | Diplomacy | TaomKingdomDecisionPermissionModel | TaomKingdomDecisionPermissionModel.cs:18–28, 31–40, 44–53 | 3 decision-gate methods each have inline `if` branching (rule 4 × 3) |
| 21 | P2 | Diplomacy | WarOfTheRingConfigProvider | WarOfTheRingConfigProvider.cs:34–35 | No ordering validation Phase1.TriggerDay < Phase2.TriggerDay; sign-flipped config silently inverts phase transition (csharp-architecture "Config Providers MUST Validate") |
| 22 | P2 | Diplomacy | DiplomacyConfigProvider | DiplomacyConfigProvider.cs:34–35 | No enum-string validation; unknown `Tier` string deserialises to `Neutral` (zero default) silently |
| 23 | P2 | Encyclopedia | TaomInformationRestrictionModel | TaomInformationRestrictionModel.cs:12 | Reaches `TaomSettings.Instance?.ShowAllEncyclopediaCharacters` directly — concrete static accessor instead of injected interface (csharp-architecture "Constructor injection only") |
| 24 | P2 | Execution | TaomExecutionRelationModel | TaomExecutionRelationModel.cs:16–33 | Override body has null-guard `if` + delegate call + `showQuickNotification = false` side-effect mutation (rule 4) |
| 25 | P2 | Execution | TaomExecutionRelationModel | TaomExecutionRelationModel.cs:20 | `Hero.MainHero.MapFaction.StringId` direct access — sealed type in model body (ADR-007) |
| 26 | P2 | Execution | TaomExecutionRelationModel + SubModule.cs:272–273 | Architectural | `IOnExecutionAction` (a HOOK interface) injected into a GameModel — no other model in the registry uses this pattern; should be a service interface |
| 27 | P2 | RaceAge | TaomPregnancyModel | TaomPregnancyModel.cs:41 | `hero.Spouse.GetPerkValue(...)` — `.Spouse` accessed without `?.` after restructured method body (adapters.md "use `?.` for computed properties") |
| 28 | P2 | RaceAge | TaomPregnancyModel | TaomPregnancyModel.cs:40 | `hero.Clan.Tier` / `hero.Clan.AliveLords.Count` — `.Clan` is nullable for wanderers / spawn-in-progress; bare `.Clan.X` will NRE |
| 29 | P2 | RaceAge | TaomPregnancyModel | TaomPregnancyModel.cs:36–58 | 22-line `GetDailyChanceOfPregnancyForHero` with fertility window + decline rate + population factor inline (rule 4) — also entirely untested |
| 30 | P2 | Siege | TaomSiegeEventModel | TaomSiegeEventModel.cs:21–28 | `foreach` + `yield return` + private static `Resolve(kind)` switch all inside override body (rule 4) |
| 31 | P2 | Siege | TaomSiegeEventModel | TaomSiegeEventModel.cs:31–39 | No `base.GetAvailableDefenderSiegeEngines` call — fully replaces vanilla list; future engine additions silently disappear (gamemodels.md rule 3) |
| 32 | P2 | TroopProgression | TaomPartyWageModel | TaomPartyWageModel.cs:37–86 | `GetTotalWage` ~50-line override with garrison-wage feat loop + Rohan mounted-share calc + `CareerPassiveHelper.ApplyFactor` (rule 4) |
| 33 | P2 | TroopProgression | TaomPartyWageModel | TaomPartyWageModel.cs:88–112 | `GetTroopRecruitmentCost` inline horse-cost branch + 2 mounted-feat guards (rule 4) |
| 34 | P2 | TroopProgression | TaomPartyWageModel | TaomPartyWageModel.cs:83 | `CareerPassiveHelper.ApplyFactor(...)` static cross-feature call inside model (couples TroopProgression → CareerSystem at entry-point layer) |
| 35 | P2 | TroopProgression | TroopProgressionIoC.cs | TroopProgressionIoC.cs | `IVolunteerContextAdapter` is feature-owned but registered in global `IoC.cs:110` instead of feature IoC class — cohesion gap |
| 36 | P2 | CulturalFeats | ALL 16 models | Models/Taom*.cs | **Systemic rule-4 violation across all 16 models** — `if`/`foreach`/multi-line computation in every override body. No `ICulturalFeatsService` exists. (Detail table inside CulturalFeats section.) |
| 37 | P2 | CulturalFeats | TaomCaravanModel | TaomCaravanModel.cs:13 | `CharacterObject.PlayerCharacter` static singleton inside override; `GetCaravanFormingCost` is called for AI caravans too → feat silently player-only despite description implying faction-wide (semantic mismatch) |
| 38 | P2 | CulturalFeats | TaomBattleRewardModel | TaomBattleRewardModel.cs:25 | Asymmetric coalesce: feat path uses `party.Owner?.Culture ?? party.Culture`; career path uses `party.Owner ?? party.LeaderHero`. Two paths can disagree on party "owner" → feat applies, career skips (or vice versa) |
| 39 | P2 | CulturalFeats | TaomClanFinanceModel | TaomClanFinanceModel.cs:19 | `clan?.Culture` uses `?.` on a parameter typed non-nullable `Clan`. Silently skips feat instead of surfacing invariant violation |
| 40 | P2 | CulturalFeats | TaomSettlementProsperityModel | TaomSettlementProsperityModel.cs:22,25,28 | `result.ResultNumber >= 0f` compound condition is a business rule (apply-only-if-growing), not a guard — belongs in service |
| 41 | P2 | CulturalFeats | TaomSmithingModel | TaomSmithingModel.cs:29–56 | Logic extracted to private static `ApplySmithingFeatReduction` on the model class — rule 4 requires extraction to a Service, not to a method on the model |
| 42 | P2 | CulturalFeats | TaomPartySpeedModel | TaomPartySpeedModel.cs:47–61 | `foreach` over `roster.GetTroopRoster()` with `mountedCount` accumulator on every tick. No early-exit on culture mismatch, no caching |
| 43 | P2 | CulturalFeats (cross-feature) | 8 of 16 models | Various | Cross-feature static coupling: 8 models call `CareerPassiveHelper.ApplyFactor` directly inside override body. Couples CulturalFeats → CareerSystem at entry-point layer (Pattern D). Phase 6 review target. |

(Per-feature P3 notes appear in their respective sections below.)

## Per-feature reports

### 1. Arena (1 model — TaomTournamentModel)

**Construction integrity:** ✅ No-arg ctor matches `new TaomTournamentModel()` at SubModule.cs:284. No P1.

**Findings:** 5 P2 + 3 P3 (rows 2–6 in master table)

**Test coverage:** `TaomTournamentModelTests.cs` exists but tests constants + the private static `ResolveDummyId` only. The override paths (`GetTournamentStartChance`, `GetTournamentEndChance`, `GetRegularRewardItems`, `GetEliteRewardItems`, `GetParticipantArmor`) are untested because all their logic lives inline in the model with no service to test. **Gap: HIGH** — extraction to `ITournamentService` is prerequisite for coverage.

**P3 notes:**
- SubModule.cs:284 (`new TaomTournamentModel()`) is registered in the "Cultural feat models" block (lines 275-292), not in a dedicated Arena block. Organizational inconsistency.
- `Main/Features/Arena/` has only the `Models/` subfolder — no IoC.cs, no `Hooks/`, no service file. Consistent with current minimalism.
- `ResolveDummyId`'s `settlementCultureId` param hardcoded to `null` at line 84 — second fallback branch is dead code from current caller.

### 2. ArmyTargeting (1 model — TaomTargetScoreModel)

**Construction integrity:** ✅ Ctor `(IArmyTargetingService)` matches line 304 `new TaomTargetScoreModel(armyTargetingService)`. No P1.

**Findings:** 2 P2 (rows 7–8 in master table)

**Test coverage:** No model test, but `ArmyTargetingServiceTests.cs` covers all 4 service methods across 18 test cases. **Per gamemodels.md rule 8, acceptable** — until the rule-4 inline-branching logic is extracted to the service, those code paths remain untested.

**P3 notes:**
- `army_targeting.json` is Singleton-cached; would benefit from "requires app restart" doc note.
- `GetDistanceCompensation` zero-scale path uncovered in service tests.

### 3. BattleBalance (3 models)

**Construction integrity:** ✅ All 3 ctors match SubModule.cs lines 297–299 exactly:
- `TaomMilitaryPowerModel(IBattleBalanceSettingsProvider, IBattleBalanceConfigProvider)` ✓
- `TaomCombatSimulationModel(IBattleBalanceSettingsProvider)` ✓ (1-arg ctor — no silent config drop)
- `TaomPartyHealingModel(IBattleBalanceSettingsProvider, IBattleBalanceConfigProvider)` ✓

No P1. The audit suspicion that line 298 (1-arg) might be silently dropping a config dep is unfounded — the ctor genuinely takes 1 arg.

**Findings:** 4 P2 (rows 9–12 in master table) + 1 P3

**Test coverage:** 3 model test files exist. Coverage is partial:
- `TaomCombatSimulationModel` — `CalculateBluntChance` static helper covered; `EnableCustomCasualtyRatios = false` early-return branch is untested.
- `TaomMilitaryPowerModel` — `CalculateTierPower` static helper covered; hero-tier derivation + multiplier selection (inline in override) is untested.
- `TaomPartyHealingModel` — `ApplyCulturalSurvivalBonus` static helper covered; `GetSurvivalChance` full path (feature-flag, career-passive `IoC.Resolve`, party-null, cultural-config double-gate) is **structurally untestable** because of the service-locator call.

**P3 notes:**
- `TaomCombatSimulationModel.GetBluntDamageChance` 5 params unguarded — base call defends, but pattern is inconsistent.

### 4. CareerSystem (5 models)

**Construction integrity:** ✅ All 5 ctors match SubModule.cs lines 330–334.
- `TaomMapVisibilityModel()` no-args ✓ (but delegates via `CareerPassiveHelper` service-locator — P2)
- `TaomInventoryCapacityModel()` no-args ✓ (same anti-pattern — P2)
- `TaomAgentStatCalculateModel(ICareerPassiveService)` ✓
- `TaomAgentApplyDamageModel(ICareerPassiveService)` ✓
- `TaomClanTierModel(ICareerPassiveService)` ✓

No P1. The audit suspicion that the 2 no-arg models were dropping `ICareerPassiveService` is technically correct in spirit — they DO use the service, just via a static service-locator shim instead of constructor injection.

**Findings:** 5 P2 (rows 13–16 in master table; row 14 covers all 3 violations in TaomAgentApplyDamageModel) + 3 P3

**Test coverage:** None of the 5 models have `*ModelTests.cs`. Service-level coverage via `CareerPassiveServiceTests.cs` is good for `TaomClanTierModel` (genuinely thin) but **inadequate** for `TaomAgentStatCalculateModel.UpdateAgentStats` (55 lines of inline logic with `CareerAbilityBuffTracker` integration path uncovered) and `TaomAgentApplyDamageModel` (resistance + hero-buff + ally-buff + shrug-off branches all inline).

**P3 notes:**
- SubModule.cs:329 stale comment — says "reuse careerPassiveService resolved above (line 300)" but actual resolution is line 317. Off by 17 lines.
- `AddModel<AgentStatCalculateModel>(...)` + `AddModel<AgentApplyDamageModel>(...)` (lines 332–333) use explicit type-parameter — different from other AddModel calls. Intentional: these inherit directly from abstract base, not `Default*`. The pattern is correct; add a one-line comment explaining the divergence for future maintainers.
- 3 models have `if (_passiveService == null) return baseValue` defensive guards that are unreachable at runtime (the service is resolved unconditionally at line 317). Remove the noise or document intent.

### 5. CulturalFeats (16 models — highest-value target)

**Construction integrity:** ✅ All 16 ctors match SubModule.cs lines 276–292.
- 15 are no-arg construction matching no-arg ctors (genuinely no service deps — feat dispatch is the only logic).
- `TaomSettlementLoyaltyModel(IRevoltTuningConfigProvider)` at line 288 matches a 1-arg ctor that takes a snapshot of the config at construction time and stores it as a plain value (correct Singleton-lifetime pattern).

No P1 silent service drops. The audit suspicion ("16 models with no-arg new TaomXxx — risk of silent service drop") is **cleared by inspection** — all 16 are genuinely no-arg by design.

**Findings:** 1 P1 + 7 P2 (rows 36–43 in master table) + 2 P3

The P1 is `TaomPartySpeedModel.cs:30` — `Campaign.Current.MapSceneWrapper` accessed without null-guard in `CalculateFinalSpeed`, which runs every tick per party. `Campaign.Current` is null before campaign load; `MapSceneWrapper` can be null on scene transitions. NRE on this hot path.

**Systemic rule-4 violation:** **all 16 models** contain `if`-branching, multi-line computation, or `foreach` logic in their override bodies. No `ICulturalFeatsService` exists — the feat-dispatch logic is inlined everywhere. Specific violation patterns (line ranges in master table row 36):

| Model | Branching constructs |
|---|---|
| TaomArmyManagementModel:9–52 | 2 ifs (DailyInfluenceAward); 4 ifs + `multiplier` accumulator (InfluenceCost) |
| TaomBattleRewardModel:16–30 | 1 if; asymmetric coalesce; cross-feature `CareerPassiveHelper` |
| TaomBuildingConstructionModel:15–36 | 4 ifs |
| TaomCaravanModel:9–17 | 1 if; `MathF.Round`; `CharacterObject.PlayerCharacter` static singleton — feat silently player-only |
| TaomClanFinanceModel:14–27 | 1 if |
| TaomFoodConsumptionModel:14–36 | 4 ifs |
| TaomPartyMoraleModel:16–44 | 5 ifs; cross-feature `CareerPassiveHelper` |
| TaomPartySizeModel:16–45 | 5 ifs; cross-feature `CareerPassiveHelper` |
| **TaomPartySpeedModel:22–68** | **`Campaign.Current.MapSceneWrapper` unguarded; terrain if; 2 inner ifs; Rohan if + `foreach` + `mountedCount` accumulator over full roster per tick; cross-feature `CareerPassiveHelper`** |
| TaomPartyTroopUpgradeModel:16–36 | IsMounted guard + 2 inner ifs; cross-feature `CareerPassiveHelper` |
| TaomRaidModel:16–39 | 3 ifs; cross-feature `CareerPassiveHelper` |
| TaomSettlementLoyaltyModel:34–58 | 5 ifs in `CalculateLoyaltyChange` (property constant overrides are fine) |
| TaomSettlementMilitiaModel:9–24 | 2 ifs |
| TaomSettlementProsperityModel:14–32 | 3 ifs each compounded with `result.ResultNumber >= 0f` business-rule guard |
| TaomSmithingModel:29–56 | logic extracted to private static `ApplySmithingFeatReduction` on the model (not to a service — rule 4 spirit violated) |
| TaomVillageProductionModel:14–34 | `isGrain` local + 3 ifs (2 grain-gated) |

**Notable individual P2s** (in addition to the systemic rule-4):
- **TaomCaravanModel:13** — `CharacterObject.PlayerCharacter` static accessed inside override; `GetCaravanFormingCost` is also called for AI caravans → feat silently player-only despite description implying faction-wide. Semantic bug.
- **TaomBattleRewardModel:25** — asymmetric coalesce between feat path (`party.Owner?.Culture ?? party.Culture`) and career path (`party.Owner ?? party.LeaderHero`). The two paths can disagree on which entity "owns" the party.
- **TaomClanFinanceModel:19** — `clan?.Culture` uses `?.` on a parameter declared as non-nullable `Clan`. If callers send null, the feat silently skips rather than surfacing the invariant violation.
- **TaomSettlementProsperityModel:22,25,28** — `result.ResultNumber >= 0f` compound condition is a business rule ("only apply bonus if hearth is growing"), not a guard.

**P3 notes:**
- `TaomSmithingModel:42–44` — dead-code branch `if (factor == 0f && hero == null)` is unreachable (hero is required and earlier guard returns).
- 8 of 16 models import `CareerSystem` namespaces and call `CareerPassiveHelper.ApplyFactor` directly inside the override body — Pattern D (cross-feature static coupling at the entry-point layer).
- `TaomCulturalFeats._instance` is a mutable static; `CreateAndRegister()` is called from a Harmony Postfix on `Campaign.InitializeDefaultCampaignObjects` (single-threaded init), so no race. Noted for completeness.
- `TaomPartyWageModel` does NOT live under `Main/Features/CulturalFeats/Models/` — it's under TroopProgression. Confirms the manifest miscount carried forward at the top of this doc.

**Test coverage:** No `*ModelTests.cs` for any of the 16. `TaomCulturalFeatsDefinitionTests.cs` covers feat registry structure (property count, uniqueness, per-culture counts) but exercises **zero override calculation paths**. No `*FeatsService` exists, so per-gamemodels.md rule 8 the coverage rule fails for all 16. **The fix is "extract to service, then test the service."**

### 6. Diplomacy (3 models)

**Construction integrity:** ✅ All 3 ctors match SubModule.cs lines 260–262 exactly:
- `TaomAllianceModel(IDiplomacyService)` ✓
- `TaomKingdomDecisionPermissionModel(IDiplomacyService, IWarOfTheRingService)` ✓
- `TaomDiplomacyModel(IWarOfTheRingService)` ✓

No P1.

**Findings:** 6 model P2 + 2 config-provider P2 (rows 17–22) + 3 P3

**Test coverage:** Hook + service tests exist. The `TaomDiplomacyModel.GetRelationChange...` Isengard branch is **completely untestable** because it lives inline in the model and accesses sealed `Hero`. Service tests cover `IsAllianceAllowed` / `GetRelationshipTier` / `ShouldBlockPeace` but cannot reach the inline model logic.

**P3 notes:**
- `TaomAllianceModel.MaxDurationOfAlliance => CampaignTime.Years(100)` is a hardcoded ceiling. Acceptable but belongs in config if tuning is expected.
- Both config providers are `Reuse.Singleton` — config changes require full app restart, not save reload. Undocumented.
- Unused `using TAOM.Features.CulturalFeats;` in `TaomDiplomacyModel` line 3 — moves with the Isengard logic if extracted.

### 7. Encyclopedia (1 model — TaomInformationRestrictionModel)

**Construction integrity:** ✅ No-arg ctor matches `new TaomInformationRestrictionModel()` at SubModule.cs:301. No P1.

**Findings:** 1 P2 (row 23) + 1 P3

**Test coverage:** `TaomInformationRestrictionModelTests.cs` covers the `Func<bool>` seam's true/false branches but **does not** test the `TaomSettings.Instance == null` fallback path (production lambda returns `true` when MCM unavailable — silently disables the restriction).

**P3 notes:**
- Null-coalescence default is `true` ("show all"), which inverts the "restrict" semantics of `InformationRestrictionModel` when MCM is unavailable. Either document as intentional safe-default, or change to `false`.

### 8. Execution (1 model — TaomExecutionRelationModel)

**Construction integrity:** ✅ Ctor `(IOnExecutionAction)` matches line 273 `new TaomExecutionRelationModel(executionAction)`. No P1 — but the choice of injecting a HOOK interface into a MODEL is itself a P2 architectural smell (row 26).

**Findings:** 3 P2 (rows 24–26) + 1 P3

**Test coverage:** No model test. `ExecutionActionHookTests.cs` covers the hook's logic but never exercises the model's `out bool showQuickNotification` mutation. 3 null-guard branches (executor / victim / evaluator kingdom == null) are also uncovered.

**P3 notes:**
- `docs/features/execution.md` missing per `detect-docs-gaps.sh` (carries forward to Phase 8 docs audit).

### 9. RaceAge (3 models)

**Construction integrity:**
- ✅ `TaomAgeModel(IRaceAgeService)` ✓ at line 252
- ✅ `TaomPregnancyModel(IRaceAgeService)` ✓ at line 253
- ✅ `TaomHeroCreationModel()` no-args — **CLEARED, not P1.** The class genuinely needs no service; override is a pure parent-selection swap (offspring sex → mother/father.CharacterObject). The audit suspicion was correct to flag this; the actual ctor inspection clears it.

**Findings:** 2 P2 (rows 27–29) + 1 P3

**Test coverage:** `TaomAgeModelTests.cs` exists (6 tests, model is thin enough). `TaomPregnancyModel` has zero model tests AND its `GetDailyChanceOfPregnancyForHero` formula (fertility window, decline rate, population factor, perk bonus) lives entirely inline → untested. `TaomHeroCreationModel` body is genuinely 2 lines; low-priority gap.

**P3 notes:**
- `TaomAgeModel.MaxAge => 10000` and `BecomeOldAge => 5000` hardcoded sentinels — appears to be "make base harmless" pattern but bypasses the per-character `GetAgeLimitForLocation` dispatch. Verify via ilspycmd whether any vanilla code reads `AgeModel.MaxAge` directly; if so, the sentinel is wrong.

### 10. Siege (1 model — TaomSiegeEventModel)

**Construction integrity:** ✅ Ctor `(ISiegeEngineAvailabilityService)` matches line 270 `new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>())`. No P1 here.

**Findings:** 1 P1 + 2 P2 (rows 1, 30, 31) + 3 P3

The P1 is the unguarded `party.MobileParty.HasPerk(...)` chain — garrison defenders (settlements without a leader army) commonly have `MobileParty == null`, so this NREs on most siege opens.

**Test coverage:** No model test. `SiegeEngineAvailabilityServiceTests.cs` covers `GetDefenderEngines` with 4 tests at the enum boundary. The `DefenderSiegeEngineKind → SiegeEngineType` resolution (`Resolve` switch on the model) is **untestable** because it lives as a private static on the model.

**P3 notes:**
- IoC class name `SiegeDefenseIoC.cs` under `Siege/` dir — cosmetic mismatch.
- SubModule.cs:270 uses inline `IoC.Resolve<...>` instead of pre-resolved local var pattern used by surrounding lines.
- `SiegeEngineAvailabilityService` is stateless; `Reuse.Singleton` is fine but slightly misleading for what is a pure function.

### 11. TroopProgression (3 models)

**Construction integrity:** ✅ All 3 ctors match SubModule.cs lines 244–246.
- `TaomCharacterStatsModel()` no-args ✓ (single-constant override `MaxCharacterTier => 10` — genuinely needs no service)
- `TaomPartyWageModel(ITroopCostService)` ✓
- `TaomVolunteerModel(IVolunteerTierService, IVolunteerRecruitmentService, IVolunteerContextAdapter)` ✓

No P1.

**Findings:** 4 P2 (rows 32–35) + 2 P3

**Test coverage:** 3 service test files exist; `TaomCharacterStatsModel` needs no test (single-constant). `TaomVolunteerModel` is genuinely thin → covered. `TaomPartyWageModel.GetTotalWage` + `GetTroopRecruitmentCost` have ~75 lines of inline logic with **zero test coverage** because the model has no tests and the logic isn't extracted to a service.

**P3 notes:**
- Manifest miscount: 3 models on disk, manifest says 2 (`TaomPartyWageModel` filed under CulturalFeats in some places — historical artifact).
- `troopRoster != null` null-check at line 65 is redundant given the parameter type and prior guards.

## Cross-cuts (patterns visible across multiple features)

### Pattern A — Rule-4 inline-branching violations are pervasive (44+ instances across 38 models)

The systemic finding. Distribution:
- Arena (5), ArmyTargeting (2), BattleBalance (2), CareerSystem (3), Diplomacy (5), Execution (1), RaceAge (1), Siege (1), TroopProgression (2)
- **CulturalFeats (16 — every single model)**

This is **the dominant systemic finding of the entire Phase 2 audit.** The rule is documented in `.claude/rules/gamemodels.md` but not enforced via linting or pre-commit hooks. Out of 38 models, **at least 22 violate rule 4 directly** (and the 16 CulturalFeats models are universal violators).

**Recommendation for Phase 9**: open a single tracking issue ("Rule 4 sweep across model overrides — feat-dispatch + branching extraction") that umbrella-tracks every instance. Per-feature service extractions can land as separate sub-issues, but the umbrella ties them to the systemic finding. Consider a lint rule: any class inheriting from `Default*Model` whose override body contains `if`/`foreach`/`switch`/`yield` is a build warning.

### Pattern B — Config provider validation gaps

3 confirmed gaps so far (BattleBalance, WarOfTheRing, Diplomacy). Pattern matches `feedback_editor_fields_are_config.md` — bug class has shipped 3× previously (Career #31, EditorCacheRebuild #38, scene-scripts CS_Road). **`FiniteFloatValidator` exists; providers aren't using it.** Phase 9 fix is mechanical.

### Pattern C — Service-locator inside model bodies

3 confirmed instances:
- `TaomPartyHealingModel.cs:53` direct `IoC.Resolve<ICareerPassiveService>`
- `TaomInventoryCapacityModel` + `TaomMapVisibilityModel` via `CareerPassiveHelper.ApplyFactor` (lazy `IoC.Resolve` in a static helper)
- `TaomInformationRestrictionModel` reaching `TaomSettings.Instance` (static singleton accessor)

All violate `csharp-architecture.md` "Constructor injection only" and `feedback_no_service_locator_in_services.md`. **Phase 9 fix:** inject the dependencies via constructor.

### Pattern D — Cross-feature coupling via static helpers

**9 confirmed instances** of `CareerPassiveHelper.ApplyFactor(...)` called directly from a model body:
- `TaomPartyWageModel.cs:83` (TroopProgression → CareerSystem)
- 8 CulturalFeats models: BattleReward, PartyMorale, PartySize, PartySpeed, PartyTroopUpgrade, Raid, Smithing (and TaomSettlementLoyaltyModel via `_revoltConfig`, but that's a different kind — explicit ctor injection)

This couples CulturalFeats + TroopProgression → CareerSystem at the model (entry-point) layer instead of via service composition. **All 9 should be addressed when the Pattern A service-extraction lands**: the extracted services should depend on `ICareerPassiveService` via constructor injection, eliminating the `CareerPassiveHelper` static shim entirely.

The clean cross-feature dependency template is `TaomSettlementLoyaltyModel(IoC.Resolve<IRevoltTuningConfigProvider>())` at SubModule.cs:288 — explicit ctor injection of the config provider. RevoltTuning produces the config; CulturalFeats consumes it.

**Note for Phase 6 cross-feature handshake review**: the 9 `CareerPassiveHelper` instances are the highest-density cross-feature coupling surface in TAOM.

### Pattern E — `Hero.MainHero` direct access in model bodies

1 confirmed (`TaomExecutionRelationModel.cs:20`). ADR-007 violation — sealed types should not cross the model boundary. Phase 6 may surface more.

## GitHub issues opened

10 issues filed against `audit-impl` label this phase. 2 P1s as individual issues, 8 P2 per-feature groupings. P3 items noted inline in per-feature sections only — no issues.

| # | Title | Severity | Feature |
|---|---|---|---|
| [#134](https://github.com/haterade22/TAOM/issues/134) | Siege — TaomSiegeEventModel party.MobileParty.HasPerk unguarded NRE for garrison defenders | **P1** | Siege |
| [#135](https://github.com/haterade22/TAOM/issues/135) | CulturalFeats — TaomPartySpeedModel Campaign.Current.MapSceneWrapper unguarded NRE on per-tick hot path | **P1** | CulturalFeats |
| [#137](https://github.com/haterade22/TAOM/issues/137) | Arena — TaomTournamentModel inline branching + unguarded Campaign.Current chain (5 P2) | P2 ×5 | Arena |
| [#138](https://github.com/haterade22/TAOM/issues/138) | ArmyTargeting — TaomTargetScoreModel inline ternary + early-return branching (2 P2) | P2 ×2 | ArmyTargeting |
| [#140](https://github.com/haterade22/TAOM/issues/140) | BattleBalance — IoC.Resolve in TaomPartyHealingModel + rule-4 + config validation (4 P2) | P2 ×4 | BattleBalance |
| [#142](https://github.com/haterade22/TAOM/issues/142) | CareerSystem GameModels — UpdateAgentStats inline + ApplyDamage branching + CareerPassiveHelper service-locator (5 P2) | P2 ×5 | CareerSystem |
| [#144](https://github.com/haterade22/TAOM/issues/144) | CulturalFeats — systemic rule-4 across all 16 models + 6 specific bugs (7 P2) | P2 ×7 | CulturalFeats |
| [#145](https://github.com/haterade22/TAOM/issues/145) | Encyclopedia — TaomInformationRestrictionModel reaches TaomSettings.Instance static instead of injected interface (1 P2) | P2 ×1 | Encyclopedia |
| [#147](https://github.com/haterade22/TAOM/issues/147) | Execution — TaomExecutionRelationModel architectural smell (hook injected into model) + ADR-007 + rule-4 (3 P2) | P2 ×3 | Execution |
| [#148](https://github.com/haterade22/TAOM/issues/148) | TroopProgression — TaomPartyWageModel inline branching + cross-feature CareerPassiveHelper + IoC cohesion gap (4 P2) | P2 ×4 | TroopProgression |

### Coverage handoff to existing audit-impl issues

Phase 2 findings for **Diplomacy** and **RaceAge** GameModels overlap substantially with already-open audit-impl issues opened during a prior session's Phase 3 (CampaignBehavior cluster) work:

- **#131 RaceAge — TaomPregnancyModel ADR-007 violation + singleton race cache stale + R3+R4** already captures the Pregnancy model's rule 4 violation, NaN/Infinity config gap, and validate-before-lookup pattern. The new null-safety findings (`hero.Spouse` / `hero.Clan` unguarded chains at lines 40-41) and the `TaomAgeModel` sentinel-constants P3 are **noted in this cluster doc's RaceAge section** but do not warrant a separate issue — Phase 9 triage of #131 should cover them when the model is touched.
- **#129 Diplomacy — WarOfTheRing.CurrentPhase unsaved + config validation gaps** already covers `WarOfTheRingConfigProvider` ordering invariants and `DiplomacyConfigProvider` `?? new T()` fallback gaps. New rule-4 findings on `TaomAllianceModel`, `TaomDiplomacyModel`, `TaomKingdomDecisionPermissionModel` (rows 17–20 of master table) are **noted in this cluster doc** and tracked as in-scope for #129's fix work or for a follow-up; we did NOT open a duplicate.

### Cross-cutting umbrella (not separately issued)

Pattern A (systemic rule-4) and Pattern D (`CareerPassiveHelper.ApplyFactor` cross-feature coupling in 9 model bodies) are NOT opened as separate umbrella issues — each per-feature issue cites the patterns. Phase 9 triage can batch them across features when fixing rule-4 systematically.

## Phase 2 complete

- **Models reviewed: 38 of 38** (across 11 features)
- **P1 findings: 2**
  - Siege `TaomSiegeEventModel.cs:23-24` — `party.MobileParty.HasPerk` unguarded (garrison defender NRE)
  - CulturalFeats `TaomPartySpeedModel.cs:30` — `Campaign.Current.MapSceneWrapper` unguarded (per-tick hot path NRE on scene transitions)
- **P2 findings: 42 distinct rows** (one of which — row 36 — umbrellas a systemic rule-4 violation across all 16 CulturalFeats models, so actual sub-violations exceed 42)
- **P3 findings: ~20** (noted inline in per-feature sections only; no issues opened)
- **Construction integrity: all 38 reviewed models pass.** No Messengers-class silent service drops in the GameModel cluster. The Messengers crash was specific to the CampaignBehavior layer; the model layer is wired correctly across the board. This is the single most valuable assurance produced by Phase 2 — the audit's core question ("is any other feature in the Messengers state?") gets a clean "no" for the model layer.

The audit had three load-bearing concerns going in. Resolution:

| Concern | Resolution |
|---|---|
| `TaomHeroCreationModel()` (no-args at SubModule:254) drops `IRaceAgeService` | **CLEARED** — ctor is genuinely parameterless; override is a 2-line parent-selection swap requiring no TAOM service |
| `TaomCharacterStatsModel()` (no-args at SubModule:244) drops `ITroopCostService` | **CLEARED** — single-constant override `MaxCharacterTier => 10`; no service needed |
| 15 CulturalFeats no-arg constructions drop a service | **CLEARED** — all 15 ctors are genuinely parameterless. The 16 models share a feat-dispatch pattern via the static `TaomCulturalFeats._instance`, not via injected services |

The risk shifted from "silent service drop" (none found) to "systemic rule-4 violation" (44+ instances) and "service-locator inside model body" (4 instances, all in CareerSystem + BattleBalance). Both are addressable in Phase 9 without breaking the wiring confirmed in this phase.

### Implications for later phases

- **Phase 3 (CampaignBehaviors)**: Pattern A (rule 4 inline-branching) likely repeats. Pre-prepare the consolidation strategy.
- **Phase 4 (Harmony patches)**: Service-locator anti-pattern (Pattern C) may surface in patch bodies too — auditor should flag.
- **Phase 5 (UI / Mixin / Prefab)**: Pattern C is the highest-risk pattern in UI VMs (recurring per memory `feedback_prefer_public_setter_over_reflected_notify` family).
- **Phase 6 (cross-feature handshake)**: Patterns D + E warrant explicit attention. Targets: `TaomPartyWageModel ↔ CareerPassiveHelper`, `TaomSettlementLoyaltyModel ↔ RevoltTuning`, `Hero.MainHero` usages.
- **Phase 7 (test coverage)**: ~35 of 38 models have at least one untested code path because logic is inline. The fix is "extract to service, then test the service."
- **Phase 8 (docs)**: `docs/features/execution.md` confirmed missing; no other gaps surfaced in this phase.
- **Phase 9 (triage)**: The 35 P2 findings cluster well into 5 patterns (A–E). Recommend Pattern A as the largest batch fix.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/cluster-campaign-behaviors.md](./cluster-campaign-behaviors.md)
- [docs/audits/cluster-harmony-patches.md](./cluster-harmony-patches.md)
- [docs/audits/phase-4-kickoff.md](./phase-4-kickoff.md)
- [docs/audits/phase-5-kickoff.md](./phase-5-kickoff.md)
- [docs/audits/phase-6-kickoff.md](./phase-6-kickoff.md)
- [docs/audits/phase-7-kickoff.md](./phase-7-kickoff.md)
- [docs/audits/phase-8-kickoff.md](./phase-8-kickoff.md)

<!-- backlinks-end -->
