using System;
using HarmonyLib;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;

namespace TAOM.Features.Diplomacy.Hooks;

/// <summary>
/// Seam A of the kingdom-vote deadlock guard: never build a decision window for a ballot that no
/// longer applies.
///
/// <para><b>The vanilla defect.</b> <c>KingdomDecisionsVM.RefreshWith</c> is the only method that
/// builds the popup, and it never calls <c>ShouldBeCancelled()</c>. It constructs the item view
/// model, whose <c>InitValues()</c> runs <c>KingdomElection.StartElection()</c>; that sets
/// <c>IsCancelled = true</c> and stops. The window opens anyway. When the player then clicks Done,
/// <c>KingdomElection.ApplySelection()</c> is <c>if (!IsCancelled) { ... }</c> — a silent no-op — so
/// <c>KingdomDecisionConcluded</c> never fires, <c>IsKingsDecisionOver</c> stays false,
/// <c>KingdomDecisionPopupWidget</c> never gets the edge that starts its five-second auto-close
/// timer, and <c>ExecuteDone</c> (the only thing that sets <c>IsActive = false</c>) never runs.
/// Meanwhile <c>ExecuteFinalSelection</c> has set <c>_finalSelectionDone</c>, which disables the
/// popup's only button. The window can no longer be closed, the map navigation handler stays locked
/// while it is active, and nothing throws.</para>
///
/// <para><b>Both call sites, v1.4.8.</b> <c>KingdomDecisionsVM.HandleDecision</c>'s inquiry
/// affirmative callback (which checked staleness when the inquiry was CREATED, not when the player
/// clicked OK), and <c>KingdomManagementVM.ForceDecideDecision</c> (:696), which is wired to the
/// Settlement, Clan, Policy and Diplomacy tabs plus <c>OnGrantFief</c> (:711) and
/// <c>OnConfirmAbdicateLeadership</c> (:805) and checks nothing at all.</para>
///
/// <para><b>Why suppressing is safe.</b> Vanilla's own hourly
/// <c>KingdomDecisionProposalBehavior.UpdateKingdomDecisions</c> deletes every decision for which
/// <c>ShouldBeCancelled()</c> is true and raises <c>KingdomDecisionCancelled</c>. We only decline to
/// DISPLAY what the engine is about to discard, and deliberately do not remove it ourselves so the
/// pruner keeps ownership of removal and the event still fires exactly once.</para>
/// </summary>
[HarmonyPatch(typeof(KingdomDecisionsVM), nameof(KingdomDecisionsVM.RefreshWith))]
[HarmonyPatchCategory("Patch80_KingdomVoteDeadlock")]
public static class KingdomDecisionsVM_RefreshWith_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(KingdomDecisionsVM __instance, KingdomDecision decision)
    {
        if (!KingdomVoteDeadlockBinding.IsReady) return true;

        try
        {
            var service = KingdomVoteDeadlockBinding.Service;
            if (service == null) return true;

            var ballot = new KingdomBallotAdapter(decision);
            if (!service.ShouldSuppressBallot(ballot)) return true;

            KingdomVoteDeadlockBinding.WithdrawBallotFromQueue(__instance, decision);
            service.AnnounceLapsedBallot(ballot);
            return false;
        }
        catch (Exception ex)
        {
            // Suppress, do not defer. Vanilla is not a safe default at this call site: its multi-clan
            // branch builds the unclosable window, and its single-clan branch calls
            // GetChosenOutcomeText() on a null _chosenOutcome and throws. Withdrawing keeps the queue
            // moving and costs at most one ballot the player can retry by reopening the screen.
            _logger?.LogWarning($"[KingdomVote] RefreshWith guard faulted, withdrawing the ballot: {ex.Message}");
            try
            {
                KingdomVoteDeadlockBinding.WithdrawBallotFromQueue(__instance, decision);
            }
            catch (Exception inner)
            {
                _logger?.LogWarning($"[KingdomVote] Queue repair also faulted: {inner.Message}");
            }
            return false;
        }
    }
}
