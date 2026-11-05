namespace TAOM.Features.Execution.Hooks;

public interface IOnExecutionAction
{
    bool ShouldApplyHonorPenalty(SideIds victim, SideIds executor);
}
