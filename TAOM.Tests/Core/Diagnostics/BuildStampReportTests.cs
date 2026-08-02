using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Diagnostics;

namespace TAOM.Tests.Core.Diagnostics;

/// <summary>
/// Pins the pure half of the build-pairing detector. The failure this exists to catch (issue #371)
/// is a current TAOM.dll running against a stale TAOM.Dependencies.dll, which nothing on disk could
/// previously distinguish.
/// </summary>
[TestClass]
public class BuildStampReportTests
{
    [TestMethod]
    public void TryParseStamp_WellFormedInformationalVersion_ParsesUtcStamp()
    {
        Assert.IsTrue(BuildStampReport.TryParseStamp("2.0.15+build.20260801-143000Z", out var stamp));
        Assert.AreEqual(new DateTime(2026, 8, 1, 14, 30, 0, DateTimeKind.Utc), stamp);
    }

    [TestMethod]
    public void TryParseStamp_NoStampMarker_ReturnsFalse()
    {
        // Pre-2026-08-01 assemblies have a bare version and must degrade to "cannot verify",
        // never to a false confident verdict.
        Assert.IsFalse(BuildStampReport.TryParseStamp("2.0.0.0", out _));
        Assert.IsFalse(BuildStampReport.TryParseStamp(null, out _));
        Assert.IsFalse(BuildStampReport.TryParseStamp(string.Empty, out _));
    }

    [TestMethod]
    public void TryParseStamp_MalformedStamp_ReturnsFalse()
    {
        Assert.IsFalse(BuildStampReport.TryParseStamp("2.0.15+build.not-a-date", out _));
        Assert.IsFalse(BuildStampReport.TryParseStamp("2.0.15+build.", out _));
    }

    [TestMethod]
    public void IsMismatched_StampsFromTheSameBuild_ReturnsFalse()
    {
        // Both modules are produced by one build.ps1 run, so they land seconds apart.
        var a = new DateTime(2026, 8, 1, 14, 30, 0, DateTimeKind.Utc);
        var b = a.AddSeconds(40);
        Assert.IsFalse(BuildStampReport.IsMismatched(a, b, BuildStampReport.MismatchTolerance));
    }

    [TestMethod]
    public void IsMismatched_StampsWeeksApart_ReturnsTrue()
    {
        // The actual #371 pairing: TAOM built 07-31, Dependencies shipped from 07-17.
        var main = new DateTime(2026, 7, 31, 10, 44, 0, DateTimeKind.Utc);
        var deps = new DateTime(2026, 7, 17, 8, 3, 0, DateTimeKind.Utc);
        Assert.IsTrue(BuildStampReport.IsMismatched(main, deps, BuildStampReport.MismatchTolerance));
    }

    [TestMethod]
    public void IsMismatched_IsSymmetric()
    {
        var a = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var b = a.AddHours(5);
        Assert.AreEqual(
            BuildStampReport.IsMismatched(a, b, BuildStampReport.MismatchTolerance),
            BuildStampReport.IsMismatched(b, a, BuildStampReport.MismatchTolerance));
    }

    [TestMethod]
    public void BuildReport_NullAssemblies_DoesNotThrowAndSaysCannotVerify()
    {
        string report = BuildStampReport.BuildReport(null, null);
        StringAssert.Contains(report, "[BuildStamp]");
        StringAssert.Contains(report, "cannot verify");
    }
}
