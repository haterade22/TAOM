using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Features.FiefManagement.Models;
using TAOM.Features.FiefManagement.UI;

namespace TAOM.Features.FiefManagement.Hooks;

[HarmonyPatch(typeof(GameStateScreenManager), "CreateScreen")]
[HarmonyPatchCategory("Patch36_FiefManagement")]
public static class Patch36_GameStateScreenManager
{
    private static IRemoteFiefSettlementSwapper _swapper;

    [HarmonyPrefix]
    public static bool Prefix(GameState state, ref ScreenBase __result)
    {
        if (state is FiefManagementGameState fmState)
        {
            var swapper = _swapper ??= IoC.Resolve<IRemoteFiefSettlementSwapper>();
            __result = new GauntletFiefManagementScreen(fmState, swapper);
            return false;
        }
        return true;
    }
}
