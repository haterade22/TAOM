using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;

namespace TAOM.Features.Diplomacy.Hooks;

/// <summary>
/// Seam D of the kingdom-vote deadlock guard: never leave a window open on an election that was
/// already over when the window was built.
///
/// <para><b>The vanilla defect.</b> <c>DecisionItemBaseVM</c>'s constructor registers its
/// <c>KingdomDecisionConcluded</c> listener and then runs <c>InitValues()</c>, which calls
/// <c>KingdomElection.StartElection()</c>. When the player is not a supporter
/// (<c>Supporter.IsPlayer => Clan.Leader.IsHumanPlayerCharacter</c>, false whenever
/// <c>Clan.PlayerClan.Leader != Hero.MainHero</c>), <c>StartElection</c> takes
/// <c>ReadyToAiChoose()</c>, which applies the outcome and fires the concluded event synchronously,
/// inside the constructor. The brand-new view model's own handler sets
/// <c>IsKingsDecisionOver = true</c> before it is ever bound, and <c>InitValues</c> then sets
/// <c>IsActive = true</c> with nothing selectable and Done disabled.</para>
///
/// <para>The popup normally closes on a five-second timer that
/// <c>KingdomDecisionPopupWidget.IsKingsDecisionDone</c> starts on its false-to-true edge. The
/// widget is reused across decisions in one screen visit and that flag is never reset, so the
/// first pre-concluded window closes on the timer and every later one binds already-true, gets no
/// edge, and stays open with map navigation locked (#550, the Player Switcher route). Seams A to C
/// gate on a CANCELLED election and never see this: <c>ReadyToAiChoose()</c> leaves
/// <c>IsCancelled</c> false.</para>
///
/// <para><b>What this seam does.</b> After <c>RefreshWith</c> has built the window for
/// <c>decision</c>, if that window's election is already over, run vanilla's own
/// <c>ExecuteDone</c> at once: the popup hides, the outcome inquiry shows, and its OK runs
/// <c>OnDecisionOver</c>. That is exactly what the timer would have done five seconds later on the
/// one occasion it works, minus the wait and minus the dependency on the widget edge.
/// <c>ExecuteDone</c> is safe on this path and unsafe on seam B's, see
/// <see cref="KingdomVoteDeadlockBinding.CloseViaExecuteDone"/>.</para>
///
/// <para><b>Both call sites, v1.5.3.</b> <c>KingdomDecisionsVM.HandleDecision</c>'s inquiry
/// affirmative callback and <c>KingdomManagementVM.ForceDecideDecision</c> (:703, wired to the
/// Settlement, Clan, Policy and Diplomacy tabs, <c>OnGrantFief</c> :720 and
/// <c>OnConfirmAbdicateLeadership</c>). The item is matched to THIS call by its <c>_decision</c>,
/// so a window seam A refused to build (prefix returned false, this postfix still runs) or an
/// older item left in <c>CurrentDecision</c> is a no-op.</para>
///
/// <para><b>The widget timer outlives this close.</b> The bind pushes <c>IsKingsDecisionOver</c>
/// into the popup widget, whose latch arms its five-second timer, and only that timer's own
/// firing disarms it. Seam E (<see cref="DecisionItemBaseVM_ExecuteDone_Patch"/>) makes the late
/// <c>FinalDone</c> harmless whichever item is bound by then.</para>
///
/// <para>TAOM's Player Switcher no longer produces the state that reaches here (a takeover
/// promotes the lord to clan leader, and a session-launch repair does the same for older saves),
/// so a hit is logged at warning level: it means some other route left the player outside their
/// clan's leadership, and the log line is the evidence to trace it from.</para>
///
/// <para><c>Priority.Last</c>, as seam B: a hard invariant (the window must end up closable)
/// rather than a tunable, applied after any other postfix has had its say.</para>
/// </summary>
[HarmonyPatch(typeof(KingdomDecisionsVM), nameof(KingdomDecisionsVM.RefreshWith))]
[HarmonyPatchCategory("Patch80_KingdomVoteDeadlock")]
public static class KingdomDecisionsVM_RefreshWith_AutoResolved_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(KingdomDecisionsVM __instance, KingdomDecision decision)
    {
        if (!KingdomVoteDeadlockBinding.IsReady || __instance == null || decision == null) return;

        try
        {
            var item = __instance.CurrentDecision;
            if (item == null) return;
            if (!ReferenceEquals(KingdomVoteDeadlockBinding.GetDecisionOf(item), decision)) return;
            if (!item.IsActive || !item.IsKingsDecisionOver) return;

            if (!KingdomVoteDeadlockBinding.CloseViaExecuteDone(item)) return;

            _logger?.LogWarning(
                "[KingdomVote] A decision window opened on an election that concluded inside its own " +
                "constructor (the player is not a supporter: is Clan.PlayerClan.Leader someone else?); " +
                "closed it through vanilla ExecuteDone instead of leaving it stuck.");
        }
        catch (Exception ex)
        {
            // Swallowing leaves vanilla's behaviour, which here is the stuck window. Nothing
            // better is available from a postfix.
            _logger?.LogWarning($"[KingdomVote] Could not close a pre-concluded decision window: {ex.Message}");
        }
    }
}
