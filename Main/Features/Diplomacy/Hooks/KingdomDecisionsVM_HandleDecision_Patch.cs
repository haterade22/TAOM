using System;
using HarmonyLib;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;

namespace TAOM.Features.Diplomacy.Hooks;

/// <summary>
/// Seam C of the kingdom-vote deadlock guard: keep the ballot queue moving after vanilla meets a
/// ballot that no longer applies.
///
/// <para><b>The vanilla defect.</b> When <c>HandleDecision</c> does catch a stale ballot it takes an
/// else branch that sets <c>_shouldCheckForDecision = false</c> WITHOUT adding the ballot to
/// <c>_examinedDecisionsSinceInit</c> and without ever re-arming. <c>OnFrameTick</c> then returns
/// early for the rest of the session, so every remaining decision is silently never offered.
/// Reopening the Kingdom screen clears it, because the <c>KingdomDecisionsVM</c> constructor
/// pre-marks stale decisions as examined — which is exactly the workaround players found
/// ("back fully out of one decision before starting the next").</para>
///
/// <para><b>All three call sites, v1.4.8.</b> <c>KingdomDecisionsVM.OnFrameTick</c> (per frame),
/// <c>KingdomDecisionsVM.HandleNextDecision</c> (reached from
/// <c>KingdomManagementVM.OnRefreshDecision</c>), and <c>GauntletKingdomScreen</c>'s
/// <c>IGameStateListener.OnActivate</c> (:166) for the decision a <c>KingdomState</c> was opened on.
/// Not <c>OnInitialize</c>: that is a separate empty stub on the same class, so a future reader
/// grepping for it will not find this call.</para>
///
/// <para>Scoped to staleness only. <c>HandleDecision</c> also bails when
/// <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c> is false, but that ballot is still
/// live and re-arming there would re-enter the same gate every frame.</para>
/// </summary>
[HarmonyPatch(typeof(KingdomDecisionsVM), nameof(KingdomDecisionsVM.HandleDecision))]
[HarmonyPatchCategory("Patch80_KingdomVoteDeadlock")]
public static class KingdomDecisionsVM_HandleDecision_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(KingdomDecisionsVM __instance, KingdomDecision curDecision)
    {
        if (!KingdomVoteDeadlockBinding.IsReady || curDecision == null) return;

        try
        {
            var service = KingdomVoteDeadlockBinding.Service;
            if (service == null) return;

            var ballot = new KingdomBallotAdapter(curDecision);
            if (!service.ShouldSuppressBallot(ballot)) return;

            KingdomVoteDeadlockBinding.WithdrawBallotFromQueue(__instance, curDecision);
            service.AnnounceLapsedBallot(ballot);
        }
        catch (Exception ex)
        {
            // A postfix that only repairs queue bookkeeping: swallowing leaves vanilla's own
            // behaviour, which is the wedged queue, not a crash.
            _logger?.LogWarning($"[KingdomVote] HandleDecision queue repair faulted: {ex.Message}");
        }
    }
}
