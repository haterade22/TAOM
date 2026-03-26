using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.TotalHealthyCount), MethodType.Getter)]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class TroopRoster_TotalHealthyCount_Patch
{
    private static IOnTroopRosterTotalHealthyCount? _hook;

    public static void Initialize(IOnTroopRosterTotalHealthyCount hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(TroopRoster __instance, ref int __result)
    {
        if (!TaomSettings.Instance.EnableTroopWeight) return;
        _hook?.OnTroopRosterTotalHealthyCount(__instance, ref __result);
    }
}
