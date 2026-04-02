using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
using TAOM.Features.BannerInjection.Hooks;
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
using BehaviorTreeWrapper;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM;

public class SubModule : MBSubModuleBase
{
    private Harmony _harmony;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        IoC.Configure();

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

        Banner_TryGetBannerDataFromCode_Patch.Initialize(logger);
        Mission_Initialize_Patch.Initialize(logger);

        InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();
        IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();
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
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        mission.AddMissionBehavior(new AdvancedCombatBehavior());
        mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
        mission.AddMissionBehavior(new AutonomousMovementPlayerController());
        mission.AddMissionBehavior(new WargMissionBehavior());
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();
    }
}
