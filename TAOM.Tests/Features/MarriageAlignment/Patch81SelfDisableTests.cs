using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Diagnostics;
using TAOM.Features.MarriageAlignment.Hooks;

namespace TAOM.Tests.Features.MarriageAlignment;

/// <summary>
/// Issue #1185: the partner-steering latch in Patch81 used to trip with no log line. It now reports the
/// fix as off, once.
/// </summary>
[TestClass]
public class Patch81SelfDisableTests
{
    private static readonly FieldInfo? ContainerField =
        typeof(TAOM.IoC).GetField("_container", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    private object? _savedContainer;
    private SelfDisableLedger _savedLedger = null!;

    [TestInitialize]
    public void Setup()
    {
        Assert.IsNotNull(ContainerField, "IoC._container moved: update this test");
        _savedContainer = ContainerField!.GetValue(null);
        _savedLedger = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
        ResetLatch();
    }

    [TestCleanup]
    public void Cleanup()
    {
        ContainerField?.SetValue(null, _savedContainer);
        SelfDisables.Ledger = _savedLedger;
        ResetLatch();
    }

    private static void ResetLatch()
    {
        typeof(Patch81_MarriageClanDraw).GetField("_service", Private)!.SetValue(null, null);
        typeof(Patch81_MarriageClanDraw).GetField("_serviceResolveFailed", Private)!.SetValue(null, false);
    }

    [TestMethod]
    public void ResolveService_WhenTheServiceIsNotRegistered_ReportsOnce()
    {
        using var container = new Container();
        ContainerField!.SetValue(null, container);
        var resolve = typeof(Patch81_MarriageClanDraw).GetMethod("ResolveService", Private)!;

        Assert.IsNull(resolve.Invoke(null, null));
        Assert.IsNull(resolve.Invoke(null, null));

        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual("Patch81_MarriageAlignment", taken[0].Id);
        StringAssert.Contains(taken[0].Reason, "IMarriageAlignmentService");
    }
}
