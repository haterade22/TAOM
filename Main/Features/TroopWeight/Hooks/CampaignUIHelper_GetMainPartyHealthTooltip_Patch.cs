using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetMainPartyHealthTooltip))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class CampaignUIHelper_GetMainPartyHealthTooltip_Patch
{
    private static IOnCampaignUIHelperGetMainPartyHealthTooltip? _hook;

    public static void Initialize(IOnCampaignUIHelperGetMainPartyHealthTooltip hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(ref List<TooltipProperty> __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnGetMainPartyHealthTooltip(ref __result);
    }
}
