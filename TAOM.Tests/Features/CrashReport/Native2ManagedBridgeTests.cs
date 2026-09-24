using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// The callback-shim Finalizer's decision, with the MCM read factored out. The enabled branch with
// an exception reaches IoC.Resolve; no test calls IoC.Configure, so the service is unreachable and
// HandleAndSwallow takes its hand-back fallback, which is the path the enabled-branch test pins.
[TestClass]
public class Native2ManagedBridgeTests
{
    [TestInitialize]
    public void ClearTheCachedService() => CrashReportPatchHelper.ResetForUnload();

    [TestMethod]
    public void HandleOrPassThrough_WhenCaptureCannotSwallow_HandsBackAnExceptionThatKeepsItsThrowSite()
    {
        // Every non-null return of the bridge is rethrown by Harmony, so every exit must preserve
        // (maintainer decision 2026-09-24, #650). Here capture is on but the service is unreachable.
        var ex = RethrowProbe.CaughtFromTheSite();

        var handedBack = Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: true);

        Assert.AreSame(ex, handedBack);
        StringAssert.Contains(RethrowProbe.TraceAfterHarmonyRethrow(handedBack!), nameof(RethrowProbe.ThrowAtTheSite),
            "the throw site must survive Harmony's `throw <result>`");
    }

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
