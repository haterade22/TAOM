using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Diplomacy.Hooks;

// DIAGNOSTIC (removable after in-game sign-off — see docs/reviews/rca-player-alliance-freedom-2026-06-16.md).
// Logs when an alliance involving the kingdom the PLAYER rules is formed, on ANY path: the
// vanilla Kingdom->Diplomacy "Propose/Enact Alliance" button AND the proposal dialog. The
// service-side FormPlayerAlliance log only covers the dialog path; this Postfix also captures the
// kingdom-screen button path the user actually used. Pairs with the [diag] line in
// AllianceCampaignBehavior_EndAlliance_Patch to confirm form -> (no auto-end) holds in-game.
// Joins the Patch11_Diplomacy category (registered in SubModule.cs); Initialize is wired alongside
// the EndAlliance patch.
[HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.StartAlliance))]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class AllianceCampaignBehavior_StartAlliance_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(Kingdom proposerKingdom, Kingdom receiverKingdom)
    {
        if (_logger == null)
            return;
        if (!PlayerKingdomHelper.InvolvesPlayerRuledKingdom(proposerKingdom, receiverKingdom))
            return;

        _logger.LogInfo($"[Diplomacy][diag] Player alliance FORMED: {proposerKingdom?.StringId} <-> {receiverKingdom?.StringId}");
    }
}
