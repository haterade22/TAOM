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
    public void TryParseStamp_RealBuildOutput_WithCommitShaSuffix_Parses()
    {
        // The ACTUAL string emitted by the build, read back off TAOM.dll with reflection.
        // Bannerlord.BuildResources appends ".{commit-sha}" of its own accord, so the stamp is NOT
        // at the end of the string. The first version of this parser did TrimEnd('Z') and failed on
        // every real assembly while these tests passed — because they asserted the format the code
        // assumed rather than the one the build produces. This case is that format, verbatim.
        const string real = "build.20260802-013132Z.46ce6436e1b538a7734a23713ec818a23afec93d";
        Assert.IsTrue(BuildStampReport.TryParseStamp("+" + real, out var stamp));
        Assert.AreEqual(new DateTime(2026, 8, 2, 1, 31, 32, DateTimeKind.Utc), stamp);
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

    // The three IsMismatched tests that used to sit here were deleted with the method itself:
    // Classify superseded it in production, and its same-build / weeks-apart / symmetry cases are
    // covered verbatim by the Classify tests below. Keeping a method alive only to keep its tests
    // green is how dead code survives a review.

    [TestMethod]
    public void BuildReport_NullAssemblies_DoesNotThrowAndSaysCannotVerify()
    {
        string report = BuildStampReport.BuildReport(null, null);
        StringAssert.Contains(report, "[BuildStamp]");
        StringAssert.Contains(report, "cannot verify");
    }

    // ---- Verdict tiers: a rebuild is not a stale pairing ----

    [TestMethod]
    public void Classify_StampsFromTheSameBuild_IsPaired()
    {
        var a = new DateTime(2026, 8, 1, 14, 30, 0, DateTimeKind.Utc);
        Assert.AreEqual(BuildPairVerdict.Paired, BuildStampReport.Classify(a, a.AddSeconds(40)));
    }

    [TestMethod]
    public void Classify_HoursApartOnOneDay_IsIncrementalRebuild_NotMismatched()
    {
        // The 2026-08-12 session: TAOM stamped 16:36:20Z at 77e2518e, TAOM.Dependencies 13:59:12Z
        // at 9c532b5e, 2h37m apart. `git diff --name-only 9c532b5e..HEAD` was docs only, so no code
        // differed and the loud MISMATCH was a false alarm. A gate that cries wolf on a docs commit
        // trains the reader to ignore it, which is worse than not having it.
        var deps = new DateTime(2026, 8, 12, 13, 59, 12, DateTimeKind.Utc);
        var main = new DateTime(2026, 8, 12, 16, 36, 20, DateTimeKind.Utc);
        Assert.AreEqual(BuildPairVerdict.IncrementalRebuild, BuildStampReport.Classify(main, deps));
    }

    [TestMethod]
    public void Classify_StampsWeeksApart_IsMismatched()
    {
        // The real #371 pairing stays loud: TAOM built 07-31, Dependencies shipped from 07-17. A
        // hand-copied stale module is days old, never hours.
        var main = new DateTime(2026, 7, 31, 10, 44, 0, DateTimeKind.Utc);
        var deps = new DateTime(2026, 7, 17, 8, 3, 0, DateTimeKind.Utc);
        Assert.AreEqual(BuildPairVerdict.Mismatched, BuildStampReport.Classify(main, deps));
    }

    [TestMethod]
    public void Classify_JustPastTheRebuildWindow_IsMismatched()
    {
        // Boundary: the rebuild tier must not swallow an overnight-stale Dependencies.
        var a = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(BuildPairVerdict.Mismatched,
            BuildStampReport.Classify(a, a.Add(BuildStampReport.RebuildTolerance).AddSeconds(1)));
        Assert.AreEqual(BuildPairVerdict.IncrementalRebuild,
            BuildStampReport.Classify(a, a.Add(BuildStampReport.RebuildTolerance)));
    }

    [TestMethod]
    public void Classify_IsSymmetric()
    {
        var a = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var b = a.AddHours(5);
        Assert.AreEqual(BuildStampReport.Classify(a, b), BuildStampReport.Classify(b, a));
    }

    [TestMethod]
    public void Classify_IncrementalRebuild_DoesNotReuseTheMismatchWording()
    {
        // The word MISMATCH is the escalation signal. Reusing it for the benign tier is exactly the
        // false alarm this change exists to remove.
        var a = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        string benign = BuildStampReport.DescribeVerdict(BuildStampReport.Classify(a, a.AddHours(3)), a, a.AddHours(3));
        string stale = BuildStampReport.DescribeVerdict(BuildStampReport.Classify(a, a.AddDays(14)), a, a.AddDays(14));

        StringAssert.Contains(stale, "MISMATCH");
        Assert.IsFalse(benign.Contains("MISMATCH"),
            "an incremental rebuild must not render with the stale-pairing wording");
        StringAssert.Contains(benign, "#371", "the benign tier still needs to be traceable to the issue");
    }

    [TestMethod]
    public void DescribeVerdict_UnknownVerdict_DoesNotRenderAsMismatch()
    {
        // A future fourth tier must not inherit the loudest wording by falling through `default`.
        // Cast an out-of-range value to stand in for that not-yet-written tier.
        var a = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        string text = BuildStampReport.DescribeVerdict((BuildPairVerdict)99, a, a.AddHours(3));

        Assert.IsFalse(text.Contains("MISMATCH"),
            "an unhandled verdict must not be reported as a stale pairing");
        StringAssert.Contains(text, "unrecognised");
    }
}
