using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Hooks;

[HarmonyPatch(typeof(AiMilitaryBehavior), "CalculateDistanceScoreForBesieging")]
[HarmonyPatchCategory("Patch22_ArmyTargeting")]
public static class AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch
{
    // Phase 9b #161 — cache the three IoC.Resolve calls instead of resolving per invocation.
    // This patch fires per-army-per-AI-tick (~500-2000 calls/cycle per feature doc). Each
    // resolve acquires a DryIoc lock and walks the registration table — non-trivial cost at
    // that frequency. Lazy `??=` cache resolves once per process. Class is also `static` per
    // Harmony 2 convention (#151 pattern).
    private static IArmyTargetingService _service;
    private static IArmyTargetingSettingsProvider _settings;
    private static IModLogger _logger;

    [HarmonyPostfix]
    public static void Postfix(
        Settlement targetSettlement,
        MobileParty mobileParty,
        ref float bestDistanceScore)
    {
        if (bestDistanceScore > 0f) return;

        try
        {
            _service  ??= IoC.Resolve<IArmyTargetingService>();
            _settings ??= IoC.Resolve<IArmyTargetingSettingsProvider>();

            if (!_settings.EnableArmyStrategicIntelligence) return;

            float floor = _settings.BorderProximityFloor;
            if (floor <= 0f) return;

            string factionId    = mobileParty?.MapFaction?.StringId;
            string settlementId = targetSettlement?.StringId;

            if (_service.IsInPriorityList(factionId, settlementId))
            {
                bestDistanceScore = floor;
                _logger ??= IoC.Resolve<IModLogger>();
                _logger.LogDebug($"ArmyTargeting: border proximity floor {floor:F2} applied for {factionId}→{settlementId}");
            }
        }
        catch (Exception)
        {
            // IoC not initialized or feature not started — degrade gracefully
        }
    }
}
