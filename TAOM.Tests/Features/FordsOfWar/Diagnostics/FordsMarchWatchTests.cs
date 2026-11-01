using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

[TestClass]
public class FordsMarchWatchTests
{
    [TestMethod]
    public void IsPast_West_IsTrueFrom10MetresBeyondTheTowersArc()
    {
        Assert.IsFalse(FordsMarchWatch.IsPast(MatchSide.West, 609.9f, 600f), "9.9 m past the tower");
        Assert.IsTrue(FordsMarchWatch.IsPast(MatchSide.West, 610f, 600f), "exactly 10 m past counts");
        Assert.IsTrue(FordsMarchWatch.IsPast(MatchSide.West, 700f, 600f));
        Assert.IsFalse(FordsMarchWatch.IsPast(MatchSide.West, 400f, 600f), "short of the tower");
    }

    [TestMethod]
    public void IsPast_East_WalksTowardLowerArcs_SoPastMeansBelowTheTower()
    {
        Assert.IsFalse(FordsMarchWatch.IsPast(MatchSide.East, 590.1f, 600f), "9.9 m past the tower");
        Assert.IsTrue(FordsMarchWatch.IsPast(MatchSide.East, 590f, 600f), "exactly 10 m past counts");
        Assert.IsTrue(FordsMarchWatch.IsPast(MatchSide.East, 500f, 600f));
        Assert.IsFalse(FordsMarchWatch.IsPast(MatchSide.East, 800f, 600f), "short of the tower");
    }

    [TestMethod]
    public void IsPast_AnArcThatIsNotANumber_IsNeverPast()
    {
        Assert.IsFalse(FordsMarchWatch.IsPast(MatchSide.West, float.NaN, 600f));
        Assert.IsFalse(FordsMarchWatch.IsPast(MatchSide.East, float.NaN, 600f));
    }

    [TestMethod]
    public void MostPast_NeedsMoreThanHalfOfTheLiveUnits()
    {
        Assert.IsFalse(FordsMarchWatch.MostPast(3, 6), "exactly half is not more than half");
        Assert.IsTrue(FordsMarchWatch.MostPast(4, 6));
        Assert.IsFalse(FordsMarchWatch.MostPast(0, 0), "an empty stream has nothing past");
        Assert.IsTrue(FordsMarchWatch.MostPast(1, 1));
    }
}
