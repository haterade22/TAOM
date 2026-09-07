using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Election;

namespace TAOM.Adapters;

/// <inheritdoc cref="IKingdomBallotAdapter"/>
/// <remarks>
/// Constructed at the patch boundary, not resolved from IoC: it wraps one per-call engine object,
/// so there is nothing for the container to own ("Convert at boundary", csharp-architecture.md).
/// </remarks>
public sealed class KingdomBallotAdapter : IKingdomBallotAdapter
{
    private readonly KingdomDecision _decision;

    public KingdomBallotAdapter(KingdomDecision decision)
    {
        _decision = decision;
    }

    public bool IsStale => _decision != null && _decision.ShouldBeCancelled();

    public string BallotKey =>
        _decision == null
            ? string.Empty
            : _decision.GetType().Name + "#" + RuntimeHelpers.GetHashCode(_decision).ToString();

    public string Title
    {
        get
        {
            // GetGeneralTitle composes from the referenced kingdoms — ProposeCallToWarAgreementDecision
            // reads CalledKingdom.InformalName, and a ballot goes stale precisely when one of those
            // kingdoms has changed state, so this is the one member likely to fault on a dead ballot.
            // The notice is worth more than the name.
            try
            {
                return _decision?.GetGeneralTitle()?.ToString() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
