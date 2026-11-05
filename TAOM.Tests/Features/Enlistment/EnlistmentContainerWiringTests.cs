using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Registration guard for the two constructors on the battle-join path. DryIoc's Validate()
/// walks the dependency graph WITHOUT constructing anything, so it runs with no live Campaign.
///
/// Why this exists: the 2026-08-07 battle-join bug was a wiring failure that compiled and passed
/// every unit test. Adding a constructor dependency to a DI-resolved behavior — as the recovery
/// event wiring did to EnlistmentBattleBehavior — otherwise fails only at runtime, in-game, as
/// the feature silently not working.
///
/// Scoped to these two roots on purpose: whole-graph validation would need every cross-feature
/// module registered too, which makes the test brittle rather than protective.
/// </summary>
[TestClass]
public class EnlistmentContainerWiringTests
{
    private static IContainer BuildContainer()
    {
        var container = new Container();

        // Cross-feature dependencies owned by other registration modules.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<ICoopPresenceProvider>());
        // IPathService is registered by Main/IoC.cs, not by RegisterEnlistmentFeature. It entered
        // this graph when the status board started reading the promotion ladder and the wage table:
        // EnlistmentBattleBehavior -> IServiceMaintenanceService -> IServiceStatusService ->
        // IPromotionService / IEnlistmentContentConfigProvider -> IPathService.
        container.RegisterInstance(Substitute.For<IPathService>());
        // IPlayerContextAdapter (SiegeDefenseIoC) and IDutyOrchestrationService (DutiesIoC) entered
        // this graph when ResetSessionCaches began resetting the wait-menu presenter:
        // IServiceMaintenanceService -> IEnlistmentWaitMenuPresenter -> IEnlistmentDialogGateService
        // -> IPlayerContextAdapter, and -> IEnlistmentPlayerActionService -> IDutyOrchestrationService.
        container.RegisterInstance(Substitute.For<global::TAOM.Adapters.IPlayerContextAdapter>());
        container.RegisterInstance(Substitute.For<global::TAOM.Features.Enlistment.Duties.IDutyOrchestrationService>());

        EnlistmentIoC.RegisterEnlistmentFeature(container);
        return container;
    }

    [TestMethod]
    public void BattleBehavior_Resolvable_RecoveryEventWiringSatisfied()
    {
        // Owns the IEnlistmentReconciler.BattleJoinRequested subscription — the hourly recovery
        // path. If this constructor cannot be satisfied the feature loses its retry entirely.
        var container = BuildContainer();

        var errors = container.Validate(typeof(EnlistmentBattleBehavior));

        Assert.AreEqual(
            0,
            errors.Length,
            "EnlistmentBattleBehavior is not resolvable: "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void Reconciler_Resolvable_EncounterGuardDependencySatisfied()
    {
        // Takes IEncounterAdapter so it can tell an open loot encounter from a finished battle.
        var container = BuildContainer();

        var errors = container.Validate(typeof(IEnlistmentReconciler));

        Assert.AreEqual(
            0,
            errors.Length,
            "IEnlistmentReconciler is not resolvable: "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    /// <summary>
    /// <c>EnlistmentBehavior.OnGameLoaded</c> is the load hook. It cannot finish in a unit test on
    /// the host (it reads <c>CampaignTime.Now</c>, which needs a live <c>Campaign</c>); its routing is
    /// pinned in <c>EnlistmentSessionResetTests</c> with a sentinel thrown before that read. This
    /// test pins that it resolves, which is what breaks when a dependency is added to the graph
    /// below it. The reset itself is pinned on the service:
    /// <c>ServiceMaintenanceServiceTests.ResetSessionCaches_AlsoDropsTheArmyAdapterHandle</c>.
    /// </summary>
    [TestMethod]
    public void LifecycleBehavior_Resolvable_SessionCacheResetChainSatisfied()
    {
        var container = BuildContainer();

        var errors = container.Validate(typeof(EnlistmentBehavior));

        Assert.AreEqual(
            0,
            errors.Length,
            "EnlistmentBehavior is not resolvable: "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void MaintenanceService_Resolvable_ArmyCacheResetDependencySatisfied()
    {
        // Owns ResetSessionCaches for the whole feature; it gained IArmyMembershipAdapter on
        // 2026-08-12 so the army handle is dropped on load with every other per-session cache.
        var container = BuildContainer();

        var errors = container.Validate(typeof(IServiceMaintenanceService));

        Assert.AreEqual(
            0,
            errors.Length,
            "IServiceMaintenanceService is not resolvable: "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void MaintenanceBehavior_Resolvable_StopEndPresenterDependencySatisfied()
    {
        // The commander's settlement-left edge ends the arrival offer's stop, so this hook takes
        // the wait-menu presenter (#656).
        var container = BuildContainer();

        var errors = container.Validate(typeof(EnlistmentMaintenanceBehavior));

        Assert.AreEqual(
            0,
            errors.Length,
            "EnlistmentMaintenanceBehavior is not resolvable: "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void DeploymentService_Resolvable_ModelDependencySatisfied()
    {
        // TaomBattleInitializationModel is constructed in SubModule.OnGameStart with this service;
        // if the graph below it breaks, the model is never registered and the Order of Battle
        // screen reopens for an enlisted soldier (#576).
        var container = BuildContainer();

        var errors = container.Validate(typeof(IEnlistmentDeploymentService));

        Assert.AreEqual(
            0,
            errors.Length,
            "IEnlistmentDeploymentService is not resolvable: "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
