using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Hooks;

[HarmonyPatch(typeof(AiMilitaryBehavior), "CalculateDistanceScoreForBesieging")]
[HarmonyPatchCategory("Patch22_ArmyTargeting")]
public class AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch
{
    [HarmonyPostfix]
    public static void Postfix(
        Settlement targetSettlement,
        MobileParty mobileParty,
        ref float bestDistanceScore)
    {
        if (bestDistanceScore > 0f) return;

        try
        {
            var service  = IoC.Resolve<IArmyTargetingService>();
            var settings = IoC.Resolve<IArmyTargetingSettingsProvider>();

            if (!settings.EnableArmyStrategicIntelligence) return;

            float floor = settings.BorderProximityFloor;
            if (floor <= 0f) return;

            string factionId    = mobileParty?.MapFaction?.StringId;
            string settlementId = targetSettlement?.StringId;

            if (service.IsInPriorityList(factionId, settlementId))
            {
                bestDistanceScore = floor;
                IoC.Resolve<IModLogger>().LogDebug($"ArmyTargeting: border proximity floor {floor:F2} applied for {factionId}→{settlementId}");
            }
        }
        catch (Exception)
        {
            // IoC not initialized or feature not started — degrade gracefully
        }
    }
}
