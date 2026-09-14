namespace TAOM.Features.Execution.Hooks;

public class ExecutionActionHook : IOnExecutionAction
{
    private readonly IAlignmentService _alignmentService;
    private readonly IExecutionRelationService _relationService;

    public ExecutionActionHook(IAlignmentService alignmentService, IExecutionRelationService relationService)
    {
        _alignmentService = alignmentService;
        _relationService = relationService;
    }

    /// <summary>
    /// There is no dishonor in killing your enemy: the vanilla trait hit applies only when the
    /// executor and victim are NOT on opposing sides. Sides resolve by kingdom id with a culture-id
    /// fallback, so a kingdom-less executor (independent, mercenary, or enlisted player) is still
    /// placed on a side instead of falling through to the vanilla penalty.
    /// </summary>
    public bool ShouldApplyHonorPenalty(ExecutionParticipant victim, ExecutionParticipant executor)
    {
        var victimSide = _alignmentService.ResolveSide(victim.KingdomId, victim.CultureId);
        var executorSide = _alignmentService.ResolveSide(executor.KingdomId, executor.CultureId);
        return !_alignmentService.AreEnemyAlignments(executorSide, victimSide);
    }

    /// <summary>
    /// Delegates to <see cref="IExecutionRelationService"/>, the one owner of the side resolution,
    /// the cross-alignment zeroing and the kinslaying multiplier. The v1.5.2 seam applies each
    /// per-clan change with quick notifications off and shows a single summary itself, so only the
    /// delta is returned here; the service's notification flag has no consumer on this path.
    /// </summary>
    public int GetRelationModifier(
        ExecutionParticipant executor,
        ExecutionParticipant victim,
        ExecutionParticipant evaluator,
        int baseRelationDelta)
        => _relationService
            .GetRelationModifier(executor, victim, evaluator, baseRelationDelta, baseShowNotification: false)
            .RelationDelta;

    public bool IsPlayerTheBereaved(string victimClanId, string playerClanId)
        => !string.IsNullOrEmpty(victimClanId) && victimClanId == playerClanId;
}
