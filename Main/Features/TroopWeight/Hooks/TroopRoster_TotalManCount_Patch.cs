using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.TotalManCount), MethodType.Getter)]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class TroopRoster_TotalManCount_Patch
{
    private static IOnTroopRosterTotalManCount? _hook;

    public static void Initialize(IOnTroopRosterTotalManCount hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(TroopRoster __instance, ref int __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnTroopRosterTotalManCount(__instance, ref __result);
    }
}
