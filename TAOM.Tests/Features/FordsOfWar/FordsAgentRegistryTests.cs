using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsAgentRegistryTests
{
    // Reference identity is what the registry must use, so the stand-in overrides Equals to prove it is not consulted.
    private sealed class FakeAgent
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 1;
    }

    [TestMethod]
    public void Register_NewAgents_GetIncreasingKeysStartingAtOne()
    {
        var registry = new FordsAgentRegistry();

        var first = registry.Register(new FakeAgent(), MatchSide.West, 0);
        var second = registry.Register(new FakeAgent(), MatchSide.East, 2);

        Assert.AreEqual(new AgentKey(1), first);
        Assert.AreEqual(new AgentKey(2), second);
    }

    [TestMethod]
    public void Register_SameAgentTwice_ReturnsTheSameKey()
    {
        var registry = new FordsAgentRegistry();
        var agent = new FakeAgent();

        var first = registry.Register(agent, MatchSide.West, 0);
        var again = registry.Register(agent, MatchSide.West, 0);

        Assert.AreEqual(first, again);
        Assert.AreEqual(1, registry.Count);
    }

    [TestMethod]
    public void TryGetKey_DistinctObjectsThatCompareEqual_AreStillDistinct()
    {
        var registry = new FordsAgentRegistry();
        var a = new FakeAgent();
        var b = new FakeAgent();
        registry.Register(a, MatchSide.West, 0);

        Assert.IsTrue(registry.TryGetKey(a, out _));
        Assert.IsFalse(registry.TryGetKey(b, out _));
    }

    [TestMethod]
    public void TryGetKey_NullAgent_ReturnsFalse()
    {
        Assert.IsFalse(new FordsAgentRegistry().TryGetKey(null, out _));
    }

    [TestMethod]
    public void TryGetAgent_RegisteredKey_ReturnsTheAgentAndItsPlacement()
    {
        var registry = new FordsAgentRegistry();
        var agent = new FakeAgent();
        var key = registry.Register(agent, MatchSide.East, 3);

        Assert.IsTrue(registry.TryGetAgent(key, out var found, out var side, out var lane));
        Assert.AreSame(agent, found);
        Assert.AreEqual(MatchSide.East, side);
        Assert.AreEqual(3, lane);
    }

    [TestMethod]
    public void Evict_RegisteredAgent_ForgetsItAndNeverReusesTheKey()
    {
        var registry = new FordsAgentRegistry();
        var agent = new FakeAgent();
        var key = registry.Register(agent, MatchSide.West, 0);

        Assert.IsTrue(registry.Evict(agent, out var evicted));
        var next = registry.Register(new FakeAgent(), MatchSide.West, 0);

        Assert.AreEqual(key, evicted);
        Assert.IsFalse(registry.TryGetKey(agent, out _));
        Assert.IsFalse(registry.TryGetAgent(key, out _, out _, out _));
        Assert.AreNotEqual(key, next);
        Assert.AreEqual(1, registry.Count);
    }

    [TestMethod]
    public void TryGetRecentKey_LiveAgent_ReturnsItsKey()
    {
        var registry = new FordsAgentRegistry();
        var agent = new FakeAgent();
        var key = registry.Register(agent, MatchSide.West, 0);

        Assert.IsTrue(registry.TryGetRecentKey(agent, out var found));
        Assert.AreEqual(key, found);
    }

    [TestMethod]
    public void TryGetRecentKey_AgentEvictedJustNow_StillReturnsItsKey()
    {
        var registry = new FordsAgentRegistry();
        var killer = new FakeAgent();
        var key = registry.Register(killer, MatchSide.West, 0);
        registry.Evict(killer, out _);

        Assert.IsFalse(registry.TryGetKey(killer, out _));
        Assert.IsTrue(registry.TryGetRecentKey(killer, out var found));
        Assert.AreEqual(key, found);
    }

    [TestMethod]
    public void TryGetRecentKey_PastTheBoundedMemory_ForgetsTheOldest()
    {
        var registry = new FordsAgentRegistry();
        var first = new FakeAgent();
        registry.Register(first, MatchSide.West, 0);
        registry.Evict(first, out _);
        for (var i = 0; i < FordsAgentRegistry.RecentLimit; i++)
        {
            var other = new FakeAgent();
            registry.Register(other, MatchSide.West, 0);
            registry.Evict(other, out _);
        }

        Assert.IsFalse(registry.TryGetRecentKey(first, out _));
    }

    [TestMethod]
    public void TryGetRecentKey_UnknownOrNullAgent_ReturnsFalse()
    {
        var registry = new FordsAgentRegistry();

        Assert.IsFalse(registry.TryGetRecentKey(new FakeAgent(), out _));
        Assert.IsFalse(registry.TryGetRecentKey(null, out _));
    }

    [TestMethod]
    public void Evict_UnknownAgent_ReturnsFalse()
    {
        Assert.IsFalse(new FordsAgentRegistry().Evict(new FakeAgent(), out _));
    }

    [TestMethod]
    public void Snapshot_ReturnsACopyOfTheRegisteredAgents()
    {
        var registry = new FordsAgentRegistry();
        var a = new FakeAgent();
        var b = new FakeAgent();
        registry.Register(a, MatchSide.West, 1);
        registry.Register(b, MatchSide.East, 2);

        var copy = registry.Snapshot();
        registry.Evict(a, out _);

        Assert.AreEqual(2, copy.Count);
        Assert.IsTrue(copy.Any(e => ReferenceEquals(e.Agent, a) && e.Side == MatchSide.West && e.Lane == 1));
        Assert.AreEqual(1, registry.Count);
    }

    [TestMethod]
    public void Clear_DropsEverythingButKeepsCounting()
    {
        var registry = new FordsAgentRegistry();
        registry.Register(new FakeAgent(), MatchSide.West, 0);

        registry.Clear();
        var next = registry.Register(new FakeAgent(), MatchSide.West, 0);

        Assert.AreEqual(1, registry.Count);
        Assert.AreEqual(new AgentKey(2), next);
    }
}
