using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(GameMenuPartyItemVM), nameof(GameMenuPartyItemVM.RefreshCounts))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class GameMenuPartyItemVM_RefreshCounts_Patch
{
    private static IOnGameMenuPartyItemRefreshCounts? _hook;

    public static void Initialize(IOnGameMenuPartyItemRefreshCounts hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(GameMenuPartyItemVM __instance)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnGameMenuPartyItemRefreshCounts(__instance);
    }
}
