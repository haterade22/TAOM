using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// The callback-shim Finalizer's decision, with the MCM read factored out. The enabled branch with
// an exception reaches IoC.Resolve, which this test process has not configured, so it is not
// exercised here.
[TestClass]
public class Native2ManagedBridgeTests
{
    [TestMethod]
    public void HandleOrPassThrough_WhenNativeCaptureIsOff_HandsBackTheSameExceptionWithItsThrowSite()
    {
        // A thrown exception, so it has live frames: PreserveForRethrow is a no-op on one that
        // never was, and a bare `return exception;` would pass an unthrown fixture unnoticed.
        var ex = ThrowAndCatch();

        var handedBack = Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: false);

        Assert.AreSame(ex, handedBack);
        Assert.IsTrue(ex.Data.Contains(RethrowStackPreserver.ThrowSiteDataKey),
            "the pass-through must record the throw site before Harmony rethrows (harmony-patches.md)");
    }

    [TestMethod]
    public void HandleOrPassThrough_WithNoException_ReturnsNull()
    {
        Assert.IsNull(Native2ManagedBridge.HandleOrPassThrough(null, nativeCaptureEnabled: true));
        Assert.IsNull(Native2ManagedBridge.HandleOrPassThrough(null, nativeCaptureEnabled: false));
    }

    [TestMethod]
    public void Finalizer_OnTheSuccessPath_ReturnsNull()
    {
        // Harmony calls the finalizer on every successful call with a null __exception; this is
        // the only path that runs in normal play.
        Assert.IsNull(Native2ManagedBridge.Finalizer(null));
    }

    private static InvalidOperationException ThrowAndCatch()
    {
        try { throw new InvalidOperationException("taom-006"); }
        catch (InvalidOperationException caught) { return caught; }
    }
}
