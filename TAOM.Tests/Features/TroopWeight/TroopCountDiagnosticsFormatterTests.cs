using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight.Diagnostics;

namespace TAOM.Tests.Features.TroopWeight;

[TestClass]
public class TroopCountDiagnosticsFormatterTests
{
    private static DiagnosticSnapshot Snapshot(
        IReadOnlyList<DiagnosticSlot> slots,
        int totalManCount,
        int totalWounded,
        bool enableTroopWeight = true,
        int numberOfAllMembers = 0,
        int numberOfHealthyMembers = 0,
        int weightedHealthy = 0,
        int weightedWounded = 0,
        int partySizeLimit = 50)
    {
        return new DiagnosticSnapshot(
            enableTroopWeight,
            slots.Count,
            totalManCount,
            totalWounded,
            numberOfAllMembers,
            numberOfHealthyMembers,
            weightedHealthy,
            weightedWounded,
            partySizeLimit,
            slots);
    }

    [TestMethod]
    public void Format_HealthySpecialTroops_ProducesHeaderSlotAndSummary()
    {
        var slots = new List<DiagnosticSlot>
        {
            new("gondor_recruit", 20, 0, 1.0f, false),
            new("harad_elephant_rider", 10, 0, 7.0f, true),
        };
        var snapshot = Snapshot(slots, totalManCount: 30, totalWounded: 0,
            numberOfAllMembers: 30, numberOfHealthyMembers: 30, weightedHealthy: 90, weightedWounded: 0);

        var lines = TroopCountDiagnosticsFormatter.Format(snapshot);

        Assert.IsTrue(lines.Any(l => l.Contains("totalManCount=30")), "header should report raw man count");
        Assert.IsTrue(lines.Any(l => l.Contains("harad_elephant_rider")), "a per-slot line should list the special troop");
        Assert.IsTrue(lines.Any(l => l.Contains("special-currency troops in main party") && l.Contains("bodies=10")),
            "summary should count the 10 special bodies present");
    }

    [TestMethod]
    public void Format_SlotBodiesMismatchTotalManCount_EmitsMismatchWarning()
    {
        // 30 real bodies across the slots, but the cached man count reads 20 (the stale-count hypothesis).
        var slots = new List<DiagnosticSlot>
        {
            new("gondor_recruit", 20, 0, 1.0f, false),
            new("harad_elephant_rider", 10, 0, 7.0f, true),
        };
        var snapshot = Snapshot(slots, totalManCount: 20, totalWounded: 0);

        var lines = TroopCountDiagnosticsFormatter.Format(snapshot);

        Assert.IsTrue(lines.Any(l => l.Contains("MISMATCH")),
            "slot bodies (30) != TotalManCount (20) must raise a MISMATCH line");
    }

    [TestMethod]
    public void Format_SlotBodiesMatchTotalManCount_NoMismatchWarning()
    {
        var slots = new List<DiagnosticSlot>
        {
            new("gondor_recruit", 20, 0, 1.0f, false),
            new("harad_elephant_rider", 10, 0, 7.0f, true),
        };
        var snapshot = Snapshot(slots, totalManCount: 30, totalWounded: 0);

        var lines = TroopCountDiagnosticsFormatter.Format(snapshot);

        Assert.IsFalse(lines.Any(l => l.Contains("MISMATCH")),
            "matching sums must NOT raise a MISMATCH line");
    }

    [TestMethod]
    public void Format_NoSpecialCurrencyTroops_SummarySaysNone()
    {
        var slots = new List<DiagnosticSlot>
        {
            new("gondor_recruit", 20, 0, 1.0f, false),
        };
        var snapshot = Snapshot(slots, totalManCount: 20, totalWounded: 0);

        var lines = TroopCountDiagnosticsFormatter.Format(snapshot);

        Assert.IsTrue(lines.Any(l => l.Contains("special-currency troops in main party") && l.Contains("NONE")),
            "with no special troops present the summary must say NONE");
    }

    [TestMethod]
    public void Format_SpecialTroopWounded_SlotLineShowsWoundedAndSpecialFlag()
    {
        var slots = new List<DiagnosticSlot>
        {
            new("imladris_blademaster", 10, 10, 2.0f, true),
        };
        var snapshot = Snapshot(slots, totalManCount: 10, totalWounded: 10,
            numberOfAllMembers: 10, numberOfHealthyMembers: 0);

        var lines = TroopCountDiagnosticsFormatter.Format(snapshot);

        var slotLine = lines.FirstOrDefault(l => l.Contains("imladris_blademaster"));
        Assert.IsNotNull(slotLine, "the wounded special troop must have a slot line");
        Assert.IsTrue(slotLine.Contains("wounded=10"), "slot line must show the wounded count");
        Assert.IsTrue(slotLine.Contains("special=True"), "slot line must flag the special-currency troop");
    }

    [TestMethod]
    public void Format_EmptyRoster_ProducesHeaderWithoutCrash()
    {
        var snapshot = Snapshot(new List<DiagnosticSlot>(), totalManCount: 0, totalWounded: 0);

        var lines = TroopCountDiagnosticsFormatter.Format(snapshot);

        Assert.IsTrue(lines.Count > 0, "an empty roster should still emit a header line");
        Assert.IsFalse(lines.Any(l => l.Contains("MISMATCH")), "0 == 0 is not a mismatch");
    }
}
