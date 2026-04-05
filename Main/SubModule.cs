using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
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
using TAOM.Features.BattleBalance;
using TAOM.Features.BattleBalance.Models;
using TAOM.Features.Arena.Models;
using TAOM.Features.Encyclopedia.Models;
using TAOM.Features.MainMenuCustomizer;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.Siege;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.ArmyTargeting.Models;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;
using BehaviorTreeWrapper;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM;

public class SubModule : MBSubModuleBase
{
    private Harmony _harmony;
    private UIExtender? _uiExtender;
    private ITimeAccelerationService? _timeAccelerationService;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        IoC.Configure();

        _uiExtender = UIExtender.Create("TAOM");
        _uiExtender.Register(typeof(SubModule).Assembly);
        _uiExtender.Enable();

        _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();

        _harmony = new Harmony("com.taom.mod");
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
            logger);

        _harmony.PatchCategory("Patch21_ShaderPrecompilation");
        ShaderPrecompilationIoC.InitializeHooks(logger);

        _harmony.PatchCategory("Patch22_ArmyTargeting");

        var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
        var bannerColorService = IoC.Resolve<IBannerColorService>();
        var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();

        Banner_TryGetBannerDataFromCode_Transpiler.Initialize(bannerColorConfig, logger);
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(bannerColorService);
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
        Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter);

        Mission_Initialize_Patch.Initialize(logger);

        InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();
        IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();

        if (Module.CurrentModule.GetInitialStateOptionWithId("TaomPrecompileShaders") == null)
        {
            var shaderService = IoC.Resolve<IShaderPrecompilationService>();
            var shaderLogger = IoC.Resolve<IModLogger>();
            Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
                id:                  "TaomPrecompileShaders",
                name:                new TextObject("{=taom_precompile_shaders}Pre-compile Shaders"),
                orderIndex:          100,
                action:              () => MBGameManager.StartNewGame(new TaomShaderGameManager(shaderService, shaderLogger)),
                isDisabledAndReason: () => (false, new TextObject("")),
                enabledHint:         new TextObject("{=taom_precompile_hint}Pre-compiles shaders to eliminate in-game stutter. Run once after installing TAOM."),
                isHidden:            null));
        }
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        if (gameStarterObject is CampaignGameStarter campaignStarter)
        {
            var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
            campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));

            var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
            campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService));

            var ccContentService = IoC.Resolve<ICharacterCreationContentService>();
            var ccLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));

            campaignStarter.RemoveBehaviors<InitialChildGenerationCampaignBehavior>();
            var childGenService = IoC.Resolve<IInitialChildGenerationService>();
            campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));

            var costService = IoC.Resolve<ITroopCostService>();
            var volunteerService = IoC.Resolve<IVolunteerTierService>();
            var recruitmentService = IoC.Resolve<IVolunteerRecruitmentService>();
            var volunteerContextAdapter = IoC.Resolve<IVolunteerContextAdapter>();
            campaignStarter.AddModel(new TaomCharacterStatsModel());
            campaignStarter.AddModel(new TaomPartyWageModel(costService));
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

            var executionAction = IoC.Resolve<IOnExecutionAction>();
            campaignStarter.AddModel(new TaomExecutionRelationModel(executionAction));

            // Cultural feat models
            campaignStarter.AddModel(new TaomArmyManagementModel());
            campaignStarter.AddModel(new TaomPartySpeedModel());
            campaignStarter.AddModel(new TaomSettlementProsperityModel());
            campaignStarter.AddModel(new TaomSettlementMilitiaModel());
            campaignStarter.AddModel(new TaomBuildingConstructionModel());
            campaignStarter.AddModel(new TaomVillageProductionModel());
            campaignStarter.AddModel(new TaomCaravanModel());
            campaignStarter.AddModel(new TaomBattleRewardModel());
            campaignStarter.AddModel(new TaomTournamentModel());
            campaignStarter.AddModel(new TaomPartyTroopUpgradeModel());
            campaignStarter.AddModel(new TaomPartySizeModel());
            campaignStarter.AddModel(new TaomFoodConsumptionModel());
            campaignStarter.AddModel(new TaomSettlementLoyaltyModel());
            campaignStarter.AddModel(new TaomPartyMoraleModel());
            campaignStarter.AddModel(new TaomSmithingModel());
            campaignStarter.AddModel(new TaomClanFinanceModel());
            campaignStarter.AddModel(new TaomRaidModel());

            // Battle balance models
            var battleBalanceSettings = IoC.Resolve<IBattleBalanceSettingsProvider>();
            var battleBalanceConfig = IoC.Resolve<IBattleBalanceConfigProvider>();
            campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
            campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
            campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig));

            campaignStarter.AddModel(new TaomInformationRestrictionModel());

            var armyTargetingService = IoC.Resolve<IArmyTargetingService>();
            campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));

            var goldService = IoC.Resolve<IStartupGoldService>();
            var influenceService = IoC.Resolve<IStartupInfluenceService>();
            var startupLogger = IoC.Resolve<IModLogger>();
            campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));
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

        // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
        var mobilePartyTarget = MobilePartyVisual_AddCharacterToPartyIcon_Patch.TargetMethod();
        if (mobilePartyTarget != null)
            _harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod(
                typeof(MobilePartyVisual_AddCharacterToPartyIcon_Patch),
                nameof(MobilePartyVisual_AddCharacterToPartyIcon_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MobilePartyVisual.AddCharacterToPartyIcon not found — party icon colors will not persist");
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        mission.AddMissionBehavior(new AdvancedCombatBehavior());
        mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
        mission.AddMissionBehavior(new AutonomousMovementPlayerController());
        mission.AddMissionBehavior(new WargMissionBehavior());
    }

    protected override void OnApplicationTick(float dt)
    {
        _timeAccelerationService?.OnTick();
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();
    }
}
