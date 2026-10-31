using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The small value types every other Fords rule is built on.</summary>
[TestClass]
public class BasicTypesTests
{
    [TestMethod]
    public void Opponent_West_ReturnsEast()
    {
        Assert.AreEqual(MatchSide.East, MatchSides.Opponent(MatchSide.West));
    }

    [TestMethod]
    public void Opponent_East_ReturnsWest()
    {
        Assert.AreEqual(MatchSide.West, MatchSides.Opponent(MatchSide.East));
    }

    [TestMethod]
    public void Distance_ThreeFourFive_ReturnsFive()
    {
        var a = new Vec3f(0, 0, 0);
        var b = new Vec3f(3, 4, 0);

        Assert.AreEqual(5f, a.Distance(b), 1e-5f);
    }

    [TestMethod]
    public void Distance_DifferentHeights_CountsZ()
    {
        var a = new Vec3f(0, 0, 0);
        var b = new Vec3f(0, 3, 4);

        Assert.AreEqual(5f, a.Distance(b), 1e-5f);
    }

    [TestMethod]
    public void Distance2D_DifferentHeights_IgnoresZ()
    {
        var a = new Vec3f(0, 0, 0);
        var b = new Vec3f(3, 4, 100);

        Assert.AreEqual(5f, a.Distance2D(b), 1e-5f);
    }

    [TestMethod]
    public void Lerp_Midpoint_ReturnsAverage()
    {
        var mid = Vec3f.Lerp(new Vec3f(0, 0, 0), new Vec3f(10, 20, 30), 0.5f);

        Assert.AreEqual(new Vec3f(5, 10, 15), mid);
    }

    [TestMethod]
    public void Lerp_OutOfRange_ClampsToEndpoints()
    {
        var a = new Vec3f(1, 2, 3);
        var b = new Vec3f(5, 6, 7);

        Assert.AreEqual(a, Vec3f.Lerp(a, b, -1f));
        Assert.AreEqual(b, Vec3f.Lerp(a, b, 2f));
    }

    [TestMethod]
    public void Operators_AddSubtractScale_Work()
    {
        var a = new Vec3f(1, 2, 3);
        var b = new Vec3f(4, 5, 6);

        Assert.AreEqual(new Vec3f(5, 7, 9), a + b);
        Assert.AreEqual(new Vec3f(3, 3, 3), b - a);
        Assert.AreEqual(new Vec3f(2, 4, 6), a * 2f);
        Assert.AreEqual(new Vec3f(2, 4, 6), 2f * a);
    }

    [TestMethod]
    public void AgentKey_SameValue_AreEqual()
    {
        Assert.AreEqual(new AgentKey(7), new AgentKey(7));
        Assert.AreNotEqual(new AgentKey(7), new AgentKey(8));
    }

    [TestMethod]
    public void SeededFordsRandom_SameSeed_ReplaysSequence()
    {
        var a = new SeededFordsRandom(42);
        var b = new SeededFordsRandom(42);

        for (var i = 0; i < 20; i++)
        {
            Assert.AreEqual(a.Next(1000), b.Next(1000));
            Assert.AreEqual(a.NextFloat(), b.NextFloat());
        }
    }

    [TestMethod]
    public void Next_ManyDraws_StaysInRange()
    {
        var r = new SeededFordsRandom(1);

        for (var i = 0; i < 500; i++)
        {
            var n = r.Next(6);
            Assert.IsTrue(n >= 0 && n < 6, "Next out of range: " + n);
        }
    }

    [TestMethod]
    public void Next_NonPositiveBound_ReturnsZeroWithoutThrowing()
    {
        var r = new SeededFordsRandom(1);

        Assert.AreEqual(0, r.Next(0));
        Assert.AreEqual(0, r.Next(-5));
    }

    [TestMethod]
    public void NextFloat_ManyDraws_StaysInUnitInterval()
    {
        var r = new SeededFordsRandom(9);

        for (var i = 0; i < 500; i++)
        {
            var f = r.NextFloat();
            Assert.IsTrue(f >= 0f && f < 1f, "NextFloat out of range: " + f);
        }
    }
}
