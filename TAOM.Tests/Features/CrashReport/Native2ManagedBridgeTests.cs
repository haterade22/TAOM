using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// The callback-shim Finalizer's decision, with the MCM read factored out. The enabled branch with
// an exception reaches IoC.Resolve, which this test process has not configured, so it is not
// exercised here.
[TestClass]
public class Native2ManagedBridgeTests
{
    [TestMethod]
    public void HandleOrPassThrough_WhenNativeCaptureIsOff_HandsBackTheSameException()
    {
        var ex = new InvalidOperationException("taom-006");

        Assert.AreSame(ex, Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: false));
    }

    [TestMethod]
    public void HandleOrPassThrough_WithNoException_ReturnsNull()
    {
        Assert.IsNull(Native2ManagedBridge.HandleOrPassThrough(null, nativeCaptureEnabled: true));
        Assert.IsNull(Native2ManagedBridge.HandleOrPassThrough(null, nativeCaptureEnabled: false));
    }
}
