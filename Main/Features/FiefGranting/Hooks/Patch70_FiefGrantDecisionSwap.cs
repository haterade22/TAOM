using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FiefGranting.Hooks;

/// <summary>
/// Patch70 — swaps vanilla's fief-grant election for TAOM's (#458).
///
/// <c>Kingdom.AddDecision</c> is the single chokepoint. <b>All three</b> producers of a
/// <c>SettlementClaimantDecision</c> funnel through it:
///
/// <list type="number">
/// <item><c>SettlementClaimantCampaignBehavior.DailyTickSettlement</c> — war capture.</item>
/// <item><c>SettlementClaimantPreliminaryDecision.ApplyChosenOutcome</c> — annexation follow-up,
/// the only one that sets <c>IsEnforced</c>.</item>
/// <item><c>KingdomManager.RelinquishSettlementOwnership</c> — a lord giving a fief up, which
/// passes the owner clan as BOTH proposer and <c>clanToExclude</c> so it cannot win the fief
/// straight back. That is why <c>ClanToExclude</c> must be carried across the swap.</item>
/// </list>
///
/// Patching the producers instead would need three patches, and the third was only found in review
/// after this was written: patching the sink caught it for free.
///
/// Producer 2 sets <c>IsEnforced = true</c> on the line BEFORE it calls <c>AddDecision</c>, so the
/// flag is present on the instance we replace and is copied across. If that ever inverts, an
/// enforced annexation silently stops being enforced, and no test can see it:
/// <c>Patch70FiefGrantDecisionSwapBindingTests</c> pins the members the copy needs, but the ordering
/// is only provable by re-reading the decompiled engine after a bump.
///
/// Replacement is safe because no producer keeps a reference to the object it hands over: all three
/// construct it inline as a local, and <c>AddDecision</c> only reads it.
/// </summary>
[HarmonyPatch(typeof(Kingdom), nameof(Kingdom.AddDecision))]
[HarmonyPatchCategory("Patch70_FiefGrantDecisionSwap")]
public static class Patch70_FiefGrantDecisionSwap
{
    // The container is built once in SubModule.OnSubModuleLoad and never rebuilt, and all three of
    // these are Reuse.Singleton holding no campaign-bound state, so caching them for the life of the
    // process is safe across "quit to menu, load another campaign".
    private static IFiefGrantPolicyService _policy;
    private static ICoopSessionProvider _coop;
    private static IModLogger _logger;
    private static bool _servicesResolved;

    /// <summary>
    /// The campaign a fault was already reported for. NOT a bare bool: a process-lifetime latch
    /// would swallow a genuinely new fault in a second campaign loaded in the same session, leaving
    /// it silently on vanilla scoring with nothing in the log. That exact shape is on file in
    /// <c>docs/reviews/lessons/state-lifecycle-save.md</c> ("ran once per process rather than once
    /// per session"), and it is set only AFTER the log call succeeds, per the same lesson's
    /// "latch after the successful pass, never before".
    /// </summary>
    private static Campaign _faultReportedFor;

    [HarmonyPrefix]
    public static void Prefix(ref KingdomDecision kingdomDecision)
    {
        try
        {
            var replacement = BuildReplacement(kingdomDecision);
            if (replacement != null)
                kingdomDecision = replacement;
        }
        catch (Exception ex)
        {
            // A fief election must never be the reason a campaign dies. Vanilla's decision is
            // already in hand, so falling through to it is a complete recovery.
            Report(ex);
        }
    }

    private static TaomSettlementClaimantDecision BuildReplacement(KingdomDecision decision)
    {
        // Already ours: re-wrapping would recurse if anything ever re-adds a decision.
        if (decision is TaomSettlementClaimantDecision) return null;
        if (!(decision is SettlementClaimantDecision original)) return null;

        EnsureServices();

        if (_policy == null || !_policy.IsEnabled) return null;

        // A client that must not mutate shared world state runs vanilla and takes the host's result.
        if (_coop != null && _coop.ShouldDeferToHost) return null;

        var proposerClan = original.ProposerClan;
        var settlement = original.Settlement;
        if (proposerClan == null || settlement == null) return null;

        // capturerHero is deliberately null: vanilla stores it and never reads it, the daily-tick
        // path passes null already, and TaomSettlementClaimantDecision reads Town.LastCapturedBy.
        return new TaomSettlementClaimantDecision(
            proposerClan, settlement, null, original.ClanToExclude)
        {
            IsEnforced = original.IsEnforced,
            NotifyPlayer = original.NotifyPlayer,
        };
    }

    /// <summary>
    /// Resolves at most once per process. Attempting per call would mean an exception on every
    /// invocation when the feature is not registered, and this runs once per candidate per election.
    /// </summary>
    private static void EnsureServices()
    {
        if (_servicesResolved) return;

        try
        {
            // Into locals first, and committed only when BOTH succeed. Assigning _policy before
            // resolving _coop would, on a coop-provider failure, leave TAOM scoring active with the
            // host-deferral gate silently absent: the worst of the two outcomes, and strictly worse
            // than falling through to vanilla.
            var policy = IoC.Resolve<IFiefGrantPolicyService>();
            var coop = IoC.Resolve<ICoopSessionProvider>();

            _policy = policy;
            _coop = coop;
            _servicesResolved = true;
        }
        catch (Exception ex)
        {
            // Left unlatched deliberately: a resolve that failed because the container was not yet
            // built should be retried, and one that keeps failing is worth a line in the log rather
            // than permanent silence. Report() is itself once per campaign.
            _policy = null;
            _coop = null;
            Report(ex);
        }
    }

    private static void Report(Exception ex)
    {
        try
        {
            // Once per CAMPAIGN, not once per process: a second campaign in the same session must
            // still be able to report its own fault. This runs on a daily tick, so an unguarded log
            // would flood.
            var campaign = Campaign.Current;
            if (ReferenceEquals(_faultReportedFor, campaign) && campaign != null) return;

            _logger ??= IoC.Resolve<IModLogger>();
            _logger?.LogWarning(
                $"Patch70: could not install the TAOM fief-grant election " +
                $"({ex.GetType().Name}: {ex.Message}). Vanilla's grant scoring is in effect for the " +
                $"rest of this campaign. See docs/features/fief-granting.md.");

            // Latched only now, after the log actually went out. Latching first would lose the one
            // report entirely if logging itself threw.
            _faultReportedFor = campaign;
        }
        catch
        {
            // Reporting must never resurrect the fault it just suppressed.
        }
    }
}
