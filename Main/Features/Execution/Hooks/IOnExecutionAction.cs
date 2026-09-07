namespace TAOM.Features.Execution.Hooks;

public interface IOnExecutionAction
{
    bool ShouldApplyHonorPenalty(ExecutionParticipant victim, ExecutionParticipant executor);
}
