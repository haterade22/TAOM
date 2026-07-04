using System;
using HarmonyLib;
using TAOM.Features.CaravanTrade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CaravanTrade.Hooks;

/// <summary>
/// Lever 4 (direct "caravans only buy one item" fix): floor the vanilla per-caravan budget factor.
/// Vanilla's <c>budgetFactor = 0.1 + clamp(PartyTradeGold/5000, 0, 1)</c> sits at ~0.1 for a poor
/// caravan, so only the single best category clears the private per-category buy-value gate and the
/// caravan buys one good. Raising the floor lets several categories clear the gate even when poor,
/// producing a fuller basket. Delegated to the pure service; player-scoped; vanilla when disabled.
/// </summary>
[HarmonyPatch(typeof(CaravansCampaignBehavior), "CalculateBudgetFactor")]
[HarmonyPatchCategory("Patch59_CaravanTrade")]
public static class CaravansCampaignBehavior_CalculateBudgetFactor_Patch
{
    private static ICaravanTradeService _service;

    [HarmonyPostfix]
    public static void Postfix(ref float __result, MobileParty caravanParty)
    {
        if (caravanParty == null) return;

        try
        {
            _service ??= IoC.Resolve<ICaravanTradeService>();
            bool isPlayer = caravanParty.Owner?.Clan == Clan.PlayerClan;
            __result = _service.ApplyBudgetFactorFloor(__result, isPlayer);
        }
        catch (Exception)
        {
            // Degrade gracefully to the vanilla budget factor.
        }
    }
}
