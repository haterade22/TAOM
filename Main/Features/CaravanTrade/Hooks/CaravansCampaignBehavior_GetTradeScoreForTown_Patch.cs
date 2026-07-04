using System;
using HarmonyLib;
using Helpers;
using TAOM.Features.CaravanTrade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CaravanTrade.Hooks;

/// <summary>
/// Lever 2: re-weight the vanilla caravan destination score so caravans stop shuttling between the
/// nearest two towns and range to the profitable far ones. Vanilla folds a <c>1/days</c> distance
/// spike into the score (a town twice as far scores ~half); this postfix recomputes the raw travel
/// days from the SAME public inputs vanilla used (<c>AiHelper</c> + the caravan speed props), strips
/// that spike, and re-applies a gentler curve via the pure service — plus an anti-shuttle cut on the
/// town just left. Selection-only; profit and payout are untouched. Naval + home pass through.
/// </summary>
[HarmonyPatch(typeof(CaravansCampaignBehavior), "GetTradeScoreForTown")]
[HarmonyPatchCategory("Patch59_CaravanTrade")]
public static class CaravansCampaignBehavior_GetTradeScoreForTown_Patch
{
    private static ICaravanTradeService _service;

    [HarmonyPostfix]
    public static void Postfix(ref float __result, MobileParty caravanParty, Town town)
    {
        // Positive-requirement gate: vanilla rejections (-1) and any NaN pass through untouched.
        if (!(__result > 0f)) return;
        if (caravanParty == null || town?.Settlement == null) return;

        try
        {
            _service ??= IoC.Resolve<ICaravanTradeService>();

            bool isNaval = caravanParty.HasNavalNavigationCapability;
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                caravanParty, town.Settlement, isNaval, out var navType, out var navDistance, out _);
            if (navType == MobileParty.NavigationType.None) return;

            float speed = isNaval
                ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed
                : Campaign.Current.EstimatedAverageCaravanPartySpeed;
            float days = navDistance / (speed * CampaignTime.HoursInDay);

            bool isHome = town.Settlement == caravanParty.HomeSettlement;
            bool isJustLeft = !isHome && town.Settlement == caravanParty.LastVisitedSettlement;
            bool isPlayer = caravanParty.Owner?.Clan == Clan.PlayerClan;

            __result = _service.ReweightTradeScore(__result, days, isNaval, isHome, isJustLeft, isPlayer);
        }
        catch (Exception)
        {
            // Degrade gracefully to the vanilla score.
        }
    }
}
