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
        // Phase 9b #147 — service-layer wrapper used by TaomExecutionRelationModel so the
        // GameModel override body satisfies rule 4 (boundary conversion + direct delegate).
        container.Register<IExecutionRelationService, ExecutionRelationService>(Reuse.Singleton);
    }

    public static void InitializeHooks(IOnExecutionAction executionHook)
    {
        TraitLevelingHelper_OnLordExecuted_Patch.Initialize(executionHook);
    }
}
