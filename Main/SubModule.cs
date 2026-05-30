using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TAOM.Features;
using TAOM.Features.BannerInjection;
using TAOM.Features.HeroRace;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionMap;
using TAOM.Features.InitialChildGeneration;
using TAOM.Adapters;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.Execution.Hooks;
using TAOM.Features.Execution.Models;
using TAOM.Features.RaceAge;
using TAOM.Features.RaceAge.Models;
using TAOM.Features.StartupResources;
using TAOM.Features.NamedCompanions;
using TAOM.Features.TroopProgression;
using TAOM.Features.TroopWeight;
using TAOM.Features.TroopWeight.Hooks;
using TAOM.Features.AtmospherePersistence.Hooks;
using TAOM.Features.TroopProgression.Models;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TAOM.Features.Warg;
// DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.
// using TAOM.Features.Spider;
using TAOM.Features.BattleBalance;
using TAOM.Features.BattleBalance.Models;
using TAOM.Features.Arena.Models;
using TAOM.Features.Encyclopedia;
using TAOM.Features.Encyclopedia.Models;
using TAOM.Features.MainMenuCustomizer;
using TAOM.Features.NativeSkinFixes;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.Siege;
using TAOM.Features.Siege.Models;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.ArmyTargeting.Models;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.LocalizationOverride;
using TAOM.Features.LocalizationOverride.Hooks;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Hooks;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.SettlementGuards;
using TAOM.Features.SettlementGuards.Hooks;
using TAOM.Features.RevoltTuning;
using TAOM.Features.BanditManagement;
using TAOM.Features.BanditManagement.Models;
using TAOM.Features.SiegeDismount.Hooks;
using TAOM.Features.MixedFormations.Hooks;
using TAOM.Features.SmartCavalryAI.Hooks;
using TAOM.Features.FiefManagement;
using TAOM.Features.FiefManagement.Hooks;
using TAOM.Features.SettlementNameplateFade;
using TAOM.Features.SettlementNameplateFade.Hooks;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using BehaviorTreeWrapper;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM;

public class SubModule : MBSubModuleBase
{
    private Harmony _harmony;
    private UIExtender? _uiExtender;
    private ITimeAccelerationService? _timeAccelerationService;
    private static float _shaderTickAccumulator;
    private static int _lastShaderCount = -1;
    private static bool _missionTimePatchesApplied;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        IoC.Configure();

        // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
        // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
        // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
        // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
        // is the IoC.Configure() call itself — if THAT throws, the entire feature is
        // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
        // a manual DI container; accept and document the residual.
        _harmony = new Harmony("com.taom.mod");
        if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
        {
            try
            {
                _harmony.PatchCategory("Patch37_CrashReport");
                IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
                {
                    IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
                }
            }
            catch (System.Exception ex)
            {
                IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        _uiExtender = UIExtender.Create("TAOM");
        _uiExtender.Register(typeof(SubModule).Assembly);
        _uiExtender.Enable();

        // Patch41_McmLayoutFix — flip MCM's embedded options-screen prefabs from VerticalBottomToTop
        // to VerticalTopToBottom (v1.4.0 layout regression). MCM's prefabs are embedded in
        // Bannerlord.MBOptionScreen and load via WidgetFactoryManager.CreateAndRegister, which bypasses
        // UIExtenderEx's [PrefabExtension] hook — so this is a Harmony Postfix, not a PrefabExtension.
        // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
        // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
        // must already be attached when MCM calls CreateAndRegister.
        _harmony.PatchCategory("Patch41_McmLayoutFix");

        _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();

        // Must be first — intercepts GetLocalizedText before any game texts are resolved.
        // Loads English string overrides from taom_module_strings.xml (removes hardcoded "The" articles).
        _harmony.PatchCategory("Patch25_LocalizationOverride");
        var pathService0 = IoC.Resolve<IPathService>();
        var logger0 = IoC.Resolve<IModLogger>();
        var xmlPath = System.IO.Path.Combine(pathService0.ModuleDataPath, "taom_module_strings.xml");
        try
        {
            var overrides = LocalizationOverrideLoader.ParseOverridesFromFile(xmlPath);
            foreach (var kvp in overrides)
                MBTextManager_GetLocalizedText_Patch.RegisterOverride(kvp.Key, kvp.Value);
            logger0.LogInfo($"[LocalizationOverride] Registered {overrides.Count} English string overrides");
        }
        catch (System.Exception ex)
        {
            logger0.LogError($"[LocalizationOverride] Failed to load overrides: {ex.Message}");
        }

        _harmony.PatchCategory("Patch18_CulturalFeats");
        _harmony.PatchCategory("Patch19_CustomBattles");
        // Battle scenes disabled — custom map not yet ready, will re-enable when TAOM_Map is integrated
        // _harmony.PatchCategory("Patch0_BattleScenes");
        // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first

        var pathService = IoC.Resolve<IPathService>();
        var logger = IoC.Resolve<IModLogger>();
        FactionMapPaths.Initialize(pathService.ModuleRootPath, logger);

        var allianceHook = IoC.Resolve<IOnAllianceAction>();
        var peaceHook = IoC.Resolve<IOnPeaceAction>();
        DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
        AllianceCampaignBehavior_EndAlliance_Patch.Initialize(logger);
        AllianceCampaignBehavior_AddAllianceDecision_Patch.Initialize(logger);
        DeclareWarAction_ApplyInternal_Patch.Initialize(logger);
        MakePeaceAction_ApplyInternal_Patch.Initialize(logger);

        var executionHook = IoC.Resolve<IOnExecutionAction>();
        ExecutionIoC.InitializeHooks(executionHook);

        TroopWeightIoC.InitializeHooks(
            IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
            IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),
            IoC.Resolve<IOnRecruitmentVMRefreshPartyProperties>(),
            IoC.Resolve<IOnPartyVMPopulatePartyListLabel>());

        CustomBattlesIoC.InitializeHooks(
            IoC.Resolve<IOnGetCustomBattleCommanders>(),
            IoC.Resolve<IOnGetCustomBattleFactions>(),
            IoC.Resolve<IOnGetDefaultTroopOfFormation>(),
            IoC.Resolve<ISideCommanderFilter>(),
            logger);

        _harmony.PatchCategory("Patch21_ShaderPrecompilation");
        ShaderPrecompilationIoC.InitializeHooks(logger);

        _harmony.PatchCategory("Patch22_ArmyTargeting");
        _harmony.PatchCategory("Patch30_MixedFormations");
        // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
        // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in
        // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
        // which is null during OnSubModuleLoad and would crash JIT prep with NRE.

        var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
        var bannerColorService = IoC.Resolve<IBannerColorService>();
        var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();

        Banner_TryGetBannerDataFromCode_Transpiler.Initialize(bannerColorConfig, logger);
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(bannerColorService);
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(logger);
        Clan_UpdateBannerColor_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        Banner_GetFirstIconColor_Patch.Initialize(bannerColorService);
        BannerEditorView_OnTick_Patch.Initialize(bannerColorService, logger);
        CampaignUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        SandBoxUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        PartyVM_RefreshCurrentCharacterInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        HeroViewModel_FillFrom_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        PartyCharacterVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        ClanPartyItemVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Initialize(bannerColorService);
        var agentColorStore = IoC.Resolve<IAgentColorStore>();
        Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
        Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
        AgentVisuals_Create_Patch.Initialize(bannerColorService);
        MapConversationTableau_SpawnOpponentLeader_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        MapConversationTableau_SpawnOpponentBodyguard_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        OrderOfBattleHeroItemVM_RefreshInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);

        Mission_Initialize_Patch.Initialize(logger);

        InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();
        IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();

        // NativeSkinFixes — three native MinHook detours that fix engine bugs
        // TaleWorlds won't: covers_head morph freeze, hair cloth orphan, beard
        // cloth orphan. Loads TAOM.NativeSkinFixes.dll from Main/_Module/bin
        // and pattern-scans TaleWorlds.Native.dll for the hook targets at
        // install time. Failure is logged and the game continues vanilla — no
        // crash, no NRE. See docs/features/native-skin-fixes.md.
        NativeSkinFixesInstaller.Install(IoC.Resolve<IModLogger>());

        // DISABLED 2026-05-22: Pre-compile Shaders main-menu button hidden — feature isn't 100% reliable yet.
        // The service, IoC registration, Harmony Patch21_ShaderPrecompilation, and the OnApplicationTick
        // in-game progress reporter (which uses _shaderTickAccumulator / _lastShaderCount) all remain
        // wired up — only this menu entry is hidden. Re-enable by removing the surrounding block-comment.
        /*
        if (Module.CurrentModule.GetInitialStateOptionWithId("TaomPrecompileShaders") == null)
        {
            var shaderService = IoC.Resolve<IShaderPrecompilationService>();
            var shaderLogger = IoC.Resolve<IModLogger>();
            Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
                id:                  "TaomPrecompileShaders",
                name:                new TextObject("{=taom_precompile_shaders}Pre-compile Shaders"),
                orderIndex:          100,
                action:              () => InformationManager.ShowInquiry(new InquiryData(
                    "Shader Pre-compilation",
                    "This will load a battle scene with all TAOM troops to pre-compile shaders.\n\n" +
                    "THIS WILL TAKE A LONG TIME (20-70 minutes).\n\n" +
                    "This is a one-time process that eliminates in-game stutter and reduces crashes.\n" +
                    "When you see the deployment phase, the process is complete!",
                    true, true, "Start", "Cancel",
                    () =>
                    {
                        _shaderTickAccumulator = 0f;
                        _lastShaderCount = -1;
                        MBGameManager.StartNewGame(new TaomShaderGameManager(shaderService, shaderLogger));
                    },
                    () => InformationManager.HideInquiry())),
                isDisabledAndReason: () => (false, new TextObject("")),
                enabledHint:         new TextObject("{=taom_precompile_hint}Pre-compiles shaders to eliminate in-game stutter. Run once after installing TAOM."),
                isHidden:            null));
        }
        */
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        // Session-level diagnostic snapshot: OS / CLR / mod list / mod-stack
        // assembly versions / campaign context. Runs once per session and is
        // idempotent so OnGameStart on save-load doesn't spam.
        try
        {
            IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>()?.LogSessionSnapshot();
        }
        catch { /* diagnostic is best-effort, never break OnGameStart */ }

        if (gameStarterObject is CampaignGameStarter campaignStarter)
        {
            var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
            campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));

            var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
            var bannerExclusionService = IoC.Resolve<IBannerExclusionService>();
            campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService, bannerExclusionService));

            var ccContentService = IoC.Resolve<ICharacterCreationContentService>();
            var ccLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));

            campaignStarter.RemoveBehaviors<InitialChildGenerationCampaignBehavior>();
            var childGenService = IoC.Resolve<IInitialChildGenerationService>();
            campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));

            var costService = IoC.Resolve<ITroopCostService>();
            // Phase 9b #173 — careerPassives resolved once for the whole CulturalFeats + CareerSystem
            // + TroopProgression model registration block. Replaces all CareerPassiveHelper static
            // calls with instance-injected ICareerPassiveService.
            var careerPassives = IoC.Resolve<TAOM.Features.CareerSystem.ICareerPassiveService>();
            // Phase 9b #180 / partial #148 — IWageModifierService extraction. Hoists garrison-wage
            // feat loop + Mordor/Gundabad/Umbar party-wage feats + Rohan mounted-wage scaling +
            // recruitment-cost feats out of the model body, satisfying gamemodels.md rule 4.
            var wageModifiers = IoC.Resolve<IWageModifierService>();
            var volunteerService = IoC.Resolve<IVolunteerTierService>();
            var recruitmentService = IoC.Resolve<IVolunteerRecruitmentService>();
            var volunteerContextAdapter = IoC.Resolve<IVolunteerContextAdapter>();
            campaignStarter.AddModel(new TaomCharacterStatsModel());
            campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
            campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter));

            var raceAgeService = IoC.Resolve<IRaceAgeService>();
            var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
            var raceAgeLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger));
            campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
            campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
            campaignStarter.AddModel(new TaomHeroCreationModel());

            var diplomacyService = IoC.Resolve<IDiplomacyService>();
            var wotrService = IoC.Resolve<IWarOfTheRingService>();
            var diplomacyLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger));
            campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
            campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
            campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));

            var wotrLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));

            var siegeDefenseService = IoC.Resolve<ISiegeDefenseService>();
            var siegeDefenseLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger));
            campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));

            var executionRelationService = IoC.Resolve<IExecutionRelationService>();
            var playerContext = IoC.Resolve<IPlayerContextAdapter>();
            campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));

            // Cultural feat models — Phase 9b #144/#176: dispatch logic extracted to
            // ICulturalFeatsService. Each model is now a thin boundary that converts
            // CultureObject → ICultureFeatAdapter and delegates (gamemodels.md rule 4).
            var culturalFeats = IoC.Resolve<TAOM.Features.CulturalFeats.ICulturalFeatsService>();
            campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
            campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
            campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
            campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
            campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
            campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
            campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
            campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
            campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
            campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
            campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
            campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
            campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
            campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
            campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
            campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
            campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
            campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));

            // Battle balance models
            var battleBalanceSettings = IoC.Resolve<IBattleBalanceSettingsProvider>();
            var battleBalanceConfig = IoC.Resolve<IBattleBalanceConfigProvider>();
            campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
            campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
            campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig));

            campaignStarter.AddModel(new TaomInformationRestrictionModel(IoC.Resolve<IEncyclopediaSettingsProvider>()));

            var armyTargetingService = IoC.Resolve<IArmyTargetingService>();
            campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));

            var specialResourceService = IoC.Resolve<ISpecialResourceService>();
            var specialResourceStorage = IoC.Resolve<ISpecialResourceStorageService>();
            var specialResourceConfig = IoC.Resolve<ISpecialResourceConfigProvider>();
            var specialResourceLogger = IoC.Resolve<IModLogger>();
            var specialResourceBehavior = new SpecialResourcesBehavior(
                specialResourceService, specialResourceStorage, specialResourceConfig, specialResourceLogger);
            campaignStarter.AddBehavior(specialResourceBehavior);
            PartyScreenLogic_AddCommand_Patch.SetBehavior(specialResourceBehavior);

            var careerDataService = IoC.Resolve<ICareerDataService>();
            var careerRegistry = IoC.Resolve<ICareerRegistry>();
            var careerPassiveService = IoC.Resolve<ICareerPassiveService>();
            var careerLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));
            var careerCreationHandler = IoC.Resolve<ICareerCreationHandler>();
            var careerAbilityServiceForBehavior = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
            campaignStarter.AddBehavior(new CareerCampaignBehavior(
                careerDataService, careerRegistry, careerPassiveService, careerCreationHandler, careerAbilityServiceForBehavior, careerLogger));

            var careerSwitchService = IoC.Resolve<ICareerSwitchService>();
            var careerAdapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
            campaignStarter.AddBehavior(new CareerSwitchDialogueBehavior(
                careerDataService, careerRegistry, careerSwitchService, careerAdapterFactory, careerLogger));

            // Career system GameModels — reuse careerPassiveService resolved above (line 334).
            // Phase 9b #142 — agent-stat extraction: TaomAgentStatCalculateModel /
            // TaomAgentApplyDamageModel now delegate UpdateAgentStats + damage-amp/red +
            // shrug-off logic to ICareerAgentStatService (gamemodels.md rule 4).
            var careerAgentStat = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>();
            campaignStarter.AddModel(new TaomMapVisibilityModel(careerPassives));
            campaignStarter.AddModel(new TaomInventoryCapacityModel(careerPassives));
            campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerPassiveService, careerAgentStat));
            campaignStarter.AddModel<AgentApplyDamageModel>(new TaomAgentApplyDamageModel(careerAgentStat));
            campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));

            var goldService = IoC.Resolve<IStartupGoldService>();
            var influenceService = IoC.Resolve<IStartupInfluenceService>();
            var startupLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));

            var namedCompanionService = IoC.Resolve<INamedCompanionService>();
            campaignStarter.AddBehavior(new NamedCompanionBehavior(namedCompanionService));

            // QuickActions: per-save inventory-search-box persistence (SyncData round-trips
            // even when EnableInventorySearch is OFF — disabled = inert, not absent).
            campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.QuickActions.Hooks.InventorySearchCampaignBehavior>());

            // EquipPresets: per-save preset persistence + orphan pruning. Unconditional registration
            // so the SyncData round-trip preserves presets even when EnableEquipmentPresets is OFF
            // (the MCM hint promises "existing presets are inert (preserved in save)").
            campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.EquipPresets.Hooks.EquipmentPresetCampaignBehavior>());

            // FiefManagement (Patch36) — register UNCONDITIONALLY so the menu is always present
            // and the EnableFiefManagement MCM toggle takes effect immediately at runtime.
            campaignStarter.AddBehavior(new FiefHubCampaignBehavior(
                IoC.Resolve<IFiefHubMenuPresenter>(),
                IoC.Resolve<IFiefManagementSettingsProvider>()));

            // CompanionTactics (Patch35) — FormationPresets persistence behavior. Registered
            // unconditionally so SyncData round-trips even when EnableFormationPresets is OFF.
            campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(
                IoC.Resolve<Features.CompanionTactics.FormationPresets.IFormationPresetService>(),
                IoC.Resolve<IModLogger>()));

            // Messengers — paid messenger dispatch + dialog hooks + per-save SyncData persistence.
            // Registered unconditionally so saves round-trip pending messengers even when
            // EnableMessengers is OFF (disabled = inert, not absent).
            campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>());

            // CultureMarketplace (#207) — daily injection of LOTRLOME items into town markets
            // keyed by owner culture. No SyncData (stock lives in vanilla Settlement.ItemRoster).
            campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
                IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
                IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
                IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
                IoC.Resolve<ITownRosterAdapter>(),
                IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
                IoC.Resolve<IModLogger>()));
        }
    }

    public override void OnGameInitializationFinished(Game game)
    {
        base.OnGameInitializationFinished(game);

        _harmony.PatchCategory("Patch1_FirstTimeInit");
        _harmony.PatchCategory("Patch2_RefreshTableau");
        _harmony.PatchCategory("Patch3_SetRace");
        _harmony.PatchCategory("Patch4_CharacterSpawner");
        _harmony.PatchCategory("Patch5_FaceGen");
        _harmony.PatchCategory("Late_Transpiler");
        _harmony.PatchCategory("Late_ActionSetOverride");
        _harmony.PatchCategory("Patch6_BannerEditor");
        _harmony.PatchCategory("Patch7_FactionMap");
        _harmony.PatchCategory("Patch9_RaceFilter");
        _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
        _harmony.PatchCategory("Patch8_SiegeCampGuard");
        _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
        _harmony.PatchCategory("Patch11_Diplomacy");
        _harmony.PatchCategory("Patch12_WarOfTheRing");

        _harmony.PatchCategory("Patch14_Execution");
        _harmony.PatchCategory("Patch15_BannerLayerLimit");
        _harmony.PatchCategory("Patch16_AtmospherePersistence");
        _harmony.PatchCategory("Patch17_TroopWeight");
        _harmony.PatchCategory("Patch23_BannerColorPersistence");
        _harmony.PatchCategory("Patch24_BannerDriftGuard");
        _harmony.PatchCategory("Patch39_BanditPartySize");
        _harmony.PatchCategory("Patch40_HideoutDescription");

        var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();
        var specResLogger = IoC.Resolve<IModLogger>();
        PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
        PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
        PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
        _harmony.PatchCategory("Patch26_SpecialResources");
        _harmony.PatchCategory("Patch27_CareerSystem");
        _harmony.PatchCategory("Patch29_CCBodyProperties");
        _harmony.PatchCategory("Patch33_EquipPresets");
        _harmony.PatchCategory("Patch34_QuickActions");
        _harmony.PatchCategory("Patch35_CompanionTactics");
        _harmony.PatchCategory("Patch36_FiefManagement");
        SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
        _harmony.PatchCategory("Patch38_SettlementNameplateFade");

        // CompanionTactics — manual patch for the PRIVATE method
        // OrderOfBattleHeroItemVM.GetCaptainTooltip (private in v1.3.15, can't use
        // [HarmonyPatch] attribute binding).
        var captainTooltipTarget = AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip");
        if (captainTooltipTarget != null)
            _harmony.Patch(captainTooltipTarget, postfix: new HarmonyMethod(
                typeof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip),
                nameof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[CompanionTactics] OrderOfBattleHeroItemVM.GetCaptainTooltip not found — captain tooltip role hint will not appear");

        var settlementGuardService = IoC.Resolve<ISettlementGuardService>();
        GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(settlementGuardService);
        GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(settlementGuardService);

        // Manual patches for private GuardsCampaignBehavior methods (SandBox.dll)
        var takeGuardTarget = GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod();
        if (takeGuardTarget != null)
            _harmony.Patch(takeGuardTarget, prefix: new HarmonyMethod(
                typeof(GuardsCampaignBehavior_TakeGuardAgentData_Patch),
                nameof(GuardsCampaignBehavior_TakeGuardAgentData_Patch.Prefix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] TakeGuardAgentDataFromGarrisonTroopList not found — custom guards will not apply");

        var spearTarget = GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod();
        if (spearTarget != null)
            _harmony.Patch(spearTarget, prefix: new HarmonyMethod(
                typeof(GuardsCampaignBehavior_GetSuitableSpear_Patch),
                nameof(GuardsCampaignBehavior_GetSuitableSpear_Patch.Prefix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] GetSuitableSpear not found — culture-specific spears will not apply");

        // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
        var mobilePartyTarget = MobilePartyVisual_AddCharacterToPartyIcon_Patch.TargetMethod();
        if (mobilePartyTarget != null)
            _harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod(
                typeof(MobilePartyVisual_AddCharacterToPartyIcon_Patch),
                nameof(MobilePartyVisual_AddCharacterToPartyIcon_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MobilePartyVisual.AddCharacterToPartyIcon not found — party icon colors will not persist");

        // Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)
        var agentVisualsCreateTarget = AgentVisuals_Create_Patch.TargetMethod();
        if (agentVisualsCreateTarget != null)
            _harmony.Patch(agentVisualsCreateTarget, prefix: new HarmonyMethod(
                typeof(AgentVisuals_Create_Patch),
                nameof(AgentVisuals_Create_Patch.Prefix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] AgentVisuals.Create not found — clan color randomness suppression will not apply");

        // Manual patches for MapConversationTableau (private methods in SandBox.View.dll)
        var leaderTarget = MapConversationTableau_SpawnOpponentLeader_Patch.TargetMethod();
        if (leaderTarget != null)
            _harmony.Patch(leaderTarget, postfix: new HarmonyMethod(
                typeof(MapConversationTableau_SpawnOpponentLeader_Patch),
                nameof(MapConversationTableau_SpawnOpponentLeader_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentLeader not found — conversation tableau leader colors will not apply");

        var bodyguardTarget = MapConversationTableau_SpawnOpponentBodyguard_Patch.TargetMethod();
        if (bodyguardTarget != null)
            _harmony.Patch(bodyguardTarget, postfix: new HarmonyMethod(
                typeof(MapConversationTableau_SpawnOpponentBodyguard_Patch),
                nameof(MapConversationTableau_SpawnOpponentBodyguard_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentBodyguardCharacter not found — conversation tableau bodyguard colors will not apply");
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);

        // Apply Formation.SetMovementOrder patches (Patch31_SmartCavalryAI + Patch35
        // CancelStanceOnMove) only once Mission.Current is non-null — MovementOrder's
        // type initializer constructs static fields whose ctor reads
        // Mission.Current.CurrentTime. Applying earlier crashes JIT prep with NRE.
        if (!_missionTimePatchesApplied)
        {
            _missionTimePatchesApplied = true;
            _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
        }

        mission.AddMissionBehavior(new AdvancedCombatBehavior());
        mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
        mission.AddMissionBehavior(new AutonomousMovementPlayerController());
        mission.AddMissionBehavior(new WargMissionBehavior());
        // DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.
        // mission.AddMissionBehavior(new SpiderMissionBehavior());
        mission.AddMissionBehavior(new SiegeDismountMissionBehavior());
        mission.AddMissionBehavior(new MixedFormationsMissionBehavior());
        mission.AddMissionBehavior(new SmartCavalryAIMissionBehavior());
        mission.AddMissionBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());

        var colorStore = IoC.Resolve<IAgentColorStore>();
        if (colorStore != null)
            mission.AddMissionBehavior(new AgentColorStoreCleanupBehavior(colorStore));

        // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
        // every other mod in the load chain. Dumps MissionBehaviors + MissionLogics
        // on first OnMissionTick to taom_debug_*.log so user-uploaded crash logs
        // contain enough data to identify mod-conflict bugs (BehaviorType=Logic +
        // !MissionLogic null-cast offenders) and action-set anomalies.
        var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
        var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
        var diagLogger = IoC.Resolve<IModLogger>();
        if (diagSvc != null && raceMgr != null && diagLogger != null)
            mission.AddMissionBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));

        // Dev-trigger behavior watches the CrashReport MCM toggle and throws a tagged
        // TaomDevTriggerException on the next OnMissionTick when the player flips
        // "Throw On Next Mission Tick". QA only — no-op in normal play.
        mission.AddMissionBehavior(new Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior());

        var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
        if (careerAbilityService != null && Campaign.Current != null)
        {
            mission.AddMissionBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
                IoC.Resolve<ICareerDataService>(),
                IoC.Resolve<ICareerRegistry>(),
                careerAbilityService,
                IoC.Resolve<ICareerConfigProvider>(),
                IoC.Resolve<Features.CareerSystem.Abilities.CareerAbilityEffectRegistry>(),
                IoC.Resolve<Features.CareerSystem.Mutations.IMutationService>(),
                IoC.Resolve<ICareerHeroAdapterFactory>(),
                IoC.Resolve<IModLogger>()));
        }
    }

    protected override void OnApplicationTick(float dt)
    {
        _timeAccelerationService?.OnTick();

        _shaderTickAccumulator += dt;
        if (_shaderTickAccumulator >= 1f)
        {
            _shaderTickAccumulator = 0f;

            if (!LoadingWindow.IsLoadingWindowActive)
            {
                int count = Utilities.GetNumberOfShaderCompilationsInProgress();
                if (count > 0 && count != _lastShaderCount)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Shader compilation in progress. Remaining: {count}"));
                }
                _lastShaderCount = count;
            }
        }
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        // Detach the AppDomain.UnhandledException subscription BEFORE IoC disposal so
        // the hook doesn't hold a stale reference to a disposed CrashReportService
        // across game-restart-in-same-process. Deep-review INC 3 (2026-05-25).
        try { IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>()?.Unsubscribe(); }
        catch { /* IoC may already be torn down — best-effort */ }

        // Reverse NativeSkinFixes hooks so DLL unload during reload-in-same-process
        // doesn't leave dangling MinHook trampolines. Best-effort — swallows.
        try { NativeSkinFixesInstaller.Uninstall(); }
        catch { /* shutdown — never block */ }

        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();

        // Codex review #46 (2026-05-25) HIGH-01: clear the static service cache in
        // the patch helper so the next module load resolves a fresh service graph from
        // the new IoC container. Without this, Finalizers fire against a disposed
        // FileLogger after reload and silently drop every log line.
        TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
    }
}
