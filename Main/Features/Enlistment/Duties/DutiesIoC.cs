using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// Issue #375 Phase 5 (field duties). NOT yet wired into <c>Main/IoC.cs</c> — the
/// feature-builder agent's report lists the exact registration call and the
/// <c>EnlistmentContentBehavior</c> wiring points the orchestrator still needs to add.
/// </summary>
internal static class DutiesIoC
{
    internal static void RegisterEnlistmentDutiesFeature(IContainer container)
    {
        container.Register<IDutyWorldAdapter, DutyWorldAdapter>(Reuse.Singleton);
        container.Register<IInquiryAdapter, InquiryAdapter>(Reuse.Singleton);

        container.Register<IDutyRotationPolicy, DutyRotationPolicy>(Reuse.Singleton);
        container.Register<IDutySelector, DutySelector>(Reuse.Singleton);
        container.Register<IFieldDutyRuntime, FieldDutyRuntime>(Reuse.Singleton);
        container.Register<IInteractiveDutyPresenter, InteractiveDutyPresenter>(Reuse.Singleton);
        container.Register<IDutyOrchestrationService, DutyOrchestrationService>(Reuse.Singleton);
    }
}
