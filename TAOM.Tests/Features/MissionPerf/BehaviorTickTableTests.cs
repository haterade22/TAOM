using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The per-behaviour-type accumulators behind <c>[TickProfile]</c>, <c>[Hitch]</c> and
/// <c>[TickSummary]</c>. Keys are ordinary <see cref="Type"/>s; one tick is one millisecond here
/// (ticksPerSecond 1000), so the expected numbers read directly.
/// </summary>
[TestClass]
public class BehaviorTickTableTests
{
    private const long Tps = 1000;

    [TestMethod]
    public void SlotFor_SameType_ReturnsSameSlot()
    {
        var table = new BehaviorTickTable();
        Assert.AreEqual(table.SlotFor(typeof(string)), table.SlotFor(typeof(string)));
    }

    [TestMethod]
    public void SlotFor_DistinctTypes_ReturnDistinctSlots()
    {
        var table = new BehaviorTickTable();
        Assert.AreNotEqual(table.SlotFor(typeof(string)), table.SlotFor(typeof(int)));
    }

    [TestMethod]
    public void SlotFor_BeyondInitialCapacity_KeepsEarlierTotals()
    {
        var table = new BehaviorTickTable();
        var types = typeof(object).Assembly.GetTypes().Distinct().Take(300).ToArray();
        Assert.AreEqual(300, types.Length);

        var first = table.SlotFor(types[0]);
        table.Record(first, 7, 64);
        foreach (var type in types)
            table.SlotFor(type);
        table.FoldFrame();

        var top = table.WindowTop(1, Tps);
        Assert.AreEqual(1, top.Count);
        Assert.AreEqual(types[0].Name, top[0].Name);
        Assert.AreEqual(7d, top[0].Ms);
        Assert.AreEqual(64L, top[0].AllocBytes);
        Assert.AreEqual(first, table.SlotFor(types[0]));
    }

    [TestMethod]
    public void FoldFrame_AccumulatesMsCallsMaxAndAllocIntoTheWindow()
    {
        var table = new BehaviorTickTable();
        var slot = table.SlotFor(typeof(string));
        table.Record(slot, 3, 100);
        table.FoldFrame();
        table.Record(slot, 5, 50);
        table.FoldFrame();

        var top = table.WindowTop(8, Tps).Single();
        Assert.AreEqual(8d, top.Ms);
        Assert.AreEqual(2, top.Calls);
        Assert.AreEqual(5d, top.MaxMs);
        Assert.AreEqual(150L, top.AllocBytes);
    }

    [TestMethod]
    public void Record_ZeroTickCalls_CountsEachCallOnce()
    {
        var table = new BehaviorTickTable();
        var slot = table.SlotFor(typeof(string));
        table.Record(slot, 0, 0);
        table.Record(slot, 0, 0);
        table.FoldFrame();

        var top = table.WindowTop(8, Tps);
        Assert.AreEqual(1, top.Count);
        Assert.AreEqual(2, top[0].Calls);
    }

    [TestMethod]
    public void WindowTop_BeforeFoldFrame_SeesNothingFromTheOpenFrame()
    {
        var table = new BehaviorTickTable();
        table.Record(table.SlotFor(typeof(string)), 4, 0);
        Assert.AreEqual(0, table.WindowTop(8, Tps).Count);
    }

    [TestMethod]
    public void WindowTop_OrdersByTotalMsDescending_ThenByName_AndTruncatesToN()
    {
        var table = new BehaviorTickTable();
        table.Record(table.SlotFor(typeof(int)), 2, 0);      // Int32
        table.Record(table.SlotFor(typeof(string)), 9, 0);   // String
        table.Record(table.SlotFor(typeof(byte)), 2, 0);     // Byte, ties Int32 on ms
        table.Record(table.SlotFor(typeof(long)), 1, 0);     // Int64, cut by N
        table.FoldFrame();

        var names = table.WindowTop(3, Tps).Select(t => t.Name).ToArray();
        CollectionAssert.AreEqual(new[] { "String", "Byte", "Int32" }, names);
    }

    [TestMethod]
    public void WindowTop_NoCalls_ReturnsEmpty()
    {
        var table = new BehaviorTickTable();
        table.SlotFor(typeof(string));
        table.FoldFrame();
        Assert.AreEqual(0, table.WindowTop(8, Tps).Count);
    }

    [TestMethod]
    public void ResetWindow_ClearsTotals_KeepsSlotsAndNames()
    {
        var table = new BehaviorTickTable();
        var slot = table.SlotFor(typeof(string));
        table.Record(slot, 4, 0);
        table.FoldFrame();
        table.ResetWindow();

        Assert.AreEqual(0, table.WindowTop(8, Tps).Count);
        Assert.AreEqual(slot, table.SlotFor(typeof(string)));
        table.Record(slot, 1, 0);
        table.FoldFrame();
        Assert.AreEqual("String", table.WindowTop(8, Tps).Single().Name);
    }

    [TestMethod]
    public void FrameTop_IsThisFramesOnly_AfterResetFrame()
    {
        var table = new BehaviorTickTable();
        var a = table.SlotFor(typeof(string));
        var b = table.SlotFor(typeof(int));
        table.Record(a, 50, 0);
        table.ResetFrame();
        table.Record(b, 3, 0);

        var top = table.FrameTop(3, Tps);
        Assert.AreEqual(1, top.Count);
        Assert.AreEqual("Int32", top[0].Name);
        Assert.AreEqual(3d, top[0].Ms);
    }

    [TestMethod]
    public void MissionTop_SpansWindows_UntilResetMission()
    {
        var table = new BehaviorTickTable();
        var slot = table.SlotFor(typeof(string));
        table.Record(slot, 3, 10);
        table.FoldFrame();
        table.ResetWindow();
        table.Record(slot, 6, 20);
        table.FoldFrame();
        table.ResetWindow();

        var whole = table.MissionTop(8, Tps).Single();
        Assert.AreEqual(9d, whole.Ms);
        Assert.AreEqual(2, whole.Calls);
        Assert.AreEqual(6d, whole.MaxMs);
        Assert.AreEqual(30L, whole.AllocBytes);

        table.ResetMission();
        Assert.AreEqual(0, table.MissionTop(8, Tps).Count);
    }
}
