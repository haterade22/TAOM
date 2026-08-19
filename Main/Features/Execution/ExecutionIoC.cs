using DryIoc;
using TAOM.Features.Execution.Hooks;

namespace TAOM.Features.Execution;

public static class ExecutionIoC
{
    public static void RegisterExecutionFeature(IContainer container)
    {
        container.Register<IAlignmentConfigProvider, AlignmentConfigProvider>(Reuse.Singleton);
        container.Register<IAlignmentService, AlignmentService>(Reuse.Singleton);
        container.Register<IOnExecutionAction, ExecutionActionHook>(Reuse.Singleton);
        // Phase 9b #147: originally the service-layer wrapper behind TaomExecutionRelationModel.
        // v1.5.0 deleted vanilla's ExecutionRelationModel, so that GameModel is gone; the same
        // per-evaluator alignment rule now runs through IOnExecutionAction.GetRelationModifier via
        // ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch on the Blood Feud seam.
        container.Register<IExecutionRelationService, ExecutionRelationService>(Reuse.Singleton);
    }

    public static void InitializeHooks(IOnExecutionAction executionHook)
    {
        TraitLevelingHelper_OnBloodFeudStarted_Patch.Initialize(executionHook);
        ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.Initialize(executionHook);
    }
}
