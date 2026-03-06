using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.BannerInjection;
using TAOM.Features.HeroRace;

namespace TAOM;

public class SubModule : MBSubModuleBase
{
    private Harmony _harmony;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        IoC.Configure();

        _harmony = new Harmony("com.taom.mod");
        // Battle scenes patch must run before Campaign.InitializeScenes (which fires before OnGameStart)
        _harmony.PatchCategory("Patch0_BattleScenes");
        // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first

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
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();
    }
}
