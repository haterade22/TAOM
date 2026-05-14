namespace TAOM.Features.Execution;

/// <summary>
/// Default implementation of <see cref="IExecutionRelationService"/>.
/// Replicates the alignment-aware logic that previously lived inline in
/// <see cref="Models.TaomExecutionRelationModel"/> + <see cref="Hooks.ExecutionActionHook"/>.
/// </summary>
public class ExecutionRelationService : IExecutionRelationService
{
    private const float KinslayingMultiplier = 1.5f;

    private readonly IAlignmentService _alignmentService;

    public ExecutionRelationService(IAlignmentService alignmentService)
    {
        _alignmentService = alignmentService;
    }

    public ExecutionRelationResult GetRelationModifier(
        string executorKingdomId,
        string victimKingdomId,
        string evaluatorKingdomId,
        int baseRelationDelta,
        bool baseShowNotification)
    {
        // Fall through to vanilla when any kingdom is null/empty (independent player, no-faction
        // victim, no-faction evaluator). Mirrors the prior model-body null guard.
        if (string.IsNullOrEmpty(executorKingdomId)
            || string.IsNullOrEmpty(victimKingdomId)
            || string.IsNullOrEmpty(evaluatorKingdomId))
        {
            return new ExecutionRelationResult(baseRelationDelta, baseShowNotification);
        }

        int modifiedDelta = CalculateModifiedDelta(executorKingdomId, victimKingdomId, evaluatorKingdomId, baseRelationDelta);

        // Suppress notification when the modifier zeroes the delta — otherwise the player sees
        // a notification claiming a relation change that did not occur.
        bool showNotification = baseShowNotification && modifiedDelta != 0;

        return new ExecutionRelationResult(modifiedDelta, showNotification);
    }

    private int CalculateModifiedDelta(
        string executorKingdomId,
        string victimKingdomId,
        string evaluatorKingdomId,
        int baseRelationDelta)
    {
        bool crossAlignment = _alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);

        if (crossAlignment)
        {
            // Executor's side cheers — no penalty among allies.
            if (_alignmentService.AreSameAlignment(evaluatorKingdomId, executorKingdomId))
                return 0;

            // Victim's side feels the full vanilla penalty.
            if (_alignmentService.AreSameAlignment(evaluatorKingdomId, victimKingdomId))
                return baseRelationDelta;

            // Neutral / third-side observer — no opinion.
            return 0;
        }

        // Same-alignment kill — kinslaying multiplier hardens the penalty.
        if (_alignmentService.AreSameAlignment(executorKingdomId, victimKingdomId))
            return (int)(baseRelationDelta * KinslayingMultiplier);

        // Same alignment but not strictly same-side (e.g. both Neutral) — vanilla.
        return baseRelationDelta;
    }
}
