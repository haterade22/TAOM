namespace TAOM.Features.Execution;

/// <summary>
/// Computes the alignment-aware relation delta and notification flag for an execution event,
/// given the vanilla pre-modified relation change and notification state.
/// </summary>
/// <remarks>
/// Phase 9b #147: extracted from <c>TaomExecutionRelationModel</c> so the GameModel override
/// body satisfies gamemodels rule 4 (boundary conversion + direct delegate only, no inline
/// branching). The model converts sealed TaleWorlds heroes to <see cref="SideIds"/> pairs
/// (<c>TAOM.Adapters.FactionSides</c>, the id and the culture of one entity) at its boundary, then
/// delegates the entire decision (side resolution, kinslaying multiplier, cross-alignment zeroing,
/// notification suppression on zero-modified) to this service.
/// </remarks>
public interface IExecutionRelationService
{
    /// <summary>
    /// Compute the alignment-modified relation delta and the post-modification notification flag.
    /// </summary>
    /// <param name="executor">The executing hero (always the player — vanilla only asks for player executions).</param>
    /// <param name="victim">The executed hero.</param>
    /// <param name="evaluator">The clan leader whose relation with the player is being calculated.</param>
    /// <param name="baseRelationDelta">Vanilla <c>GetRelationChangeForExecutingHero</c> result.</param>
    /// <param name="baseShowNotification">Vanilla <c>showQuickNotification</c> out-value.</param>
    /// <returns>
    /// Final relation delta + notification flag. Each participant's side is resolved by faction id
    /// with a culture-id fallback, so a participant with no kingdom is still placed on a side rather
    /// than collapsing the whole calculation back to the vanilla penalty. A participant that
    /// classifies on neither id resolves Neutral, which is nobody's ally. The notification is
    /// suppressed iff the resulting delta is zero.
    /// </returns>
    ExecutionRelationResult GetRelationModifier(
        SideIds executor,
        SideIds victim,
        SideIds evaluator,
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
