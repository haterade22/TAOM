using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SiegeRetreatLoot;
using TAOM.Features.SiegeRetreatLoot.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SiegeRetreatLoot;

/// <summary>
/// Wiring guards for Siege Retreat Loot (#1121). Drop the module line and Patch146 never applies; drop the statics
/// handshake and the patch has no service and leaves vanilla's retreat handling as it was.
/// </summary>
[TestClass]
public class SiegeRetreatLootWiringTests
{
    private static Container BuildContainer(SiegeRetreatLootModule module)
    {
        var container = new Container();
        module.RegisterServices(container);
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<SiegeRetreatLootModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.SiegeRetreatLoot.SiegeRetreatLootModule()"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndAppliesOneGameInitCategory()
    {
        var module = new SiegeRetreatLootModule();

        Assert.AreEqual("SiegeRetreatLoot", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch146_SiegeRetreatLoot", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, module.PatchCategories[0].Phase);
    }

    [TestMethod]
    public void TheModule_RegistersTheServiceAndItsAdapter()
    {
        var container = BuildContainer(new SiegeRetreatLootModule());

        Assert.IsNotNull(container.Resolve<SiegeRetreatLootService>());
        Assert.IsInstanceOfType(container.Resolve<ISiegeRetreatLootAdapter>(), typeof(SiegeRetreatLootAdapter));
    }

    [TestMethod]
    public void InitializeStatics_GivesThePatchItsService_AndNoRetreatLeavesNoState()
    {
        var module = new SiegeRetreatLootModule();
        var container = BuildContainer(module);

        module.InitializeStatics(container);
        try
        {
            // With a service set and no retreat, the prefix reads no engine state and sets none.
            Patch146_SiegeRetreatLoot.Prefix(null!, out bool state);
            Assert.IsFalse(state);
        }
        finally
        {
            Patch146_SiegeRetreatLoot.Initialize(null, null);
        }
    }

    [TestMethod]
    public void Prefix_NoService_SetsNoState_AndPostfixDoesNothing()
    {
        Patch146_SiegeRetreatLoot.Initialize(null, null);

        Patch146_SiegeRetreatLoot.Prefix(null!, out bool state);
        Patch146_SiegeRetreatLoot.Postfix(true);

        Assert.IsFalse(state);
    }
}
