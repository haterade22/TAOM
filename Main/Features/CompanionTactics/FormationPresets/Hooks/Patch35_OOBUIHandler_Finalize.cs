using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Postfix on <c>MissionGauntletOrderOfBattleUIHandler.OnMissionScreenFinalize</c>. Detaches
/// the overlay layer cleanly when the OOB UI shuts down.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletOrderOfBattleUIHandler), "OnMissionScreenFinalize")]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
public static class Patch35_OOBUIHandler_Finalize
{
    private static IOOBOverlayService _overlay;

    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {
            _overlay ??= IoC.Resolve<IOOBOverlayService>();
            _overlay?.Detach();
        }
        catch { }
    }
}
