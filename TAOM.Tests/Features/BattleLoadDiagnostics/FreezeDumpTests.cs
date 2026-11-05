using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class FreezeDumpPolicyTests
{
    private const int Eighty = 3;

    [TestMethod]
    public void Disabled_NeverWrites()
        => Assert.IsFalse(FreezeDumpPolicy.ShouldWrite(false, false, Eighty, FreezeStandDown.None, false, true, null));

    [TestMethod]
    public void AlreadyWrittenThisProcess_NeverWrites()
        => Assert.IsFalse(FreezeDumpPolicy.ShouldWrite(true, true, Eighty, FreezeStandDown.None, false, true, null));

    [TestMethod]
    public void BeforeEightySeconds_NeverWrites()
    {
        for (int index = 0; index < Eighty; index++)
            Assert.IsFalse(FreezeDumpPolicy.ShouldWrite(true, false, index, FreezeStandDown.None, false, true, null));
    }

    [TestMethod]
    public void WalkOrExitSampler_NeverWrites()
    {
        Assert.IsFalse(FreezeDumpPolicy.ShouldWrite(true, false, Eighty, FreezeStandDown.PrecompileWalk, false, true, null));
        Assert.IsFalse(FreezeDumpPolicy.ShouldWrite(true, false, Eighty, FreezeStandDown.ExitSampler, false, true, null));
    }

    [TestMethod]
    public void HeapMoving_NeverWrites()
        => Assert.IsFalse(FreezeDumpPolicy.ShouldWrite(true, false, Eighty, FreezeStandDown.None, true, true, null));

    [TestMethod]
    public void StillCompiling_NeverWrites()
        => Assert.IsFalse(FreezeDumpPolicy.ShouldWrite(true, false, Eighty, FreezeStandDown.None, false, true, 10d));

    [TestMethod]
    public void FlatHeapAtEightyNoCompiles_Writes()
    {
        Assert.IsTrue(FreezeDumpPolicy.ShouldWrite(true, false, Eighty, FreezeStandDown.None, false, true, null));
        Assert.IsTrue(FreezeDumpPolicy.ShouldWrite(true, false, Eighty, FreezeStandDown.None, false, true, 70d));
    }

    [TestMethod]
    public void MissionFrameInFlight_Writes()
        => Assert.IsTrue(FreezeDumpPolicy.ShouldWrite(true, false, Eighty, FreezeStandDown.MissionFrame, false, true, null));
}

[TestClass]
public class FreezeDumpWriterTests
{
    [TestMethod]
    public void FileName_HasTimestampAndPid()
    {
        Assert.AreEqual(
            "taom_freeze_2026-11-04_20-03-30_pid1744.dmp",
            FreezeDumpWriter.FileName(new DateTime(2026, 11, 4, 20, 3, 30), 1744));
    }
}
