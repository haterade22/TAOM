using DryIoc;
using TAOM.Adapters;
using TAOM.Features.Execution.Hooks;

namespace TAOM.Features.Execution;

public static class ExecutionIoC
{
    public static void RegisterExecutionFeature(IContainer container)
    {
        container.Register<IAlignmentConfigProvider, AlignmentConfigProvider>(Reuse.Singleton);
        container.Register<IAlignmentService, AlignmentService>(Reuse.Singleton);
        container.Register<IOnExecutionAction, ExecutionActionHook>(Reuse.Singleton);
        // Phase 9b #147: originally the service behind TaomExecutionRelationModel. v1.5.0 deleted
        // vanilla's ExecutionRelationModel engine-wide, so that GameModel is gone; the same
        // per-evaluator rule now runs through IOnExecutionAction.GetRelationModifier on the Blood
        // Feud seam (ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch).
        container.Register<IExecutionRelationService, ExecutionRelationService>(Reuse.Singleton);
    }

    public static void InitializeHooks(IOnExecutionAction executionHook, IPlayerContextAdapter playerContext)
    {
        TraitLevelingHelper_OnBloodFeudStarted_Patch.Initialize(executionHook, playerContext);
        ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.Initialize(executionHook, playerContext);
    }
}
