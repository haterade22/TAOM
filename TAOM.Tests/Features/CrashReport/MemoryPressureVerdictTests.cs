using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.CrashReport.Domain;
using TAOM.Features.CrashReport.Rendering;

namespace TAOM.Tests.Features.CrashReport;

// The crash bundle's memory verdict (#385 follow-up). #385 was diagnosed BY the commit figure,
// and the bundle carried none — only WorkingSet64/PrivateMemorySize64, no commit, no headroom.
//
// The sample values below are the SAME ones MemoryPressureSamplerTests pins for the [MemSample]
// contract line, deliberately: one number set pins the log lane and the crash lane, so a drift
// between them is visible by reading two test files side by side.
[TestClass]
public class MemoryPressureVerdictTests
{
    // headroom 31646-14003 = 17643; threshold max(2048, 3164) = 3164 -> healthy. 17643*100/31646 = 55.
    private static SystemMemorySnapshot HealthySnapshot() => new SystemMemorySnapshot(
        PrivateMb: 4211, WorkingSetMb: 3900, ManagedHeapMb: 654,
        SysCommitUsedMb: 14003, SysCommitLimitMb: 31646,
        AvailPhysMb: 6200, TotalPhysMb: 16296, MemLoadPercent: 61);

    // headroom 31646-29847 = 1799 < 3164 -> low. 1799*100/31646 = 5.
    private static SystemMemorySnapshot LowHeadroomSnapshot() => new SystemMemorySnapshot(
        PrivateMb: 4211, WorkingSetMb: 3900, ManagedHeapMb: 654,
        SysCommitUsedMb: 29847, SysCommitLimitMb: 31646,
        AvailPhysMb: 310, TotalPhysMb: 16296, MemLoadPercent: 97);

    // ---- Classification ------------------------------------------------------------------

    [TestMethod]
    public void IsUnderPressure_HeadroomBelowFloor_ReturnsTrue()
        => Assert.IsTrue(MemoryPressureVerdict.IsUnderPressure(LowHeadroomSnapshot()));

    [TestMethod]
    public void IsUnderPressure_HeadroomAboveFloorAndPercent_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureVerdict.IsUnderPressure(HealthySnapshot()));

    [TestMethod]
    public void IsUnderPressure_ZeroCommitLimit_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureVerdict.IsUnderPressure(
            HealthySnapshot() with { SysCommitLimitMb = 0 }));

    [TestMethod]
    public void IsUnderPressure_NegativeCommitUsed_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureVerdict.IsUnderPressure(
            HealthySnapshot() with { SysCommitUsedMb = -1 }));

    // The anti-drift pin. A prior deep review caught the C#/Python threshold mirror diverging in
    // the integer-floor band; a THIRD copy of these constants is how that recurs. This asserts the
    // verdict never forks from the sampler, including at a non-round limit where
    // limit*10/100 floors (31646 -> 3164, not 3164.6).
    [TestMethod]
    public void IsUnderPressure_AgreesWithMemoryPressureSamplerForTheSameInputs()
    {
        long[] limits = { 0, -1, 1024, 20481, 31646, 128662 };
        // Both sides of both integer-floor edges, which is what the RCA actually asks for:
        //   limit 31646 -> threshold floors 3164.6 to 3164, so headroom 3164 (used 28482) is
        //                  healthy and headroom 3163 (used 28483) is low.
        //   limit 20481 -> threshold 2048, so headroom 2048 (used 18433) is healthy and
        //                  headroom 2047 (used 18434) is low.
        long[] useds = { -1, 0, 1, 17317, 18433, 18434, 27317, 28482, 28483, 29847, 31646, 40000 };

        foreach (var limit in limits)
        {
            foreach (var used in useds)
            {
                var snap = HealthySnapshot() with { SysCommitUsedMb = used, SysCommitLimitMb = limit };
                Assert.AreEqual(
                    MemoryPressureSampler.IsLowHeadroom(used, limit),
                    MemoryPressureVerdict.IsUnderPressure(snap),
                    $"verdict forked from the sampler at used={used} limit={limit}");
            }
        }
    }

    // ---- Headline ------------------------------------------------------------------------

    [TestMethod]
    public void IsUnderPressure_NullSnapshot_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureVerdict.IsUnderPressure(null));

    [TestMethod]
    public void FormatHeadline_NullSnapshot_ReturnsNull()
        => Assert.IsNull(MemoryPressureVerdict.FormatHeadline(null));

    [TestMethod]
    public void FormatHeadline_LowHeadroomSample_MatchesPinnedLiteral()
        => Assert.AreEqual(
            "MEMORY PRESSURE - privMB=4211 wsMB=3900 heapMB=654 (managed 15% of private), " +
            "commit 29847/31646MB, headroom 1799MB (5%)",
            MemoryPressureVerdict.FormatHeadline(LowHeadroomSnapshot()));

    [TestMethod]
    public void FormatHeadline_HealthySample_MatchesPinnedLiteral()
        => Assert.AreEqual(
            "no memory pressure - privMB=4211 wsMB=3900 heapMB=654 (managed 15% of private), " +
            "commit 14003/31646MB, headroom 17643MB (55%)",
            MemoryPressureVerdict.FormatHeadline(HealthySnapshot()));

    // Never divide by zero, and never print a percentage computed from a degenerate denominator.
    [TestMethod]
    public void FormatHeadline_ZeroPrivateMb_OmitsManagedShareClause()
    {
        var text = MemoryPressureVerdict.FormatHeadline(HealthySnapshot() with { PrivateMb = 0 });

        Assert.IsFalse(text!.Contains("managed"), text);
        Assert.IsTrue(text.Contains("privMB=0"), text);
    }

    [TestMethod]
    public void FormatHeadline_ZeroCommitLimit_ReportsUnknownAndOmitsDerivedClauses()
    {
        var text = MemoryPressureVerdict.FormatHeadline(HealthySnapshot() with { SysCommitLimitMb = 0 });

        StringAssert.StartsWith(text!, "MEMORY STATUS UNKNOWN");
        Assert.IsFalse(text!.Contains("headroom "), text);
        Assert.IsFalse(text.Contains(", commit "), text);
        Assert.IsTrue(text.Contains("privMB=4211"), text);
    }

    [TestMethod]
    public void FormatHeadline_NegativeManagedHeap_OmitsManagedShareClause()
        => Assert.IsFalse(
            MemoryPressureVerdict.FormatHeadline(HealthySnapshot() with { ManagedHeapMb = -1 })!
                .Contains("managed"));

    // A negative sysCommitUsedMb is a reachable garbage reading: MemorySampleReader derives it
    // as limit-avail, so any reading where avail exceeds total goes negative. IsLowHeadroom
    // already refuses to compute a verdict from it; the RENDER has to refuse too, or the report
    // prints "commit -1/31646MB, headroom 31647MB (100%)" - a headroom larger than the limit -
    // directly beside the "no memory pressure" label the guard correctly produced.
    // The first fix suppressed the derived numbers and still printed "no memory pressure",
    // turning a rejected reading into a confident healthy verdict. Nothing about an invalid
    // reading establishes that headroom is above the threshold, and the old version of THIS TEST
    // pinned that false claim. Absence of evidence is not evidence of health.
    [TestMethod]
    public void FormatHeadline_NegativeCommitUsed_ReportsUnknownRatherThanHealthy()
    {
        var text = MemoryPressureVerdict.FormatHeadline(HealthySnapshot() with { SysCommitUsedMb = -1 });

        StringAssert.StartsWith(text!, "MEMORY STATUS UNKNOWN");
        Assert.IsFalse(text!.Contains("no memory pressure"), text);
        Assert.IsFalse(text.Contains("headroom "), text);
        Assert.IsFalse(text.Contains(", commit "), text);
    }

    [TestMethod]
    public void FormatDetail_NegativeCommitUsed_VerdictSaysUnknownNotHealthy()
    {
        var text = MemoryPressureVerdict.FormatDetail(HealthySnapshot() with { SysCommitUsedMb = -1 });

        StringAssert.Contains(text, "MEMORY STATUS UNKNOWN");
        Assert.IsFalse(text.Contains("no memory pressure"), text);
        StringAssert.Contains(text, "NOT a statement that memory was healthy");
    }

    // A failed managed-heap read must not render as a measured 0, because "managed 0% of private"
    // would falsely strengthen exactly the native-dominance reading this verdict exists to support.
    [TestMethod]
    public void FormatHeadline_UnavailableManagedHeap_RendersUnavailableAndOmitsTheShare()
    {
        var text = MemoryPressureVerdict.FormatHeadline(HealthySnapshot() with { ManagedHeapMb = null });

        StringAssert.Contains(text!, "heapMB=<unavailable>");
        Assert.IsFalse(text!.Contains("managed "), text);
        Assert.IsFalse(text.Contains("heapMB=0"), text);
    }

    [TestMethod]
    public void FormatDetail_UnavailableManagedHeap_RendersUnavailableAndOmitsTheShare()
    {
        var text = MemoryPressureVerdict.FormatDetail(HealthySnapshot() with { ManagedHeapMb = null });

        StringAssert.Contains(text, "heapMB=<unavailable>");
        Assert.IsFalse(text.Contains("managed "), text);
    }

    [TestMethod]
    public void FormatDetail_NegativeCommitUsed_OmitsHeadroomRatherThanPrintingMoreThanTheLimit()
    {
        var text = MemoryPressureVerdict.FormatDetail(HealthySnapshot() with { SysCommitUsedMb = -1 });

        Assert.IsFalse(text.Contains("headroomMB="), text);
        // The raw reading is still shown; it is the DERIVED figures that are withheld.
        StringAssert.Contains(text, "sysCommitUsedMB=-1");
    }

    // Over-commit (used > limit) is a legitimate reading, not garbage: headroom clamps to 0,
    // and 0 headroom IS low. Previously only the boolean path covered this, never the text.
    [TestMethod]
    public void FormatHeadline_OverCommitted_ClampsHeadroomToZeroAndReportsPressure()
    {
        var text = MemoryPressureVerdict.FormatHeadline(
            HealthySnapshot() with { SysCommitUsedMb = 40000, SysCommitLimitMb = 31646 });

        StringAssert.Contains(text!, "MEMORY PRESSURE");
        StringAssert.Contains(text!, "headroom 0MB (0%)");
    }

    // ---- PercentOf ------------------------------------------------------------------------

    // Unchecked arithmetic: part*100 wraps silently, and a wrapped product over a large
    // denominator lands on 0 - a fabricated zero indistinguishable from a real 0%, reached
    // through arithmetic rather than through a null. The inputs are a P/Invoke struct read taken
    // next to a crash, which is exactly where a corrupt value comes from.
    [TestMethod]
    public void PercentOf_ProductWouldOverflow_ReturnsNullRatherThanAFabricatedZero()
    {
        Assert.IsNull(MemoryPressureVerdict.PercentOf(long.MaxValue, long.MaxValue));
        Assert.IsNull(MemoryPressureVerdict.PercentOf(long.MaxValue, 1));
        Assert.IsNull(MemoryPressureVerdict.PercentOf(5_000_000_000_000_000_000L, 1));
    }

    [TestMethod]
    public void PercentOf_QuotientExceedsInt_ReturnsNull()
        => Assert.IsNull(MemoryPressureVerdict.PercentOf(1_000_000_000_000L, 1));

    [TestMethod]
    public void PercentOf_DegenerateDenominatorsOrNegativeNumerator_ReturnNull()
    {
        Assert.IsNull(MemoryPressureVerdict.PercentOf(10, 0));
        Assert.IsNull(MemoryPressureVerdict.PercentOf(10, -1));
        Assert.IsNull(MemoryPressureVerdict.PercentOf(-1, 10));
    }

    [TestMethod]
    public void PercentOf_OrdinaryValues_FloorsLikeIntegerDivision()
    {
        Assert.AreEqual(15, MemoryPressureVerdict.PercentOf(654, 4211));
        Assert.AreEqual(100, MemoryPressureVerdict.PercentOf(10, 10));
        Assert.AreEqual(0, MemoryPressureVerdict.PercentOf(0, 10));
    }

    // ---- Detail --------------------------------------------------------------------------

    // A fabricated zero in a user-uploaded crash report is indistinguishable from a real,
    // alarming reading. Three sibling sites already enforce omit-on-failure; this is the fourth.
    [TestMethod]
    public void FormatDetail_NullSnapshot_SaysUnavailableAndFabricatesNoValues()
    {
        var text = MemoryPressureVerdict.FormatDetail(null);

        StringAssert.Contains(text, "unavailable");
        Assert.IsFalse(text.Contains("privMB="), text);
        Assert.IsFalse(text.Contains("sysCommitUsedMB="), text);
        Assert.IsFalse(text.Contains("memLoad="), text);
    }

    [TestMethod]
    public void FormatDetail_LowHeadroomSample_CarriesEveryMemSampleTokenName()
    {
        var text = MemoryPressureVerdict.FormatDetail(LowHeadroomSnapshot());

        // Byte-identical token vocabulary to MemoryPressureSampler.FormatSample, so one
        // `grep privMB` over an unzipped bundle hits the report, the manifest and the log trend.
        foreach (var token in new[]
                 {
                     "privMB=4211", "wsMB=3900", "heapMB=654",
                     "sysCommitUsedMB=29847", "sysCommitLimitMB=31646",
                     "availPhysMB=310", "totalPhysMB=16296", "memLoad=97%",
                 })
        {
            StringAssert.Contains(text, token);
        }
    }

    [TestMethod]
    public void FormatDetail_LowHeadroomSample_StatesTheVerdictAndWhyItMatters()
    {
        var text = MemoryPressureVerdict.FormatDetail(LowHeadroomSnapshot());

        StringAssert.Contains(text, "MEMORY PRESSURE");
        StringAssert.Contains(text, "headroomMB=1799");
        // The reader must be told the exception may be a symptom, not the fault.
        StringAssert.Contains(text, "symptom");
    }

    [TestMethod]
    public void FormatDetail_ZeroCommitLimit_OmitsHeadroomClause()
        => Assert.IsFalse(
            MemoryPressureVerdict.FormatDetail(HealthySnapshot() with { SysCommitLimitMb = 0 })
                .Contains("headroomMB="));

    [TestMethod]
    public void FormatDetail_ZeroPrivateMb_OmitsManagedShareClause()
        => Assert.IsFalse(
            MemoryPressureVerdict.FormatDetail(HealthySnapshot() with { PrivateMb = 0 })
                .Contains("managed"));

    [TestMethod]
    public void FormatDetail_HealthySample_DoesNotClaimPressure()
        => Assert.IsFalse(MemoryPressureVerdict.FormatDetail(HealthySnapshot()).Contains("MEMORY PRESSURE"));
}
