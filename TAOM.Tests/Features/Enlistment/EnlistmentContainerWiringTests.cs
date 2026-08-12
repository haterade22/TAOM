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
    /// <c>EnlistmentBehavior.OnGameLoaded</c> is the load hook, and it cannot be executed in a unit
    /// test — it reads <c>CampaignTime.Now</c>, which needs a live <c>Campaign</c>. What IS testable
    /// is that it resolves, which is what breaks when a dependency is added to the graph below it.
    /// The behaviour it triggers is pinned directly on the service:
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
}
