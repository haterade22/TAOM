# Wiring Matrix — Phase 1

Last updated: 2026-05-13
Inputs: [feature-manifest.md](feature-manifest.md) (43 features) × 5 probe categories from [phase-1-kickoff.md](phase-1-kickoff.md)

## Executive summary

Phase 1 ran 5 parallel probes against the entire feature set. The audit found **1 P2 wiring miss** and **1 P3 manifest classification error**:

- **P2 — BannerColorPersistence.MobilePartyVisual_AddCharacterToPartyIcon_Patch is wired but never initialized.** The Postfix uses `_service` and `_heroAdapter` static fields that no `Initialize(...)` caller ever populates. Result: player-clan colors do NOT persist on world-map party icons — the Postfix is a silent no-op. Issue #122 opened (`audit-wiring`).
- **P3 — SiegeDismount manifest classification error.** Manifest "Patch cat" column lists SiegeDismount as `manual` patch, but the feature uses a `MissionBehavior` (`SiegeDismountMissionBehavior` added at `SubModule.cs:493`), not `_harmony.Patch(...)`. Audit doc note only; no issue.

Everything else is clean. The 17 CulturalFeats inline-constructed models (the highest-risk Phase 1 target) are all parameterless or properly inject services via `IoC.Resolve`. All hook interfaces, services, and patch categories balance correctly.

## Master matrix

| Feature | Probe 1 (manual patch) | Probe 2 (inline model deps) | Probe 3 (hook impl count) | Probe 4 (service consumption) | Probe 5 (patch lifecycle) | Issues opened |
|---|---|---|---|---|---|---|
| AdvancedCombat | N/A | N/A | N/A | ✅ (consumed via mission behaviors) | N/A | — |
| Arena | N/A | ✅ (parameterless) | N/A | N/A | N/A | — |
| ArmyTargeting | N/A | N/A (covered in feature IoC) | N/A | N/A | ✅ | — |
| AtmospherePersistence | N/A | N/A | N/A | N/A | ✅ | — |
| BannerColorPersistence | ✅ 3 of 4 manual patches initialized; ❌ MobilePartyVisual_AddCharacterToPartyIcon_Patch not initialized | N/A | N/A | ✅ | ✅ | **#122** |
| BannerInjection | N/A | N/A | N/A | N/A | ✅ | — |
| BattleBalance | N/A | N/A (covered in feature IoC) | N/A | N/A | N/A | — |
| BattleScenes | N/A | N/A | N/A | N/A | ✅ (intentionally disabled; commented `PatchCategory` call) | — |
| CareerSystem | N/A | N/A | N/A | ✅ | ✅ | — |
| CharacterCreation | N/A | N/A | N/A | N/A | ✅ | — |
| CharacterSelection | N/A | N/A | N/A | N/A | ✅ (shared `Late_Transpiler` category) | — |
| CompanionTactics | N/A | N/A | N/A | N/A | ✅ (shared deferred `Patch_MissionTime_SetMovementOrder`) | — |
| CulturalFeats | N/A | ✅ 17 models — 16 parameterless + 1 (TaomSettlementLoyaltyModel) correctly resolves `IRevoltTuningConfigProvider` | N/A | N/A | ✅ | — |
| CustomBattles | N/A | N/A | ✅ 4 hook interfaces — 1 impl each | ✅ | ✅ | — |
| Diplomacy | N/A | ✅ TaomAllianceModel/TaomKingdomDecisionPermissionModel/TaomDiplomacyModel correctly resolve `IDiplomacyService` + `IWarOfTheRingService` | ✅ IOnAllianceAction / IOnPeaceAction | N/A | ✅ | — |
| EditorCacheRebuild | N/A | N/A | N/A | ✅ (consumed via MCM lambda in `TaomSettings.cs`) | N/A | — |
| Encyclopedia | N/A | ✅ TaomInformationRestrictionModel (default ctor with `Func<bool>` settings fallback) | N/A | N/A | N/A | — |
| EquipPresets | N/A | N/A | N/A | N/A | ✅ | — |
| Execution | N/A | N/A | ✅ IOnExecutionAction | N/A | ✅ | — |
| FactionMap | N/A | N/A | ✅ IOnCultureStageViewCreated/Tick/Finalize | ✅ (9 services consumed via hook ctor injection) | ✅ | — |
| FiefManagement | N/A | N/A | N/A | N/A | ✅ | — |
| HeroRace | N/A | N/A | N/A | N/A | ✅ (shared `Late_ActionSetOverride`) | — |
| InitialChildGeneration | N/A | N/A | N/A | N/A | N/A | — |
| LocalizationOverride | N/A | N/A | N/A | N/A | ✅ (early phase, no Mission/Campaign dep) | — |
| MainMenuCustomizer | N/A | N/A | N/A | ✅ (consumed in `OnBeforeInitialModuleScreenSetAsRoot`) | N/A | — |
| Messengers | N/A | N/A | N/A | ✅ (Phase 0 wiring fix #121 landed; CampaignBehavior now registered) | N/A | — (closed #121) |
| MixedFormations | N/A | N/A | N/A | N/A | ⚠ Patch30 reads `Mission.Current?.Scene` from `OnSubModuleLoad` lifecycle but null-coalesces — graceful no-op, not a crash; cosmetic-only — could defer to `OnMissionBehaviorInitialize` for clarity | — |
| NamedCompanions | N/A | N/A | N/A | N/A | N/A | — |
| QuickActions | N/A | N/A | N/A | N/A | ✅ | — |
| RaceAge | N/A | N/A | N/A | N/A | N/A | — |
| RevoltTuning | N/A | (consumed by CulturalFeats `TaomSettlementLoyaltyModel`) | N/A | ✅ (consumed via cross-feature) | N/A | — |
| SettlementGuards | ✅ 2 manual patches: TakeGuardAgentDataFromGarrisonTroopList (Prefix), GetSuitableSpear (Prefix) | N/A | N/A | N/A | N/A | — |
| ShaderPrecompilation | N/A | N/A | N/A | ✅ | ✅ | — |
| Siege | N/A | N/A | N/A | N/A | ✅ | — |
| SiegeDismount | ❌ Manifest misclassifies as `manual` — actually a MissionBehavior added at `SubModule.cs:493`, no `_harmony.Patch(...)` exists | N/A | N/A | N/A | N/A | — (P3 doc note) |
| SmartCavalryAI | N/A | N/A | N/A | N/A | ✅ (shared deferred `Patch_MissionTime_SetMovementOrder`) | — |
| SpecialResources | N/A | N/A | ✅ IOnPartyUpgradeResourceCheck | N/A | ✅ | — |
| Spider | N/A | N/A | N/A | ✅ (consumed via BT-leaf nodes + mission behavior) | N/A | — |
| StartupResources | N/A | N/A | N/A | N/A | N/A | — |
| TimeAcceleration | N/A | N/A | N/A | ✅ (consumed in `OnApplicationTick`) | N/A | — |
| TroopProgression | N/A | N/A (covered in feature IoC) | N/A | N/A | N/A | — |
| TroopWeight | N/A | N/A | ✅ 4 hook interfaces — 1 impl each | ✅ | ✅ | — |
| Warg | N/A | N/A | N/A | ✅ (consumed via BT-leaf node) | N/A | — |
| WeatherBoundsGuard | N/A | N/A | N/A | N/A | ✅ | — |

Legend: ✅ pass, ❌ fail, ⚠ note, N/A not applicable (probe doesn't cover this archetype for this feature).

## Findings — Probe 1 (manual patches)

7 manual `_harmony.Patch(...)` call sites enumerated across SettlementGuards (2), BannerColorPersistence (4 — `MobilePartyVisual_AddCharacterToPartyIcon_Patch`, `AgentVisuals_Create_Patch`, `MapConversationTableau_SpawnOpponentLeader_Patch`, `MapConversationTableau_SpawnOpponentBodyguard_Patch`), plus an EquipPresets-adjacent `captainTooltipTarget` site at `SubModule.cs:417`.

| Feature | Call site (file:line) | Target method | Patch class file | Status |
|---|---|---|---|---|
| SettlementGuards | SubModule.cs:430 | `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` | `GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs` | ✅ |
| SettlementGuards | SubModule.cs:438 | `GuardsCampaignBehavior.GetSuitableSpear` | `GuardsCampaignBehavior_GetSuitableSpear_Patch.cs` | ✅ |
| BannerColorPersistence | SubModule.cs:447 | `MobilePartyVisual.AddCharacterToPartyIcon` | `MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs` | ❌ **Wired but never initialized — `_service`/`_heroAdapter` remain null → silent no-op** |
| BannerColorPersistence | SubModule.cs:456 | `AgentVisuals.Create` | `AgentVisuals_Create_Patch.cs` | ✅ Initialized at SubModule.cs:177 |
| BannerColorPersistence | SubModule.cs:463 | `MapConversationTableau.SpawnOpponentLeader` | `MapConversationTableau_SpawnOpponentLeader_Patch.cs` | ✅ Initialized at SubModule.cs:178 |
| BannerColorPersistence | SubModule.cs:469 | `MapConversationTableau.SpawnOpponentBodyguard` | `MapConversationTableau_SpawnOpponentBodyguard_Patch.cs` | ✅ Initialized at SubModule.cs:179 |

**P2 finding — BannerColorPersistence MobilePartyVisual patch silently inert:**

`Main/Features/BannerColorPersistence/Hooks/MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs` declares an `Initialize(IBannerColorService, IBannerHeroAdapter)` method that no caller invokes. SubModule.cs lines 161-180 initialize 19 other BannerColorPersistence patches but skip this one. The Postfix's `_heroAdapter?.GetClanColorInfo(...)` and `_service?.ShouldUseClanColor(...)` null-coalesce on the null statics and return without effect. **User-visible impact:** world-map party icons (e.g., when a player's party renders next to garrison/army icons) fall back to vanilla clan colors instead of the persisted clan colors. Severity P2 — feature degraded, not crashed. Issue #122 opened with `audit-wiring` label.

**SiegeDismount manifest misclassification:**

The manifest "Patch cat" column says `manual`, but `SiegeDismount` has no `_harmony.Patch(...)` call anywhere in SubModule.cs. The feature uses `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` at SubModule.cs:493 instead. The actual feature directory contains only `Hooks/SiegeDismountMissionBehavior.cs` — no patch class files. The manifest's archetype/Patch-cat columns should be corrected in a future doc-fix commit (Phase N+2, not Phase 1's scope to fix). P3 — doc note only.

## Findings — Probe 2 (inline-constructed GameModel deps)

22 inline-constructed models verified across CulturalFeats (17), Arena (1), Encyclopedia (1), Diplomacy (3). **All ✅.**

- **CulturalFeats** — 16 of 17 models are parameterless; only `TaomSettlementLoyaltyModel` takes an `IRevoltTuningConfigProvider`, correctly resolved at `SubModule.cs:288` via `IoC.Resolve<IRevoltTuningConfigProvider>()`. This is exactly the cross-feature path the manifest flagged (RevoltTuning feeds CulturalFeats) — it works as designed.
- **Arena** — `TaomTournamentModel` (parameterless, constructed inline at `SubModule.cs:284`).
- **Encyclopedia** — `TaomInformationRestrictionModel` takes a `Func<bool>` settings predicate, default-constructed with a `TaomSettings.Instance` lambda at `SubModule.cs:301`.
- **Diplomacy (inline)** — `TaomAllianceModel`, `TaomKingdomDecisionPermissionModel`, `TaomDiplomacyModel` all resolve their `IDiplomacyService` / `IWarOfTheRingService` dependencies correctly at SubModule.cs:260-262.

No Messengers-class miss. **No P1/P2 findings on this probe.**

## Findings — Probe 3 (hook interface consumer/producer balance)

12 unique hook interfaces resolved in `SubModule.cs` lifecycle methods, all balanced.

Every `IOnXxx` (or `ISideCommanderFilter`) interface resolved at `SubModule.cs` lines 124-145, 272, and 399 has exactly **1 implementation** registered in its feature IoC class (DiplomacyIoC, ExecutionIoC, TroopWeightIoC, CustomBattlesIoC, SpecialResourcesIoC). No interface is resolved with zero implementations; no implementation exists unregistered.

Lifetime check: CustomBattles hook impls use `Reuse.Transient` (CustomBattlesIoC.cs:12-14); all others use `Reuse.Singleton`. Transient is safe here because the hooks are resolved once at `OnSubModuleLoad` and passed by reference to patch `Initialize` calls — they're not re-instantiated per Harmony call.

**No P1/P2 findings on this probe.**

## Findings — Probe 4 (service consumption)

32+ services across 11 feature categories audited; **all ✅**.

The three manifest-flagged risk shapes (AdvancedCombat, Spider, Warg) all have non-test consumers:

- **AdvancedCombat** — `IBoneCollisionService` consumed via constructor injection in `AdvancedCombatBehavior` (mission-level) and `SpiderMissionBehavior`. `ISpatialGridDebugService` consumed in `AdvancedCombatBehavior`.
- **Spider** — `ISpiderSpawnerService` consumed by `SpiderMissionBehavior::ctor`. `ISpiderAttackService` consumed at BT-leaf boundary (`SpiderAttackTask:25` via `IoC.Resolve`, which is correct because behavior-tree leaf nodes are engine-constructed — this is the documented exception to "no service locator in services").
- **Warg** — `IWargAttackService` consumed at BT-leaf boundary (`WargAttackTask:26`), same pattern as Spider.

All other registered services across CustomBattles, EditorCacheRebuild, FactionMap, MainMenuCustomizer, ShaderPrecompilation, TimeAcceleration, TroopWeight have at least one production consumer (patch init, hook ctor injection, MCM action lambda, or BT-leaf boundary).

**No P1/P2 findings on this probe.**

## Findings — Probe 5 (patch category orphans + lifecycle)

36 patch categories enumerated across `Main/Features/**/Hooks/*.cs` (`[HarmonyPatchCategory("...")]` attributes) vs SubModule.cs (`_harmony.PatchCategory(...)` calls). **Perfect bidirectional match.**

- 35 active categories applied via `PatchCategory(...)` calls.
- 1 disabled category (`Patch0_BattleScenes`) — commented-out `PatchCategory` call at SubModule.cs:117; consistent with the manifest's "(disabled)" archetype for BattleScenes.
- **0 orphaned patch classes** (declared but never applied).
- **0 dead `PatchCategory` calls** (applied but no matching attribute).

Lifecycle correctness across the 6 early-phase categories (`OnSubModuleLoad`):

| Category | Target type | Lifecycle risk | Status |
|---|---|---|---|
| Patch25_LocalizationOverride | `MBTextManager` | None (stateless dictionary lookup) | ✅ |
| Patch18_CulturalFeats | `Campaign` (Postfix on `InitializeDefaultCampaignObjects`) | None (Campaign.Current is guaranteed non-null at that postfix point) | ✅ |
| Patch19_CustomBattles | `BannerlordMissions` etc. | None (Mission is the postfix `__result`, not read from `Mission.Current`) | ✅ |
| Patch21_ShaderPrecompilation | `LoadingWindowViewModel` | None (UI VM, no engine state reads at JIT prep) | ✅ |
| Patch22_ArmyTargeting | `AiMilitaryBehavior` | None (Settlement/MobileParty explicit params) | ✅ |
| Patch30_MixedFormations | `Formation` | ⚠ Patch reads `Mission.Current?.Scene` (line 35) and `unit?.Mission ?? Mission.Current` (line 49). Null-coalesces correctly — no crash — but the lifecycle assumption is fuzzy. Could be deferred to `OnMissionBehaviorInitialize` (like `Patch_MissionTime_SetMovementOrder`) for clarity. Cosmetic-only. | ⚠ P3 |

**No P1/P2 findings on this probe.** The Patch30 lifecycle note is P3 / cosmetic — no behavior bug, just a design tightening opportunity for a future phase.

## GitHub issues opened

| # | Title | Probe | Severity |
|---|---|---|---|
| 122 | BannerColorPersistence: MobilePartyVisual_AddCharacterToPartyIcon_Patch wired but never initialized → world-map party icons fall back to vanilla colors | 1 | P2 |

(Plus issue #121 from Phase 0 — Messengers wiring; fixed and closed before Phase 1's probes ran.)

## Cross-cuts / observations for later phases

- **CulturalFeats 17-model wave was clean.** The manifest's #1 concern (item #2 in "Open questions / Phase 1 targets") was Phase 0's leading hypothesis. Phase 1 disproves it: 16 of 17 are parameterless, the 1 that takes a service resolves it correctly. The Messengers-class risk shape is not repeated in CulturalFeats. Worth recording so Phase 2 (GameModel cluster review) can deprioritize re-verifying ctor wiring and focus on override correctness instead.
- **Patch30_MixedFormations lifecycle smell.** Patch30 reads `Mission.Current` from `OnSubModuleLoad` via `?.`. It works today, but if a future change makes the patch eager (e.g., adds a non-null Mission requirement to a helper it calls), the smell becomes a bug. Phase 4 (Harmony patch cluster review) should re-examine — possibly defer to `OnMissionBehaviorInitialize` like the SetMovementOrder shared category.
- **SiegeDismount classification correction.** Manifest column "Patch cat" should be `MissionBehavior` (or simply blank with archetype changed to `Behavior`) — not `manual`. Track this for the Phase N+2 docs pass.
- **No silent inertness beyond the BannerColorPersistence/MobilePartyVisual finding.** Every other patch that resolves services or hooks does so via a verified `Initialize(...)` call or constructor injection. The audit shape (`*_Patch.Initialize` called from SubModule.cs) is reliable.

## Phase 1 complete

- All 5 probes ran across all 43 features.
- 1 P2 issue opened (#122).
- 1 P3 manifest discrepancy noted (SiegeDismount classification).
- 1 P3 cosmetic lifecycle smell noted (Patch30_MixedFormations) — re-examined in Phase 4.
- 0 P1 findings.

Phase 1's hypothesis was "any other feature in the Messengers-class state". Result: **one degraded feature (BannerColorPersistence party-icon coloring), zero unfunctional features.** The audit branch's existence is vindicated — the bug is real and silent — but the project-wide wiring discipline is much better than Phase 0's worst-case framing.
