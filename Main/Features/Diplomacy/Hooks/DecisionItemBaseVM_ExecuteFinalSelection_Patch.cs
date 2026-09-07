using System;
using HarmonyLib;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;

namespace TAOM.Features.Diplomacy.Hooks;

/// <summary>
/// Seam B of the kingdom-vote deadlock guard: the escape hatch. Any decision window whose election
/// is cancelled can never close itself, so close it here.
///
/// <para><b>What this seam is actually for.</b> <c>KingdomElection.IsCancelled</c> has a private
/// setter written only in <c>Setup()</c> and <c>StartElection()</c>, both of which run exactly once
/// per item view model, from <c>DecisionItemBaseVM.InitValues()</c>. So a window's election cannot
/// go from live to cancelled after it opens: the verdict is fixed at construction. This seam is
/// therefore NOT a race guard. It is the backstop for every case where seam A did not get to judge
/// the ballot: the binding failed to resolve and <c>IsReady</c> is false, the staleness check itself
/// faulted, or a future call path reaches the item view model without passing through
/// <c>RefreshWith</c>. In those cases the window is already unclosable and this is the only thing
/// that can shut it.</para>
///
/// <para><b>Closing via the callback, not <c>ExecuteDone</c>.</b> <c>ExecuteDone</c> opens with
/// <c>KingdomDecisionMaker.GetChosenOutcomeText()</c>, which dereferences the election's
/// <c>_chosenOutcome</c>. That field is null on a cancelled election, so calling <c>ExecuteDone</c>
/// would trade the hang for an NRE. Setting <c>IsActive = false</c> hides the popup (the prefab root
/// is <c>IsVisible="@IsActive"</c>) and invoking <c>_onDecisionOver</c> runs vanilla's own
/// <c>OnDecisionOver</c>, which finalizes the item view model, clears <c>CurrentDecision</c> and
/// re-arms the queue.</para>
///
/// <para><b>Both call sites, v1.4.8.</b> The prefab's only button
/// (<c>KingdomDecision.xml</c>, <c>Command.Click="ExecuteFinalSelection"</c>) and
/// <c>GauntletKingdomScreen.OnFrameTick</c> (:68) for the Confirm hotkey.</para>
///
/// <para><c>Priority.Last</c> because this expresses a hard invariant rather than a tunable: the
/// window must end up closable, and postfixes run highest-priority first, so running last makes our
/// close the final word. Nothing is left open in the other direction — we never keep a window open
/// that vanilla would have closed.</para>
/// </summary>
[HarmonyPatch(typeof(DecisionItemBaseVM), nameof(DecisionItemBaseVM.ExecuteFinalSelection))]
[HarmonyPatchCategory("Patch80_KingdomVoteDeadlock")]
public static class DecisionItemBaseVM_ExecuteFinalSelection_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(DecisionItemBaseVM __instance)
    {
        if (!KingdomVoteDeadlockBinding.IsReady || __instance == null) return;

        try
        {
            var election = __instance.KingdomDecisionMaker;
            if (election == null || !election.IsCancelled) return;

            var service = KingdomVoteDeadlockBinding.Service;
            var decision = KingdomVoteDeadlockBinding.GetDecisionOf(__instance);
            var onDecisionOver = KingdomVoteDeadlockBinding.GetOnDecisionOver(__instance);

            __instance.IsActive = false;

            // Vanilla's ExecuteDone clears this listener before OnDecisionOver runs, and we must too.
            // CampaignEvents.KingdomDecisionConcluded stores listeners in a hand-rolled linked list that
            // holds a STRONG reference to the owner, and DecisionItemBaseVM.OnFinalize only unregisters
            // the tutorial event. Skipping it leaks the whole item view model (its DecisionOptionsList,
            // its KingdomElection and the decision it closed over) for the rest of the campaign session,
            // once per window this seam closes. OnDecisionOver then nulls CurrentDecision, so nothing
            // else can reach it to clean up.
            CampaignEvents.KingdomDecisionConcluded.ClearListeners(__instance);

            onDecisionOver?.Invoke();

            if (service != null && decision != null)
                service.AnnounceLapsedBallot(new KingdomBallotAdapter(decision));

            _logger?.LogDebug(
                "[KingdomVote] Force-closed a decision window whose election was cancelled; " +
                "vanilla ApplySelection would have left it unclosable.");
        }
        catch (Exception ex)
        {
            // Swallowing leaves vanilla's behaviour, which here is the stuck window. There is
            // nothing better available from a postfix, and seam A prevents almost every path in.
            _logger?.LogWarning($"[KingdomVote] Could not force-close a cancelled decision window: {ex.Message}");
        }
    }
}
