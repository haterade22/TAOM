using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.Diplomacy;

/// <inheritdoc cref="IKingdomVoteDeadlockService"/>
public sealed class KingdomVoteDeadlockService : IKingdomVoteDeadlockService
{
    /// <summary>Localization key for the withdrawn-vote toast. Registered in taom_module_strings.xml.</summary>
    public const string LapseKey = "taom_vote_lapsed";

    /// <summary>English fallback, used when a language file has no entry for <see cref="LapseKey"/>.</summary>
    public const string LapseFallback = "The vote on \"{VOTE}\" no longer applies and has been withdrawn.";

    /// <summary>
    /// Upper bound on the announce-dedupe set. Ballots are per-campaign-session objects, so without a
    /// cap a very long session would accumulate keys for every decision it ever withdrew. Overflow
    /// clears the set rather than evicting one entry: the worst case is announcing one lapse twice,
    /// which is strictly better than the leak.
    /// </summary>
    public const int AnnouncedCap = 64;

    private readonly IInquiryAdapter _inquiry;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _announced = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Diagnostic only, so a test can prove the dedupe set stays bounded.</summary>
    public int AnnouncedCount => _announced.Count;

    public KingdomVoteDeadlockService(IInquiryAdapter inquiry, IModLogger logger)
    {
        _inquiry = inquiry;
        _logger = logger;
    }

    public bool ShouldSuppressBallot(IKingdomBallotAdapter ballot)
    {
        if (ballot == null) return false;

        try
        {
            return ballot.IsStale;
        }
        catch (Exception ex)
        {
            // Suppress rather than defer. lessons/harmony-il.md: "fall through to vanilla on error is
            // only safe when vanilla is a safe default at THAT call site", and here it is not. On a
            // ballot we could not judge, vanilla RefreshWith either builds the unclosable window this
            // feature exists to prevent (multi-clan branch) or, for a single-clan kingdom, calls
            // GetChosenOutcomeText() on an election whose _chosenOutcome is null and throws inside the
            // concrete decision's override. The ExecuteFinalSelection backstop cannot help on that
            // second path at all, because no DecisionItemBaseVM is ever constructed there.
            //
            // Cost of being wrong this way: one ballot skipped for this screen session, which the
            // player recovers by reopening the Kingdom screen. Cost of being wrong the other way: a
            // hard lock or a crash.
            _logger?.LogWarning(
                $"[KingdomVote] Could not judge whether a ballot is stale, withdrawing it rather than " +
                $"letting vanilla open it: {ex.Message}");
            return true;
        }
    }

    public void AnnounceLapsedBallot(IKingdomBallotAdapter ballot)
    {
        if (ballot == null) return;

        try
        {
            var key = ballot.BallotKey ?? string.Empty;
            if (!_announced.Add(key)) return;
            if (_announced.Count > AnnouncedCap)
            {
                _announced.Clear();
                _announced.Add(key);
            }

            var title = ballot.Title ?? string.Empty;
            _logger?.LogDebug($"[KingdomVote] Withdrew a ballot that no longer applies: \"{title}\"");
            _inquiry?.ShowMessage(LapseKey, LapseFallback, "VOTE", title, null, null);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[KingdomVote] Could not announce a withdrawn ballot: {ex.Message}");
        }
    }
}
