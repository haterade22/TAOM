using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Collectors;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class RingBufferTests
{
    [TestMethod]
    public void Push_BelowCapacity_RetainsOrder()
    {
        var sut = new RingBuffer<int>(5);
        for (int i = 1; i <= 3; i++) sut.Push(i);

        var snap = sut.Snapshot();

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, (System.Collections.ICollection)snap);
    }

    [TestMethod]
    public void Push_OverCapacity_OverwritesOldestKeepsChronological()
    {
        var sut = new RingBuffer<int>(3);
        for (int i = 1; i <= 5; i++) sut.Push(i);

        var snap = sut.Snapshot();

        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, (System.Collections.ICollection)snap);
    }

    [TestMethod]
    public void Clear_EmptiesBufferAndResetsWritePointer()
    {
        var sut = new RingBuffer<int>(3);
        sut.Push(1); sut.Push(2);
        sut.Clear();
        sut.Push(99);

        var snap = sut.Snapshot();

        CollectionAssert.AreEqual(new[] { 99 }, (System.Collections.ICollection)snap);
    }

    [TestMethod]
    public void Capacity_ZeroOrNegative_DefaultsToOne()
    {
        var sut = new RingBuffer<int>(0);
        Assert.AreEqual(1, sut.Capacity);
        sut.Push(7);
        sut.Push(8);
        var snap = sut.Snapshot();
        Assert.AreEqual(1, snap.Count);
        Assert.AreEqual(8, snap[0]);
    }

    [TestMethod]
    public void Snapshot_EmptyBuffer_ReturnsEmpty()
    {
        var sut = new RingBuffer<int>(5);
        var snap = sut.Snapshot();
        Assert.AreEqual(0, snap.Count);
    }
}
