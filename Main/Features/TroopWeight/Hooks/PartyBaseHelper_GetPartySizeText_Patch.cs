using System;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.TroopWeight.Hooks;

// Targets the PartyBase overload only (there is also GetPartySizeText(int, int, bool)).
[HarmonyPatch(typeof(PartyBaseHelper), nameof(PartyBaseHelper.GetPartySizeText), new[] { typeof(PartyBase) })]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyBaseHelper_GetPartySizeText_Patch
{
    private static IOnPartyBaseHelperGetPartySizeText? _hook;

    public static void Initialize(IOnPartyBaseHelperGetPartySizeText hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyBase party, ref TextObject __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnGetPartySizeText(party, ref __result);
    }
}
