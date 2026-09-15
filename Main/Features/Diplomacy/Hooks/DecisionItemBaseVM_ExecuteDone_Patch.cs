using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;

namespace TAOM.Features.Diplomacy.Hooks;

/// <summary>
/// Seam E of the kingdom-vote deadlock guard: vanilla's close runs once, and only on a window
/// whose election is over.
///
/// <para><b>Why a stray close can arrive.</b> <c>ExecuteDone</c> has exactly one caller on v1.5.3:
/// <c>KingdomDecisionPopupWidget</c>'s <c>FinalDone</c> event (<c>KingdomDecision.xml:16</c>),
/// fired five seconds after the widget's <c>IsKingsDecisionDone</c> latch takes its false-to-true
/// edge. Nothing but that firing (<c>ExecuteFinalDone</c>) ever disarms the timer. Seam D closes a
/// pre-concluded window at once, before the timer fires, so the timer outlives the window and its
/// <c>FinalDone</c> reaches whatever item the widget is bound to five seconds later: the same,
/// already closed item (a duplicate outcome inquiry, which <c>GauntletQueryManager.CreateQuery</c>
/// asserts away), or the NEXT decision's live item if the player opened one in the meantime, where
/// <c>GetChosenOutcomeText()</c> dereferences a null <c>_chosenOutcome</c> inside the widget's
/// <c>OnLateUpdate</c>. Codex review 113, finding F1.</para>
///
/// <para><b>The guard.</b> Run vanilla only when <c>IsActive &amp;&amp; IsKingsDecisionOver</c>. Every
/// legitimate call satisfies both: the timer arms only on the true edge of
/// <c>IsKingsDecisionOver</c>, and <c>IsActive</c> is cleared by nothing but <c>ExecuteDone</c>
/// itself. Seam D checks the same pair before it invokes, so its call passes. A skipped call is
/// logged at debug level; it is expected exactly once after every seam D close.</para>
/// </summary>
[HarmonyPatch(typeof(DecisionItemBaseVM), "ExecuteDone")]
[HarmonyPatchCategory("Patch80_KingdomVoteDeadlock")]
public static class DecisionItemBaseVM_ExecuteDone_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(DecisionItemBaseVM __instance)
    {
        if (__instance == null) return true;

        try
        {
            if (__instance.IsActive && __instance.IsKingsDecisionOver) return true;

            _logger?.LogDebug(
                "[KingdomVote] Skipped a stray ExecuteDone (active=" + __instance.IsActive +
                ", over=" + __instance.IsKingsDecisionOver + "); the popup widget's timer outlived the window it was armed for.");
            return false;
        }
        catch (Exception ex)
        {
            // Deferring to vanilla here is the pre-seam-E behaviour, which is a duplicate inquiry
            // at worst on the closed-item path; the guard exists for the live-item path, and that
            // one cannot throw on two public bool reads.
            _logger?.LogWarning($"[KingdomVote] ExecuteDone guard faulted, deferring to vanilla: {ex.Message}");
            return true;
        }
    }
}
