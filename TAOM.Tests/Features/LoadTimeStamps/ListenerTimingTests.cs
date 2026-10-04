using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Tests.Features.LoadTimeStamps;

[TestClass]
public class ListenerTimingTests
{
    [TestMethod]
    public void New_HasItsNames_AndNoCalls()
    {
        var timing = new ListenerTiming("A.OnSessionLaunched", "SandBox", false);

        Assert.AreEqual("A.OnSessionLaunched", timing.Handler);
        Assert.AreEqual("SandBox", timing.Assembly);
        Assert.IsFalse(timing.IsTaom);
        Assert.AreEqual(0, timing.Calls);
        Assert.AreEqual(0L, timing.Ticks);
        Assert.AreEqual(0L, timing.MaxTicks);
        Assert.AreEqual(-1, timing.MaxArgument);
    }

    [TestMethod]
    public void Record_SumsTheCalls_AndKeepsTheFirstSlowestCallsArgument()
    {
        var timing = new ListenerTiming("M.OnNewGameCreatedPartialFollowUp", "TAOM", true);

        timing.Record(2, 0);
        timing.Record(30, 1);
        timing.Record(30, 2);
        timing.Record(1, 3);

        Assert.AreEqual(4, timing.Calls);
        Assert.AreEqual(63L, timing.Ticks);
        Assert.AreEqual(30L, timing.MaxTicks);
        Assert.AreEqual(1, timing.MaxArgument);
    }

    [TestMethod]
    public void Record_AZeroTickFirstCall_StillSetsTheArgument()
    {
        var timing = new ListenerTiming("A.OnGameLoaded", "SandBox", false);

        timing.Record(0, 7);

        Assert.AreEqual(1, timing.Calls);
        Assert.AreEqual(7, timing.MaxArgument);
        timing.Record(0, 4);
        Assert.AreEqual(7, timing.MaxArgument, "a tie must keep the first call");
    }
}
