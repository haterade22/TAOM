namespace TAOM.Adapters;

/// <summary>
/// ADR-007 boundary over a single vanilla <c>TaleWorlds.CampaignSystem.Election.KingdomDecision</c>,
/// so <c>IKingdomVoteDeadlockService</c> can judge a ballot without ever seeing an engine type.
///
/// Deliberately three read-only members. Everything the deadlock guard needs to decide is "has this
/// ballot gone stale", "have I already told the player about this one", and "what do I call it".
/// </summary>
public interface IKingdomBallotAdapter
{
    /// <summary>
    /// Vanilla's own verdict (<c>KingdomDecision.ShouldBeCancelled()</c>) that this ballot no longer
    /// applies. Vanilla's hourly <c>KingdomDecisionProposalBehavior.UpdateKingdomDecisions</c> deletes
    /// every decision for which this is true, so a stale ballot is one the engine is about to discard
    /// anyway. Propagates rather than swallowing: the service logs the fault and defers to vanilla.
    /// </summary>
    bool IsStale { get; }

    /// <summary>
    /// Stable identity for the lifetime of the decision object, used only to avoid announcing the
    /// same lapse twice. Not persisted and not meaningful across a save/load.
    /// </summary>
    string BallotKey { get; }

    /// <summary>
    /// The decision's own localized general title, for the player-facing notice. Never throws:
    /// a decision whose referenced kingdom has been eliminated can fault while composing its title,
    /// and losing the name must not cost us the notice.
    /// </summary>
    string Title { get; }
}
