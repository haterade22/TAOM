using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM;

public class SubModule : MBSubModuleBase
{
    private Harmony _harmony;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        IoC.Configure();

        _harmony = new Harmony("com.taom.mod");
        _harmony.PatchAll();

        InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        // Register campaign behaviors and game models here
    }

    public override void OnGameInitializationFinished(Game game)
    {
        base.OnGameInitializationFinished(game);
        // Post-initialization logic here
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();
    }
}
