using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.CareerSystem.Hooks.Patch27_CareerSystem;

// The career button's only click handler (#971): UIExtenderEx adds a mixin method to the view model only
// when it carries [DataSourceMethod], so ViewModel.ExecuteCommand finds none and this postfix handles the click.
// The click sound is vanilla's: ExecuteDone runs GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen,
// which plays event:/ui/default.
[HarmonyPatch(typeof(ViewModel), "ExecuteCommand")]
[HarmonyPatchCategory("Patch27_CareerSystem")]
public static class ViewModel_ExecuteCommand_CareerScreen_Patch
{
    static void Postfix(ViewModel __instance, string commandName, object[] parameters)
    {
        if (commandName == "ExecuteOpenCareerScreen" && __instance is CharacterDeveloperVM charDevVM)
        {
            try
            {
                // Close CharacterDeveloper first — prevents
                // GameState.HandleInitialize NullRef and map bar input crash
                charDevVM.ExecuteDone();
                GauntletCareerScreen.OpenCareerScreen();
            }
            catch (Exception ex)
            {
                // Logged, then rethrown: this postfix had no guard, and a click that throws must still surface.
                var reason = CareerScreenChecks.FailReason(ex);
                Outcomes.Fail(CareerScreenChecks.OpenId, reason, CareerScreenChecks.OpenFailDetail(reason));
                throw;
            }

            // OpenCareerScreen pushes the screen before it returns; only the init line proves OnInitialize finished.
            if (ScreenManager.TopScreen is GauntletCareerScreen)
                Outcomes.Pass(CareerScreenChecks.OpenId, "", CareerScreenChecks.OpenPassDetail);
            else
                Outcomes.Fail(CareerScreenChecks.OpenId, CareerScreenChecks.NotOnTop,
                    CareerScreenChecks.OpenFailDetail(CareerScreenChecks.NotOnTop));
        }
    }
}
