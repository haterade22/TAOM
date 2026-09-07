using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.TroopWeight.Hooks;

// Main-party HUD health tooltip: restates ONLY its "Land Troop Capacity" row. The sibling Battle Ready /
// Wounded rows are headcounts and must keep reading raw — weighting them is what manufactured the
// phantom-wounded bug (docs/reviews/rca-troopweight-phantom-wounded-2026-06-07.md).
// Vanilla takes no party argument here; it builds the tooltip for MobileParty.MainParty.
[HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetMainPartyHealthTooltip))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class CampaignUIHelper_GetMainPartyHealthTooltip_Patch
{
    private static IOnCampaignUIHelperGetPartyHealthTooltip? _hook;

    public static void Initialize(IOnCampaignUIHelperGetPartyHealthTooltip hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(ref List<TooltipProperty> __result)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnGetPartyHealthTooltip(PartyBase.MainParty, __result);
    }
}
