using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class BestRunTests
{
    [TestMethod]
    public void IsBetter_NoRecord_IsTrue()
    {
        Assert.IsTrue(new BestRun(1, 0).IsBetter(null));
    }

    [TestMethod]
    public void IsBetter_MoreWaves_WinsOverMoreKills()
    {
        Assert.IsTrue(new BestRun(3, 10).IsBetter(new BestRun(2, 500)));
        Assert.IsFalse(new BestRun(2, 500).IsBetter(new BestRun(3, 10)));
    }

    [TestMethod]
    public void IsBetter_SameWaves_MoreKillsWins()
    {
        Assert.IsTrue(new BestRun(3, 11).IsBetter(new BestRun(3, 10)));
        Assert.IsFalse(new BestRun(3, 9).IsBetter(new BestRun(3, 10)));
    }

    [TestMethod]
    public void IsBetter_Equal_IsNotBetter()
    {
        Assert.IsFalse(new BestRun(3, 10).IsBetter(new BestRun(3, 10)));
    }
}
