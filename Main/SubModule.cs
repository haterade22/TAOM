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
using TAOM.Features.TroopProgression;
using TAOM.Features.TroopProgression.Models;
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
        // Battle scenes disabled — custom map not yet ready, will re-enable when TAOM_Map is integrated
        // _harmony.PatchCategory("Patch0_BattleScenes");
        // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first

        var pathService = IoC.Resolve<IPathService>();
        var logger = IoC.Resolve<IModLogger>();
        FactionMapPaths.Initialize(pathService.ModuleRootPath, logger);

        var allianceHook = IoC.Resolve<IOnAllianceAction>();
        DiplomacyIoC.InitializeHooks(allianceHook);

        InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
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

            var diplomacyService = IoC.Resolve<IDiplomacyService>();
            campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService));
            campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
            campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService));
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
        _harmony.PatchCategory("Patch8_SiegeCampGuard");
        _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
        _harmony.PatchCategory("Patch11_Diplomacy");
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();
    }
}
