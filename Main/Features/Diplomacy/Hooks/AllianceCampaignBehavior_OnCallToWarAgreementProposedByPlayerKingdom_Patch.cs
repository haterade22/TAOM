using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Diplomacy.Hooks;

// The player's kingdom learns of a call-to-war offer here (an alliance start, or a war on the player's
// kingdom with an ally not yet in it). With a one-clan kingdom vanilla posts a map notice for the offer
// and never reads IsAllowed, so the two ballot patches cannot cancel it; accepting the notice would
// reach StartCallToWarAgreement, which refuses a forbidden war only after the player saw the offer.
// Skipping here for a war the rule refuses keeps the notice off the map. A larger kingdom's ballot is
// skipped too, which is the same outcome as its IsAllowed patches cancelling it. The veto is
// ShouldPreventCallToWar (the war veto, or a callToWarRefusals row, #1283; it defers to the host under co-op).
// Initialize is wired in DiplomacyIoC.InitializeHooks.
[HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.OnCallToWarAgreementProposedByPlayerKingdom))]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class AllianceCampaignBehavior_OnCallToWarAgreementProposedByPlayerKingdom_Patch
{
    private static IOnAllianceAction _hook;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    [HarmonyPrefix]
    public static bool Prefix(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst)
    {
        if (_hook == null || proposedKingdom == null || kingdomToCallToWarAgainst == null)
            return true;

        return !_hook.ShouldPreventCallToWar(proposedKingdom.StringId, kingdomToCallToWarAgainst.StringId);
    }
}
