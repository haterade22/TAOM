# Lane 2: abstraction economy, at `b2e387db`

Auditor: Opus lane 2 (read-only). Scope: playbook "## 5. Tech Debt & Architecture", shallow modules
and the deepening deletion test. ADR-007 adapters are decided; this lane challenges redundant layers
only. All code reads are from a `git archive b2e387db Main TAOM.Tests` extraction in the session
scratchpad, so another session's working-tree edits do not enter any number here.

## Measurement method (shared by ARCH-01 to ARCH-03)

`git archive b2e387db Main TAOM.Tests | tar -x` into the scratchpad, then two Python scripts
(`ifaces.py`, `classify.py`, kept in the scratchpad): comments stripped, `interface I\w+` declarations
collected, implementations found from every `class|struct|record X : <base list>` whose base list
names the interface, test seams found from `Substitute.For<I...>` and test classes implementing it.
Classification of each single-implementation interface:

- **(a) adapter seam:** declared or implemented under `Main/Adapters/`, named `*Adapter`, or its
  implementation both imports a TaleWorlds engine namespace and dereferences an engine static
  (`Hero.`, `MobileParty.`, `Campaign.`, `Mission.`, `Agent.` and similar).
- **(b) test seam:** not (a), and substituted or faked in `TAOM.Tests`.
- **(c) neither.**

Caller counts exclude the feature `*IoC.cs` registration files and `SubModule.cs`.

| Measure | Value |
|---|---|
| Interfaces declared in `Main/` | **495** (263 at the June commit `141b749`; `.cs` files 1,063 to 2,166 over the same span, so the ratio held) |
| Exactly one implementation | **477** (96%) |
| Zero implementations | 3 (`IBTBlackboard`, `IBTMovable`: base interfaces of the BT library; `IEditorSceneAdapter`: dead, see ARCH-02) |
| Two or more | 15 (behavior-tree blackboards, ability executors, crash renderers, the Null-object pairs, Phase1/Phase2 builders) |
| Single-impl (a) adapter seam | 138 (87 declared in `Main/Adapters/`; 100 of the 138 are also substituted in tests) |
| Single-impl (b) test seam only | 185 |
| Single-impl (c) neither | **154** (132 of them in their own file, 2,764 lines) |
| (c) by non-registration caller files | 0: 3; 1: 79; 2: 34; 3: 18; 4 or more: 20 |
| (c) mentioned anywhere in `TAOM.Tests` | 24 of 154, and those mentions are container wiring tests (`*WiringTests.cs`, `*BindingTests.cs`) that resolve the interface, not fakes |

### [ARCH-01] Settle the interface-per-service rule: the review lens demands what the coding rule forbids

- **Evidence**: `.claude/skills/deep-review/lenses/1-standards.md:13` (at `b2e387db`): "**Interface Segregation:** Every service has an interface. Every adapter has an interface." This is a checklist item every `/deep-review` Standards lens enforces.
- **Evidence**: `.claude/rules/think-before-coding.md:33`: "Only then write the minimum: no single-implementation interface, no plumbing "for later"". The playbook's shallow-module tell says the same (`audit-playbook.md:74`, "an interface or adapter with a single implementation *and* a single caller").
- **Evidence**: `.claude/agents/feature-builder.md:40,55` scaffolds `I{Name}Service.cs` plus `Register<I{Name}Service, {Name}Service>` for every feature, so every builder spawn produces the lens's shape.
- **Evidence**: the result, measured above: 477 of 495 interfaces have one implementation; **154** are neither an adapter seam nor a test seam. 82 of the 154 have zero or one non-registration caller, for example `Main/Features/SpecialResources/Hooks/IOnRecruitmentResourceGate.cs:10` (one caller, `RecruitmentVM_RecruitGate_Patch.cs`), `Main/Features/CombatMechanics/IChargeKnockdownService.cs:5`, `ICrushThroughService.cs:5`, `IShieldPenetrationService.cs:3`, `ICreatureCombatService.cs:3` (each consumed only by `TaomCombatMechanicsModel.cs`, never faked), and seven `I*ConfigProvider` interfaces whose only consumer is the same feature's `*SettingsProvider` (`Main/Features/AlignmentDesertion/IAlignmentDesertionConfigProvider.cs:3` read by `AlignmentDesertionSettingsProvider.cs:14`; likewise AlignmentRecruitment, CaravanTrade, CastleRecruitment, CultureConversion, MarriageAlignment, NavalTravel). Full table below.
- **Deepening deletion test**: deleting a (c) interface keeps its one class intact and changes each caller's declared type, so complexity is neither concentrated nor scattered; the interface hides nothing (it is the class's public surface restated). Every (c) row passes the test as a deletion candidate. The (b) rows (185) fail it: the interface is the seam a `Substitute.For` needs. The (a) rows are ADR-007 and out of scope.
- **Impact**: each (c) interface is one more file per service (132 dedicated files, 2,764 lines), one more hop in "go to definition" when reading a patch, and one more name to keep in sync on a signature change. More important, the two rules disagree, so the same code gets flagged by one reviewer and demanded by the other; `/deep-review` wins in practice because it runs on every C# commit, which is why the count grew with the codebase (263 interfaces at `141b749`, 495 now, with `.cs` files doubling over the same span).
- **Effort**: S for the policy (one lens line, one feature-builder template, one line in `think-before-coding.md` saying which wins and naming the seams that justify an interface: an adapter, a test fake, or a second implementation). M to L for any code migration, which should not be done in bulk.
- **Risk**: LOW for the policy change. A bulk deletion would be MED: it touches every feature IoC file and `Main/IoC.cs`/`SubModule.cs` (single-owner, contended; see triage-B L404/L412), and would collide with parallel sessions.
- **Confidence**: HIGH on the contradiction and the counts (script above, re-runnable). MED on the exact (a)/(c) boundary: the engine-touch heuristic may place a few services in (a) that are really (c).
- **Fix sketch**: amend lens line 13 to "Every adapter has an interface; a service gets one only when a test fakes it or a second implementation exists", mirror it in `feature-builder.md`, and let the deletions happen opportunistically when a file is next touched. Do not open a mass-deletion PR.
- **Delta**: pre-existing (lens line predates June), amplified since (232 more interfaces).
- **P1**: no.
- **Plan candidate**: yes, the policy half only (S, one clean doc change with a checkable outcome); the code half is a standing guideline, not a plan.

<details><summary>The (c) set: 154 single-implementation interfaces that are neither adapter nor test seams, with non-registration caller counts</summary>

| Callers | Interface (file:line) | Implementation | Caller files |
|---|---|---|---|
| 0 | `Main/Features/CompanionTactics/FormationPresets/IHeroAutoAssigner.cs:13` | `HeroAutoAssigner` | (registration only) |
| 0 | `Main/Features/EditorCacheRebuild/Caching/IPersistentPathCache.cs:3` | `PersistentPathCache` | (registration only) |
| 0 | `Main/Features/MainMenuCustomizer/IMainMenuCustomizerService.cs:3` | `MainMenuCustomizerService` | (registration only) |
| 1 | `Main/Core/Domain/IPartySpottingContributor.cs:13` | `LookoutSpottingContributor` | TaomMapVisibilityModel |
| 1 | `Main/Features/AlignmentDesertion/IAlignmentDesertionConfigProvider.cs:3` | `AlignmentDesertionConfigProvider` | AlignmentDesertionSettingsProvider |
| 1 | `Main/Features/AlignmentDesertion/IAlignmentDesertionService.cs:11` | `AlignmentDesertionService` | AlignmentDesertionBehavior |
| 1 | `Main/Features/AlignmentRecruitment/IRecruitmentAlignmentConfigProvider.cs:3` | `RecruitmentAlignmentConfigProvider` | RecruitmentAlignmentSettingsProvider |
| 1 | `Main/Features/AlignmentRecruitment/IRecruitmentAlignmentService.cs:3` | `RecruitmentAlignmentService` | TaomVolunteerModel |
| 1 | `Main/Features/Arena/ITournamentRosterGuardService.cs:39` | `TournamentRosterGuardService` | Patch69_TournamentRosterGuard |
| 1 | `Main/Features/ArmyTargeting/Diagnostics/ISiegeGatheringDiagnosticsService.cs:8` | `SiegeGatheringDiagnosticsService` | Army_FindBestGatheringSettlementAndMoveTheLeader_Patch |
| 1 | `Main/Features/BanditManagement/IHideoutDescriptionService.cs:17` | `HideoutDescriptionService` | Patch40_HideoutDescription |
| 1 | `Main/Features/BannerInjection/Hooks/IOnBannerEditorDone.cs:3` | `BannerEditorDoneHook` | GauntletBannerEditorScreen_OnDone_Patch |
| 1 | `Main/Features/BannerInjection/IBannerInjectionService.cs:5` | `BannerInjectionService` | BannerInjectionBehavior |
| 1 | `Main/Features/BattleLoadDiagnostics/IEngineMemoryStatsReader.cs:21` | `EngineMemoryStatsReader` | MemoryProbeCheats |
| 1 | `Main/Features/CaravanTrade/ICaravanTradeConfigProvider.cs:4` | `CaravanTradeConfigProvider` | CaravanTradeSettingsProvider |
| 1 | `Main/Features/CareerSystem/ICareerLifecycleService.cs:9` | `CareerLifecycleService` | CareerCampaignBehavior |
| 1 | `Main/Features/CareerSystem/ICareerSwitchService.cs:5` | `CareerSwitchService` | GauntletCareerScreen |
| 1 | `Main/Features/CareerSystem/Mutations/IMutationService.cs:6` | `MutationService` | AbilityEffectExecutor |
| 1 | `Main/Features/CastleRecruitment/ICastleRecruitmentConfigProvider.cs:3` | `CastleRecruitmentConfigProvider` | CastleRecruitmentSettingsProvider |
| 1 | `Main/Features/CharacterCreation/INarrativeHorseGuardService.cs:9` | `NarrativeHorseGuardService` | CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch |
| 1 | `Main/Features/CombatMechanics/IChargeKnockdownService.cs:5` | `ChargeKnockdownService` | TaomCombatMechanicsModel |
| 1 | `Main/Features/CombatMechanics/ICreatureCombatService.cs:3` | `CreatureCombatService` | TaomCombatMechanicsModel |
| 1 | `Main/Features/CombatMechanics/ICrushThroughService.cs:5` | `CrushThroughService` | TaomCombatMechanicsModel |
| 1 | `Main/Features/CombatMechanics/IShieldPenetrationService.cs:3` | `ShieldPenetrationService` | TaomCombatMechanicsModel |
| 1 | `Main/Features/CoopInterop/ISaveDefinerCollisionDetector.cs:9` | `SaveDefinerCollisionDetector` | SaveDefinerCollisionGuard |
| 1 | `Main/Features/CultureConversion/ICultureConversionConfigProvider.cs:3` | `CultureConversionConfigProvider` | CultureConversionSettingsProvider |
| 1 | `Main/Features/CultureMarketplace/ICultureMarketplaceInjectionService.cs:5` | `CultureMarketplaceInjectionService` | CultureMarketplaceBehavior |
| 1 | `Main/Features/CultureMarketplace/ICultureMarketplaceMaintenanceService.cs:21` | `CultureMarketplaceMaintenanceService` | CultureMarketplaceBehavior |
| 1 | `Main/Features/CustomBattles/Hooks/IOnGetCustomBattleCommanders.cs:6` | `CustomBattleCommandersHook` | CustomBattleData_Characters_Patch |
| 1 | `Main/Features/CustomBattles/Hooks/IOnGetCustomBattleFactions.cs:6` | `CustomBattleFactionsHook` | CustomBattleData_Factions_Patch |
| 1 | `Main/Features/CustomBattles/Hooks/IOnGetDefaultTroopOfFormation.cs:5` | `CustomBattleTroopHook` | CustomBattleHelper_Troop_Patch |
| 1 | `Main/Features/Diplomacy/Hooks/IOnPeaceAction.cs:3` | `PeaceActionHook` | MakePeaceAction_ApplyInternal_Patch |
| 1 | `Main/Features/Diplomacy/IKingdomVoteDeadlockService.cs:14` | `KingdomVoteDeadlockService` | KingdomVoteDeadlockBinding |
| 1 | `Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs:6` | `EnlistmentBattlePayoutService` | EnlistmentContentBehavior |
| 1 | `Main/Features/Enlistment/Content/IAssignmentService.cs:19` | `AssignmentService` | EnlistmentAssignmentDialogBehavior |
| 1 | `Main/Features/Enlistment/Content/IDischargeConsequenceService.cs:11` | `DischargeConsequenceService` | EnlistmentContentBehavior |
| 1 | `Main/Features/Enlistment/Content/IEnlistmentDailyService.cs:26` | `EnlistmentDailyService` | EnlistmentContentBehavior |
| 1 | `Main/Features/Enlistment/Content/IEnlistmentWagePreview.cs:22` | `ServiceRewardService` | TaomClanFinanceModel |
| 1 | `Main/Features/Enlistment/Equipment/IEnlistmentEquipmentService.cs:16` | `EnlistmentEquipmentService` | EnlistmentQuartermasterBehavior |
| 1 | `Main/Features/Enlistment/IEnlistmentDeploymentService.cs:7` | `EnlistmentDeploymentService` | TaomBattleInitializationModel |
| 1 | `Main/Features/Enlistment/IEnlistmentLoadNormalizer.cs:9` | `EnlistmentLoadNormalizer` | EnlistmentBehavior |
| 1 | `Main/Features/Enlistment/Presentation/EnlistmentWaitMenuOptions.cs:12` | `EnlistmentWaitMenuOptions` | EnlistmentMenuBehavior |
| 1 | `Main/Features/FactionMap/Hooks/IOnCultureStageViewFinalize.cs:3` | `CultureStageViewFinalizeHook` | CultureStageView_Finalize_Patch |
| 1 | `Main/Features/FactionMap/Hooks/IOnCultureStageViewTick.cs:3` | `CultureStageViewTickHook` | CultureStageView_Tick_Patch |
| 1 | `Main/Features/FactionMap/ICultureStageProgressionService.cs:3` | `CultureStageProgressionService` | CultureStageViewCreatedHook |
| 1 | `Main/Features/FactionMap/IFactionConfigProvider.cs:6` | `FactionConfigProvider` | CultureStageViewCreatedHook |
| 1 | `Main/Features/HeroRace/Hooks/IOnFaceGenGetBaseMonsterFromRace.cs:5` | `EyeHeightAdjustmentHook` | FaceGen_GetBaseMonsterFromRace_Patch |
| 1 | `Main/Features/HeroRace/IBasicTableauRaceGuard.cs:11` | `BasicTableauRaceGuard` | BasicCharacterTableau_RefreshCharacterTableau_Patch |
| 1 | `Main/Features/HeroRace/ITableauPositionService.cs:25` | `TableauPositionService` | CharacterTableau_RefreshCharacterTableau_PositionPatch |
| 1 | `Main/Features/LordSpawnGuard/ILordSpawnGuardService.cs:8` | `LordSpawnGuardService` | Patch65_LandlessCultureSpawnGuard |
| 1 | `Main/Features/MarriageAlignment/IMarriageAlignmentConfigProvider.cs:3` | `MarriageAlignmentConfigProvider` | MarriageAlignmentSettingsProvider |
| 1 | `Main/Features/MenuLinkColors/IMenuLinkStyleRewriter.cs:8` | `MenuLinkStyleRewriter` | Patch64_MenuLinkColors |
| 1 | `Main/Features/MixedFormations/ILayoutPositioner.cs:6` | `LayoutPositioner` | FormationLayoutService |
| 1 | `Main/Features/NamedCompanions/INamedCompanionService.cs:3` | `NamedCompanionService` | NamedCompanionBehavior |
| 1 | `Main/Features/NavalTravel/INavalTravelConfigProvider.cs:4` | `NavalTravelConfigProvider` | NavalTravelSettingsProvider |
| 1 | `Main/Features/PlayerSwitcher/IHeroPickerService.cs:9` | `HeroPickerService` | Patch77_BodyGeneratorView_Constructor |
| 1 | `Main/Features/PlayerSwitcher/INarrativeCareerFastPathService.cs:8` | `NarrativeCareerFastPathService` | Patch78_CharacterCreationManager_StartNarrativeStage |
| 1 | `Main/Features/PlayerSwitcher/IPlayerClanLeadershipService.cs:15` | `PlayerClanLeadershipService` | PlayerClanLeadershipRepairBehavior |
| 1 | `Main/Features/PreloadBodyGuard/IPreloadBodyGuardService.cs:10` | `PreloadBodyGuardService` | PreloadHelper_WaitForMeshesToBeLoaded_Patch |
| 1 | `Main/Features/PrisonerRecruitment/IPrisonerRecruitmentMoraleService.cs:3` | `PrisonerRecruitmentMoraleService` | TaomPrisonerRecruitmentCalculationModel |
| 1 | `Main/Features/RevoltTuning/IRevoltTuningConfigProvider.cs:3` | `RevoltTuningConfigProvider` | TaomSettlementLoyaltyModel |
| 1 | `Main/Features/SettlementFood/ISettlementFoodConfigProvider.cs:3` | `SettlementFoodConfigProvider` | TaomSettlementFoodModel |
| 1 | `Main/Features/SettlementNameplateFade/INameplateFadeService.cs:11` | `NameplateFadeService` | SettlementNameplateWidget_DetermineTargetAlphaValue_Patch |
| 1 | `Main/Features/SettlementNameplateRelation/INameplateRelationAlphaService.cs:11` | `NameplateRelationAlphaService` | SettlementNameplateWidget_DetermineTargetAlphaValue_Patch |
| 1 | `Main/Features/ShaderPrecompilation/IPrecompileSceneProvider.cs:6` | `PrecompileSceneProvider` | ShaderPrecompileRunner |
| 1 | `Main/Features/ShaderPrecompilation/IShaderPrecompileCrashGuard.cs:11` | `ShaderPrecompileCrashGuard` | ShaderPrecompileRunner |
| 1 | `Main/Features/Siege/ISiegeEngineAvailabilityService.cs:6` | `SiegeEngineAvailabilityService` | TaomSiegeEventModel |
| 1 | `Main/Features/SiegeDismount/ISiegeDismountService.cs:3` | `SiegeDismountService` | SiegeDismountMissionBehavior |
| 1 | `Main/Features/SiegePropDiagnostics/ISiegePropDiagnosticsService.cs:6` | `SiegePropDiagnosticsService` | SiegePropDiagnosticsMissionBehavior |
| 1 | `Main/Features/SpecialResources/Hooks/IOnRecruitmentResourceGate.cs:10` | `RecruitmentResourceGateHook` | RecruitmentVM_RecruitGate_Patch |
| 1 | `Main/Features/StaleCharacterRepair/IStaleCharacterRepairService.cs:9` | `StaleCharacterRepairService` | Patch83_StaleCharacterRepair |
| 1 | `Main/Features/TimeAcceleration/ITimeAccelerationService.cs:3` | `TimeAccelerationService` | TimeAccelerationMixin |
| 1 | `Main/Features/TroopProgression/IVolunteerTierService.cs:3` | `VolunteerTierService` | TaomVolunteerModel |
| 1 | `Main/Features/TroopProgression/IWageModifierService.cs:14` | `WageModifierService` | TaomPartyWageModel |
| 1 | `Main/Features/WandererAllegiance/IWandererAllegianceService.cs:9` | `WandererAllegianceService` | WandererAllegianceDialogBehavior |
| 1 | `Main/Features/WarOfTheRingMomentum/IMomentumEnrollmentService.cs:11` | `MomentumEnrollmentService` | WarOfTheRingMomentumBehavior |
| 1 | `Main/Features/WarOfTheRingMomentum/IMomentumEventService.cs:10` | `MomentumEventService` | WarOfTheRingMomentumBehavior |
| 1 | `Main/Features/WarOfTheRingMomentum/IMomentumVictoryService.cs:12` | `MomentumVictoryService` | WarOfTheRingMomentumBehavior |
| 1 | `Main/Features/WarOfTheRingMomentum/UI/IMomentumUIService.cs:7` | `MomentumUIService` | WarOfTheRingMomentumBehavior |
| 1 | `Main/Features/Warg/IWargAttackService.cs:5` | `WargAttackService` | WargAttackTask |
| 2 | `Main/Core/Infrastructure/Reflection/IReflectionService.cs:3` | `ReflectionService` | ReflectionHelper, EyeHeightAdjustmentHook |
| 2 | `Main/Features/BanditManagement/IBanditScalingService.cs:14` | `BanditScalingService` | Patch39_BanditPartySize, TaomBanditDensityModel |
| 2 | `Main/Features/BanditManagement/IHideoutBossFightService.cs:80` | `HideoutBossFightService` | Patch86_HideoutBossFight, TaomBanditDensityModel |
| 2 | `Main/Features/BattleBalance/IBattleBalanceConfigProvider.cs:3` | `BattleBalanceConfigProvider` | TaomMilitaryPowerModel, TaomPartyHealingModel |
| 2 | `Main/Features/BattleLoadDiagnostics/IBattleLoadStallMarker.cs:17` | `BattleLoadStallMarker` | BattleLoadPhaseBehavior, Mission_Initialize_BattleLoad_Patch |
| 2 | `Main/Features/CaravanTrade/ICaravanVisitMemory.cs:18` | `CaravanVisitMemory` | CaravanVisitMemoryBehavior, CaravansCampaignBehavior_GetTradeScoreForTown_Patch |
| 2 | `Main/Features/CareerSystem/Mutations/IMutationCalculatorRegistry.cs:6` | `MutationCalculatorRegistry` | BuiltInCalculators, MutationService |
| 2 | `Main/Features/CharacterCreation/ICCBodyPropertiesService.cs:3` | `CCBodyPropertiesService` | CharacterCreationContent_SetSelectedCulture_Patch, CharacterCreationCultureStageVM_OnCu... |
| 2 | `Main/Features/CharacterCreation/ICultureRaceFilterService.cs:5` | `CultureRaceFilterService` | FaceGenRaceSelectorRebuilder, FaceGenVM_Refresh_RaceFilter_Patch |
| 2 | `Main/Features/CompanionTactics/BattleActionBar/IBattleActionBarService.cs:7` | `BattleActionBarService` | BattleActionBarMissionView, BattleActionBarVM |
| 2 | `Main/Features/CultureDoctrine/ICultureAggressionService.cs:12` | `CultureAggressionService` | TaomAgentStatCalculateModel, TaomCustomBattleAgentStatCalculateModel |
| 2 | `Main/Features/CultureDoctrine/ICultureMoraleService.cs:10` | `CultureMoraleService` | TaomBattleMoraleModel, TaomCustomBattleMoraleModel |
| 2 | `Main/Features/CustomBattles/Hooks/ISideCommanderFilter.cs:6` | `SideCommanderFilter` | CustomBattleSideVM_OnCultureSelection_Patch, CustomBattleSideVM_RefreshValues_Patch |
| 2 | `Main/Features/Diplomacy/Hooks/IOnAllianceAction.cs:3` | `AllianceActionHook` | AllianceCampaignBehavior_EndAlliance_Patch, DeclareWarAction_ApplyInternal_Patch |
| 2 | `Main/Features/DreadAura/IDreadAuraService.cs:9` | `DreadAuraService` | DreadAuraMissionLogic, DreadPulseRunner |
| 2 | `Main/Features/EditorCacheRebuild/Caching/IPathReuseCache.cs:5` | `PathReuseCache` | IPersistentPathCache, PersistentPathCache |
| 2 | `Main/Features/EliteEmissary/IEliteEmissaryService.cs:12` | `EliteEmissaryService` | EliteEmissaryBehavior, EliteEmissaryInquiryPresenter |
| 2 | `Main/Features/Elk/IElkAttackService.cs:10` | `ElkAttackService` | ElkCombat, ElkMissionBehavior |
| 2 | `Main/Features/Enlistment/Content/IBattleMeritAccumulator.cs:11` | `BattleMeritAccumulator` | EnlistmentBattlePayoutService, EnlistmentMeritMissionBehavior |
| 2 | `Main/Features/Enlistment/IServiceBattleService.cs:9` | `ServiceBattleService` | EnlistmentBattleBehavior, Patch85_EnlistedDetachDeferral |
| 2 | `Main/Features/Execution/Hooks/IOnExecutionAction.cs:14` | `ExecutionActionHook` | ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch, TraitLevelingHelper_OnBloodFe... |
| 2 | `Main/Features/FactionMap/ICultureResolverService.cs:3` | `CultureResolverService` | CultureStageViewCreatedHook, FactionSelectionVM |
| 2 | `Main/Features/FactionMap/IFactionHoverService.cs:5` | `FactionHoverService` | CultureStageViewCreatedHook, FactionSelectionVM |
| 2 | `Main/Features/FactionMap/IFactionSelectionService.cs:5` | `FactionSelectionService` | CultureStageViewCreatedHook, FactionSelectionVM |
| 2 | `Main/Features/FiefGranting/IFiefGrantPolicyService.cs:8` | `FiefGrantPolicyService` | TaomSettlementClaimantDecision, Patch70_FiefGrantDecisionSwap |
| 2 | `Main/Features/FiefManagement/IFiefHubService.cs:6` | `FiefHubService` | FiefHubMenuPresenter, Patch36_MapScreenF6 |
| 2 | `Main/Features/FieldCommission/IFieldCommissionDismissService.cs:13` | `FieldCommissionDismissService` | FieldCommissionDismissDialogBehavior, FieldCommissionDismissMenuBehavior |
| 2 | `Main/Features/MixedFormations/IFormationLayoutService.cs:8` | `FormationLayoutService` | MixedFormationsMissionBehavior, Patch30_FormationGetOrderPositionOfUnit |
| 2 | `Main/Features/PlayerSwitcher/IHeroSwitchService.cs:9` | `HeroSwitchService` | PlayerSwitchContentHandler, PlayerSwitchRegistrationBehavior |
| 2 | `Main/Features/PlayerSwitcher/ISwitchPlanner.cs:8` | `SwitchPlanner` | PlayerSwitchContentHandler, PlayerSwitchRegistrationBehavior |
| 2 | `Main/Features/Refuge/IRefugeDefenseService.cs:12` | `RefugeDefenseService` | TaomCombatSimulationModel, TaomCombatMechanicsModel |
| 2 | `Main/Features/SettlementEconomy/ISettlementEconomyConfigProvider.cs:3` | `SettlementEconomyConfigProvider` | SettlementEconomyCheats, TaomSettlementEconomyModel |
| 2 | `Main/Features/SettlementEconomy/ISettlementEconomyService.cs:3` | `SettlementEconomyService` | SettlementEconomyCheats, TaomSettlementEconomyModel |
| 2 | `Main/Features/ShaderPrecompilation/IShaderPrecompilationService.cs:5` | `ShaderPrecompilationService` | ShaderPrecompileRunner, TaomShaderGameManager |
| 2 | `Main/Features/UncapturableHeroes/IUncapturableHeroService.cs:12` | `UncapturableHeroService` | Hero_CanBecomePrisoner_Patch, TakePrisonerAction_Apply_Patch |
| 2 | `Main/Features/WarRam/IWarRamAttackService.cs:12` | `WarRamAttackService` | WarRamCombat, WarRamMissionBehavior |
| 3 | `Main/Features/ArmyTargeting/IArmyTargetingService.cs:3` | `ArmyTargetingService` | TargetScoreContextFactory, AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch,... |
| 3 | `Main/Features/BannerBearers/IBannerBearerService.cs:9` | `BannerBearerService` | BannerBearerAssignmentMissionLogic, BannerBearerLogic_SpawnBannerBearer_Patch, TaomBatt... |
| 3 | `Main/Features/BannerColorPersistence/IAgentColorStore.cs:5` | `AgentColorStore` | AgentColorStoreCleanupBehavior, Agent_EquipItemsFromSpawnEquipment_Patch, Mission_Spawn... |
| 3 | `Main/Features/BattleBalance/IBattleBalanceSettingsProvider.cs:3` | `BattleBalanceSettingsProvider` | TaomCombatSimulationModel, TaomMilitaryPowerModel, TaomPartyHealingModel |
| 3 | `Main/Features/BlowDiagnostics/IBlowDiagnosticService.cs:9` | `BlowDiagnosticService` | Agent_Die_BlowDiag_Patch, Agent_HandleBlowAux_BlowDiag_Patch, RangedSiegeWeapon_ShootPr... |
| 3 | `Main/Features/CombatMechanics/IChargeDamageService.cs:9` | `ChargeDamageService` | TaomAgentStatCalculateModel, MountChargeDamageApplier, TaomCustomBattleAgentStatCalcula... |
| 3 | `Main/Features/CompanionTactics/FormationPresets/IFormationPresetService.cs:11` | `FormationPresetService` | OOBOverlayService, FormationPresetCampaignBehavior, OOBButtonsVM |
| 3 | `Main/Features/CrashReport/ICrashReportService.cs:13` | `CrashReportService` | BattleLoadStallWatchdog, AppDomainExceptionHook, CrashReportPatchHelper |
| 3 | `Main/Features/EconomyDiagnostics/ITownGoldLedger.cs:9` | `TownGoldLedger` | EconomyDiagnosticsBehavior, EconomyDiagnosticsCheats, SettlementComponent_ChangeGold_Patch |
| 3 | `Main/Features/Elephant/IElephantAttackService.cs:11` | `ElephantAttackService` | TaomAgentStatCalculateModel, ElephantCombat, ElephantMissionBehavior |
| 3 | `Main/Features/EquipPresets/IEquipmentPresetService.cs:12` | `EquipmentPresetService` | EquipmentPresetCampaignBehavior, Patch33_GauntletInventoryScreen, PresetsOverlayVM |
| 3 | `Main/Features/FactionMap/ILandmarkService.cs:6` | `LandmarkService` | FactionDisplayHelper, CultureStageViewCreatedHook, FactionSelectionVM |
| 3 | `Main/Features/FieldCommission/IFieldCommissionOfferFlowService.cs:9` | `FieldCommissionOfferFlowService` | FieldCommissionSessionReset, FieldCommissionCheats, FieldCommissionBehavior |
| 3 | `Main/Features/Mumakil/IMumakilAttackService.cs:11` | `MumakilAttackService` | TaomAgentStatCalculateModel, MumakilCombat, MumakilMissionBehavior |
| 3 | `Main/Features/NavalTravel/INavalTravelService.cs:10` | `NavalTravelService` | Patch54_NavalTravelBoatVisual, Patch57_NavalAtSeaLandRescueGuard, TaomPartyNavigationModel |
| 3 | `Main/Features/SignatureStrikes/ISignatureStrikeService.cs:11` | `SignatureStrikeService` | TaomCombatMechanicsModel, SignatureStrikeRunner, SignatureStrikesMissionLogic |
| 3 | `Main/Features/SmartCavalryAI/ICavalryChargeService.cs:13` | `CavalryChargeService` | Patch31b_FormationSetTargetFormation, Patch31_FormationSetMovementOrder, SmartCavalryAI... |
| 3 | `Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs:3` | `PartyUpgradeResourceCheckHook` | PartyCharacterVM_InitializeUpgrades_Patch, PartyScreenLogic_AddCommand_Patch, PartyScre... |
| 4 | `Main/Features/CareerSystem/ICareerQuestService.cs:12` | `CareerQuestService` | CareerQuest, CareerQuestCampaignBehavior, CareerScreenVM, GauntletCareerScreen |
| 4 | `Main/Features/CompanionTactics/FormationPresets/IOrderOfBattleVMTracker.cs:11` | `OrderOfBattleVMTracker` | OOBOverlayService, Patch35_OrderOfBattleVM_Ctor, Patch35_OrderOfBattleVM_Finalize, OOBB... |
| 4 | `Main/Features/EditorCacheRebuild/Phase1/IPhase1Filter.cs:6` | `ChangedSettlementsFilter` | CacheBuilderService, IPhase1Builder, ParallelPhase1Builder, SerialPhase1Builder |
| 4 | `Main/Features/Enlistment/IServiceMaintenanceService.cs:24` | `ServiceMaintenanceService` | EnlistmentBattleBehavior, EnlistmentBehavior, EnlistmentMaintenanceBehavior, Enlistment... |
| 4 | `Main/Features/LotrIssues/ILotrIssueService.cs:12` | `LotrIssueService` | LotrIssuesCampaignBehavior, CombatLotrIssue, DeliverGoodsLotrIssue, DeliverPersonnelLot... |
| 4 | `Main/Features/MarriageAlignment/IMarriageAlignmentService.cs:9` | `MarriageAlignmentService` | MarriageClanPoolCache, MarriageAlignmentCheats, Patch81_MarriageClanDraw, TaomMarriageM... |
| 4 | `Main/Features/SettlementGuards/ISettlementGuardService.cs:5` | `SettlementGuardService` | ManualPatchApplicator, GuardsCampaignBehavior_GetSuitableSpear_Patch, GuardsCampaignBeh... |
| 4 | `Main/Features/WarOfTheRingMomentum/IMomentumQueryService.cs:15` | `MomentumQueryService` | MomentumIndicatorItemVM, MomentumIndicatorMapView, MomentumPopupController, MomentumPop... |
| 4 | `Main/Features/WarOfTheRingMomentum/IMomentumStateStore.cs:12` | `MomentumStateStore` | MomentumQueryService, PlayerMomentumService, WarOfTheRingMomentumBehavior, MomentumCheats |
| 5 | `Main/Features/CaravanTrade/ICaravanTradeService.cs:33` | `CaravanTradeService` | CaravansCampaignBehavior_CalculateBudgetFactor_Patch, CaravansCampaignBehavior_CanTrade... |
| 5 | `Main/Features/Elephant/IHowdahDiagnosticsSettingsProvider.cs:4` | `HowdahDiagnosticsSettingsProvider` | ElephantMissionBehavior, HowdahDiagnosticsReporter, TaomHowdahMachine, TaomHowdahStandi... |
| 5 | `Main/Features/Enlistment/EncounterOwnershipPolicy.cs:14` | `EncounterOwnershipPolicy` | DischargeService, EnlistmentReconciler, EnlistmentService, ServiceBattleService, Servic... |
| 5 | `Main/Features/PlayerSwitcher/IPlayerSwitchSessionWriter.cs:9` | `PlayerSwitchSessionStore` | PlayerSwitchContentHandler, PlayerSwitchRegistrationBehavior, BodyGeneratorPreviewSink,... |
| 5 | `Main/Features/SiegeDismount/Models/IMountSnapshot.cs:3` | `MountSnapshot` | IPartyMountInventoryAdapter, IPlayerMountAdapter, PartyMountInventoryAdapter, PlayerMou... |
| 6 | `Main/Features/Spider/ISpiderAttackService.cs:6` | `SpiderAttackService` | TaomAgentStatCalculateModel, SpiderMissionBehavior, SpiderAttackOffCooldownDecorator, S... |
| 7 | `Main/Features/Enlistment/IEnlistmentStateMachine.cs:11` | `EnlistmentStateMachine` | DischargeService, EnlistmentLoadNormalizer, EnlistmentReconciler, EnlistmentService, Se... |
| 8 | `Main/Features/ElephantLike/IElephantLikeAttackService.cs:14` | `ElephantLikeAttackService` | IElephantAttackService, ElephantLikeAttackOffCooldownDecorator, ElephantLikeAttackTasks... |
| 15 | `Main/Features/SaveLoadDiagnostics/ISaveLoadDiagnosticsService.cs:9` | `SaveLoadDiagnosticsService` | ArchiveDeserializer_LoadFrom_Patch, CampaignBehaviorDataStore_LoadBehaviorData_Patch, C... |
| 17 | `Main/Features/Enlistment/Content/IEnlistmentContentStore.cs:11` | `EnlistmentContentStore` | ServiceStatusService, ArmyRhythmSnapshotService, DischargeConsequenceService, Enlistmen... |
| 18 | `Main/Features/CulturalFeats/ICulturalFeatsService.cs:21` | `CulturalFeatsService` | TaomArmyManagementModel, TaomBattleRewardModel, TaomBuildingConstructionModel, TaomCara... |

`IMainMenuCustomizerService` shows 0 because its one caller is `SubModule.cs:632`, which the filter excludes.
</details>

### [ARCH-02] Delete three unreachable scaffolds that tests and a binding row keep alive

- **Evidence**: `Main/Adapters/IEditorSceneAdapter.cs:5`: an adapter interface (29 lines) with zero implementations and zero references anywhere in `Main/` or `TAOM.Tests` (`grep -rnw IEditorSceneAdapter` returns only the declaration). Added in `6a80bac6` (2026-05-12), untouched since.
- **Evidence**: `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs:16-17` registers `IPathReuseCache`/`PathReuseCache` and `IPersistentPathCache`/`PersistentPathCache`; neither interface is resolved or injected anywhere (`grep -rlw` finds only the `Caching/` files and the IoC file). `NavigationPathCloner` and `SortedPathKey` are used only by those two caches, so the whole `Caching/` folder (6 files, 292 lines) is unreachable. `CacheRebuildConfig.cs:37` opens a "Reserved fields (NOT in shipped JSON, wired in future phases)" block whose summaries at `:42` and `:45` say "PathReuseCache scaffolding exists in `Caching/`" and "PersistentPathCache scaffolding exists", and `EnablePathReuse`/`EnablePersistentPathCache` have no reader outside the config class. The scaffold is carried by 406 test lines (`TAOM.Tests/Features/EditorCacheRebuild/Caching/*.cs`, four files) and one engine binding row, `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:65` (`TaleWorlds.Engine.PathReuseCache._store`, cited as `PersistentPathCache.cs:149`), which an engine bump must keep green for code that never runs. All from `6a80bac6`.
- **Evidence**: `Main/Features/CompanionTactics/CompanionTacticsIoC.cs:27` registers `IHeroAutoAssigner`/`HeroAutoAssigner` (51 + 17 lines, 93 test lines); nothing resolves it. The player-facing button it was built for is bound (`Main/_Module/GUI/PreFabs/OOBButtonsOverlay.xml:24`, `Command.Click="ExecuteAssignCharacters"`) to `OOBButtonsVM.cs:73-89`, which prints "Auto-Assign is a Phase-1 stub, feature pending" (literal, unlocalized, with an em dash in the shipped string) since `55950378` (2026-05-07). The comment at `:81-87` records a Codex P2 on it.
- **Impact**: 850 lines of source and tests (292 + 406 + 68 + 93, less overlap) that build, test and bind on every engine bump for zero runtime effect, plus one Order of Battle button that tells players the feature is unfinished. This is the playbook's "aspirational plumbing (banned)" case.
- **Effort**: S (delete files, two IoC lines, the reserved config fields, one DataRow; for the button, either remove the prefab node and command or wire `HeroAutoAssigner` if Mike wants the feature).
- **Risk**: LOW. Nothing resolves the types; the build and `EditorCacheRebuild` tests prove parity. The only judgment call is the OOB button (remove vs. finish), which is a product decision.
- **Confidence**: HIGH (reads plus reference greps on the `b2e387db` tree).
- **Fix sketch**: delete `IEditorSceneAdapter.cs`, `EditorCacheRebuild/Caching/` with its tests, the two registrations, the reserved `CacheRebuildConfig` fields and the `ReflectionSiteBindingTests.cs:65` row; git history (`6a80bac6`) is the recovery path. For Auto-Assign, ask Mike: remove the button and `HeroAutoAssigner`, or file the wiring as a feature issue.
- **Delta**: pre-existing (all from May 2026).
- **P1**: no.
- **Plan candidate**: yes. A parity deletion with a clean verification story (build plus the filtered suite), the highest-leverage class in `simplicity-criterion.md`.

### [ARCH-03] The "patch to hook interface to service" rule describes 11% of patches; record the real rule and drop two pure forwarders

- **Measurement**: 217 `.cs` files in `Main/` carry `[HarmonyPatch` or `HarmonyPatchCategory` (script over the `b2e387db` tree). Of those, **24** reference an `IOn*` hook interface, **113** reach a service, provider or policy interface directly (the house resolves it in a static `Initialize` or via `IoC.Resolve` at the patch boundary), 3 reach only an adapter, and 77 hold no TAOM interface at all. There are 19 `IOn*` hook interfaces, implemented by 13 classes; **none** is substituted or faked in `TAOM.Tests` (`Substitute.For<IOn` count 0; the tests that exist construct the concrete hook class).
- **Evidence**: `AGENTS.md:36` (at `b2e387db`) states the architecture as "Patch, model or behavior → hook interface → service → adapter". The code base has settled on "patch → service" for 113 patches and nobody flags it, so the rule reads as mandatory while practice treats the hook layer as optional.
- **Evidence (pure pass-throughs)**: `Main/Features/SpecialResources/Hooks/RecruitmentResourceGateHook.cs:14-15`, one method, `=> _service.CanAffordRecruit(...)`, one caller (`RecruitmentVM_RecruitGate_Patch.cs:23-26`), no test names the class. `Main/Features/FactionMap/Hooks/CultureStageViewFinalizeHook.cs:5-9`, forwards to two statics on `CultureStageViewCreatedHook`, no test. `Main/Features/BannerInjection/Hooks/BannerEditorDoneHook.cs:16-26` forwards to `IBannerExclusionService.MarkAsPlayerModified` plus two log lines, no test.
- **Kept by the deletion test**: `PartyUpgradeResourceCheckHook.cs:15-40` is also mostly forwards (8 of 10 members), but it narrows the 192-line `ISpecialResourceService.cs` to the party-screen session API for three patches; inlining it would scatter that choice across three patch files, so it provides locality and stays. The other hook classes (`PeaceActionHook`, `AllianceActionHook`, `ExecutionActionHook`, `CustomBattle*Hook`, `TroopWeightDisplayHook`, `EyeHeightAdjustmentHook`, `CultureStageViewCreatedHook`) hold co-op gating, id resolution or side logic and have concrete tests: they are the patch's service under another name, which is fine.
- **Side note (perf, LOW)**: `CultureStageViewTickHook.cs:15` calls `AccessTools.Field(viewInstance.GetType(), "GauntletLayer")` on every tick of the culture-selection screen; `CultureStageViewCreatedHook.cs:70` already does the same lookup once. Character creation only, so not worth a finding of its own.
- **Impact**: the doc rule makes reviewers ask for a hook layer that adds a file and an interface and hides nothing (the three forwarders above), while 113 compliant-in-practice patches look like violations to a strict reader. Low runtime cost; the cost is review friction and one extra hop per forwarder.
- **Effort**: S.
- **Risk**: LOW (doc line; deleting the two forwarders changes two patch field types and one IoC line each).
- **Confidence**: HIGH on the counts; MED on the classification of the 77 "no interface" patches (some take concrete services through a static `Initialize`, which the regex does not see; that does not change the conclusion).
- **Fix sketch**: amend `AGENTS.md:36` to "Patch, model or behavior → service (through a hook interface only when the patch needs a narrow seam or a test fake) → adapter", then fold `RecruitmentResourceGateHook` and `CultureStageViewFinalizeHook` into their patches' direct service or static calls when those files are next touched. Pair with ARCH-01, same policy edit.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, folded into the ARCH-01 policy plan (same reviewers, same files); not a plan on its own.

### [ARCH-04] Extract parked NativeSkinFixes (it cannot be re-enabled as parked); keep NavalTravel parked

The parking is decided (BRIEF "Decided tradeoffs"); this finding prices what parking costs and asks
whether "parked" still means "ready to re-enable". Wiring read at `b2e387db` via `git show`.

| Cost item | NativeSkinFixes | NavalTravel |
|---|---|---|
| Wiring | install branch commented out, `SubModule.cs:716-739`; `Uninstall()` still called at `:2121-2124` (no-op when not installed, `NativeSkinFixesInstaller.cs:152-154`) | model registration commented at `SubModule.cs:1038-1044`; Patch54/Patch57 commented at `:1747-1761`; services still registered, `IoC.cs:112` and `NavalTravelIoC.cs` (three singletons) |
| Managed C# (`Main/Features/<name>`) | 5 files, 474 lines | 11 files, 868 lines |
| Native source | 15 files, 1,521 lines (`Dependencies/NativeSkinFixes.NativeHooks/*.cpp,*.h,*.ps1`, MinHook excluded) | none |
| Tests | 1 file, 119 lines, plus rows in `Mcm/SettingRequireRestartPostureTests.cs:39` | 2 files, 392 lines, plus mentions in `CoopVetoClassificationTests.cs:280` and `CulturePartyTemplateTests.cs:58` |
| Binding rows kept alive | none (native; no managed engine binding) | 3 Harmony types verified by reflection in `HarmonyPatchBindingTests` (Patch54 x2, Patch57) and `TaomPartyNavigationModel` in `GameModelOverrideBindingTests` (reflects every `GameModel` subclass); API snapshot `patch-targets.md:197-199`, `gamemodel-bases.md:179-185` |
| Shipped to every player | `TAOM.NativeSkinFixes.dll` 190,976 B and `MinHook.x64.dll` 16,384 B in `Main/_Module/bin/Win64_Shipping_Client/` (`git ls-tree -l`); MinHook has no other consumer (`grep -rl MinHook Main` hits only `NativeHookLoader.cs`, `TaomSettings.cs`, `SubModule.cs`) | `naval_travel/naval_travel_config.json` |
| Player-visible no-op settings | 1 MCM toggle, `TaomSettings.cs:136-139`; its hint says the targets are "verified against Bannerlord v1.4.6" | 3 MCM toggles, `TaomSettings.cs:990-1003`, hints honest ("Currently DISABLED") |
| CI and review surface | the native-CRT gate `.github/workflows/build.yml:85-99` exists only for this DLL; `/deep-review` lens `1-standards.md` item 9b is a C++ section whose named target is this port | none |
| Open findings it carries | triage-A L77, L109 (SignatureScanner ambiguity), L131 (DllMain teardown), all STILL_VALID and LOW "while parked" | none |
| Commits since `141b749` | 3, all incidental sweeps (`4b3f30a2`, `c3dde6b9`, `0fc9b8c1`) | 4, all June: the feature (`584683f5`, 2026-06-24), the park (`e8543aff`, 2026-06-26), two sweeps |
| Engine-bump cost | `docs/migration/v1.4.7-impact.md:24`, `v1.4.8-impact.md:67` (an UNVERIFIED in-game row) | `v1.4.8-impact.md:70`; `v1.5.3-impact.md:65` (Patch54's target `AddMountToPartyIcon` changed signature; judged OK only because parked) |
| Docs referencing it | 43 files under `docs/`, `.claude/`, `.ai/` (`git grep -l -i`) | 27 files |
| Ready to re-enable? | **No.** `Signatures.h:29` "AUTHORED FOR BANNERLORD v1.4.6"; `docs/features/native-skin-fixes.md:131` "They have not been re-verified since. The engine is now v1.5.3, five bumps past". A re-enable re-authors all seven native patterns first. | **Blocked outside the code** (TAOM_Map has no naval navmesh, #296/#120); the managed code is current enough that bumps touch it only through binding rows |

- **Evidence**: the table rows, each measured with the command named in it.
- **Impact**: NativeSkinFixes ships 207 KB of native code to every player that never loads, keeps one CI gate, one review-lens section, three open security findings and two impact-doc rows alive, and its "RE-ENABLE: uncomment" instruction (`SubModule.cs:728`) is false: uncommenting would pattern-scan a v1.5.3 `TaleWorlds.Native.dll` with v1.4.6 signatures. NavalTravel's cost is managed code, three binding rows and one snapshot section per bump, which is modest against a feature whose blocker is map data.
- **Effort**: S to M for NativeSkinFixes extraction (delete `Main/Features/NativeSkinFixes/`, its test, the two DLLs, the MCM toggle and posture-test row, the SubModule comment block and `Uninstall` call, the CI step; update the feature doc to a "removed, recover from tag" stub). NavalTravel: none.
- **Risk**: LOW. The install branch is already dead code, so runtime behaviour is unchanged; the recovery path is a git tag such as `parked/native-skin-fixes` on `b2e387db` plus the feature doc, which already holds the RVA and verification map. Touches `SubModule.cs` (single-owner): a recommendation for its owner, not a parallel edit. The vendored-DLL allowlist shrinks, which the decided tradeoff permits (it limits what may ship, not what must).
- **Confidence**: HIGH. "Players never load it": `grep -rl "NativeHookLoader|TAOM.NativeSkinFixes.dll|LoadLibrary" Main` finds only the feature's own five files and `SubModule.cs:718`, which is a comment; the only caller of `Install` is the commented branch.
- **Fix sketch**: NativeSkinFixes: extract behind a tag, as above. NavalTravel: keep parked; optionally hide its three MCM toggles until the navmesh exists (players see a group that does nothing), which is Mike's call.
- **Delta**: pre-existing (parked 2026-06-26 and 2026-07-08), cost accrued since.
- **P1**: no.
- **Plan candidate**: yes for NativeSkinFixes extraction (a parity deletion with a simple build-and-suite verification, needs Mike's yes because it reverses a "parked" into a "removed"); no for NavalTravel.

### [ARCH-05] Record the protected-virtual boundary seam as an ADR-007 exception, then use it to seam the 15 services that read engine statics raw

- **Measurement**: `seams.py` (scratchpad) over every `Main/Features/**/*{Service,Engine,Policy,Store,Planner,Provider}.cs` at `b2e387db`, comments stripped. It counts engine-static reads (`Hero.MainHero`, `MobileParty.MainParty|All`, `Settlement.All|Find`, `Campaign.Current`, `Clan.PlayerClan`, `Kingdom.All`, `CampaignTime.Now`, `InformationManager.*`, `Mission.Current`, `MBObjectManager.Instance` and similar) inside and outside `protected|internal|public virtual` member bodies.
- **Who uses the pattern**: 5 services, **97 virtual seam members**, holding 91 engine-static reads: `Main/Features/Refuge/RefugeService.cs` (39 members, seams at `:789-1227`), `Main/Features/FieldCamp/CampService.cs` (32, from `:661`), `Main/Features/SupplyLines/SupplyOrderService.cs` (13, from `:457`), `Main/Features/Refuge/WardenService.cs` (10), `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs` (3). Each has one test subclass that overrides the seams: `RefugeServiceTests.cs` (42 overrides, 1,796 lines), `CampServiceTests.cs` (32, 1,256), `SupplyOrderServiceTests.cs` (13, 821), `WardenServiceTests.cs` (10, 314), `RuntimeCacheRebuildServiceTests.cs` (3, 380). Seam signatures take string ids and value types (`RefugeService.cs:789` `string MainPartyId()`, `:818` `float DistanceFromMainPartyTo(string partyId)`, `:860` `string SpawnRefugeParty(string, string)`), so the services' public surfaces stay engine-free. Four of the five were added on 2026-08-22 (`24cce287`, `03bcd465`, `2244fed5`).
- **Not recorded anywhere**: `docs/adrs/007-adapter-pattern.md:552` "## Exceptions" lists only value types and entry points; `.ai/review-reference.md:21` grades "ADR-007 (sealed type in service)" CRITICAL; `git grep -i "protected virtual|virtual seam"` over `docs/adrs`, `.ai`, `.claude/rules`, `.claude/skills/deep-review`, `AGENTS.md` returns nothing. The pattern is named only in code (`RefugeService.cs:48-50`: "the CampService/SupplyOrderService pattern ... the virtual bodies are the honest untested boundary sliver") and in three feature-doc file tables (`docs/features/field-camp.md:114`, `refuge.md:138`, `supply-lines.md:233`).
- **The contrast that matters**: 15 other services read engine statics with **no seam at all**, 115 raw reads: `SupplyLines/SupplySourceService.cs` 25, `SupplyLines/SupplyCaravanService.cs` 17, `Siege/SiegeDefenseService.cs` 13, `CharacterCreation/CharacterCreationContentService.cs` 12, `FactionMap/CultureSettingService.cs` 8, `CharacterCreation/CareerMenuService.cs` 6, `MissionDiagnostic/MissionDiagnosticService.cs` 6, then eight with 1 to 4. Three of these are already on the books (triage-B L378: SiegeDefense, CareerMenu; triage-A L291: CultureSetting); SupplySource and SupplyCaravan are new since June, sit in the same feature as the seamed `SupplyOrderService`, and together hold 42 raw reads.
- **Cost evidence**: a virtual seam costs one member in the service plus one override in the test subclass. The adapter route for the same `RefugeService` coverage would need several new adapter interfaces and implementations (gold, party lifecycle, roster transfer, raid scan, messages), factory or container registration, and `Substitute.For` setup per test; the existing player adapters cover little of it (`Main/Adapters/IPlayerContextAdapter.cs` has four id getters, `IPlayerPartyAdapter.cs` two members). The pattern's real weaknesses: the seam bodies are untested engine code inside a service (439 lines in `RefugeService.cs` alone), and seams get copied, not shared: `CampService` and `RefugeService` declare nine identically named seams (`MainPartyId`, `PlayerGold`, `ChargePlayer`, `ShowMessage`, `DistanceToNearestFortification`, `HoldMainParty`, `NowInHours`, `NextRandomFloat`, `IsCampReady`), and `CampService.cs:763-766` is byte-identical to `RefugeService.cs:791-794`.
- **Impact**: as written, ADR-007 plus the review reference make four reviewed, well-tested services CRITICAL violations, while 15 services with no seam at all are the actual untestable debt. A reviewer following the letter would push the tested services toward adapters (large, no behaviour gain) and has no named cheaper target to offer the untested ones.
- **Effort**: S to record (an ADR-007 "Exceptions" entry plus one line in `.ai/review-reference.md` and the deep-review Standards lens). M per service to seam the raw-read services (`SupplySourceService` and `SupplyCaravanService` first: same feature as the seamed `SupplyOrderService`, so the test-subclass idiom is already local).
- **Risk**: LOW for the record. Seaming is behaviour-preserving (move each static read into a virtual member) with tests written against the subclass first.
- **Confidence**: HIGH on counts and on the absence of a record. MED on the adapter-route cost comparison (estimated from the seam list, not built).
- **Fix sketch**: amend ADR-007 "Exceptions" with "Protected-virtual boundary seams" and its conditions: seam signatures use ids and value types only; each seam body is a single engine call or query; the service's interface stays engine-free; a test subclass overrides every seam the tests reach. Then plan seams for `SupplySourceService` and `SupplyCaravanService`. Do not extract the copied seams into a shared adapter yet: three copies of one-line bodies is a tiny win against a new adapter and three test rewires (`simplicity-criterion.md` Reject); revisit when a fourth service copies them.
- **Delta**: introduced (all five seamed services and the two raw-read SupplyLines services date from 2026-08-22).
- **P1**: no.
- **Plan candidate**: yes. Record (S, doc-only) and seam SupplySource/SupplyCaravan (M, TDD with the existing idiom) are two clean plans.

### [ARCH-06] Retire the static `ReflectionHelper` facade; its 26 call sites sit outside the reflection binding gate

- **Evidence**: `Main/Core/Infrastructure/Reflection/ReflectionHelper.cs` (47 lines) is a static class whose type initialiser does `_service = IoC.Resolve<IReflectionService>()` and whose six methods forward one-for-one to `IReflectionService` (16 lines) / `ReflectionService` (134 lines, registered at `Main/IoC.cs:221`). A three-layer stack for six operations.
- **Evidence**: the codebase has already recorded the harm: `Main/Features/HeroRace/EyeHeightAdjustmentHook.cs:141-144`: "Injected rather than reached through the static ReflectionHelper, whose type initialiser resolves IReflectionService from the container. That static made this hook unreachable from a test host (TypeInitializationException on first touch) ... and it is service location in a service besides."
- **Evidence**: remaining users (`grep -rn "ReflectionHelper\."` at `b2e387db`): `Main/Features/HeroRace/CharacterSpawnerService.cs` (21 calls, `:46-50,70-71,83,88,129-132,162-164,222-225,288-309`), `Hooks/CharacterTableau_SetRace_Patch.cs:18-22` (3), `Cheats/RacePositionTuningCheats.cs:192` (1), and the adapter `Main/Adapters/BannerHeroAdapter.cs:34-35` (2). Meanwhile 74 files use HarmonyLib `AccessTools.Field/Property/Method` directly and 8 use raw `Type.GetField(..., BindingFlags)`, so the house facade is the minority idiom.
- **Evidence (gate gap)**: none of the 14 distinct engine members these calls name (`CharacterSpawner._agentEntity`, `_horseEntity`, `_agentVisuals`, `_spawnFrame`, `CreateFaceImmediately`, `ClothColor1/2`, `WieldWeapon`; `CharacterTableau._agentVisuals`, `_oldAgentVisuals`, `InitializeAgentVisuals`, `_isVisualsDirty`; the two `Kingdom` banner-color backing fields) has a row in `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` or in `docs/reference/taleworlds-api-snapshot/reflection-sites.md` (grep for each name: 0 hits, not even in the "Category C, not offline-verifiable" section at `:90`). They are statically typed (`typeof(TOwner)` is the declaring engine type), so they are offline-verifiable. `ReflectionService.GetFieldValue` throws `InvalidOperationException` on a missing member (`ReflectionService.cs:31-35`), so an engine rename would throw inside character-creation and tableau code rather than degrade. All 14 exist in the installed v1.5.3 decompile today (`~/.taom-src/v1.5.3/TaleWorlds.MountAndBlade.View.Scripts.CharacterSpawner.cs`, `...Tableaus.CharacterTableau.cs`, grep counts all non-zero), so there is no live defect.
- **Deepening deletion test**: deleting `ReflectionHelper` concentrates nothing and scatters nothing (it is a pure forwarder); `CharacterSpawnerService` would take `IReflectionService` through its constructor, as `EyeHeightAdjustmentHook` already does. `IReflectionService` is itself in the ARCH-01 (c) set: its one test declares the interface but builds the concrete class (`EyeHeightAdjustmentHookTests.cs:19,33`, `new ReflectionService(...)`), so under the ARCH-01 rule the interface can go too, while `ReflectionService` stays for its member cache and throw-on-missing semantics.
- **Impact**: a service-locator static inside a service (the Standards lens item 9 violation), one proven test-host trap, and 14 engine string bindings an engine bump never checks.
- **Effort**: S (inject `IReflectionService` into `CharacterSpawnerService` and `BannerHeroAdapter`; the patch and cheat may keep a lazily resolved instance at their boundary; add 14 `DataRow`s and catalogue lines).
- **Risk**: LOW (same reflection calls, different reach path); the new binding rows only add red tests on a future drift.
- **Confidence**: HIGH.
- **Fix sketch**: add the 14 rows first (gate before refactor), then constructor-inject and delete `ReflectionHelper.cs`.
- **Delta**: pre-existing (`6be80744`, 2026-01-29); the binding-gate gap is pre-existing too.
- **P1**: no.
- **Plan candidate**: yes. Small, test-first, clean verification (the binding suite plus HeroRace tests).

### [ARCH-07] Duplication verdicts: two consolidations pass the simplicity criterion, one looks cheap but breaks saves

Starting point: `jscpd-digest.md` (76 clones, 1,114 lines, 0.69%). jscpd says 0.69% is duplicated, so
duplication is not a repo-wide problem; the question per cluster is whether folding it wins more than
it costs. Semantic twins were measured with `difflib.SequenceMatcher` on non-blank, non-comment lines
of the `b2e387db` files (script inline in this session).

| Cluster | Measurement | Verdict (`simplicity-criterion.md`) |
|---|---|---|
| **SupplyLines row VMs**: `Main/Features/SupplyLines/UI/SupplyGoodRowVM.cs` vs `SupplyTroopRowVM.cs` (79 lines each) | `diff` shows the only code differences are the class name, the constructor name, and one label (`:25`, `{=taom_sl_good_row}...{STOCK}` vs `{=taom_sl_troop_row}...{COUNT}`, set at `:27`). The comment at `SupplyTroopRowVM.cs:8-10` justifies the twin by "the prefab binds the two lists through separate item templates", but a Gauntlet item template binds property names, not VM types. | **Keep (do it).** Equal result, simpler: one `SupplyRowVM` taking its label `TextObject` removes a 79-line file. S effort, LOW risk. Introduced (2026-08-22, `2244fed5`). Confidence MED on the prefab claim (not run in game). |
| **Skill and attribute name maps**: `Main/Features/CharacterCreation/CareerMenuService.cs:30-64` vs `NarrativeMenuBuilder.cs:18-52` (jscpd's largest clone by tokens, 353), plus the second clone `:329-344` vs `:130-145` | Byte-identical 18-entry `SkillMap` and 6-entry `AttributeMap`; the only two files in `Main/` with more than 10 `DefaultSkills.X` references. | **Keep (do it).** Move both tables to one internal static class; pure deletion of one copy, no new abstraction worth the name. S, LOW. Pre-existing. |
| **LotrIssues templates**: `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs` (297), `DeliverGoodsLotrIssue.cs` (419), `DeliverPersonnelLotrIssue.cs` (366); 280 duplicated lines, the biggest jscpd cluster | `GetFrequency()`, `CanPlayerTakeQuestConditions(...)` and `EnsureDef()` are byte-identical in all three (normalised-whitespace comparison); `Tx(...)` and `GetIssueEffectAmountInternal` have drifted slightly. | **Split verdict.** A shared `LotrIssueBase : IssueBase` is the obvious fix and is **save-breaking**: the save system keys every saveable field by `(classLevel << 8) + localId` (`TaleWorlds.SaveSystem.Definition/MemberTypeId.cs:9`), and `classLevel` counts the declaring type's depth below `object` (`TypeDefinitionBase.cs:20-32`), so inserting any intermediate class shifts the ids of `_defId` (`CombatLotrIssue.cs:29`, `[SaveableField(1)]`) and every quest field in existing saves. Read from the v1.5.3 decompile dump (`E:\Decompiled_Bannerlord\_categories_v1.5.3\Core\TaleWorlds.SaveSystem`), not the installed DLL, so UNVERIFIED against the binary, but the mechanism has not changed across versions I have seen. A static `LotrIssueRules` helper for the three identical methods is save-neutral and **passes** (about 35 lines per copy, so about 70 redundant lines go), S effort; the rest of the clone is engine override boilerplate that must stay per class. |
| **Elephant and Mumakil twins** (`Main/Features/Elephant/*` vs `Main/Features/Mumakil/*`) | Similarity ratios: standing point 0.33 (277 vs 210 lines), mission behavior 0.36, crew spawner 0.42, behavior tree 0.68 (68 vs 65 lines), config 0.25. The attack logic was already unified in `a405ea15` (2026-07-01, `ElephantLike`). | **Reject.** Below 0.5 similarity on every large pair; what is shared already moved to `ElephantLike`. The 0.68 behavior-tree pair is 45 matching lines of tree wiring whose node lists differ; a shared builder would add a parameterised abstraction for a two-creature win. |
| **Spider vs ElephantLike BT nodes** | cooldown decorator 0.63 (26 vs 28 lines), engage decorator 0.34, attack service 0.10 | **Reject.** Different attack models; the one similar pair is 26 lines. |
| **Warg BT elements** (131 lines across 13 small clones, 6 to 18 lines each) | Each clone is the node constructor and blackboard plumbing the inlined BehaviorTrees library requires (`CleanIfEnemyDied.cs:10` vs `PrepareOnFirstCall.cs:13`). | **Reject.** Library-imposed shape; a Warg node base class would save about 100 lines at the price of a new hierarchy every other creature's nodes do not share. The Warg per-tick IoC resolves are seed F4, not duplication. |
| **FactionMap `CultureStageView_*_Patch`** (62 lines) | Three patches repeat the `PossibleTypeNames` list and `FindCultureStageViewType()` (`CultureStageView_Tick_Patch.cs:15-29`). | **Keep, S.** Move the type probe to one internal static (it is a two-name engine-version probe; three copies will drift at the next rename). Low value. |
| **JSON config providers** | 37 files (triage-B L343, L362) | Known, STILL_VALID; not re-reported. The MCM-over-JSON dead-fallback half is tracked as #561 (`docs/features/mcm.md:108-111`); my count: 11 settings providers with 38 `TaomSettings.Instance?.X ?? _defaults.X` fallbacks (`grep -c`). |
| **Per-culture lookup tables** | Six feature files hold 10 to 28 distinct culture ids each (`BannerBearers/Domain/BannerBearerConfig.cs` 28, `CustomBattles/Config/CustomBattleCommandersProvider.cs` 22, `MenuLinkColors/TaomCultureLinkStyles.cs` 20, `EliteEmissary/EliteEmissaryConfigProvider.cs` 20, `CombatMechanics/CombatMechanicsConfig.cs` 18, `AdvancedStartOptions/TaomStartOptionsProvider.cs` 15). | **Reject as duplication.** Each table holds feature-specific values, so there is nothing to merge; the shared risk is the culture id list itself drifting, which triage-A L205 already owns. |

- **Delta**: SupplyLines row VMs introduced; the rest pre-existing.
- **P1**: no.
- **Plan candidate**: yes for one small "fold the three safe duplicates" plan (row VM, skill maps, LotrIssue static helper) with the save-compat warning written into it; no for the rejects.

### [ARCH-08] Replace the per-creature marker services behind the mount-lock with one creature table

- **Evidence**: `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs:38-40,45,50` takes `IElephantAttackService`, `ISpiderAttackService` and `IMumakilAttackService` as constructor dependencies, and uses them only to ask one question: is this monster a locked creature (`:74-78` `CanAgentRideMount`, `:105-111` a three-level ternary that sets `MountDifficulty` to `ElephantConfig.MountDifficulty`, `SpiderConfig.MountDifficulty` or `MumakilConfig.MountDifficulty`, all `999f`: `ElephantConfig.cs:90`, `SpiderConfig.cs:20`, `MumakilConfig.cs:23`). `git show b2e387db:Main/SubModule.cs:1208-1215` resolves the three services only to pass them in. The file header (`:20-35`) logs each creature being bolted on (2026-06-05, 06-10, 06-29), plus the explicit opt-outs (chariot; `ElkConfig.cs:24` and `WarRamConfig.cs:19` "deliberately carries no MountDifficulty").
- **Evidence**: four marker interfaces exist to key per-creature registrations of one class: `IElephantAttackService`, `IMumakilAttackService`, `IElkAttackService`, `IWarRamAttackService` each `: IElephantLikeAttackService` with an empty body (`Main/Features/Elephant/IElephantAttackService.cs:11`, and the three siblings), each implemented by a subclass that is only a constructor passing config constants (`Main/Features/Elephant/ElephantAttackService.cs:12-23`, 22 lines; the Mumakil, Elk, WarRam twins 22 to 26 lines). Consumers resolve them lazily through the container inside static profiles (`Main/Features/Elephant/ElephantCombat.cs:19`, `() => IoC.Resolve<IElephantAttackService>()`). Creature monster-id checks are spread over 15 files (17 non-declaration call sites of `IsCreatureMonster`/`IsSpiderMonster`, plus `WargConfig.IsWargMonster` read from `Main/Adapters/AgentAdapter.cs:61`).
- **Deepening deletion test**: a single `CreatureMountLock` (monster id to lock flag and difficulty, built from each creature's config) concentrates the lock rule in one module the model asks once; the model loses three dependencies and the ternary. Constructing `new ElephantLikeAttackService(<creature config>)` inside each creature's static combat profile would make the four marker interfaces and four subclasses deletable (about 140 lines), because the service is pure and dependency-free (`ElephantAttackService.cs:5-10` says so). Both moves concentrate; neither scatters.
- **Side observation (lead for the correctness lane, UNVERIFIED)**: the Custom Battle slot, `Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs:25,36,43`, overrides only `InitializeAgentStats` and `UpdateAgentStats` and takes no creature service (`SubModule.cs:1266-1267`), so the creature mount-lock the campaign model applies does not exist in Custom Battle. Whether creatures can appear riderless there, and whether that is intended, I did not check.
- **Impact**: every new lockable creature edits a career-system model, its constructor, `SubModule.cs` (single-owner) and the test fixture for that model; the lock lives in a campaign-only model, which is how Custom Battle ended up without it.
- **Effort**: M.
- **Risk**: MED. Touches `ElephantLike/**`, `Elk/**` and `SubModule.cs`, all in another session's live working tree right now (BRIEF skip list), and the stat model is a hot per-agent path (keep the lookup a static dictionary, no IoC per call).
- **Confidence**: HIGH on the structure (reads at `b2e387db`); MED on the 140-line estimate (sum of the four interface and subclass files, 11 to 26 lines each).
- **Fix sketch**: after the in-flight Elk/Animalia work lands, add a static `CreatureMountLock` table fed from the creature configs, point both stat models at it, then collapse the marker interfaces by constructing each creature's `ElephantLikeAttackService` in its combat profile.
- **Delta**: introduced (the ElephantLike unification `a405ea15` 2026-07-01, Mumakil lock 2026-06-29, Elk and WarRam markers since).
- **P1**: no.
- **Plan candidate**: not now; hand it to the owner of the in-flight creature session as a follow-up once their work is committed.

## Ranking (leverage = impact / effort, discounted by confidence and risk)

1. ARCH-02 (dead scaffolds): S, LOW risk, HIGH confidence, pure parity deletion.
2. ARCH-01 + ARCH-03 (one policy edit): S, stops the (c) set growing and aligns the hook rule with practice.
3. ARCH-06 (reflection facade and 14 unbound engine members): S, test-first, closes a binding-gate gap.
4. ARCH-05 (record the protected-virtual seam; seam SupplySource/SupplyCaravan): S to record, M per service.
5. ARCH-04 (extract NativeSkinFixes): S to M, needs Mike's yes.
6. ARCH-07 (three safe folds; the LotrIssue base-class trap): S, low value but cheap.
7. ARCH-08 (creature table): M, MED risk, wait for the in-flight creature session.

## Considered and rejected

- **A mass-deletion PR for the 154 (c) interfaces.** Parity holds, but it would touch every feature IoC file plus the single-owner `IoC.cs`/`SubModule.cs` while parallel sessions are live; the policy fix in ARCH-01 plus opportunistic deletion gets the same end state without the merge risk.
- **Consolidating the 14 Hero adapter interfaces** (`Main/Adapters/I*Hero*Adapter.cs`, 62 members) against `docs/ai-includes/architecture.md:149` "One adapter interface per sealed type". Only 7 member names repeat (`StringId`, `GetSkillValue`, `IsValid` in three each; `Name`, `CultureStringId`, `AddRenown`, `AddItemToInventory` in two). One fat `IHeroAdapter` would couple every feature to every other feature's hero needs; per-feature adapters are interface segregation doing its job. The doc line is what is wrong; fold the fix into the ARCH-01 policy edit ("one adapter per sealed type per feature need").
- **Extracting the copied protected-virtual seams** (`PlayerGold`, `ChargePlayer`, `ShowMessage`, `MainPartyId` in Camp, Refuge and SupplyOrder services) into a shared adapter: one-line bodies copied three times against a new adapter plus three test-subclass rewires. Tiny win, added complexity: Reject until a fourth copy appears (ARCH-05).
- **`PartyUpgradeResourceCheckHook`** as a pass-through to delete: 8 of 10 members forward, but it narrows the 192-line `ISpecialResourceService` for three patches; the deletion test says it provides locality (ARCH-03).
- **Collapsing `TroopWeightDisplayHook`'s five one-method hook interfaces** (`IOnCampaignUIHelperGetPartyHealthTooltip`, `IOnClanPartyItemUpdateProperties`, `IOnPartyCharacterVMRefreshValues`, `IOnPartyVMRefreshPartyInformation`, `IOnRecruitmentVMRefreshPartyProperties`, all implemented by `Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs`, none faked in tests) into one: five small files, one class; saves little, reads fine. Covered by the ARCH-01 guideline if the file is touched.
- **Deleting NavalTravel.** Its blocker is map data, its carrying cost is three binding rows and one snapshot section per engine bump, and its managed code is current (ARCH-04).
- **Removing the always-true `CrewSpawnEnabled` flags** (`Main/Features/Elephant/HowdahCrewSpawner.cs:27`, `Main/Features/Mumakil/MumakilCrewSpawner.cs:27`): "flag fully rolled out but still branching" in form, but the crew has been parked and unparked before (#627) and the comment at `HowdahCrewSpawner.cs:26` names it as the park switch; two `if`s are a fair price. `TaomHowdahMachine.BoneTrackingEnabled` (`:242`, false) is triage-C L808.
- **A base class for the JSON config providers.** Known (triage-B L343, L362); triage already records that a base only wins with a bulk migration. Not re-reported.
- **Per-culture lookup tables** (six files, 15 to 28 culture ids each): feature data, not duplicates (ARCH-07).
- **Elephant/Mumakil, Spider/ElephantLike and Warg BT twins**: below the similarity where folding pays, or library-imposed shape (ARCH-07).
- **The 15 multi-implementation interfaces** (`IBTBannerlordBase` and the BT blackboards, `ICareerAbilityEffectExecutor`, `ICrashReportRenderer`, `IEnlistmentStateQuery` with its Null object, `IEquipmentIssueLedger`, `IFieldCommissionConfigProvider`, `IPhase1Builder`/`IPhase2Builder`, `ILogger`, `IBTNotifiable`): real polymorphism, no finding.

## What I did not cover

- No `dotnet build` or `dotnet test` (brief). Every parity claim ("deletion holds parity") is from reference greps on the `b2e387db` tree, not a compile.
- The (a)/(b)/(c) classification is a regex heuristic: it sees direct base-list implementations only (a class inheriting an interface through an abstract base is counted under the base), and "engine wrap" is a namespace-plus-static-dereference test. I spot-checked 51 of the 138 (a) rows by name and the 24 (c) rows that tests mention; the other rows are unreviewed by hand.
- The patch classification in ARCH-03 does not see concrete services passed through a static `Initialize`, so some of the 77 "no TAOM interface" patches do reach a service.
- ARCH-05's engine-static regex covers static entry points only (`Hero.MainHero`, `Campaign.Current` and similar), not instance calls on engine objects a service already holds, so the 115 raw-read count is a floor.
- The LotrIssues save-id mechanism was read from the v1.5.3 decompile dump, not the installed `TaleWorlds.SaveSystem.dll`; `taom-src` would not resolve `MemberTypeId` in this session (the script printed its search paths and no file).
- I did not verify in game that the Order of Battle "Auto-Assign" button is visible, nor whether creatures can appear riderless in Custom Battle (ARCH-08 side observation).
- GitHub issue state (#120, #296, #561) not checked (no `gh` call).
- `Main/SubModule.cs`, `Main/IoC.cs` and the skip-listed creature folders were read only at `b2e387db` (git archive or `git show`); their working-tree edits were not assessed.
- Python `tools/` duplication (playbook section 5, last bullet) is outside this lane's brief and was not examined.
