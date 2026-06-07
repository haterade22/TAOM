using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetPartyHealthTooltip))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class CampaignUIHelper_GetPartyHealthTooltip_Patch
{
    private static IOnCampaignUIHelperGetPartyHealthTooltip? _hook;

    public static void Initialize(IOnCampaignUIHelperGetPartyHealthTooltip hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyBase party, ref List<TooltipProperty> __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnGetPartyHealthTooltip(party, ref __result);
    }
}
