namespace TAOM.Features.Execution.Hooks;

public class ExecutionActionHook : IOnExecutionAction
{
    private readonly IAlignmentService _alignmentService;

    public ExecutionActionHook(IAlignmentService alignmentService)
    {
        _alignmentService = alignmentService;
    }

    /// <summary>
    /// There is no dishonor in killing your enemy: the vanilla -1000 Honor XP applies only when the
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
}
