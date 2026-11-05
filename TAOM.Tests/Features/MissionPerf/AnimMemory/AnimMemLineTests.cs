using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf.AnimMemory;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>Pins every <c>[AnimMem]</c> log line literally; the feature doc quotes these strings.</summary>
[TestClass]
public class AnimMemLineTests
{
    [TestMethod]
    public void Armed_FormatsHeader()
    {
        Assert.AreEqual(
            "[AnimMem] armed: TaleWorlds.Native.dll base=0x7FFB12340000 text=0x1000+0xA240CC loadSite=0x21E00F budgetSite=0x21E034 counter=0xDABE40 budget=0xB2E2DC budgetBytes=12582912 scanMs=23.4",
            AnimMemLine.Armed(0x7FFB12340000L, 0x1000, 0xA240CC, 0x21E00F, 0x21E034, 0xDABE40, 0xB2E2DC, 12582912, 23.44));
    }

    [TestMethod]
    public void Disabled_FormatsReason()
    {
        Assert.AreEqual(
            "[AnimMem] disabled for this process: TaleWorlds.Native.dll is not loaded in this process. No further [AnimMem] samples will be taken; [MissionPerf] is unaffected.",
            AnimMemLine.Disabled("TaleWorlds.Native.dll is not loaded in this process"));
    }

    [TestMethod]
    public void MissionStart_ZeroBudget_PctIsZero()
    {
        Assert.AreEqual(
            "[AnimMem] mission start: sample every 1 s, line every 5 s, startKB=10240 budgetKB=0 pctOfBudget=0 loadingNow=1",
            AnimMemLine.MissionStart(10485760, 0, true));
    }

    [TestMethod]
    public void OffForMission_Formats()
    {
        Assert.AreEqual(
            "[AnimMem] off for this mission: 'Enable Animation Clip Memory Probe' is off (Battle Load Diagnostics page).",
            AnimMemLine.OffForMission());
    }

    [TestMethod]
    public void MissionStart_Formats()
    {
        Assert.AreEqual(
            "[AnimMem] mission start: sample every 1 s, line every 5 s, startKB=10240 budgetKB=12288 pctOfBudget=83 loadingNow=0",
            AnimMemLine.MissionStart(10485760, 12582912, false));
    }

    [TestMethod]
    public void Periodic_Formats()
    {
        Assert.AreEqual(
            "[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 drops=2 minKB=9216 maxKB=12288 loadingSamples=0/6",
            AnimMemLine.Periodic(5.0, 12582912, 12582912, false, 2, 9437184, 12582912, 0, 6));
    }

    [TestMethod]
    public void Periodic_OverBudget_PctAbove100()
    {
        Assert.AreEqual(
            "[AnimMem] t=+10s loadedKB=15360 budgetKB=12288 pctOfBudget=125 loadingNow=1 drops=0 minKB=15360 maxKB=15360 loadingSamples=1/5",
            AnimMemLine.Periodic(10.0, 15728640, 12582912, true, 0, 15728640, 15728640, 1, 5));
    }

    [TestMethod]
    public void Summary_Formats()
    {
        Assert.AreEqual(
            "[AnimMem] summary: t=+7s samples=6 startKB=10240 endKB=12288 peakKB=12288 peakPct=100 samplesAtOrAbove90Pct=4 drops=2 loadingSamples=0 stopped=0",
            AnimMemLine.Summary(7.0, 6, 10485760, 12582912, 12582912, 12582912, 4, 2, 0, false));
    }

    [TestMethod]
    public void Stopped_Formats()
    {
        Assert.AreEqual(
            "[AnimMem] stopped for this mission after InvalidOperationException: boom",
            AnimMemLine.Stopped(new InvalidOperationException("boom")));
    }
}
