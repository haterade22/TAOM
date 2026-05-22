using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Diplomacy.Hooks;

/// <summary>
/// Audit Agent 2 finding (2026-05-22) — companion patch to
/// <see cref="AllianceCampaignBehavior_EndAlliance_Patch"/>.
///
/// When TAOM blocks <c>EndAlliance</c> for a Permanent lore pair, vanilla 1.4.5's
/// <c>AllianceCampaignBehavior.OnAllianceTimerExpired</c> still calls
/// <c>AddAllianceDecision</c> on the next line — vanilla has NO IsAlliedWith gate
/// (despite the stale comment in the EndAlliance Prefix). Without this guard, a
/// duplicate <c>StartAllianceDecision</c> gets queued daily for already-allied pairs
/// until the unresolved-decision queue saturates or a kingdom election re-fires
/// <c>StartAlliance</c> on an already-allied pair (vanilla side-effects undefined).
///
/// Fix: short-circuit <c>AddAllianceDecision</c> when the two kingdoms are already allies.
/// </summary>
[HarmonyPatch(typeof(AllianceCampaignBehavior), "AddAllianceDecision")]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class AllianceCampaignBehavior_AddAllianceDecision_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(Kingdom kingdomToAddDecision, Kingdom kingdomToOffer)
    {
        if (kingdomToAddDecision == null || kingdomToOffer == null) return true;

        // If they're already allies (TAOM blocked an EndAlliance call), don't queue
        // a fresh StartAllianceDecision. Without this, the unresolved-decision queue
        // accumulates duplicate decisions every tick after the alliance's EndTime expires.
        if (kingdomToAddDecision.IsAllyWith(kingdomToOffer))
        {
            _logger?.LogDebug(
                $"[Diplomacy] AddAllianceDecision skipped: {kingdomToAddDecision.StringId} ↔ " +
                $"{kingdomToOffer.StringId} are already allies (likely a TAOM-blocked EndAlliance)");
            return false;
        }
        return true;
    }
}
