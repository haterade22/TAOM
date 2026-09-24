using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// The shared sink of the five Patch37 finalizers and the Native2Managed bridge. When it cannot
// swallow, it hands the exception back and Harmony rethrows it, so the hand-back must preserve
// the throw site (harmony-patches.md; maintainer decision 2026-09-24, #650). No test calls
// IoC.Configure, so the service is unreachable here and the fallback path runs.
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
}
