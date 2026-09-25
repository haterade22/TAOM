using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// The shared sink of the five Patch37 finalizers and the Native2Managed bridge. When it cannot
// swallow, it hands the exception back and Harmony rethrows it, so the hand-back must preserve
// the throw site (harmony-patches.md; maintainer decision 2026-09-24, #650). No test calls
// IoC.Configure, so the service is unreachable and the fallback path runs unless a test installs a
// RecordingCrashService, which pins the swallow path.
[TestClass]
public class CrashReportPatchHelperTests
{
    [TestInitialize]
    public void ClearTheCachedService() => CrashReportPatchHelper.ResetForUnload();

    [TestMethod]
    public void HandleAndSwallow_WhenTheServiceIsUnreachable_HandsBackAnExceptionThatKeepsItsThrowSite()
    {
        var ex = RethrowProbe.CaughtFromTheSite();

        var handedBack = CrashReportPatchHelper.HandleAndSwallow(ex, "taom-006.test");

        Assert.AreSame(ex, handedBack, "the hand-back must be the same instance, never a wrapper");
        StringAssert.Contains(RethrowProbe.TraceAfterHarmonyRethrow(handedBack!), nameof(RethrowProbe.ThrowAtTheSite),
            "the throw site must survive Harmony's `throw <result>`");
        Assert.IsTrue(ex.Data.Contains(RethrowStackPreserver.ThrowSiteDataKey));
    }

    [TestMethod]
    public void HandleAndSwallow_WithNoException_ReturnsNull()
    {
        Assert.IsNull(CrashReportPatchHelper.HandleAndSwallow(null, "taom-006.test"));
    }

    [TestMethod]
    public void HandleAndSwallow_WhenTheServiceCaptures_SwallowsAndPassesTheOrigin()
    {
        var service = RecordingCrashService.Install();
        var ex = RethrowProbe.CaughtFromTheSite();

        Assert.IsNull(CrashReportPatchHelper.HandleAndSwallow(ex, "taom-006.test"), "a captured exception is swallowed");

        Assert.AreEqual(1, service.Calls.Count);
        Assert.AreSame(ex, service.Calls[0].Exception);
        Assert.AreEqual("taom-006.test", service.Calls[0].Origin);
    }

    [TestMethod]
    public void HandleAndSwallow_WhenTheServiceThrows_HandsBackTheOriginalWithItsThrowSite()
    {
        var service = RecordingCrashService.Install();
        service.ThrowFromHandle = new InvalidOperationException("the service failed");
        var ex = RethrowProbe.CaughtFromTheSite();

        var handedBack = CrashReportPatchHelper.HandleAndSwallow(ex, "taom-006.test");

        Assert.AreSame(ex, handedBack, "the original, never the service's own exception");
        StringAssert.Contains(RethrowProbe.TraceAfterHarmonyRethrow(handedBack!), nameof(RethrowProbe.ThrowAtTheSite));
    }

    [TestMethod]
    public void HandleAndSwallow_ReenteredFromInsideTheService_HandsBackTheInnerException()
    {
        var service = RecordingCrashService.Install();
        var inner = RethrowProbe.CaughtFromTheSite();
        Exception? innerResult = null;
        service.During = () => innerResult = CrashReportPatchHelper.HandleAndSwallow(inner, "taom-006.inner");

        Assert.IsNull(CrashReportPatchHelper.HandleAndSwallow(RethrowProbe.CaughtFromTheSite(), "taom-006.outer"));

        Assert.AreSame(inner, innerResult, "a capture already on this thread's stack hands the new exception back");
        Assert.AreEqual(1, service.Calls.Count, "the inner exception never reaches the service");
        StringAssert.Contains(RethrowProbe.TraceAfterHarmonyRethrow(innerResult!), nameof(RethrowProbe.ThrowAtTheSite));
    }

    [TestCleanup]
    public void DropTheInstalledService() => CrashReportPatchHelper.ResetForUnload();
}
