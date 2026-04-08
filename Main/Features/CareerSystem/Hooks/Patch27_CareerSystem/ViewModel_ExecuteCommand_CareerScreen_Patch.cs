using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Library;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.CareerSystem.Hooks.Patch27_CareerSystem;

[HarmonyPatch(typeof(ViewModel), "ExecuteCommand")]
[HarmonyPatchCategory("Patch27_CareerSystem")]
public static class ViewModel_ExecuteCommand_CareerScreen_Patch
{
    static void Postfix(ViewModel __instance, string commandName, object[] parameters)
    {
        if (commandName == "ExecuteOpenCareerScreen" && __instance is CharacterDeveloperVM)
        {
            GauntletCareerScreen.OpenCareerScreen();
        }
    }
}
