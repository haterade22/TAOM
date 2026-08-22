using System;
using HarmonyLib;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Hooks;

[HarmonyPatch(typeof(AiMilitaryBehavior), "CalculateDistanceScoreForBesieging")]
[HarmonyPatchCategory("Patch22_ArmyTargeting")]
public static class AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch
{
    // Phase 9b #161 — cache the IoC.Resolve calls instead of resolving per invocation.
    // This patch fires per-army-per-AI-tick (~500-2000 calls/cycle per feature doc). Each
    // resolve acquires a DryIoc lock and walks the registration table — non-trivial cost at
    // that frequency. Lazy `??=` cache resolves once per process. Class is also `static` per
    // Harmony 2 convention (#151 pattern).
    private static IArmyTargetingService _service;
    private static IArmyTargetingSettingsProvider _settings;
    private static IMapReachAdapter _reach;
    private static IModLogger _logger;

    // One-shot breadcrumbs: the path fires per-army-per-tick, so we only want a single "this is
    // alive" marker for each outcome.
    private static bool _loggedBorderFloor;
    private static bool _loggedReachRefusal;

    // Audit Agent 1 CRITICAL fix 2026-05-22: vanilla 1.4.5 method now has FOUR out params:
    //   private void CalculateDistanceScoreForBesieging(
    //       Settlement targetSettlement,
    //       MobileParty mobileParty,
    //       out MobileParty.NavigationType bestNavigationType,
    //       out float bestDistanceScore,
    //       out bool isFromPort,
    //       out bool isTargetingPort)
    // Harmony binds Postfix parameters positionally-by-name. Without the 3 extra params,
    // the patch fails to bind silently — the entire border-proximity-floor feature was
    // a runtime no-op since the v1.4.0 method-signature change.
    [HarmonyPostfix]
    public static void Postfix(
        Settlement targetSettlement,
        MobileParty mobileParty,
        ref MobileParty.NavigationType bestNavigationType,
        ref float bestDistanceScore,
        ref bool isFromPort,
        ref bool isTargetingPort)
    {
        if (bestDistanceScore > 0f) return;

        try
        {
            _service  ??= IoC.Resolve<IArmyTargetingService>();
            _settings ??= IoC.Resolve<IArmyTargetingSettingsProvider>();
            _reach    ??= IoC.Resolve<IMapReachAdapter>();

            if (!_settings.EnableArmyStrategicIntelligence) return;

            // Positive-requirement gate. The previous form was `floor <= 0f`, which NaN and
            // +Infinity both PASS, and the value is then assigned straight into bestDistanceScore,
            // which the engine multiplies into the final behaviour score. An infinity there makes
            // one settlement dominate every candidate on the map.
            float floor = _settings.BorderProximityFloor;
            if (!(floor > 0f) || !FiniteFloatValidator.IsFinite(floor)) return;

            string factionId    = mobileParty?.MapFaction?.StringId;
            string settlementId = targetSettlement?.StringId;

            if (!_service.IsInPriorityList(factionId, settlementId)) return;

            // The floor exists to rescue a BORDER target vanilla's 2-hop topology scored as
            // unreachable. Before the reach gate it rescued any priority-list entry at any
            // distance, which is what turned the list into an "ignore geography" list and is
            // named in the Patch49 registry entry as the cause of cross-map siege steering.
            float normalizedDistance = _reach.GetNormalizedDistanceToNearestFortification(
                targetSettlement, mobileParty?.MapFaction);

            if (!_service.IsWithinReach(normalizedDistance))
            {
                if (!_loggedReachRefusal)
                {
                    _loggedReachRefusal = true;
                    _logger ??= IoC.Resolve<IModLogger>();
                    _logger.LogDebug($"ArmyTargeting: border floor REFUSED for {factionId} -> {settlementId}, {normalizedDistance:F2} town gaps is out of reach");
                }
                return;
            }

            bestDistanceScore = floor;
            if (!_loggedBorderFloor)
            {
                _loggedBorderFloor = true;
                _logger ??= IoC.Resolve<IModLogger>();
                _logger.LogDebug($"ArmyTargeting: border proximity floor {floor:F2} applied for {factionId} -> {settlementId} at {normalizedDistance:F2} town gaps");
            }
        }
        catch (Exception)
        {
            // IoC not initialized or feature not started — degrade gracefully
        }
    }
}
