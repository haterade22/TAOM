using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
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
}
