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

        // === BISECT: Uncomment one at a time to find horizontal character root cause ===
        _harmony.PatchCategory("Patch1_FirstTimeInit");       // Postfix: CharacterTableau.FirstTimeInit — loads config
        _harmony.PatchCategory("Patch2_RefreshTableau");      // Prefix:  CharacterTableau.RefreshCharacterTableau — race check
        _harmony.PatchCategory("Patch3_SetRace");             // Postfix: CharacterTableau.SetRace — reset + reinit visuals
        _harmony.PatchCategory("Patch4_CharacterSpawner");    // Prefix:  CharacterSpawner.InitWithCharacter — race check
        _harmony.PatchCategory("Patch5_FaceGen");             // Postfix: FaceGen.GetBaseMonsterFromRace — eye height hook
        // _harmony.PatchCategory("Late_Transpiler");             // Transpiler: BodyGeneratorView.RefreshCharacterEntityAux — ROOT CAUSE of horizontal characters
        // _harmony.PatchCategory("Late_EarlyActionSet");         // Prefix:  CharacterTableau.RefreshCharacterTableau — pre-load visuals
        // _harmony.PatchCategory("Late_ActionSetOverride");      // Prefix:  ActionSetCode.GenerateActionSetNameWithSuffix
        // === END BISECT ===

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
