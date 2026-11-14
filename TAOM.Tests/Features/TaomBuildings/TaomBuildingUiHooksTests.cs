using System;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.TaomBuildings.Hooks;
using Arg = NSubstitute.Arg;

namespace TAOM.Tests.Features.TaomBuildings;

/// <summary>
/// The Patch108 handle: a factory that throws is called once, logged once, and never retried. No engine type is
/// involved, so this runs without the game.
/// </summary>
[TestClass]
public class TaomBuildingUiHooksTests
{
    private static readonly FieldInfo? ContainerField =
        typeof(TAOM.IoC).GetField("_container", BindingFlags.NonPublic | BindingFlags.Static);

    private object? _savedContainer;

    [TestInitialize]
    public void Setup()
    {
        Assert.IsNotNull(ContainerField, "IoC._container moved: update this test");
        _savedContainer = ContainerField!.GetValue(null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ContainerField?.SetValue(null, _savedContainer);
        TaomBuildingUiHooks.Initialize(() => null);
    }

    [TestMethod]
    public void Hook_FactoryThrows_IsCalledOnce_AndWarnsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger);
        ContainerField!.SetValue(null, container);
        var calls = 0;
        TaomBuildingUiHooks.Initialize(() =>
        {
            calls++;
            throw new InvalidOperationException("config read failed");
        });

        Assert.IsNull(TaomBuildingUiHooks.Hook);
        Assert.IsNull(TaomBuildingUiHooks.Hook);
        Assert.IsNull(TaomBuildingUiHooks.Hook);

        Assert.AreEqual(1, calls, "a failed factory is not re-invoked");
        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("UI hook could not be built")));
    }

    [TestMethod]
    public void Hook_FactoryThrows_ReportsTheUiHookOnce()
    {
        var saved = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
        try
        {
            using var container = new Container();
            container.RegisterInstance(Substitute.For<IModLogger>());
            ContainerField!.SetValue(null, container);
            TaomBuildingUiHooks.Initialize(() => throw new InvalidOperationException("config read failed"));

            Assert.IsNull(TaomBuildingUiHooks.Hook);
            Assert.IsNull(TaomBuildingUiHooks.Hook);
            Assert.IsNull(TaomBuildingUiHooks.Hook);

            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual("Patch108_TaomBuildings.UiHook", taken[0].Id);
            StringAssert.Contains(taken[0].Reason, "config read failed");
        }
        finally
        {
            SelfDisables.Ledger = saved;
        }
    }
}
