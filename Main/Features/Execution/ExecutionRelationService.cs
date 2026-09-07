namespace TAOM.Features.Execution;

/// <summary>
/// Default implementation of <see cref="IExecutionRelationService"/>.
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
        ExecutionParticipant executor,
        ExecutionParticipant victim,
        ExecutionParticipant evaluator,
        int baseRelationDelta,
        bool baseShowNotification)
    {
        // Every participant is placed on a side by kingdom id with a culture-id fallback. There is
        // deliberately no "unknown, defer to vanilla" escape: a single unresolved id used to hand the
        // whole calculation back to vanilla, and vanilla charges -10 to every honourable clan leader
        // in the world, which is exactly the Free Peoples. A participant that classifies on neither
        // id resolves Neutral, which is nobody's ally and everybody's enemy.
        var executorSide = Resolve(executor);
        var victimSide = Resolve(victim);
        var evaluatorSide = Resolve(evaluator);

        int modifiedDelta = CalculateModifiedDelta(executorSide, victimSide, evaluatorSide, baseRelationDelta);

        // Suppress notification when the modifier zeroes the delta — otherwise the player sees
        // a notification claiming a relation change that did not occur.
        bool showNotification = baseShowNotification && modifiedDelta != 0;

        return new ExecutionRelationResult(modifiedDelta, showNotification);
    }

    private FactionSide Resolve(ExecutionParticipant participant)
        => _alignmentService.ResolveSide(participant.KingdomId, participant.CultureId);

    private int CalculateModifiedDelta(
        FactionSide executorSide,
        FactionSide victimSide,
        FactionSide evaluatorSide,
        int baseRelationDelta)
    {
        if (_alignmentService.AreEnemyAlignments(executorSide, victimSide))
        {
            // Killing your enemy: only the victim's own side mourns him. The executor's allies
            // approve, and a third party has no opinion.
            return _alignmentService.AreSameAlignment(evaluatorSide, victimSide) ? baseRelationDelta : 0;
        }

        // Not enemies means both sides resolved to the same non-Neutral side, so this is kinslaying
        // and every evaluator feels it harder than vanilla.
        return (int)(baseRelationDelta * KinslayingMultiplier);
    }
}
