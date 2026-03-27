using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(PartyBase), nameof(PartyBase.NumberOfRegularMembers), MethodType.Getter)]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyBase_NumberOfRegularMembers_Patch
{
    private static IOnPartyBaseNumberOfRegularMembers? _hook;

    public static void Initialize(IOnPartyBaseNumberOfRegularMembers hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyBase __instance, ref int __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnPartyBaseNumberOfRegularMembers(__instance, ref __result);
    }
}
