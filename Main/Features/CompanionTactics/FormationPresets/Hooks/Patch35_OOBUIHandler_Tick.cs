using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Postfix on <c>MissionGauntletOrderOfBattleUIHandler.OnMissionScreenTick</c>. Delegates
/// to <see cref="IOOBOverlayService"/>, which handles the false→true→false attach/detach
/// cycle for the buttons overlay.
/// </summary>
[HarmonyPatchCategory("Patch35_CompanionTactics")]
[HarmonyPatch(typeof(MissionGauntletOrderOfBattleUIHandler), nameof(MissionGauntletOrderOfBattleUIHandler.OnMissionScreenTick))]
public static class Patch35_OOBUIHandler_Tick
{
    private static IOOBOverlayService _overlay;

    [HarmonyPostfix]
    public static void Postfix(MissionGauntletOrderOfBattleUIHandler __instance)
    {
        try
        {
            _overlay ??= IoC.Resolve<IOOBOverlayService>();
            _overlay?.OnTick(__instance);
        }
        catch { }
    }
}
