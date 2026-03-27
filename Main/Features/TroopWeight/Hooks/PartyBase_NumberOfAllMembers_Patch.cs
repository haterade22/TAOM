using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(PartyBase), nameof(PartyBase.NumberOfAllMembers), MethodType.Getter)]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyBase_NumberOfAllMembers_Patch
{
    private static IOnPartyBaseNumberOfAllMembers? _hook;

    public static void Initialize(IOnPartyBaseNumberOfAllMembers hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyBase __instance, ref int __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnPartyBaseNumberOfAllMembers(__instance, ref __result);
    }
}
