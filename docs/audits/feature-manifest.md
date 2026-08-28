# Feature Manifest — Phase 0

Last updated: 2026-05-13

## Purpose

Single source of truth for "what features exist in TAOM and what wiring touchpoints do they have." Feeds Phase 1 (wiring matrix) by sketching expected vs. observed registration. Built from a parallel sweep of `Main/Features/`, `Main/IoC.cs`, `Main/SubModule.cs`, `Main/Features/TaomSettings.cs`, `Main/_Module/SubModule.xml`, and `Main/_Module/GUI/SpriteParts/Config.xml`.

## Audit dimensions (column legend)

| Column | Meaning |
|---|---|
| **Archetype** | Primary wiring shape — `Behavior` (has `*CampaignBehavior.cs`), `Model` (has `Taom*Model.cs`), `Patch` (only `[HarmonyPatch]`), `Service` (IoC + services only, no patches/models), `UI` (has `[ViewModelMixin]` or `[PrefabExtension]`), `Mixed`. Multiple labels = combined archetype. |
| **IoC file** | `*IoC.cs` exists under the feature directory. ✅ = file present, ❌ = absent (may be intentional for pure Harmony features). |
| **IoC reg** | Feature's IoC class is invoked from [Main/IoC.cs](../../Main/IoC.cs) `Configure()`. ✅ / ❌. |
| **Patch cat** | Harmony category string passed to `_harmony.PatchCategory("...")` in `SubModule.cs`. Blank if no Harmony patches; "manual" if patched outside the category system. |
| **Behavior** | Count of `campaignStarter.AddBehavior(...)` registrations in `SubModule.cs` belonging to this feature. |
| **Model** | Count of `campaignStarter.AddModel(...)` registrations belonging to this feature. |
| **Cfg dir** | `Main/_Module/ModuleData/<X>/` directory (or `configs/<X>_config.json`) for this feature's data. Blank if no config. |
| **MCM** | `TaomSettings.cs` section name covering this feature (verbatim). Blank if no MCM exposure. |
| **Doc** | `docs/features/<X>.md` exists. ✅ / ❌. |
| **Test** | `TAOM.Tests/Features/<X>/` directory exists. ✅ / ❌. |
| **Notes** | Anomalies / Phase 1 follow-ups. |

## Master matrix (43 features)

| Feature | Archetype | IoC file | IoC reg | Patch cat | Behavior | Model | Cfg dir | MCM | Doc | Test | Notes |
|---|---|:--:|:--:|---|:--:|:--:|---|---|:--:|:--:|---|
| AdvancedCombat | Service | ✅ | ✅ | — | 0 | 0 | — | — | ✅ | ✅ | Pure-service registration; SpatialGrid/BoneCollision/CustomAttacks. Mission-level usage path needs Phase 1 trace. |
| Arena | Model | ❌ | ❌ | — | 0 | 1 (`TaomTournamentModel`) | — | — | ✅ | ✅ | No IoC class; `TaomTournamentModel` constructed inline. Phase 1: confirm it has no deps that need IoC. |
| ArmyTargeting | Patch+Model | ✅ | ✅ | `Patch22_ArmyTargeting` | 0 | 1 (`TaomTargetScoreModel`) | `configs/army_targeting.json` | AI Strategic Intelligence | ✅ | ✅ | OK |
| AtmospherePersistence | Patch | ❌ | ❌ | `Patch16_AtmospherePersistence` | 0 | 0 | — | — | ✅ | ✅ | Pure Harmony; intentional. |
| BannerColorPersistence | Patch+Service | ✅ | ✅ | `Patch23_BannerColorPersistence`, `Patch24_BannerDriftGuard`, `Patch15_BannerLayerLimit` | 0 | 0 | `configs/banner_color_config.json` | — | ✅ | ✅ | 3 patch categories; service plugged into patches via IoC.Resolve in `OnSubModuleLoad`. |
| BannerInjection | Behavior | ✅ | ✅ | `Patch6_BannerEditor` | 1 | 0 | — | — | ✅ | ✅ | OK |
| BattleBalance | Model | ✅ | ✅ | — | 0 | 3 (`TaomMilitaryPowerModel`, `TaomCombatSimulationModel`, `TaomPartyHealingModel`) | `configs/battle_balance_config.json` | Battle Balance/Troop Power, Battle Balance/Casualty Ratios | ✅ | ✅ | OK |
| BattleScenes | Patch (disabled) | ❌ | ❌ | `Patch0_BattleScenes` | 0 | 0 | — | — | ✅ | ❌ | CLAUDE.md marks DISABLED. Test dir absence is consistent. Phase 1: verify patch is genuinely no-op or removed. |
| CareerSystem | Mixed (Behavior+Model+Patch+UI) | ✅ | ✅ | `Patch27_CareerSystem` | 3 (`CareerPersistenceBehavior`, `CareerCampaignBehavior`, `CareerSwitchDialogueBehavior`) | 5 (`TaomMapVisibilityModel`, `TaomInventoryCapacityModel`, `TaomAgentStatCalculateModel`, `TaomAgentApplyDamageModel`, `TaomClanTierModel`) | `career_system/`, `charactercreation/career_menu.json` | — | ✅ | ✅ | Heaviest feature. Sprite atlas: `ui_taom_career_system`. `CareerPerkMissionBehavior` mission-level wiring at SubModule line 502+. |
| CharacterCreation | Mixed (Behavior+Model+Patch) | ✅ | ✅ | `Patch9_RaceFilter`, `Patch29_CCBodyProperties`, `Patch20_NarrativeHorseGuard` | 1 (`CharacterCreationRegistrationBehavior`) | 1 (`TaomCharacterStatsModel`) | `charactercreation/` | — | ✅ | ✅ | OK |
| CharacterSelection | Patch | ❌ | ❌ | `Late_Transpiler` | 0 | 0 | — | — | ✅ | ❌ | No tests. `Late_Transpiler` is a shared category. Phase 1: confirm what this feature does and whether tests should exist. |
| CompanionTactics | Behavior+Patch | ✅ | ✅ | `Patch35_CompanionTactics`, `Patch_MissionTime_SetMovementOrder` | 1 (`FormationPresetCampaignBehavior`) | 0 | — | Battle Tactics/Companion Roles, Battle Tactics/Formation Presets, Battle Tactics/Battle Action Bar | ✅ | ✅ | Two MCM groups + one nested under it (Battle Action Bar). |
| CulturalFeats | Model | ❌ | ❌ | `Patch18_CulturalFeats` | 0 | 17 (Army/Speed/Prosperity/Militia/Construction/Production/Caravan/BattleReward/Tournament/TroopUpgrade/PartySize/FoodConsumption/SettlementLoyalty/PartyMorale/Smithing/ClanFinance/Raid) | — | — | ✅ | ✅ | No IoC class — 17 models constructed inline in SubModule. Phase 1: verify all 17 models accept inline construction (no IoC service deps). |
| CustomBattles | Patch+Service | ✅ | ✅ | `Patch19_CustomBattles` | 0 | 0 | — | — | ✅ | ✅ | 4 IoC hook services resolved in `OnSubModuleLoad` (lines 141-145). |
| Diplomacy | Behavior+Model+Patch | ✅ | ✅ | `Patch11_Diplomacy`, `Patch12_WarOfTheRing` | 2 (`DiplomacyBehavior`, `WarOfTheRingBehavior`) | 3 (`TaomAllianceModel`, `TaomKingdomDecisionPermissionModel`, `TaomDiplomacyModel`) | `diplomacy/` | War of the Ring | ✅ | ✅ | OK |
| EditorCacheRebuild | Service | ✅ | ✅ | — | 0 | 0 | `configs/cache_rebuild_config.json` | Map Tools/Distance Cache Rebuild | ✅ | ✅ | OK (singleplayer-only per recent commits) |
| Encyclopedia | Model | ❌ | ❌ | — | 0 | 1 (`TaomInformationRestrictionModel`) | — | Encyclopedia | ✅ | ✅ | No IoC; model constructed inline at SubModule line 301. |
| EquipPresets | Behavior+Patch | ✅ | ✅ | `Patch33_EquipPresets` | 1 (`EquipmentPresetCampaignBehavior`) | 0 | `equipmentsets/` (data only, not feature config) | Inventory/Equipment Presets | ✅ | ✅ | Adapter pattern in use. |
| Execution | Patch+Model+Service | ✅ | ✅ | `Patch14_Execution` | 0 | 1 (`TaomExecutionRelationModel`) | `execution/` | — | ❌ | ✅ | **Doc gap** — `detect-docs-gaps.sh` already flags this. Need `docs/features/execution.md`. |
| FactionMap | Patch+Service | ✅ | ✅ | `Patch7_FactionMap` | 0 | 0 | `factionmap/` | — | ✅ | ✅ | OK |
| FiefManagement | Behavior+Patch | ✅ | ✅ | `Patch36_FiefManagement` | 1 (`FiefHubCampaignBehavior`) | 0 | — | Fief Management | ✅ | ✅ | Adapter pattern in use. |
| HeroRace | Behavior+Patch+Service | ✅ | ✅ | `Patch1_FirstTimeInit`, `Patch2_RefreshTableau`, `Patch3_SetRace`, `Patch4_CharacterSpawner`, `Patch5_FaceGen`, `Late_ActionSetOverride` | 1 (`RacePersistenceBehavior`) | 0 | — | — | ✅ | ✅ | 6 patch categories — broadest patch surface in the project. |
| InitialChildGeneration | Behavior | ✅ | ✅ | — | 1 (`TaomInitialChildGenerationBehavior`) | 0 | `configs/initial_child_generation.json` | — | ✅ | ✅ | OK |
| LocalizationOverride | Patch | ❌ | ❌ | `Patch25_LocalizationOverride` | 0 | 0 | — | — | ✅ | ✅ | No IoC; XML loaded inline at SubModule line 99-112 via manual parse → `MBTextManager_GetLocalizedText_Patch.RegisterOverride`. Phase 1: confirm this XML lookup is gated / cached. |
| MainMenuCustomizer | Service | ✅ | ✅ | — | 0 | 0 | — | — | ✅ | ✅ | `OnBeforeInitialModuleScreenSetAsRoot` lifecycle (SubModule line 190). Adapter pattern. |
| Messengers | Behavior+UI | ✅ | ✅ (just fixed) | — | 1 (`MessengerCampaignBehavior`, just wired) | 0 | `messengers/` | Messengers | ✅ | ✅ | **The original crash.** Fixed in this branch. |
| MixedFormations | Patch | ✅ | ✅ | `Patch30_MixedFormations` | 0 | 0 | — | Battle Tactics/Mixed Formations | ✅ | ✅ | Cross-feature interaction with SmartCavalryAI (per memory feedback). Phase 2 cluster review. |
| NamedCompanions | Behavior | ✅ | ✅ | — | 1 (`NamedCompanionBehavior`) | 0 | `named_companions/` | — | ✅ | ✅ | OK |
| QuickActions | Behavior+Patch+UI | ✅ | ✅ | `Patch34_QuickActions` | 1 (`InventorySearchCampaignBehavior`) | 0 | — | Inventory/Quick Actions | ✅ | ✅ | OK |
| RaceAge | Behavior+Model | ✅ | ✅ | — | 1 (`RaceAgeBehavior`) | 3 (`TaomAgeModel`, `TaomPregnancyModel`, `TaomHeroCreationModel`) | `raceage/` | — | ✅ | ✅ | OK |
| RevoltTuning | Service (config feeding `TaomSettlementLoyaltyModel`) | ✅ | ✅ | — | 0 | 0 (consumed by `TaomSettlementLoyaltyModel` in CulturalFeats) | `configs/revolt_tuning_config.json` | — | ✅ | ✅ | Cross-feature: feeds CulturalFeats' `TaomSettlementLoyaltyModel`. Phase 2 cluster review. |
| SettlementGuards | Patch (manual) | ✅ | ✅ | "manual" (no category) | 0 | 0 | `settlement_guards/` | — | ✅ | ✅ | Manual `_harmony.Patch(...)` calls instead of category. Phase 1: confirm patches are applied somewhere. |
| ShaderPrecompilation | Patch+Service | ✅ | ✅ | `Patch21_ShaderPrecompilation` | 0 | 0 | — | — | ✅ | ✅ | `OnBeforeInitialModuleScreenSetAsRoot` lifecycle (line 194). |
| Siege | Behavior+Model+Patch | ✅ | ✅ | `Patch8_SiegeCampGuard` | 1 (`SiegeDefenseBehavior`) | 1 (`TaomSiegeEventModel`) | `siege/` | Siege Defense | ✅ (multiple: siege.md, siege-defense.md, siege-trebuchets.md) | ✅ | OK; IoC class name is `SiegeDefenseIoC` (mismatch with dir name `Siege` — cosmetic). |
| SiegeDismount | Patch (manual) | ✅ | ✅ | "manual" (no category) | 0 | 0 | — | Battle Tactics/Siege Dismount | ✅ | ✅ | Manual `_harmony.Patch(...)`. Phase 1: confirm application. |
| SmartCavalryAI | Patch | ✅ | ✅ | `Patch_MissionTime_SetMovementOrder` | 0 | 0 | — | Battle Tactics/Smart Cavalry | ✅ | ✅ | Shared deferred patch category with CompanionTactics. Phase 2: cross-feature handshake with MixedFormations per memory feedback. |
| SpecialResources | Behavior+Patch+UI | ✅ | ✅ | `Patch26_SpecialResources` | 1 (`SpecialResourcesBehavior`) | 0 | `special_resources/` | — | ✅ | ✅ | Hook service `IOnPartyUpgradeResourceCheck` resolved at line 399. |
| Spider | Service | ✅ | ✅ | — | 0 | 0 | — | — | ✅ | ✅ | `SpiderSpawnerService` referenced; data via `characters/spider_creature` XmlNode. |
| StartupResources | Behavior | ✅ | ✅ | — | 1 (`StartupResourcesBehavior`) | 0 | `startup_resources/` | — | ✅ | ✅ | OK |
| TimeAcceleration | Service+UI | ✅ | ✅ | — | 0 | 0 | — | Time Acceleration | ✅ | ✅ | `OnApplicationTick` consumer (line 92). Adapter pattern. UI mixin/prefab present. |
| TroopProgression | Model | ✅ | ✅ | — | 0 | 2 (`TaomPartyWageModel`, `TaomVolunteerModel`) | `troops/` | — | ✅ | ✅ | Also feeds `TaomCharacterStatsModel` (registered under CharacterCreation in matrix; data span overlaps). |
| TroopWeight | Patch+Service | ✅ | ✅ | `Patch17_TroopWeight` | 0 | 0 | `TroopWeights/` | Troop Weight | ✅ | ✅ | 4 hook services resolved at lines 135-138. |
| Warg | Service | ✅ | ✅ | — | 0 | 0 | `LOTRLOME_Armory` (external) | — | ✅ | ✅ | Monster/animation data lives in LOTRLOME_Armory (absorbed from Alliance.Wargs 2026-08-28). |
| WeatherBoundsGuard | Patch | ❌ | ❌ | `Patch10_WeatherBoundsGuard` | 0 | 0 | — | — | ✅ | ✅ | Pure Harmony; intentional. |

## Cross-cuts

### Features without an `IoC.cs` file (8 — all need Phase 1 verification)

| Feature | Why no IoC? | Action |
|---|---|---|
| Arena | Inline model construction at SubModule line 284 | Verify model has no service deps that would need IoC |
| AtmospherePersistence | Pure Harmony patch | OK as-is |
| BattleScenes | Disabled per CLAUDE.md | Verify patch is actually disabled / removed in source |
| CharacterSelection | Single transpiler patch | Verify what feature does, why no test dir |
| CulturalFeats | 17 GameModels constructed inline | Verify zero of the 17 have service deps; otherwise extract IoC |
| Encyclopedia | Single inline model at line 301 | Verify |
| LocalizationOverride | Manual XML load inline | Verify load gate / caching |
| WeatherBoundsGuard | Pure Harmony patch | OK as-is |

### Features with `is registered but might not be wired beyond IoC`

Phase 1 should verify these aren't another Messengers-class case:

| Feature | Risk shape |
|---|---|
| AdvancedCombat | Service registered, but where is it consumed in patches? |
| Spider | Service registered, but where is `SpiderSpawnerService` invoked? |
| Warg | Service registered, where consumed? |

### Features with manual Harmony patching (no `[HarmonyPatchCategory]`)

These bypass the `_harmony.PatchCategory("...")` mechanism — they call `_harmony.Patch(AccessTools.Method(...))` directly. Phase 1 must verify the manual patch is actually executed:

- **SettlementGuards** — 2 manual patches per CLAUDE.md (GuardsCampaignBehavior private methods)
- **SiegeDismount** — confirm via grep
- **Banner color persistence partial** — Patch23 has 2 manual entries per CLAUDE.md

### Feature → MCM mapping (sanity check)

These features have logic but no MCM toggle:
- BannerInjection, BannerColorPersistence, CharacterCreation, Diplomacy, HeroRace, InitialChildGeneration, LocalizationOverride, MainMenuCustomizer, NamedCompanions, RaceAge, RevoltTuning, SettlementGuards, ShaderPrecompilation (shipped on by default), SpecialResources, StartupResources, TroopProgression, Warg — most are intentionally "always on."

Cross-direction: every MCM section in the audit maps to a feature directory ✅.

### Tests directory missing (2)

- **BattleScenes** — consistent with disabled status.
- **CharacterSelection** — Phase 1 / Phase N+1: confirm whether this needs tests or is genuinely test-irrelevant (transpiler-only?).

### Docs missing (1)

- **Execution** — flagged by `detect-docs-gaps.sh` at session start. Phase N+2 closes this.

## Open questions / Phase 1 targets

1. **Manual-patch features (SettlementGuards, SiegeDismount, Banner partial)** — verify the `_harmony.Patch(...)` calls actually fire. These are the highest-risk "wired but invisible" candidates.
2. **CulturalFeats 17-model wave (SubModule.cs lines 276-292)** — confirm none of the 17 `Taom*Model` classes have constructor dependencies that should be IoC-resolved. The Messengers-class lesson: silent construction with `new Xxx()` masks missing service wiring.
3. **GameModels constructed inline without their feature's IoC** (CulturalFeats, Arena, Encyclopedia, Diplomacy partial) — same risk as #2.
4. **Hook services resolved in `OnSubModuleLoad` / `OnGameInitializationFinished`** (lines 124-138, 141-145, 157-159, 174, 182, 399-400, 423, 502-513) — each `IOnXxx` interface needs at least one implementation registered in IoC. If an interface has zero implementations, the patch is wired but does nothing.
5. **Lifecycle-phase appropriateness** — 6 patches in `OnSubModuleLoad` (early) vs. 28 in `OnGameInitializationFinished` (late). Verify the 6 early ones genuinely need to be early (game state available?). One known-correct early case: `Patch_MissionTime_SetMovementOrder` is deferred to `OnMissionBehaviorInitialize` because `MovementOrder.cctor` reads `Mission.Current.CurrentTime`.

## Phase 0 complete

Phase 0 produced:
- This manifest (43 features classified).
- 8 features flagged for "no IoC.cs" verification.
- 3 features flagged for "registered but consumption path unknown."
- 3 manual-patch features flagged for "patch-applied verification."
- 1 doc gap (Execution) auto-flagged by session hook.
- 2 test-dir gaps (BattleScenes [expected], CharacterSelection [unknown]).

**No fixes performed.** Phase 0 is read-only by design. The Messengers wiring fix from earlier in this branch is the only code change; it predates this audit doc but the doc covers it.

## Phase log

| Date | Phase | Session | Output | Findings count |
|---|---|---|---|---|
| 2026-05-13 | 0 | initial | `feature-manifest.md`, `README.md` | 8 + 3 + 3 + 1 + 2 = 17 items queued for Phase 1+ |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/cluster-campaign-behaviors.md](./cluster-campaign-behaviors.md)
- [docs/audits/cluster-cross-feature.md](./cluster-cross-feature.md)
- [docs/audits/cluster-gamemodels.md](./cluster-gamemodels.md)
- [docs/audits/cluster-harmony-patches.md](./cluster-harmony-patches.md)
- [docs/audits/cluster-ui.md](./cluster-ui.md)
- [docs/audits/phase-1-kickoff.md](./phase-1-kickoff.md)
- [docs/audits/phase-2-kickoff.md](./phase-2-kickoff.md)
- [docs/audits/phase-4-kickoff.md](./phase-4-kickoff.md)
- [docs/audits/phase-5-kickoff.md](./phase-5-kickoff.md)
- [docs/audits/phase-6-kickoff.md](./phase-6-kickoff.md)
- [docs/audits/phase-7-kickoff.md](./phase-7-kickoff.md)
- [docs/audits/phase-8-kickoff.md](./phase-8-kickoff.md)
- [docs/audits/phase-9-kickoff.md](./phase-9-kickoff.md)
- [docs/audits/wiring-matrix.md](./wiring-matrix.md)

<!-- backlinks-end -->
