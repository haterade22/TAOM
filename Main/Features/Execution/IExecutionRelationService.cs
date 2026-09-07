namespace TAOM.Features.Execution;

/// <summary>
/// One side of an execution: the executor, the victim, or the clan leader evaluating the kill.
/// Carries both identifiers because a kingdom id alone is not always enough to place a hero on a
/// side — see <see cref="IAlignmentService.ResolveSide"/>.
/// </summary>
public readonly struct ExecutionParticipant
{
    public string KingdomId { get; }
    public string CultureId { get; }

    public ExecutionParticipant(string kingdomId, string cultureId)
    {
        KingdomId = kingdomId;
        CultureId = cultureId;
    }
}

/// <summary>
/// Computes the alignment-aware relation delta and notification flag for an execution event,
/// given the vanilla pre-modified relation change and notification state.
/// </summary>
/// <remarks>
/// Phase 9b #147 — extracted from <c>TaomExecutionRelationModel</c> so the GameModel override
/// body satisfies gamemodels rule 4 (boundary conversion + direct delegate only — no inline
/// branching). The model converts sealed TaleWorlds heroes to <see cref="ExecutionParticipant"/>
/// values at its boundary, then delegates the entire decision (side resolution, kinslaying
/// multiplier, cross-alignment zeroing, notification suppression on zero-modified) to this service.
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
    /// Final relation delta + notification flag. Each participant's side is resolved by kingdom id
    /// with a culture-id fallback, so a participant with no kingdom is still placed on a side rather
    /// than collapsing the whole calculation back to the vanilla penalty. A participant that
    /// classifies on neither id resolves Neutral, which is nobody's ally. The notification is
    /// suppressed iff the resulting delta is zero.
    /// </returns>
    ExecutionRelationResult GetRelationModifier(
        ExecutionParticipant executor,
        ExecutionParticipant victim,
        ExecutionParticipant evaluator,
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
