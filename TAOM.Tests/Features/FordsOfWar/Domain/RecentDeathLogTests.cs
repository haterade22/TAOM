using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The bounded record of recent deaths: lookup, eviction of the oldest, re-recording and forgetting.</summary>
[TestClass]
public class RecentDeathLogTests
{
    [TestMethod]
    public void TryGet_RecordedKey_ReturnsSideAndOwner()
    {
        var log = new RecentDeathLog();
        log.Record(new AgentKey(1), MatchSide.East, 7);

        Assert.IsTrue(log.TryGet(new AgentKey(1), out var side, out var owner));
        Assert.AreEqual(MatchSide.East, side);
        Assert.AreEqual(7, owner);
    }

    [TestMethod]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        Assert.IsFalse(new RecentDeathLog().TryGet(new AgentKey(1), out _, out _));
    }

    [TestMethod]
    public void Record_PastTheCapacity_EvictsTheOldestFirst()
    {
        var log = new RecentDeathLog(3);
        for (var i = 1; i <= 4; i++)
            log.Record(new AgentKey(i), MatchSide.West, i);

        Assert.AreEqual(3, log.Count);
        Assert.IsFalse(log.TryGet(new AgentKey(1), out _, out _));
        Assert.IsTrue(log.TryGet(new AgentKey(2), out _, out _));
        Assert.IsTrue(log.TryGet(new AgentKey(4), out _, out _));
    }

    [TestMethod]
    public void Record_SameKeyAgain_MovesItToTheNewestWithoutGrowing()
    {
        var log = new RecentDeathLog(3);
        log.Record(new AgentKey(1), MatchSide.West, 1);
        log.Record(new AgentKey(2), MatchSide.West, 2);
        log.Record(new AgentKey(1), MatchSide.East, 9);
        log.Record(new AgentKey(3), MatchSide.West, 3);
        log.Record(new AgentKey(4), MatchSide.West, 4);

        Assert.AreEqual(3, log.Count);
        Assert.IsFalse(log.TryGet(new AgentKey(2), out _, out _));
        Assert.IsTrue(log.TryGet(new AgentKey(1), out var side, out var owner));
        Assert.AreEqual(MatchSide.East, side);
        Assert.AreEqual(9, owner);
    }

    [TestMethod]
    public void Forget_RecordedKey_RemovesIt()
    {
        var log = new RecentDeathLog();
        log.Record(new AgentKey(1), MatchSide.West, 1);

        log.Forget(new AgentKey(1));
        log.Forget(new AgentKey(2));

        Assert.AreEqual(0, log.Count);
        Assert.IsFalse(log.TryGet(new AgentKey(1), out _, out _));
    }

    [TestMethod]
    public void New_ZeroCapacity_StillKeepsTheNewestDeath()
    {
        var log = new RecentDeathLog(0);
        log.Record(new AgentKey(1), MatchSide.West, 1);
        log.Record(new AgentKey(2), MatchSide.West, 2);

        Assert.AreEqual(1, log.Count);
        Assert.IsTrue(log.TryGet(new AgentKey(2), out _, out _));
    }
}
