using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class MusterPlanTests
{
    [TestMethod]
    public void Split_HundredFoot_IsFiveBatchesOfTwentySpacedByTheInterval()
    {
        var batches = MusterPlan.Split(100, 20, 4f);

        CollectionAssert.AreEqual(new[] { 20, 20, 20, 20, 20 }, batches.Select(b => b.Count).ToArray());
        CollectionAssert.AreEqual(new[] { 0f, 4f, 8f, 12f, 16f }, batches.Select(b => b.DelaySeconds).ToArray());
    }

    [TestMethod]
    public void Split_Remainder_GoesInTheLastBatch()
    {
        var batches = MusterPlan.Split(45, 20, 4f);

        CollectionAssert.AreEqual(new[] { 20, 20, 5 }, batches.Select(b => b.Count).ToArray());
    }

    [TestMethod]
    public void Split_FewerThanOneBatch_IsOneBatchNow()
    {
        var batches = MusterPlan.Split(7, 20, 4f);

        Assert.AreEqual(1, batches.Count);
        Assert.AreEqual(7, batches[0].Count);
        Assert.AreEqual(0f, batches[0].DelaySeconds);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-4)]
    public void Split_NothingToMuster_IsEmpty(int total)
    {
        Assert.AreEqual(0, MusterPlan.Split(total, 20, 4f).Count);
    }

    [TestMethod]
    public void Split_NeverLosesOrAddsASoldier()
    {
        foreach (var total in new[] { 1, 19, 20, 21, 99, 100, 101, 220 })
            Assert.AreEqual(total, MusterPlan.Split(total, 20, 4f).Sum(b => b.Count));
    }

    [TestMethod]
    public void Split_BatchSizeBelowOne_IsOneAtATime()
    {
        Assert.AreEqual(3, MusterPlan.Split(3, 0, 4f).Count);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-1f)]
    public void Split_BadInterval_MeansNoDelay(float seconds)
    {
        Assert.IsTrue(MusterPlan.Split(60, 20, seconds).All(b => b.DelaySeconds == 0f));
    }

    private static LdPoint P(float x, float y = 0f, float z = 0f) => new(x, y, z);

    [TestMethod]
    public void CountOverlaps_NewSoldierWithinRadiusOfAnEarlierOne_Counts()
    {
        Assert.AreEqual(1, MusterPlan.CountOverlaps(new[] { P(0.3f), P(5f) }, new[] { P(0f), P(10f) }, 0.4f));
    }

    [TestMethod]
    public void CountOverlaps_CountsNewSoldiers_NotPairs()
    {
        Assert.AreEqual(1, MusterPlan.CountOverlaps(new[] { P(0.1f) }, new[] { P(0f), P(0.2f), P(0.3f) }, 0.4f));
    }

    [TestMethod]
    public void CountOverlaps_NewSoldiersNextToEachOther_AreNotOverlaps()
    {
        Assert.AreEqual(0, MusterPlan.CountOverlaps(new[] { P(0f), P(0.1f) }, new[] { P(9f) }, 0.4f));
    }

    [TestMethod]
    public void CountOverlaps_BatchOneNeverMoved_AllOfBatchTwoOverlap()
    {
        var first = Enumerable.Range(0, 20).Select(i => P(i * 1.5f)).ToArray();

        Assert.AreEqual(20, MusterPlan.CountOverlaps(first, first, 0.4f));
    }

    [TestMethod]
    public void CountOverlaps_UsesHeightToo()
    {
        Assert.AreEqual(0, MusterPlan.CountOverlaps(new[] { P(0f, 0f, 3f) }, new[] { P(0f) }, 0.4f));
    }

    [TestMethod]
    public void CountOverlaps_NoEarlierSoldiers_IsZero()
    {
        Assert.AreEqual(0, MusterPlan.CountOverlaps(new[] { P(0f) }, new LdPoint[0], 0.4f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-0.4f)]
    public void CountOverlaps_BadRadius_CountsNothing(float radius)
    {
        Assert.AreEqual(0, MusterPlan.CountOverlaps(new[] { P(0f) }, new[] { P(0f) }, radius));
    }

    [TestMethod]
    public void CountOverlaps_NaNPoint_CountsNothing()
    {
        Assert.AreEqual(0, MusterPlan.CountOverlaps(new[] { P(float.NaN) }, new[] { P(0f) }, 0.4f));
    }
}
