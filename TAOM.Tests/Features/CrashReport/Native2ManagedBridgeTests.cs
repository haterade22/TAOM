using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// The callback-shim Finalizer's decision, with the MCM read and the recorded main-thread id factored
// out. The enabled branch with an exception reaches IoC.Resolve; no test calls IoC.Configure, so the
// service is unreachable and HandleAndSwallow takes its hand-back fallback, which is the path the
// enabled-branch tests pin.
[TestClass]
public class Native2ManagedBridgeTests
{
    private static int ThisThread => Thread.CurrentThread.ManagedThreadId;

    [TestInitialize]
    public void ClearTheCachedService() => CrashReportPatchHelper.ResetForUnload();

    [TestMethod]
    public void HandleOrPassThrough_WhenCaptureCannotSwallow_HandsBackAnExceptionThatKeepsItsThrowSite()
    {
        // Every non-null return of the bridge is rethrown by Harmony, so every exit must preserve
        // (maintainer decision 2026-09-24, #650). Here capture is on but the service is unreachable.
        var ex = RethrowProbe.CaughtFromTheSite();

        var handedBack = Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: true, ThisThread);

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

        var handedBack = Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: false, ThisThread);

        Assert.AreSame(ex, handedBack);
        Assert.IsTrue(ex.Data.Contains(RethrowStackPreserver.ThrowSiteDataKey),
            "the pass-through must record the throw site before Harmony rethrows (harmony-patches.md)");
    }

    [TestMethod]
    public void HandleOrPassThrough_WithNoException_ReturnsNull()
    {
        Assert.IsNull(Native2ManagedBridge.HandleOrPassThrough(null, nativeCaptureEnabled: true, ThisThread));
        Assert.IsNull(Native2ManagedBridge.HandleOrPassThrough(null, nativeCaptureEnabled: false, ThisThread));
    }

    [TestMethod]
    public void HandleOrPassThrough_CaptureOnAWorkerThread_MarksTheExceptionOffMainThread()
    {
        // Combat callbacks such as Mission_OnAgentRemoved can arrive off the main thread
        // (harmony-patches.md, "Which thread runs your target"); the mark is what makes
        // CrashReportService skip the Mission and Campaign reads and the inquiry there.
        var ex = ThrowAndCatch();
        int mainThread = ThisThread;

        RunOnWorkerThread(() => Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: true, mainThread));

        Assert.AreEqual(true, ex.Data[AppDomainExceptionHook.OffMainThreadDataKey],
            "a bridge capture on a thread other than the recorded main thread must be marked off-main");
    }

    [TestMethod]
    public void HandleOrPassThrough_CaptureOnTheRecordedMainThread_LeavesTheExceptionUnmarked()
    {
        var ex = ThrowAndCatch();

        Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: true, ThisThread);

        Assert.IsFalse(ex.Data.Contains(AppDomainExceptionHook.OffMainThreadDataKey),
            "a main-thread capture keeps the full Mission and Campaign sections");
    }

    [TestMethod]
    public void HandleOrPassThrough_WhenNoMainThreadWasRecorded_MarksTheExceptionOffMainThread()
    {
        // 0 is AppDomainExceptionHook.MainThreadId before Subscribe(); managed ids start at 1.
        var ex = ThrowAndCatch();

        Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: true, mainThreadId: 0);

        Assert.AreEqual(true, ex.Data[AppDomainExceptionHook.OffMainThreadDataKey],
            "with no recorded main thread the bridge cannot prove main-thread delivery, so it takes the safe path");
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

    private static void RunOnWorkerThread(Action action)
    {
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        worker.Start();
        worker.Join();
        if (failure != null) throw new AssertFailedException("the worker thread threw: " + failure);
    }
}
