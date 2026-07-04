using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Features.CaravanTrade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace TAOM.Features.CaravanTrade.Hooks;

/// <summary>
/// Lever 1 (highest-impact for TAOM): lift the vanilla caravan war veto so caravans can range beyond
/// their own faction's clustered towns in the endless Free-vs-Evil war. Vanilla <c>CanTradeWith</c>
/// returns <c>false</c> for any at-war pairing; this one method feeds BOTH the destination filter
/// (<c>FindNextDestinationForCaravan</c>) and the mid-route abandon (<c>HourlyTickParty</c>), so a
/// single postfix covers both.
///
/// The postfix only ever flips a war-caused <c>false</c> to <c>true</c> (per the configured policy):
/// a <c>false</c> when NOT at war is the player's prohibited-kingdom exclusion, which we respect, and
/// the player's exclusion is honored even during war (vanilla short-circuits it on the war check).
/// </summary>
[HarmonyPatch(typeof(CaravansCampaignBehavior), "CanTradeWith")]
[HarmonyPatchCategory("Patch59_CaravanTrade")]
public static class CaravansCampaignBehavior_CanTradeWith_Patch
{
    private static ICaravanTradeService _service;
    private static System.Reflection.FieldInfo _prohibitedField;
    private static bool _prohibitedFieldResolved;

    [HarmonyPostfix]
    public static void Postfix(ref bool __result, IFaction caravanFaction, IFaction targetFaction, CaravansCampaignBehavior __instance)
    {
        if (__result) return; // already allowed — never re-block
        if (caravanFaction == null || targetFaction == null) return;

        try
        {
            // A false when NOT at war is the player's prohibited-kingdom exclusion — respect it, don't lift.
            if (!caravanFaction.IsAtWarWith(targetFaction)) return;

            bool isPlayer = Hero.MainHero != null && caravanFaction == Hero.MainHero.MapFaction;

            // Honor the player's explicit prohibited-kingdom list even during war (vanilla only checks it at peace).
            if (isPlayer && IsProhibitedForPlayer(__instance, targetFaction)) return;

            _service ??= IoC.Resolve<ICaravanTradeService>();
            if (_service.AllowWartimeTrade(
                    caravanFaction.StringId, caravanFaction.Culture?.StringId,
                    targetFaction.StringId, targetFaction.Culture?.StringId, isPlayer))
                __result = true;
        }
        catch (Exception)
        {
            // IoC not initialized / feature not started — degrade gracefully to vanilla.
        }
    }

    private static bool IsProhibitedForPlayer(CaravansCampaignBehavior instance, IFaction targetFaction)
    {
        if (!(targetFaction is Kingdom kingdom)) return false;

        if (!_prohibitedFieldResolved)
        {
            _prohibitedField = AccessTools.Field(typeof(CaravansCampaignBehavior), "_prohibitedKingdomsForPlayerCaravans");
            _prohibitedFieldResolved = true;
        }

        return _prohibitedField?.GetValue(instance) is List<Kingdom> list && list.Contains(kingdom);
    }
}
