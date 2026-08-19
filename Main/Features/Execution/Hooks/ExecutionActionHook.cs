namespace TAOM.Features.Execution.Hooks;

public class ExecutionActionHook : IOnExecutionAction
{
    private readonly IAlignmentService _alignmentService;
    private const float KinslayingMultiplier = 1.5f;

    public ExecutionActionHook(IAlignmentService alignmentService)
    {
        _alignmentService = alignmentService;
    }

    public bool ShouldApplyHonorPenalty(string victimKingdomId, string executorKingdomId)
    {
        return !_alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);
    }


    public int GetRelationModifier(string executorKingdomId, string victimKingdomId, string evaluatorKingdomId, int baseRelationChange)
    {
        bool crossAlignment = _alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);

        if (crossAlignment)
        {
            if (_alignmentService.AreSameAlignment(evaluatorKingdomId, executorKingdomId))
                return 0;

            if (_alignmentService.AreSameAlignment(evaluatorKingdomId, victimKingdomId))
                return baseRelationChange;

            return 0;
        }

        if (_alignmentService.AreSameAlignment(executorKingdomId, victimKingdomId))
            return (int)(baseRelationChange * KinslayingMultiplier);

        return baseRelationChange;
    }
}
