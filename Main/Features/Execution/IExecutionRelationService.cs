namespace TAOM.Features.Execution;

/// <summary>
/// Computes the alignment-aware relation delta and notification flag for an execution event,
/// given the vanilla pre-modified relation change and notification state.
/// </summary>
/// <remarks>
/// Phase 9b #147 — extracted from <c>TaomExecutionRelationModel</c> so the GameModel override
/// body satisfies gamemodels rule 4 (boundary conversion + direct delegate only — no inline
/// branching). The model converts sealed TaleWorlds heroes to string kingdom IDs at its boundary,
/// then delegates the entire decision (null-guards, kinslaying multiplier, cross-alignment zeroing,
/// notification suppression on zero-modified) to this service.
/// </remarks>
public interface IExecutionRelationService
{
    /// <summary>
    /// Compute the alignment-modified relation delta and the post-modification notification flag.
    /// </summary>
    /// <param name="executorKingdomId">Player's kingdom (may be null/empty if independent).</param>
    /// <param name="victimKingdomId">Victim hero's kingdom (may be null if independent).</param>
    /// <param name="evaluatorKingdomId">Kingdom of the hero whose relation is being calculated (may be null if independent).</param>
    /// <param name="baseRelationDelta">Vanilla <c>GetRelationChangeForExecutingHero</c> result.</param>
    /// <param name="baseShowNotification">Vanilla <c>showQuickNotification</c> out-value.</param>
    /// <returns>
    /// Final relation delta + notification flag. When any kingdom ID is null/empty the result
    /// is the vanilla pass-through (no modifier applied). Otherwise the alignment modifier
    /// is applied and the notification is suppressed iff the resulting delta is zero.
    /// </returns>
    ExecutionRelationResult GetRelationModifier(
        string executorKingdomId,
        string victimKingdomId,
        string evaluatorKingdomId,
        int baseRelationDelta,
        bool baseShowNotification);
}

/// <summary>Final relation delta + notification flag returned by <see cref="IExecutionRelationService"/>.</summary>
public readonly struct ExecutionRelationResult
{
    public int RelationDelta { get; }
    public bool ShowNotification { get; }

    public ExecutionRelationResult(int relationDelta, bool showNotification)
    {
        RelationDelta = relationDelta;
        ShowNotification = showNotification;
    }
}
